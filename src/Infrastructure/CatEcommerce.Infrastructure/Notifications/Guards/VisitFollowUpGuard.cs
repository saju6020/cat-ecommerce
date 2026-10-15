using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Visit;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>
/// HO-EMR-001 §8.9 / ADR-131 -- the send-time guard of <c>visit.follow-up-due</c> (subject <c>VisitFollowUp</c>). The
/// reminder is valid only while <b>all</b> of these hold, re-checked at expansion and again immediately before each
/// delivery attempt (HO-NTF-001 §8.7):
/// <list type="bullet">
/// <item>the visit's follow-up still carries the request's stamp -- a changed or cleared follow-up fails;</item>
/// <item>the cat is alive and its <i>current</i> owner is the recipient;</item>
/// <item>the cat has no <c>Booked</c> appointment on or after the start of today in the visit's branch zone -- the owner
/// already booked, so the reminder stands down.</item>
/// </list>
/// Anything unreadable is "not valid": a reminder is never sent on doubt.
/// </summary>
public sealed class VisitFollowUpGuard(
    IAggregateRootRepository<VisitAggregate> visitRepository,
    IAggregateRootRepository<CatAggregate> catRepository,
    IAggregateRootRepository<AppointmentAggregate> appointmentRepository,
    IAggregateRootRepository<BranchAggregate> branchRepository,
    TimeProvider timeProvider) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.VisitFollowUp;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null || subjectStamp is null) return false;

        var visit = await visitRepository.GetByIdAsync(subjectId.Value);
        if (visit?.FollowUp is not { } followUp || !string.Equals(followUp.Stamp, subjectStamp, StringComparison.Ordinal)) return false;

        var cat = await catRepository.GetByIdAsync(visit.CatId);
        if (cat is null || cat.OwnerId != recipientId || cat.LifeStatus != CatLifeStatus.Alive) return false;

        var branch = await branchRepository.GetByIdAsync(visit.BranchId);
        var timeZoneId = branch?.TimeZoneId ?? "UTC";
        var today = BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), timeZoneId);
        var startOfToday = AppointmentSlotFinder.InstantOf(today, TimeOnly.MinValue, timeZoneId);

        var alreadyBooked = await appointmentRepository.ExistAsync(a =>
            a.CatId == cat.Id && a.Status == AppointmentStatus.Booked && a.StartsOn != null && a.StartsOn >= startOfToday);

        return !alreadyBooked;
    }
}
