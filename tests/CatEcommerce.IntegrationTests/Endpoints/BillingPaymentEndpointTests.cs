using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 §16 group D (I-D1…I-D9, I-D11) -- money received is recorded by a named person against a reference, applied
/// to invoices, approved by a different person above the threshold, and corrected by reversal -- never edited (US-BIL-03, 04, 11;
/// ADR-182, 183, 185, 187).</summary>
public sealed class BillingPaymentEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private static NpgsqlConnection OpenRaw()
    {
        var connection = new NpgsqlConnection(TestDatabase.ConnectionString);
        connection.Open();
        return connection;
    }

    // ── I-D1, I-D2 ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-D1 -- 400 cash against a 1 000 invoice: accepted, with a receipt number, the invoice part paid and 600 outstanding.</summary>
    [Fact]
    public async Task I_D1_APartPayment_IsAccepted_WithAReceipt_AndTheInvoiceIsPartPaid()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, number, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m);

        var payment = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 400m, allocations: [(invoiceId, 400m)]);

        Assert.Equal("Accepted", payment.GetProperty("status").GetString());
        Assert.Matches(@"^R-\d{6}$", payment.GetProperty("receiptNumber").GetString());
        var applied = Assert.Single(payment.GetProperty("applied").EnumerateArray());
        Assert.Equal(number, applied.GetProperty("number").GetString());
        Assert.Equal(400m, AmountOf(applied.GetProperty("amount")));
        Assert.Equal(0m, AmountOf(payment.GetProperty("unallocated")));
        Assert.Equal(600m, AmountOf(Assert.Single(payment.GetProperty("invoiceOutstanding").EnumerateArray()).GetProperty("outstanding")));

        var invoice = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal("PartPaid", invoice.GetProperty("status").GetString());
        Assert.Equal(600m, AmountOf(invoice.GetProperty("outstanding")));
        var onInvoice = Assert.Single(invoice.GetProperty("payments").EnumerateArray());
        Assert.Equal(400m, AmountOf(onInvoice.GetProperty("amount")));

        var summary = await GetSummaryAsync(context.Admin, customerId);
        Assert.Equal(600m, AmountOf(summary.GetProperty("outstanding")));
        Assert.Equal(0m, AmountOf(summary.GetProperty("freeCredit")));
    }

    /// <summary>I-D2 -- then 350 by card (terminal slip) and 250 by transfer (reference): the invoice is paid after the third.</summary>
    [Fact]
    public async Task I_D2_SeveralPaymentsOfDifferentMethods_ClearTheBalance()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m);

        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 400m, allocations: [(invoiceId, 400m)]);
        Assert.Equal("PartPaid", (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("status").GetString());

        await RecordPaymentAsync(context.Admin, customerId, "CardInStore", 350m, reference: "004512", allocations: [(invoiceId, 350m)]);
        Assert.Equal("PartPaid", (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("status").GetString());

        await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 250m, reference: "TRX-2026-0001", allocations: [(invoiceId, 250m)]);

        var invoice = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal("Paid", invoice.GetProperty("status").GetString());
        Assert.Equal(0m, AmountOf(invoice.GetProperty("outstanding")));
        Assert.Equal(3, invoice.GetProperty("payments").GetArrayLength());
        Assert.Equal(["CashInStore", "CardInStore", "BankTransfer"], invoice.GetProperty("payments").EnumerateArray().Select(p => p.GetProperty("method").GetString()!).ToArray());
    }

    [Fact]
    public async Task APaymentOverTheOutstanding_IsRefused_NamingTheAvailableAmount()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, number, _) = await IssueSimpleInvoiceAsync(context, customerId, 300m);

        var response = await RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 500m, allocations: [(invoiceId, 350m)]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Contains(BusinessViolationCodes.AllocationExceedsOutstanding, body);
        Assert.Contains($"Only BDT 300.00 is outstanding on {number}.", body);
        Assert.Equal(0, await CountAsync<PaymentAggregate>(p => p.CustomerId == customerId));
    }

    [Fact]
    public async Task AnOverpayment_BecomesCustomerCredit_AndIsListedAsFreeCredit()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 300m);

        var payment = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 500m, allocations: [(invoiceId, 300m)]);

        Assert.Equal(200m, AmountOf(payment.GetProperty("unallocated")));
        var summary = await GetSummaryAsync(context.Admin, customerId);
        Assert.Equal(200m, AmountOf(summary.GetProperty("freeCredit")));
        Assert.Equal(0m, AmountOf(summary.GetProperty("outstanding")));
    }

    [Fact]
    public async Task TheMethodFlags_DecideWhatMustBeSupplied_AndANonsenseMethodIs400()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);

        var noSlip = await RecordPaymentRawAsync(context.Admin, customerId, "CardInStore", 100m);
        Assert.Equal(HttpStatusCode.BadRequest, noSlip.StatusCode);
        Assert.Contains(BusinessViolationCodes.ReferenceRequired, await BodyAsync(noSlip));

        var noCollector = await RecordPaymentRawAsync(context.Admin, customerId, "CashOnDelivery", 100m);
        Assert.Contains(BusinessViolationCodes.CollectorRequired, await BodyAsync(noCollector));

        var cod = await RecordPaymentAsync(context.Admin, customerId, "CashOnDelivery", 100m, collector: new { kind = "Courier", courierCode = "PATHAO", courierName = "Pathao Courier" });
        var detail = await GetPaymentAsync(context.Admin, cod.GetProperty("paymentId").GetGuid());
        Assert.Equal("Courier", detail.GetProperty("collector").GetProperty("kind").GetString());
        Assert.Equal("PATHAO", detail.GetProperty("collector").GetProperty("courierCode").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await RecordPaymentRawAsync(context.Admin, customerId, "Bitcoin", 100m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 0m)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 100m, receivedOn: Today.AddDays(-32))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 100m, receivedOn: Today.AddDays(1))).StatusCode);
    }

    // ── I-D3, I-D4 ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-D3 -- above the threshold the payment waits: the invoice is unchanged, /approvals lists it, the recorder cannot approve
    /// it (400 ApproverMustDiffer), a different accountant can and the allocation is then applied (US-BIL-11 AC2).</summary>
    [Fact]
    public async Task I_D3_AboveTheThreshold_APaymentWaitsForADifferentApprover()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 90_000m);
        var (recorder, recorderId) = await StaffClientAsync(context, StaffRoles.Accountant);
        var (approver, _) = await StaffClientAsync(context, StaffRoles.Accountant);

        var pending = await RecordPaymentAsync(recorder, customerId, "BankTransfer", 60_000m, reference: "TRX-BIG-1", allocations: [(invoiceId, 55_000m)]);
        var paymentId = pending.GetProperty("paymentId").GetGuid();

        Assert.Equal("PendingApproval", pending.GetProperty("status").GetString());
        Assert.Empty(pending.GetProperty("applied").EnumerateArray());
        Assert.Equal(90_000m, AmountOf((await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("outstanding")));

        var approvals = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await approver.GetAsync("/api/staff/billing/approvals")));
        var entry = approvals.EnumerateArray().Single(a => a.GetProperty("paymentId").GetGuid() == paymentId);
        Assert.True(entry.GetProperty("canApprove").GetBoolean());
        Assert.Equal(recorderId, entry.GetProperty("recordedBy").GetGuid());

        var ownView = await ReadAsync(await recorder.GetAsync("/api/staff/billing/approvals"));
        Assert.False(ownView.EnumerateArray().Single(a => a.GetProperty("paymentId").GetGuid() == paymentId).GetProperty("canApprove").GetBoolean());

        var own = await recorder.PostAsync($"/api/staff/billing/payments/{paymentId}/approve", null);
        Assert.Equal(HttpStatusCode.BadRequest, own.StatusCode);
        Assert.Contains(BusinessViolationCodes.ApproverMustDiffer, await BodyAsync(own));

        var approved = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await approver.PostAsync($"/api/staff/billing/payments/{paymentId}/approve", null)));
        Assert.Equal(55_000m, AmountOf(Assert.Single(approved.GetProperty("applied").EnumerateArray()).GetProperty("amount")));
        Assert.Equal(5_000m, AmountOf(approved.GetProperty("leftUnallocated")));

        var invoice = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal(35_000m, AmountOf(invoice.GetProperty("outstanding")));
        Assert.Equal("Accepted", (await GetPaymentAsync(context.Admin, paymentId)).GetProperty("summary").GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await approver.PostAsync($"/api/staff/billing/payments/{paymentId}/approve", null)).StatusCode); // not pending any more
    }

    [Fact]
    public async Task I_D3_ARejectedPayment_AppliesNothing_AndNeedsAReason()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 90_000m);
        var (recorder, _) = await StaffClientAsync(context, StaffRoles.Accountant);
        var (approver, _) = await StaffClientAsync(context, StaffRoles.Accountant);
        var pending = await RecordPaymentAsync(recorder, customerId, "CashInStore", 60_000m, allocations: [(invoiceId, 60_000m)]);
        var paymentId = pending.GetProperty("paymentId").GetGuid();

        Assert.Equal(HttpStatusCode.BadRequest, (await approver.PostAsJsonAsync($"/api/staff/billing/payments/{paymentId}/reject", new { reason = "" }, Json)).StatusCode);
        await ExpectAsync(HttpStatusCode.NoContent, await approver.PostAsJsonAsync($"/api/staff/billing/payments/{paymentId}/reject", new { reason = "Counted wrongly" }, Json));

        var detail = await GetPaymentAsync(context.Admin, paymentId);
        Assert.Equal("Rejected", detail.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal("Counted wrongly", detail.GetProperty("trail").GetProperty("rejectionReason").GetString());
        Assert.Equal(90_000m, AmountOf((await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("outstanding")));
    }

    [Fact]
    public async Task I_D4_ReceptionCannotApprove_ReverseOrReleaseAnEarmark()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (reception, _) = await StaffClientAsync(context, StaffRoles.Reception);
        var payment = await RecordPaymentAsync(reception, customerId, "CashInStore", 100m);
        var paymentId = payment.GetProperty("paymentId").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync($"/api/staff/billing/payments/{paymentId}/approve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync("/api/staff/billing/approvals")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsJsonAsync($"/api/staff/billing/payments/{paymentId}/reverse", new { reason = "x" }, Json)).StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, (await reception.PostAsJsonAsync($"/api/staff/billing/payments/{paymentId}/allocations", new { allocations = new[] { new { invoiceId = Guid.NewGuid(), amount = 1 } } }, Json)).StatusCode); // Reception may allocate (Appendix D: W)
    }

    // ── I-D5 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-D5 -- the same payment submitted several times at once with ONE key: one payment, and every response carries its id
    /// (ADR-187: a double-clicked "Record payment" must not record twice).</summary>
    [Fact]
    public async Task I_D5_ADoubleSubmitWithOneKey_RecordsOnePayment_AndBothResponsesCarryItsId()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var key = "pay-" + Guid.NewGuid().ToString("N");

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 250m, idempotencyKey: key))));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, $"{(int)r.StatusCode}"));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);

        var ids = new HashSet<Guid>();
        foreach (var response in responses) ids.Add((await ReadAsync(response)).GetProperty("paymentId").GetGuid());

        Assert.Single(ids);
        Assert.Equal(1, await CountAsync<PaymentAggregate>(p => p.CustomerId == customerId));
    }

    [Fact]
    public async Task TheSameKeySequentially_ReplaysWith200_AndAKeyOnAnotherCustomerConflicts()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var key = "seq-" + Guid.NewGuid().ToString("N");

        var first = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 100m, idempotencyKey: key)));
        var second = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 100m, idempotencyKey: key)));
        Assert.Equal(first.GetProperty("paymentId").GetGuid(), second.GetProperty("paymentId").GetGuid());
        Assert.Equal(first.GetProperty("receiptNumber").GetString(), second.GetProperty("receiptNumber").GetString());

        var other = await SeedCustomerAsync(context);
        var clash = await RecordPaymentRawAsync(context.Admin, other, "CashInStore", 100m, idempotencyKey: key);
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Contains(BusinessViolationCodes.IdempotencyKeyConflict, await BodyAsync(clash));

        Assert.Equal(HttpStatusCode.BadRequest, (await RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 100m, idempotencyKey: new string('k', 101))).StatusCode);
    }

    // ── I-D6 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-D6 / C-5 -- a card number is refused, and the digits appear in no log line and no audit row.</summary>
    [Fact]
    public async Task I_D6_ACardNumberAsAReferenceOrANote_IsRefused_AndNeverLoggedOrAudited()
    {
        var (logged, logs) = Factory.WithCapturedLogs();
        using var client = logged.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = admin.Mobile, password = admin.Password }, Json);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        var context = await CreateBillingContextAsync();
        await AssignAsync(context.Admin, admin.StaffAccountId, context.BranchId);
        client.DefaultRequestHeaders.Add("X-Branch-Id", context.BranchId.ToString());
        var customerId = await SeedCustomerAsync(context);

        var asReference = await RecordPaymentRawAsync(client, customerId, "CardInStore", 100m, reference: "4111 1111 1111 1111");
        var asNote = await RecordPaymentRawAsync(client, customerId, "CashInStore", 100m, notes: "customer paid with 4111-1111-1111-1111");

        foreach (var response in new[] { asReference, asNote })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await BodyAsync(response);
            Assert.Contains(BusinessViolationCodes.ReferenceLooksLikeCardNumber, body);
            Assert.Contains("terminal slip number", body);
            Assert.DoesNotContain("4111", body);
        }

        Assert.DoesNotContain(logs, line => line.Contains("4111", StringComparison.Ordinal) && !line.Contains("INSERT INTO account.", StringComparison.Ordinal));
        Assert.Equal(0, await CountAsync<PaymentAggregate>(p => p.CustomerId == customerId));
        Assert.Equal(0, await CountAsync<AuditEntryAggregate>(a => (a.AfterValue != null && (a.AfterValue.Contains("4111 1111") || a.AfterValue.Contains("4111-1111") || a.AfterValue.Contains("4111111111"))) || (a.BeforeValue != null && a.BeforeValue.Contains("4111 1111"))));

        // The slip number that is NOT a card number is fine.
        await RecordPaymentAsync(context.Admin, customerId, "CardInStore", 100m, reference: "004512");
    }

    // ── I-D7, I-D8 ──────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-D7 -- a mistaken payment is reversed (with a reason) and re-entered, never edited: the invoice returns to part paid, the
    /// reason is audited, and the re-entry works (US-BIL-11 AC3).</summary>
    [Fact]
    public async Task I_D7_AReversal_ReleasesTheInvoice_IsAudited_AndTheReEntryWorks()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m);
        var (accountant, accountantId) = await StaffClientAsync(context, StaffRoles.Accountant);
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 400m, allocations: [(invoiceId, 400m)]);
        var wrong = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 600m, allocations: [(invoiceId, 600m)]);
        var wrongId = wrong.GetProperty("paymentId").GetGuid();
        Assert.Equal("Paid", (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("status").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await accountant.PostAsJsonAsync($"/api/staff/billing/payments/{wrongId}/reverse", new { reason = "" }, Json)).StatusCode);
        await ExpectAsync(HttpStatusCode.NoContent, await accountant.PostAsJsonAsync($"/api/staff/billing/payments/{wrongId}/reverse", new { reason = "Keyed against the wrong invoice" }, Json));

        var invoice = await GetInvoiceAsync(context.Admin, invoiceId);
        Assert.Equal("PartPaid", invoice.GetProperty("status").GetString());
        Assert.Equal(600m, AmountOf(invoice.GetProperty("outstanding")));
        Assert.Single(invoice.GetProperty("payments").EnumerateArray());

        var detail = await GetPaymentAsync(context.Admin, wrongId);
        Assert.Equal("Reversed", detail.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal("Keyed against the wrong invoice", detail.GetProperty("trail").GetProperty("reversalReason").GetString());
        Assert.Equal(accountantId, detail.GetProperty("trail").GetProperty("reversedBy").GetGuid());
        Assert.Equal(["receipt"], detail.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()!).ToArray());

        var audit = await WithDbAsync(db => db.Set<AuditEntryAggregate>().AsNoTracking()
            .Where(a => a.EntityId == wrongId && a.Category == AuditCategory.BusinessEvent && a.Action == "PaymentReversed").ToListAsync());
        var row = Assert.Single(audit);
        Assert.Equal("Keyed against the wrong invoice", row.AfterValue);
        Assert.Equal(accountantId, row.ActorUserId);

        // Re-entry against the right invoice works; the reversed payment cannot be reversed or allocated again.
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 600m, allocations: [(invoiceId, 600m)]);
        Assert.Equal("Paid", (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await accountant.PostAsJsonAsync($"/api/staff/billing/payments/{wrongId}/reverse", new { reason = "again" }, Json)).StatusCode);
    }

    [Fact]
    public async Task I_D8_ThereIsNoRouteThatEditsAPayment()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var payment = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 100m);
        var url = $"/api/staff/billing/payments/{payment.GetProperty("paymentId").GetGuid()}";

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await context.Admin.PutAsJsonAsync(url, new { amount = 1 }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await context.Admin.PatchAsJsonAsync(url, new { amount = 1 }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await context.Admin.DeleteAsync(url)).StatusCode);
    }

    /// <summary>The database backs the application (D-5): a payment's core columns cannot change and no payment row is deleted, even
    /// from a psql prompt.</summary>
    [Fact]
    public async Task TheDatabaseRefusesToEditOrDeleteAPayment_ButLetsItsStatusMove()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var payment = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 100m);
        var id = payment.GetProperty("paymentId").GetGuid();

        foreach (var sql in new[]
        {
            $"UPDATE billing.payment SET amount = 1 WHERE \"Id\" = '{id}'",
            $"UPDATE billing.payment SET method = 3 WHERE \"Id\" = '{id}'",
            $"UPDATE billing.payment SET external_reference = 'x' WHERE \"Id\" = '{id}'",
            $"UPDATE billing.payment SET received_on = received_on - 1 WHERE \"Id\" = '{id}'",
            $"UPDATE billing.payment SET recorded_by = gen_random_uuid() WHERE \"Id\" = '{id}'",
            $"UPDATE billing.payment SET receipt_number = 999999 WHERE \"Id\" = '{id}'",
            $"DELETE FROM billing.payment WHERE \"Id\" = '{id}'",
        })
        {
            await using var connection = OpenRaw();
            await using var command = new NpgsqlCommand(sql, connection);
            Assert.Equal("P0001", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }

        await using var ok = OpenRaw();
        await using var move = new NpgsqlCommand($"UPDATE billing.payment SET status = {PaymentStatus.Reversed.Value} WHERE \"Id\" = '{id}'", ok);
        Assert.Equal(1, await move.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task TheDatabaseKeepsAllocationEntriesAppendOnly()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 500m);
        var payment = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 500m, allocations: [(invoiceId, 500m)]);
        var id = payment.GetProperty("paymentId").GetGuid();

        foreach (var sql in new[]
        {
            $"UPDATE billing.payment_allocation_entry SET amount = 1 WHERE payment_id = '{id}'",
            $"DELETE FROM billing.payment_allocation_entry WHERE payment_id = '{id}'",
        })
        {
            await using var connection = OpenRaw();
            await using var command = new NpgsqlCommand(sql, connection);
            Assert.Equal("P0001", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
        }
    }

    // ── I-D9 ────────────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-D9 -- a deposit earmarked to a stay: the stay's invoice is issued, the deposit allocates to it; any other invoice
    /// refuses it (<c>EarmarkMismatch</c>) until the earmark is released.</summary>
    [Fact]
    public async Task I_D9_ADeposit_AllocatesOnlyToItsStaysInvoice_UntilTheEarmarkIsReleased()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var stay = Guid.NewGuid();

        var deposit = await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 1000m, earmark: new { kind = "BoardingStay", sourceId = stay, label = "Deposit — boarding 12–19 Oct" });
        var depositId = deposit.GetProperty("paymentId").GetGuid();
        Assert.Equal(1000m, AmountOf(deposit.GetProperty("unallocated")));

        var summary = await GetSummaryAsync(context.Admin, customerId);
        Assert.Equal(0m, AmountOf(summary.GetProperty("freeCredit")));
        var earmarked = Assert.Single(summary.GetProperty("earmarkedCredit").EnumerateArray());
        Assert.Equal("Deposit — boarding 12–19 Oct", earmarked.GetProperty("label").GetString());
        Assert.Equal(1000m, AmountOf(earmarked.GetProperty("amount")));

        var nightCharge = await RecordChargeAsync(context, customerId, 700m, itemKind: ItemKind.BoardingNight, sourceKind: ChargeSourceKind.BoardingStay, sourceId: stay,
            groupKind: BillingGroupKind.BoardingStay, groupId: stay, taxClassId: context.ZeroTaxClassId);
        var (stayInvoice, _) = await IssueFromChargesAsync(context.Admin, customerId, [nightCharge]);
        var (otherInvoice, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 400m);

        var mismatch = await context.Admin.PostAsJsonAsync($"/api/staff/billing/payments/{depositId}/allocations", new { allocations = new[] { new { invoiceId = otherInvoice, amount = 100 } } }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
        Assert.Contains(BusinessViolationCodes.EarmarkMismatch, await BodyAsync(mismatch));

        var allocated = await ReadAsync(await ExpectAsync(HttpStatusCode.OK,
            await context.Admin.PostAsJsonAsync($"/api/staff/billing/payments/{depositId}/allocations", new { allocations = new[] { new { invoiceId = stayInvoice, amount = 700 } } }, Json)));
        Assert.Equal(300m, AmountOf(allocated.GetProperty("unallocated")));
        Assert.Equal("Paid", (await GetInvoiceAsync(context.Admin, stayInvoice)).GetProperty("status").GetString());

        // A released earmark is free credit: it may now go anywhere.
        Assert.Equal(HttpStatusCode.BadRequest, (await context.Admin.PostAsJsonAsync($"/api/staff/billing/payments/{depositId}/release-earmark", new { reason = "" }, Json)).StatusCode);
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync($"/api/staff/billing/payments/{depositId}/release-earmark", new { reason = "Stay shortened" }, Json));

        var afterRelease = await GetSummaryAsync(context.Admin, customerId);
        Assert.Equal(300m, AmountOf(afterRelease.GetProperty("freeCredit")));
        Assert.Empty(afterRelease.GetProperty("earmarkedCredit").EnumerateArray());

        await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync($"/api/staff/billing/payments/{depositId}/allocations", new { allocations = new[] { new { invoiceId = otherInvoice, amount = 300 } } }, Json));
        Assert.Equal(100m, AmountOf((await GetInvoiceAsync(context.Admin, otherInvoice)).GetProperty("outstanding")));
    }

    [Fact]
    public async Task AnOrderEarmarkedPayment_FollowsTheSameRule_ForTheOrderSource()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var order = Guid.NewGuid();
        var deposit = await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 500m, reference: "TRX-ORD-1", earmark: new { kind = "Order", sourceId = order, label = "Order #1042" });
        var orderCharge = await RecordChargeAsync(context, customerId, 500m, sourceKind: ChargeSourceKind.Order, sourceId: order, taxClassId: context.ZeroTaxClassId);
        var (orderInvoice, _) = await IssueFromChargesAsync(context.Admin, customerId, [orderCharge]);

        var paid = await context.Admin.PostAsJsonAsync($"/api/staff/billing/payments/{deposit.GetProperty("paymentId").GetGuid()}/allocations", new { allocations = new[] { new { invoiceId = orderInvoice, amount = 500 } } }, Json);

        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal("Paid", (await GetInvoiceAsync(context.Admin, orderInvoice)).GetProperty("status").GetString());
    }

    // ── I-D11 ───────────────────────────────────────────────────────────────────────────────────────
    /// <summary>I-D11 -- every record, approve and reverse is audited with the actor, the IP and before/after values (BR-17, C-9).</summary>
    [Fact]
    public async Task I_D11_RecordApproveAndReverse_AreAuditedWithActorAndIp()
    {
        var factoryWithIp = Factory.WithFixedRemoteIpAddress("203.0.113.77");
        using var client = factoryWithIp.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var context = await CreateBillingContextAsync();
        var accountant = await SeedStaffAsync(StaffRoles.Accountant);
        await AssignAsync(context.Admin, accountant.StaffAccountId, context.BranchId);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = accountant.Mobile, password = accountant.Password }, Json);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        client.DefaultRequestHeaders.Add("X-Branch-Id", context.BranchId.ToString());
        var customerId = await SeedCustomerAsync(context);

        var payment = await RecordPaymentAsync(client, customerId, "CashInStore", 100m);
        var id = payment.GetProperty("paymentId").GetGuid();
        await ExpectAsync(HttpStatusCode.NoContent, await client.PostAsJsonAsync($"/api/staff/billing/payments/{id}/reverse", new { reason = "Counted twice" }, Json));

        var rows = await WithDbAsync(db => db.Set<AuditEntryAggregate>().AsNoTracking().Where(a => a.EntityId == id).OrderBy(a => a.OccurredOn).ToListAsync());

        // The fake clock does not tick, so the insert row and the update row have the same OccurredOn and the order of the query is not defined: the rows
        // are told apart by what they are (an insert has no before-snapshot, an update has one), not by position. (TEST gate: this test failed 2 runs of 4.)
        var insert = Assert.Single(rows, r => r.Category == AuditCategory.DataChange && r.BeforeValue is null);
        Assert.Equal(accountant.StaffAccountId, insert.ActorUserId);
        Assert.Equal("203.0.113.77", insert.IpAddress?.ToString());
        Assert.Contains("\"Amount\":100", insert.AfterValue!.Replace(" ", ""));
        Assert.Null(insert.BeforeValue);

        var update = Assert.Single(rows, r => r.Category == AuditCategory.DataChange && r.BeforeValue is not null);
        Assert.NotNull(update.BeforeValue);
        Assert.Contains("Reversed", update.Action + update.AfterValue);

        var decision = Assert.Single(rows, r => r.Category == AuditCategory.BusinessEvent);
        Assert.Equal("PaymentReversed", decision.Action);
        Assert.Equal("Counted twice", decision.AfterValue);
        Assert.Equal(accountant.StaffAccountId, decision.ActorUserId);
        Assert.Equal("203.0.113.77", decision.IpAddress?.ToString());
    }

    // ── Message ─────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task AnAcceptedPayment_PutsTheReceiptInTheCustomersInbox_AndAPendingOneDoesNot()
    {
        var context = await CreateBillingContextAsync();
        var (customerClient, customerId) = await RegisterAndSignInCustomerAsync();
        var (invoiceId, number, _) = await IssueSimpleInvoiceAsync(context, customerId, 90_000m);
        var (recorder, _) = await StaffClientAsync(context, StaffRoles.Accountant);
        var (approver, _) = await StaffClientAsync(context, StaffRoles.Accountant);

        var small = await RecordPaymentAsync(recorder, customerId, "CashInStore", 1500m, allocations: [(invoiceId, 1500m)]);
        var big = await RecordPaymentAsync(recorder, customerId, "BankTransfer", 60_000m, reference: "TRX-9", allocations: [(invoiceId, 60_000m)]);
        var smallReceipt = small.GetProperty("receiptNumber").GetString()!;
        await RunDeliveryPassesUntilAsync(async () => (await InboxBodiesAsync(customerClient)).Any(b => b.Contains(smallReceipt)));

        var inbox = await ReadAsync(await customerClient.GetAsync("/api/customers/me/inbox"));
        var bodies = inbox.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("body").GetString()!).ToList();
        Assert.Contains(bodies, b => b.Contains("BDT 1,500.00") && b.Contains(number) && b.Contains(small.GetProperty("receiptNumber").GetString()!));
        Assert.DoesNotContain(bodies, b => b.Contains("BDT 60,000.00")); // pending: staff-internal, nothing sent

        await ExpectAsync(HttpStatusCode.OK, await approver.PostAsync($"/api/staff/billing/payments/{big.GetProperty("paymentId").GetGuid()}/approve", null));
        await RunDeliveryPassesUntilAsync(async () => (await InboxBodiesAsync(customerClient)).Any(b => b.Contains("BDT 60,000.00")));

        var after = await ReadAsync(await customerClient.GetAsync("/api/customers/me/inbox"));
        Assert.Contains(after.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("body").GetString()!), b => b.Contains("BDT 60,000.00") && b.Contains(big.GetProperty("receiptNumber").GetString()!));
    }

    // ── Lists, detail, receipt, CSV, customer view ──────────────────────────────────────────────────
    [Fact]
    public async Task TheListsDetailReceiptAndCsv_ShowThePaymentWithItsFacts()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context, "Karim Hossain");
        var (invoiceId, number, _) = await IssueSimpleInvoiceAsync(context, customerId, 500m);
        var payment = await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 500m, reference: "=TRX-INJECT", allocations: [(invoiceId, 500m)]);
        var id = payment.GetProperty("paymentId").GetGuid();

        var list = await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/payments?customerId={customerId}&status=Accepted&method=BankTransfer"));
        Assert.Equal(id, Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("paymentId").GetGuid());

        var detail = await GetPaymentAsync(context.Admin, id);
        Assert.Equal("=TRX-INJECT", detail.GetProperty("summary").GetProperty("externalReference").GetString());
        var entry = Assert.Single(detail.GetProperty("entries").EnumerateArray());
        Assert.Equal(number, entry.GetProperty("invoiceNumber").GetString());
        Assert.Equal("Allocate", entry.GetProperty("kind").GetString());

        var receipt = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/payments/{id}/receipt")));
        Assert.Equal(payment.GetProperty("receiptNumber").GetString(), receipt.GetProperty("receiptNumber").GetString());
        Assert.Equal("Karim Hossain", receipt.GetProperty("customerName").GetString());
        Assert.Equal("BIN-" + context.BranchCode, receipt.GetProperty("seller").GetProperty("taxRegistrationNumber").GetString());
        Assert.Equal(number, Assert.Single(receipt.GetProperty("appliedTo").EnumerateArray()).GetString());

        var csv = await (await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/payments/export.csv?customerId={customerId}"))).Content.ReadAsStringAsync();
        Assert.Contains("\"'=TRX-INJECT\"", csv);
        Assert.DoesNotContain(",\"=TRX", csv);

        Assert.Equal(HttpStatusCode.NotFound, (await context.Admin.GetAsync($"/api/staff/billing/payments/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task AnotherBranchsPayment_IsNotFound_AndCustomersSeeOnlyTheirOwnAcceptedPayments()
    {
        var mine = await CreateBillingContextAsync();
        var other = await CreateBillingContextAsync();
        var (clientA, customerA) = await RegisterAndSignInCustomerAsync();
        var (clientB, _) = await RegisterAndSignInCustomerAsync();
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(mine, customerA, 90_000m);
        var (recorder, _) = await StaffClientAsync(mine, StaffRoles.Accountant);

        var accepted = await RecordPaymentAsync(recorder, customerA, "CashInStore", 700m, allocations: [(invoiceId, 700m)]);
        var pending = await RecordPaymentAsync(recorder, customerA, "CashInStore", 60_000m);
        var acceptedId = accepted.GetProperty("paymentId").GetGuid();
        var pendingId = pending.GetProperty("paymentId").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.GetAsync($"/api/staff/billing/payments/{acceptedId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.PostAsJsonAsync($"/api/staff/billing/payments/{acceptedId}/reverse", new { reason = "not mine" }, Json)).StatusCode);

        var list = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await clientA.GetAsync("/api/customers/me/payments")));
        Assert.Equal(acceptedId, Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("paymentId").GetGuid());

        var own = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await clientA.GetAsync($"/api/customers/me/payments/{acceptedId}")));
        Assert.Equal(accepted.GetProperty("receiptNumber").GetString(), own.GetProperty("receiptNumber").GetString());

        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/customers/me/payments/{pendingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientB.GetAsync($"/api/customers/me/payments/{acceptedId}")).StatusCode);
        Assert.Empty((await ReadAsync(await clientB.GetAsync("/api/customers/me/payments"))).GetProperty("items").EnumerateArray());

        var summary = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await clientA.GetAsync("/api/customers/me/billing/summary")));
        Assert.Equal(89_300m, AmountOf(summary.GetProperty("outstanding")));
    }
}
