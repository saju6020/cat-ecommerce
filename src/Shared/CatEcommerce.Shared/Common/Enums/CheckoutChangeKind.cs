namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. A change since the item entered the cart, listed on the quote (US-ORD-02).</summary>
public sealed class CheckoutChangeKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CheckoutChangeKind PriceIncreased = new(1, nameof(PriceIncreased));
    public static readonly CheckoutChangeKind PriceDecreased = new(2, nameof(PriceDecreased));
    public static readonly CheckoutChangeKind QuantityReduced = new(3, nameof(QuantityReduced));
    public static readonly CheckoutChangeKind NowUnavailable = new(4, nameof(NowUnavailable));
    public static readonly CheckoutChangeKind PrescriptionAuthorisationExpired = new(5, nameof(PrescriptionAuthorisationExpired));
    public static readonly CheckoutChangeKind PrescriptionCatRequired = new(6, nameof(PrescriptionCatRequired));
}
