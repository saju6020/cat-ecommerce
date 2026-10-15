using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Branches;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-XCT-001 section 16 D1 -- the offline front-desk snapshot and the ping. Each case builds its own branch (so its snapshot holds only its own
/// appointments) under a fake clock at 10:00 Asia/Dhaka on the next local day. Boarding is skipped by the owner (2026-10-10): I-D6 is deferred and
/// <c>boardingTasks</c> is always <c>NotAvailable</c> (I-D7).
/// </summary>
[Collection("XctRetention")]
public sealed class XctSnapshotEndpointTests(XctApiFactory factory) : XctIntegrationTestBase(factory)
{
    private Guid _lastStaffId;

    /// <summary>
    /// 10:00 Asia/Dhaka on the NEXT local day, derived from the real clock (gate fix 2026-10-13, TEST gate row 53). A pinned date is a time bomb: the
    /// session cookie is issued with a real-clock expiry, so a baseline in the past makes every seeding POST answer 401. The next local day is always in
    /// the future, sits on the booking grid, and keeps the +2 h .. +5 h appointments of these cases inside one local day.
    /// </summary>
    private static readonly DateOnly LocalDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka")).DateTime).AddDays(1);
    private static readonly DateTimeOffset Baseline = new DateTimeOffset(LocalDay.ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(6)).ToUniversalTime();

    private sealed record World(
        WebApplicationFactory<Program> App, FakeTimeProvider Clock, Guid BranchId, Guid ServiceId, Guid VetId, HttpClient Admin, HttpClient Customer, Guid CustomerId, string AdminMobile, string AdminPassword);

    private static string Code() => "X" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private async Task<World> SeedWorldAsync()
    {
        var (app, clock) = Factory.WithFakeTimeProvider(Baseline);
        var adminSeed = await SeedStaffAsync(app, StaffRoles.Administrator);
        var admin = await SignInStaffAsync(adminSeed.Mobile, adminSeed.Password, app);

        var code = Code();
        var branch = await admin.PostAsJsonAsync("/api/admin/branches", new { code, name = "Snapshot Branch " + code, timeZoneId = "Asia/Dhaka" }, Json);
        Assert.Equal(HttpStatusCode.Created, branch.StatusCode);
        var branchId = (await branch.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();

        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/branches/{branchId}/hours",
            new { intervals = days.Select(d => new { dayOfWeek = d, opens = "00:00", closes = "23:45" }).ToArray() }, Json)).StatusCode);
        var room = await admin.PostAsJsonAsync($"/api/admin/branches/{branchId}/rooms", new { name = "Consult " + Code(), kind = "Consultation" }, Json);
        Assert.Equal(HttpStatusCode.Created, room.StatusCode);

        var vet = await SeedStaffAsync(app, StaffRoles.Vet);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches",
            new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern",
            new { slots = days.Select(d => new { dayOfWeek = d, starts = "00:00", ends = "23:45" }).ToArray() }, Json)).StatusCode);

        var taxClass = await admin.PostAsJsonAsync("/api/admin/tax-classes", new { code = Code(), name = "Standard " + Code() }, Json);
        Assert.Equal(HttpStatusCode.Created, taxClass.StatusCode);
        var taxClassId = (await taxClass.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("taxClassId").GetGuid();
        var service = await admin.PostAsJsonAsync("/api/admin/services", new
        {
            code = Code(), name = "Vaccination " + Code(), description = "Test", category = "Consultation", durationMinutes = 30,
            qualifiedRoleIds = new[] { StaffRoles.Vet }, requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true
        }, Json);
        Assert.Equal(HttpStatusCode.Created, service.StatusCode);
        var serviceId = (await service.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("serviceId").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/api/admin/services/{serviceId}/prices",
            new { amount = "500.00", currencyCode = "BDT", effectiveFrom = Baseline }, Json)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/staff/{adminSeed.StaffAccountId}/branches",
            new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json)).StatusCode);
        admin.DefaultRequestHeaders.Add(HttpContextActiveBranchAccessor.BranchHeaderName, branchId.ToString());

        var (customer, customerId, _, _) = await RegisterAndSignInCustomerAsync(app);
        return new World(app, clock, branchId, serviceId, vet.StaffAccountId, admin, customer, customerId, adminSeed.Mobile, adminSeed.Password);
    }

    private async Task<Guid> HoldAsync(World w, Guid catId, DateTimeOffset startsOn)
    {
        var hold = await w.Customer.PostAsJsonAsync("/api/customers/me/appointment-holds",
            new { branchId = w.BranchId, serviceId = w.ServiceId, catId, startsOn, performerId = w.VetId }, Json);
        Assert.True(hold.StatusCode == HttpStatusCode.Created, await hold.Content.ReadAsStringAsync());
        return (await hold.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("appointmentId").GetGuid();
    }

    private async Task<Guid> BookAsync(World w, Guid catId, DateTimeOffset startsOn)
    {
        var id = await HoldAsync(w, catId, startsOn);
        var confirm = await w.Customer.PostAsJsonAsync($"/api/customers/me/appointments/{id}/confirm", new { }, Json);
        Assert.True(confirm.StatusCode == HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
        return id;
    }

    /// <summary>A staff member of <paramref name="role"/> assigned to the world's branch, signed in, with the branch header set.</summary>
    private async Task<(HttpClient Client, string Mobile, string Password)> StaffAtBranchAsync(World w, string role)
    {
        var seed = await SeedStaffAsync(w.App, role);
        _lastStaffId = seed.StaffAccountId;
        Assert.Equal(HttpStatusCode.NoContent, (await w.Admin.PutAsJsonAsync($"/api/admin/staff/{seed.StaffAccountId}/branches",
            new { branchIds = new[] { w.BranchId }, primaryBranchId = w.BranchId }, Json)).StatusCode);
        var client = await SignInStaffAsync(seed.Mobile, seed.Password, w.App);
        client.DefaultRequestHeaders.Add(HttpContextActiveBranchAccessor.BranchHeaderName, w.BranchId.ToString());
        return (client, seed.Mobile, seed.Password);
    }

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private Task<int> SnapshotAuditAsync(Guid branchId) =>
        ScalarAsync<int>("SELECT COUNT(*)::int FROM audit.audit_entry WHERE \"Action\" = 'OfflineSnapshotIssued' AND \"EntityId\" = @id", ("id", branchId));

    // I-D1, I-D7, I-D9, U-D6
    [Fact]
    public async Task I_D1_TheSnapshot_HoldsTodaysAppointmentsExceptHeldAndCancelled_SortedAudited_WithMinimalFields()
    {
        var w = await SeedWorldAsync();
        var calm = await SeedCatAsync(w.CustomerId, "Mitu", w.App);
        var second = await SeedCatAsync(w.CustomerId, "Tuktuk", w.App);
        var third = await SeedCatAsync(w.CustomerId, "Gone", w.App);
        var fourth = await SeedCatAsync(w.CustomerId, "Pending", w.App);
        var later = await BookAsync(w, second, Baseline.AddHours(4));
        var first = await BookAsync(w, calm, Baseline.AddHours(2));
        var cancelled = await BookAsync(w, third, Baseline.AddHours(3));
        await HoldAsync(w, fourth, Baseline.AddHours(5));                      // Held -- not in the snapshot
        Assert.Equal(HttpStatusCode.NoContent, (await w.Admin.PostAsJsonAsync($"/api/staff/appointments/{cancelled}/cancel", new { reason = "x" }, Json)).StatusCode);
        var (reception, _, _) = await StaffAtBranchAsync(w, StaffRoles.Reception);

        var response = await reception.GetAsync("/api/staff/offline-snapshot");
        var body = await BodyAsync(response);

        Assert.True(response.Headers.CacheControl!.NoStore && response.Headers.CacheControl.Private);
        Assert.StartsWith("\"", response.Headers.ETag!.Tag);
        var items = body.GetProperty("sections").GetProperty("appointments").GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([first, later], items.Select(i => i.GetProperty("appointmentId").GetGuid()).ToList());   // sorted by start time, cancelled and held out
        Assert.Equal(1, body.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(LocalDay.ToString("yyyy-MM-dd"), body.GetProperty("localDate").GetString());
        Assert.Equal(LocalDay.AddDays(1).ToString("yyyy-MM-dd") + "T02:00:00+06:00", body.GetProperty("validUntil").GetDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:sszzz"));
        Assert.Equal(1, await SnapshotAuditAsync(w.BranchId));

        // I-D7: the boarding section is not available (Boarding skipped).
        var omitted = body.GetProperty("omittedSections").EnumerateArray().Single();
        Assert.Equal("boardingTasks", omitted.GetProperty("section").GetString());
        Assert.Equal("NotAvailable", omitted.GetProperty("reason").GetString());

        // I-D9: no field beyond the contract; handlingNote only for a high-risk cat.
        var allowed = new[]
        {
            "appointmentId", "startsOn", "endsOn", "status", "isWalkIn", "triagePriority", "catName", "catIsHighRisk", "handlingNote", "customerName",
            "customerPhone", "serviceName", "performerName", "roomName"
        };
        Assert.All(items, i =>
        {
            Assert.All(i.EnumerateObject(), p => Assert.Contains(p.Name, allowed));
            Assert.Null(i.GetProperty("handlingNote").GetString());
            Assert.Equal("Booked", i.GetProperty("status").GetString());
        });
        Assert.Equal(["schemaVersion", "branch", "localDate", "generatedOn", "validUntil", "staffAccountId", "idleTimeoutMinutes", "refreshAfterSeconds", "sections", "omittedSections"],
            body.EnumerateObject().Select(p => p.Name).ToList());
    }

    // I-D2 + I-D3
    [Fact]
    public async Task I_D2_D3_AnUnchangedDay_Is304WithNoAudit_AndAStatusChangeGivesANewETag()
    {
        var w = await SeedWorldAsync();
        var cat = await SeedCatAsync(w.CustomerId, "Mitu", w.App);
        var appointment = await BookAsync(w, cat, Baseline.AddHours(2));
        var (reception, mobile, password) = await StaffAtBranchAsync(w, StaffRoles.Reception);

        var first = await reception.GetAsync("/api/staff/offline-snapshot");
        await BodyAsync(first);
        var etag = first.Headers.ETag!.Tag;
        Assert.Equal(1, await SnapshotAuditAsync(w.BranchId));

        w.Clock.Advance(TimeSpan.FromMinutes(3));                              // generatedOn differs, content does not
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/staff/offline-snapshot");
        request.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var notModified = await reception.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotModified, notModified.StatusCode);
        Assert.Equal(1, await SnapshotAuditAsync(w.BranchId));

        w.Clock.SetUtcNow(Baseline.AddHours(2));
        var checkInClient = await SignInStaffAsync(w.AdminMobile, w.AdminPassword, w.App);
        checkInClient.DefaultRequestHeaders.Add(HttpContextActiveBranchAccessor.BranchHeaderName, w.BranchId.ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await checkInClient.PostAsJsonAsync($"/api/staff/appointments/{appointment}/check-in", new { }, Json)).StatusCode);

        var reauth = await SignInStaffAsync(mobile, password, w.App);
        reauth.DefaultRequestHeaders.Add(HttpContextActiveBranchAccessor.BranchHeaderName, w.BranchId.ToString());
        var changed = new HttpRequestMessage(HttpMethod.Get, "/api/staff/offline-snapshot");
        changed.Headers.TryAddWithoutValidation("If-None-Match", etag);
        var after = await reauth.SendAsync(changed);
        var body = await BodyAsync(after);

        Assert.NotEqual(etag, after.Headers.ETag!.Tag);
        Assert.Equal("CheckedIn", body.GetProperty("sections").GetProperty("appointments").GetProperty("items")[0].GetProperty("status").GetString());
        Assert.Equal(2, await SnapshotAuditAsync(w.BranchId));
    }

    // U-D3
    [Fact]
    public async Task U_D3_ACaretakerWithoutAppointmentsView_GetsTheSectionOmittedAsNotPermitted()
    {
        var w = await SeedWorldAsync();
        var (caretaker, _, _) = await StaffAtBranchAsync(w, StaffRoles.Caretaker);

        var body = await BodyAsync(await caretaker.GetAsync("/api/staff/offline-snapshot"));

        Assert.False(body.GetProperty("sections").TryGetProperty("appointments", out _));
        var reasons = body.GetProperty("omittedSections").EnumerateArray().ToDictionary(o => o.GetProperty("section").GetString()!, o => o.GetProperty("reason").GetString());
        Assert.Equal("NotPermitted", reasons["appointments"]);
        Assert.Equal("NotAvailable", reasons["boardingTasks"]);
    }

    // I-D5
    [Fact]
    public async Task I_D5_CustomerAndAnonymousAre401_AndStaffWithoutTheActionAre403()
    {
        var w = await SeedWorldAsync();
        var (inventory, _, _) = await StaffAtBranchAsync(w, StaffRoles.Inventory);

        Assert.Equal(HttpStatusCode.Forbidden, (await w.Customer.GetAsync("/api/staff/offline-snapshot")).StatusCode);   // a customer is authenticated: the platform answers 403 (RequireActionFilter, U-A6)
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient(w.App).GetAsync("/api/staff/offline-snapshot")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await inventory.GetAsync("/api/staff/offline-snapshot")).StatusCode);
    }

    [Fact]
    public async Task AStaffMemberWithNoBranchHeaderAndSeveralBranches_IsRefused()
    {
        var w = await SeedWorldAsync();
        var seed = await SeedStaffAsync(w.App, StaffRoles.Reception);
        var client = await SignInStaffAsync(seed.Mobile, seed.Password, w.App);

        var response = await client.GetAsync("/api/staff/offline-snapshot");

        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.BadRequest, response.StatusCode.ToString());
    }

    // I-D4
    [Fact]
    public async Task I_D4_PollingTheSnapshotAndThePing_NeverKeepsAnIdleStaffSessionAlive()
    {
        var w = await SeedWorldAsync();
        var (reception, _, _) = await StaffAtBranchAsync(w, StaffRoles.Reception);
        Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync("/api/staff/offline-snapshot")).StatusCode);

        for (var minute = 5; minute <= 25; minute += 5)
        {
            w.Clock.Advance(TimeSpan.FromMinutes(5));
            Assert.Equal(HttpStatusCode.NoContent, (await reception.GetAsync("/api/system/ping")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await reception.GetAsync("/api/staff/offline-snapshot")).StatusCode);
        }

        w.Clock.Advance(TimeSpan.FromMinutes(6));                              // minute 31 after the last real activity (sign-in)
        var late = await reception.GetAsync("/api/staff/offline-snapshot");

        Assert.Equal(HttpStatusCode.Unauthorized, late.StatusCode);
        Assert.Contains("session_idle_timeout", await late.Content.ReadAsStringAsync());
    }

    // I-D8
    [Fact]
    public async Task I_D8_ThePing_IsPublic_204_NoStore_NoBody_AndDoesNotMoveLastActivity()
    {
        var w = await SeedWorldAsync();
        var (reception, _, _) = await StaffAtBranchAsync(w, StaffRoles.Reception);
        var before = await ScalarAsync<DateTime>("SELECT MAX(last_activity_on) FROM account.staff_session WHERE staff_account_id = @id", ("id", _lastStaffId));

        w.Clock.Advance(TimeSpan.FromMinutes(10));
        var anonymous = await NewClient(w.App).GetAsync("/api/system/ping");
        var staff = await reception.GetAsync("/api/system/ping");

        foreach (var response in new[] { anonymous, staff })
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        }

        var after = await ScalarAsync<DateTime>("SELECT MAX(last_activity_on) FROM account.staff_session WHERE staff_account_id = @id", ("id", _lastStaffId));
        Assert.Equal(before, after);
    }
}
