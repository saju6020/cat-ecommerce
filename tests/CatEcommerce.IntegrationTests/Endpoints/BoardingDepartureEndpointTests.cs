using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Events.Boarding;
using CatEcommerce.Domain.Events.Surgery;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Events;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BRD-001 Phase G -- charges, extension, relocation, referral and vet approvals, check-out and the stay summary, cycle invoices (I-G1 .. I-G12).</summary>
// One collection: the Boarding worker pass reads every stay in the shared database, so classes with different fake clocks must not overlap.
[Collection("Boarding")]
public sealed class BoardingDepartureEndpointTests(BoardingApiFactory factory) : BoardingIntegrationTestBase(factory)
{
    private static object CheckOutBody(Guid[] catIds, decimal weight = 4.1m, bool complete = true) => new
    {
        cats = catIds.Select(id => new { catId = id, weightKg = complete ? weight : (decimal?)null, belongings = Array.Empty<object>(), signature = complete ? TypedSignature() : null }).ToArray()
    };

    private async Task<List<(string Ref, bool Voided, decimal Net)>> ChargesAsync(Guid stayId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var rows = await db.Charges.AsNoTracking().Where(c => c.SourceKind == ChargeSourceKind.BoardingStay && c.SourceId == stayId).ToListAsync();
        return rows.Select(c => (c.SourceLineRef, c.Status == ChargeStatus.Voided, c.Quantity * c.UnitPriceAmount - c.LineDiscountAmount)).OrderBy(c => c.SourceLineRef).ToList();
    }

    private async Task GoToAsync(InHouse stay, DateOnly day, int hour = 10)
    {
        Factory.Clock.SetUtcNow(new DateTimeOffset(day.Year, day.Month, day.Day, hour, 0, 0, TimeSpan.FromHours(6)).ToUniversalTime());
        await ReauthenticateAsync(stay.Branch.Admin, stay.Branch.AdminLogin);
        await ReauthenticateCustomerAsync(stay.Customer, stay.CustomerMobile);
    }

    private async Task<JsonElement> DetailAsync(InHouse stay) => await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{stay.StayId}"));

    [Fact]
    public async Task I_G1_CheckIn_PushesTheStaysChargesToBilling_OneChargePerRateUnit_Once()
    {
        var stay = await CheckedInStayAsync(nights: 5);

        var charges = await ChargesAsync(stay.StayId);

        Assert.Single(charges);
        Assert.StartsWith("r1.", charges[0].Ref);
        Assert.Equal(5000m, charges[0].Net);
        Assert.Equal(1, (await DetailAsync(stay)).GetProperty("charges").GetArrayLength());
    }

    [Fact]
    public async Task I_G2_AnEarlyCheckOut_ChargesTheNightsUsed_VoidsTheRest_AndFreesTheUnitForTheTurnaround()
    {
        var stay = await CheckedInStayAsync(nights: 10);
        await GoToAsync(stay, stay.Arrival.AddDays(3), 10);

        var response = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", CheckOutBody(stay.CatIds), Json);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var view = (await ReadAsync(response)).GetProperty("stay");
        Assert.Equal("CheckedOut", view.GetProperty("status").GetString());
        Assert.Equal(3, view.GetProperty("nights").GetInt32());
        var charges = await ChargesAsync(stay.StayId);
        Assert.Contains(charges, c => c.Voided && c.Ref.StartsWith("r1."));
        Assert.Equal(3000m, charges.Where(c => !c.Voided).Sum(c => c.Net));
        Assert.Contains("boarding.checked-out", await NotificationKeysAsync(stay.CustomerId));
    }

    [Fact]
    public async Task I_G2b_TheEarlyCheckOutPreview_ChangesNothing_AndEqualsWhatTheCheckOutThenDoes()
    {
        var stay = await CheckedInStayAsync(nights: 10);
        await GoToAsync(stay, stay.Arrival.AddDays(3), 10);
        var chargesBefore = await ChargesAsync(stay.StayId);

        var preview = await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out-preview"));

        Assert.True(preview.GetProperty("isEarly").GetBoolean());
        Assert.Equal(3, preview.GetProperty("nightsCharged").GetInt32());
        Assert.Equal(chargesBefore, await ChargesAsync(stay.StayId));
        Assert.Equal("CheckedIn", (await DetailAsync(stay)).GetProperty("status").GetString());

        var result = (await ReadAsync(await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", CheckOutBody(stay.CatIds), Json))).GetProperty("stay");
        var after = await ChargesAsync(stay.StayId);
        decimal Money(JsonElement m) => decimal.Parse(m.GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(result.GetProperty("nights").GetInt32(), preview.GetProperty("nightsCharged").GetInt32());
        Assert.Equal(Money(result.GetProperty("quote").GetProperty("total")), Money(preview.GetProperty("newTotal")));
        Assert.Equal(after.Where(c => !c.Voided).Sum(c => c.Net), Money(preview.GetProperty("newTotal")));
        var lines = preview.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(after.Count(c => c.Voided), lines.Count(l => l.GetProperty("action").GetString() == "Void"));
        Assert.Equal(after.Where(c => !c.Voided && c.Ref.StartsWith("r2.")).Sum(c => c.Net), lines.Where(l => l.GetProperty("action").GetString() == "Record").Sum(l => Money(l.GetProperty("net"))));
        Assert.Equal(0m, Money(preview.GetProperty("credit")));
    }

    [Fact]
    public async Task I_G3_AnIncompleteCheckOut_ListsWhatIsMissing_AndTheStayStaysCheckedIn()
    {
        var stay = await CheckedInStayAsync(nights: 3);
        await GoToAsync(stay, stay.Arrival.AddDays(3));

        var response = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", CheckOutBody(stay.CatIds, complete: false), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(BusinessViolationCodes.CheckOutChecklistIncomplete, body);
        Assert.Contains("weight", body);
        Assert.Equal("CheckedIn", (await DetailAsync(stay)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task I_G4_AnExtension_ReQuotesAndReDerivesTheCharges_AndAStaleVersionIsAConflict()
    {
        var stay = await CheckedInStayAsync(nights: 5);
        var version = (await DetailAsync(stay)).GetProperty("version").GetInt32();
        var newDeparture = stay.Arrival.AddDays(8);

        var stale = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/extend", new { expectedVersion = version - 1, newDepartureOn = newDeparture }, Json);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var ok = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/extend", new { expectedVersion = version, newDepartureOn = newDeparture }, Json);
        Assert.True(ok.StatusCode == HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        Assert.Equal(8, (await ReadAsync(ok)).GetProperty("stay").GetProperty("nights").GetInt32());
        var charges = await ChargesAsync(stay.StayId);
        Assert.Contains(charges, c => c.Voided && c.Ref.StartsWith("r1."));
        var quoted = decimal.Parse((await DetailAsync(stay)).GetProperty("quote").GetProperty("total").GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(quoted, charges.Where(c => !c.Voided).Sum(c => c.Net));
    }

    [Fact]
    public async Task I_G5_WhenTheUnitIsBookedAfterwards_TheExtensionIsRefused_WithTheUnitsThatAreFree()
    {
        var stay = await CheckedInStayAsync(nights: 4);
        var departure = stay.Arrival.AddDays(4);
        var spare = await CreateUnitAsync(stay.Branch, "Standard", 1, block: "B");
        var (otherCustomer, otherId, _) = await RegisterAndSignInCustomerAsync();
        var otherCat = await SeedVaccinatedCatAsync(otherId);
        await ReserveOkAsync(otherCustomer, stay.Branch.BranchId, departure.AddDays(1), departure.AddDays(4), "Standard", otherCat);
        // the booked neighbour took the first free unit by block/sort order -- find which one is the stay's own
        var version = (await DetailAsync(stay)).GetProperty("version").GetInt32();

        var response = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/extend", new { expectedVersion = version, newDepartureOn = departure.AddDays(3) }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync(response);
        Assert.Contains(BusinessViolationCodes.UnitNotFreeForExtension, body.ToString());
        Assert.True(body.TryGetProperty("relocationOptions", out var options));
        Assert.True(options.ValueKind == JsonValueKind.Array);
        Assert.Contains(options.EnumerateArray(), o => o.GetProperty("unitId").GetGuid() == spare); // the units that ARE free are named (CODE_REVIEW 2026-10-15)
    }

    [Fact]
    public async Task I_G6_ARelocation_MovesTheStayToAnotherUnit_ForARecordedReason()
    {
        var stay = await CheckedInStayAsync(nights: 6);
        var spare = await CreateUnitAsync(stay.Branch, "Standard", 1, block: "B");
        var version = (await DetailAsync(stay)).GetProperty("version").GetInt32();

        var blank = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/relocate",
            new { expectedVersion = version, targetUnitId = spare, effectiveFromLocalDate = LocalToday, reason = " " }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);

        var moved = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/relocate",
            new { expectedVersion = version, targetUnitId = spare, effectiveFromLocalDate = LocalToday, reason = "Drain maintenance" }, Json);

        Assert.True(moved.StatusCode == HttpStatusCode.OK, await moved.Content.ReadAsStringAsync());
        var view = (await ReadAsync(moved)).GetProperty("stay");
        Assert.Equal(spare, view.GetProperty("unit").GetProperty("unitId").GetGuid());
        Assert.Equal(2, view.GetProperty("allocations").GetArrayLength());
    }

    [Fact]
    public async Task I_G7_AReferredCat_IsAway_CannotBeCheckedOut_UntilItReturns_AndTheVisitLinks()
    {
        var stay = await CheckedInStayAsync(nights: 3);
        var refer = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/referrals", new { catId = stay.CatIds[0], reason = "Limping", urgency = "Urgent" }, Json);
        Assert.Equal(HttpStatusCode.Created, refer.StatusCode);
        var referralId = (await ReadAsync(refer)).GetProperty("referralId").GetGuid();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var link = scope.ServiceProvider.GetRequiredService<IEventHandlerAsync<ReferralVisitOpenedEvent>>();
            await link.HandleAsync(new ReferralVisitOpenedEvent { ReferralId = referralId, VisitId = Guid.NewGuid(), CorrelationId = Guid.NewGuid(), UserContext = new(stay.Caretaker.StaffAccountId, Guid.Empty) });
        }

        var detail = await DetailAsync(stay);
        Assert.Equal("At the clinic", detail.GetProperty("cats")[0].GetProperty("awayReason").GetString());
        Assert.NotEqual(JsonValueKind.Null, detail.GetProperty("referrals")[0].GetProperty("visitId").ValueKind);

        await GoToAsync(stay, stay.Arrival.AddDays(3));
        var refused = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", CheckOutBody(stay.CatIds), Json);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(BusinessViolationCodes.CatNotPresent, await refused.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.NoContent, (await stay.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{stay.StayId}/referrals/{referralId}/return", null)).StatusCode);
        var ok = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", CheckOutBody(stay.CatIds), Json);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [Fact]
    public async Task I_G8_AClinicCharge_CountsAgainstTheHeadroom_OncePerCharge()
    {
        var stay = await CheckedInStayAsync(nights: 3);
        var url = $"/api/staff/boarding/stays/{stay.StayId}/cats/{stay.CatIds[0]}/vet-headroom";
        Assert.Equal("5000.00", (await ReadAsync(await stay.Branch.Admin.GetAsync(url))).GetProperty("headroom").GetProperty("amount").GetString());

        var chargeId = Guid.NewGuid();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IEventHandlerAsync<BoardingVetChargeRecordedEvent>>();
            var @event = new BoardingVetChargeRecordedEvent { ChargeId = chargeId, StayId = stay.StayId, CatId = stay.CatIds[0], Amount = 1500m, CorrelationId = Guid.NewGuid(), UserContext = new(stay.Caretaker.StaffAccountId, Guid.Empty) };
            await handler.HandleAsync(@event);
            await handler.HandleAsync(@event);
        }

        var headroom = await ReadAsync(await stay.Branch.Admin.GetAsync(url));
        Assert.Equal("3500.00", headroom.GetProperty("headroom").GetProperty("amount").GetString());
        Assert.Equal("1500.00", headroom.GetProperty("committed").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task I_G9_AnApprovalAboveTheLimit_NotifiesTheOwnerUrgently_TheOwnerAnswersOnce_AndOnlyTheirOwn()
    {
        var stay = await CheckedInStayAsync(nights: 3);
        var request = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/vet-approvals",
            new { catId = stay.CatIds[0], amount = "8000.00", description = "Dental surgery" }, Json);
        Assert.Equal(HttpStatusCode.Created, request.StatusCode);
        var approvalId = (await ReadAsync(request)).GetProperty("approvalId").GetGuid();
        Assert.Contains("boarding.vet-approval-requested", await NotificationKeysAsync(stay.CustomerId));

        var (stranger, _, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/customers/me/boarding/stays/{stay.StayId}/approvals/{approvalId}/respond", new { approve = true }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/approvals/{approvalId}")).StatusCode);

        var shown = await ReadAsync(await stay.Customer.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/approvals/{approvalId}"));
        Assert.Equal("Pending", shown.GetProperty("status").GetString());
        var answer = await stay.Customer.PostAsJsonAsync($"/api/customers/me/boarding/stays/{stay.StayId}/approvals/{approvalId}/respond", new { approve = true }, Json);
        Assert.Equal(HttpStatusCode.NoContent, answer.StatusCode);
        var again = await stay.Customer.PostAsJsonAsync($"/api/customers/me/boarding/stays/{stay.StayId}/approvals/{approvalId}/respond", new { approve = false }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        var headroom = await ReadAsync(await stay.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{stay.StayId}/cats/{stay.CatIds[0]}/vet-headroom"));
        Assert.Equal("13000.00", headroom.GetProperty("limit").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task I_G10_APhoneAnswer_NeedsANote_AndIsAttributedToStaff()
    {
        var stay = await CheckedInStayAsync(nights: 3);
        var request = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/vet-approvals", new { catId = stay.CatIds[0], amount = "2000.00", description = "X-ray" }, Json);
        var approvalId = (await ReadAsync(request)).GetProperty("approvalId").GetGuid();
        var url = $"/api/staff/boarding/stays/{stay.StayId}/vet-approvals/{approvalId}/staff-answer";

        Assert.Equal(HttpStatusCode.BadRequest, (await stay.Branch.Admin.PostAsJsonAsync(url, new { approve = true, note = " " }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await stay.Branch.Admin.PostAsJsonAsync(url, new { approve = true, note = "Owner agreed by phone" }, Json)).StatusCode);

        var approval = (await DetailAsync(stay)).GetProperty("approvals")[0];
        Assert.Equal("Approved", approval.GetProperty("status").GetString());
        Assert.Equal("Staff", approval.GetProperty("decidedVia").GetString());
    }

    [Fact]
    public async Task I_G11_ASurgeryOnABoardedCat_MakesItAway_WithoutReleasingTheUnit_UntilDischarge()
    {
        var stay = await CheckedInStayAsync(nights: 3);
        var surgeryId = Guid.NewGuid();
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var user = new Platform.Infrastructure.Common.Security.UserContext(stay.Caretaker.StaffAccountId, Guid.Empty);
            await scope.ServiceProvider.GetRequiredService<IEventHandlerAsync<SurgeryScheduledEvent>>().HandleAsync(new SurgeryScheduledEvent
            {
                SurgeryId = surgeryId, AppointmentId = Guid.NewGuid(), BranchId = stay.Branch.BranchId, CatId = stay.CatIds[0], CustomerId = stay.CustomerId, ServiceId = Guid.NewGuid(),
                StartsOn = Now.AddMinutes(-5), PricingBasis = SurgeryPricingBasis.Standard, CorrelationId = Guid.NewGuid(), UserContext = user
            });
        }

        Assert.Equal("In surgery", (await DetailAsync(stay)).GetProperty("cats")[0].GetProperty("awayReason").GetString());
        Assert.Equal(stay.UnitId, (await DetailAsync(stay)).GetProperty("unit").GetProperty("unitId").GetGuid());

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var user = new Platform.Infrastructure.Common.Security.UserContext(stay.Caretaker.StaffAccountId, Guid.Empty);
            await scope.ServiceProvider.GetRequiredService<IEventHandlerAsync<SurgeryDischargedEvent>>().HandleAsync(new SurgeryDischargedEvent
            {
                SurgeryId = surgeryId, CatId = stay.CatIds[0], BranchId = stay.Branch.BranchId, CustomerId = stay.CustomerId, ServiceId = Guid.NewGuid(), DischargedOn = Now,
                PricingBasis = SurgeryPricingBasis.Standard, CatWasDeceased = false, CorrelationId = Guid.NewGuid(), UserContext = user
            });
        }

        Assert.Equal(JsonValueKind.Null, (await DetailAsync(stay)).GetProperty("cats")[0].GetProperty("awayReason").ValueKind);
    }

    [Fact]
    public async Task I_G12_TheSummary_IsARead_ForTheOwnerOfTheStayOnly()
    {
        var stay = await CheckedInStayAsync(nights: 3);
        await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/observations",
            new { catId = stay.CatIds[0], portionEaten = "All", waterIntake = "Normal", litterOutput = "Normal", mood = "Relaxed", notes = "Happy" }, Json);
        await GoToAsync(stay, stay.Arrival.AddDays(3));
        Assert.Equal(HttpStatusCode.OK, (await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", CheckOutBody(stay.CatIds, 4.4m), Json)).StatusCode);

        var summary = await ReadAsync(await stay.Customer.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/summary"));

        var cat = summary.GetProperty("cats")[0];
        Assert.Equal(4.2m, cat.GetProperty("weightIn").GetDecimal());
        Assert.Equal(4.4m, cat.GetProperty("weightOut").GetDecimal());
        Assert.Equal("Happy", cat.GetProperty("observations")[0].GetProperty("notes").GetString());
        Assert.Equal(1, summary.GetProperty("charges").GetArrayLength());
        var (stranger, _, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/customers/me/boarding/stays/{stay.StayId}/summary")).StatusCode);
    }

    [Fact]
    public async Task I_G14_ALongStay_IsInvoicedPerThirtyNightCycle_OnceEach_AndTheFinalInvoiceCoversTheRest()
    {
        var stay = await CheckedInStayAsync(nights: 45, deposit: "15000.00");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            // Tax is 0 % at launch (owner decision); a rate must still be in force for the class to invoice.
            var rates = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Administration.TaxRateAggregate>>();
            var rate = new CatEcommerce.Domain.Aggregates.Administration.TaxRateAggregate(Guid.NewGuid());
            rate.Create(new CatEcommerce.Domain.Models.TaxRateCreateDto(stay.Branch.TaxClassId, 0m, Now.AddYears(-1), Now.AddYears(-1).AddDays(-1), new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await rates.SaveAsync(rate, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);
        }

        var profile = await stay.Branch.Admin.PutAsJsonAsync($"/api/admin/billing-profiles/{stay.Branch.BranchId}", new
        {
            legalName = "PurrCare Test Ltd", taxRegistrationNumber = "BIN-" + NewCode(), addressLines = new[] { "1 Test Road" }, phone = "01700000000", email = "billing@example.test",
            invoiceFooter = "Thank you", bankAccounts = new[] { new { bankName = "Test Bank", accountName = "PurrCare Ltd", accountNumber = "1234567890", branchName = "Gulshan", routingNumber = "012345678" } },
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, profile.StatusCode);

        await RunBoardingWorkCycleAsync();
        Assert.DoesNotContain(true, (await DetailAsync(stay)).GetProperty("charges").EnumerateArray().Select(c => c.GetProperty("billed").GetBoolean()));

        await GoToAsync(stay, stay.Arrival.AddDays(30), 10);
        await RunBoardingWorkCycleAsync();
        var billed = (await DetailAsync(stay)).GetProperty("charges").EnumerateArray().Where(c => !c.GetProperty("voided").GetBoolean()).ToList();
        Assert.Equal(1, billed.Count(c => c.GetProperty("billed").GetBoolean()));
        Assert.Contains(billed, c => !c.GetProperty("billed").GetBoolean()); // the rest of the stay is not yet due

        await RunBoardingWorkCycleAsync(); // a second pass issues nothing more
        Assert.Equal(1, (await DetailAsync(stay)).GetProperty("charges").EnumerateArray().Count(c => c.GetProperty("billed").GetBoolean()));

        await GoToAsync(stay, stay.Arrival.AddDays(45), 10);
        var checkOut = await stay.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{stay.StayId}/check-out", CheckOutBody(stay.CatIds), Json);
        Assert.True(checkOut.StatusCode == HttpStatusCode.OK, await checkOut.Content.ReadAsStringAsync());
        await RunBoardingWorkCycleAsync();
        Assert.DoesNotContain(false, (await DetailAsync(stay)).GetProperty("charges").EnumerateArray().Where(c => !c.GetProperty("voided").GetBoolean()).Select(c => c.GetProperty("billed").GetBoolean()));
    }

    [Fact]
    public async Task I_G13_AFlaggedStay_IsClearedWithANote()
    {
        var stay = await CheckedInStayAsync(nights: 3);
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<CatEcommerce.Domain.Aggregates.Boarding.BoardingStayAggregate>>();
            var aggregate = (await repo.GetByIdAsync(stay.StayId))!;
            aggregate.FlagAttention(StayAttentionReason.CatOwnerChanged);
            await repo.UpdateAsync(aggregate, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);
        }

        var url = $"/api/staff/boarding/stays/{stay.StayId}/attention/clear";
        Assert.Equal(HttpStatusCode.BadRequest, (await stay.Branch.Admin.PostAsJsonAsync(url, new { note = " " }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await stay.Branch.Admin.PostAsJsonAsync(url, new { note = "Spoke to both owners" }, Json)).StatusCode);

        var detail = await DetailAsync(stay);
        Assert.False(detail.GetProperty("needsAttention").GetBoolean());
        Assert.Equal("Spoke to both owners", detail.GetProperty("attentionClearedNote").GetString());
    }
}
