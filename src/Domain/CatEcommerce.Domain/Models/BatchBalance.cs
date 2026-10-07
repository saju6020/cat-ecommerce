namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-IMP-001 §8.8 (added 2026-09-23, README C-2 follow-up, closes HO-PHM-001 OQ-16). Per-batch
/// balances across every location of the branch, for reconciling a register against stock.
/// </summary>
public sealed record BatchBalance(
    Guid SkuId,
    Guid BatchId,
    string LotNumber,
    DateOnly? ExpiresOn,
    Guid LocationId,
    decimal OnHand,
    decimal Reserved);
