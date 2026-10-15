using System.Globalization;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-PHM-001 §8.2 -- a formulary medicine's strength: <c>50 mg per 1 Tablet</c>, <c>1.5 mg per 1 Millilitre</c>. Amount and per-quantity are
/// positive with at most four decimal places. EF materialises it through the private parameterless constructor (owned VO).
/// </summary>
public sealed record Strength
{
    public decimal Amount { get; init; }
    public StrengthUnit Unit { get; init; }
    public decimal PerQuantity { get; init; }
    public DispensingUnit PerUnit { get; init; }

    public Strength(decimal amount, StrengthUnit unit, decimal perQuantity, DispensingUnit perUnit)
    {
        if (amount <= 0m || decimal.Round(amount, 4) != amount)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStrength, "The strength must be greater than zero with at most four decimal places.", nameof(Amount));

        if (perQuantity <= 0m || decimal.Round(perQuantity, 4) != perQuantity)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidStrength, "The per-quantity must be greater than zero with at most four decimal places.", nameof(PerQuantity));

        Amount = amount;
        Unit = unit;
        PerQuantity = perQuantity;
        PerUnit = perUnit;
    }

    // EF only.
    private Strength()
    {
        Unit = default!;
        PerUnit = default!;
    }

    /// <summary>The label wording: <c>1.5 mg/ml</c> for a liquid, <c>50 mg per tablet</c> otherwise.</summary>
    public string Text()
    {
        var amount = Trim(Amount);
        var unit = UnitLabel(Unit);
        var per = PerUnit == DispensingUnit.Millilitre
            ? (PerQuantity == 1m ? "/ml" : $"/{Trim(PerQuantity)} ml")
            : $" per {(PerQuantity == 1m ? "" : Trim(PerQuantity) + " ")}{PerUnit.DisplayName.ToLowerInvariant()}";
        return $"{amount} {unit}{per}";
    }

    /// <summary>The strength expressed in milligrams per <see cref="PerQuantity"/> of <see cref="PerUnit"/>, or <c>null</c> when it is not a mass (IU, percent).</summary>
    public decimal? AmountInMilligrams => Unit == StrengthUnit.Mg ? Amount
        : Unit == StrengthUnit.Mcg ? Amount / 1000m
        : Unit == StrengthUnit.G ? Amount * 1000m
        : null;

    public static string UnitLabel(StrengthUnit unit) =>
        unit == StrengthUnit.Mg ? "mg" : unit == StrengthUnit.Mcg ? "mcg" : unit == StrengthUnit.G ? "g" : unit == StrengthUnit.Iu ? "IU" : "%";

    internal static string Trim(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}
