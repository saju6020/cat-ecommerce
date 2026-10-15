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

internal static class StorageBreachLoader
{
    public static async Task<(StorageBreachReportAggregate? Report, CommandResponse? Failure)> LoadAsync(
        IAggregateRootRepository<StorageBreachReportAggregate> repository, Guid breachId, Guid branchId)
    {
        var report = await repository.GetByIdAsync(breachId);
        return report is null || report.BranchId != branchId
            ? (null, InventoryResponses.NotFound(BusinessViolationCodes.StorageBreachNotFound, nameof(breachId), "That storage breach report was not found."))
            : (report, null);
    }
}

/// <summary>What a reported breach answers.</summary>
public sealed record ReportedStorageBreach(int AffectedBatchCount);

/// <summary>
/// HO-IMP-001 section 8.13 / C17 (US-IMP-16 AC2) -- reports a storage-condition breach at a location of the ACTIVE branch: every batch with stock there is flagged
/// (a snapshot), and a <c>StorageBreach</c> alert is ENLISTED so that the report and its alert commit together. Reporting moves no stock; that is a separate, deliberate step.
/// </summary>
public sealed class ReportStorageBreachCommandHandler(
    ILogger<ReportStorageBreachCommandHandler> logger,
    IAggregateRootRepository<StorageBreachReportAggregate> repository,
    IStockPositionReader positions,
    IInventoryAlertEnlister alerts,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ReportStorageBreachCommand>
{
    public async Task<CommandResponse> HandleAsync(ReportStorageBreachCommand command)
    {
        logger.LogInformation("Reporting a storage breach at {LocationId}. CorrelationId: {CorrelationId}", command.LocationId, command.CorrelationId);

        var locations = await positions.GetLocationsAsync([command.LocationId], CancellationToken.None);
        if (!locations.TryGetValue(command.LocationId, out var location) || location.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.StockLocationNotFound, nameof(command.LocationId), "That location does not exist at this branch.");

        var affected = StorageBreachReportAggregate.AffectedBatches(await positions.GetPositionsAtLocationAsync(location.LocationId, null, CancellationToken.None));
        var now = timeProvider.GetUtcNow();
        var actor = command.ToActor();

        var report = new StorageBreachReportAggregate(command.BreachId);
        try
        {
            report.Report(new StorageBreachReportDto(
                command.LocationId, command.BranchId, command.ObservedOn, command.Condition, command.TemperatureC, command.DurationMinutes, affected, now, actor));
        }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        var alert = new InventoryAlertAggregate(Guid.NewGuid());
        alert.Raise(new StockAlertRaiseDto(
            StockAlertKind.StorageBreach, command.BranchId, SkuId: null, BatchId: null, command.LocationId, ThresholdDays: null, QuantityAtRaise: null,
            StockReferenceKind.StorageBreach, command.BreachId, $"breach:{command.BreachId}", now, actor));
        alerts.Enlist(alert);

        await repository.SaveAsync(report, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(new ReportedStorageBreach(affected.Count));
    }
}

/// <summary>
/// HO-IMP-001 section 8.13 / ADR-115 -- the LOCATION-level quarantine: the free stock of every affected batch at the breached location is moved to the branch's
/// <c>Quarantine</c> location with enlisted transfer entries that reference the report, in one commit. The location then excludes it from usable stock. Stock that is
/// reserved for an order stays where it is. The decision afterwards (a transfer back, a write-off) is an ordinary movement; closing the report records it.
/// </summary>
public sealed class QuarantineStorageBreachStockCommandHandler(
    ILogger<QuarantineStorageBreachStockCommandHandler> logger,
    IAggregateRootRepository<StorageBreachReportAggregate> repository,
    IStockPositionReader positions,
    IInventoryLedgerWriter ledgerWriter,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<QuarantineStorageBreachStockCommand>
{
    public async Task<CommandResponse> HandleAsync(QuarantineStorageBreachStockCommand command)
    {
        logger.LogInformation("Quarantining the stock of storage breach {BreachId}. CorrelationId: {CorrelationId}", command.BreachId, command.CorrelationId);

        var (report, failure) = await StorageBreachLoader.LoadAsync(repository, command.BreachId, command.BranchId);
        if (failure is not null) return failure;

        var quarantine = await positions.GetQuarantineLocationAsync(command.BranchId, CancellationToken.None);
        if (quarantine is null)
            return InventoryResponses.Fail(BusinessViolationCodes.StockLocationNotFound, nameof(command.BreachId), "This branch has no quarantine location to move the stock to.");

        var affected = report!.AffectedBatchIds.ToHashSet();
        var label = $"Storage breach {report.Id.ToString("N")[..8]}";
        var drafts = (await positions.GetPositionsAtLocationAsync(report.LocationId, null, CancellationToken.None))
            .Where(p => affected.Contains(p.BatchId) && p.OnHand - p.Reserved > 0m)
            .SelectMany(p => new[]
            {
                new StockMovementDraft(command.BranchId, report.LocationId, p.SkuId, p.BatchId, MovementKind.TransferOut, -(p.OnHand - p.Reserved), StockReferenceKind.StorageBreach, report.Id, null, label, report.Condition),
                new StockMovementDraft(command.BranchId, quarantine.LocationId, p.SkuId, p.BatchId, MovementKind.TransferIn, p.OnHand - p.Reserved, StockReferenceKind.StorageBreach, report.Id, null, label, report.Condition)
            }).ToList();

        try { report.MarkQuarantined(timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        var appended = await ledgerWriter.AppendAsync(drafts, CancellationToken.None);
        if (!appended.IsSuccess) return InventoryResponses.Fail(appended);

        await repository.UpdateAsync(report, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>Records the decision taken after a breach (US-IMP-16 AC2). The stock movement itself was an ordinary transfer or write-off.</summary>
public sealed class CloseStorageBreachCommandHandler(
    ILogger<CloseStorageBreachCommandHandler> logger,
    IAggregateRootRepository<StorageBreachReportAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CloseStorageBreachCommand>
{
    public async Task<CommandResponse> HandleAsync(CloseStorageBreachCommand command)
    {
        logger.LogInformation("Closing storage breach {BreachId}. CorrelationId: {CorrelationId}", command.BreachId, command.CorrelationId);

        var (report, failure) = await StorageBreachLoader.LoadAsync(repository, command.BreachId, command.BranchId);
        if (failure is not null) return failure;

        try { report!.Close(command.Note, timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(report, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}
