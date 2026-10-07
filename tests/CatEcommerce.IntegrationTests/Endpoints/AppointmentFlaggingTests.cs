using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-APT-001 §16 I-F6, I-F7 -- ACCEPTANCE gate fix (2026-09-28, A21). Approving a vet's leave, or
/// adding a branch closure over a booked day, flags the affected (already-Booked) appointments for
/// rescheduling (§8.10/ADR-076) -- via the real HO-ADM-001 leave/closure endpoints, never dispatched
/// directly.
/// </summary>
public sealed class AppointmentFlaggingTests(AppointmentsApiFactory factory) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly DateTimeOffset BaselineNow = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); // 2026-10-12 10:00 Asia/Dhaka (a Monday)

    /// <summary>I-F6 -- approving a vet's leave over two future bookings flags both PerformerOnLeave;
    /// both listed in the staff /flagged view; cancelling the leave clears both.</summary>
    [Fact]
    public async Task IF6_ApproveVetLeaveOverTwoBookings_FlagsBoth_ListedInFlagged_CancelClearsBoth()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var (adminClient, context, _) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);
        using var vetClient = await SignInStaffOnAsync(apiFactory, context.VetMobile, context.VetPassword);

        var (customerAClient, customerAId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catAId = await SeedCatAsync(customerAId);
        var firstStart = BaselineNow.AddHours(2); // Monday 12:00 Dhaka -- local date 2026-10-12
        var firstAppointmentId = await BookConfirmedAppointmentAsync(customerAClient, context, catAId, firstStart);

        var (customerBClient, customerBId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catBId = await SeedCatAsync(customerBId);
        var secondStart = BaselineNow.AddHours(26); // Tuesday 12:00 Dhaka -- local date 2026-10-13
        var secondAppointmentId = await BookConfirmedAppointmentAsync(customerBClient, context, catBId, secondStart);

        var leaveRequest = await vetClient.PostAsJsonAsync("/api/staff/leave-requests", new
        { startsOn = "2026-10-12", endsOn = "2026-10-13", kind = "Annual" }, Json);
        Assert.True(leaveRequest.StatusCode == HttpStatusCode.Created, await leaveRequest.Content.ReadAsStringAsync());
        var leaveRequestId = (await leaveRequest.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();

        var approve = await adminClient.PostAsJsonAsync($"/api/admin/leave-requests/{leaveRequestId}/approve", new { }, Json);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var firstDetail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{firstAppointmentId}", Json);
        Assert.True(firstDetail.GetProperty("needsRescheduling").GetBoolean());
        Assert.Equal("PerformerOnLeave", firstDetail.GetProperty("rescheduleFlagReason").GetString());

        var secondDetail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{secondAppointmentId}", Json);
        Assert.True(secondDetail.GetProperty("needsRescheduling").GetBoolean());
        Assert.Equal("PerformerOnLeave", secondDetail.GetProperty("rescheduleFlagReason").GetString());

        var flagged = await adminClient.GetFromJsonAsync<JsonElement>("/api/staff/appointments/flagged", Json);
        var flaggedIds = flagged.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToHashSet();
        Assert.Contains(firstAppointmentId, flaggedIds);
        Assert.Contains(secondAppointmentId, flaggedIds);

        var cancel = await adminClient.PostAsJsonAsync($"/api/admin/leave-requests/{leaveRequestId}/cancel", new { }, Json);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var firstAfterCancel = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{firstAppointmentId}", Json);
        Assert.False(firstAfterCancel.GetProperty("needsRescheduling").GetBoolean());

        var secondAfterCancel = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{secondAppointmentId}", Json);
        Assert.False(secondAfterCancel.GetProperty("needsRescheduling").GetBoolean());

        var flaggedAfterCancel = await adminClient.GetFromJsonAsync<JsonElement>("/api/staff/appointments/flagged", Json);
        var flaggedIdsAfterCancel = flaggedAfterCancel.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToHashSet();
        Assert.DoesNotContain(firstAppointmentId, flaggedIdsAfterCancel);
        Assert.DoesNotContain(secondAppointmentId, flaggedIdsAfterCancel);
    }

    /// <summary>I-F7 -- a branch closure over a booked day flags the affected appointment
    /// BranchClosed.</summary>
    [Fact]
    public async Task IF7_BranchClosureOverBookedDay_FlagsBranchClosed()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var (adminClient, context, _) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var startsOn = BaselineNow.AddHours(2); // Monday 12:00 Dhaka -- local date 2026-10-12
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, startsOn);

        var closure = await adminClient.PostAsJsonAsync($"/api/admin/branches/{context.BranchId}/closures", new
        { startsOn = "2026-10-12", endsOn = "2026-10-12", kind = "PublicHoliday", reason = "Test holiday" }, Json);
        Assert.True(closure.StatusCode == HttpStatusCode.Created, await closure.Content.ReadAsStringAsync());

        var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentId}", Json);
        Assert.True(detail.GetProperty("needsRescheduling").GetBoolean());
        Assert.Equal("BranchClosed", detail.GetProperty("rescheduleFlagReason").GetString());

        var flagged = await adminClient.GetFromJsonAsync<JsonElement>("/api/staff/appointments/flagged", Json);
        var flaggedIds = flagged.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToHashSet();
        Assert.Contains(appointmentId, flaggedIds);
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
