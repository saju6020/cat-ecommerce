using CatEcommerce.Domain.DomainServices;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-SHP-001 §16. Real HTTP through TestServer, against this branch's own <c>catecommerce_shp</c>
/// database (see README's "per-branch database"). Raised rate limits and a test CAPTCHA answer, the
/// same pattern <c>AdministrationApiFactory</c> and <c>NotificationsApiFactory</c> use, so seeding
/// staff/customers through the real registration and sign-in endpoints is never throttled or
/// CAPTCHA-blocked in tests.
/// </summary>
public sealed class ShopApiFactory : CatEcommerceApiFactory
{
    // Product images are real uploads (U-A5b, I-A6): an isolated file store per factory, computed once
    // so every WithWebHostBuilder-derived factory shares it, the same pattern CatApiFactory uses.
    private readonly string _isolatedFileStoreRoot =
        Path.Combine(Path.GetTempPath(), "catecommerce-file-store-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        settings["FileStorage:UrlSigningKey"] = "test-environment-signing-key-at-least-32-bytes-long";
        settings["FileStorage:LocalStoreRoot"] = Path.Combine(_isolatedFileStoreRoot, "store");
        settings["FileStorage:TempPath"] = Path.Combine(_isolatedFileStoreRoot, "temp");
        settings["Captcha:TestAnswer"] = "TEST1";
        settings["DevUser:Enabled"] = "false";
        settings["RateLimiting:CaptchaIssue:PermitLimit"] = "100000";
        settings["RateLimiting:Registration:PermitLimit"] = "100000";
        settings["RateLimiting:SignIn:PermitLimit"] = "100000";
        settings["RateLimiting:PasswordReset:PermitLimit"] = "100000";
        settings["RateLimiting:AccountSensitive:PermitLimit"] = "100000";
        settings["RateLimiting:ProfileUpdate:PermitLimit"] = "100000";
        settings["Shop:Worker:Enabled"] = "false";
        // The catalogue rate limit (120/min/IP) would throttle the many tests sharing one TestServer
        // address; I-E10 lowers it for one test, I-E11 is unaffected.
        settings["Shop:Catalogue:PermitLimit"] = "100000";
        // Same for the per-customer food-recommendations limit (backlog row 20); one test lowers it.
        settings["Shop:FoodRecommendations:PermitLimit"] = "100000";
        // Notifications (HO-NTF-001): the real outbox and delivery pipeline run, with Capturing
        // senders and the worker loop off -- tests call NotificationWorkCycle passes directly, the
        // same way NotificationsApiFactory does. PublicSite:WebBaseUrl feeds INotificationLinkBuilder
        // (ProductLink).
        settings["PublicSite:WebBaseUrl"] = "https://portal.example.test";
        settings["Notifications:ApiBaseUrl"] = "https://api.example.test";
        settings["Notifications:Channels:Sms:Sender"] = "Capturing";
        settings["Notifications:Channels:Email:Sender"] = "Capturing";
        settings["Notifications:Worker:Enabled"] = "false";
        settings["Localisation:ContentLocales:0"] = "en";
    }

    /// <summary>§16.1 rule 4 -- a factory whose <see cref="IDietAuthorisationGate"/> is the
    /// controllable stand-in for HO-PHM-001's real gate, for I-F1..I-F3 and I-F7. The shipped
    /// fail-closed default (I-F8) is exercised by the base factory directly.</summary>
    public (WebApplicationFactory<Program> Factory, ControllableDietAuthorisations Authorisations) WithControllableDietGate()
    {
        var authorisations = new ControllableDietAuthorisations();
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<IDietAuthorisationGate>(sp =>
                    new ControllableDietAuthorisationGate(authorisations, sp.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>()))));

        return (factory, authorisations);
    }

    /// <summary>§16.1 rule 4 -- a second factory instance whose <see cref="IPurchaseHistoryReader"/>
    /// is the controllable double, for I-D1/I-D2. The shipped fail-closed default
    /// (<c>NoPurchaseHistoryReader</c>) is exercised by the base factory directly (I-D6), never this
    /// one.</summary>
    public (WebApplicationFactory<Program> Factory, ControllablePurchaseHistoryReader Reader) WithControllablePurchaseHistory()
    {
        var reader = new ControllablePurchaseHistoryReader();
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IPurchaseHistoryReader>(_ => reader)));

        return (factory, reader);
    }

    /// <summary>§16.1 rule 5 -- a factory with <c>Localisation:ContentLocales = ["en", "bn"]</c>
    /// (<c>bn</c> is used in tests only, OQ-11). The base factory keeps the shipped default
    /// <c>["en"]</c>, which I-H6 relies on.</summary>
    public WebApplicationFactory<Program> WithBengaliEnabled() =>
        WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configBuilder) =>
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?> { ["Localisation:ContentLocales:1"] = "bn" })));


    /// <summary>I-E10 -- the `catalogue` limit lowered for one test, the same technique as
    /// <c>AccountsApiFactory.WithTightRateLimits</c>.</summary>
    public WebApplicationFactory<Program> WithTightCatalogueLimit(int permitLimit) =>
        WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configBuilder) =>
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Shop:Catalogue:PermitLimit"] = permitLimit.ToString(),
                ["Shop:Catalogue:WindowMinutes"] = "10"
            })));
    /// <summary>
    /// A factory whose <c>TimeProvider</c> is a fixed instant inside Asia/Dhaka daytime (never inside
    /// the default 21:00-08:00 quiet hours), for the back-in-stock delivery cases -- the same
    /// technique as <c>NotificationRoutingEndpointTests.DaytimeFactory</c>. Create products and
    /// prices through the BASE factory (real time): a price scheduled now is rejected as back-dated
    /// when the fake clock is ahead of real time.
    /// </summary>
    public (WebApplicationFactory<Program> Factory, Microsoft.Extensions.Time.Testing.FakeTimeProvider TimeProvider) WithDaytimeClock()
    {
        var now = DateTimeOffset.UtcNow;
        var fake = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(now.Hour is >= 2 and < 15 ? now : now.AddHours(12));
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(fake)));

        return (factory, fake);
    }
}
