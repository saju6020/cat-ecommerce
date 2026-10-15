namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-XCT-001 §10.1 -- an erasure ledger is Open until every scope is Completed.</summary>
public sealed class ErasureRequestStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ErasureRequestStatus Open = new(1, nameof(Open));
    public static readonly ErasureRequestStatus Completed = new(2, nameof(Completed));
}
