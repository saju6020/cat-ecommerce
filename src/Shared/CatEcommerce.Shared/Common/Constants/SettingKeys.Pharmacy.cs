namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-PHM-001 §10.2 -- the six keys Pharmacy appends to the registry (ADR-341).</summary>
public static partial class SettingKeys
{
    public const string PharmacyDoseWeightMaxAgeDays = "pharmacy.doseWeightMaxAgeDays";
    public const string PharmacyDefaultDispenseWindowDays = "pharmacy.defaultDispenseWindowDays";
    public const string PharmacyMaxRepeatValidityDays = "pharmacy.maxRepeatValidityDays";
    public const string PharmacyRefillReminderLeadDays = "pharmacy.refillReminderLeadDays";
    public const string PharmacyMaxDietAuthorisationDays = "pharmacy.maxDietAuthorisationDays";
    public const string PharmacyDispensingReversalHours = "pharmacy.dispensingReversalHours";
}
