using CatEcommerce.Domain.Aggregates.Billing;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-BIL-001 §8.7 (ADR-179) -- gapless, non-reusable document numbers per
/// <c>(branch, financial year, document kind)</c>. The series row is taken <b>tracked</b> and committed
/// in the same <c>SaveChangesAsync</c> as the document, guarded by <c>xmin</c>; two concurrent issuers
/// collide on the series row and the loser retries, reloading only the series. No PostgreSQL sequence
/// (gaps on rollback), no lock, no explicit transaction.
/// </summary>
public interface IDocumentNumbering
{
    /// <summary>Creates the series row if absent (<c>INSERT … ON CONFLICT DO NOTHING</c> -- a row with
    /// <c>next_sequence = 1</c> creates no gap) and returns it tracked. Refuses with
    /// <c>FinancialYearConfigurationChanged</c> when <paramref name="financialYear"/> is earlier than the
    /// latest series already used at the branch for that kind (U-C9).</summary>
    Task<DocumentNumberSeriesAggregate> EnsureSeriesAsync(Guid branchId, string financialYear, BillingDocumentKind kind, CancellationToken ct = default);

    /// <summary>Runs <paramref name="save"/>; on a concurrency failure of the <b>series row only</b>
    /// (any other concurrency failure rethrows -- it is a real <c>409</c>) reloads the series, takes a
    /// fresh number, hands it to <paramref name="renumber"/>, and saves again, up to
    /// <c>BillingRules.MaxNumberingAttempts</c>.</summary>
    Task SaveWithRetryAsync(DocumentNumberSeriesAggregate series, Func<Task> save, Action<long> renumber, CancellationToken ct = default);
}
