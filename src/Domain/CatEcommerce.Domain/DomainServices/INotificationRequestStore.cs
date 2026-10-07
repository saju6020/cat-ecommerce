using CatEcommerce.Domain.Aggregates.Notification;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-NTF-001 §8.11/§11.4 C3 -- added beyond §8.13's own port list (recorded as a deviation in the
/// implementation report). Saves a new <see cref="NotificationRequestAggregate"/>, translating a
/// lost race on <c>ix_notification_request_dedup</c> into <c>false</c> instead of letting a
/// provider-specific exception (Npgsql's <c>PostgresException</c>) escape into the Application
/// layer, which references neither EF Core nor Npgsql. "Handled inside C3, never reaches the
/// exception filter" (§12.5) -- this port is how, without breaking the layering
/// `.ai/architecture.md` requires.
/// </summary>
public interface INotificationRequestStore
{
    /// <summary>Returns <c>false</c> only when <paramref name="request"/> carries a
    /// <c>DeduplicationKey</c> that another request won the race to save first -- the caller
    /// treats that as success (§8.11).</summary>
    Task<bool> TrySaveAsync(NotificationRequestAggregate request, CancellationToken ct = default);
}
