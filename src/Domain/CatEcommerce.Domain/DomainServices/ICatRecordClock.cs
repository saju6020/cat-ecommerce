namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Cat-owned "today" for cat records (bug fix 2026-10-10). Cat commands carry no branch, so this is the
/// main branch's calendar date (<c>BranchCalendar.LocalDateOf</c>, Asia/Dhaka fallback) -- never the UTC
/// date, which is a day behind the clinic from 00:00 to 06:00 local.
/// </summary>
public interface ICatRecordClock
{
    Task<DateOnly> LocalTodayAsync(CancellationToken ct);
}
