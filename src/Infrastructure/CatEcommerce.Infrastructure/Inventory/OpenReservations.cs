using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>One reservation line of a batch that is still open: promised to an order and neither released nor consumed.</summary>
public sealed record OpenReservation(StockReferenceKind ReferenceKind, Guid ReferenceId, Guid ReferenceLineId, Guid? CustomerId, Guid? CatId, decimal Remaining);

/// <summary>
/// HO-IMP-001 section 8.7 / 8.12 -- the reservation lines of a batch that are STILL open. A line's remaining quantity is the sum of its reservation entries (the
/// <c>Reserve</c> carries the customer and cat; a <c>Release</c> carries NO party) plus its consuming ledger entries (negative), so the sum is taken per
/// (reference, line) BEFORE the party is looked at -- grouping by party would put a Release in a group of its own and never net it against its Reserve. The ledger
/// contract is unchanged: a Release is still written without a party. Used by the recall trace and the recall recipient resolver.
/// </summary>
public static class OpenReservations
{
    public static async Task<List<OpenReservation>> ForBatchAsync(CatEcommerceDbContext db, Guid batchId, CancellationToken ct = default)
    {
        var entries = await db.StockReservationEntries.AsNoTracking().Where(e => e.BatchId == batchId)
            .Select(e => new { e.ReferenceKind, e.ReferenceId, e.ReferenceLineId, e.CustomerId, e.CatId, e.QuantityDelta }).ToListAsync(ct);
        var consumed = await db.StockLedgerEntries.AsNoTracking().Where(e => e.BatchId == batchId && e.ConsumesReservation)
            .Select(e => new { e.ReferenceKind, e.ReferenceId, e.ReferenceLineId, e.QuantityDelta }).ToListAsync(ct);

        return entries.GroupBy(e => (e.ReferenceKind, e.ReferenceId, e.ReferenceLineId)).Select(g =>
        {
            var remaining = g.Sum(e => e.QuantityDelta)
                + consumed.Where(c => c.ReferenceKind == g.Key.ReferenceKind && c.ReferenceId == g.Key.ReferenceId && c.ReferenceLineId == g.Key.ReferenceLineId).Sum(c => c.QuantityDelta);
            return new OpenReservation(
                g.Key.ReferenceKind, g.Key.ReferenceId, g.Key.ReferenceLineId, g.Select(e => e.CustomerId).FirstOrDefault(c => c != null), g.Select(e => e.CatId).FirstOrDefault(c => c != null), remaining);
        }).Where(r => r.Remaining > 0m).ToList();
    }
}
