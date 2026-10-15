using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 Phase D -- placement (<c>POST /api/customers/me/orders</c>, C9) through real HTTP against the real Shop, Inventory and Billing implementations:
/// I-D1, I-D2, I-D4, I-D6, I-D7, I-D8 (atomicity), I-D9 (twenty parallel placements for the last unit), I-D10 / I-D11 (double submit), I-D12 (a discount's
/// last use), I-D13, I-D14 (no card data anywhere) and the placement halves of I-E1 (a transfer order) and I-F12 (a pickup order paid on collection).
/// The diet-gate rows (I-D5, I-D5b, a prescription line) run against the real HO-PHM-001 gate.
/// </summary>
public sealed class OrderingPlacementEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    // ── I-D7 (US-ORD-08 AC1, US-ORD-20 AC1) ─────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D7_ACodOrder_IsConfirmedAtOnce_WithBatchesReserved_AnInvoice_AMessage_AndTheCartEmptied()
    {
        var shop = await SetUpShopAsync();
        var sellableBefore = await SellableAsync(shop.Context.BranchId, shop.Food.SkuId);

        var placed = await PlaceCodOrderAsync(shop);

        Assert.Equal("Confirmed", placed.GetProperty("status").GetString());
        Assert.Equal(2135.00m, AmountOf(placed.GetProperty("grandTotal")));
        var orderId = placed.GetProperty("orderId").GetGuid();
        var order = await LoadOrderAsync(orderId);

        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(shop.Context.BranchId, order.BranchId);
        Assert.Equal(shop.CustomerId, order.CustomerId);
        Assert.NotNull(order.InvoiceId);
        Assert.StartsWith("INV-", order.InvoiceNumber);
        Assert.Equal(2, order.Lines.Count);

        // every line carries the FEFO batch Inventory chose, and the whole quantity is allocated (D-4)
        Assert.True(order.IsFullyAllocated);
        var foodLine = order.Lines.Single(l => l.SkuId == shop.Food.SkuId);
        Assert.Equal(shop.Food.BatchIds[0], Assert.Single(foodLine.Allocations).BatchId);
        Assert.Equal(shop.Context.ShopLocationId, foodLine.Allocations[0].LocationId);

        // the stock is reserved: sellable dropped by the ordered quantity, the stamp names the customer and the order line
        Assert.Equal(sellableBefore - 2m, await SellableAsync(shop.Context.BranchId, shop.Food.SkuId));
        var entry = await WithDbAsync(db => db.StockReservationEntries.AsNoTracking().Where(e => e.ReferenceId == orderId && e.SkuId == shop.Food.SkuId)
            .Select(e => new { e.CustomerId, e.ReferenceLineId, e.ReferenceKind, e.QuantityDelta }).SingleAsync());
        Assert.Equal(shop.CustomerId, entry.CustomerId);
        Assert.Equal(foodLine.Id, entry.ReferenceLineId);
        Assert.Equal(StockReferenceKind.CustomerOrder, entry.ReferenceKind);
        Assert.Equal(2m, entry.QuantityDelta);

        // the charges Billing invoiced are the order's lines and its delivery fee, and the invoice total is the order total
        var invoice = await WithDbAsync(db => db.Invoices.AsNoTracking().SingleAsync(i => i.Id == order.InvoiceId));
        Assert.Equal(order.Totals.GrandTotal, invoice.Total);
        Assert.Equal(3, invoice.Lines.Count); // food, toy, delivery

        // the confirmation message was committed with the order
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.confirmed"));

        // the placed lines left the cart
        var cart = await ReadAsync(await shop.Customer.GetAsync("/api/cart"));
        Assert.Equal(0, cart.GetProperty("lines").GetArrayLength());

        // the history records the start, attributed to the customer
        var change = Assert.Single(order.StatusHistory);
        Assert.Equal(OrderStatus.Confirmed, change.To);
        Assert.Equal(shop.CustomerId, change.ChangedBy);
    }

    // ── I-D1 / I-D2 ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D1_PlacingWithAStaleFingerprint_IsRefusedAfterAPriceRise_AndTheNewQuoteIsAccepted()
    {
        var shop = await SetUpShopAsync();
        var stale = await QuoteAsync(shop.Customer, QuoteUrl(shop));

        await SchedulePriceAsync(shop.Context.Admin, shop.Food.SkuId, 900m);
        var fresh = await QuoteAsync(shop.Customer, QuoteUrl(shop));
        Assert.NotEqual(stale.GetProperty("fingerprint").GetString(), fresh.GetProperty("fingerprint").GetString());
        Assert.Contains("PriceIncreased", fresh.GetProperty("changes").EnumerateArray().Select(c => c.GetProperty("kind").GetString()));

        var refused = await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, stale)));
        await AssertErrorAsync(refused, "CheckoutQuoteChanged", HttpStatusCode.Conflict);

        // nothing was written by the refusal
        Assert.Equal(0, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == shop.CustomerId)));

        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, fresh))));
    }

    [Fact]
    public async Task I_D2_TheOrderStoresTheQuotesComponents_ToTheMinorUnit_AndAppliesTheDiscountCode()
    {
        var shop = await SetUpShopAsync();
        var discount = await CreateDiscountAsync(shop.Context, percent: 10m);
        await AcceptPendingPoliciesAsync(shop.Customer);

        var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop, code: discount.Code));
        var response = await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, quote, code: discount.Code)));
        var orderId = (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, response))).GetProperty("orderId").GetGuid();
        var order = await LoadOrderAsync(orderId);

        decimal Quoted(string name) => decimal.Parse(quote.GetProperty("totals").GetProperty(name).GetProperty("amount").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Quoted("subtotal"), order.Totals.Subtotal);
        Assert.Equal(Quoted("lineDiscountTotal"), order.Totals.LineDiscountTotal);
        Assert.Equal(Quoted("orderDiscount"), order.Totals.OrderDiscount);
        Assert.Equal(Quoted("deliveryCharge"), order.Totals.DeliveryCharge);
        Assert.Equal(Quoted("taxTotal"), order.Totals.TaxTotal);
        Assert.Equal(1927.50m, order.Totals.GrandTotal);
        Assert.Equal(discount.Code, order.DiscountCodeText);

        // the invoice Billing issued ties out to the same total, to the minor unit
        var invoice = await WithDbAsync(db => db.Invoices.AsNoTracking().SingleAsync(i => i.Id == order.InvoiceId));
        Assert.Equal(1927.50m, invoice.Total);
        Assert.Equal(229.50m, invoice.TaxTotal);

        // one use of the code was counted
        Assert.Equal(1, await WithDbAsync(db => db.DiscountCodes.AsNoTracking().Where(c => c.Id == discount.Id).Select(c => c.TimesRedeemed).SingleAsync()));
    }

    // ── I-D4 / I-D6 ─────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D4_APickupOrderPaidOnCollection_IsConfirmedAtOnce_AndADeliverySlotThatIsNotOffered_IsRefused()
    {
        var shop = await SetUpShopAsync();
        await AcceptPendingPoliciesAsync(shop.Customer);

        var pickupQuote = await QuoteAsync(shop.Customer, QuoteUrl(shop, "Pickup", "PayOnCollection", pickupBranchId: shop.Context.BranchId));
        var pickup = await ExpectAsync(HttpStatusCode.Created, await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, pickupQuote, "Pickup", "PayOnCollection", pickupBranchId: shop.Context.BranchId))));
        var order = await LoadOrderAsync((await ReadAsync(pickup)).GetProperty("orderId").GetGuid());
        Assert.Equal(OrderStatus.Confirmed, order.Status);
        Assert.Equal(FulfilmentMethod.Pickup, order.FulfilmentMethod);
        Assert.Equal(OrderPaymentMethod.PayOnCollection, order.PaymentMethod);
        Assert.Equal(shop.Context.BranchId, order.Fulfilment.PickupBranchId);
        Assert.Equal(0m, order.Totals.DeliveryCharge);
        Assert.NotNull(order.InvoiceId);

        // a delivery slot the service does not offer (this zone has no slot windows)
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = shop.Food.SkuId, quantity = 1 }, Json));
        var slotDate = Today.AddDays(3).ToString("yyyy-MM-dd");
        var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop, slotDate: slotDate, slotStart: "10:00"));
        Assert.False(quote.GetProperty("canPlace").GetBoolean());
        await AssertErrorAsync(await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, quote, slotDate: slotDate, slotStart: "10:00"))), "DeliverySlotUnavailable");
    }

    [Fact]
    public async Task I_D6_CashOnDelivery_InAZoneWithoutIt_IsRefusedWithItsReason()
    {
        var shop = await SetUpShopAsync(cod: false);
        await AcceptPendingPoliciesAsync(shop.Customer);

        var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop));
        var cash = quote.GetProperty("paymentOptions").EnumerateArray().Single(o => o.GetProperty("method").GetString() == "CashOnDelivery");
        Assert.False(cash.GetProperty("available").GetBoolean());
        Assert.Equal("CodNotOfferedInZone", cash.GetProperty("unavailableReason").GetString());

        await AssertErrorAsync(await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, quote))), "PaymentMethodUnavailable");
        Assert.Equal(0, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == shop.CustomerId)));
    }

    // ── I-D8 (atomicity, US-ORD-08 AC1) ─────────────────────────────────────────────────────────────
    /// <summary>The test seam is the real one: a branch with <b>no billing profile</b> makes Billing refuse the invoice after the order, its reservation, its charges and
    /// its message were saved. The unit of work is one database transaction, so none of it may exist afterwards.</summary>
    [Fact]
    public async Task I_D8_WhenBillingCannotIssueTheInvoice_NothingOfThePlacementSurvives()
    {
        var shop = await SetUpShopAsync(billingProfile: false);
        await AcceptPendingPoliciesAsync(shop.Customer);
        var sellableBefore = await SellableAsync(shop.Context.BranchId, shop.Food.SkuId);
        var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop));

        var response = await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, quote)));

        Assert.True((int)response.StatusCode >= 500, $"Expected a 5xx but got {(int)response.StatusCode}: {await BodyAsync(response)}");
        Assert.Equal(0, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == shop.CustomerId)));
        Assert.Equal(0, await WithDbAsync(db => db.Charges.AsNoTracking().CountAsync(c => c.CustomerId == shop.CustomerId)));
        Assert.Equal(0, await WithDbAsync(db => db.StockReservationEntries.AsNoTracking().CountAsync(e => e.CustomerId == shop.CustomerId)));
        Assert.Equal(0, await NotificationCountAsync(shop.CustomerId, "order.confirmed"));
        Assert.Equal(sellableBefore, await SellableAsync(shop.Context.BranchId, shop.Food.SkuId));

        // the cart is untouched
        Assert.Equal(2, (await ReadAsync(await shop.Customer.GetAsync("/api/cart"))).GetProperty("lines").GetArrayLength());
    }

    // ── I-D9 (US-IMP-08 AC2): real parallelism, real Inventory ──────────────────────────────────────
    [Fact]
    public async Task I_D9_TwentyCustomersPlaceTheLastUnitAtOnce_ExactlyOneOrderIsCreated()
    {
        var context = await CreateContextAsync();
        await SetBillingProfileAsync(context);
        var sku = await CreateSkuAsync(context, price: 100m, stock: 1m, onStorefront: true);
        var district = NewDistrict();
        await CreateZoneAsync(context, district);

        var buyers = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            var (client, customerId, _) = await RegisterAndSignInCustomerAsync();
            var addressId = await AddAddressAsync(client, district);
            await AcceptPendingPoliciesAsync(client);
            await ExpectAsync(HttpStatusCode.Created, await client.PostAsJsonAsync("/api/cart/lines", new { skuId = sku.SkuId, quantity = 1 }, Json));
            var shop = new Shop(context, district, Guid.Empty, client, customerId, addressId, sku, sku);
            return (Shop: shop, Quote: await QuoteAsync(client, QuoteUrl(shop)));
        }));

        // every buyer holds a valid quote; now all of them place, together
        var gate = new TaskCompletionSource();
        var attempts = buyers.Select(async buyer =>
        {
            await gate.Task;
            return await buyer.Shop.Customer.SendAsync(PostOrder(PlaceBody(buyer.Shop, buyer.Quote)));
        }).ToList();
        gate.SetResult();
        var responses = await Task.WhenAll(attempts);

        var created = responses.Where(r => r.StatusCode == HttpStatusCode.Created).ToList();
        Assert.True(created.Count == 1, "Statuses: " + string.Join(", ", responses.Select(r => (int)r.StatusCode)));
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            // A loser either reaches the stock check after the winner took the unit (StockNoLongerAvailable) or recomputes a quote that no longer matches theirs
            // because the unit is gone (CheckoutQuoteChanged); both are the same business answer, 409, and neither creates an order.
            Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
            var codes = await ErrorCodesAsync(refused);
            Assert.True(codes.Contains("StockNoLongerAvailable") || codes.Contains("CheckoutQuoteChanged"), "Codes: " + string.Join(",", codes));
        }

        var customerIds = buyers.Select(b => b.Shop.CustomerId).ToList();
        Assert.Equal(1, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId != null && customerIds.Contains(o.CustomerId.Value))));
        Assert.Equal(0m, await SellableAsync(context.BranchId, sku.SkuId));
    }

    // ── I-D10 / I-D11 (US-ORD-18 AC1) ───────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D10_ADoubleSubmitWithOneKey_CreatesOneOrder_AndALateRepeatReplaysTheSameAnswer()
    {
        var shop = await SetUpShopAsync();
        object body;
        Guid key;
        HttpResponseMessage[] both;

        // Another test class may publish a policy that needs acceptance while this one runs (I-D13); that is setup noise here, so re-accept and go again (nothing was placed).
        for (var attempt = 1; ; attempt++)
        {
            await AcceptPendingPoliciesAsync(shop.Customer);
            var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop));
            body = PlaceBody(shop, quote);
            key = Guid.NewGuid();

            both = await Task.WhenAll(
                Task.Run(() => shop.Customer.SendAsync(PostOrder(body, key))),
                Task.Run(() => shop.Customer.SendAsync(PostOrder(body, key))));

            if (both.Any(r => r.StatusCode == HttpStatusCode.Created) || attempt == 4) break;
            if (!(await Task.WhenAll(both.Select(async r => await r.Content.ReadAsStringAsync()))).Any(b => b.Contains("PolicyAcceptanceRequired"))) break;
        }

        Assert.True(both.Any(r => r.StatusCode == HttpStatusCode.Created && !r.Headers.Contains("Idempotent-Replayed")),
            "No original 201: " + string.Join(" | ", await Task.WhenAll(both.Select(async r => $"{(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}"))));
        foreach (var other in both.Where(r => r.Headers.Contains("Idempotent-Replayed") || r.StatusCode != HttpStatusCode.Created))
            Assert.True(other.StatusCode == HttpStatusCode.Conflict || other.StatusCode == HttpStatusCode.Created, $"Unexpected {(int)other.StatusCode}");

        Assert.Equal(1, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == shop.CustomerId)));

        var original = both.First(r => r.StatusCode == HttpStatusCode.Created && !r.Headers.Contains("Idempotent-Replayed"));
        var orderId = (await ReadAsync(original)).GetProperty("orderId").GetGuid();

        var third = await shop.Customer.SendAsync(PostOrder(body, key));
        await ExpectAsync(HttpStatusCode.Created, third);
        Assert.True(third.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(orderId, (await ReadAsync(third)).GetProperty("orderId").GetGuid());
        Assert.Equal(1, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == shop.CustomerId)));
    }

    [Fact]
    public async Task I_D11_TheSameKeyWithADifferentBody_IsRefused()
    {
        var shop = await SetUpShopAsync();
        await AcceptPendingPoliciesAsync(shop.Customer);
        var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop));
        var key = Guid.NewGuid();

        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, quote), key)));
        var different = await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, quote, recipientName: "Someone Else"), key));

        await AssertErrorAsync(different, "IdempotencyKeyReused", (HttpStatusCode)422);
    }

    [Fact]
    public async Task APlacementWithoutAnIdempotencyKey_IsRefused_AndOnlyACustomerMayPlace()
    {
        var shop = await SetUpShopAsync();
        var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop));

        var noKey = new HttpRequestMessage(HttpMethod.Post, "/api/customers/me/orders") { Content = JsonContent.Create(PlaceBody(shop, quote), options: Json) };
        await AssertErrorAsync(await shop.Customer.SendAsync(noKey), "IdempotencyKeyRequired");

        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient().SendAsync(PostOrder(PlaceBody(shop, quote)))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.Context.Admin.SendAsync(PostOrder(PlaceBody(shop, quote)))).StatusCode);
    }

    // ── I-D12 (US-ORD-05): a code's last use ────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D12_TwoCustomersRaceForACodesLastUse_OneAppliesTheOtherMustRequote()
    {
        var context = await CreateContextAsync();
        await SetBillingProfileAsync(context);
        var sku = await CreateSkuAsync(context, price: 100m, stock: 20m, onStorefront: true);
        var district = NewDistrict();
        await CreateZoneAsync(context, district);
        var discount = await CreateDiscountAsync(context, percent: 10m, usageLimit: 1);

        var buyers = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            var (client, customerId, _) = await RegisterAndSignInCustomerAsync();
            var addressId = await AddAddressAsync(client, district);
            await AcceptPendingPoliciesAsync(client);
            await ExpectAsync(HttpStatusCode.Created, await client.PostAsJsonAsync("/api/cart/lines", new { skuId = sku.SkuId, quantity = 1 }, Json));
            var shop = new Shop(context, district, Guid.Empty, client, customerId, addressId, sku, sku);
            return (Shop: shop, Quote: await QuoteAsync(client, QuoteUrl(shop, code: discount.Code)));
        }));

        Assert.All(buyers, b => Assert.True(b.Quote.GetProperty("discount").GetProperty("applied").GetBoolean()));

        var gate = new TaskCompletionSource();
        var attempts = buyers.Select(async b =>
        {
            await gate.Task;
            return await b.Shop.Customer.SendAsync(PostOrder(PlaceBody(b.Shop, b.Quote, code: discount.Code)));
        }).ToList();
        gate.SetResult();
        var responses = await Task.WhenAll(attempts);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var loser = responses.Single(r => r.StatusCode != HttpStatusCode.Created);
        await AssertErrorAsync(loser, "CheckoutQuoteChanged", HttpStatusCode.Conflict);

        // the code was redeemed exactly once, and the loser's re-quote says why
        Assert.Equal(1, await WithDbAsync(db => db.DiscountCodes.AsNoTracking().Where(c => c.Id == discount.Id).Select(c => c.TimesRedeemed).SingleAsync()));
        var loserBuyer = buyers[Array.IndexOf(responses, loser)];
        var requote = await QuoteAsync(loserBuyer.Shop.Customer, QuoteUrl(loserBuyer.Shop, code: discount.Code));
        Assert.False(requote.GetProperty("discount").GetProperty("applied").GetBoolean());
        Assert.Equal("DiscountUsageLimitReached", requote.GetProperty("discount").GetProperty("reasonCode").GetString());
    }

    // ── I-D13 (US-ADM-07 AC2, checkout half) ────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D13_APendingTermsVersion_BlocksPlacement_UntilTheCustomerAcceptsIt()
    {
        var shop = await SetUpShopAsync();
        var key = "ord-place-terms-" + Guid.NewGuid().ToString("N")[..10];
        await ExpectAsync(HttpStatusCode.Created, await shop.Context.Admin.PostAsJsonAsync("/api/admin/policies", new { key, title = "Terms of sale", requiresAcceptance = true }, Json));
        await ExpectAsync(HttpStatusCode.Created, await shop.Context.Admin.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1 terms", changeSummary = "Initial" }, Json));

        var blocked = await QuoteAsync(shop.Customer, QuoteUrl(shop));
        await AssertErrorAsync(await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, blocked))), "PolicyAcceptanceRequired");

        await AcceptPendingPoliciesAsync(shop.Customer);
        var accepted = await QuoteAsync(shop.Customer, QuoteUrl(shop));
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, accepted))));
    }

    // ── I-D5 / I-D5b: prescription lines against the REAL HO-PHM-001 IDietAuthorisationGate ─────────
    /// <summary>A shop whose customer has a cat and one prescription line in the cart, authorised by a real diet authorisation unless told otherwise.</summary>
    private async Task<(Shop Shop, OrderingSku Rx, Guid CatId, Guid? AuthorisationId)> SetUpPrescriptionShopAsync(bool authorise = true)
    {
        var shop = await SetUpShopAsync(fillCart: false);
        var rx = await CreateSkuAsync(shop.Context, price: 900m, stock: 25m, food: true, prescription: true, onStorefront: true, name: "Renal diet " + NewCode());
        var cat = await SeedCatAsync(shop.CustomerId);
        Guid? authorisationId = authorise ? await AuthoriseDietAsync(cat, shop.CustomerId, rx.ProductId, rx.Name) : null;

        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = rx.SkuId, quantity = 1, catId = cat }, Json));
        return (shop, rx, cat, authorisationId);
    }

    [Fact]
    public async Task I_D5_APrescriptionLineWithoutACat_CannotBePlaced()
    {
        var (shop, _, _, _) = await SetUpPrescriptionShopAsync();

        // the cart refuses such a line at add time, so stand the cart in the state only a lost cat can reach: the line is there and its cat is not
        await using (var connection = await OpenConnectionAsync())
        {
            await using var command = new Npgsql.NpgsqlCommand(
                "UPDATE ordering.cart_line SET cat_id = NULL WHERE cart_id IN (SELECT \"Id\" FROM ordering.cart WHERE customer_id = @c AND status = 1)", connection);
            command.Parameters.AddWithValue("c", shop.CustomerId);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        await AcceptPendingPoliciesAsync(shop.Customer);
        var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop));
        Assert.False(quote.GetProperty("canPlace").GetBoolean());
        Assert.Contains("PrescriptionCatRequired", quote.GetProperty("blockingReasons").EnumerateArray().Select(r => r.GetString()));

        await AssertErrorAsync(await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, quote))), "PrescriptionCatRequired");
        Assert.Equal(0, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == shop.CustomerId)));
    }

    [Fact]
    public async Task I_D5_AnAuthorisedPrescriptionLine_IsPlaced_WhenTheRealGateSaysAuthorised()
    {
        var (shop, rx, cat, _) = await SetUpPrescriptionShopAsync();

        var placed = await PlaceCodOrderAsync(shop);

        var order = await LoadOrderAsync(placed.GetProperty("orderId").GetGuid());
        var line = Assert.Single(order.Lines);
        Assert.Equal(rx.SkuId, line.SkuId);
        Assert.Equal(cat, line.CatId);
    }

    [Fact]
    public async Task I_D5b_ALapsedAuthorisation_BlocksThePrescriptionLineAtPlacement()
    {
        var (shop, rx, _, authorisationId) = await SetUpPrescriptionShopAsync();
        await AcceptPendingPoliciesAsync(shop.Customer);
        var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop));
        Assert.True(quote.GetProperty("canPlace").GetBoolean());

        // the authorisation lapses between the quote and the placement (US-SHP-10 AC3)
        await LapseDietAuthorisationAsync(authorisationId!.Value);

        // the old quote can no longer be placed, and nothing is ordered
        await AssertErrorAsync(await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, quote))), "CheckoutQuoteChanged", HttpStatusCode.Conflict);
        Assert.Equal(0, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == shop.CustomerId)));

        // a fresh quote names the line as excluded because its authorisation ended, and has nothing left to place
        var fresh = await QuoteAsync(shop.Customer, QuoteUrl(shop));
        var excluded = Assert.Single(fresh.GetProperty("excludedLines").EnumerateArray());
        Assert.Equal(rx.SkuId, excluded.GetProperty("skuId").GetGuid());
        Assert.Equal("PrescriptionAuthorisationExpired", excluded.GetProperty("reasonCode").GetString());
        Assert.False(fresh.GetProperty("canPlace").GetBoolean());
        await AssertErrorAsync(await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, fresh))), "NothingToPlace");
    }

    // ── I-E1 (US-ORD-19 AC1): the transfer order placed ─────────────────────────────────────────────
    [Fact]
    public async Task I_E1_ABankTransferOrder_IsHeldPendingPayment_WithStockReserved_AndTheBankDetailsShown()
    {
        var shop = await SetUpShopAsync();
        await SetBankTransferAccountAsync(shop.Context.Admin);
        await AcceptPendingPoliciesAsync(shop.Customer);
        var sellableBefore = await SellableAsync(shop.Context.BranchId, shop.Food.SkuId);

        var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop, payment: "BankTransfer"));
        var response = await ExpectAsync(HttpStatusCode.Created, await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, quote, payment: "BankTransfer"))));
        var placed = await ReadAsync(response);

        Assert.Equal("PendingPayment", placed.GetProperty("status").GetString());
        var instructions = placed.GetProperty("transferInstructions");
        Assert.Equal("1234567890", instructions.GetProperty("accountNumber").GetString());
        Assert.Equal(placed.GetProperty("orderNumber").GetString(), instructions.GetProperty("paymentReference").GetString());
        Assert.Equal("2135.00", instructions.GetProperty("amountDue").GetProperty("amount").GetString());
        Assert.True(placed.GetProperty("paymentDueBy").GetDateTimeOffset() > Now.AddHours(47));

        var order = await LoadOrderAsync(placed.GetProperty("orderId").GetGuid());
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Null(order.InvoiceId);                                          // the invoice waits for the confirmed payment
        Assert.True(order.IsFullyAllocated);
        Assert.Equal(sellableBefore - 2m, await SellableAsync(shop.Context.BranchId, shop.Food.SkuId));
        Assert.Equal(1, await NotificationCountAsync(shop.CustomerId, "order.awaiting-transfer"));
        Assert.Equal(0, await NotificationCountAsync(shop.CustomerId, "order.confirmed"));
        Assert.Equal(0, await WithDbAsync(db => db.Charges.AsNoTracking().CountAsync(c => c.CustomerId == shop.CustomerId)));
    }


    // ── I-D14 (US-ORD-07 AC3, C-5): no card data anywhere ───────────────────────────────────────────
    [Fact]
    public async Task I_D14_NoOrderingColumnCanHoldACardNumberOrAPaymentToken_AndNoStoredBodyHoldsACardLikeNumber()
    {
        var shop = await SetUpShopAsync();
        await PlaceCodOrderAsync(shop);

        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT table_schema || '.' || table_name || '.' || column_name FROM information_schema.columns WHERE table_schema IN ('ordering', 'ops')";
        var columns = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) columns.Add(reader.GetString(0));

        // a payment-card or token column by name: card / pan / cvv / cvc / wallet, or a payment token. The cart's guest cookie hash (SHA-256 of an opaque
        // cart cookie, ADR-199) is the one "token" in the schema and is not a payment token.
        var suspicious = columns.Where(c =>
            Regex.IsMatch(c.Split('.')[2], @"(^|_)(card|pan|cvv|cvc|wallet|pin)(_|$)|payment_token|card_token", RegexOptions.IgnoreCase)
            || (Regex.IsMatch(c.Split('.')[2], @"token", RegexOptions.IgnoreCase) && !c.EndsWith(".guest_token_hash", StringComparison.Ordinal))).ToList();
        Assert.Empty(suspicious);

        // the bodies the idempotency edge replays hold no 13-19 digit run (a card number)
        var raw = await WithDbAsync(db => db.IdempotencyRecords.AsNoTracking().Where(r => r.ResponseBody != null).Select(r => r.ResponseBody!).ToListAsync());
        var bodies = raw.Select(System.Text.Encoding.UTF8.GetString).ToList();

        // A ProblemDetails body (a 404 or 415 from the framework) carries a random hex trace id; its digits can, by chance, run past 13 characters between letters. That is not a
        // card number, and with every area's rows in the shared database the chance adds up -- so the trace id is removed before the scan.
        static string WithoutTraceId(string body) => Regex.Replace(body, "\"traceId\":\"[^\"]*\"", string.Empty);
        Assert.DoesNotContain(bodies, body => Regex.IsMatch(WithoutTraceId(body), @"(?<!\d)\d{13,19}(?!\d)"));
    }

    // ── cross-cutting: the right door ───────────────────────────────────────────────────────────────
    [Fact]
    public void I_D15_PlacementIsAStorefrontEndpoint_TheMaintenanceGate_ButTheStaffAndPortalReadsAreNot()
    {
        var controller = typeof(CatEcommerce.Api.Controllers.CustomerOrdersController);
        var place = controller.GetMethod(nameof(CatEcommerce.Api.Controllers.CustomerOrdersController.Place))!;

        Assert.NotEmpty(place.GetCustomAttributes(typeof(CatEcommerce.Api.Authorization.StorefrontEndpointAttribute), inherit: true));
        Assert.Empty(controller.GetCustomAttributes(typeof(CatEcommerce.Api.Authorization.StorefrontEndpointAttribute), inherit: true)); // the controller itself is not gated: reads and cancel stay up
    }
}
