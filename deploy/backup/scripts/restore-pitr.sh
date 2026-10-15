#!/usr/bin/env bash
# HO-XCT-001 section 12.8 -- point-in-time restore. DESTRUCTIVE: --delta replaces the contents of the data directory.
# usage: restore-pitr.sh --target "2026-10-12 11:52:00+06" --confirm [--repo 1|2] [--i-understand-this-replaces-production]
# Refuses to run without --confirm, and refuses a data directory that names the production cluster unless the second flag is given too.
set -euo pipefail

target=""
repo=""
confirm=false
production=false
while [ "$#" -gt 0 ]; do
  case "$1" in
    --target) target="${2:?--target needs a value}"; shift 2 ;;
    --repo) repo="${2:?--repo needs 1 or 2}"; shift 2 ;;
    --confirm) confirm=true; shift ;;
    --i-understand-this-replaces-production) production=true; shift ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

[ -n "$target" ] || { echo "--target is required" >&2; exit 2; }
[ "$confirm" = true ] || { echo "refusing to restore without --confirm: this replaces the data directory" >&2; exit 2; }

stanza="${PGBACKREST_STANZA:-purrcare}"
pgdata="${PGDATA:?PGDATA is not set}"
marker="${PRODUCTION_CLUSTER_MARKER:-purrcare-production}"

# A data directory whose postgresql.auto.conf names the production cluster is production: an extra, explicit flag is required.
if [ -f "$pgdata/postgresql.auto.conf" ] && grep -q -- "$marker" "$pgdata/postgresql.auto.conf" && [ "$production" != true ]; then
  echo "$pgdata looks like the production cluster ($marker). Re-run with --i-understand-this-replaces-production if that is really intended." >&2
  exit 3
fi

# A postmaster.pid whose process is ALIVE means PostgreSQL is running on this data directory: restoring over it would corrupt a live cluster (ARCHITECTURE/TEST
# gate row 55). A pid file whose process is gone is stale; pgBackRest still refuses to restore while the file exists, so the operator removes it by hand -- only
# after confirming that no postgres process is running (the runbook says how).
if [ -f "$pgdata/postmaster.pid" ]; then
  pid="$(head -n 1 "$pgdata/postmaster.pid" 2>/dev/null || true)"
  if [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null; then
    echo "refusing: $pgdata/postmaster.pid names process $pid, which is running. Stop PostgreSQL and wait for it to exit first." >&2
    exit 4
  fi
  echo "warning: $pgdata/postmaster.pid is stale (no process $pid). pgBackRest will refuse to restore while it exists; remove it only after confirming no postgres process is running." >&2
fi

if [ "$target" = "latest" ]; then
  # Recover to the end of the archived WAL (after a crash: lose only what was never shipped).
  args=(--stanza="$stanza" restore --delta)
else
  args=(--stanza="$stanza" restore --type=time --target="$target" --target-action=promote --delta)
fi
[ -z "$repo" ] || args+=(--repo="$repo")

pgbackrest "${args[@]}"
echo "Restore to '$target' finished. Start PostgreSQL, then follow the reconciliation steps in docs/runbooks/backup-and-restore.md (erasure journal replay is mandatory)."
