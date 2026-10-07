using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-CAT-001 ACCEPTANCE gate, first pass (2026-09-27, `docs/gates/HO-CAT-001-ACCEPTANCE.md`). Six
/// criteria (A1, A8, A9, A11, A17, A24) were marked MET only on the strength of scratch probes the
/// gate itself wrote (P1, P3-P7) because the TC sheet's cited tests did not actually assert what
/// their row claimed. This turns each probe into a real, checked-in test, one method per probe.
/// </summary>
public sealed class CatAcceptanceGateEndpointTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    // P3 / A1: "POST with breed, colour, DOB, sex and precision, then GET. All of them round-trip,
    // and ownerId matches." I2/I4 (CatCrudLifecycleEndpointTests) send and assert neither breed nor
    // colourAndMarkings.
    [Fact]
    public async Task RegisterCat_WithBreedColourSexAndDateOfBirth_AllRoundTripOnGet()
    {
        using var client = CreateClient();
        var ownerId = await SeedCustomerAsync();
        await SignInAsFreshAdministratorAsync(client);

        var dateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-3);
        var registerResponse = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId,
            name = "P3 Cat",
            species = "Cat",
            sex = "Male",
            dateOfBirth,
            dateOfBirthPrecision = "Exact",
            breed = "Domestic Shorthair",
            colourAndMarkings = "Tabby with white paws"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var catId = (await registerResponse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("catId").GetGuid();

        var getResponse = await client.GetAsync($"/api/cats/{catId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.Equal(ownerId, body.GetProperty("ownerId").GetGuid());
        Assert.Equal("Male", body.GetProperty("sex").GetString());
        Assert.Equal("Exact", body.GetProperty("dateOfBirthPrecision").GetString());
        Assert.Equal(dateOfBirth, DateOnly.Parse(body.GetProperty("dateOfBirth").GetString()!));
        Assert.Equal("Domestic Shorthair", body.GetProperty("breed").GetString());
        Assert.Equal("Tabby with white paws", body.GetProperty("colourAndMarkings").GetString());
    }

    // P4 / A8: "a cat with no allergies returns hasRecordedAllergies: false and allergies: []" --
    // the "none recorded" case itself, distinct from the already-tested "has allergies" case
    // (CatProfileProjectionEndpointTests.GetCat_FullProfile_ProjectsEveryFieldCorrectly).
    [Fact]
    public async Task GetCat_NoAllergiesEverRecorded_HasRecordedAllergiesFalseWithEmptyArray()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        var response = await client.GetAsync($"/api/cats/{catId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.False(body.GetProperty("hasRecordedAllergies").GetBoolean());
        Assert.Equal(0, body.GetProperty("allergies").GetArrayLength());
    }

    // P5 / A9: "GET /api/cats?ownerId= returns the flagged cat with isHighRisk: true" -- plus
    // handlingNote, now that the list read model carries it (HO-CAT-001 §1 Amended 2026-09-27,
    // owner decision (c), correcting §12.3 to match §4.1/HO-CAT-002 §3).
    [Fact]
    public async Task GetCats_FlaggedHighRiskCat_ListItemHasIsHighRiskAndHandlingNote()
    {
        using var client = CreateClient();
        var ownerId = await SeedCustomerAsync();
        var catId = await RegisterCatAsync(client, ownerId);

        const string handlingNote = "Bites when restrained -- muzzle required";
        var flagResponse = await client.PutAsJsonAsync($"/api/cats/{catId}/high-risk-flag", new
        {
            handlingNote, reason = "Aggressive during last visit"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, flagResponse.StatusCode);

        var listResponse = await client.GetAsync($"/api/cats?ownerId={ownerId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var body = await listResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        var item = body.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("id").GetGuid() == catId);

        Assert.True(item.GetProperty("isHighRisk").GetBoolean());
        Assert.Equal(handlingNote, item.GetProperty("handlingNote").GetString());
    }

    // P6 / A11: "each of these returns 400: empty batchReference, Guid.Empty vet
    // (AdministeredByVetId), and a Nurse named as the vet (StaffAccountNotVeterinarian)."
    [Fact]
    public async Task RecordCatVaccination_EmptyBatchReference_Returns400OnBatchReference()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var vaccineId = await RegisterCoreVaccineAsync(client);
        var vetId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Vet);

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
        {
            vaccineId,
            batchReference = "",
            batchExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            administeredOn = DateOnly.FromDateTime(DateTime.UtcNow),
            administeredByVetId = vetId,
            notes = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("BatchReference", raw);
    }

    [Fact]
    public async Task RecordCatVaccination_EmptyVetId_Returns400OnAdministeredByVetId()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var vaccineId = await RegisterCoreVaccineAsync(client);

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
        {
            vaccineId,
            batchReference = "BATCH-EMPTY-VET",
            batchExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            administeredOn = DateOnly.FromDateTime(DateTime.UtcNow),
            administeredByVetId = Guid.Empty,
            notes = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("AdministeredByVetId", raw);
    }

    [Fact]
    public async Task RecordCatVaccination_NurseNamedAsVet_Returns400WithStaffAccountNotVeterinarian()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var vaccineId = await RegisterCoreVaccineAsync(client);
        var nurseId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Nurse);

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
        {
            vaccineId,
            batchReference = "BATCH-NURSE-VET",
            batchExpiresOn = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(1),
            administeredOn = DateOnly.FromDateTime(DateTime.UtcNow),
            administeredByVetId = nurseId,
            notes = (string?)null
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("StaffAccountNotVeterinarian", raw);
    }

    // P7 / A17: "the CatOwnershipTransferred audit row's before/after values contain both the
    // previous and the new owner id."
    [Fact]
    public async Task TransferCatOwnership_Valid_AuditRowBeforeAndAfterValuesContainBothOwnerIds()
    {
        using var client = CreateClient();
        var previousOwnerId = await SeedCustomerAsync();
        var catId = await RegisterCatAsync(client, previousOwnerId);
        var newOwnerId = await SeedCustomerAsync();

        var transferResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new
        {
            newOwnerId, reason = "P7: acceptance gate audit before/after check"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, transferResponse.StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        const string action = "CatOwnershipTransferred";

        var beforeValue = await db.Database.SqlQuery<string>(
                $"""
                 SELECT "BeforeValue" AS "Value" FROM audit.audit_entry
                 WHERE "EntityId" = {catId} AND "Action" = {action}
                 ORDER BY "OccurredOn" DESC LIMIT 1
                 """)
            .FirstAsync();
        var afterValue = await db.Database.SqlQuery<string>(
                $"""
                 SELECT "AfterValue" AS "Value" FROM audit.audit_entry
                 WHERE "EntityId" = {catId} AND "Action" = {action}
                 ORDER BY "OccurredOn" DESC LIMIT 1
                 """)
            .FirstAsync();

        Assert.Contains(previousOwnerId.ToString(), beforeValue);
        Assert.Contains(newOwnerId.ToString(), afterValue);
    }

    // P1 / A24: "a second connection opens a transaction and UPDATEs the cat row. A PUT /profile is
    // then issued. The PUT is still blocked after 3 s. After the probe commits, the PUT returns
    // 409, and GET shows the competing edit survived." Forces the race deterministically with a
    // held Postgres row lock, rather than relying on genuine timing (CatCrudLifecycleEndpointTests'
    // own I7 already covers the "never a 500, at least one 200" black-box case, which cannot force
    // a guaranteed 409 without exactly this kind of instrumentation).
    [Fact]
    public async Task ConcurrentProfileUpdate_ForcedRowLockRace_Returns409AndKeepsTheCompetingEdit()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var connectionString = db.Database.GetConnectionString()!;

        await using var lockingConnection = new NpgsqlConnection(connectionString);
        await lockingConnection.OpenAsync();
        await using var lockingTransaction = await lockingConnection.BeginTransactionAsync();

        // Holds an exclusive row lock on this cat's row until committed below -- any concurrent
        // UPDATE against the same row (including the PUT's own EF SaveChanges) blocks until then.
        await using (var lockCommand = new NpgsqlCommand(
            """UPDATE clinic.cat SET "Name" = @name WHERE "Id" = @id""", lockingConnection, lockingTransaction))
        {
            lockCommand.Parameters.AddWithValue("name", "Probe Winner");
            lockCommand.Parameters.AddWithValue("id", catId);
            await lockCommand.ExecuteNonQueryAsync();
        }

        var putTask = client.PutAsJsonAsync($"/api/cats/{catId}/profile", new
        {
            name = "Blocked Update", sex = "Female", dateOfBirthPrecision = "Estimated",
            breed = (string?)null, colourAndMarkings = (string?)null
        }, Json);

        await Task.WhenAny(putTask, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.False(putTask.IsCompleted, "Expected the PUT to still be blocked behind the held row lock after 3 seconds.");

        await lockingTransaction.CommitAsync();

        var putResponse = await putTask;
        Assert.Equal(HttpStatusCode.Conflict, putResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/cats/{catId}");
        var body = await getResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Probe Winner", body.GetProperty("name").GetString());
    }

    /// <summary>Registers a core, active vaccine with a run-unique code (§16.1 rule 3), on an
    /// already-signed-in <paramref name="client"/>.</summary>
    private static async Task<Guid> RegisterCoreVaccineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/vaccines", new
        {
            code = $"ACC-{Guid.NewGuid():N}",
            name = "Acceptance Gate Test Vaccine",
            isCore = true,
            validityMonths = 12,
            description = (string?)null
        }, Json);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("vaccineId").GetGuid();
    }
}
