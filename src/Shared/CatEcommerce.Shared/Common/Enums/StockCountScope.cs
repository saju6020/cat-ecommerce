namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.10. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class StockCountScope(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StockCountScope FullLocation = new(1, nameof(FullLocation));
    public static readonly StockCountScope SelectedSkus = new(2, nameof(SelectedSkus));
}
