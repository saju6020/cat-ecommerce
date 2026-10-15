#!/usr/bin/env bash
# HO-XCT-001 section 12.8 -- proves WAL is still reaching the repositories (pgbackrest check) and records how far it has got.
# The "archived until" instant is PostgreSQL's own last_archived_time, so it moves every archive_timeout (60 s) even on a quiet database.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
stanza="${PGBACKREST_STANZA:-purrcare}"
started="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
log="$(mktemp)"
trap 'rm -f "$log"' EXIT

if pgbackrest --stanza="$stanza" check >"$log" 2>&1; then
  ok=true
  err=""
else
  ok=false
  err="$(tail -n 5 "$log")"
fi

wal_until="$(psql --no-psqlrc -At -c "SELECT to_char(last_archived_time AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS\"Z\"') FROM pg_stat_archiver" 2>/dev/null || true)"
finished="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

"$here/report-run.sh" archive-check "$started" "$finished" "$ok" repo1 "" "" "$wal_until" "$err"
[ "$ok" = true ]
