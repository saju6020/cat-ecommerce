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

    /// <summary>HO-EMR-001 §8.9 -- valid while the visit follow-up stamp equals the request stamp, the cat is
    /// alive and owned by the recipient, and the cat has no Booked appointment on or after today
    /// (<c>VisitFollowUpGuard</c>).</summary>
    public static readonly NotificationSubjectKind VisitFollowUp = new(5, nameof(VisitFollowUp));

    /// <summary>HO-IMP-001 §8.12 -- valid while the batch (<c>SubjectId</c>) is <c>Recalled</c> (<c>BatchRecallGuard</c>).</summary>
    public static readonly NotificationSubjectKind BatchRecall = new(6, nameof(BatchRecall));

    /// <summary>HO-PHM-001 §8.11 (ADR-150) -- valid while the prescription still has a supply to give, is inside its validity, the
    /// recomputed refill-due date equals the request's <c>SubjectDueOn</c>, no repeat request is open and the cat is alive and owned by the
    /// recipient (<c>PrescriptionRefillDueGuard</c>).</summary>
    public static readonly NotificationSubjectKind PrescriptionRefillDue = new(7, nameof(PrescriptionRefillDue));

    /// <summary>HO-SRG-001 §8.10 -- valid while the surgery is <c>Scheduled</c> and its <c>ScheduleStamp</c> equals the request's stamp,
    /// the cat is alive and owned by the recipient (<c>SurgeryPreOpGuard</c>).</summary>
    public static readonly NotificationSubjectKind SurgeryPreOp = new(8, nameof(SurgeryPreOp));

    /// <summary>HO-SRG-001 §8.10 -- valid while the follow-up (<c>SubjectRef</c>) is still <c>Pending</c>, the cat is alive and owned by
    /// the recipient (<c>SurgeryFollowUpGuard</c>).</summary>
    public static readonly NotificationSubjectKind SurgeryFollowUp = new(9, nameof(SurgeryFollowUp));
    public static readonly NotificationSubjectKind ScheduledReport = new(10, nameof(ScheduledReport));

    /// <summary>HO-BRD-001 section 8.19 -- valid while the boarding stay (SubjectId) is CheckedIn; the daily update uses it, so a stay checked out at 17:00 gets no 18:00 update.</summary>
    public static readonly NotificationSubjectKind BoardingStayInHouse = new(11, nameof(BoardingStayInHouse));
}
