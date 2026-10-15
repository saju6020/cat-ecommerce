# TC-HO-POR-002 -- Customer Portal (frontend): dashboard and re-order

Handover: docs/handover/HO-POR-002.md (sections 15, 16, 18; narrowed by section 1 Amended 2026-10-10 and 2026-10-13).
Branch: ho/POR-002 (from master b12fd39d). Written 2026-10-13 BEFORE the build; statuses updated as cases were run (see "Results").

Scope: the portal home `/portal` (tiles: cats, appointments, balances, orders, visits, prescriptions) and re-order through
Orders' endpoint. NOT built: stays and subscriptions tiles (owner skipped BRD and SUB, 2026-10-10: no tile and no placeholder), the stay
diary (README C-6, HO-BRD-002), every C-series case.

Status words: `PASS` = watched passing. `NOT-RUN` = written, not executed. `BLOCKED-SCOPE` = needs an edit outside HO-POR-002 section 13.
`BLOCKED-GAP` = the merged backend does not deliver what the case needs (backend backlog row). `N/A` = retired.

## Contract differences the cases are written against (merged backend wins)

See HO-POR-002 section 1 Amended 2026-10-13 and web/docs/decisions.md ADR-FE-119..122. In short: no `stays` / `subscriptions` key; re-order is
`POST /api/customers/me/orders/{id}/reorder` with a required `Idempotency-Key` header, answering `{ addedCount, unavailable[{productName, reason}] }`
only (no added-lines list, no "already in basket", no price-change or reduced-quantity flag); `reason` is a code from a small open set; `canReorder`
and `trackingNumber` come from the order item; `followUpOn` from EMR; open invoices are capped at 500 (count may be low).

## Unit / component cases (Vitest, tests/components/portal/**)

| ID | Case | Expected |
|---|---|---|
| U-A1 | Dashboard, new customer fixture | `NoCatsEmptyState` with **Add a cat** (`/portal/cats/new`); other tiles show their true empty text |
| U-A2 | `cats` `Unavailable` | Unavailable message and **Try again**; NO no-cats invitation |
| U-A3 | `balances` `Unavailable` | No amount, no "Nothing to pay"; retry refetches the dashboard |
| U-A4 | Balances in BDT and USD | Two amounts, no total; `howToPay` and clinic contacts shown; no link/button whose name contains "pay" targets a payment route (only invoice links) |
| U-A5 | Cat `DueSoon` / `Overdue` / `Current` | Status as text; **Book now** href `/book?catId=..&category=Vaccination` for the first two; none for `Current` |
| U-A6 | Cat `vaccinationState: Unavailable` | "Vaccination status unavailable"; rest of the row renders |
| U-A7 | Appointment in `Europe/London` viewed from `Asia/Dhaka` | Time in the branch zone, zone named |
| U-A8 | `hasMore` | **All cats** link emphasised ("more cats" note) |
| U-A9 | Pending query | Skeletons of fixed height (data-testid), then tiles |
| U-A10 | Every tile link | Equals a `portal-links.ts` builder result |
| U-A11 | RETIRED (subscriptions tile) -- replaced by U-A11b: dashboard JSON has no `stays` / `subscriptions` key | No such tile, no heading, no placeholder |
| U-A12 | A delivered key missing (`orders` absent) | That tile reads "Not available yet", no retry, not "no orders", not an error |
| U-A13 | Visit with `followUpOn` / without | "Follow-up due" date text / none |
| U-A14 | Order with `trackingNumber` / without | "Tracking <number>" / none |
| U-A15 | Open-invoice list at the 500 cap (`openInvoiceCount` >= 500) | Count shown as "500 or more"; otherwise exact |
| U-A16 | Prescription with `earliestEndsOn` null | "no end date recorded"; no medicine/dose words |
| U-A17 | Greeting uses `customer.data.fullName`; `customer` Unavailable | Greeting without a name, nothing else lost |
| U-B1 | `ReorderButton` double-click | One request; busy text; re-enabled after the response; same Idempotency-Key reused on retry, renewed after success |
| U-B2 | Result with `addedCount` 2 and two `unavailable` of different reasons | "2 items added" in a live region; "Not added" list with each reason as text, product name shown |
| U-B3 | `addedCount` 0 with unavailable | Heading "Nothing could be added"; **View basket** still offered |
| U-B4 | `404` / `400` / `409` | Three plain messages; no raw code in the DOM |
| U-B5 | Success | `['cart']` invalidated; dashboard key NOT invalidated |
| U-B6 | Dialog close | Focus returns to the trigger |
| U-B7 | `canReorder` false | No active re-order; reason text beside it; `canReorder` true shows button |
| U-B8 | Unknown `reason` code | Generic "could not be added" text; code not in the DOM |
| U-X1 | Every enumeration shown (order status, appointment status, invoice status, vaccination status) | Through label maps; no PascalCase identifiers in the DOM |
| U-X2 | Sign-out from the portal | Dashboard data gone from the cache (existing sign-out clears the cache; asserted here for the dashboard key) |
| U-X3 | No browser storage / console writes while rendering and re-ordering | Spies record nothing |
| U-X4 | Source guard: nothing under app/, components/, lib/ imports `@/mocks` | Grep-style test passes |
| U-L1 | `portal-links.ts` builders | Each returns the documented path; `DEFAULT_CUSTOMER_LANDING` is `/portal` |

## End-to-end cases (Playwright, MSW first)

| ID | Case | Expected |
|---|---|---|
| E-A1 | New-customer scenario: open `/portal` | No-cats invitation; **Add a cat** reaches `/portal/cats/new`. The "lands on /portal after sign-in with no `?next=`" half is BLOCKED-SCOPE (sign-in pages and `app/page.tsx` still send customers to `/portal/cats`; editing them is outside section 13) |
| E-A2 | Full customer | Six tiles populated on one screen; no stays or subscriptions tile |
| E-A3 | Degraded scenario (balances and visits Unavailable) | Unavailable tiles with Try again; others intact; after recovery Try again fills them |
| E-A4 | Follow each tile's link | Built targets reached (`/portal/cats`, `/portal/appointments`, `/portal/billing`, `/book?..`, `/portal/visits/..`); links to unbuilt areas (orders, prescriptions) asserted by href only and recorded below |
| E-B1 | Re-order a delivered order | Dialog lists the count added; **View basket** href `/cart` (page not built: ORD-002) |
| E-B2 | Re-order an order with an out-of-stock and a prescription item | Both under Not added with reasons |
| E-B3 | Re-order the same order again | RETIRED as specified ("Already in your basket" is not delivered by Orders); replaced by: second click is a second request with a new key and the dialog reports whatever the server returns |
| E-X1 | axe on `/portal` (full, new, degraded) and with the dialog open | Zero serious/critical |
| E-X2 | `/portal` at 360 / 768 / 1024 / 1440 | No horizontal scroll |
| E-X3 | Lighthouse mobile on `/portal` against the real API | NOT-RUN (no Lighthouse tooling in this environment; recorded as owed) |
| E-R | Real-API run per HO-POR-001 | see Results |

## Cross-area links (section 20.5)

| Link | Constant | Target built? |
|---|---|---|
| Cats list / add / cat | `/portal/cats`, `/portal/cats/new`, `/portal/cats/{id}` | Yes (CAT-002) |
| Appointments, book | `/portal/appointments`, `/book` | Yes (APT-002) |
| Billing, invoice | `/portal/billing`, `/portal/billing/invoices/{id}` | Yes (BIL-002) |
| Visit summary, cat's visits | `/portal/visits/{id}`, `/portal/cats/{id}/visits` | Yes (EMR-002) |
| Prescriptions | `/portal/prescriptions` | NO (HO-PHM-002 unbuilt) |
| Orders list / order | `/portal/orders`, `/portal/orders/{id}` | NO (HO-ORD-002 unbuilt) |
| Basket | `/cart` | NO (HO-ORD-002 unbuilt) |

## Results (developer run, 2026-10-13, head recorded in the handover report)

Commands: `npx tsc --noEmit` clean; `npm run lint` (`--max-warnings 0`) clean; `npx vitest run --maxWorkers=2` 193 files / 1263 tests passed (the 38 below are this handover's);
`npx next build` succeeded. Playwright with `--workers=1`, `PORT=3118`, `NODE_OPTIONS=--max-old-space-size=6144`, one spec at a time.

| Suite | Result |
|---|---|
| Vitest `tests/components/portal/` (3 files) | **48 passed** (dashboard 23, reorder 20, links-and-guards 5) |
| Playwright MSW, `chromium`: `portal-dashboard.spec.ts` | **11 passed** |
| Playwright MSW, `chromium`: `portal-a11y-responsive.spec.ts` | **23 passed** (first run 20 passed and 3 failed on a sign-in/first-navigation race, below; re-run with one retry configured: 23 passed) |
| Playwright MSW, `mobile-360`: `portal-dashboard.spec.ts` | **11 passed** (E-B2 flaky: failed once, passed on its retry) |
| Playwright MSW, `mobile-360`: `portal-a11y-responsive.spec.ts` | 22 passed, 1 failed in the first run (`new at 360`: page showed /sign-in, the same race); that case alone: passed; whole spec again with `--retries=0`: **23 passed** |
| Playwright REAL API, `chromium`: `portal-real-api.spec.ts` (API :5014, DB `catecommerce_por002e2e`, web :3118, `NEXT_PUBLIC_USE_MOCKS=false`) | **2 passed** |

Flake note and fix (gate follow-up 2026-10-13): the earlier failures (landing on `/sign-in` after sign-in, and once `POST /api/auth/sign-in` answering 404) were one race. `app/providers.tsx` starts
MSW in an effect after first paint while the session guard and the dashboard query fire their first `/api/` fetch in earlier child effects; on a busy machine or a cold compile the worker is late, the first
read fails as a network error and the guard redirects (and the fixtures' sign-in, run before `window.__apiBaseUrl` is set, reaches Next.js and gets a 404). The race lives in shared files outside section 13,
so the portal specs close it at the test boundary: `tests/e2e/portal-helpers.ts` holds page `fetch` to `/api/` until `__mswReady`, waits for `__apiBaseUrl`, and waits for the session endpoint to answer 200
before navigating. `retries: 1` is removed. Re-run with `--retries=0`, one spec at a time: chromium dashboard 11/11, chromium a11y 23/23, mobile-360 dashboard 11/11, mobile-360 a11y 23/23.
(The first retries-free attempt, before the helper, failed 2 of 23 and 1 of 23 in the a11y spec, so the helper is what made it stable.) Vitest portal now 47 tests (reorder 19: key renewal 4xx x3, 5xx, 409 RequestInProgress x2, network, 400 IdempotencyKeyRequired; dashboard 23: order date near midnight in Asia/Dhaka).

| Case | Status | Evidence |
|---|---|---|
| U-A1, U-A2, U-A3, U-A4, U-A5, U-A6, U-A7, U-A8, U-A9, U-A10, U-A12, U-A13, U-A14, U-A15, U-A16, U-A17 | PASS | `dashboard.test.tsx` |
| U-A11 | N/A (retired: subscriptions tile) | replaced by U-A11b, PASS |
| U-B1 (+ key reuse U-B1b), U-B2, U-B3, U-B4 (x3), U-B5, U-B6, U-B7, U-B8 | PASS | `reorder.test.tsx`; U-B7 in `dashboard.test.tsx` |
| U-X1 | PASS | `dashboard.test.tsx` |
| U-X2 | PASS | `links-and-guards.test.tsx` (existing `SignOutButton`, dashboard key gone) |
| U-X3 | PASS | `reorder.test.tsx` (no `setItem`, no console output) |
| U-X4, U-L1 | PASS | `links-and-guards.test.tsx` |
| E-A1 | PASS (MSW and real API) for the new-customer page and Add a cat; landing half PASS after owner decision 2026-10-13 ("Land on /portal"; `portal-dashboard.spec.ts` sign-in landing cases and `home.spec.ts`; `?next=` still wins) | `portal-dashboard.spec.ts`, `portal-real-api.spec.ts` |
| E-A2 | PASS on MSW (six tiles, no stays/subscriptions). Real API: cats tile with a registered cat and the exact key set of the dashboard; orders, visits, prescriptions and balances tiles NOT exercised against real data (a fresh database has none) | as above |
| E-A3 (+b, c) | PASS (MSW only; the mock recovers through the scenario cookie) | `portal-dashboard.spec.ts` |
| E-A4 | PASS for built targets; `/portal/orders` and `/portal/prescriptions` asserted by href only (pages unbuilt) | `portal-dashboard.spec.ts` |
| E-B1, E-B2 | PASS (MSW). Real API: only the refusal path (`400` without `Idempotency-Key`, `404` for an order that is not the caller's); a successful re-order needs an order, which needs the checkout (HO-ORD-002 unbuilt) | `portal-dashboard.spec.ts`, `portal-real-api.spec.ts` |
| E-B3 | RETIRED as specified; replaced (second click is a second request) and PASS on MSW | `portal-dashboard.spec.ts` |
| E-C* | N/A (README C-6, BRD skipped) | |
| E-X1 | PASS: zero serious/critical on six fixtures and with the result dialog open, chromium and mobile-360 | `portal-a11y-responsive.spec.ts` |
| E-X2 | PASS at 360 / 768 / 1024 / 1440 for full, new, many-cats, and the dialog fits the viewport | as above |
| E-X3 | **DEFERRED to HO-XCT-002 E-B1** (owner 2026-10-13, "Defer to XCT-002"); `/portal` added to its routes | |

Not built / owed: ReorderButton on the order-detail page (page does not exist), Lighthouse LCP, a real-API re-order success, real-API balances/orders/visits/prescriptions tiles with data.

## TEST gate run (2026-10-13, head ff31a745; supersedes the developer counts above)

Environment: Windows, worktree cat_ecommerce-wt/POR-002. tsc clean; lint clean; `vitest run --maxWorkers=2`: 193 files, 1272 tests passed, 0 failed (376 s);
`next build` exit 0. Playwright MSW with `--retries=0 --workers=1`, PORT=3118, one spec at a time. The real API was not started (RPT-001 and EMR-001-F builds running), so `portal-real-api.spec.ts` is NOT RUN by this gate.

| Case | Status | Evidence |
|---|---|---|
| U-A1..A10, U-A12..A17 | PASS | dashboard.test.tsx (23), full run and folder run |
| U-A11 | N/A (retired); U-A11b PASS | dashboard.test.tsx |
| U-B1, U-B1b, U-B2..B8 | PASS | reorder.test.tsx (19); U-B7 in dashboard.test.tsx |
| U-X1, U-X2, U-X3, U-X4, U-L1 | PASS | dashboard, links-and-guards, reorder |
| E-A1 (new customer page, Add a cat) | PASS chromium and mobile-360 | portal-dashboard.spec.ts 11/11 on each |
| E-A1 (lands on /portal after sign-in) | BLOCKED-SCOPE | backlog POR 26 |
| E-A2, E-A3 (b, c), E-A4, E-A5, E-B1, E-B2, E-B3 (replaced), E-B4 | PASS chromium and mobile-360 | as above |
| E-X1, E-X2 | PASS | portal-a11y-responsive.spec.ts 23/23 on each project |
| E-X3 Lighthouse | NOT RUN | no tooling; owed |
| E-R real API | NOT RUN by this gate | API not started; developer's run stands |
| Real-API re-order success and data tiles | NOT RUN | needs ORD-002 checkout |
| E-C* | N/A | BRD skipped |

Regression rows for the fix round (each proved by temporarily mutating production code, running tests/components/portal, then restoring the file; git status clean afterwards):

| ID | Finding | Mutation | Result | Status |
|---|---|---|---|---|
| R1 | 409 RequestInProgress keeps the key (POR 30) | renew for RequestInProgress | "409 RequestInProgress keeps the key on the retry" failed | PASS |
| R2 | same, message | in-progress message branch disabled | "...says the first request is still processing" and retry case failed | PASS |
| R3 | 5xx keeps key (POR 33) | renew on 5xx | "a 5xx keeps the key" failed | PASS |
| R4 | 4xx renews | never renew on 4xx | 400, 404, 409 StaleVersion renew cases failed (3) | PASS |
| R5 | network error keeps key | hook renews on every error | U-B1b, network, 5xx, 409 cases failed (4) | PASS |
| R6 | OrdersTile Asia/Dhaka (POR 35) | date mapped in UTC | 20:00Z on 9 Oct shown as 10 Oct case failed | PASS |
| R7 | race, no retries (POR 34) | grep: no retries in specs; four runs with --retries=0 | 11, 23, 11, 23 passed; a race is not proved by green runs (POR 38) | PASS with caveat |
| R8 | handover Amended row (POR 29) | read | present | PASS (read) |
| R9 | 400 IdempotencyKeyRequired text (POR 37) | not mutated | 400 case green in the run | PASS |

Non-portal vitest files: goods-receipt-form 7/7 and walk-in-customer-form 4/4 each passed alone on this branch and alone on master (b12fd39d), and in the full run. The branch changes neither file nor their imports (only a prettier reflow of two messages in lib/api/errors.ts). Not caused by this branch.
