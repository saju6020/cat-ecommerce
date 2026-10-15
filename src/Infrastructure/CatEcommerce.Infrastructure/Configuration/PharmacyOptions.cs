namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>
/// HO-PHM-001 §12.2 / §12.7. Bound from configuration section "Pharmacy": the worker (<c>Pharmacy:Worker</c>) and the two rate-limit policies. The limits
/// are read from configuration, not literals, so a test factory can raise them (a shared TestServer partition would otherwise throttle unrelated tests)
/// or tighten them to prove they apply.
/// </summary>
public sealed class PharmacyOptions
{
    public const string SectionName = "Pharmacy";

    public PharmacyWorkerOptions Worker { get; set; } = new();

    /// <summary>Every endpoint that accepts witness credentials -- 10 per minute per staff session (ADR-146).</summary>
    public RateLimitPolicyOptions Witness { get; set; } = new() { PermitLimit = 10, WindowMinutes = 1 };

    /// <summary>The customer's repeat request -- 10 per hour per customer (§12.4).</summary>
    public RateLimitPolicyOptions RepeatRequest { get; set; } = new() { PermitLimit = 10, WindowMinutes = 60 };
}

public sealed class PharmacyWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
    public int RefillSweepIntervalMinutes { get; set; } = 60;
}
