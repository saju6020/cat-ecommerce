using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// A clock that follows the wall clock plus an adjustable offset -- <b>never frozen</b>. Ordering tests need to cross a 48-hour payment
/// window (<c>Advance</c>) and still create prices, rates and sessions "now": with a frozen fake clock a fixture timestamp taken from the wall
/// clock can land after the clock and silently be "not yet in force" (it broke a Shop test), whereas here <c>GetUtcNow()</c> only ever moves
/// forward and a fixture built from it is always in force.
/// </summary>
public sealed class AdjustableTimeProvider : TimeProvider
{
    private long _offsetTicks;

    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow + TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    /// <summary>Moves this factory's clock forward (never back).</summary>
    public void Advance(TimeSpan by)
    {
        if (by < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(by), "The clock only moves forward.");
        Interlocked.Add(ref _offsetTicks, by.Ticks);
    }
}

/// <summary>
/// HO-ORD-001 §16. Real HTTP through TestServer, against this branch's own <c>catecommerce_ord</c> database. <b>Refuses to start</b> unless
/// <c>CATECOMMERCE_TEST_DB</c> is set and names neither <c>catecommerce_dev</c> nor <c>catecommerce_test</c>: Ordering tests create money,
/// stock and customer records that must never land in a shared database. The workers are off -- tests call <c>OrderWorkCycle</c> and
/// <c>NotificationWorkCycle</c> directly.
/// </summary>
public class OrderingApiFactory : CatEcommerceApiFactory
{
    public AdjustableTimeProvider Clock { get; } = new();

    public OrderingApiFactory()
    {
        RequireOrderingDatabase();
    }

    /// <summary>The Ordering suite's guard, also used by the schema tests that open their own connection.</summary>
    public static string RequireOrderingDatabase()
    {
        var value = Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB");
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException(
                "CATECOMMERCE_TEST_DB must be set to the full Npgsql connection string of this branch's own database (catecommerce_ord); the Ordering tests refuse to run without it.");

        if (value.Contains("catecommerce_dev", StringComparison.OrdinalIgnoreCase) || value.Contains("catecommerce_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Ordering tests must never run against catecommerce_dev or catecommerce_test.");

        return value;
    }


    /// <summary>The user the port tests act as -- fixed per factory.</summary>
    public Guid PortUserId { get; } = Guid.NewGuid();

    private Lazy<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>>? _portHost;

    /// <summary>
    /// A host (same database) whose <c>IUserContextProvider</c> answers a fixed user, for the tests that resolve a port (<c>IStockLedger</c>) in a DI scope and commit
    /// through the scoped context -- exactly what a command handler does, without an HTTP request to say who the caller is.
    /// </summary>
    public Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> PortHost =>
        (_portHost ??= new Lazy<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>>(() => WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<Platform.Infrastructure.Common.Security.IUserContextProvider>();
                services.AddScoped<Platform.Infrastructure.Common.Security.IUserContextProvider>(_ => new FixedUserContextProvider(PortUserId));
            })))).Value;

    private sealed class FixedUserContextProvider(Guid userId) : Platform.Infrastructure.Common.Security.IUserContextProvider
    {
        public Platform.Infrastructure.Common.Security.UserContext GetUserContext() =>
            new(userId, Guid.Parse("00000000-0000-0000-0000-000000000002")) { UserName = "port-test", Roles = ["Reception"] };
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
        settings["RateLimiting:Cart:PermitLimit"] = "100000";
        settings["RateLimiting:Checkout:PermitLimit"] = "100000";
        settings["RateLimiting:CourierWebhook:PermitLimit"] = "100000";

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
        settings["Orders:Worker:Enabled"] = "false";
        settings["Localisation:ContentLocales:0"] = "en";

        // A test-only courier webhook provider (HO-ORD-001 §20.8: real secrets never go in a committed file; this value exists only in the test host's in-memory configuration).
        settings["Couriers:Webhooks:testcourier:Secret"] = OrderingWebhook.Secret;
        settings["Couriers:Webhooks:testcourier:SignatureHeader"] = OrderingWebhook.Header;
    }

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(Clock);

        // The idempotency probe: a controller that exists only in the test assembly, so the HTTP-edge filter (ADR-202) can be driven
        // by real requests before any real endpoint carries [Idempotent].
        services.AddControllers().PartManager.ApplicationParts.Add(new AssemblyPart(typeof(OrderingApiFactory).Assembly));
    }
}

/// <summary>The Ordering factory with <b>every log line of any level</b> captured into <see cref="Logs"/> -- for the one class that proves what is
/// never logged (HO-ORD-001 I-X1).</summary>
public sealed class OrderingLoggedApiFactory : OrderingApiFactory
{
    public ConcurrentBag<string> Logs { get; } = [];

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        base.ConfigureAdditionalServices(services);
        services.AddLogging(logging => logging.AddProvider(new CapturingProvider(Logs)));
    }

    private sealed class CapturingProvider(ConcurrentBag<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Capturing(categoryName, logs);
        public void Dispose() { }

        private sealed class Capturing(string category, ConcurrentBag<string> logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                logs.Add($"{logLevel} {category}: {formatter(state, exception)}" + (exception is null ? string.Empty : $" | {exception}"));
        }
    }
}
