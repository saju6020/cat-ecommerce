using CatEcommerce.Domain.DomainServices;
using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Infrastructure.Fakes.Inventory;

/// <summary>
/// SUPERSEDED by <c>Inventory/EfStockLedger</c> (HO-IMP-001 lands: <c>InventoryModule</c> no longer registers this). Kept for consumers' own handler
/// unit tests that want a ledger that refuses everything; it is never registered in the host.
///
/// Originally:
/// Fail-closed: every mutating/planning method reports <c>InsufficientStock</c> (HO-IMP-001 §10.4's
/// own code name). No consumer can build on stock existing before Inventory is real -- the
/// precedent is HO-SHP-001's own already-specified deny-all diet stub (ADR-084), applied here to
/// stock instead. <see cref="GetBranchBalancesAsync"/> is the one read-only exception: it succeeds
/// with an empty list, which is the truthful answer ("no balances exist") rather than a fabricated
/// denial. Literal violation-code strings are used rather than a
/// <c>BusinessViolationCodes.Inventory.cs</c> partial file, so this fake never collides with the
/// file HO-IMP-001 creates when it actually lands (ADR-341).
/// </summary>
public sealed class FakeStockLedger : IStockLedger
{
    private const string InsufficientStock = "InsufficientStock";
    private const string Message = "Inventory is not built yet -- HO-IMP-001. No stock can be planned, reserved or moved.";

    public Task<StockResult<StockPlan>> PlanAsync(StockDemand demand, CancellationToken ct) =>
        Task.FromResult(StockResult<StockPlan>.Failure(InsufficientStock, Message));

    public Task<StockResult<StockPlan>> ReserveAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct) =>
        Task.FromResult(StockResult<StockPlan>.Failure(InsufficientStock, Message));

    public Task<StockResult<Unit>> ReleaseAsync(StockReference reference, CancellationToken ct) =>
        Task.FromResult(StockResult<Unit>.Failure(InsufficientStock, Message));

    public Task<StockResult<IReadOnlyList<StockMovementLine>>> ConsumeReservationAsync(StockReference reference, StockParty party, CancellationToken ct) =>
        Task.FromResult(StockResult<IReadOnlyList<StockMovementLine>>.Failure(InsufficientStock, Message));

    public Task<StockResult<IReadOnlyList<StockMovementLine>>> DeductAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct) =>
        Task.FromResult(StockResult<IReadOnlyList<StockMovementLine>>.Failure(InsufficientStock, Message));

    public Task<StockResult<IReadOnlyList<StockMovementLine>>> ReceiveReturnAsync(StockReturn stockReturn, StockReference reference, StockParty party, CancellationToken ct) =>
        Task.FromResult(StockResult<IReadOnlyList<StockMovementLine>>.Failure(InsufficientStock, Message));

    public Task<StockResult<StockPlan>> ReserveBatchAsync(StockBatchDemand demand, StockReference reference, StockParty party, CancellationToken ct) =>
        Task.FromResult(StockResult<StockPlan>.Failure(InsufficientStock, Message));

    public Task<StockResult<IReadOnlyList<BatchUsability>>> GetBatchUsabilityAsync(Guid branchId, IReadOnlyCollection<Guid> batchIds, CancellationToken ct) =>
        Task.FromResult(StockResult<IReadOnlyList<BatchUsability>>.Failure(InsufficientStock, Message));

    public void RecordShortfall(StockShortfall shortfall)
    {
        // Nothing to enlist yet -- no ledger table exists. Deliberately a no-op rather than a throw:
        // recording a shortfall is advisory (a reorder signal), never a reason to fail a caller's
        // command.
    }

    public Task<StockResult<IReadOnlyList<StockMovementLine>>> AdjustAsync(StockAdjustment adjustment, StockReference reference, StockParty party, CancellationToken ct) =>
        Task.FromResult(StockResult<IReadOnlyList<StockMovementLine>>.Failure(InsufficientStock, Message));

    public Task<StockResult<IReadOnlyList<BatchBalance>>> GetBranchBalancesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct) =>
        Task.FromResult(StockResult<IReadOnlyList<BatchBalance>>.Success(Array.Empty<BatchBalance>()));

    public Task<StockResult<IReadOnlyList<DispensaryBatch>>> GetDispensaryBatchesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct) =>
        Task.FromResult(StockResult<IReadOnlyList<DispensaryBatch>>.Success(Array.Empty<DispensaryBatch>()));

    public Task<StockResult<DispensaryLocation>> GetClinicDispensaryAsync(Guid branchId, CancellationToken ct) =>
        Task.FromResult(StockResult<DispensaryLocation>.Failure("DispensaryNotFound", "Inventory is not built yet -- HO-IMP-001. This branch has no clinic dispensary."));
}
