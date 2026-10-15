using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Boarding (HO-BRD-001) table classifications, added in the same change that adds the tables (HO-XCT-001 section 5.2 C-ALL-1). Units, maintenance
/// blocks and the care routine are practice configuration -> <see cref="RecordClass.Operational"/>; a rate card is a price in force -> Financial
/// (D-9), like <c>admin.service_price</c>. Nothing here is deleted: a unit is deactivated, a block cancelled, a card superseded by a later one. The
/// routine's times are an owned list the aggregate replaces wholesale on an edit (the CurrentState carve-out), hence <c>AggregateOnly</c>.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> BoardingUnitEntries =
    [
        new TableClassification("boarding", "unit", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.2 -- a boarding unit; deactivated, never deleted"),
        new TableClassification("boarding", "unit_maintenance_block", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.2 -- a maintenance block; cancelled, never deleted"),
        new TableClassification("boarding", "rate_card", RecordClass.Financial, HardDeletePolicy.Forbidden, "D-9 (price in force) -- append-only effective-dated boarding rate card"),
        new TableClassification("boarding", "care_routine", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.2 -- a branch care routine; one row per branch"),
        new TableClassification("boarding", "care_routine_time", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "HO-BRD-001 section 8.2 -- owned list replaced wholesale by SetTimes (CurrentState carve-out)"),
    ];

    private static readonly IReadOnlyList<TableClassification> BoardingStayEntries =
    [
        new TableClassification("boarding", "stay", RecordClass.Financial, HardDeletePolicy.Forbidden, "D-9 -- a boarding reservation carries the agreed quote, deposit and charges; cancelled or checked out, never deleted"),
        new TableClassification("boarding", "stay_status_change", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.3 -- the stay's own audit trail of transitions (C-ALL-4)"),
        new TableClassification("boarding", "stay_unit_allocation", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.4 -- the unit a stay held for a range; truncated or deactivated, never deleted"),
        new TableClassification("boarding", "stay_cat", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "HO-BRD-001 section 8.7 -- cats on an enquiry are replaced by ChangeEnquiry (CurrentState carve-out); a reserved stay never loses one"),
        new TableClassification("boarding", "stay_cat_feeding", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "HO-BRD-001 section 8.7 -- owned list replaced wholesale by SetCareProfile"),
        new TableClassification("boarding", "stay_cat_medication", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "HO-BRD-001 section 8.7 -- owned list replaced wholesale by SetCareProfile"),
        new TableClassification("boarding", "stay_rate_unit", RecordClass.Financial, HardDeletePolicy.AggregateOnly, "HO-BRD-001 section 8.8 -- the snapshotted quote, replaced wholesale when it is recalculated (extension, early check-out)"),
    ];

    private static readonly IReadOnlyList<TableClassification> BoardingCareEntries =
    [
        new TableClassification("boarding", "stay_cat_belonging", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "HO-BRD-001 section 8.11 -- belongings listed at check-in, ticked at check-out; owned by the stay cat"),
        new TableClassification("boarding", "care_task", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.9 -- care given to a named cat (feeds, doses, litter); done, skipped or voided, never deleted; D-8"),
        new TableClassification("boarding", "care_observation", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.9 -- appetite, water, litter and mood of a named cat; append-only; D-8"),
        new TableClassification("boarding", "isolation_entry", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.12 -- staff contact with a suspected-infectious cat; the exit is the only later change"),
    ];

    private static readonly IReadOnlyList<TableClassification> BoardingIncidentEntries =
    [
        new TableClassification("boarding", "incident", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.13 -- an injury, illness or other incident to a named cat; resolved, never deleted; D-8"),
        new TableClassification("boarding", "stay_media", RecordClass.Media, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.17 / D-10 -- the REFERENCE to a boarding photo or video; the row stays (Purged) as the record, only the stored file is erased by the boarding-media purge source"),
    ];

    private static readonly IReadOnlyList<TableClassification> BoardingClinicEntries =
    [
        new TableClassification("boarding", "stay_referral", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.14 -- a boarded cat referred to the clinic; append-only, the return is the only later change; D-8"),
        new TableClassification("boarding", "stay_vet_commitment", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.14 / D-9 -- a clinic charge counted against a cat's vet spending limit; append-only, unique by Billing charge id"),
        new TableClassification("boarding", "stay_vet_approval", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.14 / D-9 -- the owner's answer on a vet charge above the limit; a decision is final"),
        new TableClassification("boarding", "stay_surgery", RecordClass.Clinical, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.21 -- a surgery on a boarded cat (US-SRG-09); the discharge is the only later change; D-8"),
    ];

    private static readonly IReadOnlyList<TableClassification> BoardingWaitlistDaycareEntries =
    [
        new TableClassification("boarding", "waitlist_entry", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.18 -- a request to be told when a unit frees; withdrawn or closed, never deleted"),
        new TableClassification("boarding", "daycare_rate_card", RecordClass.Financial, HardDeletePolicy.Forbidden, "D-9 (price in force) -- append-only effective-dated daycare rate card"),
        new TableClassification("boarding", "daycare_visit", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-BRD-001 section 8.18 / D-9 -- a daycare visit and its charge; cancelled or checked out, never deleted"),
    ];
}
