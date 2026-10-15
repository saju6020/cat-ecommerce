using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.CommandHandlers.Billing;

/// <summary>
/// HO-BIL-001 §8.12 (ADR-193) -- builds and enqueues the three Billing messages. Called from a <b>command handler,
/// before its save</b>, through <see cref="INotificationOutbox"/> (HO-NTF-001 ADR-056/057) -- never from a domain
/// event handler, and never for a staff-internal state (a draft, a pending payment, a rejection, a reversal).
/// Amounts are formatted by the producer in the currency (<c>BDT 1,500.00</c>); the refund's <i>reason</i> is never sent.
/// </summary>
public static class BillingNotifications
{
    public static void EnqueueInvoiceIssued(
        INotificationOutbox outbox, INotificationLinkBuilder links, InvoiceAggregate invoice, Guid correlationId, UserContext userContext)
    {
        var variables = new Dictionary<string, string>
        {
            ["invoiceNumber"] = invoice.Number ?? string.Empty,
            ["totalText"] = BillingMessages.FormatMoney(invoice.Total, invoice.CurrencyCode),
            ["dueOn"] = (invoice.DueOn ?? invoice.IssueDate ?? DateOnly.MinValue).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            ["invoiceLink"] = links.InvoiceLink(invoice.Id),
        };

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BillingInvoiceIssued, NotificationRecipientKind.Customer, invoice.CustomerId,
            variables, BranchId: invoice.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    /// <param name="purposeText">The earmark label, or the invoice numbers, or <c>credit on your account</c>.</param>
    public static void EnqueuePaymentReceived(
        INotificationOutbox outbox, INotificationLinkBuilder links, PaymentAggregate payment, string purposeText, Guid correlationId, UserContext userContext)
    {
        var variables = new Dictionary<string, string>
        {
            ["amountText"] = BillingMessages.FormatMoney(payment.Amount, payment.CurrencyCode),
            ["methodName"] = payment.Method.DisplayName,
            ["receiptNumber"] = ReceiptNumberText(payment.ReceiptNumber),
            ["purposeText"] = purposeText,
            ["receiptLink"] = links.ReceiptLink(payment.Id),
        };

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BillingPaymentReceived, NotificationRecipientKind.Customer, payment.CustomerId,
            variables, BranchId: payment.BranchId, CorrelationId: correlationId, UserContext: userContext));
    }

    public static void EnqueueRefundIssued(
        INotificationOutbox outbox, Guid customerId, Guid branchId, decimal amount, string currencyCode, RefundMethod method, string? creditNoteNumber,
        Guid correlationId, UserContext userContext)
    {
        var variables = new Dictionary<string, string>
        {
            ["amountText"] = BillingMessages.FormatMoney(amount, currencyCode),
            ["methodName"] = method.DisplayName,
            ["creditNoteNumber"] = string.IsNullOrWhiteSpace(creditNoteNumber) ? "—" : creditNoteNumber,
        };

        outbox.Enqueue(new NotificationRequestDto(
            Guid.NewGuid(), NotificationTemplateCatalogue.BillingRefundIssued, NotificationRecipientKind.Customer, customerId,
            variables, BranchId: branchId, CorrelationId: correlationId, UserContext: userContext));
    }

    /// <summary>A receipt number prints as <c>R-000123</c>; a payment awaiting approval has none yet.</summary>
    public static string ReceiptNumberText(long? receiptNumber) =>
        receiptNumber is { } number ? BillingMessages.ReceiptNumber(number) : "—";
}
