using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 §8.2 / ADR-111 -- the ONE file in Inventory that knows HO-SHP-001's schema. It reads the catalogue through Shop's own
/// published projection (<c>db.SkuRows()</c>, one SQL join of <c>product.sku</c> to <c>product.product</c>) and maps a row to the
/// <see cref="SkuStockProfile"/> Inventory understands; if the catalogue's read model changes, only this adapter changes.
///
/// The field mapping (the "default if HO-SHP-001 lacks it" column of §8.2, because HO-SHP-001 publishes no item class, unit or
/// fractional flag):
/// <list type="bullet">
/// <item><c>StockClass</c> = <c>Food</c> when the product's category is Cat Food, else <c>General</c> (clinical items are OQ-1).</item>
/// <item><c>ExpiryRequired</c> = the product's own <c>IsExpiryTracked</c> flag (D-2: a food product is always tracked).</item>
/// <item><c>StockUnit</c> = <c>each</c>, <c>AllowsFractionalQuantity</c> = false (OQ-19).</item>
/// <item><c>IsActive</c> = the SKU is <c>Active</c> and the product is a <c>Standard</c> one: a bundle's own SKU holds no stock -- its
/// components are what is received and sold.</item>
/// </list>
/// </summary>
public sealed class CatalogueSkuDirectory(CatEcommerceDbContext db) : ISkuDirectory
{
    private const string DefaultStockUnit = "each";

    public async Task<SkuStockProfile?> GetAsync(Guid skuId, CancellationToken ct)
    {
        var row = await db.SkuRows().Where(s => s.SkuId == skuId).FirstOrDefaultAsync(ct);
        return row is null ? null : ToProfile(row);
    }

    public async Task<IReadOnlyList<SkuStockProfile>> GetManyAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        if (skuIds.Count == 0) return [];
        var ids = skuIds.Distinct().ToList();
        var rows = await db.SkuRows().Where(s => ids.Contains(s.SkuId)).ToListAsync(ct);
        return rows.Select(ToProfile).ToList();
    }

    public async Task<SkuStockProfile?> FindByBarcodeAsync(string barcode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;
        var code = barcode.Trim();
        var row = await db.SkuRows().Where(s => s.Barcode == code).FirstOrDefaultAsync(ct);
        return row is null ? null : ToProfile(row);
    }

    public async Task<IReadOnlyList<SkuStockProfile>> SearchAsync(string text, int take, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text) || take <= 0) return [];

        // Escape LIKE wildcards so a search for "100%" is a literal search.
        var term = text.Trim().ToLowerInvariant().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var rows = await db.SkuRows()
            .Where(s => EF.Functions.Like(s.SkuCode.ToLower(), $"%{term}%")
                        || EF.Functions.Like(s.ProductName.ToLower(), $"%{term}%")
                        || (s.Barcode != null && EF.Functions.Like(s.Barcode, $"%{term}%")))
            .OrderBy(s => s.ProductName).ThenBy(s => s.SkuCode)
            .Take(take)
            .ToListAsync(ct);

        return rows.Select(ToProfile).ToList();
    }

    public Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct) =>
        db.Products.AsNoTracking().AnyAsync(p => p.Id == productId && p.Skus.Any(), ct);

    private static SkuStockProfile ToProfile(SkuRow row) =>
        new(
            SkuId: row.SkuId,
            ProductId: row.ProductId,
            SkuCode: row.SkuCode,
            DisplayName: row.HasAnyAxis ? $"{row.ProductName} - {row.Label}" : row.ProductName,
            Barcode: row.Barcode,
            StockClass: row.IsFood ? StockClass.Food : StockClass.General,
            ExpiryRequired: row.IsExpiryTracked || row.IsFood,
            StockUnit: DefaultStockUnit,
            AllowsFractionalQuantity: false,
            CountryOfOriginCode: row.CountryOfOriginCode,
            IsActive: row.SkuStatus == SkuStatus.Active && row.Kind == ProductKind.Standard);
}
