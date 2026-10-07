using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SHP-001 §16 Phase C -- RecordProductSalesStatsCommand is inbound-only (no HTTP
/// route); dispatched here exactly as HO-ORD-001 will (section 12).</summary>
public sealed class RecordProductSalesStatsTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    [Fact]
    public async Task Record_ThenStalePush_KeepsTheNewerFigure()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var productRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<ProductAggregate>>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Scratch Post", "Acme", ProductCategory.ToysAndFurniture,
            null, Guid.NewGuid(), UniqueSlug("sales-stats"), null, null, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await productRepository.SaveAsync(product, EventPublishOption.InMemoryPublishOnly);

        var now = DateTimeOffset.UtcNow;
        var first = await dispatcher.SendAsync(new RecordProductSalesStatsCommand
        { ProductId = product.Id, UnitsSold90d = 12, AsOf = now, UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        Assert.True(first.IsSuccess);

        var stale = await dispatcher.SendAsync(new RecordProductSalesStatsCommand
        { ProductId = product.Id, UnitsSold90d = 999, AsOf = now.AddMinutes(-5), UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()), CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        Assert.True(stale.IsSuccess);

        var stats = await db.Set<ProductSalesStatsAggregate>().AsNoTracking().FirstAsync(s => s.Id == product.Id);
        Assert.Equal(12, stats.UnitsSold90d);
    }
}
