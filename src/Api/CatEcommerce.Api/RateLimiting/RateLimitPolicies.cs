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
}
