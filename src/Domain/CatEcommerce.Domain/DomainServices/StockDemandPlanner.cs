using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>What a demand resolved to: the plan, whether it departs from FEFO (and why), and FEFO's own plan for the audit entry.</summary>
public sealed record PlannedDemand(StockPlan Plan, bool Overridden, string? OverrideReason, IReadOnlyList<FefoAllocationLine> FefoLines);

/// <summary>
/// HO-IMP-001 §8.8 -- the RULES every <c>IStockLedger</c> method applies, as one PURE domain service (no I/O: the ledger reads the facts, this decides),
/// so they are tested once and exhaustively without a database (U-E5 ... U-E11):
/// <list type="number">
/// <item>the SKU exists and is active; the quantity is positive, at most three decimals, whole unless the SKU allows fractions;</item>
/// <item>the <b>party stamp</b> the recall trace depends on (ADR-104);</item>
/// <item>the purpose is permitted at every location used (<see cref="StockLocationRules"/>);</item>
/// <item><b>the batch is <c>Available</c> and not expired in the branch's local date -- no parameter, role or reason relaxes this</b> (ADR-101);</item>
/// <item>FEFO decides the plan (<see cref="FefoAllocator"/>); a different explicit plan needs a reason of at least five characters (ADR-102);</item>
/// <item>not enough usable, unreserved stock is <c>InsufficientStock</c> with the available quantity in the message.</item>
/// </list>
/// The one thing it cannot decide is the RACE for the last unit -- that is <c>ck_stock_position_balance</c>'s, at commit (ADR-099).
/// </summary>
public sealed class StockDemandPlanner(FefoAllocator fefo)
{
    public StockResult<PlannedDemand> Plan(
        StockDemand demand, StockReference? reference, StockParty? party, SkuStockProfile? sku, IReadOnlyList<StockCandidate> allCandidates,
        IReadOnlyDictionary<Guid, StockLocationFact> namedLocations, DateOnly branchToday, bool allowShortfall)
    {
        if (!MovementKindRules.IsConsuming(demand.Purpose))
            return Fail(BusinessViolationCodes.MovementKindNotAllowed, "Only a sale, dispensing, clinical use or boarding use can be planned.", nameof(demand.Purpose));

        if (reference is not null && party is not null && PartyRefusal(demand.Purpose, reference.ReferenceKind, party) is { } missing)
            return Fail(missing.Code, missing.Message, missing.Property);

        if (sku is null) return Fail(BusinessViolationCodes.SkuNotFound, "That SKU does not exist.", nameof(demand.SkuId));
        if (!sku.IsActive) return Fail(BusinessViolationCodes.SkuInactive, "That SKU is not active.", nameof(demand.SkuId));
        if (!StockQuantity.TryValidate(demand.Quantity, sku.AllowsFractionalQuantity, out var quantityCode))
            return Fail(quantityCode!, "The quantity must be greater than zero, whole unless the item is counted in fractions, with at most three decimal places.", nameof(demand.Quantity));

        // Locations: named ones must exist in the branch, be active and permit the purpose; otherwise every location that permits it (§8.3).
        foreach (var id in NamedLocationIds(demand))
        {
            if (!namedLocations.TryGetValue(id, out var location) || location.BranchId != demand.BranchId || !location.IsActive)
                return Fail(BusinessViolationCodes.StockLocationNotFound, "That location does not exist at this branch.", nameof(demand.LocationIds));
            if (!StockLocationRules.Permits(location.Kind, demand.Purpose))
                return Fail(BusinessViolationCodes.MovementNotPermittedAtLocation,
                    $"A {demand.Purpose.DisplayName} movement is not permitted at a {location.Kind.DisplayName} location.", nameof(demand.LocationIds));
        }

        var candidates = (demand.LocationIds is { Count: > 0 } wanted
            ? allCandidates.Where(c => wanted.Contains(c.LocationId))
            : allCandidates.Where(c => StockLocationRules.Permits(c.LocationKind, demand.Purpose))).ToList();

        var fefoCandidates = candidates.Select(c => new FefoCandidate(
            c.BatchId, c.LocationId, StockLocationRules.FefoPriority(c.LocationKind), c.ExpiresOn, c.ReceivedOn, c.BatchStatus, c.Free)).ToList();
        var fefoPlan = fefo.Allocate(demand.Quantity, fefoCandidates, branchToday);
        var byKey = candidates.ToDictionary(c => (c.BatchId, c.LocationId));

        if (demand.ExplicitLines is not { Count: > 0 } explicitLines)
        {
            if (fefoPlan.Shortfall > 0m && !allowShortfall)
                return Fail(BusinessViolationCodes.InsufficientStock, InventoryMessages.InsufficientStockAvailable(demand.Quantity - fefoPlan.Shortfall), nameof(demand.Quantity));

            return StockResult<PlannedDemand>.Success(new PlannedDemand(ToPlan(fefoPlan.Lines, byKey, overridden: false, fefoPlan.Shortfall), false, null, fefoPlan.Lines));
        }

        // An explicit plan: USABLE first (rule 4 -- a reason changes nothing), then sufficient, then compared with FEFO.
        if (explicitLines.Sum(l => l.Quantity) != demand.Quantity)
            return Fail(BusinessViolationCodes.InvalidQuantity, "The explicit lines must add up to the quantity asked for.", nameof(demand.ExplicitLines));

        var chosen = new List<FefoAllocationLine>();
        foreach (var line in explicitLines)
        {
            if (!StockQuantity.TryValidate(line.Quantity, sku.AllowsFractionalQuantity, out var lineCode))
                return Fail(lineCode!, "A line quantity must be greater than zero, whole unless the item is counted in fractions.", nameof(demand.ExplicitLines));

            var anywhere = allCandidates.FirstOrDefault(c => c.BatchId == line.BatchId);
            if (anywhere is null)
                return Fail(BusinessViolationCodes.InsufficientStock, InventoryMessages.InsufficientStockAvailable(0m), nameof(demand.ExplicitLines));

            if (UsabilityRefusal(anywhere.BatchStatus, anywhere.ExpiresOn, anywhere.LotNumber, branchToday) is { } refusal)
                return Fail(refusal.Code, refusal.Message, nameof(demand.ExplicitLines));

            var free = byKey.TryGetValue((line.BatchId, line.LocationId), out var candidate) ? candidate.Free : 0m;
            if (line.Quantity > free)
                return Fail(BusinessViolationCodes.InsufficientStock, InventoryMessages.InsufficientStockAvailable(Math.Max(0m, free)), nameof(demand.ExplicitLines));

            chosen.Add(new FefoAllocationLine(line.BatchId, line.LocationId, line.Quantity));
        }

        var overridden = !SamePlan(fefoPlan.Lines, chosen);
        var reason = demand.OverrideReason?.Trim();
        if (overridden && (reason is null || reason.Length < InventoryRules.ReasonMinLength))
            return Fail(BusinessViolationCodes.AllocationOverrideReasonRequired,
                "That is not the first-expired-first-out choice. Give a reason of at least five characters.", nameof(demand.OverrideReason));

        return StockResult<PlannedDemand>.Success(new PlannedDemand(ToPlan(chosen, byKey, overridden, shortfall: 0m), overridden, overridden ? reason : null, fefoPlan.Lines));
    }

    /// <summary>Rule 4: the batch is <c>Available</c> and not expired in the branch's local date (<see cref="BatchExpiryRules"/> -- the one predicate).
    /// There is no overload, flag or reason that skips it. Null when usable.</summary>
    public static (string Code, string Message)? UsabilityRefusal(BatchStatus status, DateOnly? expiresOn, string lotNumber, DateOnly branchToday)
    {
        if (status != BatchStatus.Available)
            return (BusinessViolationCodes.BatchNotUsable, InventoryMessages.BatchNotUsable);

        return BatchExpiryRules.IsExpired(expiresOn, branchToday)
            ? (BusinessViolationCodes.BatchExpired, InventoryMessages.BatchExpired(lotNumber, expiresOn!.Value))
            : null;
    }

    /// <summary>The recall stamp every consuming movement must carry (ADR-104), enforced by the port at write time so a module added later is
    /// covered automatically. <c>Sale</c> needs the customer unless the reference is a <c>PosSale</c>; <c>Dispense</c> and <c>BoardingUse</c> need both;
    /// <c>ClinicalUse</c> needs the cat.</summary>
    public static (string Code, string Message, string Property)? PartyRefusal(MovementKind purpose, StockReferenceKind referenceKind, StockParty party)
    {
        if (purpose == MovementKind.Sale && party.CustomerId is null && referenceKind != StockReferenceKind.PosSale)
            return (BusinessViolationCodes.StockPartyRequired, "A sale needs the customer (only a counter sale may be anonymous).", nameof(StockParty.CustomerId));

        if ((purpose == MovementKind.Dispense || purpose == MovementKind.BoardingUse) && (party.CustomerId is null || party.CatId is null))
            return (BusinessViolationCodes.StockPartyRequired, $"{purpose.DisplayName} needs both the customer and the cat.", party.CustomerId is null ? nameof(StockParty.CustomerId) : nameof(StockParty.CatId));

        if (purpose == MovementKind.ClinicalUse && party.CatId is null)
            return (BusinessViolationCodes.StockPartyRequired, "Clinical use needs the cat.", nameof(StockParty.CatId));

        return null;
    }

    private static IReadOnlyList<Guid> NamedLocationIds(StockDemand demand) =>
        (demand.LocationIds ?? []).Concat(demand.ExplicitLines?.Select(l => l.LocationId) ?? []).Distinct().ToList();

    private static StockPlan ToPlan(IReadOnlyList<FefoAllocationLine> lines, IReadOnlyDictionary<(Guid, Guid), StockCandidate> byKey, bool overridden, decimal shortfall) =>
        new(lines.Select(l =>
        {
            var c = byKey[(l.BatchId, l.LocationId)];
            return new StockMovementLine(l.BatchId, c.LotNumber, c.ExpiresOn, l.LocationId, l.Quantity, overridden);
        }).ToList(), shortfall);

    /// <summary>The same (batch, location, quantity) set, in any order.</summary>
    private static bool SamePlan(IReadOnlyList<FefoAllocationLine> fefoLines, IReadOnlyList<FefoAllocationLine> chosen)
    {
        static List<(Guid, Guid, decimal)> Key(IEnumerable<FefoAllocationLine> lines) =>
            lines.GroupBy(l => (l.BatchId, l.LocationId)).Select(g => (g.Key.BatchId, g.Key.LocationId, g.Sum(l => l.Quantity)))
                .OrderBy(x => x.BatchId).ThenBy(x => x.LocationId).ToList();

        return Key(fefoLines).SequenceEqual(Key(chosen));
    }

    private static StockResult<PlannedDemand> Fail(string code, string message, string property) => StockResult<PlannedDemand>.Failure(code, message, property);
}
