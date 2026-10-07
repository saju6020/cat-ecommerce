using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate (2026-09-26). HO-CAT-001 §16 I2–I10, I18 -- the base handover's own integration suite,
/// never written before this gate (CODE_REVIEW's carried HIGH). §16.1 rules apply: every test mints
/// its own owner/ids, never asserts emptiness, uses run-unique natural keys.
/// </summary>
public sealed class CatCrudLifecycleEndpointTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    // I2
    [Fact]
    public async Task RegisterCat_Valid_Returns201WithCatIdAndPersistedRow()
    {
        using var client = CreateClient();
        var ownerId = await SeedCustomerAsync();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId,
            name = "I2 Cat",
            species = "Cat",
            sex = "Male",
            dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-1),
            dateOfBirthPrecision = "Exact"
        }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var catId = body.GetProperty("catId").GetGuid();
        Assert.NotEqual(Guid.Empty, catId);

        // §16 I2 asks for the row in clinic.cat specifically, not just a 200 on a follow-up GET.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var exists = await db.Database.SqlQuery<int>(
                $"SELECT 1 AS \"Value\" FROM clinic.cat WHERE \"Id\" = {catId}")
            .AnyAsync();
        Assert.True(exists, "Expected a row in clinic.cat for the newly registered cat.");
    }

    // I3
    [Fact]
    public async Task RegisterCat_EmptyName_Returns400WithValidationResult()
    {
        using var client = CreateClient();
        var ownerId = await SeedCustomerAsync();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId,
            name = "",
            species = "Cat",
            sex = "Male",
            dateOfBirthPrecision = "Estimated"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(body.GetProperty("isValid").GetBoolean());
    }

    // I4
    [Fact]
    public async Task GetCat_ImmediatelyAfterRegister_ReturnsSameDataInSameRequestCycle()
    {
        using var client = CreateClient();
        var ownerId = await SeedCustomerAsync();
        var catId = await RegisterCatAsync(client, ownerId, name: "I4 Cat");

        var response = await client.GetAsync($"/api/cats/{catId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(catId, body.GetProperty("id").GetGuid());
        Assert.Equal(ownerId, body.GetProperty("ownerId").GetGuid());
        Assert.Equal("I4 Cat", body.GetProperty("name").GetString());
        Assert.Equal("Cat", body.GetProperty("species").GetString());
    }

    // I5
    [Fact]
    public async Task GetCat_UnknownId_Returns404()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.GetAsync($"/api/cats/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // I6
    [Fact]
    public async Task RegisterVaccine_DuplicateRunUniqueCode_Returns400WithVaccineCodeAlreadyExists()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);
        var code = $"I6-{Guid.NewGuid():N}"; // §16.1 rule 3 -- run-unique, never a fixed literal

        var first = await client.PostAsJsonAsync("/api/vaccines", new
        {
            code, name = "First", isCore = false, validityMonths = 12, description = (string?)null
        }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/vaccines", new
        {
            code, name = "Second", isCore = false, validityMonths = 12, description = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        var raw = await second.Content.ReadAsStringAsync();
        Assert.Contains("VaccineCodeAlreadyExists", raw);
    }

    // I7 -- see the sheet's "Gaps left deliberately" note: a genuine microsecond-level race cannot
    // be forced into one specific interleaving via black-box HTTP (the same caveat this codebase's
    // own ProfileEndpointTests I-E4/I-E5 record for Accounts). This asserts the legitimate outcome
    // set instead of a guaranteed 409: never a 500, and the final state is one of the attempted names.
    [Fact]
    public async Task ConcurrentProfileUpdates_NeverAServerErrorAndAtLeastOneSucceeds()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        Task<HttpResponseMessage> UpdateAsync(string name) => client.PutAsJsonAsync($"/api/cats/{catId}/profile", new
        {
            name, sex = "Female", dateOfBirthPrecision = "Estimated", breed = (string?)null, colourAndMarkings = (string?)null
        }, Json);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => UpdateAsync($"Concurrent {i}")));
        var statuses = results.Select(r => r.StatusCode).ToList();

        Assert.All(statuses, s => Assert.True(
            s is HttpStatusCode.OK or HttpStatusCode.Conflict,
            $"Unexpected status {s} -- neither a successful update nor a concurrency conflict (ADR-016)."));
        Assert.Contains(HttpStatusCode.OK, statuses);

        var getResponse = await client.GetAsync($"/api/cats/{catId}");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.StartsWith("Concurrent ", body.GetProperty("name").GetString());
    }

    // I8
    [Fact]
    public async Task RecordCatWeight_ThreeTimes_ReturnsAscendingSeriesAndLatestMatchesProfile()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var weights = new[] { (Date: today.AddDays(-2), Kg: 3.5m), (Date: today.AddDays(-1), Kg: 3.8m), (Date: today, Kg: 4.1m) };
        foreach (var (date, kg) in weights)
        {
            var response = await client.PostAsJsonAsync($"/api/cats/{catId}/weights", new
            {
                kilograms = kg, recordedOn = date, notes = (string?)null
            }, Json);
            response.EnsureSuccessStatusCode();
        }

        var historyResponse = await client.GetAsync($"/api/cats/{catId}/weights");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var historyBody = await historyResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = historyBody.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(3, items.Count);
        var recordedDates = items.Select(i => DateOnly.Parse(i.GetProperty("recordedOn").GetString()!)).ToList();
        Assert.Equal(recordedDates.OrderBy(d => d), recordedDates); // ascending

        var profileResponse = await client.GetAsync($"/api/cats/{catId}");
        var profileBody = await profileResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(4.1m, profileBody.GetProperty("latestWeightKg").GetDecimal());
    }

    // I9
    [Fact]
    public async Task RecordCatVaccination_ExpiredBatch_Returns400WithExpiredVaccineBatch()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var vaccineId = await RegisterCoreVaccineAsync(client, validityMonths: 12);
        var vetId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Vet);

        var administeredOn = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
        {
            vaccineId,
            batchReference = "EXPIRED-1",
            batchExpiresOn = administeredOn.AddDays(-1), // expired before administration
            administeredOn,
            administeredByVetId = vetId,
            notes = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("ExpiredVaccineBatch", raw);
    }

    // I10
    [Fact]
    public async Task RecordCatVaccination_Valid_Returns201WithNextDueOnMatchingCatalogueValidity()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        const int validityMonths = 12;
        var vaccineId = await RegisterCoreVaccineAsync(client, validityMonths);
        var vetId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Vet);

        var administeredOn = DateOnly.FromDateTime(DateTime.UtcNow);
        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
        {
            vaccineId,
            batchReference = "VALID-1",
            batchExpiresOn = administeredOn.AddYears(1),
            administeredOn,
            administeredByVetId = vetId,
            notes = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var statusResponse = await client.GetAsync($"/api/cats/{catId}/vaccination-status");
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var vaccineEntry = statusBody.GetProperty("vaccines").EnumerateArray()
            .First(v => v.GetProperty("vaccineId").GetGuid() == vaccineId);
        var nextDueOn = DateOnly.Parse(vaccineEntry.GetProperty("nextDueOn").GetString()!);
        Assert.Equal(administeredOn.AddMonths(validityMonths), nextDueOn);
    }

    // I18
    [Fact]
    public async Task UpdateCatProfile_DeceasedCat_Returns400WithCatIsDeceased()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        var deceasedResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/deceased", new
        {
            deceasedOn = DateOnly.FromDateTime(DateTime.UtcNow), note = (string?)null
        }, Json);
        deceasedResponse.EnsureSuccessStatusCode();

        var response = await client.PutAsJsonAsync($"/api/cats/{catId}/profile", new
        {
            name = "Should not apply", sex = "Female", dateOfBirthPrecision = "Estimated",
            breed = (string?)null, colourAndMarkings = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("CatIsDeceased", raw);
    }

    /// <summary>Registers a core, active vaccine with a run-unique code (§16.1 rule 3) and returns
    /// its id, on an already-signed-in <paramref name="client"/>.</summary>
    private static async Task<Guid> RegisterCoreVaccineAsync(HttpClient client, int validityMonths)
    {
        var response = await client.PostAsJsonAsync("/api/vaccines", new
        {
            code = $"CORE-{Guid.NewGuid():N}",
            name = "Test Core Vaccine",
            isCore = true,
            validityMonths,
            description = (string?)null
        }, Json);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("vaccineId").GetGuid();
    }
}
