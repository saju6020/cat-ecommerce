using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>HO-RPT-001 section 16 H -- I-H4 (SRS section 8.1): the order fulfilment report over real orders, one dispatched at once, one dispatched late and one still waiting, with the clock moved 25 hours.</summary>
public sealed class ReportingFulfilmentEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private async Task<Guid> CourierAsync(OrderingContext context)
    {
        var response = await ExpectAsync(HttpStatusCode.Created, await context.Admin.PostAsJsonAsync("/api/admin/couriers",
            new { name = "Courier " + Guid.NewGuid().ToString("N")[..6], trackingUrlTemplate = "https://track.example.test/t/{tracking}", supportsCod = true, isActive = true }, Json));
        return (await ReadAsync(response)).GetProperty("courierId").GetGuid();
    }

    private async Task<(Shop Shop, Guid OrderId, Guid CourierId)> PlacedAsync()
    {
        var shop = await SetUpShopAsync();
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        return (shop, orderId, await CourierAsync(shop.Context));
    }

    private async Task DispatchAsync(OrderingContext context, Guid orderId, Guid courierId)
    {
        var (reception, _) = await StaffClientAsync(context, StaffRoles.Reception);
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null));
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", new { courierId, trackingNumber = "TRK-H4" }, Json));
    }

    private async Task<JsonElement> RowAsync(OrderingContext context, string number)
    {
        var (shopManager, _) = await StaffClientAsync(context, StaffRoles.ShopManager);
        var response = await shopManager.GetAsync(
            $"/api/staff/reports/{ReportCatalogue.OrderFulfilmentKey}?from={Today.AddDays(-3):yyyy-MM-dd}&to={Today.AddDays(3):yyyy-MM-dd}&branchId={context.BranchId}");
        await ExpectAsync(HttpStatusCode.OK, response);
        return (await ReadAsync(response)).GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("orderNumber").GetString() == number);
    }

    [Fact]
    public async Task I_H4_AnOrderDispatchedPromptlyIsWithinTheServiceLevel_OneDispatchedLateOrStillWaitingBreachesIt()
    {
        var prompt = await PlacedAsync();
        var late = await PlacedAsync();
        var waiting = await PlacedAsync();
        string NumberOf(Guid orderId) => LoadOrderAsync(orderId).GetAwaiter().GetResult().OrderNumber;

        await DispatchAsync(prompt.Shop.Context, prompt.OrderId, prompt.CourierId);

        Factory.Clock.Advance(TimeSpan.FromHours(25));
        // Staff sessions idle out after 30 minutes of the fake clock: sign each branch's administrator in again.
        async Task<OrderingContext> Fresh(OrderingContext c) => c with { Admin = await SignInStaffAsync(c.AdminMobile, c.AdminPassword, c.BranchId) };
        var lateContext = await Fresh(late.Shop.Context);
        var promptContext = await Fresh(prompt.Shop.Context);
        var waitingContext = await Fresh(waiting.Shop.Context);
        await DispatchAsync(lateContext, late.OrderId, late.CourierId);

        var promptRow = await RowAsync(promptContext, NumberOf(prompt.OrderId));
        Assert.False(promptRow.GetProperty("slaBreach").GetBoolean());
        Assert.Equal("Dispatched", promptRow.GetProperty("status").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, promptRow.GetProperty("dispatchedOn").ValueKind);

        var lateRow = await RowAsync(lateContext, NumberOf(late.OrderId));
        Assert.True(lateRow.GetProperty("slaBreach").GetBoolean());
        Assert.True(lateRow.GetProperty("ageHours").GetInt64() >= 25);

        var waitingRow = await RowAsync(waitingContext, NumberOf(waiting.OrderId));
        Assert.True(waitingRow.GetProperty("slaBreach").GetBoolean());
        Assert.Equal("Confirmed", waitingRow.GetProperty("status").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, waitingRow.GetProperty("dispatchedOn").ValueKind);
    }
}
