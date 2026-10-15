GATE: ACCEPTANCE
HANDOVER: HO-EMR-001
COMMITS: b9c80c5 47612a9 baab800 9d9e2d4 d786ba4 9696dbf 5e4cf63 0805bf1 650556a e1c5e10 3b39c31 75d8c46 2f2e597 729393f dfffd23 (+ gate/doc commits c088ecf 8c2b47b 2525056 b07b45d 0b5f875); branch ho/EMR-001, base master 50f1ab1
DATE: 2026-10-07
STATUS: PASS

Phase F (US-CLN-15: A10, A11, U-F1..U-F9, I-F1..I-F8) is DEFERRED by owner decision. It is recorded as a
deferral. It is not counted as met and not counted as failed.

PRIOR GATES (read from docs/gates/ and docs/test-cases/ on this branch, not from commit messages)
- ARCHITECTURE: round 1 FAIL (1 BLOCKER, 1 HIGH). Both closed by 2f2e597 and 729393f in the combined follow-up
  review HO-EMR-001-REVIEW-R2.md, which is a PASS. This is the follow-up form the owner's gate-scope rule allows.
- CODE_REVIEW: round 1 FAIL (2 HIGH). Both closed in the same REVIEW-R2 PASS.
- SECURITY: PASS (2 MEDIUM, 4 LOW).
- TEST: PASS. Sheet docs/test-cases/TC-HO-EMR-001.md has 0 NOT RUN rows. Its 2 FAIL rows are G3 and G5, gate-added
  MEDIUMs on the accepted backlog list. Its 1 BLOCKED row is G9, the second half of I-A2, which is HO-FIL-001's
  startup guard and not §18 evidence. All 19 DEFERRED rows are Phase F.

RE-EXECUTION BY THIS GATE (database catecommerce_emr only; CATECOMMERCE_TEST_DB was the committed local-dev string
with the database name swapped; catecommerce_dev was not touched)
- `dotnet build CatEcommerce.sln`: 0 warnings, 0 errors.
- Domain 867/867, Application 211/211, Infrastructure 128/128.
- Integration, filtered to the 13 EMR test classes (FilesEndpointTests included): 99 passed, 2 skipped (G3, G5, the
  known defects), 1 failed.
  - The failure was I_G5_AChangedFollowUp_OrAnOwnerBooking_StandsTheReminderDown. RunNotificationDeliveryUntilAsync
    hit its 200-pass bound against the shared database's pending backlog. No assertion failed.
  - Rerun alone twice (I-G4 + I-G5): 2/2 passed both times. See finding M-1.
- I re-checked every §16 non-F case by name in the trx: each has a named test, and each passed.

CRITERIA
- A1 MET. U-B8 is `RecordVitals_WithAWeight_SavesTheWeightRecord_WithTheEntryId_BeforeTheVisitIsUpdated` (pass).
  I-B4 is `I_B4_NurseRecordsVitals_TheWeightReachesTheCatsHistory_WithTheEntryId` (pass). I-B6 is
  `I_B6_AWeightInsertThatFails_RollsBackTheVitalsToo` (pass; a real trigger forces the failure). G6 covers offsets.
- A2 MET. `RecordVitals_TemperatureOutsideTheAbsoluteRange_IsRefusedAndNoConfirmationSavesIt`,
  `_OutsidePlausible_NeedsConfirmation_AndStoresIt`, `_TwoWarnings_OneConfirmed_ListsOnlyTheUnconfirmedOne`,
  `_PlausibleBoundsAreInclusive` and `I_B5_PlausibleRangeWarning_ThenConfirmed_AndAbsurdIsRefusedOutright` all pass.
- A3 MET. `SaveNote_WithAnInactiveCode_IsRefused`, `SaveNote_TheSameCodeTwice_IsRefused`,
  `SaveNote_SnapshotsCodeAndName_AndKeepsThemWhenResavedWithAChangedCatalogue`,
  `I_C1_VetSavesTheNoteAndTwoProblems_VersionIncrements_VisitStaysOpen` and
  `I_C5_TwoVetsSaveWithTheSameExpectedVersion_OneWins_OneConflicts` all pass.
- A4 MET. `I_C2_C3_TheDraftIsReturnedToItsAuthorOnly_AndStopsWhenTheNoteIsSavedAfterwards`, the domain draft tests,
  `TheDraftFirstWriteRace_BothWritesSucceed_AndOneRowRemains` and G8 all pass.
- A5 MET for the built sections. I-E1 is `I_E1_TheSummaryDeclaresEverySection_AndTheUnbuiltOnesNameTheirOwner`, and
  I-E2 is `I_E2_SectionsArePaged_NewestFirst_AndTheTimelineMergesEveryBuiltSource`; both pass. The prescriptions,
  boarding-stays and food-purchases panels are deferred to PHM, BRD and ORD per §4.2 (`available: false`).
- A6 MET. `I_E4_TenYearHistory_ServerBudget_600msSummary_300msSectionPage` passes. TEST measured p95 at 56/58 ms for
  the summary and 46/41 ms for a section page. The fixture has no Phase F treatments.
- A7 MET. I-D1 is `I_D1_OrderingBloodWork_IsRecordedAsPending` (pass).
- A8 MET. The classifier tests (`Classifier_*`), `RecordResults_CopiesTheAnalytesRangeAndUnit_...` and
  `I_D2_ResultValues_AreFlaggedAgainstTheirRange_AndPersistedWithIt` all pass.
- A9 MET. I-D3 through I-D7 pass: hardened headers, DICOM served as an attachment, content-based refusals, URL expiry
  and tampering, scanner unavailable. U-D6 and U-D7 are HO-FIL-001's classes and pass in the Infrastructure and
  Domain runs.
- A10 DEFERRED (Phase F, owner decision). The contracts are left as specified: no TreatmentLine, no treatments route,
  no dispensary controller, no `GetVisitChargeLinesQuery` and no shortfall event. No HO-IMP-001 or HO-SHP-001 file
  was touched.
- A11 DEFERRED (Phase F), as for A10.
- A12 MET, except the Phase F part. Passing tests: I-G1 (`I_G1_StaffAndOwnerReadTheSummary_AndThePdfIsAvailable`,
  which includes addenda listed as corrections), I-G2 (open visit 404), I-G3 (current owner only, including after a
  transfer) and I-C14 (link-only message). In the code, VisitQueryHandlers.cs passes `treatments: []` and
  DischargeSummaryQueryHandlers.cs:76 passes `[], []` for `Treatments` and `MedicinesAdministered`. Those lists stay
  empty until Phase F. Take-home medicines go through the `IDischargeSummaryContributor` slot (HO-PHM-001).
- A13 MET. `EveryMutationMethodOnAFinalisedVisit_IsVisitFinalised_WithAMessagePointingToAddenda`,
  `EveryPublicMethodExceptOpen_IsCoveredByTheFinalisedVisitCheck`, `Finalise_Twice_...`, `I_C8_*` (both classes) all pass.
- A14 MET. `Add_OnAnOpenVisit_IsRefused`, `Add_OnAFinalisedVisit_RecordsAuthorAndTime`,
  `VisitAddendumAggregate_HasNoPublicMethodBesidesAdd`, `I_C10_*` and `I_C11_*` all pass.
- A15 MET. I-C8 passes. I-C9 passes; I read it, and it covers UPDATE, DELETE, INSERT and TRUNCATE on the visit, its
  problems, vitals and addenda, each answered with CR001 and the rows unchanged. I-D9, I-H6, G1 (CR001 answered as
  409 with a rollback) and G2 also pass. The known reparent bypass (G3) is on the accepted MEDIUM list.
- A16 MET. U-G1 (`FollowUpSchedule_SendAfter_*` and `Reminder_*`) and U-G2 (the 8 `VisitFollowUpGuardTests`) pass.
  I-G4 and I-G6 pass. I-G5 passed on two isolated reruns, after the batch-run bound failure (M-1).
- A17 MET. `AttachPhoto_TheTwentyFirst_AndATakenOnTomorrow_AreRefused`, I-D10 (both halves, including the timeline)
  and I-D11 all pass.
- A18 MET. I-C4 is `I_C4_TheTemplateIsOfferedForPreFill_UntilTheFirstSave_AndIsRecordedOnTheNote` (pass).
- A19 MET. I-H1 through I-H4 pass. U-H2 is `Consent_OnScreenWithAFile_AndUploadWithStrokes_AreEvidenceMismatches`
  (pass). U-H4 is the SVG element assertion in I-H2 (only `svg` and `polyline`). For U-H1,
  `Refusals_EvidenceMismatch_InvalidSignature_...` covers the one-point and x = 1.2 cases. The 6000-point case has no
  test, but SignatureStrokes.Create demonstrably refuses it (`MaxTotalPoints = 5000`), so it is met by code (L-1).
- A20 MET. `ConsentRules_IsValidFor_OnlyAMatchingRecordedConsent` and
  `I_H5_TheConsentStatus_FollowsRecordingAndWithdrawal` pass. Enforcement is HO-SRG-001's (§4.2).
- A21 MET. `Finalise_OnlyAVetMay_NotAnAdministratorNorANurse` passes. `U_X4_VisitsFinalise_IsVetOnly_NoAdministrator`
  asserts `[Vet]` exactly. I-C6 asserts 403 plus one AuthorizationDenied row each for reception, nurse and admin.
  The domain re-check is at VisitAggregate.cs:332.
- A22 MET. `RecordClinicalRecordAccess_WritesOneAuditEntry_...AndNeverPublishes`, the two `U_X5_*` tests, I-E3,
  I-E5 (fail-closed, no clinical data) and I-X2 all pass.
- A23 MET. `I_C12_CompleteIsRefusedWhileTheVisitIsOpen_AndSucceedsAfterFinalising` and
  `I_C13_StaffCancelOfACheckedInAppointmentWithAVisit_IsRefused` pass, as do their Application counterparts.

OTHER CHECKS
- ADR-117 to ADR-134: a script compared every Decision and Reason line in .ai/decisions/adr.md against the §9 table,
  and 18 of 18 match word for word. adr.md is append-only (the diff has 168 insertions and 0 deletions). ADR-125
  carries the §1 Amended C-5 narrowing note.
- C-ALL-1: RecordClassification.Consultation.cs classifies all 15 tables the six migrations create. The care tables
  are Clinical/Forbidden, consent_record is Regulatory, and the three catalogues are Operational.
- C-ALL-2: ErasureExemptions.Consultation.cs exempts the "consultation" context, with a reason.
- C-ALL-3: ConsultationRetentionReviewSource is a count-only source and is registered in ConsultationModule.cs:212.
- C-ALL-4: no Remove or Delete method exists on any Visit aggregate. `AClinicalRowIsNeverDeleted_EvenOnAnOpenVisit`
  passes. Problems are withdrawn, never deleted.
- C-ALL-5: ClinicalAuditEntityTypes.Consultation.cs registers the 4 root names and the 6 ClinicalRecordKind names.
  `EveryClinicalConsultationRoot_HasItsAuditEntityTypeRegistered` passes.
- Phase F contract: the visit payload `treatments: []` and the summary `treatments` / `medicinesAdministered: []` are
  as stated above.
- §4.4 forbidden shapes: none of `IFormFile`, `[Authorize]`, 202, MassTransit, `billing.*`, static files or raw HTML
  appears in the changed source. No event handler enqueues. The only HttpDelete is the follow-up route, which §12.4
  specifies.
- Caveats required by §19: OQ-6 is closed by ADR-319 (ClamAV, owned by HO-FIL-001). In this build the clinical upload
  flows ran on the Testing scanner and store. With no scanner configured, uploads are refused (I-D7, FileScanUnavailable).
  On-screen consent signatures work without a scanner. HO-NTF-001 OQ-1: no SMS or email provider, so only the in-app
  copy of visit.summary-available and visit.follow-up-due is asserted. No real GCS or clamd was exercised.

FINDINGS
- [MEDIUM] M-1 tests/CatEcommerce.IntegrationTests/Endpoints/DischargeSummaryAndFollowUpEndpointTests.cs:212 (I-G5),
  through AppointmentsIntegrationTestBase.RunNotificationDeliveryUntilAsync. I-G5 failed 1 of 3 runs at this gate.
  It hit the 200-pass delivery bound while running in a batch on catecommerce_emr, and passed when run alone (2/2).
  The test is coupled to the shared database's pending notification backlog. CODE_REVIEW and TEST had already logged
  this coupling as LOW; this gate raises it to MEDIUM because it has now been observed failing. Backlog fix: filter
  the delivery pass or the request selection to the test's own recipient. It is not a product defect, because the
  guard behaviour passes in isolation and U-G2 covers it at unit level.
- [LOW] L-1 TC-HO-EMR-001.md U-H1 row. The row marks the whole case PASS, but no test covers the 6000-point
  signature. The code refuses it (SignatureStrokes.cs `MaxTotalPoints`). Add a one-line case.
- [LOW] L-2 TC-HO-EMR-001.md. The I-E4 row (39/44 ms, from the implementation run) and the TEST-gate execution note
  (56/58 and 46/41 ms) give different figures. Both are well within budget; this is cosmetic.
- Accepted-for-backlog items, as listed by the owner and the integrator, which this gate does not fail on:
  - the visit_child_frozen OLD.visit_id bypass (G3);
  - the audit snapshot collapsing owned rows (G5);
  - nested result values not being audited (G4);
  - the ordering of stored-file purges. ADR-118's text still says stored files are append-only by trigger, but the
    implementation uses a delete guard (owner decision pending);
  - cross-branch reads returning 404 instead of 403 (APT backlog #8);
  - files outside §13's literal list.

SCOPE CHECK
- files in allowed scope: 191/198 by §13's literal globs and the ARCHITECTURE gate's per-area (ADR-341) and C-ALL
  readings. That count covers docs/gates, the TC sheet and adr.md.
- files outside allowed scope: 7. None is in §14 forbidden scope, and all are already recorded by ARCHITECTURE as
  MEDIUM. They are owed a §1 Amended row by the integrator at merge:
  - src/Api/CatEcommerce.Api/Controllers/ClinicalResponses.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/ClinicalConfigurationExtensions.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/FollowUpSchedule.cs
  - src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs
  - tests/CatEcommerce.Infrastructure.Tests/{Auditing/AuditCaptureExclusionsTests,Documents/QuestPdfDischargeSummaryRendererTests,Notifications/Guards/VisitFollowUpGuardTests}.cs
  None of these was touched: `.ai/**` (other than adr.md, append-only), `.claude/**`, `docs/handover/**`, the SRS or
  the user stories, `web/**`, any csproj, sln or NuGet file, any HO-IMP-001 or HO-SHP-001 file, CatAggregate or
  CatWeightRecordAggregate.
- Out-of-scope (§4) work found: none.
- Undeclared deviations: none found beyond L-1 and L-2, which are evidence-recording gaps, not implementation
  deviations.
