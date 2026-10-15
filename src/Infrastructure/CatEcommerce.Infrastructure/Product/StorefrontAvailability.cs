using System.Linq.Expressions;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Persistence.Views;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Product;

/// <summary>
/// HO-SHP-001 section 8.7 / 8.8 -- the catalogue-level counterpart of <see cref="SkuSellability"/>, composed in SQL so the
/// availability filter, the page labels and the recommendation pools all pass through one predicate (and so cannot disagree).
/// <c>product.storefront_catalogue_entry.is_on_sale</c> (formerly <c>in_stock</c>) means "on sale": an Active SKU priced now (and,
/// for a bundle, every component Active, listed and priced). This predicate adds Inventory's side from
/// <c>inventory.v_sku_sellable</c>: a standard product needs one such SKU with sellable quantity above zero; a bundle needs
/// every component's sellable quantity to cover its component quantity. Nothing of Inventory's but the view is touched, and
/// no Shop view or column changes (a view migration is the integrator's).
/// </summary>
public static class StorefrontAvailability
{
    private static readonly int KindBundle = ProductKind.Bundle.Value;

    /// <summary>The per-product form (a bundle is short when any component's sellable quantity, summed over the branch(es), cannot cover its component quantity): each product probes the view for its own SKUs. The planner turns it into index probes when
    /// few products are in play (the page labels, the recommendation pools) -- cheap there, but over thousands of products it
    /// degrades to a join of the whole view, so the catalogue's availability filter uses <see cref="WhereSellable"/>.</summary>
    public static Expression<Func<StorefrontCatalogueEntry, bool>> IsSellable(CatEcommerceDbContext db, Guid branchId) =>
        e => e.IsOnSale
            && (e.Kind == KindBundle
                ? !db.Products.Where(p => p.Id == e.ProductId).SelectMany(p => p.BundleComponents)
                    .Any(c => db.SkuSellable
                        .Where(v => v.SkuId == c.ComponentSkuId && v.BranchId == branchId)
                        .Sum(v => v.SellableQuantity) < c.Quantity)
                : db.Products.Where(p => p.Id == e.ProductId).SelectMany(p => p.Skus)
                    .Any(s => s.Status == SkuStatus.Active
                        && db.SkuPrices.Any(sp => sp.SkuId == s.Id && sp.EffectiveFrom <= DateTimeOffset.UtcNow)
                        && db.SkuSellable.Any(v => v.SkuId == s.Id && v.SellableQuantity > 0 && v.BranchId == branchId)));

    /// <summary>The standard-product half of <see cref="IsSellable"/>, stated on its own: an Active SKU priced now with sellable quantity at
    /// the branch. It reads no column of the view's pricing (a standard product being on sale IS having such a SKU), so a query using only
    /// this half does not price the catalogue -- the popular-suggestions pool relies on that.</summary>
    public static Expression<Func<StorefrontCatalogueEntry, bool>> IsSellableStandard(CatEcommerceDbContext db, Guid branchId) =>
        e => e.Kind != KindBundle
            && db.Products.Where(p => p.Id == e.ProductId).SelectMany(p => p.Skus)
                .Any(s => s.Status == SkuStatus.Active
                    && db.SkuPrices.Any(sp => sp.SkuId == s.Id && sp.EffectiveFrom <= DateTimeOffset.UtcNow)
                    && db.SkuSellable.Any(v => v.SkuId == s.Id && v.SellableQuantity > 0 && v.BranchId == branchId));

    /// <summary>The bundle half of <see cref="IsSellable"/>, stated on its own: on sale per the view, and no component whose sellable
    /// quantity (summed over the branch) cannot cover its component quantity.</summary>
    public static Expression<Func<StorefrontCatalogueEntry, bool>> IsSellableBundle(CatEcommerceDbContext db, Guid branchId) =>
        e => e.Kind == KindBundle && e.IsOnSale
            && !db.Products.Where(p => p.Id == e.ProductId).SelectMany(p => p.BundleComponents)
                .Any(c => db.SkuSellable
                    .Where(v => v.SkuId == c.ComponentSkuId && v.BranchId == branchId)
                    .Sum(v => v.SellableQuantity) < c.Quantity);

    /// <summary>The catalogue's availability filter (NFR-PRF-2), composed INTO the query as a SQL semi-join instead of a catalogue-wide
    /// list of GUIDs passed back in: a standard product is sellable when it has an Active SKU priced now whose sellable quantity at the
    /// branch is above zero (Inventory's view is probed by SKU id, only for the products the other filters left). Bundles (whose own SKU
    /// holds no stock) are settled by <see cref="SellableBundleProductIdsAsync"/>, which also checks every component, over only the bundles
    /// that survived the other filters -- pass its result as <paramref name="sellableBundleProductIds"/>.</summary>
    public static IQueryable<StorefrontCatalogueEntry> WhereSellable(
        CatEcommerceDbContext db, IQueryable<StorefrontCatalogueEntry> source, DateTimeOffset now, Guid branchId, IReadOnlyCollection<Guid> sellableBundleProductIds)
    {
        var sellableSkuIds = db.SkuSellable
            .Where(v => v.SellableQuantity > 0 && v.BranchId == branchId)
            .Select(v => v.SkuId);

        var bundleIds = sellableBundleProductIds.ToList();

        return source.Where(e =>
            (e.Kind != KindBundle
                && db.Products.Where(p => p.Id == e.ProductId).SelectMany(p => p.Skus)
                    .Any(s => s.Status == SkuStatus.Active
                        && db.SkuPrices.Any(sp => sp.SkuId == s.Id && sp.EffectiveFrom <= now)
                        && sellableSkuIds.Contains(s.Id)))
            || (e.Kind == KindBundle && bundleIds.Contains(e.ProductId)));
    }

    /// <summary>Which of the given bundle products are sellable right now (own SKU on sale, no prescription component, every component
    /// covered), settled through <see cref="SkuSellability.AvailabilityAsync"/>. <paramref name="candidateBundleProductIds"/> is a
    /// subquery of the bundles that passed the catalogue's other filters, so the per-SKU check never covers more bundles than the request needs.</summary>
    public static async Task<List<Guid>> SellableBundleProductIdsAsync(
        CatEcommerceDbContext db, IQueryable<Guid> candidateBundleProductIds, DateTimeOffset now, Guid branchId, CancellationToken ct = default)
    {
        var bundleSkus = await db.SkuRows()
            .Where(r => r.Kind == ProductKind.Bundle && r.ProductStatus == ProductStatus.Active && r.ListedOnStorefront && r.SkuStatus == SkuStatus.Active
                && candidateBundleProductIds.Contains(r.ProductId))
            .Select(r => new { r.SkuId, r.ProductId })
            .ToListAsync(ct);

        if (bundleSkus.Count == 0) return [];

        var availability = await SkuSellability.AvailabilityAsync(db, bundleSkus.Select(b => b.SkuId).ToList(), now, branchId, ct);
        return bundleSkus.Where(b => availability.GetValueOrDefault(b.SkuId) == AvailabilityStatus.InStock).Select(b => b.ProductId).Distinct().ToList();
    }

    /// <summary>Which of the given products are sellable right now -- for a page of products (labels, recommendation blocks), answered
    /// through the same per-SKU seam as the detail page and purchasability (<see cref="SkuSellability.AvailabilityAsync"/>): a fixed
    /// handful of small queries probing the view by SKU id, never a join of the whole view. A product is sellable when any of its
    /// Active SKUs is (a bundle has just the one, its own).</summary>
    public static async Task<HashSet<Guid>> SellableProductIdsAsync(
        CatEcommerceDbContext db, IReadOnlyCollection<Guid> productIds, DateTimeOffset now, Guid branchId, CancellationToken ct = default)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var skus = await db.SkuRows()
            .Where(r => ids.Contains(r.ProductId) && r.SkuStatus == SkuStatus.Active)
            .Select(r => new { r.SkuId, r.ProductId })
            .ToListAsync(ct);

        var availability = await SkuSellability.AvailabilityAsync(db, skus.Select(s => s.SkuId).ToList(), now, branchId, ct);
        return skus.Where(s => availability.GetValueOrDefault(s.SkuId) == AvailabilityStatus.InStock).Select(s => s.ProductId).ToHashSet();
    }
}
