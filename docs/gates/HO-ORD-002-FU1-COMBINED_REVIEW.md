GATE: COMBINED (ARCHITECTURE + SECURITY + CODE_REVIEW)
HANDOVER: HO-ORD-002-FU1
COMMITS: 5a187b6b..f7f9fb65
DATE: 2026-10-14
STATUS: PASS

FINDINGS
- [LOW] web/lib/hooks/useStaffOrders.ts:70 — query key ['staff','customer-orders',...] is not under the ['staff','orders'] prefix that the order mutations invalidate (useStaffOrders.ts:147, useStaffReturns.ts:132). After a status change the panel shows the old status until it refetches on its own. Proposed ORD row 105: key it ['staff','orders','customer',...] or invalidate it too.
- [LOW] web/components/orders/CustomerOrdersPanel.tsx:46 — a 403 from a refused branch (not only a missing Orders.View) hides the list but still offers the counter sale, which the same branch refusal will probably block. Wording says "role cannot", which can be wrong. Proposed row 106: neutral wording ("You cannot view order history here").
- [LOW] web/mocks/handlers/ordering/staff.ts:388 — the mock answers 200 with an empty page for an unknown customer id; the real endpoint answers 404. The 404 path is covered only by a server.use override. Proposed row 107: have the mock return 404 for ids that are not a known customer (the "no-orders" test id then needs a real customer without orders).

RULINGS
- Contract fit: StaffCustomerOrderRow = StaffOrderRow minus customerName/ageMinutes/flags matches StaffCustomerOrderRowViewModel (id, orderNumber, placedOn, status, fulfilmentMethod, paymentMethod, channel, grandTotal, branchTimeZoneId). PagedResult paging as built (pageNumber/pageSize). Query is keyed by active branch, matching the controller's active-branch scope. Controller route is {customerId:guid}; the URL is built with encodeURIComponent.
- Hook/client: same useBranchScope / enabled:ready / keepPreviousData / noRetryOnRefusal pattern as useStaffOrderQueue and ORD-002 clients. Matches.
- 403: copying BillingSummaryPanel is the right precedent in kind (the panel sits on a page the role can otherwise use, so no full-page refusal). Showing a sentence instead of returning null is better, because an empty gap would look like a bug. See LOW 106 on the wording.
- Paging vs design: acceptable. The design's "5 most recent of 14" has no backend equivalent, and the endpoint pages. 5 per page with a pager is a superset of the design and is documented in the handover section 5. Paging resets are fine because the panel is mounted per customer page.
- PII: the endpoint's row has no customer name, phone or address. Nothing more is rendered than order number, date, status and total. The customer id in the URL is already the page's own id. No leak. Handover notes row 101 (closed/anonymised accounts) honestly.
- Tests: 6 vitest cases cover list and not-queue, loading, empty, error and retry, 403, 404 and paging. The panel file ran green here (36/36). Playwright runs against MSW only, which the handover states openly. It proves wiring, not the contract; the backend FU2 tests own the contract. Acceptable.
- Records: handover matches the diff. ORD row 82 was appended to (original text intact), not rewritten. Scope is frontend only; no .ai/ or backend changes.
- Not committed: web/node_modules (junction) and web/.env.local are both gitignored (.gitignore:29, :34) and absent from the range.

SCOPE CHECK
- files in allowed scope: 8/8
- files outside allowed scope: none

PROPOSED docs/backlog/ORD.md ROWS (after 104)
| 105 | LOW | Customer orders panel query key is outside the ['staff','orders'] invalidation prefix; its status goes stale after an order mutation. | HO-ORD-002-FU1 COMBINED |
| 106 | LOW | Panel 403 wording blames the role; a refused branch also yields 403 and likely blocks the counter sale too. | HO-ORD-002-FU1 COMBINED |
| 107 | LOW | MSW customer-orders mock returns an empty 200 for an unknown customer; the real endpoint returns 404. | HO-ORD-002-FU1 COMBINED |
