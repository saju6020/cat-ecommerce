using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Retention;

/// <summary>One erasure scope that is due: the ledger (customer account) id and the context key.</summary>
public sealed record DueErasureScope(Guid CustomerAccountId, string ContextKey);

/// <summary>An anonymised account that has no erasure ledger yet (the self-heal case), with the instant it was anonymised.</summary>
public sealed record AccountWithoutLedger(Guid CustomerAccountId, DateTimeOffset? AnonymisedOn);

/// <summary>
/// HO-XCT-001 section 12.2 -- the retention worker read-only due-work queries. <c>AsNoTracking</c>, oldest first, bounded, and they return
/// <b>ids only</b>: the worker acts on each through a command in a fresh scope. Nothing here ever writes.
/// </summary>
public sealed class RetentionWorkQueue(CatEcommerceDbContext db)
{
    /// <summary>
    /// Pending scopes; Held scopes whose date has arrived (<paramref name="today"/> is the main-branch calendar date); Failing scopes past their
    /// back-off (<see cref="RetentionRules.ErasureBackoffMinutes"/>: 15, 60, 240, then 1440 minutes after the Nth failed attempt).
    /// </summary>
    public async Task<IReadOnlyList<DueErasureScope>> GetDueErasureScopesAsync(DateTimeOffset now, DateOnly today, int batchSize, CancellationToken ct)
    {
        var scopes = db.ErasureRequests.AsNoTracking().SelectMany(request => request.Scopes, (request, scope) => new { request.Id, request.RequestedOn, scope });

        var pendingOrHeld = await scopes
            .Where(x => x.scope.Status == ErasureScopeStatus.Pending
                        || (x.scope.Status == ErasureScopeStatus.Held && x.scope.HeldUntil != null && x.scope.HeldUntil <= today))
            .OrderBy(x => x.RequestedOn)
            .Select(x => new DueErasureScope(x.Id, x.scope.ContextKey))
            .Take(batchSize)
            .ToListAsync(ct);

        if (pendingOrHeld.Count >= batchSize) return pendingOrHeld;

        // Failing scopes: the back-off depends on the attempt count, so filter the (small) failing set in memory.
        var failing = await scopes
            .Where(x => x.scope.Status == ErasureScopeStatus.Failing)
            .OrderBy(x => x.scope.LastAttemptOn)
            .Select(x => new { x.Id, x.scope.ContextKey, x.scope.Attempts, x.scope.LastAttemptOn })
            .Take(batchSize * 4)
            .ToListAsync(ct);

        var dueFailing = failing
            .Where(x => x.LastAttemptOn is null || x.LastAttemptOn.Value.AddMinutes(RetentionRules.BackoffAfter(x.Attempts)) <= now)
            .Select(x => new DueErasureScope(x.Id, x.ContextKey));

        return pendingOrHeld.Concat(dueFailing).Take(batchSize).ToList();
    }

    /// <summary>Accounts that are <c>Anonymised</c> but have no ledger (ADR-302 self-heal): the ledger-opening handler failed after the commit.</summary>
    public async Task<IReadOnlyList<AccountWithoutLedger>> GetAnonymisedAccountsWithoutLedgerAsync(int batchSize, CancellationToken ct) =>
        await db.CustomerAccounts.AsNoTracking()
            .Where(account => account.Status == CustomerAccountStatus.Anonymised && !db.ErasureRequests.Any(ledger => ledger.Id == account.Id))
            .OrderBy(account => account.AnonymisedOn)
            .Select(account => new AccountWithoutLedger(account.Id, account.AnonymisedOn))
            .Take(batchSize)
            .ToListAsync(ct);

    /// <summary>The oldest manual run waiting to start (a <c>Requested</c> run), or <c>null</c>.</summary>
    public async Task<Guid?> GetRequestedRunIdAsync(CancellationToken ct) =>
        await db.RetentionRuns.AsNoTracking()
            .Where(run => run.Status == RetentionRunStatus.Requested)
            .OrderBy(run => run.RequestedOn)
            .Select(run => (Guid?)run.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>The local date on which the last <b>Scheduled</b> run that actually started did so, for "no run has started in that local date" (section 12.2).</summary>
    public async Task<DateTimeOffset?> GetLastScheduledRunStartAsync(CancellationToken ct) =>
        await db.RetentionRuns.AsNoTracking()
            .Where(run => run.Trigger == RetentionRunTrigger.Scheduled && run.StartedOn != null)
            .OrderByDescending(run => run.StartedOn)
            .Select(run => run.StartedOn)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Terminal notifications created before <paramref name="cutoff"/> whose content is still readable: never redacted, or redacted only for a secret (which keeps
    /// the destination). Anonymised and already retention-redacted rows are done. Oldest first (ADR-305).
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetNotificationIdsDueForContentRedactionAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct) =>
        await db.Notifications.AsNoTracking()
            .Where(n => n.CreatedOn < cutoff && n.AnonymisedOn == null
                && (n.RedactedReason == null || n.RedactedReason == NotificationRedactionReason.Secret)
                && (n.Status == NotificationStatus.Sent || n.Status == NotificationStatus.Undeliverable || n.Status == NotificationStatus.Expired
                    || n.Status == NotificationStatus.Cancelled || n.Status == NotificationStatus.InboxDelivered))
            .OrderBy(n => n.CreatedOn)
            .Take(batchSize)
            .Select(n => n.Id)
            .ToListAsync(ct);

    /// <summary>Processed notification requests older than <paramref name="cutoff"/> whose template variables (names, dates) were never redacted.</summary>
    public async Task<IReadOnlyList<Guid>> GetRequestIdsDueForVariableRedactionAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct) =>
        await db.NotificationRequests.AsNoTracking()
            .Where(r => r.RequestedOn < cutoff && r.ProcessedOn != null && r.VariablesRedactedOn == null)
            .OrderBy(r => r.RequestedOn)
            .Take(batchSize)
            .Select(r => r.Id)
            .ToListAsync(ct);
}
