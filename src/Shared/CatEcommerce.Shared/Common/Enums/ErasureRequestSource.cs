namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-XCT-001 §10.1 -- how an erasure ledger was opened (ADR-302, ADR-314).</summary>
public sealed class ErasureRequestSource(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ErasureRequestSource SelfService = new(1, nameof(SelfService));
    public static readonly ErasureRequestSource StaffOnBehalf = new(2, nameof(StaffOnBehalf));
    public static readonly ErasureRequestSource RestoreReconciliation = new(3, nameof(RestoreReconciliation));
}
