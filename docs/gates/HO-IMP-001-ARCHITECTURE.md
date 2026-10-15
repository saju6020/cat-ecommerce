GATE: ARCHITECTURE
HANDOVER: HO-IMP-001
COMMITS: 5732fa28, ac348b77, 5f150d44, e2f8d817, ced99652, bece2ba4, 8fb822c6, 8b6903d2, ce9bdb3a, e279ead5, f16b60ef, 9e2bc239, 1e399e17, 2de46058, f365340a, 772967dc (branch ho/IMP-001, `git diff 50f1ab1..HEAD`, 253 files)
DATE: 2026-10-07
STATUS: FAIL

This is the first pass. Owner rule (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings go to docs/backlog/IMP.md.

Out of scope for this gate, by plan: swapping HO-SHP-001's interim availability seam to `v_sellable_stock` is a separate follow-up slice and is not judged here.

## What was reviewed

- **Handover:** docs/handover/HO-IMP-001.md in full.
- **Architecture docs:** `.ai/` architecture, cqrs, bounded-contexts, namespace-conventions, di-registration-guide, host-composition, command-handler-pattern, read-side-pattern and persistence-ef-core, plus ADR-341.
- **Cross-area contracts:** docs/handover/CONTRACTS.md.
- **Diff:** every read handler, every command handler, `InventoryModule`, the worker, the filter, the EF configurations, the shared-file edits and the ADR diff.
- **Not done:** no build, no tests, no database. Anything that depends on runtime behaviour is marked for TEST.

## Clean (no finding)

- **No superseded architecture.** None of these appear: a broker, `IDispatcher`, `IBusMessageDispatcher`, `IEventPublisher`, `QueuePublishOnly`, `202`, rejection events, `[BsonIgnoreExtraElements]`, `CommandServices`, a second host, a `.csproj` edit, or an edit to web/, docs/handover or other §14 paths.
- **Layer boundaries.**
  - Domain references Shared only.
  - Application references Domain and Shared only.
  - Read does not reference Application.
- **Controllers.** All 17 inject only `IInMemoryDispatcher` (and the caller and branch accessors). None holds a `DbContext`, repository, aggregate or port. Every command sets `ValidateCommand = true`. No action returns `202`.
- **Write side.**
  - Each of the 48 handlers saves exactly one aggregate, with `InMemoryPublishOnly`. The one exception, the audit entry, uses `DoNotPublish`, following ACC precedent.
  - Every stock movement goes through `IInventoryLedgerWriter` or `IStockLedger`, enlists, and then saves the one aggregate.
  - `EfStockLedger` never calls `SaveChangesAsync`.
  - There are no domain event handlers (§11.6) and no enqueue from an event handler.
  - Aggregate setters are private.
- **Registration.**
  - All 48 command handlers have an `IValidator<T>`, and all 41 query handlers are registered.
  - One Scoped `EfStockLedger` is forwarded to both ports. This replaces the base branch's `FakeStockLedger` registration.
  - `BatchRecallGuard` is registered as `INotificationSubjectGuard`, the same as the other areas.
  - `Program.cs` is untouched. The ADR-341 module is the §12.7 seam, and `ValidateScopes` and `ValidateOnBuild` stay on.
- **Persistence.**
  - All 15 roots go through `ConfigureInventoryRoot`, which sets `xmin`, `ValueGeneratedNever` and ignores `Events`/`Metadata`.
  - Children use `OwnsMany`, and there is no `HasMany`.
  - `stock_position` is keyless and `ExcludeFromMigrations`. The views are mapped with `ToView`.
  - `timestamptz` throughout.
  - The snapshot keeps all 62 earlier tables and adds 28 inventory tables.
- **SKU reads.** `CatalogueSkuDirectory` is the only reader of HO-SHP-001's schema (ADR-111).
- **IStockLedger contract (§8.8, ADR-103, CONTRACTS.md #1–#3).**
  - `IStockLedger.cs` and `Models/Stock*.cs` / `BatchBalance.cs` are **unchanged** from the contracts pass.
  - All nine methods have exactly the §8.8 signatures and the `StockResult<T>` shape.
  - SHP, PHM, EMR phase F and ORD will consume what CONTRACTS.md published.
- **The Pharmacy contract gap (CONTRACTS D-5) is closed.** `StockReferenceKind.ControlledDrugCorrection = 14` is appended.
- **C-ALL duties.**
  - **C-ALL-1:** `RecordClassification.Inventory.cs` classifies all 27 migrated tables plus the raw-SQL `stock_position`, matched against the migrations.
  - **C-ALL-2:** `ErasureExemptions.Inventory.cs` exempts the area with a reason: ids only, merges resolved at read time (ADR-104).
  - **C-ALL-3:** `InventoryRetentionReviewSource` covers POs and receipts. It is registered, uses projection queries only, and only flags records.
  - **C-ALL-4:** no remove methods.
  - **C-ALL-5:** not applicable; no clinical audit types.
- **Shared-file edits are append-only and confined to §13's lines:**
  - `SystemActors.InventoryWorker` (`…0ff3`, unique);
  - `NotificationSubjectKind.BatchRecall = 5`;
  - `StockReferenceKind.ControlledDrugCorrection = 14`;
  - `NotificationTemplateCatalogue` `inventory.batch-recall`;
  - `AuditCaptureExclusions` (the two entry roots, ADR-098);
  - the filter's §12.5 maps.
- **Worker.**
  - It sits in Api/Background with the same shape as Notification and Shop.
  - Hosted service plus Scoped `InventoryWorkCycle` plus Singleton `InventoryWorkerState`.
  - It creates a scope per poll and a scope per dispatched command.
  - It never throws out of `ExecuteAsync`, and it runs as `SystemActors.InventoryWorker`.
- **ADRs.**
  - The `.ai/decisions/adr.md` diff is 203 insertions and 0 deletions, appended at the end of the file.
  - ADR-097…116 Decision and Reason match §9 **word for word**: 20 of 20, compared by script.

## Developer-declared deviations — judgement

1. **`InventoryAlertAggregate` — ACCEPTED.** Shop already owns `Domain.Aggregates.Product.StockAlertAggregate` and `Enums/StockAlertStatus`, so the rename is justified. See LOW-2 for the unrecorded enum rename.
2. **RecordShortfall's synchronous PK lookup — ACCEPTED.** The published port is `void` (contract), so it cannot await. See LOW-3.
3. **Low stock excludes Quarantine-kind locations — ACCEPTED.** This follows §8.13's definition of usable stock and `StockLocationRules`, under which quarantine never holds usable stock.
4. **Breach quarantine moves only free stock — ACCEPTED architecturally.** Moving reserved units would violate `ck_stock_position_balance` at the source (ADR-099/100). The safety consequence needs an owner decision; see MEDIUM-8.
5. **`IInventoryAlertEnlister` and `RecordRecallTraceExportCommand` — ACCEPTED.**
   - The enlister has the same shape as `IBatchRegistry`, and §8.13 and §11.4 C17 specify an enlisted alert.
   - The export command follows the ADM and ACC audited-export precedent.
   - See LOW-1 and LOW-4.
6. **Worker cycle takes an optional branch id — ACCEPTED** (LOW-5).
7. **Expiry test offsets — ACCEPTED.** These are test data only, and the reason (branch-local versus UTC date) is sound. Recorded for TEST.
8. **FI-1 "equivalent shape" — ACCEPTED architecturally, but needs ratification** (MEDIUM-2).
9. **`CountLocationBusy` returns 400 — ACCEPTED.** §12.5 itself specifies `400 CountLocationBusy`, and the handler pre-check returns it. The race path is not mapped; see MEDIUM-1.

## FINDINGS

### HIGH

- [HIGH] **The read side materialises aggregates.** This breaks ADR-012 and `.ai/read-side-pattern.md` §4 rule 1 ("Project, never rehydrate"). It is the same class of finding that failed CAT, NTF, SHP, EMR and BIL.
  - **Whole aggregate rows paged or loaded with no `Select`:**
    - `src/Read/CatEcommerce.Read/QueryHandlers/Inventory/AlertsAndReportsQueryHandlers.cs:206` — `GetStockAlertsQueryHandler` runs `alerts...Skip().Take().ToListAsync()` and builds the ViewModel from `InventoryAlertAggregate` instances.
    - `src/Read/CatEcommerce.Read/QueryHandlers/Inventory/StorageBreachQueryHandlers.cs:37` — `GetStorageBreachQueryHandler` loads the whole `StorageBreachReportAggregate` with `.FirstOrDefaultAsync()`.
  - **`ToDictionaryAsync(keySelector, elementSelector)` on aggregate DbSets with no `Select` first.** EF Core's overload takes `Func<>`, not `Expression<>`. So the whole entity is materialised, together with its auto-included owned collections, and the selector runs in memory. Sites:
    - `StockQueryHandlers.cs:39` — `SupplierAggregate` + Contact;
    - `StockQueryHandlers.cs:40` — `PurchaseOrderAggregate` + every owned Line, up to 200 per PO, to read a number;
    - `StockQueryHandlers.cs:80` — `StockLocationAggregate`;
    - `StockQueryHandlers.cs:82` — `BatchAggregate` + StatusHistory + CurrentRecall, to read a status name;
    - `StockQueryHandlers.cs:374` — `StockLocationAggregate`;
    - `StockQueryHandlers.cs:376` — `StaffAccounts`, another context's aggregate;
    - `AlertsAndReportsQueryHandlers.cs:214`, `:251`, `:278`;
    - `ConsignmentQueryHandlers.cs:57`;
    - `ProcurementQueryHandlers.cs:94`;
    - `SellableAndFefoQueryHandlers.cs:50`;
    - `StockControlQueryHandlers.cs:28` — `BatchLabels.LocationCodesAsync`, shared by six handlers;
    - `StorageBreachQueryHandlers.cs:23`.
  - **Fix:** project first. For example, `.Select(l => new { l.Id, l.Code }).ToDictionaryAsync(x => x.Id, x => x.Code)`, and project the alert and breach reads before `Skip/Take` or `FirstOrDefault`.
  - **What is already clean:** every other Inventory read (about 30 handlers, `EfStockPositionReader`, `RecallRecipientResolver`) projects correctly. Keyless view and position entities (`BatchUnitCosts`, `StockPositions`) are not aggregates and are not part of this finding.

### MEDIUM

- [MEDIUM] **1. Race mappings from §12.5 and §11.4 C15 are incomplete.** In each case below, the sequential pre-check is correct and only the race path is missing. TEST should cover the races.
  - **Two §12.5 indexes are unmapped.** `src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs:73-77` does not map `ix_supplier_code` (→ 400 `SupplierCodeAlreadyExists`) or `ix_stock_count_one_active` (→ 400 `CountLocationBusy`), so a concurrent duplicate gets a 500.
  - **C15 does not swallow the dedup 23505.** `RaiseStockAlertCommandHandler` (`AlertAndReorderCommandHandlers.cs:75-90`) only pre-checks, so a dedup-index race surfaces as an exception that the worker logs at Error.
  - **Index names differ from §12.1.** They are `ix_stock_alert_dedup_key` and `ix_stock_alert_one_live_per_pair`, not `ix_stock_alert_dedup` and `ix_stock_alert_open_pair`. The rename is not in the build amendments, and the filter comment at `:71` still cites the spec names.
- [MEDIUM] **2. FI-1 was false as written, which §20.3 makes a stop.** The build substituted a two-statement apply body instead: insert a zero row `ON CONFLICT DO NOTHING`, then `UPDATE` (`src/Infrastructure/CatEcommerce.Infrastructure/Inventory/InventoryLedgerSql.cs:63-80`), and recorded it as a build amendment.
  - **Why it is not HIGH:** the substitute keeps exactly ADR-099's control — trigger, `CHECK`, and the row lock, with no application or advisory lock. It is verified in `InventoryDatabaseFactsTests.FI1_*`.
  - **What is owed:** it should be ratified by the architect or owner as the §20.3 decision, not left as an in-build note.
  - **For TEST:** run I-E4, I-E5 and I-I7 with real parallelism against the real tables.
- [MEDIUM] **3. Scope: 16 files fall outside §13's named list** (listed under SCOPE CHECK).
  - None is in §14 forbidden scope. All are same-area support or test files.
  - This follows the HO-SHP-001 precedent, where unrecorded same-area files were MEDIUM under the owner rule.
  - The coordinator should add them to §13 through an Amended row.
- [MEDIUM] **4. `GetBatchUnitCostQuery` (§12.6, HO-RPT-001's published input) was not built.** The view `v_batch_unit_cost` (ADR-116) and `IStockPositionReader.GetUnitCostsAsync` exist, but no query exposes them to another area.
- [MEDIUM] **5. Some list reads are paged in memory or not paged at all (UI-4).**
  - **Paged in memory:**
    - `RecallQueryHandlers.cs:33-50` loads every recall, then pages.
    - `StockQueryHandlers.cs:127` materialises every position of the branch, then pages.
    - `AlertsAndReportsQueryHandlers.cs:248-257` builds the reorder-policy pairs in memory.
  - **Not paged:**
    - reorder suggestions;
    - near-expiry report (§4.1 says "Paged report endpoint");
    - stock-on-hand report.
- [MEDIUM] **6. The recall notify run does not enlist a `BusinessEvent` audit row** (§8.12 and §11.4 C16), in `RecallCommandHandlers.cs:84-116`. Only change capture on the batch records it. For CODE_REVIEW and ACCEPTANCE.
- [MEDIUM] **7. `InventoryWorker` is not disabled in the Testing environment** (§12.2).
  - Only `tests/CatEcommerce.IntegrationTests/InventoryApiFactory.cs:41` sets `Inventory:Worker:Enabled=false`.
  - Every other area's test host therefore runs one expiry, low-stock and reconciliation pass at start-up, across all branches of the shared database.
  - This matches the ShopWorker precedent.
  - The fix (`appsettings.Testing.json`) is outside §13 and needs the coordinator. For TEST.
- [MEDIUM] **8. Owner question (from deviation 4).** A breach moves only free stock to quarantine. Reserved units of a batch exposed to a storage breach stay at the breached location, still `Available`, and dispatchable by HO-ORD-001. Should a breach also flag or block those reservations? Raise as a new OQ.

### LOW

- [LOW] **1. The trace CSV action makes two dispatches.** `src/Api/CatEcommerce.Api/Controllers/StaffRecallsController.cs:72-82` sends a query and then a command, against cqrs.md §5 rule 1. Not raised higher because it follows the approved audited-export precedent (`AdminAuditEntriesController.cs:96`, `CustomerProfileController.cs:201`), and the export is refused if the audit write fails.
- [LOW] **2. The `StockAlertStatus` → `InventoryAlertStatus` rename is not in the build amendments.** ADR-110 and §8.13/§10.1 still name `StockAlertAggregate` and `StockAlertStatus`. The amendment covers only the aggregate.
- [LOW] **3. `RecordShortfall` writes ambiguous alerts for unknown locations.** At `src/Infrastructure/CatEcommerce.Infrastructure/Inventory/EfStockLedger.Consumer.cs:183-193`, an unknown location produces an alert with `BranchId = Guid.Empty`, and `ReferenceKind` is null while `ReferenceId` is set. For CODE_REVIEW.
- [LOW] **4. §8.1's list of four rule-1 exceptions is incomplete.** It does not name the breach report's enlisted `StorageBreach` alert or `RecordShortfall`'s enlisted alert. Both are specified (§8.13, §11.4 C17, §8.8) and both create only new rows. The handover text should list them.
- [LOW] **5. `InventoryWorkCycle.RunAsync(ct, onlyBranchId)` is a test-only parameter on a production class.** The reconciliation pass ignores it.
- [LOW] **6. Alert kinds are passed as string literals** at `src/Api/CatEcommerce.Api/Background/InventoryWorkCycle.cs:94`, `:115` and `:129` (`"LedgerMismatch"`, `"Expired"`, `"LowStock"`). Prefer the `StockAlertKind` members.
- [LOW] **7. `UniqueConstraintExceptionFilter.cs:92` adds `product_storage_profile` → 409**, which §12.5 does not list. Harmless, but beyond "§12.5 mappings only".
- [LOW] **8. Recall trace issues one query per customer for household cats** (`RecallQueryHandlers.cs:126-127`, N+1). For CODE_REVIEW.
- [LOW] **9. File groupings differ from §13's names.**
  - Validators, entities and configurations are grouped into multi-type files.
  - `StaffAlertsAndReorderController` replaces the three named controllers `StaffReorderPolicies`, `StaffStockAlerts` and `ProcurementReorder`.
  - This is naming only.
- [LOW] **10. Migrations were authored in the area branch.** ADR-341 point 5 reserves migrations for the integrator, but §13 explicitly allows these ten, consistent with the SHP precedent. The integrator should confirm migration ordering against other in-flight branches.

## SCOPE CHECK

- files in allowed scope: 237/253. This counts §13 paths plus their ADR-341 partial and module equivalents: `InventoryModule.cs` for `Program.cs`; `CatEcommerceDbContext.Inventory.cs`; `Actions`/`ActionRoles`/`BusinessViolationCodes`/`SettingDefinitions.Inventory.cs`; the CONTRACTS.md C-ALL files; and `Auditing/` for §13's "Audit/**".
- files outside allowed scope (MEDIUM-3; none in §14):
  - `src/Api/CatEcommerce.Api/Background/InventoryWorkerState.cs`
  - `src/Api/CatEcommerce.Api/Controllers/StaffAlertsAndReorderController.cs` (name not in §13's list)
  - `src/Domain/CatEcommerce.Domain/DomainServices/IInventoryAlertEnlister.cs`
  - `src/Domain/CatEcommerce.Domain/DomainServices/StockDemandPlanner.cs`
  - `src/Domain/CatEcommerce.Domain/DomainServices/StockUsability.cs`
  - `src/Infrastructure/CatEcommerce.Infrastructure/Configuration/InventoryOptions.cs`
  - `src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Views/BatchUnitCostEntry.cs`
  - `src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Views/SellableStockEntry.cs`
  - `src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Views/StockPositionEntry.cs`
  - `src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/SellableStockEntryConfiguration.cs`
  - `src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/BatchUnitCostEntryConfiguration.cs`
  - `src/Shared/CatEcommerce.Shared/Common/Helpers/InventoryCsv.cs`
  - `src/Shared/CatEcommerce.Shared/Common/Constants/SettingKeys.Inventory.cs`
  - `src/Infrastructure/CatEcommerce.Infrastructure/Fakes/Inventory/FakeStockLedger.cs` (doc comment only)
  - `tests/CatEcommerce.Infrastructure.Tests/Auditing/AuditCaptureExclusionsTests.cs`
  - `tests/CatEcommerce.Infrastructure.Tests/Persistence/InventorySqlAndEventTests.cs`

## To pass

Fix HIGH-1: project every read listed there before materialising. Then send a combined re-review of the fix range.
