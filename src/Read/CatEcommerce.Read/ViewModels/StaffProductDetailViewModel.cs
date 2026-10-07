namespace CatEcommerce.Read.ViewModels;

public sealed record PublicationChecklistItemViewModel(string Item, bool IsBlocking, bool IsMet, string Message);

public sealed record StaffSkuViewModel(
    Guid SkuId,
    string Code,
    string? Barcode,
    string? SizeOrWeight,
    string? Flavour,
    string? Colour,
    int? PackQuantity,
    string Status,
    MoneyViewModel? CurrentPrice,
    MoneyViewModel? ScheduledPrice,
    string? Label = null);

/// <summary>HO-SHP-001 section 12.4 -- full staff detail, including drafts, version and the
/// publication checklist (section 8.4).</summary>
public sealed record StaffProductDetailViewModel(
    Guid ProductId,
    int Version,
    string Kind,
    string Name,
    string Brand,
    string Description,
    string Category,
    string? Subcategory,
    IReadOnlyList<string> Tags,
    string Slug,
    string Status,
    DateTimeOffset? FirstPublishedOn,
    Guid TaxClassId,
    string? CountryOfOriginCode,
    bool PrescriptionRequired,
    bool IsExpiryTracked,
    bool ListedOnStorefront,
    IReadOnlyList<StaffSkuViewModel> Skus,
    IReadOnlyList<PublicationChecklistItemViewModel> PublishChecklist,
    IReadOnlyList<StaffBundleComponentViewModel>? BundleComponents = null,
    IReadOnlyList<StaffRelatedProductViewModel>? RelatedProducts = null,
    IReadOnlyList<StaffTranslationViewModel>? Translations = null,
    FoodDetailViewModel? Food = null,
    IReadOnlyList<StaffProductImageViewModel>? Images = null,
    IReadOnlyList<string>? HealthFocus = null);

/// <summary>HO-SHP-001-FU1 -- the staff image row carries <c>FileId</c> and <c>SortOrder</c> so the
/// images editor can re-save the set it loaded. <c>Url</c> is null until a media URL builder exists
/// (same as the public <c>ProductImageRef</c>).</summary>
public sealed record StaffProductImageViewModel(Guid FileId, string? Url, string AltText, int SortOrder, bool IsPrimary, Guid? SkuId);

/// <summary>HO-SHP-001 section 8.10 -- <c>IsOnSale</c> is false when the component's SKU is
/// discontinued or its product is not Active (the bundle is then unavailable; flagged for staff).</summary>
public sealed record StaffBundleComponentViewModel(Guid ComponentSkuId, string? SkuCode, string? ProductName, int Quantity, bool IsOnSale);

public sealed record StaffRelatedProductViewModel(Guid RelatedProductId, string Kind, int SortOrder, string? Name, string? Status);
