using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 Phase F -- fulfilment through real HTTP against the real Inventory and Billing: I-F1 (the status walk), I-F2 (the pick list), I-F3 (the packing
/// slip), I-F4 and I-F5 (dispatch, tracking and the manifest), I-F6 and I-F7 (a recalled batch: BR-14, the override and the recall trace), I-F8 (cash on
/// delivery), I-F10 and I-F11 (the courier webhook), I-F12 (pickup, pay on collection) and the staff queue and detail. The parcel-refused return of I-F9 is in
/// the returns tests (phase G).
/// </summary>
public sealed class OrderingFulfilmentEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private async Task<Guid> CreateCourierAsync(OrderingContext context, string name = "Test Courier", string? template = "https://track.example.test/t/{tracking}")
    {
        var response = await context.Admin.PostAsJsonAsync("/api/admin/couriers", new { name = name + " " + Guid.NewGuid().ToString("N")[..6], trackingUrlTemplate = template, supportsCod = true, isActive = true }, Json);
        await ExpectAsync(HttpStatusCode.Created, response);
        return (await ReadAsync(response)).GetProperty("courierId").GetGuid();
    }

    private async Task<(Shop Shop, Guid OrderId, HttpClient Reception, Guid CourierId)> PackingCodOrderAsync()
    {
        var shop = await SetUpShopAsync();
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var courierId = await CreateCourierAsync(shop.Context);
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null));
        return (shop, orderId, reception, courierId);
    }

    private static object DispatchBody(Guid courierId, string? tracking = null) => new { courierId, trackingNumber = tracking };

    private object Cod(decimal amount, string collector = "Rider Rahim") => new { amount, collectorKind = "Courier", collectorName = collector, collectedOn = Now.AddMinutes(-2) };

    // ── I-F1 (US-ORD-09) ────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_F1_AnOrderWalksConfirmedPackingDispatchedDelivered_EachStepAttributed_AndAnInvalidMoveIsRefused()
    {
        var shop = await SetUpShopAsync();
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (reception, receptionId) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var courierId = await CreateCourierAsync(shop.Context);

        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null));
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(courierId, "TRK-100"), Json));
        var version = (await LoadOrderAsync(orderId)).Version;
        await ExpectAsync(HttpStatusCode.NoContent, await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery",
            new { expectedVersion = version, deliveredOn = Now.AddMinutes(-1), codCollection = Cod(2135.00m) }));

        var detail = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await reception.GetAsync($"/api/staff/orders/{orderId}")));
        Assert.Equal("Delivered", detail.GetProperty("status").GetString());
        var history = detail.GetProperty("statusHistory").EnumerateArray().ToList();
        Assert.Equal(new[] { "Confirmed", "Packing", "Dispatched", "Delivered" }, history.Select(h => h.GetProperty("to").GetString()));
        Assert.Equal(receptionId, history[1].GetProperty("changedBy").GetGuid());   // timestamped and attributed
        Assert.NotNull(history[3].GetProperty("changedByName").GetString());

        // the customer's own history shows when, never who
        var mine = await ReadAsync(await shop.Customer.GetAsync($"/api/customers/me/orders/{orderId}"));
        Assert.Equal(4, mine.GetProperty("statusHistory").GetArrayLength());

        // an invalid move names both states
        var again = await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null);
        await AssertErrorAsync(again, "InvalidOrderTransition");
        Assert.Contains("delivered", await again.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.dispatched"));
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.delivered"));
    }

    [Fact]
    public async Task APickupOrderCannotBeDispatched_AndOnlyFulfilRolesMayPack()
    {
        var shop = await SetUpShopAsync();
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (inventory, _) = await StaffClientAsync(shop.Context, StaffRoles.Inventory);
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var courierId = await CreateCourierAsync(shop.Context);

        Assert.Equal(HttpStatusCode.Forbidden, (await inventory.PostAsync($"/api/staff/orders/{orderId}/packing", null)).StatusCode);
        await AssertErrorAsync(await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(courierId), Json), "InvalidOrderTransition"); // not packed yet
        Assert.Equal(HttpStatusCode.NotFound, (await reception.PostAsync($"/api/staff/orders/{Guid.NewGuid()}/packing", null)).StatusCode);
    }

    // ── I-F2 (US-ORD-10 AC1) ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_F2_ThePickList_GroupsByLocationThenBatch_WithTheQuantityEachOrderNeeds()
    {
        var shop = await SetUpShopAsync(fillCart: false);
        var sku = await CreateSkuAsync(shop.Context, price: 100m, stock: 0m, onStorefront: false);
        await StockAtStorefrontAsync(sku.SkuId, 30m);
        var second = await CreateLocationAsync(shop.Context.Admin, "ShopFloor");
        var batchA = await ReceiveAsync(shop.Context.Admin, sku.SkuId, shop.Context.ShopLocationId, 3m, UtcToday.AddDays(100));   // expires first -> FEFO takes it first
        var batchB = await ReceiveAsync(shop.Context.Admin, sku.SkuId, second, 4m, UtcToday.AddDays(200));

        var orderIds = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = sku.SkuId, quantity = 2 }, Json));
            orderIds.Add((await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid());
        }

        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var orders = await WithDbAsync(db => db.Orders.AsNoTracking().Where(o => orderIds.Contains(o.Id)).Select(o => new { o.Id, o.OrderNumber }).ToListAsync());

        var pick = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await reception.GetAsync("/api/staff/fulfilment/pick-list?orderIds=" + string.Join(',', orderIds))));
        var groups = pick.GetProperty("groups").EnumerateArray().ToList();
        Assert.Equal(2, groups.Count);

        var first = groups.Single(g => g.GetProperty("locationId").GetGuid() == shop.Context.ShopLocationId);
        var firstBatch = Assert.Single(first.GetProperty("batches").EnumerateArray());
        Assert.Equal(batchA, firstBatch.GetProperty("batchId").GetGuid());
        Assert.Equal(3m, firstBatch.GetProperty("quantity").GetDecimal());       // order 1 takes 2, order 2 takes the last 1
        Assert.Equal(2, firstBatch.GetProperty("orders").GetArrayLength());

        var other = groups.Single(g => g.GetProperty("locationId").GetGuid() == second);
        var otherBatch = Assert.Single(other.GetProperty("batches").EnumerateArray());
        Assert.Equal(batchB, otherBatch.GetProperty("batchId").GetGuid());
        Assert.Equal(3m, otherBatch.GetProperty("quantity").GetDecimal());       // order 2 takes 1, order 3 takes 2
        Assert.Equal(new[] { 1, 2 }, otherBatch.GetProperty("orders").EnumerateArray().Select(o => o.GetProperty("quantity").GetInt32()).OrderBy(q => q));
        Assert.All(orders, o => Assert.Contains(o.OrderNumber, pick.ToString()));

        // with no ids: every confirmed and packing order of the branch
        var all = await ReadAsync(await reception.GetAsync("/api/staff/fulfilment/pick-list"));
        Assert.Equal(2, all.GetProperty("groups").GetArrayLength());

        // Inventory staff read it (Appendix D); a nurse may not
        var (inventory, _) = await StaffClientAsync(shop.Context, StaffRoles.Inventory);
        var (nurse, _) = await StaffClientAsync(shop.Context, StaffRoles.Nurse);
        Assert.Equal(HttpStatusCode.OK, (await inventory.GetAsync("/api/staff/fulfilment/pick-list")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.GetAsync("/api/staff/fulfilment/pick-list")).StatusCode);
    }

    // ── I-F3 (US-ORD-10 AC2) ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_F3_ThePackingSlip_ShowsTheContentsWithBatchNumbers_TheCustomerAndTheCodAmount()
    {
        var shop = await SetUpShopAsync();
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var order = await LoadOrderAsync(orderId);

        var slip = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await reception.GetAsync($"/api/staff/orders/{orderId}/packing-slip")));
        Assert.Equal(order.OrderNumber, slip.GetProperty("orderNumber").GetString());
        Assert.Equal(order.Fulfilment.RecipientName, slip.GetProperty("recipientName").GetString());
        Assert.Contains(shop.District, slip.GetProperty("addressText").GetString());
        Assert.Equal("2135.00", slip.GetProperty("codAmountDue").GetProperty("amount").GetString());
        var lines = slip.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        var foodLine = order.Lines.Single(l => l.SkuId == shop.Food.SkuId);
        Assert.Equal(foodLine.Allocations[0].LotNumber, lines.Single(l => l.GetProperty("productName").GetString() == "Royal Canin Kitten 2 kg").GetProperty("batchNumbers")[0].GetString());

        var (elsewhere, _) = await StaffClientAsync(await CreateContextAsync(), StaffRoles.Reception);
        Assert.Equal(HttpStatusCode.NotFound, (await elsewhere.GetAsync($"/api/staff/orders/{orderId}/packing-slip")).StatusCode);
    }

    // ── I-F4 / I-F5 (US-ORD-11) ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_F4_DispatchWithACourierAndTracking_ShowsTheNumberAndTheCourierLinkToTheCustomer()
    {
        var (shop, orderId, reception, courierId) = await PackingCodOrderAsync();

        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(courierId, "TRK 42/7"), Json));

        var detail = await ReadAsync(await shop.Customer.GetAsync($"/api/customers/me/orders/{orderId}"));
        var shipment = detail.GetProperty("shipment");
        Assert.Equal("TRK 42/7", shipment.GetProperty("trackingNumber").GetString());
        Assert.StartsWith("https://track.example.test/t/TRK%2042%2F7", shipment.GetProperty("trackingUrl").GetString());   // the number is encoded, it cannot break out of the template

        // the list item carries the tracking number and the same canReorder the detail answers (additive, owner decision 2026-10-10)
        var listed = Assert.Single((await ReadAsync(await shop.Customer.GetAsync("/api/customers/me/orders"))).GetProperty("items").EnumerateArray());
        Assert.Equal("TRK 42/7", listed.GetProperty("trackingNumber").GetString());
        Assert.Equal(detail.GetProperty("canReorder").GetBoolean(), listed.GetProperty("canReorder").GetBoolean());
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.dispatched"));

        // the batches left the shelf: the reservation became the sale (sellable is unchanged, the stamp names the customer)
        var sale = await WithDbAsync(db => db.StockLedgerEntries.AsNoTracking().Where(e => e.ReferenceId == orderId && e.SkuId == shop.Food.SkuId)
            .Select(e => new { e.CustomerId, e.QuantityDelta, e.ConsumesReservation }).SingleAsync());
        Assert.Equal(shop.CustomerId, sale.CustomerId);
        Assert.Equal(-2m, sale.QuantityDelta);
        Assert.True(sale.ConsumesReservation);
    }

    [Fact]
    public async Task I_F5_DispatchWithoutTracking_TheManifestListsTheCodAmount_AndTheNumberAddedLaterIsAnnouncedOnce()
    {
        var (shop, orderId, reception, courierId) = await PackingCodOrderAsync();
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(courierId), Json));

        var csv = await ExpectAsync(HttpStatusCode.OK, await reception.GetAsync($"/api/staff/fulfilment/manifest.csv?courierId={courierId}&date={Today:yyyy-MM-dd}"));
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var body = await csv.Content.ReadAsStringAsync();
        var order = await LoadOrderAsync(orderId);
        Assert.Contains(order.OrderNumber, body);
        Assert.Contains("\"2135.00\"", body);                                                  // the COD amount to collect
        Assert.Equal(2, body.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);   // header + the one order

        // another courier / another date lists nothing
        var other = await (await reception.GetAsync($"/api/staff/fulfilment/manifest.csv?courierId={Guid.NewGuid()}&date={Today:yyyy-MM-dd}")).Content.ReadAsStringAsync();
        Assert.Single(other.Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
        Assert.Equal(HttpStatusCode.BadRequest, (await reception.GetAsync($"/api/staff/fulfilment/manifest.csv?courierId={courierId}&date=yesterday")).StatusCode);

        await ExpectAsync(HttpStatusCode.NoContent, await reception.PutAsJsonAsync($"/api/staff/orders/{orderId}/tracking", new { trackingNumber = "TRK-LATE-1" }, Json));
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PutAsJsonAsync($"/api/staff/orders/{orderId}/tracking", new { trackingNumber = "TRK-LATE-2" }, Json));
        Assert.Equal("TRK-LATE-2", (await LoadOrderAsync(orderId)).Shipment!.TrackingNumber);
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.tracking-added"));   // only the first number is announced
    }

    [Fact]
    public async Task TheManifest_GuardsAgainstSpreadsheetFormulas()
    {
        var shop = await SetUpShopAsync();
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var courierId = await CreateCourierAsync(shop.Context);

        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null));
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(courierId, "=HYPERLINK(\"http://x\")"), Json));

        var body = await (await reception.GetAsync($"/api/staff/fulfilment/manifest.csv?courierId={courierId}&date={Today:yyyy-MM-dd}")).Content.ReadAsStringAsync();
        Assert.Contains("\"'=HYPERLINK(", body);
        Assert.DoesNotContain(",\"=HYPERLINK", body);
    }

    // ── I-F6 / I-F7 (BR-14, US-IMP-11, US-IMP-07 AC2) ───────────────────────────────────────────────
    [Fact]
    public async Task I_F6_ARecalledBatchBlocksDispatch_UntilStaffReallocateWithAReason_AndTheRecallTraceNamesTheOrder()
    {
        var shop = await SetUpShopAsync(fillCart: false);
        var sku = await CreateSkuAsync(shop.Context, price: 100m, stock: 0m);
        await StockAtStorefrontAsync(sku.SkuId, 30m);
        var recalledBatch = await ReceiveAsync(shop.Context.Admin, sku.SkuId, shop.Context.ShopLocationId, 2m, UtcToday.AddDays(100), "LOT-RECALL-" + Guid.NewGuid().ToString("N")[..6]);
        var goodBatch = await ReceiveAsync(shop.Context.Admin, sku.SkuId, shop.Context.ShopLocationId, 5m, UtcToday.AddDays(200));
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = sku.SkuId, quantity = 1 }, Json));
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();

        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var (keeper, _) = await StaffClientAsync(shop.Context, StaffRoles.Inventory);
        var courierId = await CreateCourierAsync(shop.Context);
        var order = await LoadOrderAsync(orderId);
        var line = order.Lines.Single();
        Assert.Equal(recalledBatch, line.Allocations.Single().BatchId);               // FEFO chose the batch that expires first
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null));

        // the batch is recalled while the order is being packed
        var lot = line.Allocations.Single().LotNumber;
        await ExpectAsync(HttpStatusCode.Created, await keeper.PostAsJsonAsync($"/api/staff/inventory/batches/{recalledBatch}/recall",
            new { confirmLotNumber = lot, reason = "Salmonella found in the lot.", customerAdvice = "Stop feeding it." }, Json));

        await AssertErrorAsync(await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(courierId), Json), "BatchRecalled");
        Assert.Equal(OrderStatus.Packing, (await LoadOrderAsync(orderId)).Status);

        // the override needs a reason, a usable batch and the Reallocate action
        await AssertErrorAsync(await keeper.PostAsJsonAsync($"/api/staff/orders/{orderId}/lines/{line.Id}/reallocate", new { targetBatchId = goodBatch, reason = "" }, Json), "ReallocationReasonRequired");
        Assert.Equal(HttpStatusCode.Forbidden, (await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/lines/{line.Id}/reallocate", new { targetBatchId = goodBatch, reason = "Recalled lot" }, Json)).StatusCode);
        await ExpectAsync(HttpStatusCode.NoContent, await keeper.PostAsJsonAsync($"/api/staff/orders/{orderId}/lines/{line.Id}/reallocate", new { targetBatchId = goodBatch, reason = "Recalled lot replaced" }, Json));

        var moved = await LoadOrderAsync(orderId);
        Assert.Equal(goodBatch, moved.Lines.Single().Allocations.Single().BatchId);
        Assert.True(moved.IsFullyAllocated);

        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(courierId, "TRK-OK"), Json));
        Assert.Equal(OrderStatus.Dispatched, (await LoadOrderAsync(orderId)).Status);

        // I-F7: Inventory's recall trace reads the stamp this slice wrote -- the dispatch of the replacement batch, and the batch it left
        var traceOfGood = await ReadAsync(await keeper.GetAsync($"/api/staff/inventory/recalls/{recalledBatch}/trace"));
        Assert.Equal(0, traceOfGood.GetProperty("received").GetArrayLength());     // nothing of the recalled batch was ever dispatched
    }

    [Fact]
    public async Task I_F7_TheRecallTrace_ListsTheOrderCustomerAndCat_ForABatchThatShipped_AndFlagsAnUnshippedOrder()
    {
        var shop = await SetUpShopAsync(fillCart: false);
        var sku = await CreateSkuAsync(shop.Context, price: 100m, stock: 0m);
        await StockAtStorefrontAsync(sku.SkuId, 30m);
        var batch = await ReceiveAsync(shop.Context.Admin, sku.SkuId, shop.Context.ShopLocationId, 10m, UtcToday.AddDays(100), "LOT-TRACE-" + Guid.NewGuid().ToString("N")[..6]);
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        var (keeper, _) = await StaffClientAsync(shop.Context, StaffRoles.Inventory);
        var courierId = await CreateCourierAsync(shop.Context);

        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = sku.SkuId, quantity = 1 }, Json));
        var shipped = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{shipped}/packing", null));
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{shipped}/dispatch", DispatchBody(courierId), Json));

        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = sku.SkuId, quantity = 1 }, Json));
        var unshipped = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();

        var lot = (await LoadOrderAsync(shipped)).Lines.Single().Allocations.Single().LotNumber;
        await ExpectAsync(HttpStatusCode.Created, await keeper.PostAsJsonAsync($"/api/staff/inventory/batches/{batch}/recall", new { confirmLotNumber = lot, reason = "Contamination.", customerAdvice = "Stop using it." }, Json));

        var trace = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await keeper.GetAsync($"/api/staff/inventory/recalls/{batch}/trace")));
        var received = Assert.Single(trace.GetProperty("received").EnumerateArray());
        Assert.Equal("Sale", received.GetProperty("kind").GetString());
        Assert.Equal((await LoadOrderAsync(shipped)).OrderNumber, received.GetProperty("referenceLabel").GetString());
        var unshippedRow = Assert.Single(trace.GetProperty("unshippedDispatchBlocked").EnumerateArray());
        Assert.Equal(unshipped, unshippedRow.GetProperty("referenceId").GetGuid());
    }

    // ── I-F8 (US-ORD-20 AC2, AC-8) ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_F8_CodDelivery_RecordsTheCollectorAndTheCash_InBilling_AndOnTheOrder()
    {
        var (shop, orderId, reception, courierId) = await PackingCodOrderAsync();
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(courierId, "TRK-COD-1"), Json));
        var order = await LoadOrderAsync(orderId);

        // delivering without the cash is refused; so is the wrong amount
        await AssertErrorAsync(await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery", new { expectedVersion = order.Version, deliveredOn = Now.AddMinutes(-1) }), "CodCollectionRequired");
        await AssertErrorAsync(await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery",
            new { expectedVersion = order.Version, deliveredOn = Now.AddMinutes(-1), codCollection = Cod(2000m) }), "CollectionPaymentMismatch");
        Assert.Equal(OrderStatus.Dispatched, (await LoadOrderAsync(orderId)).Status);

        await ExpectAsync(HttpStatusCode.NoContent, await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery",
            new { expectedVersion = order.Version, deliveredOn = Now.AddMinutes(-1), codCollection = Cod(2135.00m, "Rider Rahim") }));

        var done = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Delivered, done.Status);
        Assert.Equal(2135.00m, done.AmountPaid);
        Assert.Equal("Rider Rahim", done.CodCollection!.CollectorName);

        var payment = await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.Id == done.Payments.Single().PaymentId)
            .Select(p => new { p.Method, p.Amount, p.Collector, p.RecordedBy, p.ExternalReference }).SingleAsync());
        Assert.Equal(PaymentMethod.CashOnDelivery, payment.Method);
        Assert.Equal(2135.00m, payment.Amount);
        Assert.Equal("Rider Rahim", payment.Collector!.CourierName);
        Assert.Equal("TRK-COD-1", payment.ExternalReference);                            // the tracking number is the reference for remittance matching
        Assert.Equal(2135.00m, await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == done.InvoiceId).Select(i => i.PaidTotal).SingleAsync()));

        var detail = await ReadAsync(await reception.GetAsync($"/api/staff/orders/{orderId}"));
        Assert.Equal("Rider Rahim", detail.GetProperty("codCollection").GetProperty("collectorName").GetString());
    }

    // ── I-F10 / I-F11 (US-ORD-18 AC2, COM-3) ────────────────────────────────────────────────────────
    private async Task<(Guid OrderId, string Tracking)> DispatchedWithTrackingAsync()
    {
        var (shop, orderId, reception, courierId) = await PackingCodOrderAsync();
        var tracking = "TRK-WH-" + Guid.NewGuid().ToString("N")[..10];
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(courierId, tracking), Json));
        return (orderId, tracking);
    }

    private static HttpRequestMessage Webhook(string body, string? signature = null, string provider = "testcourier")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/webhooks/couriers/{provider}") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add(OrderingWebhook.Header, signature ?? OrderingWebhook.Sign(body));
        return request;
    }

    [Fact]
    public async Task I_F10_AnEventDeliveredTwice_IsProcessedOnce_AndADeliveredReportDoesNotDeliverTheOrder()
    {
        var (orderId, tracking) = await DispatchedWithTrackingAsync();
        var eventId = "evt-" + Guid.NewGuid().ToString("N");
        var occurred = Now.AddMinutes(-3);
        var body = JsonSerializer.Serialize(new { eventId, trackingNumber = tracking, status = "Out for delivery", occurredOn = occurred });

        using var anonymous = CreateClient();
        await ExpectAsync(HttpStatusCode.OK, await anonymous.SendAsync(Webhook(body)));
        await ExpectAsync(HttpStatusCode.OK, await anonymous.SendAsync(Webhook(body)));

        var order = await LoadOrderAsync(orderId);
        Assert.Equal("Out for delivery", order.Shipment!.LastTrackingStatus);
        Assert.Equal(1, await WithDbAsync(db => db.InboundWebhookReceipts.AsNoTracking().CountAsync(r => r.Provider == "testcourier" && r.EventId == eventId)));

        // an older event never overwrites a newer one
        var older = JsonSerializer.Serialize(new { eventId = "evt-" + Guid.NewGuid().ToString("N"), trackingNumber = tracking, status = "At depot", occurredOn = occurred.AddMinutes(-30) });
        await ExpectAsync(HttpStatusCode.OK, await anonymous.SendAsync(Webhook(older)));
        Assert.Equal("Out for delivery", (await LoadOrderAsync(orderId)).Shipment!.LastTrackingStatus);

        // "delivered" is recorded on the shipment but a person must still record the cash (US-ORD-20 AC2)
        var delivered = JsonSerializer.Serialize(new { eventId = "evt-" + Guid.NewGuid().ToString("N"), trackingNumber = tracking, status = "Delivered", occurredOn = occurred.AddMinutes(10) });
        await ExpectAsync(HttpStatusCode.OK, await anonymous.SendAsync(Webhook(delivered)));
        var after = await LoadOrderAsync(orderId);
        Assert.Equal("Delivered", after.Shipment!.LastTrackingStatus);
        Assert.Equal(OrderStatus.Dispatched, after.Status);

        // an unknown tracking number is accepted and ignored
        var unknown = JsonSerializer.Serialize(new { eventId = "evt-" + Guid.NewGuid().ToString("N"), trackingNumber = "NOPE-" + Guid.NewGuid().ToString("N"), status = "x", occurredOn = occurred });
        await ExpectAsync(HttpStatusCode.OK, await anonymous.SendAsync(Webhook(unknown)));
    }

    [Fact]
    public async Task I_F11_ABadSignature_IsRefused_NothingIsWritten_AndAnUnknownProviderIsNotFound()
    {
        var (orderId, tracking) = await DispatchedWithTrackingAsync();
        var eventId = "evt-" + Guid.NewGuid().ToString("N");
        var body = JsonSerializer.Serialize(new { eventId, trackingNumber = tracking, status = "Out for delivery", occurredOn = Now });
        using var anonymous = CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(Webhook(body, signature: OrderingWebhook.Sign(body + " ")))).StatusCode);   // tampered body
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.SendAsync(Webhook(body, signature: "deadbeef"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.SendAsync(Webhook(body, provider: "no-such-courier"))).StatusCode);

        Assert.Null((await LoadOrderAsync(orderId)).Shipment!.LastTrackingStatus);
        Assert.Equal(0, await WithDbAsync(db => db.InboundWebhookReceipts.AsNoTracking().CountAsync(r => r.EventId == eventId)));

        // a signed body that is not a tracking event is a 400, and still records nothing
        var junk = "{\"hello\":1}";
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.SendAsync(Webhook(junk))).StatusCode);
    }

    // ── I-F12 (pickup, pay on collection) ───────────────────────────────────────────────────────────
    [Fact]
    public async Task I_F12_APickupOrderPaidOnCollection_IsReadyThenCollectedWithACardSlip_AndTheStockLeavesAtCollection()
    {
        var shop = await SetUpShopAsync();
        await AcceptPendingPoliciesAsync(shop.Customer);
        var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop, "Pickup", "PayOnCollection", pickupBranchId: shop.Context.BranchId));
        var placed = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await shop.Customer.SendAsync(PostOrder(
            PlaceBody(shop, quote, "Pickup", "PayOnCollection", pickupBranchId: shop.Context.BranchId)))));
        var orderId = placed.GetProperty("orderId").GetGuid();
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);

        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/packing", null));
        await AssertErrorAsync(await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(await CreateCourierAsync(shop.Context)), Json), "InvalidOrderTransition"); // a pickup is not dispatched
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsync($"/api/staff/orders/{orderId}/ready-for-collection", null));

        var ready = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Dispatched, ready.Status);
        Assert.NotNull(ready.CollectByDate);
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.ready-for-collection"));
        Assert.Equal(0, await WithDbAsync(db => db.StockLedgerEntries.AsNoTracking().CountAsync(e => e.ReferenceId == orderId)));   // still only reserved

        var amount = ready.Totals.GrandTotal;
        await AssertErrorAsync(await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery", new { expectedVersion = ready.Version, deliveredOn = Now.AddMinutes(-1) }), "CollectionPaymentMismatch");
        await AssertErrorAsync(await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery",
            new { expectedVersion = ready.Version, deliveredOn = Now.AddMinutes(-1), collectionPayment = new { method = "InStoreCard", amount } }), "TerminalReferenceRequired");
        await AssertErrorAsync(await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery",
            new { expectedVersion = ready.Version, deliveredOn = Now.AddMinutes(-1), collectionPayment = new { method = "InStoreCard", amount, reference = "4111 1111 1111 1111" } }), "ReferenceLooksLikeCardNumber");

        await ExpectAsync(HttpStatusCode.NoContent, await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery",
            new { expectedVersion = ready.Version, deliveredOn = Now.AddMinutes(-1), collectionPayment = new { method = "InStoreCard", amount, reference = "SLIP-77123" } }));

        var done = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Delivered, done.Status);
        Assert.Equal(amount, done.AmountPaid);
        var payment = await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.Id == done.Payments.Single().PaymentId).Select(p => new { p.Method, p.ExternalReference }).SingleAsync());
        Assert.Equal(PaymentMethod.CardInStore, payment.Method);
        Assert.Equal("SLIP-77123", payment.ExternalReference);
        Assert.True(await WithDbAsync(db => db.StockLedgerEntries.AsNoTracking().AnyAsync(e => e.ReferenceId == orderId && e.ConsumesReservation)));   // issued at collection
    }

    // ── the staff queue and detail ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TheStaffQueue_FiltersSearchesAndFlags_AndTheDetailCarriesTheVersionAndAllowedActions()
    {
        var (shop, orderId, reception, courierId) = await PackingCodOrderAsync();
        var order = await LoadOrderAsync(orderId);
        var recipient = order.Fulfilment.RecipientName;

        var queue = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await reception.GetAsync($"/api/staff/orders?status=Packing&fulfilment=Delivery&paymentMethod=CashOnDelivery&search={Uri.EscapeDataString(order.OrderNumber)}")));
        var row = Assert.Single(queue.GetProperty("items").EnumerateArray());
        Assert.Equal(orderId, row.GetProperty("id").GetGuid());
        Assert.Equal(recipient, row.GetProperty("customerName").GetString());
        Assert.True(row.GetProperty("ageMinutes").GetInt64() >= 0);

        Assert.Equal(0, (await ReadAsync(await reception.GetAsync("/api/staff/orders?status=Delivered&search=" + Uri.EscapeDataString(order.OrderNumber)))).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await reception.GetAsync("/api/staff/orders?status=Bogus")).StatusCode);

        var detail = await ReadAsync(await reception.GetAsync($"/api/staff/orders/{orderId}"));
        Assert.Equal(order.Version, detail.GetProperty("version").GetInt32());
        var allowed = detail.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()).ToList();
        Assert.Contains("Dispatch", allowed);
        Assert.Contains("Cancel", allowed);
        Assert.DoesNotContain("StartPacking", allowed);
        var line = detail.GetProperty("lines").EnumerateArray().First();
        Assert.NotEqual(string.Empty, line.GetProperty("allocations")[0].GetProperty("batchNumber").GetString());

        // an Inventory officer reads the order but is offered no fulfilment action
        var (inventory, _) = await StaffClientAsync(shop.Context, StaffRoles.Inventory);
        var inventoryView = await ReadAsync(await inventory.GetAsync($"/api/staff/orders/{orderId}"));
        Assert.DoesNotContain("Dispatch", inventoryView.GetProperty("allowedActions").EnumerateArray().Select(a => a.GetString()));

        var (nurse, _) = await StaffClientAsync(shop.Context, StaffRoles.Nurse);
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.GetAsync($"/api/staff/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.GetAsync("/api/staff/orders")).StatusCode);
    }

    // ── I-X3 (the delivered variant) ────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_X3_ADeliveredOrder_IsRedactedWhenItsCustomerIsAnonymised()
    {
        var (shop, orderId, reception, courierId) = await PackingCodOrderAsync();
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/dispatch", DispatchBody(courierId, "TRK-X3"), Json));
        await ExpectAsync(HttpStatusCode.NoContent, await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/delivery",
            new { expectedVersion = (await LoadOrderAsync(orderId)).Version, deliveredOn = Now.AddMinutes(-1), codCollection = Cod(2135.00m) }));

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<Domain.Aggregates.Account.CustomerAccountAggregate>>();
            var customer = await repository.GetByIdAsync(shop.CustomerId) ?? throw new InvalidOperationException("customer");
            customer.Anonymise(new Domain.Models.CustomerAnonymisationDto(DateTimeOffset.UtcNow, new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await repository.UpdateAsync(customer, Platform.Infrastructure.Core.Commands.EventPublishOption.InMemoryPublishOnly);
        }

        var order = await LoadOrderAsync(orderId);
        Assert.Equal("Anonymised customer", order.Fulfilment.RecipientName);
        Assert.Equal("[redacted]", order.Fulfilment.Line1);
    }
}
