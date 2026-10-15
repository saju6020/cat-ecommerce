-- HO-RPT-001 section 8.4 (HO-BIL-001, FR-BIL-1/2/8): one row per invoice line with the classification HO-BIL-001 stores ON the line -- business_line (Clinic, Shop, Boarding) and item_kind -- so a combined invoice
-- (FR-ORD-19) is classified per line. Revenue is Sigma net_amount (after the line discount and the invoice discount share, excluding tax). source_line_id identifies the ORDER LINE behind a shop line (the
-- charge's source line reference is the order line id without dashes), so the cost of goods can be found in the stock ledger (ADR-284). Sku and product come from that order line; service_category from the
-- appointment behind a clinic line. Draft and discarded invoices are filtered by the reports on invoice_status, never here, so the view stays a faithful published contract.
CREATE VIEW report.rv_invoice_line AS
SELECT
    l.id                                AS invoice_line_id,
    i."Id"                              AS invoice_id,
    i.number                            AS invoice_number,
    i.branch_id                         AS branch_id,
    i.customer_id                       AS customer_id,
    i.status                            AS invoice_status,
    i.issued_on                         AS issued_on,
    i.issue_date                        AS issue_date,
    l.business_line                     AS business_line,
    l.item_kind                         AS item_kind,
    l.source_kind                       AS source_kind,
    l.source_id                         AS source_id,
    src.line_id                         AS source_line_id,
    ol.sku_id                           AS sku_id,
    ol.product_id                       AS product_id,
    ap.service_category                 AS service_category,
    l.description                       AS description,
    l.quantity                          AS quantity,
    l.unit_price                        AS unit_price,
    l.line_discount                     AS line_discount,
    l.invoice_discount_share            AS invoice_discount_share,
    l.net_amount                        AS net_amount,
    l.tax_class_id                      AS tax_class_id,
    l.tax_class_code                    AS tax_class_code,
    l.tax_rate_percent                  AS tax_rate_percent,
    l.tax_amount                        AS tax_amount,
    i.currency_code                     AS currency_code
FROM billing.invoice_line l
JOIN billing.invoice i              ON i."Id" = l.invoice_id
LEFT JOIN billing.charge ch         ON ch."Id" = l.charge_id
LEFT JOIN LATERAL (
    SELECT CASE WHEN l.source_kind = {{ORDER_SOURCE}} AND ch.source_line_ref ~ '^[0-9a-f]{32}$' THEN ch.source_line_ref::uuid END AS line_id
) src ON true
LEFT JOIN ordering.order_line ol    ON ol.id = src.line_id
LEFT JOIN clinic.appointment ap     ON ap."Id" = l.source_id AND l.source_kind = {{APPOINTMENT_SOURCE}};
