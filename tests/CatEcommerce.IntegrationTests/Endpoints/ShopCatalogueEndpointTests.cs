using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SHP-001 §16 Phase E -- I-E1 (via the schema check below), I-E2, I-E3, I-E4, I-E5,
/// I-E6, I-E7, I-E8, I-E12 (a representative subset; I-E9 maintenance mode and I-E10 rate limit are
/// exercised elsewhere; I-E11 performance is a separate, opt-in fixture).</summary>
public sealed class ShopCatalogueEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    [Fact]
    public async Task Search_MisspelledBrandAndProductName_StillFound()
    {
        var client = await SignInAsShopManagerAsync();
        var brand = "Purrfecta" + Guid.NewGuid().ToString("N")[..8];
        await CreateActiveFoodProductAsync(client, brand, "Salmon Supreme " + Guid.NewGuid().ToString("N")[..6], "DE", ingredientList: "duck");

        var byBrand = await AnonymousGetAsync($"/api/shop/products?q={Uri.EscapeDataString(brand.ToLowerInvariant().Replace("purrfecta", "purfecta"))}");
        var byBrandBody = await byBrand.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(byBrandBody.GetProperty("totalCount").GetInt32() >= 1);
        Assert.Contains(byBrandBody.GetProperty("items").EnumerateArray(), i => i.GetProperty("brand").GetString() == brand);
    }

    [Fact]
    public async Task Search_ByIngredientAndTag_ReturnsMatch()
    {
        var client = await SignInAsShopManagerAsync();
        var brand = "IngredientBrand" + Guid.NewGuid().ToString("N")[..8];
        var (productId, slug) = await CreateActiveFoodProductAsync(client, brand, "Duck Dinner " + Guid.NewGuid().ToString("N")[..6], "DE", ingredientList: "duck and rice");

        // §16.1 rule 1: scoped to this test's own brand -- the shared catalogue holds thousands of "duck" products.
        var byIngredient = await AnonymousGetAsync($"/api/shop/products?q=duck&brand={Uri.EscapeDataString(brand)}");
        var body = await byIngredient.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains(body.GetProperty("items").EnumerateArray(), i => i.GetProperty("productId").GetGuid() == productId);
    }

    [Fact]
    public async Task Search_NoResults_ReturnsSuggestions()
    {
        var client = await SignInAsShopManagerAsync();
        var brand = "Zzsuggestbrand" + Guid.NewGuid().ToString("N")[..8];
        await CreateActiveFoodProductAsync(client, brand, "Test Product", "DE", ingredientList: "chicken");

        var zeroResult = await AnonymousGetAsync($"/api/shop/products?q=zzqx{Guid.NewGuid():N}");
        var zeroBody = await zeroResult.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(0, zeroBody.GetProperty("totalCount").GetInt32());
        Assert.True(zeroBody.GetProperty("suggestions").GetProperty("popular").GetArrayLength() > 0);

        // A near-miss of the brand still surfaces it as a didYouMean term. The query is scoped by a
        // brand filter that no product carries, so totalCount is 0 by construction (not by luck of
        // the trigram threshold) and the suggestions are always built.
        var nearMiss = brand.ToLowerInvariant()[..^1]; // drop the last character
        var nearMissResult = await AnonymousGetAsync($"/api/shop/products?q={nearMiss}&brand=Nobrand{Guid.NewGuid():N}");
        var nearMissBody = await nearMissResult.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(0, nearMissBody.GetProperty("totalCount").GetInt32());
        var didYouMean = nearMissBody.GetProperty("suggestions").GetProperty("didYouMean").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains(brand.ToLowerInvariant(), didYouMean);
    }

    [Fact]
    public async Task Filter_ByOriginCategoryLifeStageAndPriceRange_ReturnsExactMatches()
    {
        var client = await SignInAsShopManagerAsync();
        var brand = "FilterBrand" + Guid.NewGuid().ToString("N")[..8];
        var country = "FR";
        var (matchId, _) = await CreateActiveFoodProductAsync(client, brand, "Matching Kitten Food " + Guid.NewGuid().ToString("N")[..6], country, lifeStage: "Kitten", price: 1200m);
        var (nonMatchId, _) = await CreateActiveFoodProductAsync(client, brand, "Other Adult Food " + Guid.NewGuid().ToString("N")[..6], "DE", lifeStage: "Adult", price: 3000m);

        var response = await AnonymousGetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&origin={country}&lifeStage=Kitten&minPrice=1000&maxPrice=1500&category=CatFood");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        var ids = body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("productId").GetGuid()).ToList();
        Assert.Contains(matchId, ids);
        Assert.DoesNotContain(nonMatchId, ids);
    }

    [Fact]
    public async Task Sort_ByPriceAscendingAndDescending_OrdersCorrectly()
    {
        var client = await SignInAsShopManagerAsync();
        var brand = "SortBrand" + Guid.NewGuid().ToString("N")[..8];
        var (cheapId, _) = await CreateActiveFoodProductAsync(client, brand, "Cheap " + Guid.NewGuid().ToString("N")[..6], "DE", price: 500m);
        var (expensiveId, _) = await CreateActiveFoodProductAsync(client, brand, "Expensive " + Guid.NewGuid().ToString("N")[..6], "DE", price: 5000m);

        var asc = await AnonymousGetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&sort=price_asc");
        var ascBody = await asc.Content.ReadFromJsonAsync<JsonElement>(Json);
        var ascIds = ascBody.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("productId").GetGuid()).ToList();
        Assert.True(ascIds.IndexOf(cheapId) < ascIds.IndexOf(expensiveId));

        var desc = await AnonymousGetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&sort=price_desc");
        var descBody = await desc.Content.ReadFromJsonAsync<JsonElement>(Json);
        var descIds = descBody.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("productId").GetGuid()).ToList();
        Assert.True(descIds.IndexOf(expensiveId) < descIds.IndexOf(cheapId));

        Assert.Equal("PriceAscending", ascBody.GetProperty("appliedFilters").GetProperty("sort").GetString());
    }

    [Fact]
    public async Task ShopByCountry_ListsOriginWithNotesAndProductCount()
    {
        var client = await SignInAsShopManagerAsync();
        var country = "IT";
        await client.PostAsJsonAsync("/api/staff/shop/origins", new { countryCode = country, sourcingNotes = "Sourced from Italy.", sortOrder = 0 }, Json);

        var brand = "ItalyBrand" + Guid.NewGuid().ToString("N")[..8];
        await CreateActiveFoodProductAsync(client, brand, "Italian Food A " + Guid.NewGuid().ToString("N")[..6], country);
        await CreateActiveFoodProductAsync(client, brand, "Italian Food B " + Guid.NewGuid().ToString("N")[..6], country);

        var origins = await AnonymousGetAsync("/api/shop/origins");
        var body = await origins.Content.ReadFromJsonAsync<JsonElement>(Json);
        var match = body.EnumerateArray().FirstOrDefault(o => o.GetProperty("code").GetString() == country);

        Assert.True(match.ValueKind != JsonValueKind.Undefined);
        Assert.Equal("Sourced from Italy.", match.GetProperty("sourcingNotes").GetString());
        Assert.True(match.GetProperty("productCount").GetInt32() >= 2);
    }

    [Fact]
    public async Task Categories_ListsAllSixWithSubcategories()
    {
        var response = await AnonymousGetAsync("/api/shop/categories");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(6, body.GetArrayLength());
        var catFood = body.EnumerateArray().First(c => c.GetProperty("value").GetString() == "CatFood");
        Assert.True(catFood.GetProperty("subcategories").GetArrayLength() == 7);
    }

    [Fact]
    public async Task Detail_ReturnsFoodFieldsAndSkus()
    {
        var client = await SignInAsShopManagerAsync();
        var brand = "DetailBrand" + Guid.NewGuid().ToString("N")[..8];
        var (_, slug) = await CreateActiveFoodProductAsync(client, brand, "Detail Product " + Guid.NewGuid().ToString("N")[..6], "DE", ingredientList: "chicken, rice");

        var response = await AnonymousGetAsync($"/api/shop/products/{slug}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=60", response.Headers.CacheControl?.ToString());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("chicken, rice", body.GetProperty("food").GetProperty("ingredientList").GetString());
        Assert.True(body.GetProperty("skus").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Detail_DraftProduct_ReturnsNotFound()
    {
        var client = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var slug = UniqueSlug("draft-product");

        await client.PostAsJsonAsync("/api/staff/shop/products", new
        { kind = "Standard", name = "Draft Product", brand = "Acme", category = "ToysAndFurniture", taxClassId, slug }, Json);

        var response = await AnonymousGetAsync($"/api/shop/products/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var listResponse = await AnonymousGetAsync($"/api/shop/products?q={slug}");
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(0, listBody.GetProperty("totalCount").GetInt32());
    }

    private async Task<HttpResponseMessage> AnonymousGetAsync(string url) => await CreateClient().GetAsync(url);

    private async Task<(Guid ProductId, string Slug)> CreateActiveFoodProductAsync(
        HttpClient staffClient, string brand, string name, string countryCode, string? lifeStage = "Adult", string? ingredientList = null, decimal price = 1000m)
    {
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var slug = UniqueSlug(name.ToLowerInvariant().Replace(" ", "-"));

        var create = await staffClient.PostAsJsonAsync("/api/staff/shop/products", new
        { kind = "Standard", name, brand, category = "CatFood", taxClassId, slug }, Json);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(Json);
        var productId = createBody.GetProperty("productId").GetGuid();

        await staffClient.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        { expectedVersion = 1, name, brand, category = "CatFood", taxClassId, countryOfOriginCode = countryCode }, Json);

        await staffClient.PutAsJsonAsync($"/api/staff/shop/products/{productId}/food", new
        { expectedVersion = 2, lifeStage, ingredientList }, Json);

        var addSku = await staffClient.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
            new { expectedVersion = 3, code = UniqueCode("SKU") }, Json);
        var skuBody = await addSku.Content.ReadFromJsonAsync<JsonElement>(Json);
        var skuId = skuBody.GetProperty("skuId").GetGuid();

        await staffClient.PostAsJsonAsync($"/api/staff/shop/skus/{skuId}/prices",
            new { amount = price, currencyCode = "BDT", effectiveFrom = DateTimeOffset.UtcNow }, Json);

        var publish = await staffClient.PostAsJsonAsync($"/api/staff/shop/products/{productId}/publish", new { expectedVersion = 4 }, Json);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        return (productId, slug);
    }
}
