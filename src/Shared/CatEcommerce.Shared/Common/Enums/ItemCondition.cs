namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. The condition a returned item is in (BR-10).</summary>
public sealed class ItemCondition(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ItemCondition Sealed = new(1, nameof(Sealed));
    public static readonly ItemCondition Opened = new(2, nameof(Opened));
    public static readonly ItemCondition Used = new(3, nameof(Used));
}
