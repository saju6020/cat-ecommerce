-- HO-RPT-001 section 8.7 (added by the build, section 1 Amended): the branch name and IANA zone every report needs to resolve its date range.
CREATE VIEW report.rv_branch AS
SELECT
    b."Id"        AS branch_id,
    b.code        AS code,
    b.name        AS name,
    b.time_zone_id AS time_zone_id,
    b.is_active   AS is_active
FROM admin.branch b;
