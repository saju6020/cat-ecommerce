using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>HO-NTF-001 §8.7. Re-checked at expansion and again immediately before each delivery
/// attempt -- a guard reads; it never writes.</summary>
public sealed class CatVaccinationDueGuard(IAggregateRootRepository<CatAggregate> catRepository) : INotificationSubjectGuard
{
    public NotificationSubjectKind Kind => NotificationSubjectKind.CatVaccinationDue;

    public async Task<bool> IsStillValidAsync(Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (subjectId is null || subjectRef is null || subjectDueOn is null) return false;

        var cat = await catRepository.GetByIdAsync(subjectId.Value);
        if (cat is null || cat.LifeStatus != CatLifeStatus.Alive || cat.OwnerId != recipientId) return false;

        var latest = cat.Vaccinations
            .Where(v => v.VaccineId == subjectRef.Value)
            .OrderByDescending(v => v.AdministeredOn)
            .FirstOrDefault();

        return latest is not null && latest.NextDueOn == subjectDueOn.Value;
    }
}
