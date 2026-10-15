using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Prescription;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>
/// HO-PHM-001 §8.11 / ADR-150 -- the send-time guard of <c>pharmacy.refill-due</c> (subject <c>PrescriptionRefillDue</c>). The reminder is valid only while ALL of these hold, re-checked at
/// expansion and again immediately before each delivery attempt (HO-NTF-001 §8.7): the prescription is not cancelled, expired or completed; a supply is still left; today is not after
/// <c>ValidUntil</c>; the recomputed refill-due date still equals the request's <c>SubjectDueOn</c> (a repeat dispensed meanwhile moves it); no repeat request is already open; and
/// the cat is alive and owned by the recipient. Anything unreadable is "not valid": a reminder is never sent on doubt.
/// </summary>
public sealed class PrescriptionRefillDueGuard(
    IAggregateRootRepository<PrescriptionAggregate> prescriptionRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IPharmacyClock clock) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.PrescriptionRefillDue;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null || subjectDueOn is null) return false;

        var prescription = await prescriptionRepository.GetByIdAsync(subjectId.Value);
        if (prescription is null) return false;
        if (prescription.Status is var status && (status == PrescriptionStatus.Cancelled || status == PrescriptionStatus.Expired || status == PrescriptionStatus.Completed)) return false;
        if (prescription.SuppliesLeft <= 0 || prescription.OpenRepeatRequest is not null) return false;

        var today = await clock.LocalTodayAsync(prescription.BranchId, ct);
        if (today > prescription.ValidUntil) return false;

        var due = RefillScheduleCalculator.RefillDueOn(
            prescription.IsRepeatable, prescription.Status, prescription.SuppliesLeft, prescription.LastDispensedOn, prescription.DurationDays, prescription.ValidUntil);
        if (due != subjectDueOn) return false;

        var cat = await catRepository.GetByIdAsync(prescription.CatId);
        return cat is not null && cat.OwnerId == recipientId && cat.LifeStatus == CatLifeStatus.Alive;
    }
}
