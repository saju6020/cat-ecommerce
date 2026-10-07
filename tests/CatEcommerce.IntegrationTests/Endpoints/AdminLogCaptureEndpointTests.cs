using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate seventh pass: I-X1, previously BLOCKED. The TC sheet's own reasoning ("no log-capture
/// harness exists anywhere in the tree for either HO-ACC-001 or this handover") was itself wrong --
/// <c>CatEcommerceApiFactory.WithCapturedLogs()</c> already exists and is already used by
/// HO-ACC-001's own <c>SessionResolutionFailureTests</c>; this is that harness's first use for
/// Administration's own free-text fields. A representative set of flows that carry the free-text
/// fields §16's own I-X1 names (a policy body, a leave note and decision note, the maintenance
/// message, a branch's phone number) is run against a log-capturing factory, each with a unique,
/// greppable marker; none of those markers may appear anywhere in the captured log lines, at any
/// level -- broader than "Information+", since a marker at ANY level (including Debug) is still a
/// leak this test should catch.
/// </summary>
// HO-SHP-001 build (2026-10-04): sets and clears the GLOBAL maintenance mode, which makes every
// storefront endpoint answer 503 meanwhile -- serialised with the other global-setting tests.
[Collection(AdministrationGlobalSettingsCollection.Name)]
public sealed class AdminLogCaptureEndpointTests(AdministrationApiFactory factory) : AdministrationIntegrationTestBase(factory)
{
    private static string NewCode() => "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    private static string NewSlug() => "page-" + Guid.NewGuid().ToString("N")[..10];
    private static string NewKey() => "policy-" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task RepresentativeFlows_CarryingFreeTextFields_LeaveNoTraceInTheLogs()
    {
        var (capturingFactory, logs) = Factory.WithCapturedLogs();
        using var client = capturingFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = admin.Mobile, password = admin.Password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var marker = Guid.NewGuid().ToString("N")[..12];
        var policyBodyMarker = $"POLICYBODY-{marker}";
        var leaveNoteMarker = $"LEAVENOTE-{marker}";
        var decisionNoteMarker = $"DECISIONNOTE-{marker}";
        var maintenanceMessageMarker = $"MAINTENANCE-{marker}";
        var phoneMarker = $"PHONE-{marker}";

        // Policy body.
        var key = NewKey();
        var createPolicy = await client.PostAsJsonAsync("/api/admin/policies", new { key, title = "Log Capture Test Policy", requiresAcceptance = false }, Json);
        Assert.Equal(HttpStatusCode.Created, createPolicy.StatusCode);
        var publishPolicy = await client.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = policyBodyMarker, changeSummary = "Initial" }, Json);
        Assert.Equal(HttpStatusCode.Created, publishPolicy.StatusCode);

        // Leave note + decision note -- a separate requester, so the admin deciding is not a
        // self-approval (which would be refused unless the admin were the sole Administrator in a
        // database this shared test suite cannot guarantee).
        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var nurseClient = capturingFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });
        var nurseSignIn = await nurseClient.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = nurse.Mobile, password = nurse.Password }, Json);
        Assert.Equal(HttpStatusCode.OK, nurseSignIn.StatusCode);
        nurseClient.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(nurseSignIn, "pc_csrf"));

        var requestLeave = await nurseClient.PostAsJsonAsync("/api/staff/leave-requests", new
        {
            startsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20), endsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(21),
            kind = "Annual", note = leaveNoteMarker
        }, Json);
        Assert.Equal(HttpStatusCode.Created, requestLeave.StatusCode);
        var leaveRequestId = (await requestLeave.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();
        var approve = await client.PostAsJsonAsync($"/api/admin/leave-requests/{leaveRequestId}/approve", new { decisionNote = decisionNoteMarker }, Json);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        // Maintenance message (set, then immediately cleared).
        var setMaintenance = await client.PutAsJsonAsync("/api/admin/system/maintenance", new { maintenanceMode = true, message = maintenanceMessageMarker }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setMaintenance.StatusCode);
        var clearMaintenance = await client.PutAsJsonAsync("/api/admin/system/maintenance", new { maintenanceMode = false, message = (string?)null }, Json);
        Assert.Equal(HttpStatusCode.NoContent, clearMaintenance.StatusCode);

        // Branch phone number.
        var createBranch = await client.PostAsJsonAsync("/api/admin/branches", new
        {
            code = NewCode(), name = "Log Capture Test Branch", timeZoneId = "Asia/Dhaka", phoneNumber = phoneMarker
        }, Json);
        Assert.Equal(HttpStatusCode.Created, createBranch.StatusCode);

        var allLogText = string.Join("\n", logs);
        Assert.DoesNotContain(policyBodyMarker, allLogText);
        Assert.DoesNotContain(leaveNoteMarker, allLogText);
        Assert.DoesNotContain(decisionNoteMarker, allLogText);
        Assert.DoesNotContain(maintenanceMessageMarker, allLogText);
        Assert.DoesNotContain(phoneMarker, allLogText);
    }
}
