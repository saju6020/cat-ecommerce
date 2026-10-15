namespace CatEcommerce.Read.ViewModels;

public sealed record EligibleCatViewModel(Guid CatId, string Name, DateOnly ValidTo);

public sealed record SkuPurchasabilityViewModel(
    Guid SkuId, bool Purchasable, string? PurchaseBlockReason, IReadOnlyList<EligibleCatViewModel> EligibleCats);

/// <summary>HO-SHP-001 section 12.4 -- US-SHP-10 AC1.</summary>
public sealed record PurchasabilityViewModel(IReadOnlyList<SkuPurchasabilityViewModel> Skus);

/// <summary>HO-SHP-001 section 8.9 -- the contract HO-ORD-001 calls at add-to-cart and checkout.</summary>
public sealed record PrescriptionEligibilityViewModel(bool Eligible, string? BlockReason);

/// <summary>HO-SHP-001 section 8.10 -- the contract HO-ORD-001 / HO-IMP-001 use to deduct each
/// component from its own stock.</summary>
public sealed record BundleCompositionLineViewModel(Guid ComponentSkuId, int Quantity);

public sealed record LifeStageRecommendations(string Basis, IReadOnlyList<string> LifeStages, IReadOnlyList<CatalogueItem> Items);

/// <summary>HO-SHP-001 section 12.4 / 8.10 -- the three recommendation blocks.</summary>
public sealed record RecommendationsViewModel(
    IReadOnlyList<CatalogueItem> FrequentlyBoughtTogether,
    IReadOnlyList<CatalogueItem> UpSell,
    LifeStageRecommendations LifeStage);

public sealed record BundleComponentDetailViewModel(string ProductName, string ProductSlug, string SkuLabel, int Quantity, string Availability);

public sealed record BundleDetailViewModel(IReadOnlyList<BundleComponentDetailViewModel> Components);

/// <summary>HO-SHP-001 section 8.12 -- snapshot source for Orders/POS (section 8.5): price at
/// <c>AsOf</c>, or null.</summary>
public sealed record SkuSaleFactViewModel(
    Guid SkuId, Guid ProductId, string ProductName, string SkuLabel, string SkuCode, string Kind, string ProductStatus,
    string SkuStatus, bool ListedOnStorefront, bool PrescriptionRequired, Guid TaxClassId, MoneyViewModel? Price,
    int? ShippingWeightGrams = null);

/// <summary>HO-SHP-001 section 8.12 -- D-2 (expiry tracking) and revenue by origin.</summary>
public sealed record SkuCatalogueFactViewModel(
    Guid SkuId, Guid ProductId, string Category, bool IsFood, bool IsExpiryTracked, string? CountryOfOriginCode,
    string SkuCode, string? Barcode);
