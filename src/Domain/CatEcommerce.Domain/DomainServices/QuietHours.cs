using CatEcommerce.Domain.ValueObjects;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-NTF-001 §8.14 / ADR-059 -- pure function, no I/O, exactly like
/// <see cref="BranchCalendar"/>. Reuses <see cref="BranchCalendar.ToInstant"/> (internal, same
/// assembly) for the same DST-safe local-to-instant conversion.</summary>
public static class QuietHours
{
    /// <summary>Null when <paramref name="now"/> falls outside <paramref name="quiet"/> in
    /// <paramref name="zone"/>; otherwise the instant the window ends.</summary>
    public static DateTimeOffset? NextAllowedInstant(DateTimeOffset now, TimeRange quiet, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var localDate = DateOnly.FromDateTime(local.DateTime);
        var localTime = TimeOnly.FromDateTime(local.DateTime);

        if (!quiet.Contains(localTime)) return null;

        var endDate = quiet.Start <= quiet.End
            ? localDate
            : localTime >= quiet.Start ? localDate.AddDays(1) : localDate;

        // Gate fix (found by an integration test, not just a unit test): BranchCalendar.ToInstant
        // returns a DateTimeOffset carrying the BRANCH's own UTC offset (e.g. +06:00 for Asia/Dhaka),
        // never zero. Npgsql refuses to write a non-zero-offset DateTimeOffset to a `timestamptz`
        // column outright (ArgumentException, not merely a warning) -- every quiet-hours deferral
        // for a branch outside UTC would otherwise crash NotificationAggregate.CreateExternal's
        // persistence. Normalised to UTC here, at the one place this domain service hands a value
        // to a caller that stores it, rather than in BranchCalendar itself (ADM's own file, outside
        // this handover's scope, and other callers may rely on its current return shape).
        return BranchCalendar.ToInstant(endDate, quiet.End, zone).ToUniversalTime();
    }
}
