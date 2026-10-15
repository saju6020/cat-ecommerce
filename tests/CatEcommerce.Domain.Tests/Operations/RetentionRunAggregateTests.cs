using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.Events.Operations;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.Tests.TestSupport;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Tests.Operations;

/// <summary>HO-XCT-001 U-C1 -- the retention run (section 8.4, ADR-303): each transition guarded by status, each raising one event.</summary>
public sealed class RetentionRunAggregateTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 2, 0, 0, TimeSpan.Zero);

    private static RetentionRunAggregate Requested()
    {
        var run = new RetentionRunAggregate(Guid.NewGuid());
        run.Request(new RetentionRunRequestDto(RetentionRunTrigger.Manual, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid()));
        return run;
    }

    private static RetentionRunAggregate Running()
    {
        var run = Requested();
        run.Start(new RetentionRunStartDto(Now, TestUserContext.Create(), Guid.NewGuid()));
        return run;
    }

    private static RetentionPassDto Pass(RetentionPassKind kind, int affected = 3, int failed = 0, params string[] codes) =>
        new(kind, Now, 5, affected, 0, failed, codes, 12, TestUserContext.Create(), Guid.NewGuid());

    private static RetentionRunFinishDto Finish() => new(Now.AddMinutes(1), TestUserContext.Create(), Guid.NewGuid());

    [Fact]
    public void Request_MovesToRequested_AndRaisesOneEvent()
    {
        var run = Requested();

        Assert.Equal(RetentionRunStatus.Requested, run.Status);
        Assert.Equal(RetentionRunTrigger.Manual, run.Trigger);
        Assert.IsType<RetentionRunRequestedEvent>(Assert.Single(run.Events));
    }

    // U-C1
    [Fact]
    public void U_C1_ARunCannotBeRequestedTwice()
    {
        var run = Requested();

        Assert.Throws<DomainRuleViolationException>(() =>
            run.Request(new RetentionRunRequestDto(RetentionRunTrigger.Scheduled, Guid.NewGuid(), Now, TestUserContext.Create(), Guid.NewGuid())));
    }

    [Fact]
    public void U_C1_StartIsRefused_UnlessRequested()
    {
        var neverRequested = new RetentionRunAggregate(Guid.NewGuid());
        Assert.Throws<DomainRuleViolationException>(() => neverRequested.Start(new RetentionRunStartDto(Now, TestUserContext.Create(), Guid.NewGuid())));

        var running = Running();
        Assert.Throws<DomainRuleViolationException>(() => running.Start(new RetentionRunStartDto(Now, TestUserContext.Create(), Guid.NewGuid())));
    }

    [Fact]
    public void U_C1_APassIsRefused_UnlessRunning()
    {
        Assert.Throws<DomainRuleViolationException>(() => Requested().RecordPass(Pass(RetentionPassKind.PurgeSessions)));

        var finished = Running();
        finished.Finish(Finish());
        Assert.Throws<DomainRuleViolationException>(() => finished.RecordPass(Pass(RetentionPassKind.PurgeSessions)));
    }

    [Fact]
    public void U_C1_FinishIsRefused_UnlessRunning_AndNotTwice()
    {
        Assert.Throws<DomainRuleViolationException>(() => Requested().Finish(Finish()));

        var run = Running();
        run.Finish(Finish());
        Assert.Throws<DomainRuleViolationException>(() => run.Finish(Finish()));
    }

    [Fact]
    public void RecordPass_RefusesNegativeCounts()
    {
        var run = Running();

        Assert.Throws<DomainRuleViolationException>(() => run.RecordPass(Pass(RetentionPassKind.PurgeSessions, affected: -1)));
    }

    [Fact]
    public void RecordPass_KeepsAtMostTenDistinctErrorCodes_AndNeverAMessage()
    {
        var run = Running();
        var codes = Enumerable.Range(1, 15).Select(i => $"Code{i}").Append("Code1").ToArray();

        run.RecordPass(Pass(RetentionPassKind.PurgeSources, failed: 1, codes: codes));

        var pass = Assert.Single(run.Passes);
        Assert.Equal(10, pass.ErrorCodes.Length);
        Assert.Equal(pass.ErrorCodes.Distinct().Count(), pass.ErrorCodes.Length);
    }

    [Fact]
    public void Finish_IsSucceeded_WhenNoPassFailed()
    {
        var run = Running();
        run.RecordPass(Pass(RetentionPassKind.PurgeSessions, affected: 4));
        run.RecordPass(Pass(RetentionPassKind.ReviewCounts, affected: 0));

        run.Finish(Finish());

        Assert.Equal(RetentionRunStatus.Succeeded, run.Status);
        var finished = run.Events.OfType<RetentionRunFinishedEvent>().Single();
        Assert.Equal(2, finished.PassCount);
        Assert.Equal(4, finished.TotalAffected);
        Assert.NotNull(run.FinishedOn);
    }

    [Fact]
    public void Finish_IsCompletedWithErrors_WhenAnyPassFailedOrHasACode()
    {
        var run = Running();
        run.RecordPass(Pass(RetentionPassKind.PurgeSessions));
        run.RecordPass(Pass(RetentionPassKind.PurgeSources, failed: 1, codes: "PurgeSourceItemFailed"));

        run.Finish(Finish());

        Assert.Equal(RetentionRunStatus.CompletedWithErrors, run.Status);
    }

    [Fact]
    public void EveryTransition_RaisesExactlyOneEvent()
    {
        var run = Requested();
        Assert.Single(run.Events);
        run.Start(new RetentionRunStartDto(Now, TestUserContext.Create(), Guid.NewGuid()));
        Assert.Equal(2, run.Events.Count);
        run.RecordPass(Pass(RetentionPassKind.PurgeSessions));
        Assert.Equal(3, run.Events.Count);
        run.Finish(Finish());
        Assert.Equal(4, run.Events.Count);
    }
}
