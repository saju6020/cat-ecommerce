using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-RPT-001 section 16.1 rule 3 -- creates a batch with a LANDED cost through HO-IMP-001's own endpoints: a supplier, an issued purchase order, a consignment taken to Cleared, its costs, the goods receipt under both,
/// and the landed-cost allocation. The batch's landed unit cost is <c>(quantity x unitPurchase + freight) / quantity</c>. Shared by the stock and the revenue tests.
/// </summary>
internal static class ReportingLandedCostSeeder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Consignments = "/api/staff/procurement/consignments";

    public static async Task<Guid> SeedAsync(HttpClient admin, Guid skuId, Guid locationId, decimal quantity, decimal unitPurchase, decimal freight, DateOnly? expires)
    {
        var supplier = (await Read(await admin.PostAsJsonAsync("/api/staff/procurement/suppliers", new
        {
            code = "S" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), name = "Supplier " + Guid.NewGuid().ToString("N")[..6], countryCode = "DE", contactName = "Anna", phone = "+49 30 1234",
            email = "anna@example.test", address = "Berlin", currencyCode = "BDT", leadTimeDays = 21, paymentTerms = "30 days", paymentTermDays = 30,
        }, Json))).GetProperty("supplierId").GetGuid();

        var created = await Read(await admin.PostAsJsonAsync("/api/staff/procurement/purchase-orders", new
        {
            supplierId = supplier, deliverToLocationId = locationId, currencyCode = "BDT", expectedDeliveryOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
            lines = new[] { new { skuId, orderedQuantity = quantity, unitCostAmount = unitPurchase } },
        }, Json));
        var orderId = created.GetProperty("purchaseOrderId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/api/staff/procurement/purchase-orders/{orderId}/issue", null)).StatusCode);
        var detail = await Read(await admin.GetAsync($"/api/staff/procurement/purchase-orders/{orderId}"));
        var lineId = detail.GetProperty("lines").EnumerateArray().Single().GetProperty("lineId").GetGuid();

        var consignmentId = (await Read(await admin.PostAsJsonAsync(Consignments, new
        {
            originCountryCode = "DE", shipmentReference = "BL-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), purchaseOrderIds = new[] { orderId },
            importPermitNumber = "IP-1", healthCertificateNumber = "HC-1", customsDeclarationNumber = "CD-1",
        }, Json))).GetProperty("consignmentId").GetGuid();

        async Task<int> Version() => (await Read(await admin.GetAsync($"{Consignments}/{consignmentId}"))).GetProperty("version").GetInt32();

        foreach (var to in new[] { "InTransit", "AtCustoms", "Cleared" })
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"{Consignments}/{consignmentId}/status", new { to, on = DateOnly.FromDateTime(DateTime.UtcNow) }, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"{Consignments}/{consignmentId}/costs", new
        {
            expectedVersion = await Version(),
            costComponents = new[] { new { kind = "Freight", amount = freight, currencyCode = "BDT", note = (string?)null } },
            exchangeRates = Array.Empty<object>(),
        }, Json)).StatusCode);

        var receipt = await Read(await admin.PostAsJsonAsync("/api/staff/inventory/goods-receipts", new
        {
            purchaseOrderId = orderId, consignmentId,
            lines = new[]
            {
                new
                {
                    skuId, locationId, lotNumber = "LOT-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant(), expiresOn = expires, quantityAccepted = quantity, quantityRejected = 0m,
                    rejectionReason = (string?)null, purchaseOrderLineId = (Guid?)lineId, unitCostAmount = (decimal?)null, unitCostCurrency = (string?)null, holdForInspection = false, exchangeRateToBase = (decimal?)null,
                },
            },
        }, Json));
        var batchId = receipt.GetProperty("batchIds").EnumerateArray().Single().GetGuid();

        var allocation = await admin.PostAsJsonAsync($"{Consignments}/{consignmentId}/landed-cost", new { expectedVersion = await Version() }, Json);
        Assert.True(allocation.StatusCode == HttpStatusCode.OK, await allocation.Content.ReadAsStringAsync());
        return batchId;
    }

    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body, Json);
    }
}
