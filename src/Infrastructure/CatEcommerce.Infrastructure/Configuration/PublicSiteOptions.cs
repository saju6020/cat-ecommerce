namespace CatEcommerce.Infrastructure.Configuration;

/// <summary>
/// Bound from appsettings.json section "PublicSite". Owner decision, 2026-09-27 (HO-CAT-001 §1
/// Amended, ACCEPTANCE gate A22): the ONE shared public web address used to build absolute links
/// back into the site -- the cage card's QR code today (US-CAT-11), and notification links later,
/// once both this handover and the Notifications area have merged (an integrator task; this
/// handover does not touch Notifications). Replaces <c>CatProfileOptions.PublicWebBaseUrl</c>,
/// which was Cat-Profiles-only, never actually set anywhere, and produced a relative-path QR
/// payload that could not resolve on a scanning phone.
///
/// Distinct from <c>AccountOptions.WebBaseUrl</c>, which is documented as "Development-only reset
/// link base URL -- never used outside Development" -- this one is required in Production.
///
/// Validated at real host startup by <c>PublicSiteStartupGuardHostedService</c>
/// (<c>CatProfilesModule.cs</c>), not by a plain DataAnnotation: the rule depends on
/// <c>IHostEnvironment</c> ("required in Production"; "https required outside
/// Development/Testing"), which a <c>[Required]</c>/<c>[Url]</c> attribute cannot see.
/// </summary>
public sealed class PublicSiteOptions
{
    public const string SectionName = "PublicSite";

    /// <summary>
    /// An absolute http(s) URL with no path, query or fragment (e.g. "https://example.com", not
    /// "https://example.com/cats" or "https://example.com?x=1") -- validated whenever a value is
    /// present, in every environment. Required (non-empty) in Production. https is required
    /// whenever a value is present outside Development/Testing. May be empty in Development or
    /// Testing; in Testing an individual test factory sets it explicitly when the test needs an
    /// absolute URL (e.g. to decode a QR code and assert the exact target) -- callers that read
    /// this value (<c>GetCatCageCardQueryHandler</c>) fall back to a relative path when it is
    /// empty, exactly as before this option existed, so leaving it empty does not break every
    /// other test or a developer's own local run.
    /// </summary>
    public string WebBaseUrl { get; set; } = "";
}
