using CatEcommerce.Api.Modules;
using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// NTF backlog #7 (HO-NTF-001 ARCHITECTURE/SECURITY LOW): an empty or placeholder
/// <c>PublicSite:WebBaseUrl</c> used to be accepted silently in every non-Production environment,
/// so a reset/care/unsubscribe link built there would be relative and broken. Production refuses
/// to start; every other environment starts but logs a warning.
/// </summary>
public class PublicSiteStartupGuardTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    public async Task Empty_base_url_outside_Production_starts_but_logs_a_warning(string environment)
    {
        var logger = new CapturingLogger();

        await Guard(environment, "", logger).StartAsync(CancellationToken.None);

        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("PublicSite:WebBaseUrl", warning);
        Assert.Contains("is empty", warning);
        Assert.Contains(environment, warning);
    }

    [Theory]
    [InlineData("https://<your-domain>")]
    [InlineData("https://CHANGEME.example")]
    [InlineData("{{WEB_BASE_URL}}")]
    public async Task Placeholder_base_url_outside_Production_starts_but_logs_a_warning(string placeholder)
    {
        var logger = new CapturingLogger();

        await Guard("Staging", placeholder, logger).StartAsync(CancellationToken.None);

        Assert.Contains("looks like a placeholder", Assert.Single(logger.Warnings));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://<your-domain>")]
    public async Task Empty_or_placeholder_base_url_in_Production_fails_to_start(string value)
    {
        var logger = new CapturingLogger();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Guard("Production", value, logger).StartAsync(CancellationToken.None));

        Assert.Contains("PublicSite:WebBaseUrl", ex.Message);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task A_real_https_base_url_logs_no_warning_in_any_environment()
    {
        foreach (var environment in new[] { "Development", "Staging", "Production" })
        {
            var logger = new CapturingLogger();
            await Guard(environment, "https://portal.example.com", logger).StartAsync(CancellationToken.None);
            Assert.Empty(logger.Warnings);
        }
    }

    private static PublicSiteStartupGuardHostedService Guard(string environment, string webBaseUrl, ILogger<PublicSiteStartupGuardHostedService> logger)
        => new(new FakeEnvironment(environment), Options.Create(new PublicSiteOptions { WebBaseUrl = webBaseUrl }), logger);

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "CatEcommerce.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class CapturingLogger : ILogger<PublicSiteStartupGuardHostedService>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }
}
