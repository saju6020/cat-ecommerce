using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 Phase G, returns -- I-G1 to I-G6 and I-F9 through real HTTP against the real Inventory and Billing: a request inside the window (US-ORD-13 AC1), opened food and
/// the BR-10 exceptions (AC2, AC-9), approval with a confirmed defect, receipt into quarantine (US-ORD-14 AC1), the restock / write-off decision (AC2), an expired batch that
/// cannot be restocked, and a refused COD parcel coming back (US-ORD-20 AC3).
/// </summary>
public sealed class OrderingReturnsEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private sealed record Delivered(Shop Shop, Guid OrderId, HttpClient Reception, HttpClient Keeper, Domain.Aggregates.Order.OrderAggregate Order);

    private async Task<Guid> CreateCourierAsync(OrderingContext context)
    {
        var response = await context.Admin.PostAsJsonAsync("/api/admin/couriers", new { name = "Courier " + Guid.NewGuid().ToString("N")[..6], supportsCod = true, isActive = true }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return (await ReadAsync(response)).GetProperty("courierId").GetGuid();
    }

    /// <summary>A shop with a sealed-or-opened-food SKU (850.00, 15 %) and a toy (120.00, 0 %), one cash-on-delivery order of 2 x food and 1 x toy dispatched and delivered.</summary>
    private async Task<Delivered> DeliveredOrderAsync(DateOnly? foodExpires = null)
    {
        var shop = await SetUpShopAsync(fillCart: false);
        var food = await CreateSkuAsync(shop.Context, price: 850m, stock: 20m, food: true, onStorefront: true, name: "Kitten food", expires: foodExpires);
        var toy = shop.Toy;
        shop = shop with { Food = food };
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = food.SkuId, quantity = 2 }, Json));
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = toy.SkuId, quantity = 1 }, Json));
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();

        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var (keeper, _) = await StaffClientAsync(shop.Context, StaffRoles.Inventory);
        var courierId = await CreateCourierAsync(shop.Context);
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null));
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", new { courierId, trackingNumber = "TRK-RET-1" }, Json));
        var order = await LoadOrderAsync(orderId);
        await ExpectAsync(HttpStatusCode.NoContent, await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery",
            new { expectedVersion = order.Version, deliveredOn = Now.AddMinutes(-1), codCollection = new { amount = order.Totals.GrandTotal, collectorKind = "Courier", collectorName = "Rider Rahim", collectedOn = Now.AddMinutes(-2) } }));

        return new Delivered(shop, orderId, reception, keeper, await LoadOrderAsync(orderId));
    }

    private static object ReturnBody(Guid lineId, int quantity, string reason = "ChangedMind", string condition = "Sealed") =>
        new { lines = new[] { new { orderLineId = lineId, quantity, reason, condition, customerNote = (string?)null } } };

    private static Guid LineOf(Delivered d, Guid skuId) => d.Order.Lines.Single(l => l.SkuId == skuId).Id;

    // ── I-G1 (US-ORD-13 AC1) ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_G1_ASealedFoodItemWithinTheWindow_IsRequested_AndStaffApproveIt()
    {
        var d = await DeliveredOrderAsync();
        var foodLine = LineOf(d, d.Shop.Food.SkuId);

        var detailBefore = await ReadAsync(await d.Shop.Customer.GetAsync($"/api/customers/me/orders/{d.OrderId}"));
        Assert.True(detailBefore.GetProperty("canRequestReturn").GetBoolean());
        Assert.Equal(2, detailBefore.GetProperty("returnableLines").EnumerateArray().Single(l => l.GetProperty("lineId").GetGuid() == foodLine).GetProperty("maxQuantity").GetInt32());

        var response = await ExpectAsync(HttpStatusCode.Created, await d.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", ReturnBody(foodLine, 1), Json));
        var created = await ReadAsync(response);
        Assert.Equal("Requested", created.GetProperty("status").GetString());
        var returnId = created.GetProperty("returnRequestId").GetGuid();

        var mine = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await d.Shop.Customer.GetAsync($"/api/customers/me/returns/{returnId}")));
        Assert.Equal("Kitten food", mine.GetProperty("lines")[0].GetProperty("productName").GetString());

        // the quantity left to return shrinks while a request is open; asking for more than is left is refused
        var detail = await ReadAsync(await d.Shop.Customer.GetAsync($"/api/customers/me/orders/{d.OrderId}"));
        Assert.Equal(1, detail.GetProperty("returnableLines").EnumerateArray().Single(l => l.GetProperty("lineId").GetGuid() == foodLine).GetProperty("maxQuantity").GetInt32());
        await AssertErrorAsync(await d.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", ReturnBody(foodLine, 2), Json), "ReturnQuantityExceeded");

        await ExpectAsync(HttpStatusCode.NoContent, await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/approve", new { lines = Array.Empty<object>() }, Json));
        var staff = await ReadAsync(await d.Reception.GetAsync($"/api/staff/returns/{returnId}"));
        Assert.Equal("Approved", staff.GetProperty("status").GetString());
        Assert.Equal(1, await NotificationCountAsync(d.Shop.CustomerId, "order.return-decided"));

        // a return that is no longer Requested cannot be approved or rejected again
        await AssertErrorAsync(await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/reject", new { note = "No" }, Json), "InvalidReturnTransition");
    }

    [Fact]
    public async Task ARejectedReturn_IsClosedToThePolicy_AndTheCustomerIsTold()
    {
        var d = await DeliveredOrderAsync();
        var returnId = (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await d.Shop.Customer.PostAsJsonAsync(
            $"/api/customers/me/orders/{d.OrderId}/returns", ReturnBody(LineOf(d, d.Shop.Toy.SkuId), 1), Json)))).GetProperty("returnRequestId").GetGuid();

        await ExpectAsync(HttpStatusCode.NoContent, await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/reject", new { note = "Item shows signs of use" }, Json));

        var staff = await ReadAsync(await d.Reception.GetAsync($"/api/staff/returns/{returnId}"));
        Assert.Equal("Rejected", staff.GetProperty("status").GetString());
        Assert.Equal("Item shows signs of use", staff.GetProperty("decisionNote").GetString());
        Assert.Equal(1, await NotificationCountAsync(d.Shop.CustomerId, "order.return-decided"));
        await AssertErrorAsync(await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/receive", new { lines = new[] { new { orderLineId = LineOf(d, d.Shop.Toy.SkuId), quantityReceived = 1 } } }, Json), "InvalidReturnTransition");
    }

    [Fact]
    public async Task TheReturnWindow_ClosesAfterTheConfiguredDays_AndAnotherCustomersOrderIsNotFound()
    {
        var d = await DeliveredOrderAsync();
        var toyLine = LineOf(d, d.Shop.Toy.SkuId);

        await using (var connection = await OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE ordering.sales_order SET delivered_on = now() - interval '9 days' WHERE \"Id\" = @id";
            command.Parameters.AddWithValue("id", d.OrderId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        var late = await d.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", ReturnBody(toyLine, 1), Json);
        await AssertErrorAsync(late, "ReturnWindowClosed");
        Assert.Contains("lines[0]", (await late.Content.ReadAsStringAsync()), StringComparison.OrdinalIgnoreCase);
        var detail = await ReadAsync(await d.Shop.Customer.GetAsync($"/api/customers/me/orders/{d.OrderId}"));
        Assert.False(detail.GetProperty("canRequestReturn").GetBoolean());

        var (stranger, _, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", ReturnBody(toyLine, 1), Json)).StatusCode);
    }

    // ── I-G2 (US-ORD-13 AC2, AC-9, BR-10) ───────────────────────────────────────────────────────────
    [Fact]
    public async Task I_G2_OpenedFoodBoughtOnAWhim_IsRefused_UntilTheBatchIsRecalled()
    {
        var d = await DeliveredOrderAsync();
        var foodLine = LineOf(d, d.Shop.Food.SkuId);
        var body = ReturnBody(foodLine, 1, "ChangedMind", "Opened");

        var refused = await d.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", body, Json);
        await AssertErrorAsync(refused, "ReturnNotEligibleOpenedFood");
        Assert.Equal(0, await WithDbAsync(db => db.ReturnRequests.AsNoTracking().CountAsync(r => r.OrderId == d.OrderId)));

        // the batch is recalled: the same request is eligible outright
        var allocation = d.Order.Lines.Single(l => l.Id == foodLine).Allocations.Single();
        await ExpectAsync(HttpStatusCode.Created, await d.Keeper.PostAsJsonAsync($"/api/staff/inventory/batches/{allocation.BatchId}/recall",
            new { confirmLotNumber = allocation.LotNumber, reason = "Contamination.", customerAdvice = "Stop feeding it." }, Json));

        var accepted = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await d.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", body, Json)));
        var staff = await ReadAsync(await d.Reception.GetAsync($"/api/staff/returns/{accepted.GetProperty("returnRequestId").GetGuid()}"));
        var line = staff.GetProperty("lines")[0];
        Assert.False(line.GetProperty("requiresDefectConfirmation").GetBoolean());   // the recall is the evidence
        Assert.True(line.GetProperty("anyBatchRecalled").GetBoolean());
    }

    [Fact]
    public async Task UsedLitterAndAMixedRequest_AreRefusedWhole_WithAReasonPerLine()
    {
        var d = await DeliveredOrderAsync();
        var foodLine = LineOf(d, d.Shop.Food.SkuId);
        var toyLine = LineOf(d, d.Shop.Toy.SkuId);

        // an opened toy is fine; the opened food (changed mind) is not -> the whole request is refused, naming line 1
        var response = await d.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", new
        {
            lines = new object[]
            {
                new { orderLineId = toyLine, quantity = 1, reason = "ChangedMind", condition = "Opened", customerNote = (string?)null },
                new { orderLineId = foodLine, quantity = 1, reason = "ChangedMind", condition = "Opened", customerNote = (string?)null },
            }
        }, Json);

        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ReturnNotEligibleOpenedFood", text);
        Assert.Contains("Lines[1]", text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await WithDbAsync(db => db.ReturnRequests.AsNoTracking().CountAsync(r => r.OrderId == d.OrderId)));

        Assert.Equal(HttpStatusCode.BadRequest, (await d.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", ReturnBody(toyLine, 1, "NotAReason"), Json)).StatusCode);
    }

    // ── I-G3 (BR-10, the defect path) ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_G3_OpenedFoodClaimedDefective_IsRequested_AndApprovalNeedsTheDefectConfirmed()
    {
        var d = await DeliveredOrderAsync();
        var foodLine = LineOf(d, d.Shop.Food.SkuId);

        var created = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await d.Shop.Customer.PostAsJsonAsync(
            $"/api/customers/me/orders/{d.OrderId}/returns", ReturnBody(foodLine, 1, "QualityDefect", "Opened"), Json)));
        var returnId = created.GetProperty("returnRequestId").GetGuid();

        var staff = await ReadAsync(await d.Reception.GetAsync($"/api/staff/returns/{returnId}"));
        Assert.True(staff.GetProperty("lines")[0].GetProperty("requiresDefectConfirmation").GetBoolean());

        await AssertErrorAsync(await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/approve", new { lines = Array.Empty<object>() }, Json), "DefectConfirmationRequired");
        await AssertErrorAsync(await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/approve",
            new { lines = new[] { new { orderLineId = foodLine, defectConfirmed = true, defectNote = "" } } }, Json), "DefectConfirmationRequired");
        Assert.Equal("Requested", (await ReadAsync(await d.Reception.GetAsync($"/api/staff/returns/{returnId}"))).GetProperty("status").GetString());

        await ExpectAsync(HttpStatusCode.NoContent, await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/approve",
            new { lines = new[] { new { orderLineId = foodLine, defectConfirmed = true, defectNote = "Mould visible in the bag" } } }, Json));
        var approved = await ReadAsync(await d.Reception.GetAsync($"/api/staff/returns/{returnId}"));
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.Equal("Mould visible in the bag", approved.GetProperty("lines")[0].GetProperty("defectNote").GetString());
    }

    // ── I-G4 / I-G5 / I-G6 (US-ORD-14) ──────────────────────────────────────────────────────────────
    private async Task<(Delivered D, Guid ReturnId)> ReceivedReturnAsync(Delivered d, int foodQuantity = 1, int toyQuantity = 1)
    {
        var foodLine = LineOf(d, d.Shop.Food.SkuId);
        var toyLine = LineOf(d, d.Shop.Toy.SkuId);
        var returnId = (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await d.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", new
        {
            lines = new object[]
            {
                new { orderLineId = foodLine, quantity = foodQuantity, reason = "WrongItem", condition = "Sealed", customerNote = (string?)null },
                new { orderLineId = toyLine, quantity = toyQuantity, reason = "Damaged", condition = "Opened", customerNote = (string?)null },
            }
        }, Json)))).GetProperty("returnRequestId").GetGuid();

        await ExpectAsync(HttpStatusCode.NoContent, await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/approve", new { lines = Array.Empty<object>() }, Json));
        await ExpectAsync(HttpStatusCode.NoContent, await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/receive", new
        { lines = new object[] { new { orderLineId = foodLine, quantityReceived = foodQuantity }, new { orderLineId = toyLine, quantityReceived = toyQuantity } } }, Json));
        return (d, returnId);
    }

    private async Task<decimal> OnHandAsync(Guid batchId, Guid locationId) =>
        await WithDbAsync(db => db.StockPositions.AsNoTracking().Where(p => p.BatchId == batchId && p.LocationId == locationId).Select(p => p.OnHand).SumAsync());

    [Fact]
    public async Task I_G4_ReceivingAnApprovedReturn_PutsItInQuarantine_NotSellable_AgainstTheOriginalBatch()
    {
        var d = await DeliveredOrderAsync();
        var foodBatch = d.Order.Lines.Single(l => l.SkuId == d.Shop.Food.SkuId).Allocations.Single().BatchId;
        var sellableBefore = await SellableAsync(d.Shop.Context.BranchId, d.Shop.Food.SkuId);

        var (_, returnId) = await ReceivedReturnAsync(d);

        Assert.Equal(1m, await OnHandAsync(foodBatch, d.Shop.Context.QuarantineLocationId));
        Assert.Equal(sellableBefore, await SellableAsync(d.Shop.Context.BranchId, d.Shop.Food.SkuId));   // quarantine is never sellable
        var staff = await ReadAsync(await d.Reception.GetAsync($"/api/staff/returns/{returnId}"));
        Assert.Equal("Received", staff.GetProperty("status").GetString());
        Assert.Equal(1, staff.GetProperty("lines")[0].GetProperty("quantityReceived").GetInt32());

        var order = await LoadOrderAsync(d.OrderId);
        Assert.Equal(1, order.Lines.Single(l => l.SkuId == d.Shop.Food.SkuId).QuantityReturned);
        Assert.Equal(OrderStatus.Delivered, order.Status);   // not every line is fully back

        // the return entry carries the party and the return reference (the recall trace reads it)
        var entry = await WithDbAsync(db => db.StockLedgerEntries.AsNoTracking().Where(e => e.BatchId == foodBatch && e.ReferenceKind == StockReferenceKind.CustomerReturn)
            .Select(e => new { e.CustomerId, e.Kind, e.QuantityDelta }).SingleAsync());
        Assert.Equal(d.Shop.CustomerId, entry.CustomerId);
        Assert.Equal(MovementKind.Return, entry.Kind);

        // receiving more than was approved, or twice, is refused
        await AssertErrorAsync(await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/receive",
            new { lines = new[] { new { orderLineId = LineOf(d, d.Shop.Food.SkuId), quantityReceived = 1 } } }, Json), "InvalidReturnTransition");
    }

    [Fact]
    public async Task I_G4b_ReceivingMoreThanApproved_IsRefused()
    {
        var d = await DeliveredOrderAsync();
        var foodLine = LineOf(d, d.Shop.Food.SkuId);
        var returnId = (await ReadAsync(await d.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", ReturnBody(foodLine, 1), Json))).GetProperty("returnRequestId").GetGuid();
        await ExpectAsync(HttpStatusCode.NoContent, await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/approve", new { lines = Array.Empty<object>() }, Json));

        await AssertErrorAsync(await d.Reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/receive",
            new { lines = new[] { new { orderLineId = foodLine, quantityReceived = 2 } } }, Json), "ReturnReceiptExceedsApproved");
        Assert.Equal("Approved", (await ReadAsync(await d.Reception.GetAsync($"/api/staff/returns/{returnId}"))).GetProperty("status").GetString());
    }


    // ── I-F9 (US-ORD-20 AC3) ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_F9_ARefusedCodParcel_IsReturned_TheInvoiceIsCredited_AndTheGoodsComeBackThroughQuarantine()
    {
        var shop = await SetUpShopAsync();
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var courierId = await CreateCourierAsync(shop.Context);
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null));
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", new { courierId, trackingNumber = "TRK-REF-1" }, Json));
        var dispatched = await LoadOrderAsync(orderId);

        // "Uncollected" is for pickup only
        await AssertErrorAsync(await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/delivery-failure", new { reason = "Uncollected" }, Json), "DeliveryFailureReasonInvalid");

        var failure = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/delivery-failure", new { reason = "Refused", note = "Customer not home" }, Json)));
        var returnId = failure.GetProperty("returnRequestId").GetGuid();

        var order = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Returned, order.Status);
        Assert.Equal(OrderStatus.Returned, order.Status);
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.delivery-failed"));
        var credit = await WithDbAsync(db => db.CreditNotes.AsNoTracking().Where(c => c.InvoiceId == order.InvoiceId).Select(c => c.Total).SingleAsync());
        Assert.Equal(order.Totals.GrandTotal, credit);                                  // the unpaid COD invoice is credited in full

        var ret = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await reception.GetAsync($"/api/staff/returns/{returnId}")));
        Assert.Equal("DeliveryFailure", ret.GetProperty("kind").GetString());
        Assert.Equal("Approved", ret.GetProperty("status").GetString());
        Assert.Equal(2, ret.GetProperty("lines").GetArrayLength());

        // receive everything back: into quarantine against the original batches; the order is fully returned
        var lines = dispatched.Lines.Select(l => new { orderLineId = l.Id, quantityReceived = l.Quantity }).ToArray();
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/returns/{returnId}/receive", new { lines }, Json));
        var foodBatch = dispatched.Lines.Single(l => l.SkuId == shop.Food.SkuId).Allocations.Single().BatchId;
        Assert.Equal(2m, await OnHandAsync(foodBatch, shop.Context.QuarantineLocationId));
        // disposition (restock / write-off) is Inventory's quarantine workflow, not Ordering's (OQ-19): the goods stay in quarantine and the return is Received
        Assert.Equal("Received", (await ReadAsync(await reception.GetAsync($"/api/staff/returns/{returnId}"))).GetProperty("status").GetString());
    }

    [Fact]
    public async Task TheReturnsList_IsBranchScoped_FilteredByStatus_AndNurseIsRefused()
    {
        var d = await DeliveredOrderAsync();
        var returnId = (await ReadAsync(await d.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{d.OrderId}/returns", ReturnBody(LineOf(d, d.Shop.Toy.SkuId), 1), Json))).GetProperty("returnRequestId").GetGuid();

        var list = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await d.Reception.GetAsync("/api/staff/returns?status=Requested&pageSize=100")));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == returnId);
        Assert.DoesNotContain((await ReadAsync(await d.Reception.GetAsync("/api/staff/returns?status=Closed&pageSize=100"))).GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == returnId);

        var (elsewhere, _) = await StaffClientAsync(await CreateContextAsync(), StaffRoles.Reception);
        Assert.Equal(HttpStatusCode.NotFound, (await elsewhere.GetAsync($"/api/staff/returns/{returnId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await elsewhere.PostAsJsonAsync($"/api/staff/returns/{returnId}/approve", new { lines = Array.Empty<object>() }, Json)).StatusCode);

        var (nurse, _) = await StaffClientAsync(d.Shop.Context, StaffRoles.Nurse);
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.GetAsync("/api/staff/returns")).StatusCode);

        // staff can file one on the customer's behalf
        var onBehalf = await ExpectAsync(HttpStatusCode.Created, await d.Reception.PostAsJsonAsync($"/api/staff/orders/{d.OrderId}/returns", ReturnBody(LineOf(d, d.Shop.Food.SkuId), 1), Json));
        Assert.Equal("Requested", (await ReadAsync(onBehalf)).GetProperty("status").GetString());
    }

    // ── I-G7 to I-G9: refunds (US-ORD-15) ───────────────────────────────────────────────────────────
    [Fact]
    public async Task I_G7_RefundOptions_FollowHowTheCustomerPaid_AndSuggestTheValueOfTheReturnedGoods()
    {
        var d = await DeliveredOrderAsync();
        var (_, returnId) = await ReceivedReturnAsync(d);
        var (manager, _) = await StaffClientAsync(d.Shop.Context, StaffRoles.ShopManager);

        var options = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await manager.GetAsync($"/api/staff/orders/{d.OrderId}/refund-options")));
        var methods = options.GetProperty("methods").EnumerateArray().ToDictionary(m => m.GetProperty("method").GetString()!);
        Assert.True(methods["CashInStore"].GetProperty("available").GetBoolean());
        Assert.True(methods["BankTransfer"].GetProperty("available").GetBoolean());
        Assert.True(methods["StoreCredit"].GetProperty("allowed").GetBoolean());
        Assert.False(methods["StoreCredit"].GetProperty("available").GetBoolean());

        // 1 x 850.00 food (+15 %) and 1 x 120.00 toy came back; the suggestion never includes the delivery charge
        var suggestion = options.GetProperty("suggestedForReturns").EnumerateArray().Single(s => s.GetProperty("returnRequestId").GetGuid() == returnId);
        var order = await LoadOrderAsync(d.OrderId);
        var suggested = AmountOf(suggestion.GetProperty("suggestedAmount"));
        Assert.True(suggested > 0m && suggested < order.Totals.GrandTotal);

        // Nurse has no Refunds.Issue
        var (nurse, _) = await StaffClientAsync(d.Shop.Context, StaffRoles.Nurse);
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.GetAsync($"/api/staff/orders/{d.OrderId}/refund-options")).StatusCode);
    }

    [Fact]
    public async Task I_G8_ACashRefund_NamesTheReceipt_CreditsTheInvoice_AndCannotExceedWhatWasPaid()
    {
        var d = await DeliveredOrderAsync();
        var (_, returnId) = await ReceivedReturnAsync(d);
        var (manager, _) = await StaffClientAsync(d.Shop.Context, StaffRoles.ShopManager);
        var options = await ReadAsync(await manager.GetAsync($"/api/staff/orders/{d.OrderId}/refund-options"));
        var suggested = AmountOf(options.GetProperty("suggestedForReturns").EnumerateArray().Single().GetProperty("suggestedAmount"));
        var path = $"/api/staff/orders/{d.OrderId}/refunds";

        // validation: the receipt, the method, the amount and a card number in the reference
        await AssertErrorAsync(await StaffPostAsync(manager, path, new { returnRequestId = returnId, method = "CashInStore", amount = suggested, reason = "Returned goods" }), "RefundReferenceRequired");
        await AssertErrorAsync(await StaffPostAsync(manager, path, new { returnRequestId = returnId, method = "StoreCredit", amount = suggested, reason = "Returned goods" }), "RefundMethodUnavailable");
        await AssertErrorAsync(await StaffPostAsync(manager, path, new { returnRequestId = returnId, method = "CashInStore", amount = 99999999m, reference = "RCPT-1", reason = "Too much" }), "RefundExceedsAmountPaid");
        Assert.Equal(HttpStatusCode.BadRequest, (await StaffPostAsync(manager, path, new { method = "CashInStore", amount = 0m, reference = "RCPT-1", reason = "Zero" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StaffPostAsync(manager, path, new { method = "Cheque", amount = 10m, reference = "RCPT-1", reason = "Bad method" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StaffPostAsync(manager, path, new { method = "BankTransfer", amount = 10m, reference = "4111 1111 1111 1111", reason = "Card number" })).StatusCode);

        var created = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await StaffPostAsync(manager, path,
            new { returnRequestId = returnId, method = "CashInStore", amount = suggested, reference = "RCPT-7781", reason = "Returned goods" })));
        var creditNoteId = created.GetProperty("creditNoteId").GetGuid();

        var order = await LoadOrderAsync(d.OrderId);
        Assert.Equal(suggested, order.AmountRefunded);
        Assert.Contains(creditNoteId, order.CreditNoteIds);
        var credited = await WithDbAsync(db => db.CreditNotes.AsNoTracking().Where(c => c.Id == creditNoteId).Select(c => c.Total).SingleAsync());
        Assert.Equal(suggested, credited);
        Assert.Equal(1, await NotificationCountAsync(d.Shop.CustomerId, "order.refund-issued"));
        Assert.True(await WithDbAsync(db => db.AuditEntries.AsNoTracking().CountAsync(a => a.EntityId == d.OrderId && a.Action == "RefundIssued")) >= 1);

        // a second refund may only take what is left
        var left = order.AmountPaid - order.AmountRefunded;
        await AssertErrorAsync(await StaffPostAsync(manager, path, new { method = "CashInStore", amount = left + 0.01m, reference = "RCPT-7782", reason = "Over" }), "RefundExceedsAmountPaid");
    }

    [Fact]
    public async Task I_G9_ARefundIsIdempotentAtTheEdge_AndNotVisibleToAnotherBranch()
    {
        var d = await DeliveredOrderAsync();
        await ReceivedReturnAsync(d);
        var (manager, _) = await StaffClientAsync(d.Shop.Context, StaffRoles.ShopManager);
        var path = $"/api/staff/orders/{d.OrderId}/refunds";
        var key = Guid.NewGuid();
        var body = new { method = "BankTransfer", amount = 50m, reference = "TRX-9001", reason = "Goodwill" };

        async Task<HttpResponseMessage> SendAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: Json) };
            request.Headers.Add("Idempotency-Key", key.ToString());
            return await manager.SendAsync(request);
        }

        var first = await ExpectAsync(HttpStatusCode.Created, await SendAsync());
        var replay = await SendAsync();
        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal((await ReadAsync(first)).GetProperty("refundId").GetGuid(), (await ReadAsync(replay)).GetProperty("refundId").GetGuid());
        Assert.Equal(1, await WithDbAsync(db => db.OrderRefunds.AsNoTracking().CountAsync(r => r.OrderId == d.OrderId)));
        Assert.Equal(50m, (await LoadOrderAsync(d.OrderId)).AmountRefunded);

        var (elsewhere, _) = await StaffClientAsync(await CreateContextAsync(), StaffRoles.ShopManager);
        Assert.Equal(HttpStatusCode.NotFound, (await elsewhere.GetAsync($"/api/staff/orders/{d.OrderId}/refund-options")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await StaffPostAsync(elsewhere, path, new { method = "BankTransfer", amount = 5m, reference = "TRX-9002", reason = "x" })).StatusCode);
    }
}
