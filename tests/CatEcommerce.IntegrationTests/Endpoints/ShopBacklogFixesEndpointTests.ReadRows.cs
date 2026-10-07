using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>Rows 15, 19, 30, 41 -- projection edge cases, eligibility visibility, filter validation, price-filter clock.</summary>
public sealed partial class ShopBacklogFixesEndpointTests
{
    // ── Row 15: a product with no SKUs, and the food block's presence check ─────────────────────

    [Fact]
    public async Task Row15_ALiveProductWithNoSkus_IsNotA404ForPurchasabilityOrReviewEligibility()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("NoSku"), "Skuless Toy");
        await ExecuteSqlAsync($"DELETE FROM product.sku WHERE product_id = {product.ProductId}");
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        var purchasability = await customer.GetAsync($"/api/customers/me/shop/products/{product.Slug}/purchasability");
        Assert.Equal(HttpStatusCode.OK, purchasability.StatusCode);
        Assert.Equal(0, (await JsonOkAsync(purchasability)).GetProperty("skus").GetArrayLength());

        var eligibility = await customer.GetAsync($"/api/customers/me/shop/products/{product.Slug}/review-eligibility");
        Assert.Equal(HttpStatusCode.OK, eligibility.StatusCode);
        Assert.False((await JsonOkAsync(eligibility)).GetProperty("canReview").GetBoolean());
    }

    [Fact]
    public async Task Row15_FoodBlock_AppearsWhenOnlyAGuaranteedAnalysisColumnOtherThanProteinIsSet()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("FatOnly"), "Fat Only Food", "CatFood", lifeStage: "Adult");
        // Every other food column cleared: only the fat figure remains (a state the old protein-only check missed).
        await ExecuteSqlAsync($@"UPDATE product.product SET food_life_stage = NULL, food_protein_source = NULL, food_ingredient_list = NULL,
            food_manufacturer = NULL, food_feeding_guide = NULL, food_storage_instructions = NULL,
            food_analysis_protein_percent = NULL, food_analysis_fibre_percent = NULL, food_analysis_moisture_percent = NULL,
            food_analysis_fat_percent = 12.5 WHERE ""Id"" = {product.ProductId}");

        var detail = await JsonOkAsync(await CreateClient().GetAsync($"/api/shop/products/{product.Slug}"));

        Assert.Equal(JsonValueKind.Object, detail.GetProperty("food").ValueKind);
    }

    // ── Row 19: review eligibility does not reveal unreleased products ──────────────────────────

    [Fact]
    public async Task Row19_ReviewEligibility_ForADraftOrUnlistedSlug_IsA404_LikeAnUnknownOne()
    {
        var staff = await SignInAsShopManagerAsync();
        var draft = await CreateProductAsync(staff, NewBrand("EligDraft"), "Draft Eligibility", publish: false);
        var unlisted = await CreateProductAsync(staff, NewBrand("EligHidden"), "Unlisted Eligibility");
        await UnlistAsync(staff, unlisted.ProductId);
        var live = await CreateProductAsync(staff, NewBrand("EligLive"), "Live Eligibility");
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/customers/me/shop/products/{draft.Slug}/review-eligibility")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/customers/me/shop/products/{unlisted.Slug}/review-eligibility")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync($"/api/customers/me/shop/products/{UniqueSlug("nope")}/review-eligibility")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await customer.GetAsync($"/api/customers/me/shop/products/{live.Slug}/review-eligibility")).StatusCode);
    }

    // ── Row 30: unknown filter values are refused consistently ──────────────────────────────────

    [Theory]
    [InlineData("category=NotACategory", "category")]
    [InlineData("subcategory=NotASubcategory", "subcategory")]
    [InlineData("origin=ZZ9", "origin")]
    [InlineData("lifeStage=Puppy", "lifeStage")]
    [InlineData("protein=Unicorn", "protein")]
    public async Task Row30_UnknownEnumeratedFilter_IsInvalidCatalogueFilter_NamingTheParameter(string filter, string parameter)
    {
        var response = await CreateClient().GetAsync($"/api/shop/products?{filter}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.InvalidCatalogueFilter, body);
        Assert.Contains(parameter, body);
    }

    [Fact]
    public async Task Row30_KnownFilterValues_StillWork()
    {
        var response = await CreateClient().GetAsync("/api/shop/products?category=CatFood&origin=DE&lifeStage=Adult");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // ── Row 41: the price filter and the view share the database clock ──────────────────────────

    [Fact]
    public async Task Row41_PriceFilter_UsesTheDatabaseClock_NotTheApplicationClock()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("DbClock");
        var product = await CreateProductAsync(staff, brand, "Db Clock Product", price: 4321m);

        // An application clock a day BEHIND the database's: under it the price (effective "now" by the real
        // clock) has not started yet, so an application-clock filter drops the product that the view displays.
        var behind = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-1));
        var clockFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(behind)));

        var response = await clockFactory.CreateClient().GetAsync($"/api/shop/products?brand={Uri.EscapeDataString(brand)}&minPrice=4000&maxPrice=5000");
        var body = await JsonOkAsync(response);

        var item = Assert.Single(body.GetProperty("items").EnumerateArray());
        Assert.Equal(product.ProductId, item.GetProperty("productId").GetGuid());
        Assert.Equal("4321.00", item.GetProperty("fromPrice").GetProperty("amount").GetString());
    }
}
