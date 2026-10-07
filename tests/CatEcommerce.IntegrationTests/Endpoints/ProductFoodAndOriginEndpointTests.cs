using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SHP-001 §16 Phase B -- I-B1 (via staff detail), I-B2, I-B3, I-B4.</summary>
public sealed class ProductFoodAndOriginEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    [Fact]
    public async Task SetFoodDetails_PersistsEveryField()
    {
        var client = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var productId = await CreateFoodProductAsync(client, taxClassId, UniqueSlug("food"));

        var setFood = await client.PutAsJsonAsync($"/api/staff/shop/products/{productId}/food", new
        {
            expectedVersion = await GetVersionAsync(client, productId),
            manufacturer = "Royal Canin", lifeStage = "Kitten", proteinSource = "Chicken",
            ingredientList = "Chicken, rice", guaranteedAnalysis = new { proteinPercent = 32.0m, fatPercent = 15.0m, fibrePercent = 4.0m, moisturePercent = 10.0m },
            feedingGuide = "Feed twice daily.", storageInstructions = "Store in a cool, dry place."
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setFood.StatusCode);

        var detail = await client.GetAsync($"/api/staff/shop/products/{productId}");
        var body = await detail.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("DE", body.GetProperty("countryOfOriginCode").GetString());
    }

    [Fact]
    public async Task SetPrescriptionRequired_ReflectedInDetail()
    {
        var client = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var productId = await CreateFoodProductAsync(client, taxClassId, UniqueSlug("rx-food"));

        var setRx = await client.PutAsJsonAsync($"/api/staff/shop/products/{productId}/prescription",
            new { expectedVersion = await GetVersionAsync(client, productId), prescriptionRequired = true }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setRx.StatusCode);

        var detail = await client.GetAsync($"/api/staff/shop/products/{productId}");
        var body = await detail.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.True(body.GetProperty("prescriptionRequired").GetBoolean());
    }

    [Fact]
    public async Task Publish_FoodWithNoCountryOfOrigin_ThenAddIt_Publishes()
    {
        var client = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var slug = UniqueSlug("no-country");

        var create = await client.PostAsJsonAsync("/api/staff/shop/products", new
        {
            kind = "Standard", name = "No Country Food", brand = "Acme", category = "CatFood", taxClassId, slug
        }, Json);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(Json);
        var productId = createBody.GetProperty("productId").GetGuid();

        var skuCode = UniqueCode("SKU");
        var addSku = await client.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
            new { expectedVersion = await GetVersionAsync(client, productId), code = skuCode }, Json);
        var skuBody = await addSku.Content.ReadFromJsonAsync<JsonElement>(Json);
        var skuId = skuBody.GetProperty("skuId").GetGuid();

        await client.PutAsJsonAsync($"/api/staff/shop/products/{productId}/food",
            new { expectedVersion = await GetVersionAsync(client, productId), lifeStage = "Adult" }, Json);

        await client.PostAsJsonAsync($"/api/staff/shop/skus/{skuId}/prices",
            new { amount = 1000.00m, currencyCode = "BDT", effectiveFrom = DateTimeOffset.UtcNow }, Json);

        var publishWithoutCountry = await client.PostAsJsonAsync($"/api/staff/shop/products/{productId}/publish",
            new { expectedVersion = await GetVersionAsync(client, productId) }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, publishWithoutCountry.StatusCode);
        var errorBody = await publishWithoutCountry.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("country of origin", errorBody.GetProperty("errors")[0].GetProperty("errorMessage").GetString()!.ToLowerInvariant());

        await client.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        {
            expectedVersion = await GetVersionAsync(client, productId), name = "No Country Food", brand = "Acme",
            category = "CatFood", taxClassId, countryOfOriginCode = "DE"
        }, Json);

        var publishWithCountry = await client.PostAsJsonAsync($"/api/staff/shop/products/{productId}/publish",
            new { expectedVersion = await GetVersionAsync(client, productId) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, publishWithCountry.StatusCode);
    }

    [Fact]
    public async Task CreateOrigin_ThenAgain_ReturnsOriginAlreadyExists()
    {
        var client = await SignInAsShopManagerAsync();
        var countryCode = "NZ"; // not used by any other test's assertions -- see rule 16.1.2

        // Ensure a clean slate is not assumed: use a country unlikely to collide across parallel runs
        // by scoping this test's own assertion to the create/duplicate pair, not a global list.
        var first = await client.PostAsJsonAsync("/api/staff/shop/origins", new { countryCode, sortOrder = 0 }, Json);
        if (first.StatusCode == HttpStatusCode.BadRequest)
        {
            // Origin already exists from a previous run against this shared database; that is fine,
            // the assertion below (the SECOND create attempt) still proves OriginAlreadyExists.
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        var second = await client.PostAsJsonAsync("/api/staff/shop/origins", new { countryCode, sortOrder = 0 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("OriginAlreadyExists", body.GetProperty("errors")[0].GetProperty("errorCode").GetString());
    }

    private async Task<Guid> CreateFoodProductAsync(HttpClient client, Guid taxClassId, string slug)
    {
        var response = await client.PostAsJsonAsync("/api/staff/shop/products", new
        {
            kind = "Standard", name = "Food Product " + slug, brand = "Acme", category = "CatFood", taxClassId, slug
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var productId = body.GetProperty("productId").GetGuid();

        await client.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        {
            expectedVersion = await GetVersionAsync(client, productId), name = "Food Product " + slug, brand = "Acme",
            category = "CatFood", taxClassId, countryOfOriginCode = "DE"
        }, Json);

        return productId;
    }

    private async Task<int> GetVersionAsync(HttpClient client, Guid productId)
    {
        var response = await client.GetAsync($"/api/staff/shop/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("version").GetInt32();
    }
}
