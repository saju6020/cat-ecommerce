using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BRD-001 Phase B -- I-A1, I-A2, I-B1, I-B4 and the rate-card, maintenance and routine rules of section 8.2.</summary>
// One collection: the Boarding worker pass reads every stay in the shared database, so classes with different fake clocks must not overlap.
[Collection("Boarding")]
public sealed class BoardingUnitsEndpointTests(BoardingApiFactory factory) : BoardingIntegrationTestBase(factory)
{
    [Fact]
    public async Task I_A1_TheBoardingSchemaExists_WithItsExclusionConstraintsAndRetentionTriggers()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();

        var constraints = await db.Database.SqlQuery<string>($"""
            SELECT c.conname AS "Value" FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace WHERE n.nspname = 'boarding' AND c.contype = 'x'
            """).ToListAsync();
        Assert.Contains("ex_unit_maintenance", constraints);

        var guarded = await db.Database.SqlQuery<string>($"""
            SELECT DISTINCT c.relname AS "Value" FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'boarding' AND t.tgname = 'trg_refuse_hard_delete'
            """).ToListAsync();
        Assert.Contains("unit", guarded);
        Assert.Contains("rate_card", guarded);
    }

    [Fact]
    public async Task I_A2_TheHostStartsWithScopeValidation_AndTheBoardingEndpointsResolve()
    {
        var response = await CreateClient().GetAsync("/api/boarding/unit-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task I_B1_AdminCreatesUnitsOfEachType_AndTheUnitReadShowsTheTypesNightlyRate()
    {
        var branch = await SeedBranchAsync();
        var standard = await CreateUnitAsync(branch, "Standard", 1, block: "A");
        var deluxe = await CreateUnitAsync(branch, "Deluxe", 2, block: "B");
        await CreateUnitAsync(branch, "Family", 4, block: "C");
        await CreateUnitAsync(branch, "Isolation", 1, block: "Z");
        await SetRateCardAsync(branch, "Standard", nightly: "1000.00");

        var units = await ReadAsync(await branch.Admin.GetAsync("/api/staff/boarding/units"));

        Assert.Equal(4, units.GetArrayLength());
        var standardRow = units.EnumerateArray().Single(u => u.GetProperty("id").GetGuid() == standard);
        Assert.Equal("1000.00", standardRow.GetProperty("nightlyRate").GetProperty("amount").GetString());
        var deluxeRow = units.EnumerateArray().Single(u => u.GetProperty("id").GetGuid() == deluxe);
        Assert.Equal(JsonValueKind.Null, deluxeRow.GetProperty("nightlyRate").ValueKind); // no card: not bookable

        var block = await branch.Admin.PostAsJsonAsync($"/api/staff/boarding/units/{standard}/maintenance",
            new { startsOn = "2031-03-01", endsOn = "2031-03-03", reason = "Repaint" }, Json);
        Assert.Equal(HttpStatusCode.Created, block.StatusCode);
        var blocks = await ReadAsync(await branch.Admin.GetAsync($"/api/staff/boarding/units/{standard}/maintenance"));
        Assert.Equal("Repaint", blocks[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task I_B4_ANurseCannotCreateAUnit_ButCanReadThem()
    {
        var branch = await SeedBranchAsync();
        var nurse = await SeedStaffAsync(StaffRoles.Nurse);
        var assign = await branch.Admin.PutAsJsonAsync($"/api/admin/staff/{nurse.StaffAccountId}/branches", new { branchIds = new[] { branch.BranchId }, primaryBranchId = branch.BranchId }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);
        var nurseClient = await SignInStaffAsync(nurse, branch.BranchId);

        var create = await nurseClient.PostAsJsonAsync("/api/staff/boarding/units", new { code = NewCode(), name = "x", unitType = "Standard", capacityCats = 1 }, Json);
        var read = await nurseClient.GetAsync("/api/staff/boarding/units");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task ADuplicateUnitCode_IsRefusedWithTheCodeAlreadyExistsViolation()
    {
        var branch = await SeedBranchAsync();
        var code = NewCode();
        await CreateUnitAsync(branch, code: code);

        var again = await branch.Admin.PostAsJsonAsync("/api/staff/boarding/units", new { code, name = "dup", unitType = "Standard", capacityCats = 1 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains(BusinessViolationCodes.UnitCodeAlreadyExists, await again.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ACapacityAboveWhatTheTypeAllows_IsRefused()
    {
        var branch = await SeedBranchAsync();

        var isolationOfThree = await branch.Admin.PostAsJsonAsync("/api/staff/boarding/units", new { code = NewCode(), name = "iso", unitType = "Isolation", capacityCats = 3 }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, isolationOfThree.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidBoardingUnit, await isolationOfThree.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RateCards_AreAppendOnly_NeverBackdated_AndTheLatestEffectiveWins()
    {
        var branch = await SeedBranchAsync();
        await SetRateCardAsync(branch, "Deluxe", nightly: "1500.00");

        var backdated = await branch.Admin.PostAsJsonAsync("/api/staff/boarding/rate-cards", new
        {
            unitType = "Deluxe", nightlyRate = "10.00", weeklyRate = "10.00", monthlyRate = "10.00", taxClassId = branch.TaxClassId, effectiveFrom = Now.AddDays(-1)
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, backdated.StatusCode);
        Assert.Contains(BusinessViolationCodes.RateCardCannotBeBackdated, await backdated.Content.ReadAsStringAsync());

        var duplicate = await branch.Admin.PostAsJsonAsync("/api/staff/boarding/rate-cards", new
        {
            unitType = "Deluxe", nightlyRate = "10.00", weeklyRate = "10.00", monthlyRate = "10.00", taxClassId = branch.TaxClassId, effectiveFrom = Now
        }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains(BusinessViolationCodes.DuplicateRateCardEffectiveDate, await duplicate.Content.ReadAsStringAsync());

        await SetRateCardAsync(branch, "Deluxe", nightly: "1800.00", effectiveFrom: Now.AddDays(10));
        var cards = await ReadAsync(await branch.Admin.GetAsync("/api/staff/boarding/rate-cards?unitType=Deluxe"));
        Assert.Equal(["Scheduled", "Current"], cards.EnumerateArray().Select(c => c.GetProperty("status").GetString()!).ToArray());

        Factory.Clock.Advance(TimeSpan.FromDays(11));
        await ReauthenticateAsync(branch.Admin, branch.AdminLogin); // the 30-minute idle timeout runs on the fake clock
        var later = await ReadAsync(await branch.Admin.GetAsync("/api/staff/boarding/rate-cards?unitType=Deluxe"));
        Assert.Equal(["Current", "Past"], later.EnumerateArray().Select(c => c.GetProperty("status").GetString()!).ToArray());
    }

    [Fact]
    public async Task ARateCardWithMoreThanTwoDecimals_Or_AnUnknownTaxClass_IsRefused()
    {
        var branch = await SeedBranchAsync();

        var decimals = await branch.Admin.PostAsJsonAsync("/api/staff/boarding/rate-cards", new
        { unitType = "Standard", nightlyRate = "10.005", weeklyRate = "10.00", monthlyRate = "10.00", taxClassId = branch.TaxClassId, effectiveFrom = Now }, Json);
        var taxClass = await branch.Admin.PostAsJsonAsync("/api/staff/boarding/rate-cards", new
        { unitType = "Standard", nightlyRate = "10.00", weeklyRate = "10.00", monthlyRate = "10.00", taxClassId = Guid.NewGuid(), effectiveFrom = Now }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, decimals.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, taxClass.StatusCode);
        Assert.Contains(BusinessViolationCodes.TaxClassNotFound, await taxClass.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TwoOverlappingMaintenanceBlocks_AreRefused_ButBackToBackBlocksAreFine()
    {
        var branch = await SeedBranchAsync();
        var unit = await CreateUnitAsync(branch);
        var url = $"/api/staff/boarding/units/{unit}/maintenance";

        var first = await branch.Admin.PostAsJsonAsync(url, new { startsOn = "2031-05-01", endsOn = "2031-05-03", reason = "Deep clean" }, Json);
        var overlap = await branch.Admin.PostAsJsonAsync(url, new { startsOn = "2031-05-03", endsOn = "2031-05-05", reason = "Again" }, Json);
        var backToBack = await branch.Admin.PostAsJsonAsync(url, new { startsOn = "2031-05-04", endsOn = "2031-05-05", reason = "Next" }, Json);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, overlap.StatusCode);
        Assert.Contains(BusinessViolationCodes.MaintenanceOverlapsMaintenance, await overlap.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Created, backToBack.StatusCode);
    }

    [Fact]
    public async Task ACancelledMaintenanceBlock_NoLongerBlocksANewOne()
    {
        var branch = await SeedBranchAsync();
        var unit = await CreateUnitAsync(branch);
        var url = $"/api/staff/boarding/units/{unit}/maintenance";
        var created = await ReadAsync(await branch.Admin.PostAsJsonAsync(url, new { startsOn = "2031-06-01", endsOn = "2031-06-03", reason = "Repaint" }, Json));

        var cancel = await branch.Admin.DeleteAsync($"{url}/{created.GetProperty("blockId").GetGuid()}");
        var again = await branch.Admin.PostAsJsonAsync(url, new { startsOn = "2031-06-01", endsOn = "2031-06-03", reason = "Repaint again" }, Json);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task AUnitOfAnotherBranch_IsNotFound()
    {
        var mine = await SeedBranchAsync();
        var theirs = await SeedBranchAsync();
        var theirUnit = await CreateUnitAsync(theirs);

        var update = await mine.Admin.PutAsJsonAsync($"/api/staff/boarding/units/{theirUnit}", new { name = "x", unitType = "Standard", capacityCats = 1 }, Json);

        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task TheCareRoutine_IsTheDefaultUntilSet_ThenTheBranchsOwn()
    {
        var branch = await SeedBranchAsync();

        var defaults = await ReadAsync(await branch.Admin.GetAsync("/api/staff/boarding/care-routine"));
        Assert.True(defaults.GetProperty("isDefault").GetBoolean());
        Assert.Equal(5, defaults.GetProperty("times").GetArrayLength());

        var set = await branch.Admin.PutAsJsonAsync("/api/staff/boarding/care-routine", new
        { times = new[] { new { kind = "LitterChange", time = "08:30" }, new { kind = "Enrichment", time = "16:00" } } }, Json);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);

        var own = await ReadAsync(await branch.Admin.GetAsync("/api/staff/boarding/care-routine"));
        Assert.False(own.GetProperty("isDefault").GetBoolean());
        Assert.Equal(["08:30", "16:00"], own.GetProperty("times").EnumerateArray().Select(t => t.GetProperty("time").GetString()!).ToArray());

        var duplicate = await branch.Admin.PutAsJsonAsync("/api/staff/boarding/care-routine", new
        { times = new[] { new { kind = "LitterChange", time = "08:30" }, new { kind = "LitterChange", time = "08:30" } } }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
    }

    [Fact]
    public async Task ThePublicUnitTypes_ListOnlyPricedTypesWithAnActiveUnit_AndNeverIsolation()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await CreateUnitAsync(branch, "Deluxe", 2);
        await CreateUnitAsync(branch, "Isolation", 1);
        await SetRateCardAsync(branch, "Standard", nightly: "900.00");
        await SetRateCardAsync(branch, "Isolation", nightly: "1200.00");

        var types = await ReadAsync(await CreateClient().GetAsync($"/api/boarding/unit-types?branchId={branch.BranchId}"));

        var names = types.EnumerateArray().Select(t => t.GetProperty("unitType").GetString()!).ToArray();
        Assert.Equal(["Standard"], names);
        Assert.Equal("900.00", types[0].GetProperty("nightlyRate").GetProperty("amount").GetString());
    }
}
