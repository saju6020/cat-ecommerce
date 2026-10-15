using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ORD-001 §10.2 -- the ten <c>orders.*</c> settings, a new group "Orders". Defaults are the SRS defaults for the open
/// questions Q-3 and Q-3b; changing them is data, not code. No migration (values are rows).</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> OrdersEntries =
    [
        new SettingDefinition(
            SettingKeys.OrdersCodEnabled, "Orders", SettingType.Boolean, null, null, null, "true",
            true, "Cash on delivery enabled", "Whether cash on delivery is offered at checkout (FR-ORD-8, SRS Q-3)."),

        new SettingDefinition(
            SettingKeys.OrdersCodMaxOrderValue, "Orders", SettingType.Decimal, "0.00", "1000000.00", "currency", "5000.00",
            true, "Cash on delivery ceiling", "Cash on delivery is hidden above this order total unless the delivery zone sets its own ceiling (BR-15, SRS Q-3)."),

        new SettingDefinition(
            SettingKeys.OrdersBankTransferEnabled, "Orders", SettingType.Boolean, null, null, null, "true",
            true, "Bank transfer enabled", "Whether bank transfer is offered at checkout (FR-ORD-8)."),

        new SettingDefinition(
            SettingKeys.OrdersBankTransferWindowHours, "Orders", SettingType.Integer, "1", "168", "hours", "48",
            false, "Bank transfer window", "Hours a bank-transfer order waits for payment before it expires and its stock is released (FR-ORD-22, BR-16, SRS Q-3b)."),

        new SettingDefinition(
            SettingKeys.OrdersPayOnCollectionEnabled, "Orders", SettingType.Boolean, null, null, null, "true",
            true, "Pay on collection enabled", "Whether a pickup order may be paid in store when it is collected (FR-ORD-8)."),

        new SettingDefinition(
            SettingKeys.OrdersPickupCollectionDays, "Orders", SettingType.Integer, "1", "60", "days", "7",
            true, "Pickup collection period", "Days a pickup order is held for collection once it is ready (BR-16)."),

        new SettingDefinition(
            SettingKeys.OrdersReturnWindowDays, "Orders", SettingType.Integer, "0", "90", "days", "7",
            false, "Return window", "Days after delivery that a customer may request a return (FR-ORD-15)."),

        new SettingDefinition(
            SettingKeys.OrdersMaxQuantityPerLine, "Orders", SettingType.Integer, "1", "999", "units", "20",
            false, "Maximum quantity per line", "The most units of one SKU a cart line or an order line may hold."),

        new SettingDefinition(
            SettingKeys.OrdersMaxCartLines, "Orders", SettingType.Integer, "1", "50", "lines", "50",
            false, "Maximum cart lines", "The most distinct lines a cart may hold (hard cap 50)."),

        new SettingDefinition(
            SettingKeys.OrdersGuestCartRetentionDays, "Orders", SettingType.Integer, "1", "180", "days", "30",
            false, "Guest cart retention", "Days an idle guest cart is kept before it is abandoned."),
    ];
}
