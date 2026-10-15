namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. Why a parcel did not reach the customer (US-ORD-20 AC3). Uncollected is for pickup orders only.</summary>
public sealed class DeliveryFailureReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DeliveryFailureReason Refused = new(1, nameof(Refused));
    public static readonly DeliveryFailureReason Undeliverable = new(2, nameof(Undeliverable));
    public static readonly DeliveryFailureReason Uncollected = new(3, nameof(Uncollected));
}
