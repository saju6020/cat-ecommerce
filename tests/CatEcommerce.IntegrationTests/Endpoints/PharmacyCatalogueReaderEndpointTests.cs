using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.DomainServices;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate (ARCHITECTURE re-check hand-off) -- Pharmacy's <see cref="ICatalogueReader"/> now reads only Shop's <c>SkuRows()</c> projection, which is one row per SKU.
/// These tests pin what that changes: a product with no SKU is invisible, and a search <c>take</c> limits products, not SKU rows.
/// </summary>
public sealed class PharmacyCatalogueReaderEndpointTests(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    private async Task<T> WithReaderAsync<T>(Func<ICatalogueReader, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ICatalogueReader>());
    }

    private static async Task<Guid> CreateSkuLessProductAsync(HttpClient admin, string? name = null, bool prescriptionDiet = false)
    {
        var taxCode = "TAX" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var taxClass = await admin.PostAsJsonAsync("/api/admin/tax-classes", new { code = taxCode, name = "Standard " + taxCode }, Json);
        Assert.True(taxClass.StatusCode == HttpStatusCode.Created, await taxClass.Content.ReadAsStringAsync());
        var taxClassId = (await taxClass.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("taxClassId").GetGuid();
        var create = await admin.PostAsJsonAsync("/api/staff/shop/products",
            new { kind = "Standard", name = name ?? "No sku " + Guid.NewGuid().ToString("N")[..8], brand = "Brand-x", category = prescriptionDiet ? "CatFood" : "HealthAndGrooming", taxClassId, slug = "p-" + Guid.NewGuid().ToString("N") }, Json);
        Assert.True(create.StatusCode == HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var productId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("productId").GetGuid();
        if (!prescriptionDiet) return productId;

        async Task<int> VersionAsync() => (await admin.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json)).GetProperty("version").GetInt32();
        var details = await admin.PutAsJsonAsync($"/api/staff/shop/products/{productId}", new
        {
            expectedVersion = await VersionAsync(), name = name!, brand = "Brand-x", category = "CatFood", taxClassId, countryOfOriginCode = "DE", isExpiryTracked = true, listedOnStorefront = false
        }, Json);
        Assert.True(details.StatusCode == HttpStatusCode.NoContent, await details.Content.ReadAsStringAsync());
        var food = await admin.PutAsJsonAsync($"/api/staff/shop/products/{productId}/food", new { expectedVersion = await VersionAsync(), lifeStage = "Adult" }, Json);
        Assert.True(food.StatusCode == HttpStatusCode.NoContent, await food.Content.ReadAsStringAsync());
        var rx = await admin.PutAsJsonAsync($"/api/staff/shop/products/{productId}/prescription", new { expectedVersion = await VersionAsync(), prescriptionRequired = true }, Json);
        Assert.True(rx.StatusCode == HttpStatusCode.NoContent, await rx.Content.ReadAsStringAsync());
        return productId;
    }

    private static async Task PublishAsync(HttpClient admin, Guid productId)
    {
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        var published = await admin.PostAsJsonAsync($"/api/staff/shop/products/{productId}/publish", new { expectedVersion = detail.GetProperty("version").GetInt32() }, Json);
        Assert.True(published.StatusCode == HttpStatusCode.NoContent, await published.Content.ReadAsStringAsync());
    }

    private static async Task<Guid> AddSkuAsync(HttpClient admin, Guid productId, string size, DateTimeOffset? priceFrom = null)
    {
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/staff/shop/products/{productId}", Json);
        var add = await admin.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
            new { expectedVersion = detail.GetProperty("version").GetInt32(), code = "SKU" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant(), sizeOrWeight = size }, Json);
        Assert.True(add.StatusCode == HttpStatusCode.Created, await add.Content.ReadAsStringAsync());
        var skuId = (await add.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("skuId").GetGuid();
        if (priceFrom is { } from)
        {
            var priced = await admin.PostAsJsonAsync($"/api/staff/shop/skus/{skuId}/prices", new { amount = 100m, currencyCode = "BDT", effectiveFrom = from }, Json);
            Assert.True(priced.StatusCode == HttpStatusCode.Created, await priced.Content.ReadAsStringAsync());
        }
        return skuId;
    }

    [Fact]
    public async Task AProductWithNoSku_IsInvisibleToTheReader_GetProductReturnsNull_AndOnceItHasSkusItIsReturnedWithAllOfThem()
    {
        var world = await SeedPharmacyWorldAsync();
        var productId = await CreateSkuLessProductAsync(world.Admin);

        Assert.Null(await WithReaderAsync(r => r.GetProductAsync(productId, CancellationToken.None)));

        var first = await AddSkuAsync(world.Admin, productId, "1kg");
        var second = await AddSkuAsync(world.Admin, productId, "2kg");
        var product = await WithReaderAsync(r => r.GetProductAsync(productId, CancellationToken.None));

        Assert.NotNull(product);
        Assert.Equal(productId, product!.ProductId);
        Assert.Equal(new[] { first, second }.OrderBy(g => g), product.SkuIds.OrderBy(g => g));
        Assert.Equal(productId, (await WithReaderAsync(r => r.FindProductBySkuAsync(second, CancellationToken.None)))!.ProductId);
    }

    [Fact]
    public async Task SearchTake_LimitsProducts_NotSkuRows_AndEachReturnedProductKeepsAllItsSkus()
    {
        var world = await SeedPharmacyWorldAsync();
        var token = "Zqx" + Guid.NewGuid().ToString("N")[..10];
        var products = new List<(Guid ProductId, Guid SkuOne)>();
        for (var i = 0; i < 3; i++)
        {
            var productId = await CreateSkuLessProductAsync(world.Admin, $"{token} diet {i}", prescriptionDiet: true);
            var now = world.Clinic.Clock.GetUtcNow();
            var one = await AddSkuAsync(world.Admin, productId, "1kg", now); // two SKUs each: six SKU rows, three products
            await AddSkuAsync(world.Admin, productId, "2kg", now);
            await PublishAsync(world.Admin, productId);
            products.Add((productId, one));
        }

        var all = await WithReaderAsync(r => r.SearchPrescriptionProductsAsync(token, 10, CancellationToken.None));
        Assert.Equal(3, all.Count);
        Assert.All(all, p => Assert.Equal(2, p.SkuIds.Count));

        var limited = await WithReaderAsync(r => r.SearchPrescriptionProductsAsync(token, 2, CancellationToken.None));
        Assert.Equal(2, limited.Count);
        Assert.All(limited, p => Assert.Equal(2, p.SkuIds.Count));
        Assert.Equal(new[] { $"{token} diet 0", $"{token} diet 1" }, limited.Select(p => p.Name));
    }
}
