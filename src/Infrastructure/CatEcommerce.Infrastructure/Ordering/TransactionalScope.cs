using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Exceptions;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 ADR-215 -- <see cref="ITransactionalScope"/>: Billing's published <see cref="IBillingTransactionScope"/> (the transaction, the numbering
/// safety, the savepoint rule) plus Ordering's translation of a commit-time stock race.
/// <para>
/// A 23514 from the stock projection's CHECK (the last unit went to another caller), or from the batch-usability triggers (the batch expired or was
/// recalled between plan and commit), is translated here into the business refusal <c>StockNoLongerAvailable</c> -- the same race Inventory's own endpoints
/// answer with <c>409</c>. Every other exception rolls back and propagates unchanged.
/// </para>
/// </summary>
public sealed class TransactionalScope(IBillingTransactionScope billing) : ITransactionalScope
{
    private static readonly HashSet<string> StockRaceConstraints = ["ck_stock_position_balance", "tg_stock_batch_expired", "tg_stock_batch_not_usable"];

    public Task<T> RunAsync<T>(Func<Task<T>> work, Func<T, bool> commitWhen, CancellationToken ct = default) =>
        billing.RunAsync(() => Translate(work), commitWhen, ct);

    public async Task FlushAsync(CancellationToken ct = default)
    {
        try
        {
            await billing.FlushAsync(ct);
        }
        catch (DbUpdateException ex) when (IsStockRace(ex))
        {
            throw StockRace();
        }
    }

    private static async Task<T> Translate<T>(Func<Task<T>> work)
    {
        try
        {
            return await work();
        }
        catch (DbUpdateException ex) when (IsStockRace(ex))
        {
            throw StockRace();
        }
    }

    /// <summary>True when the exception is a stock-race check violation (public so the deterministic translation test can use it).</summary>
    public static bool IsStockRace(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.CheckViolation, ConstraintName: { } name } && StockRaceConstraints.Contains(name);

    public static DomainRuleViolationException StockRace() =>
        new(BusinessViolationCodes.StockNoLongerAvailable, OrderMessages.StockNoLongerAvailable, "Lines");
}
