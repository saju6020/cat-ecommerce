namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-PHM-001 (HO-XCT-001 §5.2 C-ALL-2, ADR-152) -- Pharmacy stores no customer personal data beyond ids, other than the owner's name frozen
/// into a dispensing label snapshot. That label is a clinical record of what was handed over (C-3, D-8) and is deliberately left intact on
/// anonymisation; the controlled-drug register holds ids only. Prescriptions, dispensings and diet authorisations are reassigned on a merge
/// (ADR-152), never erased, so Pharmacy is exempted rather than given an <c>IPersonalDataEraser</c>.
/// </summary>
public static partial class ErasureExemptions
{
    private static readonly IReadOnlyList<(string ContextKey, string Reason)> PharmacyEntries =
    [
        ("pharmacy", "Clinical and Regulatory records keyed by cat, customer and staff ids; the only personal text is the owner's name frozen in a dispensing label, kept intact under C-3/D-8 (HO-PHM-001 ADR-152); the register holds ids only and is never rewritten."),
    ];
}
