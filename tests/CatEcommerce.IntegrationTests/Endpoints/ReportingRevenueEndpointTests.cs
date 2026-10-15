using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-RPT-001 section 16 D -- I-D2, I-D3 and I-D4: shop revenue, cost and margin over a REAL order that is placed, packed and dispatched (so the stock ledger holds the sale), one line from a batch with a
/// landed cost and one from a batch without. The cost of goods is found through the order line id the ledger carries (ADR-284); revenue never uses the purchase cost as a fallback.
/// </summary>
public sealed class ReportingRevenueEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private static decimal Money(JsonElement cell) => decimal.Parse(cell.GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);

    private sealed record DispatchedShop(Shop Shop, Guid InvoiceId, decimal CostedNet, decimal UncostedNet, decimal DeliveryNet, string CostedName, string UncostedName);

    private async Task<JsonElement> ReportAsync(HttpClient client, string key, OrderingContext context, string? extra = null)
    {
        var response = await client.GetAsync($"/api/staff/reports/{key}?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today.AddDays(1):yyyy-MM-dd}&branchId={context.BranchId}&groupBy=Month" + (extra is null ? "" : "&" + extra));
        await ExpectAsync(HttpStatusCode.OK, response);
        return await ReadAsync(response);
    }

    /// <summary>One cash-on-delivery order of 2 x a costed food and 2 x an uncosted food, packed and dispatched. The costed food's batch has a landed cost of 10.00 a unit.</summary>
    private async Task<DispatchedShop> DispatchedOrderAsync()
    {
        var context = await CreateContextAsync();
        await SetBillingProfileAsync(context);

        var costed = await CreateSkuAsync(context, price: 500m, stock: 0m, food: true, name: "Costed food " + NewCode("N"));
        await ReportingLandedCostSeeder.SeedAsync(context.Admin, costed.SkuId, context.ShopLocationId, 20m, 5m, 100m, UtcToday.AddYears(1)); // landed unit cost (20 x 5 + 100) / 20 = 10
        await StockAtStorefrontAsync(costed.SkuId, 20m, UtcToday.AddYears(1));
        var plain = await CreateSkuAsync(context, price: 500m, stock: 20m, food: true, onStorefront: true, name: "Uncosted food " + NewCode("N"));

        var district = NewDistrict();
        var zoneId = await CreateZoneAsync(context, district, cod: true);
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var addressId = await AddAddressAsync(customer, district);
        await AcceptPendingPoliciesAsync(customer);
        await ExpectAsync(HttpStatusCode.Created, await customer.PostAsJsonAsync("/api/cart/lines", new { skuId = costed.SkuId, quantity = 2 }, Json));
        await ExpectAsync(HttpStatusCode.Created, await customer.PostAsJsonAsync("/api/cart/lines", new { skuId = plain.SkuId, quantity = 2 }, Json));

        var shop = new Shop(context, district, zoneId, customer, customerId, addressId, costed, plain);
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();

        var (reception, _) = await StaffClientAsync(context, StaffRoles.Reception);
        var courier = await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/admin/couriers",
            new { name = "Courier " + Guid.NewGuid().ToString("N")[..6], trackingUrlTemplate = "https://track.example.test/t/{tracking}", supportsCod = true, isActive = true }, Json));
        var courierId = (await ReadAsync(courier)).GetProperty("courierId").GetGuid();
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null));
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", new { courierId, trackingNumber = "TRK-RPT" }, Json));

        var invoiceId = (await LoadOrderAsync(orderId)).InvoiceId ?? throw new InvalidOperationException("the order has no invoice");
        var invoice = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/staff/billing/invoices/{invoiceId}")));
        var lines = invoice.GetProperty("lines").EnumerateArray().ToList();

        decimal NetOf(Func<JsonElement, bool> predicate) => lines.Where(predicate).Sum(l => Money(l.GetProperty("netAmount")));
        var costedNet = NetOf(l => l.GetProperty("description").GetString()!.Contains(costed.Name, StringComparison.Ordinal));
        var plainNet = NetOf(l => l.GetProperty("description").GetString()!.Contains(plain.Name, StringComparison.Ordinal));
        var deliveryNet = NetOf(l => l.GetProperty("itemKind").GetString() == "Fee");
        Assert.True(costedNet > 0m && plainNet > 0m, "the invoice should hold both food lines");

        return new DispatchedShop(shop, invoiceId, costedNet, plainNet, deliveryNet, costed.Name, plain.Name);
    }

    // ── I-D2 .. I-D3: the shop reports reconcile; margin only on the costed line, with the coverage stated ────
    [Fact]
    public async Task I_D2_I_D3_ShopRevenueReconcilesAcrossTheReports_AndMarginIsOnTheCostedLineOnly_WithTheCoverageStated()
    {
        var order = await DispatchedOrderAsync();
        var admin = order.Shop.Context.Admin;
        var shopNet = order.CostedNet + order.UncostedNet + order.DeliveryNet;

        var byLine = await ReportAsync(admin, ReportCatalogue.RevenueByBusinessLineKey, order.Shop.Context);
        var shopRow = byLine.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("businessLine").GetString() == "Shop");
        Assert.Equal(shopNet, Money(shopRow.GetProperty("netRevenue")));

        // I-D3: 2 x 10.00 of cost on the costed line; the uncosted line (and the delivery charge, which has no stock) have none.
        Assert.Equal(20m, Money(shopRow.GetProperty("cogs")));
        Assert.Equal(order.CostedNet - 20m, Money(shopRow.GetProperty("margin")));
        var expectedCoverage = decimal.Round(order.CostedNet * 100m / shopNet, 1, MidpointRounding.AwayFromZero);
        Assert.Equal(expectedCoverage, decimal.Parse(shopRow.GetProperty("coveragePercent").GetString()!, CultureInfo.InvariantCulture));
        Assert.Equal(decimal.Round((order.CostedNet - 20m) * 100m / order.CostedNet, 1, MidpointRounding.AwayFromZero), decimal.Parse(shopRow.GetProperty("marginPercent").GetString()!, CultureInfo.InvariantCulture));
        var note = byLine.GetProperty("notes").EnumerateArray().Single(n => n.GetProperty("code").GetString() == "MarginCoverage").GetProperty("text").GetString()!;
        Assert.StartsWith($"Margin covers {expectedCoverage.ToString("0.0", CultureInfo.InvariantCulture)} %", note);
        Assert.Equal(expectedCoverage, decimal.Parse(byLine.GetProperty("coverage").GetProperty("percent").GetString()!, CultureInfo.InvariantCulture));

        // I-D2: by category and by origin, the shop revenue is the same number.
        var byCategory = await ReportAsync(admin, ReportCatalogue.RevenueShopByCategoryKey, order.Shop.Context);
        var categories = byCategory.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(shopNet, categories.Sum(r => Money(r.GetProperty("netRevenue"))));
        Assert.Equal(order.CostedNet + order.UncostedNet, Money(categories.Single(r => r.GetProperty("category").GetString() == "CatFood").GetProperty("netRevenue")));
        Assert.Equal(order.DeliveryNet, Money(categories.Single(r => r.GetProperty("category").GetString() == "Delivery and fees").GetProperty("netRevenue")));
        Assert.Equal(shopNet, Money(byCategory.GetProperty("totals").GetProperty("netRevenue")));

        var byOrigin = await ReportAsync(admin, ReportCatalogue.RevenueShopByOriginKey, order.Shop.Context);
        var origins = byOrigin.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(order.CostedNet + order.UncostedNet, Money(origins.Single(r => r.GetProperty("origin").GetString() == "DE").GetProperty("netRevenue")));
        Assert.Equal(order.DeliveryNet, Money(origins.Single(r => r.GetProperty("origin").GetString() == "Not applicable").GetProperty("netRevenue")));
        Assert.Equal(shopNet, origins.Sum(r => Money(r.GetProperty("netRevenue"))));

        // The unit count is the quantity sold.
        Assert.Equal(4m, decimal.Parse(categories.Single(r => r.GetProperty("category").GetString() == "CatFood").GetProperty("units").GetString()!, CultureInfo.InvariantCulture));
    }

    // ── I-D4: ADR-281 -- a shop manager sees revenue, never cost or margin, in JSON, CSV or PDF ──────────────────
    [Fact]
    public async Task I_D4_AShopManager_SeesShopRevenueByCategory_WithNoCostMarginOrCoverage_InJsonCsvOrPdf_AndAnAdministratorDoes()
    {
        var order = await DispatchedOrderAsync();
        var context = order.Shop.Context;
        var (manager, _) = await StaffClientAsync(context, StaffRoles.ShopManager);
        string[] restricted = ["cogs", "margin", "marginPercent", "coveragePercent"];
        string[] labels = ["Cost of goods", "Margin", "Margin %", "Margin coverage %"];

        var asManager = await ReportAsync(manager, ReportCatalogue.RevenueShopByCategoryKey, context);
        Assert.DoesNotContain(asManager.GetProperty("columns").EnumerateArray(), c => restricted.Contains(c.GetProperty("key").GetString()));
        Assert.All(asManager.GetProperty("rows").EnumerateArray(), r => Assert.All(restricted, key => Assert.False(r.TryGetProperty(key, out _), key)));
        Assert.All(restricted, key => Assert.False(asManager.GetProperty("totals").TryGetProperty(key, out _), key));
        Assert.Equal(JsonValueKind.Null, asManager.GetProperty("coverage").ValueKind);
        Assert.DoesNotContain(asManager.GetProperty("notes").EnumerateArray(), n => n.GetProperty("code").GetString()!.StartsWith("Margin", StringComparison.Ordinal));

        var window = $"?from={Today.AddDays(-1):yyyy-MM-dd}&to={Today.AddDays(1):yyyy-MM-dd}&branchId={context.BranchId}&groupBy=Month";
        var csv = Encoding.UTF8.GetString(await (await ExpectAsync(HttpStatusCode.OK, await manager.GetAsync($"/api/staff/reports/{ReportCatalogue.RevenueShopByCategoryKey}/export.csv" + window))).Content.ReadAsByteArrayAsync());
        Assert.All(labels, label => Assert.DoesNotContain(label, csv));
        Assert.Contains("Net revenue", csv);

        var pdf = await (await ExpectAsync(HttpStatusCode.OK, await manager.GetAsync($"/api/staff/reports/{ReportCatalogue.RevenueShopByCategoryKey}/export.pdf" + window))).Content.ReadAsByteArrayAsync();
        var pdfText = PdfTextExtractor.Extract(pdf);
        Assert.All(labels, label => Assert.DoesNotContain(label, pdfText));
        Assert.DoesNotContain("landed cost", pdfText);
        Assert.Contains("Net revenue", pdfText);

        // The same report for an accountant has them all (owner decision 2026-10-13: the accountant holds Reports.Sales.View as well as Reports.Financial.View).
        var (accountantClient, _) = await StaffClientAsync(context, StaffRoles.Accountant);
        var asAccountant = await ReportAsync(accountantClient, ReportCatalogue.RevenueShopByCategoryKey, context);
        Assert.All(restricted, key => Assert.Contains(asAccountant.GetProperty("columns").EnumerateArray(), c => c.GetProperty("key").GetString() == key));

        // The business-line report is Financial: the shop manager cannot open it at all.
        var financial = await manager.GetAsync($"/api/staff/reports/{ReportCatalogue.RevenueByBusinessLineKey}" + window);
        Assert.Equal(HttpStatusCode.Forbidden, financial.StatusCode);
    }
}
