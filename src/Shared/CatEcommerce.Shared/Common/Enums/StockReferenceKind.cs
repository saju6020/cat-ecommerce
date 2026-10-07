namespace CatEcommerce.Shared.Common.Enums;

/// <summary>
/// Owned by HO-IMP-001 §8.8/§10.1 (ADR-103/ADR-104). What document a stock movement traces back to
/// -- the reference the recall trace reads. Consumers' kinds are **pre-declared here by the owner**
/// so a consuming handover never edits this file (HO-IMP-001 §10.1's own words).
///
/// Several consuming handovers' inline code samples use different strings for their own reference
/// kind than the ones the owner pre-declared here (e.g. HO-PHM-001's dispensing example says
/// <c>Dispensing</c>, HO-EMR-001 says <c>VisitTreatment</c>, HO-SRG-001 says <c>SurgeryConsumable</c>,
/// HO-ORD-001 says <c>Order</c>). Per the owner-wins rule, this file uses HO-IMP-001's own list
/// (<see cref="Prescription"/>, <see cref="Visit"/>, <see cref="Surgery"/>, <see cref="CustomerOrder"/>)
/// and the mismatches are recorded as disagreements in docs/handover/CONTRACTS.md. HO-PHM-001's
/// controlled-drug register correction (`ControlledDrugCorrection`) has **no** matching member at
/// all -- recorded there as a gap, not invented here.
/// </summary>
public sealed class StockReferenceKind(int value, string displayName) : Enumeration(value, displayName)
{
    public static readonly StockReferenceKind GoodsReceipt = new(1, nameof(GoodsReceipt));
    public static readonly StockReferenceKind StockTransfer = new(2, nameof(StockTransfer));
    public static readonly StockReferenceKind StockAdjustment = new(3, nameof(StockAdjustment));
    public static readonly StockReferenceKind StockCount = new(4, nameof(StockCount));
    public static readonly StockReferenceKind BatchWriteOff = new(5, nameof(BatchWriteOff));
    public static readonly StockReferenceKind StorageBreach = new(6, nameof(StorageBreach));
    public static readonly StockReferenceKind CustomerOrder = new(7, nameof(CustomerOrder));
    public static readonly StockReferenceKind PosSale = new(8, nameof(PosSale));
    public static readonly StockReferenceKind Prescription = new(9, nameof(Prescription));
    public static readonly StockReferenceKind Visit = new(10, nameof(Visit));
    public static readonly StockReferenceKind Surgery = new(11, nameof(Surgery));
    public static readonly StockReferenceKind BoardingStay = new(12, nameof(BoardingStay));
    public static readonly StockReferenceKind CustomerReturn = new(13, nameof(CustomerReturn));
}
