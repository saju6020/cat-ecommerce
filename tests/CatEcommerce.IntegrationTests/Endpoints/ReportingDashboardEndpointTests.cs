using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-RPT-001 section 16 E -- I-E1 to I-E4 (US-RPT-05): the role dashboard, composed and gated on the server, live, for today at the active branch.</summary>
public sealed class ReportingDashboardEndpointTests(ReportingApiFactory factory) : ReportingIntegrationTestBase(factory)
{
    private static string[] TileKeys(JsonElement dashboard) => dashboard.GetProperty("tiles").EnumerateArray().Select(t => t.GetProperty("key").GetString()!).ToArray();

    private static JsonElement Tile(JsonElement dashboard, string key) => dashboard.GetProperty("tiles").EnumerateArray().Single(t => t.GetProperty("key").GetString() == key);

    private static JsonElement Figure(JsonElement tile, string key) => tile.GetProperty("figures").EnumerateArray().Single(f => f.GetProperty("key").GetString() == key).GetProperty("value");

    private static async Task<JsonElement> DashboardAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/staff/dashboard");
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    // ── I-E1: the figures equal the matching report's figures ───────────────────────────────────────────────
    [Fact]
    public async Task I_E1_TheTiles_EqualTheMatchingReportFigures_ForToday()
    {
        var context = await CreateBillingContextAsync();
        var clinic = await SeedClinicAsync(context);
        var customerId = await SeedCustomerAsync(context);

        // Two walk-ins (checked in, waiting) and cash and card takings.
        for (var i = 0; i < 2; i++) await RegisterWalkInAsync(context, clinic, customerId);
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 500m);
        await RecordPaymentAsync(context.Admin, customerId, "BankTransfer", 300m, reference: "TRX-" + NewCode());

        var admin = await DashboardAsync(context.Admin);

        Assert.Equal(context.BranchId, admin.GetProperty("branchId").GetGuid());
        Assert.Equal(Today.ToString("yyyy-MM-dd"), admin.GetProperty("localDate").GetString());
        Assert.Equal(60, admin.GetProperty("refreshSeconds").GetInt32());

        // clinic.appointments-today = R1's statuses; clinic.queue-now = the two waiting cats.
        var schedule = await RunReportAsync(context.Admin, ReportCatalogue.ClinicDailyScheduleKey, context, from: Today, to: Today);
        var rows = Rows(schedule);
        var appointments = Tile(admin, "clinic.appointments-today");
        Assert.Equal(rows.Count(r => r.GetProperty("status").GetString() == "CheckedIn"), Figure(appointments, "checkedIn").GetInt64());
        Assert.Equal(rows.Count(r => r.GetProperty("status").GetString() == "Booked"), Figure(appointments, "booked").GetInt64());
        var (reception, _) = await StaffClientAsync(context, StaffRoles.Reception);
        Assert.Equal(2, Figure(Tile(await DashboardAsync(reception), "clinic.queue-now"), "waiting").GetInt64());

        // payments.today = what R13 expects per method.
        var payments = Tile(admin, "payments.today");
        Assert.Equal("500.00", Figure(payments, "CashInStore").GetProperty("amount").GetString());
        Assert.Equal("300.00", Figure(payments, "BankTransfer").GetProperty("amount").GetString());
        Assert.Equal("0.00", Figure(payments, "CashOnDelivery").GetProperty("amount").GetString());

        var reconciliation = await RunReportAsync(context.Admin, ReportCatalogue.PaymentReconciliationKey, context, from: Today, to: Today);
        Assert.Equal("500.00", Rows(reconciliation).Single(r => r.GetProperty("method").GetString() == "CashInStore").GetProperty("expected").GetProperty("amount").GetString());

        // A tile opens the report it summarises, for a caller who may open it.
        Assert.Equal(ReportCatalogue.PaymentReconciliationKey, payments.GetProperty("drillDown").GetProperty("reportKey").GetString());
    }

    // ── I-E2 / U-E2: nothing the caller may not see is computed or sent ───────────────────────────────────────
    [Fact]
    public async Task I_E2_ANurseGetsClinicTilesOnly_AShopManagerHasNoPaymentTile_AndAGatedTileIsNeverComputed()
    {
        var (nurseContext, nurse) = await ContextWithRoleAsync(StaffRoles.Nurse);
        var nurseDashboard = await DashboardAsync(nurse);
        Assert.NotEmpty(TileKeys(nurseDashboard));
        Assert.All(TileKeys(nurseDashboard), k => Assert.True(k.StartsWith("clinic.", StringComparison.Ordinal) || k.StartsWith("surgery.", StringComparison.Ordinal), k));
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.GetAsync($"/api/staff/reports/{ReportCatalogue.ClinicDailyScheduleKey}" + Query(nurseContext, Today, Today))).StatusCode);

        var (shopContext, shop) = await ContextWithRoleAsync(StaffRoles.ShopManager);
        var shopDashboard = await DashboardAsync(shop);
        var shopKeys = TileKeys(shopDashboard);
        Assert.Contains("revenue.shop-today", shopKeys);
        Assert.DoesNotContain(shopKeys, k => k.StartsWith("payments.", StringComparison.Ordinal) || k.StartsWith("billing.", StringComparison.Ordinal) || k.StartsWith("stock.", StringComparison.Ordinal));

        // The shop revenue tile has no cost, and the drill-down it offers is a report the shop manager may open.
        var revenue = Tile(shopDashboard, "revenue.shop-today");
        Assert.DoesNotContain(revenue.GetProperty("figures").EnumerateArray(), f => f.GetProperty("key").GetString()!.Contains("cost", StringComparison.OrdinalIgnoreCase) || f.GetProperty("key").GetString()!.Contains("margin", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ReportCatalogue.RevenueShopByCategoryKey, revenue.GetProperty("drillDown").GetProperty("reportKey").GetString());
        Assert.NotEqual(Guid.Empty, shopContext.BranchId);

        // U-E2: the gated tiles' sources were never invoked for these callers.
        var nurseId = StaffIdOf(nurseContext, StaffRoles.Nurse);
        var shopId = StaffIdOf(shopContext, StaffRoles.ShopManager);
        Assert.DoesNotContain(ReportingFactory.ComputedTiles, c => c.CallerId == nurseId && !c.Key.StartsWith("clinic.", StringComparison.Ordinal) && !c.Key.StartsWith("surgery.", StringComparison.Ordinal));
        Assert.DoesNotContain(ReportingFactory.ComputedTiles, c => c.CallerId == shopId && (c.Key.StartsWith("payments.", StringComparison.Ordinal) || c.Key.StartsWith("billing.", StringComparison.Ordinal)));
        Assert.Contains(ReportingFactory.ComputedTiles, c => c.CallerId == shopId && c.Key == "revenue.shop-today");
    }

    [Fact]
    public async Task EveryRoleOpensItsDashboard_AndTheTilesOfAnAnonymousOrCustomerCallerAreRefused()
    {
        foreach (var role in StaffRoles.All)
        {
            var (_, client) = await ContextWithRoleAsync(role);
            var dashboard = await DashboardAsync(client);
            Assert.Equal(DashboardTiles.ForRole(role).Select(t => t.Key).Order().ToArray(), TileKeys(dashboard).Order().ToArray());
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient().GetAsync("/api/staff/dashboard")).StatusCode);
        var (customer, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/staff/dashboard")).StatusCode);
    }

    // ── I-E3: the active branch decides ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_E3_AnotherAssignedBranchInTheHeader_ShowsThatBranchesFigures_AndNotTheFirstBranchesData()
    {
        var first = await CreateBillingContextAsync();
        var second = await CreateBillingContextAsync();
        await AssignBothAsync(first, second);

        var customerId = await SeedCustomerAsync(first);
        await RecordPaymentAsync(first.Admin, customerId, "CashInStore", 700m);

        var ownBranch = await DashboardAsync(first.Admin);
        Assert.Equal("700.00", Figure(Tile(ownBranch, "payments.today"), "CashInStore").GetProperty("amount").GetString());

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/staff/dashboard");
        request.Headers.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, second.BranchId.ToString());
        var response = await first.Admin.SendAsync(request);
        await ExpectAsync(HttpStatusCode.OK, response);
        var other = await ReadAsync(response);

        Assert.Equal(second.BranchId, other.GetProperty("branchId").GetGuid());
        Assert.Equal("0.00", Figure(Tile(other, "payments.today"), "CashInStore").GetProperty("amount").GetString());

        // A branch the caller is not assigned to is refused.
        var third = await CreateBillingContextAsync();
        var refused = new HttpRequestMessage(HttpMethod.Get, "/api/staff/dashboard");
        refused.Headers.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, third.BranchId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await first.Admin.SendAsync(refused)).StatusCode);
    }

    private async Task AssignBothAsync(BillingContext first, BillingContext second) =>
        await ExpectAsync(HttpStatusCode.NoContent, await first.Admin.PutAsJsonAsync(
            $"/api/admin/staff/{first.AdminStaffId}/branches", new { branchIds = new[] { first.BranchId, second.BranchId }, primaryBranchId = first.BranchId }, Json));

    // ── I-E4: NFR-PRF-3 -- the whole payload inside 1.5 s at the 95th percentile ───────────────────────────────
    [Fact]
    public async Task I_E4_TheAdministratorsFullDashboard_AnswersWithinOneAndAHalfSeconds_AtTheNinetyFifthPercentile()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 100m);
        await DashboardAsync(context.Admin); // warm up the host

        var times = new List<double>();
        for (var i = 0; i < 50; i++)
        {
            var watch = Stopwatch.StartNew();
            await DashboardAsync(context.Admin);
            times.Add(watch.Elapsed.TotalMilliseconds);
        }

        times.Sort();
        var p95 = times[(int)Math.Ceiling(times.Count * 0.95) - 1];
        Assert.True(p95 <= 1500, $"p95 was {p95.ToString("0", CultureInfo.InvariantCulture)} ms");
    }
}
