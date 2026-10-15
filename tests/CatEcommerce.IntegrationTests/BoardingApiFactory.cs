using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-BRD-001 section 16. Real HTTP through TestServer, against this branch's own database (<c>catecommerce_brd</c>, named by <c>CATECOMMERCE_TEST_DB</c>;
/// the factory refuses <c>catecommerce_dev</c> and <c>catecommerce_test</c>). Every worker is off -- tests call <c>BoardingWorkCycle</c> and
/// <c>NotificationWorkCycle</c> directly. The clock is a <see cref="FakeTimeProvider"/> that does not tick: rate cards are effective-dated and a stay's
/// whole life (hold window, check-in day, escalation minutes) is time, so a test must say "now" deterministically (section 16: advance
/// <c>FakeTimeProvider</c>). Staff sessions idle out after 30 minutes of FAKE time, so a test that advances far re-signs in.
/// </summary>
public class BoardingApiFactory : CatEcommerceApiFactory
{
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public BoardingApiFactory()
    {
        var connection = Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB");
        if (string.IsNullOrWhiteSpace(connection)
            || connection.Contains("catecommerce_dev", StringComparison.OrdinalIgnoreCase)
            || connection.Contains("catecommerce_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "CATECOMMERCE_TEST_DB must be set to the full Npgsql connection string of this branch's own database (e.g. catecommerce_brd), never catecommerce_dev or catecommerce_test.");
    }

    private readonly string _isolatedRoot = Path.Combine(Path.GetTempPath(), "catecommerce-file-store-tests", Guid.NewGuid().ToString("N"));

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

        settings["Clinic:Worker:Enabled"] = "false";
        settings["Boarding:Worker:Enabled"] = "false";
        // TestServer gives every request the same (null) RemoteIpAddress, so every test customer shares one rate-limit partition.
        settings["Boarding:Availability:PermitLimit"] = "100000";
        settings["Boarding:Booking:PermitLimit"] = "100000";

        // The stay diary uploads real files (purpose BoardingMedia): an isolated store per factory, never the machine-wide default.
        settings["FileStorage:UrlSigningKey"] = "test-environment-signing-key-at-least-32-bytes-long";
        settings["FileStorage:LocalStoreRoot"] = Path.Combine(_isolatedRoot, "store");
        settings["FileStorage:TempPath"] = Path.Combine(_isolatedRoot, "temp");
    }

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(Clock);

        // The gate looks only at the Boarding fixture's own core vaccines (see BoardingTestVaccineScope).
        var real = services.Single(d => d.ServiceType == typeof(CatEcommerce.Domain.DomainServices.IBoardingReferenceReader));
        services.Remove(real);
        services.AddScoped<CatEcommerce.Domain.DomainServices.IBoardingReferenceReader>(sp =>
            new BoardingTestVaccineScope((CatEcommerce.Domain.DomainServices.IBoardingReferenceReader)ActivatorUtilities.CreateInstance(sp, real.ImplementationType!)));
        RealClockCookieExpiry.Apply(services); // see its summary: the cookie jar uses the real clock
    }
}
