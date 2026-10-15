-- HO-RPT-001 section 8.4 (HO-EMR-001), dashboard only: a visit with the vet who opened it, when its appointment completed and when (if ever) it was finalised.
CREATE VIEW report.rv_visit_open AS
SELECT
    v."Id"            AS visit_id,
    v.branch_id       AS branch_id,
    v.opened_by       AS vet_id,
    v.status          AS status,
    a.completed_on    AS completed_on,
    v.finalised_on    AS finalised_on
FROM clinic.visit v
LEFT JOIN clinic.appointment a ON a."Id" = v.appointment_id;
