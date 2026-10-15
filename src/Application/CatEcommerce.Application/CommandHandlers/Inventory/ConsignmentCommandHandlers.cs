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

/// <summary>The rule "POs are Issued or Closed and of THIS branch" needs the purchase orders themselves, so it is the handler's (§8.5).
/// Suppliers may differ -- consolidated freight.</summary>
internal static class ConsignmentPurchaseOrderCheck
{
    public static async Task<CommandResponse?> RefuseIfInvalidAsync(
        IReadOnlyList<Guid> purchaseOrderIds, Guid branchId, IAggregateRootRepository<PurchaseOrderAggregate> orders)
    {
        for (var i = 0; i < purchaseOrderIds.Count; i++)
        {
            var order = await orders.GetByIdAsync(purchaseOrderIds[i]);
            if (order is null || order.BranchId != branchId)
                return InventoryResponses.Fail(BusinessViolationCodes.ConsignmentPurchaseOrderMismatch, $"PurchaseOrderIds[{i}]", "That purchase order does not exist at this branch.");
            if (order.Status != PurchaseOrderStatus.Issued && order.Status != PurchaseOrderStatus.Closed)
                return InventoryResponses.Fail(BusinessViolationCodes.ConsignmentPurchaseOrderMismatch, $"PurchaseOrderIds[{i}]", "Only an issued or closed purchase order can be grouped into a consignment.");
        }

        return null;
    }
}

/// <summary>HO-IMP-001 §11.4 C5. The reference comes from the sequence (ADR-114); every PO is checked first.</summary>
public sealed class PlanConsignmentCommandHandler(
    ILogger<PlanConsignmentCommandHandler> logger,
    IAggregateRootRepository<ConsignmentAggregate> repository,
    IAggregateRootRepository<PurchaseOrderAggregate> orders,
    IDocumentNumberGenerator numbers,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<PlanConsignmentCommand>
{
    public async Task<CommandResponse> HandleAsync(PlanConsignmentCommand command)
    {
        logger.LogInformation("Planning consignment {ConsignmentId}. CorrelationId: {CorrelationId}", command.ConsignmentId, command.CorrelationId);

        if (await ConsignmentPurchaseOrderCheck.RefuseIfInvalidAsync(command.PurchaseOrderIds, command.BranchId, orders) is { } refusal) return refusal;

        var reference = await numbers.NextAsync(DocumentKind.Consignment, CancellationToken.None);
        var consignment = new ConsignmentAggregate(command.ConsignmentId);
        try { consignment.Plan(new ConsignmentPlanDto(reference, command.BranchId, timeProvider.GetUtcNow(), command.ToDetailsDto())); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.SaveAsync(consignment, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(reference);
    }
}

/// <summary>The loading and version check every consignment update shares: another branch's consignment is "not found"; a stale
/// <c>ExpectedVersion</c> is <c>ConsignmentVersionConflict</c> (409).</summary>
internal static class ConsignmentLoader
{
    public static async Task<(ConsignmentAggregate? Consignment, CommandResponse? Failure)> LoadAsync(
        IAggregateRootRepository<ConsignmentAggregate> repository, Guid consignmentId, Guid branchId, int? expectedVersion = null)
    {
        var consignment = await repository.GetByIdAsync(consignmentId);
        if (consignment is null || consignment.BranchId != branchId)
            return (null, InventoryResponses.NotFound(BusinessViolationCodes.ConsignmentNotFound, nameof(consignmentId), "That consignment was not found."));

        if (expectedVersion is { } expected && consignment.Version != expected)
            return (null, InventoryResponses.Fail(BusinessViolationCodes.ConsignmentVersionConflict, "ExpectedVersion",
                $"Expected version {expected} but the consignment is at {consignment.Version}. Reload and try again."));

        return (consignment, null);
    }
}

public sealed class UpdateConsignmentDetailsCommandHandler(
    ILogger<UpdateConsignmentDetailsCommandHandler> logger,
    IAggregateRootRepository<ConsignmentAggregate> repository,
    IAggregateRootRepository<PurchaseOrderAggregate> orders)
    : ICommandHandlerAsync<UpdateConsignmentDetailsCommand>
{
    public async Task<CommandResponse> HandleAsync(UpdateConsignmentDetailsCommand command)
    {
        logger.LogInformation("Updating consignment {ConsignmentId}. CorrelationId: {CorrelationId}", command.ConsignmentId, command.CorrelationId);

        var (consignment, failure) = await ConsignmentLoader.LoadAsync(repository, command.ConsignmentId, command.BranchId, command.ExpectedVersion);
        if (failure is not null) return failure;
        if (await ConsignmentPurchaseOrderCheck.RefuseIfInvalidAsync(command.PurchaseOrderIds, command.BranchId, orders) is { } refusal) return refusal;

        try { consignment!.UpdateDetails(command.ToDetailsDto()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(consignment!, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>The strict sequence <c>Planned -> InTransit -> AtCustoms -> Cleared</c>; clearing needs permit, certificate and declaration numbers
/// (<c>ClearanceDocumentsMissing</c>) -- both are the aggregate's rules.</summary>
public sealed class AdvanceConsignmentStatusCommandHandler(
    ILogger<AdvanceConsignmentStatusCommandHandler> logger,
    IAggregateRootRepository<ConsignmentAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AdvanceConsignmentStatusCommand>
{
    public async Task<CommandResponse> HandleAsync(AdvanceConsignmentStatusCommand command)
    {
        logger.LogInformation("Advancing consignment {ConsignmentId} to {To}. CorrelationId: {CorrelationId}", command.ConsignmentId, command.To, command.CorrelationId);

        var (consignment, failure) = await ConsignmentLoader.LoadAsync(repository, command.ConsignmentId, command.BranchId);
        if (failure is not null) return failure;

        try { consignment!.AdvanceStatus(InventoryMapper.ParseEnum<ConsignmentStatus>(command.To), command.On, timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(consignment!, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

public sealed class CancelConsignmentCommandHandler(
    ILogger<CancelConsignmentCommandHandler> logger,
    IAggregateRootRepository<ConsignmentAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelConsignmentCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelConsignmentCommand command)
    {
        logger.LogInformation("Cancelling consignment {ConsignmentId}. CorrelationId: {CorrelationId}", command.ConsignmentId, command.CorrelationId);

        var (consignment, failure) = await ConsignmentLoader.LoadAsync(repository, command.ConsignmentId, command.BranchId);
        if (failure is not null) return failure;

        try { consignment!.Cancel(command.Reason, timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(consignment!, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>HO-IMP-001 §11.4 C6 -- METADATA plus a storage key; there is no file byte anywhere (§4.3, ADR-113, §20.8).</summary>
public sealed class AddConsignmentDocumentCommandHandler(
    ILogger<AddConsignmentDocumentCommandHandler> logger,
    IAggregateRootRepository<ConsignmentAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AddConsignmentDocumentCommand>
{
    public async Task<CommandResponse> HandleAsync(AddConsignmentDocumentCommand command)
    {
        logger.LogInformation("Adding a document to consignment {ConsignmentId}. CorrelationId: {CorrelationId}", command.ConsignmentId, command.CorrelationId);

        var (consignment, failure) = await ConsignmentLoader.LoadAsync(repository, command.ConsignmentId, command.BranchId);
        if (failure is not null) return failure;

        try { consignment!.AddDocument(command.ToDocumentDto(timeProvider.GetUtcNow())); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(consignment!, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(command.DocumentId);
    }
}

/// <summary>The old document stays, flagged superseded; the new one is added. There is no remove (NFR-LEG-2).</summary>
public sealed class SupersedeConsignmentDocumentCommandHandler(
    ILogger<SupersedeConsignmentDocumentCommandHandler> logger,
    IAggregateRootRepository<ConsignmentAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<SupersedeConsignmentDocumentCommand>
{
    public async Task<CommandResponse> HandleAsync(SupersedeConsignmentDocumentCommand command)
    {
        logger.LogInformation("Superseding document {DocumentId} of consignment {ConsignmentId}. CorrelationId: {CorrelationId}", command.SupersededDocumentId, command.ConsignmentId, command.CorrelationId);

        var (consignment, failure) = await ConsignmentLoader.LoadAsync(repository, command.ConsignmentId, command.BranchId);
        if (failure is not null) return failure;

        try { consignment!.SupersedeDocument(command.SupersededDocumentId, command.ToDocumentDto(timeProvider.GetUtcNow())); }
        catch (DomainRuleViolationException ex)
        {
            return ex.ErrorCode == BusinessViolationCodes.ConsignmentDocumentNotFound
                ? InventoryResponses.NotFound(ex.ErrorCode, ex.PropertyName, ex.Message)
                : InventoryResponses.Fail(ex);
        }

        await repository.UpdateAsync(consignment!, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(command.DocumentId);
    }
}

/// <summary>HO-IMP-001 §11.4 C7 -- <c>Consignments.ManageCosts</c> (the accountant's right, OQ-8). A stale <c>ExpectedVersion</c> is a 409. When an
/// allocation already exists it becomes STALE (§8.11).</summary>
public sealed class ReplaceConsignmentCostsCommandHandler(
    ILogger<ReplaceConsignmentCostsCommandHandler> logger,
    IAggregateRootRepository<ConsignmentAggregate> repository)
    : ICommandHandlerAsync<ReplaceConsignmentCostsCommand>
{
    public async Task<CommandResponse> HandleAsync(ReplaceConsignmentCostsCommand command)
    {
        logger.LogInformation("Replacing the costs of consignment {ConsignmentId}. CorrelationId: {CorrelationId}", command.ConsignmentId, command.CorrelationId);

        var (consignment, failure) = await ConsignmentLoader.LoadAsync(repository, command.ConsignmentId, command.BranchId, command.ExpectedVersion);
        if (failure is not null) return failure;

        try { consignment!.ReplaceCostsAndRates(command.ToDomainDto()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(consignment!, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>What an allocation answers (§12.4): the lines, the total of the cost components in base currency and when it was allocated.</summary>
public sealed record AllocatedLandedCost(IReadOnlyList<LandedCostLineDto> Lines, decimal TotalCostsBase, string CurrencyCode, DateTimeOffset AllocatedOn);

/// <summary>
/// HO-IMP-001 §8.11 / §11.4 C13 (US-IMP-05, ADR-106) -- reads the consignment's received batches, runs the pure <see cref="LandedCostAllocator"/> and records
/// the lines on the CONSIGNMENT. Only the consignment is written; the batches and the ledger are untouched (rule 1), so re-allocation is always safe.
/// </summary>
public sealed class AllocateLandedCostCommandHandler(
    ILogger<AllocateLandedCostCommandHandler> logger,
    IAggregateRootRepository<ConsignmentAggregate> repository,
    IStockPositionReader positions,
    LandedCostAllocator allocator,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<AllocateLandedCostCommand>
{
    public async Task<CommandResponse> HandleAsync(AllocateLandedCostCommand command)
    {
        logger.LogInformation("Allocating landed cost of consignment {ConsignmentId}. CorrelationId: {CorrelationId}", command.ConsignmentId, command.CorrelationId);

        var (consignment, failure) = await ConsignmentLoader.LoadAsync(repository, command.ConsignmentId, command.BranchId, command.ExpectedVersion);
        if (failure is not null) return failure;

        var baseCurrency = settings.GetString(SettingKeys.CommerceCurrencyCode, command.BranchId);
        var received = await positions.GetConsignmentBatchesAsync(command.ConsignmentId, CancellationToken.None);

        var result = allocator.Allocate(
            received.Select(b => new LandedCostBatchInput(b.BatchId, b.ReceivedQuantity, b.PurchaseUnitCostAmount, b.PurchaseUnitCostCurrency)).ToList(),
            consignment!.CostComponents.Select(c => new LandedCostComponentInput(c.Amount, c.CurrencyCode)).ToList(),
            consignment.ExchangeRates.ToDictionary(r => r.CurrencyCode, r => r.RateToBase, StringComparer.Ordinal), baseCurrency);
        if (!result.IsSuccess) return InventoryResponses.Fail(result.ViolationCode!, nameof(command.ConsignmentId), result.Message!);

        var allocatedOn = timeProvider.GetUtcNow();
        try { consignment.RecordLandedCostAllocation(new LandedCostAllocationDto(result.Lines, allocatedOn, command.ToActor())); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(consignment, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(new AllocatedLandedCost(result.Lines, result.TotalCostsBase, baseCurrency, allocatedOn));
    }
}
