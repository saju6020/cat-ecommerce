using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-POR-001 §16 for the Prescriptions section (Phase B, HO-PHM-001): the prescription is issued through Pharmacy's own vet endpoint on Pharmacy's host
/// and clinical fixture (§16.1 rule 2); the dashboard is the real <c>GET /api/portal/dashboard</c> of the same host. Every test mints its own world.
/// </summary>
public sealed class PortalPrescriptionsEndpointTests(PharmacyApiFactory factory) : PharmacyIntegrationTestBase(factory)
{
    private const string MedicineName = "Portalmeloxicam";

    private async Task<(PharmacyWorld World, Guid PrescriptionId, DateOnly EndsOn)> IssueAsync(PharmacyWorld? existing = null)
    {
        var world = existing ?? await SeedPharmacyWorldAsync(withLocations: false);
        await RecordWeightAsync(world.Vet, world.CatId, 4.0m);
        var medicineId = await CreateMedicineAsync(world.Admin, MedicineBody(name: MedicineName + Guid.NewGuid().ToString("N")[..6]));
        var response = await world.Vet.PostAsJsonAsync($"/api/staff/cats/{world.CatId}/prescriptions", PharmacyPrescribingEndpointTests.IssueBody(world.VisitId, medicineId), Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("prescriptionId").GetGuid();
        var mine = await (await world.Customer.GetAsync($"/api/customers/me/prescriptions?scope=active")).Content.ReadFromJsonAsync<JsonElement>(Json);
        var ends = DateOnly.Parse(mine.EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == id).GetProperty("courseEndsOn").GetString()!);
        return (world, id, ends);
    }

    private static async Task<JsonElement> DashboardAsync(HttpClient customer)
    {
        var response = await customer.GetAsync("/api/portal/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    [Fact]
    public async Task I_B2_Prescriptions_AnActivePrescriptionIsSummarisedPerCat_WithNoClinicalContent()
    {
        var (world, _, endsOn) = await IssueAsync();

        var response = await world.Customer.GetAsync("/api/portal/dashboard");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var board = JsonDocument.Parse(raw).RootElement;

        var section = board.GetProperty("prescriptions");
        Assert.Equal("Ready", section.GetProperty("state").GetString());
        var row = Assert.Single(section.GetProperty("data").GetProperty("byCat").EnumerateArray());
        Assert.Equal(world.CatId, row.GetProperty("catId").GetGuid());
        Assert.Equal(1, row.GetProperty("activeCount").GetInt32());
        Assert.Equal(endsOn.ToString("yyyy-MM-dd"), row.GetProperty("earliestEndsOn").GetString());
        Assert.Equal(["activeCount", "catId", "catName", "earliestEndsOn"], row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));

        // I-B7 / ADR-262: no medicine, dose, frequency, route or instruction text anywhere in the dashboard.
        Assert.DoesNotContain(MedicineName, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Give with food", raw);
        foreach (var key in new[] { "\"dose\"", "\"frequency\"", "\"strength\"", "\"instructions\"", "\"route\"", "\"medicine\"" })
            Assert.DoesNotContain(key, raw, StringComparison.OrdinalIgnoreCase);
    }

    // I-B3 -- ADR-261 (IDOR)
    [Fact]
    public async Task I_B3_Prescriptions_AnotherCustomerSeesNoneOfIt()
    {
        var (_, _, _) = await IssueAsync();
        var other = await SeedPharmacyWorldAsync(withLocations: false);

        var board = await DashboardAsync(other.Customer);

        Assert.Equal("Ready", board.GetProperty("prescriptions").GetProperty("state").GetString());
        Assert.Empty(board.GetProperty("prescriptions").GetProperty("data").GetProperty("byCat").EnumerateArray());
    }

    // SECURITY L1 / POR backlog 8: handover A4 -- a deceased cat never appears, including through its still-active prescription.
    [Fact]
    public async Task I_B2_Prescriptions_ADeceasedCatsActivePrescription_IsNotShown()
    {
        var (world, _, _) = await IssueAsync();
        await MarkCatDeceasedAsync(world.Admin, world.CatId);

        var board = await DashboardAsync(world.Customer);

        Assert.Equal("Ready", board.GetProperty("prescriptions").GetProperty("state").GetString());
        Assert.Empty(board.GetProperty("prescriptions").GetProperty("data").GetProperty("byCat").EnumerateArray());
    }

    [Fact]
    public async Task Prescriptions_ACustomerWithNone_GetsAReadySectionWithAnEmptyList()
    {
        var world = await SeedPharmacyWorldAsync(withLocations: false);

        var board = await DashboardAsync(world.Customer);

        Assert.Equal("Ready", board.GetProperty("prescriptions").GetProperty("state").GetString());
        Assert.Empty(board.GetProperty("prescriptions").GetProperty("data").GetProperty("byCat").EnumerateArray());
    }
}
