-- HO-RPT-001 section 8.4: the latest delivered care reminder per subject (the cat, the vaccine or treatment, and the due date), so the due list can say "last reminder sent".
CREATE VIEW report.rv_care_reminder_sent AS
SELECT DISTINCT ON (n.subject_kind, n.subject_id, n.subject_ref, n.subject_due_on)
    n.subject_kind     AS subject_kind,
    n.subject_id       AS subject_id,
    n.subject_ref      AS subject_ref,
    n.subject_due_on   AS subject_due_on,
    n.sent_on          AS sent_on
FROM notify.notification n
WHERE n.subject_kind IN ({{CARE_KINDS}})
  AND n.status IN ({{SENT_STATUSES}})
  AND n.sent_on IS NOT NULL
ORDER BY n.subject_kind, n.subject_id, n.subject_ref, n.subject_due_on, n.sent_on DESC;
