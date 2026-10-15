GATE: TEST
HANDOVER: HO-POR-002
COMMITS: ff31a745 (branch ho/POR-002, b12fd39d..ff31a745) plus the gate commit that adds this file
DATE: 2026-10-13
STATUS: PASS

Scope: portal home dashboard and re-order through Orders' endpoint, narrowed by HO-POR-002 section 1 Amended (stays, subscriptions, diary out). Sheet: docs/test-cases/TC-HO-POR-002.md (section "TEST gate run"). Test code and docs only; no production code changed (mutations used for proof were restored, tree clean).

RESULTS
- tsc clean. Lint (--max-warnings 0) clean. next build exit 0.
- Vitest `--maxWorkers=2`: 193 files, 1272 tests, 0 failed. Portal folder: 47 of 47 (dashboard 23, reorder 19, links-and-guards 5).
- Playwright MSW, `--retries=0 --workers=1`, PORT=3118: chromium dashboard 11/11, chromium a11y 23/23, mobile-360 dashboard 11/11, mobile-360 a11y 23/23 (68 of 68).
- Sheet: every row has a status. PASS for U-*, E-A*, E-B*, E-X1, E-X2, regression R1-R9. BLOCKED-SCOPE: E-A1 "lands on /portal after sign-in" (POR 26). NOT RUN: E-X3 Lighthouse (no tooling), real-API re-run (API not started: RPT-001 and EMR-001-F builds running; the developer's 2/2 stands, unobserved by this gate), real-API re-order success and data tiles (need ORD-002 checkout). N/A: U-A11, E-C*.

FIX-ROUND VERIFICATION (mutate production code, run portal vitest, restore)
- 409 RequestInProgress keeps the key: renew-on-everything fails 1 test; message branch removed fails 2; 5xx renew fails 1; 4xx never renews fails 3; unconditional renew in the hook fails 4. Each fix has a test that fails if regressed.
- OrdersTile Asia/Dhaka: mapping the date in UTC fails the 20:00Z test.
- Retries removed: confirmed by grep; four retries-free runs green. A race is statistical, so this is evidence, not proof.
- Handover Amended row: present and correct (read).

TWO NON-PORTAL VITEST FILES (builder's 6 failures)
- goods-receipt-form.test.tsx: 7/7 alone on branch, 7/7 alone on master, passed in the full run.
- walk-in-customer-form.test.tsx: 4/4 alone on branch, 4/4 alone on master, passed in the full run. Fails on master: no (not reproduced either way).
- The branch touches neither file nor their imports beyond a prettier-only reflow in lib/api/errors.ts. Ruling: not caused by this branch; load flakes, 0 failures in a full run of the same head.

FINDINGS
- [LOW] The cold-start sign-in race itself remains in shared files outside HO-POR-002 (`app/providers.tsx` starts MSW in an effect after children fire their first /api fetch; `fixtures.ts` swallows its `__mswReady` timeout). The portal specs close it only in `portal-helpers.ts`; other areas' specs still share it; mock mode only. Backlog POR 38.
- [LOW] goods-receipt-form and walk-in-customer-form (non-POR) failed under load in the builder's run and did not reproduce. Backlog POR 39.
- No BLOCKER or HIGH. No production bug found in the portal.

NOT RUN / GAPS
- Lighthouse LCP (E-X3), real-API re-run, real-API re-order success and data tiles, ReorderButton on the order-detail page (page unbuilt).

Verdict is for the TEST gate only; ACCEPTANCE decides the handover.
