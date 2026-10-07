namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-SHP-001 section 10.3 -- two-path messages shared by a handler's own pre-check and
/// <c>UniqueConstraintExceptionFilter</c>'s constraint-name mapping (section 12.5), so a sequential
/// request and a concurrent race that both violate the same rule return byte-for-byte the same
/// message.
/// </summary>
public static class ShopMessages
{
    public const string SlugAlreadyInUse = "This slug is already in use.";
    public const string SkuCodeAlreadyInUse = "This SKU code is already in use.";
    public const string BarcodeAlreadyInUse = "This barcode is already in use.";
    public const string DuplicatePriceEffectiveDate = "A price already exists with this effective date.";
    public const string OriginAlreadyExists = "An origin already exists for this country.";
    public const string AlreadyReviewed = "You have already reviewed this product.";
    public const string StockAlertAlreadyRequested = "You already have a pending alert for this item.";
}
