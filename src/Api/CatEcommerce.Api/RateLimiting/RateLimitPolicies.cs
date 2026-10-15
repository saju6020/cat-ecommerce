namespace CatEcommerce.Api.RateLimiting;

/// <summary>HO-ACC-001 §12.7 / ADR-030.</summary>
public static class RateLimitPolicies
{
    public const string CaptchaIssue = "captcha-issue";
    public const string Registration = "registration";
    public const string SignIn = "sign-in";
    public const string PasswordReset = "password-reset";
    public const string AccountSensitive = "account-sensitive";

    /// <summary>HO-ACC-001 §1 Amended (18), follow-up (2026-09-25). Partitioned by the signed-in
    /// customer id (falls back to IP only when no session resolved) -- see
    /// <c>AccountsModule.AddCustomerOrIpPartitionedPolicy</c>.</summary>
    public const string ProfileUpdate = "profile-update";

    /// <summary>HO-ORD-001 §12.3 -- cart writes (120/min per IP), checkout quote and placement (10/min per IP, NFR-SEC-6) and courier webhooks (600/min per IP).</summary>
    public const string Cart = "cart";
    public const string Checkout = "checkout";
    public const string CourierWebhook = "courier-webhook";

    /// <summary>HO-XCT-001 section 12.5 -- the public ping (120/min per IP) and anonymous web-vitals telemetry (30/min per IP).</summary>
    public const string Ping = "ping";
    public const string Telemetry = "telemetry";
}
