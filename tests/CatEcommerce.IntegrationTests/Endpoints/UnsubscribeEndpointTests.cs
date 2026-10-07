using System.Net;
using System.Net.Http;
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
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-NTF-001 §16, Unsubscribe (E). I-E1, I-E2, I-E3, I-E5 -- US-NTF-04 AC1.</summary>
[Collection("Notifications")]
public sealed class UnsubscribeEndpointTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{

    private static NotificationTemplateDefinition MarketingTestDefinition() => new(
        $"test.marketing-{Guid.NewGuid():N}", "Test marketing", "d", NotificationCategory.Marketing, NotificationUrgency.Normal,
        [NotificationChannel.Email], false,
        [new NotificationTemplateVariableDefinition("productName", "d", TemplateVariableKind.Text, "Food", [])],
        [new NotificationTemplateChannelDefault(NotificationChannel.Email, "Back in stock", null, "{{productName}} is back! [Unsubscribe]({{unsubscribeLink}})", null)]);

    private WebApplicationFactory<Program> MarketingDaytimeFactory(NotificationTemplateDefinition definition) =>
        Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()));
            services.AddSingleton<INotificationTemplateCatalogue>(new CatEcommerce.Infrastructure.Notifications.StaticNotificationTemplateCatalogue([definition]));
        }));

    private static async Task<string> GetUnsubscribeTokenFromLinkAsync(WebApplicationFactory<Program> factory, Guid notificationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var link = await db.Set<NotificationAggregate>().AsNoTracking()
            .Where(n => n.Id == notificationId)
            .Select(n => n.UnsubscribeLink)
            .FirstAsync();

        var fragmentIndex = link!.IndexOf("#token=", StringComparison.Ordinal);
        return Uri.UnescapeDataString(link[(fragmentIndex + "#token=".Length)..]);
    }

    [Fact]
    public async Task ValidToken_RevokesConsent_AuditRowWritten_SecondUse_WasSubscribedFalse_UnknownToken_400()
    {
        var definition = MarketingTestDefinition();
        var marketingFactory = MarketingDaytimeFactory(definition);

        var email = NewEmail();
        var (client, customerId, _) = await RegisterAndSignInCustomerAsync(email);
        await client.PutAsJsonAsync("/api/customers/me/notification-preferences", new { changes = new[] { new { channel = "Email", category = "Marketing", isGranted = true } } }, Json);

        await RequestNotificationAsync(marketingFactory, definition.Key, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["productName"] = "Cat Food" }, deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(marketingFactory);
        await RunDeliveryPassAsync(marketingFactory);

        var admin = await SeedStaffAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var notificationId = log.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var token = await GetUnsubscribeTokenFromLinkAsync(marketingFactory, notificationId);

        using var anonymousClient = Factory.CreateClient();
        var unsubscribe = await anonymousClient.PostAsJsonAsync("/api/notifications/unsubscribe", new { token }, Json);
        Assert.Equal(HttpStatusCode.OK, unsubscribe.StatusCode);
        var body = await unsubscribe.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Email", body.GetProperty("channel").GetString());
        Assert.True(body.GetProperty("wasSubscribed").GetBoolean());

        var preferences = await client.GetFromJsonAsync<JsonElement>("/api/customers/me/notification-preferences", Json);
        var emailMarketingCell = preferences.GetProperty("cells").EnumerateArray().First(c => c.GetProperty("channel").GetString() == "Email" && c.GetProperty("category").GetString() == "Marketing");
        Assert.False(emailMarketingCell.GetProperty("isGranted").GetBoolean());

        var auditFrom = SafeDaytimeInstant().AddDays(-1);
        var auditTo = DateTimeOffset.UtcNow.AddMinutes(5);
        var audit = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/audit-entries?entityType=CustomerNotificationPreferencesAggregate&from={Uri.EscapeDataString(auditFrom.ToString("O"))}&to={Uri.EscapeDataString(auditTo.ToString("O"))}", Json);
        Assert.Contains(audit.GetProperty("items").EnumerateArray(), i => i.GetProperty("entityId").GetGuid() == customerId);

        // Second use of the same token: already revoked -> wasSubscribed false, still 200.
        var second = await anonymousClient.PostAsJsonAsync("/api/notifications/unsubscribe", new { token }, Json);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.False(secondBody.GetProperty("wasSubscribed").GetBoolean());

        // Unknown token.
        var unknown = await anonymousClient.PostAsJsonAsync("/api/notifications/unsubscribe", new { token = "not-a-real-token" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task OneClickUnsubscribe_NoCookies_RevokesConsent()
    {
        var definition = MarketingTestDefinition();
        var marketingFactory = MarketingDaytimeFactory(definition);

        var email = NewEmail();
        var (client, customerId, _) = await RegisterAndSignInCustomerAsync(email);
        await client.PutAsJsonAsync("/api/customers/me/notification-preferences", new { changes = new[] { new { channel = "Email", category = "Marketing", isGranted = true } } }, Json);

        await RequestNotificationAsync(marketingFactory, definition.Key, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["productName"] = "Cat Food" }, deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(marketingFactory);
        await RunDeliveryPassAsync(marketingFactory);

        var admin = await SeedStaffAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var notificationId = log.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var token = await GetUnsubscribeTokenFromLinkAsync(marketingFactory, notificationId);

        using var noCookieClient = Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var oneClick = await noCookieClient.PostAsync(
            $"/api/notifications/unsubscribe/one-click?token={Uri.EscapeDataString(token)}",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["List-Unsubscribe"] = "One-Click" }));

        Assert.Equal(HttpStatusCode.OK, oneClick.StatusCode);

        var preferences = await client.GetFromJsonAsync<JsonElement>("/api/customers/me/notification-preferences", Json);
        var emailMarketingCell = preferences.GetProperty("cells").EnumerateArray().First(c => c.GetProperty("channel").GetString() == "Email" && c.GetProperty("category").GetString() == "Marketing");
        Assert.False(emailMarketingCell.GetProperty("isGranted").GetBoolean());
    }

    [Fact]
    public async Task UnsubscribeBeforeDelivery_PendingNotificationIsCancelledRecipientOptedOut()
    {
        var definition = MarketingTestDefinition();
        var marketingFactory = MarketingDaytimeFactory(definition);

        var email = NewEmail();
        var (client, customerId, _) = await RegisterAndSignInCustomerAsync(email);
        await client.PutAsJsonAsync("/api/customers/me/notification-preferences", new { changes = new[] { new { channel = "Email", category = "Marketing", isGranted = true } } }, Json);

        var requestCommand = await RequestNotificationAsync(marketingFactory, definition.Key, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["productName"] = "Cat Food" }, deduplicationKey: $"test:{Guid.NewGuid():N}");
        await ExpandOnlyAsync(marketingFactory, requestCommand.RequestId); // Queued, not yet delivered -- a full pass would also deliver it in the same call

        var admin = await SeedStaffAsync(CatEcommerce.Shared.Common.Constants.StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var item = log.GetProperty("items").EnumerateArray().First();
        Assert.Equal("Queued", item.GetProperty("status").GetString());

        var token = await GetUnsubscribeTokenFromLinkAsync(marketingFactory, item.GetProperty("id").GetGuid());
        using var anonymousClient = Factory.CreateClient();
        var unsubscribe = await anonymousClient.PostAsJsonAsync("/api/notifications/unsubscribe", new { token }, Json);
        Assert.Equal(HttpStatusCode.OK, unsubscribe.StatusCode);

        await RunDeliveryPassAsync(marketingFactory); // the next pass re-checks consent

        var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{item.GetProperty("id").GetGuid()}", Json);
        Assert.Equal("Cancelled", detail.GetProperty("status").GetString());
    }

    /// <summary>HO-NTF-001 ARCHITECTURE gate fix (2026-09-27, MEDIUM), I-E4: the Unsubscribe policy
    /// actually runs (as opposed to the second `AddRateLimiter` call composing incorrectly and
    /// silently never applying). The endpoint carries no session.</summary>
    [Fact]
    public async Task Unsubscribe_OverPermitLimit_Returns429()
    {
        const int limit = 5;
        var tightFactory = ((NotificationsApiFactory)Factory).WithTightUnsubscribeRateLimit(limit);
        using var client = tightFactory.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < limit + 1; i++)
            last = await client.PostAsJsonAsync("/api/notifications/unsubscribe", new { token = $"not-a-real-token-{i}" }, Json);

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
    }
}
