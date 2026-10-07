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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-NTF-001 §16, Routing/consent (B). I-B4, I-B5, I-B8, I-B9, I-B10. Every delivery pass here
/// runs against a <see cref="FakeTimeProvider"/> fixed to a known Dhaka daytime instant (noon) --
/// these tests are not about quiet hours (that is QuietHoursEndpointTests' job), and the real
/// wall-clock time the suite happens to run at would otherwise make roughly half of all runs fall
/// inside the default 21:00-08:00 quiet window and silently defer delivery instead of sending it.
/// </summary>
[Collection("Notifications")]
public sealed class NotificationRoutingEndpointTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{
    // 06:00 UTC = 12:00 in Asia/Dhaka (fixed UTC+6, no DST) -- safely outside 21:00-08:00.

    private static Dictionary<string, string> VaccinationVariables(string catName = "Mitu") => new()
    {
        ["catName"] = catName, ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://portal.example.test/portal/cats/x"
    };

    private WebApplicationFactory<Program> DaytimeFactory(params NotificationTemplateDefinition[] extraDefinitions)
    {
        return Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()));
            if (extraDefinitions.Length > 0)
                services.AddSingleton<INotificationTemplateCatalogue>(new StaticNotificationTemplateCatalogue(extraDefinitions));
        }));
    }

    [Fact]
    public async Task PreferredEmail_DeliversEmail_WithInboxCopy()
    {
        var daytimeFactory = DaytimeFactory();
        var email = NewEmail();
        var (client, customerId, _) = await RegisterAndSignInCustomerAsync(email);

        var setPreferred = await client.PutAsJsonAsync("/api/customers/me/notification-preferences", new { preferredChannel = "Email" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setPreferred.StatusCode);

        await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId, VaccinationVariables(), deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(daytimeFactory);
        await RunDeliveryPassAsync(daytimeFactory);

        var emailSender = NotificationsApiFactory.CapturingSender(daytimeFactory.Services, NotificationChannel.Email);
        Assert.Contains(emailSender.Sent, m => m.Destination == email);

        var inboxUnread = await client.GetFromJsonAsync<JsonElement>("/api/customers/me/inbox/unread-count", Json);
        Assert.True(inboxUnread.GetProperty("count").GetInt32() >= 1);

        var preferences = await client.GetFromJsonAsync<JsonElement>("/api/customers/me/notification-preferences", Json);
        Assert.Equal("Email", preferences.GetProperty("preferredChannel").GetString());
        Assert.Equal("Email", preferences.GetProperty("effectivePreferredChannel").GetString());
    }

    /// <summary>HO-NTF-001 §16, I-B15 (untested before this fix): a channel the account has no
    /// identifier for is refused, not silently accepted.</summary>
    [Fact]
    public async Task SetPreferredChannel_AccountHasNoIdentifierForIt_Returns400()
    {
        var (client, _, _) = await RegisterAndSignInCustomerAsync(); // mobile only, no email on file

        var setPreferred = await client.PutAsJsonAsync("/api/customers/me/notification-preferences", new { preferredChannel = "Email" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, setPreferred.StatusCode);
        var body = await setPreferred.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), e => e.GetProperty("errorCode").GetString() == BusinessViolationCodes.PreferredChannelHasNoDestination);
    }

    [Fact]
    public async Task SmsPermanentFailure_FallsBackToEmail()
    {
        var daytimeFactory = DaytimeFactory();
        var email = NewEmail();
        var (client, customerId, mobile) = await RegisterAndSignInCustomerAsync(email);

        var mobileNormalised = "+880" + mobile[1..];
        NotificationsApiFactory.ScriptOutcome(daytimeFactory.Services, NotificationChannel.Sms, mobileNormalised,
            new SendOutcome(SendOutcomeKind.PermanentFailure, null, "rejected"));

        await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId, VaccinationVariables(), deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(daytimeFactory); // expand -> Sms primary (mobile-only preference)
        await RunDeliveryPassAsync(daytimeFactory); // deliver Sms -> Undeliverable, enlist fallback
        await RunDeliveryPassAsync(daytimeFactory); // deliver the fallback (Email)

        var emailSender = NotificationsApiFactory.CapturingSender(daytimeFactory.Services, NotificationChannel.Email);
        Assert.Contains(emailSender.Sent, m => m.Destination == email);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var items = log.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, i => i.GetProperty("channel").GetString() == "Sms" && i.GetProperty("status").GetString() == "Undeliverable");
        Assert.Contains(items, i => i.GetProperty("channel").GetString() == "Email" && i.GetProperty("status").GetString() == "Sent" && i.GetProperty("isFallback").GetBoolean());
    }

    /// <summary>HO-NTF-001 SECURITY HIGH 1 / CODE_REVIEW BLOCKER 1 gate fix (2026-09-27), I-B7. Before
    /// the fix, <c>tried</c> was seeded only from the currently-failing notification's own channel,
    /// so once both Sms and Email permanently failed, the fallback bounced between the two channels
    /// forever -- one new notification row per delivery pass, unbounded. Both channels permanently
    /// failing must end with exactly two rows (the original and one fallback), both Undeliverable,
    /// and no third row.</summary>
    [Fact]
    public async Task SmsAndEmailBothPermanentlyFail_EndsUndeliverable_NoInfiniteFallbackLoop()
    {
        var daytimeFactory = DaytimeFactory();
        var email = NewEmail();
        var (client, customerId, mobile) = await RegisterAndSignInCustomerAsync(email);

        var mobileNormalised = "+880" + mobile[1..];
        NotificationsApiFactory.ScriptOutcome(daytimeFactory.Services, NotificationChannel.Sms, mobileNormalised,
            new SendOutcome(SendOutcomeKind.PermanentFailure, null, "rejected"));
        NotificationsApiFactory.ScriptOutcome(daytimeFactory.Services, NotificationChannel.Email, email,
            new SendOutcome(SendOutcomeKind.PermanentFailure, null, "rejected"));

        await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId, VaccinationVariables(), deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(daytimeFactory); // expand -> Sms primary (mobile-only preference)
        await RunDeliveryPassAsync(daytimeFactory); // deliver Sms -> Undeliverable, enlist Email fallback
        await RunDeliveryPassAsync(daytimeFactory); // deliver Email fallback -> Undeliverable, no further fallback (both tried)

        // Two more passes: the pre-fix code would enlist a THIRD (Sms-again), then a FOURTH, row here.
        await RunDeliveryPassAsync(daytimeFactory);
        await RunDeliveryPassAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var items = log.GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue && i.GetProperty("channel").GetString() != "InApp")
            .ToList();

        Assert.Equal(2, items.Count); // exactly the original Sms and the one Email fallback -- never a third
        Assert.All(items, i => Assert.Equal("Undeliverable", i.GetProperty("status").GetString()));
        Assert.Contains(items, i => i.GetProperty("channel").GetString() == "Sms");
        Assert.Contains(items, i => i.GetProperty("channel").GetString() == "Email");
    }

    [Fact]
    public async Task TransactionalTemplate_DeliveredRegardlessOfMarketingOptOut()
    {
        var daytimeFactory = DaytimeFactory();
        var (client, customerId, _) = await RegisterAndSignInCustomerAsync();

        // Explicitly opt out of every marketing category the account supports.
        var preferences = await client.GetFromJsonAsync<JsonElement>("/api/customers/me/notification-preferences", Json);
        var editableCells = preferences.GetProperty("cells").EnumerateArray().Where(c => c.GetProperty("isEditable").GetBoolean()).ToList();
        foreach (var cell in editableCells)
        {
            await client.PutAsJsonAsync("/api/customers/me/notification-preferences", new
            {
                changes = new[] { new { channel = cell.GetProperty("channel").GetString(), category = cell.GetProperty("category").GetString(), isGranted = false } }
            }, Json);
        }

        await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId, VaccinationVariables(), deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(daytimeFactory);
        await RunDeliveryPassAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var items = log.GetProperty("items").EnumerateArray().ToList();

        Assert.Contains(items, i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue && i.GetProperty("status").GetString() == "Sent");
    }

    private static NotificationTemplateDefinition MarketingTestDefinition() => new(
        $"test.marketing-{Guid.NewGuid():N}", "Test marketing", "d", NotificationCategory.Marketing, NotificationUrgency.Normal,
        [NotificationChannel.Sms, NotificationChannel.Email, NotificationChannel.InApp], false,
        [new NotificationTemplateVariableDefinition("productName", "d", TemplateVariableKind.Text, "Food", [])],
        [
            new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null, "{{productName}} is back! Unsubscribe: {{unsubscribeLink}}", null),
            new NotificationTemplateChannelDefault(NotificationChannel.Email, "Back in stock", null, "{{productName}} is back! [Unsubscribe]({{unsubscribeLink}})", null),
            new NotificationTemplateChannelDefault(NotificationChannel.InApp, null, "Back in stock", "{{productName}} is back!", null),
        ]);

    [Fact]
    public async Task Marketing_ConsentOnEmailOnly_DeliversEmailWithUnsubscribeHeaders()
    {
        var definition = MarketingTestDefinition();
        var marketingFactory = DaytimeFactory(definition);

        var email = NewEmail();
        var (client, customerId, _) = await RegisterAndSignInCustomerAsync(email);
        // Marketing/email must be explicitly granted (default is opted out).
        var preferences = await client.GetFromJsonAsync<JsonElement>("/api/customers/me/notification-preferences", Json);
        var emailMarketingCell = preferences.GetProperty("cells").EnumerateArray()
            .First(c => c.GetProperty("channel").GetString() == "Email" && c.GetProperty("category").GetString() == "Marketing");
        Assert.True(emailMarketingCell.GetProperty("isEditable").GetBoolean());
        await client.PutAsJsonAsync("/api/customers/me/notification-preferences", new { changes = new[] { new { channel = "Email", category = "Marketing", isGranted = true } } }, Json);

        await RequestNotificationAsync(marketingFactory, definition.Key, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["productName"] = "Cat Food" }, deduplicationKey: $"test:{Guid.NewGuid():N}");

        await RunDeliveryPassAsync(marketingFactory);
        await RunDeliveryPassAsync(marketingFactory);

        var emailSender = NotificationsApiFactory.CapturingSender(marketingFactory.Services, NotificationChannel.Email);
        var sent = Assert.Single(emailSender.Sent, m => m.Destination == email);
        Assert.Contains("Unsubscribe", sent.TextBody);
        Assert.True(sent.Headers.ContainsKey("List-Unsubscribe"));
        Assert.Equal("List-Unsubscribe=One-Click", sent.Headers["List-Unsubscribe-Post"]);

        var smsSender = NotificationsApiFactory.CapturingSender(marketingFactory.Services, NotificationChannel.Sms);
        Assert.DoesNotContain(smsSender.Sent, m => m.TextBody.Contains("Cat Food"));
    }

    [Fact]
    public async Task Marketing_NoConsentAnywhere_Suppressed_NothingSent()
    {
        var definition = MarketingTestDefinition();
        var marketingFactory = DaytimeFactory(definition);

        var (client, customerId, _) = await RegisterAndSignInCustomerAsync();

        await RequestNotificationAsync(marketingFactory, definition.Key, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["productName"] = "Cat Food" }, deduplicationKey: $"test:{Guid.NewGuid():N}");

        await RunDeliveryPassAsync(marketingFactory);

        var emailSender = NotificationsApiFactory.CapturingSender(marketingFactory.Services, NotificationChannel.Email);
        var smsSender = NotificationsApiFactory.CapturingSender(marketingFactory.Services, NotificationChannel.Sms);

        Assert.DoesNotContain(emailSender.Sent, m => m.TextBody.Contains("Cat Food"));
        Assert.DoesNotContain(smsSender.Sent, m => m.TextBody.Contains("Cat Food"));
    }

    /// <summary>
    /// HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, MEDIUM 7). A request's variable value
    /// is checked for control/private-use characters (NotificationRequestAggregate.Create) but not
    /// for whether it actually matches the template's declared Kind -- a malformed Date value (here,
    /// `dueOn`) reaches NotificationRenderer.Render and throws FormatException, which the pre-fix
    /// code did not catch (only InvalidOperationException), leaving the request stuck Pending
    /// forever and retried every poll. Now suppressed on the very first attempt, never stuck.
    /// </summary>
    [Fact]
    public async Task MalformedDateVariable_SuppressesRequestOnFirstAttempt_NeverStuckPending()
    {
        var daytimeFactory = DaytimeFactory();
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();

        var command = await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "not-a-date", ["bookingLink"] = "https://portal.example.test/portal/cats/x" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");

        await RunDeliveryPassAsync(daytimeFactory);

        await using (var scope = daytimeFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var request = await db.Set<NotificationRequestAggregate>().AsNoTracking().FirstAsync(r => r.Id == command.RequestId);
            Assert.Equal(NotificationRequestStatus.Suppressed, request.Status);
            Assert.Equal(NotificationSuppressionReason.RenderFailed, request.SuppressionReason);
        }

        // Further passes must not resurrect or retry it (it is Suppressed, not Pending).
        await RunDeliveryPassAsync(daytimeFactory);
        await RunDeliveryPassAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.DoesNotContain(log.GetProperty("items").EnumerateArray(), i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.VaccinationDue);
    }
}
