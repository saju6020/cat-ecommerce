using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-APT-001 §16 I-F2..I-F4 -- ACCEPTANCE gate fix (2026-09-28, A14/A15). Every conflict code a
/// staff <c>Move</c> can be refused with, each with a message naming the conflicting local time
/// (§10.4, US-CLN-07 AC1/AC2) -- and the version/concurrency guards around the same endpoint.
/// </summary>
public sealed class AppointmentMoveConflictTests(AppointmentsApiFactory factory) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly DateTimeOffset BaselineNow = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); // 2026-10-12 10:00 Asia/Dhaka (a Monday)

    private sealed record MoveConflictContext(
        HttpClient AdminClient, ClinicTestContext Context,
        Guid Vet2Id, Guid Room2Id, Guid NarrowVetId, Guid NurseId, Guid TreatmentRoomId);

    /// <summary>One branch with everything I-F2's six conflict codes each need: a second vet and a
    /// second Consultation room (for RoomBusy without also tripping PerformerBusy), a vet rostered
    /// only 09:00-09:30 (for PerformerNotAvailable), a Nurse (unqualified for the Vet-only service,
    /// for PerformerNotQualified) and a Treatment room (for RoomKindMismatch).</summary>
    private async Task<MoveConflictContext> SeedMoveConflictContextAsync(WebApplicationFactory<Program> apiFactory)
    {
        var (adminClient, context, _) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var vet2 = await CreateAndRosterVetAsync(adminClient, context.BranchId);
        var room2Id = await CreateRoomAsync(adminClient, context.BranchId);

        var narrowVet = await SeedStaffAsync(StaffRoles.Vet);
        var assignNarrow = await adminClient.PutAsJsonAsync($"/api/admin/staff/{narrowVet.StaffAccountId}/branches", new { branchIds = new[] { context.BranchId }, primaryBranchId = context.BranchId }, Json);
        Assert.True(assignNarrow.StatusCode == HttpStatusCode.NoContent, await assignNarrow.Content.ReadAsStringAsync());
        await NarrowVetToOneSlotADayAsync(adminClient, context.BranchId, narrowVet.StaffAccountId, "09:00", "09:30");

        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        var assignNurse = await adminClient.PutAsJsonAsync($"/api/admin/staff/{nurse.StaffAccountId}/branches", new { branchIds = new[] { context.BranchId }, primaryBranchId = context.BranchId }, Json);
        Assert.True(assignNurse.StatusCode == HttpStatusCode.NoContent, await assignNurse.Content.ReadAsStringAsync());
        var nurseSlots = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" }
            .Select(d => new { dayOfWeek = d, starts = "00:00", ends = "23:45" }).ToArray();
        var nurseRoster = await adminClient.PutAsJsonAsync($"/api/admin/branches/{context.BranchId}/rosters/{nurse.StaffAccountId}/pattern", new { slots = nurseSlots }, Json);
        Assert.True(nurseRoster.StatusCode == HttpStatusCode.NoContent, await nurseRoster.Content.ReadAsStringAsync());

        var treatmentRoomId = await CreateRoomAsync(adminClient, context.BranchId, "Treatment");

        return new MoveConflictContext(adminClient, context, vet2.StaffAccountId, room2Id, narrowVet.StaffAccountId, nurse.StaffAccountId, treatmentRoomId);
    }

    /// <summary>I-F2 -- vet busy: moving onto a time vet1 already has another appointment refuses
    /// PerformerBusy, naming vet1's own conflicting local time.</summary>
    [Fact]
    public async Task IF2_MoveOntoVetBusyTime_RefusesPerformerBusy_NamingTheConflictingLocalTime()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var ctx = await SeedMoveConflictContextAsync(apiFactory);

        var busyStart = BaselineNow.AddHours(4); // 14:00 Dhaka
        var (fillerClient, fillerCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var fillerCatId = await SeedCatAsync(fillerCustomerId);
        await BookConfirmedAppointmentAsync(fillerClient, ctx.Context, fillerCatId, busyStart, ctx.Context.VetStaffAccountId);

        var (movingClient, movingCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var movingCatId = await SeedCatAsync(movingCustomerId);
        var movingAppointmentId = await BookConfirmedAppointmentAsync(movingClient, ctx.Context, movingCatId, BaselineNow.AddHours(2), ctx.Vet2Id);

        var detail = await ctx.AdminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{movingAppointmentId}", Json);
        var expectedVersion = detail.GetProperty("version").GetInt32();

        var move = await ctx.AdminClient.PostAsJsonAsync($"/api/staff/appointments/{movingAppointmentId}/move", new
        { expectedVersion, startsOn = busyStart, performerId = ctx.Context.VetStaffAccountId, roomId = ctx.Context.RoomId }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        var body = await move.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.PerformerBusy, body);
        Assert.Contains("14:00", body);
        Assert.Contains("14:30", body);
    }

    /// <summary>I-F2 -- room busy: vet2 is free, but room1 is occupied at the target time by a
    /// different vet's appointment -- refuses RoomBusy, naming room1's own conflicting local time.</summary>
    [Fact]
    public async Task IF2_MoveOntoRoomBusyTime_RefusesRoomBusy_NamingTheConflictingLocalTime()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var ctx = await SeedMoveConflictContextAsync(apiFactory);

        // The customer hold endpoint never accepts a RoomId (auto-assigned) -- booked directly by
        // staff instead so room1 specifically, not whichever room auto-assignment would pick, is
        // the one left occupied at 14:00.
        var busyStart = BaselineNow.AddHours(4); // 14:00 Dhaka -- vet1 in room1
        var (fillerClient, fillerCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var fillerCatId = await SeedCatAsync(fillerCustomerId);
        fillerClient.Dispose();
        var fillerBook = await ctx.AdminClient.PostAsJsonAsync("/api/staff/appointments", new
        {
            customerId = fillerCustomerId, catId = fillerCatId, serviceId = ctx.Context.ServiceId, startsOn = busyStart,
            performerId = ctx.Context.VetStaffAccountId, roomId = ctx.Context.RoomId
        }, Json);
        Assert.True(fillerBook.StatusCode == HttpStatusCode.Created, await fillerBook.Content.ReadAsStringAsync());

        var (movingClient, movingCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var movingCatId = await SeedCatAsync(movingCustomerId);
        var movingAppointmentId = await BookConfirmedAppointmentAsync(movingClient, ctx.Context, movingCatId, BaselineNow.AddHours(2), ctx.Vet2Id);

        var detail = await ctx.AdminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{movingAppointmentId}", Json);
        var expectedVersion = detail.GetProperty("version").GetInt32();

        // vet2 is free at 14:00 -- only room1 (explicitly requested) is occupied.
        var move = await ctx.AdminClient.PostAsJsonAsync($"/api/staff/appointments/{movingAppointmentId}/move", new
        { expectedVersion, startsOn = busyStart, performerId = ctx.Vet2Id, roomId = ctx.Context.RoomId }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        var body = await move.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.RoomBusy, body);
        Assert.Contains("14:00", body);
        Assert.Contains("14:30", body);
    }

    /// <summary>I-F2 -- cat already booked: the SAME cat already has another appointment at the
    /// target time (a different performer and room, both otherwise free) -- refuses CatAlreadyBooked,
    /// naming the cat's own conflicting local time.</summary>
    [Fact]
    public async Task IF2_MoveOntoCatAlreadyBookedTime_RefusesCatAlreadyBooked_NamingTheConflictingLocalTime()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var ctx = await SeedMoveConflictContextAsync(apiFactory);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);

        var firstStart = BaselineNow.AddHours(4); // 14:00 Dhaka, vet1/room1
        await BookConfirmedAppointmentAsync(customerClient, ctx.Context, catId, firstStart, ctx.Context.VetStaffAccountId);

        // A second, distinct appointment for the SAME cat, elsewhere in the day, with vet2/room2 --
        // both free at 14:00, so moving it there isolates the CatAlreadyBooked conflict alone.
        var secondAppointmentId = await BookConfirmedAppointmentAsync(customerClient, ctx.Context, catId, BaselineNow.AddHours(8), ctx.Vet2Id);

        var detail = await ctx.AdminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{secondAppointmentId}", Json);
        var expectedVersion = detail.GetProperty("version").GetInt32();

        var move = await ctx.AdminClient.PostAsJsonAsync($"/api/staff/appointments/{secondAppointmentId}/move", new
        { expectedVersion, startsOn = firstStart, performerId = ctx.Vet2Id, roomId = ctx.Room2Id }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        var body = await move.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.CatAlreadyBooked, body);
        Assert.Contains("14:00", body);
        Assert.Contains("14:30", body);
    }

    /// <summary>I-F2 -- performer not available: moving onto a time outside the target vet's own
    /// roster (narrowed to 09:00-09:30) refuses PerformerNotAvailable.</summary>
    [Fact]
    public async Task IF2_MoveOntoTimeOutsidePerformerRoster_RefusesPerformerNotAvailable()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var ctx = await SeedMoveConflictContextAsync(apiFactory);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, ctx.Context, catId, BaselineNow.AddHours(2), ctx.Context.VetStaffAccountId);

        var detail = await ctx.AdminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentId}", Json);
        var expectedVersion = detail.GetProperty("version").GetInt32();

        // 15:00 Dhaka -- well outside the narrow vet's own 09:00-09:30 roster.
        var move = await ctx.AdminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/move", new
        { expectedVersion, startsOn = BaselineNow.AddHours(5), performerId = ctx.NarrowVetId, roomId = ctx.Context.RoomId }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        Assert.Contains(BusinessViolationCodes.PerformerNotAvailable, await move.Content.ReadAsStringAsync());
    }

    /// <summary>I-F2 -- performer not qualified: a Nurse is not among the Vet-only service's
    /// qualified roles -- refuses PerformerNotQualified regardless of the time chosen.</summary>
    [Fact]
    public async Task IF2_MoveOntoUnqualifiedPerformer_RefusesPerformerNotQualified()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var ctx = await SeedMoveConflictContextAsync(apiFactory);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, ctx.Context, catId, BaselineNow.AddHours(2), ctx.Context.VetStaffAccountId);

        var detail = await ctx.AdminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentId}", Json);
        var expectedVersion = detail.GetProperty("version").GetInt32();

        var move = await ctx.AdminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/move", new
        { expectedVersion, startsOn = BaselineNow.AddHours(5), performerId = ctx.NurseId, roomId = ctx.Context.RoomId }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        Assert.Contains(BusinessViolationCodes.PerformerNotQualified, await move.Content.ReadAsStringAsync());
    }

    /// <summary>I-F2 -- room kind mismatch: the requested room is a Treatment room, but the service
    /// requires Consultation -- refuses RoomKindMismatch.</summary>
    [Fact]
    public async Task IF2_MoveOntoWrongRoomKind_RefusesRoomKindMismatch()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var ctx = await SeedMoveConflictContextAsync(apiFactory);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, ctx.Context, catId, BaselineNow.AddHours(2), ctx.Context.VetStaffAccountId);

        var detail = await ctx.AdminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentId}", Json);
        var expectedVersion = detail.GetProperty("version").GetInt32();

        var move = await ctx.AdminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/move", new
        { expectedVersion, startsOn = BaselineNow.AddHours(5), performerId = ctx.Context.VetStaffAccountId, roomId = ctx.TreatmentRoomId }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        Assert.Contains(BusinessViolationCodes.RoomKindMismatch, await move.Content.ReadAsStringAsync());
    }

    /// <summary>I-F3 -- a stale expectedVersion (the calendar changed since the caller loaded the
    /// detail) is refused 409 StaleCalendarVersion.</summary>
    [Fact]
    public async Task IF3_MoveWithStaleExpectedVersion_Returns409StaleCalendarVersion()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);
        await ActivateBranchForAdminAsync(admin.StaffAccountId, context.BranchId, adminClient);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, BaselineNow.AddHours(2));

        var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentId}", Json);
        var staleVersion = detail.GetProperty("version").GetInt32();

        // Bump the version for real with one successful move first.
        var firstMove = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/move", new
        { expectedVersion = staleVersion, startsOn = BaselineNow.AddHours(3), performerId = context.VetStaffAccountId, roomId = context.RoomId }, Json);
        Assert.True(firstMove.StatusCode == HttpStatusCode.OK, await firstMove.Content.ReadAsStringAsync());

        // Retry with the now-stale version.
        var staleMove = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/move", new
        { expectedVersion = staleVersion, startsOn = BaselineNow.AddHours(4), performerId = context.VetStaffAccountId, roomId = context.RoomId }, Json);

        Assert.Equal(HttpStatusCode.Conflict, staleMove.StatusCode);
        Assert.Contains(BusinessViolationCodes.StaleCalendarVersion, await staleMove.Content.ReadAsStringAsync());
    }

    /// <summary>I-F4 -- two staff move two different appointments into the same vet/room/time
    /// concurrently: exactly one succeeds; the other is refused -- one of three shapes, all correct,
    /// depending on exactly how the race lands: synchronously (PerformerBusy/RoomBusy, 400 via
    /// <c>SlotClaim.Diagnose</c>) if its own read lands after the winner's commit; by a clean 23P01
    /// exclusion violation (409, the SPECIFIC resource code -- PerformerBusy/RoomBusy/CatAlreadyBooked
    /// -- via <c>UniqueConstraintExceptionFilter</c>'s own <c>AppointmentConflictIndexes</c> mapping,
    /// which keeps that specificity on a staff route rather than collapsing to the generic code a
    /// customer route would get, §12.5); or, under enough concurrency, by Postgres reporting the
    /// loser's transaction as a 40P01 deadlock instead of a clean 23P01 -- which carries no
    /// constraint name, so it maps to the generic 409 SlotNoLongerAvailable instead (same filter's
    /// own deadlock branch, ARCHITECTURE gate second pass HIGH B). Never a second success, never a
    /// 500. Proven flaky across repeated real runs without accepting all three shapes.</summary>
    [Fact]
    public async Task IF4_TwoConcurrentMovesIntoTheSameSlot_ExactlyOneSucceeds()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClientA = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        using var adminClientB = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClientA, BaselineNow);
        await ActivateBranchForAdminAsync(admin.StaffAccountId, context.BranchId, adminClientA, adminClientB);

        var (customerAClient, customerAId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catAId = await SeedCatAsync(customerAId);
        var appointmentAId = await BookConfirmedAppointmentAsync(customerAClient, context, catAId, BaselineNow.AddHours(2));

        var (customerBClient, customerBId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catBId = await SeedCatAsync(customerBId);
        var appointmentBId = await BookConfirmedAppointmentAsync(customerBClient, context, catBId, BaselineNow.AddHours(3));

        var detailA = await adminClientA.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentAId}", Json);
        var versionA = detailA.GetProperty("version").GetInt32();
        var detailB = await adminClientB.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentBId}", Json);
        var versionB = detailB.GetProperty("version").GetInt32();

        var targetStart = BaselineNow.AddHours(8);
        var moveA = adminClientA.PostAsJsonAsync($"/api/staff/appointments/{appointmentAId}/move", new
        { expectedVersion = versionA, startsOn = targetStart, performerId = context.VetStaffAccountId, roomId = context.RoomId }, Json);
        var moveB = adminClientB.PostAsJsonAsync($"/api/staff/appointments/{appointmentBId}/move", new
        { expectedVersion = versionB, startsOn = targetStart, performerId = context.VetStaffAccountId, roomId = context.RoomId }, Json);

        var responses = await Task.WhenAll(moveA, moveB);

        var succeeded = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1, succeeded);

        var loser = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
        var loserBody = await loser.Content.ReadAsStringAsync();
        // Three shapes are all correct, depending on exactly how the race lands: a synchronous
        // Diagnose refusal (400, PerformerBusy/RoomBusy) if the loser's own read happens after the
        // winner's commit; a clean 23P01 exclusion violation (409, the SPECIFIC resource code --
        // PerformerBusy/RoomBusy/CatAlreadyBooked -- via UniqueConstraintExceptionFilter's own
        // AppointmentConflictIndexes mapping, staff route); or, under this much concurrency,
        // Postgres reporting the loser's transaction as a 40P01 deadlock instead of a clean 23P01 --
        // which carries no constraint name to route through that dictionary, so it maps to the
        // generic 409 SlotNoLongerAvailable instead (same filter, its own deadlock branch).
        var loserNamesAKnownConflict = loserBody.Contains(BusinessViolationCodes.PerformerBusy) ||
            loserBody.Contains(BusinessViolationCodes.RoomBusy) || loserBody.Contains(BusinessViolationCodes.CatAlreadyBooked) ||
            loserBody.Contains(BusinessViolationCodes.SlotNoLongerAvailable);
        Assert.True(
            (loser.StatusCode == HttpStatusCode.BadRequest || loser.StatusCode == HttpStatusCode.Conflict) && loserNamesAKnownConflict,
            $"Unexpected loser response: {(int)loser.StatusCode} {loserBody}");
    }

    private static async Task<HttpClient> SignInStaffOnAsync(WebApplicationFactory<Program> apiFactory, string mobile, string password)
    {
        var client = apiFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return client;
    }
}
