namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SHP-001 §10.3 -- ProductAggregate's own field lengths and collection caps.</summary>
public static class ProductRules
{
    public const int MaxSkus = 100;
    public const int MaxImages = 40;
    public const int MaxTags = 20;
    public const int MaxHealthFocus = 6;

    public const int NameMaxLength = 200;
    public const int BrandMaxLength = 100;
    public const int DescriptionMaxLength = 5000;
    public const int SlugMaxLength = 120;
    public const int TagMaxLength = 40;
    public const int IngredientListMaxLength = 4000;
    public const int FeedingGuideMaxLength = 4000;
    public const int StorageInstructionsMaxLength = 1000;
    public const int ManufacturerMaxLength = 200;

    public const int SkuCodeMaxLength = 40;
    public const int BarcodeMinLength = 4;
    public const int BarcodeMaxLength = 32;
    public const int SizeOrWeightMaxLength = 50;
    public const int FlavourMaxLength = 80;
    public const int ColourMaxLength = 50;
    public const int MinPackQuantity = 1;
    public const int MaxPackQuantity = 999;
    /// <summary>Owner decision 2026-10-09: optional shipping weight per SKU, grams; 1 g to 100 kg.</summary>
    public const int MinShippingWeightGrams = 1;
    public const int MaxShippingWeightGrams = 100_000;

    public const int AltTextMaxLength = 200;
    public const long MaxImageSizeInBytes = 5_242_880;
}
