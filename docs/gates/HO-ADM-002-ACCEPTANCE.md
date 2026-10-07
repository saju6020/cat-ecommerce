GATE: ACCEPTANCE
HANDOVER: HO-ADM-002
COMMITS: f93b790 6f9da0a 5598fa5 14ea722 b7f18d9 338834f 994305c f2a8970 b7b244d ceb0019 62178ab 2299141 2106914 09b6277 c7f29d7 96889d2 b7065fc ffb1918 ce14242 d0a3758 f26cf1a 8498378 12bbe1a ec00965 bf7d3d0 313b89e fa80c47 9ce0541 e535110 b6ddbf7 f61e926 5db032f (branch ho/ADM-002, range 08e873e..5db032f, 146 files)
DATE: 2026-09-29
STATUS: PASS

# Third pass

The second pass (`f61e926`) failed on one finding only: F-H3. The admin content mock seeded a pending `terms` acceptance for the shared seeded customer, so HO-ACC-002's `account-area.spec.ts` failed 5 of 6.

The fix is `5db032f` (mocks, tests and docs only). This gate observed it closing F-H3, and observed the prompt still opening for a newly published required policy. **All eleven §18 criteria are MET. There is no open BLOCKER or HIGH.**

Owner rule (2026-09-27): only BLOCKER/HIGH fail a gate. MEDIUM/LOW items belong in `docs/backlog/ADM.md` (rows 1-40); this gate has not edited it.

DELTA SINCE THE SECOND PASS (`git diff f61e926 5db032f`)
- Three files changed:
  - `web/mocks/handlers/admin/content.ts`: new `seedAcceptances()`. The seeded customer starts with `terms:2` accepted. It is used both at first load and by `resetAdminContentMockData()`. The persisted `sessionStorage` shape is unchanged.
  - `web/tests/components/content/seeded-customer-pending.test.tsx`: new.
  - `docs/handover/HO-ADM-002.md`: §1 Amended F-H3 row, which accurately describes the change.
- **No product code changed.** `git diff --name-only f61e926 HEAD -- web` lists only `mocks/` and `tests/`.
  - The `/pending` handler is byte-identical.
  - So are `PolicyAcceptancePrompt.tsx` and `(portal)/layout.tsx`.
- No prior gate needs re-running for a fixture-only delta. HO-ACC-002 ACCEPTANCE (second pass) set the precedent for test and doc-only deltas.

VERIFICATION RUN BY THIS GATE (web/, HEAD 5db032f, tree clean before and after)
- Browser runs were one project at a time, one dev server at a time, each stopped afterwards.
- Port 3102 had no LISTEN before or after each run, and `git status` was clean at the end.

| Run | Result |
|---|---|
| `PORT=3102 npx playwright test tests/e2e/account-area.spec.ts --project=chromium --workers=1` | **6 passed** (21.0s); was 5 failed / 1 passed at the second pass |
| same, `--project=mobile-360` | **6 passed** (21.3s) |
| `PORT=3102 npx playwright test tests/e2e/admin-functional.spec.ts --project=chromium --workers=1 -g "E-F2"` | **1 passed** (13.1s) |
| `PORT=3102 npx playwright test tests/e2e/admin-a11y-responsive.spec.ts --project=chromium --workers=1 -g "policy dialog open"` | **1 passed** (9.4s) |
| `npx vitest run tests/components/content` | 6 files, 18 tests passed |
| `npx vitest run` (full) | **85 files, 312 tests passed** |

- The E-F2 pass is meaningful. The spec (`admin-functional.spec.ts:244-292`) publishes a new required policy as staff, then signs in as the seeded customer. It asserts that the dialog contains that policy's row, accepts every row, and asserts the dialog closes and the profile form becomes usable. With the new seed, the prompt can only have opened because of the newly published version.
- Not re-run by this gate (reported by the developer only): the remaining `admin-functional` and `admin-filters` cases, and the other `signInAsCustomer` suites. The delta cannot affect staff-only specs.
  - It can only close a prompt that previously opened for the seeded customer.
  - The admin specs that need a prompt (E-F2, the E-X1 dialog-open case) create their own required policy, and both were re-run above.

§12 PENDING-ACCEPTANCE FIXTURE
- §12 asks for "pending acceptances" among the fixtures. The static seed no longer holds one for the shared customer.
- The pending behaviour itself is intact and evidenced:
  - `seeded-customer-pending.test.tsx` shows that `/pending` is `[]` by default and lists `{ key: 'terms', versionNumber: 3 }` after `POST /api/admin/policies/terms/versions`.
  - E-F2 and the E-X1 dialog-open case produce a real pending acceptance in the browser.
  - U-F3 (`policy-acceptance-prompt.test.tsx`) overrides the handler with two pending acceptances.
- The change is declared in §1 Amended. Only the default *state* changed, not the fixture's capability.
- One side effect: a developer running the app locally on mocks no longer sees the prompt without publishing a version first (L-1 below).

CRITERIA (HO-ADM-002 §18)

The product code is unchanged since the second pass. Every row's evidence stands as recorded there, re-confirmed where noted.

| # | Result | Evidence |
|---|---|---|
| A1 | MET (unchanged) | U-B1, U-B2 and U-B4 (`weekly-hours-editor.test.tsx`), U-B3 (`closure-form.test.tsx`) and E-B1. Rooms are covered by source only (#26). The E-B1 calendar clause has no evidence and no component exists (#23, owner ruling pending); A1's text does not need it |
| A2 | MET (unchanged) | U-B5, E-B2 (X-Branch-Id after switching, through the real `registerQueryClient` path), X-a and X-b. The assignment panel is covered by source only (#25, #26). §11.9 banner deviation: no criterion affected (second-pass ruling, #34) |
| A3 | MET (unchanged) | `detail-pages-403.test.tsx` (real page modules: tax-class, service and roster 403 give `PermissionDeniedState`, and a non-403 gives retry, not a spinner). `form-403-message.test.tsx`. TEST 5th-pass real-browser 403 on tax-class and roster. Both suites are part of this gate's 312/312 |
| A4 | MET (unchanged) | U-D1 to U-D4 and E-D1. `RosterExceptionForm` is covered by source only |
| A5 | MET (unchanged) | U-C1, U-C2 and E-C1 (weak "Current" check, #29) |
| A6 | MET (unchanged) | U-E1 to U-E3 plus source for the PUT and DELETE wiring. The E-E1 downstream half is a real-API condition (#24) |
| A7 | MET (unchanged) | U-G1 to U-G5, `admin-filters.spec.ts` (TEST 5th pass), E-G1 and E-G3. #22 and #31-#33 are MEDIUM |
| A8 | MET (re-confirmed this pass) | U-F1 to U-F3, the content-editor and policy tests, and E-F1. **E-F2 was re-run by this gate on chromium: 1/1 passed.** A newly published required version prompts the customer, and accepting leaves the portal usable. `seeded-customer-pending.test.tsx` shows a new version becoming pending. The E-X1 dialog-open case was re-run: 1/1 passed |
| A9 | MET (unchanged) | E-H1 and `maintenance-switch.test.tsx` |
| A10 | MET (unchanged) | E-X1 and E-X2: 132/132 per project (TEST 5th pass, after the last product change fa80c47). The dialog-open case was re-run here: 1/1 passed |
| A11 | MET (unchanged) | U-F1 and U-F2. There is no `dangerouslySetInnerHTML` in `app/`, `components/` or `lib/` |

PRIOR GATES (unchanged from the second pass, all recorded PASS in docs/gates/)
- ARCHITECTURE: PASS (2nd pass), plus the delta over 2299141..fa80c47.
- SECURITY: PASS, to 2299141. The later deltas add no new I/O, sink or header, as ruled in the second pass.
- CODE_REVIEW: PASS (2nd pass), plus the delta over b7065fc..fa80c47.
- TEST: PASS, 5th pass. Sheet: 77 rows, 74 PASS, 2 FAIL (MEDIUM), 1 BLOCKED (E-E1), 0 NOT RUN.
- The 5db032f fixture delta was verified directly by this gate (above).

FINDINGS
- [HIGH, RESOLVED] F-H3 (second pass). `web/mocks/handlers/admin/content.ts` no longer makes the shared seeded customer pending by default. `account-area.spec.ts` passes 6/6 on chromium and 6/6 on mobile-360, observed by this gate.
- [LOW, new] L-1 — `web/mocks/handlers/admin/content.ts` `seedAcceptances()`. With the default mock state, a developer exercising the portal locally never sees `PolicyAcceptancePrompt` until someone publishes a new required version. §12's "pending acceptances" fixture is now reached only by action, not by seed. That is acceptable and declared in §1 Amended. Optional: note in `web/README.md` how to trigger the prompt locally.
- Carried, not re-raised: backlog #1-#40, including the second pass's F-M7 (#37) and F-M8 (#34). #3 and #39 are closed.

SCOPE
- Out-of-scope (§4) work found: none. The delta is fixture, test and doc only.
- §20: no condition was hit and worked around.

UNDECLARED DEVIATIONS
- None new. 5db032f is declared in §1 Amended.
- Carried from earlier passes and already in the backlog: F-M5 (#27), F-M6 (#28), F-M8 (#34).

SCOPE CHECK
- files in allowed scope: 146/146 in `git diff --name-only 08e873e 5db032f`.
  - This is the second pass's 145 plus `web/tests/components/content/seeded-customer-pending.test.tsx` (§13 `web/tests/components/content/**`).
  - `web/mocks/handlers/admin/content.ts` is covered by §13 `web/mocks/handlers/admin/**`.
  - `docs/handover/HO-ADM-002.md` changed only by the §1 Amended row.
- files outside allowed scope: none

---

# Second pass (f61e926): FAIL — superseded
- A1-A11 were all MET, including A3, which fa80c47 fixed.
- F-H2 was resolved by the ARCHITECTURE and CODE_REVIEW delta records.
- The pass failed on F-H3 [HIGH]: the seeded pending acceptance broke HO-ACC-002's `account-area.spec.ts` (5/6 failing), which is cited evidence for HO-ACC-002 A12/A13. Closed by 5db032f.
- New MEDIUMs F-M7 (errors.ts cross-area reach) and F-M8 (§11.9 banner) are backlog #37 and #34.

# First pass (313b89e): FAIL — superseded
- F-H1 [HIGH]: A3 NOT MET, closed by fa80c47.
- F-H2 [HIGH]: filter commits had not been through ARCHITECTURE or CODE_REVIEW, closed by the delta records.
- MEDIUM/LOW items are backlog rows 23-30.
