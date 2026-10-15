namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.10. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class StockCountStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StockCountStatus Open = new(1, nameof(Open));
    public static readonly StockCountStatus Submitted = new(2, nameof(Submitted));
    public static readonly StockCountStatus AwaitingApproval = new(3, nameof(AwaitingApproval));
    public static readonly StockCountStatus Committed = new(4, nameof(Committed));
    public static readonly StockCountStatus Cancelled = new(5, nameof(Cancelled));
}
