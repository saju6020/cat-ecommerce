namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. What a discount code applies to.</summary>
public sealed class DiscountScope(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DiscountScope Order = new(1, nameof(Order));
    public static readonly DiscountScope Products = new(2, nameof(Products));
    public static readonly DiscountScope Categories = new(3, nameof(Categories));
}
