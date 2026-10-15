-- HO-RPT-001 section 8.4: every parasite-control treatment with its cat and owner (Combined counts as deworming, HO-NTF-001 F25).
CREATE VIEW report.rv_cat_parasite_treatment AS
SELECT
    t.cat_id            AS cat_id,
    k."Name"            AS cat_name,
    k."OwnerId"         AS owner_id,
    o.full_name         AS owner_name,
    o.mobile_number     AS owner_mobile,
    o.email_address     AS owner_email,
    o.status            AS owner_status,
    k."LifeStatus"      AS cat_life_status,
    t."TreatmentType"   AS treatment_type,
    t."ProductName"     AS product_name,
    t."AdministeredOn"  AS given_on,
    t."NextDueOn"       AS next_due_on
FROM clinic.cat_parasite_treatment t
JOIN clinic.cat k                 ON k."Id" = t.cat_id
JOIN account.customer_account o   ON o."Id" = k."OwnerId";
