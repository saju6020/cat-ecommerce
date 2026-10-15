using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.Events.Operations;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Operations;

/// <summary>HO-XCT-001 U-F1..U-F6 -- the restore-test record (section 8.5, ADR-313): RPO, RTO and the outcome are computed, never supplied.</summary>
public sealed class RestoreTestRecordAggregateTests
{
    private static readonly DateTimeOffset Failure = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 12);

    private static RestoreTestDto Dto(
        int recoveredMinutesBefore = 8, int restoreMinutes = 40, bool allPass = true, DateOnly? performedOn = null, IReadOnlyList<RestoreCheckDto>? checks = null)
    {
        var started = Failure.AddMinutes(10);
        return new RestoreTestDto(
            performedOn ?? Today, Guid.NewGuid(), "drill host, isolated", "20261012-010000F", Failure, Failure.AddMinutes(-recoveredMinutesBefore),
            started, started.AddMinutes(restoreMinutes),
            checks ?? [new RestoreCheckDto("migrations-current", true, null), new RestoreCheckDto("row-counts-nonzero", allPass, null)],
            "{}", null, null, Today, TestUserContext.Create(), Guid.NewGuid());
    }

    private static RestoreTestRecordAggregate Record(RestoreTestDto dto)
    {
        var record = new RestoreTestRecordAggregate(Guid.NewGuid());
        record.Record(dto);
        return record;
    }

    // U-F1
    [Fact]
    public void U_F1_RpoIsEightMinutes_RtoFortyMinutes_AndAllChecksPassing_IsPassed()
    {
        var record = Record(Dto());

        Assert.Equal(8m, record.AchievedRpoMinutes);
        Assert.Equal(40m, record.AchievedRtoMinutes);
        Assert.Equal(RestoreTestOutcome.Passed, record.Outcome);
        var recorded = Assert.IsType<RestoreTestRecordedEvent>(Assert.Single(record.Events));
        Assert.Equal(RestoreTestOutcome.Passed, recorded.TestOutcome);
    }

    // U-F2
    [Fact]
    public void U_F2_AnRpoOfSixteenMinutes_IsFailed_FifteenIsStillPassed()
    {
        Assert.Equal(RestoreTestOutcome.Failed, Record(Dto(recoveredMinutesBefore: 16)).Outcome);
        Assert.Equal(RestoreTestOutcome.Passed, Record(Dto(recoveredMinutesBefore: 15)).Outcome);
    }

    // U-F3
    [Fact]
    public void U_F3_AnRtoOfTwoHundredAndFortyOneMinutes_IsFailed_TwoForty_IsPassed()
    {
        Assert.Equal(RestoreTestOutcome.Failed, Record(Dto(restoreMinutes: 241)).Outcome);
        Assert.Equal(RestoreTestOutcome.Passed, Record(Dto(restoreMinutes: 240)).Outcome);
    }

    // U-F4
    [Fact]
    public void U_F4_OneFailedCheck_IsFailed_EvenWithGoodNumbers() => Assert.Equal(RestoreTestOutcome.Failed, Record(Dto(allPass: false)).Outcome);

    // U-F5
    [Fact]
    public void U_F5_BadTimesAndNoChecks_AreRefusedWithTheirOwnCodes()
    {
        var recoveredAfterFailure = Assert.Throws<DomainRuleViolationException>(() => Record(Dto(recoveredMinutesBefore: -1)));
        Assert.Equal(BusinessViolationCodes.InvalidRestoreTestTimes, recoveredAfterFailure.ErrorCode);

        var completedBeforeStarted = Assert.Throws<DomainRuleViolationException>(() => Record(Dto(restoreMinutes: 0)));
        Assert.Equal(BusinessViolationCodes.InvalidRestoreTestTimes, completedBeforeStarted.ErrorCode);

        var future = Assert.Throws<DomainRuleViolationException>(() => Record(Dto(performedOn: Today.AddDays(1))));
        Assert.Equal(BusinessViolationCodes.InvalidRestoreTestTimes, future.ErrorCode);

        var none = Assert.Throws<DomainRuleViolationException>(() => Record(Dto(checks: [])));
        Assert.Equal(BusinessViolationCodes.RestoreTestChecksRequired, none.ErrorCode);
    }

    // U-F6
    [Fact]
    public void U_F6_ARecordCannotBeRecordedTwice()
    {
        var record = Record(Dto());

        var second = Assert.Throws<DomainRuleViolationException>(() => record.Record(Dto()));

        Assert.Equal(BusinessViolationCodes.RestoreTestAlreadyRecorded, second.ErrorCode);
        Assert.Equal(RestoreTestOutcome.Passed, record.Outcome);
    }

    [Fact]
    public void ThereIsNoOtherPublicMutator()
    {
        var mutators = typeof(RestoreTestRecordAggregate).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName).Select(m => m.Name).ToList();

        Assert.Equal(["Record"], mutators);
    }
}
