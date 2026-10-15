namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- What a stock location is (§8.3); its permitted movements live in <c>StockLocationRules</c>. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class StockLocationKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StockLocationKind ShopFloor = new(1, nameof(ShopFloor));
    public static readonly StockLocationKind Warehouse = new(2, nameof(Warehouse));
    public static readonly StockLocationKind ClinicDispensary = new(3, nameof(ClinicDispensary));
    public static readonly StockLocationKind BoardingStore = new(4, nameof(BoardingStore));
    public static readonly StockLocationKind Quarantine = new(5, nameof(Quarantine));
}
