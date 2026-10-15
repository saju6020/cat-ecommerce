using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// ARCHITECTURE gate, fourth pass, MEDIUM: originally recorded while <c>Platform.Infrastructure.Host.WebApi</c>
/// was still referenced pending an integrator decision (no code called into it after
/// <c>UseHttpPipeline()</c> was dropped, but MVC's "application part" mechanism means a referenced
/// assembly can still contribute controllers to <c>MapControllers()</c> even without a direct call).
/// The integrator (wave A -> B tidy-up) confirmed via grep and a build without the package that no
/// type from it is used anywhere in this solution, and removed the
/// <c>PackageReference</c> from <c>CatEcommerce.Api.csproj</c> entirely. This test is kept as a
/// regression guard: it still enumerates every mapped controller endpoint and asserts none of them
/// come from that assembly, which would now also fail to build if the package -- and with it, any
/// of its controllers -- were ever reintroduced.
/// </summary>
public sealed class HostCompositionTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    [Fact]
    public void MapControllers_ExposesNoControllerFromThePlatformHostWebApiAssembly()
    {
        using var client = CreateClient(); // forces the host to build, so EndpointDataSource is populated
        _ = client;

        var endpointDataSource = Factory.Services.GetRequiredService<EndpointDataSource>();

        var controllerAssemblies = endpointDataSource.Endpoints
            .Select(e => e.Metadata.GetMetadata<ControllerActionDescriptor>())
            .Where(d => d is not null)
            .Select(d => d!.ControllerTypeInfo.Assembly.GetName().Name)
            .Distinct()
            .ToList();

        Assert.DoesNotContain("Platform.Infrastructure.Host.WebApi", controllerAssemblies);
    }

    // I-A1. Program.cs:41-42 sets ValidateScopes=true / ValidateOnBuild=true on the default service
    // provider -- ValidateOnBuild specifically means a single unresolvable dependency anywhere in
    // the graph (a missing registration, a Scoped service injected into a Singleton, ...) throws
    // the moment the host is BUILT, not lazily on first use. Every other test in this suite already
    // depends on this succeeding (WebApplicationFactory<Program>.CreateClient() builds the real
    // host), but this makes the requirement explicit and independently checkable rather than only
    // an implicit side effect of everything else passing.
    [Fact]
    public void Host_BuildsAndStartsWithNoException_EveryRegistrationResolves()
    {
        using var freshFactory = new AccountsApiFactory();
        var exception = Record.Exception(() => freshFactory.CreateClient());
        Assert.Null(exception);
    }

    // I-A2. AccountStartupGuardHostedService (AccountsModule.cs, "§20.9 / §12.9 / I-A2") aborts host
    // startup outside Development/Testing when either a development-only seam is still configured --
    // Captcha:TestAnswer (a fixed, guessable CAPTCHA answer) or DevUser:Enabled (a fixed, no-password
    // identity). Both are exercised against Production specifically, since the guard's own condition
    // is "not Development and not Testing".
    [Theory]
    [InlineData("Captcha:TestAnswer", "TEST1", "DevUser:Enabled", "false")]
    [InlineData("DevUser:Enabled", "true", "Captcha:TestAnswer", "")]
    public void HostInProduction_WithADevelopmentOnlySeamConfigured_FailsToStart(
        string offendingKey, string offendingValue, string otherKey, string otherValueKeptSafe)
    {
        // The base AccountsApiFactory unconditionally sets Captcha:TestAnswer="TEST1" for the
        // Testing environment; overriding UseEnvironment alone does not clear that inherited
        // configuration value, since WithWebHostBuilder layers new configuration on top rather than
        // replacing it. Each case therefore also pins the OTHER seam to a safe value, so it is only
        // ever the named offendingKey that can be responsible for the failure this test checks for.
        //
        // HO-NTF-001 gate fix (2026-09-27): "Production" (unlike "Testing") loads no
        // appsettings.Testing.json, so NotificationOptions' own [Required]/https/non-placeholder
        // ValidateOnStart checks (Notifications:ApiBaseUrl) and PublicSiteStartupGuardHostedService's
        // own "required in Production" check (PublicSite:WebBaseUrl, the shared web base URL --
        // integrator instruction, post HO-CAT-001-gates merge) would otherwise ALSO fail here --
        // pinned to real-shaped https URLs so this test's Production host fails ONLY for the one
        // seam it names.
        using var productionFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CatEcommerce"] = "Host=localhost;Port=5432;Database=catecommerce_test;Username=postgres;Password=postgres",
                    ["PublicSite:WebBaseUrl"] = "https://portal.example.com",
                    ["Notifications:ApiBaseUrl"] = "https://api.example.com",
                    [offendingKey] = offendingValue,
                    [otherKey] = otherValueKeptSafe
                });
            });
        });

        var exception = Record.Exception(() => productionFactory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains(offendingKey.Split(':')[0], exception.ToString());
    }

    // ACCEPTANCE gate A22 / CODE_REVIEW gate, fifth pass (2026-09-27). PublicSiteStartupGuardHostedService
    // (CatProfilesModule.cs) validates PublicSite:WebBaseUrl whenever it is non-empty, in every
    // environment -- these structural checks don't depend on Development/Testing/Production, so
    // they're exercised in the base Testing environment rather than switching to Production.
    [Theory]
    [InlineData("https://evil.example@trusted.example", "userinfo")] // CODE_REVIEW MEDIUM, fifth
    // pass: Uri.TryCreate happily parses this as host "trusted.example" with UserInfo
    // "evil.example" -- a browser (and most humans) read the userinfo-prefixed string as the
    // domain, so this would be a phishing-shaped QR even though the URI class considers it
    // well-formed.
    [InlineData("not-a-url", "absolute http(s)")] // not absolute at all
    [InlineData("ftp://trusted.example", "absolute http(s)")] // absolute, but not http(s)
    [InlineData("https://trusted.example/cats", "path, query or fragment")]
    [InlineData("https://trusted.example?x=1", "path, query or fragment")]
    [InlineData("https://trusted.example#section", "path, query or fragment")]
    public void HostInTesting_WithAMalformedPublicSiteWebBaseUrl_FailsToStart(string badUrl, string expectedMessageFragment)
    {
        using var malformedFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configBuilder) =>
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PublicSite:WebBaseUrl"] = badUrl
                })));

        var exception = Record.Exception(() => malformedFactory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains(expectedMessageFragment, exception.ToString());
    }

    // The same well-formed value that every malformed case above is a variation of must NOT throw,
    // so the theory above is proven to test the actual guard rather than something that always fails.
    [Fact]
    public void HostInTesting_WithAWellFormedPublicSiteWebBaseUrl_StartsSuccessfully()
    {
        using var wellFormedFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configBuilder) =>
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["PublicSite:WebBaseUrl"] = "https://trusted.example"
                })));

        var exception = Record.Exception(() => wellFormedFactory.CreateClient());

        Assert.Null(exception);
    }

    // I-A2-style, Production-only rules: https is required outside Development/Testing, and the
    // value is required at all in Production (both may be empty in Development/Testing).
    [Fact]
    public void HostInProduction_WithAnHttpPublicSiteWebBaseUrl_FailsToStart()
    {
        using var productionFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CatEcommerce"] = "Host=localhost;Port=5432;Database=catecommerce_test;Username=postgres;Password=postgres",
                    ["Captcha:TestAnswer"] = "",
                    ["DevUser:Enabled"] = "false",
                    // HO-NTF-001 gate fix (2026-09-27): see the sibling Production case below for why.
                    ["Notifications:ApiBaseUrl"] = "https://api.example.com",
                    ["PublicSite:WebBaseUrl"] = "http://trusted.example"
                });
            });
        });

        var exception = Record.Exception(() => productionFactory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("https", exception.ToString());
    }

    [Fact]
    public void HostInProduction_WithNoPublicSiteWebBaseUrl_FailsToStart()
    {
        using var productionFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:CatEcommerce"] = "Host=localhost;Port=5432;Database=catecommerce_test;Username=postgres;Password=postgres",
                    ["Captcha:TestAnswer"] = "",
                    ["DevUser:Enabled"] = "false",
                    // HO-NTF-001 gate fix (2026-09-27): pinned so NotificationOptions' own
                    // [Required] ApiBaseUrl check does not ALSO fail here and short-circuit host
                    // startup before PublicSiteStartupGuardHostedService's own check ever runs --
                    // options ValidateOnStart failures are not aggregated across option types; the
                    // first one to fail is the only exception this factory ever throws.
                    ["Notifications:ApiBaseUrl"] = "https://api.example.com",
                    ["PublicSite:WebBaseUrl"] = ""
                });
            });
        });

        var exception = Record.Exception(() => productionFactory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("PublicSite:WebBaseUrl", exception.ToString());
    }
}
