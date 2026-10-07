namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Owned by HO-NTF-001 §8.7/§10.1 -- what a notification request is about, for the send-time guard.
/// Not a partial type: HO-NTF-001's own instructions have every later handover that adds a subject
/// kind append a member directly to this file (e.g. <c>BatchRecall</c>, <c>PrescriptionRefillDue</c>,
/// <c>BoardingStayInHouse</c>, <c>AppointmentReminder</c>), each with an Amended row in HO-NTF-001
/// §1. Only the two members HO-NTF-001 itself defines are created in this contracts pass; later
/// areas append their own members when they are actually built, per docs/handover/CONTRACTS.md.
/// </summary>
public sealed class NotificationSubjectKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly NotificationSubjectKind CatVaccinationDue = new(1, nameof(CatVaccinationDue));
    public static readonly NotificationSubjectKind CatDewormingDue = new(2, nameof(CatDewormingDue));

    /// <summary>HO-APT-001 §8.9 -- valid while the appointment is <c>Booked</c> and its
    /// <c>ScheduleStamp</c> equals the request's <c>SubjectStamp</c> (<c>AppointmentReminderGuard</c>).
    /// A reschedule, move or cancellation changes or removes the stamp, so a stale reminder fails
    /// the guard and is never sent (ADR-074).</summary>
    public static readonly NotificationSubjectKind AppointmentReminder = new(3, nameof(AppointmentReminder));

    /// <summary>HO-SHP-001 §8.11 -- valid while the stock alert is <c>Notified</c>, its SKU Active and its
    /// product Active and listed (<c>StockAlertGuard</c>). Stock is not re-checked at send time.</summary>
    public static readonly NotificationSubjectKind StockAlert = new(4, nameof(StockAlert));
}
