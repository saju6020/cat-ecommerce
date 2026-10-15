using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-IMP-001 §16 phase G -- I-G1 ... I-G4: landed cost across a consignment's received batches (US-IMP-05), the <c>v_batch_unit_cost</c> view's basis
/// switching from Purchase to Landed, staleness, and who may allocate.</summary>
public sealed class InventoryLandedCostEndpointTests(InventoryApiFactory factory) : InventoryIntegrationTestBase(factory)
{
    private sealed record Setup(BranchContext Branch, Guid ConsignmentId, SeededSku Sku1, SeededSku Sku2, SeededSku Sku3, Guid Batch1, Guid Batch2, Guid Batch3, Guid Order1, Guid Line1);

    private static string Consignments => "/api/staff/procurement/consignments";

    private static async Task<int> VersionAsync(HttpClient client, Guid consignmentId) =>
        (await JsonOkAsync(await client.GetAsync($"{Consignments}/{consignmentId}"))).GetProperty("version").GetInt32();

    private static Task<HttpResponseMessage> PutCostsAsync(HttpClient client, Guid consignmentId, int version, decimal freight) =>
        client.PutAsJsonAsync($"{Consignments}/{consignmentId}/costs", new
        {
            expectedVersion = version,
            costComponents = new[]
            {
                new { kind = "Freight", amount = freight, currencyCode = "BDT", note = (string?)null },
                new { kind = "Duty", amount = 100.00m, currencyCode = "USD", note = (string?)null }
            },
            exchangeRates = new[]
            {
                new { currencyCode = "USD", rateToBase = 118.5m, recordedOn = UtcToday }, new { currencyCode = "EUR", rateToBase = 130m, recordedOn = UtcToday }
            }
        }, Json);

    /// <summary>Two purchase orders (USD: 100 at 2.50 and 40 at 10.00; EUR: 60 at 5.00) in ONE cleared consignment, three batches received, costs and rates
    /// recorded. Total costs 1000.01 + 100.00 x 118.5 = 12850.01 over 200 units.</summary>
    private async Task<Setup> SeedAsync(decimal firstLineOrdered = 100m)
    {
        var branch = await SeedBranchAsync();
        var client = branch.Admin.Client;
        var usdSupplier = await SeedSupplierAsync(client, currency: "USD");
        var eurSupplier = await SeedSupplierAsync(client, currency: "EUR");
        var (sku1, sku2, sku3) = (await CreateSkuAsync(client), await CreateSkuAsync(client), await CreateSkuAsync(client));
        var usd = await SeedIssuedOrderAsync(client, usdSupplier, branch.WarehouseId, "USD", (sku1.SkuId, firstLineOrdered, 2.5m), (sku2.SkuId, 40m, 10m));
        var eur = await SeedIssuedOrderAsync(client, eurSupplier, branch.WarehouseId, "EUR", (sku3.SkuId, 60m, 5m));

        var plan = await JsonOkAsync(await client.PostAsJsonAsync(Consignments, new
        {
            originCountryCode = "DE", shipmentReference = "BL-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(), purchaseOrderIds = new[] { usd.OrderId, eur.OrderId },
            importPermitNumber = "IP-1", healthCertificateNumber = "HC-1", customsDeclarationNumber = "CD-1"
        }, Json));
        var consignmentId = plan.GetProperty("consignmentId").GetGuid();
        foreach (var to in new[] { "InTransit", "AtCustoms", "Cleared" })
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"{Consignments}/{consignmentId}/status", new { to, on = UtcToday }, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await PutCostsAsync(client, consignmentId, await VersionAsync(client, consignmentId), 1000.01m)).StatusCode);

        var first = await PostReceiptAsync(client, [
            ReceiptLine(sku1.SkuId, branch.WarehouseId, 100m, purchaseOrderLineId: usd.LineIds[sku1.SkuId], rate: 118.5m),
            ReceiptLine(sku2.SkuId, branch.WarehouseId, 40m, purchaseOrderLineId: usd.LineIds[sku2.SkuId], rate: 118.5m)], usd.OrderId, consignmentId);
        var second = await PostReceiptAsync(client, [ReceiptLine(sku3.SkuId, branch.WarehouseId, 60m, purchaseOrderLineId: eur.LineIds[sku3.SkuId], rate: 130m)], eur.OrderId, consignmentId);

        return new Setup(branch, consignmentId, sku1, sku2, sku3, first.BatchIds[0], first.BatchIds[1], second.BatchIds[0], usd.OrderId, usd.LineIds[sku1.SkuId]);
    }

    private static async Task<JsonElement> AllocateAsync(HttpClient client, Guid consignmentId) =>
        await JsonOkAsync(await client.PostAsJsonAsync($"{Consignments}/{consignmentId}/landed-cost", new { expectedVersion = await VersionAsync(client, consignmentId) }, Json));

    private static async Task<(string Basis, decimal UnitCost)> ViewAsync(Guid batchId)
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT cost_basis, unit_cost_base FROM inventory.v_batch_unit_cost WHERE batch_id = @b", connection);
        command.Parameters.AddWithValue("b", batchId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetDecimal(1));
    }

    private static decimal Amount(JsonElement money) => decimal.Parse(money.GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task I_G1_TheCostsAreAllocatedAcrossTheReceivedBatches_AndTheAllocatedAmountsSumToTheCostsExactly()
    {
        var s = await SetupAndAllocateAsync();

        var lines = s.Result.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(3, lines.Count);
        Assert.Equal(12850.01m, Amount(s.Result.GetProperty("totalCostsBase")));
        Assert.Equal(12850.01m, lines.Sum(l => Amount(l.GetProperty("allocatedCostBase"))));

        var first = lines.Single(l => l.GetProperty("batchId").GetGuid() == s.Setup.Batch1);
        Assert.Equal(29625.00m, Amount(first.GetProperty("purchaseCostBase"))); // 100 x 2.50 x 118.5
        Assert.Equal(6425.01m, Amount(first.GetProperty("allocatedCostBase"))); // the leftover cent goes to the largest remainder
        Assert.Equal("360.5001", first.GetProperty("landedUnitCostBase").GetProperty("amount").GetString());

        var third = lines.Single(l => l.GetProperty("batchId").GetGuid() == s.Setup.Batch3);
        Assert.Equal(39000.00m, Amount(third.GetProperty("purchaseCostBase"))); // 60 x 5.00 x 130
        Assert.Equal(3855.00m, Amount(third.GetProperty("allocatedCostBase")));

        var detail = await JsonOkAsync(await s.Setup.Branch.Admin.Client.GetAsync($"{Consignments}/{s.Setup.ConsignmentId}"));
        var landed = detail.GetProperty("landedCost");
        Assert.False(landed.GetProperty("isStale").GetBoolean());
        Assert.Equal(3, landed.GetProperty("lines").GetArrayLength());
        Assert.Equal(12850.01m, Amount(landed.GetProperty("totalCostsBase")));
    }

    private async Task<(Setup Setup, JsonElement Result)> SetupAndAllocateAsync()
    {
        var setup = await SeedAsync();
        return (setup, await AllocateAsync(setup.Branch.Admin.Client, setup.ConsignmentId));
    }

    [Fact]
    public async Task I_G2_TheBatchUnitCostViewSwitchesFromPurchaseToLanded_AndAnAdjustmentIsValuedAtTheLandedCost()
    {
        var s = await SeedAsync();
        var client = s.Branch.Admin.Client;
        Assert.Equal(("Purchase", 296.25m), await ViewAsync(s.Batch1)); // 2.50 x 118.5

        async Task<decimal> AdjustedValueAsync()
        {
            var body = await JsonOkAsync(await client.PostAsJsonAsync("/api/staff/inventory/adjustments", new
            {
                locationId = s.Branch.WarehouseId, reason = "Damage", note = "One tin dented.", lines = new[] { new { skuId = s.Sku1.SkuId, batchId = s.Batch1, quantityDelta = -1m } }
            }, Json));
            return Amount(body.GetProperty("valueBase"));
        }

        Assert.Equal(296.25m, await AdjustedValueAsync());

        await AllocateAsync(client, s.ConsignmentId);

        Assert.Equal(("Landed", 360.5001m), await ViewAsync(s.Batch1));
        Assert.Equal(360.50m, await AdjustedValueAsync());
    }

    [Fact]
    public async Task I_G3_ChangingTheCostsOrReceivingAnotherBatch_MakesTheAllocationStale_AndReAllocatingClearsIt()
    {
        var s = await SeedAsync(firstLineOrdered: 120m);
        var client = s.Branch.Admin.Client;
        await AllocateAsync(client, s.ConsignmentId);

        async Task<bool> IsStaleAsync() =>
            (await JsonOkAsync(await client.GetAsync($"{Consignments}/{s.ConsignmentId}"))).GetProperty("landedCost").GetProperty("isStale").GetBoolean();

        Assert.False(await IsStaleAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await PutCostsAsync(client, s.ConsignmentId, await VersionAsync(client, s.ConsignmentId), 2000m)).StatusCode);
        Assert.True(await IsStaleAsync());

        var reallocated = await AllocateAsync(client, s.ConsignmentId);
        Assert.False(await IsStaleAsync());
        Assert.Equal(13850.00m, Amount(reallocated.GetProperty("totalCostsBase"))); // 2000.00 + 11 850.00

        await PostReceiptAsync(client, [ReceiptLine(s.Sku1.SkuId, s.Branch.WarehouseId, 10m, purchaseOrderLineId: s.Line1, rate: 118.5m)], s.Order1, s.ConsignmentId);
        Assert.True(await IsStaleAsync());

        var again = await AllocateAsync(client, s.ConsignmentId);
        Assert.False(await IsStaleAsync());
        Assert.Equal(4, again.GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task I_G3_AStaleExpectedVersion_Is409_AndAnUnrecordedCurrencyIsRefusedNamingIt()
    {
        var s = await SeedAsync();
        var client = s.Branch.Admin.Client;

        var stale = await client.PostAsJsonAsync($"{Consignments}/{s.ConsignmentId}/landed-cost", new { expectedVersion = 999 }, Json);
        await AssertErrorAsync(stale, BusinessViolationCodes.ConsignmentVersionConflict, HttpStatusCode.Conflict);

        // drop the EUR/USD rates: the USD duty and the batches bought in USD/EUR can no longer be converted
        var noRates = await client.PutAsJsonAsync($"{Consignments}/{s.ConsignmentId}/costs", new
        {
            expectedVersion = await VersionAsync(client, s.ConsignmentId),
            costComponents = new[] { new { kind = "Freight", amount = 10m, currencyCode = "BDT", note = (string?)null } }, exchangeRates = Array.Empty<object>()
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, noRates.StatusCode);

        var refused = await client.PostAsJsonAsync($"{Consignments}/{s.ConsignmentId}/landed-cost", new { expectedVersion = await VersionAsync(client, s.ConsignmentId) }, Json);
        await AssertErrorAsync(refused, BusinessViolationCodes.ExchangeRateMissing);
        Assert.Contains("USD", await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task I_G4_AnAccountantMayAllocate_AndReceptionMayNot()
    {
        var s = await SeedAsync();
        var accountant = await StaffAsync(s.Branch, StaffRoles.Accountant);
        var reception = await StaffAsync(s.Branch, StaffRoles.Reception);
        var version = await VersionAsync(s.Branch.Admin.Client, s.ConsignmentId);

        var denied = await reception.Client.PostAsJsonAsync($"{Consignments}/{s.ConsignmentId}/landed-cost", new { expectedVersion = version }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(("Purchase", 296.25m), await ViewAsync(s.Batch1));

        var allowed = await accountant.Client.PostAsJsonAsync($"{Consignments}/{s.ConsignmentId}/landed-cost", new { expectedVersion = version }, Json);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal("Landed", (await ViewAsync(s.Batch1)).Basis);
    }

    [Fact]
    public async Task AConsignmentWithNothingReceivedCannotBeAllocated()
    {
        var branch = await SeedBranchAsync();
        var client = branch.Admin.Client;
        var supplier = await SeedSupplierAsync(client, currency: "BDT");
        var sku = await CreateSkuAsync(client);
        var order = await SeedIssuedOrderAsync(client, supplier, branch.WarehouseId, "BDT", (sku.SkuId, 10m, 5m));
        var consignmentId = await SeedClearedConsignmentAsync(client, order.OrderId);

        var response = await client.PostAsJsonAsync($"{Consignments}/{consignmentId}/landed-cost", new { expectedVersion = await VersionAsync(client, consignmentId) }, Json);

        await AssertErrorAsync(response, BusinessViolationCodes.NoReceivedQuantityToAllocate);
    }
}
