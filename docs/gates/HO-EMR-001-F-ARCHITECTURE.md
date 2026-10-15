GATE: ARCHITECTURE
HANDOVER: HO-EMR-001 (Phase F only -- US-CLN-15 treatments, consumables and stock; §8.6, §8.12, §17 Phase F)
COMMITS: d6ad74e1 79341a82 57b66215 dd5d9abc cd09c041 37e8c469 (branch ho/EMR-001-F, range b12fd39d..37e8c469, 44 files)
DATE: 2026-10-13
STATUS: FAIL

The gate fails on one HIGH, and a recorded decision closes it. The code does not have to change. Owner gate
policy (2026-09-27) applies: only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings are proposed
below as docs/backlog/EMR.md rows 41 onward (the highest existing row is 40). This gate does not edit the backlog.

What this pass reviewed:
- `git diff b12fd39d..37e8c469`: every source file, the hand-written SQL in `AddVisitTreatments`, and the
  snapshot by table.
- HO-EMR-001 §1, §6, §8.1, §8.2, §8.6, §8.12–§8.14, §9 (ADR-126, ADR-127, OQ-12, OQ-19), §10–§14, §17, §19
  and §20.
- docs/handover/CONTRACTS.md §1 (rows 1–3, 24–25), D-2, G-2 and §5.
- HO-BIL-001 §8.13 (Phase J) and HO-PHM-001 §1 Amended (build).
- `.ai/`: architecture, cqrs, bounded-contexts, namespace-conventions, di-registration-guide, read-side-pattern,
  and adr.md ADR-341.
- Earlier rulings, to keep severities consistent: HO-EMR-001-ARCHITECTURE.md, HO-PHM-001-ARCHITECTURE.md and
  HO-SRG-001-ARCHITECTURE.md.
- The ports this phase consumes: `IStockLedger`, `ISkuDirectory`, `IControlledDrugDirectory`, `ISkuPriceReader`,
  `IBillingPricingInputs` and `IBillableItemSource`.

Nothing was built or run. Every runtime claim is for the TEST gate. That includes I-F4 (real parallelism), I-F6
(forced failure leaves neither row) and the `tr_visit_treatment_frozen` trigger through Npgsql.

VERIFIED (no finding)
- **No superseded architecture.** None of these appear in the diff:
  - `IDispatcher`, `IBusMessageDispatcher` or `IEventPublisher`;
  - MassTransit, Mongo/Bson or `QueuePublishOnly`;
  - `202`, a rejection event, `CommandServices`, `ErrorEvents` or `IReadRepository`;
  - `HasMany`, a Transient registration or a second host.

  Both handlers save with `UpdateAsync(visit, EventPublishOption.InMemoryPublishOnly)`.
- **Layers.**
  - Domain gains no EF Core, Application, Read or Api type.
  - Application does not reference Read, and Read does not reference Application.
  - Namespaces follow `.ai/namespace-conventions.md`, with `Visit` as the feature segment.
  - `ClinicalSkuReader` sits in `Infrastructure.Consultation`, an allowed "other infrastructure" area folder.
- **Controllers.**
  - `StaffVisitsController.RecordTreatment`/`ReverseTreatment` and both `StaffDispensaryController` actions each
    dispatch exactly one command or query through `IInMemoryDispatcher`.
  - The line id is minted in the controller, and `ValidateCommand = true` is stamped.
  - Responses are `201` with the real result, or `204`, or the `ClinicalResponses` mapping: `400` with
    `availableQuantity`/`unit` for the warning, `409` for the race, and `404` for another branch's visit or an
    unknown line.
  - No controller holds a DbContext, a repository or an aggregate.
- **Active branch gives 404.**
  - Both handlers return `VisitNotFound` when `visit.BranchId != command.BranchId`.
  - `TreatmentLineNotFound` joins `ClinicalResponses.NotFoundCodes`.
  - The dispensary reads are scoped to the `X-Branch-Id` branch.
- **Aggregate discipline.**
  - `RecordTreatment`/`ReverseTreatment` start with `EnsureOpen()`, enforce the 100-line cap, the lengths and
    quantity coherence, and end with `UpdateVersion()`.
  - They raise `TreatmentRecordedEvent`, `TreatmentReversedEvent` and, on a shortfall,
    `ClinicalStockShortfallRecordedEvent`. Payloads are ids, an enumeration, numbers and a flag. There is no
    free text.
  - Lines are never removed (C-ALL-4).
  - The handlers mutate only through these two methods.
- **Rule-1 exception (ADR-126).**
  - The only extra writes are Inventory rows enlisted through `IStockLedger`: `DeductAsync`, `RecordShortfall`
    and `ReceiveReturnAsync`. One `UpdateAsync` commits them with the visit, and the F23 comment is present.
  - `CatAggregate` is loaded read-only, for the owner id in `StockParty`.
  - No `inventory.*` row is written any other way. No stock table is added.
  - Nothing moves stock from an event handler (§20.7). Nothing enqueues (§20.8).
- **Domain services and ports.**
  - `DispensaryBatchPicker` composes four read-only `IStockLedger` members. It takes the FEFO default from
    Inventory's own `PlanAsync`, so FEFO stays Inventory's domain service (bounded-contexts rule 4).
  - The command handler and `GetIssuableBatchesQuery` share the picker. It is not a CommandService (ADR-015).
- **Persistence.**
  - `visit_treatment` is mapped with `OwnsMany`. Ids are `ValueGeneratedNever`.
  - Enumerations are stored by `Value`.
  - Money is `numeric(12,2)` plus `char(3)` (SRS C-6). Instants are `timestamptz` (SRS C-7).
  - The `batch_id` and `sku_id` indexes match the §12.1 table.
  - The table is covered by the visit's existing `xmin`.
  - `clinic.visit_child_frozen()` and `no_truncate()` are attached, so DELETE is never allowed and nothing
    changes once the visit is finalised (ADR-118).
  - `RecordClassification` has a row for the table (C-ALL-1). `ConsultationConventionTests` U-X3 covers it.
- **Read side.**
  - `TreatmentRows.OfVisitAsync` projects the owned rows with `AsNoTracking` and `Select`. The visit is never
    rehydrated, and the visit detail and discharge summary reuse it.
  - `GetVisitChargeLinesQuery` is a projected, untracked read filtered by `!IsMarkedToDelete` and `Finalised`.
  - No query writes anything. `PlanAsync` and `GetBatchUsabilityAsync` are read-only by contract.
- **Registrations** (`ConsultationModule.AddTreatments`, ADR-341):
  - 2 handlers, plus 2 validators registered as `IValidator<T>`;
  - 3 query handlers;
  - `AuditTreatmentReversedHandler` (§11.6). It writes its row like the existing audit handlers;
  - `DispensaryBatchPicker` and `IClinicalSkuReader`.

  Everything is Scoped. Nothing that `AddInMemoryBusServices()` provides is registered. `Program.cs` is
  untouched.
- **IMP-4: `StockReferenceKind.Visit`, not `VisitTreatment`. Compliant.** CONTRACTS.md D-2 already settled this:
  every consumer uses the owner's member list verbatim. The line id travels as `ReferenceLineId` (G-2), so the
  recall trace stays complete (C-3).
- **IMP-7: no `VisitTreatmentReversal` member. Compliant.**
  - The return uses `Visit` with the same line id. The ledger tells the two apart by movement kind (return
    versus clinical use).
  - The stock lands in Quarantine, as HO-EMR-001 OQ-19 accepts.
  - This is the same shape HO-PHM-001 Amended (build) (2) uses for dispensing reversal.
- **Ledger ids not stored. Compliant.**
  - `IStockLedger` returns movement lines without entry ids, so `StockEntryId`/`ReversalStockEntryId` cannot be
    filled.
  - The trace is the ledger's own reference: `Visit`, the visit id and the line id.
  - This is the same ruling HO-PHM-001 Amended (build) (2) records for `DispensingLine`.
- **One line is one batch. Compliant with the model.** §8.6 gives a line one `BatchId`/`LotNumber`/`LocationId`,
  and A10 says "the exact batch". The behaviour it produces is a MEDIUM below.
- **No HO-IMP-001 or HO-SHP-001 file was touched.** Also untouched:
  - `docs/handover/**`, `.ai/**` and `.claude/**`;
  - `web/**`;
  - any `*.csproj`, `*.sln`, `NuGet.Config` or `global.json`;
  - `CatAggregate` and `CatWeightRecordAggregate`.

  No package was added. The builder's claim is confirmed.

SHP-1 RULING (the key question)
- **The design is architecturally sound, and I would accept it.**
  - The SKU facts come from one adapter, `ClinicalSkuReader`, behind a Domain port in `Domain/DomainServices`.
    HO-EMR-001 §8.13 puts ports in that folder.
  - Inventory's `CatalogueSkuDirectory` and Pharmacy's `CatalogueReader` already use this pattern (ADR-111).
  - It reads Shop only through the published `SkuRows()` projection (CONTRACTS row 24).
  - No foreign file was edited.
  - Taking the controlled flag from HO-PHM-001's `IControlledDrugDirectory` matches the rest of the system.
    PHM §8.9 exports it as "which SKUs and medicines are controlled", and HO-IMP-001 uses the same call.
- **Adapting was not what the handover allowed, and the adaptation is not recorded where it counts.**
  - §8.12's closing paragraph and §20.2 both say that if SHP-1…3 differs, the builder stops Phase F and reports.
  - SHP-1 differs materially. There is no reader, no unit and no controlled flag on the SKU.
  - The adaptation also reverses a recorded answer. OQ-12 resolved the controlled-flag source as "SKU flag
    (SHP-1)".
  - The difference is recorded only in the TC sheet, which is the builder's own file. There is no HO-EMR-001 §1
    Amended row and no CONTRACTS.md entry.
- **It needs an integrator or owner decision.** That is HIGH 1 below, and the exact decision is written there.
  The SRG-001 `ConsentRules` overload and the PHM-001 `IStockLedger`/`SkuRows.Brand` cases were closed by a record
  alone, and so can this one. Those were BLOCKERs because they edited §14 files. This one is HIGH because no
  forbidden file was edited.

FINDINGS
- [HIGH] 1. Phase F adapted to a material §8.12 mismatch and became a third-area consumer of three area-internal
  ports, without a recorded decision.
  - Sites:
    - src/Domain/CatEcommerce.Domain/DomainServices/IClinicalSkuReader.cs:12;
    - src/Infrastructure/CatEcommerce.Infrastructure/Consultation/ClinicalSkuReader.cs:21;
    - src/Application/CatEcommerce.Application/CommandHandlers/Visit/TreatmentHandlers.cs:52-54;
    - src/Read/CatEcommerce.Read/QueryHandlers/Visit/TreatmentQueryHandlers.cs:102.
  - Rules broken:
    - HO-EMR-001 §8.12 and §20.2: "If any of IMP-1…6 or SHP-1…3 differs in the merged code, stop Phase F and
      report."
    - CONTRACTS.md §5 names `ISkuDirectory` (IMP-internal) and `ISkuPriceReader` (PHM-internal) as ports that "a
      third area" does not call: "If a wave agent finds it actually needs one of these … that is a new contract
      to report, not something to build unreviewed."
    - `IBillingPricingInputs` is documented as "Billing's read-only view of Administration's tables". EMR uses
      it for §8.6 step 2's service price because HO-ADM-001 publishes no service-price-at-t port.
    - OQ-12's recorded answer, "SKU flag (SHP-1)", is replaced by PHM's formulary link.
  - Why HIGH: the pattern propagates. "When the contract is missing, consume another area's internal adapter"
    would be copied by SRG/BRD/ORD follow-ups. And the controlled-drug refusal (ADR-126) now depends on a source
    the handover did not name: a controlled SKU with no formulary medicine linked is accepted as a treatment.
  - **Decision needed (integrator, or the owner for (b)).** Record it as a dated HO-EMR-001 §1 Amended row plus
    CONTRACTS.md rows. No code change is needed if all of it is ratified:
    - (a) SHP-1 is realised as `IClinicalSkuReader` (Domain port) and `ClinicalSkuReader` (Infrastructure). This
      authorises three files outside §13 (`IClinicalSkuReader.cs`, `Infrastructure/Consultation/ClinicalSkuReader.cs`
      and `DispensaryBatchPicker.cs`) and an eighth migration, `AddVisitTreatments`. §12.1 said "F adds no
      migration", but the owned table was never created because Phase B shipped without treatments.
    - (b) OQ-12 is re-answered. The controlled-drug source is HO-PHM-001's
      `IControlledDrugDirectory.FindBySkuAsync`, meaning a formulary medicine flagged controlled and linked to the
      SKU. A SKU with no formulary link is not refused, which is the same rule Inventory applies.
    - (c) HO-EMR-001 is added as a consumer in CONTRACTS.md of `ISkuDirectory` (HO-IMP-001), `ISkuPriceReader`
      (HO-PHM-001), `IBillingPricingInputs.GetServicePriceAsync` (HO-BIL-001) and `IControlledDrugDirectory`
      (HO-PHM-001). Each owner accepts the extra consumer. The alternative for the service price is that HO-ADM-001
      publishes a service-price-at-t port and EMR switches to it.
    - (d) These are noted in the same row, already ruled compliant above:
      - the stock unit snapshot is Inventory's `each`, in whole units (HO-IMP-001 OQ-19);
      - IMP-4 and IMP-7 use `Visit` (D-2);
      - no ledger-entry ids are stored;
      - one line is one batch.
  - The alternative to ratifying is that HO-SHP-001 publishes an `ISkuReader` with a controlled flag and Phase F
    is re-pointed at it. That is the owner's choice.
- [MEDIUM] 2. src/Read/CatEcommerce.Read/QueryHandlers/Visit/TreatmentQueryHandlers.cs:111-116 — the dispensary
  item list with no search text joins Inventory's internal `db.StockPositions` and `StockLocationAggregate`
  directly.
  - `.ai/read-side-pattern.md` §5 permits cross-context reads, so this is not HIGH. It is read-only, at one site,
    and off the write path.
  - It is the coupling the HO-PHM-001 gate rated HIGH, and that gate predicted EMR's treatments would copy it. Owner
    decision A then published `GetDispensaryBatchesAsync`/`GetClinicDispensaryAsync` so consumers would not read
    positions.
  - `IStockLedger` has no "SKUs held at the dispensary" member.
  - Fix: HO-IMP-001's owner adds one, or the decision in HIGH 1 lists this read.
  - Proposed backlog row 41.
- [MEDIUM] 3. `GetVisitChargeLinesQuery` does not yet fit HO-BIL-001 Phase J as Billing builds item sources. The
  builder's fit table misses two points:
  - **Reachability.** Billing's `IBillableItemSource` implementations live in `Infrastructure/Billing/ItemSources`,
    and Infrastructure cannot reference Read. The Read project references Infrastructure, not the other way round
    (see the csproj files). The query and its ViewModel are therefore unreachable from where `VisitBillableItemSource`
    would sit, unless Billing places it in Api/Read or dispatches the query.
  - **Types.** `VisitChargeLineViewModel` carries `Quantity` as a formatted string and `UnitPrice` as
    `MoneyViewModel(string, string)` (TreatmentViewModels.cs:27-28; TreatmentQueryHandlers.cs:87-88).
    `BillableSourceItem` needs `decimal` and `Money`, so Billing would have to parse display strings.

  EMR built what §12.6 and ADR-127 specify. The conflict is between ADR-127 ("Billing reads
  `GetVisitChargeLinesQuery`") and HO-BIL-001 §8.13 ("`VisitBillableItemSource` reading the visit's treatments").
  The builder's other fit rows are confirmed accurate:
  - no `CustomerId` or `BoardingStayId` on `VisitFinalisedEvent`;
  - no priced diagnostics;
  - the kind mapping;
  - finalised visits only;
  - the line id works as `SourceLineRef`.

  Decide before Phase J. Either EMR exposes a Domain-level read port with typed values, or Billing's source reads
  `clinic.visit_treatment` itself, the way `AppointmentBillableItemSource` reads appointments. Proposed backlog
  row 42.
- [MEDIUM] 4. src/Application/CatEcommerce.Application/CommandHandlers/Visit/TreatmentHandlers.cs:202-220 — one
  line is one batch, so "available" means the chosen (FEFO) batch, not the dispensary.
  - Example: 3 in the FEFO batch, 10 in the next, and a request for 5. The vet gets "Only 3 … are in the dispensary".
  - With `acceptShortfall`, a 2-unit shortfall goes to Inventory's reorder list even though stock is on the shelf.
  - This is consistent with §8.6's single-batch line, but the warning text is wrong and the reorder signal is a
    phantom.
  - Owner to confirm one of two fixes: say "in this batch" and offer the next batch as a second line, or compute
    the shortfall against the dispensary total.
  - Proposed backlog row 43, Owner decision.
- [MEDIUM] 5. Scope: 7 files fall outside §13's literal list. None is in §14. Each needs the §1 Amended record
  from HIGH 1(a), per the EMR-001/PHM-001 practice. They are listed under SCOPE CHECK. Proposed backlog row 44,
  folded into the HIGH 1 record.
- [MEDIUM] 6. src/Read/CatEcommerce.Read/QueryHandlers/Visit/TreatmentQueryHandlers.cs:105 —
  `GET /api/staff/dispensary/items` is capped at 30, not paged (SRS UI-4, read-side rule 4). §12.4 specifies no
  paging, so this is MEDIUM, as the EMR-001 and PHM-001 gates ruled. Proposed backlog row 45.
- [LOW] 7. src/Api/CatEcommerce.Api/Modules/ConsultationModule.cs:153 — `DispensaryBatchPicker` is registered as
  its concrete type. `.ai/di-registration-guide.md` §3 shows domain services behind an interface. This is
  harmless here, because a Read handler and a command handler share it and it has one implementation. Proposed
  backlog row 46.
- [LOW] 8. docs/backlog/EMR.md:33 — the builder rewrote row 20 to "Closed … pending merge". The backlog is the
  integrator's file under the gate-blocking rule. The integrator should confirm the wording on merge. Proposed
  backlog row 47, or fold it into the merge note.

SCOPE CHECK
- files in allowed scope: 37/44. Counted in scope:
  - files inside §13;
  - ADR-341 translations: `Modules/ConsultationModule.cs` and `BusinessViolationCodes.Consultation.cs`;
  - the C-ALL-1 file `RecordClassification.Consultation.cs`;
  - the grouped validator file in the §13 Validators folder;
  - new DTOs in `Domain/Models`;
  - the regenerated model snapshot.
- files outside allowed scope (7, none in §14 forbidden scope; MEDIUM 5, and authorised by HIGH 1(a) if ratified):
  - src/Domain/CatEcommerce.Domain/DomainServices/IClinicalSkuReader.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/DispensaryBatchPicker.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Consultation/ClinicalSkuReader.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20261012195959_AddVisitTreatments.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20261012195959_AddVisitTreatments.Designer.cs

    These two migration files are not one of §12.1's seven migrations. The integrator regenerates them on merge
    (ADR-341 point 5).
  - src/Api/CatEcommerce.Api/Controllers/ClinicalResponses.cs (already outside §13 at the first EMR-001 gate)
  - docs/backlog/EMR.md
- §14 check: no HO-IMP-001 or HO-SHP-001 file, no `docs/handover/**`, no `.ai/**`, no `web/**` and no
  csproj/sln. No package was added.

Verdict: **FAIL** on HIGH 1 alone. The code is compliant with the modular-monolith rules. HIGH 1 closes when the
decision in (a)–(d) is recorded as a HO-EMR-001 §1 Amended row plus CONTRACTS.md consumer rows, with no code
change, or when the owner chooses the ISkuReader alternative instead.

---

## Combined re-check (ARCHITECTURE + SECURITY + CODE_REVIEW) -- 2026-10-13

GATE: ARCHITECTURE (combined re-check, also covering SECURITY and CODE_REVIEW)
HANDOVER: HO-EMR-001 (Phase F)
COMMITS: 17aad6be ae8e8555 04d8b118 (fix round cf3e8652..04d8b118, branch ho/EMR-001-F, 22 files)
DATE: 2026-10-13
STATUS: FAIL

The re-check fails on one BLOCKER, and a record closes it. The code does not have to change. Everything else the round claims
holds up. Owner gate policy (2026-09-27) applies: only BLOCKER and HIGH findings fail.

What this pass reviewed:
- `git diff cf3e8652..04d8b118`: every source and test file, the HO-EMR-001 §1 Amended row, CONTRACTS rows 32-34,
  backlog rows 41-57 and the TC sheet.
- Read for context:
  - HO-EMR-001 §13 and §14;
  - HO-IMP-001 §1 rows 20-21 (the precedent `IStockLedger` additions);
  - `StockDemandPlanner`, `StockLocationRules` and `EfStockLedger` (Plan, Deduct, ReceiveReturn);
  - `DispensaryBatchPicker`, `ClinicalResponses` and `UniqueConstraintExceptionFilter`.

Tests run (targeted only, `catecommerce_emrf`):
- Application `TreatmentHandlerTests`: 38/38.
- Domain `VisitTreatmentTests`: 18/18.
- Integration `TreatmentEndpointTests`: 27/27. That is 23 methods. The TC sheet's "23/23" counts methods, which is consistent.

VERIFIED (no finding)
1. **HIGH 1 is closed for the adapter.** The HO-EMR-001 §1 Amended row (2026-10-13) covers all four parts:
   - (a) `IClinicalSkuReader` and `ClinicalSkuReader`.
   - (b) OQ-12 is re-answered: the controlled flag comes from `IControlledDrugDirectory`, as owner decision (B) says.
   - (c) The four consumer uses are recorded in CONTRACTS row 33, and owner decision (A) names all four ports.
   - (d) Whole `each` units, IMP-4 and IMP-7 as `Visit`, no ledger-entry ids, and returns to Quarantine. "One line is one
     batch" is replaced by (C).

   The row names all seven out-of-§13 files from the first pass: `IClinicalSkuReader.cs`, `DispensaryBatchPicker.cs`,
   `ClinicalSkuReader.cs`, the two `AddVisitTreatments` files, `ClinicalResponses.cs` and `docs/backlog/EMR.md`.
   The edits to `docs/handover/HO-EMR-001.md` and `CONTRACTS.md` (§14 "Inputs") are authorised by owner decision (A),
   which tells them to be recorded there. What is *not* closed is the new `IStockLedger` member (BLOCKER 1).
2. **Auto-split (owner decision C) is correct by reading, and the tests agree.**
   - `ClinicalUse` is permitted only at `ClinicDispensary` (`StockLocationRules`). So `PlanAsync(available)` covers the same
     (batch, location) candidates that the picker sums. The FEFO lines and the whole-dispensary total cannot disagree.
   - A named batch:
     - is refused when expired (BR-3) before the reason is read;
     - needs a reason when it is not the FEFO first (BR-4);
     - is drawn first, and then FEFO continues with that batch excluded.
     Unit and I-cases cover this.
   - An unnamed expired batch is never counted or planned, because the FEFO allocator applies the same expiry predicate.
     The ledger checks usability again at `DeductAsync`.
   - With `acceptShortfall`, everything available is deducted across the batches. The remainder goes on the last line,
     where `Quantity` = what was taken + the shortfall, so the aggregate's deducted + shortfall = quantity holds. There is one
     `RecordShortfall` for the total, on `lines[^1].Id`.
   - With nothing available there is one line with no batch.
   - The whole request is one `DeductAsync` with explicit lines, and its `ReferenceLineId` is `command.LineId`, the first line.
     The planner checks that the explicit lines sum to the demand and compares them with FEFO as a set (`SamePlan`). So a
     plain split is not flagged as an override, and a pinned draw is.
   - **Reversal per line is correct.** `ReverseTreatmentCommandHandler` returns the line's own `BatchId` and `DeductedQuantity`,
     which excludes the shortfall. `ReceiveReturnAsync` checks the batch against the *party* (`WasConsumedByPartyAsync`), not
     the reference line. So reversing line 2 of a split returns line 2's batch and quantity, even though its movement's
     reference line is line 1. No test reverses a non-first split line (LOW 5).
3. **Idempotency.**
   - The controller takes the client `lineId`, or mints one when it is null or `Guid.Empty`.
   - Later lines get deterministic ids from `MD5(first + index)`.
   - The replay check runs after the branch check and before the status check, so a retry after finalising still replays.
     A replay deducts nothing and the controller answers `200` with the same body.
   - The aggregate's duplicate guard now answers `TreatmentLineAlreadyRecorded`, which maps to 400.
   - Two concurrent first attempts with one id both pass the replay check. The visit row's `xmin` then refuses the second
     save (409), so stock is never deducted twice.
   - **The `201` contract change.** The trailing `lines[]` member and the `200` on replay are additive. No consumer exists:
     there are no C# callers and no `web/lib/api/dispensary.ts`. HO-EMR-002's treatments panel has not been built, so nothing
     breaks. The record is incomplete (LOW 6).
4. **ServiceDate.**
   - It is `BranchCalendar.LocalDateOf(finalisedOn, tz)`.
   - A missing branch throws instead of defaulting to UTC.
   - I-F8 opens the visit at 17:50 UTC (23:50 in Dhaka) and finalises it 20 minutes later. It asserts `Asia/Dhaka`, the exact
     next-day local date, and that the date differs from both the opened date and the UTC date.
5. **The item list.**
   - It is paged (`pageNumber`, and `pageSize` defaulting to 30 with a maximum of 100) and returns `PagedResult`.
   - It no longer injects `CatEcommerceDbContext`, so it does not read `StockPositions` or `StockLocationAggregate`.
   - Inventory is reached only through `IStockLedger`.
6. **SEC 48.** `ClinicalNoteRules.MaxTreatmentQuantity = 999_999_999.999`, which fits `numeric(12,3)`. The validator
   (`LessThanOrEqualTo`) and the aggregate apply it to every kind, procedures included. Domain and I-cases cover it.
7. **The double-reverse test is sound as an outcome test.**
   - It asserts exactly one 204, the loser as 400 or 409, one `Return` entry, and 4 units in Quarantine.
   - It has no gate, so the two requests may run one after the other. In that case it exercises `TreatmentAlreadyReversed`
     rather than the `xmin` path (LOW 8).
8. **No superseded architecture.** None of these appear: `IDispatcher`, a bus, Mongo, `QueuePublishOnly`, `202`, a rejection
   event or `CommandServices`.
   - `UpdateAsync(..., InMemoryPublishOnly)` is unchanged.
   - Each controller action still makes one dispatch.
   - The registration changes only the query's result type, and it is still Scoped.
   - No package was added, and no csproj was touched.

FINDINGS
- [BLOCKER] 1. Two HO-IMP-001 files were edited without a ratifying record in HO-IMP-001.
  - Sites:
    - src/Domain/CatEcommerce.Domain/DomainServices/IStockLedger.cs:75-82 (new default interface member
      `GetDispensarySkuIdsAsync`);
    - src/Infrastructure/CatEcommerce.Infrastructure/Inventory/EfStockLedger.Consumer.cs:330-341 (its implementation).
  - Rule: HO-EMR-001 §14 forbids "Any HO-IMP-001 or HO-SHP-001 file -- a mismatch is §20.2, not a patch". The first pass's
    MEDIUM 2 offered "HO-IMP-001's owner adds one", not "the consumer adds one".
  - What exists so far:
    - The HO-EMR-001 §1 row "counts it in scope".
    - CONTRACTS row 34 calls it "owner-directed", but it also leaves "HO-IMP-001's owner to confirm at its next amendment".
    - Row 34's owner column cites "HO-IMP-001 §8.8 (Amended 2026-10-13)". **No such amendment exists.** HO-IMP-001.md is
      unchanged in b12fd39d..04d8b118.
    - The owner's verbatim decision (A) names the adapter's four ports, not `IStockLedger`.
  - Severity: the same as the precedents. The PHM-001 `IStockLedger` and `SkuRows.Brand` edits and the SRG-001 `ConsentRules`
    overload were BLOCKERs, and a record alone closed them. Both earlier `IStockLedger` additions got a HO-IMP-001 §1 Amended
    row and a §8.8 listing (HO-IMP-001 rows 20-21).
  - The change itself is sound: additive, read-only, `AsNoTracking`, the same filter as the read it replaces, and only
    `EfStockLedger` is registered live (`InventoryModule.cs:48`). **No code change is needed.** See the IStockLedger ruling
    below for the exact row.
- [MEDIUM] 2. src/Application/CatEcommerce.Application/CommandHandlers/Visit/TreatmentHandlers.cs:86-91 (with
  `UniqueConstraintExceptionFilter`) — a client-supplied `lineId` that already exists on *another* visit answers 500.
  - The replay check only searches this visit. `PK_visit_treatment` is global on `Id`, so the save raises SQLSTATE 23505.
  - The filter rethrows unmapped constraints ("an unexpected 23505 is a bug"), so the vet gets a 500.
  - Nothing is written, and only a Vet can reach this. It is a new defect from accepting client ids.
  - Fix: map `PK_visit_treatment` to `400 TreatmentLineAlreadyRecorded` in the filter, or pre-check the id.
- [LOW] 3. TreatmentHandlers.cs:86-91 — a replay returns the original lines whatever the retried body says (another SKU or
  quantity). This is normal idempotency-key behaviour. Document it for HO-EMR-002: the dialog must mint a fresh `lineId` for
  each new treatment, not for each click.
- [LOW] 4. src/Read/CatEcommerce.Read/QueryHandlers/Visit/TreatmentQueryHandlers.cs:117-119,127-128 — three smaller points:
  - A ledger failure, including the default member's `NotSupported`, silently becomes an empty list.
  - The no-search path loads every dispensary SKU id and pages in memory. That is bounded by the catalogue and acceptable.
  - `totalCount` on a search saturates at 200, which backlog 46 discloses.
- [LOW] 5. Tests: no case reverses the *second* line of a split (the batch, the quantity, and a return referencing that
  line). It is correct by reading (VERIFIED 2).
- [LOW] 6. docs/handover/HO-EMR-001.md §12.4 (the rows at lines 963 and 965) still shows the old `201` body and an unpaged
  list. The §1 Amended row records paging and the `200` replay but not the trailing `lines[]` member. HO-EMR-002 builds
  from §12.4.
- [LOW] 7. TreatmentHandlers.cs:301,325 — two smaller points:
  - A named non-FEFO batch's reason is stored on its line even when the ledger computes no override. `SamePlan` is true when
    the draw takes every batch in full, so the ledger carries no flag or audit, while the line still carries a reason.
  - Every batch's movement references the first line's id (recorded in §1 (C)). A trace from ledger to line has to match by
    visit and batch, not by line id.
- [LOW] 8. Backlog accuracy:
  - Row 42 is marked "Resolved" through CONTRACTS row 34, which is not ratified yet (BLOCKER 1).
  - Row 52's "concurrent" double-reverse test does not force overlap (VERIFIED 7).

  Rows 41, 43-51 and 53-57 are accurate.

IStockLedger RULING (the question asked)
- **Yes, it needs an explicit ratification row, and it belongs in the owning handover.**
  - A row in the consumer's handover plus a CONTRACTS row that defers the owner's confirmation is not enough.
  - It is consistent with PHM-001: HO-IMP-001 row 21, owner decision A of 2026-10-09, extended §8.8.
  - It is consistent with ORD-001: HO-IMP-001 row 20, owner decision 2 of 2026-10-09; the 2026-10-10 "additive
    Billing/Inventory edits" were owner-confirmed in the owning handover and the CONTRACTS row.
  - The owner or the integrator may sign it. The SRG-001 `ConsentRules` precedent accepted an integrator decision, mirrored
    into the owner's handover.
- **Text for a new HO-IMP-001 §1 row** (Amended (HO-EMR-001 gate fix), 2026-10-13, signed by the OWNER or the INTEGRATOR):
  > Additive member on the published `IStockLedger` (§8.8), from the HO-EMR-001 Phase F gate fix (ARCH F MEDIUM 2, backlog
  > EMR 42). Nothing is renamed or removed.
  >
  > The member is `Task<StockResult<IReadOnlyList<Guid>>> GetDispensarySkuIdsAsync(Guid branchId, CancellationToken ct)`. It
  > returns the distinct SKUs with on-hand stock above zero at the branch's ACTIVE `ClinicDispensary` location. It is
  > read-only and enlists nothing. It is declared as a default interface member that answers `NotSupported`, so other
  > implementers (`FakeStockLedger` and test doubles) are unchanged. `EfStockLedger` implements it untracked over
  > `StockPositions` joined to `StockLocations`.
  >
  > This RATIFIES the edits made on `ho/EMR-001-F` (17aad6be) to `src/Domain/CatEcommerce.Domain/DomainServices/IStockLedger.cs`
  > and `src/Infrastructure/CatEcommerce.Infrastructure/Inventory/EfStockLedger.Consumer.cs`. Those are HO-IMP-001 files and
  > lie inside HO-EMR-001 §14. No other HO-IMP-001 edit is authorised.
  >
  > The consumer is HO-EMR-001, `GET /api/staff/dispensary/items`. The §8.8 listing gains the member. CONTRACTS row 34.
  >
  > The precedent is row 21 (HO-PHM-001 owner decision A, 2026-10-09).
- **Two edits to CONTRACTS row 34:**
  - The owner column becomes "HO-IMP-001 §8.8 (Amended 2026-10-13, ratified by <owner|integrator>)".
  - Delete "HO-IMP-001's owner to confirm at its next amendment".
- Optionally, add a line to the HO-EMR-001 §1 row saying the ratification is mirrored from HO-IMP-001 §1.
- Optional, and the owner's choice: make the member abstract, as rows 20-21 did, instead of a `NotSupported` default. A
  default member hides a missing implementation as an empty list (LOW 4). Whichever is chosen, the row should name it.

PROPOSED docs/backlog/EMR.md ROWS (the highest existing is 57; not written, per instruction)
| 58 | MEDIUM | A client `lineId` that already exists on another visit hits `PK_visit_treatment` (23505), which `UniqueConstraintExceptionFilter` rethrows as a 500. Map it to `400 TreatmentLineAlreadyRecorded` (or pre-check), with a test. | ARCH F re-check M2 |
| 59 | LOW | Three items. (a) Idempotent replay ignores body differences: document for HO-EMR-002 (a fresh `lineId` per new treatment). (b) §12.4 still shows the old `201` body and an unpaged list; add `lines[]`, the `200` replay and `PagedResult` to the §12.4 rows (or the §1 row). (c) The dispensary list turns a ledger failure, including the default member's `NotSupported`, into an empty list. | ARCH F re-check L3, L4, L6 |
| 60 | LOW | Three items. (a) Add a test that reverses the second line of a split (that line's batch and quantity, a return referencing that line). (b) Make the concurrent double-reverse test overlap for real (gate both requests after the load) so the `xmin` path is pinned. (c) A named batch's reason is stored on its line even when the ledger computes no override (the whole stock drawn). | ARCH F re-check L5, L7, L8 |
| 61 | Note | Every batch movement of a split references the first line's id (§1 (C)). Recall and ledger-to-line traces match by (visit, batch). Row 42's "Resolved" depends on the IStockLedger ratification row. | ARCH F re-check L7, L8 |

SCOPE CHECK
- files in allowed scope: 20/22. That is:
  - every file inside §13;
  - the per-area partials and `ClinicalNoteRules.cs`;
  - the TC sheet;
  - `docs/backlog/EMR.md`, authorised by §1 (a);
  - `docs/handover/HO-EMR-001.md` and `docs/handover/CONTRACTS.md`, which are §14 "Inputs" but authorised by owner decision
    (A).
- files outside allowed scope (2, both in HO-EMR-001 §14 "Any HO-IMP-001 file"; BLOCKER 1, closable by the ratification row
  above):
  - src/Domain/CatEcommerce.Domain/DomainServices/IStockLedger.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Inventory/EfStockLedger.Consumer.cs

Verdict: **FAIL** on BLOCKER 1 alone. It is a records gap and needs no code change. The first pass's HIGH 1 is closed. The
auto-split, idempotency, ServiceDate, paging and SEC 48 fixes are correct. After the HO-IMP-001 row and the row-34 correction
land, this re-check passes without another code review. The proposed rows 58-61 do not block.

---

## Combined re-check 2 (ARCHITECTURE + SECURITY + CODE_REVIEW) -- 2026-10-13

GATE: ARCHITECTURE (combined re-check 2, also covering SECURITY and CODE_REVIEW)
HANDOVER: HO-EMR-001 (Phase F)
COMMITS: 01c0f4a1 (range b3c13c6a..01c0f4a1, branch ho/EMR-001-F, 6 files)
DATE: 2026-10-13
STATUS: PASS

Tests run (targeted only, `catecommerce_emrf`, with the full connection string):
- Integration `TreatmentEndpointTests`: 29/29.
- Application `TreatmentHandlerTests`: 38/38.

VERIFIED
- **Re-check 1, BLOCKER 1: closed.**
  - HO-IMP-001 §1 has a new row, "Amended (HO-EMR-001 Phase F gate fix)", dated 2026-10-13 and ratified by the integrator.
    It contains the text this gate specified: the member signature, active `ClinicDispensary` with on-hand above zero,
    read-only, the default interface member answering `NotSupported`, and `EfStockLedger` as the implementation. It ratifies
    `IStockLedger.cs` and `EfStockLedger.Consumer.cs` from 17aad6be, says no other HO-IMP-001 edit is authorised, and cites
    the consumer, CONTRACTS row 34 and precedent row 21.
  - The §8.8 listing gains the member.
  - CONTRACTS row 34 no longer says "to confirm" and now reads "Amended 2026-10-13, ratified by integrator".
  - The HO-EMR-001 §1 row mirrors the ratification. The integrator signing it is within the SRG-001 `ConsentRules` precedent.
  - **Small mismatch (not a finding):** the §8.8 listing shows the member without its default body. The row's text says it
    is a default member, and the code agrees.
- **Re-check 1, MEDIUM 2: closed.**
  - `UniqueConstraintExceptionFilter` maps `PK_visit_treatment` to `400 TreatmentLineAlreadyRecorded`, keyed on the
    constraint name, like the other entries in that dictionary.
  - `ALineIdUsedOnAnotherVisit_Is400_NotA500` asserts the 400, the code, and that only one movement exists.
  - A side effect that is acceptable: if a concurrent duplicate *on the same visit* reaches the primary key before the visit's
    `xmin`, it now gets this 400 instead of a 409. Stock is still never deducted twice.
- **The LOWs.**
  - §12.4 now documents the `201` `lines[]` body, the `200` replay (the retried body is ignored, so send a fresh `lineId` per
    new treatment), the cross-visit 400, and the paged `{ items, pageNumber, pageSize, totalCount }` list.
  - `ReversingTheSecondLineOfASplit_ReturnsThatLinesBatchAndQuantity` checks that line's batch (`late`), its quantity (2),
    that the return references that line, and Quarantine stock of 2/0. It also checks that only that line is Reversed.
  - Backlog row 42 now rests on the HO-IMP-001 row. Row 52 is reopened honestly (the test does not force overlap).
  - Rows 58-61 match this gate's proposals, and their resolved/open states are accurate.
- **The test edits are sound.**
  - `DeductAsync` writes its rows `OrderBy(BatchId)` (`EfStockLedger.Consumer.cs`), so ledger order by position was never a
    contract. The two split tests now compare per batch and still assert the count, each quantity, and the override flag.
  - No product code changed except the filter mapping.
- **Scope.** All 6 files are in scope:
  - the filter is the established Consultation mapping table;
  - the tests;
  - `docs/backlog/EMR.md`;
  - `docs/handover/HO-EMR-001.md` and `CONTRACTS.md`, under owner decision (A);
  - `docs/handover/HO-IMP-001.md`, an Amended row only. This is the ratification this gate asked for, and HO-IMP-001 §14
    allows "Amended rows only".

RULING: a split's lines are shown in GUID order
- **Yes, it needs a backlog row. LOW, non-blocking.**
- Why it happens: `TreatmentRows.OfVisitAsync` orders by `RecordedOn` and then `Id`. All lines of one request share
  `RecordedOn`, and lines after the first get MD5-derived ids. So a split appears in effectively random but stable order.
  That is not the FEFO or draw order. The same order reaches:
  - the vet's treatments panel;
  - the discharge summary and its PDF;
  - `GetVisitChargeLinesQuery`.
- Why it is only LOW:
  - Each line shows its own lot, expiry and quantity, so nothing is misattributed.
  - Quantities, the stock and the billing are unaffected.
  - The `201` and `200` `lines[]` keep the draw order, because the result is built from the in-memory list.
- Why it still needs a row:
  - A vet reading "LATE 2, then EARLY 3" for one dose sees the opposite of what was drawn.
  - The read has no total ordering by intent. EMR already fixed the same class of defect for diagnostic result values with a
    `position` column (HO-EMR-001 §1, 2026-10-07 (5)).
- Fix, either way:
  - add a line sequence (for example `position`, the draw index) at the integrator's regeneration of `AddVisitTreatments`,
    and order by (`RecordedOn`, `position`, `Id`); or
  - without a migration, order by (`RecordedOn`, `BatchExpiresOn` nulls last, `Id`). This approximates FEFO but puts a
    pinned non-FEFO batch out of draw order.

PROPOSED docs/backlog/EMR.md ROW (the highest existing is 61; not written, per instruction)
| 62 | LOW | Lines of one split request share `RecordedOn`, so `TreatmentRows.OfVisitAsync` (`RecordedOn`, `Id`) shows them in GUID order, not draw/FEFO order. This affects the vet's treatments panel, the discharge summary and the charge lines. The `201`/`200` `lines[]` is in draw order. Add a line sequence (`position` = the draw index, at the `AddVisitTreatments` regeneration) and order by (`RecordedOn`, `position`, `Id`). Or, with no migration, order by (`RecordedOn`, `BatchExpiresOn` nulls last, `Id`). | ARCH F re-check 2 |

FINDINGS
- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/Visit/TreatmentQueryHandlers.cs:42 — a split's lines are shown in GUID
  order (ruling above; proposed row 62).

SCOPE CHECK
- files in allowed scope: 6/6
- files outside allowed scope: none

Verdict: **PASS**. There are no BLOCKER or HIGH findings. Re-check 1's BLOCKER and MEDIUM are closed, and the first pass's
HIGH 1 was already closed. Phase F, at 01c0f4a1, can go to the TEST gate. The open MEDIUM and LOW rows (43, 47, 52,
55, 56, 59c, 60b, 60c and the proposed 62) do not block.
