using CatEcommerce.Shared.Common.Enums;

namespace CatEcommerce.Shared.Common.Constants;

/// <summary>
/// Ordering (HO-ORD-001) table classifications, added in the same change that adds the tables (HO-XCT-001 §5.2 C-ALL-1; rules in §8.2,
/// applied in order). Each phase appends the tables it migrates: Phase A the two generic edge tables of schema <c>ops</c>, Phase B the configuration roots, Phase C the cart, Phase D orders.
/// </summary>
public static partial class RecordClassification
{
    private static readonly IReadOnlyList<TableClassification> OrderingEntries =
    [
        // ops.idempotency_record is a 24-hour working set (ADR-202): nothing about the customer, purged by OrderWorker.
        new TableClassification("ops", "idempotency_record", RecordClass.Telemetry, HardDeletePolicy.Purgeable, "HO-ORD-001 ADR-202 -- edge working set, purged after 24 hours (IdempotencyRules.RetentionHours)"),

        // ops.inbound_webhook_receipt proves an event was processed once (COM-3); it holds a provider key and an event id, nothing personal.
        new TableClassification("ops", "inbound_webhook_receipt", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-ORD-001 ADR-209 -- the dedupe inbox; keeping it is what makes a late redelivery a no-op"),

        // ── Phase B: configuration. Edited in place and audited (ADR-206); never hard-deleted -- a zone, courier or code is deactivated. ──
        new TableClassification("ordering", "delivery_zone", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-ORD-001 ADR-206 -- configuration edited in place; the order snapshots the zone, level, charge and slot"),
        new TableClassification("ordering", "delivery_zone_coverage", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "HO-ORD-001 §8.8 -- an owned collection the zone replaces wholesale (CurrentState carve-out, C-ALL-1)"),
        new TableClassification("ordering", "delivery_zone_service", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "HO-ORD-001 §8.8 -- an owned collection the zone replaces wholesale (CurrentState carve-out, C-ALL-1)"),
        new TableClassification("ordering", "courier", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-ORD-001 §8.14 -- master data an order's shipment names; deactivated, never deleted"),
        new TableClassification("ordering", "bank_transfer_account", RecordClass.Operational, HardDeletePolicy.Forbidden, "HO-ORD-001 §20.8 -- the shop's own account, entered as data; shown on orders"),
        new TableClassification("ordering", "discount_code", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-ORD-001 ADR-207 -- a price rule in force whose redemption counter orders rely on (D-9)"),

        // ── Phase C: the cart. A cart is a working set, not a record of a sale: an abandoned guest cart carries no money and no personal data
        //    beyond a hash, and a customer cart is current state. Carts are never deleted by code; lines are the cart's own state. ──
        new TableClassification("ordering", "cart", RecordClass.CurrentState, HardDeletePolicy.Forbidden, "HO-ORD-001 ADR-199 -- a customer's working basket; merged or abandoned, never deleted (audited, OQ-18)"),
        new TableClassification("ordering", "cart_line", RecordClass.CurrentState, HardDeletePolicy.AggregateOnly, "HO-ORD-001 §8.2 -- an owned collection the cart replaces and trims (CurrentState carve-out, C-ALL-1)"),

        // ── Phase D: orders. An order is a financial record (money owed, paid or refunded -- D-9): nothing is deleted, and the database refuses it
        //    (OrderingMigrationSql). The allocation table is the one exception: a staff reallocation before dispatch replaces a line's batches. ──
        new TableClassification("ordering", "sales_order", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-ORD-001 §8.3 -- D-9; nothing deleted, enforced by trigger"),
        new TableClassification("ordering", "order_line", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-ORD-001 §8.3.1 -- a line of a financial document is itself a record (C-ALL-4)"),
        new TableClassification("ordering", "order_line_allocation", RecordClass.Operational, HardDeletePolicy.AggregateOnly, "HO-ORD-001 §8.9 (D-4) -- the recall trace; replaced only by a staff reallocation before dispatch"),
        new TableClassification("ordering", "order_status_change", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-ORD-001 §8.4 (US-ORD-09 AC2) -- append-only attributed history; a correction is a new row (C-ALL-4)"),
        new TableClassification("ordering", "order_payment_ref", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-ORD-001 §8.3 -- a reference to money recorded in Billing; append-only"),

        // ── Phase G: returns and refunds. A return is a record of a claim and a decision on money and stock (D-9); a refund is append-only (ADR-211). ──
        new TableClassification("ordering", "return_request", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-ORD-001 §8.10 -- a customer's claim and the staff decision on money and stock; nothing deleted"),
        new TableClassification("ordering", "return_line", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-ORD-001 §8.10 -- a line of a return, with what came back and what was decided (C-ALL-4)"),
        new TableClassification("ordering", "order_refund", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-ORD-001 ADR-211 -- money paid back; append-only, enforced by trigger"),

        // ── Phase E: reconciliation issues -- what staff entered when recording failed; accountants resolve them, nothing is deleted (ADR-205). ──
        new TableClassification("ordering", "reconciliation_issue", RecordClass.Financial, HardDeletePolicy.Forbidden, "HO-ORD-001 §8.16 -- payment details a failed recording would otherwise lose; resolved, never deleted"),
    ];
}
