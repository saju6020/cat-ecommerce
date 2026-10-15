using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-PHM-001 §16. Real HTTP through TestServer, against this branch's own <c>catecommerce_phm</c> database (set <c>CATECOMMERCE_TEST_DB</c> to its
/// FULL Npgsql connection string). Builds on <see cref="ConsultationApiFactory"/>: every Pharmacy test starts from a checked-in appointment and an
/// open visit made through HO-ADM-001 / HO-APT-001 / HO-EMR-001 endpoints (§16.1 rule 3). Every worker is off -- tests call <c>PharmacyWorkCycle</c> and
/// <c>NotificationWorkCycle</c> directly. The register's triggers are real and a test never disables one outside I-D7's rolled-back transaction.
///
/// Pharmacy tests create clinical and controlled-drug data that is never cleaned up (the triggers refuse it), so they refuse to run unless
/// <c>CATECOMMERCE_TEST_DB</c> names a per-branch database that is neither <c>catecommerce_dev</c> nor <c>catecommerce_test</c>.
/// </summary>
public class PharmacyApiFactory : ConsultationApiFactory
{
    public PharmacyApiFactory()
    {
        var connection = Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("CATECOMMERCE_TEST_DB must be set to the full Npgsql connection string of this branch's own database (catecommerce_phm).");

        if (connection.Contains("catecommerce_dev", StringComparison.OrdinalIgnoreCase) || connection.Contains("catecommerce_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Pharmacy tests must never run against catecommerce_dev or catecommerce_test.");
    }

    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        base.ConfigureAdditionalSettings(settings);

        settings["Pharmacy:Worker:Enabled"] = "false";
        settings["Inventory:Worker:Enabled"] = "false";
        settings["Shop:Worker:Enabled"] = "false";
        settings["Shop:Catalogue:PermitLimit"] = "100000";
        settings["Pharmacy:Witness:PermitLimit"] = "100000";
        settings["Pharmacy:RepeatRequest:PermitLimit"] = "100000";
    }

    /// <summary>A host whose rate limits for witness-bearing endpoints and customer repeat requests are tightened, for the tests that prove they are limited.</summary>
    public WebApplicationFactory<Program> WithTightPharmacyRateLimits() => WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, configBuilder) =>
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pharmacy:Witness:PermitLimit"] = "2",
                ["Pharmacy:Witness:WindowMinutes"] = "10",
                ["Pharmacy:RepeatRequest:PermitLimit"] = "2",
                ["Pharmacy:RepeatRequest:WindowMinutes"] = "10",
            })));
}
