using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.Tests.Aggregates.Product;

/// <summary>HO-SHP-001 §15 -- the sales-stats analogue of the dropped stock aggregate's Apply
/// semantics (ADR-082 narrowed).</summary>
public sealed class ProductSalesStatsAggregateTests
{
    private static ProductSalesStatsApplyDto Dto(int units, DateTimeOffset asOf) => new(units, asOf, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid());

    [Fact]
    public void Apply_FirstPush_Applies()
    {
        var stats = new ProductSalesStatsAggregate(Guid.NewGuid());
        var changed = stats.Apply(Dto(10, DateTimeOffset.UtcNow));

        Assert.True(changed);
        Assert.Equal(10, stats.UnitsSold90d);
    }

    [Fact]
    public void Apply_NewerAsOf_Applied()
    {
        var stats = new ProductSalesStatsAggregate(Guid.NewGuid());
        var t = DateTimeOffset.UtcNow;
        stats.Apply(Dto(10, t));

        var changed = stats.Apply(Dto(20, t.AddMinutes(1)));

        Assert.True(changed);
        Assert.Equal(20, stats.UnitsSold90d);
    }

    [Fact]
    public void Apply_OlderAsOf_Ignored()
    {
        var stats = new ProductSalesStatsAggregate(Guid.NewGuid());
        var t = DateTimeOffset.UtcNow;
        stats.Apply(Dto(10, t));

        var changed = stats.Apply(Dto(999, t.AddMinutes(-1)));

        Assert.False(changed);
        Assert.Equal(10, stats.UnitsSold90d);
    }

    [Fact]
    public void Apply_SameAsOf_NoChange()
    {
        var stats = new ProductSalesStatsAggregate(Guid.NewGuid());
        var t = DateTimeOffset.UtcNow;
        stats.Apply(Dto(10, t));

        var changed = stats.Apply(Dto(999, t));

        Assert.False(changed);
        Assert.Equal(10, stats.UnitsSold90d);
    }

    [Fact]
    public void Apply_Negative_ThrowsInvalidUnitsSold()
    {
        var stats = new ProductSalesStatsAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() => stats.Apply(Dto(-1, DateTimeOffset.UtcNow)));
        Assert.Equal(BusinessViolationCodes.InvalidUnitsSold, ex.ErrorCode);
    }

    // Row 35: a far-future AsOf is refused (it would freeze every later push); a few minutes of skew is fine.
    [Fact]
    public void Apply_FarFutureAsOf_ThrowsInvalidStatsAsOf_AndLaterPushesStillApply()
    {
        var stats = new ProductSalesStatsAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() => stats.Apply(Dto(10, DateTimeOffset.UtcNow.AddYears(50))));
        Assert.Equal(BusinessViolationCodes.InvalidStatsAsOf, ex.ErrorCode);

        Assert.True(stats.Apply(Dto(20, DateTimeOffset.UtcNow)));
        Assert.Equal(20, stats.UnitsSold90d);
    }

    [Fact]
    public void Apply_AsOfWithinClockSkew_Applies()
    {
        var stats = new ProductSalesStatsAggregate(Guid.NewGuid());
        Assert.True(stats.Apply(Dto(10, DateTimeOffset.UtcNow.AddMinutes(2))));
    }
}
