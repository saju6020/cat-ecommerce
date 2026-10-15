using System.Globalization;
using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001 §8.12 (ADR-213) -- builds and enqueues the order messages. Called from a <b>command handler, before its save</b>, through
/// <see cref="INotificationOutbox"/> (HO-NTF-001 ADR-056/057) -- never from a domain event handler. The customer is the recipient; an anonymous counter
/// sale sends nothing. Money variables are text formatted by the producer (<c>BDT 1,250.00</c>), instants are formatted in the branch's time zone, and no
/// variable carries an external URL (the link builder supplies <c>orderLink</c>).
/// </summary>
public static class OrderNotifications
{
    public static string PaymentSummary(OrderPaymentMethod method, bool paid) =>
        method == OrderPaymentMethod.CashOnDelivery ? "Cash on delivery"
        : method == OrderPaymentMethod.PayOnCollection ? "Pay at collection"
        : method == OrderPaymentMethod.BankTransfer ? (paid ? "Paid by bank transfer" : "Bank transfer")
        : "Paid in store";

    public static string FulfilmentSummary(OrderAggregate order) =>
        order.FulfilmentMethod == FulfilmentMethod.Pickup ? "Collect from the shop"
        : order.FulfilmentMethod == FulfilmentMethod.Delivery
            ? string.IsNullOrWhiteSpace(order.Fulfilment.District) ? "Delivery" : $"Delivery to {order.Fulfilment.District}"
        : "Sold at the counter";

    public static string Money(decimal amount, string currencyCode) => BillingMessages.FormatMoney(amount, currencyCode);

    /// <summary>An instant as <c>yyyy-MM-dd HH:mm</c> in the branch's zone.</summary>
    public static string LocalTime(DateTimeOffset instant, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTime(instant, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    public static void EnqueueConfirmed(
        INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, bool paid, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderConfirmed, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string>
            {
                ["orderNumber"] = order.OrderNumber,
                ["orderTotal"] = Money(order.Totals.GrandTotal, order.CurrencyCode),
                ["paymentSummary"] = PaymentSummary(order.PaymentMethod, paid),
                ["fulfilmentSummary"] = FulfilmentSummary(order),
                ["orderLink"] = links.OrderLink(order.Id),
            },
            BranchId: order.BranchId, DeduplicationKey: $"order.confirmed:{order.Id:N}", CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueAwaitingTransfer(
        INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, BankTransferAccountAggregate account, string branchTimeZoneId,
        Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderAwaitingTransfer, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string>
            {
                ["orderNumber"] = order.OrderNumber,
                ["amountDue"] = Money(order.Totals.GrandTotal, order.CurrencyCode),
                ["bankAccountName"] = account.AccountName,
                ["bankAccountNumber"] = account.AccountNumber,
                ["bankName"] = account.BankName,
                ["bankBranch"] = account.BranchName,
                ["paymentReference"] = order.OrderNumber,
                ["payByLocal"] = LocalTime(order.PaymentDueBy!.Value, branchTimeZoneId),
                ["orderLink"] = links.OrderLink(order.Id),
            },
            BranchId: order.BranchId, DeduplicationKey: $"order.awaiting-transfer:{order.Id:N}", CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueExpired(INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderExpired, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["orderNumber"] = order.OrderNumber, ["orderLink"] = links.OrderLink(order.Id) },
            BranchId: order.BranchId, DeduplicationKey: $"order.expired:{order.Id:N}", CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueCancelled(INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, bool byCustomer, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderCancelled, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string>
            {
                ["orderNumber"] = order.OrderNumber,
                ["cancelledByShop"] = byCustomer ? "Cancelled at your request" : "Cancelled by the shop",
                ["orderLink"] = links.OrderLink(order.Id),
            },
            BranchId: order.BranchId, DeduplicationKey: $"order.cancelled:{order.Id:N}", CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueCancelledRefundDue(INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderCancelledRefundDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string>
            {
                ["orderNumber"] = order.OrderNumber,
                ["refundAmount"] = Money(order.RefundDue, order.CurrencyCode),
                ["orderLink"] = links.OrderLink(order.Id),
            },
            BranchId: order.BranchId, DeduplicationKey: $"order.cancelled-refund-due:{order.Id:N}", CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueDispatched(INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderDispatched, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string>
            {
                ["orderNumber"] = order.OrderNumber,
                ["courierName"] = order.Shipment!.CourierName,
                ["trackingNumber"] = string.IsNullOrWhiteSpace(order.Shipment.TrackingNumber) ? "to follow" : order.Shipment.TrackingNumber,
                ["orderLink"] = links.OrderLink(order.Id),
            },
            BranchId: order.BranchId, DeduplicationKey: $"order.dispatched:{order.Id:N}", CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueTrackingAdded(INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderTrackingAdded, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string>
            {
                ["orderNumber"] = order.OrderNumber,
                ["courierName"] = order.Shipment!.CourierName,
                ["trackingNumber"] = order.Shipment.TrackingNumber ?? string.Empty,
                ["orderLink"] = links.OrderLink(order.Id),
            },
            BranchId: order.BranchId, DeduplicationKey: $"order.tracking-added:{order.Id:N}", CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueReadyForCollection(
        INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, string branchName, string? branchAddress, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderReadyForCollection, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string>
            {
                ["orderNumber"] = order.OrderNumber,
                ["branchName"] = branchName,
                ["branchAddress"] = string.IsNullOrWhiteSpace(branchAddress) ? branchName : branchAddress,
                ["collectByLocal"] = order.CollectByDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                ["amountDue"] = order.BalanceDue > 0m ? Money(order.BalanceDue, order.CurrencyCode) : "Nothing to pay",
                ["orderLink"] = links.OrderLink(order.Id),
            },
            BranchId: order.BranchId, DeduplicationKey: $"order.ready-for-collection:{order.Id:N}", CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueDelivered(INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderDelivered, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["orderNumber"] = order.OrderNumber, ["orderLink"] = links.OrderLink(order.Id) },
            BranchId: order.BranchId, DeduplicationKey: $"order.delivered:{order.Id:N}", CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueDeliveryFailed(INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderDeliveryFailed, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["orderNumber"] = order.OrderNumber, ["orderLink"] = links.OrderLink(order.Id) },
            BranchId: order.BranchId, DeduplicationKey: $"order.delivery-failed:{order.Id:N}", CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueReturnDecided(INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, string outcomeText, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderReturnDecided, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["orderNumber"] = order.OrderNumber, ["returnOutcome"] = outcomeText, ["orderLink"] = links.OrderLink(order.Id) },
            BranchId: order.BranchId, CorrelationId: correlationId, UserContext: user));
    }

    public static void EnqueueRefundIssued(
        INotificationOutbox outbox, INotificationLinkBuilder links, OrderAggregate order, decimal amount, string methodName, Guid correlationId, UserContext user)
    {
        if (order.CustomerId is not { } customerId) return;

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.OrderRefundIssued, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string>
            {
                ["orderNumber"] = order.OrderNumber,
                ["refundAmount"] = Money(amount, order.CurrencyCode),
                ["refundMethod"] = methodName,
                ["orderLink"] = links.OrderLink(order.Id),
            },
            BranchId: order.BranchId, CorrelationId: correlationId, UserContext: user));
    }
}
