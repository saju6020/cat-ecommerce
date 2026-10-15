using CatEcommerce.Domain.DomainServices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ACC-001 §16.1. Real HTTP through TestServer, against the shared catecommerce_test database (never catecommerce_dev -- Issues.md #2).
/// Sets Captcha:TestAnswer and raises every rate limit except in the dedicated rate-limit cases
/// (I-B9/I-C9), which use <see cref="WithTightRateLimits"/> instead. The Testing-environment setup
/// and the connection string are shared with every other area via <see cref="CatEcommerceApiFactory"/>;
/// this class adds only what Accounts itself needs.
/// </summary>
public sealed class AccountsApiFactory : CatEcommerceApiFactory
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
    }

    protected override void ConfigureAdditionalServices(IServiceCollection services) =>
        services.AddScoped<IPasswordResetTokenDispatcher, SpyPasswordResetTokenDispatcher>();

    /// <summary>A second factory instance with the real (low) rate limits, for I-B9/I-C9 and the
    /// SECURITY gate MEDIUM (second pass) email step-up throttling tests. <c>ProfileUpdate</c> is
    /// left at the real, committed 5-per-60-minutes default (not tightened further) -- customer
    /// partitioning means a distinct test customer never shares a bucket with another test's, so
    /// the real limit is already deterministic here.</summary>
    public WebApplicationFactory<Program> WithTightRateLimits() => WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Registration:PermitLimit"] = "2",
                ["RateLimiting:Registration:WindowMinutes"] = "10",
                ["RateLimiting:SignIn:PermitLimit"] = "2",
                ["RateLimiting:SignIn:WindowMinutes"] = "1",
                ["RateLimiting:AccountSensitive:PermitLimit"] = "2",
                ["RateLimiting:AccountSensitive:WindowMinutes"] = "60",
                ["RateLimiting:ProfileUpdate:PermitLimit"] = "5",
                ["RateLimiting:ProfileUpdate:WindowMinutes"] = "60",
            });
        }));

    /// <summary>
    /// A third factory instance whose TimeProvider is a controllable <see cref="FakeTimeProvider"/>,
    /// for cases needing to cross a real-world boundary (token expiry, I-D6; staff idle timeout,
    /// I-G3/I-G5) without an actual multi-minute wait. AccountsModule registers TimeProvider via
    /// TryAddSingleton, so re-registering it here in ConfigureTestServices (which runs after) wins:
    /// DI resolves the last registration for a directly-requested (non-IEnumerable) service type.
    /// </summary>
    public (WebApplicationFactory<Program> Factory, FakeTimeProvider TimeProvider) WithFakeTimeProvider()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<TimeProvider>(fakeTimeProvider)));

        return (factory, fakeTimeProvider);
    }

    /// <summary>
    /// A fifth factory instance whose <see cref="IPasswordHasher"/> is wrapped by
    /// <see cref="SpyPasswordHasher"/>, for the SECURITY gate's timing-enumeration MEDIUM: proves a
    /// refusal branch calls <c>VerifyAgainstDummy</c>, without relying on wall-clock timing (the
    /// gate's own guidance -- a hasher spy is deterministic, wall-clock timing is not).
    /// </summary>
    public WebApplicationFactory<Program> WithHasherSpy() => WithWebHostBuilder(builder =>
        builder.ConfigureTestServices(services =>
            services.AddScoped<IPasswordHasher>(sp => new SpyPasswordHasher(
                ActivatorUtilities.CreateInstance<CatEcommerce.Infrastructure.Security.Argon2idPasswordHasher>(sp)))));

    /// <summary>
    /// A sixth factory instance with one configured CORS-allowed origin, for the SECURITY gate's
    /// CORS MEDIUM: the committed appsettings.json's <c>Cors:AllowedOrigins</c> is empty
    /// (fail-closed), so a dedicated instance is needed to prove a genuinely configured origin
    /// *does* get a grant, alongside the base factory proving an unconfigured one does not.
    /// </summary>
    public WebApplicationFactory<Program> WithAllowedCorsOrigin(string origin) => WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configBuilder) =>
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = origin,
            })));

    /// <summary>
    /// SECURITY gate item 7 (first-pass gate fix round, owner decision 2026-09-26) -- a seventh
    /// factory instance with <c>Account:CsrfCookieDomain</c> configured, for proving the
    /// cross-subdomain-production case: <c>pc_csrf</c> gets a <c>Domain</c> attribute, while
    /// <c>pc_session</c> stays host-only regardless. The base factory (committed appsettings.json,
    /// empty <c>CsrfCookieDomain</c>) proves the opposite -- no <c>Domain</c> on any cookie at all,
    /// the correct behaviour for same-host local development.
    /// </summary>
    public WebApplicationFactory<Program> WithCsrfCookieDomain(string domain) => WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configBuilder) =>
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Account:CsrfCookieDomain"] = domain,
            })));
}
