using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.DomainServices;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 owner decision 7 / CONTRACTS #25 -- Ordering reads a SKU's shipping weight through Shop's published projection (<c>SkuRows()</c>), so the delivery
/// weight bands see the weight staff set through HO-SHP-001-FU3's endpoints. Real Shop endpoints, real <see cref="ICatalogueLookup"/>.
/// </summary>
public sealed class OrderingCatalogueShippingWeightEndpointTests(OrderingApiFactory factory) : OrderingIntegrationTestBase(factory)
{
    [Fact]
    public async Task TheCatalogueLookup_CarriesTheShippingWeightShopPublished_AndNullWhenNoneIsSet()
    {
        var context = await CreateContextAsync();
        var heavy = await CreateSkuAsync(context, price: 500m, stock: 5m, onStorefront: true);
        var unweighed = await CreateSkuAsync(context, price: 200m, stock: 5m, onStorefront: true);

        var update = await context.Admin.PutAsJsonAsync($"/api/staff/shop/products/{heavy.ProductId}/skus/{heavy.SkuId}",
            new { expectedVersion = await ProductVersionAsync(context.Admin, heavy.ProductId), shippingWeightGrams = 2500 }, Json);
        await ExpectAsync(HttpStatusCode.NoContent, update);

        await using var scope = Factory.Services.CreateAsyncScope();
        var lookup = scope.ServiceProvider.GetRequiredService<ICatalogueLookup>();
        var skus = await lookup.GetSkusAsync([heavy.SkuId, unweighed.SkuId], Now, CancellationToken.None);

        Assert.Equal(2500, skus[heavy.SkuId].ShippingWeightGrams);
        Assert.Null(skus[unweighed.SkuId].ShippingWeightGrams);
    }
}
