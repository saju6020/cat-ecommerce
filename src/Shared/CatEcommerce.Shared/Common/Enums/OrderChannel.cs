namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. Append-only: HO-SUB-001 adds Subscription.</summary>
public sealed class OrderChannel(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly OrderChannel Online = new(1, nameof(Online));
    public static readonly OrderChannel Counter = new(2, nameof(Counter));
}
