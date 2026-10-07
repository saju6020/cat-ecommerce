namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Notifications (HO-NTF-001 §10.2) own registry keys. See ADR-341 for the per-area partial
/// pattern. Do not reorder or rename another area's keys.
/// </summary>
public static partial class SettingKeys
{
    public const string NotificationsVaccinationReminderLeadDays = "notifications.vaccinationReminderLeadDays";
    public const string NotificationsDewormingReminderLeadDays = "notifications.dewormingReminderLeadDays";
}
