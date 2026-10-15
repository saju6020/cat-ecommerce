using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Read.Queries.Product;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SHP-001-FU3 -- optional per-SKU shipping weight (owner decision 2026-10-09): written by the
/// staff add/update SKU endpoints, returned in the staff detail, and published on the SKU sale-facts
/// contract Ordering reads.</summary>
public sealed class ShopSkuShippingWeightEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private async Task<(HttpClient Staff, Guid ProductId)> CreateProductAsync()
    {
        var staff = await SignInAsShopManagerAsync();
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));
        var create = await staff.PostAsJsonAsync("/api/staff/shop/products",
            new { kind = "Standard", name = "Weighed " + Guid.NewGuid().ToString("N")[..8], brand = "Acme", category = "HealthAndGrooming", taxClassId, slug = UniqueSlug("weighed") }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (staff, (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid());
    }

    private async Task<JsonElement> StaffSkuAsync(HttpClient staff, Guid productId, Guid skuId)
    {
        var body = await staff.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        return body.GetProperty("skus").EnumerateArray().Single(s => s.GetProperty("skuId").GetGuid() == skuId);
    }

    [Fact]
    public async Task FU3_T1_AddUpdateAndRead_ShippingWeightRoundTripsAndInvalidIsRefused()
    {
        var (staff, productId) = await CreateProductAsync();

        var add = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
            new { expectedVersion = await ProductVersionAsync(staff, productId), code = UniqueCode("W"), shippingWeightGrams = 2500 }, Json);
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var skuId = (await add.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();
        Assert.Equal(2500, (await StaffSkuAsync(staff, productId, skuId)).GetProperty("shippingWeightGrams").GetInt32());

        var update = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/skus/{skuId}",
            new { expectedVersion = await ProductVersionAsync(staff, productId), shippingWeightGrams = 750 }, Json);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal(750, (await StaffSkuAsync(staff, productId, skuId)).GetProperty("shippingWeightGrams").GetInt32());

        var invalid = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/skus/{skuId}",
            new { expectedVersion = await ProductVersionAsync(staff, productId), shippingWeightGrams = 0 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var tooHeavy = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/skus/{skuId}",
            new { expectedVersion = await ProductVersionAsync(staff, productId), shippingWeightGrams = 100_001 }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, tooHeavy.StatusCode);
        Assert.Equal(750, (await StaffSkuAsync(staff, productId, skuId)).GetProperty("shippingWeightGrams").GetInt32());

        var clear = await staff.PutAsJsonAsync($"/api/staff/shop/products/{productId}/skus/{skuId}",
            new { expectedVersion = await ProductVersionAsync(staff, productId) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, clear.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await StaffSkuAsync(staff, productId, skuId)).GetProperty("shippingWeightGrams").ValueKind);
    }

    [Fact]
    public async Task FU3_T2_PublishedSaleFacts_CarryTheShippingWeight_AndNullWhenUnset()
    {
        var (staff, productId) = await CreateProductAsync();
        var weighed = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
            new { expectedVersion = await ProductVersionAsync(staff, productId), code = UniqueCode("WA"), sizeOrWeight = "Small", shippingWeightGrams = 1800 }, Json);
        var weighedId = (await weighed.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();
        var plain = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
            new { expectedVersion = await ProductVersionAsync(staff, productId), code = UniqueCode("WB"), sizeOrWeight = "Large" }, Json);
        var plainId = (await plain.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();

        await using var scope = Factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var facts = await dispatcher.GetResultAsync(new GetSkuSaleFactsQuery
        {
            SkuIds = [weighedId, plainId], AsOf = DateTimeOffset.UtcNow.AddMinutes(1),
            UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(1800, facts.Result!.Single(f => f.SkuId == weighedId).ShippingWeightGrams);
        Assert.Null(facts.Result!.Single(f => f.SkuId == plainId).ShippingWeightGrams);
    }
}
