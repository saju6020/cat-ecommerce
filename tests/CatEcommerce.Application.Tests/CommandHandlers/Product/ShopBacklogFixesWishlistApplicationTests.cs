using CatEcommerce.Application.CommandHandlers.Product;
using CatEcommerce.Application.CommandHandlers.Validators;
using CatEcommerce.Application.Commands.Product;
using CatEcommerce.Application.DomainEventHandlers.Product;
using CatEcommerce.Application.Tests.TestSupport;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Persistence.Views;
using CatEcommerce.Read.QueryHandlers.Product;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Application.Tests.CommandHandlers.Product;

/// <summary>HO-SHP-001 backlog rows 18 and 21 at handler level.</summary>
public sealed class ShopBacklogFixesWishlistApplicationTests
{
    private static UserContext Ctx() => new(Guid.NewGuid(), Guid.NewGuid()) { Roles = ["ShopManager"] };

    private static ProductAggregate NewProduct(string slug = "scratch-post")
    {
        var product = new ProductAggregate(Guid.NewGuid());
        product.Create(new ProductCreateDto(ProductKind.Standard, "Scratch Post", "Acme", ProductCategory.ToysAndFurniture,
            null, Guid.NewGuid(), slug, null, null, Ctx(), Guid.NewGuid()));
        return product;
    }

    private static ProductAggregate ProductWithSku(out Guid skuId, bool publish = false, bool listed = true)
    {
        var product = NewProduct();
        skuId = Guid.NewGuid();
        product.AddSku(new SkuAddDto(skuId, "SP-1", null, null, null, null, null), Ctx(), Guid.NewGuid());
        if (!listed)
            product.UpdateDetails(new ProductDetailsUpdateDto(product.Name, product.Brand, product.Description, product.Category, null, product.Tags,
                product.TaxClassId, null, false, false, [], Ctx(), Guid.NewGuid()));
        if (publish) product.Publish(new PublicationFacts([skuId], true, []), DateTimeOffset.UtcNow, Ctx(), Guid.NewGuid());
        return product;
    }

    // ── Row 18 ──────────────────────────────────────────────────────────────────────────────────

    private static (AddWishlistItemCommandHandler Handler, FakeAggregateRootRepository<WishlistAggregate> Wishlists) WishlistHandler(ProductAggregate product)
    {
        var products = new FakeAggregateRootRepository<ProductAggregate>();
        products.Seed(product);
        var wishlists = new FakeAggregateRootRepository<WishlistAggregate>();
        return (new AddWishlistItemCommandHandler(NullLogger<AddWishlistItemCommandHandler>.Instance, products, wishlists, TimeProvider.System), wishlists);
    }

    [Fact]
    public async Task AddWishlistItem_SkuOfADraftProduct_IsRefusedAsUnknown_AndNothingIsSaved()
    {
        var product = ProductWithSku(out var skuId, publish: false);
        var (handler, wishlists) = WishlistHandler(product);

        var response = await handler.HandleAsync(new AddWishlistItemCommand { SkuId = skuId, UserContext = Ctx(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.SkuNotFound);
        Assert.Empty(wishlists.Saved);
    }

    [Fact]
    public async Task AddWishlistItem_SkuOfAnUnlistedProduct_IsRefusedAsUnknown()
    {
        var product = ProductWithSku(out var skuId, publish: true, listed: false);
        var (handler, wishlists) = WishlistHandler(product);

        var response = await handler.HandleAsync(new AddWishlistItemCommand { SkuId = skuId, UserContext = Ctx(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        Assert.False(response.IsSuccess);
        Assert.Contains(response.ValidationResult!.Errors, e => e.ErrorCode == BusinessViolationCodes.SkuNotFound);
        Assert.Empty(wishlists.Saved);
    }

    [Fact]
    public async Task AddWishlistItem_SkuOfAnActiveListedProduct_IsAccepted()
    {
        var product = ProductWithSku(out var skuId, publish: true);
        var (handler, wishlists) = WishlistHandler(product);

        var response = await handler.HandleAsync(new AddWishlistItemCommand { SkuId = skuId, UserContext = Ctx(), CorrelationId = Guid.NewGuid(), ValidateCommand = true });

        Assert.True(response.IsSuccess);
        Assert.Single(wishlists.Saved);
    }

    // ── Row 21 ──────────────────────────────────────────────────────────────────────────────────

    private sealed class FakeFinder(IReadOnlyList<Guid> reviewIds) : IShopCustomerDataFinder
    {
        public Task<IReadOnlyList<Guid>> FindStockAlertIdsAsync(Guid customerId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlyList<Guid>> FindActiveReviewIdsAsync(Guid customerId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlyList<Guid>> FindUnpublishedReviewIdsAsync(Guid customerId, CancellationToken ct = default) => Task.FromResult(reviewIds);
        public Task<IReadOnlyList<Guid>> FindPendingStockAlertIdsBySkuAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private static ProductReviewAggregate Review(Guid customerId, bool publish)
    {
        var review = new ProductReviewAggregate(Guid.NewGuid());
        review.Submit(new ProductReviewSubmitDto(Guid.NewGuid(), Guid.NewGuid(), customerId, 4, "Title", "Body text", DateTimeOffset.UtcNow, Ctx(), Guid.NewGuid()));
        if (publish) review.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow, Ctx(), Guid.NewGuid());
        return review;
    }

    [Fact]
    public async Task Anonymisation_ClearsTheTextOfUnpublishedReviews_AndLeavesAPublishedOneAlone()
    {
        var customerId = Guid.NewGuid();
        var pending = Review(customerId, publish: false);
        var published = Review(customerId, publish: true);

        var reviews = new FakeAggregateRootRepository<ProductReviewAggregate>();
        reviews.Seed(pending);
        reviews.Seed(published);

        // The real finder returns only the unpublished ids; the published review is not among them.
        var handler = new ClearShopDataOnCustomerAnonymisedHandler(
            new FakeAggregateRootRepository<WishlistAggregate>(), new FakeAggregateRootRepository<StockAlertAggregate>(),
            reviews, new FakeFinder([pending.Id]));

        await handler.HandleAsync(new CustomerAccountAnonymisedEvent { CustomerId = customerId, UserContext = Ctx(), CorrelationId = Guid.NewGuid() });

        Assert.Null(pending.Title);
        Assert.Equal("", pending.Body);
        Assert.Contains(pending, reviews.Updated);

        Assert.Equal("Title", published.Title);
        Assert.Equal("Body text", published.Body);
        Assert.DoesNotContain(published, reviews.Updated);
    }
}
