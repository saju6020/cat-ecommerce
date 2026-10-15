-- HO-RPT-001 section 8.4 (HO-BIL-001, FR-BIL-12): a payment with what has been settled against it. The methods are exactly the four launch methods (cash on delivery, cash in store, card in store, bank
-- transfer); there is no gateway (SRS section 1.2). remitted_amount is what courier remittances have covered (cash on delivery); bank_matched is true while an active bank statement match exists
-- (bank transfer); collector_kind says who collected a cash-on-delivery payment (FR-BIL-7). business_date is the branch's business day the payment was accepted.
CREATE VIEW report.rv_payment AS
SELECT
    p."Id"                    AS payment_id,
    p.branch_id               AS branch_id,
    p.customer_id             AS customer_id,
    p.method                  AS method,
    p.amount                  AS amount,
    p.currency_code           AS currency_code,
    p.received_on             AS received_on,
    p.business_date           AS business_date,
    p.status                  AS status,
    p.recorded_by             AS recorded_by,
    p.recorded_on             AS recorded_on,
    p.accepted_on             AS accepted_on,
    p.collector_kind          AS collector_kind,
    p.collector_courier_code  AS courier_code,
    COALESCE((SELECT sum(rl.amount_remitted) FROM billing.cod_remittance_line rl WHERE rl.payment_id = p."Id"), 0) AS remitted_amount,
    EXISTS (SELECT 1 FROM billing.bank_match m WHERE m.payment_id = p."Id" AND m.unmatched_on IS NULL)              AS bank_matched
FROM billing.payment p;
