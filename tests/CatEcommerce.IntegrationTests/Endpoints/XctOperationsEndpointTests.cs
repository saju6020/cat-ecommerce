using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Background;
using CatEcommerce.Infrastructure.Retention;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-XCT-001 section 16 F -- backup status, restore tests and the backup-log purge. <c>ops.backup_run</c> is Purgeable (a log the backup job writes) so these
/// cases may clear and seed it; <c>ops.restore_test_record</c> is protected and is never cleaned up (rule 10), so each case asserts on its own ids.
/// </summary>
[Collection("XctRetention")]
public sealed class XctOperationsEndpointTests(XctApiFactory factory) : XctIntegrationTestBase(factory)
{
    private static async Task InsertRunAsync(string kind, TimeSpan finishedAgo, bool succeeded = true, TimeSpan? walBehind = null, string repository = "repo1")
    {
        await using var connection = await OpenDatabaseAsync();
        await using var command = new NpgsqlCommand(
            "INSERT INTO ops.backup_run (id, kind, started_on, finished_on, succeeded, repository, backup_label, wal_archived_until) " +
            "VALUES (@id, @kind, @finished - interval '5 minutes', @finished, @ok, @repo, @label, @wal)", connection);
        var finished = DateTimeOffset.UtcNow - finishedAgo;
        command.Parameters.AddWithValue("id", Guid.NewGuid());
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("finished", finished);
        command.Parameters.AddWithValue("ok", succeeded);
        command.Parameters.AddWithValue("repo", repository);
        command.Parameters.AddWithValue("label", (object)(kind is "full" or "diff" ? "20261012-010000F" : DBNull.Value));
        command.Parameters.AddWithValue("wal", walBehind is null ? DBNull.Value : finished - walBehind.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ClearRunsAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        await using var command = new NpgsqlCommand("DELETE FROM ops.backup_run", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static object Drill(DateTimeOffset? failure = null, int recoveredMinutesBefore = 8, int restoreMinutes = 40, bool pass = true, Guid? supersedes = null)
    {
        var failureAt = failure ?? DateTimeOffset.UtcNow.AddHours(-2);
        var started = failureAt.AddMinutes(10);
        return new
        {
            performedOn = DateOnly.FromDateTime(DateTime.UtcNow), environment = "drill host, isolated", backupSetLabel = "20261012-010000F",
            simulatedFailureAt = failureAt, recoveredTo = failureAt.AddMinutes(-recoveredMinutesBefore),
            restoreStartedAt = started, restoreCompletedAt = started.AddMinutes(restoreMinutes),
            checks = new[] { new { name = "migrations-current", passed = true, detail = (string?)null }, new { name = "row-counts-nonzero", passed = pass, detail = "ok" } },
            drillOutput = "{\"note\":\"<script>alert(1)</script>\"}", notes = "quarterly", supersedesId = supersedes,
        };
    }

    // I-F1
    [Fact]
    public async Task I_F1_APassingDrill_IsRecorded_WithComputedNumbers_TheCallerAsPerformer_AndAnAuditRow()
    {
        var (admin, adminId) = await SignedInStaffAsync(StaffRoles.Administrator);

        var response = await admin.PostAsJsonAsync("/api/admin/operations/restore-tests", Drill(), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("Passed", created.GetProperty("outcome").GetString());
        Assert.Equal(8m, created.GetProperty("achievedRpoMinutes").GetDecimal());
        Assert.Equal(40m, created.GetProperty("achievedRtoMinutes").GetDecimal());
        var id = created.GetProperty("restoreTestId").GetGuid();
        Assert.Equal(adminId, await ScalarAsync<Guid>("SELECT performed_by_staff_id FROM ops.restore_test_record WHERE \"Id\" = @id", ("id", id)));
        Assert.True(await ScalarAsync<int>("SELECT COUNT(*)::int FROM audit.audit_entry WHERE \"EntityId\" = @id", ("id", id)) >= 1);

        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/operations/restore-tests/{id}", Json);
        Assert.Equal(2, detail.GetProperty("checks").GetArrayLength());
        Assert.Contains("<script>", detail.GetProperty("drillOutput").GetString());           // stored and returned as text
        var list = await admin.GetFromJsonAsync<JsonElement>("/api/admin/operations/restore-tests?pageNumber=1&pageSize=50", Json);
        Assert.Contains(list.GetProperty("items").EnumerateArray(), t => t.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task AFailingDrill_IsRecordedAsFailed_ACorrectionNamesWhatItSupersedes_AndBadRecordsAreRefused()
    {
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);

        var failed = await admin.PostAsJsonAsync("/api/admin/operations/restore-tests", Drill(recoveredMinutesBefore: 20), Json);
        var failedBody = await failed.Content.ReadFromJsonAsync<JsonElement>(Json);
        var failedId = failedBody.GetProperty("restoreTestId").GetGuid();
        Assert.Equal("Failed", failedBody.GetProperty("outcome").GetString());

        var correction = await admin.PostAsJsonAsync("/api/admin/operations/restore-tests", Drill(supersedes: failedId), Json);
        Assert.Equal(HttpStatusCode.Created, correction.StatusCode);
        var correctionId = (await correction.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("restoreTestId").GetGuid();
        var original = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/operations/restore-tests/{failedId}", Json);
        Assert.Equal(correctionId, original.GetProperty("supersededByIds")[0].GetGuid());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/operations/restore-tests", Drill(supersedes: Guid.NewGuid()), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/admin/operations/restore-tests", Drill(restoreMinutes: 0), Json)).StatusCode);
        var noChecks = await admin.PostAsJsonAsync("/api/admin/operations/restore-tests", new { performedOn = DateOnly.FromDateTime(DateTime.UtcNow), environment = "e", backupSetLabel = "b", drillOutput = "{}", checks = Array.Empty<object>() }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, noChecks.StatusCode);
        var future = await admin.PostAsJsonAsync("/api/admin/operations/restore-tests", new
        {
            performedOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3), environment = "e", backupSetLabel = "b", drillOutput = "{}",
            simulatedFailureAt = DateTimeOffset.UtcNow, recoveredTo = DateTimeOffset.UtcNow, restoreStartedAt = DateTimeOffset.UtcNow, restoreCompletedAt = DateTimeOffset.UtcNow.AddMinutes(1),
            checks = new[] { new { name = "c", passed = true } }
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);

        var (reception, _) = await SignedInStaffAsync(StaffRoles.Reception);
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsJsonAsync("/api/admin/operations/restore-tests", Drill(), Json)).StatusCode);
    }

    // I-F2 (adapted: ops.restore_test_record is protected, so "no test ever" exists only on a fresh database; the rule itself is U-F7)
    [Fact]
    public async Task I_F2_TheDueDate_IsConsistentWithTheLastPassedTest_AndNeverTestedMeansOverdueToday()
    {
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);

        var status = await admin.GetFromJsonAsync<JsonElement>("/api/admin/operations/backup-status", Json);

        var due = DateOnly.Parse(status.GetProperty("nextRestoreTestDueOn").GetString()!);
        var overdue = status.GetProperty("isRestoreTestOverdue").GetBoolean();
        if (status.GetProperty("lastPassedRestoreTestOn").ValueKind == JsonValueKind.Null)
        {
            Assert.True(overdue);
            Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), due, new DateOnlyWithinADay());
        }
        else
        {
            var last = DateOnly.Parse(status.GetProperty("lastPassedRestoreTestOn").GetString()!);
            Assert.Equal(last.AddDays(92), due);                       // operations.restoreTestIntervalDays default
        }

        Assert.Equal(15, status.GetProperty("objectives").GetProperty("rpoMinutes").GetInt32());
        Assert.Equal(240, status.GetProperty("objectives").GetProperty("rtoMinutes").GetInt32());
    }

    private sealed class DateOnlyWithinADay : IEqualityComparer<DateOnly>
    {
        public bool Equals(DateOnly x, DateOnly y) => Math.Abs(x.DayNumber - y.DayNumber) <= 1;
        public int GetHashCode(DateOnly obj) => 0;
    }

    // I-F4
    [Fact]
    public async Task I_F4_ADiffTwentySevenHoursOld_IsStale_AFreshOneIsNot_AndTheArchiveFlagFollowsTheWal()
    {
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);
        await ClearRunsAsync();
        try
        {
            await InsertRunAsync("full", TimeSpan.FromHours(100));
            await InsertRunAsync("diff", TimeSpan.FromHours(27));
            await InsertRunAsync("archive-check", TimeSpan.FromMinutes(5), walBehind: TimeSpan.FromMinutes(1));
            var stale = await admin.GetFromJsonAsync<JsonElement>("/api/admin/operations/backup-status", Json);
            Assert.True(stale.GetProperty("isBackupStale").GetBoolean());
            Assert.False(stale.GetProperty("isArchiveStale").GetBoolean());
            Assert.Equal("repo1", stale.GetProperty("lastDiff").GetProperty("repository").GetString());
            Assert.NotEqual(JsonValueKind.Null, stale.GetProperty("walArchivedUntil").ValueKind);

            await InsertRunAsync("diff", TimeSpan.FromHours(1));
            Assert.False((await admin.GetFromJsonAsync<JsonElement>("/api/admin/operations/backup-status", Json)).GetProperty("isBackupStale").GetBoolean());

            await ClearRunsAsync();
            await InsertRunAsync("diff", TimeSpan.FromHours(1));
            await InsertRunAsync("archive-check", TimeSpan.FromMinutes(5), walBehind: TimeSpan.FromMinutes(40));           // WAL not shipped for 40 minutes
            Assert.True((await admin.GetFromJsonAsync<JsonElement>("/api/admin/operations/backup-status", Json)).GetProperty("isArchiveStale").GetBoolean());

            await ClearRunsAsync();
            Assert.True((await admin.GetFromJsonAsync<JsonElement>("/api/admin/operations/backup-status", Json)).GetProperty("isBackupStale").GetBoolean());   // never ran
        }
        finally
        {
            await ClearRunsAsync();
        }
    }

    // I-F5
    [Fact]
    public async Task I_F5_OnlyAnAdministrator_SeesBackupStatus()
    {
        var (accountant, _) = await SignedInStaffAsync(StaffRoles.Accountant);
        var (admin, _) = await SignedInStaffAsync(StaffRoles.Administrator);

        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("/api/admin/operations/backup-status")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await accountant.GetAsync("/api/admin/operations/restore-tests")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/operations/backup-status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient(Factory).GetAsync("/api/admin/operations/backup-status")).StatusCode);
    }

    [Fact]
    public async Task TheDailyRun_PurgesBackupLogRowsPastFourHundredDays_AndKeepsRecentOnes()
    {
        await InsertRunAsync("diff", TimeSpan.FromDays(401));
        await InsertRunAsync("diff", TimeSpan.FromDays(1));
        var oldId = await ScalarAsync<Guid>("SELECT id FROM ops.backup_run ORDER BY finished_on ASC LIMIT 1");
        var youngId = await ScalarAsync<Guid>("SELECT id FROM ops.backup_run ORDER BY finished_on DESC LIMIT 1");

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<RetentionWorkQueue>();
            var cycle = scope.ServiceProvider.GetRequiredService<RetentionWorkCycle>();
            while (await queue.GetRequestedRunIdAsync(CancellationToken.None) is { } pending) await cycle.RunDailyPassesAsync(RetentionRunTrigger.Manual, pending);
            Assert.NotNull(await cycle.RunDailyPassesAsync(RetentionRunTrigger.Scheduled));
        }

        Assert.Equal(0, await ScalarAsync<int>("SELECT COUNT(*)::int FROM ops.backup_run WHERE id = @id", ("id", oldId)));
        Assert.Equal(1, await ScalarAsync<int>("SELECT COUNT(*)::int FROM ops.backup_run WHERE id = @id", ("id", youngId)));
        await ClearRunsAsync();
    }
}
