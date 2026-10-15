using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-EMR-001 §8.6 steps 3-4 -- which batches of a SKU the branch's clinic dispensary holds, which of them Inventory would let a vet use, and
/// which one FEFO would choose. Domain logic spanning several of the ledger's read members, used identically by the record-treatment handler and
/// the batch-picker query, so the screen and the command can never disagree about the default (the same reason <c>GetFefoPreviewQuery</c>
/// calls <c>PlanAsync</c>). It decides nothing about expiry or status itself: Inventory's <c>GetBatchUsabilityAsync</c> does, and
/// <c>PlanAsync</c> names the FEFO first batch. Reads only -- nothing is enlisted.
/// </summary>
public sealed class DispensaryBatchPicker(IStockLedger ledger)
{
    /// <summary>The dispensary and its batches of <paramref name="skuId"/>; a failure is Inventory's (<c>DispensaryNotFound</c> when the branch has none).</summary>
    public async Task<StockResult<DispensaryStock>> ReadAsync(Guid branchId, Guid skuId, CancellationToken ct)
    {
        var dispensary = await ledger.GetClinicDispensaryAsync(branchId, ct);
        if (!dispensary.IsSuccess) return StockResult<DispensaryStock>.Failure(dispensary.ViolationCode!, dispensary.Message!, dispensary.PropertyName);

        var held = await ledger.GetDispensaryBatchesAsync(branchId, [skuId], ct);
        if (!held.IsSuccess) return StockResult<DispensaryStock>.Failure(held.ViolationCode!, held.Message!, held.PropertyName);

        var batches = held.Value!.Where(b => b.SkuId == skuId).ToList();
        if (batches.Count == 0) return StockResult<DispensaryStock>.Success(new DispensaryStock(dispensary.Value!, []));

        var usability = await ledger.GetBatchUsabilityAsync(branchId, batches.Select(b => b.BatchId).Distinct().ToList(), ct);
        if (!usability.IsSuccess) return StockResult<DispensaryStock>.Failure(usability.ViolationCode!, usability.Message!, usability.PropertyName);
        var byBatch = usability.Value!.ToDictionary(u => u.BatchId);

        // The FEFO first batch is whatever Inventory's own allocator would take first for one unit.
        var fefo = await ledger.PlanAsync(new StockDemand(branchId, skuId, 1m, MovementKind.ClinicalUse, null, null, null), ct);
        var fefoBatchId = fefo.IsSuccess ? fefo.Value!.Lines.FirstOrDefault()?.BatchId : null;

        var choices = batches
            .Select(b =>
            {
                var verdict = byBatch.GetValueOrDefault(b.BatchId);
                return new DispensaryBatchChoice(
                    b.BatchId, b.LotNumber, b.ExpiresOn, b.LocationId, Math.Max(0m, b.Free), verdict?.RefusalCode, verdict?.Message, fefoBatchId == b.BatchId);
            })
            .OrderBy(c => c.RefusalCode is not null)
            .ThenBy(c => c.ExpiresOn is null)
            .ThenBy(c => c.ExpiresOn)
            .ThenBy(c => c.LotNumber, StringComparer.Ordinal)
            .ThenBy(c => c.BatchId)
            .ToList();

        return StockResult<DispensaryStock>.Success(new DispensaryStock(dispensary.Value!, choices));
    }
}
