using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Read.Queries.Product;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 §16 Phase F -- I-F1..I-F8 (US-SHP-10, US-SHP-12, US-SHP-08). Pharmacy is the
/// controllable stand-in of §16.1 rule 4 (<see cref="ControllableDietAuthorisationGate"/>); I-F8
/// runs on the base factory, whose gate is the shipped fail-closed one.
///
/// I-F5 as written needs Inventory's sellable figures (HO-IMP-001, not built): its quantity cases
/// are proved by U-F2 and its availability wiring here with the interim availability fixture
/// (§1 Amended, 2026-10-04).
/// </summary>
public sealed class ShopPrescriptionBundleRelatedEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static string NewBrand(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static JsonElement Sku(JsonElement purchasability) => purchasability.GetProperty("skus")[0];

    // ── I-F1 / I-F2 / I-F3 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F1_PrescriptionProduct_VisibleButNotPurchasableWithoutAuthorisation_WithTheReason()
    {
        var (gateFactory, _) = Factory.WithControllableDietGate();
        var staff = await SignInAsShopManagerAsync();
        var rx = await CreateProductAsync(staff, NewBrand("RxBrand"), "Renal Support", "CatFood", prescription: true, lifeStage: "Adult");
        var (customer, customerId) = await RegisterAndSignInCustomerAsync(gateFactory);
        await SeedCatAsync(customerId);

        var detail = await CreateClient().GetAsync($"/api/shop/products/{rx.Slug}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.True((await detail.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("prescriptionRequired").GetBoolean());

        var response = await customer.GetAsync($"/api/customers/me/shop/products/{rx.Slug}/purchasability");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl is { NoStore: true, Private: true });

        var sku = Sku(await response.Content.ReadFromJsonAsync<JsonElement>(Json));
        Assert.False(sku.GetProperty("purchasable").GetBoolean());
        Assert.Equal("PrescriptionAuthorisationRequired", sku.GetProperty("purchaseBlockReason").GetString());
        Assert.Equal(0, sku.GetProperty("eligibleCats").GetArrayLength());
    }

    [Fact]
    public async Task I_F2_AuthorisedCat_IsEligible_UntilTheCatDies()
    {
        var (gateFactory, authorisations) = Factory.WithControllableDietGate();
        var staff = await SignInAsShopManagerAsync();
        var rx = await CreateProductAsync(staff, NewBrand("RxBrand"), "Urinary Care", "CatFood", prescription: true);
        var (customer, customerId) = await RegisterAndSignInCustomerAsync(gateFactory);
        var catId = await SeedCatAsync(customerId, "Mitu");

        authorisations.Authorise(customerId, catId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));

        var authorised = Sku(await JsonOkAsync(await customer.GetAsync($"/api/customers/me/shop/products/{rx.Slug}/purchasability")));
        Assert.True(authorised.GetProperty("purchasable").GetBoolean());
        var eligible = authorised.GetProperty("eligibleCats").EnumerateArray().ToList();
        Assert.Single(eligible);
        Assert.Equal(catId, eligible[0].GetProperty("catId").GetGuid());
        Assert.Equal("Mitu", eligible[0].GetProperty("name").GetString());

        await MarkCatDeceasedAsync(catId);

        var afterDeath = Sku(await JsonOkAsync(await customer.GetAsync($"/api/customers/me/shop/products/{rx.Slug}/purchasability")));
        Assert.False(afterDeath.GetProperty("purchasable").GetBoolean());
        Assert.Equal("PrescriptionAuthorisationRequired", afterDeath.GetProperty("purchaseBlockReason").GetString());
    }

    [Fact]
    public async Task I_F2_AnotherCustomersAuthorisation_DoesNotMakeThisCustomerEligible()
    {
        var (gateFactory, authorisations) = Factory.WithControllableDietGate();
        var staff = await SignInAsShopManagerAsync();
        var rx = await CreateProductAsync(staff, NewBrand("RxBrand"), "Digestive Care", "CatFood", prescription: true);
        var (_, ownerId) = await RegisterAndSignInCustomerAsync(gateFactory);
        var ownersCat = await SeedCatAsync(ownerId);
        authorisations.Authorise(ownerId, ownersCat, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));

        var (other, _) = await RegisterAndSignInCustomerAsync(gateFactory);

        var sku = Sku(await JsonOkAsync(await other.GetAsync($"/api/customers/me/shop/products/{rx.Slug}/purchasability")));
        Assert.False(sku.GetProperty("purchasable").GetBoolean());
    }

    [Fact]
    public async Task I_F3_EligibilityContract_IsEvaluatedAtTheCallInstant_ExpiredTheDayAfterValidTo()
    {
        var (gateFactory, authorisations) = Factory.WithControllableDietGate();
        var staff = await SignInAsShopManagerAsync();
        var rx = await CreateProductAsync(staff, NewBrand("RxBrand"), "Weight Control", "CatFood", prescription: true);
        var (_, customerId) = await RegisterAndSignInCustomerAsync(gateFactory);
        var catId = await SeedCatAsync(customerId);

        var validTo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        authorisations.Authorise(customerId, catId, validTo);

        async Task<JsonElement> CheckAsync()
        {
            await using var scope = gateFactory.Services.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
            var response = await dispatcher.GetResultAsync(new CheckPrescriptionEligibilityQuery
            {
                CustomerId = customerId, SkuId = rx.SkuId, CatId = catId,
                UserContext = new UserContext(customerId, Guid.NewGuid()), CorrelationId = Guid.NewGuid()
            });
            return JsonSerializer.SerializeToElement(response.Result, Json);
        }

        // At add-to-cart (the authorisation is valid).
        var atAddToCart = await CheckAsync();
        Assert.True(atAddToCart.GetProperty("eligible").GetBoolean());

        // At checkout the day after ValidTo: the same contract now refuses.
        authorisations.Today = () => validTo.AddDays(1);
        var atCheckout = await CheckAsync();
        Assert.False(atCheckout.GetProperty("eligible").GetBoolean());
        Assert.Equal("PrescriptionAuthorisationRequired", atCheckout.GetProperty("blockReason").GetString());
    }

    [Fact]
    public async Task I_F3_EligibilityContract_NonPrescriptionSkuIsEligible_UnknownSkuIsNull()
    {
        var staff = await SignInAsShopManagerAsync();
        var plain = await CreateProductAsync(staff, NewBrand("Plain"), "Scoop");
        var (_, customerId) = await RegisterAndSignInCustomerAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var ctx = new UserContext(customerId, Guid.NewGuid());

        var ok = await dispatcher.GetResultAsync(new CheckPrescriptionEligibilityQuery
        { CustomerId = customerId, SkuId = plain.SkuId, CatId = Guid.NewGuid(), UserContext = ctx, CorrelationId = Guid.NewGuid() });
        Assert.True(ok.Result!.Eligible);

        var unknown = await dispatcher.GetResultAsync(new CheckPrescriptionEligibilityQuery
        { CustomerId = customerId, SkuId = Guid.NewGuid(), CatId = Guid.NewGuid(), UserContext = ctx, CorrelationId = Guid.NewGuid() });
        Assert.Null(unknown.Result);
    }

    [Fact]
    public async Task Purchasability_AnonymousAndUnknownSlug()
    {
        var anonymous = await CreateClient().GetAsync("/api/customers/me/shop/products/anything/purchasability");
        Assert.True(anonymous.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);

        var (customer, _) = await RegisterAndSignInCustomerAsync();
        var missing = await customer.GetAsync($"/api/customers/me/shop/products/{UniqueSlug("nope")}/purchasability");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // ── I-F4 / I-F5 / I-F6 ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F4_Bundle_PricedAsAUnit_ComponentsListed_CompositionContractReturnsEachWithQuantity()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Bundle");
        var a = await CreateProductAsync(staff, brand, "Litter Scoop", price: 300m);
        var b = await CreateProductAsync(staff, brand, "Cat Toy", price: 150m);
        var bundle = await CreateBundleAsync(staff, brand, "Starter Kit", [(a.SkuId, 2), (b.SkuId, 1)], price: 2500m);

        var detail = await (await CreateClient().GetAsync($"/api/shop/products/{bundle.Slug}")).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Bundle", detail.GetProperty("kind").GetString());

        var skus = detail.GetProperty("skus");
        Assert.Equal(1, skus.GetArrayLength());
        Assert.Equal("2500.00", skus[0].GetProperty("price").GetProperty("amount").GetString());

        var components = detail.GetProperty("bundle").GetProperty("components").EnumerateArray().ToList();
        Assert.Equal(2, components.Count);
        Assert.Equal(3, components.Sum(c => c.GetProperty("quantity").GetInt32()));
        Assert.Contains(components, c => c.GetProperty("productSlug").GetString() == a.Slug && c.GetProperty("quantity").GetInt32() == 2);

        await using var scope = Factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var composition = await dispatcher.GetResultAsync(new GetBundleCompositionQuery
        { BundleSkuId = bundle.SkuId, UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid() });

        Assert.NotNull(composition.Result);
        Assert.Equal(2, composition.Result!.Count);
        Assert.Contains(composition.Result, c => c.ComponentSkuId == a.SkuId && c.Quantity == 2);
        Assert.Contains(composition.Result, c => c.ComponentSkuId == b.SkuId && c.Quantity == 1);

        var notABundle = await dispatcher.GetResultAsync(new GetBundleCompositionQuery
        { BundleSkuId = a.SkuId, UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid() });
        Assert.Null(notABundle.Result);
    }

    [Fact]
    public async Task I_F5_AnyComponentUnavailable_MakesTheBundleUnavailable_InDetailAndCatalogue()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("BundleStock");
        var a = await CreateProductAsync(staff, brand, "Litter Scoop", price: 300m);
        var b = await CreateProductAsync(staff, brand, "Cat Toy", price: 150m);
        var bundle = await CreateBundleAsync(staff, brand, "Starter Kit", [(a.SkuId, 2), (b.SkuId, 1)]);

        async Task<(string Detail, string Catalogue)> AvailabilityAsync()
        {
            var detail = await (await CreateClient().GetAsync($"/api/shop/products/{bundle.Slug}")).Content.ReadFromJsonAsync<JsonElement>(Json);
            var list = await (await CreateClient().GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&q=starter")).Content.ReadFromJsonAsync<JsonElement>(Json);
            var item = list.GetProperty("items").EnumerateArray().First(i => i.GetProperty("productId").GetGuid() == bundle.ProductId);
            return (detail.GetProperty("skus")[0].GetProperty("availability").GetString()!, item.GetProperty("availability").GetString()!);
        }

        Assert.Equal(("InStock", "InStock"), await AvailabilityAsync());

        await SetSkuOutOfStockAsync(b.SkuId);
        Assert.Equal(("OutOfStock", "OutOfStock"), await AvailabilityAsync());

        await SetSkuInStockAsync(b.SkuId);
        Assert.Equal(("InStock", "InStock"), await AvailabilityAsync());
    }

    [Fact]
    public async Task I_F6_BundleIncludingAPrescriptionProduct_Is400_InvalidBundleComponent()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("BundleRx");
        var rx = await CreateProductAsync(staff, brand, "Renal Diet", "CatFood", prescription: true);
        var ok = await CreateProductAsync(staff, brand, "Cat Toy");
        var bundle = await CreateBundleAsync(staff, brand, "Kit", [(ok.SkuId, 2)], publish: false);

        var response = await staff.PutAsJsonAsync($"/api/staff/shop/products/{bundle.ProductId}/bundle-components", new
        {
            expectedVersion = await ProductVersionAsync(staff, bundle.ProductId),
            components = new[] { new { componentSkuId = rx.SkuId, quantity = 1 }, new { componentSkuId = ok.SkuId, quantity = 1 } }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("InvalidBundleComponent", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Bundle_StaleVersion_Is409_AndReceptionCannotEdit()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("BundleAuth");
        var a = await CreateProductAsync(staff, brand, "Scoop");
        var bundle = await CreateBundleAsync(staff, brand, "Kit", [(a.SkuId, 2)], publish: false);

        var stale = await staff.PutAsJsonAsync($"/api/staff/shop/products/{bundle.ProductId}/bundle-components", new
        { expectedVersion = 1, components = new[] { new { componentSkuId = a.SkuId, quantity = 2 } } }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var (_, mobile, password) = await SeedStaffAsync("Reception");
        var reception = await SignInStaffAsync(mobile, password);
        var forbidden = await reception.PutAsJsonAsync($"/api/staff/shop/products/{bundle.ProductId}/bundle-components", new
        { expectedVersion = await ProductVersionAsync(staff, bundle.ProductId), components = new[] { new { componentSkuId = a.SkuId, quantity = 2 } } }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task Bundle_StaffDetailListsComponents_AndFlagsADiscontinuedComponent()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("BundleStaff");
        var a = await CreateProductAsync(staff, brand, "Scoop");
        var bundle = await CreateBundleAsync(staff, brand, "Kit", [(a.SkuId, 2)], publish: false);

        var detail = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{bundle.ProductId}", Json);
        var components = detail.GetProperty("bundleComponents").EnumerateArray().ToList();
        Assert.Single(components);
        Assert.Equal(a.SkuCode, components[0].GetProperty("skuCode").GetString());
        Assert.True(components[0].GetProperty("isOnSale").GetBoolean());

        // Archive the component's product: the bundle's checklist and the component flag both show it.
        var archive = await staff.PostAsJsonAsync($"/api/staff/shop/products/{a.ProductId}/archive", new { expectedVersion = await ProductVersionAsync(staff, a.ProductId) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var after = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{bundle.ProductId}", Json);
        Assert.False(after.GetProperty("bundleComponents")[0].GetProperty("isOnSale").GetBoolean());
        var checklist = after.GetProperty("publishChecklist").EnumerateArray().First(i => i.GetProperty("item").GetString() == "BundleComposition");
        Assert.False(checklist.GetProperty("isMet").GetBoolean());
    }

    // ── I-F7 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F7_Recommendations_ExcludeOutOfStockAndPrescriptionProductsTheCallerCannotBuy()
    {
        var (gateFactory, authorisations) = Factory.WithControllableDietGate();
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Recs");

        var main = await CreateProductAsync(staff, brand, "Main Adult Food", "CatFood", lifeStage: "Adult");
        var inStock = await CreateProductAsync(staff, brand, "Curated In Stock", price: 200m);
        var outOfStock = await CreateProductAsync(staff, brand, "Curated Out Of Stock", price: 200m);
        var rxLinked = await CreateProductAsync(staff, brand, "Curated Rx Food", "CatFood", prescription: true, lifeStage: "Adult");
        var upSell = await CreateProductAsync(staff, brand, "Up Sell Premium", price: 900m);
        var kittenFood = await CreateProductAsync(staff, brand, "Kitten Crunch", "CatFood", lifeStage: "Kitten");
        var adultFood = await CreateProductAsync(staff, brand, "Adult Crunch", "CatFood", lifeStage: "Adult");
        var lifeStageUnits = NextUnitsSold();
        await PushUnitsSoldAsync(adultFood.ProductId, lifeStageUnits);
        await PushUnitsSoldAsync(kittenFood.ProductId, lifeStageUnits);
        await PushUnitsSoldAsync(main.ProductId, lifeStageUnits);
        await SetSkuOutOfStockAsync(outOfStock.SkuId);

        var set = await staff.PutAsJsonAsync($"/api/staff/shop/products/{main.ProductId}/related-products", new
        {
            expectedVersion = await ProductVersionAsync(staff, main.ProductId),
            links = new object[]
            {
                new { relatedProductId = inStock.ProductId, kind = "FrequentlyBoughtTogether", sortOrder = 0 },
                new { relatedProductId = outOfStock.ProductId, kind = "FrequentlyBoughtTogether", sortOrder = 1 },
                new { relatedProductId = rxLinked.ProductId, kind = "FrequentlyBoughtTogether", sortOrder = 2 },
                new { relatedProductId = upSell.ProductId, kind = "UpSell", sortOrder = 0 }
            }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        static List<Guid> Ids(JsonElement block) => block.EnumerateArray().Select(i => i.GetProperty("productId").GetGuid()).ToList();

        // Guest: curated out-of-stock and the Rx item are excluded; the in-stock one stays, in order.
        var guestResponse = await CreateClient().GetAsync($"/api/shop/products/{main.Slug}/recommendations");
        Assert.Equal("public, max-age=60", guestResponse.Headers.CacheControl?.ToString());
        var guest = await guestResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var guestFbt = Ids(guest.GetProperty("frequentlyBoughtTogether"));
        Assert.Equal([inStock.ProductId], guestFbt);
        Assert.Equal([upSell.ProductId], Ids(guest.GetProperty("upSell")));
        Assert.Equal("ThisProduct", guest.GetProperty("lifeStage").GetProperty("basis").GetString());
        var guestLifeStage = Ids(guest.GetProperty("lifeStage").GetProperty("items"));
        Assert.Contains(adultFood.ProductId, guestLifeStage);
        Assert.DoesNotContain(kittenFood.ProductId, guestLifeStage);
        Assert.DoesNotContain(main.ProductId, guestLifeStage);
        Assert.DoesNotContain(rxLinked.ProductId, guestLifeStage);

        // A customer with a kitten and no authorisation: the same exclusions, basis YourCats.
        var (customer, customerId) = await RegisterAndSignInCustomerAsync(gateFactory);
        var kitten = await SeedCatAsync(customerId, "Tiny", DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-4));

        var unauthorisedResponse = await customer.GetAsync($"/api/shop/products/{main.Slug}/recommendations");
        Assert.True(unauthorisedResponse.Headers.CacheControl is { NoStore: true, Private: true });
        var unauthorised = await unauthorisedResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal([inStock.ProductId], Ids(unauthorised.GetProperty("frequentlyBoughtTogether")));
        Assert.Equal("YourCats", unauthorised.GetProperty("lifeStage").GetProperty("basis").GetString());
        var kittenItems = Ids(unauthorised.GetProperty("lifeStage").GetProperty("items"));
        Assert.Contains(kittenFood.ProductId, kittenItems);
        Assert.DoesNotContain(adultFood.ProductId, kittenItems);

        // Once the customer has an authorisation for the Rx food, it is included (still not the out-of-stock one).
        authorisations.Authorise(customerId, kitten, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));
        var authorisedBody = await (await customer.GetAsync($"/api/shop/products/{main.Slug}/recommendations")).Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal([inStock.ProductId, rxLinked.ProductId], Ids(authorisedBody.GetProperty("frequentlyBoughtTogether")));
    }

    [Fact]
    public async Task Recommendations_UnknownOrDraftSlug_Is404()
    {
        var staff = await SignInAsShopManagerAsync();
        var draft = await CreateProductAsync(staff, NewBrand("Draft"), "Draft Item", publish: false);

        Assert.Equal(HttpStatusCode.NotFound, (await CreateClient().GetAsync($"/api/shop/products/{draft.Slug}/recommendations")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateClient().GetAsync($"/api/shop/products/{UniqueSlug("zz")}/recommendations")).StatusCode);
    }

    [Fact]
    public async Task RelatedProducts_NonexistentTargetOrSelfLink_Is400()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Related"), "Scratcher");

        var missing = await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/related-products", new
        {
            expectedVersion = await ProductVersionAsync(staff, product.ProductId),
            links = new[] { new { relatedProductId = Guid.NewGuid(), kind = "UpSell", sortOrder = 0 } }
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("RelatedProductInvalid", await missing.Content.ReadAsStringAsync());

        var self = await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/related-products", new
        {
            expectedVersion = await ProductVersionAsync(staff, product.ProductId),
            links = new[] { new { relatedProductId = product.ProductId, kind = "UpSell", sortOrder = 0 } }
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
    }

    // ── I-F8 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_F8_ShippedFailClosedGate_NoPrescriptionProductIsPurchasableByAnyCustomer()
    {
        // The base factory: IDietAuthorisationGate is PharmacyModule's registered stand-in, which
        // denies everyone until HO-PHM-001 replaces it.
        var staff = await SignInAsShopManagerAsync();
        var rx = await CreateProductAsync(staff, NewBrand("RxClosed"), "Therapeutic Diet", "CatFood", prescription: true);
        var (customer, customerId) = await RegisterAndSignInCustomerAsync();
        await SeedCatAsync(customerId);

        var sku = Sku(await JsonOkAsync(await customer.GetAsync($"/api/customers/me/shop/products/{rx.Slug}/purchasability")));
        Assert.False(sku.GetProperty("purchasable").GetBoolean());
        Assert.Equal("PrescriptionAuthorisationRequired", sku.GetProperty("purchaseBlockReason").GetString());
        Assert.Equal(0, sku.GetProperty("eligibleCats").GetArrayLength());

        var (second, _) = await RegisterAndSignInCustomerAsync();
        var secondSku = Sku(await JsonOkAsync(await second.GetAsync($"/api/customers/me/shop/products/{rx.Slug}/purchasability")));
        Assert.False(secondSku.GetProperty("purchasable").GetBoolean());
    }

    // ── Contract queries (§8.12) ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ContractQueries_SaleFactsSnapshotThePriceAtAsOf_CatalogueFactsCarryOriginAndExpiryTracking()
    {
        var staff = await SignInAsShopManagerAsync();
        var food = await CreateProductAsync(staff, NewBrand("Facts"), "Origin Food", "CatFood", price: 1200m, countryCode: "FR");

        await using var scope = Factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var ctx = new UserContext(Guid.NewGuid(), Guid.NewGuid());

        var sale = await dispatcher.GetResultAsync(new GetSkuSaleFactsQuery
        { SkuIds = [food.SkuId, Guid.NewGuid()], AsOf = DateTimeOffset.UtcNow.AddMinutes(1), UserContext = ctx, CorrelationId = Guid.NewGuid() });
        var fact = Assert.Single(sale.Result);
        Assert.Equal(food.SkuId, fact.SkuId);
        Assert.Equal("1200.00", fact.Price!.Amount);
        Assert.Equal("Active", fact.ProductStatus);

        var before = await dispatcher.GetResultAsync(new GetSkuSaleFactsQuery
        { SkuIds = [food.SkuId], AsOf = DateTimeOffset.UtcNow.AddDays(-1), UserContext = ctx, CorrelationId = Guid.NewGuid() });
        Assert.Null(Assert.Single(before.Result).Price);

        var catalogue = await dispatcher.GetResultAsync(new GetSkuCatalogueFactsQuery { SkuIds = [food.SkuId], UserContext = ctx, CorrelationId = Guid.NewGuid() });
        var cf = Assert.Single(catalogue.Result);
        Assert.True(cf.IsFood);
        Assert.True(cf.IsExpiryTracked);
        Assert.Equal("FR", cf.CountryOfOriginCode);
    }
}
