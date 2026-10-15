GATE: TEST
HANDOVER: HO-PHM-002
COMMITS: a8761829..4fd0e8e0 plus the TEST commit (branch ho/PHM-002)
DATE: 2026-10-11
STATUS: PASS

Checked, all observed: `tsc --noEmit` clean; `eslint . --max-warnings 0` clean; `next build` passes; vitest 204 files / 1323 tests, 1321 pass (the 2 that failed in the parallel run are inventory timing tests and pass alone); Playwright on MSW chromium 137 passed, mobile-360 a11y/responsive 129 passed; Playwright against the REAL API (fresh DB catecommerce_phm002e2e, API :5013, web :3116, workers=1) 20 passed, including two deliberate expected-failure cases that pin findings 50 and 51. Sheet: docs/test-cases/TC-HO-PHM-002.md.

The build had only been verified on MSW. The real run found no BLOCKER or HIGH defect in the slice. The defects it found were in the TEST SPEC (wrong customer cookie name, 60-day validity beyond the 30-day limit, strength read/write shape, stale-fingerprint design for R-A5, non-idempotent cases) and were fixed in the spec; no production code was changed.

FINDINGS
- [MEDIUM] SafetyPanel warning order: the real API puts DoseNotCheckable before the blocking allergy; handover U-A5 wants the allergy first (backlog PHM 50; pinned by R-A2o).
- [MEDIUM] Remote renewal: the first submit is always 409 PrescriptionWarningsChanged, because the live check and the renew endpoint disagree on the "already active" warning (backlog PHM 51; pinned by R-A6o).
- [LOW] Flaky timing tests under load (2 inventory vitest cases; a cold mobile-360 sign-in) (backlog PHM 52).
- [LOW] Formulary read vs write strength shape differs (backlog PHM 53).

Not run: notification/seal/refill workers against the real API; printing on paper.

SCOPE CHECK
- files changed: docs/test-cases/TC-HO-PHM-002.md, docs/gates/HO-PHM-002-TEST.md, docs/backlog/PHM.md, web/tests/e2e/pharmacy-real-api.spec.ts, tests/CatEcommerce.IntegrationTests/PharmacyE2ESeed.cs (dev-only seeder, a no-op without E2E_SEED_FILE)
- production code touched: none
