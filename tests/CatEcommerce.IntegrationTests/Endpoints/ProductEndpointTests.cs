using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SHP-001 §16 Phase A -- I-A3, I-A7, I-A9, I-A10, I-A11, I-A13 (a representative
/// subset).</summary>
public sealed class ProductEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateProduct_AsShopManager_ReturnsCreatedAndEchoesFields()
    {
        var client = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var slug = UniqueSlug("scratch-post");

        var create = await client.PostAsJsonAsync("/api/staff/shop/products", new
        {
            kind = "Standard", name = "Scratch Post", brand = "Acme", category = "ToysAndFurniture",
            taxClassId, slug
        }, Json);

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var body = await create.Content.ReadFromJsonAsync<JsonElement>(Json);
        var productId = body.GetProperty("productId").GetGuid();

        var detail = await client.GetAsync($"/api/staff/shop/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailBody = await detail.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal("Scratch Post", detailBody.GetProperty("name").GetString());
        Assert.Equal("Draft", detailBody.GetProperty("status").GetString());
        Assert.Equal(slug, detailBody.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task AddSku_DuplicateBarcodeAcrossProducts_ReturnsBadRequest()
    {
        var client = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var barcode = "BC" + Guid.NewGuid().ToString("N")[..10];

        var productAId = await CreateProductAsync(client, taxClassId, UniqueSlug("product-a"));
        await AddSkuAsync(client, productAId, await GetVersionAsync(client, productAId), UniqueCode("SKU"), barcode);

        var productBId = await CreateProductAsync(client, taxClassId, UniqueSlug("product-b"));
        var addSkuB = await client.PostAsJsonAsync($"/api/staff/shop/products/{productBId}/skus", new
        {
            expectedVersion = await GetVersionAsync(client, productBId), code = UniqueCode("SKU"), barcode
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, addSkuB.StatusCode);
        var errorBody = await addSkuB.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("BarcodeAlreadyInUse", errorBody.GetProperty("errors")[0].GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Publish_UnpricedSku_ReturnsProductNotPublishableNamingSkuCode()
    {
        var client = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var productId = await CreateProductAsync(client, taxClassId, UniqueSlug("unpriced"));
        var skuCode = UniqueCode("SKU");
        await AddSkuAsync(client, productId, await GetVersionAsync(client, productId), skuCode, null);

        var publish = await client.PostAsJsonAsync($"/api/staff/shop/products/{productId}/publish",
            new { expectedVersion = await GetVersionAsync(client, productId) }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, publish.StatusCode);
        var body = await publish.Content.ReadFromJsonAsync<JsonElement>(Json);
        var message = body.GetProperty("errors")[0].GetProperty("errorMessage").GetString();
        Assert.Contains(skuCode, message);
    }

    [Fact]
    public async Task SchedulePriceThenPublish_Succeeds()
    {
        var client = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var productId = await CreateProductAsync(client, taxClassId, UniqueSlug("priced"));
        var skuId = await AddSkuAsync(client, productId, await GetVersionAsync(client, productId), UniqueCode("SKU"), null);

        var schedule = await client.PostAsJsonAsync($"/api/staff/shop/skus/{skuId}/prices", new
        {
            amount = 1500.00m, currencyCode = "BDT", effectiveFrom = DateTimeOffset.UtcNow
        }, Json);
        Assert.Equal(HttpStatusCode.Created, schedule.StatusCode);

        var publish = await client.PostAsJsonAsync($"/api/staff/shop/products/{productId}/publish",
            new { expectedVersion = await GetVersionAsync(client, productId) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        var history = await client.GetAsync($"/api/staff/shop/skus/{skuId}/prices");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var historyBody = await history.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("1500.00", historyBody.GetProperty("current").GetProperty("amount").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task UpdateDetails_WithStaleExpectedVersion_ReturnsConflict()
    {
        var client = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var productId = await CreateProductAsync(client, taxClassId, UniqueSlug("stale"));

        var update = await client.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        {
            expectedVersion = 999, name = "Renamed", brand = "Acme", category = "ToysAndFurniture", taxClassId
        }, Json);

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    [Fact]
    public async Task CreateProduct_AsReceptionOrAnonymous_IsRefused()
    {
        var (_, mobile, password) = await SeedStaffAsync("Reception");
        var receptionClient = await SignInStaffAsync(mobile, password);
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));

        var receptionAttempt = await receptionClient.PostAsJsonAsync("/api/staff/shop/products", new
        {
            kind = "Standard", name = "X", brand = "Y", category = "ToysAndFurniture", taxClassId, slug = UniqueSlug("x")
        }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, receptionAttempt.StatusCode);

        var anonymousClient = CreateClient();
        var anonymousAttempt = await anonymousClient.PostAsJsonAsync("/api/staff/shop/products", new
        {
            kind = "Standard", name = "X", brand = "Y", category = "ToysAndFurniture", taxClassId, slug = UniqueSlug("x")
        }, Json);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousAttempt.StatusCode);
    }

    [Fact]
    public async Task FindSkuByBarcode_KnownAndUnknown()
    {
        var client = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var productId = await CreateProductAsync(client, taxClassId, UniqueSlug("barcode-lookup"));
        var barcode = "BC" + Guid.NewGuid().ToString("N")[..10];
        await AddSkuAsync(client, productId, await GetVersionAsync(client, productId), UniqueCode("SKU"), barcode);

        var found = await client.GetAsync($"/api/staff/shop/skus?barcode={barcode}");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);

        var notFound = await client.GetAsync($"/api/staff/shop/skus?barcode=zzznotreal{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }

    private async Task<Guid> CreateProductAsync(HttpClient client, Guid taxClassId, string slug)
    {
        var response = await client.PostAsJsonAsync("/api/staff/shop/products", new
        {
            kind = "Standard", name = "Test Product " + slug, brand = "Acme", category = "ToysAndFurniture", taxClassId, slug
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("productId").GetGuid();
    }

    private async Task<int> GetVersionAsync(HttpClient client, Guid productId)
    {
        var response = await client.GetAsync($"/api/staff/shop/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("version").GetInt32();
    }

    private async Task<Guid> AddSkuAsync(HttpClient client, Guid productId, int expectedVersion, string code, string? barcode)
    {
        var response = await client.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus", new
        {
            expectedVersion, code, barcode
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("skuId").GetGuid();
    }
}
