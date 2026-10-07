using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Branches;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-APT-001 §16 -- I-E1, I-E3, I-E4, I-E5.</summary>
public sealed class AppointmentVisitFlowTests(AppointmentsApiFactory factory) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly DateTimeOffset BaselineNow = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); // 2026-10-12 10:00 Asia/Dhaka

    /// <summary>I-E3 -- check-in moves booked -> checked-in, and the cat appears on that vet's queue.</summary>
    [Fact]
    public async Task CheckIn_BookedAppointment_AppearsOnVetsQueue()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var (adminClient, context, admin) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var startsOn = BaselineNow.AddHours(2);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, startsOn);

        timeProvider.SetUtcNow(startsOn);
        await ReauthenticateAsync(adminClient, admin.Mobile, admin.Password);
        var checkIn = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/check-in", new { }, Json);
        Assert.True(checkIn.StatusCode == HttpStatusCode.NoContent, await checkIn.Content.ReadAsStringAsync());

        var queue = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/queue?performerId={context.VetStaffAccountId}", Json);
        var entries = queue.GetProperty("entries").EnumerateArray().ToList();

        Assert.Contains(entries, e => e.GetProperty("appointmentId").GetGuid() == appointmentId);
    }

    /// <summary>I-E4 -- start then complete succeed; a further check-in on a completed appointment
    /// is refused (US-CLN-10 AC2).</summary>
    [Fact]
    public async Task StartThenComplete_Succeeds_FurtherCheckIn_IsInvalidTransition()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var (adminClient, context, admin) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var startsOn = BaselineNow.AddHours(2);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, startsOn);

        timeProvider.SetUtcNow(startsOn);
        await ReauthenticateAsync(adminClient, admin.Mobile, admin.Password);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/check-in", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/start", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync($"/api/staff/appointments/{appointmentId}/complete", null)).StatusCode);

        var secondCheckIn = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/check-in", new { }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, secondCheckIn.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidAppointmentTransition, await secondCheckIn.Content.ReadAsStringAsync());
    }

    /// <summary>I-E5 -- a no-show before the grace period is refused; after, it succeeds and the
    /// count is visible on the customer summary.</summary>
    [Fact]
    public async Task RecordNoShow_BeforeGrace_Refused_AfterGrace_Succeeds_AndCountsOnSummary()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var (adminClient, context, admin) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var startsOn = BaselineNow.AddHours(2);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, startsOn);

        timeProvider.SetUtcNow(startsOn.AddMinutes(10)); // grace default is 15 minutes
        await ReauthenticateAsync(adminClient, admin.Mobile, admin.Password);
        var tooEarly = await adminClient.PostAsync($"/api/staff/appointments/{appointmentId}/no-show", null);
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.StatusCode);
        Assert.Contains(BusinessViolationCodes.NoShowTooEarly, await tooEarly.Content.ReadAsStringAsync());

        timeProvider.SetUtcNow(startsOn.AddMinutes(15));
        await ReauthenticateAsync(adminClient, admin.Mobile, admin.Password);
        var noShow = await adminClient.PostAsync($"/api/staff/appointments/{appointmentId}/no-show", null);
        Assert.True(noShow.StatusCode == HttpStatusCode.NoContent, await noShow.Content.ReadAsStringAsync());

        var summary = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/customers/{customerId}/appointment-summary", Json);
        Assert.Equal(1, summary.GetProperty("noShowCount").GetInt32());
    }

    /// <summary>I-E1 -- walk-ins queue with triage; emergencies first.</summary>
    [Fact]
    public async Task RegisterWalkIns_EmergencyOrdersFirst_WithEstimatedWait()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;
        var (adminClient, context, _) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var (_, routineCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var routineCatId = await SeedCatAsync(routineCustomerId);
        var (_, emergencyCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var emergencyCatId = await SeedCatAsync(emergencyCustomerId);

        var routineWalkIn = await adminClient.PostAsJsonAsync("/api/staff/walk-ins", new
        { customerId = routineCustomerId, catId = routineCatId, serviceId = context.ServiceId, triagePriority = "Routine" }, Json);
        Assert.True(routineWalkIn.StatusCode == HttpStatusCode.Created, await routineWalkIn.Content.ReadAsStringAsync());

        var emergencyWalkIn = await adminClient.PostAsJsonAsync("/api/staff/walk-ins", new
        { customerId = emergencyCustomerId, catId = emergencyCatId, serviceId = context.ServiceId, triagePriority = "Emergency" }, Json);
        Assert.True(emergencyWalkIn.StatusCode == HttpStatusCode.Created, await emergencyWalkIn.Content.ReadAsStringAsync());
        var emergencyAppointmentId = (await emergencyWalkIn.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("appointmentId").GetGuid();

        var queue = await adminClient.GetFromJsonAsync<JsonElement>("/api/staff/queue", Json);
        var entries = queue.GetProperty("entries").EnumerateArray().ToList();

        Assert.Equal(emergencyAppointmentId, entries[0].GetProperty("appointmentId").GetGuid());
        Assert.True(entries[0].TryGetProperty("estimatedWaitMinutes", out _));
    }
}
