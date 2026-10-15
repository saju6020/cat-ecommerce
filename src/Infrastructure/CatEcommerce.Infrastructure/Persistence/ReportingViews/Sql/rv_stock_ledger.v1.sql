-- HO-RPT-001 section 8.4 (HO-IMP-001, FR-IMP-15, D-3): the immutable stock ledger. source_type / source_id / source_line_id identify the order line, POS line, dispense or stay charge that
-- caused a sale-type movement -- margin depends on it (ADR-284). For a customer order source_line_id is the order line id.
CREATE VIEW report.rv_stock_ledger AS
SELECT
    e."Id"                 AS entry_id,
    e.sku_id               AS sku_id,
    e.batch_id             AS batch_id,
    e.location_id          AS location_id,
    e.branch_id            AS branch_id,
    e.kind                 AS movement_kind,
    e.quantity_delta       AS quantity_delta,
    e.occurred_on          AS occurred_on,
    e.reference_kind       AS source_type,
    e.reference_id         AS source_id,
    e.reference_line_id    AS source_line_id
FROM inventory.stock_ledger_entry e;
