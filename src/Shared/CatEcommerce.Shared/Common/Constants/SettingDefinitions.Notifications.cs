using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-NTF-001 §10.2 -- the two reminder-lead-day keys this handover owns. Its own
/// <c>notifications.quietHours</c> key is already registered in
/// <c>SettingDefinitions.Administration.cs</c> (built ahead of this handover, per that file's own
/// note); it is not duplicated here.
/// </summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> NotificationsEntries =
    [
        new SettingDefinition(
            SettingKeys.NotificationsVaccinationReminderLeadDays, "Notifications", SettingType.Integer, "1", "90", "days", "14",
            false, "Vaccination reminder lead time", "Days before a vaccination's next-due date a reminder is sent (HO-NTF, US-NTF-03)."),

        new SettingDefinition(
            SettingKeys.NotificationsDewormingReminderLeadDays, "Notifications", SettingType.Integer, "1", "90", "days", "7",
            false, "Deworming reminder lead time", "Days before a parasite treatment's next-due date a reminder is sent (HO-NTF, US-NTF-03)."),
    ];
}
