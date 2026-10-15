using System.Diagnostics;
using CatEcommerce.Application.Commands.Operations;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Retention;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-XCT-001 section 12.2 / ADR-303 -- the units of work of the retention worker, on HO-NTF-001 shape. Scoped. Each unit is a fresh DI scope and ONE
/// command, run as <see cref="SystemActors.RetentionWorker"/> with a new correlation id; an exception is logged with the id and swallowed so one bad
/// unit can never stop the pass. Tests call these methods directly with a fake clock (the hosted worker is off in Testing).
/// </summary>
public sealed class RetentionWorkCycle(
    ILogger<RetentionWorkCycle> logger,
    IServiceScopeFactory scopeFactory,
    RetentionWorkQueue workQueue,
    IOperationsClock clock,
    IBusinessSettings settings)
{
    private static UserContext WorkerContext => new(SystemActors.RetentionWorker, Guid.Empty);

    /// <summary>
    /// Self-heal first (an Anonymised account with no ledger gets one: C1), then C2 for each due scope: Pending, Failing past its back-off, and Held
    /// whose date has passed. Returns the number of units attempted.
    /// </summary>
    public async Task<int> RunErasurePassAsync(CancellationToken ct = default)
    {
        var time = await clock.NowAsync(ct);
        var units = 0;

        foreach (var account in await workQueue.GetAnonymisedAccountsWithoutLedgerAsync(RetentionRules.BatchSize, ct))
        {
            units++;
            await SendAsync(new OpenErasureRequestCommand
            {
                CustomerAccountId = account.CustomerAccountId,
                RequestedOn = account.AnonymisedOn ?? time.Utc,
                // The origin of an erasure whose ledger had to be re-derived is not recorded on the account; the self-service path is the default one.
                Source = ErasureRequestSource.SelfService.DisplayName,
                UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, account.CustomerAccountId, ct);
        }

        foreach (var due in await workQueue.GetDueErasureScopesAsync(time.Utc, time.LocalDate, RetentionRules.BatchSize, ct))
        {
            units++;
            await SendAsync(new ExecuteErasureScopeCommand
            {
                CustomerAccountId = due.CustomerAccountId, ContextKey = due.ContextKey,
                UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, due.CustomerAccountId, ct);
        }

        return units;
    }

    /// <summary>
    /// Section 12.2 -- one retention run. C5 request (skipped when a run is already in progress, so a duplicate trigger is harmless; or an already
    /// <c>Requested</c> run passed in), C6 start, then in order the purge of sessions and reset tokens, the registered purge sources, the notification
    /// redaction and the review counts, each recorded as a pass (C6), then finish. A unit that throws or is refused is counted against its pass and the
    /// next unit still runs; the run then ends <c>CompletedWithErrors</c>. Returns the run id, or <c>null</c> when no run was started. Never throws.
    /// </summary>
    public async Task<Guid?> RunDailyPassesAsync(RetentionRunTrigger trigger, Guid? requestedRunId = null, CancellationToken ct = default)
    {
        var runId = requestedRunId ?? Guid.NewGuid();
        try
        {
            if (requestedRunId is null)
            {
                var requested = await SendAsync(new RequestRetentionRunCommand
                {
                    RetentionRunId = runId, Trigger = trigger.DisplayName,
                    UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
                }, runId, ct);
                if (!requested.Succeeded) return null;
            }

            var started = await SendAsync(new StartRetentionRunCommand
            {
                RetentionRunId = runId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, runId, ct);
            if (!started.Succeeded) return null;

            var time = await clock.NowAsync(ct);
            var securityCutoff = time.Utc.AddDays(-settings.GetInt(SettingKeys.RetentionSecurityArtefactDays));

            var passes = new List<PassOutcome>
            {
                await PurgePassAsync(RetentionPassKind.PurgeSessions, securityCutoff, ct),
                await PurgePassAsync(RetentionPassKind.PurgeResetTokens, securityCutoff, ct),
                await PurgePassAsync(RetentionPassKind.PurgeWebVitals, time.Utc.AddDays(-settings.GetInt(SettingKeys.RetentionWebVitalsDays)), ct),
                await PurgePassAsync(RetentionPassKind.PurgeBackupRuns, time.Utc.AddDays(-RetentionRules.BackupRunDays), ct),
                await PurgeSourcesPassAsync(time.Utc, ct),
                await RedactNotificationsPassAsync(time.Utc, ct),
                await ReviewCountsPassAsync(time.LocalDate, ct),
            };

            foreach (var pass in passes)
            {
                await SendAsync(new RecordRetentionPassCommand
                {
                    RetentionRunId = runId, PassKind = pass.Kind.DisplayName, Cutoff = pass.Cutoff,
                    Examined = pass.Examined, Affected = pass.Affected, Held = pass.Held, Failed = pass.Failed,
                    ErrorCodes = pass.ErrorCodes, DurationMs = pass.DurationMs,
                    UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
                }, runId, ct);
            }

            await SendAsync(new FinishRetentionRunCommand
            {
                RetentionRunId = runId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, runId, ct);
            return runId;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError("Retention run {RunId} failed ({ExceptionType}).", runId, ex.GetType().Name);
            return null;
        }
    }

    private sealed record PassOutcome(
        RetentionPassKind Kind, DateTimeOffset? Cutoff, int Examined, int Affected, int Held, int Failed, IReadOnlyList<string> ErrorCodes, int DurationMs);

    /// <summary>C7 in batches until fewer than <see cref="RetentionRules.BatchSize"/> remain, capped at <see cref="RetentionRules.MaxBatchesPerPass"/> batches.</summary>
    private async Task<PassOutcome> PurgePassAsync(RetentionPassKind kind, DateTimeOffset cutoff, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var purged = 0;
        var failed = 0;
        var codes = new List<string>();

        for (var batch = 0; batch < RetentionRules.MaxBatchesPerPass && !ct.IsCancellationRequested; batch++)
        {
            var outcome = await SendAsync(new PurgeExpiredRecordsCommand
            {
                PassKind = kind.DisplayName, Cutoff = cutoff, BatchSize = RetentionRules.BatchSize,
                UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, Guid.Empty, ct);

            if (!outcome.Succeeded)
            {
                failed++;
                codes.Add(outcome.ErrorCode);
                break;
            }

            var count = outcome.Response?.Result is int n ? n : 0;
            purged += count;
            if (count < RetentionRules.BatchSize) break;
        }

        return new PassOutcome(kind, cutoff, purged, purged, 0, failed, codes, (int)watch.ElapsedMilliseconds);
    }

    /// <summary>The cut-off of each scheduled purge source is its own setting. Only <c>boarding-media</c> (D-10, <c>retention.boardingMediaMonths</c>, HO-BRD-001's C-BRD-2) has a rule; every other key is not scheduled.</summary>
    private DateTimeOffset? SourceCutoff(string sourceKey, DateTimeOffset now) => sourceKey switch
    {
        "boarding-media" => now.AddMonths(-settings.GetInt(SettingKeys.RetentionBoardingMediaMonths)),
        _ => null,
    };

    /// <summary>C8 for each registered <see cref="IRetentionPurgeSource"/>, in batches; the sources' results are summed into one pass.</summary>
    private async Task<PassOutcome> PurgeSourcesPassAsync(DateTimeOffset now, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        int examined = 0, purged = 0, held = 0, failed = 0;
        var codes = new List<string>();

        List<string> keys;
        await using (var scope = scopeFactory.CreateAsyncScope())
            keys = scope.ServiceProvider.GetServices<IRetentionPurgeSource>().Select(s => s.SourceKey).ToList();

        foreach (var key in keys)
        {
            // A source with no retention rule in HO-XCT-001 is not scheduled -- never guessed at (ADR-301). In particular the stored-file:<purpose>
            // sources HO-FIL-001 registers include clinical purposes (lab results, consent forms) that must never be purged automatically.
            if (SourceCutoff(key, now) is not { } cutoff) continue;

            for (var batch = 0; batch < RetentionRules.MaxBatchesPerPass && !ct.IsCancellationRequested; batch++)
            {
                var outcome = await SendAsync(new RunPurgeSourceCommand
                {
                    SourceKey = key, Cutoff = cutoff, BatchSize = RetentionRules.BatchSize,
                    UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
                }, Guid.Empty, ct);

                if (!outcome.Succeeded || outcome.Response?.Result is not PurgeResult result)
                {
                    failed++;
                    codes.Add(outcome.Succeeded ? "PurgeSourceNoResult" : outcome.ErrorCode);
                    break;
                }

                examined += result.Examined; purged += result.Purged; held += result.Held; failed += result.Failed;
                if (result.Failed > 0) codes.Add("PurgeSourceItemFailed");
                if (result.Purged < RetentionRules.BatchSize) break;
            }
        }

        return new PassOutcome(RetentionPassKind.PurgeSources, null, examined, purged, held, failed, codes, (int)watch.ElapsedMilliseconds);
    }

    /// <summary>C9 once per notification and per notification request past <c>retention.notificationContentMonths</c>, capped at <see cref="RetentionRules.MaxRedactionsPerPass"/>.</summary>
    private async Task<PassOutcome> RedactNotificationsPassAsync(DateTimeOffset now, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var cutoff = now.AddMonths(-settings.GetInt(SettingKeys.RetentionNotificationContentMonths));
        var notificationIds = await workQueue.GetNotificationIdsDueForContentRedactionAsync(cutoff, RetentionRules.MaxRedactionsPerPass, ct);
        var requestIds = await workQueue.GetRequestIdsDueForVariableRedactionAsync(cutoff, RetentionRules.MaxRedactionsPerPass, ct);

        int redacted = 0, failed = 0;
        var codes = new List<string>();

        async Task RedactAsync(Guid? notificationId, Guid? requestId)
        {
            var outcome = await SendAsync(new RedactExpiredNotificationContentCommand
            {
                NotificationId = notificationId, RequestId = requestId,
                UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, notificationId ?? requestId ?? Guid.Empty, ct);
            if (outcome.Succeeded) redacted++;
            else { failed++; codes.Add(outcome.ErrorCode); }
        }

        foreach (var id in notificationIds) await RedactAsync(id, null);
        foreach (var id in requestIds) await RedactAsync(null, id);

        return new PassOutcome(RetentionPassKind.RedactNotifications, cutoff, notificationIds.Count + requestIds.Count, redacted, 0, failed, codes, (int)watch.ElapsedMilliseconds);
    }

    /// <summary>Queries only (ADR-301): the number of records past their period is stored on the pass (as Examined and Held); nothing is deleted or changed.</summary>
    private async Task<PassOutcome> ReviewCountsPassAsync(DateOnly today, CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var total = 0;
        var failed = 0;
        var codes = new List<string>();

        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var source in scope.ServiceProvider.GetServices<IRetentionReviewSource>())
        {
            try
            {
                total += (await source.CountPastRetentionAsync(today, ct)).Sum(row => row.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("Retention review source {SourceKey} failed ({ExceptionType}).", source.SourceKey, ex.GetType().Name);
                failed++;
                codes.Add("ReviewSourceFailed");
            }
        }

        return new PassOutcome(RetentionPassKind.ReviewCounts, null, total, 0, total, failed, codes, (int)watch.ElapsedMilliseconds);
    }

    private readonly record struct SendOutcome(bool Succeeded, string ErrorCode, CommandResponse? Response);

    /// <summary>Dispatches <paramref name="command"/> in its own scope. Never throws (other than cancellation at shutdown); a refusal or exception becomes an error code.</summary>
    private async Task<SendOutcome> SendAsync<TCommand>(TCommand command, Guid id, CancellationToken ct) where TCommand : Command
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
            var response = await dispatcher.SendAsync(command);
            if (response.IsSuccess) return new SendOutcome(true, string.Empty, response);

            var code = response.ValidationResult?.Errors.Select(e => e.ErrorCode).FirstOrDefault(c => !string.IsNullOrEmpty(c)) ?? "CommandRefused";
            logger.LogWarning("Retention unit {CommandType} for {Id} was refused: {Code}.", typeof(TCommand).Name, id, code);
            return new SendOutcome(false, code, response);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Graceful shutdown, not a per-unit failure.
            return new SendOutcome(false, "Cancelled", null);
        }
        catch (Exception ex)
        {
            // Ids and the exception type only.
            logger.LogError("Retention unit {CommandType} for {Id} failed ({ExceptionType}).", typeof(TCommand).Name, id, ex.GetType().Name);
            return new SendOutcome(false, "UnhandledException", null);
        }
    }
}
