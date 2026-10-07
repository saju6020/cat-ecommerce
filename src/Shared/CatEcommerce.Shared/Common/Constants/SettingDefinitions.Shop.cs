using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SHP-001 §10.2 -- Shop's own settings, appended to the registry per ADR-341. A new
/// "Shop" group; <c>commerce.pricesIncludeTax</c> joins the existing "Commerce" group
/// (<c>commerce.currencyCode</c>, HO-ADM-001).</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> ShopEntries =
    [
        new SettingDefinition(
            SettingKeys.CommercePricesIncludeTax, "Commerce", SettingType.Boolean, null, null, null, "false",
            false, "Prices include tax", "Whether catalogue prices already include tax (SRS Q-5, FR-ORD-5). Shop only labels it; Orders/Billing compute."),

        new SettingDefinition(
            SettingKeys.ShopExpiryDisplayWindowDays, "Shop", SettingType.Integer, "0", "365", "days", "60",
            false, "Expiry display window", "Days before the allocatable batch's expiry that the storefront shows a short-dated notice (US-SHP-07 AC2). 0 = never shown."),

        new SettingDefinition(
            SettingKeys.ShopRecommendationKittenMaxAgeMonths, "Shop", SettingType.Integer, "6", "24", "months", "12",
            false, "Kitten age threshold", "Age below which a cat is treated as a Kitten for food recommendations (US-SHP-11)."),

        new SettingDefinition(
            SettingKeys.ShopRecommendationSeniorFromAgeYears, "Shop", SettingType.Integer, "5", "15", "years", "7",
            false, "Senior age threshold", "Age at or above which a cat is treated as Senior for food recommendations (US-SHP-11)."),

        new SettingDefinition(
            SettingKeys.ShopRecommendationWeightManagementFromKg, "Shop", SettingType.Decimal, "3.00", "12.00", "kg", "6.00",
            false, "Weight management threshold", "Weight at or above which a non-kitten cat is offered weight-management reasons (US-SHP-11)."),
    ];
}
