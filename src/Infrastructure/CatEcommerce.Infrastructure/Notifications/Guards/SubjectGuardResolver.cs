using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Infrastructure.Notifications.Guards;

/// <summary>HO-NTF-001 §8.7/§8.13 -- resolves the one guard for a subject kind. A request with no
/// subject is always valid (no kind to resolve).</summary>
public sealed class SubjectGuardResolver(IEnumerable<INotificationSubjectGuard> guards) : ISubjectGuardResolver
{
    public async Task<bool> IsStillValidAsync(NotificationSubjectKind? kind, Guid recipientId, Guid? subjectId, Guid? subjectRef, DateOnly? subjectDueOn, string? subjectStamp = null, CancellationToken ct = default)
    {
        if (kind is null) return true;

        var guard = guards.FirstOrDefault(g => g.Kind == kind);
        return guard is not null && await guard.IsStillValidAsync(recipientId, subjectId, subjectRef, subjectDueOn, subjectStamp, ct);
    }
}
