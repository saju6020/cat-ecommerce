namespace CatEcommerce.Shared.Common.Enums;

/// <summary>Owned by HO-BIL-001 §8.2/§10.1 (FR-BIL-2). What kind of thing a charge or invoice line is.</summary>
public sealed class ItemKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ItemKind Service = new(1, nameof(Service));
    public static readonly ItemKind Product = new(2, nameof(Product));
    public static readonly ItemKind Medicine = new(3, nameof(Medicine));
    public static readonly ItemKind BoardingNight = new(4, nameof(BoardingNight));
    public static readonly ItemKind Fee = new(5, nameof(Fee));
}
