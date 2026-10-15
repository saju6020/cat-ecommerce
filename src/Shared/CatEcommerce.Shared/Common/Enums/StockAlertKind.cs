namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.13. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class StockAlertKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StockAlertKind ExpiryThreshold = new(1, nameof(ExpiryThreshold));
    public static readonly StockAlertKind Expired = new(2, nameof(Expired));
    public static readonly StockAlertKind LowStock = new(3, nameof(LowStock));
    public static readonly StockAlertKind Shortfall = new(4, nameof(Shortfall));
    public static readonly StockAlertKind StorageBreach = new(5, nameof(StorageBreach));
    public static readonly StockAlertKind LedgerMismatch = new(6, nameof(LedgerMismatch));
}
