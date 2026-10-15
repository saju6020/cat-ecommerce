# HO-IMP-001 — Imports, Batches and Inventory (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-IMP-001** |
| Title | Imports, Batches and Inventory — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §9 — **US-IMP-01 … US-IMP-16** (16 stories: 12 Must, 3 Should, 1 Could) |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-IMP-1 … FR-IMP-16**, FR-SHP-8 (the availability contract), FR-ORD-9 / FR-ORD-16 (the reservation and return contracts), FR-CLN-17, FR-PHM-5, FR-SRG-8 (the deduction contract), **D-2, D-3, D-4**, **BR-3, BR-4, BR-14**, C-4, C-6, C-7, C-9, NFR-LEG-2, NFR-PRF-2, UI-4, AC-6, AC-10, §5.1 (Batch, Consignment, Supplier, Stock Ledger Entry), §8.1 (stock on hand, near-expiry, low stock, import consignment reports), Appendix B.5, Appendix C (batch states), Appendix D ("Stock & batches", "Purchase orders / imports") |
| Pairs with | [`HO-IMP-002`](HO-IMP-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ACC-001`](HO-ACC-001.md) (staff, roles, `ActionRoles`, audit capture, customer merge chain), [`HO-ADM-001`](HO-ADM-001.md) (branches and time zones, settings registry, `Money`, active branch), [`HO-NTF-001`](HO-NTF-001.md) (outbox, templates, subject guards), [`HO-CAT-001`](HO-CAT-001.md) (cats, for the recall trace), **`HO-SHP-001` (SKUs — not yet written; §8.2 states the contract assumed)** |
| Consumed by | `HO-ORD-001` (reservation, dispatch, returns, POS), `HO-EMR-001` (US-CLN-15), `HO-PHM-001` (US-PHM-05), `HO-SRG-001` (FR-SRG-8), `HO-BRD-001` (house food), `HO-SHP-001` (availability, US-SHP-07), `HO-RPT-001` (valuation, margin) — through the contracts in §8.8 and §12.6 |
| Delivery order | Section 7 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None from `architect` for this slice. §8 is derived from `.ai/bounded-contexts.md`, `.ai/flows/import-to-shelf-flow.md` and `.ai/flows/batch-recall-flow.md`; every decision not settled there is an ADR in §9 or an open question in §9.1 |
| Baseline commit | `4007225` |
| Amended | **2026-09-23 — README C-2 follow-ups.** Reconciling the four consumers onto `IStockLedger` exposed three operations no published method covered. Two are added to the port in §8.8: **`AdjustAsync`** (HO-PHM-001 OQ-17 — a controlled-drug register correction must move stock in the same save) and **`GetBranchBalancesAsync`** (HO-PHM-001 OQ-16 — register reconciliation needs per-batch branch balances, and `IStockPositionReader` is Inventory-internal). The third (HO-ORD-001 OQ-19) is **refused on purpose**: quarantine disposition and reallocation stay inside Inventory, and §8.8 now says so. New OQ-21. <br><br>**2026-10-07 — integrator, recorded at the merge of `ho/IMP-001` (`9d2346f0`) from `docs/gates/HO-IMP-001-*.md` (ARCHITECTURE, SECURITY, CODE_REVIEW, REVIEW-R2, TEST, ACCEPTANCE: all PASS after the follow-up round) and `docs/test-cases/TC-HO-IMP-001.md` (160 rows: 158 PASS, 2 BLOCKED — I-A1 on an empty database and I-F7 — 0 FAIL). Medium/low findings are in [`docs/backlog/IMP.md`](../backlog/IMP.md).** (1) **Eighteen files outside §13's literal list**, none in §14 forbidden scope, all same-area support or test files (the ARCHITECTURE gate listed 16; ACCEPTANCE added the last two): `src/Api/CatEcommerce.Api/Background/InventoryWorkerState.cs`; `src/Api/CatEcommerce.Api/Controllers/StaffAlertsAndReorderController.cs` (one controller where §13 names `StaffReorderPolicies`, `StaffStockAlerts` and `ProcurementReorder`); `src/Domain/CatEcommerce.Domain/DomainServices/IInventoryAlertEnlister.cs`; `…/DomainServices/StockDemandPlanner.cs`; `…/DomainServices/StockUsability.cs`; `src/Infrastructure/CatEcommerce.Infrastructure/Configuration/InventoryOptions.cs`; `…/Persistence/Views/BatchUnitCostEntry.cs`, `SellableStockEntry.cs` and `StockPositionEntry.cs`; `…/Persistence/Configurations/SellableStockEntryConfiguration.cs` and `BatchUnitCostEntryConfiguration.cs`; `src/Shared/CatEcommerce.Shared/Common/Helpers/InventoryCsv.cs`; `src/Shared/CatEcommerce.Shared/Common/Constants/SettingKeys.Inventory.cs`; `src/Infrastructure/CatEcommerce.Infrastructure/Fakes/Inventory/FakeStockLedger.cs` (doc comment only); `tests/CatEcommerce.Infrastructure.Tests/Auditing/AuditCaptureExclusionsTests.cs`; `tests/CatEcommerce.Infrastructure.Tests/Persistence/InventorySqlAndEventTests.cs`; **and, added after the ARCHITECTURE gate,** `src/Domain/CatEcommerce.Domain/DomainServices/IInventoryAuditEnlister.cs` (it also declares `IInventoryAlertWriter`, so the alert-writer port needs no further line) and `tests/CatEcommerce.Infrastructure.Tests/Persistence/InventoryReadSideConventionTests.cs`. The two implementations, `EfInventoryAuditEnlister` and `EfInventoryAlertWriter`, and the `OpenReservations` helper sit in `Infrastructure/Inventory/**` (inside §13), so they are not additional files. The per-area partials (ADR-341: `InventoryModule.cs` for `Program.cs`, `CatEcommerceDbContext.Inventory.cs`, the `*.Inventory.cs` Actions / ActionRoles / BusinessViolationCodes / SettingDefinitions files), the C-ALL files and `Auditing/` for §13's "Audit/**" were counted in scope. (2) **The developer's declared deviations, as the ARCHITECTURE gate judged them** (all accepted; ADR text is in the "HO-IMP-001 build amendments" section of `.ai/decisions/adr.md`): (a) the alert aggregate is `InventoryAlertAggregate` (DbSet `InventoryAlerts`, table `inventory.stock_alert`) because Shop owns `StockAlertAggregate`, and its enum is `InventoryAlertStatus`, not `StockAlertStatus` — read ADR-110 and §8.13/§10.1 accordingly; (b) `IStockLedger.RecordShortfall` makes one synchronous primary-key lookup for the location's branch (the published port is `void`); an unknown location writes an alert with `BranchId = Guid.Empty` (backlog IMP 19); (c) low stock excludes `Quarantine`-kind locations; (d) a storage-breach quarantine moves only the **free** stock (`on_hand - reserved`) — reserved units stay put and remain dispatchable (owner decision, backlog IMP 3); (e) extra ports `IInventoryAlertEnlister` (and, in the fix round, `IInventoryAuditEnlister` and `IInventoryAlertWriter`) and a `RecordRecallTraceExportCommand` that writes the `RecallTraceExported` audit row (the trace CSV is refused if that write fails); these are additional rule-1 exceptions beside §8.1's four — the breach report's enlisted `StorageBreach` alert and `RecordShortfall`'s enlisted alert, each creating only new rows; (f) `InventoryWorkCycle.RunAsync(ct, onlyBranchId)` takes an optional branch id so a test on the shared database scans only its own branch (the reconciliation pass ignores it); (g) integration tests use expiry offsets 100/75/45/15 days instead of §16's 91/89/59/29, because a branch's local date can differ from UTC's by a day; (h) **FI-1 is false as written, which §20.3 makes a stop; the build substituted an equivalent two-statement trigger body** (insert a zero row `ON CONFLICT DO NOTHING`, then `UPDATE`; `InventoryLedgerSql.cs`) that keeps ADR-099's trigger, `CHECK` and row lock with no application lock. Behaviour is verified on the real trigger (TEST T14). **The §20.3 ratification is still owed by the owner or architect** (backlog IMP 4); (i) `CountLocationBusy` is a `400` (as §12.5 says); the race on `ix_stock_count_one_active` is now mapped too (see 3); (j) the recall template's `expiresOn` variable is `Text` (`yyyy-MM-dd` or "no expiry date"), not `Date`, because an undated batch has no date to render. Also recorded: `GetBatchUnitCostQuery` (§12.6) was **not built** (the view and `IStockPositionReader.GetUnitCostsAsync` exist; backlog IMP 9); some list reads are paged in memory or unpaged (backlog IMP 10); I-I6's observable result is a `Suppressed` request with reason `SubjectNoLongerApplies`, not a `Cancelled` notification as §16 words it (backlog IMP 32); the FakeStockLedger is retained, replaced in DI by `EfStockLedger`. (3) **Gate fix rounds** (`HO-IMP-001-REVIEW-R2.md`; round-1 ARCHITECTURE HIGH, CODE_REVIEW two HIGHs; and the post-gate fix `8f915bdf`): (a) the Inventory read handlers no longer materialise aggregates — every terminal is a `Select` projection (including the alerts list, the breach detail and every `ToDictionaryAsync` lookup) — and `InventoryReadSideConventionTests` guards the pattern (narrow; backlog IMP 12); (b) the recall trace and recipient resolver net reservations per (reference kind, reference, line) through `OpenReservations` before taking the party from the Reserve entries, so a released or consumed reservation is neither "unshipped" nor notified; the ledger contract (a Release carries no party) is unchanged; (c) the breach `observedOn` is normalised to UTC in the controller (a `+06:00` value is no longer a `500` at Npgsql) and the validator rejects an instant later than now plus 5 minutes (same defect class as HO-APT-001 Amended (5) and HO-EMR-001 Amended); (d) **write-off requires assignment to every branch holding the batch**, otherwise it is refused (`StockLocationNotFound`) and nothing is written — a rule beyond §12.3/C9, added because positions can be held at other branches after a transfer; (e) the `UniqueConstraintExceptionFilter` maps `ix_supplier_code` and `ix_stock_count_one_active` (both `400`); the alert-dedup indexes are named `ix_stock_alert_dedup_key` and `ix_stock_alert_one_live_per_pair` (§12.1 says `ix_stock_alert_dedup` / `ix_stock_alert_open_pair`); (f) a worker-raised alert is saved by `EfInventoryAlertWriter`, which treats a unique violation on those two indexes (only) as "already raised" and propagates everything else; (g) the `RecallNotified` audit row is enlisted into the same commit as the batch update and the outbox rows (`8f915bdf`), so a failed audit write rolls back the whole notify run and a retry still finds every recipient; (h) `InventoryDatabaseFactsTests` uses `InventoryApiFactory.RequireBranchDatabase()` (the branch database variable is required, `catecommerce_dev` is refused). (4) **The integrator's merge resolutions.** (a) `NotificationSubjectKind.BatchRecall` is value **6**, not the 5 the branch carried: HO-EMR-001's `VisitFollowUp` merged first and holds 5 (the enumeration is persisted by value; `StockAlert` = 4, `AppointmentReminder` = 3). (b) `Inventory:Worker:Enabled=false` was added to `src/Api/CatEcommerce.Api/appsettings.Testing.json` (ARCHITECTURE MEDIUM 7: until then every other area's test host ran one expiry, low-stock and reconciliation pass at start-up across the shared database); only `InventoryApiFactory` had set it. (5) **Owed Amended rows written.** The "Amendments this slice needs elsewhere" row above is now carried out: an Amended row dated 2026-10-07 was appended to HO-ACC-001, HO-ADM-001, HO-NTF-001 and HO-SHP-001, each after checking the change exists in `src/`. (6) **Open after the merge:** the five owner decisions (write-off approval, count self-approval, reserved units of a breach-quarantined batch, the §20.3 ratification, the controlled-drug correction approver check — OQ-21) and the integrator items (the C-ALL-5 audit-type registration before EMR, PHM or SRG call the port; I-A1 on an empty database; the SHP follow-up slice) are tracked in [`docs/backlog/IMP.md`](../backlog/IMP.md) rows 1–8. |
| Amended (HO-ORD-001 gate fix) | **2026-10-09 -- two members added to the published `IStockLedger` (§8.8), owner decision 2 of the HO-ORD-001 ARCHITECTURE fix round. Additive; nothing renamed or removed.** (1) **`ReserveBatchAsync(StockBatchDemand, StockReference, StockParty, ct)`** reserves ONE staff-chosen batch: the caller names the batch (`StockBatchDemand(BranchId, SkuId, Quantity, BatchId, Reason)`) and the reason, **Inventory picks the locations** (the fullest first) and runs the ordinary explicit-plan path, so the SKU, branch, usability (recalled / withdrawn / expired), free-stock, reason (>= 5 characters) and `FefoOverridden` audit rules (ADR-102) are the ones every reservation obeys. Refusals: `BatchNotFound` (not held at this branch for this SKU), `BatchExpired` / `BatchNotUsable`, `InsufficientStock`, `AllocationOverrideReasonRequired`. It enlists, never saves. (2) **`GetBatchUsabilityAsync(branchId, batchIds, ct)`** is a read: per batch, `BatchUsability(BatchId, SkuId, LotNumber, RefusalCode?, Message?)` with `RefusalCode` null when usable, else `BatchRecalled`, `BatchNotUsable`, `BatchExpired` (the branch's local date) or `BatchNotFound` (unknown, or held at another branch). `IsUsable` / `IsRecalled` are convenience properties. **Why:** HO-ORD-001 had read Inventory's internal `IStockPositionReader` and `IInventoryClock` for BR-14 (the batch check before dispatch and ready-for-collection), the BR-10 recalled-batch eligibility and the staff reallocation; those two types stay Inventory-internal (their own doc says so) and no other area may consume them. The ledger's rule 7 already refused to consume a recalled or expired reserved batch; `GetBatchUsabilityAsync` lets the consumer name the refusal first. HO-PHM-001 (in progress) adds its own dispensary-specific members (`GetDispensaryBatchesAsync`, `GetClinicDispensaryAsync`); neither set renames the other's. `FakeStockLedger` answers both new members fail-closed. **Restock and write-off of returned stock stay Inventory's quarantine workflow** (OQ-19, §8.8 "refused on purpose"): HO-ORD-001's C24 was removed, and `PostStockTransferCommand` / `RequestStockAdjustmentCommand` are not part of any published contract. Tests: `InventoryStockPortEndpointTests` (`ReserveBatch_*`, `BatchUsability_*`). Files: `IStockLedger.cs`, `Models/StockBatchModels.cs`, `EfStockLedger.Consumer.cs`, `Fakes/Inventory/FakeStockLedger.cs`. |
| Amended | **2026-10-09 — HO-PHM-001 architecture gate (owner decision A, "extend `IStockLedger`").** Pharmacy was reading Inventory's internal `IStockPositionReader` and loading `StockLocationAggregate` for dispensary batches and the branch's dispensary. §8.8 now publishes two more read-only members on `IStockLedger`, added the same way as `GetBranchBalancesAsync`: **`GetDispensaryBatchesAsync(branchId, skuIds)`** → `DispensaryBatch(SkuId, BatchId, LotNumber, ExpiresOn, BatchStatus, LocationId, OnHand, Reserved)` for every batch the branch's ACTIVE `ClinicDispensary` holds (on-hand or reserved above zero), with Inventory's batch status; and **`GetClinicDispensaryAsync(branchId)`** → `DispensaryLocation(LocationId, Code)` or a `DispensaryNotFound` failure. Both write nothing. Implemented in `EfStockLedger.Consumer.cs` over `IStockPositionReader` (still Inventory-internal); `FakeStockLedger` answers an empty list / `DispensaryNotFound`. Pharmacy no longer references `IStockPositionReader` or Inventory aggregates. Tests: `InventoryStockPortEndpointTests.DispensaryBatches_*`, `FakeStockLedgerTests`. |
| Amended (HO-EMR-001 Phase F gate fix) | **2026-10-13 -- ratified by integrator.** Additive member on the published `IStockLedger` (§8.8), from the HO-EMR-001 Phase F gate fix (ARCH F MEDIUM 2, backlog EMR 42). Nothing is renamed or removed. The member is `Task<StockResult<IReadOnlyList<Guid>>> GetDispensarySkuIdsAsync(Guid branchId, CancellationToken ct)`. It returns the distinct SKUs with on-hand stock above zero at the branch's ACTIVE `ClinicDispensary` location. It is read-only and enlists nothing. It is declared as a default interface member that answers `NotSupported`, so other implementers (`FakeStockLedger` and test doubles) are unchanged; `EfStockLedger` implements it untracked over `StockPositions` joined to `StockLocations`. This RATIFIES the edits made on `ho/EMR-001-F` (`17aad6be`) to `src/Domain/CatEcommerce.Domain/DomainServices/IStockLedger.cs` and `src/Infrastructure/CatEcommerce.Infrastructure/Inventory/EfStockLedger.Consumer.cs`; no other HO-IMP-001 edit is authorised. The consumer is HO-EMR-001, `GET /api/staff/dispensary/items`. The §8.8 listing gains the member. CONTRACTS row 34. The precedent is row 21 (HO-PHM-001 owner decision A, 2026-10-09). |
| Amendments this slice needs elsewhere | **Not written here** (this handover may edit only itself). The coordinator appends an Amended row to each: **HO-ACC-001** — `UniqueConstraintExceptionFilter` maps the named `23514` constraints of §12.5; change capture (§11.9 step 7 there) excludes `StockLedgerEntryAggregate` and `StockReservationEntryAggregate` (ADR-098). **HO-ADM-001** — seven settings appended (§10.2); `inventory.defaultReorderPoint` gains its consumer; `BranchCalendar.LocalDateOf` added if absent (§8.13). **HO-NTF-001** — template `inventory.batch-recall`, subject kind `BatchRecall` + `BatchRecallGuard`, `SystemActors.InventoryWorker`. **HO-SHP-001** — must publish the SKU fields of §8.2. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Give the business **control of every unit it buys, holds, sells, dispenses and feeds**: suppliers and
purchase orders; import consignments with the permits, certificates and costs an inspector will ask
for; goods receipt that captures batch and expiry; stock per batch per location; FEFO allocation;
sellable stock that never oversells; transfers, adjustments and counts that make shrinkage visible;
expiry and low-stock alerts; landed cost; and a batch recall that finds every customer and cat who
received a batch.

This area comes **before** Consultation and Pharmacy in the delivery order (README) because US-CLN-15,
US-PHM-05, FR-SRG-8 and boarding feeding must deduct **specific batches**. The single most important
output of this slice is therefore not a screen — it is the **stock contract** (§8.8) that five later
areas call, designed now so clinical stock deduction is built once.

Five things this slice is careful about:

1. **The ledger is the truth.** Every movement is an immutable ledger row (FR-IMP-15, D-3). Current
   stock is a **database-maintained projection** of the ledger, which no application code can write
   (ADR-098, ADR-099).
2. **No overselling, ever.** The last unit is protected by a PostgreSQL `CHECK` on the projection,
   evaluated inside the row lock the ledger trigger takes — not by a read-then-write check that a race
   can slip through (US-IMP-08 AC2, ADR-099). The same discipline APT applied to double booking.
3. **Expired means never.** "Expired" is **derived** from the expiry date in branch-local time, never a
   stored status that a job might forget to set. It is checked in the application **and** by a database
   trigger on every consuming movement and reservation, and **no parameter anywhere can bypass it**
   (BR-3, US-IMP-09, ADR-101).
4. **The recall trace is one query.** Every consuming movement is stamped at the moment it happens with
   its reference document, customer and cat. Tracing a batch is then a read over the ledger, not a
   fan-out to five modules — and a module added later is covered automatically (ADR-104).
5. **Money and quantity stay exact.** Money is `decimal(12,2)` (HO-ADM-001 ADR-054); unit costs carry
   four decimals in a distinct `UnitCost` value object; landed cost is allocated with largest-remainder
   rounding so batch totals sum exactly to consignment costs (ADR-106).

**No payment gateway is involved** (SRS Phase 5). Nothing here takes or records money; supplier
payment terms are recorded as text and days only.

§17 builds the slice in **ten phases (A–J)**; each compiles, migrates and passes its own tests first.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-IMP-16 *(part 1)* | C | 5 | Storage requirement per product (condition, temperature range, notes), returned on receiving and stock reads | A |
| US-IMP-01 | M | 5 | Suppliers with name, country, contact, currency, lead time and payment terms | B |
| US-IMP-02 | M | 8 | Purchase orders (SKUs, quantities, unit costs, currency, expected delivery, deliver-to location); issue, cancel, short-close; outstanding quantity derived from posted receipts | B (outstanding in D) |
| US-IMP-03 | M | 13 | Import consignments grouping POs: origin, shipment reference, HS codes, permit, health/veterinary certificate, customs declaration, clearance status, cost components, exchange rates, documents (metadata — §4.3); an inspection pack returning everything together | C |
| US-IMP-04 | M | 8 | Goods receipt per SKU: lot, manufacture date, expiry, accepted and rejected quantity with reason; **expiry mandatory for food and medicine** (D-2); one batch per accepted line | D |
| US-IMP-06 | M | 8 | Stock per batch per location (shop floor, warehouse, clinic dispensary, boarding store, quarantine) | D |
| US-IMP-15 | M | 8 | Immutable ledger for every movement with SKU, batch, delta, location, reference and user; stock derived from it; edits and deletes refused by the application **and** the database | D |
| US-IMP-07 | M | 8 | `FefoAllocator` domain service; staff override with mandatory reason, audited | E |
| US-IMP-08 | M | 8 | Sellable stock excluding expired, quarantined, damaged (non-selling locations) and reserved; reservation entries; **exactly one of two concurrent claims on the last unit succeeds** | E |
| US-IMP-09 | M | 5 | Expired batches refused for sale, dispensing and boarding use with no override anywhere | E |
| US-IMP-13 | M | 8 | Transfers with ledger entries at both ends; adjustments (damage, theft, sampling, internal use, …) with mandatory reason and approval above a configured value | F |
| US-IMP-14 | S | 8 | Cycle counts and full stock takes; blind counting; variance report before commit; commit writes adjustment entries referencing the count | F |
| US-IMP-05 | S | 8 | Landed cost allocated across a consignment's received quantity to a landed unit cost per batch; valuation and margin read landed cost in preference to purchase price | G |
| US-IMP-10 | M | 5 | Expiry alerts at 90 / 60 / 30 days (configurable) and on expiry; near-expiry report with days remaining and value at risk | H |
| US-IMP-12 | S | 8 | Low-stock alerts per SKU per location against a reorder point; reorder suggestions from consumption history and supplier lead time | H |
| US-IMP-11 | M | 13 | Recall: every remaining unit unusable at once across all locations; trace of every order, customer and cat; notify all in one action, logged; a recalled batch cannot be consumed, so an unshipped order containing it cannot be dispatched | I |
| US-IMP-16 *(part 2)* | C | 5 | Storage-condition breach reports flag the batches held at a location and can move them to quarantine pending a decision | J |

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409`; `SaveChangesAsync` per repository call | ADR-011, ADR-016, ADR-021 |
| No `CommandService` layer; cross-aggregate domain logic in `Domain.DomainServices` — **FEFO is named there as the example** | ADR-015, `.ai/bounded-contexts.md` rule 4 |
| `[RequireAction]` + the one `ActionRoles` table; every endpoint declares its access; denials audited | HO-ACC-001 ADR-036, HO-ADM-001 ADR-043 |
| Every aggregate change audited in the same transaction | HO-ACC-001 ADR-040 |
| **Money** is `decimal(12,2)` + ISO-4217 code, base `BDT` from `commerce.currencyCode`. SRS C-6 says "integer minor units"; ADR-054 chose the decimal representation and it is not revisited here | HO-ADM-001 ADR-054 |
| Branch-local time via IANA zones; conversion only in HO-ADM-001's calendar services | HO-ADM-001 ADR-045 |
| Aggregates that exist *at a place* carry `BranchId` from the active branch (`X-Branch-Id`) | HO-ADM-001 §8.2, ADR-046 |
| Business settings through `IBusinessSettings`; new keys are registry entries | HO-ADM-001 ADR-050 |
| Messages only through `INotificationOutbox.Enqueue`, in the command handler before the save; **never from an event handler** | HO-NTF-001 ADR-056, ADR-057 |
| Enlisting rows into the scoped `DbContext` without saving, so the caller's save commits them atomically (F23) | HO-NTF-001 ADR-056 — **the pattern §8.8 reuses** |
| `CustomerMergedEvent` is binding on every context holding a customer id | HO-ACC-001 ADR-039 — see ADR-104 for how this slice honours it without editing immutable rows |
| File uploads are metadata + storage key; no binary endpoint until object storage exists | HO-CAT-001 §4.2 |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-IMP-002`](HO-IMP-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-IMP-02 "outstanding quantity remains visible" | PO detail returns `orderedQuantity`, `acceptedQuantity`, `rejectedQuantity`, `outstandingQuantity` per line, and `receiptState` |
| US-IMP-03 "retrievable together" | `GET …/consignments/{id}/inspection-pack` — details, POs, documents, batches in one payload |
| US-IMP-10 "near-expiry report" | Paged report endpoint and CSV |
| US-IMP-11 "listed … notify all in one action" | Trace endpoint (paged, CSV) and one `POST …/notify` |
| US-IMP-14 "variance report produced" | `GET …/counts/{id}/variance` after submission; nothing written to the ledger until commit |
| US-IMP-16 "shown on the receiving and stock screens" | `storage` object on SKU lookup, batch and stock reads |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Story | Where it lands | What this slice delivers |
|---|---|---|---|
| Reserving stock on payment; releasing on cancel/expiry; consuming at dispatch; pick lists by location and batch | US-ORD-08, 10, 12, FR-ORD-22 | **HO-ORD-001** | `IStockLedger.ReserveAsync` / `ReleaseAsync` / `ConsumeReservationAsync` (§8.8); reservation lines carry batch and location (D-4) |
| **Blocking dispatch of an unshipped order containing a recalled batch** | US-IMP-11 AC4, BR-14 | **HO-ORD-001** | Consuming a reservation of a recalled batch is refused by the application and the database (I-I3). `BatchRecalledEvent` lets Orders flag affected orders |
| POS counter sale with barcode → FEFO batch | US-ORD-16 | HO-ORD-001 | `DeductAsync` (Sale) with FEFO; SKU barcode lookup |
| Returns into quarantine; restock or write-off | US-ORD-14, FR-ORD-16 | HO-ORD-001 | `ReceiveReturnAsync` into the branch's `Quarantine` location; restock is an ordinary transfer, write-off an ordinary adjustment |
| Consumables and medicines deducted by batch in consultation; shortfall recorded | US-CLN-15 | **HO-EMR-001** | `DeductAsync` (ClinicalUse) and `RecordShortfall` |
| Dispensing the exact batch; label with expiry | US-PHM-05 | **HO-PHM-001** | `DeductAsync` (Dispense) with an explicit batch; the returned lines carry lot and expiry for the label |
| Surgery drugs by batch; controlled-drug register | FR-SRG-8, US-SRG-08 | HO-SRG-001 | `DeductAsync` (ClinicalUse). **The controlled-drug register is not built here** |
| House food fed during boarding | FR-BRD-6, US-IMP-11 AC2 | HO-BRD-001 | `DeductAsync` (BoardingUse) with cat and customer |
| Product-page availability and expiry window | US-SHP-07 | **HO-SHP-001** | `inventory.v_sellable_stock` and `GetSellableStockQuery` (§12.6) |
| Back-in-stock alerts | US-SHP-13 | HO-SHP-001 | The same view (a sweep over it is recommended over events) |
| Margin, revenue by origin, stock valuation reports and scheduling | US-IMP-05 AC2 (report), FR-RPT-* | **HO-RPT-001** | `inventory.v_batch_unit_cost` (landed ?? purchase) and the stock-on-hand read |
| Supplier invoices, accounts payable | — | Not in the backlog (§1.2 excludes general ledger) | Nothing |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No SKU catalogue at baseline** (HO-SHP-001 being written in parallel) | Everything reads SKUs through `ISkuDirectory` (§8.2); its one adapter is written against HO-SHP-001's published read model. Until HO-SHP-001 is merged the slice cannot run — §20.1 |
| **Clinical items (medicines, consumables) may not be SKUs in HO-SHP-001** | OQ-1. If HO-SHP-001 does not cover them, clinical stock cannot be received and the EMR/PHM contracts have nothing to deduct |
| **Object storage is not provisioned** | Consignment documents are recorded as metadata + storage key (HO-CAT-001's photo pattern, ADR-113). US-IMP-03 AC2 is met for everything **except** downloading the file bytes — ACCEPTANCE records it as provisional (OQ-14) |
| No SMS or email provider (HO-NTF-001 OQ-1) | Recall notices are enqueued, logged and delivered **in-app only** outside Development |
| SRS Q-8 (source countries, per-origin licences) — **US-IMP-03 is listed as blocked by it** (backlog §18.4) | Built to the default: any ISO-3166 country; no per-origin rule (OQ-15). ACCEPTANCE marks A4 provisional on Q-8 |
| SRS Q-1 (regulatory recall reporting, retention periods) | No regulator report; consignments and their documents are never deleted (NFR-LEG-2) |
| Staff do not have an in-app inbox (HO-NTF-001 R6) | Alerts are rows in this context shown in the back office; no staff message is sent (ADR-110, OQ-11) |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, a second host, a `CommandService` layer, `202 Accepted`,
`[Authorize]`. Specific to this slice:

- **No editable stock figure.** No aggregate, column, command or endpoint sets an on-hand or reserved
  quantity. The projection is written only by the ledger triggers (ADR-099).
- **No application lock, advisory lock or `SELECT … FOR UPDATE` in C#** to protect stock — the
  `CHECK` inside the trigger's row lock is the control (ADR-099).
- **No stored `Expired` batch status** and **no parameter, flag, role or setting that lets an expired
  batch be sold, dispensed, reserved or fed** — not `force`, not `ignoreExpiry`, not an administrator
  bypass (ADR-101). U-E9 fails the build if such a parameter appears on `IStockLedger`.
- **No `UPDATE` or `DELETE` of a ledger or reservation entry** — corrections are new entries.
- **No enqueue from an event handler** (HO-NTF-001 ADR-057).
- **No payment, invoice or accounts-payable type.**
- **No file bytes** in a command, controller or table (§4.3).
- **No per-consumer "who received this batch" contract** (`IBatchConsumptionQuery`) — the ledger stamp
  replaces it (ADR-104).

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001 merged** | ❌ **not implemented at baseline** — §20.1 |
| **HO-SHP-001 written and merged** (SKUs with the fields of §8.2) | ❌ **not written at baseline** — §20.1. Phases B and C (suppliers, POs, consignments) need SKU ids only for PO lines; nothing in this slice runs without SKUs |
| PostgreSQL 17.5 `catecommerce_dev` with IANA time-zone data | ✅ assumed as for HO-APT-001; FI-4 verifies the zone data in Phase D step 1 |
| `dotnet-ef` 9.0.0 | ✅ |
| New NuGet packages | **None** |

The shared-database rules of HO-CAT-001 §16.1, HO-ACC-001 §16.1 and HO-APT-001 §16.1 apply. **Every
integration test creates its own branch** (HO-ADM-001 endpoints) **and its own stock locations** in it,
and mints its own SKUs through HO-SHP-001's endpoints, so no stock, FEFO, alert or availability
assertion can see another test's rows (§16.1).

---

## 6. Governing architecture documents

Where any of these disagrees with a `.ai/templates/*.cs` file, the markdown wins.

| Document | What you need from it |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer responsibilities; §4 "When a Domain Service is warranted" names **FEFO**; §5 events vs outbox; §6.2 the two rules that fail silently (runtime-type dispatch, `ValidateCommand = true`) |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | `IInMemoryDispatcher` only; commands synchronous with their real result; queries never write |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Rule 1 (one aggregate per command — **four recorded exceptions**, ADR-103, ADR-105); rule 2 (ids, not navigations); **rule 3 names stock ledger entries as the unbounded collection that must stay outside any aggregate**; rule 4 names **FEFO** as a domain service. Its context table lists `Supplier`, `Consignment`, `Batch` separately — ADR-097 folds them into one `Inventory` context (OQ-2). The cross-context table says expiry is the batch's own state and the recall trace is a Read-layer query — both honoured |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | Every namespace in §11/§12. `Inventory` is the `[Feature]` segment (ADR-097); `Supplier`, `PurchaseOrder`, `Consignment`, `Batch`, `Stock` from its feature list are **not** used as separate segments |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 binding.** ADR-011 (real transactions and FKs within a module — the stock ledger is named), ADR-015 (FEFO as a domain service), ADR-016, ADR-021 (per-call save — why enlisting matters), ADR-022 |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §3 handler shape; §5 — the `IStockAllocationService` sketch there is the ancestor of `FefoAllocator` + `IStockLedger` (§8.7, §8.8); §6 rule placement |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | `AsNoTracking`, explicit projections, paging (UI-4), **§5 database views created by migrations** — `v_sellable_stock`, `v_batch_unit_cost` |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §2 repository (already implemented — do not re-copy); §4 `OwnsMany`; §6 `xmin`; §7 indexes; §8 types; §9 migrations. Plus ADR-021's three corrections on every configuration |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §3 a failing event handler fails the request after commit — why nothing load-bearing is an event handler here; §4 the outbox the recall notice rides |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | The enumerations of §10.1, persisted by `Value`; ours is in `Shared` (ADR-022) |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | Validators as `IValidator<T>`; domain services Scoped; the worker creates a scope per unit of work |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs` shape, lifetimes, the never-register list |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File order within each phase |
| [`.ai/flows/import-to-shelf-flow.md`](../../.ai/flows/import-to-shelf-flow.md) | The flow this slice builds; its "Remaining work" items are each settled by an ADR or an OQ here |
| [`.ai/flows/batch-recall-flow.md`](../../.ai/flows/batch-recall-flow.md) | Invariants: quarantine immediate and atomic; trace covers all consumption paths; notices through the outbox. **Its suggested `IBatchConsumptionQuery` fan-out is replaced** by the ledger stamp — ADR-104 records why |
| [`HO-CAT-001`](HO-CAT-001.md) · [`HO-ACC-001`](HO-ACC-001.md) · [`HO-ADM-001`](HO-ADM-001.md) · [`HO-NTF-001`](HO-NTF-001.md) · [`HO-APT-001`](HO-APT-001.md) | F1–F30; EF configuration rules; `ActionRoles`; settings; outbox producer rules; **HO-APT-001's exclusion-constraint approach is the precedent for ADR-099** |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001 F28–F30
apply. **F23 is load-bearing here**: an entity added to the scoped `DbContext` before a repository save
commits atomically with it.

This slice needs five more database facts. They are numbered **FI-1 … FI-5** (not F31+) because
HO-SHP-001 is being written in parallel and may take the next F numbers. **They are not yet verified.**
Phase D step 1 verifies each in a rolled-back transaction on `catecommerce_dev`, records the result in
the test sheet, and **stops (§20.3) if any is false**.

| # | Fact to verify | Consequence if true |
|---|---|---|
| FI-1 | An `AFTER INSERT` row trigger on `stock_ledger_entry` that does `INSERT … ON CONFLICT (batch_id, location_id) DO UPDATE SET on_hand = stock_position.on_hand + EXCLUDED.on_hand` on a table with `CHECK (on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand)`: two concurrent transactions each inserting a `-1` entry against a position holding `1` → the second **blocks** on the row lock until the first commits, then fails with SQLSTATE **`23514`** naming `ck_stock_position_balance` | The last-unit race has exactly one winner with no application lock (ADR-099) |
| FI-2 | `RAISE EXCEPTION '…' USING ERRCODE = '23514', CONSTRAINT = 'tg_stock_batch_expired'` inside a trigger surfaces in Npgsql as `PostgresException` with `SqlState = "23514"` and `ConstraintName = "tg_stock_batch_expired"` | Trigger refusals map through the same named-constraint filter as real constraints (§12.5) |
| FI-3 | EF Core 9 surfaces both FI-1 and FI-2 from `SaveChangesAsync` as `DbUpdateException` whose `InnerException` is that `PostgresException` | HO-ACC-001's filter shape applies unchanged |
| FI-4 | `(now() AT TIME ZONE 'Asia/Dhaka')::date` and `'Europe/London'` resolve on the server (tzdata present) | The database can judge "expired" in the branch's local date (ADR-101) |
| FI-5 | In the ledger trigger, `SELECT status FROM inventory.batch WHERE id = NEW.batch_id FOR SHARE` makes a concurrent `UPDATE inventory.batch SET status = …` (the recall) wait, and vice versa | No window exists in which a recalled batch can still be consumed (US-IMP-11 AC1, I-I7) |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Inventory context — schema `inventory`
StockLocationAggregate        (root)  → BranchId                          inventory.stock_location
ProductStorageProfileAggregate(root, Id == ProductId)                     inventory.product_storage_profile
SupplierAggregate             (root)                                      inventory.supplier
PurchaseOrderAggregate        (root)  → SupplierId, BranchId, DeliverToLocationId   inventory.purchase_order
└── Lines [owned many ≤ 200]  PurchaseOrderLine                           inventory.purchase_order_line
ConsignmentAggregate          (root)  → BranchId, PurchaseOrderIds[]      inventory.consignment
├── HsCodes        [owned many ≤ 20]                                      inventory.consignment_hs_code
├── CostComponents [owned many ≤ 20]                                      inventory.consignment_cost
├── ExchangeRates  [owned many ≤ 10]                                      inventory.consignment_exchange_rate
├── Documents      [owned many ≤ 50]                                      inventory.consignment_document
├── LandedCost     [owned many ≤ 500] LandedCostLine                      inventory.consignment_landed_cost_line
└── StatusHistory  [owned many ≤ 20]
GoodsReceiptAggregate         (root)  → BranchId, SupplierId?, PurchaseOrderId?, ConsignmentId?   inventory.goods_receipt
└── Lines [owned many ≤ 200]  GoodsReceiptLine                            inventory.goods_receipt_line
BatchAggregate                (root)  → SkuId, GoodsReceiptId, ConsignmentId?   inventory.batch
├── Recall?        [owned one]  BatchRecall
└── StatusHistory  [owned many ≤ 20]
StockLedgerEntryAggregate     (root, append-only, immutable)              inventory.stock_ledger_entry
StockReservationEntryAggregate(root, append-only, immutable)              inventory.stock_reservation_entry
StockTransferAggregate        (root)  → From/To LocationId                inventory.stock_transfer (+ _line)
StockAdjustmentAggregate      (root)  → LocationId                        inventory.stock_adjustment (+ _line)
StockCountAggregate           (root)  → LocationId                        inventory.stock_count (+ _line ≤ 2000)
ReorderPolicyAggregate        (root)  → SkuId, LocationId (unique pair)   inventory.reorder_policy
StockAlertAggregate           (root)                                      inventory.stock_alert
StorageBreachReportAggregate  (root)  → LocationId                        inventory.storage_breach_report

Not an aggregate — a projection written only by database triggers (ADR-099)
stock_position (batch_id, location_id) → sku_id, on_hand, reserved       inventory.stock_position
Views (read contracts, ADR-116)
inventory.v_sellable_stock · inventory.v_batch_unit_cost
```

**Why ledger and reservation entries are roots.** They accumulate without bound
(`.ai/bounded-contexts.md` rule 3 names the stock ledger); each row is written once and never changed.
They have no mutation method and raise no domain event.

**Why one `Inventory` context, not four** (ADR-097). Supplier, PO, consignment, batch and stock share
one transaction boundary at goods receipt, one role set in Appendix D, and one schema of foreign keys
(ADR-011). Splitting them into four `[Feature]` segments would put the ledger's FKs across contexts.

**One aggregate mutated per command**, with **four recorded exceptions**, each committed in **one**
`SaveChangesAsync` (F23) and each creating only *new, append-only or newly created* rows beside the one
aggregate it mutates:

1. **Every stock movement** — the command's aggregate (receipt, transfer, adjustment, count, batch,
   breach report, or a consumer's order/visit/prescription/stay) plus the ledger or reservation entries
   enlisted through `IInventoryLedgerWriter` / `IStockLedger` (ADR-103).
2. **Posting a goods receipt** creates the receipt **and** its new batches (ADR-105).
3. **A FEFO override** enlists a `BusinessEvent` audit entry beside the movement (ADR-102).
4. **Notifying a recall** enqueues notification requests beside the batch (HO-NTF-001's own rule).

### 8.2 The SKU contract assumed from HO-SHP-001 — ADR-111

`HO-SHP-001` owns products and SKUs. This slice never reads its tables directly except through **one
port and one adapter**:

```csharp
// Domain.DomainServices
public interface ISkuDirectory
{
    Task<SkuStockProfile?> GetAsync(Guid skuId, CancellationToken ct);
    Task<IReadOnlyList<SkuStockProfile>> GetManyAsync(IReadOnlyCollection<Guid> skuIds, CancellationToken ct);
    Task<SkuStockProfile?> FindByBarcodeAsync(string barcode, CancellationToken ct);
    Task<IReadOnlyList<SkuStockProfile>> SearchAsync(string text, int take, CancellationToken ct);
}

public sealed record SkuStockProfile(
    Guid SkuId, Guid ProductId, string SkuCode, string DisplayName, string? Barcode,
    StockClass StockClass,            // Food | Medicine | Consumable | General
    bool ExpiryRequired,              // D-2: true for Food and Medicine
    string StockUnit,                 // "each", "bag", "ml", "g" …
    bool AllowsFractionalQuantity,    // false → quantities must be whole
    string? CountryOfOriginCode,      // ISO-3166 alpha-2, food only
    bool IsActive);
```

| Field | Expected from HO-SHP-001 | Default if HO-SHP-001 lacks it (recorded, OQ-1 / OQ-19) |
|---|---|---|
| `SkuId`, `ProductId`, `SkuCode`, `DisplayName` (product name + variant label), `Barcode`, `IsActive` | FR-SHP-2, FR-SHP-3 | — required; §20.1 if absent |
| `CountryOfOriginCode` | FR-SHP-4 | null |
| `StockClass` | An item class on the product | `Food` when the product category is Cat Food, else `General` |
| `ExpiryRequired` | An explicit flag | `StockClass ∈ {Food, Medicine}` |
| `StockUnit`, `AllowsFractionalQuantity` | Per SKU | `"each"`, `false` |

`CatalogueSkuDirectory` (Infrastructure) is the **only** file that knows HO-SHP-001's schema. If
HO-SHP-001's read model differs from what is assumed, that adapter changes and nothing else.

**Quantities** are `decimal` in the SKU's stock unit, stored `numeric(12,3)`, strictly positive on
every command; `AllowsFractionalQuantity = false` → must be whole (`FractionalQuantityNotAllowed`).
Every read returns `stockUnit` beside every quantity.

### 8.3 Locations — US-IMP-06 — ADR-109

**`StockLocationAggregate`** — `BranchId`, `Code` (≤ 20, upper-case, unique per branch), `Name`
(≤ 100), `Kind` (`StockLocationKind`), `IsActive`. Methods `Create`, `Rename`, `Deactivate` (refused
while any position at it has `on_hand > 0` → `StockLocationHasStock`, checked by the handler through
a read), `Reactivate`.

**What may happen where** — `StockLocationRules` (Shared, static, one table, tested exhaustively):

| Kind | Receipt | Sale | Dispense / ClinicalUse | BoardingUse | Return (in) | Transfer in/out, Adjustment, Count, Wastage | Counts toward retail sellable |
|---|---|---|---|---|---|---|---|
| `ShopFloor` | ✅ | ✅ | — | — | — | ✅ | ✅ |
| `Warehouse` | ✅ | ✅ | — | — | — | ✅ | ✅ |
| `ClinicDispensary` | ✅ | — | ✅ | — | — | ✅ | — |
| `BoardingStore` | ✅ | — | — | ✅ | — | ✅ | — |
| `Quarantine` | ✅ (inspection hold) | — | — | — | ✅ | ✅ | — |

A movement at a location whose kind does not allow it → `MovementNotPermittedAtLocation`. "Damaged
stock" (FR-IMP-8) is stock moved into `Quarantine` or written off — never a separate counter. OQ-5
records that the table is fixed in code.

**Seed.** The phase A migration seeds, for `AdministrationSeed.MainBranchId`, five locations with fixed
ids in `InventorySeed`: `SHOP` Shop floor, `WH` Warehouse, `DISP` Clinic dispensary, `BRD` Boarding
store, `QUAR` Quarantine. Any branch may have more than one location of a kind **except**
`Quarantine`: exactly one active per branch (`ix_stock_location_one_quarantine`, filtered unique), so
"the branch's quarantine location" is unambiguous for returns and breaches.

### 8.4 Storage requirements — US-IMP-16 part 1

**`ProductStorageProfileAggregate`** — `Id` is the `ProductId` (1:1, created on first write, like
HO-ACC-001's address book), `Condition` (`StorageCondition`: `Ambient`, `CoolDry`, `Refrigerated`,
`Frozen`), `MinTemperatureC?`, `MaxTemperatureC?` (`decimal(4,1)`, −40…60, min ≤ max →
`InvalidTemperatureRange`), `HandlingNotes?` (≤ 500, plain text). Method `Set`. It is **Inventory's**
record of how stock must be kept; HO-SHP-001's customer-facing "storage instructions" text (FR-SHP-4)
is separate and not duplicated. Every SKU lookup, receipt line read, batch and stock read includes
`storage` (null when no profile).

### 8.5 Procurement — US-IMP-01, US-IMP-02, US-IMP-03

**`SupplierAggregate`** — `Code` (≤ 20, upper-case, unique), `Name` (≤ 200), `CountryCode` (ISO-3166
alpha-2, validated with `System.Globalization.RegionInfo` → `InvalidCountryCode`), `Contact` (owned
VO: `ContactName?` ≤ 200, `Phone?` ≤ 30 free text — international numbers are not HO-ACC-001
`MobileNumber`s, `Email?` via HO-ACC-001's `EmailAddress`, `Address?` ≤ 500), `CurrencyCode` (ISO-4217,
the same 3-upper-letter rule as HO-ADM-001's `CurrencyCode` setting type → `InvalidCurrencyCode`),
`LeadTimeDays` (0–365), `PaymentTerms` (≤ 500 text, e.g. *"30% advance, 70% against B/L"*),
`PaymentTermDays?` (0–365), `Notes?` (≤ 1000), `IsActive`. Methods `Create`, `Update`, `Deactivate`,
`Reactivate`. US-IMP-01's six fields are all required except payment term days and notes.

**`PurchaseOrderAggregate`** — `Number` (`PO-{yyyy}-{seq:D6}`, ADR-114), `SupplierId`, `BranchId`
(active branch), `DeliverToLocationId` (a location of the branch that permits `Receipt`), `CurrencyCode`
(defaults to the supplier's; may differ), `ExpectedDeliveryOn` (`DateOnly`), `Notes?`, `Status`,
`Lines`: `LineId`, `SkuId`, `SkuSnapshot` (`SkuCode`, `DisplayName`, `StockUnit`), `OrderedQuantity`,
`UnitCost` (`UnitCost` VO in the PO currency, 4 dp, ≥ 0). One line per SKU (`DuplicatePurchaseOrderSku`).

```
Draft ──Issue──► Issued ──Close(reason)──► Closed
  │                │
  └──Cancel──►  Cancelled ◄── Cancel (only while nothing has been received)
```

| Method | Rules | Event |
|---|---|---|
| `CreateDraft` / `ReplaceDraft` | lines 1…200, SKUs active, quantities > 0 | `PurchaseOrderDraftedEvent` |
| `Issue(today)` | Draft; ≥ 1 line; supplier active (handler); `ExpectedDeliveryOn ≥ today` (branch-local) | `PurchaseOrderIssuedEvent` |
| `Cancel(reason, hasReceipts)` | Draft or Issued; `hasReceipts` false (handler read) else `PurchaseOrderHasReceipts` | `PurchaseOrderCancelledEvent` |
| `Close(reason)` | Issued; reason ≤ 500 required — short-closing a partially received PO | `PurchaseOrderClosedEvent` |

**Receipt progress is derived, never stored** (ADR-112). `PurchaseOrderProgress` (Shared helper, pure):
per line `accepted = Σ accepted quantity of posted receipt lines referencing it`, `rejected = Σ
rejected`, **`outstanding = max(0, ordered − accepted)`** — a rejected unit is still owed (OQ-9);
`receiptState` = `NotReceived` \| `PartiallyReceived` \| `FullyReceived`. Receiving never edits the PO,
so rule 1 holds and there is no counter to drift.

**`ConsignmentAggregate`** — FR-IMP-3's record:

| Field | Type / rule |
|---|---|
| `Reference` | `CN-{yyyy}-{seq:D6}` |
| `BranchId` | Active branch |
| `OriginCountryCode` | ISO-3166 alpha-2 |
| `ShipmentReference` | ≤ 60 (B/L, AWB) |
| `PurchaseOrderIds` | 1…20, each Issued or Closed, **same branch** (`ConsignmentPurchaseOrderMismatch`); suppliers may differ (consolidated freight) |
| `HsCodes` | owned: `Code` matching `^\d{4}(\.?\d{2}){0,3}$`, `Description?` ≤ 200 (`InvalidHsCode`) |
| `ImportPermitNumber`, `HealthCertificateNumber` (health/veterinary certificate), `CustomsDeclarationNumber` | ≤ 60 each, optional until clearance |
| `ExpectedArrivalOn?` | `DateOnly` |
| `Status` | `Planned → InTransit → AtCustoms → Cleared`; any non-`Cleared` → `Cancelled`. `Clear(on)` requires permit, certificate and declaration numbers (`ClearanceDocumentsMissing`) |
| `CostComponents` | owned: `Kind` (`Freight`, `Duty`, `Clearing`, `Insurance`, `Other`), `Amount` (`Money`, any currency), `Note?` ≤ 200 |
| `ExchangeRates` | owned: `CurrencyCode`, `RateToBase` (`decimal(18,6)` > 0), `RecordedOn`. One per currency |
| `Documents` | owned ≤ 50, **never removed** (NFR-LEG-2): `DocumentId`, `Kind` (`ImportDocumentKind`), `DocumentNumber?`, `IssuedOn?`, `FileName` ≤ 255, `StorageKey` ≤ 500, `ContentType` ∈ `application/pdf`, `image/jpeg`, `image/png`, `SizeInBytes` ≤ 20 MB, `UploadedBy`, `UploadedOn`, `SupersededOn?`, `SupersededBy?` |
| `LandedCost` | owned lines + `AllocatedOn?`, `AllocatedBy?`, `CostsChangedSinceAllocation` — §8.11 |
| `StatusHistory` | owned ≤ 20 |

Methods: `Plan`, `UpdateDetails`, `AdvanceStatus(to, on)`, `Cancel(reason)`, `AddDocument`,
`SupersedeDocument`, `ReplaceCostsAndRates` (sets `CostsChangedSinceAllocation` when an allocation
exists), `RecordLandedCostAllocation`. A cancelled consignment is kept, with its documents.

### 8.6 Goods receipt and batches — US-IMP-04, US-IMP-06, US-IMP-15 — ADR-105

**`GoodsReceiptAggregate`** — posted in **one** command; there is no draft (the receiving form keeps
its own draft — HO-IMP-002 ADR-FE-043). Immutable once posted; a mistake is corrected by an adjustment
(reason `Correction`).

| Field | Rule |
|---|---|
| `Number` | `GRN-{yyyy}-{seq:D6}` |
| `BranchId` | Active branch |
| `SupplierId?`, `PurchaseOrderId?`, `ConsignmentId?` | PO Issued, of this branch, supplier match; consignment **`Cleared`** (`ConsignmentNotCleared`) and containing the PO when both are given |
| `ReceivedOn`, `ReceivedBy` | now / caller |
| `DeliveryReference?` | ≤ 60 (supplier delivery note) |
| `Lines` | 1…200 |

`GoodsReceiptLine`: `LineId`, `SkuId`, `SkuSnapshot`, `PurchaseOrderLineId?`, `LotNumber` (≤ 60,
trimmed), `ManufacturedOn?`, `ExpiresOn?`, `QuantityAccepted` (≥ 0), `QuantityRejected` (≥ 0),
`RejectionReason?` (`RejectionReason`), `RejectionNote?` (≤ 500), `LocationId`, `HoldForInspection`
(bool, OQ-16), `UnitCost?` (in the PO currency, or any currency without a PO), `ExchangeRateToBase?`,
`BatchId?` (minted in the controller; null when nothing accepted).

Line rules (each tested, each with its own code):

1. `accepted + rejected > 0`; `rejected > 0` → `RejectionReason` required (`RejectionReasonRequired`).
2. `accepted > 0` → `LotNumber` required.
3. **D-2** — `accepted > 0` and the SKU's `ExpiryRequired` → `ExpiresOn` required → **`ExpiryDateRequired`**
   (*"Food and medicine must be received with an expiry date."*). US-IMP-04 AC2.
4. `ExpiresOn` present and `BatchExpiryRules.IsExpired(ExpiresOn, branchToday)` → **`BatchAlreadyExpired`**
   — expired goods are rejected (reason `Expired`), never accepted.
5. `ManufacturedOn ≤ branchToday` and `ManufacturedOn < ExpiresOn` (`InvalidBatchDates`).
6. PO line given → same SKU (`PurchaseOrderLineMismatch`); `accepted` + previously accepted ≤ ordered
   (`ReceiptExceedsOutstanding`, OQ-9 tolerance 0); `UnitCost` = the PO line's.
7. No PO line → `UnitCost` required (`UnitCostRequired`) — every batch has a cost for valuation.
8. Cost currency ≠ base → rate from the consignment (`ExchangeRateMissing`) or, with no consignment,
   `ExchangeRateToBase` required on the line.
9. Location in the branch and permits `Receipt`.
10. No two lines with the same `(SkuId, LotNumber, ExpiresOn)` (`DuplicateReceiptLine`).

**Posting** (`PostGoodsReceiptCommandHandler`): validate → for each line with `accepted > 0`, create a
`BatchAggregate` and **enlist** it (`IBatchRegistry.Enlist`) → build one `Receipt` ledger draft per
batch (`+accepted` at the line's location) and **enlist** through `IInventoryLedgerWriter` →
`receiptRepository.SaveAsync(receipt)` — **one commit** (ADR-105). Rejected quantities never touch the
ledger; they are recorded on the line and appear in PO progress.

**`BatchAggregate`**

| Field | Notes |
|---|---|
| `SkuId`, `SkuSnapshot` | Code, name, unit, class — reads never join HO-SHP-001 for a name |
| `LotNumber`, `ManufacturedOn?`, `ExpiresOn?` | From the line |
| `GoodsReceiptId`, `GoodsReceiptLineId`, `ConsignmentId?`, `SupplierId?` | Provenance (FR-IMP-4 "the consignment it arrived on") |
| `OriginCountryCode?` | Consignment's, else the SKU's |
| `ReceivedOn`, `ReceivedQuantity` | Informational; stock is the ledger's |
| `PurchaseUnitCost` | `UnitCost` in its currency |
| `PurchaseUnitCostBase` | `UnitCost` in base currency (rate applied at receipt) |
| `Status` | `BatchStatus` — §8.6.1 |
| `StatusReason?`, `StatusChangedOn`, `StatusChangedBy`, `StatusHistory` | |
| `Recall?` | Owned `BatchRecall` — §8.12 |

A batch is created per accepted receipt line — the same lot arriving twice is two batches (the SRS
§1.3 definition ties a batch to its consignment). Unique `(goods_receipt_id, sku_id, lot_number,
expires_on)`.

#### 8.6.1 Batch states — Appendix C mapping — ADR-101

```
             ┌── Quarantine(reason) ──► Quarantined ── Release ──┐
Available ◄──┘                              │                     │
   │  ▲                                     │                     ▼
   │  └────────────── Release ──────────────┘                 Available
   ├── Recall ──► Recalled ── WithdrawRecall ──► Quarantined
   └── WriteOff(reason) ──► WrittenOff        (also from Quarantined and Recalled)
```

| Appendix C state | Here |
|---|---|
| `in_transit` | Not a batch state: goods in transit are the consignment's status. A batch exists from receipt |
| `received` → `available` | Receipt creates `Available`, or `Quarantined` with `HoldForInspection` |
| `quarantined`, `recalled`, `written_off` | `Quarantined`, `Recalled`, `WrittenOff` |
| **`expired`** | **Derived**, never stored: `BatchExpiryRules.IsExpired(ExpiresOn, branchLocalToday)` ⇔ `ExpiresOn` is not null **and** `branchLocalToday > ExpiresOn`. Usable through the whole of its expiry date (OQ-20). Every read returns `isExpired` and `daysToExpiry` |

| Method | From | Event |
|---|---|---|
| `Quarantine(reason)` | Available | `BatchQuarantinedEvent` |
| `Release` | Quarantined | `BatchReleasedEvent` |
| `Recall(dto)` | Available, Quarantined | `BatchRecalledEvent` (contract — §11.5) |
| `WithdrawRecall(reason)` | Recalled | `BatchRecallWithdrawnEvent` — goes to **Quarantined**, never straight back to sale |
| `WriteOff(reason)` | Available, Quarantined, Recalled | `BatchWrittenOffEvent` — the handler enlists one `Wastage` entry per position with `on_hand > 0`, so nothing remains |
| `RecordRecallNotification(count, on, by)` | Recalled | `BatchRecallNotifiedEvent` |

Anything else → `InvalidBatchTransition` naming both states.

### 8.7 The ledger, positions and reservations — US-IMP-15, US-IMP-08 — ADR-098, ADR-099, ADR-100

**`StockLedgerEntryAggregate`** — append-only. Created only by `EfStockLedger` (§8.8); no method but
its factory; `EventPublishOption.DoNotPublish` semantics (it is enlisted, never saved through its own
repository).

| Column | Notes |
|---|---|
| `Id` | minted |
| `Sequence` | `bigint GENERATED ALWAYS AS IDENTITY` — total order; EF reads, never writes it |
| `OccurredOn` | now |
| `BranchId`, `LocationId`, `SkuId`, `BatchId` | FKs within the schema (ADR-011) |
| `Kind` | `MovementKind` |
| `QuantityDelta` | `numeric(12,3)`, ≠ 0, sign fixed by `Kind` (`ck_stock_ledger_sign`) |
| `ConsumesReservation` | bool — a `Sale` that consumes a reservation line |
| `ReferenceKind`, `ReferenceId`, `ReferenceLineId?`, `ReferenceLabel?` (≤ 60, e.g. `GRN-2026-000012`, `Order PC-001234`) | **The reference document** (FR-IMP-15) |
| `CustomerId?`, `CatId?` | **The recall stamp** (ADR-104) — required by rule for consumer kinds, §8.8 |
| `FefoOverridden`, `OverrideReason?` (≤ 500) | US-IMP-07 AC2 |
| `PerformedBy` | `UserContext.UserId` (a worker uses `SystemActors.InventoryWorker`) |
| `Note?` | ≤ 500 |

**`MovementKind`** and its sign — the FR-IMP-15 list plus the clinical and boarding uses:

| Kind | Sign | Written by |
|---|---|---|
| `Receipt` | + | Goods receipt |
| `TransferOut` / `TransferIn` | − / + | Transfer, breach quarantine |
| `AdjustmentIn` / `AdjustmentOut` | + / − | Adjustment |
| `CountGain` / `CountLoss` | + / − | Count commit |
| `Wastage` | − | Batch write-off; adjustment reasons `Expired`, `RecallDisposal` |
| `Sale` | − | HO-ORD-001 (online dispatch, POS) |
| `Dispense` | − | HO-PHM-001 |
| `ClinicalUse` | − | HO-EMR-001, HO-SRG-001 |
| `BoardingUse` | − | HO-BRD-001 |
| `Return` | + | HO-ORD-001 (into quarantine) |

`Sale`, `Dispense`, `ClinicalUse` and `BoardingUse` are the **consuming kinds** (`MovementKindRules.IsConsuming`).

**`StockReservationEntryAggregate`** — append-only: `Id`, `Sequence`, `OccurredOn`, `BranchId`,
`LocationId`, `SkuId`, `BatchId`, `Kind` (`Reserve` + / `Release` −), `QuantityDelta`, `ReferenceKind`,
`ReferenceId`, `ReferenceLineId`, `CustomerId?`, `CatId?`, `FefoOverridden`, `OverrideReason?`,
`PerformedBy`. The **remaining reservation** for a key `(ReferenceKind, ReferenceId, ReferenceLineId,
BatchId, LocationId)` = Σ its reservation entries + Σ its consuming ledger entries (negative). Nothing
stores it.

**`stock_position`** — `(batch_id, location_id)` primary key, `sku_id`, `on_hand numeric(12,3)`,
`reserved numeric(12,3)`, `last_movement_on`. **Written only by two triggers**:

| Trigger (after insert) | Effect on the position row |
|---|---|
| `tg_stock_ledger_apply` on `stock_ledger_entry` | `on_hand += delta`; if `consumes_reservation` also `reserved += delta` — **one statement** |
| `tg_stock_reservation_apply` on `stock_reservation_entry` | `reserved += delta` |

and constrained by **`ck_stock_position_balance CHECK (on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand)`**.
The row lock taken by `INSERT … ON CONFLICT DO UPDATE` serialises concurrent movements on one batch at
one location; the loser re-reads the committed row and fails the `CHECK` (FI-1). What that one
constraint means:

| Attempt | Why it fails |
|---|---|
| Deduct more than is on hand | `on_hand < 0` |
| Unreserved deduction that would eat another customer's reservation | `on_hand < reserved` |
| Reserve more than is free | `reserved > on_hand` |
| Two concurrent claims on the last free unit | The second sees the first's committed row |

Every refusal is SQLSTATE `23514` on `ck_stock_position_balance` → **`409 InsufficientStock`**
(*"That quantity is no longer available — someone may have just taken it. Refresh and try again."*).

**Defence in depth on the projection**: `tg_stock_position_guard` (before insert/update/delete on
`stock_position`) refuses unless `current_setting('inventory.ledger_apply', true) = 'on'`, which only
the two apply-triggers set with `set_config(…, true)` immediately before, and reset immediately after,
their upsert. A direct `UPDATE inventory.stock_position` from any session fails (I-D7).

**Usability trigger** — `tg_stock_batch_usable` (before insert) on both entry tables, for consuming
kinds and `Reserve` only: reads the batch `FOR SHARE` (FI-5) and the location's branch zone, and raises
`23514` with `CONSTRAINT = 'tg_stock_batch_expired'` when the batch is expired in branch-local date
(FI-4), or `'tg_stock_batch_not_usable'` when its status is not `Available`. **The application checks
the same things first** so users get specific messages; the trigger exists so that no future consumer
can forget (ADR-101).

**Immutability triggers** — `tg_stock_ledger_immutable` and `tg_stock_reservation_immutable` refuse
`UPDATE`, `DELETE` and `TRUNCATE` (the HO-ACC-001 audit-trigger pattern). Both entry roots are
excluded from HO-ACC-001's change capture — each row already records who, what, when and why
(ADR-098).

### 8.8 The stock contract — `IStockLedger` — ADR-103

Every movement in the solution goes through **one class**, `EfStockLedger` (Infrastructure), behind
two ports in `Domain.DomainServices`:

```csharp
/// Used by other contexts. Every method validates, then ENLISTS rows in the scoped DbContext
/// without saving; the caller's own repository save commits them with its aggregate (F23).
/// Plan and read methods write nothing.
public interface IStockLedger
{
    Task<StockResult<StockPlan>> PlanAsync(StockDemand demand, CancellationToken ct);
    Task<StockResult<StockPlan>> ReserveAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct);
    Task<StockResult<Unit>>      ReleaseAsync(StockReference reference, CancellationToken ct);           // all remaining for the reference (or one line)
    Task<StockResult<IReadOnlyList<StockMovementLine>>> ConsumeReservationAsync(StockReference reference, StockParty party, CancellationToken ct); // → Sale
    Task<StockResult<IReadOnlyList<StockMovementLine>>> DeductAsync(StockDemand demand, StockReference reference, StockParty party, CancellationToken ct);
    Task<StockResult<IReadOnlyList<StockMovementLine>>> ReceiveReturnAsync(StockReturn stockReturn, StockReference reference, StockParty party, CancellationToken ct);
    void RecordShortfall(StockShortfall shortfall);   // US-CLN-15 AC2 → a Shortfall alert, enlisted

    // --- added 2026-09-23, README C-2 follow-ups (HO-PHM-001 OQ-16/17, HO-ORD-001 OQ-19) ---

    /// Correcting a discrepancy another context has already proved: a controlled-drug register
    /// correction (HO-PHM-001 C7), never a routine stock edit. Reason ≥ 5 chars; over
    /// `inventory.adjustmentApprovalThreshold` it also needs `ApprovedByStaffId`. Enlists like the
    /// rest, and writes one BusinessEvent audit entry naming the caller's reference.
    Task<StockResult<IReadOnlyList<StockMovementLine>>> AdjustAsync(
        StockAdjustment adjustment, StockReference reference, StockParty party, CancellationToken ct);

    /// Per-batch balances across every location of the branch — for reconciling a register against
    /// stock (US-PHM-07). Read-only; includes reserved quantity separately so a reconciliation can
    /// tell "missing" from "spoken for".
    Task<StockResult<IReadOnlyList<BatchBalance>>> GetBranchBalancesAsync(
        Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct);

    /// Added 2026-10-09 (HO-PHM-001 owner decision A). Every batch held at the branch's ACTIVE
    /// clinic dispensary for the SKUs (on-hand or reserved above zero), with the batch status.
    /// Read-only; Pharmacy plans a dispensing from it and never reads Inventory's positions.
    Task<StockResult<IReadOnlyList<DispensaryBatch>>> GetDispensaryBatchesAsync(
        Guid branchId, IReadOnlyCollection<Guid> skuIds, CancellationToken ct);

    /// Added 2026-10-09. The branch's active clinic dispensary, or a DispensaryNotFound failure.
    Task<StockResult<DispensaryLocation>> GetClinicDispensaryAsync(Guid branchId, CancellationToken ct);

    /// Added 2026-10-13 (HO-EMR-001 Phase F, ratified by integrator). The distinct SKUs with on-hand stock above zero at the branch's active
    /// ClinicDispensary. Read-only, enlists nothing. A default interface member (answers NotSupported); EfStockLedger implements it.
    Task<StockResult<IReadOnlyList<Guid>>> GetDispensarySkuIdsAsync(Guid branchId, CancellationToken ct) =>
        Task.FromResult(StockResult<IReadOnlyList<Guid>>.Failure("NotSupported", "This ledger cannot list dispensary items.", nameof(branchId)));
}

StockAdjustment(Guid BranchId, Guid SkuId, Guid BatchId, Guid LocationId, decimal QuantityDelta,
                string Reason, Guid? ApprovedByStaffId)
BatchBalance(Guid SkuId, Guid BatchId, string LotNumber, DateOnly? ExpiresOn, Guid LocationId,
             decimal OnHand, decimal Reserved)
DispensaryBatch(Guid SkuId, Guid BatchId, string LotNumber, DateOnly? ExpiresOn, BatchStatus BatchStatus,
                Guid LocationId, decimal OnHand, decimal Reserved)       // Free = OnHand - Reserved
DispensaryLocation(Guid LocationId, string Code)

/// Used only by this context's own handlers (Receipt, Transfer*, Adjustment*, Count*, Wastage).
public interface IInventoryLedgerWriter
{
    Task<StockResult<Unit>> AppendAsync(IReadOnlyList<StockMovementDraft> drafts, CancellationToken ct);
}
```

| Type | Shape |
|---|---|
| `StockDemand` | `BranchId`, `SkuId`, `Quantity`, `Purpose` (a consuming `MovementKind`), `LocationIds?` (null → every active location of the branch permitting the purpose), `ExplicitLines?` `[(BatchId, LocationId, Quantity)]`, `OverrideReason?` |
| `StockReference` | `ReferenceKind`, `ReferenceId`, `ReferenceLineId?`, `ReferenceLabel?` |
| `StockParty` | `CustomerId?`, `CatId?` |
| `StockPlan` / `StockMovementLine` | lines of `BatchId`, `LotNumber`, `ExpiresOn?`, `LocationId`, `Quantity`, `FefoOverridden` — enough for a pick list or a medicine label (US-PHM-05) |
| `StockResult<T>` | `IsSuccess`, `Value`, or `ViolationCode`, `Message`, `PropertyName` — the caller converts a failure into its `CommandResponse`; **the port never throws for a business rule** |

> **Quarantine disposition stays inside Inventory** (HO-ORD-001 OQ-19). `ReceiveReturnAsync` puts a
> return into the branch's `Quarantine` location and Ordering's job ends there. Deciding what happens
> next — restock, write off, send back to the supplier — is an inventory judgement made on the
> quarantine screen (§12.x, `IInventoryLedgerWriter`), not something a consuming context asks for.
> **Reallocating** a picked line to a different batch is likewise not a new method: it is
> `ReleaseAsync` followed by `ReserveAsync` with `ExplicitLines` and an `OverrideReason`, which rule 5
> already audits.

**Rules every method applies** (tested once, in the port):

1. SKU exists and is active; quantity > 0 and whole unless fractional is allowed.
2. **Party stamp**: `Sale` requires `CustomerId` *unless* `ReferenceKind = PosSale` (anonymous counter
   sales are allowed and traced as "unidentified"); `Dispense` requires `CatId` and `CustomerId`;
   `BoardingUse` requires both; `ClinicalUse` requires `CatId` → `StockPartyRequired`.
3. Purpose permitted at every location used (§8.3).
4. Batch `Available` and not expired in branch-local date → `BatchNotUsable` / **`BatchExpired`**
   (*"Batch {lot} expired on {date} and cannot be sold, dispensed or fed."*). **There is no parameter
   that relaxes this** — U-E9 reflects over the port and every DTO it takes and fails on any member
   named like `/force|bypass|ignore|allowExpired|override(?!Reason)/i`.
5. **FEFO** — without `ExplicitLines`, the plan is `FefoAllocator.Allocate` (§8.9). With them, the plan
   is compared to FEFO's; if it differs, `OverrideReason` (≥ 5 chars) is required →
   `AllocationOverrideReasonRequired`, entries carry `FefoOverridden = true` and the reason, and one
   `BusinessEvent` audit entry (`FefoOverridden`, entity = the reference, before = FEFO plan, after =
   chosen plan) is enlisted (ADR-102). **An override may choose a different usable batch; it can never
   choose an unusable one** (rule 4 runs first).
6. Not enough usable, unreserved quantity → `InsufficientStock` with the available quantity in the
   message. A race after planning is caught by the `CHECK` at the caller's commit (`409`).
7. `ConsumeReservationAsync` consumes exactly the remaining reservation of the reference; the batch
   must still be usable — **a recalled or expired reserved batch cannot be consumed**, so HO-ORD-001
   cannot dispatch it (US-IMP-11 AC4, BR-14). Orders must release and re-reserve.
8. `ReceiveReturnAsync` writes `Return` into the branch's `Quarantine` location only, for a batch the
   party actually received (a consuming entry for the same reference exists → else
   `ReturnNotTraceable`); recalled batches may be returned.

**Consumer obligations**, binding on HO-ORD, HO-EMR, HO-PHM, HO-SRG and HO-BRD (and stated in their
handovers): call the port **in the command handler before the aggregate save**; never from an event
handler; always pass the reference and the party; record the returned batch lines on their own
aggregate (D-4); map `StockResult` failures to `400` and the commit-time `23514` to `409`.

### 8.9 FEFO — US-IMP-07 — ADR-102

`FefoAllocator` (Domain.DomainServices, **pure**, no I/O):

```
Allocate(quantity, candidates, branchToday) → Allocation(lines[], shortfall)
candidate = (BatchId, LocationId, LocationPriority, ExpiresOn?, ReceivedOn, Status, Free = on_hand − reserved)
```

1. Discard candidates whose batch is not `Available`, is expired (`BatchExpiryRules`), or has `Free ≤ 0`.
2. Order by `ExpiresOn` ascending with **nulls last** (undated goods are FIFO after all dated ones),
   then `ReceivedOn` ascending, then `LocationPriority` (`Warehouse` 1, `ShopFloor` 2, others 3 —
   `StockLocationRules.FefoPriority`), then `BatchId`. Deterministic.
3. Take from each in order until the quantity is met; split across batches and locations as needed.
4. Remainder → `shortfall` (the port turns it into `InsufficientStock`).

`BatchExpiryRules.IsExpired` is the **one** expiry predicate in C#; the trigger mirrors it in SQL, and
U-X2 checks the SQL text against it.

### 8.10 Transfers, adjustments, counts — US-IMP-13, US-IMP-14 — ADR-107, ADR-108

**`StockTransferAggregate`** — `Number` (`TRF-`), `FromLocationId`, `ToLocationId` (different →
`TransferLocationsMustDiffer`; both permit transfer; the caller assigned to both branches),
`Reason?` (≤ 500), `Lines` (1…100: `SkuId`, `BatchId`, `Quantity`), `TransferredOn/By`. Posted in one
command: the transfer is saved with **two** enlisted entries per line (`TransferOut` at the source,
`TransferIn` at the destination, same reference) — US-IMP-13 AC1. Instant, no in-transit state (OQ-12).
Moving an expired or recalled batch is **allowed** (to quarantine it or consolidate for disposal) —
BR-3 forbids use, not movement.

**`StockAdjustmentAggregate`**

| Field | Rule |
|---|---|
| `Number` | `ADJ-` |
| `LocationId` | of the active branch; permits adjustment |
| `Reason` | `AdjustmentReason` — **mandatory** (`AdjustmentReasonRequired`) |
| `Note` | 5…500 chars, **mandatory** (`AdjustmentNoteRequired`) — "a reason is mandatory" means the why, not only a category |
| `Lines` | 1…50: `SkuId`, `BatchId`, `QuantityDelta` (≠ 0) |
| `ValueBase` | `Money` = Σ \|delta\| × unit cost base (landed ?? purchase, §8.11), rounded 2 dp |
| `Status` | `Posted`, `PendingApproval → Posted \| Rejected`, `PendingApproval → Withdrawn` |
| `RequestedBy/On`, `DecidedBy/On`, `DecisionNote?` | |

| Reason | Direction | Extra rule |
|---|---|---|
| `Damage`, `Theft`, `Sampling`, `InternalUse` | − (`AdjustmentOut`) | — |
| `Expired` | − (`Wastage`) | the batch **is** expired (`AdjustmentReasonNotApplicable`) |
| `RecallDisposal` | − (`Wastage`) | the batch is `Recalled` |
| `Found`, `Correction` | + or − (`AdjustmentIn`/`AdjustmentOut`) | — |

Wrong sign for the reason → `InvalidAdjustmentDirection`.

**Approval** (ADR-107): `ValueBase ≤ inventory.adjustmentApprovalThreshold` (branch-resolved) →
`Posted` at once, entries enlisted. Above → `PendingApproval`, **no ledger entry**; `Approve` by a
staff member holding `Stock.ApproveAdjustment` **and different from the requester**
(`ApproverMustDiffer`) posts the entries then — if stock moved meanwhile the `CHECK` answers `409`.
`Reject(note)` / `Withdraw` (requester only) end it. Every decision is audited by change capture.

**`StockCountAggregate`**

| Field | Rule |
|---|---|
| `Number` | `CNT-` |
| `LocationId` | one `Open`/`Submitted`/`AwaitingApproval` count per location (`CountLocationBusy`, filtered unique index) |
| `Scope` | `FullLocation` \| `SelectedSkus` (`SkuIds` 1…200) |
| `Lines` | ≤ 2000: `SkuId`, `BatchId`, `CountedQuantity`, `BookQuantity?` (set at submit), `Variance?`, `CountedBy`, `CountedOn` |
| `Status` | `Open → Submitted → Committed`; `Submitted → AwaitingApproval → Committed \| Submitted (rejected)`; `Open \| Submitted → Cancelled` |
| `VarianceValueBase` | `Money`, set at submit |

1. **Open** — counters record lines (`RecordCountLines`, upsert by `(SkuId, BatchId)`). **Blind**: the
   count sheet read never returns book quantities while `Open` (OQ-13).
2. **Submit** — the handler reads current positions at the location for the scope: every counted line
   gets `BookQuantity` and `Variance = Counted − Book`; for `FullLocation` every position with
   `on_hand > 0` not counted becomes a line counted `0`. Value computed. **Nothing is written to the
   ledger** — US-IMP-14 AC1. The variance report is then fixed.
3. **Commit** — posts **exactly the reported variances** as `CountGain` / `CountLoss` entries with
   `ReferenceKind = StockCount` — US-IMP-14 AC2 (ADR-108: a sale during counting is a real movement;
   posting the frozen delta rather than "set to counted" keeps it). Value above the threshold →
   `AwaitingApproval`, and the same approver rule as adjustments.

### 8.11 Landed cost — US-IMP-05 — ADR-106

`LandedCostAllocator` (Domain.DomainServices, pure):

```
Allocate(batches[(BatchId, AcceptedQuantity, PurchaseUnitCost)], costComponents[], rates, baseCurrency)
  → lines[(BatchId, Quantity, PurchaseCostBase, AllocatedCostBase, LandedUnitCostBase)]
```

1. Every currency present (batch purchase costs, cost components) other than base has a rate →
   else `ExchangeRateMissing` naming the currency.
2. `TotalCostsBase = Σ round2(component.Amount × rate)`.
3. **Basis: accepted quantity** (FR-IMP-5 "across received quantity"; OQ-3). Batch share =
   `TotalCostsBase × qty_b / Σ qty`, computed in minor units and distributed by **largest remainder**,
   so Σ `AllocatedCostBase` = `TotalCostsBase` exactly.
4. `PurchaseCostBase = round2(qty × purchaseUnitCost × rate)`.
5. `LandedUnitCostBase = (PurchaseCostBase + AllocatedCostBase) / qty`, rounded **4 dp**
   (`UnitCost`, not `Money` — it is a rate, not an amount charged).
6. No accepted quantity on the consignment → `NoReceivedQuantityToAllocate`.

`AllocateLandedCostCommand` loads the consignment and its batches (read), calls the allocator, and
`RecordLandedCostAllocation` replaces the lines — **only the consignment is written**; batches are not
touched (rule 1). Re-allocation is allowed at any time; each is audited with before/after by change
capture. Reads flag an allocation as **stale** when `CostsChangedSinceAllocation` is set or the set of
batches received on the consignment differs from the allocated set.

**`inventory.v_batch_unit_cost`** — per batch: `unit_cost_base`, `cost_basis` = `Landed` when an
allocation line exists, else `Purchase`. Stock valuation and near-expiry value at risk read it; margin
reporting (HO-RPT-001) must read it — **US-IMP-05 AC2** is evidenced by the basis switching (I-G2).

### 8.12 Recall — US-IMP-11 — ADR-104

**Declare** — `RecallBatchCommand` (`BatchId`, `ConfirmLotNumber`, `Reason` ≤ 1000, `CustomerAdvice`
≤ 500 plain text — what owners should do): `ConfirmLotNumber` must equal the lot (`RecallConfirmationMismatch`
— a recall is not a mis-click); `batch.Recall(…)` sets `Status = Recalled` and the owned `BatchRecall`
(`RecallNumber` `RCL-…`, reason, advice, `DeclaredOn/By`, `NotifiedOn?`, `NotifiedBy?`,
`NotifiedRecipientCount`, `NotificationRuns`, `ClosedOn?`, `ClosingNote?`). **One row update**:
every position of the batch, at every location and branch, is unusable from that commit — there is no
per-location loop that could half-finish (US-IMP-11 AC1). FI-5 serialises it against in-flight
consumption. Stock stays where it is, shown as *Recalled — held*, until written off (reason
`RecallDisposal`) or returned.

**Trace** — `GetRecallTraceQuery(batchId)` reads the ledger (US-IMP-11 AC2):

| Section | Source |
|---|---|
| Received by customers and cats | Consuming entries of the batch: kind, date, quantity, location, reference label, `CustomerId`, `CatId` |
| Returned | `Return` entries of the batch (netted per reference) |
| **Unshipped — dispatch blocked** | Remaining reservations of the batch (§8.7) — the orders HO-ORD-001 must not dispatch |
| Unidentified | Count and quantity of `PosSale` entries with no customer |
| Stock held | Positions with `on_hand > 0`, per location |

Each customer is resolved **at read time** through HO-ACC-001's merge chain (`merged_into_customer_id`,
≤ 5 hops) — the ledger is immutable, so no `CustomerMergedEvent` handler rewrites it (ADR-104 records
this as the deliberate way ADR-039 is honoured). Each recorded cat shows its **current** owner too
(ownership may have moved since). For entries without a cat (ordinary food orders), the customer's
living cats are listed as **household cats — not specifically recorded** (OQ-18). Anonymised customers
appear as *"Anonymised customer"* with no contact details.

**Notify** — `NotifyRecallRecipientsCommand(BatchId)`:

1. Batch `Recalled` (`RecallNotActive`).
2. `IRecallRecipientResolver.ResolveAsync(batchId)` (Infrastructure, read-only) → distinct recipient
   customer ids = purchasers (resolved through merges) ∪ current owners of recorded cats; excluding
   anonymised and merged-away ids; **excluding those whose request with dedup key
   `recall:{batchId}:{customerId}` already exists** in `notify.notification_request`.
3. At most `RecallRules.MaxRecipientsPerRun` (5 000) per run; the response says how many remain.
4. `outbox.Enqueue` one `inventory.batch-recall` request per recipient (Transactional, **Urgent** —
   OQ-6), `SubjectKind = BatchRecall`, `SubjectId = batchId`, `DeduplicationKey` as above, `BranchId`
   of the batch's receipt.
5. `batch.RecordRecallNotification(count, now, caller)`; `UpdateAsync` — **one commit**. A second
   concurrent click loses on `xmin` (`409`).

Every message is logged by HO-NTF-001 (FR-NTF-6) with the batch as its subject — **"the notifications
are logged"**, US-IMP-11 AC3. A `BusinessEvent` audit row records the run and the count.

**Withdraw** — `WithdrawBatchRecallCommand` (a recall declared by mistake): → `Quarantined`, never
`Available`; pending notices fail `BatchRecallGuard` (valid only while the batch is `Recalled`) and are
cancelled by HO-NTF-001, never sent. **Close** — `CloseBatchRecallCommand(note)`: records completion
once no stock is held (`RecallStockStillHeld` otherwise); the batch stays `Recalled`.

Template (appended to `NotificationTemplateCatalogue`):

| Key | Category | Urgency | Channels | Variables (required on) |
|---|---|---|---|---|
| `inventory.batch-recall` | Transactional (safety, FR-NTF-4) | **Urgent** (OQ-6) | Sms, Email, InApp | `productName` (Text; all), `lotNumber` (Text; all), `expiresOn` (Date), `customerAdvice` (Text; Email, InApp) — plus the standard `clinicName`, `clinicPhone` |

No link: there is no customer recall page (the message carries the advice and the clinic's phone).

### 8.13 Alerts, reorder, breaches, reconciliation — US-IMP-10, 12, 16 — ADR-110, ADR-115

**`StockAlertAggregate`** — `Kind`, `BranchId`, `SkuId?`, `BatchId?`, `LocationId?`, `ThresholdDays?`,
`QuantityAtRaise?`, `ReferenceKind?`/`ReferenceId?`, `DedupKey?` (unique where not null), `RaisedOn`,
`Status` (`Open → Acknowledged → Resolved`; `Open → Resolved`), `AcknowledgedBy/On`, `ResolvedOn`,
`ResolutionNote?`. Filtered unique `(kind, sku_id, location_id) WHERE status <> Resolved` for
`LowStock` and `LedgerMismatch`.

| Kind | Raised by | Dedup |
|---|---|---|
| `ExpiryThreshold` | Worker expiry pass | `expiry:{batch}:{branch}:{threshold}` — never re-raised |
| `Expired` | Worker expiry pass, batch expired with stock in the branch | `expired:{batch}:{branch}` |
| `LowStock` | Worker low-stock pass | one open per SKU × location; **auto-resolved** when usable ≥ reorder point |
| `Shortfall` | `IStockLedger.RecordShortfall` (US-CLN-15 AC2) | none |
| `StorageBreach` | Breach report | one per report |
| `LedgerMismatch` | Worker reconciliation pass | one open per position |

**Expiry thresholds** — `ExpiryAlertRules.Thresholds(settings)` = the distinct non-zero values of
`inventory.expiryAlertFirstDays` (90), `…SecondDays` (60), `…ThirdDays` (30), sorted descending. For a
batch with stock in a branch and `daysLeft = ExpiresOn − branchToday`: `t* = min{ t : daysLeft ≤ t }`;
raise `(batch, branch, t*)` if absent. So a batch received with 45 days left raises **60**, later
**30**, and never a retrospective 90 — US-IMP-10 AC1 without a burst of stale alerts (U-H1).

**Near-expiry report** — `GetNearExpiryReportQuery(branchId, withinDays = max threshold,
includeExpired)`: per batch with stock in the branch — SKU, lot, expiry, `daysRemaining` (negative when
expired), quantity per location, unit cost base and **value at risk** = on hand × unit cost
(`v_batch_unit_cost`), sorted by expiry — US-IMP-10 AC2. CSV export.

**Reorder** — **`ReorderPolicyAggregate`** (`SkuId`, `LocationId`, `ReorderPoint` ≥ 0,
`ReorderQuantity?` > 0, `PreferredSupplierId?`). The **effective reorder point** of an SKU × location
pair that has a position row = its policy's, else `inventory.defaultReorderPoint` (HO-ADM-001's key —
its consumer). `ReorderPoint = 0` silences a pair.

`ReorderSuggestionCalculator` (pure) — US-IMP-12 AC2:

```
avgDaily   = Σ |consuming entries| at the location in the last W days / W       W = inventory.consumptionWindowDays (90)
leadTime   = preferred supplier's LeadTimeDays ?? inventory.defaultLeadTimeDays (14)
cover      = leadTime + inventory.reorderCoverDays (14)
position   = usable at location + outstanding on Issued POs delivering to that location
suggested  = policy.ReorderQuantity  when set
           = max(0, ceil(avgDaily × cover) + reorderPoint − position)  otherwise   (whole units)
```

`usable` = Σ (`on_hand − reserved`) over batches `Available` and not expired. A low-stock alert is
raised when `usable < effective reorder point` — US-IMP-12 AC1.

**Storage breach** — **`StorageBreachReportAggregate`** — `LocationId`, `ObservedOn`, `Condition`
(≤ 500 text), `TemperatureC?`, `DurationMinutes?`, `AffectedBatchIds` (uuid[] ≤ 2000 — batches with
`on_hand > 0` at the location when reported, snapshot), `Status` (`Open → Quarantined → Closed`;
`Open → Closed`), `ReportedBy`, `QuarantinedOn/By`, `ClosedOn/By`, `ClosingNote`. Reporting raises a
`StorageBreach` alert (enlisted) — **flagged for inspection**. `QuarantineStorageBreachStock` moves the
affected batches' stock at that location to the branch's `Quarantine` location with enlisted transfer
entries referencing the report — **location-level quarantine** (ADR-115), distinct from batch status.
The decision afterwards is an ordinary transfer back or a write-off; `Close(note)` records it
(US-IMP-16 AC2).

**Reconciliation** — `GetStockReconciliationQuery`: every position whose `on_hand` or `reserved`
differs from Σ of its entries. Expected empty forever; the worker runs it daily and raises
`LedgerMismatch` alerts (logged at `Error`) if not.

**Branch-local date.** HO-ADM-001 ADR-045 confines zone conversion to its calendar services. This
slice needs one conversion — *instant → branch-local date* — and takes it from
`BranchCalendar.LocalDateOf(timeZoneId, instant)`; **if HO-ADM-001 did not build that method, add it
there** (one static method, §13) rather than converting here. `IInventoryClock.LocalTodayAsync(branchId)`
(Infrastructure) loads the zone and calls it.

### 8.14 Ports and domain services

| Name | Kind | Notes |
|---|---|---|
| `FefoAllocator`, `LandedCostAllocator`, `ReorderSuggestionCalculator` | pure domain services | §8.9, §8.11, §8.13 |
| `BatchExpiryRules`, `StockLocationRules`, `MovementKindRules`, `ExpiryAlertRules`, `PurchaseOrderProgress`, `BatchTransitionRules` | Shared static rules | The aggregate and the reads use the same tables |
| `IStockLedger`, `IInventoryLedgerWriter` | ports → `EfStockLedger` | §8.8 |
| `IBatchRegistry` | port → `EfBatchRegistry` | `Enlist(BatchAggregate)` — receipts only (ADR-105) |
| `ISkuDirectory` | port → `CatalogueSkuDirectory` | §8.2 |
| `IStockPositionReader` | port → `EfStockPositionReader` | Read-only candidates and positions for handlers (FEFO inputs, counts, adjustments) |
| `IRecallRecipientResolver` | port → `RecallRecipientResolver` | §8.12 |
| `IInventoryClock` | port | §8.13 |
| `IDocumentNumberGenerator` | port → `SequenceDocumentNumberGenerator` | `nextval` per `DocumentKind` (ADR-114) |
| `BatchRecallGuard` | `INotificationSubjectGuard` | §8.12 |

Exceptions: reuse `DomainRuleViolationException`. No new type.

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. **This handover owns ADR-097 … ADR-116** (reserved by the
coordinator; the README ADR table is updated by the coordinator, not here).

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-097** | **`Inventory` bounded context**, schema `inventory`, owning suppliers, POs, consignments, receipts, batches, the ledger and stock control — folding `.ai/bounded-contexts.md`'s `Supplier`, `Consignment` and `Batch` rows into one context | Deviates from the doc's table (OQ-2); one transaction boundary at receipt and FKs within one module (ADR-011) |
| **ADR-098** | **The stock ledger and reservation entries are append-only and immutable** in the application (no mutation methods) and the database (triggers refuse `UPDATE`/`DELETE`/`TRUNCATE`); they are excluded from HO-ACC-001 change capture because each row is already an attributable record | FR-IMP-15 AC3; D-3; auditing every ledger row twice doubles the busiest table |
| **ADR-099** | **Current stock is `stock_position`, a projection maintained only by ledger/reservation triggers**, guarded against direct writes; **`ck_stock_position_balance` inside the trigger's row lock is the control against overselling and negative stock**; `23514` → `409 InsufficientStock`. No application or advisory lock | US-IMP-08 AC2 and D-3 together: stock must be derived *and* the last unit must have one winner. A read-then-write check races; a `SUM` at read time cannot enforce anything (FI-1) |
| **ADR-100** | **Reservations are reservation entries, not ledger movements**; a consuming `Sale` decrements on-hand and reserved in **one statement**; remaining reservations are derived; **sellable** = on-hand − reserved over `Available`, unexpired batches in retail-selling locations | FR-IMP-8; putting reserved in a second statement would let the `CHECK` fail spuriously depending on EF's statement order |
| **ADR-101** | **"Expired" is derived** (`branchLocalToday > ExpiresOn`), never stored; enforced in the application and by `tg_stock_batch_usable` on every consuming movement and reservation; **no parameter, role or setting can bypass it**. Appendix C's `in_transit`/`received`/`expired` mapped as in §8.6.1 | BR-3 "no override anywhere"; a stored status needs a job that can fail; Appendix C deviation must be visible |
| **ADR-102** | **FEFO is the pure `FefoAllocator`**: expiry ascending, nulls last, then received, then location priority; an explicit plan that differs from FEFO requires a reason, flags the entries and enlists a `BusinessEvent` audit entry in the same commit (a recorded rule-1 exception) | BR-4, US-IMP-07 AC2; `.ai/bounded-contexts.md` rule 4 |
| **ADR-103** | **`IStockLedger` is the only way any context moves stock**; it enlists rows without saving (HO-NTF-001's outbox pattern) so the consumer's aggregate and the movement commit together — a recorded rule-1 exception for every stock-moving command; consumers must stamp reference, customer and cat | "Inventory is the only module that allocates or deducts stock" (`.ai/flows/import-to-shelf-flow.md`); designing it now avoids building clinical deduction twice (README) |
| **ADR-104** | **Recall** = one batch-row status change (atomic across locations, serialised against consumption by `FOR SHARE`); the **trace is a ledger read** with customers resolved through merges and cats through current ownership at read time — no `CustomerMergedEvent` handler rewrites immutable rows; notices go one per distinct customer with a dedup key and a subject guard. **Replaces** `.ai/flows/batch-recall-flow.md`'s per-module `IBatchConsumptionQuery` | Fan-out needs every future module to remember a contract; the stamp is enforced by the port at write time. The merge-contract deviation must be judged by ARCHITECTURE, not discovered |
| **ADR-105** | **Goods receipt is posted in one command** that creates the receipt, one batch per accepted line (enlisted) and their ledger entries, in one commit (a recorded rule-1 exception); receipts are immutable; corrections by adjustment | A receipt half-posted is stock without a batch or a batch without stock |
| **ADR-106** | **Landed cost** is allocated by accepted quantity, in base currency using manual exchange rates on the consignment, with largest-remainder rounding at 2 dp; landed unit cost is a 4-dp `UnitCost`, not `Money`; stored on the consignment only; valuation and margin use landed ?? purchase via `v_batch_unit_cost` | FR-IMP-5; the flow doc left the basis open (OQ-3); 4 dp would break ADR-054's `Money` if not a separate type |
| **ADR-107** | **Adjustments** need a reason category **and** a note; above `inventory.adjustmentApprovalThreshold` they wait for a different approver holding `Stock.ApproveAdjustment` before any ledger entry is written; counts use the same threshold | FR-IMP-13; a count must not be a way round adjustment approval |
| **ADR-108** | **Counts are blind; variances are frozen at submission; commit posts exactly the reported variance** (not "set to counted") | US-IMP-14 AC1/AC2; sales during counting are real movements |
| **ADR-109** | **Stock locations** are an Inventory root per branch with a fixed kind → permitted-movement table in `Shared`; one quarantine location per branch; five seeded for `MAIN` | FR-IMP-6's four locations plus FR-ORD-16's quarantine; a fixed table is testable exhaustively (OQ-5) |
| **ADR-110** | **Alerts are `StockAlertAggregate` rows** raised by `InventoryWorker` timed passes with dedup keys; expiry thresholds raise only the tightest crossed; no staff message in v1 | FR-IMP-10/12; HO-NTF-001 gives staff no inbox and no provider exists |
| **ADR-111** | **SKUs come from HO-SHP-001 through `ISkuDirectory`** and one adapter; quantities are `numeric(12,3)` in the SKU's stock unit, whole unless the SKU allows fractions | Written in parallel with HO-SHP-001; clinical use needs fractions (ml), retail does not |
| **ADR-112** | **PO receipt progress is derived** from posted receipts; outstanding = ordered − accepted | Rule 1 (receipt must not also edit the PO); a stored counter drifts |
| **ADR-113** | **Consignment documents are metadata + storage key**, never removed (superseded instead) | HO-CAT-001's upload pattern; NFR-LEG-2 retention |
| **ADR-114** | **Document numbers** (`PO-`, `CN-`, `GRN-`, `TRF-`, `ADJ-`, `CNT-`, `RCL-`) come from PostgreSQL sequences, `{prefix}-{yyyy}-{seq:D6}`, not gapless | Human references on paperwork; gaplessness is an invoice requirement (FR-BIL-1), not a stock one |
| **ADR-115** | **Storage-breach quarantine is location-level** (stock transferred to the branch's quarantine location), distinct from batch-level `Quarantined` | A breach affects the stock *there*, not the same batch elsewhere |
| **ADR-116** | **`inventory.v_sellable_stock`** (per SKU per branch: sellable quantity and the FEFO-first expiry) and **`inventory.v_batch_unit_cost`** are the published read contracts for HO-SHP, HO-ORD and HO-RPT | NFR-PRF-2 catalogue filtering needs a join, not a ledger sum per request |

### 9.1 Open questions — raised, not resolved

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | **Are clinical medicines and consumables SKUs in HO-SHP-001's catalogue** (non-storefront items), or does Inventory need its own item master? | Owner + HO-SHP author | HO-SHP-001's SKUs are the single item master, including items never listed on the storefront. If not, EMR/PHM have nothing to deduct — raise before HO-EMR-001 |
| OQ-2 | `.ai/bounded-contexts.md` / namespace conventions need the `Inventory` context | Architect | ADR-097 records it |
| OQ-3 | Landed-cost basis: quantity, value or weight? | Accountant | Accepted quantity |
| OQ-4 | Adjustment approval threshold and approver role | Owner | 5 000.00 base currency; `Administrator` |
| OQ-5 | May food be sold from the clinic dispensary, or medicine dispensed from the shop floor? | Owner | No — §8.3's fixed table |
| OQ-6 | Recall notices: bypass quiet hours (Urgent)? | Owner | **Urgent** — a cat may be fed the product at breakfast |
| OQ-7 | Who may declare, notify and trace a recall (the trace shows customer contact details)? | Owner | `Inventory`, `Administrator` |
| OQ-8 | Appendix D gives Accountants **R** on imports, but US-IMP-05's persona is the accountant | Owner | Accountants may edit consignment costs and allocate landed cost (`Consignments.ManageCosts`) |
| OQ-9 | Over-receipt tolerance against a PO; does a rejected unit stay outstanding? | Procurement | No over-receipt; rejected units stay outstanding |
| OQ-10 | Minimum remaining shelf life to accept a receipt or allocate a sale | Owner | None beyond "not expired" |
| OQ-11 | Should staff be messaged about alerts (email once a provider exists)? | Owner | No — back-office alerts list only |
| OQ-12 | Transfers instant or with an in-transit state? | Owner | Instant |
| OQ-13 | Blind counts? | Owner | Yes |
| OQ-14 | Who provisions object storage for consignment documents (and cat photos)? | Owner / HO-XCT | Metadata only until then; A5 provisional |
| OQ-15 | SRS Q-8: source countries and per-origin licence rules | Procurement | Any ISO-3166 country, no per-origin rule |
| OQ-16 | Should imported food be held for inspection on receipt by default? | Owner | No; per-line `HoldForInspection` option |
| OQ-17 | HO-ADM-001's `inventory.expiryAlertDays` (60) has no consumer here | HO-SHP author | Recommend HO-SHP-001 use it as FR-SHP-8's product-page expiry window |
| OQ-18 | In a trace, list a customer's household cats when the movement recorded no cat? | Owner | Yes, marked "not specifically recorded" |
| OQ-19 | Stock unit and fractional quantities per SKU | HO-SHP author | `each`, whole units, unless HO-SHP-001 provides them |
| OQ-20 | Is a batch usable on its expiry date? | Owner / clinical lead | Yes — expired from the next branch-local day |
| OQ-21 | **`AdjustAsync` is a correction path with an approval threshold, and controlled drugs are exactly where a correction is most sensitive.** Who may approve a pharmacy correction — the Inventory role, or a clinical one? | Owner + clinical lead | The register correction is already Vet-only and witnessed (HO-PHM-001 C7); the stock side reuses `inventory.adjustmentApprovalThreshold` and its Administrator approver. Two approvals for one event is the safe reading, but say so explicitly rather than letting each side assume the other checked |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `StockLocationKind` | `ShopFloor`, `Warehouse`, `ClinicDispensary`, `BoardingStore`, `Quarantine` |
| `StockClass` | `Food`, `Medicine`, `Consumable`, `General` |
| `StorageCondition` | `Ambient`, `CoolDry`, `Refrigerated`, `Frozen` |
| `PurchaseOrderStatus` | `Draft`, `Issued`, `Closed`, `Cancelled` |
| `PurchaseOrderReceiptState` | `NotReceived`, `PartiallyReceived`, `FullyReceived` (derived, never persisted) |
| `ConsignmentStatus` | `Planned`, `InTransit`, `AtCustoms`, `Cleared`, `Cancelled` |
| `CostComponentKind` | `Freight`, `Duty`, `Clearing`, `Insurance`, `Other` |
| `ImportDocumentKind` | `ImportPermit`, `HealthCertificate`, `VeterinaryCertificate`, `CustomsDeclaration`, `BillOfLading`, `CommercialInvoice`, `PackingList`, `CertificateOfOrigin`, `Other` |
| `RejectionReason` | `Damaged`, `Expired`, `ShortShelfLife`, `WrongItem`, `TemperatureBreach`, `FailedInspection`, `Other` |
| `BatchStatus` | `Available`, `Quarantined`, `Recalled`, `WrittenOff` |
| `MovementKind` | the 14 of §8.7 |
| `ReservationEntryKind` | `Reserve`, `Release` |
| `StockReferenceKind` | `GoodsReceipt`, `StockTransfer`, `StockAdjustment`, `StockCount`, `BatchWriteOff`, `StorageBreach`, `CustomerOrder`, `PosSale`, `Prescription`, `Visit`, `Surgery`, `BoardingStay`, `CustomerReturn` — consumers' kinds are **pre-declared** so their handovers need not edit this file |
| `AdjustmentReason` | `Damage`, `Theft`, `Sampling`, `InternalUse`, `Expired`, `RecallDisposal`, `Found`, `Correction` |
| `AdjustmentStatus` | `Posted`, `PendingApproval`, `Rejected`, `Withdrawn` |
| `StockCountScope` | `FullLocation`, `SelectedSkus` |
| `StockCountStatus` | `Open`, `Submitted`, `AwaitingApproval`, `Committed`, `Cancelled` |
| `StockAlertKind` | `ExpiryThreshold`, `Expired`, `LowStock`, `Shortfall`, `StorageBreach`, `LedgerMismatch` |
| `StockAlertStatus` | `Open`, `Acknowledged`, `Resolved` |
| `StorageBreachStatus` | `Open`, `Quarantined`, `Closed` |
| `DocumentKind` | `PurchaseOrder`, `Consignment`, `GoodsReceipt`, `StockTransfer`, `StockAdjustment`, `StockCount`, `BatchRecall` |

HO-NTF-001's `NotificationSubjectKind` gains **`BatchRecall`** (append only).

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`

Consumed, already in the registry: `inventory.defaultReorderPoint` (10), `commerce.currencyCode` (`BDT`).
**Not consumed**: `inventory.expiryAlertDays` (OQ-17). New:

| Key | Group | Type | Default | Bounds | Branch override |
|---|---|---|---|---|---|
| `inventory.expiryAlertFirstDays` | Inventory | Integer | 90 | 0–730 (0 = off) | ❌ |
| `inventory.expiryAlertSecondDays` | Inventory | Integer | 60 | 0–730 | ❌ |
| `inventory.expiryAlertThirdDays` | Inventory | Integer | 30 | 0–730 | ❌ |
| `inventory.adjustmentApprovalThreshold` | Inventory | Decimal | 5000.00 | 0–10 000 000 | ✅ |
| `inventory.consumptionWindowDays` | Inventory | Integer | 90 | 7–365 | ❌ |
| `inventory.reorderCoverDays` | Inventory | Integer | 14 | 0–180 | ✅ |
| `inventory.defaultLeadTimeDays` | Inventory | Integer | 14 | 0–365 | ❌ |

No cross-key rule is needed: thresholds are sorted at use (§8.13). No migration (values are rows).

### 10.3 Other constants

`InventorySeed` (five location ids) · `StockLocationRules` · `MovementKindRules` · `BatchExpiryRules` ·
`BatchTransitionRules` · `ExpiryAlertRules` · `PurchaseOrderProgress` · `InventoryRules`
(`MaxPurchaseOrderLines = 200`, `MaxReceiptLines = 200`, `MaxTransferLines = 100`,
`MaxAdjustmentLines = 50`, `MaxCountLines = 2000`, `MaxDocuments = 50`, `MaxDocumentBytes = 20 MB`,
`AllowedDocumentContentTypes`, `LedgerQueryMaxRangeDays = 366`) · `RecallRules.MaxRecipientsPerRun =
5000` · `InventoryMessages` (shared message constants — the handler and the constraint filter return
the **same** text, HO-ACC-001 §20.6) · `SystemActors.InventoryWorker` (appended to HO-NTF-001's file) ·
the template definition of §8.12 appended to `NotificationTemplateCatalogue`.

### 10.4 Violation codes — append to `BusinessViolationCodes`

| Area | Codes |
|---|---|
| Locations, SKUs, quantities | `StockLocationCodeAlreadyExists` · `StockLocationNotFound` · `StockLocationHasStock` · `QuarantineLocationAlreadyExists` · `MovementNotPermittedAtLocation` · `SkuNotFound` · `SkuInactive` · `InvalidQuantity` · `FractionalQuantityNotAllowed` · `InvalidTemperatureRange` |
| Suppliers, POs | `SupplierCodeAlreadyExists` · `SupplierNotFound` · `SupplierInactive` · `InvalidCountryCode` · `InvalidCurrencyCode` · `PurchaseOrderNotFound` · `InvalidPurchaseOrderTransition` · `PurchaseOrderLinesRequired` · `DuplicatePurchaseOrderSku` · `PurchaseOrderHasReceipts` · `ExpectedDeliveryInPast` · `CloseReasonRequired` |
| Consignments | `ConsignmentNotFound` · `InvalidConsignmentTransition` · `ConsignmentPurchaseOrderMismatch` · `ClearanceDocumentsMissing` · `InvalidHsCode` · `DocumentLimitExceeded` · `InvalidDocumentContentType` · `DocumentTooLarge` · `ExchangeRateMissing` · `InvalidExchangeRate` · `NoReceivedQuantityToAllocate` |
| Receipts, batches | `GoodsReceiptLinesRequired` · **`ExpiryDateRequired`** · **`BatchAlreadyExpired`** · `InvalidBatchDates` · `RejectionReasonRequired` · `LotNumberRequired` · `ReceiptExceedsOutstanding` · `PurchaseOrderLineMismatch` · `UnitCostRequired` · `DuplicateReceiptLine` · `ConsignmentNotCleared` · `BatchNotFound` · `InvalidBatchTransition` |
| Movements | **`InsufficientStock`** · **`BatchExpired`** · `BatchNotUsable` · `AllocationOverrideReasonRequired` · `StockPartyRequired` · `ReservationNotFound` · `ReturnNotTraceable` |
| Stock control | `TransferLocationsMustDiffer` · `AdjustmentReasonRequired` · `AdjustmentNoteRequired` · `InvalidAdjustmentDirection` · `AdjustmentReasonNotApplicable` · `AdjustmentNotPending` · `ApproverMustDiffer` · `CountLocationBusy` · `InvalidCountTransition` · `CountLineOutsideScope` |
| Alerts, recall, breach | `StockAlertNotFound` · `StockAlertNotOpen` · `RecallConfirmationMismatch` · `RecallNotActive` · `RecallStockStillHeld` · `StorageBreachNotFound` · `InvalidStorageBreachTransition` · `NoStockAtLocation` |

`InsufficientStock`, `BatchExpired` and `BatchNotUsable` are raised both by the port (specific
message) and, on a race, by the constraint filter (§12.5) with the `InventoryMessages` text.

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Inventory/`

Namespace `CatEcommerce.Application.Commands.Inventory`. Data only; ids (including every line id and
batch id) minted in the controller; `ValidateCommand = true` everywhere (F3).

| # | Command | Story | Phase |
|---|---|---|---|
| C1 | `CreateStockLocationCommand` · `UpdateStockLocationCommand` · `SetStockLocationActivationCommand` | IMP-06 | A |
| C2 | `SetProductStorageProfileCommand` (`ProductId`, `Condition`, `MinTemperatureC?`, `MaxTemperatureC?`, `HandlingNotes?`) | IMP-16 | A |
| C3 | `CreateSupplierCommand` · `UpdateSupplierCommand` · `SetSupplierActivationCommand` | IMP-01 | B |
| C4 | `CreatePurchaseOrderCommand` · `ReplacePurchaseOrderDraftCommand` · `IssuePurchaseOrderCommand` · `CancelPurchaseOrderCommand` (`Reason`) · `ClosePurchaseOrderCommand` (`Reason`) | IMP-02 | B |
| C5 | `PlanConsignmentCommand` · `UpdateConsignmentDetailsCommand` · `AdvanceConsignmentStatusCommand` (`To`, `On`) · `CancelConsignmentCommand` | IMP-03 | C |
| C6 | `AddConsignmentDocumentCommand` · `SupersedeConsignmentDocumentCommand` | IMP-03 | C |
| C7 | `ReplaceConsignmentCostsCommand` (`ExpectedVersion`, `CostComponents[]`, `ExchangeRates[]`) | IMP-03, 05 | C |
| C8 | `PostGoodsReceiptCommand` (`GoodsReceiptId`, `SupplierId?`, `PurchaseOrderId?`, `ConsignmentId?`, `DeliveryReference?`, `Lines[]` incl. `BatchId`) | IMP-04, 06, 15 | D |
| C9 | `QuarantineBatchCommand` · `ReleaseBatchCommand` · `WriteOffBatchCommand` (`Reason`) | IMP-06, 09 | D |
| C10 | `PostStockTransferCommand` | IMP-13 | F |
| C11 | `RequestStockAdjustmentCommand` · `ApproveStockAdjustmentCommand` · `RejectStockAdjustmentCommand` (`Note`) · `WithdrawStockAdjustmentCommand` | IMP-13 | F |
| C12 | `OpenStockCountCommand` · `RecordCountLinesCommand` · `SubmitStockCountCommand` · `CommitStockCountCommand` · `ApproveStockCountCommand` · `RejectStockCountCommand` · `CancelStockCountCommand` | IMP-14 | F |
| C13 | `AllocateLandedCostCommand` (`ConsignmentId`, `ExpectedVersion`) | IMP-05 | G |
| C14 | `SetReorderPolicyCommand` (`SkuId`, `LocationId`, `ReorderPoint`, `ReorderQuantity?`, `PreferredSupplierId?`) | IMP-12 | H |
| C15 | `RaiseStockAlertCommand` (worker) · `ResolveStockAlertCommand` (worker) · `AcknowledgeStockAlertCommand` (staff, `Note?`) | IMP-10, 12 | H |
| C16 | `RecallBatchCommand` · `NotifyRecallRecipientsCommand` · `WithdrawBatchRecallCommand` · `CloseBatchRecallCommand` | IMP-11 | I |
| C17 | `ReportStorageBreachCommand` · `QuarantineStorageBreachStockCommand` · `CloseStorageBreachCommand` | IMP-16 | J |

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: ids non-empty; text lengths; enum membership; quantities > 0 (decimal, ≤ 3 dp); line counts
within `InventoryRules`; country/currency code *shape* (two/three upper letters); HS-code regex;
document content type and size; `Reason`/`Note` presence where the command requires it. **Expiry,
FEFO, stock sufficiency, approval, SKU flags and transitions are aggregate, port or handler rules.**

### 11.3 Mappers and DTOs

`InventoryMapper`, `ProcurementMapper` (static `ToDomainDto()` extensions). DTOs in `Domain/Models/`:
`SupplierDto`, `PurchaseOrderLineDto`, `ConsignmentDetailsDto`, `ConsignmentDocumentDto`,
`CostComponentDto`, `ExchangeRateDto`, `GoodsReceiptLineDto`, `NewBatchDto`, `StockMovementDraft`,
`StockDemand`, `StockReference`, `StockParty`, `StockReturn`, `StockShortfall`, `StockPlan`,
`StockMovementLine`, `StockResult<T>`, `AdjustmentLineDto`, `CountLineDto`, `BatchRecallDto`,
`LandedCostLineDto`, `SkuStockProfile`. Value objects in `Domain/ValueObjects/`: **`UnitCost`**
(`Amount` decimal 4 dp ≥ 0, `CurrencyCode`), **`StockQuantity`** (decimal 3 dp > 0),
`SupplierContact`.

### 11.4 Command handlers — `CommandHandlers/Inventory/`

The `RegisterCatCommandHandler` shape. Every handler that moves stock builds drafts, calls the ledger
port (which validates and enlists), and **only then** saves its own aggregate once.

| Handler | Specifics |
|---|---|
| **C1** | Code unique per branch (repository pre-check; `ix_stock_location_branch_code` → `400`); second active `Quarantine` in a branch → `QuarantineLocationAlreadyExists`; deactivate reads positions (`StockLocationHasStock`) |
| **C2** | Product exists via `ISkuDirectory` (any SKU of it); create-or-update the 1:1 profile |
| **C3** | Code unique (`ix_supplier_code`) |
| **C4** | SKUs through `ISkuDirectory` (active; snapshot taken); deliver-to location in the branch permitting `Receipt`; `Issue` with branch-local today; `Cancel` reads `EXISTS` posted receipt lines for the PO |
| **C5–C7** | POs loaded to check branch and status; status advance via `ConsignmentStatus` table; `Clear` requires the three document numbers; C7 with `ExpectedVersion` (`409` on stale) |
| **C8 Post receipt** | §8.6: load PO (and prior accepted per line — one grouped query), consignment, SKUs (one `GetManyAsync`), branch today; validate every line collecting **all** violations (property names `Lines[i].ExpiresOn` etc.) before returning; create and enlist batches; `IInventoryLedgerWriter.AppendAsync` the `Receipt` drafts; `SaveAsync(receipt)` |
| **C9** | Transition on the batch; **write-off** enlists one `Wastage` draft per position with `on_hand > 0` (reference `BatchWriteOff`, the batch id) then `UpdateAsync(batch)` |
| **C10** | §8.10; drafts `TransferOut`/`TransferIn`; `SaveAsync(transfer)` |
| **C11** | Request: compute value (`v_batch_unit_cost` via `IStockPositionReader`), threshold from `IBusinessSettings` for the branch; `Posted` → enlist drafts. Approve: caller holds `Stock.ApproveAdjustment` (`ActionRoles` check in the handler as defence in depth) and ≠ requester; enlist drafts; `UpdateAsync` |
| **C12** | Open: `CountLocationBusy` pre-check + filtered unique index. Record lines: `Open` only; SKU/batch within scope (`CountLineOutsideScope`). Submit: book quantities from positions, zero lines for uncounted positions (full scope), value. Commit: `Committed` → enlist `CountGain`/`CountLoss` drafts; above threshold → `AwaitingApproval`, approval as C11 |
| **C13** | §8.11 |
| **C14** | Pair exists as SKU and location; upsert |
| **C15** | Raise: dedup pre-check → success, no write; `23505` on the dedup indexes → success. Resolve: `LowStock` whose condition cleared. Acknowledge: `Open` only |
| **C16** | §8.12. Recall/withdraw/close are single-aggregate updates. Notify: resolver, enqueue per recipient **before** `UpdateAsync(batch)`, `BusinessEvent` audit enlisted |
| **C17** | Report: affected batches from positions at the location (`NoStockAtLocation` when none); enlist a `StorageBreach` alert. Quarantine: transfer drafts from the location to the branch's quarantine location; `UpdateAsync(report)` |

**`EfStockLedger`** (Infrastructure — §8.8) implements both ports: loads candidates through
`IStockPositionReader`, applies the rules, calls `FefoAllocator`, and adds entry aggregates (and, for
overrides, an `AuditEntryAggregate`) to the scoped `CatEcommerceDbContext`. **It never calls
`SaveChangesAsync`.**

### 11.5 Domain events — `Events/Inventory/`

Ids, instants, enums, decimals and booleans only — no reason text, advice, notes or names (U-X1).

`StockLocationCreatedEvent`, `SupplierCreatedEvent`, `PurchaseOrderIssuedEvent`,
`PurchaseOrderCancelledEvent`, `PurchaseOrderClosedEvent`, `ConsignmentStatusChangedEvent`,
`ConsignmentDocumentAddedEvent`, `LandedCostAllocatedEvent`, **`GoodsReceiptPostedEvent`** (`GoodsReceiptId`,
`BranchId`, `BatchIds[]`, `SkuIds[]` — HO-SHP-001 may use it for back-in-stock), `BatchQuarantinedEvent`,
`BatchReleasedEvent`, **`BatchRecalledEvent`** (`BatchId`, `SkuId`, `RecalledOn` — **contract for
HO-ORD-001** to flag unshipped orders, idempotently, without enqueueing), **`BatchRecallWithdrawnEvent`**,
`BatchRecallNotifiedEvent`, `BatchWrittenOffEvent`, `StockTransferPostedEvent`,
`StockAdjustmentRequestedEvent`, `StockAdjustmentDecidedEvent`, `StockCountSubmittedEvent`,
`StockCountCommittedEvent`, `StorageBreachReportedEvent`.

Ledger and reservation entries raise **no** events (nothing subscribes; they are enlisted, not saved
through a repository).

### 11.6 Domain event handlers — `DomainEventHandlers/Inventory/`

**None.** Every side effect of a stock change happens in the command, in the same commit. In
particular there is **no `CustomerMergedEvent` handler** (ADR-104: the ledger is immutable; merges are
resolved at read time) and no handler that enqueues anything.

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

`DbSet`s for every root in §8.1; one configuration per root in `Persistence/Configurations/`, schema
`inventory`, with HO-ACC-001 §12.1's conventions and **ADR-021's three corrections**
(`ValueGeneratedNever()` on root and owned ids, `Tags`/`Language`/`ServiceId` optional, the F15 lines);
enumerations by `Value`; `DateOnly` → `date`; instants → `timestamptz`; `Money` as `amount numeric(12,2)`
+ `currency_code char(3)`; `UnitCost` as `numeric(14,4)` + `char(3)`; quantities `numeric(12,3)`;
exchange rates `numeric(18,6)`; `PurchaseOrderIds` and `AffectedBatchIds` as `uuid[]`.

`stock_position` is mapped **keyless** (`HasNoKey`, `ToTable("stock_position", "inventory", t =>
t.ExcludeFromMigrations())`) — EF can query it and cannot write it; the table is created by raw SQL.
`Sequence` on both entry tables is `ValueGeneratedOnAdd` and never inserted.

| Index / constraint | Kind |
|---|---|
| `stock_location (branch_id, code)` | **UNIQUE** `ix_stock_location_branch_code` |
| `stock_location (branch_id) WHERE kind = Quarantine AND is_active` | **UNIQUE** `ix_stock_location_one_quarantine` |
| `supplier.code` | **UNIQUE** `ix_supplier_code` |
| `purchase_order.number`, `consignment.reference`, `goods_receipt.number`, `stock_transfer.number`, `stock_adjustment.number`, `stock_count.number` | **UNIQUE** each |
| `purchase_order (branch_id, status)`, `(supplier_id)` | lists |
| `purchase_order_line (purchase_order_id, sku_id)` | **UNIQUE** |
| `goods_receipt_line (purchase_order_line_id)` | progress |
| `batch (goods_receipt_id, sku_id, lot_number, expires_on)` | **UNIQUE** `ix_batch_receipt_lot` |
| `batch (sku_id, status, expires_on)`, `(consignment_id)`, `(lot_number)` | FEFO inputs, allocation, lot search |
| `stock_ledger_entry (batch_id, sequence)`, `(sku_id, location_id, occurred_on)`, `(reference_kind, reference_id)`, `(occurred_on)`, `(customer_id)`, `(cat_id)` | trace, consumption, reference lookups, ledger search |
| `stock_reservation_entry (reference_kind, reference_id, reference_line_id)`, `(batch_id)` | remaining reservations |
| `ck_stock_ledger_sign` | `CHECK` — `quantity_delta > 0` for incoming kinds, `< 0` for outgoing, written from `MovementKind` values |
| `ck_stock_position_balance` | `CHECK (on_hand >= 0 AND reserved >= 0 AND reserved <= on_hand)` |
| `stock_position (sku_id)` | availability |
| `stock_count (location_id) WHERE status IN (Open, Submitted, AwaitingApproval)` | **UNIQUE** `ix_stock_count_one_active` |
| `reorder_policy (sku_id, location_id)` | **UNIQUE** |
| `stock_alert.dedup_key` (filtered not null) | **UNIQUE** `ix_stock_alert_dedup` |
| `stock_alert (kind, sku_id, location_id) WHERE status <> Resolved AND kind IN (LowStock, LedgerMismatch)` | **UNIQUE** `ix_stock_alert_open_pair` |
| `stock_alert (branch_id, status, kind)` | alert list |

FKs within the schema: entries → batch, location; lines → their roots; batch → goods receipt. **No FK
into another schema** (branch, SKU, customer, cat ids are references by id — rule 2).

**Raw SQL in migrations** (`migrationBuilder.Sql`), every enumeration value written from the
enumeration in C# (`MovementKind.Sale.Value`), never typed as a literal — U-X2 checks the text:
`stock_position` table and `ck_stock_position_balance`; functions and triggers
`fn_apply_ledger_entry` / `tg_stock_ledger_apply`, `fn_apply_reservation_entry` /
`tg_stock_reservation_apply`, `fn_guard_position_write` / `tg_stock_position_guard`,
`fn_check_batch_usable` / `tg_stock_batch_usable` (both entry tables), `fn_refuse_mutation` /
`tg_stock_ledger_immutable`, `tg_stock_reservation_immutable`; sequences
`po_number_seq`, `consignment_number_seq`, `grn_number_seq`, `transfer_number_seq`,
`adjustment_number_seq`, `count_number_seq`, `recall_number_seq`; views `v_sellable_stock` and
`v_batch_unit_cost`. `fn_check_batch_usable` reads the branch zone from `admin.branch.time_zone_id`
(HO-ADM-001's column — confirm the name when writing the migration).

**Migrations**, each **read before commit** (the SQL is hand-written):
`AddInventoryLocationsAndStorage` (A, seed), `AddInventoryProcurement` (B), `AddInventoryConsignments`
(C), `AddInventoryStockLedger` (D — batch, receipt, ledger, position, triggers), `AddInventoryReservations`
(E — reservation entries, trigger, `v_sellable_stock`), `AddInventoryStockControl` (F),
`AddInventoryLandedCost` (G — lines, `v_batch_unit_cost`), `AddInventoryAlertsAndReorder` (H),
`AddInventoryRecalls` (I — owned recall columns on `batch`), `AddInventoryStorageBreaches` (J). Every
`Down` drops what its `Up` created, triggers before tables.

### 12.2 Infrastructure services and the worker

| Class | Notes |
|---|---|
| `Inventory/EfStockLedger` | §8.8, §11.4 — both ports |
| `Inventory/EfBatchRegistry` | `Enlist` only |
| `Inventory/EfStockPositionReader` | `AsNoTracking`; candidates with batch, location kind and priority; positions per location; unit costs |
| `Inventory/CatalogueSkuDirectory` | §8.2 — the only reader of HO-SHP-001's schema |
| `Inventory/RecallRecipientResolver` | §8.12 — reads `inventory`, `account`, `clinic`, `notify` |
| `Inventory/InventoryClock` | branch zone + `BranchCalendar.LocalDateOf` + `TimeProvider` |
| `Inventory/SequenceDocumentNumberGenerator` | `SELECT nextval(…)` |
| `Notifications/Guards/BatchRecallGuard` | valid while the batch is `Recalled` |

**`InventoryWorker : BackgroundService`** (Api, `Background/`) — the HO-NTF-001 `NotificationWorker`
shape: a scope per unit of work, never throws out of `ExecuteAsync`, disabled in `Testing`,
`InventoryWorkCycle` callable by tests, `SystemActors.InventoryWorker` as the `UserContext`. Passes,
every `Inventory:Worker:IntervalSeconds` (300): (1) **expiry** — per active branch, batches with stock
there, raise per §8.13; (2) **low stock** — pairs below their effective reorder point → raise; open
`LowStock` whose pair recovered → resolve; (3) **reconciliation** — once per 24 h of process uptime.
Each item is one command dispatch. Configuration `Inventory:Worker:Enabled`, `IntervalSeconds`.

### 12.3 Authorization

Appendix D — "Stock & batches": Reception R, Nurse R, Vet R, Caretaker R, **Inventory F**, ShopManager R,
**Admin F**; "Purchase orders / imports": **Inventory F**, ShopManager R, Accountant R, **Admin F**.
Appended to the one `ActionRoles` table:

| Action | Staff roles | Endpoints |
|---|---|---|
| `Stock.View` | Reception, Nurse, Vet, Caretaker, Inventory, ShopManager, Administrator | locations, SKU lookup, stock, batches, ledger, FEFO preview, alerts, storage profiles, transfers/adjustments/counts reads |
| `Stock.Receive` | Inventory, Administrator | post receipt |
| `Stock.Transfer` | Inventory, Administrator | transfers |
| `Stock.Adjust` | Inventory, Administrator | request/withdraw adjustment |
| `Stock.ApproveAdjustment` | Administrator | approve/reject adjustments and counts (OQ-4) |
| `Stock.Count` | Inventory, Administrator | counts |
| `Batches.ManageStatus` | Inventory, Administrator | quarantine, release, write-off |
| `Recalls.Manage` | Inventory, Administrator | recall, trace, notify, withdraw, close (OQ-7) |
| `StockAlerts.Acknowledge` | Inventory, ShopManager, Administrator | acknowledge |
| `StorageBreaches.Report` | Reception, Nurse, Vet, Caretaker, Inventory, ShopManager, Administrator | report |
| `StorageBreaches.Manage` | Inventory, Administrator | quarantine, close |
| `ProductStorage.Manage` | Inventory, Administrator | storage profiles |
| `StockLocations.Manage` | Administrator | location writes |
| `ReorderPolicies.Manage` | Inventory, Administrator | reorder policies |
| `InventoryReports.View` | Inventory, ShopManager, Accountant, Administrator | stock on hand (valuation), near-expiry, reconciliation |
| `Procurement.View` | Inventory, ShopManager, Accountant, Administrator | suppliers, POs, consignments, inspection pack, reorder suggestions |
| `Procurement.Manage` | Inventory, Administrator | supplier, PO, consignment writes and documents |
| `Consignments.ManageCosts` | Inventory, Accountant, Administrator | costs, rates, landed-cost allocation (OQ-8) |

No customer endpoint exists in this slice. Staff endpoints resolve the branch through
`IActiveBranchAccessor`; a location, PO, receipt, adjustment, count or transfer of another branch is
`404` (never leaks existence).

### 12.4 Api — the contract HO-IMP-002 consumes

JSON camelCase; instants ISO-8601 with offset; dates `yyyy-MM-dd`; money as HO-ADM-001 §12.4
(`{ "amount": "1500.00", "currencyCode": "BDT" }`); **unit costs** `{ "amount": "12.3456",
"currencyCode": "USD" }`; **quantities as strings** (`"12.000"` trimmed to `"12"` when whole) with
`stockUnit` beside them. Every list is paged (`pageNumber`, `pageSize` ≤ 100, `totalCount`) and
sortable/filterable as listed (UI-4). Every batch view carries `isExpired`, `daysToExpiry`,
`branchTimeZoneId`. Branch from `X-Branch-Id`.

**Lookups and set-up**

| Method | Route | Action | Command / query → success |
|---|---|---|---|
| `GET` | `/api/staff/inventory/skus?search&barcode` | `Stock.View` | ≤ 20 `SkuStockProfile` + `storage` |
| `GET` | `/api/staff/inventory/locations` | `Stock.View` | the branch's locations: `id`, `code`, `name`, `kind`, `isActive`, `permittedMovements[]` |
| `POST` / `PUT` | `/api/staff/inventory/locations` (· `/{id}`, `/{id}/activation`) | `StockLocations.Manage` | C1 → `201 { locationId }` / `204` |
| `GET` / `PUT` | `/api/staff/inventory/storage-profiles/{productId}` | `Stock.View` / `ProductStorage.Manage` | profile / C2 → `204` |

**Procurement**

| Method | Route | Action | → success |
|---|---|---|---|
| `GET` / `POST` | `/api/staff/procurement/suppliers?search&countryCode&isActive` | `Procurement.View` / `Manage` | paged / C3 → `201 { supplierId }` |
| `GET` / `PUT` | `/api/staff/procurement/suppliers/{id}` · `PUT …/activation` | same | detail / `204` |
| `GET` / `POST` | `/api/staff/procurement/purchase-orders?status&supplierId&receiptState&from&to` | `Procurement.View` / `Manage` | paged summaries (`number`, supplier, status, `receiptState`, `expectedDeliveryOn`, `totalAmount`) / C4 → `201 { purchaseOrderId, number }` |
| `GET` | `/api/staff/procurement/purchase-orders/{id}` | `Procurement.View` | detail: lines with `orderedQuantity`, `acceptedQuantity`, `rejectedQuantity`, **`outstandingQuantity`**, `unitCost`, `lineTotal`; `receiptState`; `receipts[]` (number, date, link); `allowedActions[]` |
| `PUT` | `/api/staff/procurement/purchase-orders/{id}` | `Procurement.Manage` | replace draft → `204` |
| `POST` | `…/purchase-orders/{id}/issue` · `/cancel` · `/close` | `Procurement.Manage` | `204` |
| `GET` | `/api/staff/procurement/reorder-suggestions?locationId&supplierId` | `Procurement.View` | rows: SKU, location, `usable`, `reorderPoint`, `onOrder`, `avgDailyConsumption`, `leadTimeDays`, **`suggestedQuantity`**, preferred supplier |
| `GET` / `POST` | `/api/staff/procurement/consignments?status&originCountryCode&from&to` | `Procurement.View` / `Manage` | paged / C5 → `201 { consignmentId, reference }` |
| `GET` / `PUT` | `/api/staff/procurement/consignments/{id}` | `Procurement.View` / `Manage` | detail incl. `version`, costs, rates, documents, `landedCost` (`allocatedOn`, `isStale`, lines) / `204` |
| `POST` | `…/consignments/{id}/status` · `/cancel` | `Procurement.Manage` | `204` |
| `POST` | `…/consignments/{id}/documents` · `…/documents/{documentId}/supersede` | `Procurement.Manage` | `201 { documentId }` / `204` |
| `PUT` | `…/consignments/{id}/costs` | `Consignments.ManageCosts` | C7 → `204`; `409` stale version |
| `POST` | `…/consignments/{id}/landed-cost` | `Consignments.ManageCosts` | C13 → `200 { lines[], totalCostsBase, allocatedOn }` |
| `GET` | `…/consignments/{id}/inspection-pack` | `Procurement.View` | **everything together** (US-IMP-03 AC2): details, HS codes, permit/certificate/declaration numbers, POs, documents (incl. superseded), batches received with lot and expiry |

**Stock**

| Method | Route | Action | → success |
|---|---|---|---|
| `GET` / `POST` | `/api/staff/inventory/goods-receipts?from&to&supplierId&purchaseOrderId` | `Stock.View` / `Stock.Receive` | paged / C8 → `201 { goodsReceiptId, number, batchIds[] }`; `400` with **every** line violation |
| `GET` | `/api/staff/inventory/goods-receipts/{id}` | `Stock.View` | detail with lines, batches, storage |
| `GET` | `/api/staff/inventory/stock?search&locationId&class&belowReorderPoint` | `Stock.View` | per SKU: `onHand`, `reserved`, `usable`, `sellable`, `expiredQuantity`, `heldQuantity` (quarantined/recalled/quarantine location), `nextExpiresOn` |
| `GET` | `/api/staff/inventory/stock/{skuId}` | `Stock.View` | **batch × location matrix** (US-IMP-06): per batch `lotNumber`, `expiresOn`, `isExpired`, `daysToExpiry`, `status`, per-location `onHand`/`reserved`; `storage` |
| `GET` | `/api/staff/inventory/batches?search&skuId&status&expiringWithinDays&consignmentId` | `Stock.View` | paged |
| `GET` | `/api/staff/inventory/batches/{batchId}` | `Stock.View` | detail: provenance, costs (`unitCostBase`, `costBasis` — omitted for roles without `InventoryReports.View`), positions, `recall`, `statusHistory`, **`allowedActions[]`** from `BatchTransitionRules` |
| `POST` | `…/batches/{batchId}/quarantine` · `/release` · `/write-off` | `Batches.ManageStatus` | `204` |
| `GET` | `/api/staff/inventory/fefo-preview?skuId&quantity&purpose&locationId` | `Stock.View` | the `StockPlan` or `{ shortfall }` — US-IMP-07 AC1 observable |
| `GET` | `/api/staff/inventory/ledger?skuId&batchId&locationId&kind&referenceKind&referenceId&from&to` | `Stock.View` | paged, `from`/`to` **required**, ≤ 366 days; rows: `sequence`, `occurredOn`, kind, delta, unit, location, batch/lot, reference label, `fefoOverridden`, `overrideReason`, performer name. **No customer or cat fields** (those are in the recall trace only) |
| `GET` | `…/ledger/export.csv?…same` | `InventoryReports.View` | streamed CSV, CSV-injection guard (HO-ADM-001 ADR-051's rule), ≤ 50 000 rows else `413` |
| `GET` / `POST` | `/api/staff/inventory/transfers` (· `/{id}`) | `Stock.View` / `Stock.Transfer` | paged / C10 → `201` |
| `GET` / `POST` | `/api/staff/inventory/adjustments?status` (· `/{id}`) | `Stock.View` / `Stock.Adjust` | paged / C11 → `201 { adjustmentId, status, valueBase, requiresApproval }` |
| `POST` | `…/adjustments/{id}/approve` · `/reject` | `Stock.ApproveAdjustment` | `204`; `409 InsufficientStock` if stock moved |
| `POST` | `…/adjustments/{id}/withdraw` | `Stock.Adjust` | `204` |
| `GET` / `POST` | `/api/staff/inventory/counts?status&locationId` (· `/{id}`) | `Stock.View` / `Stock.Count` | paged / C12 open → `201`. `GET /{id}` while `Open` returns lines **without** book quantities |
| `PUT` | `…/counts/{id}/lines` | `Stock.Count` | upsert lines → `204` |
| `POST` | `…/counts/{id}/submit` · `/commit` · `/cancel` | `Stock.Count` | `204`; commit → `200 { status }` (`Committed` or `AwaitingApproval`) |
| `GET` | `…/counts/{id}/variance` | `Stock.View` | after submit: lines with book, counted, variance, value; totals |
| `POST` | `…/counts/{id}/approve` · `/reject` | `Stock.ApproveAdjustment` | `204` |
| `GET` / `PUT` | `/api/staff/inventory/reorder-policies?locationId` · `PUT …/{skuId}/{locationId}` | `Stock.View` / `ReorderPolicies.Manage` | list incl. effective point and whether defaulted / `204` |
| `GET` | `/api/staff/inventory/alerts?status&kind&locationId` · `/alerts/summary` | `Stock.View` | paged / `{ open: { byKind } }` for the nav badge |
| `POST` | `…/alerts/{id}/acknowledge` | `StockAlerts.Acknowledge` | `204` |
| `GET` | `/api/staff/inventory/reports/near-expiry?withinDays&includeExpired` (+ `.csv`) | `InventoryReports.View` | §8.13 |
| `GET` | `/api/staff/inventory/reports/stock-on-hand?locationId&class` (+ `.csv`) | `InventoryReports.View` | per SKU × batch × location with unit cost, basis and value (SRS §8.1) |
| `GET` | `/api/staff/inventory/reports/reconciliation` | `InventoryReports.View` | mismatches (expected empty) |
| `POST` | `/api/staff/inventory/batches/{batchId}/recall` | `Recalls.Manage` | C16 → `201 { recallNumber }` |
| `GET` | `/api/staff/inventory/recalls?status` · `/recalls/{batchId}` | `Recalls.Manage` | list / detail incl. notified counts, stock held |
| `GET` | `/api/staff/inventory/recalls/{batchId}/trace` (+ `.csv`) | `Recalls.Manage` | §8.12 sections; customer `name`, `mobileNumber`, `email`, cat `name`, current owner; **CSV audited** (`BusinessEvent`, `RecallTraceExported`) |
| `POST` | `…/recalls/{batchId}/notify` | `Recalls.Manage` | `200 { requested, alreadyNotified, remaining }` |
| `POST` | `…/recalls/{batchId}/withdraw` · `/close` | `Recalls.Manage` | `204` |
| `GET` / `POST` | `/api/staff/inventory/storage-breaches` (· `/{id}`) | `Stock.View` / `StorageBreaches.Report` | paged / C17 → `201 { breachId, affectedBatchCount }` |
| `POST` | `…/storage-breaches/{id}/quarantine` · `/close` | `StorageBreaches.Manage` | `204` |

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes (R1–R8).

### 12.5 Filters

Extend HO-ACC-001's `UniqueConstraintExceptionFilter` (**amendment to HO-ACC-001, §1**) with
**`23514` by constraint name**: `ck_stock_position_balance` → `409 InsufficientStock`;
`tg_stock_batch_expired` → `409 BatchExpired`; `tg_stock_batch_not_usable` → `409 BatchNotUsable` —
each with the `InventoryMessages` text. `23505`: `ix_stock_location_branch_code`, `ix_supplier_code`,
`ix_batch_receipt_lot` → the matching `400`; `ix_stock_count_one_active` → `400 CountLocationBusy`;
`ix_stock_location_one_quarantine` → `400 QuarantineLocationAlreadyExists`; `ix_stock_alert_dedup` and
`ix_stock_alert_open_pair` are swallowed by C15 (they never reach the filter). The immutability
triggers are not mapped — reaching them is a bug, and a `500` is correct. Unknown constraints rethrow.

### 12.6 Read side — `src/Read/`

Queries in `Queries/Inventory/`, handlers in `QueryHandlers/Inventory/`, ViewModels flat. Explicit
projections, `AsNoTracking()`. Expiry flags computed with `BatchExpiryRules` against
`IInventoryClock` after materialising — never a second expiry rule in LINQ.

- `GetSellableStockQuery(branchId, skuIds[])` → per SKU `sellableQuantity`, `nextExpiresOn` — over
  `v_sellable_stock`. **The contract HO-SHP-001 and HO-ORD-001 consume** (ADR-116).
- `GetBatchUnitCostQuery(batchIds[])` over `v_batch_unit_cost` — HO-RPT-001's input.
- `GetPurchaseOrderQuery` (progress via `PurchaseOrderProgress` over a grouped receipt-line query),
  `GetReorderSuggestionsQuery` (calculator over consumption and on-order reads).
- `GetStockSummaryQuery`, `GetSkuStockMatrixQuery`, `GetBatchQuery`, `GetLedgerQuery` (range required),
  `GetFefoPreviewQuery` (through `IStockLedger.PlanAsync` — a read), `GetStockCountSheetQuery`
  (blind while `Open`), `GetStockCountVarianceQuery`.
- `GetNearExpiryReportQuery`, `GetStockOnHandReportQuery`, `GetStockReconciliationQuery`,
  `GetRecallTraceQuery`, `GetInspectionPackQuery`, alert and breach lists.
- Worker queries: `GetExpiryAlertCandidatesQuery`, `GetLowStockCandidatesQuery`,
  `GetRecoveredLowStockAlertsQuery`.

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler in §11–§12.6 (count them — I-A2);
`EfStockLedger` as **one Scoped instance serving both ports**
(`AddScoped<EfStockLedger>()` + two forwarding registrations); `IBatchRegistry`, `ISkuDirectory`,
`IStockPositionReader`, `IRecallRecipientResolver`, `IInventoryClock`, `IDocumentNumberGenerator`
(Scoped); the pure domain services (Scoped, per `.ai/command-handler-pattern.md` §5);
`BatchRecallGuard` into HO-NTF-001's `SubjectGuardResolver`; the template via the catalogue;
`InventoryWorkCycle` (Scoped); **`AddHostedService<InventoryWorker>()`**. `ValidateScopes` /
`ValidateOnBuild` stay on. `appsettings.json` gains `Inventory:Worker` only.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Enums/NotificationSubjectKind.cs                        ← BatchRecall only
src/Shared/CatEcommerce.Shared/Common/Constants/{InventorySeed,InventoryRules,RecallRules,InventoryMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Helpers/{StockLocationRules,MovementKindRules,BatchExpiryRules,BatchTransitionRules,ExpiryAlertRules,PurchaseOrderProgress}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs  ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                          ← the seven keys of §10.2 only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs              ← inventory.batch-recall only
src/Shared/CatEcommerce.Shared/Common/Constants/SystemActors.cs                               ← InventoryWorker only
src/Domain/CatEcommerce.Domain/Aggregates/Inventory/**
src/Domain/CatEcommerce.Domain/Entities/{PurchaseOrderLine,ConsignmentHsCode,ConsignmentCostComponent,ConsignmentExchangeRate,ConsignmentDocument,LandedCostLine,GoodsReceiptLine,BatchRecall,BatchStatusChange,ConsignmentStatusChange,StockTransferLine,StockAdjustmentLine,StockCountLine}.cs
src/Domain/CatEcommerce.Domain/ValueObjects/{UnitCost,StockQuantity,SupplierContact}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{FefoAllocator,LandedCostAllocator,ReorderSuggestionCalculator,IStockLedger,IInventoryLedgerWriter,IBatchRegistry,ISkuDirectory,IStockPositionReader,IRecallRecipientResolver,IInventoryClock,IDocumentNumberGenerator}.cs
src/Domain/CatEcommerce.Domain/DomainServices/BranchCalendar.cs                               ← LocalDateOf only, and only if HO-ADM-001 did not build it
src/Domain/CatEcommerce.Domain/Models/**                                                      ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Inventory/**
src/Application/CatEcommerce.Application/Commands/Inventory/**
src/Application/CatEcommerce.Application/CommandHandlers/Inventory/**
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DataMappers/{InventoryMapper,ProcurementMapper}.cs
src/Read/CatEcommerce.Read/Queries/Inventory/**
src/Read/CatEcommerce.Read/QueryHandlers/Inventory/**
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs          ← DbSets and the keyless position only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{one per new root, StockPositionRowConfiguration}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                      ← the ten migrations of §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Inventory/**
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Guards/BatchRecallGuard.cs
src/Infrastructure/CatEcommerce.Infrastructure/Audit/**                                        ← the change-capture exclusion list only (two entry types)
src/Api/CatEcommerce.Api/Controllers/{StaffInventoryLocations,StaffStorageProfiles,StaffSkuLookup,ProcurementSuppliers,ProcurementPurchaseOrders,ProcurementConsignments,ProcurementReorder,StaffGoodsReceipts,StaffStock,StaffBatches,StaffLedger,StaffTransfers,StaffAdjustments,StaffCounts,StaffReorderPolicies,StaffStockAlerts,StaffInventoryReports,StaffRecalls,StaffStorageBreaches}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Inventory/**
src/Api/CatEcommerce.Api/Background/{InventoryWorker,InventoryWorkCycle}.cs
src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs                          ← §12.5 mappings only
src/Api/CatEcommerce.Api/appsettings.json                                                    ← Inventory:Worker only
src/Api/CatEcommerce.Api/Program.cs                                                          ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-IMP-001.md
docs/gates/HO-IMP-001-*.md
.ai/decisions/adr.md                                                                          ← ADR-097…116, append only
```

The edits to HO-ACC-001's filter and change-capture list, HO-ADM-001's registry and calendar, and
HO-NTF-001's catalogue, subject kind and `SystemActors` are confined to the lines named above; each
owning handover gets an Amended row from the coordinator (§1).

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-2 is a separate task) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs — including this file once work starts (Amended rows only) |
| `web/**` | HO-IMP-002 |
| Every HO-SHP-001 file (products, SKUs, catalogue) | Its area; SKUs are read through `CatalogueSkuDirectory` only |
| Every file of HO-ACC-001, HO-ADM-001, HO-CAT-001, HO-NTF-001, HO-APT-001 beyond §13's lines | Their areas. No Administration, Account, Cat or Clinic aggregate is edited |
| Any order, cart, invoice, payment, prescription, visit, surgery or boarding type | HO-ORD, HO-BIL, HO-PHM, HO-EMR, HO-SRG, HO-BRD consume §8.8 |
| A controlled-drug register | HO-SRG-001 / HO-PHM-001 |
| Any object-storage client, upload endpoint or file write | §4.3 |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |

---

## 15. Unit test cases

`tests/CatEcommerce.Domain.Tests` and `Application.Tests`.

| ID | Case | Expected |
|---|---|---|
| **A — foundation** | | |
| U-A1 | `StockLocationRules` over every kind × `MovementKind` | Exactly §8.3's table |
| U-A2 | `StockQuantity`: 0, −1, 1.2345; fractional on a whole-only SKU | `InvalidQuantity`; `InvalidQuantity`; 3-dp refusal; `FractionalQuantityNotAllowed` |
| U-A3 | `UnitCost`: 4 dp kept; negative; `double` anywhere in the type | Kept; refused; reflection finds none |
| U-A4 | Storage profile min 8, max 2; −50 | `InvalidTemperatureRange` twice |
| U-A5 | `BranchCalendar.LocalDateOf("Asia/Dhaka", 2026-10-11T18:30Z)` | `2026-10-12` |
| **B — procurement** | | |
| U-B1 | Supplier with country `XX`, currency `usd`, lead time 400 | `InvalidCountryCode`, `InvalidCurrencyCode`, bound violation |
| U-B2 | PO transitions over every `(status, action)` | Only §8.5's arrows |
| U-B3 | Issue with no lines; expected delivery yesterday | `PurchaseOrderLinesRequired`; `ExpectedDeliveryInPast` |
| U-B4 | Two lines for one SKU | `DuplicatePurchaseOrderSku` |
| U-B5 | `PurchaseOrderProgress`: ordered 100, accepted 60, rejected 10 | outstanding 40; `PartiallyReceived` — **US-IMP-02 AC2** |
| U-B6 | Cancel with receipts; close without reason | `PurchaseOrderHasReceipts`; `CloseReasonRequired` |
| **C — consignment** | | |
| U-C1 | Consignment transitions; clear without permit number | Only §8.5's arrows; `ClearanceDocumentsMissing` |
| U-C2 | HS codes `2309.10`, `230910`, `23.09`, `ABCD` | valid, valid, invalid, invalid |
| U-C3 | 51st document; `text/html`; 21 MB | `DocumentLimitExceeded`; `InvalidDocumentContentType`; `DocumentTooLarge` |
| U-C4 | Supersede a document | Still present, `SupersededOn` set; no remove method exists (reflection) |
| U-C5 | `ReplaceCostsAndRates` after an allocation | `CostsChangedSinceAllocation = true` |
| **D — receipt, batch, ledger** | | |
| U-D1 | Food SKU, accepted 10, no expiry | `ExpiryDateRequired` on `Lines[0].ExpiresOn` — **US-IMP-04 AC2** |
| U-D2 | General SKU, no expiry | Accepted |
| U-D3 | Expiry yesterday (branch-local); manufactured after expiry; manufactured tomorrow | `BatchAlreadyExpired`; `InvalidBatchDates` ×2 |
| U-D4 | Rejected 2 without reason; rejected-only line | `RejectionReasonRequired`; no batch, no ledger draft |
| U-D5 | Accepted beyond outstanding | `ReceiptExceedsOutstanding` |
| U-D6 | Receipt with three invalid lines | All three violations returned together |
| U-D7 | `BatchTransitionRules` over every pair | Only §8.6.1's arrows; `WithdrawRecall` → `Quarantined` |
| U-D8 | `MovementKindRules.Sign` for all 14 kinds; a draft with the wrong sign | Table; refused |
| U-D9 | Reflection over `StockLedgerEntryAggregate` and `StockReservationEntryAggregate` | No public setter, no public method but the factory |
| **E — FEFO, reservations, expiry** | | |
| U-E1 | Batches expiring 2027-03, 2026-12, none, 2026-12 (received later) | Order 2026-12 (earlier receipt), 2026-12, 2027-03, undated — **US-IMP-07 AC1** |
| U-E2 | `BatchExpiryRules` at expiry date 2026-10-12, Dhaka today 2026-10-12 / 2026-10-13 | Not expired / expired |
| U-E3 | FEFO skips expired, quarantined, recalled and zero-free candidates | Not in the plan |
| U-E4 | Demand 15 over batches with 10 and 8 free | 10 + 5, split |
| U-E5 | Demand 30 over 18 free | Shortfall 12 → `InsufficientStock` mentioning 18 |
| U-E6 | Explicit plan equal to FEFO; different without reason; different with reason | Accepted unflagged; `AllocationOverrideReasonRequired`; flagged + audit entry enlisted — **US-IMP-07 AC2** |
| U-E7 | Explicit plan naming an **expired** batch, with a reason | `BatchExpired` — the reason changes nothing — **US-IMP-09** |
| U-E8 | Purpose `Sale` at a dispensary; `Dispense` at the shop floor | `MovementNotPermittedAtLocation` |
| U-E9 | Reflection over `IStockLedger`, `IInventoryLedgerWriter` and every parameter type | No member matching `/force|bypass|ignore|allowExpired|override(?!Reason)/i` |
| U-E10 | Party rules: Dispense without cat; Sale without customer (order); Sale without customer (`PosSale`) | `StockPartyRequired`; `StockPartyRequired`; allowed |
| U-E11 | `ConsumeReservationAsync` for a batch recalled after reserving | `BatchNotUsable` |
| **F — stock control** | | |
| U-F1 | Adjustment without reason; without note; `Found` negative; `Theft` positive | `AdjustmentReasonRequired`; `AdjustmentNoteRequired`; `InvalidAdjustmentDirection` ×2 |
| U-F2 | Value 4 999.99 / 5 000.01 at threshold 5 000.00 | `Posted` with drafts / `PendingApproval` with none |
| U-F3 | Approve by the requester | `ApproverMustDiffer` |
| U-F4 | Reason `Expired` on a live batch; `RecallDisposal` on an available batch | `AdjustmentReasonNotApplicable` ×2 |
| U-F5 | Count sheet while `Open` | No book quantity in the ViewModel |
| U-F6 | Full-location submit with an uncounted position of 7 | Line counted 0, variance −7 |
| U-F7 | Commit | Exactly the submitted variances as `CountGain`/`CountLoss` drafts referencing the count |
| U-F8 | Transfer to the same location | `TransferLocationsMustDiffer` |
| **G — landed cost** | | |
| U-G1 | Costs 1 000.00 BDT over quantities 3, 3, 3 | 333.34, 333.33, 333.33 — sum exactly 1 000.00 |
| U-G2 | Purchase USD 2.5000 × 100 at rate 118.500000, costs 1 500.00 | Purchase base 29 625.00; landed unit 311.2500 |
| U-G3 | EUR cost with no EUR rate | `ExchangeRateMissing` naming EUR |
| U-G4 | No batches received | `NoReceivedQuantityToAllocate` |
| **H — alerts, reorder** | | |
| U-H1 | Thresholds 90/60/30; batch days-left 45, then 29, then −1 | Raise 60; then 30; then `Expired`; never 90 — **US-IMP-10 AC1** |
| U-H2 | Settings 90, 0, 30 | Thresholds [90, 30] |
| U-H3 | avgDaily 2.0, lead 10, cover 14, reorder point 10, usable 5, on order 20 | ceil(48) + 10 − 25 = 33 — **US-IMP-12 AC2** |
| U-H4 | No consumption, reorder point 10, position 3 | 7 |
| U-H5 | Policy `ReorderQuantity` 50 | 50 |
| U-H6 | Usable excludes expired, quarantined and reserved | As stated — **US-IMP-12 AC1** input |
| **I — recall** | | |
| U-I1 | Recall from `WrittenOff`; lot confirmation `LOT-1` vs `lot-2` | `InvalidBatchTransition`; `RecallConfirmationMismatch` |
| U-I2 | Recipient set: purchaser A (merged into B), cat owned now by C, anonymised D | {B, C}; D excluded |
| U-I3 | `BatchRecallGuard`: recalled; withdrawn | Valid; invalid |
| U-I4 | Template definition | Passes HO-NTF-001 `TemplateRules`; `Urgent`; Transactional |
| **J — breach, reconciliation** | | |
| U-J1 | Breach at a location with positions 5, 0, 3 | Two affected batches |
| U-J2 | Reconciliation comparator with one drifted position | Reported |
| **X — cross-cutting** | | |
| U-X1 | Reflection over `Events/Inventory/**` | No string properties |
| U-X2 | Migration SQL text | Uses `MovementKind.*.Value`, `StockLocationKind.*.Value`, `BatchStatus.Available.Value`; the expiry predicate matches `BatchExpiryRules` (`>` not `>=`) |
| U-X3 | `InventoryWorker` with a throwing pass | Logged; next pass runs |
| U-X4 | CSV writer with `=HYPERLINK(…)`, `+1`, `@x` | Prefixed with `'` |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1). Worker off; tests call
`InventoryWorkCycle`, advancing `FakeTimeProvider`. **Port contract tests** resolve `IStockLedger` from
a fresh DI scope of the factory, call it, then commit through the scoped `CatEcommerceDbContext` —
exactly what a consumer handler does.

### 16.1 Additional shared-database rules

1. **Each test creates its own branch** (HO-ADM-001) and its own stock locations (C1), and mints its own
   SKUs through HO-SHP-001's endpoints. Stock, FEFO, availability and alert assertions are scoped to
   that branch and those SKUs.
2. Codes, lot numbers and document numbers are minted per test (unique keys).
3. Triggers and constraints are real; a test never disables them. Tests that need an already-expired
   batch set `expires_on` in the past **through Npgsql in test setup** (the batch table is not
   immutable) — the only direct write any test makes.
4. Never assert on global counts, sequences or emptiness (reconciliation asserts "no mismatch for this
   test's positions").

| ID | Case | Expected |
|---|---|---|
| **A** | | |
| I-A1 | Migrations on an empty database | Schema `inventory`; every table, index and view of §12.1; the seven triggers present (`pg_trigger`); `ck_stock_position_balance` present; five seeded locations for `MAIN` |
| I-A2 | Host starts, scope validation on | Every registration resolves; one `EfStockLedger` per scope behind both ports |
| I-A3 | Create a location in another branch; read it with this branch's `X-Branch-Id` | `404` |
| I-A4 | Second active quarantine location in a branch | `400 QuarantineLocationAlreadyExists` |
| I-A5 | SKU lookup by barcode and by text | The SKU with `storage` after C2 — **US-IMP-16 AC1** (lookup half) |
| **B** | | |
| I-B1 | Create a supplier with name, country, contact, currency, lead time, terms | `201`; `GET` returns all six — **US-IMP-01** |
| I-B2 | Duplicate supplier code (different case) | `400 SupplierCodeAlreadyExists` |
| I-B3 | Create, issue a PO with two SKUs, quantities, unit costs, currency, expected delivery | `Issued`, number `PO-…` — **US-IMP-02 AC1** |
| I-B4 | Receive 60 of 100 on line 1 (phase D) | Detail: accepted 60, outstanding 40, `PartiallyReceived` — **US-IMP-02 AC2** |
| I-B5 | Accountant `POST` PO; accountant `GET` PO | `403`; `200` |
| I-B6 | Cancel an issued PO with no receipts; close a partially received PO with a reason | `204`; `204`, outstanding still shown |
| **C** | | |
| I-C1 | Plan a consignment with every FR-IMP-3 field, costs and rates | `201`; `GET` returns all |
| I-C2 | Add four documents; `GET` inspection pack | Details, POs, all documents and received batches in one response — **US-IMP-03 AC2** |
| I-C3 | Supersede a document | Both remain in the pack; the old marked superseded |
| I-C4 | Receipt against an `AtCustoms` consignment | `400 ConsignmentNotCleared` |
| I-C5 | Clear without a permit number | `400 ClearanceDocumentsMissing` |
| **D** | | |
| I-D1 | Post a receipt: two accepted lines, one partly rejected | `201`; two batches; two `Receipt` ledger rows with reference `GRN-…` and the user; positions equal accepted; rejected quantity and reason on the line, not in stock — **US-IMP-04 AC1** |
| I-D2 | Food SKU without expiry | `400 ExpiryDateRequired`; nothing written — **US-IMP-04 AC2** |
| I-D3 | Receipt with an already-expired expiry | `400 BatchAlreadyExpired` |
| I-D4 | Over-receipt | `400 ReceiptExceedsOutstanding` |
| I-D5 | `UPDATE`, `DELETE`, `TRUNCATE` on `stock_ledger_entry` and `stock_reservation_entry` through Npgsql | Each raises — **US-IMP-15 AC3** |
| I-D6 | `UPDATE inventory.stock_position SET on_hand = 999` through Npgsql | Raises (guard trigger) — **US-IMP-15 AC2** |
| I-D7 | After I-D1 and later movements | Every position equals Σ its ledger entries; reconciliation empty for these positions — **US-IMP-15 AC2** |
| I-D8 | `GET /stock/{skuId}` with the SKU in two batches at three locations | Batch × location matrix — **US-IMP-06** |
| I-D9 | Ledger search without a range; with 400 days | `400` both; with a valid range, rows carry SKU, batch, delta, location, reference and user — **US-IMP-15 AC1** |
| I-D10 | Receive with `HoldForInspection` | Batch `Quarantined`; not in sellable |
| **E** | | |
| I-E1 | Three batches of one SKU; `GET /fefo-preview?quantity=15&purpose=Sale` | The soonest-expiring first, split as needed — **US-IMP-07 AC1** |
| I-E2 | Same, with the soonest batch expired (Npgsql setup) and the next quarantined | Both skipped |
| I-E3 | Contract: `ReserveAsync` with an explicit non-FEFO batch, no reason; with reason; commit | `AllocationOverrideReasonRequired`; committed reservation entry `fefoOverridden` with the reason **and** a `BusinessEvent` audit row `FefoOverridden` naming the user — **US-IMP-07 AC2** |
| I-E4 | **Contract: 20 parallel scopes each `ReserveAsync` 1 of the last free unit and commit** | Exactly **one** commit; 19 `DbUpdateException` → `23514 ck_stock_position_balance`; `reserved = 1` — **US-IMP-08 AC2** |
| I-E5 | Same race through `DeductAsync` (POS sale) | Exactly one; `on_hand = 0` |
| I-E6 | One SKU with: an expired batch, a quarantined batch, stock in the quarantine location, 3 reserved of 10 in a good batch | `GetSellableStockQuery` and `v_sellable_stock` → **7**; `nextExpiresOn` = the good batch's — **US-IMP-08 AC1**, US-SHP-07 input |
| I-E7 | Expired batch (Npgsql setup): `DeductAsync` for `Sale`, `Dispense`, `ClinicalUse`, `BoardingUse`, and `ReserveAsync`, each with an explicit batch and an override reason | Every one `BatchExpired`; nothing enlisted — **US-IMP-09** |
| I-E8 | **Defence in depth**: insert a `Sale` ledger row for the expired batch directly through Npgsql | Raises `23514 tg_stock_batch_expired`; position unchanged |
| I-E9 | Reserve 3, consume the reservation; reserve 2, release | `Sale −3`, reserved back to 0, on hand −3; release restores free stock |
| I-E10 | 10 on hand, 8 reserved by order A; unreserved `DeductAsync` of 3 | `InsufficientStock` (2 free) |
| I-E11 | A consumer commit that hits `ck_stock_position_balance` through HTTP (a transfer racing a POS deduction) | `409 InsufficientStock` with the shared message |
| **F** | | |
| I-F1 | Transfer 5 from warehouse to shop floor | `TransferOut −5` and `TransferIn +5` with one reference; positions moved — **US-IMP-13 AC1** |
| I-F2 | Adjustment below threshold; above; approve by a second administrator | `Posted` + entries; `PendingApproval`, no entries; entries on approval — **US-IMP-13 AC2** |
| I-F3 | Adjustment with no reason; no note; approval by requester | `400` ×3 (`AdjustmentReasonRequired`, `AdjustmentNoteRequired`, `ApproverMustDiffer`) |
| I-F4 | Stock sold between request and approval so the loss no longer fits | Approve → `409 InsufficientStock` |
| I-F5 | Count: open, record lines (blind read), submit | Variance report returned; **no ledger rows** yet — **US-IMP-14 AC1** |
| I-F6 | Commit | `CountGain`/`CountLoss` rows with `referenceKind StockCount` and the count id — **US-IMP-14 AC2** |
| I-F7 | Sale between submit and commit | Commit posts the reported variance; final stock = counted − sold |
| I-F8 | Second count at a busy location; count above threshold | `400 CountLocationBusy`; `AwaitingApproval` |
| I-F9 | Reception `POST` adjustment | `403` |
| **G** | | |
| I-G1 | Consignment with two POs (USD, EUR), costs, rates; receive three batches; allocate | Landed unit cost per batch; allocated totals sum to costs — **US-IMP-05 AC1** |
| I-G2 | `v_batch_unit_cost` before and after allocation | `costBasis` `Purchase` → `Landed`; stock-on-hand valuation changes accordingly — **US-IMP-05 AC2** |
| I-G3 | Edit costs after allocation; receive another batch | `landedCost.isStale = true`; re-allocation clears it |
| I-G4 | Accountant allocates; Reception allocates | `200`; `403` |
| **H** | | |
| I-H1 | Batches with 91, 89, 59, 29 days left; run the cycle twice | Alerts: none, 90, 60, 30; second run adds none — **US-IMP-10 AC1** |
| I-H2 | Near-expiry report within 90 days | Rows with `daysRemaining` and `valueAtRisk` = on hand × unit cost — **US-IMP-10 AC2**; CSV downloads |
| I-H3 | Usable falls below reorder point 10 (via deductions); cycle; receive stock; cycle | `LowStock` open; then `Resolved` — **US-IMP-12 AC1** |
| I-H4 | Reorder suggestions with 90 days of consumption and a supplier lead time | Suggested quantity equals the formula — **US-IMP-12 AC2** |
| I-H5 | Contract: `RecordShortfall` then consumer commit | `Shortfall` alert present — US-CLN-15 AC2 contract |
| I-H6 | Acknowledge an alert | `Acknowledged` with user |
| **I** | | |
| I-I1 | Batch with stock at warehouse, shop floor and another branch's warehouse; recall | `Recalled`; sellable for the SKU drops at once in both branches; `fefo-preview` skips it — **US-IMP-11 AC1** |
| I-I2 | Contract: before the recall, commit a `Sale` (customer A, order), a `Dispense` (cat X of B), a `BoardingUse` (cat Y of C), a POS sale with no customer, and a reservation for order D. Merge A into A′. Trace | Lists A′ (as survivor), X with B, Y with C, "1 unidentified", order D under **unshipped — dispatch blocked** — **US-IMP-11 AC2** |
| I-I3 | Contract: `ConsumeReservationAsync` for order D; direct Npgsql insert of the same `Sale` | `BatchNotUsable`; trigger raises `tg_stock_batch_not_usable` — **US-IMP-11 AC4** (contract; dispatch UI in HO-ORD-001) |
| I-I4 | Notify; run the notification delivery pass | Requests for A′, B, C, D's customer, one each, with the batch as subject; each in the HO-NTF-001 log; in-app copies delivered; batch `notifiedRecipientCount` 4 — **US-IMP-11 AC3** |
| I-I5 | Notify again | `requested 0, alreadyNotified 4` |
| I-I6 | Withdraw a second recall before its notices are sent | Batch `Quarantined`; pending notices `Cancelled SubjectNoLongerApplies` |
| I-I7 | **Recall and a reservation on the same batch concurrently** (20 repetitions) | Every run: either the reservation committed first (and appears in the trace) or it failed `tg_stock_batch_not_usable` — never a reservation committed after the recall |
| I-I8 | Nurse recalls; nurse reads the trace | `403`; `403` |
| I-I9 | Close with stock held; write off (`RecallDisposal`); close | `400 RecallStockStillHeld`; `204`; `204` |
| **J** | | |
| I-J1 | Report a breach at the boarding store holding two batches; quarantine; close | Two batches flagged + `StorageBreach` alert; stock transferred to the quarantine location with the breach as reference; excluded from usable; closed — **US-IMP-16 AC2** |
| I-J2 | Storage profile on receipt and stock reads | `storage` present — **US-IMP-16 AC1** |
| **X** | | |
| I-X1 | Log capture across recall, trace and notify cases at `Information`+ | No customer names, mobile numbers, emails or advice text |
| I-X2 | Every §12.4 route with a role lacking its action | `403 ActionNotPermitted` (sample of one per action) |
| I-X3 | Trace CSV | Downloaded, injection-guarded, and a `RecallTraceExported` audit row |

---

## 17. Implementation order and verification

| Phase | Stories | Delivers |
|---|---|---|
| **A** | US-IMP-16 (1), foundation | Enumerations, settings, constants, rules tables; locations + seed; storage profiles; `ISkuDirectory` + adapter (**stop if HO-SHP-001 is absent — §20.1**); SKU lookup. Migration `AddInventoryLocationsAndStorage` |
| **B** | US-IMP-01, 02 | Suppliers; POs (outstanding completes in D). `AddInventoryProcurement` |
| **C** | US-IMP-03 | Consignments, documents, costs, rates, inspection pack. `AddInventoryConsignments` |
| **D** | US-IMP-04, 06, 15 | **Step 1: verify FI-1…FI-5 in a rolled-back transaction and record them — §20.3 on any failure.** Batches, receipts, ledger, position, the five triggers, `EfStockLedger` (writer half), batch status commands, ledger read. `AddInventoryStockLedger` |
| **E** | US-IMP-07, 08, 09 | `FefoAllocator`, `IStockLedger` (consumer half), reservation entries, usability trigger, `v_sellable_stock`, FEFO preview. `AddInventoryReservations` |
| **F** | US-IMP-13, 14 | Transfers, adjustments with approval, counts. `AddInventoryStockControl` |
| **G** | US-IMP-05 | `LandedCostAllocator`, allocation, `v_batch_unit_cost`, valuation. `AddInventoryLandedCost` |
| **H** | US-IMP-10, 12 | Alerts, `InventoryWorker`, reorder policies and suggestions, near-expiry and stock-on-hand reports. `AddInventoryAlertsAndReorder` |
| **I** | US-IMP-11 | Recall, trace, notify, withdraw, close; template; guard. `AddInventoryRecalls` |
| **J** | US-IMP-16 (2), close-out | Storage breaches; reconciliation; CSV exports. `AddInventoryStorageBreaches` |

Each phase: code → its unit tests green → its migration generated **and read** (the trigger SQL is
hand-written) → its integration tests green. Record each phase's commit in the test sheet.

```bash
dotnet restore CatEcommerce.sln
dotnet build CatEcommerce.sln            # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add <PhaseMigration> --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output-dir Persistence/Migrations
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
dotnet run --project src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.IntegrationTests
```

---

## 18. Acceptance criteria

Each row is checked against its evidence. **A row with no evidence is not met.**

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | IMP-01 | Suppliers record name, country, contact, currency, lead time and payment terms | U-B1, I-B1, I-B2 |
| A2 | IMP-02 AC1 | A PO with SKUs, quantities, unit costs, currency and expected delivery is issued and trackable | U-B2, U-B3, U-B4, I-B3, I-B6 |
| A3 | IMP-02 AC2 | Partial receipt leaves the outstanding quantity visible | U-B5, I-B4 |
| A4 | IMP-03 AC1 | A consignment records origin, shipment reference, HS codes, permit, certificate, customs declaration, cost components and documents — *provisional on SRS Q-8* | U-C1, U-C2, U-C3, I-C1, I-C5 |
| A5 | IMP-03 AC2 | All documents for a consignment are retrievable together — *metadata only until object storage exists (OQ-14)* | U-C4, I-C2, I-C3 |
| A6 | IMP-04 AC1 | Receipt records per SKU lot, manufacture date, expiry, quantity received and rejected with reason | U-D4, U-D6, I-D1 |
| A7 | IMP-04 AC2 | Food or medicine without expiry is refused | U-D1, U-D2, I-D2 |
| A8 | IMP-05 AC1 | Consignment costs allocate across received quantity to a landed unit cost per batch | U-G1, U-G2, U-G3, U-G4, I-G1, I-G3 |
| A9 | IMP-05 AC2 | Where landed cost exists, valuation and the margin input use it instead of purchase price — *the margin report itself is HO-RPT-001's* | I-G2 |
| A10 | IMP-06 | Stock reported per batch per location across the four FR-IMP-6 locations (plus quarantine) | U-A1, I-D8, I-J1 |
| A11 | IMP-07 AC1 | The soonest-expiring non-expired batch is allocated first | U-E1, U-E3, U-E4, I-E1, I-E2 |
| A12 | IMP-07 AC2 | An override needs a reason and is audited | U-E6, I-E3 |
| A13 | IMP-08 AC1 | Expired, quarantined, damaged and reserved quantities never count toward availability | U-H6, I-D10, I-E6, I-E10 |
| A14 | IMP-08 AC2 | Of two concurrent claims on the last unit exactly one succeeds; the other is told it is unavailable | **I-E4**, I-E5, I-E11 |
| A15 | IMP-09 | Sale, dispensing and boarding use of an expired batch are refused with no override anywhere | U-E2, U-E7, **U-E9**, I-E7, **I-E8** |
| A16 | IMP-10 AC1 | Alerts at 90, 60 and 30 days (configurable) | U-H1, U-H2, I-H1 |
| A17 | IMP-10 AC2 | Near-expiry report with days remaining and value at risk | I-H2 |
| A18 | IMP-11 AC1 | Recall makes all remaining stock unusable immediately across every location | U-I1, I-I1, I-I7 |
| A19 | IMP-11 AC2 | Every order, customer and cat that received the batch is listed, including clinic dispensing and boarding feeding — *at contract level until HO-ORD/PHM/BRD stamp real movements* | U-I2, I-I2 |
| A20 | IMP-11 AC3 | All affected are notified in one action and the notifications are logged — *in-app only outside Development until a provider exists (HO-NTF-001 OQ-1)* | U-I3, U-I4, I-I4, I-I5, I-I6 |
| A21 | IMP-11 AC4 | An unshipped order containing the batch cannot be dispatched — *contract here; dispatch in HO-ORD-001* | U-E11, I-I3 |
| A22 | IMP-12 AC1 | A SKU below its reorder point at a location raises an alert | U-H6, I-H3 |
| A23 | IMP-12 AC2 | A reorder quantity is suggested from consumption history and supplier lead time | U-H3, U-H4, U-H5, I-H4 |
| A24 | IMP-13 AC1 | Transfers write ledger entries at both ends | U-F8, I-F1 |
| A25 | IMP-13 AC2 | Adjustments need a reason; above the configured value they need approval | U-F1…U-F4, I-F2, I-F3, I-F4, I-F9 |
| A26 | IMP-14 AC1 | A variance report is produced before any adjustment is committed | U-F5, U-F6, I-F5 |
| A27 | IMP-14 AC2 | Committing writes adjustment entries referencing the count | U-F7, I-F6, I-F7, I-F8 |
| A28 | IMP-15 AC1 | Every movement records SKU, batch, delta, location, reference and user | U-D8, I-D1, I-D9 |
| A29 | IMP-15 AC2 | Displayed stock is derived from the ledger, never an editable figure | I-D6, I-D7 |
| A30 | IMP-15 AC3 | Ledger entries cannot be edited or deleted | U-D9, I-D5 |
| A31 | IMP-16 AC1 | Storage requirements are recorded and shown on receiving and stock reads | U-A4, I-A5, I-J2 |
| A32 | IMP-16 AC2 | A reported breach flags affected batches and can quarantine them pending a decision | U-J1, I-J1 |
| A33 | US-CLN-15 AC2 *(contract)* | A consumer can record a shortfall for reordering | I-H5 |
| A34 | — | Host starts with scope validation; every registration resolves; no customer data in logs | I-A2, I-X1 |

**Deferred, named in §4.2**: order reservation/dispatch/returns and POS (HO-ORD-001); clinical,
pharmacy, surgery and boarding deductions (HO-EMR/PHM/SRG/BRD); product-page availability
(HO-SHP-001); margin and valuation reports (HO-RPT-001).

---

## 19. Required gates

In order. Each writes `docs/gates/HO-IMP-001-<GATE>.md` **before** reporting.

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Inventory` context (ADR-097, OQ-2); the four rule-1 exceptions (ADR-102, 103, 105 and the recall notify) — all one commit, all appending; `IStockLedger` as the only movement path and the enlist-without-save shape (ADR-103); the pure `FefoAllocator`, `LandedCostAllocator`, `ReorderSuggestionCalculator`; **the deliberate replacement of `IBatchConsumptionQuery` and the read-time honouring of `CustomerMergedEvent`** (ADR-104); no event handlers; no enqueue from an event handler; `CatalogueSkuDirectory` the only reader of HO-SHP-001; §13/§14 scope incl. the named lines in other areas |
| 2 | SECURITY | `security-reviewer` | **The database controls**: `ck_stock_position_balance` (I-E4), the position guard (I-D6), immutability (I-D5), the usability trigger (I-E8, I-I7); **no expiry bypass anywhere** (U-E9); recall trace exposure restricted to `Recalls.Manage`, CSV audited; no customer data in ledger reads, events or logs; branch scoping returns `404`; CSV injection guard; approver separation (ADR-107) |
| 3 | CODE_REVIEW | `code-reviewer` | `.ai/` conventions; ADR-021's corrections on every configuration; enumeration values (not literals) in hand-written SQL (U-X2); one expiry predicate; money never `double`; `StockResult` failures never thrown; worker never throws |
| 4 | TEST | `test-engineer` | `TC-HO-IMP-001.md` from §15/§16 first; FI-1…FI-5 recorded; §16.1's per-test branch, locations and SKUs; I-E4, I-E5 and I-I7 with **real parallelism**, not sequential calls |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A4/A5 provisional (Q-8, object storage), A19–A21 at contract level, A20's provider caveat — recorded, not passed silently |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is not merged** — HO-ACC-001, HO-ADM-001, HO-NTF-001, or **HO-SHP-001** (or
HO-SHP-001 lacks SKU id, product id, code, name, barcode or active status). Do not stub SKUs, do not
create a product table here, do not read another area's tables outside `CatalogueSkuDirectory`.

**20.2 HO-SHP-001 turns out not to cover clinical items** (OQ-1). Finish the slice for retail items and
report — do not invent an Inventory item master.

**20.3 Any of FI-1 … FI-5 is false.** Stop. Do not replace the trigger-and-`CHECK` control with an
application lock, an advisory lock, a retry loop or a stored balance column on an aggregate. The design
depends on those facts; if they fail, it is an architecture decision.

**20.4 A concurrency test passes only with retries, delays or disabled parallelism.** The control is
wrong; fix it, never the test.

**20.5 Something seems to need an expired batch to be usable** — a test, a consumer, "clinical
emergency". There is no override (BR-3). Report it.

**20.6 Something seems to need an editable stock number, or an `UPDATE` of a ledger row.** Corrections
are new entries (adjustment `Correction`). Report it.

**20.7 Do not enqueue from an event handler** (HO-NTF-001 ADR-057), and do not add a
`CustomerMergedEvent` handler that rewrites ledger rows (ADR-104).

**20.8 Do not build an upload endpoint or object-storage client** for consignment documents (§4.3).

**20.9 Do not build orders, POS, dispensing, clinical-use or boarding screens or aggregates.** Their
handovers call §8.8.

**20.10 An open question that blocks a behaviour is a stop.** Build to §9.1's defaults; if one proves
wrong, an Amended row changes it — never a silent code change.

**20.11 A requirement conflict is a stop, not an edit** — SRS and backlog are forbidden scope.

**20.12 Do not downgrade `Platform.Infrastructure.Core` below 1.0.34.**
