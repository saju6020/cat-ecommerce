GATE: TEST
HANDOVER: HO-FIL-002
COMMITS: fabdc6b..211661f (branch ho/FIL-002; head 211661f, includes the 2026-09-28 gate-fix round
and ARCHITECTURE/SECURITY/CODE_REVIEW second-pass records)
DATE: 2026-09-28
STATUS: PASS

Full case sheet: docs/test-cases/TC-HO-FIL-002.md — 29 rows, 27 PASS, 0 FAIL, 2 BLOCKED, 0 NOT RUN.

BUILD GATES
- `npx tsc --noEmit` — clean, 0 errors.
- `npm run lint` (`eslint . --max-warnings 0`) — clean, 0 warnings.
- `npx vitest run` (full repo suite, not filtered to this slice) — 69 files, 310 tests, all passed.

E2E (Playwright, this slice only)
- `PORT=3103 npx playwright test tests/e2e/files-a11y.spec.ts tests/e2e/files-overflow.spec.ts
  tests/e2e/files-upload.spec.ts --project=chromium --project=mobile-360 --workers=1`.
- First run: 100 tests, 98 passed, 2 failed (both the same case, `C1/E1`, one per project — a
  `page.waitForEvent('filechooser')` timeout on the first spec to hit a cold `/dev/file-storage-harness`
  compile).
- Second run, immediately after, same command: 100/100 passed, including both previously-failing
  cases. Per the brief's own instruction, this is recorded as warm-up/environment, not a product
  defect — both runs are in the case sheet's Environment section, not just the clean one.
- Ports 3103 confirmed free (no LISTENING socket) after both runs.

PRODUCTION-BUILD CHECK (requested by ARCHITECTURE)
- `npm run build` — clean, 0 type errors. `/dev/file-storage-harness` appears in the route table at
  346 B / 103 kB First Load JS.
- `PORT=3104 npm run start`, then `curl`:
  - `GET /dev/file-storage-harness` → HTTP 404.
  - `GET /sign-in` (control) → HTTP 200.
- `.next/app-build-manifest.json`'s own entry for `/dev/file-storage-harness/page` lists five files;
  none of them is the chunk containing `HarnessContent`'s content (traced by content-grepping every
  `.next/static/chunks/*.js` for `__fileStorageHarness`/`__fileUploadGates`, then cross-checking the
  one matching chunk's id against the route's declared file list). The page's own compiled client
  bundle (`page-5a196904394e9996.js`) contains only `function(){return notFound(),null}` — the
  `React.lazy(() => import('./HarnessContent'))` branch and its JSX were eliminated by the production
  build entirely (not merely never invoked at runtime), because `NODE_ENV === 'production'` is
  statically true at build time and the code after `notFound()` (which always throws) is unreachable.
  This is a stronger guarantee than SECURITY's first-pass note ("a lazily split, never-loaded chunk is
  acceptable") anticipated.
- Server stopped afterwards; port 3104 confirmed free (no LISTENING socket).

FINDINGS
- [LOW] Three of the gate-fix round's own fixes have no permanent, committed regression test:
  the proactive `urlExpiresOn`-past refresh trigger (`FileThumb.tsx`/`MediaViewer.tsx`, ARCHITECTURE
  1st-pass MEDIUM), `isRenderableImageContentType()` excluding `image/svg+xml` (same gate, ADR-FE-158),
  and `referrerPolicy="no-referrer"` on the `<img>`/`<iframe>`/download `<a>` (SECURITY 1st-pass LOW).
  All three are genuinely implemented — confirmed this session with three temporary, non-committed
  Vitest cases written and run against the real components/module, then deleted — but nothing in the
  committed suite would catch a regression on any of them. Not a production defect; a test-suite gap.
  Backlog.
- [LOW] The `lib` → `components` import-direction fix (ARCHITECTURE 1st-pass MEDIUM) has no automated
  guard at all — confirmed by direct `grep` that no `@/components` import exists under `web/lib/`,
  but nothing (lint rule or test) would catch a future regression the way the existing
  `no-restricted-imports` rule guards `useQuery`/`useMutation`. Backlog.
- CODE_REVIEW's four MEDIUM/LOW findings (Retry button's missing per-file `aria-label`;
  `multiple={false}` not enforced against drag-and-drop; `MediaViewer`'s refresh effects not gated on
  `open`; `retryAfterSeconds` parsed but unused) are unchanged since that gate — this gate did not
  re-litigate them, only confirmed none is BLOCKER/HIGH. Carried to `docs/backlog/FIL.md` rows 15-18.
- ARCHITECTURE/SECURITY's own carried-forward MEDIUM/LOW items (PDF `<iframe>` refresh-once gap;
  `PhotoField.tsx` second upload control; harness deletion owed to the first real consumer; the
  refresh-once guard's component-instance scoping) are unchanged. `docs/backlog/FIL.md` rows 11-14.

Owner rule (2026-09-27): only BLOCKER/HIGH findings block a gate. Everything above is MEDIUM/LOW and
is recorded for `docs/backlog/FIL.md`, which this gate has not edited.

PROCESS NOTE
- This gate did not cleanly follow "write the sheet, then execute": the task brief named the exact
  verification commands up front, and they were run, and every existing test file was read, before
  the case sheet was drafted. The case *enumeration* is not results-fitted (every ID is either
  HO-FIL-002's own §15/§16 numbering or an already-existing test's own literal name), but the
  sequencing itself was wrong for this role and is disclosed in the case sheet's own header rather
  than presented as if every row started `NOT RUN`. See TC-HO-FIL-002.md's header note.

SCOPE CHECK
- This gate wrote/updated only `docs/test-cases/TC-HO-FIL-002.md` and this file. No product code was
  edited. Two temporary verification test files were written, run, and deleted before this record was
  written (`tests/unit/files/_tmp-verify.test.ts`, `tests/components/files/_tmp-verify.test.tsx`) —
  `git status` is clean of them.

VERDICT: PASS. 0 BLOCKER, 0 HIGH. All of C1-C10/E1-E3 (HO-FIL-002 §19's own TEST scope) are PASS, on
real, watched executions, with the one warm-up flake re-run and reported honestly per the brief's own
instruction. The production-build check ARCHITECTURE requested is clean, and goes further than that
gate's own prediction (the harness chunk isn't merely unfetched — it's absent from the route's
compiled chunk list, and the dead branch is eliminated at build time). The remaining findings are
test-suite coverage gaps and previously-recorded MEDIUM/LOW items, not BLOCKER/HIGH defects.
