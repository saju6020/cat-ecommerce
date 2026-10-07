# HO-SHP-002 — Shop Catalogue and Products (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-SHP-002** |
| Title | Shop — Catalogue and Products — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §8 — **US-SHP-01 … US-SHP-14** |
| SRS | FR-SHP-1 … FR-SHP-15; **UI-1, UI-3, UI-4, UI-6**; **NFR-PRF-1**, NFR-PRF-2, NFR-CMP-2, NFR-USA-4, NFR-USA-5, NFR-USA-6, NFR-SCL-3; FR-ADM-7 and FR-ADM-8 (storefront rendering of banners and the maintenance notice); AC-5, AC-7 |
| Pairs with | [`HO-SHP-001`](HO-SHP-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-SHP-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, stack, layering, portal cat page), [`HO-ACC-002`](HO-ACC-002.md) (session model, `client.ts`, route groups, `?next=` guard, error mapping, `UnsavedChangesGuard`, `PermissionDeniedState`, preference page), [`HO-ADM-002`](HO-ADM-002.md) (`lib/format` money and time, `LimitedMarkup` renderer, `public-content.ts` banners and status, `(auth)` public pages, ADR-FE-018 effective-dated display), [`HO-NTF-002`](HO-NTF-002.md) (inbox, where back-in-stock messages land), [`HO-APT-002`](HO-APT-002.md) (`/book?category=…`, the consultation link target) |
| Supplies | **HO-ORD-002** — the buy box's action slot and `PrescriptionCatPicker` (§11.3), which the cart wires to add-to-cart |
| Delivery order | Section 6 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | **2026-10-04 — build-time findings (frontend build against the merged HO-SHP-001, master `cffaa79`).** (1) **Mock location.** Handlers live under `mocks/handlers/shop/**` as §13 says; the pre-wired stub `mocks/handlers/areas/shop.ts` (outside §13) now only re-exports them, so `mocks/handlers/index.ts` is unchanged and sibling branches do not conflict on it. (2) **Navigation.** The three portal entries and the one staff entry are registered in `nav/areas/shop.ts` (the registry the shells read; outside §13), not in the two layout files, which are untouched. (3) **`/` redirect (OQ-1).** `app/page.tsx` sends a signed-out visitor to `/shop` instead of `/sign-in`; HO-ACC-002's owner-decided destinations for a signed-in customer (`/portal/cats`) and staff member (`/staff/cats`) are kept. HO-ACC-002's `tests/e2e/home.spec.ts` (outside §13) had one expectation (`/sign-in`) updated to `/shop`. (4) **`?next=` allow-list.** There is no `lib/routing/`; the allow-list is the inline array in `app/(auth)/sign-in/page.tsx` (outside §13), where `/shop` was added (`/portal/reviews/new` is already covered by `/portal/`). HO-APT-002 edits the same line; the integrator resolves that merge. (5) **Mock flag and server mocking.** The repo's flag is `NEXT_PUBLIC_USE_MOCKS`, not `NEXT_PUBLIC_API_MOCKING`; `instrumentation.ts` starts `msw/node` only in `next dev` with that flag. Next re-wraps `fetch` as routes compile in dev, after which MSW stopped intercepting (reproduced), so `instrumentation.ts` also publishes MSW's `fetch` as `globalThis.__mswFetch`, which `getPublic` uses when present (inert in production; ADR-FE-030). (6) **Contract notes — HO-SHP-001 §12.4 vs the merged API (§20.4/§20.5; reported, not shimmed; mocks mirror the REAL shapes).** (a) The staff detail read returns no food labelling, images, health focus or SKU label, and no per-SKU `stock` (deferred, backlog SHP row 51): the food, images and health-focus forms therefore start empty and say so, and saving replaces what is stored; `stock` is typed optional and absent reads *No stock recorded*. (b) The images write takes a `fileId` (GUID from File Storage), not a storage key plus size/content type: `ImagesEditor` asks for the stored file's id; still no upload. (c) `GET /customers/me/reviews` carries no product slug, so *Edit* links by `reviewId` and the form loads that review from the list. (d) Product kind is `Bundle` on the wire, shown as *Kit*. (e) `InvalidBundleComponent` arrives with `propertyName: "components"` and the SKU code in its message; the editor shows it on the row whose code the message names. (f) The CSV export has no barcode filter; the Export link omits it. (g) There is no portal cat edit page; the `AddDateOfBirth` hint links to the cat's profile. (h) There is no endpoint listing published policy pages, so the footer links FAQs and Terms only. (7) **Reuse outside scope, read-only.** `ScheduleChangeDialog` (HO-ADM-002) is reused for *Schedule a new price*; its description sentence ("Bookings and invoices made before …") is service-worded and is flagged for a shared-copy follow-up. (8) **MSW limitation.** The browser worker and `msw/node` hold separate in-memory databases, so staff changes made in the browser are not seen by server-rendered storefront pages; the "visible in the shop" halves of E-C1 and of E-F1's staff edit are not runnable under MSW and are recorded as real-API cases. (9) **Rendering notes.** The catalogue is not wrapped in `<Suspense>` (a streamed boundary needs JavaScript to reveal, breaking the no-JavaScript render); in `next dev`, Back pressed immediately after a hard reload returns to the reloaded page even for plain links, so E-A2 exercises Back from the reloaded page's next navigation. `text-ink-faint` fails AA contrast on small text and is not used. (10) **Test files.** `tests/e2e/shop-helpers.ts` is a non-spec helper beside the `shop-*.spec.ts` specs. **2026-10-04 — owner decision, closing the ARCHITECTURE BLOCKER (d096f3a):** the owner approved both §14 edits — HO-ACC-002's `web/tests/e2e/home.spec.ts` (the signed-out `/` expectation follows the §13-authorised `/` → `/shop` redirect) and HO-ACC-002's `web/app/(auth)/sign-in/page.tsx` (`/shop` on the `?next=` allow-list, as §8.1 intends). §13's non-existent `web/lib/routing/**` is corrected to the sign-in page, and both files are listed in §13 with this decision as their authority. Item (4)'s expected merge conflict with HO-APT-002 does not exist (HO-APT-002 does not edit the sign-in page). |

---

## 2. Objective

Build **the storefront** and **the catalogue back office**.

- **Customers and guests** browse by category and by country of origin. They filter and sort, search
  with typos, and open a product page that shows variants, price, real availability, the expiry of
  the stock they would receive, origin, ingredients and analysis, reviews, and related products.
  Signed-in customers keep a wishlist, ask to be told when something is back in stock, write reviews
  of what they bought, and get food suggestions for each of their cats.
- **Shop managers** create and publish products with variants, prices, images, food labelling,
  bundles, related products and translations. They curate the countries the shop sources from, and
  moderate reviews.

**There is no cart, checkout or payment here.** Those are HO-ORD-002. No payment gateway exists in
v1. The product page's buy box shows everything that decides whether a variant can be bought, and
leaves an action slot that HO-ORD-002 fills with *Add to cart* (§11.3).

Three things make this harder than a product grid:

1. **It is public and must be fast.** Guests arrive from search engines on 4G phones.
   NFR-PRF-1 asks for LCP ≤ 2.5 s. The catalogue and product pages are **server-rendered** from the
   public API, with real links, metadata and canonical URLs (ADR-FE-030).
2. **The server decides.** Filtering, availability, the expiry notice, prescription eligibility and
   the publish checklist all come from the API. The UI never computes stock, prices or eligibility
   (ADR-FE-032).
3. **Prescription diets must explain themselves.** A therapeutic food is visible to everyone and
   purchasable only for an authorised cat. The page says why a customer can't buy it and offers the
   way forward — book a consultation — without leaking anything clinical (ADR-FE-033).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack and layering; HO-ACC-002's session model, `client.ts`, route groups, error
mapping, `?next=` guard, `UnsavedChangesGuard` and `PermissionDeniedState`; HO-ADM-002's
`lib/format`, `LimitedMarkup` component, public-content module and effective-dated display
(ADR-FE-018); HO-NTF-002's inbox.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-SHP-04, 06 | **Catalogue**: search box in the header; results with filters (origin, brand, category, life stage, protein, price range, availability, rating), sort, pagination; no-result suggestions | `/shop/products` (all state in URL params) |
| US-SHP-05 | **Shop by Country**: origin grid; origin page with sourcing notes and the filtered catalogue | `/shop/countries`, `/shop/countries/[countryCode]` |
| US-SHP-01, 02, 03, 07, 12, 14 | **Product page**: gallery, variant selector, price, availability and expiry notice, food tabs (ingredients and analysis, feeding guide, storage, origin), bundle contents, language of content | `/shop/products/[slug]` (`?sku=`, `?lang=`) |
| US-SHP-10 | Prescription gate on the product page: reason, eligible cats, *Book a consultation* | `/shop/products/[slug]` |
| US-SHP-08 | "Frequently bought together", "You might also like", "Recommended for {life stage}" blocks | `/shop/products/[slug]` |
| US-SHP-09 | Reviews on the product page; write/edit a review; my reviews | `/shop/products/[slug]`, `/portal/reviews/new?product={slug}`, `/portal/reviews` |
| US-SHP-13 | Wishlist and stock-alert buttons on the product page; my wishlist and alerts | `/shop/products/[slug]`, `/portal/wishlist` |
| US-SHP-11 | Food suggestions for one cat, with reasons and allergy exclusions | `/portal/cats/[catId]/food` (link from HO-CAT-002's portal cat page) |
| Storefront home | Banners, categories, featured origins, new arrivals | `/shop` |
| US-SHP-01, 02, 03, 12, 14 (staff) | **Product list** (filter, sort, paginate, CSV) and **product editor**: details · food & labelling · variants & prices · images · bundle · related · translations; publish checklist and status actions | `/staff/shop/products/**` |
| US-SHP-05 (staff) | Sourcing origins list and editor | `/staff/shop/origins` |
| US-SHP-09 (staff) | Review moderation queue | `/staff/shop/reviews` |
| US-ADM-07, 08 (storefront half) | Shop banners (placement `Shop` / `Home`); maintenance notice replacing storefront pages | `(shop)` layout |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-SHP-001 owns `src/**` |
| **Cart, add-to-cart, checkout, payment, order history** | HO-ORD-002. The buy box exposes an action slot and `PrescriptionCatPicker` for it (§11.3). **No payment step exists anywhere in v1** |
| Issuing diet authorisations | HO-PHM-002 |
| Stock, batches, receiving, expiry alerts for staff | HO-IMP-002. The variants screen shows the stock copy read-only |
| File upload of product images | No upload path exists (HO-SHP-001 ADR-091). The images editor takes a storage key + metadata, like HO-CAT-002 §11.6 |
| Translating the UI chrome | FR-SHP-15 covers **product content** only. Labels stay English (C-8) |
| Search-as-you-type suggestions | Not in any AC (OQ-4) |
| A carousel, image-zoom, slider, virtualised-list, search or i18n library | §10 |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged: no global store, no token in JS, no `fetch`
outside `client.ts`, no hooks inside components, no `dangerouslySetInnerHTML`, no client-side
authorization decision. Added here: **no client-side filtering, sorting or availability logic over
catalogue data**, and **no `next/image` remote configuration** (`next.config.ts` is forbidden scope —
ADR-FE-035).

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002, HO-ADM-002 built** (`client.ts` session and CSRF, route groups, `?next=` guard, `LimitedMarkup`, `lib/format`, `public-content.ts`) | ❌ at baseline — §20.1 |
| HO-NTF-002 built (inbox) | ❌ at baseline — needed only for the real-API run of E-D2 |
| HO-APT-002 built (`/book`) | ❌ at baseline — the consultation link is a plain link and is built regardless; E-B2's real-API run needs the route |
| HO-SHP-001 API | ❌ — build against MSW generated from §12.4 there, then re-run every E-case against the real API per phase |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-SHP-001`](HO-SHP-001.md) | **§12.4 endpoints and shapes**; §10.4 codes and their messages; §8.4 the publication checklist; §8.7 availability and the expiry notice; §8.8 filter and sort semantics; §8.9 the prescription gate's reasons; §8.11 review states and alert deliverability; §8.13 recommendation reasons; §8.14 `fieldLocales`; §12.3 who may do what |
| [`HO-ADM-002`](HO-ADM-002.md) | §8.4 `formatMoney`, `formatInZone`; §8.5 `LimitedMarkup` renderer; ADR-FE-018 Current/Scheduled/Past |
| [`HO-ACC-002`](HO-ACC-002.md) | §8.3 route groups and ADR-FE-001; §8.4 session model; §8.6 accessibility additions; §11.2 client; §11.14 error mapping |
| [`HO-CAT-002`](HO-CAT-002.md) | §8 layering, state and accessibility; §9 conventions; §10 dependencies; §11.6 the storage-key image form |
| [`HO-APT-002`](HO-APT-002.md) | §8.1 `/book` and its deep-link parameters |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.1, §6.4, §9 AC-5 | UI-1/3/4/6; NFR-PRF-1; NFR-USA-4/5/6; the AC-5 pass condition |

The `.ai/` documents describe the .NET backend only and do not govern this diff (HO-CAT-002 §6).

---

## 7. Build rules

| Req | Rule | Where |
|---|---|---|
| **UI-1 / NFR-CMP-2** | 360 px floor with no horizontal scroll. Below 768 px the filter panel is a sheet opened by a **Filters (n)** button; product grids go 2 columns at 360, 3 at 768, 4 at 1024+; the editor's tabs become a select | §11.1, §11.6 |
| **UI-3** | Confirm, stating what is affected: publish (*"Publish Royal Canin Kitten? It will appear in the shop."*), unpublish, archive, discontinue a SKU, schedule a price (*"From 1 Oct 10:00 RC-K-2KG will cost ৳2,450.00."*), reject or hide a review, remove a translation | Each screen |
| **UI-4** | Staff lists filter, sort, paginate and export CSV (from the server) | §11.6 |
| **UI-6** | WCAG 2.1 AA. Filters are real `<fieldset>`/`<legend>` groups; variant choice is a radio group; ratings are text (*"4.5 out of 5, 23 reviews"*) with stars `aria-hidden`; availability and the prescription badge are text plus icon, never colour alone; the result count after a filter change is announced in a polite live region; every image has alt text from the API; the gallery is thumbnail buttons, not an auto-rotating carousel | §11 |
| **NFR-PRF-1** | Catalogue and product pages are Server Components; the LCP image is a plain `<img>` with `width`/`height`, `fetchpriority="high"` and no lazy-loading; below-the-fold images are `loading="lazy"`; client JavaScript only for interactive islands (ADR-FE-030) | §8.2, E-X3 |
| **NFR-USA-4** | Server messages in plain language; `errorCode` never rendered | HO-ACC-002 §11.14 |
| **NFR-USA-5** | Every editor section and the review form keep their values across validation errors; `UnsavedChangesGuard` on every editor section and the review form | §11.5, §11.6 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  page.tsx                                      ⚠ redirect('/shop') instead of '/staff/cats' (OQ-1)
  (shop)/                                       ⭐ NEW public storefront group (ADR-FE-029)
    layout.tsx                                  storefront shell: header (logo, SearchBox, category nav,
                                                account/sign-in link, wishlist link), MaintenanceGate,
                                                BannerStrip, LanguageSwitcher, footer (policy links)
    shop/page.tsx                               home: banners (Home), categories, featured origins, new arrivals
    shop/products/page.tsx                      catalogue: search, filters, sort, pages          US-SHP-04, 06
    shop/products/[slug]/page.tsx               product page                                      US-SHP-01…03, 07…10, 12…14
    shop/countries/page.tsx                     Shop by Country                                   US-SHP-05
    shop/countries/[countryCode]/page.tsx       origin page + filtered catalogue                  US-SHP-05
  (portal)/portal/
    wishlist/page.tsx                           wishlist + stock alerts                           US-SHP-13
    reviews/page.tsx                            my reviews                                        US-SHP-09
    reviews/new/page.tsx                        write / edit (?product={slug}&reviewId=)          US-SHP-09
    cats/[catId]/food/page.tsx                  food suggestions                                  US-SHP-11
    cats/[catId]/page.tsx                       ⚠ gains a "Food suggestions" link (HO-CAT-002's page)
  (portal)/layout.tsx                           ⚠ nav entries: Shop, Wishlist, My reviews
  (staff)/staff/shop/
    layout.tsx                                  shop section nav
    products/page.tsx                           list                                              UI-4
    products/new/page.tsx                       create (kind, name, brand, category, tax class, slug)
    products/[productId]/layout.tsx             ⭐ ProductEditorHeader (status, PublishChecklist, actions) + tabs
    products/[productId]/page.tsx               details                                           US-SHP-01
    products/[productId]/food/page.tsx          food & labelling                                  US-SHP-03
    products/[productId]/variants/page.tsx      SKUs, prices, stock copy                          US-SHP-02
    products/[productId]/images/page.tsx        images (storage key + metadata)                   US-SHP-01, 02
    products/[productId]/bundle/page.tsx        components (bundle kind only)                     US-SHP-12
    products/[productId]/related/page.tsx       curated links                                     US-SHP-08
    products/[productId]/translations/page.tsx  per enabled locale (hidden with one locale)       US-SHP-14
    origins/page.tsx                            sourcing origins                                  US-SHP-05
    reviews/page.tsx                            moderation queue                                  US-SHP-09
  (staff)/layout.tsx                            ⚠ one nav entry: Shop
```

`(shop)` is **public**. It reads `['session','customer']` to show "Sign in" or the customer's name,
but it has **no guard**. Personal actions (wishlist, alert, review, eligible cats) call customer
endpoints. On `401` they send the visitor to `/sign-in?next=` with the current product URL.
HO-ACC-002's open-redirect allow-list gains `/shop` and `/portal/reviews/new` (§13).

### 8.2 Rendering and data

| Surface | How |
|---|---|
| Catalogue, product, countries, home | **Server Components.** Data from public endpoints through `client.ts`'s new **`getPublic(path, { revalidate: 60 })`**, which runs only on the server and targets `API_ORIGIN` (ADR-FE-030). Props flow into presentational components. `generateMetadata` sets the title, description (plain-text excerpt) and `alternates.canonical` = `/shop/products/{slug}` |
| Filters, sort, search box, variant selector, gallery | Client islands. They change **URL params** (`router.replace` for filters, `push` for pages); the server re-renders |
| Personal parts (purchasability, wishlist state, alert state, review eligibility, recommendations with a session) | Client islands with TanStack Query hooks in `lib/hooks/`; skeletons until loaded; never block the server-rendered page |
| Portal and staff pages | As HO-CAT-002 / HO-ADM-002: client data through hooks |

In MSW mode, server-side fetches are intercepted by `msw/node` started from `web/instrumentation.ts`
when `NEXT_PUBLIC_API_MOCKING=enabled`; browser fetches by the existing worker. Both use the **same**
handlers (§12).

### 8.3 API modules

`lib/api/shop-catalogue.ts` (products, filters, categories, origins, product detail, reviews,
recommendations, locales — all public), `lib/api/shop-customer.ts` (purchasability, review
eligibility, my reviews, wishlist, stock alerts, food recommendations), `lib/api/staff-shop.ts`
(products, export URL, SKUs, prices, barcode lookup, countries, origins, reviews moderation).
Tax classes come from HO-ADM-002's `admin-catalogue.ts`; banners and system status from its
`public-content.ts` / `system.ts`.

### 8.4 State

| Kind | Where |
|---|---|
| Search, filters, sort, page | **URL search params** on `/shop/products` and origin pages (ADR-FE-031). The single parser `lib/shop/catalogueParams.ts` reads and writes them; unknown params are dropped |
| Selected variant | `?sku=` on the product page; defaults to the first in-stock SKU, else the first |
| Content language | `?lang=` on shop routes, passed as `locale`; absent = default (ADR-FE-037) |
| Personal product state | `['shop','purchasability', slug]`, `['shop','review-eligibility', slug]`, `['me','wishlist']`, `['me','stock-alerts']`, `['me','reviews']`, `['me','cat', catId,'food']` |
| Staff product | `['staff','product', productId]` (with `version`); each section mutation sends `expectedVersion` and invalidates the key |
| Staff lists | URL params + `['staff','products', params]`, `['staff','reviews', params]`, `['staff','origins']` |
| Forms | react-hook-form + zod |

**Sign-in or sign-out invalidates every `['shop', …]` and `['me', …]` key** (HO-ACC-002 §8.4 clears
the whole cache on sign-out already).

### 8.5 Formatting — `lib/format` additions

- `formatPriceRange(fromPrice, priceVaries)` → `৳1,200.00` or `From ৳1,200.00`, using HO-ADM-002's
  `formatMoney`. **Amounts stay strings**; `parseMoneyForCompare` exists only for the min ≤ max check
  on price-filter inputs, never for display arithmetic.
- `taxLabel(pricesIncludeTax)` → `incl. VAT` / `+ VAT` (wording OQ-5).
- `formatRating(average, count)` → `4.5 out of 5 (23 reviews)`; `No reviews yet` when count is 0.
- `formatBestBefore(date)` → `12 Nov 2026` (date-only, no zone shift — HO-CAT-002 §9).
- `countryFlag(code)` → the in-repo SVG path for the launch origins (`public/flags/{cc}.svg`), else
  `null` and the UI shows a globe icon. **The country name is always shown as text**, and the flag is
  `alt=""` (decorative).

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing HO-APT-002's series. **This handover owns
ADR-FE-029…038.**

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-029** | A fourth route group, **`(shop)`**: public, with its own storefront shell, no guard. `/` redirects to `/shop` (OQ-1) | `(auth)` is a minimal shell without navigation, `(portal)` requires a session, and guests must browse (SRS §2.3) |
| **ADR-FE-030** | Storefront pages are **Server Components** fed by public endpoints through `client.ts`'s server-only `getPublic` with `revalidate: 60`; metadata and canonical URLs from the slug; interactive islands only where needed. `client.ts` remains the only `fetch` call site | NFR-PRF-1 LCP on 4G, SEO slug (FR-SHP-2), and HO-ACC-002's single-fetch rule all hold at once |
| **ADR-FE-031** | **Catalogue state is URL state** — search, every filter, sort and page — through one parser; changing a filter resets the page; sorting never drops a filter; the server is the only filter engine | US-SHP-04 AC2 "filters persist", shareable and reload-safe; NFR-PRF-2 is a server budget |
| **ADR-FE-032** | Availability, the expiry notice, purchasability, eligible cats and the publish checklist are **rendered only from API fields**. "No stock recorded" (a `null` copy) is shown as such, never as 0 | The server decides (HO-SHP-001 ADR-082, ADR-084); HO-ACC-002 ADR-FE-012 "a number is a claim" |
| **ADR-FE-033** | **The prescription gate** shows the product to everyone, states the reason from `purchaseBlockReason`, offers *Book a consultation* → `/book?category=Consultation` (HO-APT-002), lists eligible cats with their authorisation end dates, and exposes `PrescriptionCatPicker` for HO-ORD-002. No clinical detail is shown | US-SHP-10 AC1; BR-2; the owner sees their own cats, never a diagnosis |
| **ADR-FE-034** | **The product editor is route-based sections** under one layout that carries the status, the server's publish checklist and the status actions; each section saves on its own with `expectedVersion`; Publish is never disabled to express invalidity — the server's refusal is shown against the checklist | Mirrors HO-SHP-001 ADR-095; HO-ACC-002 §8.6 "submit is never disabled to express invalidity"; `409` handled |
| **ADR-FE-035** | **Images**: no upload; the storage-key + metadata form of HO-CAT-002 §11.6 with an honest note; rendered with plain `<img>` (no `next/image` remote config, because `next.config.ts` is forbidden scope); `url: null` → an in-repo placeholder SVG; alt text required in the editor | HO-SHP-001 ADR-091; NFR-SEC-5 upload is not provisioned; UI-6 |
| **ADR-FE-036** | **Money and tax display** through `lib/format` only: "From" when variants differ, the tax label from `pricesIncludeTax`, the currency from the API; no arithmetic on amounts | C-6; SRS Q-5 open |
| **ADR-FE-037** | **Product-content language** is a `?lang=` parameter passed as `locale`; the switcher appears only when `GET /api/shop/locales` lists more than one; each fallback field carries `lang` from `fieldLocales`; UI labels stay English | FR-SHP-15 / US-SHP-14; keeps pages cacheable per language; WCAG 3.1.2 language of parts |
| **ADR-FE-038** | **Personal storefront actions** (wishlist, stock alert, review) require sign-in through `?next=` back to the same product and variant; the stock-alert confirmation shows the server's `deliverability`, and when `willNotify` is false it says so and links to notification preferences | HO-SHP-001 F33 / OQ-2: an alert that will reach nobody must not look like it will |

| # | Question | Default |
|---|---|---|
| OQ-1 | Should `/` open the shop? (HO-ACC-002 §8.3 calls the landing route a product decision) | Yes — `/` → `/shop`; staff use `/staff/sign-in` |
| OQ-2 | Flags for origins | In-repo SVGs for FR-SHP-6's examples (DE, TH, TR, US, KR); a globe icon otherwise, name always shown. More are added when SRS Q-8 names the launch origins |
| OQ-3 | Default page size | 24 (HO-SHP-001's default) |
| OQ-4 | Search-as-you-type suggestions? | No — submit to the results page |
| OQ-5 | Tax label wording | `incl. VAT` / `+ VAT`, pending SRS Q-5 |
| OQ-6 | Copy of the prescription-gate message | *"This is a prescription diet. It can be bought only for a cat with a current authorisation from our vets."* + the reason line + *Book a consultation* |
| OQ-7 | Copy when a stock alert will not be delivered (backend OQ-2) | *"You haven't agreed to receive offers and updates, so we can't tell you when this is back. Turn on in-app, SMS or email updates in your notification settings."* |

---

## 10. Dependencies

**No new package.** Everything is on HO-CAT-002 §10's list, plus `msw/node` (part of the existing
`msw` package) for server-side mocking. A carousel, lightbox, image-zoom, slider, virtualised-list,
search, i18n, markdown or country-flag package is a §20 stop.

---

## 11. Specified changes — components and screens

Each component stays under ~150 lines, with distinct loading, empty and error states.

### 11.1 Storefront shell — `(shop)/layout.tsx`

| Component | Behaviour |
|---|---|
| ⭐ `MaintenanceGate` | Server-side `GET /api/system/status`. When `maintenanceMode` → renders `MaintenanceNotice` (the admin's message as text, *"The shop is temporarily closed. Clinic bookings and your account are still available."* with links to `/portal` and `/book`) **instead of** the page. A storefront call answering `503` renders the same notice (US-ADM-08) |
| `ShopHeader` | Logo → `/shop`; `SearchBox`; category nav from `GET /api/shop/categories` (a disclosure menu on mobile); **Sign in** or the customer's first name → `/portal`; wishlist link when signed in |
| `SearchBox` | `role="search"`, labelled *"Search products"*; submits `q` to `/shop/products` and keeps `lang`; no suggestions (OQ-4) |
| `BannerStrip` | `GET /api/content/banners?placement=Shop` (and `Home` on `/shop`); each banner's title and body through HO-ADM-002's `LimitedMarkup`; link only when present; no auto-rotation |
| `LanguageSwitcher` | Only when `locales.length > 1`; a labelled `<select>` that rewrites `?lang=` |
| `ShopFooter` | Links to published policy pages (HO-ADM-002 `/pages/[slug]`) |

### 11.2 Catalogue — US-SHP-04, 06 · `/shop/products`

| Component | Behaviour |
|---|---|
| ⭐ `CatalogueFilters` | From `GET /api/shop/filters?category`: **Country of origin** (checkboxes with flag and name and count), **Brand** (checkboxes; a text box narrows the *option list* only), **Category / subcategory** (radio tree), **Life stage**, **Protein source**, **Price** (min and max inputs labelled with the currency; applied by a button; min ≤ max checked before navigating), **Availability** (*In stock only*), **Rating** (*4 and up*, *3 and up*…). Each group is a `<fieldset>`. Desktop applies on change; mobile uses a sheet with **Show results**. Active filters appear as removable chips above the results; **Clear all** keeps `q` |
| `SortSelect` | Relevance (only with `q`), Newest, Price low–high, Price high–low, Popularity, Rating. Changing it **keeps every filter** — **US-SHP-04 AC2** |
| `ResultSummary` | *"42 products for "salmon""*, announced politely after each change |
| ⭐ `ProductCard` | `<article>` with the name as a heading link; image (placeholder when `url` is null); brand; origin (flag + name); price (`formatPriceRange`); rating text; availability text (*In stock* / *Out of stock*); prescription badge *"Vet authorisation needed"* (text + icon); bundle badge *"Kit"* |
| `ProductGrid`, `Pagination` | Pagination is real links (`?pageNumber=`), with `aria-current` on the current page |
| ⭐ `NoResults` | For `totalCount 0` with `q`: *"No products match "royl canan"."*; **Did you mean** links from `suggestions.didYouMean` (each rewrites `q` and keeps filters); **Popular right now** grid from `suggestions.popular` — **US-SHP-06 AC3**. Without `q` (filters only): *"Nothing matches these filters"* + **Clear filters** |

### 11.3 Product page — `/shop/products/[slug]`

| Component | Behaviour |
|---|---|
| `ProductGallery` | Primary image first (the LCP element), thumbnails as buttons with the image's alt text; the selected variant's own images first when it has any (US-SHP-02 AC1) |
| `ProductHeading` | Name (`<h1>`, `lang` from `fieldLocales.name`), brand, rating text linking to reviews, origin flag + name linking to the origin page |
| ⭐ `VariantSelector` | Radio group of `skus[]` by `label`; each option shows its price and *Out of stock* in text when so; selecting updates `?sku=` without scrolling |
| ⭐ `BuyBox` | For the selected SKU: price with the tax label; **availability** (*In stock* / *Out of stock*); **expiry notice** when `expiryNotice` is present — *"Best before 12 Nov 2026 — this is the date on the stock we would send you."* (**US-SHP-07 AC2**); `PrescriptionGate` when `prescriptionRequired`; `WishlistButton`; `StockAlertButton` when out of stock; and an **`actions` slot** (empty in this slice) that HO-ORD-002 fills with *Add to cart*. An out-of-stock variant never hides the others — **US-SHP-02 AC2** |
| ⭐ `PrescriptionGate` | Guest: the message (OQ-6) + *Sign in to check whether your cat is authorised* (→ `?next=`). Signed-in, `PrescriptionAuthorisationRequired`: the message + *None of your cats has a current authorisation for this food.* + **Book a consultation** → `/book?category=Consultation` — **US-SHP-10 AC1**. Eligible: *"Authorised for Mitu until 12 Dec 2026"* per cat |
| ⭐ `PrescriptionCatPicker` | Radio group of `eligibleCats` (*"Which cat is this for?"*), required, keyboard-operable, with `onChange(catId)` and no side effect — **delivered for HO-ORD-002** to record the cat on the order line (US-SHP-10 AC2) |
| `WishlistButton` | Toggle *Save* / *Saved* (`aria-pressed`) for the selected SKU; a guest is sent to sign-in with `?next=` (ADR-FE-038) |
| ⭐ `StockAlertButton` | *Tell me when it's back* → `POST` → confirmation. When `deliverability.willNotify` is false it shows OQ-7's text with a link to `/portal/account/notifications`; otherwise *"We'll send one message by {channels}."* Already requested → *You'll be told when it's back* + **Cancel alert** — **US-SHP-13** |
| `ProductDetailsTabs` | **Description** (`LimitedMarkup`); for food **Ingredients & analysis** (ingredient list as text; guaranteed analysis as a `<table>` with `%` in every cell — protein, fat, fibre, moisture); **Feeding guide** (`LimitedMarkup`); **Storage**; **Origin** (country, manufacturer, sourcing notes from the origin, link to the origin page) — **US-SHP-03 AC1**. Each field carries `lang` from `fieldLocales` when it differs from the page language (US-SHP-14 AC2) |
| `BundleContents` | *"This kit contains"*: each component's name (link), variant label, quantity and availability text — **US-SHP-12** |
| `RecommendationBlocks` | *Frequently bought together*, *You might also like* (up-sell), *Recommended for {kittens / adult cats / senior cats}* (the heading says *for your cats* when `basis` is `YourCats`); each is a small `ProductGrid`; an empty block is not rendered — **US-SHP-08**. Fetched client-side so that exclusions reflect the session |
| ⭐ `ReviewsSection` | Rating summary; paged published reviews (rating text, title, body **as text**, *"Rahim K. · verified buyer · 2 kg · 3 Oct 2026"*); **Write a review** only when `review-eligibility.canReview`; with `AlreadyReviewed` → **Edit your review**; with `NotPurchased` → *"Only customers who bought this product can review it."* |

### 11.4 Shop by Country — US-SHP-05

| Component | Behaviour |
|---|---|
| `OriginGrid` | `GET /api/shop/origins`: card per origin — flag, country name (`<h2>`), sourcing-notes excerpt (plain text, first 160 chars), *"{n} products"* — **US-SHP-05 AC1**. Featured first as the API orders them |
| `OriginPage` | Flag, name, sourcing notes (`LimitedMarkup`), then the catalogue components with `origin={code}` fixed and **every other filter available** — **US-SHP-05 AC2** |

### 11.5 Portal — US-SHP-09, 11, 13

| Component | Behaviour |
|---|---|
| `WishlistList` | Items with image, name, variant, price, availability text, *No longer sold* when `onSale` is false; **Remove**; link to the product with `?sku=` |
| `StockAlertList` | Alerts with status text (*Waiting*, *Notified on 3 Oct*, *Closed — no longer sold*); **Cancel** for waiting ones |
| ⭐ `ReviewForm` | Variant (from `purchasedSkus`), rating (radio group 1–5 with text labels), title, body (plain text, 2000, character counter announced at 100 remaining); *"Reviews are checked before they appear."*; `400 ReviewNotPermitted` / `AlreadyReviewed` shown in plain language; after submit → `/portal/reviews` with *"Thanks — your review will appear once it's approved."* — **US-SHP-09 AC3** |
| `MyReviewsList` | Each review with status text (*Awaiting approval*, *Published*, *Not published*, *Hidden*), **Edit**, **Withdraw** (confirm) |
| ⭐ `FoodSuggestions` | Cat heading (name, life stage, *"about"* when estimated); items as cards each with its **reasons** as text (*"Suits adult cats"*, *"For neutered cats"*, *"Weight-management recipe — Mitu weighs 6.8 kg"*, *"May suit Mitu's recorded urinary condition"*) — **US-SHP-11 AC1**; `excludedForAllergy` as *"We left out 3 products containing chicken (recorded allergy)."* — **US-SHP-11 AC2**; the `AddDateOfBirth` hint links to HO-CAT-002's edit page; a footnote *"General guidance — ask our vets for advice for your cat."* |

### 11.6 Back office — `/staff/shop/**`

| Component | Behaviour |
|---|---|
| `StaffProductTable` | Filters (search incl. **barcode** — a scanner's keyboard-wedge input plus Enter works (HW-2), status, category, brand, kind, listed), sort, pages, all in the URL; **Export CSV** links to the server export with the same filters — UI-4 |
| `NewProductForm` | Kind (Standard / Kit), name, brand, category → subcategory (only the category's), tax class (HO-ADM-001 list), slug (suggested from the name, editable); bundle kind also asks the kit's SKU code |
| ⭐ `ProductEditorHeader` | Name, status text, **PublishChecklist** (each item ✓/✗ with its message, blocking vs warning labelled in text), and actions from the current status: **Publish**, **Unpublish**, **Archive** (confirm each). A `400 ProductNotPublishable` shows the server message and focuses the checklist. A `409` says *"This product was changed by someone else. Reload to see the latest."* with **Reload** |
| `ProductDetailsForm` | Name, brand, description (`LimitedMarkup` textarea + live preview through the shared component), subcategory, tags (chip input, normalised preview), country of origin (select from `/api/staff/shop/countries`), tax class, **Show in online shop** (listed), **Track expiry** (non-food only; shown locked *On* for food), slug (read-only with a note after first publication), category (read-only while active) |
| ⭐ `FoodDetailsForm` | Only for Cat Food. Manufacturer, life stage, protein source, ingredient list, guaranteed analysis (four inputs, each with a visible `%` unit), feeding guide (`LimitedMarkup` + preview), storage instructions, health focus (checkboxes), **Prescription diet — requires vet authorisation** (checkbox with a confirm stating that customers will need an authorised cat) — **US-SHP-03** |
| ⭐ `VariantsTable` | SKUs: label, code, barcode, status, **current price**, **scheduled price**, **stock** (*"12 sellable · best before 12 Nov 2026 · as of 10:42"* or *"No stock recorded"* when `null`) — US-SHP-02 AC1; **Add SKU** dialog (code, barcode, size/weight, flavour, colour, pack quantity); **Discontinue / Reactivate** (confirm); **Prices** opens `PriceHistoryPanel`: Current / Scheduled / Past (HO-ADM-002 ADR-FE-018) and **Schedule a new price** (amount + effective from, which cannot be in the past; *Now* sends the current instant) |
| `ImagesEditor` | Ordered list; each image: storage key, content type, size, file name, **alt text (required)**, product-level or a SKU, primary radio; *"Image upload isn't available yet — enter the stored file's key."* (ADR-FE-035); save replaces the set |
| `BundleEditor` | Kit only. Component rows: SKU picker (staff product search, then the product's SKUs), quantity 1–20; the server's `InvalidBundleComponent` shown on the row it names — **US-SHP-12** |
| `RelatedProductsEditor` | Two ordered lists (*Frequently bought together*, *Up-sell*), ≤ 12 each; product picker; reorder with buttons (keyboard) — **US-SHP-08** |
| `TranslationsEditor` | Hidden when one locale. One sub-form per non-default enabled locale: name, description (with preview), ingredient list, feeding guide; the default text shown alongside read-only for reference; **Remove translation** (confirm) — **US-SHP-14 AC3** |
| `OriginsTable` + `OriginForm` | Country (select), sourcing notes (`LimitedMarkup` + preview), featured, sort order; the product count per origin, read-only |
| ⭐ `ReviewModerationQueue` | Pending first: product, variant, rating text, title and body **as text**, customer display name, submitted; **Publish**, **Reject** (reason select required + note), **Hide** (published ones); confirm each |

### 11.7 Error mapping — `lib/api/errors.ts`

Append entries for every HO-SHP-001 command's `propertyName`s, and map HO-SHP-001 §10.4 codes to the
messages used in §11 (`ProductNotPublishable`, `SlugAlreadyInUse`, `SlugLockedAfterPublication`,
`SkuCodeAlreadyInUse`, `BarcodeAlreadyInUse`, `InvalidBarcode`, `DuplicateVariant`,
`VariantAttributesRequired`, `ImageAltTextRequired`, `CountryOfOriginRequired`,
`InvalidGuaranteedAnalysis`, `PriceCannotBeBackdated`, `PriceMustBePositive`,
`InvalidBundleComponent`, `ReviewNotPermitted`, `AlreadyReviewed`, `SkuAlreadyAvailable`,
`StockAlertAlreadyRequested`, `PurchaseNotPermitted`, `LocaleNotEnabled`, `InvalidCatalogueFilter`).
The server's message is preferred when present; the mapped text is the fallback.

---

## 12. Mocks — `web/mocks/handlers/shop/*.ts`

Generated from HO-SHP-001 §12.4, and shared by the browser worker and `msw/node` (§8.2). Fixtures:
the six categories with subcategories; ~60 products across them, including food from DE, TH, TR, US
and KR with sourcing notes on three origins; a product with three variants where one is out of stock
and one has an `expiryNotice`; a product with `url: null` images; a prescription diet in three
purchasability states (guest, no authorisation, two eligible cats); a kit with one out-of-stock
component; curated related links and a life-stage block for both `basis` values; reviews (published,
and the caller's pending and rejected); review eligibility in each state; wishlist and alerts in each
status; alert responses with `willNotify` true and false; food suggestions with each reason code, an
allergy exclusion and the `AddDateOfBirth` hint; locales `["en"]` and `["en","bn"]` with a `bn`
product whose feeding guide falls back to `en`; a zero-result search with suggestions; staff detail
with an unmet checklist, `ProductNotPublishable`, `409`, and `stock: null`; price history with
current, scheduled and past; the moderation queue; maintenance `503`; every `403`.

---

## 13. Allowed scope

```
web/app/page.tsx                                              ← the redirect target only (OQ-1)
web/app/(shop)/**
web/app/(portal)/portal/wishlist/**
web/app/(portal)/portal/reviews/**
web/app/(portal)/portal/cats/[catId]/food/**
web/app/(portal)/portal/cats/[catId]/page.tsx                 ← Food suggestions link only (HO-CAT-002's page)
web/app/(portal)/layout.tsx                                   ← three nav entries only
web/app/(staff)/staff/shop/**
web/app/(staff)/layout.tsx                                    ← one nav entry only
web/components/{shop,catalogue,product,prescription,reviews,wishlist,recommendations,staff-shop}/**
web/lib/api/{shop-catalogue,shop-customer,staff-shop}.ts
web/lib/api/client.ts                                         ← server-only getPublic only (ADR-FE-030)
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/app/(auth)/sign-in/page.tsx                               ← `/shop` and `/portal/reviews/new` on the next-param allow-list only (was `web/lib/routing/**`, which does not exist — corrected 2026-10-04; HO-ACC-002's file, owner-approved, see §1 Amended)
web/tests/e2e/home.spec.ts                                    ← the signed-out `/` expectation `/sign-in` → `/shop` only (HO-ACC-002's spec — owner-approved 2026-10-04, see §1 Amended)
web/lib/format/**                                             ← §8.5 additions
web/lib/shop/**                                               ← catalogueParams and helpers
web/lib/hooks/**                                              ← new hooks only
web/lib/validation/**
web/public/flags/{de,th,tr,us,kr}.svg · web/public/images/product-placeholder.svg
web/instrumentation.ts · web/mocks/server.ts                  ← msw/node for server rendering in mock mode only
web/mocks/handlers/shop/** · web/mocks/handlers/index.ts
web/tests/components/{shop,catalogue,product,prescription,reviews,wishlist,recommendations,staff-shop}/**
web/tests/e2e/shop-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-029…038
docs/test-cases/TC-HO-SHP-002.md · docs/gates/HO-SHP-002-*.md
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**` | HO-SHP-001 |
| `.ai/**`, `.claude/**`, `docs/**` except the two named paths | Governing docs, agents, inputs |
| Every other HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002 and HO-APT-002 file beyond the lines in §13 | Their areas. In particular `client.ts` gains **one** server-only function and nothing else |
| `web/package.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs` | No dependency or configuration change (ADR-FE-035) |
| Any cart, checkout, order or payment component | HO-ORD-002 |

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| **A — catalogue** | | |
| U-A1 | Tick an origin, then a brand, then change the sort | URL carries both filters and the sort; the page resets to 1 on each filter; the sort keeps both filters — **US-SHP-04 AC2** |
| U-A2 | Mobile filter sheet: open, change two filters, **Show results** | URL updated once; the sheet closes; focus returns to **Filters (2)**; the result count is announced |
| U-A3 | `ProductCard` for a varying-price, out-of-stock, prescription, rated product with `url: null` | "From ৳…", *Out of stock*, *Vet authorisation needed*, *"4.5 out of 5 (23 reviews)"*, the placeholder image with alt text |
| U-A4 | `NoResults` with suggestions | *Did you mean* links rewrite `q` and keep filters; the popular grid renders — **US-SHP-06 AC3** |
| U-A5 | Price filter min 500, max 200 | Inline error; no navigation |
| U-A6 | `catalogueParams` round trip with unknown params and repeated values | Unknown params dropped; repeated brand values kept in order |
| **B — product page** | | |
| U-B1 | Select the second variant | `?sku=` updated; price, availability and variant images follow |
| U-B2 | Out-of-stock variant selected; other variants in stock | *Out of stock* + **Tell me when it's back** for it; the others still show *In stock* in the selector — **US-SHP-02 AC2** |
| U-B3 | SKU with `expiryNotice` | *"Best before 12 Nov 2026 …"* shown; absent without it — **US-SHP-07 AC2** |
| U-B4 | Food product tabs | Ingredients as text; the analysis table with `%` in every cell; feeding guide through `LimitedMarkup`; storage; origin with manufacturer — **US-SHP-03 AC1** |
| U-B5 | Kit | Components with quantities and availability text |
| U-B6 | `PrescriptionGate` for a guest, `PrescriptionAuthorisationRequired`, and two eligible cats | Sign-in link with `?next=`; the reason + **Book a consultation** → `/book?category=Consultation`; *"Authorised for … until …"* per cat — **US-SHP-10 AC1** |
| U-B7 | `PrescriptionCatPicker` keyboard-only | Arrow keys move, Space selects, `onChange` fires with the cat id; required error announced when submitted empty |
| U-B8 | `ReviewsSection` with eligibility `canReview`, `AlreadyReviewed`, `NotPurchased` | **Write a review**; **Edit your review**; the plain-language refusal |
| U-B9 | Recommendation blocks with one empty block | The empty block is absent; the life-stage heading reads *for your cats* when `basis` is `YourCats` |
| U-B10 | Stock alert with `willNotify: false` | OQ-7's text and a link to notification settings — ADR-FE-038 |
| U-B11 | Guest clicks **Save** | Sent to `/sign-in?next=/shop/products/{slug}?sku=…` |
| U-B12 | Review body containing `<b>x</b>` and `**x**` | Rendered literally as text |
| **C — countries** | | |
| U-C1 | `OriginGrid` with a flagless origin | Globe icon, name as text, count, notes excerpt; flags `alt=""` |
| U-C2 | Origin page | Origin fixed; every other filter group available — **US-SHP-05 AC2** |
| **D — back office** | | |
| U-D1 | Editor header with an unmet blocking item; **Publish** → `400 ProductNotPublishable` | Publish enabled; the server message shown; focus on the checklist |
| U-D2 | Food section on a Litter product; on Cat Food | Absent; present with `%` units on the four analysis inputs |
| U-D3 | `VariantsTable` with `stock: null` and with sellable 0 | *No stock recorded*; *0 sellable* — ADR-FE-032 |
| U-D4 | Schedule a price with a past date; *Now* | Blocked inline; sends the current instant |
| U-D5 | `ImagesEditor` without alt text | Field error; the honest upload note visible |
| U-D6 | Any section save → `409` | Reload message; the form values kept |
| U-D7 | Translations tab with one locale; with two | Hidden; one sub-form for `bn` |
| U-D8 | `StaffProductTable` barcode typed + Enter | Search runs with that value; URL updated |
| U-D9 | Review moderation: **Reject** without a reason | Field error; with a reason → confirm → request |
| **E — portal** | | |
| U-E1 | `FoodSuggestions` with each reason, an allergy exclusion and the hint | Each reason as text; the exclusion sentence; the hint links to the cat's edit page — **US-SHP-11** |
| U-E2 | `ReviewForm` server `400 ReviewNotPermitted` | Plain-language message; values kept |
| U-E3 | `StockAlertList` and `WishlistList` states | Status text per state; *No longer sold* when `onSale` is false |
| **F — shell and language** | | |
| U-F1 | `MaintenanceGate` with maintenance on; a storefront `503` | The notice replaces the page; links to portal and booking — US-ADM-08 |
| U-F2 | `LanguageSwitcher` with one locale; with two | Absent; present and rewrites `?lang=` |
| U-F3 | Product in `bn` with `fieldLocales.feedingGuide: "en"` | The feeding-guide element carries `lang="en"`; the others `lang="bn"` — **US-SHP-14 AC2** |
| **X — cross-cutting** | | |
| U-X1 | Every form: server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X2 | Dirty editor section or review form + navigate | Unsaved-changes confirm |
| U-X3 | Repository scan | No `dangerouslySetInnerHTML`; no `fetch(` outside `client.ts` |

## 16. End-to-end test cases

MSW first, then the real API per HO-SHP-001 phase; both runs are recorded. A real-API case whose
upstream area is not built is recorded **not runnable** with the reason, never passed.

| ID | Case | Expected |
|---|---|---|
| E-A1 | **AC-5**: open **Shop by Country** → Thailand → filter *Kitten* and *In stock* → open a product | Correct origins listed; only Thai kitten products; the product page shows origin, ingredients and availability; each catalogue API response in the run under 500 ms (recorded from the network log, real API) |
| E-A2 | Filter, sort, go to page 2, reload, press Back | Filters, sort and page survive reload; Back restores the previous state — **US-SHP-04 AC2** |
| E-A3 | Search a misspelled brand; search nonsense | Results include the brand; nonsense shows *Did you mean* / popular — **US-SHP-06** |
| E-A4 | Search an ingredient and a tag | Matching products listed — **US-SHP-06 AC2** |
| E-B1 | Product with an out-of-stock variant: switch variants | Unavailable variant marked; others in stock — **US-SHP-02 AC2** (real API: stock pushed by the HO-SHP-001 test helper; after HO-IMP-001, by a goods receipt) |
| E-B2 | Prescription diet as a guest → sign in → no authorisation → **Book a consultation** | Reason shown; lands on `/book` with the consultation category (real run needs HO-APT-002) — **US-SHP-10 AC1** |
| E-B3 | Kit with one out-of-stock component | Kit shows *Out of stock* — **US-SHP-12 AC2** |
| E-C1 | Staff creates a food product, adds a SKU and price, publishes without country of origin, adds it, publishes; a draft's URL | Refused naming country of origin; then published and visible in the shop; the draft URL is a not-found page — **US-SHP-01 AC2, US-SHP-03 AC3** |
| E-C2 | Staff schedules a price for tomorrow | Shown under *Scheduled*; the storefront still shows today's price |
| E-C3 | Staff moderates a pending review (MSW; real run after HO-ORD-001 — no purchase can exist before it) | Published review appears on the product page — **US-SHP-09 AC3** |
| E-D1 | Save to wishlist; request a stock alert with and without marketing consent | Wishlist shows the item; the alert confirmation shows the channel, or the not-delivered warning — **US-SHP-13** |
| E-D2 | (Real API) the stock arrives (pushed by the test helper) and both worker passes run | One back-in-stock message in the inbox (customer with in-app marketing consent) |
| E-D3 | Food suggestions for a cat with a chicken allergy | Chicken products absent; the exclusion sentence shown — **US-SHP-11** |
| E-E1 | Maintenance on (HO-ADM-002) | Storefront shows the notice; `/staff/shop/products` works — US-ADM-08 |
| E-F1 | Two locales: switch to `bn` on a product; staff edits the `bn` feeding guide | Content in `bn` with `en` fallback where missing; the edit appears — **US-SHP-14** |
| E-X1 | axe on every route in §8.1, including the open filter sheet, the prescription gate, the review form and the editor with errors | Zero serious/critical |
| E-X2 | Every storefront route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control — NFR-CMP-2 |
| E-X3 | Catalogue and product pages with 4G throttling (Playwright CDP), real API | LCP ≤ 2.5 s (recorded) — NFR-PRF-1 |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-SHP-001 §12.4; MSW handlers; `msw/node` for server rendering;
   `client.ts` `getPublic`; `lib/format` and `lib/shop/catalogueParams`.
3. `(shop)` shell (maintenance, banners, header, search) → catalogue (filters, sort, pages, no
   results) → Shop by Country.
4. Product page: gallery, variants, buy box, details tabs, bundle, reviews list → prescription gate
   and cat picker → recommendation blocks → wishlist and stock-alert buttons.
5. Portal: wishlist and alerts, review form and my reviews, food suggestions, cat-page link.
6. Back office: product list → new product → editor header and checklist → details → food →
   variants and prices → images → bundle → related → translations → origins → review moderation.
7. U-series; E-series on MSW; then E-series against the real API per HO-SHP-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | SHP-01 | Staff create products with every FR-SHP-2 field and all FR-SHP-1 categories; drafts never appear in the shop | U-D1, E-C1 |
| A2 | SHP-02 | Variants each show their own price, images and availability; an out-of-stock variant is unavailable while others remain | U-B1, U-B2, U-D3, E-B1 |
| A3 | SHP-03 | Food labelling is edited and shown; the prescription flag is set; publishing without origin is refused with the reason | U-B4, U-D2, E-C1 |
| A4 | SHP-04 | Filters, sort and pagination combine and persist across sort, reload and Back; responses meet the budget in the AC-5 run | U-A1, U-A2, U-A5, U-A6, E-A1, E-A2 |
| A5 | SHP-05 | Shop by Country lists origins with flag, notes and count; an origin page keeps the other filters | U-C1, U-C2, E-A1 |
| A6 | SHP-06 | Misspellings, ingredients and tags find products; no results offer alternatives and popular products | U-A4, E-A3, E-A4 |
| A7 | SHP-07 | Availability and the best-before notice come only from the API | U-B3, U-D3 |
| A8 | SHP-08 | Related and life-stage blocks render only what the server returns (exclusions are the server's) | U-B9 |
| A9 | SHP-09 | Eligible customers write reviews that appear after approval; ineligible ones are told why; staff moderate | U-B8, U-B12, U-E2, U-D9, E-C3 |
| A10 | SHP-10 AC1 | A prescription product is visible, not purchasable, with the reason and a consultation link | U-B6, E-B2 |
| A11 | SHP-10 AC2 *(component)* | `PrescriptionCatPicker` is built and accessible for HO-ORD-002 to record the cat on the order line | U-B7 |
| A12 | SHP-11 | Suggestions show reasons and the allergy exclusion | U-E1, E-D3 |
| A13 | SHP-12 | Kits show their contents and are unavailable when a component is | U-B5, E-B3 |
| A14 | SHP-13 | Wishlist and stock alerts work; an alert that will not be delivered says so | U-B10, U-B11, U-E3, E-D1, E-D2 |
| A15 | SHP-14 | Content shows in the chosen language with marked fallbacks; staff edit each language | U-F2, U-F3, U-D7, E-F1 |
| A16 | ADM-07/08 *(storefront half)* | Shop banners render; maintenance replaces the storefront with a notice while the back office works | U-F1, E-E1 |
| A17 | UI-6 / UI-1 / NFR-PRF-1 | Zero serious/critical axe violations; 360–1440 px; LCP ≤ 2.5 s on 4G | E-X1, E-X2, E-X3 |

**Deferred**: *Add to cart*, recording the cat on the order line, checkout blocking of an expired
authorisation (HO-ORD-002); issuing authorisations (HO-PHM-002).

---

## 19. Required gates

| # | Gate | Focus |
|---|---|---|
| 1 | ARCHITECTURE | The `(shop)` group (ADR-FE-029); Server Components through `client.ts` only (ADR-FE-030); URL-owned catalogue state; **no client-side filtering, availability or eligibility logic**; no new dependency; the scoped edits to `app/page.tsx`, `client.ts`, the layouts and the HO-CAT-002 page match §13 |
| 2 | SECURITY | Review text and all product text rendered as text or through `LimitedMarkup` only; `?next=` allow-list additions keep the open-redirect guard; `getPublic` never forwards cookies; staff routes still behind the staff guard; no clinical detail in the prescription gate |
| 3 | CODE_REVIEW | Money only through `lib/format`; one params parser; component size; hooks only in `lib/hooks/` |
| 4 | TEST | `TC-HO-SHP-002.md` first; mock and real runs separate; E-A1 and E-X3 record their timings; not-runnable real cases recorded with their reason |
| 5 | ACCEPTANCE | Every §18 row; A11 marked as the component half with HO-ORD-002 named |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002). Do not build
a parallel shell, session model, markup renderer or money formatter.

**20.2 A screen seems to need client-side filtering, sorting, stock or eligibility logic.** Ask the
server; do not compute it.

**20.3 A cart, checkout or payment step is requested.** It belongs to HO-ORD-002, and no payment
gateway exists in v1. Report it.

**20.4 The real API disagrees with HO-SHP-001 §12.4.** Report it; do not shim.

**20.5 A screen needs an endpoint or field HO-SHP-001 does not specify.** Report it; do not invent a
mock field.

**20.6 A new dependency or a `next.config.ts` change seems necessary** (image optimisation, a
carousel, i18n). Raise it.

**20.7 Image upload is requested.** The API takes a storage key (HO-SHP-001 ADR-091). Do not add an
upload route or a storage client.
