using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Surgery (HO-SRG-001) table classifications, added in the same change that adds the tables (HO-XCT-001 §5.2 C-ALL-1). Care given to a cat -- the surgery,
/// its checklist and histories, the anaesthesia log, the surgical report and its addenda, and the staff-time rows that hold a surgery's occupancy -- is
/// <see cref="RecordClass.Clinical"/> and <see cref="HardDeletePolicy.Forbidden"/> (C-ALL-4). Protocols and campaign price lists are practice
/// configuration -> <see cref="RecordClass.Operational"/>. The controlled-drug register entries a drug entry causes belong to Pharmacy's own table
/// (Regulatory) and are not re-classified here.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> SurgeryEntries =
    [
        new TableClassification("clinic", "surgery", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-SRG-001 §8.2 -- a surgical procedure on a named cat; cancelled or discharged, never deleted; D-8"),
        new TableClassification("clinic", "surgery_checklist_item", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-SRG-001 §8.6 -- the pre-operative checklist, who completed or waived each item and when"),
        new TableClassification("clinic", "surgery_follow_up", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-SRG-001 §8.10 -- post-op follow-ups; closed, never deleted"),
        new TableClassification("clinic", "surgery_status_change", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-SRG-001 §8.3 -- the surgery's own audit trail of transitions"),
        new TableClassification("clinic", "surgery_recovery_change", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-SRG-001 §8.3 -- recovery-ward status history"),
        new TableClassification("clinic", "anaesthesia_log_entry", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-SRG-001 §8.8 -- the anaesthesia log; append-only at three layers (aggregate, no endpoint, trigger)"),
        new TableClassification("clinic", "surgical_report", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-SRG-001 §8.2 -- the surgical report; immutable once finalised (trigger)"),
        new TableClassification("clinic", "surgical_report_addendum", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-SRG-001 §8.2 -- append-only addenda to a finalised report (trigger)"),
        new TableClassification("clinic", "appointment_staff_time", RecordClass.Clinical, HardDeletePolicy.AggregateOnly, "HO-SRG-001 §8.4 (ADR-159) -- derived occupancy rows kept in step with the appointment by SyncStaffTime; the appointment is the record"),
        new TableClassification("clinic", "surgery_protocol", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-SRG-001 §8.12 -- per-service fasting, post-op text and follow-up protocol (practice configuration)"),
        new TableClassification("clinic", "surgery_protocol_follow_up", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "HO-SRG-001 §8.12 -- owned list replaced wholesale on an edit (CurrentState carve-out, C-ALL-1)"),
        new TableClassification("clinic", "surgery_campaign", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-SRG-001 §8.11 -- community-cat campaign price list; deactivated, never deleted"),
        new TableClassification("clinic", "surgery_campaign_price", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "HO-SRG-001 §8.11 -- owned per-service prices of a campaign (locked once used)"),
    ];
}
