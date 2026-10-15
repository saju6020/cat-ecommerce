using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;

namespace CatEcommerce.IntegrationTests;

/// <summary>
/// HO-ORD-001 §7 "to be verified at the start of Phase A" -- recorded as tests (I-A3). V1 (the order-number sequence) is verified with the
/// migration that creates the sequence (Phase D); V2 and V3 are here.
/// </summary>
public sealed class OrderingPlatformFactsEndpointTests(OrderingApiFactory factory) : OrderingIntegrationTestBase(factory)
{
    /// <summary>
    /// V2 (ADR-201): a tracked root mutated in the same scope as <c>repository.SaveAsync(other)</c> is committed by that save -- and its domain
    /// events are <b>not</b> dispatched, because the repository dispatches and clears only the passed aggregate's. Proved here with two Shop roots
    /// (a wishlist as the "saved" root, a stock alert as the "enlisted" one), the same shape as the order (saved) and the cart (tracked).
    /// </summary>
    [Fact]
    public async Task V2_ATrackedSecondRoot_IsCommittedBytheFirstRootsSave_AndItsEventsAreNotDispatched()
    {
        var customerId = Guid.NewGuid();
        var skuId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var user = new UserContext(customerId, Guid.NewGuid());

        StockAlertAggregate enlisted;
        WishlistAggregate saved;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var wishlists = scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<WishlistAggregate>>();

            enlisted = new StockAlertAggregate(alertId);
            enlisted.Request(customerId, skuId, Now, user, Guid.NewGuid());
            db.Set<StockAlertAggregate>().Add(enlisted); // staged, never saved by itself

            saved = new WishlistAggregate(customerId);
            saved.AddItem(skuId, Guid.NewGuid(), Now, user, Guid.NewGuid());
            Assert.Single(enlisted.Events);
            Assert.Single(saved.Events);

            await wishlists.SaveAsync(saved, EventPublishOption.InMemoryPublishOnly);
        }

        // atomic: the enlisted root's row is there although its own repository was never called
        var persisted = await WithDbAsync(db => db.Set<StockAlertAggregate>().AsNoTracking().Where(a => a.Id == alertId).Select(a => a.CustomerId).SingleAsync());
        Assert.Equal(customerId, persisted);

        // the passed root's events were dispatched and cleared; the enlisted root's were not
        Assert.Empty(saved.Events);
        Assert.Single(enlisted.Events);
    }

    /// <summary>The failure half of V2's atomicity: when the saved root's insert fails, the enlisted root is not committed either.</summary>
    [Fact]
    public async Task V2_WhenTheSavedRootFails_TheEnlistedRootIsNotCommitted()
    {
        var customerId = Guid.NewGuid();
        var alertId = Guid.NewGuid();
        var user = new UserContext(customerId, Guid.NewGuid());

        // a wishlist's id is the customer id: a second wishlist for the same customer collides on the primary key
        await using (var seed = Factory.Services.CreateAsyncScope())
        {
            var first = new WishlistAggregate(customerId);
            first.AddItem(Guid.NewGuid(), Guid.NewGuid(), Now, user, Guid.NewGuid());
            await seed.ServiceProvider.GetRequiredService<IAggregateRootRepository<WishlistAggregate>>().SaveAsync(first, EventPublishOption.InMemoryPublishOnly);
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatEcommerceDbContext>();
            var enlisted = new StockAlertAggregate(alertId);
            enlisted.Request(customerId, Guid.NewGuid(), Now, user, Guid.NewGuid());
            db.Set<StockAlertAggregate>().Add(enlisted);

            var duplicate = new WishlistAggregate(customerId);
            duplicate.AddItem(Guid.NewGuid(), Guid.NewGuid(), Now, user, Guid.NewGuid());

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                scope.ServiceProvider.GetRequiredService<IAggregateRootRepository<WishlistAggregate>>().SaveAsync(duplicate, EventPublishOption.InMemoryPublishOnly));
        }

        Assert.False(await WithDbAsync(db => db.Set<StockAlertAggregate>().AsNoTracking().AnyAsync(a => a.Id == alertId)));
    }
}
