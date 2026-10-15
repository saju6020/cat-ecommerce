using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Read.Queries.Product;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 section 8.7 / I-C4 (HO-SHP-001-FU2) -- availability is Inventory's: a SKU Inventory has never stocked has no row in
/// <c>inventory.v_sellable_stock</c> and reads out of stock, however well priced and published it is, on every read that
/// shows availability. (Before HO-IMP-001 this was the fail-closed production default behind
/// <c>Shop:InterimAvailabilityFromPrice</c>; the flag is gone and "never stocked" is the same outcome by construction.)
/// </summary>
public sealed class ShopFailClosedAvailabilityEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static string NewBrand(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    [Fact]
    public async Task AnUnstockedPricedPublishedSku_IsOutOfStock_AndNotPurchasable_EverywhereAvailabilityShows()
    {
        var failClosed = Factory;
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Closed");
        var product = await CreateProductAsync(staff, brand, "Priced And Published Food", "CatFood", price: 1500m, lifeStage: "Adult", stock: 0m);
        var other = await CreateProductAsync(staff, brand, "Another Item", price: 200m, stock: 0m);
        var bundle = await CreateBundleAsync(staff, brand, "Closed Bundle", [(other.SkuId, 2)]);
        var (customer, _) = await RegisterAndSignInCustomerAsync(failClosed);

        // A stocked sibling reads in stock (a different brand, so the brand-scoped counts below are untouched) ...
        var stocked = await CreateProductAsync(staff, NewBrand("Open"), "Stocked Item", price: 200m);
        var open = await (await CreateClient().GetAsync($"/api/shop/products/{stocked.Slug}")).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("InStock", open.GetProperty("skus")[0].GetProperty("availability").GetString());

        // ... and the one Inventory never stocked reads out of stock.
        var anonymous = failClosed.CreateClient();
        var detail = await (await anonymous.GetAsync($"/api/shop/products/{product.Slug}")).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("OutOfStock", detail.GetProperty("skus")[0].GetProperty("availability").GetString());

        var bundleDetail = await (await anonymous.GetAsync($"/api/shop/products/{bundle.Slug}")).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("OutOfStock", bundleDetail.GetProperty("skus")[0].GetProperty("availability").GetString());
        Assert.All(bundleDetail.GetProperty("bundle").GetProperty("components").EnumerateArray(), c => Assert.Equal("OutOfStock", c.GetProperty("availability").GetString()));

        var list = await (await anonymous.GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}")).Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = list.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.Equal("OutOfStock", i.GetProperty("availability").GetString()));

        var inStockOnly = await (await anonymous.GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&availability=inStock")).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(0, inStockOnly.GetProperty("totalCount").GetInt32());

        // Purchasability: not purchasable, and the reason is OutOfStock.
        var purchasability = await (await customer.GetAsync($"/api/customers/me/shop/products/{product.Slug}/purchasability")).Content.ReadFromJsonAsync<JsonElement>(Json);
        var sku = purchasability.GetProperty("skus")[0];
        Assert.False(sku.GetProperty("purchasable").GetBoolean());
        Assert.Equal("OutOfStock", sku.GetProperty("purchaseBlockReason").GetString());

        // Wishlist and alerts: out of stock; an alert can therefore be requested (and is never fired).
        await customer.PutAsync($"/api/customers/me/wishlist/items/{product.SkuId}", null);
        var wishlist = await (await customer.GetAsync("/api/customers/me/wishlist")).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("OutOfStock", wishlist.GetProperty("items")[0].GetProperty("availability").GetString());

        var alert = await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json);
        Assert.Equal(HttpStatusCode.Created, alert.StatusCode);
        await RunShopCycleAsync(failClosed);
        var alerts = await (await customer.GetAsync("/api/customers/me/stock-alerts")).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Pending", alerts[0].GetProperty("status").GetString());

        // Recommendations (a life-stage block needs in-stock candidates): none of this brand's.
        var recommendations = await (await anonymous.GetAsync($"/api/shop/products/{product.Slug}/recommendations")).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.DoesNotContain(recommendations.GetProperty("lifeStage").GetProperty("items").EnumerateArray(),
            i => i.GetProperty("productId").GetGuid() == product.ProductId);

        // The staff back office still shows the catalogue (availability is not its concern).
        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/staff/shop/products/{product.ProductId}")).StatusCode);
    }

    [Fact]
    public async Task UnstockedFood_IsNeverRecommendedForACat()
    {
        var failClosed = Factory;
        var staff = await SignInAsShopManagerAsync();
        var unstocked = await CreateProductAsync(staff, NewBrand("ClosedRec"), "Adult Food", "CatFood", lifeStage: "Adult", stock: 0m);
        var (customer, customerId) = await RegisterAndSignInCustomerAsync(failClosed);
        var catId = await SeedCatAsync(customerId);

        var body = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations"));
        Assert.DoesNotContain(body.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("item").GetProperty("productId").GetGuid() == unstocked.ProductId);
    }
}
