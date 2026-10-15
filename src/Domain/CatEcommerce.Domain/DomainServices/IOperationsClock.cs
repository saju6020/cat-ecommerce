namespace CatEcommerce.Domain.DomainServices;

/// <summary>The current instant seen from the main branch: its time zone, local date and local hour.</summary>
public sealed record MainBranchTime(DateTimeOffset Utc, string TimeZoneId, DateOnly LocalDate, int LocalHour);

/// <summary>
/// HO-XCT-001 section 8.8 / 12.2 -- the one place the Operations context learns "today". Hold dates, retention cut-offs and the nightly
/// sweep hour are calendar concepts of the clinic, not of UTC: the main branch's zone (<c>Asia/Dhaka</c> when unset) decides them, via
/// <c>BranchCalendar</c> and the injected <see cref="TimeProvider"/> (a test drives it with a fake). Never <c>DateTime.Today</c>.
/// </summary>
public interface IOperationsClock
{
    Task<MainBranchTime> NowAsync(CancellationToken ct);

    /// <summary>The main-branch calendar date of an arbitrary instant (an eraser is told the instant it runs as of).</summary>
    Task<DateOnly> LocalDateOfAsync(DateTimeOffset instant, CancellationToken ct);
}
