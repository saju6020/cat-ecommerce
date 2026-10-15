# HO-POR-002 — Customer Portal (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-POR-002** |
| Title | Customer Portal — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §15 — **US-POR-01, US-POR-02, US-POR-03** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — FR-POR-1, FR-POR-4, FR-POR-5, FR-BRD-13; **UI-1, UI-6**; NFR-USA-4, NFR-USA-6, NFR-PRF-1, NFR-CMP-2, NFR-SEC-5; §1.2 (no payment gateway) |
| Pairs with | [`HO-POR-001`](HO-POR-001.md) — **§12.4 there is the contract.** On any disagreement about the wire, HO-POR-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, stack, layering, cat pages), [`HO-ACC-002`](HO-ACC-002.md) (`(portal)` layout and `CustomerSessionGuard`, `client.ts`, CSRF, error mapping, `?next=` routing, ADR-FE-012 "not recorded ≠ zero"), [`HO-ADM-002`](HO-ADM-002.md) (`lib/format`, public branches, policy prompt in the portal layout), [`HO-NTF-002`](HO-NTF-002.md) (inbox badge), [`HO-APT-002`](HO-APT-002.md) (`/book`, `/portal/appointments/**`). **Assumed, not yet written**: HO-EMR-002, HO-PHM-002, HO-ORD-002, HO-BIL-002, HO-BRD-002, HO-SUB-002 — the tiles link to their pages (§8.5) |
| Delivery order | Section 15 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with HO-CAT-002's shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — README C-6.** The stay-diary page (`/portal/stays/[stayId]/diary`), its section §11.3, its file-tree entry, ADR-FE-125 and acceptance row E-C1 are **retired** to HO-BRD-002, which owns `/portal/boarding/[stayId]/diary` and the data behind it. The dashboard's stays tile now links there. |
| Amended | **2026-10-10 -- owner-delegated decision 2026-10-10, owner to review (see HO-POR-001 §1 Amended 2026-10-10 finalise).** (1) **No stays and no subscriptions tiles:** HO-BRD and HO-SUB are skipped for now; the dashboard JSON has no `stays` / `subscriptions` key and the frontend must not render those tiles (nor a missing-key placeholder). Phases E and F are deferred by owner. (2) **Re-order goes through Orders' endpoint:** the portal has no `POST /api/portal/orders/{orderId}/reorder`, no `ReorderResultDto`, no `OrderNotFound` / `OrderNotReorderable` / `ReorderLineOutcome` for it. The order tile shows a re-order action when the order item's `canReorder` is true and calls (or links to the order page that calls) `POST /api/customers/me/orders/{orderId}/reorder`, rendering that endpoint's `unavailable[] { productName, reason }`; `useReorder` / `reorder/**` follow HO-ORD-002's contract, not §11 here. (3) **New fields present:** `visits.data.items[].followUpOn` (`yyyy-MM-dd` or null) and `orders.data.items[].trackingNumber` (string or null; show it as the tracking reference when present). `canReorder` is Orders' own flag. |
| Amended | **2026-10-13 -- build against the merged HO-POR-001 (owner: start the handovers that no longer wait on anything).** Differences between this handover's assumed shapes and the merged controllers/DTOs; the merged backend wins on the wire (ADR-FE-126, `web/docs/decisions.md`). (1) **Dashboard:** `GET /api/portal/dashboard` is the only portal route. It has `customer, cats, appointments, balances, visits, prescriptions, orders` and **no `stays` / `subscriptions` key**; every key is treated as possibly absent and an absent delivered key renders "Not available yet" (not empty, not an error, no retry). The stays and subscriptions tiles, phases E and F, E-C* and U-C* are not built. (2) **Re-order:** no `POST /api/portal/orders/{id}/reorder`, no `ReorderResultDto`, no `OrderNotFound` / `OrderNotReorderable` / `ReorderLineOutcome`. The call is Orders' `POST /api/customers/me/orders/{orderId}/reorder`, which is `[Idempotent]` (a UUID `Idempotency-Key` header is **required**, `400 IdempotencyKeyRequired` otherwise; the same key replays) and answers `200 { addedCount, unavailable[{ productName, reason }] }` only. So the dialog has **no Added list, no quantities, no price-change or reduced-quantity note and no "Already in your basket" outcome** (E-B3 is retired as specified; a second re-order is a second request that re-adds); `reason` is an open code set (`NotPurchasable`, `OutOfStock`, `PrescriptionNotAuthorised`, a cart rule code) mapped to sentences with a generic fallback; the three request errors are `404` (not on the account), `400` and `409` (basket changed / request in progress) with the §11.2-style plain messages in `lib/api/errors.ts`; the server does not refuse a non-re-orderable order, so `canReorder` on the order item is the only gate and there is no `OrderNotReorderable` `400` (ORD backlog, POR.md row 23). (3) **Order item:** `trackingNumber` (string or null) and `canReorder` come from the ORD list item; **visit item:** `followUpOn` comes from EMR; both are shown. (4) **Balances:** `openInvoiceCount` is counted over at most the newest 500 issued invoices and the response does not say when the cap was hit, so the tile prints "500 or more" only at exactly 500 (POR.md row 24); `howToPay` is the server's text, and clinic phone numbers come from the public branches list (`GET /api/branches`), which the tile hides when it fails. (5) **Types and enumerations as sent:** `PortalSection<T> { state: Ready|Unavailable, data: T|null }`; `vaccinationState` per cat; `status` strings `Booked|CheckedIn|InConsultation` (appointments), `PendingPayment|Confirmed|Packing|Dispatched|Delivered|Cancelled|Returned|Refunded` (orders), `Issued|PartPaid` (open invoices), `Current|DueSoon|Overdue` (vaccination) -- all through label maps with a neutral fallback. (6) **Scope notes (owner to decide, POR.md rows 26-28):** the Home nav entry is in `web/nav/areas/portal.ts` (the layout holds no links; HO-APT-002's precedent) and its position follows registry order; the default-landing constant exists in `lib/routing/portal-links.ts` but `app/(auth)/sign-in/page.tsx` and `app/page.tsx` (HO-ACC-002) were not edited, so sign-in still lands on `/portal/cats`; the order-detail `ReorderButton` and the pages for `/portal/orders`, `/portal/prescriptions` and `/cart` do not exist on master and are not built here. (7) **ADRs:** this handover's ADR-FE-119..123 and 126 are written in `web/docs/decisions.md`; 124 and 125 (media, diary) are retired. Evidence: `docs/test-cases/TC-HO-POR-002.md`. |
| Amended | **2026-10-13 (gate follow-up, ARCH row 29) -- what stands in for the missing §3-§11.4, and the design.** This file has no §3 (in scope), §4 (out of scope), §5 (prerequisites), §6 (governing documents), §7, §8 (architecture and component design), §9 (open questions), §10 or §11.1-§11.4. They were already missing at `529ff4f`. Read these instead: **wire contract** = HO-POR-001 §12.4 as corrected by its §1 Amended rows (2026-10-09, 2026-10-10 build, 2026-10-10 finalise) and by this file's Amended 2026-10-13 row; **layering and structure** (the old §6/§8) = HO-CAT-002 §8.2 and HO-ACC-002 §8.2 (hooks in `lib/hooks`, endpoints in `lib/api`, `apiFetch` with CSRF, no `useQuery` in components, no browser storage for data, no `@/mocks` in app code); **screens, states and copy** (the old §3.2/§8.5) = the boards in `docs/design/portal/` (README lists them; copy for the three re-order errors and the ten outcomes there is proposed wording, superseded for re-order by ADR-FE-123); **decisions** = `web/docs/decisions.md` ADR-FE-119..123 and 126 (124 and 125 retired); **tests** = §15/§16 as narrowed by `docs/test-cases/TC-HO-POR-002.md`. **Correction:** the identity row's "Approved design: None" is wrong -- an approved design exists at `docs/design/portal/` (warm customer layer, 14 boards, stays and subscriptions tiles and the diary excluded by the owner decisions above). Also: the Idempotency-Key renewal rule of the re-order follows HO-ORD-002's `shouldRenewIdempotencyKey` (keep on network error, 5xx and `409 RequestInProgress`; renew on any other 4xx); the portal carries its own copy until the second of the two branches merges (POR.md row 31). |
| Amended | **2026-10-13 (owner answers to the ACCEPTANCE gate, `docs/gates/HO-POR-002-ACCEPTANCE.md`).** (1) **A13 / E-X3 (LCP <= 2.5 s):** owner: "Defer to XCT-002". The LCP half of A13 is carried to HO-XCT-002 E-B1, which gains `/portal` among its routes (Amended row there); U-A9 (no layout shift) stays here. (2) **Landing:** owner: "Land on /portal". A customer's post-sign-in landing is now `/portal` (`DEFAULT_CUSTOMER_LANDING`), one line each in `web/app/(auth)/sign-in/page.tsx` and `web/app/page.tsx` (HO-ACC-002 Amended row records it); `sanitizeNextPath` is unchanged, so an explicit `?next=` still wins. E-A1's landing half now passes (`portal-dashboard.spec.ts`, `home.spec.ts`). Closes POR row 26. (3) **A7 (re-order twice):** owner: "Fine as is". A deliberate second re-order adds the items again (Orders' endpoint has no top-up); a retry of the same click replays under one `Idempotency-Key`. POR row 21's "already in basket" part is owner-accepted; the Added list, quantities and price-change display remain open there. (4) **Owner-confirmed ("Confirm all")**, the 2026-10-10 delegated decisions of HO-POR-001 and this file's Amended 2026-10-10 row: boarding and subscriptions skipped (no tiles); re-order through Orders' endpoint; `followUpOn` from EMR; no access log when an owner reads their own records. Closes POR row 18. (5) **Test helper outside the `portal-*.spec.ts` glob:** `web/tests/e2e/portal-helpers.ts` (holds page `/api/` fetches until MSW is ready, waits for the session; APT/EMR/SHP-002 precedent), alongside `web/nav/areas/portal.ts` (Amended 2026-10-13 above). POR rows 40-43. |

---

## 2. Objective

Build the **portal home**: the page a cat owner lands on after signing in, showing everything the
business holds about them on one screen, with a **one-click re-order** of a past order and a **stay
diary** for boarding.

- **Dashboard** (`/portal`) — tiles for cats (with vaccination status), upcoming appointments,
  boarding stays, balances and open invoices, recent orders, recent visits, prescriptions and
  subscriptions. Each tile summarises and **links out** to the page the owning area built; the portal
  never re-implements those pages.
- **Re-order** — one button on a recent order; one request; a result dialog saying what went into the
  basket and, for everything else, why not.
- **Stay diary** — **not built here** (README C-6); HO-BRD-002 owns `/portal/boarding/[stayId]/diary` and the dashboard links to it.
- `(portal)/layout.tsx` — nav entry **Home** → `/portal`, first in the list. Nothing else.
- `lib/routing` — default post-sign-in landing constant `/portal`. Nothing else.
- HO-ORD-002's order detail page — renders `ReorderButton` when the order is re-orderable. Nothing
  else; only once that page exists.

### 11.5 Error mapping — `lib/api/errors.ts`

Append `OrderNotFound`, `OrderNotReorderable`, `StayNotFound` and the `ReorderLineOutcome` label map.
The server's message is preferred when present; the mapped text is the fallback.

---

## 12. Mocks — `web/mocks/handlers/portal/*.ts`

Generated from HO-POR-001 §12.4. Fixtures:
- a **new customer** — every section `Ready` and empty, `hasCats: false`;
- a **full customer** — two cats (one `DueSoon`, one `Overdue`, one flagged high-risk), appointments
  in a branch whose zone differs from the test browser's, a current and an upcoming stay, orders in
  `PendingPayment`, `Delivered`, `Cancelled`, balances in BDT **and** USD, a visit with and without a
  summary, prescriptions, a suspended subscription;
- a **degraded customer** — `balances` and `visits` `Unavailable`, `cats` `Unavailable` on a variant;
- 13 cats (`hasMore`);
- re-order results: all added; mixed with every outcome; nothing added; `404`; `400`; `409`;
- diaries: `NotStarted`; `Open` over five days with photos and a video; two cats; `mediaPurged`;
  media URLs that expire in 90 s and a media URL that returns `403` (to exercise ADR-FE-124);
  `404`.

---

## 13. Allowed scope

```
web/app/(portal)/portal/page.tsx
web/app/(portal)/layout.tsx                                   ← Home nav entry only
web/app/(portal)/portal/orders/[orderId]/page.tsx             ← ReorderButton only (HO-ORD-002's page; only if it exists)
web/components/portal/{dashboard,reorder,diary}/**
web/lib/api/portal.ts
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/lib/hooks/{usePortalDashboard,useStayDiary,useReorder}.ts
web/lib/routing/**                                            ← default landing constant and portal-links.ts only
web/lib/format/**                                             ← additions only (e.g. day heading)
web/mocks/handlers/portal/** · web/mocks/handlers/index.ts
web/tests/components/portal/**
web/tests/e2e/portal-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-119…126
docs/test-cases/TC-HO-POR-002.md · docs/gates/HO-POR-002-*.md
```

The landing change and the order-detail button touch HO-ACC-002 and HO-ORD-002 files; each of
those handovers gets a §1 Amended row recording it when this handover is approved.

## 14. Forbidden scope

`src/**`, `tests/**`, `.ai/**`, `.claude/**`, `docs/**` (except the two named paths), every other
HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002, HO-APT-002 and later-area file beyond the lines in
§13, `web/package.json`, `package-lock.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs`.

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| U-A1 | Dashboard, new customer fixture | `NoCatsEmptyState` with **Add a cat**; other tiles show their true empty text |
| U-A2 | `cats` `Unavailable` | Unavailable message and **Try again**; **no** no-cats invitation — ADR-FE-120 |
| U-A3 | `balances` `Unavailable` | No amount, no *"Nothing to pay"*; retry refetches the dashboard |
| U-A4 | Balances in BDT and USD | Two amounts, no total; `howToPay` and clinic contacts shown; **no element whose name contains "pay" is a button or link to a payment route** — ADR-FE-122 |
| U-A5 | Cat `DueSoon` / `Overdue` / `Current` | Status as text; **Book now** href `/book?catId=…&category=Vaccination` for the first two; none for `Current` |
| U-A6 | Cat with `vaccinationState: Unavailable` | *"Vaccination status unavailable"*; the rest of the cat row renders |
| U-A7 | Appointment in `Europe/London` viewed from `Asia/Dhaka` | Time in the branch zone, labelled |
| U-A8 | `hasMore` | **All cats** link |
| U-A9 | Pending query | Skeletons of fixed height; no layout shift when data arrives |
| U-A10 | Every tile link | Points at the constant in `portal-links.ts`, never a hard-coded string elsewhere — ADR-FE-121 |
| U-A11 | Suspended subscription | Count and explanation as text |
| U-B1 | `ReorderButton` double-click | One request; busy text; re-enabled after the response — ADR-FE-123 |
| U-B2 | Mixed result with every outcome | *Added* and *Not added* lists; each reason as text; reduced quantity and price change noted; summary in a live region |
| U-B3 | Nothing added | Heading *"Nothing could be added"*; **View basket** still offered |
| U-B4 | `404` / `400` / `409` | The three messages of §11.2; no raw code in the DOM |
| U-B5 | Success | Cart query key invalidated; dashboard key **not** invalidated |
| U-B6 | Dialog close | Focus returns to the trigger |
| U-C1 | Diary `NotStarted` | Starts-at-check-in text; no days |
| U-C2 | Five days supplied | Rendered ascending, each an `<article>` with an `<h2>` date |
| U-C3 | `?catId=` filter | Radio reflects the param; the hook receives the cat |
| U-C4 | `mediaPurged` | Purge note; observations still shown |
| U-C5 | Media viewer keyboard path | Open with Enter, Next/Previous with buttons and arrows, `Escape` closes, focus returns |
| U-C6 | Video item | `controls`, no `autoplay`, caption visible |
| U-C7 | Image load error | One refetch; second failure → placeholder text — ADR-FE-124 |
| U-C8 | `staleTime` with URLs expiring in 90 s | ≈ 30 s; floored at 0 when already expiring |
| U-C9 | No media URL written to `localStorage`, `sessionStorage`, IndexedDB or `console` | Spies record nothing — NFR-SEC-5 |
| U-C10 | Observation notes containing `<b>x</b>` | Rendered as literal text |
| U-X1 | Every enumeration shown | Through the label map; no `PendingPayment`-style identifiers in the DOM |
| U-X2 | Sign-out from the portal | Whole cache cleared; dashboard data gone |

## 16. End-to-end test cases

MSW first, then the real API per HO-POR-001 phase; both runs recorded.

| ID | Case | Expected |
|---|---|---|
| E-A1 | Register, sign in with no `?next=` | Lands on `/portal`; no-cats invitation; **Add a cat** reaches the cat form — **US-POR-01 AC2** |
| E-A2 | Customer with records in every area signs in | All eight tiles populated on one screen — **US-POR-01 AC1** |
| E-A3 | (MSW) degraded customer | Unavailable tiles with retry; other tiles intact; retry after the mock recovers fills them |
| E-A4 | Follow each tile's link | Reaches the owning area's page (or, for an unbuilt area, the recorded constant — flagged in the sheet) |
| E-B1 | Re-order a delivered order | Result dialog lists added items; **View basket** shows them — **US-POR-02** |
| E-B2 | Re-order an order containing an out-of-stock and a newly prescription-only item | Both in *Not added* with reasons |
| E-B3 | Re-order the same order again | *"Already in your basket"* for every line; basket unchanged |
| ~~E-C1~~ | **Retired (README C-6)** — the diary is evidenced in HO-BRD-002 | — |
| E-C2 | Open a photo, move through the day with the keyboard, close | Focus returns; video plays only when started |
| E-C3 | (MSW) URL expiry at 90 s | After expiry the page refetches and images still load |
| E-C4 | Diary of another customer's stay by URL | *"We couldn't find that stay."* |
| E-X1 | axe on `/portal` (every fixture) and the diary including the open viewer | Zero serious/critical |
| E-X2 | Both routes at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |
| E-X3 | Lighthouse mobile (throttled 4G) on `/portal` against the real API | LCP ≤ 2.5 s recorded (NFR-PRF-1) |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and `lib/api/portal.ts` from HO-POR-001 §12.4; MSW handlers and fixtures; `portal-links.ts`.
3. `DashboardSection` and the no-cats state → cats, appointments tiles (HO-POR-001 Phase A) →
   visits, prescriptions (B) → balances (C) → orders and re-order (D) → stays tile and diary (E) →
   subscriptions (F).
4. Layout nav entry and landing constant; the order-detail button once HO-ORD-002's page exists.
5. U-series, E-series on MSW, then E-series against the real API per HO-POR-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | POR-01 AC1 | Cats, upcoming appointments, boarding stays, recent orders and outstanding balances are visible on one screen | U-A5, U-A7, U-A4, **E-A2** |
| A2 | POR-01 AC2 | With no cats, an empty state invites adding one — and only when the cats section truly has none | U-A1, U-A2, **E-A1** |
| A3 | POR-01 (FR-POR-1 "everything") | Visits, prescriptions and subscriptions tiles link to their owners' pages | U-A10, U-A11, E-A2, E-A4 |
| A4 | POR-01 | An unavailable section is explained with a retry, never shown as empty or zero | U-A2, U-A3, U-A6, E-A3 |
| A5 | POR-02 | One click re-orders; available items reach the basket | U-B1, U-B5, **E-B1** |
| A6 | POR-02 | Unavailable and newly prescription-gated items are listed with the reason | U-B2, U-B3, U-B4, **E-B2** |
| A7 | POR-02 | Re-ordering again does not duplicate the basket | E-B3 |
| A8 | POR-03 | A current or past stay's daily logs and photos are viewable in date order | U-C1, U-C2, U-C3, **E-C1** |
| A9 | POR-03 | After the retention period the diary explains that photos are gone | U-C4 |
| A10 | POR-03 / NFR-SEC-5 | Media come only from signed URLs, recover from expiry and are never stored | U-C7, U-C8, U-C9, E-C3 |
| A11 | ADR-FE-122 | No payment control exists in the portal | U-A4 |
| A12 | UI-6 / UI-1 | Zero serious/critical axe violations; keyboard-operable viewer; 360–1440 px | U-C5, U-C6, E-C2, E-X1, E-X2 |
| A13 | NFR-PRF-1 | Dashboard LCP within 2.5 s on 4G | U-A9, E-X3 |

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering; one dashboard request; no detail page duplicated from another area (ADR-FE-121); scoped edits to the layout, routing and HO-ORD-002's page match §13; no new dependency |
| 2 | SECURITY | `security-reviewer` | Signed media URLs never stored, logged or proxied (ADR-FE-124); caretaker notes and captions rendered as text; no payment surface (ADR-FE-122); `?next=` guard untouched; cache cleared on sign-out |
| 3 | CODE_REVIEW | `code-reviewer` | `DashboardSection` used by every tile; `Unavailable` never rendered as empty; enumerations through label maps; component size; time and money only through `lib/format` |
| 4 | TEST | `test-engineer` | `TC-HO-POR-002.md` from §15/§16 first; MSW and real-API runs recorded separately; E-X3 measured, not estimated |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row against named evidence; all four prior gates `PASS`; any link still pointing at an unbuilt area's placeholder route recorded |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, the HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002,
HO-NTF-002, HO-APT-002). Do not build a parallel shell, session guard or client.

**20.2 A tile seems to need a field the dashboard does not send** (a dose, a diagnosis, a cat photo).
Report it; do not call another area's endpoint from the dashboard to fill it and do not invent a mock
field.

**20.3 A payment or "pay now" control is requested.** It contradicts SRS §1.2 and ADR-FE-122.
Report it.

**20.4 The real API disagrees with HO-POR-001 §12.4.** Report it; do not shim.

**20.5 A linked area's route is unknown** because its handover is not written. Use the constant in
`portal-links.ts`, record it in the test sheet, and reconcile when that handover lands — do not build
the page here.

**20.6 A new dependency seems necessary** (carousel, lightbox, player, date library). Raise it.

**20.7 Re-order seems to need client-side eligibility or stock checks.** It does not — the server
decides (ADR-FE-123). Report the gap instead.
