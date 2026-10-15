-- HO-RPT-001 section 8.4 (HO-BIL-001, FR-BIL-13): the cash-up of a business day as Billing recorded it. cash_recorded / card_recorded / transfer_recorded are the day's accepted takings by method;
-- expected_cash is what the till should hold; counted_cash and variance exist once the day is counted. A day with no row is "not closed" -- the report says so, it does not invent a figure.
CREATE VIEW report.rv_cash_up AS
SELECT
    c."Id"                    AS cash_up_id,
    c.branch_id               AS branch_id,
    c.business_date           AS business_date,
    c.status                  AS status,
    c.expected_cash_total     AS cash_recorded,
    c.expected_card_total     AS card_recorded,
    c.expected_transfer_total AS transfer_recorded,
    c.expected_cash           AS expected_cash,
    c.counted_cash            AS counted_cash,
    c.variance                AS variance,
    c.explanation             AS explanation,
    c.closed_by               AS closed_by,
    c.closed_on               AS closed_on
FROM billing.cash_up c;
