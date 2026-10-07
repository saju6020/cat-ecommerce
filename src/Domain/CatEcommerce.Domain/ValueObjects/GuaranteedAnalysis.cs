using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>HO-SHP-001 section 8.5 -- owned by <c>FoodDetails</c>. All four percentages or none;
/// each 0-100; sum at most 100 (<c>InvalidGuaranteedAnalysis</c>).</summary>
public sealed record GuaranteedAnalysis
{
    public decimal ProteinPercent { get; }
    public decimal FatPercent { get; }
    public decimal FibrePercent { get; }
    public decimal MoisturePercent { get; }

    public GuaranteedAnalysis(decimal proteinPercent, decimal fatPercent, decimal fibrePercent, decimal moisturePercent)
    {
        foreach (var value in new[] { proteinPercent, fatPercent, fibrePercent, moisturePercent })
        {
            if (value < 0 || value > 100)
                throw new DomainRuleViolationException(BusinessViolationCodes.InvalidGuaranteedAnalysis, "Each guaranteed-analysis value must be between 0 and 100.", nameof(GuaranteedAnalysis));
        }

        if (proteinPercent + fatPercent + fibrePercent + moisturePercent > 100)
            throw new DomainRuleViolationException(BusinessViolationCodes.InvalidGuaranteedAnalysis, "The guaranteed-analysis values cannot sum to more than 100.", nameof(GuaranteedAnalysis));

        ProteinPercent = decimal.Round(proteinPercent, 2);
        FatPercent = decimal.Round(fatPercent, 2);
        FibrePercent = decimal.Round(fibrePercent, 2);
        MoisturePercent = decimal.Round(moisturePercent, 2);
    }
}
