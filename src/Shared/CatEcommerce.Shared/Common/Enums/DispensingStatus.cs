namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The state of a dispensing record. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class DispensingStatus(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DispensingStatus Dispensed = new(1, nameof(Dispensed));
    public static readonly DispensingStatus Reversed = new(2, nameof(Reversed));
}
