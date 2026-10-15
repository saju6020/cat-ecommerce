GATE: TEST
HANDOVER: HO-IMP-001
COMMITS: branch `ho/IMP-001` at 9c061270 (source head under test), plus this gate's test-only commit
DATE: 2026-10-07
STATUS: PASS

Gate policy (owner, 2026-09-27): only BLOCKER and HIGH findings fail a gate. No BLOCKER or HIGH finding was raised. MEDIUM and LOW items are listed under Backlog.

Test-case sheet: `docs/test-cases/TC-HO-IMP-001.md` (rewritten from §15, §16 and §18, one row per case, executed row by row).

## Counts

| | Count |
|---|---|
| Total rows (64 unit, 70 integration, 11 regression, 15 additional) | 160 |
| PASS | 158 |
| FAIL | 0 |
| BLOCKED | 2 |
| NOT RUN | 0 |

Suites (database `catecommerce_imp` only): Domain 1065/1065, Application 197/197, Infrastructure 119/119, Inventory integration classes 134/134 (121 existing, 13 added by this gate). Whole integration project, run before the last four added tests: 880 passed, 2 failed, both non-Inventory and both green when re-run alone (see Flakes). Build: 0 warnings, 0 errors. `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration."

## BLOCKED

- **I-A1 (empty-database half).** The schema content was observed on the already-migrated `catecommerce_imp` (tables, indexes, views, triggers, constraints, sequences, five seeded locations: PASS). Applying all migrations to an EMPTY database was not done because the brief limits the gate to `catecommerce_imp`. Unblock: permission to create and drop a scratch database.
- **I-F7.** No test performs a sale between count submit and commit and then checks final stock = counted − sold. Unblock: add that case. Recorded as MEDIUM below; it does not fail the gate.

## The holes the earlier gates named, and the result

| Hole | Result |
|---|---|
| U-J2 reconciliation detects drift | Closed. Test-owned position drifted by disabling `tg_stock_position_guard` and re-enabling it inside one transaction before commit. The report returns exactly that row (15 vs 10), the honest sibling is absent, the daily pass raises a `LedgerMismatch` alert, the report is empty after repair, and the trigger is `O` (enabled) afterwards |
| I-I1 second branch | Closed. A batch moved to a second branch's warehouse: sellable 7 and 3 before the recall, 0 and 0 after; FEFO preview from both branches skips it; a port sale at the second branch is refused; a direct insert there raises `tg_stock_batch_not_usable`; stock stays held |
| I-E4, I-E5, real parallelism | Confirmed on the existing tests (20 scopes plan before any commit, commits released together, no retry, no delay): exactly one commit, 19 × `23514 ck_stock_position_balance`; POS variant leaves `on_hand = 0` and exactly one sale |
| I-I7 recall vs reservation | Existing test kept (20 rounds, one reserver, invariant is correct). Added a six-reserver test: 148 reservations committed before the recall landed across 5 rounds (a real interleaving, which the existing test does not demonstrate), every reserver ended refused by plan or `tg_stock_batch_not_usable`, and committed count = `reserved` = trace length every round |
| FI-1 two-statement trigger body (§20.3) | Behaviour verified against the REAL trigger and table: `pg_get_functiondef` shows `ON CONFLICT … DO NOTHING` then `UPDATE`, no `DO UPDATE`; the second of two real sessions is observed waiting on a lock in `pg_stat_activity` (polled, no fixed sleep), then fails `23514 ck_stock_position_balance` after the first commits. The §20.3 decision itself (ratifying the substitution) is still owed by the architect or owner |

## The other confirmations you asked for

- Reserve → release → recall trace: PASS (`I_I2_AReleasedReservationIsNotUnshipped_*`; only the open order is listed and its customer notified).
- Breach `ObservedOn` with `+06:00` accepted (stored instant equal within 1 s), future time (two days ahead) rejected 400: PASS.
- Write-off branch-assignment refusal: PASS (keeper assigned to one branch refused with `StockLocationNotFound`, nothing written; administrator assigned to both: 204 and all positions 0).
- Duplicate alert raise race: PASS (6 rounds × 8 parallel raisers, a fresh key each round, all succeed, one row per key).
- `RecallNotified` audit row: PASS (read from `audit.audit_entry`, carries `"Requested":4`).
- Ledger immutability: PASS for both entry tables (ledger existing test; reservation entries added). Stock cannot go negative: PASS (direct `Wastage −5` against 1 unit raises `ck_stock_position_balance`; HTTP adjustment is 409).
- FEFO order and landed-cost allocation: PASS (U-E1, U-E3, U-E4, I-E1, I-E2; U-G1…G4, I-G1…G4, allocated totals sum exactly).
- `InventoryReadSideConventionTests` fails on an offending pattern: PASS. A temporary line `db.Batches.ToDictionaryAsync(b => b.Id, b => b.LotNumber)` was appended to `AlertsAndReportsQueryHandlers.cs`; the test failed naming that file; the edit was reverted with `git checkout` (file clean, nothing committed) and the test passed again.
- Also added because the sheet marked them NOT BUILT or partial: I-X1 (log capture: no customer names, mobile numbers, cat name or advice text across recall, trace and notify; the capture is asserted to contain the recall line so an empty capture cannot pass); I-I4 and I-I6 against the real `NotificationWorkCycle`; parallel duplicate supplier code and parallel count open (both 400, never 500, closing ARCH MEDIUM 1).

## Flakes

- `ShopCataloguePerformanceTests.I_E11`: failed in the whole-project run under load; PASS alone.
- `ShopBundlePrescriptionEndpointTests.FiltersCache_IsKeyedOnTheParsedCategory_JunkInputDoesNotGrowIt`: passed in the whole-project run; PASS alone.
- `TimingEnumerationTests.CustomerSignIn_LockedAccount_StillCallsVerifyAgainstDummy` (Account, NOT on your list): failed in the whole-project run, PASS alone. Timing comparison under load, unrelated to Inventory.

## Findings

No BLOCKER. No HIGH. No production defect found; no production code was changed.

## Backlog (MEDIUM and LOW, for `docs/backlog/IMP.md`)

| # | Severity | Item |
|---|---|---|
| T-1 | MEDIUM | Case I-F7 (sale between count submit and commit) has no test. Add it |
| T-2 | MEDIUM | Migrations were never applied to an empty database in this gate (I-A1). Run `InventorySchemaTests` against a freshly migrated scratch database before merge or as part of the integrator's migration regeneration |
| T-3 | MEDIUM | §20.3 ratification of the FI-1 two-statement trigger (behaviour verified; the decision is owed by the architect or owner) |
| T-4 | LOW | Existing `I_I7_*` uses one reserver against an HTTP recall and never measures that the two sides overlapped. The added six-reserver test covers it; consider deleting or folding the older one |
| T-5 | LOW | Existing `TwoRacingRaisesOfTheSameAlert_*` reuses one dedup key for every round, so only round 1 can race. The added fresh-key test supersedes it in effect |
| T-6 | LOW | `InventoryDatabaseFactsTests` FI-1 and FI-5 use `Task.Delay(750)` to show a writer is blocked (the delay gates an assertion, it does not rescue a pass, so not a §20.4 breach). Replace with a `pg_stat_activity` poll as the added FI-1 test does |
| T-7 | LOW | `InventoryReadSideConventionTests` only matches `db.<AggregateSet>` and is exempted by a `.Select(` anywhere in the same `;`-delimited statement, even one after the materialiser. Narrow, though it does fail on the classic offender |
| T-8 | LOW | I-I6's observable result is a `Suppressed` request with reason `SubjectNoLongerApplies` and no notification row, not a `Cancelled` notification as §16 words it (the guard fires at expansion). Behaviour is correct; the handover wording should be amended |
| T-9 | LOW | The added U-J2 test shows the application's database role can `ALTER TABLE … DISABLE TRIGGER` on `inventory.stock_position`. This confirms SECURITY's LOW finding that the position guard is a convention, not a privilege |
| T-10 | LOW | `TimingEnumerationTests.CustomerSignIn_LockedAccount_StillCallsVerifyAgainstDummy` fails intermittently in a full-suite run (Account area; passes alone) |

## What this gate added (test code only)

- `tests/CatEcommerce.IntegrationTests/Endpoints/InventoryTestGateEndpointTests.cs` — U-J2, I-I1 second branch, write-off assignment, negative stock, FI-1 on the real trigger, six-reserver I-I7, fresh-key alert race, parallel supplier-code and count-open races, reservation-entry immutability.
- `tests/CatEcommerce.IntegrationTests/Endpoints/InventoryLogPrivacyEndpointTests.cs` — I-X1.
- `tests/CatEcommerce.IntegrationTests/Endpoints/InventoryRecallDeliveryEndpointTests.cs` — I-I4 delivery and I-I6 cancellation through the real delivery pass.
- Test code only. No existing test was deleted or weakened. No production file is changed (the one temporary edit was reverted).
