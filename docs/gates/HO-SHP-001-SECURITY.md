GATE: SECURITY
HANDOVER: HO-SHP-001
COMMITS: second pass over 0d5d09d..HEAD = 48588a5 8eec49c a81be7a (first pass covered master...97b3514: f2e871b 2f774f5 16bc0b4 82f82e3 bd6271e c7bc6ee 9f2ae08 85691ab 07c0408 c0756c7 22d0889 97b3514)
DATE: 2026-10-04
STATUS: PASS

FINDINGS
- First-pass HIGH (prescription diet sellable through a bundle): **closed.** These were verified against every path HO-ORD-001 or the storefront can use to sell:
  - The write rule. `SetPrescriptionRequiredCommandHandler.cs` and `ProductAggregate.Food.cs` refuse to turn the flag on while any SKU of the product is a component of a non-archived bundle (Draft or Active), using `BundleMembershipReader`. Turning the flag off is always allowed.
  - The existing rules still hold. `SetBundleComponents` refuses a prescription component, and `ProductPublicationRules.cs:70` re-checks every component's prescription flag on every publish.
  - Archive, flag, re-publish is closed. Archived to Active is a `Publish` transition, and that re-check refuses it.
  - Unflag then re-flag goes through the membership check again.
  - A SKU added later to a product that needs a prescription inherits the product's flag, and `SetBundleComponents` refuses it.
  - Several SKUs in different bundles: the reader takes all of the product's SKU ids and names every bundle that holds any of them.
  - Defence in depth. `ProductProjections.BundlesWithPrescriptionComponentAsync` reads the components' *current* product flag at read time, and through it:
    - `CheckPrescriptionEligibility` returns not eligible without calling the diet gate.
    - Purchasability returns not purchasable with `PrescriptionAuthorisationRequired`.
    - `GetSkuSaleFacts` reports `prescriptionRequired: true`.
    - `SkuSellability` returns OutOfStock even with the interim flag on, so the product detail, wishlist and stock-alert worker never treat such a bundle as available.
  - The TOCTOU race. A concurrent flag save on X and component set or publish on B is write skew across two aggregates, and `xmin` does not prevent it. It can produce a bundle whose stored component is a prescription product, but every purchase decision above reads the live flag, so it is still not eligible, not purchasable and not in stock. (b) is enough to cover the race for selling.
  - The 400 `PrescriptionProductInBundle` names the bundles. It is reachable only through `PUT /api/staff/shop/products/{id}/prescription` (`Products.Manage`: ShopManager, Administrator), who can see every bundle anyway. No leak.
- First-pass MEDIUM (facet cache keyed on raw `?category=`): **closed.** `GetCatalogueFiltersQueryHandler.cs:19-20` keys on the parsed enum value, and junk maps to `all`.
- a81be7a view change (`20261003205215_AddBundlesAndRelated.cs`):
  - The raw SQL interpolates only enum constants; there is no user input in it.
  - `in_stock = pricing.min_price IS NOT NULL` is equivalent to the old `EXISTS (active SKU with a price effective <= now())`. The pricing subquery is restricted to Active SKUs, and amount is non-null.
  - `DISTINCT ON (sku_id) … ORDER BY sku_id, effective_from DESC` picks the same current price as the old `LATERAL … LIMIT 1`. `(sku_id, effective_from)` is unique, so there are no ties.
  - The visibility `WHERE p.status = Active AND listed_on_storefront` is unchanged, and the bundle clause is unchanged.
  - Fail-closed still holds: every read still ANDs `in_stock` with `Shop:InterimAvailabilityFromPrice` (default false).
  - Nothing becomes visible or purchasable that was not before.
- [LOW] `src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20261003205215_AddBundlesAndRelated.cs` (bundle `in_stock` clause). This only arises from the write-skew race above. The view's bundle clause, and the views that read it, do not apply the prescription-component check, and `RecommendationExclusionPolicy` uses the bundle's own `prescriptionRequired` (false). With the interim flag on, such a bundle could therefore appear as "InStock" in the catalogue list or in recommendations even though purchasability, eligibility and the product detail all block it. It is presentation only and cannot be bought. Remediation: add `AND NOT EXISTS (component whose product.prescription_required)` to the bundle clause when the inventory view replaces it.
- [LOW] same migration (operational note, not exploitable): a81be7a edits an already-generated migration in place. A database that already applied `AddBundlesAndRelated` (e.g. this branch's `catecommerce_shp`) keeps the old view definition until it is recreated. The semantics are identical, so there is no security impact. The integrator should confirm that the merged database gets the new definition.
- First-pass LOW items remain open (they are not regressions): wishlist echoes draft products, the review-eligibility existence oracle, the food-recommendation N+1 with no rate limit, and review text kept on anonymisation.

Tests run (CATECOMMERCE_TEST_DB=catecommerce_shp): `ShopBundlePrescriptionEndpointTests` + `ShopFailClosedAvailabilityEndpointTests`: 6 passed, 0 failed.

SCOPE CHECK
- files in allowed scope: 16/16 in 0d5d09d..HEAD (Shop domain, application, infrastructure, read, api module, the area's own unmerged migration, tests, handover Amended row)
- files outside allowed scope: none (`IBundleMembershipReader` / `BundleMembershipReader` are new ports in the existing `Domain/DomainServices` and `Infrastructure/Product` folders, following the `IShopCustomerDataFinder` precedent; not a security matter)
