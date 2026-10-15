namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class StaffTimeRole(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StaffTimeRole Performer = new(1, nameof(Performer));
    public static readonly StaffTimeRole Assistant = new(2, nameof(Assistant));
}
