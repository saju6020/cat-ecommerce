-- HO-RPT-001 section 8.4 (HO-IMP-001, FR-IMP-4, FR-IMP-5): a batch with its landed unit cost. landed_unit_cost is NULL until the consignment's costs are allocated -- there is deliberately NO
-- fallback to the purchase-order cost (ADR-284, OQ-4): a margin or a valuation is shown only where every batch has a landed cost. The currency is the base currency.
CREATE VIEW report.rv_batch AS
SELECT
    b."Id"                         AS batch_id,
    b.sku_id                       AS sku_id,
    b.sku_code                     AS sku_code,
    b.display_name                 AS display_name,
    b.lot_number                   AS lot_number,
    b.expires_on                   AS expiry_on,
    b.status                       AS status,
    b.consignment_id               AS consignment_id,
    b.branch_id                    AS branch_id,
    b.received_on                  AS received_on,
    l.landed_unit_cost_base        AS landed_unit_cost
FROM inventory.batch b
LEFT JOIN inventory.consignment_landed_cost_line l ON l.batch_id = b."Id";
