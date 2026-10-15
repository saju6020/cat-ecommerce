-- HO-RPT-001 section 8.4 (HO-SRG-001): the surgery register. The date is the day the procedure started (else the scheduled day) in the branch zone. Complications come from the surgical report;
-- controlled_drug_entries counts the controlled-drug register entries written for the surgery (source type Surgery). consent_override is true when the consent gate was overridden.
CREATE VIEW report.rv_surgery AS
SELECT
    s."Id"                                                                              AS surgery_id,
    s.branch_id                                                                         AS branch_id,
    (COALESCE(s.procedure_started_on, s.starts_on) AT TIME ZONE b.time_zone_id)::date   AS local_date,
    COALESCE(s.procedure_started_on, s.starts_on)                                       AS occurred_on,
    s.procedure_name                                                                    AS procedure_name,
    s.procedure_category                                                                AS procedure_category,
    s.procedure_is_sterilisation                                                        AS is_sterilisation,
    s.cat_id                                                                            AS cat_id,
    k."Name"                                                                            AS cat_name,
    s.customer_id                                                                       AS customer_id,
    c.full_name                                                                         AS customer_name,
    s.surgeon_id                                                                        AS surgeon_id,
    su.full_name                                                                        AS surgeon_name,
    a.full_name                                                                         AS assistant_name,
    t.name                                                                              AS theatre_name,
    s.asa_grade                                                                         AS asa_grade,
    s.status                                                                            AS status,
    s.outcome                                                                           AS outcome,
    COALESCE(r.had_complications, false)                                                AS has_complications,
    r.complications                                                                     AS complications_summary,
    (s.consent_override_on IS NOT NULL)                                                 AS consent_override,
    (SELECT count(*)::int FROM pharmacy.cd_register_entry e
      WHERE e.source_type = {{SURGERY_SOURCE}} AND e.source_id = s."Id")                AS controlled_drug_entries
FROM clinic.surgery s
JOIN admin.branch b                     ON b."Id" = s.branch_id
LEFT JOIN clinic.cat k                  ON k."Id" = s.cat_id
LEFT JOIN account.customer_account c    ON c."Id" = s.customer_id
LEFT JOIN account.staff_account su      ON su."Id" = s.surgeon_id
LEFT JOIN account.staff_account a       ON a."Id" = s.assistant_id
LEFT JOIN admin.room t                  ON t."Id" = s.theatre_id
LEFT JOIN clinic.surgical_report r      ON r.surgery_id = s."Id";
