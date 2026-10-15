using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-EMR-001 §16. Real HTTP through TestServer against this branch's own <c>catecommerce_emr</c> database.
/// Builds on <see cref="AppointmentsApiFactory"/> (every EMR test starts from a checked-in appointment made
/// through HO-ADM-001 / HO-APT-001 endpoints, §16.1 rule 1) and adds the clinical-file settings of §16.1
/// rule 3: a per-run storage root and temp directory and a per-run signing key. The scanner is
/// <c>EicarTestScanner</c> because the host is the <c>Testing</c> environment.
/// </summary>
public class ConsultationApiFactory : AppointmentsApiFactory
{
    // One subtree per factory instance, computed once so every WithWebHostBuilder()-derived factory shares it.
    private readonly string _isolatedRoot =
        Path.Combine(Path.GetTempPath(), "catecommerce-emr-tests", Guid.NewGuid().ToString("N"));

    /// <summary>EMR tests create clinical data that must never land in a shared dev database: they refuse to run
    /// unless <c>CATECOMMERCE_TEST_DB</c> names a per-branch database that is not <c>catecommerce_dev</c>.</summary>
    public ConsultationApiFactory()
    {
        var connection = Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB");
        if (string.IsNullOrWhiteSpace(connection) || connection.Contains("catecommerce_dev", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "CATECOMMERCE_TEST_DB must be set to the full Npgsql connection string of this branch's own database (e.g. catecommerce_emr), never catecommerce_dev.");
    }

    public string StoreRoot => Path.Combine(_isolatedRoot, "store");
    public string TempRoot => Path.Combine(_isolatedRoot, "temp");

    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        base.ConfigureAdditionalSettings(settings);

        settings["FileStorage:UrlSigningKey"] = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        settings["FileStorage:LocalStoreRoot"] = StoreRoot;
        settings["FileStorage:TempPath"] = TempRoot;
        settings["ErrorVerbosity"] = "ExceptionMessage";
        settings["RateLimiting:FileUpload:PermitLimit"] = "100000";
        settings["RateLimiting:FileServe:PermitLimit"] = "100000";
    }

    /// <summary>A second host with extra services replaced -- for the forced-failure hooks (I-B6, I-E5).</summary>
    public WebApplicationFactory<Program> WithServices(Action<IServiceCollection> configure) =>
        WithWebHostBuilder(builder => builder.ConfigureTestServices(configure));

    /// <summary>A second host with extra configuration -- for the host-start refusals (I-A2, I-D7).</summary>
    public WebApplicationFactory<Program> WithSettings(IDictionary<string, string?> settings) =>
        WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configBuilder) => configBuilder.AddInMemoryCollection(settings)));
}
