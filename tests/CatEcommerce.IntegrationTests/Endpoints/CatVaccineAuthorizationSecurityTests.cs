using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-ADM-001 §12.3 / ADR-043, US-ADM-02. Reproduces the attacks the HO-CAT-001 SECURITY gate
/// proved live against master (no authorization existed on any of the 19 Cat/Vaccine endpoints)
/// and asserts they are refused now that every action carries <c>[RequireAction]</c> with
/// <see cref="CatEcommerce.Api.Authorization.OwnerScope.Cat"/> where the mapping table calls for
/// it. I-A2 (the full staff-role matrix), I-A4 and I-A5.
/// </summary>
public sealed class CatVaccineAuthorizationSecurityTests(AdministrationApiFactory factory) : AdministrationIntegrationTestBase(factory)
{
    // ── 1. Anonymous is refused on every Cat/Vaccine endpoint ──────────────────────────────────

    [Fact]
    public async Task Anonymous_EveryCatAndVaccineEndpoint_IsRefused()
    {
        var ownerId = Guid.NewGuid();
        var catId = await SeedCatAsync(ownerId);

        using var client = CreateClient();

        var probes = new (HttpMethod Method, string Url, object? Body)[]
        {
            (HttpMethod.Get, "/api/cats?pageSize=100000", null),
            (HttpMethod.Post, "/api/cats", new { ownerId, name = "Planted", species = "Cat", sex = "Female", dateOfBirthPrecision = "Exact", dateOfBirth = "2020-01-01" }),
            (HttpMethod.Get, $"/api/cats/{catId}", null),
            (HttpMethod.Put, $"/api/cats/{catId}/profile", new { name = "Hacked", sex = "Female", dateOfBirthPrecision = "Exact", dateOfBirth = "2020-01-01" }),
            (HttpMethod.Put, $"/api/cats/{catId}/neuter-status", new { neuterStatus = "Neutered", neuteredOn = "2021-01-01" }),
            (HttpMethod.Put, $"/api/cats/{catId}/medical-profile", new { allergies = Array.Empty<object>(), chronicConditions = Array.Empty<object>(), medications = Array.Empty<object>() }),
            // Gate-fix 2026-09-27: the photo endpoint now takes a server-issued FileId from
            // HO-FIL-001's own upload flow, not a client-supplied storage key (the carried
            // HO-CAT-002-SECURITY item HO-FIL-001 closed -- see HO-FIL-001.md §1 Amended).
            (HttpMethod.Post, $"/api/cats/{catId}/photos", new { fileId = Guid.NewGuid(), isPrimary = true }),
            (HttpMethod.Put, $"/api/cats/{catId}/high-risk-flag", new { handlingNote = "n", reason = "r" }),
            (HttpMethod.Get, $"/api/cats/{catId}/weights", null),
            (HttpMethod.Post, $"/api/cats/{catId}/weights", new { kilograms = 4.2, recordedOn = "2024-01-01", recordedByStaffId = Guid.NewGuid() }),
            (HttpMethod.Get, $"/api/cats/{catId}/vaccination-status", null),
            (HttpMethod.Post, $"/api/cats/{catId}/parasite-treatments", new { treatmentType = "Flea", productName = "p", administeredOn = "2024-01-01", administeredByStaffId = Guid.NewGuid(), intervalDays = 30 }),
            (HttpMethod.Post, $"/api/cats/{catId}/deceased", new { deceasedOn = "2024-01-01" }),
            (HttpMethod.Post, $"/api/cats/{catId}/ownership-transfer", new { newOwnerId = Guid.NewGuid(), reason = "r" }),
            (HttpMethod.Get, $"/api/cats/{catId}/medical-history.pdf", null),
            (HttpMethod.Get, $"/api/cats/{catId}/cage-card", null),
            (HttpMethod.Get, "/api/vaccines", null),
            (HttpMethod.Post, "/api/vaccines", new { code = "V1", name = "Vax", isCore = true, validityMonths = 12 }),
        };

        foreach (var (method, url, body) in probes)
        {
            var request = new HttpRequestMessage(method, url);
            if (body is not null) request.Content = JsonContent.Create(body);

            var response = await client.SendAsync(request);

            Assert.True(
                response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
                $"{method} {url} returned {(int)response.StatusCode} for an anonymous caller -- expected 401/403.");
        }
    }

    [Fact]
    public async Task Anonymous_ListWithHugePageSize_ReturnsUnauthorizedNotOtherOwnersCats()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/cats?pageSize=100000");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── 2. Customer B cannot read, rename or transfer customer A's cat; body/query owner ignored ─

    [Fact]
    public async Task CustomerB_CannotReadCustomerAsCat_Returns404()
    {
        var (clientA, ownerAId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(ownerAId);

        var (clientB, _, _) = await RegisterAndSignInCustomerAsync();

        var response = await clientB.GetAsync($"/api/cats/{catId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CustomerB_CannotRenameCustomerAsCat_Returns404()
    {
        var (_, ownerAId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(ownerAId);

        var (clientB, _, _) = await RegisterAndSignInCustomerAsync();

        var response = await clientB.PutAsJsonAsync($"/api/cats/{catId}/profile", new
        {
            name = "Renamed by B", sex = "Female", dateOfBirthPrecision = "Exact", dateOfBirth = "2020-01-01"
        }, Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CustomerB_CannotTransferCustomerAsCat_Returns403NotInCustomerActions()
    {
        var (_, ownerAId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(ownerAId);

        var (clientB, _, _) = await RegisterAndSignInCustomerAsync();

        // Cats.TransferOwnership is staff-only (OwnerScope.None) -- a customer never reaches the
        // ownership check at all; falls straight to 403 (U-A6's rule).
        var response = await clientB.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new
        {
            newOwnerId = Guid.NewGuid(), reason = "steal"
        }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CustomerB_RegisteringACat_OwnerIdInBodyIsIgnored_CatBelongsToCaller()
    {
        var (_, ownerAId, _) = await RegisterAndSignInCustomerAsync();
        var (clientB, ownerBId, _) = await RegisterAndSignInCustomerAsync();

        var response = await clientB.PostAsJsonAsync("/api/cats", new
        {
            ownerId = ownerAId, // attempts to plant the cat into A's account
            name = "Planted", species = "Cat", sex = "Female", dateOfBirthPrecision = "Exact", dateOfBirth = "2020-01-01"
        }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        var catId = body.GetProperty("catId").GetGuid();

        // B (the actual caller) can see it; the ownerId query parameter is likewise forced.
        var getAsB = await clientB.GetAsync($"/api/cats/{catId}");
        Assert.Equal(HttpStatusCode.OK, getAsB.StatusCode);

        var listAsB = await clientB.GetAsync($"/api/cats?ownerId={ownerAId}");
        Assert.Equal(HttpStatusCode.OK, listAsB.StatusCode);
        var listBody = await listAsB.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        var ids = listBody.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(catId, ids);
    }

    // ── 3. US-CAT-08 AC2: after transfer, the FORMER owner is refused everywhere ────────────────

    [Fact]
    public async Task AfterOwnershipTransfer_FormerOwner_IsRefusedOnEveryCatEndpoint()
    {
        var (clientA, ownerAId, _) = await RegisterAndSignInCustomerAsync();
        var (_, ownerBId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(ownerAId);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var transfer = await adminClient.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new { newOwnerId = ownerBId, reason = "reassigned" }, Json);
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        // A no longer owns the cat -- every owner-scoped endpoint must now refuse A (404, U-A5),
        // proving OwnerScope checks the CURRENT owner, not a cached/at-request-time value.
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/cats/{catId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/cats/{catId}/vaccination-status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/cats/{catId}/weights")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/cats/{catId}/medical-history.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.PutAsJsonAsync($"/api/cats/{catId}/profile",
            new { name = "x", sex = "Female", dateOfBirthPrecision = "Exact", dateOfBirth = "2020-01-01" }, Json)).StatusCode);

        var listAsA = await clientA.GetAsync("/api/cats");
        Assert.Equal(HttpStatusCode.OK, listAsA.StatusCode);
        var listBody = await listAsA.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        var ids = listBody.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        Assert.DoesNotContain(catId, ids);
    }

    // ── 4. Role separation ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AccountantOnlyStaff_CannotMarkCatDeceased_Returns403()
    {
        var ownerId = Guid.NewGuid();
        var catId = await SeedCatAsync(ownerId);

        var staff = await SeedStaffAsync(StaffRoles.Accountant);
        using var client = await SignInStaffAsync(staff.Mobile, staff.Password);

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/deceased", new { deceasedOn = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd") }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ShopManagerOnlyStaff_CannotRegisterVaccine_Returns403()
    {
        var staff = await SeedStaffAsync(StaffRoles.ShopManager);
        using var client = await SignInStaffAsync(staff.Mobile, staff.Password);

        var response = await client.PostAsJsonAsync("/api/vaccines", new { code = $"V{Guid.NewGuid():N}"[..10], name = "Vax", isCore = true, validityMonths = 12 }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotSetOwnCatsHighRiskFlag_Returns403()
    {
        var (client, ownerId, _) = await RegisterAndSignInCustomerAsync();
        var catId = await SeedCatAsync(ownerId);

        var response = await client.PutAsJsonAsync($"/api/cats/{catId}/high-risk-flag", new { handlingNote = "bites", reason = "safety" }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
