using System.Net;
using System.Net.Http.Json;
using System.Text;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.ReportingViews;
using CatEcommerce.Infrastructure.Reporting;
using CatEcommerce.Read.QueryHandlers.Reporting;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-RPT-001 section 16 A -- the report framework, tested through the fixture reports of <see cref="ReportingFixtures"/> so no other area's data is involved: I-A1 to I-A11, plus the
/// platform facts F-RPT-1 to F-RPT-6 observed in Phase A (the SQL ones are recorded in the TC sheet).
/// </summary>
public sealed class ReportingFoundationEndpointTests(ReportingApiFactory factory) : ReportingIntegrationTestBase(factory)
{
    // ── I-A1: the host starts with scope validation on, and every registration resolves ─────────────────────
    [Fact]
    public async Task I_A1_EveryReportSource_Resolves_AndMatchesTheCatalogueOneToOne()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var catalogue = scope.ServiceProvider.GetRequiredService<IReportCatalogue>();
        var sources = scope.ServiceProvider.GetServices<IReportSource>().ToList();

        // U-A2 at host level: exactly one source per catalogue key, and none extra.
        Assert.Equal(catalogue.All.Select(d => d.Key).Order(), sources.Select(s => s.Key).Order());
        Assert.Equal(sources.Count, sources.Select(s => s.Key).Distinct().Count());

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IReportingReadScope>());
        Assert.Equal(2, scope.ServiceProvider.GetServices<IReportExportRenderer>().Count());
    }

    // ── I-A2: the migrations built the schema and every view; no view is a table ───────────────────────────
    [Fact]
    public async Task I_A2_TheMigrations_CreatedEveryContractedView_AndNoViewIsATable()
    {
        var views = await WithDbAsync(db => db.Database.SqlQuery<string>($"select viewname as \"Value\" from pg_views where schemaname = 'report'").ToListAsync());
        var tables = await WithDbAsync(db => db.Database.SqlQuery<string>($"select tablename as \"Value\" from pg_tables where schemaname = 'report'").ToListAsync());

        Assert.Contains("rv_staff", views);
        Assert.Contains("rv_customer", views);
        Assert.Contains("rv_branch", views);
        Assert.Contains("rv_staff_branch", views);
        Assert.DoesNotContain(tables, t => t.StartsWith("rv_", StringComparison.Ordinal));

        // Every embedded view file is a view in the database (the named, versioned files of ADR-278).
        foreach (var name in ReportingViewSql.EmbeddedNames())
            Assert.Contains(name.Split('.')[0], views);
    }

    // ── I-A3: a write inside the scope fails in the database (ADR-279, F-RPT-1) ──────────────────────────────
    [Fact]
    public async Task I_A3_AWriteInsideTheReadOnlyScope_FailsWith25006_AndNothingIsWritten()
    {
        var (context, client) = await ContextWithRoleAsync(StaffRoles.Administrator);
        var before = await WithDbAsync(db => db.Database.SqlQuery<int>($"select count(*)::int as \"Value\" from admin.branch").SingleAsync());

        // Through the dispatcher the PostgresException reaches the caller untouched, so the SQLSTATE is observable.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.IInMemoryDispatcher>();
            var exception = await Assert.ThrowsAnyAsync<Exception>(() => dispatcher.GetResultAsync(new CatEcommerce.Read.Queries.Reporting.RunReportQuery
            {
                ReportKey = ReportingFixtures.WriteKey, From = Today, To = Today, BranchId = context.BranchId.ToString(), ActiveBranchId = context.BranchId,
                CallerRoles = [StaffRoles.Administrator], CallerId = context.AdminStaffId
            }));

            var postgres = exception as PostgresException ?? exception.InnerException as PostgresException ?? exception.InnerException?.InnerException as PostgresException;
            Assert.Equal("25006", postgres?.SqlState);
        }

        // Over HTTP the host answers its generic error shape, and nothing was written either way.
        var response = await client.GetAsync($"/api/staff/reports/{ReportingFixtures.WriteKey}" + Query(context, Today, Today));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var after = await WithDbAsync(db => db.Database.SqlQuery<int>($"select count(*)::int as \"Value\" from admin.branch").SingleAsync());
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task I_A3_TheScope_NeverCommits_AnythingItsWorkChanged()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var readScope = scope.ServiceProvider.GetRequiredService<IReportingReadScope>();

        var inTransaction = await readScope.RunAsync(async (db, ct) =>
            await db.Database.SqlQuery<bool>($"select (current_setting('transaction_read_only') = 'on') as \"Value\"").SingleAsync(ct), CancellationToken.None);

        Assert.True(inTransaction);
    }

    // ── I-A4 / NFR-PRF-7: a runaway report is cut off, and interactive work is unaffected ─────────────────
    [Fact]
    public async Task I_A4_ASlowReport_TimesOut_AsAn400_WhileAnAppointmentEndpointAnswersNormally()
    {
        using var lowered = Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["Reporting:QueryTimeoutSeconds"] = "5" })));
        var (context, admin) = await ContextWithRoleAsync(StaffRoles.Administrator, lowered);

        var slow = admin.GetAsync($"/api/staff/reports/{ReportingFixtures.SleepKey}" + Query(context, Today, Today, "seconds=12"));
        var appointments = await admin.GetAsync($"/api/staff/appointments/day?date={Today:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, appointments.StatusCode);
        var response = await slow;
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(BusinessViolationCodes.ReportTimedOut, await CodesAsync(response));
    }

    // ── I-A5: the catalogue is filtered to the caller ───────────────────────────────────────────────────────
    [Fact]
    public async Task I_A5_EachRole_SeesExactlyItsCategories_ANurseIsRefused_AnAnonymousCallerIs401()
    {
        foreach (var role in StaffRoles.All)
        {
            var (_, client) = await ContextWithRoleAsync(role);
            var response = await client.GetAsync("/api/staff/reports");

            if (role == StaffRoles.Nurse)
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                continue;
            }

            await ExpectAsync(HttpStatusCode.OK, response);
            var keys = (await ReadAsync(response)).EnumerateArray().Select(e => e.GetProperty("key").GetString()!).Order().ToArray();
            var expected = AllDefinitions().Where(d => ReportAccessPolicy.CanOpen(d, [role])).Select(d => d.Key).Order().ToArray();
            Assert.Equal(expected, keys);
        }

        var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/staff/reports")).StatusCode);

        var (customer, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/staff/reports")).StatusCode);
    }

    [Fact]
    public async Task I_A5_TheCatalogue_ResolvesTheDefaultRangeOnTheServer_AndHidesRestrictedColumns()
    {
        var (_, reception) = await ContextWithRoleAsync(StaffRoles.Reception);
        var (_, accountant) = await ContextWithRoleAsync(StaffRoles.Accountant);

        var entry = (await ReadAsync(await reception.GetAsync("/api/staff/reports"))).EnumerateArray().Single(e => e.GetProperty("key").GetString() == ReportingFixtures.RowsKey);

        Assert.Equal("Today", entry.GetProperty("defaultPeriod").GetString());
        Assert.Equal(entry.GetProperty("defaultRange").GetProperty("from").GetString(), entry.GetProperty("defaultRange").GetProperty("to").GetString());
        Assert.False(entry.GetProperty("canUseAllBranches").GetBoolean());
        Assert.False(entry.GetProperty("canSchedule").GetBoolean());
        Assert.DoesNotContain("secret", entry.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("key").GetString()));
        Assert.Equal(["Csv", "Pdf"], entry.GetProperty("formats").EnumerateArray().Select(f => f.GetString()));

        // The accountant is not in ClinicOperations at all, so the fixture is not in their catalogue.
        Assert.DoesNotContain(ReportingFixtures.RowsKey, (await ReadAsync(await accountant.GetAsync("/api/staff/reports"))).EnumerateArray().Select(e => e.GetProperty("key").GetString()));
    }

    // ── I-A6: every report key x every staff role ──────────────────────────────────────────────────────────
    [Fact]
    public async Task I_A6_EveryReport_ForEveryRole_Is200WhereAllowed_Else403WithOneDenialAudit_AnUnknownKeyIs404()
    {
        foreach (var role in StaffRoles.All)
        {
            var (context, client) = await ContextWithRoleAsync(role);
            var staffId = StaffIdOf(context, role);

            foreach (var definition in AllDefinitions().Where(d => !IsHeavyFixture(d)))
            {
                var allowed = ReportAccessPolicy.CanOpen(definition, [role]) && ReportAccessPolicy.Holds([role], Actions.Reports.Any);
                var before = await AuditCountAsync("AuthorizationDenied", staffId);

                var response = await client.GetAsync($"/api/staff/reports/{definition.Key}" + Query(context, Today.AddDays(-1), Today));

                if (allowed)
                {
                    Assert.True(response.StatusCode == HttpStatusCode.OK, $"{role} / {definition.Key}: {(int)response.StatusCode} {await BodyAsync(response)}");
                    continue;
                }

                Assert.True(response.StatusCode == HttpStatusCode.Forbidden, $"{role} / {definition.Key}: expected 403, got {(int)response.StatusCode}");
                Assert.Contains(BusinessViolationCodes.ActionNotPermitted, await CodesAsync(response));
                Assert.Equal(before + 1, await AuditCountAsync("AuthorizationDenied", staffId));
            }

            if (ReportAccessPolicy.Holds([role], Actions.Reports.Any))
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/staff/reports/no-such-report" + Query(context, Today, Today))).StatusCode);
        }
    }

    // ── I-A7 / I-A8: CSV and PDF of every report, audited ──────────────────────────────────────────────────
    [Fact]
    public async Task I_A7_TheCsvExport_OfEveryReport_HasABom_TheVisibleLabels_AFileName_NoStore_AndOneAuditRow()
    {
        var (context, admin) = await ContextWithRoleAsync(StaffRoles.Administrator);
        var adminId = StaffIdOf(context, StaffRoles.Administrator);

        foreach (var definition in AllDefinitions().Where(d => !IsHeavyFixture(d)))
        {
            var before = await AuditCountAsync("ReportExported", adminId);
            var response = await admin.GetAsync($"/api/staff/reports/{definition.Key}/export.csv" + Query(context, Today.AddDays(-1), Today));
            await ExpectAsync(HttpStatusCode.OK, response);

            Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
            Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
            Assert.Equal($"purrcare-{definition.Key}-{Today.AddDays(-1):yyyy-MM-dd}-to-{Today:yyyy-MM-dd}.csv", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
            Assert.Equal("attachment", response.Content.Headers.ContentDisposition.DispositionType);

            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
            var header = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).Split("\r\n")[0];
            foreach (var column in definition.Columns.Where(c => c.RestrictedToAction is null || ReportAccessPolicy.Holds([StaffRoles.Administrator], c.RestrictedToAction)))
                Assert.Contains($"\"{column.Label}\"", header);

            Assert.Equal(before + 1, await AuditCountAsync("ReportExported", adminId));
        }
    }

    [Fact]
    public async Task I_A7_TheAuditRow_HoldsTheFiltersAndTheColumnKeys_NeverARowValue()
    {
        var (context, admin) = await ContextWithRoleAsync(StaffRoles.Administrator);
        await ExpectAsync(HttpStatusCode.OK, await admin.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}/export.csv" + Query(context, Today, Today, "rows=5")));

        var afterValue = await WithDbAsync(db => db.Database.SqlQuery<string>(
            $"select \"AfterValue\" as \"Value\" from audit.audit_entry where \"Action\" = 'ReportExported' and \"AfterValue\" like {"%" + context.BranchId + "%"} order by \"OccurredOn\" desc limit 1").SingleAsync());

        Assert.Contains(ReportingFixtures.RowsKey, afterValue);
        Assert.Contains("\"rowCount\":5", afterValue);
        Assert.Contains("\"columnKeys\":[\"name\",\"amount\",\"secret\",\"count\"]", afterValue);
        Assert.DoesNotContain("HYPERLINK", afterValue); // a row value
        Assert.DoesNotContain("Row 3", afterValue);
    }

    [Fact]
    public async Task I_A8_ThePdfExport_OfEveryReport_IsAPdf_WithItsTitleAndBranch()
    {
        var (context, admin) = await ContextWithRoleAsync(StaffRoles.Administrator);

        foreach (var definition in AllDefinitions().Where(d => !IsHeavyFixture(d)))
        {
            var response = await admin.GetAsync($"/api/staff/reports/{definition.Key}/export.pdf" + Query(context, Today.AddDays(-1), Today));
            await ExpectAsync(HttpStatusCode.OK, response);

            Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
            Assert.Contains(definition.Title, PdfTextExtractor.Extract(bytes));
        }
    }

    // ── I-A9: over the cap -> 413, and nothing is audited ──────────────────────────────────────────────────
    [Fact]
    public async Task I_A9_AReportOverTheCap_Is413ForCsvAndPdf_AndWritesNoAuditRow()
    {
        using var lowered = Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Reporting:CsvMaxRows"] = "3", ["Reporting:PdfMaxRows"] = "3" })));
        var (context, admin) = await ContextWithRoleAsync(StaffRoles.Administrator, lowered);
        var adminId = StaffIdOf(context, StaffRoles.Administrator);

        var csv = await admin.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}/export.csv" + Query(context, Today, Today, "rows=4"));
        var pdf = await admin.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}/export.pdf" + Query(context, Today, Today, "rows=4"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, csv.StatusCode);
        Assert.Contains(BusinessViolationCodes.ReportTooLargeForCsv, await CodesAsync(csv));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, pdf.StatusCode);
        Assert.Contains(BusinessViolationCodes.ReportTooLargeForPdf, await CodesAsync(pdf));
        Assert.Equal(0, await AuditCountAsync("ReportExported", adminId));

        // U-A11: cap + 1 rows were requested from the source, not all of them.
        Assert.Equal(4, ReportingFixtures.RowLimitByRows[4]); // asked for rows=4 under a cap of 3: the source was given cap + 1

        // Exactly at the cap is fine.
        await ExpectAsync(HttpStatusCode.OK, await admin.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}/export.csv" + Query(context, Today, Today, "rows=3")));
    }

    // ── I-A10: range and branch rules ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_A10_ARangeOf367Days_ABranchNotAssigned_AllAsReception_AllAsAccountant()
    {
        var (context, admin) = await ContextWithRoleAsync(StaffRoles.Administrator);
        var key = ReportingFixtures.RowsKey;

        var tooLong = await admin.GetAsync($"/api/staff/reports/{key}?from={Today.AddDays(-366):yyyy-MM-dd}&to={Today:yyyy-MM-dd}&branchId={context.BranchId}");
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains(BusinessViolationCodes.ReportRangeTooLong, await CodesAsync(tooLong));

        await ExpectAsync(HttpStatusCode.OK, await admin.GetAsync($"/api/staff/reports/{key}?from={Today.AddDays(-365):yyyy-MM-dd}&to={Today:yyyy-MM-dd}&branchId={context.BranchId}"));

        var noRange = await admin.GetAsync($"/api/staff/reports/{key}?branchId={context.BranchId}");
        Assert.Contains(BusinessViolationCodes.ReportRangeRequired, await CodesAsync(noRange));

        var inverted = await admin.GetAsync($"/api/staff/reports/{key}?from={Today:yyyy-MM-dd}&to={Today.AddDays(-1):yyyy-MM-dd}&branchId={context.BranchId}");
        Assert.Contains(BusinessViolationCodes.ReportRangeInverted, await CodesAsync(inverted));

        var stranger = Guid.NewGuid();
        var unassigned = await admin.GetAsync($"/api/staff/reports/{key}?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&branchId={stranger}");
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        Assert.Contains(BusinessViolationCodes.BranchNotAssigned, await CodesAsync(unassigned));

        var unknownParameter = await admin.GetAsync($"/api/staff/reports/{key}" + Query(context, Today, Today, "mystery=1"));
        Assert.Contains(BusinessViolationCodes.UnknownReportParameter, await CodesAsync(unknownParameter));

        var (receptionContext, reception) = await ContextWithRoleAsync(StaffRoles.Reception);
        var all = await reception.GetAsync($"/api/staff/reports/{key}?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&branchId=all");
        Assert.Equal(HttpStatusCode.Forbidden, all.StatusCode);
        Assert.Contains(BusinessViolationCodes.CrossBranchReportNotPermitted, await CodesAsync(all));
        Assert.NotEqual(Guid.Empty, receptionContext.BranchId);

        var (_, accountant) = await ContextWithRoleAsync(StaffRoles.Accountant);
        var accountantAll = await accountant.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&branchId=all");
        // The accountant holds no ClinicOperations action, so this is the category check, not the branch check.
        Assert.Equal(HttpStatusCode.Forbidden, accountantAll.StatusCode);

        var adminAll = await admin.GetAsync($"/api/staff/reports/{key}?from={Today:yyyy-MM-dd}&to={Today:yyyy-MM-dd}&branchId=all");
        await ExpectAsync(HttpStatusCode.OK, adminAll);
        var body = await ReadAsync(adminAll);
        Assert.Equal("All branches", body.GetProperty("filters").GetProperty("branchName").GetString());
        Assert.Equal(JsonValueKindNull, body.GetProperty("filters").GetProperty("branchId").ValueKind);
    }

    private static readonly System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;

    // ── The result shape of section 12.4, and the column restriction of ADR-281 ──────────────────────────
    [Fact]
    public async Task TheResult_HasTypedColumns_StringMoney_Totals_AndPaging()
    {
        var (context, admin) = await ContextWithRoleAsync(StaffRoles.Administrator);

        var report = await RunReportAsync(admin, ReportingFixtures.RowsKey, context, "rows=5&pageSize=2&pageNumber=2&sort=-amount");

        Assert.Equal(5, report.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, report.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, report.GetProperty("pageNumber").GetInt32());
        var rows = Rows(report);
        Assert.Equal(2, rows.Count);
        Assert.Equal("Row 3", rows[0].GetProperty("name").GetString()); // sorted by amount descending: 5, 4, | 3, 2 | 1
        Assert.Equal("31.50", rows[0].GetProperty("amount").GetProperty("amount").GetString()); // money is a string
        Assert.Equal("BDT", rows[0].GetProperty("amount").GetProperty("currencyCode").GetString());
        Assert.Equal(JsonValueKindNumber, rows[0].GetProperty("count").ValueKind); // integers are numbers
        Assert.Equal("157.50", report.GetProperty("totals").GetProperty("amount").GetProperty("amount").GetString());
        Assert.Equal(context.BranchId, report.GetProperty("filters").GetProperty("branchId").GetGuid());
        Assert.Equal("Asia/Dhaka", report.GetProperty("filters").GetProperty("timeZoneId").GetString());
    }

    private static readonly System.Text.Json.JsonValueKind JsonValueKindNumber = System.Text.Json.JsonValueKind.Number;

    [Fact]
    public async Task ARestrictedColumn_IsRemovedFromTheJson_TheTotals_AndTheCsv_NotBlanked()
    {
        var (receptionContext, reception) = await ContextWithRoleAsync(StaffRoles.Reception);
        var (adminContext, admin) = await ContextWithRoleAsync(StaffRoles.Administrator);

        var restricted = await RunReportAsync(reception, ReportingFixtures.RowsKey, receptionContext, "rows=2");
        var full = await RunReportAsync(admin, ReportingFixtures.RowsKey, adminContext, "rows=2");

        Assert.DoesNotContain("secret", ColumnKeys(restricted));
        Assert.All(Rows(restricted), r => Assert.False(r.TryGetProperty("secret", out _)));
        Assert.False(restricted.GetProperty("totals").TryGetProperty("secret", out _));
        Assert.Contains("secret", ColumnKeys(full));
        Assert.True(full.GetProperty("totals").TryGetProperty("secret", out _));

        var csv = await reception.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}/export.csv" + Query(receptionContext, Today, Today, "rows=2"));
        var text = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
        Assert.DoesNotContain("Secret", text);
    }

    [Fact]
    public async Task TheCsv_GuardsAFormulaCell_KeepsBengali_AndWritesMoneyAsPlainNumbers()
    {
        var (context, admin) = await ContextWithRoleAsync(StaffRoles.Administrator);
        var response = await admin.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}/export.csv" + Query(context, Today, Today, "rows=3"));
        var text = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync()).TrimStart('﻿');
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("\"Name\",\"Amount\",\"Secret\",\"Count\",\"Currency\"", lines[0]);
        Assert.StartsWith("\"'=HYPERLINK(", lines[1]);
        Assert.Contains(lines, line => line.Contains("বিড়াল")); // the default sort is by name
        Assert.Contains(",10.50,", lines[1]);
        Assert.EndsWith(",\"BDT\"", lines[1]);
    }

    [Fact]
    public async Task ASortOnAColumnTheCallerCannotSee_OrANonColumn_IsAnInvalidParameter()
    {
        var (context, reception) = await ContextWithRoleAsync(StaffRoles.Reception);

        var hidden = await reception.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}" + Query(context, Today, Today, "sort=secret"));
        var nonsense = await reception.GetAsync($"/api/staff/reports/{ReportingFixtures.RowsKey}" + Query(context, Today, Today, "sort=nope"));

        Assert.Equal(HttpStatusCode.BadRequest, hidden.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidReportParameter, await CodesAsync(hidden));
        Assert.Contains(BusinessViolationCodes.InvalidReportParameter, await CodesAsync(nonsense));
    }

    // ── I-A11: bounded concurrency (ADR-290) ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_A11_WithOneSlotAndNoQueue_TheSecondSlowReport_Is429WithRetryAfter_AndClinicEndpointsAreUnaffected()
    {
        using var narrowed = Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["Reporting:MaxConcurrentReports"] = "1", ["Reporting:ConcurrencyQueueLimit"] = "0" })));
        var (context, admin) = await ContextWithRoleAsync(StaffRoles.Administrator, narrowed);

        var first = admin.GetAsync($"/api/staff/reports/{ReportingFixtures.SleepKey}" + Query(context, Today, Today, "seconds=3"));
        await Task.Delay(700);
        var second = await admin.GetAsync($"/api/staff/reports/{ReportingFixtures.SleepKey}" + Query(context, Today, Today, "seconds=1"));
        var clinic = await admin.GetAsync($"/api/staff/appointments/day?date={Today:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("5", second.Headers.RetryAfter!.Delta!.Value.TotalSeconds.ToString());
        Assert.Equal(HttpStatusCode.OK, clinic.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await first).StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────
    private IReadOnlyList<ReportDefinition> AllDefinitions() => ReportCatalogue.All.Concat(ReportingFixtures.All).ToList();

    private static bool IsHeavyFixture(ReportDefinition definition) => definition.Key is ReportingFixtures.SleepKey or ReportingFixtures.WriteKey;
}
