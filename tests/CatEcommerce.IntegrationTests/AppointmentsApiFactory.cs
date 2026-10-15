using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-APT-001 §16. Real HTTP through TestServer, against this branch's own <c>catecommerce_apt</c>
/// database (see README's "per-branch database"). Both workers (Notifications and Clinic) are off
/// -- tests call <c>NotificationWorkCycle</c>/<c>ClinicWorkCycle</c> directly and advance a
/// <see cref="FakeTimeProvider"/>.
/// </summary>
public class AppointmentsApiFactory : CatEcommerceApiFactory
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

        settings["PublicSite:WebBaseUrl"] = "https://portal.example.test";
        settings["Notifications:ApiBaseUrl"] = "https://api.example.test";
        settings["Notifications:Channels:Sms:Sender"] = "Capturing";
        settings["Notifications:Channels:Email:Sender"] = "Capturing";
        settings["Notifications:Worker:Enabled"] = "false";
        settings["Notifications:Unsubscribe:PermitLimit"] = "100000";
        settings["Notifications:TemplatePreview:PermitLimit"] = "100000";

        settings["Clinic:Worker:Enabled"] = "false"; // I-A2: hosted but idle in Testing

        // TestServer gives every request the same (null) RemoteIpAddress, collapsing every test
        // customer into the one "unknown" rate-limit partition -- raised so I-C2's real concurrency
        // (many distinct customers, one shared partition under TestServer) is never itself throttled.
        settings["Clinic:Availability:PermitLimit"] = "100000";
        settings["Clinic:Booking:PermitLimit"] = "100000";
        settings["Clinic:Change:PermitLimit"] = "100000";
        settings["Clinic:Performers:PermitLimit"] = "100000";
    }

    /// <summary>A second factory instance with the customer change (backlog #27) and public
    /// performers (backlog #28) limits tightened to two per window, for the tests that prove those
    /// endpoints are rate limited at all (the default factory raises them so unrelated tests are
    /// never throttled).</summary>
    public WebApplicationFactory<Program> WithTightClinicRateLimits() => WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configBuilder) =>
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Clinic:Change:PermitLimit"] = "2",
                ["Clinic:Change:WindowMinutes"] = "10",
                ["Clinic:Performers:PermitLimit"] = "2",
                ["Clinic:Performers:WindowMinutes"] = "10",
            })));

    /// <summary>A second factory instance whose TimeProvider is a controllable
    /// <see cref="FakeTimeProvider"/> -- the same technique <c>AccountsApiFactory.WithFakeTimeProvider()</c>
    /// uses. Needed here because a service price must have <c>EffectiveFrom &gt;= now</c> at
    /// creation and <c>&lt;= now</c> when a booking reads it -- only achievable deterministically
    /// with a controlled clock (§16.1: tests must not depend on the wall clock).</summary>
    public (WebApplicationFactory<Program> Factory, FakeTimeProvider TimeProvider) WithFakeTimeProvider(DateTimeOffset? start = null)
    {
        var fakeTimeProvider = new FakeTimeProvider(start ?? DateTimeOffset.UtcNow);
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(fakeTimeProvider);
                RealClockCookieExpiry.Apply(services); // see its summary: cookie jar uses the real clock
            }));

        return (factory, fakeTimeProvider);
    }
}
