#!/usr/bin/env bash
# HO-XCT-001 section 12.8 / ADR-312 -- writes ONE row to ops.backup_run as catecommerce_backup_reporter (INSERT privilege only; it can read nothing back).
# usage: report-run.sh KIND STARTED_ISO FINISHED_ISO SUCCEEDED(true|false) REPOSITORY [LABEL] [SIZE_BYTES] [WAL_ARCHIVED_UNTIL_ISO] [ERROR_TEXT]
# Connection from the environment: PGHOST, PGPORT, PGDATABASE, REPORTER_DB_USER (default catecommerce_backup_reporter), REPORTER_DB_PASSWORD (secret store).
set -euo pipefail

kind="${1:?kind}"
started="${2:?started}"
finished="${3:?finished}"
succeeded="${4:?succeeded}"
repository="${5:?repository}"
label="${6:-}"
size="${7:-}"
wal_until="${8:-}"
error_text="${9:-}"

# The error summary is shown to administrators: strip anything that looks like a secret, then cut it to 500 characters.
error_summary="$(printf '%s' "$error_text" | sed -E 's/[^[:space:]]*(key|secret|password|passphrase|pass=)[^[:space:]]*/[scrubbed]/Ig' | tr '\n\t' '  ' | tr -d '\000-\010\013\014\016-\037' | cut -c1-500)"   # ONE line first: cut counts per line, so a 5-line error was 2 500 characters

PGUSER="${REPORTER_DB_USER:-catecommerce_backup_reporter}" PGPASSWORD="${REPORTER_DB_PASSWORD:?REPORTER_DB_PASSWORD is not set}" \
psql --no-psqlrc --quiet --set ON_ERROR_STOP=1 \
  --set kind="$kind" --set started="$started" --set finished="$finished" --set ok="$succeeded" --set repo="$repository" \
  --set label="$label" --set size="$size" --set wal="$wal_until" --set err="$error_summary" <<'SQL'
INSERT INTO ops.backup_run (id, kind, started_on, finished_on, succeeded, repository, backup_label, size_bytes, wal_archived_until, error_summary)
VALUES (gen_random_uuid(), :'kind', :'started'::timestamptz, :'finished'::timestamptz, :'ok'::boolean, :'repo',
        NULLIF(:'label', ''), NULLIF(:'size', '')::bigint, NULLIF(:'wal', '')::timestamptz, NULLIF(:'err', ''));
SQL
