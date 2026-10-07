namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 §8.3/§10.1. Immutable after create.</summary>
public sealed class ProductKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ProductKind Standard = new(1, nameof(Standard));
    public static readonly ProductKind Bundle = new(2, nameof(Bundle));
}
