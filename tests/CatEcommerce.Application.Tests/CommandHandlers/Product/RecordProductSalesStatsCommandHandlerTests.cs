using CatEcommerce.Application.CommandHandlers.Product;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Product;

public sealed class RecordProductSalesStatsCommandHandlerTests
{
    private static UserContext SystemContext() => new(Guid.NewGuid(), Guid.NewGuid());

    private static ProductAggregate SeedProduct()
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Scratch Post", "Acme", ProductCategory.ToysAndFurniture,
            null, Guid.NewGuid(), "scratch-post", null, null, SystemContext(), Guid.NewGuid()));
        return product;
    }

    [Fact]
    public async Task Record_UnknownProduct_ReturnsProductNotFound()
    {
        var productRepository = new FakeAggregateRootRepository<ProductAggregate>();
        var statsRepository = new FakeAggregateRootRepository<ProductSalesStatsAggregate>();
        var handler = new RecordProductSalesStatsCommandHandler(NullLogger<RecordProductSalesStatsCommandHandler>.Instance, productRepository, statsRepository, TimeProvider.System);

        var response = await handler.HandleAsync(new RecordProductSalesStatsCommand
        { ProductId = Guid.NewGuid(), UnitsSold90d = 5, AsOf = DateTimeOffset.UtcNow, UserContext = SystemContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.ProductNotFound);
    }

    [Fact]
    public async Task Record_FirstPush_Saves()
    {
        var product = SeedProduct();
        var productRepository = new FakeAggregateRootRepository<ProductAggregate>();
        productRepository.Seed(product);
        var statsRepository = new FakeAggregateRootRepository<ProductSalesStatsAggregate>();
        var handler = new RecordProductSalesStatsCommandHandler(NullLogger<RecordProductSalesStatsCommandHandler>.Instance, productRepository, statsRepository, TimeProvider.System);

        var response = await handler.HandleAsync(new RecordProductSalesStatsCommand
        { ProductId = product.Id, UnitsSold90d = 5, AsOf = DateTimeOffset.UtcNow, UserContext = SystemContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        Assert.True(response.IsSuccess);
        Assert.Single(statsRepository.Saved);
    }

    [Fact]
    public async Task Record_StalePush_NoOp_DoesNotUpdate()
    {
        var product = SeedProduct();
        var productRepository = new FakeAggregateRootRepository<ProductAggregate>();
        productRepository.Seed(product);
        var statsRepository = new FakeAggregateRootRepository<ProductSalesStatsAggregate>();

        var now = DateTimeOffset.UtcNow;
        var existing = new ProductSalesStatsAggregate(product.Id);
        existing.Apply(new ProductSalesStatsApplyDto(10, now, now, SystemContext(), Guid.NewGuid()));
        statsRepository.Seed(existing);

        var handler = new RecordProductSalesStatsCommandHandler(NullLogger<RecordProductSalesStatsCommandHandler>.Instance, productRepository, statsRepository, TimeProvider.System);
        var response = await handler.HandleAsync(new RecordProductSalesStatsCommand
        { ProductId = product.Id, UnitsSold90d = 999, AsOf = now.AddMinutes(-1), UserContext = SystemContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        Assert.True(response.IsSuccess);
        Assert.Empty(statsRepository.Updated);
        Assert.Equal(10, existing.UnitsSold90d);
    }
}
