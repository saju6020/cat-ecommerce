using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Notifications;
using CatEcommerce.Infrastructure.Notifications.Senders;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-NTF-001 §16. Real HTTP through TestServer, against this branch's own <c>catecommerce_ntf</c>
/// database (see README's "per-branch database"). Senders are Capturing; the worker loop is off --
/// tests call <c>NotificationWorkCycle</c> passes directly and advance a <see cref="FakeTimeProvider"/>.
/// </summary>
public sealed class NotificationsApiFactory : CatEcommerceApiFactory
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

        // Integrator instruction (2026-09-27, post HO-CAT-001-gates merge): the web base URL is now
        // the shared PublicSite:WebBaseUrl (also used for the cage-card QR code), which defaults to
        // empty in Testing (CatApiFactory's own owner-ratified default, ACCEPTANCE gate A22) --
        // Notifications' own NotificationLinkBuilder has no relative-path fallback the way
        // GetCatCageCardQueryHandler does, so every Notifications test needs a real value, the same
        // way CatApiFactory.WithPublicSiteWebBaseUrl opts ONE test in.
        settings["PublicSite:WebBaseUrl"] = "https://portal.example.test";
        settings["Notifications:ApiBaseUrl"] = "https://api.example.test";
        settings["Notifications:Channels:Sms:Sender"] = "Capturing";
        settings["Notifications:Channels:Email:Sender"] = "Capturing";
        settings["Notifications:Worker:Enabled"] = "false"; // I-A1: hosted but idle in Testing
        settings["Notifications:Unsubscribe:PermitLimit"] = "100000";
        settings["Notifications:TemplatePreview:PermitLimit"] = "100000";
    }

    /// <summary>A second factory instance whose TimeProvider is a controllable
    /// <see cref="FakeTimeProvider"/>, for quiet-hours and retry-backoff cases -- same technique
    /// as <c>AccountsApiFactory.WithFakeTimeProvider()</c>.</summary>
    public (WebApplicationFactory<Program> Factory, FakeTimeProvider TimeProvider) WithFakeTimeProvider()
    {
        var fakeTimeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(fakeTimeProvider)));

        return (factory, fakeTimeProvider);
    }

    /// <summary>Scripts the Capturing sender for a channel to return a fixed outcome for a given
    /// destination on its next send (and every send thereafter until re-scripted).</summary>
    public static void ScriptOutcome(IServiceProvider services, NotificationChannel channel, string destination, SendOutcome outcome)
    {
        var resolver = services.GetRequiredService<NotificationSenderResolver>();
        var sender = (CapturingChannelSender)resolver.Resolve(channel);
        sender.ScriptedOutcomes.GetOrAdd(destination, _ => new System.Collections.Concurrent.ConcurrentQueue<SendOutcome>()).Enqueue(outcome);
    }

    public static CapturingChannelSender CapturingSender(IServiceProvider services, NotificationChannel channel) =>
        (CapturingChannelSender)services.GetRequiredService<NotificationSenderResolver>().Resolve(channel);

    /// <summary>A factory instance whose catalogue includes one or more test-only definitions
    /// (a marketing template for I-B9/I-B10, an urgent one for I-C2) -- registered a second time
    /// here (DI resolves the LAST registration for a directly-requested service type), so the
    /// shipped catalogue itself is never edited for a test.</summary>
    public WebApplicationFactory<Program> WithAdditionalTemplateDefinitions(params NotificationTemplateDefinition[] definitions) =>
        WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<INotificationTemplateCatalogue>(new StaticNotificationTemplateCatalogue(definitions))));

    /// <summary>I-E4: a tight Unsubscribe permit limit for one test, the same
    /// <c>AccountsApiFactory.WithTightRateLimits</c> technique -- the base settings above raise it
    /// to 100000 so every OTHER test's unsubscribe calls never trip it.</summary>
    public WebApplicationFactory<Program> WithTightUnsubscribeRateLimit(int permitLimit) =>
        WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Unsubscribe:PermitLimit"] = permitLimit.ToString(),
                ["Notifications:Unsubscribe:WindowMinutes"] = "10"
            })));
}
