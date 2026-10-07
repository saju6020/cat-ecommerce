using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Read.Queries.Product;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 SECURITY gate HIGH (2026-10-04) -- OQ-14 / ADR-085 / BR-2: a prescription diet must never
/// be sellable through a bundle. (a) flagging a product as prescription is refused while it is a
/// component of a non-archived bundle; (b) defence in depth: a bundle with a prescription component
/// is not eligible, not purchasable and out of stock on every read, whatever its own flag says
/// (legacy data, or a race past rule (a)).
/// </summary>
public sealed class ShopBundlePrescriptionEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static string NewBrand(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private async Task<(SeededProduct Food, SeededProduct Other, SeededProduct Bundle)> PublishedKitAsync(HttpClient staff, string name)
    {
        var brand = NewBrand("Kit");
        var food = await CreateProductAsync(staff, brand, "Kit Food", "CatFood", lifeStage: "Adult");
        var other = await CreateProductAsync(staff, brand, "Kit Toy");
        var bundle = await CreateBundleAsync(staff, brand, name, [(food.SkuId, 1), (other.SkuId, 1)]);
        return (food, other, bundle);
    }

    private async Task<HttpResponseMessage> FlagAsync(HttpClient staff, Guid productId, bool value) =>
        await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/prescription",
            new { expectedVersion = await ProductVersionAsync(staff, productId), prescriptionRequired = value }, Json);

    [Fact]
    public async Task Flagging_AComponentOfAPublishedBundle_AsPrescription_Is400_NamingTheBundle()
    {
        var staff = await SignInAsShopManagerAsync();
        var kitName = "Named Kit " + Guid.NewGuid().ToString("N")[..8];
        var (food, _, bundle) = await PublishedKitAsync(staff, kitName);

        var refused = await FlagAsync(staff, food.ProductId, true);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var body = await refused.Content.ReadAsStringAsync();
        Assert.Contains("PrescriptionProductInBundle", body);
        Assert.Contains(kitName, body);

        // Once the bundle is archived it no longer blocks the flag.
        var archive = await staff.PostAsJsonAsync($"/api/staff/shop/products/{bundle.ProductId}/archive",
            new { expectedVersion = await ProductVersionAsync(staff, bundle.ProductId) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await FlagAsync(staff, food.ProductId, true)).StatusCode);
    }

    [Fact]
    public async Task ABundleWithAPrescriptionComponent_ForcedPastTheRule_IsNotEligible_NotPurchasable_OutOfStock_AndSaleFactsSayPrescription_ThenUnflaggingRestoresIt()
    {
        var staff = await SignInAsShopManagerAsync();
        var (food, _, bundle) = await PublishedKitAsync(staff, "Forced Kit");
        var (customer, customerId) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId);

        // Before: the kit is sellable.
        var before = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/shop/products/{bundle.Slug}/purchasability"));
        Assert.True(before.GetProperty("skus")[0].GetProperty("purchasable").GetBoolean());

        // Simulate legacy data / a race that bypassed the aggregate rule.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE product.product SET prescription_required = true WHERE \"Id\" = {food.ProductId}");
        }

        var purchasability = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/shop/products/{bundle.Slug}/purchasability"));
        var sku = purchasability.GetProperty("skus")[0];
        Assert.False(sku.GetProperty("purchasable").GetBoolean());
        Assert.Equal("PrescriptionAuthorisationRequired", sku.GetProperty("purchaseBlockReason").GetString());
        Assert.Equal(0, sku.GetProperty("eligibleCats").GetArrayLength());

        var detail = await JsonOkAsync(await CreateClient().GetAsync($"/api/shop/products/{bundle.Slug}"));
        Assert.Equal("OutOfStock", detail.GetProperty("skus")[0].GetProperty("availability").GetString());

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
            var ctx = new UserContext(customerId, Guid.NewGuid());

            var eligibility = await dispatcher.GetResultAsync(new CheckPrescriptionEligibilityQuery
            { CustomerId = customerId, SkuId = bundle.SkuId, CatId = catId, UserContext = ctx, CorrelationId = Guid.NewGuid() });
            Assert.False(eligibility.Result!.Eligible);
            Assert.Equal("PrescriptionAuthorisationRequired", eligibility.Result.BlockReason);

            var facts = await dispatcher.GetResultAsync(new GetSkuSaleFactsQuery
            { SkuIds = [bundle.SkuId], AsOf = DateTimeOffset.UtcNow.AddMinutes(1), UserContext = ctx, CorrelationId = Guid.NewGuid() });
            Assert.True(Assert.Single(facts.Result).PrescriptionRequired);
        }

        // Unflagging works (turning it off is always allowed) and the kit is sellable again.
        Assert.Equal(HttpStatusCode.NoContent, (await FlagAsync(staff, food.ProductId, false)).StatusCode);
        var after = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/shop/products/{bundle.Slug}/purchasability"));
        Assert.True(after.GetProperty("skus")[0].GetProperty("purchasable").GetBoolean());
    }

    [Fact]
    public async Task FiltersCache_IsKeyedOnTheParsedCategory_JunkInputDoesNotGrowIt()
    {
        var cache = (MemoryCache)Factory.Services.GetRequiredService<IMemoryCache>();
        var client = CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/shop/filters")).StatusCode);
        var baseline = cache.Count;

        foreach (var junk in new[] { "junk1", "junk2", "%00", "CATFOOD-x", new string('z', 400) })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/shop/filters?category={Uri.EscapeDataString(junk)}")).StatusCode);

        Assert.Equal(baseline, cache.Count);
    }
}
