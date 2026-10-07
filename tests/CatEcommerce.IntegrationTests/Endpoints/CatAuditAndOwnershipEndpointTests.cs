using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate (2026-09-26). HO-CAT-001 §16 I12–I17 -- high-risk flag/clear audit rows, ownership
/// transfer (with its own audit row, unchanged vaccinations/medical profile) and the owner-scoped
/// list exclusion that follows it, plus the deceased-cat list exclusion. No audit read endpoint
/// exists (§4.2), so audit rows are read directly via <see cref="CatEcommerceDbContext"/> -- the
/// same technique <c>AuditImmutabilityTests</c> already uses for Accounts.
/// </summary>
public sealed class CatAuditAndOwnershipEndpointTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    // I12
    [Fact]
    public async Task FlagCatHighRisk_Returns200AndWritesAuditEntryWithActorAndReason()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        const string reason = "New aggressive behaviour observed at check-in";

        var response = await client.PutAsJsonAsync($"/api/cats/{catId}/high-risk-flag", new
        {
            handlingNote = "Muzzle before handling", reason
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (actorUserId, storedReason) = await ReadAuditEntryAsync(catId, "CatFlaggedHighRisk");
        Assert.NotEqual(Guid.Empty, actorUserId);
        Assert.Equal(reason, storedReason);
    }

    // I13
    [Fact]
    public async Task ClearCatHighRiskFlag_Returns200AndWritesSecondAuditEntry()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        var flagResponse = await client.PutAsJsonAsync($"/api/cats/{catId}/high-risk-flag", new
        {
            handlingNote = "Muzzle before handling", reason = "Initial flag"
        }, Json);
        flagResponse.EnsureSuccessStatusCode();

        const string clearReason = "Behaviour normalised after treatment";
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/cats/{catId}/high-risk-flag")
        {
            Content = JsonContent.Create(new { reason = clearReason }, options: Json)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (actorUserId, storedReason) = await ReadAuditEntryAsync(catId, "CatHighRiskFlagCleared");
        Assert.NotEqual(Guid.Empty, actorUserId);
        Assert.Equal(clearReason, storedReason);
    }

    // I14
    [Fact]
    public async Task TransferCatOwnership_Returns200AndWritesAuditEntryVaccinationsAndMedicalProfileUnchanged()
    {
        using var client = CreateClient();
        var previousOwnerId = await SeedCustomerAsync();
        var catId = await RegisterCatAsync(client, previousOwnerId);

        var medicalProfileResponse = await client.PutAsJsonAsync($"/api/cats/{catId}/medical-profile", new
        {
            allergies = new[] { new { allergen = "Fish", reaction = "Itching", severity = "Mild", recordedOn = DateOnly.FromDateTime(DateTime.UtcNow) } },
            chronicConditions = Array.Empty<object>(),
            medications = Array.Empty<object>()
        }, Json);
        medicalProfileResponse.EnsureSuccessStatusCode();

        var vaccineId = await RegisterCoreVaccineAsync(client);
        var vetId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Vet);
        var administeredOn = DateOnly.FromDateTime(DateTime.UtcNow);
        var vaccinationResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/vaccinations", new
        {
            vaccineId, batchReference = "TXFER-1", batchExpiresOn = administeredOn.AddYears(1),
            administeredOn, administeredByVetId = vetId, notes = (string?)null
        }, Json);
        vaccinationResponse.EnsureSuccessStatusCode();

        var newOwnerId = await SeedCustomerAsync();
        const string reason = "Customer relocated, cat rehomed to a relative";
        var transferResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new
        {
            newOwnerId, reason
        }, Json);

        Assert.Equal(HttpStatusCode.OK, transferResponse.StatusCode);

        var (actorUserId, storedReason) = await ReadAuditEntryAsync(catId, "CatOwnershipTransferred");
        Assert.NotEqual(Guid.Empty, actorUserId);
        Assert.Equal(reason, storedReason);

        var profileAfter = await client.GetAsync($"/api/cats/{catId}");
        var profileBody = await profileAfter.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(newOwnerId, profileBody.GetProperty("ownerId").GetGuid());
        Assert.True(profileBody.GetProperty("hasRecordedAllergies").GetBoolean());
        Assert.Equal(1, profileBody.GetProperty("allergies").GetArrayLength());

        var statusAfter = await client.GetAsync($"/api/cats/{catId}/vaccination-status");
        var statusBody = await statusAfter.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains(statusBody.GetProperty("vaccines").EnumerateArray(),
            v => v.GetProperty("vaccineId").GetGuid() == vaccineId);
    }

    // I15
    [Fact]
    public async Task GetCatsByPreviousOwner_AfterTransfer_CatIsAbsent()
    {
        using var client = CreateClient();
        var previousOwnerId = await SeedCustomerAsync();
        var catId = await RegisterCatAsync(client, previousOwnerId);
        var newOwnerId = await SeedCustomerAsync();

        var transferResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new
        {
            newOwnerId, reason = "Rehomed"
        }, Json);
        transferResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync($"/api/cats?ownerId={previousOwnerId}");
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.DoesNotContain(listBody.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == catId);
    }

    // I16
    [Fact]
    public async Task MarkCatDeceased_ThenGetCatsByOwner_CatAbsentByDefault()
    {
        using var client = CreateClient();
        var ownerId = await SeedCustomerAsync();
        var catId = await RegisterCatAsync(client, ownerId);

        var deceasedResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/deceased", new
        {
            deceasedOn = DateOnly.FromDateTime(DateTime.UtcNow), note = (string?)null
        }, Json);
        deceasedResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync($"/api/cats?ownerId={ownerId}");
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(Json);

        Assert.DoesNotContain(listBody.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == catId);
    }

    // I17
    [Fact]
    public async Task MarkCatDeceased_GetByIdAndIncludeDeceasedList_StillReturnsCat()
    {
        using var client = CreateClient();
        var ownerId = await SeedCustomerAsync();
        var catId = await RegisterCatAsync(client, ownerId);

        var deceasedResponse = await client.PostAsJsonAsync($"/api/cats/{catId}/deceased", new
        {
            deceasedOn = DateOnly.FromDateTime(DateTime.UtcNow), note = (string?)null
        }, Json);
        deceasedResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetAsync($"/api/cats/{catId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var getBody = await getResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Deceased", getBody.GetProperty("lifeStatus").GetString());

        var listResponse = await client.GetAsync($"/api/cats?ownerId={ownerId}&includeDeceased=true");
        var listBody = await listResponse.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains(listBody.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == catId);
    }

    private async Task<(Guid ActorUserId, string Reason)> ReadAuditEntryAsync(Guid catId, string action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        // EF Core's SqlQuery<T> for a scalar type requires the result column aliased as "Value" --
        // the same pattern AuditImmutabilityTests uses. Two scalar queries, not one multi-column
        // POCO query, to stay on that already-proven path rather than an unverified shape.
        var entryId = await db.Database.SqlQuery<Guid>(
                $"""
                 SELECT "Id" AS "Value" FROM audit.audit_entry
                 WHERE "EntityId" = {catId} AND "Action" = {action}
                 ORDER BY "OccurredOn" DESC LIMIT 1
                 """)
            .FirstOrDefaultAsync();
        Assert.NotEqual(Guid.Empty, entryId);

        var actorUserId = await db.Database.SqlQuery<Guid>(
                $"""SELECT "ActorUserId" AS "Value" FROM audit.audit_entry WHERE "Id" = {entryId}""")
            .FirstAsync();
        var reason = await db.Database.SqlQuery<string>(
                $"""SELECT "Reason" AS "Value" FROM audit.audit_entry WHERE "Id" = {entryId}""")
            .FirstAsync();

        return (actorUserId, reason);
    }

    private static async Task<Guid> RegisterCoreVaccineAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/vaccines", new
        {
            code = $"CORE-{Guid.NewGuid():N}", name = "Test Core Vaccine", isCore = true,
            validityMonths = 12, description = (string?)null
        }, Json);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        return body.GetProperty("vaccineId").GetGuid();
    }
}
