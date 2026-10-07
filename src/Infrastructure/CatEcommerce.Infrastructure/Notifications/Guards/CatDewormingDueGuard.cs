using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>HO-NTF-001 §8.7. <c>Combined</c> counts as deworming (F25).</summary>
public sealed class CatDewormingDueGuard(IAggregateRootRepository<CatAggregate> catRepository) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.CatDewormingDue;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null || subjectDueOn is null) return false;

        var cat = await catRepository.GetByIdAsync(subjectId.Value);
        if (cat is null || cat.LifeStatus != CatLifeStatus.Alive || cat.OwnerId != recipientId) return false;

        var latest = cat.ParasiteTreatments
            .Where(t => t.TreatmentType == ParasiteTreatmentType.Deworming || t.TreatmentType == ParasiteTreatmentType.Combined)
            .OrderByDescending(t => t.AdministeredOn)
            .FirstOrDefault();

        return latest is not null && latest.NextDueOn == subjectDueOn.Value;
    }
}
