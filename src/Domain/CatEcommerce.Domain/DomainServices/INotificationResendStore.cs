using CatEcommerce.Domain.Aggregates.Notification;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-NTF-001 §11.4 C7 -- gate fix 2026-09-27 (SECURITY LOW, added beyond §8.13's own port list,
/// recorded in HO-NTF-001 §1 Amended). Saves a new resend <see cref="NotificationAggregate"/>,
/// translating a lost race on <c>ix_notification_resend_of_id_ordinal</c> into <c>false</c> instead
/// of letting a provider-specific exception (Npgsql's <c>PostgresException</c>) escape into the
/// Application layer, which references neither EF Core nor Npgsql -- the same shape
/// <see cref="INotificationRequestStore"/> already uses for the dedup-key race.
/// </summary>
public interface INotificationResendStore
{
    /// <summary>Returns <c>false</c> only when another concurrent resend of the same chain won the
    /// race to claim this <c>ResendOrdinal</c> first -- the caller reports
    /// <c>ResendLimitReached</c>, the same violation a correctly-counted-but-too-late resend gets.</summary>
    Task<bool> TrySaveAsync(NotificationAggregate resend, CancellationToken ct = default);
}
