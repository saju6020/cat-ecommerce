using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ADM-001 §16, Rosters &amp; leave (C). I-C1/I-C2/I-C3/I-C4/I-C5/I-C6.</summary>
public sealed class AdminRosterLeaveEndpointTests(AdministrationApiFactory factory) : AdministrationIntegrationTestBase(factory)
{
    private static string NewCode() => "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    [Fact]
    public async Task SetRosterPattern_ThenAvailability_ReflectsTheShift()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var vet = await SeedStaffAsync(StaffRoles.Vet);

        var branchId = await CreateBranchAsync(client);
        var setHours = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/hours", new
        {
            intervals = new[] { new { dayOfWeek = "Monday", opens = "08:00", closes = "18:00" } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setHours.StatusCode);

        // §11.4: the staff member must be assigned to the branch (or the sole-branch rule must
        // apply) -- other tests' branches already make this database multi-branch, so assign
        // explicitly rather than relying on the sole-branch exception.
        var assign = await client.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new
        {
            branchIds = new[] { branchId }, primaryBranchId = branchId
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        var setPattern = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern", new
        {
            slots = new[] { new { dayOfWeek = "Monday", starts = "09:00", ends = "17:00" } }
        }, Json);
        Assert.True(setPattern.StatusCode == HttpStatusCode.NoContent, await setPattern.Content.ReadAsStringAsync());

        var monday = NextMonday();
        var availability = await client.GetAsync(
            $"/api/admin/branches/{branchId}/availability?from={monday:yyyy-MM-dd}&to={monday:yyyy-MM-dd}&staffAccountId={vet.StaffAccountId}&restrictToOpeningHours=true");
        Assert.Equal(HttpStatusCode.OK, availability.StatusCode);
        var intervals = (await availability.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        Assert.Single(intervals);
    }

    /// <summary>Gate fix 2026-09-26: GetBranchRostersQueryHandler now projects its two nested
    /// owned collections (CurrentPattern, NextPattern) in SQL rather than loading the full
    /// StaffRosterAggregate -- this proves a response with actual nested collection content (three
    /// shift slots across three different days) round-trips byte-identical.</summary>
    [Fact]
    public async Task GetBranchRosters_MultipleShiftSlotsAcrossDays_ProjectionReturnsExactNestedContent()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var vet = await SeedStaffAsync(StaffRoles.Vet);

        var branchId = await CreateBranchAsync(client);
        var assign = await client.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new
        {
            branchIds = new[] { branchId }, primaryBranchId = branchId
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        var setPattern = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern", new
        {
            slots = new[]
            {
                new { dayOfWeek = "Monday", starts = "09:00", ends = "13:00" },
                new { dayOfWeek = "Wednesday", starts = "10:00", ends = "18:00" },
                new { dayOfWeek = "Friday", starts = "08:30", ends = "16:30" }
            }
        }, Json);
        Assert.True(setPattern.StatusCode == HttpStatusCode.NoContent, await setPattern.Content.ReadAsStringAsync());

        var get = await client.GetAsync($"/api/admin/branches/{branchId}/rosters");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var rosters = (await get.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        var roster = rosters.Single(r => r.GetProperty("staffAccountId").GetGuid() == vet.StaffAccountId);
        var currentPattern = roster.GetProperty("currentPattern").EnumerateArray().ToList();

        Assert.Equal(3, currentPattern.Count);

        var monday = currentPattern.Single(s => s.GetProperty("dayOfWeek").GetString() == "Monday");
        Assert.Equal("09:00", monday.GetProperty("starts").GetString());
        Assert.Equal("13:00", monday.GetProperty("ends").GetString());

        var wednesday = currentPattern.Single(s => s.GetProperty("dayOfWeek").GetString() == "Wednesday");
        Assert.Equal("10:00", wednesday.GetProperty("starts").GetString());
        Assert.Equal("18:00", wednesday.GetProperty("ends").GetString());

        var friday = currentPattern.Single(s => s.GetProperty("dayOfWeek").GetString() == "Friday");
        Assert.Equal("08:30", friday.GetProperty("starts").GetString());
        Assert.Equal("16:30", friday.GetProperty("ends").GetString());
    }

    [Fact]
    public async Task RequestLeave_Approve_MarksApprovedAndAudited()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var nurseClient = await SignInStaffAsync(nurse.Mobile, nurse.Password);

        var startsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(10);
        var endsOn = startsOn.AddDays(2);

        var request = await nurseClient.PostAsJsonAsync("/api/staff/leave-requests", new
        {
            startsOn = startsOn.ToString("yyyy-MM-dd"), endsOn = endsOn.ToString("yyyy-MM-dd"), kind = "Annual"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var leaveRequestId = (await request.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();

        var approve = await adminClient.PostAsJsonAsync($"/api/admin/leave-requests/{leaveRequestId}/approve", new { }, Json);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var list = await adminClient.GetAsync($"/api/admin/leave-requests?staffAccountId={nurse.StaffAccountId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var items = (await list.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("id").GetGuid() == leaveRequestId && i.GetProperty("status").GetString() == "Approved");
    }

    /// <summary>Gate fix 2026-09-26 (ARCH MEDIUM / SEC LOW): a lower-case status query parameter
    /// must be accepted AND correctly filter -- not just avoid a 500 (regression for a finding that
    /// survived one prior fix pass).</summary>
    [Fact]
    public async Task GetLeaveRequests_LowerCaseStatus_Returns200AndFiltersCorrectly()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var nurseClient = await SignInStaffAsync(nurse.Mobile, nurse.Password);

        var approvedStart = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(40);
        var approvedRequest = await nurseClient.PostAsJsonAsync("/api/staff/leave-requests", new
        {
            startsOn = approvedStart.ToString("yyyy-MM-dd"), endsOn = approvedStart.ToString("yyyy-MM-dd"), kind = "Annual"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, approvedRequest.StatusCode);
        var approvedId = (await approvedRequest.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync($"/api/admin/leave-requests/{approvedId}/approve", new { }, Json)).StatusCode);

        var pendingStart = approvedStart.AddDays(10);
        var pendingRequest = await nurseClient.PostAsJsonAsync("/api/staff/leave-requests", new
        {
            startsOn = pendingStart.ToString("yyyy-MM-dd"), endsOn = pendingStart.ToString("yyyy-MM-dd"), kind = "Annual"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, pendingRequest.StatusCode);
        var pendingId = (await pendingRequest.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();

        var response = await adminClient.GetAsync($"/api/admin/leave-requests?staffAccountId={nurse.StaffAccountId}&status=approved");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, i => i.GetProperty("id").GetGuid() == approvedId);
        Assert.DoesNotContain(items, i => i.GetProperty("id").GetGuid() == pendingId);
        Assert.All(items, i => Assert.Equal("Approved", i.GetProperty("status").GetString()));
    }

    /// <summary>LOW (gate fix 2026-09-26): AuditLeaveDecisionHandler's row must actually carry the
    /// decision note (previously always AfterValue: null, despite the class doc comment claiming
    /// otherwise).</summary>
    [Fact]
    public async Task ApproveLeave_WithDecisionNote_AuditRowIncludesTheNote()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var nurseClient = await SignInStaffAsync(nurse.Mobile, nurse.Password);

        var startsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var endsOn = startsOn.AddDays(1);

        var request = await nurseClient.PostAsJsonAsync("/api/staff/leave-requests", new
        {
            startsOn = startsOn.ToString("yyyy-MM-dd"), endsOn = endsOn.ToString("yyyy-MM-dd"), kind = "Annual"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var leaveRequestId = (await request.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();

        const string decisionNote = "Approved -- covered by the weekend roster.";
        var approve = await adminClient.PostAsJsonAsync($"/api/admin/leave-requests/{leaveRequestId}/approve", new { decisionNote }, Json);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var search = await adminClient.GetAsync(
            $"/api/admin/audit-entries?entityId={leaveRequestId}&entityType=LeaveRequest&action=LeaveApproved&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        var items = (await search.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("afterValue").GetString() == decisionNote);
    }

    [Fact]
    public async Task NurseApprovingLeave_Returns403()
    {
        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var nurseClient = await SignInStaffAsync(nurse.Mobile, nurse.Password);
        var otherNurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var otherClient = await SignInStaffAsync(otherNurse.Mobile, otherNurse.Password);

        var startsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);
        var request = await otherClient.PostAsJsonAsync("/api/staff/leave-requests", new
        {
            startsOn = startsOn.ToString("yyyy-MM-dd"), endsOn = startsOn.ToString("yyyy-MM-dd"), kind = "Sick"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var leaveRequestId = (await request.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();

        var response = await nurseClient.PostAsJsonAsync($"/api/admin/leave-requests/{leaveRequestId}/approve", new { }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>MEDIUM (gate fix 2026-09-26): the handler's own overlap pre-check
    /// (RequestLeaveCommandHandler's ExistAsync) cannot stop two truly concurrent requests from
    /// both passing it before either has committed. The database-level GiST exclusion constraint
    /// (ex_leave_request_staff_no_overlap) is the actual backstop -- this races two overlapping
    /// requests from the same staff member and proves only one succeeds.</summary>
    [Fact]
    public async Task TwoOverlappingLeaveRequests_SentConcurrently_OnlyOneSucceeds()
    {
        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var client = await SignInStaffAsync(nurse.Mobile, nurse.Password);

        var startsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);
        var endsOn = startsOn.AddDays(2);

        var requestA = client.PostAsJsonAsync("/api/staff/leave-requests", new
        {
            startsOn = startsOn.ToString("yyyy-MM-dd"), endsOn = endsOn.ToString("yyyy-MM-dd"), kind = "Sick"
        }, Json);
        var requestB = client.PostAsJsonAsync("/api/staff/leave-requests", new
        {
            startsOn = startsOn.AddDays(1).ToString("yyyy-MM-dd"), endsOn = endsOn.AddDays(1).ToString("yyyy-MM-dd"), kind = "Sick"
        }, Json);
        var responses = await Task.WhenAll(requestA, requestB);

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);

        var rejected = responses.First(r => r.StatusCode == HttpStatusCode.BadRequest);
        Assert.Contains("LeaveOverlapsExistingLeave", await rejected.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RosterableRoleRequired_AccountantOnlyStaff_Returns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var accountant = await SeedStaffAsync(StaffRoles.Accountant);

        var branchId = await CreateBranchAsync(client);
        var response = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{accountant.StaffAccountId}/pattern", new
        {
            slots = new[] { new { dayOfWeek = "Monday", starts = "09:00", ends = "17:00" } }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("StaffNotRosterable", await response.Content.ReadAsStringAsync());
    }

    /// <summary>Gate fix 2026-09-26 (SEC LOW, round 4): (pageNumber-1)*pageSize previously overflowed
    /// int (Skip overflow) and returned an uncaught 500 -- the same class of gap already fixed on
    /// audit search (PagingRules.MaxPageNumber), now shared with this endpoint too.</summary>
    [Fact]
    public async Task GetLeaveRequests_PageNumberAtIntMaxValue_Returns400_Never500()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var response = await client.GetAsync("/api/admin/leave-requests?pageNumber=2147483647&pageSize=100");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>I-C2, TEST gate seventh pass: ADR-047's <c>PromoteDuePattern</c> ("called lazily by
    /// any command that loads this roster... never by a background job") is proven two ways: (1) a
    /// bare <c>GET</c> of the roster listing, even well past the scheduled pattern's effective date,
    /// does NOT itself promote the stored current/next split -- <c>GetBranchRostersQueryHandler</c>
    /// is a pure read with no promotion call, unlike the availability calculator (already covered by
    /// <c>StaffAvailabilityTests</c>, U-C2), which computes the effective pattern fresh on every call
    /// with no persisted state at all; (2) the NEXT command that loads this same roster (here,
    /// scheduling a third, unrelated pattern) promotes it as a side effect, before applying its own
    /// change -- proven because the promoted pattern (B) was never itself the target of any direct
    /// "set current" call.</summary>
    [Fact]
    public async Task ScheduleNextPattern_AdvancePastEffectiveDate_NotPromotedByABareRead_PromotedByTheNextCommand()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider();
        using var client = webFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = admin.Mobile, password = admin.Password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var vet = await SeedStaffAsync(StaffRoles.Vet);
        var branchId = await CreateBranchAsync(client);
        var assign = await client.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new { branchIds = new[] { branchId }, primaryBranchId = branchId }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        // Pattern A: current, set immediately.
        var setCurrent = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern", new
        {
            slots = new[] { new { dayOfWeek = "Monday", starts = "09:00", ends = "17:00" } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setCurrent.StatusCode);

        // The branch's local date (CreateBranchAsync uses Asia/Dhaka), as the server compares it --
        // not the UTC date, which is a day behind from 18:00 to 24:00 UTC.
        var today = CatEcommerce.Domain.DomainServices.BranchCalendar.LocalDateOf(timeProvider.GetUtcNow(), "Asia/Dhaka");
        var effectiveFrom = today.AddDays(1);

        // Pattern B: scheduled as next, effective tomorrow.
        var scheduleNext = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern", new
        {
            slots = new[] { new { dayOfWeek = "Tuesday", starts = "10:00", ends = "18:00" } },
            effectiveFrom
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, scheduleNext.StatusCode);

        var beforeAdvance = await GetRosterAsync(client, branchId, vet.StaffAccountId);
        Assert.Equal(effectiveFrom, DateOnly.Parse(beforeAdvance.GetProperty("nextPatternEffectiveFrom").GetString()!));
        Assert.Single(beforeAdvance.GetProperty("currentPattern").EnumerateArray());

        timeProvider.Advance(TimeSpan.FromDays(3));

        // The 3-day jump idle-times-out (and would eventually exceed the absolute lifetime of) the
        // session above -- staff idle timeout and lifetime both use this same TimeProvider. A fresh
        // sign-in on the same fake clock is needed to keep talking to the server; the roster's own
        // effective-date logic is server-clock-driven and unaffected by which session asks for it.
        using var afterAdvanceClient = webFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });
        var reSignIn = await afterAdvanceClient.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = admin.Mobile, password = admin.Password }, Json);
        Assert.Equal(HttpStatusCode.OK, reSignIn.StatusCode);
        afterAdvanceClient.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(reSignIn, "pc_csrf"));

        // (1) A bare read does not promote -- the stored split is unchanged.
        var afterAdvanceNoWrite = await GetRosterAsync(afterAdvanceClient, branchId, vet.StaffAccountId);
        Assert.Equal(effectiveFrom, DateOnly.Parse(afterAdvanceNoWrite.GetProperty("nextPatternEffectiveFrom").GetString()!));
        Assert.Equal("Monday", afterAdvanceNoWrite.GetProperty("currentPattern")[0].GetProperty("dayOfWeek").GetString());

        // (2) The next roster-loading command (scheduling pattern C) promotes B to current first.
        var newEffectiveFrom = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(10);
        var scheduleThird = await afterAdvanceClient.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern", new
        {
            slots = new[] { new { dayOfWeek = "Wednesday", starts = "08:00", ends = "12:00" } },
            effectiveFrom = newEffectiveFrom
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, scheduleThird.StatusCode);

        var afterPromotingWrite = await GetRosterAsync(afterAdvanceClient, branchId, vet.StaffAccountId);
        var currentPattern = afterPromotingWrite.GetProperty("currentPattern").EnumerateArray().ToList();
        Assert.Single(currentPattern);
        Assert.Equal("Tuesday", currentPattern[0].GetProperty("dayOfWeek").GetString());
        Assert.Equal(newEffectiveFrom, DateOnly.Parse(afterPromotingWrite.GetProperty("nextPatternEffectiveFrom").GetString()!));
        var nextPattern = afterPromotingWrite.GetProperty("nextPattern").EnumerateArray().ToList();
        Assert.Single(nextPattern);
        Assert.Equal("Wednesday", nextPattern[0].GetProperty("dayOfWeek").GetString());
    }

    /// <summary>ACCEPTANCE gate first pass, A6/I-C1: leave's effect on availability, observed end to
    /// end -- the shift shows before the leave is approved, and is gone (the whole day removed, per
    /// §8.5 rule 5) after the SAME admin who set the pattern also approves a leave request covering
    /// that day.</summary>
    [Fact]
    public async Task ApprovedLeaveDay_ThenGetAvailability_ExcludesThatDay()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var vet = await SeedStaffAsync(StaffRoles.Vet);

        var branchId = await CreateBranchAsync(client);
        var setHours = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/hours", new
        {
            intervals = new[] { new { dayOfWeek = "Monday", opens = "08:00", closes = "18:00" } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setHours.StatusCode);

        var assign = await client.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new
        {
            branchIds = new[] { branchId }, primaryBranchId = branchId
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        var setPattern = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern", new
        {
            slots = new[] { new { dayOfWeek = "Monday", starts = "09:00", ends = "17:00" } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setPattern.StatusCode);

        var monday = NextMonday();

        var before = await client.GetAsync(
            $"/api/admin/branches/{branchId}/availability?from={monday:yyyy-MM-dd}&to={monday:yyyy-MM-dd}&staffAccountId={vet.StaffAccountId}&restrictToOpeningHours=true");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Single((await before.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList());

        using var vetClient = await SignInStaffAsync(vet.Mobile, vet.Password);
        var requestLeave = await vetClient.PostAsJsonAsync("/api/staff/leave-requests", new
        {
            startsOn = monday.ToString("yyyy-MM-dd"), endsOn = monday.ToString("yyyy-MM-dd"), kind = "Annual"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, requestLeave.StatusCode);
        var leaveRequestId = (await requestLeave.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();

        var approve = await client.PostAsJsonAsync($"/api/admin/leave-requests/{leaveRequestId}/approve", new { }, Json);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var after = await client.GetAsync(
            $"/api/admin/branches/{branchId}/availability?from={monday:yyyy-MM-dd}&to={monday:yyyy-MM-dd}&staffAccountId={vet.StaffAccountId}&restrictToOpeningHours=true");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Empty((await after.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList());
    }

    /// <summary>ACCEPTANCE gate first pass, A6/I-C1: an ExtraShift roster exception is observed to
    /// ADD availability on a day with no pattern slot of its own (§8.5 rule 4).</summary>
    [Fact]
    public async Task ExtraShiftException_ThenGetAvailability_IncludesTheExtraShift()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var vet = await SeedStaffAsync(StaffRoles.Vet);

        var branchId = await CreateBranchAsync(client);
        var setHours = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/hours", new
        {
            intervals = new[] { new { dayOfWeek = "Tuesday", opens = "08:00", closes = "18:00" } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setHours.StatusCode);

        var assign = await client.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new
        {
            branchIds = new[] { branchId }, primaryBranchId = branchId
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        // The only pattern slot is Monday -- Tuesday has none of its own, so any Tuesday interval
        // must come from the exception.
        var setPattern = await client.PutAsJsonAsync($"/api/admin/branches/{branchId}/rosters/{vet.StaffAccountId}/pattern", new
        {
            slots = new[] { new { dayOfWeek = "Monday", starts = "09:00", ends = "17:00" } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setPattern.StatusCode);

        var tuesday = NextTuesday();

        var before = await client.GetAsync(
            $"/api/admin/branches/{branchId}/availability?from={tuesday:yyyy-MM-dd}&to={tuesday:yyyy-MM-dd}&staffAccountId={vet.StaffAccountId}&restrictToOpeningHours=true");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Empty((await before.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList());

        var addException = await client.PostAsJsonAsync($"/api/admin/branches/{branchId}/roster-exceptions?staffAccountId={vet.StaffAccountId}", new
        {
            kind = "ExtraShift", date = tuesday, starts = "10:00", ends = "12:00", reason = "Cover"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, addException.StatusCode);

        var after = await client.GetAsync(
            $"/api/admin/branches/{branchId}/availability?from={tuesday:yyyy-MM-dd}&to={tuesday:yyyy-MM-dd}&staffAccountId={vet.StaffAccountId}&restrictToOpeningHours=true");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Single((await after.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList());
    }

    private static async Task<JsonElement> GetRosterAsync(HttpClient client, Guid branchId, Guid staffAccountId)
    {
        var response = await client.GetAsync($"/api/admin/branches/{branchId}/rosters");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        return items.Single(r => r.GetProperty("staffAccountId").GetGuid() == staffAccountId);
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

    private static DateOnly NextTuesday()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        while (date.DayOfWeek != DayOfWeek.Tuesday) date = date.AddDays(1);
        return date;
    }
}
