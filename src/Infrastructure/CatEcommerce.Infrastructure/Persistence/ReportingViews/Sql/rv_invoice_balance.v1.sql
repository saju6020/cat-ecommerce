-- HO-RPT-001 section 8.4 (HO-BIL-001, FR-BIL-3): what is still owed on each issued invoice, AS AT NOW (the source computes paid and credited): outstanding = total - credited - paid. Draft and discarded
-- invoices are not receivables and have no row.
CREATE VIEW report.rv_invoice_balance AS
SELECT
    i."Id"                                   AS invoice_id,
    i.number                                 AS invoice_number,
    i.branch_id                              AS branch_id,
    i.customer_id                            AS customer_id,
    i.issued_on                              AS issued_on,
    i.issue_date                             AS issue_date,
    i.due_on                                 AS due_on,
    i.status                                 AS status,
    i.currency_code                          AS currency_code,
    i.total                                  AS total,
    i.paid_total                             AS paid_total,
    i.credited_total                         AS credited_total,
    (i.total - i.credited_total - i.paid_total) AS outstanding_amount
FROM billing.invoice i
WHERE i.status IN ({{RECEIVABLE_STATUSES}});
