using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BRD-001 section 8.9/8.13 (US-BRD-11 AC2, ADR-225, renumbered ADR-358). A cat is "not eating" when its last TWO observations both record that
/// nothing was eaten. Derived at read time and never stored -- a stored flag goes stale the moment a third entry arrives.
/// </summary>
public static class NotEatingRule
{
    /// <param name="portionsNewestFirst">The portions of the cat's observations in this stay, newest first.</param>
    public static bool IsNotEating(IReadOnlyList<PortionEaten> portionsNewestFirst) =>
        portionsNewestFirst.Count >= 2
        && portionsNewestFirst[0] == PortionEaten.None
        && portionsNewestFirst[1] == PortionEaten.None;
}
