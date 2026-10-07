using CatEcommerce.Domain.Models;
using CatEcommerce.Shared.Common.Helpers;

namespace CatEcommerce.Domain.DomainServices;

/// <summary>
/// Owned by HO-IMP-001 §8.8 (ADR-103). Every movement in the solution goes through one class
/// behind this port -- <c>EfStockLedger</c> (Infrastructure), not built in this contracts pass.
/// Every method validates, then ENLISTS rows in the scoped DbContext without saving; the caller's
/// own repository save commits them with its aggregate (F23). Plan and read methods write nothing.
/// The port never throws for a business rule -- it returns <see cref="StockResult{T}"/>.
///
/// Binding on every consumer (HO-ORD-001, HO-EMR-001, HO-PHM-001, HO-SRG-001, HO-BRD-001): call in
/// the command handler before the aggregate save; never from an event handler; always pass the
/// reference and the party; map a failure to <c>400</c> and the commit-time <c>23514</c> to
/// <c>409</c>.
///
/// Until HO-IMP-001 lands, <c>Fakes/Inventory/FakeStockLedger</c> answers every method with a
/// fail-closed <see cref="StockResult{T}"/> failure (<c>InsufficientStock</c>) -- see
/// docs/handover/CONTRACTS.md. No consumer can build on a false assumption that stock exists before
/// Inventory is real.
/// </summary>
public interface IStockLedger
{
    Task<StockResult<StockPlan>> PlanAsync(StockDemand demand, CancellationToken ct);

    Task<StockResult<StockPlan>> ReserveAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct);

    /// <summary>All remaining reservation for the reference (or one line).</summary>
    Task<StockResult<Unit>> ReleaseAsync(StockReference reference, CancellationToken ct);

    Task<StockResult<IReadOnlyList<StockMovementLine>>> ConsumeReservationAsync(StockReference reference, StockParty party, CancellationToken ct);

    Task<StockResult<IReadOnlyList<StockMovementLine>>> DeductAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct);

    Task<StockResult<IReadOnlyList<StockMovementLine>>> ReceiveReturnAsync(StockReturn stockReturn, StockReference reference, StockParty party, CancellationToken ct);

    /// <summary>US-CLN-15 AC2 → a Shortfall alert, enlisted (not an event).</summary>
    void RecordShortfall(StockShortfall shortfall);

    /// <summary>
    /// Added 2026-09-23, README C-2 follow-up (closes HO-PHM-001 OQ-17). A controlled-drug register
    /// correction, never a routine stock edit.
    /// </summary>
    Task<StockResult<IReadOnlyList<StockMovementLine>>> AdjustAsync(StockAdjustment adjustment, StockReference reference, StockParty party, CancellationToken ct);

    /// <summary>
    /// Added 2026-09-23, README C-2 follow-up (closes HO-PHM-001 OQ-16). Per-batch balances across
    /// every location of the branch. Read-only.
    /// </summary>
    Task<StockResult<IReadOnlyList<BatchBalance>>> GetBranchBalancesAsync(Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct);
}
