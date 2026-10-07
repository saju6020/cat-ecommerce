using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 8.7, section 12.1 -- pure. Shop never computes sellable stock (ADR-082); this
/// takes the already-read <c>sellable_quantity</c> from <c>inventory.v_sellable_stock</c> (a SKU
/// with no row is out of stock, i.e. <c>sellableQuantity == null</c>) and applies the two rules
/// that ARE Shop's own: a discontinued SKU or an unlisted/non-active product is never shown as
/// purchasable regardless of quantity, and the expiry notice only shows inside the configured
/// window.
/// </summary>
public static class SkuAvailabilityRules
{
    public static AvailabilityStatus Evaluate(int? sellableQuantity, bool skuActive, bool productSellable)
    {
        if (!skuActive || !productSellable) return AvailabilityStatus.OutOfStock;
        return sellableQuantity is > 0 ? AvailabilityStatus.InStock : AvailabilityStatus.OutOfStock;
    }

    /// <summary>US-SHP-07 AC2 -- shown when the allocatable batch's expiry falls within
    /// <c>shop.expiryDisplayWindowDays</c> of today. <c>windowDays == 0</c> means never shown.</summary>
    public static DateOnly? ExpiryNotice(DateOnly? nextAllocatableExpiryOn, DateOnly today, int windowDays)
    {
        if (nextAllocatableExpiryOn is null || windowDays <= 0) return null;

        var daysUntilExpiry = nextAllocatableExpiryOn.Value.DayNumber - today.DayNumber;
        return daysUntilExpiry is >= 0 && daysUntilExpiry <= windowDays ? nextAllocatableExpiryOn : null;
    }

    /// <summary>US-SHP-12 AC2 -- a bundle is in stock only if every component has enough sellable
    /// quantity for at least one bundle unit.</summary>
    public static AvailabilityStatus EvaluateBundle(IReadOnlyList<(int Quantity, int? ComponentSellableQuantity)> components)
    {
        if (components.Count == 0) return AvailabilityStatus.OutOfStock;

        foreach (var (quantity, sellable) in components)
        {
            if (sellable is null or 0) return AvailabilityStatus.OutOfStock;
            if (sellable.Value / quantity < 1) return AvailabilityStatus.OutOfStock;
        }

        return AvailabilityStatus.InStock;
    }
}
