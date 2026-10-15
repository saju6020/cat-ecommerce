GATE: ACCEPTANCE
HANDOVER: HO-PHM-001
COMMITS: master(587ea317)...2c5579fc on ho/PHM-001 — 3d57854c 8f87c2dd 9c323e5b 745d944b 234e5680 38235c13 4ce733da 46e084cb 508ccc10 931df290 8f532b07 15b1e65e 9f9167af 0634368e 40afdbf9 fef6cee0 3c73e35b 52628b5b cb78b115 da720334 2c5579fc
DATE: 2026-10-10
STATUS: PASS

Owner rule applied (2026-09-27): only BLOCKER/HIGH findings block. MEDIUM/LOW go to docs/backlog/PHM.md (new rows 30-34).
Read-only review: nothing was built or run by this gate. Evidence comes from the TC sheet, the test source (read for what it asserts),
production source, the 2026-10-09 TRX (tests/CatEcommerce.IntegrationTests/TestResults/phm-full.trx, 07:53 UTC) and the earlier gate records.

PRIOR GATES (from docs/gates and docs/test-cases, not from commit messages)
- ARCHITECTURE: PASS (re-check 2, 2026-10-09). First pass FAIL (HIGH 1, Inventory internals) and re-check 1 FAIL (BLOCKER, SkuRows.cs) were both closed by owner decisions recorded in that file.
- SECURITY: PASS (2026-10-09, reviewed up to 0634368e). The fix-round delta 3c73e35b was re-checked by ARCHITECTURE only, as the owner's re-check rule requires.
- CODE_REVIEW: PASS (2026-10-09, reviewed up to 0634368e).
- TEST: PASS (2026-10-10). TC-HO-PHM-001.md has 129 rows: 127 PASS, 2 FAIL (I-C7, I-F8), 0 BLOCKED, 0 NOT RUN.

THE TWO FAIL ROWS (I-C7, I-F8) — why A3 and A18 still have evidence
- No `src/` file changed after 3c73e35b (`git diff --stat 3c73e35b HEAD` touches only docs and two test files). The TC sheet records both cases
  passing on the fix-round run.
- I-C7 also passes in the 2026-10-09 TRX (07:53 UTC). That run predates the fix round, but 3c73e35b did not touch the prescribing or weight path.
- Failure mechanism, checked in source:
  - The tests use `UtcToday` (PharmacyIntegrationTestBase.cs:34).
  - Production uses the branch-local date (IPharmacyClock; PharmacyWorkCycle.cs `MainBranchTodayAsync`).
  - In the 2026-10-10 failing runs, every behavioural assertion before the date assertion passed:
    - I-C7 (PharmacyPrescribingEndpointTests.cs:207-218): both refusals were `RecentWeightRequired`, naming the weight and its date. It then failed at :219 on "40 days ago", because the branch-local count was 41.
    - I-F8 (PharmacyRepeatEndpointTests.cs:309-311): a single reminder was produced, deduplicated, with `repeatLink`. It then failed at :312, because `SubjectDueOn` was the branch-local today + 3.
- This is a test-side defect (backlog row 29, MEDIUM). Owner rule "nothing merges with a failing test" applies to the merge run, so either fix row 29
  first (recommended; the fix is test-only) or run the integration suite outside 18:00-24:00 UTC.

CRITERIA (§18)
- A1 MET:
  - I_C1 shows 201, the RX-yyyy-nnnnnn number, the cat id, the visit id, the instructions, the dose and the quantity unit, and the visit list contains the prescription.
  - I_C3 shows the refusals VisitCatMismatch, VisitNotOpen and VisitNotFound.
  - U_P1 covers the transition table.
  - Route, frequency and duration appear in the frozen dose line asserted in I_E1:101 ("Give 2 ml by mouth once daily for 5 days").
- A2 MET:
  - U_C1, U_C2 and U_C3 cover the dose arithmetic and the range edges.
  - I_C4 returns 200 with AboveRange, 0.15 mg/kg, expectedRange and a fingerprint, and writes nothing.
  - I_C5 gives 400 PrescriptionWarningsNotAcknowledged, then 201 with the safety record.
  - I_C6 gives 409 PrescriptionWarningsChanged after a weight change, and U_C15 covers fingerprint stability.
- A3 MET:
  - U_C5 covers Missing, Stale at 31 days and Satisfied at 30 days. U_C6 covers a fixed dose with no weight.
  - I_C7 is FAIL on the sheet only because of the test's date arithmetic. The refusal behaviour was observed even in the failing run, and the whole case passed in the TRX. See above.
- A4 MET:
  - U_C7 gives a blocking warning that needs a reason. U_C8 and U_C9 cover the non-matches and the documented misspelling limit.
  - I_C8 shows AllergyConflict blocking, then PrescriptionWarningsNotAcknowledged, then AllergyAcknowledgementReasonRequired, then 201, and the reason is stored.
- A5 MET:
  - U_C10 to U_C14 cover the both-direction rule, merged severity, Minor, the same medicine already active, and profile medications.
  - I_C9 returns a single Interaction, Major, requiring acknowledgement.
- A6 MET:
  - U_B1 to U_B5 cover the formulary rules.
  - I_B1 is 201 and round-trips form, strength, range, classes, interactions and linked SKUs. I_B2 shows a nurse gets 403 plus an AuthorizationDenied row.
  - I_C1 prescribes against a medicine an administrator created.
  - The test body sends contraindications (PharmacyIntegrationTestBase.cs:174-186). The aggregate stores them (FormularyMedicineAggregate.cs:173) and the read projects them (FormularyQueryHandlers.cs:76). No test asserts the round trip; see row 31.
- A7 MET:
  - U_D1 covers WitnessRequired, a witness who is also the dispenser, and a positive delta.
  - I_W1 gives an identical body for six different failure reasons.
  - I_E5 shows WitnessRequired, then 201 with two Dispensed register entries carrying the witness, actor, cat and dose.
  - I_E6 shows a receptionist witness gets WitnessNotVerified, with no stock or register change.
  - I_D5 shows three failures, the account locked (sign-in 401) and masked audit rows.
- A8 MET:
  - U_E1 covers FEFO.
  - U_E7 shows every label field, including `DispensedOnLocal` (the date) and the earliest expiry.
  - I_E1 shows exact ledger rows per batch (-6 and -4) referenced by the dispensing id, the party stamp, and the frozen label with cat, owner, medicine, dose, instructions and expiry.
  - I_E10 shows the print count is 2 with two BusinessEvent rows. I_E12 shows the event lines carry a price snapshot.
- A9 MET:
  - U_E2 and U_E3 cover onlyExpired and the absence of any override input (checked by reflection).
  - I_E2 shows the plan blocked, 400 OnlyExpiredStockAvailable, and the ledger count unchanged.
  - I_E3 shows an administrator, even with a reason, gets 400 BatchExpired.
- A10 MET:
  - U_R1 covers the rule.
  - I_F1 shows the owner's request gets 201, the dispense needs no visit input, the request becomes Dispensed, and pharmacy.repeat-ready reaches the inbox.
- A11 MET:
  - U_R2 covers the rule.
  - I_F2 shows 400 PrescriptionValidityEnded with the date, the vet text and bookLink.
  - I_F3 shows staff dispensing is refused before the sweep has run.
  - I_F4 shows a renewal gets a new number, renewedBy/renewedFrom are linked, the safety check re-runs, and the owner can repeat the new prescription.
- A12 MET:
  - U_A2 covers the activity rules.
  - I_F6 asserts medicine, frequency, remainingDays and issuingVetName, includes a completed prescription whose course still runs, and excludes a cancelled one.
  - The owner-view dose is in the source (CustomerPrescriptionQueryHandlers.cs:69, `Dose` in the view model) but is not asserted by any test; see row 31.
  - I_F7 shows 404 for another customer's prescription.
- A13 MET (server half):
  - I_G1 shows 201, the gate returns Authorised for that cat and SKU with ValidTo, and pharmacy.diet-authorised is enqueued with no clinical text.
  - I_G2 and I_G2b cover NoAuthorisation for another cat, CatNotOwned, NotRequired, CatDeceased and a revoked authorisation.
  - I_G5, I_G5b and I_G5c cover ProductNotPrescriptionDiet, AlreadyActive, exactly one row, and the open-visit check.
  - The storefront and checkout half belongs to HO-SHP-002/HO-ORD-001/HO-ORD-002 (§4.2) and was correctly not built here.
- A14 MET:
  - I_G3 shows the gate answers AuthorisationExpired while the stored status is still Active (the worker has not run).
  - I_G4 shows the worker sets Expired, raises the event once, enqueues the notice once and it reaches the owner's inbox. A second pass is idempotent.
- A15 MET:
  - I_C2 shows receptionist, nurse and administrator each get 403, each with one AuthorizationDenied row.
  - I_G6 shows nurse, administrator and receptionist get 403 on authorise, and nurse gets 403 on extend and revoke. I_G6 does not count audit rows.
  - The refusal is logged by the shared RequireActionFilter (RequireActionFilter.cs:120-132 dispatches RecordAuthorizationDenialCommand on every 403), which every diet endpoint carries (StaffDietAuthorisationsController.cs:27-77). This is the same path I_C2 proves. See row 30.
- A16 MET:
  - U_D3 shows by reflection that the register entry has no mutator.
  - I_D1 shows UPDATE, DELETE and TRUNCATE on entries and seals each raise PH001, and that the four triggers exist.
  - I_D2 shows the interceptor throws for a modified entry, a deleted entry and a modified seal.
  - I_D3 shows a correction creates a new linked entry, the original is unchanged, and the Inventory adjustment is in the same save.
- A17 MET:
  - U_D5 and U_D6 cover the chain and that the canonical form equals the persisted properties.
  - I_D6 shows positions 1..3 chained from ZeroHash and isIntact. I_D6c covers the seal race.
  - I_D7 shows a quantity altered inside a rolled-back transaction gives isIntact false at the right position, and the trigger is enabled afterwards.
- A18 MET:
  - U_R4 covers the reminder window.
  - I_F8 shows the sweep run twice produces one reminder with repeatLink and a dedup key. This was observed even in the failing run; see above.
  - I_F9 shows a repeat dispensed first leaves the reminder Suppressed with SubjectNoLongerApplies.
  - U-R5 has no unit test; the guard is exercised only through I_F9 (backlog row 4).
- A19 MET:
  - I_E13 shows the Inventory recall trace names the cat and current owner from the party stamp.
  - I_E13b shows a reversed dispensing nets to zero.
- A20 MET:
  - I_X3, on one cat: the dose is out of range and the allergy conflicts, both warned; the issue is refused, then acknowledged and 201; the dispense is 201 with the batch at 0 and the label line; the owner's portal lists it.
- A21 MET:
  - U_E4 covers the rule.
  - I_E4 shows FefoOverrideReasonRequired with stock untouched, then 201 with fefoOverrideReason stored and the right batch deducted.

§19 ACCEPTANCE FLAGS (named, not passed silently)
- ADR-153 (the Appendix D role mapping) and OQ-1/OQ-8 are unconfirmed defaults. As built:
  - witness = Vet or Nurse, not the dispenser;
  - Reception may dispense;
  - the Administrator cannot correct the register (I_D4) and may reverse.
  No owner confirmation is on record. These are owed owner decisions, not defects (row 33).
- OQ-3 (remote renewal allowed, flagged IsRemoteRenewal) is also a default that I_F4 exercises.

SCOPE
- Out-of-scope work found: none.
  - No frontend, no storefront or checkout gating, no subscription suspension, no surgery or EMR register writers, no charging, no reports, no statutory register format.
  - PharmacyRetentionReviewSource / ErasureExemptions / RecordClassification are the CONTRACTS C-ALL per-area obligations. They are not retention enforcement (§4.2 HO-XCT-001).
- Files outside §13:
  - (a) Owner-authorised cross-area edits, recorded in the ARCHITECTURE file (owner facts a and b, 2026-10-09):
    - IStockLedger.cs, EfStockLedger.Consumer.cs, FakeStockLedger.cs, FakeStockLedgerTests.cs, InventoryStockPortEndpointTests.cs;
    - HO-IMP-001.md and CONTRACTS.md;
    - SkuRows.cs and HO-SHP-001.md.
  - (b) docs/handover/HO-PHM-001.md §1 Amended (build) row only. This is the established convention.
  - (c) docs/backlog/PHM.md, the gate process file.
  - (d) The nine Pharmacy-owned helper files in backlog row 16 (MEDIUM, "record in §1 Amended at merge"; still open).
  None blocks under the owner rule.

UNDECLARED DEVIATIONS (found by gates or here, not in the Amended (build) row)
- The nine files of row 16 and `IPharmacyClock`. Recorded as MEDIUM 16; still undeclared.
- The §12.1 explicit `WitnessPassword` entry in AuditRedactionRules was not added (row 22, LOW; covered by the `password` regex).
- `GetVisitPrescriptions` dispatches two queries (row 15, MEDIUM).
- FormularyMedicineAggregate.cs:152-153 refuses over-long contraindications with `BusinessViolationCodes.NameRequired` (new row 32, LOW).
- The first-pass undeclared `StockLocationAggregate` load was removed in 3c73e35b.
- Declared deviation (2) changes I-E11's "stock returned to the same batch": the stock goes to Quarantine instead. I_E11 asserts the Quarantine behaviour. It is outside every §18 row's evidence list, so it does not affect a criterion.

FINDINGS
- [MEDIUM] tests/CatEcommerce.IntegrationTests/PharmacyIntegrationTestBase.cs:34. Existing row 29: the tests use `UtcToday` against branch-local production logic, so I_C7 and I_F8 fail from 18:00 to 24:00 UTC. This is a merge-run precondition (see above). Not re-filed.
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/PharmacyDietEndpointTests.cs:308. I_G6 ("AndAuditable") asserts the 403s but not the AuthorizationDenied rows that A15 requires. The evidence rests on the shared filter and I_C2. Add the `CountDenialsAsync` assertion. Backlog row 30.
- [LOW] tests: I_B1 does not assert the contraindications round trip (A6), and I_F6 does not assert the owner-view dose (A12). Both are met in source only. Backlog row 31.
- [LOW] src/Domain/CatEcommerce.Domain/Aggregates/Prescription/FormularyMedicineAggregate.cs:152-153. Over-long contraindications are refused with `NameRequired`, the wrong code for the field. Backlog row 32.
- [LOW] Owner confirmation owed for ADR-153 / OQ-1 / OQ-8 (and OQ-3), the defaults as built. Backlog row 33.
- [LOW] docs/test-cases/TC-HO-PHM-001.md "Acceptance-criteria coverage" says "every named case is PASS above" and marks A3/A18 PASS while I-C7/I-F8 are FAIL in the same sheet. Correct the sheet so it does not overstate. Backlog row 34.

SCOPE CHECK
- files in allowed scope: 206/206 changed files on master...2c5579fc (plus this file), counting §13, the owner-authorised cross-area edits (a), the
  §1 Amended row convention (b) and the backlog file (c); none touches §14's web/**, .ai/** (other than adr.md), SRS/USER_STORIES, *.csproj or packages
- files outside allowed scope: none blocking; the nine backlog-row-16 files are outside §13's literal list (MEDIUM, open)
