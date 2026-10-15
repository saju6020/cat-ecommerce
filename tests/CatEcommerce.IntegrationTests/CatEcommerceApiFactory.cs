using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// Shared base for every area's <see cref="WebApplicationFactory{TEntryPoint}"/>. Extracted after
/// wave A (HO-CAT-001's <c>CatApiFactory</c>, HO-ACC-001's <c>AccountsApiFactory</c>), which had
/// each independently duplicated the Testing-environment setup and the connection-string literal.
///
/// A new area's factory derives from this class and overrides <see cref="ConfigureAdditionalSettings"/>
/// / <see cref="ConfigureAdditionalServices"/> only for what is genuinely its own -- the Testing
/// environment and the connection string are handled here, once, for everyone. See
/// tests/CatEcommerce.IntegrationTests/README.md for the full "adding a new area" walkthrough.
///
/// Real HTTP through TestServer, against the shared local Postgres database -- the same instance
/// the Development profile uses (§5.1 / HO-CAT-001 §16.1). No Testcontainers, no in-memory EF
/// provider (§20.1).
/// </summary>
public abstract class CatEcommerceApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The "Testing" environment loads appsettings.json + appsettings.Testing.json (which does not
    /// exist) -- it does NOT load appsettings.Development.json, so the real connection string must
    /// be supplied here explicitly. Defaults to the shared catecommerce_test database on the local Postgres instance -- never
    /// catecommerce_dev, whose rows the running app shows (Issues.md #2; local-only credentials). Read from
    /// <c>CATECOMMERCE_TEST_DB</c> first so a wave-B (or later) area branch can point its whole
    /// suite at its own database without editing this file -- see the README's "per-branch
    /// database" section.
    /// </summary>
    protected static string TestConnectionString { get; } =
        Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=catecommerce_test;Username=postgres;Password=postgres";

    protected sealed override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:CatEcommerce"] = TestConnectionString,
                // The hosted order worker expires unpaid bank-transfer orders with THIS host's clock. Every area's tests share one database, and several move their clock
                // forward, so an enabled worker here would expire other areas' fresh orders (HO-ORD-001). The Ordering tests drive OrderWorkCycle by hand.
                ["Orders:Worker:Enabled"] = "false",
                // The hosted Boarding worker would lapse, expire, confirm and invoice other areas' Boarding stays against the shared database with THIS host's clock; the Boarding tests drive BoardingWorkCycle by hand.
                ["Boarding:Worker:Enabled"] = "false"
            };
            ConfigureAdditionalSettings(settings);
            configBuilder.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(ConfigureAdditionalServices);
    }

    /// <summary>Override to add configuration keys beyond the connection string -- Captcha test
    /// answers, raised rate limits, and so on. <paramref name="settings"/> already contains the
    /// connection string; add to it rather than replacing it.</summary>
    protected virtual void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
    }

    /// <summary>Override to replace services (spies, fakes) for every client this factory, and
    /// every <c>WithWebHostBuilder</c>-derived factory built from it, creates. <c>WithWebHostBuilder</c>
    /// re-invokes <see cref="ConfigureWebHost"/> (and so this hook) before layering on its own
    /// delegate, so an area's own <c>With...()</c> factory methods do not need to repeat these.
    /// </summary>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// A factory instance whose every log line (any level, any category) is captured into
    /// <paramref name="Logs"/>. Originally HO-ACC-001 (I-X1, scanning a representative set of flows
    /// for secrets) -- general enough for any area to reuse for the same purpose.
    /// </summary>
    public (WebApplicationFactory<Program> Factory, ConcurrentBag<string> Logs) WithCapturedLogs()
    {
        var logs = new ConcurrentBag<string>();
        var factory = WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddLogging(logging => logging.AddProvider(new CapturingLoggerProvider(logs)))));

        return (factory, logs);
    }

    /// <summary>
    /// A factory instance whose connection <c>RemoteIpAddress</c> is fixed via an
    /// <see cref="IStartupFilter"/> -- TestServer supplies no remote address by default, so no test
    /// could otherwise observe a non-null audit IP. Originally HO-ACC-001 (ACCEPTANCE gate LOW) --
    /// general enough for any area that records a caller's IP. The filter runs ahead of
    /// <c>Program.cs</c>'s own shared pipeline block, including <c>UseForwardedHeaders()</c>, so it
    /// stands in for "the peer this request actually arrived from" -- exactly the position a real
    /// reverse proxy's TCP connection would occupy. When <paramref name="trustAsProxy"/> is true,
    /// that same address is also added to <c>RequestOrigin:TrustedProxies</c>, so
    /// <c>ForwardedHeadersMiddleware</c> honours an <c>X-Forwarded-For</c> header sent by it; when
    /// false, the address is left untrusted, so any such header is ignored and the fixed address is
    /// what the client-IP accessor reports.
    /// </summary>
    public WebApplicationFactory<Program> WithFixedRemoteIpAddress(string ipAddress, bool trustAsProxy = false) =>
        WithWebHostBuilder(builder =>
        {
            if (trustAsProxy)
            {
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["RequestOrigin:TrustedProxies:0"] = ipAddress
                    }));
            }

            builder.ConfigureTestServices(services =>
                services.AddSingleton<IStartupFilter>(
                    new FixedRemoteIpAddressStartupFilter(System.Net.IPAddress.Parse(ipAddress))));
        });

    /// <summary>Sets <c>Connection.RemoteIpAddress</c> as the very first thing in the pipeline,
    /// before <c>Program.cs</c>'s own shared block -- <see cref="IStartupFilter"/> wraps around
    /// whatever the minimal-hosting model builds, regardless of there being no <c>Startup</c>
    /// class to intercept directly.</summary>
    private sealed class FixedRemoteIpAddressStartupFilter(System.Net.IPAddress remoteIpAddress) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                context.Connection.RemoteIpAddress = remoteIpAddress;
                await nextMiddleware(context);
            });
            next(app);
        };
    }

    private sealed class CapturingLoggerProvider(ConcurrentBag<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, logs);
        public void Dispose() { }

        private sealed class CapturingLogger(string categoryName, ConcurrentBag<string> logs) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                logs.Add($"{categoryName}: {formatter(state, exception)}" + (exception is null ? "" : $" | {exception}"));
            }
        }
    }
}
