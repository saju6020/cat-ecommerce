GATE: SECURITY
HANDOVER: HO-SHP-002
COMMITS: cffaa79...ea686a1 (code head ea686a1; the later commits d096f3a, ac3f9b3 and ceb45bb are docs and gate records)
DATE: 2026-10-04
STATUS: PASS

Method: a static review of `git diff cffaa79...ea686a1` (web/ only), checked against the master SHP
controllers and `LimitedMarkup.cs`. `?next=` values were probed with Node's WHATWG URL parser.
No dev server or production build was run; the existing `.next` is a dev build.

0 BLOCKER, 0 HIGH, 1 MEDIUM, 4 LOW.

FINDINGS
- [MEDIUM] web/components/content/LimitedMarkup.tsx:24 and :64, web/lib/shop/helpers.ts:28 (`isSafeLink`, used by web/components/shop/BannerStrip.tsx:21). This is a carried defect and this slice widens its exposure. It is the open HO-ADM-002 SECURITY MEDIUM: a "site-relative" link `/\phish.example` (and `/<TAB>/phish.example`) passes both the client check and the server's `LimitedMarkup.Validate` (`src/Shared/.../LimitedMarkup.cs:40` rejects only `//`). Browsers resolve it to `https://phish.example`. It renders as a same-tab internal link with no target or rel. The client renderer also accepts `//x`, which the server refuses.
  - What HO-SHP-002 changes: product descriptions, feeding guides and origin sourcing notes, which `Products.Manage` holders write (ShopManager, a wider group than content authors), now render through the same renderer on the public storefront. Banner `linkUrl` goes through `isSafeLink`, which has the same `/\` gap.
  - Exploit: an insider, or a compromised ShopManager account, saves `[Sign in to claim your discount](/\phish.example)` in a product description. Every visitor to that product sees an internal-looking link to a credential-phishing page. There is no script execution: `javascript:` and `data:` cannot start with `/`, and the server also rejects `<`, `javascript:` and `data:`.
  - Fix, in one place for both helpers: treat a link as site-relative only if it matches `^/(?![/\\])` and contains no tab, CR or LF, or require `new URL(url, origin).origin === origin`. Make the same change in `LimitedMarkup.Validate` server-side. Add `//`, `/\` and `/\t/` cases to the tests.
- [LOW] web/app/(staff)/staff/shop/products/[productId]/layout.tsx:36 with web/lib/api/staff-shop.ts:160 (`productPath`), :315/:322/:332/:362/:368 (`skuId`), and web/lib/api/shop-customer.ts:156 (`catId` from `/portal/cats/[catId]/food`). Route params go into API paths without `encodeURIComponent` or a GUID check. This is client-side path traversal (CSPT), the same class as the HO-CAT-002 SECURITY LOW. Next 15.5 decodes dynamic params, so `/staff/shop/products/..%2F..%2F<x>` points the editor's GET, and every PUT/POST it then saves, at another API path with the staff cookies and the CSRF header.
  - Exploitability is low for three reasons. The editor renders only after a product-shaped GET. Every write is that path plus a fixed suffix. A staff member must also click Save.
  - The slug and country code are encoded, but `encodeURIComponent('..')` is still `..`. A slug of `..` therefore resolves `/api/shop/products/..` to `/api/shop/` (anonymous `getPublic`), and `/api/customers/me/shop/products/../purchasability` to `/api/customers/me/shop/purchasability` (a cookie-bearing GET). Both are harmless GETs today.
  - Fix: reject any `productId` or `catId` that is not a GUID at the route boundary with `notFound()` (`lib/validation/catId.ts` already has the pattern). Wrap the ids in `encodeURIComponent`. Reject a slug of `.` or `..` before building the path.
- [LOW] web/lib/api/client.ts:316. `getPublic` uses `globalThis.__mswFetch` whenever it is present, with no `NODE_ENV` check. Today it cannot activate in production:
  - Only `instrumentation.ts:14-22` sets it.
  - That code runs only when `NEXT_RUNTIME === 'nodejs'`, `NODE_ENV === 'development'` and `NEXT_PUBLIC_USE_MOCKS === 'true'`.
  - `next build` and `next start` force `NODE_ENV=production`.
  - Nothing user-controlled writes to `globalThis`.
  - `msw` is a devDependency. Even if the dead `import('./mocks/server')` survived into the server bundle, it is never executed.

  This is a defence-in-depth gap only. Fix: guard the read with `process.env.NODE_ENV === 'development'`, so a future module that sets the global cannot silently reroute server-side fetches.
- [LOW] web/lib/api/client.ts:322 (`next: { revalidate }`) with web/lib/shop/catalogueParams.ts:91-106. Each distinct catalogue query string becomes its own Next Data Cache entry. The `q` value is capped at 100 characters, but brand, origin, lifeStage, protein, category, subcategory and lang are free-form and unbounded in count. An anonymous client can request unique URLs and grow the server's data cache without limit (cache-flooding DoS).
  - Fix: use `revalidate: 0` / `cache: 'no-store'` for search and filter reads, and keep caching for the fixed shell reads. Alternatively, drop filter values that are not in the facet list, and cap the number of values per list.
- [LOW] Integrity, not confidentiality (point 9). The staff food, details and images saves replace fields the form never loaded. This is already recorded as a MEDIUM in HO-SHP-002-ARCHITECTURE.md (point 5) and is disclosed in §1 Amended (6)(a). There is no security escalation: every write needs `Products.Manage` and an `expectedVersion`. One more point: on the food page, the food save followed by the separate prescription save (`food/page.tsx:44-59`) is not atomic. If the second call fails, the flag is left unchanged, so it fails safe.

VERIFIED CLEAN (requested checks)
1. Open redirect.
   - `sanitizeNextPath` (lib/validation/nextPath.ts) matches `/shop` only as the exact path, `/shop/…` or `/shop?…`, so `/shopX` and `/shop.evil` fall back to the default. `//shop` is rejected by the leading-`//` check.
   - Every accepted value starts with `/s`, so it can never be protocol-relative or carry a scheme. `/shop/../..//evil.com`, `/shop/%2e%2e/%2e%2e//evil.com`, `/shop/\evil.com` and `/shop\t//evil.com` all resolve to the same origin (probed). Only the pathname can move, the same as the accepted `/portal/` and `/book` precedent (HO-ACC-002 SECURITY).
   - `app/page.tsx` sends a signed-out visitor to the fixed `/shop`; no input is involved.
2. HTML rendering.
   - There is no `dangerouslySetInnerHTML`, `innerHTML`, `eval` or `new Function` in the diff; U-X3 also scans for `dangerouslySetInnerHTML`.
   - `LimitedMarkup` builds React elements only. It has no HTML path, so `<script>` and event handlers are inert text. Links are limited to `https:` or a leading `/`, external links get `target=_blank rel="noopener noreferrer"`, and `javascript:` and `data:` render as literal text.
   - On the storefront, `LimitedMarkup` is used only for the description, feeding guide and origin sourcing notes, plus HO-ADM-002 banners (title and body), whose markup is ADM's. The staff previews are of the same fields.
   - Name, brand, ingredients, storage, review title and body, and translations render as React text. Excerpts use `plainExcerpt`, which outputs text.
   - Image `src` comes from the API `url` (currently null, so a placeholder) and does not execute script.
3. Mock seam. See the LOW above; it cannot activate in a production build.
4. `getPublic`.
   - `API_ORIGIN` is operator configuration, not user input.
   - User-controlled segments (slug, countryCode, locale, banner placement) are `encodeURIComponent`-ed, so `/`, `?` and `#` cannot escape the segment. The only residue is `..`, covered in the CSPT LOW.
   - Query strings are built with `URLSearchParams`.
   - No cookies or identity headers are forwarded, so a cached public response cannot carry customer data.
5. Catalogue URL state. `parseCatalogueParams` whitelists sort, integer-checks rating and page, regex-checks prices and caps `q`. Everything reaches the API through `URLSearchParams` and is reflected as React text (NoResults, ResultSummary, ActiveFilterChips).
6. Customer data and IDOR.
   - Every customer call is `/api/customers/me/...`. On master the server takes `CustomerId` from the session (CustomerWishlistController, CustomerStockAlertsController, CustomerReviewsController, CustomerFoodRecommendationsController with `{catId:guid}`).
   - Review edit renders, and calls `PUT /me/reviews/{reviewId}`, only when `reviewId` matches a review in the customer's own `GET /me/reviews` list.
   - Wishlist and alert ids come from the customer's own list responses.
   - The prescription cat picker lists the cats the purchasability response returns. The food page reaches only `/me/cats/{catId}`.
   - Sign-in calls `queryClient.clear()`, so there is no cross-account cache bleed.
7. Staff back office.
   - On master, every write carries `[RequireAction]`: `Products.Manage` on all product, SKU, image, kit, related, translation and transition writes; `Products.ManagePrices` on `POST skus/{id}/prices`; `Reviews.Moderate` on the queue and on publish, reject and hide.
   - The UI only reacts to 403 with `PermissionDeniedState`. It grants nothing on the client.
   - Images take a `fileId` only, with no upload path.
   - The CSV is built server-side (`StaffProductsController.BuildCsv`/`Escape`). It prefixes `'` to cells starting with `= + - @` (after leading whitespace) and quotes every cell. The client only links to it.
8. Prescription rules.
   - This slice has no add-to-cart (`actions` is empty).
   - `PrescriptionGate` is purely presentational and reads server purchasability. Nothing on the client decides eligibility or enables a purchase.
   - Bundles that contain a prescription diet are blocked server-side (HO-SHP-001 SECURITY).
   - Note for HO-ORD-002: key the buy action off the purchasability response, not `detail.prescriptionRequired`.

SCOPE CHECK
- files in allowed scope: 141/141 (web/** for the area, the TC sheet, the handover Amended row)
- files outside allowed scope: none. The two §14 edits, `web/app/(auth)/sign-in/page.tsx` (allow-list) and `web/tests/e2e/home.spec.ts`, are owner-approved in §1 Amended (ac3f9b3) and reviewed above with no security impact.
