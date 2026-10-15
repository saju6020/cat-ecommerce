GATE: COMBINED_REVIEW (architecture + security + code quality, follow-up gate rule)
HANDOVER: HO-SHP-001-FU4
COMMITS: d26c5d51 (a8761829..d26c5d51)
DATE: 2026-10-11
STATUS: PASS

FINDINGS
- [MEDIUM] tests/CatEcommerce.IntegrationTests/Endpoints/ShopCatalogueQueryEqualityEndpointTests.cs — no coverage for a product without a current price (price-sort sentinels), inactive/unpublished exclusion, category+search+brand on non-bundles, the zero-result popular merge. Backlog SHP 70.
- [LOW] GetCatalogueQueryHandler.cs BuildSuggestionsAsync/TopSellingAsync — popular suggestions have no deterministic tie-break; recommend ThenBy(ProductId) in both SQL orders and the merge. Backlog SHP 69.
- [LOW] same test file / migration doc — hard-coded `s.status = 1`; doc cites row 64 (is 68); Down untested. Backlog SHP 71.
- [LOW] migration authored on the feature branch rather than by the integrator; re-timestamp/re-Designer at merge if another migration lands first. Backlog SHP 72.

VERIFIED (no finding)
- View column list, order and types identical to the 20261003205215 definition (is_on_sale renamed in 20261007173316); CREATE OR REPLACE valid. Aggregate-without-GROUP-BY LATERAL yields one all-NULL row when unpriced, equal to the old LEFT JOIN miss. Down rebuilds the old DISTINCT ON/GROUP BY pricing with the same bundle clause, then drops the index. Up idempotent (IF NOT EXISTS, OR REPLACE). Snapshot untouched.
- Raw SQL: migration interpolates only enumeration ints; tests use parameterised FormattableString. No injection surface.
- Two-step read: ids from the filtered/sorted query (ThenBy ProductId tie-break retained, so paging stable incl. price_asc ties), rows by id restored in id order; count unchanged; constant round trips (count, ids, rows, sellable, localiser; +2 only for availability=inStock), no N+1.
- Availability: standard semi-join and bundle check equal the old predicate; the bundle list is limited to filtered bundles. Popular NULLS-FIRST on DESC is reproduced by `FirstPublishedOn is null` ordering.
- Module boundary: Shop reads Inventory only through the SkuSellable view as before.
- Worktree check: dotnet build in D:/my-repo/cat_ecommerce-wt/SHP-001-FU4 succeeded (0 errors); no POR-001 path in the test binaries or csproj; all test runs below executed this worktree's dll.

TEST RUNS (CATECOMMERCE_TEST_DB=catecommerce_shpfu4, from this worktree)
- Shop integration filter: 152 passed, 0 failed (2m37s).
- ShopCataloguePerformanceTests.I_E11 x3: p95 280.9 / 263.8 / 270.8 ms (budget 500; p50 112-131). With inStock p95 356-397, without 238-247. Less favourable than the builder's 156-173 ms (machine load/variance) but about half of the 470-532 ms before.
- OrderingPaymentEndpointTests + ShopCatalogueQueryEquality*: 19 passed, 0 failed (1m16s). The Ordering failure seen in the builder's full run did not reproduce.

SCOPE CHECK
- files in allowed scope: 7/7 (migration + Designer, availability, handler, new test, backlog, handover)
- files outside allowed scope: none
