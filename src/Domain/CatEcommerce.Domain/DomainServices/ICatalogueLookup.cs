using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-ORD-001 §8.14 -- the read port through which Ordering sees HO-SHP-001's catalogue. HO-SHP-001 published its projections
/// (<c>SkuRows</c>, <c>PriceRows</c>, <c>BundleComponentRows</c>) rather than a lookup port, so this port is Ordering's own, implemented in
/// Infrastructure over those projections -- the precedent is HO-IMP-001's <c>ISkuDirectory</c> / <c>CatalogueSkuDirectory</c> (HO-ORD-001 §1
/// Amended). Read-only, never an aggregate; "purchasable" is the one rule the storefront already uses (an Active listed product, an Active SKU,
/// a price in force, and for a bundle every component Active and priced with no prescription component).
/// </summary>
public interface ICatalogueLookup
{
    /// <summary>The SKUs that exist among <paramref name="skuIds"/>, priced as of <paramref name="asOf"/>. An unknown id is absent.</summary>
    Task<IReadOnlyDictionary<Guid, OrderCatalogueSku>> GetSkusAsync(IReadOnlyCollection<Guid> skuIds, DateTimeOffset asOf, CancellationToken ct);

    Task<OrderCatalogueSku?> GetByBarcodeAsync(string barcode, DateTimeOffset asOf, CancellationToken ct);

    /// <summary>At most <paramref name="take"/> purchasable SKUs whose code, name or barcode contains <paramref name="text"/>.</summary>
    Task<IReadOnlyList<OrderCatalogueSku>> SearchSellableAsync(string text, int take, DateTimeOffset asOf, CancellationToken ct);

    /// <summary>The subset of <paramref name="productIds"/> that exist (a discount code may only name real products).</summary>
    Task<IReadOnlySet<Guid>> GetExistingProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct);
}
