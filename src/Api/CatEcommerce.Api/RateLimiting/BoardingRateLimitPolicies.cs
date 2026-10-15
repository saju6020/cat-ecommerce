namespace CatEcommerce.Api.RateLimiting;

/// <summary>HO-BRD-001 section 12.3 -- the two boarding rate-limit policy names (limits come from <c>BoardingOptions</c>).</summary>
public static class BoardingRateLimitPolicies
{
    public const string Availability = "boarding-availability";

    public const string Booking = "boarding-booking";
}
