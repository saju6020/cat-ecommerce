using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>HO-IMP-001 §8.7 -- the sign of every <see cref="MovementKind"/>, fixed by the kind, never by the caller
/// (<c>ck_stock_ledger_sign</c> is written from these lists).</summary>
public static class MovementKindRules
{
    public static readonly IReadOnlyList<MovementKind> Incoming =
    [
        MovementKind.Receipt, MovementKind.TransferIn, MovementKind.AdjustmentIn, MovementKind.CountGain, MovementKind.Return
    ];

    public static readonly IReadOnlyList<MovementKind> Outgoing =
    [
        MovementKind.TransferOut, MovementKind.AdjustmentOut, MovementKind.CountLoss, MovementKind.Wastage,
        MovementKind.Sale, MovementKind.Dispense, MovementKind.ClinicalUse, MovementKind.BoardingUse
    ];

    /// <summary>The kinds that hand stock to a customer, a cat or a clinical use (§8.7).</summary>
    public static readonly IReadOnlyList<MovementKind> Consuming =
        [MovementKind.Sale, MovementKind.Dispense, MovementKind.ClinicalUse, MovementKind.BoardingUse];

    public static bool IsConsuming(MovementKind kind) => Consuming.Contains(kind);

    /// <summary>+1 for an incoming kind, -1 for an outgoing one.</summary>
    public static int Sign(MovementKind kind) =>
        Incoming.Contains(kind) ? 1 : Outgoing.Contains(kind) ? -1 : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown movement kind.");

    /// <summary>True when <paramref name="delta"/> is non-zero and carries the sign the kind demands.</summary>
    public static bool IsValidDelta(MovementKind kind, decimal delta) => delta != 0m && Math.Sign(delta) == Sign(kind);

    /// <summary>The kind an adjustment of this direction writes (§8.10); <c>Wastage</c> for the disposal reasons.</summary>
    public static MovementKind ForAdjustment(AdjustmentReason reason, decimal delta) =>
        reason == AdjustmentReason.Expired || reason == AdjustmentReason.RecallDisposal
            ? MovementKind.Wastage
            : delta > 0 ? MovementKind.AdjustmentIn : MovementKind.AdjustmentOut;
}
