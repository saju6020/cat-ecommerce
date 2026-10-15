GATE: ARCHITECTURE
HANDOVER: HO-ORD-002
COMMITS: 12543d76 5ef10b1a 1239ce15 62e1bac7 b22c41b9 3d4ea5be ef626a68 cc1c2d59 f4329fe4 05565241 91b6a09c 1da9a352 7eb7504b (range a8761829..7eb7504b, branch ho/ORD-002-build, 163 files)
DATE: 2026-10-12
STATUS: FAIL

## Basis

- I reviewed the range with `git diff a8761829..7eb7504b`. I read it against these sources:
  - HO-ORD-002 §1 Amended (2026-10-09 and both 2026-10-10 rows), §3.3, §4, §6, §8–§14 and §20;
  - ADR-FE-089…098 as recorded in `web/docs/decisions.md`;
  - HO-SHP-002 §2 and §11 (the buy-box seam);
  - HO-ORD-001 §12.4.
- I applied the earlier frontend §14 rulings: HO-SHP-002, HO-BIL-002, HO-APT-002, HO-ACC-002 and HO-FIL-002 ARCHITECTURE. Under those rulings, a §14 edit is in scope only when an owner or integrator decision authorises it. That decision must be recorded in §1 Amended or in the owning area's approved handover. The builder's own say-so does not count.
- This is a web-only handover with no `src/**` change. §6 says correctly that the `.ai/` backend documents do not govern this diff, so the backend checklist has nothing to apply to.
- This gate built nothing and ran nothing. That includes Playwright and the test suites (memory constraint). Runtime claims are marked for TEST.
- Owner rule (2026-09-27): only BLOCKER and HIGH fail a gate. MEDIUM and LOW items are proposed as backlog rows below. The integrator commits them to `docs/backlog/ORD.md`. The highest existing row is 62, so the proposals start at 63. SECURITY and CODE_REVIEW are running at the same time, so the integrator may renumber.

## Verdict

**FAIL.** There are five BLOCKERs, all of them edits to other areas' files that nothing authorises:
- for two of them the fix is a decision, not code (B1, B2);
- one is a decision or a revert (B3);
- one has a cheap in-scope code fix (B4);
- one is a plain revert (B5).

There is one HIGH: the customer-orders panel works around a missing backend endpoint, which §20.4 forbids, and the workaround can show another person's orders.

Apart from these, the slice follows its architecture closely. The ADRs are faithful to §9, and the hard rules (no money arithmetic, no payment instrument, quote as truth, one fetch site) are all met.

## What was verified (compliant)

**Layering and forbidden shapes (HO-ACC-002 §4.3, §8.2, §8.3)**
- There are no `useQuery` or `useMutation` calls in `components/**` or `app/**`. Every query lives in the new `lib/hooks/use*.ts` files.
- `fetch(` is called nowhere in the diff outside `lib/api/client.ts`.
- `client.ts` gains only the `idempotencyKey` option (+6/−1). The identity headers are still spread last, so a caller cannot override CSRF or `X-Branch-Id`. The client does not retry.
- Nothing in the diff writes to `localStorage`, `sessionStorage`, `document.cookie` or IndexedDB. The only `document.cookie` hit is the existing CSRF reader in `client.ts`.
- There is no `dangerouslySetInnerHTML`, no `createContext` store and no state library.
- `errors.ts` is append-only. The one changed line adds `ApiError` to an import.
- There is no new package, and `package.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs` and the Tailwind config are untouched. The new code imports only `react`, `next/*`, `lucide-react`, `react-hook-form` and `zod`.

**ADR-FE-089…098**
- All ten are recorded in `web/docs/decisions.md` and match §9. Three of them add refinements that tighten the rule rather than loosen it:
  - ADR-FE-091: the acknowledgement tick is disabled while the quote settles.
  - ADR-FE-093: dialogs sit at screen level, so a 30-second poll cannot close them. Return panels key off the return status because the API has no `allowedActions` for returns, and the server still refuses wrong moves.
  - ADR-FE-096: the order is refetched at the deadline.
- ADR-FE-098: the allow-list gains exactly `/cart` and `/checkout`.

**C-5 and C-6 (no payment gateway, no money arithmetic)**
- There is no card field, wallet, gateway, redirect or "pay online" control anywhere.
- The payment methods are COD, bank transfer, pay-on-collection and, at the counter, cash and *Card in store* with a terminal slip reference. That matches §11.2, §11.11 and the owner's no-gateway decision.
- `BankTransferInstructions` rejects a card-number-like transfer reference, using the existing `lib/validation/cardNumberGuard`.
- The two guard scans live in `web/tests/guards/` as §12.3 directs.
- A grep for `Number(`, `parseFloat`, `toFixed` and `Math.*` across the ordering components, hooks and API modules found only integer quantities, page numbers, grams, priorities and date or countdown arithmetic in `lib/format/orders.ts`. Nothing touches an amount. Change due, remaining balance, refund suggestions and totals all come from the server.

**Quote as the single source of truth (ADR-FE-090)**
- The checkout choices live in URL params via `useUrlParams`.
- Availability, slots, payment options and `canPlace` are read from the quote, not computed.

**Polling and branch scope (UI-5)**
- `useStaffOrders` polls the queue, the detail and awaiting-payment every 30 s, with `refetchIntervalInBackground: false`.
- Every query key that is scoped to a branch carries `branchId`, read from `useBranchScope`.

**Staff actions (ADR-FE-093)**
- `StaffOrderDetail` builds its action bar from `order.allowedActions` (line 57).

**Printing (ADR-FE-094)**
- The pick list, packing slip and receipt all render through `components/print/PrintSheet.tsx`. There is no PDF library.

**Scanning (ADR-FE-095)**
- `useScanner` does keyboard-wedge burst detection with no library and no camera.
- The basket, tenders and scan buffer are component state only.

**Amended contract changes**
- The restock and write-off decision is gone: a received return stays `Received` (Amended 2026-10-09 (1)).
- The POS sale sends a name and mobile (Amended (2)).
- `DeliveryTaxClassNotConfigured` handling is removed (Amended 2026-10-10 (b)).

**Customer layer versus staff screens**
- `/cart` sits in `(shop)`, and checkout, the placed page, my orders and the return request sit in `(portal)`. Both layouts set `data-theme="customer"`, so every customer screen inherits the warm layer.
- No staff route or component under `orders`, `pos`, `fulfilment`, `returns`, `refunds`, `ordering-admin`, `print` or `(staff)/staff/**` uses `warm*`, `customer-surface*` or `data-theme`. The staff screens stay neutral.
- Shared presentational parts such as `OrderSummary` carry no theme of their own.

## Rulings on each file outside the ordering area

| File | Owner | Ruling |
|---|---|---|
| `web/app/(auth)/sign-in/page.tsx` | HO-ACC-002 | **BLOCKER B1.** The content is exactly what §8.1, §12.2 and ADR-FE-098 authorise, but §13 names `web/lib/routing/**`, and the allow-list is passed from the sign-in page. This is the same situation as HO-SHP-002 B1, which was closed by an owner decision. No code change is needed. |
| `web/app/(shop)/_components/ShopAccountLinks.tsx` | HO-SHP-002 | **BLOCKER B2.** §14 explicitly forbids "HO-SHP-002's … header files". HO-SHP-002 provides no cart slot in its header; its only delegation to this handover is the buy-box `actions` slot. |
| `web/components/shop/ShopHeader.tsx` | HO-SHP-002 | **BLOCKER B2.** Same rule. The edit changes the header's flex layout for every storefront page. |
| `web/components/billing/ResponsiveTable.tsx` | HO-BIL-002 | **BLOCKER B3.** This is a shared Billing component. The 360 px fix narrows the label column from 8 rem to 6 rem and un-nowraps spans for every Billing card list as well. Nothing records an authorisation. |
| `web/mocks/handlers/areas/cat-profiles.ts` | HO-CAT-002 | **BLOCKER B4.** It rewrites `GET /api/cats` for every area's mock: a request for owner `c000…001` now returns a different owner's cats. |
| `web/public/mockServiceWorker.js` | HO-CAT-002 scaffold (generated) | **BLOCKER B5.** It was regenerated or reformatted. The version (2.15.0) and checksum are unchanged, and a normalised diff shows only formatting. It has no effect, but it is a §14 file. Revert it. |
| `web/app/(shop)/_components/ProductPurchase.tsx` | HO-SHP-002 | **Accepted.** HO-SHP-002, an approved handover from the owning area, says three times (lines 15 and 38, and the §11 BuyBox row at line 312) that its buy-box `actions` slot is one "that HO-ORD-002 fills with *Add to cart*". The edit fills that slot with a default and keeps the override. The authority is the owning area's, not the builder's. HO-ORD-002 §3.3 and §14 contradict it, so this is a LOW handover defect (row 66). |
| `web/tests/components/product/product-page.test.tsx` | HO-SHP-002 | **Accepted, under the same authority.** The five assertions changed only to follow the live *Add to cart* (+5/−4). LOW (row 66). |
| `web/components/admin/TaxClassForm.tsx` | HO-ADM-002 | **Accepted.** HO-ORD-002 §1 Amended, the 2026-10-10 FU1 fix round note (1), was not written by this builder; it was committed in dc76e62a. It directs that "the admin tax-class screen should disable or explain the toggle" for `DELIVERY`, and that screen is this file. The edit disables the toggle and explains why; the server still enforces the rule. §13 does not list the file, so this is LOW (row 66). Whether `checked` combined with `register` behaves correctly is for CODE_REVIEW and TEST. |
| `web/app/(portal)/layout.tsx` | HO-CAT-002 / HO-ACC-002 | **Accepted, in §13.** The edit mounts `CartBadge` beside `InboxBadge`. §3.3 and §20.1 sanction this: the badge is "exercised … by the `(portal)` header". §13's qualifier "one nav entry only" does not describe the edit, so this is LOW (row 66). |
| `web/app/(staff)/staff/customers/[customerId]/page.tsx` | HO-ACC-002 | **Accepted, in §13.** It is a mount only (+3). The panel it mounts carries the HIGH below. |
| `web/nav/areas/orders.ts`, `web/components/admin/adminNavItems.ts` | Orders registry / ADM | **Accepted.** The nav entries go in the area registry and `adminNavItems`, not the layouts, as `web/README.md` and the HO-BIL-002, HO-NTF-002 and HO-SHP-002 precedents direct. The edits add one portal entry, three staff entries and four admin entries, as §12.2 lists. §1 Amended does not record the substitution, so this is LOW (row 66). |
| `web/tests/e2e/ordering-helpers.ts`, `web/tests/unit/lib/ordering-next-path.test.ts` | New files | **Not §14.** They fall outside §13's test globs. LOW (row 67). |

## FINDINGS

- [BLOCKER] B1 web/app/(auth)/sign-in/page.tsx:52
  - **Rule.** §14 forbids editing an HO-ACC-002 file beyond the lines §13 names. §13 places the allow-list under `web/lib/routing/**`, not in this page.
  - **What the edit does.** It adds `'/cart', '/checkout'` to the page's allow-list. The content is correct.
  - **To close.** An owner or integrator decision in HO-ORD-002 §1 Amended that approves this file, and correct §13. No code change is needed. Precedent: HO-SHP-002 B1.
- [BLOCKER] B2 web/app/(shop)/_components/ShopAccountLinks.tsx:28-42 and web/components/shop/ShopHeader.tsx:60
  - **Rule.** §14 forbids "HO-SHP-002's product, listing and header files". §3.3 says HO-SHP-002 mounts `CartBadge`.
  - **What the build does.** It mounts the badge itself, and it changes the header's flex layout (`flex-wrap`, `justify-end`, `gap-y-1`) for every storefront page.
  - **To close.** Either an owner decision in §1 Amended that approves both edits (the simplest route, since HO-SHP-002 is merged), or revert and let a Shop follow-up mount the badge.
  - **For TEST.** The 360 px effect on non-ordering shop pages is unverified here.
- [BLOCKER] B3 web/components/billing/ResponsiveTable.tsx:213,219
  - **Rule.** §14 forbids editing another area's shared component.
  - **What the edit does.** It changes Billing's mobile card layout (label column 8 rem → 6 rem; `[&_span]:whitespace-normal`) for every Billing list in order to fix ordering's 360 px overflow.
  - **To close.** Either an owner decision recorded in §1 Amended (and preferably noted for HO-BIL-002), or revert and fix the overflow inside the ordering column `cell` renderers. `AwaitingPaymentList` and `MyOrdersList` are the consumers.
  - **For TEST.** If the edit is ratified, re-run Billing's 360 px responsive checks.
- [BLOCKER] B4 web/mocks/handlers/areas/cat-profiles.ts:788-793
  - **Rule.** §14 forbids editing an HO-CAT-002 file. §13 allows only `mocks/handlers/ordering/**` and `index.ts`.
  - **What the edit does.** It remaps `ownerId=c0000000-…-0001` to `catProfilesFixtureIds.ownerWithCats` in the shared `GET /api/cats` mock, which changes that response for every area's tests.
  - **To close.** Revert, and add an ordering-owned `http.get('*/api/cats', …)` override for that owner id. Register it ahead of `catProfilesHandlers` in `mocks/handlers/index.ts`, which is in scope; ordering currently registers after cat-profiles (index.ts:49 vs 58), so the override needs its own earlier entry. The other route is an owner decision.
- [BLOCKER] B5 web/public/mockServiceWorker.js
  - **Rule.** §14 forbids editing an HO-CAT-002 scaffold file. The file's own header says "Please do NOT modify this file".
  - **What happened.** It was regenerated or reformatted (+91/−76) with the same `PACKAGE_VERSION` 2.15.0 and the same checksum. A normalised diff shows only line-wrapping and semicolons.
  - **To close.** Revert the file to a8761829.
- [HIGH] web/components/orders/CustomerOrdersPanel.tsx:17-29, mounted at web/app/(staff)/staff/customers/[customerId]/page.tsx:69
  - **Rule.** §20.4 says that when the real API disagrees with HO-ORD-001 §12.4, the builder reports it and does not shim or compute the missing value locally. §20.5 adds that a needed endpoint the API does not provide is reported, not worked around.
  - **The gap.** HO-ORD-001 §12.4 (line 1162) specifies `GET /api/staff/customers/{customerId}/orders`. No controller in `src/**` serves it.
  - **What the panel does instead.** It queries the order queue with `search = mobile ?? fullName`, limited to the active branch. Its own comment admits it "can show another person who shares the number". The fallback to the name makes this more likely.
  - **Why it is a HIGH.** A staff customer page then lists orders under the heading "Orders" for customer X that may belong to customer Y, while silently missing X's orders at other branches. The gap is recorded only in a code comment, not in the TC sheet, the backlog or §1 Amended. If copied, this pattern turns every missing endpoint into a plausible-looking wrong answer.
  - **To fix.** Remove the query. Render the panel with the *New counter sale for this customer* action and an explicit "Order history is not available here yet" state. A plainly labelled link to `/staff/orders?search=…` is acceptable because it reads as a search. Record the backend gap for HO-ORD-001 follow-up.
  - **For SECURITY.** This is also a privacy concern.
- [MEDIUM] web/components/orders/MyOrderDetail.tsx:36-46 (proposed row 63)
  - **What the code does.** `isExpiredTransfer` infers "payment window expired" on the client by comparing the `Cancelled` history entry with `paymentDueBy`, because the customer detail carries no `cancellationReason`. HO-ORD-001 has `CancellationReason = PaymentWindowExpired` (§8, lines 351, 372 and 831).
  - **Why it matters.** This is a client-side derivation of a business state, which §4 and §20.2 forbid.
  - **Why only MEDIUM.** It affects display only; *Order again* still goes to the server.
  - **A false citation.** The comment says the gap was "reported" under "HO-ORD-002 §1 Amended", but no such Amended entry exists.
  - **To fix.** Record the gap (expose `cancellationReason`, or a `paymentWindowExpired` flag, on the customer detail) and switch to the server field.
- [MEDIUM] web/components/checkout/CheckoutScreen.tsx:58,169-176,405,436 (proposed row 64)
  - **What the code does.** The component calls `useQueryClient` and invalidates `['checkout','quote']`, `['cart']` and `['me','orders']` itself.
  - **The rule.** §8.3 says query-key knowledge belongs in `lib/hooks`.
  - **To fix.** Move these into the `onSuccess` and `onError` of the `useCheckout` mutations.
  - **For CODE_REVIEW.** The file is 473 lines against the ~150-line guidance.
- [LOW] web/lib/api/staff-orders.ts:14,389 (proposed row 65)
  - **What the code does.** The ordering API module imports `downloadBillingFile` from `billing-staff.ts` for the manifest CSV. §12.2 placed the blob helper in `client.ts`.
  - **Why it is acceptable.** It reuses the single existing download path and adds no second call site.
  - **The risk.** It couples Ordering to Billing's module. Move the helper to `client.ts` in a later batch, or record the reuse.
- [LOW] Handover §13 and §14 defects (proposed row 66). Correct §13 and §14 in an Amended row to match the rulings above:
  - `ProductPurchase.tsx` and `product-page.test.tsx`: authorised by HO-SHP-002's slot delegation, but forbidden by HO-ORD-002 §14.
  - `TaxClassForm.tsx`: authorised by Amended FU1 (1), but not listed in §13.
  - `(portal)/layout.tsx`: §13's "one nav entry only" qualifier versus the `CartBadge` mount.
  - The nav entries: added in `nav/areas/orders.ts` and `adminNavItems.ts` instead of the layouts.
- [LOW] web/tests/e2e/ordering-helpers.ts, web/tests/unit/lib/ordering-next-path.test.ts (proposed row 67)
  - **The issue.** Both fall outside §13's test globs, which are `tests/e2e/ordering-*.spec.ts` and `tests/components/{…}/**`.
  - **Why it is only LOW.** Both are new ordering-only files. This matches the HO-SHP-002 `shop-helpers.ts` ruling.
- [LOW] web/components/returns/ReturnDetail.tsx:64-76 (proposed row 68)
  - **What the code does.** The return actions are chosen by `ret.status`, because the staff return detail has no `allowedActions`. ADR-FE-093 records this, and the server refuses wrong moves.
  - **To fix.** Ask HO-ORD-001 to add `allowedActions` to the return detail so that ADR-FE-093 holds uniformly. This is a backend follow-up, not a defect in this diff.

## Proposed backlog rows (docs/backlog/ORD.md; the highest existing row is 62)

| # | Sev | Item |
|---|---|---|
| 63 | MEDIUM | `MyOrderDetail.tsx:36-46`: client-side expiry inference. Ask HO-ORD-001 for `cancellationReason` or `paymentWindowExpired` on the customer detail, then use it. Correct the false "§1 Amended" citation. |
| 64 | MEDIUM | `CheckoutScreen.tsx`: move the `useQueryClient` invalidations into the `useCheckout` mutation callbacks. |
| 65 | LOW | `staff-orders.ts`: the manifest download depends on `billing-staff.ts#downloadBillingFile`. Hoist the helper into `client.ts` (§12.2) or record the reuse. |
| 66 | LOW | Amend HO-ORD-002 §13 and §14: add `ProductPurchase.tsx` and `product-page.test.tsx` (HO-SHP-002 delegation), `TaxClassForm.tsx` (Amended FU1 (1)), the `(portal)/layout.tsx` CartBadge mount, and the nav registry in place of the layouts. |
| 67 | LOW | `tests/e2e/ordering-helpers.ts` and `tests/unit/lib/ordering-next-path.test.ts` are outside §13's test globs. Amend §13. |
| 68 | LOW | Backend follow-up: `allowedActions` on the staff return detail (ADR-FE-093 parity). |

## Marked for TEST (not observable here)

- The 360 px behaviour of the storefront header (B2) and of Billing lists (B3).
- The `TaxClassForm` disabled checkbox together with `register`.
- The single-shot behaviour of every money submission (U-B12, U-F5, E-B8).
- The countdown announcements (U-C2).
- E-F1 timing.

## SCOPE CHECK

- files in allowed scope: 150/163 by §13's literal globs.
  - 4 further files are accepted on recorded authority outside §13: `ProductPurchase.tsx`, `product-page.test.tsx`, `TaxClassForm.tsx`, and the nav registry pair counted as 2.
  - 2 are accepted as new ordering test files: `ordering-helpers.ts` and `ordering-next-path.test.ts`.
- files outside allowed scope:
  - `web/app/(auth)/sign-in/page.tsx`: §14, B1. Content correct; needs ratification.
  - `web/app/(shop)/_components/ShopAccountLinks.tsx`: §14, B2.
  - `web/components/shop/ShopHeader.tsx`: §14, B2.
  - `web/components/billing/ResponsiveTable.tsx`: §14, B3.
  - `web/mocks/handlers/areas/cat-profiles.ts`: §14, B4.
  - `web/public/mockServiceWorker.js`: §14, B5 (revert).

---

## Combined re-check 2026-10-12 (architecture + security + code-review findings)

GATE: ARCHITECTURE (combined re-check of the gate fix round)
HANDOVER: HO-ORD-002
COMMITS: 0767caa7 b0110078 (range de6cf1e6..b0110078, branch ho/ORD-002-build, 36 files)
DATE: 2026-10-12
STATUS: PASS

### Basis

- I read `git diff de6cf1e6..b0110078` in full against these sources:
  - the three earlier records (ARCH 97b8c8ac, SEC 0aaf8560, CODE de6cf1e6);
  - the HO-ORD-002 2026-10-12 Amended row;
  - the backend `IdempotencyFilter.cs`, `OrderWorkCycle` and `appsettings.json` (`Orders:Worker:IntervalSeconds` = 60);
  - `lib/api/client.ts` (the 409 body parse).
- I checked the reverts with `git diff master -- <path>`. I checked scope with `git diff --name-only master...HEAD`.
- I ran a targeted vitest (`--maxWorkers=2`) on these five files: `idempotency-renewal`, `pos`, `staff-orders`, `my-orders` and `checkout`. All 5 files and 92 tests pass.
- I ran no Playwright, no full suite and no build (memory constraint). The e2e claims are marked for TEST.
- Owner decisions 2026-10-12, as relayed by the integrator:
  - only "Sign-in redirect allow-list" was approved;
  - the header cart badge and the Billing `ResponsiveTable` fix were not approved and are to be reverted;
  - customer-orders panel: "Build the endpoint" (`ho/ORD-001-FU2`), with a "not available yet" state until it merges.

### Closure of the earlier BLOCKER and HIGH findings

| Finding | Status | Evidence |
|---|---|---|
| ARCH B1 sign-in allow-list | **Closed** | The Amended row dated 2026-10-12 quotes the owner's tick ("Sign-in redirect allow-list") and adds the file to §13 for exactly the `'/cart', '/checkout'` line. |
| ARCH B2 ShopHeader / ShopAccountLinks | **Closed (reverted)** | `git diff master` is empty for both files. |
| ARCH B3 billing/ResponsiveTable | **Closed (reverted)** | `git diff master` is empty. The `/staff/orders` 360 px fix moved into `StaffOrderQueue.tsx` (a status-cell wrapper), which is in scope. |
| ARCH B4 cat-profiles.ts | **Closed (reverted + owned override)** | `git diff master` is empty. `mocks/handlers/ordering/cats.ts` answers only `ownerId == orderingIds.customerId` and returns `undefined` otherwise. In MSW 2 that falls through to `catProfilesHandlers`. It is registered ahead of them in `mocks/handlers/index.ts`, exactly as B4 prescribed. The only frontend caller that passes `ownerId` is `usePos.ts:94`; the portal, staff and booking cat lists send no `ownerId`, so no other area's behaviour changes. |
| ARCH B5 mockServiceWorker.js | **Closed (reverted)** | `git diff master` is empty. |
| ARCH HIGH customer-orders panel | **Closed** | `CustomerOrdersPanel` no longer queries anything. It renders the counter-sale action, "Order history for a customer is not available here yet" and a plain link to the Orders screen. The mount drops `searchTerm`. The gap is recorded as backlog ORD-82 and in Amended (3). A test asserts that no queue request is made. |
| CODE HIGH queue date range | **Closed** | See the detail after this table. |

**CODE HIGH: what the fix does**
- `StaffOrderQueue` sends `dayStartInstant(from, zone)` and `dayEndInstant(to, zone)`, using `useBranchTimeZone()`.
- Both helpers go through the existing two-pass `branchLocalToInstant`, which formats with `hourCycle: 'h23'`.
- `dayEndInstant` is the next day's local 00:00 minus 1 ms.
- The mock now binds zone-less values as UTC, as the API's `DateTimeOffset?` does. It no longer depends on the machine's zone.

**CODE HIGH: what the tests assert**
- Unit tests:
  - Dhaka 2026-10-12 → `2026-10-11T18:00:00.000Z` / `2026-10-12T17:59:59.999Z`;
  - the month rollover;
  - a zone behind UTC.
- A component test asserts the `from` and `to` query parameters actually sent.

### Scope check of the fix round and the whole branch

- Every file in the fix round falls inside §13 as amended on 2026-10-12, or is one of the five reverts. The non-ordering files touched are:
  - `staff/customers/[customerId]/page.tsx`: a one-line mount change, which §13 permits;
  - `mocks/handlers/index.ts`: in §13;
  - the handover, backlog and TC docs.
- On the whole branch against master, every non-ordering path is one already ruled on in this record:
  - the sign-in page is now ratified;
  - `ProductPurchase.tsx`, `product-page.test.tsx`, `TaxClassForm.tsx`, the `(portal)/layout.tsx` CartBadge, the nav registry pair and `ordering-helpers.ts` are accepted, and Amended (4) now writes them into §13.
- No `package.json` change. No other area's file is touched without authority.
- One new file falls outside even the amended glob: `web/tests/unit/lib/idempotency-renewal.test.ts`. The glob is `tests/unit/lib/ordering-*.test.ts`. It is a new, ordering-only test, so this is LOW (row 86), consistent with the `ordering-helpers.ts` ruling.

### Defects the fix round could have introduced (checked, none blocking)

**Idempotency renewal (`shouldRenewIdempotencyKey` / `settleFailure`)**
- The rule is correct against `IdempotencyFilter`, which stores and replays responses from 200 to 499 and releases the key on a 5xx or an exception.
- The rule renews on any `ApiError` with a status from 400 to 499, except 409 `RequestInProgress`. It keeps the key on status 0 (network) and on 5xx.
- `client.ts:304` parses the 409 body into `ApiConflictError.response`, so `RequestInProgress` is detected.
- 422 `IdempotencyKeyReused` renews, as before.
- 401 and 403 come from authorization before the resource filter and are never stored, so renewing on them is harmless.
- 429 is also never stored; renewing on it is harmless.
- A stored 4xx is a completed refusal, so a new key cannot double-apply anything. (The POS walk-in customer stored on a refused sale is found again by mobile — Amended 2026-10-10 (d).)
- It is applied to checkout, POS, confirm transfer, record delivery, refund, approve payment and reorder. `lib/hooks/useIdempotencyKey.ts` is ordering-owned; Billing has its own hook, which is untouched.

**RequestInProgress retry**
- `inProgress` clears after 4 s and invalidates `me/orders`. The ReviewStep shows a "Check my orders" link meanwhile.
- The key is kept, so a second Place order with the same body either:
  - replays the first attempt's stored 2xx or 4xx; or
  - gets 409 again if the first attempt is still running.
- `submitting.current` still guards a double click.
- Sound.

**POS scan queue and ref-based basket**
- `useScanner.onKeyDown` is rebuilt with the latest `onSubmit`, so `process` is never stale.
- The field clears on Enter. Lookups are chained on a promise, and `process` catches every error, so the chain never rejects.
- `restore` puts a failed code back only when the field is empty.
- Every basket write goes through the `setLines` wrapper:
  - add, quantity, remove and cat each use the functional form;
  - `reset` uses `setLines([])`;
  - nothing calls `setLinesState` directly.
- So `linesRef` cannot drift, and two scans of one SKU reach quantity 2.
- One minor residue, LOW (row 88): a restored failed code is still in the field when the next wedge scan arrives, so the two concatenate. This is pre-existing, documented behaviour of `useScanner`.

**POS review gating**
- `settled` = not settling, not fetching and not placeholder data. `isSettling` compares the serialised input with the debounced input synchronously, so it is true on the very render a basket change lands.
- Tenders are not part of the counter quote's input. Enter adds the tender and sets `reviewRequested` in the same batched render.
- The effect then opens the review only if `ready`, and resets the request either way.
- The blocking reasons render under Complete sale.
- Sound.

**Countdown polling**
- The polling stops in two ways:
  - after the deadline it calls `onExpired` immediately, then every 10 s, at most 18 more times (about 3 minutes);
  - the parent unmounts `BankTransferInstructions` as soon as the status leaves `PendingPayment`, and the cleanup clears the interval.
- The backend expiry worker runs every 60 s, so the window normally covers it.
- The `onExpired` ref removes the dependency churn.
- The StrictMode fix (a mount-time snapshot) is correct.
- Residue, LOW (row 89): after the last re-check, the text "We are checking the order…" stays on screen.

**Mock override fall-through**
- This is covered in the B4 row above, and a test exercises both branches.

**Other fixes verified**
- Policy and prescription refusals invalidate the quote and cart and go to the review step.
- The checkout zone label uses `formatDeliverySlot`.
- The POS quantity input commits on blur or Enter.
- SEC LOW: `StaffOrderPanels` uses `safeHttpUrl`.
- The added tests exist as claimed: two scans in flight, double-click with one key, a renewed key after a refusal, multi-line totals, the F2/Enter path, StrictMode, recheck and stop, and the instants sent.

### Ruling on the narrowed e2e assertion (`/cart` 360 px ignores the storefront `<header>`)

**Acceptable.** The reasons:
- The header is HO-SHP-002's. The owner explicitly declined ORD-002 editing it, so this handover has no authority to fix it.
- The overflow comes from master's header (the account cluster and the language switcher). It is not caused by ordering code.

The narrowing is tight:
- It applies to `/cart` only (`STOREFRONT_ROUTES`).
- It skips only the elements inside a `<header>`.
- For everything else it measures each element's right edge against the viewport. That is stricter than `scrollWidth`, because a clipped child also fails.
- Staff and portal routes keep the full `scrollWidth` check.

ORD-81 records the residue. However, it is a Shop defect that exists on every storefront page, and it lives only in `ORD.md`. Mirror it into `docs/backlog/SHP.md` so that the Shop owner sees it (row 87, LOW).

### FINDINGS

No BLOCKER and no HIGH.

- [LOW] web/components/orders/StaffOrderQueue.tsx:43 (row 84)
  - **What the code does.** It uses `useBranchTimeZone()`, which returns the zone of the staff member's *primary* branch. The queue, however, is scoped to the *active* branch (`useBranchScope`).
  - **Why only LOW.** All branches share Asia/Dhaka today, so the result is the same.
  - **To fix.** Use the active branch's zone before a second zone exists. The CODE gate itself prescribed `useBranchTimeZone`.
- [LOW] web/components/checkout/CheckoutScreen.tsx:73 (row 85)
  - **What the code does.** It labels slot times with `usePublicBranches().data[0].timeZoneId` and falls back to Asia/Dhaka, because the quote carries no zone.
  - **Why only LOW.** It is correct for the single zone today.
  - **To fix.** Ask HO-ORD-001 to put the fulfilling branch's zone on the quote, or use the pickup branch's zone when the customer collects.
- [LOW] web/tests/unit/lib/idempotency-renewal.test.ts (row 86)
  - **Issue.** The file is outside the amended §13 glob `tests/unit/lib/ordering-*.test.ts`.
  - **To fix.** Rename it to `ordering-idempotency-renewal.test.ts`, or widen the glob.
- [LOW] docs/backlog/ORD.md row 81 (row 87)
  - **Issue.** The storefront header's 360 px overflow and the missing header cart badge are Shop-owned, but they are recorded only in the ORD backlog.
  - **To fix.** Mirror them into `docs/backlog/SHP.md`.
- [LOW] web/components/pos/PosScanField.tsx:38 (row 88)
  - **What the code does.** A failed code is put back into the field. The next keyboard-wedge scan then appends to it, which produces a concatenated, unknown code.
  - **To fix.** Select the restored text so that the next keystroke replaces it, or clear the field when a new burst starts.
- [LOW] web/components/orders/TransferCountdown.tsx:71 (row 89)
  - **What the code does.** After the 18th re-check (about 3 minutes) polling stops, but "We are checking the order…" stays on screen indefinitely.
  - **To fix.** After the last check, say "Refresh the page to see the latest status".

### Backlog rows 63–83 (honesty check)

- 63, 64, 65 and 68 are honestly **Open**. Row 64 now says "470-line"; the file is 499 lines.
- 66 and 67 are **Resolved** by Amended (4). True.
- 69: the frontend half is resolved, and verified. True.
- 70–80 are **Resolved**, and each was verified in code and tests above. Two notes:
  - row 76 is resolved by the revert, not by a scoped fix, which is the honest wording;
  - row 79 discloses that change due is a mock figure.
- 81 is **Open**: a Shop follow-up, see row 87.
- 82 is an Open HIGH for the backend half, tracked by the owner decision and `ho/ORD-001-FU2`. The frontend half is resolved. True.
- 83 is **Resolved**. True.

### Proposed backlog rows (docs/backlog/ORD.md; the highest existing row is 83)

| # | Sev | Item | Source |
|---|---|---|---|
| 84 | LOW | `StaffOrderQueue` date instants use the primary branch's zone (`useBranchTimeZone`), not the active branch's that scopes the queue. Same zone today; switch before a second zone. | HO-ORD-002 combined re-check |
| 85 | LOW | Checkout slot labels use `branches[0]`'s zone because the quote carries none. Ask HO-ORD-001 for the fulfilling branch zone on the quote, or use the pickup branch's zone. | HO-ORD-002 combined re-check |
| 86 | LOW | `tests/unit/lib/idempotency-renewal.test.ts` is outside the amended §13 glob `tests/unit/lib/ordering-*.test.ts`. Rename it or widen the glob. | HO-ORD-002 combined re-check |
| 87 | LOW | Mirror ORD-81 (the storefront header 360 px overflow and the missing header cart badge) into `docs/backlog/SHP.md`. It is Shop-owned and appears on every storefront page. | HO-ORD-002 combined re-check |
| 88 | LOW | POS: a failed code restored into the scan field concatenates with the next wedge scan. Select the restored text or clear it when a new burst starts. | HO-ORD-002 combined re-check |
| 89 | LOW | TransferCountdown: after the last of 18 re-checks the "We are checking the order…" text stays indefinitely. Change the wording after the final check. | HO-ORD-002 combined re-check |

### Marked for TEST

- `/cart` at 360 px: only the `<header>` subtree overflows.
- `/staff/orders` at 360 px with the status-cell wrapper. The TC sheet says the mobile-360 project was not run.
- Billing lists are back to master's layout, so no Billing re-check is needed.
- The E-series on MSW after the fix round. The builder reports 60 pass, with 2 staff specs passing only on retry.

### SCOPE CHECK

- files in allowed scope: 35/36 in the fix round. The 36th, `idempotency-renewal.test.ts`, is a new ordering-only test outside the amended glob (LOW, row 86). The five reverted files now equal master.
- files outside allowed scope: none without authority. On the whole branch, the sign-in page is ratified by the owner (Amended 2026-10-12 (1)), and the other non-ordering files are authorised by Amended (4) and HO-SHP-002's slot delegation.
