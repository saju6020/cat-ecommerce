# TC-HO-NTF-002 — Notifications (frontend)

Handover: docs/handover/HO-NTF-002.md
Commit(s) under test: code head a50a2c7 (branch `ho/NTF-002`, master base `cffaa79`)
Written: 2026-10-04 (developer; re-audited by the TEST gate)  ·  Executed: 2026-10-04 (TEST gate)

> Statuses record what was **observed** by the TEST gate, not inferred and not carried over from the
> developer's run. `BLOCKED` marks a case that could not be executed. Every case runs against **MSW**
> (`NEXT_PUBLIC_USE_MOCKS=true`). The real-API column of §16 is "not run — no API" (no database or API
> process in this worktree); it is recorded per row in the E-series table.
> The unit/E2E IDs are the handover's own (§15, §16). The gate-added rows (V-*, R-*, M-*) are new.

## Summary

| | Count |
|---|---|
| Total rows | 49 (30 unit + 4 gate-added + 10 e2e + 5 regression/master) |
| PASS | 49 (MSW; each e2e row passed on chromium and mobile-360) |
| FAIL | 0 |
| BLOCKED | 0 |
| NOT RUN | 0 |
| Real-API column | 10 cells "not run — no API" (not counted above; see below) |

Test-count totals (the numbers behind the rows): vitest 109 files / 505 tests; notification Playwright
15 + 61 per project, both projects; 392 master-existing chromium Playwright tests, 391 pass, 1 fails
identically on master.

## Environment

- Worktree `D:/my-repo/cat_ecommerce-wt/NTF-002`, code head `a50a2c7`; gate head `981f2fa`. Windows 11.
- `npx tsc --noEmit`: exit 0. `npm run lint` (`--max-warnings 0`): exit 0.
- `npx vitest run`: first run 6 files / 8 tests FAILED on 5000 ms timeouts under load (one seen:
  `tests/unit/files/file-storage-harness-page.test.tsx`, outside this slice); immediate rerun **109 files /
  505 tests, all passed**. The 13 notification-related files re-run verbose: 102/102 passed.
- Playwright: `PORT=3103`, `--workers=1`, one fresh dev server per invocation, MSW (`NEXT_PUBLIC_USE_MOCKS=true`).
  notifications inbox/unsubscribe/templates/log: chromium 15/15, mobile-360 15/15.
  notifications-a11y-responsive (alone): chromium 61/61 (4.0 m), mobile-360 61/61 (4.3 m).
  Gate-added temp spec (V-C2, V-C5, V-A2), chromium: 2/2 after correcting two mistakes in my own spec; deleted.
  Master-existing specs on chromium (all 29 non-notification spec files, `real-api` excluded): 392 tests,
  380 passed, 12 failed in the 36 m run; `--last-failed` rerun on a fresh server: 11 passed, 1 failed
  (`cat-vaccinations` E9), which fails identically on master `cffaa79` in the main checkout.
  The 11 first-run failures were `toBeVisible`/focus timeouts late in a 36-minute single-server run
  (admin-a11y-responsive E-X2 x5, admin-filters x2, admin-functional E-B1, cat-profile-header E4,
  staff-shell x2); all passed on the rerun, treated as cold-compile/load timeouts.
- No backend, database or API process was available; no real-API run was attempted.
- Port 3103 free at the end (netstat); no node process left.

## Unit / component cases (§15)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | InboxBadge 0 / 3 / 150 | Hidden count; "3" with "Inbox, 3 unread messages"; "99+" | PASS | vitest verbose PASS (inbox-badge.test.tsx) |
| U-A2 | Badge polling 60 s + focus; none while hidden | as stated | PASS | vitest verbose PASS; plus real browser (V-A2): requests 2 -> 3 after 60 s, 0 while hidden over 5 min, resumed on visible. |
| U-A3 | InboxItem open: POST read once, badge invalidated | as stated | PASS | vitest verbose PASS (inbox-item.test.tsx) |
| U-A4 | Body `<b>hi</b>` and `**x**` literal | plain text | PASS | vitest verbose PASS (inbox-item.test.tsx) |
| U-A5 | linkPath safe/unsafe (4 forms) | link / none / none / none | PASS | vitest verbose PASS (inbox-item + is-safe-internal-path tests) |
| U-B1 | PreferredChannelField both identifiers; mobile only | email disabled with hint | PASS | vitest verbose PASS (preferred-channel.test.tsx) |
| U-B2 | Channel + matrix cell, one PUT on Save, nothing before | one PUT | PASS | vitest verbose PASS (preferred-channel.test.tsx) |
| U-B3 | PreferredChannelHasNoDestination | error on field | PASS | vitest verbose PASS (preferred-channel.test.tsx) |
| U-C1 | VariablePalette insert at caret | `{{catName}}` at caret, caret after | PASS | vitest verbose PASS (template-editor.test.tsx) |
| U-C2 | Preview debounce 5 keystrokes; in-flight aborted | one request after 600 ms; earlier aborted | PASS | vitest verbose PASS; real browser (V-C2) confirms. Assertion `signals.some(aborted)` is loose (LOW, CODE_REVIEW already noted); V-C2 checks the specific request. |
| U-C3 | Preview 400 UnknownTemplatePlaceholder | server message in panel + field; typing not blocked | PASS | vitest verbose PASS (template-editor.test.tsx) |
| U-C4 | SMS Unicode 2 segments | "2 SMS · Unicode" and note | PASS | vitest verbose PASS (template-editor.test.tsx) |
| U-C5 | Email preview with script: sandbox="" and no script runs | no global set | PASS | vitest verbose PASS but WEAK: jsdom never runs srcdoc scripts, so the canary check is decoration. Real behaviour proven by V-C5 and the e2e "script never runs" test. |
| U-C6 | srcDoc lint rule fails npm run lint | lint exits non-zero | PASS | vitest verbose PASS (3 lint cases, ESLint run programmatically); the real `npm run lint` failure is V-C6. |
| U-C7 | Save 409: conflict message + Reload, values kept | as stated | PASS | vitest verbose PASS (template-editor.test.tsx) |
| U-C7-R | CODE_REVIEW HIGH regression: failed refetch/Reload keeps editor | editor + typed value kept; Reload disabled while pending | PASS | vitest verbose PASS (template-editor-page.test.tsx) |
| U-C8 | Secret template notice | notice shown | PASS | vitest verbose PASS (template-editor.test.tsx) |
| U-C9 | Save error on email subject while SMS tab active | email tab activated, field focused | PASS | vitest verbose PASS (template-editor.test.tsx) |
| U-D1 | Unsubscribe with #token: no request on load, hash removed | as stated | PASS | vitest verbose PASS (unsubscribe.test.tsx) |
| U-D2 | Unsubscribe click: 200 true/false/400/429 | four messages | PASS | vitest verbose PASS (unsubscribe.test.tsx) |
| U-D3 | No token | invalid-link state, no button | PASS | vitest verbose PASS (unsubscribe.test.tsx) |
| U-E1 | QueueSummary SMS unconfigured | banner role=status | PASS | vitest verbose PASS (queue-summary.test.tsx) |
| U-E2 | Log filters URL round-trip; >93 days blocked | as stated | PASS | vitest verbose PASS (log-filters.test.tsx) |
| U-E3 | Log row statuses as text; held row in branch zone | as stated | PASS | vitest verbose PASS (log-table.test.tsx) |
| U-E4 | Redacted reset detail | content-removed sentence + resend refusal | PASS | vitest verbose PASS (notification-detail.test.tsx) |
| U-E5 | Detail body markup / provider response script as text | plain text | PASS | vitest verbose PASS (notification-detail.test.tsx) |
| U-E6 | ResendPanel confirm and 201 held | channel + destination; held sentence + link | PASS | vitest verbose PASS (notification-detail.test.tsx) |
| U-E7 | ResendLimitReached | limit sentence | PASS | vitest verbose PASS (notification-detail.test.tsx) |
| U-X1 | Server 400 mapped to field; no errorCode in DOM | as stated | PASS | vitest verbose PASS (several files) |
| U-X2 | Dirty + navigate unsaved-changes confirm | confirm | PASS | vitest verbose PASS (preferred-channel, template-editor) |

## Gate-added verification cases (named by the ARCHITECTURE gate for TEST)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| V-C6 | Temporary violating file under web/ using `srcDoc`; run `npm run lint`; delete the file | Lint exits non-zero naming the file; file gone afterwards | PASS | Temp `web/components/zz-vc6-violation.tsx` (`<iframe srcDoc=…>`): `npm run lint` printed the error and exited 1. Second temp file under `web/tests/` with JSX, object-property and `.srcdoc =` forms: 3 errors, exit non-zero. Both files deleted; `ls` and `git status` confirm gone. |
| V-C5 | In a real browser, sandboxed email preview with `<script>`/onerror/inline handler | No script runs; frame `sandbox` is `""` | PASS | Temp Playwright spec, chromium: `sandbox` attr is `""`; `iframe.contentDocument` access blocked (cross-origin); `window.__previewScriptRan` stayed unset after the mock's `<script>` canary frame loaded. Spec deleted. |
| V-A2 | In a real browser (and the unit test), polling stops while the tab is hidden and resumes when visible | No unread-count requests while hidden | PASS | Temp Playwright spec with `page.clock`, chromium: unread-count requests 2 -> 3 after 61 s; 3 -> 3 after 5 min while `visibilityState` hidden; 4 on becoming visible; 5 after another 61 s. (First attempt used a non-bubbling event and failed in the spec, not the product; corrected.) Spec deleted. |
| V-C2 | In a real browser, typing quickly in the editor | One preview request after the debounce; earlier in-flight aborted | PASS | Temp Playwright spec, chromium, preview POST delayed 1200 ms: 5 keystrokes at ~50 ms -> 0 new requests inside the window, then exactly 1; a further keystroke while it was in flight produced a second request and the first signal reported aborted, the second not. (First attempt had my own timing error; corrected.) Spec deleted. |

## End-to-end cases (§16), MSW

| ID | Case | Spec | chromium | mobile-360 | Real API |
|---|---|---|---|---|---|
| E-A1 | Inbox shows reminder with View link; badge 1 | notifications-inbox.spec.ts | PASS (short specs 15/15) | PASS (15/15) | not run — no API |
| E-A2 | Open item clears badge | notifications-inbox.spec.ts | PASS (short specs 15/15) | PASS (15/15) | not run — no API |
| E-B1 | Preferred channel email, save, reload shows email | notifications-inbox.spec.ts | PASS (short specs 15/15) | PASS (15/15) | not run — no API |
| E-C1 | Admin edits template with {{catname}}: preview and save show named error | notifications-templates.spec.ts | PASS (short specs 15/15) | PASS (15/15) | not run — no API |
| E-C2 | Fix, save, reset to default: Customised -> Default | notifications-templates.spec.ts | PASS (short specs 15/15) | PASS (15/15) | not run — no API |
| E-D1 | Unsubscribe from link (MSW only): matrix shows email marketing off | notifications-unsubscribe.spec.ts | PASS (short specs 15/15) | PASS (15/15) | not run — no API |
| E-E1 | Log: filter by customer, open failed SMS, resend by email, chain link | notifications-log.spec.ts | PASS (short specs 15/15) | PASS (15/15) | not run — no API |
| E-E2 | Nurse -> PermissionDeniedState | notifications-log.spec.ts | PASS (short specs 15/15) | PASS (15/15) | not run — no API |
| E-X1 | axe every route, clean and with errors, editor preview open | notifications-a11y-responsive.spec.ts | PASS (61/61) | PASS (61/61) | not run — no API |
| E-X2 | Every route at 360/768/1024/1440, no h-scroll | notifications-a11y-responsive.spec.ts | PASS (61/61) | PASS (61/61) | not run — no API |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | CODE_REVIEW | HIGH: failed background refetch replaced the editor with ErrorState | U-C7-R (above) | PASS | vitest verbose: failed background refetch and failed Reload tests passed. |
| R2 | CODE_REVIEW | LOW: Reload had no pending state | U-C7-R, Reload disabled while pending | PASS | "Reload is disabled whi…" test passed in the verbose run. |
| R3 | SECURITY | srcDoc only inside SandboxedEmailPreview | V-C6 | PASS | see V-C6 |
| R4 | SECURITY | sandbox="" blocks script | V-C5 | PASS | see V-C5 |
| M1 | master regression | Specs that existed on master (admin, account, cat, staff, files, home) on chromium, after changes to adminNavItems, portal-header, NotificationPreferenceMatrix | see Environment | PASS | 392 chromium tests of 29 non-notification spec files: 380 passed first run, 12 failed (11 cold/load timeouts, passed on `--last-failed` rerun); 1 (`cat-vaccinations` E9) failed again and fails identically on master `cffaa79` (fixture/date, outside this diff). |

## Acceptance-criteria coverage (§18)

| Criterion | Covered by | Status |
|---|---|---|
| A1 | U-A1..A3, E-A1, E-A2 | PASS (MSW) |
| A2 | U-B1..B3, E-B1 | PASS (MSW) |
| A3 | U-C1, U-C2, U-C4, U-C5, E-C2 | PASS (MSW) |
| A4 | U-C3, E-C1 | PASS (MSW) |
| A5 | U-D1..D3, E-D1 | PASS (MSW) |
| A6 | U-E2, U-E3, E-E1 | PASS (MSW) |
| A7 | U-E4, U-E6, U-E7, E-E1 | PASS (MSW) |
| A8 | U-E1 | PASS (MSW) |
| A9 | U-A4, U-A5, U-C5, U-C6, U-E5 | PASS (MSW) |
| A10 | E-X1, E-X2 | PASS (MSW) |

## Cases I could not run

Every "Real API" cell above: not run — no API. Unblocked by a running HO-NTF-001 Api with Development
senders and the worker (HO-NTF-002 §16). E-D1 is MSW-only by §16's own wording.

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| Real-API column of E-A1..E-X2 | no API in worktree | integrator, after HO-NTF-001 merge |
| U-C5 (jsdom) is decoration; real-browser proof exists only as the e2e canary test | jsdom does not run `srcdoc` scripts | backlog: keep the V-C5 check as a permanent spec |
| V-A2 (hidden-tab polling) and V-C2 (abort of the specific in-flight request) have no permanent browser test | temp specs only; unit tests cover them | backlog |
| U-C2's `signals.some(aborted)` is loose (CODE_REVIEW LOW) | not changed: gate does not weaken or rewrite tests | developer |
| mobile-360 not run for the master-existing specs | task asked chromium; time | integrator if wanted |
