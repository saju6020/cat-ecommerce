namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Clinic Appointments' (HO-APT-001 §10.2) own registry keys. See ADR-341 for the
/// per-area partial pattern. <c>booking.changeCutoffHours</c>, <c>booking.slotHoldMinutes</c> and
/// <c>booking.waitlistClaimMinutes</c> already exist in <c>SettingKeys.cs</c> (added by
/// HO-ADM-001) and are consumed here, not redefined.</summary>
public static partial class SettingKeys
{
    public const string BookingSlotIntervalMinutes = "booking.slotIntervalMinutes";
    public const string BookingMinimumNoticeMinutes = "booking.minimumNoticeMinutes";
    public const string BookingHorizonDays = "booking.horizonDays";
    public const string BookingFirstReminderHours = "booking.firstReminderHours";
    public const string BookingSecondReminderHours = "booking.secondReminderHours";
    public const string ClinicNoShowGraceMinutes = "clinic.noShowGraceMinutes";
    public const string ClinicEarlyCheckInMinutes = "clinic.earlyCheckInMinutes";
}
