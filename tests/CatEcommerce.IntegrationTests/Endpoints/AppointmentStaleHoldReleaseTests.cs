using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.Commands.Clinic;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// Gate fix (HO-APT-001-ARCHITECTURE, HIGH #1) -- ADR-070/§11.4 step 3 requires every write that
/// claims a slot to first expire the stale holds overlapping it, not only C1 (place hold). These
/// prove C4 (reschedule), C6 (staff booking), C7 (move) and C15 (waitlist offer) each do so: a slot
/// left behind by an expired-but-not-yet-released <c>Held</c> row is still claimable, not a 409/500.
/// </summary>
public sealed class AppointmentStaleHoldReleaseTests(AppointmentsApiFactory factory) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly DateTimeOffset BaselineNow = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); // 2026-10-12 10:00 Asia/Dhaka (a Monday)

    /// <summary>C4 -- a customer reschedules into a slot a stale (expired but not yet released)
    /// hold still occupies.</summary>
    [Fact]
    public async Task Reschedule_IntoSlotWithExpiredHold_Succeeds()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);

        var originalStart = BaselineNow.AddHours(5);
        var targetStart = BaselineNow.AddHours(6);

        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, originalStart);

        // A different customer's stale hold on the target slot -- never confirmed, never released.
        var (otherClient, otherCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var otherCatId = await SeedCatAsync(otherCustomerId);
        var staleHold = await otherClient.PostAsJsonAsync("/api/customers/me/appointment-holds", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId = otherCatId, startsOn = targetStart, performerId = context.VetStaffAccountId }, Json);
        Assert.True(staleHold.StatusCode == HttpStatusCode.Created, await staleHold.Content.ReadAsStringAsync());

        // Past the hold's 10-minute expiry, well inside the reschedule cut-off (4 h before originalStart).
        timeProvider.SetUtcNow(BaselineNow.AddMinutes(15));

        var reschedule = await customerClient.PostAsJsonAsync($"/api/customers/me/appointments/{appointmentId}/reschedule", new
        { startsOn = targetStart, performerId = context.VetStaffAccountId }, Json);
        Assert.True(reschedule.StatusCode == HttpStatusCode.OK, await reschedule.Content.ReadAsStringAsync());

        var body = await reschedule.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(targetStart, body.GetProperty("startsOn").GetDateTimeOffset());
    }

    /// <summary>C6 -- staff books into a slot a stale hold still occupies.</summary>
    [Fact]
    public async Task StaffBooking_IntoSlotWithExpiredHold_Succeeds()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var (adminClient, context, _) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var targetStart = BaselineNow.AddHours(3);

        var (holderClient, holderCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var holderCatId = await SeedCatAsync(holderCustomerId);
        var staleHold = await holderClient.PostAsJsonAsync("/api/customers/me/appointment-holds", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId = holderCatId, startsOn = targetStart, performerId = context.VetStaffAccountId }, Json);
        Assert.True(staleHold.StatusCode == HttpStatusCode.Created, await staleHold.Content.ReadAsStringAsync());

        timeProvider.SetUtcNow(BaselineNow.AddMinutes(15)); // past the 10-minute hold expiry

        var (_, bookedCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var bookedCatId = await SeedCatAsync(bookedCustomerId);

        var book = await adminClient.PostAsJsonAsync("/api/staff/appointments", new
        {
            customerId = bookedCustomerId, catId = bookedCatId, serviceId = context.ServiceId, startsOn = targetStart,
            performerId = context.VetStaffAccountId, roomId = context.RoomId,
        }, Json);
        Assert.True(book.StatusCode == HttpStatusCode.Created, await book.Content.ReadAsStringAsync());
    }

    /// <summary>C7 -- staff moves an appointment into a slot a stale hold still occupies.</summary>
    [Fact]
    public async Task StaffMove_IntoSlotWithExpiredHold_Succeeds()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var (adminClient, context, _) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var originalStart = BaselineNow.AddHours(2);
        var targetStart = BaselineNow.AddHours(4);

        var (_, movedCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var movedCatId = await SeedCatAsync(movedCustomerId);

        var book = await adminClient.PostAsJsonAsync("/api/staff/appointments", new
        {
            customerId = movedCustomerId, catId = movedCatId, serviceId = context.ServiceId, startsOn = originalStart,
            performerId = context.VetStaffAccountId, roomId = context.RoomId,
        }, Json);
        Assert.True(book.StatusCode == HttpStatusCode.Created, await book.Content.ReadAsStringAsync());
        var appointmentId = (await book.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("appointmentId").GetGuid();

        var (holderClient, holderCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var holderCatId = await SeedCatAsync(holderCustomerId);
        var staleHold = await holderClient.PostAsJsonAsync("/api/customers/me/appointment-holds", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId = holderCatId, startsOn = targetStart, performerId = context.VetStaffAccountId }, Json);
        Assert.True(staleHold.StatusCode == HttpStatusCode.Created, await staleHold.Content.ReadAsStringAsync());

        timeProvider.SetUtcNow(BaselineNow.AddMinutes(15)); // past the 10-minute hold expiry

        var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{appointmentId}", Json);
        var expectedVersion = detail.GetProperty("version").GetInt32();

        var move = await adminClient.PostAsJsonAsync($"/api/staff/appointments/{appointmentId}/move", new
        { expectedVersion, startsOn = targetStart, performerId = context.VetStaffAccountId, roomId = context.RoomId }, Json);
        Assert.True(move.StatusCode == HttpStatusCode.OK, await move.Content.ReadAsStringAsync());

        var body = await move.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal(targetStart, body.GetProperty("startsOn").GetDateTimeOffset());
    }

    /// <summary>C15-offer -- the freed slot the worker offers to the next waitlist entry is the very
    /// slot a stale hold still occupies (US-CLN-09 AC2's re-offer, I-G3). Dispatched directly, like
    /// the worker would, rather than through the (customer-facing) HTTP surface -- ADR-075's offer
    /// is worker-only.</summary>
    [Fact]
    public async Task WaitlistOffer_IntoSlotWithExpiredHold_Succeeds()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);
        await ActivateBranchForAdminAsync(admin.StaffAccountId, context.BranchId, adminClient);

        // Narrow the vet's shift to exactly one bookable slot that day, so the offer has only one
        // candidate to pick -- the slot the stale hold occupies.
        var days = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var slots = days.Select(d => new { dayOfWeek = d, starts = "15:00", ends = "15:30" }).ToArray();
        var setPattern = await adminClient.PutAsJsonAsync($"/api/admin/branches/{context.BranchId}/rosters/{context.VetStaffAccountId}/pattern", new { slots }, Json);
        Assert.True(setPattern.StatusCode == HttpStatusCode.NoContent, await setPattern.Content.ReadAsStringAsync());

        var slotStart = BaselineNow.AddHours(5); // 15:00 Asia/Dhaka
        var localDate = DateOnly.FromDateTime(slotStart.AddHours(6).DateTime); // Asia/Dhaka is UTC+6

        var (holderClient, holderCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var holderCatId = await SeedCatAsync(holderCustomerId);
        var staleHold = await holderClient.PostAsJsonAsync("/api/customers/me/appointment-holds", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId = holderCatId, startsOn = slotStart, performerId = context.VetStaffAccountId }, Json);
        Assert.True(staleHold.StatusCode == HttpStatusCode.Created, await staleHold.Content.ReadAsStringAsync());

        timeProvider.SetUtcNow(BaselineNow.AddMinutes(15)); // past the 10-minute hold expiry

        var (_, waitingCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var waitingCatId = await SeedCatAsync(waitingCustomerId);

        await using var scope = apiFactory.Services.CreateAsyncScope();
        var waitlistRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<WaitlistEntryAggregate>>();
        var systemContext = new UserContext(Guid.NewGuid(), Guid.NewGuid());

        var entry = new WaitlistEntryAggregate(Guid.NewGuid());
        entry.Join(new WaitlistJoinDto(
            context.BranchId, waitingCustomerId, waitingCatId, context.ServiceId, localDate, context.VetStaffAccountId,
            timeProvider.GetUtcNow(), systemContext, Guid.NewGuid()));
        await waitlistRepository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var offerAppointmentId = Guid.NewGuid();
        var response = await dispatcher.SendAsync(new OfferWaitlistSlotCommand
        {
            WaitlistEntryId = entry.Id, AppointmentId = offerAppointmentId,
            UserContext = systemContext, CorrelationId = Guid.NewGuid(), ValidateCommand = true,
        });

        Assert.True(response.IsSuccess, response.ValidationResult?.Errors.FirstOrDefault()?.ErrorMessage ?? "offer failed");

        var offeredEntry = await waitlistRepository.GetByIdAsync(entry.Id);
        Assert.NotNull(offeredEntry);
        Assert.Equal("Offered", offeredEntry!.Status.DisplayName);
        Assert.Equal(offerAppointmentId, offeredEntry.OfferedAppointmentId);

        var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/staff/appointments/{offerAppointmentId}", Json);
        Assert.Equal("Held", detail.GetProperty("status").GetString());
        Assert.Equal(slotStart, detail.GetProperty("startsOn")!.GetDateTimeOffset());
    }

    private static async Task<HttpClient> SignInStaffOnAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> apiFactory, string mobile, string password)
    {
        var client = apiFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = mobile, password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        return client;
    }
}
