using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-APT-001 §16 I-D4..I-D6 -- ACCEPTANCE gate fix (2026-09-28, A13; test-isolation fix 2026-09-29):
/// reminders at 24h/2h, never for a moved or cancelled appointment. Every test drives the real HTTP
/// endpoints and the real <c>NotificationWorkCycle</c> delivery pass, looped via
/// <c>RunNotificationDeliveryUntilAsync</c> (never a fixed pass count -- see that helper's own remark
/// on why one pass is not reliable evidence on the shared §16.1 database) under the fake
/// <c>TimeProvider</c> -- never dispatched directly. <c>[Collection("Notifications")]</c> -- see
/// <c>AppointmentBookingFlowTests</c>'s own remark: every class driving a delivery/reminder pass
/// against the shared, un-scoped notification tables must share this collection so xUnit never runs
/// two of them concurrently, each with its own fake "now", against the same global work queue.
/// </summary>
[Collection("Notifications")]
public sealed class AppointmentReminderFlowTests(AppointmentsApiFactory factory) : AppointmentsIntegrationTestBase(factory)
{
    private static readonly DateTimeOffset BaselineNow = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero); // 2026-10-12 10:00 Asia/Dhaka (a Monday)

    /// <summary>I-D4 -- book 30h ahead; advance to 6h before; run the delivery pass twice: the 24h
    /// reminder is sent, the 2h one is not yet (its own SendAfter is still 22h away).</summary>
    [Fact]
    public async Task ID4_Book30HoursAhead_Advance6HoursBefore_The24HourReminderSent_The2HourNotYet()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);

        var startsOn = BaselineNow.AddHours(30);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, startsOn);

        timeProvider.SetUtcNow(startsOn.AddHours(-6));
        // ACCEPTANCE gate fix (2026-09-29, HIGH): a fixed two-pass count is not reliable evidence on
        // the shared, never-truncated test database -- other classes' own leftover Pending
        // confirmations can crowd this appointment's own 24h reminder request out of both passes'
        // batch caps. Loop until THIS request specifically has been expanded.
        var twentyFourHourSendAfter = startsOn.AddHours(-24);
        await RunNotificationDeliveryUntilAsync(apiFactory, db => db.Set<NotificationRequestAggregate>().AsNoTracking()
            .AnyAsync(r => r.RecipientId == customerId && r.TemplateKey == NotificationTemplateCatalogue.AppointmentReminder &&
                r.SubjectId == appointmentId && r.SendAfter == twentyFourHourSendAfter && r.Status == NotificationRequestStatus.Expanded));

        await using var scope = apiFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var reminderRequests = await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .Where(r => r.TemplateKey == NotificationTemplateCatalogue.AppointmentReminder && r.RecipientId == customerId && r.SubjectId == appointmentId)
            .ToListAsync();

        var twentyFourHourRequest = Assert.Single(reminderRequests, r => r.SendAfter == startsOn.AddHours(-24));
        var twoHourRequest = Assert.Single(reminderRequests, r => r.SendAfter == startsOn.AddHours(-2));
        Assert.Equal(NotificationRequestStatus.Expanded, twentyFourHourRequest.Status);
        Assert.Equal(NotificationRequestStatus.Pending, twoHourRequest.Status); // its own SendAfter is still in the future

        var sentReminders = await db.Set<NotificationAggregate>().AsNoTracking()
            .Where(n => n.TemplateKey == NotificationTemplateCatalogue.AppointmentReminder && n.RecipientId == customerId && n.Channel != NotificationChannel.InApp)
            .ToListAsync();
        var sentReminder = Assert.Single(sentReminders);
        Assert.Equal(NotificationStatus.Sent, sentReminder.Status);
    }

    /// <summary>I-D5 -- reschedule after the 24h reminder has already been expanded (queued) but
    /// before it was ever delivered (deliberately caught deferred for quiet hours, so the window is
    /// real, not a race): the old reminder ends Cancelled/SubjectNoLongerApplies (ADR-074); new
    /// reminders are enqueued for the new time.</summary>
    [Fact]
    public async Task ID5_RescheduleAfterReminderQueuedButBeforeSent_OldReminderCancelled_NewRemindersForNewTime()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);

        // startsOn's local time-of-day (02:00 Dhaka) is inside the default 21:00-08:00 quiet window,
        // so the 24h-before reminder's own SendAfter (exactly 24h earlier -- the SAME local
        // time-of-day) is too: expanding it there defers it (Queued, never delivered) instead of
        // sending it straight through.
        var startsOn = new DateTimeOffset(2026, 10, 14, 20, 0, 0, TimeSpan.Zero); // 2026-10-15 02:00 Dhaka
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, startsOn);

        var reminderSendAfter = startsOn.AddHours(-24); // 2026-10-13 20:00Z = 2026-10-14 02:00 Dhaka -- quiet.
        timeProvider.SetUtcNow(reminderSendAfter.AddMinutes(5));
        // ACCEPTANCE gate fix (2026-09-29, HIGH): loop until THIS customer's own reminder has been
        // (re-)expanded, rather than assuming one pass reaches it on a database other classes may
        // have left a backlog in. Safe to loop here: at this "now" the request can only ever reach
        // Queued/deferred (quiet hours), never Sent, no matter how many extra passes run -- see
        // RunNotificationDeliveryUntilAsync's own remark.
        await RunNotificationDeliveryUntilAsync(apiFactory, db => db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.RecipientId == customerId && n.TemplateKey == NotificationTemplateCatalogue.AppointmentReminder && n.Channel != NotificationChannel.InApp));

        Guid oldReminderId;
        await using (var checkScope = apiFactory.Services.CreateAsyncScope())
        {
            var db = checkScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var expanded = await db.Set<NotificationAggregate>().AsNoTracking()
                .Where(n => n.TemplateKey == NotificationTemplateCatalogue.AppointmentReminder && n.RecipientId == customerId && n.Channel != NotificationChannel.InApp)
                .SingleAsync();
            Assert.Equal(NotificationStatus.Queued, expanded.Status);
            Assert.NotNull(expanded.NotBefore); // deferred for quiet hours, not yet due
            oldReminderId = expanded.Id;
        }

        // Reschedule while the old reminder is still Queued/deferred -- well clear of the default 4h cut-off.
        var newStartsOn = startsOn.AddDays(1);
        var reschedule = await customerClient.PostAsJsonAsync($"/api/customers/me/appointments/{appointmentId}/reschedule", new
        { startsOn = newStartsOn, performerId = context.VetStaffAccountId }, Json);
        Assert.True(reschedule.StatusCode == HttpStatusCode.OK, await reschedule.Content.ReadAsStringAsync());

        // Past quiet hours' end (08:00 Dhaka = 02:00Z) -- the old, now-stale reminder becomes due and
        // is re-checked at delivery time (AppointmentReminderGuard, §8.9). Loop on the SPECIFIC old
        // notification id (captured above) reaching Cancelled, rather than one fixed pass.
        timeProvider.SetUtcNow(new DateTimeOffset(2026, 10, 14, 2, 5, 0, TimeSpan.Zero));
        await RunNotificationDeliveryUntilAsync(apiFactory, db => db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.Id == oldReminderId && n.Status == NotificationStatus.Cancelled));

        await using var scope = apiFactory.Services.CreateAsyncScope();
        var finalDb = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        var oldReminder = await finalDb.Set<NotificationAggregate>().AsNoTracking().SingleAsync(n => n.Id == oldReminderId);
        Assert.Equal(NotificationStatus.Cancelled, oldReminder.Status);
        Assert.Equal(NotificationCloseReason.SubjectNoLongerApplies, oldReminder.CloseReason);

        var newReminderRequests = await finalDb.Set<NotificationRequestAggregate>().AsNoTracking()
            .Where(r => r.TemplateKey == NotificationTemplateCatalogue.AppointmentReminder && r.RecipientId == customerId && r.SubjectId == appointmentId)
            .ToListAsync();
        Assert.Contains(newReminderRequests, r => r.SendAfter == newStartsOn.AddHours(-24));
        Assert.Contains(newReminderRequests, r => r.SendAfter == newStartsOn.AddHours(-2));
    }

    /// <summary>I-D6 -- cancel: <c>appointment.cancelled</c> is sent; the still-pending reminders are
    /// never sent (their guard fails once the appointment is no longer Booked, §8.9).</summary>
    [Fact]
    public async Task ID6_Cancel_AppointmentCancelledSent_PendingRemindersNeverSent()
    {
        var (webFactory, timeProvider) = Factory.WithFakeTimeProvider(BaselineNow);
        using var apiFactory = webFactory;

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffOnAsync(apiFactory, admin.Mobile, admin.Password);
        var context = await SeedFullyBookableBranchAsync(adminClient, BaselineNow);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerOnAsync(apiFactory);
        var catId = await SeedCatAsync(customerId);

        var startsOn = BaselineNow.AddHours(30);
        var appointmentId = await BookConfirmedAppointmentAsync(customerClient, context, catId, startsOn);

        var cancel = await customerClient.PostAsJsonAsync($"/api/customers/me/appointments/{appointmentId}/cancel", new { }, Json);
        Assert.True(cancel.StatusCode == HttpStatusCode.NoContent, await cancel.Content.ReadAsStringAsync());

        // ACCEPTANCE gate fix (2026-09-29, HIGH): loop until THIS customer's own appointment.cancelled
        // has been delivered, rather than assuming one pass reaches it on a database other classes
        // may have left a backlog in.
        await RunNotificationDeliveryUntilAsync(apiFactory, db => db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.TemplateKey == NotificationTemplateCatalogue.AppointmentCancelled && n.RecipientId == customerId
                && n.Channel != NotificationChannel.InApp && n.Status == NotificationStatus.Sent));

        // Advance past both reminders' own SendAfter, but still BEFORE the appointment's own StartsOn
        // (its request's own NotAfter) -- so what stops delivery here is specifically the guard
        // (appointment no longer Booked, §8.9), not the request simply expiring past NotAfter. Loop
        // until both reminder requests have left Pending (Suppressed, by the guard) -- never Expanded.
        timeProvider.SetUtcNow(startsOn.AddHours(-1));
        await RunNotificationDeliveryUntilAsync(apiFactory, async db =>
        {
            var stillPending = await db.Set<NotificationRequestAggregate>().AsNoTracking()
                .Where(r => r.SubjectId == appointmentId && r.TemplateKey == NotificationTemplateCatalogue.AppointmentReminder)
                .CountAsync(r => r.Status == NotificationRequestStatus.Pending);
            return stillPending == 0;
        });

        await using var scope = apiFactory.Services.CreateAsyncScope();
        var finalDb = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var anyReminderEverSent = await finalDb.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.TemplateKey == NotificationTemplateCatalogue.AppointmentReminder && n.RecipientId == customerId && n.Status == NotificationStatus.Sent);
        Assert.False(anyReminderEverSent, "A reminder for a cancelled appointment must never be sent.");

        var reminderRequests = await finalDb.Set<NotificationRequestAggregate>().AsNoTracking()
            .Where(r => r.TemplateKey == NotificationTemplateCatalogue.AppointmentReminder && r.RecipientId == customerId && r.SubjectId == appointmentId)
            .ToListAsync();
        Assert.NotEmpty(reminderRequests);
        Assert.All(reminderRequests, r => Assert.NotEqual(NotificationRequestStatus.Expanded, r.Status));
        Assert.All(reminderRequests, r => Assert.Equal(NotificationRequestStatus.Suppressed, r.Status));
        Assert.All(reminderRequests, r => Assert.Equal(NotificationSuppressionReason.SubjectNoLongerApplies, r.SuppressionReason));
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
