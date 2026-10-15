using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 §8.13 (ADR-202). Every method opens its own short-lived context from <see cref="IDbContextFactory{TContext}"/> and commits
/// immediately -- never the request's scoped context. The claim is a single <c>INSERT … ON CONFLICT DO NOTHING</c>, so exactly one of two
/// concurrent first requests wins; the loser reads the winner's row and decides replay / in progress / reused.
/// </summary>
public sealed class EfIdempotencyStore(IDbContextFactory<CatEcommerceDbContext> factory) : IIdempotencyStore
{
    private const int InProgress = (int)IdempotencyState.InProgress;
    private const int Completed = (int)IdempotencyState.Completed;

    public async Task<IdempotencyClaim> ClaimAsync(string scope, Guid key, string requestHash, DateTimeOffset now, TimeSpan staleAfter, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        // A row can vanish between the failed insert and the read (the winner released it after an exception): try again, which re-claims.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var id = Guid.NewGuid();
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO ops.idempotency_record (id, scope, key, request_hash, state, created_on)
                VALUES ({id}, {scope}, {key}, {requestHash}, {InProgress}, {now})
                ON CONFLICT (scope, key) DO NOTHING
                """, ct);

            if (inserted == 1) return new IdempotencyClaim(IdempotencyClaimOutcome.Claimed);

            var existing = await db.IdempotencyRecords.AsNoTracking()
                .Where(r => r.Scope == scope && r.Key == key)
                .Select(r => new { r.RequestHash, r.State, r.ResponseStatus, r.ResponseContentType, r.ResponseBody, r.CreatedOn })
                .SingleOrDefaultAsync(ct);

            if (existing is null) continue;

            if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                return new IdempotencyClaim(IdempotencyClaimOutcome.KeyReused);

            if (existing.State == IdempotencyState.Completed)
                return new IdempotencyClaim(IdempotencyClaimOutcome.Replay,
                    new IdempotentResponse(existing.ResponseStatus ?? StatusCodes200, existing.ResponseContentType, existing.ResponseBody));

            if (now - existing.CreatedOn < staleAfter)
                return new IdempotencyClaim(IdempotencyClaimOutcome.InProgress);

            // The process that claimed it died: take it over only if nobody else did first (compare-and-set on created_on).
            var taken = await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE ops.idempotency_record SET created_on = {now}
                WHERE scope = {scope} AND key = {key} AND state = {InProgress} AND created_on = {existing.CreatedOn}
                """, ct);

            return taken == 1
                ? new IdempotencyClaim(IdempotencyClaimOutcome.Claimed)
                : new IdempotencyClaim(IdempotencyClaimOutcome.InProgress);
        }

        return new IdempotencyClaim(IdempotencyClaimOutcome.InProgress);
    }

    public async Task CompleteAsync(string scope, Guid key, IdempotentResponse response, DateTimeOffset now, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE ops.idempotency_record
            SET state = {Completed}, response_status = {response.Status}, response_content_type = {response.ContentType},
                response_body = {response.Body}, completed_on = {now}
            WHERE scope = {scope} AND key = {key} AND state = {InProgress}
            """, ct);
    }

    public async Task ReleaseAsync(string scope, Guid key, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM ops.idempotency_record WHERE scope = {scope} AND key = {key} AND state = {InProgress}", ct);
    }

    public async Task<int> PurgeAsync(DateTimeOffset olderThan, int batchSize, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            DELETE FROM ops.idempotency_record
            WHERE id IN (SELECT id FROM ops.idempotency_record WHERE created_on < {olderThan} ORDER BY created_on LIMIT {batchSize})
            """, ct);
    }

    private const int StatusCodes200 = 200;
}
