using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 §8.14 -- how many of a SKU can be sold at a branch right now, from Inventory's published view <c>inventory.v_sku_sellable</c> (ADR-116:
/// excludes expired, quarantined, damaged and reserved stock). A standard SKU reads its own figure; a bundle's own SKU holds no stock (HO-IMP-001
/// ADR-111), so a bundle is as many as its scarcest component allows (<c>floor(sellable / quantity)</c>). A SKU with no row sells nothing.
/// </summary>
public static class SellableQuantities
{
    public static async Task<Dictionary<Guid, decimal>> ForAsync(CatEcommerceDbContext db, IReadOnlyCollection<OrderCatalogueSku> skus, Guid branchId, CancellationToken ct)
    {
        if (skus.Count == 0) return [];

        var stockIds = skus.Where(s => !s.IsBundle).Select(s => s.SkuId)
            .Concat(skus.Where(s => s.IsBundle).SelectMany(s => s.BundleComponents.Select(c => c.ComponentSkuId)))
            .Distinct().ToList();

        var stock = await SkuSellability.StockAsync(db, stockIds, branchId, ct);
        decimal Of(Guid skuId) => stock.TryGetValue(skuId, out var figure) ? Math.Max(0m, figure.SellableQuantity) : 0m;

        return skus.ToDictionary(
            s => s.SkuId,
            s => !s.IsBundle ? Of(s.SkuId)
                : s.BundleComponents.Count == 0 ? 0m
                : s.BundleComponents.Min(c => Math.Floor(Of(c.ComponentSkuId) / Math.Max(1, c.Quantity))));
    }
}
