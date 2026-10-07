using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>HO-NTF-001 §8.5 -- read-only over <c>account.*</c>. Implemented in Infrastructure
/// (<c>AccountRecipientDirectory</c>, Scoped, <c>AsNoTracking</c>).</summary>
public interface INotificationRecipientDirectory
{
    Task<RecipientProfile?> ResolveAsync(NotificationRecipientKind kind, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gate fix 2026-09-27 (SECURITY/ARCHITECTURE/CODE_REVIEW HIGH). The transitive closure of
    /// customer ids merged into <paramref name="survivorId"/> (the reverse of the merge walk
    /// <see cref="ResolveAsync"/> already does forward), bounded the same way. A merged-away
    /// customer cannot itself be anonymised (the aggregate requires <c>Active</c>), so its own
    /// external-channel notification rows -- which keep the pre-merge <c>RecipientId</c> as
    /// history, §11.6 -- would otherwise never be reached when the survivor is anonymised.
    /// </summary>
    Task<IReadOnlyList<Guid>> FindMergedDuplicatesAsync(Guid survivorId, CancellationToken ct = default);
}
