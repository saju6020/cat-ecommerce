namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>HO-APT-001 §12.2/§12.7. Bound from configuration section "Clinic".</summary>
public sealed class ClinicOptions
{
    public const string SectionName = "Clinic";

    public ClinicWorkerOptions Worker { get; set; } = new();

    /// <summary>§12.3/§12.7 -- read from configuration (not a literal) so a test factory can raise
    /// these the same way <c>AccountsApiFactory.WithTightRateLimits()</c> lowers others; a shared
    /// IP partition (as TestServer gives every request) would otherwise trip the real 10/minute
    /// booking limit after a handful of concurrent requests.</summary>
    public RateLimitPolicyOptions Availability { get; set; } = new() { PermitLimit = 60, WindowMinutes = 1 };
    public RateLimitPolicyOptions Booking { get; set; } = new() { PermitLimit = 10, WindowMinutes = 1 };

    /// <summary>Backlog #27 -- customer reschedule, confirm, cancel and waitlist join (reschedule and
    /// join run the full slot finder). Per customer, 20 per minute: more than the hold limit, since
    /// a normal change is a handful of requests.</summary>
    public RateLimitPolicyOptions Change { get; set; } = new() { PermitLimit = 20, WindowMinutes = 1 };

    /// <summary>Backlog #28 -- the public <c>/api/booking/performers</c> lookup, per IP. Its own
    /// bucket so browsing performers cannot starve the availability endpoint's.</summary>
    public RateLimitPolicyOptions Performers { get; set; } = new() { PermitLimit = 60, WindowMinutes = 1 };
}

public sealed class ClinicWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
}
