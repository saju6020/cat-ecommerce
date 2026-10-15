namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BRD-001 -- the rules the stay aggregate and the READ side both need, over plain values. A query handler projects columns (it never rehydrates the aggregate, ADR-012), so each rule that a read
/// must apply lives here and the aggregate method delegates to it: the two cannot drift (the SRG-001 <c>SurgeryCampaignAggregate.Evaluate</c> precedent).
/// </summary>
public static class StayAllocationRule
{
    /// <summary>The allocation currently holding a unit: the latest ACTIVE one by start. <c>null</c> for an enquiry.</summary>
    public static T? Current<T>(IEnumerable<T> allocations, Func<T, bool> isActive, Func<T, DateTimeOffset> occupiedFrom) where T : class =>
        allocations.Where(isActive).OrderByDescending(occupiedFrom).FirstOrDefault();
}

public static class StayAwayRule
{
    public const string InSurgery = "In surgery";
    public const string AtTheClinic = "At the clinic";

    /// <summary>Why a cat is away from its unit at <paramref name="at"/> (a surgery that has started and not been discharged, or an open referral), or <c>null</c> when it is in its unit.</summary>
    public static string? Reason(
        Guid catId, DateTimeOffset at, IEnumerable<(Guid CatId, bool IsOpen)> referrals, IEnumerable<(Guid CatId, DateTimeOffset StartsOn, DateTimeOffset? DischargedOn)> surgeries) =>
        surgeries.Any(s => s.CatId == catId && s.DischargedOn is null && s.StartsOn <= at) ? InSurgery
        : referrals.Any(r => r.CatId == catId && r.IsOpen) ? AtTheClinic
        : null;
}
