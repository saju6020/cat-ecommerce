-- HO-RPT-001 section 8.4 / R7 (HO-IMP-001, ADR-112): what is still due on ISSUED purchase orders, per branch, SKU and delivery location: sum over lines of max(0, ordered - accepted).
CREATE VIEW report.rv_on_order AS
SELECT
    po.branch_id                AS branch_id,
    l.sku_id                    AS sku_id,
    po.deliver_to_location_id   AS location_id,
    sum(GREATEST(0, l.ordered_quantity - COALESCE(a.accepted, 0)))   AS outstanding
FROM inventory.purchase_order po
JOIN inventory.purchase_order_line l ON l.purchase_order_id = po."Id"
LEFT JOIN (
    SELECT purchase_order_line_id, sum(quantity_accepted) AS accepted
    FROM inventory.goods_receipt_line
    WHERE purchase_order_line_id IS NOT NULL
    GROUP BY purchase_order_line_id
) a ON a.purchase_order_line_id = l."Id"
WHERE po.status = {{ISSUED}}
GROUP BY po.branch_id, l.sku_id, po.deliver_to_location_id;
