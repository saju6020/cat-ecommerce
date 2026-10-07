namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SHP-001 section 8.11/10.1.</summary>
public sealed class StockAlertStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StockAlertStatus Pending = new(1, nameof(Pending));
    public static readonly StockAlertStatus Notified = new(2, nameof(Notified));
    public static readonly StockAlertStatus Closed = new(3, nameof(Closed));
}
