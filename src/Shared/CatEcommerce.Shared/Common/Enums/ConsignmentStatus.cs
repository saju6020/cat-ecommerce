namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.5. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class ConsignmentStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ConsignmentStatus Planned = new(1, nameof(Planned));
    public static readonly ConsignmentStatus InTransit = new(2, nameof(InTransit));
    public static readonly ConsignmentStatus AtCustoms = new(3, nameof(AtCustoms));
    public static readonly ConsignmentStatus Cleared = new(4, nameof(Cleared));
    public static readonly ConsignmentStatus Cancelled = new(5, nameof(Cancelled));
}
