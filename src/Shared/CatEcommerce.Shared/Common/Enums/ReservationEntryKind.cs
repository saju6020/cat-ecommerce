namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-IMP-001 §10.1 -- §8.7. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class ReservationEntryKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ReservationEntryKind Reserve = new(1, nameof(Reserve));
    public static readonly ReservationEntryKind Release = new(2, nameof(Release));
}
