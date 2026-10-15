-- HO-RPT-001 section 8.4 (ADR-278): one row per staff account. email_present only; the address itself never leaves the source table through a view.
CREATE VIEW report.rv_staff AS
SELECT
    s."Id"        AS staff_account_id,
    s.full_name   AS full_name,
    s.role_ids    AS role_ids,
    (s.email_address IS NOT NULL AND s.email_address <> '') AS email_present
FROM account.staff_account s;
