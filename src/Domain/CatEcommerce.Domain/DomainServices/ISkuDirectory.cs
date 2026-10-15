using CatEcommerce.Domain.Models;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-IMP-001 §8.2 / ADR-111 -- the ONE port through which Inventory reads a catalogue SKU. Its single adapter
/// (<c>CatalogueSkuDirectory</c>, Infrastructure) is the only file that knows HO-SHP-001's schema; if the catalogue's
/// read model changes, that adapter changes and nothing else.
/// </summary>
public interface ISkuDirectory
{
    Task<SkuStockProfile?> GetAsync(Guid skuId, CancellationToken ct);

    Task<IReadOnlyList<SkuStockProfile>> GetManyAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct);

    Task<SkuStockProfile?> FindByBarcodeAsync(string barcode, CancellationToken ct);

    Task<IReadOnlyList<SkuStockProfile>> SearchAsync(string text, int take, CancellationToken ct);

    /// <summary>Whether any SKU of <paramref name="productId"/> exists (C2: a storage profile is keyed by product).</summary>
    Task<bool> ProductExistsAsync(Guid productId, CancellationToken ct);
}
