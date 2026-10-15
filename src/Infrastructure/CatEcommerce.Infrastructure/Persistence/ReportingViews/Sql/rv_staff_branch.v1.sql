-- HO-RPT-001 section 8.7 (added by the build): one row per (staff account, assigned branch), so a report can check the caller's branch assignment and a schedule can check a recipient's.
CREATE VIEW report.rv_staff_branch AS
SELECT
    a."Id" AS staff_account_id,
    x.branch_id AS branch_id,
    (x.branch_id = a.primary_branch_id) AS is_primary
FROM admin.staff_branch_assignment a
CROSS JOIN LATERAL unnest(a.branch_ids) AS x(branch_id);
