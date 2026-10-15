using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-PHM-001 §8.4 (ADR-142) -- THE one definition of "active", "course ends on", "supplies left" and "remaining days". Active is derived, never stored;
/// the portal query, the interaction detector, the refill sweep and the lists all call this and nothing else. Pure.
///
/// <code>
/// CourseEndsOn = (LastDispensedOn ?? StartsOn) + DurationDays - 1
/// SuppliesLeft = 1 + RepeatsAuthorised - SuppliesDispensed
/// IsActive(today) = Status != Cancelled and (CourseEndsOn >= today or (SuppliesLeft > 0 and ValidUntil >= today and Status != Expired))
/// RemainingDays(today) = max(0, CourseEndsOn - today + 1)
/// </code>
/// </summary>
public static class PrescriptionActivityRules
{
    public static DateOnly CourseEndsOn(DateOnly startsOn, DateOnly? lastDispensedOn, int durationDays) =>
        (lastDispensedOn ?? startsOn).AddDays(durationDays - 1);

    public static int SuppliesLeft(int repeatsAuthorised, int suppliesDispensed) => 1 + repeatsAuthorised - suppliesDispensed;

    public static bool IsActive(
        PrescriptionStatus status, DateOnly startsOn, DateOnly? lastDispensedOn, int durationDays, int repeatsAuthorised, int suppliesDispensed,
        DateOnly validUntil, DateOnly today)
    {
        if (status == PrescriptionStatus.Cancelled) return false;

        if (CourseEndsOn(startsOn, lastDispensedOn, durationDays) >= today) return true;

        return SuppliesLeft(repeatsAuthorised, suppliesDispensed) > 0 && validUntil >= today && status != PrescriptionStatus.Expired;
    }

    public static int RemainingDays(DateOnly startsOn, DateOnly? lastDispensedOn, int durationDays, DateOnly today) =>
        Math.Max(0, CourseEndsOn(startsOn, lastDispensedOn, durationDays).DayNumber - today.DayNumber + 1);

    /// <summary>The status every read and command treats the prescription as having on <paramref name="today"/>: an <c>Issued</c> or <c>Dispensed</c>
    /// prescription whose <c>ValidUntil</c> has passed with supplies left is <c>Expired</c> whether or not the sweep has run (ADR-142).</summary>
    public static PrescriptionStatus EffectiveStatus(PrescriptionStatus status, DateOnly validUntil, int repeatsAuthorised, int suppliesDispensed, DateOnly today) =>
        (status == PrescriptionStatus.Issued || status == PrescriptionStatus.Dispensed) && today > validUntil && SuppliesLeft(repeatsAuthorised, suppliesDispensed) > 0
            ? PrescriptionStatus.Expired
            : status;
}
