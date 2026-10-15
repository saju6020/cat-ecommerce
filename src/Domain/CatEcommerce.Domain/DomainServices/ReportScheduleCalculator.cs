using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-RPT-001 section 8.9 (ADR-287), <b>pure</b> -- the next instant a schedule is due. The next local occurrence of <c>SendAtLocal</c> on a matching day, converted with the branch's zone.
/// A local time skipped by a DST change moves to the first valid instant after it; an ambiguous (repeated) local time takes the <b>first</b> occurrence (the earlier instant).
/// </summary>
public static class ReportScheduleCalculator
{
    /// <summary>The first due instant strictly after <paramref name="after"/>. Searches a little over a year of days, which every frequency satisfies.</summary>
    public static DateTimeOffset NextAfter(
        ReportFrequency frequency, DayOfWeek? dayOfWeek, int? dayOfMonth, bool lastDayOfMonth, TimeOnly sendAtLocal, DateTimeOffset after, TimeZoneInfo zone)
    {
        var startDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(after, zone).DateTime).AddDays(-1);

        for (var offset = 0; offset <= 400; offset++)
        {
            var date = startDate.AddDays(offset);
            if (!Matches(frequency, dayOfWeek, dayOfMonth, lastDayOfMonth, date)) continue;

            var instant = ToInstant(date, sendAtLocal, zone);
            if (instant > after) return instant;
        }

        throw new InvalidOperationException("A schedule always has an occurrence within a year.");
    }

    public static IReadOnlyList<DateTimeOffset> NextRuns(
        ReportFrequency frequency, DayOfWeek? dayOfWeek, int? dayOfMonth, bool lastDayOfMonth, TimeOnly sendAtLocal, DateTimeOffset after, TimeZoneInfo zone, int count)
    {
        var runs = new List<DateTimeOffset>(count);
        var cursor = after;
        for (var i = 0; i < count; i++)
        {
            cursor = NextAfter(frequency, dayOfWeek, dayOfMonth, lastDayOfMonth, sendAtLocal, cursor, zone);
            runs.Add(cursor);
        }
        return runs;
    }

    public static bool Matches(ReportFrequency frequency, DayOfWeek? dayOfWeek, int? dayOfMonth, bool lastDayOfMonth, DateOnly date)
    {
        if (frequency == ReportFrequency.Daily) return true;
        if (frequency == ReportFrequency.Weekly) return date.DayOfWeek == dayOfWeek;

        return lastDayOfMonth
            ? date.Day == DateTime.DaysInMonth(date.Year, date.Month)
            : date.Day == dayOfMonth;
    }

    /// <summary>A skipped local time moves forward to the first valid instant; a repeated one takes the first (earlier) occurrence.</summary>
    public static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);

        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}

/// <summary>HO-RPT-001 section 8.9 -- which relative periods a report accepts on a schedule (ADR-287). Pure.</summary>
public static class ReportSchedulePeriods
{
    /// <summary>
    /// A forward-looking report (default <c>Next30Days</c>/<c>Next90Days</c>) takes only a forward period; an as-at report takes <c>Today</c> or its own default; a period report takes any
    /// backward period, and <c>Today</c> only when that is its own default (a today-style report such as the daily schedule).
    /// </summary>
    public static bool IsCompatible(ReportDefinition definition, ReportRelativePeriod period)
    {
        var forward = IsForward(period);
        var definitionForward = IsForward(definition.DefaultPeriod);

        if (forward || definitionForward) return forward && definitionForward;
        if (definition.Kind == ReportKind.AsAt) return period == ReportRelativePeriod.Today || period == definition.DefaultPeriod;
        if (period == ReportRelativePeriod.Today) return definition.DefaultPeriod == ReportRelativePeriod.Today;
        return true;
    }

    private static bool IsForward(ReportRelativePeriod period) => period == ReportRelativePeriod.Next30Days || period == ReportRelativePeriod.Next90Days;
}
