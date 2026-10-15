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

/// <summary>
/// HO-IMP-001 §11.4 C14 (US-IMP-12) -- an upsert of the reorder policy of a (SKU, location). The location must be one of the ACTIVE branch's (a location of
/// another branch does not exist to the caller), the SKU must exist and the preferred supplier, when named, must be a supplier.
/// </summary>
public sealed class SetReorderPolicyCommandHandler(
    ILogger<SetReorderPolicyCommandHandler> logger,
    IAggregateRootRepository<ReorderPolicyAggregate> repository,
    IAggregateRootRepository<SupplierAggregate> suppliers,
    ISkuDirectory skus,
    IStockPositionReader positions)
    : ICommandHandlerAsync<SetReorderPolicyCommand>
{
    public async Task<CommandResponse> HandleAsync(SetReorderPolicyCommand command)
    {
        logger.LogInformation("Setting the reorder policy of SKU {SkuId} at {LocationId}. CorrelationId: {CorrelationId}", command.SkuId, command.LocationId, command.CorrelationId);

        var locations = await positions.GetLocationsAsync([command.LocationId], CancellationToken.None);
        if (!locations.TryGetValue(command.LocationId, out var location) || location.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.StockLocationNotFound, nameof(command.LocationId), "That location does not exist at this branch.");

        if (await skus.GetAsync(command.SkuId, CancellationToken.None) is null)
            return InventoryResponses.NotFound(BusinessViolationCodes.SkuNotFound, nameof(command.SkuId), "That SKU was not found.");

        if (command.PreferredSupplierId is { } supplierId && await suppliers.GetByIdAsync(supplierId) is null)
            return InventoryResponses.Fail(BusinessViolationCodes.SupplierNotFound, nameof(command.PreferredSupplierId), "That supplier does not exist.");

        var policy = await repository.GetByFilterAsync(p => p.SkuId == command.SkuId && p.LocationId == command.LocationId);
        var isNew = policy is null;
        policy ??= new ReorderPolicyAggregate(Guid.NewGuid());

        try
        {
            policy.Set(new ReorderPolicyDto(
                command.SkuId, command.LocationId, command.ReorderPoint, command.ReorderQuantity, command.PreferredSupplierId, command.ToActor()));
        }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        if (isNew) await repository.SaveAsync(policy, EventPublishOption.InMemoryPublishOnly);
        else await repository.UpdateAsync(policy, EventPublishOption.InMemoryPublishOnly);

        return InventoryResponses.Ok();
    }
}

/// <summary>
/// HO-IMP-001 §11.4 C15 (ADR-110) -- raises an alert for the worker. A dedup hit (the same key, or a live <c>LowStock</c> / <c>LedgerMismatch</c> alert for the
/// same SKU and location) is a SUCCESS that writes nothing, so the worker can run as often as it likes. Never dispatched over HTTP.
/// </summary>
public sealed class RaiseStockAlertCommandHandler(
    ILogger<RaiseStockAlertCommandHandler> logger,
    IAggregateRootRepository<InventoryAlertAggregate> repository,
    IInventoryAlertWriter writer,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RaiseStockAlertCommand>
{
    public async Task<CommandResponse> HandleAsync(RaiseStockAlertCommand command)
    {
        logger.LogInformation("Raising a {Kind} alert. CorrelationId: {CorrelationId}", command.Kind, command.CorrelationId);

        var kind = InventoryMapper.ParseEnum<StockAlertKind>(command.Kind);

        if (command.DedupKey is { } key && await repository.ExistAsync(a => a.DedupKey == key)) return InventoryResponses.Ok();

        var isPairKind = kind == StockAlertKind.LowStock || kind == StockAlertKind.LedgerMismatch;
        if (isPairKind && await repository.ExistAsync(a => a.Kind == kind && a.SkuId == command.SkuId && a.LocationId == command.LocationId && a.Status != InventoryAlertStatus.Resolved))
            return InventoryResponses.Ok();

        var alert = new InventoryAlertAggregate(command.AlertId);
        try
        {
            alert.Raise(new StockAlertRaiseDto(
                kind, command.BranchId, command.SkuId, command.BatchId, command.LocationId, command.ThresholdDays, command.QuantityAtRaise,
                ReferenceKind: null, ReferenceId: null, command.DedupKey, timeProvider.GetUtcNow(), command.ToActor()));
        }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        if (!await writer.TryRaiseAsync(alert, CancellationToken.None))
        {
            // Two raises of the same alert raced: the unique index kept exactly one, which is what dedup means -- a success, not an error (C15).
            logger.LogInformation("The {Kind} alert was raised concurrently; keeping the first. CorrelationId: {CorrelationId}", command.Kind, command.CorrelationId);
        }

        return InventoryResponses.Ok();
    }
}

/// <summary>The worker half of US-IMP-12 AC1: a <c>LowStock</c> alert whose pair recovered is resolved. Resolving a resolved alert is a success.</summary>
public sealed class ResolveStockAlertCommandHandler(
    ILogger<ResolveStockAlertCommandHandler> logger,
    IAggregateRootRepository<InventoryAlertAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ResolveStockAlertCommand>
{
    public async Task<CommandResponse> HandleAsync(ResolveStockAlertCommand command)
    {
        logger.LogInformation("Resolving stock alert {AlertId}. CorrelationId: {CorrelationId}", command.AlertId, command.CorrelationId);

        var alert = await repository.GetByIdAsync(command.AlertId);
        if (alert is null) return InventoryResponses.NotFound(BusinessViolationCodes.StockAlertNotFound, nameof(command.AlertId), "That alert was not found.");
        if (alert.Status == InventoryAlertStatus.Resolved) return InventoryResponses.Ok();

        try { alert.Resolve(timeProvider.GetUtcNow(), command.Note ?? "Stock recovered above the reorder point.", command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(alert, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>HO-IMP-001 §12.4 -- a person has seen the alert. Only an <c>Open</c> alert of the ACTIVE branch can be acknowledged; one of another branch does not
/// exist to the caller.</summary>
public sealed class AcknowledgeStockAlertCommandHandler(
    ILogger<AcknowledgeStockAlertCommandHandler> logger,
    IAggregateRootRepository<InventoryAlertAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AcknowledgeStockAlertCommand>
{
    public async Task<CommandResponse> HandleAsync(AcknowledgeStockAlertCommand command)
    {
        logger.LogInformation("Acknowledging stock alert {AlertId}. CorrelationId: {CorrelationId}", command.AlertId, command.CorrelationId);

        var alert = await repository.GetByIdAsync(command.AlertId);
        if (alert is null || alert.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.StockAlertNotFound, nameof(command.AlertId), "That alert was not found.");

        try { alert.Acknowledge(command.UserContext!.UserId, timeProvider.GetUtcNow(), command.Note, command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(alert, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}
