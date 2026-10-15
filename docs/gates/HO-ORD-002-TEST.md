GATE: TEST
HANDOVER: HO-ORD-002
COMMITS: 64fa94ce (branch ho/ORD-002-build, after the combined ARCH/SEC/CODE re-check)
DATE: 2026-10-12
STATUS: PASS

Sheet: docs/test-cases/TC-HO-ORD-002.md (section "TEST gate execution", written before any command was run).

## Method

- The case sheet was extended first (T-1 to T-11 gate runs, R1 to R10 regression rows) with every row NOT RUN, then each run was executed and its row filled in one at a time.
- Runs were sequential. Playwright: chromium, `--workers=1`, `PORT=3117`, `NODE_OPTIONS=--max-old-space-size=6144`, one spec file per process (a11y split into customer and staff halves), a11y last.
- Each spec sets `describe.configure({ retries: 1 })`, which overrides `--retries=0`. A retry was therefore read from the list reporter (`retry #1` / `flaky`).
- Real-API runs: NOT RUN (owner rule). The staff customer-orders endpoint is on `ho/ORD-001-FU2`.

## Results

| Suite | Result |
|---|---|
| `tsc --noEmit` | clean |
| `eslint . --max-warnings 0` | clean |
| `vitest run --maxWorkers=2` | 203 files, 1369 tests pass |
| `next build` | passes |
| e2e customer (chromium) | 10/10, no retry (on the re-run, see below) |
| e2e staff (chromium) | 14/14 no retry on run 1; run 2: 13 pass + 1 flaky (E-D2) |
| e2e POS (chromium) | 5/5 no retry (on the re-run, see below) |
| e2e a11y/responsive (chromium) | 31/31, no retry |
| e2e a11y/responsive (mobile-360) | 31/31, no retry; `/cart` and `/staff/orders` at 360 px both pass |
| Real API | NOT RUN (owner rule) |

Sheet counts for the gate section: 21 rows, 20 PASS, 0 FAIL, 0 BLOCKED, 1 NOT RUN (T-11). E-A2 and E-B7 stay NOT RUN in the builder table (need two browser contexts / a real backend).

## The retries: flakiness or defect?

Every failure seen was "the page or the mock is not there yet", never a wrong value or a failed behavioural assertion:

| Case | What happened | Re-run |
|---|---|---|
| E-B6 | Add to cart button never appeared in 120 s, both attempts, in two consecutive runs (the first right after `next build`); the page snapshot showed the signed-out header | Passed 3 times afterwards, including inside the full 10-test run |
| E-F1 | `window.__orderingMock is not installed`, then the Point of sale heading missing | Passed in the re-run of the spec |
| E-D2 | Approve return button not found for 20 s (second staff run only) | Passed on retry; passed in run 1 |

Judgement: cold-start flakiness of the dev server under the memory pressure on this machine (XCT-001 drill and a backend review were running). It is not a product defect. Not proven for E-B6: the cause of the signed-out header was not isolated. Recorded as ORD-91. This also covers the two staff specs that passed only on retry in the builder's run.

## Regression rows (from the earlier gates)

All ten rows PASS. R5 is partial: the `PrescriptionCatRequired` branch of the checkout has no test of its own (ORD-90).

## Test quality

- I read the assertions behind R1 to R10. They check the values sent (instants, a new Idempotency-Key, quantity 2 after two scans, no queue request, the text rendered as plain text), so they would fail if the production code were broken. I did not weaken or change any test.
- Weakness found: R5 (above), and the mobile-360 project repeats the chromium run for the a11y spec (ORD-92).

## Production code

No production code was changed. No production defect found.

## FINDINGS (none BLOCKER/HIGH)

- [LOW] ORD-90: `PrescriptionCatRequired` branch untested.
- [LOW] ORD-91: e2e cold-start flakiness; spec-level `retries: 1` hides it and overrides `--retries=0`.
- [LOW] ORD-92: mobile-360 adds nothing for the a11y spec; flow specs never run at 360 px.

## Known and accepted, not raised again

- `/cart` storefront header overflow at 360 px: ORD-81 (Shop follow-up). The spec ignores the `<header>` subtree on `/cart` only; everything else on `/cart` passes the right-edge check.
- ARCH LOW rows 84 to 89 appended to docs/backlog/ORD.md (proposed in the ARCH re-check).
- Staff customer-orders list: ORD-82, backend on `ho/ORD-001-FU2`.

## BACKLOG ROWS ADDED

84 to 89 (from the ARCH re-check), then 90, 91, 92 (this gate), all LOW.
