using CatEcommerce.Application.Commands.Billing;
using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>HO-ORD-001 §11.4 C16 (US-ORD-09) -- <c>Confirmed -> Packing</c>. An invalid move names both states ("A delivered order cannot be packed.").</summary>
public sealed class StartPackingCommandHandler(IAggregateRootRepository<OrderAggregate> orders, TimeProvider timeProvider) : ICommandHandlerAsync<StartPackingCommand>
{
    public async Task<CommandResponse> HandleAsync(StartPackingCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();

        try
        {
            order.StartPacking(command.UserContext!, timeProvider.GetUtcNow(), command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// C16 (US-ORD-11, US-ORD-09, BR-14, US-IMP-11 AC4) -- dispatch a packed delivery order. The aggregate checks the move (<c>Packing -> Dispatched</c>) and that
/// every line is allocated; the handler checks the courier is active (<c>CourierNotFound</c>) and that no allocated batch was recalled or has expired
/// (<c>BatchRecalled</c> / <c>BatchExpired</c> -- reallocate the line); then Inventory turns the reservation into the sale and the tracking link is built from
/// the courier's https template. The order, the sale and the message commit in one save; the customer is told with the courier and the tracking number (or
/// "to follow").
/// </summary>
public sealed class DispatchOrderCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<CourierAggregate> couriers,
    IStockLedger stock,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<DispatchOrderCommand>
{
    public async Task<CommandResponse> HandleAsync(DispatchOrderCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();

        var courier = await couriers.GetByIdAsync(command.CourierId);
        if (courier is not { IsActive: true })
            return Violation(BusinessViolationCodes.CourierNotFound, nameof(command.CourierId), "That courier is not available.");

        var now = timeProvider.GetUtcNow();
        var tracking = string.IsNullOrWhiteSpace(command.TrackingNumber) ? null : command.TrackingNumber.Trim();
        try
        {
            order.Dispatch(
                new Shipment(courier.Id, courier.Name, tracking, courier.BuildTrackingUrl(tracking), now, command.UserContext!.UserId, null, null), now, command.UserContext, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (await OrderStockFulfilment.CheckBatchesAsync(stock, order) is { } unusable) return unusable;
        if (await OrderStockFulfilment.ConsumeAsync(stock, order) is { } refused) return refused;

        OrderNotifications.EnqueueDispatched(outbox, links, order, command.CorrelationId, command.UserContext);
        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C16 -- a packed pickup order is waiting at the branch (<c>Dispatched</c> reads as "ready for collection"). The batches are checked (BR-14); the
/// stock stays reserved until the hand-over (C18). Held for <c>orders.pickupCollectionDays</c>.</summary>
public sealed class MarkReadyForCollectionCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<BranchAggregate> branches,
    IStockLedger stock,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    IBusinessSettings settings,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<MarkReadyForCollectionCommand>
{
    public async Task<CommandResponse> HandleAsync(MarkReadyForCollectionCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();

        var branch = await branches.GetByIdAsync(order.Fulfilment.PickupBranchId ?? order.BranchId) ?? await branches.GetByIdAsync(order.BranchId);
        var now = timeProvider.GetUtcNow();
        var collectBy = OrderAccess.LocalDate(now, branch!.TimeZoneId).AddDays(settings.GetInt(SettingKeys.OrdersPickupCollectionDays, order.BranchId));

        try
        {
            order.MarkReadyForCollection(collectBy, now, command.UserContext!, command.CorrelationId);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (await OrderStockFulfilment.CheckBatchesAsync(stock, order) is { } unusable) return unusable;

        OrderNotifications.EnqueueReadyForCollection(outbox, links, order, branch.Name, branch.AddressLine, command.CorrelationId, command.UserContext);
        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>C17 (US-ORD-11 AC2) -- the tracking number of a dispatched order, entered by hand. The link comes from the courier's template; the customer is told
/// only the first time a number is set.</summary>
public sealed class RecordTrackingNumberCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<CourierAggregate> couriers,
    INotificationOutbox outbox,
    INotificationLinkBuilder links)
    : ICommandHandlerAsync<RecordTrackingNumberCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordTrackingNumberCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();

        var courier = order.Shipment is { } shipment ? await couriers.GetByIdAsync(shipment.CourierId) : null;
        bool first;
        try
        {
            first = order.RecordTracking(command.TrackingNumber, courier?.BuildTrackingUrl(command.TrackingNumber));
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }

        if (first) OrderNotifications.EnqueueTrackingAdded(outbox, links, order, command.CorrelationId, command.UserContext!);
        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// C18 (US-ORD-20 AC2, US-ORD-08 AC2, ADR-205) -- hand-over: the delivery of a delivery order or the collection of a pickup order. The aggregate enforces the
/// money rules (<c>CodCollectionRequired</c>; <c>CollectionPaymentMismatch</c>; <c>TerminalReferenceRequired</c>). In one database transaction (ADR-215): the
/// order moves to <c>Delivered</c>; a pickup's reservation becomes the sale; the cash collected on delivery is recorded in Billing as <c>CashOnDelivery</c>
/// with its collector, a collection payment as <c>CashInStore</c> or <c>CardInStore</c> with the slip reference, each allocated to the invoice; the customer is
/// told. A failure after staff entered the money leaves a reconciliation issue (ADR-205).
/// </summary>
public sealed class RecordDeliveryCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IStockLedger stock,
    IAggregateRootRepository<BranchAggregate> branches,
    ITransactionalScope transaction,
    IInMemoryDispatcher dispatcher,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    IReconciliationIssueRecorder reconciliation,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordDeliveryCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordDeliveryCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();
        if (order.Version != command.ExpectedVersion) return OrderAccess.Stale();

        try
        {
            return await transaction.RunAsync(() => DeliverAsync(command, order), response => response.IsSuccess);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
        catch (Exception ex) when ((command.CodCollection is not null || command.CollectionPayment is not null) && !TransferConfirmation.IsBenign(ex))
        {
            var entered = command.CodCollection is { } cod
                ? new EnteredPaymentDetails("CashOnDelivery", cod.Amount, null, cod.CollectedOn)
                : new EnteredPaymentDetails(command.CollectionPayment!.Method.DisplayName, command.CollectionPayment.Amount, command.CollectionPayment.Reference, command.DeliveredOn);

            await reconciliation.RecordAsync(new ReconciliationIssueDto(
                Guid.NewGuid(), order.Id, ReconciliationIssueKind.DeliveryPaymentFailed, entered, order.CurrencyCode,
                $"{(ex is System.Reflection.TargetInvocationException { InnerException: { } inner } ? inner : ex).GetType().Name}: the payment could not be recorded and was rolled back.",
                command.CorrelationId, command.UserContext!.UserId, timeProvider.GetUtcNow(), command.UserContext, order.BranchId));
            throw;
        }
    }

    private async Task<CommandResponse> DeliverAsync(RecordDeliveryCommand command, OrderAggregate order)
    {
        var now = timeProvider.GetUtcNow();
        var user = command.UserContext!;
        var branch = await branches.GetByIdAsync(order.BranchId) ?? throw new BillingSeamException("The order's branch was not found.");

        CodCollection? cod = null;
        if (command.CodCollection is { } input)
        {
            var name = string.IsNullOrWhiteSpace(input.CollectorName) ? (input.CollectorKind == CollectorKind.Staff ? "Shop staff" : string.Empty) : input.CollectorName.Trim();
            OrderFreeText.RefuseCardNumber(name, nameof(input.CollectorName));
            cod = new CodCollection(input.Amount, input.CollectorKind, name, input.CollectorStaffId, input.CollectedOn, user.UserId);
        }

        var payment = command.CollectionPayment is { } p ? new CollectionPaymentDto(p.Method, p.Amount, p.Reference?.Trim()) : null;
        OrderFreeText.RefuseCardNumber(payment?.Reference, "Reference"); // C-5: a slip reference, never a card number

        order.RecordDelivery(command.DeliveredOn, cod, payment, now, user, command.CorrelationId);

        if (order.FulfilmentMethod == FulfilmentMethod.Pickup && await OrderStockFulfilment.ConsumeAsync(stock, order) is { } refused) return refused;

        if (cod is not null || payment is not null)
        {
            var invoiceId = order.InvoiceId ?? throw new BillingSeamException("The order has no invoice to record the payment against.");
            var amount = cod?.Amount ?? payment!.Amount;
            var receivedOn = OrderAccess.LocalDate(cod?.CollectedOn ?? now, branch.TimeZoneId);

            (string method, string? reference, PaymentCollectorInput? collector) = cod is not null
                ? ("CashOnDelivery", order.Shipment?.TrackingNumber ?? order.OrderNumber,
                    new PaymentCollectorInput(
                        cod.CollectorKind.DisplayName, cod.CollectorStaffId, $"C-{(order.Shipment?.CourierId ?? Guid.Empty).ToString("N")[..8]}", cod.CollectorName))
                : (payment!.Method == TenderMethod.InStoreCard ? "CardInStore" : "CashInStore", payment.Reference, (PaymentCollectorInput?)null);

            var paymentId = await OrderBillingMoney.RecordPaymentAsync(dispatcher, order, invoiceId, method, amount, receivedOn, reference, collector, user, command.CorrelationId, approvalWaiverReason: cod is not null ? ApprovalWaiverReasons.CodCollection : ApprovalWaiverReasons.CounterTender);
            order.RecordPayment(paymentId, method, amount, now);
        }

        OrderNotifications.EnqueueDelivered(outbox, links, order, command.CorrelationId, user);
        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// C19 (US-ORD-20 AC3) -- a parcel refused or undeliverable, or a pickup not collected. The order becomes <c>Returned</c> (<c>Uncollected</c> only for pickup,
/// <c>Refused</c> / <c>Undeliverable</c> only for delivery). One transaction: a pickup's reservation is released (nothing left the shop); the invoice is
/// credited in full; money already received is marked owed back; the customer is told. For a refused or undeliverable <b>delivery</b> a <c>DeliveryFailure</c> return
/// (<c>Approved</c>, every line) is <b>staged</b> as a new root in the same save (<see cref="IOrderingStaging"/>, the one new-root staging of the slice): the goods come back
/// through the returns path into quarantine, where staff restock or write them off.
/// </summary>
public sealed class RecordDeliveryFailureCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<DiscountCodeAggregate> discountCodes,
    IOrderingStaging staging,
    IStockLedger stock,
    ITransactionalScope transaction,
    IInMemoryDispatcher dispatcher,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<RecordDeliveryFailureCommand>
{
    public async Task<CommandResponse> HandleAsync(RecordDeliveryFailureCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();

        try
        {
            return await transaction.RunAsync(() => FailAsync(command, order), response => response.IsSuccess);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
    }

    private async Task<CommandResponse> FailAsync(RecordDeliveryFailureCommand command, OrderAggregate order)
    {
        order.RecordDeliveryFailure(command.Reason, timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId);

        // A pickup that was never collected still holds its reservation: give it back. (A delivery's stock was sold at dispatch and returns through the returns path.)
        if (command.Reason == DeliveryFailureReason.Uncollected) await OrderRelease.ReleaseAsync(stock, discountCodes, order);
        else staging.Add(ReturnRequestAggregate.ForDeliveryFailure(command.ReturnRequestId, order, command.UserContext!.UserId, timeProvider.GetUtcNow(), command.UserContext, command.CorrelationId));

        if (order.InvoiceId is { } invoiceId)
        {
            var creditNoteId = await OrderBillingMoney.IssueFullCreditNoteAsync(dispatcher, order, invoiceId, "Return", "Delivery failed: " + command.Reason.DisplayName, command);
            order.AddCreditNote(creditNoteId);
        }

        OrderNotifications.EnqueueDeliveryFailed(outbox, links, order, command.CorrelationId, command.UserContext!);
        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// C20 (US-ORD-18 AC2, COM-3, ADR-209) -- a verified courier tracking event. In one transaction the inbox row is recorded first -- a duplicate <c>(provider, event
/// id)</c> is success with nothing else done -- then the order dispatched with that tracking number takes the event (an unknown number is success, logged by
/// the controller at <c>Information</c>; only a newer event updates). <b>A "delivered" report does not deliver the order</b>: COD money must be recorded by a
/// person (US-ORD-20 AC2); it sets the last tracking status and the staff queue highlights it.
/// </summary>
public sealed class ApplyCourierTrackingEventCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IOrderFinder finder,
    IWebhookInbox inbox,
    ITransactionalScope transaction,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ApplyCourierTrackingEventCommand>
{
    public async Task<CommandResponse> HandleAsync(ApplyCourierTrackingEventCommand command) =>
        await transaction.RunAsync(
            async () =>
            {
                if (!await inbox.TryRecordAsync(command.Provider, command.EventId, timeProvider.GetUtcNow(), default)) return Success(false);

                var orderId = await finder.FindOrderIdByTrackingNumberAsync(command.TrackingNumber, default);
                var order = orderId is { } id ? await orders.GetByIdAsync(id) : null;
                if (order is null || !order.ApplyTrackingEvent(command.Status, command.OccurredOn)) return Success(order is not null);

                await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
                return Success(true);
            },
            response => response.IsSuccess);
}

/// <summary>
/// C21 (US-IMP-07 AC2 order half, BR-14) -- staff move a line to a chosen batch while the order is <c>Confirmed</c> or <c>Packing</c>. A reason is mandatory
/// (<c>ReallocationReasonRequired</c>). One transaction: Inventory releases the line's reservation and reserves the explicit batch (refusing an unusable or
/// short one, auditing the override with the reason), the new allocations are recorded and <c>OrderLineReallocatedEvent</c> names the batches. This is how
/// a recalled batch is replaced so the order can ship.
/// </summary>
public sealed class ReallocateOrderLineCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IStockLedger stock,
    ITransactionalScope transaction)
    : ICommandHandlerAsync<ReallocateOrderLineCommand>
{
    public async Task<CommandResponse> HandleAsync(ReallocateOrderLineCommand command)
    {
        var order = await OrderAccess.ForBranchAsync(orders, command.OrderId, command.BranchId);
        if (order is null) return OrderAccess.NotFound();

        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Trim().Length < 5)
            return Violation(BusinessViolationCodes.ReallocationReasonRequired, nameof(command.Reason), "Give a reason of at least five characters for choosing this batch.");

        var line = order.Lines.FirstOrDefault(l => l.Id == command.LineId);
        if (line is null) return Violation(BusinessViolationCodes.OrderNotFound, nameof(command.LineId), "That order line was not found.");

        try
        {
            return await transaction.RunAsync(
                async () =>
                {
                    var (allocations, refusal) = await OrderStockFulfilment.ReallocateAsync(stock, transaction, order, line, command.TargetBatchId, command.Reason.Trim());
                    if (refusal is not null) return refusal;

                    order.ReallocateLine(line.Id, allocations!, command.UserContext!, command.CorrelationId);
                    await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
                    return Success();
                },
                response => response.IsSuccess);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
    }
}
