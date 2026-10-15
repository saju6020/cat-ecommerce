namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. Why an order was cancelled. An expired transfer order is Cancelled with PaymentWindowExpired (ADR-197).</summary>
public sealed class OrderCancellationReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly OrderCancellationReason CustomerRequest = new(1, nameof(CustomerRequest));
    public static readonly OrderCancellationReason StaffDecision = new(2, nameof(StaffDecision));
    public static readonly OrderCancellationReason PaymentWindowExpired = new(3, nameof(PaymentWindowExpired));
}
