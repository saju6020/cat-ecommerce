GATE: ARCHITECTURE
HANDOVER: HO-SRG-001
COMMITS: ccf7d3d6 608fefca 882d7cf4 60f3feaf dc3b0d47 20aeeed5 7a232f04 75ae3bda (branch ho/SRG-001, range a8761829..75ae3bda, 170 files)
DATE: 2026-10-11
STATUS: FAIL

This is the first ARCHITECTURE pass. Owner gate policy (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings are
appended to docs/backlog/SRG.md as rows 28-37, prefixed "ARCHITECTURE:".

The gate fails on **one HIGH and no BLOCKER**. The read side materialises aggregates in five query handlers. CAT, NTF, SHP, EMR, BIL and IMP
were each failed for the same thing. Apart from that, the slice complies with the approved architecture. The fix is mechanical: project with
`Select` before materialising, at the five sites listed under the HIGH. A delta re-check of those files is enough.

What this pass reviewed:
- HO-SRG-001 §1, including the Amended (build) row (1)-(13), and §2-§20 in full.
- docs/handover/CONTRACTS.md, the contract table and the cross-cutting duties C-ALL-1 to C-ALL-5.
- In HO-APT-001 §1: the Amended rows. In HO-EMR-001 and HO-PHM-001: the published-port sections (§8.9, ADR-132, ADR-144, ADR-146).
- `.ai/`: architecture, cqrs, bounded-contexts, namespace-conventions, di-registration-guide, command-handler-pattern, read-side-pattern,
  persistence-ef-core, domain-events-and-outbox, enumeration-pattern and host-composition. Also the appended ADR-157 to ADR-173.
- `git diff a8761829..75ae3bda`:
  - every non-generated source file;
  - the hand-written SQL in all six migrations;
  - the model snapshot, checked by its table set.
- Earlier gates' rulings: EMR-001, IMP-001, PHM-001, SHP-002 and BIL-001 ARCHITECTURE. These keep severities consistent.
- The CODE_REVIEW and SECURITY records for this handover (b4d55ff6, 06012e34), so this pass does not duplicate their findings.

Nothing was built or run. Every runtime claim is marked for TEST.

VERIFIED (no finding)
- **No superseded architecture.** A sweep of every changed `src/**` file found none of these:
  - `IDispatcher`, `IBusMessageDispatcher` or `IEventPublisher`;
  - MassTransit, RabbitMQ, Mongo/Bson or `QueuePublishOnly`;
  - `202`, `CommandServices`, `ErrorEvents`, `IReadRepository` or `ViewModelBase`;
  - `[Authorize]`, `HasMany`, `AddServiceBusProvider`, or a second host.

  Every `SaveAsync`/`UpdateAsync` passes `InMemoryPublishOnly`. The only exceptions are the audit-row writes, which pass `DoNotPublish`, as
  the precedent allows.
- **Layers.**
  - Domain references no EF Core, Application, Read or Api type.
  - No changed Application file references Read, Infrastructure.Persistence or EF Core.
  - No changed Read file references Application.
  - Namespaces use the `Surgery` feature segment (ADR-157).
- **Controllers.** Every action dispatches one command or one query through `IInMemoryDispatcher`. No controller holds a `DbContext`,
  repository or aggregate. Ids are minted in the controller.
  - Reschedule and Discharge use the established command-then-read-back shape. Many existing controllers do the same.
  - The detail, log and report reads dispatch their query and then the clinical access-log command. That is the PHM-001 approved precedent
    (`TryLogClinicalAccessAsync`), not a new finding.
  - No `PUT`, `PATCH` or `DELETE` route exists on the anaesthesia log.
- **Aggregates.**
  - Every state change is inside an aggregate method that ends with `UpdateVersion()`.
  - The one deliberate exception is `SurgicalReportAggregate.AddAddendum`. It does not bump the version, per §12.1 and U-E6, so the
    immutability trigger is not fired by an addendum.
  - Roots reference each other by id. Children are `OwnsMany` with `ValueGeneratedNever`, and every root maps `xmin`.
  - Money is `numeric(12,2)` plus `char(3)`, doses are `numeric(10,3)`, and instants are `timestamptz`.
  - Enumerations are persisted by `Value`.
  - BR-5 is the aggregate's own rule (`SurgeryAggregate.Start`), fed by a consent fact the handler re-reads.
  - The anaesthesia log entry refuses a second `Begin`, so it is append-only in the aggregate as well as in the database.
- **The one-aggregate rule and its recorded exceptions.**
  - ADR-158 (surgery and appointment): book enlists the new appointment through `ISurgeryAppointmentEnlister` without saving. Reschedule,
    cancel and end-procedure mutate the tracked appointment, and the surgery's single save commits both (F23).
  - ADR-163 (drug entry, stock ledger and register): the ledger rows are enlisted through `IStockLedger.DeductAsync`, `ReceiveReturnAsync`
    and `RecordShortfall`. The register entries are enlisted through `IControlledDrugRegister.Enlist`. One `SaveAsync` commits everything.
  - No other handler mutates two aggregates. `ApplySterilisationOutcomeCommand` mutates only the cat, through the Cat-context helper that
    §8.14 specifies.
- **Other modules are used only through published ports.**
  - Inventory: `IStockLedger`. `GetDispensaryBatchesAsync`, `DeductAsync`, `ReceiveReturnAsync` and `RecordShortfall` are all in CONTRACTS
    row 1. The reference kind is `StockReferenceKind.Surgery`, per CONTRACTS D-2.
  - Pharmacy: `IFormularyReader`, `IControlledDrugRegister` and `IStaffWitnessVerifier`. These are HO-PHM-001's exported contracts (§8.9,
    "consumed by HO-SRG-001").
  - EMR: consent goes through `ConsentRules.IsValidFor`, HO-EMR-001's stated Surgery contract (ADR-132). It is applied by Surgery's own
    `ISurgeryConsentReader`.
  - Cat: neuter status goes through the Cat-context handler and `RecordNeuterStatus`. `CatAggregate` is not edited.
  - Notifications: `INotificationOutbox.Enqueue` is called in command handlers before the save, never from an event handler. There are two
    new `INotificationLinkBuilder` methods and two `INotificationSubjectGuard`s.
  - **No module internals.** Nothing in the diff uses `IInventoryClock`, `IStockPositionReader`, `ShopBusinessDate`, an Inventory or
    Pharmacy table, or a write to another context's aggregate or DbSet. Other contexts' tables are read only, in Infrastructure ports and the
    Read layer, `AsNoTracking`.
- **Surgery's own clock.** `ISurgeryClock` (Domain port, `SurgeryClock` in Infrastructure, registered Scoped) supplies branch-local dates
  and zones. It delegates to HO-ADM-001's `BranchCalendar` (ADR-045).
  - The `ICatRecordClock` in `NeuterStatusApplication` and the repair handler belongs to the Cat-context helper. It is not used for any
    Surgery date.
- **Immutability triggers**, all hand-written SQL:
  - `AddAnaesthesiaLog`: `trg_anaesthesia_log_entry_immutable` (`BEFORE UPDATE OR DELETE`, `P0001`, `immutable:` prefix) and a
    `BEFORE TRUNCATE` trigger. The TRUNCATE trigger is stricter than §12.1 and is recorded in ADR-162.
  - `AddSurgicalReport`:
    - `trg_surgical_report_immutable` refuses a delete always, and an update once `OLD.finalised_on IS NOT NULL`;
    - `trg_surgical_report_addendum_immutable` refuses any update or delete of an addendum;
    - TRUNCATE guards cover both tables.
  - Each `Down` drops the triggers before the tables. No migration updates or deletes a log, report or addendum row.
  - The filter maps `P0001` with the `immutable:` prefix to `409 RecordIsImmutable` and rethrows any other `P0001` (§12.5).
- **The six migrations touch only their authorised tables.**
  - `AddAppointmentStaffTime` (G3) changes the authorised APT tables:
    - `clinic.appointment` gains `surgery_id`, `assistant_id` and a filtered index;
    - `clinic.appointment_staff_time` is created with its backfill (`BackfillSql`, built from the enumerations);
    - the exclusion constraint is `ex_appointment_staff_time`, `EXCLUDE USING gist (staff_account_id WITH =, tstzrange(...,'[)') WITH &&)
      WHERE (occupies)`.
  - The other five create only `clinic.surgery*`, `clinic.anaesthesia_log_entry`, `clinic.surgical_report*`, `clinic.surgery_protocol*` and
    `clinic.surgery_campaign*`. `ix_surgery_campaign_starts_on` is a hand-written partial index.
  - The snapshot's table set grows by exactly the Surgery roots. Every other `ToTable` line is EF reordering.
  - The migration timestamps follow the baseline's last migration (`20261010114755`).
- **The HO-APT-001 amendment is authorised by HO-SRG-001 §11.7 and §13.** Both were written by the handover author, not by the builder.
  - G1 is in `AppointmentAggregate`:
    - `SurgeryId` and `AssistantId`;
    - the owned `StaffTime` (≤ 2), maintained by `SyncStaffTime()`;
    - `BookForSurgery`, `MoveForSurgery`, `CancelForSurgery` and `CompleteSurgerySlot`;
    - `AppointmentManagedBySurgery` on reschedule, cancel, check-in, triage, start, complete and no-show.
  - G2 is `BookingChannel.Surgery`. G3 is the migration above.
  - G4 is `AppointmentSchedulingInputs`, which now counts an assistant as busy.
  - G5 is the queue, day-list and customer-list filters, plus the calendar's `surgeryId`/`assistantId`.
  - G6 is `ServiceNotBookable` for theatre services in `SlotClaim`.
  - G7 is the flagging finder matching `AssistantId`. G8 is the filter mapping `ex_appointment_staff_time` to `PerformerBusy`.
  - HO-APT-001's own §1 Amended row (OQ-4) records only part of this. See MEDIUM 4.
- **Owner decision 2026-10-10 is honoured.**
  - No boarding type, column or port was added. US-SRG-09 is recorded in backlog row 1.
  - `GetCatSurgeriesQuery` and `GetSurgeryChargeableItemsQuery` are registered with no endpoint and no consumer. EMR Phase F and BIL
    Phase J are recorded in backlog rows 2 and 3.
- **Host.** `Program.cs` is unchanged; it already calls `AddSurgeryModule`. `Modules/SurgeryModule.cs` (ADR-341) registers everything,
  all Scoped:
  - 25 command handlers;
  - 25 validators, all as `IValidator<T>`;
  - 18 query handlers, matching the 18 query classes;
  - 8 domain event handlers;
  - the six Surgery-owned ports, the retention source and the two guards.

  Nothing that `AddInMemoryBusServices()` provides is hand-registered.
  - The consumed ports are registered by their owners: `IStockLedger` in InventoryModule; `IFormularyReader`, `IControlledDrugRegister` and
    `IStaffWitnessVerifier` in PharmacyModule; `INotificationOutbox` in NotificationsModule.
  - No `*.csproj`, package, `*.sln`, `global.json` or `NuGet.Config` was changed.
- **Domain events.**
  - Payloads carry only ids, instants, enumerations and booleans (U-X1).
  - Handlers live in `DomainEventHandlers/Surgery/`, except the one §8.14 places in `DomainEventHandlers/Cat/`.
  - They are idempotent, use one `UpdateAsync` per row, and never enqueue.
  - The audit handlers write `BusinessEvent` rows with `DoNotPublish`, as the EMR precedent allows.
- **XCT duties.**
  - C-ALL-1: `RecordClassification.Surgery.cs` classifies all 13 new tables.
    - The care tables are Clinical/Forbidden.
    - `appointment_staff_time` is Clinical/AggregateOnly, because it is derived occupancy.
    - The protocols and campaigns are Operational.
  - C-ALL-2: `ErasureExemptions.Surgery.cs` gives a reason, and no eraser is registered (the module comment says so).
  - C-ALL-3: `SurgeryRetentionReviewSource` is a count-only `IRetentionReviewSource` and is registered.
  - C-ALL-4: no aggregate method removes a record. Follow-ups are closed, not deleted. The only removal is a derived `StaffTime` row.
  - C-ALL-5: `ClinicalAuditEntityTypes.Surgery.cs` lists the three roots and the `Surgery` BusinessEvent type.
- **Governing documents are not edited.**
  - `.ai/**` changed only in `decisions/adr.md`, with append-only ADR-157 to ADR-173 (0 removed lines, LF line endings).
  - `.ai/flows/surgery-flow.md` and `.ai/modules/**` are untouched, per Amended (13) and backlog row 12.
  - In `docs/handover/**`, only HO-SRG-001's §1 Amended (build) row was added. That is the established convention.
  - `web/**`, `docs/SRS.md` and `docs/USER_STORIES.md` are untouched.

FINDINGS
- [HIGH] **The read side materialises aggregates.** This breaks ADR-012 and `.ai/read-side-pattern.md` §4 rules 1 and 2 ("Project, never
  rehydrate"; "`Select` before `ToListAsync`"). CAT, NTF, SHP, EMR, BIL and IMP were failed for the same class of finding.
  - **Sites that load whole roots with no `Select`:**
    - src/Read/CatEcommerce.Read/QueryHandlers/Surgery/AnaesthesiaLogQueryHandler.cs:28 — `GetAnaesthesiaLogQuery`, the timeline.
      `db.AnaesthesiaLogEntries...ToListAsync()` loads up to 2000 entries per surgery.
    - src/Read/CatEcommerce.Read/QueryHandlers/Surgery/SurgeryRecoveryQueryHandlers.cs:183-185 — `GetSurgeryChargeableItemsQuery`. It
      loads the same whole entries.
    - src/Read/CatEcommerce.Read/QueryHandlers/Surgery/SurgeryCampaignQueryHandlers.cs:32 — `GetSurgeryCampaignsQuery`. It loads every
      `SurgeryCampaignAggregate` with its owned `Prices`.
    - src/Read/CatEcommerce.Read/QueryHandlers/Surgery/SurgeryCampaignQueryHandlers.cs:57-59 — `GetApplicableCampaignsQuery`, the same.
    - src/Read/CatEcommerce.Read/QueryHandlers/Surgery/SurgeryRecoveryQueryHandlers.cs:69 — `GetDischargeDraftQuery`.
      `SurgeryProtocols.AsNoTracking().FirstOrDefaultAsync(...)` loads the whole protocol with its `FollowUps`.
  - **The fix** is the same as for the earlier areas. Project the needed columns into an anonymous type (or the ViewModel) before
    `ToListAsync` or `FirstOrDefaultAsync`. Run the correction chain, the campaign evaluation and `FollowUpPlanner` over the projected rows.
  - **What is already clean.** Every other Surgery read projects correctly:
    - the day list, the list, the detail and the recovery board;
    - the customer reads, the cat history, the services, staff and availability reads, and the report.
  - Nothing here writes, and every read is `AsNoTracking`. This is a pattern violation, not a data risk.
- [MEDIUM] src/Domain/CatEcommerce.Domain/Aggregates/Surgery/SurgeryAggregate.cs:366-379 — `SetRecoveryStatus` appends to the owned
  `RecoveryHistory` with no bound. §8.1 bounds the collection to ≤ 50, and `SurgeryRules.MaxRecoveryHistory = 50` is declared but never
  checked. `.ai/bounded-contexts.md` rule 3 relies on owned collections being bounded. It is bounded in practice by one recovery stay, so this
  is not HIGH. This is the same ruling as EMR-001's problem-list growth. Backlog row 28.
- [MEDIUM] Four list queries have no paging (SRS UI-4, read-side rule 4). §12.4 does not specify paging for these routes, so they are MEDIUM,
  not HIGH (EMR precedent). Backlog row 29.
  - SurgeryCampaignQueryHandlers.cs:32 — `GetSurgeryCampaignsQuery`.
  - SurgeryCampaignQueryHandlers.cs:86-95 — `GetCampaignCasesQuery`. `from` and `to` are optional, so the whole campaign can be read.
  - SurgeryRecoveryQueryHandlers.cs:91-103 — `GetCustomerSurgeriesQuery`.
  - SurgeryRecoveryQueryHandlers.cs:151-160 — `GetCatSurgeriesQuery`.
- [MEDIUM] Scope record. Amended (build) (11) lists the files outside §13's literal list, but it misses some. None of them touches §14.
  They need adding to (11). Backlog row 30.
  - **Two new Surgery-owned Infrastructure files** are not in §13 or (11):
    - `Infrastructure/Surgery/SurgeryPorts.cs`, which implements the enlister, clock, finder and consent reader;
    - `Infrastructure/Surgery/SurgeryCampaignPricing.cs`.
  - **HO-APT-001 files changed outside §13's literal line list**, each carrying out a change §11.7 authorises:
    - the `AppointmentManagedBySurgery` early-return in `CommandHandlers/Clinic/AppointmentChangeCommandHandlers.cs` and
      `AppointmentStaffCommandHandlers.cs`. §13 says "G6 only", but these lines carry out §8.4/G1's refusal on HO-APT-001's endpoints;
    - G7 in `Infrastructure/Scheduling/FlaggableAppointmentFinder.cs`. §13 names `FlagAppointmentsOnLeaveApprovedHandler`, but the query
      lives in the finder;
    - G5's calendar fields in `Read/ViewModels/ClinicViewModels.cs`.

    These follow the EMR-001 ruling on the ADR-133 guard edits ("in the APT files that actually hold" the code). They are therefore counted
    in scope, but they should be recorded.
  - `Shared/Common/Constants/SettingKeys.Surgery.cs` is an accepted ADR-341 partial, but it is not named.
- [MEDIUM] docs/handover/HO-APT-001.md §1 — OQ-4 is only partly discharged. HO-APT-001's Amended row (2026-09-23) records:
  `BookingChannel.Surgery`, ADR-172 and `AppointmentManagedBySurgery`.

  It does not record:
  - the `appointment_staff_time` table and `ex_appointment_staff_time`;
  - the `surgery_id` and `assistant_id` columns;
  - `BookForSurgery`, `MoveForSurgery`, `CancelForSurgery` and `CompleteSurgerySlot`, and `SyncStaffTime`;
  - G4 (an assistant counts as busy for ordinary slots);
  - G5 (surgeries are excluded from the queue, day list and customer list, and shown on the calendar);
  - G7, and G8 (`ex_appointment_staff_time` mapped to `409 PerformerBusy` on appointment routes).

  Anyone reading HO-APT-001 alone gets an incomplete picture of the appointment model. This is a coordinator action (§14 forbids this
  branch from editing it), not a code defect. Backlog row 31.
- [LOW] src/Domain/CatEcommerce.Domain/Aggregates/Clinic/AppointmentAggregate.cs:198/200, 235/237, 266/268, 319/321, 334/336, 359/361 and
  378/380 — `EnsureNotManagedBySurgery()` is called twice in a row in each of the seven guarded methods. The duplicate is harmless but should
  be removed. Backlog row 32.
- [LOW] G4 and §8.5 are carried out differently from the text, and the deviation is not recorded.
  - §11.7 G4 and §8.5 say busy intervals are read from `clinic.appointment_staff_time`.
  - `AppointmentSchedulingInputs` and `SurgerySchedulingInputs.cs:55-66` read the appointment's `PerformerId`/`AssistantId` columns instead.
    The stated reason is that the table's `occupies` flag also covers lapsed holds.
  - The two are equivalent, and the exclusion constraint remains the race control. Record it in Amended (build). Backlog row 33.
- [LOW] The witness contract changed and §1 does not record it.
  - §8.9 and §11.1 C8 specify a single-use confirmation: `WitnessConfirmationId`, `IWitnessConfirmationVerifier.ConsumeAsync`.
  - The build uses HO-PHM-001's published in-request identifier and password (`IStaffWitnessVerifier.VerifyAsync`). That is correct
    against PHM as merged, and ADR-164 records it.
  - §1 Amended (build) does not mention it. Add it beside item (1). Backlog row 34.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Surgery/SurgeryPorts.cs:70-73 — `SurgeryConsentReader` materialises whole
  `ConsentRecordAggregate` rows, which belong to the EMR context.
  - This is forced by the published rule, which takes the aggregate, and it is acceptable on the start gate.
  - But the port is also called from `GetSurgeryDetailQueryHandler` (SurgeryDetailQueryHandler.cs:71), so a read path rehydrates another
    context's aggregate.
  - When the HIGH above is fixed, consider asking HO-EMR-001 for a projection-friendly form of `IsValidFor`. Backlog row 35.
- [LOW] The anaesthesia, procedure and recovery handlers parse enumerations with HO-APT-001's `AppointmentMapper.ParseEnum`.
  - That couples Surgery handlers to another area's mapper for a generic helper.
  - The same LOW was raised for EMR-001 deviation 11.
  - `SurgeryMapper` exists and could host it. Backlog row 36.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Surgery/SurgeryRetentionReviewSource.cs:22-31 — the source groups every surgery in
  the database. It then sends every cat id that has a surgery in `IN` lists to the visit and cat tables.
  - This is fine at one clinic's volume, but it grows without limit.
  - A single SQL statement with the anchor computed in the database, or a view, would bound it. Backlog row 37.

Concurrence with earlier gates (not repeated as new rows):
- CODE_REVIEW row 18: the ledger and register rows are enlisted before `entry.LogDrug`/`Correct` runs.
  - Architecturally, ADR-163's "all three or none" then depends on nothing else saving later in that request scope. Today nothing does.
  - The witness verifier's own audit save happens before anything is enlisted.
  - I agree with LOW. TEST should run the out-of-window `OccurredOn` case that row 18 asks for.
- SECURITY row 23 (Discharge and Reschedule read-backs are not access-logged) is a security concern, not a layering one.

FOR TEST (runtime, not observable here)
- `dotnet ef migrations has-pending-model-changes` reports no changes after the integrator regenerates the six migrations.
- F31: a `P0001` from each trigger surfaces as `409 RecordIsImmutable` through the filter (I-D6, I-E*).
- U-E6 / I-E*: adding an addendum to a finalised report issues no `UPDATE` on `clinic.surgical_report`. Otherwise the trigger would fire.
- I-A3: the `BackfillSql` backfill gives `ex_appointment_staff_time` the existing performer occupancy.
- I-B*: the book, reschedule and end-procedure saves commit the surgery and its appointment atomically. The constraint race maps to `409`.
- I-D*: a drug entry, its ledger rows and its register entries commit or roll back together. The CODE_REVIEW row 18 case belongs here.
- `ValidateOnBuild` succeeds with the Pharmacy, Inventory and Notifications owners' registrations in place.

SCOPE CHECK
- files in allowed scope: 168/170. Files counted in scope:
  - files inside §13's globs and lists;
  - ADR-341 per-area translations of §13's Program.cs and shared-constant entries:
    - `Modules/SurgeryModule.cs` and `CatEcommerceDbContext.Surgery.cs`;
    - the `Actions`, `ActionRoles`, `BusinessViolationCodes`, `SettingDefinitions`, `SettingKeys` and `NotificationTemplateCatalogue`
      `.Surgery.cs` partials;
  - the CONTRACTS C-ALL files: `RecordClassification`, `ErasureExemptions` and `ClinicalAuditEntityTypes` `.Surgery.cs`, and
    `SurgeryRetentionReviewSource.cs`, which is listed in Amended (11);
  - the helpers named in Amended (11): `ISurgeryPorts.cs`, the four `CommandHandlers/Surgery` helpers, `SurgeryControllerBase.cs` and
    `NeuterStatusApplication`;
  - grouped files in §13's folders: validators, configurations, entities, value objects, guards and DTOs;
  - the HO-APT-001 lines that carry out §11.7 G1/G5/G7 in the files that actually hold that code. These are counted in scope by the EMR-001
    ADR-133 precedent and need a record (MEDIUM 3);
  - `docs/handover/HO-SRG-001.md`, Amended row only (the established convention);
  - `docs/backlog/SRG.md`, under the owner's 2026-09-27 rule that MEDIUM/LOW findings go to `docs/backlog/<AREA>.md`;
  - `docs/test-cases/TC-HO-SRG-001.md` and `.ai/decisions/adr.md` (append only).
- files outside allowed scope: 2. Neither is in §14 forbidden scope; see MEDIUM 3.
  - src/Infrastructure/CatEcommerce.Infrastructure/Surgery/SurgeryPorts.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Surgery/SurgeryCampaignPricing.cs
- None of these was touched:
  - `web/**`, `docs/SRS.md` or `docs/USER_STORIES.md`;
  - another handover document, `.ai/**` other than adr.md, or `.claude/**`;
  - any `*.csproj`, `*.sln`, `NuGet.Config` or `global.json`;
  - `CatAggregate`, or any HO-EMR-001, HO-PHM-001 or HO-IMP-001 file, port or table;
  - any boarding, payment, deposit or invoice type.

  No package was added.

---

## Re-check (fix round 8719c96a)

GATE: ARCHITECTURE (combined delta re-check: architecture primary, plus security and code quality of the changed code)
HANDOVER: HO-SRG-001
COMMITS: 8719c96a (diff `b65bbf0a..8719c96a`, 37 files)
DATE: 2026-10-11
STATUS: FAIL

The first pass's HIGH is closed. The gate still fails on **one BLOCKER**, which needs no code change. `ConsentRules.cs` belongs to
HO-EMR-001 and is §14 forbidden scope. It was edited on the authority of this slice's own Amended (17) and nothing else. The
precedent (HO-APT-002, HO-ACC-002, HO-FIL-002 and HO-SHP-002 ARCHITECTURE) holds that a §14 edit needs an owner or integrator
decision recorded in §1 Amended. The builder's own row is not enough, even when the edit is additive and behaviour-neutral. That is
the case here, and it is why no other finding blocks. Once the decision is recorded, a delta check of the record is enough.

Nothing was built or run. Runtime claims are marked for TEST.

VERIFIED (no finding)
- **The HIGH is closed (ADR-012, read-side-pattern §4 rules 1-2).** All five sites now project before they materialise:
  - `GetAnaesthesiaLogQuery` and `GetSurgeryChargeableItemsQuery` use the shared `AnaesthesiaLogRows.ProjectAsync`, which `Select`s
    into an internal `AnaesthesiaLogRow` record;
  - `GetSurgeryCampaignsQuery` and `GetApplicableCampaignsQuery` project to anonymous types, with the owned `Prices` projected inside
    them;
  - `GetDischargeDraftQuery` projects the protocol's instructions and its `FollowUps`.

  I swept every `ToListAsync`, `FirstOrDefaultAsync` and similar call in `Read/QueryHandlers/Surgery/**`: Protocol, Schedule,
  Detail, Report, Recovery and Campaign. Each projects with `Select`, `SelectMany` or a `select` clause. No new rehydration was
  found.
- **`SurgeryCampaignAggregate.Evaluate` has a static overload over plain values.**
  - The instance overload delegates to it, so the booking and the read share one decision.
  - The read passes `isActive: true`. That is safe, because the query already filters `IsActive`.
  - The overload is a pure domain function, and the Read layer calling a Domain function does not cross a layer.
- **`SurgeryConsentReader`** now projects six consent columns and no longer rehydrates EMR's aggregate. On the substance, the new
  `ConsentRules.IsValidFor` overload is a sound design: it is additive, the aggregate overload delegates to it, and the semantics
  are identical. The only problem is the scope. See the BLOCKER.
- **Migration `20261010195049_UniqueAnaesthesiaCorrection`.**
  - It drops `ix_anaesthesia_log_entry_corrects` and recreates it UNIQUE with the same partial filter.
  - `Down` restores the non-unique index.
  - The configuration and the snapshot agree.
  - It is a seventh migration, beyond §13's "six migrations of §12.1". That is not §14 scope, and it is recorded in Amended (19).
  - TEST: the index build fails if a forked chain already exists in a database. The dev DB never had this branch's migrations, so
    the risk is limited to per-branch test DBs.
- **The 23505 → 409 mapping in `UniqueConstraintExceptionFilter`.**
  - It adds `ix_anaesthesia_log_entry_corrects` and `PK_anaesthesia_log_entry` to `SurgeryConflictIndexes`. Those mappings apply
    only under `/api/staff/surgeries`, which includes the `.../anaesthesia-log` routes.
  - The PK name matches `AddAnaesthesiaLog.cs:72`.
  - Unknown constraints still rethrow.
  - §13 allows "§12.5 mappings only" in this file. These rows are §12.5-class mappings, recorded in (18).
- **Drug-log idempotency.** The idempotency check:
  - runs after the surgery is resolved inside the caller's branch and after `IsVetAsync`;
  - runs before `IsOpenForLogging`, the ports and the witness;
  - replays only when the entry's `SurgeryId`, `Kind == Drug` and `LoggedBy` all match the caller.

  Any other holder of the id gets `409 DuplicateAnaesthesiaEntry`. Its message is generic and carries no data from the other
  record. The 409 confirms only that a GUID the caller already holds is in use. Entry ids are unguessable, and inside a branch they
  are already readable through GET log, so there is no meaningful oracle and no replay of another vet's or another surgery's
  entry. The replay skips witness re-verification, which is safe: it writes nothing and returns only ids, to the vet who created
  the entry. The replay path calls no save. TEST: the PK-race loser rolls back its enlisted ledger and register rows in the same
  `SaveChanges`.
- **`AnaesthesiaDrugSupport.DryRun`.**
  - The entry's own rules still live in the aggregate. They run on an untracked throwaway `AnaesthesiaLogEntryAggregate` before any
    port enlists a row.
  - The register-link refusal in `CorrectDrug` moved ahead of the stock step.
  - `Context(...)` normalises `OccurredOn` to UTC, so the shared `context` is safe for the real write.
  - No handler sets aggregate state.
- **`RecoveryHistoryLimitReached`** is enforced inside `SurgeryAggregate.SetRecoveryStatus`, against the owned collection
  (bounded-contexts rule 3). This closes backlog 28.
- **Paging.**
  - Campaigns, customer surgeries and cat surgeries return `PagedResult<T>`, with a stable order (`ThenBy(Id)`) and a count over
    the same filter.
  - Campaign cases are paged and keep their totals. The unbounded totals projection is recorded as backlog 38.
  - Controllers use `PagingHelper`.
  - The DI registrations were updated to the new result types (`IQueryHandlerAsync<…, PagedResult<…>>`).
  - The customer list is still scoped to `user.UserId`.
  - The contract change is recorded in §1 (18).
- **Clinical-access logging on the Discharge and Reschedule read-backs.**
  - Both call `TryLogClinicalAccessAsync` and fail closed. The command is already committed, and the detail is withheld.
  - Both send `no-store`.
  - Each controller still dispatches one command, with a read-back query as before.
- **`NoControlCharacters`** is on the discharge `PostOpInstructions` and on the campaign `Description` (create and update).
- **`IsVetAsync`** is re-checked in `EndSurgicalProcedure` and `ApplySterilisationOutcome`. The handlers inject the retained
  `IAggregateRootRepository<StaffAccountAggregate>`, and the new constructor dependency resolves from an existing registration.
- The duplicate `EnsureNotManagedBySurgery()` calls are removed from `AppointmentAggregate` (backlog 32), within the G1 lines.
- None of the following appears in the diff: `IDispatcher`, a broker, Mongo, a `202`, a rejection event, a projection, or a
  CommandServices class. No package was added and no `csproj` changed.

FINDINGS
- [BLOCKER] src/Domain/CatEcommerce.Domain/DomainServices/ConsentRules.cs:14-23 — **a §14 forbidden-scope file was edited, authorised
  only by the builder's own Amended (17).**
  - §14 forbids "Every file of HO-EMR-001 … Consumed, not built". HO-EMR-001 §13 lists `ConsentRules.cs` (HO-EMR-001.md:1064), and
    ADR-132 makes it "Surgery's contract".
  - The first pass's backlog 35 asked to "Ask HO-EMR-001 for a form of the rule that works on projected rows". It did not authorise
    the edit. Backlog 35 is marked RESOLVED, but that does not hold until the decision exists.
  - The edit itself is fine. It is additive, the aggregate overload delegates to the new one, and behaviour is unchanged.
  - **To close (owner or integrator decision; no code change).** Either:
    - record the decision in HO-SRG-001 §1 Amended, and preferably mirror it in HO-EMR-001 §1 Amended (the HO-ACC-002 `/book`
      precedent). This gate would then pass on a delta check of the record. Or:
    - revert the overload and accept the first pass's LOW: rehydrating EMR's consent aggregate on the start gate and the detail read.
- [MEDIUM] docs/handover/HO-APT-001.md:18 — **the HO-APT-001 Amended row was written by this fix round.** §14 forbids
  `docs/handover/**` and says "HO-APT-001's Amended row is the coordinator's (OQ-4)".
  - This is not a BLOCKER. §14 itself anticipates this exact row, the first pass asked for it (backlog 31), it is a record and not
    behaviour, and its content is accurate and complete against backlog 31.
  - Its provenance line reads "recorded by the gate fix round on `ho/SRG-001`". The coordinator should ratify it, or re-attribute it,
    when recording the BLOCKER decision. Backlog 40.
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Constants/SurgeryMessages.cs:13 and UniqueConstraintExceptionFilter.cs:159 —
  **`DuplicateAnaesthesiaEntry` serves two different situations.**
  - It is returned for "an id held by another record" and also for the PK race. In a PK race the loser is the same vet re-sending
    the same request.
  - The message tells the client to "Send the request again with a new entry id". A client that obeys it after a race creates
    exactly the duplicate the key exists to prevent. Re-sending with the same id would replay correctly.
  - Use a distinct message for the race, such as "retry with the same id", or a distinct code. HO-SRG-002 needs to know which.
    Backlog 41.
- [LOW] AnaesthesiaCommandHandlers.cs:213-219 — **a replay does not compare the payload.** A repeat with the same id and a different
  dose or batch silently returns the first result. Common idempotency practice is to refuse a mismatched body. The impact is low,
  because the first entry stands, a correction can fix it, and the second request was never recorded. Backlog 42.
- [LOW] Correction outcomes. A **sequential** second correction of an entry that is no longer the latest is `400
  CorrectionTargetNotFound`, from the handler's pre-check. A **raced** one is `409 AnaesthesiaEntryAlreadyCorrected`, from the index.
  One condition has two codes, and HO-SRG-002 must handle both. CODE_REVIEW row 16 suggested the 400. Backlog 43.
- [LOW] Contract change (18). HO-SRG-002's §12 still describes array responses for the three list routes and has no `entryId`. The
  coordinator should mirror (18) in HO-SRG-002 §1 Amended before that slice is built. Backlog 44.

SCOPE CHECK
- files in allowed scope: 35/37. These are §13 paths, recorded shared touch points (`UniqueConstraintExceptionFilter` §12.5 rows; the
  `AppointmentAggregate` G1 lines; `SurgeryPorts.cs`, recorded in (14)) and the established docs (HO-SRG-001 §1 Amended row, backlog,
  TC sheet). The seventh migration is outside §13's literal "six" but not §14, and is recorded in (19).
- files outside allowed scope:
  - `src/Domain/CatEcommerce.Domain/DomainServices/ConsentRules.cs` — §14, BLOCKER above;
  - `docs/handover/HO-APT-001.md` — §14 `docs/handover/**`, anticipated by §14 itself as the coordinator's row. MEDIUM above, to be
    ratified.

---

## Re-check 2 (6d250478)

GATE: ARCHITECTURE (delta check of the BLOCKER closure, plus the changed code)
HANDOVER: HO-SRG-001
COMMITS: 6d250478 (diff `ea27029e..6d250478`, 10 files)
DATE: 2026-10-11
STATUS: PASS

The BLOCKER is closed by option one of its remedy: a recorded decision, with no code change to `ConsentRules.cs`. No BLOCKER or
HIGH remains. Nothing was built or run here. The coordinator reports that the Surgery tests pass (117/16/96); that is for TEST to
confirm.

VERIFIED
- **The BLOCKER (`ConsentRules.cs`, §14) is closed.**
  - HO-SRG-001 §1 has a dated Amended row, "2026-10-11 -- INTEGRATOR DECISION". It ratifies the additive
    `ConsentRules.IsValidFor` overload by name and file, and limits the authority to that overload: "No other edit to an EMR file is
    authorised by this row".
  - The same decision is mirrored in HO-EMR-001 §1, as the HO-ACC-002 `/book` precedent did.
  - This is the remedy the re-check named, and it matches the HO-SHP-002, HO-APT-002 and HO-FIL-002 closures.
  - **Provenance.** This gate checked that the decision is recorded, not who made it. The coordinator relayed it as an integrator
    decision ("the owner may review"), and the commit author is shahjahan.
  - `ConsentRules.cs` is unchanged in this delta.
- **The MEDIUM on the HO-APT-001 row is closed (backlog 40).** The same decision, part (b), ratifies the HO-APT-001 §1 row as the
  coordinator's OQ-4 row.
- **Backlog 41 is closed.**
  - `PK_anaesthesia_log_entry` now maps to `409 AnaesthesiaEntryRaced`, with the message "retry with the SAME entry id". The mapping
    stays route-scoped to `/api/staff/surgeries`.
  - `DuplicateAnaesthesiaEntry` is now used only for the handler's "another surgery or user" refusal.
  - The filter comment explains why the handler cannot re-read after a failed `SaveChanges`.
  - The race test asserts the new code.
  - A race between two different users who send the same new id gets `Raced`, and their retry then gets `Duplicate`. That corrects
    itself and is harmless.
- **Backlog 42 is closed.**
  - A replay now compares `MedicineId`, `BatchId`, `QuantityDrawn` and `DoseAmount`, and a mismatch is `409 AnaesthesiaEntryIdMismatch`.
  - The comparison runs after the same-owner check, so a mismatch reveals nothing about another vet's or another surgery's entry.
  - A stocked drug requires the client to name `BatchId` (`PlanAsync`), so a true retry never mismatches because of a FEFO-chosen
    batch.
  - A test covers both a changed quantity and a changed dose, and confirms that the identical body still replays.
- **Backlog 44 is closed.** HO-SRG-002 §1 has a dated Amended row mirroring (18). It also gives the client guidance on all four 409
  codes and on the 400 versus 409 outcome for corrections, which also covers what backlog 43 asked for. Row 43 stays open as an
  alignment LOW; it is now documented for the FE.
- **Layers.** The new codes and messages are appends to the Surgery constant partials, and the two new 409 codes are in
  `SurgeryControllerBase.ConflictCodes`. No layer, aggregate or dispatch change. No package was added.

FINDINGS
- [LOW] AnaesthesiaCommandHandlers.cs:196 — **the mismatch check omits some fields.** It does not compare `DoseUnit`, `Route`,
  `QuantityWasted` or `AdministeredById`. A same-id repeat that differs only in those fields still silently replays the first
  entry. The fields that move stock (medicine, batch, drawn) and the dose amount are covered, so the impact is low. Backlog 45.

SCOPE CHECK
- files in allowed scope: 10/10
  - §13 code paths: `SurgeryControllerBase`, the filter's §12.5 rows, `CommandHandlers/Surgery/**`, the Surgery constant appends, and
    the integration tests;
  - `docs/backlog/SRG.md`;
  - HO-SRG-001 §1 Amended (the established convention);
  - HO-EMR-001 §1 and HO-SRG-002 §1 Amended rows, which are integrator and coordinator records rather than builder edits.
- files outside allowed scope: none. `ConsentRules.cs` and the HO-APT-001 row from the previous round are now authorised by the
  recorded integrator decision.
