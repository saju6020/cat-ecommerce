GATE: ARCHITECTURE
HANDOVER: HO-PHM-001
COMMITS: 3d57854c 8f87c2dd 9c323e5b 745d944b 234e5680 38235c13 4ce733da 46e084cb 508ccc10 931df290 8f532b07 15b1e65e 9f9167af 0634368e (branch ho/PHM-001, `git diff master...HEAD`, merge base 9eb87445, 189 files)
DATE: 2026-10-09
STATUS: FAIL

This is the first pass of the ARCHITECTURE gate. Under the owner's gate policy (2026-09-27), only BLOCKER and HIGH findings fail the gate. MEDIUM and LOW
findings are appended to docs/backlog/PHM.md (rows 14-23).

What this pass reviewed:
- `git diff master...HEAD`: every non-generated source file, the five migrations' `Up`/`Down` and hand-written SQL (`ControlledDrugRegisterSql`), and the
  model snapshot by table set.
- HO-PHM-001 in full, including the "Amended (build)" row. Each of its ten deviations was judged on its merits; none was treated as pre-approved.
- `.ai/`: architecture, cqrs, bounded-contexts, namespace-conventions, di-registration-guide, command-handler-pattern, read-side-pattern,
  persistence-ef-core, domain-events-and-outbox, enumeration-pattern and host-composition.
- In `.ai/decisions/adr.md`: ADR-010…023, ADR-092, ADR-111, ADR-341 and the new ADR-137…154.
- From HO-IMP-001: §1 Amended and the port tables. From docs/handover/CONTRACTS.md: §1 and §5.
- Earlier gates' rulings (EMR-001, IMP-001, FIL-001), so severities stay consistent with them.

Nothing was built or run. Every runtime claim is marked for TEST.

VERIFIED (no finding)
- **No superseded architecture.** None of these appear: `IDispatcher`, `IBusMessageDispatcher`, `IEventPublisher`, MassTransit/RabbitMQ, Mongo/Bson,
  `QueuePublishOnly`, `202`, rejection events, `IReadRepository`, a projection handler, `[Authorize]`, a CommandServices namespace or a second host.
- **Dispatch.** Controllers inject `IInMemoryDispatcher` only, and every command is stamped `ValidateCommand = true`. Every `SaveAsync`/`UpdateAsync` passes
  `InMemoryPublishOnly`. The three audit-row saves use `DoNotPublish`, which follows ACC precedent.
- **Registration.** All 21 commands have a handler and an `IValidator<T>`. All query handlers and all four event handlers are registered in
  `PharmacyModule` (ADR-341), all Scoped. A script found no unregistered handler or validator class. Nothing that `AddInMemoryBusServices()` registers is
  hand-registered, and `ValidateScopes`/`ValidateOnBuild` are untouched. The `DenyAllDietAuthorisationGate` registration was replaced by the real gate.
- **Layers.**
  - Domain references no EF Core, Application, Read, Infrastructure or Api type.
  - Application does not reference Read, and Read does not reference Application.
  - No controller holds a `DbContext`, a repository or an aggregate.
- **Aggregates.** All six roots mutate state only through their own methods (private setters). Each method raises its specified event and calls
  `UpdateVersion()`. Other aggregates are referenced by id only. Invariants that depend only on the aggregate's own state (status machine, validity,
  supplies, acknowledgement binding, repeat eligibility) live in the aggregate. The register entry and seal have only a factory, with no mutator.
  Owned collections are bounded (≤ 30 / ≤ 50 / ≤ 10 / 1…5).
- **Rule-1 exceptions.** Dispense and reverse commit dispensing, prescription, ledger and register in one save (ADR-143). Renew commits two prescriptions in
  one save (ADR-148). Both match the recorded exceptions.
- **Persistence.**
  - Every root goes through `ConfigureAggregateBase` (`xmin`, `ValueGeneratedNever`, `Events`/`Metadata` ignored). Owned ids are `ValueGeneratedNever`.
  - Children use `OwnsMany`, with no `HasMany`.
  - Timestamps are `timestamptz` and dates are `date`. Money is `numeric(12,2)` + `char(3)` (ADR-054). Enumerations are stored by `Value`, and the filtered
    index predicates are built from `.Value` (U-X3).
  - Every migration touches the `pharmacy` schema only. The snapshot keeps all 125 earlier tables and adds 11.
  - The four immutability triggers match §8.9. The interceptor is registered in `AddDbContext`.
  - No code path or migration runs `UPDATE`/`DELETE` on `cd_register_*` (ADR-144).
- **Read side.** Every query handler projects with `Select`, untracked, and none writes. The safety check is a query. Both §12.4 paged routes are paged.
- **Events.** All event names are in the past tense. The merge handler and the audit handlers run one `UpdateAsync`/`SaveAsync` per row, never call
  `SaveChangesAsync` and never enqueue (ADR-057). Messages are enqueued only from command handlers, before the save.
- **Worker.** It is a `BackgroundService` in the one host. It creates a scope per unit of work, dispatches through `IInMemoryDispatcher` and never throws out
  of `ExecuteAsync`. It is disabled in `appsettings.Testing.json`.
- **Shared-file edits are append-only.**
  - `NotificationSubjectKind.PrescriptionRefillDue = 7`;
  - five catalogue templates;
  - three link-builder methods;
  - the `UniqueConstraintExceptionFilter` map;
  - the five test fakes in other areas' Application tests (link-builder stubs, under §13's `tests/**`);
  - ADR-137…154 appended with no deletions;
  - `docs/handover/HO-PHM-001.md`, where only the §1 Amended (build) row was added (the established convention, not a §14 finding).
- **No new NuGet package, and no `*.csproj` change.**

DEVIATIONS (Amended (build) row), judged
1. Ports.
   - **Not compliant:** `IStockPositionReader` and an Inventory aggregate load. See HIGH 1.
   - **Compliant:** `ICatalogueReader`/`ISkuPriceReader` over Shop's `SkuRows()`/`PriceRows()`, the same pattern as `CatalogueSkuDirectory` (ADR-111), with
     one exception (MEDIUM 2).
   - **Compliant:** one `DeductAsync` per SKU, and `StockReferenceKind.ControlledDrugCorrection`. Both use the published `IStockLedger`.
2. Reversal into Quarantine through `ReceiveReturnAsync`.
   - **Compliant:** it uses the published port, and Inventory owns the destination.
   - **Compliant:** no `LedgerEntryId`; `ix_dispensing_supply` filtered to `Dispensed`.
   - The behaviour change against §8.7 / I-E11 text is an ACCEPTANCE matter.
3. `IRegisterChain.TryAppendAsync` saves seals outside `IAggregateRootRepository`.
   - **Accepted on precedent.** IMP-001's `EfInventoryAlertWriter` does the same, it is worker-only, and seals raise no events.
   - LOW 8: the change to §11.4 C8 is not recorded as an ADR line.
   - **Compliant:** `PH001` unmapped (§12.5), `HasRegisterEntriesAsync`, and the seal read.
4. `PharmacyOptions` instead of `RateLimitingOptions`, and per-account limiter keys. **Compliant.** `PharmacyOptions.cs` is a scope item (MEDIUM 4).
5. New codes, `PharmacySystemActors` and `controlled_change_reason`. **Compliant.** The codes are in append-only partials, and the column is in a §13
   migration. `PharmacySystemActors.cs` is a scope item.
6. Grouped files (`PharmacyEntities.cs`, `DispensedLineFact` in Models). **Compliant**, following the grouped-file precedent. The DenyAll fake is left in `src/`
   but unregistered (LOW 9).
7. Behaviour decisions. Not architectural. For ACCEPTANCE.
8. OQ-12 adopted through `[ClinicalRecordAccess]`, plus an explicit access command after id-routed reads. Mostly compliant (LOW 6). The EMR section provider
   was not built, which is a contract gap for ACCEPTANCE.
9. Five branch migrations, which the integrator regenerates on merge. **Compliant** with established practice. For TEST.
10. Test technique. Not architectural.

FINDINGS
- [HIGH] **Pharmacy depends on Inventory-internal code, not only on Inventory's published ports.** This breaks ADR-151 ("Inventory … through their ports"),
  HO-PHM-001 §14 ("consumed through their ports only") and §20.2 (a missing port is a stop: report it to the owner, do not work around it).
  - **HO-IMP-001 itself calls this port internal.** HO-IMP-001 §1 Amended (2026-09-23) says in so many words that "`IStockPositionReader` is
    Inventory-internal". That is why `GetBranchBalancesAsync` was added to the published `IStockLedger` for this slice.
  - CONTRACTS.md publishes `IStockLedger` and not this reader.
  - Pharmacy is the first non-Inventory consumer of the reader anywhere in `src/`.
  - **Sites:**
    - src/Application/CatEcommerce.Application/CommandHandlers/Prescription/DispensingCommandHandlers.cs:38, :78 (dispense handler, FEFO candidates);
    - src/Application/CatEcommerce.Application/CommandHandlers/Prescription/ControlledDrugCommandHandlers.cs:34, :104 (correction handler, dispensary
      location);
    - src/Read/CatEcommerce.Read/QueryHandlers/Prescription/DispensingQueryHandlers.cs:20, :37 (dispense plan). Its doc comment at :15 calls the reader "the
      published `IStockPositionReader`", which contradicts its owner.
  - **Undeclared in the Amended row:** ControlledDrugCommandHandlers.cs:31, :109 inject `IAggregateRootRepository<StockLocationAggregate>` and load Inventory's
    location aggregate. ADR-151 allows a cross-context repository load for visits only.
  - Why HIGH: two Application handlers and a Read handler couple directly to Inventory's internal read model, with no Pharmacy adapter in between. HO-SRG-001
    and HO-EMR-001's treatments face the same need, so the pattern will be copied.
  - **What clears it:** one of the two routes the handover allows. Either HO-IMP-001's owner publishes the read (an Amended row on HO-IMP-001 §8.8 plus a
    CONTRACTS.md entry, after which this becomes compliant as written), or Pharmacy takes batch status, location kind and the dispensary from `IStockLedger`
    once Inventory extends it. This is a decision for the HO-IMP-001 owner. The gate does not pick between the two.
- [MEDIUM] 2. src/Infrastructure/CatEcommerce.Infrastructure/Pharmacy/PharmacyReaders.cs:29, :60 — `CatalogueReader.GetProductAsync` and
  `SearchPrescriptionProductsAsync` query `db.Products` (Shop's aggregate table) directly, not a Shop-owned projection.
  - `GetProductAsync` is on the write path of `AuthoriseDietCommand`. ADR-092 says contract queries "keep … Pharmacy off `product.*` tables in write paths".
  - Deviation (1) says the adapter reads only the published `SkuRows()`/`PriceRows()`, which is not quite true.
  - Kept at MEDIUM because the access is confined to the one designated adapter file (ADR-111's pattern), so a Shop schema change touches only that file.
  - Fix: a Shop-owned product projection beside `SkuRows()`, or correct the Amended row.
- [MEDIUM] 3. src/Api/CatEcommerce.Api/Controllers/StaffPrescriptionsController.cs:165-174 — `GetVisitPrescriptions` dispatches two queries, against cqrs.md §5
  rule 1. §12.4 asks for prescriptions plus the visit's diet authorisations on one route, so one composite query is the compliant shape. Severity follows the
  FIL-001 precedent for a second dispatch.
- [MEDIUM] 4. Scope: 9 files fall outside §13's literal list. None touches §14 forbidden scope. Each needs a §1 Amended record, per the established practice
  (EMR-001, IMP-001). The list is in SCOPE CHECK.
- [MEDIUM] 5. Lists that are capped, not paged (SRS UI-4, read-side rule 4). §12.4 does not specify paging for these routes, so this is MEDIUM, consistent with
  the EMR-001 ruling:
  - CustomerPrescriptionQueryHandlers.cs:58 (200);
  - DietAuthorisationQueryHandlers.cs:88 (200), and the cat and visit diet lists at :38 and :55, which are uncapped;
  - DispensingQueryHandlers.cs:90 and :99 (pharmacy queue, 200 each);
  - PrescriptionQueryHandlers.cs:86 (history, 500).
- [LOW] 6. A query followed by a command in one action, against cqrs.md §5 rule 1:
  - the export audit: StaffControlledDrugsController.cs:132;
  - the post-read clinical-access log: StaffPrescriptionsController.cs:103, StaffDispensingsController.cs:38, CustomerPrescriptionsController.cs:51.

  LOW because it follows the approved audited-export precedent (IMP-001 LOW 1) and deviation (8). EMR logs before the read, in a filter. Pharmacy logs after
  the read because the cat is known only then. Both fail closed.
- [LOW] 7. src/Domain/CatEcommerce.Domain/Aggregates/Prescription/ControlledDrugRegisterEntryAggregate.cs:112 — `ControlledDrugRegisterEntryRecordedEvent` is
  dispatched only for corrections, which are saved through the repository.
  - Entries enlisted through `IControlledDrugRegister.Enlist` (dispense, reversal, and later SRG/EMR/IMP) are committed by another aggregate's save. Their
    events are never dispatched, because the repository dispatches only the saved root's events.
  - No handler consumes this event today. Document the limit on the event (§11.5) before a consumer relies on it.
- [LOW] 8. Deviation (3) changes §11.4 C8's commit mechanism (a port-owned `SaveChangesAsync`, not a repository save). Add a line under the HO-PHM-001 ADRs,
  as IMP-001 recorded its build amendments, so HO-SRG-001 does not copy either shape blindly.
- [LOW] 9. `src/Infrastructure/CatEcommerce.Infrastructure/Fakes/Pharmacy/DenyAllDietAuthorisationGate.cs` remains in `src/`, unregistered (deviation 6). §5 and
  §20.2 say no stubs in `src/`. It is dead code now; remove it with its test when convenient. IMP-001 left `FakeStockLedger` the same way.
- [LOW] 10. §12.1 asks for an explicit `WitnessPassword` entry in HO-ACC-001's `AuditRedactionRules`. It was not added. It is already covered by the
  `password` regex (`Auditing/AuditRedactionRules.cs:12`), and no entity carries the password, so this is documentation only.
- [LOW] 11. src/Infrastructure/CatEcommerce.Infrastructure/Pharmacy/PharmacyRetentionReviewSource.cs:26 — the C-ALL-3 review reads `db.Set<VisitAggregate>()`
  (an EMR table) directly. It is a read-only, projected, reporting-style read that read-side-pattern §5 permits, but it is a second direct read of another
  area's table beside MEDIUM 2. Noted for the owners of ADR-151.

FOR TEST (runtime, not observable here)
- `dotnet ef migrations has-pending-model-changes` must report no changes once the integrator regenerates. The model snapshot was reordered (−1817 lines) but
  keeps its table set.
- A failing step after enlisting must leave nothing behind. Examples: the second SKU's `DeductAsync` fails, or `RecordSupply`/`Create` throws after ledger and
  register rows are enlisted. The handler returns a violation without saving, which is safe only if nothing else saves in that scope.
- Reversal mutates the dispensing and the prescription before `ReceiveReturnAsync`. The same check applies.
- V1, the sequence default read back after save (`EntryNumber` with `SetAfterSaveBehavior(Ignore)`), and V2, the triggers. I-D1 covers both.

SCOPE CHECK
- files in allowed scope: 180/189. Counted in scope:
  - files inside §13;
  - ADR-341 per-area translations: `Modules/PharmacyModule.cs` for `Program.cs`, `CatEcommerceDbContext.Pharmacy.cs`, and the `Actions`, `ActionRoles`,
    `BusinessViolationCodes`, `SettingDefinitions` and `SettingKeys` `.Pharmacy.cs` partials;
  - CONTRACTS C-ALL files: `RecordClassification`, `ErasureExemptions` and `ClinicalAuditEntityTypes` `.Pharmacy.cs`, and
    `Pharmacy/PharmacyRetentionReviewSource.cs`;
  - grouped files in §13 folders: `IPharmacyPorts.cs` and `IControlledDrugRegister.cs` (the ports named in §8.12/§8.13; `IPharmacyClock` is the one new
    port, a thin wrapper over `BranchCalendar`), `Entities/PharmacyEntities.cs`, `ControlledDrugRegisterConfigurations.cs`, the grouped validator files,
    and the `Pharmacy*ViewModels.cs` files;
  - the `Program.cs` interceptor line (§12.7);
  - `docs/handover/HO-PHM-001.md`, where only the §1 Amended row changed (the established convention).
- files outside allowed scope (9, none in §14 forbidden scope — MEDIUM 4):
  - src/Api/CatEcommerce.Api/Controllers/PharmacyControllerBase.cs
  - src/Api/CatEcommerce.Api/RateLimiting/PharmacyRateLimitPolicies.cs
  - src/Api/CatEcommerce.Api/appsettings.Testing.json (`Pharmacy:Worker:Enabled=false`, the same change the integrator made for IMP)
  - src/Domain/CatEcommerce.Domain/DomainServices/BatchOfferFactory.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/ControlledDrugCanonicalMapping.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/IPharmacyRecordFinder.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/IRegisterChain.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Configuration/PharmacyOptions.cs
  - src/Shared/CatEcommerce.Shared/Common/Constants/PharmacySystemActors.cs
- None of these was touched:
  - `web/**`, `docs/SRS.md` or `docs/USER_STORIES.md`;
  - `.ai/**` other than adr.md, or `.claude/**`;
  - any `*.csproj`, `*.sln`, `NuGet.Config` or `global.json`;
  - any HO-IMP-001, HO-SHP-001 or HO-EMR-001 source file;
  - `CatAggregate` or `CatWeightRecordAggregate`.

  No package was added.

---

## Re-check 2026-10-09

GATE: ARCHITECTURE (re-check, delta only — owner rule: after a HIGH, re-run only the failing gate on only the delta)
HANDOVER: HO-PHM-001
COMMITS: 3c73e35b (`git diff fef6cee0..HEAD`, 22 files; fef6cee0 is the master merge)
DATE: 2026-10-09
STATUS: FAIL

What this re-check reviewed: every file in the delta. HO-PHM-001 §13/§14 and §1 Amended (build). HO-IMP-001's new §1 Amended row and its §8.8 code block.
CONTRACTS.md rows 1–2. HO-SHP-001's references to `SkuRows`. ADR-092 and ADR-111. `ho/SHP-001-FU3`'s uncommitted change to `SkuRows.cs` in its
worktree. For check (1), the whole branch was grepped, not only the delta. Nothing was built or run.

VERIFIED (no finding)
- **HIGH 1 is closed.** No Pharmacy file references Inventory-internal code anywhere on the branch.
  - The grep covered every non-migration file in Pharmacy's Api, Application, Domain, Read, Infrastructure/Pharmacy and Shared, and included
    `BatchOfferFactory`.
  - The types searched for were `IStockPositionReader`, `StockCandidate`, `StockLocationAggregate`, `BatchAggregate`, `StockLocationKind`,
    `IInventoryLedgerWriter`, `db.StockLocations`, `db.Batches`, `db.StockPositions`, any `Aggregates.Inventory` namespace and the Inventory view entries.
  - The one hit is `ControlledDrugSourceType.GoodsReceipt`, a Pharmacy enumeration value and not an Inventory type.
  - `IStockPositionReader` and `StockLocationAggregate` are now referenced only by Inventory's own files and tests.
- **The new port is published (check 2).**
  - **Interface.** `IStockLedger.GetDispensaryBatchesAsync` and `GetClinicDispensaryAsync` (IStockLedger.cs:58-60) are read-only.
  - **Records.** The `DispensaryBatch`/`DispensaryLocation` records are in `Domain/Models` and use only the Shared `BatchStatus` enumeration.
  - **HO-IMP-001.** The change is recorded as a dated Amended row and in the §8.8 code block, beside `GetBranchBalancesAsync`.
  - **CONTRACTS.md.** Row 1 lists both methods and states the fake's behaviour.
  - **Implementation.** It is inside Inventory, in `EfStockLedger.Consumer.cs:262-290`. Inventory using its own position reader is correct. The dispensary
    read is an untracked `Select` projection.
  - **Fake.** `FakeStockLedger` answers an empty list and `DispensaryNotFound`. Both match the fail-closed read semantics already set for
    `GetBranchBalancesAsync`.
  - **Every implementation updated.** All three implementations of `IStockLedger` on the branch have the new members: Ef, Fake and the test's
    `FailingLedger`.
  - **Callers.** The dispense handler maps a failed read to a violation and does not throw. The correction handler keeps its previous fallback.
    The dispense-plan query treats a failed read as no offers, and still writes nothing.
- **The IMP-owned edits are covered by owner decision A.** These six files are HO-IMP-001's: `IStockLedger.cs`, `EfStockLedger.Consumer.cs`,
  `FakeStockLedger.cs`, `FakeStockLedgerTests.cs`, `HO-IMP-001.md` and `CONTRACTS.md`.
  - They are in §14 forbidden scope for this slice, but they carry out the route the first pass named and the owner chose.
  - They are therefore not counted as a scope violation.
  - The only written record of decision A is in documents this branch wrote: the HO-IMP-001 Amended row and HO-PHM-001 Amended (build) (1) and (11). The
    integrator should confirm at merge that the owner authorised the PHM branch to make the edit, not only chose the route.
- **The dispense-plan access log (check 4)** is at StaffPharmacyController.cs:24 and :62-65.
  - The new dependency, `IClientIpAccessor`, is an existing registered service. It is already injected by the sibling Pharmacy controllers that call the
    same `PharmacyControllerBase.TryLogClinicalAccessAsync` helper.
  - The controller still dispatches one query and then the one access-log command. This is the same shape as first-pass LOW 6 / backlog row 18
    (approved precedent), so it is not a new finding.
  - The log fails closed (`500`), and a missing plan is a `404` before any log is written.
  - The query side is unchanged and writes nothing.
  - Owner decision B leaves the queue and register reads unlogged. That is an owner ruling and not architectural.
- **Dispensing line ids.** There is a fresh `Guid` per ledger movement and one reference line id per SKU group. This is not architectural, and it is noted
  only because it is in the delta.
- **No superseded architecture appears in the delta**: no `IDispatcher`, no broker, no Mongo, no `202`, no projection and no `QueuePublishOnly`. No
  `*.csproj`, package, migration, `.ai/**` or `web/**` change.

FINDINGS
- [BLOCKER] src/Infrastructure/CatEcommerce.Infrastructure/Product/SkuRows.cs:26, :72 — a HO-SHP-001 source file was edited from the Pharmacy branch. It adds
  `SkuRow.Brand` to Shop's published projection.
  - **What the rule says.** HO-PHM-001 §14 forbids "any file of … HO-SHP-001 (`catalogue.*` code …) — consumed through their ports only. A missing port is
    a stop (§20.2)". The gate's severity table makes any §14 edit a BLOCKER.
  - **Not covered by decision A.** Owner decision A covers extending `IStockLedger` only. No owner decision covers this file.
  - **Not recorded.** The change appears only in HO-PHM-001's own Amended (build) (1). HO-SHP-001 has no Amended row for it. CONTRACTS.md does not list
    `SkuRows` or its columns at all.
  - **First foreign-area edit.** Every earlier change to `SkuRows.cs` came from a Shop commit (22d0889d, 48588a5e).
  - **The design itself is sound.** The column is additive and read-only. With it, `CatalogueReader` reads only `SkuRows()` (PharmacyReaders.cs:23-65)
    and no longer touches `db.Products`, which is the shape ADR-092 asks for, so first-pass MEDIUM 2 / backlog row 14 is fixed in code.
  - **The defect is procedural:** one bounded context changed another's published contract without that owner's record. The next consumer that needs a
    Shop column would copy the shape.
  - **What clears it** (no code change is needed for the first route):
    - (a) The owner, or HO-SHP-001's owner, ratifies the column. The coordinator then appends a dated Amended row to HO-SHP-001 (`SkuRow.Brand`, added for
      HO-PHM-001) and lists `SkuRows()` as a published projection in CONTRACTS.md, as was done for `IStockLedger` under decision A.
    - (b) Or the edit is reverted and backlog row 14 reopened, with Amended (build) (1) corrected to say `db.Products` is still read.
- [MEDIUM] docs/handover/CONTRACTS.md:35 (row 2) — the new records `DispensaryBatch` and `DispensaryLocation` are not in the DTO row.
  - The row's file glob `Models/Stock*.cs` does not match `Models/DispensaryBatch.cs`.
  - Row 1 names the methods, but a consumer looking up the record types in the register will not find them.
  - Backlog row 25.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Inventory/EfStockLedger.Consumer.cs:287 and Fakes/Inventory/FakeStockLedger.cs:61 — the
  `"DispensaryNotFound"` code is a string literal.
  - Every other failure in `EfStockLedger` uses a `BusinessViolationCodes` constant.
  - No caller surfaces it today: both callers only test `IsSuccess`.
  - Backlog row 26.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Product/SkuRows.cs:71-72 — merge collision with `ho/SHP-001-FU3`, which adds `ShippingWeightGrams` to
  the same initializer.
  - FU3 changes the `SizeOrWeight…PackQuantity` line, and this branch changes the next line, `ProductId…ProductStatus`. Adjacent hunks give a textual
    conflict.
  - The integrator keeps both columns.
  - Backlog row 27.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Inventory/EfStockLedger.Consumer.cs:265-268 — `GetDispensaryBatchesAsync` runs one
  `GetCandidatesAsync` query per SKU.
  - It is bounded by a medicine's linked SKUs, which are few.
  - Backlog row 28.

FOR TEST (runtime, not observable here)
- **Products with no SKUs.** `CatalogueReader.GetProductAsync` and `SearchPrescriptionProductsAsync` now derive products from `SkuRows()`, a `SelectMany`
  over SKUs. A product with **no SKUs** is now invisible: `GetProductAsync` returns `null`, so AuthoriseDiet sees the product as not found, and search
  omits it. Before, it was returned with an empty `SkuIds`. Confirm this is acceptable (ACCEPTANCE), or that a prescription diet always has at least one
  SKU.
- **Search ordering and limit.** `SearchPrescriptionProductsAsync` now orders and limits on distinct `(ProductId, ProductName)`. Confirm that `take` still
  limits products, not SKUs.
- **New tests.** `InventoryStockPortEndpointTests.DispensaryBatches_*`, the new dispense-plan access-log tests and `FakeStockLedgerTests` must pass.

SCOPE CHECK
- files in the delta: 22
- in allowed scope, 14:
  - §13 paths: StaffPharmacyController.cs, ControlledDrugCommandHandlers.cs, DispensingCommandHandlers.cs, Pharmacy/PharmacyReaders.cs,
    DispensingQueryHandlers.cs, Models/DispensaryBatch.cs ("new DTOs only"), Domain.Tests/BatchOfferFactoryTests.cs, the four IntegrationTests files
    and docs/test-cases/TC-HO-PHM-001.md;
  - docs/handover/HO-PHM-001.md, where only the §1 Amended (build) row changed (the established convention);
  - docs/backlog/PHM.md (gate-policy convention).
- already recorded outside scope, 1: src/Domain/CatEcommerce.Domain/DomainServices/BatchOfferFactory.cs (first-pass MEDIUM 4 / backlog row 16, no new
  finding).
- §14 files authorised by owner decision A, 6: IStockLedger.cs, EfStockLedger.Consumer.cs, Fakes/Inventory/FakeStockLedger.cs,
  tests/CatEcommerce.Infrastructure.Tests/Fakes/Inventory/FakeStockLedgerTests.cs, docs/handover/HO-IMP-001.md and docs/handover/CONTRACTS.md.
- files outside allowed scope, with no authorisation, 1: **src/Infrastructure/CatEcommerce.Infrastructure/Product/SkuRows.cs**. It is in §14 forbidden scope
  (HO-SHP-001). This is the BLOCKER above.
- No package, `*.csproj`, migration, `.ai/**`, `web/**`, `docs/SRS.md` or `docs/USER_STORIES.md` change.

Verdict: HIGH 1 from the first pass is **closed**. The gate still **FAILS** on one new BLOCKER: the unratified edit to Shop's `SkuRows.cs`. Route (a)
clears it with documents only, after which this re-check would pass.

## Re-check 2 — 2026-10-09

GATE: ARCHITECTURE (re-check 2, delta only)
HANDOVER: HO-PHM-001
COMMITS: cb78b115 (`git diff 52628b5b..HEAD`, 2 files)
DATE: 2026-10-09
STATUS: PASS

What this re-check reviewed: the whole delta, which is two files: docs/handover/CONTRACTS.md (rows 2 and 24) and docs/handover/HO-SHP-001.md (§1, one new Amended row).
No source, test, package, migration, `.ai/**` or `web/**` file changed. Nothing was built or run.

Owner facts, as relayed by the orchestrator, who asked the owner directly in the session on 2026-10-09:
- (a) The owner chose "Extend IStockLedger", which was described to them as "Inventory adds a published read on IStockLedger ... Small code change in both
  modules". The PHM branch was therefore authorised to edit the Inventory-owned files. This answers the confirmation the previous re-check asked the
  integrator for. The six IMP-owned files are authorised and are not a scope violation.
- (b) Asked about the SkuRows `Brand` column, the owner answered "Ratify it": add an Amended row to HO-SHP-001 and a `SkuRows()` entry in CONTRACTS.md, with no
  code change.

VERIFIED (no finding)
- **The previous BLOCKER is closed** (SkuRows.cs edited from the Pharmacy branch without an owner record). Under owner fact (b) it was cleared by route (a) of
  the previous re-check, using documents only:
  - HO-SHP-001.md:20. A dated Amended row (2026-10-09) in Shop's own §1 records `SkuRow.Brand` as an owner-ratified cross-area edit. The row says the column
    is additive and read-only and changes no Shop behaviour, write path or response. It cites the ADR-092 reason and points to CONTRACTS #24.
  - CONTRACTS.md:57. New row 24 lists `SkuRows()`/`SkuRow` as Shop's published SKU projection. Owner: HO-SHP-001, Amended 2026-10-09. Consumer: HO-PHM-001
    (`CatalogueReader`, `SkuPriceReader`). File: `Product/SkuRows.cs`. Behaviour: "read-only, untracked; additive columns only". Row 24 is the next free
    number after row 23.
  - SkuRows.cs is unchanged in this delta, so the code reviewed last time stands.
- **The previous MEDIUM / backlog row 25 is fixed.** CONTRACTS.md:35 (row 2) now lists `DispensaryBatch` and `DispensaryLocation`. The row is dated for
  HO-PHM-001, and its file column adds `Models/DispensaryBatch.cs`. Both records are declared in that file (DispensaryLocation at line 16).
- The three LOW findings from the previous re-check (backlog rows 26-28) are untouched by this delta and stay in the backlog. They do not block.

FINDINGS
- none

SCOPE CHECK
- files in the delta: 2
- files in allowed scope: 2/2. Both are coordinator records the owner directed under fact (b): CONTRACTS.md (also covered by fact (a)) and the HO-SHP-001 §1
  Amended row. Neither changes code.
- files outside allowed scope: none

Verdict: the BLOCKER from re-check 1 is **closed** by owner ratification (b), and the IMP-owned edits are confirmed as authorised under (a). There are no
BLOCKER and no HIGH findings, so the ARCHITECTURE gate for HO-PHM-001 **PASSES**.
