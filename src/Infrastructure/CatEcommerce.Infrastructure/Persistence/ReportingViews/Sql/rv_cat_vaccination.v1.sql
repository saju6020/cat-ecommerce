-- HO-RPT-001 section 8.4: EVERY vaccination record with its cat and owner; "the latest per vaccine" is chosen by CatVaccinationStatusCalculator, never re-derived here.
CREATE VIEW report.rv_cat_vaccination AS
SELECT
    v.cat_id            AS cat_id,
    k."Name"            AS cat_name,
    k."OwnerId"         AS owner_id,
    o.full_name         AS owner_name,
    o.mobile_number     AS owner_mobile,
    o.email_address     AS owner_email,
    o.status            AS owner_status,
    k."LifeStatus"      AS cat_life_status,
    v."VaccineId"       AS vaccine_id,
    v."VaccineName"     AS vaccine_name,
    vc."IsActive"       AS vaccine_active,
    v."AdministeredOn"  AS administered_on,
    v."NextDueOn"       AS next_due_on
FROM clinic.cat_vaccination v
JOIN clinic.cat k                 ON k."Id" = v.cat_id
JOIN account.customer_account o   ON o."Id" = k."OwnerId"
JOIN clinic.vaccine vc            ON vc."Id" = v."VaccineId";
