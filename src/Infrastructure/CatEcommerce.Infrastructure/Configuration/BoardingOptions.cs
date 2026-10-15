namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>HO-BRD-001 section 12.2/12.3 -- the <c>Boarding</c> configuration section: the worker and the two rate-limit policies. Limits are read from options at
/// request time (not at registration), so a test factory can raise them the way the other areas' factories do.</summary>
public sealed class BoardingOptions
{
    public const string SectionName = "Boarding";

    public BoardingWorkerOptions Worker { get; set; } = new();

    /// <summary>The public availability read, per IP.</summary>
    public RateLimitPolicyOptions Availability { get; set; } = new() { PermitLimit = 60, WindowMinutes = 1 };

    /// <summary>Enquiry creation and change (10 per minute per caller, section 12.3).</summary>
    public RateLimitPolicyOptions Booking { get; set; } = new() { PermitLimit = 10, WindowMinutes = 1 };
}

public sealed class BoardingWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
}
