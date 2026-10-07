using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// What a caller wants moved -- HO-IMP-001 §8.8's <c>IStockLedger</c>. Shape confirmed identically
/// by both the owner's informal field list (§8.8's table) and HO-PHM-001 §8.12's concrete code
/// sample, so there is no disagreement to record for this one.
/// </summary>
public sealed record StockDemand(
    Guid BranchId,
    Guid SkuId,
    decimal Quantity,
    MovementKind Purpose,
    IReadOnlyList<Guid>? LocationIds,
    IReadOnlyList<(Guid BatchId, Guid LocationId, decimal Quantity)>? ExplicitLines,
    string? OverrideReason);
