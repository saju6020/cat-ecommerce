namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SHP-001 section 8.8/10.3.</summary>
public static class ShopSearchRules
{
    public const double WordSimilarityThreshold = 0.45;
    public const double DidYouMeanSimilarity = 0.30;
    public const int MaxQueryLength = 100;
    public const int MaxFilterValues = 20;
    public const int MaxPageSize = 48;
    public const int DefaultPageSize = 24;
    public const int PopularSuggestionCount = 8;
}
