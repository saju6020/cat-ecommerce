GATE: ACCEPTANCE
HANDOVER: HO-SRG-001
COMMITS: a8761829..6cdcd0b1 (branch ho/SRG-001; first run at a020d79a, re-check at 6cdcd0b1). Build ccf7d3d6 608fefca 882d7cf4 60f3feaf dc3b0d47 20aeeed5 7a232f04 75ae3bda; fix rounds 8719c96a 6d250478; TEST 3b16cfa9 54738f51 a020d79a
DATE: 2026-10-12
STATUS: PASS (re-check of 6cdcd0b1, below; the first run at a020d79a was BLOCKED)

WHY BLOCKED (first run, a020d79a -- superseded by the re-check at the end)
- There is no `docs/gates/HO-SRG-001-TEST.md`. It is not on ho/SRG-001, not on master, not untracked in either tree, and not in any
  commit (`git log --all -- docs/gates/HO-SRG-001-TEST.md` is empty). ARCHITECTURE, SECURITY and CODE_REVIEW each left a record. The
  TEST gate recorded its cases and results in `docs/test-cases/TC-HO-SRG-001.md` (T-01..T-32), but it never recorded a verdict.
  Every other handover in this repo has `<ID>-TEST.md`, and the README says each gate writes one on every run.
- The TC sheet records T-31 (full IntegrationTests) as **FAIL**. The only case still failing is the non-SRG `ShopCataloguePerformanceTests.I_E11`
  (backlog SRG 47, LOW). Under the owner's rule (2026-09-27) and the HO-POR-001 TEST precedent (the same I_E11 was LOW there, and that
  gate PASSED), I expect a TEST PASS. But the TEST gate has to make that call and record it. ACCEPTANCE cannot infer it.
- To unblock: the TEST gate writes `docs/gates/HO-SRG-001-TEST.md` with its STATUS and rules on T-31. Nothing else has to change. The
  criteria review below is complete. If TEST records PASS with no new BLOCKER or HIGH, this gate's re-check is a formality.

PRIOR GATES
- ARCHITECTURE: PASS (re-check 2 of 6d250478, recorded at 4ead0f95; the first pass and re-check 1 FAILED, both closed. The integrator
  decision in HO-SRG-001 section 1 ratifies the `ConsentRules.cs` overload and the HO-APT-001 Amended row.)
- SECURITY: PASS (06012e34; its delta was reviewed inside ARCHITECTURE re-check 1, and no security BLOCKER or HIGH was found)
- CODE_REVIEW: PASS (b4d55ff6; delta reviewed the same way)
- TEST: NOT RECORDED. The TC sheet has T-01..T-30 and T-32 PASS, and T-31 FAIL (I_E11, not SRG).

INDEPENDENT RUN BY THIS GATE (2026-10-12, head a020d79a, `CATECOMMERCE_TEST_DB` = catecommerce_srg; catecommerce_dev not touched; no full suite)
- Domain.Tests `FullyQualifiedName~Surgery`: 96/96 passed.
- Application.Tests `FullyQualifiedName~Surgery`: 16/16 passed.
- IntegrationTests `FullyQualifiedName~.Surgery`: 132/132 passed, 6 m 37 s. The TRX shows every I-* test named below as Passed.

CRITERIA (handover section 18). I re-ran each named test above and read its assertions.
- A1 MET. `SurgerySlotFinderTests::U_B1/U_B2/U_B3`. `SurgeryBookingEndpointTests::I_B2_EachClash_IsNamedOnItsOwn_AndAllTogether_OneViolationEach`
  asserts each clash alone (`[TheatreBusy]`, `[SurgeonBusy]`, `[AssistantBusy]`). Together it asserts three errors in that order, with
  properties `theatreId/surgeonId/assistantId` and the time "10:00–11:00". `I_B3_...` checks the assistant busy as a performer
  (`[AssistantBusy]`) and the reverse (`PerformerBusy`).
- A2 MET. `I_B4_TwentyParallelBookings_OfOneTheatre_ExactlyOneWins` releases 20 tasks from a barrier, then asserts `created == 1`, `TheatreBusy`
  on every loser, and one `clinic.surgery` row. `I_B5_...` runs 6 rounds of surgery against an ordinary booking for one nurse and asserts
  exactly one success each round. The TEST gate added `TwentyParallelBookings_OfOneSurgeon_ExactlyOneWins` and `..._OfOneAssistant_...`
  (T-27, T-28), so all three resources are covered. There are no retries or delays (section 20.4).
- A3 MET. `SurgeryAggregateTests::U_C1` and `SurgeryChecklistEndpointTests::I_C1_ANewSurgery_ShowsFiveOutstandingItems_ByName_OnTheDayList`.
  `I_B8` also asserts `checklist` length 5.
- A4 MET. `I_C1`; `I_C2_ARecordedWeight_CompletesTheWeightItem_ThroughTheCatEvent`;
  `I_C3_ConsentRecorded_CompletesTheConsentItem_AndWithdrawingItRevertsIt`.
- A5 DEFERRED, not passed. US-SRG-09 is not built (section 4.2, Amended (build) (5), backlog SRG 1). HO-BRD-001 has to evidence it. No
  boarding type or column exists (diff sweep).
- A6 MET. `U_C4`, `U_C7_TheRegistryWins_OverAStaleChecklistFlag`, `I_C4_StartWithoutConsent_IsRefused_AndTheSurgeryStaysPreOp`.
- A7 MET. `U_C5`; `I_C5_ANurse_CannotUseTheOverrideRoute`. `I_C6_...` asserts: a 9-character reason is
  `ConsentOverrideReasonRequired` and the surgery stays PreOp; the valid override is 204 and `InTheatre`; `consentOverride.overriddenBy`
  is the vet; exactly one `BusinessEvent` audit row with the vet as actor; and the audit `Reason` equals the text.
- A8 MET. `AnaesthesiaLogEntryTests::U_D1`, `U_D2`, `I_D1_DrugsVitalsAndMilestones_AreOneTimeline_InOrder_EachAttributed`.
- A9 MET. `U_D3`, `I_D1` (one timeline), and `I_D8` (ordering, with the corrected entry flagged).
- A10 MET. `SurgicalReportAggregateTests::U_E3..U_E6`. `I_E2_...` asserts `catId` and `visitId` on the report (I-E4), `409 ReportNotEditable`
  after finalise, and the addendum. `I_E3_TheTriggers_RefuseAnyChangeToAFinalisedReport_AndItsAddenda`. Caveat: the `visitId` is minted
  at booking. EMR does not open that visit (Amended (build) (2), backlog SRG 4).
- A11 MET. `U_E1`, `U_E2`, `NeuterStatusHandlerTests::U_E7_*`. `I_E1_...` asserts `neuterStatus` Neutered, `neuteredOn` equal to the local
  date, and an audit row with the vet as actor.
- A12 MET. `U_A6`, `U_F3` (reminder the day before at 10:00 local), `U_F5`. `I_F3_...` asserts: the instructions snapshot; 2 pending
  follow-ups; one post-op request; 2 reminders with `SendAfter` the due date minus 1 at 10:00 +06; an in-app `surgery.follow-up-due`
  delivered; the customer detail. `I_F4_...` cancels the reminder. **Provider caveat (section 19):** only the in-app copy is
  delivered, because there is no SMS or email provider (section 4.3). This applies to A12 and to the booked, rescheduled, cancelled
  and pre-op messages.
- A13 MET. `U_D4`. `I_D2_...` asserts one register row with actor = the vet, `witness_id`, `cat_id`, `batch_id` and administered
  -2; a wastage row with `wastage_quantity = 1` and the witness; and the log showing `witnessId` and `quantityWasted`.
  `I_D3_EveryControlledDrugRefusal_LeavesNothingBehind`. U-D5 is integration-level only (declared on the sheet). Mechanism: the
  witness is verified in the request, not by a single-use confirmation. This is declared (Amended (build) (16)), and T-23 covers lockout.
- A14 MET. `U_D8`, `I_D5_NoLogRouteAcceptsPutPatchOrDelete`, `I_D6_TheTrigger_RefusesUpdateDeleteAndTruncate_OfALogRow`,
  `I_D7_TheRegisterRow_CannotBeUpdated_AtTheTable`, `I_D8_ACorrection_IsANewLinkedEntry_...`. The unique correction index is covered by T-04 to T-06.
- A15 MET, with whole units only. `I_D2` asserts a ledger row on that `batch_id` with `quantity_delta = -3` and on-hand 7.
  `I_D4_AnExpiredBatch_IsRefused_AndNothingIsWritten`; `U_D7_AStockShortfall_IsRecorded_NotRefused`. Fractional draws are refused by
  HO-IMP-001's whole-unit ledger (Amended (build) (3), backlog SRG 5).
- A16 MET. `U_F1`; `I_F1_TheBoardShowsTheCurrentStatus_AndWhoChangedIt`;
  `I_F2_TheRecoveryBoard_IsAtMostTwoSqlCommands_WhateverTheNumberOfEntries`.
- A17 MET. `SurgeryCampaignAggregateTests::U_G1..U_G3`. `I_G1_...` asserts: snapshot basis `Campaign` at 500.00 with `campaignId` and
  the eligibility note; the cases list with campaign 500.00 against standard 5000.00, plus totals; and a Standard booking that is not
  listed. `I_G2_TheCap_RefusesTheSecondBooking_UntilTheFirstIsCancelled`. Known cap race: backlog SRG 7, LOW.
- A18 MET. `SurgeryCrossCuttingEndpointTests::I_X1_Ac3_ASpayEndToEnd` asserts:
  - Discharged, report Finalised, and the cat Neutered;
  - the status history Scheduled > PreOp > InTheatre > Recovery > Discharged, with an actor on every step;
  - an UPDATE of the register row is refused by the database;
  - the post-op request and 2 follow-up reminders;
  - an audit row with the vet as actor.
- A19 MET. `I_B8_..._WithNoPaymentAnywhere` asserts a `priceSnapshot` of Standard 5000.00, and that none of "deposit", "payment",
  "balance", "invoice", "owing" or "amountdue" appears in the booking or detail JSON. `I_G1` covers the campaign snapshot. The diff sweep
  found no deposit, payment, balance or invoice type.
- A20 MET. `U_X2_TheSurgeryActionRoles_MatchSection12_3` checks that `LogDrug`, `OverrideConsent`, `EndProcedure` and `WriteReport` are
  Vet only, and that Administrator holds no clinical write. HTTP 403 evidence: `I_C5`, `I_C7` (Reception and Admin start, ASA, checklist);
  `TheRolesAreEnforced_AtTheApi_ForLoggingAndReading` (nurse and admin drug log 403); and
  `TheReport_NeedsAnEndedProcedure_AVet_AndEveryRequiredField` (nurse and reception finalise 403; nurse, reception and admin write 403).

FINDINGS
- [BLOCKER, process] docs/gates/HO-SRG-001-TEST.md is missing. The TEST gate left cases and results but no verdict, and T-31 is FAIL
  with no gate ruling. This is the reason for BLOCKED. It is not an implementation defect.
- [LOW] docs/test-cases/TC-HO-SRG-001.md I-C7 row. The case text says a receptionist attempts "start, ASA grade, drug log, report
  finalise". The cited test covers start, ASA and checklist only. The drug-log and finalise refusals are evidenced by other tests (see
  A20), and no HTTP test sends a drug log as Reception. Not added to docs/backlog/SRG.md (this gate writes only docs/gates); proposed as SRG row 49.
- [LOW] docs/test-cases/TC-HO-SRG-001.md. The Summary line still says "99 of 99 cases PASS". The T-rows carry an extra column, and
  rows marked "New" do not name their test (T-02, T-04, T-06, T-10, T-12, T-13, T-15, T-20..T-25, T-27, T-28). The evidence column
  says "Surgery filter run 132/132 passed". This gate's own 132/132 run confirms it, but a reader cannot map those rows to a test
  without the source. Not added to docs/backlog/SRG.md (this gate writes only docs/gates); proposed as SRG row 49.

OWNER QUESTIONS (already open, none blocks)
- Is Anaesthesia consent required as well as Surgery consent? (Amended (build) (1))
- Should Reception read the anaesthesia log and the report? T-25 observes 200 today (SECURITY MEDIUM, backlog SRG 22).
- When a cat is transferred, should an in-care surgery (PreOp, InTheatre or Recovery) move to the new owner? T-21 records the current
  behaviour (backlog SRG 24).
- Fractional controlled-drug draws (backlog SRG 5); EMR opening the surgery's visit (backlog SRG 4); campaign cap race (backlog SRG 7).

SCOPE CHECK
- out-of-scope work found: none. No boarding type, no deposit or payment type, no UPDATE/DELETE path (I-D5, I-D6, I-E3), no
  `[Authorize]`, 202, bus or `FOR UPDATE`. `CatAggregate` is not edited.
- files in allowed scope: 181/181 (`git diff --name-status a8761829 a020d79a`). Files outside the literal section 13 list are declared in
  Amended (build) (11), (14) and (19), and were accepted by ARCHITECTURE. `ConsentRules.cs` (an EMR file) and the HO-APT-001 and
  HO-EMR-001 section 1 rows are authorised by the recorded INTEGRATOR DECISION. HO-SRG-001 and HO-SRG-002 section 1 Amended rows follow
  the established convention. `docs/backlog/SRG.md` follows the owner's 2026-09-27 rule.
- files outside allowed scope: none

UNDECLARED DEVIATIONS
- none in the implementation. Build deviations (1)-(19) are declared in section 1 Amended (build). The only overclaim found is the
  I-C7 row of the test sheet (LOW above).

---

## Re-check (6cdcd0b1)

GATE: ACCEPTANCE
HANDOVER: HO-SRG-001
COMMITS: 0fbf2f61..6cdcd0b1 (one commit: 6cdcd0b1)
DATE: 2026-10-12
STATUS: PASS

The delta is 3 files, all docs: `docs/gates/HO-SRG-001-TEST.md` (new), `docs/test-cases/TC-HO-SRG-001.md` and `docs/backlog/SRG.md` (row 49).
No `src/**` or `tests/**` file changed, so this gate's own run at a020d79a still stands (Domain Surgery 96/96, Application Surgery 16/16,
Integration `.Surgery` 132/132). No suite was re-run.

VERIFIED
- **The BLOCKER is closed.** `docs/gates/HO-SRG-001-TEST.md` exists with STATUS PASS and no BLOCKER or HIGH findings. It rules
  explicitly on T-31:
  - I_E11 is a non-SRG catalogue performance budget failing on a host whose C: drive is 98 percent full. It is LOW (SRG 47) and does not
    fail the gate under the owner's 2026-09-27 rule.
  - The FAIL stays recorded on the sheet.
  - The rest of the first full run's failures passed when rerun alone, and T-28's assertion was widened (SRG 46).
  - Its caveat is recorded here too: no single clean full-suite run was obtained on this host.
- All four prior gates are now recorded PASS: ARCHITECTURE (re-check 2), SECURITY, CODE_REVIEW and TEST.
- **The sheet LOWs are fixed (SRG 49).**
  - The summary line now reports T-01..T-32 (31 PASS, 1 FAIL ruled LOW).
  - The T-rows have the right column count.
  - The I-C7 case text now matches what the test sends, and points to U-X2, `TheRolesAreEnforced_AtTheApi_ForLoggingAndReading` and
    `TheReport_NeedsAnEndedProcedure_AVet_AndEveryRequiredField` for the drug-log and finalise refusals.
  - Every "New" row now names its test. I checked each of the 13 newly named tests (T-02, T-04, T-10, T-12..T-15, T-20..T-25): each exists
    exactly once in `tests/CatEcommerce.IntegrationTests/Endpoints`, and each appears as Passed in this gate's 132/132 TRX.
- The criteria review above is unchanged. A1-A4 and A6-A20 are MET with the evidence named there. A5 (US-SRG-09) is DEFERRED to HO-BRD-001,
  not passed. The provider caveat (in-app copy only) is recorded for the message rows (A12).

FINDINGS
- none new. Open LOWs and MEDIUMs stay in docs/backlog/SRG.md.

SCOPE CHECK
- files in allowed scope: 3/3. `docs/gates/HO-SRG-001-*.md` and `docs/test-cases/TC-HO-SRG-001.md` are listed in section 13.
  `docs/backlog/SRG.md` falls under the owner's backlog rule.
- files outside allowed scope: none

UNDECLARED DEVIATIONS
- none
