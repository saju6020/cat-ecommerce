using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Product;

/// <summary>
/// HO-SHP-001 section 8.7 / section 4.3 / ADR-082 -- the ONE place the read side answers "is this
/// SKU sellable right now". Shop never computes stock. Until HO-IMP-001's
/// <c>inventory.v_sellable_stock</c> exists the answer is FAIL-CLOSED: every SKU is out of stock
/// (<c>interimFromPrice == false</c>, the production default -- no query is even made). Only when
/// <c>Shop:InterimAvailabilityFromPrice</c> is true (the Testing environment) is the interim
/// approximation applied: an Active SKU of an Active, listed product with a price effective now is
/// available, and a bundle is available only when its own SKU and every component is
/// (<see cref="SkuAvailabilityRules.EvaluateBundle"/>, US-SHP-12 AC2). When the inventory view
/// lands, the flag goes and <see cref="ApproximateSellableQuantity"/> is replaced by the view's
/// <c>sellable_quantity</c> here and in <c>product.storefront_catalogue_entry</c>.
///
/// Reads are column projections (<see cref="SkuRow"/>, <see cref="BundleComponentRow"/>): three
/// small queries however many SKUs are asked about; no aggregate is materialised.
/// </summary>
public static class SkuSellability
{
    /// <summary>Stand-in for the unknown sellable quantity: any positive figure means "in stock".</summary>
    public const int ApproximateSellableQuantity = int.MaxValue;

    public static async Task<Dictionary<Guid, AvailabilityStatus>> AvailabilityAsync(
        CatEcommerceDbContext db, IReadOnlyCollection<Guid> skuIds, DateTimeOffset now, bool interimFromPrice, CancellationToken ct = default)
    {
        var requestedIds = skuIds.Distinct().ToList();
        var result = requestedIds.ToDictionary(id => id, _ => AvailabilityStatus.OutOfStock);
        if (requestedIds.Count == 0 || !interimFromPrice) return result;

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

        bool Sellable(Guid skuId) => all.TryGetValue(skuId, out var row) && row.SkuOnSale && pricedSkuIds.Contains(skuId);

        foreach (var row in requested)
        {
            if (row.Kind != ProductKind.Bundle)
            {
                result[row.SkuId] = Sellable(row.SkuId) ? AvailabilityStatus.InStock : AvailabilityStatus.OutOfStock;
                continue;
            }

            if (!Sellable(row.SkuId)) continue;

            // OQ-14 / BR-2: a bundle with a prescription component is never sellable.
            if (components.Any(c => c.ProductId == row.ProductId && all.TryGetValue(c.ComponentSkuId, out var part) && part.PrescriptionRequired)) continue;

            var parts = components.Where(c => c.ProductId == row.ProductId)
                .Select(c => (c.Quantity, (int?)(Sellable(c.ComponentSkuId) ? ApproximateSellableQuantity : 0))).ToList();
            result[row.SkuId] = SkuAvailabilityRules.EvaluateBundle(parts);
        }

        return result;
    }
}
