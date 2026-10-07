using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.ValueObjects;

/// <summary>
/// HO-SHP-001 section 8.5 -- owned one, optional, food-only columns on <c>product.product</c>.
///
/// The four guaranteed-analysis percentages are stored as flat, individually nullable columns
/// rather than a nested owned <c>GuaranteedAnalysis</c> type: EF Core cannot materialise an
/// optional owned type that itself contains another owned dependent ("an optional dependent using
/// table sharing and containing other dependents without any required non-shared property to
/// identify whether the entity exists") -- every field of <c>FoodDetails</c> is genuinely optional
/// (a product may set only, say, <c>LifeStage</c>), so there is no non-null sentinel to add.
/// <see cref="GuaranteedAnalysis"/> is exposed as a derived, unmapped property that reconstructs
/// the validated value object from the four columns, so every other caller keeps working with the
/// same type section 8.5 names.
/// </summary>
public sealed record FoodDetails
{
    public string? Manufacturer { get; private set; }
    public LifeStage? LifeStage { get; private set; }
    public ProteinSource? ProteinSource { get; private set; }
    public string? IngredientList { get; private set; }
    public decimal? AnalysisProteinPercent { get; private set; }
    public decimal? AnalysisFatPercent { get; private set; }
    public decimal? AnalysisFibrePercent { get; private set; }
    public decimal? AnalysisMoisturePercent { get; private set; }
    public string? FeedingGuide { get; private set; }
    public string? StorageInstructions { get; private set; }

    /// <summary>Derived, not mapped -- reconstructed from the four flat columns above.</summary>
    public GuaranteedAnalysis? GuaranteedAnalysis => AnalysisProteinPercent is null
        ? null
        : new GuaranteedAnalysis(AnalysisProteinPercent.Value, AnalysisFatPercent!.Value, AnalysisFibrePercent!.Value, AnalysisMoisturePercent!.Value);

    /// <summary>EF Core materialises this owned type by property injection (a parameterless
    /// constructor), not constructor binding.</summary>
    private FoodDetails()
    {
    }

    public FoodDetails(string? manufacturer, LifeStage? lifeStage, ProteinSource? proteinSource,
        string? ingredientList, GuaranteedAnalysis? guaranteedAnalysis, string? feedingGuide, string? storageInstructions)
    {
        if (manufacturer is { Length: > ProductRules.ManufacturerMaxLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.FoodDetailsOnlyForFood, "Manufacturer cannot exceed 200 characters.", nameof(Manufacturer));

        if (ingredientList is { Length: > ProductRules.IngredientListMaxLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.FoodDetailsOnlyForFood, "Ingredient list cannot exceed 4000 characters.", nameof(IngredientList));

        if (storageInstructions is { Length: > ProductRules.StorageInstructionsMaxLength })
            throw new DomainRuleViolationException(BusinessViolationCodes.FoodDetailsOnlyForFood, "Storage instructions cannot exceed 1000 characters.", nameof(StorageInstructions));

        if (feedingGuide is { Length: > 0 })
        {
            if (feedingGuide.Length > ProductRules.FeedingGuideMaxLength)
                throw new DomainRuleViolationException(BusinessViolationCodes.FoodDetailsOnlyForFood, "Feeding guide cannot exceed 4000 characters.", nameof(FeedingGuide));

            var markupError = LimitedMarkup.Validate(feedingGuide);
            if (markupError is not null)
                throw new DomainRuleViolationException(markupError, "Feeding guide failed markup validation.", nameof(FeedingGuide));
        }

        Manufacturer = Trim(manufacturer);
        LifeStage = lifeStage;
        ProteinSource = proteinSource;
        IngredientList = Trim(ingredientList);
        AnalysisProteinPercent = guaranteedAnalysis?.ProteinPercent;
        AnalysisFatPercent = guaranteedAnalysis?.FatPercent;
        AnalysisFibrePercent = guaranteedAnalysis?.FibrePercent;
        AnalysisMoisturePercent = guaranteedAnalysis?.MoisturePercent;
        FeedingGuide = string.IsNullOrEmpty(feedingGuide) ? null : feedingGuide;
        StorageInstructions = Trim(storageInstructions);
    }

    /// <summary>US-SHP-03 AC1's labelling checklist (OQ-3) -- which fields are present, for the
    /// "LabellingComplete" warning-only publication item.</summary>
    public bool HasFullLabelling =>
        !string.IsNullOrWhiteSpace(Manufacturer) && !string.IsNullOrWhiteSpace(IngredientList) &&
        GuaranteedAnalysis is not null && !string.IsNullOrWhiteSpace(FeedingGuide) && !string.IsNullOrWhiteSpace(StorageInstructions);

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
