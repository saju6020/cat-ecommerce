using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 §16 -- I-E9 (maintenance mode), I-E10 (the `catalogue` rate limit), I-E12 (cache
/// headers), U-X3 (CSV injection guard), I-X1 (no free text in logs), I-X2 (no customer identifier in
/// any review or recommendation response).
///
/// I-E9 flips the GLOBAL maintenance setting in the shared database, so this class is serialised
/// with the other global-setting tests (HO-ADM-001's own collection) and restores the setting in a
/// <c>finally</c> block.
/// </summary>
[Collection(AdministrationGlobalSettingsCollection.Name)]
public sealed class ShopStorefrontCrossCuttingEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static string NewBrand(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    // ── I-E9 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E9_MaintenanceMode_StorefrontRoutesAre503_StaffRoutesStillWork()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Maint"), "Maintenance Item");
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        var (_, adminMobile, adminPassword) = await SeedStaffAsync("Administrator");
        var admin = await SignInStaffAsync(adminMobile, adminPassword);

        var on = await admin.PutAsJsonAsync("/api/admin/system/maintenance", new { maintenanceMode = true, message = "Back soon" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, on.StatusCode);
        try
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await CreateClient().GetAsync("/api/shop/products")).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await CreateClient().GetAsync($"/api/shop/products/{product.Slug}")).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await customer.PutAsync($"/api/customers/me/wishlist/items/{product.SkuId}", null)).StatusCode);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, (await customer.GetAsync("/api/customers/me/stock-alerts")).StatusCode);

            // The back office is never gated (HO-ADM-001 ADR-053, ADR-093).
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/staff/shop/products")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/api/staff/shop/products/{product.ProductId}")).StatusCode);
        }
        finally
        {
            var off = await admin.PutAsJsonAsync("/api/admin/system/maintenance", new { maintenanceMode = false, message = (string?)null }, Json);
            Assert.Equal(HttpStatusCode.NoContent, off.StatusCode);
        }

        Assert.Equal(HttpStatusCode.OK, (await CreateClient().GetAsync("/api/shop/products")).StatusCode);
    }


    // ── I-E5 (the filters half) ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E5_Filters_OfferTheBrandsOriginsLifeStagesProteinsAndPriceBoundsOfActiveListedProducts()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Facet");
        await CreateProductAsync(staff, brand, "Facet Food", "CatFood", price: 777m, countryCode: "SE", lifeStage: "Senior", proteinSource: "Duck");
        await CreateProductAsync(staff, brand, "Draft Food", "CatFood", countryCode: "NO", lifeStage: "Kitten", publish: false);

        var response = await CreateClient().GetAsync("/api/shop/filters?category=CatFood");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        var brandFacet = body.GetProperty("brands").EnumerateArray().FirstOrDefault(b => b.GetProperty("name").GetString() == brand);
        Assert.True(brandFacet.ValueKind == JsonValueKind.Object, "The brand facet is missing (the facets are cached for 60 s; a fresh brand appears on the next window).");
        Assert.Equal(1, brandFacet.GetProperty("count").GetInt32());
        Assert.Contains(body.GetProperty("origins").EnumerateArray(), o => o.GetProperty("code").GetString() == "SE" && o.GetProperty("name").GetString() == "Sweden");
        Assert.Contains(body.GetProperty("lifeStages").EnumerateArray(), l => l.GetProperty("value").GetString() == "Senior");
        Assert.Contains(body.GetProperty("proteinSources").EnumerateArray(), p => p.GetProperty("value").GetString() == "Duck");
        Assert.True(body.GetProperty("price").GetProperty("min").GetDecimal() <= 777m);
    }
    // ── I-E10 ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E10_CatalogueLimitLoweredToThree_TheFourthSearchIs429WithRetryAfter()
    {
        var tight = Factory.WithTightCatalogueLimit(3);
        var client = tight.CreateClient();

        for (var i = 0; i < 3; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/shop/products?q=limit{i}")).StatusCode);

        var fourth = await client.GetAsync("/api/shop/products?q=limit3");
        Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
        Assert.True(fourth.Headers.RetryAfter is not null, "A 429 must carry Retry-After.");
    }

    // ── I-E12 ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_E12_PublicReadsAreCacheablePublic60_PersonalReadsAreNoStore()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Cache"), "Cache Item");
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        foreach (var url in new[] { "/api/shop/products", $"/api/shop/products/{product.Slug}", "/api/shop/filters", "/api/shop/categories", "/api/shop/origins",
                     $"/api/shop/products/{product.Slug}/reviews", $"/api/shop/products/{product.Slug}/recommendations", "/api/shop/locales" })
        {
            var response = await CreateClient().GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            Assert.True(response.Headers.CacheControl is { Public: true, MaxAge: { TotalSeconds: 60 } }, $"{url}: {response.Headers.CacheControl}");
        }

        foreach (var url in new[] { $"/api/customers/me/shop/products/{product.Slug}/purchasability", "/api/customers/me/wishlist", "/api/customers/me/stock-alerts" })
        {
            var response = await customer.GetAsync(url);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            Assert.True(response.Headers.CacheControl is { NoStore: true }, $"{url}: {response.Headers.CacheControl}");
        }

        var personalRecommendations = await customer.GetAsync($"/api/shop/products/{product.Slug}/recommendations");
        Assert.True(personalRecommendations.Headers.CacheControl is { NoStore: true, Private: true });
    }

    // ── U-X3 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task U_X3_CsvExport_PrefixesAFormulaCellWithAnApostrophe()
    {
        var staff = await SignInAsShopManagerAsync();
        var marker = Guid.NewGuid().ToString("N")[..10];
        var brand = NewBrand("Csv");
        await CreateProductAsync(staff, brand, $"=HYPERLINK(\"http://example.test\",\"{marker}\")", publish: false);
        await CreateProductAsync(staff, brand, $"+SUM(1+1) {marker}", publish: false);
        await CreateProductAsync(staff, brand, $"@cmd {marker}", publish: false);
        await CreateProductAsync(staff, brand, $"Ordinary {marker}", publish: false);

        var response = await staff.GetAsync($"/api/staff/shop/products/export.csv?brand={Uri.EscapeDataString(brand)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var csv = await response.Content.ReadAsStringAsync();

        Assert.StartsWith("ProductId,Name,Brand", csv);
        Assert.Contains("\"'=HYPERLINK(", csv);
        Assert.Contains("\"'+SUM(1+1)", csv);
        Assert.Contains("\"'@cmd", csv);
        Assert.Contains($"\"Ordinary {marker}\"", csv); // an ordinary cell is not prefixed
        Assert.DoesNotContain("\"=HYPERLINK(", csv);
    }

    // ── I-X1 / I-X2 ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_X1_NoReviewTextAllergenTextCustomerNamesMobilesOrEmailsAreLogged_AndI_X2_NoReviewOrRecommendationBodyCarriesAnIdentity()
    {
        var (capturedBase, logs) = Factory.WithCapturedLogs();
        var purchases = new ControllablePurchaseHistoryReader();
        var capturedFactory = capturedBase.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<CatEcommerce.Domain.DomainServices.IPurchaseHistoryReader>(_ => purchases)));

        var marker = Guid.NewGuid().ToString("N")[..12];
        var reviewMarker = $"REVIEWTEXT-{marker}";
        var allergenMarker = $"ALLERGEN{marker}";
        var nameMarker = $"Zephyrine{marker}"; // the customer's full name is "Test Customer" -- this one is greppable
        var email = NewEmail();

        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Logs");
        var food = await CreateProductAsync(staff, brand, "Log Check Food", "CatFood", lifeStage: "Adult", ingredientList: $"salmon, {allergenMarker.ToLowerInvariant()}");

        var (customer, customerId) = await RegisterAndSignInCustomerAsync(capturedFactory, email);
        var mobile = await ReadMobileAsync(customer);
        purchases.SetPurchased(customerId, food.SkuId);

        // D: a review (text is customer-authored free text), moderated.
        var review = await customer.PostAsJsonAsync("/api/customers/me/reviews",
            new { productId = food.ProductId, skuId = food.SkuId, rating = 5, title = $"Title {reviewMarker}", body = reviewMarker }, Json);
        Assert.Equal(HttpStatusCode.Created, review.StatusCode);
        var reviewId = (await review.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("reviewId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsync($"/api/staff/shop/reviews/{reviewId}/publish", null)).StatusCode);

        // G: wishlist and a stock alert.
        await customer.PutAsync($"/api/customers/me/wishlist/items/{food.SkuId}", null);
        var rename = await customer.PutAsJsonAsync("/api/customers/me", new { fullName = nameMarker, emailAddress = email, additionalPhoneNumbers = Array.Empty<string>(), currentPassword = "correcthorsebattery" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, rename.StatusCode);

        // H: a recommendation for a cat that records an allergen the log must never see.
        var catId = await SeedCatAsync(customerId, "Whiskers", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3));
        await RecordCatFactsAsync(catId, allergens: [allergenMarker], conditions: [$"Cystitis {marker}"]);
        var recommendations = await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations");
        Assert.Equal(HttpStatusCode.OK, recommendations.StatusCode);
        var recommendationsBody = await recommendations.Content.ReadAsStringAsync();

        // I-X2: no public review or recommendation body carries a customer id, mobile or email. (The
        // review author is rendered as a display name, "Rahim K." -- by design, section 12.6 -- so the
        // name itself is only checked against the LOGS below.)
        var publicReviews = await (await CreateClient().GetAsync($"/api/shop/products/{food.Slug}/reviews")).Content.ReadAsStringAsync();
        var publicRecommendations = await (await CreateClient().GetAsync($"/api/shop/products/{food.Slug}/recommendations")).Content.ReadAsStringAsync();
        foreach (var body in new[] { publicReviews, publicRecommendations })
        {
            Assert.DoesNotContain(customerId.ToString(), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(mobile, body);
            Assert.DoesNotContain(email, body);
        }

        using (var document = JsonDocument.Parse(publicReviews))
            AssertNoIdentityFields(document.RootElement);
        using (var document = JsonDocument.Parse(recommendationsBody))
            Assert.DoesNotContain(customerId.ToString(), recommendationsBody, StringComparison.OrdinalIgnoreCase);

        // I-X1: nothing free-text or personal appears at any log level.
        var allLogText = string.Join("\n", logs);
        Assert.DoesNotContain(reviewMarker, allLogText);
        Assert.DoesNotContain(allergenMarker, allLogText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"Cystitis {marker}", allLogText);
        Assert.DoesNotContain(nameMarker, allLogText);
        Assert.DoesNotContain(email, allLogText);
        Assert.DoesNotContain(mobile, allLogText);
    }

    private static readonly string[] ForbiddenFieldNames = ["customerId", "mobile", "mobileNumber", "email", "emailAddress", "fullName", "phone"];

    private static void AssertNoIdentityFields(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Assert.DoesNotContain(property.Name, ForbiddenFieldNames, StringComparer.OrdinalIgnoreCase);
                    AssertNoIdentityFields(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) AssertNoIdentityFields(item);
                break;
        }
    }

    private static async Task<string> ReadMobileAsync(HttpClient customer)
    {
        var profile = await customer.GetFromJsonAsync<JsonElement>("/api/customers/me", Json);
        return profile.GetProperty("mobileNumber").GetString()!;
    }
}
