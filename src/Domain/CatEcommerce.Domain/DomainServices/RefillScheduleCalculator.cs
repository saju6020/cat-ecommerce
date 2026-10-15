using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.11 (ADR-150, US-NTF-03 AC1) -- when the next supply of a repeat prescription falls due and from when the owner is reminded. For a
/// repeatable prescription with supplies left, a recorded last supply and no cancellation or expiry: <c>RefillDueOn = LastDispensedOn + DurationDays</c>;
/// remind when <c>RefillDueOn - leadDays &lt;= today &lt;= RefillDueOn</c> and <c>RefillDueOn &lt;= ValidUntil</c> (nothing is sent about a refill that
/// can no longer be made). A lead of zero turns reminders off. Pure -- the sweep and <c>PrescriptionRefillDueGuard</c> both call it.
/// </summary>
public static class RefillScheduleCalculator
{
    /// <summary>The due date, or <c>null</c> when the prescription is not (or no longer) refillable.</summary>
    public static DateOnly? RefillDueOn(
        bool isRepeatable, PrescriptionStatus status, int suppliesLeft, DateOnly? lastDispensedOn, int durationDays, DateOnly validUntil)
    {
        if (!isRepeatable || suppliesLeft <= 0 || lastDispensedOn is null) return null;
        if (status == PrescriptionStatus.Cancelled || status == PrescriptionStatus.Expired || status == PrescriptionStatus.Completed) return null;

        var due = lastDispensedOn.Value.AddDays(durationDays);
        return due <= validUntil ? due : null;
    }

    /// <summary>The schedule when <paramref name="today"/> lies in the reminder window, else <c>null</c>.</summary>
    public static RefillSchedule? Due(
        bool isRepeatable, PrescriptionStatus status, int suppliesLeft, DateOnly? lastDispensedOn, int durationDays, DateOnly validUntil, DateOnly today, int leadDays)
    {
        if (leadDays <= 0) return null;

        var due = RefillDueOn(isRepeatable, status, suppliesLeft, lastDispensedOn, durationDays, validUntil);
        if (due is null) return null;

        var remindFrom = due.Value.AddDays(-leadDays);
        return remindFrom <= today && today <= due.Value ? new RefillSchedule(due.Value, remindFrom) : null;
    }
}
