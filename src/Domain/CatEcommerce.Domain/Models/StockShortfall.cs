namespace CatEcommerce.Domain.Models;

/// <summary>
/// The parameter of <c>IStockLedger.RecordShortfall</c> (HO-IMP-001 §8.8, "for HO-IMP-001's reorder
/// list"). The only concrete shape any handover gives is HO-EMR-001 §8.6 step 5's literal call --
/// <c>StockShortfall(SkuId, LocationId, Quantity, VisitId, LineId)</c> -- which is EMR-specific
/// (<c>VisitId</c>/<c>LineId</c>), even though the port is meant to be called by any consuming
/// context (HO-PHM-001, HO-SRG-001 also record shortfalls). Generalised here to
/// <see cref="ReferenceId"/>/<see cref="ReferenceLineId"/> so every consumer can pass its own
/// reference, matching the shape <see cref="StockReference"/> already uses elsewhere on this port.
/// Recorded as a gap/generalisation in docs/handover/CONTRACTS.md -- the owner (HO-IMP-001) may
/// define it differently when it lands.
/// </summary>
public sealed record StockShortfall(
    Guid SkuId,
    Guid LocationId,
    decimal Quantity,
    Guid ReferenceId,
    Guid? ReferenceLineId);
