namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. A delivery zone's service levels (US-ORD-06).</summary>
public sealed class DeliveryServiceLevel(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DeliveryServiceLevel Standard = new(1, nameof(Standard));
    public static readonly DeliveryServiceLevel Express = new(2, nameof(Express));
}
