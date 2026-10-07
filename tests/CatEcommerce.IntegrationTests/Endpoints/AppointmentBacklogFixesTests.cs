using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// Backlog rows from docs/backlog/APT.md (HO-APT-001 section) fixed on branch ho/BUGS-APT-BE:
/// #8 (by-id staff actions are branch-scoped), #24 (public slots catId needs ownership),
/// #26 (customer detail projection), #27 (customer change rate limit), #28 (performers rate
/// limit), #29 (Start's performer and room belong to the appointment's branch).
/// </summary>
public sealed class AppointmentBacklogFixesTests(AppointmentsApiFactory factory) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly DateTimeOffset BaselineNow = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); // 2026-10-12 10:00 Asia/Dhaka

    /// <summary>#8 -- an admin whose active branch is A cannot read or change an appointment of
    /// branch B by id: every by-id action answers 404 and the appointment stays untouched. The
    /// control proves the same calls reach it once B is the active branch.</summary>
    [Fact]
    public async Task StaffByIdActions_OnAnotherBranchsAppointment_AreNotFound_AndChangeNothing()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var (adminClient, contextA, admin) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);
        var contextB = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var startsOn = BaselineNow.AddHours(2);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, contextB, catId, startsOn);
        var url = $"/api/staff/appointments/{appointmentId}";

        var attempts = new (string Name, Func<Task<HttpResponseMessage>> Send)[]
        {
            ("detail", () => adminClient.GetAsync(url)),
            ("move", () => adminClient.PostAsJsonAsync(url + "/move", new { expectedVersion = 1, startsOn = startsOn.AddHours(1), performerId = contextB.VetStaffAccountId, roomId = contextB.RoomId }, Json)),
            ("cancel", () => adminClient.PostAsJsonAsync(url + "/cancel", new { reason = "not yours" }, Json)),
            ("check-in", () => adminClient.PostAsJsonAsync(url + "/check-in", new { }, Json)),
            ("triage", () => adminClient.PutAsJsonAsync(url + "/triage", new { triagePriority = "Urgent" }, Json)),
            ("start", () => adminClient.PostAsJsonAsync(url + "/start", new { }, Json)),
            ("complete", () => adminClient.PostAsync(url + "/complete", null)),
            ("no-show", () => adminClient.PostAsync(url + "/no-show", null)),
        };

        foreach (var (name, send) in attempts)
        {
            var response = await send();
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{name}: expected 404, got {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        }

        var afterwards = await customerClient.GetFromJsonAsync<JsonElement>($"/api/customers/me/appointments/{appointmentId}", Json);
        Assert.Equal("Booked", afterwards.GetProperty("status").GetString());

        // Control: assigned to both branches, with B active, the same detail read succeeds.
        var assign = await adminClient.PutAsJsonAsync($"/api/admin/staff/{admin.StaffAccountId}/branches",
            new { branchIds = new[] { contextA.BranchId, contextB.BranchId }, primaryBranchId = contextA.BranchId }, Json);
        Assert.True(assign.StatusCode == HttpStatusCode.NoContent, await assign.Content.ReadAsStringAsync());

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, contextB.BranchId.ToString());
        var control = await adminClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, control.StatusCode);
    }

    /// <summary>#29 -- Start refuses a performer or room that belongs to another branch.</summary>
    [Fact]
    public async Task Start_WithAPerformerOrRoomFromAnotherBranch_IsRefused()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var (adminClient, contextA, admin) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);
        var contextB = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var startsOn = BaselineNow.AddHours(2);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, contextA, catId, startsOn);

        timeProvider.SetUtcNow(startsOn);
        await ReauthenticateAsync(adminClient, admin.Mobile, admin.Password);
        var checkIn = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/check-in", new { }, Json);
        Assert.Equal(HttpStatusCode.NoContent, checkIn.StatusCode);

        var foreignPerformer = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/start",
            new { performerId = contextB.VetStaffAccountId }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, foreignPerformer.StatusCode);
        Assert.Contains(BusinessViolationCodes.PerformerNotQualified, await foreignPerformer.Content.ReadAsStringAsync());

        var foreignRoom = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/start",
            new { roomId = contextB.RoomId }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, foreignRoom.StatusCode);
        Assert.Contains(BusinessViolationCodes.RoomNotAvailable, await foreignRoom.Content.ReadAsStringAsync());

        var ownBranch = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/start",
            new { performerId = contextA.VetStaffAccountId, roomId = contextA.RoomId }, Json);
        Assert.True(ownBranch.StatusCode == HttpStatusCode.NoContent, await ownBranch.Content.ReadAsStringAsync());
    }

    /// <summary>#24 -- another customer's cat id on the public slots endpoint must not change the
    /// answer: with a second vet free, the slot taken by that cat stays listed. The cat's owner
    /// still gets the cat-scoped exclusion.</summary>
    [Fact]
    public async Task PublicSlots_CatIdOfSomeoneElsesCat_IsIgnored()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var (adminClient, context, _) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);
        await CreateAndRosterVetAsync(adminClient, context.BranchId);
        await CreateRoomAsync(adminClient, context.BranchId); // a second vet and room, so the slot stays free for someone else

        var (ownerClient, ownerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var ownerCatId = await SeedCatAsync(ownerId);
        var startsOn = BaselineNow.AddHours(2);
        await BookConfirmedAppointmentAsync(ownerClient, context, ownerCatId, startsOn);

        var (otherClient, _, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var date = "2026-10-12";
        var baseUrl = $"/api/booking/slots?branchId={context.BranchId}&serviceId={context.ServiceId}&date={date}";

        var without = await StartsOfAsync(otherClient, baseUrl);
        var withForeignCat = await StartsOfAsync(otherClient, baseUrl + $"&catId={ownerCatId}");

        Assert.Contains(startsOn, without);
        Assert.Equal(without, withForeignCat);

        var withOwnCat = await StartsOfAsync(ownerClient, baseUrl + $"&catId={ownerCatId}");
        Assert.DoesNotContain(startsOn, withOwnCat);
    }

    private static async Task<List<DateTimeOffset>> StartsOfAsync(HttpClient client, string url)
    {
        var body = await client.GetFromJsonAsync<JsonElement>(url, Json);
        return body.GetProperty("slots").EnumerateArray().Select(s => s.GetProperty("startsOn").GetDateTimeOffset()).ToList();
    }

    /// <summary>#26 -- the customer's detail has no staff-only fields, and its history no staff
    /// account ids; the staff detail of the same appointment still carries them.</summary>
    [Fact]
    public async Task CustomerDetail_OmitsStaffOnlyFields_StaffDetailKeepsThem()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var (adminClient, context, _) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, BaselineNow.AddHours(2));

        var detail = await customerClient.GetFromJsonAsync<JsonElement>($"/api/customers/me/appointments/{appointmentId}", Json);

        foreach (var staffOnly in new[] { "version", "allowedTransitions", "triagePriority", "presentingComplaint", "customerId", "roomId", "roomName", "isWalkIn", "channel" })
            Assert.False(detail.TryGetProperty(staffOnly, out _), $"customer detail still exposes '{staffOnly}'");

        var history = detail.GetProperty("statusHistory").EnumerateArray().ToList();
        Assert.NotEmpty(history);
        Assert.All(history, h => Assert.False(h.TryGetProperty("changedBy", out _), "customer history still exposes changedBy"));

        // What the customer UI actually needs is still there.
        foreach (var needed in new[] { "id", "branchId", "branchTimeZoneId", "catId", "catName", "service", "performerId", "performerName",
                     "startsOn", "endsOn", "status", "holdExpiresOn", "isWaitlistOffer", "reasonForVisit", "canChange", "changeCutoffOn" })
            Assert.True(detail.TryGetProperty(needed, out _), $"customer detail lost '{needed}'");

        var staffDetail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentId}", Json);
        foreach (var staffField in new[] { "version", "allowedTransitions", "triagePriority", "presentingComplaint", "customerId" })
            Assert.True(staffDetail.TryGetProperty(staffField, out _), $"staff detail lost '{staffField}'");
        Assert.All(staffDetail.GetProperty("statusHistory").EnumerateArray(), h => Assert.True(h.TryGetProperty("changedBy", out _)));
    }

    /// <summary>#27 -- reschedule, confirm, cancel and waitlist join are rate limited per customer.
    /// The refusal comes ahead of the handler, so a not-found id still counts as a request.</summary>
    [Theory]
    [InlineData("confirm")]
    [InlineData("reschedule")]
    [InlineData("cancel")]
    [InlineData("waitlist")]
    public async Task CustomerChangeEndpoints_AreRateLimitedPerCustomer(string endpoint)
    {
        using var tightFactory = Factory.WithTightClinicRateLimits();
        var (client, _, _) = await RegisterAndSignInCustomerOnAsync(tightFactory);

        Task<HttpResponseMessage> Send() => endpoint switch
        {
            "confirm" => client.PostAsJsonAsync($"/api/customers/me/appointments/{Guid.NewGuid()}/confirm", new { }, Json),
            "reschedule" => client.PostAsJsonAsync($"/api/customers/me/appointments/{Guid.NewGuid()}/reschedule", new { startsOn = BaselineNow.AddDays(30) }, Json),
            "cancel" => client.PostAsJsonAsync($"/api/customers/me/appointments/{Guid.NewGuid()}/cancel", new { }, Json),
            _ => client.PostAsJsonAsync("/api/customers/me/waitlist", new { branchId = Guid.NewGuid(), serviceId = Guid.NewGuid(), catId = Guid.NewGuid(), localDate = "2026-10-20" }, Json),
        };

        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Send()).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Send()).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send()).StatusCode);
    }

    /// <summary>#28 -- the public performers lookup is rate limited.</summary>
    [Fact]
    public async Task PublicPerformers_IsRateLimited()
    {
        using var tightFactory = Factory.WithTightClinicRateLimits();
        var client = CreateClientOn(tightFactory);
        var url = $"/api/booking/performers?branchId={Guid.NewGuid()}&serviceId={Guid.NewGuid()}";

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.GetAsync(url)).StatusCode);
    }
}
