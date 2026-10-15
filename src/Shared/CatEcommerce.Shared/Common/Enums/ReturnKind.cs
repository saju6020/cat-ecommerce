namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-ORD-001 §10.1. A customer return or a delivery failure (US-ORD-20 AC3).</summary>
public sealed class ReturnKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReturnKind CustomerReturn = new(1, nameof(CustomerReturn));
    public static readonly ReturnKind DeliveryFailure = new(2, nameof(DeliveryFailure));
}
