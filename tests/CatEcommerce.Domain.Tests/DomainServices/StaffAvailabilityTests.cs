using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>
/// TEST gate seventh pass: U-C2/U-C4, previously BLOCKED. Pure function -- no database, same style
/// as <see cref="BranchCalendarTests"/> for its sibling domain service.
/// </summary>
public sealed class StaffAvailabilityTests
{
    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    private static StaffRosterAggregate BuildRoster(
        IReadOnlyList<ShiftSlotInput> currentSlots,
        IReadOnlyList<ShiftSlotInput>? nextSlots = null,
        DateOnly? nextEffectiveFrom = null,
        DateOnly? today = null)
    {
        var asOf = today ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var roster = new StaffRosterAggregate(Guid.NewGuid());
        roster.Open(Guid.NewGuid(), Guid.NewGuid(), TestUserContext.Create());
        roster.SetCurrentPattern(new RosterCurrentPatternSetDto(currentSlots, asOf, TestUserContext.Create(), Guid.NewGuid()));

        if (nextSlots is not null)
            roster.ScheduleNextPattern(new RosterNextPatternScheduleDto(nextSlots, nextEffectiveFrom!.Value, asOf, TestUserContext.Create(), Guid.NewGuid()));

        return roster;
    }

    /// <summary>U-C2: "Next pattern effective Wednesday" -- the day BEFORE the effective date uses
    /// the current pattern's own Wednesday slot; the day ON the effective date (a week later, also a
    /// Wednesday) uses the next pattern's own Wednesday slot instead. Both patterns deliberately
    /// carry a slot on the SAME day of week, at different times, so the only thing that can explain
    /// which time comes back for which date is <c>StaffAvailability</c>'s own per-date pattern
    /// selection (rule 3), not which pattern happens to have a slot at all.</summary>
    [Fact]
    public void Intervals_NextPatternEffectiveOnASpecificDate_EarlierDateUsesCurrent_LaterDateUsesNext()
    {
        var firstWednesday = NextDate(DayOfWeek.Wednesday);
        var secondWednesday = firstWednesday.AddDays(7);

        var roster = BuildRoster(
            currentSlots: [new ShiftSlotInput(DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(12, 0), null)],
            nextSlots: [new ShiftSlotInput(DayOfWeek.Wednesday, new TimeOnly(14, 0), new TimeOnly(18, 0), null)],
            nextEffectiveFrom: secondWednesday,
            today: firstWednesday);

        var results = StaffAvailability.Intervals(roster, [], [], [], "Asia/Dhaka", firstWednesday, secondWednesday, restrictToOpeningHours: false);

        Assert.Equal(2, results.Count);

        var beforeEffectiveDate = results.Single(r => r.Start.Date == firstWednesday.ToDateTime(TimeOnly.MinValue).Date);
        Assert.Equal(BranchCalendar.ToInstant(firstWednesday, new TimeOnly(9, 0), Dhaka), beforeEffectiveDate.Start);
        Assert.Equal(BranchCalendar.ToInstant(firstWednesday, new TimeOnly(12, 0), Dhaka), beforeEffectiveDate.End);

        var onEffectiveDate = results.Single(r => r.Start.Date == secondWednesday.ToDateTime(TimeOnly.MinValue).Date);
        Assert.Equal(BranchCalendar.ToInstant(secondWednesday, new TimeOnly(14, 0), Dhaka), onEffectiveDate.Start);
        Assert.Equal(BranchCalendar.ToInstant(secondWednesday, new TimeOnly(18, 0), Dhaka), onEffectiveDate.End);
    }

    /// <summary>U-C4: with <c>restrictToOpeningHours=false</c>, a shift entirely outside the branch's
    /// open intervals is still returned in full (not clipped), flagged <c>OutsideOpeningHours=true</c>.
    /// The contrasting <c>restrictToOpeningHours=true</c> case (the same shift, same open hours) is
    /// included too, since it is the same rule's other half and costs nothing extra here: no overlap
    /// at all means no interval survives clipping.</summary>
    [Fact]
    public void Intervals_ShiftOutsideOpeningHours_RestrictFalse_ReturnsFullIntervalFlagged_RestrictTrue_ReturnsNone()
    {
        var monday = NextDate(DayOfWeek.Monday);
        var roster = BuildRoster([new ShiftSlotInput(DayOfWeek.Monday, new TimeOnly(20, 0), new TimeOnly(22, 0), null)], today: monday);
        var branchOpenIntervals = new List<TimeInterval>
        {
            new(BranchCalendar.ToInstant(monday, new TimeOnly(9, 0), Dhaka), BranchCalendar.ToInstant(monday, new TimeOnly(17, 0), Dhaka))
        };

        var unrestricted = StaffAvailability.Intervals(roster, [], [], branchOpenIntervals, "Asia/Dhaka", monday, monday, restrictToOpeningHours: false);
        Assert.Single(unrestricted);
        Assert.Equal(BranchCalendar.ToInstant(monday, new TimeOnly(20, 0), Dhaka), unrestricted[0].Start);
        Assert.Equal(BranchCalendar.ToInstant(monday, new TimeOnly(22, 0), Dhaka), unrestricted[0].End);
        Assert.True(unrestricted[0].OutsideOpeningHours);

        var restricted = StaffAvailability.Intervals(roster, [], [], branchOpenIntervals, "Asia/Dhaka", monday, monday, restrictToOpeningHours: true);
        Assert.Empty(restricted);
    }

    /// <summary>ACCEPTANCE gate first pass, A6/U-C1 rule 5: a day fully covered by an Approved leave
    /// request produces no interval at all, even though the roster pattern has a shift that day. The
    /// negative half of the same rule -- "requested leave does not remove anything" -- is enforced by
    /// the CALLER only ever passing Approved leave into this parameter (see <c>Intervals</c>'s own
    /// rule-5 comment); proven here by calling with an empty list (what the caller does for a merely-
    /// Requested leave) and confirming the shift still shows.</summary>
    [Fact]
    public void Intervals_ApprovedLeaveCoveringTheDate_RemovesTheWholeDay()
    {
        var monday = NextDate(DayOfWeek.Monday);
        var roster = BuildRoster([new ShiftSlotInput(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), null)], today: monday);

        var leave = new LeaveRequestAggregate(Guid.NewGuid());
        leave.Request(new LeaveRequestCreateDto(roster.StaffAccountId, monday, monday, LeaveKind.Annual, null, TestUserContext.Create(), Guid.NewGuid()));
        leave.Approve(new LeaveDecisionDto(Guid.NewGuid(), null, DateTimeOffset.UtcNow, TestUserContext.Create(), Guid.NewGuid()));

        var withApprovedLeave = StaffAvailability.Intervals(roster, [], [leave], [], "Asia/Dhaka", monday, monday, restrictToOpeningHours: false);
        Assert.Empty(withApprovedLeave);

        // Not passed in at all -- exactly what the caller does for a Requested (undecided) leave.
        var withoutApprovedLeave = StaffAvailability.Intervals(roster, [], [], [], "Asia/Dhaka", monday, monday, restrictToOpeningHours: false);
        Assert.Single(withoutApprovedLeave);
    }

    /// <summary>A6/U-C1 rule 4, ExtraShift half: a day with no pattern slot at all still produces an
    /// interval once an ExtraShift exception is added for that day.</summary>
    [Fact]
    public void Intervals_ExtraShiftException_AddsAnIntervalOnADayWithNoPatternSlot()
    {
        var tuesday = NextDate(DayOfWeek.Tuesday);
        // The roster's only pattern slot is Monday; Tuesday has nothing of its own.
        var roster = BuildRoster([new ShiftSlotInput(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), null)], today: tuesday);

        var withoutException = StaffAvailability.Intervals(roster, [], [], [], "Asia/Dhaka", tuesday, tuesday, restrictToOpeningHours: false);
        Assert.Empty(withoutException);

        var extraShift = new RosterExceptionAggregate(Guid.NewGuid());
        extraShift.Create(new RosterExceptionCreateDto(roster.StaffAccountId, roster.BranchId, tuesday, RosterExceptionKind.ExtraShift,
            new TimeOnly(10, 0), new TimeOnly(12, 0), "Cover", TestUserContext.Create(), Guid.NewGuid()));

        var withException = StaffAvailability.Intervals(roster, [extraShift], [], [], "Asia/Dhaka", tuesday, tuesday, restrictToOpeningHours: false);
        Assert.Single(withException);
        Assert.Equal(BranchCalendar.ToInstant(tuesday, new TimeOnly(10, 0), Dhaka), withException[0].Start);
        Assert.Equal(BranchCalendar.ToInstant(tuesday, new TimeOnly(12, 0), Dhaka), withException[0].End);
    }

    /// <summary>A6/U-C1 rule 4, ShiftCancelled half: cancelling part of a pattern shift shortens it;
    /// cancelling the whole shift removes it entirely (also exercises rule 7's "never zero-length",
    /// since a fully-cancelled shift must produce no interval at all, not a zero-duration one).</summary>
    [Fact]
    public void Intervals_ShiftCancelledException_ShortensOrRemovesTheShift()
    {
        var monday = NextDate(DayOfWeek.Monday);
        var roster = BuildRoster([new ShiftSlotInput(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), null)], today: monday);

        var partialCancellation = new RosterExceptionAggregate(Guid.NewGuid());
        partialCancellation.Create(new RosterExceptionCreateDto(roster.StaffAccountId, roster.BranchId, monday, RosterExceptionKind.ShiftCancelled,
            new TimeOnly(9, 0), new TimeOnly(12, 0), "Half day off", TestUserContext.Create(), Guid.NewGuid()));

        var shortened = StaffAvailability.Intervals(roster, [partialCancellation], [], [], "Asia/Dhaka", monday, monday, restrictToOpeningHours: false);
        Assert.Single(shortened);
        Assert.Equal(BranchCalendar.ToInstant(monday, new TimeOnly(12, 0), Dhaka), shortened[0].Start);
        Assert.Equal(BranchCalendar.ToInstant(monday, new TimeOnly(17, 0), Dhaka), shortened[0].End);

        var fullCancellation = new RosterExceptionAggregate(Guid.NewGuid());
        fullCancellation.Create(new RosterExceptionCreateDto(roster.StaffAccountId, roster.BranchId, monday, RosterExceptionKind.ShiftCancelled,
            new TimeOnly(9, 0), new TimeOnly(17, 0), "Whole day off", TestUserContext.Create(), Guid.NewGuid()));

        var removed = StaffAvailability.Intervals(roster, [fullCancellation], [], [], "Asia/Dhaka", monday, monday, restrictToOpeningHours: false);
        Assert.Empty(removed);
    }

    /// <summary>A6/U-C1 rule 2 as exercised through <c>StaffAvailability</c> (the closure itself is
    /// <see cref="BranchCalendar"/>'s own rule, already covered by <see cref="BranchCalendarTests"/>;
    /// this proves the two compose correctly): with <c>restrictToOpeningHours=true</c> and an empty
    /// <c>branchOpenIntervals</c> for the day -- exactly what <c>BranchCalendar.OpenIntervals</c>
    /// returns for a fully-closed day -- the shift produces no interval, even though the roster has
    /// one.</summary>
    [Fact]
    public void Intervals_BranchClosedWholeDay_RestrictToOpeningHoursTrue_ReturnsNoIntervalThatDay()
    {
        var monday = NextDate(DayOfWeek.Monday);
        var roster = BuildRoster([new ShiftSlotInput(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0), null)], today: monday);

        var results = StaffAvailability.Intervals(roster, [], [], [], "Asia/Dhaka", monday, monday, restrictToOpeningHours: true);

        Assert.Empty(results);
    }

    /// <summary>A6/U-C1 rule 7: a pattern shift and an abutting ExtraShift exception on the same day
    /// (09:00-12:00 and 12:00-15:00) are merged into a single 09:00-15:00 interval, not returned as
    /// two adjacent ones.</summary>
    [Fact]
    public void Intervals_AbuttingPatternShiftAndExtraShift_MergeIntoOneInterval()
    {
        var monday = NextDate(DayOfWeek.Monday);
        var roster = BuildRoster([new ShiftSlotInput(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0), null)], today: monday);

        var extraShift = new RosterExceptionAggregate(Guid.NewGuid());
        extraShift.Create(new RosterExceptionCreateDto(roster.StaffAccountId, roster.BranchId, monday, RosterExceptionKind.ExtraShift,
            new TimeOnly(12, 0), new TimeOnly(15, 0), "Cover", TestUserContext.Create(), Guid.NewGuid()));

        var results = StaffAvailability.Intervals(roster, [extraShift], [], [], "Asia/Dhaka", monday, monday, restrictToOpeningHours: false);

        Assert.Single(results);
        Assert.Equal(BranchCalendar.ToInstant(monday, new TimeOnly(9, 0), Dhaka), results[0].Start);
        Assert.Equal(BranchCalendar.ToInstant(monday, new TimeOnly(15, 0), Dhaka), results[0].End);
    }

    private static DateOnly NextDate(DayOfWeek dayOfWeek)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        while (date.DayOfWeek != dayOfWeek) date = date.AddDays(1);
        return date;
    }
}
