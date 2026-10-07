using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Account;
using CatEcommerce.Domain.Aggregates.Notification;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Notifications.Senders;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.IntegrationTests.Endpoints;

/// <summary>
/// HO-SHP-001 §16 Phase G -- I-G1..I-G7 (US-SHP-13, ADR-096). <c>[Collection("Notifications")]</c>:
/// these tests drive HO-NTF-001's delivery passes, which scan the global, never-truncated
/// notification tables (§16.1), so they must not run in parallel with another class that does
/// (the HO-APT-001 ACCEPTANCE 2026-09-29 precedent) and they loop until their OWN row reaches the
/// expected state.
///
/// Stock: HO-IMP-001 is not built, so "push stock" is the interim availability fixture
/// (<c>SetSkuInStockAsync</c>/<c>SetSkuOutOfStockAsync</c>, HO-SHP-001 §1 Amended 2026-10-04).
/// </summary>
[Collection("Notifications")]
public sealed class ShopWishlistAndStockAlertEndpointTests(ShopApiFactory factory) : ShopIntegrationTestBase(factory)
{
    private static string NewBrand(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];

    private static async Task<JsonElement> BodyAsync(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<JsonElement>(Json);

    // ── I-G1 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G1_Wishlist_AddListAddAgainRemove_IsIdempotent_AndShowsPriceAndAvailability()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Wish"), "Cat Tree", price: 4500m);
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await customer.PutAsync($"/api/customers/me/wishlist/items/{product.SkuId}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await customer.PutAsync($"/api/customers/me/wishlist/items/{product.SkuId}", null)).StatusCode);

        var listResponse = await customer.GetAsync("/api/customers/me/wishlist");
        Assert.True(listResponse.Headers.CacheControl is { NoStore: true });
        var items = (await BodyAsync(listResponse)).GetProperty("items").EnumerateArray().ToList();

        var item = Assert.Single(items);
        Assert.Equal(product.SkuId, item.GetProperty("skuId").GetGuid());
        Assert.Equal(product.Slug, item.GetProperty("slug").GetString());
        Assert.Equal("4500.00", item.GetProperty("price").GetProperty("amount").GetString());
        Assert.Equal("InStock", item.GetProperty("availability").GetString());
        Assert.True(item.GetProperty("onSale").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await customer.DeleteAsync($"/api/customers/me/wishlist/items/{product.SkuId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await customer.DeleteAsync($"/api/customers/me/wishlist/items/{product.SkuId}")).StatusCode);

        Assert.Empty((await BodyAsync(await customer.GetAsync("/api/customers/me/wishlist"))).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task I_G1_Wishlist_ArchivedProductStaysAndReadsBackNotOnSale()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("WishArch"), "Old Toy");
        var (customer, _) = await RegisterAndSignInCustomerAsync();
        await customer.PutAsync($"/api/customers/me/wishlist/items/{product.SkuId}", null);

        var archive = await staff.PostAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/archive",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var item = Assert.Single((await BodyAsync(await customer.GetAsync("/api/customers/me/wishlist"))).GetProperty("items").EnumerateArray());
        Assert.False(item.GetProperty("onSale").GetBoolean());
        Assert.Equal("OutOfStock", item.GetProperty("availability").GetString());
    }

    [Fact]
    public async Task I_G1_Wishlist_UnknownSku_Is404_Anonymous_Is401_AndEachCustomerSeesOnlyTheirOwn()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("WishOwn"), "Scratcher");
        var (first, _) = await RegisterAndSignInCustomerAsync();
        var (second, _) = await RegisterAndSignInCustomerAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await first.PutAsync($"/api/customers/me/wishlist/items/{Guid.NewGuid()}", null)).StatusCode);

        await first.PutAsync($"/api/customers/me/wishlist/items/{product.SkuId}", null);
        Assert.Empty((await BodyAsync(await second.GetAsync("/api/customers/me/wishlist"))).GetProperty("items").EnumerateArray());

        var anonymous = await CreateClient().GetAsync("/api/customers/me/wishlist");
        Assert.True(anonymous.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task I_G1_Wishlist_ConcurrentFirstWrites_NeverReturn500()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("WishRace");
        var products = new List<SeededProduct>();
        for (var i = 0; i < 6; i++) products.Add(await CreateProductAsync(staff, brand, $"Race Toy {i}"));
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        var responses = await Task.WhenAll(products.Select(p => customer.PutAsync($"/api/customers/me/wishlist/items/{p.SkuId}", null)));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Conflict, $"Unexpected {(int)r.StatusCode}"));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.NoContent);
    }

    // ── I-G2 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G2_Alert_OnAnInStockSku_Is400_SkuAlreadyAvailable()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("AlertIn"), "In Stock Item");
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        var response = await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("SkuAlreadyAvailable", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task I_G2_Alert_OnAnOutOfStockSku_Is201WithDeliverability_ThenDuplicateRefused_ListedAndCancellable()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("AlertOut"), "Out Of Stock Item");
        await SetSkuOutOfStockAsync(product.SkuId);
        var (customer, _) = await RegisterAndSignInCustomerAsync();

        var created = await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await BodyAsync(created);
        var alertId = body.GetProperty("stockAlertId").GetGuid();
        var deliverability = body.GetProperty("deliverability");
        Assert.True(deliverability.TryGetProperty("willNotify", out _));
        Assert.True(deliverability.TryGetProperty("channels", out _));
        Assert.True(deliverability.TryGetProperty("inboxCopy", out _));

        var duplicate = await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Contains("StockAlertAlreadyRequested", await duplicate.Content.ReadAsStringAsync());

        var list = (await BodyAsync(await customer.GetAsync("/api/customers/me/stock-alerts"))).EnumerateArray().ToList();
        var mine = Assert.Single(list);
        Assert.Equal("Pending", mine.GetProperty("status").GetString());
        Assert.Equal(product.Slug, mine.GetProperty("slug").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await customer.DeleteAsync($"/api/customers/me/stock-alerts/{alertId}")).StatusCode);
        Assert.Equal("Closed", (await BodyAsync(await customer.GetAsync("/api/customers/me/stock-alerts"))).EnumerateArray().Single().GetProperty("status").GetString());

        // A new alert can be requested once the earlier one is closed.
        Assert.Equal(HttpStatusCode.Created, (await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json)).StatusCode);
    }

    [Fact]
    public async Task I_G2_AnotherCustomersAlert_Is404()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("AlertOwn"), "Owned Item");
        await SetSkuOutOfStockAsync(product.SkuId);
        var (owner, _) = await RegisterAndSignInCustomerAsync();
        var (other, _) = await RegisterAndSignInCustomerAsync();

        var alertId = (await BodyAsync(await owner.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json))).GetProperty("stockAlertId").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/customers/me/stock-alerts/{alertId}")).StatusCode);
        Assert.Empty((await BodyAsync(await other.GetAsync("/api/customers/me/stock-alerts"))).EnumerateArray());
    }

    [Fact]
    public async Task I_G2_Alert_OnAPrescriptionProduct_NeedsAnAuthorisedCat()
    {
        var staff = await SignInAsShopManagerAsync();
        var rx = await CreateProductAsync(staff, NewBrand("AlertRx"), "Renal Diet", "CatFood", prescription: true);
        await SetSkuOutOfStockAsync(rx.SkuId);
        var (customer, customerId) = await RegisterAndSignInCustomerAsync();
        await SeedCatAsync(customerId);

        // The shipped fail-closed gate authorises nobody (BR-2): no alert for something unbuyable.
        var refused = await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = rx.SkuId }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Contains("PurchaseNotPermitted", await refused.Content.ReadAsStringAsync());

        var (gateFactory, authorisations) = Factory.WithControllableDietGate();
        var (authorisedCustomer, authorisedId) = await RegisterAndSignInCustomerAsync(gateFactory);
        var cat = await SeedCatAsync(authorisedId);
        authorisations.Authorise(authorisedId, cat, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));

        Assert.Equal(HttpStatusCode.Created, (await authorisedCustomer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = rx.SkuId }, Json)).StatusCode);
    }

    [Fact]
    public async Task I_G2_Alert_OnUnknownSku_Is404()
    {
        var (customer, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = Guid.NewGuid() }, Json)).StatusCode);
    }

    // ── I-G3 / I-G4 ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G3_BackInStock_IsSentExactlyOnce_AndNeverAgainAfterASecondRestock()
    {
        var (daytime, _) = Factory.WithDaytimeClock();
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Once"), "Salmon Pate");
        await SetSkuOutOfStockAsync(product.SkuId);

        var email = NewEmail();
        var (customer, customerId) = await RegisterAndSignInCustomerAsync(daytime, email);
        await SetMarketingConsentAsync(customer, "Email", true);
        var preferred = await customer.PutAsJsonAsync("/api/customers/me/notification-preferences", new { preferredChannel = "Email" }, Json);
        Assert.Equal(HttpStatusCode.NoContent, preferred.StatusCode);

        var created = await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var deliverability = (await BodyAsync(created)).GetProperty("deliverability");
        Assert.True(deliverability.GetProperty("willNotify").GetBoolean());
        Assert.Contains("Email", deliverability.GetProperty("channels").EnumerateArray().Select(c => c.GetString()));

        // Still out of stock: the pass sends nothing.
        await RunShopCycleAsync(daytime);
        Assert.Equal(0, await CountRequestsAsync(customerId));

        // Stock arrives: the shop pass notifies, the notification pass delivers.
        await SetSkuInStockAsync(product.SkuId);
        await RunShopCycleAsync(daytime);
        await RunNotificationDeliveryUntilAsync(daytime, db => db.Set<NotificationAggregate>().AsNoTracking()
            .AnyAsync(n => n.RecipientId == customerId && n.TemplateKey == NotificationTemplateCatalogue.ShopBackInStock
                && n.Channel == NotificationChannel.Email && n.Status == NotificationStatus.Sent));

        var emailSender = NotificationsApiFactory.CapturingSender(daytime.Services, NotificationChannel.Email);
        var sent = Assert.Single(emailSender.Sent, m => m.Destination == email);
        Assert.Contains(product.Slug, sent.TextBody);
        Assert.Contains($"?sku={product.SkuId}", sent.TextBody);
        Assert.Contains("Salmon Pate", sent.TextBody);

        Assert.Equal(1, await CountRequestsAsync(customerId));
        Assert.Equal("Notified", (await BodyAsync(await customer.GetAsync("/api/customers/me/stock-alerts"))).EnumerateArray().Single().GetProperty("status").GetString());

        // Run both passes again: nothing more.
        await RunShopCycleAsync(daytime);
        await RunNotificationDeliveryPassAsync(daytime);
        Assert.Equal(1, await CountRequestsAsync(customerId));

        // Sell out and restock: Notified is terminal, so still nothing more is sent.
        await SetSkuOutOfStockAsync(product.SkuId);
        await RunShopCycleAsync(daytime);
        await SetSkuInStockAsync(product.SkuId);
        await RunShopCycleAsync(daytime);
        await RunNotificationDeliveryPassAsync(daytime);

        Assert.Equal(1, await CountRequestsAsync(customerId));
        Assert.Single(emailSender.Sent, m => m.Destination == email);
    }

    [Fact]
    public async Task I_G3_TwoCustomersAskingForTheSameSku_AreEachToldOnce_ByOnePass()
    {
        var (daytime, _) = Factory.WithDaytimeClock();
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Two"), "Tuna Flakes");
        await SetSkuOutOfStockAsync(product.SkuId);

        var customers = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            var (client, id) = await RegisterAndSignInCustomerAsync(daytime, NewEmail());
            await SetMarketingConsentAsync(client, "Email", true);
            await client.PutAsJsonAsync("/api/customers/me/notification-preferences", new { preferredChannel = "Email" }, Json);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json)).StatusCode);
            customers.Add(id);
        }

        await SetSkuInStockAsync(product.SkuId);
        await RunShopCycleAsync(daytime);
        await RunShopCycleAsync(daytime);

        foreach (var id in customers) Assert.Equal(1, await CountRequestsAsync(id));
    }

    [Fact]
    public async Task I_G4_NoMarketingConsent_DeliverabilityIsFalse_AndTheRequestIsSuppressedNoPermittedChannel()
    {
        var (daytime, _) = Factory.WithDaytimeClock();
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("NoConsent"), "Chicken Bites");
        await SetSkuOutOfStockAsync(product.SkuId);

        var (customer, customerId) = await RegisterAndSignInCustomerAsync(daytime, NewEmail());

        var created = await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var deliverability = (await BodyAsync(created)).GetProperty("deliverability");
        Assert.False(deliverability.GetProperty("willNotify").GetBoolean());
        Assert.Equal(0, deliverability.GetProperty("channels").GetArrayLength());

        await SetSkuInStockAsync(product.SkuId);
        await RunShopCycleAsync(daytime);
        await RunNotificationDeliveryUntilAsync(daytime, async db =>
        {
            var request = await db.Set<NotificationRequestAggregate>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.RecipientId == customerId && r.TemplateKey == NotificationTemplateCatalogue.ShopBackInStock);
            return request is not null && request.Status == NotificationRequestStatus.Suppressed;
        });

        await using var scope = daytime.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var suppressed = await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .SingleAsync(r => r.RecipientId == customerId && r.TemplateKey == NotificationTemplateCatalogue.ShopBackInStock);
        Assert.Equal(NotificationSuppressionReason.NoPermittedChannel, suppressed.SuppressionReason);

        // The alert itself was still Notified -- "told once" is about the shop's side; whether a
        // channel carries it is the consent's (F33, OQ-2).
        Assert.Equal("Notified", (await BodyAsync(await customer.GetAsync("/api/customers/me/stock-alerts"))).EnumerateArray().Single().GetProperty("status").GetString());
    }

    // ── I-G5 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G5_DiscontinuingTheSku_ClosesAPendingAlert_SkuNoLongerSold()
    {
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Disc");
        var taxClassId = await SeedActiveTaxClassAsync(UniqueCode("TAX"));

        var create = await staff.PostAsJsonAsync("/api/staff/shop/products", new { kind = "Standard", name = "Two Size Bowl", brand, category = "LitterPotsAndAccessories", taxClassId, slug = UniqueSlug("bowl") }, Json);
        var productId = (await BodyAsync(create)).GetProperty("productId").GetGuid();

        async Task<Guid> AddSkuAsync(string size)
        {
            var response = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus",
                new { expectedVersion = await ProductVersionAsync(staff, productId), code = UniqueCode("BOWL"), sizeOrWeight = size }, Json);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await BodyAsync(response)).GetProperty("skuId").GetGuid();
        }

        // Backlog row 32: a SKU added to an ACTIVE product now starts inactive (not on sale, so no alert can be
        // requested for it), so both sizes exist and are priced before publication; the large one's price is then
        // moved to tomorrow, which is what keeps it out of stock.
        var small = await AddSkuAsync("Small");
        var large = await AddSkuAsync("Large");
        await SchedulePriceAsync(staff, small, 400m);
        await SchedulePriceAsync(staff, large, 450m);
        await PublishAsync(staff, productId);
        await SetSkuOutOfStockAsync(large);

        var (customer, _) = await RegisterAndSignInCustomerAsync();
        var created = await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = large }, Json);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var discontinue = await staff.PostAsJsonAsync($"/api/staff/shop/products/{productId}/skus/{large}/discontinue",
            new { expectedVersion = await ProductVersionAsync(staff, productId) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, discontinue.StatusCode);

        var alert = Assert.Single((await BodyAsync(await customer.GetAsync("/api/customers/me/stock-alerts"))).EnumerateArray());
        Assert.Equal("Closed", alert.GetProperty("status").GetString());

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var stored = await db.StockAlerts.AsNoTracking().SingleAsync(a => a.Id == alert.GetProperty("stockAlertId").GetGuid());
        Assert.Equal(StockAlertClosureReason.SkuNoLongerSold, stored.ClosureReason);
    }

    [Fact]
    public async Task I_G5_ArchivingTheProduct_ClosesPendingAlertsOnEverySku()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("Arch"), "Soon Gone");
        await SetSkuOutOfStockAsync(product.SkuId);
        var (customer, _) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.Created, (await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json)).StatusCode);

        var archive = await staff.PostAsJsonAsync($"/api/staff/shop/products/{product.ProductId}/archive",
            new { expectedVersion = await ProductVersionAsync(staff, product.ProductId) }, Json);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        Assert.Equal("Closed", Assert.Single((await BodyAsync(await customer.GetAsync("/api/customers/me/stock-alerts"))).EnumerateArray()).GetProperty("status").GetString());
    }

    // ── I-G6 / I-G7 ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task I_G6_Merge_MovesWishlistAndAlerts_WithdrawsADuplicateReview_AndIsIdempotent()
    {
        var (purchaseFactory, purchases) = Factory.WithControllablePurchaseHistory();
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Merge");

        var reviewed = await CreateProductAsync(staff, brand, "Reviewed Food", "CatFood", lifeStage: "Adult");
        var wished = await CreateProductAsync(staff, brand, "Wished Toy");
        var alerted = await CreateProductAsync(staff, brand, "Alerted Item");
        await SetSkuOutOfStockAsync(alerted.SkuId);

        var (survivor, survivorId) = await RegisterAndSignInCustomerAsync(purchaseFactory);
        var (duplicate, duplicateId) = await RegisterAndSignInCustomerAsync(purchaseFactory);
        purchases.SetPurchased(survivorId, reviewed.SkuId);
        purchases.SetPurchased(duplicateId, reviewed.SkuId);

        var survivorReview = await survivor.PostAsJsonAsync("/api/customers/me/reviews", new { productId = reviewed.ProductId, skuId = reviewed.SkuId, rating = 5, body = "Survivor review" }, Json);
        var duplicateReview = await duplicate.PostAsJsonAsync("/api/customers/me/reviews", new { productId = reviewed.ProductId, skuId = reviewed.SkuId, rating = 2, body = "Duplicate review" }, Json);
        Assert.Equal(HttpStatusCode.Created, survivorReview.StatusCode);
        Assert.Equal(HttpStatusCode.Created, duplicateReview.StatusCode);
        var duplicateReviewId = (await BodyAsync(duplicateReview)).GetProperty("reviewId").GetGuid();
        var survivorReviewId = (await BodyAsync(survivorReview)).GetProperty("reviewId").GetGuid();

        await duplicate.PutAsync($"/api/customers/me/wishlist/items/{wished.SkuId}", null);
        var alertId = (await BodyAsync(await duplicate.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = alerted.SkuId }, Json))).GetProperty("stockAlertId").GetGuid();

        var sharedNumber = NewMobile();
        await ShareContactNumberAsync(survivor, sharedNumber);
        await ShareContactNumberAsync(duplicate, sharedNumber);

        var (_, mobile, password) = await SeedStaffAsync("Administrator");
        var admin = await SignInStaffAsync(mobile, password);
        var merge = await admin.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge", new { survivingCustomerId = survivorId, reason = "Duplicate registration" }, Json);
        Assert.Equal(HttpStatusCode.OK, merge.StatusCode);

        async Task<(int WishlistOnSurvivor, int WishlistOnDuplicate, Guid AlertOwner, StockAlertStatus AlertStatus, ProductReviewStatus DuplicateReview, ProductReviewStatus SurvivorReview)> SnapshotAsync()
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var survivorWishlist = await db.Wishlists.AsNoTracking().FirstOrDefaultAsync(w => w.Id == survivorId);
            var duplicateWishlist = await db.Wishlists.AsNoTracking().FirstOrDefaultAsync(w => w.Id == duplicateId);
            var alert = await db.StockAlerts.AsNoTracking().SingleAsync(a => a.Id == alertId);
            var dupReview = await db.ProductReviews.AsNoTracking().SingleAsync(r => r.Id == duplicateReviewId);
            var surReview = await db.ProductReviews.AsNoTracking().SingleAsync(r => r.Id == survivorReviewId);
            return (survivorWishlist?.Items.Count ?? 0, duplicateWishlist?.Items.Count ?? 0, alert.CustomerId, alert.Status, dupReview.Status, surReview.Status);
        }

        var after = await SnapshotAsync();
        Assert.Equal(1, after.WishlistOnSurvivor);
        Assert.Equal(0, after.WishlistOnDuplicate);
        Assert.Equal(survivorId, after.AlertOwner);
        Assert.Equal(StockAlertStatus.Pending, after.AlertStatus);
        Assert.Equal(ProductReviewStatus.Withdrawn, after.DuplicateReview);
        Assert.Equal(ProductReviewStatus.PendingModeration, after.SurvivorReview);

        // The survivor now sees the moved item and alert.
        Assert.Single((await BodyAsync(await survivor.GetAsync("/api/customers/me/wishlist"))).GetProperty("items").EnumerateArray());
        Assert.Single((await BodyAsync(await survivor.GetAsync("/api/customers/me/stock-alerts"))).EnumerateArray());

        // Re-running the handler (the merge event re-sent) finds nothing left to move: nothing changes.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetServices<Platform.Infrastructure.Core.Events.IEventHandlerAsync<CatEcommerce.Domain.Events.Account.CustomerMergedEvent>>()
                .OfType<CatEcommerce.Application.DomainEventHandlers.Product.ReassignShopDataOnCustomerMergedHandler>().Single();
            await handler.HandleAsync(new CatEcommerce.Domain.Events.Account.CustomerMergedEvent
            {
                DuplicateCustomerId = duplicateId, SurvivingCustomerId = survivorId, Reason = "Duplicate registration", IsReapplication = true,
                CorrelationId = Guid.NewGuid(), UserContext = new UserContext(Guid.NewGuid(), Guid.NewGuid())
            });
        }

        Assert.Equal(after, await SnapshotAsync());
    }

    [Fact]
    public async Task I_G6_Merge_WhenTheSurvivorAlreadyHasAPendingAlertForTheSku_TheDuplicatesIsClosedMergedDuplicate()
    {
        var staff = await SignInAsShopManagerAsync();
        var product = await CreateProductAsync(staff, NewBrand("MergeAlert"), "Shared Alert Item");
        await SetSkuOutOfStockAsync(product.SkuId);

        var (survivor, survivorId) = await RegisterAndSignInCustomerAsync();
        var (duplicate, duplicateId) = await RegisterAndSignInCustomerAsync();
        Assert.Equal(HttpStatusCode.Created, (await survivor.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json)).StatusCode);
        var duplicateAlertId = (await BodyAsync(await duplicate.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = product.SkuId }, Json))).GetProperty("stockAlertId").GetGuid();

        var sharedNumber = NewMobile();
        await ShareContactNumberAsync(survivor, sharedNumber);
        await ShareContactNumberAsync(duplicate, sharedNumber);

        var (_, mobile, password) = await SeedStaffAsync("Administrator");
        var admin = await SignInStaffAsync(mobile, password);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/api/staff/customers/{duplicateId}/merge", new { survivingCustomerId = survivorId, reason = "Duplicate" }, Json)).StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var closed = await db.StockAlerts.AsNoTracking().SingleAsync(a => a.Id == duplicateAlertId);
        Assert.Equal(StockAlertStatus.Closed, closed.Status);
        Assert.Equal(StockAlertClosureReason.MergedDuplicate, closed.ClosureReason);
        Assert.Equal(1, await db.StockAlerts.AsNoTracking().CountAsync(a => a.CustomerId == survivorId && a.SkuId == product.SkuId && a.Status == StockAlertStatus.Pending));
    }

    [Fact]
    public async Task I_G7_Anonymise_ClearsTheWishlist_ClosesPendingAlerts_AndKeepsPublishedReviews()
    {
        var (purchaseFactory, purchases) = Factory.WithControllablePurchaseHistory();
        var staff = await SignInAsShopManagerAsync();
        var brand = NewBrand("Anon");
        var reviewed = await CreateProductAsync(staff, brand, "Anon Reviewed Food", "CatFood", lifeStage: "Adult");
        var wished = await CreateProductAsync(staff, brand, "Anon Wished Toy");
        var alerted = await CreateProductAsync(staff, brand, "Anon Alerted Item");
        await SetSkuOutOfStockAsync(alerted.SkuId);

        var (customer, customerId) = await RegisterAndSignInCustomerAsync(purchaseFactory);
        purchases.SetPurchased(customerId, reviewed.SkuId);

        var review = await customer.PostAsJsonAsync("/api/customers/me/reviews", new { productId = reviewed.ProductId, skuId = reviewed.SkuId, rating = 4, body = "Cats loved it" }, Json);
        var reviewId = (await BodyAsync(review)).GetProperty("reviewId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await staff.PostAsync($"/api/staff/shop/reviews/{reviewId}/publish", null)).StatusCode);

        // Row 21 / BSB1: a review still awaiting moderation when the customer is anonymised.
        var pendingProduct = await CreateProductAsync(staff, brand, "Anon Pending Food", "CatFood", lifeStage: "Adult");
        purchases.SetPurchased(customerId, pendingProduct.SkuId);
        var pendingResponse = await customer.PostAsJsonAsync("/api/customers/me/reviews",
            new { productId = pendingProduct.ProductId, skuId = pendingProduct.SkuId, rating = 2, title = "Private title", body = "Private words" }, Json);
        var pendingReviewId = (await BodyAsync(pendingResponse)).GetProperty("reviewId").GetGuid();

        await customer.PutAsync($"/api/customers/me/wishlist/items/{wished.SkuId}", null);
        var alertId = (await BodyAsync(await customer.PostAsJsonAsync("/api/customers/me/stock-alerts", new { skuId = alerted.SkuId }, Json))).GetProperty("stockAlertId").GetGuid();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<CustomerAccountAggregate>>();
            var account = await repository.GetByIdAsync(customerId);
            account!.Anonymise(new CustomerAnonymisationDto(DateTimeOffset.UtcNow, new UserContext(Guid.NewGuid(), Guid.NewGuid()), Guid.NewGuid()));
            await repository.UpdateAsync(account, EventPublishOption.InMemoryPublishOnly);
        }

        await using var verify = Factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        var wishlist = await db.Wishlists.AsNoTracking().SingleAsync(w => w.Id == customerId);
        Assert.Empty(wishlist.Items);

        var alert = await db.StockAlerts.AsNoTracking().SingleAsync(a => a.Id == alertId);
        Assert.Equal(StockAlertStatus.Closed, alert.Status);
        Assert.Equal(StockAlertClosureReason.CustomerAnonymised, alert.ClosureReason);

        var pending = await db.ProductReviews.AsNoTracking().SingleAsync(r => r.Id == pendingReviewId);
        Assert.Null(pending.Title);
        Assert.Equal("", pending.Body);
        Assert.Equal(ProductReviewStatus.Withdrawn, pending.Status);
        Assert.NotEqual(HttpStatusCode.NoContent, (await staff.PostAsync($"/api/staff/shop/reviews/{pendingReviewId}/publish", null)).StatusCode);

        // The published review is still public, credited to "A verified buyer".
        var reviews = await (await CreateClient().GetAsync($"/api/shop/products/{reviewed.Slug}/reviews")).Content.ReadFromJsonAsync<JsonElement>(Json);
        var publicReview = Assert.Single(reviews.GetProperty("items").EnumerateArray());
        Assert.Equal("A verified buyer", publicReview.GetProperty("authorDisplayName").GetString());
        Assert.Equal("Cats loved it", publicReview.GetProperty("body").GetString());
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────

    private async Task<int> CountRequestsAsync(Guid customerId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
        return await db.Set<NotificationRequestAggregate>().AsNoTracking()
            .CountAsync(r => r.RecipientId == customerId && r.TemplateKey == NotificationTemplateCatalogue.ShopBackInStock);
    }
}
