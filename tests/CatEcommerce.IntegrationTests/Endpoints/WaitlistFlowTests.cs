using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Clinic;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Events.Clinic;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-APT-001 §16 I-G1..I-G4 -- CODE_REVIEW gate fix (2026-09-28, HIGH): the waitlist customer HTTP
/// surface (<c>CustomerWaitlistController</c>) and the <c>ClinicWorkCycle</c> waitlist pass had zero
/// integration coverage. Every test here drives the real endpoints and the real
/// <c>ClinicWorkCycle.RunAsync</c> (via <c>RunClinicWorkCycleAsync</c>) -- never
/// <c>OfferWaitlistSlotCommand</c> dispatched directly.
/// </summary>
public sealed class WaitlistFlowTests(AppointmentsApiFactory factory) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly DateTimeOffset BaselineNow = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); // 2026-10-12 10:00 Asia/Dhaka (a Monday)

    /// <summary>"joining, and listing your own entries" (§16's own framing for the join path).</summary>
    [Fact]
    public async Task Join_ThenListOwnEntries_ReturnsTheNewEntryWaiting()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);
        await NarrowVetToOneSlotADayAsync(adminClient, context.BranchId, context.VetStaffAccountId, "18:00", "18:30");

        var slotStart = new DateTimeOffset(BaselineNow.Year, BaselineNow.Month, BaselineNow.Day, 12, 0, 0, TimeSpan.Zero); // 18:00 Dhaka
        var localDate = DateOnly.FromDateTime(slotStart.AddHours(6).DateTime); // Asia/Dhaka is UTC+6

        var (fillerClient, fillerCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var fillerCatId = await SeedCatAsync(fillerCustomerId);
        await BookConfirmedAppointmentAsync(fillerClient, context, fillerCatId, slotStart);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);

        var join = await customerClient.PostAsJsonAsync("/api/customers/me/waitlist", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId, localDate }, Json);
        Assert.True(join.StatusCode == HttpStatusCode.Created, await join.Content.ReadAsStringAsync());
        var joinBody = await join.Content.ReadFromJsonAsync<JsonElement>(Json);
        var waitlistEntryId = joinBody.GetProperty("waitlistEntryId").GetGuid();
        Assert.True(joinBody.GetProperty("claimWindowMinutes").GetInt32() > 0);

        var list = await customerClient.GetFromJsonAsync<JsonElement>("/api/customers/me/waitlist", Json);
        var entry = Assert.Single(list.EnumerateArray(), e => e.GetProperty("id").GetGuid() == waitlistEntryId);
        Assert.Equal("Waiting", entry.GetProperty("status").GetString());
        Assert.Equal(catId, entry.GetProperty("catId").GetGuid());
        Assert.Equal(localDate, DateOnly.Parse(entry.GetProperty("localDate").GetString()!));
    }

    /// <summary>I-G1 -- "fill a day; join; another customer cancels; run the clinic pass" -> the
    /// (only, so trivially oldest) waiting entry is Offered, with a hold carrying the claim window,
    /// and <c>waitlist.slot-offered</c> enqueued (US-CLN-09 AC1).</summary>
    [Fact]
    public async Task IG1_FilledDay_JoinThenOwnerCancels_RunPass_OffersTheWaitingEntry()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var setup = await SetUpFilledDayThenJoinThenCancelAsync(apiFactory);

        // Outside quiet hours (10:00 Dhaka): the pass must actually offer.
        await RunClinicWorkCycleAsync(apiFactory);

        var list = await setup.WaitingClient.GetFromJsonAsync<JsonElement>("/api/customers/me/waitlist", Json);
        var entry = Assert.Single(list.EnumerateArray(), e => e.GetProperty("id").GetGuid() == setup.WaitlistEntryId);
        Assert.Equal("Offered", entry.GetProperty("status").GetString());
        var offeredAppointmentId = entry.GetProperty("offeredAppointmentId").GetGuid();
        Assert.NotEqual(setup.FillerAppointmentId, offeredAppointmentId);

        var offerExpiresOn = entry.GetProperty("offerExpiresOn").GetDateTimeOffset();
        Assert.True(offerExpiresOn > BaselineNow, "The offer's claim window must extend into the future.");

        var detail = await setup.WaitingClient.GetFromJsonAsync<JsonElement>($"/api/customers/me/appointments/{offeredAppointmentId}", Json);
        Assert.Equal("Held", detail.GetProperty("status").GetString());
        Assert.True(detail.GetProperty("isWaitlistOffer").GetBoolean());
        Assert.Equal(setup.SlotStart, detail.GetProperty("startsOn").GetDateTimeOffset());

        await using var scope = apiFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var offerMessageEnqueued = await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .AnyAsync(r => r.TemplateKey == NotificationTemplateCatalogue.WaitlistSlotOffered && r.RecipientId == setup.WaitingCustomerId);
        Assert.True(offerMessageEnqueued, "waitlist.slot-offered was not enqueued for the offered customer.");
    }

    /// <summary>Backlog row 4 -- the offer saves two roots in one save (ADR-075); the waitlist
    /// entry's own <c>WaitlistSlotOfferedEvent</c> must still be dispatched, not just the held
    /// appointment's events.</summary>
    [Fact]
    public async Task Offer_DispatchesTheWaitlistEntrysSlotOfferedEvent()
    {
        var spy = new SlotOfferedSpy();
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IEventHandlerAsync<WaitlistSlotOfferedEvent>>(spy)));

        var setup = await SetUpFilledDayThenJoinThenCancelAsync(apiFactory);
        await RunClinicWorkCycleAsync(apiFactory);

        var published = Assert.Single(spy.Received, e => e.WaitlistEntryId == setup.WaitlistEntryId);
        Assert.NotEqual(Guid.Empty, published.AppointmentId);
    }

    private sealed class SlotOfferedSpy : IEventHandlerAsync<WaitlistSlotOfferedEvent>
    {
        public System.Collections.Concurrent.ConcurrentBag<WaitlistSlotOfferedEvent> Received { get; } = [];

        public Task HandleAsync(WaitlistSlotOfferedEvent @event)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    /// <summary>I-G2 -- "offered customer confirms" -> Booked, entry Claimed.</summary>
    [Fact]
    public async Task IG2_OfferedCustomer_Confirms_BooksTheAppointment_AndClaimsTheEntry()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var setup = await SetUpFilledDayThenJoinThenCancelAsync(apiFactory);
        await RunClinicWorkCycleAsync(apiFactory);

        var listBefore = await setup.WaitingClient.GetFromJsonAsync<JsonElement>("/api/customers/me/waitlist", Json);
        var offeredAppointmentId = Assert.Single(listBefore.EnumerateArray(), e => e.GetProperty("id").GetGuid() == setup.WaitlistEntryId)
            .GetProperty("offeredAppointmentId").GetGuid();

        var confirm = await setup.WaitingClient.PostAsJsonAsync($"/api/customers/me/appointments/{offeredAppointmentId}/confirm", new { }, Json);
        Assert.True(confirm.StatusCode == HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
        var confirmBody = await confirm.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Booked", confirmBody.GetProperty("status").GetString());

        var listAfter = await setup.WaitingClient.GetFromJsonAsync<JsonElement>("/api/customers/me/waitlist", Json);
        var entry = Assert.Single(listAfter.EnumerateArray(), e => e.GetProperty("id").GetGuid() == setup.WaitlistEntryId);
        Assert.Equal("Claimed", entry.GetProperty("status").GetString());
    }

    /// <summary>I-G3 -- "offer not claimed; advance 30 min; run the pass twice" -> the first entry
    /// Lapsed, the next entry Offered the same slot (US-CLN-09 AC2).</summary>
    [Fact]
    public async Task IG3_UnclaimedOffer_Lapses_AndTheNextEntryIsOfferedTheSameSlot()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);
        await NarrowVetToOneSlotADayAsync(adminClient, context.BranchId, context.VetStaffAccountId, "18:00", "18:30");

        var slotStart = new DateTimeOffset(BaselineNow.Year, BaselineNow.Month, BaselineNow.Day, 12, 0, 0, TimeSpan.Zero);
        var localDate = DateOnly.FromDateTime(slotStart.AddHours(6).DateTime);

        var (fillerClient, fillerCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var fillerCatId = await SeedCatAsync(fillerCustomerId);
        var fillerAppointmentId = await BookConfirmedAppointmentAsync(fillerClient, context, fillerCatId, slotStart);

        var (firstClient, firstCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var firstCatId = await SeedCatAsync(firstCustomerId);
        var firstJoin = await firstClient.PostAsJsonAsync("/api/customers/me/waitlist", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId = firstCatId, localDate }, Json);
        Assert.True(firstJoin.StatusCode == HttpStatusCode.Created, await firstJoin.Content.ReadAsStringAsync());
        var firstJoinBody = await firstJoin.Content.ReadFromJsonAsync<JsonElement>(Json);
        var firstEntryId = firstJoinBody.GetProperty("waitlistEntryId").GetGuid();
        var claimWindowMinutes = firstJoinBody.GetProperty("claimWindowMinutes").GetInt32();

        // A distinct CreatedOn so queue order (oldest first) is unambiguous.
        timeProvider.SetUtcNow(BaselineNow.AddMinutes(1));

        var (secondClient, secondCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var secondCatId = await SeedCatAsync(secondCustomerId);
        var secondJoin = await secondClient.PostAsJsonAsync("/api/customers/me/waitlist", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId = secondCatId, localDate }, Json);
        Assert.True(secondJoin.StatusCode == HttpStatusCode.Created, await secondJoin.Content.ReadAsStringAsync());
        var secondEntryId = (await secondJoin.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("waitlistEntryId").GetGuid();

        var cancel = await fillerClient.PostAsJsonAsync($"/api/customers/me/appointments/{fillerAppointmentId}/cancel", new { }, Json);
        Assert.True(cancel.StatusCode == HttpStatusCode.NoContent, await cancel.Content.ReadAsStringAsync());

        await RunClinicWorkCycleAsync(apiFactory); // offers to the oldest entry (first)

        var firstListAfterOffer = await firstClient.GetFromJsonAsync<JsonElement>("/api/customers/me/waitlist", Json);
        var firstEntryAfterOffer = Assert.Single(firstListAfterOffer.EnumerateArray(), e => e.GetProperty("id").GetGuid() == firstEntryId);
        Assert.Equal("Offered", firstEntryAfterOffer.GetProperty("status").GetString());

        // Past the claim window, unclaimed.
        timeProvider.SetUtcNow(BaselineNow.AddMinutes(1).AddMinutes(claimWindowMinutes + 1));

        // §16 I-G3: run the pass twice.
        await RunClinicWorkCycleAsync(apiFactory);
        await RunClinicWorkCycleAsync(apiFactory);

        var firstListAfterLapse = await firstClient.GetFromJsonAsync<JsonElement>("/api/customers/me/waitlist", Json);
        var firstEntryAfterLapse = Assert.Single(firstListAfterLapse.EnumerateArray(), e => e.GetProperty("id").GetGuid() == firstEntryId);
        Assert.Equal("Lapsed", firstEntryAfterLapse.GetProperty("status").GetString());

        var secondListAfterOffer = await secondClient.GetFromJsonAsync<JsonElement>("/api/customers/me/waitlist", Json);
        var secondEntryAfterOffer = Assert.Single(secondListAfterOffer.EnumerateArray(), e => e.GetProperty("id").GetGuid() == secondEntryId);
        Assert.Equal("Offered", secondEntryAfterOffer.GetProperty("status").GetString());
        var reOfferedAppointmentId = secondEntryAfterOffer.GetProperty("offeredAppointmentId").GetGuid();

        var reOfferedDetail = await secondClient.GetFromJsonAsync<JsonElement>($"/api/customers/me/appointments/{reOfferedAppointmentId}", Json);
        Assert.Equal("Held", reOfferedDetail.GetProperty("status").GetString());
        Assert.Equal(slotStart, reOfferedDetail.GetProperty("startsOn").GetDateTimeOffset());
    }

    /// <summary>I-G4 -- "cancellation at 23:00" -> no offer until the branch's quiet hours end
    /// (default 21:00-08:00 branch-local, §8.11).</summary>
    [Fact]
    public async Task IG4_CancellationDuringQuietHours_NoOfferUntilQuietHoursEnd()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);
        await NarrowVetToOneSlotADayAsync(adminClient, context.BranchId, context.VetStaffAccountId, "18:00", "18:30");

        // The next day at 18:00 Dhaka (12:00Z) -- far enough out that cancelling at 23:00 Monday
        // Dhaka is still inside the (default 4 h) change cut-off.
        var slotStart = new DateTimeOffset(BaselineNow.Year, BaselineNow.Month, BaselineNow.Day, 12, 0, 0, TimeSpan.Zero).AddDays(1);
        var localDate = DateOnly.FromDateTime(slotStart.AddHours(6).DateTime);

        var (fillerClient, fillerCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var fillerCatId = await SeedCatAsync(fillerCustomerId);
        var fillerAppointmentId = await BookConfirmedAppointmentAsync(fillerClient, context, fillerCatId, slotStart);

        var (waitingClient, waitingCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var waitingCatId = await SeedCatAsync(waitingCustomerId);
        var join = await waitingClient.PostAsJsonAsync("/api/customers/me/waitlist", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId = waitingCatId, localDate }, Json);
        Assert.True(join.StatusCode == HttpStatusCode.Created, await join.Content.ReadAsStringAsync());
        var waitlistEntryId = (await join.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("waitlistEntryId").GetGuid();

        // 23:00 Monday Asia/Dhaka = 17:00Z Monday -- inside the default 21:00-08:00 quiet hours.
        var quietInstant = new DateTimeOffset(BaselineNow.Year, BaselineNow.Month, BaselineNow.Day, 17, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(quietInstant);

        var cancel = await fillerClient.PostAsJsonAsync($"/api/customers/me/appointments/{fillerAppointmentId}/cancel", new { }, Json);
        Assert.True(cancel.StatusCode == HttpStatusCode.NoContent, await cancel.Content.ReadAsStringAsync());

        await RunClinicWorkCycleAsync(apiFactory);

        var duringQuietHours = await waitingClient.GetFromJsonAsync<JsonElement>("/api/customers/me/waitlist", Json);
        var entryDuringQuietHours = Assert.Single(duringQuietHours.EnumerateArray(), e => e.GetProperty("id").GetGuid() == waitlistEntryId);
        Assert.Equal("Waiting", entryDuringQuietHours.GetProperty("status").GetString());

        // 08:05 Tuesday Asia/Dhaka = 02:05Z Tuesday -- just past quiet hours' end.
        timeProvider.SetUtcNow(new DateTimeOffset(BaselineNow.Year, BaselineNow.Month, BaselineNow.Day, 2, 5, 0, TimeSpan.Zero).AddDays(1));

        await RunClinicWorkCycleAsync(apiFactory);

        var afterQuietHours = await waitingClient.GetFromJsonAsync<JsonElement>("/api/customers/me/waitlist", Json);
        var entryAfterQuietHours = Assert.Single(afterQuietHours.EnumerateArray(), e => e.GetProperty("id").GetGuid() == waitlistEntryId);
        Assert.Equal("Offered", entryAfterQuietHours.GetProperty("status").GetString());
    }

    /// <summary>Withdraw: the entry stops competing for an offer but stays visible to the customer
    /// (§12.4's list has no status filter) as Withdrawn.</summary>
    [Fact]
    public async Task Withdraw_MarksTheEntryWithdrawn()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);
        await NarrowVetToOneSlotADayAsync(adminClient, context.BranchId, context.VetStaffAccountId, "18:00", "18:30");

        var slotStart = new DateTimeOffset(BaselineNow.Year, BaselineNow.Month, BaselineNow.Day, 12, 0, 0, TimeSpan.Zero);
        var localDate = DateOnly.FromDateTime(slotStart.AddHours(6).DateTime);

        var (fillerClient, fillerCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var fillerCatId = await SeedCatAsync(fillerCustomerId);
        await BookConfirmedAppointmentAsync(fillerClient, context, fillerCatId, slotStart);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        var join = await customerClient.PostAsJsonAsync("/api/customers/me/waitlist", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId, localDate }, Json);
        Assert.True(join.StatusCode == HttpStatusCode.Created, await join.Content.ReadAsStringAsync());
        var waitlistEntryId = (await join.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("waitlistEntryId").GetGuid();

        var withdraw = await customerClient.DeleteAsync($"/api/customers/me/waitlist/{waitlistEntryId}");
        Assert.Equal(HttpStatusCode.NoContent, withdraw.StatusCode);

        var list = await customerClient.GetFromJsonAsync<JsonElement>("/api/customers/me/waitlist", Json);
        var entry = Assert.Single(list.EnumerateArray(), e => e.GetProperty("id").GetGuid() == waitlistEntryId);
        Assert.Equal("Withdrawn", entry.GetProperty("status").GetString());
    }

    /// <summary>CODE_REVIEW gate fix (2026-09-28, HIGH) regression: "the day passed" (§8.11's Close
    /// rule) is the entry's own <b>branch-local</b> day, not one global UTC date. A branch in
    /// <c>Pacific/Honolulu</c> (UTC-10): at this instant the branch's own local time is
    /// 2026-10-04T23:30 -- 30 minutes still left in the entry's own local day (LocalDate
    /// 2026-10-04) -- so §8.11's own rule says it has <b>not</b> passed yet. Proven against the old
    /// logic during development (not left in the suite): with
    /// GetPastWaitlistEntriesQueryHandler's filter reverted to `w.LocalDate &lt;
    /// DateOnly.FromDateTime(query.Now.UtcDateTime)` and ClinicWorkCycle passing that precomputed
    /// UTC date, this same test failed -- the entry was closed, matching the gate's own reproduction
    /// (a UTC-10 branch closes up to 10 h before its local day ends).</summary>
    [Fact]
    public async Task Close_NonUtcBranchNearLocalMidnight_EntryNotYetPast_StaysWaiting()
    {
        // 23:30 Pacific/Honolulu (UTC-10) is 09:30Z the next UTC day. Anchored to the 09:30Z nearest the
        // real clock, not a fixed date: sessions minted under the fake clock are still checked against
        // real time, so a fixed date fails with session_invalid once it is a day old.
        var utcNow = DateTimeOffset.UtcNow;
        var honoluluNow = new DateTimeOffset(utcNow.UtcDateTime.Date.AddHours(9.5), TimeSpan.Zero);
        if (honoluluNow - utcNow > TimeSpan.FromHours(12)) honoluluNow = honoluluNow.AddDays(-1);
        else if (utcNow - honoluluNow > TimeSpan.FromHours(12)) honoluluNow = honoluluNow.AddDays(1);
        var honoluluLocalDate = DateOnly.FromDateTime(honoluluNow.UtcDateTime.AddHours(-10));
        var (webFactory, _) = Factory.WithFakeTimeProvider(honoluluNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);

        var code = NewCode();
        var createBranch = await adminClient.PostAsJsonAsync("/api/admin/branches", new { code, name = "Honolulu " + code, timeZoneId = "Pacific/Honolulu" }, Json);
        Assert.True(createBranch.StatusCode == HttpStatusCode.Created, await createBranch.Content.ReadAsStringAsync());
        var branchId = (await createBranch.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();

        var (_, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);

        await using (var scope = apiFactory.Services.CreateAsyncScope())
        {
            var waitlistRepository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<WaitlistEntryAggregate>>();
            var entry = new WaitlistEntryAggregate(Guid.NewGuid());
            entry.Join(new WaitlistJoinDto(
                branchId, customerId, catId, Guid.NewGuid(), honoluluLocalDate, null,
                honoluluNow, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await waitlistRepository.SaveAsync(entry, EventPublishOption.InMemoryPublishOnly);
        }

        await RunClinicWorkCycleAsync(apiFactory);

        await using var verifyScope = apiFactory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var status = await db.Set<WaitlistEntryAggregate>().AsNoTracking()
            .Where(w => w.BranchId == branchId).Select(w => w.Status).SingleAsync();
        Assert.Equal(CatEcommerce.Shared.Common.Enums.WaitlistStatus.Waiting, status);
    }

    private sealed record FilledDaySetup(
        ClinicTestContext Context, DateTimeOffset SlotStart, DateOnly LocalDate, Guid FillerAppointmentId,
        Guid WaitlistEntryId, HttpClient WaitingClient, Guid WaitingCustomerId, Guid WaitingCatId);

    /// <summary>Common I-G1/I-G2 setup: fill the day's one slot, a second customer joins the
    /// waitlist, the filling customer cancels (freeing the slot) -- everything up to, but not
    /// including, running the pass.</summary>
    private async Task<FilledDaySetup> SetUpFilledDayThenJoinThenCancelAsync(WebApplicationFactory<Program> apiFactory)
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);
        await NarrowVetToOneSlotADayAsync(adminClient, context.BranchId, context.VetStaffAccountId, "18:00", "18:30");

        var slotStart = new DateTimeOffset(BaselineNow.Year, BaselineNow.Month, BaselineNow.Day, 12, 0, 0, TimeSpan.Zero);
        var localDate = DateOnly.FromDateTime(slotStart.AddHours(6).DateTime);

        var (fillerClient, fillerCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var fillerCatId = await SeedCatAsync(fillerCustomerId);
        var fillerAppointmentId = await BookConfirmedAppointmentAsync(fillerClient, context, fillerCatId, slotStart);

        var (waitingClient, waitingCustomerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var waitingCatId = await SeedCatAsync(waitingCustomerId);
        var join = await waitingClient.PostAsJsonAsync("/api/customers/me/waitlist", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId = waitingCatId, localDate }, Json);
        Assert.True(join.StatusCode == HttpStatusCode.Created, await join.Content.ReadAsStringAsync());
        var waitlistEntryId = (await join.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("waitlistEntryId").GetGuid();

        var cancel = await fillerClient.PostAsJsonAsync($"/api/customers/me/appointments/{fillerAppointmentId}/cancel", new { }, Json);
        Assert.True(cancel.StatusCode == HttpStatusCode.NoContent, await cancel.Content.ReadAsStringAsync());

        return new FilledDaySetup(context, slotStart, localDate, fillerAppointmentId, waitlistEntryId, waitingClient, waitingCustomerId, waitingCatId);
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
