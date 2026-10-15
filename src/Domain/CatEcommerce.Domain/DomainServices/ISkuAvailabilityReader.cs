using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 8.7 -- the one question command handlers ask about availability ("is this SKU
/// sellable now?"), answered by <c>Infrastructure/Product/SkuAvailabilityReader</c>. Shop never
/// computes stock (ADR-082): the answer is derived from HO-IMP-001's published view
/// <c>inventory.v_sku_sellable</c> (ADR-116). A SKU that does not exist, or has no row in the
/// view, is reported <c>OutOfStock</c>. Read-only.
/// </summary>
public interface ISkuAvailabilityReader
{
    Task<IReadOnlyDictionary<Guid, AvailabilityStatus>> GetAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default);
}
