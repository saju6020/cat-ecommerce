using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Billing;

/// <summary>
/// HO-BIL-001 §8.13 (Amended 2026-10-09, ADR-215) -- <see cref="IBillingTransactionScope"/> over the scoped context's database transaction. The saves a
/// unit of work makes (the producer's own, and the ones Billing's commands make through the same scoped context) all join it. Commit happens once, at
/// the end, and only for a success. Automatic savepoints are off inside the scope (see the interface); numbering safety is <c>DocumentNumbering</c>'s.
/// </summary>
public sealed class BillingTransactionScope(CatEcommerceDbContext db) : IBillingTransactionScope
{
    public async Task<T> RunAsync<T>(Func<Task<T>> work, Func<T, bool> commitWhen, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null) return await work();

        var savepoints = db.Database.AutoSavepointsEnabled;
        db.Database.AutoSavepointsEnabled = false;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await work();
                if (commitWhen(result)) await transaction.CommitAsync(ct);
                else await transaction.RollbackAsync(CancellationToken.None);

                return result;
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

    public Task FlushAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
