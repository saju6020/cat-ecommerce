namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-SRG-001 §16. Real HTTP through TestServer, against this branch's own <c>catecommerce_srg</c> database (set <c>CATECOMMERCE_TEST_DB</c> to its FULL
/// Npgsql connection string). Builds on <see cref="PharmacyApiFactory"/>: every Surgery test starts from a branch made through HO-ADM-001 endpoints with a
/// theatre, a rostered vet surgeon and a rostered nurse assistant (§16.1), and the controlled-drug / ledger ports are the REAL implementations of
/// HO-PHM-001 and HO-IMP-001. Every worker is off -- tests call <c>NotificationWorkCycle</c> directly.
///
/// Surgery tests create clinical, anaesthesia and controlled-drug data that is never cleaned up (the triggers refuse it), so they refuse to run unless
/// <c>CATECOMMERCE_TEST_DB</c> names a per-branch database that is neither <c>catecommerce_dev</c> nor <c>catecommerce_test</c>.
/// </summary>
public class SurgeryApiFactory : PharmacyApiFactory
{
    public SurgeryApiFactory()
    {
        var connection = Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("CATECOMMERCE_TEST_DB must be set to the full Npgsql connection string of this branch's own database (catecommerce_srg).");

        if (connection.Contains("catecommerce_dev", StringComparison.OrdinalIgnoreCase) || connection.Contains("catecommerce_test", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Surgery tests must never run against catecommerce_dev or catecommerce_test.");
    }

    protected override void ConfigureAdditionalSettings(IDictionary<string, string?> settings)
    {
        base.ConfigureAdditionalSettings(settings);
        settings["Surgery:Worker:Enabled"] = "false";
    }
}
