using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-BIL-001 §16. Real HTTP through TestServer, against this branch's own <c>catecommerce_bil</c>
/// database (set <c>CATECOMMERCE_TEST_DB</c>; see the README's "per-branch database"). Both workers are off
/// -- tests call <c>NotificationWorkCycle</c> directly. The clock is a <see cref="FakeTimeProvider"/> that does
/// not tick, because tax rates and prices are effective-dated (HO-ADM-001 ADR-049, "no back-dating") and a
/// test must be able to say "this rate is in force now, that one from tomorrow" deterministically.
/// </summary>
public class BillingApiFactory : CatEcommerceApiFactory
{
    /// <summary>The one clock of this factory's host. Advance it to cross an effective date; staff sessions
    /// idle out after 30 minutes of <i>fake</i> time, so a test that advances far re-signs in.</summary>
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>Billing tests create money records that must never land in a shared dev database: the factory refuses to start unless
    /// <c>CATECOMMERCE_TEST_DB</c> names a per-branch database that is not <c>catecommerce_dev</c>.</summary>
    public BillingApiFactory()
    {
        var connection = Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB");
        if (string.IsNullOrWhiteSpace(connection) || connection.Contains("catecommerce_dev", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "CATECOMMERCE_TEST_DB must be set to the full Npgsql connection string of this branch's own database (e.g. catecommerce_bil), never catecommerce_dev.");
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

/// <summary>The Billing factory with <b>every log line of any level</b> captured into <see cref="Logs"/> -- for the one class that proves what is never logged
/// (HO-BIL-001 I-X1). Separate from <see cref="BillingApiFactory"/> so the other classes do not pay for capturing every line.</summary>
public sealed class BillingLoggedApiFactory : BillingApiFactory
{
    public System.Collections.Concurrent.ConcurrentBag<string> Logs { get; } = [];

    protected override void ConfigureAdditionalServices(IServiceCollection services)
    {
        base.ConfigureAdditionalServices(services);
        services.AddLogging(logging => logging.AddProvider(new CapturingProvider(Logs)));
    }

    private sealed class CapturingProvider(System.Collections.Concurrent.ConcurrentBag<string> logs) : Microsoft.Extensions.Logging.ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => new Capturing(categoryName, logs);
        public void Dispose() { }

        private sealed class Capturing(string category, System.Collections.Concurrent.ConcurrentBag<string> logs) : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                logs.Add($"{logLevel} {category}: {formatter(state, exception)}" + (exception is null ? string.Empty : $" | {exception}"));
        }
    }
}
