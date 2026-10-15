#!/usr/bin/env bash
# HO-XCT-001 section 12.8 -- one backup of the given type to repo1, then to repo2, each reported to ops.backup_run, each followed by expire.
# usage: backup.sh full|diff
set -euo pipefail

type="${1:?usage: backup.sh full|diff}"
case "$type" in full|diff) ;; *) echo "backup type must be full or diff" >&2; exit 2 ;; esac

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
stanza="${PGBACKREST_STANZA:-purrcare}"
failed=0

for repo in 1 2; do
  started="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  log="$(mktemp)"
  if pgbackrest --stanza="$stanza" backup --type="$type" --repo="$repo" >"$log" 2>&1; then
    finished="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    info="$(pgbackrest --stanza="$stanza" info --repo="$repo" --output=json)"
    label="$(printf '%s' "$info" | jq -r '.[0].backup[-1].label // ""')"
    size="$(printf '%s' "$info" | jq -r '.[0].backup[-1].info.repository.size // ""')"
    pgbackrest --stanza="$stanza" expire --repo="$repo" >>"$log" 2>&1 || true
    "$here/report-run.sh" "$type" "$started" "$finished" true "repo$repo" "$label" "$size"
  else
    finished="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
    failed=1
    "$here/report-run.sh" "$type" "$started" "$finished" false "repo$repo" "" "" "" "$(tail -n 5 "$log")" || true
  fi
  rm -f "$log"
done

exit "$failed"
