using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-RPT-001 TEST gate (2026-10-13), T-I8 -- <c>report.rv_batch</c> joins a batch to its landed-cost line; a batch must never appear twice, however often its consignment's costs are re-allocated
/// (a duplicate would double every valuation that reads the view). Data is made through HO-IMP-001's own endpoints; the view is read straight from the database for the assertion only.
/// </summary>
public sealed class ReportingTestGateStockTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private const string Consignments = "/api/staff/procurement/consignments";

    private static async Task<long> CountAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task T_I8_RvBatch_HasExactlyOneRowPerBatch_AfterTheConsignmentIsReAllocated_AndNoBatchIsDuplicatedAnywhereInTheDatabase()
    {
        ReportingTestDatabase.Require();
        var branch = await SeedBranchAsync();
        var admin = branch.Admin.Client;
        var sku = await CreateSkuAsync(admin, food: true);
        var batchId = await ReportingLandedCostSeeder.SeedAsync(admin, sku.SkuId, branch.WarehouseId, 20m, 5m, 100m, UtcToday.AddDays(300));

        Assert.Equal(1, await CountAsync("select count(*) from report.rv_batch where batch_id = @b", ("b", batchId)));

        // Re-allocate the same consignment twice more: the allocation REPLACES its lines, so the view must still show one row.
        var consignmentId = (Guid)(await ScalarAsync("select consignment_id from inventory.batch where \"Id\" = @b", ("b", batchId)))!;
        for (var round = 1; round <= 2; round++)
        {
            var detail = await admin.GetFromJsonAsync<JsonElement>($"{Consignments}/{consignmentId}", Json);
            var response = await admin.PostAsJsonAsync($"{Consignments}/{consignmentId}/landed-cost", new { expectedVersion = detail.GetProperty("version").GetInt32() }, Json);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            Assert.Equal(1, await CountAsync("select count(*) from report.rv_batch where batch_id = @b", ("b", batchId)));
        }

        // The landed cost is still the allocated one (20 x 5 + 100) / 20 = 10, not a fallback and not doubled.
        Assert.Equal(10m, Convert.ToDecimal(await ScalarAsync("select landed_unit_cost from report.rv_batch where batch_id = @b", ("b", batchId))));

        // Across the whole shared database: no batch id appears twice, and the view has as many rows as there are batches.
        Assert.Equal(0, await CountAsync("select count(*) from (select batch_id from report.rv_batch group by batch_id having count(*) > 1) d"));
        Assert.Equal(await CountAsync("select count(*) from inventory.batch"), await CountAsync("select count(*) from report.rv_batch"));
    }

    private static async Task<object?> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        return await command.ExecuteScalarAsync();
    }
}

/// <summary>
/// HO-RPT-001 TEST gate (2026-10-13), T-I10 -- a report day is the BRANCH day (Asia/Dhaka, UTC+6), not the UTC day: an invoice issued at 23:59:30 Dhaka belongs to that Dhaka date, and one issued 60 seconds
/// later, at 00:00:30, to the next -- although in UTC both fall in the same 18:00-19:00 hour of one UTC date. Its own class (and so its own fake clock): it jumps the clock to the evening.
/// </summary>
public sealed class ReportingMidnightEndpointTests(ReportingApiFactory factory) : ReportingIntegrationTestBase(factory)
{
    [Fact]
    public async Task T_I10_AnInvoiceAt235930Dhaka_IsInThatDaysRevenue_AndOneAt000030_IsInTheNextDays()
    {
        var offset = TimeSpan.FromHours(6);
        var local = Now.ToOffset(offset);
        var target = new DateTimeOffset(local.Date.AddHours(23).AddMinutes(59).AddSeconds(30), offset);
        if (target <= Now) target = target.AddDays(1);
        Factory.Clock.SetUtcNow(target.ToUniversalTime());

        var context = await CreateBillingContextAsync();
        var firstCustomer = await SeedCustomerAsync(context);
        var secondCustomer = await SeedCustomerAsync(context);

        var dayOne = Today;
        Assert.Equal(new TimeSpan(23, 59, 30), Now.ToOffset(offset).TimeOfDay);
        await IssueSimpleInvoiceAsync(context, firstCustomer, 50m);

        Factory.Clock.Advance(TimeSpan.FromSeconds(60));
        var dayTwo = Today;
        Assert.Equal(dayOne.AddDays(1), dayTwo);
        Assert.Equal(new TimeSpan(0, 0, 30), Now.ToOffset(offset).TimeOfDay);
        await IssueSimpleInvoiceAsync(context, secondCustomer, 70m);

        var report = await RunReportAsync(context.Admin, ReportCatalogue.RevenueByBusinessLineKey, context, "groupBy=Day", dayOne, dayTwo);

        decimal NetOn(DateOnly day) => Rows(report).Where(r => r.GetProperty("bucket").GetString() == day.ToString("yyyy-MM-dd"))
            .Sum(r => decimal.Parse(r.GetProperty("netRevenue").GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(50m, NetOn(dayOne));      // 23:59:30 Dhaka is day one (17:59:30Z); a UTC-day report would put both invoices on one date
        Assert.Equal(70m, NetOn(dayTwo));      // 00:00:30 is day two

        // A report for day one alone does not see day two's invoice, and the reverse.
        var onlyOne = await RunReportAsync(context.Admin, ReportCatalogue.RevenueByBusinessLineKey, context, "groupBy=Day", dayOne, dayOne);
        Assert.DoesNotContain(Rows(onlyOne), r => r.GetProperty("bucket").GetString() == dayTwo.ToString("yyyy-MM-dd"));
        var onlyTwo = await RunReportAsync(context.Admin, ReportCatalogue.RevenueByBusinessLineKey, context, "groupBy=Day", dayTwo, dayTwo);
        Assert.DoesNotContain(Rows(onlyTwo), r => r.GetProperty("bucket").GetString() == dayOne.ToString("yyyy-MM-dd"));
        Assert.Equal(70m, Rows(onlyTwo).Sum(r => decimal.Parse(r.GetProperty("netRevenue").GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
