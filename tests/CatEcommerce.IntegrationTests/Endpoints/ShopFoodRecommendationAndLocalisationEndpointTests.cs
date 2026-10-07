using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.Commands.Product;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 §16 Phase H -- I-H1..I-H6 (US-SHP-11, US-SHP-14).
///
/// §16.1 rule 1: the catalogue is global and never truncated, so the recommendation tests give their
/// products a units-sold figure that grows with the wall clock (a newer run always outranks an older
/// run's products on the tie-break) and a health focus nothing else uses, so they are the highest
/// ranked and assertions are about their own products, never a global list.
/// </summary>
public sealed class ShopFoodRecommendationAndLocalisationEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static string NewBrand(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>(Json);


    private static List<JsonElement> Items(JsonElement body) => body.GetProperty("items").EnumerateArray().ToList();

    private static JsonElement ItemFor(JsonElement body, Guid productId) =>
        Items(body).Single(i => i.GetProperty("item").GetProperty("productId").GetGuid() == productId);

    private static List<string> ReasonCodes(JsonElement item) =>
        item.GetProperty("reasons").EnumerateArray().Select(r => r.GetProperty("code").GetString()!).ToList();

    // ── I-H1 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_H1_NeuteredAdultCat_GetsAdultFoodWithReasons_TheSterilisedOneFirst_NoKittenFood()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("RecAdult");
        var units = NextUnitsSold();

        // "Hairball" is the focus nothing else in the shared catalogue combines with Sterilised.
        var sterilised = await CreateProductAsync(staff, brand, "Hairball Sterile Adult", "CatFood", lifeStage: "Adult", healthFocus: ["Sterilised", "Hairball"]);
        var plainAdult = await CreateProductAsync(staff, brand, "Plain Adult", "CatFood", lifeStage: "Adult");
        var kitten = await CreateProductAsync(staff, brand, "Kitten Only", "CatFood", lifeStage: "Kitten", healthFocus: ["Sterilised", "Hairball"]);
        foreach (var product in new[] { sterilised, plainAdult, kitten }) await PushUnitsSoldAsync(product.ProductId, units);

        var (customer, customerId) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Mitu", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3));
        await RecordCatFactsAsync(catId, conditions: ["Hairball problems"], neutered: true);

        var response = await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations");
        Assert.True(response.Headers.CacheControl is { NoStore: true });
        var body = await JsonOkAsync(response);

        Assert.Equal("Adult", body.GetProperty("cat").GetProperty("lifeStage").GetString());
        Assert.False(body.GetProperty("cat").GetProperty("lifeStageIsEstimated").GetBoolean());
        Assert.Equal(0, body.GetProperty("hints").GetArrayLength());

        var first = Items(body)[0];
        Assert.Equal(sterilised.ProductId, first.GetProperty("item").GetProperty("productId").GetGuid());
        var reasons = ReasonCodes(first);
        Assert.Contains("LifeStageMatch", reasons);
        Assert.Contains("Sterilised", reasons);
        Assert.Contains("ConditionMatch", reasons);
        Assert.Equal("Adult", first.GetProperty("reasons").EnumerateArray().First(r => r.GetProperty("code").GetString() == "LifeStageMatch")
            .GetProperty("values").GetProperty("lifeStage").GetString());

        // A plain adult product, when it is within the top twelve, carries only the life-stage reason.
        var plainItem = Items(body).Where(i => i.GetProperty("item").GetProperty("productId").GetGuid() == plainAdult.ProductId).ToList();
        foreach (var item in plainItem) Assert.Equal(["LifeStageMatch"], ReasonCodes(item));

        Assert.DoesNotContain(Items(body), i => i.GetProperty("item").GetProperty("productId").GetGuid() == kitten.ProductId);
    }

    [Fact]
    public async Task I_H1_HeavyAdultCat_GetsWeightManagementFood_AKittenDoesNot()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("RecWeight");
        var units = NextUnitsSold();
        var light = await CreateProductAsync(staff, brand, "Light Formula", "CatFood", lifeStage: "AllLifeStages", healthFocus: ["WeightManagement", "Dental"]);
        await PushUnitsSoldAsync(light.ProductId, units);

        var (customer, customerId) = await RegisterAndSignInCustomerAsync();
        var adult = await SeedCatAsync(customerId, "Heavy", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3));
        await RecordCatFactsAsync(adult, conditions: ["Gingivitis"], weightKg: 6.5m);
        var kitten = await SeedCatAsync(customerId, "Tiny", DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-6));
        await RecordCatFactsAsync(kitten, conditions: ["Gingivitis"], weightKg: 6.5m);

        var adultBody = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{adult}/food-recommendations"));
        var adultReasons = ReasonCodes(ItemFor(adultBody, light.ProductId));
        Assert.Contains("WeightManagement", adultReasons);
        Assert.Contains("ConditionMatch", adultReasons);

        var kittenBody = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{kitten}/food-recommendations"));
        Assert.Equal("Kitten", kittenBody.GetProperty("cat").GetProperty("lifeStage").GetString());
        Assert.DoesNotContain("WeightManagement", ReasonCodes(ItemFor(kittenBody, light.ProductId)));
    }

    [Fact]
    public async Task I_H1_NoDateOfBirth_NothingIsFilteredByLifeStage_AndTheResultHintsAtIt()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("RecNoDob");
        var units = NextUnitsSold();
        var sterile = await CreateProductAsync(staff, brand, "Sterile Any Age", "CatFood", lifeStage: "Kitten", healthFocus: ["Sterilised", "SkinAndCoat"]);
        await PushUnitsSoldAsync(sterile.ProductId, units);

        var (customer, customerId) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Unknown Age");
        await RecordCatFactsAsync(catId, conditions: ["Dermatitis"], neutered: true);

        // Remove the date of birth the seeder gave the cat (the register DTO needs one).
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
            await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync(
                db.Database, $"UPDATE clinic.cat SET \"DateOfBirth\" = NULL WHERE \"Id\" = {catId}");
        }

        var body = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations"));

        Assert.Contains("AddDateOfBirth", body.GetProperty("hints").EnumerateArray().Select(h => h.GetString()));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("cat").GetProperty("lifeStage").ValueKind);
        var item = ItemFor(body, sterile.ProductId);
        Assert.DoesNotContain("LifeStageMatch", ReasonCodes(item));
        Assert.Contains("Sterilised", ReasonCodes(item));
    }

    // ── I-H2 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_H2_AProductWhoseIngredientsMatchARecordedAllergy_IsExcluded_AndCounted()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("RecAllergy");
        var units = NextUnitsSold();
        var safe = await CreateProductAsync(staff, brand, "Salmon Dinner", "CatFood", lifeStage: "Adult", ingredientList: "salmon, rice", healthFocus: ["Sterilised", "Urinary"]);
        var poultry = await CreateProductAsync(staff, brand, "Poultry Dinner", "CatFood", lifeStage: "Adult", ingredientList: "chicken meal, rice", healthFocus: ["Sterilised", "Urinary"]);
        var pulses = await CreateProductAsync(staff, brand, "Pulse Dinner", "CatFood", lifeStage: "Adult", ingredientList: "chickpea flour", healthFocus: ["Sterilised", "Urinary"]);
        foreach (var product in new[] { safe, poultry, pulses }) await PushUnitsSoldAsync(product.ProductId, units);

        var (customer, customerId) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(customerId, "Allergic", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3));
        await RecordCatFactsAsync(catId, allergens: ["Chicken"], conditions: ["Cystitis"], neutered: true);

        var body = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations"));

        Assert.DoesNotContain(Items(body), i => i.GetProperty("item").GetProperty("productId").GetGuid() == poultry.ProductId);
        _ = ItemFor(body, safe.ProductId);
        _ = ItemFor(body, pulses.ProductId); // "chickpea" is not "chicken"

        var exclusion = body.GetProperty("excludedForAllergy").EnumerateArray().Single(e => e.GetProperty("allergen").GetString() == "Chicken");
        Assert.True(exclusion.GetProperty("count").GetInt32() >= 1);
    }

    // ── I-H3 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_H3_AnotherCustomersCat_ADeceasedCat_AnUnknownCat_Are404_AndAnonymousIsRefused()
    {
        var (owner, ownerId) = await RegisterAndSignInCustomerAsync();
        var (other, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(ownerId);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/customers/me/cats/{Guid.NewGuid()}/food-recommendations")).StatusCode);

        var anonymous = await CreateClient().GetAsync($"/api/customers/me/cats/{catId}/food-recommendations");
        Assert.True(anonymous.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);

        await MarkCatDeceasedAsync(catId);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/customers/me/cats/{catId}/food-recommendations")).StatusCode);
    }

    [Fact]
    public async Task I_H1_APrescriptionFood_IsRecommendedOnlyForTheCatItIsAuthorisedFor()
    {
        var (gateFactory, authorisations) = Factory.WithControllableDietGate();
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("RecRx");
        var units = NextUnitsSold();
        var rx = await CreateProductAsync(staff, brand, "Renal Rx Diet", "CatFood", prescription: true, lifeStage: "Adult", healthFocus: ["Renal", "Digestive"]);
        await PushUnitsSoldAsync(rx.ProductId, units);

        var (customer, customerId) = await RegisterAndSignInCustomerAsync(gateFactory);
        var authorisedCat = await SeedCatAsync(customerId, "Authorised", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3));
        var otherCat = await SeedCatAsync(customerId, "Other", DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3));
        await RecordCatFactsAsync(authorisedCat, conditions: ["Chronic kidney disease", "Colitis"]);
        await RecordCatFactsAsync(otherCat, conditions: ["Chronic kidney disease", "Colitis"]);
        authorisations.Authorise(customerId, authorisedCat, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));

        var forAuthorised = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{authorisedCat}/food-recommendations"));
        _ = ItemFor(forAuthorised, rx.ProductId);

        // U-H4 end to end: an authorisation for another cat of the same customer does not cover this one.
        var forOther = await JsonOkAsync(await customer.GetAsync($"/api/customers/me/cats/{otherCat}/food-recommendations"));
        Assert.DoesNotContain(Items(forOther), i => i.GetProperty("item").GetProperty("productId").GetGuid() == rx.ProductId);
    }

    // ── I-H4 / I-H5 / I-H6 ──────────────────────────────────────────────────────────────────────

    private async Task<(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> BnFactory, HttpClient Staff, SeededProduct Product)> BengaliProductAsync()
    {
        var bnFactory = Factory.WithBengaliEnabled();
        var staff = await SignInAsShopManagerAsync(bnFactory);
        var product = await CreateProductAsync(staff, NewBrand("Bn"), "English Name", "CatFood", lifeStage: "Adult", ingredientList: "english ingredients");
        return (bnFactory, staff, product);
    }

    private async Task SetTranslationAsync(HttpClient staff, Guid productId, string locale, object content, HttpStatusCode expected = HttpStatusCode.NoContent)
    {
        var json = JsonSerializer.SerializeToElement(content, Json);
        var body = new Dictionary<string, object?> { ["expectedVersion"] = await ProductVersionAsync(staff, productId) };
        foreach (var property in json.EnumerateObject()) body[property.Name] = property.Value;

        var response = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/translations/{locale}", body, Json);
        Assert.True(response.StatusCode == expected, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task I_H4_AllFourFieldsInBengali_ShowInBengali_WithEveryFieldLocaleBn()
    {
        var (bnFactory, staff, product) = await BengaliProductAsync();

        await SetTranslationAsync(staff, product.ProductId, "bn", new
        { name = "বাংলা নাম", description = "বাংলা বিবরণ", ingredientList = "মুরগি, চাল", feedingGuide = "দিনে দুইবার" });

        var anonymous = bnFactory.CreateClient();
        var body = await JsonOkAsync(await anonymous.GetAsync($"/api/shop/products/{product.Slug}?locale=bn"));

        Assert.Equal("বাংলা নাম", body.GetProperty("name").GetString());
        Assert.Equal("বাংলা বিবরণ", body.GetProperty("description").GetString());
        Assert.Equal("মুরগি, চাল", body.GetProperty("food").GetProperty("ingredientList").GetString());
        Assert.Equal("দিনে দুইবার", body.GetProperty("food").GetProperty("feedingGuide").GetString());
        Assert.Equal("bn", body.GetProperty("locale").GetString());
        foreach (var field in new[] { "name", "description", "ingredientList", "feedingGuide" })
            Assert.Equal("bn", body.GetProperty("fieldLocales").GetProperty(field).GetString());

        // The default language is untouched.
        var english = await JsonOkAsync(await anonymous.GetAsync($"/api/shop/products/{product.Slug}"));
        Assert.Equal("English Name", english.GetProperty("name").GetString());
        Assert.Equal("en", english.GetProperty("locale").GetString());

        // US-SHP-14 AC3: the staff detail lists the stored translation.
        var detail = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{product.ProductId}", Json);
        var stored = Assert.Single(detail.GetProperty("translations").EnumerateArray());
        Assert.Equal("bn", stored.GetProperty("locale").GetString());
        Assert.Equal("বাংলা নাম", stored.GetProperty("name").GetString());
    }

    [Fact]
    public async Task I_H4_TheCatalogueListAndSearch_UseTheTranslatedName()
    {
        var (bnFactory, staff, product) = await BengaliProductAsync();
        var token = "BnToken" + Guid.NewGuid().ToString("N")[..10];
        await SetTranslationAsync(staff, product.ProductId, "bn", new { name = $"{token} বাংলা" });

        var anonymous = bnFactory.CreateClient();

        // Search matches the translated name (it joins SearchText) in any locale.
        var byTranslatedName = await JsonOkAsync(await anonymous.GetAsync($"/api/shop/products?q={token.ToLowerInvariant()}&locale=bn"));
        var item = byTranslatedName.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("productId").GetGuid() == product.ProductId);
        Assert.Equal($"{token} বাংলা", item.GetProperty("name").GetString());

        // Without a locale the same product lists under its default-language name.
        var english = await JsonOkAsync(await anonymous.GetAsync($"/api/shop/products?q={token.ToLowerInvariant()}"));
        Assert.Equal("English Name", english.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("productId").GetGuid() == product.ProductId).GetProperty("name").GetString());
    }

    [Fact]
    public async Task I_H5_ARemovedBengaliFeedingGuide_FallsBackToEnglish_PerField()
    {
        var (bnFactory, staff, product) = await BengaliProductAsync();
        // The product's own (English) feeding guide.
        var food = await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/food",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId), lifeStage = "Adult", ingredientList = "english ingredients", feedingGuide = "Feed twice a day" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, food.StatusCode);

        await SetTranslationAsync(staff, product.ProductId, "bn", new { name = "বাংলা নাম", ingredientList = "মুরগি", feedingGuide = "দিনে দুইবার" });
        // Replace the translation without a feeding guide: that field now falls back.
        await SetTranslationAsync(staff, product.ProductId, "bn", new { name = "বাংলা নাম", ingredientList = "মুরগি" });

        var body = await JsonOkAsync(await bnFactory.CreateClient().GetAsync($"/api/shop/products/{product.Slug}?locale=bn"));

        Assert.Equal("Feed twice a day", body.GetProperty("food").GetProperty("feedingGuide").GetString());
        Assert.Equal("en", body.GetProperty("fieldLocales").GetProperty("feedingGuide").GetString());
        Assert.Equal("bn", body.GetProperty("fieldLocales").GetProperty("name").GetString());
        Assert.Equal("bn", body.GetProperty("fieldLocales").GetProperty("ingredientList").GetString());
        Assert.Equal("en", body.GetProperty("fieldLocales").GetProperty("description").GetString());

        // Removing the translation altogether: everything falls back.
        var remove = await staff.DeleteAsync($"/api/staff/shop/products/{product.ProductId}/translations/bn?expectedVersion={await ProductVersionAsync(staff, product.ProductId)}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);
        var afterRemove = await JsonOkAsync(await bnFactory.CreateClient().GetAsync($"/api/shop/products/{product.Slug}?locale=bn"));
        Assert.Equal("English Name", afterRemove.GetProperty("name").GetString());
        Assert.All(afterRemove.GetProperty("fieldLocales").EnumerateObject(), p => Assert.Equal("en", p.Value.GetString()));
    }

    [Fact]
    public async Task I_H6_ALocaleThatIsNotEnabled_Is400_OnWrite_AndTheDefaultLanguageOnAPublicRead()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("NoFr"), "Plain Name", "CatFood", lifeStage: "Adult");

        // The base factory keeps the shipped default: only English is enabled.
        var locales = await JsonOkAsync(await CreateClient().GetAsync("/api/shop/locales"));
        Assert.Equal("en", locales.GetProperty("defaultLocale").GetString());
        Assert.Equal(["en"], locales.GetProperty("locales").EnumerateArray().Select(l => l.GetString()));

        var put = await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/translations/fr",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId), name = "Nom" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        Assert.Contains("LocaleNotEnabled", await put.Content.ReadAsStringAsync());

        var english = await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/translations/en",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId), name = "Name" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, english.StatusCode);
        Assert.Contains("DefaultLocaleNotTranslatable", await english.Content.ReadAsStringAsync());

        var malformed = await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/translations/FRENCH",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId), name = "Nom" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);

        // A public read with an unknown or disabled locale is the default language, never an error.
        foreach (var locale in new[] { "fr", "bn", "nonsense", "" })
        {
            var read = await JsonOkAsync(await CreateClient().GetAsync($"/api/shop/products/{product.Slug}?locale={locale}"));
            Assert.Equal("Plain Name", read.GetProperty("name").GetString());
            Assert.Equal("en", read.GetProperty("locale").GetString());
        }
    }

    [Fact]
    public async Task I_H6_Translations_StaleVersionIs409_AndOnlyManagersMayWrite()
    {
        var (bnFactory, staff, product) = await BengaliProductAsync();

        var stale = await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/translations/bn", new { expectedVersion = 1, name = "x" }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var (_, mobile, password) = await SeedStaffAsync("Reception");
        var reception = await SignInStaffAsync(mobile, password, bnFactory);
        var forbidden = await reception.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/translations/bn",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId), name = "x" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
