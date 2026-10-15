GATE: CODE_REVIEW
HANDOVER: HO-POR-001
COMMITS: a45a85e8 (branch ho/POR-001; reviewed merge-base master..ho/POR-001: 68e0be0f 2cf203e0 9b1fd21b 05ebee45 f1cae602 55339e38 a0127b48 a45a85e8)
DATE: 2026-10-10
STATUS: PASS

Reviewed against HO-POR-001 incl. the Amended rows (BRD/SUB deferred, gates after Phase D, portal re-order retired, EMR FollowUpOn and Orders TrackingNumber/CanReorder additive). Application.Tests Portal filter re-run: 46 passed.
Checked and found correct: composer is the single catch site, rethrows OperationCanceledException, Unavailable carries null data; sections run strictly sequentially (shared DbContext, F36/ADR-267), no Task.WhenAll; limits and ordering (5 items, 12 cats +1 for hasMore, orders newest first, balances oldest due first); no DateTime.UtcNow, generatedOn from TimeProvider, appointments/orders carry instants + branch time zone id, visit date is EMR's branch-local date; null handling (EMR null cat skipped, null vaccination -> Unavailable, missing profile -> Unavailable); controller injects IInMemoryDispatcher + IUserContextProvider only, [RequireCustomerSession], 200, no-store; no AddServiceBusProvider/forbidden superseded types; no re-order/stays/subscriptions code left; cross-area edits additive (CustomerOrderRules single source, FollowUpOn, TrackingNumber) and covered by real-HTTP tests; CONTRACTS rows 30/31 match the code; TC sheet rows are backed by real tests.

FINDINGS
- [MEDIUM] src/Read/CatEcommerce.Read/QueryHandlers/Portal/Sections/VisitsSectionReader.cs:40 — N+1 across cats (<=12 sequential calls), each loading up to 200 visits + problems to keep one; backlog POR 1.
- [LOW] CatsSectionReader/VisitsSectionReader — duplicate GetCatsQuery per dashboard read; backlog POR 2.
- [LOW] VisitsSectionReader.cs:40 — one cat's visit failure makes the whole visits section Unavailable; backlog POR 3.
- [LOW] PortalModule.cs:11, VisitsSectionReader.cs:16 — stale comments (re-order "blocked", "no followUpOn"); backlog POR 4.
- [LOW] docs/test-cases/TC-HO-POR-001.md — header/Environment/"Not run" lines pre-date the finalise; backlog POR 5.
- [LOW] tests/.../PortalReaderTests.cs:343,379 — U-B13 per-status test asserts Orders' rule, not the reader; private copy of rule in test; backlog POR 6.
- [LOW] CatsSectionReader.cs:53 — null vaccination status silently becomes Unavailable without a log; backlog POR 7.

SCOPE CHECK
- files in allowed scope: 43/43 (portal files plus the additive cross-area edits authorised by the 2026-10-10 Amended row: EMR DischargeSummary*, Orders OrderViewModels/OrderQueryHandlers, ADR, CONTRACTS, BIL backlog row, sibling handover Amended rows)
- files outside allowed scope: none
