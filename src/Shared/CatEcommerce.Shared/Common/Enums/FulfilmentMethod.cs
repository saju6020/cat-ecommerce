namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. How the goods reach the customer.</summary>
public sealed class FulfilmentMethod(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly FulfilmentMethod Delivery = new(1, nameof(Delivery));
    public static readonly FulfilmentMethod Pickup = new(2, nameof(Pickup));
    public static readonly FulfilmentMethod Counter = new(3, nameof(Counter));
}
