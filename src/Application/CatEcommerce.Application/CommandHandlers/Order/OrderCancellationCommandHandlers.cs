using CatEcommerce.Application.Commands.Order;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001 §8.9, §11.4 C13/C14 -- what a cancellation and an expiry both do besides the status change: give the reserved batches back to Inventory
/// (<c>ReleaseAsync</c> for the whole order, idempotent) and the discount use back to the <b>tracked</b> code (once, ADR-207). Staged and tracked -- the
/// caller's save commits them with the order.
/// </summary>
internal static class OrderRelease
{
    public static async Task ReleaseAsync(
        IStockLedger ledger, IAggregateRootRepository<DiscountCodeAggregate> discountCodes, OrderAggregate order)
    {
        var released = await ledger.ReleaseAsync(OrderStock.ReferenceFor(order), default);
        if (!released.IsSuccess)
            throw new InvalidOperationException("Inventory refused to release the order's reservation: " + released.ViolationCode);

        if (order.ReleaseDiscount() && order.DiscountCodeId is { } codeId)
        {
            var code = await discountCodes.GetByIdAsync(codeId);
            code?.ReleaseRedemption();
        }
    }
}

/// <summary>
/// C13 (BR-16, FR-ORD-22, US-ORD-19 AC3) -- the worker expires one unpaid bank-transfer order past its window: <c>Cancelled</c> with
/// <c>PaymentWindowExpired</c>, its reservation released, its discount use given back, the customer told (with the re-order link). An order that is no longer
/// expirable (a confirmation raced it, or money was claimed and is held) is <b>success with nothing written</b> -- the worker never fights a confirmation.
/// </summary>
public sealed class ExpireUnpaidOrderCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<DiscountCodeAggregate> discountCodes,
    IStockLedger stock,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<ExpireUnpaidOrderCommand>
{
    public async Task<CommandResponse> HandleAsync(ExpireUnpaidOrderCommand command)
    {
        var order = await orders.GetByIdAsync(command.OrderId);
        if (order is null) return Success();

        try
        {
            order.Expire(timeProvider.GetUtcNow(), command.UserContext!, command.CorrelationId);
        }
        catch (DomainRuleViolationException)
        {
            return Success(); // not (or no longer) expirable: nothing to do
        }

        await OrderRelease.ReleaseAsync(stock, discountCodes, order);
        OrderNotifications.EnqueueExpired(outbox, links, order, command.CorrelationId, command.UserContext!);

        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}

/// <summary>
/// C14 (US-ORD-12) -- cancel an order. A customer cancels their own order (<c>404</c> otherwise) while it is <c>PendingPayment</c> or <c>Confirmed</c>; staff cancel
/// an order of their active branch, also while <c>Packing</c>, with a reason. After packing began the customer gets <c>CancellationNotAvailable</c> ("contact
/// the shop"). In one database transaction (ADR-215): the order is cancelled, its reservation released, its discount use returned and, when an invoice exists,
/// Billing credits it in full (the credit note is linked to the order); a paid order stays <c>Cancelled</c> with <c>RefundDue</c> and the customer is told
/// money is owed back. Staff refund through <c>IssueRefundCommand</c> (phase G).
/// </summary>
public sealed class CancelOrderCommandHandler(
    IAggregateRootRepository<OrderAggregate> orders,
    IAggregateRootRepository<DiscountCodeAggregate> discountCodes,
    IStockLedger stock,
    ITransactionalScope transaction,
    IInMemoryDispatcher dispatcher,
    INotificationOutbox outbox,
    INotificationLinkBuilder links,
    TimeProvider timeProvider)
    : ICommandHandlerAsync<CancelOrderCommand>
{
    public async Task<CommandResponse> HandleAsync(CancelOrderCommand command)
    {
        var customerId = CartCaller.CustomerId(command);
        var byCustomer = customerId is not null;

        OrderAggregate? order;
        if (customerId is { } customer) order = await OrderAccess.ForCustomerAsync(orders, command.OrderId, customer);
        else if (command.BranchId is { } branchId) order = await OrderAccess.ForBranchAsync(orders, command.OrderId, branchId);
        else order = null;

        if (order is null) return OrderAccess.NotFound();

        try
        {
            return await transaction.RunAsync(() => CancelAsync(command, order, byCustomer), response => response.IsSuccess);
        }
        catch (DomainRuleViolationException ex)
        {
            return Violation(ex.ErrorCode, ex.PropertyName, ex.Message);
        }
    }

    private async Task<CommandResponse> CancelAsync(CancelOrderCommand command, OrderAggregate order, bool byCustomer)
    {
        var now = timeProvider.GetUtcNow();
        var wasPaid = order.AmountPaid > 0m;
        order.Cancel(command.UserContext!, byCustomer, byCustomer ? OrderCancellationReason.CustomerRequest : OrderCancellationReason.StaffDecision, command.Note, now, command.CorrelationId);

        await OrderRelease.ReleaseAsync(stock, discountCodes, order);

        if (order.InvoiceId is { } invoiceId)
        {
            var creditNoteId = await OrderBillingMoney.IssueFullCreditNoteAsync(
                dispatcher, order, invoiceId, "Cancellation", byCustomer ? "Cancelled by the customer" : "Cancelled by the shop", command);
            order.AddCreditNote(creditNoteId);
        }

        if (wasPaid) OrderNotifications.EnqueueCancelledRefundDue(outbox, links, order, command.CorrelationId, command.UserContext!);
        else OrderNotifications.EnqueueCancelled(outbox, links, order, byCustomer, command.CorrelationId, command.UserContext!);

        await orders.UpdateAsync(order, EventPublishOption.InMemoryPublishOnly);
        return Success();
    }
}
