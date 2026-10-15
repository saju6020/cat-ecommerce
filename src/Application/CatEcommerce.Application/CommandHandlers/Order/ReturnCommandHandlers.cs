using CatEcommerce.Application.Commands.Inventory;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Validation;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>Several violations in one answer (a request refused whole with a reason per line, §8.10) -- the same <c>ValidationResponse</c> shape as a single one.</summary>
internal static class ReturnHandlerSupport
{
    public static CommandResponse Violations(IEnumerable<(string Code, string Property, string Message)> violations)
    {
        var response = new ValidationResponse();
        foreach (var (code, property, message) in violations) response.AddError(errorMessage: message, propertyName: property, errorCode: code, resourceName: "Return");
        return new CommandResponse { IsSuccess = false, ValidationResult = response };
    }

    public static CommandResponse ReturnNotFound() => Violation(BusinessViolationCodes.ReturnNotFound, "ReturnRequestId", "Return not found.", "Return");

    /// <summary>The units of an order line that came back, assigned to the batches they were sold from -- the allocations in order, skipping what earlier returns of the
    /// line already took (a bundle: each component by its units per bundle). Ordering never chooses a batch: it replays the allocation Inventory made.</summary>
    public static IReadOnlyList<ReturnReceipt> ReceiptsFor(OrderLine line, int quantity, int alreadyReturned)
    {
        var receipts = new List<ReturnReceipt>();
        foreach (var (skuId, total) in line.RequiredBySku())
        {
            var perUnit = total / (decimal)line.Quantity;
            var skip = alreadyReturned * perUnit;
            var take = quantity * perUnit;

            foreach (var allocation in line.Allocations.Where(a => a.SkuId == skuId))
            {
                var available = allocation.Quantity;
                if (skip >= available) { skip -= available; continue; }

                available -= skip;
                skip = 0m;
                var use = Math.Min(available, take);
                if (use <= 0m) break;

                receipts.Add(new ReturnReceipt(skuId, allocation.BatchId, allocation.LocationId, use));
                take -= use;
                if (take <= 0m) break;
            }
        }

        return receipts;
    }
}

/// <summary>
/// HO-ORD-001 §11.4 C22 (US-ORD-13, BR-10, ADR-210) -- a return request. A customer's is on their own order (<c>404</c> otherwise); staff file one on the customer's behalf for an
/// order of their active branch. <see cref="ReturnEligibilityPolicy"/> judges every line (the window in branch-local days, the quantity left after earlier and
/// open returns, opened food and used litter unless a batch is recalled or the reason is a defect); <b>any refused line refuses the whole request</b> with a reason per line
/// (<c>Lines[i]</c>) so the customer can resubmit without it. An eligible request is <c>Requested</c>, waiting for staff approval.
/// </summary>
public sealed class RequestReturnCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<ReturnRequestAggregate> returns,
    IAggregateRootRepository<BranchAggregate> branches,
    IReturnFinder returnFinder,
    IStockLedger stock,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RequestReturnCommand>
{
    public async Task<CommandResponse> HandleAsync(RequestReturnCommand command)
    {
        var customerId = CartCaller.CustomerId(command);
        OrderAggregate? order;
        if (customerId is { } customer) order = await OrderAccess.ForCustomerAsync(orders, command.OrderId, customer);
        else if (command.BranchId is { } branchId) order = await OrderAccess.ForBranchAsync(orders, command.OrderId, branchId);
        else order = null;

        if (order is null) return OrderAccess.NotFound();

        var now = timeProvider.GetUtcNow();
        var branch = await branches.GetByIdAsync(order.BranchId);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(branch?.TimeZoneId ?? "UTC");
        var open = await returnFinder.GetOpenQuantitiesAsync(order.Id, default);
        var recalled = await RecalledBatchesAsync(order);

        var inputs = new List<ReturnLineEligibilityInput>();
        for (var i = 0; i < command.Lines.Count; i++)
        {
            var requested = command.Lines[i];
            var line = order.Lines.FirstOrDefault(l => l.Id == requested.OrderLineId);
            if (line is null)
                return ReturnHandlerSupport.Violations([(BusinessViolationCodes.ReturnQuantityExceeded, $"Lines[{i}]", "That item is not part of this order.")]);

            inputs.Add(new ReturnLineEligibilityInput(
                line.Id, line.ProductName, line.Quantity, line.QuantityReturned, open.GetValueOrDefault(line.Id), line.ReturnClass, line.Allocations.Any(a => recalled.Contains(a.BatchId)),
                requested.Quantity, requested.Reason, requested.Condition));
        }

        var verdicts = ReturnEligibilityPolicy.Evaluate(new ReturnEligibilityInput(
            order.Status, order.DeliveredOn is { } delivered ? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(delivered, zone).DateTime) : null,
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime), settings.GetInt(SettingKeys.OrdersReturnWindowDays), inputs));

        var refused = verdicts.Select((v, i) => (Verdict: v, Index: i)).Where(x => !x.Verdict.Eligible).ToList();
        if (refused.Count > 0)
            return ReturnHandlerSupport.Violations(refused.Select(x => (x.Verdict.Code!, $"Lines[{x.Index}]", x.Verdict.Message!)));

        var request = new ReturnRequestAggregate(command.ReturnRequestId);
        try
        {
            request.Request(new ReturnRequestDto(
                order.Id, order.CustomerId, ReturnKind.CustomerReturn,
                command.Lines.Select((l, i) => new ReturnLineDto(l.OrderLineId, l.Quantity, l.Reason, l.Condition, l.CustomerNote, verdicts[i].RequiresDefectConfirmation)).ToList(),
                command.UserContext!.UserId, now, command.UserContext, command.CorrelationId));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await returns.SaveAsync(request, EventPublishOption.InMemoryPublishOnly);
        return Success(new RequestReturnResult(request.Id, request.Status.DisplayName));
    }

    private async Task<HashSet<Guid>> RecalledBatchesAsync(OrderAggregate order)
    {
        var batchIds = order.Lines.SelectMany(l => l.Allocations).Select(a => a.BatchId).Distinct().ToList();
        if (batchIds.Count == 0) return [];

        var usability = await stock.GetBatchUsabilityAsync(order.BranchId, batchIds, default);
        return usability.IsSuccess ? usability.Value!.Where(u => u.IsRecalled).Select(u => u.BatchId).ToHashSet() : [];
    }
}

/// <summary>C22 -- staff approve a requested return (<c>Requested -> Approved</c>). A line flagged for a defect needs it confirmed with a note
/// (<c>DefectConfirmationRequired</c>, BR-10). The customer is told.</summary>
public sealed class ApproveReturnCommandHandler(
    IAggregateRootRepository<ReturnRequestAggregate> returns,
    IAggregateRootRepository<OrderAggregate> orders,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ApproveReturnCommand>
{
    public async Task<CommandResponse> HandleAsync(ApproveReturnCommand command)
    {
        var request = await returns.GetByIdAsync(command.ReturnRequestId);
        var order = request is null ? null : await OrderAccess.ForBranchAsync(orders, request.OrderId, command.BranchId);
        if (request is null || order is null) return ReturnHandlerSupport.ReturnNotFound();

        try
        {
            request.Approve(
                command.UserContext!.UserId, command.Lines.Select(l => new DefectConfirmationDto(l.OrderLineId, l.DefectConfirmed, l.DefectNote)).ToList(), command.Note,
                timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, "Return");
        }

        OrderNotifications.EnqueueReturnDecided(outbox, links, order, "Your return was approved", command.CorrelationId, command.UserContext);
        await returns.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

public sealed class RejectReturnCommandHandler(
    IAggregateRootRepository<ReturnRequestAggregate> returns,
    IAggregateRootRepository<OrderAggregate> orders,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RejectReturnCommand>
{
    public async Task<CommandResponse> HandleAsync(RejectReturnCommand command)
    {
        var request = await returns.GetByIdAsync(command.ReturnRequestId);
        var order = request is null ? null : await OrderAccess.ForBranchAsync(orders, request.OrderId, command.BranchId);
        if (request is null || order is null) return ReturnHandlerSupport.ReturnNotFound();

        try
        {
            request.Reject(command.UserContext!.UserId, command.Note, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, "Return");
        }

        OrderNotifications.EnqueueReturnDecided(outbox, links, order, "Your return was not approved", command.CorrelationId, command.UserContext);
        await returns.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// C23 (US-ORD-14 AC1, ADR-208) -- the approved goods arrived. Per line, Inventory's <c>ReceiveReturnAsync</c> puts each unit into the branch's quarantine location
/// (never sellable) <b>against the batch it was sold from</b> -- Ordering replays the line's allocations, it chooses nothing -- stamped with the customer and the line's cat.
/// The order (tracked) counts the units as returned (<c>Delivered -> Returned</c> when every line is back) and the return becomes <c>Received</c>. One save.
/// </summary>
public sealed class ReceiveReturnCommandHandler(
    IAggregateRootRepository<ReturnRequestAggregate> returns,
    IAggregateRootRepository<OrderAggregate> orders,
    IStockLedger stock,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ReceiveReturnCommand>
{
    public async Task<CommandResponse> HandleAsync(ReceiveReturnCommand command)
    {
        var request = await returns.GetByIdAsync(command.ReturnRequestId);
        var order = request is null ? null : await OrderAccess.ForBranchAsync(orders, request.OrderId, command.BranchId);
        if (request is null || order is null) return ReturnHandlerSupport.ReturnNotFound();

        var now = timeProvider.GetUtcNow();
        var receipts = new List<ReturnReceiptDto>();
        try
        {
            foreach (var received in command.Lines)
            {
                var orderLine = order.Lines.FirstOrDefault(l => l.Id == received.OrderLineId)
                    ?? throw new DomainRuleViolationException(BusinessViolationCodes.InvalidReturnTransition, "That order line is not part of this return.", nameof(command.Lines));

                var batches = received.QuantityReceived > 0 ? ReturnHandlerSupport.ReceiptsFor(orderLine, received.QuantityReceived, orderLine.QuantityReturned) : [];
                foreach (var receipt in batches)
                {
                    var result = await stock.ReceiveReturnAsync(
                        new StockReturn(order.BranchId, receipt.SkuId, receipt.BatchId, receipt.Quantity, request.Kind == ReturnKind.DeliveryFailure ? "Delivery failed" : "Customer return"),
                        new StockReference(StockReferenceKind.CustomerReturn, request.Id, orderLine.Id, order.OrderNumber), new StockParty(order.CustomerId, orderLine.CatId), default);

                    if (!result.IsSuccess)
                        return Violation(result.ViolationCode ?? BusinessViolationCodes.ReturnReceiptExceedsApproved, result.PropertyName ?? "Lines", result.Message ?? "The goods could not be received into quarantine.", "Return");
                }

                receipts.Add(new ReturnReceiptDto(orderLine.Id, received.QuantityReceived, batches));
            }

            request.Receive(command.UserContext!.UserId, receipts, now, command.UserContext, command.CorrelationId);
            foreach (var receipt in receipts.Where(r => r.QuantityReceived > 0))
                order.RegisterReturnedQuantity(receipt.OrderLineId, receipt.QuantityReceived, now, command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message, "Return");
        }

        await returns.UpdateAsync(request, EventPublishOption.InMemoryPublishOnly); // commits the tracked order and the enlisted quarantine entries with it
        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);    // dispatches the order's own events (e.g. OrderReturnedEvent)
        return Success();
    }
}
