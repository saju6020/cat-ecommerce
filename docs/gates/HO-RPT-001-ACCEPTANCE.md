GATE: ACCEPTANCE
HANDOVER: HO-RPT-001
COMMITS: b12fd39d..5f6bccb0 (branch ho/RPT-001). Build 01c3f77b e9d56daa 033791ae 082b5684 2709ea13 16fc8a7a 2d1d5ba9 fdd776bd 28c59e18 c6826939 283b3968 3fcff556 425f0939; fix rounds 126b21f3 55204632; TEST e4f2d088 4cedeef4; TEST follow-up 5f6bccb0 (backlog 58, 59)
DATE: 2026-10-13
STATUS: PASS

No BLOCKER or HIGH findings. Under the owner's rule of 2026-09-27, only those fail a gate. One MEDIUM and five LOW findings are proposed
below as docs/backlog/RPT.md rows 60-65, prefixed "ACCEPTANCE:". Several criteria are met only in part, or are deferred. Each deferral
is listed under CRITERIA with its authority. None is counted as a pass.

PRIOR GATES (read from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: PASS. The first pass (94ac1894) and the combined re-check (81880105) both FAILED. Re-check 2 (ec3b4662) PASSED with no
  findings. Under the gate-scope rule, that combined re-check also stands as the follow-up review for SECURITY and CODE_REVIEW.
- SECURITY: PASS, through the combined re-check. Its own file still ends at the first-pass FAIL (1f2eaf4e, HIGH R3 clinical access).
  That HIGH is closed in ARCHITECTURE re-check 1 section 1 ("SECURITY HIGH ... CLOSED") and stays closed in re-check 2. The SECURITY
  MEDIUM (recipient branch scope) is backlog 29/40, RESOLVED. The SECURITY LOWs are backlog 41-43.
- CODE_REVIEW: PASS (8e52b7c1). Its mediums (backlog 28-31) were re-checked RESOLVED in the combined re-check.
- TEST: PASS (4cedeef4). docs/test-cases/TC-HO-RPT-001.md exists. It has:
  - 95 PASS rows and 11 DEFERRED rows, each with a reason;
  - T-U1..T-U6 and T-I1..T-I11, all PASS except T-I3 "throws", which was FAIL and is backlog 58;
  - mutation spot-checks M-1..M-14, all killed.
  The TEST gate checked every earlier PASS row against its full-run trx (0 missing). No row is NOT RUN. No BLOCKED row is counted as a pass.
- TEST follow-up 5f6bccb0 (rows 58 and 59). This is a tiny fix, so under the gate-scope rule a test confirmation is enough. This gate ran it (below).

INDEPENDENT RUN BY THIS GATE (head 5f6bccb0; CATECOMMERCE_TEST_DB = full Npgsql string for catecommerce_rpt; catecommerce_dev/_test untouched; no full suite)
- IntegrationTests `FullyQualifiedName~Reporting|FullyQualifiedName~ReportLimiter|FullyQualifiedName~AttachmentLogging`: 114/114 passed, 1 m 52 s.
  This matches the builder's 114. The trx is outside the repo, under the prefix rpt-acc-.
  - `ReportingTestGateEndpointTests.T_I3_WhenTheRegisterStepFails_TheStoredObjectIsRemoved_AndTheRunIsFailed(shape: "throws")`: Passed. The test asserts
    `blobsBefore == BlobsInTheFileStore()`, no new ReportExport register row, a Failed run with `UnexpectedFailureCode`, no requests and no email.
    The `returnsAFailure` shape also passed.
  - `ReportLimiterTests`: all three passed. They are `TheFifthRunNowOfTheWindow_IsTheLast_EvenAfterTheAccountWasIdleLongerThanTenSeconds`, `ARefusedAttempt_DoesNotUseUpAPermitOfTheWindow`
    (1 run, then a refused double-click, then 4 more allowed, then the 6th refused) and `AChainWithALimiterInUse_IsNotIdle_...`.
  - The trx also shows Passed for every criterion test cited below: I-A1..A11, I-B1..B4, I-C1..C4, I-D1..D6, I-E1..E4, I-F1/F2/F3/F7, I-G1..G6,
    I-G8..G11, I-H4, U-G7/G8/G9, U-A2/A3, U-X5, U-D4, T-I9 and the three R3_* tests.
- Domain.Tests `~Report|~Dashboard`: 104/104 passed. Application.Tests `~ReportBuilder|~OrderFulfilment|~ReportMaths`: 58/58 passed.
  5f6bccb0 changes no Domain or Application file.
- Source read for 5f6bccb0:
  - ScheduledReportRunner.StoreFileAsync wraps `RegisterStoredFileCommand` in try/catch. On a throw it runs `fileStore.DeleteAsync(put.Value)` and rethrows.
  - The run-now policy in ReportingModule.cs (about :203-208) passes the fixed window as `ownedLast`, so the order is one-in-flight, then host-wide, then window.
  - `ChainedLimiter` builds `_all = [ownedFirst, shared, ownedLast]`, and `IdleDuration` still covers every owned limiter.

CRITERIA (handover section 18)
- A1 MET. U-B1 (`ClinicReportBuilderTests.U_B1_HoldsNeverAppear_...`). I-B1 (`I_B1_TheDailySchedule_..._NeverAHold_NeverAnotherBranch`) passed in this run.
- A2 MET. I-B4 (`ReportingSurgeryRegisterEndpointTests.I_B4_...WithSurgeonOutcomeAndDrugEntryCount`) passed. R3 also writes the NFR-SEC-8 access row
  (owner decision "Log access"; see below).
- A3 MET. U-B3, U-B4 and U-B5, and I-B3 (`I_B3_TheDueList_HasTheDueAndOverdueCats_WithContact_AndNotTheRenewedOrTheDeceased`). Caveat: the
  anonymised owner is proven at unit level only (backlog 22, no anonymisation endpoint on this branch).
- A4 MET. U-B2; I-B2 (`I_B2_TheQueueReport_HasTheSeededWaits_AndWaitingNowIsTodayOnly`).
- A5 MET.
  - U-A1 covers every definition: it accepts from/to/branchId.
  - U-A4, U-A5 and U-A7.
  - I-A10 (`I_A10_ARangeOf367Days_ABranchNotAssigned_AllAsReception_AllAsAccountant`).
- A6 MET. U-A9 (the guard); I-A7 (`I_A7_TheCsvExport_OfEveryReport_...`, a full matrix over every key); I-A9 (413 over the cap).
- A7 MET. U-A10, U-A11; I-A8 (`I_A8_ThePdfExport_OfEveryReport_...`); I-A9.
- A8 MET. U-C1, U-C2; I-C1 (`I_C1_TheRollForward_EqualsTheInventoryProjection_...`). Caveat: visit-treatment consumables are not in the ledger
  (backlog 7). See finding 63: EMR Phase F has since merged on master.
- A9 MET. U-C3; I-C2.
- A10 MET. U-C4; I-C3 (`I_C3_TheLowStockRow_...` and the parity pin `I_C3_R7ListsTheSamePairsAndSuggestionsAsHO_IMP_001sOwnList_...`).
- A11 MET FOR CLINIC AND SHOP; BOARDING DEFERRED.
  - Evidence: U-D1 and U-D2. I-D1 (`I_D1_RevenueIsSplitByBusinessLine_TaxIsExcluded_...`) asserts Clinic and Shop.
  - The boarding line has no source. BRD was skipped by the owner on 2026-10-10. The tile's boarding figure is absent, not 0 (T-I9; mutation M-9 killed).
  - Visit, dispense and surgery charges wait for HO-BIL-001 Phase J. The R9/R12 notes say so (backlog 6).
- A12 MET. I-D2 (`I_D2_I_D3_ShopRevenueReconcilesAcrossTheReports_...`).
- A13 MET. U-D3; I-D3 (same test); T-U3 (a hand-worked fixture; M-12 killed).
- A14 MET. U-F1; I-F1 (`I_F1_I_F2_I_F3_...`).
- A15 MET FOR CLINIC AND SHOP; THE BOARDING SHARE HAS NO SOURCE. I-F2 (same test). The merge chain is counted once (I_F7, both tests).
- A16 MET. U-F2; I-F3.
- A17 DEFERRED, not met. U-F4 and I-F4 are DEFERRED: R21 needs HO-SUB-001, which the owner skipped on 2026-10-10.
  - Recorded in section 1 Amended (build) and backlog 1.
  - R21 is absent from the catalogue, not empty (T-I9). This is the handover's section 17 "absent" rule, so it is listed here.
- A18 DEFERRED, not met. U-F3 and I-F5 are DEFERRED: R22 needs the boarding line, and its subscription part needs SUB.
  - Recorded in section 1 Amended and backlog 1 and 2.
  - R22 is absent (T-I9).
- A19 MET. U-E1; I-E1 (`I_E1_TheTiles_EqualTheMatchingReportFigures_ForToday`), I-E3, I-E4 (p95 <= 1.5 s). There are 18 tiles, not 21: the three
  `boarding.*` tiles are deferred (backlog 3).
- A20 MET. U-E2 (inside `I_E2_..._AndAGatedTileIsNeverComputed`, TC adaptation 17), U-E3, U-A8; I-A6 (every key x every role, with the denial audit);
  I-D4 (JSON, CSV and PDF); I-E2.
- A21 MET WITH THE PROVIDER CAVEAT RECORDED.
  - I-G2 (`I_G2_ADailyScheduleIsGenerated_HeldByQuietHours_ThenDeliveredWithTheSameBytesAsAnInteractiveExport`) asserts
    `export bytes == attachment.Content` on the capturing sender.
  - U-G6; U-G7 (`U_G7_AnEmailWhoseFileHasGone_IsCancelled_...`).
  - The caveat: there is no real email adapter on this branch or on master (no MailKit or SMTP sender). ADR-320 chose SMTP, but there is no
    account yet, so outside Development no scheduled email reaches an inbox. Recorded in section 4.3, section 1 Amended (ADR-320), backlog 27 and TC adaptation 6.
- A22 MET. U-G1, U-G2; I-G3 (`I_G3_RunningTheCycleTwice_AndRecordingTheSameSlotConcurrently_...`), I-G4, I-G10 (London DST).
- A23 MET.
  - U-G4 and U-G5; I-G5 and I-G6. I-G6 is proved both ways, including with an Accountant recipient of R10.
  - The send-time branch and cross-branch re-checks are T-I7 and `ARecipientTakenOffTheBranchBeforeTheSend_...`; mutations M-6, M-6b and M-14 were killed.
- A24 MET. I-G1 (`I_G1_AnAdministratorCreatesADailySchedule_AShopManagerCannot_...`).
- A25 MET. U-D5 (`..._TheMethodRows_AreOnlyTheFourLaunchMethods_..._ThereIsNoGatewayRow`); I-D5; T-U4.
- A26 MET. U-D6; I-D6 (four tests: cash-up, ageing, refunds and credit notes, tax summary).
- A27 PART MET.
  - Import consignments (I-C4) and order fulfilment (U-H3, I-H4) are MET.
  - Boarding occupancy, care compliance and incidents (U-H1, U-H2, I-H1..H3) are DEFERRED. They need BRD, which the owner skipped on 2026-10-10.
    Recorded in section 1 Amended and backlog 3. R24-R26 are absent (T-I9).
- A28 DEFERRED, not met. I-F6 is DEFERRED. R23 has no data source: the owner deferred cart reminders in HO-ORD-001 (decision 10), and the
  shipped cart has no abandonment or recovery fields. The builder correctly stopped and reported this under section 20.2 rather than
  inventing an `rv_cart`. Recorded in section 1 Amended and backlog 4. R23 is absent (T-I9).
- A29 MET.
  - U-A3 (no write path in sources, tiles or infrastructure).
  - U-X5 (no migration operation outside `report`, except the four `notify.*` columns).
  - I-A3 (`I_A3_AWriteInsideTheReadOnlyScope_FailsWith25006_...`).
- A30 MET. U-A12 and I-A7 (`I_A7_TheAuditRow_HoldsTheFiltersAndTheColumnKeys_NeverARowValue`).
- A31 MET.
  - I-A4 (a 400 on timeout while an appointment endpoint answers) and I-A11 (a 429 with Retry-After).
  - The per-account limits are proven by `OneAccountsSlowReports_...` and ReportLimiterTests, and by T-I6 at the endpoint (a real 11.5 s idle gap).
  - Scheduled runs are sequential (`ReportingWorkCycle`; ARCHITECTURE verified this).
- A32 MET. I-A1 (`I_A1_EveryReportSource_Resolves_AndMatchesTheCatalogueOneToOne`) and U-A2. The host carries 21 sources and 18 tiles, against
  section 16's "27 / 21". The six absent reports and three absent tiles are the deferrals above. T-I9 proves they are absent, not zero.

Section 19 ACCEPTANCE focus:
- The A21 caveat is recorded (above).
- Reports absent because their source is not merged: R21, R22, R23, R24, R25, R26 and the tiles `boarding.*` (three).
- Section 8.11 tile fallbacks: the boarding figure of `revenue.today-by-line` is absent (Amended (21)).

RULINGS ON THE CALLER'S QUESTIONS
1. **The deferrals are authorised and recorded. None was worked around.**
   - BRD/SUB (R21, the SUB part of R22, R24-R26, the boarding tiles):
     - The owner skipped BRD and SUB on 2026-10-10. That decision is recorded on master in docs/backlog/XCT.md:4 and in the 2026-10-13
       scope decision ("build every phase whose sources are merged").
     - Section 17 and section 20.1 of this handover already require an unmerged source to make a report absent, never stubbed.
     - Recorded in section 1 Amended (build) and backlog 1-3.
     - LOW (row 64): the RPT records give the reason as "not merged" and do not cite the 2026-10-10 skip, so a reader may expect the reports soon.
   - R23: owner-authorised, through HO-ORD-001 decision 10. A source column is missing, so section 20.2 applies, and it was reported, not
     invented. Recorded in Amended and backlog 4.
   - XCT purge (C8, the purge pass, I-G7): dependency-blocked. Recorded in Amended and in backlog 5 and 43.
     - The deferral reason is wrong about the future. HO-XCT-001 as built (ho/XCT-001-build 367cd09b) does **not** schedule
       `stored-file:ReportExport`.
     - `RetentionWorkCycle.SourceCutoff` maps only `boarding-media`, and XCT backlog 4 says any ReportExport window "needs an explicit
       decision and a RetentionWorkCycle rule".
     - So after XCT merges, report files that carry R4 contacts and R3 clinical text still outlive their `RetainUntil`.
     - No section 18 row cites I-G7, and SECURITY confirmed the files are unreachable (no signed URL is minted). This is MEDIUM (row 60),
       with an owner question.
   - BIL Phase J (visit, dispense and surgery revenue): dependency-blocked; recorded in Amended and backlog 6; the R9/R12 notes state the gap.
   - EMR Phase F (the ledger): dependency-blocked when built; recorded in Amended and backlog 7.
     - EMR-001-F has since merged to master (8da8e834; `EfStockLedger.Consumer`).
     - R5 already counts `ClinicalUse` as an outgoing movement, so the movements should flow in without code changes. But backlog 7's reason
       becomes stale at the merge. LOW (row 63): re-run the R5 tests after the merge and close or reword row 7.
2. **The TC sheet says "12 deferred" in one place, but there are 11 DEFERRED rows. This needs a wording fix and does not block.**
   - The Summary line (TC-HO-RPT-001.md:16) says "12 cases DEFERRED" but lists 11 IDs: U-F3, U-F4, U-H1, U-H2, I-F4, I-F5, I-F6, I-G7, I-H1, I-H2, I-H3.
   - The table has 106 rows, which is 95 PASS plus 11 DEFERRED. The TEST gate record and its "Deferred rows" section also say 11.
   - It is a typo. No criterion depends on the count. LOW (row 61): correct "12" to "11".
   - Related LOW: T-I3 still reads "FAIL (shape throws)" and R-2 reads "PASS except the throws case", because 5f6bccb0 came after the sheet.
     This gate observed both as Passed. Append a follow-up note; do not rewrite the observed history.
3. **All three owner decisions are implemented and recorded.**
   - R3 "Log access":
     - Implemented in StaffReportsController (view, CSV and PDF) and in ScheduledReportRunner (one row per recipient). Both fail closed.
     - Evidence: `R3_AViewAndBothExports_...`, `R3_IfTheAccessRowCannotBeWritten_...`, `R3_AScheduledRun_...`, T-I4, T-I5 and T-I11 (the refused
       shape). All passed; M-4, M-4b and M-5 were killed.
     - Recorded in section 1 Amended (12) and backlog 16/52.
   - Accountant "Yes, allow Sales":
     - The code: `ActionRoles.Reports.cs:16` is `SalesView = [ShopManager, Accountant, Administrator]`. `Reports.Financial.View` still
       gates cost and margin.
     - The tests: `ReportingFoundationTests` U_A7 `[InlineData(StaffRoles.Accountant, "Sales", true)]` (Domain, passed), and
       `I_G6_AnAccountantCanBeARecipientOfR10_...` (passed).
     - **Recorded in HO-ACC-001 section 1 Amended (65)**, under "2026-10-13 — owner decision (HO-RPT-001 gate fix round)". Its text matches the
       code. Also recorded in RPT Amended (13) and backlog 8.
   - Run-now "Add a per-user limit":
     - The policy: 5 per 10 minutes per account, one in flight, then host-wide. After 5f6bccb0 the window is taken last.
     - The tests: ReportLimiterTests (all 3) and T-I6 at the endpoint. All passed; M-1 was killed.
     - Recorded in Amended (14) and backlog 17/42/53/59.
     - LOW (row 65): ReportLimiterTests builds a copy of the chain (`RunNowLikeChain`), not the module's policy. Row 59's ordering in the
       registered policy is evidenced by reading ReportingModule.cs, not by a test. An endpoint case would be: in-flight, then refused, then 4
       more allowed.
4. **Integration items owed at the merge. None blocks this gate; all are recorded.**
   - The `.ai/bounded-contexts.md` Reporting entry and a reporting flow doc: still owed.
     - Section 14 forbids the builder from writing `.ai/**`.
     - Recorded in backlog 25 for the coordinator at the merge.
     - The architecture review that OQ-1 asked for has been done by the ARCHITECTURE gate, which reviewed the view approach itself.
     - Row 62 makes this a merge checklist item, so it is not lost.
   - XCT purge coverage for ReportExport: **not covered by XCT as built.** See ruling 1 and row 60. The owner must decide.
   - No real email adapter: confirmed absent on the branch and on master. A21's caveat stands. Outside Development no scheduled email is
     delivered; the notification ends Undeliverable and the run history shows the file. Recorded in backlog 27, so no new row.

FINDINGS
- [MEDIUM] docs/backlog/RPT.md rows 5 and 43; TC-HO-RPT-001.md I-G7 — the deferral says HO-XCT-001's sweep enforces the 7-day `RetainUntil`
  on ReportExport files. As built, XCT does not schedule `stored-file:ReportExport`:
  - `RetentionWorkCycle.SourceCutoff` maps only `boarding-media`;
  - XCT backlog 4 requires an explicit owner decision.
  So files holding R4 contacts and R3 clinical text persist indefinitely after both areas merge, and so does a crash-orphaned object
  (backlog 43). No section 18 row cites I-G7, and the files are unreachable (SECURITY). Row 60; owner question.
- [LOW] docs/test-cases/TC-HO-RPT-001.md:16 — the Summary says "12 cases DEFERRED" but lists 11 IDs. The table and the TEST gate both have 11.
  Also, T-I3 and R-2 still show the throws case as FAIL, although it passes at 5f6bccb0. Row 61.
- [LOW] .ai/bounded-contexts.md and .ai/flows/ — no Reporting context entry or reporting flow doc. This is backlog 25, re-stated as a merge
  checklist item. Row 62.
- [LOW] docs/backlog/RPT.md row 7 — EMR-001 Phase F merged to master at 8da8e834 after this branch was cut. At the merge, re-run
  `ReportingStock*` and `StockReportBuilderTests`, check that visit treatments appear in R5 as `ClinicalUse`, and close or reword row 7 and the
  matching section 1 Amended item by an appended row. Row 63.
- [LOW] HO-RPT-001 section 1 Amended (build); docs/backlog/RPT.md rows 1-3 — the BRD/SUB deferrals say "not merged" and do not cite the owner's
  2026-10-10 skip (recorded in docs/backlog/XCT.md:4). Append the citation so the deferral's authority is stated where the deferral is. Row 64.
- [LOW] tests/CatEcommerce.IntegrationTests/Unit/ReportLimiterTests.cs:13 — `RunNowLikeChain()` copies the run-now chain rather than resolving the
  registered `report-run-now` policy. A change to ReportingModule.cs's order (row 59) would not fail the test. T-I6 covers the window at the
  endpoint but not a refused attempt. Row 65.

UNDECLARED DEVIATIONS
- None found. The section 13 overruns are all recorded in section 1 Amended (7), (20) and (23): the nine migrations instead of eight, the
  missing `AddOperationsReportViews`, ScheduledReportRunner.cs, IScheduledReportRunStore.cs, ReportParameterType.cs, appsettings.Testing.json
  and EfNotificationOutbox.cs. The adaptations to the shipped contracts are recorded in Amended (1)-(11). 5f6bccb0 is declared in its commit
  message and in backlog 58/59.

PROPOSED docs/backlog/RPT.md ROWS (next free number 60)
| 60 | MEDIUM | ReportExport retention / HO-XCT-001 | ACCEPTANCE: HO-XCT-001 as built does not schedule `stored-file:ReportExport` (`RetentionWorkCycle.SourceCutoff` maps only `boarding-media`; XCT backlog 4 needs an owner decision), so the 7-day `RetainUntil` this slice relies on (rows 5, 43; I-G7) is never enforced and report files with R4 contacts and R3 clinical text persist. Owner: approve a ReportExport rule (cutoff = now; the predicate already honours `RetainUntil`), then un-defer I-G7 and C8. | ACCEPTANCE |
| 61 | LOW | TC-HO-RPT-001.md | ACCEPTANCE: the Summary line says "12 cases DEFERRED" but there are 11 (the IDs it lists, the table, and the TEST gate). Also append a note that T-I3 (throws) and R-2 pass at 5f6bccb0 (observed by ACCEPTANCE: 114/114). | ACCEPTANCE |
| 62 | LOW | .ai/bounded-contexts.md, .ai/flows/ | ACCEPTANCE: merge checklist (restates row 25). Add the Reporting context entry (read-only views in `report`; writes only audit, notification requests and its own two tables; FIL producer through its published port) and a reporting flow doc covering the schedule, run and delivery sequence. | ACCEPTANCE |
| 63 | LOW | Row 7 / R5 | ACCEPTANCE: EMR-001 Phase F is on master (8da8e834). At the merge, re-run the R5 tests, confirm visit treatments arrive as `ClinicalUse`, and close or reword row 7 and its section 1 Amended item by an appended row. | ACCEPTANCE |
| 64 | LOW | Section 1 Amended (build); rows 1-3 | ACCEPTANCE: cite the owner's 2026-10-10 decision to skip BRD and SUB (docs/backlog/XCT.md:4) as the authority for R21, R22, R24-R26 and the boarding tiles, not only "not merged". | ACCEPTANCE |
| 65 | LOW | ReportLimiterTests | ACCEPTANCE: the tests copy the run-now chain (`RunNowLikeChain`) instead of using the registered `report-run-now` policy, so row 59's window-last order in ReportingModule is evidenced only by reading the source. Add an endpoint or policy-resolved case: one in flight, then refused, then four more allowed, then the sixth refused. | ACCEPTANCE |

OWNER QUESTIONS
1. ReportExport retention (row 60): should HO-XCT-001's retention worker purge `stored-file:ReportExport` once `RetainUntil` has passed
   (7 days by default)? Until it does, generated report files with contacts and clinical text are never deleted.
2. (Confirm only.) Are R21, R22, R24-R26 and the three boarding tiles to stay out until BRD and SUB are unskipped? Does R23 wait on HO-ORD-001's cart reminders
   (decision 10)? This gate treats both as owner-authorised, on the strength of the 2026-10-10 skip and ORD decision 10.

SCOPE CHECK
- files in allowed scope: 218/218 (b12fd39d..5f6bccb0).
  - Every src/** and tests/** file is a section 13 path or one of the recorded Amended (23) files.
  - The partials and ReportingModule are ADR-341 translations, accepted by ARCHITECTURE.
  - The docs changes are scoped as follows:
    - the handover section 1 cells (ACC, ADM, NTF, RPT) are append-only, as ARCHITECTURE verified;
    - docs/backlog/RPT.md;
    - docs/test-cases/TC-HO-RPT-001.md;
    - docs/gates/HO-RPT-001-*.md;
    - `.ai/decisions/adr.md` is append-only.
  - 5f6bccb0 touches ScheduledReportRunner.cs, ReportingModule.cs, ReportRateLimitPolicies.cs, ReportLimiterTests.cs and RPT.md, all in scope.
- files outside allowed scope: none. No web/**, SRS, user stories, `.ai/**` other than adr.md, .claude/**, NuGet.Config, global.json, *.sln, other
  csproj line or package.
- Out-of-scope work (section 4) found: none.
  - No gateway method (A25).
  - No projection or event handler (U-A3).
  - No DDL outside `report` except the section 11.7 columns (U-X5).
  - No free-text recipient.
  - No subsidised-programme or accounting-export report.
  - No boarding or subscription type was invented.
- Section 20 conditions:
  - 20.1 and 20.2 were hit and reported. The reports are absent; `InvoiceLineKind` and the R23 cart fields were adapted or deferred with
    records. None was worked around.
  - 20.6: the QuestPDF Community licence was confirmed by the owner on 2026-09-26 (HO-CAT-001 Amended (a)). Bengali is embedded, but shaping
    is not asserted (backlog 21).
