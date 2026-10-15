namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The kind of a controlled-drug register entry. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class ControlledDrugEntryKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ControlledDrugEntryKind OpeningBalance = new(1, nameof(OpeningBalance));
    public static readonly ControlledDrugEntryKind Receipt = new(2, nameof(Receipt));
    public static readonly ControlledDrugEntryKind Dispensed = new(3, nameof(Dispensed));
    public static readonly ControlledDrugEntryKind Administered = new(4, nameof(Administered));
    public static readonly ControlledDrugEntryKind Wastage = new(5, nameof(Wastage));
    public static readonly ControlledDrugEntryKind Returned = new(6, nameof(Returned));
    public static readonly ControlledDrugEntryKind Adjustment = new(7, nameof(Adjustment));
    public static readonly ControlledDrugEntryKind Correction = new(8, nameof(Correction));
}
