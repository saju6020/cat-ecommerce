using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Cat;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 Phase C -- the cart through real HTTP: I-C1 (guest cart and its cookie), I-C2 (merge on sign-in), I-C3 (the same cart on another
/// device), I-C4 (prescription lines and their cat; the authorised-cat case runs against the real PHM gate), I-C5 (out of stock).
/// </summary>
public sealed class OrderingCartEndpointTests(OrderingApiFactory factory) : OrderingIntegrationTestBase(factory)
{
    private async Task<OrderingSku> StockedSkuAsync(OrderingContext context, decimal price = 100m, bool prescription = false, string? name = null, bool food = false) =>
        await CreateSkuAsync(context, price: price, stock: 25m, prescription: prescription, name: name, food: food || prescription, onStorefront: true);

    private static async Task<HttpResponseMessage> AddAsync(HttpClient client, Guid skuId, int quantity = 1, Guid? catId = null) =>
        await client.PostAsJsonAsync("/api/cart/lines", new { skuId, quantity, catId }, Json);

    private static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];

    private static async Task<JsonElement> CartAsync(HttpClient client) =>
        await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await client.GetAsync("/api/cart")));

    // ── I-C1 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C1_AGuestAddsTwoSkus_GetsAnHttpOnlySecureLaxCookie_AndTheCartShowsThem()
    {
        var context = await CreateContextAsync();
        var first = await StockedSkuAsync(context, 120m);
        var second = await StockedSkuAsync(context, 80.5m);
        var guest = CreateClient();

        var add = await ExpectAsync(HttpStatusCode.Created, await AddAsync(guest, first.SkuId, 2));
        var cookie = SetCookies(add).Single(c => c.StartsWith("pc_cart=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=", cookie, StringComparison.OrdinalIgnoreCase);

        var added = await ReadAsync(add);
        Assert.NotEqual(Guid.Empty, added.GetProperty("lineId").GetGuid());
        Assert.NotEqual(Guid.Empty, added.GetProperty("cartId").GetGuid());

        // the second add reuses the cookie: no new cookie, same cart
        var secondAdd = await ExpectAsync(HttpStatusCode.Created, await AddAsync(guest, second.SkuId));
        Assert.DoesNotContain(SetCookies(secondAdd), c => c.StartsWith("pc_cart=", StringComparison.Ordinal));
        Assert.Equal(added.GetProperty("cartId").GetGuid(), (await ReadAsync(secondAdd)).GetProperty("cartId").GetGuid());

        var cart = await CartAsync(guest);
        var lines = cart.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(3, cart.GetProperty("itemCount").GetInt32());
        Assert.False(cart.GetProperty("guestCartPending").GetBoolean());
        Assert.Equal(20, cart.GetProperty("maxQuantityPerLine").GetInt32());

        var line = lines.Single(l => l.GetProperty("skuId").GetGuid() == first.SkuId);
        Assert.Equal(first.Name, line.GetProperty("productName").GetString());
        Assert.Equal(2, line.GetProperty("quantity").GetInt32());
        Assert.Equal("120.00", line.GetProperty("unitPrice").GetProperty("amount").GetString());
        Assert.Equal("240.00", line.GetProperty("lineSubtotal").GetProperty("amount").GetString());
        Assert.Equal("InStock", line.GetProperty("availability").GetString());
        Assert.False(line.GetProperty("priceChanged").GetBoolean());
        Assert.Equal("320.50", cart.GetProperty("subtotal").GetProperty("amount").GetString()); // 2 x 120.00 + 1 x 80.50
    }

    [Fact]
    public async Task AnEmptyCart_ForACallerWithNoCookieAndNoSession_SetsNoCookie()
    {
        var response = await ExpectAsync(HttpStatusCode.OK, await CreateClient().GetAsync("/api/cart"));
        var cart = await ReadAsync(response);

        Assert.Empty(SetCookies(response));
        Assert.Equal(JsonValueKind.Null, cart.GetProperty("cartId").ValueKind);
        Assert.Equal(0, cart.GetProperty("lines").GetArrayLength());
        Assert.Equal("0.00", cart.GetProperty("subtotal").GetProperty("amount").GetString());
    }

    [Fact]
    public async Task AForgedOrMalformedCartCookie_FindsNoCart_NeverAnError()
    {
        var client = CreateClient();
        foreach (var value in new[] { "forged", new string('A', 43), "!!!", new string('x', 200) })
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/cart");
            request.Headers.Add("Cookie", $"pc_cart={value}");
            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, (await ReadAsync(response)).GetProperty("lines").GetArrayLength());
        }
    }

    [Fact]
    public async Task TheCartStoresOnlyAHashOfTheCookie_NeverTheToken()
    {
        var context = await CreateContextAsync();
        var sku = await StockedSkuAsync(context);
        var guest = CreateClient();

        var add = await ExpectAsync(HttpStatusCode.Created, await AddAsync(guest, sku.SkuId));
        var token = Uri.UnescapeDataString(SetCookies(add).Single(c => c.StartsWith("pc_cart=")).Split(';')[0]["pc_cart=".Length..]);
        var cartId = (await ReadAsync(add)).GetProperty("cartId").GetGuid();

        var stored = await WithDbAsync(db => db.Carts.AsNoTracking().Where(c => c.Id == cartId).Select(c => c.GuestTokenHash).SingleAsync());
        Assert.Equal(64, stored!.Length);
        Assert.NotEqual(token, stored);
        Assert.DoesNotContain(token, await BodyAsync(add));
    }

    // ── cart editing ────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ALineIsEditedAndRemoved_AndRemovingTwiceIsStillNoContent()
    {
        var context = await CreateContextAsync();
        var sku = await StockedSkuAsync(context);
        var guest = CreateClient();

        var lineId = (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await AddAsync(guest, sku.SkuId)))).GetProperty("lineId").GetGuid();
        Assert.Equal(HttpStatusCode.Created, (await AddAsync(guest, sku.SkuId, 2)).StatusCode); // same SKU: one line, quantities summed
        Assert.Equal(3, (await CartAsync(guest)).GetProperty("lines")[0].GetProperty("quantity").GetInt32());

        await ExpectAsync(HttpStatusCode.NoContent, await guest.PutAsJsonAsync($"/api/cart/lines/{lineId}", new { quantity = 7 }, Json));
        Assert.Equal(7, (await CartAsync(guest)).GetProperty("lines")[0].GetProperty("quantity").GetInt32());

        await AssertErrorAsync(await guest.PutAsJsonAsync($"/api/cart/lines/{lineId}", new { quantity = 21 }, Json), "QuantityOutOfRange");
        await ExpectAsync(HttpStatusCode.BadRequest, await guest.PutAsJsonAsync($"/api/cart/lines/{lineId}", new { quantity = 0 }, Json));

        await ExpectAsync(HttpStatusCode.NoContent, await guest.DeleteAsync($"/api/cart/lines/{lineId}"));
        await ExpectAsync(HttpStatusCode.NoContent, await guest.DeleteAsync($"/api/cart/lines/{lineId}"));
        Assert.Equal(0, (await CartAsync(guest)).GetProperty("lines").GetArrayLength());
    }

    [Fact]
    public async Task AnotherCallersCartLine_DoesNotExist()
    {
        var context = await CreateContextAsync();
        var sku = await StockedSkuAsync(context);
        var owner = CreateClient();
        var stranger = CreateClient();

        var lineId = (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, await AddAsync(owner, sku.SkuId)))).GetProperty("lineId").GetGuid();
        await ExpectAsync(HttpStatusCode.Created, await AddAsync(stranger, sku.SkuId)); // the stranger has a cart of their own

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsJsonAsync($"/api/cart/lines/{lineId}", new { quantity = 2 }, Json)).StatusCode);
        Assert.Equal(1, (await CartAsync(owner)).GetProperty("lines")[0].GetProperty("quantity").GetInt32());

        // deleting a line that is not yours is a quiet no-op, and the owner's line survives
        await ExpectAsync(HttpStatusCode.NoContent, await stranger.DeleteAsync($"/api/cart/lines/{lineId}"));
        Assert.Equal(1, (await CartAsync(owner)).GetProperty("lines").GetArrayLength());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1000)]
    public async Task AnAddWithAnImpossibleQuantity_Is400(int quantity)
    {
        var context = await CreateContextAsync();
        var sku = await StockedSkuAsync(context);

        await ExpectAsync(HttpStatusCode.BadRequest, await AddAsync(CreateClient(), sku.SkuId, quantity));
    }

    [Fact]
    public async Task ThePriceWhenAdded_IsKept_AndTheCartReportsThePriceChange()
    {
        var context = await CreateContextAsync();
        var sku = await StockedSkuAsync(context, 100m);
        var guest = CreateClient();
        await ExpectAsync(HttpStatusCode.Created, await AddAsync(guest, sku.SkuId));

        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        await SchedulePriceAsync(context.Admin, sku.SkuId, 130m);

        var line = (await CartAsync(guest)).GetProperty("lines")[0];
        Assert.True(line.GetProperty("priceChanged").GetBoolean());
        Assert.Equal("100.00", line.GetProperty("priceWhenAdded").GetProperty("amount").GetString());
        Assert.Equal("130.00", line.GetProperty("unitPrice").GetProperty("amount").GetString());
    }

    // ── I-C5 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C5_AnOutOfStockSku_Is400SkuUnavailable()
    {
        var context = await CreateContextAsync();
        var noStock = await CreateSkuAsync(context, stock: 0m);

        await AssertErrorAsync(await AddAsync(CreateClient(), noStock.SkuId), "SkuUnavailable");
    }

    [Fact]
    public async Task AnUnpublishedOrUnknownSku_Is400SkuNotPurchasable()
    {
        var context = await CreateContextAsync();
        var draft = await CreateSkuAsync(context, publish: false, onStorefront: true);

        await AssertErrorAsync(await AddAsync(CreateClient(), draft.SkuId), "SkuNotPurchasable");
        await AssertErrorAsync(await AddAsync(CreateClient(), Guid.NewGuid()), "SkuNotPurchasable");
    }

    // ── I-C2 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C2_AGuestSignsIn_TheCartOffersAMerge_AndTheMergeTakesTheLargerQuantityPerSku()
    {
        var context = await CreateContextAsync();
        var a = await StockedSkuAsync(context, 100m);
        var b = await StockedSkuAsync(context, 50m);
        var c = await StockedSkuAsync(context, 20m);

        // the customer's own cart, from an earlier visit: A x3, C x1
        var (customer, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        await ExpectAsync(HttpStatusCode.Created, await AddAsync(customer, a.SkuId, 3));
        await ExpectAsync(HttpStatusCode.Created, await AddAsync(customer, c.SkuId, 1));

        // the same browser, as a guest: A x2, B x1 -- then it signs in (the guest cookie survives the sign-in)
        var browser = CreateClient();
        await ExpectAsync(HttpStatusCode.Created, await AddAsync(browser, a.SkuId, 2));
        await ExpectAsync(HttpStatusCode.Created, await AddAsync(browser, b.SkuId, 1));
        var signIn = await browser.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = CustomerPassword }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        browser.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var before = await CartAsync(browser);
        Assert.True(before.GetProperty("guestCartPending").GetBoolean());
        Assert.Equal(2, before.GetProperty("lines").GetArrayLength()); // the account cart, untouched by a GET

        var merge = await ExpectAsync(HttpStatusCode.OK, await browser.PostAsync("/api/customers/me/cart/merge", null));
        Assert.Equal(2, (await ReadAsync(merge)).GetProperty("mergedLineCount").GetInt32());
        Assert.Contains(SetCookies(merge), cookie => cookie.StartsWith("pc_cart=;", StringComparison.Ordinal) || cookie.StartsWith("pc_cart=", StringComparison.Ordinal) && cookie.Contains("1970"));

        var after = await CartAsync(browser);
        Assert.False(after.GetProperty("guestCartPending").GetBoolean());
        var lines = after.GetProperty("lines").EnumerateArray().ToDictionary(l => l.GetProperty("skuId").GetGuid(), l => l.GetProperty("quantity").GetInt32());
        Assert.Equal(3, lines.Count); // one line per SKU, no duplicate
        Assert.Equal(3, lines[a.SkuId]); // the larger of 2 and 3 -- not 5
        Assert.Equal(1, lines[b.SkuId]);
        Assert.Equal(1, lines[c.SkuId]);

        // the guest cart is Merged, into the customer's cart
        var guestCart = await WithDbAsync(db => db.Carts.AsNoTracking().Where(x => x.OwnerKind == CartOwnerKind.Guest && x.MergedIntoCartId != null && x.Lines.Any(l => l.SkuId == b.SkuId))
            .Select(x => new { x.Status, x.MergedIntoCartId }).SingleAsync());
        Assert.Equal(CartStatus.Merged, guestCart.Status);
        Assert.Equal(after.GetProperty("cartId").GetGuid(), guestCart.MergedIntoCartId);

        // the merge is idempotent: the cookie is gone, and a second POST changes nothing
        var again = await ExpectAsync(HttpStatusCode.OK, await browser.PostAsync("/api/customers/me/cart/merge", null));
        Assert.Equal(0, (await ReadAsync(again)).GetProperty("mergedLineCount").GetInt32());
        Assert.Equal(3, (await CartAsync(browser)).GetProperty("lines").GetArrayLength());

        // and the customer id is the account's, not a guest's
        Assert.Equal(customerId, await WithDbAsync(db => db.Carts.AsNoTracking().Where(x => x.Id == after.GetProperty("cartId").GetGuid()).Select(x => x.CustomerId!.Value).SingleAsync()));
    }

    [Fact]
    public async Task I_C2_TheMerge_NeedsASignedInCustomer_AndAGuestGetNothingFromIt()
    {
        var anonymous = CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/customers/me/cart/merge", null)).StatusCode);
    }

    [Fact]
    public async Task I_C2_AMergeWithNoCustomerCartYet_CreatesIt()
    {
        var context = await CreateContextAsync();
        var sku = await StockedSkuAsync(context);

        var (customer, _, _) = await RegisterAndSignInCustomerAsync();
        // a guest cookie from a different browser session is carried over by hand: add as guest, then replay the cookie on the customer client
        var guestBrowser = CreateClient();
        var add = await ExpectAsync(HttpStatusCode.Created, await AddAsync(guestBrowser, sku.SkuId, 4));
        var cookie = SetCookies(add).Single(c => c.StartsWith("pc_cart=")).Split(';')[0];

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/customers/me/cart/merge");
        request.Headers.Add("Cookie", cookie);
        var merge = await ExpectAsync(HttpStatusCode.OK, await customer.SendAsync(request));
        Assert.Equal(1, (await ReadAsync(merge)).GetProperty("mergedLineCount").GetInt32());

        var cart = await CartAsync(customer);
        Assert.Equal(4, cart.GetProperty("lines")[0].GetProperty("quantity").GetInt32());
    }

    // ── I-C3 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C3_TheSameCustomerOnASecondDevice_SeesTheSameCart()
    {
        var context = await CreateContextAsync();
        var sku = await StockedSkuAsync(context, 75m);
        var (phone, _, mobile) = await RegisterAndSignInCustomerAsync();
        await ExpectAsync(HttpStatusCode.Created, await AddAsync(phone, sku.SkuId, 2));

        var laptop = CreateClient(); // no guest cookie
        var signIn = await laptop.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = CustomerPassword }, Json);
        await ExpectAsync(HttpStatusCode.OK, signIn);
        laptop.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        var cart = await CartAsync(laptop);
        var line = Assert.Single(cart.GetProperty("lines").EnumerateArray());
        Assert.Equal(sku.SkuId, line.GetProperty("skuId").GetGuid());
        Assert.Equal(2, line.GetProperty("quantity").GetInt32());

        // an edit on the laptop is visible on the phone
        await ExpectAsync(HttpStatusCode.NoContent, await laptop.PutAsJsonAsync($"/api/cart/lines/{line.GetProperty("lineId").GetGuid()}", new { quantity = 5 }, Json));
        Assert.Equal(5, (await CartAsync(phone)).GetProperty("lines")[0].GetProperty("quantity").GetInt32());
    }

    [Fact]
    public async Task ACustomer_HasExactlyOneActiveCart_EvenFromTwoDevicesAtOnce()
    {
        var context = await CreateContextAsync();
        var sku = await StockedSkuAsync(context);
        var (phone, customerId, mobile) = await RegisterAndSignInCustomerAsync();
        var laptop = CreateClient();
        var signIn = await laptop.PostAsJsonAsync("/api/auth/sign-in", new { identifier = mobile, password = CustomerPassword }, Json);
        laptop.DefaultRequestHeaders.Add("X-CSRF-Token", ExtractCookieValue(signIn, "pc_csrf"));

        // two first adds race: whichever loses the unique index is told to retry (409) rather than creating a second cart
        var results = await Task.WhenAll(AddAsync(phone, sku.SkuId), AddAsync(laptop, sku.SkuId));
        Assert.All(results, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict, $"Unexpected {(int)r.StatusCode}: {BodyAsync(r).Result}"));
        Assert.Contains(results, r => r.StatusCode == HttpStatusCode.Created);

        Assert.Equal(1, await WithDbAsync(db => db.Carts.AsNoTracking().CountAsync(c => c.CustomerId == customerId && c.Status == CartStatus.Active)));
        if (results.Any(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.Contains("another device", await BodyAsync(results.First(r => r.StatusCode == HttpStatusCode.Conflict)));
    }

    // ── I-C4 ────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task I_C4_APrescriptionSku_NeedsASignedInCustomerAndACat()
    {
        var context = await CreateContextAsync();
        var rx = await StockedSkuAsync(context, 900m, prescription: true);

        // a guest cannot add a prescription SKU at all
        await AssertErrorAsync(await AddAsync(CreateClient(), rx.SkuId), "PrescriptionCatRequired");

        // a customer must say which cat
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        await AssertErrorAsync(await AddAsync(customer, rx.SkuId), "PrescriptionCatRequired");
    }

    [Fact]
    public async Task I_C4_AnotherCustomersCat_IsA404_AndACatWithoutAnAuthorisation_Is400PrescriptionNotAuthorised()
    {
        var context = await CreateContextAsync();
        var rx = await StockedSkuAsync(context, 900m, prescription: true);

        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var (_, strangerId, _) = await RegisterAndSignInCustomerAsync();
        var strangersCat = await SeedCatAsync(strangerId);
        var ownCat = await SeedCatAsync(customerId);

        // a cat that is not the caller's reads exactly like a cat that does not exist
        Assert.Equal(HttpStatusCode.NotFound, (await AddAsync(customer, rx.SkuId, 1, strangersCat)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await AddAsync(customer, rx.SkuId, 1, Guid.NewGuid())).StatusCode);

        // the caller's own cat with no live authorisation: the gate (the real HO-PHM-001 gate) refuses
        await AssertErrorAsync(await AddAsync(customer, rx.SkuId, 1, ownCat), "PrescriptionNotAuthorised");
    }

    [Fact]
    public async Task I_C4_AnAuthorisedCat_AddsTheLine_AndTheCartShowsTheCat()
    {
        var context = await CreateContextAsync();
        var rx = await StockedSkuAsync(context, 900m, prescription: true);
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var cat = await SeedCatAsync(customerId);

        // a real HO-PHM-001 authorisation for (cat, the SKU's product), judged by the real IDietAuthorisationGate
        await AuthoriseDietAsync(cat, customerId, rx.ProductId, rx.Name);
        var add = await ExpectAsync(HttpStatusCode.Created, await AddAsync(customer, rx.SkuId, 1, cat));
        Assert.NotEqual(Guid.Empty, (await ReadAsync(add)).GetProperty("lineId").GetGuid());

        var line = (await CartAsync(customer)).GetProperty("lines")[0];
        Assert.Equal(cat, line.GetProperty("cat").GetProperty("id").GetGuid());
        Assert.Equal("Authorised", line.GetProperty("prescriptionStatus").GetString());
    }

    [Fact]
    public async Task AnOrdinarySku_IgnoresACatId_SoTheSameSkuIsOneLine()
    {
        var context = await CreateContextAsync();
        var sku = await StockedSkuAsync(context);
        var (customer, customerId, _) = await RegisterAndSignInCustomerAsync();
        var mitu = await SeedCatAsync(customerId);
        var luna = await SeedCatAsync(customerId);

        await ExpectAsync(HttpStatusCode.Created, await AddAsync(customer, sku.SkuId, 1, mitu));
        await ExpectAsync(HttpStatusCode.Created, await AddAsync(customer, sku.SkuId, 1, luna));

        var cart = await CartAsync(customer);
        var line = Assert.Single(cart.GetProperty("lines").EnumerateArray());
        Assert.Equal(2, line.GetProperty("quantity").GetInt32());
        Assert.Equal(JsonValueKind.Null, line.GetProperty("cat").ValueKind);
    }
}
