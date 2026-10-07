using CatEcommerce.Domain.Aggregates.Notification;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-NTF-001 §8.6/§11.4 C5 -- added beyond §8.13's own port list (recorded as a deviation in the
/// implementation report). Saves a claimed <see cref="NotificationAggregate"/>, translating a lost
/// <c>xmin</c> optimistic-concurrency race (another worker claimed it first) into <c>false</c>
/// instead of letting <c>Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException</c> escape into
/// the Application layer, which references neither EF Core nor Npgsql. "A 409 here means another
/// worker has it -&gt; return success" (§11.4) -- this port is how, without breaking the layering
/// `.ai/architecture.md` requires.
/// </summary>
public interface INotificationClaimStore
{
    Task<bool> TrySaveClaimAsync(NotificationAggregate notification, CancellationToken ct = default);
}
