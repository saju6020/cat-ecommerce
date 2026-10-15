using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 Phase H, the point of sale -- I-H1 to I-H4 through real HTTP against the real Inventory, Shop and Billing: a barcode finds the SKU with its FEFO batch (US-ORD-16
/// AC2), a cash sale gives change and deducts through the same ledger as an online order (AC1), a prescription food needs a customer and a cat, the receipt is structured and
/// 48 columns wide, and a double submit with one key makes one sale (US-ORD-18 AC1).
/// </summary>
public sealed class OrderingPosEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private sealed record Till(Shop Shop, HttpClient Cashier, OrderingSku Food, OrderingSku Toy);

    private async Task<Till> TillAsync()
    {
        var shop = await SetUpShopAsync(fillCart: false);
        var food = await CreateSkuAsync(shop.Context, price: 850m, stock: 20m, food: true, name: "Kitten food", barcode: NewBarcode());
        var toy = await CreateSkuAsync(shop.Context, price: 120m, stock: 20m, taxClassId: shop.Context.ZeroTaxClassId, name: "Feather wand " + Guid.NewGuid().ToString("N")[..8], barcode: NewBarcode());
        var (cashier, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        return new Till(shop, cashier, food, toy);
    }

    /// <summary>A valid GTIN-13 (the catalogue checks the check digit) that no other test uses.</summary>
    private static string NewBarcode()
    {
        var body = "89" + Random.Shared.NextInt64(0, 10_000_000_000L).ToString("D10");
        var sum = body.Select((c, i) => (c - '0') * (i % 2 == 0 ? 1 : 3)).Sum();
        return body + ((10 - sum % 10) % 10);
    }

    /// <summary>ADR-216: every sale names the buyer (name and mobile, both required). With no mobile given, a fresh one is used -- a brand-new walk-in customer.</summary>
    private static object Sale(Till till, object[] tenders, int food = 1, int toy = 1, string? mobile = null, string? name = "Counter Customer", string? code = null) => new
    {
        customerName = name,
        customerMobile = mobile ?? NewMobile(),
        lines = new[] { new { skuId = till.Food.SkuId, quantity = food }, new { skuId = till.Toy.SkuId, quantity = toy } },
        discountCode = code,
        tenders,
    };

    private async Task<string> MobileOfAsync(Guid customerId) =>
        await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().Where(c => c.Id == customerId).Select(c => c.MobileNumber!.Value).SingleAsync());

    private static object Cash(decimal amount) => new { method = "Cash", amount };

    private static object Card(decimal amount, string reference = "SLIP-1001") => new { method = "InStoreCard", amount, reference };

    // 1 x food (850.00 + 15 % = 977.50) + 1 x toy (120.00 at 0 %) = 1097.50
    private const decimal Total = 1097.50m;

    // ── I-H1 (US-ORD-16 AC1, AC2) ───────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_H1_ABarcodeFindsTheSkuAndItsFefoBatch_AndACashSaleGivesChange_ThroughTheSameLedger()
    {
        var till = await TillAsync();

        var found = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await till.Cashier.GetAsync($"/api/staff/pos/sku?barcode={till.Food.Barcode}")));
        Assert.Equal(till.Food.SkuId, found.GetProperty("skuId").GetGuid());
        Assert.Equal(20, found.GetProperty("sellable").GetInt32());
        Assert.Equal("850.00", found.GetProperty("unitPrice").GetProperty("amount").GetString());
        var batch = found.GetProperty("fefoBatches").EnumerateArray().First();
        Assert.Contains(batch.GetProperty("batchId").GetGuid(), till.Food.BatchIds);

        Assert.Equal(HttpStatusCode.NotFound, (await till.Cashier.GetAsync("/api/staff/pos/sku?barcode=0000000000000")).StatusCode);
        var search = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await till.Cashier.GetAsync($"/api/staff/pos/skus?q={till.Toy.Name}")));
        Assert.Contains(search.EnumerateArray(), s => s.GetProperty("skuId").GetGuid() == till.Toy.SkuId);

        var quote = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await till.Cashier.PostAsJsonAsync("/api/staff/pos/quote", Sale(till, [])!, Json)));
        Assert.True(quote.GetProperty("canSell").GetBoolean());
        Assert.Equal("1097.50", quote.GetProperty("totals").GetProperty("grandTotal").GetProperty("amount").GetString());

        var sellableBefore = await SellableAsync(till.Shop.Context.BranchId, till.Food.SkuId);
        var response = await ExpectAsync(HttpStatusCode.Created, await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(1200m)])));
        var sale = await ReadAsync(response);
        var orderId = sale.GetProperty("orderId").GetGuid();
        Assert.Equal("102.50", sale.GetProperty("changeDue").GetProperty("amount").GetString());
        Assert.False(string.IsNullOrWhiteSpace(sale.GetProperty("invoiceNumber").GetString()));

        var order = await LoadOrderAsync(orderId);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal(OrderChannel.Counter, order.Channel);
        Assert.NotNull(order.CustomerId);                                             // ADR-216: the buyer is a real (walk-in) customer, not a shared account
        Assert.Equal(Total, order.AmountPaid);
        Assert.Equal(102.50m, order.ChangeGiven);
        Assert.Equal(3, order.StatusHistory.Count);                                   // PendingPayment, Confirmed, Delivered
        Assert.Equal(sellableBefore - 1m, await SellableAsync(till.Shop.Context.BranchId, till.Food.SkuId));

        // the stock left through the one ledger: PosSale sale entries against the FEFO batches, the same Sale kind an online order uses
        var entries = await WithDbAsync(db => db.StockLedgerEntries.AsNoTracking().Where(e => e.ReferenceId == orderId).Select(e => new { e.Kind, e.ReferenceKind, e.QuantityDelta, e.BatchId, e.CustomerId }).ToListAsync());
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => { Assert.Equal(MovementKind.Sale, e.Kind); Assert.Equal(StockReferenceKind.PosSale, e.ReferenceKind); Assert.Equal(-1m, e.QuantityDelta); Assert.Equal(order.CustomerId, e.CustomerId); });
        Assert.Contains(entries, e => e.BatchId == batch.GetProperty("batchId").GetGuid());

        // Billing: an invoice issued to the same customer as the order, and paid in full
        var invoice = await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == order.InvoiceId).Select(i => new { i.Total, i.PaidTotal, i.CustomerId, i.Number }).SingleAsync());
        Assert.Equal(order.CustomerId, invoice.CustomerId);
        Assert.Equal(Total, invoice.Total);
        Assert.Equal(Total, invoice.PaidTotal);
        Assert.Equal(sale.GetProperty("invoiceNumber").GetString(), invoice.Number);
        Assert.Equal(1, await WithDbAsync(db => db.AuditEntries.AsNoTracking().CountAsync(a => a.EntityId == orderId && a.Action == "CounterSaleCompleted")));
    }

    [Fact]
    public async Task ASaleToAKnownCustomer_IsTheirs_AndMixedTendersWork()
    {
        var till = await TillAsync();
        var mobile = await MobileOfAsync(till.Shop.CustomerId);
        var accountsBefore = await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().CountAsync(c => c.MobileNumber!.Value == mobile));

        // card 800.00 + cash 400.00 for 1097.50: the cash due is 297.50, so the change is 102.50 -- the name typed is not used, the account's is
        var sale = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await StaffPostAsync(
            till.Cashier, "/api/staff/pos/sales", Sale(till, [Card(800m), Cash(400m)], mobile: mobile, name: "Somebody Else"))));
        Assert.Equal(1, accountsBefore);
        Assert.Equal(1, await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().CountAsync(c => c.MobileNumber!.Value == mobile)));   // found, not duplicated
        Assert.Equal("102.50", sale.GetProperty("changeDue").GetProperty("amount").GetString());

        var order = await LoadOrderAsync(sale.GetProperty("orderId").GetGuid());
        Assert.Equal(till.Shop.CustomerId, order.CustomerId);
        Assert.Equal(2, order.Payments.Count);
        Assert.Equal(new[] { "CardInStore", "CashInStore" }, order.Payments.Select(p => p.Method).OrderBy(m => m).ToArray());
        Assert.Equal(Total, order.AmountPaid);

        // the customer is stamped on the stock movement (the recall trace reads it)
        Assert.All(await WithDbAsync(db => db.StockLedgerEntries.AsNoTracking().Where(e => e.ReferenceId == order.Id).Select(e => e.CustomerId).ToListAsync()), c => Assert.Equal(till.Shop.CustomerId, c));
    }

    // ── ADR-216 (owner decision 6, 2026-10-09): the buyer is found or created by mobile; no shared account ──
    [Fact]
    public async Task ACounterSale_ToANewMobile_CreatesAPasswordlessWalkInCustomer_WhoCarriesTheOrderAndTheInvoice()
    {
        var till = await TillAsync();
        var mobile = NewMobile();
        var stored = CatEcommerce.Domain.ValueObjects.MobileNumber.Create(mobile).Value;   // accounts keep the normalised +880 form

        var sale = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(1200m)], mobile: mobile, name: "Rina Akter"))));

        var order = await LoadOrderAsync(sale.GetProperty("orderId").GetGuid());
        var customer = await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().Where(c => c.MobileNumber!.Value == stored)
            .Select(c => new { c.Id, c.FullName, c.RegistrationChannel, HasPassword = c.PasswordCredential != null, Status = c.Status }).SingleAsync());
        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal("Rina Akter", customer.FullName);
        Assert.Equal(RegistrationChannel.Reception, customer.RegistrationChannel);
        Assert.False(customer.HasPassword);                                      // Incomplete until a password is set (ADR-038)
        Assert.Equal(CustomerAccountStatus.Active, customer.Status);
        Assert.Equal(customer.Id, await WithDbAsync(db => db.Invoices.AsNoTracking().Where(i => i.Id == order.InvoiceId).Select(i => i.CustomerId).SingleAsync()));

        // a second sale to the same mobile finds the same customer
        var again = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(1200m)], mobile: mobile, name: "R. Akter"))));
        Assert.Equal(customer.Id, (await LoadOrderAsync(again.GetProperty("orderId").GetGuid())).CustomerId);
        Assert.Equal(1, await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().CountAsync(c => c.MobileNumber!.Value == stored)));
    }

    [Fact]
    public async Task TheWalkInCustomer_WhoLaterRegistersOnline_WithTheSameMobile_IsOfferedThePasswordReset_TheLinkToTheSameAccount()
    {
        var till = await TillAsync();
        var mobile = NewMobile();
        var stored = CatEcommerce.Domain.ValueObjects.MobileNumber.Create(mobile).Value;   // accounts keep the normalised +880 form
        await ExpectAsync(HttpStatusCode.Created, await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(1200m)], mobile: mobile, name: "Rina Akter")));

        var client = CreateClientOn(Factory);
        var captcha = await client.PostAsync("/api/captcha", null);
        var captchaId = (await ReadAsync(captcha)).GetProperty("captchaId").GetString()!;
        var register = await client.PostAsJsonAsync("/api/customers", new { fullName = "Rina Akter", mobileNumber = mobile, password = CustomerPassword, captchaId, captchaAnswer = "TEST1" }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        var error = (await ReadAsync(register)).GetProperty("errors").EnumerateArray().Single();
        Assert.Equal("MobileNumberAlreadyRegistered", error.GetProperty("errorCode").GetString());
        Assert.Contains("reset your password", error.GetProperty("errorMessage").GetString());     // the offer that completes the walk-in's own account
        Assert.Equal(1, await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().CountAsync(c => c.MobileNumber!.Value == stored)));
    }

    [Fact]
    public async Task ACounterSale_NeedsTheCustomersNameAndAValidMobile()
    {
        var till = await TillAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(2000m)], name: null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(2000m)], name: " "))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(2000m)], mobile: ""))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(2000m)], mobile: "12345"))).StatusCode);
        Assert.Equal(20m, await SellableAsync(till.Shop.Context.BranchId, till.Food.SkuId));
    }

    [Fact]
    // FU1 D2 (owner decision 2026-10-10) REPLACES the old ARefusedSale_LeavesNoWalkInAccountBehind: the walk-in buyer is always stored, even when the sale is refused.
    public async Task ARefusedSale_StillLeavesTheWalkInAccountBehind_AndNothingIsSold()
    {
        var till = await TillAsync();
        var mobile = NewMobile();
        var stored = CatEcommerce.Domain.ValueObjects.MobileNumber.Create(mobile).Value;   // accounts keep the normalised +880 form

        await AssertErrorAsync(await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(99999m)], food: 21, mobile: mobile)), "StockNoLongerAvailable", HttpStatusCode.Conflict);

        Assert.Equal(1, await WithDbAsync(db => db.CustomerAccounts.AsNoTracking().CountAsync(c => c.MobileNumber!.Value == stored)));
        Assert.Equal(20m, await SellableAsync(till.Shop.Context.BranchId, till.Food.SkuId));
    }

    [Fact]
    public async Task TheTenders_MustCoverTheTotalExactly_ACardNeedsItsSlipReference_AndNeverAReferenceThatIsACardNumber()
    {
        var till = await TillAsync();
        const string path = "/api/staff/pos/sales";

        await AssertErrorAsync(await StaffPostAsync(till.Cashier, path, Sale(till, [Cash(1000m)])), "TendersDoNotCoverTotal");
        await AssertErrorAsync(await StaffPostAsync(till.Cashier, path, Sale(till, [Card(2000m)])), "TendersDoNotCoverTotal");
        await AssertErrorAsync(await StaffPostAsync(till.Cashier, path, Sale(till, [new { method = "InStoreCard", amount = Total }])), "TerminalReferenceRequired");
        Assert.Equal(HttpStatusCode.BadRequest, (await StaffPostAsync(till.Cashier, path, Sale(till, [Card(Total, "4111 1111 1111 1111")]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StaffPostAsync(till.Cashier, path, Sale(till, [new { method = "Cheque", amount = Total }]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await StaffPostAsync(till.Cashier, path, Sale(till, []))).StatusCode);

        // exact card payment: no change
        var exact = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await StaffPostAsync(till.Cashier, path, Sale(till, [Card(Total)]))));
        Assert.Equal("0.00", exact.GetProperty("changeDue").GetProperty("amount").GetString());

        // nothing was sold by the refused attempts
        Assert.Equal(19m, await SellableAsync(till.Shop.Context.BranchId, till.Food.SkuId));
    }

    [Fact]
    public async Task ASaleOfMoreThanIsInStock_IsRefused_AndNothingMoves()
    {
        var till = await TillAsync();

        await AssertErrorAsync(await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(99999m)], food: 21)), "StockNoLongerAvailable", HttpStatusCode.Conflict);
        Assert.Equal(20m, await SellableAsync(till.Shop.Context.BranchId, till.Food.SkuId));
    }

    // ── I-H2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_H2_APrescriptionFood_WithoutACustomerAndCat_IsRefused()
    {
        var till = await TillAsync();
        var rx = await CreateSkuAsync(till.Shop.Context, price: 500m, stock: 10m, prescription: true, food: true, name: "Renal diet", barcode: NewBarcode());
        var body = new { customerName = "New Buyer", customerMobile = NewMobile(), lines = new[] { new { skuId = rx.SkuId, quantity = 1 } }, tenders = new object[] { Cash(2000m) } };

        await AssertErrorAsync(await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", body), "PrescriptionCatRequired");

        // an existing customer (found by mobile) without a cat is still refused
        var withCustomer = new { customerName = "Ordering Customer", customerMobile = await MobileOfAsync(till.Shop.CustomerId), lines = new[] { new { skuId = rx.SkuId, quantity = 1 } }, tenders = new object[] { Cash(2000m) } };
        await AssertErrorAsync(await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", withCustomer), "PrescriptionCatRequired");
        Assert.Equal(10m, await SellableAsync(till.Shop.Context.BranchId, rx.SkuId));
    }

    // ── I-H3 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_H3_TheReceipt_IsStructured_AndTheTextIs48ColumnsWide()
    {
        var till = await TillAsync();
        var sale = await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", Sale(till, [Cash(1200m)]))));
        var orderId = sale.GetProperty("orderId").GetGuid();

        var receipt = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await till.Cashier.GetAsync($"/api/staff/pos/sales/{orderId}/receipt")));
        Assert.Equal(sale.GetProperty("invoiceNumber").GetString(), receipt.GetProperty("invoiceNumber").GetString());
        Assert.Equal(2, receipt.GetProperty("lines").GetArrayLength());
        Assert.Equal("1097.50", receipt.GetProperty("totals").GetProperty("grandTotal").GetProperty("amount").GetString());
        Assert.Equal("102.50", receipt.GetProperty("changeDue").GetProperty("amount").GetString());
        Assert.Contains("BIN-" + till.Shop.Context.BranchCode, receipt.GetProperty("taxRegistrationNumber").GetString());     // the seller's legal identifier, frozen on the invoice

        var text = receipt.GetProperty("text").GetString()!;
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        Assert.All(lines, l => Assert.True(l.Length <= 48, $"'{l}' is {l.Length} columns"));
        Assert.Contains(lines, l => l.StartsWith("TOTAL") && l.EndsWith("1,097.50") && l.Length == 48);
        Assert.Contains(lines, l => l.StartsWith("Change") && l.EndsWith("102.50"));
        Assert.Contains("Kitten food", text);

        // another branch has no such receipt; a role without Pos.Sell is refused
        var (elsewhere, _) = await StaffClientAsync(await CreateContextAsync(), StaffRoles.Reception);
        Assert.Equal(HttpStatusCode.NotFound, (await elsewhere.GetAsync($"/api/staff/pos/sales/{orderId}/receipt")).StatusCode);
        var (nurse, _) = await StaffClientAsync(till.Shop.Context, StaffRoles.Nurse);
        Assert.Equal(HttpStatusCode.Forbidden, (await nurse.GetAsync($"/api/staff/pos/sales/{orderId}/receipt")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await StaffPostAsync(nurse, "/api/staff/pos/sales", Sale(till, [Cash(2000m)]))).StatusCode);
    }

    // ── I-H4 (US-ORD-18 AC1) ────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_H4_ADoubleSubmitWithOneKey_MakesOneSale()
    {
        var till = await TillAsync();
        var key = Guid.NewGuid();
        var body = Sale(till, [Cash(1200m)]);

        var first = await ExpectAsync(HttpStatusCode.Created, await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", body, key));
        var replay = await StaffPostAsync(till.Cashier, "/api/staff/pos/sales", body, key);

        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal((await ReadAsync(first)).GetProperty("orderId").GetGuid(), (await ReadAsync(replay)).GetProperty("orderId").GetGuid());
        Assert.Equal(1, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.BranchId == till.Shop.Context.BranchId && o.Channel == OrderChannel.Counter)));
        Assert.Equal(19m, await SellableAsync(till.Shop.Context.BranchId, till.Food.SkuId));
    }
}
