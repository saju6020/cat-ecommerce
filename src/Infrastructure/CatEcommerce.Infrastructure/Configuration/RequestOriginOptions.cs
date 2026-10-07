namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>
/// Bound from appsettings.json section "RequestOrigin" -- HO-ACC-001 §12.2 / ADR-040 / §20.12.
/// Empty by default (OQ-8): a reverse proxy must be explicitly trusted, never assumed.
/// </summary>
public sealed class RequestOriginOptions
{
    public const string SectionName = "RequestOrigin";

    public string[] TrustedProxies { get; set; } = [];
    public string[] TrustedNetworks { get; set; } = [];
}
