using CatEcommerce.Application.CommandHandlers.Operations;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Retention;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Operations;

/// <summary>HO-XCT-001 U-C2, U-C4, U-C5 and the C5..C9 validators and purge handlers.</summary>
public sealed class RetentionCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly UserContext Admin = new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["Administrator"] };

    private static RetentionRunAggregate Run(RetentionRunTrigger trigger = null!, bool started = false, DateTimeOffset? startedOn = null)
    {
        var run = new RetentionRunAggregate(Guid.NewGuid());
        run.Request(new RetentionRunRequestDto(trigger ?? RetentionRunTrigger.Manual, Admin.UserId, Now, Admin, Guid.NewGuid()));
        if (started) run.Start(new RetentionRunStartDto(startedOn ?? Now, Admin, Guid.NewGuid()));
        return run;
    }

    private static RequestRetentionRunCommand Request() => new()
    {
        RetentionRunId = Guid.NewGuid(), Trigger = RetentionRunTrigger.Manual.DisplayName, UserContext = Admin, CorrelationId = Guid.NewGuid()
    };

    // U-C2
    [Fact]
    public async Task U_C2_ARequestWhileARunIsRunning_IsRefused_RetentionRunAlreadyInProgress()
    {
        var runs = new FakeAggregateRootRepository<RetentionRunAggregate>();
        runs.Seed(Run(started: true, startedOn: Now.AddMinutes(-5)));
        var handler = new RequestRetentionRunCommandHandler(runs, new FakeTimeProvider(Now));

        var response = await handler.HandleAsync(Request());

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.RetentionRunAlreadyInProgress);
        Assert.Empty(runs.Saved);
    }

    [Fact]
    public async Task U_C2_ARequestWhileARunIsRequested_IsRefused()
    {
        var runs = new FakeAggregateRootRepository<RetentionRunAggregate>();
        runs.Seed(Run());
        var handler = new RequestRetentionRunCommandHandler(runs, new FakeTimeProvider(Now));

        var response = await handler.HandleAsync(Request());

        Assert.False(response.IsSuccess);
        Assert.Empty(runs.Saved);
    }

    [Fact]
    public async Task ARunLeftRunningByACrashedWorker_NoLongerBlocks()
    {
        var runs = new FakeAggregateRootRepository<RetentionRunAggregate>();
        runs.Seed(Run(started: true, startedOn: Now.AddHours(-(RetentionRules.StaleRunHours + 1))));
        var handler = new RequestRetentionRunCommandHandler(runs, new FakeTimeProvider(Now));

        var response = await handler.HandleAsync(Request());

        Assert.True(response.IsSuccess);
        Assert.Single(runs.Saved);
    }

    [Fact]
    public async Task AFinishedRun_DoesNotBlockANewOne()
    {
        var runs = new FakeAggregateRootRepository<RetentionRunAggregate>();
        var done = Run(started: true);
        done.Finish(new RetentionRunFinishDto(Now, Admin, Guid.NewGuid()));
        runs.Seed(done);
        var handler = new RequestRetentionRunCommandHandler(runs, new FakeTimeProvider(Now));

        var response = await handler.HandleAsync(Request());

        Assert.True(response.IsSuccess);
        Assert.Equal(RetentionRunStatus.Requested, Assert.Single(runs.Saved).Status);
    }

    [Fact]
    public async Task StartRecordFinish_DriveTheRunThroughItsLifecycle_AndReportAMissingRun()
    {
        var runs = new FakeAggregateRootRepository<RetentionRunAggregate>();
        var run = Run();
        runs.Seed(run);
        var time = new FakeTimeProvider(Now);

        Assert.True((await new StartRetentionRunCommandHandler(runs, time).HandleAsync(
            new StartRetentionRunCommand { RetentionRunId = run.Id, UserContext = Admin, CorrelationId = Guid.NewGuid() })).IsSuccess);
        Assert.True((await new RecordRetentionPassCommandHandler(runs).HandleAsync(new RecordRetentionPassCommand
        {
            RetentionRunId = run.Id, PassKind = RetentionPassKind.PurgeSessions.DisplayName, Affected = 2, UserContext = Admin, CorrelationId = Guid.NewGuid()
        })).IsSuccess);
        var finished = await new FinishRetentionRunCommandHandler(runs, time).HandleAsync(
            new FinishRetentionRunCommand { RetentionRunId = run.Id, UserContext = Admin, CorrelationId = Guid.NewGuid() });

        Assert.True(finished.IsSuccess);
        Assert.Equal(RetentionRunStatus.Succeeded, run.Status);

        var missing = await new StartRetentionRunCommandHandler(runs, time).HandleAsync(
            new StartRetentionRunCommand { RetentionRunId = Guid.NewGuid(), UserContext = Admin, CorrelationId = Guid.NewGuid() });
        Assert.False(missing.IsSuccess);
    }

    [Fact]
    public async Task APurgeKindThatIsNotHandledByThePurger_IsAProgrammingError()
    {
        var handler = new PurgeExpiredRecordsCommandHandler(
            new FakePurger(), new FakeAggregateRootRepository<AuditEntryAggregate>(), new FakeClientIpAccessor(), new FakeTimeProvider(Now));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(new PurgeExpiredRecordsCommand
        {
            PassKind = RetentionPassKind.ReviewCounts.DisplayName, Cutoff = Now, BatchSize = 10, UserContext = Admin, CorrelationId = Guid.NewGuid()
        }));
    }

    [Fact]
    public async Task APurge_WritesOneBusinessEventWithCountsOnly_AndAnEmptyBatchWritesNothing()
    {
        var audit = new FakeAggregateRootRepository<AuditEntryAggregate>();
        var purger = new FakePurger { Sessions = 4 };
        var handler = new PurgeExpiredRecordsCommandHandler(purger, audit, new FakeClientIpAccessor(), new FakeTimeProvider(Now));
        var command = new PurgeExpiredRecordsCommand
        {
            PassKind = RetentionPassKind.PurgeSessions.DisplayName, Cutoff = Now.AddDays(-30), BatchSize = 500, UserContext = Admin, CorrelationId = Guid.NewGuid()
        };

        var response = await handler.HandleAsync(command);

        Assert.True(response.IsSuccess);
        Assert.Equal(8, response.Result);
        var entry = Assert.Single(audit.Saved);
        Assert.Equal("RetentionPurge", entry.Action);
        Assert.Equal(AuditCategory.BusinessEvent, entry.Category);

        purger.Sessions = 0;
        await handler.HandleAsync(command);
        Assert.Single(audit.Saved);
    }

    [Fact]
    public async Task APurgeSourceThatIsNotRegistered_IsReported_NotThrown()
    {
        var handler = new RunPurgeSourceCommandHandler(
            [], new FakeAggregateRootRepository<AuditEntryAggregate>(), new FakeClientIpAccessor(), new FakeTimeProvider(Now));

        var response = await handler.HandleAsync(new RunPurgeSourceCommand
        {
            SourceKey = "nope", Cutoff = Now, BatchSize = 10, UserContext = Admin, CorrelationId = Guid.NewGuid()
        });

        Assert.False(response.IsSuccess);
    }

    [Fact]
    public async Task APurgeSource_IsCalledWithTheCutoff_AndItsResultIsReturnedAndAudited()
    {
        var audit = new FakeAggregateRootRepository<AuditEntryAggregate>();
        var source = new FakePurgeSource("boarding-media", new PurgeResult(5, 3, 1, 1));
        var handler = new RunPurgeSourceCommandHandler([source], audit, new FakeClientIpAccessor(), new FakeTimeProvider(Now));
        var cutoff = Now.AddMonths(-12);

        var response = await handler.HandleAsync(new RunPurgeSourceCommand
        {
            SourceKey = "boarding-media", Cutoff = cutoff, BatchSize = 500, UserContext = Admin, CorrelationId = Guid.NewGuid()
        });

        Assert.Equal(cutoff, source.LastCutoff);
        Assert.Equal(new PurgeResult(5, 3, 1, 1), response.Result);
        Assert.Single(audit.Saved);
    }

    // U-C4
    [Fact]
    public void U_C4_01_59_No_02_00_Yes_02_30_AfterARunHasStarted_No()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");
        MainBranchTime At(int hour, int minute)
        {
            var local = new DateTimeOffset(2026, 10, 12, hour, minute, 0, zone.GetUtcOffset(new DateTime(2026, 10, 12)));
            var utc = local.ToUniversalTime();
            return new MainBranchTime(utc, "Asia/Dhaka", new DateOnly(2026, 10, 12), hour);
        }

        var start = At(2, 0).Utc;

        Assert.False(RetentionSchedule.IsDailyRunDue(At(1, 59), 2, null));
        Assert.True(RetentionSchedule.IsDailyRunDue(At(2, 0), 2, null));
        Assert.False(RetentionSchedule.IsDailyRunDue(At(2, 30), 2, start));
        // A run that started on the previous local date does not block today's.
        Assert.True(RetentionSchedule.IsDailyRunDue(At(2, 30), 2, start.AddDays(-1)));
    }

    [Fact]
    public void TheErasurePassIsDueEveryFifteenMinutes()
    {
        Assert.True(RetentionSchedule.IsErasurePassDue(Now, null));
        Assert.False(RetentionSchedule.IsErasurePassDue(Now, Now.AddMinutes(-14)));
        Assert.True(RetentionSchedule.IsErasurePassDue(Now, Now.AddMinutes(-15)));
    }

    // U-C5
    [Theory]
    [InlineData(1, 15)]
    [InlineData(2, 60)]
    [InlineData(3, 240)]
    [InlineData(4, 1440)]
    [InlineData(9, 1440)]
    public void U_C5_TheErasureBackoff_IsFifteen_Sixty_TwoForty_ThenADay(int attempts, int minutes) =>
        Assert.Equal(minutes, RetentionRules.BackoffAfter(attempts));

    [Fact]
    public void TheCommandValidators_RefuseBadShapes()
    {
        Assert.False(new RequestRetentionRunCommandValidator().Validate(new RequestRetentionRunCommand
        {
            RetentionRunId = Guid.Empty, Trigger = "Weekly", UserContext = Admin, CorrelationId = Guid.NewGuid()
        }).IsValid);
        Assert.False(new PurgeExpiredRecordsCommandValidator().Validate(new PurgeExpiredRecordsCommand
        {
            PassKind = "PurgeSessions", Cutoff = Now, BatchSize = 0, UserContext = Admin, CorrelationId = Guid.NewGuid()
        }).IsValid);
        Assert.False(new RedactExpiredNotificationContentCommandValidator().Validate(new RedactExpiredNotificationContentCommand
        {
            UserContext = Admin, CorrelationId = Guid.NewGuid()
        }).IsValid);
        Assert.False(new RedactExpiredNotificationContentCommandValidator().Validate(new RedactExpiredNotificationContentCommand
        {
            NotificationId = Guid.NewGuid(), RequestId = Guid.NewGuid(), UserContext = Admin, CorrelationId = Guid.NewGuid()
        }).IsValid);
        Assert.True(new RedactExpiredNotificationContentCommandValidator().Validate(new RedactExpiredNotificationContentCommand
        {
            NotificationId = Guid.NewGuid(), UserContext = Admin, CorrelationId = Guid.NewGuid()
        }).IsValid);
    }

    private sealed class FakePurger : IRetentionPurger
    {
        public int Sessions { get; set; }
        public Task<int> PurgeCustomerSessionsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct) => Task.FromResult(Sessions);
        public Task<int> PurgeStaffSessionsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct) => Task.FromResult(Sessions);
        public Task<int> PurgePasswordResetTokensAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct) => Task.FromResult(0);
        public Task<int> PurgeWebVitalBatchesAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct) => Task.FromResult(0);
        public Task<int> PurgeBackupRunsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct) => Task.FromResult(0);
    }

    private sealed class FakePurgeSource(string key, PurgeResult result) : IRetentionPurgeSource
    {
        public DateTimeOffset? LastCutoff { get; private set; }
        public string SourceKey => key;

        public Task<PurgeResult> PurgeAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
        {
            LastCutoff = cutoff;
            return Task.FromResult(result);
        }
    }
}
