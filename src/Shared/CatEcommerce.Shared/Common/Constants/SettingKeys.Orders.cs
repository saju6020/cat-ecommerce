namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ORD-001 §10.2 -- Ordering's own registry keys (ADR-341 per-area partial). <c>commerce.currencyCode</c> already
/// exists and is consumed, not redefined.</summary>
public static partial class SettingKeys
{
    public const string OrdersCodEnabled = "orders.codEnabled";
    public const string OrdersCodMaxOrderValue = "orders.codMaxOrderValue";
    public const string OrdersBankTransferEnabled = "orders.bankTransferEnabled";
    public const string OrdersBankTransferWindowHours = "orders.bankTransferWindowHours";
    public const string OrdersPayOnCollectionEnabled = "orders.payOnCollectionEnabled";
    public const string OrdersPickupCollectionDays = "orders.pickupCollectionDays";
    public const string OrdersReturnWindowDays = "orders.returnWindowDays";
    public const string OrdersMaxQuantityPerLine = "orders.maxQuantityPerLine";
    public const string OrdersMaxCartLines = "orders.maxCartLines";
    public const string OrdersGuestCartRetentionDays = "orders.guestCartRetentionDays";
}
