using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// HO-IMP-001 §8.9 / ADR-102 (US-IMP-07, BR-4) -- First-Expired-First-Out as a PURE domain service: no I/O, no clock (the branch's today is an
/// argument), deterministic. It is the one place the allocation order lives; <c>EfStockLedger</c> feeds it candidates and turns its shortfall into
/// <c>InsufficientStock</c>.
///
/// <list type="number">
/// <item>Discard a candidate whose batch is not <c>Available</c>, is expired (<see cref="BatchExpiryRules"/> -- the one expiry predicate), or has
/// nothing free (<c>Free &lt;= 0</c>).</item>
/// <item>Order by <c>ExpiresOn</c> ascending with <b>nulls last</b> (undated goods are FIFO after every dated batch), then <c>ReceivedOn</c> ascending,
/// then the location's FEFO priority (warehouse 1, shop floor 2, the rest 3 -- <see cref="StockLocationRules.FefoPriority"/>), then <c>BatchId</c>.</item>
/// <item>Take from each in order until the quantity is met, splitting across batches and locations as needed.</item>
/// <item>Anything left is the <c>Shortfall</c>.</item>
/// </list>
/// </summary>
public sealed class FefoAllocator
{
    public FefoAllocation Allocate(decimal quantity, IReadOnlyList<FefoCandidate> candidates, DateOnly branchToday)
    {
        var remaining = quantity;
        var lines = new List<FefoAllocationLine>();

        foreach (var candidate in Order(Usable(candidates, branchToday)))
        {
            if (remaining <= 0m) break;

            var take = Math.Min(remaining, candidate.Free);
            lines.Add(new FefoAllocationLine(candidate.BatchId, candidate.LocationId, take));
            remaining -= take;
        }

        return new FefoAllocation(lines, Math.Max(0m, remaining));
    }

    /// <summary>The candidates FEFO would consider at all (step 1) -- also what a sellable-quantity read sums.</summary>
    public static IEnumerable<FefoCandidate> Usable(IEnumerable<FefoCandidate> candidates, DateOnly branchToday) =>
        candidates.Where(c => c.Status == BatchStatus.Available && !BatchExpiryRules.IsExpired(c.ExpiresOn, branchToday) && c.Free > 0m);

    /// <summary>Step 2's order, exposed so a plan can be compared with an explicit choice.</summary>
    public static IEnumerable<FefoCandidate> Order(IEnumerable<FefoCandidate> candidates) =>
        candidates
            .OrderBy(c => c.ExpiresOn is null)
            .ThenBy(c => c.ExpiresOn)
            .ThenBy(c => c.ReceivedOn)
            .ThenBy(c => c.LocationPriority)
            .ThenBy(c => c.BatchId);
}
