namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class AnaesthesiaEntryKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly AnaesthesiaEntryKind Drug = new(1, nameof(Drug));
    public static readonly AnaesthesiaEntryKind Vitals = new(2, nameof(Vitals));
    public static readonly AnaesthesiaEntryKind Milestone = new(3, nameof(Milestone));
    public static readonly AnaesthesiaEntryKind Correction = new(4, nameof(Correction));
}
