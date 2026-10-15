using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-PHM-001 §10.2 -- Pharmacy's six settings, appended to the registry per ADR-341.</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> PharmacyEntries =
    [
        new SettingDefinition(
            SettingKeys.PharmacyDoseWeightMaxAgeDays, "Pharmacy", SettingType.Integer, "1", "365", "days", "30",
            false, "Recent weight window", "A per-kg medicine can only be prescribed against a weight recorded within this many days (US-PHM-02)."),

        new SettingDefinition(
            SettingKeys.PharmacyDefaultDispenseWindowDays, "Pharmacy", SettingType.Integer, "1", "365", "days", "30",
            false, "Dispense window", "How long after it starts a non-repeatable prescription may still be dispensed unless the vet sets an earlier date."),

        new SettingDefinition(
            SettingKeys.PharmacyMaxRepeatValidityDays, "Pharmacy", SettingType.Integer, "7", "730", "days", "180",
            false, "Longest repeat validity", "The longest a repeatable prescription may stay valid from its start (US-PHM-06)."),

        new SettingDefinition(
            SettingKeys.PharmacyRefillReminderLeadDays, "Pharmacy", SettingType.Integer, "0", "30", "days", "3",
            false, "Refill reminder lead time", "How many days before a repeat is due the owner is reminded to request it (US-NTF-03). 0 = off."),

        new SettingDefinition(
            SettingKeys.PharmacyMaxDietAuthorisationDays, "Pharmacy", SettingType.Integer, "1", "730", "days", "365",
            false, "Longest diet authorisation", "The longest a prescription-diet authorisation may run (US-PHM-08)."),

        new SettingDefinition(
            SettingKeys.PharmacyDispensingReversalHours, "Pharmacy", SettingType.Integer, "0", "168", "hours", "24",
            true, "Dispensing reversal window", "How long after a supply was dispensed it can still be reversed (US-PHM-05). 0 = reversal off."),
    ];
}
