GATE: CODE_REVIEW
HANDOVER: HO-BIL-001
COMMITS: 50f1ab1..ffb70cc (branch ho/BIL-001; phases A-I, phase J deferred by owner decision)
DATE: 2026-10-07
STATUS: PASS

VERIFIED
- dotnet build: 0 warnings, 0 errors. Unit projects: Domain 908/908, Application 235/235, Infrastructure 147/147 pass. Integration tests not run, no database touched (as briefed).
- Money arithmetic (InvoiceCalculator): decimal throughout, per-line tax AwayFromZero, invoice tax = sum of line taxes, largest-remainder apportionment of the loyalty discount, credit at the frozen line rate with the remainder taking the residue so totals tie out. Credit/refund/allocation limits hold in the aggregates (Unallocated invariant, Outstanding cap, ApplyCredit releases overpayment first). Ageing buckets, cash-up expected cash and loyalty accrual/reversal are correct against the spec.
- Time: every client date is a DateOnly (no client DateTimeOffset reaches timestamptz); business day, financial year, cash-up pending-approval window and export allocation dates are derived from the branch zone, with a UTC window only as a widened pre-filter.
- Numbering: series row tracked and committed in the same save as the document, xmin-guarded; retry reloads only the series row, detaches the failed attempt's message and audit rows; in-process gate is documented as an optimisation, xmin plus the unique backstops are the guard, so a multi-instance deployment stays correct. Idempotency unique violations are deliberately not retried and are answered by BillingIdempotencyReplayFilter.
- Triggers (BillingMigrationSql): status values written from Enumeration.Value, bookkeeping columns exempted, deletes refused, closed cash-up terminal. No float/double or timestamp-without-zone in the Billing mappings.

FINDINGS (no BLOCKER, no HIGH; backlog list below)
- [MEDIUM] src/Read/CatEcommerce.Read/QueryHandlers/Billing/ReceivablesQueryHandlers.cs:100-117 and Domain/DomainServices/AgeingCalculator.cs ByCustomer — ageing pages are cut from `OrderBy(OldestDueOn)` over an un-ordered SQL result with no customer-id tiebreak, so customers sharing an oldest due date can repeat or vanish between pages. Add `.ThenBy(r => r.CustomerId)` (and an ORDER BY on the row query).
- [MEDIUM] tests/CatEcommerce.IntegrationTests/Endpoints/BillingLoyaltyEndpointTests.cs:17-24 (and each try/finally) — loyalty is a global setting flipped on and off on the shared database; test classes run in parallel, so other Billing tests execute with loyalty on for the window, and a killed run leaves it on. Put the class in a non-parallel collection (or have the base reset loyalty-off at start-up) so the setting is never observed half-way.
- [MEDIUM] tests/CatEcommerce.IntegrationTests/BillingIntegrationTestBase.cs:374-382 RunDeliveryPassesUntilAsync — silently returns when maxPasses is exhausted; the failure then surfaces as an unrelated assertion, and any negative assertion placed straight after it would pass vacuously. Throw (Assert.Fail) on exhaustion.
- [LOW] src/Application/.../CommandHandlers/Billing/CashUpCommandHandlers.cs:~135 — first count of a day creates the deterministic-id aggregate with SaveAsync; two simultaneous first counts hit a primary-key violation. Confirm it maps to 409 rather than 500, or retry as an update.
- [LOW] src/Application/.../CommandHandlers/Billing/PaymentCommandHandlers.cs (Approve, `branch?.TimeZoneId ?? "UTC"`) — a missing branch silently falls back to UTC for the business date; Record and the other handlers return BranchNotFound. Return the violation instead.
- [LOW] src/Domain/.../DomainServices/InvoiceCalculator.cs CreditByQuantity/CapToLine — on very small lines, per-quantity tax rounding can sum above the line tax and refuse a partial credit with CreditExceedsLine though quantity remains (crediting the whole remainder still works). Document or clamp the tax to the remaining line tax.
- [LOW] src/Api/CatEcommerce.Api/Filters/BillingIdempotencyReplayFilter.cs — in the parallel-loser path the stored result is returned without the "same target" check the handler's pre-check does (IdempotencyKeyConflict). Compare the invoice/payment/customer id before answering 200.
- [LOW] src/Read/.../QueryHandlers/Billing/PaymentQueryHandlers.cs:110 — `Entries.OrderBy(OccurredOn)` has no tiebreak; add `.ThenBy(e => e.Id)`.
- [LOW] src/Infrastructure/.../Billing/BillingMigrationSql.cs invoice-line insert trigger — the same-transaction test compares `xmin` with the top-level xid, so it fails inside a savepoint. Fine with today's EF usage; note it next to the SQL.

SCOPE CHECK
- files in allowed scope: all changed src/tests/docs files in the Billing handover
- files outside allowed scope: none identified (262 files changed; 9 are generated migration Designer files and the model snapshot)
