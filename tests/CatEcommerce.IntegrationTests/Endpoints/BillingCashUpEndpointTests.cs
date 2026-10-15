using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-BIL-001 section 16 group F (I-F1..I-F4) -- the daily till count: expected cash, card and transfer totals; a count with a
/// variance that cannot close without an explanation; late items carried into the next day; reception counts but cannot close
/// (US-BIL-12, US-BIL-06 AC3; ADR-188; OQ-12). This class moves the (fake) clock, so its factory is its own.</summary>
public sealed class BillingCashUpEndpointTests(BillingApiFactory factory) : BillingIntegrationTestBase(factory)
{
    private static string Url(DateOnly date, string suffix = "") => $"/api/staff/billing/cash-ups/{date:yyyy-MM-dd}{suffix}";

    private static async Task<JsonElement> ViewAsync(HttpClient client, DateOnly date) =>
        await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await client.GetAsync(Url(date))));

    private static Task<HttpResponseMessage> CountAsync(HttpClient client, DateOnly date, decimal counted, string? explanation = null) =>
        client.PutAsJsonAsync(Url(date, "/count"), new { countedCash = counted, explanation }, Json);

    private static Task<HttpResponseMessage> CloseAsync(HttpClient client, DateOnly date, int version) =>
        client.PostAsJsonAsync(Url(date, "/close"), new { expectedVersion = version }, Json);

    /// <summary>I-F1 -- a day with cash, staff and courier COD, card, transfer, a cash refund and a cash reversal: the expected cash is the
    /// calculator's; card and transfer are listed, not counted; courier-held COD never counts.</summary>
    [Fact]
    public async Task I_F1_TheExpectedTotals_FollowTheDay()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var admin = context.Admin;

        var firstCash = await RecordPaymentAsync(admin, customerId, "CashInStore", 500m);
        await RecordPaymentAsync(admin, customerId, "CashInStore", 500m);
        await RecordPaymentAsync(admin, customerId, "CashInStore", 500m);
        await RecordPaymentAsync(admin, customerId, "CashOnDelivery", 200m, collector: new { kind = "Staff", staffAccountId = context.AdminStaffId });
        await RecordPaymentAsync(admin, customerId, "CashOnDelivery", 900m, collector: new { kind = "Courier", courierCode = "PATHAO", courierName = "Pathao Courier" });
        await RecordPaymentAsync(admin, customerId, "CardInStore", 700m, reference: "004512");
        await RecordPaymentAsync(admin, customerId, "BankTransfer", 1000m, reference: "TRX-F1");
        var wrong = await RecordPaymentAsync(admin, customerId, "CashInStore", 50m);

        await ExpectAsync(HttpStatusCode.Created, await admin.PostAsJsonAsync("/api/staff/billing/refunds", new
        { paymentId = firstCash.GetProperty("paymentId").GetGuid(), method = "CashInStore", amount = 100m, reason = "Returned goods" }, Json));
        await ExpectAsync(HttpStatusCode.NoContent, await admin.PostAsJsonAsync(
            $"/api/staff/billing/payments/{wrong.GetProperty("paymentId").GetGuid()}/reverse", new { reason = "Entered twice" }, Json));

        var view = await ViewAsync(admin, Today);
        var expected = view.GetProperty("expected");

        Assert.Equal("Open", view.GetProperty("status").GetString());
        Assert.Equal(1700m + 50m, AmountOf(expected.GetProperty("cash")));
        Assert.Equal(700m, AmountOf(expected.GetProperty("card")));
        Assert.Equal(1000m, AmountOf(expected.GetProperty("transfer")));
        Assert.Equal(100m, AmountOf(expected.GetProperty("cashRefunds")));
        Assert.Equal(50m, AmountOf(expected.GetProperty("cashReversals")));
        Assert.Equal(1750m - 100m - 50m, AmountOf(expected.GetProperty("expectedCash")));
        Assert.Equal(0, expected.GetProperty("carriedIn").GetArrayLength());

        var lines = view.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(8 + 2, lines.Count);
        Assert.Contains(lines, l => l.GetProperty("kind").GetString() == "Refund");
        Assert.Contains(lines, l => l.GetProperty("kind").GetString() == "Reversal");
        var courier = Assert.Single(lines, l => l.GetProperty("collector").ValueKind == JsonValueKind.String && l.GetProperty("collector").GetString() == "Courier");
        Assert.False(courier.GetProperty("countsInCash").GetBoolean());
    }

    /// <summary>I-F2 -- count with a variance: closing without an explanation is a 400; with one it closes, freezes the snapshot, and the
    /// explanation is audited. A closed day cannot be counted again (US-BIL-12 AC2, US-BIL-06 AC3).</summary>
    [Fact]
    public async Task I_F2_AVariance_NeedsAnExplanation_AndTheClosedDayIsFrozen()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 1000m);

        var counted = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await CountAsync(context.Admin, Today, 990m)));
        Assert.Equal(-10m, AmountOf(counted.GetProperty("variance")));

        var refused = await CloseAsync(context.Admin, Today, counted.GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains(BusinessViolationCodes.VarianceExplanationRequired, await ErrorCodesAsync(refused));

        var explained = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await CountAsync(context.Admin, Today, 990m, "Ten taka short, change given twice")));
        await ExpectAsync(HttpStatusCode.NoContent, await CloseAsync(context.Admin, Today, explained.GetProperty("version").GetInt32()));

        var closed = await ViewAsync(context.Admin, Today);
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal(-10m, AmountOf(closed.GetProperty("variance")));
        Assert.Equal(1000m, AmountOf(closed.GetProperty("expected").GetProperty("expectedCash")));
        Assert.Equal(context.AdminStaffId, closed.GetProperty("closedBy").GetGuid());

        var recount = await CountAsync(context.Admin, Today, 1000m);
        Assert.Equal(HttpStatusCode.BadRequest, recount.StatusCode);
        Assert.Contains(BusinessViolationCodes.CashUpClosed, await ErrorCodesAsync(recount));
        var again = await CloseAsync(context.Admin, Today, closed.GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);

        var cashUpId = CatEcommerce.Domain.Aggregates.Billing.CashUpIds.For(context.BranchId, Today);
        var audit = await WithDbAsync(db => db.Set<AuditEntryAggregate>().AsNoTracking()
            .Where(a => a.EntityId == cashUpId && a.Category == AuditCategory.BusinessEvent && a.Action == "CashUpClosed").ToListAsync());
        Assert.Contains("Ten taka short, change given twice", Assert.Single(audit).AfterValue);

        // The database too: a closed cash-up cannot be changed or deleted.
        await using var connection = new NpgsqlConnection(Environment.GetEnvironmentVariable("CATECOMMERCE_TEST_DB")
            ?? "Host=localhost;Port=5432;Database=catecommerce_test;Username=postgres;Password=postgres");
        await connection.OpenAsync();
        foreach (var sql in new[] { $"UPDATE billing.cash_up SET counted_cash = 1 WHERE \"Id\" = '{cashUpId}'", $"DELETE FROM billing.cash_up WHERE \"Id\" = '{cashUpId}'" })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("P0001", error.SqlState);
        }
    }

    /// <summary>I-F3 -- a payment accepted after the day closed (same business date) is not lost: it appears, listed, as carried-in on the
    /// next day's cash-up, is frozen there, and is not carried a second time.</summary>
    [Fact]
    public async Task I_F3_ALatePayment_IsCarriedIntoTheNextDay_Once()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var day = Today;

        var counted = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await CountAsync(context.Admin, day, 0m)));
        await ExpectAsync(HttpStatusCode.NoContent, await CloseAsync(context.Admin, day, counted.GetProperty("version").GetInt32()));

        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await RecordPaymentAsync(context.Admin, customerId, "CashInStore", 200m);

        var sameDay = await ViewAsync(context.Admin, day);
        Assert.Equal(0m, AmountOf(sameDay.GetProperty("expected").GetProperty("expectedCash"))); // the closed day stays as it was frozen

        var next = day.AddDays(1);
        var nextView = await ViewAsync(context.Admin, next);
        var carried = Assert.Single(nextView.GetProperty("expected").GetProperty("carriedIn").EnumerateArray());
        Assert.Equal(200m, AmountOf(carried.GetProperty("amount")));
        Assert.Equal(200m, AmountOf(nextView.GetProperty("expected").GetProperty("expectedCash")));
        Assert.Matches(@"^R-\d{6}$", carried.GetProperty("label").GetString());

        // Closing the next day (counted in its own time) freezes the carried-in amount.
        Factory.Clock.Advance(TimeSpan.FromDays(1));
        await ReSignInAsync(context);
        var nextCounted = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await CountAsync(context.Admin, next, 200m)));
        await ExpectAsync(HttpStatusCode.NoContent, await CloseAsync(context.Admin, next, nextCounted.GetProperty("version").GetInt32()));
        var frozen = await ViewAsync(context.Admin, next);
        Assert.Equal(200m, AmountOf(frozen.GetProperty("expected").GetProperty("carriedInTotal")));

        var third = await ViewAsync(context.Admin, next.AddDays(1));
        Assert.Equal(0, third.GetProperty("expected").GetProperty("carriedIn").GetArrayLength());
        Assert.Equal(0m, AmountOf(third.GetProperty("expected").GetProperty("expectedCash")));
    }

    /// <summary>I-F4 and U-F3 -- reception counts but cannot close (OQ-12); a payment still waiting for approval blocks the close.</summary>
    [Fact]
    public async Task I_F4_ReceptionCounts_ButCannotClose_AndAPendingApprovalBlocksTheClose()
    {
        var context = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(context);
        var (reception, _) = await StaffClientAsync(context, StaffRoles.Reception);
        var (vet, _) = await StaffClientAsync(context, StaffRoles.Vet);

        var counted = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await CountAsync(reception, Today, 0m)));
        Assert.Equal(HttpStatusCode.Forbidden, (await CloseAsync(reception, Today, counted.GetProperty("version").GetInt32())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vet.GetAsync(Url(Today))).StatusCode);

        var (accountant, _) = await StaffClientAsync(context, StaffRoles.Accountant);
        await RecordPaymentAsync(accountant, customerId, "BankTransfer", 60_000m, reference: "TRX-BIG-F4");
        var view = await ViewAsync(accountant, Today);
        Assert.Equal(1, view.GetProperty("pendingApprovals").GetInt32());

        var blocked = await CloseAsync(accountant, Today, view.GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, blocked.StatusCode);
        Assert.Contains(BusinessViolationCodes.PendingApprovalsExist, await ErrorCodesAsync(blocked));

        var future = await CountAsync(context.Admin, Today.AddDays(3), 10m);
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Contains(BusinessViolationCodes.CashUpDateInFuture, await ErrorCodesAsync(future));
    }

    [Fact]
    public async Task TheCashUp_IsScopedToTheActiveBranch()
    {
        var mine = await CreateBillingContextAsync();
        var other = await CreateBillingContextAsync();
        var customerId = await SeedCustomerAsync(mine);
        await RecordPaymentAsync(mine.Admin, customerId, "CashInStore", 300m);

        Assert.Equal(300m, AmountOf((await ViewAsync(mine.Admin, Today)).GetProperty("expected").GetProperty("expectedCash")));
        Assert.Equal(0m, AmountOf((await ViewAsync(other.Admin, Today)).GetProperty("expected").GetProperty("expectedCash")));
    }
}
