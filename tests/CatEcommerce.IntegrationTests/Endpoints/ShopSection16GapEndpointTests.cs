using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 section 16 cases that had no implementing test at the TEST gate (2026-10-04). Each
/// test is named for its section 16 id. Every test mints its own brand and asserts only over that
/// brand (section 16.1 rule 1) so the shared database and the 5,000-product performance fixture can
/// neither satisfy nor break an assertion.
/// Section 18: A1 (U-A5b), A4 (I-A6, I-A8), A6 (U-B2 over HTTP), A9 (U-E3, I-E2), A10 (I-E3).
/// </summary>
public sealed class ShopSection16GapEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static string NewBrand(string prefix) => $"{prefix}{Guid.NewGuid():N}"[..(prefix.Length + 10)];

    private static string Token() => Guid.NewGuid().ToString("N")[..8];

    private async Task<HttpResponseMessage> AnonymousGetAsync(string url) => await CreateClient().GetAsync(url);

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(Json);

    private static string FirstErrorCode(JsonElement body) => body.GetProperty("errors")[0].GetProperty("errorCode").GetString()!;

    private static string FirstErrorProperty(JsonElement body) => body.GetProperty("errors")[0].GetProperty("propertyName").GetString()!;

    private static HashSet<Guid> ItemIds(JsonElement body) =>
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("productId").GetGuid()).ToHashSet();

    private static List<Guid> OrderedItemIds(JsonElement body) =>
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("productId").GetGuid()).ToList();

    // ── Uploads (U-A5b, I-A6) ───────────────────────────────────────────────────────────────────

    private static byte[] MinimalPng() => TestImages.Png();

    private static MultipartFormDataContent Upload(byte[] content, string fileName, string contentType)
    {
        var form = new MultipartFormDataContent("test-boundary-" + Guid.NewGuid().ToString("N"));
        var part = new ByteArrayContent(content);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(part, "file", fileName);
        return form;
    }

    private static async Task<Guid> UploadProductImageAsync(HttpClient staff)
    {
        using var form = Upload(MinimalPng(), "pouch.png", "image/png");
        var response = await staff.PostAsync("/api/staff/files?purpose=ProductImage", form);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await BodyAsync(response)).GetProperty("fileId").GetGuid();
    }

    [Fact]
    public async Task U_A5b_GifProductImage_IsRefusedAtUpload_AndAFileThatWasNeverStoredCannotBecomeAProductImage()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("GifImg"), "Gif Test Pouch", publish: false);

        // 1. image/gif is not an allowed ProductImage type: the upload itself is refused, nothing stored.
        using var gifForm = Upload("GIF89a"u8.ToArray(), "dancing.gif", "image/gif");
        var gifUpload = await staff.PostAsync("/api/staff/files?purpose=ProductImage", gifForm);
        Assert.Equal(HttpStatusCode.BadRequest, gifUpload.StatusCode);
        var gifBody = await BodyAsync(gifUpload);
        Assert.Equal("FileTypeNotAllowed", FirstErrorCode(gifBody));
        Assert.False(gifBody.TryGetProperty("fileId", out _));

        // 2. The refusal leaves no file id to attach: any id that was not minted by an accepted upload
        //    is InvalidImage at the product (the handler asks the StoredFile for attach eligibility).
        var attachGhost = await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/images", new
        {
            expectedVersion = await ProductVersionAsync(staff, product.ProductId),
            images = new[] { new { fileId = Guid.NewGuid(), altText = "A gif", sortOrder = 0, isPrimary = true } }
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, attachGhost.StatusCode);
        Assert.Equal("InvalidImage", FirstErrorCode(await BodyAsync(attachGhost)));

        // 3. Positive control in the same product: a real png is accepted and attaches.
        var pngFileId = await UploadProductImageAsync(staff);
        var attachPng = await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/images", new
        {
            expectedVersion = await ProductVersionAsync(staff, product.ProductId),
            images = new[] { new { fileId = pngFileId, altText = "A pouch", sortOrder = 0, isPrimary = true } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, attachPng.StatusCode);
    }

    [Fact]
    public async Task U_A5b_ProductImageUpload_IsRefusedForAGifEvenWhenThePartIsLabelledAsPngOnlyByExtension()
    {
        // The declared content type decides, not the file name: image/gif with a .png name is still refused.
        var staff = await SignInAsShopManagerAsync();
        using var form = Upload("GIF89a"u8.ToArray(), "looks-like.png", "image/gif");

        var response = await staff.PostAsync("/api/staff/files?purpose=ProductImage", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("FileTypeNotAllowed", FirstErrorCode(await BodyAsync(response)));
    }

    // ── U-B2 over HTTP ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task U_B2_GuaranteedAnalysis_OverHttp_AcceptedSumBoundAndAllOrNone()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Analysis"), "Analysis Food", category: "CatFood", countryCode: "DE", lifeStage: "Adult");
        var url = $"/api/staff/shop/products/{product.ProductId}/food";

        async Task<HttpResponseMessage> PutAsync(object analysis) => await staff.PutAsJsonAsync(url, new
        { expectedVersion = await ProductVersionAsync(staff, product.ProductId), lifeStage = "Adult", guaranteedAnalysis = analysis }, Json);

        async Task<JsonElement> StoredAnalysisAsync() =>
            (await BodyAsync(await AnonymousGetAsync($"/api/shop/products/{product.Slug}"))).GetProperty("food").GetProperty("guaranteedAnalysis");

        // 32/15/4/10 accepted and round-trips through the public detail.
        Assert.Equal(HttpStatusCode.NoContent, (await PutAsync(new { proteinPercent = 32, fatPercent = 15, fibrePercent = 4, moisturePercent = 10 })).StatusCode);
        var stored = await StoredAnalysisAsync();
        Assert.Equal(32m, stored.GetProperty("proteinPercent").GetDecimal());
        Assert.Equal(10m, stored.GetProperty("moisturePercent").GetDecimal());

        // 60/30/10/5 sums to 105.
        var sum = await PutAsync(new { proteinPercent = 60, fatPercent = 30, fibrePercent = 10, moisturePercent = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, sum.StatusCode);
        Assert.Equal("InvalidGuaranteedAnalysis", FirstErrorCode(await BodyAsync(sum)));

        // 101/0/0/0 is out of the 0-100 bound.
        var bound = await PutAsync(new { proteinPercent = 101, fatPercent = 0, fibrePercent = 0, moisturePercent = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, bound.StatusCode);
        Assert.Equal("InvalidGuaranteedAnalysis", FirstErrorCode(await BodyAsync(bound)));

        // Three of four is refused outright (all four or none): the request contract requires every value.
        var threeOfFour = await PutAsync(new { proteinPercent = 20, fatPercent = 10, fibrePercent = 5 });
        Assert.Equal(HttpStatusCode.BadRequest, threeOfFour.StatusCode);

        // None of the refusals touched the stored analysis.
        var after = await StoredAnalysisAsync();
        Assert.Equal(32m, after.GetProperty("proteinPercent").GetDecimal());
        Assert.Equal(15m, after.GetProperty("fatPercent").GetDecimal());
    }

    // ── U-E3 ────────────────────────────────────────────────────────────────────────────────────

    public static TheoryData<string, string> InvalidFilters() => new()
    {
        { "minPrice=500&maxPrice=100", "minPrice" },
        { "minPrice=abc", "minPrice" },
        { "maxPrice=abc", "maxPrice" },
        { "lifeStage=Puppy", "lifeStage" },
        { "protein=Wombat", "protein" },
        { "minRating=6", "minRating" },
        { "minRating=0", "minRating" },
        { "pageSize=1&pageNumber=2000000000", "pageNumber" },
    };

    [Theory]
    [MemberData(nameof(InvalidFilters))]
    public async Task U_E3_InvalidFilterInput_IsInvalidCatalogueFilter_NamingTheParameter(string queryString, string parameter)
    {
        var response = await AnonymousGetAsync($"/api/shop/products?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal("InvalidCatalogueFilter", FirstErrorCode(body));
        Assert.Equal(parameter, FirstErrorProperty(body));
    }

    [Fact]
    public async Task U_E3_TwentyOneBrands_IsInvalidCatalogueFilter_AndTwentyIsAccepted()
    {
        string Brands(int count) => string.Join("&", Enumerable.Range(0, count).Select(i => $"brand=b{i}-{Token()}"));

        var tooMany = await AnonymousGetAsync($"/api/shop/products?{Brands(21)}");
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
        var body = await BodyAsync(tooMany);
        Assert.Equal("InvalidCatalogueFilter", FirstErrorCode(body));
        Assert.Equal("brand", FirstErrorProperty(body));

        var atLimit = await AnonymousGetAsync($"/api/shop/products?{Brands(20)}");
        Assert.Equal(HttpStatusCode.OK, atLimit.StatusCode);
        Assert.Equal(0, (await BodyAsync(atLimit)).GetProperty("totalCount").GetInt32());
    }

    [Theory]
    [InlineData("origin")]
    [InlineData("lifeStage")]
    [InlineData("protein")]
    public async Task U_E3_TwentyOneValuesOfAMultiValuedFilter_IsInvalidCatalogueFilter(string parameter)
    {
        var values = string.Join("&", Enumerable.Range(0, 21).Select(i => $"{parameter}=Adult"));

        var response = await AnonymousGetAsync($"/api/shop/products?{values}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal("InvalidCatalogueFilter", FirstErrorCode(body));
        Assert.Equal(parameter, FirstErrorProperty(body));
    }

    [Fact]
    public async Task U_E3_QueryOverOneHundredCharacters_IsInvalidCatalogueFilter()
    {
        var response = await AnonymousGetAsync($"/api/shop/products?q={new string('x', 101)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal("InvalidCatalogueFilter", FirstErrorCode(body));
        Assert.Equal("q", FirstErrorProperty(body));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public async Task U_E3_MinRatingOnTheBoundary_IsAccepted(int minRating)
    {
        var response = await AnonymousGetAsync($"/api/shop/products?minRating={minRating}&brand=nobrand-{Token()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── I-A6 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_A6_TwoSkus_EachWithItsOwnCodeBarcodePriceAndImages_BarcodesNeverPublic()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("TwoSku");
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var slug = UniqueSlug("two-sku");

        var create = await staff.PostAsJsonAsync("/api/staff/shop/products",
            new { kind = "Standard", name = "Two Sku Litter " + Token(), brand, category = "HealthAndGrooming", taxClassId, slug }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var productId = (await BodyAsync(create)).GetProperty("productId").GetGuid();

        async Task<(Guid SkuId, string Code, string Barcode)> AddSkuAsync(string size, decimal price)
        {
            var code = UniqueCode("SKU");
            var barcode = "BC" + Guid.NewGuid().ToString("N")[..12];
            var response = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
                new { expectedVersion = await ProductVersionAsync(staff, productId), code, barcode, sizeOrWeight = size }, Json);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var skuId = (await BodyAsync(response)).GetProperty("skuId").GetGuid();
            await SchedulePriceAsync(staff, skuId, price);
            return (skuId, code, barcode);
        }

        var small = await AddSkuAsync("1 kg", 1000m);
        var large = await AddSkuAsync("5 kg", 4200m);

        var general = await UploadProductImageAsync(staff);
        var smallFile = await UploadProductImageAsync(staff);
        var largeFile = await UploadProductImageAsync(staff);
        var setImages = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/images", new
        {
            expectedVersion = await ProductVersionAsync(staff, productId),
            images = new object[]
            {
                new { fileId = general, altText = "General pack shot", sortOrder = 0, isPrimary = true },
                new { fileId = smallFile, skuId = small.SkuId, altText = "One kilo bag", sortOrder = 1, isPrimary = false },
                new { fileId = largeFile, skuId = large.SkuId, altText = "Five kilo bag", sortOrder = 2, isPrimary = false }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setImages.StatusCode);

        await PublishAsync(staff, productId);

        var publicResponse = await AnonymousGetAsync($"/api/shop/products/{slug}");
        Assert.Equal(HttpStatusCode.OK, publicResponse.StatusCode);
        var raw = await publicResponse.Content.ReadAsStringAsync();
        var detail = JsonDocument.Parse(raw).RootElement;

        var skus = detail.GetProperty("skus").EnumerateArray().ToList();
        Assert.Equal(2, skus.Count);

        var smallPublic = skus.Single(s => s.GetProperty("code").GetString() == small.Code);
        var largePublic = skus.Single(s => s.GetProperty("code").GetString() == large.Code);

        // Each SKU carries its own price...
        Assert.Equal(1000m, decimal.Parse(smallPublic.GetProperty("price").GetProperty("amount").GetString()!, CultureInfo.InvariantCulture));
        Assert.Equal(4200m, decimal.Parse(largePublic.GetProperty("price").GetProperty("amount").GetString()!, CultureInfo.InvariantCulture));
        Assert.Equal("1 kg", smallPublic.GetProperty("sizeOrWeight").GetString());
        Assert.Equal("5 kg", largePublic.GetProperty("sizeOrWeight").GetString());

        // ...and its own images, never the other SKU's.
        Assert.Equal(["One kilo bag"], smallPublic.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("altText").GetString()!).ToArray());
        Assert.Equal(["Five kilo bag"], largePublic.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("altText").GetString()!).ToArray());
        Assert.Equal(["General pack shot"], detail.GetProperty("images").EnumerateArray().Select(i => i.GetProperty("altText").GetString()!).ToArray());

        // Barcodes are not exposed on the public detail, and nothing public is even named "barcode".
        Assert.DoesNotContain(small.Barcode, raw);
        Assert.DoesNotContain(large.Barcode, raw);
        Assert.DoesNotContain("barcode", raw, StringComparison.OrdinalIgnoreCase);

        // Nor in the public listing...
        var listing = await (await AnonymousGetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(small.Barcode, listing);
        Assert.DoesNotContain(large.Barcode, listing);

        // ...while the staff view (control) does carry them, per SKU.
        var staffDetail = await BodyAsync(await staff.GetAsync($"/api/staff/shop/products/{productId}"));
        var staffBarcodes = staffDetail.GetProperty("skus").EnumerateArray().Select(s => s.GetProperty("barcode").GetString()).ToHashSet();
        Assert.Contains(small.Barcode, staffBarcodes);
        Assert.Contains(large.Barcode, staffBarcodes);
    }

    // ── I-A8 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_A8_PriceLifecycle_ScheduledThenCurrentThenPast_OverHttp_WithTheFakeClock()
    {
        var baseStaff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(baseStaff, NewBrand("Lifecycle"), "Lifecycle Pouch", price: 1000m);

        // The host's clock is a FakeTimeProvider starting now. It is built after the first price was
        // scheduled (effective at real "now"), so that price is current on the fake clock too.
        var fake = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var clocked = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(fake)));
        var staff = await SignInAsShopManagerAsync(clocked);
        var anonymous = clocked.CreateClient();

        var pricesUrl = $"/api/staff/shop/skus/{product.SkuId}/prices";
        async Task<decimal> PublicPriceAsync()
        {
            var detail = await BodyAsync(await anonymous.GetAsync($"/api/shop/products/{product.Slug}"));
            return decimal.Parse(detail.GetProperty("skus")[0].GetProperty("price").GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);
        }

        static decimal Amount(JsonElement entry) =>
            decimal.Parse(entry.GetProperty("amount").GetProperty("amount").GetString()!, CultureInfo.InvariantCulture);

        // Schedule 2,500 one hour ahead.
        await SchedulePriceAsync(staff, product.SkuId, 2500m, fake.GetUtcNow().AddHours(1));

        var before = await BodyAsync(await staff.GetAsync(pricesUrl));
        Assert.Equal(1000m, Amount(before.GetProperty("current")));
        Assert.Equal(2500m, Amount(Assert.Single(before.GetProperty("scheduled").EnumerateArray())));
        Assert.Equal(0, before.GetProperty("past").GetArrayLength());
        Assert.Equal(1000m, await PublicPriceAsync());

        // An hour (and a minute) later the scheduled price is current and the old one is past.
        fake.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        staff = await SignInAsShopManagerAsync(clocked); // the first session expired with the hour

        var after = await BodyAsync(await staff.GetAsync(pricesUrl));
        Assert.Equal(2500m, Amount(after.GetProperty("current")));
        Assert.Equal(0, after.GetProperty("scheduled").GetArrayLength());
        Assert.Equal(1000m, Amount(Assert.Single(after.GetProperty("past").EnumerateArray())));
        Assert.Equal(2500m, await PublicPriceAsync());
    }

    // ── I-A12 ───────────────────────────────────────────────────────────────────────────────────

    private async Task<(SeededProduct Alpha, SeededProduct Bravo, SeededProduct Charlie, string Brand, string Token)> SeedStaffListAsync(HttpClient staff)
    {
        var brand = NewBrand("StaffList");
        var token = Token();
        var alpha = await CreateProductAsync(staff, brand, $"Alpha {token}");
        var bravo = await CreateProductAsync(staff, brand, $"Bravo {token}");
        var charlie = await CreateProductAsync(staff, brand, $"Charlie {token}", category: "ToysAndFurniture", publish: false);
        await CreateProductAsync(staff, NewBrand("OtherList"), $"Alpha {token}"); // same name, other brand: must never appear
        return (alpha, bravo, charlie, brand, token);
    }

    private static List<string> ItemNames(JsonElement body) =>
        body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()!).ToList();

    [Fact]
    public async Task I_A12_StaffList_FilterSortAndPage_AreScopedToTheBrand()
    {
        var staff = await SignInAsShopManagerAsync();
        var (alpha, bravo, charlie, brand, token) = await SeedStaffListAsync(staff);
        var scope = $"/api/staff/shop/products?brand={Uri.EscapeDataString(brand)}";

        // Brand alone: three products, name ascending by default.
        var all = await BodyAsync(await staff.GetAsync(scope));
        Assert.Equal(3, all.GetProperty("totalCount").GetInt32());
        Assert.Equal([$"Alpha {token}", $"Bravo {token}", $"Charlie {token}"], ItemNames(all));

        // Status.
        var active = await BodyAsync(await staff.GetAsync($"{scope}&status=Active"));
        Assert.Equal(2, active.GetProperty("totalCount").GetInt32());
        Assert.Equal([alpha.ProductId, bravo.ProductId], OrderedItemIds(active));
        var draft = await BodyAsync(await staff.GetAsync($"{scope}&status=Draft"));
        Assert.Equal([charlie.ProductId], OrderedItemIds(draft));
        Assert.Equal("Draft", draft.GetProperty("items")[0].GetProperty("status").GetString());

        // Category, kind and listed.
        var toys = await BodyAsync(await staff.GetAsync($"{scope}&category=ToysAndFurniture"));
        Assert.Equal([charlie.ProductId], OrderedItemIds(toys));
        Assert.Equal(3, (await BodyAsync(await staff.GetAsync($"{scope}&kind=Standard"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await BodyAsync(await staff.GetAsync($"{scope}&kind=Bundle"))).GetProperty("totalCount").GetInt32());
        var listed = (await BodyAsync(await staff.GetAsync($"{scope}&listed=true"))).GetProperty("totalCount").GetInt32();
        var unlisted = (await BodyAsync(await staff.GetAsync($"{scope}&listed=false"))).GetProperty("totalCount").GetInt32();
        Assert.Equal(3, listed + unlisted);

        // Free text, combined with a filter.
        var byName = await BodyAsync(await staff.GetAsync($"{scope}&q={Uri.EscapeDataString("bravo " + token)}"));
        Assert.Equal([bravo.ProductId], OrderedItemIds(byName));
        var combined = await BodyAsync(await staff.GetAsync($"{scope}&status=Active&q={Uri.EscapeDataString("charlie " + token)}"));
        Assert.Equal(0, combined.GetProperty("totalCount").GetInt32());

        // Sort.
        var descending = await BodyAsync(await staff.GetAsync($"{scope}&sort=name_desc"));
        Assert.Equal([charlie.ProductId, bravo.ProductId, alpha.ProductId], OrderedItemIds(descending));

        // Paging: two per page over three rows.
        var page1 = await BodyAsync(await staff.GetAsync($"{scope}&pageSize=2&pageNumber=1"));
        Assert.Equal(3, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal([alpha.ProductId, bravo.ProductId], OrderedItemIds(page1));
        var page2 = await BodyAsync(await staff.GetAsync($"{scope}&pageSize=2&pageNumber=2"));
        Assert.Equal(3, page2.GetProperty("totalCount").GetInt32());
        Assert.Equal([charlie.ProductId], OrderedItemIds(page2));
        var page3 = await BodyAsync(await staff.GetAsync($"{scope}&pageSize=2&pageNumber=3"));
        Assert.Equal(0, page3.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task I_A12_CsvExport_HasTheHeaderRow_TheFilteredRows_AndTheInjectionGuard()
    {
        var staff = await SignInAsShopManagerAsync();
        var (_, _, _, brand, token) = await SeedStaffListAsync(staff);
        var guardedBrand = NewBrand("CsvGuard");
        await CreateProductAsync(staff, guardedBrand, $"=HYPERLINK-{token}");

        static string[] Lines(string csv) => csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();

        var response = await staff.GetAsync($"/api/staff/shop/products/export.csv?brand={Uri.EscapeDataString(brand)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());

        var lines = Lines(await response.Content.ReadAsStringAsync());
        Assert.Equal("ProductId,Name,Brand,Category,Status,Kind,SkuCount,ListedOnStorefront,LastUpdatedOn", lines[0]);
        Assert.Equal(4, lines.Length); // header + the brand's three products, and no other brand's
        Assert.Contains(lines.Skip(1), l => l.Contains($"\"Alpha {token}\"") && l.Contains($"\"{brand}\""));
        Assert.Contains(lines.Skip(1), l => l.Contains($"\"Charlie {token}\"") && l.Contains("\"Draft\""));

        // The export honours the same filter as the list.
        var activeOnly = Lines(await staff.GetStringAsync($"/api/staff/shop/products/export.csv?brand={Uri.EscapeDataString(brand)}&status=Active"));
        Assert.Equal(3, activeOnly.Length);
        Assert.DoesNotContain(activeOnly, l => l.Contains($"Charlie {token}"));

        // A cell that begins with = is prefixed with ' so a spreadsheet cannot run it as a formula.
        var guarded = Lines(await staff.GetStringAsync($"/api/staff/shop/products/export.csv?brand={Uri.EscapeDataString(guardedBrand)}"));
        var row = Assert.Single(guarded.Skip(1));
        Assert.Contains($"\"'=HYPERLINK-{token}\"", row);
    }

    // ── Reviews (I-E2 rating, I-E3 rating sort) ─────────────────────────────────────────────────

    private async Task<Func<SeededProduct, int, Task>> ReviewerAsync(HttpClient staff)
    {
        var (reviewFactory, history) = Factory.WithControllablePurchaseHistory();
        var (customer, customerId) = await RegisterAndSignInCustomerAsync(reviewFactory);

        return async (product, rating) =>
        {
            history.SetPurchased(customerId, product.SkuId);
            var submit = await customer.PostAsJsonAsync("/api/customers/me/reviews",
                new { productId = product.ProductId, skuId = product.SkuId, rating, title = (string?)null, body = "Reviewed for the catalogue test." }, Json);
            Assert.True(submit.StatusCode == HttpStatusCode.Created, $"{(int)submit.StatusCode}: {await submit.Content.ReadAsStringAsync()}");
            var reviewId = (await BodyAsync(submit)).GetProperty("reviewId").GetGuid();
            Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsync($"/api/staff/shop/reviews/{reviewId}/publish", null)).StatusCode);
        };
    }

    // ── I-E2 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E2_EveryFilter_AloneAndCombined_ReturnsExactlyTheExpectedProducts()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Filters");
        var review = await ReviewerAsync(staff);

        var p1 = await CreateProductAsync(staff, brand, "Kitten Dry " + Token(), category: "CatFood", subcategory: "Dry", countryCode: "FR", lifeStage: "Kitten", proteinSource: "Chicken", price: 1200m);
        var p2 = await CreateProductAsync(staff, brand, "Adult Wet " + Token(), category: "CatFood", subcategory: "Wet", countryCode: "DE", lifeStage: "Adult", proteinSource: "Salmon", price: 3000m);
        var p3 = await CreateProductAsync(staff, brand, "Adult Dry " + Token(), category: "CatFood", subcategory: "Dry", countryCode: "FR", lifeStage: "Adult", proteinSource: "Chicken", price: 2000m);
        var p4 = await CreateProductAsync(staff, brand, "Toy Mouse " + Token(), category: "ToysAndFurniture", price: 500m);
        var p5 = await CreateProductAsync(staff, brand, "Toy Ball " + Token(), category: "ToysAndFurniture", price: 700m);
        await MakeSkuUnpricedAsync(p5.SkuId); // no sellable price: not in stock
        await review(p1, 5);
        await review(p2, 2);

        var scope = $"/api/shop/products?brand={Uri.EscapeDataString(brand)}&pageSize=48";

        async Task AssertFilterAsync(string filter, params SeededProduct[] expected)
        {
            var response = await AnonymousGetAsync($"{scope}&{filter}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await BodyAsync(response);
            Assert.True(expected.Select(p => p.ProductId).ToHashSet().SetEquals(ItemIds(body)),
                $"filter '{filter}': expected [{string.Join(", ", expected.Select(p => p.ProductId))}] but got [{string.Join(", ", ItemIds(body))}]");
            Assert.Equal(expected.Length, body.GetProperty("totalCount").GetInt32());
        }

        // The brand alone: all five (an out-of-stock product is listed, just not "in stock").
        await AssertFilterAsync("", p1, p2, p3, p4, p5);

        // Each filter alone.
        await AssertFilterAsync("origin=FR", p1, p3);
        await AssertFilterAsync("category=CatFood", p1, p2, p3);
        await AssertFilterAsync("category=ToysAndFurniture", p4, p5);
        await AssertFilterAsync("category=CatFood&subcategory=Dry", p1, p3);
        await AssertFilterAsync("subcategory=Wet", p2);
        await AssertFilterAsync("lifeStage=Adult", p2, p3);
        await AssertFilterAsync("lifeStage=Kitten", p1);
        await AssertFilterAsync("protein=Chicken", p1, p3);
        await AssertFilterAsync("protein=Salmon", p2);
        await AssertFilterAsync("minPrice=1500&maxPrice=2500", p3);
        await AssertFilterAsync("minPrice=1000", p1, p2, p3);
        await AssertFilterAsync("maxPrice=1200", p1, p4);
        await AssertFilterAsync("availability=inStock", p1, p2, p3, p4);
        await AssertFilterAsync("minRating=4", p1);
        await AssertFilterAsync("minRating=2", p1, p2);

        // Three filters combined.
        await AssertFilterAsync("origin=FR&lifeStage=Adult&protein=Chicken", p3);
        await AssertFilterAsync("category=CatFood&minRating=2&maxPrice=2500", p1);
        await AssertFilterAsync("subcategory=Dry&availability=inStock&minPrice=1500", p3);
        await AssertFilterAsync("category=ToysAndFurniture&availability=inStock&maxPrice=600", p4);

        // Filters that exclude everything.
        await AssertFilterAsync("origin=FR&protein=Salmon");
        await AssertFilterAsync("minRating=5&maxPrice=1000");

        // The response echoes what was applied.
        var echoed = (await BodyAsync(await AnonymousGetAsync($"{scope}&origin=FR&subcategory=Dry&minRating=4"))).GetProperty("appliedFilters");
        Assert.Equal("FR", echoed.GetProperty("origin").GetString());
        Assert.Equal("Dry", echoed.GetProperty("subcategory").GetString());
        Assert.Equal("4", echoed.GetProperty("minRating").GetString());
    }

    // ── I-E3 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E3_EverySort_OrdersCorrectly_WithABrandAndAvailabilityFilter_AndEchoesTheSort()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Sorts");
        var review = await ReviewerAsync(staff);

        // Created (and so first published) in this order: q1 oldest ... q4 newest.
        var q1 = await CreateProductAsync(staff, brand, "Sort One " + Token(), price: 1500m);
        var q2 = await CreateProductAsync(staff, brand, "Sort Two " + Token(), price: 900m);
        var q3 = await CreateProductAsync(staff, brand, "Sort Three " + Token(), price: 2500m);
        var q4 = await CreateProductAsync(staff, brand, "Sort Four " + Token(), price: 1200m);
        var outOfStock = await CreateProductAsync(staff, brand, "Sort Gone " + Token(), price: 100m);
        await SetSkuOutOfStockAsync(outOfStock.SkuId);

        var units = NextUnitsSold();
        await PushUnitsSoldAsync(q1.ProductId, units + 30);
        await PushUnitsSoldAsync(q2.ProductId, units + 10);
        await PushUnitsSoldAsync(q3.ProductId, units + 40);
        await PushUnitsSoldAsync(q4.ProductId, units + 20);
        await PushUnitsSoldAsync(outOfStock.ProductId, units + 50); // would lead popularity were it not filtered out

        await review(q1, 5);
        await review(q2, 3);
        await review(q4, 4); // q3 has no rating

        async Task AssertSortAsync(string sort, string applied, params SeededProduct[] expectedOrder)
        {
            var response = await AnonymousGetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&availability=inStock&sort={sort}&pageSize=48");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await BodyAsync(response);
            Assert.Equal(expectedOrder.Select(p => p.ProductId).ToList(), OrderedItemIds(body));
            Assert.Equal(applied, body.GetProperty("appliedFilters").GetProperty("sort").GetString());
            Assert.Equal(brand, body.GetProperty("appliedFilters").GetProperty("brand").GetString());
            Assert.Equal("inStock", body.GetProperty("appliedFilters").GetProperty("availability").GetString());
        }

        await AssertSortAsync("price_asc", "PriceAscending", q2, q4, q1, q3);
        await AssertSortAsync("price_desc", "PriceDescending", q3, q1, q4, q2);
        await AssertSortAsync("popularity", "Popularity", q3, q1, q4, q2);
        await AssertSortAsync("newest", "Newest", q4, q3, q2, q1);
        await AssertSortAsync("rating", "Rating", q1, q4, q2, q3);

        // Paging keeps the sort: page 2 of 2 per page is the tail of the popularity order.
        var page2 = await BodyAsync(await AnonymousGetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&availability=inStock&sort=popularity&pageSize=2&pageNumber=2"));
        Assert.Equal([q4.ProductId, q2.ProductId], OrderedItemIds(page2));
    }

    // ── R3 over HTTP ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task R3_MalformedSkuCode_OverHttp_IsInvalidSkuCode()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("R3Sku"), "R3 Sku Product", publish: false);

        var response = await staff.PostAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/skus",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId), code = "not a valid code" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("InvalidSkuCode", FirstErrorCode(await BodyAsync(response)));
    }

    [Fact]
    public async Task R3_OverlongNameBrandAndDescription_OverHttp_AreRefused_NeverAsInvalidSlug()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("R3Text"), "R3 Text Product", publish: false);
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));

        async Task<HttpResponseMessage> UpdateAsync(string name, string brand, string description) =>
            await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}", new
            {
                expectedVersion = await ProductVersionAsync(staff, product.ProductId), name, brand, description,
                category = "HealthAndGrooming", taxClassId, tags = Array.Empty<string>(), healthFocus = Array.Empty<string>()
            }, Json);

        foreach (var response in new[]
        {
            await UpdateAsync(new string('n', 201), "Acme", "ok"),
            await UpdateAsync("Name", new string('b', 101), "ok"),
            await UpdateAsync("Name", "Acme", new string('d', 5001))
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.DoesNotContain("InvalidSlug", await response.Content.ReadAsStringAsync());
        }
    }
}
