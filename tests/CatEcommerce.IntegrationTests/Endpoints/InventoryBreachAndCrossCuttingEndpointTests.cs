using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-IMP-001 section 16 phase J and cross-cutting -- I-J1, I-J2 (storage breaches and the storage profile), the ledger CSV export and a sample 403 per action (I-X2).</summary>
public sealed class InventoryBreachAndCrossCuttingEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private const string Breaches = "/api/staff/inventory/storage-breaches";

    // ── I-J1 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_J1_AReportedBreachFlagsTheBatchesHeld_RaisesAnAlert_AndQuarantineMovesTheStockToTheQuarantineLocation()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var nurse = await StaffAsync(branch, StaffRoles.Nurse);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var first = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.BoardingId, 6m);
        var second = await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.BoardingId, 4m);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 9m); // another location: not affected

        var report = await nurse.Client.PostAsJsonAsync(Breaches, new { locationId = branch.BoardingId, condition = "The fridge door was left open overnight.", temperatureC = 14.5m, durationMinutes = 480 }, Json);
        var body = await JsonOkAsync(report);
        var breachId = body.GetProperty("breachId").GetGuid();

        Assert.Equal(HttpStatusCode.Created, report.StatusCode);
        Assert.Equal(2, body.GetProperty("affectedBatchCount").GetInt32());
        var detail = await JsonOkAsync(await keeper.Client.GetAsync($"{Breaches}/{breachId}"));
        Assert.Equal(2, detail.GetProperty("affectedBatches").GetArrayLength());
        Assert.Equal("Open", detail.GetProperty("summary").GetProperty("status").GetString());

        var alerts = (await JsonOkAsync(await keeper.Client.GetAsync("/api/staff/inventory/alerts?kind=StorageBreach"))).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(branch.BoardingId, Assert.Single(alerts).GetProperty("locationId").GetGuid());

        // reporting moved nothing; a nurse may report but not quarantine
        Assert.Equal((6m, 0m), await PositionAsync(first, branch.BoardingId));
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.Client.PostAsync($"{Breaches}/{breachId}/quarantine", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await keeper.Client.PostAsync($"{Breaches}/{breachId}/quarantine", null)).StatusCode);

        Assert.Equal((0m, 0m), await PositionAsync(first, branch.BoardingId));
        Assert.Equal((6m, 0m), await PositionAsync(first, branch.QuarantineId));
        Assert.Equal((4m, 0m), await PositionAsync(second, branch.QuarantineId));
        var ledger = (await JsonOkAsync(await keeper.Client.GetAsync(
            $"/api/staff/inventory/ledger?from={UtcToday.AddDays(-1):yyyy-MM-dd}&to={UtcToday.AddDays(1):yyyy-MM-dd}&referenceKind=StorageBreach&referenceId={breachId}")))
            .GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(4, ledger.Count); // out and in per batch, the report as the reference
        var (ledgerSum, positionSum) = await LedgerVersusPositionAsync([first, second]);
        Assert.Equal(ledgerSum, positionSum);

        // the stock now sits at the quarantine location, so none of it is usable
        var summary = await JsonOkAsync(await keeper.Client.GetAsync($"/api/staff/inventory/stock?search={sku.SkuCode}"));
        Assert.Equal("9", Assert.Single(summary.GetProperty("items").EnumerateArray()).GetProperty("usable").GetString());
        Assert.Equal("10", Assert.Single(summary.GetProperty("items").EnumerateArray()).GetProperty("heldQuantity").GetString());

        await AssertErrorAsync(await keeper.Client.PostAsync($"{Breaches}/{breachId}/quarantine", null), BusinessViolationCodes.InvalidStorageBreachTransition);

        await AssertErrorAsync(await keeper.Client.PostAsJsonAsync($"{Breaches}/{breachId}/close", new { note = "" }, Json), BusinessViolationCodes.InvalidStorageBreachTransition);
        Assert.Equal(HttpStatusCode.NoContent, (await keeper.Client.PostAsJsonAsync($"{Breaches}/{breachId}/close", new { note = "Written off after the vet inspected it." }, Json)).StatusCode);
        Assert.Equal("Closed", (await JsonOkAsync(await keeper.Client.GetAsync($"{Breaches}/{breachId}"))).GetProperty("summary").GetProperty("status").GetString());
    }

    [Fact]
    public async Task I_J1_ABreachAtAnEmptyLocation_OrAnotherBranchsLocation_IsRefused()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var other = await SeedBranchAsync();

        await AssertErrorAsync(
            await keeper.Client.PostAsJsonAsync(Breaches, new { locationId = branch.BoardingId, condition = "Warm." }, Json), BusinessViolationCodes.NoStockAtLocation);
        Assert.Equal(HttpStatusCode.NotFound,
            (await keeper.Client.PostAsJsonAsync(Breaches, new { locationId = other.BoardingId, condition = "Warm." }, Json)).StatusCode);
    }

    // ── I-J2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_J2_TheStorageProfileComesWithTheStockReads()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        var put = await branch.Admin.Client.PutAsJsonAsync($"/api/staff/inventory/storage-profiles/{sku.ProductId}", new
        { condition = "Refrigerated", minTemperatureC = 2m, maxTemperatureC = 8m, handlingNotes = "Keep upright." }, Json);
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 3m);

        var matrix = await JsonOkAsync(await branch.Admin.Client.GetAsync($"/api/staff/inventory/stock/{sku.SkuId}"));

        var storage = matrix.GetProperty("storage");
        Assert.Equal("Refrigerated", storage.GetProperty("condition").GetString());
        Assert.Equal(8m, storage.GetProperty("maxTemperatureC").GetDecimal());
    }

    // ── ledger CSV ──────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheLedgerExportIsACsv_RequiresTheReportsAction_AndRefusesAMissingRange()
    {
        var branch = await SeedBranchAsync();
        var reception = await StaffAsync(branch, StaffRoles.Reception);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 3m, lot: "=CMD-LOT");
        var range = $"from={UtcToday.AddDays(-1):yyyy-MM-dd}&to={UtcToday.AddDays(1):yyyy-MM-dd}";

        var csv = await branch.Admin.Client.GetAsync($"/api/staff/inventory/ledger/export.csv?{range}&skuId={sku.SkuId}");

        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.Contains("\"'=CMD-LOT\"", text); // the injection guard on a text cell
        Assert.Contains("\"3\"", text);

        Assert.Equal(HttpStatusCode.Forbidden, (await reception.Client.GetAsync($"/api/staff/inventory/ledger/export.csv?{range}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await branch.Admin.Client.GetAsync("/api/staff/inventory/ledger/export.csv")).StatusCode);
    }

    // ── I-X2 ────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_X2_ASampleEndpointOfEveryActionRefusesARoleThatLacksIt()
    {
        var branch = await SeedBranchAsync();
        var reception = await StaffAsync(branch, StaffRoles.Reception);
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var id = Guid.NewGuid();
        var body = JsonContent.Create(new { }, options: Json);

        var denied = new (HttpClient Client, string Action, Func<Task<HttpResponseMessage>> Call)[]
        {
            (reception.Client, "Stock.Receive", () => reception.Client.PostAsJsonAsync("/api/staff/inventory/goods-receipts", new { lines = Array.Empty<object>() }, Json)),
            (reception.Client, "Stock.Transfer", () => reception.Client.PostAsJsonAsync("/api/staff/inventory/transfers", new { fromLocationId = id, toLocationId = id, lines = Array.Empty<object>() }, Json)),
            (reception.Client, "Stock.Adjust", () => reception.Client.PostAsJsonAsync("/api/staff/inventory/adjustments", new { locationId = id, lines = Array.Empty<object>() }, Json)),
            (keeper.Client, "Stock.ApproveAdjustment", () => keeper.Client.PostAsJsonAsync($"/api/staff/inventory/adjustments/{id}/approve", new { }, Json)),
            (reception.Client, "Stock.Count", () => reception.Client.PostAsJsonAsync("/api/staff/inventory/counts", new { locationId = id, scope = "FullLocation" }, Json)),
            (reception.Client, "Batches.ManageStatus", () => reception.Client.PostAsJsonAsync($"/api/staff/inventory/batches/{id}/quarantine", new { reason = "Check it." }, Json)),
            (reception.Client, "Recalls.Manage", () => reception.Client.GetAsync("/api/staff/inventory/recalls")),
            (reception.Client, "StorageBreaches.Manage", () => reception.Client.PostAsync($"{Breaches}/{id}/quarantine", null)),
            (reception.Client, "ReorderPolicies.Manage", () => reception.Client.PutAsJsonAsync($"/api/staff/inventory/reorder-policies/{id}/{id}", new { reorderPoint = 1m }, Json)),
            (reception.Client, "StockAlerts.Acknowledge", () => reception.Client.PostAsJsonAsync($"/api/staff/inventory/alerts/{id}/acknowledge", new { }, Json)),
            (reception.Client, "InventoryReports.View", () => reception.Client.GetAsync("/api/staff/inventory/reports/reconciliation")),
            (reception.Client, "Consignments.ManageCosts", () => reception.Client.PostAsJsonAsync($"/api/staff/procurement/consignments/{id}/landed-cost", new { expectedVersion = 1 }, Json)),
            (reception.Client, "Procurement.View", () => reception.Client.GetAsync("/api/staff/procurement/reorder-suggestions")),
        };

        foreach (var (_, action, call) in denied)
            Assert.True(HttpStatusCode.Forbidden == (await call()).StatusCode, $"{action} did not refuse a role that lacks it");

        Assert.NotNull(body);
    }

    [Fact]
    public async Task TheRetentionReviewSourceCountsOnlyFinancialRecordsPastTheirPeriod_AndDeletesNothing()
    {
        var branch = await SeedBranchAsync();
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.ShopId, 3m);
        using var scope = Factory.Services.CreateScope();
        var source = scope.ServiceProvider.GetServices<IRetentionReviewSource>().Single(s => s.SourceKey == "inventory-financial");

        Assert.DoesNotContain(await source.CountPastRetentionAsync(UtcToday.AddYears(-1), default), c => c.Count < 0);
        var later = await source.CountPastRetentionAsync(UtcToday.AddYears(InventoryRules.DefaultFinancialRetentionYears + 1), default);

        Assert.All(later, c => Assert.Equal(RecordClass.Financial, c.RecordClass));
        Assert.Contains(later, c => c.RecordKind == "goods-receipt" && c.Count >= 1);
        Assert.Equal(1, (await JsonOkAsync(await branch.Admin.Client.GetAsync("/api/staff/inventory/goods-receipts"))).GetProperty("items").GetArrayLength()); // still there
    }

    [Fact]
    public async Task AnObservedTimeWithAnOffsetIsAccepted_AndAFutureOneIsRefused()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(branch.Admin.Client);
        await ReceiveAsync(branch.Admin.Client, sku.SkuId, branch.BoardingId, 2m);
        var local = DateTimeOffset.UtcNow.AddHours(-2).ToOffset(TimeSpan.FromHours(6)); // the same instant as a +06:00 wall-clock time

        var accepted = await keeper.Client.PostAsJsonAsync(Breaches, new { locationId = branch.BoardingId, condition = "Warm.", observedOn = local.ToString("O") }, Json);

        var body = await JsonOkAsync(accepted);
        var detail = await JsonOkAsync(await keeper.Client.GetAsync($"{Breaches}/{body.GetProperty("breachId").GetGuid()}"));
        var stored = detail.GetProperty("summary").GetProperty("observedOn").GetDateTimeOffset();
        Assert.True((local - stored).Duration() < TimeSpan.FromSeconds(1), $"{local:O} vs {stored:O}");

        var future = await keeper.Client.PostAsJsonAsync(Breaches, new { locationId = branch.BoardingId, condition = "Warm.", observedOn = DateTimeOffset.UtcNow.AddDays(2).ToString("O") }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
    }
}
