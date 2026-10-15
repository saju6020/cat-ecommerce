-- HO-RPT-001 section 8.4 (added by the build, for R16): money returned to a customer, by method (cash in store or bank transfer, FR-ORD-17), with the credit note it settles.
CREATE VIEW report.rv_refund AS
SELECT
    r."Id"             AS refund_id,
    r.payment_id       AS payment_id,
    r.credit_note_id   AS credit_note_id,
    r.branch_id        AS branch_id,
    r.customer_id      AS customer_id,
    r.method           AS method,
    r.amount           AS amount,
    r.currency_code    AS currency_code,
    r.reason           AS reason,
    r.issued_on        AS issued_on,
    r.business_date    AS business_date
FROM billing.refund r;
