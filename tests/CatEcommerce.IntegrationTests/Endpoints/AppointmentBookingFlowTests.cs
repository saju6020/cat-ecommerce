using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-APT-001 §16 -- I-A1, I-C1..I-C7, I-D1/I-D2, I-D7.
/// ACCEPTANCE gate fix (2026-09-28, A12/A13): <c>[Collection("Notifications")]</c> -- these tests now
/// call <see cref="AppointmentsIntegrationTestBase.RunNotificationDeliveryPassAsync"/>, which scans
/// the GLOBAL, un-scoped <c>NotificationRequests</c>/<c>Notifications</c> tables (§16.1's shared,
/// never-truncated database); without this tag, xUnit could run this class in parallel with any
/// other class that also drives a delivery/reminder pass (every HO-NTF-001 test class already carries
/// this same tag for exactly this reason) -- a concurrent pass using a DIFFERENT fake "now" could
/// expand/defer/delay THIS test's own rows before its own pass call gets to them. Proven during
/// development: ID5 passed in isolation but failed intermittently once run alongside
/// AppointmentMoveConflictTests/AppointmentFlaggingTests until this tag was added.</summary>
[Collection("Notifications")]
public sealed class AppointmentBookingFlowTests(AppointmentsApiFactory factory) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly DateTimeOffset BaselineNow = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); // 2026-10-12 10:00 Asia/Dhaka (a Monday)

    /// <summary>I-A1 -- btree_gist installed; the three exclusion constraints present.</summary>
    [Fact]
    public async Task Migration_InstallsBtreeGistAndExclusionConstraints()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using (var extCmd = connection.CreateCommand())
        {
            extCmd.CommandText = "SELECT extname FROM pg_extension WHERE extname = 'btree_gist';";
            await using var reader = await extCmd.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync(), "btree_gist extension is not installed.");
        }

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT conname FROM pg_constraint WHERE contype = 'x' AND conname LIKE 'ex_appointment%';";
        await using var constraintReader = await cmd.ExecuteReaderAsync();
        var names = new List<string>();
        while (await constraintReader.ReadAsync()) names.Add(constraintReader.GetString(0));

        Assert.Contains("ex_appointment_performer", names);
        Assert.Contains("ex_appointment_room", names);
        Assert.Contains("ex_appointment_cat", names);
    }

    /// <summary>I-C6 -- ACCEPTANCE gate fix (2026-09-28, A12/A22): extended to also assert the
    /// confirmation half of US-CLN-06 AC1 -- a <c>appointment.confirmed</c> request is committed for
    /// the customer at confirm time, and after the HO-NTF-001 delivery pass an in-app copy exists.</summary>
    [Fact]
    public async Task PlaceHold_ThenConfirm_Booked_PriceSnapshotMatchesCurrentPrice()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);

        var startsOn = BaselineNow.AddHours(2); // 12:00 Dhaka, well past the 60-min notice
        var hold = await customerClient.PostAsJsonAsync("/api/customers/me/appointment-holds", new
        {
            branchId = context.BranchId, serviceId = context.ServiceId, catId, startsOn, performerId = context.VetStaffAccountId
        }, Json);
        Assert.True(hold.StatusCode == HttpStatusCode.Created, await hold.Content.ReadAsStringAsync());
        var holdBody = await hold.Content.ReadFromJsonAsync<JsonElement>(Json);
        var appointmentId = holdBody.GetProperty("appointmentId").GetGuid();

        var confirm = await customerClient.PostAsJsonAsync($"/api/customers/me/appointments/{appointmentId}/confirm", new { reasonForVisit = "Annual check" }, Json);
        Assert.True(confirm.StatusCode == HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());

        var detail = await customerClient.GetFromJsonAsync<JsonElement>($"/api/customers/me/appointments/{appointmentId}", Json);
        Assert.Equal("Booked", detail.GetProperty("status").GetString());
        Assert.Equal(500.00m, detail.GetProperty("service").GetProperty("priceAmount").GetDecimal());

        await using (var scope = apiFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var confirmationRequested = await db.Set<NotificationRequestAggregate>().AsNoTracking()
                .AnyAsync(r => r.TemplateKey == NotificationTemplateCatalogue.AppointmentConfirmed && r.RecipientId == customerId);
            Assert.True(confirmationRequested, "appointment.confirmed was not committed for the customer at confirm time.");
        }

        // ACCEPTANCE gate fix (2026-09-29, HIGH): a single pass is not reliable evidence on the
        // shared, never-truncated test database -- other classes' own leftover Pending confirmations
        // can crowd this one out of the delivery pass's own batch cap. Loop until this customer's own
        // in-app copy exists (or fail loudly), never past that -- see RunNotificationDeliveryUntilAsync.
        await RunNotificationDeliveryUntilAsync(apiFactory, db => db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.RecipientId == customerId && n.TemplateKey == NotificationTemplateCatalogue.AppointmentConfirmed && n.Channel == NotificationChannel.InApp));

        var from = BaselineNow.AddMinutes(-5);
        var to = BaselineNow.AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var inAppCopy = log.GetProperty("items").EnumerateArray().Any(i =>
            i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.AppointmentConfirmed && i.GetProperty("channel").GetString() == "InApp");
        Assert.True(inAppCopy, "The in-app copy of appointment.confirmed does not exist after the delivery pass.");
    }

    /// <summary>I-C6 -- staff booking (C6, <c>BookAppointmentByStaffCommandHandler</c>) also enqueues
    /// the confirmation, exactly like the customer confirm path.</summary>
    [Fact]
    public async Task StaffBooksAppointment_EnqueuesConfirmedNotification()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var (adminClient, context, _) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        customerClient.Dispose();

        var startsOn = BaselineNow.AddHours(2);
        var book = await adminClient.PostAsJsonAsync("/api/staff/appointments", new
        {
            customerId, catId, serviceId = context.ServiceId, startsOn, performerId = context.VetStaffAccountId,
            roomId = context.RoomId, reasonForVisit = "Walk-in check"
        }, Json);
        Assert.True(book.StatusCode == HttpStatusCode.Created, await book.Content.ReadAsStringAsync());

        await using var scope = apiFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var confirmationRequested = await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .AnyAsync(r => r.TemplateKey == NotificationTemplateCatalogue.AppointmentConfirmed && r.RecipientId == customerId);
        Assert.True(confirmationRequested, "appointment.confirmed was not committed for the customer when staff booked directly (BookAppointmentByStaffCommand skips the separate confirm step -- §11.4 C1).");
    }

    /// <summary>I-D7 -- a staff booking made at 22:30 Asia/Dhaka (inside the default 21:00-08:00
    /// quiet hours) still gets its confirmation delivered at once, because
    /// <c>appointment.confirmed</c> is <c>NotificationUrgency.Urgent</c> and bypasses quiet hours
    /// (<c>DeliverNotificationCommandHandler</c>'s own check is scoped to <c>Normal</c>) -- while its
    /// reminders (<c>NotificationUrgency.Normal</c>) are held (deferred) until quiet hours end.</summary>
    [Fact]
    public async Task ID7_StaffBooksDuringQuietHours_ConfirmationDeliveredAtOnce_RemindersHeldUntilQuietHoursEnd()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var (adminClient, context, admin) = await SeedBranchWithAdminAssignedAsync(apiFactory, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);
        customerClient.Dispose();

        // 22:30 Monday Asia/Dhaka = 16:30Z Monday -- inside the default 21:00-08:00 quiet hours.
        var quietInstant = new DateTimeOffset(BaselineNow.Year, BaselineNow.Month, BaselineNow.Day, 16, 30, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(quietInstant);
        await ReauthenticateAsync(adminClient, admin.Mobile, admin.Password);

        // startsOn = 2026-10-13 09:00 Dhaka (2026-10-13T03:00Z) -- picked so the 2h-before
        // reminder's own SendAfter (2026-10-13T01:00Z = 07:00 Dhaka) itself falls inside quiet
        // hours (the deferral under test is the QUIET-HOURS one, not merely SendAfter not yet due),
        // while quiet hours' own end (08:00 Dhaka = 02:00Z) still lands BEFORE the appointment's own
        // start (so the reminder's NotAfter has not yet passed once quiet hours end and it becomes
        // due -- a later start would make NotAfter pass first, and the reminder would Expire instead
        // of ever being delivered, which is a different, uninteresting outcome from what I-D7 tests).
        var startsOn = new DateTimeOffset(2026, 10, 13, 3, 0, 0, TimeSpan.Zero);
        var book = await adminClient.PostAsJsonAsync("/api/staff/appointments", new
        {
            customerId, catId, serviceId = context.ServiceId, startsOn, performerId = context.VetStaffAccountId,
            roomId = context.RoomId, reasonForVisit = "Quiet-hours booking"
        }, Json);
        Assert.True(book.StatusCode == HttpStatusCode.Created, await book.Content.ReadAsStringAsync());

        // Confirmation: Urgent, delivered at once, even though it's 22:30 Dhaka. ACCEPTANCE gate fix
        // (2026-09-29, HIGH): loop until THIS customer's own confirmation reaches Sent, rather than
        // assuming one pass reaches it on a database other classes may have left a backlog in.
        await RunNotificationDeliveryUntilAsync(apiFactory, db => db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.RecipientId == customerId && n.TemplateKey == NotificationTemplateCatalogue.AppointmentConfirmed &&
                n.Channel != NotificationChannel.InApp && n.Status == NotificationStatus.Sent));

        var confirmedFrom = quietInstant.AddMinutes(-5);
        var confirmedTo = quietInstant.AddMinutes(5);
        var confirmedLog = await adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(confirmedFrom.ToString("O"))}&to={Uri.EscapeDataString(confirmedTo.ToString("O"))}", Json);
        var confirmedItem = confirmedLog.GetProperty("items").EnumerateArray().First(i =>
            i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.AppointmentConfirmed && i.GetProperty("channel").GetString() != "InApp");
        Assert.Equal("Sent", confirmedItem.GetProperty("status").GetString());

        // Advance to just after the 2h reminder's own SendAfter (2026-10-13T01:00Z = 07:00 Dhaka) --
        // still inside quiet hours -- and expand it: it must be Queued/deferred, never Sent. Looping
        // more passes at this SAME "now" cannot deliver it early -- NotificationWorkQueue's own
        // NotBefore filter means it stays Queued regardless of how many extra passes run (see
        // RunNotificationDeliveryUntilAsync's own remark) -- so it is safe to loop only until the row
        // has been (re-)expanded, then assert the held state separately, never looping further.
        var reminderSendAfter = startsOn.AddHours(-2);
        timeProvider.SetUtcNow(reminderSendAfter.AddMinutes(5));
        await RunNotificationDeliveryUntilAsync(apiFactory, db => db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.RecipientId == customerId && n.TemplateKey == NotificationTemplateCatalogue.AppointmentReminder && n.Channel != NotificationChannel.InApp));
        await ReauthenticateAsync(adminClient, admin.Mobile, admin.Password); // the jump exceeds the 30-min staff idle timeout

        var reminderFrom = reminderSendAfter.AddMinutes(-5);
        var reminderTo = reminderSendAfter.AddHours(1);
        var duringQuietHoursLog = await adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(reminderFrom.ToString("O"))}&to={Uri.EscapeDataString(reminderTo.ToString("O"))}", Json);
        var reminderItem = duringQuietHoursLog.GetProperty("items").EnumerateArray().First(i =>
            i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.AppointmentReminder && i.GetProperty("channel").GetString() != "InApp");
        Assert.Equal("Queued", reminderItem.GetProperty("status").GetString());
        Assert.True(reminderItem.TryGetProperty("deferredUntil", out var deferredUntil) && deferredUntil.ValueKind != JsonValueKind.Null,
            "The reminder must carry a deferredUntil while held for quiet hours.");

        // Past 08:00 Dhaka (02:00Z) -- the deferred reminder is now due, and still comfortably
        // before the appointment's own 03:00Z start (its request's own NotAfter).
        var reminderNotificationId = reminderItem.GetProperty("id").GetGuid();
        timeProvider.SetUtcNow(new DateTimeOffset(2026, 10, 13, 2, 5, 0, TimeSpan.Zero));
        await RunNotificationDeliveryUntilAsync(apiFactory, db => db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.Id == reminderNotificationId && n.Status == NotificationStatus.Sent));
        await ReauthenticateAsync(adminClient, admin.Mobile, admin.Password);

        var afterQuietHours = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{reminderNotificationId}", Json);
        Assert.Equal("Sent", afterQuietHours.GetProperty("status").GetString());
    }

    /// <summary>I-C2 -- exactly one of many concurrent holds for the same slot succeeds.</summary>
    [Fact]
    public async Task PlaceHold_TwentyConcurrentRequestsForSameSlot_ExactlyOneSucceeds()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);

        var startsOn = BaselineNow.AddHours(3);
        const int attempts = 20;

        var clientsAndCats = new List<(HttpClient Client, Guid CatId)>();
        for (var i = 0; i < attempts; i++)
        {
            var (client, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
            var catId = await SeedCatAsync(customerId);
            clientsAndCats.Add((client, catId));
        }

        var tasks = clientsAndCats.Select(cc => cc.Client.PostAsJsonAsync("/api/customers/me/appointment-holds", new
        {
            branchId = context.BranchId, serviceId = context.ServiceId, catId = cc.CatId, startsOn, performerId = context.VetStaffAccountId
        }, Json)).ToArray();

        var responses = await Task.WhenAll(tasks);
        foreach (var (client, _) in clientsAndCats) client.Dispose();

        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflictResponses = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        var others = responses.Where(r => r.StatusCode != HttpStatusCode.Created && r.StatusCode != HttpStatusCode.Conflict).ToList();
        if (others.Count > 0)
        {
            var bodies = await Task.WhenAll(others.Select(async r => $"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}"));
            Assert.Fail("Unexpected status codes: " + string.Join(" | ", bodies));
        }

        Assert.Equal(1, created);
        Assert.Equal(attempts - 1, conflictResponses.Count);

        // ARCHITECTURE gate second pass, HIGH B: every loser -- whether it lost to a clean 23P01
        // exclusion violation or to a 40P01 deadlock on the same constraint under this much
        // concurrency -- must carry the SlotNoLongerAvailable code (ADR-068, §12.4), not just a
        // 409 status a generic "someone else changed this" body would also satisfy.
        var conflictBodies = await Task.WhenAll(conflictResponses.Select(r => r.Content.ReadAsStringAsync()));
        foreach (var body in conflictBodies)
            Assert.Contains(BusinessViolationCodes.SlotNoLongerAvailable, body);
    }

    /// <summary>SECURITY gate fix (2026-09-28, HIGH) regression -- "one hold per customer" (§8.6
    /// step 3, ADR-070) must hold under concurrency, not just sequentially. One customer, one cat,
    /// N concurrent holds for N different, non-overlapping slots: every request is serialised per
    /// customer by <c>ICustomerHoldSerializer</c>'s advisory lock, so each in turn sees the previous
    /// one's committed hold and releases it before placing its own (§8.6 step 3's "replaced" path) --
    /// no unlocked-read race lets two land as live holds together. No response is ever a 500.</summary>
    [Fact]
    public async Task PlaceHold_EightConcurrentRequestsFromOneCustomerForDifferentSlots_AtMostOneHeldRowRemains()
    {
        var (webFactory, _) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);

        const int attempts = 8;
        // One hour apart, well clear of the 30-minute service duration -- no two of these slots
        // overlap, so nothing here can hit the exclusion constraints; only the one-hold-per-customer
        // race is under test.
        var startTimes = Enumerable.Range(2, attempts).Select(h => BaselineNow.AddHours(h)).ToArray();

        var tasks = startTimes.Select(startsOn => customerClient.PostAsJsonAsync("/api/customers/me/appointment-holds", new
        {
            branchId = context.BranchId, serviceId = context.ServiceId, catId, startsOn, performerId = context.VetStaffAccountId
        }, Json)).ToArray();

        var responses = await Task.WhenAll(tasks);

        var others = responses.Where(r => r.StatusCode != HttpStatusCode.Created && r.StatusCode != HttpStatusCode.Conflict).ToList();
        if (others.Count > 0)
        {
            var bodies = await Task.WhenAll(others.Select(async r => $"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}"));
            Assert.Fail("Unexpected status codes (no 500s allowed): " + string.Join(" | ", bodies));
        }

        // §8.6 step 3: a conflict here (if the serialising lock ever left a narrow enough window for
        // two requests to both validate the same freed slot) must still carry the same specific code
        // I-C2 requires -- never a generic body.
        var conflictResponses = responses.Where(r => r.StatusCode == HttpStatusCode.Conflict).ToList();
        var conflictBodies = await Task.WhenAll(conflictResponses.Select(r => r.Content.ReadAsStringAsync()));
        foreach (var body in conflictBodies)
            Assert.Contains(BusinessViolationCodes.SlotNoLongerAvailable, body);

        Assert.Equal(attempts, responses.Count(r => r.StatusCode == HttpStatusCode.Created) + conflictResponses.Count);

        await using var scope = apiFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var liveHolds = await db.Set<CatEcommerce.Domain.Aggregates.Clinic.AppointmentAggregate>().AsNoTracking()
            .CountAsync(a => a.CustomerId == customerId && a.Status == CatEcommerce.Shared.Common.Enums.AppointmentStatus.Held);

        Assert.True(liveHolds <= 1, $"Expected at most one live Held row for the customer; found {liveHolds}.");
    }

    /// <summary>I-D2 -- past the cut-off, reschedule/cancel are refused.</summary>
    [Fact]
    public async Task Cancel_PastCutoff_ReturnsChangeCutoffPassed()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);

        // Cut-off default is 4 hours -- book 5 hours out, then advance the clock to 3 hours before.
        var startsOn = BaselineNow.AddHours(5);
        var hold = await customerClient.PostAsJsonAsync("/api/customers/me/appointment-holds", new
        { branchId = context.BranchId, serviceId = context.ServiceId, catId, startsOn, performerId = context.VetStaffAccountId }, Json);
        Assert.True(hold.StatusCode == HttpStatusCode.Created, await hold.Content.ReadAsStringAsync());
        var appointmentId = (await hold.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("appointmentId").GetGuid();

        var confirm = await customerClient.PostAsJsonAsync($"/api/customers/me/appointments/{appointmentId}/confirm", new { }, Json);
        Assert.True(confirm.StatusCode == HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());

        timeProvider.SetUtcNow(startsOn.AddHours(-3));

        var cancel = await customerClient.PostAsJsonAsync($"/api/customers/me/appointments/{appointmentId}/cancel", new { }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
        Assert.Contains(BusinessViolationCodes.ChangeCutoffPassed, await cancel.Content.ReadAsStringAsync());
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
