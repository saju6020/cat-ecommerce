-- HO-XCT-001 section 12.8 -- the checks of a restore drill, one row each: name | passed (t, f or skipped) | detail.
-- psql variables: expected_migration (the latest EF migration id the build expects), forbidden_count (number of Forbidden tables that should carry triggers),
-- target_ts (the simulated failure instant, ISO-8601 UTC).

SELECT 'migrations-current',
       (SELECT max("MigrationId") FROM public."__EFMigrationsHistory") = :'expected_migration',
       'latest applied: ' || coalesce((SELECT max("MigrationId") FROM public."__EFMigrationsHistory"), 'none');

SELECT 'audit-triggers-present',
       count(*) >= 2,
       count(*)::text || ' triggers on audit.audit_entry'
FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'audit' AND c.relname = 'audit_entry' AND NOT t.tgisinternal;

SELECT 'retention-triggers-present',
       count(DISTINCT c.oid) >= :'forbidden_count'::int,
       count(DISTINCT c.oid)::text || ' tables carry the retention trigger, expected at least ' || :'forbidden_count'
FROM pg_trigger t JOIN pg_class c ON c.oid = t.tgrelid
WHERE t.tgname = 'trg_refuse_hard_delete' AND NOT t.tgisinternal;

SELECT 'row-counts-nonzero',
       (SELECT count(*) FROM account.customer_account) > 0
         AND (SELECT count(*) FROM clinic.cat) > 0
         AND (SELECT count(*) FROM clinic.appointment) > 0,
       'customer_account=' || (SELECT count(*) FROM account.customer_account)
         || ' cat=' || (SELECT count(*) FROM clinic.cat)
         || ' appointment=' || (SELECT count(*) FROM clinic.appointment);

SELECT 'audit-chain-recent',
       coalesce(max("OccurredOn"), '-infinity'::timestamptz) >= :'target_ts'::timestamptz - interval '15 minutes',
       'latest audit entry at ' || coalesce(max("OccurredOn")::text, 'none')
FROM audit.audit_entry WHERE "OccurredOn" <= :'target_ts'::timestamptz;

-- The media store is not backed up yet (HO-XCT-001 OQ-9): reported as skipped, never as a silent pass.
SELECT 'media-store', 'skipped', 'skipped: object storage is not backed up yet (OQ-9)';
