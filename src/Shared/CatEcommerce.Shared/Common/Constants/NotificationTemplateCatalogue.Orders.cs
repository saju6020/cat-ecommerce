using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ORD-001 §8.12 (ADR-213) -- the twelve order templates appended to HO-NTF-001's catalogue. All <b>Transactional</b> (order and payment
/// messages are exempt from marketing opt-out, FR-NTF-4) with SMS, email and in-app. The <b>Urgent</b> set is exactly confirmation,
/// awaiting-transfer, cancelled, cancelled-refund-due and refund-issued -- the customer who just acted should not wait for quiet hours.
/// Enqueued by command handlers only (HO-NTF-001 ADR-057). No template carries a tracking URL: Url variables come from the link builder,
/// and the order page shows the courier link. Money variables are pre-formatted text; instants are formatted in the branch zone.
/// </summary>
public static partial class NotificationTemplateCatalogue
{
    public const string OrderConfirmed = "order.confirmed";
    public const string OrderAwaitingTransfer = "order.awaiting-transfer";
    public const string OrderExpired = "order.expired";
    public const string OrderCancelled = "order.cancelled";
    public const string OrderCancelledRefundDue = "order.cancelled-refund-due";
    public const string OrderDispatched = "order.dispatched";
    public const string OrderTrackingAdded = "order.tracking-added";
    public const string OrderReadyForCollection = "order.ready-for-collection";
    public const string OrderDelivered = "order.delivered";
    public const string OrderDeliveryFailed = "order.delivery-failed";
    public const string OrderReturnDecided = "order.return-decided";
    public const string OrderRefundIssued = "order.refund-issued";

    /// <summary>The five Urgent order templates (§8.12) -- pinned by U-A7.</summary>
    public static readonly IReadOnlyList<string> UrgentOrderTemplateKeys =
        [OrderConfirmed, OrderAwaitingTransfer, OrderCancelled, OrderCancelledRefundDue, OrderRefundIssued];

    // A property, not a static field: Definitions (main partial file) is built during static initialisation and calls OrderDefinitions() --
    // a static field of this file could still be null then, because static-initialiser order across partial files is unspecified.
    private static NotificationChannel[] AllChannels => [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp];

    private static NotificationTemplateVariableDefinition OrderText(string name, string description, string sample, NotificationChannel[]? requiredOn = null) =>
        new(name, description, TemplateVariableKind.Text, sample, requiredOn ?? AllChannels);

    private static NotificationTemplateVariableDefinition OrderLinkVariable() =>
        new("orderLink", "A link to the order page.", TemplateVariableKind.Url, "https://portal.example.com/portal/orders/sample", AllChannels);

    private static NotificationTemplateDefinition OrderTemplate(
        string key, string displayName, string description, NotificationUrgency urgency,
        IReadOnlyList<NotificationTemplateVariableDefinition> variables,
        string sms, string emailSubject, string emailBody, string inAppTitle, string inAppBody) =>
        new(key, displayName, description, NotificationCategory.Transactional, urgency, AllChannels, ContainsSecret: false, variables,
        [
            new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null, sms, null),
            new NotificationTemplateChannelDefault(NotificationChannel.Email, emailSubject, null, emailBody, null),
            new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, inAppTitle, inAppBody, "{{orderLink}}"),
        ]);

    private static IReadOnlyList<NotificationTemplateDefinition> OrderDefinitions() =>
    [
        OrderTemplate(
            OrderConfirmed, "Order confirmed", "Sent when an order is placed (cash on delivery, pay on collection) or its payment is confirmed (HO-ORD-001 §8.12).",
            NotificationUrgency.Urgent,
            [
                OrderText("orderNumber", "The order number.", "ORD-2026-000123"),
                OrderText("orderTotal", "The order total, formatted with its currency.", "BDT 1,927.50"),
                OrderText("paymentSummary", "How the order is or was paid.", "Cash on delivery"),
                OrderText("fulfilmentSummary", "How the goods reach the customer.", "Delivery to Dhaka"),
                OrderLinkVariable(),
            ],
            "Order {{orderNumber}} confirmed: {{orderTotal}}. {{paymentSummary}}. {{fulfilmentSummary}}. Track it: {{orderLink}}",
            "Your order {{orderNumber}} is confirmed",
            "Hello {{recipientName}},\n\nThank you. Your order {{orderNumber}} is confirmed.\n\nTotal: {{orderTotal}}\nPayment: {{paymentSummary}}\n{{fulfilmentSummary}}\n\n[View your order]({{orderLink}}).",
            "Order confirmed",
            "Order {{orderNumber}} is confirmed. Total {{orderTotal}}. {{paymentSummary}}. {{fulfilmentSummary}}."),

        OrderTemplate(
            OrderAwaitingTransfer, "Order awaiting bank transfer", "Sent when a bank-transfer order is placed and is waiting for payment (HO-ORD-001 §8.12).",
            NotificationUrgency.Urgent,
            [
                OrderText("orderNumber", "The order number.", "ORD-2026-000123"),
                OrderText("amountDue", "The amount to transfer, formatted with its currency.", "BDT 1,927.50"),
                OrderText("bankAccountName", "The shop's bank account name.", "Purr Care Ltd"),
                OrderText("bankAccountNumber", "The shop's bank account number.", "1234567890"),
                OrderText("bankName", "The shop's bank.", "Sample Bank"),
                OrderText("bankBranch", "The shop's bank branch.", "Gulshan", [NotificationChannel.Email, NotificationChannel.InApp]),
                OrderText("paymentReference", "The reference to quote on the transfer.", "ORD-2026-000123"),
                OrderText("payByLocal", "When payment is due, in the branch's time zone.", "2026-10-12 18:00"),
                OrderLinkVariable(),
            ],
            "Order {{orderNumber}}: please transfer {{amountDue}} to {{bankAccountName}}, a/c {{bankAccountNumber}}, {{bankName}}. Quote {{paymentReference}} by {{payByLocal}}. {{orderLink}}",
            "Order {{orderNumber}}: please complete your bank transfer",
            "Hello {{recipientName}},\n\nYour order {{orderNumber}} is reserved for you until {{payByLocal}}. Please transfer {{amountDue}}:\n\nAccount name: {{bankAccountName}}\nAccount number: {{bankAccountNumber}}\nBank: {{bankName}}, {{bankBranch}}\nReference: {{paymentReference}}\n\n[View your order]({{orderLink}}).",
            "Bank transfer needed",
            "Order {{orderNumber}}: transfer {{amountDue}} to {{bankAccountName}}, {{bankAccountNumber}}, {{bankName}}, {{bankBranch}}. Reference {{paymentReference}}. Pay by {{payByLocal}}."),

        OrderTemplate(
            OrderExpired, "Order expired", "Sent when an unpaid bank-transfer order passes its payment window and is cancelled (HO-ORD-001 §8.12).",
            NotificationUrgency.Normal,
            [OrderText("orderNumber", "The order number.", "ORD-2026-000123"), OrderLinkVariable()],
            "Order {{orderNumber}} was cancelled because we did not receive the payment in time. You can order again: {{orderLink}}",
            "Order {{orderNumber}} has expired",
            "Hello {{recipientName}},\n\nWe did not receive the bank transfer for order {{orderNumber}} in time, so it has been cancelled and the items released. [Order again]({{orderLink}}).",
            "Order expired",
            "Order {{orderNumber}} was cancelled because the payment did not arrive in time. You can order again."),

        OrderTemplate(
            OrderCancelled, "Order cancelled", "Sent when an order is cancelled and no money had been received (HO-ORD-001 §8.12).",
            NotificationUrgency.Urgent,
            [OrderText("orderNumber", "The order number.", "ORD-2026-000123"), OrderText("cancelledByShop", "Who cancelled it.", "Cancelled at your request"), OrderLinkVariable()],
            "Order {{orderNumber}} has been cancelled. {{cancelledByShop}}. Details: {{orderLink}}",
            "Order {{orderNumber}} has been cancelled",
            "Hello {{recipientName}},\n\nOrder {{orderNumber}} has been cancelled. {{cancelledByShop}}.\n\n[View your order]({{orderLink}}).",
            "Order cancelled",
            "Order {{orderNumber}} has been cancelled. {{cancelledByShop}}."),

        OrderTemplate(
            OrderCancelledRefundDue, "Order cancelled - refund due", "Sent when an order that was already paid is cancelled (HO-ORD-001 §8.12).",
            NotificationUrgency.Urgent,
            [OrderText("orderNumber", "The order number.", "ORD-2026-000123"), OrderText("refundAmount", "The amount to be refunded, formatted with its currency.", "BDT 1,927.50"), OrderLinkVariable()],
            "Order {{orderNumber}} has been cancelled. We will refund {{refundAmount}}. Details: {{orderLink}}",
            "Order {{orderNumber}} cancelled - refund on its way",
            "Hello {{recipientName}},\n\nOrder {{orderNumber}} has been cancelled. We owe you a refund of {{refundAmount}} and will contact you to arrange it. [View your order]({{orderLink}}).",
            "Order cancelled - refund due",
            "Order {{orderNumber}} has been cancelled. We will refund {{refundAmount}}."),

        OrderTemplate(
            OrderDispatched, "Order dispatched", "Sent when an order leaves the shop with a courier (HO-ORD-001 §8.12).",
            NotificationUrgency.Normal,
            [
                OrderText("orderNumber", "The order number.", "ORD-2026-000123"),
                OrderText("courierName", "The courier.", "Sample Courier"),
                OrderText("trackingNumber", "The tracking number, or \"to follow\".", "TRK123456"),
                OrderLinkVariable(),
            ],
            "Order {{orderNumber}} is on its way with {{courierName}}. Tracking: {{trackingNumber}}. {{orderLink}}",
            "Order {{orderNumber}} is on its way",
            "Hello {{recipientName}},\n\nOrder {{orderNumber}} has been dispatched with {{courierName}}. Tracking number: {{trackingNumber}}. [View your order]({{orderLink}}).",
            "Order dispatched",
            "Order {{orderNumber}} is on its way with {{courierName}}. Tracking number: {{trackingNumber}}."),

        OrderTemplate(
            OrderTrackingAdded, "Tracking number added", "Sent when the first tracking number is added to a dispatched order (HO-ORD-001 §8.12).",
            NotificationUrgency.Normal,
            [
                OrderText("orderNumber", "The order number.", "ORD-2026-000123"),
                OrderText("courierName", "The courier.", "Sample Courier"),
                OrderText("trackingNumber", "The tracking number.", "TRK123456"),
                OrderLinkVariable(),
            ],
            "Order {{orderNumber}}: {{courierName}} tracking number {{trackingNumber}}. {{orderLink}}",
            "Tracking number for order {{orderNumber}}",
            "Hello {{recipientName}},\n\nThe tracking number for order {{orderNumber}} is {{trackingNumber}} ({{courierName}}). [View your order]({{orderLink}}).",
            "Tracking number added",
            "Order {{orderNumber}}: {{courierName}} tracking number {{trackingNumber}}."),

        OrderTemplate(
            OrderReadyForCollection, "Order ready for collection", "Sent when a pickup order is packed and waiting at the branch (HO-ORD-001 §8.12).",
            NotificationUrgency.Normal,
            [
                OrderText("orderNumber", "The order number.", "ORD-2026-000123"),
                OrderText("branchName", "The branch to collect from.", "Gulshan"),
                OrderText("branchAddress", "The branch address.", "House 1, Road 2, Gulshan, Dhaka"),
                OrderText("collectByLocal", "The last day to collect, in the branch's time zone.", "2026-10-19"),
                OrderText("amountDue", "The amount to pay on collection, formatted with its currency, or nothing owed.", "BDT 1,927.50"),
                OrderLinkVariable(),
            ],
            "Order {{orderNumber}} is ready at {{branchName}} ({{branchAddress}}). Collect by {{collectByLocal}}. To pay: {{amountDue}}. {{orderLink}}",
            "Order {{orderNumber}} is ready for collection",
            "Hello {{recipientName}},\n\nOrder {{orderNumber}} is ready at {{branchName}}, {{branchAddress}}. Please collect it by {{collectByLocal}}. Amount to pay on collection: {{amountDue}}. [View your order]({{orderLink}}).",
            "Ready for collection",
            "Order {{orderNumber}} is ready at {{branchName}}, {{branchAddress}}. Collect by {{collectByLocal}}. To pay: {{amountDue}}."),

        OrderTemplate(
            OrderDelivered, "Order delivered", "Sent when an order is recorded as delivered or collected (HO-ORD-001 §8.12).",
            NotificationUrgency.Normal,
            [OrderText("orderNumber", "The order number.", "ORD-2026-000123"), OrderLinkVariable()],
            "Order {{orderNumber}} has been delivered. Thank you! {{orderLink}}",
            "Order {{orderNumber}} has been delivered",
            "Hello {{recipientName}},\n\nOrder {{orderNumber}} has been delivered. Thank you for shopping with {{clinicName}}. [View your order]({{orderLink}}).",
            "Order delivered",
            "Order {{orderNumber}} has been delivered. Thank you!"),

        OrderTemplate(
            OrderDeliveryFailed, "Delivery unsuccessful", "Sent when a parcel could not be delivered or a pickup was not collected (HO-ORD-001 §8.12).",
            NotificationUrgency.Normal,
            [OrderText("orderNumber", "The order number.", "ORD-2026-000123"), OrderLinkVariable()],
            "We could not complete order {{orderNumber}}. Please call {{clinicPhone}}. {{orderLink}}",
            "Order {{orderNumber}} could not be completed",
            "Hello {{recipientName}},\n\nWe could not complete order {{orderNumber}}. Please call {{clinicName}} on {{clinicPhone}}. [View your order]({{orderLink}}).",
            "Delivery unsuccessful",
            "We could not complete order {{orderNumber}}. Please call {{clinicPhone}}."),

        OrderTemplate(
            OrderReturnDecided, "Return decided", "Sent when staff approve or reject a return request (HO-ORD-001 §8.12).",
            NotificationUrgency.Normal,
            [OrderText("orderNumber", "The order number.", "ORD-2026-000123"), OrderText("returnOutcome", "What was decided.", "Your return was approved"), OrderLinkVariable()],
            "Order {{orderNumber}}: {{returnOutcome}}. Details: {{orderLink}}",
            "Your return for order {{orderNumber}}",
            "Hello {{recipientName}},\n\nOrder {{orderNumber}}: {{returnOutcome}}. [View your order]({{orderLink}}).",
            "Return decided",
            "Order {{orderNumber}}: {{returnOutcome}}."),

        OrderTemplate(
            OrderRefundIssued, "Order refund issued", "Sent when a refund is recorded against an order (HO-ORD-001 §8.12). The reason is never sent.",
            NotificationUrgency.Urgent,
            [
                OrderText("orderNumber", "The order number.", "ORD-2026-000123"),
                OrderText("refundAmount", "The amount refunded, formatted with its currency.", "BDT 500.00"),
                OrderText("refundMethod", "How it was refunded.", "Bank transfer"),
                OrderLinkVariable(),
            ],
            "Order {{orderNumber}}: we refunded {{refundAmount}} ({{refundMethod}}). {{orderLink}}",
            "A refund for order {{orderNumber}}",
            "Hello {{recipientName}},\n\nWe refunded {{refundAmount}} ({{refundMethod}}) for order {{orderNumber}}. [View your order]({{orderLink}}).",
            "Refund issued",
            "Order {{orderNumber}}: we refunded {{refundAmount}} ({{refundMethod}})."),
    ];
}
