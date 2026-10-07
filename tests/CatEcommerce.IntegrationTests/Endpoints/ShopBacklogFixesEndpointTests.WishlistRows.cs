using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>Rows 18, 33, 34 -- wishlist visibility, due-alert starvation, notify-time diet gate.</summary>
public sealed partial class ShopBacklogFixesEndpointTests
{
    // ── Row 18: the wishlist and draft / unlisted products ──────────────────────────────────────

    [Fact]
    public async Task Row18_Wishlist_RefusesASkuOfADraftProduct_AsUnknown()
    {
        var staff = await SignInAsShopManagerAsync();
        var draft = await CreateProductAsync(staff, NewBrand("WishDraft"), "Draft Toy", publish: false);
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await customer.PutAsync($"/api/customers/me/wishlist/items/{draft.SkuId}", null)).StatusCode);
        Assert.Empty((await JsonOkAsync(await customer.GetAsync("/api/customers/me/wishlist"))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Row18_Wishlist_RefusesASkuOfAnUnlistedProduct()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("WishHidden"), "Hidden Toy");
        await UnlistAsync(staff, product.ProductId);
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await customer.PutAsync($"/api/customers/me/wishlist/items/{product.SkuId}", null)).StatusCode);
    }

    [Fact]
    public async Task Row18_Wishlist_DoesNotShowAnItemWhoseProductWasUnpublished_ButKeepsAnArchivedOne()
    {
        var staff = await SignInAsShopManagerAsync();
        var toUnpublish = await CreateProductAsync(staff, NewBrand("WishUnpub"), "Soon Draft");
        var toArchive = await CreateProductAsync(staff, NewBrand("WishArch"), "Soon Archived");
        var (customer, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await customer.PutAsync($"/api/customers/me/wishlist/items/{toUnpublish.SkuId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await customer.PutAsync($"/api/customers/me/wishlist/items/{toArchive.SkuId}", null)).StatusCode);

        var unpublished = await staff.PostAsJsonAsync($"/api/staff/shop/products/{toUnpublish.ProductId}/unpublish",
            new { expectedVersion = await ProductVersionAsync(staff, toUnpublish.ProductId) }, Json);
        Assert.True(unpublished.StatusCode == HttpStatusCode.NoContent, await unpublished.Content.ReadAsStringAsync());
        var archivedResponse = await staff.PostAsJsonAsync($"/api/staff/shop/products/{toArchive.ProductId}/archive",
            new { expectedVersion = await ProductVersionAsync(staff, toArchive.ProductId) }, Json);
        Assert.True(archivedResponse.StatusCode == HttpStatusCode.NoContent, await archivedResponse.Content.ReadAsStringAsync());

        var items = (await JsonOkAsync(await customer.GetAsync("/api/customers/me/wishlist"))).GetProperty("items").EnumerateArray().ToList();

        Assert.DoesNotContain(items, i => i.GetProperty("skuId").GetGuid() == toUnpublish.SkuId);
        var archived = Assert.Single(items, i => i.GetProperty("skuId").GetGuid() == toArchive.SkuId);
        Assert.False(archived.GetProperty("onSale").GetBoolean());
    }

    // ── Row 33: due alerts are not starved by never-available bundle alerts ─────────────────────

    [Fact]
    public async Task Row33_DueAlerts_AreNotStarvedByOlderBundleAlertsThatCanNeverBeAvailable()
    {
        // A never-available alert stays Pending forever, so this test's own leftovers (from this run or an earlier
        // aborted one) would pile up in the shared database and eat the page budget of a small-limit read. They
        // are removed before and after.
        await DeleteStarveAlertsAsync();
        try
        {
            await StarvationScenarioAsync();
        }
        finally
        {
            await DeleteStarveAlertsAsync();
        }
    }

    private Task DeleteStarveAlertsAsync() => ExecuteSqlAsync(
        $"DELETE FROM product.stock_alert WHERE sku_id IN (SELECT s.\"Id\" FROM product.sku s JOIN product.product p ON p.\"Id\" = s.product_id WHERE p.brand LIKE 'Starve-%')");

    private async Task StarvationScenarioAsync()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Starve");
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        // Three bundles whose component is out of stock: their own SKU is priced (so the SQL prefilter passes)
        // but they are never available. Their alerts are the OLDEST.
        var component = await CreateProductAsync(staff, brand, "Component");
        await SetSkuOutOfStockAsync(component.SkuId);
        var neverAvailable = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var other = await CreateProductAsync(staff, brand, $"Other {i}");
            var bundle = await CreateBundleAsync(staff, brand, $"Never Bundle {i}", [(component.SkuId, 1), (other.SkuId, 1)]);
            Assert.Equal(HttpStatusCode.Created, (await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = bundle.SkuId }, Json)).StatusCode);
            neverAvailable.Add(bundle.SkuId);
        }

        // A genuinely due alert, requested AFTER them. Other processes may run a shop pass against the same shared
        // database and notify it before this test reads it back; that is a lost race, not a failure, so a
        // consumed alert is replaced by a fresh one (up to three times).
        for (var attempt = 1; ; attempt++)
        {
            var real = await CreateProductAsync(staff, brand, $"Really Due {attempt}");
            await SetSkuOutOfStockAsync(real.SkuId);
            var created = await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = real.SkuId }, Json);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var realAlertId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("stockAlertId").GetGuid();
            await SetSkuInStockAsync(real.SkuId);

            // Position of the real alert among ALL due alerts (a limit far beyond any realistic backlog).
            var everything = await DueAlertsAsync(limit: 100_000);
            var position = everything.IndexOf(realAlertId);
            if (position < 0 && attempt < 3) continue;
            Assert.True(position >= 0, "the real alert must be due");

            // A limit that just covers it. The older never-available alerts sit in front of it among the
            // candidates, so a plain Take(limit) over the candidates would run out before reaching it.
            var limited = await DueAlertsAsync(limit: position + 1);
            if (!limited.Contains(realAlertId) && attempt < 3 && !await IsStillPendingAsync(realAlertId)) continue;

            Assert.Contains(realAlertId, limited);
            break;
        }
    }

    private async Task<bool> IsStillPendingAsync(Guid alertId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        return await db.StockAlerts.AsNoTracking().AnyAsync(a => a.Id == alertId && a.Status == CatEcommerce.Shared.Common.Enums.StockAlertStatus.Pending);
    }

    private async Task<List<Guid>> DueAlertsAsync(int limit)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IInMemoryDispatcher>();
        var response = await dispatcher.GetResultAsync(new GetDueStockAlertsQuery
        { Limit = limit, UserContext = new UserContext(Guid.NewGuid(), Guid.Empty), CorrelationId = Guid.NewGuid() });
        return (response.Result ?? []).ToList();
    }

    // ── Row 34: the notify pass re-checks the diet gate for a prescription product ──────────────

    [Fact]
    public async Task Row34_ALapsedAuthorisation_StopsTheBackInStockNotification_UntilItIsRenewed()
    {
        var (gateFactory, authorisations) = Factory.WithControllableDietGate();
        var staff = await SignInAsShopManagerAsync();
        var rx = await CreateProductAsync(staff, NewBrand("LapseRx"), "Lapsing Rx Diet", "CatFood", prescription: true);
        await SetSkuOutOfStockAsync(rx.SkuId);

        var (customer, customerId) = await RegisterAndSignInCustomerAsync(gateFactory);
        var catId = await SeedCatAsync(customerId);
        authorisations.Authorise(customerId, catId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));
        Assert.Equal(HttpStatusCode.Created, (await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = rx.SkuId }, Json)).StatusCode);

        // The authorisation lapses (the gate's "today" moves past its end), THEN stock arrives.
        authorisations.Today = () => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(60);
        await SetSkuInStockAsync(rx.SkuId);
        await RunShopCycleAsync(gateFactory);

        Assert.Equal(0, await CountBackInStockRequestsAsync(customerId));
        Assert.Equal("Pending", (await JsonOkAsync(await customer.GetAsync("/api/customers/me/stock-alerts"))).EnumerateArray().Single().GetProperty("status").GetString());

        // Renewed: the next pass notifies as normal.
        authorisations.Today = () => DateOnly.FromDateTime(DateTime.UtcNow);
        await RunShopCycleAsync(gateFactory);

        Assert.Equal(1, await CountBackInStockRequestsAsync(customerId));
        Assert.Equal("Notified", (await JsonOkAsync(await customer.GetAsync("/api/customers/me/stock-alerts"))).EnumerateArray().Single().GetProperty("status").GetString());
    }

    private async Task<int> CountBackInStockRequestsAsync(Guid customerId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        return await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .CountAsync(r => r.RecipientId == customerId && r.TemplateKey == NotificationTemplateCatalogue.ShopBackInStock);
    }
}
