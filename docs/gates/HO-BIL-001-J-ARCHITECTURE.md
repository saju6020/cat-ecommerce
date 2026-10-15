GATE: ARCHITECTURE
HANDOVER: HO-BIL-001 (Phase J only -- automatic billing from visits, dispensing and surgery; §8.5, §8.13, §17 Phase J, §20.2)
COMMITS: 081197e6 10c74551 10faf707 f946b7af 663e05c5 (branch ho/BIL-001-J, range 5a187b6b..663e05c5, 56 files)
DATE: 2026-10-14
STATUS: FAIL

The gate fails on one BLOCKER and one HIGH. Both have small fixes:
- BLOCKER: revert one doc file.
- HIGH: add a guard to the appointment source and to the uncaptured list.

Owner gate policy (2026-09-27) applies: only BLOCKER and HIGH findings fail. MEDIUM and LOW findings are proposed
below as `docs/backlog/BIL.md` rows. This gate does not edit the backlog.

## What this pass reviewed

- **The diff.** `git diff 5a187b6b..663e05c5`: every source and test file, the migration and the snapshot. The
  `.Designer.cs` was checked only as far as its model matches the snapshot.
- **HO-BIL-001.** §1 (all Amended rows, including the new Phase J row), §6, §8.5, §8.13, §11.6, §13, §14, §17 and §20.
- **Other handovers.** The new §1 rows in HO-EMR-001, HO-PHM-001 and HO-SRG-001, and CONTRACTS rows 33 and 36–39.
- **`.ai/`.** architecture, cqrs, bounded-contexts (aggregate and cross-context rules), and adr.md (ADR-117, ADR-178,
  ADR-344..346).
- **The live trigger.** The current `clinic.visit_frozen`, `visit_child_frozen` and `diagnostic_order_guard` (migration
  `20261009055613_HardenClinicalAndBillingTriggers`) and the XCT `RecordClassification.Consultation` entries.
- **Precedent rulings, for consistent severities.** HO-ORD-001-ARCHITECTURE (the `.ai/flows` BLOCKER and the §14
  owner-decision scope note) and HO-EMR-001-F-ARCHITECTURE (§14 edits authorised by an owner decision).

The owner decisions of 2026-10-14 and the coordinator's architecture call on EMR backlog 43 are taken as stated in the
gate brief. They are recorded only in this branch's Amended rows and ADRs. The integrator confirms them at merge.

## Runtime check (targeted, as permitted)

- **Run.** `dotnet test tests/CatEcommerce.IntegrationTests --no-build --filter FullyQualifiedName~BillingPhaseJEndpointTests`.
  - Database: `catecommerce_bilj`, with the full connection string.
  - Binaries: built 01:47:29–37, after `663e05c5` was committed (01:47:12).
- **Result.** 15 passed, 0 failed.
- **What this confirms.** The event route works end to end through the real host: finalise, dispense, reverse and
  discharge each produce or void charges. The fresh-scope capture also sees the source's committed row. None of the
  source handlers opens an explicit transaction (`BeginTransaction`, `ITransactionalScope` and `RunAsync` were
  grepped in the Prescription and Surgery handlers).
- **What it does not cover.** Everything else is for the TEST gate.

## Rulings on the questions asked

### 1. The cross-area EMR edits: authorised, additive, recorded. No finding except LOW 6.

Each edit was checked against the owner decision that authorises it.

- **`VisitAggregate.CustomerId` (decision 1).**
  - It is set once, inside `Finalise`, the aggregate's own mutation method. `EnsureOpen()` runs first and
    `UpdateVersion()` is called.
  - The invariant ("no owner, no finalise", `CatNotFound`) is in the aggregate, not the handler.
  - The setter is private.
  - `FinaliseVisitCommandHandler` only moves the existing cat read above the `Finalise` call and passes `cat?.OwnerId`.
    It still mutates one aggregate and saves with `InMemoryPublishOnly`. The outbox enqueue was already there.
- **`VisitFinalisedEvent.CustomerId` (decision 1).**
  - It is a new `required` member, so the change is additive. The only producer is `Finalise`.
  - The event is still past tense and carries ids only.
- **`IVisitBillingReader` (the coordinator's call).**
  - It is a Domain port (`DomainServices/IVisitBillingReader.cs`). Its model is in `Domain/Models`. It is implemented
    in `Infrastructure/Consultation`, EMR's folder.
  - It is registered Scoped in `ConsultationModule`, the owning module.
  - It returns typed values. Each read is `AsNoTracking()` and is projected with `Select` before it is materialised.
    No aggregate is rehydrated.
  - The Billing source (`VisitBillableItemSource`) reads the visit only through this port. Every Billing-side read of
    the capture path is untracked and projected.
- **Diagnostic price and tax class on `DiagnosticOrderAggregate` (decision 2), and the five settings.**
  - Four nullable properties with private setters, set only in `Order(...)`. The tax class is nulled when there is no
    price.
  - The DTO grows by two optional trailing parameters. `OrderDiagnosticCommandHandler` reads the settings and
    `IBillingPricingInputs.GetServiceTaxClassIdAsync`. These are reads only, and the handler still mutates one
    aggregate.
  - The five settings are appended to the Consultation partials. `DiagnosticPricing.SettingKeyFor` is exhaustive.
  - The tax-class choice (the visit's service class) goes beyond the literal decision ("add a price"). It is flagged
    as an owner question in the builder's backlog row 69, which is acceptable.
- **Recorded append-only.**
  - HO-EMR-001 §1 gains one row, "Amended (HO-BIL-001 Phase J)", which covers all four edits. Diff: +1 line, 0
    deletions.
  - CONTRACTS gains rows 36–38. EMR backlog 43 is closed in place, as backlog convention allows.

### 2. Is an owner decision plus a coordinator call sufficient authority to narrow ADR-117 (by ADR-344)? Yes.

- **What ADR-117 says.** Its customer clause ("the visit holds no customer id") is one clause of a context-placement
  ADR.
- **The owner's decision contradicts that clause directly.** Decision 1 says the CustomerId "is stored on the visit
  at finalising". The owner is the authority above every ADR. Precedent is the same: HO-ORD-001's gate accepted
  "owner directed: amend ADR-179" as an appended note.
- **The narrowing is drawn tightly.**
  - An open visit still holds no customer id.
  - Owner-facing reads still resolve the current owner.
  - The id is for billing only.
- **ADR-117 itself is not edited**, so append-only holds.
- **One condition.** The coordinator owns `.ai/` and ADR numbering. ADR-344..346 lie outside this handover's §9
  range (ADR-177…196), so the integrator must ratify them at merge. LOW 5 covers the numbering paperwork.

### 3. Billing reading PHM and SRG rows directly: read-only and untracked. No finding.

The reads in each source:
- **`DispenseBillableItemSource`.** `db.Dispensings.AsNoTracking()` with `Select` and with `SelectMany(Lines)` plus
  `Select`.
- **`SurgeryBillableItemSource`.** `db.Surgeries.AsNoTracking()` with `Select`, and
  `db.AnaesthesiaLogEntries.AsNoTracking()` with `Select` into a private record.
- **`BillableSourceSupport`.** Branch zone and merge chain, both `AsNoTracking()` with `Select`.
- **`BillingPricingInputs.GetSkuTaxClassIdsAsync`.** Shop's published `SkuRows()`, which is `AsNoTracking()` at its
  root.
- **`EfBillingFinder.FindChargeIdsBySourceAsync`.** `AsNoTracking()` with `Select`.

Across all of them: no `SaveChanges`, no `Attach`, no repository and no aggregate is materialised. This follows §8.5
("reading the source area's own table") and the appointment-source precedent.

Two caveats:
- Re-implementing SRG's correction-chain rule in Billing is a duplicated domain rule. The builder already raises it as
  row 74 (LOW). This gate agrees and adds nothing.
- The uncaptured list also reads `clinic.*`. See MEDIUM 2.

### 4. `DispensingReversedEvent` voiding uninvoiced charges: structurally in scope; the behaviour needs owner sign-off. MEDIUM 1.

- **Structure: compliant.**
  - The handler is in `DomainEventHandlers/Billing/**`, a §13 path. It is registered as
    `IEventHandlerAsync<DispensingReversedEvent>`.
  - It runs in a fresh scope and dispatches Billing's own `VoidChargeCommand` through `IInMemoryDispatcher`, with
    `ValidateCommand = true`. That is one aggregate per command, one command per charge.
  - It swallows errors and logs ids only. It never enqueues and never calls `SaveChangesAsync`.
  - Billing's invariant `ChargeAlreadyInvoiced` is respected (it is checked in the handler before `Void`).
- **Behaviour: not specified anywhere.**
  - Neither §8.13 nor any owner decision of 2026-10-14 asks for it.
  - It is recorded only as the builder's own ADR-346 (4) and the BIL Amended row (4). Unlike rows 67–69, it is not
    flagged as an owner question.
- **Why it is not a BLOCKER.**
  - Without it, a reversed supply stays billed, which is an incorrect bill.
  - It goes through Billing's own published command, so it cannot break an invariant.
  - §20.2's remedy ("an Amended row here") was followed.
  - It is still new automatic behaviour that changes money records, done as the reversing staff member (the void's
    actor is `@event.UserContext`). So the owner must ratify it, or the handler is removed and the reversal handled by
    hand.

### 5. Handler shape and patterns: compliant. No finding.

- **The four new handlers.** They are thin. `SourceChargeCapture.RunAsync` and the void handler each:
  - `CreateAsyncScope()`;
  - resolve `IInMemoryDispatcher` (not `IDispatcher`);
  - send one command with `ValidateCommand = true`;
  - catch every exception and log the exception type and ids only (no message, no payload);
  - never enqueue.
- **Registrations.** Each handler is registered explicitly against the event's concrete runtime type
  (`BillingModule.cs:212-215`). The three item sources are registered as `IBillableItemSource` and resolved by kind.
  `VisitBillingReader` is registered in `ConsultationModule`. All are Scoped. Nothing on the never-register list was
  added, and `Program.cs` is untouched.
- **One aggregate per command.**
  - `FinaliseVisitCommand`: the visit only.
  - `OrderDiagnosticCommand`: the order only. The visit is only read.
  - `VoidChargeCommand`: one charge.
  - The capture path (`CaptureSourceChargesCommand`): it records N independent `ChargeAggregate`s with one save each,
    idempotent on `ix_charge_source`. That is the shape §8.5 and ADR-178 specify ("one ChargeAggregate per item ... one
    SaveAsync per new charge"), approved in Phase B and unchanged here. The only Phase J change to the handler seeds the
    skipped list from `BillableSourceResult.Skipped` (`ChargeCommandHandlers.cs:162`). No second aggregate kind is
    touched.
- **Superseded architecture.** None is in the diff. Grepped added lines found no:
  - `IDispatcher`, `IBusMessageDispatcher`, `IEventPublisher`, `SendBusMessageAsync`, `PublishBusMessageAsync`;
  - MassTransit, RabbitMQ, Mongo or Bson;
  - `202`, `QueuePublishOnly`, `INotifiedByEventHandler`;
  - `SaveChangesAsync`, `.Enqueue(` in a handler, or `CommandServices`;
  - `HasMany`, or a repository returning `IQueryable<`.
- **Dependencies.** No csproj, sln, NuGet.Config or global.json change, and no new package.
- **Cross-module event handler.** The checklist's "a domain event handler calling another module" rule is overridden
  for this case by the approved ADR-178 pull path. That path is a Billing handler on another area's event, in a fresh
  scope, dispatching Billing's own command. Phase J reuses it unchanged.

### 6. The migration: additive and safe. The guard is not weakened. LOW 3 and LOW 4.

- **What `AddVisitBillingFacts` adds.** Four nullable columns (the Amended rows say three):
  - `clinic.visit.customer_id uuid`;
  - `clinic.diagnostic_order.unit_price_amount numeric(12,2)`;
  - `clinic.diagnostic_order.unit_price_currency_code char(3)`;
  - `clinic.diagnostic_order.tax_class_id uuid`.
- **The rest of the migration.** No new table, no data update, no hand-written SQL. `Down` drops the four columns.
  The snapshot matches. Money follows HO-ADM-001's `numeric(12,2)` and `char(3)`, as F33 and ADR-194 require.
- **XCT.** The classification is table-level (`clinic.visit` and `clinic.diagnostic_order` are already `Clinical` /
  `Forbidden`), so nothing is owed.
- **The frozen-visit trigger is not changed by this branch.** The brief's premise is wrong here: the diff touches no
  trigger. The claim "the frozen-visit trigger allows the finalising UPDATE" describes the existing
  `clinic.visit_frozen()`, which refuses an UPDATE when `OLD.status = Finalised`.
  - The finalising UPDATE has `OLD.status = Open`, so `customer_id` is written in that same statement.
  - Every later UPDATE, including one to `customer_id`, is refused with `CR001`.
  - DELETE and the soft-delete flag stay refused.
  - The guard is exactly as strong as before. "Set once, never changes" is enforced by the database after
    finalisation.
  - Before finalisation, nothing in the database stops `customer_id` being set on an open visit. Only the aggregate
    stops it.
- **The diagnostic order has the opposite gap.** Its "frozen" price is protected only by the aggregate:
  `diagnostic_order_guard` freezes identity and the soft-delete flag, not the new price and tax-class columns. That is
  LOW 4, a hardening suggestion.

### 7. ADR-344..346: no collision.

- **Where they were searched.** All 70 local branches and master: ADR-34x numbers in `.ai/decisions/adr.md` on each
  branch, and `git grep "ADR-34[4-6]"` across master.
- **Result.** ADR-344, ADR-345 and ADR-346 exist only on `ho/BIL-001-J`. Every other branch and master stop at ADR-342.
- **ADR-343 is skipped.** `docs/handover/README.md:107` names ADR-343 as the next free number, and no branch uses it.
  That is LOW 5.
- **How they were added.** They are appended after the HO-RPT-001 section, so append-only holds.

### 8. CONTRACTS rows 36–39: present, additive, mostly accurate. MEDIUM 2 and LOW 6.

- **Row 36 (the port).** It matches the code, with one exception: its claim "Billing ... never reads `clinic.*`" is
  contradicted by the uncaptured list (MEDIUM 2). ADR-346 (1) says the same.
- **Row 37 (event field and column).** Accurate.
- **Row 38 (diagnostic prices).** Accurate. It does not name EMR's new consumer use of
  `IBillingPricingInputs.GetServiceTaxClassIdAsync`. Row 33 lists only `GetServicePriceAsync` (LOW 6).
- **Row 39 (Billing as consumer).** Accurate on events, `ISkuPriceReader`, `SkuRows` and the reversal. It does not
  mention the account and branch reads (merge chain, zone), which follow the appointment-source precedent, or the
  Read-side uncaptured list over PHM, SRG and clinic tables (LOW 6).

## FINDINGS

- [BLOCKER] .ai/flows/clinic-visit-flow.md:55-56 — §14 forbids `.ai/**` except `decisions/adr.md`.
  - The branch rewrites the "Cross-module rules" bullet and adds an "Updated 2026-10-14" paragraph.
  - Same ruling as HO-ORD-001-ARCHITECTURE ("No build branch has ever edited `.ai/flows/`"). The opposite precedent
    is HO-BIL-001's own deviation (r) of the first build ("No `.ai/flows` updates").
  - No owner decision of 2026-10-14 and nothing in the coordinator's call covers `.ai/flows`.
  - Remedy: revert the file to master's content. The coordinator updates the flow at or after the merge. The text can
    be kept in the Amended row or a backlog row for that.

- [HIGH] src/Infrastructure/CatEcommerce.Infrastructure/Billing/ItemSources/AppointmentBillableItemSource.cs:36 and src/Read/CatEcommerce.Read/QueryHandlers/Billing/BillingChargeQueryHandlers.cs:96-101 — one fee is billable from two sources. This breaks the rule that each charge has exactly one owning source (§8.5: "the appointment fee is charged here and only here"; OQ-14). The §20.2 contract record that justifies the design is false.
  - **The claim.** The BIL Amended row (4) and the `SurgeryBillableItemSource` summary say a surgery's appointment
    "never completes by itself, so the appointment source never bills it and there is no double charge".
  - **What the code does.** `SurgeryProcedureCommandHandlers.cs:55` calls `AppointmentAggregate.CompleteSurgerySlot`
    (`AppointmentAggregate.cs:494-505`), which sets `Status = Completed` and `CompletedOn`. It raises no
    `AppointmentCompletedEvent`, so nothing bills it automatically. But:
    - `GetUncapturedSourcesQuery`'s appointment arm lists every `Completed` appointment with no Appointment charge,
      with no `SurgeryId == null` filter. So every ended surgery's appointment appears on the staff "uncaptured" list.
    - `AppointmentBillableItemSource` has no `SurgeryId` guard. So `POST .../sources/Appointment/{id}/capture` bills
      the appointment's `ServiceSnapshot` fee as well as the Surgery source's `procedure` line, which is a double
      charge the list invites.
  - **This gate verified it independently.** It concurs with CODE_REVIEW's HIGH on the same lines.
  - **Fix.**
    - The appointment source returns `NotBillable("AppointmentManagedBySurgery")` when `SurgeryId != null`.
    - Add `a.SurgeryId == null` to the uncaptured query's appointment arm.
    - Pin both in J7.
    - Correct the Amended row (4) and ADR-346 to say the surgery source owns the surgery fee and the appointment source
      refuses surgery appointments.

- [MEDIUM 1] src/Application/CatEcommerce.Application/DomainEventHandlers/Billing/CaptureChargesOnPhaseJSources.cs:47-78 — `VoidChargesOnDispensingReversedHandler` is unrequested behaviour: §8.13 and the three owner decisions do not cover it.
  - Architecturally it is compliant (ruling 4).
  - It automatically voids money records as the reversing pharmacy user, on the builder's own ADR-346 (4).
  - Needs owner ratification at merge: keep it, or remove it and void by hand. Proposed row A1.

- [MEDIUM 2] src/Read/CatEcommerce.Read/QueryHandlers/Billing/BillingChargeQueryHandlers.cs:106-119 (the visit arm of `GetUncapturedSourcesQueryHandler`) — ADR-346 (1) and CONTRACTS row 36 say Billing "never reads `clinic.*`", but this query reads them and re-implements the billability rule.
  - What it reads: `clinic.visit`, `visit_treatment` and `clinic.diagnostic_order`.
  - What it duplicates: the "billable, not reversed; diagnostic not cancelled and priced" rule that `VisitBillingReader`
    owns. The two predicates agree today only because the aggregate refuses a billable treatment without a price
    (`VisitAggregate.cs:327`).
  - A cross-context list query in the Read layer is endorsed by `.ai/bounded-contexts.md` (FR-IMP-11 row), so this is
    not a layer breach. But the record written by this same change contradicts the code.
  - Fix one or the other: narrow ADR-346 (1) and row 36 to "the capture path", or add a count/exists member to the
    EMR port and use it. Proposed row A2.

- [MEDIUM 3] docs/handover/HO-PHM-001.md:22, docs/handover/HO-SRG-001.md:22 — these are §14 "Inputs" edits that the owner decisions do not cover.
  - HO-BIL-001 §1 itself says "this author may not edit other handover files; the coordinator appends".
  - Both rows are append-only "consumer note, no change to this area's files", and they follow the established mirror
    convention. The SRG row also declares SRG's build row item (4) superseded.
  - Not counted as a violation, on the HO-ORD-001 precedent: they are §20.2 contract-difference records, additive and
    informational.
  - The coordinator must ratify or rewrite them at merge. If the coordinator does not, they are §14 BLOCKERs.
    Proposed row A3.

- [MEDIUM 4] src/Read/CatEcommerce.Read/QueryHandlers/Billing/BillingChargeQueryHandlers.cs:131-138 (surgery arm) — a discharged surgery is listed as "uncaptured" whenever it has no charge, even when it has nothing to bill.
  - Example: an abandoned procedure with no drugs, or with only `SkuNotPriced` drugs. Capture then records 0 items,
    so the row stays on the list permanently.
  - The visit and dispensing arms filter on "has something billable". The surgery arm does not.
  - For CODE_REVIEW and TEST to confirm. Proposed row A4.

- [LOW 1] tests/CatEcommerce.Infrastructure.Tests/Billing/VisitBillingReaderTests.cs — outside §13's literal list.
  - §13 lists Domain.Tests, Application.Tests and IntegrationTests only.
  - The first build had the same deviation (three Infrastructure.Tests files, accepted).
  - Not named in the Phase J Amended row. Proposed row A5.

- [LOW 2] docs/handover/HO-BIL-001.md:22 — the Phase J Amended row does not list the out-of-§13 and §14 files by name, as the 2026-10-07 row and the EMR-F precedent did.
  - It names the EMR edits by type, not by path.
  - Missing: `VisitBillableLineKindMap.cs`, `VisitBillableLineKind.cs`, `DiagnosticPricing.cs`,
    `VisitBillingReaderTests.cs` and the `.ai/flows` edit.
  - Proposed row A5.

- [LOW 3] docs/handover/HO-BIL-001.md:22 (item 6), docs/handover/HO-EMR-001.md:23 (item 4) — "three nullable columns". The migration adds four (`customer_id`, `unit_price_amount`, `unit_price_currency_code`, `tax_class_id`). Proposed row A5.

- [LOW 4] `clinic.diagnostic_order_guard` (migration 20261009055613) — ADR-345's "frozen on the order" price and tax class are protected only by the aggregate.
  - The DB guard freezes identity and the soft-delete flag, not the three new columns.
  - Hardening: add them to the guard's `IS DISTINCT FROM` list. That is a migration, so the integrator does it.
    Proposed row A6.

- [LOW 5] .ai/decisions/adr.md:4566-4590 — ADR-344..346 are outside HO-BIL-001 §9's range (ADR-177…196, with 195 and 196 still free).
  - `docs/handover/README.md:107` still says "next new area starts at ADR-343", and ADR-343 is skipped.
  - There is no collision (ruling 7).
  - ADR-117's Status line carries no "narrowed by ADR-344" pointer. Append-only forbids the branch adding one.
  - The coordinator should record the assignment, bump the README pointer to 347, and add the pointer note if wanted.
    Proposed row A7.

- [LOW 6] docs/handover/CONTRACTS.md rows 33, 38, 39 — the consumer lists are incomplete.
  - EMR now also consumes `IBillingPricingInputs.GetServiceTaxClassIdAsync` (`DiagnosticHandlers.cs:134`).
  - Row 39 omits Billing's untracked reads of `account.customer_account` (merge chain) and the branch zone, and the
    Read-side uncaptured list over PHM, SRG and clinic tables.
  - Proposed row A7.

- [Note] `.ai/templates/*.cs` is still stale (standing defect, BIL row 33). Nothing in this diff copies from it.

## Proposed docs/backlog/BIL.md rows

The numbers are provisional. The highest existing row is 76. SECURITY proposed rows 77–81 and CODE_REVIEW proposed
77–79, so those collide; the integrator assigns final numbers. Suggested 82 onward:

| # | Severity | Item | Source |
|---|---|---|---|
| A1 (82) | MEDIUM — Owner decision | **Dispensing reversal voids charges automatically.** `VoidChargesOnDispensingReversedHandler` voids a reversed supply's uninvoiced charges as the reversing user (ADR-346 (4)). §8.13 and the 2026-10-14 decisions do not ask for it. Ratify it, or remove it and have staff void by hand. | ARCH J M1 |
| A2 (83) | MEDIUM | **Uncaptured list reads `clinic.*` and duplicates the billability rule.** ADR-346 (1) and CONTRACTS row 36 say Billing never reads `clinic.*`. `GetUncapturedSourcesQueryHandler`'s visit arm does. Narrow the ADR and row wording to the capture path, or add a typed exists/count member to `IVisitBillingReader` and use it. | ARCH J M2 |
| A3 (84) | MEDIUM — Coordinator | **Ratify the HO-PHM-001 and HO-SRG-001 consumer-note rows** written by the branch (§14 "Inputs"; HO-BIL-001 §1 says the coordinator appends). This includes the SRG row's "item (4) superseded". | ARCH J M3 |
| A4 (85) | MEDIUM | **Surgery arm of the uncaptured list has no "something to bill" filter.** A discharged surgery with no billable item (abandoned with no drugs, or only unpriced drugs) stays listed forever. | ARCH J M4 |
| A5 (86) | LOW | **Phase J Amended row paperwork.** List the out-of-§13 and §14 files by path (`VisitBillableLineKindMap.cs`, `VisitBillableLineKind.cs`, `DiagnosticPricing.cs`, `tests/CatEcommerce.Infrastructure.Tests/Billing/VisitBillingReaderTests.cs`, and the EMR file set). Correct "three nullable columns" to four in the BIL and EMR rows. | ARCH J L1, L2, L3 |
| A6 (87) | LOW — Integrator | **DB-guard the frozen diagnostic price.** Add `unit_price_amount`, `unit_price_currency_code` and `tax_class_id` to `clinic.diagnostic_order_guard`. This is a migration, so the integrator does it. | ARCH J L4 |
| A7 (88) | LOW — Coordinator | **ADR and contract bookkeeping.** Record the assignment of ADR-344..346, and bump `docs/handover/README.md`'s next ADR to 347 (ADR-343 is skipped). Optionally add a "narrowed by ADR-344" note under ADR-117. Add EMR's use of `GetServiceTaxClassIdAsync` to CONTRACTS row 33 or 38. Add the account/branch and Read-side reads to row 39. | ARCH J L5, L6 |

## SCOPE CHECK

- **Files in allowed scope: 52/56.** That is:
  - **28 within §13 or established Billing conventions.**
    - the TC sheet and `.ai/decisions/adr.md` (appended);
    - `ChargeCommandHandlers.cs` and the two `DomainEventHandlers/Billing` files;
    - `IBillableItemSource.cs` and `IBillingPricingInputs.cs`, plus `IBillingFinder.cs` (accepted in the first build);
    - the new `Models/VisitBillingModels.cs`;
    - six `Infrastructure/Billing/**` files;
    - `Read/QueryHandlers/Billing`;
    - `BillingModule.cs` (an ADR-341 partial);
    - ten files under `tests/{Domain,Application,Integration}Tests`;
    - `docs/backlog/BIL.md`, by gate convention.
  - **24 §14 or out-of-§13 files changed under the recorded owner decisions of 2026-10-14 and the coordinator's call.**
    Accepted on the HO-ORD-001 and HO-EMR-001-F precedent: all are additive, and all are recorded in HO-EMR-001 §1 and
    CONTRACTS rows 36–38.
    - `VisitAggregate.cs`, `DiagnosticOrderAggregate.cs`, `VisitEvents.cs`, `VisitDtos.cs`, `DiagnosticDtos.cs`;
    - `ConsultationNoteHandlers.cs`, `DiagnosticHandlers.cs`;
    - `VisitConfiguration.cs`, `DiagnosticOrderConfiguration.cs`;
    - `AddVisitBillingFacts` (.cs and .Designer.cs) and the model snapshot (a tenth migration; §12.1 says Phase J adds
      none; the integrator regenerates it);
    - `IVisitBillingReader.cs`, `VisitBillableLineKindMap.cs`, `Infrastructure/Consultation/VisitBillingReader.cs`,
      `ConsultationModule.cs`;
    - `SettingDefinitions.Consultation.cs`, `SettingKeys.Consultation.cs`, `DiagnosticPricing.cs`,
      `VisitBillableLineKind.cs`;
    - `docs/backlog/EMR.md`;
    - `docs/handover/HO-EMR-001.md`, `docs/handover/CONTRACTS.md`, `docs/handover/HO-BIL-001.md`.
  - **This acceptance rests on the owner authorisation**, which is evidenced only by the branch's Amended rows, the
    ADRs and the gate brief. The integrator confirms it at merge.
- **Files outside allowed scope: 4.**
  - `.ai/flows/clinic-visit-flow.md`: §14, BLOCKER.
  - `docs/handover/HO-PHM-001.md` and `docs/handover/HO-SRG-001.md`: §14, MEDIUM 3. Pending coordinator ratification;
    a BLOCKER if it is refused.
  - `tests/CatEcommerce.Infrastructure.Tests/Billing/VisitBillingReaderTests.cs`: outside §13, LOW 1.
- **New NuGet or npm dependencies: none.** No csproj, sln, NuGet.Config, global.json or `Program.cs` change.

---

## Combined re-check (architecture, security, code review) -- 2026-10-14

GATE: ARCHITECTURE (combined re-check: ARCHITECTURE + SECURITY + CODE_REVIEW findings of Phase J)
HANDOVER: HO-BIL-001 (Phase J gate fix round)
COMMITS: 2409bedc 3f57bb5d (fix range 663e05c5..3f57bb5d; the gate-record commits 21894ec9, 7eed9141, 4118ebb0 skipped)
DATE: 2026-10-14
STATUS: PASS

No BLOCKER or HIGH remains. Owner gate policy (2026-09-27): MEDIUM and LOW go to `docs/backlog/BIL.md`. This gate does
not edit the backlog. Rows 95-97 are proposed below.

**Runtime.** No tests were run. `CATECOMMERCE_TEST_DB` is not set here, and the brief allows a static review in that
case. The IntegrationTests binaries (13:39:04 +0600) predate commit 3f57bb5d (13:42:11), so the earlier run cannot be
shown to cover the final test file. **For the TEST gate:** J7 (surgery appointment refused and not listed), J4c, J4d,
J2c, J3b, J1c, J7d, J7e and `SurgeryAggregateTests.Discharge_StoresTheOwnerAtDischarge_AndCarriesItOnTheEvent`.

**Owner decisions of 2026-10-14**, as given in the brief: reversal void "Keep it", surgery payer "Owner at discharge",
abandoned surgery "Drugs only", old visits "Bill by hand". The coordinator ratified the HO-PHM-001 and HO-SRG-001
consumer notes (former MEDIUM 3). These are evidenced by the brief and the appended Amended rows, and the integrator
confirms them at merge.

### Verification of the items asked

1. **The BLOCKER is closed.** `git diff 5a187b6b..3f57bb5d -- .ai/flows` is empty, so the file is byte-identical to
   the base. The flow text is carried in the gate-fix Amended row, item (7), for the coordinator.
   **The HIGH is closed.**
   - `AppointmentBillableItemSource` projects `SurgeryId` and returns `NotBillable("AppointmentManagedBySurgery")`
     before the status check. That covers the event route and the manual route.
   - The uncaptured list's appointment arm has `a.SurgeryId == null`.
   - J7 pins three things: the surgery appointment is absent from `/uncaptured-sources`, a manual Appointment capture
     answers `captured = 0` with skip reason `AppointmentManagedBySurgery`, and there are no Appointment charges.
   - `SurgeryId` is mapped (`AppointmentConfiguration.cs:73`).
   - The corrections are append-only. A new HO-BIL-001 §1 row "Amended (Phase J gate fix)" corrects item (4) of the
     Phase J row, which is left as written. A new "ADR-344 … ADR-346 — revisions" section is appended after ADR-346.
     The new HO-EMR-001 and HO-SRG-001 rows add +1 line each and delete nothing.
   - CONTRACTS rows 36, 38 and 39 are this branch's own unmerged rows, rewritten in place. Row 33 (master) gains only
     an additive clause. That is the CONTRACTS in-place annotation convention (see row 1), so there is no finding.
2. **Partial capture is closed for the visit and dispensing arms, and partly for the surgery arm.**
   - Each arm now lists a source while its billable-line count is greater than its count of charges (any status).
   - The visit arm's predicates match `VisitBillingReader`. Its treatment arm omits the reader's
     `UnitPriceAmount != null`, which is safe because the aggregate refuses an unpriced billable treatment.
   - `SourceChargeCapture` logs `{SourceKind} {SourceId}`, the captured and skipped counts, and the distinct reason
     codes only. It logs no description, message or customer data.
   - J4d pins a visit whose diagnostic is skipped `TaxClassNotFound`: it stays listed, and the retry reports the reason.
   - The surgery arm under-counts corrected drug entries. See MEDIUM R1.
3. **The owner-at-discharge build is correct and additive.**
   - `SurgeryAggregate.OwnerAtDischargeId` has a private setter. It is set only in `Discharge`, inside the existing
     mutation, before `UpdateVersion()`.
   - `DischargeSurgeryCommandHandler` passes `cat?.OwnerId`, using the cat it already read. It still mutates one
     aggregate.
   - `SurgeryDischargedEvent.OwnerAtDischargeId` is an optional `Guid?`, not `required`. `SurgeryDischargeDto` gains
     a trailing optional parameter, and all 10 call sites still compile against it. Column: `owner_at_discharge_id`,
     uuid, nullable. The snapshot and the Designer match.
   - `SurgeryBillableItemSource` bills `OwnerAtDischargeId` through `BillableSourceSupport.BillableCustomerAsync`,
     which follows the merge chain. With no id it returns `NotBillable("SurgeryCustomerNotRecorded")` and does not fall
     back to the booking customer. The list's surgery arm also requires `OwnerAtDischargeId != null` and shows that
     customer.
   - The HO-SRG-001 Amended row and CONTRACTS row 40 exist and name the files.
   - **XCT is unaffected.** `clinic.surgery` is already classified at table level (`RecordClassification.Surgery.cs`,
     Clinical/Forbidden). No column-level registry names customer columns, and the erasure exemption already covers ids.
   - **The SRG triggers allow the discharge UPDATE, and nothing is weakened.** `clinic.surgery` carries only the
     ADR-299 retention pair (BEFORE DELETE / TRUNCATE, `AttachRetentionGuardsToSurgeryTables`). It has no UPDATE
     trigger, and no trigger was touched. The discharge UPDATE therefore passes. The trade-off is that the column is
     not database-frozen after discharge. That matches every other surgery column (LOW R3).
   - The SRG source edits fall in §14 ("surgery aggregates or tables"). They are authorised by the owner decision,
     additive, and recorded in HO-SRG-001 §1, on the HO-EMR-001-F and Phase J precedent.
4. **Narrowing ADR-346 (1) and CONTRACTS 36, rather than adding a port member: acceptable.**
   - It was one of the two remedies offered for former MEDIUM 2.
   - A cross-context list query in the Read layer is endorsed by `.ai/bounded-contexts.md`.
   - The capture path still reads a visit only through `IVisitBillingReader`.
   - The narrowing is appended (the ADR revision) and was rewritten in place on the branch's own row 36.
   - The cost is a Read-side duplicate of the billability rules in three arms. That is honest only if each twin matches
     its source, and the surgery twin does not (MEDIUM R1).
5. **Spot checks.**
   - **Surgery-arm filter.** "Completed ? 1 : 0" plus the uncorrected drug draws must exceed the charges. An abandoned
     surgery with no drugs is no longer listed. For the shortfall, see R1.
   - **Branch-first 404 (J4c).** Every source sets `InBranch(...)` on every non-NotFound result: NotBillable, Billable
     and each reason. `CaptureSourceChargesCommandHandler` compares `result.BranchId` with the scoped branch before
     `IsBillable`. The items check remains as a fallback.
   - **`diagnostic_order_guard`.** The Up body is the 20261009055613 body word for word, plus one new
     `IS DISTINCT FROM` block over `unit_price_amount`, `unit_price_currency_code` and `tax_class_id` with the same
     `CR001`. No later migration redefines the function. Down restores the original body. Rows priced before the
     change (null to null) are unaffected.
   - **J3b.** The `TaskCompletionSource` barrier releases the three `Task.Run` posts together. The dead sign-in and
     the conditional assertion are gone, and an unconditional repeat capture then asserts 4 distinct line refs.
   - **J2c.** A merges into B, which merges into C. All four charges go to C and none to B.
   - **J1c and J7e.** The fake clock starts at `UtcNow.Date + 20:00 UTC`, and the surgery helpers do not advance it.
     The asserted service date is the UTC date + 1 (Asia/Dhaka, UTC+6), which pins the zone.
6. **Migration `20261014033110_AddVisitBillingFacts`: safe.**
   - It replaces `20261013192812`, which is deleted, so only one copy exists.
   - Five nullable columns, with no default and no back-fill: `clinic.visit.customer_id`,
     `clinic.surgery.owner_at_discharge_id` and `clinic.diagnostic_order.{tax_class_id, unit_price_amount
     numeric(12,2), unit_price_currency_code char(3)}`.
   - Plus `CREATE OR REPLACE` of the guard function. The trigger binding is unchanged.
   - No table, no index, no data UPDATE. Down drops the five columns and restores the original guard.
   - The integrator regenerates it at merge, as before.
7. **No new BLOCKER or HIGH. Backlog rows 77-94 are honest, with two qualifications.**
   - **Row 83** says the arm counts an "uncorrected drug draw". That is accurate, but it does not say that a corrected
     drug, which the source does bill, is not counted (R1). The ADR revision's "with the same rules" is therefore not
     true for the surgery arm.
   - **Row 92** records the README bump that this round made in a §14 file (R2).
   - **Row 87**, the dispensing capture/reversal race, is correctly left Open, LOW, with the manual remedy from row 72.
   - **Row 81**, the wording narrowed, and **row 85**, branch first, match the code.

### FINDINGS

- [MEDIUM R1] src/Read/CatEcommerce.Read/QueryHandlers/Billing/BillingChargeQueryHandlers.cs:140-143 — the surgery arm's billable-line count is not the twin of `SurgeryBillableItemSource.DrugsAsync`.
  - **What the list counts.** Only `Kind == Drug` entries that no entry corrects.
  - **What the source bills.** Each original Drug entry resolved to its correction-chain leaf, where the leaf is a
    `Kind == Correction` row with a SKU and a positive quantity. That gives one charge `drug:{originalId}` per
    corrected drug, which the list counts as 0.
  - **Consequences.**
    - Completed, with one corrected drug that is unpriced: 1 line counted against 1 charge, so the source is not
      listed, although a skipped line exists. That is the row 77 gap again.
    - Abandoned, with only corrected drugs, after a failed event capture: 0 lines counted, so the source is never
      listed. That is a regression, because before this round every uncharged discharged surgery was listed.
  - This contradicts the ADR-346 revision ("counts the same tables with the same rules").
  - **Why it is not HIGH.** Nothing is double-charged, and it takes a correction plus either a skip or a failed
    capture.
  - **Fix.** Count original Drug entries whose latest correction, if any, has a SKU and a quantity above 0, or keep the
    simple count and state the limit in row 83 and the ADR revision.
  - Proposed row 95.
- [MEDIUM R2] docs/handover/README.md:107 — the ADR pointer bump (ADR-343 to ADR-347) is a new edit in this round to §14 `docs/handover/**`.
  - Former LOW 5 assigned it to the coordinator.
  - It is additive bookkeeping and is recorded in row 92, so it is treated as former MEDIUM 3 was: the coordinator
    ratifies or reverts it at merge. If the coordinator refuses, it becomes a §14 BLOCKER.
  - Proposed row 96.
- [LOW R3] clinic.surgery (no UPDATE trigger) — `owner_at_discharge_id`, "stored once", is frozen by the aggregate only.
  - `clinic.visit.customer_id` is frozen by `visit_frozen` after finalising. The surgery column has no equivalent.
  - This is consistent with SRG's existing posture, where no surgery column is database-frozen.
  - Hardening is an integrator migration: refuse a change to the column once `OLD.status = Discharged`.
  - Proposed row 97.
- [Note] Former MEDIUM 1 (reversal void) was ratified by the owner. Former MEDIUM 2 was narrowed. Former MEDIUM 3 was
  ratified by the coordinator. Former MEDIUM 4 is fixed, apart from R1. Former LOW 1 to 6 are recorded or resolved in
  rows 86 and 90-93. The CODE_REVIEW and SECURITY MEDIUM and LOW findings are closed, as rows 77, 84, 85, 86, 88 and
  89 state, except row 87, which stays Open.
- [Note] `.ai/templates/*.cs` is still stale (BIL row 33). Nothing in this diff copies from it.

### Proposed docs/backlog/BIL.md rows

| # | Severity | Item | Source |
|---|---|---|---|
| 95 | MEDIUM | **The uncaptured list's surgery arm ignores corrected drugs.** It counts only `Drug` entries that no entry corrects, while `SurgeryBillableItemSource` bills each drug chain's leaf (a `Correction` row with a SKU and quantity). A corrected drug that is skipped or never captured is invisible; an abandoned surgery with only corrected drugs and a failed capture is never listed. Count originals whose latest correction keeps a SKU and quantity, or state the limit in row 83 and the ADR-346 revision. | ARCH J re-check R1 |
| 96 | MEDIUM — Coordinator | **Ratify or revert the `docs/handover/README.md` ADR pointer bump** (ADR-343 to ADR-347) made by the Phase J gate-fix round in a §14 file. | ARCH J re-check R2 |
| 97 | LOW — Integrator | **DB-freeze `clinic.surgery.owner_at_discharge_id` after discharge**, as `visit_frozen` does for `clinic.visit.customer_id`. This is a migration. | ARCH J re-check R3 |

### SCOPE CHECK (fix range, excluding gate records)

- **Files in allowed scope: 27/28.**
  - **Within §13 or Billing convention:** `ChargeCommandHandlers.cs`, `SourceChargeCapture.cs`,
    `IBillableItemSource.cs`, four `Billing/ItemSources/*`, `BillingChargeQueryHandlers.cs`, the
    IntegrationTests and Domain.Tests files, `docs/test-cases/TC-HO-BIL-001.md`, `.ai/decisions/adr.md` (appended),
    and `docs/backlog/BIL.md`.
  - **§14 edits authorised by the owner decisions of 2026-10-14 or ratified by the coordinator:** the five SRG files
    (`SurgeryAggregate.cs`, `SurgeryEvents.cs`, `SurgeryDtos.cs`, `SurgeryRecoveryCommandHandlers.cs`,
    `SurgeryConfiguration.cs`), the migration pair and the snapshot, `HO-BIL-001.md`, `HO-EMR-001.md`, `HO-SRG-001.md`
    and `CONTRACTS.md`.
  - **`.ai/flows/clinic-visit-flow.md`:** reverted, with a net zero diff against the base.
- **Files outside allowed scope:** `docs/handover/README.md`, MEDIUM R2. It is pending coordinator ratification and
  becomes a BLOCKER if refused.
- **New NuGet or npm dependencies:** none. No csproj, sln, NuGet.Config, global.json or `Program.cs` change.
