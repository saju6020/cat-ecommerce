using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-NTF-001 §16, Templates (A). I-A2..I-A7. The template row is shared, persistent state
/// (§16.1) -- every test reads the CURRENT content version first (a real admin client always GETs
/// before PUTs) rather than assuming version 0, since <c>ResetToDefault</c> increments the version
/// forever rather than returning it to 0 (§8.2).
/// </summary>
[Collection("Notifications")]
public sealed class NotificationTemplateEndpointTests(NotificationsApiFactory factory) : NotificationsIntegrationTestBase(factory)
{
    private const string Key = NotificationTemplateCatalogue.VaccinationDue;

    private static object ValidVaccinationContents(int expectedVersion, string suffix) => new
    {
        expectedContentVersion = expectedVersion,
        contents = new object[]
        {
            new { channel = "Sms", body = $"{{{{catName}}}} {{{{vaccineName}}}} {{{{dueOn}}}} {{{{bookingLink}}}} {suffix}" },
            new { channel = "Email", subject = "Reminder", body = $"{{{{catName}}}} {{{{vaccineName}}}} {{{{dueOn}}}} {{{{bookingLink}}}} {suffix}" },
            new { channel = "InApp", title = "Reminder", body = $"{{{{catName}}}} {{{{vaccineName}}}} {{{{dueOn}}}} {suffix}", linkPath = "{{bookingLink}}" },
        }
    };

    private static async Task<int> GetCurrentVersionAsync(HttpClient client)
    {
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/notification-templates/{Key}", Json);
        return detail.GetProperty("contentVersion").GetInt32();
    }

    /// <summary>§16.1 rule 2: restore the template to "not customised" in `finally`. Reset always
    /// increments the version further (§8.2) -- it never returns it to a fixed baseline -- so this
    /// only needs the version to pass as `expectedContentVersion`, not any particular value.</summary>
    private static async Task ResetTemplateAsync(HttpClient client)
    {
        var version = await GetCurrentVersionAsync(client);
        await client.PostAsJsonAsync($"/api/admin/notification-templates/{Key}/reset", new { expectedContentVersion = version }, Json);
    }

    [Fact]
    public async Task EditThenGet_ReturnsCustomisedAtIncrementedVersion()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        try
        {
            var versionBefore = await GetCurrentVersionAsync(client);
            var save = await client.PutAsJsonAsync($"/api/admin/notification-templates/{Key}", ValidVaccinationContents(versionBefore, "edit1"), Json);
            Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);

            var detail = await client.GetFromJsonAsync<JsonElement>($"/api/admin/notification-templates/{Key}", Json);
            Assert.True(detail.GetProperty("isCustomised").GetBoolean());
            Assert.Equal(versionBefore + 1, detail.GetProperty("contentVersion").GetInt32());
            Assert.Contains(detail.GetProperty("contents").EnumerateArray(), c => c.GetProperty("body").GetString()!.EndsWith("edit1"));
        }
        finally
        {
            await ResetTemplateAsync(client);
        }
    }

    [Fact]
    public async Task SaveWithUnknownPlaceholder_Returns400NamingTheVariable()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);
        var version = await GetCurrentVersionAsync(client);

        var response = await client.PutAsJsonAsync($"/api/admin/notification-templates/{Key}", new
        {
            expectedContentVersion = version,
            contents = new object[]
            {
                new { channel = "Sms", body = "{{catname}} due" },
                new { channel = "Email", subject = "s", body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                new { channel = "InApp", title = "t", body = "{{catName}} {{vaccineName}} {{dueOn}}", linkPath = "{{bookingLink}}" },
            }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        var errors = body.GetProperty("errors").EnumerateArray().ToList();
        Assert.Contains(errors, e => e.GetProperty("errorCode").GetString() == BusinessViolationCodes.UnknownTemplatePlaceholder
            && e.GetProperty("errorMessage").GetString()!.Contains("{{catname}}"));

        // Nothing should have been written -- the version is unchanged.
        Assert.Equal(version, await GetCurrentVersionAsync(client));
    }

    [Fact]
    public async Task TwoSavesWithSameExpectedVersion_SecondReturns409()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        try
        {
            var version = await GetCurrentVersionAsync(client);

            var first = await client.PutAsJsonAsync($"/api/admin/notification-templates/{Key}", ValidVaccinationContents(version, "v1"), Json);
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

            var second = await client.PutAsJsonAsync($"/api/admin/notification-templates/{Key}", ValidVaccinationContents(version, "v2-conflict"), Json);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }
        finally
        {
            await ResetTemplateAsync(client);
        }
    }

    [Fact]
    public async Task Preview_RendersSampleData_WritesNothing()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var versionBefore = await GetCurrentVersionAsync(client);

        var preview = await client.PostAsJsonAsync($"/api/admin/notification-templates/{Key}/preview", new
        {
            contents = new object[]
            {
                new { channel = "Sms", body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                new { channel = "Email", subject = "s", body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                new { channel = "InApp", title = "t", body = "{{catName}} {{vaccineName}} {{dueOn}}", linkPath = "{{bookingLink}}" },
            }
        }, Json);

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var body = await preview.Content.ReadFromJsonAsync<JsonElement>(Json);
        var channels = body.GetProperty("channels").EnumerateArray().ToList();
        Assert.Equal(3, channels.Count);
        var sms = channels.First(c => c.GetProperty("channel").GetString() == "Sms");
        Assert.True(sms.TryGetProperty("sms", out var smsMeasure));
        Assert.True(smsMeasure.GetProperty("segments").GetInt32() >= 1);

        Assert.Equal(versionBefore, await GetCurrentVersionAsync(client)); // nothing written
    }

    [Fact]
    public async Task ShopManager_CanPreviewAndCannotSave_NurseCannotView()
    {
        var shopManager = await SeedStaffAsync(StaffRoles.ShopManager);
        using var managerClient = await SignInStaffAsync(shopManager.Mobile, shopManager.Password);

        var preview = await managerClient.PostAsJsonAsync($"/api/admin/notification-templates/{Key}/preview", new
        {
            contents = new object[]
            {
                new { channel = "Sms", body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                new { channel = "Email", subject = "s", body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                new { channel = "InApp", title = "t", body = "{{catName}} {{vaccineName}} {{dueOn}}", linkPath = "{{bookingLink}}" },
            }
        }, Json);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);

        var version = await GetCurrentVersionAsync(managerClient);
        var save = await managerClient.PutAsJsonAsync($"/api/admin/notification-templates/{Key}", ValidVaccinationContents(version, "shopmgr"), Json);
        Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);

        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        using var nurseClient = await SignInStaffAsync(nurse.Mobile, nurse.Password);
        var view = await nurseClient.GetAsync($"/api/admin/notification-templates/{Key}");
        Assert.Equal(HttpStatusCode.Forbidden, view.StatusCode);
    }

    [Fact]
    public async Task TemplateSave_WritesAuditDataChangeRow()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        try
        {
            var version = await GetCurrentVersionAsync(client);
            var save = await client.PutAsJsonAsync($"/api/admin/notification-templates/{Key}", ValidVaccinationContents(version, "audit"), Json);
            Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);

            var from = DateTimeOffset.UtcNow.AddMinutes(-2);
            var to = DateTimeOffset.UtcNow.AddMinutes(2);
            var search = await client.GetAsync($"/api/admin/audit-entries?actorUserId={admin.StaffAccountId}&entityType=NotificationTemplateAggregate&from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");
            Assert.Equal(HttpStatusCode.OK, search.StatusCode);

            var items = (await search.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("items").EnumerateArray().ToList();
            Assert.NotEmpty(items);
        }
        finally
        {
            await ResetTemplateAsync(client);
        }
    }

    /// <summary>HO-NTF-001 SECURITY gate fix (2026-09-27, LOW): every one of these used to return
    /// 500 instead of 400 -- a null contents entry, an unknown channel, a non-https Url sample
    /// override, a malformed Date sample override, and a body containing the renderer's own
    /// sentinel character.</summary>
    [Theory]
    [MemberData(nameof(BadPreviewRequests))]
    public async Task Preview_MalformedInput_Returns400NotServerError(object requestBody)
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var preview = await client.PostAsJsonAsync($"/api/admin/notification-templates/{Key}/preview", requestBody, Json);

        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
    }

    public static IEnumerable<object[]> BadPreviewRequests()
    {
        yield return [new { contents = new object?[] { null } }];
        yield return [new { contents = new object[] { new { channel = "Fax", body = "x" } } }];
        yield return
        [
            new
            {
                contents = new object[]
                {
                    new { channel = "Sms", body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                    new { channel = "Email", subject = "s", body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                    new { channel = "InApp", title = "t", body = "{{catName}} {{vaccineName}} {{dueOn}}", linkPath = "{{bookingLink}}" },
                },
                sampleValues = new Dictionary<string, string> { ["bookingLink"] = "javascript:alert(1)" }
            }
        ];
        yield return
        [
            new
            {
                contents = new object[]
                {
                    new { channel = "Sms", body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                    new { channel = "Email", subject = "s", body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                    new { channel = "InApp", title = "t", body = "{{catName}} {{vaccineName}} {{dueOn}}", linkPath = "{{bookingLink}}" },
                },
                sampleValues = new Dictionary<string, string> { ["dueOn"] = "junk" }
            }
        ];
        yield return
        [
            new
            {
                contents = new object[]
                {
                    new { channel = "Sms", body = "{{catName}} 99 {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                    new { channel = "Email", subject = "s", body = "{{catName}} {{vaccineName}} {{dueOn}} {{bookingLink}}" },
                    new { channel = "InApp", title = "t", body = "{{catName}} {{vaccineName}} {{dueOn}}", linkPath = "{{bookingLink}}" },
                }
            }
        ];
    }

    [Fact]
    public async Task Save_NullContentsEntry_Returns400NotServerError()
    {
        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var client = await SignInStaffAsync(admin.Mobile, admin.Password);

        var version = await GetCurrentVersionAsync(client);
        var save = await client.PutAsJsonAsync($"/api/admin/notification-templates/{Key}", new
        {
            expectedContentVersion = version,
            contents = new object?[] { null }
        }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, save.StatusCode);
    }
}
