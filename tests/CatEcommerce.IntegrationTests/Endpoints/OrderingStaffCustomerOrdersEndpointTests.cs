using System.Net;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001-FU2 (owner decision 2026-10-12) -- <c>GET /api/staff/customers/{customerId}/orders</c>, through real HTTP against the per-branch database.
/// Scope rule: the staff member's ACTIVE branch only (as the queue and the detail), never across branches.
/// </summary>
public sealed class OrderingStaffCustomerOrdersEndpointTests(OrderingApiFactory factory) : OrderingShopTestBase(factory)
{
    private sealed record Till(Shop Shop, HttpClient Reception, OrderingSku Item);

    private async Task<Till> TillAsync()
    {
        var shop = await SetUpShopAsync(fillCart: false);
        var item = await CreateSkuAsync(shop.Context, price: 100m, stock: 50m, taxClassId: shop.Context.ZeroTaxClassId, name: "FU2 item " + Guid.NewGuid().ToString("N")[..8]);
        var (reception, _) = await StaffClientAsync(shop.Context, StaffRoles.Reception);
        return new Till(shop, reception, item);
    }

    private static async Task<Guid> SellAsync(Till till, string mobile, string name)
    {
        var response = await StaffPostAsync(till.Reception, "/api/staff/pos/sales", new
        {
            customerName = name, customerMobile = mobile,
            lines = new[] { new { skuId = till.Item.SkuId, quantity = 1 } },
            tenders = new object[] { new { method = "Cash", amount = 100m } },
        });
        return (await ReadAsync(await ExpectAsync(HttpStatusCode.Created, response))).GetProperty("orderId").GetGuid();
    }

    private Task<Guid> AccountIdAsync(string mobile)
    {
        var stored = MobileNumber.Create(mobile).Value;
        return WithDbAsync(db => db.CustomerAccounts.AsNoTracking().Where(c => c.MobileNumber!.Value == stored).Select(c => c.Id).SingleAsync());
    }

    private static string Url(Guid customerId, string query = "") => $"/api/staff/customers/{customerId}/orders{query}";

    private static Guid[] Ids(System.Text.Json.JsonElement page) => page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToArray();

    [Fact]
    public async Task TheCustomersOrders_AreListed_NewestFirst_WithTheFieldsThePanelNeeds()
    {
        var till = await TillAsync();
        var mobile = NewMobile();
        var first = await SellAsync(till, mobile, "Panel Customer");
        var second = await SellAsync(till, mobile, "Panel Customer");
        var customerId = await AccountIdAsync(mobile);

        var page = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await till.Reception.GetAsync(Url(customerId))));

        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(new[] { second, first }, Ids(page));
        var order = await LoadOrderAsync(second);
        var row = page.GetProperty("items")[0];
        Assert.Equal(order.OrderNumber, row.GetProperty("orderNumber").GetString());
        Assert.False(string.IsNullOrEmpty(row.GetProperty("status").GetString()));
        Assert.Equal("100.00", row.GetProperty("grandTotal").GetProperty("amount").GetString());
        Assert.True(row.TryGetProperty("placedOn", out _));
    }

    [Fact]
    public async Task AnotherCustomersOrders_AreNeverReturned()
    {
        var till = await TillAsync();
        var (mobileA, mobileB) = (NewMobile(), NewMobile());
        var orderA = await SellAsync(till, mobileA, "Customer A");
        var orderB = await SellAsync(till, mobileB, "Customer B");

        var a = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await till.Reception.GetAsync(Url(await AccountIdAsync(mobileA)))));
        var b = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await till.Reception.GetAsync(Url(await AccountIdAsync(mobileB)))));

        Assert.Equal(new[] { orderA }, Ids(a));
        Assert.Equal(new[] { orderB }, Ids(b));
    }

    [Fact]
    public async Task AnUnknownCustomer_IsNotFound()
    {
        var till = await TillAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await till.Reception.GetAsync(Url(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task BranchScope_AStaffMemberOfAnotherBranch_SeesNoneOfThisBranchsOrders()
    {
        var till = await TillAsync();
        var mobile = NewMobile();
        await SellAsync(till, mobile, "Elsewhere Customer");
        var customerId = await AccountIdAsync(mobile);
        var (otherBranch, _) = await StaffClientAsync(await CreateContextAsync(), StaffRoles.Reception);

        var page = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await otherBranch.GetAsync(Url(customerId))));

        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
        Assert.Empty(Ids(page));
    }

    [Fact]
    public async Task Paging_SplitsTheListAndReportsTheTotal_AndABadPageIsRefused()
    {
        var till = await TillAsync();
        var mobile = NewMobile();
        for (var i = 0; i < 3; i++) await SellAsync(till, mobile, "Pager");
        var customerId = await AccountIdAsync(mobile);

        var one = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await till.Reception.GetAsync(Url(customerId, "?pageNumber=1&pageSize=2"))));
        var two = await ReadAsync(await ExpectAsync(HttpStatusCode.OK, await till.Reception.GetAsync(Url(customerId, "?pageNumber=2&pageSize=2"))));

        Assert.Equal(3, one.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, Ids(one).Length);
        Assert.Single(Ids(two));
        Assert.DoesNotContain(Ids(two)[0], Ids(one));
        Assert.Equal(HttpStatusCode.BadRequest, (await till.Reception.GetAsync(Url(customerId, "?pageSize=0"))).StatusCode);
    }

    [Fact]
    public async Task Roles_WithOrdersView_AreAllowed_AndOthersAreRefused()
    {
        var till = await TillAsync();
        var mobile = NewMobile();
        await SellAsync(till, mobile, "Roles");
        var customerId = await AccountIdAsync(mobile);

        foreach (var role in new[] { StaffRoles.Reception, StaffRoles.Inventory, StaffRoles.ShopManager, StaffRoles.Accountant, StaffRoles.Administrator })
        {
            var (client, _) = await StaffClientAsync(till.Shop.Context, role);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Url(customerId))).StatusCode);
        }

        foreach (var role in new[] { StaffRoles.Nurse, StaffRoles.Vet, StaffRoles.Caretaker })
        {
            var (client, _) = await StaffClientAsync(till.Shop.Context, role);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Url(customerId))).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateClient().GetAsync(Url(customerId))).StatusCode);
    }
}
