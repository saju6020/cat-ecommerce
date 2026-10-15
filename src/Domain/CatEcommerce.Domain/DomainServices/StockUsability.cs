using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-IMP-001 §8.13 (US-IMP-12 AC1) -- what part of a position counts as USABLE: <c>on hand - reserved</c> of an <c>Available</c> batch that is not
/// expired in the branch's local date and does not sit at the quarantine location. Quarantined, recalled, written-off and expired stock is never usable,
/// and neither is stock that is already promised to an order. The one place the low-stock and reorder reads take their numbers from.
/// </summary>
public static class StockUsability
{
    public static decimal Usable(decimal onHand, decimal reserved, BatchStatus status, StockLocationKind locationKind, DateOnly? expiresOn, DateOnly branchToday)
    {
        if (status != BatchStatus.Available) return 0m;
        if (locationKind == StockLocationKind.Quarantine) return 0m;
        if (BatchExpiryRules.IsExpired(expiresOn, branchToday)) return 0m;

        return Math.Max(0m, onHand - reserved);
    }
}
