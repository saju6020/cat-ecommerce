using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.DataMappers;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Application.CommandHandlers.Inventory;

/// <summary>
/// HO-IMP-001 §8.10 / §11.4 C10 (US-IMP-13 AC1) -- posts a transfer: the transfer is saved with TWO enlisted ledger entries per line (<c>TransferOut</c>
/// at the source, <c>TransferIn</c> at the destination, one reference), one commit, instant (OQ-12). Moving an expired or recalled batch is allowed --
/// BR-3 forbids USE, not movement -- so a transfer can quarantine a batch or consolidate it for disposal.
///
/// <b>The handler does NOT pre-check that the stock is there.</b> The CHECK on the projection is the control (ADR-099): a pre-read would race the
/// commit anyway, and one control that cannot be raced is better than two. A transfer of stock that is no longer free is a <c>409 InsufficientStock</c>.
/// The source is a location of the caller's ACTIVE branch; a destination in ANOTHER branch needs the caller to be assigned to it too.
/// </summary>
public sealed class PostStockTransferCommandHandler(
    ILogger<PostStockTransferCommandHandler> logger,
    IAggregateRootRepository<StockTransferAggregate> repository,
    IAggregateRootRepository<StaffBranchAssignmentAggregate> assignments,
    ISkuDirectory skus,
    IStockPositionReader positions,
    IInventoryLedgerWriter ledgerWriter,
    IDocumentNumberGenerator numbers,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<PostStockTransferCommand>
{
    public async Task<CommandResponse> HandleAsync(PostStockTransferCommand command)
    {
        logger.LogInformation("Posting stock transfer {TransferId}. CorrelationId: {CorrelationId}", command.TransferId, command.CorrelationId);

        var locations = await positions.GetLocationsAsync([command.FromLocationId, command.ToLocationId], CancellationToken.None);
        if (!locations.TryGetValue(command.FromLocationId, out var from) || from.BranchId != command.BranchId || !from.IsActive)
            return InventoryResponses.Fail(BusinessViolationCodes.StockLocationNotFound, nameof(command.FromLocationId), "The source location does not exist at this branch.");
        if (!locations.TryGetValue(command.ToLocationId, out var to) || !to.IsActive)
            return InventoryResponses.Fail(BusinessViolationCodes.StockLocationNotFound, nameof(command.ToLocationId), "The destination location does not exist.");

        if (to.BranchId != command.BranchId)
        {
            var assignment = await assignments.GetByIdAsync(command.UserContext!.UserId);
            if (assignment is null || !assignment.BranchIds.Contains(to.BranchId))
                return InventoryResponses.Fail(BusinessViolationCodes.StockLocationNotFound, nameof(command.ToLocationId), "The destination location does not exist.");
        }

        var batches = await positions.GetBatchFactsAsync(command.Lines.Select(l => l.BatchId).Distinct().ToList(), CancellationToken.None);
        var profiles = (await skus.GetManyAsync(command.Lines.Select(l => l.SkuId).Distinct().ToList(), CancellationToken.None)).ToDictionary(s => s.SkuId);
        for (var i = 0; i < command.Lines.Count; i++)
        {
            var line = command.Lines[i];
            if (!batches.TryGetValue(line.BatchId, out var batch) || batch.SkuId != line.SkuId || !profiles.TryGetValue(line.SkuId, out var sku))
                return InventoryResponses.Fail(BusinessViolationCodes.BatchNotFound, $"Lines[{i}].BatchId", "That batch does not exist for that SKU.");
            if (!StockQuantity.TryValidate(line.Quantity, sku.AllowsFractionalQuantity, out var code))
                return InventoryResponses.Fail(code!, $"Lines[{i}].Quantity", "The quantity must be greater than zero, whole unless the item is counted in fractions, with at most three decimal places.");
        }

        var transfer = new StockTransferAggregate(command.TransferId);
        try
        {
            transfer.Post(new StockTransferPostDto(
                await numbers.NextAsync(DocumentKind.StockTransfer, CancellationToken.None), command.BranchId, command.FromLocationId, command.ToLocationId, command.Reason,
                timeProvider.GetUtcNow(), command.Lines.Select(l => new StockTransferLineDto(l.LineId, l.SkuId, l.BatchId, l.Quantity)).ToList(), command.ToActor()));
        }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        var appended = await ledgerWriter.AppendAsync(transfer.BuildDrafts(to.BranchId), CancellationToken.None);
        if (!appended.IsSuccess) return InventoryResponses.Fail(appended);

        await repository.SaveAsync(transfer, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(transfer.Number);
    }
}

/// <summary>What a requested adjustment answers: whether it posted at once, its value and whether it now waits for an approver (US-IMP-13 AC2).</summary>
public sealed record RequestedAdjustment(string Number, string Status, decimal ValueBase, string CurrencyCode, bool RequiresApproval);

/// <summary>
/// HO-IMP-001 §8.10 / §11.4 C11 (US-IMP-13 AC2, ADR-107) -- a mandatory reason CATEGORY and a mandatory NOTE. The handler reads the facts the rules
/// need (each batch's status, whether it is expired in the branch's local date, its unit cost, the branch's threshold); the aggregate decides. Worth
/// at most the threshold it is <c>Posted</c> at once and its entries are enlisted with the save; worth more it is <c>PendingApproval</c> with NO
/// ledger entry until a different approver approves.
/// </summary>
public sealed class RequestStockAdjustmentCommandHandler(
    ILogger<RequestStockAdjustmentCommandHandler> logger,
    IAggregateRootRepository<StockAdjustmentAggregate> repository,
    IStockPositionReader positions,
    ISkuDirectory skus,
    IInventoryClock clock,
    IInventoryLedgerWriter ledgerWriter,
    IDocumentNumberGenerator numbers,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RequestStockAdjustmentCommand>
{
    public async Task<CommandResponse> HandleAsync(RequestStockAdjustmentCommand command)
    {
        logger.LogInformation("Requesting stock adjustment {AdjustmentId}. CorrelationId: {CorrelationId}", command.AdjustmentId, command.CorrelationId);

        var locations = await positions.GetLocationsAsync([command.LocationId], CancellationToken.None);
        if (!locations.TryGetValue(command.LocationId, out var location) || location.BranchId != command.BranchId || !location.IsActive)
            return InventoryResponses.Fail(BusinessViolationCodes.StockLocationNotFound, nameof(command.LocationId), "That location does not exist at this branch.");

        var reason = InventoryMapper.ParseNullableEnum<AdjustmentReason>(command.Reason);
        var today = await clock.LocalTodayAsync(command.BranchId, CancellationToken.None);
        var batches = await positions.GetBatchFactsAsync(command.Lines.Select(l => l.BatchId).Distinct().ToList(), CancellationToken.None);
        var costs = await positions.GetUnitCostsAsync(command.Lines.Select(l => l.BatchId).Distinct().ToList(), CancellationToken.None);
        var profiles = (await skus.GetManyAsync(command.Lines.Select(l => l.SkuId).Distinct().ToList(), CancellationToken.None)).ToDictionary(s => s.SkuId);

        var lines = new List<AdjustmentLineDto>();
        for (var i = 0; i < command.Lines.Count; i++)
        {
            var line = command.Lines[i];
            if (!batches.TryGetValue(line.BatchId, out var batch) || batch.SkuId != line.SkuId || batch.BranchId != command.BranchId)
                return InventoryResponses.Fail(BusinessViolationCodes.BatchNotFound, $"Lines[{i}].BatchId", "That batch does not exist for that SKU at this branch.");

            var kind = line.QuantityDelta >= 0m ? MovementKind.AdjustmentIn : MovementKind.AdjustmentOut;
            if (!StockLocationRules.Permits(location.Kind, kind))
                return InventoryResponses.Fail(BusinessViolationCodes.MovementNotPermittedAtLocation, nameof(command.LocationId), $"An adjustment is not permitted at a {location.Kind.DisplayName} location.");

            lines.Add(new AdjustmentLineDto(
                line.LineId, line.SkuId, line.BatchId, line.QuantityDelta, BatchExpiryRules.IsExpired(batch.ExpiresOn, today), batch.Status,
                costs.TryGetValue(line.BatchId, out var cost) ? cost.UnitCostBase : 0m));
        }

        var adjustment = new StockAdjustmentAggregate(command.AdjustmentId);
        try
        {
            adjustment.Request(new StockAdjustmentRequestDto(
                await numbers.NextAsync(DocumentKind.StockAdjustment, CancellationToken.None), command.BranchId, command.LocationId, reason, command.Note,
                settings.GetDecimal(SettingKeys.InventoryAdjustmentApprovalThreshold, command.BranchId), settings.GetString(SettingKeys.CommerceCurrencyCode, command.BranchId),
                timeProvider.GetUtcNow(), lines, command.ToActor()));
        }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        var appended = await ledgerWriter.AppendAsync(adjustment.BuildDrafts(), CancellationToken.None);
        if (!appended.IsSuccess) return InventoryResponses.Fail(appended);

        await repository.SaveAsync(adjustment, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok(new RequestedAdjustment(adjustment.Number, adjustment.Status.DisplayName, adjustment.ValueBase, adjustment.CurrencyCode, adjustment.Status == AdjustmentStatus.PendingApproval));
    }
}

/// <summary>The defence-in-depth role check (§11.4 C11): the endpoint already requires <c>Stock.ApproveAdjustment</c>; a handler dispatched any
/// other way must not be a back door around it.</summary>
internal static class ApproverCheck
{
    public static CommandResponse? RefuseIfNotApprover(UserContext? user)
    {
        var holds = ActionRoles.TryGetRoles(Actions.Stock.ApproveAdjustment, out var roles) && roles.Intersect(user?.Roles ?? [], StringComparer.Ordinal).Any();
        return holds ? null : InventoryResponses.Fail(BusinessViolationCodes.ActionNotPermitted, "Action", "You are not permitted to approve stock adjustments or counts.");
    }
}

/// <summary>Approve by a staff member holding <c>Stock.ApproveAdjustment</c> and DIFFERENT from the requester (<c>ApproverMustDiffer</c>); the entries
/// are enlisted with the save, so if stock moved meanwhile the CHECK answers 409.</summary>
public sealed class ApproveStockAdjustmentCommandHandler(
    ILogger<ApproveStockAdjustmentCommandHandler> logger,
    IAggregateRootRepository<StockAdjustmentAggregate> repository,
    IInventoryLedgerWriter ledgerWriter,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ApproveStockAdjustmentCommand>
{
    public async Task<CommandResponse> HandleAsync(ApproveStockAdjustmentCommand command)
    {
        logger.LogInformation("Approving stock adjustment {AdjustmentId}. CorrelationId: {CorrelationId}", command.AdjustmentId, command.CorrelationId);

        if (ApproverCheck.RefuseIfNotApprover(command.UserContext) is { } denied) return denied;
        var adjustment = await repository.GetByIdAsync(command.AdjustmentId);
        if (adjustment is null || adjustment.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.StockAdjustmentNotFound, nameof(command.AdjustmentId), "That adjustment was not found.");

        try { adjustment.Approve(command.UserContext!.UserId, timeProvider.GetUtcNow(), command.Note, command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        var appended = await ledgerWriter.AppendAsync(adjustment.BuildDrafts(), CancellationToken.None);
        if (!appended.IsSuccess) return InventoryResponses.Fail(appended);

        await repository.UpdateAsync(adjustment, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

public sealed class RejectStockAdjustmentCommandHandler(
    ILogger<RejectStockAdjustmentCommandHandler> logger,
    IAggregateRootRepository<StockAdjustmentAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RejectStockAdjustmentCommand>
{
    public async Task<CommandResponse> HandleAsync(RejectStockAdjustmentCommand command)
    {
        logger.LogInformation("Rejecting stock adjustment {AdjustmentId}. CorrelationId: {CorrelationId}", command.AdjustmentId, command.CorrelationId);

        if (ApproverCheck.RefuseIfNotApprover(command.UserContext) is { } denied) return denied;
        var adjustment = await repository.GetByIdAsync(command.AdjustmentId);
        if (adjustment is null || adjustment.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.StockAdjustmentNotFound, nameof(command.AdjustmentId), "That adjustment was not found.");

        try { adjustment.Reject(command.UserContext!.UserId, timeProvider.GetUtcNow(), command.Note, command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(adjustment, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}

/// <summary>Only the requester may withdraw a request that is still waiting.</summary>
public sealed class WithdrawStockAdjustmentCommandHandler(
    ILogger<WithdrawStockAdjustmentCommandHandler> logger,
    IAggregateRootRepository<StockAdjustmentAggregate> repository,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<WithdrawStockAdjustmentCommand>
{
    public async Task<CommandResponse> HandleAsync(WithdrawStockAdjustmentCommand command)
    {
        logger.LogInformation("Withdrawing stock adjustment {AdjustmentId}. CorrelationId: {CorrelationId}", command.AdjustmentId, command.CorrelationId);

        var adjustment = await repository.GetByIdAsync(command.AdjustmentId);
        if (adjustment is null || adjustment.BranchId != command.BranchId)
            return InventoryResponses.NotFound(BusinessViolationCodes.StockAdjustmentNotFound, nameof(command.AdjustmentId), "That adjustment was not found.");

        try { adjustment.Withdraw(command.UserContext!.UserId, timeProvider.GetUtcNow(), command.ToActor()); }
        catch (DomainRuleViolationException ex) { return InventoryResponses.Fail(ex); }

        await repository.UpdateAsync(adjustment, EventPublishOption.InMemoryPublishOnly);
        return InventoryResponses.Ok();
    }
}
