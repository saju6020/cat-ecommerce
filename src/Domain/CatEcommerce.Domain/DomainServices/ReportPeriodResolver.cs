using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-RPT-001 section 8.7 / ADR-282 -- the time model of a report, <b>pure</b> (HO-ADM-001 ADR-045). <c>from</c> and <c>to</c> are branch-local dates, inclusive; the instant range
/// is <c>[from 00:00, to + 1 day 00:00)</c> in the report branch's zone. Relative periods are resolved as of an instant in a zone, for the client's defaults and for schedules.
/// </summary>
public static class ReportPeriodResolver
{
    /// <summary>The local calendar date of <paramref name="asOf"/> in <paramref name="zone"/>.</summary>
    public static DateOnly LocalDate(DateTimeOffset asOf, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(asOf, zone).DateTime);

    /// <summary>Resolves a relative period to an inclusive local date range. "Last" periods end yesterday; weeks run Monday to Sunday.</summary>
    public static (DateOnly From, DateOnly To) Resolve(ReportRelativePeriod period, DateTimeOffset asOf, TimeZoneInfo zone)
    {
        var today = LocalDate(asOf, zone);

        if (period == ReportRelativePeriod.Today) return (today, today);
        if (period == ReportRelativePeriod.PreviousDay) return (today.AddDays(-1), today.AddDays(-1));

        if (period == ReportRelativePeriod.PreviousWeek)
        {
            var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var thisMonday = today.AddDays(-daysSinceMonday);
            return (thisMonday.AddDays(-7), thisMonday.AddDays(-1));
        }

        if (period == ReportRelativePeriod.PreviousMonth)
        {
            var firstOfThisMonth = new DateOnly(today.Year, today.Month, 1);
            var firstOfPrevious = firstOfThisMonth.AddMonths(-1);
            return (firstOfPrevious, firstOfThisMonth.AddDays(-1));
        }

        if (period == ReportRelativePeriod.MonthToDate) return (new DateOnly(today.Year, today.Month, 1), today);
        if (period == ReportRelativePeriod.Last7Days) return (today.AddDays(-7), today.AddDays(-1));
        if (period == ReportRelativePeriod.Last30Days) return (today.AddDays(-30), today.AddDays(-1));
        if (period == ReportRelativePeriod.Next30Days) return (today, today.AddDays(29));
        if (period == ReportRelativePeriod.Next90Days) return (today, today.AddDays(89));

        throw new ArgumentOutOfRangeException(nameof(period), period.DisplayName, "Unknown relative period.");
    }

    /// <summary>The instant range of an inclusive local date range: start inclusive, end exclusive.</summary>
    public static (DateTimeOffset StartInclusive, DateTimeOffset EndExclusive) ToInstants(DateOnly from, DateOnly to, TimeZoneInfo zone) =>
        (StartOfDay(from, zone), StartOfDay(to.AddDays(1), zone));

    /// <summary>Local midnight as an instant. A midnight skipped by a DST change moves to the first valid instant after it.</summary>
    public static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);

        while (zone.IsInvalidTime(local)) local = local.AddMinutes(30);

        // Ambiguous (repeated) local time: take the first occurrence, which is the earlier instant.
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
}
