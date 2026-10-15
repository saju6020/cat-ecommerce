# HO-ORD-002-FU1 — Customer page Orders panel on the real endpoint (frontend follow-up)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-ORD-002-FU1** (follow-up slice to [`HO-ORD-002`](HO-ORD-002.md); consumes [`HO-ORD-001-FU2`](HO-ORD-001-FU2.md)) |
| Date | 2026-10-14 |
| Branch | `ho/ORD-002-FU1` from master `5a187b6b` |
| Status | Frontend: Complete. Not merged |

## 2. Objective

Close the frontend half of backlog ORD row 82. ORD-002's gate-fix round left `CustomerOrdersPanel` as a placeholder ("not available yet") because `GET /api/staff/customers/{customerId}/orders` did not exist. HO-ORD-001-FU2 built it (CONTRACTS row 35); this slice lists the customer's real orders in the panel.

## 3. Scope

Frontend only. No backend, no migration, no contract change.

- `web/lib/api/staff-orders.ts` (`StaffCustomerOrderRow`, `listStaffCustomerOrders`)
- `web/lib/hooks/useStaffOrders.ts` (`useStaffCustomerOrders`)
- `web/components/orders/CustomerOrdersPanel.tsx`
- `web/mocks/handlers/ordering/staff.ts` (mock of the endpoint, matched by customer id)
- `web/tests/components/orders/staff-orders.test.tsx`, `web/tests/e2e/ordering-staff.spec.ts`

## 4. What was done

- **Shape as built** (`StaffCustomerOrderRowViewModel`): `{ id, orderNumber, placedOn, status, fulfilmentMethod, paymentMethod, channel, grandTotal, branchTimeZoneId }` in `{ items, pageNumber, pageSize, totalCount }`. It has no `customerName`, `ageMinutes` or `flags`, so the row type is the queue row without those three.
- **Panel** (design `docs/design/orders/CustomerOrdersPanel.dc.html`): table of Order (link to `/staff/orders/{id}`), Placed (branch time zone), Status badge, Total; the counter-sale button kept; a "See all in Orders" link. The query is keyed by active branch like the other staff order reads.
- **States:** loading skeleton (also while the branch is not yet resolved); empty ("No orders yet."); error with retry (shared `ResponsiveTable`); `403` replaces the list with a plain sentence and keeps the counter-sale action; `404` says the customer was not found.
- **Paging:** the endpoint pages, so the panel pages with the server, 5 per page, through the shared pager (shown only when there is more than one page).

## 5. Adaptations to report (design vs endpoint)

- The design shows "5 most recent of 14" with no pager; the endpoint pages with no "recent" cut-off, so the panel shows 5 per page with Previous/Next and the total.
- The endpoint lists only the **active branch's** orders (CONTRACTS row 35). The panel description says "at the active branch". Orders of the same customer at another branch are not shown; a staff member with several branches sees them after switching.
- A `403` (no `Orders.View`, or a refused branch) does not use the full-page refusal state, because the panel sits on a page the role can otherwise use; it hides only the list, as `BillingSummaryPanel` does.
- ORD row 101 (FU2): a closed or anonymised account still answers 200 with its branch orders; the panel renders whatever it gets.

## 6. Tests

- vitest (`staff-orders.test.tsx`, 6 panel tests): own orders listed from the customer endpoint and the queue is never called; loading then empty; error then retry; `403`; `404`; paging asks for page 2.
- Playwright (MSW, chromium, `ordering-staff.spec.ts`): the customer page lists orders and a link opens the order. Run with `--workers=1`, `PORT=3115`, `NEXT_PUBLIC_USE_MOCKS=true`. It was not run against the real API/DB (`catecommerce_ordfu1e2e`, port 5115): the slice is frontend-only and a real-API run needs a built API host, which was too heavy while other builds run.
