using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>HO-PHM-001 §8.2 -- the practice's mg/kg range for a per-kg medicine. Both bounds positive, at most four decimal places, min not above max.</summary>
public sealed record DoseRange
{
    public decimal MinMgPerKg { get; init; }
    public decimal MaxMgPerKg { get; init; }

    public DoseRange(decimal minMgPerKg, decimal maxMgPerKg)
    {
        if (minMgPerKg <= 0m || maxMgPerKg <= 0m || decimal.Round(minMgPerKg, 4) != minMgPerKg || decimal.Round(maxMgPerKg, 4) != maxMgPerKg)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidDoseRange, "The dose range must be greater than zero with at most four decimal places.", nameof(MinMgPerKg));

        if (minMgPerKg > maxMgPerKg)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidDoseRange, "The minimum dose cannot be above the maximum.", nameof(MinMgPerKg));

        MinMgPerKg = minMgPerKg;
        MaxMgPerKg = maxMgPerKg;
    }

    // EF only.
    private DoseRange() { }
}
