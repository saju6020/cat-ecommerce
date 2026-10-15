GATE: TEST
HANDOVER: HO-EMR-002
COMMITS: cacfb200 (head under test; master merged at 3258ad8d)
DATE: 2026-10-09
STATUS: PASS

Sheet: docs/test-cases/TC-HO-EMR-002.md, section "TEST gate run". Written before any suite ran.

RESULTS
- tsc --noEmit: clean. eslint --max-warnings 0: clean. next build: PASS.
- Vitest whole web suite, --maxWorkers=3, default 5 s timeout: 169 files, 1003/1003 passed. Default workers not run (loaded host).
- EMR Vitest files: 16 files, 152 tests, all passed. U-D1..D4 DEFERRED (Treatments, HO-EMR-001 Phase F not on master).
- Playwright chromium, 4 EMR specs, --workers=1, PORT 3110: run 1 = 143 passed, 1 failed (E-D2). --last-failed re-run: E-D2 failed again (staff sign-in 404, MSW not ready after dev-server restart). E-D2 run alone: PASS, p95 1004 ms (< 2000 ms). Every test was observed passing in some run.
- E-X1 axe 30/30; E-X2 viewport tests passed (chromium). E-C2 DEFERRED.
- Real API (:5009, catecommerce_dev, not migrated, web :3110 mocks off): E-X real PASS; E-A real and E-B real SKIPPED by the spec (no open visit in the dev DB); the other real-API cases were not run (need clinical data, scanner, or idle expiry).

FINDINGS
- [MEDIUM] web/tests/e2e/emr-history-diagnostics.spec.ts:40 — E-D2 fails on a loaded host (5 s expect on first cold load); backlog EMR #28.
- [MEDIUM] web/tests/e2e/emr-real-api.spec.ts — E-A real / E-B real have never run; the dev DB has no open visit. The coordinator offered permission to create dev-only test data, but the permission system denied the step, so none was created; backlog EMR #31.
- [LOW] web/tests/e2e/emr-real-api.spec.ts — credentials are not loaded from .env.e2e.local, so the spec silently skips unless exported; backlog #29.
- [LOW] docs/test-cases/TC-HO-EMR-002.md — fix-round-1 test count 19 stated, 18 actual; backlog #30.

SCOPE CHECK
- files in allowed scope: 3/3 (TC sheet, backlog, this gate file); no product code changed
- files outside allowed scope: none

ADDENDUM 2026-10-09 (real-API cases E-A real, E-B real)
- Owner approved creating dev-only test data in catecommerce_dev. Created via the real API as e2e.vet: customer ab9555b5-b056-4e25-a2c6-a8d004be9c4f, cat 41d546ea-54cc-45c2-bfa0-9439881b8956, checked-in walk-in appointment 679ffde8-24ea-44f7-9c2a-a8cd827aaab9 (main branch), open visit b0d728a4-c9b1-4ba9-a05a-6a2b2429b9d7. No SQL, no migration.
- emr-real-api.spec.ts, chromium, --workers=1, API :5009 + web :3110 mocks off: 3 passed, 0 skipped (E-X real, E-A real, E-B real).
- E-A real: draft auto-saved, text and restore notice present after reload; a direct GET of the visit confirmed the draft on the server.
- E-B real: history drawer rendered Active problems and the Prescriptions "unavailable" text on real data.
- No product bug found; no spec change needed. Backlog EMR #31 closed. Verdict unchanged: PASS.
- Note: e2e.reception and e2e.admin do not exist in this dev DB (401); only e2e.vet was used.
