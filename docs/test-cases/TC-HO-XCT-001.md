# TC-HO-XCT-001 — Cross-cutting and Technical (backend)

Handover: docs/handover/HO-XCT-001.md  ·  Branch: ho/XCT-001-build (from master a8761829)
Written: 2026-10-11 (before any production code)  ·  Executed: see the Status column and the Summary at the end of this file

> Statuses record what was **observed**. `PLANNED` = written before the build, not yet run.
> The build agent replaces each `PLANNED` with PASS / FAIL / BLOCKED / DEFERRED and a test name.
> Case ids are the handover's §15/§16 ids. Rows marked **(adapted)** keep the intent of the
> handover case but differ in mechanism because the repository (not the handover text) is the
> ground truth — the reason is stated in the row.

## Environment

- Per-branch database `catecommerce_xct` (`CATECOMMERCE_TEST_DB` = its full connection string). The XCT factory refuses
  `catecommerce_dev` and `catecommerce_test` by name.
- Unit suites need no database. Integration suite: real HTTP through TestServer, workers off, `RetentionWorkCycle` driven by hand.
- Owner decision 2026-10-10: Boarding (BRD) and Subscriptions (SUB) are skipped; their duties are unregistered and nothing here invents them.
  HO-SRG-001 is on another branch and is not depended on.
- Docker backup drill (I-G*) needs image pulls and an isolated host: see section G.

## A — classification and guards

| ID | Case | Expected | Test home | Status |
|---|---|---|---|---|
| U-A1 | Every `(schema, table)` of the EF model (owned included, views excluded) vs `RecordClassification` | Set equality, no duplicates | Infrastructure.Tests `RecordClassificationConventionTests` | PASS |
| U-A2 | Source scan of `src/**/*.cs` | No `ExecuteDelete`, raw DELETE/TRUNCATE, `DISABLE TRIGGER`, `session_replication_role` outside the allow-list (**adapted**: `EfIdempotencyStore.cs`, Ordering's purge of `ops.idempotency_record`, is a named, reasoned exception) | Infrastructure.Tests `HardDeleteSourceScanTests` | PASS |
| U-A3 | Interceptor: `Remove(cat)` | `ProtectedRecordDeletionException` before SQL | Infrastructure.Tests `HardDeleteGuardInterceptorTests` | PASS |
| U-A4 | Interceptor: owned allergy removed via `UpdateMedicalProfile` | Allowed | same | PASS |
| U-A5 | Interceptor: owned `CustomerDeliveryAddress` removed with owner untracked | Refused | same | PASS |
| U-A6 | Interceptor: tracked `Remove` of a `customer_session` row | Refused | same | PASS |
| U-A7 | Every `Forbidden` entry | In `AddRetentionGuards` literal list, or `audit.audit_entry`, or attached by a later migration | Infrastructure.Tests `RetentionGuardMigrationTests` | PASS |
| U-A8 | `RetentionPurger` table set | Every purger table is `Purgeable` | same | PASS |
| I-A1 | Host starts with scope validation | All registrations resolve incl. erasers, worker, purger | IntegrationTests `XctGuardEndpointTests` | PASS |
| I-A2 | Migrations on an empty database | Function + both triggers on every `Forbidden` table except `audit.audit_entry`; `ops` tables and indexes | IntegrationTests `XctSchemaMigrationTests` | PASS |
| I-A3 | For **every** `Forbidden` table: `pg_trigger` | BEFORE DELETE row trigger and BEFORE TRUNCATE statement trigger (every table, not a sample) | same | PASS |
| I-A4 | Raw DELETE of own cat; TRUNCATE of `clinic.appointment` in a rolled-back txn | Both `P0001` "Hard delete … not permitted" | `XctGuardEndpointTests` | PASS |
| I-A5 | Test-only controller calling `db.Remove(cat)` | `409 RecordIsRetained`, row still exists | same | PASS |
| I-A6 | `UpdateMedicalProfile` removes an allergy through the shipped endpoint | `200`, allergy row gone | same | PASS |
| I-A7 | `DatabaseRoleGuard` in Production as superuser/owner | Startup fails with the stated message | `XctRoleGuardTests` | PASS |
| I-A8 | Guard with a `roles.sql` role in a scratch database | Starts; role's `ALTER TABLE … DISABLE TRIGGER` refused | same | PASS |

## B — erasure ledger

| ID | Case | Expected | Test home | Status |
|---|---|---|---|---|
| U-B1 | `Open` with 3 keys | 3 `Pending` scopes; event ids only | Domain.Tests `ErasureRequestAggregateTests` | PASS |
| U-B2 | `Held(tomorrow)` / `Held(today)` | `Held` / `InvalidHoldDate` | same | PASS |
| U-B3 | Last scope completed | `Completed`, completed event once | same | PASS |
| U-B4 | `AddMissingScopes` on completed request | Reopened, new scope `Pending` | same | PASS |
| U-B5 | C2 handler, eraser throws | `Failing`, `UnhandledEraserException`, attempts 1, nothing escapes | Application.Tests `ExecuteErasureScopeCommandHandlerTests` | PASS |
| U-B6 | Aggregate with `*CustomerId`/`*CustomerAccountId`/`OwnerId` | Context has an eraser **or** an exemption (**adapted**: the per-area gap list is the deliverable; BRD/SUB are owner-skipped and listed, not invented) | Infrastructure.Tests `ErasureCoverageConventionTests` | PASS |
| U-B7 | Eraser contract suite over each registered eraser | Idempotent; `Completed` for unknown customer; never throws; logs no value | IntegrationTests `XctErasureContractTests` | PASS |
| U-B8 | Administration eraser: 7-year and 1-year acceptance, financialYears 6 | Old redacted; `Held(young + 6y)` | IntegrationTests `XctErasureEndpointTests` | PASS |
| U-B9 | C3: active / anonymised / merged / unknown | Anonymise + audit / `isReapplication` / `CustomerRecordMerged` / `CustomerAccountNotFound` | Application.Tests `EraseCustomerOnRequestCommandHandlerTests` | PASS |
| U-B10 | C4: 501 ids; 3 ids one unknown | `ReapplyListTooLarge`; `{processed:2,notFound:1}` continues | Application.Tests | PASS |
| U-B11 | Ledger-open source derivation | `SelfService` / `StaffOnBehalf` / `RestoreReconciliation` | Application.Tests | PASS |
| I-B1 | Customer self-deletes | Ledger with scopes, source `SelfService` | `XctErasureEndpointTests` | PASS |
| I-B2 | Erasure pass | account, notification `Completed`; administration `Held` if accepted else `Completed` | same | PASS |
| I-B3 | Advance clock past hold; pass | IP null; request `Completed`; `ErasureRequestCompleted` BusinessEvent | same | PASS |
| I-B4 | Clinical/financial rows after erasure | Still present | same | PASS |
| I-B5 | Admin erasure with channel + identity check | `200 {isReapplication:false}`; audit row; source `StaffOnBehalf` | same | PASS |
| I-B6 | Same again | `200 {isReapplication:true}`; nothing written | same | PASS |
| I-B7 | Receptionist | `403`, `AuthorizationDenied` row | same | PASS |
| I-B8 | Ledger handler forced to fail | Request fails as ACC describes; next pass self-heals | same | PASS |
| I-B9 | Reapply: anonymised + active + unknown | `{processed:2,alreadyErased:1,notFound:1}`; source `RestoreReconciliation` | same | PASS |
| I-B10 | `GET erasure-requests/{id}` | Scopes with status/heldUntil/basis; no name/phone/email | same | PASS |

## C — worker and purges

| ID | Case | Expected | Test home | Status |
|---|---|---|---|---|
| U-C1 | `RetentionRun` transitions out of order | Each refused | Domain.Tests `RetentionRunAggregateTests` | PASS |
| U-C2 | C5 while a run is `Running` | `RetentionRunAlreadyInProgress` | Application.Tests | PASS |
| U-C3 | Work cycle with a throwing unit | Logged; next unit runs; run `CompletedWithErrors` | IntegrationTests `XctRetentionWorkerTests` | PASS |
| U-C4 | Daily trigger, Asia/Dhaka, sweepHour 2, 01:59 / 02:00 / 02:30 | No / yes / no | Application.Tests `RetentionSchedulingTests` | PASS |
| U-C5 | Erasure back-off | Retried at 15, 60, 240, 1440 min | same | PASS |
| U-C6 | `CatClinicalReviewSource` | Counts 2; oldest anchor (adapted: CreatedDate cannot be back-dated through the repository, so the integration test seeds cats deceased 8 y / 3 y ago and asserts the count delta of exactly 1) | IntegrationTests | PASS |
| I-C1 | Own ended sessions 40 d / 10 d, securityArtefactDays 30 | 40 d gone, 10 d stays; BusinessEvent row | `XctRetentionWorkerTests` | PASS |
| I-C2 | Own notification 25 months / 1 month | Old `[redacted]`, destination null; young untouched | same | PASS |
| I-C3 | Web-vital batches 100 d / 10 d | Old purged, young kept | `XctWebVitalsEndpointTests` (run in Phase E) | PASS |
| I-C4 | Fake `IRetentionPurgeSource` | Called with right cutoff; result on the pass | same | PASS |
| I-C5 | `POST retention/runs` twice | `201` then `409` | same | PASS |
| I-C6 | `GET runs/{id}` | Passes with counts; `Succeeded` | same | PASS |
| I-C7 | `GET retention/review` | Cat-clinical row; the "nothing is deleted automatically" note | same | PASS |
| I-C8 | `GET retention/schedule`; lower `retention.clinicalYears` | Reflects immediately; DataChange row | same | PASS |
| I-C9 | Clinical rows after C1..C3 | Still present | same | PASS |

## D — offline snapshot and ping

| ID | Case | Expected | Test home | Status |
|---|---|---|---|---|
| U-D1 | ETag, different `generatedOn` | Same | Application.Tests `OfflineSnapshotEtagTests` | PASS |
| U-D2 | ETag, one status change | Different | same | PASS |
| U-D3 | Sections: Caretaker vs Reception | `appointments` `NotPermitted` / present | same | PASS |
| U-D4 | No boarding source | `boardingTasks` `NotAvailable` | same | PASS |
| U-D5 | `validUntil` Asia/Dhaka 2026-10-12 | `2026-10-13T02:00:00+06:00` | same | PASS |
| U-D6 | Clinic source status filter | Held/Cancelled out; walk-ins in; handlingNote only if high-risk | IntegrationTests | PASS |
| I-D1 | Receptionist, 3 appointments (1 cancelled) | `200`, 2 items sorted, ETag, `no-store, private`, audit row | `XctSnapshotEndpointTests` | PASS |
| I-D2 | `If-None-Match` | `304`, no new audit row | same | PASS |
| I-D3 | Check one in; old ETag | `200`, new ETag | same | PASS |
| I-D4 | Poll snapshot + ping for 40 fake minutes, idle 30 | `401 session_idle_timeout` at minute 31 | same | PASS |
| I-D5 | Customer / anonymous / Inventory staff | `403` (customer, adapted: RequireActionFilter answers 403 to an authenticated customer, ACC U-A6), `401`, `403` | same | PASS |
| I-D6 | Caretaker with boarding | **DEFERRED** — Boarding skipped by owner (2026-10-10) | — | DEFERRED |
| I-D7 | No boarding source | `boardingTasks` omitted `NotAvailable` | `XctSnapshotEndpointTests` | PASS |
| I-D8 | Ping anonymous and staff | `204`, `no-store`, no body; `lastActivityOn` unchanged | same | PASS |
| I-D9 | Body scan of I-D1 | No field beyond §12.4 | same | PASS |

## E — web vitals

| ID | Case | Expected | Test home | Status |
|---|---|---|---|---|
| U-E1 | Validator: `?q=`, `@`, 201 chars, 11 samples, unknown metric, NaN | `InvalidWebVitalsPayload` each | Application.Tests | PASS |
| U-E2 | C11 with a signed-in customer context | `CreatedBy == Guid.Empty` | Application.Tests | PASS |
| U-E3 | Verdicts 49 samples / p75 2400 / 2600 | InsufficientData / Pass / Fail | Application.Tests | PASS |
| I-E1 | POST JSON 3 samples | `204`; 1 batch 3 samples; no identity columns | `XctWebVitalsEndpointTests` | PASS |
| I-E2 | `text/plain` + customer cookie, no CSRF header | `204` | same | PASS |
| I-E3 | 5 KB; 11 samples; route with query | `413`; `400`; `400` | same | PASS |
| I-E4 | 31 posts in a minute | 31st `429` + `Retry-After` | same | PASS |
| I-E5 | 60 LCP samples; summary | p75 = independent `percentile_cont`; verdict | same | PASS |
| I-E6 | Log capture during I-E1..E4 | No IP / user id logged | same | PASS |
| I-E7 | Change-capture exclusion | No DataChange row for batches | same | PASS |

## F — restore tests and status

| ID | Case | Expected | Test home | Status |
|---|---|---|---|---|
| U-F1 | Failure 12:00, recovered 11:52, restore 40 min, all pass | RPO 8, RTO 40, `Passed` | Domain.Tests `RestoreTestRecordAggregateTests` | PASS |
| U-F2 | RPO 16 | `Failed` | same | PASS |
| U-F3 | RTO 241 | `Failed` | same | PASS |
| U-F4 | One check failed | `Failed` | same | PASS |
| U-F5 | Bad times / no checks | `InvalidRestoreTestTimes` ×2 / `RestoreTestChecksRequired` | same | PASS |
| U-F6 | `Record` twice | `RestoreTestAlreadyRecorded` | same | PASS |
| U-F7 | Due date: last passed 2026-07-01, interval 92 | Due 2026-10-01; failure does not reset | Application.Tests | PASS |
| I-F1 | POST restore-tests with passing drill JSON | `201`, computed RPO/RTO, `Passed`, performer = caller, DataChange | `XctOperationsEndpointTests` | PASS |
| I-F2 | `backup-status`, no test ever | `isRestoreTestOverdue:true`, due today | same | PASS (adapted: see docs/backlog/XCT.md row 8; the never-tested rule is U-F7) |
| I-F3 | Insert `backup_run` as `catecommerce_backup_reporter` | Insert works; SELECT/UPDATE/DELETE refused | `XctDatabaseRoleAndSchemaTests.I_F3` (scratch database, roles.sql) | PASS |
| I-F4 | Latest diff 27 h old | `isBackupStale:true` | `XctOperationsEndpointTests` | PASS |
| I-F5 | Accountant calls backup status | `403` | same | PASS |

## X — conventions

| ID | Case | Expected | Test home | Status |
|---|---|---|---|---|
| U-X1 | Every new event (reflection) | No string property other than codes/keys | Domain.Tests `OperationsEventConventionTests` | PASS |
| U-X2 | `SettingDefinitions` nine new keys | Defaults in bounds; keys unique | Domain.Tests | PASS |
| U-X3 | Every new endpoint declares its access (existing `EndpointAccessConventionTests` still passes) | Pass | existing convention test | PASS (EndpointAccessConventionTests passes with the new controllers) |

## G — backup drill (Docker, category `BackupDrill`) — needs image pulls and an isolated host

Executed three times on 2026-10-12/13 with `bash deploy/backup/test/run-drill-test.sh` (Docker 29, `postgres:17-bookworm`, pgBackRest 2.59.3 from PGDG) against the real EF schema: first on the original head, then on the gate-fixed head 95fc72b1 plus the report-run fix (the ARCHITECTURE re-check required it). Latest run, measured, not assumed:

| Measurement | Value |
|---|---|
| Data loss after an unclean `docker kill` while writing every 5 s (`archive_timeout = 60`) | **21 s** (first run 27 s; RPO objective 15 min) |
| Drill RPO from the audit trail (`recoveredTo` against the simulated failure) | **5.9 s** (0.1 min) |
| Restore of the latest backup from repo2, drill script, small database | 3 s (RTO objective 4 h; **not representative of production volume**, backlog row 6) |
| PITR restore | exact: 2 000 of 3 000 rows, the ones committed up to T |

| ID | Case | Status |
|---|---|---|
| I-G1 | Build image, start, `stanza-create`, `check` | PASS (both runs) |
| I-G2 | PITR to a recorded instant | PASS (on the gate-fixed head the first run FAILED with the cluster not restored -- found 3 000 rows, a restore aborted by a leftover `postmaster.pid`; it did not recur in the next full run (the one reported here; an intermediate run was disturbed by manual inspection and is not counted), and the script now records the restore output and the count found. See backlog row 50) |
| I-G3 | Unclean kill, restore to latest, measure | PASS (21 s; 27 s in the first run) |
| I-G4 | `restore-drill.sh` JSON, all checks passed; posted to the real endpoint by `XctBackupDrillTests` (reusing the run results) -> `201`, `Passed` | PASS |
| I-G5 | Restore from repo2 with repo1 deleted | PASS (adapted: repo2 is a posix repository with the production cipher, backlog row 5) |
| I-G6 | repo2 without the passphrase | PASS (restore fails; same adaptation) |
| I-G7 | No literal secret under `deploy/` | PASS (`DeployConventionTests`, default suite) |
| I-G8 | `export-erasure-journal.sh`: ids and dates only, in both places, reported | PASS (repo2 copy is the encrypted `.csv.enc`; ADR-314 note) |

Not exercised: the S3 transport (no S3-compatible image could be pulled; backlog row 5).

## Summary (2026-10-12, build agent)

Unit: Domain.Tests 1 919, Application.Tests 427, Infrastructure.Tests 189 -- all pass. Integration (`catecommerce_xct`): the Xct* classes (retention, erasure, snapshot, web vitals, operations, roles, deploy conventions) pass; one full run of the whole suite: 1 505 passed, 5 skipped (the BackupDrill fact unless `RUN_BACKUP_DRILL=1`, plus four pre-existing skips), 2 unrelated failures that pass in isolation (backlog row 13). The backup drill was executed in full once (section G) and its JSON accepted by the real endpoint.
Deferred (owner decision 2026-10-10, Boarding and Subscriptions skipped): I-D6, the `boardingTasks` section, the `boarding-media` purge source and the Boarding/Subscriptions erasers: `docs/backlog/XCT.md` rows 1-3.
Gates are not run by the build agent.

## Gate fix round (2026-10-12) -- added cases

| ID | Case | Status |
|---|---|---|
| I-A3 (re-run) | Every `Forbidden` table carries the two triggers, over the corrected set (the six line tables are now AggregateOnly, no exemption list) | PASS (`XctDatabaseRoleAndSchemaTests`, `XctGuardEndpointTests`) |
| U-A9 | Classification and enforcement agree: the six tables are CurrentState/AggregateOnly; no AggregateOnly/Purgeable table has the blanket trigger; the frozen `AddRetentionGuards` list is pinned (count + hash) | PASS (`ClassificationEnforcementAgreementTests`) |
| U-B12 | C3 identity check: only the four codes validate; free text and an empty value are refused; the audit row holds the code and no free text; the endpoint answers 400 to an identifier-bearing string | PASS (`ErasureCommandHandlerTests`, `XctErasureEndpointTests`) |
| U-B13 | C1 lost insert race: reload in a fresh scope, top up, succeed; a real failure with no ledger afterwards is not swallowed | PASS (`OpenErasureRequestRaceTests`) |
| I-B3 (extended) | The hold date equals the local (Asia/Dhaka) acceptance date plus the period | PASS (`XctErasureEndpointTests`) |
| U-C6 (extended) | Cat anchor: death, later visit, visit only, created only; a 20:30 UTC instant is the next day in Dhaka | PASS (`CatClinicalAnchorTests`) |
| I-C9 (rewritten) | Own cat and owner survive a pass; a raw DELETE of the cat is refused `P0001` (negative control) | PASS (`XctRetentionWorkerTests`) |
| I-A8 (extended) | `DatabaseRoleGuard` refuses a NOINHERIT member of the owning role | PASS (`XctDatabaseRoleAndSchemaTests`) |
| I-C8 (Q3) | Regulatory reports `ReviewOnly` | PASS (Q3 handler; schedule endpoint test) |

## TEST gate (2026-10-12, TEST agent) -- cases added before any execution

Written before the gate ran anything. Every row starts `NOT RUN`; the status column below is updated one row at a time as each case is observed. The build agent's PASS marks in sections A to G above are the build agent's statement; the gate re-observes them through the unit suites, the one full integration run and the drill runs listed here (see the gate's own summary at the end of this section).
The script-level cases (TG-1 to TG-6) use a stub `pgbackrest` on `PATH` (`deploy/backup/test/restore-pitr-guard-test.sh`): no Docker, no database. TG-7 uses the real pgBackRest in the drill's containers.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| TG-1 | `restore-pitr.sh --target X` without `--confirm` | exit 2, `pgbackrest` never called | PASS | restore-pitr-guard-test.sh: exit 2, stub never called |
| TG-2 | no `--target`; unknown argument | exit 2 each, `pgbackrest` never called | PASS | same script, TG-2a and TG-2b: exit 2, stub never called |
| TG-3 | `PGDATA` whose `postgresql.auto.conf` names the production cluster, without / with `--i-understand-this-replaces-production` | exit 3, not called / called | PASS | same script, TG-3a exit 3 not called; TG-3b called |
| TG-4 | `pgbackrest restore` fails (stub exits 56, "unable to restore while PostgreSQL is running") | the script exits non-zero (56) and never prints "Restore to ... finished" | PASS | same script: stub exit 56 -> script exit 56, error shown, no "finished" line |
| TG-5 | Argument shape: a time target; `latest`; `--repo 2` | time: `--type=time --target=<t> --target-action=promote --delta`; latest: no `--type`/`--target`; repo passed through | PASS | same script, TG-5a/b/c: argument shapes as expected |
| TG-6 | Success path | exit 0 and the "finished ... reconciliation" reminder is printed | PASS | same script: exit 0 and the reconciliation reminder printed |
| TG-7 | Real pgBackRest refuses to restore over a data directory holding a `postmaster.pid` | non-zero exit and an error naming the running cluster; the data directory is untouched | PASS | real pgBackRest 2.59.3 in the drill volumes, fake postmaster.pid: `ERROR: [038]: unable to restore while PostgreSQL is running` + HINT, script exit 38, data-directory file list identical before/after |
| TG-8 | Backlog row 50 reproducibility: `run-drill-test.sh` full run A and full run B on the gate head | I-G2 finds 2 000 rows; `g2-restore.log` shows the restore ran | PASS | drill A (KEEP=1) and drill B on gate head ba7d894e: both exit 0, I-G1..I-G6, I-G8 PASS, I-G2 found 2000 both times, g2-restore.log "restore command end: completed successfully"; the first-attempt failure did not recur in 2 of 2. Cause of the original refusal not reproduced (machine was quiet) |
| TG-9 | Runbook section 4 steps 2-3: is there a positive check that the restore happened (not only `pg_is_in_recovery() = false`, which an unrestored cluster also satisfies)? | A step that proves the restore ran (script exit status, the "finished" line, timeline / `recovery` log) | FAIL | Read of runbook section 4: step 3 checks only `pg_is_in_recovery() = false`, which an unrestored cluster also satisfies; no step says to check the script exit status or the "finished" line, and step 2 says to delete `postmaster.pid` without saying to confirm no postgres process is running. Backlog row 52 (MEDIUM) |
| TG-10 | Drill harness: can a refused restore pass the drill unnoticed (I-G2, I-G3, I-G5)? | Each restore failure either aborts the script or fails a check | PASS | Read of run-drill-test.sh: I-G2 swallows the exit (`|| echo`) but the row-count check then fails (that is how the first attempt was seen); I-G3 and I-G5 restores run bare under `set -e` and abort the drill; I-G4 failure -> `fail`; I-G6 inverted |
| TG-11 | Full unit suites (Domain, Application, Infrastructure) on the gate head | all pass | PASS | dotnet test --no-build: Domain 1919, Application 435, Infrastructure 189, 0 failed |
| TG-12 | Full integration suite against `catecommerce_xct`, once | no failure other than the known load flakes; each failure re-run alone | FAIL | 1 full run: 1513 total, 1497 passed, 11 failed, 5 skipped. After re-runs alone: 7 XctSnapshotEndpointTests FAIL deterministically (wall-clock dependent, row 53, HIGH); I_E11 fails alone 3 of 3 (p95 546-585 ms vs 500, Shop test, known flake, row 54); DeployConventionTests failure was caused by the gate's own new script (`set -uo`), fixed, passes 5/5; Waitlist IG3 + Offer_Dispatches pass alone 8/8 |
| TG-13 | The Xct* integration classes alone, then `BackupDrill` fact excluded by default | all pass; fact skipped without `RUN_BACKUP_DRILL=1` | PASS | All other Xct* classes passed in the full run (DatabaseRole 7, ErasureContract 2, ErasureEndpoint 9, Guard 6, Operations 6, RetentionWorker 11, WebVitals 8); BackupDrill fact skipped by default (observed Skipped), then run with RUN_BACKUP_DRILL=1 BACKUP_DRILL_RESULTS=<drill B>: Passed 1/1 (drill JSON accepted by the real endpoint as Passed) |

### TEST gate summary (2026-10-12, TEST agent, head under test a9282e6d plus the gate's own files)

STATUS of the gate: FAIL (one HIGH, test-only: backlog row 53). Details in `docs/gates/HO-XCT-001-TEST.md`.

Re-observed by the gate, not taken from the build agent: unit suites 1919 / 435 / 189 pass; full integration run 1513 total, 1497 passed, 11 failed, 5 skipped; two further full drill runs (I-G1 to I-G6 and I-G8 PASS in both); the BackupDrill fact against drill B's results PASS.
Rows in sections A to F whose home is `XctSnapshotEndpointTests` (U-D3, I-D1, I-D2, I-D3, I-D4, I-D5, I-D8, and the no-branch-header refusal) are **FAIL on re-run**, not PASS: the tests fix the fake clock at 2026-10-12 04:00Z and now answer 401 at the first admin POST (row 53). Their PASS marks above were true when written and are not true now. Every other row in A to F has its test class green in the gate's full run. I-D6 stays DEFERRED.
Counts for the TG rows: 13 total, 11 PASS, 2 FAIL (TG-9: runbook has no positive restore check, row 52; TG-12: full suite red, row 53), 0 BLOCKED, 0 NOT RUN. For section G, I-G4 (endpoint acceptance) and I-G7 were re-observed (BackupDrill fact PASS; DeployConventionTests 5 of 5).

### Gate fix 2026-10-13 (TEST gate rows 52, 53, 55) -- results
- `XctSnapshotEndpointTests`: baseline now derived from the real clock (next local day 10:00 Asia/Dhaka); 7 of 7 pass alone, twice in a row (previously all 7 failed with 401 once the pinned date had passed).
- `restore-pitr-guard-test.sh`: 13 assertions pass (TG-7 added: a live `postmaster.pid` refuses, a stale one warns and pgBackRest decides). `DeployConventionTests`: 5 of 5.
- Backup drill re-run on this head (script changes: `stop -t 60`, clean-stop assertion, abort on non-zero restore and on a missing "finished", `wait_ready` waits for the end of recovery): I-G1..I-G6 and I-G8 pass; unclean-kill data loss 28 s; restore 2 s (PITR) and 4 s (drill).
