namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. A discount code's value kind (US-ORD-05).</summary>
public sealed class DiscountKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DiscountKind Percentage = new(1, nameof(Percentage));
    public static readonly DiscountKind FixedAmount = new(2, nameof(FixedAmount));
}
