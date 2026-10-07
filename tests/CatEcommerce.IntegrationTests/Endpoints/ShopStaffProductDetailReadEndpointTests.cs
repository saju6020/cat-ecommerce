using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SHP-001-FU1 (backlog S1) -- the staff product detail returns what the staff forms
/// write: food labelling, images, health focus, SKU label. Tests FU1-T1, FU1-T1b and FU1-T2.</summary>
public sealed class ShopStaffProductDetailReadEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static byte[] MinimalPng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE
    ];

    private static async Task<Guid> UploadProductImageAsync(HttpClient staff)
    {
        using var form = new MultipartFormDataContent("test-boundary-" + Guid.NewGuid().ToString("N"));
        var part = new ByteArrayContent(MinimalPng());
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(part, "file", "pouch.png");
        var response = await staff.PostAsync("/api/staff/files?purpose=ProductImage", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("fileId").GetGuid();
    }

    private async Task<(HttpClient Staff, Guid ProductId, Guid TaxClassId, Guid SkuId, string SkuCode, Guid FileId)> SeedFullFoodProductAsync()
    {
        var staff = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var name = "Staff Read Food " + Guid.NewGuid().ToString("N")[..8];

        var create = await staff.PostAsJsonAsync("/api/staff/shop/products",
            new { kind = "Standard", name, brand = "Acme", category = "CatFood", taxClassId, slug = UniqueSlug("staff-read") }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var productId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid();

        var details = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        {
            expectedVersion = await ProductVersionAsync(staff, productId), name, brand = "Acme", description = "A food.",
            category = "CatFood", taxClassId, countryOfOriginCode = "DE", tags = new[] { "grain-free" },
            healthFocus = new[] { "Sterilised", "Urinary" }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, details.StatusCode);

        var skuCode = UniqueCode("SKU");
        var addSku = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus", new
        {
            expectedVersion = await ProductVersionAsync(staff, productId), code = skuCode,
            sizeOrWeight = "2 kg", flavour = "Chicken", colour = "Red", packQuantity = 3
        }, Json);
        Assert.Equal(HttpStatusCode.Created, addSku.StatusCode);
        var skuId = (await addSku.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();

        var food = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/food", new
        {
            expectedVersion = await ProductVersionAsync(staff, productId),
            manufacturer = "Royal Canin", lifeStage = "Kitten", proteinSource = "Chicken", ingredientList = "Chicken, rice",
            guaranteedAnalysis = new { proteinPercent = 32.0m, fatPercent = 15.0m, fibrePercent = 4.0m, moisturePercent = 10.0m },
            feedingGuide = "Feed twice daily.", storageInstructions = "Store in a cool, dry place."
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, food.StatusCode);

        var fileId = await UploadProductImageAsync(staff);
        var images = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/images", new
        {
            expectedVersion = await ProductVersionAsync(staff, productId),
            images = new object[] { new { fileId, altText = "Front of pack", sortOrder = 0, isPrimary = true } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, images.StatusCode);

        return (staff, productId, taxClassId, skuId, skuCode, fileId);
    }

    [Fact]
    public async Task FU1_T1_StaffDetail_ReturnsFoodImagesHealthFocusAndSkuLabelAndAttributes()
    {
        var (staff, productId, _, skuId, skuCode, fileId) = await SeedFullFoodProductAsync();

        var response = await staff.GetAsync($"/api/staff/shop/products/{productId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        var food = body.GetProperty("food");
        Assert.Equal("Royal Canin", food.GetProperty("manufacturer").GetString());
        Assert.Equal("Kitten", food.GetProperty("lifeStage").GetString());
        Assert.Equal("Chicken", food.GetProperty("proteinSource").GetString());
        Assert.Equal("Chicken, rice", food.GetProperty("ingredientList").GetString());
        Assert.Equal("Feed twice daily.", food.GetProperty("feedingGuide").GetString());
        Assert.Equal("Store in a cool, dry place.", food.GetProperty("storageInstructions").GetString());
        var analysis = food.GetProperty("guaranteedAnalysis");
        Assert.Equal(32.0m, analysis.GetProperty("proteinPercent").GetDecimal());
        Assert.Equal(15.0m, analysis.GetProperty("fatPercent").GetDecimal());
        Assert.Equal(4.0m, analysis.GetProperty("fibrePercent").GetDecimal());
        Assert.Equal(10.0m, analysis.GetProperty("moisturePercent").GetDecimal());

        var focus = body.GetProperty("healthFocus").EnumerateArray().Select(e => e.GetString()!).OrderBy(f => f).ToArray();
        Assert.Equal(new[] { "Sterilised", "Urinary" }, focus);

        var image = Assert.Single(body.GetProperty("images").EnumerateArray());
        Assert.Equal(fileId, image.GetProperty("fileId").GetGuid());
        Assert.Equal("Front of pack", image.GetProperty("altText").GetString());
        Assert.Equal(0, image.GetProperty("sortOrder").GetInt32());
        Assert.True(image.GetProperty("isPrimary").GetBoolean());
        Assert.Equal(JsonValueKind.Null, image.GetProperty("skuId").ValueKind);

        var sku = Assert.Single(body.GetProperty("skus").EnumerateArray());
        Assert.Equal(skuId, sku.GetProperty("skuId").GetGuid());
        Assert.Equal(skuCode, sku.GetProperty("code").GetString());
        Assert.Equal("2 kg", sku.GetProperty("sizeOrWeight").GetString());
        Assert.Equal("Chicken", sku.GetProperty("flavour").GetString());
        Assert.Equal("Red", sku.GetProperty("colour").GetString());
        Assert.Equal(3, sku.GetProperty("packQuantity").GetInt32());
        var label = sku.GetProperty("label").GetString()!;
        Assert.Contains("2 kg", label);
        Assert.Contains("Chicken", label);
    }

    [Fact]
    public async Task FU1_T1b_StaffDetail_OfAProductWithNoFoodDetailsOrImages_ReturnsNullFoodAndEmptyLists()
    {
        var staff = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var create = await staff.PostAsJsonAsync("/api/staff/shop/products",
            new { kind = "Standard", name = "Bare Toy", brand = "Acme", category = "CatFood", taxClassId, slug = UniqueSlug("bare") }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var productId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid();

        var body = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("food").ValueKind);
        Assert.Empty(body.GetProperty("images").EnumerateArray());
        Assert.Empty(body.GetProperty("healthFocus").EnumerateArray());
    }

    [Fact]
    public async Task FU1_T2_RoundTrip_ReadThenResaveDetailsWithReturnedHealthFocus_PreservesIt_AndFoodAndImagesResaveUnchanged()
    {
        var (staff, productId, taxClassId, _, _, fileId) = await SeedFullFoodProductAsync();

        var before = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        var returnedFocus = before.GetProperty("healthFocus").EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(2, returnedFocus.Length);

        // The form's Details save: everything it loaded, health focus included.
        var details = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        {
            expectedVersion = before.GetProperty("version").GetInt32(),
            name = before.GetProperty("name").GetString(), brand = before.GetProperty("brand").GetString(),
            description = before.GetProperty("description").GetString(), category = before.GetProperty("category").GetString(),
            taxClassId, countryOfOriginCode = before.GetProperty("countryOfOriginCode").GetString(),
            listedOnStorefront = before.GetProperty("listedOnStorefront").GetBoolean(),
            isExpiryTracked = before.GetProperty("isExpiryTracked").GetBoolean(),
            tags = before.GetProperty("tags").EnumerateArray().Select(t => t.GetString()!).ToArray(),
            healthFocus = returnedFocus
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, details.StatusCode);

        // The food form's save, from the returned food object.
        var food = (await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json)).GetProperty("food");
        var setFood = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/food", new
        {
            expectedVersion = await ProductVersionAsync(staff, productId),
            manufacturer = food.GetProperty("manufacturer").GetString(), lifeStage = food.GetProperty("lifeStage").GetString(),
            proteinSource = food.GetProperty("proteinSource").GetString(), ingredientList = food.GetProperty("ingredientList").GetString(),
            guaranteedAnalysis = food.GetProperty("guaranteedAnalysis"),
            feedingGuide = food.GetProperty("feedingGuide").GetString(), storageInstructions = food.GetProperty("storageInstructions").GetString()
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setFood.StatusCode);

        // The images editor's save, from the returned images.
        var current = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        var setImages = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/images", new
        {
            expectedVersion = current.GetProperty("version").GetInt32(),
            images = current.GetProperty("images").EnumerateArray().Select(i => new
            {
                fileId = i.GetProperty("fileId").GetGuid(), altText = i.GetProperty("altText").GetString(),
                sortOrder = i.GetProperty("sortOrder").GetInt32(), isPrimary = i.GetProperty("isPrimary").GetBoolean()
            }).ToArray()
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setImages.StatusCode);

        var after = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        Assert.Equal(returnedFocus.OrderBy(f => f), after.GetProperty("healthFocus").EnumerateArray().Select(e => e.GetString()!).OrderBy(f => f));
        Assert.Equal("Royal Canin", after.GetProperty("food").GetProperty("manufacturer").GetString());
        Assert.Equal(32.0m, after.GetProperty("food").GetProperty("guaranteedAnalysis").GetProperty("proteinPercent").GetDecimal());
        Assert.Equal(fileId, Assert.Single(after.GetProperty("images").EnumerateArray()).GetProperty("fileId").GetGuid());
    }
}
