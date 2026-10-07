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

/// <summary>Rows 29, 31, 32, 35 -- price bound, category change, AddSku on an Active product, sales-stats AsOf.</summary>
public sealed partial class ShopBacklogFixesEndpointTests
{
    // ── Row 29: an oversized price is a 400 ─────────────────────────────────────────────────────

    [Fact]
    public async Task Row29_SchedulingAnAmountBeyondNumeric12_2_Is400_Not500()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("BigPrice"), "Big Price");

        var response = await staff.PostAsJsonAsync($"/api/staff/shop/skus/{product.SkuId}/prices",
            new { amount = 10_000_000_000m, currencyCode = "BDT", effectiveFrom = DateTimeOffset.UtcNow.AddDays(1) }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Row 31: a Draft category change away from CatFood ───────────────────────────────────────

    [Fact]
    public async Task Row31_DraftFoodProduct_MovedToANonFoodCategory_LosesThePrescriptionFlag()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("LeaveFood"), "Leaves Food", "CatFood", prescription: true, publish: false);
        var detail = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{product.ProductId}", Json);
        Assert.True(detail.GetProperty("prescriptionRequired").GetBoolean());
        var taxClassId = detail.GetProperty("taxClassId").GetGuid();

        var update = await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}", new
        {
            expectedVersion = detail.GetProperty("version").GetInt32(), name = "Leaves Food", brand = "Acme", category = "ToysAndFurniture", taxClassId
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var after = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{product.ProductId}", Json);
        Assert.False(after.GetProperty("prescriptionRequired").GetBoolean());
    }

    // ── Row 32: AddSku on an Active product ─────────────────────────────────────────────────────

    [Fact]
    public async Task Row32_AddingASkuToAnActiveProduct_CreatesItInactive_AndActivatingNeedsAPriceNow()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("ActiveAdd"), "Active Add");

        // With two SKUs every SKU needs a variant attribute, so the first one gets one before the second is added.
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PutAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/skus/{product.SkuId}",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId), sizeOrWeight = "Small" }, Json)).StatusCode);

        var added = await staff.PostAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/skus",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId), code = UniqueCode("ADD"), sizeOrWeight = "Large" }, Json);
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var newSkuId = (await added.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();

        async Task<string> StatusOfAsync(Guid skuId) => (await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{product.ProductId}", Json))
            .GetProperty("skus").EnumerateArray().Single(k => k.GetProperty("skuId").GetGuid() == skuId).GetProperty("status").GetString()!;

        Assert.Equal("Discontinued", await StatusOfAsync(newSkuId));
        var publicDetail = await JsonOkAsync(await CreateClient().GetAsync($"/api/shop/products/{product.Slug}"));
        Assert.DoesNotContain(publicDetail.GetProperty("skus").EnumerateArray(), k => k.GetProperty("skuId").GetGuid() == newSkuId);

        // Activating it with no price is refused; with a price effective now it works.
        var refused = await staff.PostAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/skus/{newSkuId}/reactivate",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId) }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("ProductNotPublishable", await refused.Content.ReadAsStringAsync());

        await SchedulePriceAsync(staff, newSkuId, 650m);
        var activated = await staff.PostAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/skus/{newSkuId}/reactivate",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, activated.StatusCode);
        Assert.Equal("Active", await StatusOfAsync(newSkuId));
    }

    // ── Row 35: a far-future AsOf does not freeze later pushes ──────────────────────────────────

    [Fact]
    public async Task Row35_AFarFutureAsOf_IsRefused_AndALaterRealPushStillApplies()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("AsOf"), "AsOf Product");

        await using var scope = Factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        CatEcommerce.Application.Commands.Product.RecordProductSalesStatsCommand Push(int units, DateTimeOffset asOf) => new()
        { ProductId = product.ProductId, UnitsSold90d = units, AsOf = asOf, UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid(), ValidateCommand = true };

        var refused = await dispatcher.SendAsync(Push(7, DateTimeOffset.UtcNow.AddYears(30)));
        Assert.False(refused.IsSuccess);
        Assert.Contains(refused.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.InvalidStatsAsOf);

        Assert.True((await dispatcher.SendAsync(Push(11, DateTimeOffset.UtcNow))).IsSuccess);
        await using var readScope = Factory.Services.CreateAsyncScope();
        var db = readScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        Assert.Equal(11, (await db.ProductSalesStats.AsNoTracking().SingleAsync(s => s.Id == product.ProductId)).UnitsSold90d);
    }
}
