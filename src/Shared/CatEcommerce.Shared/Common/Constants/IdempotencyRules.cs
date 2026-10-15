namespace CatEcommerce.Shared.Common.Constants;

/// <summary>HO-ORD-001 §8.13 / §10.3 (ADR-202) -- HTTP idempotency at the edge.</summary>
public static class IdempotencyRules
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";
    public const int RetentionHours = 24;
    public const int MaxStoredBodyBytes = 65_536;
    public const int MaxScopeLength = 300;
}
