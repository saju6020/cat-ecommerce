# HO-SRG-001 — Surgery: Spay, Neuter and Procedures (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-SRG-001** |
| Title | Surgery — Spay, Neuter and Procedures — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §5 — **US-SRG-01 … US-SRG-11** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-SRG-1 … FR-SRG-11**, FR-CLN-23 (consent, consumed), FR-PHM-7 (controlled register, consumed), FR-IMP-9, FR-IMP-15 (batch deduction, consumed), FR-NTF-3 (surgery pre-op and post-op messages), FR-CAT-2 (neuter status), BR-3, **BR-5**, BR-6, **BR-7**, BR-11, C-2, C-3, D-5, D-7, NFR-LEG-1, NFR-LEG-4, NFR-SEC-2, NFR-SEC-8, UI-5, UI-8, **AC-3**, AC-18, AC-20, Appendix B.2, Appendix C ("Surgery"), Appendix D |
| Pairs with | [`HO-SRG-002`](HO-SRG-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-APT-001`](HO-APT-001.md) (appointments, exclusion constraints, `SlotClaim`, `SendAfter`/`SubjectStamp`), [`HO-ADM-001`](HO-ADM-001.md) (branches, `Theatre` rooms, rosters, leave, services, prices, settings), [`HO-NTF-001`](HO-NTF-001.md) (outbox, templates, guards), [`HO-ACC-001`](HO-ACC-001.md) (staff, roles, sessions, audit), [`HO-CAT-001`](HO-CAT-001.md) (cats, neuter status, weight records) |
| Depends on (not yet written) | **`HO-EMR-001`** (consent forms, visits), **`HO-PHM-001`** (formulary, controlled-drug register, witness confirmation), **`HO-IMP-001`** (batches, stock ledger, clinical stock location). The contracts assumed from each are in §8.13 and are listed as open questions in §9.1 |
| Delivery order | Section 10 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. `.ai/flows/surgery-flow.md` is the flow sketch this slice builds out |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — README C-2 (stock port).** §8.9 and §8.13's I1/I2 were rewritten against HO-IMP-001 as written: the assumed `IStockIssuer` and `IStockLocationDirectory` become the one published port `IStockLedger`, and the location lookup is dropped (the ledger resolves locations from the purpose). A named batch is passed as `ExplicitLines`, which makes HO-IMP-001 rule 5 require an `OverrideReason` when the choice is not FEFO — see OQ-13. |
| Amended (build) | **2026-10-11 -- build-time deviations (branch `ho/SRG-001`, phases A to G; the integrator regenerates the migrations at the merge).** **(1) Consent (section 8.13 E1, ADR-161).** HO-EMR-001 as merged publishes consent as `ConsentRules.IsValidFor(cat, procedure-booking appointment, kind)`, not `IConsentRegistry.HasValidConsentAsync(catId, ProcedureKind, procedureId, kinds)`. `ISurgeryConsentReader` applies EMR's rule to the surgery's appointment for BOTH kinds `Surgery` and `Anaesthesia`; the checklist item is completed or reopened from EMR's consent recorded and withdrawn events. **Owner question: is Anaesthesia consent required as well as Surgery consent?** **(2) Visit (E2).** HO-EMR-001 does not open a visit from `SurgeryAdmittedEvent`; `VisitId` is minted at booking and published only, so the report and the cat history carry a visit id that EMR may not have opened. **(3) Stock (I1 to I3).** HO-IMP-001's ledger takes whole units only in v1 (its OQ-19), so a fractional draw (for example 0.5 vial) is refused with the ledger's own message; a controlled drug logs whole vials. **(4) Deferred by the owner, not built:** HO-EMR-001 Phase F (treatments) and HO-BIL-001 Phase J (automatic charge sources). `GetSurgeryChargeableItemsQuery` and `GetCatSurgeriesQuery` are built and tested but nothing consumes them yet (no endpoint). **(5) Boarding (US-SRG-09) is not built:** HO-BRD-001 was skipped; no boarding type or column exists; the ports it would need are listed in `docs/backlog/SRG.md`. **(6) Names.** `SurgeryFollowUpKind` (not `FollowUpKind`, which HO-EMR-001 owns); `StaffSurgeries` is one controller with the booking, checklist, start, end, recovery and follow-up routes; the pharmacy witness rate-limit policy is reused for the drug log. **(7) Report.** The draft is created by the first `PUT .../report`, not by `EndProcedure` (keeps the end handler inside the recorded surgery-appointment pair); the list endpoint takes `reportOutstanding=true` (OQ-10). **(8) Discharge.** `followUps` omitted means the protocol defaults, an empty list means none, a kind the protocol does not have is `FollowUpOutsideWindow`; a deceased cat is closed out with no follow-ups and no messages. The customer detail shows post-op instructions only once the surgery is `Discharged`. **(9) Day list.** `consentRecorded` on the day list is the checklist flag (cheap, polled); the detail reads consent live. **(10) Campaigns.** The case list is the active branch's; the cap is judged by a count at booking (two simultaneous bookings for the last place can both pass); a duplicate code racing another is caught by the unique index. **(11) Extra files outside the literal list of section 13:** `Infrastructure/Surgery/SurgeryRetentionReviewSource.cs` (HO-XCT-001 C-ALL-3), `Domain/DomainServices/ISurgeryPorts.cs`, `Application/CommandHandlers/Surgery/{SurgeryHandlerSupport,SurgerySlotClaim,SurgeryNotifications,SurgeryProtocols}.cs`, `Api/Controllers/SurgeryControllerBase.cs`, the `NeuterStatusApplication` helper beside the Cat-context handler. **(12) Migrations** `AddAppointmentStaffTime`, `AddSurgeryProtocols`, `AddSurgery`, `AddAnaesthesiaLog`, `AddSurgicalReport`, `AddSurgeryCampaigns` were applied by hand to the per-branch DB `catecommerce_srg`; `ix_surgery_campaign_starts_on` (partial) is hand-written SQL in `AddSurgeryCampaigns`. **(13) `.ai/flows/surgery-flow.md` and `.ai/modules/**` are not updated** (section 14 forbids `.ai/**` except `decisions/adr.md`); the coordinator updates them at the merge. ADR-157 to ADR-173 are appended to `.ai/decisions/adr.md`. **(14) Gate fix round (2026-10-11, after the first ARCHITECTURE FAIL; CODE_REVIEW and SECURITY passed).** **Files outside section 13 (row 30), additions to (11):** `Infrastructure/Surgery/SurgeryPorts.cs` (the Surgery ports' EF implementations, including `SurgeryConsentReader`), `Infrastructure/Surgery/SurgeryCampaignPricing.cs`, `Shared/.../Constants/SettingKeys.Surgery.cs`, and, in HO-APT-001's files, the lines section 11.7 needs that section 13's literal list does not name: the `AppointmentManagedBySurgery` early-return in `AppointmentChangeCommandHandlers.cs` and `AppointmentStaffCommandHandlers.cs` (section 13 says "G6 only"), G1's guards in `AppointmentAggregate.cs`, G7 in `Infrastructure/Scheduling/FlaggableAppointmentFinder.cs`, and G5's calendar fields in `Read/ViewModels/ClinicViewModels.cs` (with the two staff/customer appointment query handlers that fill them). The full HO-APT-001 amendment is recorded in that handover's section 1 (OQ-4 closed). **(15) Staff busy intervals (row 33).** Section 11.7 G4 and section 8.5 say staff busy intervals come from `clinic.appointment_staff_time`; the build reads the appointment's `PerformerId` and `AssistantId` columns in `AppointmentSchedulingInputs`/`SurgerySchedulingInputs` instead, so a lapsed hold never counts. The two are equivalent and the exclusion constraint `ex_appointment_staff_time` is still the race control. **(16) Witness mechanism (row 34, ADR-164).** Section 8.9 and section 11.1 C8 specify a single-use witness confirmation (`WitnessConfirmationId`, `ConsumeAsync`). HO-PHM-001 as merged has no such object: the witness types their own identifier and password into the SAME request and the handler verifies them in-request through `IStaffWitnessVerifier` (HO-PHM-001 ADR-146). The command carries `WitnessIdentifier` and `WitnessPassword`; neither is logged, stored or echoed. **(17) Read side (ADR-012).** `GetAnaesthesiaLogQuery`, `GetSurgeryChargeableItemsQuery`, `GetDischargeDraftQuery`, `GetSurgeryCampaignsQuery` and `GetApplicableCampaignsQuery` project with `Select` and never rehydrate an aggregate; `SurgeryConsentReader` projects EMR's consent columns and applies a new overload of `ConsentRules.IsValidFor` over plain values (an additive edit to HO-EMR-001's file; the aggregate overload delegates to it). `SurgeryCampaignAggregate.Evaluate` gained a static overload over plain values, which the booking and the read both use. **(18) Contract changes in section 12.4:** `GET /api/staff/surgery-campaigns` and `GET /api/customers/me/surgeries` now return `PagedResult<T>` (`{ items, pageNumber, pageSize, totalCount }`; `pageNumber`, `pageSize` query parameters, `pageSize` clamped to 100, `pageSize<=0` and `pageNumber>1000000` are 400); `GET /api/staff/surgery-campaigns/{id}/cases` keeps `cases` and `totals` (totals cover every case) and adds `pageNumber`, `pageSize`, `totalCases`; `GetCatSurgeriesQuery` (no endpoint) returns `PagedResult<T>`. `POST .../anaesthesia-log/drugs` accepts an optional `entryId` (a client-generated GUID, the idempotency key): the same id again returns the first result with no second entry, deduction or register entry; an id used by another record is `409 DuplicateAnaesthesiaEntry`. Two simultaneous corrections of one entry: the loser is `409 AnaesthesiaEntryAlreadyCorrected` (unique partial index `ix_anaesthesia_log_entry_corrects`, migration `UniqueAnaesthesiaCorrection`). `PUT .../recovery-status` past 50 changes is `RecoveryHistoryLimitReached`. Discharge and reschedule read-backs write a clinical-access row. **(19) Migration** `UniqueAnaesthesiaCorrection` (one index made unique) is added; the integrator regenerates it in order with the rest. *(append further rows here)* |
| Amended | **2026-10-11 -- INTEGRATOR DECISION (integrator; the owner may review), closing the ARCHITECTURE re-check BLOCKER on `ho/SRG-001` fix round 8719c96a (SRG backlog 35, 40).** (a) The additive overload `ConsentRules.IsValidFor(status, catId, appointmentId, kind, ...)` in `src/Domain/CatEcommerce.Domain/DomainServices/ConsentRules.cs` (a HO-EMR-001 section 13 file, HO-SRG-001 section 14 forbidden scope) is RATIFIED: additive only, the aggregate overload delegates to it, behaviour unchanged, and it is needed so `SurgeryConsentReader` can project consent columns instead of rehydrating EMR's aggregate (ADR-012). No other edit to an EMR file is authorised by this row. (b) The HO-APT-001 section 1 Amended row written by that fix round (2026-10-11, staff-time table, surgery columns and methods, G4, G5, G7, G8) is RATIFIED as the coordinator's row under HO-SRG-001 OQ-4. Recorded again, as the same decision, in HO-EMR-001 section 1. |
| Amended (HO-BIL-001 Phase J) | **2026-10-14 -- consumer note, no change to this area's files.** HO-BIL-001 Phase J (owner decisions 2026-10-14) now captures a `Discharged` surgery: the procedure fee from `SurgeryAggregate.PriceSnapshot` (only for a procedure that ended `Completed`) and one Medicine charge per anaesthesia-log drug as the log stands after corrections (the leaf of each correction chain, SRG's `GetSurgeryChargeableItemsQuery` rule, re-implemented over `clinic.anaesthesia_log_entry` because Infrastructure cannot reference the Read layer), priced at the SKU price in force when the drug was given. It subscribes to `SurgeryDischargedEvent`; there is no `SurgeryCompletedEvent`. Item (4) of the 2026-10-11 build row ("HO-BIL-001 Phase J deferred") is superseded. The Read-layer `GetSurgeryChargeableItemsQuery` is still unused by Billing. See HO-BIL-001 §1 Amended (Phase J), CONTRACTS row 39. |
| Amended (HO-BIL-001 Phase J gate fix) | **2026-10-14 -- OWNER DECISION "Owner at discharge" (additive SRG edit, owner-authorised; integrator to review).** `SurgeryAggregate.OwnerAtDischargeId` (nullable `uuid` `clinic.surgery.owner_at_discharge_id`, mapped in `SurgeryConfiguration`) is set once by `Discharge` from the cat's owner at that moment (`DischargeSurgeryCommandHandler` passes `cat?.OwnerId` in the new trailing, optional `SurgeryDischargeDto.OwnerAtDischargeId`); `SurgeryDischargedEvent` gains the optional `OwnerAtDischargeId`. `CustomerId` (the booking customer) and every existing event field are unchanged. HO-BIL-001 bills the owner at discharge and follows the merged-survivor chain, as it does for a visit (ADR-344). Files: `Aggregates/Surgery/SurgeryAggregate.cs`, `Events/Surgery/SurgeryEvents.cs`, `Models/SurgeryDtos.cs`, `CommandHandlers/Surgery/SurgeryRecoveryCommandHandlers.cs`, `Configurations/SurgeryConfiguration.cs`; migration `AddVisitBillingFacts`; test `SurgeryAggregateTests.Discharge_StoresTheOwnerAtDischarge_AndCarriesItOnTheEvent`. A surgery discharged before this is billed by hand. The consumer-only note of the earlier row is ratified by the coordinator. |
| Amended (HO-BIL-001 Phase J re-check) | **2026-10-14 -- additive guard.** `clinic.surgery.owner_at_discharge_id` is frozen in the database once set, by the new trigger `tr_surgery_owner_at_discharge_frozen` in migration `AddVisitBillingFacts` (SQLSTATE `CR001`, `BEFORE UPDATE OF owner_at_discharge_id`). No existing SRG trigger or guard is changed. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Let the clinic **schedule, run and document a surgery from booking to the last suture check**, and
make the three things that most often go wrong impossible to get wrong silently:

1. **A theatre, a surgeon and an assistant are all actually free.** Reception books a surgery
   against all three at once. Each clash is refused with its reason (US-SRG-01). Two receptionists
   racing for the same theatre get exactly one booking, because the check is a **PostgreSQL exclusion
   constraint** and not a read-then-write. The constraint mechanism is HO-APT-001's (ADR-068),
   extended to cover the assistant (ADR-158, ADR-159).
2. **No cat goes under without consent.** Starting a surgery re-reads consent from its source of
   truth, HO-EMR-001's consent registry. A checkbox is not enough. The only way past a missing
   consent is a **veterinarian's emergency override with a reason**, stored on the surgery and
   written to the audit log with their identity (BR-5, ADR-161).
3. **Controlled drugs leave an append-only trail.** Logging a controlled drug in the anaesthesia log
   writes three things **in one transaction**: the log entry, a batch-specific stock deduction and a
   witnessed register entry. None of the three can be edited or deleted. A correction is a new entry
   linked to the one it corrects (BR-7, C-2, ADR-162, ADR-163).

Around those three sit the pre-operative checklist, the surgical report (immutable once finalised),
**automatic neuter status** after a spay or neuter, recovery-ward status for reception, post-op
instructions with reminder follow-ups, and campaign pricing for community-cat programmes.

**There is no deposit and no payment at booking.** The SRS asks for no surgery deposit. FR-BRD-7's
deposit belongs to boarding, and appointments take none (HO-APT-001 ADR-073). A surgery carries a
**price snapshot** (standard or campaign) for HO-BIL-001 to invoice. Payment is taken at the clinic
by one of the v1 offline methods (ADR-170).

§17 builds the slice in **seven phases**. Each phase compiles, migrates and passes its own tests
before the next begins.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-SRG-01 | M | 8 | Book a surgery against a theatre, a surgeon and an assistant. Each of the three is checked separately and a clash is refused with its **specific** reason. Races are closed by exclusion constraints. Reschedule and cancel go through the same checks | B |
| US-SRG-02 | M | 5 | A five-item pre-op checklist is created at booking (fasting instructions issued, pre-anaesthetic blood work, weight recorded, consent signed, ASA grade). Outstanding items are named on the theatre day list | C |
| US-SRG-03 | M | 5 | Start is blocked without recorded consent. A vet-only emergency override requires a reason and is audited with the vet's identity | C |
| US-SRG-04 | M | 8 | Anaesthesia log: drugs with dose, route and time; periodic vitals; milestones. Every entry is timestamped and attributed and appears on one timeline | D |
| US-SRG-08 | M | 8 | A controlled drug writes an append-only register entry (vet, cat, dose, wastage, second-staff witness) through HO-PHM-001. Stock is deducted **by batch** through HO-IMP-001. Edits and deletes are refused; corrections are new linked entries | D |
| US-SRG-05 | M | 5 | Surgical report (procedure, findings, technique, suture material, complications, surgeon, assistant), linked to the cat and the visit, **immutable on finalisation**, with addenda | E |
| US-SRG-06 | M | 2 | A completed spay or neuter sets the cat's neuter status and date automatically, through a Cat-context event handler | E |
| US-SRG-10 | S | 3 | Recovery status (recovering / stable / ready for discharge) on a front-desk recovery board, polled every 30 s | F |
| US-SRG-07 | M | 5 | At discharge, post-op instructions are produced from a per-procedure protocol. Follow-ups (wound check ~day 3, suture removal ~day 10–14) are recorded and **queued as reminders** | F |
| US-SRG-11 | C | 5 | Campaign price lists for sterilisation. The campaign price is applied at booking and campaign cases are listed and totalled separately | G |
| US-SRG-09 | S | 5 | **Not delivered here — deferred to HO-BRD-001** (§4.2). This slice delivers the part that belongs to it: nothing blocks a surgery for a boarding cat (D-7), and the surgery events carry what HO-BRD-001 needs to link the stay | — |

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409` | ADR-011, ADR-016, ADR-021 |
| Double booking is prevented by exclusion constraints, never an application lock | HO-APT-001 ADR-068 |
| Surgery bookings are appointments on `Theatre` rooms | HO-APT-001 §4.2 ("Surgery scheduling in theatres") |
| Staff bookings skip the hold; staff-mode availability (a shift outside opening hours counts) | HO-APT-001 ADR-072 |
| Branch-local time through HO-ADM-001's domain services only | HO-ADM-001 ADR-045, ADR-047 |
| Prices are snapshotted at transaction time | HO-ADM-001 ADR-049 |
| Messages go only through `INotificationOutbox.Enqueue`, in the command handler, before the save; never from an event handler | HO-NTF-001 ADR-056, ADR-057 |
| `SendAfter` and `SubjectStamp` on notification requests; subject guards per kind | HO-APT-001 ADR-074 (HO-NTF-001 amendment) |
| Weight history is its own root; current weight is derived | ADR-023 |
| Only a Veterinarian finalises clinical records | BR-6, FR-CLN-24 |
| **No payment gateway in v1; no deposit on clinic bookings** | SRS §10 Phase 5; HO-APT-001 ADR-073 |
| Identifier + password authentication only — no OTP, no 2FA | SRS v2.2, HO-ACC-001 |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens belong to [`HO-SRG-002`](HO-SRG-002.md). Criteria phrased as UI are met here at the contract
level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-SRG-02 "visibly outstanding on the day list" | `GET /api/staff/surgeries/day` returns `outstandingItems[]` for each surgery, by name |
| US-SRG-04 "appear on a timeline" | `GET /api/staff/surgeries/{id}/anaesthesia-log` returns every entry ordered by `occurredOn`, with corrections linked |
| US-SRG-10 "visible on the front-desk screen and updates in near real time" | `GET /api/staff/surgeries/recovery-board` is cheap to poll every 30 s (UI-5) |
| US-SRG-07 "instructions are produced" | The discharge response and `GET /api/customers/me/surgeries/{id}` return the instructions text; printing belongs to the frontend |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Where it lands | What this slice delivers |
|---|---|---|
| **US-SRG-09**: link a surgery to the active boarding stay, charges to the stay invoice, spending-limit check | **HO-BRD-001** (with HO-BIL-001) | No conflict between a surgery and a stay (D-7): the exclusion constraints cover appointments only. `SurgeryScheduledEvent` and `SurgeryDischargedEvent` carry `SurgeryId`, `CatId` and `BranchId`. HO-BRD-001 stores the link on its side and asks HO-BIL-001 to route the charge. A5 in §18 is evidenced there |
| Invoicing the surgery | **HO-BIL-001** | `PriceSnapshot` (amount, basis `Standard`/`Campaign`, `CampaignId?`) on the surgery; `SurgeryDischargedEvent`. **Billable drugs and consumables** from the anaesthesia log are exposed by `GetSurgeryChargeableItemsQuery` (§12.6) |
| Surgery register report (SRS §8.1) and campaign report export (CSV/PDF, FR-RPT-1) | HO-RPT-001 | The tables, plus the on-screen campaign case list of US-SRG-11 (§12.4) |
| The consent capture screen and the stored consent record | **HO-EMR-001 / HO-EMR-002** (US-CLN-21) | The contract consumed in §8.13 E1 |
| Opening the clinical visit a surgery belongs to (SRS §5.1 "belongs to Cat and Visit") | **HO-EMR-001** | `VisitId` minted at booking and published on `SurgeryAdmittedEvent` (§8.13 E2) |
| Surgeries on the cat's clinical history (US-CLN-13) | HO-EMR-001 / HO-EMR-002 | `GetCatSurgeriesQuery` (§12.6) |
| Portal dashboard | HO-POR-001 | `GET /api/customers/me/surgeries` |
| Dose warnings against weight (FR-PHM-2) for surgical drugs | HO-PHM-001, if the owner wants it (OQ-11) | The log shows the cat's latest weight and the dose in mg/kg for information only |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No SMS or email provider** (HO-NTF-001 OQ-1) | Messages are enqueued, rendered and logged. Outside Development only the **in-app copy** is delivered. The checklist item "fasting instructions issued" is therefore a **staff confirmation**, not a delivery receipt (ADR-160) |
| Controlled-drug schedule and register format for the governing jurisdiction (SRS Q-1) | HO-PHM-001 owns the register format; this slice supplies the fields FR-SRG-8 names |
| Community-cat programmes at launch (SRS Q-15) | Phase G is built last and can be dropped without touching A–F (OQ-9) |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, second host, `CommandService` layer, `202 Accepted`, or
`[Authorize]`. **No application lock, advisory lock or `SELECT … FOR UPDATE`** to prevent a clash:
the constraints are the control. **No `UPDATE` or `DELETE` path** for anaesthesia log entries, finalised
reports or register entries, whether through an endpoint, a command, a handler or a migration data fix. **No
deposit, payment, balance or invoice type.** No enqueue from an event handler. **The surgery never writes
`clinic.cat`** — neuter status is set by a Cat-context handler (ADR-166). No direct write to
HO-PHM-001's or HO-IMP-001's tables — only through their enlisting ports (ADR-163).

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| HO-ACC-001, HO-ADM-001, HO-NTF-001 merged | ❌ not implemented at baseline — §20.1 |
| **HO-APT-001 merged**, including its HO-NTF-001 amendment (`SendAfter`, `SubjectStamp`) | ❌ — §20.1 |
| **HO-IMP-001 merged** (batches, `IStockLedger`, clinical stock location) | ✅ written 2026-09-22 — contracts reconciled (README C-2). Phases A–C and E–G do not need it |
| **HO-PHM-001 merged** (formulary, `IControlledDrugRegister`, witness confirmation) | ❌ handover not yet written — §20.2. Needed by phase D only |
| **HO-EMR-001 merged** (consent registry and events; visit opening) | ❌ handover not yet written — §20.2. Needed by phase C (consent) |
| PostgreSQL 17.5 with `btree_gist` | ✅ installed by HO-APT-001's first migration (F28, F29) |
| New NuGet packages | **None** |

The shared-database rules of HO-CAT-001 §16.1, HO-ACC-001 §16.1 and HO-APT-001 §16.1 apply. In
particular, **every test creates its own branch**, with its own theatre and rostered surgeon and
assistant.

---

## 6. Governing architecture documents

Where any of these disagrees with a `.ai/templates/*.cs` file, **the markdown wins**.

| Document | What you need from it |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer responsibilities and reference direction. The ports in §8.12–§8.13 are declared in `Domain.DomainServices` and implemented in Infrastructure; §6 covers the verified platform surface |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | `IInMemoryDispatcher` only; queries never write |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | **`Surgery` is a named context** (`SurgeryAggregate` — checklist, consent, anaesthesia log, report). **BR-5 belongs to `SurgeryAggregate`'s own state.** Rule 1 applies with the recorded exceptions ADR-158 and ADR-163. Rule 3 is why the log and the report are their own roots (ADR-162, ADR-165). Rule 4 is why availability is a pure domain service (`SurgerySlotFinder`) |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | `Surgery` is in the feature-name list. Every namespace in §11–§12 uses it: `Application.Commands.Surgery`, `Domain.Aggregates.Surgery`, `Domain.Events.Surgery`, `Read.Queries.Surgery`, `Application.DomainEventHandlers.Surgery`. Do not invent others |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 are binding.** This slice turns on ADR-014 (violations returned synchronously), ADR-015 (no service layer; domain services only), ADR-016 (`xmin`), ADR-021 (`ValueGeneratedNever`, optional `Tags`/`Language`/`ServiceId`), ADR-022 (`Enumeration` in `Shared`) and ADR-023 (the precedent for separate append-only roots) |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §3 handler shape; §5 when a domain service is warranted (`SurgerySlotFinder`, `PreOpChecklistRules`); **§6 the rule-placement table**. Consent is an aggregate invariant fed by the handler; availability is a domain service; "the witness is a different person" is a handler rule over a port |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | `AsNoTracking()`; project before materialising. The recovery board and day list are polled, so they must be single round-trip projections |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §2 the repository (implemented — do not re-copy); §4 `OwnsMany` for the checklist, follow-ups, status and recovery history, and report addenda; §6 `xmin`; §7 indexes; §8 types (`numeric` for doses); §9 migrations — the constraints and immutability triggers are hand-written SQL in the migration |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §2 runtime-type dispatch; **§3 a failing event handler fails the request after the commit** — why the neuter-status handler catches domain violations and a repair command exists (ADR-166); §4 the outbox — messages are enlisted in the same save; §5 event vs outbox |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | The enumerations of §10.1, persisted by `Value` |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | §3 validators as `IValidator<T>`; §4 `ValidateScopes`/`ValidateOnBuild` stay on |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs` shape; §3 lifetimes (the ports are Scoped); §5 the never-register list |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File order within each phase |
| [`.ai/flows/surgery-flow.md`](../../.ai/flows/surgery-flow.md) | The flow and invariants this slice implements. Its "Remaining work" items are all decided here (§9) |
| [`HO-APT-001`](HO-APT-001.md) | §8.4 the exclusion constraints; §8.5 slot rules; §11.4 `SlotClaim`; §12.5 the `23P01` mapping. **This slice amends HO-APT-001 (§11.7)** |
| [`HO-ADM-001`](HO-ADM-001.md) | §8.2 `RoomKind.Theatre`, the branch-scoping rule; §8.5 `StaffAvailability`; §8.6 services (`Category` `Spay`/`Neuter`, `RequiredRoomKind`) and price snapshots; §8.7 settings registry |
| [`HO-NTF-001`](HO-NTF-001.md) | §8.2 catalogue rules; §8.4 producer rules; §8.7 guards; §8.8 link builder |
| [`HO-CAT-001`](HO-CAT-001.md) | §8.2 `RecordNeuterStatus` and its invariants; `CatWeightRecordedEvent`; §12.1 EF configuration rules. **The reference implementation — match it** |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001
F28–F30 apply. These facts matter most here:

| # | Fact (source) | Consequence in this slice |
|---|---|---|
| F23 | `CommitAsync` saves **every tracked entity** in the scoped context | An appointment, a surgery, notification requests, a stock-ledger entry and a register entry added before one repository save **commit atomically**. ADR-158 and ADR-163 depend on this |
| F24 | Domain events dispatch **after** the commit | The neuter-status handler (ADR-166) and the checklist handlers (ADR-160) run after the surgery or cat change is durable. A failure there cannot roll it back |
| F28 | `EXCLUDE USING gist (… WITH =, tstzrange(…,'[)') WITH &&) WHERE (…)` works; `NULL` ids never conflict; the SQLSTATE is `23P01` | The assistant staff-time constraint (ADR-159) uses the same form |
| F30 | The shared filter maps named `23P01` constraints; unknown ones rethrow | Surgery routes get their own mapping of the same constraint names (§12.5) |

Additionally, and **to verify in phase A** before relying on it (§20.6):

| # | Fact to verify | Why |
|---|---|---|
| F31 | A PostgreSQL `BEFORE UPDATE OR DELETE` row trigger that `RAISE EXCEPTION USING ERRCODE = 'P0001'` surfaces through Npgsql as a `PostgresException` with `SqlState = "P0001"` and the trigger's message, wrapped in `DbUpdateException` | The immutability triggers (ADR-162, ADR-165) are the second line behind the aggregates. The filter maps `P0001` messages that start `immutable:` to `409 RecordIsImmutable` |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Surgery context — namespace segment `Surgery`, schema `clinic`
SurgeryAggregate              (root)  → BranchId, CatId, CustomerId, ServiceId, AppointmentId, VisitId,
                                        SurgeonId, AssistantId, TheatreId, CampaignId?          clinic.surgery
├── Checklist        [owned many = 5] PreOpChecklistItem                                          clinic.surgery_checklist_item
├── FollowUps        [owned many ≤ 5] SurgeryFollowUp                                             clinic.surgery_follow_up
├── StatusHistory    [owned many ≤ 20] SurgeryStatusChange                                        clinic.surgery_status_change
└── RecoveryHistory  [owned many ≤ 50] RecoveryStatusChange                                       clinic.surgery_recovery_change
AnaesthesiaLogEntryAggregate  (root, append-only) → SurgeryId                                     clinic.anaesthesia_log_entry
SurgicalReportAggregate       (root, Id == SurgeryId; immutable once finalised)                  clinic.surgical_report
└── Addenda          [owned many ≤ 50] SurgicalReportAddendum (append-only)                       clinic.surgical_report_addendum
SurgeryProtocolAggregate      (root, one per ServiceId)                                           clinic.surgery_protocol
SurgeryCampaignAggregate      (root)                                                              clinic.surgery_campaign
└── Prices           [owned many ≤ 20] CampaignServicePrice                                       clinic.surgery_campaign_price

Amended here (HO-APT-001's root, §11.7):
AppointmentAggregate  + SurgeryId?, AssistantId?, BookingChannel.Surgery
└── StaffTime        [owned many ≤ 2] AppointmentStaffTime                                        clinic.appointment_staff_time
```

**Why the log and the report are separate roots.** In theatre a nurse logs vitals every few minutes
while the vet logs drugs, and in recovery a nurse changes recovery status while the vet writes the
report. If those were owned by `SurgeryAggregate`, every write would bump the surgery row's `xmin`,
and two people working on one cat at the same time would get `409`s. Separate roots keep writes
independent. They also make **append-only** a property of the table: an entry row is inserted once and
never updated, so a trigger can refuse any change (ADR-162). The same reasoning placed weight history in
its own root (ADR-023).

**One aggregate mutated per command**, with two recorded exceptions, each committed in **one**
`SaveChangesAsync`:
- **Scheduling** (book, reschedule, cancel, end procedure) writes the surgery **and its appointment**
  (ADR-158).
- **Logging a drug** writes the log entry and **enlists** a stock-ledger entry (HO-IMP-001) and, for a
  controlled drug, a register entry (HO-PHM-001) — append-only rows in other contexts, added through
  their ports the way HO-NTF-001 enlists notification requests (ADR-163).

### 8.2 `SurgeryAggregate`

| Field | Type | Notes |
|---|---|---|
| `BranchId` | Guid | HO-ADM-001 §8.2's rule; from the active branch |
| `CatId`, `CustomerId` | Guid | The cat's owner at booking |
| `ServiceId` | Guid | A service whose `RequiredRoomKind == Theatre` (ADR-172) |
| `Procedure` | owned VO `ProcedureSnapshot` | `ServiceName`, `Category`, `DurationMinutes`, `IsSterilisation` (= `Category ∈ {Spay, Neuter}`) — snapshotted at booking |
| `PriceSnapshot` | owned VO | `Amount` (`Money`), `Basis` (`SurgeryPricingBasis`), `CampaignId?`, `CampaignEligibilityNote?` (≤ 500) — ADR-170, ADR-171 |
| `AppointmentId` | Guid | The appointment row that holds the time (ADR-158) |
| `VisitId` | Guid | Minted at booking; HO-EMR-001 opens the visit with this id at admission (§8.13 E2) |
| `TheatreId`, `SurgeonId`, `AssistantId` | Guid | Copied from the appointment; the appointment is authoritative for time and occupancy |
| `StartsOn`, `EndsOn` | `DateTimeOffset` | Planned theatre time; mirrors the appointment |
| `ScheduleStamp` | string | `StartsOn` in round-trip format; changed by every reschedule; used by the pre-op reminder guard (§8.10) |
| `Status` | `SurgeryStatus` | §8.3 |
| `AsaGrade?`, `AsaIsEmergency` | `AsaGrade?` / bool | ASA I–V, with the "E" suffix as a flag |
| `ConsentOverride?` | owned VO | `OverriddenBy`, `OverriddenOn`, `Reason` (10–1000) — BR-5 |
| `AdmittedOn?`, `ProcedureStartedOn?`, `ProcedureEndedOn?`, `DischargedOn?` | instants | |
| `Outcome?` | `ProcedureOutcome?` | `Completed` or `Abandoned`, set by `EndProcedure` |
| `RecoveryStatus?` | `RecoveryStatus?` | Only while `Recovery` |
| `PostOpInstructions?` | string ≤ 4000 | Plain text, snapshotted at discharge from the protocol and edited by staff |
| `CancelledOn?`, `CancelledBy?`, `CancellationReason?` (≤ 500) | | |
| `Checklist`, `FollowUps`, `StatusHistory`, `RecoveryHistory` | owned | §8.1 |

### 8.3 The status machine — Appendix C — ADR-157

```
Scheduled ──Admit──► PreOp ──Start──► InTheatre ──EndProcedure──► Recovery ──Discharge──► Discharged
    │                  │                                            │
    └──Cancel──────────┴──► Cancelled                               └── SetRecoveryStatus (recovering ⇄ stable ⇄ ready)
```

| Method | From | Rules | Event |
|---|---|---|---|
| `Schedule(dto)` | — | §8.5 inputs; creates the five checklist items `Outstanding` (§8.6) | `SurgeryScheduledEvent` |
| `Reschedule(dto)` | `Scheduled` | New slot valid (§8.5); `ScheduleStamp` changes; **date-bound items** revert to `Outstanding` when the local date changes: `FastingInstructionsIssued` always, `WeightRecorded` if its weight falls outside the new window (§8.6) | `SurgeryRescheduledEvent` |
| `Cancel(by, reason, now)` | `Scheduled`, `PreOp` | Reason required | `SurgeryCancelledEvent` |
| `Admit(now, zone)` | `Scheduled` | On the surgery's **local date** in the branch zone, or up to `surgery.earlyAdmissionHours` (12) before `StartsOn` (a cat admitted the evening before) → else `AdmissionNotToday` | `SurgeryAdmittedEvent` |
| `Start(now, consentRecorded, override?)` | `PreOp` | §8.7 | `SurgeryStartedEvent` (+ `SurgeryConsentOverriddenEvent` when overridden) |
| `EndProcedure(outcome, now)` | `InTheatre` | Sets `Outcome`, `ProcedureEndedOn`, `RecoveryStatus = Recovering` | `SurgeryProcedureEndedEvent`; **`SterilisationCompletedEvent`** when `Outcome == Completed` and `Procedure.IsSterilisation` |
| `SetRecoveryStatus(status, note?, now)` | `Recovery` | Any of the three to any other; a change to the same status is a no-op | `RecoveryStatusChangedEvent` |
| `Discharge(dto, now, catIsDeceased)` | `Recovery` | `RecoveryStatus == ReadyForDischarge`, else `NotReadyForDischarge`. **Exception**: a deceased cat may be discharged (closed) from any recovery status, with no follow-ups and no instructions. Otherwise instructions (1–4000) are required and follow-ups are set (§8.10) | `SurgeryDischargedEvent` |
| `CompleteFollowUp(id, now)` / `CancelFollowUp(id, reason)` | `Discharged` | Follow-up `Pending` | `SurgeryFollowUpClosedEvent` |

Any other transition → **`InvalidSurgeryTransition`**, with a message naming both states (*"A surgery
in recovery cannot be admitted."*). Every transition appends to `StatusHistory`. The table lives in
`SurgeryTransitionRules`, which both the aggregate and the detail read use (`allowedTransitions`).

### 8.4 The schedule is an appointment — ADR-158, ADR-159

A surgery's time is held by **one `AppointmentAggregate`** (HO-APT-001), created in the same save:

| Appointment field | Value |
|---|---|
| `PerformerId` | the surgeon |
| `RoomId` | the theatre |
| `CatId`, `CustomerId`, `ServiceId`, `ServiceSnapshot` | as the surgery |
| `AssistantId` *(new)* | the assistant |
| `SurgeryId` *(new)* | the surgery |
| `Channel` | `BookingChannel.Surgery` *(new member)* |
| `Status` | `Booked` from creation to the end of the procedure |

HO-APT-001's three constraints then protect the **surgeon** (`ex_appointment_performer`), the
**theatre** (`ex_appointment_room`) and the **cat** (`ex_appointment_cat`) against every other
appointment and surgery. The **assistant** is protected by a fourth constraint over a new owned table,
`clinic.appointment_staff_time`. Each occupying appointment holds one row per staff member it
occupies: the performer, and the assistant where there is one. HO-APT-001's performer constraint
cannot see the assistant column, and an exclusion constraint cannot compare two different columns.
The owned table is what makes "the nurse assisting at 10:00 is not also doing a vaccination at 10:00"
a database guarantee.

```
clinic.appointment_staff_time (appointment_id, staff_account_id, role, starts_on, ends_on, occupies)
EXCLUDE USING gist (staff_account_id WITH =, tstzrange(starts_on, ends_on, '[)') WITH &&) WHERE (occupies)
```

`occupies` mirrors HO-APT-001 §8.4's occupying predicate: status `Held`, `Booked`, `CheckedIn` or
`InConsultation`, and not a walk-in. It is maintained by one private `SyncStaffTime()` called at the end
of every appointment method that changes status, time, performer or assistant. U-A8 checks that every
such method calls it.

**Coupling rules** — the appointment cannot diverge from the surgery:

| Surgery change | Appointment change, same save |
|---|---|
| `Schedule` | `BookForSurgery` (new factory: staff mode, no hold) |
| `Reschedule` | `MoveForSurgery` (new time, theatre, surgeon, assistant) |
| `Cancel` | `CancelForSurgery` |
| `EndProcedure` | `CompleteSurgerySlot` (new: `Booked → Completed`, frees any remaining planned time) |

Every other appointment method refuses an appointment with `SurgeryId` using
**`AppointmentManagedBySurgery`**: customer and staff reschedule, move and cancel through HO-APT-001's
endpoints, check-in, triage, start, complete and no-show. HO-APT-001's endpoints cannot change a surgery's time
behind its back. Surgery appointments are excluded from the queue, from HO-APT-001's day list and from
the customer's appointment list. They **are** shown on the calendar, carrying `surgeryId`, so reception
sees a vet in theatre (§11.7).

### 8.5 Availability and booking — US-SRG-01 — ADR-158

`SurgerySlotFinder` (Domain.DomainServices, **pure**, no I/O):

```
FindStarts(SurgerySlotRequest) → IReadOnlyList<SurgerySlot(StartsOn, EndsOn)>

SurgerySlotRequest:
  Procedure        (DurationMinutes, QualifiedRoleIds)          // HO-ADM-001 service
  LocalDate, Zone
  Surgeon          (StaffAccountId, RoleIds, AvailableIntervals)
  Assistant        (StaffAccountId, RoleIds, AvailableIntervals)
  Theatre          (RoomId, Kind, IsActive)
  Busy             surgeon → intervals, assistant → intervals, theatre → intervals, cat → intervals
  GridMinutes      booking.slotIntervalMinutes
  EarliestStart    now                                           // staff mode (HO-APT-001 ADR-072)
  ExcludeAppointmentId?                                          // reschedule
```

It also has `Explain(request, startsOn) → IReadOnlyList<SurgeryClash>`, which returns **every** reason a
given start is refused, in this fixed order:

| Code | When |
|---|---|
| `SurgeonNotQualified` | The surgeon's roles do not intersect the service's `QualifiedRoleIds` |
| `AssistantNotQualified` | The assistant holds neither `Nurse` nor `Vet` (`SurgeryRules.AssistantRoleIds`) |
| `AssistantSameAsSurgeon` | Same staff account |
| `TheatreNotAvailable` | The room is not an active `Theatre` in this branch |
| `SurgeonNotAvailable` / `AssistantNotAvailable` | `[s, e)` is not inside one of their `StaffAvailability` intervals (staff mode: a shift outside opening hours counts; leave and closures remove time) |
| `SlotNotOnGrid` | Not aligned to the branch-local grid |
| `SlotInPast` | `s < now` |
| `TheatreBusy` | Overlaps the theatre's busy intervals |
| `SurgeonBusy` | Overlaps the surgeon's busy intervals **as performer or as assistant** |
| `AssistantBusy` | Overlaps the assistant's busy intervals **as performer or as assistant** |
| `CatAlreadyBooked` | Overlaps the cat's appointments |

Busy clashes name the conflicting booking's local time (*"Nurse Akter is assisting another surgery
10:00–11:00."*). US-SRG-01's "each checked and any clash blocks the booking with the reason" is met
by returning **all** of the clashes, not just the first: the `400` carries one violation per clash, each
with its own `propertyName` (`theatreId`, `surgeonId`, `assistantId`, `catId`, `startsOn`).

**Busy intervals** come from `clinic.appointment_staff_time` (staff) and `clinic.appointment` (theatre,
cat), for occupying rows only, **excluding stale holds** (HO-APT-001 §8.4) and the surgery's own
appointment on reschedule. They are loaded by `SurgerySchedulingInputs` (Infrastructure), which reuses
HO-APT-001's `AppointmentSchedulingInputs` for rosters and availability.

**Booking sequence** (`BookSurgeryCommand`, following HO-APT-001's `SlotClaim` shape):

1. Service active, priced, `RequiredRoomKind == Theatre` → else `ServiceNotSurgical` / `ServiceNotBookable`.
2. Cat exists, alive, owned by `CustomerId` → else `CatNotBookable`. **Sex check**: `Spay` on a cat
   recorded `Male`, or `Neuter` on a cat recorded `Female` → `ProcedureSexMismatch`. `Unknown` passes.
3. Sterilisation of a cat already recorded `Neutered` → `CatAlreadyNeutered` unless
   `ConfirmNeuterStatusDiscrepancy = true` (the record may be wrong; the vet decides).
4. `Explain` → any clash → `400` with every clash.
5. Price: campaign (§8.11) if `CampaignId` is given, else HO-ADM-001's price at now.
6. Expire overlapping stale holds (HO-APT-001 ADR-070); `BookForSurgery`; `Schedule` with the minted
   `SurgeryId`, `VisitId` and the protocol's fasting instructions; enqueue messages (§8.10); **one save**.
7. A `23P01` means another booking won the race → the constraint's code (§12.5), `409`.

### 8.6 Pre-operative checklist — US-SRG-02 — ADR-160

Created at `Schedule` with five items, always in this order: `PreOpChecklistItem(Kind, Status,
CompletedBy?, CompletedOn?, Note? ≤ 500, WaiverReason? ≤ 500, ReferenceId?)`.

| Item | How it becomes `Done` | Waivable? |
|---|---|---|
| `FastingInstructionsIssued` | `CompleteChecklistItem` by nurse or vet, with `Note` naming how (message / printed / verbal). Reverts on a date change | No |
| `PreAnaestheticBloodWork` | `CompleteChecklistItem` by nurse or vet, with a note (e.g. results reviewed) | **Yes — vet only, reason required** (common for young healthy cats) |
| `WeightRecorded` | **Automatically** when HO-CAT-001's `CatWeightRecordedEvent` records a weight for the cat dated within `surgery.weightValidityDays` (1) before the surgery's local date, or on it (`ReferenceId` = weight record id). Also completable by command with a `WeightRecordId` that the handler verifies against that window | No |
| `ConsentSigned` | **Automatically** from HO-EMR-001's `ConsentRecordedEvent` for this surgery (§8.13 E1); reverts on `ConsentWithdrawnEvent` (`ReferenceId` = consent id) | **Never** — only the BR-5 override (§8.7) gets past it |
| `AsaGradeAssessed` | `RecordAsaGrade` by a vet | No |

`PreOpChecklistRules` (pure) computes `outstandingItems` and `IsComplete`. **Start requires every item
`Done` or `Waived`**, which is how AC-3's "pre-op checklist enforced" is met. The one exception is
`ConsentSigned`, which may be bypassed only by the override (§8.7). The day list shows outstanding
items by name (US-SRG-02 AC2).

Event-driven completion (weight, consent) runs in `DomainEventHandlers/Surgery/`. The handlers are
idempotent: an item already `Done` with the same reference is skipped. They loop with one `UpdateAsync`
per surgery and **never enqueue**. They only touch surgeries in `Scheduled` or `PreOp`.

### 8.7 Consent and the start gate — US-SRG-03 — BR-5 — ADR-161

`StartSurgeryCommand` handler:

1. Load the surgery (must be `PreOp`).
2. `consentRecorded = await consentRegistry.HasValidConsentAsync(catId, ProcedureKind.Surgery,
   surgeryId, requiredKinds: {Surgery, Anaesthesia})`. **The checklist flag is not consulted.** A
   consent withdrawn a minute ago must stop the start even if the handler that reverts the flag has
   not run yet.
3. `surgery.Start(now, consentRecorded, override)`:
   - consent recorded → start; the override is ignored even if one was sent (and the response says so);
   - not recorded and no override → **`ConsentNotRecorded`** (*"Consent has not been recorded for this
     surgery. Record consent, or a veterinarian may override in an emergency."*);
   - not recorded, override given → requires a caller holding **`Vet`** (checked in the handler from
     `UserContext.Roles` as defence in depth, and by `[RequireAction(Surgery.OverrideConsent)]` on the
     endpoint) and `Reason` of 10–1000 characters → `ConsentOverride` is stored on the aggregate and
     `SurgeryConsentOverriddenEvent` is raised;
   - other outstanding items (not waived) → **`PreOpChecklistIncomplete`**, naming the items.
4. `UpdateAsync`.

`AuditConsentOverrideHandler` writes a `BusinessEvent` audit row: action `SurgeryConsentOverridden`,
actor, surgery, cat and the **reason**. The reason is also on the aggregate, so the surgery detail
shows it permanently. That satisfies US-SRG-03 AC2 twice over: audited with identity, and visible on
the record.

### 8.8 Anaesthesia log — US-SRG-04 — ADR-162

**`AnaesthesiaLogEntryAggregate`** — append-only. Created once, never mutated, never deleted.

| Field | Notes |
|---|---|
| `SurgeryId`, `CatId`, `BranchId` | |
| `Kind` | `AnaesthesiaEntryKind`: `Drug`, `Vitals`, `Milestone`, `Correction` |
| `OccurredOn` | When it happened. Not in the future (+2 min tolerance); not before the surgery's `AdmittedOn − 1 h` |
| `LoggedBy`, `LoggedOn` | From `UserContext` and the clock — **attributed and timestamped** (US-SRG-04 AC1) |
| **Drug**: `MedicineId`, `MedicineName` (snapshot), `IsControlled` (snapshot), `DoseAmount` (`numeric(10,3)` > 0), `DoseUnit`, `Route`, `AdministeredById`, `SkuId?`, `BatchId?`, `BatchNumber?` (snapshot), `LocationId?`, `QuantityDrawn?`, `QuantityWasted?` (≥ 0, ≤ drawn), `StockUnit?`, `WitnessId?`, `RegisterEntryId?`, `StockShortfall` (bool) | §8.9 |
| **Vitals**: `HeartRateBpm?`, `RespiratoryRatePerMin?`, `SpO2Percent?`, `EtCo2MmHg?`, `TemperatureCelsius?` (`numeric(4,1)`), `SystolicMmHg?`, `MeanArterialMmHg?`, `Note?` | At least one value. Outside `AnaesthesiaVitalsRanges` → `ImplausibleVitalValue` unless `ConfirmImplausible = true`, which is stored |
| **Milestone**: `Milestone` (`Premedication`, `Induction`, `Intubation`, `IncisionStart`, `ProcedureEnd`, `Extubation`, `Other`), `Note?` (≤ 500) | |
| **Correction**: `CorrectsEntryId`, `Reason` (10–500), plus the corrected values for the corrected entry's kind | The corrected entry stays visible, marked *corrected by* on read. **A correction of a correction corrects the original's latest state** — the chain is read in order |

The log is open while the surgery is `PreOp`, `InTheatre` or `Recovery` (premedication happens in
pre-op, and monitoring continues in recovery) → else `AnaesthesiaLogClosed`. The handler reads the
surgery's status; it does not mutate the surgery.

**The timeline** (`GetAnaesthesiaLogQuery`) returns every entry ordered by `OccurredOn`, then `LoggedOn`,
with `correctedByEntryId` and the effective values after corrections. Drug and vitals events are
interleaved on that one axis, which is US-SRG-04 AC2.

**Append-only in the database.** The migration adds trigger `trg_anaesthesia_log_entry_immutable`:
`BEFORE UPDATE OR DELETE … RAISE EXCEPTION 'immutable: anaesthesia log entries cannot be changed'
USING ERRCODE = 'P0001'`. No endpoint or command edits or deletes an entry. The trigger catches the
code path nobody intended (F31, I-D6).

### 8.9 Drugs, stock and the controlled register — US-SRG-08 — ADR-163, ADR-164

`LogAnaesthesiaDrugCommand` handler, **one save**:

1. Surgery open for logging (§8.8). Caller holds `Surgery.LogDrug` (Vet).
2. Medicine from `IFormularyLookup` (HO-PHM-001): active → else `MedicineNotFound`. Snapshot the name
   and `IsControlled`.
3. **Stock** — when the medicine has a `SkuId`: `BatchId` and `QuantityDrawn` are required
   (`BatchRequired`). `IStockLedger.DeductAsync(` `StockDemand(BranchId, SkuId, QuantityDrawn,
   Purpose: ClinicalUse, ExplicitLines: [(BatchId, LocationId: null, QuantityDrawn)]),`
   `StockReference(SurgeryConsumable, entryId),` `StockParty(CustomerId, CatId))`:
   - expired, quarantined or recalled batch → **refused, no override** (`BatchNotUsable`, BR-3,
     FR-IMP-9);
   - batch of another SKU → `BatchSkuMismatch`;
   - on-hand below the quantity → the issue is **recorded as a shortfall** (`StockShortfall = true`),
     not refused. The drug has already been given, and the clinical record must exist. HO-IMP-001
     raises its variance or reorder signal (US-CLN-15 AC2's "record the shortfall").
   Medicines without a `SkuId` (e.g. medical oxygen) deduct nothing.
4. **Controlled** — when `IsControlled`:
   - `AdministeredById` must hold `Vet` → `AdministeringVetRequired` (the register names "the
     administering vet");
   - `WitnessId` and `WitnessConfirmationId` required → `WitnessRequired`;
     `IWitnessConfirmationVerifier.ConsumeAsync(confirmationId, witnessId, notStaffId:
     AdministeredById)` → must be valid, unexpired, unused and for that witness → else
     `WitnessConfirmationInvalid`; witness ≠ administering vet and witness ≠ caller → `WitnessSameAsAdministrator`;
   - `QuantityWasted` required (0 allowed) → `WastageRequired`;
   - `IControlledDrugRegister.Enlist(ControlledDrugRegisterEntryRequest(EntryId = registerEntryId,
     MedicineId, SkuId, BatchId, LocationId, CatId, AdministeringVetId, WitnessId,
     QuantityAdministered = QuantityDrawn − QuantityWasted, QuantityWasted, Unit, OccurredOn,
     SourceKind = Surgery, SourceId = SurgeryId, SourceEntryId = entryId, CorrectsEntryId = null))`.
5. Create the entry with `RegisterEntryId`; `SaveAsync`. The ledger entry and the register entry are
   in the same context and commit with it (F23). **If anything throws, none of the three exists.**

**Corrections** (`CorrectAnaesthesiaEntryCommand`) for a drug entry:
- stock delta = corrected `QuantityDrawn` − the original's → `EnlistIssueAsync` for a positive delta,
  `EnlistReturnAsync` for a negative one (same batch; a batch change is a return of the whole original
  plus a new issue);
- controlled: a new register entry with `CorrectsEntryId` = the original register entry, **new witness
  confirmation required**;
- the original entry, ledger entry and register entry are untouched (US-SRG-08 AC2).

**Nobody can edit or delete a register entry through this slice.** It has no path to do so, and
HO-PHM-001 enforces the rule in its table (I-D7 verifies against the real table).

### 8.10 Messages — ADR-169

Templates appended to HO-NTF-001's catalogue. All are Transactional (medical, FR-NTF-4) and all have
an `InApp` channel:

| Key | Urgency | Channels | Enqueued by | Variables |
|---|---|---|---|---|
| `surgery.booked` | **Urgent** — reception books with the owner on the phone or at the desk | Sms, Email, InApp | Book | `catName`, `procedureName`, `startsOnLocal`, `admitByLocal`, `branchName`, `branchAddress`, `fastingInstructions` (Text; Email, InApp), `detailsLink` (Url; every channel) |
| `surgery.pre-op-reminder` | Normal | Sms, Email, InApp | Book, Reschedule | same as above |
| `surgery.rescheduled` | Urgent | Sms, Email, InApp | Reschedule | same + `previousStartsOnLocal` |
| `surgery.cancelled` | Urgent | Sms, Email, InApp | Cancel | `catName`, `procedureName`, `startsOnLocal` |
| `surgery.post-op-instructions` | Urgent — the owner is collecting the cat now | Sms, Email, InApp | Discharge | `catName`, `procedureName`, `followUpSummary` (Text), `detailsLink` (Url; every channel) |
| `surgery.follow-up-due` | Normal | Sms, Email, InApp | Discharge | `catName`, `followUpName`, `dueOn` (Date), `bookingLink` (Url; every channel) |

Formatting follows HO-APT-001 §8.9: `startsOnLocal` uses `ddd d MMM yyyy, HH:mm` in the branch zone;
`admitByLocal` = `StartsOn − surgery.admissionLeadMinutes`. SMS bodies carry the link rather than the
instructions (the SMS body limit is 480 characters). Links come from `INotificationLinkBuilder`:
**`SurgeryLink(surgeryId)`** → `{WebBaseUrl}/portal/surgeries/{id}` and **`FollowUpBookingLink(catId)`**
→ `{WebBaseUrl}/book?catId={catId}&category=FollowUp` (two new methods).

**Timed messages** use HO-APT-001's `SendAfter`, `NotAfter` and `SubjectStamp`, with two new subject kinds and guards:

| Kind | `SendAfter` / `NotAfter` | Guard valid while |
|---|---|---|
| `SurgeryPreOp` | `StartsOn − surgery.preOpReminderHours` (18) / `StartsOn`. Not enqueued if `SendAfter` has already passed | Surgery `Scheduled` **and** `ScheduleStamp` equals the request's stamp, cat alive, owner unchanged |
| `SurgeryFollowUp` (`SubjectRef` = follow-up id) | `DueOn − surgery.followUpReminderLeadDays` (1) at 10:00 branch-local / end of `DueOn` | Follow-up still `Pending`, cat alive, owner unchanged |

**Follow-ups** — US-SRG-07 — are set at `Discharge` from the protocol (§8.12): each has a `Kind`
(`WoundCheck`, `SutureRemoval`, `Recheck`), `DueOn` (local date) and `Status` (`Pending`, `Done`,
`Cancelled`). Staff may change the dates within the protocol's window, or remove the suture-removal
follow-up when absorbable sutures were used. Each follow-up gets **one reminder** ("scheduled as
reminders"). Booking the follow-up appointment is the owner's or reception's ordinary HO-APT booking,
reached through `bookingLink`. `CompleteFollowUp` and `CancelFollowUp` close a follow-up, which makes its
pending reminder fail the guard.

### 8.11 Campaign pricing — US-SRG-11 — ADR-171

**`SurgeryCampaignAggregate`** — `Code` (unique, ≤ 30), `Name` (≤ 200), `Description` (≤ 2000),
`ValidFrom`/`ValidTo` (`DateOnly`, branch-local, inclusive), `BranchIds` (empty = all), `Prices` (one per
**sterilisation** service → else `CampaignServiceNotSterilisation`; `Money` ≥ 0), `MaxCases?`,
`IsActive`. Methods: `Create`, `Update`, `Deactivate`/`Reactivate`. **A price cannot change once a
surgery has been booked under the campaign** (`CampaignPriceLocked`, checked in the handler by a count
query). The snapshot protects past bookings in any case; the lock keeps the report honest.

**At booking**, a campaign applies when the caller passes `CampaignId` and all of these hold: active; the
surgery's local date is within `[ValidFrom, ValidTo]`; the branch is covered; the service has a price;
`MaxCases` is not reached (count of non-cancelled surgeries); `CampaignEligibilityNote` is given (1–500
characters — who qualifies is reception's judgement and must be written down). Each failure has its own code
(`CampaignNotApplicable` with the reason in the message). The snapshot records `Basis = Campaign`.

**Reportable separately**: `GetCampaignCasesQuery` lists the surgeries under a campaign in a date range
(date, cat, procedure, price, status, outcome), with totals and the **standard price of each case for
comparison** (from HO-ADM-001 at the surgery's booking time). `GetSurgeryListQuery` filters by
`pricingBasis`.

### 8.12 Protocols — US-SRG-07 — ADR-167

**`SurgeryProtocolAggregate`** — one per surgical service (`ServiceId` unique): `FastingInstructions`
(≤ 2000), `FastingHours` (0–24), `PostOpInstructions` (≤ 4000), `FollowUps[]` (`Kind`, `DayOffset`
0–60, `WindowFromDay`, `WindowToDay`), `UpdatedBy`, `UpdatedOn`. All text is **plain text** (no markup),
rendered as text by every consumer.

**Defaults** (`SurgeryProtocolDefaults`, Shared) are used when a service has no protocol: fasting "No
food from midnight before surgery; water may be given until {admitBy}" at 8 h; generic post-op text;
follow-ups `WoundCheck` day 3 (window 2–4) and `SutureRemoval` day 12 (window 10–14). These are the
SRS's example values. The clinic replaces them with its own protocols (OQ-5).

### 8.13 Contracts assumed from areas without a handover yet

These are **assumptions**, stated so the owning handover can confirm or amend them (§9.1 OQ-1…OQ-3).
If the owner ships a different shape, the implementer stops (§20.2); they do not adapt silently.

**HO-EMR-001 (Consultation and Medical Records)**

| # | Contract | Used by |
|---|---|---|
| E1 | `IConsentRegistry` (Domain.DomainServices, read-only): `Task<bool> HasValidConsentAsync(Guid catId, ProcedureKind kind, Guid procedureId, IReadOnlySet<ConsentKind> required, CancellationToken)` — true when one or more non-withdrawn consent records, linked to that cat and procedure, together cover every required kind. `ConsentKind` includes `Surgery`, `Anaesthesia`. `ProcedureKind` includes `Surgery`. Events `ConsentRecordedEvent(ConsentId, CatId, ProcedureKind, ProcedureId, Kinds)` and `ConsentWithdrawnEvent(ConsentId, CatId, ProcedureKind, ProcedureId)` | §8.6, §8.7 |
| E2 | On `SurgeryAdmittedEvent(SurgeryId, VisitId, CatId, CustomerId, BranchId, SurgeonId, AdmittedOn)`, HO-EMR-001 opens a `VisitAggregate` with **`Id = VisitId`** and source `Surgery`, idempotently | §8.2 `VisitId` |

**HO-PHM-001 (Pharmacy and Prescriptions)**

| # | Contract | Used by |
|---|---|---|
| P1 | `IFormularyLookup.GetAsync(Guid medicineId)` → `FormularyMedicineInfo(Name, Form, Strength, IsControlled, IsActive, SkuId?, AllowedDoseUnits, DefaultRoutes)` | §8.9 |
| P2 | `IControlledDrugRegister.Enlist(ControlledDrugRegisterEntryRequest)` — adds an append-only register row to the scoped context **without saving**; validates shape; the table refuses `UPDATE`/`DELETE` | §8.9 |
| P3 | `IWitnessConfirmationVerifier.ConsumeAsync(Guid confirmationId, Guid witnessId, Guid notStaffId)` — a witness confirmation is created when the **witness re-enters their own identifier and password** (HO-PHM-001's endpoint); it is single-use and expires in ≤ 5 minutes. No OTP | §8.9 |
| P4 | Enumerations `AdministrationRoute` and `DoseUnit` in `Shared/Common/Enums/` — **reused here, not duplicated** | §8.8 |

**HO-IMP-001 (Imports, Batches and Inventory)**

| # | Contract | Used by |
|---|---|---|
| I1 | **`IStockLedger.DeductAsync(demand, reference, party, ct)`** / **`ReceiveReturnAsync(...)`** → `StockResult<IReadOnlyList<StockMovementLine>>`; refuses expired, quarantined and recalled batches (`BatchExpired` / `BatchNotUsable`); enlists immutable ledger rows (FR-IMP-15) without saving. `StockReference(SurgeryConsumable, SurgeryId)`; `StockParty(CustomerId, CatId)` is **required** for a clinical purpose | §8.9 |
| I2 | **No location lookup.** `StockDemand.LocationIds = null` lets the ledger use every active location of the branch that permits the purpose; HO-IMP-001 §8.3 guarantees one `ClinicDispensary` per branch | §8.9 |
| I3 | A usable-batch read for the batch picker (`GET /api/staff/inventory/batches?skuId&locationId&usable=true`, FEFO-ordered) — consumed by HO-SRG-002, not called here | HO-SRG-002 |

### 8.14 Neuter status — US-SRG-06 — ADR-166

`SetNeuterStatusOnSterilisationHandler` (`DomainEventHandlers/Cat/`, **Cat context**) handles
`SterilisationCompletedEvent(SurgeryId, CatId, CompletedOnLocalDate)`:

1. Load the cat. Deceased → skip, log `Information`, and write a `BusinessEvent` audit row
   `NeuterStatusNotApplied` (reason `CatDeceased`).
2. Already `Neutered` with the same date → skip (idempotent).
3. Otherwise `RecordNeuterStatus(Neutered, CompletedOnLocalDate)` → `UpdateAsync`. HO-CAT-001's
   invariants apply; the audit interceptor records before and after, with the vet from the event's
   `UserContext`. **No manual entry** (US-SRG-06).

It catches `DomainRuleViolationException` and records it as in step 1. Infrastructure exceptions propagate
(`.ai/domain-events-and-outbox.md` §3: the surgery has already ended). The repair path is
**`ApplySterilisationOutcomeCommand(SurgeryId)`**, which calls the same helper. It is idempotent and
available from the surgery detail when `neuterStatusApplied` is false (§12.4). The surgery context never
writes `clinic.cat`.

### 8.15 Ports and domain services

| Name | Kind | Notes |
|---|---|---|
| `SurgerySlotFinder` | pure domain service | §8.5 |
| `SurgeryTransitionRules` | pure | §8.3 |
| `PreOpChecklistRules` | pure | §8.6 |
| `FollowUpPlanner` | pure | Protocol + procedure date → default follow-ups within the windows |
| `SurgerySchedulingInputs` | port, Infrastructure | §8.5 |
| `SurgeryPreOpGuard`, `SurgeryFollowUpGuard` | `INotificationSubjectGuard` | §8.10 |
| `IConsentRegistry`, `IFormularyLookup`, `IControlledDrugRegister`, `IWitnessConfirmationVerifier`, **`IStockLedger`** | ports **owned by other handovers** | §8.13. Declared and implemented there; consumed here |

### 8.16 Exceptions

Reuse `DomainRuleViolationException`. No new type.

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. **This handover owns ADR-157 … ADR-176** (reserved range). It uses
157–173 and leaves 174–176 free.

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-157** | **`Surgery` context** (namespace segment `Surgery`, per `.ai/bounded-contexts.md` and `.ai/namespace-conventions.md`), **schema `clinic`**, shared with Appointments and Consultation. Statuses are exactly Appendix C's: `Scheduled → PreOp → InTheatre → Recovery → Discharged`, `Cancelled`. An abandoned procedure is `Outcome = Abandoned`, not a separate state | HO-APT-001 put appointments under a `Clinic` segment (its ADR-067); `.ai/flows/surgery-flow.md` says "Clinic owns `Surgery`". Surgery follows the governing context list instead, so the choice must be deliberate |
| **ADR-158** | **A surgery's time is an appointment row** (surgeon = performer, theatre = room), created and changed with the surgery **in one save**: a recorded rule-1 exception for the surgery–appointment pair only. The appointment refuses every non-surgery transition (`AppointmentManagedBySurgery`) | HO-APT-001 §4.2 declared this contract. It reuses a race-free mechanism instead of building a second one, and puts surgeries on the calendar |
| **ADR-159** | **Staff occupancy table** `clinic.appointment_staff_time` with exclusion constraint `ex_appointment_staff_time`, maintained by `AppointmentAggregate.SyncStaffTime()`. It is the only way to stop an assistant being double-booked against another appointment's performer | An exclusion constraint compares one column against itself. Assistant against performer needs both in one column |
| **ADR-160** | **Pre-op checklist**: five fixed owned items created at booking. Weight and consent complete from HO-CAT/HO-EMR events. Start requires all items `Done`/`Waived`; only blood work is waivable (vet, reason); consent is never waivable | AC-3 "checklist enforced"; the weight and consent facts live elsewhere, and copying them by hand drifts |
| **ADR-161** | **Consent is re-read from HO-EMR-001's registry at start.** The override is vet-only, needs a 10–1000-character reason, is stored on the aggregate and written as a `BusinessEvent` audit row | BR-5. A checklist flag can be stale; the override must be permanent and attributable |
| **ADR-162** | **Anaesthesia log entries are an append-only root**; corrections are new linked entries; a `BEFORE UPDATE OR DELETE` trigger refuses changes (`P0001`, `immutable:` prefix → `409 RecordIsImmutable`) | Concurrent theatre logging without `409`s; D-5-grade immutability for a clinical record |
| **ADR-163** | **Drug entry + stock issue + controlled register entry commit in one save** through enlisting ports owned by HO-IMP-001 and HO-PHM-001: a recorded rule-1 exception of the outbox kind (append-only rows only). An expired batch is refused with no override; a stock shortfall is recorded, not refused | FR-SRG-8 and US-SRG-08 need all three or none. Refusing to document a drug already given would falsify the clinical record |
| **ADR-164** | **Witness** = a second staff member who **re-enters their own identifier and password** (HO-PHM-001's single-use confirmation, ≤ 5 min). Witness ≠ administering vet ≠ caller | BR-7 with identifier + password only (no OTP) |
| **ADR-165** | **Surgical report** is its own root (`Id = SurgeryId`); finalisation (vet only) makes it immutable in the aggregate and by trigger; corrections are addenda | FR-SRG-5, BR-11, D-5 |
| **ADR-166** | **Neuter status is set by a Cat-context handler** on `SterilisationCompletedEvent`; idempotent; catches domain violations; repair command `ApplySterilisationOutcomeCommand` | FR-SRG-6 without the surgery writing `clinic.cat` (`.ai/flows/surgery-flow.md`) |
| **ADR-167** | **Per-service surgery protocols** (fasting, post-op text, follow-up offsets and windows) with shipped defaults; snapshotted into the surgery at discharge | "Configurable protocol per procedure" (flow remaining work); a later edit must not rewrite the instructions an owner was given |
| **ADR-168** | **Recovery status is a sub-state of `Recovery`** with history; the board is **polled** (30 s), not pushed | FR-SRG-10 "near real time" meets UI-5 without adding a push channel |
| **ADR-169** | **Surgery messages** are enqueued with the change. Pre-op reminders and follow-up reminders use `SendAfter` plus the guards `SurgeryPreOp` (stamp) and `SurgeryFollowUp`. Booked, rescheduled, cancelled and post-op messages are `Urgent` | FR-NTF-3; the recipient is acting at that moment |
| **ADR-170** | **No deposit, no payment on a surgery.** A `PriceSnapshot` with its basis goes to HO-BIL-001 | The SRS asks for none; v1 has no gateway; consistent with HO-APT-001 ADR-073 |
| **ADR-171** | **Campaign price lists** are a separate root with fixed per-service prices, applied by staff at booking with a mandatory eligibility note, locked once used, and reportable per campaign | FR-SRG-11 "distinct price list with its own reporting"; HO-ADM-001's effective-dated price is a single list |
| **ADR-172** | **Theatre services are scheduled only as surgeries**: HO-APT-001's booking paths refuse services whose `RequiredRoomKind == Theatre` (`ServiceNotBookable`) | Otherwise a spay could be booked as an ordinary appointment with no assistant, checklist or consent gate |
| **ADR-173** | **Surgery permissions** are inferred (Appendix D has no Surgery row) from "Appointments", "Clinical notes", "Controlled drug register" and "System configuration" — §12.3 | The mapping is a judgement; it must be visible and reviewable (OQ-6) |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | **HO-EMR-001 must confirm E1–E2**: consent linked by `(ProcedureKind.Surgery, SurgeryId)` with `Kinds` covering `Surgery` and `Anaesthesia`; visit opened from `SurgeryAdmittedEvent` with the supplied `VisitId` | HO-EMR-001 author | As §8.13 |
| OQ-2 | **HO-PHM-001 must confirm P1–P4**, especially that witnessing is a password re-entry by the witness and that the register ports enlist without saving | HO-PHM-001 author | As §8.13 |
| OQ-3 | **HO-IMP-001 must confirm I1–I3**, especially shortfall recording and the clinical location per branch | HO-IMP-001 author | As §8.13 |
| OQ-4 | **HO-APT-001 needs an Amended row** in its §1 for §11.7 (staff-time table, surgery channel and coupling, theatre services refused, calendar/list filters, leave flagging of assistants). This handover may not edit it | Coordinator | §11.7 is binding; the row is appended by whoever maintains HO-APT-001 |
| OQ-5 | Clinical content of the default protocols (fasting, post-op text, follow-up days) | Clinical lead | The SRS examples (§8.12) |
| OQ-6 | Surgery permission mapping (§12.3), notably: may **nurses log non-controlled drugs** (Appendix D gives them only "W (vitals)")? | Owner + clinical lead | No — Appendix D as written; nurses log vitals and milestones |
| OQ-7 | Must **other checklist items besides consent** block start, or only be shown? | Clinical lead | Block, except blood work, which a vet may waive (ADR-160) |
| OQ-8 | Consent validity: is one signed consent valid if the surgery is rescheduled weeks later? | Owner / legal (SRS Q-1) | Yes — consent is linked to the surgery, not its date |
| OQ-9 | Community-cat campaigns at launch (SRS Q-15)? | Owner | Built as phase G; can be switched off by creating no campaign |
| OQ-10 | Is a surgical report required to be finalised before discharge? | Clinical lead | No; unfinalised reports appear on `reportOutstanding` filters |
| OQ-11 | Weight-based dose warnings for surgical drugs (FR-PHM-2 reuse)? | Clinical lead | Information only (mg/kg shown) |
| OQ-12 | Should owners see recovery status in the portal? | Owner | No — reception relays it (FR-SRG-10 says "visible to reception") |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `SurgeryStatus` | `Scheduled`, `PreOp`, `InTheatre`, `Recovery`, `Discharged`, `Cancelled` |
| `PreOpChecklistItemKind` | `FastingInstructionsIssued`, `PreAnaestheticBloodWork`, `WeightRecorded`, `ConsentSigned`, `AsaGradeAssessed` (in checklist order) |
| `ChecklistItemStatus` | `Outstanding`, `Done`, `Waived` |
| `AsaGrade` | `I` (1) … `V` (5), ordered by `Value` |
| `ProcedureOutcome` | `Completed`, `Abandoned` |
| `RecoveryStatus` | `Recovering`, `Stable`, `ReadyForDischarge` |
| `AnaesthesiaEntryKind` | `Drug`, `Vitals`, `Milestone`, `Correction` |
| `AnaesthesiaMilestone` | `Premedication`, `Induction`, `Intubation`, `IncisionStart`, `ProcedureEnd`, `Extubation`, `Other` |
| `FollowUpKind` | `WoundCheck`, `SutureRemoval`, `Recheck` |
| `FollowUpStatus` | `Pending`, `Done`, `Cancelled` |
| `SurgeryPricingBasis` | `Standard`, `Campaign` |
| `StaffTimeRole` | `Performer`, `Assistant` |

Appended to other areas' enumerations (append only): HO-APT-001 `BookingChannel` **`Surgery`**;
HO-NTF-001 `NotificationSubjectKind` **`SurgeryPreOp`**, **`SurgeryFollowUp`**. Reused, not
created: HO-PHM-001's `AdministrationRoute`, `DoseUnit`; HO-EMR-001's `ConsentKind`, `ProcedureKind`.

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`

| Key | Group | Type | Default | Bounds | Branch override |
|---|---|---|---|---|---|
| `surgery.admissionLeadMinutes` | Surgery | Integer | 60 | 0–720 | ✅ |
| `surgery.earlyAdmissionHours` | Surgery | Integer | 12 | 0–24 | ✅ |
| `surgery.preOpReminderHours` | Surgery | Integer | 18 | 0–72 (0 = off) | ✅ |
| `surgery.weightValidityDays` | Surgery | Integer | 1 | 0–7 | ❌ |
| `surgery.followUpReminderLeadDays` | Surgery | Integer | 1 | 0–7 | ❌ |

No migration is needed (values are rows).

### 10.3 Other constants

`SurgeryRules` (`AssistantRoleIds = [Nurse, Vet]`, `MaxAnaesthesiaEntries = 2000`,
`OverrideReasonMin = 10`, `OverrideReasonMax = 1000`, text limits) · `AnaesthesiaVitalsRanges` (feline
plausibility: HR 60–260, RR 4–80, SpO2 70–100, EtCO2 15–80, temp 32.0–42.0, systolic 40–250, MAP
30–200) · `SurgeryProtocolDefaults` · `SurgeryMessages` · the six template definitions of §8.10
appended to `NotificationTemplateCatalogue`.

### 10.4 Violation codes — append to `BusinessViolationCodes`

`SurgeryNotFound` · `InvalidSurgeryTransition` · `ServiceNotSurgical` · `ProcedureSexMismatch` ·
`CatAlreadyNeutered` · `SurgeonNotQualified` · `AssistantNotQualified` · `AssistantSameAsSurgeon` ·
`TheatreNotAvailable` · `SurgeonNotAvailable` · `AssistantNotAvailable` · `SlotInPast` · `TheatreBusy`
· `SurgeonBusy` · `AssistantBusy` · `StaffBusy` · `AdmissionNotToday` · `ConsentNotRecorded` ·
`ConsentOverrideNotPermitted` · `ConsentOverrideReasonRequired` · `PreOpChecklistIncomplete` ·
`ChecklistItemNotWaivable` · `ChecklistItemAlreadyDone` · `WeightRecordNotValidForSurgery` ·
`AnaesthesiaLogClosed` · `ImplausibleVitalValue` · `VitalsRequired` · `MedicineNotFound` ·
`BatchRequired` · `BatchNotUsable` · `BatchSkuMismatch` · `AdministeringVetRequired` · `WitnessRequired` ·
`WitnessConfirmationInvalid` · `WitnessSameAsAdministrator` · `WastageRequired` ·
`CorrectionTargetNotFound` · `RecordIsImmutable` · `ReportNotEditable` · `ReportAlreadyFinalised` ·
`ReportIncomplete` · `NotReadyForDischarge` · `PostOpInstructionsRequired` · `FollowUpOutsideWindow` ·
`FollowUpNotPending` · `CampaignNotApplicable` · `CampaignServiceNotSterilisation` ·
`CampaignPriceLocked` · `AppointmentManagedBySurgery` · `CancellationReasonRequired` (existing).

Reused from HO-APT-001: `SlotNotOnGrid`, `CatNotBookable`, `CatAlreadyBooked`, `ServiceNotBookable`.

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Surgery/`

Namespace `CatEcommerce.Application.Commands.Surgery`. Data only; ids minted in the controller
(including `AppointmentId`, `VisitId` and register/ledger entry ids); `ValidateCommand = true`
everywhere.

| # | Command | Story | Phase |
|---|---|---|---|
| C1 | `UpsertSurgeryProtocolCommand` (`ServiceId`, `FastingInstructions`, `FastingHours`, `PostOpInstructions`, `FollowUps[]`) | US-SRG-07 | A |
| C2 | `BookSurgeryCommand` (`SurgeryId`, `AppointmentId`, `VisitId`, `CustomerId`, `CatId`, `ServiceId`, `StartsOn`, `TheatreId`, `SurgeonId`, `AssistantId`, `CampaignId?`, `CampaignEligibilityNote?`, `ConfirmNeuterStatusDiscrepancy`) — branch from the active branch | US-SRG-01, 02, 11 | B (campaign fields G) |
| C3 | `RescheduleSurgeryCommand` (`SurgeryId`, `ExpectedVersion`, `StartsOn`, `TheatreId`, `SurgeonId`, `AssistantId`) | US-SRG-01 | B |
| C4 | `CancelSurgeryCommand` (`SurgeryId`, `Reason`) | US-SRG-01 | B |
| C5 | `CompleteChecklistItemCommand` (`SurgeryId`, `Kind`, `Note?`, `WeightRecordId?`) · `WaiveChecklistItemCommand` (`SurgeryId`, `Kind`, `Reason`) · `RecordAsaGradeCommand` (`SurgeryId`, `Grade`, `IsEmergency`) | US-SRG-02 | C |
| C6 | `AdmitSurgeryCommand` (`SurgeryId`) | — | C |
| C7 | `StartSurgeryCommand` (`SurgeryId`, `ConsentOverrideReason?`) | US-SRG-03 | C |
| C8 | `LogAnaesthesiaDrugCommand` (`EntryId`, `SurgeryId`, `MedicineId`, `DoseAmount`, `DoseUnit`, `Route`, `OccurredOn`, `AdministeredById`, `BatchId?`, `QuantityDrawn?`, `QuantityWasted?`, `WitnessId?`, `WitnessConfirmationId?`, `RegisterEntryId?`, `LedgerEntryId?`) | US-SRG-04, 08 | D |
| C9 | `LogAnaesthesiaVitalsCommand` (`EntryId`, `SurgeryId`, `OccurredOn`, values…, `ConfirmImplausible`) · `LogAnaesthesiaMilestoneCommand` (`EntryId`, `SurgeryId`, `Milestone`, `OccurredOn`, `Note?`) | US-SRG-04 | D |
| C10 | `CorrectAnaesthesiaEntryCommand` (`EntryId`, `CorrectsEntryId`, `Reason`, corrected values, drug-correction witness and ledger/register ids) | US-SRG-04, 08 | D |
| C11 | `EndSurgicalProcedureCommand` (`SurgeryId`, `Outcome`) | US-SRG-05, 06 | E |
| C12 | `SaveSurgicalReportDraftCommand` (`SurgeryId`, `ExpectedVersion?`, `ProcedurePerformed`, `Findings`, `Technique`, `SutureMaterial`, `HadComplications`, `Complications?`, `SurgeonId`, `AssistantId`) · `FinaliseSurgicalReportCommand` (`SurgeryId`, `ExpectedVersion`) · `AddSurgicalReportAddendumCommand` (`AddendumId`, `SurgeryId`, `Text`) | US-SRG-05 | E |
| C13 | `ApplySterilisationOutcomeCommand` (`SurgeryId`) | US-SRG-06 | E |
| C14 | `SetRecoveryStatusCommand` (`SurgeryId`, `Status`, `Note?`) | US-SRG-10 | F |
| C15 | `DischargeSurgeryCommand` (`SurgeryId`, `PostOpInstructions?`, `FollowUps[]` (`FollowUpId`, `Kind`, `DueOn`)) · `CompleteSurgeryFollowUpCommand` · `CancelSurgeryFollowUpCommand` (`Reason`) | US-SRG-07 | F |
| C16 | `CreateSurgeryCampaignCommand` · `UpdateSurgeryCampaignCommand` · `SetSurgeryCampaignActiveCommand` | US-SRG-11 | G |

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: non-empty ids, `StartsOn` with an offset, text lengths per §8, enum membership, `DoseAmount >
0`, quantities ≥ 0 with `QuantityWasted ≤ QuantityDrawn`, at least one vital value, `Reason` lengths.
Availability, consent, witnessing, stock and transitions are aggregate or handler rules (the
`.ai/command-handler-pattern.md` §6 table).

### 11.3 Mappers and DTOs

`SurgeryMapper`, `AnaesthesiaLogMapper`, `SurgicalReportMapper`, `SurgeryProtocolMapper`,
`SurgeryCampaignMapper` (static `ToDomainDto()` extensions). DTOs in `Domain/Models/`:
`SurgeryScheduleDto`, `ProcedureSnapshot`, `SurgeryPriceSnapshot`, `SurgeryDischargeDto`,
`AnaesthesiaDrugEntryDto`, `AnaesthesiaVitalsEntryDto`, `AnaesthesiaMilestoneEntryDto`,
`AnaesthesiaCorrectionDto`, `SurgicalReportDraftDto`, `SurgeryProtocolDto`, `SurgeryCampaignDto`.

### 11.4 Command handlers — `CommandHandlers/Surgery/`

Every handler that **takes time** (C2, C3) follows one sequence, implemented once in `SurgerySlotClaim`
(a static helper over the ports, not a service layer — the counterpart of HO-APT-001's `SlotClaim`).

| Handler | Specifics |
|---|---|
| **C1** | One protocol per service (upsert); service must be surgical; follow-up windows `WindowFromDay ≤ DayOffset ≤ WindowToDay` |
| **C2 Book** | §8.5; creates appointment + surgery; enqueues `surgery.booked` and `surgery.pre-op-reminder`; **one save** (ADR-158) |
| **C3 Reschedule** | `ExpectedVersion`; `SurgerySlotClaim` excluding its own appointment; `Reschedule` + `MoveForSurgery`; enqueue `surgery.rescheduled` + fresh pre-op reminder (the old one fails the stamp guard) |
| **C4 Cancel** | `Cancel` + `CancelForSurgery`; enqueue `surgery.cancelled` |
| **C5** | `CompleteChecklistItem` refuses `ConsentSigned` (event-driven only) and `AsaGradeAssessed` (use `RecordAsaGrade`); `WeightRecordId` is verified to belong to the cat and fall inside the window; waiver: `PreAnaestheticBloodWork` only, vet only |
| **C6 Admit** | `Admit` with the branch zone |
| **C7 Start** | §8.7 |
| **C8 Drug** | §8.9 |
| **C9 Vitals / milestone** | Surgery open for logging; one new root; `SaveAsync` |
| **C10 Correct** | Target exists, same surgery, not itself superseded by a later correction of the same field set (the chain is read in order); drug corrections per §8.9 |
| **C11 End procedure** | `EndProcedure` + `CompleteSurgerySlot`; **one save** (ADR-158). The report is **not** created here — C12 creates the draft on first save, which keeps C11 inside the recorded surgery–appointment pair |
| **C12 Report** | Draft editable only while not finalised (`ReportNotEditable`); surgery must be `Recovery` or `Discharged`. Finalise: vet only; required fields present (`ReportIncomplete` naming them: procedure performed, findings, technique, suture material, surgeon, assistant, and `Complications` when `HadComplications`). Addendum: vet only; finalised reports only |
| **C13** | §8.14 |
| **C14** | `SetRecoveryStatus` |
| **C15 Discharge** | Loads the protocol, the cat (alive?) and settings; `Discharge`; enqueues `surgery.post-op-instructions` and one `surgery.follow-up-due` per follow-up (§8.10); **one aggregate** |
| **C16** | §8.11 |

### 11.5 Domain events — `Events/Surgery/`

Ids, instants, enums and booleans only. No reasons, notes, instructions or names (U-X1).

`SurgeryScheduledEvent` (`SurgeryId`, `AppointmentId`, `BranchId`, `CatId`, `CustomerId`, `ServiceId`,
`StartsOn`, `PricingBasis`), `SurgeryRescheduledEvent` (+ `PreviousStartsOn`), `SurgeryCancelledEvent`,
`SurgeryAdmittedEvent` (§8.13 E2), `SurgeryStartedEvent` (`ConsentWasOverridden`),
`SurgeryConsentOverriddenEvent`, `SurgeryProcedureEndedEvent` (`Outcome`),
**`SterilisationCompletedEvent`** (`SurgeryId`, `CatId`, `CompletedOnLocalDate`),
`RecoveryStatusChangedEvent`, `SurgeryDischargedEvent` (HO-BIL-001 and HO-BRD-001 contract),
`SurgeryFollowUpClosedEvent`, `AnaesthesiaEntryLoggedEvent` (`EntryId`, `SurgeryId`, `Kind`,
`IsControlled`), `SurgicalReportFinalisedEvent`, `SurgicalReportAddendumAddedEvent`,
`SurgeryCampaignChangedEvent`.

### 11.6 Domain event handlers

| Handler | Folder | Handles | Does |
|---|---|---|---|
| `CompleteWeightItemOnWeightRecordedHandler` | `Surgery/` | HO-CAT-001 `CatWeightRecordedEvent` | §8.6 |
| `CompleteConsentItemOnConsentRecordedHandler` · `RevertConsentItemOnConsentWithdrawnHandler` | `Surgery/` | HO-EMR-001 events (§8.13 E1) | §8.6 |
| `AuditConsentOverrideHandler` | `Surgery/` | `SurgeryConsentOverriddenEvent` | `BusinessEvent` audit row with the reason (§8.7) |
| `AuditControlledDrugEntryHandler` | `Surgery/` | `AnaesthesiaEntryLoggedEvent` where `IsControlled` | `BusinessEvent` row naming the administering vet and the witness |
| `SetNeuterStatusOnSterilisationHandler` | **`Cat/`** | `SterilisationCompletedEvent` | §8.14 |
| `ReassignSurgeriesOnCustomerMergedHandler` | `Surgery/` | HO-ACC-001 `CustomerMergedEvent` | `CustomerId` of the duplicate → the survivor; idempotent |
| `FlagSurgeryOnCatTransferredHandler` | `Surgery/` | HO-CAT-001 `CatOwnershipTransferredEvent` | Future `Scheduled` surgeries: `CustomerId` → the new owner (the guards already stop messages to the old owner) |

All loop with one `UpdateAsync` per row, are idempotent and **never enqueue**.

### 11.7 Amendment to HO-APT-001 — applied here, recorded there (OQ-4)

The same arrangement as HO-APT-001's amendment to HO-NTF-001: the code change and its tests (U-G*, I-G*)
are built in this slice, confined to the lines listed in §13.

| # | Change | Where |
|---|---|---|
| G1 | `AppointmentAggregate`: `SurgeryId?`, `AssistantId?`; owned `StaffTime` (≤ 2) + private `SyncStaffTime()` called by every method that changes status, time, performer or assistant; factory `BookForSurgery`; methods `MoveForSurgery`, `CancelForSurgery`, `CompleteSurgerySlot`; every other method refuses when `SurgeryId` is set (`AppointmentManagedBySurgery`) | Domain |
| G2 | `BookingChannel.Surgery` | Shared |
| G3 | Migration `AddAppointmentStaffTime`: table, **backfill** rows for existing occupying appointments, `ex_appointment_staff_time`, columns `surgery_id`, `assistant_id`, index `(surgery_id)` | Infrastructure |
| G4 | `AppointmentSchedulingInputs`: performer busy intervals are read from `appointment_staff_time` (so a nurse assisting a surgery is busy for ordinary slots) | Infrastructure |
| G5 | Reads: queue, HO-APT day list and customer appointment lists exclude `surgery_id IS NOT NULL`; calendar includes them with `surgeryId` and `assistantId` | Read |
| G6 | `ServiceNotBookable` for `RequiredRoomKind == Theatre` on HO-APT-001's online and staff booking (ADR-172) | Application |
| G7 | `FlagAppointmentsOnLeaveApprovedHandler` also flags where `AssistantId == StaffAccountId` | Application |
| G8 | Filter: `ex_appointment_staff_time` on appointment routes → `409 PerformerBusy` (staff) / `SlotNoLongerAvailable` (customer) | Api |

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

Five new `DbSet`s. One configuration per root; schema `clinic`; HO-CAT-001 §12.1 and ADR-021 conventions
(`ValueGeneratedNever` on every root **and owned** id; `Tags`/`Language`/`ServiceId` optional; `xmin`);
`Money` as `amount numeric(12,2)` + `currency_code char(3)`; doses `numeric(10,3)`; quantities
`numeric(12,3)`.

| Index / constraint | Kind |
|---|---|
| `surgery (branch_id, starts_on)` | day list, list |
| `surgery (cat_id, starts_on)` | cat history |
| `surgery (customer_id)` | portal |
| `surgery (status, branch_id)` | recovery board |
| `surgery (appointment_id)` **UNIQUE** | coupling |
| `surgery (campaign_id, starts_on)` filtered `WHERE campaign_id IS NOT NULL` | campaign report |
| `anaesthesia_log_entry (surgery_id, occurred_on, logged_on)` | timeline |
| `anaesthesia_log_entry (corrects_entry_id)` | correction chain |
| `surgical_report` PK = surgery id | 1:1 |
| `surgery_protocol (service_id)` **UNIQUE** | |
| `surgery_campaign (code)` **UNIQUE** | |
| `trg_anaesthesia_log_entry_immutable` | `BEFORE UPDATE OR DELETE` → `RAISE EXCEPTION 'immutable: …' USING ERRCODE = 'P0001'` |
| `trg_surgical_report_immutable` | `BEFORE UPDATE` when `OLD.finalised_on IS NOT NULL`, and `BEFORE DELETE` always |
| `trg_surgical_report_addendum_immutable` | `BEFORE UPDATE OR DELETE` |

A **finalised** report row can never be updated again, so adding an addendum inserts only into the
addendum table: the root's `Version` is **not** bumped for addenda once finalised. U-E6 guards this. If it
were bumped, the trigger would fire on the first addendum.

**Migrations**, each read before commit (the triggers and the constraint are hand-written SQL):
`AddAppointmentStaffTime` (A), `AddSurgeryProtocols` (A), `AddSurgery` (B), `AddAnaesthesiaLog` (D),
`AddSurgicalReport` (E), `AddSurgeryCampaigns` (G). `Down` drops triggers before tables. **No data
migration ever updates or deletes log, report or addendum rows.**

### 12.2 Infrastructure services

| Class | Notes |
|---|---|
| `Scheduling/SurgerySchedulingInputs` | §8.5; `AsNoTracking`; reuses HO-APT-001 inputs for rosters and availability |
| `Notifications/Guards/{SurgeryPreOpGuard,SurgeryFollowUpGuard}` | §8.10 |
| `Notifications/NotificationLinkBuilder` | `SurgeryLink`, `FollowUpBookingLink` added |

No background worker: timed messages ride HO-NTF-001's `SendAfter`.

### 12.3 Authorization — ADR-173

Appendix D has no Surgery row. The mapping below is inferred and appended to the one `ActionRoles`
table. Administrators get read and configuration access but not clinical writes: Appendix D gives them
"R" on clinical notes and on the controlled drug register.

| Action | Staff roles | Endpoints |
|---|---|---|
| `Surgery.View` | Reception, Nurse, Vet, Administrator | list, day list, detail, availability, log (read), report (read), recovery board, campaign cases |
| `Surgery.Schedule` | Reception, Vet, Administrator | book, reschedule, cancel |
| `Surgery.Admit` | Reception, Nurse, Vet | admit |
| `Surgery.ManageChecklist` | Nurse, Vet | complete checklist items |
| `Surgery.AssessRisk` | Vet | ASA grade, blood-work waiver |
| `Surgery.Start` | Nurse, Vet | start (consent recorded) |
| `Surgery.OverrideConsent` | **Vet** | start with override |
| `Surgery.LogVitals` | Nurse, Vet | vitals, milestones, their corrections |
| `Surgery.LogDrug` | **Vet** | drug entries and their corrections (Appendix D: controlled register W = Vet only) |
| `Surgery.EndProcedure` | Vet | end procedure |
| `Surgery.WriteReport` | Vet | draft, finalise, addendum, apply sterilisation outcome |
| `Surgery.ManageRecovery` | Nurse, Vet | recovery status, discharge, follow-ups |
| `Surgery.ManageProtocols` | Vet, Administrator | protocols |
| `Surgery.ManageCampaigns` | Administrator | campaigns |

Customer endpoints carry `[RequireCustomerSession]` and resolve by id **and caller**. Another
customer's surgery returns **`404`**. Every staff endpoint that reads the log, report or detail is a
clinical-record access and is logged (NFR-SEC-8), using HO-ACC-001's access-log mechanism.

### 12.4 Api — the contract HO-SRG-002 consumes

JSON camelCase; instants ISO-8601 with offset; local dates `yyyy-MM-dd`; money as HO-ADM-001 §12.4.
Every surgery view carries `branchTimeZoneId`. Branch from `X-Branch-Id` (HO-ADM-001 §8.3).

**Staff**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/staff/surgeries/services` | `Surgery.View` | Active, priced theatre services: `{ id, name, category, durationMinutes, price, isSterilisation, qualifiedRoleIds }` |
| `GET` | `/api/staff/surgeries/staff?serviceId` | `Surgery.View` | `{ surgeons: [{ id, name }], assistants: [{ id, name, roleIds }] }` — qualified and assigned to the branch |
| `GET` | `/api/staff/surgeries/availability?serviceId&date&surgeonId&assistantId&theatreId&catId&excludeSurgeryId` | `Surgery.View` | `{ date, branchTimeZoneId, gridMinutes, starts: [{ startsOn, endsOn, outsideOpeningHours }], busy: { theatre: [...], surgeon: [...], assistant: [...] } }` — busy intervals as `{ startsOn, endsOn, label }`, so the form can show why a time is missing |
| `POST` | `/api/staff/surgeries` | `Surgery.Schedule` | C2 → `201 { surgeryId }`; `400` with **one violation per clash**; `409` race code |
| `GET` | `/api/staff/surgeries?from&to&status&surgeonId&pricingBasis&reportOutstanding&pageNumber&pageSize` | `Surgery.View` | paged `SurgerySummary` |
| `GET` | `/api/staff/surgeries/day?date` | `Surgery.View` | The theatre day list, by theatre then start: `surgeryId`, `version`, times, `theatre`, `surgeon`, `assistant`, cat (`name`, `isHighRisk`, `isDeceased`), `customer` (`name`, `phone`), `procedure`, `status`, **`checklist: { isComplete, outstandingItems: [kind] }`**, `consentRecorded`, `needsRescheduling` (from the appointment) — **US-SRG-02 AC2** |
| `GET` | `/api/staff/surgeries/{id}` | `Surgery.View` | Detail: everything in §8.2, checklist items with who and when, `consentRecorded` (live from the registry), `consentOverride?`, `startBlockers[]` (codes and messages the start would return now), **`allowedTransitions[]`**, `statusHistory[]`, `recoveryHistory[]`, `followUps[]`, `priceSnapshot`, `reportStatus` (`None`/`Draft`/`Finalised`), `neuterStatusApplied?` (sterilisations only), `latestWeight` (kg, date) |
| `POST` | `/api/staff/surgeries/{id}/reschedule` · `/cancel` | `Surgery.Schedule` | C3 → `200 { surgery }` · C4 → `204` |
| `POST` | `/api/staff/surgeries/{id}/checklist/{kind}/complete` · `/waive` | `ManageChecklist` · `AssessRisk` | C5 → `204` |
| `PUT` | `/api/staff/surgeries/{id}/asa-grade` | `Surgery.AssessRisk` | C5 → `204` |
| `POST` | `/api/staff/surgeries/{id}/admit` | `Surgery.Admit` | C6 → `204` |
| `POST` | `/api/staff/surgeries/{id}/start` | `Surgery.Start` | C7 without override → `204`; `400 ConsentNotRecorded` / `PreOpChecklistIncomplete` |
| `POST` | `/api/staff/surgeries/{id}/start-with-consent-override` | `Surgery.OverrideConsent` | C7 with `{ reason }` → `204` |
| `GET` | `/api/staff/surgeries/{id}/anaesthesia-log` | `Surgery.View` | `{ entries: [{ id, kind, occurredOn, loggedOn, loggedBy, …kind fields, isCorrected, correctedByEntryId?, correctsEntryId? }], latestWeight, isOpen }` |
| `POST` | `/api/staff/surgeries/{id}/anaesthesia-log/drugs` | `Surgery.LogDrug` | C8 → `201 { entryId, registerEntryId?, stockShortfall }` |
| `POST` | `/api/staff/surgeries/{id}/anaesthesia-log/vitals` · `/milestones` | `Surgery.LogVitals` | C9 → `201 { entryId }` |
| `POST` | `/api/staff/surgeries/{id}/anaesthesia-log/{entryId}/corrections` | `LogDrug` for drug entries, `LogVitals` otherwise (checked in the handler) | C10 → `201 { entryId }` |
| — | **No `PUT`, `PATCH` or `DELETE` exists on any log route** | — | I-D5 asserts `405` |
| `POST` | `/api/staff/surgeries/{id}/end-procedure` | `Surgery.EndProcedure` | C11 → `204` |
| `GET` / `PUT` | `/api/staff/surgeries/{id}/report` | `View` / `WriteReport` | report or `404`; C12 draft → `200 { version }` |
| `POST` | `/api/staff/surgeries/{id}/report/finalise` · `/report/addenda` | `Surgery.WriteReport` | C12 → `204` / `201`; **`PUT` after finalisation → `409 ReportNotEditable`** |
| `POST` | `/api/staff/surgeries/{id}/apply-sterilisation-outcome` | `Surgery.WriteReport` | C13 → `204` |
| `PUT` | `/api/staff/surgeries/{id}/recovery-status` | `Surgery.ManageRecovery` | C14 → `204` |
| `GET` | `/api/staff/surgeries/recovery-board` | `Surgery.View` | `{ generatedOn, entries: [{ surgeryId, catName, catIsHighRisk, customerName, customerPhone, procedure, procedureEndedOn, recoveryStatus, statusChangedOn, statusChangedBy }] }` for surgeries in `Recovery` at the active branch — **US-SRG-10** |
| `GET` | `/api/staff/surgeries/{id}/discharge-draft` | `Surgery.ManageRecovery` | Protocol-filled `{ postOpInstructions, followUps: [{ kind, dueOn, windowFrom, windowTo }] }` |
| `POST` | `/api/staff/surgeries/{id}/discharge` | `Surgery.ManageRecovery` | C15 → `200 { surgery }` |
| `POST` | `/api/staff/surgeries/{id}/follow-ups/{followUpId}/complete` · `/cancel` | `Surgery.ManageRecovery` | C15 → `204` |
| `GET` / `PUT` | `/api/staff/surgery-protocols` · `/api/staff/surgery-protocols/{serviceId}` | `View` / `ManageProtocols` | list with defaults marked / C1 → `200` |
| `GET` / `POST` / `PUT` | `/api/staff/surgery-campaigns[/{id}]` · `POST …/{id}/active` | `View` / `ManageCampaigns` | C16 |
| `GET` | `/api/staff/surgery-campaigns/applicable?serviceId&date` | `Surgery.View` | Campaigns applicable to a booking, with price and remaining cases |
| `GET` | `/api/staff/surgery-campaigns/{id}/cases?from&to` | `Surgery.View` | `{ cases: [...], totals: { count, campaignTotal, standardTotal } }` — **US-SRG-11 "reportable separately"** |

**Customer**

| Method | Route | Access | Query → success |
|---|---|---|---|
| `GET` | `/api/customers/me/surgeries?scope=upcoming\|past` | customer | `{ id, catName, procedure, startsOn, admitBy, status, branch, branchTimeZoneId }` — status is shown as `Scheduled`, `InCare` (for `PreOp`/`InTheatre`/`Recovery`, OQ-12), `Discharged` or `Cancelled` |
| `GET` | `/api/customers/me/surgeries/{id}` | customer | + `fastingInstructions`, `admitBy`, after discharge `postOpInstructions`, `followUps[]` (`kind`, `dueOn`, `status`), `bookFollowUpLink`; `404` if not the caller's |

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes.

### 12.5 Filters

In HO-ACC-001's `UniqueConstraintExceptionFilter`, for **surgery routes** (`/api/staff/surgeries/**`):
`ex_appointment_room` → `409 TheatreBusy`; `ex_appointment_performer` → `409 SurgeonBusy`;
`ex_appointment_staff_time` → `409 StaffBusy` (*"The surgeon or assistant has just been booked
elsewhere. Check availability again."*); `ex_appointment_cat` → `409 CatAlreadyBooked`. Plus G8 for
appointment routes. **`P0001` with an `immutable:` message → `409 RecordIsImmutable`** (F31). Unknown
constraints and other `P0001`s rethrow.

### 12.6 Read side — `src/Read/`

Queries in `Queries/Surgery/`, handlers in `QueryHandlers/Surgery/`, ViewModels flat; explicit
projections, `AsNoTracking()`.

- `GetSurgeryAvailabilityQuery` — `SurgerySchedulingInputs` + `SurgerySlotFinder`. **Never computes
  availability in SQL.**
- `GetSurgeryDayListQuery`, `GetSurgeryListQuery`, `GetSurgeryDetailQuery` (`allowedTransitions` from
  `SurgeryTransitionRules`, `startBlockers` from `PreOpChecklistRules` + the consent registry).
- `GetAnaesthesiaLogQuery`, `GetSurgicalReportQuery`, `GetRecoveryBoardQuery`, `GetDischargeDraftQuery`.
- `GetCatSurgeriesQuery` (HO-EMR history contract), `GetSurgeryChargeableItemsQuery` (HO-BIL contract:
  price snapshot + drug entries with SKU and quantity, effective after corrections).
- `GetCustomerSurgeriesQuery`, `GetCustomerSurgeryDetailQuery`.
- `GetSurgeryProtocolsQuery`, `GetSurgeryCampaignsQuery`, `GetApplicableCampaignsQuery`,
  `GetCampaignCasesQuery`.

The recovery board and the day list are **one query each**, with no N+1 queries (I-F2 counts commands).

### 12.7 Host — `Program.cs`

Register every command handler, validator (as `IValidator<T>`), query handler and event handler in
§11–§12.6; `SurgerySchedulingInputs` (Scoped); the two guards into HO-NTF-001's `SubjectGuardResolver`;
the six templates through the catalogue. **The §8.13 ports are registered by their owning handovers**;
`ValidateOnBuild` fails the host if one is missing, which is the intended signal (§20.2).

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the twelve enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Enums/BookingChannel.cs                                      ← Surgery only
src/Shared/CatEcommerce.Shared/Common/Enums/NotificationSubjectKind.cs                             ← SurgeryPreOp, SurgeryFollowUp only
src/Shared/CatEcommerce.Shared/Common/Constants/{SurgeryRules,AnaesthesiaVitalsRanges,SurgeryProtocolDefaults,SurgeryMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs     ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                               ← the five keys of §10.2 only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs                   ← the six definitions of §8.10 only
src/Domain/CatEcommerce.Domain/Aggregates/Surgery/**
src/Domain/CatEcommerce.Domain/Aggregates/Clinic/AppointmentAggregate.cs                           ← §11.7 G1 only
src/Domain/CatEcommerce.Domain/Entities/{PreOpChecklistItem,SurgeryFollowUp,SurgeryStatusChange,RecoveryStatusChange,SurgicalReportAddendum,CampaignServicePrice,AppointmentStaffTime}.cs
src/Domain/CatEcommerce.Domain/ValueObjects/{ProcedureSnapshot,SurgeryPriceSnapshot,ConsentOverride}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{SurgerySlotFinder,SurgeryTransitionRules,PreOpChecklistRules,FollowUpPlanner,ISurgerySchedulingInputs}.cs
src/Domain/CatEcommerce.Domain/DomainServices/INotificationLinkBuilder.cs                           ← two methods added only
src/Domain/CatEcommerce.Domain/Models/**                                                           ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Surgery/**
src/Application/CatEcommerce.Application/Commands/Surgery/**
src/Application/CatEcommerce.Application/CommandHandlers/Surgery/**                                ← incl. SurgerySlotClaim
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/CommandHandlers/Clinic/**                                 ← §11.7 G6 only
src/Application/CatEcommerce.Application/DomainEventHandlers/Surgery/**
src/Application/CatEcommerce.Application/DomainEventHandlers/Cat/SetNeuterStatusOnSterilisationHandler.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Clinic/FlagAppointmentsOnLeaveApprovedHandler.cs ← §11.7 G7 only
src/Application/CatEcommerce.Application/DataMappers/{SurgeryMapper,AnaesthesiaLogMapper,SurgicalReportMapper,SurgeryProtocolMapper,SurgeryCampaignMapper}.cs
src/Read/CatEcommerce.Read/Queries/Surgery/**
src/Read/CatEcommerce.Read/QueryHandlers/Surgery/**
src/Read/CatEcommerce.Read/QueryHandlers/Clinic/**                                                 ← §11.7 G5 filters only
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs               ← DbSets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{Surgery,AnaesthesiaLogEntry,SurgicalReport,SurgeryProtocol,SurgeryCampaign}Configuration.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/AppointmentConfiguration.cs ← §11.7 G1/G3 mapping only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                           ← the six migrations of §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Scheduling/SurgerySchedulingInputs.cs
src/Infrastructure/CatEcommerce.Infrastructure/Scheduling/AppointmentSchedulingInputs.cs          ← §11.7 G4 only
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Guards/{SurgeryPreOpGuard,SurgeryFollowUpGuard}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs            ← two methods only
src/Api/CatEcommerce.Api/Controllers/{StaffSurgeries,StaffSurgeryAnaesthesia,StaffSurgeryReports,StaffSurgeryProtocols,StaffSurgeryCampaigns,CustomerSurgeries}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Surgery/**
src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs                                ← §12.5 mappings only
src/Api/CatEcommerce.Api/Program.cs                                                                ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-SRG-001.md
docs/gates/HO-SRG-001-*.md
.ai/decisions/adr.md                                                                               ← ADR-157…173, append only
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs. HO-APT-001's Amended row is the coordinator's (OQ-4) |
| `web/**` | HO-SRG-002 |
| Every file of HO-ACC-001, HO-ADM-001, HO-CAT-001, HO-NTF-001 and HO-APT-001 beyond the lines in §13 | Their areas. **`CatAggregate` is not edited**; neuter status uses its existing `RecordNeuterStatus` |
| Every file of HO-EMR-001, HO-PHM-001 and HO-IMP-001, **including the ports of §8.13** | Consumed, not built. Stubbing or implementing them here is a §20.2 stop |
| Any payment, deposit, invoice or balance type | ADR-170; Billing is HO-BIL |
| Any boarding-stay type or column | US-SRG-09 is HO-BRD-001's |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |

---

## 15. Unit test cases

| ID | Case | Expected |
|---|---|---|
| **A — status machine and foundations** | | |
| U-A1 | `SurgeryTransitionRules` over every `(from, action)` pair | Only §8.3's arrows; every other pair `InvalidSurgeryTransition` naming both states |
| U-A2 | Every transition | One `StatusHistory` row |
| U-A3 | `Admit` the day before at 14:00 for 09:00 surgery (early 12 h); at 20:00 | `AdmissionNotToday`; allowed |
| U-A4 | `Cancel` from `InTheatre` | `InvalidSurgeryTransition` |
| U-A5 | Protocol with `DayOffset` outside its window | Refused |
| U-A6 | `FollowUpPlanner` with the defaults for a surgery on 10 Oct | Wound check 13 Oct; suture removal 22 Oct |
| U-A7 | Settings bounds for the five keys | Enforced by the registry |
| U-A8 | Reflection: every `AppointmentAggregate` method that changes `Status`, `StartsOn`, `PerformerId` or `AssistantId` calls `SyncStaffTime` (verified by exercising each and checking `StaffTime`) | Rows match the occupying predicate after every call |
| **B — scheduling** | | |
| U-B1 | `Explain` with surgeon busy, assistant on leave and theatre busy | Three clashes in the fixed order, each with its property and a message naming the time |
| U-B2 | Assistant who is a Receptionist; assistant = surgeon; nurse surgeon for a vet-only service | `AssistantNotQualified`; `AssistantSameAsSurgeon`; `SurgeonNotQualified` |
| U-B3 | Assistant busy as **performer** of a vaccination 10:00–10:15; surgeon busy as **assistant** elsewhere | `AssistantBusy`; `SurgeonBusy` |
| U-B4 | 60-min procedure, grid 15, all free 09:00–12:00 | Starts 09:00…11:00 |
| U-B5 | A stale hold overlapping the surgeon | Ignored |
| U-B6 | Spay for a `Male` cat; neuter for `Female`; `Unknown` | `ProcedureSexMismatch` ×2; allowed |
| U-B7 | Sterilisation for a `Neutered` cat without / with confirmation | `CatAlreadyNeutered`; allowed |
| U-B8 | `Reschedule` changes `ScheduleStamp`, reverts fasting, keeps a weight still in the window, reverts one outside it | As stated |
| U-B9 | `SurgeryPreOpGuard`: scheduled + same stamp; rescheduled; cancelled; cat deceased | Valid; invalid; invalid; invalid |
| U-B10 | Pre-op reminder when `StartsOn − 18 h` is already past | Not enqueued |
| U-B11 | Template definitions | Pass HO-NTF-001 `TemplateRules`; urgencies as §8.10; SMS bodies contain the Url variable |
| **C — checklist and start** | | |
| U-C1 | New surgery | Five items `Outstanding`, in order |
| U-C2 | Waive `PreAnaestheticBloodWork` as vet with reason; waive `ConsentSigned`; waive `FastingInstructionsIssued` | Waived; `ChecklistItemNotWaivable` ×2 |
| U-C3 | `Start` with consent recorded, all items done | `InTheatre` |
| U-C4 | `Start` with consent **not** recorded, no override | `ConsentNotRecorded` — **US-SRG-03 AC1** |
| U-C5 | `Start` with override, reason 9 chars; 10 chars | `ConsentOverrideReasonRequired`; started, `ConsentOverride` stored, `SurgeryConsentOverriddenEvent` — **US-SRG-03 AC2** |
| U-C6 | Consent recorded, weight outstanding | `PreOpChecklistIncomplete` naming *weight recorded* |
| U-C7 | Checklist says consent `Done` but the registry says no (withdrawn) | `ConsentNotRecorded` — the registry wins (ADR-161) |
| U-C8 | Weight handler with a weight 2 days before (validity 1); same day; repeated event | Not completed; completed; no second change |
| **D — anaesthesia log** | | |
| U-D1 | Drug entry records `LoggedBy`, `LoggedOn` from context and clock | As stated — **US-SRG-04 AC1** |
| U-D2 | `OccurredOn` 5 min in future; before admission − 1 h | Refused |
| U-D3 | Vitals with none set; HR 300 unconfirmed; confirmed | `VitalsRequired`; `ImplausibleVitalValue`; stored with the flag |
| U-D4 | Controlled drug: nurse as administering; no witness; witness = administering vet; wastage missing | `AdministeringVetRequired`; `WitnessRequired`; `WitnessSameAsAdministrator`; `WastageRequired` |
| U-D5 | Controlled drug valid (fake ports) | One entry, one stock issue of `QuantityDrawn`, one register request with administered = drawn − wasted |
| U-D6 | Stock port reports expired batch | Refused `BatchNotUsable`; register not enlisted — **BR-3** |
| U-D7 | Stock port reports shortfall | Entry saved with `StockShortfall = true` |
| U-D8 | Correction of a controlled dose from 0.5 to 0.4 mL drawn | New entry; stock **return** 0.1; new register entry with `CorrectsEntryId`; original untouched — **US-SRG-08 AC2** |
| U-D9 | Timeline ordering with interleaved drug, vitals and a correction | By `OccurredOn` then `LoggedOn`; corrected entry flagged; effective values applied — **US-SRG-04 AC2** |
| U-D10 | Log when surgery `Scheduled` / `Discharged` | `AnaesthesiaLogClosed` |
| **E — report and neuter status** | | |
| U-E1 | `EndProcedure(Completed)` on a spay | `Recovery`, `Recovering`, `SterilisationCompletedEvent` with the local date |
| U-E2 | `EndProcedure(Abandoned)` on a spay; `Completed` on a dental | No sterilisation event ×2 |
| U-E3 | Finalise with suture material empty; complications flagged but empty | `ReportIncomplete` naming the fields |
| U-E4 | Draft after finalise | `ReportNotEditable` |
| U-E5 | Finalise as nurse (handler guard) | Refused |
| U-E6 | Addendum on a finalised report | Addendum row added; root `Version` unchanged (the trigger would fire otherwise) |
| U-E7 | Neuter handler: intact cat; already neutered same date; deceased cat | Neutered with date; no change; skipped with a `NeuterStatusNotApplied` audit row — **US-SRG-06** |
| **F — recovery and discharge** | | |
| U-F1 | Recovery status changes and history | History rows; same status no-op |
| U-F2 | Discharge while `Stable` | `NotReadyForDischarge` |
| U-F3 | Discharge ready with defaults | Instructions snapshot; two follow-ups; one `surgery.post-op-instructions` + two `surgery.follow-up-due` with `SendAfter` = due − 1 day at 10:00 local — **US-SRG-07** |
| U-F4 | Follow-up date outside its window | `FollowUpOutsideWindow` |
| U-F5 | `SurgeryFollowUpGuard` pending / done / cat deceased | Valid / invalid / invalid |
| U-F6 | Discharge of a deceased cat while `Recovering` | Allowed; no follow-ups, no messages |
| **G — campaigns** | | |
| U-G1 | Campaign on a dental service | `CampaignServiceNotSterilisation` |
| U-G2 | Booking outside dates; branch not covered; max cases reached; no eligibility note | `CampaignNotApplicable` with each reason |
| U-G3 | Price change after one booking | `CampaignPriceLocked` |
| **X — cross-cutting** | | |
| U-X1 | Reflection over `Events/Surgery/**` | No string properties |
| U-X2 | `ActionRoles` for `Surgery.*` | Matches §12.3; `OverrideConsent` and `LogDrug` Vet only; Administrator absent from clinical writes |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database. The HO-APT-001 §16.1 rules apply: **each test
creates its own branch** with a theatre, a rostered vet surgeon and a nurse assistant. HO-NTF-001's
`NotificationWorkCycle` is called directly, advancing `FakeTimeProvider`. The §8.13 ports are the
**real** implementations from HO-EMR/HO-PHM/HO-IMP; tests that need them run only when those areas
are merged (§20.2).

| ID | Case | Expected |
|---|---|---|
| I-A1 | Migrations on an empty database | Tables, `ex_appointment_staff_time` (`contype = 'x'`), the three triggers present |
| I-A2 | Host starts, scope validation on | Every registration resolves |
| I-A3 | `AddAppointmentStaffTime` over existing appointments | Backfilled rows for occupying ones only |
| I-B1 | Book a surgery; `GET` availability again | The start is gone for that theatre, surgeon and assistant |
| I-B2 | Book with the theatre busy, the surgeon busy and the assistant busy (each seeded separately, then all together) | `400` with `TheatreBusy`, `SurgeonBusy`, `AssistantBusy` — one violation each, with the times — **US-SRG-01** |
| I-B3 | Assistant has a vaccination appointment 10:00–10:15 (HO-APT) | Surgery at 10:00 refused `AssistantBusy`; and reversed: HO-APT staff booking of that nurse inside a surgery is refused `PerformerBusy` |
| I-B4 | **20 parallel bookings** of the same theatre/time with different surgeons and assistants | Exactly one `201`; the rest `409 TheatreBusy` |
| I-B5 | **Parallel**: surgery booking with assistant N and HO-APT staff booking with performer N, same time | Exactly one succeeds; the other `409` (`StaffBusy` or `PerformerBusy`) — ADR-159 |
| I-B6 | HO-APT move / cancel / check-in / no-show on a surgery's appointment | `400 AppointmentManagedBySurgery` |
| I-B7 | HO-APT online or staff booking of a theatre service | `400 ServiceNotBookable` — ADR-172 |
| I-B8 | Booking commits: appointment `Booked` with `surgeryId`; `surgery.booked` request in the same transaction; in-app copy after the pass | As stated; no payment or deposit field anywhere in the response — ADR-170 |
| I-B9 | Reschedule after the pre-op reminder was queued | Old reminder `Cancelled SubjectNoLongerApplies`; new one queued; `surgery.rescheduled` sent |
| I-B10 | Cancel | Surgery and appointment `Cancelled`; slot free; `surgery.cancelled` sent; reminder never sent |
| I-B11 | Calendar (HO-APT) for the theatre and the assistant | Surgery shown with `surgeryId` on both; not in the queue, the HO-APT day list or the customer's appointment list |
| I-B12 | Approve leave for the assistant | The surgery's appointment is flagged; the day list shows `needsRescheduling` |
| I-C1 | New surgery; `GET /day` | Five `outstandingItems` by name — **US-SRG-02 AC1, AC2** |
| I-C2 | Record a weight through HO-CAT's endpoint the day before | `WeightRecorded` done with the weight id |
| I-C3 | Record consent through HO-EMR's endpoint | `ConsentSigned` done; withdraw it → reverted |
| I-C4 | Admit, then start without consent | `400 ConsentNotRecorded`; status still `PreOp` — **US-SRG-03 AC1** |
| I-C5 | Nurse calls `start-with-consent-override` | `403` |
| I-C6 | Vet overrides with a reason | `204`; `InTheatre`; detail shows `consentOverride` with vet and reason; a `BusinessEvent` audit row `SurgeryConsentOverridden` with actor and reason — **US-SRG-03 AC2** |
| I-C7 | Receptionist attempts start, ASA grade, drug log, report finalise | `403` each (AC-18 pattern) |
| I-D1 | Log premedication, vitals ×3, induction, a drug; `GET` log | One timeline in order, each with `loggedBy` and `loggedOn` — **US-SRG-04** |
| I-D2 | Controlled drug with a real witness confirmation (witness re-enters identifier + password through HO-PHM's endpoint) | `201`; one log entry, **one ledger entry for that batch**, **one register entry** naming vet, cat, dose, wastage and witness — **US-SRG-08 AC1, AC3** |
| I-D3 | Same, reusing the confirmation | `400 WitnessConfirmationInvalid`; **no** ledger or register row written (atomicity) |
| I-D4 | Expired batch | `400 BatchNotUsable`; nothing written |
| I-D5 | `PUT`, `PATCH`, `DELETE` on any log entry route | `405`; entry unchanged |
| I-D6 | Raw SQL `UPDATE` / `DELETE` of an anaesthesia log row inside a rolled-back transaction | `P0001 immutable:` — trigger fires (F31) |
| I-D7 | Raw SQL `UPDATE` of the register row written in I-D2 | Refused by HO-PHM-001's table rule — **US-SRG-08 AC2** |
| I-D8 | Correct the controlled entry | New linked log entry; ledger delta entry; new register entry with `correctsEntryId`; originals unchanged |
| I-E1 | End a spay `Completed`; `GET /api/cats/{id}` | `neuterStatus: Neutered`, `neuteredOn` = surgery local date; audit row with the vet — **US-SRG-06** |
| I-E2 | Draft, finalise, then `PUT` the report | `409 ReportNotEditable`; addendum `201`; both visible — **US-SRG-05** |
| I-E3 | Raw SQL `UPDATE` of a finalised report row | `P0001 immutable:` |
| I-E4 | Report read includes `catId` and `visitId` | As stated — "attaches to the cat and the visit" |
| I-E5 | Neuter handler for a cat marked deceased after the procedure; then `apply-sterilisation-outcome` | Not applied; audit row; repair refuses with `CatIsDeceased` shown plainly |
| I-F1 | Set recovery statuses; `GET /recovery-board` | Current status and who changed it — **US-SRG-10** |
| I-F2 | Recovery board query | ≤ 2 SQL commands regardless of entries (no N+1) |
| I-F3 | Discharge with defaults; advance to wound-check − 1 day 10:00; run the pass | `surgery.follow-up-due` sent (in-app); customer detail shows instructions and follow-ups — **US-SRG-07** |
| I-F4 | Complete the wound check before its reminder is sent | Reminder cancelled by the guard |
| I-F5 | Customer reads another customer's surgery | `404` |
| I-G1 | Book under an active campaign | `priceSnapshot.basis = Campaign` with the campaign price; appears in `/cases` with standard-price comparison — **US-SRG-11** |
| I-G2 | Campaign with `maxCases = 1`, second booking | `400 CampaignNotApplicable` |
| I-X1 | **AC-3 end to end**: book spay → weight → consent → bloods waived → ASA → admit → start → controlled drug with witness → vitals → end procedure → report finalised → recovery → discharge | Every step `2xx`; neuter status set; register entry non-editable; follow-up reminders queued; each change in the audit log with actor (AC-20) |
| I-X2 | Log capture at `Information`+ across all cases | No override reasons, instructions, report text, complaint text or customer names |

---

## 17. Implementation order and verification

| Phase | Stories | Delivers | Needs merged |
|---|---|---|---|
| **A** | — | Enumerations, settings, constants, transition rules, protocols (C1) and defaults; **HO-APT-001 amendment §11.7** with `AddAppointmentStaffTime`; F31 verified | HO-APT-001 |
| **B** | US-SRG-01 | `SurgerySlotFinder`, inputs, availability, book/reschedule/cancel, templates, `SurgeryPreOpGuard`; `AddSurgery` | — |
| **C** | US-SRG-02, 03 | Checklist commands, ASA, waivers, weight/consent handlers, admit, start, override + audit, day list | **HO-EMR-001** (consent) |
| **D** | US-SRG-04, 08 | Log root, drugs with stock + register + witness, vitals, milestones, corrections, trigger; `AddAnaesthesiaLog` | **HO-IMP-001, HO-PHM-001** |
| **E** | US-SRG-05, 06 | End procedure, report root, finalise, addenda, triggers, neuter handler + repair; `AddSurgicalReport` | — |
| **F** | US-SRG-07, 10 | Recovery status and board, discharge draft, discharge, follow-ups, `SurgeryFollowUpGuard`, customer endpoints | — |
| **G** | US-SRG-11 | Campaigns, campaign pricing at booking, cases; `AddSurgeryCampaigns` | — |

Phases E–G do not depend on C and D. If HO-EMR, HO-PHM or HO-IMP is late, build A, B, E, F and G in
that order, then return to C and D.

```bash
dotnet build CatEcommerce.sln -warnaserror
dotnet ef migrations add <Name> -p src/Infrastructure/CatEcommerce.Infrastructure -s src/Api/CatEcommerce.Api
#   read the generated migration; hand-write the constraint and trigger SQL; read it again
dotnet ef database update -p src/Infrastructure/CatEcommerce.Infrastructure -s src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet test tests/CatEcommerce.IntegrationTests
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | SRG-01 | Theatre, surgeon and assistant conflicts are each checked and any clash blocks the booking with its reason | U-B1…U-B3, I-B2, I-B3 |
| A2 | SRG-01 | Concurrent bookings cannot double-book any of the three | **I-B4, I-B5** |
| A3 | SRG-02 AC1 | Booking creates the five-item checklist | U-C1, I-C1 |
| A4 | SRG-02 AC2 | Incomplete items are visibly outstanding on the day list | I-C1, I-C2, I-C3 |
| A5 | SRG-09 | **Deferred to HO-BRD-001** (§4.2); this slice shows only that a surgery is not blocked by a stay | HO-BRD-001 |
| A6 | SRG-03 AC1 | Start is blocked without recorded consent | U-C4, U-C7, I-C4 |
| A7 | SRG-03 AC2 | A vet override needs a reason and is audited with the vet's identity | U-C5, I-C5, I-C6 |
| A8 | SRG-04 AC1 | Drug entries with dose, route and time are timestamped and attributed | U-D1, U-D2, I-D1 |
| A9 | SRG-04 AC2 | Vitals appear on one timeline with drug events | U-D3, U-D9, I-D1 |
| A10 | SRG-05 | The report records the required fields, attaches to cat and visit, and is immutable on finalisation | U-E3…U-E6, I-E2, I-E3, I-E4 |
| A11 | SRG-06 | Neuter status and date are set automatically from a completed spay or neuter | U-E1, U-E2, U-E7, I-E1 |
| A12 | SRG-07 | Post-op instructions are produced and follow-ups are scheduled as reminders | U-A6, U-F3…U-F5, I-F3, I-F4 |
| A13 | SRG-08 AC1 | A controlled-drug entry records vet, cat, dose, wastage and a second-staff witness | U-D4, U-D5, I-D2, I-D3 |
| A14 | SRG-08 AC2 | Entries cannot be edited or deleted; corrections are new linked entries | U-D8, I-D5…I-D8 |
| A15 | SRG-08 AC3 | Stock is deducted by the specific batch; expired batches refused | U-D6, U-D7, I-D2, I-D4 |
| A16 | SRG-10 | Recovery status is visible to reception and refreshable every 30 s | U-F1, I-F1, I-F2 |
| A17 | SRG-11 | Campaign pricing applies to qualifying bookings and cases are reportable separately | U-G1…U-G3, I-G1, I-G2 |
| A18 | AC-3 | Spay end to end | **I-X1** |
| A19 | ADR-170 | No deposit or payment exists on a surgery; the price is snapshotted | I-B8, I-G1 |
| A20 | AC-18 / BR-6 | Non-vets are refused clinical finalisation and controlled-drug logging at the API | I-C5, I-C7, U-X2 |

**Deferred, named in §4.2**: US-SRG-09 (HO-BRD-001), invoicing (HO-BIL-001), report exports (HO-RPT-001),
consent capture (HO-EMR-001).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Surgery` context (ADR-157); the two rule-1 exceptions only (ADR-158, ADR-163); the HO-APT-001 amendment confined to §13's lines; **no port of §8.13 implemented or stubbed here**; the neuter handler in the Cat folder and no write to `clinic.cat` from Surgery; no enqueue from event handlers |
| 2 | SECURITY | `security-reviewer` | Exclusion constraints as the clash control (I-B4, I-B5); consent re-read at start; override Vet-only with audit; witness single-use and distinct; **no update/delete path for log, report, addenda or register** (I-D5…I-D7, I-E3); `404` on other customers' surgeries; clinical-access logging; no free text in events or logs |
| 3 | CODE_REVIEW | `code-reviewer` | `SurgerySlotClaim` used by C2 and C3; `SyncStaffTime` on every relevant method (U-A8); idempotent handlers; one save per recorded exception; time only through HO-ADM-001's services |
| 4 | TEST | `test-engineer` | `TC-HO-SRG-001.md` from §15/§16 first; I-B4/I-B5 with real parallelism; trigger tests in rolled-back transactions; per-test branch |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A5 recorded as deferred to HO-BRD-001, not passed; the provider caveat (in-app only) recorded for message rows |

---

## 20. BLOCKED conditions — stop and report

**20.1 HO-ACC-001, HO-ADM-001, HO-NTF-001 or HO-APT-001 is not merged.** This slice consumes their
availability, constraints, settings, outbox, guards, sessions and audit. Do not stub them.

**20.2 A §8.13 contract is missing or different** in HO-EMR-001, HO-PHM-001 or HO-IMP-001. Do **not**
implement, stub or fake the port in this slice, and do not adapt silently to a different shape. Report
the difference; build the phases that do not need it (§17).

**20.3 HO-APT-001's shipped code differs from its handover** in a way that breaks §11.7 (e.g. no
`SlotClaim`, different constraint names). Report it; do not redesign the appointment here.

**20.4 A clash test passes only with retries or delays.** The constraint is wrong; fix it, never the test.

**20.5 Do not add an update or delete path** for anaesthesia log entries, finalised reports, addenda or
register entries — not for data fixes, not for tests outside rolled-back transactions.

**20.6 F31 does not hold** (the trigger error does not reach the filter as described). Stop before
phase D and report; do not drop the triggers.

**20.7 Do not add a deposit, payment or balance** — ADR-170.

**20.8 Do not enqueue from an event handler** — HO-NTF-001 ADR-057.

**20.9 A requirement conflict is a stop, not an edit** — the SRS, the backlog and other handovers are
forbidden scope.
