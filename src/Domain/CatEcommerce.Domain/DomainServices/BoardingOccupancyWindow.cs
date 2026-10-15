using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>An instant range <c>[From, To)</c> a unit or a cat is occupied for.</summary>
public readonly record struct OccupancyRange(DateTimeOffset From, DateTimeOffset To)
{
    public bool Overlaps(OccupancyRange other) => From < other.To && other.From < To;
}

/// <summary>
/// HO-BRD-001 section 8.4 (ADR-219, renumbered ADR-352): the instants a stay occupies a unit, derived from its LOCAL dates, the branch's
/// check-in and check-out windows and the cleaning turnaround. Pure. Conversion goes through <see cref="BranchCalendar"/> only (HO-ADM-001 ADR-045),
/// so no time-zone library appears here. With the defaults (in from 14:00, out by 11:00, 3 h turnaround) a departure and an arrival on the same day
/// are back to back; a 24 h turnaround leaves a day between them (US-BRD-02 AC2).
/// </summary>
public static class BoardingOccupancyWindow
{
    public static OccupancyRange Compute(
        DateOnly arrivalOn, DateOnly departureOn, string timeZoneId, TimeRange checkInWindow, TimeRange checkOutWindow, int turnaroundHours)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var from = BranchCalendar.ToInstant(arrivalOn, checkInWindow.Start, zone).ToUniversalTime();
        var to = BranchCalendar.ToInstant(departureOn, checkOutWindow.End, zone).AddHours(turnaroundHours).ToUniversalTime();
        return new OccupancyRange(from, to);
    }

    /// <summary>Whole local days <c>[startsOn at 00:00, endsOn + 1 at 00:00)</c> as instants -- a maintenance block's range.</summary>
    public static OccupancyRange ForDays(DateOnly startsOn, DateOnly endsOn, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return new OccupancyRange(
            BranchCalendar.ToInstant(startsOn, TimeOnly.MinValue, zone).ToUniversalTime(),
            BranchCalendar.ToInstant(endsOn.AddDays(1), TimeOnly.MinValue, zone).ToUniversalTime());
    }

    // Every instant is returned in UTC: Npgsql writes timestamptz only with a zero offset.

    /// <summary>An early check-out frees the unit at the check-out instant plus the turnaround, never earlier than the range began.</summary>
    public static OccupancyRange TruncateAtCheckOut(OccupancyRange range, DateTimeOffset checkedOutOn, int turnaroundHours)
    {
        var end = checkedOutOn.AddHours(turnaroundHours);
        if (end > range.To) end = range.To;
        if (end <= range.From) end = range.From.AddTicks(1);
        return range with { To = end };
    }

    /// <summary>A branch-local date and time as a UTC instant.</summary>
    public static DateTimeOffset InstantOf(DateOnly date, TimeOnly time, string timeZoneId) =>
        BranchCalendar.ToInstant(date, time, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).ToUniversalTime();

    /// <summary>The branch-local date of an instant.</summary>
    public static DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId) => BranchCalendar.LocalDateOf(instant, timeZoneId);
}
