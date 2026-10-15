namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The unit a prescription quantity and the stock ledger use. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class DispensingUnit(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly DispensingUnit Tablet = new(1, nameof(Tablet));
    public static readonly DispensingUnit Capsule = new(2, nameof(Capsule));
    public static readonly DispensingUnit Millilitre = new(3, nameof(Millilitre));
    public static readonly DispensingUnit Gram = new(4, nameof(Gram));
    public static readonly DispensingUnit Vial = new(5, nameof(Vial));
    public static readonly DispensingUnit Ampoule = new(6, nameof(Ampoule));
    public static readonly DispensingUnit Tube = new(7, nameof(Tube));
    public static readonly DispensingUnit Bottle = new(8, nameof(Bottle));
    public static readonly DispensingUnit Patch = new(9, nameof(Patch));
    public static readonly DispensingUnit Pipette = new(10, nameof(Pipette));
    public static readonly DispensingUnit Dose = new(11, nameof(Dose));
}
