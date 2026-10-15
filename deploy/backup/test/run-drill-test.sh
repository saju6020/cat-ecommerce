#!/usr/bin/env bash
# HO-XCT-001 section 16.2 -- the backup drill test (I-G1 .. I-G8), run against a DISPOSABLE PostgreSQL 17 + pgBackRest container. It measures, it does not assume.
#   usage: bash deploy/backup/test/run-drill-test.sh        (env: OUT_DIR for the results, KEEP=1 to leave the containers up, SKIP_SCHEMA_GENERATION=1 with SCHEMA_SQL=<file>)
#   needs: docker (with compose), bash, and for the schema `dotnet` (the EF migrations are the schema; the drill restores the REAL schema, not a toy one).
# Exit code 0 only if every case passed. Results: $OUT_DIR/results.txt, the measured numbers $OUT_DIR/measurements.json, the C12-shaped drill output $OUT_DIR/drill.json.
# No secret is committed: every password below is generated for this run and exists only in this process and the throw-away containers.
set -euo pipefail
export MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*'

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$here/../../.." && pwd)"
here_native="$(cd "$here" && (pwd -W 2>/dev/null || pwd))"
repo_native="$(cd "$repo_root" && (pwd -W 2>/dev/null || pwd))"
out="${OUT_DIR:-$(mktemp -d)}"
mkdir -p "$out"
results="$out/results.txt"
: > "$results"

jqx() { docker run --rm -i --entrypoint jq purrcare-backup:dev "$@"; }
jqfile() { local f="$1"; shift; jqx "$@" < "$f"; }
random() { head -c 18 /dev/urandom | od -An -tx1 | tr -d ' \n'; }
DRILL_PG_PASSWORD="$(random)"; DRILL_CIPHER_PASS="$(random)"
migrator_pw="$(random)"; app_pw="$(random)"; reporter_pw="$(random)"; journal_pw="$(random)"
export DRILL_PG_PASSWORD DRILL_CIPHER_PASS

container=purrcare-drill-pg
compose=(docker compose -f "$here_native/docker-compose.drill.yml")
failures=0

pass() { echo "PASS $1 $2" | tee -a "$results"; }
fail() { echo "FAIL $1 $2" | tee -a "$results"; failures=$((failures + 1)); }
check() { local id="$1" description="$2"; shift 2; if "$@"; then pass "$id" "$description"; else fail "$id" "$description"; fi; }

dx() { docker exec -i -e JOURNAL_REPO2_TARGET=/var/lib/pgbackrest/repo2-store/erasure-journal -e REPO2_CIPHER_PASS="$DRILL_CIPHER_PASS" -e PGDATABASE=catecommerce -e REPORTER_DB_PASSWORD="$reporter_pw" -e JOURNAL_DB_USER=catecommerce_journal_reader -e JOURNAL_DB_PASSWORD="$journal_pw" "$container" "$@"; }
sql() { dx psql -U postgres -d catecommerce -v ON_ERROR_STOP=1 -At "$@"; }

# A one-off container on the SAME volumes (the main container is stopped or killed): renders the drill pgBackRest config, then runs the command.
oneoff() {
  local cipher="${CIPHER_OVERRIDE:-$DRILL_CIPHER_PASS}"
  docker run --rm --entrypoint bash \
    -e PGDATA=/var/lib/postgresql/data -e PGPORT=5432 -e PGDATABASE=catecommerce -e REPO2_CIPHER_PASS="$cipher" \
    -e DRILL_ENVIRONMENT -e EXPECTED_MIGRATION -e FORBIDDEN_TABLE_COUNT -e DRILL_DATABASE \
    -v purrcare-drill_pgdata:/var/lib/postgresql/data -v purrcare-drill_repo1:/var/lib/pgbackrest -v "$here_native":/drill:ro \
    purrcare-backup:dev /drill/in-container.sh "$@"
}

# Starts the restored cluster WITHOUT archiving, runs a query, stops it. usage: after_restore <sql>
query_restored() {
  oneoff bash -c '
    set -euo pipefail
    pg_ctl -D "$PGDATA" -o "-c archive_mode=off -c listen_addresses=" -w -t 600 start >/dev/null
    psql -U postgres -d catecommerce -At -c "$0"
    pg_ctl -D "$PGDATA" -m fast stop >/dev/null' "$1"
}

# Ready means accepting connections AND promoted: a restored cluster answers pg_isready while still in recovery (read-only, and pgBackRest cannot find a primary yet).
# Waiting only for pg_isready was the cause of the intermittent I-G2 / "unable to find primary cluster" failures (backlog row 50).
wait_ready() {
  for _ in $(seq 1 90); do
    if [ "$(dx psql -U postgres -d catecommerce -At -c 'SELECT pg_is_in_recovery()' 2>/dev/null || echo t)" = "f" ]; then return 0; fi
    sleep 2
  done
  return 1
}

# Row 55 (TEST gate): after a graceful stop the data directory must hold NO postmaster.pid. A stop that outlived Docker's grace period leaves one, and the restore
# then silently does nothing -- so assert it, instead of discovering it as a wrong row count.
assert_clean_stop() {
  if [ "$(oneoff bash -c 'test -e "$PGDATA/postmaster.pid" && echo present || echo gone')" != "gone" ]; then
    echo "ABORT: postmaster.pid is still present after the graceful stop; refusing to restore over a possibly running cluster" >&2
    exit 1
  fi
}

# A restore whose exit status is not 0 aborts the drill with the tail of its log (never swallowed), and so does an exit 0 that never printed "finished".
# usage: restore_pitr_or_abort <log-name> <restore-pitr.sh args>
restore_pitr_or_abort() {
  local name="$1"; shift
  if ! oneoff /opt/purrcare/scripts/restore-pitr.sh "$@" > "$out/$name-restore.log" 2>&1; then
    echo "ABORT: restore-pitr.sh failed (see $out/$name-restore.log):" >&2
    tail -n 8 "$out/$name-restore.log" >&2
    exit 1
  fi
  grep -q "finished" "$out/$name-restore.log" || { echo "ABORT: restore-pitr.sh exited 0 but never printed 'finished'" >&2; exit 1; }
}

cleanup() {
  if [ "${KEEP:-0}" != "1" ]; then "${compose[@]}" down -v >/dev/null 2>&1 || true; fi
}
trap cleanup EXIT

echo "== build the backup image" >&2
docker build -q -t purrcare-backup:dev "$repo_native/deploy/backup" >/dev/null

# ---- the schema: the real EF migrations, as one plain script (from an empty database; the idempotent form cannot wrap every migration) --------------------------------------------------------------------------------------------------
schema_sql="${SCHEMA_SQL:-$out/schema.sql}"
if [ "${SKIP_SCHEMA_GENERATION:-0}" != "1" ]; then
  echo "== generate the schema from the EF migrations" >&2
  (cd "$repo_root" && ConnectionStrings__CatEcommerce="Host=localhost;Database=schema_only_never_connected;Username=x;Password=$DRILL_PG_PASSWORD" \
    dotnet dotnet-ef migrations script --no-build \
      --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output "$(cd "$(dirname "$schema_sql")" && (pwd -W 2>/dev/null || pwd))/$(basename "$schema_sql")" >/dev/null)
fi
export EXPECTED_MIGRATION
EXPECTED_MIGRATION="$(grep -oE "VALUES \('[0-9]{14}_[A-Za-z0-9]+'" "$schema_sql" | tail -n 1 | sed -E "s/VALUES \('//; s/'//")"
[ -n "$EXPECTED_MIGRATION" ] || { echo "could not determine the latest migration id from $schema_sql" >&2; exit 2; }

# ---- I-G1: build, start, stanza-create, check ----------------------------------------------------------------------------------------------------------------------
echo "== start the drill cluster" >&2
"${compose[@]}" down -v >/dev/null 2>&1 || true
"${compose[@]}" up -d >/dev/null
check I-G1 "cluster starts" wait_ready
check I-G1 "stanza-create succeeds" dx pgbackrest --stanza=purrcare stanza-create
check I-G1 "check succeeds" dx pgbackrest --stanza=purrcare check

# ---- load the real schema, the roles of deploy/database/roles.sql, and the few rows the verification needs ------------------------------------------------------------
dx psql -U postgres -d catecommerce -v ON_ERROR_STOP=1 -q -f - < "$schema_sql" >/dev/null
dx psql -U postgres -d catecommerce -v ON_ERROR_STOP=1 -q \
  -v migrator_password="$migrator_pw" -v app_password="$app_pw" -v reporter_password="$reporter_pw" -f - < "$repo_root/deploy/database/roles.sql" >/dev/null
dx psql -U postgres -d catecommerce -v ON_ERROR_STOP=1 -q -f - < "$here/seed-helpers.sql" >/dev/null
sql -c "CREATE ROLE catecommerce_journal_reader LOGIN PASSWORD '$journal_pw'" >/dev/null
sql -c "GRANT USAGE ON SCHEMA ops TO catecommerce_journal_reader; GRANT SELECT ON ops.erasure_request TO catecommerce_journal_reader" >/dev/null
sql -c "SELECT drill.insert_default('account','customer_account'); SELECT drill.insert_default('clinic','cat'); SELECT drill.insert_default('clinic','appointment')" >/dev/null
export FORBIDDEN_TABLE_COUNT DRILL_ENVIRONMENT=isolated DRILL_DATABASE=catecommerce
FORBIDDEN_TABLE_COUNT="$(sql -c "SELECT count(DISTINCT tgrelid) FROM pg_trigger WHERE tgname = 'trg_refuse_hard_delete' AND NOT tgisinternal")"
sql -c "CREATE TABLE drill.rows (n int, ts timestamptz DEFAULT clock_timestamp()); CREATE TABLE drill.live (n serial, ts timestamptz)" >/dev/null

# ---- I-G2: point-in-time recovery to a recorded instant --------------------------------------------------------------------------------------------------------------
echo "== I-G2 PITR" >&2
sql -c "INSERT INTO drill.rows(n) SELECT g FROM generate_series(1,1000) g" >/dev/null
dx /opt/purrcare/scripts/backup.sh full
sql -c "INSERT INTO drill.rows(n) SELECT g FROM generate_series(1001,2000) g" >/dev/null
target="$(sql -c "SELECT to_char(clock_timestamp() AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS.US') || '+00'")"
sleep 1
sql -c "INSERT INTO drill.rows(n) SELECT g FROM generate_series(2001,3000) g" >/dev/null
sql -c "SELECT pg_switch_wal()" >/dev/null
sleep 5
dx pgbackrest --stanza=purrcare check >/dev/null
"${compose[@]}" stop -t 60 pg >/dev/null
assert_clean_stop
restore_started=$(date +%s)
restore_pitr_or_abort g2 --target "$target" --confirm --i-understand-this-replaces-production
restore_seconds=$(( $(date +%s) - restore_started ))
g2_count="$(query_restored 'SELECT count(*) FROM drill.rows' 2>&1 | tail -n 1)"
check I-G2 "exactly the rows up to T exist after the restore (2000 of 3000; found: $g2_count)" test "$g2_count" = "2000"

# ---- I-G3: kill uncleanly while writing; restore to the latest; measure the loss -------------------------------------------------------------------------------------------
echo "== I-G3 crash and restore to latest (about 80 s of writes)" >&2
"${compose[@]}" up -d pg >/dev/null
wait_ready
dx /opt/purrcare/scripts/backup.sh full
last_committed=""
for _ in $(seq 1 16); do
  last_committed="$(sql -c "INSERT INTO drill.live(ts) VALUES (clock_timestamp()) RETURNING to_char(ts AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS.US')" | head -n 1)"
  sleep 5
done
docker kill "$container" >/dev/null
# The killed container must really be dead before its stale postmaster.pid is removed (row 55: remove the file only once no postgres process is running).
[ "$(docker inspect -f '{{.State.Running}}' "$container")" = "false" ] || { echo "ABORT: the drill container is still running after docker kill" >&2; exit 1; }
oneoff bash -c 'rm -f "$PGDATA/postmaster.pid"; /opt/purrcare/scripts/restore-pitr.sh --target latest --confirm --i-understand-this-replaces-production' >/dev/null
restored_last="$(query_restored "SELECT to_char(max(ts) AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS.US') FROM drill.live")"
loss_seconds="$(date -u -d "$last_committed" +%s) - $(date -u -d "$restored_last" +%s)"
loss_seconds=$(( loss_seconds ))
echo "measured data loss after an unclean kill: ${loss_seconds}s (archive_timeout is 60s)" >&2
check I-G3 "data loss after an unclean kill is within 150 s -- far inside the 15-minute RPO (measured ${loss_seconds}s)" test "$loss_seconds" -le 150

# ---- I-G8 (journal) and the data the restore drill needs, on the running cluster -------------------------------------------------------------------------------------
echo "== I-G8 erasure journal" >&2
"${compose[@]}" up -d pg >/dev/null
wait_ready
sql -c "SELECT drill.insert_default('ops','erasure_request', jsonb_build_object('requested_on','now()'))" >/dev/null
day="$(date -u +%Y-%m-%d)"
check I-G8 "export-erasure-journal.sh succeeds" dx /opt/purrcare/scripts/export-erasure-journal.sh
journal1="$(docker exec "$container" cat "/var/lib/pgbackrest/erasure-journal/$day.csv" 2>/dev/null || true)"
journal2="$(dx bash -c 'openssl enc -d -aes-256-cbc -pbkdf2 -pass env:REPO2_CIPHER_PASS -in /var/lib/pgbackrest/repo2-store/erasure-journal/'"$day"'.csv.enc' 2>/dev/null || true)"
check I-G8 "the journal is present in repo1 and repo2 and identical" test -n "$journal1" -a "$journal1" = "$journal2"
check I-G8 "the journal header is ids and dates only" test "$(printf '%s\n' "$journal1" | head -n 1 | tr -d '\r')" = "customer_account_id,requested_on"
check I-G8 "every journal row is exactly two fields: a uuid and a timestamp" \
  bash -c 'printf "%s\n" "$0" | tail -n +2 | grep -q . && ! printf "%s\n" "$0" | tail -n +2 | grep -vE "^[0-9a-f-]{36},[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9:.]+\+00$"' "$journal1"
check I-G8 "the run was reported to ops.backup_run" test "$(sql -c "SELECT count(*) FROM ops.backup_run WHERE kind = 'erasure-journal' AND succeeded")" -ge 1

# ---- I-G4: the restore drill itself; the JSON is what POST /api/admin/operations/restore-tests accepts -----------------------------------------------------------------
echo "== I-G4 restore drill (a background writer keeps the audit trail moving for ~7 minutes)" >&2
audit_writer() {
  while true; do
    docker exec -i "$container" psql -U postgres -d catecommerce -q -At -c "SELECT drill.insert_default('audit','audit_entry', jsonb_build_object('OccurredOn','clock_timestamp()'))" >/dev/null 2>&1 || true
    sleep 20
  done
}
audit_writer & writer_pid=$!
trap 'kill "$writer_pid" 2>/dev/null || true; cleanup' EXIT
dx /opt/purrcare/scripts/backup.sh full
sleep 400
kill "$writer_pid" 2>/dev/null || true
dx pgbackrest --stanza=purrcare check >/dev/null
"${compose[@]}" stop -t 60 pg >/dev/null
assert_clean_stop
if oneoff /opt/purrcare/scripts/restore-drill.sh > "$out/drill.json" 2> "$out/drill.err"; then
  check I-G4 "restore-drill.sh emits the C12-shaped JSON" jqfile "$out/drill.json" -e '(.checks | length >= 5) and (.checks | map(.name) | index("migrations-current") != null)'
  check I-G4 "every drill check passed" jqfile "$out/drill.json" -e '[.checks[] | .passed] | all'
  check I-G4 "the drill restored from the latest backup in repo2 (off-site stand-in)" jqfile "$out/drill.json" -e '.backupSetLabel | length > 0'
else
  fail I-G4 "restore-drill.sh failed: $(tail -n 3 "$out/drill.err" | tr '\n' ' ')"
fi

# ---- I-G5: repo2 alone is enough --------------------------------------------------------------------------------------------------------------------------------------------
echo "== I-G5 restore from repo2 only" >&2
oneoff bash -c 'rm -rf /var/lib/pgbackrest/archive /var/lib/pgbackrest/backup'
check I-G5 "repo1 is gone" test "$(oneoff bash -c 'ls /var/lib/pgbackrest | grep -cE "^(archive|backup)$" || true')" = "0"
assert_clean_stop   # restore-drill.sh stopped its own server with pg_ctl before it exited
oneoff bash -c 'rm -f "$PGDATA/postmaster.pid"; /opt/purrcare/scripts/restore-pitr.sh --target latest --repo 2 --confirm --i-understand-this-replaces-production' >/dev/null
check I-G5 "the restore from repo2 alone succeeds and the data is there" test "$(query_restored 'SELECT count(*) FROM drill.rows')" = "2000"

# ---- I-G6: without the passphrase the repository cannot be read -------------------------------------------------------------------------------------------------------------
echo "== I-G6 wrong passphrase" >&2
if CIPHER_OVERRIDE="$(random)" oneoff /opt/purrcare/scripts/restore-pitr.sh --target latest --repo 2 --confirm --i-understand-this-replaces-production >/dev/null 2>&1; then
  fail I-G6 "a restore with the wrong passphrase must fail"
else
  pass I-G6 "a restore with the wrong passphrase fails: repo2 is encrypted"
fi

# ---- summary -------------------------------------------------------------------------------------------------------------------------------------------------------------------------
rto_seconds="$(jqfile "$out/drill.json" -r '(.restoreCompletedAt | fromdateiso8601) - (.restoreStartedAt | fromdateiso8601)' 2>/dev/null || echo null)"
cat > "$out/measurements.json" <<JSON
{ "pitrRestoreSeconds": $restore_seconds, "crashDataLossSeconds": $loss_seconds, "drillRestoreSeconds": $rto_seconds, "archiveTimeoutSeconds": 60 }
JSON
echo "measurements: $(cat "$out/measurements.json")" | tee -a "$results" >&2
echo "results in $out" >&2
[ "$failures" -eq 0 ]
