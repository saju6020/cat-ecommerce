namespace CatEcommerce.Domain.DomainServices;

/// <summary>How a <see cref="IIdempotencyStore.ClaimAsync"/> call ended (HO-ORD-001 §8.13 step 4).</summary>
public enum IdempotencyClaimOutcome
{
    /// <summary>First request with this key: it may run.</summary>
    Claimed,

    /// <summary>Same hash, already completed: replay <see cref="IdempotencyClaim.Replay"/>.</summary>
    Replay,

    /// <summary>Same hash, still running: <c>409 RequestInProgress</c>.</summary>
    InProgress,

    /// <summary>A different request used this key: <c>422 IdempotencyKeyReused</c>.</summary>
    KeyReused,
}

/// <summary>The stored response of a completed request.</summary>
public sealed record IdempotentResponse(int Status, string? ContentType, byte[]? Body);

public sealed record IdempotencyClaim(IdempotencyClaimOutcome Outcome, IdempotentResponse? Replay = null);

/// <summary>
/// HO-ORD-001 §8.13 (ADR-202) -- HTTP idempotency records, each operation committed <b>on its own</b> through a separate short-lived
/// <c>DbContext</c> (never the request's scoped context), so a claim survives the request's own rollback and a concurrent duplicate sees it
/// immediately. Reusable by Billing and Boarding. Implemented in Infrastructure.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary><c>INSERT … ON CONFLICT DO NOTHING</c> on <c>(scope, key)</c>; on conflict, decides replay / in progress / reused by the request hash.
    /// An <c>InProgress</c> record older than <paramref name="staleAfter"/> belongs to a process that died: it is taken over.</summary>
    Task<IdempotencyClaim> ClaimAsync(string scope, Guid key, string requestHash, DateTimeOffset now, TimeSpan staleAfter, CancellationToken ct);

    /// <summary>Stores the response and marks the record <c>Completed</c> (2xx and 4xx results).</summary>
    Task CompleteAsync(string scope, Guid key, IdempotentResponse response, DateTimeOffset now, CancellationToken ct);

    /// <summary>Deletes an <c>InProgress</c> record so a retry can run (an unhandled exception or a 5xx result).</summary>
    Task ReleaseAsync(string scope, Guid key, CancellationToken ct);

    /// <summary>Deletes records created before <paramref name="olderThan"/>; at most <paramref name="batchSize"/> per call. Returns the number deleted.</summary>
    Task<int> PurgeAsync(DateTimeOffset olderThan, int batchSize, CancellationToken ct);
}
