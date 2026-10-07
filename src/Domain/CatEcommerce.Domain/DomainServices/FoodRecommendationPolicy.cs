using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>What the policy knows about the cat (HO-CAT-001's own fields, read-only).</summary>
public sealed record CatRecommendationFacts(
    DateOnly? DateOfBirth,
    DateOfBirthPrecision DateOfBirthPrecision,
    NeuterStatus NeuterStatus,
    decimal? LatestWeightKg,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Conditions);

/// <summary>One food product the caller could be shown, with everything the policy needs.
/// <c>AuthorisedForThisCat</c> is the gate's answer for THIS cat (HO-SHP-001 section 8.9).</summary>
public sealed record RecommendationCandidate(
    Guid ProductId,
    string Name,
    LifeStage? LifeStage,
    IReadOnlyList<HealthFocus> HealthFocus,
    string? IngredientList,
    bool PrescriptionRequired,
    bool InStock,
    bool AuthorisedForThisCat,
    int UnitsSold90d,
    bool IngredientsUnknown = false);

/// <summary>The thresholds, from <c>shop.recommendation*</c> settings (OQ-9).</summary>
public sealed record RecommendationThresholds(int KittenMaxAgeMonths, int SeniorFromAgeYears, decimal WeightManagementFromKg);

public sealed record RecommendationReasonItem(RecommendationReason Code, IReadOnlyDictionary<string, string> Values);

public sealed record RecommendedProduct(Guid ProductId, IReadOnlyList<RecommendationReasonItem> Reasons);

public sealed record AllergyExclusion(string Allergen, int Count);

public sealed record FoodRecommendationResult(
    DerivedLifeStage LifeStage,
    IReadOnlyList<RecommendedProduct> Items,
    IReadOnlyList<AllergyExclusion> ExcludedForAllergy,
    IReadOnlyList<string> Hints);

/// <summary>
/// HO-SHP-001 section 8.13 / ADR-089 -- pure and explainable. (1) Exclude out-of-stock, prescription
/// products without a valid authorisation for THIS cat, and every product whose ingredient list
/// matches a recorded allergen (<see cref="AllergenMatcher"/>); (2) derive the life stage from the
/// date of birth (<see cref="CatLifeStageDerivation"/>) -- with none, life stage is not filtered and
/// the result says so (<c>AddDateOfBirth</c>); (3) attach reasons; (4) rank by number of reasons,
/// then units sold, then name, and keep up to <see cref="MaxResults"/>. A product with no reason is
/// not a recommendation. General guidance pending clinical sign-off (OQ-9).
/// </summary>
public static class FoodRecommendationPolicy
{
    public const int MaxResults = 12;

    public static FoodRecommendationResult Recommend(
        CatRecommendationFacts cat, IReadOnlyList<RecommendationCandidate> candidates, RecommendationThresholds thresholds, DateOnly today)
    {
        var lifeStage = CatLifeStageDerivation.Derive(
            cat.DateOfBirth, cat.DateOfBirthPrecision, today, thresholds.KittenMaxAgeMonths, thresholds.SeniorFromAgeYears);

        var hints = new List<string>();
        if (lifeStage.Stage is null) hints.Add("AddDateOfBirth");

        var conditionFocuses = cat.Conditions.SelectMany(c => HealthFocusKeywords.FocusesFor(c)).ToHashSet();
        var wantsWeightManagement = cat.LatestWeightKg is { } weight
            && weight >= thresholds.WeightManagementFromKg && lifeStage.Stage != LifeStage.Kitten;

        // De-duplicated case-insensitively (trimmed): a cat can carry "Chicken" and "chicken", or the same
        // allergen twice, and nothing upstream prevents it. A duplicate key here once crashed the endpoint.
        var allergyCounts = cat.Allergens
            .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim())
            .GroupBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.First(), _ => 0, StringComparer.OrdinalIgnoreCase);
        var hasAllergies = allergyCounts.Count > 0;
        var excludedUnknown = false;
        var recommended = new List<(RecommendationCandidate Candidate, List<RecommendationReasonItem> Reasons)>();

        foreach (var candidate in candidates)
        {
            if (!candidate.InStock) continue;
            if (candidate.PrescriptionRequired && !candidate.AuthorisedForThisCat) continue;

            // Life stage is filtered only when the cat's is known; AllLifeStages matches every stage.
            var stageMatches = lifeStage.Stage is null
                || candidate.LifeStage == LifeStage.AllLifeStages
                || candidate.LifeStage == lifeStage.Stage;
            if (!stageMatches) continue;

            // A product with no reason is not a recommendation, so it is neither shown nor counted
            // as "excluded for allergy" -- the count is of products that WOULD have been recommended.
            var reasons = BuildReasons(cat, candidate, lifeStage, conditionFocuses, wantsWeightManagement);
            if (reasons.Count == 0) continue;

            // FAIL-CLOSED (integrator decision 2026-10-04, section 1 Amended): for a cat with ANY recorded
            // allergen, a product whose ingredient list is unknown (empty, or a bundle with a food
            // component whose list is unknown) may contain it, so it is never recommended.
            if (hasAllergies && (candidate.IngredientsUnknown || AllergenMatcher.IsUnknown(candidate.IngredientList)))
            {
                excludedUnknown = true;
                continue;
            }

            var matchedAllergens = allergyCounts.Keys.Where(a => AllergenMatcher.Matches(a, candidate.IngredientList)).ToList();
            if (matchedAllergens.Count > 0)
            {
                foreach (var allergen in matchedAllergens) allergyCounts[allergen]++;
                continue;
            }

            recommended.Add((candidate, reasons));
        }

        var items = recommended
            .OrderByDescending(r => r.Reasons.Count)
            .ThenByDescending(r => r.Candidate.UnitsSold90d)
            .ThenBy(r => r.Candidate.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Candidate.ProductId)
            .Take(MaxResults)
            .Select(r => new RecommendedProduct(r.Candidate.ProductId, r.Reasons))
            .ToList();

        if (excludedUnknown) hints.Add("IngredientsUnknownExcluded");

        var excluded = allergyCounts.Where(kv => kv.Value > 0).Select(kv => new AllergyExclusion(kv.Key, kv.Value)).ToList();

        return new FoodRecommendationResult(lifeStage, items, excluded, hints);
    }

    private static List<RecommendationReasonItem> BuildReasons(
        CatRecommendationFacts cat, RecommendationCandidate candidate, DerivedLifeStage lifeStage,
        IReadOnlySet<HealthFocus> conditionFocuses, bool wantsWeightManagement)
    {
        var reasons = new List<RecommendationReasonItem>();

        if (lifeStage.Stage is not null)
        {
            reasons.Add(new RecommendationReasonItem(RecommendationReason.LifeStageMatch, new Dictionary<string, string>
            {
                ["lifeStage"] = lifeStage.Stage.DisplayName,
                ["about"] = lifeStage.IsEstimated ? "true" : "false"
            }));
        }

        if (cat.NeuterStatus == NeuterStatus.Neutered && candidate.HealthFocus.Contains(HealthFocus.Sterilised))
            reasons.Add(new RecommendationReasonItem(RecommendationReason.Sterilised, new Dictionary<string, string>()));

        if (wantsWeightManagement && candidate.HealthFocus.Contains(HealthFocus.WeightManagement))
        {
            reasons.Add(new RecommendationReasonItem(RecommendationReason.WeightManagement, new Dictionary<string, string>
            { ["weightKg"] = cat.LatestWeightKg!.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) }));
        }

        var matchedFocus = candidate.HealthFocus.FirstOrDefault(f => conditionFocuses.Contains(f) && f != HealthFocus.WeightManagement);
        var weightConditionMatch = candidate.HealthFocus.Contains(HealthFocus.WeightManagement) && conditionFocuses.Contains(HealthFocus.WeightManagement);
        if (matchedFocus is not null || weightConditionMatch)
        {
            reasons.Add(new RecommendationReasonItem(RecommendationReason.ConditionMatch, new Dictionary<string, string>
            { ["focus"] = (matchedFocus ?? HealthFocus.WeightManagement).DisplayName }));
        }

        return reasons;
    }
}
