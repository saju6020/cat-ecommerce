using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Domain.Models;

/// <summary>
/// HO-IMP-001 §8.8 (amended 2026-10-09 for HO-PHM-001, ADR-103). One batch held at a branch's CLINIC DISPENSARY, with Inventory's own batch status. The published
/// read behind <see cref="DomainServices.IStockLedger.GetDispensaryBatchesAsync"/>; Pharmacy never reads Inventory's positions, batches or locations itself.
/// </summary>
public sealed record DispensaryBatch(
    Guid SkuId, Guid BatchId, string LotNumber, DateOnly? ExpiresOn, BatchStatus BatchStatus, Guid LocationId, decimal OnHand, decimal Reserved)
{
    public decimal Free => OnHand - Reserved;
}

/// <summary>HO-IMP-001 §8.8 (amended 2026-10-09). The branch's active clinic dispensary location, from <see cref="DomainServices.IStockLedger.GetClinicDispensaryAsync"/>.</summary>
public sealed record DispensaryLocation(Guid LocationId, string Code);
