namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. Exactly Appendix C's states (ADR-197). Expiry is Cancelled + PaymentWindowExpired; pickup reuses Dispatched (ready for collection) and Delivered (collected).</summary>
public sealed class OrderStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly OrderStatus PendingPayment = new(1, nameof(PendingPayment));
    public static readonly OrderStatus Confirmed = new(2, nameof(Confirmed));
    public static readonly OrderStatus Packing = new(3, nameof(Packing));
    public static readonly OrderStatus Dispatched = new(4, nameof(Dispatched));
    public static readonly OrderStatus Delivered = new(5, nameof(Delivered));
    public static readonly OrderStatus Cancelled = new(6, nameof(Cancelled));
    public static readonly OrderStatus Returned = new(7, nameof(Returned));
    public static readonly OrderStatus Refunded = new(8, nameof(Refunded));
}
