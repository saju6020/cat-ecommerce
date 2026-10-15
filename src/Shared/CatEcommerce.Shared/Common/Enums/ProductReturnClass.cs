namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. A SKU's category class for BR-10 return eligibility.</summary>
public sealed class ProductReturnClass(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ProductReturnClass Food = new(1, nameof(Food));
    public static readonly ProductReturnClass Litter = new(2, nameof(Litter));
    public static readonly ProductReturnClass Other = new(3, nameof(Other));
}
