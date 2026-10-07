# HO-ORD-002 — Cart, Checkout and Orders (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-ORD-002** |
| Title | Cart, Checkout and Orders — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §10 — **US-ORD-01 … US-ORD-20** (US-ORD-17 deferred) |
| SRS | FR-ORD-1 … FR-ORD-22; **UI-1, UI-3, UI-4, UI-5, UI-6**; NFR-USA-2, NFR-USA-4, NFR-USA-5, NFR-PRF-1, NFR-PRF-5, NFR-CMP-2, C-5, C-6, HW-1, HW-2, AC-6, AC-7, AC-8, AC-8b, AC-9 |
| Pairs with | [`HO-ORD-001`](HO-ORD-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-ORD-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, stack), [`HO-ACC-002`](HO-ACC-002.md) (sessions, `client.ts`, route groups, error mapping, address form, customer page), [`HO-ADM-002`](HO-ADM-002.md) (active branch, `lib/format` incl. `formatMoney`, in-repo grid approach), [`HO-NTF-002`](HO-NTF-002.md) (inbox), [`HO-APT-002`](HO-APT-002.md) (`useServerNow`, the 30-second polling rule, `allowedTransitions` rule). **Mounts into `HO-SHP-002`'s storefront** (§3.3) |
| Delivery order | Section 12 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | *(none — append rows here)* |

---

## 2. Objective

Build **buying** — the storefront cart, checkout and order tracking for cat owners — and **selling** —
the back office that takes the money, packs, dispatches, handles returns and runs the counter.

- **Customers** keep a cart as a guest, sign in and find it merged, see exactly what changed since they
  added each item, choose delivery or pickup, pick cash on delivery, bank transfer or pay-on-collection,
  see every part of the total before they commit, and then track, cancel, re-order or return.
- **Staff** work one order queue: confirm transfers against the bank, pick and pack, dispatch with a
  courier and tracking, record the cash the courier collected, approve returns, put stock into
  quarantine, issue refunds, and sell across the counter with a barcode scanner.

Four things make this harder than it looks:

1. **There is no payment step.** No card form, no gateway redirect, no wallet button — anywhere. The
   "payment" step is a choice of *how you will pay a person later* (ADR-FE-092).
2. **The browser never does arithmetic on money.** Every amount rendered comes from the server as a
   string; the client formats it and nothing else (C-6, ADR-FE-092).
3. **The customer must see the change before they pay.** The quote is the only truth; a changed quote
   re-opens the acknowledgement, and the Place button goes back to disabled (ADR-FE-091).
4. **A money form must never fire twice.** One idempotency key per attempt, held across retries,
   buttons disabled while pending, no automatic retry (ADR-FE-097).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack (Next.js 15 App Router, React 19, TypeScript strict, Tailwind v4, shadcn/ui,
TanStack Query v5, react-hook-form + zod, Vitest/Testing Library, Playwright + axe, MSW); HO-ACC-002's
session model, `client.ts`, route groups, error mapping, `PermissionDeniedState`, `UnsavedChangesGuard`,
customer search, address form; HO-ADM-002's active branch header, `lib/format` (`formatMoney` keeps
amounts as **strings**, `Intl` time in the branch zone); HO-APT-002's `useServerNow`, 30-second polling
rule and "render actions only from the server's allowed list".

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-ORD-01 | Cart page and cart button/badge; guest cart survives; merge banner after sign-in | `/cart` (storefront), header |
| US-ORD-02 | **Changes panel** in checkout review with an explicit acknowledgement | `/checkout` |
| US-ORD-03 | Checkout steps: fulfilment (address or pickup) → slot → payment → review; cat pickers for prescription lines | `/checkout` |
| US-ORD-04 | Order summary breakdown (every component separate) in checkout, order detail and receipt | `/checkout`, `/portal/orders/[orderId]` |
| US-ORD-05 | Discount code field with the server's specific reason; admin discount-code screens | `/checkout`, `/staff/admin/discount-codes/**` |
| US-ORD-06 | Delivery charge and service levels in checkout; "we don't deliver there — collect instead"; admin zone screens | `/checkout`, `/staff/admin/delivery-zones/**` |
| US-ORD-07 | Payment method step with unavailable methods and their reason; **no card form anywhere** | `/checkout` |
| US-ORD-19 | Bank-transfer instructions with reference and countdown; submit transfer reference; expired-order state with **Order again**; staff transfer confirmation queue | `/checkout/complete/[orderId]`, `/portal/orders/[orderId]`, `/staff/orders/awaiting-payment` |
| US-ORD-20 | Delivery recording with COD cash, collector and date; refused/undelivered parcel | `/staff/orders/[orderId]` |
| US-ORD-08 | Placement result; staff reconciliation-issues list | `/checkout`, `/staff/orders/reconciliation` |
| US-ORD-09 | Staff order queue and detail; actions only from `allowedActions` | `/staff/orders`, `/staff/orders/[orderId]` |
| US-ORD-10 | Pick list (print) and packing slip (print) | `/staff/fulfilment/pick-list`, `/staff/orders/[orderId]/packing-slip` |
| US-ORD-11 | Dispatch dialog (courier, tracking), manifest download, later tracking entry; customer tracking link; admin courier screens | `/staff/orders/[orderId]`, `/staff/admin/couriers/**` |
| US-ORD-12 | Customer cancel with confirmation; "contact the shop" state | `/portal/orders/[orderId]` |
| US-ORD-13 | Return request form with reason and condition per line; eligibility messages | `/portal/orders/[orderId]/return` |
| US-ORD-14 | Staff return receipt and per-line restock / write-off decision | `/staff/returns/[returnRequestId]` |
| US-ORD-15 | Refund dialog: allowed methods, suggested amount, reference, reason; refunds-due list | `/staff/refunds`, `/staff/orders/[orderId]` |
| US-ORD-16 | **Point of sale**: scan, basket, discount, tenders with change, receipt print | `/staff/pos`, `/staff/pos/sales/[orderId]/receipt` |
| US-ORD-18 | One idempotency key per money submission; disabled buttons; replay handling | Every money form |
| — | Bank-transfer account settings | `/staff/admin/payment-settings` |

### 3.3 The seam with HO-SHP-002 (storefront)

HO-SHP-002 owns the public storefront route group and the product page. This slice provides two
components it mounts, and nothing else of theirs changes:

| Component | Mounted by | Contract |
|---|---|---|
| `AddToCartButton` (`components/cart/`) | HO-SHP-002's product / listing card | Props `{ skuId, requiresPrescription, disabled?, quantityMax? }`; opens `PrescriptionCatDialog` when required; posts to `/api/cart/lines`; announces success in a live region |
| `CartBadge` (`components/cart/`) | HO-SHP-002's storefront header | Item count from `['cart']`; hidden at 0; links to `/cart` |

`/cart` itself lives in the storefront group. **The group's name is assumed to be `(shop)`** — if
HO-SHP-002 named it otherwise, use theirs and record an Amended row (OQ-1). If HO-SHP-002 is not built
yet, `/cart` and the two components are still built and exercised by MSW and by the `(portal)` header
(§20.1).

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-ORD-001 owns `src/**` |
| **Any card, wallet or gateway UI** — card number field, saved cards, "pay online" button, gateway iframe or redirect | C-5, SRS Phase 5. Building one is a FAIL at the acceptance gate |
| Product pages, catalogue, search, filters, "shop by country" | HO-SHP-002 |
| Stock, batch, consignment, supplier screens | HO-IMP-002 |
| Invoice, payment, reconciliation, cash-up and store-credit screens | HO-BIL-002 (this slice links to an invoice by number only) |
| Subscriptions | HO-SUB-002 |
| Portal dashboard, "re-order" entry point on the dashboard | HO-POR-002 (the endpoint is used here for expired orders) |
| Combined invoice (US-ORD-17) | HO-BIL-002 / HO-BRD-002 — HO-ORD-001 §4.2 |
| A PDF library, a barcode-scanner library, a receipt/ESC-POS driver, a table/grid/data-grid library, a money library, a state-management library | §10 |
| Offline point of sale | Not in NFR-AVL-3's list; HO-XCT-002 |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged: no global store, no token or draft in browser
storage, no `useQuery`/`useMutation` inside a component, no second `fetch` call site, no client-side
authorization decision, no `dangerouslySetInnerHTML`. Additionally: **no arithmetic on money in
JavaScript** and **no client-side availability, pricing, tax, discount or eligibility logic**.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, the HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002, HO-ADM-002, HO-NTF-002, HO-APT-002 built** | ❌ at baseline — §20.1 |
| **HO-SHP-002 storefront group and product page** | ❌ not written — §3.3, OQ-1 |
| HO-ORD-001 API | ❌ — build against MSW generated from its §12.4, then re-run every E-case against the real API |

Every §16 case is run twice — MSW and the real API — and the test sheet records both. **A mock may not
contain a field HO-ORD-001 does not specify.**

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-ORD-001`](HO-ORD-001.md) | **§12.4 endpoints and shapes; §10.4 codes and their messages; §8.4 the status machine; §8.5 the quote, its changes and the fingerprint; §8.6 payment and refund methods; §8.8 zones, slots and payment availability; §8.10 return eligibility; §8.11 the counter sale; §8.13 idempotency; §12.3 who may do what** |
| [`HO-ACC-002`](HO-ACC-002.md) | §8.3 route groups; §8.4 sessions and `?next=`; §11.2 client; §11.7 address form; §11.14 error mapping; §4.3 forbidden shapes |
| [`HO-ADM-002`](HO-ADM-002.md) | §8.3 active branch; §8.4 `lib/format` (`formatMoney`, branch time); ADR-FE-016 in-repo grids |
| [`HO-APT-002`](HO-APT-002.md) | ADR-FE-025 `useServerNow`; ADR-FE-027 polling that never resets a form; ADR-FE-028 actions from the server's list |
| [`HO-NTF-002`](HO-NTF-002.md) | Inbox links land on order pages |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.1, §6.4 | UI-1/3/4/5/6; NFR-USA-2/4/5; NFR-PRF-1/5; HW-1, HW-2 |
| `.claude/agents/frontend-developer.md` | Component size, tokens, flag-don't-introduce libraries |

**The `.ai/` documents describe the .NET backend only** and do not govern this diff — same position as
HO-CAT-002 §6.

---

## 7. The SRS requirements, as build rules

| Req | Rule | Where enforced |
|---|---|---|
| **UI-1 / NFR-CMP-2** | 360 px floor. Cart and checkout are single-column; the staff order queue becomes cards below 768 px; the POS becomes a stacked basket + tender sheet; the pick list prints from any width | §11.1, §11.2, §11.6, §11.10 |
| **UI-3** | Destructive and money actions confirm, stating exactly what is affected: cancel an order (*"Cancel order ORD-2026-000123? Your reserved items are released."*), confirm a transfer (*"Confirm BDT 1,927.50 received against ORD-…?"*), record delivery with cash, approve/reject a return, restock or write off, issue a refund, void a POS basket | Each screen |
| **UI-4** | Every list filters, sorts where the API supports it, paginates, and offers CSV where the API provides it (manifest) | §11.6, §11.8, §11.9 |
| **UI-5** | The staff order queue, the awaiting-payment list and the order detail refetch every **30 s** without resetting an open form, dialog or scan session (HO-APT-002 ADR-FE-027) | §11.6, §11.10 |
| **UI-6** | WCAG 2.1 AA: the changes panel is a labelled list, not colour alone; the POS scan field keeps a visible focus ring and announces each scanned item in a live region; quantity steppers are real buttons with accessible names; the countdown announces at 6 h, 1 h and expiry, not continuously; status is text plus icon | §11 |
| **NFR-USA-2 (spirit)** | A counter sale of three scanned items and a cash tender completes **keyboard-only, without leaving the scan field**, in under 60 s | E-H1 |
| **NFR-USA-4** | Server messages first, mapped fallbacks second; no code, id or stack in the DOM | §11.13 |
| **NFR-USA-5** | Checkout choices survive a validation error, a quote change and a reload (they are in the URL); the return form and POS basket survive a failed submit | §8.3 |
| **NFR-PRF-1 / NFR-PRF-5** | The cart and checkout pages fetch once per step change; the quote is debounced 300 ms on option changes; no waterfall of per-line requests | §8.3 |
| **C-5** | **No card input of any kind.** A lint rule fails the build on `autocomplete="cc-*"`, `cardnumber`, `cvv`, `/card-?number/i` in `web/**` | §12.3, U-X3 |
| **C-6** | Money is rendered from server strings via `formatMoney`; no `Number()`, `parseFloat`, `+`, `*` on an amount anywhere in `web/**` (lint rule) | ADR-FE-092, U-X4 |
| **HW-2** | Barcode scanning is keyboard-wedge input into a focused field; no library, no camera | ADR-FE-095 |
| **HW-1** | The receipt is a print-styled 80 mm route; the browser and OS driver do the printing | ADR-FE-094 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (shop)/                                          ⚠ HO-SHP-002's group (assumed name — §3.3, OQ-1)
    cart/page.tsx                                  cart                              US-ORD-01, 02
  (portal)/
    checkout/page.tsx                              stepper: fulfilment → slot → payment → review   US-ORD-02…07
    checkout/complete/[orderId]/page.tsx           placed; transfer instructions      US-ORD-07, 19
    portal/orders/page.tsx                         my orders                          FR-ORD-13
    portal/orders/[orderId]/page.tsx               detail, cancel, transfer ref, track, re-order   US-ORD-12, 19
    portal/orders/[orderId]/return/page.tsx        return request                     US-ORD-13
    layout.tsx                                     ⚠ nav entry: Orders
  (staff)/staff/
    orders/page.tsx                                queue (UI-4, UI-5)                 US-ORD-09
    orders/awaiting-payment/page.tsx               transfers to confirm/approve       US-ORD-19 AC2
    orders/reconciliation/page.tsx                 reconciliation issues              US-ORD-08 AC2
    orders/[orderId]/page.tsx                      detail + every staff action        US-ORD-09…12, 15, 20
    orders/[orderId]/packing-slip/page.tsx         print                              US-ORD-10 AC2
    fulfilment/pick-list/page.tsx                  print                              US-ORD-10 AC1
    returns/page.tsx · returns/[returnRequestId]/page.tsx   approve, receive, decide  US-ORD-13, 14
    refunds/page.tsx                               refunds due                        US-ORD-15
    pos/page.tsx                                   counter sale                       US-ORD-16
    pos/sales/[orderId]/receipt/page.tsx           receipt print                      US-ORD-16 AC1
    admin/delivery-zones/{page,new,[zoneId]}.tsx   zones                              US-ORD-06
    admin/discount-codes/{page,new,[codeId]}.tsx   codes                              US-ORD-05
    admin/couriers/page.tsx                        couriers                           US-ORD-11
    admin/payment-settings/page.tsx                bank transfer account              US-ORD-19
    customers/[customerId]/page.tsx                ⚠ gains CustomerOrdersPanel (HO-ACC-002's page)
    layout.tsx                                     ⚠ nav entries: Orders, Returns, POS
```

`/checkout` is in `(portal)`: **sign-in is required to place an order** (HO-ORD-001 OQ-1). An
unauthenticated visitor pressing *Checkout* goes to sign-in with `?next=/checkout`; `/checkout` and
`/cart` are added to HO-ACC-002's open-redirect allow-list (ADR-FE-098).

### 8.2 API modules

```
lib/api/cart.ts             get, add line, update line, remove line, merge
lib/api/checkout.ts         quote, place order
lib/api/my-orders.ts        list, detail, transfer reference, cancel, reorder, returns
lib/api/staff-orders.ts     queue, detail, awaiting payment, transfer confirm/approve/reject,
                            packing, dispatch, ready-for-collection, tracking, delivery,
                            delivery failure, reallocate, cancel, pick list, packing slip,
                            manifest, reconciliation issues
lib/api/staff-returns.ts    list, detail, request on behalf, approve, reject, receive, decisions
lib/api/staff-refunds.ts    refunds due, refund options, issue refund
lib/api/pos.ts              sku by barcode, sku search, counter quote, sale, receipt
lib/api/ordering-admin.ts   zones, couriers, discount codes, bank transfer account
```

`lib/api/client.ts` gains **one** capability: an `idempotencyKey` option that sets the
`Idempotency-Key` header (ADR-FE-097). No second `fetch` site.

### 8.3 State

| Kind | Where |
|---|---|
| Cart | `['cart']` — one query for guests and customers; invalidated by every cart mutation and after sign-in |
| `guestCartPending` | A field of the cart response; `useCart` fires the merge mutation **once** when it is true, then invalidates (ADR-FE-089) |
| Checkout choices (fulfilment, addressId, serviceLevel, slotDate, slotWindowStart, pickupBranchId, paymentMethod, discountCode, step) | **URL search params** (ADR-FE-090) — reload-safe, back-button-safe, and a validation error never loses them |
| Quote | `['checkout','quote', params]`, `staleTime: 0`, debounced 300 ms on param change; `placeholderData: keepPreviousData` so the summary does not flash |
| Changes acknowledged | Component state keyed by the quote's `fingerprint`; a new fingerprint clears it (ADR-FE-091) |
| Idempotency key | `useIdempotencyKey()` — `crypto.randomUUID()` per *attempt*, kept across retries of the same payload, regenerated when the payload changes or after a success (ADR-FE-097). Never persisted |
| My orders, order detail | `['me','orders', …]` |
| Staff queue, order detail, awaiting payment | `['staff','orders', filters]` etc., `refetchInterval: 30_000`, active branch in the key |
| POS basket, tenders, scan buffer | Component state in `/staff/pos`; **never** storage (ADR-FE-095) |
| Admin lists | Per resource; mutations invalidate their resource |
| Forms | react-hook-form + zod (shape only — every rule that matters is the server's) |

### 8.4 Money, time and print — `lib/format` additions

- `formatMoney({ amount, currencyCode })` is HO-ADM-002's; **amounts stay strings end to end**. This
  slice adds `formatMoneyRange` only if a design needs it — no other money helper, and **no arithmetic**.
- `formatOrderWhen(instant, zone)` → `Mon 12 Oct, 10:30`; `formatSlot(date, start, end, zone)` →
  `Mon 12 Oct, 10:00–13:00` (branch zone, labelled).
- `useServerNow()` (HO-APT-002) drives the transfer countdown and the "collect by" countdown.
- `components/print/PrintSheet.tsx` — a shared print shell: `@media print` rules, a **Print** button, page
  size `A4` (pick list, packing slip) or `80mm` (receipt), no navigation chrome (ADR-FE-094).

### 8.5 Accessibility

HO-CAT-002 §8.5 in full, plus:

- The **changes panel** is `role="region"` with a heading, an ordered list of changes as sentences, and
  an acknowledgement checkbox that is the labelled control the Place button depends on — never colour or
  position alone.
- The **POS scan field** keeps focus; each accepted scan appends a row and announces *"Added Royal Canin
  Kitten 2 kg, quantity 1"* in a polite live region; an unknown barcode announces the failure and keeps
  the buffer visible.
- Quantity steppers: `−` / `+` buttons with names (*"Decrease quantity of …"*) plus a number input.
- Status and payment-method chips are text first; icons carry `aria-hidden`.
- Countdown regions use `role="timer"` with a separate polite region announcing at 6 h, 1 h and 0.
- Print routes are ordinary accessible documents (tables with captions and headers), not images.

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`. **This handover owns ADR-FE-089 … ADR-FE-098.**

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-089** | **The cart is server state.** One `['cart']` query for guests and customers; the guest identity is the API's `HttpOnly` cookie, which the client never reads; when the cart says `guestCartPending`, `useCart` calls the merge endpoint once and refetches | HO-ACC-002 forbids browser-stored drafts and tokens; a client-side cart would have to be merged in the browser, which cannot see the guest cookie |
| **ADR-FE-090** | **Checkout is one route with a stepper**, every choice in URL search params; the **quote is the single source of truth** for prices, charges, slots, payment availability and whether the order can be placed | NFR-USA-5 across reloads and the back button; a client that computes anything about money or availability will drift from the server |
| **ADR-FE-091** | **Acknowledgement is bound to a fingerprint.** The Place button is disabled until the customer ticks the acknowledgement for *this* quote; any new fingerprint (poll, option change, or `409 CheckoutQuoteChanged`) clears the tick and re-opens the panel | FR-ORD-3 "must acknowledge it before paying" — acknowledging a quote they never saw is the failure this prevents |
| **ADR-FE-092** | **No arithmetic on money in the browser and no payment instrument UI.** Amounts are server strings rendered by `formatMoney`; totals, change and refund suggestions come from the server; lint rules fail the build on money arithmetic and on card-like inputs | C-6, C-5; a rounding difference between client and server is a support incident, and a card field is a compliance incident |
| **ADR-FE-093** | **Staff actions render only from the API's `allowedActions`** (and the queue's `flags[]`); a server `403` renders `PermissionDeniedState` | HO-APT-002 ADR-FE-028; the order status machine is the server's |
| **ADR-FE-094** | **Printables are print-styled routes** (`PrintSheet`) for pick list, packing slip and receipt; no PDF library, no server render | UI-4/HW-1 without a dependency; the browser's print dialog already targets the thermal and A4 printers |
| **ADR-FE-095** | **Barcode scanning is a focused input with burst detection** (characters < 50 ms apart terminated by Enter are a scan; slower typing is manual entry); no scanner library, no camera; the field regains focus after every dialog | HW-2 keyboard wedge; a library would add a dependency for an input event |
| **ADR-FE-096** | **Bank-transfer instructions are a first-class screen**: account details with copy buttons, the **payment reference** (the order number) shown prominently, a countdown to `payByLocal` on the server clock, and a *"I have made the transfer"* action that submits the customer's reference. Nothing here is editable by the client | US-ORD-19 AC1 — the customer is away from the screen when they pay; the reference is what lets staff match it |
| **ADR-FE-097** | **Money submissions are idempotent and single-shot**: one key per attempt (kept across retries of the same payload), buttons disabled while pending, **no automatic retry** anywhere in `client.ts` for these routes, and a `409 RequestInProgress` renders *"This is already being processed."* rather than resubmitting | FR-ORD-20; an auto-retry over a non-idempotent-looking failure is how a customer gets charged twice |
| **ADR-FE-098** | `/cart` is public in the storefront group; `/checkout` is behind the customer guard, reached with `?next=/checkout`; both are added to the open-redirect allow-list | HO-ORD-001 OQ-1 (no guest checkout); HO-ACC-002's guard must stay closed |

### 9.1 Open questions

| # | Question | Default |
|---|---|---|
| OQ-1 | HO-SHP-002's storefront route-group name and header slot | Assume `(shop)`; `CartBadge` and `AddToCartButton` are exported and mounted by them (§3.3) |
| OQ-2 | Cart as a page, a drawer, or both? | **Page** (`/cart`) only — deep-linkable, printable, and one implementation to keep accessible |
| OQ-3 | Should the checkout show a slot picker when the service level has no windows? | No — the step is skipped and the review says *"Delivery in 1–2 days"* from `leadTimeDays` |
| OQ-4 | How prominent is the tracking link when the courier has no URL template? | Tracking number as copyable text, no link |
| OQ-5 | POS: customer attached to every sale, or anonymous by default? | **Anonymous by default**, with an *Attach customer* search (HO-ORD-001 OQ-14); prescription lines force attaching |
| OQ-6 | Should the customer see staff names in the order history? | No — the customer timeline shows status and time only (HO-ORD-001 §12.4) |

---

## 10. Dependencies

**No new package.** A PDF generator, barcode/scanner library, data-grid, money library, state manager,
date picker or timezone library is a §20.6 stop. Date entry uses the in-repo pattern from HO-ADM-002.

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error / permission-denied states.

### 11.1 Cart — US-ORD-01, 02 · `/cart` and `components/cart/**`

| Component | Behaviour |
|---|---|
| ⭐ `CartPage` | Lines with image, product and variant name, cat chip for prescription lines, quantity stepper (max `maxQuantityPerLine`), line subtotal, **Remove** (UI-3 confirm not required — it is reversible by re-adding, and an undo toast is offered). Availability per line: *In stock* / *Only 2 left* / **Unavailable** with the line dimmed and a *Remove* prompt. Price changed since adding → *"Price is now BDT 850.00 (was 800.00)"* as text. Footer: item count, subtotal, *"Delivery and tax are calculated at checkout"*, **Checkout** (signed out → sign-in with `?next=/checkout`) |
| `AddToCartButton` | §3.3. States: idle, pending, *Added* (live region), disabled with reason when the SKU is not purchasable |
| `PrescriptionCatDialog` | Cats of the signed-in customer with their authorisation state from the server's response; a cat without authorisation is disabled with *"No current authorisation for this food"* and a **Book a consultation** link (`/book?catId=…&category=Consultation` — HO-APT-002's route). A signed-out visitor sees *"Sign in to buy prescription food"* |
| `CartBadge` | §3.3 |
| `CartMergeNotice` | After a merge: *"We've added the items from your cart before you signed in."* plus any `droppedLines` listed by name and reason. Dismissible; never auto-hides |
| `EmptyCart` | *"Your cart is empty."* + **Browse the shop** |

### 11.2 Checkout — US-ORD-02…07 · `/checkout`

`CheckoutStepper` — four steps, each addressable (`?step=`), each summarised and re-openable once passed.

| Step | Component | Behaviour |
|---|---|---|
| 1 | `FulfilmentStep` | **Delivery** (saved addresses from HO-ACC-002; **Add an address** opens their form inline and returns the id) or **Pickup** (branches with address, phone and opening hours). When the quote says `delivery.available = false`: *"We don't deliver to {district} yet. You can collect your order instead."* with pickup preselected — **US-ORD-06 AC2**. Recipient name and phone default from the profile and are editable |
| 2 | `SlotStep` | Service levels as radio cards showing **charge** (server string) and lead time; slots for the chosen level as a date strip + windows in branch time. Skipped when the level has no windows (OQ-3) — **US-ORD-03 AC1** |
| 3 | ⭐ `PaymentStep` | One radio group from `paymentOptions[]`: *Cash on delivery*, *Bank transfer*, *Pay when you collect*. Unavailable options are **shown disabled with the server's reason** (*"Cash on delivery isn't available for orders over BDT 5,000.00"*) — **US-ORD-07 AC2**. A short explanation under each. **No card option, no online payment control exists** — **US-ORD-07 AC3** |
| 4 | ⭐ `ReviewStep` | `PrescriptionLinesPanel` (each Rx line's cat; a missing cat blocks with a picker — **US-ORD-03 AC2**), ⭐`ChangesPanel` (§11.3), `DiscountCodeField` (apply/remove; a rejected code shows the server's specific reason inline and the summary is unchanged — **US-ORD-05 AC2**), `PendingPoliciesPanel` (accept via HO-ADM-002's policy component before placing), ⭐`OrderSummary` (§11.4) and **Place order** |

**Place order** sends the quote's `fingerprint` and one idempotency key. Outcomes:

| Response | Behaviour |
|---|---|
| `201` | Navigate to `/checkout/complete/{orderId}`; `['cart']` and `['me','orders']` invalidated |
| `409 CheckoutQuoteChanged` | Re-fetch the quote, re-open the changes panel, clear the acknowledgement, message *"Something changed while you were checking out — please review and place the order again."* — **US-ORD-02** |
| `409 StockNoLongerAvailable` | Same, with the server's product-specific message |
| `400 PaymentMethodUnavailable` / `PolicyAcceptanceRequired` / `PrescriptionCatRequired` | Jump to the owning step with the message on the field |
| `409 RequestInProgress` | *"This order is already being placed."*; the button stays disabled and the page polls the order list once — never resubmits (ADR-FE-097) |

### 11.3 `ChangesPanel` — US-ORD-02

Renders `changes[]` as sentences, grouped by kind, each naming the product:

- *"Royal Canin Kitten 2 kg is now BDT 850.00 (was BDT 800.00)."*
- *"Only 1 of the 3 Cat Litter 10 L you wanted is in stock, so we've reduced the quantity."*
- *"Whiskas Tuna 400 g is no longer available and has been removed."*
- *"Hill's k/d 2 kg needs a current authorisation for Mitu — it has been removed. Book a consultation."*

Below them, one checkbox: **"I've reviewed these changes."** Until it is ticked, **Place order** is
disabled with an explanatory `aria-describedby`. A new `fingerprint` unticks it and announces
*"The order has changed — please review again."* (ADR-FE-091).

### 11.4 `OrderSummary` — US-ORD-04

One definition list, every component on its own row, always all of them (zero rows shown as `BDT 0.00`,
never hidden): *Items subtotal*, *Item discounts*, *Order discount ({code})*, *Delivery ({level})*,
*Tax*, then **Total**. Per-line tax rate is shown in the line list. **Every value is a server string**
(ADR-FE-092). Reused verbatim in the order detail, the staff order detail and the POS.

### 11.5 Order placed and transfer instructions — US-ORD-07, 19 · `/checkout/complete/[orderId]`

| Component | Behaviour |
|---|---|
| `OrderPlaced` | Order number, summary, what happens next per payment method (*"Pay the courier in cash when your order arrives."* / *"Collect and pay at {branch}."*), links to the order and the shop |
| ⭐ `BankTransferInstructions` | Account name, number, bank, branch, routing — each with a **Copy** button and a visible value; **Payment reference: ORD-2026-000123** (copy); amount due; *"Transfer by {payByLocal}"* with a countdown on the server clock; **I've made the transfer** opens `TransferReferenceForm` (their bank's reference, ≤ 100 chars) → `204` → *"Thanks — we'll confirm within one working day."* (ADR-FE-096) — **US-ORD-19 AC1** |

### 11.6 My orders — FR-ORD-13, US-ORD-12, 19 · `/portal/orders/**`

| Component | Behaviour |
|---|---|
| `MyOrdersList` | Rows: order number, date (branch zone), status as text + chip, item count, total, payment method. Filter by status; paginated (UI-4). Empty state links to the shop |
| ⭐ `OrderDetail` | Summary (§11.4), lines with cat chips, fulfilment (address or pickup branch, slot), `OrderTimeline` (status, when — no staff names, OQ-6), invoice number when issued, amounts paid/refunded. Conditional panels: `BankTransferInstructions` while `PendingPayment`; **Track your parcel** (courier name, tracking number, link when the courier has a URL, last tracking status) when dispatched; *"Ready for collection at {branch} by {collectBy}"*; refunds list; returns list |
| `CancelOrderAction` | Shown only when `canCancel`. UI-3 confirm naming the order and *"your reserved items are released"*; when money was received it adds *"Any money you have paid will be refunded by the shop."* After `400 CancellationNotAvailable` → `ContactShopCard` with the branch phone and email (`tel:`/`mailto:` from `GET /api/branches`) — **US-ORD-12 AC2** |
| `ExpiredOrderCard` | For a `Cancelled` order with reason *payment window expired*: *"We couldn't confirm your transfer in time, so the items were released."* + **Order again** → the re-order endpoint → cart with a summary of what could not be added — **US-ORD-19 AC3** |
| `ReturnEntry` | **Request a return** when `canRequestReturn`; otherwise the reason (window closed / not delivered) |

### 11.7 Return request — US-ORD-13 · `/portal/orders/[orderId]/return`

`ReturnRequestForm` — returnable lines only, each with quantity (≤ `maxQuantity`), **reason** (radio,
the server's enum with plain labels), **condition** (*Sealed / unopened*, *Opened*, *Used*) and an
optional note. Inline policy text under food and litter lines: *"Opened food can only be returned if
there's a quality problem or a recall."* (BR-10, not enforced client-side). Submit → `201` → status
page; `400` per line renders the server's message against that line — **US-ORD-13 AC2**.

### 11.8 Staff order queue and detail — US-ORD-09, 11, 12, 15, 20 · `/staff/orders/**`

| Component | Behaviour |
|---|---|
| ⭐ `OrderQueue` | Filters: status, fulfilment, payment method, channel, date range, search (number, name, phone); sortable by placed-on and age; paginated (UI-4). Row flags as text chips: *Awaiting approval*, *Transfer reported*, *Courier says delivered*, *Collection overdue*, *Customer anonymised*. Polls 30 s (UI-5) without disturbing an open dialog. Below 768 px: cards |
| ⭐ `StaffOrderDetail` | Customer (link to HO-ACC-002's page), fulfilment, summary (§11.4), lines with **batch numbers and expiry** and cat chips, payments, credit notes, invoice number, status history with staff names, reconciliation flag. Action bar rendered **only from `allowedActions`** (ADR-FE-093): *Start packing*, *Dispatch*, *Ready for collection*, *Record delivery*, *Record delivery failure*, *Cancel*, *Confirm transfer*, *Approve / Reject payment*, *Issue refund*, *Reallocate line* |
| `DispatchDialog` | Courier (active couriers), tracking number (optional, with *"Enter it later"* explained), confirm. `400 BatchRecalled` / `BatchExpired` / `OrderNotFullyAllocated` → the server's message with a **Reallocate** shortcut on the affected line — **US-ORD-11 AC1, BR-14** |
| `TrackingDialog` | Later tracking entry (US-ORD-11 AC2) |
| ⭐ `RecordDeliveryDialog` | Delivered-on (defaults to now, branch zone). COD orders: **Cash collected** (amount, pre-filled from the order total but editable), collector (*Courier* with name, or *Staff member* with a search), collection date — all required; confirm dialog restates the amount (UI-3) — **US-ORD-20 AC2**. Pickup + pay-on-collection: tender (*Cash* / *Card in store*), amount = balance due (read-only), **terminal slip reference** required for card. Idempotency key per attempt |
| `DeliveryFailureDialog` | Reason (*Refused*, *Undeliverable*, *Uncollected* — only those the order allows) and note; confirm explains *"a return will be opened and the parcel must be received into quarantine"* — **US-ORD-20 AC3** |
| `ReallocateLineDialog` | Target batch (from the server's error context or the line's alternatives) and a **mandatory reason** — US-IMP-07 AC2 |
| `CancelOrderDialog` (staff) | Reason required; states what is released and whether a refund becomes due |

### 11.9 Money screens — US-ORD-19 AC2, US-ORD-08 AC2, US-ORD-15

| Component | Behaviour |
|---|---|
| ⭐ `AwaitingPaymentList` (`/staff/orders/awaiting-payment`) | Transfer orders: number, customer, amount due, due-by with countdown, the customer's reported reference, and *Awaiting approval* rows separated. **Confirm transfer** opens `ConfirmTransferDialog`: amount (pre-filled, must match), bank reference, received-on; confirm dialog restates *"Confirm BDT 1,927.50 received against ORD-…?"*. Response `awaitingApproval: true` → *"Recorded. A second staff member must approve it before the order is confirmed."* — **US-ORD-19 AC2** |
| `ApprovePaymentPanel` | On the order detail for a held payment: who recorded it, when, amount, reference; **Approve** / **Reject** (note). A `400 SecondApproverMustDiffer` renders *"Someone else must approve a payment you recorded."* |
| `ReconciliationIssuesList` (`/staff/orders/reconciliation`) | Open issues: raised on, kind, order link, the payment details that were entered, who raised it; **Resolve** with a mandatory note — **US-ORD-08 AC2** |
| ⭐ `RefundDialog` | From the order detail or `/staff/refunds`: allowed methods only (server), suggested amount and `maxRefundable` shown, amount, reference (required for cash/transfer), reason. Confirm restates method and amount (UI-3). `RefundMethodUnavailable` → *"Store credit isn't available yet."* — **US-ORD-15 AC1** |
| `RefundsDueList` (`/staff/refunds`) | Orders with money owed and received returns awaiting a refund; links to the dialog |

### 11.10 Fulfilment and returns — US-ORD-10, 13, 14

| Component | Behaviour |
|---|---|
| ⭐ `PickListPage` | Selected orders or all confirmed/packing at the active branch; grouped **by location, then batch**, each batch row showing SKU, batch number, expiry, total quantity and the orders needing it; **Print** (`PrintSheet`, A4) and **Start packing** for the listed orders (bulk, one request per order with progress and per-order failures listed) — **US-ORD-10 AC1** |
| `PackingSlipPage` | `PrintSheet` A4: order number, date, customer and recipient, address or pickup branch, slot, lines with quantity, batch number and cat name, COD amount due prominently, notes — **US-ORD-10 AC2** |
| `ManifestDownload` | Courier + date → downloads the CSV through `client.ts` (no second fetch site) — **US-ORD-11 AC2** |
| `ReturnsList` / ⭐`ReturnDetail` | Status filter; detail shows the order, lines with reason, condition, eligibility notes and **batch status** (recalled / defect recorded). **Approve** requires ticking *Defect confirmed* with a note on every line flagged `requiresDefectConfirmation` (the button explains why it is disabled) — **US-ORD-13 AC2**. **Reject** needs a note. **Receive** takes quantities received per line and states *"Received items go to quarantine."* — **US-ORD-14 AC1** |
| ⭐ `StockDecisionPanel` | Per received line: **Restock** (to its original batch — the batch number is shown) or **Write off**, with a mandatory reason; confirm restates both counts. A server refusal (expired or recalled batch) is shown on the line — **US-ORD-14 AC2** |

### 11.11 Point of sale — US-ORD-16 · `/staff/pos`

| Component | Behaviour |
|---|---|
| ⭐ `PosScanField` | Always-focused input; burst detection (ADR-FE-095); Enter → lookup by barcode → row appended with product, variant, unit price, **batch to be used and its expiry**; unknown barcode → live-region announcement and a *Search by name* fallback. Focus returns after every dialog — **US-ORD-16 AC2** |
| `PosBasket` | Rows with quantity stepper, remove, line total; **Void basket** (UI-3 confirm). Prescription SKU → forces *Attach customer* + cat (OQ-5); the server's refusal is shown inline |
| `PosSummary` | `OrderSummary` (§11.4) from `POST /api/staff/pos/quote`, debounced 300 ms |
| ⭐ `PosTenderPanel` | Tender rows: **Cash** (amount tendered; the server returns `changeDue` — **never computed here**) and **Card in store** (amount + **terminal slip reference**, required). Running "remaining" comes from the server quote; **Complete sale** is disabled until the server says the tenders cover the total (`TendersDoNotCoverTotal` renders inline). One idempotency key per attempt |
| `PosSaleComplete` | Order and invoice number, **change due** in large type, **Print receipt** (opens the receipt route) and **New sale** (resets, focus to scan) |
| `ReceiptPage` | `PrintSheet` 80 mm rendering of the server's receipt data, with the server's 48-column `text` available behind *Plain text* for narrow printers — **US-ORD-16 AC1** |

**Keyboard-only path** (NFR-USA-2 spirit): scan, scan, scan → `F2` (or *Tender*) → amount → Enter →
Enter (confirm) → receipt. No pointer needed, no focus loss.

### 11.12 Administration — US-ORD-05, 06, 11, 19

| Screen | Behaviour |
|---|---|
| `DeliveryZonesPage` / `ZoneForm` | List (name, branch, coverage count, COD, active). Form: name, branch, priority, COD toggle and ceiling, **coverage rows** (district + optional postal codes as chips), **service levels** (Standard/Express: active, base charge, free-over, **weight bands** as rows *up to N g → + charge*, lead time, **slot windows** per weekday). A *Test an address* box calls the resolve endpoint and names the matching zone — **US-ORD-06 AC1** |
| `DiscountCodesPage` / `DiscountCodeForm` | List with code, kind, value, window, uses / limit, active. Form: code (upper-cased as typed), kind, value, minimum spend, scope (order / products / categories with a picker), first-order-only, overall and per-customer limits, validity window, active. Server rejections render on their field — **US-ORD-05 AC1** |
| `CouriersPage` | Name, tracking URL template (must contain `{tracking}`), supports COD, active |
| `PaymentSettingsPage` | Bank transfer account: account name, number, bank, branch, routing, active. UI-3 confirm on save: *"Customers will be asked to transfer to this account."* Shows a preview of the instructions the customer sees |

### 11.13 Error mapping — `lib/api/errors.ts`

Append every HO-ORD-001 §10.4 code with a plain fallback message and the field it belongs to. The
**server's message is preferred** whenever present; `errorCode` never reaches the DOM. Special handling:
`CheckoutQuoteChanged` and `StockNoLongerAvailable` (re-quote + panel), `RequestInProgress` (no
resubmit), `IdempotencyKeyReused` (*"That looked like a different request — please start again."*),
`PolicyAcceptanceRequired` (jump to the policies panel), `CancellationNotAvailable` (contact card).

### 11.14 Customer page panel — HO-ACC-002's page

`CustomerOrdersPanel` — the customer's recent orders (number, date, status, total) with links, plus
**New counter sale for this customer** → `/staff/pos?customerId=…`.

---

## 12. Specified changes — mocks, config and guards

### 12.1 Mocks — `web/mocks/handlers/ordering/*.ts`

Generated from HO-ORD-001 §12.4. Fixtures must include: an empty cart, a guest cart with a merge
pending and a dropped line, a cart with a price change, a limited-stock line, an unavailable line and a
prescription line (authorised and lapsed); quotes for delivery inside a zone, outside every zone, with
slots and without, with COD unavailable for three different reasons, with each discount rejection
reason, with pending policies; placement `201`, `409 CheckoutQuoteChanged`, `409
StockNoLongerAvailable`, `409 RequestInProgress`; a transfer order with instructions and a 30-second
countdown (to exercise expiry quickly) plus an expired one; orders in every status with and without
tracking; a COD order ready for delivery; staff queue rows carrying each flag; a held payment recorded
by the signed-in user and by someone else; a reconciliation issue; pick list across two locations and
three batches; returns requiring defect confirmation, received and awaiting decisions; refund options
for each payment method including store credit unavailable; POS lookups (found, unknown barcode,
prescription SKU), a counter quote and a sale with change due; every `403`.

### 12.2 Config and routing

- `/cart` and `/checkout` added to HO-ACC-002's `?next=` allow-list (ADR-FE-098) — the only change to
  `lib/routing/**`.
- `client.ts` gains the `idempotencyKey` option and a CSV/blob download helper (§11.10) — no new call
  site.
- Nav entries: portal *Orders*; staff *Orders*, *Returns*, *POS*; staff admin *Delivery zones*,
  *Discount codes*, *Couriers*, *Payment settings*.

### 12.3 Lint rules (the two that carry compliance)

`eslint.config.mjs` is **forbidden scope**; instead both rules live in `web/tests/guards/` as Vitest
source scans that fail the suite:

1. **No card UI** — no `autocomplete="cc-*"`, no identifier matching `/card ?number|cardnum|cvv|cvc|expiry ?date|pan\b/i`
   in `web/app/**` or `web/components/**` (C-5).
2. **No money arithmetic** — no `Number(`, `parseFloat(`, `+`, `-`, `*`, `/` applied to a value whose
   identifier matches `/amount|total|price|charge|discount|tax|refund|change/i` in `web/**` except
   `lib/format/money.ts` (C-6).

---

## 13. Allowed scope

```
web/app/(shop)/cart/**                                        ← new page in HO-SHP-002's group (§3.3)
web/app/(portal)/checkout/**
web/app/(portal)/portal/orders/**
web/app/(portal)/layout.tsx                                   ← one nav entry only
web/app/(staff)/staff/orders/**
web/app/(staff)/staff/fulfilment/**
web/app/(staff)/staff/returns/**
web/app/(staff)/staff/refunds/**
web/app/(staff)/staff/pos/**
web/app/(staff)/staff/admin/{delivery-zones,discount-codes,couriers,payment-settings}/**
web/app/(staff)/staff/customers/[customerId]/page.tsx         ← CustomerOrdersPanel mount only (HO-ACC-002's page)
web/app/(staff)/layout.tsx                                    ← nav entries only
web/components/{cart,checkout,orders,fulfilment,returns,refunds,pos,ordering-admin,print}/**
web/lib/api/{cart,checkout,my-orders,staff-orders,staff-returns,staff-refunds,pos,ordering-admin}.ts
web/lib/api/client.ts                                         ← idempotencyKey option and blob download only
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/lib/routing/**                                            ← /cart and /checkout allow-list entries only
web/lib/format/**                                             ← §8.4 additions only
web/lib/hooks/**                                              ← new hooks only (useCart, useCheckoutQuote, useIdempotencyKey, useScanner)
web/lib/validation/**
web/mocks/handlers/ordering/** · web/mocks/handlers/index.ts
web/tests/components/{cart,checkout,orders,fulfilment,returns,refunds,pos,ordering-admin}/**
web/tests/guards/**                                           ← §12.3
web/tests/e2e/ordering-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-089…098
docs/test-cases/TC-HO-ORD-002.md · docs/gates/HO-ORD-002-*.md
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**` | HO-ORD-001 |
| `.ai/**`, `.claude/**`, `docs/**` except the two named paths | Inputs and agents |
| Every other HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002, HO-APT-002 file beyond the lines in §13 | Their areas |
| `web/app/(shop)/**` except `cart/**`, and HO-SHP-002's product, listing and header files | Theirs — they mount our two components (§3.3) |
| `web/package.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs`, `tailwind` config | No dependency, no build change (§10) |

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| **A — cart** | | |
| U-A1 | Cart with a changed price, a limited line and an unavailable line | Each stated as text; the unavailable line cannot be checked out and offers removal |
| U-A2 | Quantity stepper beyond `maxQuantityPerLine` | Blocked with a message; server value respected |
| U-A3 | `guestCartPending: true` | The merge endpoint is called **once**; the cart refetches; `CartMergeNotice` lists dropped lines — **US-ORD-01 AC1** |
| U-A4 | `AddToCartButton` for a prescription SKU, signed out / signed in | *"Sign in to buy prescription food"* / cat dialog with unauthorised cats disabled + booking link |
| U-A5 | Checkout pressed while signed out | Sign-in with `?next=/checkout`; allow-list unchanged otherwise |
| **B — checkout** | | |
| U-B1 | Quote with `delivery.available = false` | Pickup preselected with the explanation — **US-ORD-06 AC2** |
| U-B2 | Service levels and slots | Charges and lead times from the server; slot labels in branch time; no slots → the step is skipped |
| U-B3 | Payment options with COD unavailable for zone / ceiling / disabled | Three distinct disabled reasons rendered — **US-ORD-07 AC2** |
| U-B4 | The whole checkout DOM | **No card input, no `cc-*` autocomplete, no "pay online" control** — **US-ORD-07 AC3** |
| U-B5 | `ChangesPanel` with one change of each kind | Sentences naming products; Place disabled until acknowledged — **US-ORD-02** |
| U-B6 | Quote refetch returns a new fingerprint while acknowledged | Tick cleared, announcement made, Place disabled |
| U-B7 | `OrderSummary` | Six rows always present; values are the server's strings; no arithmetic in the component — **US-ORD-04 AC1** |
| U-B8 | Discount code rejected with `DiscountMinimumSpendNotMet` | The server's message inline; totals unchanged — **US-ORD-05 AC2** |
| U-B9 | Rx line without a cat | Place blocked; the picker is focused — **US-ORD-03 AC2** |
| U-B10 | Reload mid-checkout | Every choice restored from the URL (NFR-USA-5) |
| U-B11 | Place → `409 CheckoutQuoteChanged` | Re-quote, panel re-opened, tick cleared, one message |
| U-B12 | Place double-clicked | One request; button disabled while pending; the same idempotency key on a manual retry — **US-ORD-18 AC1** |
| U-B13 | `409 RequestInProgress` | No resubmit; explanatory message |
| **C — orders** | | |
| U-C1 | Detail while `PendingPayment` | Instructions, reference, copy buttons, countdown from the server clock — **US-ORD-19 AC1** |
| U-C2 | Countdown announcements | 6 h, 1 h, 0 only |
| U-C3 | `canCancel` true / false | Cancel with UI-3 confirm / contact card with `tel:` — **US-ORD-12** |
| U-C4 | Cancel of a paid order | The confirm mentions the refund |
| U-C5 | Expired order | Explanation + **Order again**; unavailable items listed after the call — **US-ORD-19 AC3** |
| U-C6 | Dispatched order | Tracking number, courier, link only when a URL exists (OQ-4) |
| U-C7 | Return form: sealed line; opened food line | Policy text shown; a server `400` renders against the line — **US-ORD-13** |
| **D — staff orders** | | |
| U-D1 | Detail renders actions from `allowedActions` only | No hardcoded action appears |
| U-D2 | Queue polls while a dialog holds values | Values kept, data behind updated (UI-5) |
| U-D3 | `DispatchDialog` → `400 BatchRecalled` | Message plus a **Reallocate** shortcut on the line |
| U-D4 | `RecordDeliveryDialog` for COD | Amount, collector and date required; confirm restates the amount — **US-ORD-20 AC2** |
| U-D5 | Pay-on-collection card tender without a slip reference | Blocked with the server's message |
| U-D6 | `DeliveryFailureDialog` | Only the reasons the order allows; explains the quarantine path — **US-ORD-20 AC3** |
| U-D7 | `ConfirmTransferDialog` → `awaitingApproval: true` | Second-approval message; the order stays in the list flagged |
| U-D8 | Approve a payment you recorded | `SecondApproverMustDiffer` message |
| U-D9 | Reconciliation list | Entered payment details shown; resolve requires a note — **US-ORD-08 AC2** |
| U-D10 | Queue at 360 px | Cards, no horizontal scroll |
| **E — fulfilment and returns** | | |
| U-E1 | Pick list grouped by location then batch | Headings and quantities as delivered; print sheet has no navigation — **US-ORD-10 AC1** |
| U-E2 | Packing slip | Batch numbers, recipient, COD amount — **US-ORD-10 AC2** |
| U-E3 | Return detail with `requiresDefectConfirmation` | Approve disabled until every flagged line has a confirmed defect and note — **US-ORD-13 AC2** |
| U-E4 | Receive form | Quantities ≤ approved; the quarantine sentence present — **US-ORD-14 AC1** |
| U-E5 | Stock decision per line | Restock names the original batch; write-off requires a reason; the confirm restates both — **US-ORD-14 AC2** |
| U-E6 | `RefundDialog` per payment method | Only the allowed methods; suggested amount from the server; store credit unavailable message — **US-ORD-15 AC1** |
| **F — POS** | | |
| U-F1 | Scan burst vs typed input | Burst → lookup; slow typing → no lookup until Enter |
| U-F2 | Unknown barcode | Announced; search fallback offered; focus kept |
| U-F3 | Prescription SKU without a customer | Server refusal inline; attach-customer offered |
| U-F4 | Tender panel: cash over total; card over the balance | `changeDue` from the server; the card amount refused by the server's message — **no client arithmetic** |
| U-F5 | Complete sale double-submit | One request (idempotency key) |
| U-F6 | Receipt route | 80 mm print sheet; plain-text alternative available |
| **X — cross-cutting** | | |
| U-X1 | Every form: server `400` | Mapped to its field; `errorCode` absent from the DOM |
| U-X2 | Every staff screen with `403` | `PermissionDeniedState` |
| U-X3 | **Guard scan:** card-like inputs | None in `web/**` — **C-5** |
| U-X4 | **Guard scan:** money arithmetic | None outside `lib/format/money.ts` — **C-6** |
| U-X5 | Dirty forms + navigate | `UnsavedChangesGuard` (POS basket included) |

## 16. End-to-end test cases

MSW first, then the real API; both runs recorded.

| ID | Case | Expected |
|---|---|---|
| E-A1 | Guest adds two items, signs in, reaches the cart | Items present, merge notice shown, no duplicate line — **US-ORD-01 AC1** |
| E-A2 | Same customer in a second browser context | The cart is there — **US-ORD-01 AC2** |
| E-B1 | **AC-6 / AC-8**: checkout to delivery with COD, place, staff pack, dispatch with tracking, record COD cash, customer sees delivered | Order confirmed without prepayment; tracking visible; cash recorded with collector and date — **US-ORD-20** |
| E-B2 | Price changes between cart and checkout (real API: change the price through HO-ADM/SHP) | Change listed, acknowledgement required, order placed at the new price — **US-ORD-02** |
| E-B3 | Address outside every zone | Delivery unavailable message, pickup completes the order — **US-ORD-06 AC2** |
| E-B4 | COD above the ceiling | COD disabled with the reason; bank transfer completes — **US-ORD-07 AC2** |
| E-B5 | Discount code: invalid, then valid | Specific reason; then the summary shows the order discount — **US-ORD-05** |
| E-B6 | **AC-7**: prescription diet with an authorised cat; then with the authorisation expired (real API) | Placed with the cat on the line; second attempt blocks the line with the reason — **US-SHP-10 AC3** |
| E-B7 | Two contexts check out the last unit | One succeeds; the other sees the sold-out message and recovers |
| E-B8 | Place order with a double-click and a manual retry | Exactly one order in *My orders* — **US-ORD-18 AC1** |
| E-C1 | **AC-8b**: bank transfer — instructions, submit reference, staff confirms | Order confirmed; customer sees it and the inbox message (real API) — **US-ORD-19 AC1, AC2** |
| E-C2 | Transfer left unconfirmed past the window (mock 30 s; real API: worker cycle) | Order expired, explanation and **Order again** repopulates the cart — **US-ORD-19 AC3** |
| E-C3 | Transfer above the approval threshold: one staff confirms, a second approves | Held then confirmed; the first cannot approve — **FR-BIL-12** |
| E-D1 | Cancel before packing; try again after packing (second order) | Released with confirmation; contact card — **US-ORD-12** |
| E-D2 | **AC-9**: delivered order → return a sealed item → staff approve → receive → restock; then attempt an opened food return | Refund issued and shown to the customer; opened food refused with the policy reason — **US-ORD-13, 14, 15** |
| E-D3 | Refused COD parcel | Returns path opened, received into quarantine, written off — **US-ORD-20 AC3** |
| E-E1 | Pick list for three orders, print preview, start packing | Grouped by location and batch; orders move to packing — **US-ORD-10** |
| E-E2 | Manifest CSV download | File contains the dispatched orders with COD amounts — **US-ORD-11 AC2** |
| E-F1 | **NFR-USA-2 spirit**: counter sale — scan three items, cash tender, complete, print receipt | Under 60 s measured by the test, keyboard only; receipt shows lines, tax, total, change — **US-ORD-16** |
| E-F2 | Counter sale of a prescription SKU | Blocked until a customer and cat are attached |
| E-X1 | axe on every route in §8.1, including the changes panel, the countdown, the POS with a scan in progress and each print route | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |
| E-X3 | Whole-app DOM and network scan during E-B1 and E-F1 | No card field, no gateway origin, no money arithmetic in shipped bundles — **C-5, C-6** |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-ORD-001 §12.4; MSW handlers; `client.ts` idempotency option;
   `lib/format` additions; `useIdempotencyKey`; the two guard scans (§12.3) **first**, so they fail
   before any offending code is written.
3. Cart + `AddToCartButton` + `CartBadge` + merge.
4. Checkout stepper → quote → changes panel → summary → placement → placed page and transfer
   instructions.
5. My orders → detail → cancel → transfer reference → re-order → return request.
6. Staff queue → detail → status actions → dispatch/tracking → delivery and failure dialogs.
7. Awaiting payment → approval → reconciliation issues → refunds.
8. Pick list, packing slip, manifest, returns and stock decisions.
9. POS: scan → basket → tenders → receipt.
10. Administration: zones, discount codes, couriers, payment settings; customer panel.
11. U-series, E-series on MSW, then E-series against the real API per HO-ORD-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | ORD-01 AC1 | The guest cart merges on sign-in with no duplicate lines | U-A3, E-A1 |
| A2 | ORD-01 AC2 | The cart is there on another device | E-A2 |
| A3 | ORD-02 | Price and availability changes are shown explicitly and must be acknowledged before placing | U-B5, U-B6, U-B11, E-B2 |
| A4 | ORD-03 AC1 | Saved address or pickup, plus a delivery slot where applicable | U-B1, U-B2, E-B1, E-B3 |
| A5 | ORD-03 AC2 | A prescription line needs its cat before proceeding | U-B9, E-B6 |
| A6 | ORD-04 AC1 | Line subtotals, line discounts, order discount, delivery and tax each shown before the total | U-B7, E-B1 |
| A7 | ORD-04 AC2 | No money arithmetic happens in the browser | U-B7, **U-X4**, E-X3 |
| A8 | ORD-05 AC2 | A rejected code shows the specific reason | U-B8, E-B5 |
| A9 | ORD-06 AC1 | The delivery charge is shown before payment | U-B2, E-B1 |
| A10 | ORD-06 AC2 | Outside every zone: explained, pickup offered | U-B1, E-B3 |
| A11 | ORD-07 AC1 | COD, bank transfer and pay-on-collection offered as configured | U-B3, E-B1, E-C1 |
| A12 | ORD-07 AC2 | COD hidden with its reason | U-B3, E-B4 |
| A13 | ORD-07 AC3 | No card number or token is ever entered or stored by the client | **U-B4, U-X3**, E-X3 |
| A14 | ORD-19 AC1 | Bank details and reference shown with the hold's deadline | U-C1, U-C2, E-C1 |
| A15 | ORD-19 AC2 | Staff confirmation (with second approval above the threshold) confirms the order | U-D7, U-D8, E-C1, E-C3 |
| A16 | ORD-19 AC3 | Expiry is explained and re-ordering is one action | U-C5, E-C2 |
| A17 | ORD-20 AC2 | Cash collected, collector and date recorded at delivery | U-D4, E-B1 |
| A18 | ORD-20 AC3 | A refused parcel goes through the returns path | U-D6, E-D3 |
| A19 | ORD-08 AC2 | A reconciliation issue is visible and resolvable | U-D9 |
| A20 | ORD-09 | Only valid actions are offered; the queue stays current without losing input | U-D1, U-D2 |
| A21 | ORD-10 | Pick list grouped by location and batch; packing slip prints | U-E1, U-E2, E-E1 |
| A22 | ORD-11 | Tracking is stored and shown; manifest downloads; tracking can be entered later | U-C6, U-D3, E-E2 |
| A23 | ORD-12 | Cancel before packing; contact the shop after | U-C3, U-C4, E-D1 |
| A24 | ORD-13 | Return requests with reason and condition; the policy is explained and the refusal understood | U-C7, U-E3, E-D2 |
| A25 | ORD-14 | Receipt into quarantine and a per-line restock / write-off decision | U-E4, U-E5, E-D2 |
| A26 | ORD-15 | Refund by an allowed method with reference and reason | U-E6, E-D2 |
| A27 | ORD-16 | Scan, tender, receipt — keyboard only | U-F1…U-F6, E-F1, E-F2 |
| A28 | ORD-18 AC1 | A double submit creates one order | U-B12, U-F5, E-B8 |
| A29 | UI-5 | Fulfilment screens refresh without losing unsaved input | U-D2 |
| A30 | UI-6 / UI-1 | Zero serious/critical axe violations; 360–1440 px | E-X1, E-X2 |

**Deferred, named in §4**: US-ORD-17 combined invoice (HO-BIL-002/HO-BRD-002); catalogue and product
pages (HO-SHP-002); invoice, payment and reconciliation screens (HO-BIL-002); subscriptions
(HO-SUB-002); portal dashboard (HO-POR-002).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering; no new dependency; **no client-side pricing, availability, tax, discount or eligibility logic**; the quote as the single source of truth; hooks in `lib/hooks/`; one `fetch` site; the scoped edits to HO-ACC-002's customer page, the layouts and HO-SHP-002's group match §13 |
| 2 | SECURITY | `security-reviewer` | **No card or gateway UI anywhere (U-X3, E-X3)**; the guest cart token never read or stored by JS; CSRF header on every write; `?next=` allow-list unchanged beyond the two entries; idempotency keys never persisted and never reused across payloads; no automatic retry on money routes; server messages rendered as text (no HTML); order and return ids from the API only |
| 3 | CODE_REVIEW | `code-reviewer` | Money as strings through `formatMoney` (U-X4); time only through `lib/format` and `useServerNow`; polling never resets forms, dialogs or the scan session; component size; print routes share `PrintSheet` |
| 4 | TEST | `test-engineer` | `TC-HO-ORD-002.md` from §15/§16 first; mock and real runs recorded separately; E-B7 and E-B8 with real parallelism; E-F1 measures time |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; the US-ORD-17 deferral and the HO-SHP-002 seam assumption (OQ-1) recorded, not passed silently |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, the HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002, HO-NTF-002,
HO-APT-002). Do not build a parallel shell, session guard, address form or format helper. If HO-SHP-002
is missing, build `/cart` and the two mountable components and report the seam as unverified — do not
create a storefront layout, header or product page.

**20.2 A screen seems to need client-side pricing, tax, availability, discount or eligibility logic.**
Ask the server; if the quote or the detail does not carry it, that is §20.5.

**20.3 A card field, wallet button, gateway redirect or "pay online" control is requested.** It
contradicts SRS §1.2, C-5 and the owner's decision. Report it; build nothing.

**20.4 The real API disagrees with HO-ORD-001 §12.4.** Report it; do not shim, and do not compute the
missing value locally.

**20.5 A screen needs an endpoint or field HO-ORD-001 does not specify.** Report it; do not invent a
mock field — a mock that lies produces a screen that breaks on integration.

**20.6 A new dependency seems necessary** (PDF, scanner, grid, money, date picker — §10). Raise it.

**20.7 A money total, change amount or refund suggestion would have to be computed in the browser.**
Stop: the server owes that number (HO-ORD-001 §8.5, §8.11).
