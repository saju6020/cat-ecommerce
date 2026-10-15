#!/usr/bin/env bash
# TEST gate HO-XCT-001 (TG-1 .. TG-6): the guards and the failure behaviour of restore-pitr.sh, against a STUB pgbackrest on PATH.
# No Docker, no database. usage: bash deploy/backup/test/restore-pitr-guard-test.sh   (exit 0 only if every case passed)
set -euo pipefail
here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
script="$here/../scripts/restore-pitr.sh"
work="$(mktemp -d)"; trap 'rm -rf "$work"' EXIT
mkdir -p "$work/bin" "$work/pgdata"
log="$work/calls.log"
cat > "$work/bin/pgbackrest" <<STUB
#!/usr/bin/env bash
echo "\$*" >> "$log"
if [ -n "\${STUB_FAIL:-}" ]; then echo "ERROR: [056]: unable to restore while PostgreSQL is running" >&2; exit 56; fi
exit 0
STUB
chmod +x "$work/bin/pgbackrest"
export PATH="$work/bin:$PATH" PGDATA="$work/pgdata"
failures=0
ok() { echo "PASS $1"; }
bad() { echo "FAIL $1"; failures=$((failures + 1)); }
run() { : > "$log"; rc=0; out="$("$@" 2>&1)" || rc=$?; calls="$(cat "$log")"; }

run bash "$script" --target "2026-10-12 11:52:00+06"
{ [ "$rc" = 2 ] && [ -z "$calls" ]; } && ok "TG-1 no --confirm: exit 2, pgbackrest not called" || bad "TG-1 rc=$rc calls=[$calls]"

run bash "$script" --confirm
{ [ "$rc" = 2 ] && [ -z "$calls" ]; } && ok "TG-2a no --target: exit 2, not called" || bad "TG-2a rc=$rc calls=[$calls]"
run bash "$script" --target latest --confirm --bogus
{ [ "$rc" = 2 ] && [ -z "$calls" ]; } && ok "TG-2b unknown argument: exit 2, not called" || bad "TG-2b rc=$rc calls=[$calls]"

echo "archive_command = 'pgbackrest ... purrcare-production'" > "$PGDATA/postgresql.auto.conf"
run bash "$script" --target latest --confirm
{ [ "$rc" = 3 ] && [ -z "$calls" ]; } && ok "TG-3a production marker without the flag: exit 3, not called" || bad "TG-3a rc=$rc calls=[$calls]"
run bash "$script" --target latest --confirm --i-understand-this-replaces-production
{ [ "$rc" = 0 ] && [ -n "$calls" ]; } && ok "TG-3b production marker with the flag: restore called" || bad "TG-3b rc=$rc calls=[$calls]"
rm -f "$PGDATA/postgresql.auto.conf"

STUB_FAIL=1 run bash "$script" --target "2026-10-12 11:52:00+06" --confirm
if [ "$rc" = 56 ] && ! grep -q "finished" <<<"$out" && grep -q "unable to restore while PostgreSQL is running" <<<"$out"; then ok "TG-4 refused restore: exit 56, error shown, no 'finished' line"; else bad "TG-4 rc=$rc out=[$out]"; fi

run bash "$script" --target "2026-10-12 11:52:00+06" --confirm
grep -q -- "--type=time --target=2026-10-12 11:52:00+06 --target-action=promote --delta" <<<"$calls" && ! grep -q -- "--repo" <<<"$calls" && ok "TG-5a time target args" || bad "TG-5a calls=[$calls]"
run bash "$script" --target latest --confirm
{ grep -q -- "restore --delta" <<<"$calls" && ! grep -q -- "--type" <<<"$calls" && ! grep -q -- "--target" <<<"$calls"; } && ok "TG-5b latest: no --type/--target" || bad "TG-5b calls=[$calls]"
run bash "$script" --target latest --repo 2 --confirm
grep -q -- "--repo=2" <<<"$calls" && ok "TG-5c --repo passed through" || bad "TG-5c calls=[$calls]"

run bash "$script" --target latest --confirm
{ [ "$rc" = 0 ] && grep -q "finished" <<<"$out" && grep -q "reconciliation" <<<"$out"; } && ok "TG-6 success: exit 0 and the reconciliation reminder" || bad "TG-6 rc=$rc out=[$out]"

# TG-7 (row 55): a postmaster.pid naming a LIVE process refuses the restore; a stale one only warns and leaves the refusal to pgBackRest.
echo "$$" > "$PGDATA/postmaster.pid"
run bash "$script" --target latest --confirm
{ [ "$rc" = 4 ] && [ -z "$calls" ] && grep -q "is running" <<<"$out"; } && ok "TG-7a live postmaster.pid: exit 4, pgbackrest not called" || bad "TG-7a rc=$rc calls=[$calls]"
echo "2147483646" > "$PGDATA/postmaster.pid"
run bash "$script" --target latest --confirm
{ [ "$rc" = 0 ] && grep -q "stale" <<<"$out" && [ -n "$calls" ]; } && ok "TG-7b stale postmaster.pid: warns, pgbackrest decides" || bad "TG-7b rc=$rc out=[$out]"
STUB_FAIL=1 run bash "$script" --target latest --confirm
{ [ "$rc" = 56 ] && ! grep -q "finished" <<<"$out"; } && ok "TG-7c stale file plus pgBackRest refusal: non-zero, no 'finished'" || bad "TG-7c rc=$rc out=[$out]"
rm -f "$PGDATA/postmaster.pid"

echo "failures: $failures"
[ "$failures" -eq 0 ]
