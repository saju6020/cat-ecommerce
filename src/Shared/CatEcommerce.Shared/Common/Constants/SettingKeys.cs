namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-ADM-001 §10.2. Typed constants for every registry key, so a consumer never spells a key as a
/// raw string literal.
///
/// Gate fix 2026-09-27 (ARCHITECTURE MEDIUM, integrator-authorised, independently raised and made
/// on both <c>ho/NTF-001</c> and <c>ho/FIL-001</c>): made <c>static partial</c>, matching
/// <c>SettingDefinitions</c>/<c>ActionRoles</c>/<c>RecordClassification</c>'s own per-area-file
/// convention (ADR-341), instead of every area editing this one shared file. Each area's own keys
/// now live in its own <c>SettingKeys.[Area].cs</c> file -- the <c>Notifications</c>-prefixed
/// values in <c>SettingKeys.Notifications.cs</c>, HO-FIL-001's own in <c>SettingKeys.Files.cs</c> --
/// this file keeps only the keys that predate the split and have no area file of their own yet.
///
/// Gate fix 2026-09-27 (ARCHITECTURE second pass, LOW): the prior XML doc comment above named a
/// nonexistent member (<c>&lt;see cref="SettingKeys.Notifications"/&gt;</c> -- there is no
/// <c>Notifications</c> member on this class, only a same-named partial file), which compiles
/// silently because XML documentation is not generated in this solution. Corrected to plain
/// <c>&lt;c&gt;</c> text.
/// </summary>
public static partial class SettingKeys
{
    public const string BookingChangeCutoffHours = "booking.changeCutoffHours";
    public const string BookingSlotHoldMinutes = "booking.slotHoldMinutes";
    public const string BookingWaitlistClaimMinutes = "booking.waitlistClaimMinutes";
    public const string BoardingDepositPercent = "boarding.depositPercent";
    public const string InventoryExpiryAlertDays = "inventory.expiryAlertDays";
    public const string InventoryDefaultReorderPoint = "inventory.defaultReorderPoint";
    public const string ClinicalVaccinationDueSoonDays = "clinical.vaccinationDueSoonDays";
    public const string ClinicalDefaultParasiteIntervalDays = "clinical.defaultParasiteIntervalDays";
    public const string NotificationsQuietHours = "notifications.quietHours";

    public const string SecurityStaffIdleTimeoutMinutes = "security.staffIdleTimeoutMinutes";
    public const string CommerceCurrencyCode = "commerce.currencyCode";
}
