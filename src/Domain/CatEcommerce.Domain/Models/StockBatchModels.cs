namespace CatEcommerce.Domain.Models;

/// <summary>
/// Added 2026-10-09 (HO-ORD-001 architecture-gate fix, owner decision 2). A reservation of ONE staff-chosen batch for a SKU at a branch. The caller
/// names the batch and the reason; <b>Inventory chooses the locations</b> (the fullest first) and validates the batch (right SKU, held at the branch,
/// usable, enough free stock). Used for the FEFO override on an order line (US-IMP-07 AC2).
/// </summary>
public sealed record StockBatchDemand(Guid BranchId, Guid SkuId, decimal Quantity, Guid BatchId, string Reason);

/// <summary>
/// Added 2026-10-09 (owner decision 2). Whether a batch may still be sold, as Inventory judges it in the branch's local date. <see cref="RefusalCode"/> is
/// null when usable, else <c>BatchRecalled</c>, <c>BatchNotUsable</c> or <c>BatchExpired</c>; <see cref="Message"/> is Inventory's wording.
/// </summary>
public sealed record BatchUsability(Guid BatchId, Guid SkuId, string LotNumber, string? RefusalCode, string? Message)
{
    public bool IsUsable => RefusalCode is null;

    public bool IsRecalled => RefusalCode == "BatchRecalled";
}
