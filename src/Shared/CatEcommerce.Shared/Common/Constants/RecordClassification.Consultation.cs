using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Consultation and Medical Records (HO-EMR-001) table classifications, added in the same change that
/// adds the tables (HO-XCT-001 §5.2 C-ALL-1, ordered rules of §8.2). Everything recording the care of a
/// cat -- the visit, its problems, vitals and photos, addenda, the draft working copy of the note,
/// diagnostic orders and their results -- is <see cref="RecordClass.Clinical"/> and
/// <see cref="HardDeletePolicy.Forbidden"/> (C-ALL-4: no method removes a clinical row, and no owned
/// child that is itself a record; a problem removed from an open visit is withdrawn, not deleted).
/// A signed consent is named by law (NFR-LEG-4) -> <see cref="RecordClass.Regulatory"/>. The three
/// catalogues (diagnosis codes, lab analytes, note templates) are configuration referenced by clinical
/// records -> <see cref="RecordClass.Operational"/>, like <c>clinic.vaccine</c>.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> ConsultationEntries =
    [
        new TableClassification("clinic", "visit", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.2 -- the consultation record; D-8"),
        new TableClassification("clinic", "visit_problem", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.2 -- coded diagnoses; withdrawn while open, never deleted"),
        new TableClassification("clinic", "visit_vitals", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.2 -- measurements taken"),
        new TableClassification("clinic", "visit_photo", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.2 -- clinical photographs"),
        new TableClassification("clinic", "visit_treatment", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.6 -- treatments given and the stock they took; reversed, never deleted"),
        new TableClassification("clinic", "visit_addendum", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.4 -- append-only corrections (BR-11)"),
        new TableClassification("clinic", "consultation_draft", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.4 -- working copy of the clinical note; overwritten, never deleted"),
        new TableClassification("clinic", "diagnostic_order", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.5 -- diagnostics ordered for a cat"),
        new TableClassification("clinic", "diagnostic_requested_analyte", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.5 -- what was asked of the laboratory"),
        new TableClassification("clinic", "diagnostic_result_set", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.5 -- append-only laboratory results"),
        new TableClassification("clinic", "diagnostic_result_value", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.5 -- append-only result values"),
        new TableClassification("clinic", "diagnostic_attachment", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.5 -- append-only lab files"),
        new TableClassification("clinic", "consent_record", RecordClass.Regulatory, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.9 -- signed consent (NFR-LEG-4)"),
        new TableClassification("clinic", "diagnosis_code", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.2 -- catalogue referenced by clinical records"),
        new TableClassification("clinic", "lab_analyte", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.5 -- catalogue referenced by clinical records"),
        new TableClassification("clinic", "note_template", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-EMR-001 §8.4 -- configuration (templates)"),
    ];
}
