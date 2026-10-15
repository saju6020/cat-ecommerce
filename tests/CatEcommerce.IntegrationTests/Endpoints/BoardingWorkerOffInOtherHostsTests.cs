using CatEcommerce.Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate, regression for CODE_REVIEW HIGH (2026-10-15): `BoardingWorker` is hosted unconditionally and defaults to ON, so every OTHER area's test host (this one is the Accounts host, not Boarding's) must
/// have it switched off by configuration -- otherwise it lapses, confirms, invoices and writes audit rows for other classes' stays with that host's own (often faked) clock.
/// The Boarding factory sets the flag itself, so nothing else pinned the base factory / appsettings.Testing.json.
/// </summary>
public sealed class BoardingWorkerOffInOtherHostsTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    [Fact]
    public void TheBoardingWorker_IsDisabledByConfiguration_InAnotherAreasTestHost()
    {
        using var scope = Factory.Services.CreateScope();

        Assert.False(scope.ServiceProvider.GetRequiredService<IOptions<BoardingOptions>>().Value.Worker.Enabled);
        Assert.Contains(Factory.Services.GetServices<IHostedService>(), s => s.GetType().Name == "BoardingWorker"); // still hosted -- it just returns at once
    }
}
