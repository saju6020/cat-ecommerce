using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Administration;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;
using Xunit.Abstractions;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// TEST gate for HO-BRD-001 (cases T-M*, T-D*, T-R*, I-H3b of docs/test-cases/TC-HO-BRD-001.md). Expected figures were written into the sheet BEFORE this class was run: a hand-built fixture
/// (one Standard unit; nightly 1000, weekly 6000, monthly 24000; threshold 14 nights; tax 0 %) priced by hand, never by calling the calculator under test.
/// The fake clock never moves backwards, so every test's arrival is "tomorrow" on the clock it finds.
/// </summary>
[Collection("Boarding")]
public sealed class BoardingTestGateTests(BoardingApiFactory factory, ITestOutputHelper output) : BoardingIntegrationTestBase(factory)
{
    private sealed record Fx(BoardingBranch Branch, HttpClient Customer, string Mobile, Guid CustomerId, Guid StayId, Guid[] CatIds, Guid UnitId, DateOnly Arrival, int Nights);

    private static decimal Dec(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

    private async Task SetSettingAsync(BoardingBranch branch, string key, string value)
    {
        var response = await branch.Admin.PutAsJsonAsync($"/api/admin/settings?branchId={branch.BranchId}", new { changes = new[] { new { key, value } } }, Json);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Reserved (not paid): a fresh branch with one unit of <paramref name="capacity"/>, the fixture's rate card, a customer with vaccinated cats.</summary>
    private async Task<Fx> ReserveFixtureAsync(int nights, int cats = 1, string additionalCat = "0.00", string? discountPercent = null, int capacity = 1, int daysAhead = 1)
    {
        var branch = await SeedBranchAsync();
        var unit = await CreateUnitAsync(branch, "Standard", capacity);
        await SetRateCardAsync(branch, "Standard", nightly: "1000.00", weekly: "6000.00", monthly: "24000.00", additionalCat: additionalCat);
        if (discountPercent is not null) await SetSettingAsync(branch, "boarding.longStayDiscountPercent", discountPercent);
        await SeedCaretakerAsync(branch);
        var (customer, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var catIds = new List<Guid>();
        for (var i = 0; i < cats; i++) catIds.Add(await SeedVaccinatedCatAsync(customerId, "Cat" + i));

        var arrival = LocalToday.AddDays(daysAhead);
        var stayId = await ReserveOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(nights), "Standard", catIds.ToArray());
        return new Fx(branch, customer, mobile, customerId, stayId, catIds.ToArray(), unit, arrival, nights);
    }

    private async Task GoToLocalAsync(Fx f, DateOnly day, int hour, int minute = 0)
    {
        var at = new DateTimeOffset(day.Year, day.Month, day.Day, hour, minute, 0, TimeSpan.FromHours(6));
        Factory.Clock.SetUtcNow(at.ToUniversalTime());
        await ReauthenticateAsync(f.Branch.Admin, f.Branch.AdminLogin);
        await ReauthenticateCustomerAsync(f.Customer, f.Mobile);
    }

    private async Task ConfirmAsync(Fx f, string deposit)
    {
        await RecordDepositAsync(f.Branch, f.CustomerId, f.StayId, deposit);
        var confirm = await f.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{f.StayId}/confirm", null);
        Assert.True(confirm.StatusCode == HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
    }

    private async Task<Fx> CheckedInFixtureAsync(int nights, string deposit, int cats = 1, string additionalCat = "0.00", string? discountPercent = null, int capacity = 1)
    {
        var f = await ReserveFixtureAsync(nights, cats, additionalCat, discountPercent, capacity);
        await ConfirmAsync(f, deposit);
        await GoToLocalAsync(f, f.Arrival, 14, 30);
        var checkIn = await f.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{f.StayId}/check-in", CheckInBody(f.CatIds), Json);
        Assert.True(checkIn.StatusCode == HttpStatusCode.OK, await checkIn.Content.ReadAsStringAsync());
        return f;
    }

    private async Task<JsonElement> DetailAsync(Fx f) => await ReadAsync(await f.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{f.StayId}"));

    private async Task<JsonElement> PreviewAsync(Fx f) => await ReadAsync(await f.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{f.StayId}/check-out-preview"));

    private static object CheckOutBody(Guid[] catIds) => new
    {
        cats = catIds.Select(id => new { catId = id, weightKg = 4.1m, belongings = Array.Empty<object>(), signature = TypedSignature() }).ToArray()
    };

    private async Task<JsonElement> CheckOutOkAsync(Fx f)
    {
        var response = await f.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{f.StayId}/check-out", CheckOutBody(f.CatIds), Json);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await ReadAsync(response)).GetProperty("stay");
    }

    private async Task<List<(string Ref, bool Voided, decimal Net)>> ChargesAsync(Guid stayId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var rows = await db.Charges.AsNoTracking().Where(c => c.SourceKind == ChargeSourceKind.BoardingStay && c.SourceId == stayId).ToListAsync();
        return rows.Select(c => (c.SourceLineRef, c.Status == ChargeStatus.Voided, c.Quantity * c.UnitPriceAmount - c.LineDiscountAmount)).OrderBy(c => c.SourceLineRef).ToList();
    }

    private static decimal Money(JsonElement m) => Dec(m.GetProperty("amount").GetString()!);

    private async Task PrepareInvoicingAsync(Fx f)
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var rates = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<TaxRateAggregate>>();
            var rate = new TaxRateAggregate(Guid.NewGuid());
            rate.Create(new TaxRateCreateDto(f.Branch.TaxClassId, 0m, Now.AddYears(-1), Now.AddYears(-1).AddDays(-1), new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await rates.SaveAsync(rate, EventPublishOption.InMemoryPublishOnly);
        }

        var profile = await f.Branch.Admin.PutAsJsonAsync($"/api/admin/billing-profiles/{f.Branch.BranchId}", new
        {
            legalName = "PurrCare Test Ltd", taxRegistrationNumber = "BIN-" + NewCode(), addressLines = new[] { "1 Test Road" }, phone = "01700000000", email = "billing@example.test",
            invoiceFooter = "Thank you", bankAccounts = new[] { new { bankName = "Test Bank", accountName = "PurrCare Ltd", accountNumber = "1234567890", branchName = "Gulshan", routingNumber = "012345678" } },
        }, Json);
        Assert.Equal(HttpStatusCode.NoContent, profile.StatusCode);
    }

    /// <summary>The net of every line of every invoice of the stay, one figure per invoice.</summary>
    private async Task<List<decimal>> InvoiceNetsAsync(Fx f)
    {
        var summary = await ReadAsync(await f.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{f.StayId}/summary"));
        var nets = new List<decimal>();
        foreach (var id in summary.GetProperty("billing").GetProperty("invoiceIds").EnumerateArray().Select(i => i.GetGuid()))
        {
            var invoice = await ReadAsync(await f.Branch.Admin.GetAsync($"/api/staff/billing/invoices/{id}"));
            nets.Add(invoice.GetProperty("lines").EnumerateArray().Sum(l => Money(l.GetProperty("netAmount"))));
        }

        return nets.OrderBy(n => n).ToList();
    }

    // ── money: a 3-night stay ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T_M1_AThreeNightStay_IsQuotedAndChargedThreeThousand()
    {
        var f = await ReserveFixtureAsync(3);
        var quote = (await DetailAsync(f)).GetProperty("quote");
        Assert.Equal("Nightly", quote.GetProperty("basis").GetString());
        Assert.Equal(3000m, Money(quote.GetProperty("total")));
        Assert.Equal(0m, Money(quote.GetProperty("discountTotal")));
        Assert.Single(quote.GetProperty("units").EnumerateArray());
        Assert.Equal(900m, Money((await DetailAsync(f)).GetProperty("deposit").GetProperty("required")));

        await ConfirmAsync(f, "900.00");
        await GoToLocalAsync(f, f.Arrival, 14, 30);
        Assert.Equal(HttpStatusCode.OK, (await f.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{f.StayId}/check-in", CheckInBody(f.CatIds), Json)).StatusCode);

        var charges = await ChargesAsync(f.StayId);
        Assert.Single(charges);
        Assert.Equal(3000m, charges[0].Net);
        Assert.False(charges[0].Voided);
    }

    // ── money: a 31-night stay with a 10 % long-stay discount and a cycle invoice ────────────────────────────────────

    [Fact]
    public async Task T_M2_AThirtyOneNightStay_IsOneMonthPlusOneNight_WithTheDiscountOnEachUnit_AndInvoicedPerCycle()
    {
        var f = await ReserveFixtureAsync(31, discountPercent: "10.00");
        var detail = await DetailAsync(f);
        var quote = detail.GetProperty("quote");
        Assert.Equal("Monthly", quote.GetProperty("basis").GetString());
        Assert.Equal(25000m, Money(quote.GetProperty("gross")));
        Assert.Equal(2500m, Money(quote.GetProperty("discountTotal")));
        Assert.Equal(22500m, Money(quote.GetProperty("total")));
        Assert.Equal(6750m, Money(detail.GetProperty("deposit").GetProperty("required")));
        var units = quote.GetProperty("units").EnumerateArray().ToList();
        Assert.Equal(2, units.Count);
        Assert.Equal([24000m, 1000m], units.Select(u => Money(u.GetProperty("amount"))).ToArray());

        await ConfirmAsync(f, "6750.00");
        await GoToLocalAsync(f, f.Arrival, 14, 30);
        Assert.Equal(HttpStatusCode.OK, (await f.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{f.StayId}/check-in", CheckInBody(f.CatIds), Json)).StatusCode);
        var charges = await ChargesAsync(f.StayId);
        Assert.Equal([900m, 21600m], charges.Select(c => c.Net).OrderBy(n => n).ToArray());
        Assert.Equal(22500m, charges.Sum(c => c.Net));

        await PrepareInvoicingAsync(f);
        await GoToLocalAsync(f, f.Arrival.AddDays(30), 10);
        await RunBoardingWorkCycleAsync();
        Assert.Equal([21600m], await InvoiceNetsAsync(f)); // the cycle invoice carries the month only

        await GoToLocalAsync(f, f.Arrival.AddDays(31), 10);
        var view = await CheckOutOkAsync(f);
        Assert.Equal(22500m, Money(view.GetProperty("quote").GetProperty("total"))); // on the booked departure: nothing changes
        await RunBoardingWorkCycleAsync();
        Assert.Equal([900m, 21600m], await InvoiceNetsAsync(f)); // the final invoice carries the remaining night only
        Assert.Equal(22500m, (await InvoiceNetsAsync(f)).Sum());
    }

    // ── money: early check-out ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T_M3_AnEarlyCheckOutAfterThreeOfTenNights_ChargesThreeThousand_AndThePreviewSaidSo()
    {
        var f = await CheckedInFixtureAsync(10, "3000.00");
        await GoToLocalAsync(f, f.Arrival.AddDays(3), 10);
        var before = await ChargesAsync(f.StayId);

        var preview = await PreviewAsync(f);

        Assert.True(preview.GetProperty("isEarly").GetBoolean());
        Assert.False(preview.GetProperty("isLate").GetBoolean());
        Assert.Equal(3, preview.GetProperty("nightsCharged").GetInt32());
        Assert.Equal(10000m, Money(preview.GetProperty("previousTotal")));
        Assert.Equal(3000m, Money(preview.GetProperty("newTotal")));
        Assert.Equal(0m, Money(preview.GetProperty("credit")));
        Assert.Equal(before, await ChargesAsync(f.StayId));

        var view = await CheckOutOkAsync(f);
        Assert.Equal("Nightly", view.GetProperty("quote").GetProperty("basis").GetString());
        Assert.Equal(3000m, Money(view.GetProperty("quote").GetProperty("total")));
        var after = await ChargesAsync(f.StayId);
        Assert.Equal([10000m], after.Where(c => c.Voided).Select(c => c.Net).ToArray());
        var active = Assert.Single(after.Where(c => !c.Voided));
        Assert.Equal(3000m, active.Net);
        Assert.StartsWith("r2.", active.Ref);
    }

    [Fact]
    public async Task T_M3b_AnEarlyCheckOutOnDayThirty_AfterTheCycleInvoice_KeepsTheInvoicedMonth_AndVoidsTheUnbilledNight()
    {
        var f = await CheckedInFixtureAsync(31, "7000.00", discountPercent: "10.00");
        await PrepareInvoicingAsync(f);
        await GoToLocalAsync(f, f.Arrival.AddDays(30), 10);
        await RunBoardingWorkCycleAsync();
        Assert.Equal([21600m], await InvoiceNetsAsync(f));

        var preview = await PreviewAsync(f);

        Assert.True(preview.GetProperty("isEarly").GetBoolean());
        Assert.Equal(30, preview.GetProperty("nightsCharged").GetInt32());
        Assert.Equal(21600m, Money(preview.GetProperty("newTotal")));
        Assert.Equal(0m, Money(preview.GetProperty("credit")));
        Assert.Equal("None", preview.GetProperty("creditOutcome").GetString());
        var lines = preview.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(900m, Assert.Single(lines.Where(l => l.GetProperty("action").GetString() == "Void").Select(l => Money(l.GetProperty("net")))));
        Assert.Equal(21600m, Assert.Single(lines.Where(l => l.GetProperty("action").GetString() == "Keep").Select(l => Money(l.GetProperty("net")))));

        var view = await CheckOutOkAsync(f);
        Assert.Equal("Monthly", view.GetProperty("quote").GetProperty("basis").GetString());
        Assert.Equal(21600m, Money(view.GetProperty("quote").GetProperty("total")));
        Assert.True(!view.TryGetProperty("creditDue", out var credit) || credit.ValueKind == JsonValueKind.Null || Money(credit) == 0m, "creditDue must be absent or zero");
        var after = await ChargesAsync(f.StayId);
        Assert.Equal(21600m, after.Where(c => !c.Voided).Sum(c => c.Net));
        Assert.Equal([900m], after.Where(c => c.Voided).Select(c => c.Net).ToArray());
        await RunBoardingWorkCycleAsync();
        Assert.Equal([21600m], await InvoiceNetsAsync(f)); // no second invoice for a voided charge
    }

    [Fact]
    public async Task T_M3c_AnEarlyCheckOutOnDayTen_OfADiscountedMonthlyStay_LosesTheDiscount_AndIsTenThousand()
    {
        var f = await CheckedInFixtureAsync(31, "7000.00", discountPercent: "10.00");
        await GoToLocalAsync(f, f.Arrival.AddDays(10), 10);

        var preview = await PreviewAsync(f);

        Assert.Equal(10, preview.GetProperty("nightsCharged").GetInt32());
        Assert.Equal(22500m, Money(preview.GetProperty("previousTotal")));
        Assert.Equal(10000m, Money(preview.GetProperty("newTotal")));
        Assert.Equal("Nightly", preview.GetProperty("quote").GetProperty("basis").GetString());
        Assert.Equal(0m, Money(preview.GetProperty("quote").GetProperty("discountTotal")));

        var view = await CheckOutOkAsync(f);
        Assert.Equal(10000m, Money(view.GetProperty("quote").GetProperty("total")));
        var after = await ChargesAsync(f.StayId);
        Assert.Equal([900m, 21600m], after.Where(c => c.Voided).Select(c => c.Net).OrderBy(n => n).ToArray());
        Assert.Equal([10000m], after.Where(c => !c.Voided).Select(c => c.Net).ToArray());
    }

    // ── money: a late check-out by two nights, two cats ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task T_M4_ALateCheckOutByTwoNights_ChargesFiveNightsForTwoCats_NeverBlocks_DoesNotExtendTheAllocation_AndFlagsTheNextBooking()
    {
        var f = await CheckedInFixtureAsync(3, "2000.00", cats: 2, additionalCat: "200.00", capacity: 2);
        Assert.Equal(3600m, Money((await DetailAsync(f)).GetProperty("quote").GetProperty("total"))); // 3 x 1000 + 1 x 200 x 3

        var nextStart = f.Arrival.AddDays(3);
        var (next, nextId, _) = await RegisterAndSignInCustomerAsync();
        var nextStay = await ReserveOkAsync(next, f.Branch.BranchId, nextStart, nextStart.AddDays(2), "Standard", await SeedVaccinatedCatAsync(nextId));

        string ActiveTo(JsonElement detail) =>
            detail.GetProperty("allocations").EnumerateArray().Where(a => a.GetProperty("isActive").GetBoolean() && a.GetProperty("unitId").GetGuid() == f.UnitId)
                .Select(a => a.GetProperty("occupiedTo").GetString()!).Single();

        var allocatedTo = ActiveTo(await DetailAsync(f));
        await GoToLocalAsync(f, f.Arrival.AddDays(5), 10);

        var preview = await PreviewAsync(f);
        Assert.True(preview.GetProperty("isLate").GetBoolean());
        Assert.False(preview.GetProperty("isEarly").GetBoolean());
        Assert.Equal(5, preview.GetProperty("nightsCharged").GetInt32());
        Assert.Equal(6000m, Money(preview.GetProperty("newTotal")));

        var view = await CheckOutOkAsync(f);
        Assert.Equal(6000m, Money(view.GetProperty("quote").GetProperty("total")));
        Assert.Equal(6000m, (await ChargesAsync(f.StayId)).Where(c => !c.Voided).Sum(c => c.Net));
        Assert.Equal(allocatedTo, ActiveTo(await DetailAsync(f))); // charged, not extended

        var warned = await ReadAsync(await f.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{nextStay}"));
        Assert.True(warned.GetProperty("needsAttention").GetBoolean());
        Assert.Equal("UnitOverstayed", warned.GetProperty("attentionReason").GetString());
    }

    // ── money: extension ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T_M5_AnExtensionFromFiveToEightNights_IsEightThousand_AndFromEightToFifteenCrossesTheThreshold_ToThirteenThousand()
    {
        var f = await CheckedInFixtureAsync(5, "1500.00");
        Assert.Equal(5000m, Money((await DetailAsync(f)).GetProperty("quote").GetProperty("total")));

        async Task<JsonElement> ExtendAsync(int nights)
        {
            var version = (await DetailAsync(f)).GetProperty("version").GetInt32();
            var response = await f.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{f.StayId}/extend", new { expectedVersion = version, newDepartureOn = f.Arrival.AddDays(nights) }, Json);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return (await ReadAsync(response)).GetProperty("stay");
        }

        var eight = await ExtendAsync(8);
        Assert.Equal("Nightly", eight.GetProperty("quote").GetProperty("basis").GetString());
        Assert.Equal(8000m, Money(eight.GetProperty("quote").GetProperty("total")));
        var afterEight = await ChargesAsync(f.StayId);
        Assert.Equal(8000m, afterEight.Where(c => !c.Voided).Sum(c => c.Net));
        Assert.Equal([5000m], afterEight.Where(c => c.Voided).Select(c => c.Net).ToArray());

        var fifteen = await ExtendAsync(15);
        var quote = fifteen.GetProperty("quote");
        Assert.Equal("Weekly", quote.GetProperty("basis").GetString());
        Assert.Equal(13000m, Money(quote.GetProperty("total")));
        Assert.Equal([6000m, 6000m, 1000m], quote.GetProperty("units").EnumerateArray().Select(u => Money(u.GetProperty("amount"))).ToArray());
        var afterFifteen = await ChargesAsync(f.StayId);
        Assert.Equal([1000m, 6000m, 6000m], afterFifteen.Where(c => !c.Voided).Select(c => c.Net).OrderBy(n => n).ToArray());
        Assert.Equal(13000m, afterFifteen.Where(c => !c.Voided).Sum(c => c.Net));
    }

    // ── money: the deposit ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T_M6_TheDepositOfNineHundred_ConfirmsOnlyAtNineHundred_AndAnotherCustomersMoneyDoesNotCount()
    {
        var f = await ReserveFixtureAsync(3);
        var (_, strangerId, _) = await RegisterAndSignInCustomerAsync();

        await RecordDepositAsync(f.Branch, strangerId, f.StayId, "900.00"); // the full amount, but another customer's money
        var strangers = await f.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{f.StayId}/confirm", null);
        Assert.Equal(HttpStatusCode.BadRequest, strangers.StatusCode);
        Assert.Contains(BusinessViolationCodes.DepositNotYetReceived, await strangers.Content.ReadAsStringAsync());
        Assert.Equal(0m, Money((await DetailAsync(f)).GetProperty("deposit").GetProperty("received")));

        await RecordDepositAsync(f.Branch, f.CustomerId, f.StayId, "899.99");
        var short1 = await f.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{f.StayId}/confirm", null);
        Assert.Equal(HttpStatusCode.BadRequest, short1.StatusCode);
        Assert.Contains(BusinessViolationCodes.DepositNotYetReceived, await short1.Content.ReadAsStringAsync());

        await RecordDepositAsync(f.Branch, f.CustomerId, f.StayId, "0.01");
        var ok = await f.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{f.StayId}/confirm", null);
        Assert.True(ok.StatusCode == HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        var detail = await DetailAsync(f);
        Assert.Equal("Confirmed", detail.GetProperty("status").GetString());
        Assert.Equal(900m, Money(detail.GetProperty("deposit").GetProperty("received")));
        Assert.Equal(2100m, Money(detail.GetProperty("balanceDue")));
    }

    // -- owner decision "Keep everything" (erasure) -------------------------------------------------------------------

    [Fact]
    public async Task T_E1_ErasureOfACustomerWithAStay_IsHeldForTheFinancialYears_AndChangesNothing_WhileAnEnquiryOnlyCustomerIsCompleted()
    {
        var f = await ReserveFixtureAsync(3);
        var (enquirer, enquirerId, _) = await RegisterAndSignInCustomerAsync();
        await EnquireOkAsync(enquirer, f.Branch.BranchId, f.Arrival.AddDays(20), f.Arrival.AddDays(22), "Standard", await SeedVaccinatedCatAsync(enquirerId));
        var before = await DetailAsync(f);
        await using var scope = Factory.Services.CreateAsyncScope();
        var eraser = scope.ServiceProvider.GetServices<CatEcommerce.Domain.DomainServices.IPersonalDataEraser>().Single(e => e.ContextKey == "boarding");
        var actor = new UserContext(Guid.NewGuid(), Guid.NewGuid());

        var held = await eraser.EraseAsync(f.CustomerId, Now, actor, CancellationToken.None);
        var nothing = await eraser.EraseAsync(enquirerId, Now, actor, CancellationToken.None);

        var outcome = Assert.IsType<CatEcommerce.Domain.Models.ErasureOutcome.Held>(held);
        Assert.True(outcome.Until > f.Arrival.AddYears(1), "held until " + outcome.Until); // financial retention is years, not days
        Assert.IsType<CatEcommerce.Domain.Models.ErasureOutcome.Completed>(nothing);
        var after = await DetailAsync(f);
        Assert.Equal(before.GetProperty("status").GetString(), after.GetProperty("status").GetString());
        Assert.Equal(before.GetProperty("version").GetInt32(), after.GetProperty("version").GetInt32()); // the eraser mutated nothing
    }

    // ── Dhaka dates ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T_D1_D2_CheckIn_IsRefusedAt2350TheDayBefore_AndAllowedAt0010OnTheArrivalDate()
    {
        var f = await ReserveFixtureAsync(3);
        await ConfirmAsync(f, "900.00");

        await GoToLocalAsync(f, f.Arrival.AddDays(-1), 23, 50);
        var early = await f.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{f.StayId}/check-in", CheckInBody(f.CatIds), Json);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Contains(BusinessViolationCodes.CheckInNotOnArrivalDate, await early.Content.ReadAsStringAsync());

        await GoToLocalAsync(f, f.Arrival, 0, 10); // 18:10 UTC of the day before: a UTC date would still refuse
        var ok = await f.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{f.StayId}/check-in", CheckInBody(f.CatIds), Json);
        Assert.True(ok.StatusCode == HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task T_D3_CheckIn_IsAllowedAt2350OnTheArrivalDate()
    {
        var f = await ReserveFixtureAsync(3);
        await ConfirmAsync(f, "900.00");

        await GoToLocalAsync(f, f.Arrival, 23, 50);

        var ok = await f.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{f.StayId}/check-in", CheckInBody(f.CatIds), Json);
        Assert.True(ok.StatusCode == HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task T_D4_CheckOutAt2350OnTheSecondNight_IsEarly_AndChargesTwoNights()
    {
        var f = await CheckedInFixtureAsync(3, "900.00");
        await GoToLocalAsync(f, f.Arrival.AddDays(2), 23, 50);

        var preview = await PreviewAsync(f);

        Assert.True(preview.GetProperty("isEarly").GetBoolean());
        Assert.Equal(2, preview.GetProperty("nightsCharged").GetInt32());
        Assert.Equal(2000m, Money(preview.GetProperty("newTotal")));
        Assert.Equal(2000m, Money((await CheckOutOkAsync(f)).GetProperty("quote").GetProperty("total")));
    }

    [Fact]
    public async Task T_D5_CheckOutAt0010OnTheDepartureDate_IsOnTime_NotEarly()
    {
        var f = await CheckedInFixtureAsync(3, "900.00");
        await GoToLocalAsync(f, f.Arrival.AddDays(3), 0, 10); // 18:10 UTC of the day before the departure date

        var preview = await PreviewAsync(f);

        Assert.False(preview.GetProperty("isEarly").GetBoolean());
        Assert.False(preview.GetProperty("isLate").GetBoolean());
        Assert.Equal(3, preview.GetProperty("nightsCharged").GetInt32());
        Assert.Equal(3000m, Money((await CheckOutOkAsync(f)).GetProperty("quote").GetProperty("total")));
    }

    [Fact]
    public async Task T_D6_CheckOutAt2350OnTheDepartureDate_IsOnTime_NotLate()
    {
        var f = await CheckedInFixtureAsync(3, "900.00");
        await GoToLocalAsync(f, f.Arrival.AddDays(3), 23, 50);

        var preview = await PreviewAsync(f);

        Assert.False(preview.GetProperty("isEarly").GetBoolean());
        Assert.False(preview.GetProperty("isLate").GetBoolean());
        Assert.Equal(3000m, Money((await CheckOutOkAsync(f)).GetProperty("quote").GetProperty("total")));
    }

    [Fact]
    public async Task T_D7_CheckOutAt0010TheDayAfterDeparture_IsLate_AndChargesFourNights()
    {
        var f = await CheckedInFixtureAsync(3, "900.00");
        await GoToLocalAsync(f, f.Arrival.AddDays(4), 0, 10); // 18:10 UTC on the departure date: a UTC date would say "on time"

        var preview = await PreviewAsync(f);

        Assert.True(preview.GetProperty("isLate").GetBoolean());
        Assert.Equal(4, preview.GetProperty("nightsCharged").GetInt32());
        Assert.Equal(4000m, Money(preview.GetProperty("newTotal")));
        Assert.Equal(4000m, Money((await CheckOutOkAsync(f)).GetProperty("quote").GetProperty("total")));
    }

    // ── real overlapping races ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task T_R1_TwoCustomersReservingTheLastUnitAtOnce_HaveOneWinner_EveryRound()
    {
        var winners = 0;
        for (var round = 0; round < 5; round++)
        {
            var branch = await SeedBranchAsync();
            await CreateUnitAsync(branch, "Standard", 1);
            await SetRateCardAsync(branch);
            var arrival = LocalToday.AddDays(5);
            var stays = new List<(HttpClient Client, Guid StayId)>();
            for (var i = 0; i < 2; i++)
            {
                var (client, customerId, _) = await RegisterAndSignInCustomerAsync();
                var cat = await SeedVaccinatedCatAsync(customerId);
                var stayId = await EnquireOkAsync(client, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
                await SetProfileAsync(client, stayId, cat);
                stays.Add((client, stayId));
            }

            var start = new TaskCompletionSource();
            var tasks = stays.Select(s => Task.Run(async () => { await start.Task; return await ReserveAsync(s.Client, s.StayId); })).ToArray();
            start.SetResult();
            var responses = await Task.WhenAll(tasks);

            var codes = responses.Select(r => (int)r.StatusCode).OrderBy(c => c).ToArray();
            Assert.Equal([200, 409], codes);
            var loser = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Contains(BusinessViolationCodes.UnitNoLongerAvailable, await loser.Content.ReadAsStringAsync());
            var statuses = new List<string>();
            foreach (var s in stays) statuses.Add((await ReadAsync(await branch.Admin.GetAsync($"/api/staff/boarding/stays/{s.StayId}"))).GetProperty("status").GetString()!);
            Assert.Equal(["Enquiry", "Reserved"], statuses.OrderBy(s => s).ToArray());
            winners++;
        }

        Assert.Equal(5, winners);
    }

    [Fact]
    public async Task T_R1b_TwoCustomersReservingAtOnce_WhenTwoUnitsOfTheTypeAreFree_BothSucceed()
    {
        var outcomes = new List<string>();
        for (var round = 0; round < 5; round++)
        {
            var branch = await SeedBranchAsync();
            await CreateUnitAsync(branch, "Standard", 1);
            await CreateUnitAsync(branch, "Standard", 1);
            await SetRateCardAsync(branch);
            var arrival = LocalToday.AddDays(5);
            var stays = new List<(HttpClient Client, Guid StayId)>();
            for (var i = 0; i < 2; i++)
            {
                var (client, customerId, _) = await RegisterAndSignInCustomerAsync();
                var cat = await SeedVaccinatedCatAsync(customerId);
                var stayId = await EnquireOkAsync(client, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
                await SetProfileAsync(client, stayId, cat);
                stays.Add((client, stayId));
            }

            var start = new TaskCompletionSource();
            var tasks = stays.Select(s => Task.Run(async () => { await start.Task; return await ReserveAsync(s.Client, s.StayId); })).ToArray();
            start.SetResult();
            var responses = await Task.WhenAll(tasks);
            outcomes.Add(string.Join("/", responses.Select(r => (int)r.StatusCode).OrderBy(c => c)));
        }

        output.WriteLine("status pairs per round: " + string.Join(", ", outcomes));
        Assert.All(outcomes, o => Assert.Equal("200/200", o)); // a free unit must never be reported "no longer available"
    }

    [Fact]
    public async Task A41_TheFinalInvoice_CarriesTheNights_AndTheDepositIsOfferedForAllocation()
    {
        var f = await CheckedInFixtureAsync(3, "900.00");
        await PrepareInvoicingAsync(f);
        await GoToLocalAsync(f, f.Arrival.AddDays(3), 10);
        await CheckOutOkAsync(f);
        await RunBoardingWorkCycleAsync();

        var summary = await ReadAsync(await f.Branch.Admin.GetAsync($"/api/staff/boarding/stays/{f.StayId}/summary"));
        var invoiceId = Assert.Single(summary.GetProperty("billing").GetProperty("invoiceIds").EnumerateArray()).GetGuid();
        var invoice = await ReadAsync(await f.Branch.Admin.GetAsync($"/api/staff/billing/invoices/{invoiceId}"));
        Assert.Equal(3000m, invoice.GetProperty("lines").EnumerateArray().Sum(l => Money(l.GetProperty("netAmount"))));
        // Billing does not auto-apply an earmarked deposit on issue (backlog row 61): the invoice is outstanding in full and "allocate" is offered for the deposit payment.
        Assert.Equal(3000m, Money(invoice.GetProperty("outstanding")));
        Assert.Contains("allocate", invoice.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));
    }

    [Fact]
    public async Task T_R1c_TwoSuspectedInfectiousFlagsAtOnce_WhenTwoIsolationUnitsAreFree_BothMoveToDifferentUnits()
    {
        var outcomes = new List<string>();
        for (var round = 0; round < 3; round++)
        {
            var branch = await SeedBranchAsync();
            await CreateUnitAsync(branch, "Standard", 1);
            await CreateUnitAsync(branch, "Standard", 1);
            var iso1 = await CreateUnitAsync(branch, "Isolation", 1, block: "Z");
            var iso2 = await CreateUnitAsync(branch, "Isolation", 1, block: "Z");
            await SetRateCardAsync(branch);
            await SetRateCardAsync(branch, "Isolation", nightly: "1200.00");
            await SeedCaretakerAsync(branch);
            var arrival = LocalToday.AddDays(1);
            var stays = new List<(Guid StayId, Guid CatId)>();
            for (var i = 0; i < 2; i++)
            {
                var (client, customerId, _) = await RegisterAndSignInCustomerAsync();
                var cat = await SeedVaccinatedCatAsync(customerId);
                var stayId = await ReserveOkAsync(client, branch.BranchId, arrival, arrival.AddDays(3), "Standard", cat);
                await RecordDepositAsync(branch, customerId, stayId, "900.00");
                stays.Add((stayId, cat));
            }

            var at = new DateTimeOffset(arrival.Year, arrival.Month, arrival.Day, 14, 30, 0, TimeSpan.FromHours(6));
            Factory.Clock.SetUtcNow(at.ToUniversalTime());
            await ReauthenticateAsync(branch.Admin, branch.AdminLogin);
            foreach (var s in stays)
            {
                var confirm = await branch.Admin.PostAsync($"/api/staff/boarding/stays/{s.StayId}/confirm", null);
                Assert.True(confirm.StatusCode == HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());
                var checkIn = await branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{s.StayId}/check-in", CheckInBody([s.CatId]), Json);
                Assert.True(checkIn.StatusCode == HttpStatusCode.OK, await checkIn.Content.ReadAsStringAsync());
            }

            var start = new TaskCompletionSource();
            var tasks = stays.Select(s => Task.Run(async () =>
            {
                await start.Task;
                return await branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{s.StayId}/cats/{s.CatId}/suspected-infectious", new { note = "Sneezing" }, Json);
            })).ToArray();
            start.SetResult();
            var responses = await Task.WhenAll(tasks);
            outcomes.Add(string.Join("/", responses.Select(r => (int)r.StatusCode).OrderBy(c => c)));

            var units = new List<Guid>();
            foreach (var s in stays) units.Add((await ReadAsync(await branch.Admin.GetAsync($"/api/staff/boarding/stays/{s.StayId}"))).GetProperty("unit").GetProperty("unitId").GetGuid());
            Assert.Equal(new[] { iso1, iso2 }.OrderBy(g => g), units.OrderBy(g => g));
        }

        output.WriteLine("status pairs per round: " + string.Join(", ", outcomes));
        Assert.All(outcomes, o => Assert.Equal("204/204", o));
    }

    [Fact]
    public async Task T_R2_ConfirmRacingTheWorkersLapse_WithAFullDepositOnFile_NeverCancelsTheStay()
    {
        var outcomes = new List<string>();
        for (var round = 0; round < 8; round++)
        {
            var f = await ReserveFixtureAsync(3, daysAhead: 10);
            await RecordDepositAsync(f.Branch, f.CustomerId, f.StayId, "900.00");
            Factory.Clock.Advance(TimeSpan.FromHours(49)); // past the 48 h hold
            await ReauthenticateAsync(f.Branch.Admin, f.Branch.AdminLogin);

            var start = new TaskCompletionSource();
            var confirm = Task.Run(async () => { await start.Task; return await f.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{f.StayId}/confirm", null); });
            var worker = Task.Run(async () => { await start.Task; await RunBoardingWorkCycleAsync(); });
            start.SetResult();
            var confirmResponse = await confirm;
            await worker;

            Assert.True((int)confirmResponse.StatusCode < 500, "confirm returned " + (int)confirmResponse.StatusCode + " " + await confirmResponse.Content.ReadAsStringAsync());
            var status = (await DetailAsync(f)).GetProperty("status").GetString();
            if (status == "Reserved") { await RunBoardingWorkCycleAsync(); status = (await DetailAsync(f)).GetProperty("status").GetString(); }
            outcomes.Add($"{(int)confirmResponse.StatusCode}->{status}" + (confirmResponse.IsSuccessStatusCode ? "" : " " + (await confirmResponse.Content.ReadAsStringAsync())));
            Assert.Equal("Confirmed", status);
        }

        output.WriteLine("confirm status -> final status per round: " + string.Join(", ", outcomes));
    }

    [Fact]
    public async Task T_R3_APaymentRacingTheWorkersLapse_IsNeverLost()
    {
        var outcomes = new List<string>();
        for (var round = 0; round < 8; round++)
        {
            var f = await ReserveFixtureAsync(3, daysAhead: 10);
            Factory.Clock.Advance(TimeSpan.FromHours(49));
            await ReauthenticateAsync(f.Branch.Admin, f.Branch.AdminLogin);

            var start = new TaskCompletionSource();
            var payment = Task.Run(async () =>
            {
                await start.Task;
                return await f.Branch.Admin.PostAsJsonAsync("/api/staff/billing/payments", new
                {
                    customerId = f.CustomerId, method = "CashInStore", amount = 900m, receivedOn = LocalToday.ToString("yyyy-MM-dd"),
                    collector = new { kind = "Staff", staffAccountId = f.Branch.AdminLogin.StaffAccountId },
                    earmark = new { kind = "BoardingStay", sourceId = f.StayId, label = "Boarding deposit" },
                }, Json);
            });
            var worker = Task.Run(async () => { await start.Task; await RunBoardingWorkCycleAsync(); });
            start.SetResult();
            var paymentResponse = await payment;
            await worker;
            Assert.Equal(HttpStatusCode.Created, paymentResponse.StatusCode);

            await RunBoardingWorkCycleAsync(); // a second pass settles whatever the race left
            var detail = await DetailAsync(f);
            var status = detail.GetProperty("status").GetString();
            if (status == "Cancelled")
            {
                var dashboard = await ReadAsync(await f.Branch.Admin.GetAsync("/api/staff/boarding/dashboard"));
                Assert.Contains(dashboard.GetProperty("refundDue").EnumerateArray(), r => r.GetProperty("stayId").GetGuid() == f.StayId); // the money is listed to give back
                Assert.DoesNotContain(true, detail.GetProperty("allocations").EnumerateArray().Select(a => a.GetProperty("isActive").GetBoolean()));
            }
            else
            {
                Assert.Equal("Confirmed", status);
            }

            outcomes.Add(status!);
        }

        output.WriteLine("final status per round: " + string.Join(", ", outcomes));
    }

    [Fact]
    public async Task I_H3b_ThreeConcurrentDaycareBookingsForTwoSlots_GiveTwoVisitsAndOneRefusal()
    {
        var branch = await SeedBranchAsync();
        await SetSettingAsync(branch, "boarding.daycareDailyCapacity", "2");
        var card = await branch.Admin.PostAsJsonAsync("/api/staff/boarding/daycare-rate-cards", new { hourlyRate = "200.00", dayRate = "1000.00", taxClassId = branch.TaxClassId, effectiveFrom = Now }, Json);
        Assert.Equal(HttpStatusCode.Created, card.StatusCode);
        var date = LocalToday.AddDays(2);
        var bookings = new List<(Guid CustomerId, Guid CatId)>();
        for (var i = 0; i < 3; i++)
        {
            var (_, customerId, _) = await RegisterAndSignInCustomerAsync();
            bookings.Add((customerId, await SeedVaccinatedCatAsync(customerId)));
        }

        var start = new TaskCompletionSource();
        var tasks = bookings.Select(b => Task.Run(async () =>
        {
            await start.Task;
            return await branch.Admin.PostAsJsonAsync("/api/staff/daycare", new { customerId = b.CustomerId, catId = b.CatId, localDate = date, basis = "Day", expectedHours = (int?)null }, Json);
        })).ToArray();
        start.SetResult();
        var responses = await Task.WhenAll(tasks);

        var bodies = await Task.WhenAll(responses.Select(async r => $"{(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}"));
        output.WriteLine(string.Join(Environment.NewLine, bodies));
        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var refused = responses.Single(r => r.StatusCode != HttpStatusCode.Created);
        Assert.True(refused.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.BadRequest, "refusal was " + (int)refused.StatusCode);
        Assert.Contains(BusinessViolationCodes.DaycareFull, await refused.Content.ReadAsStringAsync());
        var day = await ReadAsync(await branch.Admin.GetAsync($"/api/staff/daycare?date={date:yyyy-MM-dd}"));
        Assert.Equal(2, day.GetProperty("slotsTaken").GetInt32());
        Assert.Equal(2, day.GetProperty("visits").EnumerateArray().Select(v => v.GetProperty("slotNumber").GetInt32()).Distinct().Count()); // two different slots
    }
}
