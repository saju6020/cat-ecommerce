using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CatEcommerce.Application.Commands.Visit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Api.Background;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Notifications;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Commands;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-RPT-001 gate fix round (ARCHITECTURE, SECURITY and CODE_REVIEW, 2026-10-13; owner decisions the same day): the clinical-access log for R3 (view, both exports, each scheduled recipient), the
/// Accountant on the Sales reports (I-G6 re-proved with an Accountant recipient of R10), recipient branch rights when a schedule is saved and when it is sent, a merge chain A to B to C, the boarding
/// figure absent from the revenue tile, and the per-account concurrency limit. In the schedule collection: the scheduled cases run the worker's pass over the shared database.
/// </summary>
[Collection(ScheduleTestBase.CollectionName)]
public sealed class ReportingGateFixEndpointTests(ReportingApiFactory factory) : ScheduleTestBase(factory)
{
    private sealed class FailingClinicalAccessHandler : ICommandHandlerAsync<RecordClinicalRecordAccessCommand>
    {
        public Task<CommandResponse> HandleAsync(RecordClinicalRecordAccessCommand command) => throw new InvalidOperationException("The audit store is down.");
    }

    /// <summary>Enlists the first request (as the real outbox does), then fails on the second: the case where some requests are tracked when the handler throws.</summary>
    private sealed class ThrowOnSecondOutbox(INotificationOutbox inner) : INotificationOutbox
    {
        private int _count;

        public void Enqueue(NotificationRequestDto request)
        {
            if (++_count == 2) throw new InvalidOperationException("The outbox failed.");
            inner.Enqueue(request);
        }
    }

    private Task<int> ClinicalReadsAsync(Guid actorId) => AuditCountAsync("ClinicalRecordRead", actorId);

    // ── NFR-SEC-8: R3 carries clinical text, so a view, a CSV, a PDF and each scheduled recipient write a clinical-access row ──────────────
    [Fact]
    public async Task R3_AViewAndBothExports_EachWriteOneClinicalAccessRow_AndOtherCategoriesWriteNone()
    {
        var (context, vet) = await ContextWithRoleAsync(StaffRoles.Vet);
        var vetId = StaffIdOf(context, StaffRoles.Vet);
        var before = await ClinicalReadsAsync(vetId);

        await ExpectAsync(HttpStatusCode.OK, await vet.GetAsync($"/api/staff/reports/{ReportCatalogue.SurgeryRegisterKey}" + Query(context, Today.AddDays(-30), Today)));
        Assert.Equal(before + 1, await ClinicalReadsAsync(vetId));

        await ExpectAsync(HttpStatusCode.OK, await vet.GetAsync($"/api/staff/reports/{ReportCatalogue.SurgeryRegisterKey}/export.csv" + Query(context, Today.AddDays(-30), Today)));
        await ExpectAsync(HttpStatusCode.OK, await vet.GetAsync($"/api/staff/reports/{ReportCatalogue.SurgeryRegisterKey}/export.pdf" + Query(context, Today.AddDays(-30), Today)));
        Assert.Equal(before + 3, await ClinicalReadsAsync(vetId));

        // A refused request writes nothing, and a non-clinical report writes no clinical-access row.
        await vet.GetAsync($"/api/staff/reports/{ReportCatalogue.SurgeryRegisterKey}" + $"?from={Today:yyyy-MM-dd}&to={Today.AddDays(-5):yyyy-MM-dd}&branchId={context.BranchId}");
        await ExpectAsync(HttpStatusCode.OK, await vet.GetAsync($"/api/staff/reports/{ReportCatalogue.ClinicDailyScheduleKey}" + Query(context, Today, Today)));
        Assert.Equal(before + 3, await ClinicalReadsAsync(vetId));
    }

    [Fact]
    public async Task R3_IfTheAccessRowCannotBeWritten_NothingIsReleased_AndTheAnswerIsABare500()
    {
        using var failing = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddScoped<ICommandHandlerAsync<RecordClinicalRecordAccessCommand>, FailingClinicalAccessHandler>()));
        var (context, vet) = await ContextWithRoleAsync(StaffRoles.Vet, failing);

        foreach (var path in new[] { "", "/export.csv", "/export.pdf" })
        {
            var response = await vet.GetAsync($"/api/staff/reports/{ReportCatalogue.SurgeryRegisterKey}{path}" + Query(context, Today.AddDays(-30), Today));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.DoesNotContain("rows", await response.Content.ReadAsStringAsync());
        }

        // A report of another category is unaffected.
        await ExpectAsync(HttpStatusCode.OK, await vet.GetAsync($"/api/staff/reports/{ReportCatalogue.ClinicDailyScheduleKey}" + Query(context, Today, Today)));
    }

    [Fact]
    public async Task R3_AScheduledRun_WritesOneClinicalAccessRowForEachRecipient()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var vet = await SeedRecipientAsync(context, StaffRoles.Vet);
        var administrator = await SeedRecipientAsync(context, StaffRoles.Administrator);
        var beforeVet = await ClinicalReadsAsync(vet.Id);
        var beforeAdmin = await ClinicalReadsAsync(administrator.Id);

        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportCatalogue.SurgeryRegisterKey, "PreviousMonth", "10:00", [vet.Id, administrator.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();

            var admin = await FreshAdminAsync(context);
            Assert.Equal("Generated", Assert.Single(await RunsAsync(admin, created.ScheduleId)).GetProperty("status").GetString());
            Assert.Equal(beforeVet + 1, await ClinicalReadsAsync(vet.Id));
            Assert.Equal(beforeAdmin + 1, await ClinicalReadsAsync(administrator.Id));
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── A failure while the emails are being enlisted leaves nothing behind (gate re-check row 57) ───────────────────────────────────────────
    [Fact]
    public async Task AFailureWhileEnlistingTheEmails_PersistsNoRequest_RemovesTheFile_AndRecordsAFailedRun()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var first = await SeedRecipientAsync(context, StaffRoles.Reception);
        var second = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [first.Id, second.Id]));
        try
        {
            using var failing = Factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
                s.AddScoped<INotificationOutbox>(sp => new ThrowOnSecondOutbox(ActivatorUtilities.CreateInstance<EfNotificationOutbox>(sp)))));
            var filesBefore = await WithDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.StoredFiles, f => f.Purpose == FilePurpose.ReportExport));

            await AdvanceToAsync(created.NextRunOn!.Value);
            await using (var scope = failing.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<ReportingWorkCycle>().RunAsync();

            var admin = await FreshAdminAsync(context);
            var run = Assert.Single(await RunsAsync(admin, created.ScheduleId));
            Assert.Equal("Failed", run.GetProperty("status").GetString());
            var runId = run.GetProperty("runId").GetGuid();

            Assert.Empty(await RequestsForRunAsync(runId));                               // the request enlisted before the failure was not persisted
            Assert.Equal(filesBefore, await WithDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.StoredFiles, f => f.Purpose == FilePurpose.ReportExport)));
            Assert.Empty(EmailsTo(first.Email));
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── Owner decision: the Accountant holds Reports.Sales.View; I-G6 re-proved with an Accountant recipient of R10 ──────────────────────
    [Fact]
    public async Task I_G6_AnAccountantCanBeARecipientOfR10_AndTheCostColumnsGoOnlyWhenEveryRecipientMaySeeThem()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var shopManager = await SeedRecipientAsync(context, StaffRoles.ShopManager);
        var accountant = await SeedRecipientAsync(context, StaffRoles.Accountant);
        var administrator = await SeedRecipientAsync(context, StaffRoles.Administrator);

        var mixed = await CreateScheduleAsync(context.Admin, Body(context, ReportCatalogue.RevenueShopByCategoryKey, "PreviousMonth", "10:00", [shopManager.Id, accountant.Id], name: "Shop + accountant"));
        var financial = await CreateScheduleAsync(context.Admin, Body(context, ReportCatalogue.RevenueShopByCategoryKey, "PreviousMonth", "10:00", [accountant.Id, administrator.Id], name: "Accountant + admin"));
        try
        {
            await AdvanceToAsync(mixed.NextRunOn!.Value);
            await RunReportingCycleAsync();
            await RunDeliveryPassesUntilAsync(() => Task.FromResult(EmailsTo(shopManager.Email).Count > 0 && EmailsTo(administrator.Email).Count > 0));
            await RunDeliveryPassesUntilAsync(() => Task.FromResult(EmailsTo(accountant.Email).Count >= 2));

            // ShopManager + Accountant: the ShopManager cannot see cost, so nobody receives it.
            var shopCsv = Encoding.UTF8.GetString(Assert.Single(EmailsTo(shopManager.Email)).AttachmentList.Single().Content);
            Assert.DoesNotContain("Cost of goods", shopCsv);
            Assert.DoesNotContain("Margin", shopCsv);

            // Accountant + Administrator: both hold the financial action, so the cost columns are there.
            var adminCsv = Encoding.UTF8.GetString(Assert.Single(EmailsTo(administrator.Email)).AttachmentList.Single().Content);
            Assert.Contains("Cost of goods", adminCsv);

            // The accountant received both files, one without and one with.
            var accountantFiles = EmailsTo(accountant.Email).Select(m => Encoding.UTF8.GetString(m.AttachmentList.Single().Content)).ToList();
            Assert.Contains(accountantFiles, csv => !csv.Contains("Cost of goods"));
            Assert.Contains(accountantFiles, csv => csv.Contains("Cost of goods"));
        }
        finally
        {
            await CancelAsync(context.Admin, mixed.ScheduleId);
            await CancelAsync(context.Admin, financial.ScheduleId);
        }
    }

    // ── Recipient branch rights: when the schedule is saved, and again when it is sent ─────────────────────────────────────────────────────
    [Fact]
    public async Task AnAllBranchesSchedule_NeedsRecipientsWhoMayOpenEveryBranch_AtSaveTime()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var vet = await SeedRecipientAsync(context, StaffRoles.Vet);          // may open clinic operations, but not every branch
        var administrator = await SeedRecipientAsync(context, StaffRoles.Administrator);

        object AllBranches(Guid recipient) => new
        {
            name = "All branches", reportKey = ReportCatalogue.ClinicDailyScheduleKey, branchId = (Guid?)null, period = "Today", format = "Csv", frequency = "Daily", sendAtLocal = "10:00",
            recipientIds = new[] { recipient }, parameters = new Dictionary<string, string>()
        };

        var refused = await context.Admin.PostAsJsonAsync("/api/admin/report-schedules", AllBranches(vet.Id), Json);
        await ExpectAsync(HttpStatusCode.BadRequest, refused);
        Assert.Contains(BusinessViolationCodes.ReportRecipientNotPermitted, await CodesAsync(refused));

        var created = await CreateScheduleAsync(context.Admin, AllBranches(administrator.Id));
        await CancelAsync(context.Admin, created.ScheduleId);

        // The picker offers only people who could open every branch, and is bounded (the shared test database holds hundreds of administrators, so the first 200 by name are returned).
        var eligible = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/admin/report-schedules/recipients?reportKey={ReportCatalogue.ClinicDailyScheduleKey}")));
        var people = eligible.EnumerateArray().ToList();
        Assert.InRange(people.Count, 1, 200);
        Assert.DoesNotContain(people, p => p.GetProperty("id").GetGuid() == vet.Id);
        Assert.All(people, p => Assert.Contains(p.GetProperty("roles").EnumerateArray().Select(r => r.GetString()), r => r is StaffRoles.Administrator or StaffRoles.Accountant));
    }

    [Fact]
    public async Task TheSavePath_AppliesCanReceive_SoTheActionAndTheCrossBranchRightMayComeFromDifferentRoles()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        // A vet (holds the clinical action) who is also an accountant (a cross-branch role): CanReceive says yes for an all-branches R3, and so does the save path.
        var both = await SeedStaffWithEmailAsync(StaffRoles.Vet, StaffRoles.Accountant);
        await AssignAsync(context.Admin, both.Id, context.BranchId);

        var created = await CreateScheduleAsync(context.Admin, new
        {
            name = "Both roles", reportKey = ReportCatalogue.SurgeryRegisterKey, branchId = (Guid?)null, period = "PreviousMonth", format = "Csv", frequency = "Daily", sendAtLocal = "10:00",
            recipientIds = new[] { both.Id }, parameters = new Dictionary<string, string>()
        });
        await CancelAsync(context.Admin, created.ScheduleId);

        // ... and the picker for a branch offers them too (it applies the same rule).
        var eligible = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await context.Admin.GetAsync($"/api/admin/report-schedules/recipients?reportKey={ReportCatalogue.SurgeryRegisterKey}&branchId={context.BranchId}")));
        Assert.Contains(eligible.EnumerateArray(), e => e.GetProperty("id").GetGuid() == both.Id);
    }

    [Fact]
    public async Task ARecipientTakenOffTheBranchBeforeTheSend_NeverGetsTheEmail()
    {
        await RetireOtherSchedulesAsync();
        var context = await CreateBillingContextAsync();
        var elsewhere = await CreateBillingContextAsync();
        var reception = await SeedRecipientAsync(context, StaffRoles.Reception);
        var created = await CreateScheduleAsync(context.Admin, Body(context, ReportingFixtures.RowsKey, "Today", "10:00", [reception.Id]));
        try
        {
            await AdvanceToAsync(created.NextRunOn!.Value);
            await RunReportingCycleAsync();
            var admin = await FreshAdminAsync(context);
            var runId = Assert.Single(await RunsAsync(admin, created.ScheduleId)).GetProperty("runId").GetGuid();

            // The receptionist is moved to another branch (the assignment replaces the old one) before the email goes.
            await ReSignInAsync(elsewhere);
            await AssignAsync(elsewhere.Admin, reception.Id, elsewhere.BranchId);
            Assert.False(await GuardAsync(runId, reception.Id));

            await RunDeliveryPassesUntilAsync(async () => (await RequestsForRunAsync(runId)).All(s => s != "Pending"));
            Assert.Empty(EmailsTo(reception.Email));

            // And the detail read says so.
            var detail = await GetScheduleAsync(await FreshAdminAsync(context), created.ScheduleId);
            Assert.False(Assert.Single(detail.GetProperty("recipients").EnumerateArray()).GetProperty("stillPermitted").GetBoolean());
        }
        finally
        {
            await CancelAsync(context.Admin, created.ScheduleId);
        }
    }

    // ── A merge chain A -> B -> C is one customer ───────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_F7_AMergeChain_IsFollowedToTheFinalSurvivor_InTheGrowthReportsAndTheAgeing()
    {
        var context = await CreateBillingContextAsync();
        var (clientA, a) = await RegisterAndSignInCustomerAsync();
        var (clientB, b) = await RegisterAndSignInCustomerAsync();
        var (clientC, c) = await RegisterAndSignInCustomerAsync();
        var ab = NewMobile();
        var bc = NewMobile();
        await ExpectAsync(HttpStatusCode.NoContent, await clientA.PutAsJsonAsync("/api/customers/me", new { fullName = "Chain A", additionalPhoneNumbers = new[] { ab } }, Json));
        await ExpectAsync(HttpStatusCode.NoContent, await clientB.PutAsJsonAsync("/api/customers/me", new { fullName = "Chain B", additionalPhoneNumbers = new[] { ab, bc } }, Json));
        await ExpectAsync(HttpStatusCode.NoContent, await clientC.PutAsJsonAsync("/api/customers/me", new { fullName = "Chain C", additionalPhoneNumbers = new[] { bc } }, Json));

        await IssueSimpleInvoiceAsync(context, a, 100m);
        await IssueSimpleInvoiceAsync(context, b, 200m);
        await IssueSimpleInvoiceAsync(context, c, 300m);

        // A is merged into B, and later B (which now carries A's history) is merged into C.
        await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync($"/api/staff/customers/{a}/merge", new { survivingCustomerId = b, reason = "Same person" }, Json));
        await ExpectAsync(HttpStatusCode.OK, await context.Admin.PostAsJsonAsync($"/api/staff/customers/{b}/merge", new { survivingCustomerId = c, reason = "Same person again" }, Json));

        var from = new DateOnly(Today.Year, Today.Month, 1);
        var active = await RunReportAsync(context.Admin, ReportCatalogue.CustomersNewVsReturningKey, context, "groupBy=Month", from, Today);
        Assert.Equal(1, Rows(active).Single().GetProperty("activeCustomers").GetInt64());

        var ltv = await RunReportAsync(context.Admin, ReportCatalogue.CustomerLifetimeValueKey, context, from: from, to: Today);
        var top = Assert.Single(Rows(ltv), r => r.GetProperty("section").GetString() == "Top 50");
        Assert.Equal(600m, decimal.Parse(top.GetProperty("averageLtv").GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture));

        // The ageing shows ONE customer for the three unpaid invoices (the branch is this test's own).
        var ageing = await RunReportAsync(context.Admin, ReportCatalogue.ReceivablesAgeingKey, context, from: from, to: Today);
        var rows = Rows(ageing);
        Assert.Single(rows);
        Assert.Equal("Chain C", rows[0].GetProperty("customerName").GetString());
    }

    // ── Deferred sources are absent, not zero ─────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TheRevenueTile_HasNoBoardingFigure_WhileBoardingIsNotBuilt()
    {
        var (_, administrator) = await ContextWithRoleAsync(StaffRoles.Administrator);
        var dashboard = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await administrator.GetAsync("/api/staff/dashboard")));

        var tile = dashboard.GetProperty("tiles").EnumerateArray().Single(t => t.GetProperty("key").GetString() == "revenue.today-by-line");
        var keys = tile.GetProperty("figures").EnumerateArray().Select(f => f.GetProperty("key").GetString()).ToList();
        Assert.Contains("clinic", keys);
        Assert.Contains("shop", keys);
        Assert.DoesNotContain("boarding", keys);
    }

    // ── Owner decision: a per-account limit, so one user cannot lock everyone out ─────────────────────────────────────────────────────────
    [Fact]
    public async Task OneAccountsSlowReports_CannotFillTheHostWideLimit_AnotherAccountStillRuns()
    {
        using var narrowed = Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Reporting:MaxConcurrentReports"] = "4", ["Reporting:ConcurrencyQueueLimit"] = "0", ["Reporting:MaxConcurrentPerAccount"] = "1" })));
        var (contextA, adminA) = await ContextWithRoleAsync(StaffRoles.Administrator, narrowed);
        var (contextB, adminB) = await ContextWithRoleAsync(StaffRoles.Administrator, narrowed);

        var firstA = adminA.GetAsync($"/api/staff/reports/{ReportingFixtures.SleepKey}" + Query(contextA, Today, Today, "seconds=3"));
        await Task.Delay(700);
        var secondA = await adminA.GetAsync($"/api/staff/reports/{ReportingFixtures.SleepKey}" + Query(contextA, Today, Today, "seconds=1"));
        var firstB = await adminB.GetAsync($"/api/staff/reports/{ReportingFixtures.SleepKey}" + Query(contextB, Today, Today, "seconds=1"));

        Assert.Equal(HttpStatusCode.TooManyRequests, secondA.StatusCode);   // account A has its one slot taken
        Assert.Equal("5", secondA.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ToString());
        Assert.Equal(HttpStatusCode.OK, firstB.StatusCode);                  // account B is not locked out
        Assert.Equal(HttpStatusCode.OK, (await firstA).StatusCode);
    }

    [Fact]
    public void TheTestDatabaseGuard_RefusesUnsetDevTestAndSharedDatabases_AndAcceptsABranchDatabase()
    {
        Assert.Throws<InvalidOperationException>(() => ReportingTestDatabase.Validate(null));
        Assert.Throws<InvalidOperationException>(() => ReportingTestDatabase.Validate("  "));
        Assert.Throws<InvalidOperationException>(() => ReportingTestDatabase.Validate("not a connection string"));
        foreach (var name in new[] { "catecommerce_dev", "catecommerce_test", "CATECOMMERCE_TEST", "catecommerce_test_old_20261010", "catecommerce" })
            Assert.Throws<InvalidOperationException>(() => ReportingTestDatabase.Validate($"Host=localhost;Database={name};Username=u;Password=p"));

        Assert.NotNull(ReportingTestDatabase.Validate("Host=localhost;Database=catecommerce_rpt;Username=u;Password=p"));
    }
}
