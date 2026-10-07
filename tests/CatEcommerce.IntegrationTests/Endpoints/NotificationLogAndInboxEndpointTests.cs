using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Notifications;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-NTF-001 §16, Log/resend/inbox (D). I-D1..I-D9 -- US-NTF-06.</summary>
[Collection("Notifications")]
public sealed class NotificationLogAndInboxEndpointTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{

    private WebApplicationFactory<Program> DaytimeFactory() =>
        Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()))));

    private static Dictionary<string, string> VaccinationVariables() => new()
    {
        ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://portal.example.test/portal/cats/x"
    };

    /// <summary>Creates a customer whose SMS permanently fails, producing an Undeliverable SMS
    /// notification with an Email fallback -- the shared fixture I-D2..I-D5 build on.</summary>
    private async Task<(Guid CustomerId, string Email, JsonElement SmsItem)> SeedUndeliverableSmsWithFallbackAsync(WebApplicationFactory<Program> daytimeFactory)
    {
        var email = NewEmail();
        var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync(email);
        var mobileNormalised = "+880" + mobile[1..];
        NotificationsApiFactory.ScriptOutcome(daytimeFactory.Services, NotificationChannel.Sms, mobileNormalised,
            new SendOutcome(SendOutcomeKind.PermanentFailure, null, "rejected by carrier"));

        await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            VaccinationVariables(), deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(daytimeFactory);
        await RunDeliveryPassAsync(daytimeFactory);
        await RunDeliveryPassAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var smsItem = log.GetProperty("items").EnumerateArray().First(i => i.GetProperty("channel").GetString() == "Sms");

        return (customerId, email, smsItem);
    }

    [Fact]
    public async Task SearchByRecipientDestinationTemplateStatus_FindsMatchingRows_DestinationsMasked()
    {
        var daytimeFactory = DaytimeFactory();
        var (customerId, email, smsItem) = await SeedUndeliverableSmsWithFallbackAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);

        var byRecipient = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.NotEmpty(byRecipient.GetProperty("items").EnumerateArray());

        var byTemplate = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?templateKey={NotificationTemplateCatalogue.VaccinationDue}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.Contains(byTemplate.GetProperty("items").EnumerateArray(), i => i.GetProperty("recipientId").GetGuid() == customerId);

        var byStatus = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&status=Undeliverable&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.All(byStatus.GetProperty("items").EnumerateArray(), i => Assert.Equal("Undeliverable", i.GetProperty("status").GetString()));

        var byDestination = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?destination={Uri.EscapeDataString(email)}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.Contains(byDestination.GetProperty("items").EnumerateArray(), i => i.GetProperty("recipientId").GetGuid() == customerId);

        // Destinations are masked in the list -- never the full email/mobile.
        var maskedDestination = smsItem.GetProperty("destinationMasked").GetString();
        Assert.NotNull(maskedDestination);
        Assert.Contains('•', maskedDestination);
    }

    [Fact]
    public async Task Detail_ShowsFullDestinationAttemptsAndFallbackChain()
    {
        var daytimeFactory = DaytimeFactory();
        var (_, _, smsItem) = await SeedUndeliverableSmsWithFallbackAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{smsItem.GetProperty("id").GetGuid()}", Json);
        Assert.False(string.IsNullOrEmpty(detail.GetProperty("destination").GetString()));
        Assert.NotEmpty(detail.GetProperty("attempts").EnumerateArray());
        Assert.True(detail.GetProperty("chain").GetProperty("fallbacks").GetArrayLength() > 0);
    }

    [Fact]
    public async Task Resend_UndeliverableSmsOnEmail_Creates201WithNewIdAndAuditRow()
    {
        var daytimeFactory = DaytimeFactory();
        var (_, _, smsItem) = await SeedUndeliverableSmsWithFallbackAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var resend = await adminClient.PostAsJsonAsync($"/api/admin/notifications/{smsItem.GetProperty("id").GetGuid()}/resend", new { channel = "Email" }, Json);
        Assert.Equal(HttpStatusCode.Created, resend.StatusCode);
        var resendBody = await resend.Content.ReadFromJsonAsync<JsonElement>(Json);
        var newId = resendBody.GetProperty("notificationId").GetGuid();
        Assert.NotEqual(Guid.Empty, newId);
        // Gate fix 2026-09-27 (ARCHITECTURE LOW): §12.4 specifies { notificationId, status, notBefore? }.
        Assert.Equal("Queued", resendBody.GetProperty("status").GetString());
        Assert.True(resendBody.TryGetProperty("notBefore", out _));

        var from = SafeDaytimeInstant().AddDays(-1);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var audit = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/audit-entries?actorUserId={admin.StaffAccountId}&entityType=NotificationAggregate&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.Contains(audit.GetProperty("items").EnumerateArray(), i => i.GetProperty("action").GetString() == "NotificationResent");

        // Gate fix 2026-09-27 (ARCHITECTURE/CODE_REVIEW MEDIUM, §11.4 C7): the resent Email
        // notification is re-rendered for its own channel, not the Sms original's rendered fields
        // reused verbatim -- an Sms body carries no Subject/HtmlBody at all.
        var resentDetail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{newId}", Json);
        Assert.Equal("Email", resentDetail.GetProperty("channel").GetString());
        Assert.False(string.IsNullOrEmpty(resentDetail.GetProperty("subject").GetString()));
    }

    [Fact]
    public async Task Resend_SecretNotification_InApp_AndFourthResend_AllRefused()
    {
        var (client, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var daytimeFactory = DaytimeFactory();

        var reset = await client.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobile }, Json);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        await RunDeliveryPassAsync();
        await RunDeliveryPassAsync();

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var resetItem = log.GetProperty("items").EnumerateArray().First(i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.PasswordReset);

        var resendSecret = await adminClient.PostAsJsonAsync($"/api/admin/notifications/{resetItem.GetProperty("id").GetGuid()}/resend", new { }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, resendSecret.StatusCode);
        var secretBody = await resendSecret.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains(secretBody.GetProperty("errors").EnumerateArray(), e => e.GetProperty("errorCode").GetString() == BusinessViolationCodes.NotificationCannotBeResent);

        // An in-app inbox item is also never resendable.
        var (_, sameCustomerId, _) = (client, customerId, mobile);
        await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, sameCustomerId,
            VaccinationVariables(), deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(daytimeFactory);

        var log2 = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(SafeDaytimeInstant().AddMinutes(-5).ToString("O"))}&to={Uri.EscapeDataString(SafeDaytimeInstant().AddMinutes(5).ToString("O"))}", Json);
        var inboxItem = log2.GetProperty("items").EnumerateArray().First(i => i.GetProperty("channel").GetString() == "InApp");
        var resendInbox = await adminClient.PostAsJsonAsync($"/api/admin/notifications/{inboxItem.GetProperty("id").GetGuid()}/resend", new { }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, resendInbox.StatusCode);
    }

    [Fact]
    public async Task ResendLimitReached_AfterThreeResends()
    {
        var daytimeFactory = DaytimeFactory();
        var (_, _, smsItem) = await SeedUndeliverableSmsWithFallbackAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var originalId = smsItem.GetProperty("id").GetGuid();

        for (var i = 0; i < 3; i++)
        {
            var resend = await adminClient.PostAsJsonAsync($"/api/admin/notifications/{originalId}/resend", new { channel = "Email" }, Json);
            Assert.Equal(HttpStatusCode.Created, resend.StatusCode);
        }

        var fourth = await adminClient.PostAsJsonAsync($"/api/admin/notifications/{originalId}/resend", new { channel = "Email" }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, fourth.StatusCode);
        var body = await fourth.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains(body.GetProperty("errors").EnumerateArray(), e => e.GetProperty("errorCode").GetString() == BusinessViolationCodes.ResendLimitReached);
    }

    /// <summary>HO-NTF-001 SECURITY gate fix (2026-09-27, LOW). The handler used to count existing
    /// resends and then insert, with no lock or unique constraint -- N concurrent resends could all
    /// pass the same "count &lt; MaxResendsPerNotification" check before any of them committed
    /// (probed: 10 parallel requests gave 8 x 201 against a limit of 3). ResendOrdinal now backs a
    /// unique filtered index, serialising the limit at the database.</summary>
    [Fact]
    public async Task ConcurrentResends_NeverExceedTheLimit()
    {
        var daytimeFactory = DaytimeFactory();
        var (_, _, smsItem) = await SeedUndeliverableSmsWithFallbackAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var originalId = smsItem.GetProperty("id").GetGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => adminClient.PostAsJsonAsync($"/api/admin/notifications/{originalId}/resend", new { channel = "Email" }, Json)));

        var createdCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        var refusedCount = responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        // The atomicity guarantee is "never exceeds the limit", not "always exactly reaches it under
        // adversarial concurrency without a retry loop": several requests can race for the SAME
        // ResendOrdinal slot (all reading the same prior-resend count before any of them commits),
        // in which case one wins and the rest are refused even if a later ordinal was never
        // attempted by anyone -- exactly the unique-constraint violation the fix introduces,
        // confirmed by re-running this test and observing it in the logs. What must never happen,
        // and does not, is createdCount exceeding the limit (the pre-fix bug: 8 x 201 against 3).
        Assert.True(createdCount <= NotificationResendRules.MaxResendsPerNotification,
            $"Expected at most {NotificationResendRules.MaxResendsPerNotification} successful resends, got {createdCount}.");
        Assert.Equal(10, createdCount + refusedCount);
    }

    [Fact]
    public async Task NurseCannotViewLog()
    {
        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var nurseClient = await SignInStaffAsync(nurse.Mobile, nurse.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var response = await nurseClient.GetAsync($"/api/admin/notifications?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LogWithoutRange_Returns400_AndOver93Days_Returns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var noRange = await adminClient.GetAsync("/api/admin/notifications");
        Assert.Equal(HttpStatusCode.BadRequest, noRange.StatusCode);

        var from = DateTimeOffset.UtcNow.AddDays(-100);
        var to = DateTimeOffset.UtcNow;
        var tooWide = await adminClient.GetAsync($"/api/admin/notifications?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.BadRequest, tooWide.StatusCode);
    }

    /// <summary>
    /// TEST gate addition (paging limits) -- §12.7's shared <c>PagingRules</c> (MaxPageNumber
    /// 1,000,000 / MaxPageSize 100) is checked by every admin-notifications-log request, but nothing
    /// exercised it here before this pass.
    /// </summary>
    [Fact]
    public async Task LogPageSizeOrPageNumberOverLimit_Returns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var range = $"from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

        var pageSizeOverLimit = await adminClient.GetAsync($"/api/admin/notifications?{range}&pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, pageSizeOverLimit.StatusCode);

        var pageNumberOverLimit = await adminClient.GetAsync($"/api/admin/notifications?{range}&pageNumber=1000001");
        Assert.Equal(HttpStatusCode.BadRequest, pageNumberOverLimit.StatusCode);

        var atTheLimit = await adminClient.GetAsync($"/api/admin/notifications?{range}&pageSize=100&pageNumber=1000000");
        Assert.Equal(HttpStatusCode.OK, atTheLimit.StatusCode); // the limit itself is still accepted, only "over" is rejected
    }

    /// <summary>Same cap, the customer-facing inbox endpoint.</summary>
    [Fact]
    public async Task InboxPageSizeOverLimit_Returns400()
    {
        var (client, _, _) = await RegisterAndSignInCustomerAsync();

        var response = await client.GetAsync("/api/customers/me/inbox?pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inbox_NewestFirst_UnreadCount_MarkRead()
    {
        var daytimeFactory = DaytimeFactory();
        var (client, customerId, _) = await RegisterAndSignInCustomerAsync();

        await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            VaccinationVariables(), deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(daytimeFactory);

        var before = await client.GetFromJsonAsync<JsonElement>("/api/customers/me/inbox/unread-count", Json);
        var countBefore = before.GetProperty("count").GetInt32();
        Assert.True(countBefore >= 1);

        var inbox = await client.GetFromJsonAsync<JsonElement>("/api/customers/me/inbox", Json);
        var items = inbox.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        var newestId = items[0].GetProperty("id").GetGuid();

        var markRead = await client.PostAsync($"/api/customers/me/inbox/{newestId}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, markRead.StatusCode);

        var after = await client.GetFromJsonAsync<JsonElement>("/api/customers/me/inbox/unread-count", Json);
        Assert.Equal(countBefore - 1, after.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task MarkAnotherCustomersInboxItem_Returns404()
    {
        var daytimeFactory = DaytimeFactory();
        var (_, customerAId, _) = await RegisterAndSignInCustomerAsync();
        await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerAId,
            VaccinationVariables(), deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerAId}&from={Uri.EscapeDataString(SafeDaytimeInstant().AddMinutes(-5).ToString("O"))}&to={Uri.EscapeDataString(SafeDaytimeInstant().AddMinutes(5).ToString("O"))}", Json);
        var inboxItemId = log.GetProperty("items").EnumerateArray().First(i => i.GetProperty("channel").GetString() == "InApp").GetProperty("id").GetGuid();

        var (clientB, _, _) = await RegisterAndSignInCustomerAsync();
        var response = await clientB.PostAsync($"/api/customers/me/inbox/{inboxItemId}/read", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private const string CustomerPassword = "correcthorsebattery"; // matches RegisterAndSignInCustomerAsync's own hardcoded password

    /// <summary>HO-NTF-001 ARCHITECTURE/CODE_REVIEW gate fix (2026-09-27), I-D10: the missing test
    /// case for <c>ReassignInboxOnCustomerMergedHandler</c> -- an in-app inbox item moves to the
    /// merge survivor, while the external log keeps the original recipient (§11.6, "it is
    /// history").</summary>
    [Fact]
    public async Task Merge_MovesInboxItemToSurvivor_ExternalLogKeepsOriginalRecipient()
    {
        var daytimeFactory = DaytimeFactory();
        var (duplicateClient, duplicateId, duplicateMobile) = await RegisterAndSignInCustomerAsync();
        var (survivorClient, survivorId, survivorMobile) = await RegisterAndSignInCustomerAsync();

        var sharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(duplicateClient, sharedNumber);
        await AddSharedAdditionalNumberAsync(survivorClient, sharedNumber);

        await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, duplicateId,
            VaccinationVariables(), deduplicationKey: $"test:{Guid.NewGuid():N}");
        await RunDeliveryPassAsync(daytimeFactory);

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var merge = await adminClient.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge", new
        {
            survivingCustomerId = survivorId, reason = "Same person, confirmed by phone"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, merge.StatusCode);

        // The inbox item now belongs to the survivor.
        var survivorInbox = await survivorClient.GetFromJsonAsync<JsonElement>("/api/customers/me/inbox", Json);
        Assert.Contains(survivorInbox.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("category").GetString() == NotificationCategory.Transactional.DisplayName);

        // The external (Sms) log row keeps the pre-merge recipient id -- it is history.
        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={duplicateId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.Contains(log.GetProperty("items").EnumerateArray(), i => i.GetProperty("channel").GetString() == "Sms");
    }

    /// <summary>HO-NTF-001 SECURITY/ARCHITECTURE/CODE_REVIEW gate fix (2026-09-27, HIGH), I-D11.
    /// Anonymising the survivor must redact the merged-away duplicate's own external notification
    /// rows too (§11.6 keeps their pre-merge <c>RecipientId</c>, and a merged-away customer can
    /// never itself be anonymised), and must still null the destination on a row already
    /// Secret-redacted (a delivered password reset, redacted within minutes of being sent).</summary>
    [Fact]
    public async Task Anonymise_RedactsSurvivorAndMergedDuplicateNotifications_EvenIfAlreadySecretRedacted()
    {
        var daytimeFactory = DaytimeFactory();
        var (duplicateClient, duplicateId, duplicateMobile) = await RegisterAndSignInCustomerAsync();
        var (survivorClient, survivorId, survivorMobile) = await RegisterAndSignInCustomerAsync();

        var sharedNumber = NewMobile();
        await AddSharedAdditionalNumberAsync(duplicateClient, sharedNumber);
        await AddSharedAdditionalNumberAsync(survivorClient, sharedNumber);

        // A Secret-redacted row on the DUPLICATE: password reset requested (and delivered) before the merge.
        // Test-quality fix (2026-09-27, found while verifying the I-F5 care-reminder fix): the
        // reset must be posted through daytimeFactory's OWN client, not the base Factory's --
        // posting through Factory computes the token's 15-minute NotAfter against REAL wall-clock
        // time, while the delivery passes below run against daytimeFactory's FakeTimeProvider
        // (SafeDaytimeInstant()), which is `real now + 12 hours` whenever the real UTC hour is not
        // in [2, 15). That gap dwarfs the 15-minute lifetime, so expand saw an already-Expired
        // request instead of delivering it (deterministic given the real time of day, not a flake).
        var duplicateMobileNormalised = "+880" + duplicateMobile[1..];
        using var daytimeClient = daytimeFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var reset = await daytimeClient.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = duplicateMobileNormalised }, Json);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        await RunDeliveryPassAsync(daytimeFactory); // expand
        await RunDeliveryPassAsync(daytimeFactory); // deliver -> Sent -> Secret-redacted immediately

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var merge = await adminClient.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge", new
        {
            survivingCustomerId = survivorId, reason = "Same person, confirmed by phone"
        }, Json);
        Assert.Equal(HttpStatusCode.OK, merge.StatusCode);

        var deletion = await survivorClient.PostAsJsonAsync("/api/customers/me/deletion", new { currentPassword = CustomerPassword }, Json);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);

        var from = SafeDaytimeInstant().AddMinutes(-5);
        var to = SafeDaytimeInstant().AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={duplicateId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var resetItem = log.GetProperty("items").EnumerateArray().First(i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.PasswordReset);

        Assert.Null(resetItem.GetProperty("destinationMasked").GetString()); // the duplicate's own row, now anonymised
    }

    /// <summary>Shares a mobile number as an additional phone number on the signed-in customer --
    /// the same "shared identifier" trick HO-ACC-001's own merge tests use to make two accounts
    /// merge-eligible, inlined here since this base class does not otherwise need it.</summary>
    private async Task AddSharedAdditionalNumberAsync(HttpClient client, string sharedNumber)
    {
        // "Test Customer" matches RegisterAndSignInCustomerAsync's own hardcoded registration fullName.
        var update = await client.PutAsJsonAsync("/api/customers/me", new
        {
            fullName = "Test Customer", additionalPhoneNumbers = new[] { sharedNumber }, currentPassword = CustomerPassword
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
    }
}
