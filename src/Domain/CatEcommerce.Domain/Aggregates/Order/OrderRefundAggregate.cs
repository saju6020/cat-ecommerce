using CatEcommerce.Domain.Events.Order;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Domain.Aggregates.Order;

/// <summary>
/// HO-ORD-001 §8.10 (ADR-211), US-ORD-15 -- one refund of an order, <b>append-only</b>: method, amount, reference, the staff member who issued it, the reason and
/// the Billing credit note it is linked to. There is no mutator: a mistake is a new, opposite record in Billing, never an edit here. The order itself
/// keeps the running <c>AmountRefunded</c> / <c>RefundDue</c> (<c>OrderAggregate.ApplyRefund</c>).
/// </summary>
public sealed class OrderRefundAggregate : AggregateRoot
{
    public Guid OrderId { get; private set; }
    public Guid? ReturnRequestId { get; private set; }
    public OrderRefundMethod Method { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = default!;
    public string? Reference { get; private set; }
    public string Reason { get; private set; } = default!;
    public Guid IssuedBy { get; private set; }
    public DateTimeOffset IssuedOn { get; private set; }
    public Guid? CreditNoteId { get; private set; }

    private OrderRefundAggregate() { }

    public OrderRefundAggregate(Guid id) : base(id) { }

    public void Issue(
        Guid orderId, Guid? returnRequestId, OrderRefundMethod method, decimal amount, string currencyCode, string? reference, string reason, Guid? creditNoteId,
        DateTimeOffset now, UserContext user, Guid correlationId)
    {
        if (amount <= 0m)
            throw new DomainRuleViolationException(BusinessViolationCodes.RefundExceedsAmountPaid, "A refund must be more than zero.", nameof(amount));

        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > OrderRules.MaxReasonLength)
            throw new DomainRuleViolationException(BusinessViolationCodes.RefundReferenceRequired, $"Give the reason for the refund (up to {OrderRules.MaxReasonLength} characters).", nameof(reason));

        if (reference is { Length: > OrderRules.MaxTransferReferenceLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.RefundReferenceRequired, $"A reference is up to {OrderRules.MaxTransferReferenceLength} characters.", nameof(reference));

        OrderFreeText.RefuseCardNumber(reference, nameof(reference)); // C-5

        OrderId = orderId;
        ReturnRequestId = returnRequestId;
        Method = method;
        Amount = amount;
        CurrencyCode = currencyCode.ToUpperInvariant();
        Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim();
        Reason = reason.Trim();
        IssuedBy = user.UserId;
        IssuedOn = now;
        CreditNoteId = creditNoteId;

        SetDefaultValue(user);
        AddDomainEvent(new RefundIssuedEvent { RefundId = Id, OrderId = orderId, Method = method.Value, Amount = new Money(amount, CurrencyCode), CorrelationId = correlationId, UserContext = user });
        UpdateVersion();
    }
}
