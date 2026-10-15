namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-SRG-001 §10.2 -- the five keys Surgery appends to the registry (ADR-341).</summary>
public static partial class SettingKeys
{
    public const string SurgeryAdmissionLeadMinutes = "surgery.admissionLeadMinutes";
    public const string SurgeryEarlyAdmissionHours = "surgery.earlyAdmissionHours";
    public const string SurgeryPreOpReminderHours = "surgery.preOpReminderHours";
    public const string SurgeryWeightValidityDays = "surgery.weightValidityDays";
    public const string SurgeryFollowUpReminderLeadDays = "surgery.followUpReminderLeadDays";
}
