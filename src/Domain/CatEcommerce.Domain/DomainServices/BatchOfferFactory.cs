using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-PHM-001 §8.7 -- turns Inventory's facts (<see cref="DispensaryBatch"/>, from the published <see cref="IStockLedger.GetDispensaryBatchesAsync"/>) into the <see cref="BatchOffer"/>s the
/// pure <see cref="DispensingBatchPlanner"/> works on. Inventory returns only batches held at the clinic DISPENSARY (US-PHM-05: batches offered from the branch's clinic
/// dispensary); a batch that is not <c>Available</c> (quarantined, recalled, written off) is offered but not dispensable, with Inventory's reason. Expiry is NOT decided
/// here -- the planner decides it from the date, with no override. Pure.
/// </summary>
public static class BatchOfferFactory
{
    public static IReadOnlyList<BatchOffer> FromCandidates(IReadOnlyCollection<Guid> linkedSkuIds, IEnumerable<DispensaryBatch> batches) =>
        batches
            .Where(c => linkedSkuIds.Contains(c.SkuId))
            .Select(c => new BatchOffer(
                c.SkuId, c.BatchId, c.LotNumber, c.ExpiresOn, Math.Max(0m, c.Free),
                c.BatchStatus == BatchStatus.Available, Reason(c.BatchStatus), c.LocationId))
            .ToList();

    private static string? Reason(BatchStatus status) =>
        status == BatchStatus.Quarantined ? "This batch is in quarantine."
        : status == BatchStatus.Recalled ? "This batch has been recalled."
        : status == BatchStatus.WrittenOff ? "This batch was written off."
        : null;
}
