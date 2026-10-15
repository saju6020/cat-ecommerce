using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.5 (ADR-141) -- converts a prescribed dose to milligrams only where the units are provably convertible, and to mg/kg for a weight.
/// Anything else is <c>null</c> -- "not convertible" -- and the caller must report it; a silent pass on an unconvertible dose is the dangerous failure.
/// Pure.
/// <list type="bullet">
/// <item><c>Mg</c>/<c>Mcg</c>/<c>G</c>: converted to mg whatever the strength.</item>
/// <item><c>Ml</c>/<c>Tablet</c>/<c>Capsule</c>: amount x strength-in-mg / per-quantity, but only when the strength is a mass and its per-unit is
/// the same kind (a tablet dose against a per-ml strength is not convertible).</item>
/// <item><c>Drop</c>, <c>Application</c>, <c>Iu</c>: never.</item>
/// </list>
/// </summary>
public static class DoseConverter
{
    public const int MgPerKgDecimals = 4;

    public static decimal? ToMilligrams(Dose dose, Strength strength)
    {
        var unit = dose.Unit;
        if (unit == DoseUnit.Mg) return dose.Amount;
        if (unit == DoseUnit.Mcg) return dose.Amount / 1000m;
        if (unit == DoseUnit.G) return dose.Amount * 1000m;

        var strengthMg = strength.AmountInMilligrams;
        if (strengthMg is null) return null;

        var matches =
            (unit == DoseUnit.Ml && strength.PerUnit == DispensingUnit.Millilitre) ||
            (unit == DoseUnit.Tablet && strength.PerUnit == DispensingUnit.Tablet) ||
            (unit == DoseUnit.Capsule && strength.PerUnit == DispensingUnit.Capsule);

        return matches ? dose.Amount * strengthMg.Value / strength.PerQuantity : null;
    }

    public static decimal MgPerKg(decimal doseMg, decimal weightKg) => decimal.Round(doseMg / weightKg, MgPerKgDecimals, MidpointRounding.ToEven);

    public static decimal Round(decimal value) => decimal.Round(value, MgPerKgDecimals, MidpointRounding.ToEven);
}
