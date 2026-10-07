using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ADM-001 §8.5 / ADR-045. Pure function, no I/O -- the one place local wall-clock time is
/// converted to an instant for the branch calendar (§20.7); <see cref="LocalDateOf"/> is the
/// inverse (instant -&gt; local date) and must be used everywhere a handler needs "today" or "this
/// date" in a branch's own time zone instead of an inline <see cref="TimeZoneInfo"/> conversion
/// (ADR-045, gate fix 2026-09-26). A full-day closure removes the whole day; a partial closure
/// (same-day, <c>PartialFrom</c>/<c>PartialTo</c> set) removes only that window, and every matching
/// partial closure for the day is folded in turn (not just the first -- gate BLOCKER 2026-09-26). A
/// day with no <c>OpeningInterval</c> is closed. Cancelled closures are ignored.
/// </summary>
public static class BranchCalendar
{
    public static IReadOnlyList<TimeInterval> OpenIntervals(
        BranchAggregate branch,
        IReadOnlyList<BranchClosureAggregate> closures,
        DateOnly fromLocalDate,
        DateOnly toLocalDate)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(branch.TimeZoneId);
        var raw = new List<TimeInterval>();

        for (var date = fromLocalDate; date <= toLocalDate; date = date.AddDays(1))
        {
            var isFullDayClosed = closures.Any(c =>
                !c.IsCancelled && c.PartialFrom is null && c.StartsOn <= date && date <= c.EndsOn);
            if (isFullDayClosed) continue;

            var partials = closures
                .Where(c => !c.IsCancelled && c.PartialFrom is not null && c.StartsOn == date && c.EndsOn == date)
                .ToList();

            foreach (var opening in branch.WeeklyHours.Where(h => h.DayOfWeek == date.DayOfWeek))
            {
                var segments = new List<(TimeOnly Start, TimeOnly End)> { (opening.Opens, opening.Closes) };
                foreach (var partial in partials)
                {
                    segments = segments
                        .SelectMany(s => Subtract(s.Start, s.End, partial.PartialFrom!.Value, partial.PartialTo!.Value))
                        .ToList();
                }

                foreach (var (start, end) in segments)
                {
                    if (start >= end) continue;
                    raw.Add(new TimeInterval(ToInstant(date, start, timeZone), ToInstant(date, end, timeZone)));
                }
            }
        }

        return Merge(raw);
    }

    /// <summary>The branch-local calendar date that <paramref name="instant"/> falls on.</summary>
    public static DateOnly LocalDateOf(DateTimeOffset instant, string timeZoneId)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(instant, timeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    private static List<(TimeOnly Start, TimeOnly End)> Subtract(TimeOnly opens, TimeOnly closes, TimeOnly cutFrom, TimeOnly cutTo)
    {
        var overlapStart = opens > cutFrom ? opens : cutFrom;
        var overlapEnd = closes < cutTo ? closes : cutTo;

        if (overlapStart >= overlapEnd) return [(opens, closes)];

        var result = new List<(TimeOnly, TimeOnly)>();
        if (opens < overlapStart) result.Add((opens, overlapStart));
        if (overlapEnd < closes) result.Add((overlapEnd, closes));
        return result;
    }

    /// <summary>
    /// Converts a branch-local wall-clock date/time into an instant. DST rule (documented,
    /// gate fix 2026-09-26): an <em>invalid</em> local time (the spring-forward gap, which never
    /// occurs on the wall clock) is advanced minute-by-minute to the first instant that does exist,
    /// rather than throwing -- the shift/opening starts as soon as the clock allows it. An
    /// <em>ambiguous</em> local time (the fall-back overlap, which occurs twice) always resolves to
    /// its earlier occurrence (the larger UTC offset), so a start and an end computed independently
    /// from the same nominal time never appear to cross.
    /// </summary>
    internal static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (timeZone.IsInvalidTime(local))
        {
            var probe = local;
            while (timeZone.IsInvalidTime(probe)) probe = probe.AddMinutes(1);
            return new DateTimeOffset(probe, timeZone.GetUtcOffset(probe));
        }

        if (timeZone.IsAmbiguousTime(local))
        {
            var earliestOffset = timeZone.GetAmbiguousTimeOffsets(local).Max();
            return new DateTimeOffset(local, earliestOffset);
        }

        return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
    }

    internal static IReadOnlyList<TimeInterval> Merge(List<TimeInterval> intervals)
    {
        if (intervals.Count == 0) return intervals;

        var ordered = intervals.OrderBy(i => i.Start).ToList();
        var merged = new List<TimeInterval> { ordered[0] };

        foreach (var next in ordered.Skip(1))
        {
            var last = merged[^1];
            if (next.Start <= last.End)
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
