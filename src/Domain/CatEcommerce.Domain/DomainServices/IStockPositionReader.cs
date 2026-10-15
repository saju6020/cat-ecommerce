using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-IMP-001 §8.14 -- read-only facts for Inventory's own handlers and for <c>EfStockLedger</c>: FEFO candidates, positions per
/// location (counts, adjustments, breaches), batch and location facts, unit costs and the derived progress and reservation reads.
/// Every method is a read; nothing here writes (the projection is written only by the ledger triggers, ADR-099).
/// </summary>
public interface IStockPositionReader
{
    /// <summary>Every position of <paramref name="skuId"/> at the branch's ACTIVE locations (on-hand or reserved above zero), with
    /// its batch and location facts.</summary>
    Task<IReadOnlyList<StockCandidate>> GetCandidatesAsync(Guid branchId, Guid skuId, CancellationToken ct);

    /// <summary>Positions with <c>on_hand &gt; 0</c> at one location, optionally for some SKUs.</summary>
    Task<IReadOnlyList<StockPositionRow>> GetPositionsAtLocationAsync(Guid locationId, IReadOnlyCollection<Guid>? skuIds, CancellationToken ct);

    /// <summary>Every position of one batch (on-hand or reserved above zero), at every location and branch -- a write-off, a recall and the
    /// stock-held check read it.</summary>
    Task<IReadOnlyList<StockPositionRow>> GetPositionsOfBatchAsync(Guid batchId, CancellationToken ct);

    Task<bool> LocationHasStockAsync(Guid locationId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, BatchFact>> GetBatchFactsAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken ct);

    /// <summary><c>v_batch_unit_cost</c> per batch: the unit cost in base currency and its basis -- <c>Landed</c> when an allocation line exists,
    /// else <c>Purchase</c> (US-IMP-05 AC2, ADR-106).</summary>
    Task<IReadOnlyDictionary<Guid, BatchUnitCost>> GetUnitCostsAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, StockLocationFact>> GetLocationsAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken ct);

    /// <summary>The branch's one active quarantine location (<c>ix_stock_location_one_quarantine</c>).</summary>
    Task<StockLocationFact?> GetQuarantineLocationAsync(Guid branchId, CancellationToken ct);

    /// <summary>Remaining reservations of a reference (or one line): Σ reservation entries + Σ consuming ledger entries (§8.7).</summary>
    Task<IReadOnlyList<ReservationRemaining>> GetRemainingReservationsAsync(StockReferenceKind referenceKind, Guid referenceId, Guid? referenceLineId, CancellationToken ct);

    /// <summary>The party actually received this batch: a consuming ledger entry of the batch names the customer or the cat (§8.8 rule 8 -- a return
    /// is only traceable to someone who got it). With neither given, an anonymous counter (<c>PosSale</c>) sale of the batch counts.</summary>
    Task<bool> WasConsumedByPartyAsync(Guid batchId, Guid? customerId, Guid? catId, CancellationToken ct);

    /// <summary>Σ accepted / rejected per purchase order line over posted receipt lines (ADR-112).</summary>
    Task<IReadOnlyList<PurchaseOrderLineReceipt>> GetPurchaseOrderReceiptsAsync(Guid purchaseOrderId, CancellationToken ct);

    Task<bool> PurchaseOrderHasReceiptsAsync(Guid purchaseOrderId, CancellationToken ct);

    /// <summary>The DERIVED receipt state of each purchase order (ADR-112, <c>PurchaseOrderProgress</c>) -- for the list filter and column.</summary>
    Task<IReadOnlyDictionary<Guid, PurchaseOrderReceiptState>> GetPurchaseOrderReceiptStatesAsync(IReadOnlyCollection<Guid> purchaseOrderIds, CancellationToken ct);

    /// <summary>The posted receipts that reference a purchase order (newest first) -- for the PO detail's <c>receipts[]</c>.</summary>
    Task<IReadOnlyList<PurchaseOrderReceiptFact>> GetPurchaseOrderReceiptHeadersAsync(Guid purchaseOrderId, CancellationToken ct);

    /// <summary>The batches received on a consignment with their accepted quantity and purchase cost (landed-cost input).</summary>
    Task<IReadOnlyList<ConsignmentBatchFact>> GetConsignmentBatchesAsync(Guid consignmentId, CancellationToken ct);
}
