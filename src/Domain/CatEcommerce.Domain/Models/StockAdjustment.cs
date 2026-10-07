namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-IMP-001 §8.8 (added 2026-09-23, README C-2 follow-up, closes HO-PHM-001 OQ-17). Correcting a
/// discrepancy another context has already proved -- never a routine stock edit.
/// </summary>
public sealed record StockAdjustment(
    Guid BranchId,
    Guid SkuId,
    Guid BatchId,
    Guid LocationId,
    decimal QuantityDelta,
    string Reason,
    Guid? ApprovedByStaffId);
