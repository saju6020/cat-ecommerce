using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 Phase D (quote half) -- <c>GET /api/customers/me/checkout/quote</c> through real HTTP: I-D1 (price rise and the fingerprint), I-D2 (every
/// component of the price), I-D3 (outside every zone, pickup offered), I-D4 (slots), I-D6 (cash on delivery hidden with its reason), the discount reasons
/// (I-D12's quote half) and I-D13's quote half (a pending policy). Placement (I-D7 to I-D12) is built after the Billing seam is decided -- HO-ORD-001 §1 Amended.
/// </summary>
public sealed class OrderingCheckoutQuoteEndpointTests(OrderingApiFactory factory) : OrderingIntegrationTestBase(factory)
{
    private sealed record Shop(
        OrderingContext Context, string District, Guid ZoneId, HttpClient Customer, Guid CustomerId, Guid AddressId, OrderingSku Food, OrderingSku Toy);

    /// <summary>One branch with a 15 % food (850.00) and a 0 % toy (120.00), stocked at the branch and at the storefront; a Standard-60.00 zone for a district; and a
    /// signed-in customer with an address in it, every pending policy accepted and 2 x food + 1 x toy in the cart -- the worked cart of U-D1.</summary>
    private async Task<Shop> SetUpAsync(bool cod = true, decimal? codMax = null, object[]? windows = null, decimal? freeOver = null, int lead = 1, bool fillCart = true)
    {
        var context = await CreateContextAsync();
        var food = await CreateSkuAsync(context, price: 850m, stock: 50m, onStorefront: true, name: "Royal Canin Kitten 2 kg");
        var toy = await CreateSkuAsync(context, price: 120m, stock: 50m, taxClassId: context.ZeroTaxClassId, onStorefront: true, name: "Feather wand");
        var district = NewDistrict();
        var zoneId = await CreateZoneAsync(context, district, cod: cod, codMax: codMax, slotWindows: windows, freeOver: freeOver, leadTimeDays: lead);

        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var addressId = await AddAddressAsync(customer, district);
        await AcceptPendingPoliciesAsync(customer);

        if (fillCart)
        {
            await ExpectAsync(HttpStatusCode.Created, await customer.PostAsJsonAsync("/api/cart/lines", new { skuId = food.SkuId, quantity = 2 }, Json));
            await ExpectAsync(HttpStatusCode.Created, await customer.PostAsJsonAsync("/api/cart/lines", new { skuId = toy.SkuId, quantity = 1 }, Json));
        }

        return new Shop(context, district, zoneId, customer, customerId, addressId, food, toy);
    }

    private static string QuoteUrl(Shop shop, string fulfilment = "Delivery", string? payment = "CashOnDelivery", string? code = null, string? level = null, string? slotDate = null,
        string? slotStart = null, Guid? addressId = null, Guid? pickupBranchId = null)
    {
        var parts = new List<string> { $"fulfilment={fulfilment}" };
        if ((addressId ?? (fulfilment == "Delivery" ? shop.AddressId : null)) is { } address) parts.Add($"addressId={address}");
        if (payment is not null) parts.Add($"paymentMethod={payment}");
        if (code is not null) parts.Add($"discountCode={Uri.EscapeDataString(code)}");
        if (level is not null) parts.Add($"serviceLevel={level}");
        if (slotDate is not null) parts.Add($"slotDate={slotDate}");
        if (slotStart is not null) parts.Add($"slotWindowStart={slotStart}");
        if (pickupBranchId is not null) parts.Add($"pickupBranchId={pickupBranchId}");
        return "/api/customers/me/checkout/quote?" + string.Join('&', parts);
    }

    private static async Task<JsonElement> QuoteAsync(Shop shop, string url) =>
        await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await shop.Customer.GetAsync(url)));

    private static string Amount(JsonElement quote, string total) => quote.GetProperty("totals").GetProperty(total).GetProperty("amount").GetString()!;

    // ── I-D2 (and U-D1 end to end) ──────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D2_TheQuoteShowsEveryComponentSeparately_AndTheWorkedCartTotals1927_50()
    {
        var shop = await SetUpAsync();
        var code = await CreateDiscountAsync(shop.Context, percent: 10m);

        var quote = await QuoteAsync(shop, QuoteUrl(shop, code: code.Code));

        Assert.Equal("1820.00", Amount(quote, "subtotal"));
        Assert.Equal("0.00", Amount(quote, "lineDiscountTotal"));
        Assert.Equal("182.00", Amount(quote, "orderDiscount"));
        Assert.Equal("60.00", Amount(quote, "deliveryCharge"));
        Assert.Equal("229.50", Amount(quote, "taxTotal"));
        Assert.Equal("1927.50", Amount(quote, "grandTotal"));
        Assert.Equal("BDT", quote.GetProperty("totals").GetProperty("grandTotal").GetProperty("currencyCode").GetString());

        var lines = quote.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("skuId").GetGuid());
        var food = lines[shop.Food.SkuId];
        Assert.Equal(2, food.GetProperty("quantity").GetInt32());
        Assert.Equal("850.00", food.GetProperty("unitPrice").GetProperty("amount").GetString());
        Assert.Equal("1700.00", food.GetProperty("lineSubtotal").GetProperty("amount").GetString());
        Assert.Equal("170.00", food.GetProperty("orderDiscountShare").GetProperty("amount").GetString());
        Assert.Equal(15m, food.GetProperty("taxRatePercent").GetDecimal());
        Assert.Equal("229.50", food.GetProperty("taxAmount").GetProperty("amount").GetString());
        Assert.Equal("1759.50", food.GetProperty("lineTotal").GetProperty("amount").GetString());
        Assert.Equal("0.00", lines[shop.Toy.SkuId].GetProperty("taxAmount").GetProperty("amount").GetString());

        Assert.True(quote.GetProperty("discount").GetProperty("applied").GetBoolean());
        Assert.Equal(code.Code, quote.GetProperty("discount").GetProperty("code").GetString());
        Assert.True(quote.GetProperty("canPlace").GetBoolean());
        Assert.Equal(0, quote.GetProperty("blockingReasons").GetArrayLength());
        Assert.Equal(0, quote.GetProperty("changes").GetArrayLength());
        Assert.Equal(64, quote.GetProperty("fingerprint").GetString()!.Length);
        Assert.Equal("Standard", quote.GetProperty("selectedServiceLevel").GetString());
    }

    [Fact]
    public async Task TheQuote_ListsTheZoneAndBothServiceLevels_WithTheirCharges()
    {
        var shop = await SetUpAsync();
        var quote = await QuoteAsync(shop, QuoteUrl(shop));

        var delivery = quote.GetProperty("delivery");
        Assert.True(delivery.GetProperty("available").GetBoolean());
        Assert.Equal(shop.ZoneId, delivery.GetProperty("zoneId").GetGuid());
        var levels = delivery.GetProperty("serviceLevels").EnumerateArray().ToDictionary(l => l.GetProperty("level").GetString()!);
        Assert.Equal("60.00", levels["Standard"].GetProperty("charge").GetProperty("amount").GetString());
        Assert.Equal("150.00", levels["Express"].GetProperty("charge").GetProperty("amount").GetString());

        var express = await QuoteAsync(shop, QuoteUrl(shop, level: "Express"));
        Assert.Equal("150.00", Amount(express, "deliveryCharge"));
        Assert.NotEqual(quote.GetProperty("fingerprint").GetString(), express.GetProperty("fingerprint").GetString());
    }

    [Fact]
    public async Task TheDeliveryCharge_IsFreeOverTheThreshold()
    {
        var shop = await SetUpAsync(freeOver: 1500m);
        var quote = await QuoteAsync(shop, QuoteUrl(shop));

        Assert.Equal("0.00", Amount(quote, "deliveryCharge")); // 1820.00 of goods >= 1500.00
    }

    // ── I-D1 (the quote half) ───────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D1_APriceRise_IsListedAsAChange_AndTheFingerprintChanges_ThenIsStableOnceAcknowledged()
    {
        var shop = await SetUpAsync();
        var before = await QuoteAsync(shop, QuoteUrl(shop));
        var again = await QuoteAsync(shop, QuoteUrl(shop));
        Assert.Equal(before.GetProperty("fingerprint").GetString(), again.GetProperty("fingerprint").GetString());

        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        await SchedulePriceAsync(shop.Context.Admin, shop.Food.SkuId, 900m);

        var after = await QuoteAsync(shop, QuoteUrl(shop));
        var change = Assert.Single(after.GetProperty("changes").EnumerateArray());
        Assert.Equal("PriceIncreased", change.GetProperty("kind").GetString());
        Assert.Equal("850.00", change.GetProperty("before").GetString());
        Assert.Equal("900.00", change.GetProperty("after").GetString());
        Assert.Equal(shop.Food.Name, change.GetProperty("productName").GetString());
        Assert.Contains("increased", change.GetProperty("message").GetString());
        Assert.NotEqual(before.GetProperty("fingerprint").GetString(), after.GetProperty("fingerprint").GetString());
        Assert.Equal("1800.00", after.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("skuId").GetGuid() == shop.Food.SkuId).GetProperty("lineSubtotal").GetProperty("amount").GetString());

        var acknowledged = await QuoteAsync(shop, QuoteUrl(shop));
        Assert.Equal(after.GetProperty("fingerprint").GetString(), acknowledged.GetProperty("fingerprint").GetString());
    }

    [Fact]
    public async Task TheFingerprint_ChangesWithEveryChoice_TheMethodTheLevelTheAddress()
    {
        var shop = await SetUpAsync();
        var otherAddress = await AddAddressAsync(shop.Customer, shop.District, "1213");

        string Fingerprint(JsonElement quote) => quote.GetProperty("fingerprint").GetString()!;

        var cod = Fingerprint(await QuoteAsync(shop, QuoteUrl(shop)));
        var transfer = Fingerprint(await QuoteAsync(shop, QuoteUrl(shop, payment: "BankTransfer")));
        var address = Fingerprint(await QuoteAsync(shop, QuoteUrl(shop, addressId: otherAddress)));

        Assert.Equal(3, new[] { cod, transfer, address }.Distinct().Count());
    }

    [Fact]
    public async Task AStockOfTwoAgainstAThree_ReducesTheQuantity_AtTheFulfillingBranch()
    {
        var context = await CreateContextAsync();
        var scarce = await CreateSkuAsync(context, price: 100m, stock: 2m, onStorefront: false);
        await StockAtStorefrontAsync(scarce.SkuId, 50m); // the storefront shows plenty; the fulfilling branch has two
        var district = NewDistrict();
        await CreateZoneAsync(context, district);
        var (customer, _, _) = await RegisterAndSignInCustomerAsync();
        var addressId = await AddAddressAsync(customer, district);
        await AcceptPendingPoliciesAsync(customer);
        await ExpectAsync(HttpStatusCode.Created, await customer.PostAsJsonAsync("/api/cart/lines", new { skuId = scarce.SkuId, quantity = 3 }, Json));

        var quote = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await customer.GetAsync($"/api/customers/me/checkout/quote?fulfilment=Delivery&addressId={addressId}&paymentMethod=CashOnDelivery")));

        Assert.Equal(2, quote.GetProperty("lines")[0].GetProperty("quantity").GetInt32());
        var change = Assert.Single(quote.GetProperty("changes").EnumerateArray());
        Assert.Equal("QuantityReduced", change.GetProperty("kind").GetString());
        Assert.Equal("3", change.GetProperty("before").GetString());
        Assert.Equal("2", change.GetProperty("after").GetString());
    }

    [Fact]
    public async Task ASkuThatIsNoLongerPurchasable_IsExcludedAndTheRestStaysPlaceable()
    {
        var shop = await SetUpAsync();
        // the shop manager unpublishes the toy after the customer added it
        var staff = shop.Context.Admin;
        await ExpectAsync(HttpStatusCode.NoContent, await staff.PostAsJsonAsync($"/api/staff/shop/products/{shop.Toy.ProductId}/unpublish",
            new { expectedVersion = await ProductVersionAsync(staff, shop.Toy.ProductId) }, Json));

        var quote = await QuoteAsync(shop, QuoteUrl(shop));

        var excluded = Assert.Single(quote.GetProperty("excludedLines").EnumerateArray());
        Assert.Equal(shop.Toy.SkuId, excluded.GetProperty("skuId").GetGuid());
        Assert.Equal("NowUnavailable", excluded.GetProperty("reasonCode").GetString());
        Assert.Contains(quote.GetProperty("changes").EnumerateArray(), c => c.GetProperty("kind").GetString() == "NowUnavailable");
        Assert.Single(quote.GetProperty("lines").EnumerateArray());
        Assert.True(quote.GetProperty("canPlace").GetBoolean());
        Assert.Equal("1700.00", Amount(quote, "subtotal"));
    }

    [Fact]
    public async Task AnEmptyCart_IsNotPlaceable()
    {
        var shop = await SetUpAsync(fillCart: false);
        var quote = await QuoteAsync(shop, QuoteUrl(shop));

        Assert.False(quote.GetProperty("canPlace").GetBoolean());
        Assert.Contains("CartEmpty", quote.GetProperty("blockingReasons").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal("0.00", Amount(quote, "grandTotal"));
    }

    // ── I-D3 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D3_AnAddressOutsideEveryZone_MakesDeliveryUnavailable_AndPickupIsOffered()
    {
        var shop = await SetUpAsync();
        var elsewhere = await AddAddressAsync(shop.Customer, NewDistrict());

        var delivery = await QuoteAsync(shop, QuoteUrl(shop, addressId: elsewhere));
        Assert.False(delivery.GetProperty("delivery").GetProperty("available").GetBoolean());
        Assert.Equal("AddressOutsideServedZones", delivery.GetProperty("delivery").GetProperty("unavailableReason").GetString());
        Assert.False(delivery.GetProperty("canPlace").GetBoolean());
        Assert.Contains("AddressOutsideServedZones", delivery.GetProperty("blockingReasons").EnumerateArray().Select(r => r.GetString()));

        var branch = delivery.GetProperty("pickupBranches").EnumerateArray().First(b => b.GetProperty("id").GetGuid() == shop.Context.BranchId);
        Assert.Contains(shop.Context.BranchCode, branch.GetProperty("name").GetString() ?? "");

        // the same cart as a pickup order is placeable, and costs no delivery
        var pickup = await QuoteAsync(shop, QuoteUrl(shop, "Pickup", "PayOnCollection", addressId: elsewhere, pickupBranchId: shop.Context.BranchId));
        Assert.True(pickup.GetProperty("canPlace").GetBoolean());
        Assert.Equal("0.00", Amount(pickup, "deliveryCharge"));
        Assert.Equal("1820.00", Amount(pickup, "subtotal"));
    }

    [Fact]
    public async Task APickupOrder_NeedsAnOfferedBranch()
    {
        var shop = await SetUpAsync();

        var none = await QuoteAsync(shop, QuoteUrl(shop, "Pickup", "PayOnCollection"));
        Assert.Contains("PickupBranchUnavailable", none.GetProperty("blockingReasons").EnumerateArray().Select(r => r.GetString()));

        var bogus = await QuoteAsync(shop, QuoteUrl(shop, "Pickup", "PayOnCollection", pickupBranchId: Guid.NewGuid()));
        Assert.False(bogus.GetProperty("canPlace").GetBoolean());
    }

    // ── I-D4 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D4_ASlotTheServiceOffers_IsAccepted_AnotherIsDeliverySlotUnavailable()
    {
        var windows = Enum.GetNames<DayOfWeek>().Select(d => (object)new { dayOfWeek = d, start = "10:00", end = "13:00" }).ToArray();
        var shop = await SetUpAsync(windows: windows, lead: 1);
        await OpenBranchEveryDayAsync(shop.Context);

        var open = await QuoteAsync(shop, QuoteUrl(shop));
        var standard = open.GetProperty("delivery").GetProperty("serviceLevels").EnumerateArray().First(l => l.GetProperty("level").GetString() == "Standard");
        var slots = standard.GetProperty("slots").EnumerateArray().ToList();
        Assert.Equal(7, slots.Count);
        Assert.Equal("10:00–13:00", slots[0].GetProperty("label").GetString());
        var tomorrow = Today.AddDays(1).ToString("yyyy-MM-dd");
        Assert.Equal(tomorrow, slots[0].GetProperty("date").GetString()); // lead time 1 day

        var chosen = await QuoteAsync(shop, QuoteUrl(shop, slotDate: tomorrow, slotStart: "10:00"));
        Assert.True(chosen.GetProperty("canPlace").GetBoolean());

        var wrongWindow = await QuoteAsync(shop, QuoteUrl(shop, slotDate: tomorrow, slotStart: "11:00"));
        Assert.Contains("DeliverySlotUnavailable", wrongWindow.GetProperty("blockingReasons").EnumerateArray().Select(r => r.GetString()));

        var today = await QuoteAsync(shop, QuoteUrl(shop, slotDate: Today.ToString("yyyy-MM-dd"), slotStart: "10:00")); // before the lead time
        Assert.False(today.GetProperty("canPlace").GetBoolean());
    }

    [Fact]
    public async Task ABranchWithNoHoursSet_OffersNoSlotChoice()
    {
        var windows = Enum.GetNames<DayOfWeek>().Select(d => (object)new { dayOfWeek = d, start = "10:00", end = "13:00" }).ToArray();
        var shop = await SetUpAsync(windows: windows);

        var quote = await QuoteAsync(shop, QuoteUrl(shop));
        var standard = quote.GetProperty("delivery").GetProperty("serviceLevels").EnumerateArray().First(l => l.GetProperty("level").GetString() == "Standard");

        Assert.Equal(0, standard.GetProperty("slots").GetArrayLength()); // a new branch is closed until its hours are set (HO-ADM-001 §8.2)
        Assert.True(quote.GetProperty("canPlace").GetBoolean()); // slots are optional preference windows
    }

    // ── I-D6 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D6_CodInAZoneWithoutCod_IsHiddenWithItsReason_AndChoosingItBlocks()
    {
        var shop = await SetUpAsync(cod: false);
        var quote = await QuoteAsync(shop, QuoteUrl(shop));

        var options = quote.GetProperty("paymentOptions").EnumerateArray().ToDictionary(o => o.GetProperty("method").GetString()!);
        Assert.False(options["CashOnDelivery"].GetProperty("available").GetBoolean());
        Assert.Equal("CodNotOfferedInZone", options["CashOnDelivery"].GetProperty("unavailableReason").GetString());
        Assert.False(options["PayOnCollection"].GetProperty("available").GetBoolean());
        Assert.Equal("NotForDelivery", options["PayOnCollection"].GetProperty("unavailableReason").GetString());
        Assert.Contains("PaymentMethodUnavailable", quote.GetProperty("blockingReasons").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task I_D6_CodAboveTheCeiling_IsHiddenAndNamesTheLimit()
    {
        var shop = await SetUpAsync(codMax: 1000m);
        var quote = await QuoteAsync(shop, QuoteUrl(shop));

        var cod = quote.GetProperty("paymentOptions").EnumerateArray().Single(o => o.GetProperty("method").GetString() == "CashOnDelivery");
        Assert.False(cod.GetProperty("available").GetBoolean());
        Assert.Equal("CodAboveOrderLimit", cod.GetProperty("unavailableReason").GetString());
        Assert.Contains("BDT 1,000.00", cod.GetProperty("message").GetString());
        Assert.False(quote.GetProperty("canPlace").GetBoolean());

        // choosing bank transfer instead is fine -- but only once the shop has an active bank account
        var transfer = await QuoteAsync(shop, QuoteUrl(shop, payment: "BankTransfer"));
        var transferOption = transfer.GetProperty("paymentOptions").EnumerateArray().Single(o => o.GetProperty("method").GetString() == "BankTransfer");
        Assert.Equal(transferOption.GetProperty("available").GetBoolean(), transfer.GetProperty("canPlace").GetBoolean());
    }

    /// <summary>
    /// HO-ORD-001 CODE_REVIEW backlog 6. The HTTP-level <c>BankTransferDisabled</c> rule, proved through the branch's own <c>orders.bankTransferEnabled</c> override -- a per-branch setting the
    /// test owns -- instead of the shop's one global bank-account row, which parallel test classes switch on (flipping it here would race them). The account half of the rule
    /// (<c>BankAccountActive</c>) stays covered by U-B5 on <c>PaymentOptionPolicy</c>.
    /// </summary>
    [Fact]
    public async Task BankTransfer_TurnedOffForTheBranch_IsHiddenWithItsReason_AndChoosingItBlocks()
    {
        var shop = await SetUpAsync();
        await ExpectAsync(HttpStatusCode.NoContent, await shop.Context.Admin.PutAsJsonAsync(
            $"/api/admin/settings?branchId={shop.Context.BranchId}", new { changes = new[] { new { key = SettingKeys.OrdersBankTransferEnabled, value = "false" } } }, Json));

        var quote = await QuoteAsync(shop, QuoteUrl(shop, payment: "BankTransfer"));

        var transfer = quote.GetProperty("paymentOptions").EnumerateArray().Single(o => o.GetProperty("method").GetString() == "BankTransfer");
        Assert.False(transfer.GetProperty("available").GetBoolean());
        Assert.Equal("BankTransferDisabled", transfer.GetProperty("unavailableReason").GetString());
        Assert.False(quote.GetProperty("canPlace").GetBoolean());
        Assert.Contains("PaymentMethodUnavailable", quote.GetProperty("blockingReasons").EnumerateArray().Select(r => r.GetString()));
    }

    // ── discount reasons (U-B6 end to end; the usage-limit race is I-D12, built with placement) ─────
    [Fact]
    public async Task ADiscountCodeThatDoesNotApply_IsReportedWithItsSpecificReason_AndTheQuoteStillWorks()
    {
        var shop = await SetUpAsync();

        var unknown = await QuoteAsync(shop, QuoteUrl(shop, code: "NO-SUCH-CODE"));
        Assert.False(unknown.GetProperty("discount").GetProperty("applied").GetBoolean());
        Assert.Equal("DiscountCodeNotFound", unknown.GetProperty("discount").GetProperty("reasonCode").GetString());
        Assert.True(unknown.GetProperty("canPlace").GetBoolean());
        Assert.Equal("0.00", Amount(unknown, "orderDiscount"));

        var minimum = await CreateDiscountAsync(shop.Context, minimumSpend: 5000m);
        var minQuote = await QuoteAsync(shop, QuoteUrl(shop, code: minimum.Code));
        Assert.Equal("DiscountMinimumSpendNotMet", minQuote.GetProperty("discount").GetProperty("reasonCode").GetString());
        Assert.Equal("Spend BDT 5,000.00 on eligible items to use this code.", minQuote.GetProperty("discount").GetProperty("message").GetString());

        var expired = await CreateDiscountAsync(shop.Context, validTo: Now.AddDays(-1));
        Assert.Equal("DiscountCodeExpired", (await QuoteAsync(shop, QuoteUrl(shop, code: expired.Code))).GetProperty("discount").GetProperty("reasonCode").GetString());

        var notForThese = await CreateDiscountAsync(shop.Context, scope: "Categories", categoryIds: [2]); // litter; the cart holds food and a toy
        Assert.Equal("DiscountNotApplicableToItems", (await QuoteAsync(shop, QuoteUrl(shop, code: notForThese.Code))).GetProperty("discount").GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task AScopedProductCode_DiscountsOnlyThatLine_AndTaxFollowsTheDiscountedBase()
    {
        var shop = await SetUpAsync();
        var code = await CreateDiscountAsync(shop.Context, percent: 10m, scope: "Products", productIds: [shop.Food.ProductId]);

        var quote = await QuoteAsync(shop, QuoteUrl(shop, code: code.Code));

        Assert.Equal("170.00", Amount(quote, "lineDiscountTotal"));
        Assert.Equal("0.00", Amount(quote, "orderDiscount"));
        Assert.Equal("229.50", Amount(quote, "taxTotal")); // 15 % of (1700.00 - 170.00)
        Assert.Equal("1939.50", Amount(quote, "grandTotal")); // 1820 - 170 + 60 + 229.50
        var food = quote.GetProperty("lines").EnumerateArray().Single(l => l.GetProperty("skuId").GetGuid() == shop.Food.SkuId);
        Assert.Equal("170.00", food.GetProperty("lineDiscount").GetProperty("amount").GetString());
    }

    // ── I-D13 (the quote half) ──────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_D13_APendingTermsVersion_BlocksPlacement_UntilTheCustomerAcceptsIt()
    {
        var shop = await SetUpAsync();
        var key = "ord-terms-" + Guid.NewGuid().ToString("N")[..10];
        var create = await shop.Context.Admin.PostAsJsonAsync("/api/admin/policies", new { key, title = "Terms of sale", requiresAcceptance = true }, Json);
        await ExpectAsync(HttpStatusCode.Created, create);
        await ExpectAsync(HttpStatusCode.Created, await shop.Context.Admin.PostAsJsonAsync($"/api/admin/policies/{key}/versions", new { body = "v1 terms", changeSummary = "Initial" }, Json));

        var blocked = await QuoteAsync(shop, QuoteUrl(shop));
        var pending = blocked.GetProperty("pendingPolicies").EnumerateArray().Single(p => p.GetProperty("key").GetString() == key);
        Assert.Equal("Terms of sale", pending.GetProperty("title").GetString());
        Assert.Equal(1, pending.GetProperty("versionNumber").GetInt32());
        Assert.False(blocked.GetProperty("canPlace").GetBoolean());
        Assert.Contains("PolicyAcceptanceRequired", blocked.GetProperty("blockingReasons").EnumerateArray().Select(r => r.GetString()));

        await AcceptPendingPoliciesAsync(shop.Customer);

        var accepted = await QuoteAsync(shop, QuoteUrl(shop));
        Assert.Equal(0, accepted.GetProperty("pendingPolicies").GetArrayLength());
        Assert.True(accepted.GetProperty("canPlace").GetBoolean());
    }

    // ── the endpoint itself ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task TheQuote_IsForASignedInCustomerOnly_AndRefusesBadParameters()
    {
        var shop = await SetUpAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient().GetAsync("/api/customers/me/checkout/quote?fulfilment=Delivery")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await shop.Context.Admin.GetAsync("/api/customers/me/checkout/quote?fulfilment=Delivery")).StatusCode);

        foreach (var bad in new[]
        {
            "fulfilment=Teleport", "fulfilment=Counter", "fulfilment=Delivery&serviceLevel=Warp", "fulfilment=Delivery&paymentMethod=InStore",
            "fulfilment=Delivery&paymentMethod=BitcoinWallet", "fulfilment=Delivery&slotDate=tomorrow", "fulfilment=Delivery&slotWindowStart=ten",
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await shop.Customer.GetAsync("/api/customers/me/checkout/quote?" + bad)).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await shop.Customer.GetAsync("/api/customers/me/checkout/quote")).StatusCode);
    }

    [Fact]
    public async Task AnotherCustomersAddress_FindsNoAddress_TheQuoteNeverRevealsIt()
    {
        var shop = await SetUpAsync();
        var (stranger, _, _) = await RegisterAndSignInCustomerAsync();
        var strangersAddress = await AddAddressAsync(stranger, shop.District);

        var quote = await QuoteAsync(shop, QuoteUrl(shop, addressId: strangersAddress));

        Assert.False(quote.GetProperty("delivery").GetProperty("available").GetBoolean());
        Assert.Contains("DeliveryAddressNotFound", quote.GetProperty("blockingReasons").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task TheQuote_IsNoStore_AndStoresNothing()
    {
        var shop = await SetUpAsync();
        var response = await ExpectAsync(HttpStatusCode.OK, await shop.Customer.GetAsync(QuoteUrl(shop)));

        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal(0, await WithDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(db.Orders, o => o.CustomerId == shop.CustomerId)));
    }
}
