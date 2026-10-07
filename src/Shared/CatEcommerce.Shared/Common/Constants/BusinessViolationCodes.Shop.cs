namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SHP-001 §10.4 -- Shop's own violation codes, appended per ADR-341. Reused, not
/// duplicated: <c>PriceCannotBeBackdated</c>, <c>DuplicatePriceEffectiveDate</c>,
/// <c>TaxClassNotFound</c> (HO-ADM-001), <c>ActionNotPermitted</c> (HO-ACC-001).
///
/// <c>ProductVersionConflict</c> is one addition beyond §10.4's own list, needed for the
/// "stale ExpectedVersion -&gt; 409" contract (§12.4, I-A11). The platform's
/// <c>IAggregateRootRepository&lt;T&gt;.UpdateAsync(aggregate, expectedVersion, option)</c> overload
/// compares <c>aggregate.Version</c> against <c>expectedVersion</c> at CALL time -- but every
/// mutation method already calls <c>UpdateVersion()</c> before the handler reaches that call, so the
/// comparison is always against the POST-mutation version and the overload can never succeed for an
/// ordinary edit. The handler instead compares the loaded (pre-mutation) version itself and returns
/// this code -- the exact pattern HO-NTF-001's own <c>TemplateVersionConflict</c> already
/// established (<c>SaveNotificationTemplateCommandHandler</c> / <c>AdminNotificationTemplatesController</c>):
/// a handler-returned code the controller maps to 409, not a thrown
/// <c>DbUpdateConcurrencyException</c> (Application has no EF Core reference).</summary>
public static partial class BusinessViolationCodes
{
    public const string ProductVersionConflict = nameof(ProductVersionConflict);
    public const string ProductNotFound = nameof(ProductNotFound);
    public const string SkuNotFound = nameof(SkuNotFound);
    public const string InvalidSlug = nameof(InvalidSlug);
    public const string SlugAlreadyInUse = nameof(SlugAlreadyInUse);
    public const string SlugLockedAfterPublication = nameof(SlugLockedAfterPublication);
    public const string InvalidProductCategory = nameof(InvalidProductCategory);
    public const string CategoryLockedWhileActive = nameof(CategoryLockedWhileActive);
    public const string InvalidTag = nameof(InvalidTag);
    public const string TagLimitExceeded = nameof(TagLimitExceeded);
    public const string SkuLimitReached = nameof(SkuLimitReached);
    public const string VariantAttributesRequired = nameof(VariantAttributesRequired);
    public const string DuplicateVariant = nameof(DuplicateVariant);
    public const string SkuCodeAlreadyInUse = nameof(SkuCodeAlreadyInUse);
    public const string InvalidBarcode = nameof(InvalidBarcode);
    public const string BarcodeAlreadyInUse = nameof(BarcodeAlreadyInUse);
    public const string LastActiveSkuCannotBeDiscontinued = nameof(LastActiveSkuCannotBeDiscontinued);
    public const string ImageLimitReached = nameof(ImageLimitReached);
    public const string ImageAltTextRequired = nameof(ImageAltTextRequired);
    public const string InvalidImage = nameof(InvalidImage);
    public const string InvalidCountryCode = nameof(InvalidCountryCode);
    public const string FoodDetailsOnlyForFood = nameof(FoodDetailsOnlyForFood);
    public const string InvalidGuaranteedAnalysis = nameof(InvalidGuaranteedAnalysis);
    public const string PrescriptionOnlyForFood = nameof(PrescriptionOnlyForFood);
    public const string CountryOfOriginRequired = nameof(CountryOfOriginRequired);
    public const string ProductNotPublishable = nameof(ProductNotPublishable);
    public const string InvalidProductTransition = nameof(InvalidProductTransition);
    public const string PriceMustBePositive = nameof(PriceMustBePositive);
    public const string CurrencyMismatch = nameof(CurrencyMismatch);
    public const string OriginAlreadyExists = nameof(OriginAlreadyExists);
    public const string OriginNotFound = nameof(OriginNotFound);
    public const string InvalidStockLevel = nameof(InvalidStockLevel);
    public const string InvalidUnitsSold = nameof(InvalidUnitsSold);
    public const string InvalidStatsAsOf = nameof(InvalidStatsAsOf);
    public const string InvalidBundleComponent = nameof(InvalidBundleComponent);
    public const string BundleComponentLimitReached = nameof(BundleComponentLimitReached);
    public const string RelatedProductInvalid = nameof(RelatedProductInvalid);
    public const string ReviewNotPermitted = nameof(ReviewNotPermitted);
    public const string AlreadyReviewed = nameof(AlreadyReviewed);
    public const string ReviewNotFound = nameof(ReviewNotFound);
    public const string InvalidReviewTransition = nameof(InvalidReviewTransition);
    public const string ReviewRejectionReasonRequired = nameof(ReviewRejectionReasonRequired);
    public const string InvalidReviewText = nameof(InvalidReviewText);
    public const string WishlistLimitReached = nameof(WishlistLimitReached);
    public const string SkuAlreadyAvailable = nameof(SkuAlreadyAvailable);
    public const string StockAlertAlreadyRequested = nameof(StockAlertAlreadyRequested);
    public const string StockAlertNotFound = nameof(StockAlertNotFound);
    public const string PurchaseNotPermitted = nameof(PurchaseNotPermitted);
    public const string LocaleNotEnabled = nameof(LocaleNotEnabled);
    public const string DefaultLocaleNotTranslatable = nameof(DefaultLocaleNotTranslatable);
    public const string InvalidCatalogueFilter = nameof(InvalidCatalogueFilter);
    public const string InvalidStockAlertTransition = nameof(InvalidStockAlertTransition);
    public const string InvalidTranslationContent = nameof(InvalidTranslationContent);
    public const string InvalidProductName = nameof(InvalidProductName);
    public const string InvalidBrand = nameof(InvalidBrand);
    public const string InvalidProductDescription = nameof(InvalidProductDescription);
    public const string InvalidSkuCode = nameof(InvalidSkuCode);
    public const string PrescriptionProductInBundle = nameof(PrescriptionProductInBundle);
}
