using CatEcommerce.Domain.Aggregates.Product;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 8.5 -- "price at instant t = the row with the greatest EffectiveFrom &lt;= t".
/// Pure; the exact same rule the storefront view SQL implements (U-A11, I-A8 pin both).
/// </summary>
public static class SkuPricing
{
    public static SkuPriceAggregate? PriceAt(IEnumerable<SkuPriceAggregate> prices, DateTimeOffset asOf) =>
        PriceAt(prices, p => p.EffectiveFrom, asOf);

    /// <summary>The same rule over projected rows, so a read-side query need not materialise
    /// <see cref="SkuPriceAggregate"/> to apply it.</summary>
    public static T? PriceAt<T>(IEnumerable<T> prices, Func<T, DateTimeOffset> effectiveFrom, DateTimeOffset asOf) where T : class =>
        prices.Where(p => effectiveFrom(p) <= asOf)
              .OrderByDescending(effectiveFrom)
              .FirstOrDefault();
}
