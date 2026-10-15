GATE: TEST
HANDOVER: HO-BIL-001 (Phase J only -- automatic billing from visits, dispensing and surgery)
COMMITS: ho/BIL-001-J at ae926317 plus the TEST gate's test files (this commit)
DATE: 2026-10-14
STATUS: PASS (no BLOCKER or HIGH finding; 2 LOW go to the backlog as rows 98 and 99)

Sheet: docs/test-cases/TC-HO-BIL-001.md, section "Phase J TEST gate". Written with every row NOT RUN before anything was run, then filled one row at a time from what was watched.
Owner rule (2026-09-27): only BLOCKER and HIGH fail a gate. Database used: catecommerce_bilj only (full connection string in CATECOMMERCE_TEST_DB). No production code was changed.

## Counts (49 rows of the Phase J TEST gate)

| | Count |
|---|---|
| PASS | 47 |
| FAIL | 1 |
| BLOCKED | 1 |
| NOT RUN | 0 |

- FAIL is JT-C1: the single full-suite run had 3 failures, none in Phase J (see below). All three pass alone.
- BLOCKED is OD-7b: a surgery with no `owner_at_discharge_id` cannot be made (backlog 99).

## Order of work and results

1. **Flagged tests, run first.** J7, J7d to J7i, J4c, J4d, J2c, J3b, J1c, J7e: 12 of 12 passed against the final code. `SurgeryAggregateTests.Discharge_StoresTheOwnerAtDischarge_AndCarriesItOnTheEvent`: passed.
2. **Build, unit suites, model.** Build: 0 errors (32 warnings on a full rebuild, none in a file this branch changed). Domain 2132, Application 623, Infrastructure 198 (201 with my 3 new tests): all passed. `has-pending-model-changes`: none.
3. **Full integration suite, once, on catecommerce_bilj.** 1862 total, 1853 passed, 3 failed, 6 skipped, 13 m 12 s. No testhost was running beforehand. All 25 BillingPhaseJEndpointTests passed. Failures, each rerun alone and passed:
   - `ShopCataloguePerformanceTests.I_E11` (known flake).
   - `ReportingScheduleEndpointTests.I_G6` (known flake).
   - `ReportingScheduleEndpointTests.I_G5` (**not** on the known list; same class and same error as I_G6, "The condition did not hold after 600 delivery passes"). It uses nothing Phase J touches; it is the same shared-database schedule-runner coupling.
   - The other listed flakes (WaitlistFlowTests IG2 to IG4, U_G7, I_G3, I_G8, T_I7, XI_6, XI_9) all passed in this run.
4. **Mutations.** Eight asked for; the SurgeryId guard sits in two places, so nine were run, plus one extra on the visit source (item below). Every file mutation was restored with `git checkout -- <file>` and `git status` was clean after each.

| Mutation | Killed by | Failure seen |
|---|---|---|
| Remove `SurgeryId == null` from the uncaptured query | J7 | surgery's appointment appears on the list |
| Remove the `SurgeryId` refusal in the appointment source | J7 | manual Appointment capture bills 1 line, expected 0 |
| Drop the partial-capture count (visit) | J4d | partly captured visit leaves the list |
| Break the idempotency key (constant dispense line ref) | J6, JT-E2 | 1 charge, expected 2 |
| ServiceDate from the UTC date | J1c, J7e | expected 2026-10-15 |
| Bill the booking customer at discharge | J7d | nothing billed to the owner |
| Skip the merge chain | J2b, J2c | 0 charges, expected 4 |
| Drop the owner-at-discharge freeze trigger (DB) | J7i | expected CR001 |
| Drop the diagnostic price/tax-class freeze (DB) | **nothing that existed** | 56 related tests all passed (survived); my JT-G1 kills it |

   The two database mutations were applied to catecommerce_bilj with a throwaway Npgsql tool and reversed; the function and trigger definitions were compared with `cmp` against the text saved before, and are identical.
5. **Money maths.** Expected totals worked out before running and written as literals: visit 1,090.00 (500 + 2 x 120 + 350), dispense 855.00 (6 x 85.50 + 4 x 85.50), surgery 5,285.50 (5,000 + 2 x 120 + 1 x 45.50, the second drug corrected from 3 to 1), together 7,230.50 over 8 lines. All four cases passed to the paisa.
6. **Overlapping race.** JT-F1 to JT-F3: finalise, then two captures (and, in F2, an event replay) released by one barrier. Each test measures the request intervals and asserts that they overlapped. The book held each line exactly once, no 5xx, and in F3 (no event handler, so both callers had all three lines to insert) the callers together reported exactly 3 captured. Run four times in total, all green.

## Findings

No BLOCKER, no HIGH, no MEDIUM. No production bug found.

- [LOW, resolved by tests, backlog 98] Two rules had no test that could fail.
  - Row 86's database freeze of a diagnostic order's price, currency and tax class survived removal (above). Added `JT_G1`.
  - The owner decision "old visits: bill by hand" was only tested by feeding the handler a fake source that already said `VisitCustomerNotRecorded`, so a real source that billed an owner-less visit to someone else would have passed. Added `VisitBillableItemSourceTests` (real source, no database needed); a mutant that falls back to `Guid.Empty` fails it. The old test stays; it is decoration for this rule.
- [LOW, open, backlog 99] A surgery with no `owner_at_discharge_id` (`SurgeryCustomerNotRecorded`) and the uncaptured list's owner-present filters are only read. The API always stores the owner and the database refuses to clear it. Needs a seam or a test-only seed.

## Tests added (tests only)

- `tests/CatEcommerce.IntegrationTests/Endpoints/BillingPhaseJGateTests.cs`: JT_E1 to E4, JT_F1 to F3, JT_G1.
- `tests/CatEcommerce.Infrastructure.Tests/Billing/VisitBillableItemSourceTests.cs`: 3 tests.

## Other notes

- Owner decisions of 2026-10-13/14 each have a row (OD-1 to OD-7b); all PASS except OD-7b.
- Not covered, already backlogged: charges already invoiced when a dispensing is reversed (row 72), the capture/reversal race (row 87).
- Temp output named `bilj-test-*` and the throwaway SQL tool were removed after the run.
