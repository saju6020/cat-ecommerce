namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-BRD-001 §10.1 (ADR-221). The kind of a boarding unit; also the key of a rate card: the nightly rate is the unit TYPE rate.
/// <see cref="Isolation"/> units are never offered online or to a stay that does not need isolation (ADR-227).</summary>
public sealed class BoardingUnitType(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly BoardingUnitType Standard = new(1, nameof(Standard));
    public static readonly BoardingUnitType Deluxe = new(2, nameof(Deluxe));
    public static readonly BoardingUnitType Family = new(3, nameof(Family));
    public static readonly BoardingUnitType Isolation = new(4, nameof(Isolation));
}
