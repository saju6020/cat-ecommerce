using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SRG-001 §10.2 -- Surgery's five settings, appended to the registry per ADR-341. No migration (values are rows).</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> SurgeryEntries =
    [
        new SettingDefinition(
            SettingKeys.SurgeryAdmissionLeadMinutes, "Surgery", SettingType.Integer, "0", "720", "minutes", "60",
            true, "Admission lead time", "How long before the planned start the owner is asked to bring the cat in (US-SRG-01)."),

        new SettingDefinition(
            SettingKeys.SurgeryEarlyAdmissionHours, "Surgery", SettingType.Integer, "0", "24", "hours", "12",
            true, "Early admission window", "How long before the planned start a cat may be admitted on the evening before (US-SRG-03)."),

        new SettingDefinition(
            SettingKeys.SurgeryPreOpReminderHours, "Surgery", SettingType.Integer, "0", "72", "hours", "18",
            true, "Pre-op reminder lead time", "Hours before the surgery the owner is reminded of the fasting instructions; 0 disables it (US-SRG-02)."),

        new SettingDefinition(
            SettingKeys.SurgeryWeightValidityDays, "Surgery", SettingType.Integer, "0", "7", "days", "1",
            false, "Pre-op weight validity", "A weight recorded this many days before the surgery date (or on it) completes the pre-op weight item (US-SRG-02)."),

        new SettingDefinition(
            SettingKeys.SurgeryFollowUpReminderLeadDays, "Surgery", SettingType.Integer, "0", "7", "days", "1",
            false, "Follow-up reminder lead time", "Days before a post-op follow-up is due that the owner is reminded (US-SRG-07)."),
    ];
}
