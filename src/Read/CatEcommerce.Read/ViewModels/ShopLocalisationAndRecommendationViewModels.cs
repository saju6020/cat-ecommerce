namespace CatEcommerce.Read.ViewModels;

/// <summary>HO-SHP-001 section 12.4 -- <c>GET /api/shop/locales</c>.</summary>
public sealed record LocalesViewModel(string DefaultLocale, IReadOnlyList<string> Locales);

public sealed record RecommendationReasonViewModel(string Code, IReadOnlyDictionary<string, string> Values);

public sealed record FoodRecommendationItemViewModel(CatalogueItem Item, IReadOnlyList<RecommendationReasonViewModel> Reasons);

public sealed record RecommendationCatViewModel(Guid CatId, string Name, string? LifeStage, bool LifeStageIsEstimated);

public sealed record AllergyExclusionViewModel(string Allergen, int Count);

/// <summary>HO-SHP-001 section 12.4 / 8.13 -- US-SHP-11. General guidance pending clinical sign-off
/// (OQ-9): the client labels it as such.</summary>
public sealed record FoodRecommendationsViewModel(
    RecommendationCatViewModel Cat,
    IReadOnlyList<FoodRecommendationItemViewModel> Items,
    IReadOnlyList<AllergyExclusionViewModel> ExcludedForAllergy,
    IReadOnlyList<string> Hints);

public sealed record StaffTranslationViewModel(string Locale, string? Name, string? Description, string? IngredientList, string? FeedingGuide);
