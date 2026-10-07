GATE: ARCHITECTURE
HANDOVER: HO-SHP-001
COMMITS: second pass, fix range f24cdc1..22d0889: 1375c3a (merge of master 29d1b3d, test-only), c0756c7, 22d0889. Full branch: ho/SHP-001, `git diff master...HEAD`.
DATE: 2026-10-04
STATUS: PASS

This is the **second pass** of the ARCHITECTURE gate. The first pass (recorded in f24cdc1) failed on one HIGH: read-side handlers rehydrated aggregates. Its MEDIUM and LOW findings are docs/backlog/SHP.md rows 1–11, which this gate does not edit.

What this pass did:
- Reviewed `git diff f24cdc1 HEAD` line by line.
- Excluded the master merge's three test files. `git diff master HEAD` is empty for `AdminRosterLeaveEndpointTests.cs`, `CareReminderEndpointTests.cs` and `NotificationsIntegrationTestBase.cs`.
- Re-read every first-pass HIGH site in its current form.
- Read `Infrastructure/Product/{SkuRows,SkuSellability,SkuAvailabilityReader}.cs` and `ShopOptions.InterimAvailabilityFromPrice`.
- Read the `Sku.LabelFor` and `SkuPricing.PriceAt<T>` changes.
- Read the new §1 Amended row ("ARCHITECTURE gate first pass fixes"), the new ADR-082 build-amendments bullet and the `ShopApiFactory` change.
- Ran `dotnet build CatEcommerce.sln --no-incremental`: 0 warnings, 0 errors.
- Ran no tests and touched no database. Anything that depends on runtime behaviour is marked for TEST.

Owner rule (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings are reported only.

FIRST-PASS HIGH — read side rehydrated aggregates — **RESOLVED**

Every site listed in the first pass now projects before it materialises.
- **`SkuRows.cs` holds the shared projections.**
  - `SkuRow` and `BundleComponentRow` are built with `SelectMany(p => p.Skus / p.BundleComponents, …)` over the owned tables, so each is one SQL join, never an aggregate graph.
  - `PriceRow` is a `Select` over `sku_price`.
  - These are settable classes, not positional records, so EF can compose `Where` after the projection. That is the same lesson as the `/filters` fix.
- **`SkuSellability.cs:33-53`** makes at most three projected queries, whatever the number of SKUs. It makes none when the interim flag is off (:33).
- **`GetProductDetailQueryHandler.cs:29-48`** projects the product, including food columns, images and translations, with `AsSplitQuery`.
  - The SKU, price and rating reads are projections (:53-57).
  - The bundle path is two projected reads, not one per component (:121, :125).
- **The customer and contract queries now read `SkuRows()` and, where they need prices, `PriceRows()`:**
  - `GetPurchasabilityQueryHandler`
  - `GetSkuSaleFactsQueryHandler`
  - `GetSkuCatalogueFactsQueryHandler`
  - `FindSkuByBarcodeQueryHandler`
  - `GetBundleCompositionQueryHandler`
  - `GetReviewEligibilityQueryHandler`
  - `GetMyReviewsQueryHandler`
  - `GetReviewModerationQueueQueryHandler`
  - `GetSkuPriceHistoryQueryHandler`
- **The wishlist and alert reads project.** `WishlistAndStockAlertQueryHandlers` reads wishlist items through `SelectMany` (:25-29), alerts through `Select` (:73-78), and SKU, price and image rows as projections.
- **The cat is projected.** `GetFoodRecommendationsQueryHandler.cs:32-40` reads HO-CAT-001's cat as a projection, with allergens and conditions as string lists. Its candidate details (:104-113) are a projection too.
- **The remaining reads project too:** `GetProductRecommendationsQueryHandler.cs:37-46`, `GetProductReviewsQueryHandler`, `GetOriginsQueryHandler` and `GetOriginQueryHandler`.
- **The one kept aggregate read is justified.** It is the root load at `GetStaffProductDetailQueryHandler.cs:27`.
  - ADR-095 has the staff read share `ProductPublicationRules.Evaluate(product, facts)` with the publish command, and that pure rule takes the aggregate.
  - The reason is commented in the code (:18-21).
  - It is one bounded graph on a staff-only screen.
  - Its prices, component SKUs, related products and tax class are projections (:32-73).
- **No N+1 queries were introduced.** Every lookup is batched by an id list and matched in memory. The only per-item calls are to the `IDietAuthorisationGate` port, per SKU of prescription candidates. That shape is unchanged from the first pass and stays a CODE_REVIEW note.
- **Response shapes and contract query signatures are unchanged.** No query, ViewModel or `ShopModule` registration is touched in the diff. Two edge-case behaviour differences are noted below as LOW.
- **The new Domain helpers belong in Domain.**
  - `Sku.LabelFor` is the §8.3.1 display rule, made static. The instance `Label` delegates to it, so the aggregate and projected rows share one implementation.
  - `SkuPricing.PriceAt<T>(prices, selector, asOf)` is the same pure "greatest `EffectiveFrom ≤ t`" rule over any row type. The aggregate overload delegates to it.
  - Both are pure with no I/O, consistent with §8.12's pure domain services.
  - The aggregate and the read side now share one price-at implementation (U-A11, I-A8); the view's SQL is still a separate expression of the same rule.

AVAILABILITY MECHANISM (first-pass backlog MEDIUM: the interim approximation) — architecturally sound; fail-closed restored
- **It restores the intent of §4.3 and ADR-082.**
  - `ShopOptions.InterimAvailabilityFromPrice` defaults to `false` (`ShopOptions.cs:19`), and `appsettings.json` does not set it.
  - With the flag off:
    - `SkuSellability` returns `OutOfStock` for every id without querying.
    - `GetDueStockAlertsQueryHandler` returns no alerts (`WishlistAndStockAlertQueryHandlers.cs:133-134`).
    - Every read of the view's `in_stock` column applies the flag:
      - the catalogue filter, suggestions and items (`GetCatalogueQueryHandler.cs:106, :163, :207`);
      - the product recommendations (`GetProductRecommendationsQueryHandler.cs:85, :115`);
      - the food recommendations (`GetFoodRecommendationsQueryHandler.cs:61-62`).
  - Detail, purchasability, wishlist, and the C15/C16 command handlers all go through `ISkuAvailabilityReader`. So in production:
    - every SKU is out of stock and not purchasable;
    - a stock-alert request is accepted;
    - no alert is ever due.
  - Only the test factory enables the flag (`ShopApiFactory.cs:29-32`). `WithFailClosedAvailability()` and `ShopFailClosedAvailabilityEndpointTests` exercise the production default.
- **A flag ANDed in the read path is acceptable as the interim.** Making the view itself fail-closed would have taken one of two forbidden routes:
  - editing a committed migration, which persistence-ef-core §9 forbids;
  - adding an eighth migration outside §13/§1.
  The view's `in_stock` stays the price-derived interim expression, and the decision is taken in C#. The cost is that the guard is repeated at about five call sites (MEDIUM below).
- **HO-IMP-001's replacement will be clean.** It will:
  - replace the view's `in_stock` expression and `ApproximateSellableQuantity` with the `v_sellable_stock` join;
  - delete the option and the `interim &&` guards.
  There is no data migration, no stock copy and no contract change.
  - The SKU-level seam is now genuinely single. In the first pass, Read handlers called `SkuSellability` directly; now every caller goes through `ISkuAvailabilityReader`.
- **The recording is correct.**
  - The §1 Amended row and the new ADR-082 bullet say "integrator recommendation, pending owner confirmation".
  - Both state the production default, the Testing switch and the removal path.
  - One older bullet is now stale (LOW below).

FINDINGS
- [MEDIUM] **The fail-closed decision is applied at several call sites, not one.**
  - Sites:
    - `src/Read/CatEcommerce.Read/QueryHandlers/Product/GetCatalogueQueryHandler.cs:28, :106, :163, :207`
    - `GetProductRecommendationsQueryHandler.cs:35, :85, :115`
    - `GetFoodRecommendationsQueryHandler.cs:61-62`
    - `WishlistAndStockAlertQueryHandlers.cs:133`
    - `src/Infrastructure/CatEcommerce.Infrastructure/Product/SkuSellability.cs:33`
  - Each handler reads `IOptions<ShopOptions>` and must remember to AND `storefront_catalogue_entry.in_stock` with it.
  - The column itself still answers with the price-based approximation, so any future reader of the view that forgets the guard fails open. That includes another area, a report, or interim work before HO-IMP-001.
  - Fold the guard into one helper, such as an `IQueryable<StorefrontCatalogueEntry>` extension or `StorefrontAvailability`. Alternatively, have the column's configuration comment and the Amended row state "never read `in_stock` raw".
  - This is not HIGH. Every current reader is guarded, and the guard disappears with HO-IMP-001.
- [LOW] `src/Infrastructure/CatEcommerce.Infrastructure/Configuration/ShopOptions.cs:19` and `src/Api/CatEcommerce.Api/appsettings.json`:
  - The production default exists only as the C# default; `appsettings.json` does not state `"InterimAvailabilityFromPrice": false`.
  - Nothing stops a deployed environment from switching it on.
  - Consider making the value explicit in `appsettings.json`, and, once the owner confirms, refusing it at start outside Development/Testing.
- [LOW] `.ai/decisions/adr.md` ~3101: the older "ADR-082 / ADR-085 (availability, bundles)" bullet still says availability is "the Phase E approximation until HO-IMP-001…", with no mention of the flag. The new bullet above it supersedes it in substance; one clause would remove the ambiguity.
- [LOW] Projecting introduced two edge-case behaviour differences, both for CODE_REVIEW:
  - **Food block.** `food` used to be non-null whenever the owned `FoodDetails` existed. Now it requires at least one food column to be set, and the presence test checks only the analysis protein column (`GetProductDetailQueryHandler.cs:83-85`).
  - **Products with no SKU rows** now answer `null`/`404` instead of an empty `skus` list, in `GetPurchasabilityQueryHandler.cs:30` and `GetReviewEligibilityQueryHandler.cs:19`. The public impact is nil, because an Active product always has an active SKU (ActiveSku blocks publish).
- [LOW] `GetProductDetailQueryHandler.cs:75-76` constructs `Domain.Entities.ProductTranslation` instances on the read side to feed the pure `ProductContentLocalisation.Resolve`. No aggregate is rehydrated and nothing is persisted, but a row overload of `Resolve` (like `PriceAt<T>`) would keep write-model entities out of Read.

FIRST-PASS BACKLOG ROWS 1–11 — status (not re-raised)
- **Addressed, owner confirmation pending:** the interim availability approximation (MEDIUM). Production is now fail-closed. The open decision becomes "confirm the integrator's recommendation".
- **Fixed in c0756c7:**
  - wrong violation codes (LOW). `InvalidProductName`, `InvalidBrand`, `InvalidProductDescription` and `InvalidSkuCode` are now in `BusinessViolationCodes.Shop.cs`, used at `ProductAggregate.cs:248/:256/:264` and `Sku.cs`;
  - `ShopModule` now registers `AddMemoryCache()` itself (LOW).
- **Unchanged and still open:**
  - image URLs always null (MEDIUM);
  - the six unrecorded out-of-scope files (MEDIUM);
  - the `Persistence.Views` namespace gap (MEDIUM);
  - unpaginated "my reviews" and the 200-alert cap (MEDIUM/LOW);
  - the ADR block placement and missing separator (LOW);
  - migration timestamp order (LOW);
  - UTC "today" for cat age (LOW; still at `GetFoodRecommendationsQueryHandler.cs:49` and `GetProductRecommendationsQueryHandler.cs:99`);
  - command-then-query on the stock-alert POST (LOW);
  - stale `.ai/templates` (LOW).

SCOPE CHECK
- **Fix range f24cdc1..HEAD: 31 of 31 files in allowed scope**, excluding the three master test files. Each is one of the following:
  - inside §13:
    - Read `QueryHandlers/Product/**`
    - `Domain/Entities/Sku.cs`
    - `Domain/DomainServices/SkuPricing.cs`
    - `Domain/Aggregates/Product/**`
    - `tests/**`, including the new `ShopFailClosedAvailabilityEndpointTests.cs` and `ShopApiFactory.cs`
    - `.ai/decisions/adr.md` (an ADR-082 note)
  - an ADR-341 per-area partial or module: `BusinessViolationCodes.Shop.cs`, `Modules/ShopModule.cs`;
  - recorded in §1 Amended:
    - `Infrastructure/Product/SkuRows.cs` and `SkuAvailabilityReader.cs` (new row, (2));
    - `Infrastructure/Configuration/ShopOptions.cs` (new row, (1));
  - the §1 Amended row itself: `docs/handover/HO-SHP-001.md`, line 19 only (accepted practice).
- **Files outside allowed scope:** none new. The six from the first pass are unchanged and carried as backlog.
- **§14 is untouched.**
  - No migration was edited or added; the diff of `Persistence/Migrations/**` is empty.
  - No `*.csproj`, `*.sln` or package change.
  - No `web/**`, `.claude/**`, or `.ai/**` other than `adr.md`.
  - No other area's aggregate.

NOTES FOR LATER GATES
- **TEST.**
  - Run against the branch database `catecommerce_shp`.
  - Run `ShopFailClosedAvailabilityEndpointTests` with the flag off. It must cover detail, bundle, list, the `availability=inStock` filter, purchasability, wishlist, alerts (request accepted, nothing due), and both recommendation endpoints.
  - Re-run the full Shop suite with the flag on, to prove the projections preserved every response shape: I-A6, I-B1, I-D*, I-E*, I-F*, I-G*, I-H*.
  - Record I-E11's p95.
- **CODE_REVIEW.** The two edge-case behaviour differences (LOW) and the per-SKU diet-gate calls.
- **ACCEPTANCE.**
  - Under production configuration, A4, A5, A17, A29 and A30 show the fail-closed default. Their positive evidence runs under the Testing-only interim flag; record that caveat.
  - Owner confirmation of the fail-closed recommendation is pending.
