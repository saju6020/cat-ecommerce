using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-RPT-001 section 16.1 -- every test creates its own branch (through HO-ADM-001's endpoints as an administrator), assigns its staff to it, and passes <c>branchId={that branch}</c> on every
/// report call, so no assertion depends on other tests' data. Source data is created through the owning area's own endpoints or port, never by inserting into its tables. Builds on
/// <see cref="BillingIntegrationTestBase"/> for the branch, staff, customer, clinic, charge, invoice and payment helpers; the fixture it adds is <see cref="ReportingApiFactory"/>.
/// </summary>
public abstract class ReportingIntegrationTestBase(ReportingApiFactory factory) : BillingIntegrationTestBase(factory), IClassFixture<ReportingApiFactory>
{
    protected ReportingApiFactory ReportingFactory => factory;

    /// <summary>The query string of a report call at <paramref name="context"/>'s branch for an inclusive local date range.</summary>
    protected static string Query(BillingContext context, DateOnly from, DateOnly to, string? extra = null) =>
        $"?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&branchId={context.BranchId}" + (string.IsNullOrEmpty(extra) ? "" : "&" + extra);

    /// <summary>The branch-local date range a test-created record falls in: today, plus a day either side to absorb the fake clock's offset.</summary>
    protected (DateOnly From, DateOnly To) AroundToday() => (Today.AddDays(-1), Today.AddDays(1));

    protected async Task<JsonElement> RunReportAsync(HttpClient client, string key, BillingContext context, string? extra = null, DateOnly? from = null, DateOnly? to = null)
    {
        var (defaultFrom, defaultTo) = AroundToday();
        var response = await client.GetAsync($"/api/staff/reports/{key}" + Query(context, from ?? defaultFrom, to ?? defaultTo, extra));
        await ExpectAsync(HttpStatusCode.OK, response);
        return await ReadAsync(response);
    }

    /// <summary>The staff accounts this test seeded, by branch and role (for audit assertions).</summary>
    protected Dictionary<(Guid BranchId, string Role), Guid> StaffIds { get; } = [];

    /// <summary>
    /// A fresh branch (§16.1 rule 1) with a signed-in staff member of <paramref name="role"/> assigned to it and the active-branch header set. With <paramref name="host"/> the client talks to a derived host
    /// (a lowered cap, timeout or concurrency limit); the staff and the branch live in the database, so the derived host sees them.
    /// </summary>
    protected async Task<(BillingContext Context, HttpClient Client)> ContextWithRoleAsync(string role, Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>? host = null)
    {
        var context = await CreateBillingContextAsync();
        if (role == StaffRoles.Administrator && host is null)
        {
            StaffIds[(context.BranchId, role)] = context.AdminStaffId;
            return (context, context.Admin);
        }

        var staff = await SeedStaffAsync(role);
        await AssignAsync(context.Admin, staff.StaffAccountId, context.BranchId);
        StaffIds[(context.BranchId, role)] = staff.StaffAccountId;

        if (host is null) return (context, await SignInStaffAsync(staff.Mobile, staff.Password, context.BranchId));

        var client = host.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost/"), HandleCookies = true });
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = staff.Mobile, password = staff.Password }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));
        client.DefaultRequestHeaders.Add(CatEcommerce.Api.Branches.HttpContextActiveBranchAccessor.BranchHeaderName, context.BranchId.ToString());
        return (context, client);
    }

    protected Guid StaffIdOf(BillingContext context, string role) => StaffIds[(context.BranchId, role)];

    /// <summary>The rows of a result keyed by the value of one column.</summary>
    protected static List<JsonElement> Rows(JsonElement report) => report.GetProperty("rows").EnumerateArray().ToList();

    protected static string[] ColumnKeys(JsonElement report) => report.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("key").GetString()!).ToArray();

    protected static async Task<string[]> CodesAsync(HttpResponseMessage response)
    {
        var body = await ReadAsync(response);
        return body.TryGetProperty("errors", out var errors) ? errors.EnumerateArray().Select(e => e.GetProperty("errorCode").GetString()!).ToArray() : [];
    }

    protected Task<int> AuditCountAsync(string action, Guid actorId) =>
        WithDbAsync(db => db.Database.SqlQuery<int>($"select count(*)::int as \"Value\" from audit.audit_entry where \"Action\" = {action} and \"ActorUserId\" = {actorId}").SingleAsync());
}
