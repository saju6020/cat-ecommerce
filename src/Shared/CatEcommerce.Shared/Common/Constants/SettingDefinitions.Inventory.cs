using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-IMP-001 §10.2 -- Inventory's seven settings, appended to the registry per ADR-341.</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> InventoryEntries =
    [
        new SettingDefinition(
            SettingKeys.InventoryExpiryAlertFirstDays, "Inventory", SettingType.Integer, "0", "730", "days", "90",
            false, "First expiry alert", "Days before a batch's expiry that the first alert is raised (US-IMP-10). 0 = off."),

        new SettingDefinition(
            SettingKeys.InventoryExpiryAlertSecondDays, "Inventory", SettingType.Integer, "0", "730", "days", "60",
            false, "Second expiry alert", "Days before a batch's expiry that the second alert is raised. 0 = off."),

        new SettingDefinition(
            SettingKeys.InventoryExpiryAlertThirdDays, "Inventory", SettingType.Integer, "0", "730", "days", "30",
            false, "Third expiry alert", "Days before a batch's expiry that the third alert is raised. 0 = off."),

        new SettingDefinition(
            SettingKeys.InventoryAdjustmentApprovalThreshold, "Inventory", SettingType.Decimal, "0", "10000000", "BDT", "5000.00",
            true, "Adjustment approval threshold", "A stock adjustment or count worth more than this needs approval by a second person (US-IMP-13, ADR-107)."),

        new SettingDefinition(
            SettingKeys.InventoryConsumptionWindowDays, "Inventory", SettingType.Integer, "7", "365", "days", "90",
            false, "Consumption window", "Days of consumption history behind a reorder suggestion (US-IMP-12)."),

        new SettingDefinition(
            SettingKeys.InventoryReorderCoverDays, "Inventory", SettingType.Integer, "0", "180", "days", "14",
            true, "Reorder cover", "Days of stock a reorder suggestion covers beyond the supplier lead time (US-IMP-12)."),

        new SettingDefinition(
            SettingKeys.InventoryDefaultLeadTimeDays, "Inventory", SettingType.Integer, "0", "365", "days", "14",
            false, "Default lead time", "Supplier lead time assumed when a reorder has no preferred supplier (US-IMP-12)."),
    ];
}
