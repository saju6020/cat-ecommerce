using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Product;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Events.Account;
using CatEcommerce.Domain.Events.Product;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using Platform.Infrastructure.Common.Security;
using Platform.Infrastructure.Core.Commands;
using Platform.Infrastructure.Core.Domain;
using Platform.Infrastructure.Core.Events;

namespace CatEcommerce.Application.DomainEventHandlers.Product;

// Every handler in this file is idempotent, loops with ONE UpdateAsync per row, and NEVER enqueues a
// notification (HO-NTF-001 ADR-057). A failure here fails the request AFTER the source change
// committed -- re-sending the source command (or the HO-ACC-001 merge/anonymise request) is the
// recovery, because a re-run finds nothing left to move (.ai/domain-events-and-outbox.md section 3).

/// <summary>HO-SHP-001 section 11.6 -- a discontinued SKU can never be bought again, so a customer's
/// pending alert for it is closed (<c>SkuNoLongerSold</c>).</summary>
public sealed class CloseStockAlertsOnSkuDiscontinuedHandler(
    IShopCustomerDataFinder finder, IAggregateRootRepository<StockAlertAggregate> alertRepository)
    : IEventHandlerAsync<SkuDiscontinuedEvent>
{
    public async Task HandleAsync(SkuDiscontinuedEvent @event) =>
        await StockAlertClosing.CloseAsync(
            await finder.FindPendingStockAlertIdsBySkuAsync([@event.SkuId]), alertRepository,
            StockAlertClosureReason.SkuNoLongerSold, @event.UserContext, @event.CorrelationId);
}

/// <summary>HO-SHP-001 section 11.6 -- the same for every SKU of an archived product.</summary>
public sealed class CloseStockAlertsOnProductArchivedHandler(
    IAggregateRootRepository<ProductAggregate> productRepository, IShopCustomerDataFinder finder,
    IAggregateRootRepository<StockAlertAggregate> alertRepository)
    : IEventHandlerAsync<ProductArchivedEvent>
{
    public async Task HandleAsync(ProductArchivedEvent @event)
    {
        var product = await productRepository.GetByIdAsync(@event.ProductId);
        if (product is null) return;

        var skuIds = product.Skus.Select(s => s.Id).ToList();
        await StockAlertClosing.CloseAsync(
            await finder.FindPendingStockAlertIdsBySkuAsync(skuIds), alertRepository,
            StockAlertClosureReason.SkuNoLongerSold, @event.UserContext, @event.CorrelationId);
    }
}

internal static class StockAlertClosing
{
    public static async Task CloseAsync(
        IReadOnlyList<Guid> alertIds, IAggregateRootRepository<StockAlertAggregate> alertRepository,
        StockAlertClosureReason reason, UserContext userContext, Guid correlationId)
    {
        foreach (var id in alertIds)
        {
            var alert = await alertRepository.GetByIdAsync(id);
            if (alert is null || alert.Status != StockAlertStatus.Pending) continue;

            alert.Close(reason, userContext, correlationId);
            await alertRepository.UpdateAsync(alert, EventPublishOption.InMemoryPublishOnly);
        }
    }
}

/// <summary>
/// HO-SHP-001 section 11.6 / ADR-096 -- HO-ACC-001 <c>CustomerMergedEvent</c> is binding on every
/// context holding a customer id (ADR-039). The duplicate's wishlist items are unioned into the
/// survivor's (the 200-item cap respected: the oldest duplicate items beyond it are dropped and
/// counted in the audit row) and the duplicate's wishlist emptied; its alerts move to the
/// survivor, a pending one closing <c>MergedDuplicate</c> when the survivor already has a pending
/// alert for that SKU; its reviews move to the survivor, or are withdrawn when the survivor already
/// reviewed the product (one non-withdrawn review per customer per product).
/// </summary>
public sealed class ReassignShopDataOnCustomerMergedHandler(
    IAggregateRootRepository<WishlistAggregate> wishlistRepository,
    IAggregateRootRepository<StockAlertAggregate> alertRepository,
    IAggregateRootRepository<ProductReviewAggregate> reviewRepository,
    IAggregateRootRepository<AuditEntryAggregate> auditRepository,
    IShopCustomerDataFinder finder,
    IClientIpAccessor clientIpAccessor,
    TimeProvider timeProvider)
    : IEventHandlerAsync<CustomerMergedEvent>
{
    public async Task HandleAsync(CustomerMergedEvent @event)
    {
        var duplicateId = @event.DuplicateCustomerId;
        var survivorId = @event.SurvivingCustomerId;

        var (wishlistMoved, wishlistDropped) = await MergeWishlistAsync(duplicateId, survivorId, @event);
        var (alertsMoved, alertsClosed) = await MergeAlertsAsync(duplicateId, survivorId, @event);
        var (reviewsMoved, reviewsWithdrawn) = await MergeReviewsAsync(duplicateId, survivorId, @event);

        if (wishlistMoved + wishlistDropped + alertsMoved + alertsClosed + reviewsMoved + reviewsWithdrawn == 0) return;

        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            EntityType: "Customer", EntityId: survivorId, Action: "ShopDataReassigned", Reason: "CustomerMerged",
            ActorUserId: @event.UserContext.UserId, OccurredOn: timeProvider.GetUtcNow(), BeforeValue: null,
            AfterValue: $"WishlistMoved={wishlistMoved};WishlistDropped={wishlistDropped};AlertsMoved={alertsMoved};AlertsClosed={alertsClosed};ReviewsMoved={reviewsMoved};ReviewsWithdrawn={reviewsWithdrawn}",
            UserContext: @event.UserContext, CorrelationId: @event.CorrelationId, Category: AuditCategory.BusinessEvent,
            IpAddress: clientIpAccessor.GetClientIpAddress()));

        await auditRepository.SaveAsync(entry, EventPublishOption.DoNotPublish);
    }

    private async Task<(int Moved, int Dropped)> MergeWishlistAsync(Guid duplicateId, Guid survivorId, CustomerMergedEvent @event)
    {
        var duplicate = await wishlistRepository.GetByIdAsync(duplicateId);
        if (duplicate is null || duplicate.Items.Count == 0) return (0, 0);

        var survivor = await wishlistRepository.GetByIdAsync(survivorId);
        var isNew = survivor is null;
        survivor ??= new WishlistAggregate(survivorId);

        var (moved, dropped) = survivor.MergeItemsFrom(duplicate.Items, @event.UserContext);
        if (moved > 0)
        {
            if (isNew) await wishlistRepository.SaveAsync(survivor, EventPublishOption.DoNotPublish);
            else await wishlistRepository.UpdateAsync(survivor, EventPublishOption.DoNotPublish);
        }

        duplicate.Clear(@event.UserContext, @event.CorrelationId);
        await wishlistRepository.UpdateAsync(duplicate, EventPublishOption.DoNotPublish);

        return (moved, dropped);
    }

    private async Task<(int Moved, int Closed)> MergeAlertsAsync(Guid duplicateId, Guid survivorId, CustomerMergedEvent @event)
    {
        var moved = 0;
        var closed = 0;

        foreach (var id in await finder.FindStockAlertIdsAsync(duplicateId))
        {
            var alert = await alertRepository.GetByIdAsync(id);
            if (alert is null) continue;

            if (alert.Status == StockAlertStatus.Pending &&
                await alertRepository.ExistAsync(a => a.CustomerId == survivorId && a.SkuId == alert.SkuId && a.Status == StockAlertStatus.Pending))
            {
                alert.Close(StockAlertClosureReason.MergedDuplicate, @event.UserContext, @event.CorrelationId);
                closed++;
            }
            else if (alert.ReassignCustomer(survivorId))
            {
                moved++;
            }
            else
            {
                continue;
            }

            await alertRepository.UpdateAsync(alert, EventPublishOption.DoNotPublish);
        }

        return (moved, closed);
    }

    private async Task<(int Moved, int Withdrawn)> MergeReviewsAsync(Guid duplicateId, Guid survivorId, CustomerMergedEvent @event)
    {
        var moved = 0;
        var withdrawn = 0;

        foreach (var id in await finder.FindActiveReviewIdsAsync(duplicateId))
        {
            var review = await reviewRepository.GetByIdAsync(id);
            if (review is null) continue;

            if (await reviewRepository.ExistAsync(r => r.CustomerId == survivorId && r.ProductId == review.ProductId && r.Status != ProductReviewStatus.Withdrawn))
            {
                review.Withdraw(@event.UserContext, @event.CorrelationId);
                withdrawn++;
            }
            else if (review.ReassignCustomer(survivorId))
            {
                moved++;
            }
            else
            {
                continue;
            }

            await reviewRepository.UpdateAsync(review, EventPublishOption.DoNotPublish);
        }

        return (moved, withdrawn);
    }
}

/// <summary>HO-SHP-001 section 11.6 / ADR-096 -- HO-ACC-001 <c>CustomerAccountAnonymisedEvent</c>
/// (ADR-041): the wishlist is cleared and pending alerts close (<c>CustomerAnonymised</c>). A PUBLISHED
/// review stays public, its author rendering as "A verified buyer" because the display name is derived
/// from the account at read time and never stored here (ADR-096). Every other review (pending, rejected,
/// hidden, withdrawn) is not public, so ADR-096 does not justify keeping it: its title and body are cleared.</summary>
public sealed class ClearShopDataOnCustomerAnonymisedHandler(
    IAggregateRootRepository<WishlistAggregate> wishlistRepository,
    IAggregateRootRepository<StockAlertAggregate> alertRepository,
    IAggregateRootRepository<ProductReviewAggregate> reviewRepository,
    IShopCustomerDataFinder finder)
    : IEventHandlerAsync<CustomerAccountAnonymisedEvent>
{
    public async Task HandleAsync(CustomerAccountAnonymisedEvent @event)
    {
        var wishlist = await wishlistRepository.GetByIdAsync(@event.CustomerId);
        if (wishlist is not null && wishlist.Clear(@event.UserContext, @event.CorrelationId) > 0)
            await wishlistRepository.UpdateAsync(wishlist, EventPublishOption.DoNotPublish);

        foreach (var id in await finder.FindStockAlertIdsAsync(@event.CustomerId))
        {
            var alert = await alertRepository.GetByIdAsync(id);
            if (alert is null || alert.Status != StockAlertStatus.Pending) continue;

            alert.Close(StockAlertClosureReason.CustomerAnonymised, @event.UserContext, @event.CorrelationId);
            await alertRepository.UpdateAsync(alert, EventPublishOption.DoNotPublish);
        }

        foreach (var id in await finder.FindUnpublishedReviewIdsAsync(@event.CustomerId))
        {
            var review = await reviewRepository.GetByIdAsync(id);
            if (review is null || !review.ClearTextForAnonymisation()) continue;

            await reviewRepository.UpdateAsync(review, EventPublishOption.DoNotPublish);
        }
    }
}
