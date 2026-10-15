-- HO-RPT-001 section 8.4 / R7 (HO-IMP-001): the stock position of a batch at a location, with the batch facts the usability rule needs (status, expiry) and the SKU names. R7 applies HO-IMP-001's own
-- StockUsability.Usable to these rows, so the view carries inputs and no rule of its own.
CREATE VIEW report.rv_stock_position AS
SELECT
    p.batch_id      AS batch_id,
    p.location_id   AS location_id,
    p.sku_id        AS sku_id,
    p.on_hand       AS on_hand,
    p.reserved      AS reserved,
    b.status        AS batch_status,
    b.expires_on    AS expiry_on,
    b.sku_code      AS sku_code,
    b.display_name  AS display_name,
    b.stock_unit    AS stock_unit
FROM inventory.stock_position p
JOIN inventory.batch b ON b."Id" = p.batch_id;
