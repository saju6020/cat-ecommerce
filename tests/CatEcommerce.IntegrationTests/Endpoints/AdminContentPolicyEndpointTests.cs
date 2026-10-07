using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-ADM-001 §16, Content, policies, maintenance (F). I-F1/I-F2/I-F4/I-F5/I-F6/I-F7.</summary>
// HO-SHP-001 build (2026-10-04): MaintenanceMode_NeverBlocksAdminApiOrStaffSignIn flips the GLOBAL
// maintenance setting in the shared, never-truncated database for the duration of the test, and a
// storefront endpoint (HO-ADM-001 ADR-053, first applied by HO-SHP-001) answers 503 while it is on --
// so any storefront test running in parallel flakes. Serialising this class with the other global-
// setting tests (DisableParallelization) removes the window without touching a single assertion.
[Collection(AdministrationGlobalSettingsCollection.Name)]
public sealed class AdminContentPolicyEndpointTests(AdministrationApiFactory factory) : AdministrationIntegrationTestBase(factory)
{
    private static string NewSlug() => "page-" + Guid.NewGuid().ToString("N")[..10];
    private static string NewKey() => "policy-" + Guid.NewGuid().ToString("N")[..10];

    [Fact]
    public async Task CreatePublishEditDraft_PublicReadReturnsPublishedNotDraft()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var slug = NewSlug();
        var create = await client.PostAsJsonAsync("/api/admin/content", new
        {
            kind = "Page", slug, title = "About Us", draftBody = "## Welcome\n\nOriginal body."
        }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var contentItemId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("contentItemId").GetGuid();

        var publish = await client.PostAsync($"/api/admin/content/{contentItemId}/publish", null);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        var editDraft = await client.PutAsJsonAsync($"/api/admin/content/{contentItemId}", new
        {
            title = "About Us", draftBody = "## Welcome\n\nEdited draft, not yet published."
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, editDraft.StatusCode);

        using var anon = CreateClient();
        var publicRead = await anon.GetAsync($"/api/content/page/{slug}");
        Assert.Equal(HttpStatusCode.OK, publicRead.StatusCode);
        var body = await publicRead.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains("Original body", body.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Unpublish_PublicReadReturns404()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var slug = NewSlug();
        var create = await client.PostAsJsonAsync("/api/admin/content", new { kind = "Faq", slug, title = "Q1", draftBody = "Answer" }, Json);
        var contentItemId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("contentItemId").GetGuid();
        await client.PostAsync($"/api/admin/content/{contentItemId}/publish", null);
        var unpublish = await client.PostAsync($"/api/admin/content/{contentItemId}/unpublish", null);
        Assert.Equal(HttpStatusCode.NoContent, unpublish.StatusCode);

        using var anon = CreateClient();
        var response = await anon.GetAsync($"/api/content/faq/{slug}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ContentWithScriptTag_Returns400InvalidContentMarkup()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var response = await client.PostAsJsonAsync("/api/admin/content", new
        {
            kind = "Page", slug = NewSlug(), title = "Bad", draftBody = "<script>alert(1)</script>"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("InvalidContentMarkup", await response.Content.ReadAsStringAsync());
    }

    /// <summary>Owner decision 2026-09-26, superseded 2026-09-26 (round 4, see the theory below):
    /// publishing is now Administrator-only via <c>[RequireAction(Actions.Policies.Manage)]</c>, so
    /// the Shop Manager's blocked attempt is now a <c>403</c> (never reaches the handler), not the
    /// former handler-level <c>400 ActionNotPermitted</c>. Content.Manage still lets a Shop Manager
    /// manage everything else -- creating the policy document itself is untouched by this test
    /// (see the "any key, any flag" theory below for create), and publishing ordinary content.</summary>
    [Fact]
    public async Task ShopManager_CannotPublishPolicyVersion_ButCanManageOtherContent()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var key = NewKey();
        var createPolicy = await adminClient.PostAsJsonAsync("/api/admin/policies", new { key, title = "Privacy Policy", requiresAcceptance = true }, Json);
        Assert.Equal(HttpStatusCode.Created, createPolicy.StatusCode);

        var shopManager = await SeedStaffAsync(StaffRoles.ShopManager);
        using var shopManagerClient = await SignInStaffAsync(shopManager.Mobile, shopManager.Password);

        var blockedPublish = await shopManagerClient.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1", changeSummary = "Initial" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, blockedPublish.StatusCode);
        Assert.Contains("ActionNotPermitted", await blockedPublish.Content.ReadAsStringAsync());

        // Content.Manage still lets the Shop Manager publish a page.
        var slug = NewSlug();
        var createPage = await shopManagerClient.PostAsJsonAsync("/api/admin/content", new { kind = "Page", slug, title = "Shipping", draftBody = "We ship nationwide." }, Json);
        Assert.Equal(HttpStatusCode.Created, createPage.StatusCode);
        var contentItemId = (await createPage.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("contentItemId").GetGuid();
        var publishPage = await shopManagerClient.PostAsync($"/api/admin/content/{contentItemId}/publish", null);
        Assert.Equal(HttpStatusCode.NoContent, publishPage.StatusCode);

        // An Administrator can still publish the policy version.
        var adminPublish = await adminClient.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1", changeSummary = "Initial" }, Json);
        Assert.Equal(HttpStatusCode.Created, adminPublish.StatusCode);
    }

    /// <summary>Owner decision 2026-09-26 (round 4, supersedes the "reserved key" version of this
    /// test): every policy document's create AND publish is Administrator-only, for ANY key,
    /// regardless of RequiresAcceptance -- not just a hard-coded prefix set that never matched the
    /// spec's real keys (the gate proved a Shop Manager could squat `terms`, `privacy`,
    /// `boarding-terms` and every HO-EMR-001 `consent-*` key). Enforced by
    /// `[RequireAction(Actions.Policies.Manage)]`, so a Shop Manager gets 403 on both, for every key
    /// tried, and an Administrator gets 201 on both.</summary>
    [Theory]
    [InlineData("terms")]
    [InlineData("consent-surgery")]
    [InlineData("boarding-terms")]
    [InlineData("anything")]
    public async Task ShopManager_CannotCreateOrPublishAnyPolicyDocument_AdministratorCan(string keyPrefix)
    {
        var shopManager = await SeedStaffAsync(StaffRoles.ShopManager);
        using var shopManagerClient = await SignInStaffAsync(shopManager.Mobile, shopManager.Password);

        var key = keyPrefix + "-" + Guid.NewGuid().ToString("N")[..10];

        var createBlocked = await shopManagerClient.PostAsJsonAsync("/api/admin/policies", new { key, title = "Test Policy", requiresAcceptance = false }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, createBlocked.StatusCode);
        Assert.Contains("ActionNotPermitted", await createBlocked.Content.ReadAsStringAsync());

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        var create = await adminClient.PostAsJsonAsync("/api/admin/policies", new { key, title = "Test Policy", requiresAcceptance = false }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var publishBlocked = await shopManagerClient.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1", changeSummary = "Initial" }, Json);
        Assert.Equal(HttpStatusCode.Forbidden, publishBlocked.StatusCode);
        Assert.Contains("ActionNotPermitted", await publishBlocked.Content.ReadAsStringAsync());

        var publishAllowed = await adminClient.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1", changeSummary = "Initial" }, Json);
        Assert.Equal(HttpStatusCode.Created, publishAllowed.StatusCode);
    }

    /// <summary>Gate fix 2026-09-26 (hardening round): a NUL byte in <c>draftBody</c> is now caught
    /// by the API-wide <c>NulByteRejectionFilter</c> before the action (and <c>LimitedMarkup</c>'s
    /// own NUL check inside it) ever runs, so the error code is the filter's generic
    /// <c>ContainsControlCharacter</c>, not <c>InvalidContentMarkup</c> -- still 400, never a 500.
    /// <c>LimitedMarkup</c>'s own NUL check is retained (defence in depth for any caller that
    /// reaches <c>ContentItemAggregate.Create</c> directly, bypassing the Api's request pipeline),
    /// and still produces <c>InvalidContentMarkup</c> for every other disallowed-markup case, proven
    /// by <see cref="ContentWithScriptTag_Returns400InvalidContentMarkup"/> below (no NUL byte, so
    /// the global filter never intercepts it).</summary>
    [Fact]
    public async Task ContentWithNulByte_Returns400_ViaGlobalNulGuard()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var response = await client.PostAsJsonAsync("/api/admin/content", new
        {
            kind = "Page", slug = NewSlug(), title = "Bad", draftBody = "Hello\0World"
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ContainsControlCharacter", await response.Content.ReadAsStringAsync());
    }

    /// <summary>Gate fix 2026-09-26 (SEC LOW): a NUL byte in any of these free-text fields
    /// previously reached Postgres unfiltered and returned an uncaught 500. One theory covering
    /// every fixed site: content title, policy title, a policy's changeSummary, a branch name, the
    /// maintenance message, and the audit log's entityType/action query filters.</summary>
    public static IEnumerable<object[]> NulByteRejectionCases()
    {
        yield return new object[]
        {
            "content title",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/content", new { kind = "Page", slug = NewSlug(), title = "Bad\0Title", draftBody = "Body" }, Json))
        };

        yield return new object[]
        {
            "policy title",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/policies", new { key = NewKey(), title = "Bad\0Title", requiresAcceptance = false }, Json))
        };

        yield return new object[]
        {
            "policy changeSummary",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var key = NewKey();
                var create = await client.PostAsJsonAsync("/api/admin/policies", new { key, title = "Valid Title For ChangeSummary Test", requiresAcceptance = false }, Json);
                create.EnsureSuccessStatusCode();
                return await client.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1", changeSummary = "Bad\0Summary" }, Json);
            })
        };

        yield return new object[]
        {
            "branch name",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/branches", new { code = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), name = "Bad\0Name", timeZoneId = "Asia/Dhaka" }, Json))
        };

        yield return new object[]
        {
            "maintenance message",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PutAsJsonAsync("/api/admin/system/maintenance", new { maintenanceMode = false, message = "Bad\0Message" }, Json))
        };

        yield return new object[]
        {
            "audit entityType filter",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client => client.GetAsync(
                $"/api/admin/audit-entries?entityType=Bad%00Type&from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"))}"))
        };

        yield return new object[]
        {
            "audit action filter",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client => client.GetAsync(
                $"/api/admin/audit-entries?action=Bad%00Action&from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"))}"))
        };

        // Gate fix 2026-09-26 (SEC LOW, round 4): the remaining fields the third-pass gate proved
        // still 500'd -- policy key, content slug, branch code/addressLine, room name, tax class
        // code/name, clinic service code/name/description.

        yield return new object[]
        {
            "policy key",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/policies", new { key = "bad\0key", title = "Valid Title", requiresAcceptance = false }, Json))
        };

        yield return new object[]
        {
            "content slug",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/content", new { kind = "Page", slug = "bad\0slug", title = "Valid Title", draftBody = "Body" }, Json))
        };

        yield return new object[]
        {
            "branch code",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/branches", new { code = "Bad\0Code", name = "Valid Name", timeZoneId = "Asia/Dhaka" }, Json))
        };

        yield return new object[]
        {
            "branch addressLine",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/branches", new
                {
                    code = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), name = "Valid Name",
                    addressLine = "Bad\0Address", timeZoneId = "Asia/Dhaka"
                }, Json))
        };

        yield return new object[]
        {
            "room name",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var branchId = await CreateValidBranchAsync(client);
                return await client.PostAsJsonAsync($"/api/admin/branches/{branchId}/rooms", new { name = "Bad\0Room", kind = "Consultation" }, Json);
            })
        };

        yield return new object[]
        {
            "tax class code",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/tax-classes", new { code = "Bad\0Code", name = "Valid Name" }, Json))
        };

        yield return new object[]
        {
            "tax class name",
            (Func<HttpClient, Task<HttpResponseMessage>>)(client =>
                client.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode("TAX"), name = "Bad\0Name" }, Json))
        };

        yield return new object[]
        {
            "clinic service code",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var taxClassId = await CreateValidTaxClassAsync(client);
                return await client.PostAsJsonAsync("/api/admin/services", new
                {
                    code = "Bad\0Code", name = "Valid Service", description = "Valid description", category = "Consultation",
                    durationMinutes = 30, qualifiedRoleIds = new[] { "Vet" }, requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true
                }, Json);
            })
        };

        yield return new object[]
        {
            "clinic service name",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var taxClassId = await CreateValidTaxClassAsync(client);
                return await client.PostAsJsonAsync("/api/admin/services", new
                {
                    code = NewCode("SVC"), name = "Bad\0Name", description = "Valid description", category = "Consultation",
                    durationMinutes = 30, qualifiedRoleIds = new[] { "Vet" }, requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true
                }, Json);
            })
        };

        yield return new object[]
        {
            "clinic service description",
            (Func<HttpClient, Task<HttpResponseMessage>>)(async client =>
            {
                var taxClassId = await CreateValidTaxClassAsync(client);
                return await client.PostAsJsonAsync("/api/admin/services", new
                {
                    code = NewCode("SVC"), name = "Valid Service", description = "Bad\0Description", category = "Consultation",
                    durationMinutes = 30, qualifiedRoleIds = new[] { "Vet" }, requiredRoomKind = "Consultation", taxClassId, isBookableOnline = true
                }, Json);
            })
        };
    }

    private static string NewCode(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async Task<Guid> CreateValidBranchAsync(HttpClient client)
    {
        var code = "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var create = await client.PostAsJsonAsync("/api/admin/branches", new { code, name = "NUL Test Branch", timeZoneId = "Asia/Dhaka" }, Json);
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("branchId").GetGuid();
    }

    private static async Task<Guid> CreateValidTaxClassAsync(HttpClient client)
    {
        var create = await client.PostAsJsonAsync("/api/admin/tax-classes", new { code = NewCode("TAX"), name = "NUL Test Tax Class" }, Json);
        create.EnsureSuccessStatusCode();
        return (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("taxClassId").GetGuid();
    }

    /// <summary>Gate fix 2026-09-26 (SEC LOW, round 4): the acceptance policyKey is
    /// **customer-reachable**, unlike every field above (all staff-only) -- a separate test because
    /// the customer session path is a different client than <see cref="NulByteInField_Returns400"/>
    /// signs in with.</summary>
    [Fact]
    public async Task PolicyAcceptance_NulBytePolicyKey_Returns400()
    {
        var (customerClient, _, _) = await RegisterAndSignInCustomerAsync();
        using var client = customerClient;

        var response = await client.PostAsJsonAsync("/api/customers/me/policy-acceptances", new { policyKey = "bad\0key", versionNumber = 1 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(NulByteRejectionCases))]
    public async Task NulByteInField_Returns400(string caseName, Func<HttpClient, Task<HttpResponseMessage>> sendRequest)
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var response = await sendRequest(client);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest,
            $"Case '{caseName}': expected 400, got {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>Gate fix 2026-09-26 (SEC LOW): (pageNumber-1)*pageSize previously overflowed int
    /// (Skip overflow) and returned an uncaught 500. pageNumber is now capped -- this asserts the
    /// documented outcome (400), never a 500.</summary>
    [Fact]
    public async Task AuditSearch_PageNumberAtIntMaxValue_Returns400_Never500()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var from = DateTimeOffset.UtcNow.AddMinutes(-1);
        var to = DateTimeOffset.UtcNow.AddMinutes(1);
        var response = await client.GetAsync(
            $"/api/admin/audit-entries?pageNumber=2147483647&pageSize=200&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAdminContent_UnrecognisedKind_Returns400()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var response = await client.GetAsync("/api/admin/content?kind=NotAKind");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetBanners_UnrecognisedPlacement_Returns400()
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/content/banners?placement=NotAPlacement");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Gate fix 2026-09-26 (ARCH MEDIUM / SEC LOW): a lower-case content kind must be
    /// accepted (`EnumQueryParameter` matches case-insensitively) AND correctly filter -- not just
    /// avoid a 500. Regression for a finding that survived one prior fix pass (the controller's 400
    /// guard passed a case-variant value, but the handler's case-sensitive `FromDisplayName` still
    /// threw before the canonical `DisplayName` was passed on instead).</summary>
    [Fact]
    public async Task GetAdminContent_LowerCaseKind_Returns200AndFiltersCorrectly()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var pageSlug = NewSlug();
        var pageTitle = "Marker-Page-" + Guid.NewGuid().ToString("N")[..8];
        var createPage = await client.PostAsJsonAsync("/api/admin/content", new { kind = "Page", slug = pageSlug, title = pageTitle, draftBody = "Page body" }, Json);
        Assert.Equal(HttpStatusCode.Created, createPage.StatusCode);

        var faqSlug = NewSlug();
        var faqTitle = "Marker-Faq-" + Guid.NewGuid().ToString("N")[..8];
        var createFaq = await client.PostAsJsonAsync("/api/admin/content", new { kind = "Faq", slug = faqSlug, title = faqTitle, draftBody = "Faq body" }, Json);
        Assert.Equal(HttpStatusCode.Created, createFaq.StatusCode);

        var response = await client.GetAsync("/api/admin/content?kind=page");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();

        Assert.Contains(items, i => i.GetProperty("title").GetString() == pageTitle);
        Assert.DoesNotContain(items, i => i.GetProperty("title").GetString() == faqTitle);
        Assert.All(items, i => Assert.Equal("Page", i.GetProperty("kind").GetString()));
    }

    /// <summary>Gate fix 2026-09-26 (ARCH MEDIUM / SEC LOW): same regression as the content-kind
    /// test above, for the anonymous banners endpoint.</summary>
    [Fact]
    public async Task GetBanners_LowerCasePlacement_Returns200AndFiltersCorrectly()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var homeTitle = "Marker-Home-Banner-" + Guid.NewGuid().ToString("N")[..8];
        var createHome = await client.PostAsJsonAsync("/api/admin/content", new
        {
            kind = "Banner", slug = NewSlug(), title = homeTitle, draftBody = "Home banner", placement = "Home"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, createHome.StatusCode);
        var homeId = (await createHome.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("contentItemId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/admin/content/{homeId}/publish", null)).StatusCode);

        var shopTitle = "Marker-Shop-Banner-" + Guid.NewGuid().ToString("N")[..8];
        var createShop = await client.PostAsJsonAsync("/api/admin/content", new
        {
            kind = "Banner", slug = NewSlug(), title = shopTitle, draftBody = "Shop banner", placement = "Shop"
        }, Json);
        Assert.Equal(HttpStatusCode.Created, createShop.StatusCode);
        var shopId = (await createShop.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("contentItemId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/admin/content/{shopId}/publish", null)).StatusCode);

        using var anon = CreateClient();
        var response = await anon.GetAsync("/api/content/banners?placement=home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();

        Assert.Contains(items, i => i.GetProperty("title").GetString() == homeTitle);
        Assert.DoesNotContain(items, i => i.GetProperty("title").GetString() == shopTitle);
    }

    /// <summary>I-F3, TEST gate seventh pass: a banner outside its <c>visibleFrom</c>/<c>visibleTo</c>
    /// window is excluded from the public banners read, even though it is published. Uses the real
    /// system clock (no time travel needed) -- a future <c>visibleFrom</c> or a past <c>visibleTo</c>
    /// is excluded regardless of what "now" actually is. A third, currently-visible banner is the
    /// positive control, proving the filter does not simply exclude everything.</summary>
    [Fact]
    public async Task GetBanners_OutsideVisibilityWindow_IsExcluded_InsideWindow_IsReturned()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var now = DateTimeOffset.UtcNow;

        var notYetVisibleTitle = "Marker-NotYetVisible-Banner-" + Guid.NewGuid().ToString("N")[..8];
        var notYetVisibleId = await CreateAndPublishBannerAsync(client, notYetVisibleTitle, visibleFrom: now.AddDays(7), visibleTo: null);

        var noLongerVisibleTitle = "Marker-NoLongerVisible-Banner-" + Guid.NewGuid().ToString("N")[..8];
        var noLongerVisibleId = await CreateAndPublishBannerAsync(client, noLongerVisibleTitle, visibleFrom: now.AddDays(-30), visibleTo: now.AddDays(-1));

        var currentlyVisibleTitle = "Marker-CurrentlyVisible-Banner-" + Guid.NewGuid().ToString("N")[..8];
        var currentlyVisibleId = await CreateAndPublishBannerAsync(client, currentlyVisibleTitle, visibleFrom: now.AddDays(-1), visibleTo: now.AddDays(1));

        using var anon = CreateClient();
        var response = await anon.GetAsync("/api/content/banners?placement=Home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();

        Assert.DoesNotContain(items, i => i.GetProperty("title").GetString() == notYetVisibleTitle);
        Assert.DoesNotContain(items, i => i.GetProperty("title").GetString() == noLongerVisibleTitle);
        Assert.Contains(items, i => i.GetProperty("title").GetString() == currentlyVisibleTitle);

        // Not asserted on response content, just to silence "unused" -- the ids prove creation
        // succeeded (StatusCode already checked inside the helper).
        Assert.NotEqual(Guid.Empty, notYetVisibleId);
        Assert.NotEqual(Guid.Empty, noLongerVisibleId);
        Assert.NotEqual(Guid.Empty, currentlyVisibleId);
    }

    private async Task<Guid> CreateAndPublishBannerAsync(HttpClient client, string title, DateTimeOffset? visibleFrom, DateTimeOffset? visibleTo)
    {
        var create = await client.PostAsJsonAsync("/api/admin/content", new
        {
            kind = "Banner", slug = NewSlug(), title, draftBody = "Banner body", placement = "Home", visibleFrom, visibleTo
        }, Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var contentItemId = (await create.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("contentItemId").GetGuid();

        var publish = await client.PostAsync($"/api/admin/content/{contentItemId}/publish", null);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        return contentItemId;
    }

    [Fact]
    public async Task PublishPolicyVersion_CustomerAcceptsThenPendingIsEmpty_LaterVersionReopensPending()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var key = NewKey();
        var createPolicy = await adminClient.PostAsJsonAsync("/api/admin/policies", new { key, title = "Terms", requiresAcceptance = true }, Json);
        Assert.Equal(HttpStatusCode.Created, createPolicy.StatusCode);

        var v1 = await adminClient.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1 terms", changeSummary = "Initial" }, Json);
        Assert.Equal(HttpStatusCode.Created, v1.StatusCode);

        var (customerClient, _, _) = await RegisterAndSignInCustomerAsync();

        var pendingBefore = await customerClient.GetAsync("/api/customers/me/policy-acceptances/pending");
        var pendingBeforeItems = (await pendingBefore.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        Assert.Contains(pendingBeforeItems, p => p.GetProperty("policyKey").GetString() == key && p.GetProperty("versionNumber").GetInt32() == 1);

        var accept = await customerClient.PostAsJsonAsync("/api/customers/me/policy-acceptances", new { policyKey = key, versionNumber = 1 }, Json);
        Assert.Equal(HttpStatusCode.NoContent, accept.StatusCode);

        var pendingAfter = await customerClient.GetAsync("/api/customers/me/policy-acceptances/pending");
        var pendingAfterItems = (await pendingAfter.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        Assert.DoesNotContain(pendingAfterItems, p => p.GetProperty("policyKey").GetString() == key);

        // I-F5: a new version reopens the requirement; the old acceptance's version is retained.
        var v2 = await adminClient.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v2 terms", changeSummary = "Update" }, Json);
        Assert.Equal(HttpStatusCode.Created, v2.StatusCode);

        var pendingAfterV2 = await customerClient.GetAsync("/api/customers/me/policy-acceptances/pending");
        var pendingV2Items = (await pendingAfterV2.Content.ReadFromJsonAsync<JsonElement>(Json)).EnumerateArray().ToList();
        Assert.Contains(pendingV2Items, p => p.GetProperty("policyKey").GetString() == key && p.GetProperty("versionNumber").GetInt32() == 2);
    }

    /// <summary>U-F5, written for the TEST gate: no existing test accepts a version that is no
    /// longer current. <c>PublishPolicyVersion_CustomerAcceptsThenPendingIsEmpty_LaterVersionReopensPending</c>
    /// publishes v1 then v2 but only ever accepts the (then-current) v1 -- it never tries to accept
    /// v1 again once v2 has superseded it.</summary>
    [Fact]
    public async Task AcceptPolicyVersion_NoLongerCurrent_Returns400PolicyVersionNotCurrent()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var key = NewKey();
        var createPolicy = await adminClient.PostAsJsonAsync("/api/admin/policies", new { key, title = "Terms", requiresAcceptance = true }, Json);
        Assert.Equal(HttpStatusCode.Created, createPolicy.StatusCode);

        var v1 = await adminClient.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1 terms", changeSummary = "Initial" }, Json);
        Assert.Equal(HttpStatusCode.Created, v1.StatusCode);

        var v2 = await adminClient.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v2 terms", changeSummary = "Update" }, Json);
        Assert.Equal(HttpStatusCode.Created, v2.StatusCode);

        var (customerClient, _, _) = await RegisterAndSignInCustomerAsync();

        var acceptSuperseded = await customerClient.PostAsJsonAsync("/api/customers/me/policy-acceptances", new { policyKey = key, versionNumber = 1 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, acceptSuperseded.StatusCode);
        Assert.Contains("PolicyVersionNotCurrent", await acceptSuperseded.Content.ReadAsStringAsync());

        // The current version can still be accepted normally.
        var acceptCurrent = await customerClient.PostAsJsonAsync("/api/customers/me/policy-acceptances", new { policyKey = key, versionNumber = 2 }, Json);
        Assert.Equal(HttpStatusCode.NoContent, acceptCurrent.StatusCode);
    }

    /// <summary>Gate fix 2026-09-26 (ARCH LOW): §8.8 says accepting the same policy version twice
    /// is a no-op -- two truly concurrent accepts must both succeed with the same 204 the
    /// sequential path returns (UniqueConstraintExceptionFilter maps the race's 23505 on
    /// ix_policy_acceptance_customer_key_number to 204, not 400), and never leave more than the
    /// one row the unique index allows.</summary>
    [Fact]
    public async Task ConcurrentAcceptance_SameCustomerSameVersion_BothSucceed_ExactlyOneRow()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);

        var key = NewKey();
        var createPolicy = await adminClient.PostAsJsonAsync("/api/admin/policies", new { key, title = "Concurrent Accept Test", requiresAcceptance = true }, Json);
        Assert.Equal(HttpStatusCode.Created, createPolicy.StatusCode);
        var publish = await adminClient.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1", changeSummary = "Initial" }, Json);
        Assert.Equal(HttpStatusCode.Created, publish.StatusCode);

        var (customerClient, customerId, _) = await RegisterAndSignInCustomerAsync();
        using var client = customerClient;

        var acceptA = client.PostAsJsonAsync("/api/customers/me/policy-acceptances", new { policyKey = key, versionNumber = 1 }, Json);
        var acceptB = client.PostAsJsonAsync("/api/customers/me/policy-acceptances", new { policyKey = key, versionNumber = 1 }, Json);
        var responses = await Task.WhenAll(acceptA, acceptB);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NoContent, r.StatusCode));

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var count = await db.Set<PolicyAcceptanceAggregate>().AsNoTracking()
            .CountAsync(a => a.CustomerAccountId == customerId && a.PolicyKey == key && a.VersionNumber == 1);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task MaintenanceMode_NeverBlocksAdminApiOrStaffSignIn()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var setOn = await client.PutAsJsonAsync("/api/admin/system/maintenance", new { maintenanceMode = true, message = "Down for maintenance" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, setOn.StatusCode);

        try
        {
            using var anon = CreateClient();
            var status = await anon.GetAsync("/api/system/status");
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
            var body = await status.Content.ReadFromJsonAsync<JsonElement>(Json);
            Assert.True(body.GetProperty("maintenanceMode").GetBoolean());

            // The admin API itself is never gated (ADR-053) -- reading branches still works.
            var adminRead = await client.GetAsync("/api/admin/branches");
            Assert.Equal(HttpStatusCode.OK, adminRead.StatusCode);

            // Staff sign-in still works.
            using var freshClient = CreateClient();
            var signIn = await freshClient.PostAsJsonAsync("/api/staff/auth/sign-in", new { identifier = admin.Mobile, password = admin.Password }, Json);
            Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        }
        finally
        {
            await client.PutAsJsonAsync("/api/admin/system/maintenance", new { maintenanceMode = false, message = (string?)null }, Json);
        }
    }
}
