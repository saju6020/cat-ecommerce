using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Notifications;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, second pass, MEDIUM). Reproduces probe
/// P6c (a rendered field overflowing its `notify.notification` column blocks the whole
/// BatchSize-capped queue, password resets included) and the matching overflow shapes on the
/// fallback (C5) and cross-channel resend (C7) paths. A real admin template customisation is used
/// throughout, exactly like the SECURITY probe, because the shipped catalogue's own default
/// templates never reference a variable in a way that can overflow a column.
/// </summary>
[Collection("Notifications")]
public sealed class NotificationOverflowEndpointTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{
    private static async Task<int> GetVersionAsync(HttpClient client, string key) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/admin/notification-templates/{key}", Json)).GetProperty("contentVersion").GetInt32();

    private static async Task ResetTemplateAsync(HttpClient client, string key)
    {
        var version = await GetVersionAsync(client, key);
        await client.PostAsJsonAsync($"/api/admin/notification-templates/{key}/reset", new { expectedContentVersion = version }, Json);
    }

    /// <summary>
    /// Probe P6c, reproduced end to end: an Administrator customises the deworming InApp title to
    /// include placeholders (76 raw characters, well within the template's own 100-character raw
    /// limit), a 100-character cat name pushes the RENDERED title past the column's 100-character
    /// limit, `Worker:BatchSize=3`, three such reminders, then a password reset. Before this fix,
    /// all three reminders stayed Pending forever (Postgres 22001 on every pass) and the reset,
    /// queued behind them, was never expanded either. Now the title is truncated with an ellipsis,
    /// every reminder expands normally, and the reset is expanded too.
    /// </summary>
    [Fact]
    public async Task P6c_OverlongCustomisedInAppTitle_TruncatedNotStuck_QueueNeverBlocked_ResetStillExpanded()
    {
        const string key = NotificationTemplateCatalogue.DewormingDue;
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var version = await GetVersionAsync(adminClient, key);
        var save = await adminClient.PutAsJsonAsync($"/api/admin/notification-templates/{key}", new
        {
            expectedContentVersion = version,
            contents = new object[]
            {
                new { channel = "Sms", body = "{{catName}}'s {{treatmentName}} treatment is due on {{dueOn}}. Book here: {{bookingLink}}" },
                new { channel = "Email", subject = "A parasite treatment is coming up", body = "Hello {{recipientName}},\n\n{{catName}}'s {{treatmentName}} treatment is due on {{dueOn}}. [Book now]({{bookingLink}})." },
                new { channel = "InApp", title = "Time for {{catName}}'s {{treatmentName}} treatment - book now at the clinic", body = "A treatment reminder.", linkPath = "{{bookingLink}}" },
            }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);

        try
        {
            var daytimeFactory = Factory.WithWebHostBuilder(builder => builder
                .ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Notifications:Worker:BatchSize"] = "3" }))
                .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()))));

            var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
            var longCatName = new string('M', 100);

            var reminderRequestIds = new List<Guid>();
            for (var i = 0; i < 3; i++)
            {
                var command = await RequestNotificationAsync(daytimeFactory, key, NotificationRecipientKind.Customer, customerId,
                    new Dictionary<string, string> { ["catName"] = longCatName, ["treatmentName"] = "Deworming", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://portal.example.test/x" },
                    deduplicationKey: $"test:{Guid.NewGuid():N}");
                reminderRequestIds.Add(command.RequestId);
            }

            var (_, resetCustomerId, _) = await RegisterAndSignInCustomerAsync();
            var resetCommand = await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.PasswordReset, NotificationRecipientKind.Customer, resetCustomerId,
                new Dictionary<string, string> { ["resetLink"] = "https://portal.example.test/reset-password#token=abc", ["expiresInMinutes"] = "15" },
                deduplicationKey: $"test:{Guid.NewGuid():N}");

            for (var pass = 0; pass < 4; pass++)
                await RunDeliveryPassAsync(daytimeFactory);

            await using var scope = daytimeFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

            foreach (var requestId in reminderRequestIds)
            {
                var request = await db.Set<NotificationRequestAggregate>().AsNoTracking().FirstAsync(r => r.Id == requestId);
                Assert.Equal(NotificationRequestStatus.Expanded, request.Status);
                Assert.Equal(0, request.AttemptCount); // truncated on the first render -- never even a failed save

                var inbox = await db.Set<NotificationAggregate>().AsNoTracking().FirstAsync(n => n.RequestId == requestId && n.Channel == NotificationChannel.InApp);
                Assert.True(inbox.Title!.Length <= 100);
                Assert.EndsWith("…", inbox.Title);
            }

            // The key P6c assertion: the reset was NOT stuck behind the reminders.
            var resetRequest = await db.Set<NotificationRequestAggregate>().AsNoTracking().FirstAsync(r => r.Id == resetCommand.RequestId);
            Assert.Equal(NotificationRequestStatus.Expanded, resetRequest.Status);
        }
        finally
        {
            await ResetTemplateAsync(adminClient, key);
        }
    }

    /// <summary>
    /// The fallback shape of the same bug (§11.4 C5): the primary channel (Sms) fails permanently,
    /// and the customised Email fallback content would render a body over the 10,000-character
    /// column limit. Before this fix, the claim was already committed, the final save (which would
    /// have recorded the fallback) threw 22001, and the row was left `Sending` -- re-claimed and
    /// re-sent every lease, unbounded, with no 5xx visible to anyone but silently looping forever.
    /// Now the overflow is caught before the fallback is ever built: no fallback is created, and the
    /// original notification ends `Undeliverable`, exactly like any other unrenderable fallback.
    /// </summary>
    [Fact]
    public async Task FallbackEmailBodyOverflow_EndsUndeliverable_NeverStuckSending()
    {
        const string key = NotificationTemplateCatalogue.VaccinationDue;
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var version = await GetVersionAsync(adminClient, key);
        // The raw template must stay within its own 10,000-character limit (TemplateRules), so the
        // filler alone is deliberately kept under that -- it is the 2000-character (the maximum a
        // request variable may carry) catName substitution that pushes the RENDERED body over the
        // column's 10,000-character limit.
        var filler = new string('x', 8900);
        var longCatName = new string('M', NotificationRequestRules.MaxVariableLength);
        var save = await adminClient.PutAsJsonAsync($"/api/admin/notification-templates/{key}", new
        {
            expectedContentVersion = version,
            contents = new object[]
            {
                new { channel = "Sms", body = "{{catName}}'s {{vaccineName}} is due {{dueOn}} {{bookingLink}}" },
                new { channel = "Email", subject = "Reminder", body = filler + " {{catName}} {{vaccineName}} {{dueOn}} [Book]({{bookingLink}})" },
                new { channel = "InApp", title = "Reminder", body = "{{catName}}'s {{vaccineName}} is due {{dueOn}}", linkPath = "{{bookingLink}}" },
            }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);

        try
        {
            var daytimeFactory = Factory.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()))));

            var email = NewEmail();
            var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync(email);
            var mobileNormalised = "+880" + mobile[1..];
            NotificationsApiFactory.ScriptOutcome(daytimeFactory.Services, NotificationChannel.Sms, mobileNormalised,
                new SendOutcome(SendOutcomeKind.PermanentFailure, null, "rejected by carrier"));

            var command = await RequestNotificationAsync(daytimeFactory, key, NotificationRecipientKind.Customer, customerId,
                new Dictionary<string, string> { ["catName"] = longCatName, ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://portal.example.test/x" },
                deduplicationKey: $"test:{Guid.NewGuid():N}");

            await RunDeliveryPassAsync(daytimeFactory); // expand
            await RunDeliveryPassAsync(daytimeFactory); // deliver Sms -> PermanentFailure -> fallback attempt overflows -> no fallback -> Undeliverable
            await RunDeliveryPassAsync(daytimeFactory); // a further pass must not re-claim or re-send anything

            await using var scope = daytimeFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

            var smsNotification = await db.Set<NotificationAggregate>().AsNoTracking().FirstAsync(n => n.RequestId == command.RequestId && n.Channel == NotificationChannel.Sms);
            // Terminal, not stuck Sending -- the whole point of the fix: before it, the overflowing
            // fallback save threw AFTER the claim committed, leaving this row Sending forever,
            // re-claimed and re-sent on every subsequent lease.
            Assert.Equal(NotificationStatus.Undeliverable, smsNotification.Status);

            var emailFallbackExists = await db.Set<NotificationAggregate>().AsNoTracking().AnyAsync(n => n.FallbackFromId == smsNotification.Id);
            Assert.False(emailFallbackExists); // the overflowing fallback was never created
        }
        finally
        {
            await ResetTemplateAsync(adminClient, key);
        }
    }

    /// <summary>
    /// The cross-channel resend shape of the same bug (§11.4 C7): resending an Undeliverable Sms
    /// notification on Email, where the (by-now customised) Email template would overflow, used to
    /// return 500 -- `EfNotificationResendStore` only translates the unique-violation exception, not
    /// a column-length overflow. Now the overflow is caught before the save is attempted, in
    /// `RenderForChannelAsync`, so it falls back to reusing the original (Sms) channel's
    /// already-column-safe rendered fields -- "a degraded resend is still a resend" -- and the
    /// resend still succeeds with 201, never 500.
    /// </summary>
    [Fact]
    public async Task ResendCrossChannelBodyOverflow_FallsBackToOriginalFields_Returns201_Never500()
    {
        const string key = NotificationTemplateCatalogue.VaccinationDue;

        var daytimeFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()))));

        var email = NewEmail();
        var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync(email);
        var mobileNormalised = "+880" + mobile[1..];
        NotificationsApiFactory.ScriptOutcome(daytimeFactory.Services, NotificationChannel.Sms, mobileNormalised,
            new SendOutcome(SendOutcomeKind.PermanentFailure, null, "rejected by carrier"));

        // Seeded with the DEFAULT (uncustomised) templates -- the automatic fallback to Email here
        // succeeds normally (a 2000-character catName renders comfortably under 10,000 through the
        // default, much shorter templates); only the SUBSEQUENT admin-initiated resend below
        // exercises the overflowing customised template, isolating C7's own re-render path from
        // C5's.
        var longCatName = new string('M', NotificationRequestRules.MaxVariableLength);
        await RequestNotificationAsync(daytimeFactory, key, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = longCatName, ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://portal.example.test/x" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(daytimeFactory);
        await RunDeliveryPassAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var smsItem = log.GetProperty("items").EnumerateArray().First(i => i.GetProperty("channel").GetString() == "Sms");

        var version = await GetVersionAsync(adminClient, key);
        var filler = new string('x', 8900);
        var save = await adminClient.PutAsJsonAsync($"/api/admin/notification-templates/{key}", new
        {
            expectedContentVersion = version,
            contents = new object[]
            {
                new { channel = "Sms", body = "{{catName}}'s {{vaccineName}} is due {{dueOn}} {{bookingLink}}" },
                new { channel = "Email", subject = "Reminder", body = filler + " {{catName}} {{vaccineName}} {{dueOn}} [Book]({{bookingLink}})" },
                new { channel = "InApp", title = "Reminder", body = "{{catName}}'s {{vaccineName}} is due {{dueOn}}", linkPath = "{{bookingLink}}" },
            }
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);

        try
        {
            var resend = await adminClient.PostAsJsonAsync($"/api/admin/notifications/{smsItem.GetProperty("id").GetGuid()}/resend", new { channel = "Email" }, Json);
            Assert.Equal(HttpStatusCode.Created, resend.StatusCode);

            var resendBody = await resend.Content.ReadFromJsonAsync<JsonElement>(Json);
            var newId = resendBody.GetProperty("notificationId").GetGuid();

            var resentDetail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{newId}", Json);
            Assert.Equal("Email", resentDetail.GetProperty("channel").GetString());
            // Fell back to the ORIGINAL Sms notification's rendered fields (no Subject at all,
            // rather than the overflowing customised Email content) -- proving the fallback path
            // was taken, not that the overflowing content was somehow saved anyway.
            Assert.True(string.IsNullOrEmpty(resentDetail.GetProperty("subject").GetString()));
        }
        finally
        {
            await ResetTemplateAsync(adminClient, key);
        }
    }

    /// <summary>
    /// SECURITY/CODE_REVIEW gate fix (2026-09-27, second pass, MEDIUM), the queue-ordering half of
    /// the fix: <c>NotificationWorkQueue.GetPendingRequestIdsAsync</c> used to order strictly by
    /// <c>RequestedOn</c> (oldest first), so a request that keeps failing to save was re-selected
    /// FIRST on every pass, ahead of every healthy request behind it. A never-attempted request now
    /// always sorts before a previously-failed one, however much older the failed one is.
    /// </summary>
    [Fact]
    public async Task GetPendingRequestIds_NeverAttemptedRequest_SortsBeforeAnOlderPreviouslyFailedOne()
    {
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();

        var olderFailedCommand = await RequestNotificationAsync(Factory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<NotificationRequestAggregate>>();
            var olderFailed = await repository.GetByIdAsync(olderFailedCommand.RequestId);
            olderFailed.RecordFailedAttempt(DateTimeOffset.UtcNow, NotificationRequestRules.MaxExpansionAttempts);
            await repository.UpdateAsync(olderFailed, EventPublishOption.InMemoryPublishOnly);
        }

        // Requested strictly AFTER the failed one, so a RequestedOn-only order would still put the
        // failed one first.
        var neverAttemptedCommand = await RequestNotificationAsync(Factory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");

        await using var assertScope = Factory.Services.CreateAsyncScope();
        var workQueue = assertScope.ServiceProvider.GetRequiredService<NotificationWorkQueue>();
        var ids = await workQueue.GetPendingRequestIdsAsync(2);

        Assert.Equal(neverAttemptedCommand.RequestId, ids[0]);
        Assert.Equal(olderFailedCommand.RequestId, ids[1]);
    }
}
