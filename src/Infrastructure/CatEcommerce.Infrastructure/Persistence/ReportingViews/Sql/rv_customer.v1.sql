-- HO-RPT-001 section 8.4 (ADR-278): one row per customer account. display_name is the stored name, which is already the placeholder for an anonymised record.
-- merged_into_customer_id lets the growth reports count a merged customer once (HO-BIL-001 keeps issued invoices on the original customer id).
CREATE VIEW report.rv_customer AS
SELECT
    c."Id"                    AS customer_id,
    c.full_name               AS display_name,
    c.status                  AS status,
    c.registration_channel    AS registration_channel,
    c."CreatedDate"           AS registered_on,
    c.merged_into_customer_id AS merged_into_customer_id
FROM account.customer_account c;
