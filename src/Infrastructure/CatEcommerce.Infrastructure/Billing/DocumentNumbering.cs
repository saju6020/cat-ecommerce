using System.Collections.Concurrent;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>
/// HO-BIL-001 §8.7 (ADR-179, amended 2026-10-09 for callers inside a transaction). Gapless numbering with <b>no sequence</b>; since the 2026-10-10 amendment every issuer takes the series row lock first (see <see cref="SaveUnderSeriesLockAsync"/>): the series row is taken tracked and committed in the same <c>SaveChangesAsync</c>
/// as the document, guarded by <c>xmin</c>. Two issuers that read the same series collide on the
/// <c>UPDATE … WHERE xmin = …</c>; the loser reloads <b>only the series row</b>, takes a fresh number
/// and saves again, up to <see cref="BillingRules.MaxNumberingAttempts"/>.
///
/// An in-process gate per series key additionally serialises the issuers of one series inside this
/// host, so a burst (I-C3, 20 at once) resolves in at most two attempts each instead of a pile-up of
/// collisions. It is a throughput optimisation, never the correctness guard -- <c>xmin</c> is.
/// </summary>
public sealed class DocumentNumbering(
    CatEcommerceDbContext db, IUserContextProvider userContextProvider, ILogger<DocumentNumbering> logger) : IDocumentNumbering
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    public async Task<DocumentNumberSeriesAggregate> EnsureSeriesAsync(Guid branchId, string financialYear, BillingDocumentKind kind, CancellationToken ct = default)
    {
        await RefuseEarlierFinancialYearAsync(branchId, financialYear, kind, ct);

        // Inside a caller's transaction (IBillingTransactionScope) the series row is locked before it is read: see LockSeriesRowAsync.
        var locked = await LockSeriesRowAsync(branchId, financialYear, kind, ct);

        var existing = await LoadAsync(branchId, financialYear, kind, ct);
        if (existing is not null)
        {
            // The row was not visible when the lock ran (its creator had not committed) but is now: nothing is held yet, and the number read above may be stale by the
            // time we use it. Take the lock on the row that exists, then re-read it under the lock (a tracked entity is not refreshed by a query, hence the reload).
            if (!locked && await LockSeriesRowAsync(branchId, financialYear, kind, ct)) await db.Entry(existing).ReloadAsync(ct);
            return existing;
        }

        var created = new DocumentNumberSeriesAggregate(Guid.NewGuid());
        created.Open(branchId, financialYear, kind, userContextProvider.GetUserContext());

        await RawInsert.InsertIgnoringConflictAsync(db, created, ["branch_id", "financial_year", "document_kind"], ct);
        await LockSeriesRowAsync(branchId, financialYear, kind, ct);

        return await LoadAsync(branchId, financialYear, kind, ct)
            ?? throw new InvalidOperationException("The document series row could not be created.");
    }

    /// <summary>
    /// ADR-179 amendment (2026-10-09, ADR-215): when the caller has joined its own transaction through <see cref="IBillingTransactionScope"/> the retry above
    /// cannot recover (a failed statement aborts the whole transaction, and EF's savepoints are off). So, <b>inside an open transaction only</b>, the series row is
    /// locked <c>FOR UPDATE</c> before it is read: another issuer -- in a scope or not -- waits on the row, then reads the number the first one took. Outside a
    /// transaction nothing changes: still no lock and no explicit transaction. The lock is Billing's own and per (branch, financial year, kind); it is released
    /// with the caller's commit or rollback.
    /// </summary>
    private async Task<bool> LockSeriesRowAsync(Guid branchId, string financialYear, BillingDocumentKind kind, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) return true;

        // False when no row was visible to lock yet (a concurrent creator had not committed): the caller must lock again once it can see the row.
        var rows = await db.Database.SqlQuery<int>(
            $"SELECT 1 AS \"Value\" FROM billing.document_number_series WHERE branch_id = {branchId} AND financial_year = {financialYear} AND document_kind = {kind.Value} FOR UPDATE").ToListAsync(ct);
        return rows.Count > 0;
    }

    public async Task SaveWithRetryAsync(DocumentNumberSeriesAggregate series, Func<Task> save, Action<long> renumber, CancellationToken ct = default)
    {
        // Inside a caller's transaction the series row is already locked (see LockSeriesRowAsync); the in-process gate must not be held while waiting for that lock
        // (a gate holder waiting on a row lock and a row-lock holder waiting on the gate would deadlock), so it is skipped there.
        var inTransaction = db.Database.CurrentTransaction is not null;
        var gate = inTransaction ? null : Gates.GetOrAdd($"{series.BranchId:N}|{series.FinancialYear}|{series.DocumentKind.Value}", _ => new SemaphoreSlim(1, 1));
        if (gate is not null) await gate.WaitAsync(ct);
        try
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    if (inTransaction) await save();
                    else await SaveUnderSeriesLockAsync(series, save, renumber, ct);
                    return;
                }
                catch (Exception ex) when (attempt < BillingRules.MaxNumberingAttempts && IsCollision(ex, inTransaction))
                {
                    // F35: the series row lost its xmin check -- or the document's own number backstop tripped first, which is the
                    // same race seen from the other side (EF may send the document's INSERT before the series' UPDATE, and a unique
                    // index is checked immediately). Either way the whole save rolled back: reload ONLY the series row (every
                    // other tracked change stays pending), take a fresh number and save again. If the cause is not a stale read
                    // the reload gives the same number, the attempts run out, and the failure is rethrown -- loud, never mapped.
                    logger.LogInformation("Document number collision on series {SeriesId}; retry {Attempt} of {Max}.", series.Id, attempt, BillingRules.MaxNumberingAttempts);
                    await db.Entry(series).ReloadAsync(ct);
                    DetachSideEffectsOfTheFailedAttempt();

                    renumber(series.Take());
                    await Task.Delay(Random.Shared.Next(1, 8) * attempt, ct);
                }
            }
        }
        finally
        {
            gate?.Release();
        }
    }

    /// <summary>
    /// ADR-179 lock rule (amended 2026-10-10, backlog ORD 41): <b>every issuer takes the series row lock FIRST</b>, before it writes the document. A scoped issuer
    /// does so in <see cref="LockSeriesRowAsync"/> inside the caller's transaction; the unscoped issuer does so here, in its own short transaction around the
    /// save. Without it the unscoped save could INSERT its document (holding number N in the unique index) before it UPDATEd the series row, while a scoped
    /// issuer held the row lock and waited on that very index entry -- a lock cycle (PostgreSQL 40P01). With one order, waiting is all that ever happens.
    /// After the lock the series row is reloaded, so the number is the one committed, and if it moved the document is renumbered. A rolled-back attempt
    /// (deadlock, failure) rolls the series update back with it, so no number is consumed or skipped.
    /// </summary>
    private async Task SaveUnderSeriesLockAsync(DocumentNumberSeriesAggregate series, Func<Task> save, Action<long> renumber, CancellationToken ct)
    {
        // Savepoints off, as in BillingTransactionScope: the frozen-invoice trigger needs the invoice row to have been written by the top-level transaction.
        var savepoints = db.Database.AutoSavepointsEnabled;
        db.Database.AutoSavepointsEnabled = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                await LockSeriesRowAsync(series.BranchId, series.FinancialYear, series.DocumentKind, ct);

                var taken = series.NextSequence - 1;
                await db.Entry(series).ReloadAsync(ct);
                var fresh = series.Take();
                if (fresh != taken) renumber(fresh);

                await save();
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            db.Database.AutoSavepointsEnabled = savepoints;
        }
    }

    /// <summary>The unique indexes that back the numbers up (§12.1). A violation of one of these during the issuing save means
    /// two issuers drew the same number from a stale series read.</summary>
    private static readonly HashSet<string> NumberBackstops = ["ix_invoice_number", "ix_invoice_sequence", "ix_credit_note_number", "ix_credit_note_sequence"];

    private static bool IsCollision(Exception exception, bool inCallersTransaction)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            // A deadlock victim is retried like a collision -- but only in our own transaction: inside the caller's the whole transaction is aborted.
            if (!inCallersTransaction && current is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected })
                return true;

            if (current is DbUpdateConcurrencyException concurrency)
                return concurrency.Entries.Count > 0 && concurrency.Entries.All(e => e.Entity is DocumentNumberSeriesAggregate);

            if (current is DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg })
                return pg.ConstraintName is not null && NumberBackstops.Contains(pg.ConstraintName);
        }

        return false;
    }

    /// <summary>The failed attempt left its message request and its audit rows tracked as Added (the audit interceptor adds
    /// them during the save). The retry re-enlists both with the fresh number, so the stale ones are detached -- otherwise a
    /// message or an audit row would describe a number that never existed.</summary>
    private void DetachSideEffectsOfTheFailedAttempt()
    {
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added && e.Entity is NotificationRequestAggregate or AuditEntryAggregate).ToList())
            entry.State = EntityState.Detached;
    }

    private Task<DocumentNumberSeriesAggregate?> LoadAsync(Guid branchId, string financialYear, BillingDocumentKind kind, CancellationToken ct) =>
        db.DocumentNumberSeries.FirstOrDefaultAsync(s => s.BranchId == branchId && s.FinancialYear == financialYear && s.DocumentKind == kind, ct);

    /// <summary>U-C9: the setting that decides the financial year must not move numbering back into a
    /// year already closed at this branch -- the next number would re-enter a finished series.</summary>
    private async Task RefuseEarlierFinancialYearAsync(Guid branchId, string financialYear, BillingDocumentKind kind, CancellationToken ct)
    {
        var used = await db.DocumentNumberSeries.AsNoTracking()
            .Where(s => s.BranchId == branchId && s.DocumentKind == kind)
            .Select(s => s.FinancialYear)
            .ToListAsync(ct);

        var requested = FinancialYearRules.StartYear(financialYear);
        if (used.Any(y => FinancialYearRules.StartYear(y) > requested))
            throw new DomainRuleViolationException(BusinessViolationCodes.FinancialYearConfigurationChanged,
                "The financial year setting was changed after invoices were issued. Restore it before issuing.", nameof(financialYear));
    }
}
