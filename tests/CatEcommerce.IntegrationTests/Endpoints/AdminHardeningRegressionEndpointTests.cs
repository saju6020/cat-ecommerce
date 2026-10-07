using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ADM-001 gate fix 2026-09-26 (hardening round, "stop the whack-a-mole"). Systemic
/// fixes: the API-wide <c>NulByteRejectionFilter</c>, request-body enum validation
/// (<c>MustBeValidEnumeration</c>), the <c>SlugFormat</c>/<c>CodeFormat</c> <c>\A…\z</c> anchors,
/// and the leave-requests <c>pageSize</c> cap. One dedicated file, since the cases cut across
/// content, branches, rosters, leave and the service catalogue rather than any single existing
/// endpoint test file.</summary>
public sealed class AdminHardeningRegressionEndpointTests(AdministrationApiFactory factory) : AdministrationIntegrationTestBase(factory)
{
    private static string NewCode(string prefix = "T") => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    private static string NewSlug() => "page-" + Guid.NewGuid().ToString("N")[..10];
    private static string NewKey() => "policy-" + Guid.NewGuid().ToString("N")[..10];

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        return await SignInStaffAsync(admin.Mobile, admin.Password);
    }

    private static async Task<Guid> CreateValidBranchAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/admin/branches", new { code = NewCode(), name = "Hardening Test Branch", timeZoneId = "Asia/Dhaka" }, Json);
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();
    }

    private static async Task<Guid> CreateValidTaxClassAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode("TAX"), name = "Hardening Test Tax Class" }, Json);
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("taxClassId").GetGuid();
    }

    // ---------- Item 1 (SEC LOW): one API-wide NUL guard, closing every remaining free-text field ----------

    public static IEnumerable<object[]> NulByteInFreeTextCases()
    {
        yield return new object[]
        {
            "leave note",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/staff/leave-requests", new
                {
                    startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)), endsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(11)),
                    kind = "Annual", note = "Bad\0Note"
                }, Json))
        };

        yield return new object[]
        {
            "leave decisionNote (approve)",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var request = await client.PostAsJsonAsync("/api/staff/leave-requests", new
                {
                    startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), endsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(21)), kind = "Sick"
                }, Json);
                request.EnsureSuccessStatusCode();
                var leaveRequestId = (await request.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("leaveRequestId").GetGuid();
                return await client.PostAsJsonAsync($"/api/admin/leave-requests/{leaveRequestId}/approve", new { decisionNote = "Bad\0Note" }, Json);
            })
        };

        yield return new object[]
        {
            "roster-exception reason",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var branchId = await CreateValidBranchAsync(client);
                var staffAccountId = Guid.NewGuid();
                return await client.PostAsJsonAsync($"/api/admin/branches/{branchId}/roster-exceptions?staffAccountId={staffAccountId}", new
                {
                    kind = "ExtraShift", date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), starts = "09:00", ends = "17:00", reason = "Bad\0Reason"
                }, Json);
            })
        };

        yield return new object[]
        {
            "branch-closure reason",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var branchId = await CreateValidBranchAsync(client);
                return await client.PostAsJsonAsync($"/api/admin/branches/{branchId}/closures", new
                {
                    startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), endsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                    kind = "Closure", reason = "Bad\0Reason"
                }, Json);
            })
        };

        yield return new object[]
        {
            "branch phoneNumber",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/branches", new
                {
                    code = NewCode(), name = "Phone Test Branch", timeZoneId = "Asia/Dhaka", phoneNumber = "Bad\0Phone"
                }, Json))
        };

        yield return new object[]
        {
            "content linkUrl",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/content", new
                {
                    kind = "Page", slug = NewSlug(), title = "Link Test", draftBody = "Body", linkUrl = "https://example.test/bad\0url"
                }, Json))
        };
    }

    [Theory]
    [MemberData(nameof(NulByteInFreeTextCases))]
    public async Task NulByteInField_Returns400_NeverA500(string caseName, Func<HttpClient, Task<HttpResponseMessage>> sendRequest)
    {
        using var client = await AdminClientAsync();

        var response = await sendRequest(client);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
            $"Case '{caseName}': expected 400, got {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------- Item 2 (SEC LOW): request-body enum values -- unknown -> 400, case-variant -> succeeds ----------

    public static IEnumerable<object[]> UnknownEnumInBodyCases()
    {
        yield return new object[]
        {
            "content kind",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/content", new { kind = "Nope", slug = NewSlug(), title = "Bad Kind", draftBody = "Body" }, Json))
        };

        yield return new object[]
        {
            "banner placement",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/content", new
                {
                    kind = "Banner", slug = NewSlug(), title = "Bad Placement", draftBody = "Body", placement = "Nope"
                }, Json))
        };

        yield return new object[]
        {
            "room kind",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var branchId = await CreateValidBranchAsync(client);
                return await client.PostAsJsonAsync($"/api/admin/branches/{branchId}/rooms", new { name = "Bad Kind Room", kind = "Nope" }, Json);
            })
        };

        yield return new object[]
        {
            "closure kind",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var branchId = await CreateValidBranchAsync(client);
                return await client.PostAsJsonAsync($"/api/admin/branches/{branchId}/closures", new
                {
                    startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)), endsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
                    kind = "Nope", reason = "Bad kind"
                }, Json);
            })
        };

        yield return new object[]
        {
            "service category",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var taxClassId = await CreateValidTaxClassAsync(client);
                return await client.PostAsJsonAsync("/api/admin/services", new
                {
                    code = NewCode("SVC"), name = "Bad Category", description = "Bad category test", category = "nope",
                    durationMinutes = 30, qualifiedRoleIds = new[] { "Vet" }, requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true
                }, Json);
            })
        };

        yield return new object[]
        {
            "leave kind",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/staff/leave-requests", new
                {
                    startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)), endsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(31)), kind = "Nope"
                }, Json))
        };
    }

    [Theory]
    [MemberData(nameof(UnknownEnumInBodyCases))]
    public async Task UnknownEnumInBody_Returns400_NeverA500(string caseName, Func<HttpClient, Task<HttpResponseMessage>> sendRequest)
    {
        using var client = await AdminClientAsync();

        var response = await sendRequest(client);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
            $"Case '{caseName}': expected 400, got {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
    }

    public static IEnumerable<object[]> LowerCaseEnumInBodyCases()
    {
        yield return new object[]
        {
            "content kind",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/content", new { kind = "page", slug = NewSlug(), title = "Lower Kind", draftBody = "Body" }, Json))
        };

        yield return new object[]
        {
            "banner placement",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/content", new
                {
                    kind = "Banner", slug = NewSlug(), title = "Lower Placement", draftBody = "Body", placement = "home"
                }, Json))
        };

        yield return new object[]
        {
            "room kind",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var branchId = await CreateValidBranchAsync(client);
                return await client.PostAsJsonAsync($"/api/admin/branches/{branchId}/rooms", new { name = "Lower Kind Room", kind = "consultation" }, Json);
            })
        };

        yield return new object[]
        {
            "closure kind",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var branchId = await CreateValidBranchAsync(client);
                return await client.PostAsJsonAsync($"/api/admin/branches/{branchId}/closures", new
                {
                    startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6)), endsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6)),
                    kind = "closure", reason = "Lower kind"
                }, Json);
            })
        };

        yield return new object[]
        {
            "service category",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var taxClassId = await CreateValidTaxClassAsync(client);
                return await client.PostAsJsonAsync("/api/admin/services", new
                {
                    code = NewCode("SVC"), name = "Lower Category", description = "Lower category test", category = "consultation",
                    durationMinutes = 30, qualifiedRoleIds = new[] { "Vet" }, requiredRoomKind = "consultation", taxClassId, isBookableOnline = true
                }, Json);
            })
        };

        yield return new object[]
        {
            "leave kind",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/staff/leave-requests", new
                {
                    startsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40)), endsOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(41)), kind = "annual"
                }, Json))
        };
    }

    [Theory]
    [MemberData(nameof(LowerCaseEnumInBodyCases))]
    public async Task LowerCaseEnumInBody_Returns201_NeverA500(string caseName, Func<HttpClient, Task<HttpResponseMessage>> sendRequest)
    {
        using var client = await AdminClientAsync();

        var response = await sendRequest(client);

        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Case '{caseName}': expected 201, got {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------- Item 3 (SEC LOW): SlugFormat/CodeFormat anchored with \A…\z -- trailing "\n" must fail ----------

    public static IEnumerable<object[]> TrailingNewlineInFormattedFieldCases()
    {
        yield return new object[]
        {
            "policy key",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/policies", new { key = NewKey() + "\n", title = "Trailing Newline Key", requiresAcceptance = false }, Json))
        };

        yield return new object[]
        {
            "content slug",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/content", new { kind = "Page", slug = NewSlug() + "\n", title = "Trailing Newline Slug", draftBody = "Body" }, Json))
        };

        yield return new object[]
        {
            "branch code",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/branches", new { code = NewCode() + "\n", name = "Trailing Newline Branch", timeZoneId = "Asia/Dhaka" }, Json))
        };

        yield return new object[]
        {
            "tax class code",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode("TAX") + "\n", name = "Trailing Newline Tax Class" }, Json))
        };
    }

    [Theory]
    [MemberData(nameof(TrailingNewlineInFormattedFieldCases))]
    public async Task TrailingNewlineInFormattedField_Returns400_NeverAccepted(string caseName, Func<HttpClient, Task<HttpResponseMessage>> sendRequest)
    {
        using var client = await AdminClientAsync();

        var response = await sendRequest(client);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
            $"Case '{caseName}': expected 400, got {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
    }

    // ---------- pageSize cap (CR LOW): the other half of the leave-requests Skip overflow ----------

    [Fact]
    public async Task GetLeaveRequests_PageSizeAtIntMaxValue_Returns400_Never500()
    {
        using var client = await AdminClientAsync();

        var response = await client.GetAsync("/api/admin/leave-requests?pageNumber=1&pageSize=2147483647");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetLeaveRequests_PageSizeSkipOverflowCombination_Returns400_Never500()
    {
        using var client = await AdminClientAsync();

        // pageNumber=3, pageSize=1_500_000_000 -> Skip = (3-1)*1_500_000_000 overflows int negative.
        var response = await client.GetAsync("/api/admin/leave-requests?pageNumber=3&pageSize=1500000000");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Sixth fix round (SEC LOW): blank placement ("", " ", "\t") on create/edit -> 500 ----------
    // AdministrationMapper.ParseNullableEnum checked `value is null` only; a blank-but-non-null
    // placement fell through to ParseEnum, which threw on EnumerationParsing.TryParse's (correct)
    // null result. There is no domain/validator rule requiring a Banner to carry a Placement, so a
    // blank placement now succeeds the same as an absent one, for both Page and Banner.

    public static IEnumerable<object[]> BlankPlacementCases()
    {
        foreach (var blank in new[] { "", " ", "\t" })
        {
            yield return new object[] { $"kind=Page, placement='{blank}'", "Page", blank };
            yield return new object[] { $"kind=Banner, placement='{blank}'", "Banner", blank };
        }
    }

    [Theory]
    [MemberData(nameof(BlankPlacementCases))]
    public async Task BlankPlacementOnCreate_Returns201_NeverA500(string caseName, string kind, string blankPlacement)
    {
        using var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/admin/content", new
        {
            kind, slug = NewSlug(), title = "Blank Placement Test", draftBody = "Body", placement = blankPlacement
        }, Json);

        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"Case '{caseName}': expected 201, got {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
    }

    [Theory]
    [MemberData(nameof(BlankPlacementCases))]
    public async Task BlankPlacementOnEdit_Returns204_NeverA500(string caseName, string kind, string blankPlacement)
    {
        using var client = await AdminClientAsync();
        var create = await client.PostAsJsonAsync("/api/admin/content", new { kind, slug = NewSlug(), title = "Blank Placement Edit Test", draftBody = "Body" }, Json);
        create.EnsureSuccessStatusCode();
        var contentItemId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("contentItemId").GetGuid();

        var response = await client.PutAsJsonAsync($"/api/admin/content/{contentItemId}", new
        {
            title = "Blank Placement Edit Test", draftBody = "Edited body", placement = blankPlacement
        }, Json);

        Assert.True(response.StatusCode == HttpStatusCode.NoContent,
            $"Case '{caseName}': expected 204, got {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
    }
}
