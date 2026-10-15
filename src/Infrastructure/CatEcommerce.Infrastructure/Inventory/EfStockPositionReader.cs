using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 §8.14 / §12.2 -- every read is <c>AsNoTracking</c> with an explicit projection, never an aggregate. Nothing here writes: the
/// projection (<c>stock_position</c>) is written only by the ledger triggers (ADR-099).
/// </summary>
public sealed class EfStockPositionReader(CatEcommerceDbContext db) : IStockPositionReader
{
    public async Task<IReadOnlyDictionary<Guid, StockLocationFact>> GetLocationsAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken ct)
    {
        if (locationIds.Count == 0) return new Dictionary<Guid, StockLocationFact>();

        var ids = locationIds.Distinct().ToList();
        var rows = await db.StockLocations.AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.BranchId, l.Code, l.Kind, l.IsActive })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => new StockLocationFact(r.Id, r.BranchId, r.Code, r.Kind, r.IsActive));
    }

    public async Task<StockLocationFact?> GetQuarantineLocationAsync(Guid branchId, CancellationToken ct)
    {
        var row = await db.StockLocations.AsNoTracking()
            .Where(l => l.BranchId == branchId && l.Kind == StockLocationKind.Quarantine && l.IsActive)
            .Select(l => new { l.Id, l.BranchId, l.Code, l.Kind, l.IsActive })
            .FirstOrDefaultAsync(ct);

        return row is null ? null : new StockLocationFact(row.Id, row.BranchId, row.Code, row.Kind, row.IsActive);
    }

    public async Task<IReadOnlyList<StockCandidate>> GetCandidatesAsync(Guid branchId, Guid skuId, CancellationToken ct)
    {
        var rows = await (
            from p in db.StockPositions.AsNoTracking()
            join b in db.Batches.AsNoTracking() on p.BatchId equals b.Id
            join l in db.StockLocations.AsNoTracking() on p.LocationId equals l.Id
            where p.SkuId == skuId && l.BranchId == branchId && l.IsActive && (p.OnHand > 0m || p.Reserved > 0m)
            select new { p.BatchId, b.LotNumber, b.ExpiresOn, b.ReceivedOn, b.Status, p.LocationId, l.Kind, p.OnHand, p.Reserved })
            .ToListAsync(ct);

        return rows.Select(r => new StockCandidate(r.BatchId, r.LotNumber, r.ExpiresOn, r.ReceivedOn, r.Status, r.LocationId, r.Kind, r.OnHand, r.Reserved)).ToList();
    }

    public async Task<IReadOnlyList<StockPositionRow>> GetPositionsAtLocationAsync(Guid locationId, IReadOnlyCollection<Guid>? skuIds, CancellationToken ct)
    {
        var query = db.StockPositions.AsNoTracking().Where(p => p.LocationId == locationId && p.OnHand > 0m);
        if (skuIds is { Count: > 0 })
        {
            var ids = skuIds.ToList();
            query = query.Where(p => ids.Contains(p.SkuId));
        }

        return await query.OrderBy(p => p.SkuId).ThenBy(p => p.BatchId)
            .Select(p => new StockPositionRow(p.SkuId, p.BatchId, p.LocationId, p.OnHand, p.Reserved))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StockPositionRow>> GetPositionsOfBatchAsync(Guid batchId, CancellationToken ct) =>
        await db.StockPositions.AsNoTracking()
            .Where(p => p.BatchId == batchId && (p.OnHand > 0m || p.Reserved > 0m))
            .OrderBy(p => p.LocationId)
            .Select(p => new StockPositionRow(p.SkuId, p.BatchId, p.LocationId, p.OnHand, p.Reserved))
            .ToListAsync(ct);

    public Task<bool> LocationHasStockAsync(Guid locationId, CancellationToken ct) =>
        db.StockPositions.AsNoTracking().AnyAsync(p => p.LocationId == locationId && (p.OnHand > 0m || p.Reserved > 0m), ct);

    public async Task<IReadOnlyDictionary<Guid, BatchFact>> GetBatchFactsAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken ct)
    {
        if (batchIds.Count == 0) return new Dictionary<Guid, BatchFact>();

        var ids = batchIds.Distinct().ToList();
        var rows = await db.Batches.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .Select(b => new { b.Id, b.SkuId, b.BranchId, b.LotNumber, b.ExpiresOn, b.ReceivedOn, b.Status })
            .ToListAsync(ct);

        return rows.ToDictionary(r => r.Id, r => new BatchFact(r.Id, r.SkuId, r.BranchId, r.LotNumber, r.ExpiresOn, r.ReceivedOn, r.Status));
    }

    /// <summary>The batch's unit cost in base currency and its basis -- <c>v_batch_unit_cost</c>: landed when the consignment's allocation has a line, else purchase.</summary>
    public async Task<IReadOnlyDictionary<Guid, BatchUnitCost>> GetUnitCostsAsync(IReadOnlyCollection<Guid> batchIds, CancellationToken ct)
    {
        if (batchIds.Count == 0) return new Dictionary<Guid, BatchUnitCost>();

        var ids = batchIds.Distinct().ToList();
        return await db.BatchUnitCosts.AsNoTracking().Where(b => ids.Contains(b.BatchId))
            .ToDictionaryAsync(b => b.BatchId, b => new BatchUnitCost(b.UnitCostBase, b.CostBasis), ct);
    }

    // ── reservations (§8.7) -- remaining = Σ reservation entries + Σ consuming ledger entries (negative); nothing stores it ────────────

    public async Task<IReadOnlyList<ReservationRemaining>> GetRemainingReservationsAsync(
        StockReferenceKind referenceKind, Guid referenceId, Guid? referenceLineId, CancellationToken ct)
    {
        var reserved = await db.StockReservationEntries.AsNoTracking()
            .Where(e => e.ReferenceKind == referenceKind && e.ReferenceId == referenceId && (referenceLineId == null || e.ReferenceLineId == referenceLineId))
            .GroupBy(e => new { e.ReferenceLineId, e.BatchId, e.LocationId, e.SkuId })
            .Select(g => new { g.Key.ReferenceLineId, g.Key.BatchId, g.Key.LocationId, g.Key.SkuId, Quantity = g.Sum(e => e.QuantityDelta) })
            .ToListAsync(ct);

        var consumed = await db.StockLedgerEntries.AsNoTracking()
            .Where(e => e.ConsumesReservation && e.ReferenceKind == referenceKind && e.ReferenceId == referenceId && (referenceLineId == null || e.ReferenceLineId == referenceLineId))
            .GroupBy(e => new { e.ReferenceLineId, e.BatchId, e.LocationId })
            .Select(g => new { g.Key.ReferenceLineId, g.Key.BatchId, g.Key.LocationId, Quantity = g.Sum(e => e.QuantityDelta) })
            .ToListAsync(ct);

        return reserved
            .Select(r => new ReservationRemaining(
                r.ReferenceLineId, r.BatchId, r.LocationId, r.SkuId,
                r.Quantity + consumed.Where(c => c.ReferenceLineId == r.ReferenceLineId && c.BatchId == r.BatchId && c.LocationId == r.LocationId).Sum(c => c.Quantity)))
            .Where(r => r.Quantity > 0m)
            .OrderBy(r => r.ReferenceLineId).ThenBy(r => r.BatchId).ThenBy(r => r.LocationId)
            .ToList();
    }

    public Task<bool> WasConsumedByPartyAsync(Guid batchId, Guid? customerId, Guid? catId, CancellationToken ct)
    {
        var consuming = db.StockLedgerEntries.AsNoTracking().Where(e => e.BatchId == batchId);
        consuming = consuming.Where(e => e.Kind == MovementKind.Sale || e.Kind == MovementKind.Dispense || e.Kind == MovementKind.ClinicalUse || e.Kind == MovementKind.BoardingUse);

        if (customerId is null && catId is null)
            return consuming.AnyAsync(e => e.ReferenceKind == StockReferenceKind.PosSale && e.CustomerId == null, ct);

        return consuming.AnyAsync(e => (customerId != null && e.CustomerId == customerId) || (catId != null && e.CatId == catId), ct);
    }

    // ── procurement progress (ADR-112) -- derived from posted receipt lines, never stored. ─────────────────────────────────────────

    public Task<bool> PurchaseOrderHasReceiptsAsync(Guid purchaseOrderId, CancellationToken ct) =>
        db.GoodsReceipts.AsNoTracking().AnyAsync(g => g.PurchaseOrderId == purchaseOrderId, ct);

    public async Task<IReadOnlyList<PurchaseOrderLineReceipt>> GetPurchaseOrderReceiptsAsync(Guid purchaseOrderId, CancellationToken ct)
    {
        var rows = await db.GoodsReceipts.AsNoTracking()
            .Where(g => g.PurchaseOrderId == purchaseOrderId)
            .SelectMany(g => g.Lines)
            .Where(l => l.PurchaseOrderLineId != null)
            .GroupBy(l => l.PurchaseOrderLineId!.Value)
            .Select(g => new { LineId = g.Key, Accepted = g.Sum(l => l.QuantityAccepted), Rejected = g.Sum(l => l.QuantityRejected) })
            .ToListAsync(ct);

        return rows.Select(r => new PurchaseOrderLineReceipt(r.LineId, r.Accepted, r.Rejected)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, PurchaseOrderReceiptState>> GetPurchaseOrderReceiptStatesAsync(IReadOnlyCollection<Guid> purchaseOrderIds, CancellationToken ct)
    {
        if (purchaseOrderIds.Count == 0) return new Dictionary<Guid, PurchaseOrderReceiptState>();

        var ids = purchaseOrderIds.Distinct().ToList();
        var lines = await db.PurchaseOrders.AsNoTracking().Where(p => ids.Contains(p.Id))
            .SelectMany(p => p.Lines, (p, l) => new { OrderId = p.Id, LineId = l.Id, l.OrderedQuantity })
            .ToListAsync(ct);

        var received = await db.GoodsReceipts.AsNoTracking()
            .Where(g => g.PurchaseOrderId != null && ids.Contains(g.PurchaseOrderId.Value))
            .SelectMany(g => g.Lines)
            .Where(l => l.PurchaseOrderLineId != null)
            .GroupBy(l => l.PurchaseOrderLineId!.Value)
            .Select(g => new { LineId = g.Key, Accepted = g.Sum(l => l.QuantityAccepted), Rejected = g.Sum(l => l.QuantityRejected) })
            .ToDictionaryAsync(r => r.LineId, ct);

        return ids.ToDictionary(
            id => id,
            id => PurchaseOrderProgress.StateOf(lines.Where(l => l.OrderId == id)
                .Select(l => received.TryGetValue(l.LineId, out var r)
                    ? PurchaseOrderProgress.ForLine(l.OrderedQuantity, r.Accepted, r.Rejected)
                    : PurchaseOrderProgress.ForLine(l.OrderedQuantity, 0m, 0m)).ToList()));
    }

    public async Task<IReadOnlyList<PurchaseOrderReceiptFact>> GetPurchaseOrderReceiptHeadersAsync(Guid purchaseOrderId, CancellationToken ct)
    {
        var rows = await db.GoodsReceipts.AsNoTracking()
            .Where(g => g.PurchaseOrderId == purchaseOrderId)
            .OrderByDescending(g => g.ReceivedOn)
            .Select(g => new { g.Id, g.Number, g.ReceivedOn })
            .ToListAsync(ct);

        return rows.Select(r => new PurchaseOrderReceiptFact(r.Id, r.Number, r.ReceivedOn)).ToList();
    }

    public async Task<IReadOnlyList<ConsignmentBatchFact>> GetConsignmentBatchesAsync(Guid consignmentId, CancellationToken ct)
    {
        var rows = await db.Batches.AsNoTracking()
            .Where(b => b.ConsignmentId == consignmentId)
            .OrderBy(b => b.ReceivedOn).ThenBy(b => b.Id)
            .Select(b => new { b.Id, b.ReceivedQuantity, b.PurchaseUnitCostAmount, b.PurchaseUnitCostCurrency })
            .ToListAsync(ct);

        return rows.Select(r => new ConsignmentBatchFact(r.Id, r.ReceivedQuantity, r.PurchaseUnitCostAmount, r.PurchaseUnitCostCurrency)).ToList();
    }
}
