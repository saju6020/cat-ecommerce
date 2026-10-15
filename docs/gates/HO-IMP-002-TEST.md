GATE: TEST
HANDOVER: HO-IMP-002
COMMITS: a301affb (head under test) plus the sheet commits on ho/IMP-002
DATE: 2026-10-09
STATUS: PASS

RESULTS (sheet: docs/test-cases/TC-HO-IMP-002.md, section "TEST gate run")
- tsc --noEmit: clean. eslint --max-warnings 0: clean. next build: exit 0.
- vitest --maxWorkers=2: 162 files, 955 tests, 955 passed. All 46 handover U-cases (A1-A6, B1-B8, C1-C6, D1-D6, E1-E7, F1-F8, X1-X5) mapped to passing tests.
- Playwright chromium on MSW (PORT 3111, --workers=1): 245 cases. Run 1: 227 pass, 18 fail (all 5 s cold-compile waits). --last-failed run 2: 15 pass, 3 fail. --last-failed run 3: 3 pass. Every E-case (A1..G1, X1 x46 routes, X2 x46 routes x4 widths, X3) passed in some run; no assertion about product behaviour failed.
- Real API (API :5008 from this worktree on catecommerce_dev, no migration; web :3111 mocks off): 11 pass, 1 skipped. R-D2 NOT RUN: stale awaiting-approval count CNT-2026-000001, no second approver. Seeding a second administrator was refused by the permission system in this session and not authorised by the user, so nothing was added to the database.
- Servers (next dev, API) stopped after each run; web/.env.local restored.

FINDINGS
- [LOW] web/tests/e2e/inventory-*.spec.ts — cold-compile 5 s timeouts under load (backlog 59).
- [LOW] R-D2 (blind count, variance, commit) has no real-API result; mock coverage E-D2 and U-E5/U-E6 pass (existing backlog 58).
- [LOW] E-X1 "each dialog open" axe is covered through the shared dialog component, not per dialog (noted in the sheet).

SCOPE CHECK
- files in allowed scope: tests/docs only touched by this gate (docs/test-cases, docs/gates, docs/backlog)
- files outside allowed scope: none

ADDENDUM 2026-10-09 (R-D2 real-API run; owner approved one dev-only second administrator)
- Created in catecommerce_dev: e2e.approver@purrcare.test (Administrator, main branch), via the staff-account API; password credential copied from e2e.admin by a throwaway SQL update (password-reset link unusable in dev). No migration.
- inventory-real-api.spec.ts, chromium, workers=1, API :5008, web :3111 mocks off: 12 passed (1.8 min). R-D2 PASS: stale CNT-2026-000001 approved as the second administrator (Committed), then blind count CNT-2026-000002 committed.
- Backlog 58 closed. Verdict stays PASS. No product bug found.
- Servers stopped, web/.env.local restored to mocks-on, scratch folder deleted.
