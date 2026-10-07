using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Infrastructure.Configuration;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.Queries.Product;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.Infrastructure.Core.Queries;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>HO-SHP-001 section 12.4 -- US-SHP-13. Items for an archived product or discontinued SKU
/// stay and read back with <c>onSale: false</c>. Column projections only (the wishlist items, the
/// SKU rows, the price rows and the primary images), never the aggregates.</summary>
public sealed class GetWishlistQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider, ISkuAvailabilityReader availabilityReader)
    : IQueryHandlerAsync<GetWishlistQuery, WishlistViewModel>
{
    public async Task<QueryResponse<WishlistViewModel>> HandleAsync(GetWishlistQuery query)
    {
        var wished = await db.Wishlists.AsNoTracking()
            .Where(w => w.Id == query.CustomerId)
            .SelectMany(w => w.Items, (w, i) => new { i.SkuId, i.AddedOn })
            .OrderByDescending(i => i.AddedOn)
            .ToListAsync();

        if (wished.Count == 0)
            return new QueryResponse<WishlistViewModel> { Result = new WishlistViewModel([]) };

        var now = timeProvider.GetUtcNow();
        var skuIds = wished.Select(i => i.SkuId).ToList();

        var skus = await db.SkuRows().Where(s => skuIds.Contains(s.SkuId)).ToListAsync();
        var prices = await db.PriceRows().Where(p => skuIds.Contains(p.SkuId) && p.EffectiveFrom <= now).ToListAsync();
        var availability = await availabilityReader.GetAsync(skuIds);

        var productIds = skus.Select(s => s.ProductId).Distinct().ToList();
        var images = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .SelectMany(p => p.Images.Where(i => i.SkuId == null && i.IsPrimary), (p, i) => new { p.Id, i.AltText })
            .ToListAsync();

        var items = new List<WishlistItemViewModel>();
        foreach (var item in wished)
        {
            var sku = skus.FirstOrDefault(s => s.SkuId == item.SkuId);
            if (sku is null) continue;

            // Row 18: a product that is a draft (never published, or unpublished) or unlisted is not shown --
            // no name, slug or price. An ARCHIVED product's item stays, read back with onSale false.
            if (sku.ProductStatus == ProductStatus.Draft || !sku.ListedOnStorefront) continue;

            var price = SkuPricing.PriceAt(prices.Where(p => p.SkuId == item.SkuId), p => p.EffectiveFrom, now);
            var image = images.Where(i => i.Id == sku.ProductId).Select(i => new ProductImageRef(null, i.AltText, true, null)).FirstOrDefault();

            items.Add(new WishlistItemViewModel(
                item.SkuId, sku.ProductId, sku.ProductSlug, sku.ProductName, sku.Label,
                price is null ? null : new Money(price.Amount, price.CurrencyCode).ToViewModel(),
                availability.GetValueOrDefault(item.SkuId, AvailabilityStatus.OutOfStock).DisplayName,
                sku.SkuOnSale, item.AddedOn, image));
        }

        return new QueryResponse<WishlistViewModel> { Result = new WishlistViewModel(items) };
    }
}

/// <summary>HO-SHP-001 section 12.4 -- the caller's own alerts, newest first. Column projections only.</summary>
public sealed class GetMyStockAlertsQueryHandler(CatEcommerceDbContext db)
    : IQueryHandlerAsync<GetMyStockAlertsQuery, IReadOnlyList<StockAlertViewModel>>
{
    public async Task<QueryResponse<IReadOnlyList<StockAlertViewModel>>> HandleAsync(GetMyStockAlertsQuery query)
    {
        var alerts = await db.StockAlerts.AsNoTracking()
            .Where(a => a.CustomerId == query.CustomerId)
            .OrderByDescending(a => a.RequestedOn).ThenBy(a => a.Id)
            .Take(200)
            .Select(a => new { a.Id, a.SkuId, a.Status, a.RequestedOn, a.NotifiedOn })
            .ToListAsync();

        var skuIds = alerts.Select(a => a.SkuId).Distinct().ToList();
        var skus = skuIds.Count == 0
            ? []
            : await db.SkuRows().Where(s => skuIds.Contains(s.SkuId)).ToListAsync();

        IReadOnlyList<StockAlertViewModel> result = alerts.Select(a =>
        {
            var sku = skus.FirstOrDefault(s => s.SkuId == a.SkuId);
            return new StockAlertViewModel(a.Id, a.SkuId, sku?.ProductSlug, sku?.ProductName, sku?.Label, a.Status.DisplayName, a.RequestedOn, a.NotifiedOn);
        }).ToList();

        return new QueryResponse<IReadOnlyList<StockAlertViewModel>> { Result = result };
    }
}

/// <summary>
/// HO-SHP-001 section 8.11 / F33 / OQ-2 -- resolves the customer through HO-NTF-001's
/// <c>INotificationRecipientDirectory</c> and runs HO-NTF-001's OWN pure
/// <c>NotificationRoutingPolicy.Plan</c> against the <c>shop.back-in-stock</c> definition, so the
/// answer is the real routing decision, not a guess.
/// </summary>
public sealed class GetStockAlertDeliverabilityQueryHandler(INotificationRecipientDirectory recipientDirectory, INotificationTemplateCatalogue catalogue)
    : IQueryHandlerAsync<GetStockAlertDeliverabilityQuery, StockAlertDeliverabilityViewModel?>
{
    public async Task<QueryResponse<StockAlertDeliverabilityViewModel?>> HandleAsync(GetStockAlertDeliverabilityQuery query)
    {
        var definition = catalogue.Find(NotificationTemplateCatalogue.ShopBackInStock);
        var recipient = await recipientDirectory.ResolveAsync(NotificationRecipientKind.Customer, query.CustomerId);
        if (definition is null || recipient is null) return new QueryResponse<StockAlertDeliverabilityViewModel?> { Result = null };

        var plan = NotificationRoutingPolicy.Plan(definition, recipient, channelHint: null);

        var channels = new List<string>();
        if (plan.PrimaryChannel is { } primary) channels.Add(primary.DisplayName);
        if (plan.InboxCopy) channels.Add(NotificationChannel.InApp.DisplayName);

        var result = new StockAlertDeliverabilityViewModel(channels.Count > 0, channels, plan.InboxCopy);
        return new QueryResponse<StockAlertDeliverabilityViewModel?> { Result = result };
    }
}

/// <summary>
/// HO-SHP-001 section 12.2 -- the ShopWorker's input. Fail-closed like every availability read: with
/// <c>Shop:InterimAvailabilityFromPrice</c> off nothing is available, so no alert is due. With it on,
/// the SQL prefilter is the interim rule (Active SKU, Active listed product, a price effective now)
/// and <see cref="ISkuAvailabilityReader"/> then settles bundles (every component too). The notify
/// command re-checks, so a race only means the next pass retries.
/// </summary>
public sealed class GetDueStockAlertsQueryHandler(CatEcommerceDbContext db, TimeProvider timeProvider, ISkuAvailabilityReader availabilityReader, IOptions<ShopOptions> options)
    : IQueryHandlerAsync<GetDueStockAlertsQuery, IReadOnlyList<Guid>>
{
    /// <summary>Row 33: a bounded number of keyset pages per pass (Limit rows each).</summary>
    private const int MaxCandidatePages = 10;

    public async Task<QueryResponse<IReadOnlyList<Guid>>> HandleAsync(GetDueStockAlertsQuery query)
    {
        if (!options.Value.InterimAvailabilityFromPrice)
            return new QueryResponse<IReadOnlyList<Guid>> { Result = [] };

        var now = timeProvider.GetUtcNow();

        // Row 33: the SQL prefilter cannot tell a bundle that is never available (a component is out of stock or
        // needs a prescription) from one that is, so a plain Take(Limit) over it could be filled with such
        // alerts and starve every genuinely due one behind them. Candidates are therefore read in keyset
        // pages (oldest first) and settled by the availability reader until Limit due alerts are found, the
        // candidates run out, or the page budget is spent.
        var due = new List<Guid>();
        DateTimeOffset? lastRequestedOn = null;
        Guid lastId = Guid.Empty;

        for (var page = 0; page < MaxCandidatePages && due.Count < query.Limit; page++)
        {
            var candidates = await db.StockAlerts.AsNoTracking()
                .Where(a => a.Status == StockAlertStatus.Pending
                    && db.Products.Any(p => p.Status == ProductStatus.Active && p.ListedOnStorefront
                        && p.Skus.Any(s => s.Id == a.SkuId && s.Status == SkuStatus.Active))
                    && db.SkuPrices.Any(sp => sp.SkuId == a.SkuId && sp.EffectiveFrom <= now))
                .Where(a => lastRequestedOn == null || a.RequestedOn > lastRequestedOn
                    || (a.RequestedOn == lastRequestedOn && a.Id.CompareTo(lastId) > 0))
                .OrderBy(a => a.RequestedOn).ThenBy(a => a.Id)
                .Select(a => new { a.Id, a.SkuId, a.RequestedOn })
                .Take(query.Limit)
                .ToListAsync();

            if (candidates.Count == 0) break;

            var availability = await availabilityReader.GetAsync(candidates.Select(c => c.SkuId).Distinct().ToList());
            due.AddRange(candidates
                .Where(c => availability.GetValueOrDefault(c.SkuId) == AvailabilityStatus.InStock)
                .Select(c => c.Id)
                .Take(query.Limit - due.Count));

            if (candidates.Count < query.Limit) break;

            lastRequestedOn = candidates[^1].RequestedOn;
            lastId = candidates[^1].Id;
        }

        return new QueryResponse<IReadOnlyList<Guid>> { Result = due };
    }
}
