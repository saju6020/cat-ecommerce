using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Helpers;

/// <summary>
/// HO-IMP-001 §8.3 -- what may happen where, as ONE table (ADR-109), tested exhaustively. Anything the
/// table does not allow is <c>MovementNotPermittedAtLocation</c>. OQ-5: fixed in code.
/// </summary>
public static class StockLocationRules
{
    private static readonly IReadOnlyList<MovementKind> Everywhere =
    [
        MovementKind.Receipt, MovementKind.TransferIn, MovementKind.TransferOut, MovementKind.AdjustmentIn,
        MovementKind.AdjustmentOut, MovementKind.CountGain, MovementKind.CountLoss, MovementKind.Wastage
    ];

    private static readonly IReadOnlyDictionary<StockLocationKind, IReadOnlyList<MovementKind>> Table =
        new Dictionary<StockLocationKind, IReadOnlyList<MovementKind>>
        {
            [StockLocationKind.ShopFloor] = [.. Everywhere, MovementKind.Sale],
            [StockLocationKind.Warehouse] = [.. Everywhere, MovementKind.Sale],
            [StockLocationKind.ClinicDispensary] = [.. Everywhere, MovementKind.Dispense, MovementKind.ClinicalUse],
            [StockLocationKind.BoardingStore] = [.. Everywhere, MovementKind.BoardingUse],
            [StockLocationKind.Quarantine] = [.. Everywhere, MovementKind.Return],
        };

    public static bool Permits(StockLocationKind kind, MovementKind movement) => Table[kind].Contains(movement);

    public static IReadOnlyList<MovementKind> PermittedMovements(StockLocationKind kind) => Table[kind];

    /// <summary>Whether stock here counts toward retail sellable stock (§8.3's last column).</summary>
    public static bool CountsTowardRetailSellable(StockLocationKind kind) =>
        kind == StockLocationKind.ShopFloor || kind == StockLocationKind.Warehouse;

    /// <summary>§8.9 step 2: <c>Warehouse</c> 1, <c>ShopFloor</c> 2, the rest 3.</summary>
    public static int FefoPriority(StockLocationKind kind) =>
        kind == StockLocationKind.Warehouse ? 1 : kind == StockLocationKind.ShopFloor ? 2 : 3;
}
