using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Entities;

/// <summary>
/// HO-XCT-001 section 8.3. One context's progress through an erasure request. Owned by <c>ErasureRequestAggregate</c>. Holds no personal
/// data: a context key, dates, a count and an error <b>code</b> (never a message or a value).
/// </summary>
public sealed class ErasureScope
{
    public const int MaxContextKeyLength = 50;
    public const int MaxErrorCodeLength = 100;
    public const int MaxHeldBasisLength = 100;

    public Guid Id { get; private set; }
    public string ContextKey { get; private set; } = default!;
    public ErasureScopeStatus Status { get; private set; } = default!;
    public DateOnly? HeldUntil { get; private set; }

    /// <summary>A short citation such as "D-9 invoice" (section 12.6 Q6), stored so the status read needs no event history.</summary>
    public string? HeldBasis { get; private set; }

    public int Attempts { get; private set; }
    public DateTimeOffset? LastAttemptOn { get; private set; }
    public string? LastErrorCode { get; private set; }
    public DateTimeOffset? CompletedOn { get; private set; }

    private ErasureScope() { }

    public ErasureScope(Guid id, string contextKey)
    {
        Id = id;
        ContextKey = contextKey;
        Status = ErasureScopeStatus.Pending;
    }

    internal void Complete(DateTimeOffset now)
    {
        Status = ErasureScopeStatus.Completed;
        HeldUntil = null;
        HeldBasis = null;
        LastErrorCode = null;
        LastAttemptOn = now;
        CompletedOn = now;
    }

    internal void Hold(DateOnly until, string basis, DateTimeOffset now)
    {
        Status = ErasureScopeStatus.Held;
        HeldUntil = until;
        HeldBasis = basis.Length > MaxHeldBasisLength ? basis[..MaxHeldBasisLength] : basis;
        LastErrorCode = null;
        LastAttemptOn = now;
    }

    internal void Fail(string errorCode, DateTimeOffset now)
    {
        Status = ErasureScopeStatus.Failing;
        Attempts++;
        LastAttemptOn = now;
        LastErrorCode = errorCode.Length > MaxErrorCodeLength ? errorCode[..MaxErrorCodeLength] : errorCode;
    }
}
