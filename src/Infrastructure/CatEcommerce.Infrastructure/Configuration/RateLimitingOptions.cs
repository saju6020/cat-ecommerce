namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>Bound from appsettings.json section "RateLimiting" -- HO-ACC-001 §12.7.</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitPolicyOptions CaptchaIssue { get; set; } = new() { PermitLimit = 20, WindowMinutes = 10 };
    public RateLimitPolicyOptions Registration { get; set; } = new() { PermitLimit = 5, WindowMinutes = 10 };
    public RateLimitPolicyOptions SignIn { get; set; } = new() { PermitLimit = 10, WindowMinutes = 1 };
    public RateLimitPolicyOptions PasswordReset { get; set; } = new() { PermitLimit = 5, WindowMinutes = 60 };
    public RateLimitPolicyOptions AccountSensitive { get; set; } = new() { PermitLimit = 5, WindowMinutes = 60 };

    /// <summary>HO-ACC-001 §1 Amended (18), follow-up (2026-09-25): the profile-update/email
    /// step-up path (<c>PUT /api/customers/me</c>) -- partitioned by the signed-in customer id, not
    /// IP, so it no longer shares a bucket with <see cref="AccountSensitive"/> (deletion, export)
    /// and customers behind shared NAT/Wi-Fi do not block each other.</summary>
    public RateLimitPolicyOptions ProfileUpdate { get; set; } = new() { PermitLimit = 5, WindowMinutes = 60 };

    /// <summary>HO-ORD-001 §12.3: cart writes, the checkout quote and placement, and inbound courier webhooks -- all IP-partitioned.</summary>
    public RateLimitPolicyOptions Cart { get; set; } = new() { PermitLimit = 120, WindowMinutes = 1 };
    public RateLimitPolicyOptions Checkout { get; set; } = new() { PermitLimit = 10, WindowMinutes = 1 };
    public RateLimitPolicyOptions CourierWebhook { get; set; } = new() { PermitLimit = 600, WindowMinutes = 1 };
}

public sealed class RateLimitPolicyOptions
{
    public int PermitLimit { get; set; }
    public int WindowMinutes { get; set; }
}
