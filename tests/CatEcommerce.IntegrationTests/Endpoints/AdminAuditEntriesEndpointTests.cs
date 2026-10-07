using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ADM-001 §16, Audit log (E). I-E1/I-E2/I-E3/I-E4.</summary>
public sealed class AdminAuditEntriesEndpointTests(AdministrationApiFactory factory) : AdministrationIntegrationTestBase(factory)
{
    [Fact]
    public async Task SearchByActor_FindsTheChangeItMade()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var code = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var create = await client.PostAsJsonAsync("/api/admin/branches", new { code, name = "Audit Test Branch", timeZoneId = "Asia/Dhaka" }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var search = await client.GetAsync($"/api/admin/audit-entries?actorUserId={admin.StaffAccountId}&entityType=BranchAggregate&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        var body = await search.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("actorUserId").GetGuid() == admin.StaffAccountId);
        Assert.Contains(items, i => i.GetProperty("actorDisplayName").GetString() == "Test Staff");
    }

    /// <summary>Gate fix 2026-09-26 (ARCH MEDIUM / SEC LOW): a lower-case category query parameter
    /// must be accepted AND correctly filter -- not just avoid a 500 (regression for a finding that
    /// survived one prior fix pass). The same sign-in that authenticates the caller already wrote
    /// an AuthenticationEvent row for the same actor in the same window, so filtering to DataChange
    /// only proves the filter, not just an empty result set.</summary>
    [Fact]
    public async Task SearchByCategory_LowerCase_Returns200AndFiltersCorrectly()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password); // writes an AuthenticationEvent row

        var code = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var create = await client.PostAsJsonAsync("/api/admin/branches", new { code, name = "Category Filter Test Branch", timeZoneId = "Asia/Dhaka" }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode); // writes a DataChange row

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var response = await client.GetAsync(
            $"/api/admin/audit-entries?actorUserId={admin.StaffAccountId}&entityType=BranchAggregate&category=datachange&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        Assert.All(items, i => Assert.Equal("DataChange", i.GetProperty("category").GetString()));
        Assert.Contains(items, i => i.GetProperty("entityType").GetString() == "BranchAggregate");
    }

    [Fact]
    public async Task UnrecognisedCategory_Returns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var to = DateTimeOffset.UtcNow.AddMinutes(1);
        var response = await client.GetAsync(
            $"/api/admin/audit-entries?category=NotACategory&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ToBeforeFrom_Returns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var from = DateTimeOffset.UtcNow;
        var to = DateTimeOffset.UtcNow.AddMinutes(-5);
        var response = await client.GetAsync(
            $"/api/admin/audit-entries?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MissingDateRange_Returns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var response = await client.GetAsync("/api/admin/audit-entries");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Gate fix 2026-09-26 (TEST gate seventh pass, item 4): renamed from
    /// <c>ExportCsv_ReturnsCsvAndAuditsTheExport</c> -- that name promised two things ("and audits
    /// the export") this body never checked; it only ever asserted the CSV response shape and
    /// headers. The "audits the export" half is now its own, separately named and separately
    /// proven test, <see cref="ExportCsv_WritesOneAuditLogExportedRow_FindableBySearchingForIt"/>
    /// below (I-E2).</summary>
    [Fact]
    public async Task ExportCsv_ReturnsCsvResponseWithHeaderRow()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var to = DateTimeOffset.UtcNow.AddMinutes(1);
        var response = await client.GetAsync($"/api/admin/audit-entries/export.csv?actorUserId={admin.StaffAccountId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        // Gate fix 2026-09-26 (SEC hardening): this is an attachment of audit data, not meant to
        // be cached by an intermediary.
        Assert.True(response.Headers.CacheControl?.NoStore, "Expected Cache-Control: no-store on the CSV export.");
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("OccurredOn,ActorUserId", csv);
    }

    /// <summary>ADR-051 / U-E1, written for the TEST gate: <c>ExportCsv_ReturnsCsvResponseWithHeaderRow</c>
    /// above proves the export succeeds and the CSV header is present, but never inspects a cell that
    /// could carry a spreadsheet formula, and the private <c>BuildCsv</c>/<c>Escape</c> methods on
    /// <c>AdminAuditEntriesController</c> have no seam for a direct unit test. Exercised at the one
    /// level that is reachable: a real free-text field (the leave-decision note, whose text
    /// <c>AuditLeaveDecisionHandler</c> is documented to carry into the audit row's <c>Reason</c>)
    /// carrying each of the four guarded prefix characters, then read back through the real CSV
    /// export.</summary>
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+1+1")]
    [InlineData("-1-1")]
    [InlineData("@SUM(1,1)")]
    public async Task ExportCsv_ReasonBeginningWithAFormulaCharacter_IsEscapedWithALeadingApostrophe(string decisionNote)
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var nurseClient = await SignInStaffAsync(nurse.Mobile, nurse.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);

        var startsOn = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);
        var endsOn = startsOn.AddDays(1);
        var request = await nurseClient.PostAsJsonAsync("/api/staff/leave-requests", new
        {
            startsOn = startsOn.ToString("yyyy-MM-dd"), endsOn = endsOn.ToString("yyyy-MM-dd"), kind = "Annual"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var leaveRequestId = (await request.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();

        var approve = await adminClient.PostAsJsonAsync($"/api/admin/leave-requests/{leaveRequestId}/approve", new { decisionNote }, Json);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var export = await adminClient.GetAsync(
            $"/api/admin/audit-entries/export.csv?actorUserId={admin.StaffAccountId}&action=LeaveApproved" +
            $"&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);

        var csv = await export.Content.ReadAsStringAsync();
        Assert.Contains("\"'" + decisionNote, csv);
    }

    /// <summary>I-E2, written for the TEST gate: <c>ExportCsv_ReturnsCsvResponseWithHeaderRow</c> above
    /// asserts the CSV response but never confirms ADR-051's "every search-export is itself audited"
    /// by actually searching for the resulting <c>AuditLogExported</c> row.</summary>
    [Fact]
    public async Task ExportCsv_WritesOneAuditLogExportedRow_FindableBySearchingForIt()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var to = DateTimeOffset.UtcNow.AddMinutes(1);
        var export = await client.GetAsync($"/api/admin/audit-entries/export.csv?actorUserId={admin.StaffAccountId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);

        var searchFrom = from.AddMinutes(-1);
        var searchTo = DateTimeOffset.UtcNow.AddMinutes(5);
        var search = await client.GetAsync(
            $"/api/admin/audit-entries?actorUserId={admin.StaffAccountId}&action=AuditLogExported" +
            $"&from={Uri.EscapeDataString(searchFrom.ToString("O"))}&to={Uri.EscapeDataString(searchTo.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        var body = await search.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(items);
        Assert.Equal("AuditLogExported", items[0].GetProperty("action").GetString());
    }

    [Fact]
    public async Task ReceptionStaff_CannotSearchAuditLog_Returns403()
    {
        var reception = await SeedStaffAsync(StaffRoles.Reception);
        using var client = await SignInStaffAsync(reception.Mobile, reception.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var to = DateTimeOffset.UtcNow.AddMinutes(1);
        var response = await client.GetAsync($"/api/admin/audit-entries?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AccountantStaff_CanSearchAuditLog_Returns200()
    {
        var accountant = await SeedStaffAsync(StaffRoles.Accountant);
        using var client = await SignInStaffAsync(accountant.Mobile, accountant.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var to = DateTimeOffset.UtcNow.AddMinutes(1);
        var response = await client.GetAsync($"/api/admin/audit-entries?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Owner decision 2026-09-26: an Accountant (holds AuditLog.View, not Cats.View) sees
    /// that a cat record changed but never its clinical content.</summary>
    [Fact]
    public async Task AccountantStaff_SearchingACatAuditRow_SeesRedactedValues()
    {
        var owner = await RegisterAndSignInCustomerAsync();
        using var ownerClient = owner.Client;
        var catId = await SeedCatAsync(owner.CustomerId, "Whiskers The Accountant Test Cat");

        var accountant = await SeedStaffAsync(StaffRoles.Accountant);
        using var client = await SignInStaffAsync(accountant.Mobile, accountant.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var search = await client.GetAsync(
            $"/api/admin/audit-entries?entityId={catId}&entityType=CatAggregate&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        var body = await search.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.NotEmpty(items);
        var row = items.First(i => i.GetProperty("entityId").GetGuid() == catId);
        var afterValue = row.GetProperty("afterValue").GetString();
        Assert.NotNull(afterValue);
        Assert.Contains("Redacted", afterValue);
        Assert.DoesNotContain("Whiskers The Accountant Test Cat", afterValue);
    }

    /// <summary>Owner decision 2026-09-26: a Vet (holds Cats.View) sees the actual clinical content.</summary>
    [Fact]
    public async Task VetStaff_SearchingACatAuditRow_SeesActualValues()
    {
        var owner = await RegisterAndSignInCustomerAsync();
        using var ownerClient = owner.Client;
        var catId = await SeedCatAsync(owner.CustomerId, "Whiskers The Vet Test Cat");

        var vet = await SeedStaffAsync(StaffRoles.Vet, StaffRoles.Accountant); // Accountant role grants AuditLog.View
        using var client = await SignInStaffAsync(vet.Mobile, vet.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var search = await client.GetAsync(
            $"/api/admin/audit-entries?entityId={catId}&entityType=CatAggregate&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);

        var body = await search.Content.ReadFromJsonAsync<JsonElement>(Json);
        var items = body.GetProperty("items").EnumerateArray().ToList();
        var row = items.First(i => i.GetProperty("entityId").GetGuid() == catId);
        var afterValue = row.GetProperty("afterValue").GetString();
        Assert.NotNull(afterValue);
        Assert.DoesNotContain("Redacted", afterValue);
        Assert.Contains("Whiskers The Vet Test Cat", afterValue);
    }

    /// <summary>Gate fix 2026-09-26 (SEC MEDIUM #3): owner decision (a) previously missed the four
    /// `Cat`-labelled BusinessEvent rows a high-risk flag, clear, or ownership transfer writes --
    /// an Accountant could read the handling note and every reason through the audit log even
    /// though the cat record itself was already redacted. Covers search and the CSV export.</summary>
    [Fact]
    public async Task AccountantStaff_SearchingCatBusinessEventRows_SeesRedactedValuesAndReasons_AdministratorSeesActualContent()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var owner = await RegisterAndSignInCustomerAsync();
        using var ownerClient = owner.Client;
        var catId = await SeedCatAsync(owner.CustomerId, "Whiskers The BusinessEvent Test Cat");

        const string handlingNote = "HANDLINGNOTE-marker: muzzle required, bites when restrained";
        const string flagReason = "FLAGREASON-marker: FIV positive, high risk of transmission";
        const string clearReason = "CLEARREASON-marker: re-tested negative, flag no longer needed";
        const string transferReason = "TRANSFERREASON-marker: relocating, cat stays with prior owner's family";

        var flag = await adminClient.PutAsJsonAsync($"/api/cats/{catId}/high-risk-flag", new { handlingNote, reason = flagReason }, Json);
        Assert.Equal(HttpStatusCode.OK, flag.StatusCode);

        var clear = await adminClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/cats/{catId}/high-risk-flag")
        {
            Content = JsonContent.Create(new { reason = clearReason }, options: Json)
        });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);

        var newOwner = await RegisterAndSignInCustomerAsync();
        using var newOwnerClient = newOwner.Client;
        var transfer = await adminClient.PostAsJsonAsync($"/api/cats/{catId}/ownership-transfer", new { newOwnerId = newOwner.CustomerId, reason = transferReason }, Json);
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var to = DateTimeOffset.UtcNow.AddMinutes(5);
        var query = $"entityId={catId}&entityType=Cat&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}";

        var accountant = await SeedStaffAsync(StaffRoles.Accountant);
        using var accountantClient = await SignInStaffAsync(accountant.Mobile, accountant.Password);

        var accountantSearch = await accountantClient.GetAsync($"/api/admin/audit-entries?{query}");
        Assert.Equal(HttpStatusCode.OK, accountantSearch.StatusCode);
        var accountantBody = await accountantSearch.Content.ReadFromJsonAsync<JsonElement>(Json);
        var accountantItems = accountantBody.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(3, accountantItems.Count);
        foreach (var item in accountantItems)
        {
            Assert.Contains("Redacted", item.GetProperty("reason").GetString());
            var itemAfterValue = item.GetProperty("afterValue").GetString();
            if (itemAfterValue is not null) Assert.Contains("Redacted", itemAfterValue);
        }

        var accountantCsv = await accountantClient.GetAsync($"/api/admin/audit-entries/export.csv?{query}");
        Assert.Equal(HttpStatusCode.OK, accountantCsv.StatusCode);
        var accountantCsvBody = await accountantCsv.Content.ReadAsStringAsync();
        Assert.DoesNotContain(handlingNote, accountantCsvBody);
        Assert.DoesNotContain(flagReason, accountantCsvBody);
        Assert.DoesNotContain(clearReason, accountantCsvBody);
        Assert.DoesNotContain(transferReason, accountantCsvBody);

        // The Administrator holds both AuditLog.View and Cats.View, so sees the actual content.
        var adminSearch = await adminClient.GetAsync($"/api/admin/audit-entries?{query}");
        var adminBody = await adminSearch.Content.ReadFromJsonAsync<JsonElement>(Json);
        var adminItems = adminBody.GetProperty("items").EnumerateArray().ToList();

        var flagRow = adminItems.Single(i => i.GetProperty("action").GetString() == "CatFlaggedHighRisk");
        Assert.Contains(handlingNote, flagRow.GetProperty("afterValue").GetString());
        Assert.Equal(flagReason, flagRow.GetProperty("reason").GetString());

        var clearRow = adminItems.Single(i => i.GetProperty("action").GetString() == "CatHighRiskFlagCleared");
        Assert.Equal(clearReason, clearRow.GetProperty("reason").GetString());

        var transferRow = adminItems.Single(i => i.GetProperty("action").GetString() == "CatOwnershipTransferred");
        Assert.Equal(transferReason, transferRow.GetProperty("reason").GetString());
    }

    /// <summary>I-E3, TEST gate seventh pass: <c>AuditExportRules.MaxRows</c> is now overridable per
    /// test factory (Amended (38), <c>AdministrationApiFactory.WithLoweredAuditExportCap</c>), so
    /// this is exercised with a handful of real rows instead of 50,001. The production cap (50,000)
    /// is unaffected -- only this test's own factory instance has a lowered configuration value.</summary>
    [Fact]
    public async Task ExportCsv_OverALoweredCap_Returns413()
    {
        var cappedFactory = Factory.WithLoweredAuditExportCap(3);
        using var client = cappedFactory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"), HandleCookies = true
        });

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        var signIn = await client.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = admin.Mobile, password = admin.Password }, Json);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        for (var i = 0; i < 4; i++)
        {
            var create = await client.PostAsJsonAsync("/api/admin/branches", new
            {
                code = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), name = $"Export Cap Test Branch {i}", timeZoneId = "Asia/Dhaka"
            }, Json);
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        }
        var to = DateTimeOffset.UtcNow.AddMinutes(1);

        // 4 branch creates alone already exceed the lowered cap of 3 -- the sign-in's own audit row
        // for the same actor, in the same window, only adds margin.
        var export = await client.GetAsync(
            $"/api/admin/audit-entries/export.csv?actorUserId={admin.StaffAccountId}&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        Assert.Equal((HttpStatusCode)413, export.StatusCode);
    }

    /// <summary>Gate fix 2026-09-26 (CODE_REVIEW seventh pass, MEDIUM): `AuditExport:MaxRows` is
    /// validated at startup (`AuditExportOptions`'s `[Range(1, AuditExportRules.MaxRows)]` +
    /// `AdministrationModule`'s `ValidateDataAnnotations().ValidateOnStart()`) -- zero, negative or
    /// above-the-product-cap values must fail the host at boot, not silently disable the cap or
    /// overflow `MaxRows + 1`. Mirrors `HostCompositionTests`'s own
    /// `HostInProduction_WithADevelopmentOnlySeamConfigured_FailsToStart` pattern:
    /// `Record.Exception(() => factory.CreateClient())` forces the host to build and start.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("50001")] // one above AuditExportRules.MaxRows, the product's own hard ceiling
    public void HostStartup_AuditExportMaxRowsOutOfRange_FailsToStart(string maxRows)
    {
        using var badFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configBuilder) =>
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?> { ["AuditExport:MaxRows"] = maxRows })));

        var exception = Record.Exception(() => badFactory.CreateClient());

        Assert.NotNull(exception);
        Assert.Contains("MaxRows", exception.ToString());
    }

    /// <summary>Negative control: the product's own cap, used as the configured ceiling, is itself
    /// valid -- the range check must not be off-by-one.</summary>
    [Fact]
    public void HostStartup_AuditExportMaxRowsAtTheProductCap_StartsSuccessfully()
    {
        using var okFactory = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configBuilder) =>
                configBuilder.AddInMemoryCollection(new Dictionary<string, string?> { ["AuditExport:MaxRows"] = AuditExportRules.MaxRows.ToString() })));

        var exception = Record.Exception(() => okFactory.CreateClient());

        Assert.Null(exception);
    }
}
