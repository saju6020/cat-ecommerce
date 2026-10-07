namespace CatEcommerce.Read.ViewModels;

public sealed record CountryRef(string Code, string Name);

public sealed record RatingSummary(decimal? Average, int Count);

public sealed record ProductImageRef(string? Url, string AltText, bool IsPrimary, Guid? SkuId);

/// <summary>HO-SHP-001 section 12.4 -- the public catalogue list/search item.</summary>
public sealed record CatalogueItem(
    Guid ProductId,
    string Slug,
    string Name,
    string Brand,
    string Category,
    string? Subcategory,
    string Kind,
    CountryRef? CountryOfOrigin,
    string? LifeStage,
    bool PrescriptionRequired,
    MoneyViewModel? FromPrice,
    bool PriceVaries,
    string Availability,
    RatingSummary Rating,
    ProductImageRef? PrimaryImage);

public sealed record CatalogueResult(
    IReadOnlyList<CatalogueItem> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, string?> AppliedFilters,
    CatalogueSuggestions? Suggestions);

public sealed record CatalogueSuggestions(IReadOnlyList<string> DidYouMean, IReadOnlyList<CatalogueItem> Popular);
