using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Infrastructure.Retention;

/// <summary>
/// HO-XCT-001 section 12.2 -- when the retention worker runs its passes, as pure functions of the main-branch clock (so they can be tested with a fake
/// one). Calendar concepts are the clinic's, never UTC's and never <c>DateTime.Today</c>.
/// </summary>
public static class RetentionSchedule
{
    /// <summary>
    /// The daily passes are due when the main branch's local hour equals <c>retention.sweepHour</c> and no scheduled run has started on that local date.
    /// </summary>
    public static bool IsDailyRunDue(MainBranchTime now, int sweepHour, DateTimeOffset? lastScheduledRunStart)
    {
        if (now.LocalHour != sweepHour) return false;
        return lastScheduledRunStart is null || BranchCalendar.LocalDateOf(lastScheduledRunStart.Value, now.TimeZoneId) != now.LocalDate;
    }

    /// <summary>The erasure pass runs every <see cref="RetentionRules.ErasurePassMinutes"/> minutes.</summary>
    public static bool IsErasurePassDue(DateTimeOffset now, DateTimeOffset? lastErasurePass) =>
        lastErasurePass is null || now - lastErasurePass.Value >= TimeSpan.FromMinutes(RetentionRules.ErasurePassMinutes);
}
