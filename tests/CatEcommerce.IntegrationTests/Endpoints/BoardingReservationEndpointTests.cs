using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BRD-001 Phase D -- the deposit without a gateway: reserve, confirm from Billing's accepted payments, lapse, cancel, no-show (I-D1 .. I-D7).</summary>
// One collection: the Boarding worker pass reads every stay in the shared database, so classes with different fake clocks must not overlap.
[Collection("Boarding")]
public sealed class BoardingReservationEndpointTests(BoardingApiFactory factory) : BoardingIntegrationTestBase(factory)
{
    private sealed record Booked(BoardingBranch Branch, HttpClient Customer, Guid CustomerId, Guid StayId, DateOnly Arrival, string Mobile);

    private async Task<Booked> BookAsync(int daysAhead = 10, int nights = 4)
    {
        var branch = await SeedBranchAsync();
        await CreateUnitAsync(branch, "Standard", 1);
        await SetRateCardAsync(branch, "Standard", nightly: "1000.00");
        var (customer, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedVaccinatedCatAsync(customerId);
        var arrival = LocalToday.AddDays(daysAhead);
        var stayId = await ReserveOkAsync(customer, branch.BranchId, arrival, arrival.AddDays(nights), "Standard", cat);
        return new Booked(branch, customer, customerId, stayId, arrival, mobile);
    }

    private static async Task<JsonElement> StayAsync(HttpClient client, string prefix, Guid stayId) =>
        await ReadAsync(await client.GetAsync($"{prefix}/{stayId}"));

    [Fact]
    public async Task I_D1_Reserve_ReturnsTheDepositAndPayBy_TheBalanceDue_AndEnqueuesTheReservedMessage()
    {
        var b = await BookAsync();

        var stay = await StayAsync(b.Customer, "/api/customers/me/boarding/stays", b.StayId);

        Assert.Equal("Reserved", stay.GetProperty("status").GetString());
        Assert.Equal("1200.00", stay.GetProperty("deposit").GetProperty("required").GetProperty("amount").GetString()); // 30 % of 4 000
        Assert.Equal("Awaiting", stay.GetProperty("deposit").GetProperty("state").GetString());
        Assert.Equal("4000.00", stay.GetProperty("balanceDue").GetProperty("amount").GetString());
        Assert.NotEqual(JsonValueKind.Null, stay.GetProperty("deposit").GetProperty("dueBy").ValueKind);
        Assert.Contains(NotificationTemplateCatalogue.BoardingReserved, await NotificationKeysAsync(b.CustomerId));
    }

    [Fact]
    public async Task I_D3_StaffRecordTheDepositInBilling_ThenConfirm_AndTheBalanceDueFalls()
    {
        var b = await BookAsync();
        var early = await b.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{b.StayId}/confirm", null);
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Contains(BusinessViolationCodes.DepositNotYetReceived, await early.Content.ReadAsStringAsync());

        await RecordDepositAsync(b.Branch, b.CustomerId, b.StayId, "1200.00");
        var confirm = await b.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{b.StayId}/confirm", null);

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Equal("Confirmed", (await ReadAsync(confirm)).GetProperty("status").GetString());
        var stay = await StayAsync(b.Customer, "/api/customers/me/boarding/stays", b.StayId);
        Assert.Equal("Received", stay.GetProperty("deposit").GetProperty("state").GetString());
        Assert.Equal("2800.00", stay.GetProperty("balanceDue").GetProperty("amount").GetString());
        Assert.Contains(NotificationTemplateCatalogue.BoardingConfirmed, await NotificationKeysAsync(b.CustomerId));
    }

    [Fact]
    public async Task I_D5_ADepositRecordedFromBillingOnly_IsConfirmedByTheWorker()
    {
        var b = await BookAsync();
        await RecordDepositAsync(b.Branch, b.CustomerId, b.StayId, "1500.00");

        await RunBoardingWorkCycleAsync();

        var stay = await StayAsync(b.Customer, "/api/customers/me/boarding/stays", b.StayId);
        Assert.Equal("Confirmed", stay.GetProperty("status").GetString());
    }

    [Fact]
    public async Task I_D2_PastTheHoldWindow_AnUnpaidReservationLapses_TheUnitIsFree_AndTheOwnerIsTold()
    {
        var b = await BookAsync();

        Factory.Clock.Advance(TimeSpan.FromHours(49));
        await RunBoardingWorkCycleAsync();

        var stay = await StayAsync(b.Customer, "/api/customers/me/boarding/stays", b.StayId);
        Assert.Equal("Cancelled", stay.GetProperty("status").GetString());
        var availability = await ReadAsync(await CreateClient().GetAsync(
            $"/api/boarding/availability?branchId={b.Branch.BranchId}&arrivalOn={b.Arrival:yyyy-MM-dd}&departureOn={b.Arrival.AddDays(4):yyyy-MM-dd}&catCount=1"));
        Assert.Equal(1, availability.GetProperty("unitTypes")[0].GetProperty("freeUnitCount").GetInt32());
        Assert.Contains(NotificationTemplateCatalogue.BoardingReservationLapsed, await NotificationKeysAsync(b.CustomerId));
    }

    [Fact]
    public async Task I_D4_APaymentAwaitingASecondApprover_SuspendsTheLapse_AndTheWorkerConfirmsAfterApproval()
    {
        var b = await BookAsync();
        var lower = await b.Branch.Admin.PutAsJsonAsync($"/api/admin/settings?branchId={b.Branch.BranchId}",
            new { changes = new[] { new { key = "billing.paymentApprovalThreshold", value = "100.00" } } }, Json);
        Assert.Equal(HttpStatusCode.NoContent, lower.StatusCode);
        var paymentId = await RecordDepositAsync(b.Branch, b.CustomerId, b.StayId, "1200.00", "BankTransfer");

        Factory.Clock.Advance(TimeSpan.FromHours(49));
        await ReauthenticateAsync(b.Branch.Admin, b.Branch.AdminLogin);
        await RunBoardingWorkCycleAsync();
        var held = await StayAsync(b.Customer, "/api/customers/me/boarding/stays", b.StayId);
        Assert.Equal("Reserved", held.GetProperty("status").GetString());
        Assert.Equal("PendingApproval", held.GetProperty("deposit").GetProperty("state").GetString());

        var approver = await SeedStaffAsync(StaffRoles.Accountant);
        var assign = await b.Branch.Admin.PutAsJsonAsync($"/api/admin/staff/{approver.StaffAccountId}/branches", new { branchIds = new[] { b.Branch.BranchId }, primaryBranchId = b.Branch.BranchId }, Json);
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);
        var accountant = await SignInStaffAsync(approver, b.Branch.BranchId);
        var approve = await accountant.PostAsync($"/api/staff/billing/payments/{paymentId}/approve", null);
        Assert.True(approve.IsSuccessStatusCode, await approve.Content.ReadAsStringAsync());

        await RunBoardingWorkCycleAsync();
        var confirmed = await StayAsync(b.Customer, "/api/customers/me/boarding/stays", b.StayId);
        Assert.Equal("Confirmed", confirmed.GetProperty("status").GetString());
    }

    [Fact]
    public async Task I_D6_CancellingFiveDaysOut_OwesTheDepositBack_ButOneDayOut_ForfeitsIt()
    {
        var early = await BookAsync(daysAhead: 10);
        var late = await BookAsync(daysAhead: 10);

        var outside = await early.Customer.PostAsJsonAsync($"/api/customers/me/boarding/stays/{early.StayId}/cancel", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, outside.StatusCode);
        Assert.False((await ReadAsync(outside)).GetProperty("depositForfeited").GetBoolean());

        Factory.Clock.Advance(TimeSpan.FromDays(9));
        await ReauthenticateCustomerAsync(late.Customer, late.Mobile);
        var inside = await late.Customer.PostAsJsonAsync($"/api/customers/me/boarding/stays/{late.StayId}/cancel", new { }, Json);
        Assert.Equal(HttpStatusCode.OK, inside.StatusCode);
        Assert.True((await ReadAsync(inside)).GetProperty("depositForfeited").GetBoolean());
        Assert.Contains(NotificationTemplateCatalogue.BoardingCancelled, await NotificationKeysAsync(late.CustomerId));
    }

    [Fact]
    public async Task StaffNeedANoteToCancel_AndANoShowIsRefusedBeforeTheGraceHasPassed()
    {
        var b = await BookAsync();
        await RecordDepositAsync(b.Branch, b.CustomerId, b.StayId, "1200.00");
        await RunBoardingWorkCycleAsync();

        var noNote = await b.Branch.Admin.PostAsJsonAsync($"/api/staff/boarding/stays/{b.StayId}/cancel", new { }, Json);
        var early = await b.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{b.StayId}/no-show", null);

        Assert.Equal(HttpStatusCode.BadRequest, noNote.StatusCode);
        Assert.Contains(BusinessViolationCodes.CancellationReasonRequired, await noNote.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, early.StatusCode);
        Assert.Contains(BusinessViolationCodes.NoShowTooEarly, await early.Content.ReadAsStringAsync());

        // An absolute instant (the day after arrival, 15:00 Dhaka): the shared fake clock's time of day depends on the tests that ran before this one.
        Factory.Clock.SetUtcNow(new DateTimeOffset(b.Arrival.Year, b.Arrival.Month, b.Arrival.Day, 15, 0, 0, TimeSpan.FromHours(6)).AddDays(1).ToUniversalTime());
        await ReauthenticateAsync(b.Branch.Admin, b.Branch.AdminLogin);
        var noShow = await b.Branch.Admin.PostAsync($"/api/staff/boarding/stays/{b.StayId}/no-show", null);
        Assert.Equal(HttpStatusCode.NoContent, noShow.StatusCode);
        var stay = await StayAsync(b.Branch.Admin, "/api/staff/boarding/stays", b.StayId);
        Assert.Equal("NoShow", stay.GetProperty("status").GetString());
        Assert.True(stay.GetProperty("depositForfeited").GetBoolean());
    }

    [Fact]
    public async Task I_D7_NoBoardingTable_HasAPaymentCardNumberOrTokenColumn()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerce.Infrastructure.Persistence.CatEcommerceDbContext>();

        var columns = await db.Database.SqlQuery<string>($"SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_schema = 'boarding'").ToListAsync();

        Assert.NotEmpty(columns);
        Assert.DoesNotContain(columns, c => new[] { "card_number", "cvv", "token", "pan", "payment_method", "gateway" }.Any(bad => c.Contains(bad, StringComparison.OrdinalIgnoreCase)));
    }
}
