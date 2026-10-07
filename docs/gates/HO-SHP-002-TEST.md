GATE: TEST
HANDOVER: HO-SHP-002
COMMITS: 8b09024 (code head under test); b9021c3 (docs head). This gate changed no product or test code; it committed only this record and the TC sheet.
DATE: 2026-10-05
STATUS: PASS

Sheet: `docs/test-cases/TC-HO-SHP-002.md`. The developer's rows were reset to NOT RUN, then set from what this gate watched. Rows were added for the CODE_REVIEW fixes (R-1..R-5) and the requested spot checks (S-1..S-9). Final: 69 rows, 68 PASS, 1 FAIL (S-1), 0 BLOCKED, 0 NOT RUN. Seven real-API rows are recorded "not run — no API".

Owner rule (2026-09-27): no BLOCKER and no HIGH finding, so the gate passes. MEDIUM and LOW items are for `docs/backlog/SHP.md` (not edited here).

SUITE RESULTS (web/, Windows, `PORT=3104`, `--workers=1`, one dev server at a time)
- `npx tsc --noEmit`: exit 0.
- `npm run lint`: exit 0 (max-warnings 0).
- `npx vitest run --maxWorkers=3`: 111 files, 513 tests, 0 failed.
- `npx next build`: exit 0, "Compiled successfully", no Suspense/`useSearchParams` error. `/shop`, `/shop/countries`, `/shop/products` and `/shop/products/[slug]` are dynamic (ƒ). `.next` deleted afterwards.
- Playwright chromium, shop specs + home (29 tests): run 1 had 28 passed and 1 failed (`home.spec` signed-in customer: fixture POST `/api/auth/sign-in` returned 404). Rerun of `home.spec` on a fresh server: 4/4 passed.
- Playwright mobile-360, shop specs + home: 29/29.
- `shop-a11y-responsive` alone: chromium 42/42, mobile-360 42/42.
- Master-era specs, chromium, `real-api` excluded: a first sweep (my filter also matched home and the shop specs, 462 tests) was cut at test 456 by my own timeout, on a machine shared with another worktree's e2e run: 440 passed, 16 failed, 6 not reached. Every failure was a timeout or a cold-start fixture failure and passed on a rerun (detail in the sheet), except `cat-vaccinations` E9, which failed in the sweep and in two reruns. The shop tests that timed out in the sweep (E-D3, E-C1, E-C3, three staff axe routes) passed in the dedicated runs above.
- `cat-vaccinations` E9: pre-existing. This branch has no change to `staff/cats`, vaccination code or the cat mocks. Not counted against the branch. This gate could not run it on master itself (a throwaway master worktree with a `node_modules` junction would not start Next; it was removed).
- The `notifications-*` specs are not in this worktree (HO-NTF-002 merged after the branch was cut), so they were not run.
- Cleanup: all dev and production servers stopped, port 3104 confirmed free, `.next` deleted, scratch specs and the scratch vitest deleted.

SPECIFIC CHECKS
- No-JavaScript header (S-1, S-2): FAIL on `/shop` in `next dev`. With `javaScriptEnabled: false` there is no visible header, nav or search. The HTML holds `<template id="B:0">` (the `fallback={null}`) and the header sits in `<div hidden id="S:0">`, which only the inline `$RC` script reveals. `/shop/countries` rendered the header inline in the same runs; `/shop/products` streamed it hidden like `/shop`. In `next start` (API absent) `/shop` rendered the header inline and `/shop/countries` and `/shop/products` returned 500, so production behaviour with a live API was not observable. The result depends on whether the Suspense boundary resolves with the shell. Page bodies are unaffected.
- Catalogue without JavaScript (S-3): PASS. `/shop/products` showed 24 cards with real `/shop/products/<slug>` links, the H1, 45 filter checkboxes and 6 "Out of stock" labels.
- Fail-closed availability (S-4): PASS. A product with every SKU out of stock shows "Out of stock" on every variant and has no cart control. The action slot is empty and no add-to-cart or checkout control exists anywhere in `app`, `components` or `lib`.
- Prescription diets and kits (S-5): PASS. The gate (guest, no authorisation, eligible cats), kit unavailable with an out-of-stock component, E-B2 and E-B3 are green; there is nothing to purchase with.
- Image placeholder with the API alt text (S-6): PASS (U-A3: `img` named "Royal Canin Kitten bag", `src=/images/product-placeholder.svg`, API `url` null). A hostile alt string stays an attribute value.
- Plain-text fields (S-7): PASS. U-B12 and the card test are green. A scratch vitest (deleted) put `<img src=x onerror=...>` and `<script>` in product name, brand, heading and alt: no element injected, no `onerror` attribute, `window.__pwned` never set.
- ARCHITECTURE leftovers: `next build` succeeded with no `useSearchParams` Suspense error. The routes are dynamic, so the "statically prerendered header" concern does not apply; the streaming behaviour above is the remaining form of it.
- msw in production (S-9): PASS. In `.next` (cache packs excluded) `instrumentation.js` registers an empty `register(){}`. No `msw`, `msw/node` or `mockServiceWorker` string appears in `.next/static` or `.next/server` JS. Only `globalThis.__mswFetch ?? fetch` remains, in `.next/server/chunks/358.js`; nothing in production assigns `__mswFetch`, so it is inert. `next start` served `/shop` with 200.
- Mutation check of the CODE_REVIEW fixes (R-5): each defect was reintroduced in the working tree and reverted with `git checkout`. Details-page `key` on version plus invalidate after the first command: two-step test 1 fails. Same on the food page: test 2 fails. `key` on `PriceFilter`: the sheet-focus test fails. `PrescriptionGate` ignoring `isError`: the neutral-retry test fails. One mutation survived (second MEDIUM below).

FINDINGS
- [MEDIUM] web/app/(shop)/layout.tsx:42-55 — `ShopHeader` is wrapped in `<Suspense fallback={null}>`, and with JavaScript disabled the header (logo, search, category nav, account links) can be absent. Reproduced on `/shop` and `/shop/products` in `next dev`: the header streams into `<div hidden id="S:0">` and stays hidden. The page body and the product links still render. No acceptance criterion requires the header without JavaScript and E-X3 checks only the product page body, so this does not fail the gate. It does contradict the component's own claim that the category nav works without JavaScript, and ADR-FE-030's no-JavaScript reasoning (ARCHITECTURE raised it as LOW and left it to this gate). Options: drop the Suspense, or narrow the boundary to the `useSearchParams` islands so the logo and category links are in the shell. Add an e2e with `javaScriptEnabled: false` asserting the header link and nav on `/shop`, `/shop/countries` and `/shop/products`.
- [MEDIUM] web/app/(shop)/_components/ProductPurchase.tsx:~110 with tests — the CODE_REVIEW fix for the purchasability error is tested only at `PrescriptionGate` (the `isError` prop). Replacing `isError={signedIn && purchasability.isError}` with `isError={false}` leaves all 20 prescription and product-page tests green, so the wiring from the failed query to the neutral retry has no test. Add a ProductPurchase test with a failing purchasability handler for a signed-in customer.
- [LOW] web/tests/e2e/fixtures.ts (sign-in fixtures) — on a freshly started dev server the first sign-in POST can return 404 because the MSW worker is not ready (seen on `home.spec` customer and `admin-functional` E-B1; both passed on rerun). Not specific to this branch; the fixtures could wait for the worker.
- [LOW] cat-vaccinations E9 — fails persistently here, pre-existing per the caller. This gate could not confirm it on master itself.
- [LOW] With the API unreachable, production `/shop/countries` and `/shop/products` returned HTTP 500 (observed with `next start` and no API; only `/shop` degraded to 200). Informational; real-API behaviour belongs to the HO-SHP-001 integration run.
- [LOW] `globalThis.__mswFetch` is still read in production code (`getPublic`) and so appears in the server bundle. Inert, as designed in ADR-FE-030; noted for completeness.
- Not re-raised: the MEDIUM and LOW items from earlier gates (health focus write-only, SKU state on Back, translations remount, `PartialSaveError` conflict mapping, reload on a failed refetch, and the rest).

Not run — no API: E-A1 timings, E-X3 LCP on throttled 4G, E-B2 `/book` landing, the "visible in the shop" halves of E-C1 and E-F1, E-C3 real run, E-D2, staff stock copy and expiry notice.

SCOPE CHECK
- files in allowed scope: 2/2 (`docs/test-cases/TC-HO-SHP-002.md`, `docs/gates/HO-SHP-002-TEST.md`)
- files outside allowed scope: none. No product or test code was edited; scratch specs and the scratch vitest were created and deleted before the commit.
