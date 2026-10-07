namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>
/// Bound from appsettings.json section "Cors" -- SECURITY gate on HO-ACC-001 (the CORS MEDIUM).
/// Empty by default: no origin is granted a credentialed cross-origin response until explicitly
/// configured, the same "never assumed" default as <see cref="RequestOriginOptions"/>.
/// </summary>
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; set; } = [];
}
