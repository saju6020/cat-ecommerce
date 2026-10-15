GATE: ARCHITECTURE + SECURITY + CODE_REVIEW (combined follow-up review, gate-scope rule)
HANDOVER: HO-ADM-002-FU1
COMMITS: 271076c7 964057c0 3cbdcdc0 ab0f5d02 d3c06322 a87ee9e1 b17f7933 2653cfe8 f3e5fba0 (master 9eb87445..HEAD)
DATE: 2026-10-09
STATUS: PASS

FINDINGS
- [MEDIUM] docs/backlog/ADM.md FU1-1 — E-CAL8 (real-API run) not executed; A2/A10 and the real half of E-B1 not evidenced against the real API.
- [LOW] web/app/(staff)/staff/admin/branches/[branchId]/calendar/page.tsx — LoadingState replaces nav and grid on every month change (FU1-2).
- [LOW] calendar/page.tsx — restoreFocusDay not cleared if the next month fails to load (FU1-3).
- [LOW] web/lib/format/branch-calendar.ts shiftMonth — out-of-range months fall back silently (FU1-4).
- [LOW] web/components/branches/BranchCalendarMonthNav.tsx — STATUS_VARIANT exported from a component and imported by siblings (FU1-5).
- [LOW] full vitest run flaky under load in unrelated inventory tests (FU1-6).

CHECKS
- ADR-FE-170: open/closed state and times come only from GET .../calendar; closures and weeklyHours only label reasons; calendar failure draws no grid. OK.
- Layering: hooks only in lib/hooks and the page; components presentational; pure model; no new package, no fetch outside client, no dangerouslySetInnerHTML; closure reasons rendered as text. OK.
- Security: month param validated by regex and range; 403 from any of three reads shows AccessRefusedState. OK.
- A11y: table role=grid, aria-readonly, th scope/abbr, roving tabindex (one Tab stop), APG keys, status in words, axe zero serious/critical.
- Mock parity (section 12.2): unknown branch 200 [], 62-day 400, zoned instants, part-day subtraction, merge, closures from/to filter, seed moved to 2027-02-15. OK.
- Section 18 A1-A12: evidenced by MSW runs, except real-API halves of A2/A10 (FU1-1).
- E-B1 calendar half: PASS under MSW (admin-functional.spec.ts E-B1).
- Open owner questions (not failed): OQ-1 optional backend days query; OQ-2 special hours that extend opening; OQ-3 week start day; OQ-4 standalone acceptance pass.

RESULTS
- npx tsc --noEmit: clean. npm run lint: clean. npm run build: OK.
- npx vitest run (full): 1107 passed, 2 failed, 7 worker crashes (exit 134) under load, all in unrelated files; re-run of those files: 24/24 passed; tests/components/branches (slice unit/component tests) all passed.
- Playwright chromium, mocks, PORT 3114, workers=1: admin-branch-calendar E-CAL2,3,4,6(x2),7 + admin-functional E-B1: 7 passed, 1 skipped (E-CAL8 opt-in). admin-a11y-responsive calendar route (E-X1 axe, E-X2 at 360/768/1024/1440): 5 passed. Dev server stopped.

SCOPE CHECK
- files in allowed scope: 22/22
- files outside allowed scope: none
