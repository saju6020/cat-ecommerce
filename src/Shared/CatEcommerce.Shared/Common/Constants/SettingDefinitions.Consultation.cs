using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-EMR-001 §10.2 -- the follow-up reminder lead (OQ-16: 3 days, 09:00 branch-local), plus the five diagnostic test prices
/// HO-BIL-001 Phase J adds (owner decision 2026-10-14: "add a price, charge them", admin-set, default 0).</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> ConsultationEntries =
    [
        new SettingDefinition(
            SettingKeys.ClinicalFollowUpReminderLeadDays, "Clinical", SettingType.Integer, "0", "30", "days", "3",
            false, "Follow-up reminder lead time",
            "How many days before a follow-up's due date the owner's reminder is sent, at 09:00 branch-local time (HO-EMR-001, US-CLN-18)."),

        .. DiagnosticPriceEntries(),
    ];

    private static IEnumerable<SettingDefinition> DiagnosticPriceEntries() =>
    [
        DiagnosticPrice(SettingKeys.ClinicalDiagnosticPriceBloodWork, "blood work"),
        DiagnosticPrice(SettingKeys.ClinicalDiagnosticPriceUrinalysis, "urinalysis"),
        DiagnosticPrice(SettingKeys.ClinicalDiagnosticPriceFelvFivTest, "FeLV/FIV test"),
        DiagnosticPrice(SettingKeys.ClinicalDiagnosticPriceImaging, "imaging study"),
        DiagnosticPrice(SettingKeys.ClinicalDiagnosticPriceOther, "other diagnostic test"),
    ];

    private static SettingDefinition DiagnosticPrice(string key, string what) =>
        new(key, "Clinical", SettingType.Decimal, "0.00", "10000000.00", "currency", "0.00", false,
            $"Price of a {what}",
            $"What the owner is charged for each {what} ordered on a visit; the order records this price when it is made and the visit's bill carries one line per test (HO-BIL-001 Phase J). 0.00 charges nothing.");
}
