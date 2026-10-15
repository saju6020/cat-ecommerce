using System.Text.Json;
using CatEcommerce.Domain.Aggregates.Audit;
using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Domain.ValueObjects;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 §8.8 -- the consumer half of the stock contract (ADR-103): <see cref="IStockLedger"/>, the ONLY way HO-ORD, HO-EMR, HO-PHM, HO-SRG
/// and HO-BRD move stock. Every method validates and then ENLISTS rows without saving; a failure is a <see cref="StockResult{T}"/> -- the port
/// never throws for a business rule. Plan and read methods write nothing.
///
/// <b>Rules every method applies</b> (§8.8): the SKU exists and is active and the quantity is positive and whole unless fractional is allowed;
/// the party stamp the recall trace depends on (<c>Sale</c> needs the customer unless the reference is a <c>PosSale</c>, <c>Dispense</c> and
/// <c>BoardingUse</c> need both, <c>ClinicalUse</c> needs the cat); the purpose is permitted at every location used; the batch is <c>Available</c>
/// and NOT expired in the branch's local date -- <b>no member of this port or any type it takes relaxes that</b> (U-E9, ADR-101); FEFO decides the
/// plan, and a different explicit plan needs a reason, flags the entries and enlists a <c>BusinessEvent</c> audit entry beside them (ADR-102).
/// The commit-time race (the last unit) is caught by <c>ck_stock_position_balance</c>, not by anything here (ADR-099).
/// </summary>
public sealed partial class EfStockLedger : IStockLedger
{
    // ── planning (writes nothing) ───────────────────────────────────────────────────────────────────────────────────────────────

    public async Task<StockResult<StockPlan>> PlanAsync(StockDemand demand, CancellationToken ct)
    {
        var resolved = await ResolveAsync(demand, reference: null, party: null, allowShortfall: true, ct);
        return resolved.IsSuccess ? StockResult<StockPlan>.Success(resolved.Value!.Plan) : Failed<StockPlan>(resolved);
    }

    public async Task<StockResult<StockPlan>> ReserveAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct)
    {
        var resolved = await ResolveAsync(demand, reference, party, allowShortfall: false, ct);
        if (!resolved.IsSuccess) return Failed<StockPlan>(resolved);

        var plan = resolved.Value!;
        var user = userContextProvider.GetUserContext();
        var now = timeProvider.GetUtcNow();
        var lineId = reference.ReferenceLineId ?? reference.ReferenceId;

        foreach (var line in plan.Plan.Lines.OrderBy(l => l.BatchId).ThenBy(l => l.LocationId))
        {
            db.StockReservationEntries.Add(StockReservationEntryAggregate.Record(new StockReservationEntryDto(
                demand.BranchId, line.LocationId, demand.SkuId, line.BatchId, ReservationEntryKind.Reserve, line.Quantity, reference.ReferenceKind, reference.ReferenceId,
                lineId, party.CustomerId, party.CatId, plan.Overridden, plan.OverrideReason, user.UserId, now, user)));
        }

        EnlistOverrideAudit(plan, reference, user, now);
        return StockResult<StockPlan>.Success(plan.Plan);
    }

    /// <summary>Reserves ONE staff-chosen batch (the FEFO override on an order line). Inventory picks the locations -- the fullest first -- and then runs the
    /// ordinary explicit-plan path, so the SKU, branch, usability, free-stock, reason and audit rules are the ones every reservation obeys.</summary>
    public async Task<StockResult<StockPlan>> ReserveBatchAsync(StockBatchDemand demand, StockReference reference, StockParty party, CancellationToken ct)
    {
        var candidates = (await positions.GetCandidatesAsync(demand.BranchId, demand.SkuId, ct)).Where(c => c.BatchId == demand.BatchId).OrderByDescending(c => c.Free).ThenBy(c => c.LocationId).ToList();
        if (candidates.Count == 0)
            return Fail<StockPlan>(BusinessViolationCodes.BatchNotFound, "That batch is not held at this branch for this item.", nameof(demand.BatchId));

        var remaining = demand.Quantity;
        var picked = new List<(Guid BatchId, Guid LocationId, decimal Quantity)>();
        foreach (var candidate in candidates)
        {
            if (remaining <= 0m) break;
            var take = Math.Min(remaining, Math.Max(0m, candidate.Free));
            if (take <= 0m) continue;
            picked.Add((candidate.BatchId, candidate.LocationId, take));
            remaining -= take;
        }

        // Not enough free in the batch: hand the planner the whole quantity at the fullest location so it answers with its own usability / InsufficientStock refusal.
        if (remaining > 0m) { picked.Clear(); picked.Add((demand.BatchId, candidates[0].LocationId, demand.Quantity)); }

        return await ReserveAsync(
            new StockDemand(demand.BranchId, demand.SkuId, demand.Quantity, MovementKind.Sale, null, picked, demand.Reason), reference, party, ct);
    }

    public async Task<StockResult<IReadOnlyList<BatchUsability>>> GetBatchUsabilityAsync(Guid branchId, IReadOnlyCollection<Guid> batchIds, CancellationToken ct)
    {
        var facts = await positions.GetBatchFactsAsync(batchIds.Distinct().ToList(), ct);
        var today = await clock.LocalTodayAsync(branchId, ct);
        var result = new List<BatchUsability>();

        foreach (var batchId in batchIds.Distinct())
        {
            if (!facts.TryGetValue(batchId, out var batch) || batch.BranchId != branchId)
            {
                result.Add(new BatchUsability(batchId, Guid.Empty, string.Empty, BusinessViolationCodes.BatchNotFound, "That batch does not exist at this branch."));
                continue;
            }

            if (batch.Status == BatchStatus.Recalled)
            {
                result.Add(new BatchUsability(batchId, batch.SkuId, batch.LotNumber, BusinessViolationCodes.BatchRecalled, InventoryMessages.BatchNotUsable));
                continue;
            }

            var refusal = UsabilityRefusal(batch, today);
            result.Add(new BatchUsability(batchId, batch.SkuId, batch.LotNumber, refusal?.Code, refusal?.Message));
        }

        return StockResult<IReadOnlyList<BatchUsability>>.Success(result);
    }

    public async Task<StockResult<Unit>> ReleaseAsync(StockReference reference, CancellationToken ct)
    {
        var remaining = await positions.GetRemainingReservationsAsync(reference.ReferenceKind, reference.ReferenceId, reference.ReferenceLineId, ct);
        if (remaining.Count == 0) return StockResult<Unit>.Success(Unit.Value); // nothing held: releasing is idempotent

        var locations = await positions.GetLocationsAsync(remaining.Select(r => r.LocationId).Distinct().ToList(), ct);
        var user = userContextProvider.GetUserContext();
        var now = timeProvider.GetUtcNow();

        foreach (var held in remaining.OrderBy(r => r.BatchId).ThenBy(r => r.LocationId))
        {
            db.StockReservationEntries.Add(StockReservationEntryAggregate.Record(new StockReservationEntryDto(
                locations[held.LocationId].BranchId, held.LocationId, held.SkuId, held.BatchId, ReservationEntryKind.Release, -held.Quantity, reference.ReferenceKind,
                reference.ReferenceId, held.ReferenceLineId, null, null, FefoOverridden: false, OverrideReason: null, user.UserId, now, user)));
        }

        return StockResult<Unit>.Success(Unit.Value);
    }

    // ── consuming ───────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Consumes EXACTLY the remaining reservation of the reference as <c>Sale</c> entries. The batch must still be usable: a recalled or
    /// expired RESERVED batch cannot be consumed, so HO-ORD-001 cannot dispatch it (US-IMP-11 AC4, BR-14) -- it must release and re-reserve.</summary>
    public async Task<StockResult<IReadOnlyList<StockMovementLine>>> ConsumeReservationAsync(StockReference reference, StockParty party, CancellationToken ct)
    {
        var remaining = await positions.GetRemainingReservationsAsync(reference.ReferenceKind, reference.ReferenceId, reference.ReferenceLineId, ct);
        if (remaining.Count == 0)
            return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.ReservationNotFound, "Nothing is reserved for that reference.", "Reference");

        if (PartyRefusal(MovementKind.Sale, reference, party) is { } missing)
            return Fail<IReadOnlyList<StockMovementLine>>(missing.Code, missing.Message, missing.Property);

        var batches = await positions.GetBatchFactsAsync(remaining.Select(r => r.BatchId).Distinct().ToList(), ct);
        var locations = await positions.GetLocationsAsync(remaining.Select(r => r.LocationId).Distinct().ToList(), ct);
        var todays = new Dictionary<Guid, DateOnly>();
        var lines = new List<StockMovementLine>();

        foreach (var held in remaining)
        {
            var location = locations[held.LocationId];
            if (!StockLocationRules.Permits(location.Kind, MovementKind.Sale))
                return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.MovementNotPermittedAtLocation, $"A sale is not permitted at a {location.Kind.DisplayName} location.", "LocationId");

            if (!todays.TryGetValue(location.BranchId, out var today))
                todays[location.BranchId] = today = await clock.LocalTodayAsync(location.BranchId, ct);

            var batch = batches[held.BatchId];
            if (UsabilityRefusal(batch, today) is { } refusal)
                return Fail<IReadOnlyList<StockMovementLine>>(refusal.Code, refusal.Message, "BatchId");

            lines.Add(new StockMovementLine(batch.BatchId, batch.LotNumber, batch.ExpiresOn, held.LocationId, held.Quantity, FefoOverridden: false));
        }

        var user = userContextProvider.GetUserContext();
        var now = timeProvider.GetUtcNow();
        foreach (var (held, _) in remaining.Zip(lines).OrderBy(x => x.First.BatchId).ThenBy(x => x.First.LocationId))
        {
            db.StockLedgerEntries.Add(StockLedgerEntryAggregate.Record(new StockLedgerEntryDto(
                locations[held.LocationId].BranchId, held.LocationId, held.SkuId, held.BatchId, MovementKind.Sale, -held.Quantity, ConsumesReservation: true,
                reference.ReferenceKind, reference.ReferenceId, held.ReferenceLineId, Truncate(reference.ReferenceLabel, 60), party.CustomerId, party.CatId,
                FefoOverridden: false, OverrideReason: null, user.UserId, Note: null, now, user)));
        }

        return StockResult<IReadOnlyList<StockMovementLine>>.Success(lines);
    }

    /// <summary>Deducts <c>Purpose</c> (<c>Sale</c>, <c>Dispense</c>, <c>ClinicalUse</c> or <c>BoardingUse</c>) from usable, unreserved stock -- FEFO, or an
    /// explicit batch (HO-PHM-001's dispensing names the batch it gave).</summary>
    public async Task<StockResult<IReadOnlyList<StockMovementLine>>> DeductAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct)
    {
        var resolved = await ResolveAsync(demand, reference, party, allowShortfall: false, ct);
        if (!resolved.IsSuccess) return Failed<IReadOnlyList<StockMovementLine>>(resolved);

        var plan = resolved.Value!;
        var user = userContextProvider.GetUserContext();
        var now = timeProvider.GetUtcNow();

        foreach (var line in plan.Plan.Lines.OrderBy(l => l.BatchId).ThenBy(l => l.LocationId))
        {
            db.StockLedgerEntries.Add(StockLedgerEntryAggregate.Record(new StockLedgerEntryDto(
                demand.BranchId, line.LocationId, demand.SkuId, line.BatchId, demand.Purpose, -line.Quantity, ConsumesReservation: false, reference.ReferenceKind,
                reference.ReferenceId, reference.ReferenceLineId, Truncate(reference.ReferenceLabel, 60), party.CustomerId, party.CatId, plan.Overridden, plan.OverrideReason,
                user.UserId, Note: null, now, user)));
        }

        EnlistOverrideAudit(plan, reference, user, now);
        return StockResult<IReadOnlyList<StockMovementLine>>.Success(plan.Plan.Lines);
    }

    /// <summary>§8.8 rule 8: a return goes into the branch's QUARANTINE location only, for a batch the party actually received (else
    /// <c>ReturnNotTraceable</c>); a recalled batch may be returned. What happens to it next (restock, write off) is an inventory judgement on the
    /// quarantine screen, never something a consuming context asks for.</summary>
    public async Task<StockResult<IReadOnlyList<StockMovementLine>>> ReceiveReturnAsync(StockReturn stockReturn, StockReference reference, StockParty party, CancellationToken ct)
    {
        var sku = await skus.GetAsync(stockReturn.SkuId, ct);
        if (sku is null) return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.SkuNotFound, "That SKU does not exist.", nameof(stockReturn.SkuId));
        if (!StockQuantity.TryValidate(stockReturn.Quantity, sku.AllowsFractionalQuantity, out var quantityCode))
            return Fail<IReadOnlyList<StockMovementLine>>(quantityCode!, "The quantity must be greater than zero, whole unless the item is counted in fractions, with at most three decimal places.", nameof(stockReturn.Quantity));

        var batches = await positions.GetBatchFactsAsync([stockReturn.BatchId], ct);
        if (!batches.TryGetValue(stockReturn.BatchId, out var batch) || batch.SkuId != stockReturn.SkuId)
            return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.BatchNotFound, "That batch does not exist for that SKU.", nameof(stockReturn.BatchId));

        var quarantine = await positions.GetQuarantineLocationAsync(stockReturn.BranchId, ct);
        if (quarantine is null)
            return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.StockLocationNotFound, "This branch has no active quarantine location to receive the return.", "BranchId");

        if (!await positions.WasConsumedByPartyAsync(stockReturn.BatchId, party.CustomerId, party.CatId, ct))
            return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.ReturnNotTraceable, "That batch was not supplied to this customer or cat, so it cannot be returned.", nameof(stockReturn.BatchId));

        var user = userContextProvider.GetUserContext();
        db.StockLedgerEntries.Add(StockLedgerEntryAggregate.Record(new StockLedgerEntryDto(
            stockReturn.BranchId, quarantine.LocationId, stockReturn.SkuId, stockReturn.BatchId, MovementKind.Return, stockReturn.Quantity, ConsumesReservation: false,
            reference.ReferenceKind, reference.ReferenceId, reference.ReferenceLineId, Truncate(reference.ReferenceLabel, 60), party.CustomerId, party.CatId,
            FefoOverridden: false, OverrideReason: null, user.UserId, Truncate(stockReturn.Reason, InventoryRules.NoteMaxLength), timeProvider.GetUtcNow(), user)));

        return StockResult<IReadOnlyList<StockMovementLine>>.Success(
            [new StockMovementLine(batch.BatchId, batch.LotNumber, batch.ExpiresOn, quarantine.LocationId, stockReturn.Quantity, FefoOverridden: false)]);
    }

    // ── shortfall, correction, balances ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>US-CLN-15 AC2 -- a consumer could not deduct what the visit needed: a <c>Shortfall</c> alert is ENLISTED for the reorder list, and the
    /// consumer's own commit carries it. Advisory: it never fails the caller. The published contract is synchronous (<c>void</c>), so the location's branch
    /// is read with one primary-key lookup -- the single synchronous database call in this class (a deviation from the async-only rule, forced by the port).</summary>
    public void RecordShortfall(StockShortfall shortfall)
    {
        var user = userContextProvider.GetUserContext();
        var branchId = db.StockLocations.AsNoTracking().Where(l => l.Id == shortfall.LocationId).Select(l => l.BranchId).FirstOrDefault();

        var alert = new InventoryAlertAggregate(Guid.NewGuid());
        alert.Raise(new StockAlertRaiseDto(
            StockAlertKind.Shortfall, branchId, shortfall.SkuId, BatchId: null, shortfall.LocationId, ThresholdDays: null, shortfall.Quantity,
            ReferenceKind: null, shortfall.ReferenceId, DedupKey: null, timeProvider.GetUtcNow(), new InventoryActor(user, Guid.NewGuid())));
        db.InventoryAlerts.Add(alert);
    }

    /// <summary>A controlled-drug register correction (HO-PHM-001 C7) -- never a routine stock edit. Reason at least five characters; over
    /// <c>inventory.adjustmentApprovalThreshold</c> it also needs an approver other than the caller. ONE <c>BusinessEvent</c> audit entry names the
    /// caller's reference.</summary>
    public async Task<StockResult<IReadOnlyList<StockMovementLine>>> AdjustAsync(StockAdjustment adjustment, StockReference reference, StockParty party, CancellationToken ct)
    {
        var reason = adjustment.Reason?.Trim() ?? string.Empty;
        if (reason.Length < InventoryRules.ReasonMinLength || reason.Length > InventoryRules.NoteMaxLength)
            return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.AdjustmentNoteRequired, "Explain the correction in 5 to 500 characters.", nameof(adjustment.Reason));
        if (adjustment.QuantityDelta == 0m || decimal.Round(adjustment.QuantityDelta, 3) != adjustment.QuantityDelta)
            return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.InvalidQuantity, "The quantity must be non-zero with at most three decimal places.", nameof(adjustment.QuantityDelta));

        var batches = await positions.GetBatchFactsAsync([adjustment.BatchId], ct);
        if (!batches.TryGetValue(adjustment.BatchId, out var batch) || batch.SkuId != adjustment.SkuId)
            return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.BatchNotFound, "That batch does not exist for that SKU.", nameof(adjustment.BatchId));

        var locations = await positions.GetLocationsAsync([adjustment.LocationId], ct);
        if (!locations.TryGetValue(adjustment.LocationId, out var location) || !location.IsActive || location.BranchId != adjustment.BranchId)
            return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.StockLocationNotFound, "That location does not exist at this branch.", nameof(adjustment.LocationId));

        var kind = adjustment.QuantityDelta > 0m ? MovementKind.AdjustmentIn : MovementKind.AdjustmentOut;
        if (!StockLocationRules.Permits(location.Kind, kind))
            return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.MovementNotPermittedAtLocation, $"An adjustment is not permitted at a {location.Kind.DisplayName} location.", nameof(adjustment.LocationId));

        var user = userContextProvider.GetUserContext();
        var costs = await positions.GetUnitCostsAsync([adjustment.BatchId], ct);
        var value = Math.Abs(adjustment.QuantityDelta) * (costs.TryGetValue(adjustment.BatchId, out var cost) ? cost.UnitCostBase : 0m);
        if (value > settings.GetDecimal(SettingKeys.InventoryAdjustmentApprovalThreshold, adjustment.BranchId))
        {
            if (adjustment.ApprovedByStaffId is null)
                return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.AdjustmentNotPending, "This correction is worth more than the approval threshold and needs an approver.", nameof(adjustment.ApprovedByStaffId));
            if (adjustment.ApprovedByStaffId == user.UserId)
                return Fail<IReadOnlyList<StockMovementLine>>(BusinessViolationCodes.ApproverMustDiffer, "A correction must be approved by someone other than the person who makes it.", nameof(adjustment.ApprovedByStaffId));
        }

        var now = timeProvider.GetUtcNow();
        db.StockLedgerEntries.Add(StockLedgerEntryAggregate.Record(new StockLedgerEntryDto(
            adjustment.BranchId, adjustment.LocationId, adjustment.SkuId, adjustment.BatchId, kind, adjustment.QuantityDelta, ConsumesReservation: false, reference.ReferenceKind,
            reference.ReferenceId, reference.ReferenceLineId, Truncate(reference.ReferenceLabel, 60), party.CustomerId, party.CatId, FefoOverridden: false, OverrideReason: null,
            user.UserId, Truncate(reason, InventoryRules.NoteMaxLength), now, user)));

        EnlistAudit(
            reference.ReferenceKind.DisplayName, reference.ReferenceId, "StockCorrectedThroughLedger", reason, user, now,
            before: null, after: new { adjustment.BatchId, adjustment.LocationId, adjustment.QuantityDelta, Approver = adjustment.ApprovedByStaffId });

        return StockResult<IReadOnlyList<StockMovementLine>>.Success(
            [new StockMovementLine(batch.BatchId, batch.LotNumber, batch.ExpiresOn, adjustment.LocationId, adjustment.QuantityDelta, FefoOverridden: false)]);
    }

    /// <summary>Per-batch balances across every location of the branch, with RESERVED separate so a register reconciliation (US-PHM-07) can tell
    /// "missing" from "spoken for". Read-only.</summary>
    public async Task<StockResult<IReadOnlyList<BatchBalance>>> GetBranchBalancesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        var ids = skuIds.Distinct().ToList();
        var rows = await (
            from p in db.StockPositions.AsNoTracking()
            join b in db.Batches.AsNoTracking() on p.BatchId equals b.Id
            join l in db.StockLocations.AsNoTracking() on p.LocationId equals l.Id
            where l.BranchId == branchId && ids.Contains(p.SkuId) && (p.OnHand > 0m || p.Reserved > 0m)
            orderby b.ExpiresOn == null, b.ExpiresOn, b.ReceivedOn, l.Code
            select new { p.SkuId, p.BatchId, b.LotNumber, b.ExpiresOn, p.LocationId, p.OnHand, p.Reserved }).ToListAsync(ct);

        return StockResult<IReadOnlyList<BatchBalance>>.Success(
            rows.Select(r => new BatchBalance(r.SkuId, r.BatchId, r.LotNumber, r.ExpiresOn, r.LocationId, r.OnHand, r.Reserved)).ToList());
    }

    /// <summary>The batches the branch's ACTIVE clinic dispensary holds for the SKUs (on-hand or reserved above zero), with each batch's status. Read-only.
    /// Added 2026-10-09 so HO-PHM-001 needs no Inventory-internal reader.</summary>
    public async Task<StockResult<IReadOnlyList<DispensaryBatch>>> GetDispensaryBatchesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct)
    {
        var result = new List<DispensaryBatch>();
        foreach (var skuId in skuIds.Distinct())
        {
            foreach (var c in await positions.GetCandidatesAsync(branchId, skuId, ct))
            {
                if (c.LocationKind != StockLocationKind.ClinicDispensary) continue;
                result.Add(new DispensaryBatch(skuId, c.BatchId, c.LotNumber, c.ExpiresOn, c.BatchStatus, c.LocationId, c.OnHand, c.Reserved));
            }
        }

        return StockResult<IReadOnlyList<DispensaryBatch>>.Success(result);
    }

    /// <summary>The distinct SKUs with stock on hand at the branch's active clinic dispensary. Read-only. Added 2026-10-13 for HO-EMR-001's dispensary item list.</summary>
    public async Task<StockResult<IReadOnlyList<Guid>>> GetDispensarySkuIdsAsync(Guid branchId, CancellationToken ct)
    {
        var ids = await (
            from p in db.StockPositions.AsNoTracking()
            join l in db.StockLocations.AsNoTracking() on p.LocationId equals l.Id
            where l.BranchId == branchId && l.Kind == StockLocationKind.ClinicDispensary && l.IsActive && p.OnHand > 0m
            select p.SkuId).Distinct().ToListAsync(ct);

        return StockResult<IReadOnlyList<Guid>>.Success(ids);
    }

    /// <summary>The branch's active clinic dispensary location. Read-only.</summary>
    public async Task<StockResult<DispensaryLocation>> GetClinicDispensaryAsync(Guid branchId, CancellationToken ct)
    {
        var row = await db.StockLocations.AsNoTracking()
            .Where(l => l.BranchId == branchId && l.Kind == StockLocationKind.ClinicDispensary && l.IsActive)
            .OrderBy(l => l.Code)
            .Select(l => new { l.Id, l.Code })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? Fail<DispensaryLocation>("DispensaryNotFound", "This branch has no active clinic dispensary.", "BranchId")
            : StockResult<DispensaryLocation>.Success(new DispensaryLocation(row.Id, row.Code));
    }

    // ── resolving a demand into a plan: read the facts here, DECIDE in the pure StockDemandPlanner ─────────────────────────────────

    private async Task<StockResult<PlannedDemand>> ResolveAsync(StockDemand demand, StockReference? reference, StockParty? party, bool allowShortfall, CancellationToken ct)
    {
        var sku = await skus.GetAsync(demand.SkuId, ct);
        var today = await clock.LocalTodayAsync(demand.BranchId, ct);
        var candidates = await positions.GetCandidatesAsync(demand.BranchId, demand.SkuId, ct);

        var named = (demand.LocationIds ?? []).Concat(demand.ExplicitLines?.Select(l => l.LocationId) ?? []).Distinct().ToList();
        var locations = named.Count == 0 ? new Dictionary<Guid, StockLocationFact>() : (await positions.GetLocationsAsync(named, ct)).ToDictionary(l => l.Key, l => l.Value);

        return planner.Plan(demand, reference, party, sku, candidates, locations, today, allowShortfall);
    }

    // ── rule 4 and the party stamp (the pure rules live in StockDemandPlanner) ──────────────────────────────────────────────────

    private static (string Code, string Message)? UsabilityRefusal(BatchFact batch, DateOnly branchToday) =>
        StockDemandPlanner.UsabilityRefusal(batch.Status, batch.ExpiresOn, batch.LotNumber, branchToday);

    private static (string Code, string Message, string Property)? PartyRefusal(MovementKind purpose, StockReference reference, StockParty party) =>
        StockDemandPlanner.PartyRefusal(purpose, reference.ReferenceKind, party);

    // ── audit and small helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>ADR-102: an explicit plan that differs from FEFO enlists ONE <c>BusinessEvent</c> audit entry beside the movement, in the same commit
    /// (a recorded rule-1 exception): entity = the reference, before = FEFO's plan, after = the chosen plan.</summary>
    private void EnlistOverrideAudit(PlannedDemand plan, StockReference reference, UserContext user, DateTimeOffset now)
    {
        if (!plan.Overridden) return;

        EnlistAudit(
            reference.ReferenceKind.DisplayName, reference.ReferenceId, "FefoOverridden", plan.OverrideReason!, user, now,
            before: plan.FefoLines.Select(l => new { l.BatchId, l.LocationId, l.Quantity }).ToList(),
            after: plan.Plan.Lines.Select(l => new { l.BatchId, l.LocationId, l.Quantity }).ToList());
    }

    private void EnlistAudit(string entityType, Guid entityId, string action, string reason, UserContext user, DateTimeOffset now, object? before, object? after)
    {
        var entry = new AuditEntryAggregate(Guid.NewGuid());
        entry.Record(new AuditEntryDto(
            entityType, entityId, action, reason, user.UserId, now,
            before is null ? null : JsonSerializer.Serialize(before), after is null ? null : JsonSerializer.Serialize(after),
            user, Guid.NewGuid(), AuditCategory.BusinessEvent, clientIp.GetClientIpAddress()));
        db.Add(entry);
    }

    private static StockResult<T> Fail<T>(string code, string message, string property) => StockResult<T>.Failure(code, message, property);

    private static StockResult<TOut> Failed<TOut>(StockResult<PlannedDemand> failure) => StockResult<TOut>.Failure(failure.ViolationCode!, failure.Message!, failure.PropertyName);
}
