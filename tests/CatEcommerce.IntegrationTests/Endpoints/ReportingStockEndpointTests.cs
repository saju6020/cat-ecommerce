using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-RPT-001 section 16 C -- I-C1 to I-C4: the stock reports over data created through HO-IMP-001's and HO-SHP-001's own endpoints and the stock port (the same port HO-ORD-001 sells through).
/// One fresh branch per test (section 16.1 rule 1).
/// </summary>
public sealed class ReportingStockEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private const string Consignments = "/api/staff/procurement/consignments";

    private static string Window(DateOnly from, DateOnly to, Guid branchId, string? extra = null) =>
        $"?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&branchId={branchId}" + (extra is null ? "" : "&" + extra);

    private static async Task<JsonElement> ReportAsync(HttpClient client, string key, BranchContext branch, DateOnly from, DateOnly to, string? extra = null)
    {
        var response = await client.GetAsync($"/api/staff/reports/{key}" + Window(from, to, branch.BranchId, extra));
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private static List<JsonElement> RowsOf(JsonElement report) => report.GetProperty("rows").EnumerateArray().ToList();

    private static decimal Number(JsonElement element) => decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture);

    private static decimal MoneyOf(JsonElement element) => decimal.Parse(element.GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);

    /// <summary>A batch whose consignment is cleared, costed and allocated: its landed unit cost is <c>(qty x unitPurchase + freight) / qty</c>.</summary>
    private Task<Guid> SeedLandedBatchAsync(BranchContext branch, SeededSku sku, decimal quantity, decimal unitPurchase, decimal freight, DateOnly? expires = null) =>
        ReportingLandedCostSeeder.SeedAsync(branch.Admin.Client, sku.SkuId, branch.WarehouseId, quantity, unitPurchase, freight, expires);

    private async Task SellAsync(BranchContext branch, SeededSku sku, Guid locationId, decimal quantity)
    {
        await using var port = OpenPort();
        var result = await port.Ledger.DeductAsync(
            Demand(branch.BranchId, sku.SkuId, quantity, MovementKind.Sale, locations: [locationId]),
            new StockReference(StockReferenceKind.PosSale, Guid.NewGuid(), null, "POS-" + Random.Shared.Next(1000, 9999)), new StockParty(null, null), default);
        Assert.True(result.IsSuccess, result.Message);
        await port.CommitAsync();
    }

    // ── I-C1: US-RPT-02 -- stock on hand and movement with valuation ──────────────────────────────────────────
    [Fact]
    public async Task I_C1_TheRollForward_EqualsTheInventoryProjection_ValuesTheCostedBatch_AndSaysWhatItCouldNotValue()
    {
        var branch = await SeedBranchAsync();
        var admin = branch.Admin.Client;
        var food = await CreateSkuAsync(admin, food: true);
        var landed = await SeedLandedBatchAsync(branch, food, 20m, 5m, 100m, expires: UtcToday.AddDays(300)); // landed unit cost (20 x 5 + 100) / 20 = 10
        var plainSku = await CreateSkuAsync(admin, food: true);
        var plain = await ReceiveAsync(admin, plainSku.SkuId, branch.WarehouseId, 8m, UtcToday.AddDays(300)); // received at purchase cost only: no landed cost

        await SellAsync(branch, food, branch.WarehouseId, 3m);
        var transfer = await admin.PostAsJsonAsync("/api/staff/inventory/transfers", new
        { fromLocationId = branch.WarehouseId, toLocationId = branch.ShopId, reason = "Restock", lines = new[] { new { skuId = food.SkuId, batchId = landed, quantity = 4m } } }, Json);
        Assert.Equal(HttpStatusCode.Created, transfer.StatusCode);

        var report = await ReportAsync(admin, ReportCatalogue.StockPositionKey, branch, UtcToday.AddDays(-1), UtcToday.AddDays(1));
        var rows = RowsOf(report);

        Assert.Equal(3, rows.Count);
        Assert.All(rows, r => Assert.Equal(0m, Number(r.GetProperty("openingQuantity"))));

        // Closing equals HO-IMP-001's own position for each batch and location.
        var warehouseLanded = rows.Single(r => r.GetProperty("skuCode").GetString() == food.SkuCode && r.GetProperty("locationName").GetString()!.StartsWith("Warehouse", StringComparison.Ordinal));
        var shopLanded = rows.Single(r => r.GetProperty("skuCode").GetString() == food.SkuCode && r.GetProperty("locationName").GetString()!.StartsWith("ShopFloor", StringComparison.Ordinal));
        var warehousePlain = rows.Single(r => r.GetProperty("skuCode").GetString() == plainSku.SkuCode);

        Assert.Equal((await PositionAsync(landed, branch.WarehouseId)).OnHand, Number(warehouseLanded.GetProperty("closingQuantity")));
        Assert.Equal((await PositionAsync(landed, branch.ShopId)).OnHand, Number(shopLanded.GetProperty("closingQuantity")));
        Assert.Equal((await PositionAsync(plain, branch.WarehouseId)).OnHand, Number(warehousePlain.GetProperty("closingQuantity")));

        Assert.Equal(20m, Number(warehouseLanded.GetProperty("receivedQuantity")));
        Assert.Equal(7m, Number(warehouseLanded.GetProperty("issuedQuantity"))); // 3 sold + 4 transferred out
        Assert.Equal(13m, Number(warehouseLanded.GetProperty("closingQuantity")));
        Assert.Equal(4m, Number(shopLanded.GetProperty("receivedQuantity"))); // the other half of the transfer
        Assert.Equal(10m, MoneyOf(warehouseLanded.GetProperty("landedUnitCost")));
        Assert.Equal(130m, MoneyOf(warehouseLanded.GetProperty("valuation")));
        Assert.Equal(40m, MoneyOf(shopLanded.GetProperty("valuation")));

        // The batch with no landed cost has no valuation -- null, not zero -- the total leaves it out, and a note says how much is covered.
        Assert.Equal(JsonValueKind.Null, warehousePlain.GetProperty("valuation").ValueKind);
        Assert.Equal(170m, MoneyOf(report.GetProperty("totals").GetProperty("valuation")));
        Assert.Contains(report.GetProperty("notes").EnumerateArray(), n => n.GetProperty("code").GetString() == "ValuationCoverage" && n.GetProperty("text").GetString()!.Contains("2 of 3"));
    }

    // ── I-C2: US-RPT-02 -- near-expiry and expired ──────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C2_TheNearExpiryReport_BandsTheBatches_ValuesThemAtCostAndRetail_AndListsTheExpiredWithStock()
    {
        var branch = await SeedBranchAsync();
        var admin = branch.Admin.Client;
        var landedSku = await CreateSkuAsync(admin, food: true);
        var landed = await SeedLandedBatchAsync(branch, landedSku, 5m, 4m, 50m, expires: UtcToday.AddDays(20)); // landed unit cost (5 x 4 + 50) / 5 = 14
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/staff/shop/skus/{landedSku.SkuId}/prices", new { amount = 30m, currencyCode = "BDT", effectiveFrom = DateTimeOffset.UtcNow }, Json)).StatusCode);

        var fifty = await ReceiveAsync(admin, (await CreateSkuAsync(admin, food: true)).SkuId, branch.WarehouseId, 3m, UtcToday.AddDays(50));
        var eighty = await ReceiveAsync(admin, (await CreateSkuAsync(admin, food: true)).SkuId, branch.WarehouseId, 3m, UtcToday.AddDays(80));
        var expired = await ReceiveAsync(admin, (await CreateSkuAsync(admin, food: true)).SkuId, branch.WarehouseId, 2m, UtcToday.AddDays(5));
        await SetBatchExpiryDirectlyAsync(expired, UtcToday.AddDays(-3));

        var report = await ReportAsync(admin, ReportCatalogue.StockNearExpiryKey, branch, UtcToday, UtcToday.AddDays(90));
        var rows = RowsOf(report);

        Assert.Equal(4, rows.Count);
        Assert.Equal(new HashSet<string?> { "Expired", "Within 30 days", "Within 60 days", "Within 90 days" }, rows.Select(r => r.GetProperty("band").GetString()).ToHashSet());

        var near = rows.Single(r => r.GetProperty("skuCode").GetString() == landedSku.SkuCode);
        Assert.InRange(near.GetProperty("daysToExpiry").GetInt64(), 19, 20); // the report counts from the branch-local date
        Assert.Equal(70m, MoneyOf(near.GetProperty("valueAtRiskCost"))); // 5 x 14
        Assert.Equal(150m, MoneyOf(near.GetProperty("valueAtRiskRetail"))); // 5 x 30
        Assert.Equal(70m, MoneyOf(report.GetProperty("totals").GetProperty("valueAtRiskCost"))); // the batches with no landed cost are left out of the total

        var gone = rows.Single(r => r.GetProperty("band").GetString() == "Expired");
        Assert.InRange(gone.GetProperty("daysToExpiry").GetInt64(), -4, -3);
        Assert.Equal(JsonValueKind.Null, gone.GetProperty("valueAtRiskCost").ValueKind);

        var withoutExpired = await ReportAsync(admin, ReportCatalogue.StockNearExpiryKey, branch, UtcToday, UtcToday.AddDays(90), "includeExpired=false");
        Assert.Equal(3, RowsOf(withoutExpired).Count);
        Assert.NotEqual(Guid.Empty, fifty);
        Assert.NotEqual(Guid.Empty, eighty);
        Assert.NotEqual(Guid.Empty, landed);
    }

    // ── I-C3: US-RPT-02 -- low stock and the reorder suggestion HO-IMP-001 itself would make ──────────────
    [Fact]
    public async Task I_C3_TheLowStockRow_CarriesTheSuggestedQuantity_HO_IMP_001_ItselfGivesForTheSameSkuAndLocation()
    {
        var branch = await SeedBranchAsync();
        var admin = branch.Admin.Client;
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var sku = await CreateSkuAsync(admin);

        var policy = await keeper.Client.PutAsJsonAsync($"/api/staff/inventory/reorder-policies/{sku.SkuId}/{branch.ShopId}", new { reorderPoint = 10m, reorderQuantity = (decimal?)null, preferredSupplierId = (Guid?)null }, Json);
        Assert.Equal(HttpStatusCode.NoContent, policy.StatusCode);
        await ReceiveAsync(admin, sku.SkuId, branch.ShopId, 12m);
        await SellAsync(branch, sku, branch.ShopId, 7m); // 5 left, below the point of 10

        var report = await ReportAsync(admin, ReportCatalogue.StockLowReorderKey, branch, UtcToday.AddDays(-88), UtcToday.AddDays(1)); // a 90-day window ending tomorrow so it holds 'now' whatever the time of day; HO-IMP-001's own window is 90 days, so the two suggestions use the same consumption
        var row = Assert.Single(RowsOf(report), r => r.GetProperty("skuCode").GetString() == sku.SkuCode);

        var own = await JsonOkAsync(await admin.GetAsync("/api/staff/procurement/reorder-suggestions"));
        var theirs = Assert.Single(own.GetProperty("items").EnumerateArray(), i => i.GetProperty("skuId").GetGuid() == sku.SkuId && i.GetProperty("locationId").GetGuid() == branch.ShopId);

        Assert.Equal(Number(theirs.GetProperty("suggestedQuantity")), Number(row.GetProperty("suggestedQuantity")));
        Assert.Equal(5m, Number(row.GetProperty("onHand")));
        Assert.Equal(10m, Number(row.GetProperty("reorderPoint")));
        Assert.True(Number(row.GetProperty("suggestedQuantity")) > 0m);
        Assert.Equal(theirs.GetProperty("leadTimeDays").GetInt32(), row.GetProperty("leadTimeDays").GetInt64());
    }

    /// <summary>
    /// U-C4 parity pin (gate re-check row 56): R7 reads views, so its pair selection and on-order figure are copies of HO-IMP-001's rule. This pins the copy to IMP's own suggestion list over the cases where a
    /// copy would drift -- stock below a policy, an issued purchase order (on order), a policy with no stock at all, and stock sitting at the quarantine location: both must list the same pairs with the
    /// same suggested quantity and lead time. If IMP changes its rule, this fails.
    /// </summary>
    [Fact]
    public async Task I_C3_R7ListsTheSamePairsAndSuggestionsAsHO_IMP_001sOwnList_IncludingOnOrder_PolicyOnlyPairs_AndQuarantine()
    {
        var branch = await SeedBranchAsync();
        var admin = branch.Admin.Client;
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var supplier = await SeedSupplierAsync(admin, currency: "BDT");

        var onOrder = await CreateSkuAsync(admin);      // below its point, with an issued purchase order
        var policyOnly = await CreateSkuAsync(admin);   // a policy at the warehouse, no stock anywhere
        var quarantined = await CreateSkuAsync(admin);  // stock only at the quarantine location

        async Task PolicyAsync(Guid skuId, Guid locationId, decimal point, decimal? quantity) =>
            Assert.Equal(HttpStatusCode.NoContent, (await keeper.Client.PutAsJsonAsync($"/api/staff/inventory/reorder-policies/{skuId}/{locationId}",
                new { reorderPoint = point, reorderQuantity = quantity, preferredSupplierId = (Guid?)supplier }, Json)).StatusCode);

        await PolicyAsync(onOrder.SkuId, branch.ShopId, 10m, null);
        await ReceiveAsync(admin, onOrder.SkuId, branch.ShopId, 12m);
        await SellAsync(branch, onOrder, branch.ShopId, 9m);
        await SeedIssuedOrderAsync(admin, supplier, branch.ShopId, "BDT", (onOrder.SkuId, 30m, 5m));

        await PolicyAsync(policyOnly.SkuId, branch.WarehouseId, 5m, 20m);
        await ReceiveAsync(admin, quarantined.SkuId, branch.QuarantineId, 3m);

        var report = await ReportAsync(admin, ReportCatalogue.StockLowReorderKey, branch, UtcToday.AddDays(-88), UtcToday.AddDays(1));
        var own = await JsonOkAsync(await admin.GetAsync("/api/staff/procurement/reorder-suggestions"));

        var mine = RowsOf(report).Select(r => r.GetProperty("skuCode").GetString()).ToHashSet();
        var theirs = own.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("locationId").GetGuid() is var l && (l == branch.ShopId || l == branch.WarehouseId || l == branch.QuarantineId)).ToList();

        foreach (var sku in new[] { onOrder, policyOnly, quarantined })
        {
            var imp = theirs.Where(i => i.GetProperty("skuId").GetGuid() == sku.SkuId).ToList();
            // A SKU that has never been received has no batch to name it (IMP lists it with an empty code too), so R7's row is found by its location.
            var warehouseCode = theirs.Where(i => i.GetProperty("skuId").GetGuid() == policyOnly.SkuId).Select(i => i.GetProperty("locationCode").GetString()).FirstOrDefault();
            var r7 = RowsOf(report).Where(r => sku.SkuId == policyOnly.SkuId
                ? string.IsNullOrEmpty(r.GetProperty("skuCode").GetString()) && r.GetProperty("locationName").GetString() == warehouseCode
                : r.GetProperty("skuCode").GetString() == sku.SkuCode).ToList();
            Assert.True(imp.Count == r7.Count, $"{sku.SkuCode}: IMP lists {imp.Count} ({string.Join(";", imp.Select(i => i.GetProperty("locationCode").GetString() + "=" + i.GetProperty("suggestedQuantity")))}), R7 lists {r7.Count}");

            foreach (var item in imp)
            {
                var row = r7.Single(r => r.GetProperty("locationName").GetString() == item.GetProperty("locationCode").GetString());
                Assert.Equal(Number(item.GetProperty("suggestedQuantity")), Number(row.GetProperty("suggestedQuantity")));
                Assert.Equal(item.GetProperty("leadTimeDays").GetInt32(), row.GetProperty("leadTimeDays").GetInt64());
            }
        }

        Assert.Contains(onOrder.SkuCode, mine);                                   // below its point
        Assert.NotEmpty(theirs.Where(i => i.GetProperty("skuId").GetGuid() == policyOnly.SkuId));   // a policy-only pair is listed by IMP, and (above) by R7
        Assert.DoesNotContain(theirs, i => i.GetProperty("locationId").GetGuid() == branch.QuarantineId);   // stock at quarantine is never a reorder pair, in either list
        Assert.DoesNotContain(quarantined.SkuCode, mine);
    }

    // ── I-C4: SRS section 8.1 -- import consignments ──────────────────────────────────────────────────────
    [Fact]
    public async Task I_C4_TheConsignmentReport_ShowsTheDocumentsOnRecord_AndNoLandedCostBeforeTheCostsAreAllocated()
    {
        var branch = await SeedBranchAsync();
        var admin = branch.Admin.Client;
        var sku = await CreateSkuAsync(admin, food: true);
        var supplier = await SeedSupplierAsync(admin, currency: "BDT");
        var order = await SeedIssuedOrderAsync(admin, supplier, branch.WarehouseId, "BDT", (sku.SkuId, 10m, 5m));

        // A permit and a health certificate, but no customs declaration.
        var plan = await JsonOkAsync(await admin.PostAsJsonAsync(Consignments, new
        {
            originCountryCode = "DE", shipmentReference = "BL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), purchaseOrderIds = new[] { order.OrderId },
            importPermitNumber = "IP-9", healthCertificateNumber = "HC-9", customsDeclarationNumber = (string?)null
        }, Json));
        var reference = plan.GetProperty("reference").GetString();

        var report = await ReportAsync(admin, ReportCatalogue.ImportConsignmentsKey, branch, UtcToday.AddDays(-1), UtcToday.AddDays(1));
        var row = Assert.Single(RowsOf(report));

        Assert.Equal(reference, row.GetProperty("reference").GetString());
        Assert.Equal("DE", row.GetProperty("originCountry").GetString());
        Assert.True(row.GetProperty("hasPermit").GetBoolean());
        Assert.True(row.GetProperty("hasHealthCertificate").GetBoolean());
        Assert.False(row.GetProperty("hasCustomsDeclaration").GetBoolean());
        Assert.Equal(1, row.GetProperty("poCount").GetInt64());
        Assert.Equal("Planned", row.GetProperty("clearanceStatus").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("landedCostTotal").ValueKind); // not estimated before the costs are allocated
        Assert.Contains(report.GetProperty("notes").EnumerateArray(), n => n.GetProperty("code").GetString() == "LandedCostPending");
    }

    // ── Access: stock reports are for the Inventory role and administrators; the shop manager has none of them ──
    [Fact]
    public async Task TheStockReports_AreForInventoryAndAdministrators_NotTheShopManager()
    {
        var branch = await SeedBranchAsync();
        var keeper = await StaffAsync(branch, StaffRoles.Inventory);
        var shop = await StaffAsync(branch, StaffRoles.ShopManager);

        var allowed = await keeper.Client.GetAsync($"/api/staff/reports/{ReportCatalogue.StockPositionKey}" + Window(UtcToday, UtcToday, branch.BranchId));
        var denied = await shop.Client.GetAsync($"/api/staff/reports/{ReportCatalogue.StockPositionKey}" + Window(UtcToday, UtcToday, branch.BranchId));

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
