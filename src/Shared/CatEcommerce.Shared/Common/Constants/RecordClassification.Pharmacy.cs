using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Pharmacy and Prescriptions (HO-PHM-001) table classifications, added in the same change that adds the tables (HO-XCT-001 §5.2 C-ALL-1, ordered
/// rules of §8.2). Care given to a cat -- the prescription, its warnings and repeat requests, the dispensing and its lines, a diet authorisation --
/// is <see cref="RecordClass.Clinical"/> and <see cref="HardDeletePolicy.Forbidden"/> (C-ALL-4: no method removes a clinical row, and no owned child
/// that is itself a record). The controlled-drug register and its hash-chain seals are named by law (C-2, BR-7) -> <see cref="RecordClass.Regulatory"/>,
/// append-only at three layers. The formulary is practice configuration referenced by prescriptions -> <see cref="RecordClass.Operational"/>.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> PharmacyEntries =
    [
        new TableClassification("pharmacy", "formulary_medicine", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-PHM-001 §8.2 -- the practice's formulary, referenced by prescriptions; deactivated, never deleted"),
        new TableClassification("pharmacy", "formulary_interaction", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "HO-PHM-001 §8.2 -- owned list replaced wholesale on an edit (CurrentState carve-out, C-ALL-1)"),
        new TableClassification("pharmacy", "formulary_medicine_sku", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "HO-PHM-001 §8.2 -- owned list of linked SKUs replaced wholesale on an edit (CurrentState carve-out)"),
        new TableClassification("pharmacy", "prescription", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-PHM-001 §8.3 -- what a vet prescribed; cancelled, never deleted; D-8"),
        new TableClassification("pharmacy", "prescription_warning", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-PHM-001 §8.5 -- exactly what was on the vet's screen, kept forever with the prescription"),
        new TableClassification("pharmacy", "repeat_request", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-PHM-001 §8.8 -- an owner's request for a repeat and the decision on it"),
        new TableClassification("pharmacy", "dispensing", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-PHM-001 §8.7 -- what was handed over, with its frozen label; reversed, never deleted"),
        new TableClassification("pharmacy", "dispensing_line", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-PHM-001 §8.7 -- the exact batch given (C-ALL-4)"),
        new TableClassification("pharmacy", "cd_register_entry", RecordClass.Regulatory, HardDeletePolicy.Forbidden, "HO-PHM-001 §8.9 -- the controlled-drug register (C-2, BR-7, D-5); append-only at three layers"),
        new TableClassification("pharmacy", "cd_register_seal", RecordClass.Regulatory, HardDeletePolicy.Forbidden, "HO-PHM-001 §8.9 -- tamper-evident hash chain over the register (ADR-145)"),
        new TableClassification("pharmacy", "diet_authorisation", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-PHM-001 §8.10 -- a vet's authorisation of a therapeutic diet; expired or revoked, never deleted"),
    ];
}
