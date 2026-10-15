namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.13. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class StorageBreachStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StorageBreachStatus Open = new(1, nameof(Open));
    public static readonly StorageBreachStatus Quarantined = new(2, nameof(Quarantined));
    public static readonly StorageBreachStatus Closed = new(3, nameof(Closed));
}
