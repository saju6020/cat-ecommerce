using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>Clinic Appointments (HO-APT-001) table classifications, added in the same change that
/// adds these tables (HO-XCT-001 §5.2 C-ALL-1). An appointment records an actual or planned clinical
/// encounter for a named cat (reason for visit, presenting complaint, triage) -- "care given to a
/// cat" is the first-priority rule in §8.2's ordering, so both the appointment and its status
/// history are classified <see cref="RecordClass.Clinical"/>. The waitlist entry is a booking-queue
/// record, not care itself -- <see cref="RecordClass.Operational"/>.</summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> AppointmentsEntries =
    [
        new TableClassification("clinic", "appointment", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-APT-001 §8.2 -- clinical encounter for a named cat"),
        new TableClassification("clinic", "appointment_status_change", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-APT-001 §8.2 -- part of the clinical encounter's own audit trail"),
        new TableClassification("clinic", "waitlist_entry", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-APT-001 §8.11 -- a booking queue entry, not care itself"),
    ];
}
