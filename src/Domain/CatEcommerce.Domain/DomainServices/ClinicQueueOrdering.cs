using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-APT-001 §8.7 -- pure. Order: triage priority descending (emergencies first, US-CLN-05 AC1);
/// within a priority, booked appointments by <c>StartsOn</c> and walk-ins by <c>CheckedInOn</c>,
/// interleaved by that one "due time"; ties broken by <c>CheckedInOn</c>, then id.
/// </summary>
public static class ClinicQueueOrdering
{
    public static IReadOnlyList<QueueEntryInput> Order(IEnumerable<QueueEntryInput> entries) =>
        entries
            .OrderByDescending(e => e.Priority.Value)
            .ThenBy(DueOn)
            .ThenBy(e => e.CheckedInOn)
            .ThenBy(e => e.AppointmentId)
            .ToList();

    public static DateTimeOffset DueOn(QueueEntryInput entry) =>
        entry.IsWalkIn ? entry.CheckedInOn ?? DateTimeOffset.MaxValue : entry.StartsOn ?? entry.CheckedInOn ?? DateTimeOffset.MaxValue;

    /// <summary>A vet's own queue: their entries, plus unassigned walk-ins any qualified performer
    /// may take (US-CLN-10 AC1).</summary>
    public static IReadOnlyList<QueueEntryInput> ForPerformer(IReadOnlyList<QueueEntryInput> ordered, Guid performerId) =>
        ordered.Where(e => e.PerformerId == performerId || e.PerformerId is null).ToList();
}
