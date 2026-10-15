GATE: TEST
HANDOVER: HO-XCT-001
COMMITS: a9282e6d (head under test, branch ho/XCT-001-build) plus the gate's own files: docs/test-cases/TC-HO-XCT-001.md (section "TEST gate"), deploy/backup/test/restore-pitr-guard-test.sh
DATE: 2026-10-12
STATUS: PASS on RE-CHECK of 3468c46a (see the section at the end). First pass 4b55a457 was FAIL (1 HIGH, test-only: backlog row 53; 1 MEDIUM and 2 LOW go to docs/backlog/XCT.md rows 52, 54, 55)
SHEET: docs/test-cases/TC-HO-XCT-001.md (section "TEST gate" and its summary)

ENVIRONMENT
- Build green (0 errors). Database `catecommerce_xct` only (full connection string in CATECOMMERCE_TEST_DB); never dev or test.
- Unit: Domain 1919, Application 435, Infrastructure 189 -- all passed.
- Integration, one full run: 1513 total, 1497 passed, 11 failed, 5 skipped (BackupDrill fact by default, four pre-existing skips).
- Docker Desktop was down when the session resumed after the parent restart; it was restarted, and the stale `purrcare-drill-pg` container and two `purrcare-drill_*` volumes were removed. Drills ran only while no other test host was running.
- The gate added 13 cases (TG-1 to TG-13) to the sheet before running anything.

RESULTS (TG rows): 13 total, 11 PASS, 2 FAIL, 0 BLOCKED, 0 NOT RUN.
- PASS: TG-1..TG-8, TG-10, TG-11, TG-13.
- FAIL: TG-9 (runbook, MEDIUM row 52), TG-12 (full suite red, HIGH row 53).

FAILURES OF THE FULL RUN (11) AND WHAT EACH WAS
- 7 x `XctSnapshotEndpointTests` (U_D3, I_D1, I_D2_D3, I_D4, I_D5, I_D8, no-branch-header): fail alone too, deterministically (`SeedWorldAsync` line 37, expected 201, got 401). HIGH, row 53. The fake clock is fixed at 2026-10-12 04:00Z; with the baseline moved to the real clock 5 of 7 pass (so it is the test, not production; the other 2 then hit only `SlotNotOnGrid` from the probe). The class passed earlier on 2026-10-12; it is red for every run after the real clock moved away from the fixed instant. Not a product defect.
- 1 x `DeployConventionTests.EveryShellScript_...`: caused by the gate's own new script (`set -uo pipefail`); the check did its job. Script fixed to `set -euo pipefail`; 5 of 5 pass.
- `ShopCataloguePerformanceTests.I_E11`: fails alone 3 of 3 here (p95 546-585 ms vs 500 ms). Shop test, known load flake, LOW row 54.
- `WaitlistFlowTests` IG3 and Offer_Dispatches: pass alone (8 of 8). Known flake.
- I_E4, CustomerAuth lockout, I_D13: did not fail in the run.

BACKUP DRILL (`bash deploy/backup/test/run-drill-test.sh`, two further full runs, both on the head under test)
- Run A (KEEP=1) and run B: exit 0; I-G1, I-G2 (found 2000 of 3000 both times; `g2-restore.log` shows "completed successfully"), I-G3 (27 s data loss both runs), I-G4, I-G5, I-G6, I-G8 all PASS. PITR restore 2 s, drill restore 4 s.
- On run A's volumes the gate then forced the failure by hand (TG-7): a fake `postmaster.pid`, real pgBackRest 2.59.3, `restore-pitr.sh --target latest --repo 2 ...` gave `ERROR: [038]: unable to restore while PostgreSQL is running` plus HINTs, script exit 38, data directory file list unchanged.
- `XctBackupDrillTests` fact, run with RUN_BACKUP_DRILL=1 and BACKUP_DRILL_RESULTS=<run B>: Passed (the real endpoint accepts the drill JSON as Passed).
- Reproducibility of the first-attempt failure: 0 of 2 gate runs (3 clean runs counting the build agent's). The original cause is not proven; the refusal mechanism is proven and deterministic. The gate's runs were on a quiet machine; the first failure was probably under load, and a loaded-machine run was not attempted (no second suite may run beside the drill).

RULING ON BACKLOG ROW 50 (can a restore that refuses go unnoticed?)
- In the production scripts it cannot go unnoticed: pgBackRest exits 38 with an ERROR and two HINT lines; `restore-pitr.sh` runs under `set -euo pipefail`, propagates the status, and prints "Restore to ... finished" only after success (TG-4 with a stub, TG-7 with the real tool). Only a wrapper that discards the exit status could miss it.
- In the drill: I-G2 swallows the exit status but then checks the row count, and that check is what caught it (3000 vs 2000); I-G3 and I-G5 run bare under `set -e` and abort; so the drill cannot pass over a refused restore (TG-10).
- Real residual: the runbook (section 4 step 3) verifies only `pg_is_in_recovery() = false`, which an unrestored cluster also satisfies, and step 2 tells the operator to delete `postmaster.pid` with no instruction to check that no postgres process is running. An operator who missed the error would see a passing check. MEDIUM (row 52), not HIGH: the failure is loud and nothing is silent in code.
- Row 50 stays MEDIUM; its mitigations are not applied yet (row 55, LOW).

EXISTING TESTS JUDGED
- `DeployConventionTests` is not decoration: it caught the gate's own non-conforming script.
- `XctSnapshotEndpointTests` asserts well but is time-fragile (row 53). Not edited or weakened; a local probe edit was reverted.
- Added `deploy/backup/test/restore-pitr-guard-test.sh` (10 assertions with a stub pgbackrest: exit codes, arguments, stub not called on refusals, no "finished" on failure). Not wired into the dotnet suite; run it with bash.

PRODUCTION CODE: no defect found in production code; nothing production was changed.

BLOCKED CASES: none. (S3 transport remains un-exercised, backlog row 5; I-D6 deferred by owner decision.)

RE-CHECK (2026-10-13, range 4b55a457..3468c46a, head 3468c46a)
STATUS: PASS -- no BLOCKER or HIGH open. Row 53 (HIGH) is closed; rows 52 and 55 are closed; no new row.
- Diff read: `XctSnapshotEndpointTests` baseline is now 10:00 Asia/Dhaka on the next local day derived from the real clock (always in the future, on the grid; the `localDate` / `validUntil` assertions follow it); `restore-pitr.sh` refuses with exit 4 when `postmaster.pid` names a live process and warns on a stale one; the drill uses `stop -t 60`, asserts the pid file is gone, aborts on a non-zero or unfinished restore, and `wait_ready` waits for `pg_is_in_recovery() = false`; runbook step 2 and 3 now require exit 0 plus the "finished" line, a no-postgres-process check before removing the pid, and a positive proof (timeline etc.).
- Build: 0 errors.
- `restore-pitr-guard-test.sh`: all assertions pass, including the new live-pid (exit 4, pgbackrest not called), stale-pid and stale-plus-refusal cases (the builder numbered them TG-7a..c in the script; they are not the sheet's TG-7, which is the real-pgBackRest case, still PASS from the first run).
- `XctSnapshotEndpointTests` + `DeployConventionTests`: 12 of 12 pass, twice in a row. All `Xct*` classes together: 56 passed, 1 skipped (the BackupDrill fact), 0 failed.
- Full integration suite once on `catecommerce_xct`: 1513 total, 1505 passed, 3 failed, 5 skipped. The 3 failures are the known load flakes and pass alone: WaitlistFlowTests IG3 and IG1 (8 of 8 alone), I_E11 (passed alone, 1 of 1; it had failed alone 3 of 3 on 2026-10-12, so it is load dependent). No Xct* failure.
- Drill: not re-run by the gate (not asked, evidence not doubted for the scripts; the drill-script edits are verified by reading and by the guard test only). Row 50's root-cause claim (`wait_ready` returning during read-only recovery) is the builder's explanation, plausible and consistent with the original symptom, but not independently proven by the gate; the builder reports one passing re-run. The refusal itself is loud (first-run TG-7).
- TG-9 now satisfied by the runbook text (read, not executed). TG-12 now PASS apart from the known flakes.
