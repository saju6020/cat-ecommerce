using CatEcommerce.Application.Commands.Notification;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Notifications;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;

namespace CatEcommerce.Api.Background;

/// <summary>
/// HO-NTF-001 §12.2 -- one pass. <see cref="RunDeliveryPassAsync"/>: for each id from the work
/// queue, a fresh scope, dispatch C4/C5/C6; an exception is logged at <c>Error</c> with the id and
/// swallowed so one bad row cannot stop the queue. <see cref="RunReminderSweepAsync"/> delegates to
/// <see cref="CareReminderSweep"/> (§8.12). Scoped -- <see cref="NotificationWorker"/> creates one
/// scope per poll and resolves this from it; the per-item fresh scopes below are created from the
/// injected <see cref="IServiceScopeFactory"/>, not that outer one.
/// </summary>
public sealed class NotificationWorkCycle(
    ILogger<NotificationWorkCycle> logger,
    IServiceScopeFactory scopeFactory,
    NotificationWorkQueue workQueue,
    CareReminderSweep careReminderSweep,
    IOptions<NotificationOptions> options,
    TimeProvider timeProvider)
{
    private static UserContext WorkerContext => new(SystemActors.NotificationWorker, Guid.Empty);

    public async Task RunDeliveryPassAsync(CancellationToken ct = default)
    {
        var batchSize = options.Value.Worker.BatchSize;
        var now = timeProvider.GetUtcNow();

        foreach (var requestId in await workQueue.GetPendingRequestIdsAsync(batchSize, ct))
        {
            await DispatchAsync(new ExpandNotificationRequestCommand
            {
                RequestId = requestId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, requestId, ct);
        }

        foreach (var notificationId in await workQueue.GetDueNotificationIdsAsync(now, batchSize, ct))
        {
            await DispatchAsync(new DeliverNotificationCommand
            {
                NotificationId = notificationId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, notificationId, ct);
        }

        foreach (var requestId in await workQueue.GetRequestsDueForVariableRedactionAsync(now, batchSize, ct))
        {
            await DispatchAsync(new RedactNotificationSecretsCommand
            {
                RequestId = requestId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, requestId, ct);
        }

        foreach (var notificationId in await workQueue.GetNotificationsDueForRedactionAsync(batchSize, ct))
        {
            await DispatchAsync(new RedactNotificationSecretsCommand
            {
                NotificationId = notificationId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }, notificationId, ct);
        }
    }

    public Task RunReminderSweepAsync(CancellationToken ct = default) => careReminderSweep.RunAsync(ct);

    private async Task DispatchAsync<TCommand>(TCommand command, Guid id, CancellationToken ct) where TCommand : Command
    {
        using var scope = scopeFactory.CreateScope();
        try
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
            await dispatcher.SendAsync(command);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Gate fix 2026-09-27 (CODE_REVIEW MEDIUM): graceful shutdown, not a per-item failure --
            // matches NotificationWorker.ExecuteAsync's own `ex is not OperationCanceledException`
            // filter. Logging this at Error on every deploy/restart made a real failure harder to
            // spot among expected shutdown noise.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Notification worker pass failed for {CommandType} {Id}.", typeof(TCommand).Name, id);

            // Gate fix 2026-09-27 (CODE_REVIEW third pass, HIGH): this used to fire on ANY
            // exception escaping ExpandNotificationRequestCommand's dispatch, not only a genuine
            // save failure -- a transient DB read failure from recipientDirectory.ResolveAsync,
            // subjectGuardResolver.IsStillValidAsync, branchRepository.GetByIdAsync,
            // templateRepository.GetByFilterAsync or quietHoursCalendar.NextAllowedInstantAsync
            // (the exact scenario ce1991c's own regression test exists for) therefore counted
            // toward MaxExpansionAttempts and, after 5 consecutive failing polls (~20-25s at the
            // default 5s interval), permanently Suppressed the request as PersistenceFailed --
            // reopening a bounded version of the very "transient DB hiccup causes silent,
            // permanent data loss" defect ce1991c fixed a few commits earlier in this same round.
            // DbUpdateException (DbUpdateConcurrencyException derives from it) is thrown ONLY by
            // EfAggregateRootRepository.CommitAsync's db.SaveChangesAsync() call -- never by the
            // plain SELECT-based reads above, which throw NpgsqlException/TimeoutException/a bare
            // Exception instead -- so this is a closed, proven signal that the FINAL commit is
            // what failed, not "whatever happened to escape the whole handler".
            if (command is ExpandNotificationRequestCommand && ex is DbUpdateException)
                await RecordExpandFailureAsync(id, ct);
        }
    }

    /// <summary>
    /// Gate fix 2026-09-27 (SECURITY/CODE_REVIEW second pass, MEDIUM, "one bad row blocks the whole
    /// queue"). A SAVE-time failure in <see cref="ExpandNotificationRequestCommand"/> (any cause --
    /// the one previously-known cause, a rendered field overflowing its column, is now caught and
    /// suppressed as RenderFailed BEFORE this point is ever reached) used to leave the request
    /// Pending, retried every poll forever, and -- being the OLDEST Pending row -- block the whole
    /// BatchSize-capped queue behind it. This records the failed attempt, in a BRAND NEW scope,
    /// never the one whose dispatch just failed above: once a Postgres command fails inside an
    /// implicit transaction, every further command on that same connection is refused ("current
    /// transaction is aborted") until the transaction ends -- the failed scope's DbContext is
    /// poisoned for the rest of its lifetime.
    /// </summary>
    private async Task RecordExpandFailureAsync(Guid requestId, CancellationToken ct)
    {
        try
        {
            using var recoveryScope = scopeFactory.CreateScope();
            var dispatcher = recoveryScope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
            await dispatcher.SendAsync(new RecordNotificationExpansionFailureCommand
            {
                RequestId = requestId, UserContext = WorkerContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true
            });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            // The recovery write itself failed -- the row simply stays Pending and is retried on the
            // next pass, no worse off than before this fix existed.
            logger.LogError(ex, "Failed to record a failed expand attempt for notification request {RequestId}.", requestId);
        }
    }
}
