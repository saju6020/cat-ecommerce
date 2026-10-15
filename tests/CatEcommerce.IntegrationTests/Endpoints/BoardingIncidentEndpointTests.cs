using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BRD-001 Phase F -- incidents, the owner's diary, stay media and the dashboard (I-F1 .. I-F8).</summary>
// One collection: the Boarding worker pass reads every stay in the shared database, so classes with different fake clocks must not overlap.
[Collection("Boarding")]
public sealed class BoardingIncidentEndpointTests(BoardingApiFactory factory) : BoardingIntegrationTestBase(factory)
{
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x02, 0x00, 0x00, 0x00, 0x90, 0x77, 0x53, 0xDE
    ];

    private async Task<HttpResponseMessage> RecordAsync(InHouse stay, string severity, string kind = "Injury") =>
        await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/incidents", new
        {
            catId = stay.CatIds[0], kind, severity, description = "Scratch on the left ear", immediateAction = "Cleaned and checked", occurredOn = Now.AddMinutes(-10)
        }, Json);

    private async Task<Guid> RecordOkAsync(InHouse stay, string severity)
    {
        var response = await RecordAsync(stay, severity);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadAsync(response)).GetProperty("incidentId").GetGuid();
    }

    private async Task<Guid> UploadMediaFileAsync(HttpClient staff)
    {
        using var form = new MultipartFormDataContent("test-boundary-" + Guid.NewGuid().ToString("N"));
        var content = new ByteArrayContent(PngBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        form.Add(content, "file", "cat.png");

        var upload = await staff.PostAsync("/api/staff/files?purpose=BoardingMedia", form);
        Assert.True(upload.IsSuccessStatusCode, (int)upload.StatusCode + " " + await upload.Content.ReadAsStringAsync());
        return (await ReadAsync(upload)).GetProperty("fileId").GetGuid();
    }

    [Fact]
    public async Task I_F1_ALowIncidentIsInternal_AMediumAndAHighOneNotifyTheOwner_WithTheirOwnTemplates()
    {
        var stay = await CheckedInStayAsync();

        var low = await RecordOkAsync(stay, "Low");
        Assert.DoesNotContain("boarding.incident-medium", await NotificationKeysAsync(stay.CustomerId));
        Assert.DoesNotContain("boarding.incident-high", await NotificationKeysAsync(stay.CustomerId));

        var medium = await RecordOkAsync(stay, "Medium");
        var high = await RecordOkAsync(stay, "High");
        var keys = await NotificationKeysAsync(stay.CustomerId);
        Assert.Contains("boarding.incident-medium", keys);
        Assert.Contains("boarding.incident-high", keys);

        Assert.Equal(HttpStatusCode.NotFound, (await stay.Customer.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/incidents/{low}")).StatusCode);
        var shown = await ReadAsync(await stay.Customer.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/incidents/{high}"));
        Assert.Equal("High", shown.GetProperty("severity").GetString());
        Assert.Equal(HttpStatusCode.OK, (await stay.Customer.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/incidents/{medium}")).StatusCode);
    }

    // Handover I-F3 (acceptance A31): a High incident recorded at 22:30 Dhaka is in the owner inbox after ONE delivery pass; quiet hours hold the SMS/email copies, never the in-app one.
    [Fact]
    public async Task I_F3b_AHighIncidentRecordedAt2230_IsInTheOwnerInbox_AfterOneDeliveryPass()
    {
        var stay = await CheckedInStayAsync();
        var at = new DateTimeOffset(LocalToday.Year, LocalToday.Month, LocalToday.Day, 22, 30, 0, TimeSpan.FromHours(6));
        Factory.Clock.SetUtcNow(at.ToUniversalTime());
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);
        await ReauthenticateCustomerAsync(stay.Customer, stay.CustomerMobile);
        var before = (await ReadAsync(await stay.Customer.GetAsync("/api/customers/me/inbox"))).GetProperty("items").GetArrayLength();

        await RecordOkAsync(stay, "High");
        Assert.Contains("boarding.incident-high", await NotificationKeysAsync(stay.CustomerId));
        for (var pass = 0; pass < 60; pass++)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<CatEcommerce.Api.Background.NotificationWorkCycle>().RunDeliveryPassAsync();
            var count = (await ReadAsync(await stay.Customer.GetAsync("/api/customers/me/inbox"))).GetProperty("items").GetArrayLength();
            if (count > before) return;
        }

        Assert.Fail("The High incident never reached the owner inbox.");
    }

    [Fact]
    public async Task I_F2_AnIncidentCannotBeRecordedInTheFuture_OrWithoutADescription()
    {
        var stay = await CheckedInStayAsync();
        var url = $"/api/staff/boarding/stays/{stay.StayId}/incidents";

        var future = await stay.Branch.Admin.PostAsJsonAsync(url, new { catId = stay.CatIds[0], kind = "Injury", severity = "Low", description = "x", immediateAction = "y", occurredOn = Now.AddHours(2) }, Json);
        var blank = await stay.Branch.Admin.PostAsJsonAsync(url, new { catId = stay.CatIds[0], kind = "Injury", severity = "Low", description = " ", immediateAction = "y", occurredOn = Now.AddMinutes(-1) }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
    }

    [Fact]
    public async Task I_F3_TheOwnerAcknowledgesFromThePortal_AndAnotherCustomerCannotSeeOrAcknowledgeIt()
    {
        var stay = await CheckedInStayAsync();
        var incidentId = await RecordOkAsync(stay, "High");
        var (stranger, _, _) = await RegisterAndSignInCustomerAsync();

        var denied = await stranger.PostAsync($"/api/customers/me/boarding/stays/{stay.StayId}/incidents/{incidentId}/acknowledge", null);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/incidents/{incidentId}")).StatusCode);
        Assert.False(denied.IsSuccessStatusCode);

        var ack = await stay.Customer.PostAsync($"/api/customers/me/boarding/stays/{stay.StayId}/incidents/{incidentId}/acknowledge", null);
        Assert.Equal(HttpStatusCode.NoContent, ack.StatusCode);
        var again = await stay.Customer.PostAsync($"/api/customers/me/boarding/stays/{stay.StayId}/incidents/{incidentId}/acknowledge", null);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);

        var shown = await ReadAsync(await stay.Customer.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/incidents/{incidentId}"));
        Assert.Equal("Portal", shown.GetProperty("acknowledgedVia").GetString());
    }

    [Fact]
    public async Task I_F4_AnUnacknowledgedHighIncident_IsEscalatedByTheWorker_AfterTheConfiguredMinutes_AndShowsOnTheCallOwnerList()
    {
        var stay = await CheckedInStayAsync();
        var incidentId = await RecordOkAsync(stay, "High");

        await RunBoardingWorkCycleAsync();
        Assert.Empty((await ReadAsync(await stay.Branch.Admin.GetAsync("/api/staff/boarding/dashboard"))).GetProperty("callOwner").EnumerateArray());

        Factory.Clock.Advance(TimeSpan.FromMinutes(31));
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);
        await RunBoardingWorkCycleAsync();
        await RunBoardingWorkCycleAsync(); // a second pass changes nothing

        var dashboard = await ReadAsync(await stay.Branch.Admin.GetAsync("/api/staff/boarding/dashboard"));
        Assert.Contains(dashboard.GetProperty("callOwner").EnumerateArray(), i => i.GetProperty("incidentId").GetGuid() == incidentId);

        var call = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/incidents/{incidentId}/phone-call", new { outcome = "Spoke to the owner", ownerReached = true }, Json);
        Assert.Equal(HttpStatusCode.NoContent, call.StatusCode);
        Assert.Empty((await ReadAsync(await stay.Branch.Admin.GetAsync("/api/staff/boarding/dashboard"))).GetProperty("callOwner").EnumerateArray());
    }

    [Fact]
    public async Task I_F5_AnAcknowledgedHighIncident_IsNeverEscalated()
    {
        var stay = await CheckedInStayAsync();
        var incidentId = await RecordOkAsync(stay, "High");
        Assert.Equal(HttpStatusCode.NoContent, (await stay.Customer.PostAsync($"/api/customers/me/boarding/stays/{stay.StayId}/incidents/{incidentId}/acknowledge", null)).StatusCode);

        Factory.Clock.Advance(TimeSpan.FromMinutes(90));
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);
        await RunBoardingWorkCycleAsync();

        var dashboard = await ReadAsync(await stay.Branch.Admin.GetAsync("/api/staff/boarding/dashboard"));
        Assert.Empty(dashboard.GetProperty("callOwner").EnumerateArray());
        Assert.Contains(dashboard.GetProperty("openIncidents").EnumerateArray(), i => i.GetProperty("incidentId").GetGuid() == incidentId);
    }

    [Fact]
    public async Task I_F6_ResolvingAnIncident_NeedsAResolution_AndMovesItOffTheOpenList()
    {
        var stay = await CheckedInStayAsync();
        var incidentId = await RecordOkAsync(stay, "Medium");

        var blank = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/incidents/{incidentId}/resolve", new { resolution = " " }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        var resolved = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/incidents/{incidentId}/resolve", new { resolution = "Healed, vet checked" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, resolved.StatusCode);

        var open = await ReadAsync(await stay.Branch.Admin.GetAsync("/api/staff/boarding/incidents?status=Open"));
        var done = await ReadAsync(await stay.Branch.Admin.GetAsync("/api/staff/boarding/incidents?status=Resolved"));
        Assert.DoesNotContain(open.EnumerateArray(), i => i.GetProperty("id").GetGuid() == incidentId);
        Assert.Contains(done.EnumerateArray(), i => i.GetProperty("id").GetGuid() == incidentId);
    }

    [Fact]
    public async Task I_F7_TheDiaryShowsTheDaysObservationsAndPhotos_ToTheOwnerOnly_AndAHiddenPhotoDisappears()
    {
        var stay = await CheckedInStayAsync();
        var observation = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/observations",
            new { catId = stay.CatIds[0], portionEaten = "ThreeQuarters", waterIntake = "Normal", litterOutput = "Normal", mood = "Relaxed", notes = "Playful" }, Json);
        Assert.Equal(HttpStatusCode.Created, observation.StatusCode);

        var fileId = await UploadMediaFileAsync(stay.Branch.Admin);
        var add = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/media", new { fileId, catId = stay.CatIds[0], caption = "Sunbathing", capturedOnLocalDate = LocalToday }, Json);
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var mediaId = (await ReadAsync(add)).GetProperty("mediaId").GetGuid();

        var diary = await ReadAsync(await stay.Customer.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/diary"));
        var day = diary.GetProperty("days").EnumerateArray().Single(d => d.GetProperty("date").GetString() == LocalToday.ToString("yyyy-MM-dd"));
        Assert.Equal("Playful", day.GetProperty("observations")[0].GetProperty("notes").GetString());
        Assert.Equal("Sunbathing", day.GetProperty("media")[0].GetProperty("caption").GetString());

        var (stranger, _, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/diary")).StatusCode);

        var hide = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/media/{mediaId}/hide", new { reason = "Not flattering" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);
        var after = await ReadAsync(await stay.Customer.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/diary"));
        Assert.Empty(after.GetProperty("days").EnumerateArray().Single(d => d.GetProperty("date").GetString() == LocalToday.ToString("yyyy-MM-dd")).GetProperty("media").EnumerateArray());
    }

    [Fact]
    public async Task I_F8_AFileCanBeAttachedOnce_AndAnUnknownFileIsRefused()
    {
        var stay = await CheckedInStayAsync();
        var fileId = await UploadMediaFileAsync(stay.Branch.Admin);
        var url = $"/api/staff/boarding/stays/{stay.StayId}/media";

        Assert.Equal(HttpStatusCode.Created, (await stay.Branch.Admin.PostAsJsonAsync(url, new { fileId, capturedOnLocalDate = LocalToday }, Json)).StatusCode);
        var twice = await stay.Branch.Admin.PostAsJsonAsync(url, new { fileId, capturedOnLocalDate = LocalToday }, Json);
        var unknown = await stay.Branch.Admin.PostAsJsonAsync(url, new { fileId = Guid.NewGuid(), capturedOnLocalDate = LocalToday }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, twice.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidMediaReference, await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task I_F9_TheBoardingMediaPurgeSource_ErasesTheFileAndKeepsTheRowAsPurged_ButNeverAnIncidentPhoto()
    {
        var stay = await CheckedInStayAsync();
        var plainFile = await UploadMediaFileAsync(stay.Branch.Admin);
        var incidentFile = await UploadMediaFileAsync(stay.Branch.Admin);
        var incidentId = await RecordOkAsync(stay, "Medium");
        var url = $"/api/staff/boarding/stays/{stay.StayId}/media";
        var plain = (await ReadAsync(await stay.Branch.Admin.PostAsJsonAsync(url, new { fileId = plainFile, capturedOnLocalDate = LocalToday }, Json))).GetProperty("mediaId").GetGuid();
        var attached = (await ReadAsync(await stay.Branch.Admin.PostAsJsonAsync(url, new { fileId = incidentFile, incidentId, capturedOnLocalDate = LocalToday }, Json))).GetProperty("mediaId").GetGuid();

        await using var scope = Factory.Services.CreateAsyncScope();
        var source = scope.ServiceProvider.GetServices<IRetentionPurgeSource>().Single(s => s.SourceKey == "boarding-media");
        var result = await source.PurgeAsync(Now.AddDays(1), 100, CancellationToken.None);
        var second = await source.PurgeAsync(Now.AddDays(1), 100, CancellationToken.None);

        Assert.True(result.Purged >= 1);
        Assert.Equal(0, second.Purged);
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var statuses = await db.BoardingStayMedia.AsNoTracking().Where(m => m.Id == plain || m.Id == attached).ToDictionaryAsync(m => m.Id, m => m.Status.DisplayName);
        Assert.Equal("Purged", statuses[plain]);
        Assert.Equal("Visible", statuses[attached]);
    }

    [Fact]
    public async Task I_F10_TheDashboard_ShowsTheStayInHouse_AndTheOccupancy()
    {
        var stay = await CheckedInStayAsync();

        var response = await stay.Branch.Admin.GetAsync("/api/staff/boarding/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await ReadAsync(response);
        Assert.Equal(1, dashboard.GetProperty("occupancy").GetProperty("unitsOccupied").GetInt32());
        Assert.Equal(1, dashboard.GetProperty("occupancy").GetProperty("catsInHouse").GetInt32());
    }
}
