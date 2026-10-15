-- HO-RPT-001 section 8.4 (ADR-278): appointments with their customer, cat, performer, room and the local date in the branch zone. The three HOLD statuses are excluded here, in the WHERE, so a
-- held, expired or released slot can never appear in a report (HO-APT-001 section 8.1: holds never count as appointments).
CREATE VIEW report.rv_clinic_appointment AS
SELECT
    a."Id"                                                                        AS appointment_id,
    a.branch_id                                                                   AS branch_id,
    ((COALESCE(a.starts_on, a.checked_in_on)) AT TIME ZONE b.time_zone_id)::date  AS local_date,
    a.starts_on                                                                   AS starts_on,
    a.ends_on                                                                     AS ends_on,
    a.customer_id                                                                 AS customer_id,
    c.full_name                                                                   AS customer_name,
    a.cat_id                                                                      AS cat_id,
    k."Name"                                                                      AS cat_name,
    a.service_name                                                                AS service_name,
    a.service_category                                                            AS service_category,
    a.performer_id                                                                AS performer_id,
    p.full_name                                                                   AS performer_name,
    r.name                                                                        AS room_name,
    a.channel                                                                     AS channel,
    a.is_walk_in                                                                  AS is_walk_in,
    a.triage_priority                                                             AS triage_priority,
    a.status                                                                      AS status,
    a.checked_in_on                                                               AS checked_in_on,
    a.started_on                                                                  AS started_on,
    a.completed_on                                                                AS completed_on,
    a.cancelled_on                                                                AS cancelled_on,
    a.no_show_recorded_on                                                         AS no_show_recorded_on
FROM clinic.appointment a
JOIN admin.branch b                    ON b."Id" = a.branch_id
LEFT JOIN account.customer_account c   ON c."Id" = a.customer_id
LEFT JOIN clinic.cat k                 ON k."Id" = a.cat_id
LEFT JOIN account.staff_account p      ON p."Id" = a.performer_id
LEFT JOIN admin.room r                 ON r."Id" = a.room_id
WHERE a.status NOT IN ({{HOLD_STATUSES}});
