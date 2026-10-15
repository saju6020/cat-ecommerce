namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-EMR-001 §8.9 -- when a follow-up reminder is sent. <c>SendAfter</c> is 09:00 branch-local on
/// <c>DueOn - leadDays</c>; if that instant has already passed the reminder is sent now. <c>NotAfter</c>
/// is the end of <c>DueOn</c> in the branch zone, so a reminder that could not be delivered in time is
/// dropped rather than sent late. Pure: the clock and the zone are inputs.
/// </summary>
public static class FollowUpSchedule
{
    public const int SendHourLocal = 9;

    public static DateTimeOffset SendAfter(DateOnly dueOn, int leadDays, DateTimeOffset now, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var sendLocalDate = dueOn.AddDays(-leadDays);
        var sendLocal = sendLocalDate.ToDateTime(new TimeOnly(SendHourLocal, 0), DateTimeKind.Unspecified);
        var sendInstant = new DateTimeOffset(sendLocal, zone.GetUtcOffset(sendLocal));

        // UTC offset: the same instant, in the only offset Npgsql accepts for timestamptz.
        return sendInstant <= now ? now : sendInstant.ToUniversalTime();
    }

    /// <summary>The first instant after <paramref name="dueOn"/> ends, in the branch zone.</summary>
    public static DateTimeOffset NotAfter(DateOnly dueOn, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var endLocal = dueOn.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(endLocal, zone.GetUtcOffset(endLocal)).ToUniversalTime();
    }
}
