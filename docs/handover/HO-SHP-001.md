# HO-SHP-001 — Shop Catalogue and Products (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-SHP-001** |
| Title | Shop — Catalogue and Products — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §8 — **US-SHP-01 … US-SHP-14** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-SHP-1 … FR-SHP-15**, FR-IMP-8 (the availability input), FR-PHM-9 (the authorisation input), **BR-2**, **NFR-PRF-2**, NFR-LEG-2, NFR-SCL-2, NFR-SCL-3, NFR-SEC-2, NFR-SEC-5, C-4, C-6, C-8, D-2, D-3, UI-4, AC-5, AC-7, §5.1 (Product, SKU), Appendix D ("Product catalogue") |
| Pairs with | [`HO-SHP-002`](HO-SHP-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ADM-001`](HO-ADM-001.md) (tax classes, settings registry, `Money`, `LimitedMarkup`, `[StorefrontEndpoint]`, `ActionRoles` additions), [`HO-NTF-001`](HO-NTF-001.md) (outbox, template catalogue, routing policy, subject guards, link builder), [`HO-ACC-001`](HO-ACC-001.md) (customers, staff, sessions, `CustomerMergedEvent`, `CustomerAccountAnonymisedEvent`, `UniqueConstraintExceptionFilter`, rate limiter), [`HO-CAT-001`](HO-CAT-001.md) (cats — life stage, weight, neuter status, allergies, conditions) |
| Supplies | **HO-IMP-001** (SKUs to receive against, catalogue facts), **HO-ORD-001** (sale facts, prescription eligibility, bundle composition), **HO-PHM-001** (the products a diet authorisation names), **HO-EMR-001 / HO-PHM-001** (non-retail SKUs for clinical stock) — §4.2 and §8.12 state each contract |
| Delivery order | Section 6 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None from `architect`. This document proposes the design and records every decision as an ADR in §9 for the ARCHITECTURE gate to judge |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 - README C-1, C-4.** §8.7 was rewritten: the pushed `SkuStockStatusAggregate` / `RecordSkuStockLevelCommand` are **dropped** in favour of reading HO-IMP-001's published view `inventory.v_sellable_stock` (its ADR-116). ADR-082 is narrowed to the sales-stats copy, so the ADR-012 exception this slice asked the architecture gate to judge no longer exists for stock. The diet port is renamed to HO-PHM-001's `IDietAuthorisationGate` (the fail-closed stub is unchanged). New OQ-21; §20.1 now also covers test seeding. <br><br>**2026-09-23 — README C-5 (file storage).** Product images now go through [`HO-FIL-001`](HO-FIL-001.md): this slice contributes the `ProductImage` purpose and its `FileRules` row and stores `fileId` values. ADR-091's `Media:PublicBaseUrl` + placeholder fallback is withdrawn. OQ-17 closed. <br><br>**2026-09-28 — build-time findings (Phase A).** (1) `docs/handover/CONTRACTS.md`'s contracts pass landed between this handover's writing and its build: `IDietAuthorisationGate` (`CheckAsync`/`GetAuthorisedCatsAsync`, `DietGateResult`, `AuthorisedCat`, `DietGateOutcome`) is already registered by `PharmacyModule` — this slice consumes the existing port and registers nothing for it (§8.9's inline stub instructions are superseded). (2) `ProductImage` stores `FileId` only (no `ContentType`/`SizeInBytes`), matching `CatPhoto`'s precedent (content type/size stay on `StoredFileAggregate`, read at validate time via `IsEligibleForAttach`) — a tighter reading of ADR-091/HO-FIL-001 than §8.3.2's literal field list. (3) `FoodDetails`' `GuaranteedAnalysis` is persisted as four flat nullable columns, not a nested owned type: EF Core cannot materialise an optional owned type that itself owns another optional owned type with no non-null discriminator, and every `FoodDetails` field is genuinely optional. `GuaranteedAnalysis` is exposed as a derived, unmapped property so every other caller is unaffected. (4) `sku_price` has one index (`ix_sku_price_sku_effective`, unique on `(sku_id, effective_from)`), not two: PostgreSQL scans a btree index backwards as cheaply as forwards, so the `(sku_id, effective_from DESC)` access pattern §12.1 names is the same index, not a second one (a second `HasIndex` on the identical column set only collided with the first). (5) `BusinessViolationCodes.ProductVersionConflict` is one code beyond §10.4's list: `IAggregateRootRepository<T>.UpdateAsync(aggregate, expectedVersion, option)` compares `aggregate.Version` at call time, but every mutation method already calls `UpdateVersion()` before the handler reaches that call, so the platform overload can never succeed for an ordinary edit. Every `ExpectedVersion`-carrying handler instead compares the freshly-loaded aggregate's version itself and returns this code; the controller maps it to `409`, the same pattern HO-NTF-001's `TemplateVersionConflict` already established. The 2-arg `UpdateAsync` is used for the actual save (`xmin` remains the real concurrent-write guard, ADR-016). (6) `SettingKeys.Shop.cs` (a partial-file companion to `SettingDefinitions.Shop.cs`, following `SettingKeys.Notifications.cs`'s precedent) and `RecordClassification.Shop.cs` (HO-XCT-001 §5.2 C-ALL-1) are added; neither is named in §13 but both follow its own listed files' established ADR-341 pattern. <br><br>**2026-09-28 — build-time findings (Phase C).** HO-IMP-001 is not a §5 prerequisite of this slice and is not merged in this worktree, so `inventory.v_sellable_stock` (ADR-116) does not exist as a database object yet. Per this build's explicit instruction to keep migrations to `product.*` plus `pg_trgm` only, no stand-in view is created in `inventory.*`. `SkuAvailabilityRules` (the pure section 8.7 rule: a discontinued SKU or a non-active/unlisted product is always out of stock regardless of quantity; the expiry-window rule) and `ProductSalesStatsAggregate`/`RecordProductSalesStatsCommand` (self-contained, ADR-082's narrowed copy) are built and fully tested now. The live join — `StaffSkuViewModel.Stock` and, from Phase E, `storefront_catalogue_entry.in_stock`/`next_allocatable_expiry_on` — is wired to always read as "not reported" (`stock: null`, `in_stock: false`) until HO-IMP-001 lands and the view exists; this is the same value the wire contract already uses for "never pushed" (section 12.4), so no consumer sees a new shape, only the same fail-closed default the handover names in section 4.3 ("every SKU displays out of stock... until Inventory pushes levels"). I-C1..I-C6 (which need HO-IMP-001's `IStockLedger` fixtures per section 16.1 rule 3) cannot be run in this worktree; U-C2/U-C3 (the pure rule) and the sales-stats equivalent of U-C1 are covered. <br><br>**2026-09-28 — build-time findings (Phase E, supersedes the Phase C row's `in_stock` value).** The Phase C row above says the storefront's `in_stock` would be wired to always read `false` ("not reported") until HO-IMP-001 lands. Building the `storefront_catalogue_entry` view (section 12.1 Phase E) showed that literal default would make the catalogue's availability filter and every U-E/I-E test scoped to it vacuous — no product could ever be found "in stock", including ones with a published price and an active SKU. Instead `in_stock` is computed in the view as "has at least one Active SKU with a current (`effective_from <= now()`) price" — still a documented, temporary approximation (it ignores actual quantity, exactly like section 4.3's fail-closed intent for the *quantity* dimension), but it lets the availability filter, sort and the catalogue's other fields (price, rating, popularity, search) behave correctly for every SKU this slice can create, rather than reporting every product out of stock. `next_allocatable_expiry_on` is not populated (stays out of scope, unchanged from the Phase C row: HO-IMP-001 owns expiry allocation). `StaffSkuViewModel` in `StaffProductDetailViewModel` carries no `Stock` field at all yet (rather than a `stock: null` placeholder) since no consumer of the staff detail contract was built against one in this worktree; adding it is left to whichever slice wires `IStockLedger`/`inventory.v_sellable_stock`. See `src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20260928032055_AddStorefrontCatalogue.cs` for the SQL and this reasoning in code. <br><br>**2026-10-04 — build-time findings (Phase F).** (1) **The diet port is HO-PHM-001's, not §8.9's draft.** The registered `IDietAuthorisationGate` (ADR-149) is `CheckAsync(customerId, catId, skuId)` → `DietGateResult` and `GetAuthorisedCatsAsync(customerId, skuId)` → `AuthorisedCat[]`, evaluated "today" on every call; §8.9's `GetValidAsync` / `DietAuthorisationFact` and `PrescriptionPurchaseGate.Evaluate(…, authorisations, customerCats, asOfLocalDate)` do not exist. `PrescriptionPurchaseGate` (pure, built) therefore owns what is still Shop's — the non-prescription pass-through, the guest rule (`PrescriptionSignInRequired`) and the mapping of a gate outcome to `PrescriptionAuthorisationRequired` — and the gate owns expiry, liveness and ownership. §15 U-F1's rows "valid yesterday / deceased cat / cat now owned by someone else / another variant" are the gate's own rules: they are proved in Shop as the outcome mapping (`U_F1_GateOutcomeThatIsNotAnAuthorisation…`, every non-`Authorised` outcome refused) and end to end by I-F2/I-F3 against `ControllableDietAuthorisationGate`, a test double that applies those four rules over the real `clinic.cat` rows. The shipped fail-closed stub is `PharmacyModule`'s `DenyAllDietAuthorisationGate` (I-F8 runs on the base factory against it); `Product/NoDietAuthorisationsReader.cs` (§12.2/§13) is **not built**. (2) `CheckPrescriptionEligibilityQuery` has **no `AsOf`** (§8.9): the port takes none. I-F3's "the day after `ValidTo`" is driven by the controllable gate's own clock, which is how the real gate behaves at checkout. The query has no HTTP route (contract query dispatched in-process by HO-ORD-001). (3) **`AddBundlesAndRelated` is the catalogue view alone.** `bundle_component`, `related_product` (and `product_translation`) are owned collections of `ProductAggregate` and were created with it by Phase A's `AddProductCatalogue`, so the model snapshot is unchanged and Phase F's migration only re-creates `product.storefront_catalogue_entry` with the bundle clause (a bundle is in stock only when its own SKU is on sale, it has components and every component is Active, on an Active listed product, and priced now). The three tables are classified in `RecordClassification.Shop.cs`. (4) **One availability seam, `Read/Support/SkuSellability`.** The detail page, purchasability, recommendations and the bundle clause of the view all answer "sellable now" from the Phase E approximation (Active SKU, Active listed product, price effective now), and a bundle through the existing `SkuAvailabilityRules.EvaluateBundle` over its components — so when HO-IMP-001 lands only `ApproximateSellableQuantity` and the view's clause change. This file is outside §13's list (a new `Support` folder in `src/Read`). **I-F5 as written (stock A 5, B 1 → bundle; B 0 → bundle) cannot run** — it needs `IStockLedger` fixtures (§16.1 rule 3); its quantity arithmetic is proved by U-F2 (`floor(1÷2) = 0`), and the wiring by `I_F5_AnyComponentUnavailable…`, which uses a test-only fixture (`SetSkuOutOfStockAsync`/`SetSkuInStockAsync`) that moves the SKU's price effectivity — the one state the interim approximation reads. No production code path and no Testing endpoint sets stock. (5) `GetSkuSaleFactsQuery` and `GetSkuCatalogueFactsQuery` (§8.12/§12.6) are built in Phase F although no phase names them; both are in-process contract queries. (6) `CatLifeStageDerivation` (pure, `Domain.DomainServices`) is added for the "recommended for your cat's life stage" block (§8.10) and is the §8.13 rule-2 derivation Phase H reuses; it is not in §13's list. (7) `ProductDetailViewModel` gains `bundle`; `StaffProductDetailViewModel` gains `bundleComponents` (each flagged `isOnSale`) and `relatedProducts`, and its publication checklist now receives the bundle component facts. <br><br>**2026-10-04 — build-time findings (Phase G).** (1) **The availability seam moved to `Infrastructure/Product`.** Phase F's note (4) placed `SkuSellability` in `src/Read/Support`; command handlers (C15 request, C16 notify) cannot reach `Read`, so it now lives in `Infrastructure/Product/SkuSellability.cs` behind a new Domain port `ISkuAvailabilityReader` (`SkuAvailabilityReader`, Scoped) that handlers use, while read-side handlers call `SkuSellability` directly — still the one place that changes when HO-IMP-001 lands. Neither the port nor `IShopCustomerDataFinder` (find-by-customer / find-pending-alerts-by-SKU for the merge, anonymisation and discontinuation handlers, the `IWaitlistEntryFinder` precedent) is in §13's list. (2) `BusinessViolationCodes.InvalidStockAlertTransition` is one code beyond §10.4's list (U-G2: "the second `MarkNotified` is refused" needs a code; the review codes do not fit). `PurchaseNotPermitted` is also returned for an alert on a SKU that is not on sale (no other code fits), `SkuNotFound` maps to `404` on the wishlist and alert routes, and cancelling an alert that is no longer Pending is a no-op success (Notified is terminal history). (3) `StockAlertGuard` applies exactly §8.11's rule (Notified; SKU Active; product Active and listed) and **no owner check**: a merge between enqueue and delivery moves the alert to the survivor while the request keeps the pre-merge recipient as history. (4) **Stock for I-G2..I-G5.** "Push stock" / "push 0 then 5" is the test-only interim fixture (`SetSkuInStockAsync` / `SetSkuOutOfStockAsync`, Phase F note (4)); the worker and the request handler read availability through the same seam as the storefront, so the once-only, sell-out-and-restock and consent cases are real, only the source of "available" is the interim approximation. (5) `ShopWorkCycle` (`GetDueStockAlertsQuery`: SQL prefilter by the standard sellability rule, bundles settled by `SkuSellability`) dispatches `NotifyStockAlertCommand` per alert in its own scope; the handler enqueues `shop.back-in-stock` before its save with deduplication key `shop:back-in-stock:{alertId}`. The template's `variantLabel` is "Standard" for an axis-less SKU (otherwise "Royal Canin (Royal Canin)"). `GetStockAlertDeliverabilityQuery` runs `NotificationRoutingPolicy.Plan` against the **shipped** catalogue definition (`INotificationTemplateCatalogue.Find`), not a staff-customised template. (6) The merge handler writes one `BusinessEvent` audit row (`ShopDataReassigned`, counts of moved / dropped / closed / withdrawn) when it changed anything; `ErasureExemptions.Shop.cs` (HO-XCT-001 C-ALL-2: Shop holds customer ids only), `RecordClassification.Shop.cs` rows for `wishlist` / `wishlist_item` / `stock_alert`, `ShopActors.Worker` and the `Shop` section of `appsettings.json` (`Media` is withdrawn, README C-5; `Localisation` arrives with Phase H) are added — none named in §13. `NotificationLinkBuilder.ProductLink` URL-escapes the slug (an identity for a valid slug), and `ClinicNotificationsTests`' private `StubLinkBuilder` gains the new interface method. (7) **Cross-cutting §15 tests written now** (none existed for Phases A–E): U-X1 (reflection over `Events/Product`; the allow-list is the three short key codes `ProductTranslationChangedEvent.Locale` and `SourcingOriginCreated/UpdatedEvent.CountryCode`, never free text), U-X2 (`ShopWorkerTests`), U-X4 (`ShopCrossCuttingTests`). The Shop test factory gains Capturing senders, `PublicSite:WebBaseUrl` and a daytime-clock variant so the delivery cases run the real HO-NTF-001 pipeline; the classes that run delivery passes carry `[Collection("Notifications")]` and loop-until-own-row. <br><br>**2026-10-04 — build-time findings (Phase H and the evidence pass).** (1) **No `AddProductTranslations` migration.** `product_translation` (like `bundle_component` and `related_product`) is an owned collection of `ProductAggregate` and was created by Phase A's `AddProductCatalogue`; the Phase H settings are rows (§10.2) and were registered with the other Shop definitions in Phase A; `dotnet ef migrations has-pending-model-changes` reports none. §13's eighth migration is therefore not created — the slice has seven migrations, `AddProductCatalogue` … `AddWishlistAndStockAlerts`. (2) **`IContentLocaleSettings`** (Domain port; `ConfiguredContentLocales`, Singleton, in `Infrastructure/Configuration` beside `LocalisationOptions`) replaces §11.4 C17's "handler injects `IOptions<LocalisationOptions>`": Application cannot reference Infrastructure's options type. `LocalisationOptions.ContentLocales` starts **empty** because the configuration binder appends to an initialised list (a `["en"]` initialiser turned the committed `["en"]` into `["en","en"]` and failed validation); `appsettings.json` carries the shipped default, the validator allows at most five locales (the default plus the four translations a product holds) and a missing section fails at start. `BusinessViolationCodes.InvalidTranslationContent` is one code beyond §10.4's list (length / control-character failures); markup failures surface `LimitedMarkup`'s own code. (3) **`FoodRecommendationPolicy` decisions** (§8.13 left them open): a product with no reason is not a recommendation; `excludedForAllergy` counts only products that would otherwise have been recommended; ranking is reasons desc, units sold desc, name, then product id; the candidate pool is the first 2,000 in-stock food products by popularity, pre-filtered in SQL by life stage when the cat's is known (the policy needs each candidate's reasons to rank, so it cannot be a SQL top-N); the gate's `CheckAsync` is asked per active SKU of each prescription candidate for THIS cat. §15 U-H2's fourth row ("`Fish oil` against `fish`") is read as the allergen `fish` against the ingredient list "Fish oil" — the §8.13 token rule makes the reverse a non-match. In the shared database, I-H1/I-H2 assert on the test's own products (units sold grow with the clock so a newer run outranks older runs' products; the allergy count is asserted `>= 1`, the exact count by U-H2). (4) `?locale=` is honoured by every public read that shows a name — detail, catalogue list and suggestions, recommendations and food recommendations (`GetProductRecommendationsQuery` gained `Locale`) — and `GET /api/shop/locales` and the staff detail's `translations` are added. (5) **Defects found while writing the missing §16 evidence for Phases A–E, fixed:** `GET /api/shop/filters` answered `500` (EF cannot order by a member of a positional record built in the projection) and its `brands` facet was `{ value, count }` where §12.4 says `{ name, count }`; the facet cache TTL is now `Shop:Catalogue:FacetCacheSeconds` instead of a literal. The `catalogue` rate limit is read from `ShopOptions` (`Shop:Catalogue:PermitLimit` / `WindowMinutes`, default 120 per minute) at request time rather than a literal, because I-E10 needs to lower it and I-E11 to raise it. (6) **Tests written now for rows that cited tests that did not exist:** I-E5 (filters half), I-E9, I-E10, I-E11 (`Category=Performance`, opt-in), I-E12, U-X1, U-X2, U-X3, U-X4, U-X5, I-X1, I-X2. I-E11's 20,000-SKU fixture (brand `zz Perf Fixture`, 5,000 products x 4 SKUs, seeded idempotently through the aggregates and repositories) lives in this branch's own `catecommerce_shp` database, **not** the shared `catecommerce_dev` (OQ-15): the integrator seeds the dev database by running the Performance category there. (7) **Cross-area test isolation:** HO-ADM-001's `MaintenanceMode_NeverBlocksAdminApiOrStaffSignIn` (in `AdminContentPolicyEndpointTests`) and `AdminLogCaptureEndpointTests` flip the GLOBAL maintenance setting in the shared database, and a `[StorefrontEndpoint]` answers `503` meanwhile — any storefront test running in parallel flaked. Both classes now carry `[Collection(AdministrationGlobalSettingsCollection.Name)]` (the existing serialised collection); no assertion changed. (8) **Pre-existing, not this slice:** `AdminRosterLeaveEndpointTests.ScheduleNextPattern_AdvancePastEffectiveDate_NotPromotedByABareRead_PromotedByTheNextCommand` and `CareReminderEndpointTests.ReminderWindowBoundaries_15DaysOutIsExcluded_DueTodayIsIncluded_OverdueByOneIsExcluded` fail on pristine `master` (`d2d82d6`, fresh database) between 18:00 and 24:00 UTC: each compares a UTC calendar date in the test with the Asia/Dhaka date the code uses, which differ in that window. <br><br>**2026-10-04 — ARCHITECTURE gate first pass fixes (integrator recommendation on availability, **confirmed by the owner 2026-10-04**).** (1) **Availability is fail-closed in production, per §4.3 / ADR-082** — this supersedes the Phase C/E/F/G notes that made "an Active SKU with a current price" read as in stock everywhere. `Shop:InterimAvailabilityFromPrice` (`ShopOptions.InterimAvailabilityFromPrice`, default **false**) gates the price-based approximation; only the Testing environment turns it on (`ShopApiFactory`), so the catalogue and availability tests stay meaningful. With it off every SKU reads out of stock and not purchasable, the `availability=inStock` filter matches nothing, no recommendation block has in-stock candidates, and no stock alert is ever due. Mechanism, chosen for one seam and no migration: the view `product.storefront_catalogue_entry` keeps computing `in_stock` in SQL as the *interim price-derived rule* (no migration edit); every read that uses that column ANDs it with the setting (`interim && e.InStock`, a constant when off), and the SKU-level seam (`SkuSellability`, `ISkuAvailabilityReader`) short-circuits to OutOfStock without a query. When HO-IMP-001's `inventory.v_sellable_stock` lands, the view's expression and `ApproximateSellableQuantity` are replaced by the stock join and the flag is deleted. A test (`ShopFailClosedAvailabilityEndpointTests`) proves the default on detail, bundle, list, filter, purchasability, wishlist, alerts, recommendations and food recommendations. (2) **Read side projects instead of rehydrating** (`.ai/read-side-pattern.md` §4 rules 1-2, ADR-012): every handler listed in the gate record now reads column projections — `SkuRow` / `BundleComponentRow` / `PriceRow` (`Infrastructure/Product/SkuRows.cs`, built with `SelectMany` over the owned tables so SKU lookups are one join, never an aggregate graph), anonymous projections for the product, reviews, wishlist, alerts, origins and the cat (allergies and conditions as projected string lists). `SkuSellability` is three small queries however many SKUs are asked about. The one deliberate aggregate read is `GetStaffProductDetailQueryHandler`'s root load, because ADR-095 has the staff read share `ProductPublicationRules.Evaluate(product, facts)`, which takes the aggregate (commented in code). Public signatures of the contract queries and every response shape are unchanged; `Sku.LabelFor` and `SkuPricing.PriceAt<T>` apply the same label and price-at rules to projected rows. (3) `BusinessViolationCodes` gains `InvalidProductName`, `InvalidBrand`, `InvalidProductDescription` and `InvalidSkuCode` (the aggregate used `InvalidSlug` for name, brand, description and SKU-code failures); `ShopModule` registers `AddMemoryCache()` itself (the filters facet cache). <br><br>**2026-10-04 — SECURITY gate first pass fix (prescription diet through a bundle, OQ-14 / ADR-085 / BR-2; owner-delegated decision: both fixes).** (a) `ProductAggregate.SetPrescriptionRequired` now refuses to turn the flag ON while a SKU of the product is a component of a non-archived bundle (`BusinessViolationCodes.PrescriptionProductInBundle`, one code beyond §10.4's list; `400` naming the bundle(s)); turning it off is always allowed. The handler supplies the bundle names through a new Domain port `IBundleMembershipReader` (a projection over the owned `bundle_component` rows; outside §13's list). (b) Defence in depth for legacy data or a race past (a): a bundle with ANY component whose product currently requires a prescription is not prescription-eligible (`CheckPrescriptionEligibilityQuery` returns `PrescriptionAuthorisationRequired` without asking the gate), not purchasable (`GetPurchasabilityQuery`), reported `prescriptionRequired: true` by `GetSkuSaleFactsQuery`, and out of stock in `SkuSellability` — all from one projection join (`ProductProjections.BundlesWithPrescriptionComponentAsync`), no aggregate loads. Tests: `ShopBundlePrescriptionEndpointTests` (refusal naming the bundle, archived bundle no longer blocks, state forced by SQL then every read refuses, unflagging restores the kit) and a domain unit test. Also: the filters facet cache is keyed on the parsed category (junk input resolves to the all-categories key and cannot grow the cache), with a test. <br><br>**2026-10-04 — I-E11 investigation (view cost; no budget change).** I-E11 read 518-568 ms p95 on a slower machine/Docker state (earlier 381-405 ms on the same code). `EXPLAIN (ANALYZE, BUFFERS)` of the slowest shape (brand + q + price range + `availability=inStock` + `sort=relevance` + page 2) showed ~210 ms of pure execution per query in Postgres (buffer hits only, no I/O; the handler issues a count and a page query, each evaluating the view over all 5,000 matching products, ~370 ms combined of a ~400 ms request); the trigram index is not the cost (the fixture's search text matches every row), nor is the maintenance check (3 ms). The view's per-product work was the cost: a correlated `LATERAL` price probe per SKU (about 75k buffer hits) plus a second per-product `EXISTS` for `in_stock` (about 25k). Both are changed inside this branch's own unmerged `AddBundlesAndRelated` migration (the view is re-created there; `AddStorefrontCatalogue` is untouched): (1) the current price per SKU (greatest `effective_from <= now()`, the `SkuPricing` rule) is computed once, set-based, with `DISTINCT ON (sku_id)` and joined by product, and (2) a standard product's `in_stock` is `pricing.min_price IS NOT NULL` (an Active SKU with a current price is exactly a non-null aggregate), dropping the duplicate `EXISTS`; semantics are unchanged. Trade-off: the set-based price join has a fixed cost (about 70 ms) even for very selective queries, against about 9 ms before, which is acceptable for the p95 cases it fixes. Measured on the same fixture, 3 runs each: before p95 523.5 / 518.5 / 567.8 ms (p50 about 265-315); after p95 344.4 / 342.9 / 345.9 ms (p50 about 222-234). The 500 ms budget, the 20,000-SKU fixture and the test are unchanged. Test note: the generated mix sometimes repeats `minPrice`/`maxPrice` parameters (the handler uses the first); harmless and left as is. <br><br>**2026-10-04 — CODE_REVIEW gate first pass fixes (health-safety rules are the integrator's decision, **confirmed by the owner 2026-10-04**).** (1) **HIGH:** `FoodRecommendationPolicy` built its allergy dictionary with `ToDictionary(..., OrdinalIgnoreCase)`, which threw for a cat holding "Chicken" and "chicken" (or the same allergen twice) — a 500 for exactly the allergic cats. Allergens are now trimmed and de-duplicated case-insensitively in the policy (nothing upstream does it); U-H2 rows and an HTTP test cover it. (2) **Fail-closed allergen rules, stricter than §8.13 as written:** (a) for a cat with ANY recorded allergen, a product whose ingredient list is unknown (empty) is excluded, with the hint `IngredientsUnknownExcluded` — cats with no allergen are unaffected; (b) a food bundle has no list of its own, so its exclusion is evaluated over the UNION of its components' lists (projections: component rows joined to SKU rows and ingredient columns), a food component with an unknown list makes the whole bundle unknown, and a non-food component carries no ingredients and is ignored; (c) plural matching is symmetric — two tokens are the same word when equal or when one is the other plus `s`/`es` ("Eggs"/"egg", "Peas"/"pea", "Oats"/"oat", and the reverse; "chickpea" still is not "chicken"). This supersedes §8.13's documented one-way `s`/`es` rule; `RecommendationCandidate` gains an optional `IngredientsUnknown`. (3) **Price filter is §12.4's rule:** a product matches when ANY active SKU's CURRENT price (the greatest `effective_from <= now`) is within `[minPrice, maxPrice]`, via a correlated per-SKU subquery; the old overlap test and the null passes are gone, so SKUs at 500 and 5000 no longer match 1000-1500 and a product with no current price never matches once a bound is given (it still lists with no bound). A two-SKU test covers it. (4) Culture: `decimal.TryParse` for `minPrice`/`maxPrice` and the `F2` renderings use `CultureInfo.InvariantCulture`. <br><br>**2026-10-04 - TEST gate follow-up: the 9 BLOCKED section 16 cases now have tests (U-A5b, U-B2, U-E3, I-A6, I-A8, I-A12, I-E2, I-E3, R3), by integrator decision before ACCEPTANCE.** (1) **Real bug found and fixed at the root: `FilePurpose.ProductImage` had no staff action in `FilePurposeAccess`.** The fail-closed policy (HO-FIL-001 SECURITY MEDIUM) refuses every purpose with no mapping, and this handover consumes `ProductImage` files (`SetProductImagesCommandHandler`) without ever naming its action, so in the real host no staff role could upload a product image at all; HO-FIL-001's own tests hid it behind the test-only `UnownedPurposeAllowingPolicy`. `ProductImage` now maps to `Actions.Shop.ProductsManage` (ShopManager, Administrator), the action that governs the write that attaches the file; customers still may not upload it. `FilePurposeAccessTests` (HO-FIL-001) previously listed `ProductImage` among the unmapped purposes; it now asserts the mapping and that customers are refused, and the other six purposes are unchanged. (2) Test seams: `ShopApiFactory` gains an isolated file store and signing key (the `CatApiFactory` pattern) so tests upload real PNGs; `CreateProductAsync` gains an optional `subcategory`. (3) Traceability: Domain `ProductSection16GapTests` (U-A5b StoredFile refuses `image/gif` for `ProductImage`; U-B2 value-object and aggregate; R3 the four codes `InvalidProductName`, `InvalidBrand`, `InvalidProductDescription`, `InvalidSkuCode`) and integration `ShopSection16GapEndpointTests`, each test named for its id. Over HTTP only `InvalidSkuCode` reaches the aggregate (the command validators refuse empty and over-long name, brand and description first, under their own codes), so the other three are pinned at the aggregate and the HTTP test asserts only that none of them comes back as `InvalidSlug`. U-B2's "all or none" is the request contract (`guaranteedAnalysis` has four required values; three of four is a 400). (4) `Search_NoResults_ReturnsSuggestions` (backlog row 38): the near-miss query now also carries a brand filter no product has, so `totalCount` is 0 by construction and the `didYouMean` assertion is unconditional (it must contain the product's brand). No production behaviour changed other than (1). <br><br>**2026-10-04 — Deferral of stock-dependent evidence to HO-IMP-001, and two late declarations (integrator under owner delegation, **confirmed by the owner 2026-10-04**; ACCEPTANCE first pass).** (1) **Deferred criteria:** §18 A4 (the "displayed stock" clause only), A5, A17 (display half) and A18. Their only evidence (U-C1, U-C4, I-C1…I-C6, I-F5 as written) needs `inventory.v_sellable_stock` / `IStockLedger`, and HO-IMP-001 requires HO-SHP-001 merged first, so they cannot be evidenced before this merge. **Until then** production is fail-closed: every SKU is OutOfStock and not purchasable and `expiryNotice` is absent (`ShopFailClosedAvailabilityEndpointTests`). **Owner:** the project owner. **Trigger:** HO-IMP-001 merged to master. **Who does the swap:** an HO-SHP-001 follow-up slice that the integrator schedules on that trigger (HO-IMP-001's §14 forbids it editing Shop files, so it is not the owner). On the trigger that slice: replaces the `in_stock` expression of `product.storefront_catalogue_entry` and `SkuSellability.ApproximateSellableQuantity` with the `v_sellable_stock` join; populates `next_allocatable_expiry_on` and wires `SkuAvailabilityRules.ExpiryNotice` into the product detail (`GetProductDetailQueryHandler.cs:70` passes `null` today); adds the staff SKU `stock` field (I-C4); deletes `Shop:InterimAvailabilityFromPrice` and its guards; and runs U-C4, I-C1…I-C6, I-F5 as written and I-G3 against Inventory's own fixtures. That slice's ACCEPTANCE re-judges A4/A5/A17/A18. Tracked as `docs/backlog/SHP.md` row 51. (2) **Declared late — image URLs:** public, wishlist and catalogue reads return `url: null` for every product image; HO-FIL-001 signed-URL wiring (amended ADR-091 / README C-5) is not built in this slice (backlog row 2). (3) **Declared late — six files outside §13:** `RateLimiting/ShopRateLimitPolicies.cs`, `Shared/Common/Helpers/Gtin.cs`, `Read/ViewModels/ProductMoneyExtensions.cs`, and `Infrastructure/Persistence/Views/{StorefrontCatalogueEntry,ProductRatingSummaryRow,CatalogueTermRow}.cs` — each a direct consequence of an in-scope requirement (catalogue rate limit, GTIN validation, money display, the storefront/rating/term views); backlog row 3. <br><br>**2026-10-04 — owner confirmation.** The owner confirmed all four integrator decisions recorded above under the 2026-10-04 delegation: (1) fail-closed availability by default, with the price-based approximation behind `Shop:InterimAvailabilityFromPrice` for Testing only; (2) a prescription diet cannot be flagged while it is a bundle component, with defence in depth on every sell path; (3) the stricter allergen rules (unknown ingredients excluded for allergic cats, the bundle ingredient union, symmetric plurals), superseding §8.13's one-way plural rule; (4) §18 A4/A5/A17/A18 deferred to HO-IMP-001, with the HO-SHP-001 follow-up slice owed on HO-IMP-001's merge (backlog SHP row 51). |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Give the business a **product catalogue it can sell from**, and give customers a storefront they can
**find things in**: browse by category and by country of origin, filter and sort, search with typos,
see real availability and — for imported food — origin, ingredients, analysis and the expiry of the
stock they would receive.

This is the area the brief's second and third lines stand on ("sell cat food along with litter pots
and cat bags"; "food sourced from different countries"). It is also the foundation of the next
area. Imports & Inventory (HO-IMP-001) receives batches **against SKUs defined here**, and
Consultation and Pharmacy deduct stock from those batches. That is why the delivery order puts this
section sixth, ahead of the clinical areas.

Five things this slice is careful about:

1. **Shop never decides stock.** Sellable stock (FR-IMP-8) is computed by Inventory from its ledger.
   Shop displays a copy that Inventory pushes to it. The copy is never editable, and checkout
   re-checks against Inventory. D-3 stays true (ADR-082).
2. **Prescription diets fail closed.** Until Pharmacy (HO-PHM-001) exists, no therapeutic product is
   purchasable by anyone. The gate is one pure function, and the order area calls the same contract
   at add-to-cart and at checkout (BR-2, ADR-084).
3. **Search meets its budget in the database.** One SQL query over a storefront view, with
   `pg_trgm` for misspellings. A performance test with 20,000 SKUs proves NFR-PRF-2 (ADR-083).
4. **Prices are history, not a column.** SKU prices follow HO-ADM-001 ADR-049: they are
   append-only, effective-dated and never back-dated, and every consumer snapshots them (ADR-080).
5. **Contracts before consumers.** Inventory, Orders and Pharmacy do not exist yet. Each seam is a
   named port, command or query with a stated shape and a safe default. The areas that own them
   fill them in by an Amended row, never by editing this slice's code behind its back (§8.12).

§17 builds the slice in **eight phases**. Each phase compiles, migrates and passes its own tests
before the next begins.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-SHP-01 | M | 8 | Products with name, brand, description, images, category, tags, status (draft / active / archived) and SEO slug; the FR-SHP-1 two-level category taxonomy; drafts invisible on the storefront | A |
| US-SHP-02 | M | 8 | Variants (SKUs) by size/weight, flavour, colour and pack quantity, each with its own code, barcode, **effective-dated price**, images and displayed stock. An out-of-stock variant is unavailable while the others stay purchasable | A, C |
| US-SHP-03 | M | 8 | Food details: country of origin, manufacturer, life stage, protein source, ingredient list, guaranteed analysis, feeding guide, storage instructions; prescription-required flag; **a food product cannot be published without a country of origin** | B |
| US-SHP-07 | M | 5 | Displayed availability comes from Inventory's sellable figure (pushed, never computed here). The expiry of the batch that would be allocated is shown when it falls inside `shop.expiryDisplayWindowDays` | C |
| US-SHP-09 | S | 8 | Reviews and ratings from verified buyers only, published after moderation; rating summary | D |
| US-SHP-04 | M | 8 | Filters (country of origin, brand, category, life stage, protein source, price range, availability, rating) and sorts (price, popularity, newest, rating) that combine; **≤ 500 ms p95 at 20,000 SKUs** | E |
| US-SHP-05 | M | 8 | "Shop by Country": each sourced origin with its flag code, sourcing notes and product count; selecting one keeps every other filter available | E |
| US-SHP-06 | M | 8 | Forgiving search over name, brand, ingredient and tag; misspellings tolerated; no-result responses carry "did you mean" terms and popular products | E |
| US-SHP-10 | M | 8 | Prescription products visible but not purchasable without a valid authorisation, with the reason; eligible cats listed; the eligibility contract HO-ORD-001 calls at add-to-cart and checkout | F |
| US-SHP-12 | S | 8 | Bundles priced as a unit, composed of component SKUs; unavailable when any component is; the composition contract HO-ORD-001 and HO-IMP-001 use to deduct each component | F |
| US-SHP-08 | S | 5 | "Frequently bought together" and up-sell blocks curated by the shop manager; "recommended for your cat's life stage" computed; out-of-stock and prescription-gated items the customer cannot buy excluded | F |
| US-SHP-13 | S | 5 | Wishlist per SKU; back-in-stock alert per SKU, notified **once** when sellable stock exists | G |
| US-SHP-11 | C | 8 | Food recommendations for one of the customer's cats from life stage, weight, neuter status and conditions, each with its reason; products whose ingredients match a recorded allergy excluded | H |
| US-SHP-14 | C | 8 | Product content (name, description, ingredients, feeding guide) per enabled language, with per-field fallback to the default language; staff edit each language | H |

Also delivered, from HO-ADM-001's contracts: storefront endpoints refuse work in **maintenance mode**
(HO-ADM-001 §4.2, US-ADM-08 — the server half of "the storefront shows a notice").

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409` | ADR-011, ADR-016 |
| No `CommandService` layer; handlers load aggregates; per-call `SaveChangesAsync` | ADR-015, ADR-021 |
| No projections; query handlers read the tables; heavy reads through a view created by a migration | ADR-012, `.ai/read-side-pattern.md` §5 |
| Money is `decimal(12,2)` + ISO-4217 code, default `BDT`, amount serialised as a string | HO-ADM-001 ADR-054, §12.4 |
| Prices and tax rates are append-only and effective-dated; consumers snapshot at transaction time | HO-ADM-001 ADR-049 |
| Tax classes are HO-ADM-001's `TaxClassAggregate`; a product references one by id | HO-ADM-001 §8.6 |
| Content is `LimitedMarkup`, never HTML | HO-ADM-001 ADR-052 |
| Every endpoint declares its access; `ActionRoles` is the one table | HO-ADM-001 ADR-043, HO-ACC-001 ADR-036 |
| `[StorefrontEndpoint]` + `MaintenanceModeFilter`; the back office is never gated | HO-ADM-001 ADR-053 |
| Messages only through `INotificationOutbox.Enqueue`, in the command handler before the save; never from an event handler | HO-NTF-001 ADR-056, ADR-057 |
| **Back-in-stock is a Marketing template** and inherits the unsubscribe rule | HO-NTF-001 §4.2 — consequence recorded in OQ-2 |
| `CustomerMergedEvent` binds every context that holds a customer id | HO-ACC-001 ADR-039 |
| Object storage is not provisioned; images are a validated storage key + metadata | HO-CAT-001 §4.2 |
| **No payment gateway in v1** — nothing here takes or records payment | SRS §1.2, A-2 |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-SHP-002`](HO-SHP-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-SHP-04 "filters persist" when I sort | The list endpoint accepts every filter with every sort and echoes `appliedFilters`; persistence across navigation is the URL's (HO-SHP-002) |
| US-SHP-05 "listed with its flag" | `countryCode` (ISO-3166 alpha-2) and `countryName` per origin; the flag image is the client's |
| US-SHP-06 "suggested rather than an empty page" | `suggestions.didYouMean[]` and `suggestions.popular[]` on a zero-result search |
| US-SHP-10 "with the reason and a link to book a consultation" | `purchaseBlockReason` per SKU; the link target is HO-APT-002's `/book?category=Consultation` |
| US-SHP-13 "I am notified" | The notification is enqueued; `deliverability` on the alert response tells the client whether any channel will carry it (OQ-2) |
| US-SHP-14 "shown in that language" | `?locale=` on every public read; `fieldLocales` says which language each field came from |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Story / AC | Where it lands | What this slice delivers |
|---|---|---|---|
| **Computing sellable stock**: excluding expired, quarantined, damaged and reserved quantities; FEFO head batch | US-SHP-07 AC1 (the exclusion rule), US-IMP-08 | **HO-IMP-001** | **`inventory.v_sellable_stock`** (ADR-116, README C-1) — Shop joins the view; a SKU with no row is out of stock |
| Selecting the cat at add-to-cart and recording it on the order line | US-SHP-10 AC2 | **HO-ORD-001 / HO-ORD-002** | `CheckPrescriptionEligibilityQuery` and `eligibleCats[]` on purchasability (§8.9) |
| Blocking a checkout line whose authorisation expired after add-to-cart | US-SHP-10 AC3 | **HO-ORD-001** | The same query, evaluated at the checkout instant (§8.9) |
| Issuing and expiring diet authorisations | FR-PHM-9, US-PHM-08 | **HO-PHM-001** | `IDietAuthorisationGate` port with a **fail-closed** stub (§8.9, ADR-084) |
| Deducting each bundle component from stock | US-SHP-12 AC1 (second half) | **HO-ORD-001, HO-IMP-001** | `GetBundleCompositionQuery` (§8.10) |
| "Purchased the SKU" for review eligibility | US-SHP-09 AC1, AC2 | **HO-ORD-001** | `IPurchaseHistoryReader` port with a stub that knows no purchases (§8.11) |
| Popularity figures (units sold) | US-SHP-04 AC2 "popularity" | **HO-ORD-001** | `RecordProductSalesStatsCommand` — the inbound contract (§8.7). With no figures, popularity ties break by newest |
| Co-purchase "frequently bought together" computed from orders | US-SHP-08 | HO-ORD-001 / HO-RPT-001 | Curated links now (OQ-7) |
| Storage/temperature requirements per product | FR-IMP-16 | HO-IMP-001 | Nothing. `StorageInstructions` (FR-SHP-4 text) is here; HO-IMP-001 may add structured requirements by an Amended row |
| Revenue by country of origin | US-RPT-03 | HO-RPT-001 | `CountryOfOriginCode` on the product, readable by SKU |
| Cart, checkout, POS scanning | US-ORD-* | HO-ORD-001 | `GetSkuSaleFactsQuery`, `FindSkuByBarcodeQuery` (§8.12) |
| Portal dashboard, re-order | FR-POR-* | HO-POR-001 | Nothing |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No Inventory** (HO-IMP-001 not built) | Every SKU displays **out of stock** in every environment until Inventory pushes levels. Tests push levels through the inbound command exactly as Inventory will (§16.1) |
| **No Pharmacy** (HO-PHM-001 not built) | No prescription product is purchasable by anyone. The storefront says why. Tests use a controllable reader double (§16.1) |
| **No Orders** (HO-ORD-001 not built) | Nobody can post a review, because no purchase exists. Popularity is flat. Tests use a controllable purchase-history double |
| ~~No object storage or upload path~~ **— resolved 2026-09-23** | [`HO-FIL-001`](HO-FIL-001.md) provides it (GCS, ADR-318; ClamAV, ADR-319). This slice adds the `ProductImage` purpose and its allow-list row and stores `fileId` per image; serving is HO-FIL-001's signed URL. **ADR-091 is amended**: no `Media:PublicBaseUrl`, no placeholder-for-missing-storage path |
| **No SMS or email provider** (HO-NTF-001 OQ-1) | Back-in-stock alerts reach the in-app inbox only, and only for customers with in-app marketing consent (OQ-2) |
| **Source countries and import-licence constraints** (SRS Q-8) | Origins are data (`SourcingOriginAggregate`); nothing is hard-coded, so Q-8 blocks the content, not the build. Backlog §18.4 lists US-SHP-05 as blocked by Q-8; this design removes the block |
| **Second language** (SRS Q-12) | The localisation mechanism is built, but no second locale is enabled by default (`Localisation:ContentLocales = ["en"]`). US-SHP-14 is proven in `Testing` with a second locale configured |
| **Tax-inclusive or tax-exclusive prices** (SRS Q-5) | One setting, `commerce.pricesIncludeTax` (default `false` per FR-ORD-5). Shop labels the price; Orders and Billing compute (OQ-4) |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, a second host, a `CommandService` layer, `202 Accepted`,
`[Authorize]`, a search engine (Elasticsearch, Meilisearch, Lucene), a projection event handler
(`.ai/domain-events-and-outbox.md` §3, ADR-012).

Specific to this slice:

- **No stock quantity** anywhere in this schema, and no stock arithmetic. Availability is read from
  `inventory.v_sellable_stock` at query time; nothing in Shop stores it or edits it (ADR-082, D-3).
- **No cart, order, order line, reservation, payment or invoice type.**
- **No diet-authorisation table or aggregate.** Pharmacy owns the fact (`.ai/bounded-contexts.md`
  "cross-context rules"; `.ai/flows/prescription-diet-flow.md`).
- **No file upload**: no `IFormFile`, no bytes in a command, no S3 client, no local file write.
- **No HTML** in any product text field. Description, feeding guide and sourcing notes are
  `LimitedMarkup`. Review text is plain text.
- **No enqueue from an event handler** (HO-NTF-001 ADR-057).
- **No client-trusted price, availability or eligibility.** No endpoint accepts a price for
  anything except the staff price-scheduling command.

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged — `CatAggregate`, `clinic.cat*`, `CatWeightRecordAggregate` | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001 merged** — sessions, `ActionRoles`, `[PublicEndpoint]`, `[StorefrontEndpoint]`, `IBusinessSettings`, `SettingDefinitions`, tax classes, `Money`, `LimitedMarkup`, the outbox, `NotificationTemplateCatalogue`, `NotificationRoutingPolicy`, `INotificationRecipientDirectory`, `INotificationLinkBuilder`, subject guards, the rate limiter, `UniqueConstraintExceptionFilter` | ❌ **not implemented at baseline** — §20.1 |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ |
| PostgreSQL **`pg_trgm`** extension available | ⚠️ **unverified.** `pg_trgm` is a contrib extension and **trusted** since PostgreSQL 13, so the database owner can `CREATE EXTENSION` it without superuser. Its presence on the local install has not been checked. **Phase E step 1:** run `SELECT name, default_version, installed_version FROM pg_available_extensions WHERE name = 'pg_trgm';`. No row → §20.2 |
| HO-APT-001 | Not required by this slice |
| New NuGet packages | **None** |

The shared-database rules of HO-CAT-001 §16.1 and HO-ACC-001 §16.1 apply. Product slugs, SKU codes,
barcodes, origin country codes and review uniqueness are **unique keys**, so mint them per test.
The public catalogue is **global**, so every catalogue assertion is scoped to data the test created
(§16.1).

---

## 6. Governing architecture documents

Where any of these disagrees with a `.ai/templates/*.cs` file, **the markdown wins**.

| Document | What you need from it in this slice |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer responsibilities; ports in `Domain`, implementations in `Infrastructure` (the diet-authorisation, purchase-history and media ports, §8.12); §6 the verified platform surface; §6.2 the two rules that fail silently |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | `IInMemoryDispatcher` only. The **inbound** command `RecordProductSalesStatsCommand` and the contract queries are dispatched in-process by later areas, never over HTTP |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Names the **`Product`** context (`ProductAggregate`, SKUs, country of origin) and places `DietAuthorisation` in Prescription with "the order handler checks it", which is the split ADR-084 follows. **Rule 1:** one aggregate per command, with no exception recorded here. **Rule 2:** by-id references (`TaxClassId`, component `SkuId`s). **Rule 3:** prices, reviews and alerts accumulate without bound, so each is its own root. **Rule 4:** the prescription gate, bundle availability and food recommendation are domain services |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | Every namespace in §11–§12. `Product` is the `[Feature]` segment for the whole slice. `Sku` is listed there but **not used** (ADR-077) |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 are binding.** ADR-012 (no projections) - the stock copy that once conflicted with it is gone (README C-1); the narrowed ADR-082 (sales stats only) is still judged against it; ADR-015, ADR-016 (`xmin` + `ExpectedVersion` on the product editor), ADR-021 |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §3 handler shape; §4 uniqueness pre-check (slug, SKU code, barcode); §5 when a domain service is warranted; **§6 rule placement** (publish rules are the aggregate's; "tax class exists" is the handler's) |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | `AsNoTracking()`, explicit `Select`, paginate everything. **§5 a heavy read goes through a database view created by a migration and mapped with `ToView`**: `product.storefront_catalogue_entry`, `product.product_rating_summary`, `product.catalogue_term` (§12.1) |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §4 `OwnsMany` for SKUs, images, components, related links and translations; §6 `xmin`; §7 indexes in configurations (the trigram GIN index and the views are raw SQL in the migration); §8 column types; §9 reversible migrations |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §3 a failing event handler fails the command after commit, so the merge and anonymisation handlers are idempotent and never enqueue; **§4 the outbox**: back-in-stock messages go through HO-NTF-001's outbox from a command handler; §5 event vs direct call |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | The enumerations in §10.1. The two-level taxonomy is an enumeration with a parent reference, persisted by `Value` |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | §3 validators as `IValidator<T>`; §4 lifetimes (the worker is a Singleton creating a scope per unit of work); `ValidateScopes` / `ValidateOnBuild` stay on |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs` shape; §5 the never-register list; §6 configuration (`Media`, `Localisation`, `Shop:Worker` sections) |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File order within each phase |
| [`.ai/flows/prescription-diet-flow.md`](../../.ai/flows/prescription-diet-flow.md), [`.ai/flows/import-to-shelf-flow.md`](../../.ai/flows/import-to-shelf-flow.md) | The cross-module shape of BR-2 and of receiving against catalogue SKUs. Both say "SKU" where FR-SHP-4 and FR-SHP-11 say "product"; ADR-081 and OQ-6 settle that |
| [`HO-CAT-001`](HO-CAT-001.md) | The reference implementation: §12.1 EF configuration rules, §16.1 shared-database rules, §8 the cat fields read by §8.13 |
| [`HO-ACC-001`](HO-ACC-001.md) | §7 F11–F18; §12.1 persistence conventions; §16.1 test rules; §17 commands; ADR-039 merge contract; ADR-041 anonymisation |
| [`HO-ADM-001`](HO-ADM-001.md) | §8.6 tax classes and the snapshot rule; §8.7 settings; §8.8 `LimitedMarkup`; §12.3 `ActionRoles`; §12.5 `MaintenanceModeFilter` |
| [`HO-NTF-001`](HO-NTF-001.md) | §8.2 template definitions and `TemplateRules`; §8.4 the producer rules; §8.5 routing (R3/R4/R6 decide who receives a marketing alert); §8.7 subject guards; §8.8 link builder |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001 F28–F30
apply. The facts below are **read from the approved handovers, not from running code**. Only
HO-CAT-001 is implemented at `4007225`, and nothing here was executed.

| # | Fact | Consequence |
|---|---|---|
| F31 | HO-ADM-001 §12.3 gives `TaxClasses.View` to `Administrator`, `Accountant` and **`ShopManager`** | The product editor's tax-class picker reads `GET /api/admin/tax-classes` as a shop manager. No new action is needed |
| F32 | HO-ADM-001 §4.2 reserves `[StorefrontEndpoint]` for "HO-SHP-001, HO-ORD-001", **applied to nothing yet** | This slice is the first to apply it (§12.3). I-E9 proves a staff endpoint still answers during maintenance |
| F33 | HO-NTF-001 §4.2 classifies back-in-stock as **Marketing**. §8.5 R4 sends marketing only on consented channels; R6 makes the in-app copy for marketing depend on in-app consent; R7 suppresses with `NoPermittedChannel` otherwise | A customer who asked for an alert but granted no marketing consent **receives nothing**. The alert response computes and returns this (§8.11). OQ-2 asks the owner |
| F34 | HO-CAT-001 §8: `CatAllergy.Allergen` is free text ≤ 200; `CatChronicCondition.Condition` is free text ≤ 200; `DateOfBirth` is `DateOnly?` with `DateOfBirthPrecision`; `NeuterStatus` is `Intact`/`Neutered`/`Unknown`; the current weight is derived from `clinic.cat_weight_record` (ADR-023) | Allergy exclusion is a normalised **token match** on free text, and conditions map to health focus through a keyword table (§8.13, OQ-9). Neither is a coded lookup |
| F35 | HO-ACC-001 §10.2 `StaffRoles` ids: `Reception`, `Nurse`, `Vet`, `Caretaker`, `Inventory`, `ShopManager`, `Accountant`, `Administrator` | §12.3 uses these ids verbatim |
| F36 | HO-APT-001 §7 F30: `UniqueConstraintExceptionFilter` maps named `23505` (and now `23P01`) constraints; unknown ones rethrow | §12.5 adds this slice's named indexes |
| F37 | HO-ADM-001 §8.6 `Money`: `decimal(12,2)` ≥ 0 + `CurrencyCode`; wire shape `{ "amount": "1500.00", "currencyCode": "BDT" }` | SKU prices reuse the value object. Shop adds the stricter "> 0" rule for goods (§8.5) |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Product context — schema `product`          (ADR-077)
ProductAggregate            (root)                                  product.product
├── Skus               [owned many ≤ 100]  Sku                      product.sku
├── Images             [owned many ≤ 40]   ProductImage (SkuId?)    product.product_image
├── BundleComponents   [owned many ≤ 20]   BundleComponent          product.bundle_component
├── RelatedProducts    [owned many ≤ 24]   RelatedProductLink       product.related_product
├── Translations       [owned many ≤ 4]    ProductTranslation       product.product_translation
├── FoodDetails        [owned one, opt]    FoodDetails              (columns on product.product)
└── Tags, HealthFocus  [primitive collections]                      text[] / int[]
SkuPriceAggregate           (root, append-only)   → SkuId           product.sku_price
SourcingOriginAggregate     (root)                → CountryCode     product.sourcing_origin
ProductSalesStatsAggregate  (root, Id == ProductId, system-written) product.product_sales_stats
ProductReviewAggregate      (root)  → ProductId, SkuId, CustomerId  product.product_review
WishlistAggregate           (root, Id == CustomerId)                product.wishlist
└── Items              [owned many ≤ 200]  WishlistItem             product.wishlist_item
StockAlertAggregate         (root)  → CustomerId, SkuId             product.stock_alert

Views (migration-created, read-only, `ToView`):
product.storefront_catalogue_entry · product.product_rating_summary · product.catalogue_term
```

**One aggregate mutated per command, with no exception.** Every cross-aggregate need is a read in
the handler (tax class exists; component SKUs are active and not prescription; the SKU has a current
price) or an idempotent event handler (merge, anonymisation, discontinuation → alerts).

**Why SKUs are owned by the product.** A product's variants are bounded (≤ 100) and share
invariants the product enforces: a unique variant combination, at least one active SKU while the
product is active, one tax class. Other contexts refer to a SKU by its own id, which is a stable
`Guid` that is never reused and never deleted (a SKU is discontinued, not removed). The `product.sku`
table carries a unique index on its id for cross-context lookups (ADR-079).

**Why prices, reviews, alerts and stock levels are separate roots.** Price history, reviews and
alerts grow without bound (rule 3). Stock levels and sales figures are written by another context on
its own schedule, and must never make a product edit conflict on `xmin` (ADR-082).

### 8.2 The taxonomy — US-SHP-01 AC3 — ADR-078

Code-defined, two levels, FR-SHP-1 verbatim. Each `ProductSubcategory` carries its parent
`ProductCategory`.

| `ProductCategory` | `ProductSubcategory` members |
|---|---|
| `CatFood` | `Dry`, `Wet`, `Treats`, `Kitten`, `Adult`, `Senior`, `Therapeutic` |
| `Litter` | `Clumping`, `Silica`, `Tofu`, `Bentonite` |
| `LitterPotsAndAccessories` | `Trays`, `HoodedBoxes`, `Scoops`, `Mats`, `Liners` |
| `BagsAndCarriers` | `Carriers`, `Backpacks`, `TravelBags`, `StorageBags` |
| `HealthAndGrooming` | *(none in FR-SHP-1)* |
| `ToysAndFurniture` | *(none in FR-SHP-1)* |

A product has one category and **at most one** subcategory. When a subcategory is given, it must
belong to the category (`InvalidProductCategory`). `IsFood` is true exactly for `CatFood`. Adding a
category or subcategory is an SRS change (OQ-18).

### 8.3 `ProductAggregate` — US-SHP-01, US-SHP-02, US-SHP-03

| Field | Type | Rules |
|---|---|---|
| `Kind` | `ProductKind` | `Standard` \| `Bundle`; immutable after create |
| `Name` | string | 1…200 |
| `Brand` | string | 1…100 |
| `Description` | string | `LimitedMarkup`, ≤ 5000 (HO-ADM-001 `LimitedMarkup.Validate`) |
| `Category`, `Subcategory?` | enumerations | §8.2. **Category is immutable while `Active`** (`CategoryLockedWhileActive`): it decides whether food rules apply |
| `Tags` | `text[]` ≤ 20 | Each lower-cased, trimmed, `^[a-z0-9]+(-[a-z0-9]+)*$`, ≤ 40, de-duplicated (`InvalidTag`, `TagLimitExceeded`) |
| `Slug` | string ≤ 120 | `^[a-z0-9]+(-[a-z0-9]+)*$`; unique; **locked after first publication** (ADR-095) |
| `Status` | `ProductStatus` | §8.4 |
| `FirstPublishedOn?` | `DateTimeOffset?` | Set once; the "newest" sort key |
| `TaxClassId` | Guid | HO-ADM-001 tax class; exists and is active (handler) |
| `CountryOfOriginCode?` | `char(2)` | ISO-3166-1 alpha-2 from `CountryCodes` (Shared) — `InvalidCountryCode`. On the **product**, not the SKU (ADR-081) |
| `FoodDetails?` | owned | §8.5; only when `Category == CatFood` |
| `PrescriptionRequired` | bool | Food only (`PrescriptionOnlyForFood`). FR-SHP-4 "whether veterinary authorisation is required"; FR-SHP-11 |
| `IsExpiryTracked` | bool | **Forced `true` for food**; settable for other categories (a flea treatment in Health & Grooming). HO-IMP-001 reads it to refuse a receipt without expiry (D-2, US-IMP-04 AC2) |
| `ListedOnStorefront` | bool | Default `true`. `false` = an active SKU for stock and clinical use that is never shown or sold online (ADR-094, OQ-5) |
| `HealthFocus` | `int[]` of `HealthFocus` ≤ 6 | Food only. Input to §8.13 |
| `SearchText` | string | Derived. Lower-cased, whitespace-collapsed concatenation of name, brand, tags, ingredient list, every SKU's flavour and every translation's name. **Recomputed by the aggregate on every mutation that touches those fields** (`RefreshSearchText`), never set from outside (ADR-083) |
| `Skus`, `Images`, `BundleComponents`, `RelatedProducts`, `Translations` | owned | §8.3.1–§8.3.3, §8.10, §8.14 |

#### 8.3.1 `Sku` (owned) — US-SHP-02 AC1

| Field | Rules |
|---|---|
| `Id` | Guid, minted by the controller, never reused (F16) |
| `Code` | `^[A-Z0-9]+(-[A-Z0-9]+)*$`, ≤ 40, **unique across all products** (`SkuCodeAlreadyInUse`), immutable |
| `Barcode?` | 4…32 chars `[0-9A-Za-z-]`. When it is 8, 12, 13 or 14 **digits** it is treated as a GTIN and its check digit must be valid (`InvalidBarcode`). **Unique across all products** when present (`BarcodeAlreadyInUse`) — HW-2 scanning and POS depend on it |
| `SizeOrWeight?` (≤ 50, e.g. "2 kg"), `Flavour?` (≤ 80), `Colour?` (≤ 50), `PackQuantity?` (1…999) | FR-SHP-3's four axes. **With more than one SKU, every SKU has at least one axis set** (`VariantAttributesRequired`), and no two SKUs of a product share the same combination (`DuplicateVariant`, compared case-insensitively) |
| `Status` | `SkuStatus`: `Active` \| `Discontinued` |
| `Label` | Derived for display: the set axes joined with " · " ("2 kg · Salmon"), or the product name for a single-axis-less SKU |

A bundle product has **exactly one** SKU (the sellable bundle) and no variant axes (§8.10).

#### 8.3.2 `ProductImage` (owned) — US-SHP-01, US-SHP-02 AC1

`Id`, `SkuId?` (null = product-level; otherwise a SKU of this product — `InvalidImage`),
`StorageKey` (≤ 500), `ContentType` (`image/jpeg` \| `image/png` \| `image/webp`), `SizeInBytes`
(1…5 242 880), `AltText` (**required**, 1…200 — UI-6; `ImageAltTextRequired`), `SortOrder`,
`IsPrimary`. Exactly one `IsPrimary` among product-level images when any exist. The whole set is
replaced at once (`SetImages`), ≤ 40 (`ImageLimitReached`).

#### 8.3.3 Methods

| Method | Rules | Event |
|---|---|---|
| `Create(dto)` | Fields above; `Status = Draft`; a bundle is created with its single SKU | `ProductCreatedEvent` |
| `UpdateDetails(dto)` | Name, brand, description, subcategory, tags, tax class, country of origin, listed flag, expiry tracking (non-food), health focus (food); category only while not `Active`. **While `Active`, a food product cannot lose its country of origin** (`CountryOfOriginRequired`) | `ProductDetailsUpdatedEvent` |
| `SetFoodDetails(dto)` | Food only (`FoodDetailsOnlyForFood`); §8.5 | `ProductFoodDetailsUpdatedEvent` |
| `SetPrescriptionRequired(bool)` | Food only | `ProductPrescriptionRequirementChangedEvent` (`ProductId`, `PrescriptionRequired`) — contract for HO-ORD-001 and HO-PHM-001 |
| `ChangeSlug(slug)` | Only if `FirstPublishedOn` is null (`SlugLockedAfterPublication`) | `ProductSlugChangedEvent` |
| `AddSku(dto)` / `UpdateSku(dto)` | §8.3.1; ≤ 100 (`SkuLimitReached`); not on a bundle after create | `SkuAddedEvent` / `SkuUpdatedEvent` |
| `DiscontinueSku(skuId)` / `ReactivateSku(skuId)` | Cannot discontinue the **last active SKU of an `Active` product** (`LastActiveSkuCannotBeDiscontinued`) | `SkuDiscontinuedEvent` / `SkuReactivatedEvent` |
| `SetImages(images)` | §8.3.2 | `ProductImagesChangedEvent` |
| `SetBundleComponents(components, componentFacts)` | §8.10 | `BundleCompositionChangedEvent` |
| `SetRelatedProducts(links)` | §8.10 | `RelatedProductsChangedEvent` |
| `SetTranslation(locale, content, enabledLocales)` / `RemoveTranslation(locale)` | §8.14 | `ProductTranslationChangedEvent` |
| `Publish(facts, now)` / `Unpublish()` / `Archive()` | §8.4 | `ProductPublishedEvent` / `ProductUnpublishedEvent` / `ProductArchivedEvent` |

### 8.4 Status and the publication rules — US-SHP-01 AC2, US-SHP-03 AC3 — ADR-095

```
Draft ──Publish──► Active ──Archive──► Archived ──Publish──► Active
                     │
                     └──Unpublish──► Draft
```

Any other transition → `InvalidProductTransition`, with a message naming both states.
**Only `Active` and `ListedOnStorefront` products exist on the storefront.** A draft or archived
slug is `404` on every public route, and so is an unlisted product.

`ProductPublicationRules.Evaluate(product, facts)` is **pure** (Domain.DomainServices). It returns
a checklist of `(Item, IsBlocking, IsMet, Message)`. `Publish` throws `ProductNotPublishable` when
any blocking item is unmet, and the message lists every unmet item. The staff detail read returns
the same checklist, so the screen shows exactly what the server will enforce.

| Item | Blocking | Rule |
|---|---|---|
| `ActiveSku` | ✅ | At least one `Active` SKU |
| `PricedSkus` | ✅ | Every `Active` SKU has a price effective **now** (`facts.PricedSkuIds`, from the handler) — the message names the SKU codes |
| `TaxClassActive` | ✅ | `facts.TaxClassActive` |
| **`CountryOfOrigin`** | ✅ food | Food product without `CountryOfOriginCode` → *"Add the country of origin before publishing this food product."* — **US-SHP-03 AC3** |
| `FoodDetails` | ✅ food | `FoodDetails` present with `LifeStage` set (it drives filtering and recommendation) |
| `PrimaryImage` | — | Warning only: *"Products without a photo convert poorly."* |
| `LabellingComplete` | — | Warning only, food: manufacturer, ingredient list, guaranteed analysis, feeding guide and storage instructions each present. **Which of these are legally mandatory is OQ-3**; until answered only country of origin blocks, because that is the one the AC names |
| `BundleComposition` | ✅ bundle | §8.10: ≥ 2 component units, every component `Active`, on sale, not a bundle and not prescription |

### 8.5 Food details and SKU prices — US-SHP-03, US-SHP-02 — ADR-080, ADR-081

**`FoodDetails`** (owned one, columns on `product.product`):

| Field | Rules |
|---|---|
| `Manufacturer?` | ≤ 200 |
| `LifeStage?` | `LifeStage`: `Kitten`, `Adult`, `Senior`, `AllLifeStages` |
| `ProteinSource?` | `ProteinSource` (§10.1, OQ-10) — the **primary** protein, one value, so the filter is exact |
| `IngredientList?` | plain text ≤ 4000 |
| `GuaranteedAnalysis?` | owned VO: `ProteinPercent`, `FatPercent`, `FibrePercent`, `MoisturePercent`, each `decimal(5,2)` 0…100, sum ≤ 100 (`InvalidGuaranteedAnalysis`). All four or none |
| `FeedingGuide?` | `LimitedMarkup` ≤ 4000 |
| `StorageInstructions?` | plain text ≤ 1000 |

**`SkuPriceAggregate`** (append-only; HO-ADM-001 ADR-049 applied to goods):
`SkuId`, `Amount` (`Money`), `EffectiveFrom` (`DateTimeOffset`), `SetBy`. Created by
`ScheduleSkuPriceCommand` only.

- `EffectiveFrom ≥ now` → else `PriceCannotBeBackdated` (HO-ADM-001's code). The editor sends "now"
  as the current instant, which the handler accepts within a 60-second tolerance for clock skew.
- `Amount > 0` → else `PriceMustBePositive` (a sold item is never free; free samples are OQ-19).
- `Amount.CurrencyCode == commerce.currencyCode` → else `CurrencyMismatch`.
- Unique `(sku_id, effective_from)` → `DuplicatePriceEffectiveDate` (HO-ADM-001's code).
- **Price at instant t** = the row with the greatest `EffectiveFrom ≤ t`. `SkuPricing.PriceAt`
  (pure) and the SQL in the storefront view implement the same rule, and U-A11 / I-A8 pin both.
- Discontinued SKUs keep their history. A SKU with no current price is not sellable, and its product
  cannot be published.

> **Binding on HO-ORD-001 and HO-BIL-001:** read the SKU's price and its product's tax class **at
> the moment of the transaction** through `GetSkuSaleFactsQuery`, and **store the snapshot** on the
> line. Never join back to `product.sku_price` to reprice a line that already exists.

`commerce.pricesIncludeTax` (§10.2) tells every consumer whether the stored amount includes tax.
Shop only labels it (OQ-4).

### 8.6 Sourcing origins — US-SHP-05 — ADR-081

**`SourcingOriginAggregate`**: `CountryCode` (unique, from `CountryCodes`), `SourcingNotes?`
(`LimitedMarkup` ≤ 2000), `IsFeatured`, `SortOrder` (0…999). Methods `Create`, `Update`.
Events `SourcingOriginCreatedEvent`, `SourcingOriginUpdatedEvent`.

**An origin appears in "Shop by Country" when at least one `Active`, listed food product has that
country of origin.** The origin record adds notes, featuring and order. A country with products but
no record is still listed, with no notes. A record with no products is not listed. The country's
display name comes from `CountryCodes` (English, C-8).

### 8.7 Availability and figures from other contexts - US-SHP-02 AC2, US-SHP-07 - ADR-082

> **Reconciled 2026-09-22 (README C-1).** This section originally had Inventory **push**
> `RecordSkuStockLevelCommand` into a Shop-owned `SkuStockStatusAggregate`, and ADR-082 recorded that
> copy as a deliberate exception to ADR-012 (no projections). HO-IMP-001 is now written and publishes
> **`inventory.v_sellable_stock`** (its ADR-116), a migration-created view of sellable quantity and
> FEFO-first expiry per SKU per branch, built for exactly this purpose. Shop **reads the view**. The
> aggregate, its table, its command and the ADR-012 exception are all dropped - the architecture gate
> no longer has an exception to judge, and there is no copy that can fall stale.

**Shop never computes sellable stock.** Inventory (HO-IMP-001) owns the ledger, batches, quarantine,
reservations and FEFO. Shop **joins** `inventory.v_sellable_stock` in its catalogue read models:

| Column | Use |
|---|---|
| `sku_id`, `branch_id` | join key; the storefront uses the configured storefront branch |
| `sellable_quantity` | US-SHP-07 in-stock / low-stock / out-of-stock bands; the availability filter and sort |
| `next_allocatable_expiry_on` | US-SHP-02 AC2 short-dated notice |

Rules that follow from reading a view instead of holding a copy:

- **A SKU with no row is out of stock** - unchanged behaviour, now a `LEFT JOIN` producing `NULL`.
- **No `AsOf`, no monotonicity, no staleness window.** The view is the live figure, so the ordering
  problem the push contract had (late, stale pushes arriving out of order) does not exist.
- **Reading crosses a schema boundary**, which is what `.ai/read-side-pattern.md` §5 allows for
  migration-created views. Shop reads `inventory.v_sellable_stock` and **nothing else** of
  Inventory's - no table, no ledger row, no batch. One convention test asserts that (§12).
- **The 500 ms / 20,000-SKU budget (NFR-PRF-2) now depends on Inventory's view**, not on a local
  column. HO-IMP-001 ADR-116 states the view exists to be joined, but the index that makes the
  *filtered and sorted* catalogue query meet the budget is Shop's to prove - see OQ-21 and the
  Phase D performance gate.

**`ProductSalesStatsAggregate` is unchanged.** HO-ORD-001 publishes no equivalent view, so the
popularity figures stay a pushed copy through `RecordProductSalesStatsCommand`; ADR-082 is narrowed
to cover that copy alone.

### 8.8 Catalogue search, filter and sort — US-SHP-04, US-SHP-05, US-SHP-06 — ADR-083

**One SQL query per request**, over `product.storefront_catalogue_entry` (§12.1). That view has one
row per `Active`, listed product, with its min and max **current** price, `in_stock`, rating average
and count, units sold, first-published instant, primary image and `search_text`.

| Parameter | Semantics |
|---|---|
| `q` | Normalised (`CatalogueQuery.Normalise`): trimmed, whitespace collapsed, lower-cased, ≤ 100 chars, `%`/`_`/`\` escaped for `ILIKE`. Match = `search_text ILIKE '%q%'` **or** `word_similarity(q, search_text) ≥ ShopSearchRules.WordSimilarityThreshold` (0.45). The trigram GIN index serves both |
| `category`, `subcategory` | Exact |
| `brand[]` | ≤ 20 values, case-insensitive exact |
| `origin[]` | ≤ 20 ISO codes |
| `lifeStage[]` | ≤ 4; a product labelled `AllLifeStages` matches every life stage |
| `protein[]` | ≤ 20 |
| `minPrice`, `maxPrice` | Decimal strings; `min ≤ max`; a product matches when **any** active SKU's current price is in range |
| `availability=inStock` | `in_stock = true` |
| `minRating` | 1…5; `rating_average ≥ minRating` (unrated products excluded when set) |
| `sort` | `relevance` (default when `q` is present: `word_similarity` desc), `newest` (default otherwise: `first_published_on` desc), `price_asc` / `price_desc` (by min current price), `popularity` (units sold desc, then newest), `rating` (average desc, then count desc) |
| `pageNumber`, `pageSize` | `pageSize` 1…48, default 24 |

Every sort accepts every filter, and the response echoes `appliedFilters` (**US-SHP-04 AC2**, server
half). Ties always break by `product_id`, so paging is stable.

**Suggestions — US-SHP-06 AC3.** When `q` is present and `totalCount == 0`:
`didYouMean` = up to 3 terms from `product.catalogue_term` (distinct brands and product names of
active listed products) with `similarity(term, q) ≥ 0.3`, best first. `popular` = up to 8 in-stock
products by popularity, then newest. A zero-result search is never an empty page.

**Facets** (`GET /api/shop/filters`): the brands, origins (with names), life stages, protein sources
and price bounds present among active, listed products (optionally within a category), each with a
product count. The same SQL view serves them, and they are cached 60 s in memory.

**Performance — NFR-PRF-2.** The trigram GIN index on `product.product.search_text`, B-tree indexes
on every filter column (§12.1), `(sku_id, effective_from DESC)` on prices, and the view's lateral
current-price join are designed for **≤ 500 ms p95 at 20,000 SKUs**. I-E11 measures this against an
idempotent 20,000-SKU fixture (§16.1). **If it fails, the query or the indexes change. The test
never does, and no cache is placed in front to pass it** (§20.4).

### 8.9 Prescription gating — US-SHP-10 — ADR-084

**Pharmacy owns the authorisation** (FR-PHM-9; `.ai/bounded-contexts.md`). Shop reads it through
a port:

```csharp
// Domain.DomainServices — the contract HO-PHM-001 implements
public interface IDietAuthorisationGate
{
    Task<IReadOnlyList<DietAuthorisationFact>> GetValidAsync(
        Guid customerId, Guid productId, DateTimeOffset asOf, CancellationToken ct);
}
public sealed record DietAuthorisationFact(Guid CatId, Guid? SkuId, DateOnly ValidTo);
// SkuId null = any variant of the product (OQ-6)
```

**Shipped implementation: `NoDietAuthorisationsReader`** (Infrastructure), which always returns an
empty list. It is registered until HO-PHM-001 replaces the one registration line by an Amended row.
**Fail-closed**: with no Pharmacy, no prescription product is purchasable by anyone (BR-2).

`PrescriptionPurchaseGate.Evaluate(prescriptionRequired, customerId?, skuId, authorisations,
customerCats, asOfLocalDate)` (pure):

| Case | Result |
|---|---|
| Not prescription | Purchasable (subject to availability) |
| Prescription, no customer (guest) | `PrescriptionSignInRequired` |
| Prescription, customer | Eligible cats = authorisations whose `ValidTo ≥ asOfLocalDate`, whose `SkuId` is null or equals the SKU, whose cat is **alive** and **still owned by this customer** (ownership can move — FR-CAT-8). None → `PrescriptionAuthorisationRequired`; otherwise purchasable **for those cats only** |

The purchasability read (§12.4) combines the gate with availability per SKU:
`purchasable`, `purchaseBlockReason` (`OutOfStock` \| `PrescriptionSignInRequired` \|
`PrescriptionAuthorisationRequired` \| `NotOnSale`) and `eligibleCats[]` (`catId`, `name`,
`validTo`). **US-SHP-10 AC1** is this read plus the client's link to book a consultation.

> **Contract for HO-ORD-001 — `CheckPrescriptionEligibilityQuery`** (`CustomerId`, `SkuId`,
> `CatId`, `AsOf`) → `{ eligible, blockReason? }`. The order area calls it **at add-to-cart**
> (US-SHP-10 AC2: the cat must be selected and is then recorded on the order line) **and again at
> checkout** (US-SHP-10 AC3: an authorisation that expired in between blocks the line, with the
> reason). Both calls go through the same gate, so the two checks cannot drift
> (`.ai/flows/prescription-diet-flow.md` "checked twice").

### 8.10 Bundles and related products — US-SHP-12, US-SHP-08 — ADR-085, ADR-088

**Bundle** = a product with `Kind = Bundle`, **one** SKU (its own code, barcode and effective-dated
price — "priced as a unit", **US-SHP-12 AC1** first half) and `BundleComponents`:

`BundleComponent`: `Id`, `ComponentSkuId`, `Quantity` (1…20). ≤ 20 components; no duplicate SKU; not
its own SKU. The handler supplies `componentFacts`, and `SetBundleComponents` refuses
(`InvalidBundleComponent`, naming the SKU code) a component that is a bundle SKU, belongs to a
**prescription** product (a kit cannot carry an authorisation per cat — OQ-14), or is discontinued.
Publishing needs ≥ 2 component units in total, every component `Active` and on a product that is
`Active` (§8.4).

Availability: **`InStock` only if every component has `floor(sellable ÷ quantity) ≥ 1`**
(**US-SHP-12 AC2**). An archived or discontinued component makes the bundle `OutOfStock` and
flags it on the staff detail.

> **Contract for HO-ORD-001 / HO-IMP-001 — `GetBundleCompositionQuery`** (`BundleSkuId`) →
> `[(ComponentSkuId, Quantity)]`, or null for a non-bundle SKU. Orders reserves, and Inventory
> deducts, **each component from its own stock** (US-SHP-12 AC1, second half). The bundle SKU has
> no stock of its own.

**Related products** — `RelatedProductLink`: `RelatedProductId`, `Kind` (`FrequentlyBoughtTogether`
\| `UpSell`), `SortOrder`. ≤ 12 per kind, no self-link, no duplicates (`RelatedProductInvalid`).
Curated by the shop manager (OQ-7).

**The recommendation blocks read** (`GetProductRecommendationsQuery`, public, optional customer
session):
- `frequentlyBoughtTogether`, `upSell`: the curated links, in order;
- `lifeStage`: up to 8 food products whose life stage matches — **the signed-in customer's alive
  cats' life stages** (§8.13's derivation) when they have any, else the product's own — by
  popularity, then newest; `basis` = `YourCats` \| `ThisProduct`.

**Exclusions — US-SHP-08 AC**, applied to every block by `RecommendationExclusionPolicy` (pure):
not `Active`/listed; `OutOfStock`; the product itself; and **prescription products the caller
cannot buy**: always for a guest, and for a customer unless the gate (§8.9) finds an eligible cat.

### 8.11 Reviews, wishlist, back-in-stock — US-SHP-09, US-SHP-13 — ADR-086, ADR-087

**Purchase history is Orders' fact.** Port:

```csharp
public interface IPurchaseHistoryReader   // HO-ORD-001 implements
{
    Task<IReadOnlySet<Guid>> GetPurchasedSkuIdsAsync(
        Guid customerId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct);
}
```

Shipped `NoPurchaseHistoryReader` returns an empty set (OQ-13 defines "purchased": a delivered
online order line or a completed POS sale, for HO-ORD-001 to implement).

**`ProductReviewAggregate`**: `ProductId`, `SkuId` (the purchased variant), `CustomerId`, `Rating`
(1…5), `Title?` (≤ 120), `Body` (1…2000, **plain text**; control characters other than line breaks
refused), `Status`, `SubmittedOn`, `ModeratedOn?`, `ModeratedBy?`, `RejectionReason?`,
`ModerationNote?` (≤ 500).

```
Submit ──► PendingModeration ──Publish──► Published ──Hide──► Hidden
                 │                            │
                 └──Reject(reason)──► Rejected │
Edit (Published | Rejected | PendingModeration) ──► PendingModeration
Withdraw (any but Withdrawn) ──► Withdrawn
```

| Rule | Where |
|---|---|
| **The SKU must be among the customer's purchased SKUs of this product** → else `ReviewNotPermitted` (**US-SHP-09 AC2**) | Handler, via the port |
| One non-withdrawn review per customer per product → `AlreadyReviewed` | Handler pre-check + filtered unique index |
| **Published only after moderation** (**US-SHP-09 AC3**). Public reads return `Published` only | Aggregate + query |
| Reject needs a reason (`ReviewRejectionReasonRequired`) | Aggregate |

Rating summary: `product.product_rating_summary` view (average to 1 dp, count) over `Published`
reviews. It is not stored and cannot drift.

**`WishlistAggregate`** (`Id == CustomerId`, created on first write): `Items` ≤ 200
(`WishlistLimitReached`), `WishlistItem(SkuId, ProductId, AddedOn)`, unique per SKU. Adding a
present SKU is a no-op success. Items for archived products or discontinued SKUs remain, and read
back as not on sale.

**`StockAlertAggregate`**: `CustomerId`, `SkuId`, `Status` (`Pending` → `Notified` \| `Closed`),
`ClosureReason?`, `RequestedOn`, `NotifiedOn?`. Rules:
- Request only when the SKU is currently `OutOfStock` (`SkuAlreadyAvailable` otherwise), on sale,
  and not a prescription product the customer cannot buy (`PurchaseNotPermitted`). One `Pending`
  alert per customer per SKU (`StockAlertAlreadyRequested`, filtered unique index).
- **Notified once — US-SHP-13 AC1.** `ShopWorker` (§12.2) runs `BackInStockPass` every
  `Shop:Worker:IntervalSeconds` (120): it reads `Pending` alerts whose SKU is now `InStock` and
  dispatches `NotifyStockAlertCommand` per alert. The handler re-checks availability, calls
  `MarkNotified`, **enqueues `shop.back-in-stock`** and saves: one aggregate, with the message
  committed with it (HO-NTF-001 ADR-057). `Notified` is terminal, so a later sell-out and
  restock sends nothing more.
- Closed by the customer (`CancelledByCustomer`), or by idempotent event handlers when the SKU is
  discontinued or the product archived (`SkuNoLongerSold`), on anonymisation (`CustomerAnonymised`),
  or as the duplicate side of a merge collision (`MergedDuplicate`).

**Deliverability — F33, OQ-2.** The request response carries
`deliverability: { willNotify, channels[], inboxCopy }`, computed by
`GetStockAlertDeliverabilityQuery`. That query resolves the customer through HO-NTF-001's
`INotificationRecipientDirectory` and runs HO-NTF-001's **own** pure `NotificationRoutingPolicy.Plan`
against the `shop.back-in-stock` definition, so the answer is the real routing decision, not a guess.

Template (appended to `NotificationTemplateCatalogue`, marketing rules apply):

| Key | Category | Urgency | Channels | Variables (required on) |
|---|---|---|---|---|
| `shop.back-in-stock` | **Marketing** (HO-NTF-001 §4.2) | Normal | Sms, Email, InApp | `productName` (Text; every channel), `variantLabel` (Text), `productLink` (Url; every channel) + standard `unsubscribeLink` (every channel) |

`productLink` = `INotificationLinkBuilder.ProductLink(slug, skuId)` →
`{WebBaseUrl}/shop/products/{slug}?sku={skuId}` (a new method; HO-NTF-001 amendment, §13).
Subject kind **`StockAlert`**, guarded by `StockAlertGuard`: valid while the alert is `Notified`,
the SKU `Active` and its product `Active` and listed. Stock is **not** re-checked at send time. A
quiet-hours hold may outlast a brief sell-out, and "it came back" is still true.

### 8.12 Ports, domain services and contracts for other areas

| Name | Kind | Owner of the implementation | Notes |
|---|---|---|---|
| `ProductPublicationRules`, `SkuPricing`, `SkuAvailabilityRules`, `PrescriptionPurchaseGate`, `RecommendationExclusionPolicy`, `FoodRecommendationPolicy`, `ProductContentLocalisation`, `CatalogueQuery` | pure domain services | here | No I/O; unit-tested without a database |
| `IDietAuthorisationGate` | port | **HO-PHM-001**; stub here | §8.9 |
| `IPurchaseHistoryReader` | port | **HO-ORD-001**; stub here | §8.11 |
| `IMediaUrlBuilder` | port | here (`Media/ConfiguredMediaUrlBuilder`) | `PublicImageUrl(storageKey)` → `{Media:PublicBaseUrl}/{key}` or null (ADR-091) |
| `StockAlertGuard` | `INotificationSubjectGuard` | here | §8.11 |
| `RecordProductSalesStatsCommand` | **inbound** command | sent by **HO-ORD-001** | §8.7 |
| `GetSkuSaleFactsQuery` (`SkuIds[]`, `AsOf`) → per SKU: `productId`, `productName`, `skuLabel`, `skuCode`, `kind`, `productStatus`, `skuStatus`, `listedOnStorefront`, `prescriptionRequired`, `taxClassId`, `price` (at `AsOf`, or null) | contract query | consumed by HO-ORD-001, POS | Snapshot source (§8.5) |
| `GetSkuCatalogueFactsQuery` (`SkuIds[]`) → `productId`, `category`, `isFood`, `isExpiryTracked`, `countryOfOriginCode`, `skuCode`, `barcode` | contract query | consumed by HO-IMP-001, HO-RPT-001 | D-2 and revenue by origin |
| `FindSkuByBarcodeQuery` (`Barcode`) | contract query + staff endpoint | consumed by HO-IMP-001 receiving, HO-ORD-001 POS | HW-2 |
| `CheckPrescriptionEligibilityQuery` | contract query | consumed by HO-ORD-001 | §8.9 |
| `GetBundleCompositionQuery` | contract query | consumed by HO-ORD-001, HO-IMP-001 | §8.10 |

Outbound events that later areas may subscribe to: `ProductPublishedEvent`, `ProductArchivedEvent`,
`SkuAddedEvent`, `SkuDiscontinuedEvent`, `SkuPriceScheduledEvent`,
`ProductPrescriptionRequirementChangedEvent`, `BundleCompositionChangedEvent` (§11.5).

### 8.13 Food recommendations for a cat — US-SHP-11 — ADR-089

`GetFoodRecommendationsQuery(customerId, catId, locale)`: the cat must be the caller's and alive
(else `404`). The query loads the cat facts read-only (§12.6) and the candidates (active, listed food
products with their life stage, health focus, ingredient list, prescription flag, availability and
the gate's result for **this cat**), then calls `FoodRecommendationPolicy.Recommend` (pure):

1. **Exclude** `OutOfStock`; prescription products without a valid authorisation **for this cat**;
   and **every product whose ingredient list contains a recorded allergen** (**US-SHP-11 AC2**).
   The match is `AllergenMatcher`: both texts are normalised (lower-case, punctuation → space,
   collapsed); the allergen matches when its token sequence appears as whole tokens, allowing a
   trailing `s`/`es` on the last token. So "Chicken" matches "chicken meal" and "Chickens", and
   does not match "chickpea". The result reports `excludedForAllergy: [{ allergen, count }]`.
2. **Life stage** from `DateOfBirth`: under `shop.recommendationKittenMaxAgeMonths` → `Kitten`;
   at or over `shop.recommendationSeniorFromAgeYears` → `Senior`; else `Adult`. A candidate matches
   its own life stage or `AllLifeStages`. With no date of birth, life stage is not filtered and the
   result says so (`hints: ["AddDateOfBirth"]`). An `Estimated` date is used, and the reason says
   "about".
3. **Reasons** (each a code plus the values the client formats): `LifeStageMatch` (always, when
   known); `Sterilised` when `NeuterStatus == Neutered` and the product has that focus;
   `WeightManagement` when the latest weight ≥ `shop.recommendationWeightManagementFromKg` and the
   cat is not a kitten; `ConditionMatch` when a chronic-condition text contains a keyword that
   `HealthFocusKeywords` (Shared) maps to one of the product's health focuses — for example
   "urinary", "FLUTD" or "cystitis" → `Urinary`; "kidney", "renal" or "CKD" → `Renal`.
4. **Rank**: number of reasons desc, then units sold, then name. Up to 12 results, each with
   `reasons[]` (**US-SHP-11 AC1** "with the reason shown").

The keyword table and the thresholds are **general guidance pending clinical sign-off** (OQ-9). The
policy never recommends a product the cat cannot be sold.

### 8.14 Product content localisation — US-SHP-14 — ADR-090

Enabled locales come from configuration `Localisation:ContentLocales` (default `["en"]`). The first
entry is the **default locale**, whose text lives in the product's own fields.
`ProductTranslation` (owned): `Locale` (enabled, **not** the default — `LocaleNotEnabled`,
`DefaultLocaleNotTranslatable`), `Name?` (≤ 200), `Description?` (`LimitedMarkup` ≤ 5000),
`IngredientList?` (≤ 4000), `FeedingGuide?` (`LimitedMarkup` ≤ 4000). An empty translation is
removed. Translated names join `SearchText`.

**Resolution** (`ProductContentLocalisation.Resolve`, pure): per field, the requested locale's
value if present and non-empty, else the default's (**US-SHP-14 AC2**, "never an empty field").
Every public read returns `locale` (requested), and `fieldLocales` (`name`, `description`,
`ingredientList`, `feedingGuide` → the locale actually used), so the client can mark each field's
language. Unknown or disabled `locale` → the default, never an error on a public read.

### 8.15 Exceptions

Reuse `DomainRuleViolationException`. No new exception type.

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. HO-APT-001 owns ADR-067…076; **this handover owns ADR-077…096**
(reserved by the coordinating author; the README ADR table gains the row when this handover is
registered).

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-077** | **`Product` bounded context**, namespace segment `Product`, schema `product`, owning products, SKUs, prices, origins, stock and sales copies, reviews, wishlists and stock alerts. The `Sku` feature name in `.ai/namespace-conventions.md` is **not used** | Reviews, wishlists and alerts are not named in `.ai/bounded-contexts.md` (OQ-1); an unused listed segment looks like an omission |
| **ADR-078** | **Category taxonomy is code-defined**, two levels, FR-SHP-1 verbatim, as enumerations with a parent reference; subcategory optional | US-SHP-01 AC3 names the set; a data-managed taxonomy lets the storefront drift from the SRS |
| **ADR-079** | **SKUs are owned by `ProductAggregate`** (≤ 100); a SKU id is stable, never reused and never deleted (discontinued instead); SKU code and barcode unique across all products; GTIN-length numeric barcodes check-digit validated | Other contexts (Inventory, Orders, Pharmacy) hold SKU ids for years; POS and receiving scan barcodes |
| **ADR-080** | **SKU prices are append-only, effective-dated roots** (HO-ADM-001 ADR-049 applied to goods): no back-dating, > 0, in `commerce.currencyCode`; consumers snapshot through `GetSkuSaleFactsQuery`; `commerce.pricesIncludeTax` tells them whether tax is inside | A price column silently reprices history and makes FR-ORD-3's "price changed since you added it" unanswerable |
| **ADR-081** | **Country of origin and the prescription flag live on the product, not the SKU** — FR-SHP-4 and FR-SHP-11 say "product"; SRS §5.1 and `.ai/flows/*` say SKU. Origins are data (`SourcingOriginAggregate`); an origin is listed when it has an active food product | The SRS disagrees with itself; the choice decides the filter, the gate and the revenue-by-origin report |
| **ADR-082** | **Shop never computes stock or sales figures.** Availability is read live from **`inventory.v_sellable_stock`** (HO-IMP-001 ADR-116) — no copy, no push, no staleness (amended 2026-09-22, README C-1). Popularity remains `ProductSalesStatsAggregate`, a copy pushed by HO-ORD-001 through `RecordProductSalesStatsCommand`: monotonic by `AsOf`, no HTTP route, never editable, display only. Checkout still re-validates with Inventory | The stock half no longer needs an ADR-012 exception, which is why it was dropped. The sales half keeps one: HO-ORD-001 publishes no view, the owning context is the only writer, and no decision depends on the number |
| **ADR-083** | **Catalogue search is PostgreSQL**: `pg_trgm` word-similarity and `ILIKE` over an aggregate-maintained `search_text`, one query over the `product.storefront_catalogue_entry` view, stable tie-breaks; the 500 ms p95 budget is proven by a 20,000-SKU performance test; no search engine | NFR-PRF-2 and NFR-CMP-3 (no proprietary service); a second store would be a projection |
| **ADR-084** | **Prescription gating reads Pharmacy through `IDietAuthorisationGate`**; the shipped stub denies everyone (fail-closed); `PrescriptionPurchaseGate` is pure; `CheckPrescriptionEligibilityQuery` is the one contract HO-ORD-001 calls at add-to-cart and checkout; authorised cats must be alive and still owned | BR-2 must hold before Pharmacy exists; two call sites must not drift |
| **ADR-085** | **Bundles** are products of kind `Bundle` with one priced SKU and component SKUs; availability is the minimum over components; components may not be bundles, prescription products or discontinued; deduction is exploded by Orders/Inventory through `GetBundleCompositionQuery` | FR-SHP-13 "priced as a unit while deducting each component" spans three contexts |
| **ADR-086** | **Reviews**: verified purchase through `IPurchaseHistoryReader` (HO-ORD-001; stub knows no purchases), one per customer per product, plain text, published only after moderation, rating summary as a view | FR-SHP-10; a stored average drifts |
| **ADR-087** | **Wishlist per SKU and back-in-stock alerts** notified exactly once by a worker pass that dispatches one command per alert (enqueue in the command handler); the alert response states deliverability computed by HO-NTF-001's own routing policy; template `shop.back-in-stock` is **Marketing** per HO-NTF-001 §4.2 | FR-SHP-14; F33 means some customers will not be told — the API must say so rather than hide it |
| **ADR-088** | **Cross-sell and up-sell are curated** by the shop manager; the life-stage block is computed; every block excludes out-of-stock items and prescription products the caller cannot buy | No order history exists to compute "frequently bought together" (OQ-7) |
| **ADR-089** | **Food recommendation** is a pure policy over cat facts and candidates: allergen token-match exclusion, life stage from date of birth with configurable thresholds, reason codes, no prescription product without this cat's authorisation | Clinical-adjacent logic must be explainable and testable; thresholds await clinical sign-off (OQ-9) |
| **ADR-090** | **Product content localisation**: owned translations per enabled locale from `Localisation:ContentLocales`, per-field fallback, `fieldLocales` returned; UI chrome is out of scope | C-8 / FR-SHP-15; SRS Q-12 open |
| **ADR-091** | **Images are storage key + metadata** (HO-CAT-001's approach); public URL from `Media:PublicBaseUrl`, else null; alt text required | NFR-SEC-5 upload path is not provisioned; UI-6 |
| **ADR-092** | **Contract queries** (`GetSkuSaleFactsQuery`, `GetSkuCatalogueFactsQuery`, `FindSkuByBarcodeQuery`, `CheckPrescriptionEligibilityQuery`, `GetBundleCompositionQuery`) are the only way other contexts read catalogue facts in their command handlers; they return facts, never decisions other than the gate's | Keeps Orders, Inventory and Pharmacy off `product.*` tables in write paths |
| **ADR-093** | **Storefront endpoints are `[StorefrontEndpoint]`** (public reads and customer shop writes) and answer `503` in maintenance mode; staff endpoints never; anonymous catalogue reads are `Cache-Control: public, max-age=60`, personal reads `no-store`; a `catalogue` rate limit protects search | HO-ADM-001 ADR-053's first consumer; NFR-SCL-2 wants cacheable catalogue traffic |
| **ADR-094** | **`ListedOnStorefront`** lets a product be an active, stockable SKU that is never shown or sold online — for clinic consumables and dispensed items that HO-EMR-001 / HO-PHM-001 deduct | The delivery order exists because clinical areas deduct batches of catalogue SKUs (README); without the flag those items would appear in the shop (OQ-5) |
| **ADR-095** | **Product lifecycle**: `Draft → Active → Archived`, `Active → Draft`, `Archived → Active`; never deleted; slug locked after first publication; category locked while active; publication rules are one pure checklist shared by the command and the staff read | SEO links and order history must not break; the screen must show what the server enforces |
| **ADR-096** | **Customer-data obligations**: `CustomerMergedEvent` moves wishlist items (union), alerts and reviews to the survivor idempotently, closing duplicates on collision; `CustomerAccountAnonymisedEvent` clears the wishlist and closes pending alerts; published reviews stay, credited to "A verified buyer" | HO-ACC-001 ADR-039 / ADR-041 bind every context holding a customer id |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | `.ai/bounded-contexts.md` should list reviews, wishlists and alerts under `Product` | Architect | ADR-077 records it |
| **OQ-2** | **Back-in-stock is Marketing** (HO-NTF-001 §4.2), so a customer who explicitly asked for an alert but granted no marketing consent is told nothing (F33). Reclassify as Transactional because the customer requested it? | **Owner** | **Marketing**, as settled upstream. The response returns `deliverability`, and HO-SHP-002 tells the customer how to receive it. Reclassifying is one catalogue line plus an Amended row to HO-NTF-001 |
| OQ-3 | Which food label fields are legally mandatory before sale (NFR-LEG-2, SRS Q-1)? | Owner / legal | Only country of origin blocks publication (US-SHP-03 AC3); the rest are checklist warnings |
| OQ-4 | Are catalogue prices tax-inclusive (SRS Q-5)? | Accountant | `commerce.pricesIncludeTax = false`, reading FR-ORD-5 as tax added on top |
| OQ-5 | Where do clinic consumables and dispensed medicines live as SKUs? | Owner + HO-EMR / HO-PHM authors | Products with `ListedOnStorefront = false`, category `HealthAndGrooming` until an SRS change adds a clinical category |
| OQ-6 | Is a diet authorisation per product (FR-PHM-9 "therapeutic food") or per SKU (`.ai/flows/prescription-diet-flow.md`)? | HO-PHM-001 author + clinical lead | The port carries `SkuId?` (null = any variant); HO-PHM-001 chooses |
| OQ-7 | Should "frequently bought together" be computed from orders? | Owner | Curated now; computed later by HO-ORD / HO-RPT through a new inbound contract |
| OQ-8 | Expiry display window | Owner | `shop.expiryDisplayWindowDays = 60` |
| OQ-9 | Recommendation thresholds and the condition keyword table — clinically acceptable? | Clinical lead | Kitten < 12 months; senior ≥ 7 years; weight management ≥ 6.00 kg; the §10.3 keyword map; labelled as guidance |
| OQ-10 | The protein source and health focus lists (§10.1) | Owner / shop manager | As listed; append-only enumerations |
| OQ-11 | Second language at launch (SRS Q-12) | Owner | None enabled; Bangla (`bn`) is used in tests only |
| OQ-12 | May a published product's slug change (with a redirect)? | Owner | No — locked after first publication (ADR-095) |
| OQ-13 | What counts as "purchased" for a review — delivered order, completed POS sale, or confirmed order? | Owner → HO-ORD-001 | Delivered online order line or completed POS sale |
| OQ-14 | May a bundle contain a prescription diet? | Owner + clinical lead | No — refused |
| OQ-15 | Is a 20,000-SKU performance fixture acceptable in the shared `catecommerce_dev` database? It is listed on the dev storefront under the brand "zz Perf Fixture" | Owner | Yes — seeded once, idempotently, never deleted (§16.1 rule 6) |
| OQ-16 | Appendix D gives Vets no catalogue access, but HO-PHM-001 needs vets to choose the therapeutic product they authorise | Owner → HO-PHM-001 | `Products.View` excludes Vet here; HO-PHM-001 adds a narrow read by amendment |
| ~~OQ-17~~ | **Closed 2026-09-23 — README C-5.** [`HO-FIL-001`](HO-FIL-001.md) owns upload, scanning, storage (GCS, ADR-318) and serving. This slice adds the `ProductImage` purpose and its `FileRules` row (5 MB; JPEG/PNG/WebP), stores the returned `fileId`, and renders through HO-FIL-002's components. `Media:PublicBaseUrl` and the placeholder path are withdrawn — images are served through signed URLs like every other file (HO-FIL-001 OQ-8 revisits public serving later) | — | — |
| OQ-18 | Do shop managers need to add categories? | Owner | No — FR-SHP-1 is the list (ADR-078) |
| OQ-19 | Free items (price 0), such as samples? | Owner | Refused (`PriceMustBePositive`) |
| OQ-20 | Should an unanswered stock alert expire? | Owner | No expiry |
| OQ-21 | **NFR-PRF-2 now depends on a view in another schema.** The catalogue filters and sorts 20,000 SKUs by availability inside 500 ms by joining `inventory.v_sellable_stock` (README C-1). HO-IMP-001 ADR-116 publishes the view but does not commit to an index that makes *this* query shape fast | HO-IMP-001 author + this slice | Measure first on the 20,000-SKU fixture in Phase D. If the join misses the budget, HO-IMP-001 adds the index (it owns the view); Shop does **not** reintroduce a local copy without a new ADR |

---

## 10. Enumerations, constants, settings and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `ProductCategory` | `CatFood`, `Litter`, `LitterPotsAndAccessories`, `BagsAndCarriers`, `HealthAndGrooming`, `ToysAndFurniture` |
| `ProductSubcategory` | §8.2's twenty members, each with `Category` |
| `ProductKind` | `Standard`, `Bundle` |
| `ProductStatus` | `Draft`, `Active`, `Archived` |
| `SkuStatus` | `Active`, `Discontinued` |
| `LifeStage` | `Kitten`, `Adult`, `Senior`, `AllLifeStages` |
| `ProteinSource` | `Chicken`, `Turkey`, `Duck`, `Beef`, `Lamb`, `Fish`, `Tuna`, `Salmon`, `Rabbit`, `Insect`, `PlantBased`, `Mixed`, `Other` |
| `HealthFocus` | `Sterilised`, `WeightManagement`, `Urinary`, `Renal`, `Digestive`, `SkinAndCoat`, `Hairball`, `Dental`, `Sensitive` |
| `RelatedProductKind` | `FrequentlyBoughtTogether`, `UpSell` |
| `ProductReviewStatus` | `PendingModeration`, `Published`, `Rejected`, `Hidden`, `Withdrawn` |
| `ReviewRejectionReason` | `OffTopic`, `Offensive`, `PersonalInformation`, `Spam`, `Other` |
| `StockAlertStatus` | `Pending`, `Notified`, `Closed` |
| `StockAlertClosureReason` | `CancelledByCustomer`, `SkuNoLongerSold`, `CustomerAnonymised`, `MergedDuplicate` |
| `AvailabilityStatus` | `InStock`, `OutOfStock` — **computed only** |
| `PurchaseBlockReason` | `OutOfStock`, `PrescriptionSignInRequired`, `PrescriptionAuthorisationRequired`, `NotOnSale` — **computed only** |
| `CatalogueSort` | `Relevance`, `Newest`, `PriceAscending`, `PriceDescending`, `Popularity`, `Rating` |
| `RecommendationReason` | `LifeStageMatch`, `Sterilised`, `WeightManagement`, `ConditionMatch` |

HO-NTF-001's `NotificationSubjectKind` gains **`StockAlert`** (append only).

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`

| Key | Group | Type | Default | Bounds | Branch override | Phase |
|---|---|---|---|---|---|---|
| `commerce.pricesIncludeTax` | Commerce | Boolean | `false` | — | ❌ | A |
| `shop.expiryDisplayWindowDays` | Shop | Integer | 60 | 0–365 (0 = never shown) | ❌ | C |
| `shop.recommendationKittenMaxAgeMonths` | Shop | Integer | 12 | 6–24 | ❌ | H |
| `shop.recommendationSeniorFromAgeYears` | Shop | Integer | 7 | 5–15 | ❌ | H |
| `shop.recommendationWeightManagementFromKg` | Shop | Decimal | 6.00 | 3.00–12.00 | ❌ | H |

A new `Shop` group is added to the registry's group list. Values are rows, so no migration is needed.
HO-ADM-001's `inventory.expiryAlertDays` is Inventory's staff alert and is **not** reused for the
customer-facing window.

Configuration (committed `appsettings.json`, **no secrets**): `Media:PublicBaseUrl` (empty),
`Localisation:ContentLocales` (`["en"]`), `Shop:Worker:Enabled` (`true`),
`Shop:Worker:IntervalSeconds` (120), `Shop:Catalogue:FacetCacheSeconds` (60).

### 10.3 Other constants

`ProductRules` (`MaxSkus = 100`, `MaxImages = 40`, `MaxTags = 20`, `MaxHealthFocus = 6`, lengths) ·
`BundleRules` (`MaxComponents = 20`, `MaxComponentQuantity = 20`, `MinComponentUnits = 2`) ·
`RelatedProductRules.MaxPerKind = 12` · `WishlistRules.MaxItems = 200` · `ReviewRules` (lengths) ·
`ShopSearchRules` (`WordSimilarityThreshold = 0.45`, `DidYouMeanSimilarity = 0.30`,
`MaxQueryLength = 100`, `MaxFilterValues = 20`, `MaxPageSize = 48`, `DefaultPageSize = 24`,
`PopularSuggestionCount = 8`) · `CountryCodes` (ISO-3166-1 alpha-2 → English name) ·
`HealthFocusKeywords` (e.g. `Urinary`: urinary, flutd, cystitis, bladder, struvite; `Renal`: kidney,
renal, ckd; `Digestive`: digestive, ibd, colitis, gastro; `SkinAndCoat`: dermatitis, skin, coat;
`WeightManagement`: obese, obesity, overweight; `Dental`: dental, gingivitis; `Hairball`: hairball)
· `ShopMessages` (two-path messages shared by handler pre-checks and the unique-constraint filter) ·
`ShopActors.Worker` (a fixed Guid for the worker's `UserContext`) · the `shop.back-in-stock`
definition appended to `NotificationTemplateCatalogue`.

### 10.4 Violation codes — append to `BusinessViolationCodes`

`ProductNotFound` · `SkuNotFound` · `InvalidSlug` · `SlugAlreadyInUse` · `SlugLockedAfterPublication`
· `InvalidProductCategory` · `CategoryLockedWhileActive` · `InvalidTag` · `TagLimitExceeded` ·
`SkuLimitReached` · `VariantAttributesRequired` · `DuplicateVariant` · `SkuCodeAlreadyInUse` ·
`InvalidBarcode` · `BarcodeAlreadyInUse` · `LastActiveSkuCannotBeDiscontinued` · `ImageLimitReached`
· `ImageAltTextRequired` · `InvalidImage` · `InvalidCountryCode` · `FoodDetailsOnlyForFood` ·
`InvalidGuaranteedAnalysis` · `PrescriptionOnlyForFood` · `CountryOfOriginRequired` ·
`ProductNotPublishable` · `InvalidProductTransition` · `PriceMustBePositive` · `CurrencyMismatch` ·
`OriginAlreadyExists` · `OriginNotFound` · `InvalidStockLevel` · `InvalidBundleComponent` ·
`BundleComponentLimitReached` · `RelatedProductInvalid` · `ReviewNotPermitted` · `AlreadyReviewed` ·
`ReviewNotFound` · `InvalidReviewTransition` · `ReviewRejectionReasonRequired` · `InvalidReviewText`
· `WishlistLimitReached` · `SkuAlreadyAvailable` · `StockAlertAlreadyRequested` ·
`StockAlertNotFound` · `PurchaseNotPermitted` · `LocaleNotEnabled` · `DefaultLocaleNotTranslatable`
· `InvalidCatalogueFilter`.

Reused, not duplicated: `PriceCannotBeBackdated`, `DuplicatePriceEffectiveDate`, `TaxClassNotFound`
(HO-ADM-001), `ActionNotPermitted` (HO-ACC-001).

`ProductNotPublishable`'s message lists every unmet blocking item, for example *"This product can't
be published yet: add the country of origin; set a price for SKU RC-K-2KG."* `ReviewNotPermitted`
says *"Only customers who bought this product can review it."* Neither leaks another customer's
data.

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Product/`

Namespace `CatEcommerce.Application.Commands.Product`. Data only; ids minted in the controller;
`ValidateCommand = true` everywhere (F3). Every staff command that edits a product carries
`ExpectedVersion`.

| # | Command | Sent by | Phase |
|---|---|---|---|
| C1 | `CreateProductCommand` (`ProductId`, `Kind`, `Name`, `Brand`, `Category`, `Subcategory?`, `TaxClassId`, `Slug`, `BundleSkuId?`, `BundleSkuCode?`) | Staff | A |
| C2 | `UpdateProductDetailsCommand` (all §8.3.3 `UpdateDetails` fields) | Staff | A |
| C3 | `ChangeProductSlugCommand` | Staff | A |
| C4 | `AddSkuCommand` (`SkuId`, `Code`, `Barcode?`, axes) · `UpdateSkuCommand` · `DiscontinueSkuCommand` · `ReactivateSkuCommand` | Staff | A |
| C5 | `SetProductImagesCommand` (`Images[]`) | Staff | A |
| C6 | `ScheduleSkuPriceCommand` (`SkuPriceId`, `SkuId`, `Amount`, `EffectiveFrom`) | Staff | A |
| C7 | `PublishProductCommand` · `UnpublishProductCommand` · `ArchiveProductCommand` | Staff | A |
| C8 | `SetFoodDetailsCommand` · `SetPrescriptionRequiredCommand` | Staff | B |
| C9 | `CreateSourcingOriginCommand` · `UpdateSourcingOriginCommand` | Staff | B |
| C10 | **`RecordProductSalesStatsCommand`** | **In-process only** — HO-ORD-001 (and tests) | C |
| C11 | `SubmitProductReviewCommand` (`ReviewId`, `ProductId`, `SkuId`, `Rating`, `Title?`, `Body`) · `EditProductReviewCommand` · `WithdrawProductReviewCommand` | Customer | D |
| C12 | `PublishProductReviewCommand` · `RejectProductReviewCommand` (`Reason`, `Note?`) · `HideProductReviewCommand` | Staff | D |
| C13 | `SetBundleComponentsCommand` · `SetRelatedProductsCommand` | Staff | F |
| C14 | `AddWishlistItemCommand` · `RemoveWishlistItemCommand` | Customer | G |
| C15 | `RequestStockAlertCommand` (`StockAlertId`, `SkuId`) · `CancelStockAlertCommand` | Customer | G |
| C16 | `NotifyStockAlertCommand` (`StockAlertId`) | Worker | G |
| C17 | `SetProductTranslationCommand` (`Locale`, fields) · `RemoveProductTranslationCommand` | Staff | H |

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: ids non-empty, lengths, patterns (slug, SKU code, tag, locale `^[a-z]{2}(-[A-Z]{2})?$`),
enum membership, `Amount` ≥ 0.01 with ≤ 2 dp, `Rating` 1…5, collection sizes, `EffectiveFrom` has an
offset. **Slug, code and barcode uniqueness, GTIN check digits, taxonomy parentage, publication
rules, purchase eligibility and "is in the future" are aggregate or handler rules.**

### 11.3 Mappers and DTOs

`ProductMapper`, `ProductReviewMapper`, `ShopCustomerMapper` (static `ToDomainDto()` extensions,
`UserContext` / `CorrelationId` passed through). DTOs in `Domain/Models/` (flat), one per aggregate
method, plus `PublicationFacts`, `BundleComponentFact`, `DietAuthorisationFact`, `CatRecommendationFacts`,
`RecommendationCandidate`.

### 11.4 Command handlers — `CommandHandlers/Product/`

The `RegisterCatCommandHandler` shape. Cross-record rules in the handler:

| Handler | Specifics |
|---|---|
| C1 Create | Slug and (bundle) SKU-code uniqueness pre-checks (`SlugAlreadyInUse`, `SkuCodeAlreadyInUse`), backed by the unique indexes and the filter mapping; tax class exists and is active (HO-ADM-001 repository → `TaxClassNotFound`) |
| C2 Update | `ExpectedVersion`; tax class check when changed; country code validity is the aggregate's |
| C4 SKUs | Code / barcode uniqueness pre-checks across all products (repository filter on the owned table) |
| C6 Schedule price | SKU exists (any product); `EffectiveFrom ≥ now − 60 s`; currency from `IBusinessSettings`; duplicate `EffectiveFrom` pre-check |
| C7 Publish | Builds `PublicationFacts`: the ids of active SKUs **priced now** (price repository), the tax class's `IsActive`, and for a bundle the component facts; `Publish(facts, now)` |
| C9 Origins | Country code in `CountryCodes`; uniqueness pre-check |
| C10 Record stock / sales | SKU (or product) exists → else `SkuNotFound` / `ProductNotFound`; load-or-create the copy; `Apply`; save only if changed. **No event, no enqueue.** A first-write race on the same id is a `409`, which the caller's next push supersedes |
| C11 Submit review | Product `Active`; SKU belongs to it; `IPurchaseHistoryReader` says the customer bought that SKU → else `ReviewNotPermitted`; `AlreadyReviewed` pre-check. Edit/withdraw: own review only (`404` otherwise) |
| C12 Moderate | Load; transition; `ModeratedBy` = caller |
| C13 Bundle / related | Loads component products (by SKU id) or related products and builds facts; related products must exist, and linked products that are not yet active are allowed but never shown until they are |
| C14 Wishlist | SKU exists and belongs to an existing product; load-or-create the wishlist (a first-write race → `409`) |
| C15 Request alert | SKU on sale; **availability now** is `OutOfStock` (else `SkuAlreadyAvailable`); a prescription product needs an eligible cat (gate) → else `PurchaseNotPermitted`; pending pre-check; after the save, the controller dispatches `GetStockAlertDeliverabilityQuery` for the response |
| C16 Notify | Alert `Pending` (else success, no write); SKU still `InStock` and on sale (else success, no write — the next pass retries); load product slug and names; `MarkNotified`; **`INotificationOutbox.Enqueue`** (`shop.back-in-stock`, recipient the customer, subject `StockAlert`); `UpdateAsync` |
| C17 Translation | Enabled locales from `IOptions<LocalisationOptions>`; `ExpectedVersion` |

### 11.5 Domain events — `Events/Product/`

Ids, instants, enums, numbers and booleans only — **no names, descriptions, ingredients, review
text or notes** (HO-ACC-001 U-X1 reflects assembly-wide; U-X1 here repeats it for `Events/Product`).

`ProductCreatedEvent` · `ProductDetailsUpdatedEvent` · `ProductFoodDetailsUpdatedEvent` ·
**`ProductPrescriptionRequirementChangedEvent`** (`ProductId`, `PrescriptionRequired`) ·
`ProductSlugChangedEvent` · **`SkuAddedEvent`** (`ProductId`, `SkuId`) · `SkuUpdatedEvent` ·
**`SkuDiscontinuedEvent`** · `SkuReactivatedEvent` · `ProductImagesChangedEvent` ·
**`BundleCompositionChangedEvent`** · `RelatedProductsChangedEvent` · `ProductTranslationChangedEvent`
(`ProductId`, `Locale`) · **`ProductPublishedEvent`** (`ProductId`, `FirstPublication`) ·
`ProductUnpublishedEvent` · **`ProductArchivedEvent`** · **`SkuPriceScheduledEvent`** (`SkuId`,
`EffectiveFrom`) · `SourcingOriginCreatedEvent` · `SourcingOriginUpdatedEvent` ·
`ProductReviewSubmittedEvent` · `ProductReviewPublishedEvent` · `ProductReviewRejectedEvent`
(`Reason`) · `ProductReviewHiddenEvent` · `ProductReviewWithdrawnEvent` · `WishlistItemAddedEvent` ·
`WishlistItemRemovedEvent` · `StockAlertRequestedEvent` · `StockAlertNotifiedEvent` ·
`StockAlertClosedEvent` (`Reason`).

Bold events are contracts other areas may subscribe to (§8.12).

### 11.6 Domain event handlers — `DomainEventHandlers/Product/`

| Handler | Handles | Does |
|---|---|---|
| `CloseStockAlertsOnSkuDiscontinuedHandler` | `SkuDiscontinuedEvent` | Pending alerts for the SKU → `Close(SkuNoLongerSold)`, one `UpdateAsync` each; idempotent |
| `CloseStockAlertsOnProductArchivedHandler` | `ProductArchivedEvent` | Same for every SKU of the product |
| `ReassignShopDataOnCustomerMergedHandler` | HO-ACC-001 `CustomerMergedEvent` | Duplicate's wishlist items → added to the survivor's (union, the cap respected: the oldest duplicate items beyond 200 are dropped and counted in the audit row), duplicate wishlist emptied; pending alerts → survivor, or closed `MergedDuplicate` when the survivor already has one for that SKU; reviews → survivor, or the duplicate's `Withdrawn` when the survivor already reviewed the product. Idempotent (re-send finds nothing to move) |
| `ClearShopDataOnCustomerAnonymisedHandler` | HO-ACC-001 `CustomerAccountAnonymisedEvent` | Wishlist cleared; pending alerts `Close(CustomerAnonymised)`; reviews untouched (their author renders as "A verified buyer" — ADR-096) |
| `AuditReviewModerationHandler` | `ProductReviewPublishedEvent`, `ProductReviewRejectedEvent`, `ProductReviewHiddenEvent` | `BusinessEvent` audit row with the moderator, the reason and the note. The note lives here, not in the event |
| `AuditPrescriptionFlagHandler` | `ProductPrescriptionRequirementChangedEvent` | `BusinessEvent` row — changing who may buy a therapeutic food is a safety-relevant act |

All loop with one `UpdateAsync` per row and **never enqueue** (HO-NTF-001 ADR-057). Each carries the
summary comment that a failure fails the request after the source change committed, and that
re-sending the source command is the recovery (`.ai/domain-events-and-outbox.md` §3). Data changes
are audited by HO-ACC-001's interceptor.

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

`DbSet`s for the eight roots; one configuration per root in `Persistence/Configurations/`, modelled
on HO-ACC-001 §12.1: schema `product`, `ValueGeneratedNever()` on roots and owned ids, the five F15
lines, enumerations by `Value`, `DateOnly` → `date`, instants → `timestamptz`, `Money` as
`amount numeric(12,2)` + `currency_code char(3)`, primitive collections as `text[]` / `integer[]`,
owned collections via `OwnsMany` with their own tables. The three views are keyless entity types
mapped with `ToView` and `HasNoKey` (`.ai/read-side-pattern.md` §5). **They are never modified from
the read side.**

| Index / object | Kind |
|---|---|
| `product.slug` | **UNIQUE** `ix_product_slug` |
| `product (status, listed_on_storefront, category, subcategory)` | filter |
| `product (country_of_origin_code)`, `(brand)`, `(life_stage)`, `(protein_source)` | filter (filtered `WHERE status = Active`) |
| `product (first_published_on DESC)` | newest |
| `product.search_text` | **GIN `gin_trgm_ops`** `ix_product_search_text_trgm` (phase E, raw SQL) |
| `sku.id` | **UNIQUE** (cross-context lookup — ADR-079) |
| `sku.code` | **UNIQUE** `ix_sku_code` |
| `sku.barcode` | **UNIQUE filtered `WHERE barcode IS NOT NULL`** `ix_sku_barcode` |
| `sku (product_id, status)` | |
| `sku_price (sku_id, effective_from)` | **UNIQUE** `ix_sku_price_sku_effective`; plus `(sku_id, effective_from DESC)` |
| `sourcing_origin.country_code` | **UNIQUE** `ix_sourcing_origin_country` |
| `sku_stock_status.id` | PK (== SKU id); `(sellable_quantity)` partial `WHERE sellable_quantity > 0` |
| `product_review (product_id, status, submitted_on DESC)` | public list, rating view |
| `product_review (customer_id, product_id)` | **UNIQUE filtered `WHERE status <> Withdrawn`** `ix_product_review_active_per_customer_product` |
| `product_review (status, submitted_on)` | moderation queue |
| `wishlist_item (wishlist_id, sku_id)` | **UNIQUE** |
| `stock_alert (customer_id, sku_id)` | **UNIQUE filtered `WHERE status = Pending`** `ix_stock_alert_pending_per_customer_sku` |
| `stock_alert (status, sku_id)` | worker pass |
| `bundle_component (component_sku_id)` | availability join, "which bundles contain this SKU" |

Status values in filtered predicates and in the view SQL are **the `Enumeration.Value` integers,
written from the enumeration in migration code** (`ProductStatus.Active.Value`, …), never typed as
literals. U-X5 checks this.

**Views** (raw SQL in the migration, each with a reversible `Down`):
- `product.storefront_catalogue_entry` — one row per `Active` and listed product: identity and
  filter columns; `min_price` / `max_price` from a `LATERAL` "greatest `effective_from ≤ now()`"
  price per active SKU (for a bundle, its one SKU); `in_stock` (standard: `EXISTS` an active SKU
  with `sellable_quantity > 0`; bundle: `NOT EXISTS` a component with
  `COALESCE(sellable_quantity,0) / quantity < 1` or a component not on sale); `rating_average`,
  `rating_count` (from the rating view); `units_sold_90d` (`COALESCE` from the sales copy, 0);
  primary image key and alt text; `search_text`.
- `product.product_rating_summary` — `product_id`, `round(avg(rating),1)`, `count(*)` over
  `Published` reviews.
- `product.catalogue_term` — `DISTINCT` lower-cased brand and product name of active, listed
  products.

**Migrations**, each read before commit and confirmed to touch only `product.*` (plus the
extension): `AddProductCatalogue` (A — product, sku, product_image, sku_price, search_text column),
`AddFoodDetailsAndOrigins` (B), `AddCatalogueFigures` (C — stock and sales copies),
`AddProductReviews` (D), `AddStorefrontCatalogue` (E — `CREATE EXTENSION IF NOT EXISTS pg_trgm;`,
the trigram index, the three views; `Down` drops the views and index but **not** the extension),
`AddBundlesAndRelated` (F — bundle_component, related_product; **re-creates** the catalogue view with
the bundle clause), `AddWishlistAndStockAlerts` (G), `AddProductTranslations` (H).

### 12.2 Infrastructure services and the worker

| Class | Notes |
|---|---|
| `Product/NoDietAuthorisationsReader : IDietAuthorisationGate` | Returns empty. Summary comment: *"Fail-closed placeholder until HO-PHM-001 registers the real reader."* A startup `Information` log states that prescription products are not purchasable |
| `Product/NoPurchaseHistoryReader : IPurchaseHistoryReader` | Returns empty; same pattern for HO-ORD-001 |
| `Media/ConfiguredMediaUrlBuilder : IMediaUrlBuilder` | `Media:PublicBaseUrl` absolute **https** outside Development (validated on start when set); empty → null URLs |
| `Configuration/LocalisationOptions` | `ContentLocales` (≥ 1, each matching the locale pattern, distinct), `ValidateOnStart` |
| `Notifications/Guards/StockAlertGuard` | §8.11; reads only |
| `Notifications/NotificationLinkBuilder` | `ProductLink(slug, skuId)` added (§13) |

**`ShopWorker : BackgroundService`** (Api, `Background/`) has the same shape and rules as HO-NTF-001's
`NotificationWorker` and HO-APT-001's `ClinicWorker`: a scope per unit of work, never throws out of
`ExecuteAsync`, disabled in `Testing`, and `ShopWorkCycle` is callable by tests. One pass:
`GetDueStockAlertsQuery` (`Pending` alerts whose SKU is `InStock`, oldest first, ≤ 500 per pass) →
`NotifyStockAlertCommand` per alert.

### 12.3 Authorization

Appendix D "Product catalogue": Customer **R**, Reception **R**, Inventory **R**, Shop Mgr **F**,
Admin **F**; Nurse, Vet, Caretaker, Accountant **—**. Appended to the one `ActionRoles` table:

| Action | Staff roles | Endpoints |
|---|---|---|
| `Products.View` | Reception, Inventory, ShopManager, Administrator | staff product list, detail, price history, barcode lookup, CSV export, countries list |
| `Products.Manage` | ShopManager, Administrator | every product, SKU, image, food, prescription, bundle, related and translation write; publish / unpublish / archive |
| `Products.ManagePrices` | ShopManager, Administrator | schedule a SKU price |
| `SourcingOrigins.Manage` | ShopManager, Administrator | origin writes (reads are public) |
| `Reviews.Moderate` | ShopManager, Administrator | moderation queue, publish, reject, hide |

Public catalogue reads carry `[PublicEndpoint]` **and** `[StorefrontEndpoint]`. Customer shop
endpoints carry `[RequireCustomerSession]` **and** `[StorefrontEndpoint]`, and every handler
resolves by id **and** caller: another customer's review, alert, wishlist or cat is **`404`**
(HO-ADM-001 §12.3's rule). **No staff endpoint carries `[StorefrontEndpoint]`** (ADR-093).

Rate limits (HO-ACC-001 ADR-030's shape): `catalogue` 120 per minute per IP on public list, search,
filters and detail; `reviews` 5 per hour per customer on submit and edit; `stock-alerts` 30 per hour
per customer.

### 12.4 Api — the contract HO-SHP-002 consumes

JSON camelCase; instants ISO-8601 with offset; dates `yyyy-MM-dd`; money
`{ "amount": "1500.00", "currencyCode": "BDT" }`; paged lists `{ items, pageNumber, pageSize,
totalCount }`; enumerations by name. Image objects: `{ url | null, altText, isPrimary, skuId? }`.

**Public — `[PublicEndpoint]` `[StorefrontEndpoint]`**

| Method | Route | Returns |
|---|---|---|
| `GET` | `/api/shop/products?q&category&subcategory&brand&origin&lifeStage&protein&minPrice&maxPrice&availability&minRating&sort&pageNumber&pageSize&locale` | Paged `CatalogueItem`: `productId`, `slug`, `name`, `brand`, `category`, `subcategory?`, `kind`, `countryOfOrigin? { code, name }`, `lifeStage?`, `prescriptionRequired`, `fromPrice`, `priceVaries`, `availability`, `rating { average?, count }`, `primaryImage?`. Plus `appliedFilters` (normalised echo, incl. `sort`) and, on zero results with `q`, `suggestions { didYouMean[], popular[] }`. `400 InvalidCatalogueFilter` names the parameter. `Cache-Control: public, max-age=60` |
| `GET` | `/api/shop/filters?category` | `brands[{ name, count }]`, `origins[{ code, name, count }]`, `lifeStages[{ value, count }]`, `proteinSources[{ value, count }]`, `price { min, max }` |
| `GET` | `/api/shop/categories` | The taxonomy (§8.2) with `productCount` per category and subcategory |
| `GET` | `/api/shop/origins` | Shop by Country: `[{ code, name, sourcingNotes?, productCount, isFeatured }]`, featured first, then `sortOrder`, then name — **US-SHP-05 AC1** |
| `GET` | `/api/shop/origins/{countryCode}` | One origin (`404` when it has no active food product) |
| `GET` | `/api/shop/products/{slug}?locale` | `ProductDetail`: `productId`, `slug`, `name`, `brand`, `description` (LimitedMarkup), `category`, `subcategory?`, `tags[]`, `kind`, `countryOfOrigin?`, `prescriptionRequired`, `food? { manufacturer?, lifeStage?, proteinSource?, ingredientList?, guaranteedAnalysis? { proteinPercent, fatPercent, fibrePercent, moisturePercent }, feedingGuide?, storageInstructions? }`, `images[]`, `skus[{ skuId, code, label, sizeOrWeight?, flavour?, colour?, packQuantity?, price?, availability, expiryNotice? { expiresOn }, images[] }]` (active SKUs only), `bundle? { components[{ productName, productSlug, skuLabel, quantity, availability }] }`, `rating`, `pricesIncludeTax`, `locale`, `fieldLocales`. `404` for draft, archived, unlisted or unknown |
| `GET` | `/api/shop/products/{slug}/reviews?pageNumber&pageSize` | Paged published reviews: `rating`, `title?`, `body`, `authorDisplayName` ("Rahim K."; "A verified buyer" for anonymised or merged-away authors), `skuLabel`, `submittedOn`, `verifiedPurchase: true` |
| `GET` | `/api/shop/products/{slug}/recommendations` | `{ frequentlyBoughtTogether[], upSell[], lifeStage { basis, lifeStages[], items[] } }` of `CatalogueItem`, with §8.10's exclusions. With a customer session: `Cache-Control: private, no-store` |
| `GET` | `/api/shop/locales` | `{ defaultLocale, locales[] }` |

**Customer — `[RequireCustomerSession]` `[StorefrontEndpoint]`** (`no-store`)

| Method | Route | Command / query → success |
|---|---|---|
| `GET` | `/api/customers/me/shop/products/{slug}/purchasability` | `{ skus[{ skuId, purchasable, purchaseBlockReason?, eligibleCats[{ catId, name, validTo }] }] }` — **US-SHP-10 AC1** |
| `GET` | `/api/customers/me/shop/products/{slug}/review-eligibility` | `{ canReview, reason? ("NotPurchased" \| "AlreadyReviewed"), purchasedSkus[{ skuId, label }], existingReviewId? }` |
| `GET` / `POST` | `/api/customers/me/reviews` | the caller's reviews with status / C11 → `201 { reviewId, status }`; `400 ReviewNotPermitted`, `AlreadyReviewed` |
| `PUT` / `DELETE` | `/api/customers/me/reviews/{reviewId}` | edit → `200 { status: "PendingModeration" }` / withdraw → `204` |
| `GET` | `/api/customers/me/wishlist` | `items[{ skuId, productId, slug, productName, skuLabel, price?, availability, onSale, addedOn, primaryImage? }]` |
| `PUT` | `/api/customers/me/wishlist/items/{skuId}` | C14 add → `204` (idempotent) |
| `DELETE` | `/api/customers/me/wishlist/items/{skuId}` | C14 remove → `204` |
| `GET` | `/api/customers/me/stock-alerts` | `[{ stockAlertId, skuId, slug, productName, skuLabel, status, requestedOn, notifiedOn? }]` |
| `POST` | `/api/customers/me/stock-alerts` | C15 → `201 { stockAlertId, deliverability { willNotify, channels[], inboxCopy } }`; `400 SkuAlreadyAvailable`, `StockAlertAlreadyRequested`, `PurchaseNotPermitted` |
| `DELETE` | `/api/customers/me/stock-alerts/{stockAlertId}` | C15 cancel → `204` |
| `GET` | `/api/customers/me/cats/{catId}/food-recommendations?locale` | `{ cat { catId, name, lifeStage?, lifeStageIsEstimated }, items[{ item: CatalogueItem, reasons[{ code, values }] }], excludedForAllergy[{ allergen, count }], hints[] }` — **US-SHP-11**; `404` for a cat not the caller's |

**Staff** — branch-independent (the catalogue is not branch-scoped: HO-ADM-001 §8.2 scopes things
that exist *at a place*; stock locations are Inventory's)

| Method | Route | Action | Command / query → success |
|---|---|---|---|
| `GET` | `/api/staff/shop/products?q&status&category&brand&barcode&kind&listed&sort&pageNumber&pageSize` | `Products.View` | paged `StaffProductSummary` (`productId`, `name`, `brand`, `category`, `status`, `kind`, `skuCount`, `listedOnStorefront`, `lastUpdatedOn`) — UI-4 |
| `GET` | `/api/staff/shop/products/export.csv?…same filters` | `Products.View` | streamed CSV, ≤ 50 000 rows (`413` above), HO-ADM-001 ADR-051's injection guard |
| `POST` | `/api/staff/shop/products` | `Products.Manage` | C1 → `201 { productId }` |
| `GET` | `/api/staff/shop/products/{productId}` | `Products.View` | full detail incl. drafts, `version`, `publishChecklist[{ item, isBlocking, isMet, message }]`, translations, related, bundle components, and per SKU: `currentPrice?`, `scheduledPrice?`, `stock { sellableQuantity, nextAllocatableExpiryOn?, asOf } \| null` (**null = never reported**, not zero) |
| `PUT` | `/api/staff/shop/products/{productId}` | `Products.Manage` | C2 → `204`; `409` stale version |
| `PUT` | `…/{productId}/slug` · `…/food` · `…/prescription` · `…/images` · `…/bundle-components` · `…/related-products` | `Products.Manage` | C3 / C8 / C5 / C13 → `204` |
| `PUT` / `DELETE` | `…/{productId}/translations/{locale}` | `Products.Manage` | C17 → `204` |
| `POST` | `…/{productId}/skus` · `PUT …/skus/{skuId}` · `POST …/skus/{skuId}/discontinue` · `…/reactivate` | `Products.Manage` | C4 → `201 { skuId }` / `204` |
| `POST` | `…/{productId}/publish` · `/unpublish` · `/archive` | `Products.Manage` | C7 → `204`; `400 ProductNotPublishable` |
| `GET` / `POST` | `/api/staff/shop/skus/{skuId}/prices` | `Products.View` / `Products.ManagePrices` | history (`current`, `scheduled[]`, `past[]`) / C6 → `201 { skuPriceId }` |
| `GET` | `/api/staff/shop/skus?barcode=` | `Products.View` | `FindSkuByBarcodeQuery` → `{ skuId, code, label, productId, productName, status }` or `404` — HW-2 |
| `GET` | `/api/staff/shop/countries` | `Products.View` | `CountryCodes` as `[{ code, name }]` |
| `GET` / `POST` / `PUT` | `/api/staff/shop/origins` (· `/{countryCode}`) | `Products.View` / `SourcingOrigins.Manage` | list incl. products-less origins / C9 |
| `GET` | `/api/staff/shop/reviews?status&productId&pageNumber&pageSize` | `Reviews.Moderate` | paged queue: product, SKU label, rating, title, body, customer display name, submitted on, status |
| `POST` | `/api/staff/shop/reviews/{reviewId}/publish` · `/reject` · `/hide` | `Reviews.Moderate` | C12 → `204` |

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes (R1–R8). Maintenance mode →
`503` on storefront routes (HO-ADM-001 §12.5).

### 12.5 Filters

Extend HO-ACC-001's `UniqueConstraintExceptionFilter` by constraint name: `ix_product_slug` →
`400 SlugAlreadyInUse`; `ix_sku_code` → `400 SkuCodeAlreadyInUse`; `ix_sku_barcode` →
`400 BarcodeAlreadyInUse`; `ix_sku_price_sku_effective` → `400 DuplicatePriceEffectiveDate`;
`ix_sourcing_origin_country` → `400 OriginAlreadyExists`;
`ix_product_review_active_per_customer_product` → `400 AlreadyReviewed`;
`ix_stock_alert_pending_per_customer_sku` → `400 StockAlertAlreadyRequested`. A first-write race on a
wishlist or a stock/sales copy → `409`. Messages come from `ShopMessages`, so the handler and the
filter cannot drift. Unknown constraints still rethrow.

### 12.6 Read side — `src/Read/`

Queries in `Queries/Product/`, handlers in `QueryHandlers/Product/`, ViewModels flat; explicit
member-by-member projections; `AsNoTracking()`.

- `GetCatalogueQuery`, `GetCatalogueFiltersQuery`, `GetCategoriesQuery`, `GetOriginsQuery`,
  `GetOriginQuery` — over the storefront view (§8.8). **Filtering, sorting and paging are SQL.**
  Nothing is filtered in memory after `ToListAsync`.
- `GetProductDetailQuery` (by slug; localisation through `ProductContentLocalisation`; availability
  and expiry through `SkuAvailabilityRules`; image URLs through `IMediaUrlBuilder`),
  `GetProductReviewsQuery`, `GetProductRecommendationsQuery`, `GetLocalesQuery`.
- Customer: `GetPurchasabilityQuery`, `GetReviewEligibilityQuery`, `GetMyReviewsQuery`,
  `GetWishlistQuery`, `GetMyStockAlertsQuery`, `GetStockAlertDeliverabilityQuery`,
  `GetFoodRecommendationsQuery`. The last reads the cat from `clinic.cat` (allergies, conditions,
  date of birth and precision, neuter status, life status, owner) and the latest weight from
  `clinic.cat_weight_record`, read-only (`.ai/read-side-pattern.md` §5: reading widely is fine).
- Staff: `GetStaffProductsQuery`, `ExportStaffProductsQuery`, `GetStaffProductDetailQuery` (the
  checklist from `ProductPublicationRules`), `GetSkuPriceHistoryQuery`, `GetStaffOriginsQuery`,
  `GetReviewModerationQueueQuery`, `GetCountriesQuery`.
- Contracts (§8.12): `GetSkuSaleFactsQuery`, `GetSkuCatalogueFactsQuery`, `FindSkuByBarcodeQuery`,
  `CheckPrescriptionEligibilityQuery`, `GetBundleCompositionQuery`.
- Worker: `GetDueStockAlertsQuery`.

The author display name is derived at read time from `account.customer_account` (`FullName` → first
name + initial of the last word), and is "A verified buyer" when the account is `Anonymised` or
`Merged`. **No review read exposes a customer id, mobile or email.**

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler and event handler in §11–§12.6;
the pure domain services (Scoped); `IDietAuthorisationGate` → `NoDietAuthorisationsReader`,
`IPurchaseHistoryReader` → `NoPurchaseHistoryReader`, `IMediaUrlBuilder` (Scoped) — **each on its own
line, so HO-PHM-001 and HO-ORD-001 replace exactly one line**; `StockAlertGuard` into HO-NTF-001's
`SubjectGuardResolver`; the template through the catalogue; `LocalisationOptions` with
`ValidateOnStart`; `ShopWorkCycle` (Scoped); **`AddHostedService<ShopWorker>()`**; the `catalogue`,
`reviews` and `stock-alerts` rate-limit policies. `ValidateScopes` / `ValidateOnBuild` stay on.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the seventeen enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Enums/NotificationSubjectKind.cs                               ← StockAlert only
src/Shared/CatEcommerce.Shared/Common/Constants/{ProductRules,BundleRules,RelatedProductRules,WishlistRules,ReviewRules,ShopSearchRules,CountryCodes,HealthFocusKeywords,ShopMessages,ShopActors}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs       ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                                 ← the five keys and the Shop group of §10.2 only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs                     ← shop.back-in-stock only
src/Domain/CatEcommerce.Domain/Aggregates/Product/**
src/Domain/CatEcommerce.Domain/Entities/{Sku,ProductImage,BundleComponent,RelatedProductLink,ProductTranslation,WishlistItem}.cs
src/Domain/CatEcommerce.Domain/ValueObjects/{FoodDetails,GuaranteedAnalysis}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{ProductPublicationRules,SkuPricing,SkuAvailabilityRules,PrescriptionPurchaseGate,RecommendationExclusionPolicy,FoodRecommendationPolicy,AllergenMatcher,ProductContentLocalisation,CatalogueQuery,IDietAuthorisationGate,IPurchaseHistoryReader,IMediaUrlBuilder}.cs
src/Domain/CatEcommerce.Domain/DomainServices/INotificationLinkBuilder.cs                             ← ProductLink only
src/Domain/CatEcommerce.Domain/Models/**                                                             ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Product/**
src/Application/CatEcommerce.Application/Commands/Product/**
src/Application/CatEcommerce.Application/CommandHandlers/Product/**
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Product/**
src/Application/CatEcommerce.Application/DataMappers/{ProductMapper,ProductReviewMapper,ShopCustomerMapper}.cs
src/Read/CatEcommerce.Read/Queries/Product/**
src/Read/CatEcommerce.Read/QueryHandlers/Product/**
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs                  ← DbSets and the three view entity sets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{one per new root and per view}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                              ← the eight migrations of §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Product/{NoDietAuthorisationsReader,NoPurchaseHistoryReader}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Media/ConfiguredMediaUrlBuilder.cs
src/Infrastructure/CatEcommerce.Infrastructure/Configuration/{LocalisationOptions,MediaOptions}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Guards/StockAlertGuard.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs               ← ProductLink only
src/Api/CatEcommerce.Api/Controllers/{ShopCatalogue,ShopOrigins,CustomerShop,CustomerReviews,CustomerWishlist,CustomerStockAlerts,CustomerFoodRecommendations,StaffProducts,StaffSkus,StaffOrigins,StaffReviews}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Product/**
src/Api/CatEcommerce.Api/Background/{ShopWorker,ShopWorkCycle}.cs
src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs                                  ← §12.5 mappings only
src/Api/CatEcommerce.Api/appsettings.json                                                            ← Media, Localisation, Shop sections only
src/Api/CatEcommerce.Api/Program.cs                                                                  ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-SHP-001.md
docs/gates/HO-SHP-001-*.md
.ai/decisions/adr.md                                                                                 ← ADR-077…096, append only
```

**Amendments this slice makes to other areas' code** are confined to the lines marked above: two
settings files, the template catalogue, one enum member, one link-builder method, the filter
mappings and the `ActionRoles` rows. Each needs an **Amended row** in the owning handover's §1
(HO-ADM-001, HO-NTF-001, HO-ACC-001). **The coordinating author appends those rows — not this build,
and not this document** (see the report accompanying this handover).

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-1) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs. A conflict is a §20.7 stop |
| `web/**` | HO-SHP-002 |
| Every file of HO-ACC-001, HO-ADM-001, HO-CAT-001, HO-NTF-001 and HO-APT-001 beyond the lines in §13 | Their areas. **No Cat, Account, Administration or Notification aggregate is edited**; cats are read, never written |
| Any batch, stock ledger, location, reservation, supplier or consignment type; any write to `inventory.*` | HO-IMP-001 (ADR-082). Shop reads exactly one object there: the view `v_sellable_stock` |
| Any cart, order, order line, payment, invoice or refund type | HO-ORD-001 / HO-BIL-001; no payment gateway in v1 |
| Any diet-authorisation, prescription or formulary type | HO-PHM-001 (ADR-084) |
| An upload endpoint, `IFormFile`, an object-storage client, a file write | ADR-091, OQ-17 |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |

---

## 15. Unit test cases

| ID | Case | Expected |
|---|---|---|
| **A — catalogue core** | | |
| U-A1 | `Create` with every FR-SHP-2 field | Draft; fields as given; tags normalised; `SearchText` contains name, brand and tags |
| U-A2 | Every §8.2 subcategory with its parent; `Litter` + `Dry` | Accepted; `InvalidProductCategory` — **US-SHP-01 AC3** |
| U-A3 | Two SKUs, the second with no axis; two SKUs "2 kg · Salmon" and "2 KG · salmon"; the 101st SKU | `VariantAttributesRequired`; `DuplicateVariant`; `SkuLimitReached` |
| U-A4 | Barcodes `5901234123457` (valid EAN-13), `5901234123458`, `ABC-123`, `12` | Valid; `InvalidBarcode`; valid; `InvalidBarcode` |
| U-A5 | Images: no alt text; two primaries; `SkuId` of another product; `image/gif`; 41 images | `ImageAltTextRequired`; `InvalidImage`; `InvalidImage`; `InvalidImage`; `ImageLimitReached` |
| U-A6 | Status table over every `(from, action)` pair | Only §8.4's arrows; others `InvalidProductTransition` naming both states |
| U-A7 | `Publish` with no active SKU; with an unpriced active SKU | `ProductNotPublishable` listing `ActiveSku`; listing the SKU code under `PricedSkus` |
| U-A8 | `ChangeSlug` before and after first publication | Allowed; `SlugLockedAfterPublication` |
| U-A9 | Discontinue the only active SKU of an `Active` product; of a `Draft` | `LastActiveSkuCannotBeDiscontinued`; allowed |
| U-A10 | Change flavour, name, tags, translation name | `SearchText` refreshed each time, lower-cased, whitespace collapsed |
| U-A11 | `SkuPricing.PriceAt` with rows at t−2d, t−1d, t+1d, queried at t; at exactly t+1d | The t−1d row; the t+1d row |
| U-A12 | Schedule price: `EffectiveFrom` 2 min ago; 30 s ago; 0.00; USD with `commerce.currencyCode = BDT` | `PriceCannotBeBackdated`; accepted (skew tolerance); `PriceMustBePositive`; `CurrencyMismatch` |
| U-A13 | 21 tags; tag `Grain Free` | `TagLimitExceeded`; normalised to `grain-free` |
| U-A14 | `ProductPublicationRules` on a complete non-food product | Every blocking item met; `PrimaryImage` warning only when no image |
| **B — food and origin** | | |
| U-B1 | `SetFoodDetails` on a `Litter` product | `FoodDetailsOnlyForFood` |
| U-B2 | Guaranteed analysis 32/15/4/10; 60/30/10/5; 101/0/0/0; three of four | Accepted; `InvalidGuaranteedAnalysis` (sum); invalid (bound); invalid (all or none) |
| U-B3 | Publish a food product with everything but country of origin | `ProductNotPublishable` whose message contains *"country of origin"* — **US-SHP-03 AC3** |
| U-B4 | Clear the country of origin of an `Active` food product | `CountryOfOriginRequired` |
| U-B5 | `SetPrescriptionRequired(true)` on food; on `Litter` | Event raised; `PrescriptionOnlyForFood` — **US-SHP-03 AC2** |
| U-B6 | Country codes `DE`, `de`, `XX` | Accepted; normalised `DE`; `InvalidCountryCode` |
| U-B7 | Food with `IsExpiryTracked = false` requested | Stored `true` |
| U-B8 | Food missing ingredients and analysis but with country of origin and life stage | Publishable; `LabellingComplete` warning lists the missing fields (OQ-3) |
| U-B9 | Change category of an `Active` product | `CategoryLockedWhileActive` |
| **C — availability** | | |
| U-C1 | `SkuStockStatus.Apply`: newer, older, same `AsOf`, negative | Applied; ignored; no change; `InvalidStockLevel` |
| U-C2 | `SkuAvailabilityRules`: sellable 3; 0; no row; SKU discontinued; product archived | `InStock`; `OutOfStock`; `OutOfStock`; not listed; not listed |
| U-C3 | Expiry notice: window 60, expiry today+60; today+61; window 0; expiry null | Shown; not shown; never shown; none — **US-SHP-07 AC2** |
| U-C4 | Two SKUs, one with sellable 0 | Each has its own status — **US-SHP-02 AC2** |
| **D — reviews** | | |
| U-D1 | Rating 0 and 6; body 2001 chars; body with a `NUL` char; body with line breaks | Invalid ×3; accepted |
| U-D2 | Review status table | Only §8.11's arrows |
| U-D3 | Reject with no reason | `ReviewRejectionReasonRequired` |
| **E — search** | | |
| U-E1 | `CatalogueQuery.Normalise("  Royal   CANIN%_ ")` | `royal canin\%\_` |
| U-E2 | Sort defaults with and without `q` | `Relevance`; `Newest` |
| U-E3 | `minPrice` > `maxPrice`; 21 brands; `lifeStage=Puppy`; `minRating=6` | `InvalidCatalogueFilter` naming the parameter, each |
| **F — gate, bundles, related** | | |
| U-F1 | Gate: non-Rx; Rx guest; Rx with no authorisation; Rx with one for an alive owned cat valid today; valid yesterday; for a deceased cat; for a cat now owned by someone else; with `SkuId` = another variant | Purchasable; `PrescriptionSignInRequired`; `PrescriptionAuthorisationRequired`; eligible [cat]; required; required; required; required — **BR-2** |
| U-F2 | Bundle availability: components (A×2 with sellable 5, B×1 with 1); B sellable 0; A sellable 1 | `InStock`; `OutOfStock`; `OutOfStock` (floor(1÷2) = 0) — **US-SHP-12 AC2** |
| U-F3 | Bundle components: a bundle SKU; a prescription product's SKU; its own SKU; a duplicate; quantity 21; one unit in total | `InvalidBundleComponent` ×4; invalid; `ProductNotPublishable` |
| U-F4 | Related links: self; duplicate; 13 of one kind | `RelatedProductInvalid` ×2; invalid |
| U-F5 | `RecommendationExclusionPolicy`: out-of-stock; Rx for a guest; Rx for a customer with no eligible cat; Rx with an eligible cat | Excluded; excluded; excluded; included — **US-SHP-08** |
| **G — wishlist and alerts** | | |
| U-G1 | Add the same SKU twice; the 201st item; remove an absent SKU | One item; `WishlistLimitReached`; no-op |
| U-G2 | Alert status table; `MarkNotified` twice | Only §8.11's arrows; the second is refused |
| U-G3 | `shop.back-in-stock` definition | Passes HO-NTF-001 `TemplateRules`; `Marketing`; `unsubscribeLink` and `productLink` required on every channel |
| U-G4 | `ProductLink("royal-canin-kitten", skuId)` | `{WebBaseUrl}/shop/products/royal-canin-kitten?sku={skuId}` |
| U-G5 | `StockAlertGuard`: notified + on sale; SKU discontinued; product archived | Valid; invalid; invalid |
| **H — recommendations and localisation** | | |
| U-H1 | Life stage: 8 months; 3 years; 7 years; no date of birth; estimated 2 years | Kitten; Adult; Senior; none + `AddDateOfBirth` hint; Adult with `lifeStageIsEstimated` |
| U-H2 | `AllergenMatcher("Chicken")` against "Chicken meal, rice"; "chickens"; "chickpea flour"; `"Fish oil"` against "fish" | Match; match; **no match**; match — **US-SHP-11 AC2** |
| U-H3 | Neutered cat + product with `Sterilised`; 6.5 kg adult + `WeightManagement`; condition "FLUTD" + `Urinary`; kitten at 6.5 kg | Reasons `Sterilised`; `WeightManagement`; `ConditionMatch`; no `WeightManagement` |
| U-H4 | Rx candidate with an authorisation for another cat of the same customer | Excluded for this cat |
| U-H5 | Ranking with equal reasons | Units sold, then name; deterministic |
| U-H6 | `Resolve` with `bn` name present, `bn` description empty | Name `bn`, description `en`; `fieldLocales` says so — **US-SHP-14 AC2** |
| U-H7 | Translation for `en` (default); for `fr` (not enabled) | `DefaultLocaleNotTranslatable`; `LocaleNotEnabled` |
| **X — cross-cutting** | | |
| U-X1 | Reflection over `Events/Product/**` | No string properties |
| U-X2 | `ShopWorker` loop with a throwing pass | Logged; the next pass still runs |
| U-X3 | CSV export of a product named `=HYPERLINK(...)` | Cell prefixed with `'` |
| U-X4 | `ActionRoles` rows for §12.3 | Match Appendix D's "Product catalogue" row; Vet, Nurse, Caretaker, Accountant absent |
| U-X5 | Migration predicates and view SQL | Built from `ProductStatus.Active.Value`, `ProductReviewStatus.Published.Value`, `StockAlertStatus.Pending.Value` — no literals |

## 16. Integration test cases

Real HTTP through `WebApplicationFactory<Program>` in `Testing`, against the **shared**
`catecommerce_dev` (HO-ACC-001 §16.1). The worker is off; tests call `ShopWorkCycle` and HO-NTF-001's
`NotificationWorkCycle`, advancing `FakeTimeProvider`.

### 16.1 Additional shared-database and seam rules

1. **Every catalogue assertion is scoped to the test's own data.** Each test mints a unique brand
   (`$"Brand-{Guid.NewGuid():N}"[..20]`) and filters by it, or asserts **inclusion** of its own
   products. Never assert a global count, an empty catalogue, or a global first page.
2. Slugs, SKU codes, barcodes (valid GTINs generated with a correct check digit) and origin country
   codes are minted per test. Origin tests use a country code no fixture uses, or assert inclusion.
3. **Stock is seeded exactly as Inventory will hold it**: integration tests write Inventory's own
   batch and ledger rows through HO-IMP-001's `IStockLedger` (or its migration fixtures) and let
   `v_sellable_stock` derive the figure. Shop has **no** command, HTTP route or Testing-only endpoint
   that sets stock, and none may be added. This makes Shop's integration tests depend on HO-IMP-001
   being merged - §20.1.
4. **Pharmacy and Orders seams**: the factory replaces `IDietAuthorisationGate` and
   `IPurchaseHistoryReader` with `ControllableDietAuthorisationGate` /
   `ControllablePurchaseHistoryReader` (test project), which answer per customer id from a
   concurrent dictionary the test fills. A test that asserts the **shipped** fail-closed behaviour
   (I-F8, I-D6) runs in a second factory without the replacement. When HO-PHM-001 and HO-ORD-001
   land, their handovers re-run I-F1…I-F3 and I-D1 against the real implementations.
5. Localisation tests run in a factory with `Localisation:ContentLocales = ["en","bn"]`.
6. **The performance fixture** (I-E11, `[Trait("Category","Performance")]`, opt-in) is seeded once and
   **idempotently**: brand `zz Perf Fixture`, 5,000 active listed products × 4 SKUs = 20,000 SKUs,
   each priced and stocked, across all categories, 12 origins and all life stages and proteins. It is
   created through the application's own commands and repositories, and skipped when all 5,000
   exist. It is never deleted (OQ-15).

| ID | Case | Expected |
|---|---|---|
| **A — catalogue core** | | |
| I-A1 | Migrations `AddProductCatalogue` on an empty database | Schema `product`; tables and unique indexes of §12.1 present; `clinic.*` and `admin.*` untouched |
| I-A2 | Host starts, scope validation on; HO-ADM-001's `EndpointAccessConventionTests` | Every registration resolves; every new action carries exactly one access marker |
| I-A3 | Shop manager `POST`s a product with name, brand, description, category, tags, tax class and slug; adds images; `GET` staff detail | `201`; every field echoed; status `Draft` — **US-SHP-01 AC1** |
| I-A4 | Create one product in each of the six categories and each subcategory | All `201`; `GET /api/shop/categories` lists all six with their subcategories — **US-SHP-01 AC3** |
| I-A5 | Draft product: public detail by slug; public list filtered by its brand; publish; archive | `404`, absent; after publish present in both; after archive `404`, absent — **US-SHP-01 AC2** |
| I-A6 | Two SKUs with their own code, barcode, images and prices; publish; public detail | Each SKU carries its own price and images; barcodes not exposed publicly — **US-SHP-02 AC1** |
| I-A7 | Same barcode on another product; 10 concurrent SKUs with one new barcode | `400 BarcodeAlreadyInUse`; exactly one `201`, the rest `400`, never `500` |
| I-A8 | Schedule a price 1 h ahead; `GET` history; advance 1 h; public detail | Listed under `scheduled`; after the advance it is the current price, and the old one is under `past` |
| I-A9 | Publish with an unpriced SKU | `400 ProductNotPublishable` naming the SKU code |
| I-A10 | `POST /api/staff/shop/products` as Reception, Vet, a customer, anonymous; `GET` staff list as Inventory | `403`, `403`, `401`/`403`, `401`; `200` — Appendix D; the `403`s audited (HO-ACC-001) |
| I-A11 | Update with a stale `expectedVersion` | `409` |
| I-A12 | Staff list filtered by status and brand, sorted, paged; CSV export | Scoped results; CSV with a header row and the injection guard |
| I-A13 | `GET /api/staff/shop/skus?barcode=` known; unknown | `200` with the SKU; `404` |
| **B — food and origin** | | |
| I-B1 | Food product with every FR-SHP-4 field; public detail | Country of origin, manufacturer, life stage, protein source, ingredients, guaranteed analysis, feeding guide and storage instructions returned — **US-SHP-03 AC1** |
| I-B2 | Set prescription required; public detail | `prescriptionRequired: true`; a `BusinessEvent` audit row — **US-SHP-03 AC2** |
| I-B3 | Publish food with no country of origin; add it; publish | `400 ProductNotPublishable` mentioning country of origin; then `204` — **US-SHP-03 AC3** |
| I-B4 | Create an origin; create it again | `201`; `400 OriginAlreadyExists` |
| **C — availability** | | |
| I-C1 | Product with SKUs A and B; push A sellable 5; public detail | A `InStock`, B `OutOfStock`; the product is still listed and A still purchasable — **US-SHP-02 AC2** |
| I-C2 | Push A with an older `AsOf` and sellable 0 | Ignored; A still `InStock` |
| I-C3 | Push A with `nextAllocatableExpiryOn` = today+30 (window 60); then today+90; set the window to 120 | `expiryNotice` shown; absent; shown — **US-SHP-07 AC2** |
| I-C4 | SKU never pushed: public and staff detail | `OutOfStock`; staff `stock: null` ("never reported"), not 0 |
| I-C5 | Push for an unknown SKU id | `SkuNotFound` from the dispatcher |
| I-C6 | Push sellable 0 after 5 | Detail shows exactly what was pushed — **US-SHP-07 AC1** display path (the computation is HO-IMP-001's) |
| **D — reviews** | | |
| I-D1 | Customer with the SKU in `ControllablePurchaseHistoryReader` posts a review; public reviews list | `201 PendingModeration`; not listed — **US-SHP-09 AC1, AC3** |
| I-D2 | Customer without a purchase posts | `400 ReviewNotPermitted` — **US-SHP-09 AC2** |
| I-D3 | Moderator publishes; public list and rating; another is rejected with a reason | Listed with `verifiedPurchase`; rating summary updated; the rejected one absent; `BusinessEvent` rows with the moderator — **US-SHP-09 AC3** |
| I-D4 | Second review for the same product; edit a published review | `400 AlreadyReviewed`; status back to `PendingModeration` and gone from the public list |
| I-D5 | Reception moderates; another customer edits the review | `403`; `404` |
| I-D6 | Shipped `NoPurchaseHistoryReader` factory: any customer posts | `400 ReviewNotPermitted` |
| **E — storefront search, filter, sort** | | |
| I-E1 | Migration `AddStorefrontCatalogue` | `pg_trgm` installed; the trigram index and three views exist |
| I-E2 | Within the test's brand: filter by origin, category, subcategory, life stage, protein, price range, availability and rating, each alone and three combined | Exactly the expected products each time — **US-SHP-04 AC1** |
| I-E3 | Sort by `price_asc`, `price_desc`, `popularity` (sales pushed via `RecordProductSalesStatsCommand`), `newest`, `rating` with a brand + availability filter | Correct order each time; `appliedFilters` echoes the filters with the sort — **US-SHP-04 AC2** |
| I-E4 | Two food products from a fresh origin, one origin record with notes; `GET /api/shop/origins` | The origin listed with code, name, notes and `productCount: 2`; an origin whose only product is a draft absent — **US-SHP-05 AC1** |
| I-E5 | `GET /api/shop/products?origin=XX&brand=…&lifeStage=Kitten` | Only that origin's matching products; the filters response for that origin still offers brands and life stages — **US-SHP-05 AC2** |
| I-E6 | Brand "Purrfecta{suffix}", product "Salmon Supreme"; search `purfecta`, `salmn supreme` | The product is included in both — **US-SHP-06 AC1** |
| I-E7 | Ingredient list containing "duck"; tag `grain-free`; search `duck`, `grain free` | Included — **US-SHP-06 AC2** |
| I-E8 | Search `zzqx{guid}`; search a near miss of the test's brand with no other match | `totalCount 0`; `suggestions.popular` non-empty; `didYouMean` contains the brand — **US-SHP-06 AC3** |
| I-E9 | Maintenance on (HO-ADM-001): `GET /api/shop/products`; `POST` a wishlist item; `GET /api/staff/shop/products` | `503`; `503`; `200` — US-ADM-08, ADR-093 |
| I-E10 | `catalogue` limit lowered to 3: four searches | Fourth `429` with `Retry-After` |
| I-E11 | **Performance** (§16.1 rule 6): 300 requests mixing search, 1–4 filters and every sort, 8 concurrent | **p95 ≤ 500 ms**, measured per request inside the test process — **US-SHP-04 AC3, NFR-PRF-2** |
| I-E12 | Public list and detail headers; purchasability headers | `Cache-Control: public, max-age=60`; `no-store` |
| **F — prescription, bundles, related** | | |
| I-F1 | Rx product, customer with an alive cat and no authorisation: public detail; purchasability | Visible with `prescriptionRequired`; `purchasable: false`, `PrescriptionAuthorisationRequired` — **US-SHP-10 AC1** |
| I-F2 | Authorisation added for that cat; purchasability; the cat marked deceased; again | Purchasable with `eligibleCats` [that cat]; then not purchasable |
| I-F3 | `CheckPrescriptionEligibilityQuery` at `AsOf` = the day after `ValidTo` | `eligible: false`, `PrescriptionAuthorisationRequired` — the contract for **US-SHP-10 AC3** |
| I-F4 | Bundle of A×2 + B×1 priced 2,500; public detail; `GetBundleCompositionQuery` | One price for the bundle; components listed; the composition returns both with quantities — **US-SHP-12 AC1** |
| I-F5 | Stock A 5, B 1 → bundle; B 0 → bundle | `InStock`; `OutOfStock` — **US-SHP-12 AC2** |
| I-F6 | Bundle including a prescription product's SKU | `400 InvalidBundleComponent` |
| I-F7 | Curated FBT and up-sell (one out of stock, one Rx); life-stage block; as guest, as a customer without authorisation, as one with authorisation | Out-of-stock always excluded; the Rx item excluded for the guest and the unauthorised customer, included for the authorised one; `basis: YourCats` for a customer with a kitten — **US-SHP-08** |
| I-F8 | Shipped `NoDietAuthorisationsReader` factory: purchasability for an Rx product | Not purchasable for every customer — fail-closed |
| **G — wishlist and back-in-stock** | | |
| I-G1 | Add, list, add again, remove | Idempotent; the list shows availability and price |
| I-G2 | Alert on an in-stock SKU; on an out-of-stock SKU | `400 SkuAlreadyAvailable`; `201` with `deliverability` |
| I-G3 | Customer with email marketing consent: alert; push stock; run the shop pass, then the notification pass; run both again; push 0 then 5 and run again | Exactly **one** `shop.back-in-stock` request and one captured email; the alert `Notified`; nothing on later passes — **US-SHP-13 AC1** |
| I-G4 | Customer with no marketing consent | `deliverability.willNotify: false`; after the pass the request is `Suppressed NoPermittedChannel` (F33, OQ-2) |
| I-G5 | Discontinue the SKU with a pending alert | Alert `Closed SkuNoLongerSold` |
| I-G6 | Merge a duplicate with a wishlist item, a pending alert and a review into a survivor who reviewed the same product; re-send the merge | Items unioned; the alert moved; the duplicate's review `Withdrawn`; the re-send changes nothing — ADR-096 |
| I-G7 | Anonymise a customer with a wishlist, an alert and a published review | Wishlist empty; alert `Closed CustomerAnonymised`; the review still public by "A verified buyer" |
| **H — recommendations and localisation** | | |
| I-H1 | Customer's neutered 3-year-old cat; food products for Adult with and without `Sterilised`, one Kitten | Adult products recommended with `LifeStageMatch`, the sterilised one first with `Sterilised`; Kitten absent — **US-SHP-11 AC1** |
| I-H2 | The cat has allergy "Chicken"; one otherwise matching product lists "chicken meal" | That product absent; `excludedForAllergy [{ allergen: "Chicken", count: 1 }]` — **US-SHP-11 AC2** |
| I-H3 | Another customer's cat | `404` |
| I-H4 | (`bn` enabled) Staff sets `bn` name, description, ingredients and feeding guide; public detail `?locale=bn` | All four in `bn`; `fieldLocales` all `bn` — **US-SHP-14 AC1, AC3** |
| I-H5 | Remove the `bn` feeding guide; `?locale=bn` | Feeding guide in `en`, `fieldLocales.feedingGuide: "en"` — **US-SHP-14 AC2** |
| I-H6 | Translation for `fr`; public `?locale=fr` | `400 LocaleNotEnabled`; default-language content, no error |
| **X — cross-cutting** | | |
| I-X1 | Log capture across the D, G and H cases at `Information`+ | No review text, allergen text, customer names, mobiles or emails |
| I-X2 | Scan every review and recommendation response body | No customer id, mobile or email field |

---

## 17. Implementation order and verification

Build **phase by phase**. Each phase goes code → its unit tests green → its migration generated **and
read** → its integration tests green. Record each phase's commit in the test sheet.

| Phase | Stories | Delivers |
|---|---|---|
| **A** | US-SHP-01, US-SHP-02 AC1 | Enumerations, taxonomy, constants, `commerce.pricesIncludeTax`; `ProductAggregate` with SKUs, images, slug, status, `SearchText`; `SkuPriceAggregate`; publication rules; staff product, SKU and price endpoints; CSV export; barcode lookup; `ActionRoles`; filter mappings; migration `AddProductCatalogue` |
| **B** | US-SHP-03 | Food details, country of origin, prescription flag, expiry tracking, health focus; sourcing origins; migration `AddFoodDetailsAndOrigins` |
| **C** | US-SHP-02 AC2, US-SHP-07 | Stock and sales copies, the two inbound commands, availability rules, expiry notice, `shop.expiryDisplayWindowDays`; migration `AddCatalogueFigures` |
| **D** | US-SHP-09 | Reviews, moderation, the purchase-history port and stub; migration `AddProductReviews` |
| **E** | US-SHP-04, 05, 06 | **Step 1: verify `pg_trgm` (§5) — else stop (§20.2).** Extension, trigram index, views; catalogue, filters, categories, origins, detail, reviews reads; suggestions; `[StorefrontEndpoint]`; `catalogue` limit; cache headers; the performance fixture and I-E11; migration `AddStorefrontCatalogue` |
| **F** | US-SHP-10, 12, 08 | Diet-authorisation port and stub, gate, purchasability, eligibility contract; bundles; related products and recommendation blocks; migration `AddBundlesAndRelated` |
| **G** | US-SHP-13 | Wishlist, stock alerts, deliverability, template, link method, guard, `ShopWorker`; merge and anonymisation handlers; migration `AddWishlistAndStockAlerts` |
| **H** | US-SHP-11, 14 | Food recommendations and settings; translations, `LocalisationOptions`; migration `AddProductTranslations` |

```bash
dotnet restore CatEcommerce.sln
dotnet build CatEcommerce.sln            # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add <PhaseMigration> --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output-dir Persistence/Migrations
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
dotnet run --project src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.IntegrationTests --filter "Category!=Performance"
dotnet test tests/CatEcommerce.IntegrationTests --filter "Category=Performance"      # phase E onward; record the p95
```

**Read `AddStorefrontCatalogue` and `AddBundlesAndRelated` before applying them**: the view SQL is
hand-written, and the second re-creates the first view.

---

## 18. Acceptance criteria

Each row is checked against its evidence. **A row with no evidence is not met.**

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | SHP-01 AC1 | A product carries name, brand, description, images, category, tags, status and SEO slug | U-A1, U-A5, I-A3 |
| A2 | SHP-01 AC2 | A draft (and an archived or unlisted) product is invisible on the storefront | U-A6, I-A5 |
| A3 | SHP-01 AC3 | All six FR-SHP-1 categories and their subcategories are supported | U-A2, I-A4 |
| A4 | SHP-02 AC1 | Variants by size/weight, flavour, colour or pack quantity; each SKU has its own price, barcode, displayed stock and images | U-A3, U-A4, U-A11, I-A6, I-A7, I-A8; displayed-stock clause **deferred** to HO-IMP-001 (§1 Amended 2026-10-04 deferral; I-C1 owed) |
| A5 | SHP-02 AC2 | An out-of-stock variant is unavailable while other variants remain purchasable | **Deferred** to HO-IMP-001 (§1 Amended 2026-10-04 deferral; U-C4, I-C1 owed) |
| A6 | SHP-03 AC1 | Food products record country of origin, manufacturer, life stage, protein source, ingredients, guaranteed analysis, feeding guide and storage instructions | U-B2, I-B1 |
| A7 | SHP-03 AC2 | A product requiring veterinary authorisation is flagged prescription-required | U-B5, I-B2 |
| A8 | SHP-03 AC3 | A food product without country of origin cannot be published as active | U-B3, U-B4, I-B3 |
| A9 | SHP-04 AC1 | Filters by country of origin, brand, category, life stage, protein source, price range, availability and rating | U-E3, I-E2 |
| A10 | SHP-04 AC2 | Sorting by price, popularity, newest and rating reorders correctly with filters retained (server half; URL persistence is HO-SHP-002) | U-E2, I-E3 |
| A11 | SHP-04 AC3 | ≤ 500 ms p95 at 20,000 SKUs | **I-E11** |
| A12 | SHP-05 AC1 | Shop by Country lists each sourced origin with flag code, sourcing notes and product count | I-E4 |
| A13 | SHP-05 AC2 | Selecting an origin shows only its products with other filters still available | I-E5 |
| A14 | SHP-06 AC1 | Misspelled brand or product names still return relevant results | U-E1, I-E6 |
| A15 | SHP-06 AC2 | Searching by ingredient or tag returns matching products | U-A10, I-E7 |
| A16 | SHP-06 AC3 | No results → alternatives and popular products | I-E8 |
| A17 | SHP-07 AC1 | Displayed availability is exactly Inventory's sellable figure (display path here; the exclusion computation is **HO-IMP-001**'s, US-IMP-08) | U-C2, `ShopFailClosedAvailabilityEndpointTests` (fail-closed half); display half **deferred** to HO-IMP-001 (§1 Amended 2026-10-04 deferral; I-C4, I-C6 owed) |
| A18 | SHP-07 AC2 | The allocatable batch's expiry is displayed when inside the configured window | U-C3 (pure rule); display **deferred** to HO-IMP-001 (§1 Amended 2026-10-04 deferral; I-C3 owed) |
| A19 | SHP-08 | "Frequently bought together" and life-stage recommendations shown, excluding out-of-stock and prescription items the customer cannot buy | U-F4, U-F5, I-F7 |
| A20 | SHP-09 AC1 | A customer who purchased the SKU may post a rating and review | I-D1 *(against the controllable reader until HO-ORD-001)* |
| A21 | SHP-09 AC2 | A customer who did not purchase it is refused | I-D2, I-D6 |
| A22 | SHP-09 AC3 | A review appears only after moderation approval | U-D2, I-D1, I-D3, I-D4 |
| A23 | SHP-10 AC1 | Without a valid authorisation the product is visible, not purchasable, with the reason (the consultation link is HO-SHP-002's) | U-F1, I-F1, I-F8 |
| A24 | SHP-10 AC2 | *(contract)* Eligible cats are returned for selection at add-to-cart; recording the cat on the order line is **HO-ORD-001** | I-F2 |
| A25 | SHP-10 AC3 | *(contract)* Eligibility evaluated at the checkout instant refuses an expired authorisation; blocking the line is **HO-ORD-001** | I-F3 |
| A26 | SHP-11 AC1 | Recommendations from life stage, weight, neuter status and conditions, each with its reason | U-H1, U-H3, U-H5, I-H1 |
| A27 | SHP-11 AC2 | A product whose ingredients match a recorded allergy is excluded | U-H2, I-H2 |
| A28 | SHP-12 AC1 | A bundle is priced as a unit; the composition contract lets Orders/Inventory deduct each component (**HO-ORD-001 / HO-IMP-001**) | I-F4 |
| A29 | SHP-12 AC2 | Any component out of stock → bundle unavailable | U-F2, I-F5 |
| A30 | SHP-13 AC1 | A back-in-stock alert is notified once sellable stock exists — *for customers whose consent permits a channel (F33, OQ-2); in-app only outside Development until a provider exists (HO-NTF-001 OQ-1)* | U-G2, U-G3, I-G2, I-G3, I-G4 |
| A31 | SHP-14 AC1 | With i18n enabled and a language selected, name, description, ingredients and feeding guide show in that language | I-H4 |
| A32 | SHP-14 AC2 | A missing translation falls back to the default-language text | U-H6, I-H5 |
| A33 | SHP-14 AC3 | A shop manager can enter content per enabled language | U-H7, I-H4, I-H6 |
| A34 | ADM-08 *(storefront half)* | In maintenance mode storefront endpoints refuse; staff endpoints work | I-E9 |
| A35 | ADR-096 | Merge and anonymisation obligations met | I-G6, I-G7 |
| A36 | BR-2 | No prescription product is purchasable without a valid authorisation, including before Pharmacy exists | U-F1, I-F8 |
| A37 | — | Host starts with scope validation; every endpoint declares access; no free text in events or logs | I-A2, U-X1, I-X1, I-X2 |

**Deferred, named in §4.2**: the sellable-stock computation (HO-IMP-001); cat selection on the order
line and checkout blocking (HO-ORD-001); authorisations (HO-PHM-001); component deduction
(HO-ORD-001 / HO-IMP-001); real purchase history (HO-ORD-001); popularity figures (HO-ORD-001).

---

## 19. Required gates

In order. Each writes `docs/gates/HO-SHP-001-<GATE>.md` **before** reporting.

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | The `Product` context (ADR-077, OQ-1); **ADR-082 judged against ADR-012** — the stock and sales copies have one writer each, no route, no edit path, and no decision depends on them; no stock arithmetic anywhere; the ports and their fail-closed stubs (ADR-084, ADR-086) with one registration line each; pure domain services; one aggregate per command with **no** exception; views created by migration, never from the read side; the amendments to other areas confined to §13's lines; ADR-077…096 recorded |
| 2 | SECURITY | `security-reviewer` | **BR-2 fail-closed** (I-F8) and gate ownership checks (deceased, transferred cats); `404` for another customer's review, alert, wishlist or cat; review text stored and returned as plain text and never logged; no customer identifier in public review reads; the inbound commands unreachable over HTTP; CSV injection guard; rate limits; `LimitedMarkup` validation on every markup field; image URLs only from configuration; `[StorefrontEndpoint]` never on staff routes |
| 3 | CODE_REVIEW | `code-reviewer` | Filtering, sorting and paging in SQL only; the price-at-t rule identical in `SkuPricing` and the view (U-A11, I-A8); `SearchText` maintained only by the aggregate; idempotent event handlers that never enqueue; the worker never throws; two-path messages from `ShopMessages` |
| 4 | TEST | `test-engineer` | `TC-HO-SHP-001.md` from §15/§16 first; §16.1 scoping; stock pushed only through the dispatcher; controllable seams only where §16.1 rule 4 allows; I-E11 run and its p95 recorded, not skipped |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A17, A24, A25, A28 marked as the display/contract half with the owning handover named; A20 marked against the controllable reader; A30's consent and provider caveats recorded, not passed silently |

---

## 20. BLOCKED conditions — stop and report, do not work around

**20.1 HO-ACC-001, HO-ADM-001 or HO-NTF-001 is not merged.** This slice consumes their sessions,
access markers, settings, tax classes, `Money`, `LimitedMarkup`, outbox, template catalogue, routing
policy and link builder. Do not stub them, and do not copy their types.

**20.2 `pg_trgm` is unavailable or `CREATE EXTENSION pg_trgm` fails** in a target environment. Stop.
Do not fall back to `ILIKE` only (misspellings would fail US-SHP-06 AC1), and do not add a search
engine. It is a deployment decision.

**20.3 Something seems to need Shop to compute stock**, store an editable quantity, or expose
a Shop route or command that sets stock. Stop — ADR-082, D-3, README C-1. Shop has no such
command: a test that needs stock writes Inventory's rows through `IStockLedger`.

**20.4 I-E11 exceeds 500 ms p95.** Change the query, the view or the indexes. Never weaken the test,
shrink the fixture, or put a response cache in front of the list to pass it. If the budget cannot be
met in PostgreSQL, report it — it is an architecture decision (ADR-083).

**20.5 Do not build a diet authorisation, a cart, an order, a reservation or a payment.** The
contracts in §8.9–§8.12 are all this slice owns. No payment gateway exists in v1.

**20.6 Do not enqueue from an event handler** — HO-NTF-001 ADR-057. Back-in-stock is enqueued only
by `NotifyStockAlertCommand`'s handler.

**20.7 A requirement conflict is a stop, not an edit.** SRS, backlog and every handover are forbidden
scope. The known conflicts (SRS §5.1 vs FR-SHP-4/11 on where origin and the prescription flag live;
back-in-stock's category) are recorded as ADR-081 and OQ-2 — do not re-decide them in code.

**20.8 Do not reclassify `shop.back-in-stock` as Transactional** without the owner's answer to OQ-2
and an Amended row on HO-NTF-001.

**20.9 Do not add an upload path** — no `IFormFile`, no object-storage client, no local file write
(ADR-091, OQ-17).

**20.10 A contract consumer (HO-IMP-001, HO-ORD-001, HO-PHM-001) needs a different shape.** Their
handover amends this one by an Amended row; this build does not guess ahead of them.

**20.11 Do not downgrade `Platform.Infrastructure.Core` below 1.0.34.**
