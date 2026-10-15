namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-BIL-001 §10.2 -- Billing's own registry keys (ADR-341 per-area partial).
/// <c>commerce.currencyCode</c> already exists (<see cref="CommerceCurrencyCode"/>) and is consumed,
/// not redefined.</summary>
public static partial class SettingKeys
{
    public const string BillingFinancialYearStartMonth = "billing.financialYearStartMonth";
    public const string BillingPaymentTermsDays = "billing.paymentTermsDays";
    public const string BillingPaymentApprovalThreshold = "billing.paymentApprovalThreshold";
    public const string BillingBankMatchDateToleranceDays = "billing.bankMatchDateToleranceDays";
    public const string BillingCodRemittanceDueDays = "billing.codRemittanceDueDays";
    public const string BillingLoyaltyEnabled = "billing.loyaltyEnabled";
    public const string BillingLoyaltySpendPerPoint = "billing.loyaltySpendPerPoint";
    public const string BillingLoyaltyPointValue = "billing.loyaltyPointValue";
    public const string BillingLoyaltyMinimumRedemptionPoints = "billing.loyaltyMinimumRedemptionPoints";
}
