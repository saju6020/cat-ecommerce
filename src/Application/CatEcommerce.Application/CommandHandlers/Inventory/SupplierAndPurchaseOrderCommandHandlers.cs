using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Inventory;

/// <summary>HO-IMP-001 §11.4 C3. The code is unique, compared upper-case (<c>ix_supplier_code</c> backs the pre-check, §12.5).</summary>
public sealed class CreateSupplierCommandHandler(
    ILogger<CreateSupplierCommandHandler> logger,
    IAggregateRootRepository<SupplierAggregate> repository)
    : ICommandHandlerAsync<CreateSupplierCommand>
{
    public async Task<CommandResponse> HandleAsync(CreateSupplierCommand command)
    {
        logger.LogInformation("Creating supplier {SupplierId}. CorrelationId: {CorrelationId}", command.SupplierId, command.CorrelationId);

        var code = command.Code.Trim().ToUpperInvariant();
        if (await repository.ExistAsync(s => s.Code == code))
            return InventoryResponses.Fail(BusinessViolationCodes.SupplierCodeAlreadyExists, nameof(command.Code), InventoryMessages.SupplierCodeAlreadyExists);

        var supplier = new SupplierAggregate(command.SupplierId);
        try { supplier.Create(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.SaveAsync(supplier, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(command.SupplierId);
    }
}

public sealed class UpdateSupplierCommandHandler(
    ILogger<UpdateSupplierCommandHandler> logger,
    IAggregateRootRepository<SupplierAggregate> repository)
    : ICommandHandlerAsync<UpdateSupplierCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateSupplierCommand command)
    {
        logger.LogInformation("Updating supplier {SupplierId}. CorrelationId: {CorrelationId}", command.SupplierId, command.CorrelationId);

        var supplier = await repository.GetByIdAsync(command.SupplierId);
        if (supplier is null)
            return InventoryResponses.NotFound(BusinessViolationCodes.SupplierNotFound, nameof(command.SupplierId), "That supplier was not found.");

        try { supplier.Update(command.ToDomainDto(supplier.Code)); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(supplier, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

public sealed class SetSupplierActivationCommandHandler(
    ILogger<SetSupplierActivationCommandHandler> logger,
    IAggregateRootRepository<SupplierAggregate> repository)
    : ICommandHandlerAsync<SetSupplierActivationCommand>
{
    public async Task<CommandResponse> HandleAsync(SetSupplierActivationCommand command)
    {
        logger.LogInformation("Setting supplier {SupplierId} active={IsActive}. CorrelationId: {CorrelationId}", command.SupplierId, command.IsActive, command.CorrelationId);

        var supplier = await repository.GetByIdAsync(command.SupplierId);
        if (supplier is null)
            return InventoryResponses.NotFound(BusinessViolationCodes.SupplierNotFound, nameof(command.SupplierId), "That supplier was not found.");

        if (command.IsActive) supplier.Reactivate(command.ToActor());
        else supplier.Deactivate(command.ToActor());

        await repository.UpdateAsync(supplier, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>
/// The checks Create and Replace share: the supplier exists and is active, the deliver-to location is an active location of THIS branch
/// that permits receipt, and every SKU exists and is active (the snapshot is taken here, §11.4 C4). Returns the draft or the failure.
/// </summary>
internal static class PurchaseOrderDraftBuilder
{
    public static async Task<(PurchaseOrderDraftDto? Draft, CommandResponse? Failure)> BuildAsync(
        string number, Guid branchId, Guid supplierId, Guid deliverToLocationId, string? currencyCode, DateOnly expectedDeliveryOn, string? notes,
        IReadOnlyList<PurchaseOrderLineInput> lines, InventoryActor actor,
        IAggregateRootRepository<SupplierAggregate> suppliers, IStockPositionReader positions, ISkuDirectory skus)
    {
        var supplier = await suppliers.GetByIdAsync(supplierId);
        if (supplier is null)
            return (null, InventoryResponses.Fail(BusinessViolationCodes.SupplierNotFound, nameof(supplierId), "That supplier was not found."));
        if (!supplier.IsActive)
            return (null, InventoryResponses.Fail(BusinessViolationCodes.SupplierInactive, nameof(supplierId), "That supplier is not active."));

        var locations = await positions.GetLocationsAsync([deliverToLocationId], CancellationToken.None);
        if (!locations.TryGetValue(deliverToLocationId, out var location) || location.BranchId != branchId || !location.IsActive)
            return (null, InventoryResponses.Fail(BusinessViolationCodes.StockLocationNotFound, nameof(deliverToLocationId), "That location does not exist at this branch."));
        if (!StockLocationRules.Permits(location.Kind, MovementKind.Receipt))
            return (null, InventoryResponses.Fail(BusinessViolationCodes.MovementNotPermittedAtLocation, nameof(deliverToLocationId), "Stock cannot be received at that location."));

        var profiles = (await skus.GetManyAsync(lines.Select(l => l.SkuId).ToList(), CancellationToken.None)).ToDictionary(s => s.SkuId);
        var lineDtos = new List<PurchaseOrderLineDto>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (!profiles.TryGetValue(lines[i].SkuId, out var sku))
                return (null, InventoryResponses.Fail(BusinessViolationCodes.SkuNotFound, $"Lines[{i}].SkuId", "That SKU does not exist."));
            if (!sku.IsActive)
                return (null, InventoryResponses.Fail(BusinessViolationCodes.SkuInactive, $"Lines[{i}].SkuId", "That SKU is not active."));

            lineDtos.Add(lines[i].ToDomainDto(sku));
        }

        var draft = new PurchaseOrderDraftDto(
            number, supplierId, branchId, deliverToLocationId, string.IsNullOrWhiteSpace(currencyCode) ? supplier.CurrencyCode : currencyCode,
            expectedDeliveryOn, notes, lineDtos, actor);

        return (draft, null);
    }
}

/// <summary>HO-IMP-001 §11.4 C4. The PO number comes from the sequence (ADR-114); SKUs and the deliver-to location are validated and
/// snapshotted by <see cref="PurchaseOrderDraftBuilder"/>.</summary>
public sealed class CreatePurchaseOrderCommandHandler(
    ILogger<CreatePurchaseOrderCommandHandler> logger,
    IAggregateRootRepository<PurchaseOrderAggregate> repository,
    IAggregateRootRepository<SupplierAggregate> suppliers,
    IStockPositionReader positions,
    ISkuDirectory skus,
    IDocumentNumberGenerator numbers)
    : ICommandHandlerAsync<CreatePurchaseOrderCommand>
{
    public async Task<CommandResponse> HandleAsync(CreatePurchaseOrderCommand command)
    {
        logger.LogInformation("Creating purchase order {PurchaseOrderId}. CorrelationId: {CorrelationId}", command.PurchaseOrderId, command.CorrelationId);

        var number = await numbers.NextAsync(DocumentKind.PurchaseOrder, CancellationToken.None);
        var (draft, failure) = await PurchaseOrderDraftBuilder.BuildAsync(
            number, command.BranchId, command.SupplierId, command.DeliverToLocationId, command.CurrencyCode, command.ExpectedDeliveryOn, command.Notes,
            command.Lines, command.ToActor(), suppliers, positions, skus);
        if (failure is not null) return failure;

        var order = new PurchaseOrderAggregate(command.PurchaseOrderId);
        try { order.CreateDraft(draft!); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.SaveAsync(order, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(number);
    }
}

public sealed class ReplacePurchaseOrderDraftCommandHandler(
    ILogger<ReplacePurchaseOrderDraftCommandHandler> logger,
    IAggregateRootRepository<PurchaseOrderAggregate> repository,
    IAggregateRootRepository<SupplierAggregate> suppliers,
    IStockPositionReader positions,
    ISkuDirectory skus)
    : ICommandHandlerAsync<ReplacePurchaseOrderDraftCommand>
{
    public async Task<CommandResponse> HandleAsync(ReplacePurchaseOrderDraftCommand command)
    {
        logger.LogInformation("Replacing draft of purchase order {PurchaseOrderId}. CorrelationId: {CorrelationId}", command.PurchaseOrderId, command.CorrelationId);

        var order = await repository.GetByIdAsync(command.PurchaseOrderId);
        if (order is null || order.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.PurchaseOrderNotFound, nameof(command.PurchaseOrderId), "That purchase order was not found.");

        var (draft, failure) = await PurchaseOrderDraftBuilder.BuildAsync(
            order.Number, command.BranchId, command.SupplierId, command.DeliverToLocationId, command.CurrencyCode, command.ExpectedDeliveryOn, command.Notes,
            command.Lines, command.ToActor(), suppliers, positions, skus);
        if (failure is not null) return failure;

        try { order.ReplaceDraft(draft!); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>Issue needs the supplier still active and "today" in the branch's own time zone (ADR-045).</summary>
public sealed class IssuePurchaseOrderCommandHandler(
    ILogger<IssuePurchaseOrderCommandHandler> logger,
    IAggregateRootRepository<PurchaseOrderAggregate> repository,
    IAggregateRootRepository<SupplierAggregate> suppliers,
    IInventoryClock clock,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<IssuePurchaseOrderCommand>
{
    public async Task<CommandResponse> HandleAsync(IssuePurchaseOrderCommand command)
    {
        logger.LogInformation("Issuing purchase order {PurchaseOrderId}. CorrelationId: {CorrelationId}", command.PurchaseOrderId, command.CorrelationId);

        var order = await repository.GetByIdAsync(command.PurchaseOrderId);
        if (order is null || order.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.PurchaseOrderNotFound, nameof(command.PurchaseOrderId), "That purchase order was not found.");

        var supplier = await suppliers.GetByIdAsync(order.SupplierId);
        if (supplier is null || !supplier.IsActive)
            return InventoryResponses.Fail(BusinessViolationCodes.SupplierInactive, nameof(order.SupplierId), "That supplier is not active.");

        try { order.Issue(await clock.LocalTodayAsync(order.BranchId, CancellationToken.None), timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>"Only while nothing has been received": the handler reads whether any posted receipt line references the order.</summary>
public sealed class CancelPurchaseOrderCommandHandler(
    ILogger<CancelPurchaseOrderCommandHandler> logger,
    IAggregateRootRepository<PurchaseOrderAggregate> repository,
    IStockPositionReader positions)
    : ICommandHandlerAsync<CancelPurchaseOrderCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelPurchaseOrderCommand command)
    {
        logger.LogInformation("Cancelling purchase order {PurchaseOrderId}. CorrelationId: {CorrelationId}", command.PurchaseOrderId, command.CorrelationId);

        var order = await repository.GetByIdAsync(command.PurchaseOrderId);
        if (order is null || order.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.PurchaseOrderNotFound, nameof(command.PurchaseOrderId), "That purchase order was not found.");

        var hasReceipts = await positions.PurchaseOrderHasReceiptsAsync(order.Id, CancellationToken.None);
        try { order.Cancel(command.Reason, hasReceipts, command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>Short-closing a partially received order -- the outstanding quantity is still shown afterwards (ADR-112: nothing is stored).</summary>
public sealed class ClosePurchaseOrderCommandHandler(
    ILogger<ClosePurchaseOrderCommandHandler> logger,
    IAggregateRootRepository<PurchaseOrderAggregate> repository)
    : ICommandHandlerAsync<ClosePurchaseOrderCommand>
{
    public async Task<CommandResponse> HandleAsync(ClosePurchaseOrderCommand command)
    {
        logger.LogInformation("Closing purchase order {PurchaseOrderId}. CorrelationId: {CorrelationId}", command.PurchaseOrderId, command.CorrelationId);

        var order = await repository.GetByIdAsync(command.PurchaseOrderId);
        if (order is null || order.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.PurchaseOrderNotFound, nameof(command.PurchaseOrderId), "That purchase order was not found.");

        try { order.Close(command.Reason, command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}
