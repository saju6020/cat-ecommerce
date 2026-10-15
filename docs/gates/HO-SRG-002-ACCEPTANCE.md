GATE: ACCEPTANCE
HANDOVER: HO-SRG-002
COMMITS: 5a187b6b..1ea99f95 (branch ho/SRG-002). Build b95263fd 14098a39 a49cb937 9bbf3a17 1626a126 e8de5bf1 6ea95dc8 cfcd13ac df419408; fix rounds c2e70472 19e9f304 da303517 5306904c; gate records f1293e56 0e07b9fb 0832958f 921521a6 2af2c64b; TEST 1ea99f95
DATE: 2026-10-14
STATUS: PASS

This verdict is from a static review. The worktree has no node_modules, so this gate ran no vitest or Playwright. It relies on the TEST gate's recorded runs (S1-S4, M1-M4, RA-*), and I read the test sources and production code behind each criterion myself. No BLOCKER or HIGH finding. The MEDIUM and LOW items are below, with proposed backlog rows.

PRIOR GATES (verified from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: PASS. The first pass FAILED (H1). The combined re-check, which covered ARCH H1, CODE_REVIEW H1 + M1-M5 + L1-L6 and SECURITY L1-L2, FAILED on R1 and R2. Re-check 2 PASSED at 2af2c64b. Its N1 and N2 (MEDIUM) were fixed in 5306904c. I read that diff: it changes `ReportTab.tsx`, `useReportAutosave.ts` and `remount.test.tsx` only. TEST then covered it through MU1-MU3 and RA-N2.
- SECURITY: PASS (f1293e56). Both LOWs were closed in c2e70472. The combined re-check verified this (rows 71, 72).
- CODE_REVIEW: the record still says FAIL (0e07b9fb), with no PASS appended to that file. Its HIGH (autosave text lost on a tab switch) and its MEDIUM and LOW items were re-reviewed in the ARCHITECTURE combined re-check and re-check 2. That is one combined follow-up review, as the gate-scope rule asks. I accept that as CODE_REVIEW's closure. Backlog rows 60 and 73 record it.
- TEST: PASS (1ea99f95). `docs/test-cases/TC-HO-SRG-002.md` has 49 rows: 45 PASS, 3 FAIL (M4, MU4, MU5), 1 BLOCKED (RA-E1b), 0 NOT RUN. MU4 and MU5 were killed by `web/tests/components/anaesthesia/gate-strengthening.test.tsx`, which TEST added. They are FAIL rows in the sheet, and I do not count them as passes. They do not affect any criterion.

CRITERIA (handover section 18)
- A1 MET.
  - Component tests (S3 green): `booking.test.tsx` U-A1 (surgeon absent from the assistant options), U-A2 (BusyExplainer labels), U-A3 (three clashes beside their fields, in the summary, focus on the summary), U-A4 (409 race message plus refetch).
  - Real API, `surgery-real-api.spec.ts`:
    - RA-A1: surgery page opens, and the calendar block shows in the surgeon, assistant and theatre views.
    - RA-A2: the explainer names theatre, surgeon and assistant; the busy start is absent; another start books.
    - RA-A3: two real contexts; one wins; the loser got a real `400 SurgeonBusy` from the exclusion constraint, shown as the race text, and the start was gone after the refresh.
  - Recorded deviation: E-A2's clash was another surgery, not a vaccination.
- A2 MET.
  - `preop.test.tsx` U-B1: "3 outstanding: ..." as text.
  - U-B2, 4 cases: actions by nurse, vet and reception; Waive is vet-only.
  - RA-B1, inside RA-X1: the day list read "5 outstanding: fasting instructions, blood work, weight, consent, ASA grade"; after the items were done it showed "Everything is in place to start." (spec line 1095).
  - Recorded deviation: weight and consent were recorded through their real endpoints, not through those areas' screens.
- A3 MET.
  - U-B3 (3 cases), U-B4, U-B5, U-B6 in `preop.test.tsx`. U-B6 ("keeps the override reason across a refetch") is the poll-safety case.
  - RA-B2: Start disabled, consent blocker shown, no override for a nurse, and a nurse override call was a real 403.
  - RA-B3: 9 characters keep Continue disabled; the banner names the vet and the reason; the API status is `InTheatre`.
- A4 MET.
  - U-C1 (timeline order, correction struck and linked) and U-C5 (`timeline.test.tsx` asserts "0.05 mg", "drew 1 ml", "wasted 0.5 ml", "120 bpm"; `drug.test.tsx` covers mg/kg) and U-C6 (`drug.test.tsx`, ImplausibleVitalValue, then Save anyway).
  - RA-C1: "120 bpm", "38.6 °C", "Controlled — witnessed by Akter Nurse" (spec lines 1105-1115).
- A5 MET.
  - `report.test.tsx` U-D1 (2 cases), U-D2 (3 cases), U-D3. `remount.test.tsx` covers N1 and N2.
  - RA-D1: "Finalised by Rahman Vet on"; no Findings field; a direct PUT after finalising was refused.
  - RA-N1 to RA-N3 (real autosave, tab switch, StaleVersion) passed.
  - Note: the RA-D1 assertion accepts 400 or 409 (spec line 1160), although the sheet records 409 ReportNotEditable (row 96).
- A6 MET.
  - U-D4, 3 cases. The applied case asserts `role=status` "Neuter status set to Neutered on". The not-applied case asserts `role=alert` "neuter status has not been updated", and **Apply neuter status** calls the repair endpoint once.
  - RA-D1: the UI says Neutered, and `GET /api/cats/{id}` returns `Neutered`.
  - Note: the handover's U-D4 expects the *reason* to be shown. The backend gives none (section 1 Amended (4c)), so the notice says the record was not updated. The criterion itself (automatic status plus a repair action) holds (row 92).
- A7 MET.
  - `recovery.test.tsx`: U-E1 (2 cases), U-E2 (3 cases), U-E4 (instructions, follow-ups, contact, print-only stylesheet), U-F1 (before, during and after; another customer's surgery is a 404).
  - RA-E2: the post-op sheet carries the protocol text; the owner's page shows the instructions, "Wound check" and "Suture removal", and a `Book follow-up` link starting `/book?`; the owner's inbox holds "Ready to go home" with the follow-ups.
- A8 MET.
  - U-C2: `timeline.test.tsx` "has Correct… and no edit or delete control" queries both buttons and links.
  - U-C3, and U-C4: `drug.test.tsx` asserts the password and identifier are absent from the query cache, an empty mutation cache, the URL, sessionStorage and localStorage, and that they are cleared on cancel.
  - U-C7 (2 cases) and U-C8.
  - Real API:
    - RA-C1 and RA-C2: a second real account typed as the witness; on correction the witness dialog appeared again, then "Corrected", "See the correction" and no edit/delete control (spec line 1127).
    - RA-N4 to RA-N7: one entry, one deduction, two register rows; a wrong witness password writes nothing; a lost response replays with the same entryId; a changed dose gives `AnaesthesiaEntryIdMismatch`.
- A9 DEFERRED, not passed. US-SRG-09 goes to HO-BRD-002 (section 4). No boarding or stay code is in the diff (grep of the changed files).
- A10 MET, on source evidence for the refresh half (see finding F1).
  - Visible on the front desk:
    - U-E3 (`recovery.test.tsx`, 2 cases): order Ready → Recovering → Stable, "since/by" text, a `tel:` link, the empty state.
    - RA-E1, inside RA-X1: the front-desk card showed "Ready for discharge" and "since hh:mm · set by" (spec lines 1171-1175).
  - Refreshes every 30 s: no test asserts this, and RA-E1b is BLOCKED. The source implements it as follows:
    - `web/lib/hooks/useSurgeries.ts:45` sets `SURGERY_POLL_MS = 30_000`.
    - `useRecoveryBoard` (`:91-99`) passes `refetchInterval: SURGERY_POLL_MS` with no condition.
    - `RecoveryBoardPanel` calls only `useRecoveryBoard()`.
    - It is mounted by `FrontDeskView.tsx` on `?tab=recovery`.
    - TanStack Query refetches on that interval while the query is mounted.
- A11 MET.
  - U-A6 (`booking.test.tsx`): no eligibility note gives a field error and nothing is posted.
  - U-G4 (`campaigns.test.tsx`, 6 cases).
  - RA-F1: the admin saves a campaign; the booking needs the note; the surgery says "Campaign price, for information"; the cases page lists the cat and the totals.
- A12 MET.
  - U-A7 (`booking.test.tsx:278-283`): "for information — paid at the clinic" is shown, and no pay, deposit or card button or label exists.
  - RA-X1 asserts no `/pay/i` button on the owner's page (spec line 1202).
- A13 MET.
  - AC-3: RA-X1 is one real-API test (25 s). It books, checks the day list, admits, enforces the checklist and the consent block, starts, logs the anaesthesia, logs a controlled drug with a witness (register rows per RA-N4), corrects it, ends the procedure (neuter status auto-set), finalises, adds an addendum, sets recovery, discharges (follow-ups scheduled), and checks the sheet, the owner page and the inbox. That covers every clause of SRS AC-3 (SRS line 750).
  - AC-18: RA-B4. No Add drug/vitals/milestone, Start, override or End procedure control for reception. Real 403s on milestone, vitals, drug, end-procedure, report PUT and override (reception), and on drug (nurse).
- A14 MET.
  - Axe (`surgery-a11y-responsive.spec.ts`):
    - E-X2 filters `impact === 'serious' || 'critical'` and expects an empty list.
    - It covers all 20 routes in `surgery-routes.ts`, which includes every section 8.1 route and `/staff/front-desk?tab=recovery`.
    - It also opens the override, drug + witness, correction + vitals, and discharge + cancel dialogs.
    - Runs: M2 on chromium, 24 passed. M3 on mobile-360, 24 passed after one documented cold-start sign-in race passed when rerun alone.
  - Width: E-X3 sets viewports of 360, 768, 1024 and 1440 and asserts `scrollWidth <= innerWidth` on every route (M2, 20 passed).
  - Units: U-C5 (above).

RULINGS ASKED FOR
1. FAIL and BLOCKED rows.
   - M4 blocks no criterion.
     - E-X1 is the evidence for A13 (AC-3), which has no width clause, and RA-X1 meets it.
     - A14's "360-1440 px" names E-X2 and E-X3, and both ran at 360 (the a11y spec on mobile-360; responsive at width 360).
     - Section 16 never asked for E-X1 at 360.
     - The 360 px tab-to-select switch is UI-1 by design, and M4's snapshot shows it.
     - The gap is real but outside section 18. It stays as SRG 86.
   - RA-E1b does not block A10, because the 30 s refetch is in source (A10 above).
     - But the sheet's claim that "the poll is covered by the build's hook tests" is not true. No test in `web/tests/**` refers to `useRecoveryBoard`, `SURGERY_POLL_MS` or `refetchInterval` (finding F1).
     - The refresh is evidenced by reading code, not by any test.
   - MU4 and MU5: the tests TEST added kill both mutants. Neither touches a criterion's evidence.
2. Section 16 "both runs recorded": acceptable. Both runs are recorded.
   - MSW: M1-M3, 11 flow, 24+24 axe and 20 responsive cases.
   - Real API: 13 specs in one invocation on a fresh, migrated, throwaway database (RA-0a to RA-0c). The spec is committed (`web/tests/e2e/surgery-real-api.spec.ts`, skipped unless `E2E_REAL_SURGERY=1`).
   - Section 16 asks for a recorded run, not one anyone can re-run. Re-runnability needs a `SurgeryE2ESeed` in `tests/**`, which section 14 forbids to this slice. It is an integrator follow-up (SRG 89). Do it before the next Surgery FE change, not as a merge condition.
   - Row 55 ("real-API E-series not run") is now stale and should point to the run and to row 89 (finding F2).
3. Product deviations in section 1 Amended. None changes the wording of a section 18 criterion, so none needs an owner sign-off to pass this gate.
   - Consent as a page: a consumed HO-EMR-002 page, not a local copy. A2 ("actionable by role") and A3 hold, and section 20.2 is not engaged (ARCH L2).
   - Witness credential in the request:
     - This follows the gated HO-SRG-001 section 1 Amended (16); the backend wins under section 20.4.
     - A8 and U-C4 hold, and the credential is still never stored (SECURITY verified this).
     - Only section 2 point 3 and ADR-FE-071's "goes straight to HO-PHM-001" no longer describe the wire.
   - Portal nav always shown: it differs from section 8.1 ("shown only when the customer has one"), but no criterion mentions it.
     - It is a product choice. I list it as an owner question below.
     - It has no backlog row (row 95).
   - Reschedule by service name: no criterion covers reschedule wording. It fails closed with an explicit message (rows 63, 77). The backend half is row 52.
   - DrugBatchChooser: owner-accepted 2026-10-14 ("Accept it").
     - The decision is recorded in section 1 Amended (3), ADR-FE-078's Status, SRG 50 and IMP 63.
     - A section 20.2 stop resolved by the owner, not worked around. The location-scope gap stays as SRG 51.
4. Backend follow-ups: all four named are recorded.
   - `serviceId` is SRG 52.
   - Structured `ReportIncomplete` is SRG 53.
   - Caller-aware `allowedTransitions` is SRG 54.
   - A theatres endpoint is SRG 57.
   - IMP 63 records the shared batch picker.
   - Not recorded: the other declared contract gaps in section 1 Amended (4b)-(4e). They are no checklist `completedBy` name, no `neuterStatusApplied` reason, no discharge channel, and no branch contact on the owner's surgery detail. Rows 91-94 below.
5. Row 85: yes, treat it as a go-live configuration condition, not as a finding against this FE handover.
   - The setting: `PublicSite:WebBaseUrl` and `Account:WebBaseUrl` must be absolute https URLs in every non-Development environment.
   - The effect otherwise: every Surgery message, including the post-op instructions at discharge, is suppressed as RenderFailed, and staff see no sign of it.
   - That is a patient-care risk in production. The fix (a start-up check plus a staff-visible suppression) belongs to HO-SRG-001 / HO-NTF-001, so it does not fail this gate.
   - Add it to the deployment checklist (row 97).
6. Merge checklist items, all verified against 5a187b6b:
   - Six reverted files: `git diff 5a187b6b..1ea99f95` is empty (0 lines) for `billing/useActiveBranchZone.ts`, `billing/useBillingMutations.ts`, `billing/useBillingQueries.ts`, `useOrderingAdmin.ts`, `usePos.ts` and `useStaffReturns.ts`.
   - `web/lib/api/errors.ts`: numstat +138 / -0, with no removed line. It is append-only.
   - `web/docs/decisions.md`: +86 / -0. The headings ADR-FE-069 to ADR-FE-078 are at lines 1529-1605.
     - `git grep` on master (5a187b6b) finds none of FE-069..078.
     - No collision.
     - Section 13's comment says "069…076", but section 9 reserves 069-078 for this handover, and the extra two are recorded (row 56).
   - IMP row 63: +1 line in `docs/backlog/IMP.md`.
     - Master's IMP.md ends at 62, and no other `ho/*` branch adds a row 63. No collision.
     - Its text records the owner acceptance and the later follow-up.
   - `web/mocks/handlers/index.ts`: +6 / -0. `docs/handover/HO-SRG-002.md`: one changed line (the Amended (build) row). That row reads whole, with a single section 1.
   - SRG rows 83-90: no other `ho/*` branch adds rows of 83 or higher to SRG.md. Rows 91-97 below are free.

FINDINGS (none BLOCKER or HIGH)
- [MEDIUM] F1 docs/test-cases/TC-HO-SRG-002.md (E-E1 MSW row; RA-E1b) — the sheet states that the 30 s recovery-board poll "is covered by the build's hook tests". No such test exists. A10's refresh half rests on source only (`useSurgeries.ts:45,97`). A test with fake timers that advances 30 s and expects a second `GET /api/staff/surgeries/recovery-board` would close it, as would the RA-E1b run. Amend row 90.
- [LOW] F2 docs/backlog/SRG.md row 55 — it says the real-API E-series was not run, but the TEST gate ran it (13/13). Mark it superseded by the TEST run, with re-runnability tracked as row 89.
- [LOW] F3 section 1 Amended (4b)-(4e) — declared contract gaps with no backlog row (rows 91-94).
- [LOW] F4 web/nav/areas/surgery.ts — the portal "Surgeries" entry is always shown, which is declared but has no row (row 95).
- [LOW] F5 web/tests/e2e/surgery-real-api.spec.ts:1160 — RA-D1's edit-after-finalise assertion accepts `[400, 409]`. The sheet records `409 ReportNotEditable`. Pin 409 and the code (row 96).
- [LOW] F6 row 85 — the go-live configuration condition (ruling 5, row 97).

PROPOSED BACKLOG ROWS (docs/backlog/SRG.md, continuing at 91; this gate does not edit the backlog)

| # | Sev | Where | Item | Source |
|---|---|---|---|---|
| 91 | LOW | HO-SRG-001 checklist item | `completedBy` is an id only, so the Pre-op checklist names the person only when it is the surgeon, assistant or overriding vet (section 1 Amended (4b)). Add `completedByName`. | ACCEPTANCE F3 |
| 92 | LOW | HO-SRG-001 end-procedure / surgery detail | `neuterStatusApplied: false` carries no reason, so U-D4's "reason" cannot be shown, and the notice only says the record was not updated (Amended (4c)). Add a reason code. | ACCEPTANCE F3 |
| 93 | LOW | HO-SRG-001 `POST .../discharge` | The response has no delivery channel, so section 11.6's "sent to the owner's {channel}" reads "sent to the owner ... and in their inbox" (Amended (4d)). Return the channel, or accept the wording. | ACCEPTANCE F3 |
| 94 | LOW | HO-SRG-001 `GET /api/customers/me/surgeries/{id}` | Carries the branch NAME only; the phone and address are joined from the public branch list. `bookFollowUpLink` is absolute and is rewritten to `/book?...` (Amended (4e)). Add the branch contact and a relative link. | ACCEPTANCE F3 |
| 95 | LOW | web/nav/areas/surgery.ts | The portal "Surgeries" nav entry is always shown, not "only when the customer has one" (section 8.1). Needs a has-surgeries flag in the portal shell's session/summary, or owner acceptance. | ACCEPTANCE F4 |
| 96 | LOW | web/tests/e2e/surgery-real-api.spec.ts:1160 | RA-D1 accepts 400 or 409 for an edit after finalising; pin `409 ReportNotEditable`. Also amend row 90: no hook test covers the recovery-board 30 s poll (the TC claim is wrong); add a fake-timer test or run RA-E1b. Mark row 55 superseded by the TEST real-API run (row 89 for re-runnability). | ACCEPTANCE F1, F2, F5 |
| 97 | MEDIUM | deployment checklist / HO-SRG-001, HO-NTF-001 | Go-live condition from row 85: `PublicSite:WebBaseUrl` and `Account:WebBaseUrl` must be absolute https URLs outside Development, or every Surgery message (including the post-op instructions) is silently suppressed as RenderFailed. Put it on the release checklist until a start-up check exists. | ACCEPTANCE F6 |

OWNER QUESTIONS (none block this gate)
- Should the portal "Surgeries" entry stay always shown (row 95), or wait for a backend flag?
- Do you confirm row 85 / 97 as a go-live configuration condition, owned by whoever prepares the production settings?

SCOPE
- Out-of-scope work found: none.
  - No payment or deposit control (U-A7, and RA-X1 checks for no pay button).
  - No boarding or stay link (US-SRG-09).
  - No CSV or PDF export.
  - No new dependency: `package.json`, `tsconfig.json`, `next.config.ts` and `eslint.config.mjs` are untouched.
  - No `src/**`, root `tests/**`, `.ai/**` or `.claude/**` change.
- Files outside allowed scope: none unaccepted. 127 files changed in 5a187b6b..1ea99f95.
  - Within section 13 literally, or under its new-hooks-only rule: every file under `web/lib/hooks` is new at 5a187b6b (9 files).
  - The rest are accepted with recorded authority or precedent (ARCH L2/L3, section 1 Amended (5)):
    - `web/nav/areas/surgery.ts`
    - `web/app/(staff)/staff/front-desk/FrontDeskView.tsx`: the Recovery tab only, +15 / -6.
    - `web/lib/api/staff-appointments.ts`: two optional fields, +4 / -0.
    - `web/tests/unit/surgery-format-and-validation.test.ts`
    - `web/tests/e2e/surgery-helpers.ts` and `surgery-routes.ts`
    - `docs/handover/HO-SRG-002.md`: the Amended row only.
    - `docs/backlog/SRG.md` and `docs/backlog/IMP.md`: append-only, under the gate-blocking rule.

UNDECLARED DEVIATIONS
- The TC sheet's claim that hook tests cover the 30 s recovery-board poll (F1). This is a claim about the evidence, not a product deviation.
- Nothing undeclared in the product: every behaviour difference I found is in section 1 Amended or in the TEST gate's recorded deviations.
