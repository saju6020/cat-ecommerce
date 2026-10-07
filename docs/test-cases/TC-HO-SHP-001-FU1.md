# TC-HO-SHP-001-FU1 — Staff product detail read completeness (backlog S1)

Handover: docs/handover/HO-SHP-001-FU1.md
Commit(s) under test: becc18f (code head); gate records up to 09fbdbd
Written: 2026-10-06  ·  Executed: 2026-10-06

> Statuses record what was observed. IDs U/I/E/R below are assigned by this gate where the handover
> lists only FU1-T1/T1b/T2 (backend) and prose (web). The sheet was written with every row
> NOT RUN before anything was executed.

## Summary

| | Count |
|---|---|
| Total | 31 |
| PASS | 30 |
| FAIL | 1 |
| BLOCKED | 0 |
| NOT RUN | 0 |

The one FAIL (I-FULL) is a single pre-existing waitlist test unrelated to this slice; see below.

## Environment

Windows 11, worktree D:/my-repo/cat_ecommerce-wt/SHP-001-FU1, branch ho/SHP-001-FU1, head becc18f
(plus docs commits). dotnet 9, shared catecommerce_dev (no new migrations on this branch). Web:
`next dev` with MSW mocks (.env.local present), PORT=3106, --workers=1, one dev server, stopped
afterwards (only TIME_WAIT sockets left). No external services needed.

## Unit cases

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U1 | dotnet build | 0 errors | PASS | Build succeeded, 0 warnings, 0 errors |
| U2 | Domain.Tests | all pass | PASS | 683/683 |
| U3 | Application.Tests | all pass | PASS | 134/134 |
| U4 | Infrastructure.Tests | all pass | PASS | 107/107 |
| U5 | Web tsc --noEmit | clean | PASS | no output, exit 0 |
| U6 | Web lint (max-warnings 0) | clean | PASS | no output |
| U7 | Web vitest --maxWorkers=3 | all pass | PASS | 137 files, 694/694 |
| U8 | FoodDetailsForm shows every stored food field | pass | PASS | in U7 run |
| U9 | ProductDetailsForm ticks stored health focus | pass | PASS | in U7 run |
| U10 | S1 regression: Details save without touching health focus sends loaded value | pass | PASS | in U7 run |
| U11 | ImagesEditor lists stored images and re-sends them | pass | PASS | in U7 run |

## Integration cases

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| FU1-T1 | GET returns food, images, healthFocus, SKU attributes, skus[].label | pass | PASS | not among failures in the full run (690/691); failed only under mutation R1 |
| FU1-T1b | No food/images: food null, images [], healthFocus [] | pass | PASS | not among failures in the full run; passed under mutation R1 as expected |
| FU1-T2 | Round trip re-save preserves healthFocus, food, images | pass | PASS | not among failures in the full run; failed only under mutation R1 |
| I-FULL | Full integration suite Category!=Performance | no failures | FAIL | 690 passed, 1 failed of 691: WaitlistFlowTests.Close_NonUtcBranchNearLocalMidnight_EntryNotYetPast_StaysWaiting, `{"error":"session_invalid"}` at WaitlistFlowTests.cs:304. Reran alone: fails again. Branch base 578dc79 lacks master commits 9644a8e and 0c41fa6, test-only fixes for fake-clock vs wall-clock and session cookie. The slice touches only Read-side code and a new test file. Not caused by this slice; not verified green on master by this gate |
| I-E11 | Shop perf test, p95 <= 500 ms | pass | PASS | p95 = 295.5 ms, p50 128.4 ms, 300 requests, 8 concurrent |
| E1 | Playwright shop-staff, chromium | pass | PASS | one run of all three specs on both projects: 40 passed (3.5 m); no rerun needed |
| E2 | shop-staff, mobile-360 | pass | PASS | same run |
| E3 | shop-product, chromium | pass | PASS | same run |
| E4 | shop-product, mobile-360 | pass | PASS | same run |
| E5 | shop-catalogue, chromium | pass | PASS | same run |
| E6 | shop-catalogue, mobile-360 | pass | PASS | same run |

## Regression / mutation cases (S1 fix)

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | origin | S1: staff read omits food/images/healthFocus | Handler's trailing args replaced by null, empty images, empty healthFocus; FU1_T1 and FU1_T2 must FAIL | PASS | FU1_T1 and FU1_T2 failed, FU1_T1b passed (3 run, 2 failed). Reverted by git checkout |
| R2 | origin | S1: Details save clears health focus | `healthFocus: product.healthFocus ?? []` changed to `[]` (line 110); web regression test must FAIL | PASS | "S1 regression: saving Details without touching…" and "ProductDetailsForm ticks the stored health focus" failed (2 failed, 22 passed). Reverted by git checkout |
| R3 | CODE_REVIEW | MEDIUM: multi-image order and SKU-level image not asserted | No existing test covers it | n/a | Not run; recorded as a gap, not claimed as covered |

The brief asked for food, images and healthFocus to be dropped together; done as one mutation.
Per-field mutations were not run.

## Acceptance-criteria coverage

| Criterion | Covered by | Status |
|---|---|---|
| A1 shape | FU1-T1, FU1-T1b, U8-U11 | PASS |
| A2 additive | existing staff detail integration tests in I-FULL | PASS |
| A3 no migration, no change under Domain, Application, Infrastructure, Api | `git diff 578dc79 HEAD --stat`: src changes only under src/Read; no migration | PASS |
| A4 re-save keeps healthFocus | FU1-T2, R1, U10, R2 | PASS |
| A5 no regression | I-FULL (1 unrelated pre-existing failure), I-E11 pass | PASS with caveat |

## Cases I could not run

None.

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| Multi-image ordering by sortOrder and SKU-level image on read and after re-save | CODE_REVIEW MEDIUM; this gate does not write product tests | SHP backlog |
| Reload re-initialises forms; loaded form is not dirty | CODE_REVIEW LOW | SHP backlog |
| Mock returns food null when all fields empty; real API returns an object | CODE_REVIEW LOW | SHP backlog |
| Per-field mutations (food only, images only) | one combined mutation killed the tests | none |
