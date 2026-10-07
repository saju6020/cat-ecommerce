GATE: CODE_REVIEW (combined: architecture, security, code)
HANDOVER: BUGS-SHP-WEB
COMMITS: cf80713 76343e3 21a8a32 1503d35 d1f1d7f f4f4d02 269b890 8cc230e 278460d cc395a3 4aecff1 38b91fc 02804bf (range 7a742ec...HEAD)
DATE: 2026-10-07
STATUS: PASS

FINDINGS
- [MEDIUM] web/components/shop/ShopLoadError.tsx, web/app/(shop)/_components/CatalogueView.tsx, web/app/(shop)/shop/countries/page.tsx — S19 returns the error state with HTTP 200. Crawlers can index the "couldn't load" page, any CDN/proxy cache may keep it, and 5xx-based monitoring/alerting no longer sees API outages. The error is also swallowed (settle) with no server-side log. Fix: log the error server-side (console.error with status) before returning ShopLoadError, add robots noindex to the error render, and make sure such responses are not cacheable (Cache-Control no-store via dynamic rendering); consider an alert on the log line.
- [MEDIUM] web/lib/api/shop-catalogue.ts:~219-235 — S12 cache bound is incomplete. getProductDetailServer(slug, lang) and getProductReviewsServer(slug, page) still use revalidate 60 and are keyed on visitor-supplied lang / pageNumber, so a valid slug with arbitrary ?lang= values still creates unbounded Data Cache entries. Fix: validate lang against the enabled locales (or revalidate 0) and clamp pageNumber.
- [LOW] web/components/staff-shop/BundleEditor.tsx:~537 — S14 uses a regex lookbehind ((?<!...)); Safari before 16.4 throws a SyntaxError at render of the kit editor. Use a manual boundary check or a capture-group prefix.
- [LOW] web/lib/api/staff-shop.ts, shop-customer.ts — S10 encodeURIComponent leaves "." and ".." unencoded; safe for the guarded route ids (isGuid) but not a general guarantee for ids taken from API data. Optional: apply isGuid at the API layer.
- [LOW] web/tests/unit/cat/add-cat-customer.test.tsx — CAT AC4 test asserts class-name strings, not a real focus style; would pass on any class with those names.
- [LOW] web/app/cats/[id]/page.tsx — CAT row 1: a window-focus refetch after ownership resolved sets isFetching and flips isOwnershipResolved back to false (possible loading flicker). Harmless unless the page stays mounted without having redirected.
- [LOW] web/tests/e2e/shop-catalogue.spec.ts — S4 is covered only by Playwright e2e (JS disabled); there is no unit test and no hydration-mismatch assertion. Not run in this review (needs a build/server).

CHECKS
- S5 isSafeLink: rejects tab/CR/LF anywhere, then for a "/" path requires the next char not to be "/" or "\"; leading-space and "\/host" forms fall to new URL() with no base and throw (false). No bypass found.
- S10: isGuid is anchored (^...$), 8-4-4-4-12 hex, applied before any request (notFound in layout/page, which are client components, test covers both); encodeURIComponent on every id segment; tests assert one encoded segment.
- S11: NODE_ENV === 'development' gate matches instrumentation.ts; test covers production and development.
- S4: header, nav, SearchForm fallback and a Sign-in fallback are in the HTML; fallbacks are the same markup the server emits, so the client replaces them as a CSR-bailout boundary, no mismatch expected.
- S16: fallback chain is SKU price history, then any SKU price, then NEXT_PUBLIC_CURRENCY_CODE, then BDT. Admin callers (services, tax-classes) pass no consequence and keep the default sentence; unchanged.
- S13/S9: Reload remounts only on result.isSuccess; translations editor no longer keyed on version; removed locale resets its form. Sound.
- S15: aria-disabled (not disabled) keeps focus, click ignored while pending; aria-pressed removed so state is not announced twice. Acceptable.
- CAT 1: isOwner needs isFetchedAfterMount, not fetching, isSuccess and data, with staleTime 0. Cached data cannot confer ownership; a failed refetch with retained data gives status error so isSuccess is false.
- Each new test reasoned to fail without its fix (no fix reverted and re-run).
- tsc: exit 0. eslint: exit 0. vitest --maxWorkers=3: 145 files, 762 tests, all passed.
- Merge forecast: git merge-tree HEAD vs master, and HEAD vs ho/BUGS-ADM-WEB, and master vs ho/BUGS-ADM-WEB: all clean, no conflicts. ADM row 16 edits handleSubmit; SHP edits the props and the DialogDescription, different hunks. Retest ScheduleChangeDialog after the final merge.

SCOPE CHECK
- files in allowed scope: 47/47 (web/ source and tests only, plus web/.env.local.example and web/README.md)
- files outside allowed scope: none
