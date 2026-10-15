GATE: ACCEPTANCE
HANDOVER: HO-PHM-002
COMMITS: a8761829..029e817b (branch ho/PHM-002). First pass reviewed a8761829..de95d7b1 and FAILED (94440629). This re-check reviews 94440629..029e817b.
DATE: 2026-10-12
STATUS: PASS

RE-CHECK 2026-10-12 (94440629..029e817b, one commit: 029e817b). Verdict: PASS.
The first pass failed on one HIGH and nothing else. That HIGH is closed. The 18/18 criteria evidence below is unchanged, because the delta touches no web/ file.

Delta reviewed (3 files, +20/-4):
- docs/handover/HO-PHM-002.md:
  - A new row, "Amended (tests, seeder)", records the owner decision of 2026-10-12 verbatim ("Keep it, record it"). It gives the reason (the run's data cannot be produced from web/**) and states the guards:
    - Skipped unless the env vars are set;
    - PharmacyApiFactory refuses catecommerce_dev and catecommerce_test;
    - it seeds through real endpoints plus the repository staff path;
    - it makes two direct batch-expiry writes;
    - it is single-use per database.
  - The row also records the owner's decision to merge with PHM 50 open as MEDIUM.
  - The "Amended (tests)" row now says the real-API run happened (de95d7b1, 20 cases) and names the helpers (PHM 48).
  - Checked against the seeder source: the description matches what the file does.
- tests/CatEcommerce.IntegrationTests/PharmacyE2ESeed.cs: the `[Fact]` becomes `[SkipUnlessSeedEnvironmentFact]`. This is a `FactAttribute` subclass that sets `Skip` when E2E_SEED_FILE or E2E_STAFF_PASSWORD is empty, the supported xunit 2.9.2 pattern. The seeding body is unchanged.
- docs/backlog/PHM.md:
  - rows 54-58 are appended as proposed, with 54, 55 and 56 marked RESOLVED with how;
  - rows 50 and 51 carry the proposed additions;
  - no other row is changed.

Evidence run by this gate on 029e817b:
- `dotnet build tests/CatEcommerce.IntegrationTests` gave 0 errors and 3 pre-existing analyzer warnings, none in this file.
- `dotnet test --no-build --filter FullyQualifiedName~PharmacyE2ESeed` was run with CATECOMMERCE_TEST_DB, E2E_SEED_FILE and E2E_STAFF_PASSWORD unset. Result: "Skipped: 1, Passed: 0, Failed: 0". The test is now reported as Skipped, not as a vacuous pass, and no database was touched.
- No full suite was run (the caller's instruction).

Rulings:
- [HIGH, first pass] PharmacyE2ESeed.cs in a §14 path is RESOLVED. The owner's decision is recorded in the handover's §1 Amended, which is the same remedy that closed HO-APT-002's §14 BLOCKER. The file is now an owner-accepted, declared deviation and no longer counts against scope.
- [LOW, first pass] The vacuous pass is RESOLVED (observed Skipped above).
- [LOW, first pass] The stale "Amended (tests)" row is RESOLVED.
- Still open in backlog, none blocking:
  - PHM 50 (MEDIUM): the owner chose "Merge, fix later", recorded in §1;
  - PHM 51 (MEDIUM): backend;
  - PHM 57 and 58 (LOW).
- New findings in the delta: none.

Scope (re-check):
- 3/3 delta files accepted:
  - the handover's own §1 rows (precedent);
  - the backlog (owner rule of 2026-09-27);
  - PharmacyE2ESeed.cs (owner decision 2026-10-12, recorded).
- Files outside allowed scope without approval: none.

=== FIRST PASS (94440629, 2026-10-12): STATUS: FAIL. Kept below as written. ===


All 18 §18 criteria have concrete evidence. The gate fails on one scope finding (HIGH). The TEST gate added `tests/CatEcommerce.IntegrationTests/PharmacyE2ESeed.cs`. That path is forbidden by §14 (`src/**`, `tests/**`, owned by HO-PHM-001), and no owner decision or §1 Amended row covers it. Under this gate's rules, a changed file outside §13 forbids PASS. The precedent is the HO-APT-002 ARCHITECTURE first pass, which FAILED on a §14 file until an owner decision was recorded in §1 Amended. Nothing else blocks.

PRIOR GATES (read from docs/gates/ and docs/test-cases/)
- ARCHITECTURE: PASS (4fd0e8e0; reviewed a8761829..aa78e655). 0 BLOCKER, 0 HIGH. Backlog PHM 43-49.
- SECURITY: PASS (reviewed a8761829..aa78e655). 0 BLOCKER, 0 HIGH. Backlog PHM 42.
- CODE_REVIEW: PASS (reviewed a8761829..aa78e655). 0 BLOCKER, 0 HIGH. Backlog PHM 35-41.
- TEST: PASS (de95d7b1). 0 BLOCKER, 0 HIGH. Backlog PHM 50-53.
  - TC-HO-PHM-002.md exists. Status counts:
    - 52 component rows: all PASS. U-X1, U-X2 and U-X3 are partial by their own note.
    - 13 MSW E-rows: all PASS.
    - 20 real-API R-rows: 18 PASS, plus 2 deliberate `test.fail` rows (R-A2o, R-A6o). Those 2 are not counted as passes.
    - 0 NOT RUN rows, 0 BLOCKED rows.
  - The real-API evidence column says only "observed". This gate therefore read the asserting code in `web/tests/e2e/pharmacy-real-api.spec.ts` for every R-row it relies on.
- Code since the last code review: 4fd0e8e0..de95d7b1 touches no `web/` production file, only `web/tests/e2e/pharmacy-real-api.spec.ts`. The three code gates therefore still cover the shipped code.

VERIFICATION RUN BY THIS GATE (worktree D:/my-repo/cat_ecommerce-wt/PHM-002 at de95d7b1)
- `npx vitest run tests/components/{prescriptions,pharmacy,controlled-drugs,formulary,diet-authorisations,my-prescriptions,common} --maxWorkers=2`: 14 files, 98 tests, all passed.
- `npx tsc --noEmit`: exit 0.
- `git diff --name-only a8761829..de95d7b1`: nothing under src/**, .ai/**, .claude/**, web/package*.json, tsconfig.json, next.config.ts or eslint.config.mjs. Under tests/** there is exactly one file, PharmacyE2ESeed.cs.
- `git merge-tree --write-tree master de95d7b1`: clean.
- Not run: the backend integration suite, the real-API stack and Playwright (the caller's instruction: memory). The real-API results are the TEST gate's, checked here against the spec source.

CRITERIA (§18)
- A1 MET.
  - Real run, R-A1: the vet issues from `/new?visitId=` through the composer, filling dose, unit, frequency, duration, quantity and instructions. The server detail asserts `catId` and `visitId`.
  - U-A1: the open-a-visit message and the finalised-visit message.
  - Clinical header: the composer route sits under `cats/[catId]/layout.tsx`, which renders `CatProfileDataShell` (header and tabs). Read in source.
- A2 MET.
  - Components: U-A3, U-A7 and U-A8 pass (vitest, this run).
  - R-A1: "Above range", mg/kg and the 4.10 kg weight are visible; submit without a tick shows the field error; the server records `doseCheckOutcome = AboveRange`.
  - R-A5: real 400/409 bodies; the composer replaces its panel on a 409 and keeps the form.
- A3 MET.
  - U-A4.
  - R-A3: the stale-weight message shows and Issue is disabled; Record weight opens a dialog; after the save the panel shows "Latest weight", Issue is enabled and the prescription is issued.
- A4 MET.
  - U-A5.
  - R-A2 on the real API: "Blocking" is shown, the allergy item is present, and without a reason the screen says "Say why you are prescribing despite the allergy." After the reason is given, the prescription is issued and the detail quotes the reason.
  - Ordering (PHM 50, see FINDINGS): U-A5's "listed first" holds only on the mock. On the real API the allergy is listed below the DoseNotCheckable note. The criterion as written ("a blocking warning needing explicit acknowledgement") holds, so A4 is met; the ordering defect is MEDIUM.
- A5 MET.
  - U-A6: Major and Minor are shown with their severity words; only Major needs a tick.
  - R-A2 (second half): a real Major interaction is shown with its note text and an "I have reviewed" checkbox.
- A6 MET.
  - U-E1 to U-E5.
  - R-F1: create a per-kg medicine with an interaction, link a SKU, mark it controlled; it is then prescribable.
  - R-F2: a real 409 StaleVersion shows the reload message and keeps the values.
- A7 MET.
  - U-C6, U-C7, U-C8 and U-C12.
  - R-B4: the witness rule on the real API.
    - Refused, with one generic message: the dispenser as witness, an administrator, and a wrong password.
    - Accepted: a vet witness.
    - Nothing is kept in storage or the URL.
    - The register entry exists.
    - A nurse self-witness is refused server-side.
    - The spec counts 4 dispensing POSTs.
- A8 MET.
  - U-C1, U-C3, U-C4, U-C9 and U-C10.
  - R-B2: the FEFO batch is preselected, the expired lot is disabled, "10.00 of 10.00 ml selected" is shown, the dispense succeeds, stock falls by 10 (HO-IMP stock matrix), the label is printed twice with `printCount 2` on the server, and lot E2E-MELOX-NEAR is on the dispensing.
  - R-B3: a later-expiring batch needs a reason, and the override is recorded.
- A9 MET.
  - U-C2.
  - R-B1: `canDispense = false`, the server's notice is shown, there are 0 dispense buttons and 0 batch checkboxes, a direct POST gets a 4xx, and stock is unchanged at 25.
- A10 MET.
  - U-F3 and U-F4.
  - R-D1: the owner requests a repeat (customer cookie and CSRF); "Requested. We'll message you…" is shown; Withdraw is offered; staff dispense from Repeat requests.
  - The "ready to collect" inbox message (E-D1, real run) needs the notification worker. It is not asserted, and it is not part of the criterion text. See FINDINGS (LOW).
- A11 MET.
  - U-F2 (ended validity).
  - R-D2: repeats exhausted; the reason and "Book a consultation" to `/book?catId=` are shown.
- A12 MET.
  - U-F1.
  - R-D1: the owner's row contains the frequency, "Prescribed by: E2E vet" and "days left", and no lot number anywhere on the page.
- A13 MET for the slice.
  - U-F5: active and expired rows with their texts.
  - R-D3: the vet authorises from a visit; the owner sees it under Authorised foods.
  - The storefront half (visible but not purchasable, cart and checkout re-check) is recorded as HO-SHP-002 / HO-ORD-002's (§4, §19 row 5). It is not assessed here.
  - E-E1's real-run part (time advanced, expired state and expiry message in the inbox) was not run. See FINDINGS (LOW).
- A14 MET.
  - U-D1 to U-D4.
  - R-B5: read-only on the real API.
    - The administrator has no correction control and gets 403.
    - A vet adds a witnessed correction.
    - The original is unchanged and shows exactly one corrected-by link.
  - Integrity is visible: U-D4.
  - "Integrity still intact after the seal worker" (real run) was not run. See FINDINGS (LOW).
- A15 MET.
  - R-A4: the reception session sees the permission-denied page on the composer URL; the server answers 403 for both safety-check and issue (nurse 403 too).
  - MSW E-A2.
- A16 MET.
  - U-A12: tablet and mass units only, no default.
  - U-X1: `[data-clinical-value]` is asserted on the register, detail, safety panel, dispense totals, label and portal rows. By its own note it is not a sweep of every screen.
  - For the screens U-X1 does not assert, this gate read the source:
    - CatPrescriptionsTable:63 renders its dose through `<Quantity>`;
    - DispensingDetail:109 renders line quantities through `<Quantity>`;
    - the dispense totals and the shortfall message carry `unitText`.
  - A grep of the slice's components and pages finds no bare `.amount` rendered without a unit.
- A17 MET.
  - E-X1: axe on every §8.1 route, chromium 28 and mobile-360 28.
  - E-X2: 360/768/1024/1440, chromium 101.
  - U-D5.
  - The TEST gate's re-run reports chromium 137 and mobile-360 129 passed.
- A18 MET.
  - MSW E-A1 (`pharmacy-flows.spec.ts:49`) is one continuous scenario, read in source: the vet goes through the visit picker, issues above range, issues amoxicillin for the allergic cat with a reason, reception dispenses from the queue with the FEFO lots, the label is printed, and the owner sees it.
  - On the real API the same chain is covered in segments:
    - R-A1: issue through the UI;
    - R-B2: UI dispense of an API-issued prescription, the exact batch, and the stock reduction;
    - R-D1: the owner sees it.
  - §19 asks for "E-A1 as one continuous scenario". That holds on MSW only. Recorded as LOW, and the TEST gate owns it.

SCOPE
- Out-of-scope work found (§4): none.
  - No backend change: 0 lines under src/**.
  - No storefront, cart or checkout file.
  - No printer, PDF, barcode, chart or date-picker library: package.json is unchanged.
  - No price shown to the owner.
  - No client-side dose, allergy, FEFO or expiry logic (ARCHITECTURE checked this; the U-X5 scan passes in this run).
- Files outside allowed scope:
  - `tests/CatEcommerce.IntegrationTests/PharmacyE2ESeed.cs`: a §14 forbidden path. HIGH, see FINDINGS.
  - Accepted, as ARCHITECTURE ruled and by precedent:
    - `docs/handover/HO-PHM-002.md`: its own §1 Amended rows;
    - `web/nav/areas/pharmacy.ts` and `web/components/admin/adminNavItems.ts`: §1 Amended (screens)(5);
    - `web/tests/e2e/pharmacy-helpers.ts` and `pharmacy-routes.ts`: backlog PHM 48;
    - `docs/backlog/PHM.md`: the gates' own backlog rows, under the owner rule of 2026-09-27.

FINDINGS
- [HIGH] tests/CatEcommerce.IntegrationTests/PharmacyE2ESeed.cs:1-129 (added by 0ba5997d and 1b6334c1, TEST gate)
  - **Rule:** HO-PHM-002 §14 forbids `src/**, tests/**` ("HO-PHM-001"). §13 lists no backend path.
  - **What the branch does:** it adds a backend integration-test class to HO-PHM-001's test project, an xUnit `[Fact]` that seeds the real-API run's data.
  - **What it touches:**
    - staff accounts through the account repository;
    - cats, visits, locations, SKUs, medicines and goods receipts through real endpoints;
    - batch expiry set directly in the database (`SetBatchExpiryDirectlyAsync`).
  - **Disclosure:** the TEST gate listed the file in its scope check but did not rule it outside scope. No §1 Amended row and no owner decision covers it. The "owner-approved 2026-10-09" in its doc comment approves dev-only test data, not a file in a forbidden path.
  - **Why HIGH and not BLOCKER:**
    - it has no production effect;
    - it is a no-op unless `E2E_SEED_FILE` and `E2E_STAFF_PASSWORD` are set;
    - it inherits `PharmacyApiFactory`'s refusal of catecommerce_dev and catecommerce_test (PharmacyApiFactory.cs:20-25, read);
    - the merge into master is clean.
  - **Why it blocks:** a §14 file with no recorded approval is the shape HO-APT-002's ARCHITECTURE failed on. This gate's rules forbid PASS while any changed file lies outside §13.
  - **The handover's share:** §16 requires a real-API run, and E-B1 needs only-expired stock and staff in four roles. That data cannot be produced from inside §13's `web/**` paths. §13 should have named a seeding location.
  - **To close (owner choice):**
    - (a) Approve keeping the seeder in the backend test project, and record it in HO-PHM-002 §1 Amended (tests). That is a doc-only follow-up, and ACCEPTANCE re-checks only the record.
    - (b) Remove it from ho/PHM-002 and land it under a HO-PHM-001 test follow-up. The real-API run then stands on the TEST gate's record, but this branch can no longer reproduce it.
- [MEDIUM] web/components/prescriptions/SafetyPanel.tsx:204: backlog PHM 50 (existing row, severity confirmed).
  - **What the code does:** the panel maps `assessment.warnings` in server order, with no ordering of its own.
  - **Rule:** §11.2 and U-A5 require "allergy conflicts first … then interactions by severity, then dose". HO-PHM-001 specifies no order (only U-C15: the fingerprint ignores order). The ordering is therefore this slice's job.
  - **Effect on the evidence:** U-A5 passes only because the MSW fixture happens to list the allergy first.
  - **Effect on A4:** none, so not HIGH. On the real API the allergy is still labelled Blocking with danger styling. Issue is refused until it is ticked and a reason is given (R-A2), so it cannot be passed over.
  - **Fix:** sort by warning kind for presentation. That is not clinical logic. Also make the U-A5 fixture list the allergy last, so the test proves the sort.
- [MEDIUM] Remote renewal, first submit always 409: backlog PHM 51 (existing row, severity confirmed).
  - **No §18 criterion covers renewal.** A10 and A11 are the owner's repeat request, not the vet's renewal. So no criterion is unmet.
  - **Behaviour:** the vet re-ticks and submits again, and R-A6 shows the second submit issues. The server binds the acknowledgement to the renew endpoint's own assessment, so consent is not weakened.
  - **The fix is backend, not this slice.** `SafetyCheckRequest` (src/Api/.../PrescriptionRequests.cs:13-19) has no field to exclude the prescription being renewed. The domain port already supports one: `IPharmacyPorts.cs:14-17` `excludePrescriptionId`. The live check therefore cannot match the renew endpoint.
  - **Fix:** add an optional `renewingPrescriptionId` to the safety-check request, and have the composer send it.
- [LOW] tests/CatEcommerce.IntegrationTests/PharmacyE2ESeed.cs:27-31: without the environment variables, the `[Fact]` returns early and is reported as a passing test in every normal pharmacy integration run. Make it a skipped test (for example a skip-unless-env fact attribute), not a vacuous pass. Proposed backlog row 55.
- [LOW] docs/handover/HO-PHM-002.md:24 (§1 Amended (tests)) still says "the real-API re-run (§5.1) was not run". The TEST gate has since run it (20 cases, de95d7b1). The row is stale. Proposed backlog row 56.
- [LOW] The real-run parts that depend on workers were not run, and are not asserted anywhere:
  - E-D1's "ready to collect" inbox message;
  - E-E1's time-advanced expiry and expiry message;
  - E-C1's "integrity intact after the seal worker".
  The TC sheet declares this under "Not covered". None is part of a §18 criterion's text: A10, A13 and A14 are met on their component, MSW and real UI evidence. Proposed backlog row 57.
- [LOW] §19 TEST focus "E-A1 as one continuous scenario" holds on MSW only. The real run covers AC-4 in segments (R-A1, R-B2, R-D1); R-B2 dispenses an API-issued prescription, not the one R-A1 issued in the UI. Proposed backlog row 58.

UNDECLARED DEVIATIONS
- PharmacyE2ESeed.cs in a §14 path, with no Amended row or owner record (HIGH above).
- SafetyPanel renders warnings in server order, not the §11.2 order. It was not declared at build; the TEST gate found it on the real API (MEDIUM above, PHM 50).
- The §1 Amended (tests) row is stale (LOW above).
- None found in production code beyond those already ruled by ARCHITECTURE (PHM 43, the client dose-unit table).

PROPOSED BACKLOG ROWS (docs/backlog/PHM.md, this gate may write only docs/gates; the integrator should append and renumber against master)
- 54 | HIGH (blocking until closed) | tests/CatEcommerce.IntegrationTests/PharmacyE2ESeed.cs | PHM-002 ACCEPTANCE: real-API seeder added in a HO-PHM-002 §14 path (`tests/**`) without an owner record. Owner: approve and record in §1 Amended (tests), or move it to a HO-PHM-001 test follow-up. | ACCEPTANCE 2026-10-12
- 55 | LOW | PharmacyE2ESeed.cs:27-31 | PHM-002 ACCEPTANCE: env-gated seeder `[Fact]` passes vacuously in normal runs; make it a skip-unless-env fact. | ACCEPTANCE 2026-10-12
- 56 | LOW | docs/handover/HO-PHM-002.md §1 Amended (tests) | PHM-002 ACCEPTANCE: the row says the real-API re-run was not run; it ran at de95d7b1 (20 cases). Update it, and name the helpers (PHM 48) and the seeder (row 54) there. | ACCEPTANCE 2026-10-12
- 57 | LOW | web/tests/e2e/pharmacy-real-api.spec.ts | PHM-002 ACCEPTANCE: the real-run worker cases are unasserted: E-D1 "ready to collect" inbox, E-E1 time-advanced expiry and inbox message, E-C1 integrity after the seal worker. Run them once the workers can be started in the e2e stack. | ACCEPTANCE 2026-10-12
- 58 | LOW | web/tests/e2e/pharmacy-real-api.spec.ts | PHM-002 ACCEPTANCE: AC-4 on the real API is segmented (R-B2 dispenses an API-issued prescription); chain R-A1's UI-issued prescription through dispense and the owner's view as one test. | ACCEPTANCE 2026-10-12
- PHM 50 and 51 stay MEDIUM. Add to PHM 50: "also reorder the U-A5 fixture so the test proves the sort". Add to PHM 51: "backend: optional `renewingPrescriptionId` on `SafetyCheckRequest`, wired to the existing `excludePrescriptionId` port parameter".

OWNER QUESTIONS
1. PharmacyE2ESeed.cs: keep it in the backend test project and record it in HO-PHM-002 §1 Amended (option a), or move it out of this branch (option b)? With (a) the remaining work is one doc commit plus an ACCEPTANCE re-check.
2. PHM 50 (allergy below a note on the real API): fix it before merge, or accept it into backlog as MEDIUM? This gate does not block on it. It is the one place the live screen contradicts a ⭐ behaviour in §11.2.

SCOPE CHECK
- files in allowed scope: 124/125 in `git diff --name-only a8761829..de95d7b1` plus this file. 6 of the 124 are accepted equivalents (listed under SCOPE above).
- files outside allowed scope: tests/CatEcommerce.IntegrationTests/PharmacyE2ESeed.cs (§14)
