using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 Phase E -- the bank-transfer path and cancellation through real HTTP against the real Billing and Inventory: I-E1 (see the placement tests), I-E2,
/// I-E3 (a second person for large amounts), I-E4 and I-E5 (expiry by the worker), I-E6 (order again), I-E8 to I-E11 (cancellation) and the card-number rule
/// (C-5). The Billing payment-stage failure of I-E7 and the worker isolation of U-X1 are in <see cref="OrderingPaymentFaultEndpointTests"/>.
/// </summary>
public sealed class OrderingPaymentEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private async Task<(Shop Shop, Guid OrderId, HttpClient Reception)> TransferOrderAsync(bool fillCart = true)
    {
        var shop = await SetUpShopAsync(fillCart: fillCart);
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var placed = await PlaceTransferOrderAsync(shop);
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        return (shop, placed.GetProperty("orderId").GetGuid(), reception);
    }

    // ── I-E2 (US-ORD-19 AC2, US-ORD-08 AC1) ─────────────────────────────────────────────────────────
    [Fact]
    public async Task I_E2_TheCustomerReportsAReference_StaffConfirmTheExactAmount_AndTheOrderIsPaidInvoicedAndConfirmed()
    {
        var (shop, orderId, reception) = await TransferOrderAsync();

        await ExpectAsync(HttpStatusCode.NoContent, await shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{orderId}/transfer-reference", new { reference = "TXN-998877" }, Json));
        Assert.Equal("TXN-998877", (await LoadOrderAsync(orderId)).CustomerTransferReference);

        // the awaiting-payment list shows it with the customer's reference
        var awaiting = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await reception.GetAsync("/api/staff/orders/awaiting-payment")));
        var row = awaiting.EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == orderId);
        Assert.Equal("TXN-998877", row.GetProperty("customerTransferReference").GetString());

        var before = await LoadOrderAsync(orderId);
        var confirm = await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(reception, orderId, before.Version, 2135.00m, "BANK-REF-1"));
        var body = await ReadAsync(confirm);
        Assert.Equal("Confirmed", body.GetProperty("status").GetString());
        Assert.False(body.GetProperty("awaitingApproval").GetBoolean());

        var order = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(2135.00m, order.AmountPaid);
        Assert.NotNull(order.InvoiceId);
        var payment = Assert.Single(order.Payments);
        Assert.Equal("BankTransfer", payment.Method);

        // Billing recorded the payment (method, reference, the recorder) and allocated it to the invoice, which is therefore Paid
        var billing = await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.Id == payment.PaymentId)
            .Select(p => new { p.Amount, p.Status, p.RecordedBy, p.ExternalReference }).SingleAsync());
        Assert.Equal(2135.00m, billing.Amount);
        Assert.Equal("BANK-REF-1", billing.ExternalReference);
        Assert.Equal(await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == order.InvoiceId).Select(i => i.PaidTotal).SingleAsync()), 2135.00m);

        // the customer was told, and the reservation is still held until dispatch
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.confirmed"));
        Assert.Equal(48m, await SellableAsync(shop.Context.BranchId, shop.Food.SkuId));
    }

    [Fact]
    public async Task AnAmountThatIsNotExactlyTheTotal_IsRefused_AndNothingIsWritten()
    {
        var (shop, orderId, reception) = await TransferOrderAsync();
        var order = await LoadOrderAsync(orderId);

        await AssertErrorAsync(await ConfirmTransferAsync(reception, orderId, order.Version, 2134.99m), "TransferAmountMismatch");
        await AssertErrorAsync(await ConfirmTransferAsync(reception, orderId, order.Version, 2135.01m), "TransferAmountMismatch");

        var after = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.PendingPayment, after.Status);
        Assert.Equal(0m, after.AmountPaid);
        Assert.Equal(0, await WithDbAsync(db => db.Charges.AsNoTracking().CountAsync(c => c.CustomerId == shop.CustomerId)));
    }

    [Fact]
    public async Task AStaleVersion_IsAConflict_AndOnlyConfirmPaymentRolesMayConfirm()
    {
        var (shop, orderId, reception) = await TransferOrderAsync();
        var order = await LoadOrderAsync(orderId);

        await AssertErrorAsync(await ConfirmTransferAsync(reception, orderId, order.Version + 7, 2135.00m), "StaleVersion", HttpStatusCode.Conflict);

        var (inventory, _) = await StaffClientAsync(shop.Context, StaffRoles.Inventory);
        Assert.Equal(HttpStatusCode.Forbidden, (await ConfirmTransferAsync(inventory, orderId, order.Version, 2135.00m)).StatusCode);
        var (nurse, _) = await StaffClientAsync(shop.Context, StaffRoles.Nurse);
        Assert.Equal(HttpStatusCode.Forbidden, (await ConfirmTransferAsync(nurse, orderId, order.Version, 2135.00m)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.GetAsync("/api/staff/orders/awaiting-payment")).StatusCode);
    }

    [Fact]
    public async Task AnotherBranchsStaff_DoNotSeeThisOrder()
    {
        var (shop, orderId, _) = await TransferOrderAsync();
        var elsewhere = await CreateContextAsync();
        var (reception, _) = await StaffClientAsync(elsewhere, StaffRoles.Reception);
        var order = await LoadOrderAsync(orderId);

        Assert.Equal(HttpStatusCode.NotFound, (await ConfirmTransferAsync(reception, orderId, order.Version, 2135.00m)).StatusCode);
        var awaiting = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await reception.GetAsync("/api/staff/orders/awaiting-payment")));
        Assert.DoesNotContain(awaiting.EnumerateArray(), o => o.GetProperty("id").GetGuid() == orderId);
    }

    // ── I-E3 (FR-BIL-12, BR-17) ─────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_E3_AnAmountAboveTheApprovalThreshold_IsHeld_TheSamePersonCannotApprove_AndAnotherCan()
    {
        var (shop, orderId, _) = await TransferOrderAsync();
        await SetApprovalThresholdAsync(shop.Context, "1000.00");
        var (manager, managerId) = await StaffClientAsync(shop.Context, StaffRoles.Administrator);   // may record AND approve; the second person must differ (Billing enforces it)
        var (shopManager, _) = await StaffClientAsync(shop.Context, StaffRoles.ShopManager);
        var (accountant, accountantId) = await StaffClientAsync(shop.Context, StaffRoles.Accountant);
        var order = await LoadOrderAsync(orderId);

        var first = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(manager, orderId, order.Version, 2135.00m, "BANK-HELD-1")));
        Assert.True(first.GetProperty("awaitingApproval").GetBoolean());
        Assert.Equal("PendingPayment", first.GetProperty("status").GetString());

        var held = await LoadOrderAsync(orderId);
        Assert.NotNull(held.HeldPayment);
        Assert.Equal(managerId, held.HeldPayment!.RecordedBy);
        // owner decision 4: the payment is recorded in Billing at once, under the person who entered it, and waits there for a second person; the invoice exists but is unpaid
        var pending = await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.CustomerId == shop.CustomerId).Select(p => new { p.Id, p.RecordedBy, p.Status }).SingleAsync());
        Assert.Equal(managerId, pending.RecordedBy);
        Assert.Equal(CatEcommerce.Shared.Common.Enums.PaymentStatus.PendingApproval, pending.Status);
        Assert.Equal(held.HeldPayment.PaymentId, pending.Id);
        Assert.Equal(0m, await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == held.InvoiceId).Select(i => i.PaidTotal).SingleAsync()));

        // a second confirmation while one waits is refused
        await AssertErrorAsync(await ConfirmTransferAsync(manager, orderId, held.Version, 2135.00m), "PaymentAlreadyAwaitingApproval");

        // the person who recorded it cannot approve it ...
        await AssertErrorAsync(await StaffPostAsync(manager, $"/api/staff/orders/{orderId}/payment/approve", new { expectedVersion = held.Version }), "SecondApproverMustDiffer");
        Assert.Equal(OrderStatus.PendingPayment, (await LoadOrderAsync(orderId)).Status);

        // ... a shop manager no longer holds the approve action either (it follows Billing's Payments.Approve: Accountant and Administrator) ...
        Assert.Equal(HttpStatusCode.Forbidden, (await StaffPostAsync(shopManager, $"/api/staff/orders/{orderId}/payment/approve", new { expectedVersion = held.Version })).StatusCode);

        // ... and a role without the approve action cannot even try
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        Assert.Equal(HttpStatusCode.Forbidden, (await StaffPostAsync(reception, $"/api/staff/orders/{orderId}/payment/approve", new { expectedVersion = held.Version })).StatusCode);

        // the held confirmation is visible to the approver
        var awaiting = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await accountant.GetAsync("/api/staff/orders/awaiting-payment")));
        var row = awaiting.EnumerateArray().Single(o => o.GetProperty("id").GetGuid() == orderId);
        Assert.Equal("2135.00", row.GetProperty("heldPayment").GetProperty("amount").GetProperty("amount").GetString());

        // a different person approves: recorded-by the first, approved-by the second
        var approve = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await StaffPostAsync(accountant, $"/api/staff/orders/{orderId}/payment/approve", new { expectedVersion = held.Version })));
        Assert.Equal("Confirmed", approve.GetProperty("status").GetString());

        var done = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Confirmed, done.Status);
        Assert.Null(done.HeldPayment);
        var payment = await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.Id == done.Payments[0].PaymentId).Select(p => new { p.RecordedBy, p.ApprovedBy, p.Status }).SingleAsync());
        Assert.Equal(managerId, payment.RecordedBy);
        Assert.Equal(accountantId, payment.ApprovedBy);
        Assert.Equal(2135.00m, await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == done.InvoiceId).Select(i => i.PaidTotal).SingleAsync()));
    }

    [Fact]
    public async Task ARejectedHeldConfirmation_IsCleared_AndTheOrderStillAwaitsItsTransfer()
    {
        var (shop, orderId, _) = await TransferOrderAsync();
        await SetApprovalThresholdAsync(shop.Context, "1000.00");
        var (manager, _) = await StaffClientAsync(shop.Context, StaffRoles.ShopManager);
        var (accountant, _) = await StaffClientAsync(shop.Context, StaffRoles.Accountant);
        var order = await LoadOrderAsync(orderId);
        await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(manager, orderId, order.Version, 2135.00m));

        Assert.Equal(HttpStatusCode.BadRequest, (await accountant.PostAsJsonAsync($"/api/staff/orders/{orderId}/payment/reject", new { note = "" }, Json)).StatusCode); // a note is required
        await ExpectAsync(HttpStatusCode.NoContent, await accountant.PostAsJsonAsync($"/api/staff/orders/{orderId}/payment/reject", new { note = "Not on the bank statement" }, Json));

        var after = await LoadOrderAsync(orderId);
        Assert.Null(after.HeldPayment);
        Assert.Equal(OrderStatus.PendingPayment, after.Status);
        await AssertErrorAsync(await accountant.PostAsJsonAsync($"/api/staff/orders/{orderId}/payment/reject", new { note = "again" }, Json), "NoPaymentAwaitingApproval");

        // Billing's pending payment was rejected through its own route (by the second person, as themselves); the invoice stays on the order
        var rejected = await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.CustomerId == shop.CustomerId).Select(p => new { p.Status, p.RejectedBy }).SingleAsync());
        Assert.Equal(CatEcommerce.Shared.Common.Enums.PaymentStatus.Rejected, rejected.Status);
        Assert.NotNull(after.InvoiceId);

        // entering the transfer again (now at or below the threshold) reuses that invoice, records a new payment and confirms the order
        await SetApprovalThresholdAsync(shop.Context, "100000.00");
        await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(manager, orderId, after.Version, 2135.00m, "BANK-OK-2"));
        var confirmed = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Confirmed, confirmed.Status);
        Assert.Equal(after.InvoiceId, confirmed.InvoiceId);
        Assert.Equal(2135.00m, await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == confirmed.InvoiceId).Select(i => i.PaidTotal).SingleAsync()));
        Assert.Equal(1, await WithDbAsync(db => db.Invoices.AsNoTracking().CountAsync(i => i.CustomerId == shop.CustomerId)));
    }

    // ── I-E4 / I-E5 (BR-16, US-ORD-19 AC3) ──────────────────────────────────────────────────────────
    [Fact]
    public async Task I_E4_AfterTheWindow_TheWorkerExpiresTheOrder_ReleasesTheStockAndTheDiscount_AndTellsTheCustomer()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var discount = await CreateDiscountAsync(shop.Context, percent: 10m, usageLimit: 5);
        var placed = await PlaceTransferOrderAsync(shop, discount.Code);
        var orderId = placed.GetProperty("orderId").GetGuid();
        Assert.Equal(48m, await SellableAsync(shop.Context.BranchId, shop.Food.SkuId));
        Assert.Equal(1, await WithDbAsync(db => db.DiscountCodes.AsNoTracking().Where(c => c.Id == discount.Id).Select(c => c.TimesRedeemed).SingleAsync()));

        // just before the window: not expired
        Factory.Clock.Advance(TimeSpan.FromHours(47));
        await RunOrderWorkCycleAsync(shop.Context.BranchId);
        Assert.Equal(OrderStatus.PendingPayment, (await LoadOrderAsync(orderId)).Status);

        Factory.Clock.Advance(TimeSpan.FromHours(2));
        await RunOrderWorkCycleAsync(shop.Context.BranchId);

        var order = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(OrderCancellationReason.PaymentWindowExpired, order.CancellationReason);
        Assert.Equal(50m, await SellableAsync(shop.Context.BranchId, shop.Food.SkuId));           // the stock is sellable again
        Assert.Equal(0, await WithDbAsync(db => db.DiscountCodes.AsNoTracking().Where(c => c.Id == discount.Id).Select(c => c.TimesRedeemed).SingleAsync()));
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.expired"));

        // the customer's order page offers to order again
        var detail = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await shop.Customer.GetAsync($"/api/customers/me/orders/{orderId}")));
        Assert.True(detail.GetProperty("canReorder").GetBoolean());
        Assert.False(detail.GetProperty("canCancel").GetBoolean());
        Assert.Equal("Cancelled", detail.GetProperty("status").GetString());

        // running the worker again changes nothing (idempotent)
        await RunOrderWorkCycleAsync(shop.Context.BranchId);
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.expired"));
    }

    [Fact]
    public async Task I_E5_AHeldConfirmation_IsNotExpired_EvenPastTheDueTime()
    {
        var (shop, orderId, reception) = await TransferOrderAsync();
        await SetApprovalThresholdAsync(shop.Context, "1000.00");
        var order = await LoadOrderAsync(orderId);
        await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(reception, orderId, order.Version, 2135.00m));

        Factory.Clock.Advance(TimeSpan.FromHours(49));
        await RunOrderWorkCycleAsync(shop.Context.BranchId);

        Assert.Equal(OrderStatus.PendingPayment, (await LoadOrderAsync(orderId)).Status);
        Assert.Equal(0, await NotificationCountAsync(shop.CustomerId, "order.expired"));
    }

    // ── I-E6 (US-ORD-19 AC3) ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_E6_OrderAgain_PutsTheAvailableLinesBackInTheCart_AndListsWhatIsGone()
    {
        var (shop, orderId, _) = await TransferOrderAsync();

        // the toy is archived meanwhile and the food's price moves (done before the clock jump: a 49-hour jump would end the staff session)
        await ExpectAsync(HttpStatusCode.NoContent, await shop.Context.Admin.PostAsJsonAsync(
            $"/api/staff/shop/products/{shop.Toy.ProductId}/archive", new { expectedVersion = await ProductVersionAsync(shop.Context.Admin, shop.Toy.ProductId) }, Json));
        await SchedulePriceAsync(shop.Context.Admin, shop.Food.SkuId, 900m);

        Factory.Clock.Advance(TimeSpan.FromHours(49));
        await RunOrderWorkCycleAsync(shop.Context.BranchId);
        Assert.Equal(OrderStatus.Cancelled, (await LoadOrderAsync(orderId)).Status);
        var response = await shop.Customer.SendAsync(StaffReorder(orderId));
        var result = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, response));
        Assert.Equal(1, result.GetProperty("addedCount").GetInt32());
        var unavailable = Assert.Single(result.GetProperty("unavailable").EnumerateArray());
        Assert.Equal("Feather wand", unavailable.GetProperty("productName").GetString());
        Assert.Equal("NotPurchasable", unavailable.GetProperty("reason").GetString());

        var cart = await ReadAsync(await shop.Customer.GetAsync("/api/cart"));
        var line = Assert.Single(cart.GetProperty("lines").EnumerateArray());
        Assert.Equal(shop.Food.SkuId, line.GetProperty("skuId").GetGuid());
        Assert.Equal(2, line.GetProperty("quantity").GetInt32());
        Assert.Equal("900.00", line.GetProperty("unitPrice").GetProperty("amount").GetString());     // today's price, not the order's
        Assert.Equal(1, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == shop.CustomerId)));   // no order was created
    }

    private static HttpRequestMessage StaffReorder(Guid orderId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/customers/me/orders/{orderId}/reorder");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return request;
    }

    // ── I-E8 / I-E9 / I-E10 / I-E11 (US-ORD-12) ─────────────────────────────────────────────────────
    [Fact]
    public async Task I_E8_ACustomerCancelsAConfirmedCodOrder_TheStockIsReleased_AndTheInvoiceIsCreditedInFull()
    {
        var shop = await SetUpShopAsync();
        var placed = await PlaceCodOrderAsync(shop);
        var orderId = placed.GetProperty("orderId").GetGuid();
        var order = await LoadOrderAsync(orderId);
        Assert.Equal(48m, await SellableAsync(shop.Context.BranchId, shop.Food.SkuId));

        var detail = await ReadAsync(await shop.Customer.GetAsync($"/api/customers/me/orders/{orderId}"));
        Assert.True(detail.GetProperty("canCancel").GetBoolean());

        await ExpectAsync(HttpStatusCode.NoContent, await shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{orderId}/cancel", new { }, Json));

        var cancelled = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(OrderCancellationReason.CustomerRequest, cancelled.CancellationReason);
        Assert.True(cancelled.CancelledByCustomer);
        Assert.Equal(50m, await SellableAsync(shop.Context.BranchId, shop.Food.SkuId));

        var creditNote = await WithDbAsync(db => db.CreditNotes.AsNoTracking().Where(c => c.InvoiceId == order.InvoiceId).Select(c => new { c.Id, c.Total }).SingleAsync());
        Assert.Equal(order.Totals.GrandTotal, creditNote.Total);
        Assert.Contains(creditNote.Id, cancelled.CreditNoteIds);
        Assert.Equal(0m, cancelled.RefundDue);                          // nothing was paid yet
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.cancelled"));
        Assert.Equal(0, await NotificationCountAsync(shop.CustomerId, "order.cancelled-refund-due"));
    }

    [Fact]
    public async Task I_E9_ACustomerCancelsATransferPaidOrder_TheRefundIsOwed_ListedForStaff_AndTheCustomerIsTold()
    {
        var (shop, orderId, reception) = await TransferOrderAsync();
        var order = await LoadOrderAsync(orderId);
        await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(reception, orderId, order.Version, 2135.00m));

        await ExpectAsync(HttpStatusCode.NoContent, await shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{orderId}/cancel", new { }, Json));

        var cancelled = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.Equal(2135.00m, cancelled.RefundDue);
        Assert.Equal(50m, await SellableAsync(shop.Context.BranchId, shop.Food.SkuId));
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.cancelled-refund-due"));

        var (manager, _) = await StaffClientAsync(shop.Context, StaffRoles.ShopManager);
        var due = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await manager.GetAsync("/api/staff/orders/refunds-due")));
        var row = due.GetProperty("items").EnumerateArray().Single(o => o.GetProperty("orderId").GetGuid() == orderId);
        Assert.Equal("2135.00", row.GetProperty("refundDue").GetProperty("amount").GetString());

        var detail = await ReadAsync(await shop.Customer.GetAsync($"/api/customers/me/orders/{orderId}"));
        Assert.Equal("2135.00", detail.GetProperty("refundDue").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task I_E10_CancellationAfterPackingBegan_IsRefusedForTheCustomer_ButStaffMayCancel()
    {
        var shop = await SetUpShopAsync();
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var staffUser = new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid());
        await MutateOrderAsync(orderId, o => o.StartPacking(staffUser, Now, Guid.NewGuid()));

        var detail = await ReadAsync(await shop.Customer.GetAsync($"/api/customers/me/orders/{orderId}"));
        Assert.False(detail.GetProperty("canCancel").GetBoolean());
        await AssertErrorAsync(await shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{orderId}/cancel", new { }, Json), "CancellationNotAvailable");
        Assert.Equal(OrderStatus.Packing, (await LoadOrderAsync(orderId)).Status);

        // staff can, with a reason
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        await AssertErrorAsync(await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/cancel", new { note = "" }, Json), "CancellationReasonRequired");
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{orderId}/cancel", new { note = "Customer called to cancel" }, Json));
        var cancelled = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Cancelled, cancelled.Status);
        Assert.False(cancelled.CancelledByCustomer);
        Assert.Equal(OrderCancellationReason.StaffDecision, cancelled.CancellationReason);
    }

    [Fact]
    public async Task I_E11_AnotherCustomersOrder_IsNotFound_ForDetailCancelReorderAndReference()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var orderId = (await PlaceTransferOrderAsync(shop)).GetProperty("orderId").GetGuid();
        var (stranger, _, _) = await RegisterAndSignInCustomerAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/customers/me/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/customers/me/orders/{orderId}/cancel", new { }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsJsonAsync($"/api/customers/me/orders/{orderId}/transfer-reference", new { reference = "X-1" }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.SendAsync(StaffReorder(orderId))).StatusCode);
        Assert.Equal(OrderStatus.PendingPayment, (await LoadOrderAsync(orderId)).Status);

        var list = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await stranger.GetAsync("/api/customers/me/orders")));
        Assert.Equal(0, list.GetProperty("totalCount").GetInt32());
    }

    // ── the customer's own reads ────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TheCustomersOrderList_IsPagedAndFilteredByStatus_AndTheDetailShowsTransferInstructionsWhileAwaitingPayment()
    {
        var (shop, orderId, _) = await TransferOrderAsync();

        var list = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await shop.Customer.GetAsync("/api/customers/me/orders?status=PendingPayment&pageSize=5")));
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(orderId, item.GetProperty("id").GetGuid());
        Assert.Equal("BankTransfer", item.GetProperty("paymentMethod").GetString());
        Assert.Equal(3, item.GetProperty("itemCount").GetInt32());
        Assert.Equal("Asia/Dhaka", item.GetProperty("branchTimeZoneId").GetString());
        Assert.Equal(0, (await ReadAsync(await shop.Customer.GetAsync("/api/customers/me/orders?status=Delivered"))).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await shop.Customer.GetAsync("/api/customers/me/orders?status=Bogus")).StatusCode);

        var detail = await ReadAsync(await shop.Customer.GetAsync($"/api/customers/me/orders/{orderId}"));
        var instructions = detail.GetProperty("transferInstructions");
        Assert.Equal("1234567890", instructions.GetProperty("accountNumber").GetString());
        Assert.Equal(detail.GetProperty("orderNumber").GetString(), instructions.GetProperty("paymentReference").GetString());
        Assert.True(detail.GetProperty("canCancel").GetBoolean());
        Assert.Equal(2, detail.GetProperty("lines").GetArrayLength());
        Assert.Equal("2135.00", detail.GetProperty("totals").GetProperty("grandTotal").GetProperty("amount").GetString());
        var history = Assert.Single(detail.GetProperty("statusHistory").EnumerateArray());
        Assert.False(history.TryGetProperty("changedBy", out _));          // a customer never sees who changed it
    }

    // ── C-5: a card number is never a reference ─────────────────────────────────────────────────────
    [Fact]
    public async Task AReferenceThatLooksLikeACardNumber_IsRefused_ByTheCustomerAndByStaff_AndIsNeverStored()
    {
        var (shop, orderId, reception) = await TransferOrderAsync();
        var order = await LoadOrderAsync(orderId);

        await AssertErrorAsync(await shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{orderId}/transfer-reference", new { reference = "4111 1111 1111 1111" }, Json), "ReferenceLooksLikeCardNumber");
        await AssertErrorAsync(await ConfirmTransferAsync(reception, orderId, order.Version, 2135.00m, "4111-1111-1111-1111"), "ReferenceLooksLikeCardNumber");

        var after = await LoadOrderAsync(orderId);
        Assert.Null(after.CustomerTransferReference);
        Assert.Equal(OrderStatus.PendingPayment, after.Status);
    }

    // ── reconciliation issues: the list and resolving (the failure itself is in the fault tests) ─────
    [Fact]
    public async Task TheReconciliationIssueList_IsForAccountantsAndAdmins_AndNurseIsRefused()
    {
        var shop = await SetUpShopAsync(fillCart: false);
        var (nurse, _) = await StaffClientAsync(shop.Context, StaffRoles.Nurse);
        var (accountant, _) = await StaffClientAsync(shop.Context, StaffRoles.Accountant);

        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.GetAsync("/api/staff/orders/reconciliation-issues")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await accountant.GetAsync("/api/staff/orders/reconciliation-issues?status=Open")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await accountant.GetAsync("/api/staff/orders/reconciliation-issues?status=Bogus")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await accountant.PostAsJsonAsync($"/api/staff/orders/reconciliation-issues/{Guid.NewGuid()}/resolve", new { note = "done" }, Json)).StatusCode);
    }
}
