namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The kind of a prescription safety warning. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class SafetyWarningKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly SafetyWarningKind DoseBelowRange = new(1, nameof(DoseBelowRange));
    public static readonly SafetyWarningKind DoseAboveRange = new(2, nameof(DoseAboveRange));
    public static readonly SafetyWarningKind DoseNotCheckable = new(3, nameof(DoseNotCheckable));
    public static readonly SafetyWarningKind AllergyConflict = new(4, nameof(AllergyConflict));
    public static readonly SafetyWarningKind Interaction = new(5, nameof(Interaction));
}
