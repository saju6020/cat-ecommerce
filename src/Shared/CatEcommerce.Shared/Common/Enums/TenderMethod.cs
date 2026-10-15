namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. A counter-sale tender (US-ORD-16). An in-store card is a method, an amount and a slip reference only.</summary>
public sealed class TenderMethod(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly TenderMethod Cash = new(1, nameof(Cash));
    public static readonly TenderMethod InStoreCard = new(2, nameof(InStoreCard));
}
