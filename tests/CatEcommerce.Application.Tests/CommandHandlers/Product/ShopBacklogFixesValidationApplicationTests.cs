using CatEcommerce.Application.CommandHandlers.Product;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DomainEventHandlers.Product;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence.Views;
using CatEcommerce.Read.QueryHandlers.Product;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Product;

/// <summary>HO-SHP-001 backlog rows 29, 32 and 35 at handler / validator level.</summary>
public sealed class ShopBacklogFixesValidationApplicationTests
{
    private static UserContext Ctx() => new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["ShopManager"] };

    private static ProductAggregate NewProduct(string slug = "scratch-post")
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Scratch Post", "Acme", ProductCategory.ToysAndFurniture,
            null, Guid.NewGuid(), slug, null, null, Ctx(), Guid.NewGuid()));
        return product;
    }

    private static ProductAggregate ProductWithSku(out Guid skuId, bool publish = false, bool listed = true)
    {
        var product = NewProduct();
        skuId = Guid.NewGuid();
        product.AddSku(new SkuAddDto(skuId, "SP-1", null, null, null, null, null), Ctx(), Guid.NewGuid());
        if (!listed)
            product.UpdateDetails(new ProductDetailsUpdateDto(product.Name, product.Brand, product.Description, product.Category, null, product.Tags,
                product.TaxClassId, null, false, false, [], Ctx(), Guid.NewGuid()));
        if (publish) product.Publish(new PublicationFacts([skuId], true, []), DateTimeOffset.UtcNow, Ctx(), Guid.NewGuid());
        return product;
    }

    // ── Row 29 ──────────────────────────────────────────────────────────────────────────────────

    private static ScheduleSkuPriceCommand PriceCommand(decimal amount) => new()
    {
        SkuPriceId = Guid.NewGuid(), SkuId = Guid.NewGuid(), Amount = amount, CurrencyCode = "BDT", EffectiveFrom = DateTimeOffset.UtcNow,
        UserContext = Ctx(), CorrelationId = Guid.NewGuid()
    };

    [Fact]
    public void ScheduleSkuPriceValidator_AmountAboveNumeric12_2_IsInvalid_NotA500()
    {
        var validator = new ScheduleSkuPriceCommandValidator();

        Assert.True(validator.Validate(PriceCommand(9_999_999_999.99m)).IsValid);

        var tooBig = validator.Validate(PriceCommand(10_000_000_000.00m));
        Assert.False(tooBig.IsValid);
        Assert.Contains(tooBig.Errors, e => e.PropertyName == nameof(ScheduleSkuPriceCommand.Amount));
        Assert.False(validator.Validate(PriceCommand(79_228_162_514_264_337_593_543_950_335m)).IsValid);
    }

    // ── Row 32 (handler wiring: "priced now" comes from the price repository) ───────────────────

    [Fact]
    public async Task ReactivateSku_OnAnActiveProduct_NeedsAPriceEffectiveNow()
    {
        var product = NewProduct();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        product.AddSku(new SkuAddDto(first, "SP-1", null, "Small", null, null, null), Ctx(), Guid.NewGuid());
        product.AddSku(new SkuAddDto(second, "SP-2", null, "Large", null, null, null), Ctx(), Guid.NewGuid());
        product.DiscontinueSku(second, Ctx(), Guid.NewGuid());
        product.Publish(new PublicationFacts([first], true, []), DateTimeOffset.UtcNow, Ctx(), Guid.NewGuid());

        var products = new FakeAggregateRootRepository<ProductAggregate>();
        products.Seed(product);
        var prices = new FakeAggregateRootRepository<SkuPriceAggregate>();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new ReactivateSkuCommandHandler(NullLogger<ReactivateSkuCommandHandler>.Instance, products, prices, clock);

        ReactivateSkuCommand Command() => new()
        { ProductId = product.Id, SkuId = second, ExpectedVersion = product.Version, UserContext = Ctx(), CorrelationId = Guid.NewGuid(), ValidateCommand = true };

        var refused = await handler.HandleAsync(Command());
        Assert.False(refused.IsSuccess);
        Assert.Contains(refused.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.ProductNotPublishable);
        Assert.Empty(products.Updated);

        var price = new SkuPriceAggregate(Guid.NewGuid());
        price.Create(new SkuPriceCreateDto(second, new Money(100m, "BDT"), clock.GetUtcNow(), Guid.NewGuid(), clock.GetUtcNow(), Ctx(), Guid.NewGuid()), "BDT");
        prices.Seed(price);

        var accepted = await handler.HandleAsync(Command());
        Assert.True(accepted.IsSuccess);
        Assert.Single(products.Updated);
    }

    // ── Row 35 ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordSalesStats_FarFutureAsOf_IsRefused_AndALaterRealPushStillApplies()
    {
        var product = NewProduct();
        var products = new FakeAggregateRootRepository<ProductAggregate>();
        products.Seed(product);
        var stats = new FakeAggregateRootRepository<ProductSalesStatsAggregate>();
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var handler = new RecordProductSalesStatsCommandHandler(NullLogger<RecordProductSalesStatsCommandHandler>.Instance, products, stats, clock);

        RecordProductSalesStatsCommand Push(int units, DateTimeOffset asOf) => new()
        { ProductId = product.Id, UnitsSold90d = units, AsOf = asOf, UserContext = Ctx(), CorrelationId = Guid.NewGuid(), ValidateCommand = true };

        var refused = await handler.HandleAsync(Push(5, clock.GetUtcNow().AddYears(40)));
        Assert.False(refused.IsSuccess);
        Assert.Contains(refused.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.InvalidStatsAsOf);
        Assert.Empty(stats.Saved);

        var applied = await handler.HandleAsync(Push(9, clock.GetUtcNow()));
        Assert.True(applied.IsSuccess);
        Assert.Equal(9, Assert.Single(stats.Saved).UnitsSold90d);
    }

    [Fact]
    public async Task RecordSalesStats_NegativeUnits_UsesTheUnitsSoldCode_NotInvalidStockLevel()
    {
        var product = NewProduct();
        var products = new FakeAggregateRootRepository<ProductAggregate>();
        products.Seed(product);
        var handler = new RecordProductSalesStatsCommandHandler(NullLogger<RecordProductSalesStatsCommandHandler>.Instance, products,
            new FakeAggregateRootRepository<ProductSalesStatsAggregate>(), TimeProvider.System);

        // Bypass the command validator (which also refuses it) to reach the aggregate's own rule.
        var response = await handler.HandleAsync(new RecordProductSalesStatsCommand
        { ProductId = product.Id, UnitsSold90d = -1, AsOf = DateTimeOffset.UtcNow, UserContext = Ctx(), CorrelationId = Guid.NewGuid(), ValidateCommand = false });

        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.InvalidUnitsSold);
        Assert.DoesNotContain(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.InvalidStockLevel);
    }
}
