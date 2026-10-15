using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Application.Commands.Surgery;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SRG-001 gate fix round (ARCHITECTURE FAIL, CODE_REVIEW and SECURITY PASS with backlog rows): the paged lists (row 29), the read side that projects instead of rehydrating (ADR-012),
/// the clinical-access log on the discharge and reschedule read-backs (row 23), control characters in customer-visible text (row 26) and the handler-level Vet re-check (row 27).
/// </summary>
public sealed class SurgeryGateFixEndpointTests(SurgeryApiFactory factory) : SurgeryIntegrationTestBase(factory)
{
    private static string NewCode() => "G" + Guid.NewGuid().ToString("N")[..10];

    private static object CampaignBody(SurgeryWorld world, string? code = null, string description = "Free-roaming cats", decimal price = 500m) => new
    {
        code = code ?? NewCode(), name = "Community cats", description, validFrom = LocalToday(world).AddDays(-1), validTo = LocalToday(world).AddDays(30),
        branchIds = Array.Empty<Guid>(), prices = new[] { new { serviceId = world.SpayServiceId, amount = price } }, maxCases = (int?)null
    };

    private Task<Guid> SeedAnotherCatAsync(SurgeryWorld world) => SeedCatOfAsync(world.CustomerId, CatSex.Female, name: "Cat" + Guid.NewGuid().ToString("N")[..4]);

    private async Task<int> AccessRowsAsync(Guid recordId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        return await db.Set<AuditEntryAggregate>().AsNoTracking()
            .CountAsync(a => a.Category == AuditCategory.ClinicalRecordAccess && a.EntityId == recordId);
    }

    // ── row 29 -- paging ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TheCampaignList_IsPaged_WithATotal_AndRefusesABadPageSize()
    {
        var world = await SeedSurgeryWorldAsync();
        foreach (var _ in new[] { 1, 2, 3 })
            Assert.Equal(HttpStatusCode.Created, (await world.Admin.PostAsJsonAsync("/api/staff/surgery-campaigns", CampaignBody(world), Json)).StatusCode);

        var first = await world.Reception.GetFromJsonAsync<JsonElement>("/api/staff/surgery-campaigns?pageNumber=1&pageSize=2", Json);
        Assert.Equal(2, first.GetProperty("items").GetArrayLength());
        Assert.Equal(1, first.GetProperty("pageNumber").GetInt32());
        Assert.Equal(2, first.GetProperty("pageSize").GetInt32());
        Assert.True(first.GetProperty("totalCount").GetInt32() >= 3);

        var second = await world.Reception.GetFromJsonAsync<JsonElement>("/api/staff/surgery-campaigns?pageNumber=2&pageSize=2", Json);
        var firstIds = first.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).ToHashSet();
        Assert.DoesNotContain(second.GetProperty("items").EnumerateArray(), c => firstIds.Contains(c.GetProperty("id").GetGuid()));

        var oversized = await world.Reception.GetFromJsonAsync<JsonElement>("/api/staff/surgery-campaigns?pageSize=100000", Json);
        Assert.Equal(PagingRules.MaxPageSize, oversized.GetProperty("pageSize").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await world.Reception.GetAsync("/api/staff/surgery-campaigns?pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Reception.GetAsync($"/api/staff/surgery-campaigns?pageNumber={int.MaxValue}")).StatusCode);
    }

    [Fact]
    public async Task TheCampaignCases_ArePaged_ButTheTotalsCoverEveryCase()
    {
        var world = await SeedSurgeryWorldAsync();
        var created = await world.Admin.PostAsJsonAsync("/api/staff/surgery-campaigns", CampaignBody(world), Json);
        var campaignId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("campaignId").GetGuid();
        var cat2 = await SeedAnotherCatAsync(world);
        foreach (var (cat, hour) in new[] { ((Guid?)null, 10), (cat2, 12) })
        {
            var response = await TryBookAsync(world.Reception, BookBody(world, LocalStart(world, 2, hour), catId: cat, campaignId: campaignId, eligibilityNote: "Community cat"));
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        }

        var page = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgery-campaigns/{campaignId}/cases?pageNumber=1&pageSize=1", Json);

        Assert.Equal(1, page.GetProperty("cases").GetArrayLength());
        Assert.Equal(2, page.GetProperty("totalCases").GetInt32());
        Assert.Equal(1, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, page.GetProperty("totals").GetProperty("count").GetInt32()); // the totals are not paged
        var all = await world.Reception.GetFromJsonAsync<JsonElement>($"/api/staff/surgery-campaigns/{campaignId}/cases", Json);
        Assert.Equal(2, all.GetProperty("cases").GetArrayLength());
        Assert.Equal(all.GetProperty("totals").GetProperty("campaignTotal").GetProperty("amount").GetString(), page.GetProperty("totals").GetProperty("campaignTotal").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task TheCustomersSurgeryList_IsPaged()
    {
        var world = await SeedSurgeryWorldAsync();
        await BookAsync(world, LocalStart(world, 2, 10));
        await BookAsync(world, LocalStart(world, 2, 13), catId: await SeedAnotherCatAsync(world));

        var page = await world.Customer.GetFromJsonAsync<JsonElement>("/api/customers/me/surgeries?pageNumber=1&pageSize=1", Json);

        Assert.Equal(1, page.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        var next = await world.Customer.GetFromJsonAsync<JsonElement>("/api/customers/me/surgeries?pageNumber=2&pageSize=1", Json);
        Assert.Equal(1, next.GetProperty("items").GetArrayLength());
        Assert.NotEqual(page.GetProperty("items")[0].GetProperty("id").GetGuid(), next.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await world.Customer.GetAsync("/api/customers/me/surgeries?pageSize=-1")).StatusCode);
    }

    // ── ADR-012 -- the read side projects ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>The applicable-campaigns read now decides over projected columns with the booking's own rules: the cap, the branch, the date and the service all still hold.</summary>
    [Fact]
    public async Task TheApplicableCampaigns_StillHonourTheDateServiceAndCap()
    {
        var world = await SeedSurgeryWorldAsync();
        var capped = await world.Admin.PostAsJsonAsync("/api/staff/surgery-campaigns", new
        {
            code = NewCode(), name = "Capped", description = "x", validFrom = LocalToday(world).AddDays(-1), validTo = LocalToday(world).AddDays(30), branchIds = Array.Empty<Guid>(),
            prices = new[] { new { serviceId = world.SpayServiceId, amount = 400m } }, maxCases = 1
        }, Json);
        var cappedId = (await capped.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("campaignId").GetGuid();
        string Url(Guid serviceId, int days) => $"/api/staff/surgery-campaigns/applicable?serviceId={serviceId}&date={LocalToday(world).AddDays(days):yyyy-MM-dd}";

        var open = await world.Reception.GetFromJsonAsync<JsonElement>(Url(world.SpayServiceId, 3), Json);
        var row = Assert.Single(open.EnumerateArray(), c => c.GetProperty("id").GetGuid() == cappedId);
        Assert.Equal(1, row.GetProperty("casesRemaining").GetInt32());

        Assert.DoesNotContain((await world.Reception.GetFromJsonAsync<JsonElement>(Url(world.NeuterServiceId, 3), Json)).EnumerateArray(), c => c.GetProperty("id").GetGuid() == cappedId);
        Assert.DoesNotContain((await world.Reception.GetFromJsonAsync<JsonElement>(Url(world.SpayServiceId, 90), Json)).EnumerateArray(), c => c.GetProperty("id").GetGuid() == cappedId);

        var booked = await TryBookAsync(world.Reception, BookBody(world, LocalStart(world, 3, 10), campaignId: cappedId, eligibilityNote: "Community cat"));
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        Assert.DoesNotContain((await world.Reception.GetFromJsonAsync<JsonElement>(Url(world.SpayServiceId, 3), Json)).EnumerateArray(), c => c.GetProperty("id").GetGuid() == cappedId);
    }

    // ── row 23 -- the read-back after discharge and reschedule is a logged clinical read ───────────────────────────────────

    [Fact]
    public async Task TheDischargeAndRescheduleReadBacks_WriteAClinicalAccessRow()
    {
        var world = await SeedSurgeryWorldAsync();

        var scheduled = await BookAsync(world, LocalStart(world, 2, 10));
        var version = (await GetSurgeryAsync(world.Vet, scheduled)).GetProperty("version").GetInt32();
        var beforeReschedule = await AccessRowsAsync(scheduled);
        var moved = await world.Reception.PostAsJsonAsync($"/api/staff/surgeries/{scheduled}/reschedule",
            new { expectedVersion = version, startsOn = LocalStart(world, 2, 12), theatreId = world.TheatreId, surgeonId = world.VetId, assistantId = world.AssistantId }, Json);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(beforeReschedule + 1, await AccessRowsAsync(scheduled));
        Assert.Contains("no-store", moved.Headers.CacheControl?.ToString());

        var (surgeryId, _) = await InTheatreAsync(world, catId: await SeedAnotherCatAsync(world), hour: 15);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/end-procedure", new { outcome = "Completed" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Nurse.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/recovery-status", new { status = "ReadyForDischarge" }, Json)).StatusCode);
        var beforeDischarge = await AccessRowsAsync(surgeryId);
        var discharged = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/discharge", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, discharged.StatusCode);
        Assert.Equal(beforeDischarge + 1, await AccessRowsAsync(surgeryId));
    }

    // ── row 26 -- control characters ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ControlCharacters_AreRefused_InDischargeInstructions_AndTheCampaignDescription()
    {
        var world = await SeedSurgeryWorldAsync();

        var campaign = await world.Admin.PostAsJsonAsync("/api/staff/surgery-campaigns", CampaignBody(world, description: "Free-roaming\u0000cats"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, campaign.StatusCode);
        var escape = await world.Admin.PostAsJsonAsync("/api/staff/surgery-campaigns", CampaignBody(world, description: "Free-roaming\u001bcats"), Json);
        Assert.Equal(HttpStatusCode.BadRequest, escape.StatusCode);

        var (surgeryId, _) = await InTheatreAsync(world);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/end-procedure", new { outcome = "Completed" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await world.Nurse.PutAsJsonAsync($"/api/staff/surgeries/{surgeryId}/recovery-status", new { status = "ReadyForDischarge" }, Json)).StatusCode);

        var bad = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/discharge", new { postOpInstructions = "Keep indoors\u0007for ten days." }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("Recovery", (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("status").GetString());

        // Ordinary line breaks in the instructions remain fine.
        var good = await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/discharge", new { postOpInstructions = "Keep indoors.\nNo jumping for ten days." }, Json);
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    // ── row 27 -- handler-level Vet re-check ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task EndingAProcedure_AndApplyingASterilisationOutcome_AreRefusedByTheHandler_ForANonVet()
    {
        var world = await SeedSurgeryWorldAsync();
        var (surgeryId, _) = await InTheatreAsync(world);
        await using var scope = world.Api.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var nurse = new UserContext(world.NurseId, Guid.NewGuid());

        // Past [RequireAction] (called straight at the handler), the handler itself still refuses a caller who is not a vet.
        var end = await dispatcher.SendAsync(new EndSurgicalProcedureCommand
        { SurgeryId = surgeryId, BranchId = world.BranchId, Outcome = "Completed", UserContext = nurse, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        Assert.False(end.IsSuccess);
        Assert.Contains(end.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.VeterinarianRequired);
        Assert.Equal("InTheatre", (await GetSurgeryAsync(world.Vet, surgeryId)).GetProperty("status").GetString());

        var apply = await dispatcher.SendAsync(new ApplySterilisationOutcomeCommand
        { SurgeryId = surgeryId, BranchId = world.BranchId, UserContext = nurse, CorrelationId = Guid.NewGuid(), ValidateCommand = true });
        Assert.False(apply.IsSuccess);
        Assert.Contains(apply.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.VeterinarianRequired);

        // The vet still ends it.
        Assert.Equal(HttpStatusCode.NoContent, (await world.Vet.PostAsJsonAsync($"/api/staff/surgeries/{surgeryId}/end-procedure", new { outcome = "Completed" }, Json)).StatusCode);
    }
}
