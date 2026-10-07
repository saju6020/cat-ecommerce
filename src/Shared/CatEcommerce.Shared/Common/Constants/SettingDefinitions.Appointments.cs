using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-APT-001 §10.2 -- the seven settings this handover itself specifies.
/// <c>booking.changeCutoffHours</c>, <c>booking.slotHoldMinutes</c> and
/// <c>booking.waitlistClaimMinutes</c> were already registered by HO-ADM-001.</summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> AppointmentsEntries =
    [
        new SettingDefinition(
            SettingKeys.BookingSlotIntervalMinutes, "Booking", SettingType.Integer, "5", "60", "minutes", "15",
            true, "Slot grid interval", "The grid slots are offered on, aligned to local midnight (HO-APT, US-CLN-02)."),

        new SettingDefinition(
            SettingKeys.BookingMinimumNoticeMinutes, "Booking", SettingType.Integer, "0", "2880", "minutes", "60",
            true, "Minimum booking notice", "How far ahead an online booking must start (HO-APT, US-CLN-02)."),

        new SettingDefinition(
            SettingKeys.BookingHorizonDays, "Booking", SettingType.Integer, "1", "365", "days", "60",
            true, "Booking horizon", "How many days ahead online booking is offered (HO-APT, US-CLN-02)."),

        new SettingDefinition(
            SettingKeys.BookingFirstReminderHours, "Booking", SettingType.Integer, "0", "168", "hours", "24",
            false, "First reminder lead time", "Hours before the appointment the first reminder is sent; 0 disables it (HO-APT, US-CLN-06)."),

        new SettingDefinition(
            SettingKeys.BookingSecondReminderHours, "Booking", SettingType.Integer, "0", "48", "hours", "2",
            false, "Second reminder lead time", "Hours before the appointment the second reminder is sent; 0 disables it (HO-APT, US-CLN-06)."),

        new SettingDefinition(
            SettingKeys.ClinicNoShowGraceMinutes, "Clinic", SettingType.Integer, "0", "240", "minutes", "15",
            true, "No-show grace period", "Minutes after the start time before a no-show may be recorded (HO-APT, US-CLN-08)."),

        new SettingDefinition(
            SettingKeys.ClinicEarlyCheckInMinutes, "Clinic", SettingType.Integer, "0", "240", "minutes", "60",
            true, "Early check-in window", "Minutes before a just-after-midnight start a customer may still check in on the previous local day (HO-APT, US-CLN-10)."),
    ];
}
