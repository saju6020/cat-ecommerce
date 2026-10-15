-- HO-RPT-001 section 8.4 / ADR-291 (Reporting's own view, derived): the money events of a customer, one row per invoice (or credit note) and business line, used by the growth reports. The customer is the
-- SURVIVOR of a merge (account.customer_account.merged_into_customer_id), because HO-BIL-001 keeps issued invoices on the original customer id (the merge assumption of OQ-14 does not hold for Billing), so a merged
-- customer is counted once (I-F7). Invoices count in the revenue states only; a credit note is a negative event on its own date. kind: 1 = invoice, 2 = credit note.
CREATE VIEW report.rv_customer_activity AS
SELECT
    COALESCE(c.merged_into_customer_id, i.customer_id)   AS customer_id,
    i.branch_id                                          AS branch_id,
    l.business_line                                      AS business_line,
    i.issued_on                                          AS occurred_on,
    sum(l.net_amount)                                    AS net_amount,
    i.currency_code                                      AS currency_code,
    1                                                    AS kind
FROM billing.invoice_line l
JOIN billing.invoice i                      ON i."Id" = l.invoice_id
LEFT JOIN account.customer_account c        ON c."Id" = i.customer_id
WHERE i.status IN ({{COUNTED_STATUSES}}) AND i.issued_on IS NOT NULL
GROUP BY COALESCE(c.merged_into_customer_id, i.customer_id), i.branch_id, l.business_line, i."Id", i.issued_on, i.currency_code
UNION ALL
SELECT
    COALESCE(c.merged_into_customer_id, cn.customer_id)  AS customer_id,
    cn.branch_id                                         AS branch_id,
    il.business_line                                     AS business_line,
    cn.issued_on                                         AS occurred_on,
    -sum(cl.net_amount)                                  AS net_amount,
    cn.currency_code                                     AS currency_code,
    2                                                    AS kind
FROM billing.credit_note_line cl
JOIN billing.credit_note cn                 ON cn."Id" = cl.credit_note_id
JOIN billing.invoice_line il                ON il.id = cl.invoice_line_id
LEFT JOIN account.customer_account c        ON c."Id" = cn.customer_id
GROUP BY COALESCE(c.merged_into_customer_id, cn.customer_id), cn.branch_id, il.business_line, cn."Id", cn.issued_on, cn.currency_code;
