namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-SRG-001 §10.1. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class ChecklistItemStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ChecklistItemStatus Outstanding = new(1, nameof(Outstanding));
    public static readonly ChecklistItemStatus Done = new(2, nameof(Done));
    public static readonly ChecklistItemStatus Waived = new(3, nameof(Waived));
}
