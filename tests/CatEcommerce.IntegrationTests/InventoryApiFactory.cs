using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Infrastructure.Common.Security;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-IMP-001 §16. Real HTTP through TestServer, against this branch's own <c>catecommerce_imp</c> database (set
/// <c>CATECOMMERCE_TEST_DB</c> to its FULL connection string). Every worker is off -- tests call <c>InventoryWorkCycle</c> and
/// <c>NotificationWorkCycle</c> directly, optionally advancing a <see cref="FakeTimeProvider"/>. Triggers and constraints are real; a test
/// never disables one (§16.1 rule 3).
/// </summary>
public sealed class InventoryApiFactory : CatEcommerceApiFactory
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

        settings["Shop:Worker:Enabled"] = "false";
        settings["Shop:Catalogue:PermitLimit"] = "100000";
        settings["Clinic:Worker:Enabled"] = "false";
        settings["Inventory:Worker:Enabled"] = "false";
    }

    /// <summary>The user the port tests act as -- fixed per factory, so a ledger entry and an audit row can be asserted to name them.</summary>
    public Guid PortUserId { get; } = Guid.NewGuid();

    private readonly Lazy<WebApplicationFactory<Program>> _portHost;

    /// <summary>The Inventory suite never runs against the shared dev database: <c>CATECOMMERCE_TEST_DB</c> must be set and must not name
    /// <c>catecommerce_dev</c>.</summary>
    public static string RequireBranchDatabase()
    {
        var value = Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB");
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("CATECOMMERCE_TEST_DB must be set to this branch's own database (catecommerce_imp); the Inventory tests refuse to run without it.");
        if (value.Contains("catecommerce_dev", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Inventory tests must never run against catecommerce_dev.");
        return value;
    }

    public InventoryApiFactory()
    {
        RequireBranchDatabase();
        // The "port contract" tests resolve IStockLedger from a fresh DI scope and commit through the scoped CatEcommerceDbContext -- exactly what a
        // consumer handler does -- so there is no HTTP request to say who the caller is. This SECOND host (same database) answers with a fixed user
        // instead; every HTTP test uses the ordinary host and its real sessions.
        _portHost = new Lazy<WebApplicationFactory<Program>>(() => WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserContextProvider>();
                services.AddScoped<IUserContextProvider>(_ => new FixedUserContextProvider(PortUserId));
            })));
    }

    /// <summary>A host whose <c>IUserContextProvider</c> is <see cref="PortUserId"/> -- see the constructor.</summary>
    public WebApplicationFactory<Program> PortHost => _portHost.Value;

    private sealed class FixedUserContextProvider(Guid userId) : IUserContextProvider
    {
        public UserContext GetUserContext() => new(userId, Guid.Parse("00000000-0000-0000-0000-000000000002")) { UserName = "port-test", Roles = ["Inventory"] };
    }

    /// <summary>A factory whose <c>TimeProvider</c> is a controllable clock starting at <paramref name="start"/> -- for the expiry-alert and
    /// consumption-window cases. Staff sessions idle out after 30 minutes, so a test that jumps the clock re-signs in afterwards.</summary>
    public (WebApplicationFactory<Program> Factory, FakeTimeProvider TimeProvider) WithFakeClock(DateTimeOffset start)
    {
        var fake = new FakeTimeProvider(start);
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(fake)));

        return (factory, fake);
    }
}
