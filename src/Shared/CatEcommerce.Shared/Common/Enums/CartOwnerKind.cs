namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. Who a cart belongs to.</summary>
public sealed class CartOwnerKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CartOwnerKind Guest = new(1, nameof(Guest));
    public static readonly CartOwnerKind Customer = new(2, nameof(Customer));
}
