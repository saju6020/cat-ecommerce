-- HO-RPT-001 section 8.4 (added by the build, for R16): a credit note with how much of it was refunded and how much was released to the customer's credit. Refund methods are exactly cash in store and
-- bank transfer (HO-BIL-001 has no store-credit refund tender; the part kept as customer credit is its own column).
CREATE VIEW report.rv_credit_note AS
SELECT
    cn."Id"                              AS credit_note_id,
    cn.number                            AS credit_note_number,
    cn.branch_id                         AS branch_id,
    cn.customer_id                       AS customer_id,
    cn.issued_on                         AS issued_on,
    cn.kind                              AS credit_note_kind,
    cn.reason                            AS reason,
    cn.net_total                         AS net_total,
    cn.tax_total                         AS tax_total,
    cn.total                             AS total,
    cn.released_to_customer_credit       AS released_to_customer_credit,
    cn.currency_code                     AS currency_code
FROM billing.credit_note cn;
