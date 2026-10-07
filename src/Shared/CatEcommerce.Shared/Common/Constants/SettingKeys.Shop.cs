namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Shop's (HO-SHP-001 §10.2) own registry keys. See ADR-341 for the per-area partial
/// pattern.</summary>
public static partial class SettingKeys
{
    public const string CommercePricesIncludeTax = "commerce.pricesIncludeTax";
    public const string ShopExpiryDisplayWindowDays = "shop.expiryDisplayWindowDays";
    public const string ShopRecommendationKittenMaxAgeMonths = "shop.recommendationKittenMaxAgeMonths";
    public const string ShopRecommendationSeniorFromAgeYears = "shop.recommendationSeniorFromAgeYears";
    public const string ShopRecommendationWeightManagementFromKg = "shop.recommendationWeightManagementFromKg";
}
