using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 §16.1 -- the shared "shop" the placement, payment, fulfilment, return and counter test classes start from: one branch with a billing
/// profile (an invoice cannot be issued without one), a 15 % food and a 0 % toy stocked at that branch (and at the storefront, where the cart reads
/// availability), a delivery zone, and a signed-in customer with an address in it and every pending policy accepted. Real endpoints throughout.
/// </summary>
public abstract class OrderingShopTestBase(OrderingApiFactory factory) : OrderingIntegrationTestBase(factory)
{
    protected sealed record Shop(
        OrderingContext Context, string District, Guid ZoneId, HttpClient Customer, Guid CustomerId, Guid AddressId, OrderingSku Food, OrderingSku Toy);

    protected async Task SetBillingProfileAsync(OrderingContext context) =>
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PutAsJsonAsync($"/api/admin/billing-profiles/{context.BranchId}", new
        {
            legalName = "PurrCare Test Ltd " + context.BranchCode,
            taxRegistrationNumber = "BIN-" + context.BranchCode,
            addressLines = new[] { "1 Test Road", "Dhaka 1212" },
            phone = "01700000000",
            email = "billing@example.test",
            invoiceFooter = "Thank you for choosing PurrCare",
            bankAccounts = new[] { new { bankName = "Test Bank", accountName = "PurrCare Ltd", accountNumber = "1234567890", branchName = "Gulshan", routingNumber = "012345678" } },
        }, Json));

    /// <summary>Sets the shop's one bank-transfer account. The account is a single global row, so concurrent test classes can race its first write; the
    /// helper retries that benign <c>409</c> (setup noise, not the control under test).</summary>
    protected async Task SetBankTransferAccountAsync(HttpClient admin)
    {
        for (var attempt = 1; ; attempt++)
        {
            var response = await admin.PutAsJsonAsync("/api/admin/bank-transfer-account", new
            {
                accountName = "PurrCare Ltd", accountNumber = "1234567890", bankName = "Test Bank", branchName = "Gulshan", routingNumber = "012345678", isActive = true,
            }, Json);

            if (response.StatusCode == HttpStatusCode.Conflict && attempt < 8) { await Task.Delay(50 * attempt); continue; }
            await ExpectAsync(HttpStatusCode.NoContent, response);
            return;
        }
    }

    /// <summary>A branch with its billing profile, a food (850.00, 15 %) and a toy (120.00, 0 %) with <paramref name="stock"/> units each, a Standard-60.00 zone for
    /// a fresh district, and a customer ready to check out. <paramref name="fillCart"/> adds 2 x food + 1 x toy (the worked cart of U-D1).</summary>
    protected async Task<Shop> SetUpShopAsync(
        bool cod = true, decimal? codMax = null, decimal? freeOver = null, bool fillCart = true, decimal stock = 50m, bool billingProfile = true, object[]? windows = null, int lead = 1)
    {
        var context = await CreateContextAsync();
        if (billingProfile) await SetBillingProfileAsync(context);

        var food = await CreateSkuAsync(context, price: 850m, stock: stock, onStorefront: true, name: "Royal Canin Kitten 2 kg");
        var toy = await CreateSkuAsync(context, price: 120m, stock: stock, taxClassId: context.ZeroTaxClassId, onStorefront: true, name: "Feather wand");
        var district = NewDistrict();
        var zoneId = await CreateZoneAsync(context, district, cod: cod, codMax: codMax, freeOver: freeOver, slotWindows: windows, leadTimeDays: lead);

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

    protected static string QuoteUrl(
        Shop shop, string fulfilment = "Delivery", string? payment = "CashOnDelivery", string? code = null, string? level = null, string? slotDate = null,
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

    protected static async Task<JsonElement> QuoteAsync(HttpClient customer, string url) =>
        await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await customer.GetAsync(url)));

    /// <summary>The body of a placement for the same choices the quote URL names, carrying the fingerprint of <paramref name="quote"/>.</summary>
    protected static object PlaceBody(
        Shop shop, JsonElement quote, string fulfilment = "Delivery", string payment = "CashOnDelivery", string? code = null, string? level = null, string? slotDate = null,
        string? slotStart = null, Guid? pickupBranchId = null, string? recipientName = null, string? recipientPhone = null) => new
        {
            fulfilment,
            deliveryAddressId = fulfilment == "Delivery" ? shop.AddressId : (Guid?)null,
            serviceLevel = level,
            slotDate,
            slotWindowStart = slotStart,
            pickupBranchId,
            paymentMethod = payment,
            discountCode = code,
            recipientName,
            recipientPhone,
            quoteFingerprint = quote.GetProperty("fingerprint").GetString(),
        };

    protected static HttpRequestMessage PostOrder(object body, Guid? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/customers/me/orders") { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return request;
    }

    /// <summary>Accepts what is pending, quotes and places a cash-on-delivery delivery order for the shop's customer -- the happy path most tests start from.</summary>
    protected Task<JsonElement> PlaceCodOrderAsync(Shop shop, string? code = null) => PlaceAsync(shop, "CashOnDelivery", code);

    /// <summary>Accepts what is pending, quotes and places a bank-transfer delivery order (the shop's bank account must be set first).</summary>
    protected Task<JsonElement> PlaceTransferOrderAsync(Shop shop, string? code = null) => PlaceAsync(shop, "BankTransfer", code);

    /// <summary>
    /// Quote and place. The shared database holds <c>RequiresAcceptance</c> policies that other test classes create while this one runs (I-D13), so a placement
    /// can meet a version published after the customer last accepted: that single case (and only that) re-accepts and goes again -- exactly what the storefront
    /// does when the server answers <c>PolicyAcceptanceRequired</c>. Every other answer is asserted as is.
    /// </summary>
    private async Task<JsonElement> PlaceAsync(Shop shop, string payment, string? code)
    {
        for (var attempt = 1; ; attempt++)
        {
            await AcceptPendingPoliciesAsync(shop.Customer);
            var quote = await QuoteAsync(shop.Customer, QuoteUrl(shop, payment: payment, code: code));
            var response = await shop.Customer.SendAsync(PostOrder(PlaceBody(shop, quote, payment: payment, code: code)));

            if (response.StatusCode == HttpStatusCode.BadRequest && attempt < 4 && (await ErrorCodesAsync(response)).Contains("PolicyAcceptanceRequired")) continue;

            await ExpectAsync(HttpStatusCode.Created, response);
            return await ReadAsync(response);
        }
    }

    protected async Task<Domain.Aggregates.Order.OrderAggregate> LoadOrderAsync(Guid orderId) =>
        await WithDbAsync(db => db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId));

    protected async Task<int> NotificationCountAsync(Guid customerId, string templateKey) =>
        await WithDbAsync(db => db.NotificationRequests.AsNoTracking().CountAsync(n => n.RecipientId == customerId && n.TemplateKey == templateKey));

    protected async Task<decimal> SellableAsync(Guid branchId, Guid skuId) =>
        await WithDbAsync(db => db.SkuSellable.AsNoTracking().Where(v => v.SkuId == skuId && v.BranchId == branchId).Select(v => v.SellableQuantity).SumAsync());

    // ── payment, worker and staff helpers (phase E onwards) ─────────────────────────────────────────

    /// <summary>A staff POST with a fresh <c>Idempotency-Key</c> (every money-recording staff endpoint is idempotent at the edge).</summary>
    protected static async Task<HttpResponseMessage> StaffPostAsync(HttpClient staff, string url, object? body = null, Guid? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body ?? new { }, options: Json) };
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        return await staff.SendAsync(request);
    }

    protected Task<HttpResponseMessage> ConfirmTransferAsync(HttpClient staff, Guid orderId, int version, decimal amount, string? reference = null, Guid? key = null) =>
        StaffPostAsync(staff, $"/api/staff/orders/{orderId}/payment/bank-transfer",
            new { expectedVersion = version, amount, bankReference = reference ?? "BANK-" + Guid.NewGuid().ToString("N")[..8], receivedOn = Now.AddMinutes(-5) }, key);

    /// <summary>Sets the Billing approval threshold for one branch (a transfer above it is held for a second person, FR-BIL-12).</summary>
    protected async Task SetApprovalThresholdAsync(OrderingContext context, string amount) =>
        await ExpectAsync(HttpStatusCode.NoContent, await context.Admin.PutAsJsonAsync(
            $"/api/admin/settings?branchId={context.BranchId}", new { changes = new[] { new { key = SettingKeys.BillingPaymentApprovalThreshold, value = amount } } }, Json));

    /// <summary>Runs one <c>OrderWorkCycle</c> pass in a fresh scope (the hosted worker is off in tests). A test that moves its clock passes its own branch, so only that
    /// branch's orders are expired and no global purge runs against the shared database.</summary>
    protected async Task RunOrderWorkCycleAsync(Guid? onlyBranchId = null, bool withIdempotencyPurge = false)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CatEcommerce.Api.Background.OrderWorkCycle>().RunAsync(default, onlyBranchId, withIdempotencyPurge);
    }

    /// <summary>Applies a domain mutation to a stored order the way a later phase's command will (load, mutate through the aggregate, save) -- so an earlier
    /// phase's test can stand an order in a state only a later phase's endpoint reaches.</summary>
    protected async Task MutateOrderAsync(Guid orderId, Action<Domain.Aggregates.Order.OrderAggregate> mutate)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<Platform.Infrastructure.Core.Domain.IAggregateRootRepository<Domain.Aggregates.Order.OrderAggregate>>();
        var order = await repository.GetByIdAsync(orderId) ?? throw new InvalidOperationException("order not found");
        mutate(order);
        await repository.UpdateAsync(order, Platform.Infrastructure.Core.Commands.EventPublishOption.DoNotPublish);
    }
}
