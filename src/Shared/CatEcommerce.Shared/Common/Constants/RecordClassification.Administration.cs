using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Administration (HO-ADM-001) table classifications -- transcribed verbatim from HO-XCT-001
/// §10.3's own table (C-ALL-1), added in the same change that adds these tables.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> AdministrationEntries =
    [
        new TableClassification("admin", "branch", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "branch_closure", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "room", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "staff_branch_assignment", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "staff_roster", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "roster_exception", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "leave_request", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "clinic_service", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "tax_class", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "business_settings", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "content_item", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "policy_document", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),
        new TableClassification("admin", "system_status", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-XCT-001 §10.3"),

        new TableClassification("admin", "service_price", RecordClass.Financial, HardDeletePolicy.Forbidden, "D-9 (price in force)"),
        new TableClassification("admin", "tax_rate", RecordClass.Financial, HardDeletePolicy.Forbidden, "D-9 (price in force)"),

        new TableClassification("admin", "policy_version", RecordClass.Regulatory, HardDeletePolicy.Forbidden, "NFR-LEG-5"),
        new TableClassification("admin", "policy_acceptance", RecordClass.Regulatory, HardDeletePolicy.Forbidden, "NFR-LEG-5"),

        new TableClassification("admin", "branch_opening_interval", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "Replaced wholesale"),
        new TableClassification("admin", "staff_roster_current_shift", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "Replaced wholesale"),
        new TableClassification("admin", "staff_roster_next_shift", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "Replaced wholesale"),
        new TableClassification("admin", "business_settings_value", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "Replaced wholesale"),
    ];
}
