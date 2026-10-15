namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Surgery (HO-SRG-001) is exempted from <c>IPersonalDataEraser</c> registration: it stores ids, snapshots of the procedure and price, clinical
/// text (consent-override reasons, report text, instructions) and an append-only anaesthesia log -- all part of the clinical record, retained under D-8
/// (HO-XCT-001 §8.8, OQ-7). No owner name or contact detail is ever copied onto a surgery row; they are read from the customer account at display time
/// and follow that account's own erasure.</summary>
public static partial class ErasureExemptions
{
    private static readonly IReadOnlyList<(string ContextKey, string Reason)> SurgeryEntries =
    [
        ("surgery", "Ids, procedure and price snapshots and clinical text -- part of the clinical record, retained under D-8 (HO-XCT-001 §8.8, OQ-7); no owner name or contact detail is stored."),
    ];
}
