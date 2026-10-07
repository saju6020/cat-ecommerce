using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ADM-001 §16. Real HTTP through TestServer, against this branch's own
/// <c>catecommerce_adm</c> database (see README's "per-branch database"). Adds only what
/// Administration itself needs beyond the shared base -- raised rate limits and a test CAPTCHA
/// answer, matching <c>AccountsApiFactory</c>'s pattern, so seeding staff/customers through the
/// real registration and sign-in endpoints doesn't get throttled or CAPTCHA-blocked in tests.
/// </summary>
public sealed class AdministrationApiFactory : CatEcommerceApiFactory
{
    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        settings["Captcha:TestAnswer"] = "TEST1";
        settings["DevUser:Enabled"] = "false";
        settings["RateLimiting:CaptchaIssue:PermitLimit"] = "100000";
        settings["RateLimiting:Registration:PermitLimit"] = "100000";
        settings["RateLimiting:SignIn:PermitLimit"] = "100000";
        settings["RateLimiting:PasswordReset:PermitLimit"] = "100000";
        settings["RateLimiting:AccountSensitive:PermitLimit"] = "100000";
        settings["RateLimiting:ProfileUpdate:PermitLimit"] = "100000";
        settings["ErrorVerbosity"] = "ExceptionMessage";
    }

    /// <summary>Gate fix 2026-09-26 (TEST gate I-E3): a second factory instance with
    /// <c>AuditExport:MaxRows</c> lowered, so "export over the cap -&gt; 413" can be exercised with
    /// a handful of real rows instead of 50,001 -- the same technique <c>AccountsApiFactory
    /// .WithTightRateLimits()</c> already uses for rate limits. The production default (50,000,
    /// <c>AuditExportRules.MaxRows</c>) is untouched; only this factory instance's configuration is
    /// overridden.</summary>
    public WebApplicationFactory<Program> WithLoweredAuditExportCap(int maxRows) => WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configBuilder) =>
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuditExport:MaxRows"] = maxRows.ToString()
            })));

    /// <summary>A third factory instance whose TimeProvider is a controllable
    /// <see cref="FakeTimeProvider"/>, for I-D4 (<c>security.staffIdleTimeoutMinutes</c> live
    /// effect) -- the same technique <c>AccountsApiFactory.WithFakeTimeProvider()</c> already uses
    /// for its own idle-timeout tests. <c>AccountsModule</c> registers <c>TimeProvider</c> via
    /// <c>TryAddSingleton</c>, so re-registering it here in <c>ConfigureTestServices</c> (which
    /// runs after) wins the direct (non-<c>IEnumerable</c>) resolution.</summary>
    public (WebApplicationFactory<Program> Factory, FakeTimeProvider TimeProvider) WithFakeTimeProvider()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(fakeTimeProvider)));

        return (factory, fakeTimeProvider);
    }
}
