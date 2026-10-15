using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-SRG-001 phase G -- US-SRG-11: community-cat campaign price lists, applied at booking, snapshotted, locked once used and reported separately.</summary>
public sealed class SurgeryCampaignEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    private static string NewCode() => "T" + Guid.NewGuid().ToString("N")[..10];

    private static object CampaignBody(
        SurgeryWorld world, string? code = null, DateOnly? from = null, DateOnly? to = null, int? maxCases = null, Guid[]? branchIds = null, decimal spayPrice = 500m, Guid? serviceId = null) => new
    {
        code = code ?? NewCode(), name = "Community cats", description = "Free-roaming cats", validFrom = from ?? LocalToday(world).AddDays(-1), validTo = to ?? LocalToday(world).AddDays(30),
        branchIds = branchIds ?? [], prices = new[] { new { serviceId = serviceId ?? world.SpayServiceId, amount = spayPrice } }, maxCases
    };

    private static async Task<Guid> CreateCampaignAsync(SurgeryWorld world, object body)
    {
        var response = await world.Admin.PostAsJsonAsync("/api/staff/surgery-campaigns", body, Json);
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("campaignId").GetGuid();
    }

    private Task<HttpResponseMessage> BookUnderAsync(SurgeryWorld world, Guid? campaignId, string? note, DateTimeOffset startsOn, Guid? catId = null) =>
        TryBookAsync(world.Reception, BookBody(world, startsOn, catId: catId, campaignId: campaignId, eligibilityNote: note));

    private static async Task<Guid> SurgeryIdOfAsync(HttpResponseMessage response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("surgeryId").GetGuid();
    }

    /// <summary>I-G1 -- book under an active campaign: the snapshot is the campaign price; the case appears in /cases with the standard price for comparison (US-SRG-11).</summary>
    [Fact]
    public async Task I_G1_ABookingUnderACampaign_IsSnapshottedAtTheCampaignPrice_AndListedWithTheStandardPrice()
    {
        var world = await SeedSurgeryWorldAsync();
        var campaignId = await CreateCampaignAsync(world, CampaignBody(world));

        var applicable = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgery-campaigns/applicable?serviceId={world.SpayServiceId}&date={LocalToday(world).AddDays(3):yyyy-MM-dd}", Json);
        var offered = Assert.Single(applicable.EnumerateArray(), c => c.GetProperty("id").GetGuid() == campaignId);
        Assert.Equal("500.00", offered.GetProperty("price").GetProperty("amount").GetString());

        var surgeryId = await SurgeryIdOfAsync(await BookUnderAsync(world, campaignId, "Volunteer carer, letter on file", LocalStart(world, 3, 10)));

        var snapshot = (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("priceSnapshot");
        Assert.Equal("Campaign", snapshot.GetProperty("basis").GetString());
        Assert.Equal("500.00", snapshot.GetProperty("amount").GetProperty("amount").GetString());
        Assert.Equal(campaignId, snapshot.GetProperty("campaignId").GetGuid());
        Assert.Equal("Volunteer carer, letter on file", snapshot.GetProperty("campaignEligibilityNote").GetString());

        var cases = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgery-campaigns/{campaignId}/cases", Json);
        var row = Assert.Single(cases.GetProperty("cases").EnumerateArray());
        Assert.Equal(surgeryId, row.GetProperty("surgeryId").GetGuid());
        Assert.Equal("500.00", row.GetProperty("campaignPrice").GetProperty("amount").GetString());
        Assert.Equal("5000.00", row.GetProperty("standardPrice").GetProperty("amount").GetString());
        var totals = cases.GetProperty("totals");
        Assert.Equal(1, totals.GetProperty("count").GetInt32());
        Assert.Equal("500.00", totals.GetProperty("campaignTotal").GetProperty("amount").GetString());
        Assert.Equal("5000.00", totals.GetProperty("standardTotal").GetProperty("amount").GetString());

        // A date range that excludes the surgery lists nothing; a booking without a campaign is not a campaign case.
        var empty = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgery-campaigns/{campaignId}/cases?from={LocalToday(world).AddDays(10):yyyy-MM-dd}", Json);
        Assert.Empty(empty.GetProperty("cases").EnumerateArray());
        var standard = await BookAsync(world, LocalStart(world, 4, 10), catId: await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Plain"));
        Assert.Equal("Standard", (await GetSurgeryAsync(world.Vet, standard)).GetProperty("priceSnapshot").GetProperty("basis").GetString());
        Assert.Single((await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgery-campaigns/{campaignId}/cases", Json)).GetProperty("cases").EnumerateArray());

        Assert.Equal(HttpStatusCode.NotFound, (await world.Reception.GetAsync($"/api/staff/surgery-campaigns/{Guid.NewGuid()}/cases")).StatusCode);
    }

    /// <summary>I-G2 -- a campaign capped at one case refuses the second booking; cancelling the first frees the place.</summary>
    [Fact]
    public async Task I_G2_TheCap_RefusesTheSecondBooking_UntilTheFirstIsCancelled()
    {
        var world = await SeedSurgeryWorldAsync();
        var campaignId = await CreateCampaignAsync(world, CampaignBody(world, maxCases: 1));
        var first = await SurgeryIdOfAsync(await BookUnderAsync(world, campaignId, "Volunteer carer", LocalStart(world, 3, 10)));
        var other = await SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Second");

        var refused = await BookUnderAsync(world, campaignId, "Volunteer carer", LocalStart(world, 3, 14), other);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var refusal = (await ErrorsOfAsync(refused)).Single();
        Assert.Equal(BusinessViolationCodes.CampaignNotApplicable, refusal.GetProperty("errorCode").GetString());
        Assert.Contains("limit of 1 case", refusal.GetProperty("errorMessage").GetString());

        var applicable = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgery-campaigns/applicable?serviceId={world.SpayServiceId}&date={LocalToday(world).AddDays(3):yyyy-MM-dd}", Json);
        Assert.DoesNotContain(applicable.EnumerateArray(), c => c.GetProperty("id").GetGuid() == campaignId);

        Assert.Equal(HttpStatusCode.NoContent, (await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{first}/cancel", new { reason = "Owner called to cancel" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await BookUnderAsync(world, campaignId, "Volunteer carer", LocalStart(world, 3, 14), other)).StatusCode);
    }

    [Fact]
    public async Task ABookingUnderACampaign_IsRefusedForEachReason()
    {
        var world = await SeedSurgeryWorldAsync();
        var start = LocalStart(world, 3, 10);
        var today = LocalToday(world);
        var note = "Volunteer carer";

        var expired = await CreateCampaignAsync(world, CampaignBody(world, from: today.AddDays(-30), to: today.AddDays(-2)));
        var otherBranch = await CreateCampaignAsync(world, CampaignBody(world, branchIds: [Guid.NewGuid()]));
        var inactive = await CreateCampaignAsync(world, CampaignBody(world));
        var live = await CreateCampaignAsync(world, CampaignBody(world));
        Assert.Equal(HttpStatusCode.NoContent, (await world.Admin.PostAsJsonAsync($"/api/staff/surgery-campaigns/{inactive}/active", new { isActive = false }, Json)).StatusCode);

        async Task<string> RefusalAsync(Guid? campaign, string? eligibility, Guid? service = null, Guid? cat = null)
        {
            var response = await TryBookAsync(world.Reception, BookBody(world, start, serviceId: service, catId: cat, campaignId: campaign, eligibilityNote: eligibility));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = (await ErrorsOfAsync(response)).Single();
            Assert.Equal(BusinessViolationCodes.CampaignNotApplicable, error.GetProperty("errorCode").GetString());
            return error.GetProperty("errorMessage").GetString()!;
        }

        Assert.Contains("runs from", await RefusalAsync(expired, note));
        Assert.Contains("does not cover this branch", await RefusalAsync(otherBranch, note));
        Assert.Contains("not active", await RefusalAsync(inactive, note));
        Assert.Contains("eligibility note", await RefusalAsync(live, null));
        Assert.Contains("eligibility note", await RefusalAsync(live, "  "));
        Assert.Contains("no price for this procedure", await RefusalAsync(live, note, world.NeuterServiceId, await SeedCatOfAsync(world.CustomerId, CatSex.Male, name: "Tom")));
        Assert.Contains("does not exist", await RefusalAsync(Guid.NewGuid(), note));

        // None of the refusals left a surgery behind.
        Assert.Empty((await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgeries/day?date={today.AddDays(3):yyyy-MM-dd}", Json)).GetProperty("surgeries").EnumerateArray());
    }

    [Fact]
    public async Task Campaigns_AreManagedByAnAdministrator_AndReadByStaff()
    {
        var world = await SeedSurgeryWorldAsync();
        var body = CampaignBody(world);

        foreach (var client in new[] { world.Reception, world.Nurse, world.Vet })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/staff/surgery-campaigns", body, Json)).StatusCode);

        var campaignId = await CreateCampaignAsync(world, body);
        var list = await world.Reception.GetFromJsonAsync<JsonElement>("/api/staff/surgery-campaigns?pageSize=100", Json);
        var row = Assert.Single(list.GetProperty("items").EnumerateArray(), c => c.GetProperty("id").GetGuid() == campaignId);
        Assert.True(row.GetProperty("isActive").GetBoolean());
        Assert.Equal(0, row.GetProperty("casesBooked").GetInt32());
        Assert.Equal(world.SpayServiceId, Assert.Single(row.GetProperty("prices").EnumerateArray()).GetProperty("serviceId").GetGuid());
        Assert.False(string.IsNullOrEmpty(row.GetProperty("prices")[0].GetProperty("serviceName").GetString()));

        Assert.Equal(HttpStatusCode.Forbidden, (await world.Reception.PostAsJsonAsync($"/api/staff/surgery-campaigns/{campaignId}/active", new { isActive = false }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.PostAsJsonAsync($"/api/staff/surgery-campaigns/{Guid.NewGuid()}/active", new { isActive = false }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Admin.PostAsJsonAsync($"/api/staff/surgery-campaigns/{campaignId}/active", new { isActive = false }, Json)).StatusCode);
        var active = await world.Reception.GetFromJsonAsync<JsonElement>("/api/staff/surgery-campaigns?activeOnly=true&pageSize=100", Json);
        Assert.DoesNotContain(active.GetProperty("items").EnumerateArray(), c => c.GetProperty("id").GetGuid() == campaignId);
    }

    [Fact]
    public async Task ACampaign_NeedsAUniqueCode_AndSterilisationPricesOnly()
    {
        var world = await SeedSurgeryWorldAsync();
        var code = NewCode();
        await CreateCampaignAsync(world, CampaignBody(world, code: code));

        var duplicate = await world.Admin.PostAsJsonAsync("/api/staff/surgery-campaigns", CampaignBody(world, code: code.ToLowerInvariant()), Json);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal([BusinessViolationCodes.CampaignCodeAlreadyExists], await CodesOfAsync(duplicate));

        var dental = await world.Admin.PostAsJsonAsync("/api/staff/surgery-campaigns", CampaignBody(world, serviceId: world.DentalServiceId), Json);
        Assert.Equal(HttpStatusCode.BadRequest, dental.StatusCode);
        Assert.Equal([BusinessViolationCodes.CampaignServiceNotSterilisation], await CodesOfAsync(dental));

        var backwards = await world.Admin.PostAsJsonAsync("/api/staff/surgery-campaigns", CampaignBody(world, from: LocalToday(world).AddDays(5), to: LocalToday(world)), Json);
        Assert.Equal([BusinessViolationCodes.SurgeryCampaignInvalid], await CodesOfAsync(backwards));
    }

    /// <summary>U-G3 (integration) -- once a surgery is booked under a campaign its prices are locked; the dates and cap can still change; a stale version is a 409.</summary>
    [Fact]
    public async Task ThePrices_LockOnceASurgeryIsBooked()
    {
        var world = await SeedSurgeryWorldAsync();
        var campaignId = await CreateCampaignAsync(world, CampaignBody(world));
        var version = (await world.Reception.GetFromJsonAsync<JsonElement>("/api/staff/surgery-campaigns?pageSize=100", Json)).GetProperty("items").EnumerateArray().First(c => c.GetProperty("id").GetGuid() == campaignId).GetProperty("version").GetInt32();

        // Before any booking the price may change.
        var repriced = await world.Admin.PutAsJsonAsync($"/api/staff/surgery-campaigns/{campaignId}", UpdateBody(world, version, spayPrice: 450m), Json);
        Assert.Equal(HttpStatusCode.NoContent, repriced.StatusCode);
        version = (await ListRowAsync(world, campaignId)).GetProperty("version").GetInt32();

        await SurgeryIdOfAsync(await BookUnderAsync(world, campaignId, "Volunteer carer", LocalStart(world, 3, 10)));

        var locked = await world.Admin.PutAsJsonAsync($"/api/staff/surgery-campaigns/{campaignId}", UpdateBody(world, version, spayPrice: 400m), Json);
        Assert.Equal(HttpStatusCode.BadRequest, locked.StatusCode);
        Assert.Equal([BusinessViolationCodes.CampaignPriceLocked], await CodesOfAsync(locked));
        Assert.True((await ListRowAsync(world, campaignId)).GetProperty("pricesLocked").GetBoolean());

        var stale = await world.Admin.PutAsJsonAsync($"/api/staff/surgery-campaigns/{campaignId}", UpdateBody(world, version - 1, spayPrice: 450m), Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal([BusinessViolationCodes.StaleVersion], await CodesOfAsync(stale));

        var extended = await world.Admin.PutAsJsonAsync($"/api/staff/surgery-campaigns/{campaignId}", UpdateBody(world, version, spayPrice: 450m, maxCases: 10), Json);
        Assert.Equal(HttpStatusCode.NoContent, extended.StatusCode);
        Assert.Equal(10, (await ListRowAsync(world, campaignId)).GetProperty("maxCases").GetInt32());
        Assert.Equal(9, (await ListRowAsync(world, campaignId)).GetProperty("casesRemaining").GetInt32());

        // The earlier booking kept its snapshot: 450 was the price when it was made.
        Assert.Equal(HttpStatusCode.NotFound, (await world.Admin.PutAsJsonAsync($"/api/staff/surgery-campaigns/{Guid.NewGuid()}", UpdateBody(world, 0), Json)).StatusCode);
    }

    private static async Task<JsonElement> ListRowAsync(SurgeryWorld world, Guid campaignId) =>
        (await world.Reception.GetFromJsonAsync<JsonElement>("/api/staff/surgery-campaigns?pageSize=100", Json)).GetProperty("items").EnumerateArray().First(c => c.GetProperty("id").GetGuid() == campaignId);

    private static object UpdateBody(SurgeryWorld world, int expectedVersion, decimal spayPrice = 500m, int? maxCases = null) => new
    {
        expectedVersion, name = "Community cats", description = "Free-roaming cats", validFrom = LocalToday(world).AddDays(-1), validTo = LocalToday(world).AddDays(30), branchIds = Array.Empty<Guid>(),
        prices = new[] { new { serviceId = world.SpayServiceId, amount = spayPrice } }, maxCases
    };
}
