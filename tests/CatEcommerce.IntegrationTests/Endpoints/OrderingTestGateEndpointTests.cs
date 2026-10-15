using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 TEST gate -- the five cases the ARCHITECTURE re-check handed over (docs/gates/HO-ORD-001-ARCHITECTURE.md, "NOTES FOR TEST"), run against the real per-branch database:
/// T1/T2 the new-buyer registration inside the sale's transaction under a unique-mobile race, T3 the Reception cashier's customer lookup, T4 invoice AND credit-note numbering with
/// scoped and unscoped issuers together (no deadlock), T5 Billing's refusals in approve/reject rolling the order change back, T6 the refund draw plan seeing the excess a credit
/// note released earlier in the same transaction.
/// </summary>
public sealed class OrderingTestGateEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private sealed record Till(Shop Shop, HttpClient Cashier, OrderingSku Item);

    private async Task<Till> TillAsync(decimal stock = 200m)
    {
        var shop = await SetUpShopAsync(fillCart: false);
        var item = await CreateSkuAsync(shop.Context, price: 100m, stock: stock, taxClassId: shop.Context.ZeroTaxClassId, name: "Gate item " + Guid.NewGuid().ToString("N")[..8]);
        var (cashier, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        return new Till(shop, cashier, item);
    }

    private static Task<HttpResponseMessage> SellAsync(Till till, string mobile, string name = "Gate Buyer") => StaffPostAsync(till.Cashier, "/api/staff/pos/sales", new
    {
        customerName = name, customerMobile = mobile,
        lines = new[] { new { skuId = till.Item.SkuId, quantity = 1 } },
        tenders = new object[] { new { method = "Cash", amount = 100m } },
    });

    private static string Stored(string mobile) => MobileNumber.Create(mobile).Value;

    // ── T1 (architecture re-check: counter sale, new buyer, concurrent registration) ────────────────
    // F-2 fixed (ORD 42): every concurrent sale to one new mobile succeeds against one account, with no false reconciliation issue.
    [Fact]
    public async Task T1_ConcurrentCounterSales_ToTheSameNewMobile_NeverFiveHundred_NeverTwoAccounts_AndTheRetryAlwaysSells()
    {
        var till = await TillAsync(stock: 400m);
        var outcomes = new List<string>();

        for (var round = 0; round < 6; round++)
        {
            var mobile = NewMobile();
            var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => SellAsync(till, mobile, "Race Buyer " + i)));
            var codes = new List<string>();
            foreach (var r in responses) codes.Add(r.IsSuccessStatusCode ? "201" : $"{(int)r.StatusCode}:{string.Join("/", await ErrorCodesSafeAsync(r))}");
            outcomes.Add(string.Join(" ", codes.GroupBy(c => c).Select(g => $"{g.Count()}x{g.Key}")));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));   // F-2 fixed: the buyer is resolved before the sale scope, so every concurrent sale succeeds
            Assert.Equal(1, await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().CountAsync(c => c.MobileNumber!.Value == Stored(mobile))));

            // the retry the cashier makes after a refusal sells, to the one customer
            await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, mobile));
        }

        var counterOrders = await WithDbAsync(db => db.Orders.AsNoTracking().Where(o => o.BranchId == till.Shop.Context.BranchId && o.Channel == OrderChannel.Counter).CountAsync());
        var invoices = await WithDbAsync(db => db.Invoices.AsNoTracking().CountAsync(i => i.BranchId == till.Shop.Context.BranchId));
        Assert.Equal(counterOrders, invoices);   // every sale has exactly its invoice
        Assert.Equal(400m - counterOrders, await SellableAsync(till.Shop.Context.BranchId, till.Item.SkuId));   // and its one unit of stock, no more
        Assert.Empty(await WithDbAsync(db => db.ReconciliationIssues.AsNoTracking().Where(i => i.BranchId == till.Shop.Context.BranchId).Select(i => i.Kind.DisplayName + " / " + i.ErrorSummary).ToListAsync()));   // no false CounterSaleFailed
        Console.WriteLine("T1 outcomes per round: " + string.Join(" || ", outcomes));
    }

    private static async Task<string[]> ErrorCodesSafeAsync(HttpResponseMessage r)
    {
        try { return await ErrorCodesAsync(r); } catch { return ["?"]; }
    }

    // ── T2 (architecture re-check: the mobile is held by an account that is not Active) ─────────────
    [Fact]
    // FU1 D1: unchanged assertions -- here the merged account records NO surviving account, so there is nobody to follow to and the cashier-facing refusal stays. A merge
    // with an Active survivor now sells to the survivor (OrderingFollowUp1EndpointTests.D1_*).
    public async Task T2_ACounterSale_ToAMobileHeldByANonActiveAccount_IsACleanRefusal_NotAServerError_AndNothingMoves()
    {
        var till = await TillAsync();
        var mobile = NewMobile();
        await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, mobile));   // creates the walk-in account
        var accountId = await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().Where(c => c.MobileNumber!.Value == Stored(mobile)).Select(c => c.Id).SingleAsync());
        await WithDbAsync(async db =>
        {
            var account = await db.CustomerAccounts.SingleAsync(c => c.Id == accountId);
            db.Entry(account).Property(c => c.Status).CurrentValue = CustomerAccountStatus.Merged;
            return await db.SaveChangesAsync();
        });
        var stockBefore = await SellableAsync(till.Shop.Context.BranchId, till.Item.SkuId);
        var ordersBefore = await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.BranchId == till.Shop.Context.BranchId));

        var response = await SellAsync(till, mobile);
        var body = await BodyAsync(response);

        Assert.True((int)response.StatusCode < 500, $"status {(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("AccountNotActive", body);   // F-3: cashier-facing code, not the customer's reset offer
        Assert.DoesNotContain("MobileNumberAlreadyRegistered", body);
        Assert.Equal(stockBefore, await SellableAsync(till.Shop.Context.BranchId, till.Item.SkuId));
        Assert.Equal(ordersBefore, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.BranchId == till.Shop.Context.BranchId)));
        Assert.Empty(await WithDbAsync(db => db.ReconciliationIssues.AsNoTracking().Where(i => i.BranchId == till.Shop.Context.BranchId).ToListAsync()));   // a refusal is not a failed sale
    }

    // ── T3 (architecture re-check: the cashier's FindCustomersQuery) ────────────────────────────────
    [Fact]
    public async Task T3_AReceptionCashier_FindsAnExistingCustomerByMobile_SoTheSaleIsTheirs_AndNoSecondAccountIsAttempted()
    {
        var till = await TillAsync();
        var mobile = await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().Where(c => c.Id == till.Shop.CustomerId).Select(c => c.MobileNumber!.Value).SingleAsync());

        var sale = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, mobile, "Typed Name Is Ignored")));

        Assert.Equal(till.Shop.CustomerId, (await LoadOrderAsync(sale.GetProperty("orderId").GetGuid())).CustomerId);   // it can only be found if the lookup returned a row to Reception
        Assert.Equal(1, await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().CountAsync(c => c.MobileNumber!.Value == mobile)));
    }

    // ── T4 (ADR-179 amended; ADR-215): invoice AND credit-note numbering, scoped and unscoped issuers together ──
    [Fact]
    public Task T4a_ScopedSales_AndStaffInvoices_AtOneBranch_NeitherDeadlockNorGap() => MixAsync(sales: 8, staffInvoices: 6, staffCredits: 0, cancels: 0);

    [Fact]
    public Task T4b_ScopedCancellations_AndStaffCreditNotes_AtOneBranch_NeitherDeadlockNorGap() => MixAsync(sales: 0, staffInvoices: 0, staffCredits: 4, cancels: 3);

    [Fact]
    public Task T4d_ScopedCancellationsAlone_AtOneBranch_NeitherDeadlockNorGap() => MixAsync(sales: 0, staffInvoices: 0, staffCredits: 0, cancels: 3);

    [Fact]
    public Task T4e_StaffCreditNotesAlone_AtOneBranch_NeitherDeadlockNorGap() => MixAsync(sales: 0, staffInvoices: 0, staffCredits: 4, cancels: 0);

    [Fact]
    public Task T4c_AllFourIssuers_AtOneBranch_NeitherDeadlockNorGap() => MixAsync(sales: 6, staffInvoices: 4, staffCredits: 4, cancels: 3);

    private async Task MixAsync(int sales, int staffInvoices, int staffCredits, int cancels)
    {
        var till = await TillAsync();
        var context = till.Shop.Context;

        // orders to cancel (each cancellation issues a credit note inside Billing's published scope)
        var toCancel = new List<Guid>();
        for (var i = 0; i < cancels; i++)
        {
            await ExpectAsync(HttpStatusCode.Created, await till.Shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = till.Shop.Food.SkuId, quantity = 1 }, Json));
            toCancel.Add((await PlaceCodOrderAsync(till.Shop)).GetProperty("orderId").GetGuid());
        }

        // sale invoices that staff will credit by hand, and charges that staff will invoice by hand
        var creditInvoices = new List<Guid>();
        for (var i = 0; i < staffCredits; i++)
        {
            var sale = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, NewMobile())));
            creditInvoices.Add((await LoadOrderAsync(sale.GetProperty("orderId").GetGuid())).InvoiceId!.Value);
        }
        var charges = new List<Guid>();
        for (var i = 0; i < staffInvoices; i++) charges.Add(await RecordStaffChargeAsync(till.Shop.CustomerId, context));

        var calls = new List<Task<HttpResponseMessage>>();
        var labels = new List<string>();
        calls.AddRange(Enumerable.Range(0, sales).Select(_ => SellAsync(till, NewMobile()))); labels.AddRange(Enumerable.Repeat("counter-sale", sales));
        calls.AddRange(toCancel.Select(id => till.Shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{id}/cancel", new { }, Json))); labels.AddRange(Enumerable.Repeat("customer-cancel", cancels));
        calls.AddRange(charges.Select(c => context.Admin.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/staff/billing/invoices/issue-from-charges")
        {
            Content = JsonContent.Create(new { customerId = till.Shop.CustomerId, selector = new { chargeIds = new[] { c } } }, options: Json),
            Headers = { { "Idempotency-Key", Guid.NewGuid().ToString() } },
        }))); labels.AddRange(Enumerable.Repeat("staff-invoice", staffInvoices));
        calls.AddRange(creditInvoices.Select(inv => StaffPostAsync(context.Admin, $"/api/staff/billing/invoices/{inv}/credit-notes", new { kind = "Goodwill", reason = "Gate numbering", fullCredit = true }))); labels.AddRange(Enumerable.Repeat("staff-credit-note", staffCredits));

        var all = Task.WhenAll(calls);
        Assert.True(await Task.WhenAny(all, Task.Delay(TimeSpan.FromSeconds(60))) == all, "a call did not finish within 60 s: a deadlock or a lock never released");
        var responses = await all;

        var failures = new List<string>();
        for (var i = 0; i < responses.Length; i++)
        {
            if ((int)responses[i].StatusCode < 300) continue;
            var text = await BodyAsync(responses[i]);
            failures.Add($"{labels[i]}: {(int)responses[i].StatusCode} {(text.Length > 160 ? text[..160] : text)}");
        }
        Assert.True(failures.Count == 0, string.Join(" | ", failures));

        var invoiceNumbers = await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.BranchId == context.BranchId && i.SequenceNumber != null).Select(i => i.SequenceNumber!.Value).OrderBy(n => n).ToListAsync());
        var creditNumbers = await WithDbAsync(db => db.CreditNotes.AsNoTracking().Where(c => c.BranchId == context.BranchId).Select(c => c.SequenceNumber).OrderBy(n => n).ToListAsync());
        Assert.Equal(Enumerable.Range(1, invoiceNumbers.Count).Select(n => (long)n), invoiceNumbers);
        Assert.Equal(Enumerable.Range(1, creditNumbers.Count).Select(n => (long)n), creditNumbers);
        Assert.Equal(cancels + staffCredits, creditNumbers.Count);
        Assert.Equal(cancels + staffCredits + sales + staffInvoices, invoiceNumbers.Count);
    }

    private async Task<Guid> RecordStaffChargeAsync(Guid customerId, OrderingContext context)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var recorder = scope.ServiceProvider.GetRequiredService<IBillableChargeRecorder>();
        var id = await recorder.RecordAsync(new ChargeRequestDto(
            customerId, context.BranchId, null, BusinessLine.Shop, ItemKind.Product, ChargeSourceKind.Order, Guid.NewGuid(), "main", null, null, "Staff-issued item", 1m,
            new Money(100m, "BDT"), Money.Zero("BDT"), context.ZeroTaxClassId, DateOnly.FromDateTime(DateTime.UtcNow)));
        await scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>().SaveChangesAsync();
        return id;
    }

    // ── T5 (C12): Billing's own refusals in approve / reject answer 4xx and roll the order change back ──
    private async Task<(Shop Shop, Guid OrderId, HttpClient Recorder, Guid HeldPaymentId, int Version)> HeldTransferAsync()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        var orderId = (await PlaceTransferOrderAsync(shop)).GetProperty("orderId").GetGuid();
        await SetApprovalThresholdAsync(shop.Context, "1000.00");
        var (recorder, _) = await StaffClientAsync(shop.Context, StaffRoles.Administrator);
        var order = await LoadOrderAsync(orderId);
        await ExpectAsync(HttpStatusCode.OK, await ConfirmTransferAsync(recorder, orderId, order.Version, 2135.00m, "BANK-GATE-1"));
        var held = await LoadOrderAsync(orderId);
        return (shop, orderId, recorder, held.HeldPayment!.PaymentId!.Value, held.Version);
    }

    [Fact]
    public async Task T5a_Approve_WhenBillingSaysPaymentNotPending_Answers4xx_AndTheOrderKeepsItsHold()
    {
        var (shop, orderId, _, paymentId, version) = await HeldTransferAsync();
        var (billingApprover, _) = await StaffClientAsync(shop.Context, StaffRoles.Accountant);
        var (orderApprover, _) = await StaffClientAsync(shop.Context, StaffRoles.Accountant);

        // another accountant settles the payment in Billing directly, so by the time the order's approver acts it is no longer pending
        await ExpectAsync(HttpStatusCode.OK, await StaffPostAsync(billingApprover, $"/api/staff/billing/payments/{paymentId}/approve"));

        var response = await StaffPostAsync(orderApprover, $"/api/staff/orders/{orderId}/payment/approve", new { expectedVersion = version });

        await AssertErrorAsync(response, "PaymentNotPending");
        var after = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.PendingPayment, after.Status);          // the order did not move
        Assert.NotNull(after.HeldPayment);                               // and the hold is intact
        Assert.Empty(after.Payments);
        Assert.Equal(version, after.Version);
    }

    [Fact]
    public async Task T5b_Reject_ByThePersonWhoRecordedIt_IsRefusedByBilling_ApproverMustDiffer_AndTheHoldRemains()
    {
        var (shop, orderId, recorder, paymentId, version) = await HeldTransferAsync();

        var response = await recorder.PostAsJsonAsync($"/api/staff/orders/{orderId}/payment/reject", new { note = "Changed my mind" }, Json);

        await AssertErrorAsync(response, "ApproverMustDiffer");
        var after = await LoadOrderAsync(orderId);
        Assert.NotNull(after.HeldPayment);                               // the order's change (clearing the hold) rolled back with Billing's refusal
        Assert.Equal(version, after.Version);
        Assert.Equal(PaymentStatus.PendingApproval, await WithDbAsync(db => db.Payments.AsNoTracking().Where(p => p.Id == paymentId).Select(p => p.Status).SingleAsync()));
    }

    [Fact]
    public async Task T5c_AHoldMadeBeforeThePaymentWasRecordedAtHoldTime_IsRefused_NoPaymentAwaitingApproval_AndNothingChanges()
    {
        var (shop, orderId, _, _, _) = await HeldTransferAsync();
        await MutateOrderAsync(orderId, o =>
        {
            var legacy = new HeldPaymentConfirmation(2135.00m, "BANK-LEGACY", DateTimeOffset.UtcNow, Guid.NewGuid(), DateTimeOffset.UtcNow, paymentId: null);
            typeof(Domain.Aggregates.Order.OrderAggregate).GetProperty(nameof(o.HeldPayment))!.SetValue(o, legacy);
        });
        var (approver, _) = await StaffClientAsync(shop.Context, StaffRoles.Accountant);
        var order = await LoadOrderAsync(orderId);

        var response = await StaffPostAsync(approver, $"/api/staff/orders/{orderId}/payment/approve", new { expectedVersion = order.Version });

        await AssertErrorAsync(response, "NoPaymentAwaitingApproval");
        Assert.Equal(OrderStatus.PendingPayment, (await LoadOrderAsync(orderId)).Status);
    }

    // ── T6 (C23 refund): the draw plan sees the excess released by the credit note issued earlier in the same transaction ──
    [Fact]
    public async Task T6_ACashRefund_OnAPaidInvoice_DrawsTheExcessItsCreditNoteReleased_SoBillingRecordsTheRefundInFull()
    {
        var till = await TillAsync();
        var sale = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await SellAsync(till, NewMobile())));
        var orderId = sale.GetProperty("orderId").GetGuid();
        var order = await LoadOrderAsync(orderId);
        var paymentId = order.Payments.Single().PaymentId;
        var (manager, _) = await StaffClientAsync(till.Shop.Context, StaffRoles.ShopManager);

        // precondition: the payment is fully allocated to the invoice, so before the credit note nothing is free to refund
        Assert.Equal(0m, await WithDbAsync(async db => (await db.Payments.AsNoTracking().SingleAsync(p => p.Id == paymentId)).Unallocated));

        var created = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await StaffPostAsync(manager, $"/api/staff/orders/{orderId}/refunds",
            new { method = "CashInStore", amount = 40m, reference = "RCPT-GATE-1", reason = "Goodwill" })));

        var creditNoteId = created.GetProperty("creditNoteId").GetGuid();
        var refunds = await WithDbAsync(db => db.Refunds.AsNoTracking().Where(r => r.CreditNoteId == creditNoteId).Select(r => new { r.PaymentId, r.Amount }).ToListAsync());
        Assert.Equal(40m, refunds.Sum(r => r.Amount));                   // Billing's refund record exists for the whole amount
        Assert.All(refunds, r => Assert.Equal(paymentId, r.PaymentId));
        Assert.Equal(40m, await WithDbAsync(db => db.CreditNotes.AsNoTracking().Where(c => c.Id == creditNoteId).Select(c => c.Total).SingleAsync()));
    }
}
