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
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-NTF-001 §16, Requests/delivery (B). I-B1, I-B3 -- FR-ACC-9 end to end.</summary>
[Collection("Notifications")]
public sealed class PasswordResetDeliveryEndpointTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{
    [Fact]
    public async Task ResetRequestByMobile_DeliversSms_NoInboxCopy_RedactedAfterSend()
    {
        var (client, customerId, mobile) = await RegisterAndSignInCustomerAsync();

        var reset = await client.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobile }, Json);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        await RunDeliveryPassAsync(); // expand
        await RunDeliveryPassAsync(); // deliver

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var log = await adminClient.GetAsync($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, log.StatusCode);

        var items = (await log.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.PasswordReset);
        Assert.DoesNotContain(items, i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.PasswordReset && i.GetProperty("channel").GetString() == "InApp");

        var resetItem = items.First(i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.PasswordReset);
        Assert.Equal("Sent", resetItem.GetProperty("status").GetString());

        var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{resetItem.GetProperty("id").GetGuid()}", Json);
        Assert.Equal("[redacted]", detail.GetProperty("body").GetString());

        // The Capturing sender actually received a message containing the reset link fragment.
        using var scope = Factory.Services.CreateScope();
        var sender = NotificationsApiFactory.CapturingSender(scope.ServiceProvider, NotificationChannel.Sms);
        Assert.Contains(sender.Sent, m => m.TextBody.Contains("/reset-password#token="));
    }

    /// <summary>
    /// I-B2 (untested before this pass): "the account save fails (inject a failure after enqueue)
    /// -&gt; Neither the token nor the request exists -- atomicity (F23)." <c>RequestPasswordReset
    /// CommandHandler</c> dispatches the notification (adds an untracked NotificationRequestAggregate
    /// to the scoped DbContext, per F23) BEFORE <c>tokenRepository.SaveAsync(token)</c> actually
    /// commits. A genuine save failure is forced here by dispatching the command directly with a
    /// <c>TokenId</c> that collides with an ALREADY-SAVED token's primary key (ids are minted
    /// client-side, `ValueGeneratedNever()`, matching every other aggregate in this codebase) --
    /// <c>SaveChangesAsync()</c> then fails on a real Postgres unique-violation, which must roll back
    /// EVERYTHING in that same unit of work, including the just-enqueued request.
    /// </summary>
    [Fact]
    public async Task AccountSaveFailsAfterEnqueue_NeitherTokenNorNotificationRequestExists()
    {
        var (_, customerAId, mobileA) = await RegisterAndSignInCustomerAsync();
        var (_, customerBId, mobileB) = await RegisterAndSignInCustomerAsync();
        var collidingTokenId = Guid.NewGuid();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.IInMemoryDispatcher>();
            var first = await dispatcher.SendAsync(new CatEcommerce.Application.Commands.Account.RequestPasswordResetCommand
            {
                TokenId = collidingTokenId, AccountKind = AccountKind.Customer, Identifier = mobileA,
                UserContext = new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()),
                CorrelationId = Guid.NewGuid(), ValidateCommand = true
            });
            Assert.True(first.IsSuccess);
        }

        await using var db1 = Factory.Services.CreateAsyncScope();
        var requestCountBeforeCollision = await db1.ServiceProvider.GetRequiredService<CatEcommerceDbContext>()
            .Set<NotificationRequestAggregate>().AsNoTracking().CountAsync(r => r.RecipientId == customerBId);
        Assert.Equal(0, requestCountBeforeCollision);

        // The colliding dispatch: customer B's reset, but reusing A's already-saved TokenId --
        // forces a genuine Postgres primary-key violation exactly at tokenRepository.SaveAsync,
        // AFTER the notification request for B has already been added (untracked) to this same
        // DbContext by the dispatcher above it in the handler.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.IInMemoryDispatcher>();
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => dispatcher.SendAsync(new CatEcommerce.Application.Commands.Account.RequestPasswordResetCommand
            {
                TokenId = collidingTokenId, AccountKind = AccountKind.Customer, Identifier = mobileB,
                UserContext = new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()),
                CorrelationId = Guid.NewGuid(), ValidateCommand = true
            }));
        }

        await using var db2 = Factory.Services.CreateAsyncScope();
        var context = db2.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var requestCountAfterCollision = await context.Set<NotificationRequestAggregate>().AsNoTracking().CountAsync(r => r.RecipientId == customerBId);
        var tokenCount = await context.PasswordResetTokens.AsNoTracking().CountAsync(t => t.Id == collidingTokenId);

        Assert.Equal(0, requestCountAfterCollision); // B's request never committed -- rolled back with the failed save
        Assert.Equal(1, tokenCount); // still only A's original token row, untouched
    }

    [Fact]
    public async Task ResetLinkAndToken_NeverAppearInAuditLog()
    {
        var (client, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        await client.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobile }, Json);

        await RunDeliveryPassAsync();
        await RunDeliveryPassAsync();

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var audit = await adminClient.GetAsync($"/api/admin/audit-entries?actorUserId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        var body = await audit.Content.ReadAsStringAsync();

        Assert.DoesNotContain("reset-password#token=", body);
    }

    /// <summary>
    /// HO-NTF-001 SECURITY gate fix (2026-09-27, HIGH 1). Proven end to end (SECURITY probes P1/P1b):
    /// a Reception user reading the log detail of a Queued or RetryPending reset notification --
    /// i.e. before it reaches a terminal status and gets persisted-redacted -- must never see the
    /// live token, in the body or in the redactedReason (which must still say the row is Secret).
    /// </summary>
    [Fact]
    public async Task LogDetail_OfQueuedOrRetryPendingResetNotification_NeverShowsTheLiveToken()
    {
        var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var mobileNormalised = "+880" + mobile[1..];

        await CreateClient().PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobileNormalised }, Json);

        Guid requestId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            requestId = await db.Set<NotificationRequestAggregate>().AsNoTracking()
                .Where(r => r.TemplateKey == NotificationTemplateCatalogue.PasswordReset && r.RecipientId == customerId)
                .Select(r => r.Id)
                .FirstAsync();
        }

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);

        async Task<Guid> ResetItemIdAsync()
        {
            var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
            return log.GetProperty("items").EnumerateArray().First(i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.PasswordReset).GetProperty("id").GetGuid();
        }

        // -- Plain Queued window: expanded, not yet claimed (SECURITY probe P1b). --
        await ExpandOnlyAsync(Factory, requestId);
        var queuedId = await ResetItemIdAsync();
        var queuedDetail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{queuedId}", Json);
        Assert.Equal("Queued", queuedDetail.GetProperty("status").GetString());
        Assert.Equal("[redacted]", queuedDetail.GetProperty("body").GetString());
        Assert.DoesNotContain("token=", queuedDetail.GetRawText());
        Assert.Equal("Secret", queuedDetail.GetProperty("redactedReason").GetString());

        // -- RetryPending window: a transient provider failure widens the window (SECURITY probe P1). --
        NotificationsApiFactory.ScriptOutcome(Factory.Services, NotificationChannel.Sms, mobileNormalised,
            new SendOutcome(SendOutcomeKind.TransientFailure, null, "timeout"));
        await RunDeliveryPassAsync(); // claim + send -> TransientFailure -> RetryPending

        var retryDetail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{queuedId}", Json);
        Assert.Equal("RetryPending", retryDetail.GetProperty("status").GetString());
        Assert.Equal("[redacted]", retryDetail.GetProperty("body").GetString());
        Assert.DoesNotContain("token=", retryDetail.GetRawText());
        Assert.Equal("Secret", retryDetail.GetProperty("redactedReason").GetString());

        // The search list itself never carries body/token either.
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        Assert.DoesNotContain("token=", log.GetRawText());
    }

    /// <summary>HO-NTF-001 SECURITY/CODE_REVIEW gate fix (2026-09-27, HIGH/BLOCKER 3): before
    /// <c>GetRequestsDueForVariableRedactionAsync</c> excluded already-redacted rows, a sweep with
    /// <c>BatchSize</c> capacity for only the OLDER expired secret request re-selected that same
    /// (already-redacted, no-op) row on every pass and never reached a newer one -- so the newer
    /// request's plaintext reset link stayed in <c>notification_request.variables</c> forever.
    /// Reproduces that exact starvation with <c>BatchSize=1</c> and two expired requests.</summary>
    [Fact]
    public async Task RedactionSweep_WithBatchSizeSmallerThanBacklog_StillReachesNewerRequest()
    {
        var fakeTime = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var smallBatchFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, cb) => cb.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Worker:BatchSize"] = "1"
            }));
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(fakeTime));
        });

        var (olderMobile, olderCustomerId) = await RegisterCustomerAsync(smallBatchFactory);
        await smallBatchFactory.CreateClient().PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = olderMobile }, Json);

        fakeTime.Advance(TimeSpan.FromMinutes(1));
        var (newerMobile, newerCustomerId) = await RegisterCustomerAsync(smallBatchFactory);
        await smallBatchFactory.CreateClient().PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = newerMobile }, Json);

        // Past both requests' 15-minute token lifetime -- both are now due for variable redaction.
        fakeTime.Advance(TimeSpan.FromMinutes(20));

        // Test-quality fix (2026-09-27, found while verifying the I-F5 care-reminder fix): the
        // two rows MUST be identified by their own RecipientId, not by "the last two globally, by
        // RequestedOn" -- this is a shared, persistent database (§16.1), and this suite runs
        // multiple xUnit collections in PARALLEL (only tests within the SAME named collection are
        // serialized). A concurrently-running test in a DIFFERENT collection using its own
        // FakeTimeProvider (e.g. SafeDaytimeInstant()'s `real now + 12 hours` branch) can create a
        // PasswordReset row with a RequestedOn far ahead of this test's own two rows, so
        // `requests[^2]`/`requests[^1]` silently grabbed a completely unrelated row instead.
        Guid olderRequestId, newerRequestId;
        await using (var scope = smallBatchFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            olderRequestId = await db.Set<NotificationRequestAggregate>().AsNoTracking()
                .Where(r => r.TemplateKey == NotificationTemplateCatalogue.PasswordReset && r.RecipientId == olderCustomerId)
                .Select(r => r.Id)
                .SingleAsync();
            newerRequestId = await db.Set<NotificationRequestAggregate>().AsNoTracking()
                .Where(r => r.TemplateKey == NotificationTemplateCatalogue.PasswordReset && r.RecipientId == newerCustomerId)
                .Select(r => r.Id)
                .SingleAsync();
        }

        // With BatchSize=1, the pre-fix code re-selects only olderRequestId forever, however many
        // passes run. Enough passes to drain the WHOLE due-for-redaction backlog that exists at this
        // instant (this is a shared, persistent database, §16.1 -- other tests' own expired secret
        // requests count too) guarantees both of THIS test's rows are reached, deterministically,
        // without guessing a fixed pass count that could be too small on a busy full-suite run.
        int dueBacklogCount;
        await using (var scope = smallBatchFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var now = fakeTime.GetUtcNow();
            dueBacklogCount = await db.Set<NotificationRequestAggregate>().AsNoTracking()
                .CountAsync(r => r.ContainsSecret && r.VariablesRedactedOn == null && r.NotAfter != null && r.NotAfter < now);
        }

        for (var i = 0; i < dueBacklogCount; i++)
            await RunDeliveryPassAsync(smallBatchFactory);

        await using var finalScope = smallBatchFactory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        var older = await finalDb.Set<NotificationRequestAggregate>().AsNoTracking().FirstAsync(r => r.Id == olderRequestId);
        var newer = await finalDb.Set<NotificationRequestAggregate>().AsNoTracking().FirstAsync(r => r.Id == newerRequestId);

        Assert.NotNull(older.VariablesRedactedOn);
        Assert.All(older.Variables.Values, v => Assert.Equal("[redacted]", v));

        Assert.NotNull(newer.VariablesRedactedOn); // the case the pre-fix code never reached
        Assert.All(newer.Variables.Values, v => Assert.Equal("[redacted]", v));
    }

    /// <summary>Registers a fresh customer directly against <paramref name="targetFactory"/> (rather
    /// than the base <c>RegisterAndSignInCustomerAsync</c>, which always uses <c>Factory</c>) --
    /// needed so the whole flow shares the same <see cref="FakeTimeProvider"/> instance.</summary>
    private async Task<(string Mobile, Guid CustomerId)> RegisterCustomerAsync(WebApplicationFactory<Program> targetFactory)
    {
        using var client = targetFactory.CreateClient();
        var captchaResponse = await client.PostAsync("/api/captcha", null);
        captchaResponse.EnsureSuccessStatusCode();
        var captchaId = (await captchaResponse.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("captchaId").GetString()!;

        var mobile = NewMobile();
        var register = await client.PostAsJsonAsync("/api/customers", new
        {
            fullName = "Test Customer", mobileNumber = mobile, password = "correcthorsebattery", captchaId, captchaAnswer = "TEST1"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var customerId = (await register.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("customerId").GetGuid();

        return (mobile, customerId);
    }

    /// <summary>HO-NTF-001 §16, I-B16 (untested before this fix): with no provider configured
    /// (ADR-061's default production state), a reset ends Undeliverable -- not stuck, not crashed --
    /// and no token ever appears in the log detail, matching the SECURITY HIGH 1 fix's guarantee
    /// for a terminal, non-Sent status too.</summary>
    [Fact]
    public async Task UnconfiguredSenders_ResetEndsUndeliverable_NoTokenAnywhere()
    {
        var unconfiguredFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, cb) => cb.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Channels:Sms:Sender"] = "Unconfigured",
                ["Notifications:Channels:Email:Sender"] = "Unconfigured"
            })));

        var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        await unconfiguredFactory.CreateClient().PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobile }, Json);

        await RunDeliveryPassAsync(unconfiguredFactory); // expand
        await RunDeliveryPassAsync(unconfiguredFactory); // deliver -> PermanentFailure -> Undeliverable

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var log = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications?recipientId={customerId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}", Json);
        var resetItem = log.GetProperty("items").EnumerateArray().First(i => i.GetProperty("templateKey").GetString() == NotificationTemplateCatalogue.PasswordReset);
        Assert.Equal("Undeliverable", resetItem.GetProperty("status").GetString());

        var detail = await adminClient.GetFromJsonAsync<JsonElement>($"/api/admin/notifications/{resetItem.GetProperty("id").GetGuid()}", Json);
        Assert.Equal("[redacted]", detail.GetProperty("body").GetString());
        Assert.DoesNotContain("token=", detail.GetRawText());
        Assert.DoesNotContain("token=", log.GetRawText());
    }
}
