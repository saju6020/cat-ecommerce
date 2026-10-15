using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BIL-001 §10.2 -- the nine <c>billing.*</c> settings, a new group "Billing". Defaults are
/// the handover's open-question defaults (OQ-2, OQ-5, OQ-6, OQ-8). The financial-year start month must
/// not change once any invoice exists -- <c>DocumentNumbering</c> refuses (U-C9), the settings handler
/// is not edited.</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> BillingEntries =
    [
        new SettingDefinition(
            SettingKeys.BillingFinancialYearStartMonth, "Billing", SettingType.Integer, "1", "12", "month", "7",
            false, "Financial year start month", "The month the financial year starts (7 = July, Bangladesh). Must not be changed once any invoice has been issued (HO-BIL-001 OQ-2)."),

        new SettingDefinition(
            SettingKeys.BillingPaymentTermsDays, "Billing", SettingType.Integer, "0", "120", "days", "0",
            true, "Payment terms", "Days after issue that an invoice falls due; 0 = due on issue (HO-BIL-001 OQ-6)."),

        new SettingDefinition(
            SettingKeys.BillingPaymentApprovalThreshold, "Billing", SettingType.Decimal, "0.00", "100000000.00", "currency", "50000.00",
            true, "Payment approval threshold", "A recorded payment above this amount waits for a second, different staff member to approve it (HO-BIL-001 OQ-5, BR-17)."),

        new SettingDefinition(
            SettingKeys.BillingBankMatchDateToleranceDays, "Billing", SettingType.Integer, "0", "14", "days", "3",
            false, "Bank match date tolerance", "How many days a bank statement line's date may differ from a transfer payment's received date and still auto-match (HO-BIL-001 ADR-189)."),

        new SettingDefinition(
            SettingKeys.BillingCodRemittanceDueDays, "Billing", SettingType.Integer, "1", "60", "days", "7",
            false, "COD remittance due", "Days after collection that a courier's cash-on-delivery remittance is overdue (HO-BIL-001 US-BIL-06)."),

        new SettingDefinition(
            SettingKeys.BillingLoyaltyEnabled, "Billing", SettingType.Boolean, null, null, null, "false",
            false, "Loyalty points enabled", "Whether customers earn and redeem loyalty points (HO-BIL-001 US-BIL-10). Disabled by default."),

        new SettingDefinition(
            SettingKeys.BillingLoyaltySpendPerPoint, "Billing", SettingType.Decimal, "1.00", "100000.00", "currency", "100.00",
            false, "Spend per loyalty point", "Qualifying spend that earns one point (HO-BIL-001 OQ-8)."),

        new SettingDefinition(
            SettingKeys.BillingLoyaltyPointValue, "Billing", SettingType.Decimal, "0.01", "1000.00", "currency", "1.00",
            false, "Loyalty point value", "The invoice discount one redeemed point is worth (HO-BIL-001 OQ-8)."),

        new SettingDefinition(
            SettingKeys.BillingLoyaltyMinimumRedemptionPoints, "Billing", SettingType.Integer, "1", "100000", "points", "100",
            false, "Minimum redemption", "The fewest points that can be redeemed at once (HO-BIL-001 OQ-8)."),
    ];
}
