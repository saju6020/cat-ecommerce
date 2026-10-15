using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-RPT-001 section 16 D -- I-D1, I-D5 and I-D6 over HO-BIL-001's own endpoints and its charge port (the port every producer, and every Billing test, goes through): revenue by business line, the payment
/// reconciliation of the four launch methods, the cash-up, ageing, credit notes and tax. The shop margin cases (I-D2, I-D3, I-D4) are <see cref="ReportingRevenueEndpointTests"/>, which needs a real order
/// and the stock ledger. One fresh branch per test.
/// </summary>
public sealed class ReportingFinancialEndpointTests(ReportingApiFactory factory) : ReportingIntegrationTestBase(factory)
{
    private static decimal Money(JsonElement cell) => decimal.Parse(cell.GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);

    private static JsonElement Find(JsonElement report, Func<JsonElement, bool> predicate) => Rows(report).Single(predicate);

    private async Task<HttpResponseMessage> CreditAsync(HttpClient client, Guid invoiceId, object body) =>
        await client.SendAsync(WithKey(HttpMethod.Post, $"/api/staff/billing/invoices/{invoiceId}/credit-notes", body, null));

    // ── I-D1: AC-16 -- one invoice, three business lines, a draft, a credit note the next day ─────────────────
    [Fact]
    public async Task I_D1_RevenueIsSplitByBusinessLine_TaxIsExcluded_ADraftIsIgnored_AndACreditNoteCountsOnItsOwnDay()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var stay = Guid.NewGuid();

        var consult = await RecordChargeAsync(context, customerId, 800m, 1m, itemKind: ItemKind.Service, sourceKind: ChargeSourceKind.Appointment, description: "Consultation");
        var product = await RecordChargeAsync(context, customerId, 450m, 2m, itemKind: ItemKind.Product, sourceKind: ChargeSourceKind.Order, description: "Cat food 2kg");
        var nights = new List<Guid>();
        for (var night = 1; night <= 3; night++)
            nights.Add(await RecordChargeAsync(context, customerId, 1000m, 1m, itemKind: ItemKind.BoardingNight, sourceKind: ChargeSourceKind.BoardingStay, sourceId: stay,
                sourceLineRef: $"night-{night}", taxClassId: context.ZeroTaxClassId, groupKind: BillingGroupKind.BoardingStay, groupId: stay, description: $"Boarding night {night}"));

        var (invoiceId, _) = await IssueFromChargesAsync(context.Admin, customerId, new[] { consult, product }.Concat(nights));

        // A draft that must not count.
        var spare = await RecordChargeAsync(context, customerId, 999m, 1m, itemKind: ItemKind.Product, sourceKind: ChargeSourceKind.Order, description: "Never issued");
        await CreateDraftAsync(context.Admin, customerId, [spare]);

        var firstDay = Today;
        var invoice = await GetInvoiceAsync(context.Admin, invoiceId);
        var shopLine = invoice.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("description").GetString() == "Cat food 2kg");

        // The credit note comes the next day.
        Factory.Clock.Advance(TimeSpan.FromDays(1));
        await ReSignInAsync(context);
        await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, invoiceId, new
        { kind = "Return", reason = "Returned unopened", fullCredit = false, lines = new[] { new { invoiceLineId = shopLine.GetProperty("lineId").GetGuid(), amount = 450m } } }));
        var secondDay = Today;
        Assert.NotEqual(firstDay, secondDay);

        var report = await RunReportAsync(context.Admin, ReportCatalogue.RevenueByBusinessLineKey, context, "groupBy=Day", firstDay, secondDay);
        string Day(DateOnly d) => d.ToString("yyyy-MM-dd");

        var clinic = Find(report, r => r.GetProperty("bucket").GetString() == Day(firstDay) && r.GetProperty("businessLine").GetString() == "Clinic");
        var shop = Find(report, r => r.GetProperty("bucket").GetString() == Day(firstDay) && r.GetProperty("businessLine").GetString() == "Shop");
        var boarding = Find(report, r => r.GetProperty("bucket").GetString() == Day(firstDay) && r.GetProperty("businessLine").GetString() == "Boarding");
        var shopNextDay = Find(report, r => r.GetProperty("bucket").GetString() == Day(secondDay) && r.GetProperty("businessLine").GetString() == "Shop");

        Assert.Equal(800m, Money(clinic.GetProperty("netRevenue")));      // tax (120) is not revenue
        Assert.Equal(120m, Money(clinic.GetProperty("tax")));
        Assert.Equal(900m, Money(shop.GetProperty("netRevenue")));
        Assert.Equal(3000m, Money(boarding.GetProperty("netRevenue")));
        Assert.Equal(0m, Money(boarding.GetProperty("tax")));

        // The draft (999) is nowhere; the credit note reduces the day it was issued, and the first day never changes.
        Assert.DoesNotContain(Rows(report), r => Money(r.GetProperty("netRevenue")) == 999m);
        Assert.Equal(-391.30m, Money(shopNextDay.GetProperty("netRevenue")));   // a credit note's amount is gross: 450.00 at 15 % is 391.30 net and 58.70 tax
        Assert.Equal(391.30m, Money(shopNextDay.GetProperty("creditNotes")));
        Assert.Equal(-58.70m, Money(shopNextDay.GetProperty("tax")));

        // No landed cost exists for any of it: the margin is not available -- null, never zero -- and the note says so.
        Assert.Equal(JsonValueKind.Null, shop.GetProperty("margin").ValueKind);
        Assert.Equal("0.0", shop.GetProperty("coveragePercent").GetString());
        Assert.Contains(report.GetProperty("notes").EnumerateArray(), n => n.GetProperty("code").GetString() == "MarginCoverage");
        Assert.Contains(report.GetProperty("notes").EnumerateArray(), n => n.GetProperty("code").GetString() == "ClinicVisitChargesNotDelivered");

        // R12 splits the same revenue by group.
        var byService = await RunReportAsync(context.Admin, ReportCatalogue.RevenueByServiceKey, context, "groupBy=Month", firstDay, secondDay);
        Assert.Contains(Rows(byService), r => r.GetProperty("businessLine").GetString() == "Clinic" && Money(r.GetProperty("netRevenue")) == 800m);
        Assert.Contains(Rows(byService), r => r.GetProperty("businessLine").GetString() == "Boarding" && Money(r.GetProperty("netRevenue")) == 3000m);
    }

    // ── I-D5: AC-8 -- the payment reconciliation, the four launch methods only ────────────────────────────────
    private static MultipartFormDataContent Upload(string csv, string label, DateOnly from, DateOnly to)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "statement.csv");
        content.Add(new StringContent(label), "bankAccountLabel");
        content.Add(new StringContent(from.ToString("yyyy-MM-dd")), "periodFrom");
        content.Add(new StringContent(to.ToString("yyyy-MM-dd")), "periodTo");
        return content;
    }

    [Fact]
    public async Task I_D5_TheReconciliation_FlagsTheCodShortfall_MatchesATransfer_ShowsTheTillVariance_AndKnowsOnlyTheFourMethods()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var admin = context.Admin;
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        // Cash on delivery collected by a courier: 500, of which the courier remitted 400.
        var cod = (await RecordPaymentAsync(admin, customerId, "CashOnDelivery", 500m, collector: new { kind = "Courier", courierCode = "PATHAO", courierName = "Pathao Courier" })).GetProperty("paymentId").GetGuid();
        await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync("/api/staff/billing/cod/remittances", new
        {
            courierCode = "pathao", receivedOn = Today.ToString("yyyy-MM-dd"), amount = 400m, method = "BankTransfer", reference = "REM-" + tag, lines = new[] { new { paymentId = cod, amountRemitted = 400m } },
        }, Json));

        // Two bank transfers (100 and 200); the statement has a credit for the first only.
        await RecordPaymentAsync(admin, customerId, "BankTransfer", 100m, reference: $"TRX{tag}A1");
        await RecordPaymentAsync(admin, customerId, "BankTransfer", 200m, reference: $"TRX{tag}A2");
        var statement = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await admin.PostAsync("/api/staff/billing/bank-statements",
            Upload($"date,amount,direction,reference,description,bankTransactionId\n{Today:yyyy-MM-dd},100.00,C,NEFT TRX-{tag}-A1,Transfer in,\n", "Main " + tag, Today.AddDays(-5), Today))));
        await ExpectAsync(HttpStatusCode.OK, await admin.PostAsync($"/api/staff/billing/bank-statements/{statement.GetProperty("statementId").GetGuid()}/auto-match", null));

        // Cash in store 1000 counted as 990 (an explained variance), card in store 700.
        await RecordPaymentAsync(admin, customerId, "CashInStore", 1000m);
        await RecordPaymentAsync(admin, customerId, "CardInStore", 700m, reference: "004512");
        var url = $"/api/staff/billing/cash-ups/{Today:yyyy-MM-dd}";
        await ExpectAsync(HttpStatusCode.OK, await admin.PutAsJsonAsync(url + "/count", new { countedCash = 990m, explanation = "Ten given as change in error" }, Json));
        var counted = await ReadAsync(await admin.GetAsync(url));
        await ExpectAsync(HttpStatusCode.NoContent, await admin.PostAsJsonAsync(url + "/close", new { expectedVersion = counted.GetProperty("version").GetInt32() }, Json)); // closing freezes the day's expected figures

        var report = await RunReportAsync(admin, ReportCatalogue.PaymentReconciliationKey, context, from: Today, to: Today);
        var rows = Rows(report);

        Assert.Equal(["CashOnDelivery", "BankTransfer", "CashInStore", "CardInStore"], rows.Select(r => r.GetProperty("method").GetString()).ToArray()); // exactly the four; no gateway
        var codRow = rows[0];
        Assert.Equal(500m, Money(codRow.GetProperty("expected")));
        Assert.Equal(400m, Money(codRow.GetProperty("received")));
        Assert.Equal(100m, Money(codRow.GetProperty("variance")));
        Assert.Equal("Short", codRow.GetProperty("varianceDirection").GetString()); // RPT-001-FU1: 400 received against 500 expected, though this row's variance is positive
        Assert.Equal(1, codRow.GetProperty("mismatchesOpen").GetInt64());

        var transferRow = rows[1];
        Assert.Equal(300m, Money(transferRow.GetProperty("expected")));
        Assert.Equal(100m, Money(transferRow.GetProperty("received")));
        Assert.Equal(200m, Money(transferRow.GetProperty("variance")));
        Assert.Equal("Short", transferRow.GetProperty("varianceDirection").GetString());
        Assert.Equal(1, transferRow.GetProperty("matched").GetInt64());
        Assert.Equal(1, transferRow.GetProperty("mismatchesOpen").GetInt64());

        var cashRow = rows[2];
        Assert.Equal(1000m, Money(cashRow.GetProperty("expected")));
        Assert.Equal(990m, Money(cashRow.GetProperty("received")));
        Assert.Equal(-10m, Money(cashRow.GetProperty("variance")));
        Assert.Equal("Short", cashRow.GetProperty("varianceDirection").GetString()); // counted 990 against 1000: the cash row's variance is negative, the COD row's positive, both Short

        var cardRow = rows[3];
        Assert.Equal(700m, Money(cardRow.GetProperty("expected")));
        Assert.Equal(JsonValueKind.Null, cardRow.GetProperty("received").ValueKind);
        Assert.Equal(JsonValueKind.Null, cardRow.GetProperty("varianceDirection").ValueKind); // nothing to compare against: no direction, not Matched

        // The column is declared right after variance with its meaning, in the run result and in the catalogue.
        var columns = report.GetProperty("columns").EnumerateArray().ToList();
        var at = columns.FindIndex(c => c.GetProperty("key").GetString() == "variance");
        Assert.Equal("varianceDirection", columns[at + 1].GetProperty("key").GetString());
        Assert.Equal("Text", columns[at + 1].GetProperty("type").GetString());
        Assert.Contains("Over", columns[at + 1].GetProperty("description").GetString());
        var entry = (await ReadAsync(await admin.GetAsync("/api/staff/reports"))).EnumerateArray().Single(e => e.GetProperty("key").GetString() == ReportCatalogue.PaymentReconciliationKey);
        Assert.Contains(entry.GetProperty("columns").EnumerateArray(), c => c.GetProperty("key").GetString() == "varianceDirection" && c.GetProperty("description").GetString()!.Contains("Short"));
        Assert.Contains(report.GetProperty("notes").EnumerateArray(), n => n.GetProperty("text").GetString()!.Contains("SRS Q-3c"));
    }

    // ── I-D6: the cash-up, ageing, refunds and credit notes, and the tax summary ─────────────────────────────
    [Fact]
    public async Task I_D6_TheCashUp_IsAsBillingRecordedIt_AndADayNotClosedSaysSo()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 1000m);
        await RecordPaymentAsync(context.Admin, customerId, "CardInStore", 400m, reference: "88");
        var url = $"/api/staff/billing/cash-ups/{Today:yyyy-MM-dd}";
        await ExpectAsync(HttpStatusCode.OK, await context.Admin.PutAsJsonAsync(url + "/count", new { countedCash = 990m, explanation = "Short by ten" }, Json));
        var view = await ReadAsync(await context.Admin.GetAsync(url));
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsJsonAsync(url + "/close", new { expectedVersion = view.GetProperty("version").GetInt32() }, Json));

        var report = await RunReportAsync(context.Admin, ReportCatalogue.DailyCashUpKey, context, from: Today.AddDays(-1), to: Today);
        var rows = Rows(report);

        Assert.Equal(2, rows.Count);
        Assert.Equal("Not closed", rows[0].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("expected").ValueKind);

        Assert.Equal("Closed", rows[1].GetProperty("status").GetString());
        Assert.Equal(1000m, Money(rows[1].GetProperty("cash")));
        Assert.Equal(400m, Money(rows[1].GetProperty("cardInStore")));
        Assert.Equal(1000m, Money(rows[1].GetProperty("expected")));
        Assert.Equal(990m, Money(rows[1].GetProperty("counted")));
        Assert.Equal(-10m, Money(rows[1].GetProperty("variance")));
        Assert.Equal("Short", rows[1].GetProperty("varianceDirection").GetString()); // counted 990 against 1000 expected
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("varianceDirection").ValueKind); // the day not closed has no direction
        Assert.Equal("Short by ten", rows[1].GetProperty("explanation").GetString());
        Assert.False(string.IsNullOrWhiteSpace(rows[1].GetProperty("closedBy").GetString()));
    }

    [Fact]
    public async Task I_D6_TheAgeing_BucketsTheOutstandingBalance_ByDaysSinceIssue_AtTheBoundaries()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (small, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 100m);
        await IssueSimpleInvoiceAsync(context, customerId, 250m);
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 40m, allocations: [(small, 40m)]);

        decimal Bucket(JsonElement report, string key) => Money(Rows(report).Single().GetProperty(key));

        // All issued today: the age is the number of days from the issue date to the end of the range.
        var today = await RunReportAsync(context.Admin, ReportCatalogue.ReceivablesAgeingKey, context, from: Today, to: Today);
        Assert.Equal(310m, Bucket(today, "balance"));
        Assert.Equal(310m, Bucket(today, "age0To30"));
        Assert.Equal(2, Rows(today).Single().GetProperty("invoicesOutstanding").GetInt64());

        Assert.Equal(310m, Bucket(await RunReportAsync(context.Admin, ReportCatalogue.ReceivablesAgeingKey, context, from: Today, to: Today.AddDays(30)), "age0To30"));
        Assert.Equal(310m, Bucket(await RunReportAsync(context.Admin, ReportCatalogue.ReceivablesAgeingKey, context, from: Today, to: Today.AddDays(31)), "age31To60"));
        Assert.Equal(310m, Bucket(await RunReportAsync(context.Admin, ReportCatalogue.ReceivablesAgeingKey, context, from: Today, to: Today.AddDays(61)), "age61To90"));
        Assert.Equal(310m, Bucket(await RunReportAsync(context.Admin, ReportCatalogue.ReceivablesAgeingKey, context, from: Today, to: Today.AddDays(91)), "ageOver90"));
    }

    [Fact]
    public async Task I_D6_RefundsAndCreditNotes_AreGroupedByReasonAndByHowTheyWereSettled()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (paid, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 1000m);
        var paidLine = (await GetInvoiceAsync(context.Admin, paid)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 1000m, allocations: [(paid, 1000m)]);
        var (unpaid, _, _) = await IssueSimpleInvoiceAsync(context, customerId, 600m);
        var unpaidLine = (await GetInvoiceAsync(context.Admin, unpaid)).GetProperty("lines")[0].GetProperty("lineId").GetGuid();

        // A return on the paid invoice: the money is released to the customer's credit.
        await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, paid, new { kind = "Return", reason = "Returned unopened", fullCredit = false, lines = new[] { new { invoiceLineId = paidLine, amount = 300m } } }));
        // Goodwill on the unpaid invoice: it simply reduces what is owed.
        await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, unpaid, new { kind = "Goodwill", reason = "Late delivery", fullCredit = false, lines = new[] { new { invoiceLineId = unpaidLine, amount = 100m } } }));

        var report = await RunReportAsync(context.Admin, ReportCatalogue.RefundsCreditNotesKey, context, from: Today, to: Today);
        var rows = Rows(report);

        var returned = Find(report, r => r.GetProperty("reason").GetString() == "Return");
        Assert.Equal("Customer credit", returned.GetProperty("refundMethod").GetString());
        Assert.Equal(300m, Money(returned.GetProperty("amount")));

        var goodwill = Find(report, r => r.GetProperty("reason").GetString() == "Goodwill");
        Assert.Equal("Applied to the invoice balance", goodwill.GetProperty("refundMethod").GetString());
        Assert.Equal(100m, Money(goodwill.GetProperty("amount")));
        Assert.Equal(2, rows.Count);
        Assert.Equal(2, report.GetProperty("totals").GetProperty("count").GetInt64());
    }

    [Fact]
    public async Task I_D6_TheTaxSummary_SumsToTheInvoicesTax_WithCreditNotesAsNegativeLines()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var taxed = await RecordChargeAsync(context, customerId, 1000m, 1m, itemKind: ItemKind.Product, sourceKind: ChargeSourceKind.Order, description: "Taxed");
        var zero = await RecordChargeAsync(context, customerId, 400m, 1m, itemKind: ItemKind.Product, sourceKind: ChargeSourceKind.Order, taxClassId: context.ZeroTaxClassId, description: "Zero rated");
        var (invoiceId, _) = await IssueFromChargesAsync(context.Admin, customerId, [taxed, zero]);
        var invoice = await GetInvoiceAsync(context.Admin, invoiceId);
        var taxedLine = invoice.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("description").GetString() == "Taxed").GetProperty("lineId").GetGuid();
        await ExpectAsync(HttpStatusCode.Created, await CreditAsync(context.Admin, invoiceId, new { kind = "Correction", reason = "Wrong price", fullCredit = false, lines = new[] { new { invoiceLineId = taxedLine, amount = 200m } } }));

        var report = await RunReportAsync(context.Admin, ReportCatalogue.TaxSummaryKey, context, "groupBy=Month", Today, Today);
        var invoiceRows = Rows(report).Where(r => r.GetProperty("source").GetString() == "Invoices").ToList();
        var creditRows = Rows(report).Where(r => r.GetProperty("source").GetString() == "Credit notes").ToList();

        Assert.Equal(Money(invoice.GetProperty("totals").GetProperty("tax")), invoiceRows.Sum(r => Money(r.GetProperty("outputTax")))); // I-D6: equals the invoices' tax
        var standard = invoiceRows.Single(r => decimal.Parse(r.GetProperty("ratePercent").GetString()!, CultureInfo.InvariantCulture) == 15m);
        Assert.Equal(150m, Money(standard.GetProperty("outputTax")));
        var credit = Assert.Single(creditRows);
        Assert.Equal(-173.91m, Money(credit.GetProperty("taxableNet")));   // 200.00 gross at 15 %
        Assert.Equal(-26.09m, Money(credit.GetProperty("outputTax")));
    }

    // ── Access ──────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TheFinancialReports_AreForTheAccountantAndTheAdministrator_NotForReceptionOrTheShopManager()
    {
        foreach (var role in new[] { StaffRoles.Reception, StaffRoles.ShopManager, StaffRoles.Inventory, StaffRoles.Vet })
        {
            var (context, client) = await ContextWithRoleAsync(role);
            var response = await client.GetAsync($"/api/staff/reports/{ReportCatalogue.RevenueByBusinessLineKey}" + Query(context, Today, Today));
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        var (accountantContext, accountant) = await ContextWithRoleAsync(StaffRoles.Accountant);
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync($"/api/staff/reports/{ReportCatalogue.RevenueByBusinessLineKey}" + Query(accountantContext, Today, Today))).StatusCode);
    }
}
