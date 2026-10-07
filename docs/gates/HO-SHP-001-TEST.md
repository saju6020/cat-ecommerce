GATE: TEST
HANDOVER: HO-SHP-001
COMMITS: abe212a (HEAD of ho/SHP-001; full branch, `git diff master...HEAD`). This gate made no commit and changed no product or test code.
DATE: 2026-10-04
STATUS: PASS

Sheet: `docs/test-cases/TC-HO-SHP-001.md`. It was written first from §15, §16, §18 and the three earlier gate records, with all 149 rows `NOT RUN`, then executed row by row. Final counts: **149 total, 131 PASS, 0 FAIL, 18 BLOCKED, 0 NOT RUN.** Of the 131 PASS rows, 15 are marked "PASS (partial)": the mapped test was watched passing and covers the core of the row, and the Evidence column names the uncovered clause.

The 18 BLOCKED rows:
- 9 need HO-IMP-001, which does not exist: I-C1..I-C6, I-F5 (as written), U-C1, U-C4. The substitute rows U-C1s (sales stats) and I-F5s (price-effectivity fixture) are PASS.
- 9 have no implementing test (or only a partial one that leaves a whole expected outcome unasserted): U-A5b, U-B2, U-E3, I-A6, I-A8, I-A12, I-E2, I-E3 and the regression row R3. The sheet lists each with what would unblock it.

Owner rule (2026-09-27) applied: no BLOCKER or HIGH finding, so the gate passes. MEDIUM and LOW items are for `docs/backlog/SHP.md` (not edited here).

Exact results (all against `CATECOMMERCE_TEST_DB` = `catecommerce_shp`; `catecommerce_dev` never touched):
- `dotnet build CatEcommerce.sln`: 0 warnings, 0 errors. `dotnet ef migrations has-pending-model-changes`: none.
- Domain.Tests 650/650, Application.Tests 134/134, Infrastructure.Tests 107/107 (each run twice, identical).
- Integration `Category!=Performance`: run 1 **662/662**, run 2 **662/662**, 0 failed, 0 skipped.
- Integration `Category=Performance` (I-E11), three runs, 1/1 each: p95 **267.0 ms**, **266.4 ms**, **277.7 ms** (p50 101.8 / 104.7 / 106.8; max 492.1 / 503.0 / 477.4; budget 500 ms on p95). The test asserts the fixture holds at least 20,000 SKUs and that every response is 200 before it judges p95.

Extra checks:
- (a) Fail-closed default. `ShopFailClosedAvailabilityEndpointTests` 3/3. The main test has a positive control (same product InStock on the Testing host, OutOfStock on the fail-closed host) across detail, bundle, list, `availability=inStock`, purchasability, wishlist, alerts and recommendations. Config read: `appsettings.json` has no `InterimAvailabilityFromPrice`; `appsettings.Development.json` and `appsettings.Testing.json` do not set it; `ShopOptions.InterimAvailabilityFromPrice` is a bare bool, default false; the only `true` in the repo is `tests/CatEcommerce.IntegrationTests/ShopApiFactory.cs:32`. Production leaves it false.
- (b) `ShopBundlePrescriptionEndpointTests` 3/3, plus the Domain test `SetPrescriptionRequired_On_WhileAComponentOfABundle_IsRefused_NamingTheBundle_ButOffIsAllowed`.
- (c) `ShopCodeReviewFixesEndpointTests` 5/5; `FoodRecommendationAndLocalisationTests` all pass inside the Domain run (U_H2 incl. duplicate allergens, unknown ingredients, bundle union, plural matching).
- (d) `MaintenanceMode_NeverBlocksAdminApiOrStaffSignIn` and `AdminLogCaptureEndpointTests` pass (2/2); both carry `[Collection(AdministrationGlobalSettingsCollection.Name)]`, a `DisableParallelization` collection; no storefront test failed with a 503 in either full run.
- (e) Cold start: scratch DB `catecommerce_shp_tg1` created, migrated from empty with `dotnet ef database update --connection` (Done), then 89 Shop and Product integration tests 89/89 passed (the same 89 that pass on `catecommerce_shp`), schema inspected by SQL (schema `product`, 13 tables, the 12.1 unique indexes, `pg_trgm`, trigram GIN index, three views), then the DB was dropped and confirmed absent from `pg_database`.
- (f) Notification tests: the stock-alert class carries `[Collection("Notifications")]`; delivery waits use the bounded `RunNotificationDeliveryUntilAsync` (200 passes, fails loudly) with an own-row predicate. Single-pass calls appear only where the assertion is "nothing more was sent".

FINDINGS
- [MEDIUM] tests/CatEcommerce.IntegrationTests/Endpoints/ShopCatalogueEndpointTests.cs — I-E2 and I-E3 (acceptance A9, A10) are thinly evidenced: no test filters by subcategory, protein source or rating, none runs "each filter alone", and only the price sorts are asserted; popularity, newest and rating orderings have no test (I-E11 hits every sort for latency and a 200 only). BLOCKED on the sheet.
- [MEDIUM] tests/CatEcommerce.IntegrationTests/Endpoints/ProductEndpointTests.cs — I-A6: no Shop test asserts that barcodes are absent from the public product detail, nor per-SKU images. The behaviour is a data-exposure promise of §16 and has no guard.
- [MEDIUM] tests/CatEcommerce.IntegrationTests/Endpoints/ProductEndpointTests.cs — I-A8: no test schedules a price ahead, advances the clock and reads `scheduled`/`past`; only a price effective now is read back.
- [MEDIUM] tests/CatEcommerce.Domain.Tests — U-B2: `GuaranteedAnalysis` validation (sum over 100, bounds, all-or-none) has no test; only the happy path through `SetFoodDetails_PersistsEveryField`. U-E3: no test names `InvalidCatalogueFilter` (4 parameters).
- [MEDIUM] I-A12 — staff product list filter, sort and paging and the CSV header row have no test (only the CSV formula guard, U-X3).
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20260927183008_AddProductCatalogue.cs — §12.1 index table deviations, observed on a freshly migrated DB: no index on `food_life_stage` or `food_protein_source`; the brand and country-of-origin indexes are not filtered `WHERE status = Active`; `ix_product_first_published_on_desc` is ascending. I-E11 still passes (p95 about 270 ms), so no budget impact.
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/ShopFailClosedAvailabilityEndpointTests.cs — `WithTheInterimSettingOff_FoodRecommendationsForACat_AreEmpty` has no positive control, so an unrelated reason for an empty list would also pass; `TheShippedDefault_IsFailClosed` asserts only the C# default of `ShopOptions`, not the layered configuration (read by this gate, not asserted by a test).
- [LOW] Audit rows (`BusinessEvent`) for the prescription-flag change and for moderation, and the audited 403 of I-A10, are never asserted over HTTP (I-B2, I-D3, I-A10 are PASS (partial)).
- [LOW] Other partial rows: I-A4 (no HTTP creation per category), I-A5 (no 404 after archive), I-A7 (10-way concurrent race untested), I-D5 (another customer's edit 404 untested), I-E4 (draft-only origin absent, exact count), I-E6 (product-name typo not queried), I-E7 (tag search not queried), U-C2 (discontinued/archived assert OutOfStock, not "not listed").
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/ShopCatalogueEndpointTests.cs:55 — `Search_NoResults_ReturnsSuggestions` puts its `didYouMean` assertion inside `if (totalCount == 0)`, so it can pass vacuously (already backlog row 38; re-confirmed). `ShopCodeReviewFixesEndpointTests` hint assertion shared-database caveat is backlog row 42, not re-raised.
- [LOW] No test asserts the four new violation codes `InvalidProductName/Brand/Description/SkuCode` (regression row R3, BLOCKED).
- [LOW] Environment, not product: `catecommerce_migration_schema_test_<guid>` is present in the local PostgreSQL (MigrationsOnAnEmptyDatabaseTests' name pattern, apparently a past aborted run). This gate did not create or drop it.
- [LOW] Informational: `appsettings.Development.json` carries no override either, so a local Development host is also fail-closed (every SKU out of stock) until `Shop:InterimAvailabilityFromPrice` is set; consistent with ADR-082 and backlog row 13.
- Rows 1-42 of `docs/backlog/SHP.md` were not re-raised except where noted above. No FAIL row and no production bug found.

Not run, with reasons (full list in the sheet): I-C1..I-C6, I-F5 as written, U-C1, U-C4 (HO-IMP-001 absent); U-A5b, U-B2, U-E3, I-A6, I-A8, I-A12, I-E2, I-E3, R3 (no test exists).

SCOPE CHECK
- files in allowed scope: 2/2 (`docs/test-cases/TC-HO-SHP-001.md`, `docs/gates/HO-SHP-001-TEST.md`), both left uncommitted
- files outside allowed scope: none. No product code and no test code edited. Scratch artefacts live only in the session scratchpad (a throwaway Npgsql inspector, logs, the sheet generator). Scratch DB `catecommerce_shp_tg1` created and dropped; `catecommerce_dev` untouched.

ADDENDUM 2026-10-04 — RE-VERIFICATION AFTER 4bbd6c3 (STATUS unchanged: PASS)

Commit 4bbd6c3 wrote tests for the nine rows this gate had marked BLOCKED "no test exists", rewrote the conditional `didYouMean` assertion in `Search_NoResults_ReturnsSuggestions` (backlog row 38), and fixed a production gap found while writing U-A5b: `FilePurposeAccess` now maps `ProductImage` to `Products.Manage` (before, no staff role could upload a product image in the real host). That production change is under a separate security delta review and is not judged here.

I re-ran everything on `catecommerce_shp` (`CATECOMMERCE_TEST_DB` set; `catecommerce_dev` untouched) and read each new test against §16's wording.
- `dotnet build`: 0 warnings, 0 errors.
- New tests: `ProductSection16GapTests` + `FilePurposeAccessTests` (Domain) 49/49; `ShopSection16GapEndpointTests` + `ShopCatalogueEndpointTests` 35/35.
- Full suites again: Domain 683/683, Application 134/134, Infrastructure 107/107, Integration `Category!=Performance` 688/688, 0 failed. (I-E11 not re-run; nothing in 4bbd6c3 touches the catalogue read path or the views.)

Per-row verdicts (all in the sheet, marked "added after the TEST gate (4bbd6c3); re-verified"):
- U-A5b PASS: gif refused by the StoredFile rule and at upload (`FileTypeNotAllowed`), unminted file id is `InvalidImage`, png control attaches.
- U-B2 PASS: 32/15/4/10, 60/30/10/5, 101/0/0/0, three-of-four, domain and HTTP; stored value untouched by refusals.
- U-E3 PASS: all four named inputs plus boundaries, each asserting code `InvalidCatalogueFilter` and the parameter name.
- I-A6 PASS: two SKUs, own price/size/images; no barcode value or the word "barcode" in public detail or listing; staff control carries them.
- I-A8 PASS: scheduled, then current and past after advancing a FakeTimeProvider, public price follows.
- I-A12 PASS: staff list status/brand/category/kind/listed/q/sort/paging with a decoy brand; CSV header, filtered rows, injection guard.
- I-E2 PASS: every filter alone, three-filter combinations, exclusions, echo; exact set equality.
- I-E3 PASS: all five sorts with exact order, echoed sort, paging keeps the sort.
- R3 PASS: all four violation codes at domain and HTTP level, never `InvalidSlug`. The `AddMemoryCache` half of the original R3 wording is still indirect.
- I-E8 upgraded from PASS (partial) to PASS: `didYouMean` is now asserted unconditionally with `totalCount` 0 by construction (backlog row 38 is resolved by this test change).

New sheet counts: **149 total, 140 PASS, 0 FAIL, 9 BLOCKED, 0 NOT RUN.** 14 PASS rows remain "PASS (partial)". The 9 BLOCKED rows are exactly those needing HO-IMP-001 (I-C1..I-C6, I-F5 as written, U-C1, U-C4). The MEDIUM findings above for I-E2/I-E3, I-A6, I-A8, U-B2/U-E3 and I-A12, and the LOW finding on the conditional `didYouMean` assertion, are closed by these tests; the other LOW findings stand.

New observations: none at BLOCKER or HIGH. LOW: the U-B2 "three of four" case is asserted as HTTP 400 only, not as `InvalidGuaranteedAnalysis` (the request contract refuses it before the domain rule). STATUS stays PASS.
