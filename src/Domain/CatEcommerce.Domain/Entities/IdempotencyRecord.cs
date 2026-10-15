namespace CatEcommerce.Domain.Entities;

/// <summary>The lifecycle of an <see cref="IdempotencyRecord"/> (ADR-202).</summary>
public enum IdempotencyState
{
    /// <summary>The first request with this key is still running.</summary>
    InProgress = 1,

    /// <summary>The first request finished; its status and body are stored for replay.</summary>
    Completed = 2,
}

/// <summary>
/// HO-ORD-001 §8.13 (ADR-202) -- one HTTP idempotency record: <c>(Scope, Key)</c> is unique, the request hash tells a replay from a key reused
/// for a different request, and the stored status and body are what a replay returns. A plain entity in schema <c>ops</c>, not an aggregate:
/// it is edge infrastructure written by <c>IdempotencyFilter</c> in its own short transaction (never the request's scoped context), reusable by
/// Billing and Boarding. No personal data: the body stored is the response of an endpoint that returns ids and totals only, capped at 64 KB.
/// </summary>
public sealed class IdempotencyRecord
{
    public Guid Id { get; set; }

    /// <summary>Caller kind + caller id + route template.</summary>
    public string Scope { get; set; } = default!;

    public Guid Key { get; set; }

    /// <summary>SHA-256 (hex) of method, path, query and the canonical JSON body.</summary>
    public string RequestHash { get; set; } = default!;

    public IdempotencyState State { get; set; }

    public int? ResponseStatus { get; set; }
    public string? ResponseContentType { get; set; }
    public byte[]? ResponseBody { get; set; }

    public DateTimeOffset CreatedOn { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }
}
