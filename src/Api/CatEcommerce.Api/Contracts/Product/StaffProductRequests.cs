namespace CatEcommerce.Api.Contracts.Product;

public sealed class CreateProductRequest
{
    public required string Kind { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public required string Category { get; init; }
    public string? Subcategory { get; init; }
    public required Guid TaxClassId { get; init; }
    public required string Slug { get; init; }
    public string? BundleSkuCode { get; init; }
}

public sealed class UpdateProductDetailsRequest
{
    public required int ExpectedVersion { get; init; }
    public required string Name { get; init; }
    public required string Brand { get; init; }
    public string? Description { get; init; }
    public required string Category { get; init; }
    public string? Subcategory { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public required Guid TaxClassId { get; init; }
    public string? CountryOfOriginCode { get; init; }
    public bool ListedOnStorefront { get; init; } = true;
    public bool IsExpiryTracked { get; init; }
    public IReadOnlyList<string> HealthFocus { get; init; } = [];
}

public sealed class ChangeProductSlugRequest
{
    public required int ExpectedVersion { get; init; }
    public required string Slug { get; init; }
}

public sealed class AddSkuRequest
{
    public required int ExpectedVersion { get; init; }
    public required string Code { get; init; }
    public string? Barcode { get; init; }
    public string? SizeOrWeight { get; init; }
    public string? Flavour { get; init; }
    public string? Colour { get; init; }
    public int? PackQuantity { get; init; }
}

public sealed class UpdateSkuRequest
{
    public required int ExpectedVersion { get; init; }
    public string? SizeOrWeight { get; init; }
    public string? Flavour { get; init; }
    public string? Colour { get; init; }
    public int? PackQuantity { get; init; }
}

public sealed class SkuVersionRequest
{
    public required int ExpectedVersion { get; init; }
}

public sealed class ProductImageRequest
{
    public required Guid FileId { get; init; }
    public Guid? SkuId { get; init; }
    public required string AltText { get; init; }
    public int SortOrder { get; init; }
    public bool IsPrimary { get; init; }
}

public sealed class SetProductImagesRequest
{
    public required int ExpectedVersion { get; init; }
    public required IReadOnlyList<ProductImageRequest> Images { get; init; }
}

public sealed class ScheduleSkuPriceRequest
{
    public required decimal Amount { get; init; }
    public required string CurrencyCode { get; init; }
    public required DateTimeOffset EffectiveFrom { get; init; }
}

public sealed class BundleComponentRequest
{
    public required Guid ComponentSkuId { get; init; }
    public required int Quantity { get; init; }
}

public sealed class SetBundleComponentsRequest
{
    public required int ExpectedVersion { get; init; }
    public required IReadOnlyList<BundleComponentRequest> Components { get; init; }
}

public sealed class RelatedProductRequest
{
    public required Guid RelatedProductId { get; init; }
    public required string Kind { get; init; }
    public int SortOrder { get; init; }
}

public sealed class SetRelatedProductsRequest
{
    public required int ExpectedVersion { get; init; }
    public required IReadOnlyList<RelatedProductRequest> Links { get; init; }
}

public sealed class SetProductTranslationRequest
{
    public required int ExpectedVersion { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? IngredientList { get; init; }
    public string? FeedingGuide { get; init; }
}
