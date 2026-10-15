namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- Derived from posted receipts, never persisted (ADR-112). Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class PurchaseOrderReceiptState(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly PurchaseOrderReceiptState NotReceived = new(1, nameof(NotReceived));
    public static readonly PurchaseOrderReceiptState PartiallyReceived = new(2, nameof(PartiallyReceived));
    public static readonly PurchaseOrderReceiptState FullyReceived = new(3, nameof(FullyReceived));
}
