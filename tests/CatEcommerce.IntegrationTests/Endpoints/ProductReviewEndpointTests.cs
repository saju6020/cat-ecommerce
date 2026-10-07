using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SHP-001 §16 Phase D -- I-D1, I-D2, I-D3, I-D4, I-D6 (a representative subset).</summary>
public sealed class ProductReviewEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    [Fact]
    public async Task Submit_WithPurchase_PendingModeration_NotYetPublic()
    {
        var (controllableFactory, purchaseHistory) = Factory.WithControllablePurchaseHistory();
        var staffClient = await SignInAsShopManagerAsync();
        var (productId, skuId, slug) = await CreateAndPublishProductAsync(staffClient);

        var (customerClient, customerId) = await RegisterAndSignInCustomerAsync(controllableFactory);
        purchaseHistory.SetPurchased(customerId, skuId);

        var submit = await customerClient.PostAsJsonAsync("/api/customers/me/reviews", new
        { productId, skuId, rating = 5, title = "Great", body = "Loved it." }, Json);

        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("PendingModeration", body.GetProperty("status").GetString());

        var publicList = await customerClient.GetAsync($"/api/shop/products/{slug}/reviews");
        var publicBody = await publicList.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(0, publicBody.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Submit_WithoutPurchase_ReturnsReviewNotPermitted()
    {
        var (controllableFactory, _) = Factory.WithControllablePurchaseHistory();
        var staffClient = await SignInAsShopManagerAsync();
        var (productId, skuId, _) = await CreateAndPublishProductAsync(staffClient);

        var (customerClient, _) = await RegisterAndSignInCustomerAsync(controllableFactory);

        var submit = await customerClient.PostAsJsonAsync("/api/customers/me/reviews", new
        { productId, skuId, rating = 4, title = (string?)null, body = "Did not buy this." }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ReviewNotPermitted", body.GetProperty("errors")[0].GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Submit_UsingShippedDefaultReader_AlwaysReviewNotPermitted()
    {
        // No WithControllablePurchaseHistory() -- exercises the shipped NoPurchaseHistoryReader (I-D6).
        var staffClient = await SignInAsShopManagerAsync();
        var (productId, skuId, _) = await CreateAndPublishProductAsync(staffClient);
        var (customerClient, _) = await RegisterAndSignInCustomerAsync();

        var submit = await customerClient.PostAsJsonAsync("/api/customers/me/reviews", new
        { productId, skuId, rating = 4, title = (string?)null, body = "Testing the fail-closed default." }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, submit.StatusCode);
        var body = await submit.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("ReviewNotPermitted", body.GetProperty("errors")[0].GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Moderate_PublishThenReject_ReflectedInPublicListAndQueue()
    {
        var (controllableFactory, purchaseHistory) = Factory.WithControllablePurchaseHistory();
        var staffClient = await SignInAsShopManagerAsync();
        var (productId, skuId, slug) = await CreateAndPublishProductAsync(staffClient);

        var (customerClient, customerId) = await RegisterAndSignInCustomerAsync(controllableFactory);
        purchaseHistory.SetPurchased(customerId, skuId);

        var submit = await customerClient.PostAsJsonAsync("/api/customers/me/reviews", new
        { productId, skuId, rating = 5, title = "Great", body = "Loved it." }, Json);
        var submitBody = await submit.Content.ReadFromJsonAsync<JsonElement>(Json);
        var reviewId = submitBody.GetProperty("reviewId").GetGuid();

        var publish = await staffClient.PostAsync($"/api/staff/shop/reviews/{reviewId}/publish", null);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        var publicList = await customerClient.GetAsync($"/api/shop/products/{slug}/reviews");
        var publicBody = await publicList.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(1, publicBody.GetProperty("totalCount").GetInt32());
        Assert.True(publicBody.GetProperty("items")[0].GetProperty("verifiedPurchase").GetBoolean());

        // A second review by the same customer for the same product is refused.
        var second = await customerClient.PostAsJsonAsync("/api/customers/me/reviews", new
        { productId, skuId, rating = 3, title = (string?)null, body = "Trying again." }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("AlreadyReviewed", secondBody.GetProperty("errors")[0].GetProperty("errorCode").GetString());

        // Editing the published review returns it to PendingModeration, off the public list.
        var edit = await customerClient.PutAsJsonAsync($"/api/customers/me/reviews/{reviewId}", new
        { rating = 4, title = "Updated", body = "Still good, updated." }, Json);
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);

        var publicListAfterEdit = await customerClient.GetAsync($"/api/shop/products/{slug}/reviews");
        var publicBodyAfterEdit = await publicListAfterEdit.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(0, publicBodyAfterEdit.GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public async Task Moderate_AsReception_Forbidden()
    {
        var (_, mobile, password) = await SeedStaffAsync("Reception");
        var receptionClient = await SignInStaffAsync(mobile, password);

        var response = await receptionClient.PostAsync($"/api/staff/shop/reviews/{Guid.NewGuid()}/publish", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(Guid ProductId, Guid SkuId, string Slug)> CreateAndPublishProductAsync(HttpClient staffClient)
    {
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var slug = UniqueSlug("reviewed-product");

        var create = await staffClient.PostAsJsonAsync("/api/staff/shop/products", new
        { kind = "Standard", name = "Reviewed Product", brand = "Acme", category = "ToysAndFurniture", taxClassId, slug }, Json);
        var createBody = await create.Content.ReadFromJsonAsync<JsonElement>(Json);
        var productId = createBody.GetProperty("productId").GetGuid();

        var addSku = await staffClient.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
            new { expectedVersion = 1, code = UniqueCode("SKU") }, Json);
        var skuBody = await addSku.Content.ReadFromJsonAsync<JsonElement>(Json);
        var skuId = skuBody.GetProperty("skuId").GetGuid();

        await staffClient.PostAsJsonAsync($"/api/staff/shop/skus/{skuId}/prices",
            new { amount = 500.00m, currencyCode = "BDT", effectiveFrom = DateTimeOffset.UtcNow }, Json);

        var publish = await staffClient.PostAsJsonAsync($"/api/staff/shop/products/{productId}/publish", new { expectedVersion = 2 }, Json);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        return (productId, skuId, slug);
    }
}
