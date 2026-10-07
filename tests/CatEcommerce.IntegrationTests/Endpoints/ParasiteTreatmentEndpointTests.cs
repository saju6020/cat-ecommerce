using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-CAT-001 §16 I10a -- owner decision 2026-09-25 (HO-CAT-002-ACCEPTANCE BLOCKER 1 / A13).
/// </summary>
public sealed class ParasiteTreatmentEndpointTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    // I10a
    [Fact]
    public async Task RecordParasiteTreatment_Valid_Returns201WithTreatmentIdAndComputedNextDueOn()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var staffId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Nurse);

        var administeredOn = DateOnly.FromDateTime(DateTime.UtcNow);
        const int intervalDays = 90;

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/parasite-treatments", new
        {
            treatmentType = "Deworming",
            productName = "Drontal",
            batchReference = "BATCH-1",
            administeredOn,
            administeredByStaffId = staffId,
            intervalDays,
            notes = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        // Exactly { treatmentId, nextDueOn } -- no catId (HO-CAT-001 §12.4).
        var propertyNames = new List<string>();
        foreach (var property in body.EnumerateObject())
            propertyNames.Add(property.Name);
        Assert.Equal(["treatmentId", "nextDueOn"], propertyNames);

        var treatmentId = body.GetProperty("treatmentId").GetGuid();
        Assert.NotEqual(Guid.Empty, treatmentId);

        var nextDueOn = DateOnly.Parse(body.GetProperty("nextDueOn").GetString()!);
        Assert.Equal(administeredOn.AddDays(intervalDays), nextDueOn);
    }

    // I-C10-1 (TC-HO-CAT-001-C10) -- HO-CAT-001 §16 names only the valid path (I10a) for C10;
    // this failure-path row has no §16 ID of its own. Checked against §12.4 and CatsController's
    // own pattern that every POST command failure maps to 400, never 404 (only the two
    // GET-by-id query endpoints in this controller return 404).
    [Fact]
    public async Task RecordParasiteTreatment_UnknownCat_Returns400WithCatNotFound()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);
        var unknownCatId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync($"/api/cats/{unknownCatId}/parasite-treatments", new
        {
            treatmentType = "Deworming",
            productName = "Drontal",
            batchReference = "BATCH-1",
            administeredOn = DateOnly.FromDateTime(DateTime.UtcNow),
            administeredByStaffId = Guid.NewGuid(),
            intervalDays = 90,
            notes = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("CatNotFound", raw);
    }

    // I-C10-2 (TC-HO-CAT-001-C10) -- §8.2's "not deceased" guard (EnsureAlive()) still applies to
    // C10 after the amendment; the amendment only changes the success body, not this guard.
    [Fact]
    public async Task RecordParasiteTreatment_DeceasedCat_Returns400WithCatIsDeceased()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var staffId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Nurse);

        var deceasedResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/deceased", new
        {
            deceasedOn = DateOnly.FromDateTime(DateTime.UtcNow),
            note = (string?)null
        }, Json);
        deceasedResponse.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/parasite-treatments", new
        {
            treatmentType = "Deworming",
            productName = "Drontal",
            batchReference = "BATCH-1",
            administeredOn = DateOnly.FromDateTime(DateTime.UtcNow),
            administeredByStaffId = staffId,
            intervalDays = 90,
            notes = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("CatIsDeceased", raw);
    }

    // I-C10-3 (TC-HO-CAT-001-C10) -- proves F3 (ValidateCommand = true) actually runs
    // RecordCatParasiteTreatmentCommandValidator's IntervalDays InclusiveBetween(1, 730) (§11.3).
    [Fact]
    public async Task RecordParasiteTreatment_IntervalDaysOutOfRange_Returns400WithValidationError()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/parasite-treatments", new
        {
            treatmentType = "Deworming",
            productName = "Drontal",
            batchReference = "BATCH-1",
            administeredOn = DateOnly.FromDateTime(DateTime.UtcNow),
            administeredByStaffId = Guid.NewGuid(),
            intervalDays = 0, // outside the validator's 1-730 range
            notes = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("IntervalDays", raw);
    }
}
