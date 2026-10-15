#!/usr/bin/env bash
# HO-XCT-001 section 12.8 / ADR-313 -- a restore drill on an ISOLATED host or container. It restores the latest backup from repo2 (the off-site copy, to prove it works)
# to SimulatedFailureAt = now - 5 minutes, times the restore, runs verify-restore.sql, and prints the JSON that POST /api/admin/operations/restore-tests accepts.
# Required environment: DRILL_ENVIRONMENT=isolated (a guard against running it on a live host), PGDATA, EXPECTED_MIGRATION, FORBIDDEN_TABLE_COUNT.
# Optional: DRILL_PORT (default 55432), DRILL_REPO (default 2), DRILL_DATABASE (default catecommerce), DRILL_LABEL.
set -euo pipefail

[ "${DRILL_ENVIRONMENT:-}" = "isolated" ] || { echo "refusing: set DRILL_ENVIRONMENT=isolated on an isolated drill host only" >&2; exit 2; }

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
stanza="${PGBACKREST_STANZA:-purrcare}"
pgdata="${PGDATA:?PGDATA is not set}"
port="${DRILL_PORT:-55432}"
repo="${DRILL_REPO:-2}"
database="${DRILL_DATABASE:-catecommerce}"

failure_epoch="$(( $(date -u +%s) - 300 ))"
failure_at="$(date -u -d "@$failure_epoch" +%Y-%m-%dT%H:%M:%SZ)"
target="$(date -u -d "@$failure_epoch" '+%Y-%m-%d %H:%M:%S+00')"

started_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
"$here/restore-pitr.sh" --target "$target" --repo "$repo" --confirm --i-understand-this-replaces-production >&2

pg_ctl -D "$pgdata" -o "-p $port -c archive_mode=off -c listen_addresses=''" -w -t 600 start >&2
trap 'pg_ctl -D "$pgdata" -m fast stop >/dev/null 2>&1 || true' EXIT
until [ "$(psql -p "$port" -h /var/run/postgresql -d "$database" -At -c 'SELECT pg_is_in_recovery()' 2>/dev/null || echo t)" = "f" ]; do sleep 1; done
completed_at="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

recovered_to="$(psql -p "$port" -h /var/run/postgresql -d "$database" -At -c \
  "SELECT to_char(max(\"OccurredOn\") AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS.US\"Z\"') FROM audit.audit_entry WHERE \"OccurredOn\" <= '$failure_at'::timestamptz")"
[ -n "$recovered_to" ] || recovered_to="$failure_at"

checks="$(psql -p "$port" -h /var/run/postgresql -d "$database" -At -F '|' --set ON_ERROR_STOP=1 \
  --set expected_migration="${EXPECTED_MIGRATION:?EXPECTED_MIGRATION is not set}" --set forbidden_count="${FORBIDDEN_TABLE_COUNT:?FORBIDDEN_TABLE_COUNT is not set}" \
  --set target_ts="$failure_at" -f "$here/verify-restore.sql")"

label="${DRILL_LABEL:-$(pgbackrest --stanza="$stanza" info --repo="$repo" --output=json | jq -r '.[0].backup[-1].label')}"

printf '%s\n' "$checks" | jq -R -s \
  --arg environment "drill host, isolated" --arg setlabel "$label" --arg failure "$failure_at" --arg recovered "$recovered_to" \
  --arg started "$started_at" --arg completed "$completed_at" --arg performedOn "$(date -u +%Y-%m-%d)" '
  {
    performedOn: $performedOn, environment: $environment, backupSetLabel: $setlabel,
    simulatedFailureAt: $failure, recoveredTo: $recovered, restoreStartedAt: $started, restoreCompletedAt: $completed,
    checks: ( split("\n") | map(select(length > 0) | split("|") | { name: .[0], passed: (.[1] == "t" or .[1] == "skipped"), detail: (.[2] // null) }) ),
    drillOutput: "restore-drill.sh"
  }'
