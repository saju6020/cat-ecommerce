-- HO-RPT-001 section 8.4 / R7 (HO-IMP-001, FR-IMP-8): the reorder policy of a SKU at a location with its preferred supplier's name and lead time (null when the policy names no supplier).
CREATE VIEW report.rv_reorder_policy AS
SELECT
    r.sku_id                 AS sku_id,
    r.location_id            AS location_id,
    r.reorder_point          AS reorder_point,
    r.reorder_quantity       AS reorder_quantity,
    r.preferred_supplier_id  AS supplier_id,
    s.name                   AS supplier_name,
    s.lead_time_days         AS lead_time_days
FROM inventory.reorder_policy r
LEFT JOIN inventory.supplier s ON s."Id" = r.preferred_supplier_id;
