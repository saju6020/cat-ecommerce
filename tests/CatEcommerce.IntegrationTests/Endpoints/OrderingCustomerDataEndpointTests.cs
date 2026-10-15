using System.Net;
using System.Net.Http.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 I-X2 and I-X3 (HO-ACC-001 ADR-039, ADR-214, D-11) -- what a customer merge and an anonymisation do to carts and orders: a merge moves the cart
/// and every order to the survivor; an anonymisation redacts a terminal order's recipient and address, flags an open one (redacted when it reaches a terminal
/// state) and abandons the active cart. Nothing is deleted.
/// </summary>
public sealed class OrderingCustomerDataEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    [Fact]
    public async Task I_X2_MergingACustomer_MovesTheirCartAndOrdersToTheSurvivor_AndMergesTheCartsWithoutDuplicates()
    {
        var shop = await SetUpShopAsync();
        var orderId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        // the duplicate keeps shopping: the toy is in their cart again, and so is the food (a line the survivor also has)
        var other = await CreateSkuAsync(shop.Context, price: 40m, stock: 20m, onStorefront: true);
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = other.SkuId, quantity = 2 }, Json));
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = shop.Food.SkuId, quantity = 1 }, Json));

        var (survivor, survivorId, _) = await RegisterAndSignInCustomerAsync();
        await ExpectAsync(HttpStatusCode.Created, await survivor.PostAsJsonAsync("/api/cart/lines", new { skuId = shop.Food.SkuId, quantity = 3 }, Json));

        var shared = NewMobile();
        foreach (var client in new[] { shop.Customer, survivor })
            await ExpectAsync(HttpStatusCode.NoContent, await client.PutAsJsonAsync("/api/customers/me",
                new { fullName = "Test Customer", additionalPhoneNumbers = new[] { shared }, currentPassword = CustomerPassword }, Json));

        var admin = await SeedStaffAsync(StaffRoles.Administrator);
        using var adminClient = await SignInStaffAsync(admin.Mobile, admin.Password);
        await ExpectAsync(HttpStatusCode.OK, await adminClient.PostAsJsonAsync(
            $"/api/staff/customers/{shop.CustomerId}/merge", new { survivingCustomerId = survivorId, reason = "Same person, confirmed by phone" }, Json));

        // the order is the survivor's now
        Assert.Equal(survivorId, (await LoadOrderAsync(orderId)).CustomerId);
        var list = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await survivor.GetAsync("/api/customers/me/orders")));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), o => o.GetProperty("id").GetGuid() == orderId);

        // one cart, one line per SKU, the larger quantity for the SKU both had
        var cart = await ReadAsync(await survivor.GetAsync("/api/cart"));
        var lines = cart.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(2, lines.Count);
        Assert.Equal(3, lines.Single(l => l.GetProperty("skuId").GetGuid() == shop.Food.SkuId).GetProperty("quantity").GetInt32());
        Assert.Equal(2, lines.Single(l => l.GetProperty("skuId").GetGuid() == other.SkuId).GetProperty("quantity").GetInt32());

        // the duplicate's own cart is no longer active, and the duplicate has no orders left
        Assert.Equal(0, await WithDbAsync(db => db.Carts.AsNoTracking().CountAsync(c => c.CustomerId == shop.CustomerId && c.Status == CartStatus.Active)));
        Assert.Equal(0, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.CustomerId == shop.CustomerId)));
    }

    [Fact]
    public async Task I_X3_AnonymisingACustomer_RedactsATerminalOrder_FlagsAnOpenOne_AndRedactsItWhenItEnds()
    {
        var shop = await SetUpShopAsync();
        var terminalId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();
        await ExpectAsync(HttpStatusCode.NoContent, await shop.Customer.PostAsJsonAsync($"/api/customers/me/orders/{terminalId}/cancel", new { }, Json)); // terminal
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = shop.Food.SkuId, quantity = 1 }, Json));
        var openId = (await PlaceCodOrderAsync(shop)).GetProperty("orderId").GetGuid();                                                                    // stays open
        await ExpectAsync(HttpStatusCode.Created, await shop.Customer.PostAsJsonAsync("/api/cart/lines", new { skuId = shop.Food.SkuId, quantity = 1 }, Json));

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();
            var customer = await repository.GetByIdAsync(shop.CustomerId) ?? throw new InvalidOperationException("customer");
            customer.Anonymise(new CustomerAnonymisationDto(DateTimeOffset.UtcNow, new Platform.Infrastructure.Common.Security.UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await repository.UpdateAsync(customer, EventPublishOption.InMemoryPublishOnly);
        }

        var terminal = await LoadOrderAsync(terminalId);
        Assert.True(terminal.CustomerAnonymised);
        Assert.Equal("Anonymised customer", terminal.Fulfilment.RecipientName);
        Assert.Equal("[redacted]", terminal.Fulfilment.Line1);
        Assert.NotEqual("[redacted]", terminal.Fulfilment.District);     // the district is kept for reporting

        var open = await LoadOrderAsync(openId);
        Assert.True(open.CustomerAnonymised);
        Assert.NotEqual("Anonymised customer", open.Fulfilment.RecipientName);   // still needed to deliver it

        Assert.Equal(0, await WithDbAsync(db => db.Carts.AsNoTracking().CountAsync(c => c.CustomerId == shop.CustomerId && c.Status == CartStatus.Active)));

        // when the open order ends, it is redacted too
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        await ExpectAsync(HttpStatusCode.NoContent, await reception.PostAsJsonAsync($"/api/staff/orders/{openId}/cancel", new { note = "Customer closed the account" }, Json));
        var ended = await LoadOrderAsync(openId);
        Assert.Equal("Anonymised customer", ended.Fulfilment.RecipientName);
        Assert.Equal("[redacted]", ended.Fulfilment.Line1);

        // nothing was deleted
        Assert.Equal(2, await WithDbAsync(db => db.Orders.AsNoTracking().CountAsync(o => o.Id == terminalId || o.Id == openId)));
    }
}
