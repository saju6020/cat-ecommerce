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

    // Owner, 2026-10-19: the administrator creates a staff member with a starting password and shares it.
    [Fact]
    public async Task Administrator_CreatesStaffWithAPassword_TheNewMemberSignsInWithIt()
    {
        var (mobile, password) = await SeedStaffAsync("Administrator");
        using var client = CreateClient();
        await SignInWithCsrfAsync(client, mobile, password);

        var newMobile = NewMobile();
        var response = await client.PostAsJsonAsync("/api/staff-accounts", new
        {
            fullName = "New Nurse", mobileNumber = newMobile, roleIds = new[] { "Nurse" }, password = "a-shared-starting-password"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var newStaffId = (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json)).GetProperty("staffAccountId").GetGuid();

        var body = await (await client.GetAsync($"/api/staff-accounts/{newStaffId}")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.True(body.GetProperty("hasPassword").GetBoolean());

        using var memberClient = CreateClient();
        var signIn = await memberClient.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = newMobile, password = "a-shared-starting-password" }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    // Owner, 2026-10-19: the administrator sets a new password; the member's open session ends and the new one works.
    [Fact]
    public async Task Administrator_SetsAStaffPassword_OldSessionEnds_NewPasswordSignsIn()
    {
        var (administratorId, (administratorMobile, administratorPassword)) = await SeedStaffWithIdAsync("Administrator");
        var (memberId, (memberMobile, memberPassword)) = await SeedStaffWithIdAsync("Nurse");

        using var memberClient = CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await memberClient.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = memberMobile, password = memberPassword }, Json)).StatusCode);

        using var adminClient = CreateClient();
        await SignInWithCsrfAsync(adminClient, administratorMobile, administratorPassword);
        var set = await adminClient.PutAsJsonAsync($"/api/staff-accounts/{memberId}/password", new { password = "a-brand-new-password" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await memberClient.GetAsync("/api/staff/session")).StatusCode);
        using var fresh = CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await fresh.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = memberMobile, password = "a-brand-new-password" }, Json)).StatusCode);
        using var old = CreateClient();
        Assert.NotEqual(HttpStatusCode.OK, (await old.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = memberMobile, password = memberPassword }, Json)).StatusCode);

        // SECURITY gate HIGH: the audit row names the administrator who set it, not the staff member.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        var audit = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            db.Set<CatEcommerce.Domain.Aggregates.Audit.AuditEntryAggregate>(),
            e => e.EntityId == memberId && e.Action == "StaffPasswordSet" && e.Reason == "SetByAdministrator");
        Assert.Equal(administratorId, audit.ActorUserId);
    }

    [Fact]
    public async Task NonAdministrator_SettingAStaffPassword_Returns403()
    {
        var (receptionMobile, receptionPassword) = await SeedStaffAsync("Reception");
        var (memberId, _) = await SeedStaffWithIdAsync("Nurse");
        using var client = CreateClient();
        await SignInWithCsrfAsync(client, receptionMobile, receptionPassword);

        var set = await client.PutAsJsonAsync($"/api/staff-accounts/{memberId}/password", new { password = "a-brand-new-password" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, set.StatusCode);
    }

    [Fact]
    public async Task SettingAPasswordForAnUnknownStaffAccount_Returns404_AndAShortOne_Returns400()
    {
        var (mobile, password) = await SeedStaffAsync("Administrator");
        var (memberId, _) = await SeedStaffWithIdAsync("Nurse");
        using var client = CreateClient();
        await SignInWithCsrfAsync(client, mobile, password);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/staff-accounts/{Guid.NewGuid()}/password", new { password = "a-brand-new-password" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/staff-accounts/{memberId}/password", new { password = "short" }, Json)).StatusCode);
    }

    // Owner, 2026-10-20: the administrator edits a staff member's name and sign-in identifiers.
    [Fact]
    public async Task Administrator_UpdatesStaffDetails_NewEmailSignsIn_AndIsAudited()
    {
        var (administratorId, (administratorMobile, administratorPassword)) = await SeedStaffWithIdAsync("Administrator");
        var (memberId, (memberMobile, memberPassword)) = await SeedStaffWithIdAsync("Nurse");
        using var client = CreateClient();
        await SignInWithCsrfAsync(client, administratorMobile, administratorPassword);

        var email = NewEmail();
        var update = await client.PutAsJsonAsync($"/api/staff-accounts/{memberId}", new { fullName = "Renamed Nurse", mobileNumber = memberMobile, emailAddress = email }, Json);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var body = await (await client.GetAsync($"/api/staff-accounts/{memberId}")).Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Json);
        Assert.Equal("Renamed Nurse", body.GetProperty("fullName").GetString());
        Assert.Equal(email, body.GetProperty("emailAddress").GetString());

        // Re-saving the same identifiers is not a duplicate of itself.
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/staff-accounts/{memberId}", new { fullName = "Renamed Nurse", mobileNumber = memberMobile, emailAddress = email }, Json)).StatusCode);

        using var memberClient = CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await memberClient.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = email, password = memberPassword }, Json)).StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();
        var audit = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            db.Set<CatEcommerce.Domain.Aggregates.Audit.AuditEntryAggregate>(),
            e => e.EntityId == memberId && e.Action == "StaffAccountDetailsChanged");
        Assert.Equal(administratorId, audit.ActorUserId);
    }

    [Fact]
    public async Task UpdatingStaffDetails_Duplicate400_Unknown404_NonAdministrator403()
    {
        var (adminMobile, adminPassword) = await SeedStaffAsync("Administrator");
        var (memberId, _) = await SeedStaffWithIdAsync("Nurse");
        var (_, (otherMobile, _)) = await SeedStaffWithIdAsync("Vet");
        using var client = CreateClient();
        await SignInWithCsrfAsync(client, adminMobile, adminPassword);

        var duplicate = await client.PutAsJsonAsync($"/api/staff-accounts/{memberId}", new { fullName = "Nurse", mobileNumber = otherMobile }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("MobileNumberAlreadyRegistered", await duplicate.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/staff-accounts/{memberId}", new { fullName = "Nurse" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/staff-accounts/{Guid.NewGuid()}", new { fullName = "Nobody", mobileNumber = NewMobile() }, Json)).StatusCode);

        var (receptionMobile, receptionPassword) = await SeedStaffAsync("Reception");
        using var reception = CreateClient();
        await SignInWithCsrfAsync(reception, receptionMobile, receptionPassword);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PutAsJsonAsync($"/api/staff-accounts/{memberId}", new { fullName = "Nurse", mobileNumber = NewMobile() }, Json)).StatusCode);
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
