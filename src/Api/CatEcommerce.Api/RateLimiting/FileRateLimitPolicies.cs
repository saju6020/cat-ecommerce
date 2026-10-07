namespace CatEcommerce.Api.RateLimiting;

/// <summary>
/// HO-FIL-001 (HO-FIL-001-ARCHITECTURE.md second pass, MEDIUM: scope failure). The gate-fix that
/// first added a per-caller upload limit and a per-IP serve limit put these two constants in
/// HO-ACC-001's own <see cref="RateLimitPolicies"/> -- outside HO-FIL-001 §13, with no
/// authorisation recorded. Moved to this file, matching HO-NTF-001's own
/// <c>NotificationRateLimitPolicies.cs</c>, which the ARCHITECTURE gate endorsed as the correct
/// ADR-341 shape: each area names its own policies in its own file, registered from its own
/// module's <c>AddRateLimiter(...)</c> call (additive -- see <c>FileStorageModule</c>).
/// </summary>
public static class FileRateLimitPolicies
{
    public const string FileUpload = "file-upload";
    public const string FileServe = "file-serve";
}
