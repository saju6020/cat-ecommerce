-- HO-XCT-001 section 12.8 / ADR-300 -- the database role model.
--
-- WHY: PostgreSQL lets a table's OWNER switch its triggers off, and a superuser skip row triggers entirely. The retention
-- triggers (ADR-299) therefore protect clinical and financial records only while the application connects as a role that is
-- neither. The host refuses to start outside Development/Testing otherwise (DatabaseRoleGuard).
--
--   catecommerce_migrator         owns every solution schema and table; applies EF migrations. Never used by the running app.
--   catecommerce_app              the running application: SELECT/INSERT/UPDATE/DELETE, no TRUNCATE, no DDL, no ownership.
--                                 DELETE is needed by AggregateOnly and Purgeable tables; the triggers refuse the rest.
--   catecommerce_backup_reporter  the backup job: INSERT on ops.backup_run only (ADR-312). Cannot read, update or delete anything.
--
-- HOW (psql; no secret is stored in this file -- passwords arrive as psql variables from the secret store):
--   psql -v ON_ERROR_STOP=1 -v app_password="$APP_DB_PASSWORD" -v migrator_password="$MIGRATOR_DB_PASSWORD" \
--        -v reporter_password="$REPORTER_DB_PASSWORD" -d catecommerce -f deploy/database/roles.sql
--
-- Idempotent: safe to run again after every migration batch. A new context that adds a schema adds it to the array below
-- (RolesSqlConventionTests fails until it does).
--
-- Order of use in a deployment:
--   1. run this file once (creates the roles, adopts existing objects, sets default privileges);
--   2. run `dotnet ef database update` AS catecommerce_migrator (new objects then belong to it and inherit the app grants);
--   3. start the API as catecommerce_app.

DO $roles$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'catecommerce_migrator') THEN
    CREATE ROLE catecommerce_migrator LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'catecommerce_app') THEN
    CREATE ROLE catecommerce_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'catecommerce_backup_reporter') THEN
    CREATE ROLE catecommerce_backup_reporter LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;
  END IF;
END
$roles$;

-- Passwords come from psql variables (never literals). Skipped silently when a variable is not supplied, so the privilege
-- part of this file can be re-run by a tool that has no access to the secrets.
\if :{?migrator_password}
ALTER ROLE catecommerce_migrator PASSWORD :'migrator_password';
\endif
\if :{?app_password}
ALTER ROLE catecommerce_app PASSWORD :'app_password';
\endif
\if :{?reporter_password}
ALTER ROLE catecommerce_backup_reporter PASSWORD :'reporter_password';
\endif

DO $privileges$
DECLARE
  -- Every schema of the solution's EF model, plus 'retention' (the trigger function). Keep in step with
  -- RecordClassification (RolesSqlConventionTests).
  solution_schemas text[] := ARRAY['account','admin','audit','billing','boarding','clinic','files','inventory','notify','ops','ordering','pharmacy','product','report','retention'];
  s text;
  r record;
BEGIN
  -- The connecting database name is not hard-coded: privileges apply to the database this script runs in.
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO catecommerce_migrator, catecommerce_app, catecommerce_backup_reporter', current_database());
  EXECUTE format('GRANT CREATE ON DATABASE %I TO catecommerce_migrator', current_database());

  -- The migrator creates schemas and the migrations history table, and the btree_gist extension is already created or trusted.
  GRANT USAGE, CREATE ON SCHEMA public TO catecommerce_migrator;
  GRANT USAGE ON SCHEMA public TO catecommerce_app;

  FOREACH s IN ARRAY solution_schemas LOOP
    EXECUTE format('CREATE SCHEMA IF NOT EXISTS %I AUTHORIZATION catecommerce_migrator', s);
    EXECUTE format('ALTER SCHEMA %I OWNER TO catecommerce_migrator', s);

    -- Adopt anything an earlier superuser-run migration created, so that the migrator (not postgres) owns every object.
    FOR r IN SELECT c.relname, c.relkind FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE n.nspname = s AND c.relkind IN ('r','p','v','m','S') LOOP
      IF r.relkind = 'S' THEN
        -- Identity/serial sequences follow their table; only standalone sequences are altered directly.
        IF NOT EXISTS (SELECT 1 FROM pg_depend d WHERE d.objid = (quote_ident(s) || '.' || quote_ident(r.relname))::regclass AND d.deptype IN ('a','i')) THEN
          EXECUTE format('ALTER SEQUENCE %I.%I OWNER TO catecommerce_migrator', s, r.relname);
        END IF;
      ELSIF r.relkind = 'v' THEN
        EXECUTE format('ALTER VIEW %I.%I OWNER TO catecommerce_migrator', s, r.relname);
      ELSIF r.relkind = 'm' THEN
        EXECUTE format('ALTER MATERIALIZED VIEW %I.%I OWNER TO catecommerce_migrator', s, r.relname);
      ELSE
        EXECUTE format('ALTER TABLE %I.%I OWNER TO catecommerce_migrator', s, r.relname);
      END IF;
    END LOOP;

    FOR r IN SELECT p.oid::regprocedure AS signature FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
             WHERE n.nspname = s AND p.prokind IN ('f','p') LOOP
      EXECUTE format('ALTER ROUTINE %s OWNER TO catecommerce_migrator', r.signature);
    END LOOP;

    -- The application: DML on every table and view, USAGE+SELECT on sequences. No TRUNCATE, no DDL, no ownership.
    EXECUTE format('GRANT USAGE ON SCHEMA %I TO catecommerce_app', s);
    EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO catecommerce_app', s);
    EXECUTE format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO catecommerce_app', s);
    EXECUTE format('REVOKE TRUNCATE, REFERENCES, TRIGGER ON ALL TABLES IN SCHEMA %I FROM catecommerce_app', s);

    -- Objects the migrator creates LATER get the same grants automatically.
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE catecommerce_migrator IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO catecommerce_app', s);
    EXECUTE format('ALTER DEFAULT PRIVILEGES FOR ROLE catecommerce_migrator IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO catecommerce_app', s);
  END LOOP;

  -- The backup reporter writes its own run log and nothing else (ADR-312). No SELECT: it never needs to read a row back.
  GRANT USAGE ON SCHEMA ops TO catecommerce_backup_reporter;
  IF to_regclass('ops.backup_run') IS NOT NULL THEN
    REVOKE ALL ON ops.backup_run FROM catecommerce_backup_reporter;
    GRANT INSERT ON ops.backup_run TO catecommerce_backup_reporter;
  END IF;
END
$privileges$;
