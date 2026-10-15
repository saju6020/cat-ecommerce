using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Inventory;

internal static class RecallBatchLoader
{
    /// <summary>A recall applies to the batch EVERYWHERE, so it is not scoped to the caller's branch.</summary>
    public static async Task<(BatchAggregate? Batch, CommandResponse? Failure)> LoadAsync(IAggregateRootRepository<BatchAggregate> repository, Guid batchId)
    {
        var batch = await repository.GetByIdAsync(batchId);
        return batch is null
            ? (null, InventoryResponses.NotFound(BusinessViolationCodes.BatchNotFound, nameof(batchId), "That batch was not found."))
            : (batch, null);
    }
}

/// <summary>What a declared recall answers.</summary>
public sealed record DeclaredRecall(string RecallNumber);

/// <summary>
/// HO-IMP-001 section 8.12 / 11.4 C16 (US-IMP-11 AC1, ADR-104) -- declares a recall. The lot number must be typed exactly (a recall is not a mis-click). ONE row
/// update: the batch becomes <c>Recalled</c> at every location and branch from this commit, so there is no loop that could half-finish; the usability trigger
/// serialises it against in-flight consumption (FI-5). Stock stays where it is, shown as held, until it is written off (<c>RecallDisposal</c>) or returned.
/// </summary>
public sealed class RecallBatchCommandHandler(
    ILogger<RecallBatchCommandHandler> logger,
    IAggregateRootRepository<BatchAggregate> repository,
    IDocumentNumberGenerator numbers,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecallBatchCommand>
{
    public async Task<CommandResponse> HandleAsync(RecallBatchCommand command)
    {
        logger.LogInformation("Declaring a recall of batch {BatchId}. CorrelationId: {CorrelationId}", command.BatchId, command.CorrelationId);

        var (batch, failure) = await RecallBatchLoader.LoadAsync(repository, command.BatchId);
        if (failure is not null) return failure;

        try
        {
            batch!.Recall(
                new BatchRecallDto(await numbers.NextAsync(DocumentKind.BatchRecall, CancellationToken.None), command.Reason, command.CustomerAdvice, timeProvider.GetUtcNow(), command.ToActor()),
                command.ConfirmLotNumber);
        }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(batch, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(new DeclaredRecall(batch.CurrentRecall!.RecallNumber));
    }
}

/// <summary>What a notification run answers (section 12.4): how many requests it created, how many recipients had been notified already and how many remain
/// for another run (a run is capped).</summary>
public sealed record RecallNotificationRun(int Requested, int AlreadyNotified, int Remaining);

/// <summary>
/// HO-IMP-001 section 8.12 / C16 (US-IMP-11 AC3) -- enqueues ONE urgent <c>inventory.batch-recall</c> request per recipient through the outbox, each with the
/// batch as its subject and the deduplication key <c>recall:{batchId}:{customerId}</c> (so a second click, or a second run, creates none for the same
/// customer), then records the run on the batch -- ONE commit, so a second concurrent click loses on <c>xmin</c>. HO-NTF-001 logs every message.
/// </summary>
public sealed class NotifyRecallRecipientsCommandHandler(
    ILogger<NotifyRecallRecipientsCommandHandler> logger,
    IAggregateRootRepository<BatchAggregate> repository,
    IRecallRecipientResolver resolver,
    INotificationOutbox outbox,
    IInventoryAuditEnlister audit,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<NotifyRecallRecipientsCommand>
{
    public async Task<CommandResponse> HandleAsync(NotifyRecallRecipientsCommand command)
    {
        logger.LogInformation("Notifying the recipients of the recall of batch {BatchId}. CorrelationId: {CorrelationId}", command.BatchId, command.CorrelationId);

        var (batch, failure) = await RecallBatchLoader.LoadAsync(repository, command.BatchId);
        if (failure is not null) return failure;
        if (batch!.Status != BatchStatus.Recalled || batch.CurrentRecall is null)
            return InventoryResponses.Fail(BusinessViolationCodes.RecallNotActive, nameof(command.BatchId), "This batch is not under recall.");

        var alreadyNotified = await resolver.CountAlreadyNotifiedAsync(batch.Id, CancellationToken.None);
        var pending = await resolver.ResolveAsync(batch.Id, CancellationToken.None);
        var run = pending.Take(RecallRules.MaxRecipientsPerRun).ToList();

        if (run.Count == 0) return InventoryResponses.Ok(new RecallNotificationRun(0, alreadyNotified, 0));

        var variables = new Dictionary<string, string>
        {
            ["productName"] = batch.DisplayName,
            ["lotNumber"] = batch.LotNumber,
            ["expiresOn"] = batch.ExpiresOn is { } expires ? expires.ToString("yyyy-MM-dd") : "no expiry date",
            ["customerAdvice"] = batch.CurrentRecall.CustomerAdvice
        };

        foreach (var recipient in run)
        {
            outbox.Enqueue(new NotificationRequestDto(
                Guid.NewGuid(), NotificationTemplateCatalogue.InventoryBatchRecall, NotificationRecipientKind.Customer, recipient.CustomerId, variables,
                SubjectKind: NotificationSubjectKind.BatchRecall, SubjectId: batch.Id, BranchId: recipient.BranchId,
                DeduplicationKey: RecallRules.DeduplicationKey(batch.Id, recipient.CustomerId), CorrelationId: command.CorrelationId, UserContext: command.UserContext));
        }

        try { batch.RecordRecallNotification(run.Count, timeProvider.GetUtcNow(), command.UserContext!.UserId, command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        // A BusinessEvent audit row, in the SAME commit as the batch and the requests (SRS C-9), records the run and its count (section 8.12): who told how many customers about which recall, and when.
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "Batch", EntityId: batch.Id, Action: "RecallNotified", Reason: "RecallNotified", ActorUserId: command.UserContext!.UserId, OccurredOn: timeProvider.GetUtcNow(),
            BeforeValue: null, AfterValue: System.Text.Json.JsonSerializer.Serialize(new { batch.CurrentRecall.RecallNumber, Requested = run.Count, AlreadyNotified = alreadyNotified, Remaining = pending.Count - run.Count }),
            UserContext: command.UserContext, CorrelationId: command.CorrelationId, Category: AuditCategory.BusinessEvent, IpAddress: null));
        audit.Enlist(entry); // enlisted, not saved: the batch update below commits it with the batch and the notification requests
        await repository.UpdateAsync(batch, EventPublishOption.InMemoryPublishOnly);

        return InventoryResponses.Ok(new RecallNotificationRun(run.Count, alreadyNotified, pending.Count - run.Count));
    }
}

/// <summary>A recall declared by mistake: the batch goes to <c>Quarantined</c>, never straight back to sale, and pending notices fail
/// <c>BatchRecallGuard</c> (valid only while the batch is <c>Recalled</c>) and are cancelled by HO-NTF-001.</summary>
public sealed class WithdrawBatchRecallCommandHandler(
    ILogger<WithdrawBatchRecallCommandHandler> logger,
    IAggregateRootRepository<BatchAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<WithdrawBatchRecallCommand>
{
    public async Task<CommandResponse> HandleAsync(WithdrawBatchRecallCommand command)
    {
        logger.LogInformation("Withdrawing the recall of batch {BatchId}. CorrelationId: {CorrelationId}", command.BatchId, command.CorrelationId);

        var (batch, failure) = await RecallBatchLoader.LoadAsync(repository, command.BatchId);
        if (failure is not null) return failure;

        try { batch!.WithdrawRecall(command.Reason, timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(batch, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>Records the completion of a recall once no stock of the batch is held anywhere (<c>RecallStockStillHeld</c> otherwise); the batch stays <c>Recalled</c>.</summary>
public sealed class CloseBatchRecallCommandHandler(
    ILogger<CloseBatchRecallCommandHandler> logger,
    IAggregateRootRepository<BatchAggregate> repository,
    IStockPositionReader positions,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CloseBatchRecallCommand>
{
    public async Task<CommandResponse> HandleAsync(CloseBatchRecallCommand command)
    {
        logger.LogInformation("Closing the recall of batch {BatchId}. CorrelationId: {CorrelationId}", command.BatchId, command.CorrelationId);

        var (batch, failure) = await RecallBatchLoader.LoadAsync(repository, command.BatchId);
        if (failure is not null) return failure;
        if (batch!.Status != BatchStatus.Recalled || batch.CurrentRecall is null)
            return InventoryResponses.Fail(BusinessViolationCodes.RecallNotActive, nameof(command.BatchId), "This batch is not under recall.");

        if ((await positions.GetPositionsOfBatchAsync(batch.Id, CancellationToken.None)).Any(p => p.OnHand > 0m))
            return InventoryResponses.Fail(BusinessViolationCodes.RecallStockStillHeld, nameof(command.BatchId), "Stock of this batch is still held. Write it off or return it first.");

        try { batch.CloseRecall(command.Note, timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(batch, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>The recall trace holds customers names and contact details, so reading it as a file is itself a sensitive act (see <see cref="RecordRecallTraceExportCommand"/>).</summary>
public sealed class RecordRecallTraceExportCommandHandler(IAggregateRootRepository<AuditEntryAggregate> auditRepository, TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordRecallTraceExportCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordRecallTraceExportCommand command)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "Batch", EntityId: command.BatchId, Action: "RecallTraceExported", Reason: "RecallTraceExported",
            ActorUserId: command.UserContext!.UserId, OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null,
            AfterValue: System.Text.Json.JsonSerializer.Serialize(new { command.BatchId, command.RowCount }),
            UserContext: command.UserContext, CorrelationId: command.CorrelationId, Category: AuditCategory.BusinessEvent,
            IpAddress: command.IpAddress is null ? null : System.Net.IPAddress.Parse(command.IpAddress)));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
        return InventoryResponses.Ok();
    }
}
