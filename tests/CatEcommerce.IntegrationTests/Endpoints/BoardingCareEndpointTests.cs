using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BRD-001 Phase E -- check-in, the daily task list, completion, overdue, observations and isolation (I-E1 .. I-E10).</summary>
// One collection: the Boarding worker pass reads every stay in the shared database, so classes with different fake clocks must not overlap.
[Collection("Boarding")]
public sealed class BoardingCareEndpointTests(BoardingApiFactory factory) : BoardingIntegrationTestBase(factory)
{
    private async Task<JsonElement> TasksAsync(HttpClient client, DateOnly date) =>
        await ReadAsync(await client.GetAsync($"/api/staff/boarding/tasks?date={date:yyyy-MM-dd}"));

    private static IEnumerable<JsonElement> AllTasks(JsonElement dayList) =>
        dayList.GetProperty("units").EnumerateArray().SelectMany(u => u.GetProperty("tasks").EnumerateArray());

    [Fact]
    public async Task I_E1_AnIncompleteChecklist_ListsTheMissingItems_AndTheStayStaysConfirmed_ThenTheCompleteOneChecksIn()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch);
        await SetRateCardAsync(branch);
        var (customer, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var arrival = LocalToday.AddDays(1);
        var stayId = await ReserveOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
        await RecordDepositAsync(branch, customerId, stayId, "5000.00");
        Assert.Equal(HttpStatusCode.OK, (await branch.Admin.PostAsync($"/api/staff/boarding/stays/{stayId}/confirm", null)).StatusCode);
        Factory.Clock.SetUtcNow(new DateTimeOffset(arrival.Year, arrival.Month, arrival.Day, 14, 30, 0, TimeSpan.FromHours(6)).ToUniversalTime());
        await ReauthenticateAsync(branch.Admin, branch.AdminLogin);

        var incomplete = await branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stayId}/check-in", CheckInBody([cat], complete: false), Json);
        Assert.Equal(HttpStatusCode.BadRequest, incomplete.StatusCode);
        var body = await incomplete.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.CheckInChecklistIncomplete, body);
        Assert.Contains("weight", body);
        Assert.Contains("owner signature", body);
        Assert.Equal("Confirmed", (await ReadAsync(await branch.Admin.GetAsync($"/api/staff/boarding/stays/{stayId}"))).GetProperty("status").GetString());

        var complete = await branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stayId}/check-in", CheckInBody([cat]), Json);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("CheckedIn", (await ReadAsync(complete)).GetProperty("stay").GetProperty("status").GetString());

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var weights = await db.Set<CatEcommerce.Domain.Aggregates.Cat.CatWeightRecordAggregate>().AsNoTracking().Where(w => w.CatId == cat).ToListAsync();
        Assert.Single(weights);
        Assert.Equal(4.2m, weights[0].Weight.Kilograms);
    }

    [Fact]
    public async Task CheckInBeforeTheArrivalDate_IsRefused()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch);
        await SetRateCardAsync(branch);
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var arrival = LocalToday.AddDays(5);
        var stayId = await ReserveOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
        await RecordDepositAsync(branch, customerId, stayId, "5000.00");
        await branch.Admin.PostAsync($"/api/staff/boarding/stays/{stayId}/confirm", null);

        var early = await branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stayId}/check-in", CheckInBody([cat]), Json);

        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Contains(BusinessViolationCodes.CheckInNotOnArrivalDate, await early.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task I_E2_E3_ThePassBuildsTodaysAndTomorrowsList_GroupedByUnit_AndRunningItTwiceAddsNothing()
    {
        var stay = await CheckedInStayAsync();
        var today = LocalToday;

        await RunBoardingWorkCycleAsync();
        var first = await TasksAsync(stay.Branch.Admin, today);
        var firstIds = AllTasks(first).Select(t => t.GetProperty("id").GetGuid()).ToHashSet();
        var tomorrow = await TasksAsync(stay.Branch.Admin, today.AddDays(1));

        await RunBoardingWorkCycleAsync();
        var second = await TasksAsync(stay.Branch.Admin, today);

        Assert.NotEmpty(firstIds);
        Assert.Single(first.GetProperty("units").EnumerateArray());
        Assert.Equal(stay.UnitId, first.GetProperty("units")[0].GetProperty("unitId").GetGuid());
        Assert.NotEmpty(AllTasks(tomorrow));
        Assert.Equal(firstIds, AllTasks(second).Select(t => t.GetProperty("id").GetGuid()).ToHashSet());
        var kinds = AllTasks(tomorrow).Select(t => t.GetProperty("kind").GetString()).ToHashSet();
        Assert.Contains("Feed", kinds);
        Assert.Contains("Medication", kinds);
        Assert.Contains("LitterChange", kinds);
        Assert.All(AllTasks(tomorrow), t => Assert.Equal(stay.Caretaker.StaffAccountId, t.GetProperty("assignedCaretakerId").GetGuid()));
    }

    [Fact]
    public async Task I_E6_CompletingATaskWithAnEmptyBody_RecordsTheCallerAndTime_AndASecondTapIsHarmless()
    {
        var stay = await CheckedInStayAsync();
        await RunBoardingWorkCycleAsync();
        var task = AllTasks(await TasksAsync(stay.Branch.Admin, LocalToday.AddDays(1))).First();
        var taskId = task.GetProperty("id").GetGuid();
        var caretaker = await SignInStaffAsync(stay.Caretaker, stay.Branch.BranchId);

        var first = await caretaker.PostAsync($"/api/staff/boarding/tasks/{taskId}/complete", null);
        var second = await caretaker.PostAsync($"/api/staff/boarding/tasks/{taskId}/complete", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var done = AllTasks(await TasksAsync(caretaker, LocalToday.AddDays(1))).Single(t => t.GetProperty("id").GetGuid() == taskId);
        Assert.Equal("Done", done.GetProperty("status").GetString());
        Assert.Equal(stay.Caretaker.StaffAccountId, done.GetProperty("completedBy").GetGuid());
        Assert.NotEqual(JsonValueKind.Null, done.GetProperty("completedOn").ValueKind);
    }

    [Fact]
    public async Task I_E7_APendingTaskPastItsDuePlusGrace_IsOverdue_OnTheDayList()
    {
        var stay = await CheckedInStayAsync();
        await RunBoardingWorkCycleAsync();
        var tomorrow = LocalToday.AddDays(1);
        var next = AllTasks(await TasksAsync(stay.Branch.Admin, tomorrow)).OrderBy(t => t.GetProperty("dueOn").GetDateTimeOffset()).First();
        var due = next.GetProperty("dueOn").GetDateTimeOffset();
        Assert.False(next.GetProperty("isOverdue").GetBoolean());

        Factory.Clock.SetUtcNow(due.AddMinutes(31).ToUniversalTime());
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);
        var late = AllTasks(await TasksAsync(stay.Branch.Admin, tomorrow)).Single(t => t.GetProperty("id").GetGuid() == next.GetProperty("id").GetGuid());

        Assert.True(late.GetProperty("isOverdue").GetBoolean());
    }

    [Fact]
    public async Task I_E8_TwoNoFoodObservations_FlagTheCatAsNotEating()
    {
        var stay = await CheckedInStayAsync();
        var url = $"/api/staff/boarding/stays/{stay.StayId}/observations";
        object Body(string portion) => new { catId = stay.CatIds[0], portionEaten = portion, waterIntake = "Normal", litterOutput = "Normal", mood = "Relaxed", notes = "ok" };

        Assert.Equal(HttpStatusCode.Created, (await stay.Branch.Admin.PostAsJsonAsync(url, Body("None"), Json)).StatusCode);
        Assert.Empty((await ReadAsync(await stay.Branch.Admin.GetAsync(url))).GetProperty("notEating").EnumerateArray());

        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(HttpStatusCode.Created, (await stay.Branch.Admin.PostAsJsonAsync(url, Body("None"), Json)).StatusCode);
        var flagged = await ReadAsync(await stay.Branch.Admin.GetAsync(url));
        Assert.Equal(stay.CatIds[0], flagged.GetProperty("notEating")[0].GetGuid());

        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        await stay.Branch.Admin.PostAsJsonAsync(url, Body("Half"), Json);
        Assert.Empty((await ReadAsync(await stay.Branch.Admin.GetAsync(url))).GetProperty("notEating").EnumerateArray());
    }

    [Fact]
    public async Task AnObservationForACatThatIsNotBoarding_IsRefused()
    {
        var stay = await CheckedInStayAsync();
        var (_, strangerId, _) = await RegisterAndSignInCustomerAsync();
        var other = await SeedVaccinatedCatAsync(strangerId);

        var refused = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/observations",
            new { catId = other, portionEaten = "Half", waterIntake = "Normal", litterOutput = "Normal", mood = "Relaxed" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(BusinessViolationCodes.ObservationNotAllowed, await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task I_E9_FlaggingACatInAStandardUnit_MovesTheStayToIsolation_AndEntriesAreRecorded()
    {
        var stay = await CheckedInStayAsync();
        var isolation = await CreateUnitAsync(stay.Branch, "Isolation", 1, block: "Z");
        await SetRateCardAsync(stay.Branch, "Isolation", nightly: "1200.00");
        var vet = await SeedStaffAsync(StaffRoles.Vet);
        Assert.Equal(HttpStatusCode.NoContent, (await stay.Branch.Admin.PutAsJsonAsync($"/api/admin/staff/{vet.StaffAccountId}/branches", new { branchIds = new[] { stay.Branch.BranchId }, primaryBranchId = stay.Branch.BranchId }, Json)).StatusCode);
        var vetClient = await SignInStaffAsync(vet, stay.Branch.BranchId);

        var flag = await vetClient.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/cats/{stay.CatIds[0]}/suspected-infectious", new { note = "Sneezing, discharge" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, flag.StatusCode);

        var detail = await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{stay.StayId}"));
        Assert.True(detail.GetProperty("requiresIsolation").GetBoolean());
        Assert.True(detail.GetProperty("inIsolationUnit").GetBoolean());
        Assert.Equal(isolation, detail.GetProperty("unit").GetProperty("unitId").GetGuid());

        var enter = await vetClient.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/isolation-entries", new { purpose = "Medication" }, Json);
        Assert.Equal(HttpStatusCode.Created, enter.StatusCode);
        var entryId = (await ReadAsync(enter)).GetProperty("entryId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await vetClient.PostAsync($"/api/staff/boarding/stays/{stay.StayId}/isolation-entries/{entryId}/exit", null)).StatusCode);
        var entries = (await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{stay.StayId}"))).GetProperty("isolationEntries");
        Assert.Equal(vet.StaffAccountId, entries[0].GetProperty("staffAccountId").GetGuid());
        Assert.NotEqual(JsonValueKind.Null, entries[0].GetProperty("leftOn").ValueKind);
    }

    [Fact]
    public async Task I_E10_WithNoIsolationUnitFree_TheFlagIsRefused_AndNothingChanges_AndAnEntryNeedsIsolation()
    {
        var stay = await CheckedInStayAsync();

        var flag = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/cats/{stay.CatIds[0]}/suspected-infectious", new { note = "Cough" }, Json);
        var entry = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/isolation-entries", new { purpose = "Check" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, flag.StatusCode);
        Assert.Contains(BusinessViolationCodes.NoIsolationUnitAvailable, await flag.Content.ReadAsStringAsync());
        var detail = await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{stay.StayId}"));
        Assert.False(detail.GetProperty("requiresIsolation").GetBoolean());
        Assert.Equal(stay.UnitId, detail.GetProperty("unit").GetProperty("unitId").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, entry.StatusCode);
        Assert.Contains(BusinessViolationCodes.NotInIsolation, await entry.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ACaretakerCannotClearTheFlag_ButAnAdministratorCan_AndReceptionCannotCompleteTasks()
    {
        var stay = await CheckedInStayAsync();
        await RunBoardingWorkCycleAsync();
        var taskId = AllTasks(await TasksAsync(stay.Branch.Admin, LocalToday.AddDays(1))).First().GetProperty("id").GetGuid();
        var reception = await SeedStaffAsync(StaffRoles.Reception);
        Assert.Equal(HttpStatusCode.NoContent, (await stay.Branch.Admin.PutAsJsonAsync($"/api/admin/staff/{reception.StaffAccountId}/branches", new { branchIds = new[] { stay.Branch.BranchId }, primaryBranchId = stay.Branch.BranchId }, Json)).StatusCode);
        var receptionClient = await SignInStaffAsync(reception, stay.Branch.BranchId);
        var caretaker = await SignInStaffAsync(stay.Caretaker, stay.Branch.BranchId);

        var complete = await receptionClient.PostAsync($"/api/staff/boarding/tasks/{taskId}/complete", null);
        var clear = await caretaker.DeleteAsync($"/api/staff/boarding/stays/{stay.StayId}/cats/{stay.CatIds[0]}/suspected-infectious");

        Assert.Equal(HttpStatusCode.Forbidden, complete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, clear.StatusCode);
    }

    [Fact]
    public async Task ACatDeceasedWithALiveStay_FlagsTheStayForAttention_AndCancelsNothing()
    {
        var stay = await CheckedInStayAsync();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Cat.CatAggregate>>();
            var cat = (await repository.GetByIdAsync(stay.CatIds[0]))!;
            cat.MarkDeceased(new CatEcommerce.Domain.Models.CatDeceasedDto(
                LocalToday, null, LocalToday, new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await repository.UpdateAsync(cat, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);
        }

        var detail = await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{stay.StayId}"));
        Assert.True(detail.GetProperty("needsAttention").GetBoolean());
        Assert.Equal("CatDeceased", detail.GetProperty("attentionReason").GetString());
        Assert.Equal("CheckedIn", detail.GetProperty("status").GetString());
    }
}
