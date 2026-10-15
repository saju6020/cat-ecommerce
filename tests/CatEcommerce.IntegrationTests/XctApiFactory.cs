using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-XCT-001 section 16. Real HTTP through TestServer against this branch's own database (<c>catecommerce_xct</c>).
/// <b>Refuses to start</b> when <c>CATECOMMERCE_TEST_DB</c> is unset or names <c>catecommerce_dev</c> or <c>catecommerce_test</c>:
/// this suite writes protected rows that can never be cleaned up (section 5.1), so it must never share a database with development
/// or with the other areas' suites. Both workers are off in Testing; tests call <c>RetentionWorkCycle</c> directly.
/// </summary>
public sealed class XctApiFactory : CatEcommerceApiFactory
{
    private static readonly string[] ForbiddenDatabases = ["catecommerce_dev", "catecommerce_test"];

    /// <summary>The per-branch connection string, after the guard. Throws with the reason when it is unsafe.</summary>
    public static string GuardedConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB");
        if (string.IsNullOrWhiteSpace(configured))
            throw new InvalidOperationException(
                "CATECOMMERCE_TEST_DB must be set to the full Npgsql connection string of this branch's own database (e.g. catecommerce_xct).");

        var database = new NpgsqlConnectionStringBuilder(configured).Database ?? string.Empty;
        if (ForbiddenDatabases.Contains(database, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"The XCT suite refuses to run against '{database}'. Point CATECOMMERCE_TEST_DB at catecommerce_xct.");

        return configured;
    }

    public XctApiFactory() => GuardedConnectionString();

    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        GuardedConnectionString();

        settings["Captcha:TestAnswer"] = "TEST1";
        settings["DevUser:Enabled"] = "false";
        foreach (var policy in new[] { "CaptchaIssue", "Registration", "SignIn", "PasswordReset", "AccountSensitive", "ProfileUpdate" })
            settings[$"RateLimiting:{policy}:PermitLimit"] = "100000";

        settings["PublicSite:WebBaseUrl"] = "https://portal.example.test";
        settings["Notifications:ApiBaseUrl"] = "https://api.example.test";
        settings["Notifications:Channels:Sms:Sender"] = "Capturing";
        settings["Notifications:Channels:Email:Sender"] = "Capturing";
        settings["Notifications:Worker:Enabled"] = "false";
        settings["Operations:RetentionWorker:Enabled"] = "false";
        settings["Operations:Database:EnforceNonOwnerRole"] = "false";
    }

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        // I-A5: a controller that exists only in this test assembly, so a refused delete can be provoked through real HTTP.
        services.AddControllers().AddApplicationPart(typeof(XctApiFactory).Assembly);
    }

    /// <summary>A factory whose <see cref="TimeProvider"/> the test controls (erasure holds, retention cut-offs, idle timeouts).</summary>
    public (WebApplicationFactory<Program> Factory, FakeTimeProvider TimeProvider) WithFakeTimeProvider(DateTimeOffset? start = null)
    {
        var fake = new FakeTimeProvider(start ?? DateTimeOffset.UtcNow);
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(fake)));
        return (factory, fake);
    }

}
