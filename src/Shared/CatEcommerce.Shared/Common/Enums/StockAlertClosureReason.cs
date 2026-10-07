namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.11/10.1.</summary>
public sealed class StockAlertClosureReason(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StockAlertClosureReason CancelledByCustomer = new(1, nameof(CancelledByCustomer));
    public static readonly StockAlertClosureReason SkuNoLongerSold = new(2, nameof(SkuNoLongerSold));
    public static readonly StockAlertClosureReason CustomerAnonymised = new(3, nameof(CustomerAnonymised));
    public static readonly StockAlertClosureReason MergedDuplicate = new(4, nameof(MergedDuplicate));
}
