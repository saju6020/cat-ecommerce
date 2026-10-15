namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- How stock must be kept (§8.4). Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class StorageCondition(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StorageCondition Ambient = new(1, nameof(Ambient));
    public static readonly StorageCondition CoolDry = new(2, nameof(CoolDry));
    public static readonly StorageCondition Refrigerated = new(3, nameof(Refrigerated));
    public static readonly StorageCondition Frozen = new(4, nameof(Frozen));
}
