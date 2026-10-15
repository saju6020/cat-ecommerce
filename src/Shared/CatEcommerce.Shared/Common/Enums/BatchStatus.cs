namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.6.1. <c>Expired</c> is deliberately NOT a member: it is derived (ADR-101). Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class BatchStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BatchStatus Available = new(1, nameof(Available));
    public static readonly BatchStatus Quarantined = new(2, nameof(Quarantined));
    public static readonly BatchStatus Recalled = new(3, nameof(Recalled));
    public static readonly BatchStatus WrittenOff = new(4, nameof(WrittenOff));
}
