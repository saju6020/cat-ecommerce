using System.Net;
using System.Net.Http.Json;
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
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-NTF-001 CODE_REVIEW gate fix (2026-09-27, second pass, HIGH #2). The §1 Amended row claimed
/// I-B11/I-B12 were "covered by the existing claim-store tests, re-verified" -- no such test existed
/// anywhere in the codebase; the only claim tests were domain-level <c>NotificationAggregate.Claim</c>
/// unit tests, which never exercise <c>EfNotificationClaimStore</c> or a real <c>xmin</c> conflict.
/// This file is that missing coverage, plus the other named gaps this gate asked for: I-B6
/// (backoff), I-B13 (concurrent dedup) and I-B14 (anonymised recipient suppressed).
/// </summary>
[Collection("Notifications")]
public sealed class NotificationClaimAndDeliveryEndpointTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{
    // Matches RegisterAndSignInCustomerAsync's own hardcoded password (see
    // NotificationLogAndInboxEndpointTests' own copy of this constant).
    private const string CustomerPassword = "correcthorsebattery";

    private static UserContext TestUserContext() => new(Guid.NewGuid(), Guid.NewGuid());

    private (WebApplicationFactory<Program> Factory, FakeTimeProvider Time) DaytimeFactory()
    {
        var time = new FakeTimeProvider(SafeDaytimeInstant());
        var daytimeFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(time)));
        return (daytimeFactory, time);
    }

    /// <summary>Queues a single external (Sms) password-reset notification -- Urgent, never
    /// deferred by quiet hours, exactly one external channel (no InApp inbox copy for this
    /// template) -- so the resulting row is unambiguous for direct claim-store manipulation.
    /// Dispatches ONLY <c>ExpandNotificationRequestCommand</c> directly, never
    /// <see cref="NotificationsIntegrationTestBase.RunDeliveryPassAsync"/>'s full pass: a full pass
    /// also runs the delivery loop in the SAME call, and an Urgent, never-deferred notification
    /// becomes due the instant it is Queued -- it would already be claimed and sent before this
    /// helper could return a genuinely still-Queued row for the concurrency tests below.
    /// <para>Test-quality fix (2026-09-27, found while verifying the I-F5 care-reminder fix):
    /// (1) the request lookup below filters by <paramref name="customerId"/>, not just
    /// "the globally most recent PasswordReset row" -- this is a shared, persistent database
    /// (§16.1). (2) The reset request is now posted through <paramref name="daytimeFactory"/>'s
    /// OWN client, not the base <c>Factory</c>'s -- posting through <c>Factory</c> (the real
    /// <c>TimeProvider</c>) computes the token's 15-minute <c>NotAfter</c> against REAL wall-clock
    /// time, while every later step here runs against <paramref name="daytimeFactory"/>'s
    /// <c>FakeTimeProvider</c> (<c>SafeDaytimeInstant()</c>) -- which, whenever the real UTC hour is
    /// NOT in [2, 15), is <c>real now + 12 hours</c>. That 12-hour gap dwarfs the 15-minute
    /// lifetime, so <c>ExpandOnlyAsync</c> saw an already-<c>Expired</c> request instead of
    /// expanding it -- reproducible deterministically, but only in the UTC hours where
    /// <c>SafeDaytimeInstant()</c> takes its `+12h` branch (confirmed live: real UTC 15:xx, request
    /// ended `Expired` with zero notifications). Both the request's own clock and every later
    /// step's clock must agree.</para></summary>
    private async Task<(Guid NotificationId, string DestinationMobile)> SeedQueuedResetAsync(WebApplicationFactory<Program> daytimeFactory, Guid customerId, string mobile)
    {
        var mobileNormalised = "+880" + mobile[1..];
        using var daytimeClient = daytimeFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            HandleCookies = true
        });
        var reset = await daytimeClient.PostAsJsonAsync("/api/auth/password-reset/request", new { identifier = mobileNormalised }, Json);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        Guid requestId;
        await using (var scope = daytimeFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            requestId = await db.Set<NotificationRequestAggregate>().AsNoTracking()
                .Where(r => r.TemplateKey == NotificationTemplateCatalogue.PasswordReset && r.RecipientId == customerId)
                .OrderByDescending(r => r.RequestedOn)
                .Select(r => r.Id)
                .SingleAsync();
        }

        await ExpandOnlyAsync(daytimeFactory, requestId);

        await using var assertScope = daytimeFactory.Services.CreateAsyncScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var notification = await assertDb.Set<NotificationAggregate>().AsNoTracking()
            .Where(n => n.TemplateKey == NotificationTemplateCatalogue.PasswordReset && n.Destination == mobileNormalised)
            .OrderByDescending(n => n.CreatedOn)
            .FirstAsync();
        Assert.Equal(NotificationStatus.Queued, notification.Status);

        return (notification.Id, mobileNormalised);
    }

    /// <summary>
    /// I-B11: two concurrent claims of the same notification -- exactly one wins, via <c>xmin</c>.
    /// Two separately-scoped repositories each load their own tracked copy of the SAME row (the same
    /// <c>xmin</c>), both call <c>Claim</c> in memory, and both try to save. Only the first save can
    /// possibly win; the second must see its <c>xmin</c> already moved and lose cleanly (<c>false</c>),
    /// never throwing <c>DbUpdateConcurrencyException</c> out to the caller.
    /// </summary>
    [Fact]
    public async Task TrySaveClaimAsync_TwoConcurrentClaims_ExactlyOneWins()
    {
        var (daytimeFactory, time) = DaytimeFactory();
        var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var (notificationId, _) = await SeedQueuedResetAsync(daytimeFactory, customerId, mobile);

        await using var scopeA = daytimeFactory.Services.CreateAsyncScope();
        await using var scopeB = daytimeFactory.Services.CreateAsyncScope();

        var repositoryA = scopeA.ServiceProvider.GetRequiredService<IAggregateRootRepository<NotificationAggregate>>();
        var repositoryB = scopeB.ServiceProvider.GetRequiredService<IAggregateRootRepository<NotificationAggregate>>();
        var claimStoreA = scopeA.ServiceProvider.GetRequiredService<INotificationClaimStore>();
        var claimStoreB = scopeB.ServiceProvider.GetRequiredService<INotificationClaimStore>();

        var copyA = await repositoryA.GetByIdAsync(notificationId);
        var copyB = await repositoryB.GetByIdAsync(notificationId);

        var now = time.GetUtcNow();
        copyA.Claim(now, TimeSpan.FromSeconds(120));
        copyB.Claim(now, TimeSpan.FromSeconds(120));

        var wonA = await claimStoreA.TrySaveClaimAsync(copyA);
        var wonB = await claimStoreB.TrySaveClaimAsync(copyB);

        Assert.True(wonA);
        Assert.False(wonB); // lost the xmin race cleanly, no exception propagated

        await using var assertScope = daytimeFactory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var final = await db.Set<NotificationAggregate>().AsNoTracking().FirstAsync(n => n.Id == notificationId);
        Assert.Equal(NotificationStatus.Sending, final.Status); // committed exactly once
    }

    /// <summary>
    /// I-B12: an expired lease can be re-claimed. After the lease committed by
    /// <see cref="TrySaveClaimAsync_TwoConcurrentClaims_ExactlyOneWins"/>'s winner expires,
    /// <c>CanClaim</c> allows a fresh claim (ADR-060's "crashed mid-send" recovery path), and it
    /// commits cleanly against the now-current <c>xmin</c> -- no lingering conflict from the
    /// original claim.
    /// </summary>
    [Fact]
    public async Task TrySaveClaimAsync_LeaseExpired_CanBeReClaimed()
    {
        var (daytimeFactory, time) = DaytimeFactory();
        var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var (notificationId, _) = await SeedQueuedResetAsync(daytimeFactory, customerId, mobile);

        await using (var scope = daytimeFactory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<NotificationAggregate>>();
            var claimStore = scope.ServiceProvider.GetRequiredService<INotificationClaimStore>();
            var notification = await repository.GetByIdAsync(notificationId);
            notification.Claim(time.GetUtcNow(), TimeSpan.FromSeconds(120));
            Assert.True(await claimStore.TrySaveClaimAsync(notification));
        }

        time.Advance(TimeSpan.FromSeconds(200)); // past the 120s lease

        await using (var scope = daytimeFactory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<NotificationAggregate>>();
            var claimStore = scope.ServiceProvider.GetRequiredService<INotificationClaimStore>();
            var notification = await repository.GetByIdAsync(notificationId);

            Assert.True(notification.CanClaim(time.GetUtcNow())); // the "crashed mid-send" path
            notification.Claim(time.GetUtcNow(), TimeSpan.FromSeconds(120));
            Assert.True(await claimStore.TrySaveClaimAsync(notification));
        }

        await using var assertScope = daytimeFactory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var final = await db.Set<NotificationAggregate>().AsNoTracking().FirstAsync(n => n.Id == notificationId);
        Assert.Equal(NotificationStatus.Sending, final.Status);
        Assert.True(final.LeaseExpiresOn > time.GetUtcNow().AddSeconds(-1)); // the NEW lease, not the expired one
    }

    /// <summary>
    /// A15's at-least-once half: a crash between claim and <c>RecordAttempt</c> leads to one
    /// eventual re-send, never zero. The claim below is committed exactly like C5's own
    /// (<c>Claim</c> then <c>TrySaveClaimAsync</c>) but the process is simulated as dying right
    /// there -- no send, no <c>RecordAttempt</c> ever runs for it. Once the lease expires, a real
    /// delivery pass re-claims and actually sends it: the message reaches the Capturing sender
    /// exactly once, and the notification reaches a terminal <c>Sent</c> status -- the crashed
    /// attempt was recovered, not lost, and not duplicated.
    /// </summary>
    [Fact]
    public async Task CrashBetweenClaimAndRecordAttempt_RecoversToExactlyOneSend()
    {
        var (daytimeFactory, time) = DaytimeFactory();
        var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var (notificationId, destinationMobile) = await SeedQueuedResetAsync(daytimeFactory, customerId, mobile);

        await using (var scope = daytimeFactory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<NotificationAggregate>>();
            var claimStore = scope.ServiceProvider.GetRequiredService<INotificationClaimStore>();
            var notification = await repository.GetByIdAsync(notificationId);
            notification.Claim(time.GetUtcNow(), TimeSpan.FromSeconds(120));
            Assert.True(await claimStore.TrySaveClaimAsync(notification));
            // Simulated crash: no SendAsync call, no RecordAttempt -- the process dies right here.
        }

        time.Advance(TimeSpan.FromSeconds(200)); // past the lease -- CanClaim's recovery path opens

        await RunDeliveryPassAsync(daytimeFactory); // the real C5 path: re-claims and actually sends

        var smsSender = NotificationsApiFactory.CapturingSender(daytimeFactory.Services, NotificationChannel.Sms);
        Assert.Single(smsSender.Sent, m => m.Destination == destinationMobile); // exactly one send, not zero, not two

        await using var assertScope = daytimeFactory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var final = await db.Set<NotificationAggregate>().AsNoTracking().FirstAsync(n => n.Id == notificationId);
        Assert.Equal(NotificationStatus.Sent, final.Status);
    }

    /// <summary>
    /// NTF-001 backlog row 5 -- a row whose claim keeps committing but whose outcome is never
    /// recorded (a save that fails after the claim) was re-claimed and re-sent every lease forever.
    /// After <c>MaxAttempts</c> such claims the real delivery pass must park it Undeliverable and
    /// send nothing further.
    /// </summary>
    [Fact]
    public async Task RepeatedCrashesAfterClaim_AreParkedAtTheCap_NotResentForever()
    {
        var (daytimeFactory, time) = DaytimeFactory();
        var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var (notificationId, destinationMobile) = await SeedQueuedResetAsync(daytimeFactory, customerId, mobile);

        for (var i = 0; i < NotificationRetryRules.MaxAttempts; i++)
        {
            await using var scope = daytimeFactory.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<NotificationAggregate>>();
            var claimStore = scope.ServiceProvider.GetRequiredService<INotificationClaimStore>();
            var notification = await repository.GetByIdAsync(notificationId);
            notification.Claim(time.GetUtcNow(), TimeSpan.FromSeconds(120));
            Assert.True(await claimStore.TrySaveClaimAsync(notification));
            time.Advance(TimeSpan.FromSeconds(125)); // lease (120s) lapses; the process "died" before RecordAttempt. 6 x 125s stays inside the reset's 15-minute expiry
        }

        await RunDeliveryPassAsync(daytimeFactory);
        await RunDeliveryPassAsync(daytimeFactory);

        var smsSender = NotificationsApiFactory.CapturingSender(daytimeFactory.Services, NotificationChannel.Sms);
        Assert.DoesNotContain(smsSender.Sent, m => m.Destination == destinationMobile);

        await using var assertScope = daytimeFactory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var final = await db.Set<NotificationAggregate>().AsNoTracking().FirstAsync(n => n.Id == notificationId);
        Assert.Equal(NotificationStatus.Undeliverable, final.Status);
        Assert.Equal(NotificationRetryRules.MaxAttempts, final.ClaimCount);
    }

    /// <summary>A test-only Urgent template -- never deferred by quiet hours, and (unlike
    /// account.password-reset) has no 15-minute request expiry -- because the backoff table's own
    /// total span (1+5+15+60+240 = 321 minutes) would otherwise run past a real reset token's
    /// lifetime, or (for a Normal-urgency shipped template) risk crossing into the 21:00-08:00 quiet
    /// window partway through, before all 6 attempts are exhausted.</summary>
    private static NotificationTemplateDefinition UrgentNoExpiryTestDefinition() => new(
        $"test.urgent-{Guid.NewGuid():N}", "Test urgent", "d", NotificationCategory.Transactional, NotificationUrgency.Urgent,
        [NotificationChannel.Sms], false,
        [new NotificationTemplateVariableDefinition("message", "d", TemplateVariableKind.Text, "Hi", [])],
        [new NotificationTemplateChannelDefault(NotificationChannel.Sms, null, null, "{{message}}", null)]);

    /// <summary>
    /// I-B6: retry backoff. Six consecutive transient failures follow the 1/5/15/60/240-minute
    /// table (<c>NotificationRetryRules</c>) and the sixth exhausts it -- <c>Undeliverable</c>, not
    /// a seventh <c>RetryPending</c>.
    /// </summary>
    [Fact]
    public async Task TransientFailures_FollowBackoffTable_ThenExhaustToUndeliverable()
    {
        var definition = UrgentNoExpiryTestDefinition();
        var (daytimeFactory, time) = DaytimeFactory();
        daytimeFactory = daytimeFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<INotificationTemplateCatalogue>(new StaticNotificationTemplateCatalogue([definition]))));

        var (_, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var mobileNormalised = "+880" + mobile[1..];

        for (var i = 0; i < NotificationRetryRules.MaxAttempts; i++)
        {
            NotificationsApiFactory.ScriptOutcome(daytimeFactory.Services, NotificationChannel.Sms, mobileNormalised,
                new SendOutcome(SendOutcomeKind.TransientFailure, null, "temporary provider error"));
        }

        await RequestNotificationAsync(daytimeFactory, definition.Key, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["message"] = "Hi" }, deduplicationKey: $"test:{Guid.NewGuid():N}");

        // Driven by the notification's OWN persisted state, not an assumed one-attempt-per-pass
        // cadence: the very first pass runs expand AND delivery together
        // (NotificationWorkCycle.RunDeliveryPassAsync), and an Urgent, never-deferred template
        // becomes due in that same pass, so it is not safe to assume which pass number produces
        // which attempt.
        var previousAttemptCount = 0;
        var maxPasses = NotificationRetryRules.MaxAttempts * 3; // safety margin, never a tight loop
        for (var pass = 0; pass < maxPasses; pass++)
        {
            await RunDeliveryPassAsync(daytimeFactory);

            await using var scope = daytimeFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var notification = await db.Set<NotificationAggregate>().AsNoTracking()
                .FirstAsync(n => n.TemplateKey == definition.Key && n.Destination == mobileNormalised);

            if (notification.AttemptCount > previousAttemptCount)
            {
                Assert.Equal(previousAttemptCount + 1, notification.AttemptCount); // never skips or double-counts an attempt
                previousAttemptCount = notification.AttemptCount;

                if (notification.Status != NotificationStatus.Undeliverable)
                {
                    Assert.Equal(NotificationStatus.RetryPending, notification.Status);
                    var expectedBackoff = NotificationRetryRules.BackoffFor(notification.AttemptCount);
                    var expectedNextAttempt = time.GetUtcNow() + expectedBackoff;
                    // A small tolerance, not exact equality: the handler reads
                    // TimeProvider.GetUtcNow() more than once per pass, and this assertion is not
                    // about clock precision.
                    Assert.True((notification.NextAttemptOn!.Value - expectedNextAttempt).Duration() < TimeSpan.FromSeconds(5),
                        $"Expected NextAttemptOn near {expectedNextAttempt:O}, was {notification.NextAttemptOn:O}.");
                }
            }

            if (notification.Status == NotificationStatus.Undeliverable)
            {
                Assert.Equal(NotificationRetryRules.MaxAttempts, notification.AttemptCount); // exhausted, not a 7th retry
                return;
            }

            // Queued but not yet claimed this pass (should not normally happen for an Urgent,
            // never-deferred template, but costs nothing to tolerate): nudge time forward slightly
            // and let the next pass pick it up, rather than dereferencing a null NextAttemptOn.
            time.Advance(notification.NextAttemptOn is { } nextAttemptOn
                ? (nextAttemptOn - time.GetUtcNow()) + TimeSpan.FromSeconds(1)
                : TimeSpan.FromSeconds(1));
        }

        Assert.Fail($"Notification never reached Undeliverable within the expected number of passes. Last seen attemptCount={previousAttemptCount}.");
    }

    /// <summary>
    /// I-B13: concurrent dedup. N concurrent <c>RequestNotificationCommand</c> dispatches carrying
    /// the SAME deduplication key race on <c>ix_notification_request_dedup</c>
    /// (<c>EfNotificationRequestStore</c>) -- exactly one request row is ever created, and every
    /// caller still gets back success (§11.4 C3: "the race was lost cleanly").
    /// </summary>
    [Fact]
    public async Task RequestNotification_ConcurrentSameDeduplicationKey_ExactlyOneRequestCreated()
    {
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var dedupKey = $"test:concurrent:{Guid.NewGuid():N}";
        var variables = new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" };

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
            return await dispatcher.SendAsync(new CatEcommerce.Application.Commands.Notification.RequestNotificationCommand
            {
                RequestId = Guid.NewGuid(), TemplateKey = NotificationTemplateCatalogue.VaccinationDue,
                RecipientKind = NotificationRecipientKind.Customer.DisplayName, RecipientId = customerId,
                Variables = variables, DeduplicationKey = dedupKey,
                UserContext = TestUserContext(), CorrelationId = Guid.NewGuid(), ValidateCommand = true
            });
        });

        var responses = await Task.WhenAll(tasks);
        Assert.All(responses, r => Assert.True(r.IsSuccess)); // every caller sees success, win or lose

        await using var assertScope = Factory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var count = await db.Set<NotificationRequestAggregate>().AsNoTracking().CountAsync(r => r.DeduplicationKey == dedupKey);
        Assert.Equal(1, count);
    }

    /// <summary>
    /// I-B14: an anonymised customer with a pending request is suppressed as RecipientInactive, not
    /// stuck Pending and not expanded into a notification that could deliver to a no-longer-active
    /// account.
    /// </summary>
    [Fact]
    public async Task ExpandRequest_RecipientAnonymisedSinceRequesting_SuppressedRecipientInactive()
    {
        var daytimeFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()))));

        var (client, customerId, _) = await RegisterAndSignInCustomerAsync();

        var command = await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");

        var deletion = await client.PostAsJsonAsync("/api/customers/me/deletion", new { currentPassword = CustomerPassword }, Json);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);

        await RunDeliveryPassAsync(daytimeFactory);

        await using var scope = daytimeFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var request = await db.Set<NotificationRequestAggregate>().AsNoTracking().FirstAsync(r => r.Id == command.RequestId);

        Assert.Equal(NotificationRequestStatus.Suppressed, request.Status);
        Assert.Equal(NotificationSuppressionReason.RecipientInactive, request.SuppressionReason);

        var notificationExists = await db.Set<NotificationAggregate>().AsNoTracking().AnyAsync(n => n.RequestId == command.RequestId);
        Assert.False(notificationExists); // nothing was ever sent to the anonymised account
    }

    /// <summary>
    /// TEST gate addition -- the retry/park rule's OTHER half. Every existing regression test
    /// (<c>QuietHoursEndpointTests.QuietHoursCalendarThrows...</c>) proves a non-<c>DbUpdateException</c>
    /// read failure never counts and leaves the request Pending. Nothing proved the converse: that a
    /// REAL <c>DbUpdateException</c> (here, <c>DbUpdateConcurrencyException</c> from a genuine
    /// <c>xmin</c> conflict -- the same real optimistic-concurrency column
    /// <c>NotificationRequestConfiguration</c> already carries) is a safe, correctly-classified event
    /// for the SAME request expanding twice at once: exactly one winner commits, the request ends
    /// Expanded exactly once (never double-expanded into four notifications), and a losing race
    /// either cleanly no-ops (idempotent re-check) or throws -- in neither case is data corrupted or
    /// duplicated. Does not itself drive <c>NotificationWorkCycle</c>'s specific recovery path (see
    /// the TEST gate verdict for the residual gap this leaves).
    /// </summary>
    [Fact]
    public async Task ConcurrentExpand_OfTheSameRequest_ExactlyOneExpansion_NeverDoubled()
    {
        var daytimeFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(SafeDaytimeInstant()))));
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync(NewEmail());

        var command = await RequestNotificationAsync(daytimeFactory, NotificationTemplateCatalogue.VaccinationDue, NotificationRecipientKind.Customer, customerId,
            new Dictionary<string, string> { ["catName"] = "Mitu", ["vaccineName"] = "Rabies", ["dueOn"] = "2026-10-12", ["bookingLink"] = "https://x/y" },
            deduplicationKey: $"test:{Guid.NewGuid():N}");

        async Task<Type?> ExpandOnceAsync()
        {
            await using var scope = daytimeFactory.Services.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
            try
            {
                await dispatcher.SendAsync(new CatEcommerce.Application.Commands.Notification.ExpandNotificationRequestCommand
                {
                    RequestId = command.RequestId, UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid()),
                    CorrelationId = Guid.NewGuid(), ValidateCommand = true
                });
                return null; // succeeded (winner, or a clean idempotent no-op if it lost the race but ran after the winner's commit)
            }
            catch (Exception ex)
            {
                return ex.GetType();
            }
        }

        var results = await Task.WhenAll(ExpandOnceAsync(), ExpandOnceAsync());

        // Whichever of the two failed, it must have failed with a real DbUpdateException (the
        // xmin conflict) -- never anything else, and never BOTH failing.
        Assert.All(results, r => Assert.True(r is null || typeof(DbUpdateException).IsAssignableFrom(r)));
        Assert.Contains(results, r => r is null); // at least one genuinely succeeded

        await using var assertScope = daytimeFactory.Services.CreateAsyncScope();
        var db = assertScope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var request = await db.Set<NotificationRequestAggregate>().AsNoTracking().SingleAsync(r => r.Id == command.RequestId);
        Assert.Equal(NotificationRequestStatus.Expanded, request.Status);

        // Exactly one expansion's worth of notifications -- vaccination-due has Sms/Email/InApp
        // wording, so Plan picks one external primary plus the always-on inbox copy (R6): 2 rows,
        // never 4 from a double expansion.
        var notificationCount = await db.Set<NotificationAggregate>().AsNoTracking().CountAsync(n => n.RequestId == command.RequestId);
        Assert.Equal(2, notificationCount);
    }
}
