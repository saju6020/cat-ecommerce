using CatEcommerce.Domain.Entities;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Product;

/// <summary>
/// One SKU with the columns of its product that the read side needs -- a projection, never an
/// aggregate (.ai/read-side-pattern.md section 4 rules 1-2, ADR-012). A class with settable members
/// (not a positional record) so EF can compose <c>Where</c> over it after the projection.
/// </summary>
public sealed class SkuRow
{
    public Guid SkuId { get; set; }
    public string SkuCode { get; set; } = default!;
    public string? Barcode { get; set; }
    public SkuStatus SkuStatus { get; set; } = default!;
    public string? SizeOrWeight { get; set; }
    public string? Flavour { get; set; }
    public string? Colour { get; set; }
    public int? PackQuantity { get; set; }
    public int? ShippingWeightGrams { get; set; }

    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public string Brand { get; set; } = default!;
    public string ProductSlug { get; set; } = default!;
    public ProductKind Kind { get; set; } = default!;
    public ProductStatus ProductStatus { get; set; } = default!;
    public bool ListedOnStorefront { get; set; }
    public bool PrescriptionRequired { get; set; }
    public Guid TaxClassId { get; set; }
    public ProductCategory Category { get; set; } = default!;
    public bool IsExpiryTracked { get; set; }
    public string? CountryOfOriginCode { get; set; }

    public string Label => Sku.LabelFor(ProductName, SizeOrWeight, Flavour, Colour, PackQuantity);
    public bool HasAnyAxis => !string.IsNullOrWhiteSpace(SizeOrWeight) || !string.IsNullOrWhiteSpace(Flavour) || !string.IsNullOrWhiteSpace(Colour) || PackQuantity is not null;
    public bool ProductOnSale => ProductStatus == ProductStatus.Active && ListedOnStorefront;
    public bool SkuOnSale => ProductOnSale && SkuStatus == SkuStatus.Active;
    public bool IsFood => Category == ProductCategory.CatFood;
}

/// <summary>One bundle component (a projected owned row).</summary>
public sealed class BundleComponentRow
{
    public Guid ProductId { get; set; }
    public Guid ComponentSkuId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>A price row projected from <c>product.sku_price</c>.</summary>
public sealed class PriceRow
{
    public Guid Id { get; set; }
    public Guid SkuId { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = default!;
    public DateTimeOffset EffectiveFrom { get; set; }
    public Guid SetBy { get; set; }
}

public static class ProductProjections
{
    /// <summary>Every SKU joined to its product's columns -- the owned <c>sku</c> table is reached
    /// with <c>SelectMany</c>, so one SQL join, no aggregate graph.</summary>
    public static IQueryable<SkuRow> SkuRows(this CatEcommerceDbContext db) =>
        db.Products.AsNoTracking().SelectMany(p => p.Skus, (p, s) => new SkuRow
        {
            SkuId = s.Id, SkuCode = s.Code, Barcode = s.Barcode, SkuStatus = s.Status,
            SizeOrWeight = s.SizeOrWeight, Flavour = s.Flavour, Colour = s.Colour, PackQuantity = s.PackQuantity, ShippingWeightGrams = s.ShippingWeightGrams,
            ProductId = p.Id, ProductName = p.Name, Brand = p.Brand, ProductSlug = p.Slug, Kind = p.Kind, ProductStatus = p.Status,
            ListedOnStorefront = p.ListedOnStorefront, PrescriptionRequired = p.PrescriptionRequired, TaxClassId = p.TaxClassId,
            Category = p.Category, IsExpiryTracked = p.IsExpiryTracked, CountryOfOriginCode = p.CountryOfOriginCode
        });

    public static IQueryable<BundleComponentRow> BundleComponentRows(this CatEcommerceDbContext db) =>
        db.Products.AsNoTracking().SelectMany(p => p.BundleComponents, (p, c) => new BundleComponentRow
        { ProductId = p.Id, ComponentSkuId = c.ComponentSkuId, Quantity = c.Quantity });

    /// <summary>
    /// OQ-14 / BR-2 defence in depth (HO-SHP-001 section 1 Amended 2026-10-04): of the given bundle
    /// products, those with ANY component whose product currently requires a prescription. A bundle
    /// like that is never eligible, purchasable or in stock, whatever its own flag says (covers legacy
    /// data and a race past the aggregate rule). A join of two projections, no aggregate.
    /// </summary>
    public static async Task<HashSet<Guid>> BundlesWithPrescriptionComponentAsync(
        this CatEcommerceDbContext db, IReadOnlyCollection<Guid> bundleProductIds, CancellationToken ct = default)
    {
        var ids = bundleProductIds.ToList();
        if (ids.Count == 0) return [];

        var found = await db.BundleComponentRows().Where(c => ids.Contains(c.ProductId))
            .Join(db.SkuRows().Where(s => s.PrescriptionRequired), c => c.ComponentSkuId, s => s.SkuId, (c, s) => c.ProductId)
            .Distinct().ToListAsync(ct);
        return found.ToHashSet();
    }

    public static IQueryable<PriceRow> PriceRows(this CatEcommerceDbContext db) =>
        db.SkuPrices.AsNoTracking().Select(p => new PriceRow
        { Id = p.Id, SkuId = p.SkuId, Amount = p.Amount.Amount, CurrencyCode = p.Amount.CurrencyCode, EffectiveFrom = p.EffectiveFrom, SetBy = p.SetBy });
}
