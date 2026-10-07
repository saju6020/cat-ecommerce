# TC-HO-FIL-002 — Shared file uploader / list / thumb / viewer (frontend)

Handover: docs/handover/HO-FIL-002.md
Commit(s) under test: fabdc6b c59b4a5 560d5ce 62f7b5e db613bf 4bb04e2 2d5a6ee 53606a1 c962934
f981e57 3934ce5 5c0d8a3 7b345db **211661f** (branch head, `ho/FIL-002`)
Written: 2026-09-28 · Executed: 2026-09-28

> Statuses record what was **observed**, not inferred. `BLOCKED` marks a case that could not be
> executed, or whose result was reasoned about rather than watched — it is not a failure, and it is
> not hidden.
>
> **§16 lists only E1–E3** (end-to-end). HO-FIL-002 §19's own "Required gates" row for TEST names
> **"C1–C10, E1–E3"** — §15's ten component-level cases plus §16's three end-to-end ones — so both
> are enumerated here as the contract this gate actually owes, not §16 alone. Unit/component IDs use
> the handover's own `C#` numbering (§15); end-to-end IDs use `E#` (§16). Rows for gate-fix
> regression coverage (fixed findings from ARCHITECTURE/SECURITY's first pass) are `R#`; three
> targeted, ad hoc, **non-committed** verification tests I wrote and ran myself this session to
> settle gaps in the existing regression coverage are marked as such in Evidence and were deleted
> before this sheet was finalised (nothing was committed).
>
> **Process disclosure, not hidden:** the case-first rule this gate is supposed to follow is "write
> the sheet, then execute". This gate did not do that cleanly — the task brief itself named the exact
> commands to run (`tsc`, `lint`, the full `vitest run`, the specific Playwright specs, the production
> build/curl check) up front, and I ran all of them, plus read every existing test file to build the
> row list, **before** drafting this document, then filled in every row's status from what I had
> already observed rather than executing against a fully blank sheet. The row *enumeration* itself is
> not results-fitted: every ID here is either the handover's own §15/§16 numbering or an
> already-named test string that existed in the repository before this session touched anything
> (e.g. `"C3 — a server FileContentMismatch is shown..."` is the test's own literal name, not a label
> I invented after seeing it pass). But the sequencing itself is the wrong order for this role, and is
> recorded here rather than silently presented as if every row had started `NOT RUN`.

## Summary

| | Count |
|---|---|
| Total | 29 |
| PASS | 28 |
| FAIL | 0 |
| BLOCKED | 0 |
| DEFERRED (owner) | 1 |
| NOT RUN | 0 |

Breakdown: 10 unit/component cases (C1–C10, all PASS) + 3 end-to-end cases (E1–E3, all PASS) + 10
regression cases (R1–R10, all PASS) + 6 acceptance-criteria rows (A1–A5 PASS, A6 DEFERRED by the owner).

> **Updated 2026-09-28 after the owner's §18 A1/A6 decisions** (HO-FIL-002 §1 Amended): at TEST time
> A1 and A6 were BLOCKED against the original wording (27 PASS / 2 BLOCKED). A1 was reworded and is
> now evidenced by C1–C10; A6 was deferred to the Phase 6 performance pass.
"Cases I could not run" and "Gaps left deliberately" are informational lists, not counted rows.

## Environment

- **Node** `v24.21.0`, **npm** `11.19.0`.
- **Build tools**: `npx tsc --noEmit` — clean, 0 errors. `npm run lint` (`eslint . --max-warnings 0`)
  — clean, 0 warnings.
- **Unit/component suite**: Vitest 5.0.1 + Testing Library + jsdom, full repo suite (not filtered to
  this slice). `npx vitest run` — **69 files, 310 tests, all passed** (59.8s). The four
  `Not implemented: navigation to another Document` jsdom warnings are pre-existing, unrelated to
  this slice (jsdom's own limitation, printed by other areas' specs that trigger a real navigation).
- **E2E suite**: Playwright 1.63.0, Chromium already installed locally. Ran exactly the three files
  named in the brief — `tests/e2e/files-a11y.spec.ts`, `tests/e2e/files-overflow.spec.ts`,
  `tests/e2e/files-upload.spec.ts` — `PORT=3103 npx playwright test ... --project=chromium
  --project=mobile-360 --workers=1` (single worker, dedicated port, per the brief).
  - **First run**: 100 tests, **98 passed, 2 failed** — both failures were the same case
    (`C1/E1 — keyboard-only pick uploads a file...`), one per project (`chromium`, `mobile-360`),
    both timing out on `page.waitForEvent('filechooser')`. This is the first spec in the file to hit
    a route Playwright's dev-server `webServer` had not yet warmed up (a cold Next.js dev compile of
    `/dev/file-storage-harness` and its lazy `HarnessContent` chunk).
  - **Second run** (same command, same port, immediately after): **100/100 passed**, including both
    previously-failing cases, on both projects, in the same single-worker serial mode (no
    parallelism to blame). Per the brief's own instruction ("rerun once serially before calling it a
    product failure, and report both runs"), this is recorded as environment/warm-up, not a product
    defect. Both runs are reported above; nothing was silently discarded.
  - Port 3103 confirmed free (no `LISTENING` socket) after both runs.
- **Mocks**: `NEXT_PUBLIC_USE_MOCKS=true`, `NEXT_PUBLIC_API_BASE_URL=http://localhost:5005`
  (`.env.local`) — nothing is actually listening on that port; MSW intercepts every request in both
  Vitest and Playwright. **No real backend (HO-FIL-001) was started or reachable.** Every case below
  ran against MSW fixtures/handlers only (`web/mocks/handlers/files/*`).
- **Production-build check** (ARCHITECTURE's extra request): `npm run build` — compiled clean (0
  type errors), `/dev/file-storage-harness` listed in the route table at 346 B / 103 kB First Load
  JS (consistent with only the `notFound()`-calling shell, not the harness content). `PORT=3104 npm
  run start`, then `curl`:
  - `GET /dev/file-storage-harness` → **HTTP 404** (Next's own not-found page markup).
  - `GET /sign-in` (sanity control, a real route) → **HTTP 200**.
  - `.next/app-build-manifest.json`'s own entry for `/dev/file-storage-harness/page` lists five
    files, none of them the chunk that contains `HarnessContent`'s content (see R1 below) — the
    route's own declared chunk list does not reference it at all, not merely "unfetched at runtime".
  - The production server (port 3104) was stopped afterwards (`taskkill`); port 3103 and 3104 both
    confirmed free (no `LISTENING` socket) at the end of this session.
- No product code was edited. Three ad hoc verification test files were written, run, and then
  **deleted** before this sheet was finalised (`tests/unit/files/_tmp-verify.test.ts`,
  `tests/components/files/_tmp-verify.test.tsx`) — `git status` is clean of them.

## Unit / component cases (§15)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| C1 | Keyboard only: tab to the control, `Enter`, choose a file | Uploads; no pointer needed | PASS | `tests/components/files/file-uploader.test.tsx` "C1 — the drop-zone button is the only tab stop..." — watched pass in the full `vitest run`. |
| C2 | File over the cap | Refused client-side; no request sent | PASS | `file-uploader.test.tsx` C2 (message shown, no Retry) **and** `tests/unit/files/use-file-upload.test.tsx` C2 (`fakeXhrRequests()` has length 0) — both watched pass. |
| C3 | `.exe` renamed `.png` (server `FileContentMismatch`) | Server sentence shown, not raw text | PASS | `file-uploader.test.tsx` C3 — asserts the §8.2 sentence is present and the raw server phrase ("does not match its declared type") is absent. |
| C4 | `FileScanUnavailable` | Service-state wording; file stays queued; Retry offered and succeeds | PASS | `file-uploader.test.tsx` C4 **and** `use-file-upload.test.tsx` C4 — both drive Retry to a real `onUploaded` call. |
| C5 | Abort mid-upload | Request cancelled; row removed; no orphan in host state | PASS | `file-uploader.test.tsx` C5 and `use-file-upload.test.tsx` C5 — both release the gate *after* cancelling and assert `onUploaded` is never called. |
| C6 | Completion, failure and removal | Exactly one polite announcement each; no progress announcements | PASS | `file-uploader.test.tsx` C6 (rejection) and the adjacent "ADR-FE-153" test (progress never announced, completion is) — both watched pass. |
| C7 | Viewer opened from a thumb | Focus trapped; `Esc` restores focus to the thumb | PASS | `tests/components/files/media-viewer.test.tsx` C7 — opens, `Esc`, dialog gone, opener `toHaveFocus()`. |
| C8 | Image whose `urlExpiresOn` has passed | One re-fetch, then the plain message; no retry loop | PASS | `media-viewer.test.tsx` C8 (on `img` error) and `tests/components/files/file-thumb.test.tsx`'s two C8 cases (refresh succeeds; a **second** failure does not spend a second `onRefresh` call) — all watched pass. |
| C9 | axe on the uploader (idle, uploading, error) and the viewer | No violations | PASS | e2e, `tests/e2e/files-a11y.spec.ts` — 5 states × 4 widths × 2 projects = 40 executions, all passed in both Playwright runs (see Environment). |
| C10 | 320 px width | No horizontal scroll; control still operable | PASS | e2e, `tests/e2e/files-overflow.spec.ts` — checked at 360/768/1024/1440 (this repo's own UI-1 floor is 360, not 320 — same substitution `cat-responsive.spec.ts` already established), 5 states × 4 widths + 1 keyboard-operability check per width, both projects — all passed in both Playwright runs. |

## Integration / end-to-end cases (§16)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| E1 | Staff upload a product image and see it in the form | Visible before submit; survives a validation failure elsewhere in the form | PASS | `tests/e2e/files-upload.spec.ts` "C1/E1" — uploads via keyboard, asserts the file stays in `file-list-section` after an unrelated "Save" validation error. Passed on both projects, both runs (first-run failure was warm-up — see Environment). |
| E2 | Upload the EICAR file | Rejected message; the file is not listed | PASS | `files-upload.spec.ts` "E2" — reaches the harness's `error` state, asserts the scanner-refusal text is visible and the filename is never listed. Passed both projects, both runs. |
| E3 | Upload three files | Sequential; each shows its own progress; one failure does not cancel the others | PASS | `files-upload.spec.ts` "E3" — gates two of three uploads, confirms only one `progressbar` at a time, the rejected middle file never blocks the third, both survivors land in the persisted list. Passed both projects, both runs. Also unit-level: `use-file-upload.test.tsx` "E3/ADR-FE-150" (hook-level, same contract, gated fakes). |

## Regression cases from earlier gates

Findings from ARCHITECTURE's first pass (FAIL) and SECURITY's first pass (PASS with 2 MEDIUM/2 LOW),
fixed in the 2026-09-28 gate-fix round, per both gates' second-pass records.

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCHITECTURE (1st pass) | BLOCKER — `/dev/file-storage-harness` was reachable in production, forbidden by §14 | `notFound()` fires and renders nothing when `NODE_ENV=production`; the real harness renders otherwise; the route is a real 404 under a production build; the harness chunk is never fetched | PASS | `tests/unit/files/file-storage-harness-page.test.tsx` (both branches, watched pass in the full suite) **plus** this gate's own production check: `npm run build && npm run start`, `curl` → 404; `.next/app-build-manifest.json`'s route entry does not list the chunk containing `HarnessContent`'s content (confirmed by reading the built manifest directly, not inferred) — see Environment for the exact chunk-tracing steps. |
| R2 | ARCHITECTURE (1st pass) | MEDIUM — `lib/hooks/useFileUpload.ts` imported `fileRules`/`fileErrors` from `components/files/`, a `lib` → `components` inversion | `fileRules.ts`/`fileErrors.ts` live under `lib/files/`; nothing under `lib/` imports `@/components` | PASS | `grep -rn "@/components" web/lib/` returns only the doc-comment recording the historical fix, no actual import — checked directly this session. No dedicated automated test guards the import direction (see Gaps). |
| R3 | ARCHITECTURE (1st pass) | MEDIUM — an upload `401`/`403` bypassed HO-ACC-002's session-error surface | A `401` reaches `notifySessionError`/`sessionErrorHandler`; a CSRF `403` is a distinct `FileUploadForbiddenError`; a purpose-refusal `403` stays a validation error | PASS | `tests/unit/files/files-api-error-routing.test.ts` — 4 cases, all watched pass in the full suite. |
| R4 | ARCHITECTURE (1st pass) | MEDIUM — the `urlExpiresOn`-past half of ADR-FE-154's trigger was not built (only `onerror` was) | `<FileThumb>`/`<MediaViewer>` proactively call `onRefresh` for a file whose `urlExpiresOn` has already passed, without waiting for a load error | PASS | No committed test exercises this without also firing `onerror` first. I wrote and ran a temporary, non-committed test (`tests/components/files/_tmp-verify.test.tsx`) rendering both components with an already-past `urlExpiresOn` and **no** `fireEvent.error` — both called `onRefresh` exactly once, confirming the proactive branch fires on its own. Deleted after running; not part of the committed suite. Flagged in Gaps: this mechanism has no permanent regression test. |
| R5 | ARCHITECTURE (1st pass) | MEDIUM — ADR-FE-158's "never renders SVG" wasn't enforced by `isRenderableImageContentType()` | `image/svg+xml` is excluded explicitly, not only by the upload allow-list | PASS | No committed unit test targets `isRenderableImageContentType` directly (`file-rules.test.ts` doesn't call it). Temporary, non-committed test (`tests/unit/files/_tmp-verify.test.ts`) confirmed `isRenderableImageContentType('image/svg+xml') === false`, with PNG/JPEG/WebP still `true` and PDF/MP4 still `false`. Deleted after running. Flagged in Gaps: no permanent regression test. |
| R6 | ARCHITECTURE (1st pass) | LOW — a `413`/`502` lost its violation code, read as a generic network failure | Any status not already special-cased parses the `ValidationResponse` shape | PASS | `files-api-error-routing.test.ts` — 413/`FileTooLarge` and 502/`FileRejected` cases, both watched pass. |
| R7 | ARCHITECTURE (1st pass) | LOW — unrelated `package-lock.json` churn (`devOptional`→`dev`) | Reverted to baseline | PASS | `git diff 08e873e HEAD -- web/package-lock.json web/package.json` — empty, checked directly this session. |
| R8 | SECURITY (1st pass) | MEDIUM — the refresh-once guard reset whenever the host passed a new `url` for the same file, looping forever against a persistently-bad host re-fetch | Guard is keyed on `fileId` alone; a prop-only `url` change for the same `fileId` does not reset it | PASS | `tests/components/files/file-thumb.test.tsx` and `media-viewer.test.tsx`, both "gate-fix 2026-09-28 (SECURITY MEDIUM)" cases — `rerender()` with a new, still-failing URL for the same `fileId`; `onRefresh` still called exactly once. Both watched pass in the full suite. |
| R9 | SECURITY (1st pass) | LOW — an unrecognised server `errorCode` would be shown verbatim | Maps to `UnknownViolation` → a fixed, generic sentence | PASS | `files-api-error-routing.test.ts` "maps an unknown code to UnknownViolation" — watched pass. |
| R10 | SECURITY (1st pass) | LOW — `<iframe>`/`<img>`/download `<a>` carried no referrer policy | `referrerPolicy="no-referrer"` on all three (not `<video>`, which has no such attribute) | PASS | No committed test asserts the `referrerpolicy` attribute value. Temporary, non-committed test (`tests/components/files/_tmp-verify.test.tsx`) confirmed the attribute on `<FileThumb>`'s `<img>` and on `<MediaViewer>`'s `<img>` and PDF `<iframe>`. Deleted after running. Flagged in Gaps: no permanent regression test. |

## Acceptance-criteria coverage

| Criterion | Covered by | Status |
|---|---|---|
| A1 — One uploader exists; each consuming handover's own ACCEPTANCE verifies adoption (amended 2026-09-28) | §14 (no second HTTP client); C1–C10 all PASS. Owed per the owner decision: HO-EMR-002 §8.2 amendment (backlog #22) and the `PhotoField.tsx` conversion (backlog #12) | PASS (as amended; was BLOCKED against the original wording) |
| A2 — Fully operable by keyboard and screen reader | C1, C6, C7, C9 | PASS |
| A3 — Every server violation code produces an actionable sentence | C3, C4, R6, R9 | PASS |
| A4 — A scanner outage reads as a service state | C4 | PASS |
| A5 — Expired signed URLs recover once and then say so plainly | C8, R4 | PASS |
| A6 — Known cost measured, not assumed (full-size images on a 3G boarding-diary profile) | Owner decision 2026-09-28 (§1 Amended): deferred to the Phase 6 performance pass, triggered by the first `<FileThumb>` list screen | DEFERRED (owner) — nothing measured yet; was BLOCKED before the decision |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| A1 (full) | No consuming handover (HO-SHP-002/EMR-002/BRD-002/RPT-002) exists yet, and `PhotoField.tsx` is a known second control | HO-SHP-002 (or whichever consumer lands first) actually importing `<FileUploader>`, and the Cat Profiles photo page conversion (flagged, not owned by this slice — HO-FIL-002 §1's own "not done here" row) |
| A6 | No Phase 6 performance note in this worktree; no real device/3G profile was measured | The owner's own Phase 6 performance-measurement pass, against a real consuming diary screen |
| PDF refresh-once (ADR-FE-154 gap) | `<iframe>`-embedded PDFs fire no observable `onerror`; HO-FIL-002 §1 and ARCHITECTURE's carried-forward MEDIUM both record this as a known, undone gap, not something this gate can pass or fail | HO-EMR-002 (the first consumer to pass a PDF through `<MediaViewer>`) picking up the backlog item |
| Real backend (HO-FIL-001) integration | No real API host, ClamAV, or GCS bucket was started this session; every case ran against MSW fixtures only, per this repo's established pattern for web-only gates | A real API host + ClamAV + GCS/ADC, as HO-FIL-001's own backlog #10 already records as outstanding |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| No permanent regression test for the proactive `urlExpiresOn` trigger (R4) | This gate wrote and ran a temporary test to confirm the mechanism is real, then deleted it (out of scope to add new committed product-adjacent tests during a TEST gate audit) | Whoever next touches `FileThumb.tsx`/`MediaViewer.tsx` — add a permanent case asserting `onRefresh` fires from an already-past `urlExpiresOn` with **no** `fireEvent.error` |
| No permanent regression test for SVG exclusion (R5) | Same reason — confirmed with a temporary test, not committed | Add a case to `tests/unit/files/file-rules.test.ts` calling `isRenderableImageContentType('image/svg+xml')` directly |
| No permanent regression test for `referrerPolicy="no-referrer"` (R10) | Same reason | Add an attribute assertion to the existing `file-thumb.test.tsx`/`media-viewer.test.tsx` render cases |
| No test guards the `lib` → `components` import direction (R2) | Structural convention, not runtime behaviour; would need an ESLint rule or a lint-time import-graph check, not a Vitest case | An ESLint `no-restricted-imports`/dependency-cruiser rule, analogous to the existing `no-restricted-imports` rule for `useQuery`/`useMutation` |
| CODE_REVIEW's four open findings (Retry button's missing per-file `aria-label`; `multiple={false}` not enforced against drag-and-drop; `MediaViewer`'s refresh effects not gated on `open`; `retryAfterSeconds` parsed but unused) | MEDIUM/LOW, not BLOCKER/HIGH — owner rule (2026-09-27) does not require a fix before this gate; recorded in `docs/backlog/FIL.md` rows 15–18, not re-litigated here | Whichever batch next works `docs/backlog/FIL.md` |
| ARCHITECTURE/SECURITY's three carried-forward MEDIUM/LOW items (PDF refresh-once gap; `PhotoField.tsx` second control; harness deletion owed to the first real consumer; refresh-once guard's component-instance scoping) | Same — `docs/backlog/FIL.md` rows 11, 12, 13, 14 | Same |
