#!/usr/bin/env bash
# HO-XCT-001 section 12.8 / ADR-314 -- writes the erasure journal next to BOTH backup repositories: customer ids and request dates ONLY, for the last 40 days
# (longer than the 35-day backup window). After a restore, docs/runbooks/backup-and-restore.md has the operator replay it so no erased customer reappears.
#
# pgBackRest has no command that stores an arbitrary file (repo-get and repo-ls only), so this script places the file itself, as the runbook records:
#   repo1 (posix)   plain copy to  $REPO1_PATH/erasure-journal/<date>.csv
#   repo2 (off-site) the file is ENCRYPTED with the repository passphrase (openssl aes-256-cbc, pbkdf2) and uploaded as <date>.csv.enc
#                    - to s3://... through curl's SigV4 signing when JOURNAL_REPO2_TARGET is an https:// URL prefix (path-style: <endpoint>/<bucket>/<prefix>),
#                    - to a directory when it is a path (used by the drill test, where repo2 is a posix stand-in).
# It reads ops.erasure_request, which the reporter role cannot: JOURNAL_DB_USER is a dedicated read-only role (see the runbook), never the owner.
# Secrets come from the environment only: JOURNAL_DB_PASSWORD, REPO2_CIPHER_PASS, REPO2_S3_KEY, REPO2_S3_KEY_SECRET.
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo1_path="${REPO1_PATH:-/var/lib/pgbackrest}"
day="$(date -u +%Y-%m-%d)"
started="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
file="$work/$day.csv"

ok=true
err=""
repo_used=repo1

if ! PGUSER="${JOURNAL_DB_USER:?JOURNAL_DB_USER is not set}" PGPASSWORD="${JOURNAL_DB_PASSWORD:?JOURNAL_DB_PASSWORD is not set}" \
     psql --no-psqlrc --set ON_ERROR_STOP=1 \
     -c "\\copy (SELECT \"Id\" AS customer_account_id, requested_on FROM ops.erasure_request WHERE requested_on > now() - interval '40 days' ORDER BY requested_on, \"Id\") TO STDOUT WITH (FORMAT csv, HEADER true)" >"$file" 2>"$work/err"; then
  ok=false
  err="$(tail -n 3 "$work/err")"
fi

if [ "$ok" = true ]; then
  if ! { mkdir -p "$repo1_path/erasure-journal" && cp "$file" "$repo1_path/erasure-journal/$day.csv"; } 2>"$work/put"; then
    ok=false
    err="$(tail -n 3 "$work/put")"
  fi
fi

if [ "$ok" = true ]; then
  repo_used=repo2
  target="${JOURNAL_REPO2_TARGET:?JOURNAL_REPO2_TARGET is not set}"
  encrypted="$work/$day.csv.enc"
  if ! openssl enc -aes-256-cbc -pbkdf2 -salt -pass env:REPO2_CIPHER_PASS -in "$file" -out "$encrypted" 2>"$work/put"; then
    ok=false
    err="$(tail -n 3 "$work/put")"
  elif [[ "$target" == https://* ]]; then
    # The credentials go to curl on STDIN (--config -), never on its command line, where any user who can list processes could read them
    # (/proc/<pid>/cmdline, ps, docker top) -- gate fix 2026-10-12, SECURITY low 3.
    if ! curl --fail --silent --show-error --aws-sigv4 "aws:amz:${REPO2_S3_REGION:?REPO2_S3_REGION is not set}:s3" --config - \
           --upload-file "$encrypted" "${target%/}/erasure-journal/$day.csv.enc" 2>"$work/put" <<CURLCONFIG
user = "${REPO2_S3_KEY:?REPO2_S3_KEY is not set}:${REPO2_S3_KEY_SECRET:?REPO2_S3_KEY_SECRET is not set}"
CURLCONFIG
    then
      ok=false
      err="$(tail -n 3 "$work/put")"
    fi
  elif ! { mkdir -p "$target" && cp "$encrypted" "$target/$day.csv.enc"; } 2>"$work/put"; then
    ok=false
    err="$(tail -n 3 "$work/put")"
  fi
fi

finished="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
"$here/report-run.sh" erasure-journal "$started" "$finished" "$ok" "$repo_used" "" "" "" "$err"
[ "$ok" = true ]
