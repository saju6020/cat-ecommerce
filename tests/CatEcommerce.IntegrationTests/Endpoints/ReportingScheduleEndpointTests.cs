using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CatEcommerce.Api.Background;
using CatEcommerce.Application.Commands.Reporting;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Reporting;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Notifications;
using CatEcommerce.Infrastructure.Notifications.Senders;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-RPT-001 section 16 G (US-RPT-06, FR-RPT-2) -- scheduled reports over the real endpoints, the real worker cycle and the real notification pipeline with the capturing email sender: I-G1 to I-G6,
/// I-G8, I-G9 and I-G11, with U-G5, U-G7 and U-G8 (which need the database). I-G7 (purge) is deferred with HO-XCT-001, and I-G10 (London across the October change) is
/// <see cref="ReportingScheduleDstEndpointTests"/>. One fresh branch per test; every schedule is cancelled when its test ends, and the leftovers of an earlier failed run are retired first, so the
/// worker's pass only ever sees this test's schedule.
/// </summary>
[Collection(ScheduleTestBase.CollectionName)]
public sealed class ReportingScheduleEndpointTests(ReportingApiFactory factory) : ScheduleTestBase(factory)
{
    // ── I-G1 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_G1_AnAdministratorCreatesADailySchedule_AShopManagerCannot_AndEveryRecipientIsChecked()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var accountant = await SeedRecipientAsync(context, StaffRoles.Accountant);

        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportCatalogue.RevenueByBusinessLineKey, "MonthToDate", "07:00", [accountant.Id]));
        try
        {
            // 07:00 Dhaka is 01:00 UTC: the next one after the fake clock now.
            Assert.Equal(NextLocalOccurrence(new TimeOnly(7, 0)), created.NextRunOn);

            var (_, shopManager) = await ContextWithRoleAsync(StaffRoles.ShopManager);
            var forbidden = await shopManager.PostAsJsonAsync("/api/admin/report-schedules", Body(context, ReportCatalogue.RevenueByBusinessLineKey, "MonthToDate", "07:00", [accountant.Id]), Json);
            await ExpectAsync(HttpStatusCode.Forbidden, forbidden);

            var noEmail = await SeedStaffAsync(StaffRoles.Accountant);
            await AssignAsync(context.Admin, noEmail.StaffAccountId, context.BranchId);
            var noEmailResponse = await context.Admin.PostAsJsonAsync("/api/admin/report-schedules", Body(context, ReportCatalogue.RevenueByBusinessLineKey, "MonthToDate", "07:00", [noEmail.StaffAccountId]), Json);
            await ExpectAsync(HttpStatusCode.BadRequest, noEmailResponse);
            Assert.Contains(BusinessViolationCodes.ReportRecipientHasNoEmail, await CodesAsync(noEmailResponse));

            var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
            var notPermitted = await context.Admin.PostAsJsonAsync("/api/admin/report-schedules", Body(context, ReportCatalogue.RevenueByBusinessLineKey, "MonthToDate", "07:00", [reception.Id]), Json);
            await ExpectAsync(HttpStatusCode.BadRequest, notPermitted);
            Assert.Contains(BusinessViolationCodes.ReportRecipientNotPermitted, await CodesAsync(notPermitted));

            // An accountant assigned to a DIFFERENT branch only.
            var elsewhere = await CreateBillingContextAsync();
            var stranger = await SeedRecipientAsync(elsewhere, StaffRoles.Accountant);
            var notAssigned = await context.Admin.PostAsJsonAsync("/api/admin/report-schedules", Body(context, ReportCatalogue.RevenueByBusinessLineKey, "MonthToDate", "07:00", [stranger.Id]), Json);
            await ExpectAsync(HttpStatusCode.BadRequest, notAssigned);
            Assert.Contains(BusinessViolationCodes.ReportRecipientNotAssignedToBranch, await CodesAsync(notAssigned));

            // The shape and the report are checked too.
            var unknown = await context.Admin.PostAsJsonAsync("/api/admin/report-schedules", Body(context, "no-such-report", "MonthToDate", "07:00", [accountant.Id]), Json);
            await ExpectAsync(HttpStatusCode.BadRequest, unknown);
            Assert.Contains(BusinessViolationCodes.UnknownReport, await CodesAsync(unknown));

            var badPeriod = await context.Admin.PostAsJsonAsync("/api/admin/report-schedules", Body(context, ReportCatalogue.RevenueByBusinessLineKey, "Today", "07:00", [accountant.Id]), Json);
            await ExpectAsync(HttpStatusCode.BadRequest, badPeriod);
            Assert.Contains(BusinessViolationCodes.InvalidReportSchedulePeriod, await CodesAsync(badPeriod));

            var offGrid = await context.Admin.PostAsJsonAsync("/api/admin/report-schedules", Body(context, ReportCatalogue.RevenueByBusinessLineKey, "MonthToDate", "07:03", [accountant.Id]), Json);
            await ExpectAsync(HttpStatusCode.BadRequest, offGrid);
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── I-G2: the user-story acceptance criterion ───────────────────────────────────────────────────
    [Fact]
    public async Task I_G2_ADailyScheduleIsGenerated_HeldByQuietHours_ThenDeliveredWithTheSameBytesAsAnInteractiveExport()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var accountant = await SeedRecipientAsync(context, StaffRoles.Accountant);
        var customerId = await SeedCustomerAsync(context);
        var charge = await RecordChargeAsync(context, customerId, 800m, 1m, itemKind: ItemKind.Service, sourceKind: ChargeSourceKind.Appointment, description: "Consultation");
        await IssueFromChargesAsync(context.Admin, customerId, [charge]);

        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportCatalogue.RevenueByBusinessLineKey, "MonthToDate", "07:00", [accountant.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();

            var admin = await FreshAdminAsync(context);
            var runs = await RunsAsync(admin, created.ScheduleId);
            var run = Assert.Single(runs);
            Assert.Equal("Generated", run.GetProperty("status").GetString());
            Assert.Equal("Schedule", run.GetProperty("trigger").GetString());
            Assert.False(run.GetProperty("attachmentOmitted").GetBoolean());

            // One file in HO-FIL-001, for the ReportExport purpose.
            var fileName = $"purrcare-{ReportCatalogue.RevenueByBusinessLineKey}-{run.GetProperty("periodFrom").GetString()}-to-{run.GetProperty("periodTo").GetString()}.csv";
            Assert.True(await WithDbAsync(db => db.StoredFiles.AnyAsync(f => f.OriginalFileName == fileName && f.Purpose == FilePurpose.ReportExport && f.RetainUntil != null)));

            // 07:00 Dhaka is inside the default quiet hours (21:00-08:00): the Normal email is held.
            await RunDeliveryPassAsync();
            Assert.Empty(EmailsTo(accountant.Email));

            // At 08:00 it goes.
            Factory.Clock.Advance(TimeSpan.FromHours(1));
            await RunDeliveryPassesUntilAsync(() => Task.FromResult(EmailsTo(accountant.Email).Count > 0));

            var email = Assert.Single(EmailsTo(accountant.Email));
            Assert.Contains("Revenue by business line", email.Subject);
            var attachment = Assert.Single(email.AttachmentList);
            Assert.Equal(fileName, attachment.FileName);
            Assert.StartsWith("text/csv", attachment.ContentType);
            Assert.Contains("/staff/reports/" + ReportCatalogue.RevenueByBusinessLineKey, email.TextBody);
            Assert.Contains($"branchId={context.BranchId}", email.TextBody);

            // US-RPT-06 AC: the attachment is byte-for-byte what the recipient could export on screen for the same filters and columns.
            var client = await SignInStaffAsync(accountant.Mobile, accountant.Password, context.BranchId);
            var export = await client.GetAsync($"/api/staff/reports/{ReportCatalogue.RevenueByBusinessLineKey}/export.csv?from={run.GetProperty("periodFrom").GetString()}&to={run.GetProperty("periodTo").GetString()}&branchId={context.BranchId}");
            await ExpectAsync(HttpStatusCode.OK, export);
            Assert.Equal(await export.Content.ReadAsByteArrayAsync(), attachment.Content);
            Assert.Contains("Clinic", Encoding.UTF8.GetString(attachment.Content));

            // The schedule moved on: next 07:00.
            admin = await FreshAdminAsync(context);
            var detail = await GetScheduleAsync(admin, created.ScheduleId);
            Assert.True(DateTimeOffset.Parse(detail.GetProperty("nextRunOn").GetString()!) > Now);
            Assert.Equal(JsonValueKind.String, detail.GetProperty("lastRunOn").ValueKind);

            run = Assert.Single(await RunsAsync(admin, created.ScheduleId));
            var delivery = Assert.Single(run.GetProperty("deliveries").EnumerateArray());
            Assert.Equal("Sent", delivery.GetProperty("status").GetString());
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── I-G3: exactly once per slot ─────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_G3_RunningTheCycleTwice_AndRecordingTheSameSlotConcurrently_StillMakesOneRunAndOneEmail()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();
            await RunReportingCycleAsync();

            // Two workers record the very same slot at the same instant (the first run was already recorded above, so use a second schedule slot: a fresh schedule for the race).
            var admin = await FreshAdminAsync(context);
            Assert.Single(await RunsAsync(admin, created.ScheduleId));

            var race = await CreateScheduleAsync(admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id], name: "Race"));
            try
            {
                var due = race.NextRunOn!.Value;
                var filesBefore = await WithDbAsync(db => db.StoredFiles.CountAsync(f => f.Purpose == FilePurpose.ReportExport));

                // Two runners work the very same slot at the same instant: each stores its file and records its run (C5); the unique slot index lets one win and the other removes its file.
                async Task<ScheduledRunOutcome?> RunAsync()
                {
                    await using var scope = Factory.Services.CreateAsyncScope();
                    return await scope.ServiceProvider.GetRequiredService<ScheduledReportRunner>()
                        .RunAsync(race.ScheduleId, due, ReportRunTrigger.Schedule, new UserContext(ReportingSystemActors.ReportingWorker, Guid.Empty));
                }

                var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(RunAsync)));
                Assert.Equal(1, results.Count(r => r!.Created));

                var runs = await RunsAsync(admin, race.ScheduleId);
                var kept = Assert.Single(runs);
                Assert.Equal(results.Single(r => r!.Created)!.RunId, kept.GetProperty("runId").GetGuid());

                // The loser's file was removed (its own step): exactly one new stored file remains, and it is the kept run's.
                Assert.Equal(filesBefore + 1, await WithDbAsync(db => db.StoredFiles.CountAsync(f => f.Purpose == FilePurpose.ReportExport)));
                var keptId = kept.GetProperty("runId").GetGuid();
                Assert.True(await WithDbAsync(db => db.StoredFiles.AnyAsync(f => db.Set<ScheduledReportRunAggregate>().Any(r => r.Id == keptId && r.FileId == f.Id))));

                Factory.Clock.Advance(TimeSpan.FromMinutes(1));
                await RunDeliveryPassesUntilAsync(async () => (await RequestsForRunAsync(kept.GetProperty("runId").GetGuid())) is { Count: > 0 } rows && rows.All(s => s != "Pending"));
                Assert.Equal(1, (await RequestsForRunAsync(kept.GetProperty("runId").GetGuid())).Count);
            }
            finally
            {
                await CancelAsync(admin, race.ScheduleId);
            }

            // And the first schedule, run twice: one email to its recipient, not two.
            await RunDeliveryPassesUntilAsync(() => Task.FromResult(EmailsTo(reception.Email).Any(m => m.AttachmentList.Any(a => a.FileName.StartsWith("purrcare-fixture-rows-")))));
            await RunDeliveryPassesUntilAsync(() => Task.FromResult(EmailsTo(reception.Email).Count(m => m.AttachmentList.Any()) >= 2));
            Assert.Equal(2, EmailsTo(reception.Email).Count(m => m.AttachmentList.Any())); // one per schedule (the race schedule and the first), never two for one slot
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── I-G4: catching up after downtime ────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_G4_AfterThreeDaysOfDowntime_OneCycleMakesOneRunForTheLatestSlot_AndTheNextRunIsTomorrow()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            Factory.Clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromHours(2));
            await RunReportingCycleAsync();

            var admin = await FreshAdminAsync(context);
            var run = Assert.Single(await RunsAsync(admin, created.ScheduleId));
            var dueOn = DateTimeOffset.Parse(run.GetProperty("dueOn").GetString()!);
            Assert.True(dueOn <= Now && dueOn > Now.AddHours(-24), $"The run is for the latest slot ({dueOn:u}), not the first missed one.");

            var detail = await GetScheduleAsync(admin, created.ScheduleId);
            var next = DateTimeOffset.Parse(detail.GetProperty("nextRunOn").GetString()!);
            Assert.True(next > Now && next <= Now.AddHours(24));

            // A second cycle at the same instant does nothing.
            await RunReportingCycleAsync();
            Assert.Single(await RunsAsync(admin, created.ScheduleId));
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── I-G5 + U-G5: losing the role stops delivery ─────────────────────────────────────────────────
    [Fact]
    public async Task I_G5_RemovingTheRecipientsRoleBetweenGenerationAndDelivery_CancelsTheEmail()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var accountant = await SeedRecipientAsync(context, StaffRoles.Accountant);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportCatalogue.RevenueByBusinessLineKey, "MonthToDate", "07:00", [accountant.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();
            await RunDeliveryPassAsync(); // expanded at 07:00, held until 08:00

            var admin = await FreshAdminAsync(context);
            var run = Assert.Single(await RunsAsync(admin, created.ScheduleId));
            var runId = run.GetProperty("runId").GetGuid();
            Assert.Empty(EmailsTo(accountant.Email));

            // U-G5: while the accountant still holds the action the guard is valid ...
            Assert.True(await GuardAsync(runId, accountant.Id));

            // ... and once the role is gone it is not.
            await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/staff-accounts/{accountant.Id}/roles", new { roleIds = new[] { StaffRoles.Reception } }, Json));
            Assert.False(await GuardAsync(runId, accountant.Id));

            Factory.Clock.Advance(TimeSpan.FromHours(1));
            await RunDeliveryPassesUntilAsync(async () => (await NotificationsForRunAsync(runId)) is { Count: > 0 } rows && rows.All(n => n.Status is "Cancelled" or "Suppressed"));

            Assert.Empty(EmailsTo(accountant.Email));
            var notification = Assert.Single(await NotificationsForRunAsync(runId));
            Assert.Equal("Cancelled", notification.Status);
            Assert.Equal(NotificationCloseReason.SubjectNoLongerApplies.DisplayName, notification.CloseReason);
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    [Fact]
    public async Task U_G5_TheGuard_IsInvalidForAnInactiveAccount_AMissingRun_AndAPurgedRun()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();
            var admin = await FreshAdminAsync(context);
            var runId = Assert.Single(await RunsAsync(admin, created.ScheduleId)).GetProperty("runId").GetGuid();

            Assert.True(await GuardAsync(runId, reception.Id));
            Assert.False(await GuardAsync(Guid.NewGuid(), reception.Id));      // no such run
            Assert.False(await GuardAsync(runId, Guid.NewGuid()));              // no such account

            // An account whose roles are all removed is inactive for this purpose.
            await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/staff-accounts/{reception.Id}/roles", new { roleIds = new[] { StaffRoles.Nurse } }, Json));
            Assert.False(await GuardAsync(runId, reception.Id));
            await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync($"/api/staff-accounts/{reception.Id}/roles", new { roleIds = new[] { StaffRoles.Reception } }, Json));
            Assert.True(await GuardAsync(runId, reception.Id));

            // A purged run: the file is gone.
            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<ScheduledReportRunAggregate>>();
                var run = (await repository.GetByIdAsync(runId))!;
                run.Purge(Now, new UserContext(ReportingSystemActors.ReportingWorker, Guid.Empty));
                await repository.UpdateAsync(run, EventPublishOption.InMemoryPublishOnly);
            }
            Assert.False(await GuardAsync(runId, reception.Id));
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── U-G7: the deliver handler and a file that is gone ───────────────────────────────────────────
    [Fact]
    public async Task U_G7_AnEmailWhoseFileHasGone_IsCancelled_NeverSentWithoutIt()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();
            var admin = await FreshAdminAsync(context);
            var runId = Assert.Single(await RunsAsync(admin, created.ScheduleId)).GetProperty("runId").GetGuid();

            // The stored object disappears from the file store (its register row stays, so the guard still passes).
            await using (var scope = Factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
                var fileId = await db.Set<ScheduledReportRunAggregate>().AsNoTracking().Where(r => r.Id == runId).Select(r => r.FileId).SingleAsync();
                var storageKey = await db.StoredFiles.AsNoTracking().Where(f => f.Id == fileId).Select(f => f.StorageKey).SingleAsync();
                var deleted = await scope.ServiceProvider.GetRequiredService<IFileStore>().DeleteAsync(storageKey, CancellationToken.None);
                Assert.True(deleted.IsSuccess);
            }

            await RunDeliveryPassesUntilAsync(async () => (await NotificationsForRunAsync(runId)) is { Count: > 0 } rows && rows.All(n => n.Status != "Pending" && n.Status != "Queued"));

            Assert.Empty(EmailsTo(reception.Email));
            var notification = Assert.Single(await NotificationsForRunAsync(runId));
            Assert.Equal("Cancelled", notification.Status);
            Assert.Equal(NotificationCloseReason.SubjectNoLongerApplies.DisplayName, notification.CloseReason);
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── U-G8: no file content in the audit trail ────────────────────────────────────────────────────
    [Fact]
    public async Task U_G8_NoAuditRowContainsTheFileBytesOrAnyOfItsContent()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();
            var admin = await FreshAdminAsync(context);
            var run = Assert.Single(await RunsAsync(admin, created.ScheduleId));
            var runId = run.GetProperty("runId").GetGuid();

            var rows = await WithDbAsync(db => db.Database.SqlQuery<string>(
                $"select coalesce(\"BeforeValue\", '') || ' ' || coalesce(\"AfterValue\", '') as \"Value\" from audit.audit_entry where \"EntityId\" in ({runId}, {created.ScheduleId})").ToListAsync());
            Assert.NotEmpty(rows); // the interceptor did audit the run and the schedule

            var joined = string.Join("\n", rows);
            Assert.DoesNotContain("Row 3", joined);        // a cell of the CSV
            Assert.DoesNotContain("HYPERLINK", joined);    // another
            Assert.DoesNotContain("Fixture rows,Name", joined);
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── I-G6: nobody receives a column they could not open ──────────────────────────────────────────
    [Fact]
    public async Task I_G6_ARestrictedColumn_IsLeftOutWhenAnyRecipientCouldNotSeeIt()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
        var administrator = await SeedRecipientAsync(context, StaffRoles.Administrator);

        var mixed = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id, administrator.Id], name: "Mixed"));
        var adminsOnly = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [administrator.Id], name: "Admins"));
        try
        {
            await AdvanceToAsync(mixed.NextRunOn!.Value);
            await RunReportingCycleAsync();

            var admin = await FreshAdminAsync(context);
            var mixedRun = Assert.Single(await RunsAsync(admin, mixed.ScheduleId));
            var adminsRun = Assert.Single(await RunsAsync(admin, adminsOnly.ScheduleId));

            await RunDeliveryPassesUntilAsync(() => Task.FromResult(EmailsTo(reception.Email).Count > 0 && EmailsTo(administrator.Email).Count >= 2));

            var receptionCsv = Encoding.UTF8.GetString(Assert.Single(EmailsTo(reception.Email)).AttachmentList.Single().Content);
            Assert.DoesNotContain("Secret", receptionCsv);
            Assert.Contains("Amount", receptionCsv);

            // The administrator got the same restricted file from the mixed schedule, and the full one from the administrators-only schedule.
            var adminFiles = EmailsTo(administrator.Email).Select(m => Encoding.UTF8.GetString(m.AttachmentList.Single().Content)).ToList();
            Assert.Contains(adminFiles, csv => csv.Contains("Secret"));
            Assert.Contains(adminFiles, csv => !csv.Contains("Secret"));

            Assert.Equal(3, mixedRun.GetProperty("rowCount").GetInt32());
            Assert.Equal(3, adminsRun.GetProperty("rowCount").GetInt32());
            admin = await FreshAdminAsync(context);
            mixedRun = Assert.Single(await RunsAsync(admin, mixed.ScheduleId));
            Assert.Equal(2, mixedRun.GetProperty("deliveries").GetArrayLength());
        }
        finally
        {
            await CancelAsync(context.Admin, mixed.ScheduleId);
            await CancelAsync(context.Admin, adminsOnly.ScheduleId);
        }
    }

    // ── I-G8: too large to attach ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_G8_AFileOverTheAttachmentLimit_IsNotAttached_TheEmailSaysSo_AndCarriesTheLink()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);

        await SetMaxAttachmentMegabytesAsync(context.Admin, 1);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id], parameters: new Dictionary<string, string> { ["rows"] = "40000" }));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();

            var admin = await FreshAdminAsync(context);
            var run = Assert.Single(await RunsAsync(admin, created.ScheduleId));
            Assert.True(run.GetProperty("attachmentOmitted").GetBoolean());
            Assert.Equal("Generated", run.GetProperty("status").GetString());
            Assert.True(run.GetProperty("contentLength").GetInt64() > 1024 * 1024);

            await RunDeliveryPassesUntilAsync(() => Task.FromResult(EmailsTo(reception.Email).Count > 0));
            var email = Assert.Single(EmailsTo(reception.Email));
            Assert.Empty(email.AttachmentList);
            Assert.Contains("too large to attach", email.TextBody);
            Assert.Contains("/staff/reports/" + ReportingFixtures.RowsKey, email.TextBody);
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
            await SetMaxAttachmentMegabytesAsync(context.Admin, 10);
        }
    }

    // ── I-G9: run now, pause, resume, cancel ────────────────────────────────────────────────────────
    [Fact]
    public async Task I_G9_RunNow_Pause_Resume_AndCancel()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id]));
        try
        {
            var runNow = await context.Admin.PostAsync($"/api/admin/report-schedules/{created.ScheduleId}/run-now", null);
            await ExpectAsync(HttpStatusCode.OK, runNow);
            var body = await ReadAsync(runNow);
            Assert.Equal("Generated", body.GetProperty("status").GetString());
            Assert.Equal(3, body.GetProperty("rowCount").GetInt32());

            var runs = await RunsAsync(context.Admin, created.ScheduleId);
            var manual = Assert.Single(runs);
            Assert.Equal("Manual", manual.GetProperty("trigger").GetString());
            Assert.Equal(body.GetProperty("runId").GetGuid(), manual.GetProperty("runId").GetGuid());

            // A manual run does not advance the schedule.
            var detail = await GetScheduleAsync(context.Admin, created.ScheduleId);
            Assert.Equal(created.NextRunOn, DateTimeOffset.Parse(detail.GetProperty("nextRunOn").GetString()!));
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("lastRunOn").ValueKind);

            // Paused: the time passes and nothing runs.
            await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PostAsync($"/api/admin/report-schedules/{created.ScheduleId}/pause", null));
            await AdvanceToAsync(created.NextRunOn!.Value.AddHours(1));
            await RunReportingCycleAsync();
            var admin = await FreshAdminAsync(context);
            Assert.Single(await RunsAsync(admin, created.ScheduleId));
            Assert.Equal(JsonValueKind.Null, (await GetScheduleAsync(admin, created.ScheduleId)).GetProperty("nextRunOn").ValueKind);

            // Resumed: the next run is computed from now, not from the missed time.
            await ExpectAsync(HttpStatusCode.NoContent, await admin.PostAsync($"/api/admin/report-schedules/{created.ScheduleId}/resume", null));
            var resumed = DateTimeOffset.Parse((await GetScheduleAsync(admin, created.ScheduleId)).GetProperty("nextRunOn").GetString()!);
            Assert.True(resumed > Now);
            await RunReportingCycleAsync();
            Assert.Single(await RunsAsync(admin, created.ScheduleId));

            // Cancelled: it never runs, can no longer be changed or run, and is kept for history.
            await ExpectAsync(HttpStatusCode.NoContent, await admin.PostAsync($"/api/admin/report-schedules/{created.ScheduleId}/cancel", null));
            await AdvanceToAsync(resumed.AddDays(2));
            await RunReportingCycleAsync();
            admin = await FreshAdminAsync(context);
            Assert.Single(await RunsAsync(admin, created.ScheduleId));

            var resumeCancelled = await admin.PostAsync($"/api/admin/report-schedules/{created.ScheduleId}/resume", null);
            await ExpectAsync(HttpStatusCode.BadRequest, resumeCancelled);
            Assert.Contains(BusinessViolationCodes.ReportScheduleCancelled, await CodesAsync(resumeCancelled));
            await ExpectAsync(HttpStatusCode.BadRequest, await admin.PostAsync($"/api/admin/report-schedules/{created.ScheduleId}/run-now", null));
            Assert.Equal("Cancelled", (await GetScheduleAsync(admin, created.ScheduleId)).GetProperty("status").GetString());
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── The reads and an edit ───────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TheListDetailRecipientsAndUpdate_AgreeWithEachOther_AndAStaleVersionIs409()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
        var nurse = await SeedRecipientAsync(context, StaffRoles.Nurse);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id], name: "Reception rows"));
        try
        {
            var list = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/admin/report-schedules?status=Active&reportKey={ReportingFixtures.RowsKey}")));
            var item = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == created.ScheduleId);
            Assert.Equal("Reception rows", item.GetProperty("name").GetString());
            Assert.Equal("Daily", item.GetProperty("frequencyLabel").GetString());
            Assert.Equal("10:00", item.GetProperty("sendAtLocal").GetString());
            Assert.Equal("Asia/Dhaka", item.GetProperty("timeZoneId").GetString());
            Assert.Equal(1, item.GetProperty("recipientCount").GetInt32());

            var detail = await GetScheduleAsync(context.Admin, created.ScheduleId);
            var recipient = Assert.Single(detail.GetProperty("recipients").EnumerateArray());
            Assert.True(recipient.GetProperty("emailPresent").GetBoolean());
            Assert.True(recipient.GetProperty("stillPermitted").GetBoolean());
            var version = detail.GetProperty("version").GetInt32();

            // Eligible recipients: the receptionist holds the clinic-operations action, the nurse does not.
            var eligible = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/admin/report-schedules/recipients?reportKey={ReportingFixtures.RowsKey}&branchId={context.BranchId}")));
            var ids = eligible.EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
            Assert.Contains(reception.Id, ids);
            Assert.DoesNotContain(nurse.Id, ids);
            await ExpectAsync(HttpStatusCode.NotFound, await context.Admin.GetAsync("/api/admin/report-schedules/recipients?reportKey=nope"));

            var update = Body(context, ReportingFixtures.RowsKey, "Today", "12:30", [reception.Id], name: "Renamed", expectedVersion: version);
            var ok = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PutAsJsonAsync($"/api/admin/report-schedules/{created.ScheduleId}", update, Json)));
            Assert.Equal(NextLocalOccurrence(new TimeOnly(12, 30)), DateTimeOffset.Parse(ok.GetProperty("nextRunOn").GetString()!));

            // The same version again is now stale.
            await ExpectAsync(HttpStatusCode.Conflict, await context.Admin.PutAsJsonAsync($"/api/admin/report-schedules/{created.ScheduleId}", update, Json));
            await ExpectAsync(HttpStatusCode.NotFound, await context.Admin.GetAsync($"/api/admin/report-schedules/{Guid.NewGuid()}"));
            Assert.Equal("Renamed", (await GetScheduleAsync(context.Admin, created.ScheduleId)).GetProperty("name").GetString());

            var filtered = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync("/api/admin/report-schedules?status=Cancelled")));
            Assert.DoesNotContain(filtered.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == created.ScheduleId);
            await ExpectAsync(HttpStatusCode.BadRequest, await context.Admin.GetAsync("/api/admin/report-schedules?status=Nonsense"));
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── I-G11 ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_G11_ThePreview_SaysWhenQuietHoursWouldHoldTheEmail_AndWritesNothing()
    {
        var context = await CreateBillingContextAsync();
        var before = await WithDbAsync(db => db.Set<ReportScheduleAggregate>().CountAsync());

        var late = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync("/api/admin/report-schedules/preview",
            new { reportKey = ReportCatalogue.RevenueByBusinessLineKey, branchId = context.BranchId, period = "PreviousDay", frequency = "Daily", sendAtLocal = "22:00" }, Json)));
        var nextRuns = late.GetProperty("nextRuns").EnumerateArray().Select(r => DateTimeOffset.Parse(r.GetString()!)).ToList();
        Assert.Equal(3, nextRuns.Count);
        Assert.Equal(NextLocalOccurrence(new TimeOnly(22, 0)), nextRuns[0]);
        Assert.Equal(TimeSpan.FromDays(1), nextRuns[1] - nextRuns[0]);

        // Delivery of a 22:00 send waits for 08:00 the next local morning.
        var firstLocal = nextRuns[0].ToOffset(TimeSpan.FromHours(6));
        var expectedHold = new DateTimeOffset(firstLocal.Date.AddDays(1).AddHours(8), TimeSpan.FromHours(6));
        Assert.Equal(expectedHold, DateTimeOffset.Parse(late.GetProperty("deliveryHeldUntil").GetString()!));

        // The period that first run would cover: "previous day" as of the run.
        var period = late.GetProperty("periodForNextRun");
        var expectedDay = DateOnly.FromDateTime(firstLocal.Date).AddDays(-1).ToString("yyyy-MM-dd");
        Assert.Equal(expectedDay, period.GetProperty("from").GetString());
        Assert.Equal(expectedDay, period.GetProperty("to").GetString());

        var day = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync("/api/admin/report-schedules/preview",
            new { reportKey = ReportCatalogue.RevenueByBusinessLineKey, branchId = context.BranchId, period = "PreviousDay", frequency = "Daily", sendAtLocal = "10:00" }, Json)));
        Assert.True(!day.TryGetProperty("deliveryHeldUntil", out var held) || held.ValueKind == JsonValueKind.Null);

        var invalid = await context.Admin.PostAsJsonAsync("/api/admin/report-schedules/preview",
            new { reportKey = ReportCatalogue.RevenueByBusinessLineKey, branchId = context.BranchId, period = "PreviousDay", frequency = "Monthly", dayOfMonth = 29, sendAtLocal = "10:00" }, Json);
        await ExpectAsync(HttpStatusCode.BadRequest, invalid);
        await ExpectAsync(HttpStatusCode.NotFound, await context.Admin.PostAsJsonAsync("/api/admin/report-schedules/preview",
            new { reportKey = "nope", period = "PreviousDay", frequency = "Daily", sendAtLocal = "10:00" }, Json));

        Assert.Equal(before, await WithDbAsync(db => db.Set<ReportScheduleAggregate>().CountAsync()));
    }

    // ── A failed run is recorded, retried, and advanced after three ─────────────────────────────────
    [Fact]
    public async Task AFailingReport_RecordsFailedRuns_NoEmail_AndTheThirdFailureAdvancesTheSchedule()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);

        // The write fixture tries to write inside the read-only scope and fails every time.
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.WriteKey, "Today", "10:00", [reception.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            for (var attempt = 1; attempt <= ReportScheduleRules.MaxAttemptsPerSlot; attempt++)
            {
                await RunReportingCycleAsync();
                var admin = await FreshAdminAsync(context);
                var runs = await RunsAsync(admin, created.ScheduleId);
                Assert.Equal(attempt, runs.Count);
                Assert.All(runs, r => Assert.Equal("Failed", r.GetProperty("status").GetString()));

                var next = DateTimeOffset.Parse((await GetScheduleAsync(admin, created.ScheduleId)).GetProperty("nextRunOn").GetString()!);
                if (attempt < ReportScheduleRules.MaxAttemptsPerSlot) Assert.True(next <= Now, "Not yet advanced: the slot is tried again on the next pass.");
                else Assert.True(next > Now, "Advanced after the third failed attempt.");
            }

            Assert.Empty(EmailsTo(reception.Email));
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }
}

/// <summary>
/// HO-RPT-001 section 16 G -- I-G10: a schedule for a <c>Europe/London</c> branch keeps its local send time across a clock change. Its own class (so its own fake clock): it moves time by months, which must
/// not disturb the other schedule tests.
/// </summary>
[Collection(ScheduleTestBase.CollectionName)]
public sealed class ReportingScheduleDstEndpointTests(ReportingApiFactory factory) : ScheduleTestBase(factory)
{
    private static readonly TimeZoneInfo London = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");

    [Fact]
    public async Task I_G10_ALondonSchedule_RunsAt0700LocalOnBothSidesOfTheNextClockChange_WhichShiftsUtcByAnHour()
    {
        await RetireOtherSchedulesAsync();

        // The next local date, after the fake clock, on which London's UTC offset changes.
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Now, London).DateTime);
        while (London.GetUtcOffset(ReportScheduleCalculator.ToInstant(day.AddDays(1), new TimeOnly(12, 0), London)) == London.GetUtcOffset(ReportScheduleCalculator.ToInstant(day, new TimeOnly(12, 0), London)))
            day = day.AddDays(1);
        var seven = new TimeOnly(7, 0);

        // Set the clock to the small hours of the last day on the old offset, then build everything.
        await AdvanceToAsync(ReportScheduleCalculator.ToInstant(day, new TimeOnly(3, 0), London));
        var admin = await SeedAdminAsync();
        var branchCode = NewCode();
        var branch = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await admin.Client.PostAsJsonAsync("/api/admin/branches", new { code = branchCode, name = "London Test " + branchCode, timeZoneId = "Europe/London" }, Json)));
        var branchId = branch.GetProperty("branchId").GetGuid();
        await AssignAsync(admin.Client, admin.Id, branchId);
        admin.Client.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, branchId.ToString());

        var reception = await SeedStaffWithEmailAsync(StaffRoles.Reception);
        await AssignAsync(admin.Client, reception.Id, branchId);

        var created = await CreateScheduleAsync(admin.Client, new
        {
            name = "London daily", reportKey = ReportingFixtures.RowsKey, branchId, period = "Today", format = "Csv", frequency = "Daily", sendAtLocal = "07:00", recipientIds = new[] { reception.Id }
        });
        try
        {
            var firstSlot = created.NextRunOn!.Value;
            var firstDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(firstSlot, London).DateTime);
            Assert.Equal(ReportScheduleCalculator.ToInstant(firstDate, seven, London), firstSlot);

            await AdvanceToAsync(firstSlot);
            await RunReportingCycleAsync();

            var client = await SignInStaffAsync(admin.Mobile, admin.Password, branchId);
            var afterFirst = DateTimeOffset.Parse((await GetScheduleAsync(client, created.ScheduleId)).GetProperty("nextRunOn").GetString()!);
            var secondSlot = ReportScheduleCalculator.ToInstant(firstDate.AddDays(1), seven, London);
            Assert.Equal(secondSlot, afterFirst);
            if (firstDate == day) Assert.NotEqual(TimeSpan.FromDays(1), secondSlot - firstSlot); // the clock change moved UTC by an hour

            await AdvanceToAsync(secondSlot);
            await RunReportingCycleAsync();

            client = await SignInStaffAsync(admin.Mobile, admin.Password, branchId);
            var runs = await RunsAsync(client, created.ScheduleId);
            Assert.Equal(2, runs.Count);
            var dueOns = runs.Select(r => DateTimeOffset.Parse(r.GetProperty("dueOn").GetString()!)).OrderBy(d => d).ToList();
            Assert.Equal(new[] { firstSlot, secondSlot }, dueOns.ToArray());

            foreach (var due in dueOns)
                Assert.Equal(seven, TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(due, London).DateTime));
        }
        finally
        {
            await CancelAsync(admin.Client, created.ScheduleId);
        }
    }

    private async Task<(Guid Id, string Mobile, string Password, HttpClient Client)> SeedAdminAsync()
    {
        var staff = await SeedStaffAsync(StaffRoles.Administrator);
        return (staff.StaffAccountId, staff.Mobile, staff.Password, await SignInStaffAsync(staff.Mobile, staff.Password));
    }
}

/// <summary>What both schedule test classes share: building schedules, driving the cycles, and reading what they produced.</summary>
public abstract class ScheduleTestBase(ReportingApiFactory factory) : ReportingIntegrationTestBase(factory)
{
    /// <summary>
    /// The worker's pass runs EVERY due schedule in the shared database, and RetireOtherSchedulesAsync cancels every active one, so two schedule test classes running at the same time would run and
    /// cancel each other's schedules (and see each other's fake clock jump). One collection: the classes run one after the other (each keeps its own fixture and its own clock).
    /// </summary>
    public const string CollectionName = "ReportingSchedules";

    /// <summary>
    /// A delivery pass handles the oldest <c>Notifications:Worker:BatchSize</c> requests of the WHOLE shared database, so after a full run's backlog of other areas' pending requests this test's email can
    /// be many passes away. A much larger bound than the base class's 60 (it ends as soon as the email is out).
    /// </summary>
    protected new Task RunDeliveryPassesUntilAsync(Func<Task<bool>> delivered, int maxPasses = 600) => base.RunDeliveryPassesUntilAsync(delivered, maxPasses);

    protected sealed record Recipient(Guid Id, string Mobile, string Password, string Email);

    protected sealed record CreatedSchedule(Guid ScheduleId, DateTimeOffset? NextRunOn);

    /// <summary>A staff member with an email address (the schedule's recipients are emailed), assigned to the context's branch.</summary>
    protected async Task<Recipient> SeedRecipientAsync(BillingContext context, string role)
    {
        var recipient = await SeedStaffWithEmailAsync(role);
        await AssignAsync(context.Admin, recipient.Id, context.BranchId);
        return recipient;
    }

    protected async Task<Recipient> SeedStaffWithEmailAsync(params string[] roles)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<StaffAccountAggregate>>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var mobile = NewMobile();
        var email = $"rpt-{Guid.NewGuid():N}@example.test";
        var staff = new StaffAccountAggregate(Guid.NewGuid());
        staff.Create(new StaffAccountCreationDto("Report Recipient " + NewCode(), MobileNumber.Create(mobile), EmailAddress.Create(email), roles, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.SaveAsync(staff, EventPublishOption.InMemoryPublishOnly);

        staff.SetPassword(new PasswordSetDto(hasher.Hash(StaffPassword), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
        await repository.UpdateAsync(staff, EventPublishOption.InMemoryPublishOnly);
        return new Recipient(staff.Id, mobile, StaffPassword, email);
    }

    /// <summary>The test database is shared and never truncated: schedules an earlier failed run left active would be picked up by this test's worker pass, so retire them first.</summary>
    protected Task RetireOtherSchedulesAsync()
    {
        ReportingTestDatabase.Require(); // never cancel every schedule of a shared or dev database
        return RetireAsync();
    }

    private Task RetireAsync() =>
        WithDbAsync(db => db.Database.ExecuteSqlAsync($"update report.report_schedule set status = {ReportScheduleStatus.Cancelled.Value}, next_run_on = null where status <> {ReportScheduleStatus.Cancelled.Value}"));

    protected static object Body(
        BillingContext context, string reportKey, string period, string sendAt, IEnumerable<Guid> recipients, string name = "Scheduled report",
        Dictionary<string, string>? parameters = null, int? expectedVersion = null) =>
        new
        {
            name, reportKey, branchId = context.BranchId, period, format = "Csv", frequency = "Daily", sendAtLocal = sendAt, recipientIds = recipients.ToArray(),
            parameters = parameters ?? new Dictionary<string, string>(), expectedVersion
        };

    protected async Task<CreatedSchedule> CreateScheduleAsync(HttpClient admin, object body)
    {
        var response = await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync("/api/admin/report-schedules", body, Json));
        var created = await ReadAsync(response);
        return new CreatedSchedule(created.GetProperty("scheduleId").GetGuid(), DateTimeOffset.Parse(created.GetProperty("nextRunOn").GetString()!));
    }

    protected async Task CancelAsync(HttpClient admin, Guid scheduleId)
    {
        try
        {
            await admin.PostAsync($"/api/admin/report-schedules/{scheduleId}/cancel", null);
        }
        catch
        {
            // A schedule that cannot be cancelled here is retired by the next test's RetireOtherSchedulesAsync.
        }
    }

    protected async Task<JsonElement> GetScheduleAsync(HttpClient admin, Guid scheduleId) =>
        await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await admin.GetAsync($"/api/admin/report-schedules/{scheduleId}")));

    protected async Task<List<JsonElement>> RunsAsync(HttpClient admin, Guid scheduleId)
    {
        var body = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await admin.GetAsync($"/api/admin/report-schedules/{scheduleId}/runs?pageSize=50")));
        return body.GetProperty("items").EnumerateArray().ToList();
    }

    /// <summary>A staff session idles out after 30 minutes of fake time: sign the context's administrator in again (the original client is reused, so its default headers stay).</summary>
    protected async Task<HttpClient> FreshAdminAsync(BillingContext context)
    {
        await ReSignInAsync(context);
        return context.Admin;
    }

    /// <summary>The first <paramref name="localTime"/> in Asia/Dhaka (UTC+6, no DST) strictly after the fake clock.</summary>
    protected DateTimeOffset NextLocalOccurrence(TimeOnly localTime)
    {
        var local = Now.ToOffset(TimeSpan.FromHours(6));
        var candidate = new DateTimeOffset(local.Date.Add(localTime.ToTimeSpan()), TimeSpan.FromHours(6));
        return (candidate > Now ? candidate : candidate.AddDays(1)).ToUniversalTime();
    }

    protected Task AdvanceToAsync(DateTimeOffset instant)
    {
        var delta = instant - Now;
        if (delta > TimeSpan.Zero) Factory.Clock.Advance(delta);
        return Task.CompletedTask;
    }

    protected async Task RunReportingCycleAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ReportingWorkCycle>().RunAsync();
    }

    protected IReadOnlyList<OutgoingMessage> EmailsTo(string address)
    {
        var sender = (CapturingChannelSender)Factory.Services.GetRequiredService<NotificationSenderResolver>().Resolve(NotificationChannel.Email);
        return sender.Sent.Where(m => string.Equals(m.Destination, address, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    protected sealed record NotificationRow(string Status, string? CloseReason);

    protected Task<List<NotificationRow>> NotificationsForRunAsync(Guid runId) =>
        WithDbAsync(async db =>
        {
            var subject = NotificationSubjectKind.ScheduledReport;
            var rows = await db.Set<CatEcommerce.Domain.Aggregates.Notification.NotificationAggregate>().AsNoTracking()
                .Where(n => n.SubjectKind == subject && n.SubjectId == runId).Select(n => new { n.Status, n.CloseReason }).ToListAsync();
            return rows.Select(n => new NotificationRow(n.Status.DisplayName, n.CloseReason?.DisplayName)).ToList();
        });

    protected Task<List<string>> RequestsForRunAsync(Guid runId) =>
        WithDbAsync(async db =>
        {
            var subject = NotificationSubjectKind.ScheduledReport;
            var rows = await db.Set<CatEcommerce.Domain.Aggregates.Notification.NotificationRequestAggregate>().AsNoTracking()
                .Where(r => r.SubjectKind == subject && r.SubjectId == runId).Select(r => r.Status).ToListAsync();
            return rows.Select(s => s.DisplayName).ToList();
        });

    /// <summary>Asks the registered <see cref="ScheduledReportGuard"/> whether an email about the run is still valid for the recipient.</summary>
    protected async Task<bool> GuardAsync(Guid runId, Guid recipientId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var resolver = scope.ServiceProvider.GetRequiredService<ISubjectGuardResolver>();
        return await resolver.IsStillValidAsync(NotificationSubjectKind.ScheduledReport, recipientId, runId, null, null);
    }

    protected async Task SetMaxAttachmentMegabytesAsync(HttpClient admin, int megabytes) =>
        await ExpectAsync(HttpStatusCode.NoContent, await admin.PutAsJsonAsync("/api/admin/settings",
            new { changes = new[] { new { key = SettingKeys.ReportingMaxAttachmentMegabytes, value = megabytes.ToString(CultureInfo.InvariantCulture) } } }, Json));
}
