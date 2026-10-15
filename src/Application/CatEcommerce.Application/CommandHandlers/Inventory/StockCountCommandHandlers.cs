using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.DataMappers;
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

/// <summary>The loading every count command shares: another branch's count is "not found".</summary>
internal static class StockCountLoader
{
    public static async Task<(StockCountAggregate? Count, CommandResponse? Failure)> LoadAsync(
        IAggregateRootRepository<StockCountAggregate> repository, Guid countId, Guid branchId)
    {
        var count = await repository.GetByIdAsync(countId);
        return count is null || count.BranchId != branchId
            ? (null, InventoryResponses.NotFound(BusinessViolationCodes.StockCountNotFound, nameof(countId), "That stock count was not found."))
            : (count, null);
    }
}

/// <summary>HO-IMP-001 §8.10 / §11.4 C12 (US-IMP-14). One <c>Open</c>, <c>Submitted</c> or <c>AwaitingApproval</c> count per location
/// (<c>CountLocationBusy</c>; <c>ix_stock_count_one_active</c> backs the pre-check). A selected-SKU count names SKUs that exist.</summary>
public sealed class OpenStockCountCommandHandler(
    ILogger<OpenStockCountCommandHandler> logger,
    IAggregateRootRepository<StockCountAggregate> repository,
    IStockPositionReader positions,
    ISkuDirectory skus,
    IDocumentNumberGenerator numbers,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<OpenStockCountCommand>
{
    public async Task<CommandResponse> HandleAsync(OpenStockCountCommand command)
    {
        logger.LogInformation("Opening stock count {CountId} at {LocationId}. CorrelationId: {CorrelationId}", command.CountId, command.LocationId, command.CorrelationId);

        var locations = await positions.GetLocationsAsync([command.LocationId], CancellationToken.None);
        if (!locations.TryGetValue(command.LocationId, out var location) || location.BranchId != command.BranchId || !location.IsActive)
            return InventoryResponses.Fail(BusinessViolationCodes.StockLocationNotFound, nameof(command.LocationId), "That location does not exist at this branch.");

        if (await repository.ExistAsync(c => c.LocationId == command.LocationId &&
                (c.Status == StockCountStatus.Open || c.Status == StockCountStatus.Submitted || c.Status == StockCountStatus.AwaitingApproval)))
            return InventoryResponses.Fail(BusinessViolationCodes.CountLocationBusy, nameof(command.LocationId), InventoryMessages.CountLocationBusy);

        var scope = InventoryMapper.ParseEnum<StockCountScope>(command.Scope);
        if (scope == StockCountScope.SelectedSkus)
        {
            var found = await skus.GetManyAsync(command.SkuIds, CancellationToken.None);
            if (found.Count != command.SkuIds.Distinct().Count())
                return InventoryResponses.Fail(BusinessViolationCodes.SkuNotFound, nameof(command.SkuIds), "One of the selected SKUs does not exist.");
        }

        var count = new StockCountAggregate(command.CountId);
        try
        {
            count.Open(new StockCountOpenDto(
                await numbers.NextAsync(DocumentKind.StockCount, CancellationToken.None), command.BranchId, command.LocationId, scope, command.SkuIds,
                timeProvider.GetUtcNow(), command.ToActor()));
        }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.SaveAsync(count, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(count.Number);
    }
}

/// <summary>Counters record lines, upserting by (SKU, batch). <c>Open</c> only; each batch must be a batch of that SKU, and (for a selected-SKU
/// count) the SKU must be in scope (<c>CountLineOutsideScope</c>, decided by the aggregate).</summary>
public sealed class RecordCountLinesCommandHandler(
    ILogger<RecordCountLinesCommandHandler> logger,
    IAggregateRootRepository<StockCountAggregate> repository,
    IStockPositionReader positions,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordCountLinesCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordCountLinesCommand command)
    {
        logger.LogInformation("Recording count lines on {CountId}. CorrelationId: {CorrelationId}", command.CountId, command.CorrelationId);

        var (count, failure) = await StockCountLoader.LoadAsync(repository, command.CountId, command.BranchId);
        if (failure is not null) return failure;

        var batches = await positions.GetBatchFactsAsync(command.Lines.Select(l => l.BatchId).Distinct().ToList(), CancellationToken.None);
        for (var i = 0; i < command.Lines.Count; i++)
        {
            if (!batches.TryGetValue(command.Lines[i].BatchId, out var batch) || batch.SkuId != command.Lines[i].SkuId)
                return InventoryResponses.Fail(BusinessViolationCodes.BatchNotFound, $"Lines[{i}].BatchId", "That batch does not exist for that SKU.");
        }

        try { count!.RecordLines(command.Lines.Select(l => new CountLineDto(l.SkuId, l.BatchId, l.CountedQuantity)).ToList(), timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(count!, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>
/// Submit freezes the variance report: the handler reads the CURRENT positions at the location for the scope (the book quantities) and their unit
/// costs; the aggregate gives every counted line its book quantity and variance and, for a FULL-LOCATION count, turns each positive position that was
/// not counted into a line counted 0. <b>Nothing is written to the ledger</b> (US-IMP-14 AC1) -- the report exists before any adjustment does.
/// </summary>
public sealed class SubmitStockCountCommandHandler(
    ILogger<SubmitStockCountCommandHandler> logger,
    IAggregateRootRepository<StockCountAggregate> repository,
    IStockPositionReader positions,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SubmitStockCountCommand>
{
    public async Task<CommandResponse> HandleAsync(SubmitStockCountCommand command)
    {
        logger.LogInformation("Submitting stock count {CountId}. CorrelationId: {CorrelationId}", command.CountId, command.CorrelationId);

        var (count, failure) = await StockCountLoader.LoadAsync(repository, command.CountId, command.BranchId);
        if (failure is not null) return failure;

        var isFull = count!.Scope == StockCountScope.FullLocation;
        var held = await positions.GetPositionsAtLocationAsync(count.LocationId, isFull ? null : count.SkuIds, CancellationToken.None);

        // A counted line may also be for a batch the book says is not here at all (found stock): its book quantity is 0, its cost still needed.
        var batchIds = held.Select(p => p.BatchId).Concat(count.Lines.Select(l => l.BatchId)).Distinct().ToList();
        var costs = await positions.GetUnitCostsAsync(batchIds, CancellationToken.None);
        var books = held.Select(p => new CountBookDto(p.SkuId, p.BatchId, p.OnHand, costs.TryGetValue(p.BatchId, out var c) ? c.UnitCostBase : 0m)).ToList();
        books.AddRange(count.Lines.Where(l => held.All(p => p.BatchId != l.BatchId))
            .Select(l => new CountBookDto(l.SkuId, l.BatchId, 0m, costs.TryGetValue(l.BatchId, out var c) ? c.UnitCostBase : 0m)));

        try
        {
            count.Submit(new StockCountSubmitDto(books, isFull, settings.GetString(SettingKeys.CommerceCurrencyCode, command.BranchId), timeProvider.GetUtcNow(), command.ToActor()));
        }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(count, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>What a commit answers: <c>Committed</c>, or <c>AwaitingApproval</c> when the reported variance is worth more than the threshold.</summary>
public sealed record CommittedStockCount(string Status);

/// <summary>
/// Commit posts EXACTLY the reported variances as <c>CountGain</c>/<c>CountLoss</c> entries referencing the count (US-IMP-14 AC2, ADR-108) -- not
/// "set to counted", so a sale made during counting stays a real movement. Above <c>inventory.adjustmentApprovalThreshold</c> it waits for a
/// different approver, exactly like an adjustment (ADR-107): a count must not be a way round adjustment approval.
/// </summary>
public sealed class CommitStockCountCommandHandler(
    ILogger<CommitStockCountCommandHandler> logger,
    IAggregateRootRepository<StockCountAggregate> repository,
    IInventoryLedgerWriter ledgerWriter,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CommitStockCountCommand>
{
    public async Task<CommandResponse> HandleAsync(CommitStockCountCommand command)
    {
        logger.LogInformation("Committing stock count {CountId}. CorrelationId: {CorrelationId}", command.CountId, command.CorrelationId);

        var (count, failure) = await StockCountLoader.LoadAsync(repository, command.CountId, command.BranchId);
        if (failure is not null) return failure;

        try { count!.Commit(settings.GetDecimal(SettingKeys.InventoryAdjustmentApprovalThreshold, command.BranchId), timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        var appended = await ledgerWriter.AppendAsync(count.BuildDrafts(), CancellationToken.None);
        if (!appended.IsSuccess) return InventoryResponses.Fail(appended);

        await repository.UpdateAsync(count, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(new CommittedStockCount(count.Status.DisplayName));
    }
}

public sealed class ApproveStockCountCommandHandler(
    ILogger<ApproveStockCountCommandHandler> logger,
    IAggregateRootRepository<StockCountAggregate> repository,
    IInventoryLedgerWriter ledgerWriter,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ApproveStockCountCommand>
{
    public async Task<CommandResponse> HandleAsync(ApproveStockCountCommand command)
    {
        logger.LogInformation("Approving stock count {CountId}. CorrelationId: {CorrelationId}", command.CountId, command.CorrelationId);

        if (ApproverCheck.RefuseIfNotApprover(command.UserContext) is { } denied) return denied;
        var (count, failure) = await StockCountLoader.LoadAsync(repository, command.CountId, command.BranchId);
        if (failure is not null) return failure;

        try { count!.Approve(command.UserContext!.UserId, timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        var appended = await ledgerWriter.AppendAsync(count.BuildDrafts(), CancellationToken.None);
        if (!appended.IsSuccess) return InventoryResponses.Fail(appended);

        await repository.UpdateAsync(count, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>A rejected commit goes back to <c>Submitted</c> -- the variance report stays frozen.</summary>
public sealed class RejectStockCountCommandHandler(
    ILogger<RejectStockCountCommandHandler> logger,
    IAggregateRootRepository<StockCountAggregate> repository)
    : ICommandHandlerAsync<RejectStockCountCommand>
{
    public async Task<CommandResponse> HandleAsync(RejectStockCountCommand command)
    {
        logger.LogInformation("Rejecting stock count {CountId}. CorrelationId: {CorrelationId}", command.CountId, command.CorrelationId);

        if (ApproverCheck.RefuseIfNotApprover(command.UserContext) is { } denied) return denied;
        var (count, failure) = await StockCountLoader.LoadAsync(repository, command.CountId, command.BranchId);
        if (failure is not null) return failure;

        try { count!.Reject(command.UserContext!.UserId, command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(count, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

public sealed class CancelStockCountCommandHandler(
    ILogger<CancelStockCountCommandHandler> logger,
    IAggregateRootRepository<StockCountAggregate> repository)
    : ICommandHandlerAsync<CancelStockCountCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelStockCountCommand command)
    {
        logger.LogInformation("Cancelling stock count {CountId}. CorrelationId: {CorrelationId}", command.CountId, command.CorrelationId);

        var (count, failure) = await StockCountLoader.LoadAsync(repository, command.CountId, command.BranchId);
        if (failure is not null) return failure;

        try { count!.Cancel(command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(count, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}
