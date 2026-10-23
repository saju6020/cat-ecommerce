using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 section 16 group E (I-E1..I-E4) -- an issued invoice is corrected only by a credit note of its own, optionally
/// followed by a linked corrected invoice; money goes back through refunds that record method, amount, reference, who and why
/// (US-BIL-05 AC2, FR-BIL-5, FR-ORD-17; ADR-185, ADR-186).</summary>
public sealed class BillingCreditNoteEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private static NpgsqlConnection OpenRaw()
    {
        var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        connection.Open();
        return connection;
    }

    private static async Task<HttpResponseMessage> CreditAsync(HttpClient client, Guid invoiceId, object body, string? key = null) =>
        await client.SendAsync(WithKey(HttpMethod.Post, $"/api/staff/billing/invoices/{invoiceId}/credit-notes", body, key));

    private static async Task<HttpResponseMessage> RefundAsync(HttpClient client, object body, string? key = null) =>
        await client.SendAsync(WithKey(HttpMethod.Post, "/api/staff/billing/refunds", body, key));

    /// <summary>I-E1 -- credit a paid invoice partially: the credit note takes a number from its own series, the invoice outstanding is
    /// recomputed, the excess is released to the customer as credit, and the summary shows it as free credit.</summary>
    [Fact]
    public async Task I_E1_CreditingAPaidInvoice_ReleasesTheExcessAsFreeCredit()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m);
        var invoice = await GetInvoiceAsync(context.Admin, invoiceId);
        var lineId = invoice.GetProperty("lines")[0].GetProperty("lineId").GetGuid();

        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 600m, allocations: [(invoiceId, 600m)]);
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 400m, allocations: [(invoiceId, 400m)]);

        var response = await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, invoiceId, new
        {
            kind = "Return", reason = "Returned unopened", fullCredit = false, lines = new[] { new { invoiceLineId = lineId, amount = 500m } },
        }));
        var body = await ReadAsync(response);

        var (financialYear, sequence) = ParseNumber(body.GetProperty("number").GetString()!, context.BranchCode, "CN");
        Assert.Equal(1, sequence);
        Assert.NotEmpty(financialYear);
        Assert.Equal(500m, body.GetProperty("releasedToCustomerCredit").GetDecimal());

        var after = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal(500m, AmountOf(after.GetProperty("credited")));
        Assert.Equal(500m, AmountOf(after.GetProperty("paid")));
        Assert.Equal(0m, AmountOf(after.GetProperty("outstanding")));
        Assert.Equal("Paid", after.GetProperty("status").GetString());
        var listed = Assert.Single(after.GetProperty("creditNotes").EnumerateArray());
        Assert.Equal(body.GetProperty("creditNoteId").GetGuid(), listed.GetProperty("creditNoteId").GetGuid());

        var summary = await GetSummaryAsync(context.Admin, customerId);
        Assert.Equal(500m, AmountOf(summary.GetProperty("freeCredit")));

        var detail = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/credit-notes/{body.GetProperty("creditNoteId").GetGuid()}")));
        Assert.Equal(500m, AmountOf(detail.GetProperty("total")));
        Assert.Equal(invoiceId, detail.GetProperty("invoiceId").GetGuid());

        var pdf = await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/credit-notes/{body.GetProperty("creditNoteId").GetGuid()}.pdf"));
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", pdf.Headers.CacheControl?.ToString());
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }

    /// <summary>U-E1 at the endpoint -- more than a line holds is refused; a second full credit has nothing left; an unissued invoice has no credit note.</summary>
    [Fact]
    public async Task ACreditAboveTheLine_IsRefused_AndAFullCreditTakesEverythingOnce()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 300m);
        var lineId = (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();

        var tooMuch = await CreditAsync(context.Admin, invoiceId, new { kind = "Correction", reason = "Wrong price", lines = new[] { new { invoiceLineId = lineId, amount = 300.01m } } });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Contains(BusinessViolationCodes.CreditExceedsLine, await ErrorCodesAsync(tooMuch));

        await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, invoiceId, new { kind = "Cancellation", reason = "Cancelled", fullCredit = true }));
        var nothing = await CreditAsync(context.Admin, invoiceId, new { kind = "Cancellation", reason = "Again", fullCredit = true });
        Assert.Equal(HttpStatusCode.BadRequest, nothing.StatusCode);
        Assert.Contains(BusinessViolationCodes.NothingToCredit, await ErrorCodesAsync(nothing));

        var after = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal("Credited", after.GetProperty("status").GetString());
        Assert.Equal(0m, AmountOf(after.GetProperty("outstanding")));

        var draftId = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId, 10m, 1m, taxClassId: context.ZeroTaxClassId)]);
        var onDraft = await CreditAsync(context.Admin, draftId, new { kind = "Correction", reason = "x", fullCredit = true });
        Assert.Equal(HttpStatusCode.BadRequest, onDraft.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvoiceNotIssued, await ErrorCodesAsync(onDraft));
    }

    /// <summary>U-E3 at the endpoint -- an unpaid invoice releases nothing; the outstanding falls by the credit; credit notes number 1, 2 in their own series.</summary>
    [Fact]
    public async Task CreditingAnUnpaidInvoice_ReleasesNothing_AndTheNumbersAreGapless()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, invoiceNumber, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m);
        var lineId = (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();

        var first = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, invoiceId,
            new { kind = "Goodwill", reason = "Apology", lines = new[] { new { invoiceLineId = lineId, amount = 300m } } })));
        var second = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, invoiceId,
            new { kind = "Goodwill", reason = "More apology", lines = new[] { new { invoiceLineId = lineId, amount = 100m } } })));

        Assert.Equal(0m, first.GetProperty("releasedToCustomerCredit").GetDecimal());
        Assert.Equal(1, ParseNumber(first.GetProperty("number").GetString()!, context.BranchCode, "CN").Sequence);
        Assert.Equal(2, ParseNumber(second.GetProperty("number").GetString()!, context.BranchCode, "CN").Sequence);
        Assert.NotEqual(invoiceNumber, first.GetProperty("number").GetString());

        var after = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal(600m, AmountOf(after.GetProperty("outstanding")));
        Assert.Equal(2, after.GetProperty("creditNotes").GetArrayLength());
    }

    /// <summary>I-E2 -- a corrected draft from the credit note, edit a line, issue it: the new invoice corrects the old; the old shows correctedBy (US-BIL-05 AC2).</summary>
    [Fact]
    public async Task I_E2_ACorrectedDraft_IsLinkedBothWays_AndIssuesLikeAnyInvoice()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m);
        var lineId = (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();

        var note = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, invoiceId,
            new { kind = "Correction", reason = "Wrong price", fullCredit = true })));
        var creditNoteId = note.GetProperty("creditNoteId").GetGuid();

        var created = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync(
            $"/api/staff/billing/invoices/{invoiceId}/corrected-draft", new { creditNoteId }, Json)));
        var draftId = created.GetProperty("invoiceId").GetGuid();

        var again = await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{invoiceId}/corrected-draft", new { creditNoteId }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains(BusinessViolationCodes.CorrectedDraftInvalid, await ErrorCodesAsync(again));

        var draft = await GetInvoiceAsync(context.Admin, draftId);
        Assert.Equal("Draft", draft.GetProperty("status").GetString());
        Assert.Equal(invoiceId, draft.GetProperty("correctsInvoiceId").GetGuid());
        Assert.Equal(creditNoteId, draft.GetProperty("correctsCreditNoteId").GetGuid());
        var draftLine = Assert.Single(draft.GetProperty("lines").EnumerateArray());

        var stale = await context.Admin.PutAsJsonAsync($"/api/staff/billing/invoices/{draftId}/lines", new
        {
            expectedVersion = draft.GetProperty("version").GetInt32() + 5,
            lines = new[] { new { description = "Corrected", quantity = 1m, unitPrice = 800m, lineDiscount = 0m, taxClassId = context.ZeroTaxClassId } },
        }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        await ExpectAsync(HttpStatusCode.OK, await context.Admin.PutAsJsonAsync($"/api/staff/billing/invoices/{draftId}/lines", new
        {
            expectedVersion = draft.GetProperty("version").GetInt32(),
            lines = new[] { new { description = "Corrected", quantity = 1m, unitPrice = 800m, lineDiscount = 0m, taxClassId = context.ZeroTaxClassId } },
        }, Json));
        Assert.NotEqual(Guid.Empty, draftLine.GetProperty("lineId").GetGuid());

        var edited = await GetInvoiceAsync(context.Admin, draftId);
        var issued = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync(
            $"/api/staff/billing/invoices/{draftId}/issue", new { expectedVersion = edited.GetProperty("version").GetInt32() }, Json)));
        Assert.StartsWith("INV", issued.GetProperty("number").GetString());

        var corrected = await GetInvoiceAsync(context.Admin, draftId);
        Assert.Equal(800m, AmountOf(corrected.GetProperty("totals").GetProperty("total")));
        Assert.Equal(invoiceId, corrected.GetProperty("correctsInvoiceId").GetGuid());

        var original = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal(draftId, original.GetProperty("correctedByInvoiceId").GetGuid());
        Assert.NotEqual(Guid.Empty, lineId);

        // Only a corrected draft may have its lines edited this way.
        var plain = await CreateDraftAsync(context.Admin, customerId, [await RecordChargeAsync(context, customerId, 10m, 1m, taxClassId: context.ZeroTaxClassId)]);
        var notCorrected = await context.Admin.PutAsJsonAsync($"/api/staff/billing/invoices/{plain}/lines", new
        {
            expectedVersion = (await GetInvoiceAsync(context.Admin, plain)).GetProperty("version").GetInt32(),
            lines = new[] { new { description = "x", quantity = 1m, unitPrice = 1m, lineDiscount = 0m, taxClassId = context.ZeroTaxClassId } },
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, notCorrected.StatusCode);
        Assert.Contains(BusinessViolationCodes.CorrectedDraftInvalid, await ErrorCodesAsync(notCorrected));
    }

    /// <summary>I-E3 -- refund 200 cash from the credit a credit note released: the refund is recorded, free credit falls, and the
    /// customer gets a refund message that never carries the reason.</summary>
    [Fact]
    public async Task I_E3_ARefundFromReleasedCredit_IsRecorded_ReducesFreeCredit_AndEnqueuesTheMessage()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m);
        var lineId = (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();
        var payment = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 1000m, allocations: [(invoiceId, 1000m)]);
        var paymentId = payment.GetProperty("paymentId").GetGuid();

        var note = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, invoiceId,
            new { kind = "Return", reason = "Returned", lines = new[] { new { invoiceLineId = lineId, amount = 500m } } })));
        var creditNoteId = note.GetProperty("creditNoteId").GetGuid();
        Assert.Equal(500m, AmountOf((await GetSummaryAsync(context.Admin, customerId)).GetProperty("freeCredit")));

        var refund = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await RefundAsync(context.Admin, new
        {
            paymentId, creditNoteId, method = "CashInStore", amount = 200m, reason = "Customer asked for cash back, secret-reason-text",
        })));
        var refundId = refund.GetProperty("refundId").GetGuid();

        Assert.Equal(300m, AmountOf((await GetSummaryAsync(context.Admin, customerId)).GetProperty("freeCredit")));

        var detail = await GetPaymentAsync(context.Admin, paymentId);
        var listed = Assert.Single(detail.GetProperty("refunds").EnumerateArray());
        Assert.Equal(refundId, listed.GetProperty("refundId").GetGuid());
        Assert.Equal(note.GetProperty("number").GetString(), listed.GetProperty("creditNoteNumber").GetString());
        Assert.Equal(200m, AmountOf(detail.GetProperty("refunded")));

        var page = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync("/api/staff/billing/refunds")));
        Assert.Contains(page.GetProperty("items").EnumerateArray(), item => item.GetProperty("refundId").GetGuid() == refundId);

        var messages = await WithDbAsync(db => db.Set<NotificationRequestAggregate>().AsNoTracking()
            .Where(n => n.TemplateKey == NotificationTemplateCatalogue.BillingRefundIssued && n.RecipientId == customerId).ToListAsync());
        var message = Assert.Single(messages);
        Assert.DoesNotContain(message.Variables.Values, v => v.Contains("secret-reason-text"));
        Assert.Contains("200.00", message.Variables["amountText"]);
        Assert.Equal(note.GetProperty("number").GetString(), message.Variables["creditNoteNumber"]);

        // More than is available is refused, naming what is.
        var tooMuch = await RefundAsync(context.Admin, new { paymentId, method = "CashInStore", amount = 300.01m, reason = "Too much" });
        Assert.Equal(HttpStatusCode.BadRequest, tooMuch.StatusCode);
        Assert.Contains(BusinessViolationCodes.RefundExceedsAvailable, await ErrorCodesAsync(tooMuch));
    }

    /// <summary>I-E4 -- a deposit refunded directly (no credit note) is allowed with a reason, and the reason is audited.</summary>
    [Fact]
    public async Task I_E4_ADepositRefundedDirectly_IsAllowed_AndTheReasonIsAudited()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var stayId = Guid.NewGuid();
        var deposit = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 5000m,
            earmark: new { kind = "BoardingStay", sourceId = stayId, label = "Stay deposit" });
        var paymentId = deposit.GetProperty("paymentId").GetGuid();

        var refund = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await RefundAsync(context.Admin, new
        {
            paymentId, method = "CashInStore", amount = 5000m, reason = "Stay cancelled, deposit returned (BR-12)",
        })));
        var refundId = refund.GetProperty("refundId").GetGuid();

        var audit = await WithDbAsync(db => db.Set<AuditEntryAggregate>().AsNoTracking()
            .Where(a => a.EntityId == refundId && a.Category == AuditCategory.BusinessEvent && a.Action == "RefundIssued").ToListAsync());
        var row = Assert.Single(audit);
        Assert.Contains("Stay cancelled, deposit returned (BR-12)", row.AfterValue);

        Assert.Equal(0m, AmountOf((await GetPaymentAsync(context.Admin, paymentId)).GetProperty("summary").GetProperty("unallocated")));
    }

    /// <summary>U-E4 at the endpoint -- a transfer payment cannot be refunded in cash; a bank-transfer refund needs a reference; a card number is refused.</summary>
    [Fact]
    public async Task RefundRules_MethodReferenceAndCardNumber_AreEnforced()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var transfer = await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 1000m, reference: "TRX-12345");
        var cash = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 1000m);

        var wrongMethod = await RefundAsync(context.Admin, new { paymentId = transfer.GetProperty("paymentId").GetGuid(), method = "CashInStore", amount = 10m, reason = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, wrongMethod.StatusCode);
        Assert.Contains(BusinessViolationCodes.RefundMethodNotAllowed, await ErrorCodesAsync(wrongMethod));

        var noReference = await RefundAsync(context.Admin, new { paymentId = transfer.GetProperty("paymentId").GetGuid(), method = "BankTransfer", amount = 10m, reason = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, noReference.StatusCode);
        Assert.Contains(BusinessViolationCodes.ReferenceRequired, await ErrorCodesAsync(noReference));

        var card = await RefundAsync(context.Admin, new { paymentId = cash.GetProperty("paymentId").GetGuid(), method = "CashInStore", amount = 10m, reason = "card 4111 1111 1111 1111" });
        Assert.Equal(HttpStatusCode.BadRequest, card.StatusCode);
        Assert.Contains(BusinessViolationCodes.ReferenceLooksLikeCardNumber, await ErrorCodesAsync(card));
        Assert.DoesNotContain("4111 1111", await BodyAsync(card));

        await ExpectAsync(HttpStatusCode.Created, await RefundAsync(context.Admin, new
        { paymentId = transfer.GetProperty("paymentId").GetGuid(), method = "BankTransfer", amount = 10m, reference = "REF-9", reason = "Returned" }));
    }

    /// <summary>ADR-187 -- a repeated Idempotency-Key returns the original credit note and refund with 200 and writes nothing twice.</summary>
    [Fact]
    public async Task ACreditNoteAndARefund_AreIdempotent_ByTheirKey()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 400m);
        var payment = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 400m, allocations: [(invoiceId, 400m)]);
        var key = "ck-" + Guid.NewGuid().ToString("N");

        var first = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, invoiceId, new { kind = "Goodwill", reason = "Sorry", fullCredit = true }, key)));
        var replay = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await CreditAsync(context.Admin, invoiceId, new { kind = "Goodwill", reason = "Sorry", fullCredit = true }, key)));
        Assert.Equal(first.GetProperty("creditNoteId").GetGuid(), replay.GetProperty("creditNoteId").GetGuid());
        Assert.Equal(first.GetProperty("number").GetString(), replay.GetProperty("number").GetString());
        Assert.Equal(1, await CountAsync<CreditNoteAggregate>(c => c.InvoiceId == invoiceId));

        var refundKey = "rk-" + Guid.NewGuid().ToString("N");
        var paymentId = payment.GetProperty("paymentId").GetGuid();
        var refund = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await RefundAsync(context.Admin, new { paymentId, method = "CashInStore", amount = 100m, reason = "Back" }, refundKey)));
        var refundReplay = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await RefundAsync(context.Admin, new { paymentId, method = "CashInStore", amount = 100m, reason = "Back" }, refundKey)));
        Assert.Equal(refund.GetProperty("refundId").GetGuid(), refundReplay.GetProperty("refundId").GetGuid());
        Assert.Equal(1, await CountAsync<RefundAggregate>(r => r.PaymentId == paymentId));
    }

    /// <summary>ADR-180 -- a credit note and a refund are append-only in the database: an update or a delete is refused.</summary>
    [Fact]
    public async Task CreditNotesAndRefunds_CannotBeChangedOrDeleted_InTheDatabase()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 400m);
        var payment = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 400m);
        var note = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, invoiceId, new { kind = "Goodwill", reason = "Sorry", fullCredit = true })));
        var refund = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await RefundAsync(context.Admin, new
        { paymentId = payment.GetProperty("paymentId").GetGuid(), method = "CashInStore", amount = 50m, reason = "Back" })));

        await using var connection = OpenRaw();
        foreach (var sql in new[]
        {
            $"UPDATE billing.credit_note SET reason = 'edited' WHERE \"Id\" = '{note.GetProperty("creditNoteId").GetGuid()}'",
            $"DELETE FROM billing.credit_note WHERE \"Id\" = '{note.GetProperty("creditNoteId").GetGuid()}'",
            $"UPDATE billing.refund SET amount = 1 WHERE \"Id\" = '{refund.GetProperty("refundId").GetGuid()}'",
            $"DELETE FROM billing.refund WHERE \"Id\" = '{refund.GetProperty("refundId").GetGuid()}'",
            $"UPDATE billing.credit_note_line SET gross_amount = 1 WHERE credit_note_id = '{note.GetProperty("creditNoteId").GetGuid()}'",
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("P0001", error.SqlState);
        }
    }

    /// <summary>Authority -- a vet may not issue a credit note or a refund; the invoice of another branch is not found.</summary>
    [Fact]
    public async Task CreditNotesAndRefunds_AreForTheirRoles_AndTheirBranch()
    {
        var mine = await CreateBillingContextAsync();
        var other = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(mine);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(mine, customerId, 100m);
        var (vet, _) = await StaffClientAsync(mine, StaffRoles.Vet);

        Assert.Equal(HttpStatusCode.Forbidden, (await CreditAsync(vet, invoiceId, new { kind = "Goodwill", reason = "x", fullCredit = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RefundAsync(vet, new { paymentId = Guid.NewGuid(), method = "CashInStore", amount = 1m, reason = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CreditAsync(other.Admin, invoiceId, new { kind = "Goodwill", reason = "x", fullCredit = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await RefundAsync(other.Admin, new { paymentId = Guid.NewGuid(), method = "CashInStore", amount = 1m, reason = "x" })).StatusCode);
    }
}
