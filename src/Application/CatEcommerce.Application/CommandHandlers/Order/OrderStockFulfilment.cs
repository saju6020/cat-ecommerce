using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Entities;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Platform.Infrastructure.Core.Commands;
using static CatEcommerce.Application.CommandHandlers.Order.OrderHandlerSupport;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>
/// HO-ORD-001 §8.9 (ADR-208, BR-14) -- the fulfilment half of Ordering's relationship with stock, only through Inventory's published <see cref="IStockLedger"/>:
/// the batch check before an order leaves the shop, the reservation becoming the sale, and the staff's FEFO override. Ordering never chooses a batch or a
/// location -- staff name a target batch and Inventory picks the locations, validates and audits it (owner decision 2, 2026-10-09).
/// </summary>
public static class OrderStockFulfilment
{
    /// <summary>
    /// BR-14, US-IMP-11 AC4: every batch allocated to the order must still be usable -- not recalled (<c>BatchRecalled</c>), not otherwise withdrawn
    /// (<c>BatchNotUsable</c>), not expired in the branch's local date (<c>BatchExpired</c>). Inventory judges it (<c>GetBatchUsabilityAsync</c>); the message
    /// names the product and tells staff to reallocate the line. Null when every batch is usable.
    /// </summary>
    public static async Task<CommandResponse?> CheckBatchesAsync(IStockLedger ledger, OrderAggregate order)
    {
        var batchIds = order.Lines.SelectMany(l => l.Allocations).Select(a => a.BatchId).Distinct().ToList();
        if (batchIds.Count == 0) return null;

        var usability = await ledger.GetBatchUsabilityAsync(order.BranchId, batchIds, default);
        if (!usability.IsSuccess)
            return Violation(usability.ViolationCode ?? BusinessViolationCodes.BatchNotUsable, usability.PropertyName ?? "LineId", usability.Message ?? "The batches could not be checked.");

        var byBatch = usability.Value!.ToDictionary(u => u.BatchId);
        foreach (var line in order.Lines)
        {
            foreach (var allocation in line.Allocations)
            {
                if (!byBatch.TryGetValue(allocation.BatchId, out var batch) || batch.IsUsable) continue;

                return batch.RefusalCode switch
                {
                    BusinessViolationCodes.BatchRecalled => Violation(BusinessViolationCodes.BatchRecalled, "LineId", $"'{line.ProductName}' is allocated from a recalled batch. Reallocate the line to another batch before this order can leave the shop."),
                    BusinessViolationCodes.BatchExpired => Violation(BusinessViolationCodes.BatchExpired, "LineId", $"'{line.ProductName}' is allocated from a batch that has expired. Reallocate the line to another batch."),
                    _ => Violation(BusinessViolationCodes.BatchNotUsable, "LineId", $"'{line.ProductName}' is allocated from a batch that can no longer be sold. Reallocate the line.")
                };
            }
        }

        return null;
    }

    /// <summary>
    /// The reservation becomes the sale (<c>ConsumeReservationAsync</c>, enlisted): dispatch for a delivery, collection for a pickup. One call per line so
    /// each movement carries <b>that line's</b> cat in the party stamp (US-IMP-11 AC2). Null on success, else the refusal to answer with.
    /// </summary>
    public static async Task<CommandResponse?> ConsumeAsync(IStockLedger ledger, OrderAggregate order)
    {
        foreach (var line in order.Lines)
        {
            var result = await ledger.ConsumeReservationAsync(OrderStock.ReferenceFor(order, line.Id), OrderStock.PartyFor(order, line.CatId), default);
            if (!result.IsSuccess)
                return Violation(result.ViolationCode ?? BusinessViolationCodes.OrderNotFullyAllocated, result.PropertyName ?? "Lines", result.Message ?? "The reservation could not be turned into a sale.");
        }

        return null;
    }

    /// <summary>
    /// US-IMP-07 AC2 (order half): move one line to <paramref name="targetBatchId"/>. Inventory says which item and branch the batch belongs to
    /// (<c>GetBatchUsabilityAsync</c>); the line's reservation is released and the staff-chosen batch reserved with the reason (<c>ReserveBatchAsync</c> --
    /// Inventory picks the locations, refuses an unusable or short batch and audits the override); a bundle's other components are re-reserved FEFO. The flush
    /// between the two makes the release visible to the second plan inside the caller's transaction. Returns the new allocations, or the refusal.
    /// </summary>
    public static async Task<(IReadOnlyList<AllocationDto>? Allocations, CommandResponse? Refusal)> ReallocateAsync(
        IStockLedger ledger, ITransactionalScope scope, OrderAggregate order, OrderLine line, Guid targetBatchId, string reason)
    {
        var lookup = await ledger.GetBatchUsabilityAsync(order.BranchId, [targetBatchId], default);
        var target = lookup.IsSuccess ? lookup.Value!.FirstOrDefault() : null;
        if (target is null || target.RefusalCode == BusinessViolationCodes.BatchNotFound || !line.RequiredBySku().ContainsKey(target.SkuId))
            return (null, Violation(BusinessViolationCodes.BatchNotFound, "TargetBatchId", "That batch is not stock for this item at this branch."));

        var released = await ledger.ReleaseAsync(OrderStock.ReferenceFor(order, line.Id), default);
        if (!released.IsSuccess) return (null, Violation(released.ViolationCode ?? BusinessViolationCodes.ReservationNotFound, "LineId", released.Message ?? "The old reservation could not be released."));
        await scope.FlushAsync();

        var allocations = new List<AllocationDto>();
        foreach (var (skuId, quantity) in line.RequiredBySku().OrderBy(r => r.Key))
        {
            var reference = OrderStock.ReferenceFor(order, line.Id);
            var party = OrderStock.PartyFor(order, line.CatId);

            var result = skuId == target.SkuId
                ? await ledger.ReserveBatchAsync(new StockBatchDemand(order.BranchId, skuId, quantity, targetBatchId, reason), reference, party, default)
                : await ledger.ReserveAsync(new StockDemand(order.BranchId, skuId, quantity, MovementKind.Sale, null, null, null), reference, party, default);

            if (!result.IsSuccess)
                return (null, Violation(result.ViolationCode ?? BusinessViolationCodes.InsufficientStock, result.PropertyName ?? "TargetBatchId", result.Message ?? "That batch cannot be reserved."));

            allocations.AddRange(result.Value!.Lines.Select(l => new AllocationDto(line.Id, skuId, l.BatchId, l.LotNumber, l.LocationId, l.Quantity, l.ExpiresOn)));
        }

        return (allocations, null);
    }
}
