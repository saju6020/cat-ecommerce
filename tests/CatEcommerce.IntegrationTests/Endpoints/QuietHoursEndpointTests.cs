using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-NTF-001 §16, Quiet hours (C). I-C1, I-C2, I-C3 -- US-NTF-05.</summary>
[Collection("Notifications")]
public sealed class QuietHoursEndpointTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{
    // 2026-06-15 22:30 Asia/Dhaka (UTC+6, fixed) = 16:30 UTC.
    private static readonly DateTimeOffset InsideQuietHours = new(2026, 6, 15, 16, 30, 0, TimeSpan.Zero);

    private (WebApplicationFactory<Program> Factory, FakeTimeProvider Time) NightFactory()
    {
        var time = new FakeTimeProvider(InsideQuietHours);
        var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        return (factory, time);
    }

    [Fact]
    public async Task NormalReminder_At2230_IsQueuedAndDeferredUntil0800_NotSentBefore_SentAfter()
    {
        var (nightFactory, time) = NightFactory();
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();

        await RequestNotificationAsync(nightFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");

        await RunDeliveryPassAsync(nightFactory); // expand -> Queued, deferred to 08:00

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = InsideQuietHours.AddMinutes(-5);
        var to = InsideQuietHours.AddHours(20);

        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        // Filtered by channel too: the always-on InApp inbox copy shares the same templateKey and
        // is never held by quiet hours, so it would otherwise ambiguously match first.
        var item = log.GetProperty("items").EnumerateArray().First(i =>
            i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue && i.GetProperty("channel").GetString() != "InApp");
        Assert.Equal("Queued", item.GetProperty("status").GetString());
        Assert.True(item.TryGetProperty("deferredUntil", out var deferredUntil) && deferredUntil.ValueKind != JsonValueKind.Null);

        // Still 23:00 Dhaka -- not due yet.
        time.SetUtcNow(InsideQuietHours.AddHours(0.5));
        await RunDeliveryPassAsync(nightFactory);
        var stillQueued = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{item.GetProperty("id").GetGuid()}", Json);
        Assert.Equal("Queued", stillQueued.GetProperty("status").GetString());

        // Past 08:00 Dhaka the next morning.
        time.SetUtcNow(InsideQuietHours.AddHours(10));
        await RunDeliveryPassAsync(nightFactory);
        var sent = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{item.GetProperty("id").GetGuid()}", Json);
        Assert.Equal("Sent", sent.GetProperty("status").GetString());
    }

    /// <summary>
    /// NTF-001 #16 (§8.14): a hold that would pass <c>NotAfter</c> expires the notification when the
    /// hold is decided, not on a later pass. Queued in the daytime with a <c>NotAfter</c> that falls
    /// INSIDE the coming quiet-hours window; the delivery pass that finds it inside quiet hours
    /// (22:30 Dhaka, <c>NotAfter</c> 23:00) must expire it at once instead of deferring it to 08:00.
    /// </summary>
    [Fact]
    public async Task HoldThatWouldPassNotAfter_ExpiresTheNotificationAtOnce_NotOnALaterPass()
    {
        var daytime = InsideQuietHours.AddHours(-4.5); // 18:00 Dhaka -- outside quiet hours
        var time = new FakeTimeProvider(daytime);
        var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();

        var command = await RequestNotificationAsync(factory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            deduplicationKey: $"test:{Guid.NewGuid():N}",
            notAfter: InsideQuietHours.AddMinutes(30)); // 23:00 Dhaka

        await ExpandOnlyAsync(factory, command.RequestId); // daytime: Queued, not held

        time.SetUtcNow(InsideQuietHours); // 22:30 Dhaka: now inside quiet hours, before NotAfter
        await RunDeliveryPassAsync(factory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = daytime.AddMinutes(-5);
        var to = InsideQuietHours.AddHours(20);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var external = log.GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue && i.GetProperty("channel").GetString() != "InApp")
            .ToList();
        Assert.NotEmpty(external);
        Assert.All(external, item => Assert.Equal("Expired", item.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task UrgentTemplate_At2230_SentAtOnce()
    {
        var (nightFactory, _) = NightFactory();
        var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync();

        // account.password-reset is Urgent (§8.2) -- reused here as the shipped urgent template.
        await RequestNotificationAsync(nightFactory, NotificationTemplateCatalogue.PasswordReset, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["resetLink"] = "https://portal.example.test/reset-password#token=abc", ["expiresInMinutes"] = "15" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");

        await RunDeliveryPassAsync(nightFactory); // expand -- Urgent, never deferred
        await RunDeliveryPassAsync(nightFactory); // deliver immediately

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = InsideQuietHours.AddMinutes(-5);
        var to = InsideQuietHours.AddMinutes(5);

        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var item = log.GetProperty("items").EnumerateArray().First(i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.PasswordReset);
        Assert.Equal("Sent", item.GetProperty("status").GetString());
    }

    [Fact]
    public async Task InboxCopy_At2230_DeliveredAtOnce()
    {
        var (nightFactory, _) = NightFactory();
        var (client, customerId, _) = await RegisterAndSignInCustomerAsync();

        await RequestNotificationAsync(nightFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");

        await RunDeliveryPassAsync(nightFactory);

        var inbox = await client.GetFromJsonAsync<JsonElement>("/api/customers/me/inbox", Json);
        var items = inbox.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items); // InApp copies are never held by quiet hours
    }

    /// <summary>
    /// TEST gate, second pass, 2026-09-27 -- closes I-C4 (BLOCKED on the first pass: no test drove a
    /// branch-specific <c>notifications.quietHours</c> override). A fresh branch's own override
    /// ("09:00-10:00" Asia/Dhaka -- a daytime window the shared default, 21:00-08:00, never covers)
    /// is set via <c>PUT /api/admin/settings?branchId=...</c>, and a notification carrying that
    /// <c>BranchId</c> is requested at 09:30 Asia/Dhaka. <c>IBusinessSettings</c>'s resolution order
    /// (§8.7: branch override -&gt; global -&gt; the definition's own default) means that if
    /// <c>QuietHoursCalendar</c> failed to pass this specific <c>BranchId</c> through (falling back
    /// to the main branch or the global default instead), the notification would be SENT AT ONCE --
    /// 09:30 is well outside 21:00-08:00. Deferral is only possible if the branch-specific override
    /// was genuinely read for THIS branch.
    /// </summary>
    [Fact]
    public async Task BranchQuietHoursOverride_AppliesToARequestCarryingThatBranchId()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var branchCode = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var createBranch = await adminClient.PostAsJsonAsync("/api/admin/branches",
            new { code = branchCode, name = "Quiet Hours Override Branch", timeZoneId = "Asia/Dhaka" }, Json);
        Assert.Equal(HttpStatusCode.Created, createBranch.StatusCode);
        var branchId = (await createBranch.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();

        var setOverride = await adminClient.PutAsJsonAsync($"/api/admin/settings?branchId={branchId}", new
        {
            changes = new[] { new { key = "notifications.quietHours", value = "09:00-10:00" } }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setOverride.StatusCode);

        // 2026-06-15 09:30 Asia/Dhaka (UTC+6, fixed, no DST) = 03:30 UTC -- inside the OVERRIDE
        // window, outside the shared default.
        var insideOverrideWindow = new DateTimeOffset(2026, 6, 15, 3, 30, 0, TimeSpan.Zero);
        var time = new FakeTimeProvider(insideOverrideWindow);
        var branchFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));

        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        await RequestNotificationAsync(branchFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            branchId: branchId, deduplicationKey: $"test:{Guid.NewGuid():N}");

        await RunDeliveryPassAsync(branchFactory); // expand -- must be held by THIS branch's own override, not the shared default

        var from = insideOverrideWindow.AddMinutes(-5);
        var to = insideOverrideWindow.AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>(
            $"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var item = log.GetProperty("items").EnumerateArray().First(i =>
            i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue && i.GetProperty("channel").GetString() != "InApp");

        Assert.Equal("Queued", item.GetProperty("status").GetString());
        Assert.True(item.TryGetProperty("deferredUntil", out var deferredUntil) && deferredUntil.ValueKind != JsonValueKind.Null);

        // Deferred to 10:00 Asia/Dhaka the SAME day (04:00 UTC) -- the OVERRIDE's own end, not the
        // shared default's 08:00.
        var expectedDeferredUntil = new DateTimeOffset(2026, 6, 15, 4, 0, 0, TimeSpan.Zero);
        Assert.Equal(expectedDeferredUntil, deferredUntil.GetDateTimeOffset());
    }

    /// <summary>
    /// NTF-001 row 5 review finding: a Normal-urgency row stuck Sending at the claim cap, seen
    /// again INSIDE quiet hours, must be parked Undeliverable -- not run into <c>Defer()</c>, which
    /// throws for a Sending row and would block parking until quiet hours end.
    /// </summary>
    [Fact]
    public async Task StuckSendingRowAtTheClaimCap_DuringQuietHours_IsParked_NotDeferred()
    {
        var (nightFactory, time) = NightFactory();
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();

        await RequestNotificationAsync(nightFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(nightFactory); // expand -> Queued, deferred to 08:00

        Guid notificationId;
        await using (var scope = nightFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            notificationId = await db.Set<NotificationAggregate>().AsNoTracking()
                .Where(n => n.RecipientId == customerId && n.TemplateKey == NotificationTemplateCatalogue.VaccinationDue && n.Channel != NotificationChannel.InApp)
                .Select(n => n.Id).FirstAsync();
        }

        time.SetUtcNow(InsideQuietHours.AddHours(10)); // 08:30 next morning -- due
        for (var i = 0; i < NotificationRetryRules.MaxAttempts; i++)
        {
            await using var scope = nightFactory.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<NotificationAggregate>>();
            var claimStore = scope.ServiceProvider.GetRequiredService<INotificationClaimStore>();
            var notification = await repository.GetByIdAsync(notificationId);
            notification.Claim(time.GetUtcNow(), TimeSpan.FromSeconds(120));
            Assert.True(await claimStore.TrySaveClaimAsync(notification));
            time.Advance(TimeSpan.FromSeconds(125)); // lease lapses; "crashed" before RecordAttempt
        }

        time.SetUtcNow(InsideQuietHours.AddHours(24)); // 22:30 the next night -- inside quiet hours again
        await RunDeliveryPassAsync(nightFactory);

        await using var assertScope = nightFactory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var final = await assertDb.Set<NotificationAggregate>().AsNoTracking().FirstAsync(n => n.Id == notificationId);
        Assert.Equal(NotificationStatus.Undeliverable, final.Status);
    }

    /// <summary>
    /// CODE_REVIEW gate fix (2026-09-27, second pass, HIGH): a Normal-urgency template's expand path
    /// calls <c>IQuietHoursCalendar.NextAllowedInstantAsync</c> -- a real database read
    /// (<c>QuietHoursCalendar</c> loads the branch) -- from inside
    /// <c>ExpandNotificationRequestCommandHandler</c>'s render try/catch. The prior gate-fix
    /// (b496f22) had widened that catch to <c>Exception ex when (ex is not
    /// OperationCanceledException)</c> to fix a DIFFERENT bug (a poison template/value that must be
    /// Suppressed), but that widening also swallowed a transient failure from this DB read and
    /// permanently Suppressed the request instead of leaving it to retry -- silent, irrecoverable
    /// data loss on a momentary database hiccup, including of password resets. The fix narrows the
    /// caught exception types to a closed, render-only set (never a database exception type), so a
    /// throwing quiet-hours calendar's failure propagates out of the handler, is caught and logged by
    /// <c>NotificationWorkCycle.DispatchAsync</c>'s per-item catch-all exactly like any other
    /// transient infra failure, and the request is left <c>Pending</c> -- retried on the very next
    /// pass, never touched by <c>SuppressAsync</c>.
    /// </summary>
    [Fact]
    public async Task QuietHoursCalendarThrows_LeavesRequestPendingForRetry_NeverSuppressed()
    {
        var daytimeFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()));
            services.AddScoped<IQuietHoursCalendar>(_ => new ThrowingQuietHoursCalendar());
        }));

        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();

        var command = await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");

        // The DB failure surfaces from inside the command handler, propagates through the
        // dispatcher, and is caught and logged by NotificationWorkCycle's per-item catch-all -- the
        // pass itself must not throw.
        await RunDeliveryPassAsync(daytimeFactory);

        await using (var scope = daytimeFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var request = await db.Set<NotificationRequestAggregate>().AsNoTracking().FirstAsync(r => r.Id == command.RequestId);
            Assert.Equal(NotificationRequestStatus.Pending, request.Status);
            Assert.Null(request.SuppressionReason);
        }

        // A second pass, with the DB read now healthy, must retry and succeed -- proving this was a
        // retry, not a stuck row.
        var healthyFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()))));
        await RunDeliveryPassAsync(healthyFactory);

        await using (var scope = healthyFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var request = await db.Set<NotificationRequestAggregate>().AsNoTracking().FirstAsync(r => r.Id == command.RequestId);
            Assert.Equal(NotificationRequestStatus.Expanded, request.Status);
        }
    }

    /// <summary>
    /// CODE_REVIEW gate fix (2026-09-27, third pass, HIGH): the second-pass fix above only proved
    /// ONE failed pass stays Pending. The 8fd5a37 attempt-count backstop
    /// (<c>NotificationRequestRules.MaxExpansionAttempts</c>) fired on ANY exception escaping
    /// <c>ExpandNotificationRequestCommandHandler</c>'s dispatch -- including this exact
    /// transient-DB-read scenario -- so after <c>MaxExpansionAttempts</c> consecutive failing
    /// polls the request was silently and permanently <c>Suppressed(PersistenceFailed)</c>,
    /// reopening a bounded version of the very defect the second pass fixed. The fix scopes the
    /// backstop to genuine SAVE failures only (<c>DbUpdateException</c>, thrown only by
    /// <c>SaveChangesAsync</c> -- never by the plain read <c>quietHoursCalendar
    /// .NextAllowedInstantAsync</c> throws here). This drives MORE than
    /// <c>MaxExpansionAttempts</c> consecutive failing passes and asserts the request is still
    /// <c>Pending</c>, with <c>AttemptCount</c> never having moved off zero, throughout.
    /// </summary>
    [Fact]
    public async Task QuietHoursCalendarThrowsRepeatedly_PastMaxExpansionAttempts_StillPendingNeverCounted()
    {
        var daytimeFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()));
            services.AddScoped<IQuietHoursCalendar>(_ => new ThrowingQuietHoursCalendar());
        }));

        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();

        var command = await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");

        for (var pass = 0; pass < NotificationRequestRules.MaxExpansionAttempts + 3; pass++)
        {
            await RunDeliveryPassAsync(daytimeFactory);

            await using var scope = daytimeFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var request = await db.Set<NotificationRequestAggregate>().AsNoTracking().FirstAsync(r => r.Id == command.RequestId);

            Assert.Equal(NotificationRequestStatus.Pending, request.Status);
            Assert.Null(request.SuppressionReason);
            Assert.Equal(0, request.AttemptCount); // never counted: this is a read failure, not a save failure
        }

        // Still recoverable once the dependency is healthy again -- proving this was a retry the
        // whole time, never a slowly-arming terminal countdown.
        var healthyFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()))));
        await RunDeliveryPassAsync(healthyFactory);

        await using var assertScope = healthyFactory.Services.CreateAsyncScope();
        var healthyDb = assertScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var healed = await healthyDb.Set<NotificationRequestAggregate>().AsNoTracking().FirstAsync(r => r.Id == command.RequestId);
        Assert.Equal(NotificationRequestStatus.Expanded, healed.Status);
    }

    /// <summary>Simulates a transient database failure from the real <c>QuietHoursCalendar</c>'s
    /// branch read -- never a render-failure type, so the fix must let it propagate.</summary>
    private sealed class ThrowingQuietHoursCalendar : IQuietHoursCalendar
    {
        public Task<DateTimeOffset?> NextAllowedInstantAsync(DateTimeOffset now, Guid? branchId, CancellationToken ct = default) =>
            throw new TimeoutException("Injected test failure -- simulated transient database timeout, never a render failure.");
    }
}
