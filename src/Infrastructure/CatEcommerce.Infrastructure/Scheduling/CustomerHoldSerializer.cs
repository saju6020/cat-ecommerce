using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Scheduling;

/// <summary>HO-APT-001 §8.6 step 3 / ADR-070, SECURITY gate fix (2026-09-28). See
/// <see cref="ICustomerHoldSerializer"/> for the rule and the reasoning.
///
/// <c>pg_advisory_xact_lock</c> takes a <c>bigint</c> key; <c>hashtextextended(text, seed)</c> is
/// Postgres's own stable 64-bit text hash (unlike <see cref="Guid.GetHashCode"/>, which .NET does
/// not guarantee stable across versions or processes -- irrelevant for a lock that lives only for
/// one transaction, but avoiding it removes the question entirely). The customer id is bound as a
/// parameter (<c>ExecuteSqlInterpolatedAsync</c>), never string-concatenated.</summary>
public sealed class CustomerHoldSerializer(CatEcommerceDbContext db) : ICustomerHoldSerializer
{
    public async Task<T> RunSerializedAsync<T>(Guid customerId, Func<Task<T>> operation, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var key = customerId.ToString();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);

            var result = await operation();

            await transaction.CommitAsync(ct);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
