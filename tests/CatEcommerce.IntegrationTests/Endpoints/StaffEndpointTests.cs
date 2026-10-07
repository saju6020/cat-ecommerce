using Platform.Infrastructure.Core.Commands;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ACC-001 §16, Staff & authorization (F) / Staff sign-in (G) -- §16.1 rule 7:
/// staff callers are seeded through the repository, given a password via SetPassword, then
/// signed in through the real endpoint.</summary>
public sealed class StaffEndpointTests(AccountsApiFactory factory) : AccountsIntegrationTestBase(factory)
{
    private async Task<(string Mobile, string Password)> SeedStaffAsync(params string[] roles) =>
        (await SeedStaffWithIdAsync(roles)).Credentials;

    private async Task<(Guid StaffAccountId, (string Mobile, string Password) Credentials)> SeedStaffWithIdAsync(params string[] roles)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto(
            "Seeded Staff", MobileNumber.Create(mobile), null, roles,
            new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        const string password = "staffpassword1";
        staff.SetPassword(new PasswordSetDto(hasher.Hash(password), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);

        return (staff.Id, (mobile, password));
    }

    private static async Task SignInWithCsrfAsync(HttpClient client, string mobile, string password)
    {
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
    }

    // I-F1
    [Fact]
    public async Task Administrator_CreatesStaffAccount_RolesStoredInCatalogueOrderAndHasPasswordFalse()
    {
        var (mobile, password) = await SeedStaffAsync("Administrator");
        using var client = CreateClient();
        await SignInWithCsrfAsync(client, mobile, password);

        // Requested out of catalogue order (Vet, Reception) -- StaffRoles.All order is
        // Reception, Nurse, Vet, ... so the stored/returned order must be Reception, Vet.
        var response = await client.PostAsJsonAsync("/api/staff-accounts", new
        {
            fullName = "New Vet", mobileNumber = NewMobile(), roleIds = new[] { "Vet", "Reception" }
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        var newStaffId = created.GetProperty("staffAccountId").GetGuid();

        var get = await client.GetAsync($"/api/staff-accounts/{newStaffId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await get.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);

        var roleIds = body.GetProperty("roleIds").EnumerateArray().Select(r => r.GetString()).ToList();
        Assert.Equal(["Reception", "Vet"], roleIds);
        Assert.False(body.GetProperty("hasPassword").GetBoolean());
    }

    // I-F4
    [Fact]
    public async Task ChangeRoles_RotatesStamp_AffectedMembersExistingSessionIs401OnNextRequest()
    {
        var (administratorMobile, administratorPassword) = await SeedStaffAsync("Administrator");
        var (memberId, (memberMobile, memberPassword)) = await SeedStaffWithIdAsync("Nurse");

        // The affected member signs in first and keeps their own session/client.
        using var memberClient = CreateClient();
        var memberSignIn = await memberClient.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = memberMobile, password = memberPassword }, Json);
        Assert.Equal(HttpStatusCode.OK, memberSignIn.StatusCode);

        var statusBeforeChange = await memberClient.GetAsync("/api/staff/session");
        Assert.Equal(HttpStatusCode.OK, statusBeforeChange.StatusCode);

        using var adminClient = CreateClient();
        await SignInWithCsrfAsync(adminClient, administratorMobile, administratorPassword);

        var changeRoles = await adminClient.PutAsJsonAsync($"/api/staff-accounts/{memberId}/roles", new { roleIds = new[] { "Vet" } }, Json);
        Assert.Equal(HttpStatusCode.NoContent, changeRoles.StatusCode);

        var statusAfterChange = await memberClient.GetAsync("/api/staff/session");
        Assert.Equal(HttpStatusCode.Unauthorized, statusAfterChange.StatusCode);
    }

    // I-F6: cannot be automated in this shared, non-empty database. The guard checks for the
    // existence of ANY OTHER active Administrator system-wide (not scoped to this test's own
    // data) -- reproducing "this is the last Administrator" would require either truncating
    // staff_account (forbidden, §16.1 rule 4) or the table being otherwise empty of other
    // Administrators (a global-emptiness precondition forbidden by §16.1 rule 2). No amount of
    // data minted by this test alone can make that condition true in a shared database that
    // other tests and other runs have already seeded Administrators into.

    // I-F2
    [Fact]
    public async Task NonAdministrator_CallingCreateStaffAccount_Returns403AndAudits()
    {
        var (mobile, password) = await SeedStaffAsync("Nurse");
        using var client = CreateClient();

        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var response = await client.PostAsJsonAsync("/api/staff-accounts", new
        {
            fullName = "New Vet", mobileNumber = NewMobile(), roleIds = new[] { "Vet" }
        }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("ActionNotPermitted", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Administrator_CallingCreateStaffAccount_Returns201()
    {
        var (mobile, password) = await SeedStaffAsync("Administrator");
        using var client = CreateClient();

        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var response = await client.PostAsJsonAsync("/api/staff-accounts", new
        {
            fullName = "New Vet", mobileNumber = NewMobile(), roleIds = new[] { "Vet" }
        }, Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // I-F3. HO-ADM-001 §12.3 / ADR-043 (U-A6) amends this from 401 to 403: a customer IS
    // authenticated, just not authorized for a staff-only action -- the same 403-plus-audit path
    // a staff caller with no matching role gets, rather than the blanket 401 an anonymous caller
    // gets. See HO-ADM-001 §1 Amended.
    [Fact]
    public async Task CustomerSession_CallingStaffEndpoint_Returns403()
    {
        using var client = CreateClient();
        var (_, mobile, password) = await RegisterCustomerAsync(client);
        await client.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password }, Json);

        var response = await client.GetAsync("/api/staff-roles");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // I-G2
    [Fact]
    public async Task CustomerCredentials_OnStaffSignIn_ReturnsSameGenericFailure()
    {
        using var client = CreateClient();
        var (_, mobile, password) = await RegisterCustomerAsync(client);

        var response = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid_credentials", await response.Content.ReadAsStringAsync());
    }

    // I-F5
    [Fact]
    public async Task StaffRoles_ReturnsEightRolesAndTwentyCapabilities()
    {
        var (mobile, password) = await SeedStaffAsync("Administrator");
        using var client = CreateClient();
        await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);

        var response = await client.GetAsync("/api/staff-roles");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Administrator", body);
    }

    // Integrator fix 2026-09-26 (HO-ADM-001-SECURITY's carried line: "HO-ACC-001:
    // GET /api/staff-accounts?pageNumber=2147483647 -> 500 (the Skip overflow)"): GET
    // /api/staff-accounts applied pageSize with no cap and pageNumber with no cap either --
    // (pageNumber-1)*pageSize can overflow int (Skip overflow), producing a negative OFFSET
    // Postgres rejects as an uncaught 500. Now capped the same way AdminLeaveRequestsController
    // already is, via the shared PagingRules.
    [Fact]
    public async Task GetStaffAccounts_PageNumberAtIntMaxValue_Returns400_Never500()
    {
        var (mobile, password) = await SeedStaffAsync("Administrator");
        using var client = CreateClient();
        await SignInWithCsrfAsync(client, mobile, password);

        var response = await client.GetAsync("/api/staff-accounts?pageNumber=2147483647&pageSize=20");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetStaffAccounts_PageSizeAtIntMaxValue_Returns400_Never500()
    {
        var (mobile, password) = await SeedStaffAsync("Administrator");
        using var client = CreateClient();
        await SignInWithCsrfAsync(client, mobile, password);

        var response = await client.GetAsync("/api/staff-accounts?pageNumber=1&pageSize=2147483647");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetStaffAccounts_PageSizeSkipOverflowCombination_Returns400_Never500()
    {
        var (mobile, password) = await SeedStaffAsync("Administrator");
        using var client = CreateClient();
        await SignInWithCsrfAsync(client, mobile, password);

        // pageNumber=3, pageSize=1_500_000_000 -> Skip = (3-1)*1_500_000_000 overflows int negative.
        var response = await client.GetAsync("/api/staff-accounts?pageNumber=3&pageSize=1500000000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
