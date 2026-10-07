using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.Domain.Tests.DomainServices;

/// <summary>HO-ADM-001 §15 U-B3/U-B4/U-B5. Pure function -- no database.</summary>
public sealed class BranchCalendarTests
{
    private static BranchAggregate DhakaBranch(params (DayOfWeek Day, TimeOnly Opens, TimeOnly Closes)[] hours)
    {
        var branch = new BranchAggregate(Guid.NewGuid());
        branch.Create(new BranchCreateDto("MAIN", "Main", "Asia/Dhaka", null, null, null, TestUserContext.Create(), Guid.NewGuid()));
        branch.SetWeeklyHours(new BranchWeeklyHoursDto(
            hours.Select(h => new OpeningIntervalInput(h.Day, h.Opens, h.Closes)).ToList(),
            TestUserContext.Create(), Guid.NewGuid()));
        return branch;
    }

    [Fact]
    public void OpenIntervals_ClosedDayHasNoInterval()
    {
        var branch = DhakaBranch((DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        // Find the next Friday, a day with no configured hours.
        var friday = NextDate(DayOfWeek.Friday);

        var result = BranchCalendar.OpenIntervals(branch, [], friday, friday);
        Assert.Empty(result);
    }

    [Fact]
    public void OpenIntervals_LunchBreakProducesTwoIntervals()
    {
        var branch = DhakaBranch(
            (DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
            (DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0)));
        var monday = NextDate(DayOfWeek.Monday);

        var result = BranchCalendar.OpenIntervals(branch, [], monday, monday);

        Assert.Equal(2, result.Count);
        Assert.Equal(TimeSpan.FromHours(6), result[0].Start.Offset); // Asia/Dhaka is fixed +06:00, no DST
    }

    [Fact]
    public void OpenIntervals_CancelledClosure_IsIgnored()
    {
        var branch = DhakaBranch((DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        var monday = NextDate(DayOfWeek.Monday);

        var closure = new BranchClosureAggregate(Guid.NewGuid());
        closure.Create(new BranchClosureCreateDto(branch.Id, monday, monday, ClosureKind.Closure, "Test", null, null, TestUserContext.Create(), Guid.NewGuid()));
        closure.Cancel(new BranchClosureCancelDto(monday.AddDays(-30), TestUserContext.Create(), Guid.NewGuid()));

        var result = BranchCalendar.OpenIntervals(branch, [closure], monday, monday);

        Assert.Single(result);
    }

    [Fact]
    public void OpenIntervals_HalfDayClosure_ShortensInterval()
    {
        var branch = DhakaBranch((DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)));
        var monday = NextDate(DayOfWeek.Monday);

        var closure = new BranchClosureAggregate(Guid.NewGuid());
        closure.Create(new BranchClosureCreateDto(branch.Id, monday, monday, ClosureKind.Closure, "Half day",
            new TimeOnly(13, 0), new TimeOnly(17, 0), TestUserContext.Create(), Guid.NewGuid()));

        var result = BranchCalendar.OpenIntervals(branch, [closure], monday, monday);

        Assert.Single(result);
        // The interval's own offset already encodes Dhaka's +06:00, so compare via the ToOffset(0) instant hour instead of relying on the test host's local time zone.
        Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromTimeSpan(result[0].Start.TimeOfDay));
        Assert.Equal(new TimeOnly(13, 0), TimeOnly.FromTimeSpan(result[0].End.TimeOfDay));
    }

    [Fact]
    public void OpenIntervals_TwoDisjointPartialClosures_BothAreRemoved()
    {
        var branch = DhakaBranch((DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0)));
        var monday = NextDate(DayOfWeek.Monday);

        var morning = new BranchClosureAggregate(Guid.NewGuid());
        morning.Create(new BranchClosureCreateDto(branch.Id, monday, monday, ClosureKind.Closure, "Morning maintenance",
            new TimeOnly(9, 0), new TimeOnly(10, 0), TestUserContext.Create(), Guid.NewGuid()));
        var afternoon = new BranchClosureAggregate(Guid.NewGuid());
        afternoon.Create(new BranchClosureCreateDto(branch.Id, monday, monday, ClosureKind.Closure, "Afternoon training",
            new TimeOnly(15, 0), new TimeOnly(16, 0), TestUserContext.Create(), Guid.NewGuid()));

        var result = BranchCalendar.OpenIntervals(branch, [morning, afternoon], monday, monday);

        // 09:00-18:00 minus [09:00-10:00] and [15:00-16:00] -> [10:00-15:00], [16:00-18:00].
        Assert.Equal(2, result.Count);
        Assert.Equal(new TimeOnly(10, 0), TimeOnly.FromTimeSpan(result[0].Start.TimeOfDay));
        Assert.Equal(new TimeOnly(15, 0), TimeOnly.FromTimeSpan(result[0].End.TimeOfDay));
        Assert.Equal(new TimeOnly(16, 0), TimeOnly.FromTimeSpan(result[1].Start.TimeOfDay));
        Assert.Equal(new TimeOnly(18, 0), TimeOnly.FromTimeSpan(result[1].End.TimeOfDay));
    }

    [Fact]
    public void OpenIntervals_ThreeOverlappingPartialClosures_AllAreFolded()
    {
        var branch = DhakaBranch((DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(18, 0)));
        var monday = NextDate(DayOfWeek.Monday);

        var closureA = new BranchClosureAggregate(Guid.NewGuid());
        closureA.Create(new BranchClosureCreateDto(branch.Id, monday, monday, ClosureKind.Closure, "A",
            new TimeOnly(9, 0), new TimeOnly(12, 0), TestUserContext.Create(), Guid.NewGuid()));
        var closureB = new BranchClosureAggregate(Guid.NewGuid());
        closureB.Create(new BranchClosureCreateDto(branch.Id, monday, monday, ClosureKind.Closure, "B",
            new TimeOnly(11, 0), new TimeOnly(14, 0), TestUserContext.Create(), Guid.NewGuid()));
        var closureC = new BranchClosureAggregate(Guid.NewGuid());
        closureC.Create(new BranchClosureCreateDto(branch.Id, monday, monday, ClosureKind.Closure, "C",
            new TimeOnly(16, 0), new TimeOnly(18, 0), TestUserContext.Create(), Guid.NewGuid()));

        var result = BranchCalendar.OpenIntervals(branch, [closureA, closureB, closureC], monday, monday);

        // 09:00-18:00 minus [09:00-12:00, 11:00-14:00, 16:00-18:00] (folded, overlapping A/B) -> only 14:00-16:00 remains.
        Assert.Single(result);
        Assert.Equal(new TimeOnly(14, 0), TimeOnly.FromTimeSpan(result[0].Start.TimeOfDay));
        Assert.Equal(new TimeOnly(16, 0), TimeOnly.FromTimeSpan(result[0].End.TimeOfDay));
    }

    [Fact]
    public void ToInstant_LondonSpringForwardGap_AdvancesToFirstValidInstant()
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        // 2027-03-28 is the last Sunday of March 2027 -- clocks jump 01:00 -> 02:00 local.
        var gapDate = new DateOnly(2027, 3, 28);

        var instant = BranchCalendar.ToInstant(gapDate, new TimeOnly(1, 30), london);

        // Compare via the DateTimeOffset's own wall-clock time (its Offset already encodes
        // London's), never .LocalDateTime, which would convert into the test host's own zone.
        Assert.Equal(new TimeOnly(2, 0), TimeOnly.FromTimeSpan(instant.TimeOfDay));
        Assert.Equal(TimeSpan.FromHours(1), instant.Offset);
    }

    [Fact]
    public void ToInstant_LondonFallBackAmbiguity_ResolvesToEarlierOffset()
    {
        var london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        // 2027-10-31 is the last Sunday of October 2027 -- 01:00-02:00 local occurs twice.
        var ambiguousDate = new DateOnly(2027, 10, 31);

        var instant = BranchCalendar.ToInstant(ambiguousDate, new TimeOnly(1, 30), london);

        Assert.Equal(TimeSpan.FromHours(1), instant.Offset); // the earlier (still-BST) occurrence
    }

    [Fact]
    public void LocalDateOf_ConvertsInstantBackToBranchLocalDate()
    {
        var instant = new DateTimeOffset(2026, 6, 15, 23, 30, 0, TimeSpan.FromHours(6)); // 23:30 Dhaka time
        var localDate = BranchCalendar.LocalDateOf(instant.ToUniversalTime(), "Asia/Dhaka");

        Assert.Equal(new DateOnly(2026, 6, 15), localDate);
    }

    private static DateOnly NextDate(DayOfWeek dayOfWeek)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        while (date.DayOfWeek != dayOfWeek) date = date.AddDays(1);
        return date;
    }
}
