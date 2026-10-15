GATE: TEST
HANDOVER: HO-BIL-001
COMMITS: ho/BIL-001 at a4ae560e plus the TEST gate's test files (this commit)
DATE: 2026-10-07
STATUS: PASS (no BLOCKER or HIGH finding; 3 MEDIUM and 3 LOW go to the backlog)

Sheet: docs/test-cases/TC-HO-BIL-001.md. Written with every row NOT RUN before any test was executed, then filled from TRX results.
Counts (158 rows): PASS 147, FAIL 7, BLOCKED 0, DEFERRED 4 (Phase J, by owner decision), NOT RUN 0.
The 7 FAIL rows are 5 TEST-gate probe rows (XU-3, XU-6, XI-7, XI-8, XI-15) and 2 regression rows that restate unfixed earlier-gate findings (R-7, R-8). No handover case row from section 15 or 16 failed.
Whole-project runs on a clean `catecommerce_bil`: 897 passed / 0 failed, then 896 / 1 (the known flake ShopCataloguePerformanceTests.I_E11, which passed when rerun alone). Unit suites green. `dotnet ef migrations has-pending-model-changes`: none.
Owner rule (2026-09-27): only BLOCKER and HIGH fail a gate. Database used: catecommerce_bil only.

What was confirmed (watched, not inferred)
- Gapless numbering under concurrency (I-C3): the developer's single-process test passes; XI-1 (two hosts, one database, 40 invoices from a cold then a warm series) and XI-1b (log shows the collision retries) give contiguous 1..40, no 5xx, series next = 41.
- Idempotency double-submit (I-D5): XI-2 shows the Billing replay filter is added after UniqueConstraintExceptionFilter with equal Order, so it sees the exception first. XI-3: 15 rounds x 6 parallel same-key payments, always one 201 and five 200, one row, and the log holds `ix_payment_idempotency` violations, so the race path really ran. See F-6 for the other keyed writes.
- Immutability triggers: every billing table has a delete trigger except the two AggregateOnly working sets; a real row of every populated table refuses DELETE (XI-6); I-A3 and the payment, credit-note, reconciliation and export tests pass. TRUNCATE is not covered (F-2).
- Card-number guard: every free-text field the SECURITY gate named refuses it (developer tests pass; Bengali digits, NBSP, tab, double space, dot, slash, underscore, spaced dash, glued digit, stray number before it all refused and masked, XU-2, XU-5). It does NOT cover other separators (F-1) or the remaining free-text fields (F-3). No card number reached a table, audit row or log from any flow the developer's tests or mine exercised: I-X1 passes and XI-5 (Trace level, every category) found nothing; XI-9 swept every text/json column of the billing schema, audit and notification tables on the populated database: clean. XI-9 did find the number my own XI-8 planted, which proves the sweep can see one.
- I-J4 loyalty: after a merge both accounts' balances equal their ledger sums, and so does every account in the database (XI-10).
- Two partial credit notes: XI-11 (taxed, three notes, remainder exact to the paisa, fourth refused); loyalty clawback twice passes.
- Cash-up totals: I-F1 has a hand-computed oracle (cash, staff COD, courier COD, card, transfer, cash refund, same-day reversal) and passes; I-F2, I-F3, I-F4 pass.
- Ageing buckets: boundary unit cases XU-1 (0/1/30/31/60/61/90/91) and HTTP edges XI-12 pass, as does I-D10.

FINDINGS
- [MEDIUM] F-1 `src/Shared/CatEcommerce.Shared/Common/Helpers/CardNumberGuard.cs` does not refuse a card number separated by a comma, colon, semicolon, pipe, en dash, em dash, non-breaking hyphen, minus sign, zero-width space or joiner, soft hyphen, brackets, `*` or a letter (15 of 15 probes, XU-3). En and em dashes are what a phone or word processor turns a typed dash into. Fix: treat every non-alphanumeric, non-digit character (and Unicode format characters) between digit groups as a separator. Probe is Skip-marked in BillingTestGateProbeTests.
- [MEDIUM] F-3 The guard is missing on: billing profile legal name, address lines, footer and bank-account name; bank statement account label and uploaded file name; the courier name and courier code on a cash-on-delivery payment. A number typed there is stored, and copied to audit rows (confirmed by XI-9 on the planted values). I rate it MEDIUM because these fields rarely carry a card number, but acceptance criterion A26 ("no card number is stored anywhere") reads literally and the acceptance gate may rate it higher. Test XI-8 (Skip-marked) lists every field.
- [MEDIUM] F-4 Accounting export ignores the caller's branch assignment: an Accountant assigned to branch A downloaded branch B's invoices with `branchId` = B and with `branchId` omitted (XI-15). This is the SECURITY gate's unfixed MEDIUM. Skip-marked.
- [LOW] F-2 TRUNCATE is accepted on all 21 billing tables (row triggers do not fire for it). XI-7, Skip-marked. (SECURITY gate LOW, confirmed.)
- [LOW] F-5 CardNumberGuard refuses ordinary numbers about one time in ten because any 13 to 19 digit run that passes Luhn is refused: a Bangladeshi mobile with country code (`bKash 8801712345605`) and our own document number when the branch code ends in digits (`INV-DHK001-2627-000123`). Staff are told it is a card number. XU-6, Skip-marked.
- [LOW] F-6 Parallel double-submit with one key can answer the loser with 400 (seen once: credit note, `201, 200 x4, 400`) instead of the winner's result, when the duplicate reads the aggregate after the winner committed; the pre-check runs before the load and is not repeated, and the replay filter handles only the unique-index case. Never more than one record. Payments held in 15 of 15 rounds. XI-4 tolerates it.
- Test-quality fixes made (test code only): BillingPaymentEndpointTests.I_D11 was flaky (2 of 4 runs; identical OccurredOn from the non-ticking fake clock, row order undefined) and now selects rows by before-snapshot; RunDeliveryPassesUntilAsync now throws instead of returning silently (XI-17); the loyalty ledger test was added to the loyalty class because the loyalty setting is global to the shared database.
- Flakiness and weak-assertion notes are in the sheet (section "Flakiness and test-quality notes"). U-X6 is a two-shape regex and would not catch every rehydration; I grepped the Read handlers by hand and found none.

BACKLOG (MEDIUM and LOW, for docs/backlog/BIL.md; I did not create it because the branch has none and another gate may be writing it)
F-1, F-3, F-4 (MEDIUM); F-2, F-5, F-6 (LOW); plus the gaps table in the sheet.

Production defects were reported, not fixed. Phase J is DEFERRED, not failed.
