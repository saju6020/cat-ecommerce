GATE: TEST
HANDOVER: HO-ORD-001
COMMITS: 85c74f53 (head under test) plus the new test file `tests/CatEcommerce.IntegrationTests/Endpoints/OrderingTestGateEndpointTests.cs`
DATE: 2026-10-10
STATUS: PASS (no BLOCKER or HIGH finding; 2 MEDIUM and 2 LOW go to docs/backlog/ORD.md rows 41-44)
SHEET: docs/test-cases/TC-HO-ORD-001.md (section "TEST gate")

ENVIRONMENT
- Build green. Database `catecommerce_ord` only. Real PostgreSQL 17, in-process host. No broker or external service.
- Unit: Domain 1773, Application 372, Infrastructure 172 -- all passed.
- Integration (full): 1325 total, 1315 passed, 3 failed, 7 skipped. Of the 3: `T4a` is F-1; `I_E11` (p95 1976 ms under load) passes alone; `ShopStaffProductDetailReadEndpointTests.FU1_T1` (another area's test) passes alone 3 of 3.
- Skipped: 3 diet-gate rows `needs HO-PHM-001 merged` (I_C4, I_D5, I_D5b -- left skipped as instructed); 2 not ORD (EMR G5, Billing XI_15); 2 new, T4b and T4c, skipped by me with the finding id (assertions unchanged).

RESULTS (new cases; ids as in the sheet)
- PASS: T1 (with F-2), T2 (with F-3), T3, T4d, T4e, T5a, T5b, T5c, T6.
- FAIL: T4a (intermittent, full run only), T4b, T4c (F-1).
- BLOCKED: I_C4, I_D5, I_D5b (HO-PHM-001).

FINDINGS

F-1 [MEDIUM] Gapless numbering: a scoped issuer (Billing's transaction scope, used by Ordering's counter sale and cancellation) and an unscoped issuer (staff invoice or staff credit note) at the same branch and year can deadlock, and the loser is an unhandled 500. ADR-179 as amended and the architecture note say the staff side "waits on FOR UPDATE then proceeds" and "nothing deadlocks"; that is not what happens.
  - Reproduction: `T4b` (3 customer cancellations + 4 staff credit notes, one branch): 500 on two cancels in 2 of 3 runs; `T4c` (all four issuers): 3 of 3. Invoice kind: the existing `OrderingBillingNumberingEndpointTests` failed once (500 on one of 14) in the first full run and my `T4a` failed once in the second; both pass alone (5 of 5 and 3 of 3), so it needs load or enough overlap. The credit-note-only runs `T4d` and `T4e` (scoped alone, unscoped alone) never failed.
  - Evidence: PostgreSQL log `deadlock detected` with `INSERT INTO billing.credit_note` (waiting through `ix_credit_note_number`) against `UPDATE billing.document_number_series ... AND xmin`. Application log: `40P01` raised from `DocumentNumbering.LockSeriesRowAsync` (line 64) and from the save. Likely cause: the unscoped side reads the series row without a lock, takes number N, and its save writes the document and the series row in an order that crosses the scoped side's `FOR UPDATE` lock and its own insert of the same number. `IsCollision` retries 23505 on the backstop indexes and xmin conflicts but not 40P01, and the scoped side cannot retry at all.
  - Impact: the failed transaction rolls back whole. A customer cancel, a counter sale (which also opens a `CounterSaleFailed` reconciliation issue) or a staff invoice returns 500 and works on retry. It needs two issuers within a few milliseconds at one branch, which is rare at clinic volume. Rated MEDIUM, not HIGH, for that reason. The owner may reasonably upgrade it, because a documented guarantee is false and one suite test is intermittently red until it is fixed.
  - Remedy (Billing's area; ORD must not change it): make the unscoped path take the same series lock (a short transaction with `FOR UPDATE` before the read) or treat 40P01 as a collision and retry; map 40P01 to a business answer so it is never a bare 500. Then un-skip T4b and T4c.
  - Backlog ORD 41.

F-2 [MEDIUM] Counter sale to a new buyer: the "raced, find the other buyer" fallback in `CompleteCounterSaleCommandHandler.RegisterBuyerAsync` can never run, as the architecture re-check predicted. Registration inside the sale's transaction (savepoints off) hits `ix_customer_account_mobile_number` 23505, which aborts the transaction, so the loser is answered `400 MobileNumberAlreadyRegistered` ("log in, or reset your password") and a false `CounterSaleFailed / DbUpdateException` reconciliation issue is opened.
  - Observed: 6 rounds x 8 concurrent sales to one new mobile: each round 1 x 201 and 7 x 400; 42 of 42 losers left an open issue. No 5xx, no second account, and nothing sold or invoiced for a loser. The cashier's retry then sold to the new customer (6 of 6 retries 201). So no lost sale and no lost money; the harm is a misleading message and reconciliation noise that an accountant must resolve by hand.
  - Realism: two cashiers selling to one new mobile in the same moment is unusual; a double submit with a fresh key after a timeout is the likely trigger.
  - Remedy: register the walk-in in its own short transaction before the sale's scope (then the fallback works), or take a transaction-level advisory lock on the mobile before the lookup; do not open an issue for a refusal that is the expected outcome.
  - Backlog ORD 42.

F-3 [LOW] A mobile held by a non-Active account (Merged, Anonymised) can never be sold to at the counter: the lookup returns only Active accounts, registration collides with the unique index, and the cashier gets the same "log in or reset your password" message plus a `CounterSaleFailed` issue. Nothing is lost; no 5xx. Point the cashier to the merge target, or answer with a clean issue-free refusal. Backlog ORD 43.

F-4 [LOW] Test hygiene: `I_E11` (p95 budget) and `FU1_T1` (Shop upload step) are intermittently red in a full run under load and pass alone. Not ORD defects. Backlog ORD 44.

THE FIVE CASES HANDED OVER BY THE ARCHITECTURE RE-CHECK
1. Counter sale, new buyer: tested (T1, T2). No unhandled 500 and no lost sale; false issue and misleading 400 (F-2, F-3), so not HIGH.
2. FindCustomersQuery as the cashier: PASS (T3). Reception gets the customer and the sale is theirs.
3. Invoice numbering, scoped versus unscoped: FAIL, intermittent (F-1). The staff side does not simply wait; a deadlock or an exhausted retry is possible. A cross-kind lock cycle (invoice series against credit-note series) was not observed; the deadlocks seen are inside one series.
4. Billing refusals in approve and reject: PASS (T5a PaymentNotPending, T5b ApproverMustDiffer, T5c NoPaymentAwaitingApproval). Each answers 4xx, the hold and the order version are unchanged, and Billing's payment is untouched.
5. Refund draw plan: PASS (T6). The excess released by the credit note issued earlier in the same transaction is visible and Billing records the full refund.

NOT VERIFIED
- After a deadlocked run I did not re-read the series to confirm that no number was consumed; passing runs of T4a, T4d and T4e assert distinct contiguous numbers.
- More than one host: Billing's in-process gate is per host, so a multi-host deployment is outside what this gate observed.

PRODUCTION CODE
- None changed. Test additions only: `OrderingTestGateEndpointTests.cs` (12 tests, two skipped with F-1).
