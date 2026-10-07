namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Owned by HO-IMP-001 §8.7/§10.1 (ADR-098). The kind of every row on the stock ledger, its sign
/// fixed by the aggregate's own rule (<c>ck_stock_ledger_sign</c>), never by the caller.
/// <c>MovementKindRules.IsConsuming</c> treats <see cref="Sale"/>, <see cref="Dispense"/>,
/// <see cref="ClinicalUse"/> and <see cref="BoardingUse"/> as the consuming kinds.
///
/// Created here (Step 1 of the contracts pass) because <c>IStockLedger</c>'s <c>StockDemand.Purpose</c>
/// needs this type before HO-IMP-001 is built. HO-IMP-001 §10.1 says "the 14 of §8.7"; §8.7's own
/// table lists the 13 members below -- recorded as a documentation discrepancy in
/// docs/handover/CONTRACTS.md, not resolved by inventing a 14th member.
/// </summary>
public sealed class MovementKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly MovementKind Receipt = new(1, nameof(Receipt));
    public static readonly MovementKind TransferOut = new(2, nameof(TransferOut));
    public static readonly MovementKind TransferIn = new(3, nameof(TransferIn));
    public static readonly MovementKind AdjustmentIn = new(4, nameof(AdjustmentIn));
    public static readonly MovementKind AdjustmentOut = new(5, nameof(AdjustmentOut));
    public static readonly MovementKind CountGain = new(6, nameof(CountGain));
    public static readonly MovementKind CountLoss = new(7, nameof(CountLoss));
    public static readonly MovementKind Wastage = new(8, nameof(Wastage));
    public static readonly MovementKind Sale = new(9, nameof(Sale));
    public static readonly MovementKind Dispense = new(10, nameof(Dispense));
    public static readonly MovementKind ClinicalUse = new(11, nameof(ClinicalUse));
    public static readonly MovementKind BoardingUse = new(12, nameof(BoardingUse));
    public static readonly MovementKind Return = new(13, nameof(Return));
}
