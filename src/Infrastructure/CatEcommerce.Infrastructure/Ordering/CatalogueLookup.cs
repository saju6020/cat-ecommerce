using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Ordering;

/// <summary>
/// HO-ORD-001 §8.14 -- <see cref="ICatalogueLookup"/> over HO-SHP-001's published projections (<c>SkuRows</c>, <c>PriceRows</c>,
/// <c>BundleComponentRows</c>): the ONE file in Ordering that knows Shop's schema. No aggregate is materialised; a fixed handful of small
/// queries however many SKUs are asked about. A SKU is <b>purchasable</b> by the storefront's own rule -- an Active, listed product with an
/// Active SKU and a price in force at <c>asOf</c>; a bundle also needs every component Active and priced and no prescription component
/// (BR-2, OQ-14). Stock is not part of "purchasable": availability is Inventory's (<c>inventory.v_sku_sellable</c>), asked separately.
/// </summary>
public sealed class CatalogueLookup(CatEcommerceDbContext db) : ICatalogueLookup
{
    public async Task<IReadOnlyDictionary<Guid, OrderCatalogueSku>> GetSkusAsync(IReadOnlyCollection<Guid> skuIds, DateTimeOffset asOf, CancellationToken ct)
    {
        var ids = skuIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, OrderCatalogueSku>();

        var rows = await db.SkuRows().Where(r => ids.Contains(r.SkuId)).ToListAsync(ct);
        return await SnapshotAsync(rows, asOf, ct);
    }

    public async Task<OrderCatalogueSku?> GetByBarcodeAsync(string barcode, DateTimeOffset asOf, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;

        var code = barcode.Trim();
        var rows = await db.SkuRows().Where(r => r.Barcode == code).Take(1).ToListAsync(ct);
        return (await SnapshotAsync(rows, asOf, ct)).Values.FirstOrDefault();
    }

    public async Task<IReadOnlyList<OrderCatalogueSku>> SearchSellableAsync(string text, int take, DateTimeOffset asOf, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text) || take <= 0) return [];

        // Escape LIKE wildcards so a search for "100%" is a literal search.
        var term = text.Trim().ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var rows = await db.SkuRows()
            .Where(r => r.SkuStatus == SkuStatus.Active
                        && (EF.Functions.Like(r.SkuCode.ToLower(), $"%{term}%") || EF.Functions.Like(r.ProductName.ToLower(), $"%{term}%")
                            || (r.Barcode != null && EF.Functions.Like(r.Barcode, $"%{term}%"))))
            .OrderBy(r => r.ProductName).ThenBy(r => r.SkuCode)
            .Take(take * 3) // some rows are filtered out below as not purchasable
            .ToListAsync(ct);

        var snapshots = await SnapshotAsync(rows, asOf, ct);
        return rows.Select(r => snapshots[r.SkuId]).Where(s => s.IsPurchasable).Take(take).ToList();
    }

    public async Task<IReadOnlySet<Guid>> GetExistingProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct)
    {
        var ids = productIds.Distinct().ToList();
        if (ids.Count == 0) return new HashSet<Guid>();

        return (await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
    }

    private async Task<Dictionary<Guid, OrderCatalogueSku>> SnapshotAsync(List<SkuRow> rows, DateTimeOffset asOf, CancellationToken ct)
    {
        if (rows.Count == 0) return new Dictionary<Guid, OrderCatalogueSku>();

        var bundleProductIds = rows.Where(r => r.Kind == ProductKind.Bundle).Select(r => r.ProductId).Distinct().ToList();
        var components = bundleProductIds.Count == 0
            ? []
            : await db.BundleComponentRows().Where(c => bundleProductIds.Contains(c.ProductId)).ToListAsync(ct);

        var known = rows.Select(r => r.SkuId).ToHashSet();
        var componentSkuIds = components.Select(c => c.ComponentSkuId).Where(id => !known.Contains(id)).Distinct().ToList();
        var componentRows = componentSkuIds.Count == 0 ? [] : await db.SkuRows().Where(r => componentSkuIds.Contains(r.SkuId)).ToListAsync(ct);

        var all = rows.Concat(componentRows).GroupBy(r => r.SkuId).ToDictionary(g => g.Key, g => g.First());
        var allIds = all.Keys.ToList();

        var priceRows = await db.PriceRows().Where(p => allIds.Contains(p.SkuId) && p.EffectiveFrom <= asOf).ToListAsync(ct);
        var current = priceRows.GroupBy(p => p.SkuId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.EffectiveFrom).First());

        bool OnSale(Guid skuId) => all.TryGetValue(skuId, out var row) && row.SkuOnSale && current.ContainsKey(skuId);

        var result = new Dictionary<Guid, OrderCatalogueSku>();
        foreach (var row in rows)
        {
            var isBundle = row.Kind == ProductKind.Bundle;
            var parts = isBundle
                ? components.Where(c => c.ProductId == row.ProductId).Select(c => new CatalogueBundlePart(c.ComponentSkuId, c.Quantity)).ToList()
                : [];

            var purchasable = OnSale(row.SkuId);
            if (isBundle)
            {
                purchasable = purchasable && parts.Count > 0 && parts.All(p => OnSale(p.ComponentSkuId))
                              && !parts.Any(p => all.TryGetValue(p.ComponentSkuId, out var part) && part.PrescriptionRequired);
            }

            current.TryGetValue(row.SkuId, out var price);
            result[row.SkuId] = new OrderCatalogueSku(
                row.SkuId, row.ProductId, row.ProductName, row.Label, row.Barcode, row.Category.Value, ReturnClassOf(row.Category),
                price?.Amount, price?.CurrencyCode, row.TaxClassId, purchasable, row.PrescriptionRequired,
                row.ShippingWeightGrams, // Shop's published SkuRow (CONTRACTS #25); null = not set, counts as 0 g
                isBundle, parts);
        }

        return result;
    }

    private static ProductReturnClass ReturnClassOf(ProductCategory category) =>
        category == ProductCategory.CatFood ? ProductReturnClass.Food
        : category == ProductCategory.Litter ? ProductReturnClass.Litter
        : ProductReturnClass.Other;
}
