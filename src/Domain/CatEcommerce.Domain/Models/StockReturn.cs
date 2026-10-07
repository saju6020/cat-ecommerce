namespace CatEcommerce.Domain.Models;

/// <summary>
/// The parameter of <c>IStockLedger.ReceiveReturnAsync</c> (HO-IMP-001 §8.8). <b>No handover gives
/// this type's exact shape</b> -- every mention (HO-IMP-001 §8.8 rule 8, §11.3's DTO list,
/// HO-ORD-001 §8.13, HO-EMR-001 IMP-7) names the method and its behaviour ("writes <c>Return</c>
/// into the branch's <c>Quarantine</c> location only, for a batch the party actually received")
/// without ever writing out <c>StockReturn</c>'s fields. Recorded as a gap in
/// docs/handover/CONTRACTS.md. This shape is the minimum inferable from rule 8's prose: which batch
/// is being returned, how much, and why; <c>BranchId</c>/<c>SkuId</c> match every other DTO on this
/// port. The owner (HO-IMP-001) may change this without notice when it lands.
/// </summary>
public sealed record StockReturn(
    Guid BranchId,
    Guid SkuId,
    Guid BatchId,
    decimal Quantity,
    string? Reason);
