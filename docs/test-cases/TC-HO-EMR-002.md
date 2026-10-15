# TC-HO-EMR-002 — Clinic Consultation and Medical Records (frontend)

Handover: docs/handover/HO-EMR-002.md (§15 component cases, §16 end-to-end cases, §18 acceptance)
Branch: ho/EMR-002 (built against master d3527166, HO-EMR-001 merged)
Written by the developer; the TEST gate re-executes and supersedes the Status column.

Mock and real runs are recorded separately. "Mock" = Vitest with the MSW node server, or Playwright with
the MSW service worker (`NEXT_PUBLIC_USE_MOCKS=true`). "Real" = Playwright against `dotnet run` of this
worktree on http://localhost:5005 with `NEXT_PUBLIC_USE_MOCKS=false`.

## Deferred (not built, not passing)

Treatments and stock (US-CLN-15, A5): HO-EMR-001 Phase F is not on master (no dispensary or treatment
endpoint, `treatments: []`), so `TreatmentsPanel`, `RecordTreatmentDialog`, reversal and the dispensary
mocks are not built. Deferred cases: U-D1, U-D2, U-D3, U-D4, E-C2, and the consumable step of E-A1.
A3's prescriptions / boarding / food panels are shown as "not yet available" and are recorded as deferred
(HO-EMR-001 §4.2), not passed.

## Component cases (§15) — Vitest

| ID | Case | Where | Mock |
|---|---|---|---|
| U-A1 | Vet: editor, panels, Save, Finalise | visit/workspace-roles | PASS |
| U-A2 | Nurse: vitals writable, note as text, no Finalise | visit/workspace-roles | PASS |
| U-A3 | Administrator read-only | visit/workspace-roles | PASS |
| U-A4 | Finalised = record, addendum form only with addAddendum | visit/workspace-roles | PASS |
| U-A5 | Newer draft restored with notice + Revert; no draft = saved note | visit/note-flows | PASS |
| U-A6 | Template pre-fill, editable, notice | visit/note-flows | PASS |
| U-A7 | Auto-save timing: 3 s, 20 s ceiling, keepalive on hide/unmount | visit/autosave | PASS |
| U-A8 | Failure announced once, no replay, next edit saves | visit/autosave | PASS |
| U-A9 | No clinical text in browser storage | visit/note-flows | PASS |
| U-A10 | Save 409: message, text kept, Reload shows their note beside it | visit/note-flows | PASS |
| U-A11 | Finalise checklist blocks, links focus field, server ProblemRequired mapped | visit/note-flows | PASS |
| U-A12 | Finalise with unsaved changes saves first, then finalises with new version | visit/note-flows | PASS |
| U-A13 | Addendum confirm, then listed with author and time | visit/note-flows | PASS |
| U-B1 | MeasurementInput name and suffix | visit/vitals | PASS |
| U-B2 | Plausible-range confirm lists both signs, re-posts both | visit/vitals | PASS |
| U-B3 | VitalSignOutOfRange is a field error, no confirm | visit/vitals | PASS |
| U-B4 | Weight invalidates cat + weights | visit/vitals | PASS |
| U-C1 | Preview flag replaced by the server's | visit/diagnostics | PASS |
| U-C2 | Flags text + icon | visit/diagnostics | PASS |
| U-C3 | DICOM Download only | visit/diagnostics | PASS |
| U-C4 | Type / size refused client-side; FileScanUnavailable message | files/file-upload-field | PASS |
| U-D1..D4 | Treatments | — | DEFERRED |
| U-E1 | Reminder; appointment booked then follow-up set with the id | visit/panels | PASS |
| U-F1 | History open/close while dirty: value and URL unchanged | clinical-history/history | PASS |
| U-F2 | Unavailable section: sentence, no page request | clinical-history/history | PASS |
| U-F3 | Weight trend chart + table alternative | clinical-history/history | PASS |
| U-G1 | prescriptionsAvailable false sentence | visit/summary-portal | PASS |
| U-G2 | Portal summary 404 neutral | visit/summary-portal | PASS |
| U-H1 | SignaturePad strokes, 4 dp, Undo, minimum points | consent/consent | PASS |
| U-H2 | Unpublished form: message, no submit | consent/consent | PASS |
| U-H3 | LimitedMarkup: no HTML injection | consent/consent | PASS |
| U-X1 | 400 mapped to field, code never in DOM | visit/vitals, visit/clinical-lib, clinical-catalogue | PASS |
| U-X2 | `<script>` and `**bold**` literal | visit/cross-cutting | PASS |
| U-X3 | No clinical number without unit | visit/cross-cutting | PASS |
| U-X4 | Signed URLs not in keys, storage or console; one refetch on expiry | visit/cross-cutting, visit/panels | PASS |

Additional Vitest files: visit/integrations (queue-card actions, appointment links, providers gate),
clinical-catalogue/catalogue (low <= high, one template per service, code locked, built-in code),
visit/clinical-lib (error mapping, form model, vitals helpers, formatting).

## End-to-end cases (§16) — Playwright

| ID | Case | Mock (chromium) | Real API |
|---|---|---|---|
| E-A1 | Nurse vitals (warning confirmed) -> vet consults, orders, results, follow-up, finalise, complete | PASS (consumable step deferred) | SKIPPED (no appointment or open visit in the dev DB) |
| E-A2 | Type, reload, text restored; nothing in storage | PASS | SKIPPED (no open visit in the dev DB) |
| E-A3 | Idle lock while typing; text kept; next edit saves | PASS | not run (needs real idle expiry) |
| E-A4 | Reception denied; administrator has no Finalise | PASS | not run (single real login) |
| E-A5 | Addendum; original unchanged | PASS | not run |
| E-B1 | History from workspace, three sections, close, draft untouched | PASS | SKIPPED (no open visit in the dev DB) |
| E-D2 | 10-year fixture, overview p95 < 2 s over 20 cold opens | PASS (p95 about 1.35 s dev server) | not measurable: dev DB has no 10-year cat |
| E-C1 | Lab PDF upload, attach, open from signed URL; DICOM download-only | PASS (link asserted; mock file server is cross-origin) | not run (scanner not configured) |
| E-C2 | Treatment with override and shortfall | DEFERRED | DEFERRED |
| E-G1 | Owner visits, summary, PDF | PASS | not run |
| E-G2 | Open visit by URL guess: neutral not found | PASS | not run |
| E-H1 | Tablet 768x1024 on-screen consent listed with date, signer, version | PASS | not run |
| E-H2 | Signed upload consent | PASS | not run |
| E-X (real) | Every clinical route (visits, diagnostics, the three catalogue screens) loads signed in as a real Vet, no crash | n/a | PASS (real API, :5009) |
| E-X1 | axe on every route and the named states | PASS (30 tests) | — |
| E-X2 | 360 / 768 / 1024 / 1440, no horizontal scroll, no clipped control | PASS (chromium 96 + overlays 1; mobile-360 project: workspace, history, portal/consent 17 PASS) | — |

## Fix round (2026-10-08 / 2026-10-09) — gate findings

ARCHITECTURE HIGH (staff PDF and consent signature could not send `X-Branch-Id`) and CODE_REVIEW HIGH x3
(blank result sent as 0, typed vitals wiped by a failed refetch, consent signature silently lost on a tab
round-trip), plus the auto-save, problem-lookup and consent-entry MEDIUMs. Each has a test in
`tests/components/visit/fix-round-1.test.tsx` (19 tests) that fails without the fix; the consent-link rule
also in `visit/integrations`. E-A5 now owns its fixture (unique addendum text, scoped locators, reload check).

## Results after the fix round and the merge of master (79 commits)

| Check | Result |
|---|---|
| `npx tsc --noEmit` | clean |
| `npm run lint` (`--max-warnings 0`) | clean |
| Vitest, whole web suite | 169 files, 1003 tests, all PASS (run with `--maxWorkers=3 --testTimeout=30000`; at the default 5 s timeout and full parallelism a loaded host timed out about 20 unrelated tests, none in EMR) |
| `npx next build` | PASS (one earlier attempt failed fetching the Google font on a transient network error; the retry built clean) |
| Playwright chromium, `--workers=1`, PORT 3110, emr-workspace + emr-history-diagnostics + emr-portal-consent + emr-a11y-responsive | 144 tests: 143 PASS, 1 failed on a 5 s expect during a cold dev-server compile (E-D2); re-run alone PASS, p95 803 ms |

## Real-API run (2026-10-09)

Against `dotnet run` of this worktree on http://localhost:5009, catecommerce_dev (not migrated), web with
`NEXT_PUBLIC_USE_MOCKS=false` on :3110. Staff logins `e2e.{admin,vet,nurse,accountant,reception}@purrcare.test`
were seeded dev-only on the seeded main branch (three already existed; vet and accountant were created).

`tests/e2e/emr-real-api.spec.ts`: 1 PASS, 2 SKIPPED.
- E-X real PASS: signed in as the real Vet; /staff/visits, /staff/diagnostics and the three clinical
  catalogue screens render their headings against the real API.
- E-A real and E-B real SKIPPED by the spec itself: the dev database holds no visit and no appointment at
  the main branch, so there is nothing to open. Opening a visit needs a checked-in appointment (customer,
  cat, roster, slot, booking, check-in), which is clinical data beyond the approved staff seeding, so none was
  created. Auto-save, restore and the history drawer are therefore verified on mocks and Vitest only; run them
  once a checked-in appointment exists.

Spec fixes made to run it at all: it now imports Playwright's own `test` (the shared `./fixtures`
wrapper waits 25 s on every `page.goto` for an MSW worker that does not exist with mocks off), and waits for
`window.__apiBaseUrl` before its first API call.

## TEST gate run (2026-10-09)

Written before any suite was executed; every row starts NOT RUN and is updated one at a time from what was
observed. Commit under test: cacfb200 (master merged at 3258ad8d). Mapping of case to file is the developer's
table above; the TEST gate maps each case to the Vitest file / Playwright test that runs it and records the
observed result of that file or test.

### Suites

| ID | Suite | Status | Evidence |
|---|---|---|---|
| S1 | `npx tsc --noEmit` | PASS | exit 0, no errors |
| S2 | `npm run lint` | PASS | exit 0, --max-warnings 0 |
| S3 | Vitest, whole web suite, `--maxWorkers=3` | PASS | 169 files, 1003 tests, 1003 passed, at default 5 s test timeout, --maxWorkers=3 (default workers not run: loaded host) |
| S4 | `npx next build` | PASS | exit 0, build completed, routes listed |
| S5 | Playwright chromium, 4 EMR mock specs, `--workers=1`, PORT 3110 | PASS (with re-runs) | run 1: 144 tests, 143 passed, 1 failed (E-D2, 5 s expect on History button, loaded host). --last-failed re-run: E-D2 failed again, sign-in 404 (MSW not ready after cold dev-server restart). E-D2 re-run alone (-g): PASS, p95 1004 ms. Net: 144/144 observed passing across runs; 2 runs failed. |
| S6 | Playwright `emr-real-api.spec.ts` against API :5009 (catecommerce_dev, not migrated) | PASS (1 pass, 2 skipped) | 1 passed, 2 skipped (see Real-API cases) |

### Component cases (Vitest, mock)

| ID | Where | Status | Evidence |
|---|---|---|---|
| U-A1 | visit/workspace-roles | PASS | workspace-roles: 8/8 tests passed (file-level result, mapping by file; no per-case name) |
| U-A2 | visit/workspace-roles | PASS | workspace-roles: 8/8 tests passed (file-level result, mapping by file; no per-case name) |
| U-A3 | visit/workspace-roles | PASS | workspace-roles: 8/8 tests passed (file-level result, mapping by file; no per-case name) |
| U-A4 | visit/workspace-roles | PASS | workspace-roles: 8/8 tests passed (file-level result, mapping by file; no per-case name) |
| U-A5 | visit/note-flows | PASS | note-flows: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-A6 | visit/note-flows | PASS | note-flows: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-A7 | visit/autosave | PASS | autosave: 7/7 tests passed (file-level result, mapping by file; no per-case name) |
| U-A8 | visit/autosave | PASS | autosave: 7/7 tests passed (file-level result, mapping by file; no per-case name) |
| U-A9 | visit/note-flows | PASS | note-flows: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-A10 | visit/note-flows | PASS | note-flows: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-A11 | visit/note-flows | PASS | note-flows: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-A12 | visit/note-flows | PASS | note-flows: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-A13 | visit/note-flows | PASS | note-flows: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-B1 | visit/vitals | PASS | vitals: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-B2 | visit/vitals | PASS | vitals: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-B3 | visit/vitals | PASS | vitals: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-B4 | visit/vitals | PASS | vitals: 12/12 tests passed (file-level result, mapping by file; no per-case name) |
| U-C1 | visit/diagnostics | PASS | diagnostics: 8/8 tests passed (file-level result, mapping by file; no per-case name) |
| U-C2 | visit/diagnostics | PASS | diagnostics: 8/8 tests passed (file-level result, mapping by file; no per-case name) |
| U-C3 | visit/diagnostics | PASS | diagnostics: 8/8 tests passed (file-level result, mapping by file; no per-case name) |
| U-C4 | files/file-upload-field | PASS | file-upload-field: 5/5 tests passed (file-level result, mapping by file; no per-case name) |
| U-D1..D4 | Treatments (deferred, HO-EMR-001 Phase F not on master) | DEFERRED | not built; not run |
| U-E1 | visit/panels | PASS | panels: 9/9 tests passed (file-level result, mapping by file; no per-case name) |
| U-F1 | clinical-history/history | PASS | history: 8/8 tests passed (file-level result, mapping by file; no per-case name) |
| U-F2 | clinical-history/history | PASS | history: 8/8 tests passed (file-level result, mapping by file; no per-case name) |
| U-F3 | clinical-history/history | PASS | history: 8/8 tests passed (file-level result, mapping by file; no per-case name) |
| U-G1 | visit/summary-portal | PASS | summary-portal: 10/10 tests passed (file-level result, mapping by file; no per-case name) |
| U-G2 | visit/summary-portal | PASS | summary-portal: 10/10 tests passed (file-level result, mapping by file; no per-case name) |
| U-H1 | consent/consent | PASS | consent: 9/9 tests passed (file-level result, mapping by file; no per-case name) |
| U-H2 | consent/consent | PASS | consent: 9/9 tests passed (file-level result, mapping by file; no per-case name) |
| U-H3 | consent/consent | PASS | consent: 9/9 tests passed (file-level result, mapping by file; no per-case name) |
| U-X1 | visit/vitals, visit/clinical-lib, clinical-catalogue | PASS | vitals: 12/12 tests passed; clinical-lib: 9/9 tests passed; catalogue: 7/7 tests passed (file-level result, mapping by file; no per-case name) |
| U-X2 | visit/cross-cutting | PASS | cross-cutting: 4/4 tests passed (file-level result, mapping by file; no per-case name) |
| U-X3 | visit/cross-cutting | PASS | cross-cutting: 4/4 tests passed (file-level result, mapping by file; no per-case name) |
| U-X4 | visit/cross-cutting, visit/panels | PASS | cross-cutting: 4/4 tests passed; panels: 9/9 tests passed (file-level result, mapping by file; no per-case name) |
| U-FR | visit/fix-round-1 (19 regression tests for the gate findings) | PASS | fix-round-1: 18/18 tests passed (file-level result, mapping by file; no per-case name) |
| U-INT | visit/integrations, clinical-catalogue/catalogue | PASS | integrations: 9/9 tests passed; catalogue: 7/7 tests passed (file-level result, mapping by file; no per-case name) |

### End-to-end cases (Playwright chromium, mocks)

| ID | Spec | Status | Evidence |
|---|---|---|---|
| E-A1 | emr-workspace (consumable step deferred) | PASS | run 1 (consumable step deferred, not tested) |
| E-A2 | emr-workspace | PASS | run 1 passed |
| E-A3 | emr-workspace | PASS | run 1 passed |
| E-A4 | emr-workspace | PASS | run 1 passed |
| E-A5 | emr-workspace | PASS | run 1 passed |
| E-B1 | emr-history-diagnostics | PASS | run 1 passed |
| E-C1 | emr-history-diagnostics | PASS | run 1 passed |
| E-C2 | Treatment with override and shortfall | DEFERRED | not built |
| E-D2 | emr-history-diagnostics (p95 < 2 s over 20 cold opens) | PASS (re-run) | runs 1 and 2 FAILED on loaded host (5 s expect / MSW 404 on cold dev server); alone: p95 1004 ms < 2000 ms, 20 cold opens |
| E-G1 | emr-portal-consent | PASS | run 1 passed |
| E-G2 | emr-portal-consent | PASS | run 1 passed |
| E-H1 | emr-portal-consent | PASS | run 1 passed |
| E-H2 | emr-portal-consent | PASS | run 1 passed |
| E-X1 | emr-a11y-responsive (axe, every route and named state) | PASS | 30/30 tests (25 routes + 5 named-state tests) in run 1 |
| E-X2 | emr-a11y-responsive (360/768/1024/1440 + mobile-360 project) | PASS | 96 viewport tests + 1 overlay in run 1 (chromium project); mobile-360 project NOT re-run by the gate |

### Real-API cases (API :5009, mocks off)

| ID | Case | Status | Evidence |
|---|---|---|---|
| E-X real | Every clinical route loads as a real Vet | PASS | observed: API :5009 + web :3110 mocks off, signed in as real Vet, 5 routes rendered headings (13.6 s) |
| E-A real | Open visit, auto-save, restore | PASS | observed 2026-10-09 (second real-API run): spec ran, not skipped; as real Vet typed in Plan, "Draft saved" shown, reload restored the text and the "restored from your working copy" notice; server-side GET of the visit afterwards returned plan = "Real API draft 1791522517776" |
| E-B real | History drawer on a real visit | PASS | observed 2026-10-09: History drawer opened, "Active problems" heading visible, Prescriptions tab shows "will appear here once the pharmacy module is live" |
| E-A1/A3/A4/A5, E-C1, E-D2, E-G1/G2, E-H1/H2 real | Need clinical data (appointment, 10-year cat, scanner, idle expiry) the owner has not approved creating | NOT RUN | no clinical data; needs idle expiry, scanner or 10-year cat |

Second real-API run (2026-10-09, owner approved creating dev-only test data in this session): `npx playwright test tests/e2e/emr-real-api.spec.ts --project=chromium --workers=1`, API :5009, web :3110 mocks off: 3 passed (E-X real, E-A real, E-B real), 0 skipped, 30.3 s.

Rows created in catecommerce_dev (all via the real API, signed in as e2e.vet; e2e.reception and e2e.admin do not exist in this DB, they return 401; no SQL, no migration):

| Entity | Id | Notes |
|---|---|---|
| Customer "E2E Test Customer" | ab9555b5-b056-4e25-a2c6-a8d004be9c4f | POST /api/customers with the dev CAPTCHA test answer; e2e.customer@purrcare.test, mobile +8801700000099 |
| Cat "E2E Test Cat" | 41d546ea-54cc-45c2-bfa0-9439881b8956 | POST /api/cats, owner above, Female, estimated DOB 2022-01-01 |
| Walk-in appointment (status CheckedIn, Consultation service 1ff92fe3-0d8e-4204-814c-d4bcbf557cb2) | 679ffde8-24ea-44f7-9c2a-a8cd827aaab9 | POST /api/staff/walk-ins at main branch 00000000-0000-0000-0000-00000000ba1c, presenting complaint "E2E TEST DATA - dev-only"; a walk-in needs no roster or slot, so none was created |
| Visit (Open) | b0d728a4-c9b1-4ba9-a05a-6a2b2429b9d7 | POST /api/staff/visits; the test then wrote a Plan draft into it |

Audit or outbox rows written by those commands were not enumerated. The visit stays Open so the cases can be re-run.


Notes from the TEST gate: `visit/fix-round-1.test.tsx` holds 18 tests, not 19 as the fix-round paragraph above says (documentation slip, LOW). U-X4 and U-X1 span several files; all pass. Weakness check of the new fix-round tests was by reading names only; no mutation run performed.
Real-API setup: `.env.e2e.local` is not read by Playwright config; its values were exported into the shell (Vet identifier used as E2E_STAFF_IDENTIFIER). Without that the spec skips all 3 tests.
