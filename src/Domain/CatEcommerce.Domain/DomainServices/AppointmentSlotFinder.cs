using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-APT-001 §8.5 (ADR-069) -- pure, no I/O. Slots are computed on every request from the branch
/// calendar, rosters and leave (already folded into each performer's <c>AvailableIntervals</c> by
/// HO-ADM-001's <c>StaffAvailability</c>), the service's duration and qualified roles, the rooms,
/// and the existing bookings. Never stores a slot.
/// </summary>
public static class AppointmentSlotFinder
{
    public static IReadOnlyList<Slot> FindSlots(SlotRequest request)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId);
        var duration = TimeSpan.FromMinutes(request.Service.DurationMinutes);
        var gridStarts = GridStarts(request.LocalDate, request.GridMinutes, timeZone);

        var qualifiedPerformers = request.Performers
            .Where(p => p.RoleIds.Any(r => request.Service.QualifiedRoleIds.Contains(r, StringComparer.Ordinal)))
            .Where(p => request.PreferredPerformerId is null || p.StaffAccountId == request.PreferredPerformerId)
            .ToList();

        if (qualifiedPerformers.Count == 0) return [];

        var activeRooms = request.Rooms
            .Where(r => r.IsActive && r.Kind == request.Service.RequiredRoomKind)
            .Where(r => request.PreferredRoomId is null || r.RoomId == request.PreferredRoomId)
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();

        var results = new List<Slot>();

        foreach (var performer in qualifiedPerformers)
        {
            var performerBusy = request.Busy.ForPerformer(performer.StaffAccountId);

            foreach (var start in gridStarts)
            {
                if (start < request.EarliestStart) continue;
                var end = start + duration;

                var window = performer.AvailableIntervals.FirstOrDefault(w => w.Start <= start && end <= w.End);
                if (window is null) continue;

                if (Overlaps(performerBusy, start, end)) continue;
                if (Overlaps(request.Busy.CatBusyIntervals, start, end)) continue;

                var room = activeRooms.FirstOrDefault(r => !Overlaps(request.Busy.ForRoom(r.RoomId), start, end));
                if (room is null) continue;

                results.Add(new Slot(start, end, performer.StaffAccountId, performer.Name, room.RoomId, room.Name, window.OutsideOpeningHours));
            }
        }

        return results
            .DistinctBy(s => (s.StartsOn, s.PerformerId))
            .OrderBy(s => s.StartsOn)
            .ThenBy(s => s.PerformerName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Public wrapper over <see cref="GridStarts(DateOnly,int,TimeZoneInfo)"/> for
    /// Application-layer callers (<c>SlotClaim</c>) that need to check "is this exact instant on
    /// the grid" without re-deriving the alignment rule themselves -- <c>BranchCalendar.ToInstant</c>
    /// is internal to this assembly.</summary>
    public static IReadOnlyList<DateTimeOffset> GridStartsForDay(DateOnly localDate, int gridMinutes, string timeZoneId) =>
        GridStarts(localDate, gridMinutes, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

    /// <summary>Public wrapper over <see cref="BranchCalendar.ToInstant"/> (internal to this
    /// assembly) for Infrastructure/Read-side callers that need one specific branch-local
    /// wall-clock instant -- e.g. a day's start/end for a scheduling-inputs or calendar range --
    /// without hand-building a <see cref="DateTimeOffset"/> themselves. ADR-045 requires
    /// local-to-instant conversion to happen nowhere outside <c>BranchCalendar</c>/
    /// <c>StaffAvailability</c> and their own wrappers; this is that wrapper's counterpart to
    /// <see cref="GridStartsForDay"/>. Always UTC-offset (<c>.ToUniversalTime()</c>) -- Npgsql
    /// accepts only offset-0 <see cref="DateTimeOffset"/> values.</summary>
    public static DateTimeOffset InstantOf(DateOnly localDate, TimeOnly localTime, string timeZoneId) =>
        BranchCalendar.ToInstant(localDate, localTime, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).ToUniversalTime();

    /// <summary>Grid starts for the whole local day, aligned to local midnight, converted to
    /// instants (§8.5 rule 3). Always UTC-offset (<c>.ToUniversalTime()</c>) -- these values flow
    /// straight onto <c>AppointmentAggregate.StartsOn</c>/<c>EndsOn</c> and into EF query
    /// parameters, and Npgsql accepts only offset-0 <see cref="DateTimeOffset"/> values.</summary>
    private static List<DateTimeOffset> GridStarts(DateOnly localDate, int gridMinutes, TimeZoneInfo timeZone)
    {
        var starts = new List<DateTimeOffset>();
        for (var minutes = 0; minutes < 24 * 60; minutes += gridMinutes)
        {
            var time = new TimeOnly(0, 0).Add(TimeSpan.FromMinutes(minutes));
            starts.Add(BranchCalendar.ToInstant(localDate, time, timeZone).ToUniversalTime());
        }

        return starts;
    }

    private static bool Overlaps(IReadOnlyList<TimeInterval> intervals, DateTimeOffset start, DateTimeOffset end) =>
        intervals.Any(i => i.Start < end && start < i.End);
}
