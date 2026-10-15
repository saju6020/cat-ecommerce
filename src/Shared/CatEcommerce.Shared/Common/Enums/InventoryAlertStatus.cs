namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.13. Named <c>InventoryAlertStatus</c>, not <c>StockAlertStatus</c> as §10.1 says -- that name is already Shop's back-in-stock alert status (HO-SHP-001); see HO-IMP-001 §1 Amended. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class InventoryAlertStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly InventoryAlertStatus Open = new(1, nameof(Open));
    public static readonly InventoryAlertStatus Acknowledged = new(2, nameof(Acknowledged));
    public static readonly InventoryAlertStatus Resolved = new(3, nameof(Resolved));
}
