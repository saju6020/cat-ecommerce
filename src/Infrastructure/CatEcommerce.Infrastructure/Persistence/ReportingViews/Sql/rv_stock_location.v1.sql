-- HO-RPT-001 section 8.4 (HO-IMP-001, FR-IMP-6): stock locations carry their branch (HO-ADM-001 section 8.2).
CREATE VIEW report.rv_stock_location AS
SELECT
    l."Id"        AS location_id,
    l.branch_id   AS branch_id,
    l.code        AS code,
    l.name        AS name,
    l.kind        AS kind,
    l.is_active   AS is_active
FROM inventory.stock_location l;
