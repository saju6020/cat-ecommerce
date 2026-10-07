using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 CODE_REVIEW gate fixes (2026-10-04): duplicate / case-variant allergens no longer crash
/// the food recommendation (U-H2 through HTTP), an unknown ingredient list or a bundle with an unsafe or
/// unknown component is excluded for an allergic cat, and the price filter is section 12.4 rule (ANY
/// active SKU CURRENT price within the bounds; no current price never matches a bound).
/// </summary>
public sealed class ShopCodeReviewFixesEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static string NewBrand(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static List<Guid> Ids(JsonElement body) =>
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("item").GetProperty("productId").GetGuid()).ToList();

    private async Task<(HttpClient Customer, Guid CatId)> AllergicCatAsync(params string[] allergens)
    {
        var (customer, customerId) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Allergic", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3));
        await RecordCatFactsAsync(catId, allergens: allergens, conditions: ["Gingivitis problems"], neutered: true);
        return (customer, catId);
    }

    [Fact]
    public async Task U_H2_ACatWithDuplicateAndCaseVariantAllergens_GetsRecommendations_Not500()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("DupAllergy");
        var units = NextUnitsSold();
        string[] focus = ["Sterilised", "Dental"];
        var safe = await CreateProductAsync(staff, brand, "Salmon Dinner", "CatFood", lifeStage: "Adult", ingredientList: "salmon, rice", healthFocus: focus);
        var poultry = await CreateProductAsync(staff, brand, "Poultry Dinner", "CatFood", lifeStage: "Adult", ingredientList: "chicken meal, rice", healthFocus: focus);
        var unlabelled = await CreateProductAsync(staff, brand, "No Label Dinner", "CatFood", lifeStage: "Adult", ingredientList: null, healthFocus: focus);
        foreach (var p in new[] { safe, poultry, unlabelled }) await PushUnitsSoldAsync(p.ProductId, units);

        var (customer, catId) = await AllergicCatAsync("Chicken", "chicken", " CHICKEN ");

        var body = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations"));
        var ids = Ids(body);

        Assert.Contains(safe.ProductId, ids);
        Assert.DoesNotContain(poultry.ProductId, ids);
        Assert.DoesNotContain(unlabelled.ProductId, ids); // unknown ingredients: fail-closed for an allergic cat
        Assert.Contains("IngredientsUnknownExcluded", body.GetProperty("hints").EnumerateArray().Select(h => h.GetString()));
        var exclusion = Assert.Single(body.GetProperty("excludedForAllergy").EnumerateArray());
        Assert.Equal("chicken", exclusion.GetProperty("allergen").GetString(), ignoreCase: true);
    }

    [Fact]
    public async Task AnUnlabelledFood_IsStillRecommended_ToACatWithNoRecordedAllergy()
    {
        var staff = await SignInAsShopManagerAsync();
        var unlabelled = await CreateProductAsync(staff, NewBrand("NoAllergy"), "Unlabelled", "CatFood", lifeStage: "Adult", ingredientList: null, healthFocus: ["Sterilised", "Dental"]);
        await PushUnitsSoldAsync(unlabelled.ProductId, NextUnitsSold());

        var (customer, customerId) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Healthy", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3));
        await RecordCatFactsAsync(catId, conditions: ["Gingivitis problems"], neutered: true);

        var body = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations"));
        Assert.Contains(unlabelled.ProductId, Ids(body));
    }

    private async Task<SeededProduct> FoodBundleAsync(HttpClient staff, string brand, string name, params SeededProduct[] components)
    {
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var skuCode = UniqueCode("FKIT");
        var create = await staff.PostAsJsonAsync("/api/staff/shop/products", new
        { kind = "Bundle", name, brand, category = "CatFood", taxClassId, slug = UniqueSlug("food-kit"), bundleSkuCode = skuCode }, Json);
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var productId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid();
        var skuId = await StaffSkuIdAsync(staff, productId);

        Assert.Equal(HttpStatusCode.NoContent, (await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        { expectedVersion = await ProductVersionAsync(staff, productId), name, brand, category = "CatFood", taxClassId, countryOfOriginCode = "DE", healthFocus = new[] { "Sterilised", "Dental" } }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/food",
            new { expectedVersion = await ProductVersionAsync(staff, productId), lifeStage = "Adult" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/bundle-components", new
        {
            expectedVersion = await ProductVersionAsync(staff, productId),
            components = components.Select(c => new { componentSkuId = c.SkuId, quantity = 1 }).ToArray()
        }, Json)).StatusCode);
        await SchedulePriceAsync(staff, skuId, 2000m);
        await PublishAsync(staff, productId);
        return new SeededProduct(productId, "", skuId, skuCode);
    }

    [Fact]
    public async Task AFoodBundle_IsCheckedOverItsComponents_UnsafeOrUnknownIsExcluded_SafeIsKept()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Kit");
        var units = NextUnitsSold();
        var salmon = await CreateProductAsync(staff, brand, "Salmon", "CatFood", lifeStage: "Adult", ingredientList: "salmon, rice");
        var tuna = await CreateProductAsync(staff, brand, "Tuna", "CatFood", lifeStage: "Adult", ingredientList: "tuna, rice");
        var chicken = await CreateProductAsync(staff, brand, "Chicken", "CatFood", lifeStage: "Adult", ingredientList: "Chicken meal, rice");
        var unlabelled = await CreateProductAsync(staff, brand, "Mystery", "CatFood", lifeStage: "Adult", ingredientList: null);

        var safeKit = await FoodBundleAsync(staff, brand, "Safe Kit", salmon, tuna);
        var unsafeKit = await FoodBundleAsync(staff, brand, "Unsafe Kit", salmon, chicken);
        var unknownKit = await FoodBundleAsync(staff, brand, "Unknown Kit", salmon, unlabelled);
        foreach (var kit in new[] { safeKit, unsafeKit, unknownKit }) await PushUnitsSoldAsync(kit.ProductId, units);

        var (customer, catId) = await AllergicCatAsync("chicken");
        var ids = Ids(await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations")));

        Assert.Contains(safeKit.ProductId, ids);
        Assert.DoesNotContain(unsafeKit.ProductId, ids);
        Assert.DoesNotContain(unknownKit.ProductId, ids);
    }

    [Fact]
    public async Task PriceFilter_MatchesAnySkuCurrentPriceInRange_NotARangeOverlap_AndNoCurrentPriceNeverMatches()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("PriceBand");
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));

        async Task<Guid> TwoSkuProductAsync(string name, decimal first, decimal second)
        {
            var create = await staff.PostAsJsonAsync("/api/staff/shop/products", new { kind = "Standard", name, brand, category = "ToysAndFurniture", taxClassId, slug = UniqueSlug("band") }, Json);
            var productId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid();
            foreach (var (size, price) in new[] { ("S", first), ("L", second) })
            {
                var sku = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
                    new { expectedVersion = await ProductVersionAsync(staff, productId), code = UniqueCode("BAND"), sizeOrWeight = size }, Json);
                var skuId = (await sku.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();
                await SchedulePriceAsync(staff, skuId, price);
            }

            await PublishAsync(staff, productId);
            return productId;
        }

        var spread = await TwoSkuProductAsync("Spread 500 and 5000", 500m, 5000m);   // overlaps 1000-1500, no SKU inside it
        var inside = await TwoSkuProductAsync("Spread 500 and 1200", 500m, 1200m);   // one SKU at 1200
        var unpriced = await CreateProductAsync(staff, brand, "Unpriced Now", price: 1200m);
        await SetSkuOutOfStockAsync(unpriced.SkuId); // moves its only price into the future: no current price

        var anonymous = CreateClient();
        async Task<List<Guid>> SearchAsync(string query)
        {
            var body = await JsonOkAsync(await anonymous.GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&{query}"));
            return body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("productId").GetGuid()).ToList();
        }

        var band = await SearchAsync("minPrice=1000&maxPrice=1500");
        Assert.Contains(inside, band);
        Assert.DoesNotContain(spread, band);
        Assert.DoesNotContain(unpriced.ProductId, band);

        Assert.Contains(spread, await SearchAsync("minPrice=4500"));      // the 5000 SKU
        Assert.DoesNotContain(inside, await SearchAsync("minPrice=4500"));
        Assert.Contains(spread, await SearchAsync("maxPrice=600"));       // the 500 SKU
        Assert.DoesNotContain(unpriced.ProductId, await SearchAsync("maxPrice=100000"));

        // With no bound the unpriced product is still listed (the price filter is the only thing excluding it).
        Assert.Contains(unpriced.ProductId, await SearchAsync("q=unpriced"));
    }

    [Fact]
    public async Task PriceFilter_ParsesInvariantCulture_AndRejectsGarbage()
    {
        var client = CreateClient();
        var body = await JsonOkAsync(await client.GetAsync("/api/shop/products?minPrice=10.5&maxPrice=20.25"));
        Assert.Equal("10.50", body.GetProperty("appliedFilters").GetProperty("minPrice").GetString());
        Assert.Equal("20.25", body.GetProperty("appliedFilters").GetProperty("maxPrice").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/shop/products?minPrice=ten")).StatusCode);
    }
}
