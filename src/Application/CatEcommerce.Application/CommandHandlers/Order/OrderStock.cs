using CatEcommerce.Domain.Aggregates.Order;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Application.CommandHandlers.Order;

/// <summary>What <see cref="OrderStock"/> reserved, or the refusal to answer with.</summary>
public sealed record OrderStockOutcome(IReadOnlyList<AllocationDto> Allocations, string? ViolationCode, string? Message, string? PropertyName)
{
    public bool IsSuccess => ViolationCode is null;

    public static OrderStockOutcome Success(IReadOnlyList<AllocationDto> allocations) => new(allocations, null, null, null);

    public static OrderStockOutcome Failure(string code, string message, string property) => new([], code, message, property);
}

/// <summary>
/// HO-ORD-001 §8.9 (ADR-208, D-4) -- Ordering's whole relationship with stock. <b>Ordering never picks a batch</b>: it states a demand per SKU and
/// Inventory's <see cref="IStockLedger"/> answers with the FEFO batches, which are recorded on the order line. Every call carries the reference
/// (<c>CustomerOrder</c>, order, line) and the party (customer, cat) that Inventory's recall trace reads back (US-IMP-11 AC2). The port enlists without
/// saving; a business refusal is a result, not an exception.
/// </summary>
public static class OrderStock
{
    public static StockReference ReferenceFor(OrderAggregate order, Guid? lineId = null) =>
        new(order.Channel == OrderChannel.Counter ? StockReferenceKind.PosSale : StockReferenceKind.CustomerOrder, order.Id, lineId, order.OrderNumber);

    public static StockParty PartyFor(OrderAggregate order, Guid? catId) => new(order.CustomerId, catId);

    /// <summary>
    /// Reserves every line (each component of a bundle) FEFO at the order's branch. Demands are issued in SKU order so concurrent placements take their
    /// stock rows in one global sequence and queue instead of deadlocking. When two lines of one placement need the same SKU (a prescription food for two
    /// cats), the enlisted rows are flushed in between so the second plan sees what the first took.
    /// </summary>
    public static async Task<OrderStockOutcome> ReserveAsync(IStockLedger ledger, ITransactionalScope scope, OrderAggregate order, CancellationToken ct = default)
    {
        var demands = order.Lines
            .SelectMany(line => line.RequiredBySku().Select(r => (Line: line, SkuId: r.Key, Quantity: r.Value)))
            .OrderBy(d => d.SkuId).ThenBy(d => d.Line.LineNumber)
            .ToList();

        var allocations = new List<AllocationDto>();
        var reserved = new HashSet<Guid>();
        foreach (var (line, skuId, quantity) in demands)
        {
            if (!reserved.Add(skuId)) await scope.FlushAsync(ct);

            var result = await ledger.ReserveAsync(
                new StockDemand(order.BranchId, skuId, quantity, MovementKind.Sale, null, null, null),
                ReferenceFor(order, line.Id), PartyFor(order, line.CatId), ct);

            if (!result.IsSuccess)
            {
                return result.ViolationCode == BusinessViolationCodes.InsufficientStock
                    ? OrderStockOutcome.Failure(BusinessViolationCodes.StockNoLongerAvailable, OrderMessages.StockNoLongerAvailableFor(line.ProductName), "Lines")
                    : OrderStockOutcome.Failure(result.ViolationCode ?? BusinessViolationCodes.StockNoLongerAvailable, result.Message ?? OrderMessages.StockNoLongerAvailable, result.PropertyName ?? "Lines");
            }

            allocations.AddRange(result.Value!.Lines.Select(l => new AllocationDto(line.Id, skuId, l.BatchId, l.LotNumber, l.LocationId, l.Quantity, l.ExpiresOn)));
        }

        return OrderStockOutcome.Success(allocations);
    }

    /// <summary>
    /// The counter's version of <see cref="ReserveAsync"/> (C26): the goods leave the shelf now, so every demand is <b>deducted</b> FEFO in one go against the
    /// same ledger an online order uses (US-ORD-16 AC1), stamped <c>PosSale</c> with the order and line. The batches are recorded on the lines like a reservation's.
    /// </summary>
    public static async Task<OrderStockOutcome> DeductAsync(IStockLedger ledger, ITransactionalScope scope, OrderAggregate order, CancellationToken ct = default)
    {
        var demands = order.Lines
            .SelectMany(line => line.RequiredBySku().Select(r => (Line: line, SkuId: r.Key, Quantity: r.Value)))
            .OrderBy(d => d.SkuId).ThenBy(d => d.Line.LineNumber)
            .ToList();

        var allocations = new List<AllocationDto>();
        var taken = new HashSet<Guid>();
        foreach (var (line, skuId, quantity) in demands)
        {
            if (!taken.Add(skuId)) await scope.FlushAsync(ct);

            var result = await ledger.DeductAsync(
                new StockDemand(order.BranchId, skuId, quantity, MovementKind.Sale, null, null, null),
                ReferenceFor(order, line.Id), PartyFor(order, line.CatId), ct);

            if (!result.IsSuccess)
            {
                return result.ViolationCode == BusinessViolationCodes.InsufficientStock
                    ? OrderStockOutcome.Failure(BusinessViolationCodes.StockNoLongerAvailable, OrderMessages.StockNoLongerAvailableFor(line.ProductName), "Lines")
                    : OrderStockOutcome.Failure(result.ViolationCode ?? BusinessViolationCodes.StockNoLongerAvailable, result.Message ?? OrderMessages.StockNoLongerAvailable, result.PropertyName ?? "Lines");
            }

            allocations.AddRange(result.Value!.Select(l => new AllocationDto(line.Id, skuId, l.BatchId, l.LotNumber, l.LocationId, l.Quantity, l.ExpiresOn)));
        }

        return OrderStockOutcome.Success(allocations);
    }
}
