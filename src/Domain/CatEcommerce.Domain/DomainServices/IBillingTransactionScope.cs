namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BIL-001 §8.13 (Amended 2026-10-09, ADR-215 accepted by the owner) -- <b>the published entry point for a producer whose own change and Billing's
/// documents must stand or fall together</b>. Billing's invoice, payment, credit-note and refund commands each save through their own repository; a
/// producer (HO-ORD-001 today, HO-BRD-001 and HO-SUB-001 next) that needs "my change, my stock and the invoice, all or nothing" runs its steps inside
/// <see cref="RunAsync{T}"/> and dispatches Billing's published commands from within.
/// <para>
/// What Billing guarantees to a caller inside the scope, so the caller never reaches into Billing's internals:
/// </para>
/// <list type="bullet">
/// <item><b>One database transaction.</b> Every save made through the scoped context joins it; it commits once, at the end, and only when
/// <c>commitWhen</c> says the result is a success. A returned refusal rolls everything back, and so does any exception. A call made inside an
/// open transaction joins it.</item>
/// <item><b>Document numbering is safe under the scope.</b> Inside an open transaction <c>DocumentNumbering</c> reads the per-branch, per-year,
/// per-kind series row <c>FOR UPDATE</c> before it takes a number: a second issuer (inside a scope or not) waits on that row, so the number is
/// gapless and never collides, and the retry that ADR-179 relies on outside a transaction is not needed. The lock is Billing's -- the caller names
/// nothing and takes nothing.</item>
/// <item><b>Frozen-invoice trigger.</b> Billing's trigger accepts the lines of an invoice only when the invoice row was written by the current
/// top-level transaction (<c>xmin = pg_current_xact_id()</c>). EF Core's automatic savepoints are therefore switched off inside the scope. A failed
/// statement aborts the transaction, which rolls back as a whole -- the intended outcome.</item>
/// </list>
/// Implemented in Infrastructure/Billing.
/// </summary>
public interface IBillingTransactionScope
{
    /// <summary>Runs <paramref name="work"/> in one transaction (or joins the open one). Commits only when <paramref name="commitWhen"/> approves the result.</summary>
    Task<T> RunAsync<T>(Func<Task<T>> work, Func<T, bool> commitWhen, CancellationToken ct = default);

    /// <summary>Sends what the scoped context has enlisted so far to the database <b>inside the open transaction</b>, so a later read of this unit of work
    /// (a Billing command that selects the producer's charges, the next stock plan) sees it.</summary>
    Task FlushAsync(CancellationToken ct = default);
}
