GATE: TEST
HANDOVER: HO-SHP-001-FU1
COMMITS: becc18f (code head); sheet docs/test-cases/TC-HO-SHP-001-FU1.md
DATE: 2026-10-06
STATUS: PASS

CHECKS RUN (31 cases: 30 PASS, 1 FAIL, 0 BLOCKED, 0 NOT RUN)
- dotnet build 0 warnings 0 errors. Domain 683/683, Application 134/134, Infrastructure 107/107.
- Integration suite (Category!=Performance, catecommerce_dev): 690 passed, 1 failed of 691. FU1_T1, FU1_T1b, FU1_T2 pass.
- I-E11 perf: p95 295.5 ms against the 500 ms budget.
- Web: tsc clean, lint clean, vitest 137 files / 694 tests pass.
- Playwright (PORT=3106, workers=1) shop-staff, shop-product, shop-catalogue on chromium and mobile-360: 40/40 in one run, no rerun. Dev server stopped.
- Mutation 1 (handler drops food, images, healthFocus): FU1_T1 and FU1_T2 fail, as required. Reverted.
- Mutation 2 (ProductDetailsForm healthFocus default []): the S1 regression test and the "ticks stored health focus" test fail, as required. Reverted.

FINDINGS
- [MEDIUM] tests/CatEcommerce.IntegrationTests/Endpoints/WaitlistFlowTests.cs:304 — Close_NonUtcBranchNearLocalMidnight_EntryNotYetPast_StaysWaiting fails with session_invalid on this branch (reran alone, same). Pre-existing and unrelated: the branch base 578dc79 lacks master's test-only fixes 9644a8e and 0c41fa6. Should clear on merge with master; the integrator should confirm the suite is green after the merge.
- [MEDIUM] (carried from CODE_REVIEW) FU1_T1/FU1_T2 do not assert multi-image sortOrder ordering or a SKU-level image (skuId non-null). Backlog.
- [LOW] (carried) StaffProductDetailViewModel new fields are nullable with defaults while the web treats them as required.
- [LOW] (carried) Mock staffFood returns null when all fields are empty; the real API returns an object.
- [LOW] (carried) No test that Reload re-initialises forms, nor that a loaded form is not dirty.
- [LOW] Mutations were combined (all three handler fields at once); per-field sensitivity not shown.

SCOPE CHECK
- files in allowed scope: 18/18 (16 slice files, the test sheet and this record)
- files outside allowed scope: none (web/public/mockServiceWorker.js left uncommitted and untouched; mutations reverted with git checkout)
