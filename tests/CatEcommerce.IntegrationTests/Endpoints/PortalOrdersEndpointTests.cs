using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-POR-001 §16 for the Orders section (Phase D reader): the order is placed and cancelled through Ordering's own customer endpoints (§16.1 rule 2),
/// on Ordering's host and shop fixture, and the dashboard is the real <c>GET /api/portal/dashboard</c> of the same host. Every test mints its own shop
/// and customer. The portal has no re-order of its own (ADR-263 retired, owner decision 2026-10-10): it passes Orders' canReorder through, so there is no I-C* here.
/// </summary>
public sealed class PortalOrdersEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private static async Task<JsonElement> DashboardAsync(HttpClient customer)
    {
        var response = await customer.GetAsync("/api/portal/dashboard");
        await ExpectAsync(HttpStatusCode.OK, response);
        return await ReadAsync(response);
    }

    [Fact]
    public async Task I_B2_Orders_ARecentOrderIsListedWithItsTotal_AndAConfirmedOrderCannotBeReordered()
    {
        var shop = await SetUpShopAsync();
        var placed = await PlaceCodOrderAsync(shop);
        var orderId = placed.GetProperty("orderId").GetGuid();

        var board = await DashboardAsync(shop.Customer);

        var orders = board.GetProperty("orders");
        Assert.Equal("Ready", orders.GetProperty("state").GetString());
        var item = Assert.Single(orders.GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal(orderId, item.GetProperty("orderId").GetGuid());
        Assert.Equal(placed.GetProperty("orderNumber").GetString(), item.GetProperty("orderNumber").GetString());
        Assert.Equal("Confirmed", item.GetProperty("status").GetString());
        Assert.Equal(placed.GetProperty("grandTotal").GetProperty("amount").GetString(), item.GetProperty("total").GetProperty("amount").GetString());
        Assert.Equal("BDT", item.GetProperty("total").GetProperty("currencyCode").GetString());
        Assert.Equal(3, item.GetProperty("itemCount").GetInt32());
        Assert.False(item.GetProperty("canReorder").GetBoolean());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("trackingNumber").ValueKind);   // not dispatched yet
    }

    [Fact]
    public async Task Orders_CanReorderMatchesTheOrderDetailPage_AfterTheOrderIsCancelled()
    {
        var shop = await SetUpShopAsync();
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        await ExpectAsync(HttpStatusCode.NoContent, await shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{orderId}/cancel", new { }, Json));

        var board = await DashboardAsync(shop.Customer);
        var detail = await ReadAsync(await shop.Customer.GetAsync($"/api/customers/me/orders/{orderId}"));

        var item = Assert.Single(board.GetProperty("orders").GetProperty("data").GetProperty("items").EnumerateArray());
        Assert.Equal("Cancelled", item.GetProperty("status").GetString());
        Assert.True(item.GetProperty("canReorder").GetBoolean());
        Assert.Equal(detail.GetProperty("canReorder").GetBoolean(), item.GetProperty("canReorder").GetBoolean());
    }

    // I-B3 -- ADR-261 (IDOR)
    [Fact]
    public async Task I_B3_Orders_TwoCustomers_EachSeesOnlyItsOwnOrders_AndTheOtherCustomerSeesNone()
    {
        var first = await SetUpShopAsync();
        var second = await SetUpShopAsync();
        var firstOrder = (await PlaceCodOrderAsync(first)).GetProperty("orderId").GetGuid();

        var firstBoard = await DashboardAsync(first.Customer);
        var secondBoard = await DashboardAsync(second.Customer);

        Assert.Equal(firstOrder, Assert.Single(firstBoard.GetProperty("orders").GetProperty("data").GetProperty("items").EnumerateArray()).GetProperty("orderId").GetGuid());
        Assert.Equal("Ready", secondBoard.GetProperty("orders").GetProperty("state").GetString());
        Assert.Empty(secondBoard.GetProperty("orders").GetProperty("data").GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Orders_ACustomerWithNoOrders_GetsAReadySectionWithAnEmptyList()
    {
        var shop = await SetUpShopAsync(fillCart: false);

        var board = await DashboardAsync(shop.Customer);

        Assert.Equal("Ready", board.GetProperty("orders").GetProperty("state").GetString());
        Assert.Empty(board.GetProperty("orders").GetProperty("data").GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Orders_NoBankDetailsOrPaymentReferenceLeakIntoTheDashboard()
    {
        var shop = await SetUpShopAsync();
        await PlaceCodOrderAsync(shop);

        var raw = await (await shop.Customer.GetAsync("/api/portal/dashboard")).Content.ReadAsStringAsync();

        foreach (var forbidden in new[] { "paymentReference", "transferInstructions", "accountNumber", "routing", "recipientPhone", "line1" })
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
    }
}
