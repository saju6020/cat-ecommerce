using CatEcommerce.Shared.Common.Constants;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-BIL-001 section 16 I-X1 (C-5, NFR-SEC-2) -- run a representative sweep of Billing flows that carry free text or money references through a host that
/// captures <b>every log line of every level</b>, and prove none of them leaves a trace: no reference that looks like a card number (nor the
/// digits of one that was refused), no statement description, no reason or note text, no customer name. Stricter than the handover asks
/// ("at Information and above"): a debug line may not carry them either.
/// </summary>
public sealed partial class BillingLogCaptureEndpointTests(BillingLoggedApiFactory factory) : BillingIntegrationTestBase(factory), IClassFixture<BillingLoggedApiFactory>
{
    [GeneratedRegex(@"(?:\d[ -]?){13,19}")]
    private static partial Regex DigitRun();

    [Fact]
    public async Task I_X1_NoCardNumber_NoStatementDescription_NoReasonOrNoteText_NoCustomerName_IsEverLogged()
    {
        var marker = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var customerName = $"Zephyrine{marker}";
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context, customerName);
        var (accountant, _) = await StaffClientAsync(context, StaffRoles.Accountant);
        var (otherAccountant, _) = await StaffClientAsync(context, StaffRoles.Accountant);
        var texts = new List<string>();
        string Text(string label) { var value = $"{label}-{marker}"; texts.Add(value); return value; }

        // Payments: a reference and notes, a refused card number as reference and as notes, a reject and a reversal with reasons.
        var (invoiceId, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 10_000m);
        var lineId = (await GetInvoiceAsync(context.Admin, invoiceId)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();
        var paid = await RecordPaymentAsync(context.Admin, customerId, "CardInStore", 400m, reference: Text("SLIP"), allocations: [(invoiceId, 400m)]);
        var withNotes = await context.Admin.PostAsJsonAsync("/api/staff/billing/payments", new
        { customerId, method = "CashInStore", amount = 50m, receivedOn = Today.ToString("yyyy-MM-dd"), notes = Text("NOTE"), allocations = Array.Empty<object>() }, Json);
        await ExpectAsync(HttpStatusCode.Created, withNotes);
        Assert.Equal(HttpStatusCode.BadRequest, (await RecordPaymentRawAsync(context.Admin, customerId, "CardInStore", 10m, reference: "4111 1111 1111 1111")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RecordPaymentRawAsync(context.Admin, customerId, "CashInStore", 10m, notes: "card 4111-1111-1111-1111")).StatusCode);

        var pending = await RecordPaymentAsync(accountant, customerId, "BankTransfer", 60_000m, reference: Text("TRX"));
        await ExpectAsync(HttpStatusCode.NoContent, await otherAccountant.PostAsJsonAsync(
            $"/api/staff/billing/payments/{pending.GetProperty("paymentId").GetGuid()}/reject", new { reason = Text("REJECT") }, Json));
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync(
            $"/api/staff/billing/payments/{paid.GetProperty("paymentId").GetGuid()}/reverse", new { reason = Text("REVERSAL") }, Json));

        // Credit note and refund reasons, a refund reference, a void reason.
        var cash = (await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 900m, allocations: [(invoiceId, 900m)])).GetProperty("paymentId").GetGuid();
        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync($"/api/staff/billing/invoices/{invoiceId}/credit-notes", new
        { kind = "Return", reason = Text("CREDIT"), lines = new[] { new { invoiceLineId = lineId, amount = 9500m } } }, Json));
        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/billing/refunds", new
        { paymentId = cash, method = "CashInStore", amount = 100m, reason = Text("REFUND") }, Json));
        var charge = await RecordChargeAsync(context, customerId, 20m, 1m, taxClassId: context.ZeroTaxClassId);
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync($"/api/staff/billing/charges/{charge}/void", new { reason = Text("VOID") }, Json));

        // Cash-up explanation, a COD remittance reference.
        var counted = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PutAsJsonAsync(
            $"/api/staff/billing/cash-ups/{Today:yyyy-MM-dd}/count", new { countedCash = 1m, explanation = Text("VARIANCE") }, Json)));
        Assert.True(counted.GetProperty("variance").ValueKind != System.Text.Json.JsonValueKind.Null);
        var cod = (await RecordPaymentAsync(context.Admin, customerId, "CashOnDelivery", 100m, collector: new { kind = "Courier", courierCode = "PATHAO", courierName = "Pathao" })).GetProperty("paymentId").GetGuid();
        await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/staff/billing/cod/remittances", new
        { courierCode = "PATHAO", receivedOn = Today.ToString("yyyy-MM-dd"), amount = 100m, method = "BankTransfer", reference = Text("REMIT"), lines = new[] { new { paymentId = cod, amountRemitted = 100m } } }, Json));

        // A bank statement whose description carries free text and a card number, then an unmatch with a reason.
        var transfer = (await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 77m, reference: $"TRXLOG{marker}")).GetProperty("paymentId").GetGuid();
        var description = Text("STMTDESC");
        var csv = $"date,amount,direction,reference,description,bankTransactionId\n{Today:yyyy-MM-dd},77.00,C,TRXLOG{marker},{description} 4111 1111 1111 1111 {customerName},\n";
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "log.csv" }, { new StringContent("Main " + marker), "bankAccountLabel" },
            { new StringContent($"{Today.AddDays(-1):yyyy-MM-dd}"), "periodFrom" }, { new StringContent($"{Today:yyyy-MM-dd}"), "periodTo" },
        };
        var statement = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsync("/api/staff/billing/bank-statements", form)));
        var statementId = statement.GetProperty("statementId").GetGuid();
        await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsync($"/api/staff/billing/bank-statements/{statementId}/auto-match", null));
        var view = await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/bank-statements/{statementId}/reconciliation"));
        var matchId = view.GetProperty("matches")[0].GetProperty("matchId").GetGuid();
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync($"/api/staff/billing/bank-matches/{matchId}/unmatch", new { reason = Text("UNMATCH") }, Json));
        Assert.NotEqual(Guid.Empty, transfer);

        // The documents that print the customer name are read too.
        await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/invoices/{invoiceId}.pdf"));

        var all = string.Join("\n", ((BillingLoggedApiFactory)Factory).Logs);
        Assert.True(((BillingLoggedApiFactory)Factory).Logs.Count > 50, "The capture saw almost nothing; the sweep proves nothing.");
        foreach (var text in texts) Assert.DoesNotContain(text, all, StringComparison.Ordinal);
        Assert.DoesNotContain(customerName, all, StringComparison.Ordinal);
        Assert.DoesNotContain("4111 1111", all, StringComparison.Ordinal);
        Assert.DoesNotContain("4111-1111", all, StringComparison.Ordinal);
        Assert.DoesNotContain("4111111111111111", all, StringComparison.Ordinal);
        Assert.DoesNotContain("1111 1111", all, StringComparison.Ordinal);

        // Nothing in any line looks like a card number: a 13-19 digit run (spaces or dashes allowed) that passes Luhn. (Ids and timestamps
        // are digit runs too, so the check is the same one the guard uses.)
        var offenders = DigitRun().Matches(all).Select(m => m.Value).Where(CatEcommerce.Shared.Common.Helpers.CardNumberGuard.LooksLikeCardNumber).Take(5).ToList();
        Assert.True(offenders.Count == 0, "A card-like number was logged: " + string.Join(" | ", offenders.Select(o => o[..Math.Min(4, o.Length)] + "...")));
    }
}
