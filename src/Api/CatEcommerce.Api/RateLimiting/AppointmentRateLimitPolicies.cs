namespace CatEcommerce.Api.RateLimiting;

/// <summary>HO-APT-001 §12.3/§12.7. Kept in its own file, registered from <c>AppointmentsModule</c>'s
/// own <c>AddRateLimiter(...)</c> call -- additive alongside every other area's (ADR-341).</summary>
public static class AppointmentRateLimitPolicies
{
    /// <summary>The public availability endpoint -- 60 per minute per IP (§12.3).</summary>
    public const string Availability = "clinic-availability";

    /// <summary>Slot holds -- 10 per minute per IP (§12.7, HO-ACC-001 ADR-030's shape).</summary>
    public const string Booking = "clinic-booking";

    /// <summary>Customer reschedule, confirm, cancel and waitlist join -- per customer (backlog #27).</summary>
    public const string Change = "clinic-change";

    /// <summary>The public performer lookup -- per IP (backlog #28).</summary>
    public const string Performers = "clinic-performers";
}
