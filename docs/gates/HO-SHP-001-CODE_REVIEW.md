GATE: CODE_REVIEW
HANDOVER: HO-SHP-001
COMMITS: second pass over ff04d57..HEAD = f6a024a 5de95f9 7cafa22 (first pass covered master...64b550a, record ff04d57)
DATE: 2026-10-04
STATUS: PASS

FINDINGS
- First-pass HIGH (duplicate/case-variant allergens crash `FoodRecommendationPolicy` with ArgumentException): **closed.** `FoodRecommendationPolicy.cs` now trims, groups case-insensitively and keys the dictionary on the first spelling; blank entries are dropped. `U_H2_DuplicateAndCaseVariantAllergens_DoNotCrash_AndCountOnce` asserts no crash, one exclusion entry, count 1; the HTTP twin asserts 200 with the safe product present.
- First-pass MEDIUMs: **closed.**
  - Price filter (`GetCatalogueQueryHandler.cs`): now "any Active SKU whose current price (greatest `EffectiveFrom <= now`, the `SkuPricing` rule) lies within the bounds"; a product with no current price never matches once a bound is given. Translated entirely to SQL (a correlated subquery over `db.Products`/`db.SkuPrices`; the suites would throw on client evaluation otherwise). `now` is the injected `TimeProvider`, a `DateTimeOffset` against `timestamptz`, so there is no time zone dependence; inactive SKUs are excluded; the boundary matches the view's `effective_from <= now()`.
  - Unknown ingredients: with any recorded allergen an empty list, or a bundle with an unknown food component, is excluded and `IngredientsUnknownExcluded` is hinted; a cat with no allergen is unaffected.
  - Bundles: `LoadBundleIngredientsAsync` unions the food components' lists via projections (SkuRows, BundleComponentRows, one product projection; no aggregate); a food component with no list, or an unresolvable SKU, makes the kit unknown; non-food components are ignored.
  - Plural matching is symmetric (`SameWord`). Checked: "pea" vs "peach", "oat" vs "boat", "chicken" vs "chickpea" do not match; "glass" does not match "gla"; "es"/"s" both directions; multi-word "fish oil" matches "fish oil" and, conservatively, "fish, oil" (punctuation tokenises to space).
  - Culture: price parsing and `F2` rendering use `InvariantCulture`.
- [LOW] `AllergenMatcher.cs` `SameWord` — y/ies plurals are not matched ("anchovy"/"anchovies", "berry"/"berries"); a residual false negative. Add an "ies" rule.
- [LOW] `GetFoodRecommendationsQueryHandler.LoadBundleIngredientsAsync` — a food-category bundle whose components are all non-food yields an empty union, which the policy reads as unknown, so an allergic cat never sees it. Conservative; arguably correct.
- [LOW] `GetCatalogueQueryHandler.cs` price filter uses the application clock while the view's displayed min/max use the database `now()`; a price flipping within the skew window can be filtered one way and displayed the other. Sub-second, cosmetic.
- [LOW] `ShopCodeReviewFixesEndpointTests` — the `IngredientsUnknownExcluded` hint assertion can be satisfied by another test's unlabelled product in the shared database (the policy-level unit test proves it precisely). The isolation from 7cafa22 is sound but relies on the clock-monotonic `NextUnitsSold` and the 12-result cap: this class adds about seven products with the shared focus, so newer units outrank older runs and the cap is not reached; it would become brittle if many more same-focus products were seeded in parallel.
- First-pass LOW items not part of this fix remain open (no regressions): unbounded Amount, unknown filter values ignored, category change leaving food state, due-alerts window, notify-time gate recheck, sales-stats code, review title control characters, hardcoded "BDT", weak/timing-based tests.

VERIFIED
- New tests assert real behaviour and can fail: duplicate allergens, unknown/blank/kit-unknown exclusion with a healthy-cat control, union over components (unit and HTTP incl. safe/unsafe/unknown kits), the two-SKU price band (500/5000 excluded from 1000-1500, 500/1200 included, 4500 and 600 bounds, unpriced excluded), invariant culture and garbage rejection (400).
- `git diff ff04d57 HEAD --stat`: 8 files only (handover Amended row, ShopCatalogueController, AllergenMatcher, FoodRecommendationPolicy, GetCatalogueQueryHandler, GetFoodRecommendationsQueryHandler, one unit test file, one new integration test file). No other code, migration or doc changed.

COMMANDS AND RESULTS (CATECOMMERCE_TEST_DB = catecommerce_shp)
- `dotnet build`: 0 warnings, 0 errors.
- `dotnet test --filter "Category!=Performance" --no-build`: Domain 650/650, Application 134/134, Infrastructure 107/107, Integration 662/662 (3 m 11 s). 0 failed.
- `dotnet test tests/CatEcommerce.IntegrationTests --filter "Category=Performance" --no-build`: I-E11 passed, p95 233.9 ms (p50 97.2, max 437.4; budget 500).

SCOPE CHECK
- files in allowed scope: 8/8 in ff04d57..HEAD
- files outside allowed scope: none
