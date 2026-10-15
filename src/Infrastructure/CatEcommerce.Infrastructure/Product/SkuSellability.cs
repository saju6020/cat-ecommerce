using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Product;

/// <summary>What <c>inventory.v_sku_sellable</c> says about one SKU at the storefront branch:
/// the sellable quantity and the FEFO-first expiry of the stock that quantity is made of.</summary>
public sealed record SkuStockFigure(decimal SellableQuantity, DateOnly? NextExpiresOn);

/// <summary>
/// HO-SHP-001 section 8.7 / ADR-082 / ADR-116 -- the ONE place the read side answers "is this SKU
/// sellable right now". Shop never computes stock: the figure is read from HO-IMP-001's published
/// summary view <c>inventory.v_sku_sellable</c> (<see cref="Persistence.Views.SkuSellableEntry"/>, ADR-116 amendment 2026-10-07: the trigger-kept
/// per-SKU summary, equal to <c>v_sellable_stock</c> at every instant) and nothing else of Inventory's. A SKU with no row in the view is out of stock (and, for staff, "never reported").
/// On top of the figure sit the rules that are Shop's own: the SKU must be Active on an Active, listed
/// product with a price effective now, and a bundle is available only when its own SKU is on sale and every
/// component has enough sellable quantity for one bundle (<see cref="SkuAvailabilityRules.EvaluateBundle"/>,
/// US-SHP-12 AC2). A bundle's own SKU holds no stock (HO-IMP-001 ADR-111), so its figure is never read.
///
/// <c>branchId</c> is the storefront branch (<c>Shop:StorefrontBranchId</c>, default the seeded main branch).
///
/// Reads are column projections (<see cref="SkuRow"/>, <see cref="BundleComponentRow"/>, the view's columns):
/// a fixed handful of small queries however many SKUs are asked about; no aggregate is materialised.
/// </summary>
public static class SkuSellability
{
    /// <summary>The view's figure per SKU at the storefront branch (summed over expiry buckets). SKUs with no row are absent.</summary>
    public static async Task<Dictionary<Guid, SkuStockFigure>> StockAsync(
        CatEcommerceDbContext db, IReadOnlyCollection<Guid> skuIds, Guid branchId, CancellationToken ct = default)
    {
        var ids = skuIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var rows = await db.SkuSellable.AsNoTracking()
            .Where(v => ids.Contains(v.SkuId) && v.BranchId == branchId)
            .Select(v => new { v.SkuId, v.SellableQuantity, NextExpiresOn = v.ExpiresOn })
            .ToListAsync(ct);

        return rows.GroupBy(r => r.SkuId).ToDictionary(
            g => g.Key,
            g => new SkuStockFigure(
                g.Sum(r => r.SellableQuantity),
                g.Where(r => r.SellableQuantity > 0 && r.NextExpiresOn != null).Select(r => r.NextExpiresOn).DefaultIfEmpty().Min()));
    }

    public static async Task<Dictionary<Guid, AvailabilityStatus>> AvailabilityAsync(
        CatEcommerceDbContext db, IReadOnlyCollection<Guid> skuIds, DateTimeOffset now, Guid branchId, CancellationToken ct = default)
    {
        var requestedIds = skuIds.Distinct().ToList();
        var result = requestedIds.ToDictionary(id => id, _ => AvailabilityStatus.OutOfStock);
        if (requestedIds.Count == 0) return result;

        var requested = await db.SkuRows().Where(r => requestedIds.Contains(r.SkuId)).ToListAsync(ct);

        var bundleProductIds = requested.Where(r => r.Kind == ProductKind.Bundle).Select(r => r.ProductId).Distinct().ToList();
        var components = bundleProductIds.Count == 0
            ? []
            : await db.BundleComponentRows().Where(c => bundleProductIds.Contains(c.ProductId)).ToListAsync(ct);

        var knownIds = requested.Select(r => r.SkuId).ToHashSet();
        var componentIds = components.Select(c => c.ComponentSkuId).Where(id => !knownIds.Contains(id)).Distinct().ToList();
        var componentRows = componentIds.Count == 0
            ? []
            : await db.SkuRows().Where(r => componentIds.Contains(r.SkuId)).ToListAsync(ct);

        var all = requested.Concat(componentRows).ToDictionary(r => r.SkuId);
        var allIds = all.Keys.ToList();

        var pricedSkuIds = (await db.PriceRows()
            .Where(p => allIds.Contains(p.SkuId) && p.EffectiveFrom <= now)
            .Select(p => p.SkuId).Distinct().ToListAsync(ct)).ToHashSet();

        // A bundle's own SKU holds no stock, so only standard SKUs and bundle components are looked up in the view.
        var stockedIds = allIds.Where(id => all[id].Kind != ProductKind.Bundle).ToList();
        var stock = await StockAsync(db, stockedIds, branchId, ct);

        bool OnSale(Guid skuId) => all.TryGetValue(skuId, out var row) && row.SkuOnSale && pricedSkuIds.Contains(skuId);

        // One rule with the catalogue predicates (StorefrontAvailability): a standard SKU is available when its sellable quantity is > 0 (rounded UP, so a
        // fractional balance still counts); a bundle needs sellable >= the component quantity, which for whole component quantities is the same as the
        // floor of the balance >= it. Both rules read the same figure.
        decimal? Figure(Guid skuId) => OnSale(skuId) && stock.TryGetValue(skuId, out var figure) ? Math.Clamp(figure.SellableQuantity, 0m, int.MaxValue) : null;
        int? AtLeastOneIfPositive(Guid skuId) => Figure(skuId) is { } q ? (int)Math.Ceiling(q) : null;
        int? WholeUnits(Guid skuId) => Figure(skuId) is { } q ? (int)Math.Floor(q) : null;

        foreach (var row in requested)
        {
            if (row.Kind != ProductKind.Bundle)
            {
                result[row.SkuId] = SkuAvailabilityRules.Evaluate(AtLeastOneIfPositive(row.SkuId), skuActive: OnSale(row.SkuId), productSellable: true);
                continue;
            }

            if (!OnSale(row.SkuId)) continue;

            // OQ-14 / BR-2: a bundle with a prescription component is never sellable.
            if (components.Any(c => c.ProductId == row.ProductId && all.TryGetValue(c.ComponentSkuId, out var part) && part.PrescriptionRequired)) continue;

            var parts = components.Where(c => c.ProductId == row.ProductId)
                .Select(c => (c.Quantity, WholeUnits(c.ComponentSkuId))).ToList();
            result[row.SkuId] = SkuAvailabilityRules.EvaluateBundle(parts);
        }

        return result;
    }
}
