GATE: TEST
HANDOVER: HO-POR-001
COMMITS: 38a1c2f0 (branch ho/POR-001) plus the gate's test-only commit (the commit that adds this file)
DATE: 2026-10-10/11
STATUS: PASS

Scope: "after Phase D" per HO-POR-001 1 Amended (stays and subscriptions deferred by owner; portal re-order retired). Sheet: docs/test-cases/TC-HO-POR-001.md (updated, not replaced). Database: `catecommerce_por` only.

RESULTS
- Handover cases: 67 = 31 PASS + 34 RETIRED + 2 DEFERRED-BY-OWNER; 0 FAIL, 0 BLOCKED, 0 NOT RUN. X- rows 21 PASS (3 added), regression rows 10 (8 PASS, 2 n/a: open backlog items).
- Unit: Domain 1886, Application 427 (47 Portal), Infrastructure 174, all passed.
- Integration: 1476 run, 1472 passed, 2 skipped, 2 failed, neither Portal (below). Portal filter 33 of 33 passed.

TEST BUG FIXED FIRST (test code only)
- Cause confirmed: `OnTheGrid(Now.AddDays(3))` rounds the real clock down to 15 minutes. Run at 23:41 Dhaka it gave a 23:30 Dhaka start; the 30-minute service then ends after the test clinic's 23:45 close, so the hold returned 409 `SlotNoLongerAvailable`. Reproduced before the change.
- Fix: `PortalIntegrationTestBase.BookableSlot(daysAhead = 3)` = 10:00 Asia/Dhaka on the Dhaka date of the clock plus 3 days, as UTC. `OnTheGrid` is removed (it had no other user). The four named tests pass at 23:4x Dhaka.
- Sweep of other helpers: no other helper uses the pattern (Appointments tests use a fixed `BaselineNow`). Residual real-clock date arithmetic listed in POR backlog 16; not changed.

FINDINGS
- [LOW] Transferred cat still named in the former owner's own appointment list (Appointments' read). Backlog POR 14.
- [LOW] `OrderingPaymentEndpointTests.AReferenceThatLooksLikeACardNumber_...` failed once under load (hard-coded total 2135.00m); passed alone 4 of 4. Not Portal. Backlog POR 15.
- [LOW] Known `I_E11` (shop perf) fails alone at p95 580 ms vs 500 ms; fix in progress elsewhere. Not Portal. Backlog POR 15.
- [LOW] Time-of-day sweep note. Backlog POR 16.
- No production bug found in the Portal. No BLOCKER or HIGH.

TESTS ADDED / STRENGTHENED (test code only)
- New `PortalOwnershipChangeEndpointTests` (2 tests): transferred cat leaves the former owner's cats and visits and reaches the new owner (precondition proves the former owner saw them first); merged account: a third customer's raw dashboard has no id or name of either account, the merged-away session is 401, the survivor sees Billing's folded-in invoice.
- Existing IDOR tests judged adequate: two-customer I-B3 (cats, visits, invoices, name, balance amount, forged `customerId`/`ownerId`), orders and prescriptions two-customer tests, deceased-cat tests (cats, visits over HTTP; prescriptions over HTTP and unit). None would pass if the reader dropped the owner filter (each asserts equality with the exact expected id list, not a subset).
- Judged weak: none requiring change. I-B8 is measured at 3 cats only (noted as a gap with POR 1 and 9).

NOT RUN
- U-B14, U-B15 (stays, subscriptions): the readers do not exist (owner deferral).

Verdict is for the TEST gate only; ACCEPTANCE decides the handover.
