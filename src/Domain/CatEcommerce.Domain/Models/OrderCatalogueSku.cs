using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>One component of a bundle SKU: the component SKU and how many of it make one bundle.</summary>
public sealed record CatalogueBundlePart(Guid ComponentSkuId, int Quantity);

/// <summary>
/// HO-ORD-001 §8.14 -- a SKU as Ordering needs it at an instant: identity, display text, category class, the price in force, tax class,
/// whether it can be bought at all, whether it needs a prescription and, for a bundle, its components. A snapshot over Shop's published
/// projections (<c>ICatalogueLookup</c>) -- Ordering never loads a product aggregate. <c>CurrentPrice</c> is <c>null</c> when no price is in
/// force (then <c>IsPurchasable</c> is false). <c>ShippingWeightGrams</c> is <c>null</c> until Shop publishes it (HO-SHP-001-FU3, owner decision 7 of 2026-10-09); pricing already sums it
/// per line (quantity x grams) against the zone's weight bands, and a SKU without one counts as 0 grams.
/// </summary>
public sealed record OrderCatalogueSku(
    Guid SkuId,
    Guid ProductId,
    string ProductName,
    string VariantLabel,
    string? Barcode,
    int CategoryId,
    ProductReturnClass ReturnClass,
    decimal? CurrentPrice,
    string? CurrencyCode,
    Guid TaxClassId,
    bool IsPurchasable,
    bool RequiresPrescription,
    int? ShippingWeightGrams,
    bool IsBundle,
    IReadOnlyList<CatalogueBundlePart> BundleComponents);
