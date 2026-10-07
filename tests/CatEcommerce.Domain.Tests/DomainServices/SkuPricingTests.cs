using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-SHP-001 §15 U-A11 -- the same rule the storefront view SQL implements.</summary>
public sealed class SkuPricingTests
{
    private static SkuPriceAggregate PriceAt(DateTimeOffset effectiveFrom, DateTimeOffset now, decimal amount)
    {
        var price = new SkuPriceAggregate(Guid.NewGuid());
        price.Create(new SkuPriceCreateDto(Guid.NewGuid(), new Money(amount, "BDT"), effectiveFrom, Guid.NewGuid(), now, TestUserContext.Create(), Guid.NewGuid()), "BDT");
        return price;
    }

    [Fact]
    public void PriceAt_PicksGreatestEffectiveFromNotAfterAsOf()
    {
        var t = DateTimeOffset.UtcNow.AddDays(10); // safely in the future so "now" for Create passes
        var rows = new[]
        {
            PriceAt(t.AddDays(-2), t.AddDays(-2), 100m),
            PriceAt(t.AddDays(-1), t.AddDays(-1), 200m),
            PriceAt(t.AddDays(1), t.AddDays(-3), 300m), // scheduled ahead of "t"
        };

        var atT = SkuPricing.PriceAt(rows, t);
        Assert.Equal(200m, atT!.Amount.Amount);

        var atTPlus1Day = SkuPricing.PriceAt(rows, t.AddDays(1));
        Assert.Equal(300m, atTPlus1Day!.Amount.Amount);
    }

    [Fact]
    public void PriceAt_NoRowsAtOrBeforeAsOf_ReturnsNull()
    {
        var t = DateTimeOffset.UtcNow.AddDays(10);
        var rows = new[] { PriceAt(t.AddDays(1), t.AddDays(-3), 100m) };

        Assert.Null(SkuPricing.PriceAt(rows, t));
    }
}
