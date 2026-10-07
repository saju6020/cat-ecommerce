# TC-HO-SHP-002 — Shop Catalogue and Products (frontend)

Handover: docs/handover/HO-SHP-002.md
Branch: `ho/SHP-002` (from master `cffaa79`); code head `8b09024`.
Developer sheet written 2026-10-04; TEST gate re-executed independently 2026-10-04 and 2026-10-05 at code head 8b09024. Every status below was reset to NOT RUN and then set from what the gate observed itself.

> Statuses record what was **observed**. `not run — no API` marks a real-API case whose upstream is
> absent in this worktree; it is recorded with its reason, never passed.

## Summary

| | Count |
|---|---|
| Rows (U, E, R, S) | 69 |
| PASS | 68 |
| FAIL | 1 (S-1, header without JavaScript) |
| BLOCKED | 0 rows; 1 side check blocked (E9 on master, see below) |
| NOT RUN | 0 |
| Real-API rows | 7, all "not run — no API" (table below) |

The developer's U/E rows were reset to NOT RUN and re-set from what the gate watched. IDs R-1..R-5 and S-1..S-9 were added by the gate.

## Mock-mode (MSW) runs

Component tests: `web/tests/components/**` (Vitest + Testing Library, MSW). End-to-end:
`web/tests/e2e/shop-*.spec.ts` (Playwright, `PORT=3104`, `--workers=1`, one project per run).

| ID | Case | Where | Status |
|---|---|---|---|
| U-A1 | Origin, brand, sort accumulate in the URL; page resets; sort keeps filters | `catalogue/catalogue-controls.test.tsx`, `catalogue/catalogue-params.test.ts` | PASS (vitest verbose run, test named by this ID watched green) |
| U-A2 | Mobile sheet: two changes, **Show results** navigates once, focus returns, count announced | `catalogue/catalogue-controls.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-A3 | Card: From price, Out of stock, Vet authorisation needed, rating text, placeholder image | `catalogue/product-card.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-A4 | No results: Did you mean rewrites `q` keeping filters; popular grid | `catalogue/product-card.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-A5 | Price min 500 / max 200: inline error, no navigation | `catalogue/catalogue-controls.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-A6 | `catalogueParams` round trip, unknown params dropped, repeated values kept | `catalogue/catalogue-params.test.ts` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B1 | Second variant: `?sku=`, price, availability, variant images follow | `product/product-page.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B2 | Out-of-stock variant: alert offered, others still In stock; every SKU out of stock handled | `product/product-page.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B3 | `expiryNotice` shows the best-before sentence; absent without it | `product/product-page.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B4 | Tabs: ingredients as text, analysis table with `%` per cell, feeding guide markup, storage, origin | `product/product-page.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B5 | Kit contents with quantities and availability text | `product/product-page.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B6 | Prescription gate: guest, no authorisation, two eligible cats | `prescription/prescription.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B7 | Cat picker keyboard operation and required error | `prescription/prescription.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B8 | Reviews: Write / Edit your review / NotPurchased text | `reviews/reviews-recommendations.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B9 | Recommendation blocks: empty block absent; "for your cats" heading | `reviews/reviews-recommendations.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B10 | `willNotify: false` shows OQ-7 text and the settings link | `reviews/reviews-recommendations.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B11 | Guest Save is a sign-in link with `?next=` product and variant | `product/product-page.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-B12 | Review `<b>x</b>` and `**x**` render literally | `reviews/reviews-recommendations.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-C1 | Origin grid: flagless origin gets a globe, name as text, count, excerpt; flags `alt=""` | `shop/shell.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-C2 | Origin page: country fixed, other filter groups present | `catalogue/catalogue-controls.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-D1 | Header: Publish enabled with unmet item; server message; focus on checklist | `staff-shop/staff-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-D2 | Food section absent for Litter; present with `%` units for Cat Food | `staff-shop/staff-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-D3 | `stock: null` reads No stock recorded; sellable 0 reads 0 sellable | `staff-shop/staff-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-D4 | Schedule a price: Now sends the current instant; backdated refusal shown inline; `min` on the date | `staff-shop/staff-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-D5 | Images: alt text required; honest upload note; no upload control | `staff-shop/staff-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-D6 | Section save 409: reload message; values kept | `staff-shop/staff-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-D7 | Translations hidden with one locale; one `bn` sub-form with two | `staff-shop/staff-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-D8 | Barcode typed + Enter searches and updates the URL | `staff-shop/staff-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-D9 | Reject without reason is a field error; with reason sends | `staff-shop/staff-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-E1 | Food suggestions: reasons, exclusion sentence, date-of-birth hint | `reviews/portal-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-E2 | Review `ReviewNotPermitted` shown plainly; values kept | `reviews/portal-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-E3 | Wishlist No longer sold; alert status text | `reviews/portal-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-F1 | Maintenance notice (initial state, client watch, storefront 503) | `shop/shell.test.tsx`, `shop/catalogue-view-503.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-F2 | Language switcher absent with one locale, present with two | `shop/shell.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-F3 | `fieldLocales`: feeding guide `lang="en"`, others `lang="bn"` | `product/product-page.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-X1 | Server 400 mapped to its field; `errorCode` not in the DOM | `reviews/portal-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-X2 | Dirty editor section / review form asks before leaving | `reviews/portal-shop.test.tsx`, `staff-shop/staff-shop.test.tsx` | PASS (vitest verbose run, test named by this ID watched green) |
| U-X3 | No `dangerouslySetInnerHTML`; no `fetch(` outside `client.ts`; hooks only in `lib/hooks` | `shop/repository-scan.test.ts` | PASS (vitest verbose run, test named by this ID watched green) |

| ID | Case | Spec | Status (MSW) |
|---|---|---|---|
| E-A1 | AC-5 journey: Shop by Country → Thailand → Kitten + In stock → product | `shop-product.spec.ts` | PASS (MSW; chromium and mobile-360). Timings: not run — no API |
| E-A2 | Filter, sort, page 2, reload, Back | `shop-catalogue.spec.ts` | PASS (MSW; Back exercised from the next navigation, see §1 Amended 9) |
| E-A3 | Misspelled brand; nonsense; Did you mean | `shop-catalogue.spec.ts` | PASS (MSW; chromium 29/29 and mobile-360 29/29 dedicated runs) |
| E-A4 | Ingredient and tag search | `shop-catalogue.spec.ts` | PASS (MSW; chromium 29/29 and mobile-360 29/29 dedicated runs) |
| E-B1 | Out-of-stock variant marked, others in stock | `shop-product.spec.ts` | PASS (MSW; chromium 29/29 and mobile-360 29/29 dedicated runs) |
| E-B2 | Prescription diet: guest → sign in → no authorisation → Book a consultation | `shop-product.spec.ts` | PASS (MSW; asserts the destination URL only). `/book` landing: not run — no API |
| E-B3 | Kit with an out-of-stock component | `shop-product.spec.ts` | PASS (MSW; chromium 29/29 and mobile-360 29/29 dedicated runs) |
| E-C1 | Staff create → refused without origin → add → publish | `shop-staff.spec.ts` | PASS (MSW; the "visible in the shop" half: not run — no API) |
| E-C2 | Scheduled price listed; storefront unchanged | `shop-staff.spec.ts` | PASS (MSW; chromium 29/29 and mobile-360 29/29 dedicated runs) |
| E-C3 | Moderate a pending review; it appears on the product page | `shop-staff.spec.ts` | PASS (MSW; chromium 29/29 and mobile-360 29/29 dedicated runs) |
| E-D1 | Wishlist; alert with and without consent | `shop-portal.spec.ts` | PASS (MSW; chromium 29/29 and mobile-360 29/29 dedicated runs) |
| E-D3 | Food suggestions with chicken allergy | `shop-portal.spec.ts` | PASS (MSW; chromium 29/29 and mobile-360 29/29 dedicated runs) |
| E-E1 | Maintenance replaces the storefront; back office works | `shop-catalogue.spec.ts` | PASS (MSW; chromium 29/29 and mobile-360 29/29 dedicated runs) |
| E-F1 | `bn` with `en` fallback; staff edit of the `bn` feeding guide persists in the editor | `shop-staff.spec.ts` | PASS (MSW; the storefront-edit half: not run — no API) |
| E-X1 | axe on every §8.1 route, open filter sheet, prescription gate, review form, editor with errors | `shop-a11y-responsive.spec.ts` | PASS (MSW; `shop-a11y-responsive` run alone: chromium 42/42, mobile-360 42/42) |
| E-X2 | 360 / 768 / 1024 / 1440 px, no horizontal scroll; grid columns | `shop-a11y-responsive.spec.ts` | PASS (MSW; `shop-a11y-responsive` run alone: chromium 42/42, mobile-360 42/42) |
| E-X3 | LCP structure (plain `<img>`, `fetchpriority=high`, dimensions, lazy elsewhere); server-rendered without JavaScript | `shop-product.spec.ts` | PASS (structure only; the LCP 2.5 s budget: not run — no API/4G) |

Findings from the build that changed the code: filter checkboxes bound straight to the URL snapped
back until the navigation landed (fixed with `pending`, ADR-FE-031); axe flagged `text-ink-faint` for
contrast, `aria-label` on role-less `div`s, and a nowrap badge overflowing a 2-column card at 360 px
(all fixed); a streamed Suspense boundary hid the catalogue without JavaScript (removed).

## Real API runs

Recorded "not run — no API" in this worktree (no API process, and the dependent areas are not built here):

| ID | Reason |
|---|---|
| E-A1 timings (each catalogue response < 500 ms) | needs the real API; MSW timings are not a budget |
| E-X3 LCP ≤ 2.5 s on throttled 4G | needs the real API and a production build; only the structural half ran |
| E-B2 `/book` landing | `/book` is HO-APT-002's route (sibling branch) |
| E-C1 "visible in the shop", E-F1 storefront edit | MSW browser and server databases are separate; needs the real API |
| E-C3 real run | no purchase can exist before HO-ORD-001 |
| E-D2 back-in-stock message in the inbox | needs the real worker passes and HO-NTF-002 |
| Staff stock copy and expiry notice | deferred in HO-SHP-001 to the HO-IMP-001 follow-up (backlog SHP row 51) |

## Rows added by the TEST gate (CODE_REVIEW fixes and the spot checks asked for)

| ID | Case | Where | Status |
|---|---|---|---|
| R-1 | Two-step save: details saved, slug refused: typed slug and a partial-success message stay | `staff-shop/two-step-save.test.tsx` | PASS (watched green; mutation M1 fails it, see R-5) |
| R-2 | Two-step save: labelling saved, prescription flag refused: ticked box and message stay | `staff-shop/two-step-save.test.tsx` | PASS (watched green; mutation M1b fails it) |
| R-3 | Mobile sheet: typing a multi-digit price keeps focus on the same input | `catalogue/catalogue-controls.test.tsx` | PASS (watched green; mutation M2 fails it) |
| R-4 | Failed purchasability read: neutral retry, never "no authorisation" | `prescription/prescription.test.tsx` | PASS for the component (a mutation of PrescriptionGate fails it). The wiring `isError={signedIn && purchasability.isError}` in ProductPurchase is NOT covered: mutation M3 (`isError={false}`) left 20/20 tests green. MEDIUM gap |
| R-5 | Mutation check: reintroduce each fix's defect in a scratch copy, confirm the test fails | scratch copy | PASS. Defect reintroduced in the working tree and reverted with `git checkout`: M1 (details page `key` on version + invalidate after the first command) fails two-step test 1; M1b (food page) fails test 2; M2 (`key` on PriceFilter in CatalogueFilters) fails the sheet-focus test; PrescriptionGate ignoring `isError` fails the neutral-retry test; M3 (the wiring in ProductPurchase) survives. Tree clean afterwards |
| S-1 | `/shop` header renders with JavaScript disabled | Playwright, scratch spec | FAIL. JavaScript disabled, `next dev` + MSW: `/shop` has no visible header, nav or search. The header is streamed inside `<div hidden id="S:0">` with a `<template id="B:0">` fallback and is revealed only by the inline `$RC` script. Reproduced twice. Production `/shop` (`next start`, API absent) rendered the header inline, so it depends on whether the Suspense boundary resolves with the shell |
| S-2 | `/shop/countries` header renders with JavaScript disabled | Playwright, scratch spec | PASS in dev (header and nav inline, 2 of 2 runs); but the same build streams the header hidden on `/shop/products`, so the result is not reliable. Production `/shop/countries` could not be observed (500 with no API) |
| S-3 | Catalogue (cards, filters as links) renders with JavaScript disabled | Playwright, scratch spec | PASS in dev: `/shop/products` with JS off shows 24 cards with real `/shop/products/<slug>` links, the H1, 45 filter checkboxes and 6 "Out of stock" texts. (Its header is missing, see S-1) |
| S-4 | Fail-closed availability: every SKU out of stock and no cart/add action | component tests, source read | PASS. `handles every SKU being out of stock` (3 group labels plus more, no add-to-cart button) green; `leaves the action slot empty` green; grep finds no add-to-cart or checkout control in app, components or lib |
| S-5 | Prescription diets and kits are not purchasable | U-B5, U-B6, E-B2, E-B3 | PASS. U-B6 (guest, no authorisation, eligible cats), U-B5 (kit unavailable with a component out), E-B2, E-B3 green; there is no purchase control to enable (BuyBox action slot empty) |
| S-6 | Image placeholder with the API's alt text | U-A3 plus source read | PASS. U-A3 asserts an `img` named by the API alt text ("Royal Canin Kitten bag") with `src=/images/product-placeholder.svg` when `url` is null; a scratch probe confirmed a hostile alt string stays an attribute value |
| S-7 | Plain-text fields: HTML in a product name or review renders as text | U-B12 plus scratch probe | PASS. U-B12 and the card test green; a scratch vitest (deleted) with `<img onerror>` and `<script>` in name, brand, heading and alt: no injected element, no `onerror` attribute, `window.__pwned` unset |
| S-8 | `next build` succeeds | `npx next build` | PASS. `npx next build` exit 0, "Compiled successfully in 2.3min", no Suspense/useSearchParams error. `/shop`, `/shop/countries`, `/shop/products`, `/shop/products/[slug]` are all dynamic (ƒ), not prerendered |
| S-9 | No `msw` / `__mswFetch` code can activate in the production build or server | `.next` inspection, `next start` probe | PASS. In `.next` (cache packs excluded): `instrumentation.js` registers an empty `register(){}`; no `msw`, `msw/node` or `mockServiceWorker` reference in `.next/static` or `.next/server` JS. Only `globalThis.__mswFetch ?? fetch` remains in `.next/server/chunks/358.js` (the getPublic reader): inert, nothing in production assigns it. `public/mockServiceWorker.js` is a static file that only the dev-gated import registers. `next start` served `/shop` 200 |

## Suite runs

| Run | Result |
|---|---|
| `tsc --noEmit` | PASS: exit 0 |
| `npm run lint` | PASS: exit 0 (max-warnings 0) |
| `vitest run --maxWorkers=3` | PASS: 111 files, 513 tests, 0 failed |
| Playwright `chromium`, shop specs | PASS after one rerun. Run 1 (shop-catalogue, shop-product, shop-portal, shop-staff, home): 28 passed, 1 failed (`home.spec` signed-in customer, fixture POST `/api/auth/sign-in` 404). Rerun of `home.spec` on a fresh server: 4/4 |
| Playwright `chromium`, `shop-a11y-responsive` alone | PASS: 42/42 (4.1 min) |
| Playwright `mobile-360`, shop specs | PASS: 29/29 |
| Playwright `mobile-360`, `shop-a11y-responsive` alone | PASS: 42/42 (6.1 min) |
| Playwright `chromium`, master-era specs (real-api excluded) | See "Master-era regression run" below. Only `cat-vaccinations` E9 fails persistently (known) |
| `next build` | PASS: exit 0 |

## Master-era regression run (chromium, `real-api` excluded)

The first sweep (`--workers=1`, one dev server) used a spec-name filter that also matched `home` and the shop specs, so it ran 462 tests. It hit my own 3000 s timeout at test 456, on a machine shared with another worktree's e2e run. Results: 440 passed, 16 failed, 6 not reached. Every failure was then rerun on a fresh server:

| Test | Sweep | Rerun | Diagnosis |
|---|---|---|---|
| admin-a11y-responsive `/staff/admin/content` axe; `/staff/admin/tax-classes at 1440px` | timeout | PASS (21/21 in the filtered rerun) | cold-compile load |
| admin-functional E-F1 | timeout 1.0 min | PASS | load |
| admin-functional E-B1 | passed | rerun 1: fixture POST `/api/staff/auth/sign-in` 404; rerun 2: PASS | MSW worker not ready on a cold server (same signature as the `home.spec` customer failure) |
| cat-medical-history E14 | 30 s timeout | PASS | load |
| cat-profile-header E3, E4 | timeout | E3 PASS; E4 failed once, then PASS | load |
| home.spec a11y | 30 s timeout | PASS (4/4 in two dedicated runs) | load |
| shop-a11y staff `/staff/shop/products`, `/variants`, `/reviews` | timeout | PASS (42/42 alone, chromium and mobile-360) | load |
| shop-portal E-D3, shop-staff E-C1, E-C3 | timeout (23 s, 2.0 min, 30 s) | PASS in the dedicated chromium and mobile-360 runs | load; not rerun again after the sweep, the dedicated runs are the evidence |
| staff-shell-manual-expand-occludes-content "Escape closes the sheet" | timeout | failed once in rerun 1, PASS in rerun 2 | load/flake |
| staff-shell-manual-expand-overflow (456) and the 6 tests not reached | killed by my timeout | PASS (rerun 1 covered those files in full) | none |
| cat-vaccinations E9 | FAIL | FAIL again (twice more) | pre-existing, see below |

`cat-vaccinations` E9 ("after recording a vaccination, the status tab is refreshed, not stale") fails persistently here. The branch has no change under `staff/cats`, vaccination code or the cat mock handlers (`git diff cffaa79 HEAD`). Master-side confirmation was attempted and is BLOCKED: a throwaway master worktree with a `node_modules` junction would not start (Next could not resolve through the junction), and the worktree and junction were removed. The caller states E9 also fails on master.

Not in this worktree: the `notifications-*` specs (HO-NTF-002 merged to master after this branch was cut), so they were not run here.

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| Every "real API" row (Real API runs table above: E-A1 timings, E-X3 LCP, E-B2 `/book`, E-C1 and E-F1 storefront halves, E-C3, E-D2, staff stock copy) | not run — no API | a running HO-SHP-001 API, HO-APT-002 `/book`, HO-ORD-001, HO-NTF-002 worker passes |
| `cat-vaccinations` E9 on master | the junction worktree could not run Next | a master checkout with its own `node_modules` |
| Production no-JS header on `/shop/countries` and `/shop/products` | production pages return 500 with no API | a running API, or a production run with mocks |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| ProductPurchase to PrescriptionGate error wiring (M3 survived) | the gate does not edit tests | implementer: a ProductPurchase test with a failing purchasability handler |
| No-JS header has no e2e assertion | E-X3 checks the product page body only | implementer: assert the header link and nav with `javaScriptEnabled: false` on `/shop`, `/shop/countries`, `/shop/products` |
