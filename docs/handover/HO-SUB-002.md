# HO-SUB-002 — Subscriptions (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-SUB-002** |
| Title | Subscriptions — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §11 — **US-SUB-01 … US-SUB-06** |
| SRS | FR-SUB-1 … FR-SUB-6; FR-ORD-4, FR-ORD-8, FR-ORD-22 (as shown to the customer); **UI-1, UI-3, UI-4, UI-6**; NFR-USA-4, NFR-USA-5, NFR-CMP-2; AC-17 |
| Pairs with | [`HO-SUB-001`](HO-SUB-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-SUB-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold), [`HO-ACC-002`](HO-ACC-002.md) (sessions, `client.ts`, route groups, the `?next=` guard, error mapping, address book screens, staff customer page), [`HO-ADM-002`](HO-ADM-002.md) (`lib/format` time and money helpers, the registry-driven settings screen), [`HO-NTF-002`](HO-NTF-002.md) (inbox — the pre-charge notice and its skip link arrive there). **Depends on handovers not yet written**: `HO-SHP-002` (the product page that hosts the **Subscribe** button) and `HO-ORD-002` (the customer order page that cycle history links to) — assumptions in §8.6 |
| Delivery order | Section 14 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | *(none — append rows here)* |

---

## 2. Objective

Give customers a **self-service subscription area** and give staff the **two lists that matter**:
unpaid subscriptions, and prescription-diet subscriptions suspended for a lapsed authorisation.

- **Customers** subscribe to a food or litter SKU from its product page. They choose quantity,
  interval, delivery or pickup, and how they will pay each order. They see the next order date and the
  date after which changes roll to the following delivery. They pause, resume, skip, change or cancel,
  and they follow the skip link in the pre-charge notice straight to a confirmation.
- **Staff** see every subscription, filter and export the list, see the unpaid and prescription
  lists, and reinstate or cancel with a reason.

**There is no payment step and no card field** (HO-SUB-001 ADR-238). Choosing "cash on delivery" or
"bank transfer" records how the customer **intends** to pay each order. The order itself, its bank
details and its payment window belong to Orders (HO-ORD-002).

Three things make this harder than it looks:

1. **The server owns the schedule.** Whether a change hits the next delivery or the following one
   depends on the cut-off, pending changes and the interval. The UI **never adds weeks to a date**. It
   shows `nextDeliveryOn`, `changeCutoffOn`, `isLocked`, `allowedActions` and, after every change, the
   server's `message` naming the affected delivery (ADR-FE-110).
2. **Dates are dates, not instants.** `2026-10-20` rendered through `new Date()` becomes 19 October in
   any zone west of UTC. Every subscription date goes through one date-only formatter (ADR-FE-112).
3. **A link in a message must never act on its own.** The notice's skip link opens a confirmation for
   exactly that delivery. A stale link explains itself instead of skipping the wrong delivery
   (ADR-FE-111).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack; HO-ACC-002's session model, `client.ts`, route groups, `?next=` guard, error
mapping, `PermissionDeniedState`, `UnsavedChangesGuard`, address book screens and staff customer page;
HO-ADM-002's `lib/format` (`formatMoney`, zone-aware instant formatting) and settings screen; HO-NTF-002's
inbox.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-SUB-01 | **Subscribe** form (product summary, quantity, interval, delivery address or pickup branch, payment method, cat for a prescription diet, first order date, estimate) reached from the product page's **Subscribe** button | `/portal/subscriptions/new/[skuId]` |
| US-SUB-01, 02, 05, 06 | **My subscriptions** — live and cancelled, each with status, next order date and any warning | `/portal/subscriptions` |
| US-SUB-02, 03, 05, 06 | **Subscription detail** — next order, cut-off, pending changes, terms, actions (change, skip, pause, resume, cancel, withdraw pending), order history with payment state, suspension explanation; opened with `?skip=yyyy-MM-dd` from the notice | `/portal/subscriptions/[subscriptionId]` |
| US-SUB-03 | No screen of its own — the notice arrives in the inbox (HO-NTF-002) and links to the detail page's skip confirmation | — |
| US-SUB-04 | No new screen — the shop manager sets `subscription.discountPercent` in HO-ADM-002's settings screen, which renders every registry entry. The discount appears in the subscribe estimate and the detail | — |
| US-SUB-05, 06 | **Staff subscriptions** — tabs *All*, *Unpaid* (US-SUB-06 AC2), *Prescription suspended* (US-SUB-05); filters, sort, pagination, CSV export (UI-4) | `/staff/subscriptions` |
| US-SUB-05, 06 | **Staff subscription detail** — customer, terms, full order history, reinstate and cancel | `/staff/subscriptions/[subscriptionId]` |
| — | **Subscriptions panel** on HO-ACC-002's staff customer page | `/staff/customers/[customerId]` |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-SUB-001 owns `src/**` |
| **Any card, wallet, token, "save payment method" or auto-charge control** | No gateway in v1 (HO-SUB-001 ADR-238, SRS Phase 5). A §20.3 stop |
| Showing bank details, confirming a transfer, the order page itself | HO-ORD-002 — the cycle history links to it |
| The invoice showing the discount | HO-ORD-002 / HO-BIL-002 |
| Client-side schedule arithmetic: cut-off, following delivery, skip dates, notice dates | Server-owned (ADR-FE-110) |
| COD eligibility, delivery zones, delivery charges, tax | HO-ORD-001's, surfaced through HO-SUB-001's payment-methods endpoint |
| The portal dashboard tile | HO-POR-002 |
| The subscription-health report | HO-RPT-002 |
| Any verification or OTP step in subscribing | HO-ACC-002 §4.1 — identifier + password only |
| A date-picker, calendar, state or time-zone library | §10 |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002, HO-ADM-002, HO-NTF-002 built** | ❌ at baseline — §20.1 |
| **HO-SHP-002 built** (product page) | ❌ handover not written — §20.1. Only the **Subscribe** button insertion depends on it. The subscribe route works by URL without it |
| **HO-ORD-002 built** (customer order page) | ❌ handover being written in parallel. Only the cycle-history order link depends on it (§8.6) |
| HO-SUB-001 API | ❌ — build against MSW generated from §12.4 there, then re-run every E-case against the real API |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-SUB-001`](HO-SUB-001.md) | **§12.4** endpoints and shapes (`SubscriptionView`, `SubscriptionChangeResult`); §10.4 codes and their messages; §8.4 the status machine; §8.5 what "locked" means, so the copy can explain it; §8.9 what "unpaid" means; §12.3 who may do what |
| [`HO-ACC-002`](HO-ACC-002.md) | §8.4 sessions; §11.2 client; §11.5 the customer guard and its `?next=` rule (pathname only, `/portal/` prefix); §11.7 address book (the "Add an address" path); §11.14 error mapping; the staff customer page |
| [`HO-ADM-002`](HO-ADM-002.md) | §8.4 `lib/format` (`formatMoney`, zone-aware instants); ADR-FE-014 zone labelling |
| [`HO-NTF-002`](HO-NTF-002.md) | How inbox items open their `linkPath` — the skip link lands on this slice's detail page |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.4 | UI-1/3/4/6; NFR-USA-4/5 |

---

## 7. Build rules

| Req | Rule | Where |
|---|---|---|
| **UI-1 / NFR-CMP-2** | 360 px floor. The subscribe form is one column; the detail stacks panels; order history and staff tables become cards below 768 px with no horizontal scroll | §11 |
| **UI-3** | Confirm, stating what is affected: **skip** (*"Skip the delivery on Tue 20 Oct?"*), **pause** (and when locked: *"Your order on Tue 20 Oct is already being prepared and will still be placed. Nothing after it will be ordered until you resume."*), **cancel** (and when locked: *"Your order on Tue 20 Oct will still be placed; the subscription ends after it."*), **withdraw pending changes**, staff **cancel** and **reinstate** (*"Restart Farhana's subscription? Orders resume from {date}."*). The "when locked" wording is chosen from the server's `isLocked`, never from a client date comparison | §11.3, §11.5 |
| **UI-4** | Staff list: filter, sort, paginate, **CSV export through the server endpoint** | §11.4 |
| **UI-6** | WCAG 2.1 AA. Status and payment state as **text**, never colour alone; every dialog focus-trapped with its heading announced; the change `message` read by a live region; the date field is a native `<input type="date">` with a visible label and the allowed range stated in text | §11 |
| **NFR-USA-4/5** | Server messages in plain language; the subscribe form keeps its values across validation errors and across the "Add an address" detour | HO-ACC-002 §11.14 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (portal)/
    portal/subscriptions/page.tsx                     my subscriptions             US-SUB-01, 02, 05, 06
    portal/subscriptions/new/[skuId]/page.tsx         subscribe                    US-SUB-01, 04
    portal/subscriptions/[subscriptionId]/page.tsx    detail (?skip=yyyy-MM-dd)    US-SUB-02, 03, 05, 06
  (portal)/layout.tsx                                 ⚠ nav entry: Subscriptions
  (staff)/staff/
    subscriptions/page.tsx                            All · Unpaid · Prescription  US-SUB-05, 06
    subscriptions/[subscriptionId]/page.tsx           staff detail                 US-SUB-05, 06
    customers/[customerId]/page.tsx                   ⚠ gains SubscriptionSummaryPanel (HO-ACC-002)
  (staff)/layout.tsx                                  ⚠ nav entry: Subscriptions
  <HO-SHP-002 product page>                           ⚠ renders <SubscribeButton skuId> (OQ-1)
```

**Why the SKU is a path segment, not a query parameter.** HO-ACC-002's `CustomerSessionGuard` sends
an unauthenticated visitor to `/sign-in?next=` built from the **pathname only**, and honours only paths
starting `/portal/` (HO-ACC-002 §11.5). A visitor who presses **Subscribe** while signed out therefore
comes back to `/portal/subscriptions/new/{skuId}` with the SKU intact. A `?skuId=` would be dropped.
No change to HO-ACC-002's guard is needed.

`new` is a static segment, so Next.js matches `/portal/subscriptions/new/{skuId}` before the dynamic
`[subscriptionId]` route. A subscription id can never be the literal `new`, because ids are GUIDs.

The **skip link** keeps `?skip=` as a query parameter. It arrives from the inbox, which already
requires a session. If the session has expired, the guard returns the customer to the detail page
without the parameter. The detail page then shows **Skip next delivery** from `allowedActions`, so
nothing is lost but one click (ADR-FE-111).

### 8.2 API modules

`lib/api/subscriptions.ts` — options, payment methods, list, detail, cycles, create, change terms,
skip, pause, resume, cancel, withdraw pending. `lib/api/staff-subscriptions.ts` — list, unpaid list,
export URL builder, detail, reinstate, staff cancel, customer summary. Addresses come from HO-ACC-002's
`account.ts`; branches from HO-ADM-002's public branches module.

### 8.3 State

| Kind | Where |
|---|---|
| Subscribe choices | react-hook-form + zod; `skuId` from the route segment. The form's values survive the "Add an address" detour in `sessionStorage` under one key, **values only (nothing secret, nothing about anyone else)**, cleared on submit or cancel |
| Options, payment methods | `['subscriptions','options', skuId]`; `['subscriptions','payment-methods', skuId, quantity, fulfilmentKey]`. The payment-method list is refetched when quantity or fulfilment changes, debounced 300 ms |
| My subscriptions, detail, cycles | `['me','subscriptions', status]`, `['me','subscriptions', id]`, `['me','subscriptions', id, 'cycles', page]`. **Every mutation replaces the detail cache with the `subscription` in its response**, then invalidates the list |
| Staff lists | `['staff','subscriptions', filters]`, `['staff','subscriptions','unpaid', page]`; filters in URL search params (shareable, reload-safe) |
| Last change message | Component state of the detail page, shown in an `aria-live="polite"` alert until the next action |

No polling. UI-5 does not name subscription screens. The server re-evaluates the lock on every action,
and a change result always states the real outcome, so a page left open across the cut-off cannot
mislead (ADR-FE-110).

### 8.4 Dates and money — `lib/format` additions

- **`formatDateOnly('2026-10-20')` → `Tue 20 Oct`** (and a `long` variant `Tuesday 20 October 2026`).
  It splits the string and formats `Date.UTC(y, m-1, d)` with `timeZone: 'UTC'`. **Never `new
  Date('yyyy-MM-dd')` and never local-time parsing** (ADR-FE-112). `eslint.config.mjs` is forbidden
  scope, so a unit test scans the subscription folders instead (U-X3).
- `changeCutoffOn` and `paymentExpiresOn` are **instants**, formatted with HO-ADM-002's zone-aware
  helper in `commerceTimeZoneId` and labelled with the zone (ADR-FE-014).
- Money: HO-ADM-002's `formatMoney`, amounts kept as strings end to end.

### 8.5 Copy that explains the model

One `SubscriptionCopy` module holds the recurring explanations so every screen says the same thing:

- **How paying works**: *"No card needed. Each delivery creates an order that you pay for by {cash on
  delivery / bank transfer / at the counter}."*
- **Cut-off**: *"Changes made before {Sun 18 Oct, 00:00 (Asia/Dhaka)} apply to this delivery. After
  that, they start with the next one."*
- **Reminder**: *"We'll remind you {n} days before each order."* (`preChargeNoticeDays`)
- **Estimate**: *"Estimated per delivery. Delivery and tax are added when each order is placed, at the
  price on that day."*
- **"Next order"** is the label for `nextDeliveryOn` (HO-SUB-001 OQ-3: the order is raised on that
  date; the parcel follows).

### 8.6 Assumptions about handovers not yet written

| ID | Owner | Assumption | If it does not hold |
|---|---|---|---|
| FA-1 | **HO-SHP-002** | The product page renders a client component slot where `<SubscribeButton skuId={…} />` can be placed. The button itself asks `GET …/options?skuId` and hides when `subscribable` is false, so the page needs to know nothing about subscriptions | The subscribe route still works by URL. Record the page path in §1 Amended before touching it (§13) |
| FA-2 | **HO-ORD-002** | The customer order page is `/portal/orders/[orderId]` and shows the bank details and reference for a held transfer | The history shows the order number without a link, and a §20.5 report is filed |

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing the series. **This handover owns ADR-FE-109 … ADR-FE-118**
(range reserved by the coordinator) and uses **ADR-FE-109 … ADR-FE-114**.

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-109** | Subscriptions are **route-based under `/portal/subscriptions`** (list, `new/[skuId]`, detail). Subscribing is **one page**, not a stepper, and the SKU is a **path segment** so it survives HO-ACC-002's pathname-only `?next=` round trip through sign-in. The only entry from the catalogue is a `SubscribeButton` that HO-SHP-002's product page renders | Six fields fit one screen; a query parameter would be lost at sign-in; the button keeps this slice out of HO-SHP-002's files |
| **ADR-FE-110** | **The server owns the schedule.** Actions render only from `allowedActions` / `allowedStaffActions`; lock wording only from `isLocked` / `changeCutoffOn`; the effect of a change only from `SubscriptionChangeResult.message` and `appliesFromCycleOn`. The client never adds weeks, compares a date to "today" to decide anything, or predicts the following delivery | US-SUB-02 AC2 "I am told which one" — a client guess that disagrees with the server is worse than no guess |
| **ADR-FE-111** | The notice's **skip link** (`?skip=yyyy-MM-dd`) opens a confirmation **for exactly that date** when it equals `skippableCycleOn`. Otherwise it shows *"That delivery can no longer be skipped. The next one you can skip is {date}."* with a **Skip that one** option. Nothing mutates on page load; the parameter is removed from the address bar after it is read | A GET must never change state (a mail scanner or link preview fetches links); a stale link must not skip the wrong delivery |
| **ADR-FE-112** | **Subscription dates are date-only strings**, formatted by `formatDateOnly` in UTC arithmetic; never `new Date('yyyy-MM-dd')`. Instants (`changeCutoffOn`, `paymentExpiresOn`) use the zone-aware helper with `commerceTimeZoneId` | The off-by-one-day bug for any user west of UTC |
| **ADR-FE-113** | **No payment capture anywhere.** The payment method is a stated intention with explanatory copy (§8.5); unavailable methods are shown disabled **with Orders' reason as text**; the estimate is labelled an estimate | HO-SUB-001 ADR-238; US-ORD-07 AC2's "hidden with the reason available" pattern, kept consistent |
| **ADR-FE-114** | Staff lists keep **filters in the URL**, sort and paginate server-side, and export by linking to `export.csv` with the same filters. The client never builds CSV | UI-4; the server already guards against CSV injection (HO-ADM-001 ADR-051) |

| # | Question | Owner | Default |
|---|---|---|---|
| OQ-1 | Where exactly does HO-SHP-002's product page live, and where does the button sit? | HO-SHP-002 author | Beneath the add-to-cart control, labelled **Subscribe & save {n} %** when a discount is set, else **Subscribe** |
| OQ-2 | Label for `nextDeliveryOn`: "next order" or "next delivery"? | Owner (HO-SUB-001 OQ-3) | **"Next order on …"**, with the §8.5 explanation |
| OQ-3 | Ask for a cancellation reason as required or optional? | Owner | **Required choice** from HO-SUB-001's customer reasons, with an optional note — the §8.3 churn report needs it |
| OQ-4 | Order-page link target | HO-ORD-002 author | `/portal/orders/[orderId]` (FA-2) |

---

## 10. Dependencies

**No new package.** Native `<input type="date">` for dates. A date-picker, calendar, date library
beyond HO-CAT-002's `date-fns`, state library or form wizard is a §20.6 stop.

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error / permission-denied states.
Components in `web/components/subscriptions/`.

### 11.1 Subscribe — US-SUB-01, 04 · `/portal/subscriptions/new/[skuId]`

`SubscribeForm` — one column, sections in order:

| Section | Behaviour |
|---|---|
| Product | Image, product name, variant, unit price (`formatMoney`). `subscribable: false` → the form is replaced by *"{reason}"* and a link back. `existingSubscriptionId` → *"You already subscribe to this."* with a link to it. Unknown SKU (`404`) → *"We couldn't find that product."* |
| Quantity | Number input with − / + buttons, 1…`maxQuantity`, label *"Per delivery"* |
| Interval | Radio group from `allowedIntervalWeeks`: *"Every 2 weeks"*, … |
| Delivery or pickup | Radio **Deliver to me** / **Collect from the clinic**. Delivery: the customer's saved addresses (default preselected), **Add an address** → HO-ACC-002's address page with `?next=` back here (form values restored, §8.3). Pickup: active branches with address |
| Cat *(prescription diet only)* | Select from `eligibleCats`, each *"{name} — authorised until {date}"*. None → *"This food needs a vet's authorisation for one of your cats."* with **Book a consultation** (`/book?category=Consultation`) and no submit |
| How you'll pay | Radio from the payment-methods endpoint for the chosen fulfilment: **Cash on delivery**, **Bank transfer**, **Pay at the counter**. Unavailable ones disabled with Orders' reason as text (ADR-FE-113). Beneath: §8.5 *How paying works* |
| First order | `<input type="date">`, `min = earliestFirstDeliveryOn`, `max = latestFirstDeliveryOn`, default the earliest; helper text states the range in words and the reminder lead time |
| Estimate | `SubscriptionEstimate`: unit price × quantity, **Subscription discount −{n} %** line when `discountPercent > 0`, estimated total, §8.5 *Estimate* note. Calculated from the options data locally **for display only** (quantity × price, the same half-up minor-unit rounding as HO-SUB-001's `SubscriptionEstimate`), and **replaced by the server's `estimate` once created** |

**Start subscription** → `POST`. `201` → the detail page with an announced success alert: *"Subscribed.
Your first order is on {formatDateOnly(nextDeliveryOn)}."* Errors map to fields (§11.7):
`PaymentMethodUnavailable` refreshes the payment list; `SubscriptionAlreadyExists` links to the
existing subscription; `PrescriptionAuthorisationRequired` refreshes `eligibleCats`. The submit button
is disabled only while the request is in flight, with its busy state announced (HO-ACC-002 §8.6).

`SubscribeButton` (for HO-SHP-002's page) — fetches options for its `skuId` only when a customer
session exists (`['session','customer']`); signed out, it renders **Subscribe** linking to the route and
lets the guard handle sign-in. Signed in, it renders nothing while loading or when not subscribable;
otherwise a secondary button (**Subscribe** / **Subscribe & save {n} %**) linking to
`/portal/subscriptions/new/{skuId}`.

### 11.2 My subscriptions — `/portal/subscriptions`

`MySubscriptionsList` — tabs **Active & paused** / **Cancelled**. Each `SubscriptionCard`: product and
variant, quantity and interval, status as text (*Active*, *Paused*, *Suspended*, *Cancelled*), **Next
order on {date}** when present, and one warning line where it applies: *"Delivery address removed —
choose another"* (`deliveryAddressMissing`), *"Changes scheduled from {appliesFromCycleOn}"*, *"Paused
after unpaid orders"*, *"Paused: {cat}'s food authorisation has ended"*, *"Waiting for your transfer —
pay by {paymentExpiresOn}"* (`lastCycle.orderState = AwaitingPayment`). Empty state: *"No
subscriptions yet. Look for **Subscribe** on food and litter in the shop."*

### 11.3 Subscription detail — US-SUB-02, 03, 05, 06 · `/portal/subscriptions/[subscriptionId]`

| Component | Behaviour |
|---|---|
| `NextOrderPanel` | **Next order on {date}**; the §8.5 cut-off sentence from `changeCutoffOn`; when `isLocked`: *"Your order on {date} is being prepared. Changes now start with the delivery on {followingDeliveryOn}."* Paused: *"Paused — no orders until you resume."* |
| `PendingChangesPanel` | When `pending`: what is scheduled (new terms as text, *"Skip {date}"*, *"Pause"*, *"Cancel"*) and from which date; **Withdraw scheduled changes** (confirm) |
| `TermsPanel` | Quantity, interval, delivery address or pickup branch, payment method, cat. **Change** when `allowedActions` has `ChangeTerms` → `ChangeTermsDialog` |
| `SubscriptionActions` | Buttons **only** from `allowedActions` (ADR-FE-110): **Skip next delivery**, **Pause**, **Resume**, **Cancel subscription** |
| `ChangeTermsDialog` | Quantity, interval, fulfilment, address/branch, payment method (the §11.1 controls, payment list refetched for the new terms); `PUT` with `version`. `200` → dialog closes, the `message` is shown and announced. `409` → *"This subscription changed in another window. We've loaded the latest."*, refetch, the dialog stays open with the customer's values |
| `SkipDialog` | *"Skip the delivery on {skippableCycleOn}?"* → `POST …/skip` with that date → the server's `message`. `CycleNoLongerSkippable` → the server's message (naming the next skippable date) and refetch |
| `PauseDialog`, `CancelDialog` | UI-3 text per §7; cancel asks for a reason (radio from HO-SUB-001 §10.1's customer reasons, OQ-3) and an optional note (500, plain text) |
| `ResumeDialog` | Optional first-order date (`min`/`max` from `GET …/options?skuId`); `SubscriptionSuspended` → the server's message; `PrescriptionAuthorisationRequired` → the message with **Book a consultation** |
| `SkipLinkHandler` | Reads `?skip`, applies ADR-FE-111, then `router.replace` without the parameter |
| `SuspensionBanner` | `Suspended`: reason as plain text with what to do — `UnpaidCycles`: *"Paused after {n} unpaid orders. Please contact the shop on {phone} to restart it."* (phone from `GET /api/branches`); `AuthorisationExpired`: *"{Cat}'s authorisation for this food has ended. It restarts automatically when the vet renews it."* + **Book a consultation**; `ProductUnavailable`: *"This product is no longer available."* + **Cancel**; `CatDeceased` / `CatOwnerChanged`: a neutral sentence and **Cancel** |
| `OrderHistory` | From the detail's `cycles` and `…/cycles?pageNumber`: date, outcome, **order number linking to the order page** (FA-2), payment state as text (*Awaiting transfer — pay by …*, *Confirmed*, *Delivered*, *Expired unpaid*, *Refused on delivery*, *Cancelled*), failure reason as text for failed cycles (*"Out of stock — nothing was ordered"*). A table from 768 px, cards below |
| `LastChangeAlert` | The latest `SubscriptionChangeResult.message`, `role="status"` |

### 11.4 Staff subscriptions — US-SUB-05, 06 · `/staff/subscriptions`

| Component | Behaviour |
|---|---|
| `StaffSubscriptionsTabs` | **All** · **Unpaid** · **Prescription suspended** (= `status=Suspended&suspensionReason=AuthorisationExpired`) |
| `StaffSubscriptionsTable` | Filters (status, suspension reason, payment method, search by customer name or mobile), sort (next order, status, customer), pagination; columns customer (link to the staff customer page), mobile, product, cat, status, reason, next order, payment method, unpaid streak; **Export CSV** button → `export.csv` with the current filters (ADR-FE-114); `413` → *"Too many rows — narrow the filters."* |
| `UnpaidSubscriptionsList` | Two groups from the `group` field: **Suspended** and **At risk**. Each row lists the unpaid orders (date, order number, pay-by, state) — **US-SUB-06 AC2** |

### 11.5 Staff subscription detail — `/staff/subscriptions/[subscriptionId]`

`StaffSubscriptionDetail` — the §11.3 panels in read-only form plus customer (name, mobile, link),
cancellation reason and note, reinstatement record, full paged history. Actions **only** from
`allowedStaffActions`:
- **Reinstate** → `ReinstateDialog`: reason (required, 500), optional first-order date; UI-3 text per §7.
  `PrescriptionAuthorisationRequired` shown in the dialog.
- **Cancel** → `StaffCancelDialog`: note (required), UI-3 text.
A `403` renders `PermissionDeniedState` in place of the action, not a toast.

`SubscriptionSummaryPanel` (HO-ACC-002's customer page) — live subscriptions (product, status, next
order, link), counts of suspended and cancelled.

### 11.6 Navigation

`(portal)/layout.tsx` — **Subscriptions** entry. `(staff)/layout.tsx` — **Subscriptions** entry,
shown to roles holding `Subscriptions.View` (presentation only; the server enforces).

### 11.7 Error mapping — `lib/api/errors.ts`

Append entries for every HO-SUB-001 command's `propertyName`s, and map the §10.4 codes to the
messages used above: `SkuNotSubscribable`, `InvalidSubscriptionQuantity`,
`InvalidSubscriptionInterval`, `FulfilmentDetailsInvalid`, `PaymentMethodNotAllowedForFulfilment`,
`PaymentMethodUnavailable`, `PickupBranchNotAvailable`, `InvalidFirstDeliveryDate`,
`SubscriptionAlreadyExists`, `SubscriptionLimitReached`, `CatRequiredForPrescriptionDiet`,
`PrescriptionAuthorisationRequired`, `InvalidSubscriptionTransition`, `SubscriptionSuspended`,
`SubscriptionCannotBeReinstated`, `ReinstatementReasonRequired`, `CancellationReasonRequired`,
`NoChangeRequested`, `NoPendingChange`, `CycleNoLongerSkippable`, `SkipAlreadyScheduled`,
`DeliveryAddressNotFound`. The server's message is preferred when present; the mapped text is the
fallback. `errorCode` never appears in the DOM.

---

## 12. Mocks — `web/mocks/handlers/subscriptions/*.ts`

Generated from HO-SUB-001 §12.4. Fixtures:
- options for a dry food (discount 10 %), a litter, a **prescription diet** with one eligible cat and
  one without authorisation, a carrier (`subscribable: false`), a SKU already subscribed, and an
  unknown SKU (`404`);
- payment methods with **COD unavailable** and a reason, for delivery and for pickup;
- subscriptions in every status and suspension reason; one **unlocked** and one **locked** with
  `followingDeliveryOn`; one with pending terms, skip and cancel; one with `deliveryAddressMissing`;
  one whose last cycle is `AwaitingPayment`;
- change results for immediate and scheduled outcomes with their `message`s; `409` stale version;
  `CycleNoLongerSkippable`; `SubscriptionSuspended`;
- cycles covering every outcome, order state and failure reason;
- staff lists with both unpaid groups, the prescription filter, export `413`, and every `403`;
- a subscription whose `commerceTimeZoneId` is `Asia/Dhaka` viewed by a browser in `America/New_York`
  (the date-only test).

---

## 13. Allowed scope

```
web/app/(portal)/portal/subscriptions/**
web/app/(portal)/layout.tsx                                   ← one nav entry only
web/app/(staff)/staff/subscriptions/**
web/app/(staff)/staff/customers/[customerId]/page.tsx         ← SubscriptionSummaryPanel only (HO-ACC-002's page)
web/app/(staff)/layout.tsx                                    ← one nav entry only
<the HO-SHP-002 product page file>                            ← the <SubscribeButton> element only; exact path recorded in §1 Amended before it is touched (OQ-1, FA-1)
web/components/subscriptions/**
web/lib/api/{subscriptions,staff-subscriptions}.ts
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/lib/format/**                                             ← formatDateOnly only (§8.4)
web/lib/hooks/**                                              ← new hooks only
web/lib/validation/**
web/mocks/handlers/subscriptions/** · web/mocks/handlers/index.ts
web/tests/components/subscriptions/**
web/tests/e2e/subscriptions-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-109…114
docs/test-cases/TC-HO-SUB-002.md · docs/gates/HO-SUB-002-*.md
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**` | HO-SUB-001 |
| `.ai/**`, `.claude/**`, `docs/**` except the two named paths | Governing docs, agents, inputs |
| Every other HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002, HO-SHP-002 and HO-ORD-002 file beyond the lines in §13 — in particular **HO-ACC-002's session guard and `?next=` rule** (§8.1 works with them as they are) | Their areas. **No change to the order page, the cart or checkout** |
| `web/package.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs` | No dependency or config change (§8.4's rule is enforced by U-X3 instead) |

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| **A — subscribe** | | |
| U-A1 | Options for a dry food | Intervals from the payload; first date defaults to `earliestFirstDeliveryOn`; min/max set and stated in text |
| U-A2 | Carrier (`subscribable: false`); already subscribed; unknown SKU | Reason and back link; link to the existing subscription; not-found text |
| U-A3 | Prescription diet with no eligible cat | Explanation and **Book a consultation**; no submit button |
| U-A4 | COD unavailable for the address | COD disabled with Orders' reason as text; bank transfer selectable |
| U-A5 | Switch to pickup | Address hidden, branch shown; COD not offered; **Pay at the counter** offered; payment list refetched |
| U-A6 | Estimate at 10 % with quantity 3 of `1450.00` | `4350.00`, `−435.00`, `3915.00`; *"Delivery and tax are added…"* note |
| U-A7 | "Add an address" detour and return | Every value restored; storage key cleared after submit |
| U-A8 | Submit `201` | Navigates to detail; success alert announced with the date from `nextDeliveryOn` |
| U-A9 | No payment control | No card-number, expiry or CVV input and no "save card" text anywhere in `components/subscriptions/**` — ADR-FE-113 |
| U-A10 | `SubscribeButton` signed in for a non-subscribable SKU; signed out | Renders nothing; renders a link to `/portal/subscriptions/new/{skuId}` without calling options |
| **B — detail and control** | | |
| U-B1 | Unlocked subscription | Cut-off sentence; actions exactly from `allowedActions` |
| U-B2 | Locked subscription | Locked sentence with `followingDeliveryOn`; cancel confirm uses the locked wording |
| U-B3 | Change terms → scheduled result | Dialog closes; `message` shown in a `role="status"` region, read by screen readers — **US-SUB-02 AC2** |
| U-B4 | Change terms `409` | Stale message; refetch; the dialog keeps the customer's values |
| U-B5 | Skip confirm then `CycleNoLongerSkippable` | Server message shown; data refetched |
| U-B6 | `?skip=` equal to `skippableCycleOn` | Skip confirm opens for that date; **no request until confirmed**; the parameter removed — ADR-FE-111 |
| U-B7 | `?skip=` for a past or other date | *"That delivery can no longer be skipped…"* with **Skip that one** for `skippableCycleOn` |
| U-B8 | Pending changes | Listed with their start date; **Withdraw** confirm → `DELETE` |
| U-B9 | Each suspension reason | Its banner text and action; `UnpaidCycles` shows the branch phone |
| U-B10 | Order history states | Every state as text; order number links to `/portal/orders/{id}` |
| U-B11 | `deliveryAddressMissing` | Warning on card and detail; **Change** opens with the address field focused |
| U-B12 | Cancel requires a reason | Submit without one → field error; with one → `POST` with reason and note |
| **C — staff** | | |
| U-C1 | Filters in the URL; reload | Same results; export link carries the same filters |
| U-C2 | Unpaid list groups | *Suspended* and *At risk* headed separately; unpaid orders listed per row |
| U-C3 | Prescription tab | Requests `status=Suspended&suspensionReason=AuthorisationExpired` |
| U-C4 | Reinstate without reason | Field error; with reason → `POST`; the confirm names the customer |
| U-C5 | `403` on reinstate | `PermissionDeniedState` in place of the action |
| U-C6 | Staff actions only from `allowedStaffActions` | As stated |
| **X — cross-cutting** | | |
| U-X1 | `formatDateOnly('2026-10-20')` under `TZ=America/New_York` and `TZ=Asia/Dhaka` | `Tue 20 Oct` both — ADR-FE-112 |
| U-X2 | `changeCutoffOn` formatted | In `commerceTimeZoneId`, zone named |
| U-X3 | Source scan of `components/subscriptions/**` and `app/**/subscriptions/**` | No `new Date('` / `new Date("` with a date-only literal or a `…On` field; no date-arithmetic helpers (`addWeeks`, `addDays`) imported — ADR-FE-110, ADR-FE-112 |
| U-X4 | Every form: server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X5 | Dirty subscribe form or open dialog + navigate | Unsaved-changes confirm |

## 16. End-to-end test cases

MSW first, then the real API. Both runs are recorded. Cases marked **MSW-only** need time to pass
(a cut-off, a generation date, a payment window). Their real-API equivalents are HO-SUB-001's
integration cases, cited in the row.

| ID | Case | Expected |
|---|---|---|
| E-A1 | Signed out, on the product page, **Subscribe** → sign in → back on `/portal/subscriptions/new/{skuId}` → form → start (COD, every 4 weeks) | Detail shows *Next order on {date}* and the terms; the confirmation in the inbox (real API) — **US-SUB-01 AC1** |
| E-A2 | Prescription diet: without an authorised cat, then (real API: a vet authorises) with one | Blocked with the consultation link; then subscribed with the cat shown |
| E-A3 | **AC-17 (MSW-only; real API = HO-SUB-001 I-D1, I-D2, I-E1, I-C2)**: subscribe → the pre-charge notice appears in the inbox → follow its skip link → confirm → the history later shows the next cycle's order *Awaiting transfer* | Each step visible; the skip needed a confirmation |
| E-B1 | Change quantity before the cut-off | Message names the next delivery; terms updated — **US-SUB-02 AC1** |
| E-B2 | **MSW-only (real API = I-C6…I-C8)**: locked subscription — change, skip, cancel | Each message names the following delivery; pending panel shows them; withdraw works — **US-SUB-02 AC2** |
| E-B3 | Pause → resume with a chosen date | Status text changes; next order is the chosen date |
| E-B4 | Stale skip link from an old notice | The explanation and the offer for the next skippable date |
| E-C1 | **MSW-only (real API = I-F4, I-F7)**: staff open *Unpaid*, open a suspended subscription, reinstate with a reason | Subscription *Active*; it leaves the Suspended group |
| E-C2 | Staff open *Prescription suspended*; export CSV | The filtered rows; the download request carries the filters |
| E-C3 | A Reception user opens a subscription | Detail visible; no Reinstate / Cancel |
| E-X1 | axe on every route in §8.1 including each dialog open | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-SUB-001 §12.4; MSW handlers; `formatDateOnly`; `SubscriptionCopy`.
3. Subscribe form and `SubscribeButton` (render it on HO-SHP-002's page only once OQ-1's path is in §1
   Amended).
4. My subscriptions → detail → change / skip / pause / resume / cancel / withdraw → skip-link handler →
   order history.
5. Staff list, tabs, export → staff detail with reinstate and cancel → customer-page panel → nav.
6. U-series, E-series on MSW, then E-series against the real API per HO-SUB-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | SUB-01 AC1 | A customer subscribes to a food or litter SKU with an interval and sees the next order date, including after signing in mid-flow | U-A1, U-A8, E-A1 |
| A2 | SUB-01 AC1 | Non-subscribable SKUs and prescription diets without an authorised cat are explained, not offered | U-A2, U-A3, U-A10, E-A2 |
| A3 | SUB-01 AC2 | Generated orders appear in the history with their payment state, COD or awaiting transfer | U-B10, E-A3 |
| A4 | SUB-01 / ADR-238 | No card, token or payment capture exists; unavailable methods carry Orders' reason | U-A4, U-A5, U-A9 |
| A5 | SUB-02 AC1 | Pause, skip, change and cancel before the cut-off | U-B1, U-B8, U-B12, E-B1, E-B3 |
| A6 | SUB-02 AC2 | After the cut-off the customer is told which delivery a change affects | U-B2, U-B3, E-B2 |
| A7 | SUB-03 | The notice's skip link leads to a confirmation for that delivery and never acts on its own; stale links explain themselves | U-B6, U-B7, E-A3, E-B4 |
| A8 | SUB-04 | The discount shows in the estimate; the manager configures it in the settings screen | U-A6, E-A1 (discount line visible with the mocked 10 %) |
| A9 | SUB-05 | A suspended prescription subscription explains why and how it restarts; staff see the prescription list | U-B9, U-C3, E-C2 |
| A10 | SUB-06 | Customers see unpaid orders and the suspension; staff see the unpaid list and can reinstate | U-B9, U-C2, U-C4, E-C1 |
| A11 | ADR-FE-112 | Dates never shift a day by the viewer's zone | U-X1, U-X3 |
| A12 | UI-6 / UI-1 | Zero serious/critical axe violations; 360–1440 px | E-X1, E-X2 |

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering; no new dependency; **no client-side schedule logic** (ADR-FE-110, U-X3); the scoped edits to HO-ACC-002's page, the layouts and HO-SHP-002's page match §13 and §1 Amended; HO-ACC-002's guard untouched |
| 2 | SECURITY | `security-reviewer` | No payment capture (U-A9); the skip link never mutates on load (ADR-FE-111); `?next=` still passes HO-ACC-002's `/portal/`-only guard; `sessionStorage` holds form values only and is cleared; notes rendered as text; actions only from `allowedActions` / `allowedStaffActions` |
| 3 | CODE_REVIEW | `code-reviewer` | Dates only through `formatDateOnly` and the zone-aware helper; mutation responses replace the cache; component size; copy centralised in `SubscriptionCopy` |
| 4 | TEST | `test-engineer` | `TC-HO-SUB-002.md` first; mock and real runs separate; MSW-only cases cite their HO-SUB-001 counterparts; U-X1 run under two `TZ` values |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; FA-1 / FA-2 checked against HO-SHP-002 and HO-ORD-002 as written |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002, HO-NTF-002). Do
not build a parallel shell, address book or inbox. HO-SHP-002 missing blocks only the button (§5).

**20.2 A screen seems to need schedule arithmetic** (the following delivery, whether it is past the
cut-off, the next skippable date). Ask the server. HO-SUB-001 §12.4 already returns each of these.

**20.3 A payment, card, wallet or "save for next time" control is requested.** It contradicts ADR-238
and the owner's instruction; report it.

**20.4 The real API disagrees with HO-SUB-001 §12.4.** Report it; do not shim.

**20.5 A screen needs an endpoint or field HO-SUB-001 does not specify**, or FA-1 / FA-2 does not hold.
Report it; do not invent a mock field or a link target.

**20.6 A new dependency seems necessary** (§10). Raise it.

**20.7 The sign-in round trip loses the SKU** (HO-ACC-002's guard behaves differently from §8.1). Report
it; do not change the guard, and do not move the SKU back into a query parameter.
