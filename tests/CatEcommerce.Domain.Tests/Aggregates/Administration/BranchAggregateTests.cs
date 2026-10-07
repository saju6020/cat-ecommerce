using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.Domain.Tests.Aggregates.Administration;

/// <summary>HO-ADM-001 §15, U-B1/U-B2.</summary>
public sealed class BranchAggregateTests
{
    private static BranchCreateDto ValidCreate() => new(
        "MAIN", "Main clinic", "Asia/Dhaka", null, null, null, TestUserContext.Create(), Guid.NewGuid());

    private static BranchAggregate CreatedBranch()
    {
        var branch = new BranchAggregate(Guid.NewGuid());
        branch.Create(ValidCreate());
        return branch;
    }

    [Fact]
    public void Create_NormalisesCodeToUpperCase()
    {
        var branch = new BranchAggregate(Guid.NewGuid());
        branch.Create(ValidCreate() with { });
        Assert.Equal("MAIN", branch.Code);
    }

    [Fact]
    public void Create_UnknownTimeZone_ThrowsInvalidTimeZone()
    {
        var branch = new BranchAggregate(Guid.NewGuid());
        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            branch.Create(new BranchCreateDto("MAIN", "Main", "Europe/Nowhere", null, null, null, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.InvalidTimeZone, ex.ErrorCode);
    }

    [Fact]
    public void SetWeeklyHours_ReversedInterval_ThrowsInvalidOpeningInterval()
    {
        var branch = CreatedBranch();
        var ex = Assert.Throws<DomainRuleViolationException>(() => branch.SetWeeklyHours(new BranchWeeklyHoursDto(
            [new OpeningIntervalInput(DayOfWeek.Monday, new TimeOnly(17, 0), new TimeOnly(9, 0))],
            TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.InvalidOpeningInterval, ex.ErrorCode);
    }

    [Fact]
    public void SetWeeklyHours_OverlappingIntervalsSameDay_Throws()
    {
        var branch = CreatedBranch();
        var ex = Assert.Throws<DomainRuleViolationException>(() => branch.SetWeeklyHours(new BranchWeeklyHoursDto(
            [
                new OpeningIntervalInput(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
                new OpeningIntervalInput(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(17, 0)),
            ],
            TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.InvalidOpeningInterval, ex.ErrorCode);
    }

    [Fact]
    public void SetWeeklyHours_FourIntervalsInOneDay_Throws()
    {
        var branch = CreatedBranch();
        var intervals = new List<OpeningIntervalInput>
        {
            new(DayOfWeek.Monday, new TimeOnly(6, 0), new TimeOnly(7, 0)),
            new(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(9, 0)),
            new(DayOfWeek.Monday, new TimeOnly(10, 0), new TimeOnly(11, 0)),
            new(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(13, 0)),
        };

        var ex = Assert.Throws<DomainRuleViolationException>(() =>
            branch.SetWeeklyHours(new BranchWeeklyHoursDto(intervals, TestUserContext.Create(), Guid.NewGuid())));

        Assert.Equal(BusinessViolationCodes.InvalidOpeningInterval, ex.ErrorCode);
    }

    [Fact]
    public void SetWeeklyHours_ValidWithLunchBreak_Succeeds()
    {
        var branch = CreatedBranch();
        branch.SetWeeklyHours(new BranchWeeklyHoursDto(
            [
                new OpeningIntervalInput(DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)),
                new OpeningIntervalInput(DayOfWeek.Monday, new TimeOnly(14, 0), new TimeOnly(18, 0)),
            ],
            TestUserContext.Create(), Guid.NewGuid()));

        Assert.Equal(2, branch.WeeklyHours.Count);
    }

    [Fact]
    public void SetActivation_SameState_IsNoOpAndRaisesNoEvent()
    {
        var branch = CreatedBranch();
        branch.ClearEvents();
        branch.SetActivation(new BranchActivationDto(true, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Empty(branch.Events);
    }
}
