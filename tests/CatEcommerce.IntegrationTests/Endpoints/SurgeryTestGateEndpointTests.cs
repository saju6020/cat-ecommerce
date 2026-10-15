using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Read.Queries.Surgery;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SRG-001 TEST gate (2026-10-11) -- the cases the earlier gates referred to TEST that had no test: the cat-history list paging (no HTTP route), the recovery-history bound through the API,
/// and the customer's view of a surgery after the cat changes owner.
/// </summary>
public sealed class SurgeryTestGateEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    private static Task<HttpResponseMessage> SetStatusAsync(HttpClient client, Guid surgeryId, string status) =>
        client.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/recovery-status", new { status }, Json);

    /// <summary>T-12 -- the cat's surgery history is a paged result: three surgeries, page size 2, a total of 3, two pages, no repeats, newest first.</summary>
    [Fact]
    public async Task TheCatSurgeryHistory_IsPaged_WithATotal()
    {
        var world = await SeedSurgeryWorldAsync();
        var ids = new List<Guid>();
        for (var day = 1; day <= 3; day++) ids.Add(await BookAsync(world, LocalStart(world, day, 10)));

        await using var scope = world.Api.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();

        var first = (await dispatcher.GetResultAsync(new GetCatSurgeriesQuery { CatId = world.CatId, PageNumber = 1, PageSize = 2, CorrelationId = Guid.NewGuid() })).Result!;
        var second = (await dispatcher.GetResultAsync(new GetCatSurgeriesQuery { CatId = world.CatId, PageNumber = 2, PageSize = 2, CorrelationId = Guid.NewGuid() })).Result!;

        Assert.Equal(3, first.TotalCount);
        Assert.Equal(3, second.TotalCount);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);
        var seen = first.Items.Concat(second.Items).Select(i => i.SurgeryId).ToList();
        Assert.Equal(ids.OrderBy(x => x), seen.OrderBy(x => x));
        Assert.Equal(ids[2], first.Items[0].SurgeryId); // newest first
    }

    /// <summary>T-13 -- the recovery history is bounded at 50 rows (the initial Recovering row plus 49 changes): the next change is refused with RecoveryHistoryLimitReached and nothing changes. Observation recorded in the TEST gate:
    /// once the bound is hit the surgery cannot be moved to ReadyForDischarge, which discharge requires.</summary>
    [Fact]
    public async Task TheRecoveryHistory_IsBounded_AtFiftyRows_ThroughTheApi()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/end-procedure", new { outcome = "Completed" }, Json)).StatusCode);

        // The surgery enters Recovery as "Recovering" and that entry is the first of the 50 history rows, so 49 real changes fit. Alternate Stable / Recovering so every call is a real change.
        var accepted = 0;
        HttpResponseMessage? refused = null;
        for (var change = 1; change <= 60 && refused is null; change++)
        {
            var response = await SetStatusAsync(world.Nurse, surgeryId, change % 2 == 1 ? "Stable" : "Recovering");
            if (response.StatusCode == HttpStatusCode.NoContent) accepted++;
            else refused = response;
        }

        Assert.NotNull(refused);
        Assert.Equal(49, accepted);
        Assert.Equal(HttpStatusCode.BadRequest, refused!.StatusCode);
        Assert.Equal([BusinessViolationCodes.RecoveryHistoryLimitReached], await CodesOfAsync(refused));

        var detail = await GetSurgeryAsync(world.Vet, surgeryId);
        Assert.Equal(50, detail.GetProperty("recoveryHistory").GetArrayLength());
        Assert.Equal("Stable", detail.GetProperty("recoveryStatus").GetString()); // the 49th change was odd = Stable; the refused one changed nothing

        // The bound also refuses the move discharge needs (ReadyForDischarge): recorded as a finding, asserted so a change is noticed.
        var ready = await SetStatusAsync(world.Nurse, surgeryId, "ReadyForDischarge");
        Assert.Equal([BusinessViolationCodes.RecoveryHistoryLimitReached], await CodesOfAsync(ready));
    }

    /// <summary>T-20 -- after the cat changes owner, a FUTURE Scheduled surgery belongs to the new owner: the old owner gets 404 on it and an empty list, the new owner reads it.</summary>
    [Fact]
    public async Task AfterAnOwnershipTransfer_AFutureSurgeryIsTheNewOwnersAlone()
    {
        var world = await SeedSurgeryWorldAsync();
        var newOwner = await SeedSurgeryWorldAsync(); // its own customer account and client
        var surgeryId = await BookAsync(world, LocalStart(world, 3, 10));
        Assert.Equal(HttpStatusCode.OK, (await world.Customer.GetAsync($"/api/customers/me/surgeries/{surgeryId}")).StatusCode);

        var transfer = await world.Admin.PostAsJsonAsync($"/api/cats/{world.CatId}/ownership-transfer", new { newOwnerId = newOwner.CustomerId, reason = "Cat rehomed to a relative" }, Json);
        Assert.True(transfer.StatusCode == HttpStatusCode.OK, $"{(int)transfer.StatusCode}: {await transfer.Content.ReadAsStringAsync()}");

        Assert.Equal(HttpStatusCode.NotFound, (await world.Customer.GetAsync($"/api/customers/me/surgeries/{surgeryId}")).StatusCode);
        Assert.Empty((await world.Customer.GetFromJsonAsync<JsonElement>("/api/customers/me/surgeries", Json)).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await newOwner.Customer.GetAsync($"/api/customers/me/surgeries/{surgeryId}")).StatusCode);
        Assert.Contains((await newOwner.Customer.GetFromJsonAsync<JsonElement>("/api/customers/me/surgeries", Json)).GetProperty("items").EnumerateArray(), s => s.GetProperty("id").GetGuid() == surgeryId);
    }

    /// <summary>T-21 -- OBSERVATION of SECURITY backlog row 24: a surgery already in care (InTheatre) when the cat changes owner. The test asserts what happens today so a change is noticed; it does not say
    /// the behaviour is right. Today the in-care surgery stays with the previous owner.</summary>
    [Fact]
    public async Task AfterAnOwnershipTransfer_ASurgeryAlreadyInCare_StaysWithThePreviousOwner_Today()
    {
        var world = await SeedSurgeryWorldAsync();
        var newOwner = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);

        var transfer = await world.Admin.PostAsJsonAsync($"/api/cats/{world.CatId}/ownership-transfer", new { newOwnerId = newOwner.CustomerId, reason = "Cat rehomed to a relative" }, Json);
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await world.Customer.GetAsync($"/api/customers/me/surgeries/{surgeryId}")).StatusCode); // previous owner still sees it (backlog 24)
        Assert.Equal(HttpStatusCode.NotFound, (await newOwner.Customer.GetAsync($"/api/customers/me/surgeries/{surgeryId}")).StatusCode);
    }
    /// <summary>T-25 -- OBSERVATION of SECURITY backlog row 22 (owner decision pending): Reception can read the anaesthesia log and the surgical report today. Asserts current behaviour so a change is noticed.</summary>
    [Fact]
    public async Task Reception_CanReadTheAnaesthesiaLog_AndTheReport_Today()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/end-procedure", new { outcome = "Completed" }, Json)).StatusCode);
        var draft = await world.Vet.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/report", new
        {
            expectedVersion = 0, procedurePerformed = "Ovariohysterectomy", findings = "Normal", technique = "Midline approach", sutureMaterial = "Vicryl 3-0", hadComplications = false,
            complications = (string?)null, surgeonId = world.VetId, assistantId = world.AssistantId
        }, Json);
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await world.Reception.GetAsync($"/api/staff/surgeries/{surgeryId}/anaesthesia-log")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await world.Reception.GetAsync($"/api/staff/surgeries/{surgeryId}/report")).StatusCode);
    }
}
