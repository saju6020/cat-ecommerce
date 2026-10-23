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
    ProductImageRef? PrimaryImage,
    // One card per size (owner, 2026-10-18), additive: the SKU this card is, and its size label
    // ("400 g"). Absent on rows that are not a single size (recommendations, suggestions).
    Guid? SkuId = null,
    string? SizeLabel = null);

public sealed record CatalogueResult(
    IReadOnlyList<CatalogueItem> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    IReadOnlyDictionary<string, string?> AppliedFilters,
    CatalogueSuggestions? Suggestions,
    // How many cards (sizes) the whole filtered list has; TotalCount stays the product count that
    // paging is based on (owner, 2026-10-18).
    int? TotalItemCount = null);

public sealed record CatalogueSuggestions(IReadOnlyList<string> DidYouMean, IReadOnlyList<CatalogueItem> Popular);
