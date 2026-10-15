namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Consultation and Medical Records' (HO-EMR-001 §10.2) own registry key. See ADR-341.</summary>
public static partial class SettingKeys
{
    public const string ClinicalFollowUpReminderLeadDays = "clinical.followUpReminderLeadDays";

    // HO-BIL-001 Phase J (owner decision 2026-10-14): what one diagnostic test of each kind costs the owner; 0.00 until an administrator sets it.
    public const string ClinicalDiagnosticPriceBloodWork = "clinical.diagnosticPriceBloodWork";
    public const string ClinicalDiagnosticPriceUrinalysis = "clinical.diagnosticPriceUrinalysis";
    public const string ClinicalDiagnosticPriceFelvFivTest = "clinical.diagnosticPriceFelvFivTest";
    public const string ClinicalDiagnosticPriceImaging = "clinical.diagnosticPriceImaging";
    public const string ClinicalDiagnosticPriceOther = "clinical.diagnosticPriceOther";
}
