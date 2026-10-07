using System.Net;
using System.Net.Http.Json;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate (2026-09-26). SECURITY gate LOW (gate-fix round, CLOSED): staff-supplied
/// <c>ownerId</c>/<c>newOwnerId</c> must resolve to a real customer. Dedicated coverage for
/// <c>RegisterCatCommandHandler</c>/<c>TransferCatOwnershipCommandHandler</c>'s new lookup, which had
/// no test of its own anywhere in the Cat integration suite before this gate (CODE_REVIEW third
/// pass, 2026-09-26, confirmed the gap with a literal grep): an unknown id, a staff account's id
/// (a real, present row, but the wrong aggregate), and a real customer (must still succeed).
///
/// The Merged/Anonymised half of CODE_REVIEW's third-pass MEDIUM (an existing-but-not-Active
/// customer) is deliberately NOT duplicated here -- a developer landed a separate fix
/// (<c>cb2657c</c>) plus its own four tests (<c>CatOwnerCustomerStatusEndpointTests.cs</c>,
/// <c>3c8dcbf</c>) during this same gate, asserting the actual <c>AccountNotActive</c> errorCode the
/// fix returns (not <c>CustomerNotFound</c> -- the record does exist, it just is not Active). That
/// file is cited directly in this sheet's regression table (R10) rather than re-implemented here.
/// </summary>
public sealed class CatOwnerAndTransferCustomerValidationTests(CatApiFactory factory) : CatIntegrationTestBase(factory)
{
    // ── Register ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterCat_OwnerIdRandomGuid_Returns400WithCustomerNotFound()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);

        var response = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId = Guid.NewGuid(),
            name = "Orphan Attempt",
            species = "Cat",
            sex = "Male",
            dateOfBirthPrecision = "Estimated"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("CustomerNotFound", raw);
    }

    [Fact]
    public async Task RegisterCat_OwnerIdIsStaffAccountId_Returns400WithCustomerNotFound()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);
        var staffId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Nurse);

        var response = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId = staffId,
            name = "Staff As Owner Attempt",
            species = "Cat",
            sex = "Male",
            dateOfBirthPrecision = "Estimated"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("CustomerNotFound", raw);
    }

    [Fact]
    public async Task RegisterCat_OwnerIdIsRealCustomer_Returns201()
    {
        using var client = CreateClient();
        await SignInAsFreshAdministratorAsync(client);
        var customerId = await SeedCustomerAsync();

        var response = await client.PostAsJsonAsync("/api/cats", new
        {
            ownerId = customerId,
            name = "Real Owner Cat",
            species = "Cat",
            sex = "Male",
            dateOfBirthPrecision = "Estimated"
        }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ── Ownership transfer ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TransferCatOwnership_NewOwnerIdRandomGuid_Returns400WithCustomerNotFound()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new
        {
            newOwnerId = Guid.NewGuid(), reason = "Attempted transfer to a non-existent customer"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("CustomerNotFound", raw);
    }

    [Fact]
    public async Task TransferCatOwnership_NewOwnerIdIsStaffAccountId_Returns400WithCustomerNotFound()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var staffId = await SeedStaffAccountAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Nurse);

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new
        {
            newOwnerId = staffId, reason = "Attempted transfer to a staff account id"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Contains("CustomerNotFound", raw);
    }

    [Fact]
    public async Task TransferCatOwnership_NewOwnerIdIsRealCustomer_Returns200()
    {
        using var client = CreateClient();
        var catId = await RegisterCatAsync(client);
        var newOwnerId = await SeedCustomerAsync();

        var response = await client.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new
        {
            newOwnerId, reason = "Legitimate transfer to a real customer"
        }, Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
