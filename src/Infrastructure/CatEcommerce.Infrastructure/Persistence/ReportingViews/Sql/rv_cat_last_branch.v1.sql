-- HO-RPT-001 ADR-292: cats are not branch-scoped, so a cat belongs to the branch of its most recent non-hold appointment. A cat never seen has no row (it is "Unattributed").
CREATE VIEW report.rv_cat_last_branch AS
SELECT DISTINCT ON (a.cat_id)
    a.cat_id                                   AS cat_id,
    a.branch_id                                AS branch_id,
    COALESCE(a.starts_on, a.checked_in_on)     AS last_appointment_on
FROM clinic.appointment a
WHERE a.status NOT IN ({{HOLD_STATUSES}})
ORDER BY a.cat_id, COALESCE(a.starts_on, a.checked_in_on) DESC NULLS LAST;
