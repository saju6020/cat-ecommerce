using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ADM-001 §8.5 / ADR-047. Pure function over pre-loaded roster, exceptions, approved leave and
/// the branch's open intervals (from <see cref="BranchCalendar"/>) -- no I/O, so Appointments and
/// Boarding can generate slots, and this can be tested, without a database. <paramref
/// name="timeZoneId"/> is the branch's own -- the inferred parameter this pure function needs to
/// convert a shift's local <c>TimeOnly</c> into an instant (§20.7); not named as such in the prose
/// signature but required by rule 1.
/// </summary>
public static class StaffAvailability
{
    public static IReadOnlyList<StaffAvailableInterval> Intervals(
        StaffRosterAggregate roster,
        IReadOnlyList<RosterExceptionAggregate> exceptions,
        IReadOnlyList<LeaveRequestAggregate> approvedLeave,
        IReadOnlyList<TimeInterval> branchOpenIntervals,
        string timeZoneId,
        DateOnly from,
        DateOnly to,
        bool restrictToOpeningHours)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var results = new List<StaffAvailableInterval>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            // Rule 5: approved leave removes whole days. Requested leave is not passed in here --
            // the caller only supplies Approved leave.
            if (approvedLeave.Any(l => l.StartsOn <= date && date <= l.EndsOn)) continue;

            // Rule 3: NextPattern wins once its effective date has arrived.
            var pattern = roster.NextPatternEffectiveFrom is not null && roster.NextPatternEffectiveFrom <= date
                ? roster.NextPattern
                : roster.CurrentPattern;

            var daySlots = pattern
                .Where(s => s.DayOfWeek == date.DayOfWeek)
                .Select(s => (Starts: s.Starts, Ends: s.Ends, RoomId: s.DefaultRoomId))
                .ToList();

            // Rule 4: ExtraShift adds; ShiftCancelled subtracts.
            foreach (var exception in exceptions.Where(e => !e.IsCancelled && e.Date == date))
            {
                if (exception.Kind == RosterExceptionKind.ExtraShift)
                {
                    daySlots.Add((exception.Starts, exception.Ends, null));
                }
                else
                {
                    daySlots = daySlots.SelectMany(s => Subtract(s, exception.Starts, exception.Ends)).ToList();
                }
            }

            foreach (var slot in daySlots)
            {
                if (slot.Starts >= slot.Ends) continue;

                var instantStart = BranchCalendar.ToInstant(date, slot.Starts, timeZone);
                var instantEnd = BranchCalendar.ToInstant(date, slot.Ends, timeZone);

                if (restrictToOpeningHours)
                {
                    foreach (var open in branchOpenIntervals)
                    {
                        var overlapStart = instantStart > open.Start ? instantStart : open.Start;
                        var overlapEnd = instantEnd < open.End ? instantEnd : open.End;
                        if (overlapStart < overlapEnd)
                            results.Add(new StaffAvailableInterval(roster.StaffAccountId, roster.BranchId, overlapStart, overlapEnd, slot.RoomId, false));
                    }
                }
                else
                {
                    var outsideOpeningHours = !branchOpenIntervals.Any(open => open.Start < instantEnd && instantStart < open.End);
                    results.Add(new StaffAvailableInterval(roster.StaffAccountId, roster.BranchId, instantStart, instantEnd, slot.RoomId, outsideOpeningHours));
                }
            }
        }

        return Merge(results);
    }

    private static IEnumerable<(TimeOnly Starts, TimeOnly Ends, Guid? RoomId)> Subtract(
        (TimeOnly Starts, TimeOnly Ends, Guid? RoomId) slot, TimeOnly cutFrom, TimeOnly cutTo)
    {
        var overlapStart = slot.Starts > cutFrom ? slot.Starts : cutFrom;
        var overlapEnd = slot.Ends < cutTo ? slot.Ends : cutTo;

        if (overlapStart >= overlapEnd)
        {
            yield return slot;
            yield break;
        }

        if (slot.Starts < overlapStart) yield return (slot.Starts, overlapStart, slot.RoomId);
        if (overlapEnd < slot.Ends) yield return (overlapEnd, slot.Ends, slot.RoomId);
    }

    private static IReadOnlyList<StaffAvailableInterval> Merge(List<StaffAvailableInterval> intervals)
    {
        if (intervals.Count == 0) return intervals;

        var ordered = intervals.OrderBy(i => i.Start).ToList();
        var merged = new List<StaffAvailableInterval> { ordered[0] };

        foreach (var next in ordered.Skip(1))
        {
            var last = merged[^1];
            if (next.Start <= last.End && next.OutsideOpeningHours == last.OutsideOpeningHours && next.DefaultRoomId == last.DefaultRoomId)
            {
                if (next.End > last.End) merged[^1] = last with { End = next.End };
            }
            else
            {
                merged.Add(next);
            }
        }

        return merged;
    }
}
