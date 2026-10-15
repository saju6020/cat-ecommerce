GATE: ACCEPTANCE
HANDOVER: HO-ORD-002
COMMITS: 12543d76 5ef10b1a 1239ce15 62e1bac7 b22c41b9 3d4ea5be ef626a68 cc1c2d59 f4329fe4 05565241 91b6a09c 1da9a352 7eb7504b 0767caa7 b0110078 (code and docs; branch ho/ORD-002-build, range a8761829..c9bc23fd); gate records 97b8c8ac 0aaf8560 de6cf1e6 64fa94ce c9bc23fd
DATE: 2026-10-13

> **Re-check 2026-10-13 over f1362a3a..27ddd341: PASS.** A30 is now MET. The section "Re-check (f1362a3a..27ddd341)" at the end of this file supersedes the first-pass verdict, the A30 row and the HIGH below. The first-pass text is kept as the historical record.
STATUS: PASS (re-check at 27ddd341; first pass f1362a3a was FAIL)

First pass. This gate read:
- all of HO-ORD-002, including the §1 Amended rows of 2026-10-09, 2026-10-10 (two) and 2026-10-12;
- the four prior gate records and `docs/test-cases/TC-HO-ORD-002.md`;
- the tests and source cited for each §18 row (listed per row below).

Owner rule (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings are proposed below as rows for `docs/backlog/ORD.md` (highest existing row: 92). This gate wrote only this file.

MSW-only evidence is judged as in the HO-SHP-002, HO-ADM-002, HO-FIL-002, HO-NTF-002 and HO-APT-002 ACCEPTANCE records:
- a criterion is MET on MSW e2e plus unit evidence, or on source this gate read;
- the real-API half is carried as a condition (C-1), never counted as a pass.

## Verdict

**FAIL, on one criterion: A30 (UI-6 / UI-1).** E-X1 and E-X2 never visit `/checkout` or `/checkout/complete/[orderId]`. So the checkout, the changes panel and the bank-transfer page have no axe result and no 360-1440 px result. The TC sheet and the TEST record both say "every route". Every other criterion is MET, or MET with a carried condition.

The fix is a test change only, with no product code (see "To close").

## PRIOR GATES (read from docs/gates/ and docs/test-cases/, not from commit messages)

- ARCHITECTURE: first pass FAIL (97b8c8ac: 5 BLOCKER, 1 HIGH). The combined re-check, appended to `HO-ORD-002-ARCHITECTURE.md` (64fa94ce), is PASS. It closes B1 (owner ratified), B2-B5 (reverted), the ARCH HIGH (customer-orders panel) and the CODE_REVIEW HIGH (queue date range). It adds 6 LOW (ORD 84-89).
- SECURITY: PASS (0aaf8560), 1 LOW. That LOW (staff tracking link without `safeHttpUrl`) is closed in the combined re-check, and TEST R10 confirms it.
- CODE_REVIEW: first pass FAIL (de6cf1e6: 1 HIGH, 7 MEDIUM, 4 LOW).
  - The HIGH and the MEDIUMs are closed in the combined re-check, and TEST R1 and R3-R8 confirm this.
  - Under the gate scope rule (follow-ups get one combined review), that re-check is the CODE_REVIEW result.
  - Paper-trail gap, LOW (row 98): `HO-ORD-002-CODE_REVIEW.md` still ends in `STATUS: FAIL` and does not point to the re-check.
- TEST: PASS (c9bc23fd), run at head 64fa94ce. Sheet section "TEST gate execution":
  - 21 rows: 20 PASS, 0 FAIL, 0 BLOCKED, 1 NOT RUN (T-11 real API, owner rule);
  - tsc, eslint and build clean; vitest 203 files / 1369 tests;
  - e2e chromium 10/14/5/31, mobile-360 31.
- No product code has changed since 64fa94ce. Commit c9bc23fd touches only the TC sheet, the TEST record and `ORD.md`.

## VERIFICATION RUN BY THIS GATE (web/, worktree at c9bc23fd)

- `npx vitest run tests/guards tests/components/{cart,checkout,orders,fulfilment,returns,refunds,pos} tests/unit/lib/ordering-next-path.test.ts tests/unit/lib/idempotency-renewal.test.ts --maxWorkers=2`: 12 files, 137 tests, all passed.
- No Playwright and no full suite (memory constraint). The e2e evidence below is the TEST record's, checked against the spec source.
- `git diff a8761829..HEAD` is empty for `ShopHeader.tsx`, `ShopAccountLinks.tsx`, `billing/ResponsiveTable.tsx`, `cat-profiles.ts`, `public/mockServiceWorker.js` and `package.json`.
- The sign-in diff is exactly the `'/cart', '/checkout'` allow-list entries plus a comment.

## CRITERIA (HO-ORD-002 §18)

| # | Result | Evidence |
|---|---|---|
| A1 | MET (MSW) | E-A1 (`ordering-customer.spec.ts:35`) PASS (T-5): a guest adds two items and signs in; `/cart` shows the merge notice, exactly 2 list items, and a re-add raises the quantity to 2 instead of adding a line. Source `useCart.ts:46-54`: the merge fires once (ref + `isMutating` guard) and the cart is refetched. U-A3 has no component test of its own; the TC cites E-A1 (LOW, row 93). Backend: HO-ORD-001 ACCEPTANCE A1 |
| A2 | MET (frontend half, source) / real-API carried (C-1) | E-A2 is NOT RUN, because a per-tab MSW cannot share state between contexts. Source `useCart.ts:29-34`: the cart is only `GET /api/cart` under `['cart']`, and nothing in `lib/hooks/useCart.ts`, `lib/api/cart.ts` or `components/cart/**` touches browser storage (SECURITY confirms the same for the whole diff). So a second device shows whatever the server holds. That property is evidenced by HO-ORD-001 ACCEPTANCE A2 (`I_C3_TheSameCustomerOnASecondDevice_SeesTheSameCart`) |
| A3 | MET (MSW) | E-B2 (`:107`) PASS: the price change is listed in the "What changed" region, Place order stays disabled until the tick, then the order is placed. Unit tests in `checkout.test.tsx`: `:275` (a new fingerprint clears the tick and announces), `:294` (Place disabled with the reason until acknowledged), `:383` (409 CheckoutQuoteChanged re-quotes, re-opens and shows one message). The availability half: the panel renders the server's sentence for every change kind (`ChangesPanel.tsx`), and the cart states limited and unavailable lines as text (`cart.test.tsx:59`). There is no checkout test with a non-price change kind (LOW, row 93) |
| A4 | MET (MSW) | E-B1 (`:74`): saved address, then a time window. E-B3 (`:129`): pickup branch, slot step skipped. `checkout.test.tsx:153,180,198`: levels and windows come from the server, and a level without windows marks the step as not needed |
| A5 | MET (MSW) | `cart.test.tsx:184`: a signed-in prescription Add to cart opens the cat dialog and posts nothing. E-B6 (`:209`) PASS: an unauthorised cat is disabled, the order is placed with "For Whiskers", and a lapsed authorisation is refused with the reason. Source: `ReviewStep.tsx:65` disables Place while `!quote.canPlace`, and `PrescriptionLinesPanel.tsx` shows and focuses a picker for a `CatRequired` line. The TC cites E-B6 for U-B9, but E-B6 never shows a cat-less line at review, and the `PrescriptionCatRequired` placement branch has no test (ORD-90). LOW, row 93 |
| A6 | MET (MSW) | `checkout.test.tsx:319,330`: six rows always present, zero rows included, server strings, code and level named. E-B1 asserts the six row labels in the review |
| A7 | MET | `no-money-arithmetic.test.ts:92,99` PASS (an AST scan, plus a self-test that it can fail). The OrderSummary test renders the server strings verbatim. E-X3 ran as a DOM/network check per route, not as a bundle scan. The guard covers the Ordering files only, not all of `web/**` as §12.3 says (undeclared deviation, LOW, row 96) |
| A8 | MET (MSW) | `checkout.test.tsx:338`: the reason is inline and the totals are unchanged. E-B5 (`:181`): "We don't recognise that code." and the minimum-spend reason, then "Order discount (WELCOME10)" |
| A9 | MET (MSW) | `checkout.test.tsx:153`: the server charge appears per service level before payment. E-B1: the "Delivery (Standard)" row appears in the review |
| A10 | MET (MSW) | E-B3: "We don't deliver to Gazipur yet. You can collect your order instead.", pickup and branch preselected, the pickup order placed |
| A11 | MET (MSW) | `checkout.test.tsx:231`: PaymentStep renders exactly the quote's options. E-B1 (COD), E-B3 (pay on collection), E-B4/E-C1 (bank transfer) |
| A12 | MET (MSW) | E-B4 (`:150`): COD is disabled, with "Cash on delivery is available for orders up to BDT 5,000.00". `checkout.test.tsx:231` renders the server message on disabled options. U-B3's "three distinct reasons" relies on that generic rendering; no test feeds it three reasons (LOW, row 93) |
| A13 | MET | `no-card-ui.test.ts:29,36,50` PASS: no `cc-*` in app/ or components/, no card identifier in Ordering files, and a self-test. `checkout.test.tsx:221`. E-B1 `:90`. The a11y spec checks every visited route for `cc-*` / card inputs and for gateway hostnames. POS "Card in store" takes the amount and the slip reference only |
| A14 | MET (MSW) | `my-orders.test.tsx:118` (instructions, reference, copy buttons, countdown), `:165` (6 h, 1 h, expiry only), `:190,209,220`. E-B4/E-C1: "Pay by bank transfer", the reference, the `timer` region, Copy, then the reference submitted and the thanks shown |
| A15 | MET (MSW) | E-C1 staff (`ordering-staff.spec.ts:113`): the confirm restates "...received against ORD-2026-000102?" and is confirmed. E-C3 (`:125`): the recorder's approval is refused with "Someone else must approve a payment you recorded.", then another user approves and the hold clears. The `awaitingApproval: true` message is source-only (`AwaitingPaymentList.tsx:130-131`, `StaffOrderDetail.tsx:150-151`); E-C1's assertion accepts either outcome text (LOW, row 93) |
| A16 | MET (MSW) | E-C2 (`:246`) PASS: "This order has expired", the explanation, Order again, "1 item was added", the cart shows the line. The expiry state is inferred on the client (ARCH MEDIUM, ORD-63, open) |
| A17 | MET (MSW) | E-B1 staff (`:24`): the amount is pre-filled, the rider's name is entered, the confirm restates it, and the Money region shows the collector, then Delivered. `:51`: a missing rider's name blocks the review. Source `RecordDeliveryDialog.tsx:179,197`: "Delivered on" and "Cash collected on" are required `datetime-local` fields, sent as branch-zone instants |
| A18 | MET as amended (MSW) | E-D3 (`:61`): failure recorded, quarantine sentence, a return opened, "Received into quarantine", no restock or write-off control. `staff-orders.test.tsx:164,172,179`. "Written off" in E-D3 no longer applies (Amended 2026-10-09 (1)); §18 was not updated (LOW, row 97) |
| A19 | MET (MSW) | `ordering-staff.spec.ts:138`: an empty note is refused ("Enter a reason."), then resolved, then "No open issues." The entered details are listed (`ReconciliationIssuesList.tsx`). There is no component test (U-D9 is e2e only) |
| A20 | MET (MSW) | `staff-orders.test.tsx:44`: a 30 s refetch keeps the typed dialog values and updates the page behind. E-B1 staff: no "Record delivery" on a confirmed order; actions appear only as the server allows them. Source: `StaffOrderDetail` action bar built from `allowedActions` (ARCH verified, ADR-FE-093). U-D1 has no test of its own (LOW, row 93) |
| A21 | MET (MSW) | `fulfilment.test.tsx:25,40,51,63`. E-E1 (`:75`): level-2 location headings, a Batch column, no links on the sheet, then Start packing moves an order to "Being packed". Packing slip e2e (`:102`): the COD amount and the Batch column, no navigation |
| A22 | MET (MSW + source) | Stored and shown: E-B1 staff (`PC-5550001` in the Shipment region), `my-orders.test.tsx:263,276,281`, E-D1 (tracking link and number). Manifest: E-E2 (`:90`) downloads `manifest-*.csv`; the content belongs to the server. Later entry is **source only**: `ShipmentPanel` "Enter tracking number" → `TrackingDialog` → `useRecordTracking` → `PUT /api/staff/orders/{id}/tracking`, which matches `StaffOrdersController.cs:202` and HO-ORD-001 §12.4. No test exercises it (LOW, row 94) |
| A23 | MET (MSW) | E-D1 (`:272`): "Cancel order ORD-2026-000104?", "Your reserved items are released.", Cancelled; a packed order has no Cancel and shows "Contact the shop". `my-orders.test.tsx:241,251` (the refund sentence only when paid) |
| A24 | MET (MSW) | `my-orders.test.tsx:303,316,335`: policy text, the server 400 shown on its line. `returns.test.tsx:60,74,92,98`: Approve gated on the defect and note. E-D2 (`:151`) staff half. The customer's opened-food refusal is the line-level 400 test, not an e2e |
| A25 | MET as amended (MSW) | `returns.test.tsx:116,126,143`: quantities up to the approved amount, the quarantine sentence. E-D2: "Received into quarantine", no restock or write-off button, a link to the Inventory batches screen. The per-line restock / write-off decision left Ordering (Amended 2026-10-09 (1)), and U-E5* replaces U-E5. §18 not updated (row 97) |
| A26 | MET (MSW) | `refunds.test.tsx:40,52,63,97,103`: allowed methods only, the server's suggestion, the confirm restating method and amount. E-D2: "Store credit isn't available yet.", cash in store with a reference and reason, then the refund issued |
| A27 | MET (MSW) | E-F1 (`ordering-pos.spec.ts:33`): three scans, F2, amount, Enter, Complete sale focused, Enter, change due, Print receipt focused, receipt sheet; `Date.now() - began < 60_000` is asserted; plain text available. Also slow typing does no lookup (`:75`), an unknown barcode is announced (`:67`), E-F2 (`:89`), double Enter makes one sale (`:108`). `pos.test.tsx:57-131,179-279,327,346`. The `scan()` helper refocuses `#pos-scan` before each scan, so E-F1 does not prove focus is kept between scans (LOW, row 95) |
| A28 | MET (MSW) | E-B1/E-B8: a double click creates exactly one new order (ORD-2026-000200 present, 000201 absent). POS `:108`: one POST on a double Enter. `pos.test.tsx:327` (one key), `:346`. `checkout.test.tsx:451` (same key on retry after RequestInProgress), `:478` (new key after refusal). `idempotency-renewal.test.ts` |
| A29 | MET (MSW) | `staff-orders.test.tsx:44` (above). The POS basket is component state; `pos.test.tsx:138,152` cover the unsaved-basket guard |
| A30 | **NOT MET: no evidence for the checkout routes** | `ordering-a11y-responsive.spec.ts:16-50` lists 24 staff and 7 customer routes. **`/checkout` and `/checkout/complete/[orderId]` are not among them.** These are §8.1 routes, and E-X1 names "the changes panel" explicitly. The customer flow specs run on the `chromium` project only (desktop viewport) and never run axe. So neither page has any axe result, and neither has been rendered at 360, 768 or 1024 px by any test (ORD-92 confirms the flow specs never ran at 360 px). E-X1's "POS with a scan in progress" is not covered either: `/staff/pos` is scanned only while idle. The covered routes PASS: 31/31 on chromium and on mobile-360 (T-8, T-9); axe is checked at 360 and 1440. `/cart` excludes the Shop-owned `<header>` (owner deferral, ORD-81) |

To close A30, change tests only:
- Add these to the a11y/responsive spec:
  - `/checkout?step=review` with a seeded cart and a price change (the changes panel visible);
  - `/checkout/complete/{orderId}` for a bank-transfer order (the countdown);
  - `/staff/pos` after one scan.
- Run the spec on chromium (and mobile-360).
- Record the rows in the TC sheet.
- If a 360 px overflow or a serious/critical violation appears, it is a product defect for this handover.

## RULINGS REQUESTED BY THE INTEGRATOR

**Criteria that depend on the customer-orders endpoint or the header cart badge: deferred by owner decision, not unmet.**
- Neither feature is a §18 criterion.
- The customer-orders panel is §11.14 only:
  - the owner chose "Build the endpoint" (Amended 2026-10-12 (3));
  - the backend is on `ho/ORD-001-FU2`;
  - the panel honestly says "Order history for a customer is not available here yet" and makes no queue request (`staff-orders.test.tsx:271`, TEST R2);
  - ORD-82 tracks it.
- The storefront header badge (§3.2/§3.3) belongs to a Shop file the owner declined (Amended 2026-10-12 (2)), and ORD-81 tracks it.
- A1 and A2 do not depend on the badge. `/cart` is reachable through the "View cart" link (`AddToCartButton.tsx:123`) and through the `CartBadge` in the `(portal)` header.
- `CartBadge` itself has no test of its contract ("hidden at 0"). LOW, row 93.
- Owed after FU2 merges: a list test for the panel, and an e2e on the real API.

**Real-API column stays NOT RUN: accepted under the owner's rule, and counted as nothing.**
- It is carried as condition C-1, as in the HO-SHP-002 and earlier frontend records.
- No criterion here is MET on real-API evidence.
- A2 rests on frontend source plus the backend's own I_C3 test. It is not a frontend run.

**E-series cold-start flakiness (ORD-91): LOW, not blocking.**
- Every failure was "page or mock not ready" on the first visit after a dev-server start. No behavioural assertion failed.
- Each affected case later passed with no retry. E-B6 passed 3 times; E-F1 and E-D2 each passed in a clean run. T-5 to T-9 all have a no-retry run.
- Two caveats keep it on the backlog:
  - the E-B6 signed-out header was never explained;
  - spec-level `retries: 1` overrides `--retries=0`, so future runs can hide a real first-attempt failure.
- I agree with ORD-91 as written.

## CONDITIONS (carried, not counted as passes)

- **C-1 Integrator. Real-API E-series (§5, §16, §17 step 11), all NOT RUN** (owner rule: nothing against `catecommerce_dev` until cleanup). Owed:
  - every E row on the real API;
  - E-A2 and E-B7, which need real shared state and parallelism;
  - E-C1's inbox message;
  - E-B2 with a real price change;
  - E-C2 on the worker cycle.
- **C-2 Integrator.** The customer-orders list (ORD-82) after `ho/ORD-001-FU2` merges.
- **C-3 Shop follow-up.** The storefront header badge and the `/cart` header overflow at 360 px (ORD-81; mirror to SHP.md per ORD-87).
- **C-4 Merge.** The branch is based on a8761829. Master is now at b12fd39d. Re-run tsc and the ordering suites after the merge.

## FINDINGS

- [HIGH] web/tests/e2e/ordering-a11y-responsive.spec.ts:43-51 — §18 A30 / §16 E-X1, E-X2 require axe and 360-1440 px on every §8.1 route, "including the changes panel, the countdown, the POS with a scan in progress". The spec omits `/checkout` and `/checkout/complete/[orderId]`, and only checks `/staff/pos` idle.
  - TC E-X1 and E-X2, and TEST T-8 and T-9, record PASS for "every route".
  - The core customer page of this handover therefore has no accessibility or responsive evidence.
  - Fix: tests only, as in "To close" above.
- [LOW] Carried, not re-raised: ARCH MEDIUM ORD-63 (client-inferred expiry), ORD-64 (checkout invalidations in the component) and the LOWs ORD-65, 68, 84-92.

## PROPOSED BACKLOG ROWS (docs/backlog/ORD.md; the highest existing row is 92)

| # | Sev | Item | Source |
|---|---|---|---|
| 93 | LOW | Tests cited in the TC sheet through e2e, with no unit test of their own: U-A3 (merge fires exactly once), U-B5 (a non-price change kind in the changes panel), U-B9 (a cat-less Rx line at review: picker focused, Place disabled), U-B3 (three distinct COD reasons), U-D1 (actions only from `allowedActions`), U-D7 (the `awaitingApproval: true` message; E-C1 accepts either text), `CartBadge` (hidden at 0, count). Add focused component tests. | HO-ORD-002 ACCEPTANCE |
| 94 | LOW | Later tracking entry (`ShipmentPanel` → `TrackingDialog` → `PUT .../tracking`, US-ORD-11 AC2) has no test. Add a component test, and extend E-B1 to dispatch without tracking and enter it afterwards. | HO-ORD-002 ACCEPTANCE |
| 95 | LOW | E-F1's `scan()` helper calls `#pos-scan.focus()` before each scan, so the NFR-USA-2 "without leaving the scan field" claim is not shown. Assert `#pos-scan` is focused after each scan instead of refocusing. | HO-ORD-002 ACCEPTANCE |
| 96 | LOW | Undeclared narrowing: §12.3 says the card-identifier scan covers `web/app/**` and `web/components/**`, and the money-arithmetic scan covers `web/**`. `tests/guards/ordering-scope.ts` limits both identifier and arithmetic scans to the Ordering files; only `cc-*` is scanned app-wide. Sensible (Inventory's "expiry date" would trip rule 1), but record it in an Amended row or in ADR-FE-092. | HO-ORD-002 ACCEPTANCE |
| 97 | LOW | Handover §18 A18 ("written off"), A25 ("per-line restock / write-off decision") and §16 E-D2 / E-D3 still describe the restock and write-off that Amended 2026-10-09 (1) removed from Ordering. Update §18 so later readers do not read them as unmet. | HO-ORD-002 ACCEPTANCE |
| 98 | LOW | `docs/gates/HO-ORD-002-CODE_REVIEW.md` still ends `STATUS: FAIL`. Its closure lives in the combined re-check appended to `HO-ORD-002-ARCHITECTURE.md`. Add a one-line pointer to the CODE_REVIEW (and SECURITY) records. | HO-ORD-002 ACCEPTANCE |

## OUT-OF-SCOPE CHECK (§4, §14, §20)

- §4: none found.
  - No card, wallet or gateway UI: the guard scans pass, and the routes visited are checked for gateway hostnames.
  - No backend change: the diff has no `src/**` or `tests/**` paths.
  - No new dependency: `package.json` unchanged.
  - Not built: subscriptions, the portal dashboard, a combined invoice (US-ORD-17, deferred as §18 names it), offline POS, and any PDF, scanner or grid library.
- §19 ACCEPTANCE focus:
  - US-ORD-17 deferral: recorded, nothing built.
  - OQ-1: the `(shop)` group exists as assumed. `/cart` is at `app/(shop)/cart`. `AddToCartButton` is mounted in `ProductPurchase.tsx` under HO-SHP-002's slot delegation (Amended 2026-10-12 (4)). The header `CartBadge` mount is deferred (C-3), not passed silently.
- §20:
  - §20.4 / §20.5 (customer-orders endpoint) was reported, not shimmed: ORD-82 and Amended (3).
  - Wire and contract changes are recorded in the 2026-10-09 and 2026-10-10 Amended rows.
  - ARCH MEDIUM ORD-63 (expiry inferred from history, §20.2 spirit) is open and tracked. It is not hidden.

## SCOPE CHECK

- files in allowed scope: 165/166 on the whole branch against a8761829.
  - 143 web files match §13's original globs; the 16 other web files are listed below.
  - 7 are docs: the TC sheet and 4 HO-ORD-002 gate records (§13), plus the `docs/handover/HO-ORD-002.md` Amended rows and `docs/backlog/ORD.md`, both under the owner and integrator recording practice.
  - Allowed by the 2026-10-12 Amended row:
    - `web/app/(auth)/sign-in/page.tsx` (owner-approved line, (1));
    - `ProductPurchase.tsx`, `product-page.test.tsx`, `TaxClassForm.tsx`, the `(portal)/layout.tsx` CartBadge, `nav/areas/orders.ts`, `adminNavItems.ts`, `tests/e2e/ordering-helpers.ts`, `tests/unit/lib/ordering-next-path.test.ts` ((4)).
  - Other in-scope files: `lib/api/client.ts` (the idempotencyKey option only), `errors.ts` (append), `lib/format/orders.ts`, `mocks/handlers/index.ts`, the customer page mount and `web/docs/decisions.md`.
- files outside allowed scope: `web/tests/unit/lib/idempotency-renewal.test.ts`, a new ordering-only test outside the amended glob. Already LOW, ORD-86. The five reverted files equal master.

## UNDECLARED DEVIATIONS

- The guard scans are narrowed to the Ordering files (row 96, LOW).
- The TC sheet and the TEST record claim E-X1 and E-X2 for "every route of §8.1", but the checkout routes are absent (the HIGH above). This is a misstatement in the evidence, not in the product.
- The TC sheet attributes U-B9, U-D1, U-D4 and U-D7 to e2e cases that do not assert those behaviours (U-B9, U-D7) or assert them only in part (U-D1, U-D4). Row 93. The criteria they serve are still evidenced elsewhere, as cited above.

---

## Re-check (f1362a3a..27ddd341)

GATE: ACCEPTANCE (re-check of the A30 fix)
HANDOVER: HO-ORD-002
COMMITS: 27ddd341 (one commit, 9 files)
DATE: 2026-10-13
STATUS: PASS

### What changed

- `web/tests/e2e/ordering-a11y-responsive.spec.ts`:
  - adds a helper, `auditViewports`, that runs the same checks as the main loop: overflow at 360, 768, 1024 and 1440; axe at 360 and 1440; the card-field check;
  - adds three set-up tests:
    - `/checkout?step=review` after a `setPrice`, asserting the "What changed since you added these items" region and Place order at every width;
    - `/checkout/complete/{id}` of a newly placed bank-transfer order (the path is asserted to be a GUID), asserting the heading and the `timer`;
    - `/staff/pos` after one scan, asserting one basket line and that `#pos-scan` keeps focus.
- Product fixes, all within §13 (`components/cart/**`, `components/checkout/**`):
  - `CartBadge.tsx`: the word "Cart" is hidden below 640 px. The icon and count stay, and the `aria-label` is unchanged.
  - `CartLineRow.tsx` and `CheckoutStepper.tsx`: `text-ink-faint` becomes `text-ink-muted` (contrast).
- `staff-orders.test.tsx`: two tests for entering tracking later.
  - The first asserts that an empty number is refused before anything is sent, then `PUT .../tracking` with the trimmed number, the panel showing it, and "Change tracking number".
  - The second asserts that a server refusal shows its message, the typed value is kept, and no error code appears.
- Docs:
  - the TC sheet's E-X1, E-X2 and E-X3 rows now list the routes covered and say what is not covered;
  - `ORD.md` rows 93-98 are appended (94's component test and 98 marked resolved);
  - one-line pointers are added to the CODE_REVIEW and SECURITY records.

### Evidence observed by this gate

- `npx vitest run tests/components/orders tests/components/cart tests/components/checkout tests/guards --maxWorkers=2`: 6 files, 81 tests, all passed. This includes both new tracking tests.
- I did not take the builder's 34/34 claim on trust. I ran the three new tests myself:
  - command: `PORT=3118 npx playwright test tests/e2e/ordering-a11y-responsive.spec.ts -g "flows that need set-up" --workers=1 --project=chromium`;
  - result: **3 passed, none retried**, in 1.3 min (checkout review 24.7 s, checkout complete 22.1 s, POS after a scan 19.6 s).
  - The spec sets each viewport itself, so the mobile-360 project would repeat these same checks (ORD-92).
- I did not re-run the other 31 routes. The product changes are three class-only edits: a label hidden below `sm`, and two colour tokens moved to the darker `ink-muted`. Neither can add overflow or lower contrast on another route. The fixer records 34/34 on both projects.

### Criteria affected

- **A30: MET (MSW).**
  - axe (zero serious/critical at 360 and 1440) and no horizontal scroll at 360-1440 now cover `/checkout` at the review step with the changes panel, `/checkout/complete/[orderId]` with the countdown, and the POS with a scan in progress. This is the new set-up block above, run by this gate.
  - The 31 earlier routes pass as before (T-8, T-9).
  - Still not covered (recorded honestly in the TC E-X1 row, LOW, row 99): the fulfilment, slot and payment steps of `/checkout`, and the empty cart.
  - The `/cart` storefront header is still excluded (ORD-81, owner deferral).
- **A22:** later tracking entry now has a test (`staff-orders.test.tsx`, "Entering the tracking number later"). It is MET on a test as well as on source; row 94's component half is resolved.
- **A27:** the POS set-up test asserts that `#pos-scan` is still focused after a scan, but only within `auditViewports`, not in E-F1. Row 95 stays open (LOW).
- A1-A29 are otherwise unchanged from the first pass.

### Prior gates

- Unchanged. The combined re-check (64fa94ce) is the ARCHITECTURE and CODE_REVIEW result.
- The CODE_REVIEW and SECURITY records now point to it (row 98, resolved).
- The delta is a test addition plus three class-only fixes. Under the gate scope rule ("tiny fixes just a test"), it needs no further ARCH, SEC or CODE pass. The test evidence is the run above.

### Findings

No BLOCKER and no HIGH.

- [LOW] `web/components/checkout/CheckoutStepper.tsx:68`: `step.status === 'skipped' ? 'text-ink-muted' : 'text-ink-muted'` is now a ternary with identical branches. Use the class directly. (Proposed row 100.)
- [LOW] TC E-X1 row: the fulfilment, slot and payment steps of `/checkout` have no axe or 360 px check. The flow specs exercise them only on the desktop chromium viewport. Add `?step=fulfilment` and `?step=payment` (with a pickup and a delivery quote) to the set-up block. (Proposed row 99.)
- Carried, unchanged:
  - rows 93, 95, 96 and 97 (LOW, open);
  - ARCH MEDIUM rows 63 and 64;
  - the LOW rows 65, 68 and 84-92.

### Proposed backlog rows (docs/backlog/ORD.md; the highest existing row is 98)

| # | Sev | Item | Source |
|---|---|---|---|
| 99 | LOW | E-X1/E-X2 do not cover the `/checkout` fulfilment, slot and payment steps or the empty cart (TC E-X1 "Not covered"). Add `?step=fulfilment`, `?step=slot` and `?step=payment` routes to the `ordering-a11y-responsive.spec.ts` set-up block. | HO-ORD-002 ACCEPTANCE re-check |
| 100 | LOW | `CheckoutStepper.tsx:68` has a ternary with identical branches (`'text-ink-muted' : 'text-ink-muted'`) after the contrast fix. Use the class directly. | HO-ORD-002 ACCEPTANCE re-check |

### Conditions (unchanged, carried, not counted as passes)

- C-1: the real-API E-series, including E-A2 and E-B7. NOT RUN by owner rule.
- C-2: the customer-orders list after `ho/ORD-001-FU2` merges (ORD-82).
- C-3: the Shop follow-up for the storefront header badge and the `/cart` header overflow at 360 px (ORD-81, ORD-87).
- C-4: the branch is based on a8761829, and master is now b12fd39d. Re-run tsc and the ordering suites after the merge.

### Scope check

- files in allowed scope: 9/9 in the delta:
  - `components/cart/**`, `components/checkout/**`, `tests/components/orders/**` and `tests/e2e/ordering-*.spec.ts` (§13);
  - the TC sheet and the two HO-ORD-002 gate records (§13);
  - `docs/backlog/ORD.md` (the gate-rule recording practice).
- files outside allowed scope: none in the delta. On the whole branch, it is still only `tests/unit/lib/idempotency-renewal.test.ts` (ORD-86, LOW).

### Undeclared deviations

- None new.
- The guard narrowing (row 96) is still open, but it is declared in the backlog now.
- The TC sheet's E-X rows now state exactly what is and is not covered, which closes the earlier misstatement.
