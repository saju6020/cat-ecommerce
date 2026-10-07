using System.Linq.Expressions;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Notifications;
using CatEcommerce.Infrastructure.Notifications.Guards;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Xunit;

namespace CatEcommerce.Infrastructure.Tests.Notifications.Guards;

/// <summary>HO-SHP-001 §15 -- U-G5 (<c>StockAlertGuard</c>) and U-G4 (<c>ProductLink</c>).</summary>
public sealed class StockAlertGuardTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 6, 0, 0, TimeSpan.Zero);
    private static UserContext Ctx => new(Guid.NewGuid(), Guid.NewGuid());

    /// <summary>Hand-written in-memory repository, the same shape the sibling guard tests use.</summary>
    private sealed class FakeRepository<T> : IAggregateRootRepository<T> where T : AggregateRoot
    {
        private readonly Dictionary<Guid, T> _store = [];

        public void Seed(T aggregate) => _store[aggregate.Id] = aggregate;

        public Task<T> GetByIdAsync(Guid id) => Task.FromResult(_store.TryGetValue(id, out var value) ? value : null!);

        public Task<T> GetByFilterAsync(Expression<Func<T, bool>> dataFilters) =>
            Task.FromResult(_store.Values.AsQueryable().FirstOrDefault(dataFilters)!);

        public Task<bool> ExistAsync(Expression<Func<T, bool>> dataFilters) =>
            Task.FromResult(_store.Values.AsQueryable().Any(dataFilters));

        public Task SaveAsync(T aggregate, EventPublishOption eventPublishOption) { _store[aggregate.Id] = aggregate; return Task.CompletedTask; }

        public Task UpdateAsync(T aggregate, EventPublishOption eventPublishOption) { _store[aggregate.Id] = aggregate; return Task.CompletedTask; }

        public Task UpdateAsync(T aggregate, int expectedVersion, EventPublishOption eventPublishOption) => UpdateAsync(aggregate, eventPublishOption);
    }

    private sealed record Fixture(StockAlertGuard Guard, StockAlertAggregate Alert, ProductAggregate Product, Guid SkuId, Guid CustomerId);

    private static Fixture NewFixture(bool published = true, bool notified = true)
    {
        var customerId = Guid.NewGuid();
        var skuId = Guid.NewGuid();

        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Scoop", "PurrCare", ProductCategory.LitterPotsAndAccessories, null,
            Guid.NewGuid(), "scoop", null, null, Ctx, Guid.NewGuid()));
        product.AddSku(new SkuAddDto(skuId, "SCOOP-1", null, "Small", null, null, null), Ctx, Guid.NewGuid());
        if (published)
            product.Publish(new PublicationFacts([skuId], true, []), Now, Ctx, Guid.NewGuid());

        var alert = new StockAlertAggregate(Guid.NewGuid());
        alert.Request(customerId, skuId, Now, Ctx, Guid.NewGuid());
        if (notified) alert.MarkNotified(Now.AddHours(1), Ctx, Guid.NewGuid());

        var alerts = new FakeRepository<StockAlertAggregate>();
        alerts.Seed(alert);
        var products = new FakeRepository<ProductAggregate>();
        products.Seed(product);

        return new Fixture(new StockAlertGuard(alerts, products), alert, product, skuId, customerId);
    }

    [Fact]
    public async Task U_G5_NotifiedAlert_OnSaleSku_IsValid()
    {
        var f = NewFixture();
        Assert.True(await f.Guard.IsStillValidAsync(f.CustomerId, f.Alert.Id, null, null));
    }

    [Fact]
    public async Task U_G5_NoStockCheckAtSendTime_AGuardNeverReadsAvailability()
    {
        // The guard has no availability dependency at all (section 8.11: a quiet-hours hold may
        // outlast a brief sell-out) -- constructing it with repositories only is the proof.
        var f = NewFixture();
        Assert.True(await f.Guard.IsStillValidAsync(f.CustomerId, f.Alert.Id, null, null));
    }

    [Fact]
    public async Task U_G5_SkuDiscontinued_IsNotValid()
    {
        var f = NewFixture();
        // Backlog row 32: a SKU cannot be added to an Active product (it would be unpriced), so the product is
        // taken back to Draft, gains its second (priced) SKU and is published again before the first is discontinued.
        var second = Guid.NewGuid();
        f.Product.Unpublish(Ctx, Guid.NewGuid());
        f.Product.AddSku(new SkuAddDto(second, "SCOOP-2", null, "Large", null, null, null), Ctx, Guid.NewGuid());
        f.Product.Publish(new PublicationFacts([f.SkuId, second], true, []), Now, Ctx, Guid.NewGuid());
        f.Product.DiscontinueSku(f.SkuId, Ctx, Guid.NewGuid());

        Assert.False(await f.Guard.IsStillValidAsync(f.CustomerId, f.Alert.Id, null, null));
    }

    [Fact]
    public async Task U_G5_ProductArchived_IsNotValid()
    {
        var f = NewFixture();
        f.Product.Archive(Ctx, Guid.NewGuid());

        Assert.False(await f.Guard.IsStillValidAsync(f.CustomerId, f.Alert.Id, null, null));
    }

    [Fact]
    public async Task U_G5_PendingAlert_NoSubject_OrUnknownAlert_IsNotValid()
    {
        var pending = NewFixture(notified: false);
        Assert.False(await pending.Guard.IsStillValidAsync(pending.CustomerId, pending.Alert.Id, null, null));

        var f = NewFixture();
        Assert.False(await f.Guard.IsStillValidAsync(f.CustomerId, null, null, null));
        Assert.False(await f.Guard.IsStillValidAsync(f.CustomerId, Guid.NewGuid(), null, null));
    }

    [Fact]
    public void U_G5_GuardKind_IsStockAlert()
    {
        var f = NewFixture();
        Assert.Equal(NotificationSubjectKind.StockAlert, f.Guard.Kind);
    }

    // ── U-G4 ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void U_G4_ProductLink_IsTheShopProductPageWithTheSkuSelected()
    {
        var builder = new NotificationLinkBuilder(
            Options.Create(new NotificationOptions()),
            Options.Create(new PublicSiteOptions { WebBaseUrl = "https://portal.example.test/" }));
        var skuId = Guid.NewGuid();

        Assert.Equal($"https://portal.example.test/shop/products/royal-canin-kitten?sku={skuId}", builder.ProductLink("royal-canin-kitten", skuId));
    }
}
