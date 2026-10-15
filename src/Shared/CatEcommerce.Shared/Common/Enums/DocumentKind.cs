namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- Document-number sequences (ADR-114). Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class DocumentKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DocumentKind PurchaseOrder = new(1, nameof(PurchaseOrder));
    public static readonly DocumentKind Consignment = new(2, nameof(Consignment));
    public static readonly DocumentKind GoodsReceipt = new(3, nameof(GoodsReceipt));
    public static readonly DocumentKind StockTransfer = new(4, nameof(StockTransfer));
    public static readonly DocumentKind StockAdjustment = new(5, nameof(StockAdjustment));
    public static readonly DocumentKind StockCount = new(6, nameof(StockCount));
    public static readonly DocumentKind BatchRecall = new(7, nameof(BatchRecall));
}
