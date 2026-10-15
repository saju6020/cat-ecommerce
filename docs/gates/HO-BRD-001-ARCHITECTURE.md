GATE: ARCHITECTURE
HANDOVER: HO-BRD-001
COMMITS: 2d5f1e38 7e2806c2 79b1236d a8e7f416 0aecd7f7 a5c5b296 9eb583c7 16909328 7df0573e 7ddf9852 95a97bab (branch ho/BRD-001, range 5a187b6b..95a97bab, 203 files)
DATE: 2026-10-15
STATUS: FAIL

This is the first ARCHITECTURE pass. Owner gate policy (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings
are proposed below as rows for docs/backlog/BRD.md. That file does not exist yet. This gate was told to write and commit only this file,
and SECURITY and CODE_REVIEW are running at the same time, so the integrator creates BRD.md and numbers the rows.

The gate fails on **one BLOCKER and one HIGH**.
- The BLOCKER is the edit to `.ai/flows/boarding-stay-flow.md`. §14 forbids it, and the HO-ORD-001 and HO-BIL-001-J precedents apply.
  Reverting the file closes it.
- The HIGH is the read side: query handlers materialise aggregates instead of projecting them (ADR-012, read-side-pattern §4 rules 1-2).
  CAT, NTF, SHP, EMR, BIL, IMP and SRG each failed for the same thing.

Otherwise the slice complies with the approved architecture: the in-memory dispatcher, aggregate discipline, persistence, explicit
registration, the worker shape and the Billing push model.

What this pass reviewed:
- HO-BRD-001 in full, including §1.1 Amended (build).
- docs/handover/CONTRACTS.md: rows 10-15, the new rows 50-59 and C-ALL-1 to C-ALL-5.
- `.ai/`: architecture, cqrs, bounded-contexts, namespace-conventions, di-registration-guide, command-handler-pattern, read-side-pattern,
  persistence-ef-core, domain-events-and-outbox, enumeration-pattern and host-composition. Also the appended ADR-350 to ADR-369.
- `git diff 5a187b6b..95a97bab`:
  - every non-generated source file;
  - the hand-written SQL in all six migrations;
  - the model snapshot, checked by its table set.
- Earlier gates' rulings for consistency: SRG-001, BIL-001-J, ORD-001 and XCT-001 ARCHITECTURE.

Nothing was built or run here. Every runtime claim is marked for TEST.

VERIFIED (no finding)
- **No superseded architecture.** A sweep of every changed `src/**` file found none of these:
  - `IDispatcher`, `IBusMessageDispatcher`, `IEventPublisher`, `SendBusMessageAsync` or `PublishBusMessageAsync`;
  - MassTransit, RabbitMQ, Mongo/Bson, `[BsonIgnoreExtraElements]` or `QueuePublishOnly`;
  - `202`, `CommandServices`, `ErrorEvents`, `IReadRepository`, `ViewModelBase` or a rejection event;
  - `[Authorize]`, `HasMany`, `SaveChangesAsync` in a handler, `FOR UPDATE` or an advisory lock;
  - `AddServiceBusProvider`, or a second host.

  Every `SaveAsync`/`UpdateAsync` passes `InMemoryPublishOnly`. The one exception is the audit row in `AuditVaccinationOverrideHandler`,
  which passes `DoNotPublish`, as the precedent allows. No csproj, sln, NuGet.Config or global.json changed, and `Program.cs` is unchanged.
- **Layers.**
  - Domain references no EF Core, Application, Read, Infrastructure or Api type.
  - No changed Application file references Read, Infrastructure or EF Core.
  - No changed Read file references Application.
  - The namespaces use the `Boarding` feature segment.
  - Every value set is an `Enumeration` persisted by `Value`, and every index filter interpolates `.Value`.
- **Controllers.** Every action dispatches one command or one query through `IInMemoryDispatcher`. No controller holds a `DbContext`,
  repository or aggregate.
  - Extend, relocate and check-out use the established command-then-read-back shape.
  - No `202` is returned. Failures are `400` or `409` carrying `ValidationResult`.
- **Aggregates.**
  - State changes happen only in aggregate methods, each ending in `UpdateVersion()`. No handler sets a property.
  - Roots reference each other by id.
  - Unbounded history is in separate roots, not inside the stay: tasks, observations, incidents, media, isolation entries, waitlist
    entries and daycare visits (rule 3).
  - The stay's children are bounded `OwnsMany` collections: cats with their feedings, medications and belongings, plus allocations, rate
    units, status history, referrals, commitments, approvals and surgeries.
- **One aggregate per command.** Every Boarding handler writes exactly one Boarding root.
  - `GenerateCareTasksCommand` saves several new roots of one type. §11.4 C14 specifies that.
  - The Billing charges that check-in, extend, relocate, check-out and the daycare check-out record are not a second Boarding aggregate.
    They are enlisted through Billing's own `IBillableChargeRecorder`, under the HO-BIL-001 producer rule (ADR-178). That port is
    CONTRACTS row 10 and names HO-BRD-001 as a consumer, and ORD-001 set the precedent. See "Billing push model" below.
- **Billing push model (§1.1 mismatch 2, ADR-363).** This complies with the approved cross-area contract.
  - `BoardingCharges.SyncAsync` is the only writer. It reads the stay's charges through the read-only `IBoardingChargeLedger` (projected),
    runs the pure `BoardingChargePlanner.Plan`, voids the uninvoiced charges that no longer apply, and records the new units. Each source
    key is revision-keyed: `(BoardingStay, stayId, "r{rev}.{kind}.{from}-{to}")`.
  - Everything is enlisted before the stay's single `UpdateAsync`, so the charges commit atomically with the stay.
  - When the units are re-derived on extension, relocation with an extension, or early check-out, the stay is still the only Boarding
    root written. An invoiced charge that no longer applies is never changed. It becomes `CreditDue` on the stay.
  - Cycle and final invoices: the worker sends `InvoiceBoardingCycleCommand`. That handler reads the stay and sends exactly one
    `IssueInvoiceFromChargesCommand` through `IInMemoryDispatcher`, with a `BillingGroup` selector. This is the `OrderBilling` precedent.
    Nothing is written to a Billing aggregate directly.
  - TEST: the cycle and final invoices of I-G9 / AC-15, and replaying a cycle key.
- **Deposit as an earmarked Billing payment (ADR-356).**
  - Boarding records no money. `BillingDepositLedger` reads `billing.payment` by earmark `{BoardingStay, stayId}`. The read is unreleased
    only, Accepted or PendingApproval, and projected.
  - Confirm and lapse re-read the ledger inside the handler.
  - `ApplyBillingDepositOutcomeHandler` subscribes to `PaymentRejectedEvent` and mutates only the stay, with no enqueue.
  - There is no `BoardingStayDeposit` type, gateway, card or token field.
  - The remaining gap (Billing does not validate the stay id on `RecordPayment`, P1) is recorded in §1.1 as a merge-time item.
- **Owner-authorised early check-out preview.** `GetEarlyCheckoutPreviewQueryHandler` calls `EarlyCheckout.Compute` and
  `BoardingChargePlanner.Plan`, the same two calls the check-out handler makes, and writes nothing.
  - It passes `outcome.Quote?.Units ?? stay.RateUnits` and `stay.ChargeRevision`. These are the values `CheckOut` → `ApplyQuote` →
    `SyncAsync` will use.
  - TEST: I-G2b pins preview = check-out.
  - Its rehydration of the stay is part of the HIGH.
- **The added `POST …/referrals/{referralId}/return`.**
  - The pattern complies: one command, `RecordReferralReturnCommand`, registered with an `IValidator<T>`.
  - The handler loads the stay by branch, calls the aggregate's `ReturnCatFromClinic`, which raises `CatReturnedFromClinicEvent` and calls
    `UpdateVersion()`, and saves once.
  - It is idempotent on a referral that has already returned.
  - It is recorded in §1.1 and justified: without it an away cat could never be checked out. Contract follow-up: LOW 5.
- **Migrations (6).** Each was authored on the branch, and the integrator regenerates them.
  - They create 25 tables. `RecordClassification.Boarding.cs` classifies all 25.
  - Every Forbidden table (19) has a literal `RetentionGuardSql.AttachTo` in its creating migration, and a matching `DetachFrom` in `Down`.
  - The six `AggregateOnly` tables (`care_routine_time`, `stay_cat`, `stay_cat_feeding`, `stay_cat_medication`, `stay_cat_belonging` and
    `stay_rate_unit`) carry no guard, which is correct.
  - `btree_gist` is installed idempotently.
  - The exclusion constraints `ex_unit_maintenance`, `ex_stay_allocation_unit` and `ex_stay_cat` use `tstzrange(...,'[)')` with
    `WHERE is_active`, and `Down` drops each one.
  - Every root maps `xmin` through `ConfigureAggregateBase`.
  - Money is `numeric(12,2)` plus `char(3)`, and instants are `timestamptz`.
  - `roles.sql` gains `boarding`. That is the RolesSqlConventionTests duty, but see MEDIUM 4.
- **Explicit registration (`Modules/BoardingModule.cs`, ADR-341).**
  - Every command handler, validator, query handler, domain event handler and port class in the diff is registered, all Scoped.
    A script cross-checked every class name.
  - Every validator is registered as `IValidator<TCommand>`.
  - `BoardingWorkCycle` is Scoped, with `AddHostedService<BoardingWorker>()`.
  - Nothing that `AddInMemoryBusServices()` already registers is hand-registered.
  - XCT is reached through its contracts: `IRetentionPurgeSource` `boarding-media` and `IPersonalDataEraser` `boarding`.
- **BoardingWorker** has the ClinicWorker shape:
  - a scope per pass, and a scope per item;
  - one command per item through `IInMemoryDispatcher`;
  - it never throws out of `ExecuteAsync`, and it is off when `Boarding:Worker:Enabled=false`.

  CODE_REVIEW owns whether the Testing hosts actually set that flag.
- **Domain event handlers** stay in the Boarding context, or mutate a root that §8.11 or §8.21 sanctions: `CatWeightRecordAggregate`, and
  the stay on the Surgery events. None of them enqueues, calls `SaveChangesAsync` or calls out of process. Each one does one
  `UpdateAsync` per row.
- **The cross-area edits listed in §1.1 are additive.**
  - **Within §13:**
    - `CatAggregate.EvaluateVaccinationStatus` is read-only, with no `UpdateVersion` and no event.
    - `INotificationLinkBuilder` gains seven default members, and `NotificationLinkBuilder` implements them.
    - `NotificationTemplateCatalogue.cs` gains one spread line; the 11 definitions are in the `.Boarding.cs` partial.
    - `NotificationSubjectKind.BoardingStayInHouse = 11`.
    - The `UniqueConstraintExceptionFilter` rows are §12.5 mappings. The `40P01` → `409` mapping is gated by
      `[BoardingClaimEndpoint]`, the APT precedent, and unknown constraints rethrow.
    - `appsettings.json` gains the `Boarding:Worker` section only.
  - **The calculator.** `CatVaccinationStatusCalculator` delegates to `VaccinationStatusRule`. The algorithm moved verbatim, so the
    output is unchanged. This is §13's `GetCatVaccinationStatusQueryHandler` edit, landing in the file that actually holds the
    algorithm (EMR ADR-133 precedent), and it is recorded in §1.1.
  - **Tests.** The edits to `ErasureCoverageConventionTests` (map row) and `XctRetentionWorkerTests` (a stand-in source replaces the real
    `boarding-media` registration instead of duplicating its key) keep XCT's assertions unchanged. TEST confirms they are green.
  - **Outside §13:** `BusinessSettingsAggregate`, `FilePurposeAccess` and `roles.sql`. See MEDIUM 2-4.
- **Numbering.** No collision was found on master or on any of the 78 `ho/*` branches.
  - ADR-350..369 are used only on ho/BRD-001. BIL-J holds ADR-344..346 on master.
  - CONTRACTS rows 50-59 are used only on ho/BRD-001. BIL-J holds 36-40 and RPT-001-FU1 holds 41.
  - Rows 12-14 gain only a "replaced by row 50/51/52" pointer. They are Boarding-owned contracts, so this follows the CONTRACTS "only the
    owning area may change a contract" rule.

FINDINGS
- [BLOCKER] .ai/flows/boarding-stay-flow.md:3,77-91 — **a §14 forbidden-scope file was edited.** §14 forbids `.ai/**` except
  `decisions/adr.md`.
  - The commit changes the status line and appends a "Built -- HO-BRD-001" section. No owner or integrator decision authorises it,
    and §1.1 does not mention it.
  - The precedent is the same file class: HO-ORD-001 ARCHITECTURE BLOCKER (`order-fulfilment-flow.md`), and HO-BIL-001-J
    (`clinic-visit-flow.md`). Both were closed by reverting.
  - **To close:** restore the master content, so that `git diff master -- .ai/` shows only the `adr.md` append. Proposed row A1 keeps
    the content for the coordinator.
- [HIGH] src/Read/CatEcommerce.Read/QueryHandlers/Boarding/** — **query handlers rehydrate aggregates instead of projecting them**
  (ADR-012; `.ai/read-side-pattern.md` §4 rule 1 "Project, never rehydrate" and rule 2 "`Select` before `ToListAsync`").
  - `GetStayDetailQueryHandler` (BoardingStayQueryHandlers.cs:72-78) says so explicitly: "the aggregate is read whole (it is bounded)
    and mapped on the way out".
  - Sites, file:line:
    - BoardingStayQueryHandlers.cs: 78, 100, 106, 111, 193, 216, 248, 256;
    - BoardingDepartureQueryHandlers.cs: the summary at 37-39 and 60; vet headroom at 86; relocation options at 103; the preview at 138
      and 150. Line 150 is `ToDictionaryAsync` on Billing's `ChargeAggregate` without `Select`;
    - BoardingIncidentQueryHandlers.cs: 22, 40, 50, 73, 74, 102, and the dashboard at 138, 141, 168, 173, 186, 195 and 201;
    - BoardingCareQueryHandlers.cs: 33, 42, 77, 97;
    - BoardingUnitQueryHandlers.cs: 44, 74, 94;
    - BoardingWaitlistDaycareQueryHandlers.cs: 18, 37, 50, 61.
  - Several of these materialise **another context's** aggregate: Billing `ChargeAggregate` and Administration `BranchAggregate`.
  - **Fix:** project with `Select` into the row the ViewModel needs before materialising. The stay's owned collections can be projected
    inside the `Select`.
  - Where a read must run domain logic (the preview, headroom, the quote), give that logic a pure overload over plain values that the
    aggregate method delegates to. That is the SRG-001 `SurgeryCampaignAggregate.Evaluate` precedent. The preview then still runs the
    one `EarlyCheckout.Compute` and `BoardingChargePlanner.Plan`, as the owner's authorisation requires.
  - A delta re-check of `Read/QueryHandlers/Boarding/**` is enough.
- [MEDIUM] 1. docs/handover/HO-BRD-001.md §4.4 / §9 ADR-232 vs ADR-365 — **the push model rewrites a handover ADR, recorded only by
  the builder.**
  - ADR-232 says "No cross-context write from a Boarding command handler", and §4.4 forbids "a write to a Billing … aggregate". ADR-365
    restates this as "limited to Billing's published seams".
  - On substance this is the approved architecture: CONTRACTS row 10, HO-BIL-001 ADR-178 and the ORD-001 precedent. It is not
    blocking.
  - The coordinator should ratify the ADR-232 → ADR-365 reading in §1 Amended, so the ACCEPTANCE gate does not read §4.4 literally.
- [MEDIUM] 2. src/Domain/CatEcommerce.Domain/Aggregates/Administration/BusinessSettingsAggregate.cs:102-106 — **an Administration
  aggregate was edited.** §14 says "no Administration … aggregate is edited", and §13 places the bound in `SettingDefinitions.cs`.
  - The handover contradicts itself. §10.2 asks for "a new bound flag in the registry validator (one line, U-A9)", and the only TimeRange
    validator in the solution is in this aggregate (line 99).
  - The edit is that one rule. It is additive: quiet hours declare no bound, so they still wrap midnight. It is recorded in §1.1.
  - This needs a coordinator or integrator ratification in §1 Amended. **It becomes a BLOCKER if ratification is refused** (the
    BIL-001-J MEDIUM 3 formula). No code change is needed.
- [MEDIUM] 3. src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs:63-66 — **an HO-FIL-001 file outside §13.**
  - It maps `BoardingMedia` → `Boarding.RecordCare` and `BoardingBelongings` → `Boarding.CheckInOut`. Without this, the fail-closed policy
    refuses every role.
  - It is additive, and README C-5 (this slice contributes the two purposes) supports it. It is recorded in §1.1 and CONTRACTS 58.
  - It is not in §14's area list. Ratify it in §1 Amended, and mirror it in HO-FIL-001 §1.
- [MEDIUM] 4. deploy/database/roles.sql:54 — **an integrator-owned file outside §13.** The integrator made the last edit, for RPT's
  `report` schema (5a187b6b).
  - The change is one token (`'boarding'`) that RolesSqlConventionTests requires.
  - Ratify it, or let the integrator re-apply it at merge.
- [MEDIUM] 5. src/Api/CatEcommerce.Api/Modules/BoardingModule.cs — **C-ALL-3 is not met.** Boarding classifies Financial tables (`stay`,
  `rate_card`, `stay_vet_*`, `daycare_*`) and Clinical tables (`care_task`, `care_observation`, `incident`, `isolation_entry`,
  `stay_referral`, `stay_surgery`), but registers no `IRetentionReviewSource`.
  - Every other clinical or financial context provides one: BIL, EMR, IMP, SRG and PHM.
  - Add a count-only review source. Neither the handover nor §1.1 records the omission.
- [MEDIUM] 6. src/Read/CatEcommerce.Read/QueryHandlers/Boarding/BoardingIncidentQueryHandlers.cs:40,
  BoardingWaitlistDaycareQueryHandlers.cs:18,50 — **lists are capped instead of paginated** (read-side-pattern §4 rule 4, SRS UI-4):
  - `GetIncidentsQuery` uses `Take(200)`;
  - the customer waitlist and the customer daycare lists use `Take(100)`.

  Once the cap is reached, older rows silently disappear. Page them with `PagedResult<T>`, as the stay lists already are.
- [LOW] 1. src/Infrastructure/CatEcommerce.Infrastructure/Boarding/BoardingBillingReaders.cs:114 — `BoardingBillableSource` materialises
  Billing's `ChargeAggregate`, untracked, without a projection. Its own comment says no consumer calls it. Project it, or retire the port
  with CONTRACTS row 12/50.
- [LOW] 2. BoardingBillingReaders.cs:91 — `BoardingVetSpendingPolicy` rehydrates the stay (`Include` ×3) to compute headroom. This is an
  untracked read port, not a query handler, the same class as the SRG consent-reader LOW. It moves with the HIGH's pure overload.
- [LOW] 3. BoardingDepartureCommandHandlers.cs:292 — the final-invoice idempotency key contains `Guid.NewGuid()`, so a replay of the final
  invoice is not keyed. Only the "nothing left uninvoiced" selection stops a duplicate. A deterministic key would match the cycle keys.
  Runtime behaviour is for TEST.
- [LOW] 4. RecordClassification.Boarding.cs:30 — `stay_rate_unit` is classified `Financial` but is `AggregateOnly` and replaced
  wholesale. The C-ALL-1 rule for a wholesale-replaced owned list is `CurrentState`. The financial record is the Billing charge.
- [LOW] 5. StaffBoardingDepartureController.cs:33-43 — the added `/referrals/{referralId}/return` route is a contract addition. Mirror it
  in HO-BRD-002 §1 Amended. Once HO-EMR-001 lands, say whether the return should also follow from EMR's visit completion.
- [LOW] 6. **Path translations outside §13's literal list:**
  - `Filters/BoardingClaimEndpointAttribute.cs`;
  - `Infrastructure/Configuration/BoardingOptions.cs`;
  - `RateLimiting/BoardingRateLimitPolicies.cs`;
  - `Retention/Erasers/BoardingPersonalDataEraser.cs` (C-ALL-2);
  - `StaffBoardingDepartureController.cs` and `DaycareControllers.cs` (controller split and grouping);
  - flat `Configurations/Boarding*.cs` instead of `Configurations/Boarding/**`.

  They are helpers or groupings of §13 items, on the ADR-341 and SRG precedent. Note them in §1 Amended.

PROPOSED ROWS FOR docs/backlog/BRD.md (prefix "ARCHITECTURE:"; the integrator assigns the numbers)
- A1 (BLOCKER remedy, record): `.ai/flows/boarding-stay-flow.md`. After the revert, the coordinator applies the branch's "Built --
  HO-BRD-001" text (7ddf9852) under its own authority.
- A2 (MEDIUM 1): ratify the ADR-232 → ADR-365 push-model reading in HO-BRD-001 §1 Amended.
- A3 (MEDIUM 2): ratify the `BusinessSettingsAggregate` TimeRange bound against §14. It becomes a BLOCKER if refused.
- A4 (MEDIUM 3): ratify the `FilePurposeAccess` mapping, and mirror it in HO-FIL-001 §1.
- A5 (MEDIUM 4): `roles.sql` `boarding` schema. The integrator ratifies it or re-applies it.
- A6 (MEDIUM 5): add a count-only Boarding `IRetentionReviewSource` (C-ALL-3).
- A7 (MEDIUM 6): paginate the incidents, customer waitlist and customer daycare lists.
- A8 (LOW 1): project or retire `BoardingBillableSource`.
- A9 (LOW 2): `BoardingVetSpendingPolicy` should use the pure headroom overload.
- A10 (LOW 3): a deterministic final-invoice idempotency key.
- A11 (LOW 4): reclassify `stay_rate_unit` as CurrentState.
- A12 (LOW 5): mirror the referral-return route in HO-BRD-002, and decide the EMR follow-up.
- A13 (LOW 6): note the path translations in §1 Amended.

FOR THE INTEGRATOR (not code findings)
- **Commit trailer.** 2d5f1e38..a5c5b296 (Phases A-E) carry `Co-Authored-By: Claude Opus 5.5 (1M context)`. From 9eb583c7 (Phase F)
  through 95a97bab the trailer is `Co-Authored-By: Claude Sonnet 5.5`. The builder reports a harness reminder as the cause.
- **BIL-001 Phase J is now on master.**
  - The six migrations and the snapshot must be regenerated on top of it.
  - When Phase J's Visit, Dispense and Surgery sources set `BillingGroupKind.BoardingStay` and `BillingGroupId = stayId` (§1.1 item (a)),
    Boarding's cycle invoices will sweep those clinic charges in through the same `BillingGroup` selector. P2 intends that. TEST should
    cover it after the merge.

SCOPE CHECK
- files in allowed scope: 199/203. Files counted in scope:
  - files inside §13's globs and lists;
  - the ADR-341 per-area translations: `Modules/BoardingModule.cs` (for Program.cs), `CatEcommerceDbContext.Boarding.cs`, and the `.Boarding.cs` partials of
    `Actions`, `ActionRoles`, `BusinessViolationCodes`, `SettingDefinitions`, `SettingKeys`, `NotificationTemplateCatalogue`;
  - the CONTRACTS C-ALL files: `RecordClassification.Boarding.cs`, `ClinicalAuditEntityTypes.Boarding.cs` and
    `BoardingPersonalDataEraser.cs`;
  - `CatVaccinationStatusCalculator.cs` (§13's vaccination-status edit, in the file that holds it);
  - the helpers and groupings in LOW 6;
  - `docs/handover/HO-BRD-001.md` (§1 Amended only — the established convention);
  - `docs/handover/CONTRACTS.md` (rows for Boarding-owned contracts only);
  - `docs/test-cases/TC-HO-BRD-001.md`, `.ai/decisions/adr.md` (append only, ADR-350..369) and `tests/**`.
- files outside allowed scope: 4
  - `.ai/flows/boarding-stay-flow.md`: §14, BLOCKER;
  - `src/Domain/CatEcommerce.Domain/Aggregates/Administration/BusinessSettingsAggregate.cs`: §14 conflicts with §10.2/U-A9. MEDIUM 2,
    pending ratification; a BLOCKER if refused;
  - `src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs`: outside §13, not §14. MEDIUM 3;
  - `deploy/database/roles.sql`: outside §13, not §14. MEDIUM 4.
- None of these was touched:
  - `web/**`, `docs/SRS.md`, `docs/USER_STORIES.md` or another handover document;
  - `.claude/**`, or any `*.csproj`, `*.sln`, `NuGet.Config` or `global.json`;
  - any Billing, Inventory or Clinic type or table. Billing is only read (payments, charges, invoices) and written through its
    published port and command.

  No package was added.

---

## Combined re-check -- 2026-10-15 (ARCHITECTURE + SECURITY + CODE_REVIEW findings)

GATE: ARCHITECTURE (combined re-check of the gate-fix round; covers the ARCHITECTURE, SECURITY and CODE_REVIEW first-round findings)
HANDOVER: HO-BRD-001
COMMITS: 5d6f16fc fc6ef889 (fix range 43437fb8..fc6ef889, branch ho/BRD-001, 62 files)
DATE: 2026-10-15
STATUS: PASS

No BLOCKER and no HIGH remain. The first-round BLOCKER (ARCHITECTURE), the read-side HIGH (ARCHITECTURE) and the worker HIGH (CODE_REVIEW) are closed.
Under the owner gate policy (2026-09-27), the MEDIUM and LOW findings below go to docs/backlog/BRD.md.

Inputs: the owner decisions of 2026-10-15 ("Keep everything" for erasure, "On or before" for vaccination). The coordinator ratified BusinessSettingsAggregate,
FilePurposeAccess and roles.sql. ADR-232 versus ADR-365 is left for merge (backlog row 4); this gate does not rule on it again.

Targeted runs (catecommerce_brd, no full suite):
- Domain tests `~Boarding`: 99/99 pass, including U-C10b and U-G22..U-G27.
- Integration: `BoardingGateFixEndpointTests` (I-S1..I-S9), Boarding I-G2b and I-G5, plus the ErasureCoverage, RecordClassification and RolesSql conventions: all pass.
- My filter also caught EMR's `DischargeSummaryAndFollowUpEndpointTests.I_G5`. It failed once (Expected Suppressed, got Expired) and passed 8/8 when its class
  ran alone. The branch touches nothing in that area, and the Boarding worker is now off, so this is an order- or clock-dependent flake. TEST: note it with the 3
  remaining full-run failures.
- The claim that the full run went from 49 failures to 3 was not re-run here (no full suite). TEST confirms it.

VERIFIED (closed)
1. **BLOCKER closed.** `git diff 5a187b6b..fc6ef889 -- .ai` shows only the `decisions/adr.md` append (ADR-350..369 plus the owner-authorised preview note). Merge-base = 5a187b6b.
   `.ai/flows/boarding-stay-flow.md` is byte-identical to the base. It does exist on master (baseline bb9fbf66), so restoring it was the correct remedy. The proposed
   flow text is preserved in HO-BRD-001 §1.1 for the coordinator (row A1).
2. **Read-side HIGH closed.**
   - Sweep of `src/Read/**/QueryHandlers/Boarding/*.cs`: no `Include`. Every `ToListAsync`/`FirstOrDefaultAsync`/`ToDictionaryAsync` sits behind a `Select`, or behind
     `ProjectStay()`/`Project()`/`Rows()`, which are themselves `Select`s. This was checked by a script over each terminal call and its preceding chain: zero unprojected
     sites. Billing `ChargeAggregate` and Administration `BranchAggregate`/`StaffAccountAggregate`/`CustomerAccountAggregate` reads are column projections.
   - Ports: `BoardingVetSpendingPolicy` projects the cat, the approved amounts and the commitments, then calls `VetSpendingHeadroom.Compute(plain)`.
     `BoardingBillableSource` projects the charge columns. `BoardingInputs` rate cards are projected (fc6ef889).
   - Pure overloads, and the aggregate methods delegate to them (checked in the source, not only in the tests):
     - `EarlyCheckout.Compute(stay)` -> `Compute(EarlyCheckoutInput)`;
     - `VetSpendingHeadroom.Compute(StayCat, ...)` -> `Compute(bool, decimal, ...)`;
     - `IsAway`/`AwayReason` -> `StayAwayRule.Reason`;
     - `CurrentAllocation` -> `StayAllocationRule.Current`;
     - `CareTaskAggregate.IsOverdue` -> the static overload;
     - `BoardingCareRoutineAggregate.Effective(aggregate)` -> `Effective(times)`.
   - The preview still calls `EarlyCheckout.Compute` and `BoardingChargePlanner.Plan`, with `outcome.Quote?.Units ?? stay.RateUnits` and `stay.ChargeRevision`. Those
     are the values the check-out handler passes to `SyncAsync`.
   - U-G25 compares the aggregate overload with the plain-value overload for the early branch only. The late branch is not compared at unit level, but it is pinned
     preview-to-result by I-S7 (5 nights in both). Because the aggregate overload is a one-line delegation, the two cannot drift, so U-G25 is enough.
3. **Worker HIGH closed.**
   - `appsettings.Testing.json` gains `Boarding:Worker:Enabled=false`.
   - `CatEcommerceApiFactory` base settings add `["Boarding:Worker:Enabled"] = "false"`, next to the Orders precedent.
   - Boarding tests drive `BoardingWorkCycle` by hand.
4. **Security MEDIUM fixes.**
   - **Deposit earmark (I-S1).** `BillingDepositLedger.GetManyAsync` joins `boarding.stay` on the earmark source id and requires `p.CustomerId == s.CustomerId &&
     p.BranchId == s.BranchId`. `GetAsync` goes through `GetManyAsync`, and confirm, lapse, the worker's ready query and the dashboard all use the ledger, so no
     unscoped path remains. Bypass attempts:
     - another customer's payment: excluded;
     - another branch: excluded;
     - a released earmark: excluded;
     - a rejected payment: excluded.
     What remains is Billing not validating the earmark when a payment is recorded (row 17, merge). That is out of Boarding's reach and is honestly recorded.
   - **Lapsed in refundDue (I-S5).** The dashboard lists `Cancelled && !DepositForfeited` stays with accepted ledger money. A lapse and an enquiry expiry are
     `Cancelled` with a reason. `NoShow` is correctly absent, because `RecordNoShow` always sets `DepositForfeited = true`.
   - **Low incidents (I-S3).** The diary excludes media whose `IncidentId` is a Low incident (an `Any` subquery). `AcknowledgeIncident` returns `IncidentNotFound` for a
     Low incident.
   - **Vet authority (U-G27, I-S2).**
     - The rule is in the aggregate (`SetCareProfile`): staff + not Enquiry + an authorisation or limit change -> reason required (<= `MaxReasonLength`).
     - The aggregate raises `StayVetAuthorityChangedEvent`, which is past tense and carries primitives only.
     - `AuditVetAuthorityChangeHandler` writes one BusinessEvent audit row with the actor, the IP, the reason and before/after, using `DoNotPublish`. This matches the
       `AuditVaccinationOverrideHandler` precedent. It is registered on `IEventHandlerAsync<StayVetAuthorityChangedEvent>`.
     - Staff and customer are told apart by `BranchId is not null`. The customer controller never sets `BranchId`, and `StayAccess` fails closed.
     - **Migration `AddBoardingGateFixes`:** it adds one nullable `varchar(500)` column, `boarding.stay_cat.vet_authority_change_reason`, and `Down` drops it.
       Classification is per table, and `stay_cat` is already classified (Operational/AggregateOnly, no retention guard), so no new classification row or guard is
       needed. The RecordClassification and ErasureCoverage conventions pass. The column holds staff free text, which the owner's "Keep everything" covers. The
       integrator regenerates the migration with the other six (row 42).
   - **Rate limits.** `[EnableRateLimiting(Booking)]` is on Reserve, JoinWaitlist and Book (daycare).
   - **Daycare horizon (I-S4).** `LocalDate > today + bookingHorizonDays` is refused.
   - **Caps of 10 waitlist entries and 30 daycare visits (ruling).** They are untested, and that is acceptable for this gate: each is a single count against a
     constant, the control is self-contained, and the TC document says honestly that they are not exercised. Not blocking. See LOW 3.
5. **Code-review MEDIUM fixes.**
   - **NeedsAttention, then lapse (I-S5).**
     - A worker confirm refused for `VaccinationGateBlocked` calls `FlagAttention(VaccinationGateBlocked)` (an aggregate method with `UpdateVersion`) and saves once.
     - The lapse handler then proceeds for a flagged paid stay.
     - `Confirm` runs `ApplyGates` before it throws, so the save also persists the cats' Blocked status. That is the true state, and it is changed only inside the
       aggregate, so it is not a finding. Residual: LOW 4.
   - **Duplicate catch (I-S6).** The catch goes through the `IUniqueViolationDetector` port. The interface is in Domain/DomainServices with the other Boarding ports,
     and Npgsql appears only in Infrastructure, so the Application layer stays provider-free. The detector is a Singleton: it is stateless and holds no scoped
     dependency, which is compatible with `ValidateScopes`. The handler stops at the first duplicate and returns success. That is safe because the only sender is
     `BoardingWorkCycle`, which uses a scope per item, so the failed tracked row is discarded with the scope.
   - **creditDue netting (ruling).** The planner change, `Math.Max(0, Net - CreditedNet)`, is unit-pinned (U-G22). The new SQL in
     `BoardingChargeLedger.CreditedNetAsync` has no integration test. That SQL joins credit-note lines to invoice lines to charges; credit notes have no status, so
     summing every line is correct. Unit-only is not enough to call the row fully Resolved. It is not blocking. See MEDIUM 2.
   - **BoardingTestVaccineScope (ruling: it does not hide real gate behaviour).**
     - It is a test-only decorator over `IBoardingReferenceReader`. It narrows only the vaccine *input list* to the fixture's `BRD` vaccines.
     - The gate logic itself (`StayCatGate`, `VaccinationStatusRule`, `BoardingVaccinationGate`) runs unchanged.
     - All 7 gate call sites read through that one port.
     - Real blocks are still asserted at integration level: BoardingBooking:165, BoardingWaitlistDaycare:169 and I-S5, which registers a fresh BRD core vaccine.
     - The Boarding classes share `[Collection("Boarding")]`, so I-S5's temporary vaccine cannot leak into a parallel class.
     - The silent re-vaccinate-and-retry, the 25-night cutoff and the gate override for long stays are all gone.
     - The cross-vaccine global list behaviour stays covered by the U-C* unit tests.
   - **I-G5 / I-S9.** The spare unit's id is asserted in `relocationOptions`, and its code in the message.
   - **I-S8.** The invoiced 45-night case passes. Preview credit None equals the stay's `creditDue` null afterwards, and the invoiced month is kept.
6. **Owner decision "On or before" (U-C10b).** `BoardingVaccinationGate` now blocks `Overdue || NextDueOn <= departureOn`, and `Passed = reasons.Count == 0`.
   U-C10b pins -1 and 0 as blocked and +1 as passing. `VaccinationStatusRule` and `CatVaccinationStatusCalculator` have no diff in the fix range. "Keep everything":
   the eraser is unchanged, and backlog row 22 records it as Decided.
7. **Low-severity changes.**
   - The final-invoice key is `boarding-{stay}-final-{sha256(sorted remaining charge ids)[..16]}`. Nothing remaining means success without calling Billing. The key is
     deterministic, and the invoice is still issued only through `IssueInvoiceFromChargesCommand`.
   - The ready-to-confirm worker query is ordered by `ReservedOn, Id`, paged in 500s up to 20 pages, and filtered per page. The escalation query is ordered oldest
     first.
   - `isLate` is an additive, defaulted field on the preview ViewModel.
   - `[BoardingClaimEndpoint]` on check-out maps a 23P01/40P01 there to 409, on the APT and ADR-352 precedent.
   - The `IRetentionReviewSource` `boarding-records` is count-only, projected, and Scoped-registered. It closes C-ALL-3 (row 8).
   - The overstay change: see MEDIUM 1.
8. **No superseded architecture in the fix range.** There is no `IDispatcher`/bus/Bson/`QueuePublishOnly`/`HasMany`/`Include`/`SaveChangesAsync`/202. No csproj,
   sln, `Program.cs`, `web/**`, SRS or USER_STORIES change. The new handlers, ports and sources are all registered in `BoardingModule`, Scoped except the stateless
   detector.

FINDINGS
- [MEDIUM] 1. src/Domain/CatEcommerce.Domain/DomainServices/EarlyCheckout.cs (late branch) + BoardingStayAggregate.Departure.cs:83-88 +
  StaffBoardingDepartureController.cs:154 — **the overstay can refuse the check-out of a cat that is physically leaving, and its charging policy is a builder choice.**
  - A check-out on a day after `DepartureOn` moves the active allocation's `OccupiedTo` to now + turnaround. Allocations stay `is_active` after check-out (ADR-352).
  - So when the unit is booked next inside that window, `ex_stay_allocation_unit` raises 23P01. `[BoardingClaimEndpoint]` turns that into a 409, and the stay stays
    `CheckedIn`.
  - Before this round the check-out always succeeded. Staff can still recover by relocating one of the two stays, so this is not blocking. But it contradicts row
    34's own rationale ("charge was chosen over refusing a cat that is physically in the unit"), and I-S7 has no following booking, so the case is untested.
  - §8 is silent. Charging extra nights changes what a customer pays, and the vaccination gate is not re-evaluated for those nights (extend does re-evaluate). That
    is a business-policy decision.
  - **Ruling:** it needs an owner decision before ACCEPTANCE: charge or not, and what happens on a collision. One option is to charge the nights but never let the
    allocation extension refuse the check-out (for example, extend only up to the next allocation, or flag NeedsAttention). Row 34 should read "Open (owner)", not
    Resolved.
- [MEDIUM] 2. src/Infrastructure/CatEcommerce.Infrastructure/Boarding/BoardingBillingReaders.cs (`CreditedNetAsync`) — **the creditDue netting SQL is untested.**
  U-G22 pins only the planner arithmetic. The credit-note -> invoice-line -> charge join that feeds `CreditedNet` is never run against a database. It does not need
  an end-to-end early check-out: a port-level integration test can issue a Billing credit note against a boarding charge and assert
  `GetRowsAsync(stayId).CreditedNet`. Row 26 should say "Resolved (planner); ledger query untested". TEST.
- [LOW] 3. StayRules.cs / BoardingWaitlistDaycareCommandHandlers.cs:265-281 — the waitlist (10) and daycare (30) caps and the three new rate-limit attributes have no
  test. The cap refusals also reuse unrelated violation codes (`AlreadyWaitlisted`, `StayBeyondHorizon`), so a client keyed on the code shows the wrong message. Add
  one boundary test per cap and a dedicated code. Row 23 should mention that they are untested; the TC document already does.
- [LOW] 4. BoardingLifecycleCommandHandlers.cs:77 / `FlagAttention` — `FlagAttention` is a no-op when the stay is already flagged for another reason. A paid,
  gate-blocked stay that already carries, say, `CatOwnerChanged` therefore still neither confirms nor lapses (CODE M1's condition, though now visible to staff). The
  worker also re-dispatches and re-saves a flagged stay on every pass until it lapses. Consider letting the lapse proceed on a fresh gate evaluation rather than on
  the attention reason.
- [LOW] 5. src/Api/CatEcommerce.Api/appsettings.Testing.json — outside §13's literal list (§13 names `appsettings.json` only). It is the remedy CODE_REVIEW's HIGH
  prescribed, and it is counted in scope here, but §1.1's path-translation list does not name it. Add it.
- [LOW] 6. BoardingIncidentViewModels.cs:77 — `isLate` on the check-out preview is a contract addition. Mirror it in HO-BRD-002 §1 Amended, together with row 14's
  route.
- [LOW] 7. BoardingIncidentQueryHandlers.cs:223 — `refundDue` looks back only 90 days on `CancelledOn`. A bank transfer recorded more than 90 days after a lapse is
  never listed. This is rare, and cash-up should catch it. Record it with row 17.

BACKLOG HONESTY (docs/backlog/BRD.md)
- Rows 1-3, 5-12, 15-16, 18-20, 22, 24, 27-31 and 33 match the code and tests checked above.
- Row 4 (ADR-232/365, merge), row 17 (Billing earmark validation, merge), rows 37-42 (merge or other areas) and row 44 are honest Opens. Row 44 is the
  `BoardingCareInputs` port, which still materialises Administration aggregates; the gate did not list it; LOW class.
- Corrections:
  - row 23: add that the caps and rate limits are untested (LOW 3);
  - row 26: "Resolved (planner); ledger query untested" (MEDIUM 2);
  - row 34: "Open (owner)", plus the collision case (MEDIUM 1).
- Proposed new rows: LOW 4, LOW 5, LOW 6, LOW 7.

SCOPE CHECK
- files in allowed scope: 62/62.
  - `.ai/flows/boarding-stay-flow.md` appears in the range only as the revert, and its net diff against the base is empty.
  - `appsettings.Testing.json` and `CatEcommerceApiFactory.cs` are the remedy CODE_REVIEW prescribed (LOW 5).
  - The new `Infrastructure/Boarding/*` files and the migration follow the existing Boarding translations.
  - `docs/handover/HO-BRD-001.md` changes in §1.1 only; `docs/backlog/BRD.md` and `docs/test-cases/TC-HO-BRD-001.md` are additive.
- files outside allowed scope: none. No `.ai/**` change except the `adr.md` append. No package was added.
