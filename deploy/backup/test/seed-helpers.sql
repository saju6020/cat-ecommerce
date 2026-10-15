-- Drill-only: inserts one row into any table, filling every NOT NULL column that has no default with a type-appropriate dummy value. Overrides are given as
-- jsonb {"column name": "<sql literal>"}. The drill needs a few rows in tables whose real constructors live in the application; this keeps the drill independent of it.
CREATE SCHEMA IF NOT EXISTS drill;

CREATE OR REPLACE FUNCTION drill.insert_default(target_schema text, target_table text, overrides jsonb DEFAULT '{}'::jsonb) RETURNS void LANGUAGE plpgsql AS $fn$
DECLARE
  cols text := '';
  vals text := '';
  c record;
  literal text;
BEGIN
  FOR c IN
    SELECT column_name, data_type, udt_name FROM information_schema.columns
    WHERE table_schema = target_schema AND table_name = target_table
      AND (is_nullable = 'NO' OR overrides ? column_name) AND column_default IS NULL AND is_generated = 'NEVER' AND identity_generation IS NULL
    ORDER BY ordinal_position
  LOOP
    literal := CASE
      WHEN overrides ? c.column_name THEN overrides ->> c.column_name
      WHEN c.data_type = 'uuid' THEN 'gen_random_uuid()'
      WHEN c.data_type IN ('text', 'character varying', 'character') THEN '''x'''
      WHEN c.data_type IN ('integer', 'smallint', 'bigint', 'numeric', 'real', 'double precision') THEN '0'
      WHEN c.data_type = 'boolean' THEN 'false'
      WHEN c.data_type LIKE 'timestamp%' THEN 'now()'
      WHEN c.data_type = 'date' THEN 'current_date'
      WHEN c.data_type IN ('jsonb', 'json') THEN '''{}'''
      WHEN c.data_type = 'inet' THEN '''127.0.0.1'''
      WHEN c.data_type = 'ARRAY' THEN '''{}'''
      ELSE 'NULL'
    END;
    cols := cols || CASE WHEN cols = '' THEN '' ELSE ', ' END || format('%I', c.column_name);
    vals := vals || CASE WHEN vals = '' THEN '' ELSE ', ' END || literal;
  END LOOP;
  EXECUTE format('INSERT INTO %I.%I (%s) VALUES (%s)', target_schema, target_table, cols, vals);
END
$fn$;
