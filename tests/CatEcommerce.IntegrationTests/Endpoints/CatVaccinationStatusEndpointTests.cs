using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate (2026-09-26). HO-CAT-001 §16 I11 -- US-CAT-07, matching U34–U37 against real rows over
/// HTTP, across the three per-vaccine states.
///
/// <c>GetCatVaccinationStatusQueryHandler</c> computes over the catalogue's <b>global</b> set of
/// active core vaccines (§12.3 step 2), and <c>catecommerce_cat</c> is shared with every other test
/// class in this run, most of which execute concurrently (xUnit's default parallelism runs each
/// test class's own collection independently) -- some of those classes register their own core
/// vaccines mid-run. An assertion on <c>overallStatus</c> (the worst across every core vaccine in
/// the whole catalogue, §12.3 step 4) is therefore not safe to pin to one value here: a concurrently
/// running test class can add a brand-new, never-administered-by-this-cat core vaccine between this
/// test's own setup and its final GET, which would force <c>overallStatus</c> to <c>Overdue</c>
/// regardless of what this test did (this was observed directly: an early version of this file
/// asserting <c>overallStatus</c> failed intermittently with exactly that shape). Each case below
/// instead asserts the status of <b>this test's own vaccine entry</b> in the per-vaccine
/// <c>vaccines</c> array -- deterministic and immune to any other test's concurrently-registered
/// vaccine, while still exercising the real read model (§12.3 steps 1–3) end to end over HTTP,
/// which is what §16 I11 actually asks for ("matches U34–U37 against real rows").
/// </summary>
public sealed class CatVaccinationStatusEndpointTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    // I11 (Overdue / NeverAdministered) -- robust even against overallStatus, since "worst wins"
    // and a never-administered core vaccine can only ever push the overall status to Overdue too.
    [Fact]
    public async Task GetCatVaccinationStatus_NoVaccinationsRecorded_ReturnsOverdueWithNeverAdministeredReason()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var vaccineId = await RegisterCoreVaccineAsync(client, validityMonths: 12);

        var response = await client.GetAsync($"/api/cats/{catId}/vaccination-status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Overdue", body.GetProperty("overallStatus").GetString());
        Assert.False(body.GetProperty("bookingEligible").GetBoolean());

        var entry = FindVaccineEntry(body, vaccineId);
        Assert.Equal("Overdue", entry.GetProperty("status").GetString());
        Assert.Equal("NeverAdministered", entry.GetProperty("reason").GetString());
    }

    // I11 (Current)
    [Fact]
    public async Task GetCatVaccinationStatus_VaccineAdministeredWellWithinValidity_ReturnsCurrentForThatVaccine()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var vetId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Vet);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        const int validityMonths = 12;

        var vaccineId = await RegisterCoreVaccineAsync(client, validityMonths);
        await AdministerAsync(client, catId, vaccineId, today, validityMonths, vetId);

        var response = await client.GetAsync($"/api/cats/{catId}/vaccination-status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var entry = FindVaccineEntry(body, vaccineId);
        Assert.Equal("Current", entry.GetProperty("status").GetString());
        Assert.Equal(today.AddMonths(validityMonths), DateOnly.Parse(entry.GetProperty("nextDueOn").GetString()!));
    }

    // I11 (DueSoon)
    [Fact]
    public async Task GetCatVaccinationStatus_VaccineDueWithinWindow_ReturnsDueSoonForThatVaccine()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var vetId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Vet);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        const int validityMonths = 1;

        var vaccineId = await RegisterCoreVaccineAsync(client, validityMonths);
        // 1-month validity, administered 20 days ago -> next due in roughly 10-11 days: always
        // inside the default 30-day window (FR-CAT-7) and never negative (overdue).
        await AdministerAsync(client, catId, vaccineId, today.AddDays(-20), validityMonths, vetId);

        var response = await client.GetAsync($"/api/cats/{catId}/vaccination-status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var entry = FindVaccineEntry(body, vaccineId);
        Assert.Equal("DueSoon", entry.GetProperty("status").GetString());
    }

    private static JsonElement FindVaccineEntry(JsonElement statusBody, Guid vaccineId) =>
        statusBody.GetProperty("vaccines").EnumerateArray()
            .First(v => v.GetProperty("vaccineId").GetGuid() == vaccineId);

    private static async Task<Guid> RegisterCoreVaccineAsync(HttpClient client, int validityMonths)
    {
        var response = await client.PostAsJsonAsync("/api/vaccines", new
        {
            code = $"STATUS-{Guid.NewGuid():N}", name = "Status Test Vaccine", isCore = true,
            validityMonths, description = (string?)null
        }, Json);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("vaccineId").GetGuid();
    }

    private static async Task AdministerAsync(HttpClient client, Guid catId, Guid vaccineId, DateOnly administeredOn, int validityMonths, Guid vetId)
    {
        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
        {
            vaccineId,
            batchReference = $"B-{Guid.NewGuid():N}"[..20],
            batchExpiresOn = administeredOn.AddMonths(Math.Max(validityMonths, 1) + 1),
            administeredOn,
            administeredByVetId = vetId,
            notes = (string?)null
        }, Json);
        response.EnsureSuccessStatusCode();
    }
}
