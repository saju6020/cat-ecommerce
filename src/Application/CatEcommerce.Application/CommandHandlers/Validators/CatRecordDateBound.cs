namespace CatEcommerce.Application.CommandHandlers.Validators;

/// <summary>
/// Upper bound for the "cannot be in the future" pre-check on cat-record dates. A validator is
/// synchronous and cannot load the branch's IANA zone, so it uses the furthest-ahead civil date
/// anywhere on earth (UTC+14): it never refuses a date that is valid in the clinic's own zone
/// (Asia/Dhaka, UTC+6 -- a UTC clock refused today's local date from 00:00 to 06:00). The strict,
/// branch-local check stays in the aggregates, which receive <c>dto.Today</c> from
/// <c>ICatRecordClock.LocalTodayAsync</c> in the handler.
/// </summary>
internal static class CatRecordDateBound
{
    private static readonly TimeSpan MaxUtcOffset = TimeSpan.FromHours(14);

    public static DateOnly LatestPossibleLocalDate(TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.GetUtcNow().Add(MaxUtcOffset).UtcDateTime);
}
