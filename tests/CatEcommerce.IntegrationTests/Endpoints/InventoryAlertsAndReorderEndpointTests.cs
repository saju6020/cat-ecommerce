using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Background;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Read.Queries.Inventory;
using CatEcommerce.Read.ViewModels;
using Microsoft.AspNetCore.TestHost;
using CatEcommerce.Application.Commands.Inventory;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-IMP-001 section 16 phase H -- I-H1 ... I-H6: the expiry thresholds (US-IMP-10), low stock and reorder suggestions (US-IMP-12) and the shortfall contract.
/// The worker is off in tests; every test calls <see cref="InventoryWorkCycle"/> itself. Expiry offsets are chosen well inside each band (100, 75, 45, 15 days)
/// rather than on the 91/89/59/29 boundaries, because the branch's local date can differ from UTC's by a day.
/// </summary>
public sealed class InventoryAlertsAndReorderEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private const string Alerts = "/api/staff/inventory/alerts";
    private const string Policies = "/api/staff/inventory/reorder-policies";

    private static DateOnly InDays(int days) => UtcToday.AddDays(days);

    /// <summary>One worker pass, limited to the test's own branch -- the database is shared and grows with every test, so a full scan would measure the suite, not the rule.</summary>
    private async Task RunCycleAsync(BranchContext branch)
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<InventoryWorkCycle>().RunAsync(default, branch.BranchId);
    }

    private static async Task<List<JsonElement>> AlertsAsync(HttpClient client, string query = "")
    {
        var body = await JsonOkAsync(await client.GetAsync($"{Alerts}?pageSize=100{query}"));
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    private static async Task PutPolicyAsync(HttpClient client, Guid skuId, Guid locationId, decimal point, decimal? quantity = null, Guid? supplierId = null)
    {
        var response = await client.PutAsJsonAsync($"{Policies}/{skuId}/{locationId}", new { reorderPoint = point, reorderQuantity = quantity, preferredSupplierId = supplierId }, Json);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private async Task DeductAsync(BranchContext branch, SeededSku sku, Guid locationId, decimal quantity)
    {
        await using var port = OpenPort();
        var result = await port.Ledger.DeductAsync(
            Demand(branch.BranchId, sku.SkuId, quantity, MovementKind.Sale, locations: [locationId]),
            new StockReference(StockReferenceKind.PosSale, Guid.NewGuid(), null, "POS-" + Random.Shared.Next(1000, 9999)), new StockParty(null, null), default);
        Assert.True(result.IsSuccess, result.Message);
        await port.CommitAsync();
    }

    // ── I-H1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_H1_EachBatchRaisesOnlyItsTightestThreshold_AndASecondRunAddsNothing()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client, food: true);
        var far = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 5m, InDays(100));
        var ninety = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 5m, InDays(75));
        var sixty = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 5m, InDays(45));
        var thirty = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 5m, InDays(15));

        await RunCycleAsync(branch);
        var first = await AlertsAsync(branch.Admin.Client, "&kind=ExpiryThreshold");
        await RunCycleAsync(branch);
        var second = await AlertsAsync(branch.Admin.Client, "&kind=ExpiryThreshold");

        var byBatch = first.ToDictionary(a => a.GetProperty("batchId").GetGuid(), a => a.GetProperty("thresholdDays").GetInt32());
        Assert.False(byBatch.ContainsKey(far));
        Assert.Equal(90, byBatch[ninety]);
        Assert.Equal(60, byBatch[sixty]);
        Assert.Equal(30, byBatch[thirty]);
        Assert.Equal(3, first.Count);
        Assert.Equal(3, second.Count); // US-IMP-10 AC1: never raised twice
    }

    [Fact]
    public async Task I_H1_ABatchThatHasExpiredWithStock_RaisesOneExpiredAlert_AndNoThresholdOne()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client, food: true);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 5m, InDays(40));
        await SetBatchExpiryDirectlyAsync(batchId, InDays(-3));

        await RunCycleAsync(branch);
        await RunCycleAsync(branch);

        var expired = await AlertsAsync(branch.Admin.Client, "&kind=Expired");
        Assert.Equal(batchId, Assert.Single(expired).GetProperty("batchId").GetGuid());
        Assert.Empty(await AlertsAsync(branch.Admin.Client, "&kind=ExpiryThreshold"));
    }

    // ── I-H2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_H2_TheNearExpiryReportListsDaysRemainingAndValueAtRisk_AndDownloadsAsCsv()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client, food: true);
        var near = Assert.Single((await PostReceiptAsync(branch.Admin.Client, [ReceiptLine(sku.SkuId, branch.WarehouseId, 10m, expires: InDays(20), cost: 5m)])).BatchIds);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 10m, InDays(300));
        var expired = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 4m, InDays(40));
        await SetBatchExpiryDirectlyAsync(expired, InDays(-2));

        var report = await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/reports/near-expiry?withinDays=90"));
        var row = Assert.Single(report.GetProperty("rows").EnumerateArray());
        Assert.Equal(near, row.GetProperty("batchId").GetGuid());
        Assert.InRange(row.GetProperty("daysRemaining").GetInt32(), 19, 20);
        Assert.Equal("10", row.GetProperty("quantity").GetString());
        Assert.Equal("50.00", row.GetProperty("valueAtRisk").GetProperty("amount").GetString()); // 10 x 5.00
        Assert.Equal("Purchase", row.GetProperty("costBasis").GetString());

        var withExpired = await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/reports/near-expiry?withinDays=90&includeExpired=true"));
        var rows = withExpired.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.True(rows[0].GetProperty("isExpired").GetBoolean()); // sorted by expiry: the expired one first
        Assert.True(rows[0].GetProperty("daysRemaining").GetInt32() < 0);

        var csv = await branch.Admin.Client.GetAsync("/api/staff/inventory/reports/near-expiry.csv?withinDays=90");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.StartsWith("\"SkuCode\"", text);
        Assert.Contains("\"50.00\"", text);
    }

    [Fact]
    public async Task I_H2_TheReportsNeedTheReportsAction_ReceptionIsRefused()
    {
        var branch = await SeedBranchAsync();
        var reception = await StaffAsync(branch, StaffRoles.Reception);

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.Client.GetAsync("/api/staff/inventory/reports/near-expiry")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.Client.GetAsync("/api/staff/inventory/reports/stock-on-hand")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.Client.GetAsync("/api/staff/inventory/reports/near-expiry.csv")).StatusCode);
    }

    // ── I-H3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_H3_LowStockIsRaisedWhenUsableFallsBelowThePoint_AndResolvedWhenStockIsReceived()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await PutPolicyAsync(keeper.Client, sku.SkuId, branch.ShopId, 10m);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 12m);

        await RunCycleAsync(branch);
        Assert.Empty(await AlertsAsync(branch.Admin.Client, "&kind=LowStock")); // 12 >= 10

        await DeductAsync(branch, sku, branch.ShopId, 7m); // usable 5 < 10
        await RunCycleAsync(branch);
        await RunCycleAsync(branch);

        var low = Assert.Single(await AlertsAsync(branch.Admin.Client, "&kind=LowStock"));
        Assert.Equal("Open", low.GetProperty("status").GetString());
        Assert.Equal(sku.SkuId, low.GetProperty("skuId").GetGuid());
        Assert.Equal("5", low.GetProperty("quantityAtRaise").GetString());

        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 20m);
        await RunCycleAsync(branch);

        var resolved = Assert.Single(await AlertsAsync(branch.Admin.Client, "&kind=LowStock"));
        Assert.Equal("Resolved", resolved.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, resolved.GetProperty("resolvedOn").ValueKind);

        // the pair falls again: a NEW alert, because the old one is closed
        await DeductAsync(branch, sku, branch.ShopId, 22m);
        await RunCycleAsync(branch);
        Assert.Equal(2, (await AlertsAsync(branch.Admin.Client, "&kind=LowStock")).Count);
    }

    [Fact]
    public async Task I_H3_ZeroSilencesAPair_AndTheBelowReorderPointFilterFindsTheLowOnes()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var low = await CreateSkuAsync(branch.Admin.Client);
        var silenced = await CreateSkuAsync(branch.Admin.Client);
        var fine = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, low.SkuId, branch.ShopId, 3m);
        await ReceiveAsync(branch.Admin.Client, silenced.SkuId, branch.ShopId, 3m);
        await ReceiveAsync(branch.Admin.Client, fine.SkuId, branch.ShopId, 50m);
        await PutPolicyAsync(keeper.Client, silenced.SkuId, branch.ShopId, 0m);

        await RunCycleAsync(branch);

        var alerts = await AlertsAsync(branch.Admin.Client, "&kind=LowStock");
        Assert.Equal(low.SkuId, Assert.Single(alerts).GetProperty("skuId").GetGuid());

        var filtered = await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/stock?belowReorderPoint=true"));
        Assert.Equal(low.SkuId, Assert.Single(filtered.GetProperty("items").EnumerateArray()).GetProperty("skuId").GetGuid());

        var summary = await JsonOkAsync(await branch.Admin.Client.GetAsync($"{Alerts}/summary"));
        Assert.Equal(1, summary.GetProperty("open").GetProperty("byKind").GetProperty("LowStock").GetInt32());
    }

    [Fact]
    public async Task I_H3_TheReorderPolicyIsAnUpsert_WithTheEffectivePointAndWhetherItIsTheDefault()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var reception = await StaffAsync(branch, StaffRoles.Reception);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 5m);

        async Task<JsonElement> RowAsync() => Assert.Single((await JsonOkAsync(await keeper.Client.GetAsync($"{Policies}?locationId={branch.ShopId}"))).GetProperty("items").EnumerateArray());

        var defaulted = await RowAsync();
        Assert.True(defaulted.GetProperty("isDefaulted").GetBoolean());
        Assert.Equal("10", defaulted.GetProperty("reorderPoint").GetString()); // inventory.defaultReorderPoint

        await PutPolicyAsync(keeper.Client, sku.SkuId, branch.ShopId, 25m, quantity: 40m);
        await PutPolicyAsync(keeper.Client, sku.SkuId, branch.ShopId, 30m, quantity: 40m);

        var explicitRow = await RowAsync();
        Assert.False(explicitRow.GetProperty("isDefaulted").GetBoolean());
        Assert.Equal("30", explicitRow.GetProperty("reorderPoint").GetString());
        Assert.Equal("40", explicitRow.GetProperty("reorderQuantity").GetString());

        await AssertErrorAsync(
            await keeper.Client.PutAsJsonAsync($"{Policies}/{sku.SkuId}/{branch.ShopId}", new { reorderPoint = -1m }, Json), BusinessViolationCodes.InvalidReorderPolicy);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reception.Client.PutAsJsonAsync($"{Policies}/{sku.SkuId}/{branch.ShopId}", new { reorderPoint = 1m }, Json)).StatusCode);

        var other = await SeedBranchAsync();
        Assert.Equal(HttpStatusCode.NotFound,
            (await keeper.Client.PutAsJsonAsync($"{Policies}/{sku.SkuId}/{other.ShopId}", new { reorderPoint = 1m }, Json)).StatusCode);
    }

    // ── I-H4 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_H4_TheSuggestedQuantityEqualsTheFormula_OverConsumptionOnOrderAndTheSuppliersLeadTime()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var supplierId = await SeedSupplierAsync(branch.Admin.Client, currency: "BDT"); // lead time 21 days
        await PutPolicyAsync(keeper.Client, sku.SkuId, branch.ShopId, 50m, supplierId: supplierId);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 60m);
        await DeductAsync(branch, sku, branch.ShopId, 55m); // consumption 55, usable 5
        await SeedIssuedOrderAsync(branch.Admin.Client, supplierId, branch.ShopId, "BDT", (sku.SkuId, 20m, 5m)); // 20 on order

        var body = await JsonOkAsync(await keeper.Client.GetAsync($"/api/staff/procurement/reorder-suggestions?locationId={branch.ShopId}"));

        var row = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(sku.SkuId, row.GetProperty("skuId").GetGuid());
        Assert.Equal("5", row.GetProperty("usable").GetString());
        Assert.Equal("20", row.GetProperty("onOrder").GetString());
        Assert.Equal(21, row.GetProperty("leadTimeDays").GetInt32());
        var cover = row.GetProperty("leadTimeDays").GetInt32() + row.GetProperty("coverDays").GetInt32();
        var expected = Math.Max(0m, Math.Ceiling(55m / 90m * cover) + 50m - (5m + 20m));
        Assert.Equal(expected, decimal.Parse(row.GetProperty("suggestedQuantity").GetString()!, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(supplierId, row.GetProperty("preferredSupplierId").GetGuid());

        var otherSupplier = await SeedSupplierAsync(branch.Admin.Client, currency: "BDT");
        var filtered = await JsonOkAsync(await keeper.Client.GetAsync($"/api/staff/procurement/reorder-suggestions?locationId={branch.ShopId}&supplierId={otherSupplier}"));
        Assert.Equal(0, filtered.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task I_H4_APolicyQuantityOverridesTheFormula()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await PutPolicyAsync(keeper.Client, sku.SkuId, branch.ShopId, 50m, quantity: 120m);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 4m);

        var body = await JsonOkAsync(await keeper.Client.GetAsync($"/api/staff/procurement/reorder-suggestions?locationId={branch.ShopId}"));

        Assert.Equal("120", Assert.Single(body.GetProperty("items").EnumerateArray()).GetProperty("suggestedQuantity").GetString());
    }

    // ── I-H5 / I-H6 ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_H5_AShortfallRecordedThroughThePort_IsCommittedWithTheConsumersOwnSave_AsAnOpenAlert()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);

        await using (var port = OpenPort())
        {
            port.Ledger.RecordShortfall(new StockShortfall(sku.SkuId, branch.DispensaryId, 3m, Guid.NewGuid(), null));
            Assert.Equal(1, port.Enlisted); // enlisted, not saved: the consumer's commit carries it
            await port.CommitAsync();
        }

        var alert = Assert.Single(await AlertsAsync(branch.Admin.Client, "&kind=Shortfall"));
        Assert.Equal("Open", alert.GetProperty("status").GetString());
        Assert.Equal(sku.SkuId, alert.GetProperty("skuId").GetGuid());
        Assert.Equal(branch.DispensaryId, alert.GetProperty("locationId").GetGuid());
        Assert.Equal("3", alert.GetProperty("quantityAtRaise").GetString());
    }

    [Fact]
    public async Task I_H5_AShortfallThatIsNeverCommittedLeavesNoAlert()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);

        await using (var port = OpenPort())
        {
            port.Ledger.RecordShortfall(new StockShortfall(sku.SkuId, branch.DispensaryId, 3m, Guid.NewGuid(), null));
        }

        Assert.Empty(await AlertsAsync(branch.Admin.Client, "&kind=Shortfall"));
    }

    [Fact]
    public async Task I_H6_AcknowledgingAnAlertRecordsWhoSawIt_OnlyOnce_AndNotFromAnotherBranch()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var reception = await StaffAsync(branch, StaffRoles.Reception);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await using (var port = OpenPort())
        {
            port.Ledger.RecordShortfall(new StockShortfall(sku.SkuId, branch.DispensaryId, 1m, Guid.NewGuid(), null));
            await port.CommitAsync();
        }

        var id = Assert.Single(await AlertsAsync(keeper.Client, "&kind=Shortfall")).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.Client.PostAsJsonAsync($"{Alerts}/{id}/acknowledge", new { note = "seen" }, Json)).StatusCode);

        var other = await SeedBranchAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.Admin.Client.PostAsJsonAsync($"{Alerts}/{id}/acknowledge", new { note = "not mine" }, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await keeper.Client.PostAsJsonAsync($"{Alerts}/{id}/acknowledge", new { note = "Ordering more." }, Json)).StatusCode);

        var alert = Assert.Single(await AlertsAsync(keeper.Client, "&kind=Shortfall"));
        Assert.Equal("Acknowledged", alert.GetProperty("status").GetString());
        Assert.Equal(keeper.StaffAccountId, alert.GetProperty("acknowledgedBy").GetGuid());

        await AssertErrorAsync(await keeper.Client.PostAsJsonAsync($"{Alerts}/{id}/acknowledge", new { note = "again" }, Json), BusinessViolationCodes.StockAlertNotOpen);

        var summary = await JsonOkAsync(await keeper.Client.GetAsync($"{Alerts}/summary"));
        Assert.Equal(0, summary.GetProperty("open").GetProperty("total").GetInt32()); // an acknowledged alert no longer needs a person
    }

    // ── reports ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheStockOnHandReport_ValuesEveryPositionAtItsCostBasis_AndDownloadsAsCsv()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 10m); // 5.00 each

        var report = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/reports/stock-on-hand?locationId={branch.ShopId}"));

        var row = Assert.Single(report.GetProperty("rows").EnumerateArray());
        Assert.Equal(batchId, row.GetProperty("batchId").GetGuid());
        Assert.Equal("10", row.GetProperty("onHand").GetString());
        Assert.Equal("50.00", row.GetProperty("value").GetProperty("amount").GetString());
        Assert.Equal("Purchase", row.GetProperty("costBasis").GetString());
        Assert.Equal("50.00", report.GetProperty("totalValue").GetProperty("amount").GetString());

        var csv = await branch.Admin.Client.GetAsync($"/api/staff/inventory/reports/stock-on-hand.csv?locationId={branch.ShopId}");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        Assert.Contains("\"50.00\"", await csv.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheReconciliationReport_IsEmptyForAnHonestLedger()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 10m);
        await DeductAsync(branch, sku, branch.ShopId, 4m);

        var body = await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/reports/reconciliation"));

        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    // ── U-X3 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class ThrowingExpiryCandidates : IQueryHandlerAsync<GetExpiryAlertCandidatesQuery, IReadOnlyList<ExpiryAlertCandidate>>
    {
        public Task<QueryResponse<IReadOnlyList<ExpiryAlertCandidate>>> HandleAsync(GetExpiryAlertCandidatesQuery query) =>
            throw new InvalidOperationException("The expiry pass blew up.");
    }

    [Fact]
    public async Task U_X3_APassThatThrowsIsLogged_AndTheNextPassStillRuns()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 3m); // below the default point of 10

        using var broken = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IQueryHandlerAsync<GetExpiryAlertCandidatesQuery, IReadOnlyList<ExpiryAlertCandidate>>, ThrowingExpiryCandidates>()));

        using (var scope = broken.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<InventoryWorkCycle>().RunAsync(default, branch.BranchId); // must not throw

        Assert.Equal(sku.SkuId, Assert.Single(await AlertsAsync(branch.Admin.Client, "&kind=LowStock")).GetProperty("skuId").GetGuid());
    }

    [Fact]
    public async Task TheReconciliationPassOfAnHonestLedgerRaisesNoLedgerMismatchAlert()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 10m);
        await DeductAsync(branch, sku, branch.ShopId, 4m);

        using (var scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<InventoryWorkCycle>().RunReconciliationPassAsync();

        Assert.Empty(await AlertsAsync(branch.Admin.Client, "&kind=LedgerMismatch"));
    }

    [Fact]
    public async Task TwoRacingRaisesOfTheSameAlert_BothSucceed_AndExactlyOneRowRemains()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client, food: true);
        var batchId = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, 3m, InDays(15));
        var key = $"expiry:{batchId}:{branch.BranchId}:30";

        async Task<bool> RaiseAsync()
        {
            using var scope = Factory.Services.CreateScope();
            var response = await scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>().SendAsync(new RaiseStockAlertCommand
            {
                AlertId = Guid.NewGuid(), Kind = "ExpiryThreshold", BranchId = branch.BranchId, SkuId = sku.SkuId, BatchId = batchId, ThresholdDays = 30, DedupKey = key,
                UserContext = new UserContext(Guid.NewGuid(), Guid.Empty), CorrelationId = Guid.NewGuid(), ValidateCommand = true
            });
            return response.IsSuccess;
        }

        for (var round = 0; round < 5; round++)
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(RaiseAsync)));
            Assert.All(results, Assert.True);
        }

        Assert.Single(await AlertsAsync(branch.Admin.Client, "&kind=ExpiryThreshold"));
    }
}
