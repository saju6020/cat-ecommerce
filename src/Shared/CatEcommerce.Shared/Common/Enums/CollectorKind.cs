namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BIL-001 §8.8 / §10.1. Who physically took the money for a cash-on-delivery payment.</summary>
public sealed class CollectorKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly CollectorKind Staff = new(1, nameof(Staff));
    public static readonly CollectorKind Courier = new(2, nameof(Courier));
}
