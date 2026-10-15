using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-POR-001 §16. Real HTTP through TestServer against this branch's own database (<c>catecommerce_por</c>). The factory REFUSES to
/// start unless <c>CATECOMMERCE_TEST_DB</c> is set to the full connection string of a database that is neither
/// <c>catecommerce_dev</c> nor <c>catecommerce_test</c> -- a Portal run must never write to a shared database by accident.
/// Both workers are off. The clock is a <see cref="FakeTimeProvider"/> that does not tick, so prices and booking windows are deterministic.
/// </summary>
public class PortalApiFactory : CatEcommerceApiFactory
{
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    public PortalApiFactory()
    {
        var connection = Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB");
        if (string.IsNullOrWhiteSpace(connection)
            || connection.Contains("catecommerce_dev", StringComparison.OrdinalIgnoreCase)
            || connection.Contains("catecommerce_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "CATECOMMERCE_TEST_DB must be set to the full Npgsql connection string of this branch's own database (catecommerce_por), "
                + "never catecommerce_dev or catecommerce_test.");
    }

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

        settings["Pharmacy:Worker:Enabled"] = "false";
        settings["Inventory:Worker:Enabled"] = "false";
        settings["Shop:Worker:Enabled"] = "false";
        settings["Clinic:Worker:Enabled"] = "false";
        settings["Clinic:Availability:PermitLimit"] = "100000";
        settings["Clinic:Booking:PermitLimit"] = "100000";
        settings["Clinic:Change:PermitLimit"] = "100000";
        settings["Clinic:Performers:PermitLimit"] = "100000";
    }

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(Clock);
        RealClockCookieExpiry.Apply(services); // see its summary: the cookie jar uses the real clock
    }
}
