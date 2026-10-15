using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
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

/// <summary>The loading every batch status command shares: another branch's batch is "not found" (never revealing which).</summary>
internal static class BatchLoader
{
    public static async Task<(BatchAggregate? Batch, CommandResponse? Failure)> LoadAsync(
        IAggregateRootRepository<BatchAggregate> repository, Guid batchId, Guid branchId)
    {
        var batch = await repository.GetByIdAsync(batchId);
        return batch is null || batch.BranchId != branchId
            ? (null, InventoryResponses.NotFound(BusinessViolationCodes.BatchNotFound, nameof(batchId), "That batch was not found."))
            : (batch, null);
    }
}

/// <summary>HO-IMP-001 §8.6.1 / §11.4 C9. <c>Available -> Quarantined</c>; a quarantined batch is never usable (the application and
/// <c>tg_stock_batch_usable</c> both refuse it).</summary>
public sealed class QuarantineBatchCommandHandler(
    ILogger<QuarantineBatchCommandHandler> logger,
    IAggregateRootRepository<BatchAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<QuarantineBatchCommand>
{
    public async Task<CommandResponse> HandleAsync(QuarantineBatchCommand command)
    {
        logger.LogInformation("Quarantining batch {BatchId}. CorrelationId: {CorrelationId}", command.BatchId, command.CorrelationId);

        var (batch, failure) = await BatchLoader.LoadAsync(repository, command.BatchId, command.BranchId);
        if (failure is not null) return failure;

        try { batch!.Quarantine(command.Reason, timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(batch!, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary><c>Quarantined -> Available</c>. A recalled batch is never released straight back (<c>WithdrawRecall</c> goes to Quarantined first).</summary>
public sealed class ReleaseBatchCommandHandler(
    ILogger<ReleaseBatchCommandHandler> logger,
    IAggregateRootRepository<BatchAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ReleaseBatchCommand>
{
    public async Task<CommandResponse> HandleAsync(ReleaseBatchCommand command)
    {
        logger.LogInformation("Releasing batch {BatchId}. CorrelationId: {CorrelationId}", command.BatchId, command.CorrelationId);

        var (batch, failure) = await BatchLoader.LoadAsync(repository, command.BatchId, command.BranchId);
        if (failure is not null) return failure;

        try { batch!.Release(timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(batch!, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>
/// HO-IMP-001 §11.4 C9 -- writes a batch off: the transition, then ONE <c>Wastage</c> entry per position with <c>on_hand &gt; 0</c> (reference
/// <c>BatchWriteOff</c>, the batch id) so nothing remains, then the single <c>UpdateAsync</c>. A position still holding RESERVED stock cannot be
/// written off (the orders' reservations would be left pointing at nothing): the handler says so before the database's <c>CHECK</c> does.
/// </summary>
public sealed class WriteOffBatchCommandHandler(
    ILogger<WriteOffBatchCommandHandler> logger,
    IAggregateRootRepository<BatchAggregate> repository,
    IStockPositionReader positions,
    IInventoryLedgerWriter ledgerWriter,
    IAggregateRootRepository<StaffBranchAssignmentAggregate> assignments,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<WriteOffBatchCommand>
{
    public async Task<CommandResponse> HandleAsync(WriteOffBatchCommand command)
    {
        logger.LogInformation("Writing off batch {BatchId}. CorrelationId: {CorrelationId}", command.BatchId, command.CorrelationId);

        var (batch, failure) = await BatchLoader.LoadAsync(repository, command.BatchId, command.BranchId);
        if (failure is not null) return failure;

        var held = await positions.GetPositionsOfBatchAsync(batch!.Id, CancellationToken.None);
        if (held.Any(p => p.Reserved > 0m))
            return InventoryResponses.Fail(BusinessViolationCodes.InsufficientStock, nameof(command.BatchId),
                "Part of this batch is reserved by open orders. Release those reservations before writing it off.");

        // A write-off leaves nothing anywhere (§8.6.1), so it may only be done by someone assigned to EVERY branch that holds the batch -- the same rule a
        // transfer applies to its destination. Otherwise a manager could zero another branch's stock.
        var locations = await positions.GetLocationsAsync(held.Select(p => p.LocationId).Distinct().ToList(), CancellationToken.None);
        var heldBranches = held.Where(p => p.OnHand > 0m).Select(p => locations[p.LocationId].BranchId).Distinct().Where(b => b != command.BranchId).ToList();
        if (heldBranches.Count > 0)
        {
            var assignment = await assignments.GetByIdAsync(command.UserContext!.UserId);
            if (assignment is null || heldBranches.Any(b => !assignment.BranchIds.Contains(b)))
                return InventoryResponses.Fail(BusinessViolationCodes.StockLocationNotFound, nameof(command.BatchId), "This batch is also held at a branch you are not assigned to, so it cannot be written off from here.");
        }

        try { batch.WriteOff(command.Reason, timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        var drafts = held.Where(p => p.OnHand > 0m).Select(p => new StockMovementDraft(
            locations[p.LocationId].BranchId, p.LocationId, p.SkuId, p.BatchId, MovementKind.Wastage, -p.OnHand, StockReferenceKind.BatchWriteOff, batch.Id, null,
            $"Write-off {batch.LotNumber}", command.Reason)).ToList();

        var appended = await ledgerWriter.AppendAsync(drafts, CancellationToken.None);
        if (!appended.IsSuccess) return InventoryResponses.Fail(appended);

        await repository.UpdateAsync(batch, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}
