using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// HO-IMP-001 table classifications -- HO-XCT-001 §5.2 C-ALL-1, added in the same change that adds each
/// table (ADR-341). Applied in §8.2's order. Money owed on goods bought (purchase orders, receipts, the
/// consignment's costs and rates, landed cost) is <see cref="RecordClass.Financial"/>; the consignment record
/// and its documents are named by law (NFR-LEG-2) so <see cref="RecordClass.Regulatory"/>; every other
/// table accumulates history and is <see cref="RecordClass.Operational"/>. None is hard-deleted (C-ALL-4):
/// ledger and reservation entries are immutable (ADR-098) and a correction is a new entry.
/// <c>stock_position</c> is a trigger-maintained projection, so <see cref="RecordClass.CurrentState"/>.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> InventoryEntries =
    [
        new TableClassification("inventory", "stock_location", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.3 -- a place stock is held; deactivated, never deleted"),
        new TableClassification("inventory", "product_storage_profile", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "HO-IMP-001 §8.4 -- the current storage requirement, replaced by Set"),
        new TableClassification("inventory", "supplier", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.5 -- referenced by purchase orders for years; deactivated, never deleted"),

        new TableClassification("inventory", "purchase_order", RecordClass.Financial, HardDeletePolicy.Forbidden, "D-9: money owed to a supplier"),
        new TableClassification("inventory", "purchase_order_line", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "PurchaseOrderAggregate.ReplaceDraft replaces a Draft order lines (ADR-298 amendment 2026-10-12)"),

        new TableClassification("inventory", "consignment", RecordClass.Regulatory, HardDeletePolicy.Forbidden, "NFR-LEG-2: import consignments are never deleted"),
        new TableClassification("inventory", "consignment_hs_code", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "ConsignmentAggregate.UpdateDetails replaces the HS code list (ADR-298 amendment 2026-10-12)"),
        new TableClassification("inventory", "consignment_document", RecordClass.Regulatory, HardDeletePolicy.Forbidden, "NFR-LEG-2/ADR-113: documents are superseded, never removed"),
        new TableClassification("inventory", "consignment_status_change", RecordClass.Regulatory, HardDeletePolicy.Forbidden, "NFR-LEG-2: the import record's own history"),
        new TableClassification("inventory", "consignment_cost", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "ConsignmentAggregate.ReplaceCostsAndRates replaces the cost components (ADR-298 amendment 2026-10-12)"),
        new TableClassification("inventory", "consignment_exchange_rate", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "ConsignmentAggregate.ReplaceCostsAndRates replaces the exchange rates (ADR-298 amendment 2026-10-12)"),
        new TableClassification("inventory", "consignment_landed_cost_line", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "ConsignmentAggregate.RecordLandedCostAllocation re-allocates at any time and replaces the lines (ADR-298 amendment 2026-10-12)"),

        new TableClassification("inventory", "goods_receipt", RecordClass.Financial, HardDeletePolicy.Forbidden, "D-9: goods received against a purchase order; immutable (ADR-105)"),
        new TableClassification("inventory", "goods_receipt_line", RecordClass.Financial, HardDeletePolicy.Forbidden, "D-9: goods received against a purchase order; immutable (ADR-105)"),
        new TableClassification("inventory", "batch", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.6 -- recall traceability; written off, never deleted"),
        new TableClassification("inventory", "batch_status_change", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.6.1 -- one row per transition, never trimmed"),

        new TableClassification("inventory", "stock_ledger_entry", RecordClass.Operational, HardDeletePolicy.Forbidden, "ADR-098: immutable; a correction is a new entry"),
        new TableClassification("inventory", "stock_reservation_entry", RecordClass.Operational, HardDeletePolicy.Forbidden, "ADR-098: immutable; a release is a new entry"),
        new TableClassification("inventory", "stock_position", RecordClass.CurrentState, HardDeletePolicy.Forbidden, "ADR-099: trigger-maintained projection of the ledger; no path may delete it"),

        new TableClassification("inventory", "stock_transfer", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.10"),
        new TableClassification("inventory", "stock_transfer_line", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.10"),
        new TableClassification("inventory", "stock_adjustment", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.10 -- shrinkage record"),
        new TableClassification("inventory", "stock_adjustment_line", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.10"),
        new TableClassification("inventory", "stock_count", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.10"),
        new TableClassification("inventory", "stock_count_line", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.10 -- upserted while Open, frozen at submit"),

        new TableClassification("inventory", "reorder_policy", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "HO-IMP-001 §8.13 -- the current reorder point, replaced by Set"),
        new TableClassification("inventory", "stock_alert", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.13"),
        new TableClassification("inventory", "storage_breach_report", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-IMP-001 §8.13 -- a safety record"),
    ];
}
