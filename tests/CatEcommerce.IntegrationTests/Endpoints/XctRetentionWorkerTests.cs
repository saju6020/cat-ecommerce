using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Background;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Retention;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-XCT-001 section 16 C -- the retention run, purge, redaction and review end to end. Serialised with the erasure tests (<c>XctRetention</c>) because a
/// daily pass acts on EVERY due row in the shared branch database. Every case seeds its own rows and asserts only on their ids (rule 9); a run left
/// <c>Requested</c> by one case is driven to completion by the next (the table is protected and can never be cleaned up, rule 10).
/// </summary>
[Collection("XctRetention")]
public sealed class XctRetentionWorkerTests(XctApiFactory factory) : XctIntegrationTestBase(factory)
{
    private static async Task<T> WithCycleAsync<T>(WebApplicationFactory<Program> app, Func<RetentionWorkCycle, RetentionWorkQueue, Task<T>> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<RetentionWorkCycle>(), scope.ServiceProvider.GetRequiredService<RetentionWorkQueue>());
    }

    /// <summary>Finishes any run another case left <c>Requested</c>, so this case's own run is not blocked by it.</summary>
    private static Task DrainRequestedRunsAsync(WebApplicationFactory<Program> app) =>
        WithCycleAsync(app, async (cycle, queue) =>
        {
            while (await queue.GetRequestedRunIdAsync(CancellationToken.None) is { } requested)
                await cycle.RunDailyPassesAsync(RetentionRunTrigger.Manual, requested);
            return 0;
        });

    private static async Task<Guid> RunDailyAsync(WebApplicationFactory<Program> app)
    {
        await DrainRequestedRunsAsync(app);
        var runId = await WithCycleAsync(app, (cycle, _) => cycle.RunDailyPassesAsync(RetentionRunTrigger.Scheduled));
        Assert.NotNull(runId);
        return runId!.Value;
    }

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, response.StatusCode + " " + await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>(Json);
    }

    private static JsonElement Pass(JsonElement run, string kind) =>
        run.GetProperty("passes").EnumerateArray().Single(p => p.GetProperty("kind").GetString() == kind);

    private async Task<Guid> SeedCustomerSessionAsync(Guid customerId, DateTimeOffset issuedOn, DateTimeOffset expiresOn, DateTimeOffset? endedOn)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerSessionAggregate>>();
        var session = new CustomerSessionAggregate(Guid.NewGuid());
        session.Issue(new SessionIssueDto(customerId, "hash-" + Guid.NewGuid().ToString("N"), Guid.NewGuid(), issuedOn, expiresOn, null, null, AnyActor(), Guid.NewGuid(), "csrf-" + Guid.NewGuid().ToString("N")));
        if (endedOn is not null) session.End(new SessionEndDto(endedOn.Value, SessionEndReason.SignedOut, AnyActor(), Guid.NewGuid()));
        await sessions.SaveAsync(session, EventPublishOption.InMemoryPublishOnly);
        return session.Id;
    }

    private async Task<Guid> SeedInboxNotificationAsync(Guid customerId, DateTimeOffset createdOn)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<NotificationAggregate>>();
        var notification = new NotificationAggregate(Guid.NewGuid());
        notification.CreateInbox(new NotificationCreateDto(
            Guid.NewGuid(), "care.vaccination-due", 0, "en", NotificationCategory.Transactional, NotificationUrgency.Normal, false,
            NotificationRecipientKind.Customer, customerId, null, null, null, null, null, NotificationChannel.InApp, null,
            "Subject text", "Title text", "Body text with a cat name: Mitu", null, "/inbox", null, createdOn, null, false, null, null, null, null, null));
        await repository.SaveAsync(notification, EventPublishOption.InMemoryPublishOnly);
        return notification.Id;
    }

    private async Task<Guid> SeedCancelledSmsAsync(Guid customerId, DateTimeOffset createdOn)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<NotificationAggregate>>();
        var notification = new NotificationAggregate(Guid.NewGuid());
        notification.CreateExternal(new NotificationCreateDto(
            Guid.NewGuid(), "care.vaccination-due", 0, "en", NotificationCategory.Transactional, NotificationUrgency.Normal, false,
            NotificationRecipientKind.Customer, customerId, null, null, null, null, null, NotificationChannel.Sms, "+8801700000000",
            null, null, "Your cat Mitu is due", null, null, null, createdOn, null, false, null, null, null, null, null));
        notification.Cancel(NotificationCloseReason.RecipientOptedOut, createdOn);
        await repository.SaveAsync(notification, EventPublishOption.InMemoryPublishOnly);
        return notification.Id;
    }

    private Task<int> CountRowsAsync(string table, Guid id) =>
        ScalarAsync<int>($"SELECT COUNT(*)::int FROM {table} WHERE \"Id\" = @id", ("id", id));

    // I-C1
    [Fact]
    public async Task I_C1_TheDailyRun_PurgesSessionsPastTheSecurityPeriod_KeepsRecentOnes_AndAuditsThePurge()
    {
        var (_, customerId, _, _) = await RegisterAndSignInCustomerAsync();
        var now = DateTimeOffset.UtcNow;
        var old = await SeedCustomerSessionAsync(customerId, now.AddDays(-41), now.AddDays(-40), now.AddDays(-40));
        var recent = await SeedCustomerSessionAsync(customerId, now.AddDays(-11), now.AddDays(-10), now.AddDays(-10));
        var purgeRowsBefore = await ScalarAsync<int>("SELECT COUNT(*)::int FROM audit.audit_entry WHERE \"Action\" = 'RetentionPurge'");

        await RunDailyAsync(Factory);

        Assert.Equal(0, await CountRowsAsync("account.customer_session", old));
        Assert.Equal(1, await CountRowsAsync("account.customer_session", recent));
        Assert.True(await ScalarAsync<int>("SELECT COUNT(*)::int FROM audit.audit_entry WHERE \"Action\" = 'RetentionPurge'") > purgeRowsBefore);
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*)::int FROM account.customer_account WHERE \"Id\" = @id", ("id", customerId)));
    }

    // I-C2
    [Fact]
    public async Task I_C2_TheDailyRun_RedactsNotificationContentPastTheContentPeriod_KeepsMetadata_AndLeavesYoungOnesAlone()
    {
        var (_, customerId, _, _) = await RegisterAndSignInCustomerAsync();
        var now = DateTimeOffset.UtcNow;
        var oldInbox = await SeedInboxNotificationAsync(customerId, now.AddMonths(-25));
        var oldSms = await SeedCancelledSmsAsync(customerId, now.AddMonths(-25));
        var young = await SeedInboxNotificationAsync(customerId, now.AddMonths(-1));

        await RunDailyAsync(Factory);

        var oldRows = await WithDbAsync(db => db.Notifications.AsNoTracking().Where(n => n.Id == oldInbox || n.Id == oldSms).ToListAsync());
        Assert.Equal(2, oldRows.Count);
        Assert.All(oldRows, n =>
        {
            Assert.Equal("[redacted]", n.Body);
            Assert.Null(n.Destination);
            Assert.Equal(NotificationRedactionReason.Retention, n.RedactedReason);
            Assert.Equal(customerId, n.RecipientId);          // metadata kept
            Assert.Equal("care.vaccination-due", n.TemplateKey);
        });
        Assert.Equal(NotificationStatus.Cancelled, oldRows.Single(n => n.Id == oldSms).Status);

        var youngRow = await WithDbAsync(db => db.Notifications.AsNoTracking().SingleAsync(n => n.Id == young));
        Assert.Contains("Mitu", youngRow.Body);
        Assert.Null(youngRow.RedactedReason);
    }

    /// <summary>HO-BRD-001 registers the real <c>boarding-media</c> purge source, so a test that stands in for it replaces that registration instead of adding a second source with the same key.</summary>
    private static IServiceCollection ReplaceBoardingMediaSource(IServiceCollection services, IRetentionPurgeSource replacement)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IRetentionPurgeSource) && d.ImplementationType == typeof(CatEcommerce.Infrastructure.Boarding.BoardingMediaPurgeSource)).ToList())
            services.Remove(descriptor);

        return services.AddSingleton<IRetentionPurgeSource>(replacement);
    }

    // I-C4 (and the pass result of I-C6)
    [Fact]
    public async Task I_C4_ARegisteredPurgeSource_IsCalledWithItsOwnCutoff_AndItsResultIsOnThePass()
    {
        var source = new RecordingPurgeSource("boarding-media", new PurgeResult(Examined: 4, Purged: 3, Held: 1, Failed: 0));
        var app = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => ReplaceBoardingMediaSource(services, source)));
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);

        var runId = await RunDailyAsync(app);

        var cutoff = Assert.Single(source.Cutoffs.Distinct());
        var expected = DateTimeOffset.UtcNow.AddMonths(-12);
        Assert.InRange((cutoff - expected).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(5));
        var pass = Pass(await ReadAsync(await admin.GetAsync($"/api/admin/retention/runs/{runId}")), "PurgeSources");
        Assert.Equal(3, pass.GetProperty("affected").GetInt32());
        Assert.Equal(1, pass.GetProperty("held").GetInt32());
        Assert.Equal(4, pass.GetProperty("examined").GetInt32());
    }

    [Fact]
    public async Task APurgeSourceWithNoRetentionRule_IsNeverScheduled()
    {
        var unruled = new RecordingPurgeSource("stored-file:LabResult", new PurgeResult(1, 1, 0, 0));
        var app = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IRetentionPurgeSource>(unruled)));

        await RunDailyAsync(app);

        Assert.Empty(unruled.Cutoffs);
    }

    // U-C3
    [Fact]
    public async Task U_C3_AThrowingSourceAndAThrowingReviewSource_AreCountedAgainstTheirPasses_TheRestStillRun_AndTheRunIsCompletedWithErrors()
    {
        var app = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            ReplaceBoardingMediaSource(services, new ThrowingPurgeSource("boarding-media"));
            services.AddSingleton<IRetentionReviewSource>(new ThrowingReviewSource());
        }));
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);
        var (_, customerId, _, _) = await RegisterAndSignInCustomerAsync();
        var oldInbox = await SeedInboxNotificationAsync(customerId, DateTimeOffset.UtcNow.AddMonths(-30));

        var runId = await RunDailyAsync(app);

        var run = await ReadAsync(await admin.GetAsync($"/api/admin/retention/runs/{runId}"));
        Assert.Equal("CompletedWithErrors", run.GetProperty("status").GetString());
        Assert.Equal(7, run.GetProperty("passes").GetArrayLength());
        Assert.Equal(1, Pass(run, "PurgeSources").GetProperty("failed").GetInt32());
        Assert.Equal(1, Pass(run, "ReviewCounts").GetProperty("failed").GetInt32());
        Assert.Contains("UnhandledException", Pass(run, "PurgeSources").GetProperty("errorCodes").EnumerateArray().Select(c => c.GetString()));
        // The pass after the failing one still ran.
        var redacted = await WithDbAsync(db => db.Notifications.AsNoTracking().SingleAsync(n => n.Id == oldInbox));
        Assert.Equal("[redacted]", redacted.Body);
    }

    // I-C5
    [Fact]
    public async Task I_C5_AManualRunRequestedTwice_Is201ThenAConflict_AndTheWorkerCompletesIt()
    {
        await DrainRequestedRunsAsync(Factory);
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);
        var (reception, _) = await SignedInStaffAsync(StaffRoles.Reception);

        var first = await admin.PostAsync("/api/admin/retention/runs", null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Requested", created.GetProperty("status").GetString());
        var runId = created.GetProperty("retentionRunId").GetGuid();

        var second = await admin.PostAsync("/api/admin/retention/runs", null);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("RetentionRunAlreadyInProgress", await second.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsync("/api/admin/retention/runs", null)).StatusCode);

        // The worker picks up the Requested run (it does not request another one) ...
        await WithCycleAsync(Factory, async (cycle, queue) =>
        {
            Assert.Equal(runId, await queue.GetRequestedRunIdAsync(CancellationToken.None));
            return await cycle.RunDailyPassesAsync(RetentionRunTrigger.Manual, runId);
        });
        var run = await ReadAsync(await admin.GetAsync($"/api/admin/retention/runs/{runId}"));
        Assert.Equal("Manual", run.GetProperty("trigger").GetString());
        Assert.NotEqual("Requested", run.GetProperty("status").GetString());

        // ... and with nothing in progress a new request is accepted again.
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsync("/api/admin/retention/runs", null)).StatusCode);
        await DrainRequestedRunsAsync(Factory);
    }

    // I-C6
    [Fact]
    public async Task I_C6_ARunDetail_ListsEveryPassWithCounts_AndTheRunSucceeds()
    {
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);

        var runId = await RunDailyAsync(Factory);

        var run = await ReadAsync(await admin.GetAsync($"/api/admin/retention/runs/{runId}"));
        Assert.Equal("Succeeded", run.GetProperty("status").GetString());
        Assert.Equal("Scheduled", run.GetProperty("trigger").GetString());
        var kinds = run.GetProperty("passes").EnumerateArray().Select(p => p.GetProperty("kind").GetString()).ToList();
        Assert.Equal(["PurgeSessions", "PurgeResetTokens", "PurgeWebVitals", "PurgeBackupRuns", "PurgeSources", "RedactNotifications", "ReviewCounts"], kinds);
        Assert.All(run.GetProperty("passes").EnumerateArray(), p => Assert.Equal(0, p.GetProperty("failed").GetInt32()));

        var list = await ReadAsync(await admin.GetAsync("/api/admin/retention/runs?pageNumber=1&pageSize=5"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), r => r.GetProperty("retentionRunId").GetGuid() == runId);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/admin/retention/runs/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(1, await ScalarAsync<int>(
            "SELECT COUNT(*)::int FROM audit.audit_entry WHERE \"Action\" = 'RetentionRunFinished' AND \"EntityId\" = @id", ("id", runId)));
    }

    // I-C7 + U-C6
    [Fact]
    public async Task I_C7_U_C6_TheReviewReportCountsADeceasedCatPastTheClinicalPeriod_AndSaysNothingIsDeletedAutomatically()
    {
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);
        var (_, ownerId, _, _) = await RegisterAndSignInCustomerAsync();
        var before = await CatClinicalCountAsync(admin);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var longGone = await SeedDeceasedCatAsync(ownerId, born: today.AddYears(-12), died: today.AddYears(-8), today);
        var recentlyGone = await SeedDeceasedCatAsync(ownerId, born: today.AddYears(-12), died: today.AddYears(-3), today);
        var living = await SeedCatAsync(ownerId);

        var after = await CatClinicalCountAsync(admin);

        Assert.Equal(before.Count + 1, after.Count);          // only the cat deceased 8 years ago
        Assert.True(after.Oldest <= today.AddYears(-8));
        var report = await ReadAsync(await admin.GetAsync("/api/admin/retention/review"));
        Assert.Equal("Eligible for review. Nothing is deleted automatically.", report.GetProperty("note").GetString());
        foreach (var id in new[] { longGone, recentlyGone, living }) Assert.Equal(1, await CountRowsAsync("clinic.cat", id));
        Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInStaffAsync(StaffRoles.Reception)).Client.GetAsync("/api/admin/retention/review")).StatusCode);
    }

    private static async Task<(int Count, DateOnly Oldest)> CatClinicalCountAsync(HttpClient admin)
    {
        var report = await ReadAsync(await admin.GetAsync("/api/admin/retention/review"));
        var row = report.GetProperty("rows").EnumerateArray().FirstOrDefault(r =>
            r.GetProperty("sourceKey").GetString() == CatClinicalReviewSource.Key && r.GetProperty("recordKind").GetString() == "cat-profile");
        return row.ValueKind == JsonValueKind.Undefined
            ? (0, DateOnly.MaxValue)
            : (row.GetProperty("count").GetInt32(), DateOnly.Parse(row.GetProperty("oldestAnchor").GetString()!));
    }

    private async Task<Guid> SeedDeceasedCatAsync(Guid ownerId, DateOnly born, DateOnly died, DateOnly today)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CatAggregate>>();
        var cat = new CatAggregate(Guid.NewGuid());
        cat.Register(new CatRegistrationDto(ownerId, "Old Cat", "Cat", CatSex.Female, born, DateOfBirthPrecision.Exact, "Domestic Shorthair", "Tabby", today, AnyActor(), Guid.NewGuid()));
        cat.MarkDeceased(new CatDeceasedDto(died, null, today, AnyActor(), Guid.NewGuid()));
        await repository.SaveAsync(cat, EventPublishOption.InMemoryPublishOnly);
        return cat.Id;
    }

    // I-C8
    [Fact]
    public async Task I_C8_TheSchedule_ListsEveryClass_AndReflectsALoweredClinicalPeriodImmediately()
    {
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);

        var schedule = await ReadAsync(await admin.GetAsync("/api/admin/retention/schedule"));
        var classes = schedule.GetProperty("classes").EnumerateArray().ToList();
        Assert.Equal(Enumeration.GetAll<RecordClass>().Count(), classes.Count);
        var tableTotal = classes.Sum(c => c.GetProperty("tableCount").GetInt32());
        Assert.Equal(RecordClassification.All.Count, tableTotal);

        var clinical = classes.Single(c => c.GetProperty("recordClass").GetString() == "Clinical");
        Assert.Equal("ReviewOnly", clinical.GetProperty("actionAtEnd").GetString());
        Assert.Equal("Purge", classes.Single(c => c.GetProperty("recordClass").GetString() == "Security").GetProperty("actionAtEnd").GetString());
        var original = clinical.GetProperty("effectivePeriod").GetInt32();

        var changeTo = original == 7 ? 8 : 7;
        try
        {
            var change = await admin.PutAsJsonAsync("/api/admin/settings", new { changes = new[] { new { key = SettingKeys.RetentionClinicalYears, value = changeTo.ToString() } } }, Json);
            Assert.True(change.IsSuccessStatusCode, await change.Content.ReadAsStringAsync());

            var again = await ReadAsync(await admin.GetAsync("/api/admin/retention/schedule"));
            var updated = again.GetProperty("classes").EnumerateArray().Single(c => c.GetProperty("recordClass").GetString() == "Clinical");
            Assert.Equal(changeTo, updated.GetProperty("effectivePeriod").GetInt32());
            Assert.True(await ScalarAsync<int>(
                "SELECT COUNT(*)::int FROM audit.audit_entry WHERE \"EntityType\" LIKE '%Setting%' AND \"AfterValue\" LIKE @v", ("v", "%" + SettingKeys.RetentionClinicalYears + "%")) >= 1);
        }
        finally
        {
            await admin.PutAsJsonAsync("/api/admin/settings", new { changes = new[] { new { key = SettingKeys.RetentionClinicalYears, value = original.ToString() } } }, Json);
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await (await SignedInStaffAsync(StaffRoles.Reception)).Client.GetAsync("/api/admin/retention/schedule")).StatusCode);
    }

    // I-C9
    [Fact]
    public async Task I_C9_TheWorker_DeletesNothingProtected_ClinicalRowsSurviveEveryPass()
    {
        var (_, ownerId, _, _) = await RegisterAndSignInCustomerAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var oldCat = await SeedDeceasedCatAsync(ownerId, today.AddYears(-20), today.AddYears(-15), today);

        await RunDailyAsync(Factory);

        // Only this case's own rows (rule 9): the cat and its owner survive a full pass.
        Assert.Equal(1, await CountRowsAsync("clinic.cat", oldCat));
        Assert.Equal(1, await CountRowsAsync("account.customer_account", ownerId));

        // Negative control (CODE_REVIEW 18): "still there" is falsifiable -- a raw DELETE of the same cat is refused by the database, so the row survives
        // because the protection works and not merely because nobody tried.
        await using var connection = await OpenDatabaseAsync();
        await using var delete = new NpgsqlCommand("DELETE FROM clinic.cat WHERE \"Id\" = @id", connection);
        delete.Parameters.AddWithValue("id", oldCat);
        var refused = await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync());
        Assert.Equal("P0001", refused.SqlState);
        Assert.Equal(1, await CountRowsAsync("clinic.cat", oldCat));
    }
    [Fact]
    public async Task TheRetentionReads_AreRefusedToStaffWithoutTheAction_AndToAnonymousCallers()
    {
        var (reception, _) = await SignedInStaffAsync(StaffRoles.Reception);

        foreach (var path in new[] { "schedule", "runs", "review" })
            Assert.Equal(HttpStatusCode.Forbidden, (await reception.GetAsync($"/api/admin/retention/{path}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient().GetAsync("/api/admin/retention/schedule")).StatusCode);
    }

    private sealed class RecordingPurgeSource(string key, PurgeResult result) : IRetentionPurgeSource
    {
        private readonly List<DateTimeOffset> _cutoffs = [];
        public IReadOnlyList<DateTimeOffset> Cutoffs => _cutoffs;
        public string SourceKey => key;

        public Task<PurgeResult> PurgeAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct)
        {
            _cutoffs.Add(cutoff);
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingPurgeSource(string key) : IRetentionPurgeSource
    {
        public string SourceKey => key;
        public Task<PurgeResult> PurgeAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct) => throw new InvalidOperationException("boom");
    }

    private sealed class ThrowingReviewSource : IRetentionReviewSource
    {
        public string SourceKey => "throwing";
        public Task<IReadOnlyList<RetentionReviewCount>> CountPastRetentionAsync(DateOnly asOf, CancellationToken ct) => throw new InvalidOperationException("boom");
    }
}
