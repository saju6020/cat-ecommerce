-- HO-RPT-001 section 8.4 (HO-BIL-001, FR-BIL-5, FR-ORD-17): one row per credit note line, amounts POSITIVE, with the classification and the cost-of-goods link of the invoice line it credits. A credit note counts
-- on its OWN issue date, so a closed month never changes (BR-11).
CREATE VIEW report.rv_credit_note_line AS
SELECT
    cl.id                      AS credit_note_line_id,
    cn."Id"                    AS credit_note_id,
    cn.number                  AS credit_note_number,
    cn.invoice_id              AS invoice_id,
    cl.invoice_line_id         AS invoice_line_id,
    cn.branch_id               AS branch_id,
    cn.customer_id             AS customer_id,
    cn.issued_on               AS issued_on,
    cn.kind                    AS credit_note_kind,
    cn.reason                  AS reason,
    cl.net_amount              AS net_amount,
    cl.tax_amount              AS tax_amount,
    cl.tax_rate_percent        AS tax_rate_percent,
    il.tax_class_code          AS tax_class_code,
    il.business_line           AS business_line,
    il.item_kind               AS item_kind,
    il.source_kind             AS source_kind,
    il.source_line_id          AS source_line_id,
    il.sku_id                  AS sku_id,
    il.product_id              AS product_id,
    il.service_category        AS service_category,
    il.description             AS description,
    cn.currency_code           AS currency_code
FROM billing.credit_note_line cl
JOIN billing.credit_note cn         ON cn."Id" = cl.credit_note_id
JOIN report.rv_invoice_line il      ON il.invoice_line_id = cl.invoice_line_id;
