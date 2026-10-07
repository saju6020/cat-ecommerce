using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Product;

/// <summary>
/// HO-SHP-001 section 8.7 -- a system-written copy of units-sold, pushed by HO-ORD-001 through
/// <c>RecordProductSalesStatsCommand</c> (ADR-082, narrowed 2026-09-22: the stock half of this
/// aggregate pattern was dropped in favour of reading <c>inventory.v_sellable_stock</c> live; only
/// the sales-figure copy remains, because HO-ORD-001 publishes no equivalent view). <c>Id ==
/// ProductId</c>. No HTTP route, never editable by a person, display only.
/// </summary>
public sealed class ProductSalesStatsAggregate : AggregateRoot
{
    public static readonly TimeSpan MaxAsOfSkew = TimeSpan.FromMinutes(5);

    public int UnitsSold90d { get; private set; }
    public DateTimeOffset AsOf { get; private set; }

    private ProductSalesStatsAggregate() { }

    public ProductSalesStatsAggregate(Guid id) : base(id) { }

    /// <summary>Monotonic by <see cref="AsOf"/>: a newer push applies; an older or equal-AsOf push
    /// is silently ignored (never throws for staleness -- the next push from the real owner
    /// supersedes it). Returns whether anything changed, so the handler can skip persisting a
    /// no-op push.</summary>
    public bool Apply(ProductSalesStatsApplyDto dto)
    {
        if (dto.UnitsSold90d < 0)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidUnitsSold, "Units sold cannot be negative.", nameof(UnitsSold90d));

        // Row 35: a far-future AsOf would become the stored AsOf and make every later (genuine) push look
        // stale, freezing the figure. A little forward skew between the two systems' clocks is tolerated.
        if (dto.AsOf > dto.Now + MaxAsOfSkew)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStatsAsOf, "AsOf cannot be in the future.", nameof(AsOf));

        if (Version > 0 && dto.AsOf <= AsOf) return false;

        UnitsSold90d = dto.UnitsSold90d;
        AsOf = dto.AsOf;

        SetDefaultValue(dto.UserContext);
        UpdateVersion();

        return true;
    }
}
