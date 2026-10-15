namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-IMP-001 §10.2 -- the seven keys Inventory appends to the registry. (<c>inventory.defaultReorderPoint</c>,
/// which gains its consumer here, and <c>inventory.expiryAlertDays</c>, which does not -- OQ-17 -- are HO-ADM-001's.)</summary>
public static partial class SettingKeys
{
    public const string InventoryExpiryAlertFirstDays = "inventory.expiryAlertFirstDays";
    public const string InventoryExpiryAlertSecondDays = "inventory.expiryAlertSecondDays";
    public const string InventoryExpiryAlertThirdDays = "inventory.expiryAlertThirdDays";
    public const string InventoryAdjustmentApprovalThreshold = "inventory.adjustmentApprovalThreshold";
    public const string InventoryConsumptionWindowDays = "inventory.consumptionWindowDays";
    public const string InventoryReorderCoverDays = "inventory.reorderCoverDays";
    public const string InventoryDefaultLeadTimeDays = "inventory.defaultLeadTimeDays";
}
