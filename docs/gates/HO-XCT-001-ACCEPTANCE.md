GATE: ACCEPTANCE
HANDOVER: HO-XCT-001
COMMITS: a8761829..c70180d6 (re-check range b9699889..c70180d6; first run a8761829..ec583b38, branch ho/XCT-001-build, 203 files). Build c68d4bbe 61805d27 c9050c84 b5ade66e e8f685e4 d38ba82e 202241b4; fix rounds cc6510a1 95fc72b1 a3500f27 a9282e6d 3468c46a; gate records 7f916e7b b8d75f5d 32eedf42 a65f9ce2 ba7d894e 4b55a457 ec583b38
DATE: 2026-10-13
STATUS: PASS on RE-CHECK of c70180d6 (see the section at the end). The first run at b9699889 was BLOCKED (section 5.3 Amended rows missing; records only)

WHY BLOCKED (one item; no code change is needed to clear it)
- **§20.2 / §19: the §5.3 amendment rows are not in the target handovers, and the build edited those areas' files anyway.**
  - §5.3 lists amendments this slice needs in other handovers. §1 says "the README owner records each one as an Amended row in the target's
    §1 **before** implementation starts (OQ-1)". §20.2 says that if the rows are absent when the step begins, the builder stops at that step
    and does not edit the other area's file on the strength of this document alone. §19 makes "§5.3 amendment rows confirmed present in the
    target handovers" an explicit ACCEPTANCE check.
  - What I found (grep of HO-ACC-001, HO-NTF-001 and HO-ADM-001 on this branch and on master b12fd39d):
    - **HO-ACC-001 §11.9 step 7** (exclude `ops.web_vital_batch` and `ops.retention_run` from change capture): no row. The edit was made anyway,
      in `src/Infrastructure/CatEcommerce.Infrastructure/Auditing/AuditCaptureExclusions.cs:25-28`.
    - **HO-ACC-001 §12.4/§12.9** (`ProtectedRecordDeletionException` → `409 RecordIsRetained`): no row. The edit was made anyway, in
      `src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs:253-254, :440-469`.
    - **HO-NTF-001 §8.6** (`Redact(Retention)`): no row. The edits were made anyway, in `NotificationRedactionReason.cs:20-24` and
      `NotificationAggregate.cs:290-305`.
    - **HO-ADM-001**: covered in substance. RedactIpAddress was delivered by ADM and is recorded in this handover's 2026-09-26 Amended row.
      ADM-001's own Amended row (4) anticipates the nine settings as "HO-XCT-001's own addition to this registry when it lands (§5.3)". The
      settings live in a new `SettingDefinitions` partial, not in an ADM file.
  - The edits themselves are correct. Each is exactly the §5.3 text, each is inside §13, each carries a code comment citing §5.3, and
    ARCHITECTURE reviewed them as append-only. **The defect is the missing record, and the fact that the stop was passed without a report.**
    No gate, TC row or backlog row declares it.
  - Why this is BLOCKED and not FAIL: under OQ-1 the rows are owed by the README owner or integrator, not by the builder. Whether to ratify
    edits made ahead of them is also the integrator's or owner's decision. The handover makes a criterion depend on an action nobody took.
    Nothing the builder can change in code clears it.
  - **To clear it:** the integrator appends one dated Amended row to each of HO-ACC-001 §1 (both items) and HO-NTF-001 §1 (the `Retention`
    reason). Each row should ratify the edit as already made on ho/XCT-001-build, following the "amendment already in the target's own
    Amended row" pattern from this handover's 2026-09-26 row. Do the HO-XCT-002 row below in the same pass. Then a re-check of this gate is a
    formality: every §18 criterion below is evidenced, and nothing else stands in the way.

PRIOR GATES (verified from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: PASS. The first pass at 202241b4 was FAIL (HIGH 1, the undeclared Forbidden→AggregateOnly override). The combined re-check
  a65f9ce2 of cc6510a1 PASSED and also covered the SECURITY and CODE_REVIEW fixes.
- SECURITY: PASS (b8d75f5d). Both mediums were fixed on the owner's order "Fix both before merge", and the fixes were verified in a65f9ce2.
- CODE_REVIEW: PASS (7f916e7b).
- TEST: PASS on re-check of 3468c46a (recorded in ec583b38). The first pass at 4b55a457 was FAIL on HIGH row 53, which is now closed.
  `docs/test-cases/TC-HO-XCT-001.md` exists with sections A-G, TG-1..TG-13 and a gate summary. There are no NOT RUN rows. I-D6 is DEFERRED
  on the owner decision of 2026-10-10.
- Later than the last architecture re-check: 95fc72b1 (two src files). The C1 retry now finishes inline, and the audit codes are canonical.
  These are follow-ups to re-check findings 42 and 46. The TEST gate ran over them (head a9282e6d), and the owner's gate-scope rule treats
  them as small fixes. Accepted.

INDEPENDENT RUNS BY THIS GATE (2026-10-13, head ec583b38, build 0 errors; catecommerce_xct only; catecommerce_dev not touched; no full suite, no drill)
- Infrastructure.Tests (Retention, ClassificationEnforcement, AuditCaptureExclusions, RecordClassification): 19/19 pass.
- Domain.Tests (Operations, ErasureRequest, RetentionRun, RestoreTestRecord, SettingDefinition, BillingConvention): 58/58 pass.
  This includes `OperationsConventionTests.U_X1`/`U_X2`.
- Application.Tests (Operations, Retention, OfflineSnapshot, WebVital, Erasure): 56/56 pass. CatClinicalAnchor and SettingDefinitions,
  filtered on their own: 5/5 and 5/5.
- IntegrationTests `FullyQualifiedName~.Xct`: 56 passed, 1 skipped (the `BackupDrill` fact, skipped by design without `RUN_BACKUP_DRILL`),
  0 failed, 1 m 1 s (TRX read).
- IntegrationTests `ErasureCoverage|EndpointAccessConvention|CatClinicalAnchor`: 6/6 pass.

CRITERIA (§18). I re-ran each test named below as part of the runs above, and read the assertions where marked.
- A1 MET. U-A2 `HardDeleteSourceScanTests`; U-A3 `HardDeleteGuardInterceptorTests`; U-A7 `RetentionGuardMigrationTests` (all re-run).
  I-A3 `XctGuardEndpointTests.I_A3_EveryForbiddenTable_...` (read): it iterates **every** `Forbidden` entry of `RecordClassification`, with no
  filter, and requires an enabled BEFORE ROW DELETE trigger and a BEFORE STATEMENT TRUNCATE trigger. I-A4 `I_A4_ARawDelete_AndATruncate_...`;
  I-A5 `I_A5_..._Answers409RecordIsRetained_AndTheRowStays`.
- A2 MET. U-A1 `RecordClassificationConventionTests` (set equality); U-A9 `ClassificationEnforcementAgreementTests`; U-B6
  `ErasureCoverageConventionTests.U_B6_...`. Boarding and Subscriptions have no aggregates, owner-skipped.
- A3 MET. I-A7 (both facts) and I-A8 `I_A8_TheAppRole_StartsTheGuard_AndCannotSwitchATriggerOff_OrTruncate_OrDeleteARetainedRow`, plus the
  NOINHERIT-member case. All are in `XctDatabaseRoleAndSchemaTests` and were re-run.
- A4 MET. U-A4 (interceptor); I-A6 `I_A6_UpdateMedicalProfileRemovingAnAllergy_StillWorks_...`.
- A5 MET. U-B1, U-B3 (domain); U-B9 (application); I-B1 (read): the ledger scopes equal every registered eraser's key, and the source is
  `SelfService`. I-B2/I-B3 (read): account and notification are `Completed`, administration is `Held`, then the request is `Completed` with one
  `ErasureRequestCompleted` audit row. I-B5 (read): the body is `{channel:"Phone", identityCheck:"RegisteredMobileCallback"}`, the account is
  anonymised, there is one `CustomerErasureRequestedByStaff` row, and the source is `StaffOnBehalf`. "Across contexts" means the registered
  erasers. Boarding and Subscriptions are deferred (backlog rows 1-3).
- A6 MET. `I_B2_B3_B4_...` (read): the hold basis is "NFR-LEG-5 acceptance evidence"; `heldUntil` equals the local Dhaka acceptance date plus
  6 years; the IP is kept while held and is null after the clock passes the hold; the cat row survives; the owner shows the placeholder. U-B8
  is covered by the same test.
- A7 MET. U-A8 (re-run); I-C1, I-C9 (`XctRetentionWorkerTests`; I-C9 has a P0001 negative control); I-C3 (`XctWebVitalsEndpointTests`); I-C4.
- A8 MET for the server half only. I-C4 (a registered fake source is called with its cutoff, and its result is on the pass). **DEFERRED:** the
  `boarding-media` source itself (C-BRD-2, HO-BRD-001, owner decision 2026-10-10, backlog row 2). This split is recorded as §19 requires.
- A9 MET. U-C6 `CatClinicalAnchorTests` (death, later visit, visit only, created only, local midnight); I-C7
  `I_C7_U_C6_...SaysNothingIsDeletedAutomatically`.
- A10 MET for appointments; boarding is DEFERRED. U-D3 (application and integration); I-D1 (read): 2 of 4 items, sorted, with Held and
  Cancelled excluded. The U-D6 status filter, walk-ins included and `handlingNote` only when high-risk are **evidenced by source read**:
  `ClinicDayListSnapshotSource.cs:34-35, 47-49, 84`. No test asserts the walk-in or high-risk branch (proposed row 59). I-D6 is DEFERRED,
  because the handover's own §18 note says A10 is met for appointments before BRD lands. I-D7: `boardingTasks` is omitted as
  `NotAvailable`, asserted in I-D1.
- A11 MET. U-D1, U-D2 (`OfflineSnapshotEtagTests`); I-D2/I-D3 `I_D2_D3_AnUnchangedDay_Is304WithNoAudit_AndAStatusChangeGivesANewETag`.
- A12 MET. I-D4 `I_D4_PollingTheSnapshotAndThePing_NeverKeepsAnIdleStaffSessionAlive`; I-D8 `I_D8_..._DoesNotMoveLastActivity`.
- A13 MET. I-D1 (read): `no-store` and `private` set, exactly one `OfflineSnapshotIssued` audit row, every item field in the §12.4 allow-list,
  and the top-level keys exactly the §12.4 list. I-D2: `304` with no new audit row. Minor gap: the test does not assert
  `staffAccountId == caller`. The handler passes `query.StaffAccountId` (`GetFrontDeskOfflineSnapshotQueryHandler.cs:65`) (row 59).
- A14 MET, server half. U-E3 (application); I-E1; I-E5 (p75 against an independent `percentile_cont`, with verdict). The lab budget and the
  browser half are HO-XCT-002's.
- A15 MET. U-E2; I-E2 (`text/plain`, a customer cookie, no CSRF, 204, no identity); I-E6 (log capture); I-E7 (no DataChange row).
- A16 MET, with a go-live condition. I-G1, I-G2, I-G3, I-G5 and I-G6 come from TC section G. The TEST gate observed two full runs, A and B, on
  ba7d894e: I-G2 found 2 000 of 3 000 rows both times, and I-G3 measured 27 s of data loss against a 15-min RPO. The builder ran the earlier
  runs. See the S3 and drill-currency rulings below.
- A17 MET. U-F1, U-F3 (domain, re-run). I-G4: the TEST gate ran the `BackupDrill` fact with drill B's results, and the real endpoint accepted
  the JSON as `Passed`. The 2-4 s restore time is not a production RTO; backlog row 6 covers that, and the quarterly test is the real measure.
- A18 MET. U-F1..U-F6 (domain); U-F7 `U_F7_TheDueDate_IsTheLastPassedTestPlusTheInterval` and `NoPassedTestEver_IsDueToday`. The rule that
  "a failure does not reset" is shown by source read: `OperationsStatusQueryHandlers.cs:44-48` uses the last `Passed` only. I-F1
  (`XctOperationsEndpointTests`). I-F2 is adapted (backlog row 8, accepted by CODE_REVIEW): it is branch-dependent on the data present, and
  the never-tested rule is unit-proved.
- A19 MET. I-F3 `I_F3_TheBackupReporter_CanInsertABackupRun_AndNothingElse` (scratch database, `roles.sql`); I-F4
  `I_F4_ADiffTwentySevenHoursOld_IsStale_...`.
- A20 MET, with the S3 condition for the off-site journal copy. U-B10; I-B9 `I_B9_ReapplyAfterARestore_...` and
  `Reapply_With501Ids_Is400ReapplyListTooLarge`. I-G8 comes from TEST drill runs A and B: the posix copy plus the encrypted `.csv.enc` copy,
  ids and dates only. `TheJournalExport_SelectsIdsAndDatesOnly` was re-run. The 10-minute export and the restore-mode runbook were read and
  are recorded in ADR-314's amendment.
- A21 MET. I-A1 `I_A1_TheHostStartsWithScopeValidationOn_...`; I-G7 `I_G7_NoFileUnderDeploy_HoldsALiteralSecret_...` (re-run).

RULINGS REQUESTED BY THE CALLER
- **S3 transport (never exercised; no MinIO image could be pulled):** acceptable for this gate, **with a go-live condition**. It is not unmet.
  - The handover makes the S3 endpoint optional in the drill (§12.8: "an *optional* MinIO-compatible S3 endpoint"). §16.2's I-G5 and I-G6 ask
    for "restore from repo2" and "repo2 without the passphrase", not for S3 specifically. §20.9 is about `postgres:17-bookworm` and PGDG
    pgBackRest, and both pulled.
  - The encryption, the repo2-only restore and the refusal without the passphrase were observed on a posix repo2 with the production cipher
    settings. `pgbackrest.conf.template` declares `repo2-type=s3` with every credential as `${VAR}`.
  - What is unproven is configuration against a real provider: endpoint, bucket, region, TLS, credentials, URI style, and the
    `curl --aws-sigv4` journal upload. MinIO would not have proved the real provider either.
  - **Condition:** before production go-live, run `restore-drill.sh` against the real repo2 bucket and record the result through
    `POST /api/admin/operations/restore-tests`. The run must cover stanza-create and check on repo2, a backup to repo2, a restore from repo2
    alone, and `export-erasure-journal.sh` uploading to and reading back from the bucket. Proposed row 57 turns backlog row 5 into this
    go-live gate.
- **Drill-script edits after the last executed drill:** sufficiently evidenced.
  - The last drills the gate observed were TEST runs A and B on ba7d894e. Since then (`git diff ba7d894e..ec583b38 -- deploy`), only three
    files changed:
    - `run-drill-test.sh`: harness only. It adds stricter assertions (`stop -t 60`, a pid-gone check, abort on a non-zero or unfinished
      restore, wait for the end of recovery).
    - `restore-pitr-guard-test.sh`: a test.
    - `restore-pitr.sh`: one new block, which runs **before** pgBackRest is invoked: a live `postmaster.pid` refuses with exit 4, and a stale
      one warns. The stub guard test covers that block, and the TEST re-check observed 13 assertions pass. The pgBackRest invocation and the
      rest of the restore path are byte-identical to the observed runs.
  - The builder's single re-run on 3468c46a is the builder's claim, so I gave it no weight. I did not need it: the measured RPO and RTO come
    from gate-observed runs of the same production restore path.
  - One residual from reading the code (LOW, proposed row 58): the new liveness check is not PID-namespace-aware. It fails safe (it refuses),
    so it does not affect A16.
  - The go-live S3 drill (row 57) will run the final scripts end to end anyway.
- **Deferred D2 items:** correctly deferred under §20.3 on the owner decision of 2026-10-10 (§1 and backlog rows 1-3).
  - No boarding table, mock or guessed schema is in `src/`. The only references are the `"boardingTasks"` section key, the `boarding-media`
    rule key and the `retention.boardingMediaMonths` setting, all specified by §10.2 and §12.4.
  - The snapshot reports `boardingTasks` as `NotAvailable`, asserted in I-D1.
  - A8 and A10 are recorded above as met for the server half and for appointments, and deferred for the rest. I-D6 must be un-deferred
    when HO-BRD-001 lands (row 1). It is not a pass.
- **Frontend contract change (erasure body is now `{channel, identityCheck}`, four codes, no `reason`):** **NOT flagged in HO-XCT-002.**
  - HO-XCT-002's §1 Amended cell still says "(none. Append rows here)". Its §11.5 `ErasePersonalDataPanel` row (line 399) still requires "how
    identity was checked (**free text**, required), reason". Its U-B5 (line 552) still "requires channel, identity check and reason".
  - The change is recorded only in HO-XCT-001 §1 Amended item (4). HO-XCT-002 §1 says HO-XCT-001 wins on the wire (§20.4), so a careful
    builder would find it. But a builder who follows HO-XCT-002's own §11.5 would build a free-text field that the server rejects with 400.
    That free-text field is exactly the leak SECURITY fixed.
  - Not blocking, because no `web/` consumer exists yet. Proposed row 56, to be written in the same integrator pass that clears the BLOCKED
    item.

OTHER §19 ACCEPTANCE CHECKS
- OQ-3 defaults are flagged as **provisional pending SRS Q-1**: 7 years clinical, 6 years financial, 12 months boarding media, 24 months
  notification content.
  - They are settings, and ADR-301 guarantees that a wrong period can delay a review or an anonymisation but can never destroy a record.
  - `docs/runbooks/retention-and-erasure.md:11` states "Kept 7 years" as fact, with no provisional note (row 60).
  - OQ-12 (the privacy policy must mention telemetry and the 35-day backup window) is open for the owner. It is listed at
    `retention-and-erasure.md:64-65`.
- OQ-5 (personal data in immutable audit entries) and OQ-10 (phone number in the snapshot) are accepted residuals at the stated defaults. The
  owner has not confirmed them.

PROPOSED BACKLOG ROWS (docs/backlog/XCT.md, continuing from row 55; this gate did not append them)
- 56 MEDIUM: HO-XCT-002 needs a §1 Amended row (integrator) stating that `POST /api/admin/customers/{id}/erasure` takes
  `{ channel, identityCheck }`. `identityCheck` is one of `RegisteredMobileCallback`, `RegisteredEmailReply`, `InPersonPhotoId` or
  `WrittenRequestSigned`; there is no `reason` field, and no free text may be offered. Its §11.5 `ErasePersonalDataPanel` and U-B5 must
  follow: a select of the four codes, no reason box. Optionally note that the erasure journal now exports every 10 min (Q7's
  `lastErasureJournal` will look "fresh" far more often). Source: HO-XCT-001 §1 Amended (4), SECURITY row 25.
- 57 MEDIUM (go-live gate; strengthens row 5): before production go-live, run `restore-drill.sh` against the real repo2 S3 bucket. It must
  cover a render with the real secrets through `render-config.sh`, stanza-create and check on repo2, a backup to repo2, a restore from repo2
  alone, and the `export-erasure-journal.sh` `curl --aws-sigv4` upload and read-back. Record the result through
  `POST /api/admin/operations/restore-tests`. Until then, the off-site leg of A16 and A20 is proven only on a posix repo2.
- 58 LOW: `deploy/backup/scripts/restore-pitr.sh:37-44`: the "live postmaster.pid" check is not PID-namespace-aware.
  - In the shipped image, PostgreSQL runs as PID 1, so the pid file names 1.
  - A restore in a new container (`in-container.sh` ends in `exec "$@"`) makes the script itself PID 1. So `kill -0 1` succeeds and the
    script refuses with exit 4: "names process 1, which is running".
  - This fails safe (it refuses, never corrupts), but the stale-pid warning branch is unreachable in containers and the message misleads.
  - Fix: compare `/proc/$pid/comm` (or `cmdline`) with `postgres`, or have runbook §4 say that exit 4 in a fresh container means the pid
    file is stale.
  - Found by reading the code; not executed.
- 59 LOW: snapshot test gaps. No test asserts that walk-ins in the queue are included (U-D6) or that `handlingNote` is non-null for a
  high-risk cat. I-D1 does not assert `staffAccountId == caller`. The source implements all three (`ClinicDayListSnapshotSource.cs:47-49,
  :84`; `GetFrontDeskOfflineSnapshotQueryHandler.cs:65`). Add the assertions to I-D1 or a sibling.
- 60 LOW / OWNER: mark the retention periods in `docs/runbooks/retention-and-erasure.md` as provisional defaults pending SRS Q-1 (OQ-3). Owner
  to confirm OQ-12 (privacy-policy wording for telemetry and the 35-day backup window).

FINDINGS
- [BLOCKER for this gate, record-only] docs/handover/HO-ACC-001.md §1, docs/handover/HO-NTF-001.md §1: the §5.3 Amended rows are missing,
  and the build edited `AuditCaptureExclusions.cs`, `UniqueConstraintExceptionFilter.cs`, `NotificationRedactionReason.cs` and
  `NotificationAggregate.cs` without them (§20.2, §19), undeclared. See WHY BLOCKED.
- [MEDIUM] docs/handover/HO-XCT-002.md:399, :552: the erasure contract change is not flagged in the frontend handover (row 56).
- [MEDIUM] Off-site S3 transport unexercised: go-live condition (row 57).
- [LOW] deploy/backup/scripts/restore-pitr.sh:37-44: the liveness check is not PID-namespace-aware (row 58).
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/XctSnapshotEndpointTests.cs:118-163: the U-D6 walk-in and high-risk branches and
  `staffAccountId` are unasserted (row 59).
- [LOW] docs/runbooks/retention-and-erasure.md:11: the OQ-3 periods are stated as fact (row 60).

SCOPE
- out-of-scope work found (§4): none.
  - No `web/**` change.
  - No boarding table or mock.
  - No `DeleteAsync` on the repository contract.
  - `ExecuteDeleteAsync` appears only in `RetentionPurger` (5 named Purgeable tables).
  - No `DISABLE TRIGGER` or `session_replication_role` in the diff.
  - No superseded dispatch types, `202` or `[Authorize]`.
  - No identity in telemetry.
  - No secret under `deploy/` (I-G7 re-run).
- files in allowed scope: 203/203.
  - 190 at 202241b4 and 45 in cc6510a1 were checked by ARCHITECTURE (non-literal files recorded in the 2026-10-12 Amended row items 2 and 4).
  - The 18 files changed after a65f9ce2 were checked by this gate. All fall in §13 paths (`deploy/backup/**`, `docs/runbooks/**`, the TC
    sheet, `docs/gates/HO-XCT-001-*`, the Operations handlers, `tests/**`, the `adr.md` append) or are recorded (`docs/backlog/XCT.md`).
  - `docs/handover/HO-XCT-001.md`: verified append-only. The old §1 Amended cell is an exact prefix of the new one (1 732 → 4 711
    characters), and no other line changed.
  - `.ai/decisions/adr.md`: +100 / −0.
- files outside allowed scope: none.

UNDECLARED DEVIATIONS
- The §20.2 stop was passed without a report: ACC and NTF files were edited with no §5.3 Amended row in either target handover (BLOCKED
  above). The edits match §5.3 exactly; only the record and the report are missing.
- None other found. The deviations ARCHITECTURE found earlier (the six-table override, the interceptor order, `DedicatedlyGuardedTables`, the
  C1 fresh scope, the `files.stored_file` allowance) are now declared in §1 Amended and the ADR-297..302/314 amendments.

NOTE FOR THE INTEGRATOR
- master has moved to b12fd39d (SRG merged) since this branch's base a8761829. Under ADR-341 rule 5, the integrator regenerates the five XCT
  migrations against master, and `has-pending-model-changes` must be clean afterwards (ARCHITECTURE "FOR TEST").


---

## RE-CHECK (2026-10-13): range b9699889..c70180d6, records only

GATE: ACCEPTANCE
HANDOVER: HO-XCT-001
COMMITS: c70180d6 (range b9699889..c70180d6, 4 files, +8/-3)
DATE: 2026-10-13
STATUS: PASS

No suites were run; the range changes records only, and no source, test, deploy or runbook file changed.

THE BLOCKED ITEM: CLEARED
- All three handover edits are append-only. I checked each by comparing the old line with the new one: only the §1 Amended cell changed in
  each file, the old cell is an exact prefix of the new one, and no other line moved.
- `docs/handover/HO-ACC-001.md:16` gains a dated integrator row, 2026-10-13, which ratifies the HO-XCT-001 §5.3 edits as made.
  - Item (63) covers §11.9 step 7: `RetentionRunAggregate` and `WebVitalBatchAggregate` appended to `AuditCaptureExclusions`.
  - Item (64) covers §12.4/§12.9: the `TryMapRecordIsRetained` mapping in `UniqueConstraintExceptionFilter`, which answers
    `409 RecordIsRetained` for both `ProtectedRecordDeletionException` and the `P0001` "Hard delete of" message.
  - Item numbers (63) and (64) continue the cell's sequence from (62) with no collision.
- `docs/handover/HO-NTF-001.md:17` gains item (68), §8.6, for `NotificationRedactionReason.Retention` (value 3) and the `Redact` behaviour. Its
  text matches the code I read in the first run (`NotificationAggregate.cs:290-305`): it nulls the destination and the unsubscribe link, is a
  no-op once `AnonymisedOn` is set, and is idempotent. The item number continues from (67).
- Both rows name the files, cite §5.3, and state plainly that the §20.2 stop was passed without a report. The integrator ratifies the edits;
  nothing is rebuilt. That is the remedy this gate asked for.
- HO-ADM-001 needs no row, and I agree. RedactIpAddress is recorded in HO-XCT-001's 2026-09-26 Amended row, and the nine settings live in
  XCT's own `SettingDefinitions` partial, which ADM-001 Amended (4) anticipates.

OTHER RECORDS IN THE RANGE
- `docs/handover/HO-XCT-002.md:18` gains a 2026-10-13 integrator row with the erasure contract.
  - The body is `{ channel, identityCheck }`, with the four fixed codes, `reason` removed, and free text answered with 400.
  - It supersedes §11.5 `ErasePersonalDataPanel` and U-B5 on those points: a select of the four codes, no reason box.
  - It notes the 10-minute journal export.
  - It is accurate against `ErasureIdentityCheck.cs` and the I-B5 and 400 tests read in the first run. Proposed row 56 is satisfied.
- `docs/backlog/XCT.md` gains rows 56-60, which match this gate's proposals word for word in substance.
  - Row 56 is marked Resolved, and correctly so.
  - Row 57 (the go-live S3 drill) stays MEDIUM and open as a go-live condition.
  - Rows 58-60 stay LOW and open.

PRIOR GATES: unchanged. ARCHITECTURE PASS (a65f9ce2), SECURITY PASS, CODE_REVIEW PASS, TEST PASS (re-check recorded in ec583b38).

CRITERIA: unchanged from the first run. A1-A21 are evidenced. A8 and A10 are met for the server half and for appointments; their boarding
halves are DEFERRED (owner 2026-10-10, backlog rows 1-3), and I-D6 must be un-deferred when HO-BRD-001 lands. A16 and A20 carry the go-live S3
condition (row 57).

FINDINGS
- None open at BLOCKER or HIGH. The open MEDIUM and LOW items are backlog rows 57-60 (and the earlier open rows).

SCOPE
- files in allowed scope: 4/4. Three are integrator Amended rows (integrator-authorised record practice, the remedy this gate prescribed), and
  one is `docs/backlog/XCT.md`, which is recorded in HO-XCT-001 §1 Amended (2).
- files outside allowed scope: none.

UNDECLARED DEVIATIONS
- None open. The §20.2 pass-without-report found in the first run is now declared and ratified in HO-ACC-001 (63, 64) and HO-NTF-001 (68).

CARRIED CONDITIONS FOR MERGE AND GO-LIVE (not gate blockers)
- Before production go-live: run the real-bucket drill (row 57) and record it.
- At merge: master has moved to b12fd39d, so regenerate the five XCT migrations against master and confirm `has-pending-model-changes` is clean.
- Owner, still open: OQ-3 (retention periods, SRS Q-1), OQ-12 (privacy-policy wording), and OQ-5 and OQ-10 (accepted residuals at their
  defaults).
