GATE: ACCEPTANCE
HANDOVER: HO-CAT-002
COMMITS: 2fcd392 0f6bc0e b1f380c c6cb154 a408ab1 40ea4a8 70ae99a 94a626b 824b1fb bee5be8 542477e c3a076a 1a4cac4 fdef977 ad776b0 09a01fd 820f5dc 0c5a59b 454f6f9 bb9fef1 47d8f10 06121d9 4187ef8 241d310 e4f368c a76aca5 07e0733 6e71fae ee59d2a 24828c5 dbba453 b19928e f478f41 7304f85 7f758b3 8379121 bb24673 64c05f4 ee54879 6baf0bb 9464acd 92a33e5 1bf95d8 b209ef7 c813696 3e9bf01 54d55d3 d0ed408 3fe983f b83bd2a f392132 8b38cc3 ccf29b1 631904f 0efab8c 2e663e9 c2d941c 7f5301c 03226e9 b546fb1 331e527
DATE: 2026-09-25
STATUS: PASS

This is the second pass. It reviewed branch `ho/CAT-002` at head `331e527` against HO-CAT-002
§1 (the Amended rows), §3, §4, §5.1, §13, §14, §16, §17, §18, §19 and §20.

The verdict is PASS **against the owner-confirmed contract**. It is not a PASS for the running
system. Four real-API conditions are carried forward, and one new finding affects A11 end to end:
the HO-CAT-001 backend already on `master` does not implement the amended C10. See FINDING 1. It
needs an owner action before merge but does not block this frontend slice.

HISTORY
- 1st pass (`3e9bf01`): BLOCKED.
  - (1) A11's "shows its next-due date" could not be evidenced under the original C10 contract.
  - (2) ARCHITECTURE, SECURITY and CODE_REVIEW did not cover 14 production commits at the head.
- 2nd pass (`331e527`, this file): PASS. Both blockers are resolved; details below.

INDEPENDENTLY RE-RUN BY THIS GATE AT `331e527` (the working tree was clean before and after)
- `npm run typecheck`: 0 errors. `npm run lint`: clean. `npm run format:check`: clean.
- `npx vitest run`: **104/104 passed**.
- `npx playwright test --workers=1`: **110/110 passed** in 5.5 min, both projects, no flake.
  - Port 3000 was confirmed free first, so the config's `reuseExistingServer` could not attach to
    another worktree's server.
  - The run includes the A11 spec (`cat-parasite-control.spec.ts`), the rewritten back-navigation
    spec, E17 (axe) and E18/E19 (360 px).
- `npm run build` (§17.3): succeeded. No gate had built for production since the TEST 7th pass.

BLOCKER 1 (1st pass): A11 — RESOLVED
- Owner decision: HO-CAT-001 §1 (new Amended row) and HO-CAT-002 §1 (first 2026-09-25 row) record
  the owner-confirmed amendment. C10 returns `{ treatmentId, nextDueOn }`, and `nextDueOn` is a
  required `DateOnly`.
- The contract is consistent everywhere I checked. In HO-CAT-001:
  - the §11.5 handler note and `RecordCatParasiteTreatmentResult(Guid, DateOnly)`;
  - the §12.4 table row, the note and the snippet (no `catId`);
  - U41, I10a and A13.
- The web code matches it exactly:
  - `web/lib/api/cats.ts` `RecordCatParasiteTreatmentResponse = { treatmentId: string; nextDueOn: string }`;
  - the mock `mocks/handlers/areas/cat-profiles.ts:1114` returns `{ treatmentId, nextDueOn }`.
- Display (read in source): `app/(staff)/staff/cats/[catId]/parasite-control/page.tsx` renders the
  server's `nextDueOn` in a `role="status"` region. It never recomputes the date.
- `331e527`: the state is `{ catId, nextDueOn }`, and the banner is derived only when
  `lastRecorded.catId === catId`. So a stale date cannot show, in any frame, on another cat's page,
  including when that cat's profile is already cached.
  - No prior gate reviewed this commit. This gate read it: it implements the LOW suggested by
    ARCHITECTURE (6th pass) and CODE_REVIEW (4th pass), has no new I/O, and is correct.
- Evidence:
  - TC U34 PASS: proven non-decorative by TEST (8th pass), because it fails on the pre-change page.
  - The A11 e2e row PASS: `cat-parasite-control.spec.ts:24` asserts
    `getByRole('status').getByText('Next due 19 Dec 2026')`, also proven non-decorative.
  - Both re-passed in this gate's runs at the head.

BLOCKER 2 (1st pass): gate coverage — RESOLVED
- ARCHITECTURE:
  - 4th pass PASS (`241d310..c813696`, covering the shared-component delta);
  - 5th pass FAIL;
  - 6th pass PASS, up to `b546fb1`.
- CODE_REVIEW:
  - 2nd pass PASS;
  - 3rd pass FAIL (the stale-banner HIGH);
  - 4th pass PASS, up to `b546fb1`.
- TEST: 8th pass PASS, 82/0/4, up to `631904f`.
- Decision on the post-TEST delta `2e663e9..331e527`: **the evidence is sufficient for §18**.
  - Production changes in the delta:
    - the C10 type and mock narrowing (ARCHITECTURE 6th pass and CODE_REVIEW 4th pass);
    - `FormFieldRow` forwarding `required` plus styling. This is inert for validation: every form
      using it sets `noValidate`, so native validation cannot pre-empt the app's announced errors;
    - the ~5-line derived-state change in `331e527`, read here.
  - Every §18 row's tests were re-executed at `331e527` by this gate (see above). The integrator's
    104/104 and the developer's 109/110 at `b546fb1` agree.
- What is still missing, as bookkeeping. None of it is a §18 criterion:
  - The TC sheet (last touched `0efab8c`) has no rows for:
    - the catId-change unit test (`parasite-control-page.test.tsx`, second `describe`);
    - the ceiling test (`tests/unit/lib/use-vaccines.test.tsx`);
    - the rewritten back-navigation spec.
  - That spec's "fails 2/2 on pre-fix code" rests only on the developer's commit message
    (`03226e9`). No gate observed it.
  - The cached-profile case fixed by `331e527` has no test. Its evidence is source reading only.
  - TEST should add these rows on its next touch.
- SECURITY is not needed for this delta. I agree: there is no new I/O, sink, identity or header
  handling. The only I/O change removes a field from a mock response.

FINDINGS
- [OWNER ACTION, before merge — new] `master:src/Api/CatEcommerce.Api/Controllers/CatsController.cs:297-320`.
  An HO-CAT-001 backend already exists on `master`.
  - It arrived in commit `4007225`, 2026-09-21, 196 files, with no gate records in `docs/gates/`.
    Its C10 returns `new { catId, treatmentId }`, and its handler sets
    `Result = command.TreatmentId`. There is **no `nextDueOn`**.
  - The owner-confirmed amendment in HO-CAT-001 §1 says "No implementation of this handover had
    started, so this is a pre-build amendment, not a rebuild". **That is false.** The amendment is
    a change to shipped code on `master`.
  - Consequence at integration: `response.nextDueOn` would be `undefined`, so the page renders no
    banner. The only symptom is the toast, and nothing errors. A11 therefore holds for this
    frontend against the contract, but **is known not to hold against the backend on `master`
    today**.
  - This is not a defect in HO-CAT-002, and nothing on this branch needs to change. The owner
    must:
    - (a) assign the C10 change, plus U41 and I10a, to the HO-CAT-001 implementation on `master`,
      under gates;
    - (b) correct the "no implementation had started" sentence in HO-CAT-001 §1.
  - Related:
    - The original mock returned `{ catId, treatmentId }`, which mirrors this real controller.
      So the `catId` that ARCHITECTURE's 5th pass called "invented" was copied from the real
      controller, not invented. Removing it is still right, because the contract has no `catId`.
    - The TC sheet's "HO-CAT-001 does not exist yet" (Cases I could not run) is inaccurate. The
      code exists; it is unshipped and has no gate records.
    - This gate's 1st pass repeated the same premise, which is corrected here.
- [LOW] `web/tests/unit/cat/parasite-control-page.test.tsx:65-73`: the doc comment still says "a
  `useEffect` keyed on `catId` resets the banner". `331e527` replaced that mechanism with derived
  state, and the test still passes against the new mechanism.
  - The page's own doc comment (`page.tsx:32`, "Reset the banner on every `catId` change") is also
    slightly stale.
  - These are comment drift only.
- [NOTE — carried] Two a11y NOTEs remain the owner's call. Neither breaks §8.5:
  - the Radix `Dialog.Content` has no `aria-modal="true"` (`components/ui/dialog.tsx`);
  - `hideOthers` leaves `<main>` in the landmark list while the sheet is open.
- [NOTE] The component-test IDs differ: the handover §15 calls the case U33, and the TC sheet calls
  it U34 to avoid a collision. This is declared in the TC sheet header.
- [RESOLVED since 1st pass]
  - the `useAllVaccines` unbounded loop: `631904f` adds the ceiling, and `b546fb1` adds the test,
    which passes in 104/104;
  - the wrong-aggregate comment: rewritten in `b83bd2a`;
  - the E6 wording: the owner accepted it (HO-CAT-002 §1, second 2026-09-25 row);
  - the shared StaffShell/`dialog.tsx` edits and the `components/common/*` README rule: owner
    decisions (HO-CAT-002 §1, third row; `web/README.md:100-192`).

CONDITIONS CARRIED FORWARD (not blocking; each is an integration-time check)
- §5.1 / §17.2 step 12: re-run E1–E22 plus the parasite case against the real HO-CAT-001 API once
  it ships, and record both results in the TC sheet. Any mismatch is a §20.7 stop.
  - It is already known that the A11 case will fail until the FINDING-1 C10 change lands.
  - HO-CAT-002 §5 and §20.2 ("not implemented" / "does not exist yet") are stale for the same
    reason.
- Paging caps: confirm the real `PageSize` policy for `pageSize+1`, the `pageSize=1000` weight
  series and `useAllVaccines`.
- Credentialed CORS: `credentials: 'include'` needs the requesting origin echoed together with
  `Access-Control-Allow-Credentials: true`.
  - On `ho/ACC-001` (`c36d1d0`, read only), `AccountsModule.cs:316-320` replaces allow-all with
    `WithOrigins(Cors:AllowedOrigins).AllowCredentials()`.
  - `appsettings.json` ships `AllowedOrigins: []`, and returns early when the list is empty. The
    dev origin `http://localhost:3000` exists only in `appsettings.Development.json.example`. So a
    developer who has not copied it gets **no** CORS headers.
  - Confirm with a real browser request after ACC-001 merges.
  - HO-CAT-002 §12.3 still says `UseCorsToAllowAll()` at `c36d1d0`. The correction was not yet
    committed there when observed.
- US-CAT-08 AC2 ("the previous owner loses access") is not a §18 row, and the UI cannot show it
  (§4.2, §20.3). It moves to Accounts and HO-CAT-001 authorization, which today is a query filter
  only.

CRITERIA (HO-CAT-002 §18)

"TC" means `docs/test-cases/TC-HO-CAT-002.md` (8th pass). The E-rows are MSW-only; see the §5.1
condition. Every cited test also re-passed in this gate's own runs at `331e527` (vitest 104/104,
Playwright 110/110).

| # | Result | Evidence |
|---|---|---|
| A1 | MET | TC E1 PASS. `cat-register.spec.ts:6-17`: lands on `/staff/cats/<id>`, h1 "Nutmeg", "Ragdoll" |
| A2 | MET | TC U2 PASS (`age-display.test.tsx`), plus the `AgeDisplay` render at `CatProfileHeader.tsx:70` |
| A3 | MET | TC U14, U15 PASS (`photo-field.test.tsx`): 8 MB rejected naming 5 MB; GIF rejected naming the accepted types |
| A4 | MET | TC U20 PASS (`neuter-status-form.test.tsx`) |
| A5 | MET | TC U21, U22, E5 PASS. `cat-weights.spec.ts:17,26` "5.5 kg". The Y-axis unit rests on source (`WeightTrendChart.tsx:41`), per the disclosed U22 caveat |
| A6 | MET | TC U4 PASS. The header is mounted from the layout (`[catId]/layout.tsx:35` → `CatProfileFrame.tsx:61`). TC E4 shows it on all 10 tabs |
| A7 | MET | TC U5 and E4 PASS ("None recorded" on all 10 tabs) |
| A8 | MET | TC U6 PASS (badge and note before the name) and E3 PASS (all 10 tabs, `cat-profile-header.spec.ts:18-33`) |
| A9 | MET | TC E6 PASS: "Next due 20 Sep 2029" in the status panel. The history-list wording is owner-accepted (§1) |
| A10 | MET | TC E7 PASS: the message and `aria-invalid="true"` on Batch expiry (`cat-vaccinations.spec.ts:42-43`) |
| A11 | MET (against the amended contract) | TC U34 PASS and the parasite-control e2e PASS, both proven non-decorative. Source: `parasite-control/page.tsx` renders the server's `nextDueOn` in `role="status"`, scoped to its `catId` (`331e527`). **Not true against `master`'s current backend; see FINDING 1** |
| A12 | MET | TC U10 PASS (`vaccination-status-badge.test.tsx`: label and icon for each status) |
| A13 | MET | TC U11 PASS (date plus booking `href`). The §20.5 choice is recorded at `VaccinationStatusPanel.tsx:15-23` |
| A14 | MET | TC U12 PASS (`role=alert` "boarding will be blocked") and E8 PASS |
| A15 | MET | TC U27 and E13 PASS (`cat-transfer.spec.ts:11-19`: what transfers, and the success message) |
| A16 | MET | TC U26, E11, E12 PASS (`cat-deceased.spec.ts:13-15, :25, :31`) |
| A17 | MET | TC U30 PASS, and E12 (`cat-deceased.spec.ts:36-37`: banner shown, Name disabled) |
| A18 | MET | TC E14 PASS (`.pdf` href, 200, `application/pdf`, against the mock) |
| A19 | MET | TC E15 PASS (QR visible; print media hides the controls and keeps the QR) |
| A20 | MET | TC E18, E19 PASS (7th pass: triple isolated runs plus a 360 px measurement on the prod build). Re-passed on both projects in this gate's serial run |
| A21 | MET | TC U26, U27 PASS |
| A22 | MET | TC U29 PASS, E22 PASS (`cat-pagination.spec.ts:5-23`), and R1 for the vaccine list |
| A23 | MET | TC U28, U31, E16, E17 PASS (axe, 17 screens × 2 projects), plus R14–R16 for the below-sm sheet. The two a11y NOTEs are carried |
| A24 | MET | TC U4–U9 PASS, plus the remaining UI-7 fields in source at `CatProfileHeader.tsx:55, :59, :70, :79-82, :99, :126-139`. On every tab via the layout and E3 |
| A25 | MET | TC U9 ("4.2 kg"), U21 and U22 PASS, plus `WeightTrendChart.tsx:41,:44,:55,:68` |
| A26 | MET | TC U25 PASS (disclosed caveat) and E21 PASS (`cat-error-handling.spec.ts:31-33`: no stack, no GUID) |
| A27 | MET | TC E2 PASS (`cat-register.spec.ts:31-35`: field error, no navigation, values kept) |
| A28 | MET | TC E20 PASS (`cat-error-handling.spec.ts:15-21`: conflict explained, typed name kept) |

PRIOR GATES (§19, from `docs/gates/` and `docs/test-cases/`)
- ARCHITECTURE PASS: 6th pass, to `b546fb1`.
- SECURITY PASS: 1st pass, to `06121d9`. No later delta needs it; see above.
- CODE_REVIEW PASS: 4th pass, to `b546fb1`.
- TEST PASS: 8th pass, to `631904f`, 82 PASS / 0 FAIL / 4 BLOCKED / 0 NOT RUN.
- The 4 BLOCKED rows are the real-API conditions above. None is counted as a pass.

SCOPE
- Out-of-scope work found: none.
  - No login, booking screen, search, CSV export, upload route, auto-refresh or dark mode.
  - `web/package.json` dependencies are unchanged.
- §20 conditions: none was worked around. The C10 gap was stopped at and escalated to the owner,
  not shimmed.

UNDECLARED DEVIATIONS
- None by the implementation.
- There is a document defect: HO-CAT-001 §1's owner-confirmed row claims no implementation had
  started. See FINDING 1.

SCOPE CHECK
- Files in allowed scope: 121/123 in the net diff `master...331e527`:
  - 115 under `web/**`;
  - `docs/test-cases/TC-HO-CAT-002.md`;
  - 5 `docs/gates/HO-CAT-002-*.md`.
  This file is one of the 5 gate files.
- Files outside allowed scope: `docs/handover/HO-CAT-001.md` (listed in §14) and
  `docs/handover/HO-CAT-002.md` (not in §13).
  - Both are owner-authorised amendments to the governing documents, not implementation scope
    creep:
    - `3fe983f` was confirmed by the owner (settled 2026-09-25);
    - `2e663e9` only records that confirmation and the owner's "required `nextDueOn`" decision.
  - ARCHITECTURE's 6th pass confirmed these stay inside the confirmed amendment.
  - Not counted as a violation, on that basis.
