GATE: TEST
HANDOVER: HO-NTF-002
COMMITS: a50a2c7 (code head under test; range cffaa79..a50a2c7), gate records up to 981f2fa
DATE: 2026-10-04
STATUS: PASS

First pass. Sheet: `docs/test-cases/TC-HO-NTF-002.md`. The developer's sheet was rewritten with every row
`NOT RUN` before anything was executed, then each row was set from what this gate watched. 49 rows: 49
PASS (MSW), 0 FAIL, 0 BLOCKED, 0 NOT RUN. The real-API column of E-A1..E-X2 is "not run — no API" (10
cells, no backend in the worktree), as instructed.

Exact results (all in `web/`; Playwright with `PORT=3103`, `--workers=1`, one fresh dev server per invocation, MSW):
- `npx tsc --noEmit`: exit 0.
- `npm run lint` (`--max-warnings 0`): exit 0.
- `npx vitest run`: first run **6 files / 8 tests failed** on 5000 ms timeouts under load (one seen:
  `tests/unit/files/file-storage-harness-page.test.tsx`, not in this slice). Immediate rerun, unchanged code:
  **109 files / 505 tests, all passed**. The 13 notification-related files re-run with `--reporter=verbose`:
  102/102, and every U-ID in §15 maps to a named passing test.
- Notifications e2e (inbox, unsubscribe, templates, log): **chromium 15/15, mobile-360 15/15**.
- `notifications-a11y-responsive.spec.ts`, alone per project: **chromium 61/61 (4.0 m), mobile-360 61/61 (4.3 m)**,
  first attempt, no cold timeout.
- Master-existing e2e on chromium (all 29 non-notification spec files incl. admin, account, FIL, cat, staff; `real-api`
  excluded; 392 tests in one 36-minute run): **380 passed, 12 failed**. Rerun of exactly the 12 with
  `--last-failed` on a fresh server: **11 passed, 1 failed**. Original and rerun results:
  - admin-a11y-responsive E-X2 (branches/:id at 768 and 1024, branches/:id/hours at 1024, content/:id at 360,
    system at 768), admin-filters (audit-log typing, content Back twice), admin-functional E-B1,
    cat-profile-header E4, staff-shell focus-return, staff-shell-toggle: failed late in the long run on
    `toBeVisible`/`locator.focus` timeouts; all passed on the warm rerun. Treated as cold-compile/load timeouts.
  - `cat-vaccinations.spec.ts` E9 ("Due soon" not visible for Biscuit): failed in the run and on rerun. I ran
    `cat-vaccinations.spec.ts` on master `cffaa79` in the main checkout (`D:/my-repo/cat_ecommerce/web`, same port, worktree
    server stopped): E9 fails identically (4 passed, 1 failed). This branch changes nothing under the cat area
    (`mocks/handlers/areas/cat-profiles.ts` not in the diff); the fixture derives dates from the current date.
    Pre-existing, not a regression. The main checkout `git status` is clean afterwards.
  - `account-area.spec.ts` passed in full in this run (the ADM-002 pending-policy failures are not present on this base).
- The `adminNavItems`, portal header and `NotificationPreferenceMatrix` changes show no regression in the
  account, admin, staff, cat and files specs above. mobile-360 was not run for the master-existing specs.

Specific verifications left by the ARCHITECTURE gate:
- **U-C6 PASS (V-C6).** A temporary `web/components/zz-vc6-violation.tsx` using `<iframe srcDoc=…>` made
  `npm run lint` print `srcDoc is allowed only in components/notifications/SandboxedEmailPreview.tsx` and exit 1.
  A second temporary file under `web/tests/` with the JSX, object-property (`{ srcDoc: … }`) and `el.srcdoc = …`
  forms produced 3 errors. Both files deleted; `ls` shows them gone and `git status` shows only the two expected
  files before commit. The `ignores` path resolves from `web/` (the real component lints clean).
- **U-C5 PASS (V-C5), with a weak unit test.** In real chromium the preview frame has `sandbox=""`, its
  `contentDocument` is inaccessible, and the mock's `<script>window.parent.__previewScriptRan = true</script>` never
  set the flag. The vitest U-C5 test cannot fail for that reason: jsdom does not execute `srcdoc` scripts, so its
  "flag undefined" assertion is decoration; the attribute assertions in it are real. The e2e "script never runs" test
  and my temporary spec carry the behavioural proof.
- **U-A2 PASS (V-A2).** Real chromium with `page.clock`: unread-count requests went 2 -> 3 after 61 s; no further
  request over 5 minutes while `document.visibilityState` was `hidden`; one on becoming visible; one more after another
  61 s. The unit test (fake timers, hidden for 5 min, count stays at 3) also passes and would fail if
  `refetchIntervalInBackground` were true.
- **U-C2 PASS (V-C2).** Real chromium, preview response delayed 1200 ms: five keystrokes at 50 ms produced no request
  inside the window and exactly one after it; a keystroke while that one was in flight produced a second request and the
  first request's signal aborted, the second's did not.
The two V-C2/V-A2 temporary specs each failed once on my own mistakes (a timing overlap; a non-bubbling synthetic
event) and were corrected, not the product; temporary spec file deleted (`tests/e2e/zz-tmp-gate.spec.ts` absent).

Cleanup: every dev server stopped; `netstat` shows nothing on 3103 and no node process remains. `web/.env.local`
untouched. `web/public/mockServiceWorker.js` left modified and uncommitted, as instructed. No product code changed.

FINDINGS
- [MEDIUM] web/tests/components/templates/sandboxed-email-preview.test.tsx:18-24 (U-C5) — the "no script runs" assertion
  cannot fail in jsdom (it never executes `srcdoc`); only the attribute assertions are live. Behaviour is proven by the
  e2e test and V-C5, so nothing is unprotected, but the unit comment overstates it. Backlog.
- [LOW] web/tests/components/templates/template-editor.test.tsx:107-150 (U-C2) — `signals.some((s) => s.aborted)` is loose
  (already raised by CODE_REVIEW); V-C2 shows the real abort works. Backlog: assert on the specific first signal.
- [LOW] No permanent browser test for hidden-tab polling (V-A2) or for the preview abort (V-C2); both rest on unit tests
  plus this gate's temporary specs. Backlog: promote them to `notifications-*.spec.ts`.
- [LOW, environment] 11 admin/cat/staff-shell e2e tests time out late in a 36-minute single-server run and pass on
  rerun (the backlog "row 1" pattern); not caused by this branch. vitest also timed out 8 tests once under load.
- [LOW, pre-existing, not this branch] `cat-vaccinations.spec.ts` E9 fails on master `cffaa79` too (date-derived
  fixture). Belongs in the CAT backlog.
- No BLOCKER or HIGH findings. Production bugs found: none.

SCOPE CHECK
- files in allowed scope: 2/2 (`docs/test-cases/TC-HO-NTF-002.md`, `docs/gates/HO-NTF-002-TEST.md`)
- files outside allowed scope: none (temporary files removed before commit; `web/public/mockServiceWorker.js` left uncommitted)
