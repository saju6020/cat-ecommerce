using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BRD-001 Phase H -- the waitlist, daycare and the daily update (I-H1 .. I-H10).</summary>
[Collection("Boarding")]
public sealed class BoardingWaitlistDaycareEndpointTests(BoardingApiFactory factory) : BoardingIntegrationTestBase(factory)
{
    private async Task SetSettingAsync(BoardingBranch branch, string key, string value)
    {
        var response = await branch.Admin.PutAsJsonAsync($"/api/admin/settings?branchId={branch.BranchId}", new { changes = new[] { new { key, value } } }, Json);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task<BoardingBranch> DaycareBranchAsync(string capacity = "2")
    {
        var branch = await SeedBranchAsync();
        await SetSettingAsync(branch, "boarding.daycareDailyCapacity", capacity);
        var card = await branch.Admin.PostAsJsonAsync("/api/staff/boarding/daycare-rate-cards",
            new { hourlyRate = "200.00", dayRate = "1000.00", taxClassId = branch.TaxClassId, effectiveFrom = Now }, Json);
        Assert.True(card.StatusCode == HttpStatusCode.Created, await card.Content.ReadAsStringAsync());
        return branch;
    }

    private async Task<(Guid Id, string Body)> StaffBookAsync(BoardingBranch branch, Guid customerId, Guid catId, DateOnly date, string basis = "Day", int? hours = null)
    {
        var response = await branch.Admin.PostAsJsonAsync("/api/staff/daycare", new { customerId, catId, localDate = date, basis, expectedHours = hours }, Json);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode == HttpStatusCode.Created ? JsonSerializer.Deserialize<JsonElement>(text, Json).GetProperty("visitId").GetGuid() : Guid.Empty, response.StatusCode + " " + text);
    }

    [Fact]
    public async Task I_H1_AWaitlistIsRefusedWhileAUnitIsFree_AndAcceptedOnceTheRangeIsFull_OncePerRange()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch);
        await SetRateCardAsync(branch);
        var (first, firstId, _) = await RegisterAndSignInCustomerAsync();
        var (second, _, _) = await RegisterAndSignInCustomerAsync();
        var arrival = LocalToday.AddDays(10);
        var departure = arrival.AddDays(3);
        var body = new { branchId = branch.BranchId, arrivalOn = arrival, departureOn = departure, catCount = 1 };

        var early = await second.PostAsJsonAsync("/api/customers/me/boarding/waitlist", body, Json);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Contains(BusinessViolationCodes.UnitsStillAvailable, await early.Content.ReadAsStringAsync());

        await ReserveOkAsync(first, branch.BranchId, arrival, departure, "Standard", await SeedVaccinatedCatAsync(firstId));

        var joined = await second.PostAsJsonAsync("/api/customers/me/boarding/waitlist", body, Json);
        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);
        var again = await second.PostAsJsonAsync("/api/customers/me/boarding/waitlist", body, Json);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains(BusinessViolationCodes.AlreadyWaitlisted, await again.Content.ReadAsStringAsync());
        Assert.Single((await ReadAsync(await second.GetAsync("/api/customers/me/boarding/waitlist"))).EnumerateArray());
    }

    [Fact]
    public async Task I_H2_TheOldestWaitingEntryIsNotifiedOnce_WhenAUnitFrees_WithNoHold()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch);
        await SetRateCardAsync(branch);
        var (holder, holderId, _) = await RegisterAndSignInCustomerAsync();
        var (older, olderId, _) = await RegisterAndSignInCustomerAsync();
        var (newer, newerId, _) = await RegisterAndSignInCustomerAsync();
        var arrival = LocalToday.AddDays(12);
        var departure = arrival.AddDays(3);
        var stayId = await ReserveOkAsync(holder, branch.BranchId, arrival, departure, "Standard", await SeedVaccinatedCatAsync(holderId));
        var body = new { branchId = branch.BranchId, arrivalOn = arrival, departureOn = departure, catCount = 1 };
        Assert.Equal(HttpStatusCode.Created, (await older.PostAsJsonAsync("/api/customers/me/boarding/waitlist", body, Json)).StatusCode);
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(HttpStatusCode.Created, (await newer.PostAsJsonAsync("/api/customers/me/boarding/waitlist", body, Json)).StatusCode);

        await RunBoardingWorkCycleAsync();
        Assert.DoesNotContain("boarding.waitlist-available", await NotificationKeysAsync(olderId));

        Assert.Equal(HttpStatusCode.OK, (await holder.PostAsJsonAsync($"/api/customers/me/boarding/stays/{stayId}/cancel", new { }, Json)).StatusCode);
        await RunBoardingWorkCycleAsync();
        await RunBoardingWorkCycleAsync();

        Assert.Single((await NotificationKeysAsync(olderId)).Where(k => k == "boarding.waitlist-available"));
        Assert.DoesNotContain("boarding.waitlist-available", await NotificationKeysAsync(newerId)); // one free unit, one notice
        Assert.Equal("Notified", (await ReadAsync(await older.GetAsync("/api/customers/me/boarding/waitlist")))[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task I_H3_AWithdrawnEntry_IsOffTheList_AndAStaleOneIsClosed()
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch);
        await SetRateCardAsync(branch);
        var (holder, holderId, _) = await RegisterAndSignInCustomerAsync();
        var (waiter, _, mobile) = await RegisterAndSignInCustomerAsync();
        var arrival = LocalToday.AddDays(3);
        var departure = arrival.AddDays(2);
        await ReserveOkAsync(holder, branch.BranchId, arrival, departure, "Standard", await SeedVaccinatedCatAsync(holderId));
        var body = new { branchId = branch.BranchId, arrivalOn = arrival, departureOn = departure, catCount = 1 };
        var entryId = (await ReadAsync(await waiter.PostAsJsonAsync("/api/customers/me/boarding/waitlist", body, Json))).GetProperty("entryId").GetGuid();

        Factory.Clock.Advance(TimeSpan.FromDays(5));
        await ReauthenticateCustomerAsync(waiter, mobile);
        await RunBoardingWorkCycleAsync();
        Assert.Equal("Closed", (await ReadAsync(await waiter.GetAsync("/api/customers/me/boarding/waitlist")))[0].GetProperty("status").GetString());

        var (stranger, _, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/customers/me/boarding/waitlist/{entryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await waiter.DeleteAsync($"/api/customers/me/boarding/waitlist/{entryId}")).StatusCode);
    }

    [Fact]
    public async Task I_H4_DaycareIsNotOffered_WhenTheCapacityIsZero()
    {
        var branch = await SeedBranchAsync();
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);

        var (_, result) = await StaffBookAsync(branch, customerId, cat, LocalToday.AddDays(1));

        Assert.Contains(BusinessViolationCodes.DaycareNotOffered, result);
    }

    [Fact]
    public async Task I_H5_DaycareFillsItsSlots_RefusesTheNextVisit_AndACancellationFreesASlot()
    {
        var branch = await DaycareBranchAsync("2");
        var date = LocalToday.AddDays(2);
        var visits = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
            var (id, text) = await StaffBookAsync(branch, customerId, await SeedVaccinatedCatAsync(customerId), date);
            Assert.True(id != Guid.Empty, text);
            visits.Add(id);
        }

        var (_, thirdOwner, _) = await RegisterAndSignInCustomerAsync();
        var thirdCat = await SeedVaccinatedCatAsync(thirdOwner);
        var (full, fullText) = await StaffBookAsync(branch, thirdOwner, thirdCat, date);
        Assert.Equal(Guid.Empty, full);
        Assert.Contains(BusinessViolationCodes.DaycareFull, fullText);
        var day = await ReadAsync(await branch.Admin.GetAsync($"/api/staff/daycare?date={date:yyyy-MM-dd}"));
        Assert.Equal(2, day.GetProperty("slotsTaken").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await branch.Admin.PostAsync($"/api/staff/daycare/{visits[0]}/cancel", null)).StatusCode);
        var (retry, retryText) = await StaffBookAsync(branch, thirdOwner, thirdCat, date);
        Assert.True(retry != Guid.Empty, retryText);
    }

    [Fact]
    public async Task I_H6_ACatHasOneDaycareVisitPerDay_AndTheGateApplies()
    {
        var branch = await DaycareBranchAsync("3");
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var unvaccinated = await SeedCatAsync(customerId, "Bare");
        var date = LocalToday.AddDays(2);

        Assert.True((await StaffBookAsync(branch, customerId, cat, date)).Id != Guid.Empty);
        Assert.Contains(BusinessViolationCodes.CatAlreadyBoarding, (await StaffBookAsync(branch, customerId, cat, date)).Body);
        Assert.Contains(BusinessViolationCodes.VaccinationGateBlocked, (await StaffBookAsync(branch, customerId, unvaccinated, date.AddDays(1))).Body);
    }

    [Fact]
    public async Task I_H7_ADaycareVisitIsCheckedInOnItsDay_AndCheckedOutWithAHourlyChargeCappedAtTheDayRate()
    {
        var branch = await DaycareBranchAsync("2");
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var (visitId, text) = await StaffBookAsync(branch, customerId, cat, LocalToday, "Hourly", 3);
        Assert.True(visitId != Guid.Empty, text);

        Assert.Equal(HttpStatusCode.NoContent, (await branch.Admin.PostAsync($"/api/staff/daycare/{visitId}/check-in", null)).StatusCode);
        Factory.Clock.Advance(TimeSpan.FromMinutes(150)); // 2.5 h -> 3 h x 200
        await ReauthenticateAsync(branch.Admin, branch.AdminLogin);

        var checkOut = await branch.Admin.PostAsync($"/api/staff/daycare/{visitId}/check-out", null);

        Assert.Equal(HttpStatusCode.OK, checkOut.StatusCode);
        Assert.Equal(600m, (await ReadAsync(checkOut)).GetProperty("charge").GetDecimal());
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var charge = await db.Charges.AsNoTracking().SingleAsync(c => c.SourceKind == ChargeSourceKind.DaycareVisit && c.SourceId == visitId);
        Assert.Equal(600m, charge.UnitPriceAmount);
    }

    [Fact]
    public async Task I_H8_ANoShowNeedsTheDayToHavePassed_AndABookedVisitCannotBeCheckedInOnAnotherDay()
    {
        var branch = await DaycareBranchAsync("2");
        var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
        var (visitId, text) = await StaffBookAsync(branch, customerId, await SeedVaccinatedCatAsync(customerId), LocalToday.AddDays(1));
        Assert.True(visitId != Guid.Empty, text);

        Assert.Equal(HttpStatusCode.BadRequest, (await branch.Admin.PostAsync($"/api/staff/daycare/{visitId}/check-in", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await branch.Admin.PostAsync($"/api/staff/daycare/{visitId}/no-show", null)).StatusCode);

        Factory.Clock.Advance(TimeSpan.FromDays(2));
        await ReauthenticateAsync(branch.Admin, branch.AdminLogin);
        Assert.Equal(HttpStatusCode.NoContent, (await branch.Admin.PostAsync($"/api/staff/daycare/{visitId}/no-show", null)).StatusCode);
    }

    [Fact]
    public async Task I_H9_TheOwnersDaycareIsTheirOwn()
    {
        var branch = await DaycareBranchAsync("2");
        var (owner, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);

        var booked = await owner.PostAsJsonAsync("/api/customers/me/daycare", new { branchId = branch.BranchId, catId = cat, localDate = LocalToday.AddDays(2), basis = "Day" }, Json);
        Assert.True(booked.StatusCode == HttpStatusCode.Created, await booked.Content.ReadAsStringAsync());
        var visitId = (await ReadAsync(booked)).GetProperty("visitId").GetGuid();

        Assert.Single((await ReadAsync(await owner.GetAsync("/api/customers/me/daycare"))).EnumerateArray());
        var (stranger, _, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/customers/me/daycare/{visitId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsync($"/api/customers/me/daycare/{visitId}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task I_H10_TheDailyUpdate_IsSentOncePerStayPerDay_AtTheConfiguredHour_WhileTheStayIsInHouse()
    {
        var stay = await CheckedInStayAsync(nights: 4);
        // 14:30 on the arrival day: before the default 18:00 hour.
        await RunBoardingWorkCycleAsync();
        Assert.DoesNotContain("boarding.daily-update", await NotificationKeysAsync(stay.CustomerId));

        Factory.Clock.SetUtcNow(new DateTimeOffset(stay.Arrival.Year, stay.Arrival.Month, stay.Arrival.Day, 18, 30, 0, TimeSpan.FromHours(6)).ToUniversalTime());
        await RunBoardingWorkCycleAsync();
        await RunBoardingWorkCycleAsync();

        Assert.Single((await NotificationKeysAsync(stay.CustomerId)).Where(k => k == "boarding.daily-update"));
    }
}
