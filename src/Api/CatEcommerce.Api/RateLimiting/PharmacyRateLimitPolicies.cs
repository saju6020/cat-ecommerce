namespace CatEcommerce.Api.RateLimiting;

/// <summary>HO-PHM-001 §12.7. Kept in its own file, registered from <c>PharmacyModule</c>'s own <c>AddRateLimiter(...)</c> call -- additive alongside every other
/// area's (ADR-341). Limits come from <c>PharmacyOptions</c>, not literals, so a test factory can raise or tighten them.</summary>
public static class PharmacyRateLimitPolicies
{
    /// <summary>Every endpoint that accepts witness credentials (dispense, reverse, correct) -- 10 per minute per staff member (ADR-146): guessing a colleague's password
    /// at the dispensing counter is throttled even before the witness account's own lockout.</summary>
    public const string Witness = "pharmacy-witness";

    /// <summary>A customer's repeat request -- 10 per hour per customer.</summary>
    public const string RepeatRequest = "pharmacy-repeat-request";
}
