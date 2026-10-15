using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Api.Background;
using CatEcommerce.Application.Commands.StoredFile;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Domain.Aggregates.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Reporting;
using CatEcommerce.Read.QueryHandlers.Reporting;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Infrastructure.Core.Commands;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-RPT-001 TEST gate (2026-10-13), T-I1 to T-I7 and T-I9 -- the fixes of the gate rounds, each proved by a case that FAILS when the fix is taken out (the mutation spot-checks are recorded in the
/// sheet): projected schedule reads, the store-then-C5 flow and its failure paths with no orphan file, R3 clinical access on a scheduled run and on run-now (failing closed), the per-account run-now limit
/// across an idle gap, the recipient re-check at send time for an all-branches schedule, and deferred sources that are absent rather than zero. In the schedule collection: the worker's pass runs over the
/// shared database.
/// </summary>
[Collection(ScheduleTestBase.CollectionName)]
public sealed class ReportingTestGateEndpointTests(ReportingApiFactory factory) : ScheduleTestBase(factory)
{
    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    /// <summary>Throws when a Generated run is about to be INSERTed -- after its notification requests were enlisted in the same save.</summary>
    private sealed class ThrowOnGeneratedRunInsert(Func<Exception> make) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<ScheduledReportRunAggregate>().Any(e => e.State == EntityState.Added && e.Entity.Status == ScheduledReportRunStatus.Generated))
                throw make();

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class FailingClinicalAccessHandler : ICommandHandlerAsync<RecordClinicalRecordAccessCommand>
    {
        public Task<CommandResponse> HandleAsync(RecordClinicalRecordAccessCommand command) => throw new InvalidOperationException("The audit store is down.");
    }

    /// <summary>The access write is REFUSED rather than thrown: a failed command response (for example a validation failure).</summary>
    private sealed class RefusedClinicalAccessHandler : ICommandHandlerAsync<RecordClinicalRecordAccessCommand>
    {
        public Task<CommandResponse> HandleAsync(RecordClinicalRecordAccessCommand command) => Task.FromResult(new CommandResponse { IsSuccess = false });
    }

    private static void WithClinicalAccessWrite(Microsoft.Extensions.DependencyInjection.IServiceCollection services, string shape)
    {
        if (shape == "throws") services.AddScoped<ICommandHandlerAsync<RecordClinicalRecordAccessCommand>, FailingClinicalAccessHandler>();
        else services.AddScoped<ICommandHandlerAsync<RecordClinicalRecordAccessCommand>, RefusedClinicalAccessHandler>();
    }

    private sealed class RegisterReturnsFailure : ICommandHandlerAsync<RegisterStoredFileCommand>
    {
        public Task<CommandResponse> HandleAsync(RegisterStoredFileCommand command) => Task.FromResult(new CommandResponse { IsSuccess = false });
    }

    private sealed class RegisterThrows : ICommandHandlerAsync<RegisterStoredFileCommand>
    {
        public Task<CommandResponse> HandleAsync(RegisterStoredFileCommand command) => throw new InvalidOperationException("The register is down.");
    }

    private int BlobsInTheFileStore()
    {
        var root = Factory.Services.GetRequiredService<IConfiguration>()["FileStorage:LocalStoreRoot"]!;
        return Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length : 0;
    }

    private Task<int> ReportExportRowsAsync() => WithDbAsync(db => db.StoredFiles.CountAsync(f => f.Purpose == FilePurpose.ReportExport));

    private async Task RunWorkerPassOnAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ReportingWorkCycle>().RunAsync();
    }

    private Task<int> ClinicalReadsAsync(Guid actorId) => AuditCountAsync("ClinicalRecordRead", actorId);

    /// <summary>True when the SQL reads <paramref name="column"/> of some table alias (EF writes `r.parameters`, or `AS "Parameters"` for a projection alias of a different name).</summary>
    private static bool SelectsColumn(string sql, string column) => System.Text.RegularExpressions.Regex.IsMatch(sql, $@"\.""?{System.Text.RegularExpressions.Regex.Escape(column)}""?(?![\w])");

    private static string ColumnOf(IEntityType entity, string property) =>
        entity.FindProperty(property)!.GetColumnName(StoreObjectIdentifier.Create(entity, StoreObjectType.Table)!.Value)!;

    // ── T-I1: the schedule reads project; they never load a schedule or a run whole ─────────────────────────
    [Fact]
    public async Task T_I1_TheScheduleReads_SelectOnlyTheColumnsTheyNeed()
    {
        var context = await CreateBillingContextAsync();
        var recipient = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [recipient.Id]));
        try
        {
            await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsync($"/api/admin/report-schedules/{created.ScheduleId}/run-now", null));

            await using var scope = Factory.Services.CreateAsyncScope();
            var recorder = new CommandRecorder();
            var baseOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<CatEcommerceDbContext>>();
            await using var db = new CatEcommerceDbContext(new DbContextOptionsBuilder<CatEcommerceDbContext>(baseOptions).AddInterceptors(recorder).Options);
            var catalogue = scope.ServiceProvider.GetRequiredService<IReportCatalogue>();

            var schedule = db.Model.FindEntityType(typeof(ReportScheduleAggregate))!;
            var run = db.Model.FindEntityType(typeof(ScheduledReportRunAggregate))!;
            var parametersColumn = ColumnOf(schedule, nameof(ReportScheduleAggregate.Parameters));
            var fileNameColumn = ColumnOf(run, nameof(ScheduledReportRunAggregate.FileName));
            var sha256Column = ColumnOf(run, nameof(ScheduledReportRunAggregate.ContentSha256));
            var timeZoneColumn = ColumnOf(run, nameof(ScheduledReportRunAggregate.TimeZoneId));

            string SqlOf() => string.Join("\n---\n", recorder.Commands);

            // The list: no parameters, and the whole-row load that a rehydrated aggregate would be is not there.
            var list = await new GetReportSchedulesQueryHandler(db, catalogue).HandleAsync(new GetReportSchedulesQuery { PageNumber = 1, PageSize = 20, CorrelationId = Guid.NewGuid() });
            Assert.NotEmpty(list.Result!.Items);
            Assert.False(SelectsColumn(SqlOf(), parametersColumn), $"a column the read does not need ({parametersColumn}) is selected: {SqlOf()}");
            Assert.False(SelectsColumn(SqlOf(), fileNameColumn), $"a column the read does not need ({fileNameColumn}) is selected: {SqlOf()}");
            recorder.Commands.Clear();

            // The runs: a run row's file name, hash and zone are not needed for the list of runs.
            var runs = await new GetReportScheduleRunsQueryHandler(db).HandleAsync(new GetReportScheduleRunsQuery { ScheduleId = created.ScheduleId, PageNumber = 1, PageSize = 20, CorrelationId = Guid.NewGuid() });
            Assert.Single(runs.Result!.Items);
            Assert.False(SelectsColumn(SqlOf(), fileNameColumn), $"a column the read does not need ({fileNameColumn}) is selected: {SqlOf()}");
            Assert.False(SelectsColumn(SqlOf(), sha256Column), $"a column the read does not need ({sha256Column}) is selected: {SqlOf()}");
            Assert.False(SelectsColumn(SqlOf(), timeZoneColumn), $"a column the read does not need ({timeZoneColumn}) is selected: {SqlOf()}");
            recorder.Commands.Clear();

            // The picker never reads the secrets of the staff accounts it lists.
            var eligible = await new GetEligibleReportRecipientsQueryHandler(db, catalogue).HandleAsync(
                new GetEligibleReportRecipientsQuery { ReportKey = ReportingFixtures.RowsKey, BranchId = context.BranchId, CorrelationId = Guid.NewGuid() });
            Assert.Contains(eligible.Result!, e => e.Id == recipient.Id);
            var pickerSql = SqlOf().ToLowerInvariant();
            Assert.DoesNotContain("password", pickerSql);
            Assert.DoesNotContain("hash", pickerSql);
            recorder.Commands.Clear();

            // Nothing the reads touched is left tracked (no aggregate was rehydrated for change tracking).
            Assert.Empty(db.ChangeTracker.Entries());
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── T-I2: a failure of the run save that is NOT the duplicate slot leaves nothing behind ─────────────────
    [Theory]
    [InlineData("anException")]
    [InlineData("aUniqueViolationOnAnotherConstraint")]
    public async Task T_I2_ASaveThatFailsForAnyReasonButTheDuplicateSlot_RecordsAFailedRun_AndLeavesNoRequestNoRegisterRowAndNoFile(string shape)
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var first = await SeedRecipientAsync(context, StaffRoles.Reception);
        var second = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [first.Id, second.Id]));
        try
        {
            Func<Exception> make = shape == "anException"
                ? () => new InvalidOperationException("The disk is full.")
                : () => new DbUpdateException("unique violation", new PostgresException("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation, constraintName: "ix_some_other_unique_index"));
            var interceptor = new ThrowOnGeneratedRunInsert(make);
            using var failing = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddDbContext<CatEcommerceDbContext>((sp, o) => o.AddInterceptors(interceptor))));

            var registerRowsBefore = await ReportExportRowsAsync();
            var blobsBefore = BlobsInTheFileStore();

            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunWorkerPassOnAsync(failing);

            var admin = await FreshAdminAsync(context);
            var run = Assert.Single(await RunsAsync(admin, created.ScheduleId));
            Assert.Equal("Failed", run.GetProperty("status").GetString());                    // a real failure is a Failed run, never "success with nothing written"
            Assert.Equal(ScheduledReportRunner.UnexpectedFailureCode, run.GetProperty("failureCode").GetString());
            var runId = run.GetProperty("runId").GetGuid();

            Assert.Empty(await RequestsForRunAsync(runId));                                     // the requests enlisted with the failed save did not survive it
            Assert.Equal(registerRowsBefore, await ReportExportRowsAsync());                    // no register row for the file
            Assert.Equal(blobsBefore, BlobsInTheFileStore());                                   // and no orphan object in the store
            Assert.Empty(EmailsTo(first.Email));
            Assert.Empty(EmailsTo(second.Email));
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── T-I3: the stored file when HO-FIL-001's register step fails (returns a failure, or throws) ────────────
    [Theory]
    [InlineData("returnsAFailure")]
    [InlineData("throws")]
    public async Task T_I3_WhenTheRegisterStepFails_TheStoredObjectIsRemoved_AndTheRunIsFailed(string shape)
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id]));
        try
        {
            using var failing = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            {
                if (shape == "returnsAFailure") s.AddScoped<ICommandHandlerAsync<RegisterStoredFileCommand>, RegisterReturnsFailure>();
                else s.AddScoped<ICommandHandlerAsync<RegisterStoredFileCommand>, RegisterThrows>();
            }));

            var registerRowsBefore = await ReportExportRowsAsync();
            var blobsBefore = BlobsInTheFileStore();

            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunWorkerPassOnAsync(failing);

            var admin = await FreshAdminAsync(context);
            var run = Assert.Single(await RunsAsync(admin, created.ScheduleId));
            Assert.Equal("Failed", run.GetProperty("status").GetString());
            Assert.Equal(shape == "returnsAFailure" ? BusinessViolationCodes.ReportFileNotStored : ScheduledReportRunner.UnexpectedFailureCode, run.GetProperty("failureCode").GetString());

            Assert.Equal(registerRowsBefore, await ReportExportRowsAsync());
            Assert.Equal(blobsBefore, BlobsInTheFileStore());                                   // the object that was put is not left behind unregistered
            Assert.Empty(await RequestsForRunAsync(run.GetProperty("runId").GetGuid()));
            Assert.Empty(EmailsTo(reception.Email));
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── T-I4: R3 on a scheduled run -- the clinical-access write fails, nothing is stored or sent ─────────────
    [Theory]
    [InlineData("throws")]
    [InlineData("isRefused")]
    public async Task T_I4_AScheduledR3_WhoseClinicalAccessRowCannotBeWritten_FailsClosed_NothingStoredNothingSent(string shape)
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var vet = await SeedRecipientAsync(context, StaffRoles.Vet);
        var administrator = await SeedRecipientAsync(context, StaffRoles.Administrator);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportCatalogue.SurgeryRegisterKey, "PreviousMonth", "10:00", [vet.Id, administrator.Id]));
        try
        {
            using var failing = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => WithClinicalAccessWrite(s, shape)));
            var registerRowsBefore = await ReportExportRowsAsync();
            var blobsBefore = BlobsInTheFileStore();

            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunWorkerPassOnAsync(failing);

            var admin = await FreshAdminAsync(context);
            var run = Assert.Single(await RunsAsync(admin, created.ScheduleId));
            Assert.Equal("Failed", run.GetProperty("status").GetString());
            Assert.Equal(ScheduledReportRunner.ClinicalAccessLogFailedCode, run.GetProperty("failureCode").GetString());
            Assert.Equal(registerRowsBefore, await ReportExportRowsAsync());
            Assert.Equal(blobsBefore, BlobsInTheFileStore());
            Assert.Empty(await RequestsForRunAsync(run.GetProperty("runId").GetGuid()));
            Assert.Empty(EmailsTo(vet.Email));
            Assert.Empty(EmailsTo(administrator.Email));
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── T-I5: R3 run-now (the manual trigger): one access row per recipient; fails closed ────────────────────
    [Theory]
    [InlineData("throws")]
    [InlineData("isRefused")]
    public async Task T_I5_RunNowOfR3_WritesOneClinicalAccessRowPerRecipient_AndFailsClosedWhenTheWriteFails(string shape)
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var vet = await SeedRecipientAsync(context, StaffRoles.Vet);
        var administrator = await SeedRecipientAsync(context, StaffRoles.Administrator);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportCatalogue.SurgeryRegisterKey, "PreviousMonth", "10:00", [vet.Id, administrator.Id]));
        try
        {
            var vetBefore = await ClinicalReadsAsync(vet.Id);
            var adminBefore = await ClinicalReadsAsync(administrator.Id);

            var ok = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsync($"/api/admin/report-schedules/{created.ScheduleId}/run-now", null)));
            Assert.Equal("Generated", ok.GetProperty("status").GetString());
            Assert.Equal(vetBefore + 1, await ClinicalReadsAsync(vet.Id));
            Assert.Equal(adminBefore + 1, await ClinicalReadsAsync(administrator.Id));

            // The same call on a host whose access write fails: a Failed outcome, nothing stored, no further row.
            using var failing = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => WithClinicalAccessWrite(s, shape)));
            var (_, failingAdmin) = await ContextWithRoleAsync(StaffRoles.Administrator, failing);
            var registerRowsBefore = await ReportExportRowsAsync();
            var blobsBefore = BlobsInTheFileStore();

            var failed = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await failingAdmin.PostAsync($"/api/admin/report-schedules/{created.ScheduleId}/run-now", null)));
            Assert.Equal("Failed", failed.GetProperty("status").GetString());
            Assert.Equal(ScheduledReportRunner.ClinicalAccessLogFailedCode, failed.GetProperty("failureCode").GetString());
            Assert.Equal(registerRowsBefore, await ReportExportRowsAsync());
            Assert.Equal(blobsBefore, BlobsInTheFileStore());
            Assert.Equal(vetBefore + 1, await ClinicalReadsAsync(vet.Id));
            Assert.Equal(adminBefore + 1, await ClinicalReadsAsync(administrator.Id));
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── T-I11: the interactive R3 view and exports when the access write is REFUSED (returns a failed response) rather than thrown ───────
    [Fact]
    public async Task T_I11_AnInteractiveR3_WhoseClinicalAccessWriteIsRefused_ReleasesNothing_AViewAndBothExportsAreABare500()
    {
        using var refusing = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => WithClinicalAccessWrite(s, "isRefused")));
        var (context, vet) = await ContextWithRoleAsync(StaffRoles.Vet, refusing);

        foreach (var path in new[] { "", "/export.csv", "/export.pdf" })
        {
            var response = await vet.GetAsync($"/api/staff/reports/{ReportCatalogue.SurgeryRegisterKey}{path}" + Query(context, Today.AddDays(-30), Today));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("rows", await response.Content.ReadAsStringAsync());
        }

        await ExpectAsync(HttpStatusCode.OK, await vet.GetAsync($"/api/staff/reports/{ReportCatalogue.ClinicDailyScheduleKey}" + Query(context, Today, Today)));
    }

    // ── T-I6: run-now is limited per ACCOUNT, and the window survives an idle gap longer than the partition's 10 seconds ──
    [Fact]
    public async Task T_I6_RunNow_IsFivePerWindowPerAccount_TheSixthIs429_AnotherAccountIsServed_AndTheWindowSurvivesAnIdleGap()
    {
        await RetireOtherSchedulesAsync();
        var contextA = await CreateBillingContextAsync();
        var contextB = await CreateBillingContextAsync();
        var receptionA = await SeedRecipientAsync(contextA, StaffRoles.Reception);
        var receptionB = await SeedRecipientAsync(contextB, StaffRoles.Reception);
        var scheduleA = await CreateScheduleAsync(contextA.Admin, Body(contextA, ReportingFixtures.RowsKey, "Today", "10:00", [receptionA.Id], name: "Limit A"));
        var scheduleB = await CreateScheduleAsync(contextB.Admin, Body(contextB, ReportingFixtures.RowsKey, "Today", "10:00", [receptionB.Id], name: "Limit B"));
        try
        {
            for (var call = 1; call <= 5; call++)
                await ExpectAsync(HttpStatusCode.OK, await contextA.Admin.PostAsync($"/api/admin/report-schedules/{scheduleA.ScheduleId}/run-now", null));

            var sixth = await contextA.Admin.PostAsync($"/api/admin/report-schedules/{scheduleA.ScheduleId}/run-now", null);
            Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
            Assert.NotNull(sixth.Headers.RetryAfter);

            // Another account is not affected by A's exhausted window.
            await ExpectAsync(HttpStatusCode.OK, await contextB.Admin.PostAsync($"/api/admin/report-schedules/{scheduleB.ScheduleId}/run-now", null));

            // A real idle gap longer than the partitioned limiter's idle disposal (10 s): the window must still hold.
            await Task.Delay(TimeSpan.FromSeconds(11.5));
            var seventh = await contextA.Admin.PostAsync($"/api/admin/report-schedules/{scheduleA.ScheduleId}/run-now", null);
            Assert.Equal(HttpStatusCode.TooManyRequests, seventh.StatusCode);
            await ExpectAsync(HttpStatusCode.OK, await contextB.Admin.PostAsync($"/api/admin/report-schedules/{scheduleB.ScheduleId}/run-now", null));

            // Exactly five manual runs were produced for A, one per allowed call -- a refused call produced nothing.
            var admin = await FreshAdminAsync(contextA);
            Assert.Equal(5, (await RunsAsync(admin, scheduleA.ScheduleId)).Count);
        }
        finally
        {
            await CancelAsync(contextA.Admin, scheduleA.ScheduleId);
            await CancelAsync(contextB.Admin, scheduleB.ScheduleId);
        }
    }

    // ── T-I7: an ALL-BRANCHES schedule is re-checked at send time for the cross-branch right ────────────────
    [Fact]
    public async Task T_I7_ARecipientWhoLosesTheCrossBranchRoleBeforeTheSend_IsNeverEmailedAnAllBranchesReport()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        // An administrator who is also a vet: the clinic action comes from either role, the cross-branch right from the administrator role only. R1 has no restricted column, so the cross-branch right is the ONLY thing the role change takes away.
        var both = await SeedStaffWithEmailAsync(StaffRoles.Administrator, StaffRoles.Vet);
        var created = await CreateScheduleAsync(context.Admin, new
        {
            name = "All branches clinic", reportKey = ReportCatalogue.ClinicDailyScheduleKey, branchId = (Guid?)null, period = "Today", format = "Csv", frequency = "Daily", sendAtLocal = "07:00",
            recipientIds = new[] { both.Id }, parameters = new Dictionary<string, string>()
        });
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();
            await RunDeliveryPassAsync();                                          // expanded at 07:00 and held by quiet hours until 08:00
            var admin = await FreshAdminAsync(context);
            var run = Assert.Single(await RunsAsync(admin, created.ScheduleId));
            Assert.Equal("Generated", run.GetProperty("status").GetString());
            var runId = run.GetProperty("runId").GetGuid();

            Assert.True(await GuardAsync(runId, both.Id));                         // still a cross-branch clinic recipient

            // The administrator role is removed. The Vet role still holds the clinic action, so only the cross-branch right is gone.
            await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/staff-accounts/{both.Id}/roles", new { roleIds = new[] { StaffRoles.Vet } }, Json));
            Assert.False(await GuardAsync(runId, both.Id));

            Factory.Clock.Advance(TimeSpan.FromHours(1));
            await RunDeliveryPassesUntilAsync(async () => (await NotificationsForRunAsync(runId)) is { Count: > 0 } rows && rows.All(n => n.Status is "Cancelled" or "Suppressed"));

            Assert.Empty(EmailsTo(both.Email));
            var notification = Assert.Single(await NotificationsForRunAsync(runId));
            Assert.Equal("Cancelled", notification.Status);
            Assert.Equal(NotificationCloseReason.SubjectNoLongerApplies.DisplayName, notification.CloseReason);
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── T-I9: deferred sources are ABSENT, not zero ─────────────────────────────────────────────────────────
    [Fact]
    public async Task T_I9_TheDashboardAndTheCatalogue_HaveNoBoardingSubscriptionOrCartAbandonmentKeyAtAll()
    {
        var (_, administrator) = await ContextWithRoleAsync(StaffRoles.Administrator);

        var dashboard = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await administrator.GetAsync("/api/staff/dashboard")));
        var tiles = dashboard.GetProperty("tiles").EnumerateArray().ToList();
        Assert.NotEmpty(tiles);
        var tileKeys = tiles.Select(t => t.GetProperty("key").GetString()!).ToList();
        var figureKeys = tiles.SelectMany(t => t.TryGetProperty("figures", out var figures) && figures.ValueKind == JsonValueKind.Array ? figures.EnumerateArray().Select(f => f.GetProperty("key").GetString()!) : []).ToList();
        foreach (var key in tileKeys.Concat(figureKeys))
        {
            Assert.DoesNotContain("boarding", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("subscription", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("abandon", key, StringComparison.OrdinalIgnoreCase);
        }

        var catalogue = (await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await administrator.GetAsync("/api/staff/reports")))).EnumerateArray().Select(e => e.GetProperty("key").GetString()!).ToList();
        foreach (var key in catalogue)
        {
            Assert.DoesNotContain("boarding", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("subscription", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("abandon", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("conversion", key, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("occupancy", key, StringComparison.OrdinalIgnoreCase);
        }

        // The static catalogue agrees: nothing in it is a deferred source.
        Assert.DoesNotContain(ReportCatalogue.All, d => d.Key.Contains("boarding", StringComparison.OrdinalIgnoreCase) || d.Key.Contains("subscription", StringComparison.OrdinalIgnoreCase));
    }
}
