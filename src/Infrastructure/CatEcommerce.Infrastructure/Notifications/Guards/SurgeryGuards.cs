using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Surgery;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>
/// HO-SRG-001 §8.10 (ADR-169) -- valid while the surgery (<c>subjectId</c>) is <see cref="SurgeryStatus.Scheduled"/>, its <c>ScheduleStamp</c> equals the request's own
/// <c>subjectStamp</c>, the cat is alive and still the recipient's. A reschedule changes the stamp and a cancellation or admission changes the status, so a stale pre-op reminder
/// fails here and is never sent. Re-checked at expansion and again before each delivery attempt (HO-NTF-001 §8.7).
/// </summary>
public sealed class SurgeryPreOpGuard(IAggregateRootRepository<SurgeryAggregate> surgeryRepository, IAggregateRootRepository<CatAggregate> catRepository) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.SurgeryPreOp;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null || subjectStamp is null) return false;

        var surgery = await surgeryRepository.GetByIdAsync(subjectId.Value);
        if (surgery is null || surgery.CustomerId != recipientId) return false;
        if (surgery.Status != SurgeryStatus.Scheduled || !string.Equals(surgery.ScheduleStamp, subjectStamp, StringComparison.Ordinal)) return false;

        return await CatIsAliveAndOwnedAsync(catRepository, surgery.CatId, recipientId);
    }

    internal static async Task<bool> CatIsAliveAndOwnedAsync(IAggregateRootRepository<CatAggregate> catRepository, Guid catId, Guid ownerId)
    {
        var cat = await catRepository.GetByIdAsync(catId);
        return cat is not null && cat.LifeStatus == CatLifeStatus.Alive && cat.OwnerId == ownerId;
    }
}

/// <summary>HO-SRG-001 §8.10 -- valid while the follow-up (<c>subjectRef</c>) of the surgery (<c>subjectId</c>) is still <see cref="FollowUpStatus.Pending"/>, the cat is alive and
/// still the recipient's. Completing or cancelling the follow-up makes its pending reminder fail here.</summary>
public sealed class SurgeryFollowUpGuard(IAggregateRootRepository<SurgeryAggregate> surgeryRepository, IAggregateRootRepository<CatAggregate> catRepository) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.SurgeryFollowUp;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null || subjectRef is null) return false;

        var surgery = await surgeryRepository.GetByIdAsync(subjectId.Value);
        if (surgery is null || surgery.CustomerId != recipientId) return false;
        if (!surgery.FollowUps.Any(f => f.Id == subjectRef.Value && f.Status == FollowUpStatus.Pending)) return false;

        return await SurgeryPreOpGuard.CatIsAliveAndOwnedAsync(catRepository, surgery.CatId, recipientId);
    }
}
