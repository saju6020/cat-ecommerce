namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.1, ADR-201, ADR-215 -- Ordering's unit of work: the order, its stock reservation, its charges and Billing's documents in <b>one database
/// transaction</b> (US-ORD-08 AC1, "order, batch reservation, confirmation and invoice happen atomically"). It is Billing's published
/// <see cref="IBillingTransactionScope"/> (HO-BIL-001 §8.13, Amended 2026-10-09 -- the transaction, the numbering safety and the savepoint rule are Billing's)
/// plus the one thing that is Ordering's own: a commit-time stock race (the last unit, an expiry between plan and commit) is translated into the business
/// refusal <c>StockNoLongerAvailable</c>. Ordering takes no lock and knows nothing of Billing's numbering. Implemented in Infrastructure.
/// </summary>
public interface ITransactionalScope
{
    Task<T> RunAsync<T>(Func<Task<T>> work, Func<T, bool> commitWhen, CancellationToken ct = default);

    /// <summary>Sends what the scoped context has enlisted so far to the database <b>inside the open transaction</b>, so a later read of this unit of work
    /// (the next stock plan, Billing selecting the order's charges) sees it.</summary>
    Task FlushAsync(CancellationToken ct = default);
}
