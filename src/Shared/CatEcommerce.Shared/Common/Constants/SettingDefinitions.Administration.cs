using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ADM-001 §10.2 -- the eleven settings this handover itself specifies. Gate fix 2026-09-26:
/// the two <c>notifications.*</c> reminder-lead-day keys previously built here on the strength of
/// HO-NTF-001's §1 Amended row have been removed again -- a key, type, bound and default named in
/// another area's own Amended row is still that area's setting to add, in its own change, not this
/// handover's to pre-build on its behalf (the same reason HO-APT-001's "seven settings", named only
/// as a count, were correctly never built here). HO-NTF-001 must add
/// <c>notifications.vaccinationReminderLeadDays</c> and <c>notifications.dewormingReminderLeadDays</c>
/// itself when it lands; see its own Amended row here for the key/default it already committed to.
/// </summary>
public static partial class SettingDefinitions
{
    private static readonly IReadOnlyList<SettingDefinition> AdministrationEntries =
    [
        new SettingDefinition(
            SettingKeys.BookingChangeCutoffHours, "Booking", SettingType.Integer, "0", "72", "hours", "4",
            true, "Booking change cutoff", "How many hours before an appointment a customer may still change or cancel it (HO-APT, FR-CLN-5)."),

        new SettingDefinition(
            SettingKeys.BookingSlotHoldMinutes, "Booking", SettingType.Integer, "2", "30", "minutes", "10",
            true, "Slot hold duration", "How long a selected slot is held for a customer completing checkout (HO-APT, FR-CLN-4)."),

        new SettingDefinition(
            SettingKeys.BookingWaitlistClaimMinutes, "Booking", SettingType.Integer, "5", "240", "minutes", "30",
            true, "Waitlist claim window", "How long a customer has to claim a released slot from the waitlist (HO-APT, FR-CLN-10)."),

        new SettingDefinition(
            SettingKeys.BoardingDepositPercent, "Boarding", SettingType.Decimal, "0", "100", "%", "30.00",
            true, "Boarding deposit", "Deposit percentage required to confirm a boarding reservation (HO-BRD, FR-BRD-7)."),

        new SettingDefinition(
            SettingKeys.InventoryExpiryAlertDays, "Inventory", SettingType.Integer, "1", "365", "days", "60",
            true, "Expiry alert window", "Days before expiry a batch is flagged for attention (HO-IMP)."),

        new SettingDefinition(
            SettingKeys.InventoryDefaultReorderPoint, "Inventory", SettingType.Integer, "0", "100000", "units", "10",
            true, "Default reorder point", "Default reorder point for a new SKU location (HO-IMP)."),

        new SettingDefinition(
            SettingKeys.ClinicalVaccinationDueSoonDays, "Clinical", SettingType.Integer, "1", "180", "days", "30",
            false, "Vaccination due-soon window", "Days before a vaccination's next-due date it is shown as DueSoon (HO-CAT, rewired here)."),

        new SettingDefinition(
            SettingKeys.ClinicalDefaultParasiteIntervalDays, "Clinical", SettingType.Integer, "7", "365", "days", "90",
            false, "Default parasite treatment interval", "Default interval between parasite treatments (HO-CAT; no reader yet)."),

        new SettingDefinition(
            SettingKeys.NotificationsQuietHours, "Notifications", SettingType.TimeRange, null, null, null, "21:00-08:00",
            true, "Quiet hours", "Local time window during which non-urgent notifications are held (HO-NTF, FR-NTF-5)."),

        new SettingDefinition(
            SettingKeys.SecurityStaffIdleTimeoutMinutes, "Security", SettingType.Integer, "5", "240", "minutes", "30",
            false, "Staff idle timeout", "Minutes of staff inactivity before a session is treated as idle (HO-ACC, rewired here)."),

        new SettingDefinition(
            SettingKeys.CommerceCurrencyCode, "Commerce", SettingType.CurrencyCode, null, null, null, "BDT",
            false, "Currency code", "The ISO-4217 currency code money is denominated in (ADR-054)."),
    ];
}
