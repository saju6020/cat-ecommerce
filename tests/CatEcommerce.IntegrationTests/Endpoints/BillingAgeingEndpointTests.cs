using System.Net;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 §16 I-D10 (US-BIL-03 AC1, AC2) -- what customers owe, and how late it is. This class moves the (fake) clock
/// forward by 100 days to age four invoices, so it has a factory of its own: no other class shares that clock.</summary>
public sealed class BillingAgeingEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private static JsonElement Bucket(JsonElement totals, string name) => totals.GetProperty(name);

    /// <summary>I-D10 -- invoices that fell due 10, 40, 75 and 100 days ago land in four different buckets; the summary shows the
    /// outstanding, the overdue part, the oldest due date and the free credit.</summary>
    [Fact]
    public async Task I_D10_TheAgeingBucketsAndTheSummary_FollowTheDaysOverdue()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context, "Late Payer");
        var start = Today;

        // Issued on day 0, 25, 60 and 90 with terms of 0 days (due on issue) -- so on day 100 they are 100, 75, 40 and 10 days overdue.
        await IssueSimpleInvoiceAsync(context, customerId, 100m); // D, due day 0
        var (invoiceC, _, _) = await IssueAtDayAsync(context, customerId, 25, 200m);
        await IssueAtDayAsync(context, customerId, 60, 300m);
        await IssueAtDayAsync(context, customerId, 90, 400m);
        Factory.Clock.Advance(TimeSpan.FromDays(10));
        await ReSignInAsync(context);
        Assert.Equal(start.AddDays(100), Today);

        var ageing = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/receivables/ageing?customerId={customerId}")));
        var totals = ageing.GetProperty("totals");
        Assert.Equal(0m, AmountOf(Bucket(totals, "current")));
        Assert.Equal(400m, AmountOf(Bucket(totals, "days1To30")));
        Assert.Equal(300m, AmountOf(Bucket(totals, "days31To60")));
        Assert.Equal(200m, AmountOf(Bucket(totals, "days61To90")));
        Assert.Equal(100m, AmountOf(Bucket(totals, "over90")));

        var row = Assert.Single(ageing.GetProperty("rows").GetProperty("items").EnumerateArray());
        Assert.Equal(customerId, row.GetProperty("customerId").GetGuid());
        Assert.Equal("Late Payer", row.GetProperty("customerName").GetString());
        Assert.Equal(1000m, AmountOf(row.GetProperty("total")));
        Assert.Equal(start.ToString("yyyy-MM-dd"), row.GetProperty("oldestDueOn").GetString());

        var summary = await GetSummaryAsync(context.Admin, customerId);
        Assert.Equal(1000m, AmountOf(summary.GetProperty("outstanding")));
        Assert.Equal(1000m, AmountOf(summary.GetProperty("overdue")));
        Assert.Equal(start.ToString("yyyy-MM-dd"), summary.GetProperty("oldestDueOn").GetString());

        // A part payment reduces its bucket; an overpayment elsewhere shows as free credit.
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 250m, allocations: [(invoiceC, 50m)]);
        var afterPayment = await GetSummaryAsync(context.Admin, customerId);
        Assert.Equal(950m, AmountOf(afterPayment.GetProperty("outstanding")));
        Assert.Equal(200m, AmountOf(afterPayment.GetProperty("freeCredit")));
        var rebucketed = (await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/receivables/ageing?customerId={customerId}"))).GetProperty("totals");
        Assert.Equal(150m, AmountOf(Bucket(rebucketed, "days61To90")));

        // A second customer's brand-new invoice is not yet overdue: it is "current".
        var other = await SeedCustomerAsync(context);
        await IssueSimpleInvoiceAsync(context, other, 150m);
        var all = (await ReadAsync(await context.Admin.GetAsync("/api/staff/billing/receivables/ageing"))).GetProperty("totals");
        Assert.True(AmountOf(Bucket(all, "current")) >= 150m);

        // asOf shifts the buckets without touching any record.
        var future = (await ReadAsync(await context.Admin.GetAsync($"/api/staff/billing/receivables/ageing?customerId={customerId}&asOf={Today.AddDays(30):yyyy-MM-dd}"))).GetProperty("totals");
        Assert.Equal(0m, AmountOf(Bucket(future, "days1To30")));
        Assert.Equal(400m, AmountOf(Bucket(future, "days31To60")));
    }

    [Fact]
    public async Task TheAgeingView_IsScopedToTheActiveBranch_AndToBillingStaff()
    {
        var mine = await CreateBillingContextAsync();
        var other = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(mine);
        await IssueSimpleInvoiceAsync(mine, customerId, 500m);
        var (vet, _) = await StaffClientAsync(mine, StaffRoles.Vet);

        var mineAgeing = (await ReadAsync(await mine.Admin.GetAsync($"/api/staff/billing/receivables/ageing?customerId={customerId}"))).GetProperty("totals");
        Assert.Equal(500m, AmountOf(Bucket(mineAgeing, "days1To30")) + AmountOf(Bucket(mineAgeing, "current")));

        var otherAgeing = (await ReadAsync(await other.Admin.GetAsync($"/api/staff/billing/receivables/ageing?customerId={customerId}"))).GetProperty("totals");
        Assert.Equal(0m, AmountOf(Bucket(otherAgeing, "current")) + AmountOf(Bucket(otherAgeing, "days1To30")));

        Assert.Equal(HttpStatusCode.Forbidden, (await vet.GetAsync("/api/staff/billing/receivables/ageing")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vet.GetAsync($"/api/staff/billing/customers/{customerId}/summary")).StatusCode);
    }

    private async Task<(Guid InvoiceId, string Number, Guid ChargeId)> IssueAtDayAsync(BillingContext context, Guid customerId, int daysAfterPrevious, decimal amount)
    {
        Factory.Clock.Advance(TimeSpan.FromDays(daysAfterPrevious == 25 ? 25 : daysAfterPrevious == 60 ? 35 : 30));
        await ReSignInAsync(context);
        return await IssueSimpleInvoiceAsync(context, customerId, amount);
    }
}
