using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-CAT-001 §16.1. The Cat Profiles API needs nothing beyond what every area shares -- see
/// <see cref="CatEcommerceApiFactory"/> for the Testing-environment setup and the connection
/// string, and tests/CatEcommerce.IntegrationTests/README.md for how a new area adds its own.
///
/// HO-ADM-001 §12.3 / ADR-043: every Cat/Vaccine action now requires a real signed-in caller
/// (<c>[RequireAction]</c>), so <see cref="CatIntegrationTestBase.RegisterCatAsync"/> seeds and
/// signs in a staff account for every call. The default <c>SignIn</c> rate limit (10/minute,
/// shared across every test in this factory's TestServer instance) is raised here, matching
/// <c>AccountsApiFactory</c>'s pattern, so a large test run does not fail on the limiter rather
/// than the behaviour under test.
/// </summary>
public sealed class CatApiFactory : CatEcommerceApiFactory
{
    // Gate-fix 2026-09-27 (HO-FIL-001-ARCHITECTURE.md third pass): CatProfileProjectionEndpointTests
    // now uploads a real CatProfilePhoto through /api/staff/files before attaching it -- needs a
    // signing key for HmacFileUrlSigner (Testing does not enforce FileStorageStartupGuardHostedService's
    // own check, but the signer still needs a real value to sign/verify with), and its own isolated
    // file-store subtree, matching FilesApiFactory's identical fix (I4 was observed to fail against
    // objects left over from another run against another database on the fixed, machine-wide default).
    private readonly string _isolatedFileStoreRoot =
        Path.Combine(Path.GetTempPath(), "catecommerce-file-store-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        settings["DevUser:Enabled"] = "false";
        settings["RateLimiting:SignIn:PermitLimit"] = "100000";
        settings["RateLimiting:Registration:PermitLimit"] = "100000";
        settings["FileStorage:UrlSigningKey"] = "test-environment-signing-key-at-least-32-bytes-long";
        settings["FileStorage:LocalStoreRoot"] = Path.Combine(_isolatedFileStoreRoot, "store");
        settings["FileStorage:TempPath"] = Path.Combine(_isolatedFileStoreRoot, "temp");
    }

    /// <summary>
    /// ACCEPTANCE gate A22 fix, owner decision (b), 2026-09-27: <c>PublicSite:WebBaseUrl</c> is
    /// empty by default in Testing (<see cref="PublicSiteStartupGuardHostedService"/> allows that),
    /// so the cage-card QR encodes a bare relative path unless a test opts in to an absolute one --
    /// the same "second factory instance, one setting overridden" technique
    /// <c>AdministrationApiFactory.WithLoweredAuditExportCap</c> already uses.
    /// </summary>
    public WebApplicationFactory<Program> WithPublicSiteWebBaseUrl(string webBaseUrl) => WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configBuilder) =>
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PublicSite:WebBaseUrl"] = webBaseUrl
            })));
}
