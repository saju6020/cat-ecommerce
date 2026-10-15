using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 C-5 (security gate round 1) -- a card number written into <b>any</b> free-text field of a money record -- a payment reject, reversal
/// or earmark-release reason, a credit-note reason, a cash-up explanation, an unmatch reason, a void reason, an invoice note, an earmark label, a
/// manual charge description or a corrected-draft line description -- is refused with <c>ReferenceLooksLikeCardNumber</c>, however it is spaced,
/// and neither the table nor any audit snapshot ever holds it.</summary>
public sealed class BillingCardNumberEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    // Spaced three ways -- the plain form is the one every audit probe searches for.
    private const string Plain = "card 4111 1111 1111 1111 charged twice";
    private const string Odd = "card 4111 - 1111.1111/1111 charged twice";
    private const string Bengali = "৪১১১ ১১১১ ১১১১ ১১১১";

    private static async Task AssertRefusedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(BusinessViolationCodes.ReferenceLooksLikeCardNumber, await ErrorCodesAsync(response));
        Assert.DoesNotContain("4111", await BodyAsync(response));
    }

    [Fact]
    public async Task EveryFreeTextMoneyField_RefusesACardNumber_AndNothingReachesTheAuditTrail()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var admin = context.Admin;
        var (accountant, _) = await StaffClientAsync(context, StaffRoles.Accountant);
        var (otherAccountant, _) = await StaffClientAsync(context, StaffRoles.Accountant);

        // Payments: reject (pending), reverse, release an earmark, and an earmark label.
        var pending = await RecordPaymentAsync(accountant, customerId, "BankTransfer", 60_000m, reference: "TRX-CARD-1");
        await AssertRefusedAsync(await otherAccountant.PostAsJsonAsync($"/api/staff/billing/payments/{pending.GetProperty("paymentId").GetGuid()}/reject", new { reason = Plain }, Json));
        await AssertRefusedAsync(await otherAccountant.PostAsJsonAsync($"/api/staff/billing/payments/{pending.GetProperty("paymentId").GetGuid()}/reject", new { reason = Odd }, Json));
        await AssertRefusedAsync(await otherAccountant.PostAsJsonAsync($"/api/staff/billing/payments/{pending.GetProperty("paymentId").GetGuid()}/reject", new { reason = Bengali }, Json));

        var cash = await RecordPaymentAsync(admin, customerId, "CashInStore", 100m);
        await AssertRefusedAsync(await admin.PostAsJsonAsync($"/api/staff/billing/payments/{cash.GetProperty("paymentId").GetGuid()}/reverse", new { reason = Plain }, Json));

        var deposit = await RecordPaymentAsync(admin, customerId, "CashInStore", 500m, earmark: new { kind = "BoardingStay", sourceId = Guid.NewGuid(), label = "Stay deposit" });
        await AssertRefusedAsync(await admin.PostAsJsonAsync($"/api/staff/billing/payments/{deposit.GetProperty("paymentId").GetGuid()}/release-earmark", new { reason = Plain }, Json));
        await AssertRefusedAsync(await RecordPaymentRawAsync(admin, customerId, "CashInStore", 10m, earmark: new { kind = "BoardingStay", sourceId = Guid.NewGuid(), label = Plain }));

        // Credit note reason; invoice note; corrected-draft line description.
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 400m);
        await AssertRefusedAsync(await admin.PostAsJsonAsync($"/api/staff/billing/invoices/{invoiceId}/credit-notes", new { kind = "Return", reason = Plain, fullCredit = true }, Json));
        var chargeId = await RecordChargeAsync(context, customerId, 50m, 1m, taxClassId: context.ZeroTaxClassId);
        await AssertRefusedAsync(await admin.PostAsJsonAsync("/api/staff/billing/invoices", new { customerId, chargeIds = new[] { chargeId }, notes = Plain }, Json));

        var credit = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync(
            $"/api/staff/billing/invoices/{invoiceId}/credit-notes", new { kind = "Correction", reason = "Wrong price", fullCredit = true }, Json)));
        var corrected = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync(
            $"/api/staff/billing/invoices/{invoiceId}/corrected-draft", new { creditNoteId = credit.GetProperty("creditNoteId").GetGuid() }, Json)));
        var draftId = corrected.GetProperty("invoiceId").GetGuid();
        var draft = await GetInvoiceAsync(admin, draftId);
        await AssertRefusedAsync(await admin.PutAsJsonAsync($"/api/staff/billing/invoices/{draftId}/lines", new
        {
            expectedVersion = draft.GetProperty("version").GetInt32(),
            lines = new[] { new { description = Plain, quantity = 1m, unitPrice = 400m, lineDiscount = 0m, taxClassId = context.ZeroTaxClassId } },
        }, Json));

        // Charges: a void reason and a manual charge description.
        await AssertRefusedAsync(await admin.PostAsJsonAsync($"/api/staff/billing/charges/{chargeId}/void", new { reason = Plain }, Json));
        await AssertRefusedAsync(await admin.PostAsJsonAsync("/api/staff/billing/charges", new
        { customerId, description = Plain, unitPrice = 10m, taxClassId = context.ZeroTaxClassId, quantity = 1m, serviceDate = Today.ToString("yyyy-MM-dd") }, Json));

        // Cash-up explanation and a bank unmatch reason.
        await AssertRefusedAsync(await admin.PutAsJsonAsync($"/api/staff/billing/cash-ups/{Today:yyyy-MM-dd}/count", new { countedCash = 1m, explanation = Plain }, Json));

        var transfer = await RecordPaymentAsync(admin, customerId, "BankTransfer", 77m, reference: "TRXCARDNUM77");
        var csv = $"date,amount,direction,reference,description,bankTransactionId\n{Today:yyyy-MM-dd},77.00,C,TRXCARDNUM77,transfer,\n";
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "file", "s.csv" }, { new StringContent("Main " + NewCode()), "bankAccountLabel" },
            { new StringContent($"{Today.AddDays(-1):yyyy-MM-dd}"), "periodFrom" }, { new StringContent($"{Today:yyyy-MM-dd}"), "periodTo" },
        };
        var statementId = (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await admin.PostAsync("/api/staff/billing/bank-statements", form)))).GetProperty("statementId").GetGuid();
        await ExpectAsync(HttpStatusCode.OK, await admin.PostAsync($"/api/staff/billing/bank-statements/{statementId}/auto-match", null));
        var matchId = (await ReadAsync(await admin.GetAsync($"/api/staff/billing/bank-statements/{statementId}/reconciliation"))).GetProperty("matches")[0].GetProperty("matchId").GetGuid();
        await AssertRefusedAsync(await admin.PostAsJsonAsync($"/api/staff/billing/bank-matches/{matchId}/unmatch", new { reason = Plain }, Json));
        Assert.NotEqual(Guid.Empty, transfer.GetProperty("paymentId").GetGuid());

        // Nothing was stored and no audit row ever held a card number.
        var rows = await WithDbAsync(db => db.Set<AuditEntryAggregate>().AsNoTracking()
            .Where(a => a.AfterValue != null && (a.AfterValue.Contains("4111 1111") || a.AfterValue.Contains("4111 - 1111") || a.AfterValue.Contains("৪১১১")))
            .CountAsync());
        Assert.Equal(0, rows);
    }
}
