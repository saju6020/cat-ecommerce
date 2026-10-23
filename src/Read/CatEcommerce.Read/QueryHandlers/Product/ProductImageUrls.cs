using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>
/// SHP backlog row 2 (2026-10-16): storefront image refs carried <c>url: null</c> because HO-FIL-001 signed URLs were
/// never wired, so no product photo ever showed. Images are signed here, in one batch per response
/// (<see cref="IStoredFileSigningBatcher"/>: one stored-file query, one signature per file), as <c>inline</c> files.
/// A file that no longer exists simply keeps <c>url: null</c> and the screen shows its placeholder.
/// ADR-370 (owner, 2026-10-18): every list, card, cart and order line asks for the 480 px card copy; the product page
/// asks for the 1,200 px copy. Where product photos are served straight from the bucket the URL points at that copy.
/// </summary>
internal static class ProductImageUrls
{
    internal const string Disposition = "inline";

    /// <summary>Fills the signed URL of each item's primary (product-level) image.</summary>
    internal static async Task<IReadOnlyList<CatalogueItem>> WithPrimaryImageUrlsAsync(
        CatEcommerceDbContext db, IStoredFileSigningBatcher signer, IReadOnlyList<CatalogueItem> items)
    {
        var productIds = items.Where(i => i.PrimaryImage is not null).Select(i => i.ProductId).Distinct().ToList();
        if (productIds.Count == 0) return items;

        var files = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .SelectMany(p => p.Images.Where(i => i.SkuId == null && i.IsPrimary), (p, i) => new { ProductId = p.Id, i.FileId })
            .ToListAsync();
        var urls = await SignAsync(signer, files.Select(f => f.FileId));
        var urlByProduct = files
            .Where(f => urls.ContainsKey(f.FileId))
            .GroupBy(f => f.ProductId)
            .ToDictionary(g => g.Key, g => urls[g.First().FileId]);

        return items
            .Select(i => i.PrimaryImage is not null && urlByProduct.TryGetValue(i.ProductId, out var url)
                ? i with { PrimaryImage = i.PrimaryImage with { Url = url } }
                : i)
            .ToList();
    }

    /// <summary>
    /// SKU → signed photo URL for cart and order lines (2026-10-16): the variant's own primary image, else the product's
    /// primary image, else its first image by sort order. A SKU with no photo is absent and the screen shows its pack tile.
    /// </summary>
    internal static async Task<IReadOnlyDictionary<Guid, string>> ForSkusAsync(
        CatEcommerceDbContext db, IStoredFileSigningBatcher signer, IReadOnlyCollection<Guid> skuIds)
    {
        if (skuIds.Count == 0) return new Dictionary<Guid, string>();

        var products = await db.Products.AsNoTracking()
            .Where(p => p.Skus.Any(s => skuIds.Contains(s.Id)))
            .Select(p => new
            {
                Skus = p.Skus.Where(s => skuIds.Contains(s.Id)).Select(s => s.Id).ToList(),
                Images = p.Images.Select(i => new { i.SkuId, i.FileId, i.IsPrimary, i.SortOrder }).ToList(),
            })
            .ToListAsync();

        var chosen = new Dictionary<Guid, Guid>();
        foreach (var product in products)
        foreach (var skuId in product.Skus)
        {
            var pick = product.Images.Where(i => i.SkuId == null || i.SkuId == skuId)
                .OrderByDescending(i => i.SkuId == skuId && i.IsPrimary)
                .ThenByDescending(i => i.SkuId == null && i.IsPrimary)
                .ThenBy(i => i.SortOrder)
                .FirstOrDefault();
            if (pick is not null) chosen[skuId] = pick.FileId;
        }

        var urls = await SignAsync(signer, chosen.Values);
        return chosen.Where(c => urls.ContainsKey(c.Value)).ToDictionary(c => c.Key, c => urls[c.Value]);
    }

    /// <summary>fileId → signed URL for the given files (missing files are absent).</summary>
    internal static async Task<IReadOnlyDictionary<Guid, string>> SignAsync(
        IStoredFileSigningBatcher signer, IEnumerable<Guid> fileIds, FileVariant? variant = null)
    {
        var ids = fileIds.Distinct().ToList();
        var signed = await signer.SignManyAsync(ids, Disposition, variant ?? FileVariants.Card);
        return signed.ToDictionary(kv => kv.Key, kv => kv.Value.Url);
    }
}
