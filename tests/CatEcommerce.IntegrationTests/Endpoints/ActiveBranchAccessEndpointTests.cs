using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Branches;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ADM-001 §16, active-branch resolution (ADR-046). U-B6/I-B5, exercised through
/// <c>GET /api/staff/branches/mine</c> -- the one endpoint in this slice that calls
/// <c>IActiveBranchAccessor</c> (gate fix 2026-09-26). "No assignment, exactly one active branch"
/// is not automated here for the same reason I-B4 is not (§16.1 rules 2/4): the shared,
/// concurrently-tested database already has more than one active branch (MAIN plus whatever other
/// tests created) by the time this runs, so "no assignment, two-or-more active branches" is the
/// reliably reproducible half of that pair.</summary>
public sealed class ActiveBranchAccessEndpointTests(AdministrationApiFactory factory) : AdministrationIntegrationTestBase(factory)
{
    private static string NewCode() => "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Fact]
    public async Task Mine_HeaderNamesAnAssignedBranch_ResolvesToThatBranch()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var vet = await SeedStaffAsync(StaffRoles.Vet);

        var branchA = await CreateBranchAsync(adminClient);
        var branchB = await CreateBranchAsync(adminClient);
        var assign = await adminClient.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new
        {
            branchIds = new[] { branchA, branchB }, primaryBranchId = branchA
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        using var vetClient = await SignInStaffAsync(vet.Mobile, vet.Password);
        vetClient.DefaultRequestHeaders.Add(HttpContextActiveBranchAccessor.BranchHeaderName, branchB.ToString());

        var mine = await vetClient.GetAsync("/api/staff/branches/mine");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var body = await mine.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(branchB, body.GetProperty("activeBranchId").GetGuid());
    }

    /// <summary>Gate fix 2026-09-26 (ARCH MEDIUM, second pass): the picker's own payload never
    /// needed an active branch (ADR-046), so a refused header must not turn into a hard failure --
    /// the caller still gets their assignment back, plus the refusal code, so the frontend can
    /// recover (e.g. by dropping the stale header and retrying).</summary>
    [Fact]
    public async Task Mine_HeaderNamesABranchNotInAssignment_Returns200WithAssignmentAndRefusalCode()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var vet = await SeedStaffAsync(StaffRoles.Vet);

        var branchA = await CreateBranchAsync(adminClient);
        var branchOutside = await CreateBranchAsync(adminClient);
        var assign = await adminClient.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new
        {
            branchIds = new[] { branchA }, primaryBranchId = branchA
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        using var vetClient = await SignInStaffAsync(vet.Mobile, vet.Password);
        vetClient.DefaultRequestHeaders.Add(HttpContextActiveBranchAccessor.BranchHeaderName, branchOutside.ToString());

        var mine = await vetClient.GetAsync("/api/staff/branches/mine");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var body = await mine.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(branchA, body.GetProperty("primaryBranchId").GetGuid());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("activeBranchId").ValueKind);
        Assert.Equal("BranchNotAssigned", body.GetProperty("activeBranchRefusal").GetString());
    }

    [Fact]
    public async Task Mine_HeaderIsNotAGuid_Returns200WithAssignmentAndRefusalCode_NeverFallsBackToPrimary()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var vet = await SeedStaffAsync(StaffRoles.Vet);

        var branchA = await CreateBranchAsync(adminClient);
        var assign = await adminClient.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new
        {
            branchIds = new[] { branchA }, primaryBranchId = branchA
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        using var vetClient = await SignInStaffAsync(vet.Mobile, vet.Password);
        vetClient.DefaultRequestHeaders.Add(HttpContextActiveBranchAccessor.BranchHeaderName, "not-a-guid");

        var mine = await vetClient.GetAsync("/api/staff/branches/mine");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var body = await mine.Content.ReadFromJsonAsync<JsonElement>(Json);
        // Never silently falls back to the primary branch as "activeBranchId" -- the refusal is
        // surfaced instead.
        Assert.Equal(JsonValueKind.Null, body.GetProperty("activeBranchId").ValueKind);
        Assert.Equal("BranchNotAssigned", body.GetProperty("activeBranchRefusal").GetString());
        Assert.Equal(branchA, body.GetProperty("primaryBranchId").GetGuid());
    }

    [Fact]
    public async Task Mine_NoHeader_ResolvesToPrimaryAssignedBranch()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var vet = await SeedStaffAsync(StaffRoles.Vet);

        var branchA = await CreateBranchAsync(adminClient);
        var branchB = await CreateBranchAsync(adminClient);
        var assign = await adminClient.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new
        {
            branchIds = new[] { branchA, branchB }, primaryBranchId = branchA
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        using var vetClient = await SignInStaffAsync(vet.Mobile, vet.Password);

        var mine = await vetClient.GetAsync("/api/staff/branches/mine");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var body = await mine.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(branchA, body.GetProperty("activeBranchId").GetGuid());
    }

    [Fact]
    public async Task Mine_NoHeaderNoAssignment_MultipleActiveBranchesExist_Returns200WithRefusalCode()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        // Ensure at least two active branches exist (MAIN plus this one) -- see class doc comment.
        await CreateBranchAsync(adminClient);

        var vet = await SeedStaffAsync(StaffRoles.Vet); // never assigned to any branch

        using var vetClient = await SignInStaffAsync(vet.Mobile, vet.Password);
        var mine = await vetClient.GetAsync("/api/staff/branches/mine");

        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        var body = await mine.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("activeBranchId").ValueKind);
        Assert.Equal("BranchAssignmentRequired", body.GetProperty("activeBranchRefusal").GetString());
    }

    [Fact]
    public async Task Mine_CalledByCustomer_Returns403_NotTheOldHandRolled401()
    {
        var (customerClient, _, _) = await RegisterAndSignInCustomerAsync();
        using var client = customerClient;

        var mine = await client.GetAsync("/api/staff/branches/mine");
        Assert.Equal(HttpStatusCode.Forbidden, mine.StatusCode);
    }

    private async Task<Guid> CreateBranchAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/admin/branches", new { code = NewCode(), name = "Test Branch", timeZoneId = "Asia/Dhaka" }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();
    }
}
