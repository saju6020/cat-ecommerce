namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.5. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class PurchaseOrderStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly PurchaseOrderStatus Draft = new(1, nameof(Draft));
    public static readonly PurchaseOrderStatus Issued = new(2, nameof(Issued));
    public static readonly PurchaseOrderStatus Closed = new(3, nameof(Closed));
    public static readonly PurchaseOrderStatus Cancelled = new(4, nameof(Cancelled));
}
