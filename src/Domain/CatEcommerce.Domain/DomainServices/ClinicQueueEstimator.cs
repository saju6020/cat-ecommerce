using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-APT-001 §8.7 -- pure. An estimate, recomputed on every read (US-CLN-05 AC2); nothing is
/// stored. <c>ahead = Σ duration of waiting entries before i + Σ remaining minutes of InConsultation
/// entries; performers = max(1, qualified performers on shift); wait = ceil_to_5(ahead / performers)</c>.
/// </summary>
public static class ClinicQueueEstimator
{
    /// <summary><paramref name="orderedEntries"/> must already be in <see cref="ClinicQueueOrdering"/>'s
    /// order.</summary>
    public static IReadOnlyDictionary<Guid, int> EstimateWaitMinutes(
        IReadOnlyList<QueueEntryInput> orderedEntries, int performersOnShift, DateTimeOffset now)
    {
        var performers = Math.Max(1, performersOnShift);
        var remainingInConsultation = orderedEntries
            .Where(e => e.Status == AppointmentStatus.InConsultation)
            .Sum(e => RemainingMinutes(e, now));

        var result = new Dictionary<Guid, int>();
        var waitingAhead = 0;

        foreach (var entry in orderedEntries.Where(e => e.Status == AppointmentStatus.CheckedIn))
        {
            var ahead = waitingAhead + remainingInConsultation;
            result[entry.AppointmentId] = CeilTo5((double)ahead / performers);
            waitingAhead += entry.DurationMinutes;
        }

        return result;
    }

    private static int RemainingMinutes(QueueEntryInput entry, DateTimeOffset now)
    {
        if (entry.StartedOn is null) return entry.DurationMinutes;

        var elapsed = (now - entry.StartedOn.Value).TotalMinutes;
        return Math.Max(0, (int)Math.Ceiling(entry.DurationMinutes - elapsed));
    }

    private static int CeilTo5(double minutes) => (int)(Math.Ceiling(minutes / 5.0) * 5);
}
