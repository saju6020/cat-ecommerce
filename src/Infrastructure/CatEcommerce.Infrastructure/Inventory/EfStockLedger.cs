using CatEcommerce.Domain.Aggregates.Inventory;
using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Infrastructure.Persistence;
using CatEcommerce.Shared.Common.Constants;
using CatEcommerce.Shared.Common.Enums;
using CatEcommerce.Shared.Common.Helpers;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Common.Security;

namespace CatEcommerce.Infrastructure.Inventory;

/// <summary>
/// HO-IMP-001 §8.8 / §11.4 / ADR-103 -- EVERY movement in the solution goes through this one class. It validates, then adds ledger (and
/// reservation) entries to the scoped <see cref="CatEcommerceDbContext"/> <b>without saving</b>: the caller's own repository save commits them with
/// its aggregate (F23), so a movement and the thing that caused it are one commit. <b>It never calls <c>SaveChangesAsync</c>.</b>
///
/// This file is the writer half (<see cref="IInventoryLedgerWriter"/>, used only by Inventory's own handlers). The consumer half
/// (<see cref="IStockLedger"/>) is <c>EfStockLedger.Consumer.cs</c>; both are served by ONE Scoped instance (§12.7) so a unit of work shares
/// one view of its own enlisted rows.
/// </summary>
public sealed partial class EfStockLedger(
    CatEcommerceDbContext db,
    IStockPositionReader positions,
    ISkuDirectory skus,
    IInventoryClock clock,
    StockDemandPlanner planner,
    IBusinessSettings settings,
    IClientIpAccessor clientIp,
    IUserContextProvider userContextProvider,
    TimeProvider timeProvider) : IInventoryLedgerWriter
{
    public async Task<StockResult<Unit>> AppendAsync(IReadOnlyList<StockMovementDraft> drafts, CancellationToken ct)
    {
        if (drafts.Count == 0) return StockResult<Unit>.Success(Unit.Value);

        var facts = await LoadFactsAsync(drafts.Select(d => d.BatchId), drafts.Select(d => d.LocationId), ct);

        for (var i = 0; i < drafts.Count; i++)
        {
            if (Check(drafts[i], i, facts) is { } failure) return failure;
        }

        var user = userContextProvider.GetUserContext();
        var now = timeProvider.GetUtcNow();

        // A deterministic order: concurrent units of work that touch the same positions lock them in the same sequence, so they queue on the
        // row lock instead of deadlocking.
        foreach (var draft in drafts.OrderBy(d => d.BatchId).ThenBy(d => d.LocationId).ThenBy(d => d.Kind.Value))
        {
            db.StockLedgerEntries.Add(StockLedgerEntryAggregate.Record(new StockLedgerEntryDto(
                draft.BranchId, draft.LocationId, draft.SkuId, draft.BatchId, draft.Kind, draft.QuantityDelta, ConsumesReservation: false,
                draft.ReferenceKind, draft.ReferenceId, draft.ReferenceLineId, Truncate(draft.ReferenceLabel, 60), CustomerId: null, CatId: null,
                FefoOverridden: false, OverrideReason: null, PerformedBy: user.UserId, Truncate(draft.Note, InventoryRules.NoteMaxLength), now, user)));
        }

        return StockResult<Unit>.Success(Unit.Value);
    }

    private StockResult<Unit>? Check(StockMovementDraft draft, int index, LedgerFacts facts)
    {
        var property = $"Drafts[{index}]";

        if (MovementKindRules.IsConsuming(draft.Kind) || draft.Kind == MovementKind.Return)
            return StockResult<Unit>.Failure(BusinessViolationCodes.MovementKindNotAllowed,
                $"A {draft.Kind.DisplayName} movement is made through IStockLedger, never the inventory writer.", $"{property}.Kind");

        if (!MovementKindRules.IsValidDelta(draft.Kind, draft.QuantityDelta) || decimal.Round(draft.QuantityDelta, 3) != draft.QuantityDelta)
            return StockResult<Unit>.Failure(BusinessViolationCodes.InvalidQuantity,
                "A quantity must be non-zero, have at most three decimal places and carry the sign its movement kind demands.", $"{property}.QuantityDelta");

        if (!facts.Batches.TryGetValue(draft.BatchId, out var batch) || batch.SkuId != draft.SkuId)
            return StockResult<Unit>.Failure(BusinessViolationCodes.BatchNotFound, "That batch does not exist for that SKU.", $"{property}.BatchId");

        if (!facts.Locations.TryGetValue(draft.LocationId, out var location) || !location.IsActive || location.BranchId != draft.BranchId)
            return StockResult<Unit>.Failure(BusinessViolationCodes.StockLocationNotFound, "That location does not exist at this branch.", $"{property}.LocationId");

        if (!StockLocationRules.Permits(location.Kind, draft.Kind))
            return StockResult<Unit>.Failure(BusinessViolationCodes.MovementNotPermittedAtLocation,
                $"A {draft.Kind.DisplayName} movement is not permitted at a {location.Kind.DisplayName} location.", $"{property}.LocationId");

        return null;
    }

    // ── facts: the database PLUS the rows this unit of work has already enlisted but not saved ──────────────────────────────────────

    private sealed record LedgerFacts(IReadOnlyDictionary<Guid, BatchFact> Batches, IReadOnlyDictionary<Guid, StockLocationFact> Locations);

    private async Task<LedgerFacts> LoadFactsAsync(IEnumerable<Guid> batchIds, IEnumerable<Guid> locationIds, CancellationToken ct)
    {
        var wantedBatches = batchIds.Distinct().ToList();
        var batches = new Dictionary<Guid, BatchFact>();

        // A goods receipt enlists its NEW batches and then asks for their ledger entries in the same unit of work: those batches are only in
        // the change tracker, not yet in the database.
        foreach (var entry in db.ChangeTracker.Entries<BatchAggregate>().Where(e => e.State == EntityState.Added))
        {
            var batch = entry.Entity;
            if (wantedBatches.Contains(batch.Id))
                batches[batch.Id] = new BatchFact(batch.Id, batch.SkuId, batch.BranchId, batch.LotNumber, batch.ExpiresOn, batch.ReceivedOn, batch.Status);
        }

        var missing = wantedBatches.Where(id => !batches.ContainsKey(id)).ToList();
        foreach (var (id, fact) in await positions.GetBatchFactsAsync(missing, ct)) batches[id] = fact;

        return new LedgerFacts(batches, await positions.GetLocationsAsync(locationIds.Distinct().ToList(), ct));
    }

    private static string? Truncate(string? text, int max) => text is null || text.Length <= max ? text : text[..max];
}
