namespace CatEcommerce.Read.ViewModels;

public sealed record GuaranteedAnalysisViewModel(decimal ProteinPercent, decimal FatPercent, decimal FibrePercent, decimal MoisturePercent);

public sealed record FoodDetailViewModel(
    string? Manufacturer, string? LifeStage, string? ProteinSource, string? IngredientList,
    GuaranteedAnalysisViewModel? GuaranteedAnalysis, string? FeedingGuide, string? StorageInstructions);

public sealed record ExpiryNoticeViewModel(DateOnly ExpiresOn);

public sealed record DetailSkuViewModel(
    Guid SkuId, string Code, string Label, string? SizeOrWeight, string? Flavour, string? Colour, int? PackQuantity,
    MoneyViewModel? Price, string Availability, ExpiryNoticeViewModel? ExpiryNotice, IReadOnlyList<ProductImageRef> Images);

public sealed record ProductDetailViewModel(
    Guid ProductId, string Slug, string Name, string Brand, string Description, string Category, string? Subcategory,
    IReadOnlyList<string> Tags, string Kind, CountryRef? CountryOfOrigin, bool PrescriptionRequired,
    FoodDetailViewModel? Food, IReadOnlyList<ProductImageRef> Images, IReadOnlyList<DetailSkuViewModel> Skus,
    RatingSummary Rating, bool PricesIncludeTax, string Locale, IReadOnlyDictionary<string, string> FieldLocales,
    BundleDetailViewModel? Bundle = null);
