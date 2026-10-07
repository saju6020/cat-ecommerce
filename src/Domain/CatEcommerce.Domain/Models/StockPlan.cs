namespace CatEcommerce.Domain.Models;

/// <summary>
/// The result of <c>IStockLedger.PlanAsync</c> -- HO-IMP-001 §8.8, §8.9's <c>FefoAllocator</c>
/// (<c>Allocate(...) → Allocation(lines[], shortfall)</c>). No handover gives <c>StockPlan</c> as a
/// full record (only "lines of BatchId, LotNumber, ExpiresOn?, LocationId, Quantity, FefoOverridden"
/// -- the same shape as <see cref="StockMovementLine"/>); <see cref="Shortfall"/> is inferred from
/// <c>FefoAllocator</c>'s own return shape. Flagged as inferred in docs/handover/CONTRACTS.md.
/// </summary>
public sealed record StockPlan(IReadOnlyList<StockMovementLine> Lines, decimal Shortfall);
