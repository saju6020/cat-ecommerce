namespace CatEcommerce.Shared.Common.Enums;

/// <summary>HO-PHM-001 §10.1 -- The kind of record a register entry points at. Persisted by <c>Value</c> (ADR-022).</summary>
public sealed class ControlledDrugSourceType(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly ControlledDrugSourceType Dispensing = new(1, nameof(Dispensing));
    public static readonly ControlledDrugSourceType DispensingReversal = new(2, nameof(DispensingReversal));
    public static readonly ControlledDrugSourceType Treatment = new(3, nameof(Treatment));
    public static readonly ControlledDrugSourceType Surgery = new(4, nameof(Surgery));
    public static readonly ControlledDrugSourceType GoodsReceipt = new(5, nameof(GoodsReceipt));
    public static readonly ControlledDrugSourceType StockAdjustment = new(6, nameof(StockAdjustment));
    public static readonly ControlledDrugSourceType Correction = new(7, nameof(Correction));
}
