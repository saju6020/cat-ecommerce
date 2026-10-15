namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.8 / §10.1. What an earmarked payment (a deposit) is set aside for.</summary>
public sealed class EarmarkKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly EarmarkKind Order = new(1, nameof(Order));
    public static readonly EarmarkKind BoardingStay = new(2, nameof(BoardingStay));
}
