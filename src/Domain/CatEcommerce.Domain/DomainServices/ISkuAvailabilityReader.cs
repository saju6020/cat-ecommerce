using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-SHP-001 section 8.7 -- the one question command handlers ask about availability ("is this SKU
/// sellable now?"), answered by <c>Infrastructure/Product/SkuAvailabilityReader</c>. Shop never
/// computes stock (ADR-082): today the answer is the interim approximation described in
/// HO-SHP-001.md section 1 Amended (2026-09-28/2026-10-04); when HO-IMP-001's
/// <c>inventory.v_sellable_stock</c> exists, only that implementation changes. A SKU that does not
/// exist is reported <c>OutOfStock</c>. Read-only.
/// </summary>
public interface ISkuAvailabilityReader
{
    Task<IReadOnlyDictionary<Guid, AvailabilityStatus>> GetAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct = default);
}
