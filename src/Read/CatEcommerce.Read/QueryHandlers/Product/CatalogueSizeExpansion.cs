using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Infrastructure.Product;
using CatEcommerce.Read.QueryHandlers.StoredFile;
using CatEcommerce.Read.ViewModels;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Read.QueryHandlers.Product;

/// <summary>
/// The shop list shows one card per size (owner, 2026-10-18; design
/// https://claude.ai/artifact/VTT9ehMuJMc49sYqWmh9gq): each product on the page becomes one item per
/// active, priced SKU — its size label, its own current price, its own stock and its own primary photo
/// (else the product's). Filtering, sorting and paging stay per product in the database
/// (<see cref="GetCatalogueQueryHandler"/>); a product's sizes follow each other on its page, cheapest
/// first (dearest first when sorting by price, high to low). A product with one SKU keeps its row and
/// gains the size label. Only the price and in-stock filters also apply per size, so a size outside
/// them is not shown.
/// </summary>
internal static class CatalogueSizeExpansion
{
    internal static async Task<IReadOnlyList<CatalogueItem>> ExpandAsync(
        CatEcommerceDbContext db,
        ISkuAvailabilityReader availabilityReader,
        IStoredFileSigningBatcher signer,
        IReadOnlyList<CatalogueItem> items,
        DateTimeOffset now,
        bool dearestFirst,
        decimal? minPrice,
        decimal? maxPrice,
        bool inStockOnly)
    {
        if (items.Count == 0) return items;
        var productIds = items.Select(i => i.ProductId).Distinct().ToList();

        var skus = await db.SkuRows()
            .Where(s => productIds.Contains(s.ProductId) && s.SkuStatus == SkuStatus.Active)
            .ToListAsync();
        var skuIds = skus.Select(s => s.SkuId).ToList();
        var prices = await db.PriceRows().Where(p => skuIds.Contains(p.SkuId) && p.EffectiveFrom <= now).ToListAsync();
        var stock = await availabilityReader.GetAsync(skuIds);
        var skuImages = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .SelectMany(p => p.Images.Where(i => i.SkuId != null && i.IsPrimary), (p, i) => new { SkuId = i.SkuId!.Value, i.FileId, i.AltText })
            .ToListAsync();
        var imageUrls = await ProductImageUrls.SignAsync(signer, skuImages.Select(i => i.FileId));
        var skusByProduct = skus.ToLookup(s => s.ProductId);
        var pricesBySku = prices.ToLookup(p => p.SkuId);
        var imageBySku = skuImages.GroupBy(i => i.SkuId).ToDictionary(g => g.Key, g => g.First());

        var result = new List<CatalogueItem>(items.Count);
        foreach (var item in items)
        {
            var sizes = skusByProduct[item.ProductId]
                .Select(s => new { Sku = s, Price = SkuPricing.PriceAt(pricesBySku[s.SkuId], p => p.EffectiveFrom, now) })
                .Where(x => x.Price is not null)
                .ToList();

            if (sizes.Count <= 1)
            {
                // One size (or nothing priced): the product's own row, now naming its size.
                var only = sizes.FirstOrDefault();
                result.Add(only is null ? item : item with { SkuId = only.Sku.SkuId, SizeLabel = SizeLabelOf(only.Sku) });
                continue;
            }

            var ordered = dearestFirst
                ? sizes.OrderByDescending(x => x.Price!.Amount).ThenBy(x => x.Sku.SkuCode)
                : sizes.OrderBy(x => x.Price!.Amount).ThenBy(x => x.Sku.SkuCode);
            foreach (var size in ordered)
            {
                var amount = size.Price!.Amount;
                if ((minPrice is not null && amount < minPrice) || (maxPrice is not null && amount > maxPrice)) continue;
                var inStock = stock.GetValueOrDefault(size.Sku.SkuId, AvailabilityStatus.OutOfStock) == AvailabilityStatus.InStock;
                if (inStockOnly && !inStock) continue;

                var primaryImage = imageBySku.TryGetValue(size.Sku.SkuId, out var image) && imageUrls.TryGetValue(image.FileId, out var url)
                    ? new ProductImageRef(url, image.AltText, true, size.Sku.SkuId)
                    : item.PrimaryImage;

                result.Add(item with
                {
                    SkuId = size.Sku.SkuId,
                    SizeLabel = SizeLabelOf(size.Sku),
                    FromPrice = new MoneyViewModel(amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), size.Price.CurrencyCode),
                    PriceVaries = false,
                    Availability = inStock ? "InStock" : "OutOfStock",
                    PrimaryImage = primaryImage,
                });
            }
        }

        return result;
    }

    /// <summary>The words that tell this size apart ("400 g", "Red", "Pack of 6"); null when the SKU has none.</summary>
    internal static string? SizeLabelOf(SkuRow sku) => sku.HasAnyAxis
        ? CatEcommerce.Domain.Entities.Sku.LabelFor(sku.ProductName, sku.SizeOrWeight, sku.Flavour, sku.Colour, sku.PackQuantity)
        : null;
}
