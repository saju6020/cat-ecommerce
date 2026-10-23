using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ADM-001 §16, Branches (B). I-B1/I-B2/I-B3/I-B4/I-B6.</summary>
public sealed class AdminBranchEndpointTests(AdministrationApiFactory factory) : AdministrationIntegrationTestBase(factory)
{
    private static string NewCode() => "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Fact]
    public async Task CreateBranch_SetHours_AddClosure_CalendarReflectsBoth()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var code = NewCode();
        var create = await client.PostAsJsonAsync("/api/admin/branches", new
        {
            code, name = "Test Branch", timeZoneId = "Asia/Dhaka"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var branchId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();

        var setHours = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/hours", new
        {
            intervals = new[] { new { dayOfWeek = "Monday", opens = "09:00", closes = "17:00" } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setHours.StatusCode);

        var monday = NextMonday();
        var closure = await client.PostAsJsonAsync($"/api/admin/branches/{branchId}/closures", new
        {
            startsOn = monday.ToString("yyyy-MM-dd"), endsOn = monday.ToString("yyyy-MM-dd"), kind = "PublicHoliday", reason = "Test holiday"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, closure.StatusCode);

        var nextMonday = monday.AddDays(7);
        var calendar = await client.GetAsync($"/api/admin/branches/{branchId}/calendar?from={monday:yyyy-MM-dd}&to={nextMonday:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, calendar.StatusCode);
        var intervals = (await calendar.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();

        // The holiday Monday is closed; the following Monday is open.
        Assert.Single(intervals);
    }

    [Fact]
    public async Task DuplicateBranchCode_Returns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var code = NewCode();
        var first = await client.PostAsJsonAsync("/api/admin/branches", new { code, name = "First", timeZoneId = "Asia/Dhaka" }, Json);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/admin/branches", new { code, name = "Second", timeZoneId = "Asia/Dhaka" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("BranchCodeAlreadyExists", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DuplicateRoomNameInSameBranch_Returns400_DifferentBranch_Returns201()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var branchAId = await CreateBranchAsync(client);
        var branchBId = await CreateBranchAsync(client);

        var room1 = await client.PostAsJsonAsync($"/api/admin/branches/{branchAId}/rooms", new { name = "Room 1", kind = "Consultation" }, Json);
        Assert.Equal(HttpStatusCode.Created, room1.StatusCode);

        var duplicate = await client.PostAsJsonAsync($"/api/admin/branches/{branchAId}/rooms", new { name = "Room 1", kind = "Treatment" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);

        var otherBranch = await client.PostAsJsonAsync($"/api/admin/branches/{branchBId}/rooms", new { name = "Room 1", kind = "Treatment" }, Json);
        Assert.Equal(HttpStatusCode.Created, otherBranch.StatusCode);
    }

    // I-B4: "deactivate the only active branch" needs every OTHER active branch deactivated first,
    // which would mutate this shared, concurrently-tested database's global state (§16.1 rules 2/4
    // -- the same reason HO-ACC-001's I-F6, "sole administrator", is not automated here either).
    // Covered instead as a handler-level unit test with a fake repository:
    // SetBranchActivationCommandHandlerTests.Deactivating_TheOnlyOtherActiveBranchAbsent_Returns400.

    /// <summary>MEDIUM (gate fix 2026-09-26): AdministrationListCaps.MaxUnboundedListSize hard-caps
    /// this list rather than fully paginating it (recorded as an HO-ADM-001 §1 Amended row and in
    /// ADR-050). This proves the cheap truncation signal (a response header, not a body-shape
    /// change) actually fires once the cap is reached.</summary>
    [Fact]
    public async Task GetClosures_MoreThanTheCap_TruncatesAndSignalsWithHeader()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var branchId = await CreateBranchAsync(client);

        var firstDay = NextMonday();
        for (var i = 0; i < AdministrationListCaps.MaxUnboundedListSize + 1; i++)
        {
            var day = firstDay.AddDays(i);
            var create = await client.PostAsJsonAsync($"/api/admin/branches/{branchId}/closures", new
            {
                startsOn = day.ToString("yyyy-MM-dd"), endsOn = day.ToString("yyyy-MM-dd"), kind = "Closure", reason = "Cap test"
            }, Json);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }

        var get = await client.GetAsync($"/api/admin/branches/{branchId}/closures");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var items = (await get.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        Assert.Equal(AdministrationListCaps.MaxUnboundedListSize, items.Count);
        Assert.True(get.Headers.TryGetValues("X-Truncated", out var values));
        Assert.Equal("true", values!.Single());
    }

    [Fact]
    public async Task PublicBranchesEndpoint_ReturnsContactDetails_NoAuth()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/branches");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StaffAssignment_AssignAndReadBack()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var staffMember = await SeedStaffAsync(StaffRoles.Vet);

        var branchId = await CreateBranchAsync(client);

        var assign = await client.PutAsJsonAsync($"/api/admin/staff/{staffMember.StaffAccountId}/branches", new
        {
            branchIds = new[] { branchId }, primaryBranchId = branchId
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        var get = await client.GetAsync($"/api/admin/staff/{staffMember.StaffAccountId}/branches");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await get.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(branchId, body.GetProperty("primaryBranchId").GetGuid());
    }

    // Owner, 2026-10-21: a new staff member with no branch yet reads as an empty assignment, so the picker shows.
    [Fact]
    public async Task StaffAssignment_NotYetAssigned_ReadsAsEmpty_UnknownAccountIs404()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var staffMember = await SeedStaffAsync(StaffRoles.Vet);

        var get = await client.GetAsync($"/api/admin/staff/{staffMember.StaffAccountId}/branches");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await get.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(0, body.GetProperty("branchIds").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("primaryBranchId").ValueKind);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/admin/staff/{Guid.NewGuid()}/branches")).StatusCode);
    }

    private async Task<Guid> CreateBranchAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/admin/branches", new { code = NewCode(), name = "Test Branch", timeZoneId = "Asia/Dhaka" }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        return (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();
    }

    private static DateOnly NextMonday()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        while (date.DayOfWeek != DayOfWeek.Monday) date = date.AddDays(1);
        return date;
    }
}
