using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Notifications;

/// <summary>HO-NTF-001 §12.2 -- read-only due-work queries. It returns ids; it never writes.
/// <paramref name="timeProvider"/> -- HO-APT-001 §8.9 amendment: honours <c>SendAfter</c> on
/// <see cref="GetPendingRequestIdsAsync"/> without changing that method's own signature or its
/// caller (<c>NotificationWorkCycle</c>), which is outside HO-APT-001's own scope.</summary>
public sealed class NotificationWorkQueue(CatEcommerceDbContext db, TimeProvider timeProvider)
{
    /// <summary>
    /// Gate fix 2026-09-27 (SECURITY/CODE_REVIEW second pass, MEDIUM, "one bad row blocks the whole
    /// queue"). Used to order strictly by <c>RequestedOn</c> (oldest first), so a request that keeps
    /// failing to save (see <c>NotificationRequestAggregate.RecordFailedAttempt</c>) -- being the
    /// OLDEST Pending row -- was re-selected first on every single pass and could starve every
    /// healthy request behind it. Never-yet-attempted requests (<c>LastAttemptOn == null</c>) now
    /// always sort first; only once every one of those has been picked up (or the batch still has
    /// room) does a previously-failed request get its turn again, oldest-failure-first.
    ///
    /// HO-APT-001 §8.9 amendment: a request whose <c>SendAfter</c> is still in the future is not
    /// returned (U-G1) -- a reminder is not expanded before its lead time has actually elapsed.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetPendingRequestIdsAsync(int batchSize, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();

        return await db.NotificationRequests.AsNoTracking()
            .Where(r => r.Status == NotificationRequestStatus.Pending)
            .Where(r => r.SendAfter == null || r.SendAfter <= now)
            .OrderBy(r => r.LastAttemptOn == null ? 0 : 1)
            .ThenBy(r => r.LastAttemptOn)
            .ThenBy(r => r.RequestedOn)
            .Take(batchSize)
            .Select(r => r.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetDueNotificationIdsAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default) =>
        await db.Notifications.AsNoTracking()
            .Where(n =>
                (n.Status == NotificationStatus.Queued && (n.NotBefore == null || n.NotBefore <= now)) ||
                (n.Status == NotificationStatus.RetryPending && (n.NextAttemptOn == null || n.NextAttemptOn <= now)) ||
                (n.Status == NotificationStatus.Sending && n.LeaseExpiresOn != null && n.LeaseExpiresOn < now))
            .OrderBy(n => n.CreatedOn)
            .Take(batchSize)
            .Select(n => n.Id)
            .ToListAsync(ct);

    /// <summary>
    /// Gate fix 2026-09-27 (SECURITY HIGH / CODE_REVIEW BLOCKER, ADR-063). The missing
    /// <c>VariablesRedactedOn == null</c> filter meant the same oldest <paramref name="batchSize"/>
    /// already-redacted rows were selected on every pass forever once that many secret requests had
    /// expired -- the 51st-onward request's plaintext (a password-reset link) was never redacted.
    /// Mirrors <see cref="GetNotificationsDueForRedactionAsync"/>'s own <c>RedactedOn == null</c> filter.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetRequestsDueForVariableRedactionAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default) =>
        await db.NotificationRequests.AsNoTracking()
            .Where(r => r.ContainsSecret && r.VariablesRedactedOn == null && r.NotAfter != null && r.NotAfter < now)
            .OrderBy(r => r.NotAfter)
            .Take(batchSize)
            .Select(r => r.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetNotificationsDueForRedactionAsync(int batchSize, CancellationToken ct = default) =>
        await db.Notifications.AsNoTracking()
            .Where(n => n.ContainsSecret && n.RedactedOn == null &&
                (n.Status == NotificationStatus.Sent || n.Status == NotificationStatus.Expired ||
                 n.Status == NotificationStatus.Cancelled || n.Status == NotificationStatus.Undeliverable))
            .OrderBy(n => n.CreatedOn)
            .Take(batchSize)
            .Select(n => n.Id)
            .ToListAsync(ct);
}
