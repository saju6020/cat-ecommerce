using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BRD-001 Phase C -- availability, the vaccination gate, care profiles, capacity and the reserve race (I-B2, I-B3, I-C1 .. I-C13).</summary>
// One collection: the Boarding worker pass reads every stay in the shared database, so classes with different fake clocks must not overlap.
[Collection("Boarding")]
public sealed class BoardingBookingEndpointTests(BoardingApiFactory factory) : BoardingIntegrationTestBase(factory)
{
    private static string D(DateOnly date) => date.ToString("yyyy-MM-dd");

    private async Task<JsonElement> AvailabilityAsync(HttpClient client, Guid branchId, DateOnly arrival, DateOnly departure, int cats = 1)
    {
        var response = await client.GetAsync($"/api/boarding/availability?branchId={branchId}&arrivalOn={D(arrival)}&departureOn={D(departure)}&catCount={cats}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private static JsonElement TypeRow(JsonElement availability, string type) =>
        availability.GetProperty("unitTypes").EnumerateArray().Single(t => t.GetProperty("unitType").GetString() == type);

    [Fact]
    public async Task I_C1_Availability_ListsTypesWithCapacityForTheCats_PricesAndTotals()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await CreateUnitAsync(branch, "Deluxe", 2);
        await CreateUnitAsync(branch, "Isolation", 1);
        await SetRateCardAsync(branch, "Standard", nightly: "1000.00");
        await SetRateCardAsync(branch, "Deluxe", nightly: "1500.00");
        var arrival = LocalToday.AddDays(10);

        var one = await AvailabilityAsync(CreateClient(), branch.BranchId, arrival, arrival.AddDays(5), cats: 1);
        var two = await AvailabilityAsync(CreateClient(), branch.BranchId, arrival, arrival.AddDays(5), cats: 2);

        Assert.Equal(["Standard", "Deluxe"], one.GetProperty("unitTypes").EnumerateArray().Select(t => t.GetProperty("unitType").GetString()!).ToArray());
        Assert.Equal("5000.00", TypeRow(one, "Standard").GetProperty("quote").GetProperty("total").GetProperty("amount").GetString());
        Assert.Equal(["Deluxe"], two.GetProperty("unitTypes").EnumerateArray().Select(t => t.GetProperty("unitType").GetString()!).ToArray());
        Assert.Equal(5, one.GetProperty("nights").GetInt32());
    }

    [Fact]
    public async Task I_C4_ASixteenNightQuote_IsWeekly_WithTheDiscountLinesSeparate()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard", nightly: "1000.00", weekly: "6000.00");
        var set = await branch.Admin.PutAsJsonAsync($"/api/admin/settings?branchId={branch.BranchId}", new { changes = new[] { new { key = "boarding.longStayDiscountPercent", value = "10.00" } } }, Json);
        Assert.Equal(HttpStatusCode.NoContent, set.StatusCode);
        var arrival = LocalToday.AddDays(20);

        var availability = await AvailabilityAsync(CreateClient(), branch.BranchId, arrival, arrival.AddDays(16));
        var quote = TypeRow(availability, "Standard").GetProperty("quote");

        Assert.Equal("Weekly", quote.GetProperty("basis").GetString());
        Assert.Equal(3, quote.GetProperty("units").GetArrayLength());
        Assert.Equal(3, quote.GetProperty("discountLines").GetArrayLength());
        Assert.Equal("12600.00", quote.GetProperty("total").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task I_C2_AReservedUnitDisappearsForItsDates_ButABackToBackRangeIsOffered()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var arrival = LocalToday.AddDays(12);
        await ReserveOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(4), "Standard", cat);

        var overlapping = await AvailabilityAsync(CreateClient(), branch.BranchId, arrival.AddDays(1), arrival.AddDays(3));
        var backToBack = await AvailabilityAsync(CreateClient(), branch.BranchId, arrival.AddDays(4), arrival.AddDays(6));

        Assert.Equal(0, TypeRow(overlapping, "Standard").GetProperty("freeUnitCount").GetInt32());
        Assert.Equal(1, TypeRow(backToBack, "Standard").GetProperty("freeUnitCount").GetInt32());
    }

    [Fact]
    public async Task I_C3_WhenEverythingIsTaken_AlternativesAndTheWaitlistAreOffered()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var arrival = LocalToday.AddDays(12);
        await ReserveOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);

        var full = await AvailabilityAsync(CreateClient(), branch.BranchId, arrival, arrival.AddDays(3));

        Assert.True(full.GetProperty("waitlistAvailable").GetBoolean());
        var alternatives = full.GetProperty("alternatives");
        Assert.True(alternatives.GetArrayLength() is >= 1 and <= 3);
        Assert.Equal("Standard", alternatives[0].GetProperty("unitType").GetString());
    }

    [Fact]
    public async Task I_B2_AMaintenanceBlock_RemovesTheUnitForThoseDatesOnly()
    {
        var branch = await SeedBranchAsync();
        var unit = await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var arrival = LocalToday.AddDays(15);
        var block = await branch.Admin.PostAsJsonAsync($"/api/staff/boarding/units/{unit}/maintenance", new { startsOn = D(arrival.AddDays(1)), endsOn = D(arrival.AddDays(2)), reason = "Repaint" }, Json);
        Assert.Equal(HttpStatusCode.Created, block.StatusCode);

        var blocked = await AvailabilityAsync(CreateClient(), branch.BranchId, arrival, arrival.AddDays(3));
        var clear = await AvailabilityAsync(CreateClient(), branch.BranchId, arrival.AddDays(5), arrival.AddDays(7));

        Assert.Equal(0, TypeRow(blocked, "Standard").GetProperty("freeUnitCount").GetInt32());
        Assert.Equal(1, TypeRow(clear, "Standard").GetProperty("freeUnitCount").GetInt32());
    }

    [Fact]
    public async Task I_B3_MaintenanceOverAReservation_IsRefusedWithTheStaysArrivalDates()
    {
        var branch = await SeedBranchAsync();
        var unit = await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var arrival = LocalToday.AddDays(18);
        await ReserveOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(4), "Standard", cat);

        var block = await branch.Admin.PostAsJsonAsync($"/api/staff/boarding/units/{unit}/maintenance", new { startsOn = D(arrival.AddDays(1)), endsOn = D(arrival.AddDays(2)), reason = "Repaint" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, block.StatusCode);
        var body = await block.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.MaintenanceOverlapsStays, body);
        Assert.Contains(D(arrival), body);
    }

    [Fact]
    public async Task I_C5_AnOverdueCat_IsBlocked_WithABookingLink_AndReserveIsRefused()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedCatAsync(customerId, "Unvaccinated"); // never vaccinated: every core vaccine is overdue
        await CoreVaccinesAsync();
        var arrival = LocalToday.AddDays(10);

        var enquiry = await EnquireAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
        Assert.Equal(HttpStatusCode.Created, enquiry.StatusCode);
        var gate = (await ReadAsync(enquiry)).GetProperty("vaccinationGate");
        Assert.False(gate.GetProperty("passed").GetBoolean());
        var blocked = gate.GetProperty("blockedCats")[0];
        Assert.Equal(cat, blocked.GetProperty("catId").GetGuid());
        Assert.Contains("catid=" + cat.ToString().ToLowerInvariant(), blocked.GetProperty("bookingLink").GetString()!.ToLowerInvariant());

        var stayId = (await ReadAsync(enquiry)).GetProperty("stayId").GetGuid();
        await SetProfileAsync(customer, stayId, cat);
        var reserve = await ReserveAsync(customer, stayId);

        Assert.Equal(HttpStatusCode.BadRequest, reserve.StatusCode);
        Assert.Contains(BusinessViolationCodes.VaccinationGateBlocked, await reserve.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task I_C6_AfterTheVaccinationIsRecorded_TheSameBookingProceedsWithNothingReEntered()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedCatAsync(customerId);
        await CoreVaccinesAsync();
        var arrival = LocalToday.AddDays(10);
        var stayId = await EnquireOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
        await SetProfileAsync(customer, stayId, cat);
        Assert.Equal(HttpStatusCode.BadRequest, (await ReserveAsync(customer, stayId)).StatusCode);

        await VaccinateAsync(cat);
        var reserve = await ReserveAsync(customer, stayId);

        Assert.Equal(HttpStatusCode.OK, reserve.StatusCode);
        var stay = (await ReadAsync(reserve)).GetProperty("stay");
        Assert.Equal("Reserved", stay.GetProperty("status").GetString());
        Assert.NotNull(stay.GetProperty("cats")[0].GetProperty("careProfile").GetProperty("notes").GetString());
    }

    [Fact]
    public async Task I_C7_OnlyAnAdministratorMayOverrideTheGate_WithAReason_AndItIsAudited()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedCatAsync(customerId);
        await CoreVaccinesAsync();
        var arrival = LocalToday.AddDays(10);
        var stayId = await EnquireOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
        await SetProfileAsync(customer, stayId, cat);

        var reception = await SeedStaffAsync(StaffRoles.Reception);
        var assign = await branch.Admin.PutAsJsonAsync($"/api/admin/staff/{reception.StaffAccountId}/branches", new { branchIds = new[] { branch.BranchId }, primaryBranchId = branch.BranchId }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);
        var receptionClient = await SignInStaffAsync(reception, branch.BranchId);
        var url = $"/api/staff/boarding/stays/{stayId}/cats/{cat}/vaccination-override";

        var denied = await receptionClient.PostAsJsonAsync(url, new { reason = "Owner promised" }, Json);
        var noReason = await branch.Admin.PostAsJsonAsync(url, new { reason = "" }, Json);
        var granted = await branch.Admin.PostAsJsonAsync(url, new { reason = "Vet confirmed the booster by phone" }, Json);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ReserveAsync(customer, stayId)).StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var audited = await db.Set<CatEcommerce.Domain.Aggregates.Audit.AuditEntryAggregate>().AsNoTracking()
            .AnyAsync(a => a.EntityId == stayId && a.Action == "BoardingVaccinationOverridden" && a.Reason == "Vet confirmed the booster by phone");
        Assert.True(audited);
    }

    [Fact]
    public async Task I_C8_ReserveWithOneProfileMissing_NamesTheCat()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Family", 3);
        await SetRateCardAsync(branch, "Family");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var mitu = await SeedVaccinatedCatAsync(customerId, "Mitu");
        var lulu = await SeedVaccinatedCatAsync(customerId, "Lulu");
        var arrival = LocalToday.AddDays(10);
        var stayId = await EnquireOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Family", mitu, lulu);
        await SetProfileAsync(customer, stayId, mitu);

        var reserve = await ReserveAsync(customer, stayId);

        Assert.Equal(HttpStatusCode.BadRequest, reserve.StatusCode);
        var body = await reserve.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.CareProfileIncomplete, body);
        Assert.Contains("Lulu", body);
        Assert.DoesNotContain("Mitu", body);
    }

    [Fact]
    public async Task I_C9_ThreeCatsOfOneHousehold_ShareOneStayAndOneUnit_WithThreeProfiles()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Family", 3);
        await SetRateCardAsync(branch, "Family", additionalCat: "200.00");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cats = new[] { await SeedVaccinatedCatAsync(customerId, "A"), await SeedVaccinatedCatAsync(customerId, "B"), await SeedVaccinatedCatAsync(customerId, "C") };
        var arrival = LocalToday.AddDays(10);

        var stayId = await ReserveOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Family", cats);

        var stay = await ReadAsync(await customer.GetAsync($"/api/customers/me/boarding/stays/{stayId}"));
        Assert.Equal(3, stay.GetProperty("cats").GetArrayLength());
        Assert.All(stay.GetProperty("cats").EnumerateArray(), c => Assert.Equal(JsonValueKind.Object, c.GetProperty("careProfile").ValueKind));
        Assert.Equal(JsonValueKind.Object, stay.GetProperty("unit").ValueKind);
        // 3 nights x 1000 + 2 extra cats x 200 x 3 nights
        Assert.Equal("4200.00", stay.GetProperty("quote").GetProperty("total").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task I_C10_FourCatsIntoACapacityOfThree_IsRefusedStatingTheCapacity()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Family", 3);
        await SetRateCardAsync(branch, "Family");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cats = new[] { await SeedVaccinatedCatAsync(customerId), await SeedVaccinatedCatAsync(customerId), await SeedVaccinatedCatAsync(customerId), await SeedVaccinatedCatAsync(customerId) };
        var arrival = LocalToday.AddDays(10);
        var stayId = await EnquireOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Family", cats);
        foreach (var cat in cats) await SetProfileAsync(customer, stayId, cat);

        var reserve = await ReserveAsync(customer, stayId);

        Assert.Equal(HttpStatusCode.BadRequest, reserve.StatusCode);
        var body = await reserve.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.UnitCapacityExceeded, body);
        Assert.Contains("holds up to 3 cats", body);
    }

    [Fact]
    public async Task I_C11_AnotherCustomersCat_Or_Stay_IsNotFound()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var (owner, ownerId, _) = await RegisterAndSignInCustomerAsync();
        var (stranger, _, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(ownerId);
        var arrival = LocalToday.AddDays(10);

        var steal = await EnquireAsync(stranger, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
        var stayId = await EnquireOkAsync(owner, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
        var peek = await stranger.GetAsync($"/api/customers/me/boarding/stays/{stayId}");
        var reserve = await ReserveAsync(stranger, stayId);

        Assert.Equal(HttpStatusCode.NotFound, steal.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, peek.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reserve.StatusCode);
    }

    [Fact]
    public async Task I_C12_TwentyParallelReserves_ForTheLastUnit_HaveExactlyOneWinner()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var arrival = LocalToday.AddDays(25);

        var stays = new List<(HttpClient Client, Guid StayId)>();
        for (var i = 0; i < 20; i++)
        {
            var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
            var cat = await SeedVaccinatedCatAsync(customerId);
            var stayId = await EnquireOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
            await SetProfileAsync(customer, stayId, cat);
            stays.Add((customer, stayId));
        }

        var results = await Task.WhenAll(stays.Select(s => ReserveAsync(s.Client, s.StayId)));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(19, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var loser in results.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Contains(BusinessViolationCodes.UnitNoLongerAvailable, await loser.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task I_C13_OneCatInTwoOverlappingStaysAtTwoBranches_IsRefusedWithTheOtherStaysDates()
    {
        var first = await SeedBranchAsync();
        var second = await SeedBranchAsync();
        foreach (var branch in new[] { first, second })
        {
            await CreateUnitAsync(branch, "Standard", 1);
            await SetRateCardAsync(branch, "Standard");
        }

        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var arrival = LocalToday.AddDays(10);
        await ReserveOkAsync(customer, first.BranchId, arrival, arrival.AddDays(5), "Standard", cat);

        var stayId = await EnquireOkAsync(customer, second.BranchId, arrival.AddDays(2), arrival.AddDays(6), "Standard", cat);
        await SetProfileAsync(customer, stayId, cat);
        var reserve = await ReserveAsync(customer, stayId);

        Assert.Equal(HttpStatusCode.Conflict, reserve.StatusCode);
        var body = await reserve.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.CatAlreadyBoarding, body);
        Assert.Contains(D(arrival), body);
    }

    [Fact]
    public async Task AnEnquiryThatIsNotYetReserved_HoldsNoUnit()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var arrival = LocalToday.AddDays(10);

        await EnquireOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
        var availability = await AvailabilityAsync(CreateClient(), branch.BranchId, arrival, arrival.AddDays(3));

        Assert.Equal(1, TypeRow(availability, "Standard").GetProperty("freeUnitCount").GetInt32());
    }

    [Fact]
    public async Task OnlineBookingBeyondTheHorizon_Or_InThePast_IsRefused()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard");
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);

        var past = await EnquireAsync(customer, branch.BranchId, LocalToday.AddDays(-2), LocalToday.AddDays(2), "Standard", cat);
        var far = await EnquireAsync(customer, branch.BranchId, LocalToday.AddDays(400), LocalToday.AddDays(403), "Standard", cat);

        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Contains(BusinessViolationCodes.InvalidStayDates, await past.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, far.StatusCode);
        Assert.Contains(BusinessViolationCodes.StayBeyondHorizon, await far.Content.ReadAsStringAsync());
    }
}
