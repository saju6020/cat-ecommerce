# HO-BRD-001 — Boarding — Cat Care Centre (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-BRD-001** |
| Title | Boarding — Cat Care Centre — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §7 — **US-BRD-01 … US-BRD-21** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-BRD-1 … FR-BRD-22**, **BR-1, BR-8, BR-9, BR-12, BR-13, BR-16**, FR-NTF-3 (boarding messages), **FR-NTF-7**, FR-POR-4, FR-ADM-3 (task assignment), FR-IMP-9 / BR-3 (house food), FR-IMP-11 (recall tracing through boarding), FR-BIL-3, FR-BIL-12, D-7, D-10, NFR-USA-3, UI-5, C-6, C-7, C-9, AC-11 … AC-15, Appendix B.4, Appendix C ("Boarding stay"), Appendix D ("Boarding bookings", "Care logs & tasks", "Incidents") |
| Pairs with | [`HO-BRD-002`](HO-BRD-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-CAT-001`](HO-CAT-001.md) (cats, vaccination status, weight history, high-risk flag), [`HO-ACC-001`](HO-ACC-001.md) (customers, staff, sessions, audit, merge contract), [`HO-ADM-001`](HO-ADM-001.md) (branches, rosters, availability, settings, tax classes, content pages, money), [`HO-NTF-001`](HO-NTF-001.md) (outbox, templates, guards, links), [`HO-APT-001`](HO-APT-001.md) (the `SendAfter`/`SubjectStamp` amendment, the vaccination booking link) |
| Depends on — **not yet written** | `HO-BIL-001` (payments, invoices, credit notes), `HO-IMP-001` (boarding store, stock issue by batch), `HO-EMR-001` (the consultation a referral opens). §12.8 states every contract assumed from them |
| Delivery order | Section 13 of 17 in [`README.md`](README.md#delivery-order) |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — README C-7 (US-SRG-09 had no owner).** HO-SRG-001 deferred surgery-on-a-boarded-cat here while this handover deferred it there. This slice takes the story: new §8.21, new acceptance rows I-G7/I-G8, new OQ-18. It adds no new port — the stay lookup, `IBoardingVetSpendingPolicy`, the approval event and `IBoardingBillableSource` all already exist for the clinic-referral path. <br><br>**2026-09-23 — README C-5 (file storage).** Belongings, diary and incident media now go through [`HO-FIL-001`](HO-FIL-001.md) instead of being metadata-only references. This slice contributes the `BoardingMedia` and `BoardingBelongings` purposes and their `FileRules` rows. ADR-228's fallback is withdrawn; OQ-1 closed; the US-BRD-12 and US-BRD-08 caveats in §18 are lifted. <br><br>**2026-10-14 — Note from the HO-BIL-001 Phase J merge (integrator; BIL backlog rows 76 and 103).** Clinic charges do not carry `BoardingStayId` yet. `VisitFinalisedEvent`, `PrescriptionDispensedEvent` and `SurgeryDischargedEvent` have no stay link, and Billing's Visit, Dispense and Surgery item sources set no billing group, so a charge for a boarded cat is billed on its own, not on the stay invoice. Contract **P2** (and FR-BRD-15 / FR-SRG-9) needs the stay link added to those three events and sources when this handover is built. |
| Amended | **2026-09-22 — README C-7 (US-SRG-09 had no owner).** HO-SRG-001 deferred surgery-on-a-boarded-cat here while this handover deferred it there. This slice takes the story: new §8.21, new acceptance rows I-G7/I-G8, new OQ-18. It adds no new port — the stay lookup, `IBoardingVetSpendingPolicy`, the approval event and `IBoardingBillableSource` all already exist for the clinic-referral path. <br><br>**2026-09-23 — README C-5 (file storage).** Belongings, diary and incident media now go through [`HO-FIL-001`](HO-FIL-001.md) instead of being metadata-only references. This slice contributes the `BoardingMedia` and `BoardingBelongings` purposes and their `FileRules` rows. ADR-228's fallback is withdrawn; OQ-1 closed; the US-BRD-12 and US-BRD-08 caveats in §18 are lifted. |
| Amended (build) | **2026-10-14 — owner started Boarding; built on `ho/BRD-001` from master `5a187b6b`. The contract HO-BRD-002 aligns to, the mismatches found against what is actually merged, and the merge-time integration items are in [§1.1](#11-amended--build-contract-and-mismatches-2026-10-14).** |
| Amended (coordinator) | **2026-10-15 - ADR-365 ratified, narrowly.** "Boarding command handlers hold no Billing repository; they record charges only through IBillableChargeRecorder and request invoices only through IssueInvoiceFromChargesCommand." `BoardingConventionTests.U_X4b` enforces the allow-list. |
| Amended (owner) | **2026-10-15 - owner decisions at acceptance.** (1) p95 targets: "Measure at merge". (2) Owner phone, deposit details and `canOverrideGate` on the stay view: "Ship, follow-up fixes" -- they go in BRD-001-FU1 (backlog row 63). (3) Deposit on the final invoice: "Apply automatically" -- the deposit is applied to the final invoice, built as BIL-001-FU2 (backlog row 61); until then the invoice is outstanding in full and offers allocate. **Credentials note.** An earlier commit (84caeffd) put the dev-only test password of the local Postgres in `docs/test-cases/TC-HO-BRD-001.md`; it is a local default, not a secret, and the file now says only "full connection string in CATECOMMERCE_TEST_DB". History is not rewritten. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

### 1.1 Amended — build contract and mismatches (2026-10-14)

**Numbering.** This handover's reserved ADR-217…236 were taken on master by other areas (ADR-217 is the delivery-tax ADR). Boarding's ADRs are therefore **ADR-350…369** (350 = old 217 … 369 = old 236; code comments say "renumbered ADR-35x"). CONTRACTS rows used: **50–59**.

**Concurrency (2026-10-15, corrected by the ACCEPTANCE ruling).** There is no application lock for occupancy (section 4.4). A reserve, an isolation flag or a daycare booking that loses `ex_stay_allocation_unit`, `ex_stay_cat`, `ex_unit_maintenance` or `ix_daycare_slot` is retried in a fresh DI scope (new DbContext) with the next free unit or slot, up to 4 attempts, and only then answered `409 UnitNoLongerAvailable` / `DaycareFull`. A Confirm that races the worker re-reads in a fresh scope and reports Confirmed. Application detects the loss through `IUniqueViolationDetector` (Infrastructure); it references no Npgsql type.

**Wire conventions** (all endpoints): camelCase JSON; instants ISO-8601 with offset; local dates `yyyy-MM-dd`; local times `"HH:mm"` strings; money is `{ "amount": "1500.00", "currencyCode": "BDT" }`; every enumeration is its display name (`"Deluxe"`, `"CheckedIn"`). Staff routes take the branch from `X-Branch-Id`; customer routes pass `branchId` in the body/query. Writes: `201 {id}` / `204` / `200 {…}`; failures `400 {errors:[{errorCode,errorMessage,propertyName}],isValid:false}`; someone else's stay/incident/approval is `404`; a lost race is `409`; never `202`.

**Built (Phase B, committed):**

| Method | Route | Access | Body → response |
|---|---|---|---|
| GET | `/api/boarding/unit-types?branchId` | public | `[ { unitType, capacityCats, nightlyRate, weeklyRate, monthlyRate, additionalCatNightlyRate } ]` (priced types with an active non-isolation unit; `branchId` omitted = seeded main branch) |
| GET | `/api/staff/boarding/units` | ViewUnits | `[ { id, branchId, code, name, unitType, capacityCats, block?, isActive, sortOrder, nightlyRate? (null = type not priced), version } ]` |
| POST | `/api/staff/boarding/units` | ManageUnits | `{ code, name, unitType, capacityCats, block?, sortOrder }` → `201 { unitId }` |
| PUT | `…/units/{unitId}` | ManageUnits | `{ name, unitType, capacityCats, block?, sortOrder }` → `204` |
| PUT | `…/units/{unitId}/activation` | ManageUnits | `{ isActive }` → `204` |
| GET / POST | `…/units/{unitId}/maintenance` | ViewUnits / ManageUnits | `[ { id, unitId, startsOn, endsOn, reason, isCancelled } ]` / `{ startsOn, endsOn, reason }` → `201 { blockId }`; `400 MaintenanceOverlapsStays` (message lists arrival dates) or `MaintenanceOverlapsMaintenance` |
| DELETE | `…/units/{unitId}/maintenance/{blockId}` | ManageUnits | `204` |
| GET | `/api/staff/boarding/rate-cards?unitType` | ViewUnits | `[ { id, unitType, nightlyRate, weeklyRate, monthlyRate, additionalCatNightlyRate, taxClassId, effectiveFrom, setBy, status: Current/Scheduled/Past } ]` |
| POST | `…/rate-cards` | ManageUnits | `{ unitType, nightlyRate:"1000.00", weeklyRate, monthlyRate, additionalCatNightlyRate?, taxClassId, effectiveFrom }` → `201 { rateCardId }` |
| GET / PUT | `…/care-routine` | ViewUnits / ManageUnits | `{ branchId, isDefault, times:[{kind,time}] }` / `{ times:[{kind: LitterChange/Enrichment/ObservationCheck, time:"HH:mm"}] }` → `204` |

**Phases C–H (the contract the builder commits to; deviations from §12.4 are in bold).**

* `GET /api/boarding/availability?branchId&arrivalOn&departureOn&catCount` (public) and `GET /api/staff/boarding/availability?arrivalOn&departureOn&catCount&requiresIsolation&excludeStayId`: `{ branchTimeZoneId, nights, occupiedFrom, occupiedTo, violation?:{errorCode,message}, unitTypes:[{ unitType, capacityCats, freeUnitCount, quote }], units?:[{unitId,code,unitType,capacityCats,block,isFree}] (staff only), alternatives:[{arrivalOn,departureOn,unitType,total}], waitlistAvailable }`; `quote = { basis: Nightly/Weekly/Monthly, units:[{sequence,kind: Month/Week/Night/AdditionalCat,fromNight,toNight,description,amount}], discountLines:[{sequence,description,amount}], gross, discountTotal, total }` (money objects). **Consecutive nights are ONE `Night` unit covering `fromNight..toNight` (the handover's "5 night units" would break the ≤40-unit bound).**
* Customer: `POST /api/customers/me/boarding/enquiries` `{ branchId, arrivalOn, departureOn, unitType, catIds[] }` → `201 { stayId, vaccinationGate, quote }`; `PUT …/stays/{id}/enquiry` (same body minus branchId) → `200` same shape; `PUT …/stays/{id}/cats/{catId}/care-profile` → `204`; `POST …/stays/{id}/reserve` → `200 { stay, deposit:{ amount, payBy, instructionsPath } }`; `POST …/cancel` `{ note? }` → `200 { depositForfeited }`; `GET …/stays?scope=upcoming|current|past&pageNumber&pageSize` → paged summaries; `GET …/stays/{id}` → stay detail; diary, incident, approval, summary, waitlist, daycare as §12.4. `vaccinationGate = { passed, blockedCats:[{catId,catName,reasons:[{vaccineCode,vaccineName,reason: NeverAdministered/DueBeforeDeparture,dueOn?}],bookingLink}] }`.
* `careProfile` body: `{ feedings:[{time,portion,foodSource: OwnFood/HouseFood,foodDescription,houseFoodSkuId?}], medications:[{name,dose,route,times:[HH:mm],instructions?,fromOn?,toOn?}], litterType, litterNote?, vetVisitAuthorised, vetSpendingLimit:"0.00", emergencyContact:{name,phone,relationship?}, notes? }`.
* Stay view: `{ id, version, branchId, branchTimeZoneId, customerId, status, allowedTransitions[], channel, arrivalOn, departureOn, occupiedFrom, occupiedTo, nights, requestedUnitType, unit?:{unitId,code,name,unitType}, requiresIsolation, needsAttention, attentionReason?, quote?:{…}, deposit:{ required, state, dueBy?, received, outstanding }, balanceDue, cats:[{ catId, catName, catPhotoUrl?, catIsHighRisk, handlingNote?, gate:{status,reasons[],evaluatedOn?,overridden}, careProfile?, checkIn?, checkOut? }], statusHistory[], cancellation?:{…} }` (staff detail adds allocations, charges, referrals, approvals, isolation entries, deposit ledger).
* Staff: routes as §12.4. `POST …/stays/{id}/confirm` → `200 { status, depositState, outstanding }`; `POST …/check-in` and `…/check-out` bodies `{ cats:[{ catId, …checklist }] }` → `200 { stay }` or `400 CheckInChecklistIncomplete` (message lists the missing items per cat); tasks, observations, media, incidents, referrals, vet approvals, dashboard, daycare as §12.4.
* **Media and belongings photos carry a `fileId` (HO-FIL-001), not a storage key**: upload with `POST /api/staff/files?purpose=BoardingMedia|BoardingBelongings`, then send the id. **Signature**: `{ signerName, method: Drawn/Typed, strokes?:[[[x,y],…]], confirmedStatement:true }`.

**Mismatches against what is merged (decisions in the build):**

1. **Rate cards are per branch** (`branch_id`; unique `(branch, type, effective-from)`): §16.1 gives each test branch "its own rate cards", and a card names a branch-owned tax class. The rate-card routes use `X-Branch-Id`.
2. **Billing is push, not pull (P2/P3).** Merged Billing exposes `IBillableChargeRecorder` (charges, `BillingGroupKind.BoardingStay`), `IssueInvoiceFromChargesCommand` (group selector with `ServiceDateTo`) and `IBillingRefundPlanner`; it never calls `IBoardingBillableSource`. Boarding pushes charges in its own handlers and the worker/FE issues the invoice. Deposits are Billing payments with an **earmark `{Kind: BoardingStay, SourceId: stayId}`** (`EarmarkKind`), not `BoardingStayDeposit`; the ledger reads accepted/pending earmarked payments. Billing does not call `IBoardingStayReferenceReader` today, so nothing validates the stay id on `RecordPayment`.
3. **Charges are recorded at check-in, not reserve** (no phantom charges for stays that lapse); the long-stay discount rides each charge as its `LineDiscount` (Billing charges cannot be negative), so `LongStayDiscount` lines are not created. Negative lines (`DepositRefundDue`, `EarlyCheckoutCredit`) are Boarding-side records surfaced on the dashboard `refundDue[]`; staff issue the credit note / release the earmark in Billing.
4. **`InvoiceIssuedEvent` carries no line list**, so the invoiced status of a stay line is read from Billing's invoice lines by charge id (no `MarkStayChargesInvoicedHandler` stamping).
5. **Media retention uses XCT's `retention.boardingMediaMonths` and a registered `IRetentionPurgeSource` (`boarding-media`)**; `boarding.mediaRetentionMonths` and a Boarding purge command are not created. The row is kept (`Purged`); only the stored file is erased.
6. **Settings:** `TimeRange` start<end is the marker `SettingBounds.StartBeforeEnd` in the definition's `Max` (quiet hours still wrap midnight). Violation codes `NoShowTooEarly`, `AlreadyWaitlisted`, `CancellationReasonRequired`, `InvalidSignature` and `WaitlistEntryNotFound` already exist and are reused.

**Merge-time integration items (not built here, need other branches):** (a) HO-BIL-001 Phase J (`ho/BIL-001-J`, backlog BIL 76 "skip until Boarding"): contract row P2 assumed clinic charges carry `BoardingStayId` — **they do not**, on master or on Phase J. When Phase J merges, its Visit / Dispense / Surgery sources must set `BillingGroupKind.BoardingStay` and `BillingGroupId` from a `BoardingStayId` that EMR/SRG add to the visit/surgery and their events; Boarding exposes `IBoardingStayReferenceReader.IsInHouse(stayId, catId)` and `IBoardingVetSpendingPolicy` for that. (b) HO-EMR-001 must subscribe to `BoardedCatReferredToClinicEvent` and raise `ReferralVisitOpenedEvent`; EMR/BIL raise `BoardingVetChargeRecordedEvent`. (c) HO-IMP-001's house-food issue validates through `IBoardingStayReferenceReader`. (d) Reports R24–R26 and the three boarding dashboard tiles are left to HO-RPT-001 (RPT backlog 3).


**Phases F-H as built (2026-10-14).**

* **Routes added** (all as §12.4 unless noted): staff media `POST/GET stays/{id}/media`, `POST …/media/{mediaId}/hide` (body `{ fileId, catId?, incidentId?, caption?, capturedOnLocalDate }`); incidents `POST stays/{id}/incidents`, `GET incidents?status&stayId`, `POST incidents/{id}/acknowledgement|phone-call|resolve`; `GET dashboard`; customer `GET stays/{id}/diary?from&to`, `GET/POST stays/{id}/incidents/{incidentId}[/acknowledge]`, `GET stays/{id}/summary`, `GET stays/{id}/approvals/{approvalId}`, `POST …/respond { approve, note? }`; staff `POST stays/{id}/referrals` → `201 { referralId }`, **`POST …/referrals/{referralId}/return`** (added: without it an away cat could never be checked out), `POST stays/{id}/vet-approvals` `{ catId, referralId?, amount:"4500.00", description }` → `201 { approvalId }`, `POST …/vet-approvals/{approvalId}/staff-answer { approve, note }`, `GET stays/{id}/cats/{catId}/vet-headroom` → `{ catId, catName, vetVisitAuthorised, limit, committed, headroom }` (money objects), `GET stays/{id}/summary`, `POST stays/{id}/extend { expectedVersion, newDepartureOn }` (`400 UnitNotFreeForExtension` + `relocationOptions[]`; `409 StaleStayVersion`), `GET stays/{id}/relocation-options?newDepartureOn`, `POST stays/{id}/relocate { expectedVersion, targetUnitId, effectiveFromLocalDate, reason, newDepartureOn? }`, `POST stays/{id}/check-out { cats:[{ catId, weightKg, belongings:[{belongingId,returned,note?}], signature }] }` → `200 { stay }`, `POST stays/{id}/attention/clear { note }`; customer `POST/GET/DELETE boarding/waitlist`, `POST/GET daycare`, `POST daycare/{id}/cancel`; staff `GET/POST daycare`, `POST daycare/{id}/check-in|check-out|cancel|no-show`, `GET/POST boarding/daycare-rate-cards`.
* **Owner-authorised addition (2026-10-14, "Show before"): early check-out preview** `GET /api/staff/boarding/stays/{id}/check-out-preview` (`Boarding.CheckInOut`), changes nothing and runs the very code the check-out runs (`EarlyCheckout.Compute` + `BoardingChargePlanner.Plan`; pinned by I-G2b). Shape (BRD-002's `CheckOutPreview` fields first, then extras): `{ isEarly, nightsBooked, nightsCharged, quote?: {basis,units[],discountLines[],gross,discountTotal,total}, lines:[{ lineRef, description, net, action: "Keep"|"Void"|"Record" }], credit: money, creditOutcome: "None"|"Credit"|"Refund", today, bookedDepartureOn, chargedDepartureOn, unitFreeFrom, previousTotal: money, newTotal: money }`. `creditOutcome` follows the snapshotted `boarding.earlyCheckoutRefundAsCredit`.
* **Stay view additions** (staff and customer): `charges[]` (customer sees active ones), `referrals[]`, `approvals[]`, `incidents[]` (owner: medium and high only), `cats[].awayReason` (`"At the clinic"`/`"In surgery"`/null); staff only: `creditDue`, `headroom[]`, `attentionClearedNote`, `allocations[]`. Occupancy cells gain `catNames[]` and `inTurnaround`. **Asked for by BRD-002 but not specified in §12 and therefore NOT built**: invoices on the stay view (use `GET …/summary` → `billing.invoiceIds`), `cancellation` preview of the deposit outcome before cancelling, `canOverrideGate` (derivable from the caller's role), enquiry `deposit.amount/payBy` (the stay view carries `deposit.required` and `deposit.dueBy` once reserved; `reserve` returns the deposit), check-in/check-out windows on availability and the enquiry (only `occupiedFrom/occupiedTo` are exposed), `minutesLate` and a per-cat not-eating flag on the task list (tasks have `isOverdue`; `notEating` is on observations and the dashboard).
* **Decisions.** Charges are recorded at check-in and re-derived on extension and early check-out under a revision (ADR-363); an invoiced charge that no longer applies is a `creditDue` on the stay (staff issue the credit note in Billing; it stays on the dashboard `refundDue[]` until Billing offers a way to resolve it -- recorded gap). Cycle and final invoices are issued by the worker through Billing's `IssueInvoiceFromChargesCommand` (a branch without a billing profile is refused and retried each pass, logged as a warning). Early check-out sets the departure to `arrival + nights charged`. The Boarding eraser holds a stay for `retention.financialYears` and mutates nothing. `FilePurposeAccess` maps the two boarding purposes (cross-area edit). Test classes that call the worker share one xUnit collection because a pass reads every stay in the shared database.
* **Migrations on the branch** (the integrator regenerates them): `AddBoardingUnits`, `AddBoardingStays`, `AddBoardingCare`, `AddBoardingIncidentsAndMedia`, `AddBoardingCharges`, `AddBoardingWaitlistAndDaycare`; literal `RetentionGuardSql.AttachTo` calls cover every Forbidden table.
* **Cross-area edits**: `CatAggregate.EvaluateVaccinationStatus` and `CatVaccinationStatusCalculator`, `BusinessSettingsAggregate` (time-range start < end), `INotificationLinkBuilder`/`NotificationLinkBuilder`, `NotificationTemplateCatalogue.cs`, `NotificationSubjectKind`, `UniqueConstraintExceptionFilter`, `FilePurposeAccess` (+ its test), `ErasureCoverageConventionTests` (map), `appsettings.json`.
* **Merge-time items added**: EMR raises `ReferralVisitOpenedEvent` and `BoardingVetChargeRecordedEvent`, subscribes `BoardedCatReferredToClinicEvent` and `VetChargeApprovalDecidedEvent`; Billing may start calling `IBoardingStayReferenceReader` for `DaycareVisit` payment references; reports R24-R26 and the boarding dashboard tiles stay with HO-RPT-001; Phase J of BIL-001 (clinic charges carrying `BoardingStayId`) is unchanged from item (a) above. XCT backlog 1 (`BoardingTaskSnapshotSource`, `BoardingTasks.View`) is not built.


**Gate-fix round (2026-10-15).** All three first-round gates are answered in `docs/backlog/BRD.md` (one numbering; fixed rows are marked Resolved).

* **Owner decisions.** *Erasure after retention:* "Keep everything" -- the eraser stays as built (it holds the stay for `retention.financialYears` and redacts nothing, also after the period). *Vaccination check:* "On or before" -- a vaccine due ON the last day of the stay also blocks (§8.6); the boundary is fixed in `BoardingVaccinationGate` and pinned by U-C10b (the shared `VaccinationStatusRule`, strict "before", and the cat profile are untouched).
* **Coordinator ratifications** (cross-area edits outside §13/§14, each recorded here): (1) `BusinessSettingsAggregate`: the one-line TimeRange start<end rule (§10.2 / U-A9) against §14; (2) `FilePurposeAccess`: `BoardingMedia` -> `Boarding.RecordCare`, `BoardingBelongings` -> `Boarding.CheckInOut` (mirror it in HO-FIL-001 §1 at merge); (3) `deploy/database/roles.sql`: the `boarding` schema token.
* **ADR-232 / ADR-365 and §4.4.** §9 ADR-232 ("no cross-context write from a Boarding command handler") and §4.4 ("no write to a Billing aggregate") are read by the build as: Boarding writes no Billing aggregate DIRECTLY; it uses Billing's published seams -- `IBillableChargeRecorder` (enlists a charge in the producer's own save, CONTRACTS row 10, HO-BIL-001 ADR-178) and `IssueInvoiceFromChargesCommand` through the dispatcher. ADR-363 and ADR-365 record that reading. It is the approved Billing contract and the HO-ORD-001 precedent, but it is a reading of two handover sentences, so the coordinator decides at merge; neither ADR is rewritten.
* **Fixes** (see the backlog for tests): read handlers project (no aggregate is rehydrated); the Boarding worker is off in the Testing hosts; deposit earmarks count only for the stay's own customer and branch; a late deposit on a lapsed stay shows under `refundDue[]`; the owner diary and acknowledge exclude Low incidents; staff changes to a reserved stay's vet authorisation need a reason and are audited (`StayVetAuthorityChangedEvent`, audit row, `stay_cat.vet_authority_change_reason`); reserve, waitlist and daycare booking are rate limited, daycare is bounded by the booking horizon and a customer's live waitlist entries (10) and upcoming daycare visits (30) are capped (defaults, no setting); a paid reservation that fails the gate is flagged `NeedsAttention` (`VaccinationGateBlocked`) and may lapse; the care-task duplicate race is a success; `creditDue` nets Billing credit-note lines (netting, not a resolve action); the final invoice key is deterministic; a check-out after the booked departure charges the extra nights without extending the allocation (see the late check-out paragraph below); a count-only `IRetentionReviewSource` (`boarding-records`) is registered. The Boarding test host looks at its own fixture core vaccines only (`BoardingTestVaccineScope`).
* **Path translations** (outside §13's literal list, ADR-341 / SRG precedent): `Filters/BoardingClaimEndpointAttribute.cs`, `Infrastructure/Configuration/BoardingOptions.cs`, `RateLimiting/BoardingRateLimitPolicies.cs`, `Retention/Erasers/BoardingPersonalDataEraser.cs`, `StaffBoardingDepartureController.cs` and `DaycareControllers.cs` (controller grouping), flat `Configurations/Boarding*.cs`, `Infrastructure/Boarding/BoardingRetentionReviewSource.cs`.
* **Flow-doc text for the coordinator.** `.ai/flows/boarding-stay-flow.md` is §14 scope and was restored to master. The text the build proposed for it (status line becomes "Backend complete (HO-BRD-001, 2026-10-14) -- frontend is HO-BRD-002"; append):

```markdown
## Built -- HO-BRD-001 (updated 2026-10-14)

Status: Complete (backend, phases A-H). One `Boarding` context, schema `boarding`, ADR-350 ... ADR-369 (`.ai/decisions/adr.md`); the contract and every departure from the handover are in `docs/handover/HO-BRD-001.md` section 1.1 Amended.

**Aggregates** (`src/Domain/CatEcommerce.Domain/Aggregates/Boarding/`): `BoardingUnit`, `UnitMaintenanceBlock`, `BoardingRateCard`, `BoardingCareRoutine`, `BoardingStay` (partials: core, `.Lifecycle`, `.Care`, `.Clinic`, `.Departure`), `CareTask`, `CareObservation`, `IsolationEntry`, `BoardingIncident`, `StayMedia`, `BoardingWaitlistEntry`, `DaycareRateCard`, `DaycareVisit`.

**Pure domain services**: `BoardingAvailabilityFinder`, `BoardingQuoteCalculator`, `BoardingOccupancyWindow`, `BoardingChargePlanner`, `EarlyCheckout`, `RelocationOptions`, `VetSpendingHeadroom`, `DaycarePricing`, `CareTaskGenerator`, `CareTaskAssigner`, `NotEatingRule`, `VaccinationStatusRule`.

**Money flow** (push model): deposit = Billing payment earmarked `{BoardingStay, stayId}`; charges recorded at check-in through `IBillableChargeRecorder` and re-derived by revision on extension / early check-out; 30-night cycle invoices and the final invoice issued by `BoardingWorker` through `IssueInvoiceFromChargesCommand`. Early check-out preview = `GET .../check-out-preview` (same code as the check-out).

**Where**: handlers `src/Application/.../CommandHandlers/Boarding/`, event handlers `.../DomainEventHandlers/Boarding/`, reads `src/Read/.../{Queries,QueryHandlers}/Boarding/`, controllers `src/Api/.../Controllers/{BoardingPublic,CustomerBoarding,CustomerDaycare,StaffBoardingUnits,StaffBoardingStays,StaffBoardingCare,StaffBoardingIncidents,StaffBoardingDeparture,StaffDaycare}*`, host `Modules/BoardingModule.cs`, worker `Background/BoardingWorkCycle.cs`, ports and readers `src/Infrastructure/.../Boarding/`.

**Remaining**: merge-time integration with EMR (referral events), BIL Phase J (`BoardingStayId` on clinic charges), IMP house food, HO-RPT-001 reports R24-R26 and boarding tiles; a way to resolve `creditDue` once the credit note is issued; waitlist notice quiet hours rely on the notification service.
```


**Late check-out (owner decision 2026-10-15): "Charge extra nights, never block".** A check-out after the booked departure always succeeds. The extra nights are CHARGES ONLY (the stay is re-quoted for the nights through the check-out date with its own snapshots); the unit allocation is NOT extended, so the exclusion constraint cannot refuse the check-out and the check-out endpoint no longer carries `[BoardingClaimEndpoint]`. A booking of the same unit that overlaps the overstay is flagged `NeedsAttention` (`UnitOverstayed`) by `FlagStaysOnOverstayHandler` (event `BoardingStayCheckedOutEvent` now carries `Late`, `OverstayUnitId`, `OverstayFrom`, `OverstayTo`) and appears on the dashboard `attention[]` list so staff move it. The vaccination gate is not re-evaluated for the extra nights. The check-out preview carries `isLate` (mirror it in HO-BRD-002, with the referral-return route). Pinned by U-G24, I-S7 and I-S10.

Also named in the path-translation list above: `src/Api/CatEcommerce.Api/appsettings.Testing.json` (`Boarding:Worker:Enabled=false`, the remedy of CODE_REVIEW's HIGH; §13 names `appsettings.json` only) and `Domain/DomainServices/{StayPureRules,IUniqueViolationDetector}.cs`, `Infrastructure/Boarding/PostgresUniqueViolationDetector.cs`. Waitlist and daycare caps have their own codes (`WaitlistLimitReached`, `DaycareLimitReached`). The `refundDue[]` list has no look-back window.

---

## 2. Objective

Run the **cat care centre** on the platform: owners check availability, price and book a short or
long stay for one or several cats; the booking is gated on vaccination, carries a detailed care
profile, and is held as `reserved` until **staff record a deposit paid offline**; caretakers check the
cat in against a checklist, work a generated daily task list on a tablet, record observations,
photos and incidents; the clinic sees referred cats without them leaving the premises; the manager
extends, relocates, checks out, and bills long stays per cycle.

**There is no payment gateway** (SRS §1.2, Phase 5). A deposit is paid by bank transfer, in-store
cash or in-store card and **recorded by a staff member in Billing** (FR-BIL-12). Nothing in this slice
captures, charges or refunds money online, and nothing stores a card number or a payment token (C-5).

Six things this slice is careful about:

1. **No unit is double-booked and no cat is in two stays.** Both are PostgreSQL exclusion constraints
   over instant ranges (ADR-219), the pattern HO-APT-001 ADR-068 proved (F28). A read-then-write
   check is not the control.
2. **A cat whose vaccinations lapse during the stay does not board** (BR-1). The gate is evaluated as
   of the **departure date**, by the cat's own rule, at reserve, confirm and check-in; only an
   administrator can override it, with a reason, audited (ADR-222).
3. **A unit is never held indefinitely on unconfirmed payment** (BR-16). A reservation expires after
   `boarding.reservationHoldHours` unless Billing shows the deposit accepted (ADR-223).
4. **Boarding never writes another context's aggregate.** Money is Billing's, stock is Inventory's,
   the consultation is the Clinic's. Boarding exposes references and read ports, consumes their
   events, and asks through their commands (ADR-232).
5. **Unbounded things are not inside the stay.** Care tasks, observations, incidents, media and
   isolation entries are their own roots (`.ai/bounded-contexts.md` rule 3, ADR-217).
6. **Messages ride the change.** Every owner message is enqueued in the command handler through
   HO-NTF-001's outbox; high-severity incidents use an `Urgent` template, so they leave within one
   5-second notification poll — well inside FR-NTF-7's 15 minutes (ADR-226).

§17 builds the slice in **eight phases** (A–H); each compiles, migrates and passes its own tests first.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-BRD-01 | M | 5 | Units with type (standard, deluxe, family/multi-cat, isolation), capacity in cats, location/block; the nightly rate on the unit type's rate card; maintenance blocks excluded from availability | B |
| US-BRD-03 | M | 5 | Effective-dated rate cards per unit type (nightly, weekly, monthly, additional cat); long-stay threshold and discount; itemised quote | B |
| US-BRD-02 | M | 8 | Availability for a date range and number of cats: unit types with prices and total cost; bookings, maintenance and turnaround gap excluded; nearest alternative dates when full | C |
| US-BRD-04 | M | 8 | Vaccination gate as of departure; blocked enquiry kept and resumable; vaccination booking link returned; administrator override with reason, audited | C |
| US-BRD-05 | M | 8 | Per-cat care profile: feeding schedule and food source, medications with times, litter preference, vet-visit authorisation, emergency contact, veterinary spending limit | C |
| US-BRD-07 | S | 8 | Several cats of one household on one reservation in one unit, per-cat profiles; refused over capacity with the capacity stated | C |
| US-BRD-06 | M | 5 | Deposit (percentage or fixed) computed; payment instructions issued; `reserved` until Billing shows the deposit accepted, then `confirmed` and the owner notified; lapse after the window, unit released, owner notified | D |
| US-BRD-08 | M | 8 | Check-in checklist per cat (identity, vaccination proof, weight, arrival health, belongings with photos, owner signature); incomplete → no `checked_in` | E |
| US-BRD-09 | M | 8 | Daily task list per unit from each care profile: feeds, medication doses, litter changes, enrichment, observation checks, with due times; extension generates the added days; tasks assigned to rostered caretakers | E |
| US-BRD-10 | M | 5 | One-action task completion recording time and identity; overdue derived for the dashboard | E |
| US-BRD-11 | M | 5 | Observations per cat (portion eaten, water, litter output, mood, notes); "not eating" after two consecutive no-food entries | E |
| US-BRD-18 | M | 5 | Suspected-infectious flag; stays with a flagged cat only in isolation units; staff isolation entries recorded | C (rule), E (flag, entries) |
| US-BRD-12 | S | 5 | Stay media (photo / short video) references per cat and day, shown as a diary; retained 12 months unless attached to an incident | F |
| US-BRD-13 | M | 8 | Incidents with kind, severity, description, immediate action; medium/high notify the owner, high as `Urgent`; unacknowledged high escalated to a phone-call follow-up | F |
| US-BRD-19 | M | 8 | Dashboard read: occupancy, arrivals, departures, not eating, overdue tasks, open incidents and escalations, unpaid balances | F |
| US-BRD-14 | M | 8 | Referral to the clinic linked to the stay; per-cat spending headroom; owner approval requests above it | G |
| US-BRD-15 | M | 5 | Extend when the unit is free; otherwise relocation offered and recorded with a reason; quote, charges and tasks follow | G |
| US-BRD-16 | S | 5 | Early check-out recalculates nights and rate basis per the configured policy; the difference becomes a credit line for Billing | G |
| US-BRD-17 | M | 5 | Check-out checklist (belongings returned, weight, owner signature) and the stay summary; itemised charge lines for the final invoice | G |
| US-BRD-20 | S | 8 | Stays longer than one 30-night cycle invoiced per cycle; the final invoice covers the remaining nights only | G |
| US-BRD-02 AC3 (waitlist) | M | — | Waitlist for a full date range; notified in order when a unit frees | H |
| US-BRD-21 | C | 5 | Daycare visits with hourly or day rates and a daily capacity, never consuming an overnight unit | H |

Also delivered from upstream contracts: **task reassignment on approved caretaker leave** (HO-ADM-001
ADR-048, US-ADM-03 AC1's boarding half), stays reassigned on **customer merge** (HO-ACC-001 ADR-039),
stays **flagged** when a cat dies or changes owner, and the **boarding-feeding half of recall tracing**
(US-IMP-11 AC2) through the stay reference Inventory records.

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409`; per-call `SaveChangesAsync`; `ValueGeneratedNever()` and the F15 lines on every configuration | ADR-011, 016, 021 |
| No `CommandService` layer; cross-aggregate logic in pure domain services | ADR-015, `.ai/bounded-contexts.md` rule 4 |
| `Enumeration` in `Shared`, persisted by `Value` | ADR-022 |
| Weight history is `CatWeightRecordAggregate`; never cached on the cat | ADR-023 |
| `[RequireAction]` + the one `ActionRoles` table; `[RequireCustomerSession]`; `[PublicEndpoint]`; every endpoint declared | HO-ACC-001 ADR-036, HO-ADM-001 ADR-043 |
| Every aggregate change audited in the same transaction by the interceptor | HO-ACC-001 ADR-040 |
| Branch-local time via IANA zones; conversion only in HO-ADM-001's domain services | HO-ADM-001 ADR-045 |
| Active branch from `X-Branch-Id`; customers pass `branchId` | HO-ADM-001 ADR-046 |
| Staff availability is the pure `StaffAvailability`; leave events are integration contracts consumed idempotently | HO-ADM-001 ADR-047, ADR-048 |
| Prices and tax rates effective-dated, append-only; **snapshot at transaction time** | HO-ADM-001 ADR-049 |
| Settings are registry entries read through `IBusinessSettings` | HO-ADM-001 ADR-050 |
| Money is `decimal(12,2)` + ISO code; JSON amount as a string | HO-ADM-001 ADR-054 |
| Messages only through `INotificationOutbox.Enqueue`, in the command handler before the save; never from an event handler; timed producers use `RequestNotificationCommand` with a deduplication key | HO-NTF-001 ADR-056, ADR-057 |
| `SendAfter` and `SubjectStamp` on notification requests | HO-APT-001 ADR-074 (HO-NTF-001 amendment) |
| Media are **validated references** (storage key + content type + size), never bytes, until object storage exists | HO-CAT-001 §4.2 |
| **No payment gateway**; payments are recorded by staff | SRS A-2, §10 Phase 5; owner |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-BRD-002`](HO-BRD-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-BRD-04 "a vaccination appointment is offered inline" | The enquiry and reserve responses carry `vaccinationGate.blockedCats[]` with each cat's reason and `bookingLink` (HO-APT-001's `CatCareLink`) |
| US-BRD-10 "no more than two taps" | Completion is **one** request with no required body; the tap budget is the FE's |
| US-BRD-10 / US-BRD-19 "highlighted as overdue on the dashboard" | `isOverdue` on every task read and `overdueTasks[]` on the dashboard read, both derived at read time |
| US-BRD-12 "appears in my portal stay diary" | `GET /api/customers/me/boarding/stays/{id}/diary` |
| US-BRD-19 "refreshes at least every 30 seconds without losing unsaved input" | The dashboard read is cheap and side-effect free; polling is the FE's (HO-BRD-002) |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Where it lands | What this slice delivers |
|---|---|---|
| Recording the deposit payment (method, amount, reference, second approval above threshold) | **HO-BIL-001** (US-BIL-11, FR-BIL-12) | The `BoardingStayDeposit` reference kind, the read port Billing uses to validate it (§12.8 P1), and the confirm path that reads Billing's accepted payments (ADR-223) |
| Issuing invoices and credit notes; applying the deposit; refunds | **HO-BIL-001** | Charge lines per stay and period (`IBoardingBillableSource`, §12.8 P2), the worker trigger (ADR-230) |
| The combined customer invoice (US-ORD-17) | HO-BIL-001 / HO-ORD-001 | Boarding lines carry `LineOfBusiness = Boarding` |
| Opening the consultation for a referral; holding treatment above the limit | **HO-EMR-001** | `BoardedCatReferredToClinicEvent`, the headroom read port and the approval decision event (§12.8 E1–E4) |
| Issuing house food from the boarding store by batch, FEFO, expired refused (BR-3) | **HO-IMP-001** | The `BoardingStay` reference kind and `IBoardingStayReferenceReader` (§12.8 I1) |
| Surgery on a boarded cat (**US-SRG-09**) | **This slice (README C-7)** | HO-SRG-001 §4.2 defers the story here and emits `SurgeryScheduledEvent` / `SurgeryDischargedEvent` carrying `SurgeryId`, `CatId`, `BranchId`. Both handovers had deferred it to the other, so nobody owned it; Boarding takes it because it owns the stay, the vet-spending headroom and the stay invoice. See §8.21 |
| Portal dashboard of stays and balances | HO-POR-001 | `GET /api/customers/me/boarding/stays` |
| Offline read-only boarding task list (NFR-AVL-3) | HO-XCT-001 | The day task list read (§12.4) |
| Occupancy, care-compliance and incident reports (SRS §8.1) | HO-RPT-001 | The tables |
| Deleting stored media bytes after retention | HO-XCT-001 (with object storage) | `RetainUntil` and the `Purged` mark (ADR-228) |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No object storage** (HO-CAT-001 §4.2, US-XCT-07) | Belongings photos, diary media and incident media are **references** (key, type, size). Nothing is viewable until storage exists. `boarding.requireBelongingsPhotos` (default **true**) lets the owner relax the check-in photo requirement until then — OQ-1 |
| **No SMS or email provider** (HO-NTF-001 OQ-1) | Owner messages are enqueued, rendered and logged; outside Development only the **in-app copy** is delivered. FR-NTF-7's 15-minute bound is proved for the in-app copy and the enqueue (A-rows note it) |
| Deposit, cancellation and early check-out policy (SRS **Q-6**) | Settings with defaults (§10.2); nothing hard-coded — OQ-3 |
| Rate card (SRS **Q-7**) | Rate cards are data; none are seeded — OQ-4 |
| Unit counts and isolation ward (SRS **Q-11**) | Units are data; none are seeded. Isolation is a unit type — OQ-5 |
| A "boarding manager" role | Not an Appendix D column; mapped to `Administrator` (+ `Reception` for booking changes) — ADR-233, OQ-6 |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, a second host, a `CommandService` layer, `202 Accepted`,
`[Authorize]`. **No online payment, no payment link, no gateway SDK, no card or token field** — the
deposit is recorded by staff in Billing (SRS A-2, C-5). **No application lock, advisory lock or
`SELECT … FOR UPDATE`** for occupancy — the exclusion constraints are the control (ADR-219). **No
stored availability table** (ADR-220). **No enqueue from an event handler** (HO-NTF-001 ADR-057).
**No write to a Billing, Inventory, Clinic, Appointment or Administration aggregate** from a Boarding
command handler (ADR-232). **No stored "not eating" or "overdue" flag** (ADR-224, ADR-225). **No
upload endpoint, object-storage client or local file write** (HO-CAT-001 §4.2). **No time-zone
library.**

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-APT-001 merged** | ❌ not implemented at baseline — §20.1 |
| **HO-BIL-001 merged** (payments with a reference, invoices from a source, credit notes) | ❌ **not written** — needed from Phase D. §20.2 |
| **HO-IMP-001 merged** (boarding-store location, stock issue with a reference) | ❌ **not written** — needed for the house-food half of Phase E. §20.2 |
| **HO-EMR-001 merged** (visit opened from a referral) | ❌ **not written** — needed from Phase G. §20.2 |
| PostgreSQL 17.5 with **`btree_gist`** | ✅ verified (HO-APT-001 F28); `CREATE EXTENSION IF NOT EXISTS` is idempotent |
| `dotnet-ef` 9.0.0 | ✅ |
| New NuGet packages | **None** |

Phases **A, B, C and the non-house-food half of E and F** depend only on the four written areas.
The shared-database rules of HO-CAT-001 §16.1 and HO-ACC-001 §16.1 apply; **every test creates its
own branch** with its own units (§16.1).

---

## 6. Governing architecture documents

Where any of these disagrees with a `.ai/templates/*.cs` file, **the markdown wins**. The old
Mongo/RabbitMQ material (`.ai/mongodb.md`, `.ai/flows/_archive-procurement/**`, the worker-host
examples) is **superseded** and must not be followed.

| Document | What you need from it |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer responsibilities; §6 verified platform surface; §6.2 the two rules that fail silently |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | `IInMemoryDispatcher` only; the worker dispatches commands through it like a controller |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Lists `BoardingUnit` and `BoardingStay` as contexts — merged into one `Boarding` context (ADR-217, OQ-2). **Rule 1** one aggregate per command (no exception in this slice); **rule 2** reference by id; **rule 3** tasks, logs, incidents, media are not inside the stay; **rule 4** availability and pricing are domain services; the BR-1 row — "`CatAggregate` exposes vaccination status; the boarding command handler checks it" (ADR-222) |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | `Boarding` is the `[Feature]` segment everywhere in §11/§12 |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 binding**; ADR-016's note that boarding booking needs more than `xmin` is what ADR-219 answers |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | Handler shape; §6 rule placement — existence, ownership and cross-context reads in the handler, invariants in the aggregate |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | `AsNoTracking()`, explicit projections; the dashboard and summary are reads, never writes |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §2 repository (already built); §4 owned mapping for the stay's bounded children; §6 `xmin`; §8 column types. Exclusion constraints are raw SQL in the migration |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §3 an event handler fails after commit — why messages and money never happen there; §4 the outbox; §5 event vs direct call |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | The enumerations in §10.1 (base class is ours, ADR-022) |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | Validators as `IValidator<T>`; `ValidateScopes`/`ValidateOnBuild` on |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs`; lifetimes; the never-register list; `AddHostedService` for the worker |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File order within each phase |
| [`.ai/flows/boarding-stay-flow.md`](../../.ai/flows/boarding-stay-flow.md) | The flow, aggregates and invariants this slice operationalises; its "remaining work" list is §17's phases |
| [`HO-APT-001`](HO-APT-001.md) §8.4, §12.1, §12.5 | The exclusion-constraint pattern, the `23P01` mapping, the worker shape |
| [`HO-ADM-001`](HO-ADM-001.md) §8.2, §8.5, §8.6, §8.7 | Branch scoping; `StaffAvailability`; price snapshots; the settings registry |
| [`HO-NTF-001`](HO-NTF-001.md) §8.2, §8.4, §8.7, §8.8 | Template definitions, the producer contract, subject guards, link builder |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001 F28–F30
apply. Additionally, from reading those documents (no new code exists at baseline):

| # | Fact | Consequence |
|---|---|---|
| F31 | An exclusion constraint's predicate can reference only the row's own columns (F28 shows the `WHERE status IN (…)` form) | The unit and cat ranges live on the **child rows** (`stay_unit_allocation`, `stay_cat`) with a denormalised `is_active`, maintained by the stay aggregate (§8.4) |
| F32 | A constraint cannot span two tables | Maintenance blocks and stay allocations are checked against each other by the handlers on both sides; the residual race is recorded in ADR-219 |
| F33 | HO-NTF-001 template urgency is a property of the **definition**, not the request | Medium and high incidents need **two** templates (§8.13) |
| F34 | `StaffAvailability.Intervals` returns instants for one staff member; HO-APT-001's `AppointmentSchedulingInputs` shows the load shape | `CareTaskAssigner` takes pre-loaded caretaker intervals; one input loader (`BoardingCareInputs`) |
| F35 | HO-CAT-001 Q4 computes status read-side and explicitly leaves the write-path method to "§7 Boarding" | This slice extracts the rule into `VaccinationStatusRule` and adds a **read-only** method on `CatAggregate` (§8.6) |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries — ADR-217

```
Boarding context — schema `boarding`
BoardingUnitAggregate          (root)  → BranchId                          boarding.unit
UnitMaintenanceBlockAggregate  (root)  → UnitId, BranchId                  boarding.unit_maintenance_block
BoardingRateCardAggregate      (root, append-only) → UnitType             boarding.rate_card
BoardingCareRoutineAggregate   (root, Id == BranchId)                      boarding.care_routine
└── Times [owned many ≤ 24]    RoutineTime(Kind, LocalTime)
BoardingStayAggregate          (root)  → BranchId, CustomerId              boarding.stay
├── Cats          [owned many ≤ 6]   StayCat (+ CareProfile, CheckIn, CheckOut, flags)   boarding.stay_cat
│   ├── Feedings    [owned ≤ 6]      boarding.stay_cat_feeding
│   ├── Medications [owned ≤ 10]     boarding.stay_cat_medication
│   └── Belongings  [owned ≤ 30]     boarding.stay_cat_belonging
├── Allocations   [owned many ≤ 10]  UnitAllocation                        boarding.stay_unit_allocation
├── RateUnits     [owned many ≤ 40]  QuotedRateUnit (the snapshotted quote) boarding.stay_rate_unit
├── Charges       [owned many ≤ 60]  StayChargeLine                        boarding.stay_charge_line
├── Referrals     [owned many ≤ 20]  VetReferral                           boarding.stay_referral
├── Approvals     [owned many ≤ 20]  VetChargeApproval                     boarding.stay_vet_approval
├── VetCommitments [owned many ≤ 100] VetChargeCommitment                  boarding.stay_vet_commitment
└── StatusHistory [owned many ≤ 20]  StayStatusChange                      boarding.stay_status_change
CareTaskAggregate              (root)  → StayId, CatId, BranchId           boarding.care_task
CareObservationAggregate       (root)  → StayId, CatId                     boarding.care_observation
BoardingIncidentAggregate      (root)  → StayId, CatId                     boarding.incident
StayMediaAggregate             (root)  → StayId, CatId?                    boarding.stay_media
IsolationEntryAggregate        (root)  → StayId, StaffAccountId            boarding.isolation_entry
BoardingWaitlistEntryAggregate (root)  → BranchId, CustomerId              boarding.waitlist_entry
DaycareVisitAggregate          (root)  → BranchId, CustomerId, CatId       boarding.daycare_visit
DaycareRateCardAggregate       (root, append-only)                         boarding.daycare_rate_card
```

**Every owned collection on the stay is bounded** by `StayRules` (a stay is at most
`boarding.maxStayNights` long, so its rate units, charge lines and referrals are too). Anything that
grows with the number of days times the number of events — tasks, observations, media, incidents,
isolation entries — is its own root (rule 3). This departs from `.ai/bounded-contexts.md`'s
one-line summary ("`BoardingStayAggregate` — dates, care profile, tasks, incidents"), deliberately.

**One aggregate mutated per command, with no exception in this slice.** Where a use case touches
another root (a task from a stay, a weight record from a check-in, a commitment from a clinic charge)
it happens in a domain-event handler that mutates and never enqueues, or in a worker pass that
dispatches one command per item.

### 8.2 `BoardingUnitAggregate`, maintenance, rate cards — US-BRD-01, US-BRD-03

**`BoardingUnitAggregate`** — `BranchId`, `Code` (≤ 20, unique per branch), `Name` (≤ 100),
`Type` (`BoardingUnitType`), `CapacityCats` (1…6; `Isolation` 1…2), `Block` (≤ 50, location/block),
`IsActive`, `SortOrder`. Methods `Create`, `Update` (type change refused while future active
allocations exist → `UnitHasFutureStays`), `Deactivate` (same refusal), `Reactivate`. Events
`BoardingUnitCreatedEvent`, `BoardingUnitUpdatedEvent`, `BoardingUnitActivationChangedEvent`.

**`UnitMaintenanceBlockAggregate`** — `UnitId`, `BranchId`, `StartsOn` / `EndsOn` (`DateOnly`,
inclusive, branch-local), `Reason` (≤ 200), `IsCancelled`. Occupies
`[StartsOn at 00:00, EndsOn+1 at 00:00)` local. `Create` refuses an overlap with an active allocation
(`MaintenanceOverlapsStays`, message listing the stays' arrival dates — the handler pre-check) and
another block (constraint `ex_unit_maintenance`). `Cancel` (not after `EndsOn`). A unit on
maintenance is excluded from availability for that period — **US-BRD-01 AC2**.

**`BoardingRateCardAggregate`** — append-only, effective-dated, HO-ADM-001 ADR-049's rule:
`UnitType`, `NightlyRate`, `WeeklyRate` (7 nights), `MonthlyRate` (30 nights),
`AdditionalCatNightlyRate` (per cat beyond the first, ≥ 0), `TaxClassId` (HO-ADM-001), `EffectiveFrom`
(≥ now → else `RateCardCannotBeBackdated`; unique per type), `SetBy`. **The card at instant *t*** is
the row with the greatest `EffectiveFrom ≤ t`. A unit type with no current card is **not bookable**
(`UnitTypeNotPriced`). All amounts `Money` ≥ 0, currency = `commerce.currencyCode`.

**Where US-BRD-01's "nightly rate" lives (ADR-221).** The SRS shows availability and prices **by unit
type** (FR-BRD-2). A per-unit nightly rate would make "the price of a deluxe unit" ambiguous and
change with whichever unit the allocator picks. The nightly rate is therefore the unit **type's**
rate card; the unit read returns it as `nightlyRate` and the unit form shows it. OQ-4 records the
alternative.

**`BoardingCareRoutineAggregate`** — per branch, created on first write; `Times`:
`RoutineTime(Kind ∈ {LitterChange, Enrichment, ObservationCheck}, LocalTime)`, ≤ 8 per kind, sorted,
unique. Absent → `BoardingRoutineDefaults` (litter 09:00, 17:00; enrichment 15:00; observation 10:00,
20:00). Method `SetTimes` (replaces all). Drives task generation (§8.9).

### 8.3 `BoardingStayAggregate` — fields

| Field | Type | Notes |
|---|---|---|
| `BranchId`, `CustomerId` | Guid | HO-ADM-001 §8.2 rule; the owner of every cat |
| `Channel` | `BoardingChannel` | `Online`, `Reception` |
| `Status` | `BoardingStayStatus` | §8.5 |
| `ArrivalOn`, `DepartureOn` | `DateOnly` | Branch-local; nights = `DepartureOn − ArrivalOn` (1 … `boarding.maxStayNights`) |
| `OccupiedFrom`, `OccupiedTo` | `DateTimeOffset` | §8.4 — computed by `BoardingOccupancyWindow` from the dates, settings and zone |
| `RequestedUnitType` | `BoardingUnitType` | The type chosen at enquiry |
| `RequiresIsolation` | bool | Derived: any cat `SuspectedInfectious` (§8.12) |
| `Cats` | owned | §8.7 |
| `Allocations` | owned | `UnitAllocation(Id, UnitId, UnitType, From, To, IsActive, Reason?, CreatedBy, CreatedOn)` |
| `RateCardSnapshot` | owned VO | Card id, the four rates, tax class id, taken **at reserve** |
| `RateBasis` | `StayRateBasis` | `Nightly`, `Weekly`, `Monthly` |
| `RateUnits` | owned | §8.8 — the chronological quote |
| `QuoteTotal`, `DiscountTotal` | `Money` | Sum of the units |
| `DepositRequired` | `Money` | §8.10 |
| `DepositDueBy` | `DateTimeOffset?` | `Reserved` only |
| `DepositState` | `DepositState` | `NotRequired`, `Awaiting`, `PendingApproval`, `Received` |
| `DepositPaymentIds` | `Guid[]` ≤ 10 | Billing payments referenced (§12.8 P1) |
| `ConfirmedOn?`, `ConfirmedBy?`, `CheckedInOn?`, `CheckedOutOn?` | | |
| `CancelledOn?`, `CancelledBy?`, `CancellationReason?` (enum), `CancellationNote?` (≤ 500) | | |
| `DepositForfeited` | bool | BR-12 outcome at cancellation (§8.10) |
| `NoShowRecordedOn?`, `NoShowRecordedBy?` | | |
| `NeedsAttention`, `AttentionReason?` | bool / enum | Set by cat events (§11.6); cleared by staff |
| `Charges` | owned | §8.15 |
| `Referrals`, `Approvals`, `VetCommitments` | owned | §8.14 |
| `StatusHistory` | owned | `From`, `To`, `ChangedOn`, `ChangedBy` |

### 8.4 Occupancy and the exclusion constraints — ADR-219

**The occupancy window** of a date range (`BoardingOccupancyWindow`, pure, uses HO-ADM-001's zone
conversion):

```
OccupiedFrom = ArrivalOn   at boarding.checkInWindow.Start   (branch zone → instant)
OccupiedTo   = DepartureOn at boarding.checkOutWindow.End    (branch zone → instant)
               + boarding.turnaroundHours
```

With the defaults (check-in from 14:00, check-out by 11:00, 3 h turnaround) a departure and an arrival
on the same day are back to back, and a turnaround of 24 h leaves a day between them — **US-BRD-02 AC2**
"the configured cleaning turnaround gap". An early check-out truncates the allocation to
`CheckedOutOn + turnaround`.

**One reservation per unit at a time.** A unit holds one household's cats; capacity limits how many
(US-BRD-07). Cats from two households never share a unit (OQ-7).

| Constraint | Table | Stops |
|---|---|---|
| `ex_stay_allocation_unit` | `stay_unit_allocation` | `EXCLUDE USING gist (unit_id WITH =, tstzrange(occupied_from, occupied_to, '[)') WITH &&) WHERE (is_active)` — two stays in one unit |
| `ex_stay_cat` | `stay_cat` | same over `cat_id` — **one cat in two stays (BR-13, D-7)**. A clinic appointment is another table, so D-7's "a stay and a visit may overlap" holds by construction |
| `ex_unit_maintenance` | `unit_maintenance_block` | same over `unit_id` `WHERE NOT is_cancelled` — two blocks on one unit |

`is_active` on an allocation / stay-cat row is **true** in `Reserved`, `Confirmed`, `CheckedIn` and
`CheckedOut` (the truncated range still enforces turnaround after a departure) and **false** in
`Enquiry`, `Cancelled` and `NoShow`. The aggregate maintains it in every transition; U-X3 checks it.

**Maintenance vs stays (F32).** `CreateMaintenanceBlock` pre-checks allocations; every command that
takes a unit pre-checks blocks (inside `UnitClaim`, §11.4). Two administrators racing a block against
a booking within the same millisecond is the residual race; its consequence is a stay overlapping a
block, which the dashboard lists under **attention**. Recorded in ADR-219 rather than solved with a
lock.

### 8.5 The stay status machine — ADR-218

```
Enquiry ──Reserve──► Reserved ──Confirm──► Confirmed ──CheckIn──► CheckedIn ──CheckOut──► CheckedOut
   │                    │                     │                      │
   └─► Cancelled        ├─► Cancelled         ├─► Cancelled          └─(early: CheckOut before DepartureOn)
     (EnquiryExpired,   │   (ReservationLapsed,├─► NoShow
      ByCustomer)       │    ByCustomer, ByStaff)
                        └─ (deposit NotRequired → Confirm immediately, staff only)
```

Appendix C's states exactly. A lapse is `Cancelled` with `CancellationReason.ReservationLapsed` —
not a new state.

| Method | From | Rules | Event |
|---|---|---|---|
| `CreateEnquiry(dto)` | — | Dates valid (§10.3); cats 1…6, distinct, alive, owned by `CustomerId` (handler); `RequestedUnitType` capacity ≥ cats; stores each cat's gate evaluation | `BoardingEnquiryCreatedEvent` |
| `ChangeEnquiry(dto)` | `Enquiry` | Same rules | `BoardingEnquiryChangedEvent` |
| `SetCareProfile(catId, profile)` | `Enquiry`, `Reserved`, `Confirmed`, `CheckedIn` | §8.7 rules. In `CheckedIn` raises the event that regenerates pending tasks (§8.9) | `StayCareProfileSetEvent` |
| `Reserve(unit, card, quote, deposit, gate, now, holdHours)` | `Enquiry` | Every cat: care profile complete (`CareProfileIncomplete`, naming the cat); gate passed or overridden (`VaccinationGateBlocked`); isolation rule (§8.12); capacity (`UnitCapacityExceeded`); adds the allocation, stay-cat ranges active; snapshots card and quote; `DepositDueBy = now + holdHours`; `DepositState = Awaiting` (or `NotRequired` when the deposit is zero) | `BoardingStayReservedEvent` |
| `Confirm(acceptedDeposit, gate, now)` | `Reserved` | `acceptedDeposit ≥ DepositRequired` else `DepositNotYetReceived`; gate re-evaluated (`VaccinationGateBlocked`) | `BoardingStayConfirmedEvent` |
| `MarkDepositPendingApproval(paymentId)` | `Reserved` | Suspends the lapse (§8.10) | — |
| `Lapse(now, depositState)` | `Reserved` | `now ≥ DepositDueBy` and `DepositState ∉ {PendingApproval, Received}` else `ReservationNotLapsable` | `BoardingReservationLapsedEvent` |
| `Cancel(by, reason, note, now, windowHours)` | `Enquiry`, `Reserved`, `Confirmed` | Customer: own; staff: note required. BR-12: `DepositForfeited = now > OccupiedFrom − windowHours` (inside the window) | `BoardingStayCancelledEvent` |
| `CheckIn(catChecklists, gate, now, zone)` | `Confirmed` | On `ArrivalOn` (branch-local) or the day after (a late arrival) else `CheckInNotOnArrivalDate`; **every cat's checklist complete** (§8.11) else `CheckInChecklistIncomplete` naming the missing items — **US-BRD-08 AC2**; gate re-evaluated | `BoardingStayCheckedInEvent` + per cat `StayCatCheckedInEvent` |
| `RecordNoShow(now, graceHours)` | `Confirmed` | `now ≥ OccupiedFrom + graceHours` else `NoShowTooEarly`; deposit forfeited | `BoardingStayNoShowRecordedEvent` |
| `Extend(newDeparture, recalculatedQuote, gate)` | `Confirmed`, `CheckedIn` | `newDeparture > DepartureOn`, ≤ max nights; the current allocation extended; gate as of the new date | `BoardingStayExtendedEvent` |
| `Relocate(targetUnit, fromInstant, reason, by)` | `Confirmed`, `CheckedIn` | Reason required (`RelocationReasonRequired`); target type isolation if `RequiresIsolation`; capacity; current allocation truncated at `fromInstant`, a new one from it | `BoardingStayRelocatedEvent` |
| `CheckOut(catChecklists, recalculation, now)` | `CheckedIn` | Checklist complete (§8.16); early when `now` local date < `DepartureOn` (§8.15) | `BoardingStayCheckedOutEvent` + per cat `StayCatCheckedOutEvent` |

Any other transition → `InvalidStayTransition`, message naming both states (*"A cancelled stay cannot
be checked in."*). Every transition appends to `StatusHistory`. `StayTransitionRules` is one table
used by the aggregate and by the read side's `allowedTransitions[]`.

### 8.6 The vaccination gate — US-BRD-04 — ADR-222

**Rule extracted, not duplicated.** HO-CAT-001 Q4's algorithm moves verbatim into
`Domain/DomainServices/VaccinationStatusRule.Evaluate(catVaccinations, activeCoreVaccines, asOf,
dueSoonDays) → VaccinationEvaluation(OverallStatus, PerVaccine[], EarliestNextDueOn)` (pure).
`GetCatVaccinationStatusQueryHandler` calls it with `asOf = today`; **its output does not change**
(HO-CAT-001's I-tests are the regression guard).

**`CatAggregate.EvaluateVaccinationStatus(activeCoreVaccines, asOf, dueSoonDays)`** — a read-only
method (no mutation, no event, no `UpdateVersion()`) returning the same evaluation over the cat's own
vaccinations. This is `.ai/bounded-contexts.md`'s "`CatAggregate` exposes vaccination status; the
boarding command handler checks it".

**`BoardingVaccinationGate`** (pure): a cat **passes** when
`EvaluateVaccinationStatus(core, asOf: DepartureOn, …).OverallStatus != Overdue` — i.e. no core
vaccine is due on or before the last day of the stay. At booking time this blocks every cat the story
calls *Overdue* (US-BRD-04 AC1) **and** a cat whose vaccine would lapse mid-stay — the reading of
FR-BRD-5's "Current" that protects the facility for the whole stay (OQ-8).

Evaluated by the handler (loading the cat and the active core vaccines) at **enquiry** (stored per cat
as `GateStatus`, `GateReasons[]`, `GateEvaluatedOn`), **reserve**, **confirm**, **check-in** and
**extend**. A blocked enquiry is **kept**: dates, cats and care profiles stay, and **Reserve**
re-evaluates, so once the vaccination is recorded the same booking proceeds with nothing re-entered —
**US-BRD-04 AC2**. The response carries, per blocked cat, the reason (`NeverAdministered`,
`DueBeforeDeparture` with the vaccine and date) and `bookingLink` =
`INotificationLinkBuilder.CatCareLink(catId)` (HO-APT-001's `/book?catId&category=Vaccination`) —
**AC1**.

**Override** — `OverrideVaccinationGateCommand(stayId, catId, reason ≤ 500)`, **Administrator only**
(FR-BRD-5), sets `StayCat.GateOverride(By, On, Reason)`; the override covers this stay only and
survives re-evaluation. The data change is captured by the interceptor, and
`AuditVaccinationOverrideHandler` writes a `BusinessEvent` row (`BoardingVaccinationOverridden`,
entity = stay, reason) — **AC3**.

### 8.7 `StayCat` and the care profile — US-BRD-05, US-BRD-07

| Field | Notes |
|---|---|
| `CatId` | Unique within the stay |
| `OccupiedFrom`, `OccupiedTo`, `IsActive` | Denormalised from the stay for `ex_stay_cat` (F31) |
| `GateStatus`, `GateReasons`, `GateEvaluatedOn`, `GateOverride?` | §8.6 |
| `SuspectedInfectious`, `InfectiousFlaggedBy?`, `InfectiousFlaggedOn?`, `InfectiousNote?` | §8.12 |
| `CareProfile` | owned VO, below; `IsComplete` derived |
| `CheckIn?`, `CheckOut?` | §8.11, §8.16 |

**`CareProfile`** (FR-BRD-6, all required unless marked):

| Part | Shape |
|---|---|
| `Feedings` (1…6) | `LocalTime`, `Portion` (≤ 100, e.g. "⅓ cup dry"), `FoodSource` (`OwnFood` \| `HouseFood`), `FoodDescription` (≤ 200), `HouseFoodSkuId?` (a Shop SKU, optional — Inventory issues against it) |
| `Medications` (0…10) | `Name` (≤ 100), `Dose` (≤ 100), `Route` (≤ 50), `Times` (1…6 `LocalTime`), `Instructions?` (≤ 500), `FromOn?`/`ToOn?` within the stay |
| `LitterPreference` | `LitterType` + `Note?` (≤ 200) |
| `VetVisitAuthorised` | bool — the owner authorises the clinic to examine and treat within the limit |
| `VetSpendingLimit` | `Money` ≥ 0 |
| `EmergencyContact` | `Name` (≤ 200), `Phone` (≤ 30), `Relationship?` (≤ 50) |
| `Notes?` | ≤ 1000, plain text (behaviour, routine) |

Per cat, so a household of three has three profiles and three task streams on **one** reservation —
**US-BRD-07 AC1**. Cats > the chosen unit's `CapacityCats` → `UnitCapacityExceeded`, message *"A
Family unit holds up to 3 cats. You have chosen 4."* — **AC2**. The FE prefills medications and notes
from the cat profile (FR-CAT-3); the server stores what is submitted.

### 8.8 Availability and pricing — US-BRD-02, US-BRD-03 — ADR-220, ADR-221

**`BoardingAvailabilityFinder`** (Domain.DomainServices, **pure**):

```
Find(AvailabilityRequest) → IReadOnlyList<UnitTypeAvailability(UnitType, FreeUnitCount, AllocatedUnitId?, Quote?)>

AvailabilityRequest:
  ArrivalOn, DepartureOn, CatCount, Zone, CheckInWindow, CheckOutWindow, TurnaroundHours
  Units         [(UnitId, Type, CapacityCats, Block, SortOrder, IsActive)]
  BusyUnits     unitId → intervals   (active allocations + maintenance blocks, excluding `ExcludeStayId`)
  RequiresIsolation, Mode (Online | Staff), ExcludeStayId?
  RateCards     type → card at now,  PricingSettings
```

Rules, each tested:

1. Window per §8.4; a unit is **free** if no busy interval overlaps `[OccupiedFrom, OccupiedTo)` — US-BRD-02 AC2.
2. Candidate units: active, `CapacityCats ≥ CatCount`.
3. **Isolation units** are offered only when `RequiresIsolation`, and then **only** isolation units; online mode never offers them (ADR-227).
4. Per type: `FreeUnitCount`; `AllocatedUnitId` = the first free unit by `Block`, `SortOrder`, `Code` — deterministic, so reserve takes the unit that was priced.
5. A type without a current card is omitted.
6. Online: `ArrivalOn ≥ today` (branch), `ArrivalOn ≤ today + boarding.bookingHorizonDays`, and if `ArrivalOn == today` the check-in window has not closed.
7. Result ordered by `BoardingUnitType` order.

**Nothing free** (every type `FreeUnitCount = 0`) → the query runs the finder over shifted ranges of
the same length, up to `boarding.alternativeSearchDays` before and after, and returns up to **three**
nearest alternatives (`alternatives[]`: arrival, departure, cheapest type, total), plus
`waitlistAvailable: true` — **US-BRD-02 AC3** (the waitlist is Phase H, §8.18).

**`BoardingQuoteCalculator`** (pure) — the same function for availability, reserve, extension, early
check-out and cycle invoices:

```
Quote(nights N, catCount, card, longStayThresholdNights T, longStayDiscountPercent D) → Quote
  basis  = N < T ? Nightly : (N < 30 ? Weekly : Monthly)
  units, in chronological order from night 1:
    Monthly basis: floor(N/30) × MONTH (30 nights, card.MonthlyRate), then the remainder as Weekly
    Weekly  basis: floor(R/7)  × WEEK  (7 nights,  card.WeeklyRate),  then the remainder as Nightly
    Nightly: each remaining night × card.NightlyRate
  additional cats: one unit per night-range above × (catCount−1) × card.AdditionalCatNightlyRate × nights
  discount: when basis ≠ Nightly and D > 0, each unit carries Discount = round(unit.Amount × D/100, 2, AwayFromZero)
  total = Σ amount − Σ discount
```

Each `QuotedRateUnit`: `Sequence`, `Kind` (`Month`, `Week`, `Night`, `AdditionalCat`), `FromNight`,
`ToNight`, `Amount`, `Discount`, `Description` (*"Month 1 (nights 1–30) — Deluxe monthly rate"*). The
quote read returns the units grouped with a basis label and the discount as **separate** lines —
**US-BRD-03 AC2** "itemised, not merged". Crossing `T` switches to the weekly or monthly card with the
discount — **AC1**. Discounting per unit makes the sum of per-cycle invoices equal the quote to the
paisa (U-B8).

**Snapshot.** `Reserve` stores the card (`RateCardSnapshot`) and the units. Extension and early
check-out re-run the calculator **with the snapshotted card** (the agreed prices, ADR-049) and the
current threshold/discount settings snapshotted too (`PricingSnapshot`).

### 8.9 Care tasks — US-BRD-09, US-BRD-10 — ADR-224

**`CareTaskAggregate`** — `BranchId`, `StayId`, `CatId`, `UnitId` (the allocation's unit on that
date), `LocalDate`, `Kind` (`CareTaskKind`: `Feed`, `Medication`, `LitterChange`, `Enrichment`,
`ObservationCheck`), `DueOn` (instant), `DueLocalTime`, `SourceKey` (e.g. `feed:{feedingId}`,
`med:{medicationId}:{time}`, `routine:LitterChange:09:00`), `Title` (≤ 200, e.g. *"Feed ⅓ cup dry —
own food"*; *"Give Metacam 0.5 ml oral"*), `AssignedCaretakerId?`, `Status` (`Pending`, `Done`,
`Skipped`, `Voided`), `CompletedOn?`, `CompletedBy?`, `Note?` (≤ 500), `SkipReason?` (≤ 200).

**Unique key** `(stay_id, cat_id, local_date, source_key)` — generation is **idempotent**: running it
twice inserts nothing the second time (the flow doc's requirement).

**Generation** — `GenerateCareTasksCommand(StayId, LocalDate)`: stay `CheckedIn`, date within
`[ArrivalOn, DepartureOn]` (the departure day gets tasks up to the check-out window end); per cat, one
task per feeding time, per medication time within its `FromOn…ToOn`, and per routine time; skip
existing keys; assign (below); save each task (`SaveAsync` per new task — a batch of new roots, all
the same aggregate type, **no other aggregate touched**). Dispatched by:

- `BoardingWorker` every pass for **today and tomorrow** of every `CheckedIn` stay (cheap when done);
- the check-in response path: the FE calls nothing — the worker's next pass (≤ 60 s) generates today's
  list; the check-in handler **does not** create tasks (rule 1);
- `StayCareProfileSetEvent` (in `CheckedIn`) → `VoidPendingTasksOnCareProfileChangedHandler` voids that
  cat's `Pending` tasks for today and later whose `SourceKey` no longer exists or whose time changed;
  the next pass regenerates. Extension needs nothing — the added dates simply start generating —
  **US-BRD-09 AC2**.

**Assignment** (`CareTaskAssigner`, pure, FR-ADM-3): caretakers = staff with the `Caretaker` role
rostered at the branch, with `StaffAvailability` intervals (not restricted to opening hours — boarding
runs outside clinic hours). A task goes to the caretaker on shift at `DueOn`; with several, **all
tasks of one unit in one shift go to the same caretaker**, units distributed round-robin by `Block`
then `Code`; none on shift → unassigned (the dashboard lists unassigned tasks). Assignment is
guidance: **any** caretaker may complete any task.

`StaffLeaveApprovedEvent` → `ReassignCareTasksOnLeaveApprovedHandler` — that caretaker's `Pending`
tasks in the leave range are re-run through the assigner excluding them; idempotent (ADR-048).
`LeaveCancelledEvent` needs nothing (future generation picks the caretaker up again).

**Completion** — `CompleteCareTaskCommand(TaskId, Note?)`: `Pending` → `Done`, `CompletedOn = now`,
`CompletedBy = caller`. **No required body**, so the tablet's second tap is the whole request —
**US-BRD-10 AC1**. `SkipCareTaskCommand(TaskId, Reason)` (e.g. *"refused"*). Completing a `Done` task
→ success, nothing written (a double tap is harmless).

**Overdue** is derived: `Status = Pending ∧ now > DueOn + boarding.taskOverdueGraceMinutes` —
computed by every task read and the dashboard, **never stored** — **US-BRD-10 AC2**.

### 8.10 Deposit, confirmation, lapse, cancellation — US-BRD-06 — ADR-223

**Amount** (`BoardingDepositPolicy`, pure): `boarding.depositFixedAmount > 0` → that amount, capped at
`QuoteTotal`; else `round(QuoteTotal × boarding.depositPercent / 100, 2)`. Zero → `NotRequired`.
**Balance due** = `QuoteTotal − accepted deposit`, returned on every stay read — AC1 "the balance due
is recorded".

**Payment instructions** — the `reserved` response and the `boarding.reserved` message carry the
amount, `payBy` (branch-local) and `paymentInstructionsLink` → the published HO-ADM-001 content page
`page/boarding-payment-instructions` (bank details, how to pay in store). No bank detail is stored in
Boarding (OQ-9).

**Recording** is Billing's: staff record the payment with HO-BIL-001's command, with reference
`{ kind: BoardingStayDeposit, id: stayId }` (§12.8 P1). Boarding then learns it in one of two ways,
neither of which writes Billing data:

1. **Immediately** — the FE follows the Billing call with `POST …/stays/{id}/confirm`.
   `ConfirmBoardingStayCommand` reads `IBoardingDepositLedger.GetAsync(stayId)` (Billing read port):
   accepted total ≥ required → `Confirm`, enqueue `boarding.confirmed` — AC3. Accepted short → `400
   DepositNotYetReceived` with the amount outstanding. A payment awaiting Billing's second approval →
   `MarkDepositPendingApproval`, `202`-free `200` with `depositState: PendingApproval`.
2. **Eventually** — `BoardingWorker` finds `Reserved` stays whose ledger shows the deposit accepted
   (e.g. recorded from Billing's own screen, or approved later) and dispatches the same command.

**While a payment awaits second approval the lapse is suspended** — money has been handed over; a
lapse would release a unit someone paid for. Once Billing rejects the payment the state returns to
`Awaiting` with `DepositDueBy` unchanged (so it may lapse on the next pass).

**Lapse** — the worker dispatches `LapseBoardingReservationCommand` for `Reserved` stays past
`DepositDueBy` whose ledger shows nothing accepted or pending → `Cancelled`
(`ReservationLapsed`), allocations inactive (the unit is free at once), enqueue
`boarding.reservation-lapsed` — AC4, BR-16. A lapse with a **partial** accepted payment still lapses
and appears on the dashboard's **refund due** list for Billing (OQ-10).

**Cancellation** (Appendix C, BR-12): customer (own) or staff (reason). `DepositForfeited` when
cancelled within `boarding.cancellationWindowHours` of `OccupiedFrom`; otherwise the stay emits a
`DepositRefundDue` credit charge line (§8.15) that Billing refunds by the method paid (FR-ORD-17).
Enqueue `boarding.cancelled`.

**No-show** — `RecordNoShowCommand`, staff, after `boarding.noShowGraceHours`; deposit forfeited;
allocations inactive.

### 8.11 Check-in — US-BRD-08

`CheckInBoardingStayCommand(StayId, Cats[])`, per cat a `CheckInRecord`:

| Item | Rule |
|---|---|
| `IdentityConfirmed` | must be `true` |
| `VaccinationProofVerified` | must be `true`; the gate is re-evaluated too |
| `WeightKg` | 0.10 … 30.00 (HO-CAT-001 `Weight`) |
| `ArrivalHealthObservation` | 1 … 1000 chars |
| `Belongings[]` (0…30) | `Kind` (`BelongingKind`: `Carrier`, `Bowl`, `Toy`, `Bedding`, `Food`, `Medication`, `Other`), `Description` (≤ 200), `Media[]` (0…3 `MediaRef`); **or** `NoBelongings = true` |
| Belongings photos | When `boarding.requireBelongingsPhotos` and belongings exist → at least one `MediaRef` across them (OQ-1) |
| `OwnerSignature` | `SignatureCapture` (§8.17) — required |

Any missing item → `CheckInChecklistIncomplete`, message listing each missing item per cat; status
stays `Confirmed` — **AC2**. On success `StayCatCheckedInEvent` (`StayId`, `CatId`, `WeightKg`,
`RecordedOn`) → `RecordCatWeightOnBoardingHandler` creates a `CatWeightRecordAggregate` (HO-CAT-001's
root; the cat itself untouched — ADR-023) so the clinic sees it; a failure there is logged and does
not fail check-in (its summary comment says so, `.ai/domain-events-and-outbox.md` §3). Enqueue
`boarding.checked-in`.

### 8.12 Isolation — US-BRD-18 — ADR-227

- `FlagSuspectedInfectiousCommand(StayId, CatId, Note, TargetIsolationUnitId?)` — Caretaker, Nurse,
  Vet, Administrator. Sets the flag; `RequiresIsolation` becomes true. If the stay's current
  allocation is **not** an isolation unit, `TargetIsolationUnitId` is **required** and the same
  command relocates (§8.5 `Relocate`, reason `SuspectedInfectious`) — **one aggregate**, so a flagged
  cat is never left in a shared block. No free isolation unit → `NoIsolationUnitAvailable` and nothing
  changes (the dashboard lists the cat under attention).
- **Every** allocation — reserve, relocate, extend-with-relocation — with `RequiresIsolation` must be
  an `Isolation` unit → else `IsolationUnitRequired` — **AC1**. Isolation units are never offered to
  a stay that does not require isolation, nor online (ADR-227).
- `ClearSuspectedInfectiousCommand(StayId, CatId, Note)` — Vet, Administrator.
- **`IsolationEntryAggregate`** — `StayId`, `UnitId`, `StaffAccountId` (the caller), `EnteredOn`,
  `LeftOn?`, `Purpose` (≤ 200). `RecordIsolationEntryCommand` (only for a stay in an isolation unit →
  else `NotInIsolation`), `RecordIsolationExitCommand`. Listed on the stay — **AC2**.

### 8.13 Incidents — US-BRD-13 — ADR-226

**`BoardingIncidentAggregate`** — `BranchId`, `StayId`, `CatId`, `Kind` (`IncidentKind`: `Injury`,
`Illness`, `EscapeAttempt`, `Fight`, `RefusalToEat`, `Other`), `Severity` (`Low`, `Medium`, `High`),
`Description` (1…2000), `ImmediateAction` (1…2000), `OccurredOn`, `ReportedBy`, `ReportedOn`,
`OwnerNotified` (bool), `OwnerAcknowledgedOn?`, `AcknowledgedVia?` (`Portal`, `Staff`),
`EscalatedOn?`, `PhoneCallOn?`, `PhoneCallBy?`, `PhoneCallOutcome?` (≤ 500), `Status` (`Open`,
`Resolved`), `ResolvedOn?`, `ResolvedBy?`, `Resolution?` (≤ 1000).

| Command | Rules |
|---|---|
| `RecordIncidentCommand` | Stay `CheckedIn` (or checked out today); cat in the stay. **Medium → enqueue `boarding.incident-medium` (Normal)**; **High → enqueue `boarding.incident-high` (Urgent)**, `OwnerNotified = true` — AC2. Low → no message |
| `AcknowledgeIncidentCommand` (customer, own) · `RecordIncidentAcknowledgementCommand` (staff, e.g. the owner phoned) | Sets `OwnerAcknowledgedOn`; idempotent |
| `EscalateIncidentCommand` | Worker only: `High ∧ OwnerNotified ∧ no ack ∧ now ≥ ReportedOn + boarding.incidentAckEscalationMinutes ∧ not escalated` → `EscalatedOn` — the **phone-call task** appears on the dashboard's *Call owner* list — AC3 |
| `RecordIncidentPhoneCallCommand` | Staff; outcome required; closes the follow-up (and counts as acknowledgement when the owner was reached) |
| `ResolveIncidentCommand` | Resolution required |

The phone-call task is **state on the incident**, not a `CareTask` — creating a task from the
incident would be a second aggregate. "Within 15 minutes" (FR-NTF-7): the message is committed with
the incident and the `Urgent` template bypasses quiet hours and leaves on the next 5-second
notification poll (HO-NTF-001 ADR-060). I-F3 measures it. Media may be attached to an incident
(§8.17), which exempts them from the retention purge (D-10).

### 8.14 Referral and the spending limit — US-BRD-14 — ADR-229

- `ReferStayCatToClinicCommand(StayId, CatId, Reason ≤ 500, Urgency ∈ TriagePriority)` — stay
  `CheckedIn`; adds `VetReferral(Id, CatId, Reason, Urgency, ReferredBy, ReferredOn, VisitId?)`;
  raises **`BoardedCatReferredToClinicEvent`** (`ReferralId`, `StayId`, `CatId`, `CustomerId`,
  `BranchId`, `Urgency`). **HO-EMR-001 subscribes** and opens the visit linked to the stay (§12.8 E1),
  then raises `ReferralVisitOpenedEvent` (`ReferralId`, `VisitId`), which
  `LinkReferralVisitHandler` stores — **AC1** once HO-EMR-001 is merged.
- **Headroom** (`VetSpendingHeadroom`, pure) per cat: `limit = VetVisitAuthorised ? VetSpendingLimit
  : 0` + Σ approved increases; `committed` = Σ `VetCommitments`; `headroom = limit − committed`.
  Exposed as `IBoardingVetSpendingPolicy.GetHeadroomAsync(stayId, catId)` (§12.8 E2) for the clinic
  to decide whether a charge is within the limit — **AC2**'s check.
- **Commitments** — HO-EMR-001 / HO-BIL-001 raise `BoardingVetChargeRecordedEvent` (`ChargeId`,
  `StayId`, `CatId`, `Amount`, `Description`) for each charge on a referred visit (and HO-SRG-001 for
  US-SRG-09); `RecordVetCommitmentHandler` appends a `VetChargeCommitment` (idempotent by `ChargeId`).
  Those charges reach **the stay invoice** by Billing's composition (§12.8 P2) — AC2 "added to the stay
  invoice automatically".
- **Approval above the limit** — `RequestVetChargeApprovalCommand(StayId, CatId, ReferralId?,
  Amount, Description ≤ 200)` — Vet, Reception, Administrator; adds `VetChargeApproval(Pending)` and
  enqueues `boarding.vet-approval-requested` (**Urgent**). The owner answers with
  `RespondToVetChargeApprovalCommand` (customer, own: `Approve` \| `Decline`, note?) or staff record a
  phone answer with `RecordVetChargeApprovalByStaffCommand` (answer, note required — attributed).
  Raises `VetChargeApprovalDecidedEvent` (`ApprovalId`, `StayId`, `CatId`, `Approved`, `Amount`);
  **HO-EMR-001 holds the treatment beyond the limit until this event** — AC3 (§12.8 E4).

### 8.15 Charges, invoicing, extension, early check-out — US-BRD-15, 16, 20 — ADR-230

**`StayChargeLine`** — `Id` (stable; Billing references it), `Kind` (`RateUnit`, `AdditionalCat`,
`LongStayDiscount`, `Adjustment`, `DepositRefundDue`, `EarlyCheckoutCredit`), `FromNight?`/`ToNight?`,
`Description`, `Amount` (signed `Money`), `TaxClassId`, `InvoicedOn?`, `InvoiceId?`, `SupersededBy?`.
Lines are **derived from the rate units** at reserve and re-derived on extension and early check-out:
lines not yet invoiced are replaced; invoiced lines are never changed — a difference becomes a new
line (`Adjustment` or `EarlyCheckoutCredit`). Clinic charges are **not** stay lines — Billing adds
them by stay reference.

**Cycles (US-BRD-20).** A cycle is 30 nights from arrival — the monthly unit's length, so a cycle
boundary never splits a unit (`StayRules.NightsPerCycle = 30`). `BoardingWorker` finds `CheckedIn`
stays where nights elapsed ≥ 30 × (cycles invoiced + 1) and dispatches HO-BIL-001's
`IssueInvoiceFromSourceCommand(Source = BoardingStay, StayId, UpToNight = 30 × k)` (§12.8 P3). The
**final invoice** covers every remaining uninvoiced line — AC2. Billing's `InvoiceIssuedEvent` →
`MarkStayChargesInvoicedHandler` stamps the lines (idempotent). AC-15: a 60-night stay at the monthly
rate gives two invoices of one month each with the discount per invoice (I-G9).

**Extension (US-BRD-15).** `ExtendBoardingStayCommand(StayId, NewDepartureOn)`: `UnitClaim` (§11.4)
for the extra range on the **current** unit. Free → extend allocation and stay-cat ranges, re-quote
with the snapshot card, re-derive lines, gate as of the new date — AC1 (tasks follow, §8.9). Not
free → `400 UnitNotFreeForExtension` with `relocationOptions[]` = units of an allowed type free for
the **remaining** stay through the new departure — AC2. Staff then send
`RelocateBoardingStayCommand(StayId, TargetUnitId, EffectiveFromLocalDate, Reason, NewDepartureOn?)`
— relocation and, when given, extension in one command (one aggregate). The reason is stored on the
new allocation.

**Early check-out (US-BRD-16).** When check-out happens before `DepartureOn`:
`nightsUsed = max(1, localCheckoutDate − ArrivalOn) + boarding.earlyCheckoutChargedNights` (≤ booked),
re-quote with the snapshot card and **the basis recalculated** for `nightsUsed` (a 30-night booking
left after 10 nights falls to the nightly basis and loses the discount) — AC2, shown as new lines.
Difference vs the originally quoted uninvoiced lines → replaced; vs **invoiced** lines → one
`EarlyCheckoutCredit` line (negative), which Billing issues as a credit note and refunds or credits
per `boarding.earlyCheckoutRefundAsCredit` — AC1. The allocation is truncated (§8.4).

### 8.16 Check-out and the stay summary — US-BRD-17

`CheckOutBoardingStayCommand(StayId, Cats[])`, per cat: `WeightKg`, `Belongings[]` returned
(`BelongingId`, `Returned` bool, `Note?` required when not returned), `OwnerSignature`. Incomplete →
`CheckOutChecklistIncomplete`. Weight recorded to the cat as at check-in. Enqueue
`boarding.checked-out` with the summary link.

**The summary is a read** (`GetStaySummaryQuery`), never stored: per cat the daily observations, weight
at check-in and check-out and the change, incidents, medications given (completed `Medication` tasks
with time and caretaker), vet visits (referrals + visit ids), belongings with returned status, the
check-out signature; plus the charge lines, the deposit received (ledger), and the invoice(s) from
Billing's read port — **AC1**. The itemisation of **deposit, nights, extras and vet charges** on the
final invoice is Billing's rendering of those lines (§12.8 P2) — **AC2**, evidenced here by the lines
and the source contract.

### 8.17 Media and signatures — US-BRD-12 — ADR-228, ADR-236

**`MediaRef`** (VO) — HO-CAT-001's reference shape: `StorageKey` (≤ 500), `ContentType`
(`image/jpeg`, `image/png`, `image/webp`, `video/mp4`), `SizeInBytes` (images ≤ 10 MB, video ≤ 50 MB),
`FileName` (≤ 255).

**`StayMediaAggregate`** — `StayId`, `CatId?`, `IncidentId?`, `Media` (`MediaRef`), `Caption?`
(≤ 300), `CapturedOnLocalDate`, `UploadedBy`, `UploadedOn`, `RetainUntil` = `UploadedOn +
boarding.mediaRetentionMonths`, `Status` (`Visible`, `Hidden`, `Purged`). Only for a `CheckedIn` or
checked-out stay. The diary read lists visible items by date — AC1. After the stay the diary stays
readable until `RetainUntil` — AC2 (D-10); the worker marks items past it **`Purged`** unless
`IncidentId` is set (the bytes are storage's, §4.2). Staff may hide an item (reason).

**Signatures are data, not files** (ADR-236). `SignatureCapture` (VO): `SignerName` (≤ 200),
`Method` (`Drawn` \| `Typed`), `Strokes?` (for `Drawn`: JSON array of strokes of `[x,y]` integer pairs,
0…10 000, ≤ 32 KB serialised, validated as numbers only), `ConfirmedStatement` (must be true),
`SignedOn` (server time). `Typed` — name plus an explicit confirmation — is the accessible alternative
(UI-6). No object storage is needed.

### 8.18 Waitlist and daycare — US-BRD-02 AC3, US-BRD-21 — ADR-234, ADR-235

**`BoardingWaitlistEntryAggregate`** — `BranchId`, `CustomerId`, `ArrivalOn`, `DepartureOn`,
`CatCount`, `UnitType?`, `Status` (`Waiting` → `Notified`; `Waiting`/`Notified` → `Withdrawn`;
`Waiting` → `Closed` when the arrival date passes), `NotifiedOn?`. Join only when the finder shows
nothing (`UnitsStillAvailable`); one live entry per customer and range. The worker, **outside quiet
hours**, dispatches `NotifyWaitlistEntryCommand` for the **oldest** `Waiting` entry whose range now has
a free unit, enqueuing `boarding.waitlist-available` with a booking link. **No hold** — the first to
book wins (ADR-235, OQ-11).

**`DaycareVisitAggregate`** (FR-BRD-22, C) — `BranchId`, `CustomerId`, `CatId`, `LocalDate`,
`Basis` (`Hourly` \| `Day`), `ExpectedHours?` (1…12), `Status` (`Booked` → `CheckedIn` → `CheckedOut`;
`Cancelled`, `NoShow`), `PriceSnapshot` (from `DaycareRateCardAggregate`: `HourlyRate`, `DayRate`,
`TaxClassId`, effective-dated), `CheckedInOn?`, `CheckedOutOn?`, `ChargeAmount?` (hours rounded up ×
hourly, capped at the day rate; or the day rate). **Never allocates a unit** — capacity is
`boarding.daycareDailyCapacity` visits per branch per day (0 = daycare not offered →
`DaycareNotOffered`), counted in the handler, raced by a unique `(branch_id, local_date, slot_number)`
row (`ix_daycare_slot`). The vaccination gate (as of that date) applies (OQ-12). Charged through
Billing as source `DaycareVisit` (§12.8 P2).

### 8.19 Messages — ADR-226, ADR-231

Templates appended to HO-NTF-001's catalogue (all **Transactional**, all with `InApp`, wording in
`Sms`, `Email`, `InApp`):

| Key | Urgency | Enqueued by | Variables |
|---|---|---|---|
| `boarding.reserved` | **Urgent** (the owner just booked) | Reserve | `catNames`, `arrivalOn` (Date), `departureOn` (Date), `depositAmount`, `payByLocal`, `paymentInstructionsLink` (Url), `stayLink` (Url) |
| `boarding.confirmed` | **Urgent** | Confirm | `catNames`, `arrivalOn`, `departureOn`, `balanceDue`, `stayLink` |
| `boarding.reservation-lapsed` | Normal | Lapse | `catNames`, `arrivalOn`, `bookLink` (Url) |
| `boarding.cancelled` | Urgent | Cancel | `catNames`, `arrivalOn`, `depositOutcome` (*"kept under our cancellation policy"* / *"will be refunded"* — the producer's wording of a decision already made, as HO-APT-001's `cancelledByClinic`) , `stayLink` |
| `boarding.checked-in` | Normal | Check-in | `catNames`, `departureOn`, `diaryLink` |
| `boarding.daily-update` | Normal | Worker (timed, `RequestNotificationCommand`, dedup `boarding.daily-update:{stayId}:{date}`, at `boarding.dailyUpdateHour`) | `catNames`, `date` (Date), `diaryLink` |
| `boarding.incident-medium` | Normal | Record incident | `catName`, `incidentKind`, `occurredOnLocal`, `incidentLink` |
| `boarding.incident-high` | **Urgent** | Record incident | same |
| `boarding.vet-approval-requested` | **Urgent** | Request approval | `catName`, `amount`, `description`, `approvalLink` |
| `boarding.checked-out` | Normal | Check-out | `catNames`, `summaryLink` |
| `boarding.waitlist-available` | Normal | Worker | `arrivalOn`, `departureOn`, `bookLink` |

Amounts are formatted by the producer (`৳1,500.00` style, from `Money` and the currency); local
instants as HO-APT-001 §8.9 (`ddd d MMM yyyy, HH:mm`). No description, note or health text goes in a
message — the link opens it behind sign-in.

**Guard** — new `NotificationSubjectKind.BoardingStayInHouse`, `BoardingStayInHouseGuard`: valid while
the stay is `CheckedIn`. Used by the daily update (a stay checked out at 17:00 gets no 18:00 update).

**Links** — `INotificationLinkBuilder` gains `BoardingStayLink(stayId)` → `/portal/boarding/{stayId}`,
`BoardingDiaryLink(stayId)` → `…/{stayId}/diary`, `BoardingIncidentLink(stayId, incidentId)` →
`…/{stayId}/incidents/{incidentId}`, `BoardingApprovalLink(stayId, approvalId)` →
`…/{stayId}/approvals/{approvalId}`, `BoardingSummaryLink(stayId)` → `…/{stayId}/summary`,
`BoardingBookLink(arrival?, departure?)` → `/boarding/book?arrival&departure`,
`BoardingPaymentInstructionsLink()` → `/pages/boarding-payment-instructions`.

### 8.20 Ports and domain services

| Name | Kind | Notes |
|---|---|---|
| `BoardingOccupancyWindow`, `BoardingAvailabilityFinder`, `BoardingQuoteCalculator`, `BoardingDepositPolicy`, `BoardingVaccinationGate`, `VaccinationStatusRule`, `CareTaskGenerator`, `CareTaskAssigner`, `VetSpendingHeadroom`, `StayChargeDeriver`, `StayTransitionRules`, `NotEatingRule` | pure domain services | Each unit-tested without a database |
| `IBoardingInputs` | port, Infrastructure | Loads for a branch and range: units, busy intervals (allocations + blocks), current cards, settings, zone. One place, used by the availability query and every command that takes a unit |
| `IBoardingCareInputs` | port | Stays in house for a date with profiles; caretaker availability via HO-ADM-001 |
| `IBoardingDepositLedger` | port — **implemented over HO-BIL-001's tables** | §12.8 P1 |
| `IBoardingStayReferenceReader`, `IBoardingBillableSource`, `IBoardingVetSpendingPolicy` | ports **implemented here, consumed by other contexts** | §12.8 |
| `BoardingStayInHouseGuard` | `INotificationSubjectGuard` | §8.19 |

### 8.21 Exceptions

Reuse `DomainRuleViolationException`. No new type.

---

### 8.21 Surgery on a boarded cat — US-SRG-09 — added 2026-09-22 (README C-7)

> **Why this is here.** HO-SRG-001 and this handover were written in parallel and each deferred
> US-SRG-09 to the other, so the story had no owner. Boarding takes it: the stay, the vet-spending
> headroom (§8.14) and the stay invoice (§8.19) are all here, and Surgery already emits what is
> needed. Surgery builds nothing further.

This slice subscribes to HO-SRG-001's two events and reuses machinery that already exists:

| Step | Reuses |
|---|---|
| `SurgeryScheduledEvent` for a cat with an in-house stay → record the surgery against the stay | The `BoardedCatReferredToClinicEvent` path's stay lookup (§8.14) |
| Charge headroom before the surgery proceeds | `IBoardingVetSpendingPolicy` — **unchanged**; a surgery is a vet charge like any other |
| Over headroom → owner approval, treatment held | `VetChargeApprovalDecidedEvent` (§8.14), already built for US-BRD-15 |
| `SurgeryDischargedEvent` → surgery charges join the stay invoice | `IBoardingBillableSource` (§8.19); HO-BIL-001 composes |
| The cat is in theatre, not its unit | Occupancy shows **Away — in surgery**, the existing `AwayFromUnit` state from the clinic-referral path |

**Rules.** A surgery never releases the unit (the cat returns to it). Care tasks due while the cat is
away are **skipped with a reason**, not marked missed. If the stay ends before discharge from
surgery, check-out is refused (`CatNotPresent`) — the existing referral rule, unchanged.

**Acceptance.** A5 and A9 in §18, which HO-SRG-001 §4.2 marks "evidenced there", are evidenced by
I-G7 and I-G8 in §16.

**Open question OQ-18.** HO-SRG-001 owns whether a surgery on a boarded cat needs the owner's consent
again when the boarding contract already carries a treatment authority. Default: **yes** — consent is
per procedure (HO-EMR-001's registry), and a boarding authority is not surgical consent.

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. **This handover owns ADR-217 … ADR-236** (reserved by the
coordinator; `README.md` is updated by the coordinator, not by this build).

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-217** | **One `Boarding` bounded context**, schema `boarding`, owning the roots of §8.1. `.ai/bounded-contexts.md`'s `BoardingUnit` and `BoardingStay` rows become one context; tasks, observations, incidents, media and isolation entries are **separate roots**, not stay children | The doc lists two contexts and puts tasks and incidents inside the stay; rule 3 says otherwise (OQ-2) |
| **ADR-218** | **Stay states are Appendix C's**; a lapse is `Cancelled/ReservationLapsed`; an `Enquiry` holds no unit and is how a vaccination-blocked booking resumes | A `Lapsed` state or a unit-holding enquiry would each look reasonable and each breaks something (Appendix C; blocked cats would block units) |
| **ADR-219** | **Occupancy by exclusion constraints** on allocation and stay-cat rows over instant ranges derived from local check-in/out windows plus turnaround; **one reservation per unit at a time**; `is_active` maintained by the aggregate; maintenance vs stays cross-checked by handlers with the residual race accepted | ADR-016 says `xmin` is not enough; F31/F32 explain the shape and the one gap |
| **ADR-220** | **Availability is computed** by the pure `BoardingAvailabilityFinder`; deterministic unit selection; alternatives by shifting the range | Rule 4; a stored availability table must be rebuilt on every booking, block and setting change |
| **ADR-221** | **Pricing**: effective-dated rate cards **per unit type**; the pure calculator emits chronological month/week/night units, discount per unit; the card and settings are snapshotted at reserve and reused for extension and early check-out; US-BRD-01's "nightly rate" is the type's card | FR-BRD-2 prices by type; per-unit discount keeps cycle invoices summing to the quote; snapshots honour ADR-049 |
| **ADR-222** | **Vaccination gate**: `VaccinationStatusRule` extracted from HO-CAT-001 Q4; `CatAggregate.EvaluateVaccinationStatus` (read-only); a cat passes when not Overdue **as of departure**; evaluated at enquiry, reserve, confirm, check-in, extend; override by Administrator per cat per stay, reason required, `BusinessEvent` audit | BR-1; the "as of departure" reading and the edit to a HO-CAT-001 file must be deliberate |
| **ADR-223** | **Deposit without a gateway**: Billing records the money against a `BoardingStayDeposit` reference; Boarding confirms only after reading Billing's accepted payments (FE follow-up call or worker pass); a payment awaiting second approval suspends the lapse; the worker lapses expired reservations | FR-BRD-7 v1.1, BR-16, BR-17; the alternative (Boarding recording money) duplicates FR-BIL-12's controls |
| **ADR-224** | **Care tasks** are their own root, generated idempotently per stay, cat, local date and source key, for today and tomorrow by the worker; profile changes void pending tasks; assigned to rostered caretakers per unit and shift; overdue derived | Rule 3; idempotency is the flow doc's named risk; FR-ADM-3's boarding half |
| **ADR-225** | **Observations** are their own root; "not eating" is derived from the last two entries, never stored | A stored flag goes stale the moment a third entry arrives |
| **ADR-226** | **Incidents**: medium → `Normal` template, high → `Urgent` (two definitions, F33); acknowledgement by owner or staff; escalation to a phone-call follow-up **on the incident** by a worker pass | FR-NTF-7; escalation from an event handler would enqueue after commit |
| **ADR-227** | **Isolation**: per-cat flag on the stay; any stay with a flagged cat only in isolation units; flagging in a shared unit relocates in the same command; isolation units never offered online or to non-isolation stays; entries a separate root | BR-9; leaving a flagged cat in place "until someone moves it" is the failure this prevents |
| **ADR-228** | **Media are references** (HO-CAT-001's shape) until object storage exists; retention `RetainUntil`, incident media exempt; the worker marks `Purged` | NFR-SEC-5 / US-XCT-07 are not met by any upload built here; the gap is named |
| **ADR-229** | **Referral and spending limit**: Boarding owns the referral, the per-cat limit, commitments (from clinic events, idempotent) and approval requests (message from the command); `VetVisitAuthorised = false` means zero headroom; the clinic opens the visit and holds treatment | BR-8 across two contexts without either writing the other |
| **ADR-230** | **Charges and invoicing**: Boarding derives charge lines; Billing issues invoices from them by source; 30-night cycles aligned with the monthly unit; invoiced lines immutable, differences as new lines; triggers by the FE after check-out and by the worker catch-up | FR-BRD-21, FR-BRD-17, BR-11 |
| **ADR-231** | **`BoardingWorker`**: one `BackgroundService`, 60 s passes (lapse, confirm, escalate, generate tasks, invoice, waitlist, daily update, purge, close), one command per item through `IInMemoryDispatcher`, never throws, off in `Testing` | The only asynchronous element Boarding adds; its guarantees must be stated |
| **ADR-232** | **No cross-context write from a Boarding command handler.** Other contexts are reached through their commands (worker or FE), read ports, and event handlers that mutate without enqueueing; house food is issued by Inventory's command against a `BoardingStay` reference | Rule 1 held without exception across four neighbouring contexts |
| **ADR-233** | **Roles**: "boarding manager" = `Administrator` (+ `Reception` for booking changes); check-in/out also `Caretaker` (US-BRD-08's persona), a recorded deviation from Appendix D's "Caretaker R" on bookings | No such role exists (HO-ACC-001 `StaffRoles`); the deviation must be visible to ACCEPTANCE |
| **ADR-234** | **Daycare** is a separate aggregate that never allocates a unit; capacity is a daily count raced by a unique slot row | FR-BRD-22 "distinct product", US-BRD-21 "does not consume an overnight unit" |
| **ADR-235** | **Waitlist notifies, never holds**, oldest first, outside quiet hours | A hold on a boarding unit for a waitlisted owner would itself be an unpaid hold (BR-16) |
| **ADR-236** | **Signatures are stored as bounded vector data or a typed confirmation**, not files | Check-in and check-out need a signature and there is no object storage; typed is the accessible path |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| ~~OQ-1~~ | **Closed 2026-09-23 — README C-5.** [`HO-FIL-001`](HO-FIL-001.md) builds it and ships before Shop; storage is GCS (ADR-318), scanning ClamAV (ADR-319). This slice adds the `BoardingMedia` and `BoardingBelongings` purposes and their `FileRules` rows (50 MB incl. MP4; 15 MB images) and stores `fileId` values. **ADR-228's metadata-only fallback is withdrawn** and `boarding.requireBelongingsPhotos` can stay `true`. US-BRD-12's diary is no longer caveated. Note HO-FIL-001 OQ-4: **EXIF is not stripped**, and this slice serves owner-supplied photos back to owners | — | — |
| OQ-2 | `.ai/bounded-contexts.md` needs the merged `Boarding` context and the separate roots | Architect | ADR-217 records it |
| OQ-3 | **SRS Q-6**: deposit %/fixed, cancellation window, early check-out policy | Owner | 30 %, 72 h, charge nights used + 0, refund (not credit) |
| OQ-4 | **SRS Q-7**: rate card; per-type or per-unit nightly rate; multi-cat pricing | Owner | Per type; additional-cat nightly rate on the card (0 unless set); long-stay threshold 14 nights, discount 0 % |
| OQ-5 | **SRS Q-11**: unit counts; dedicated isolation ward | Owner | Units are data; isolation is a unit type |
| OQ-6 | Should a `BoardingManager` staff role be added? | Owner | No — ADR-233 mapping |
| OQ-7 | May cats of two households share a unit? | Owner | No — one reservation per unit |
| OQ-8 | Gate "as of departure" (stricter than the story's "Overdue at booking")? | Owner + clinical lead | Yes — FR-BRD-5 "Current" for the whole stay |
| OQ-9 | Bank account and in-store payment wording | Owner | A content page `boarding-payment-instructions` (HO-ADM-001), edited by an administrator |
| OQ-10 | Partial deposit at the lapse — lapse, or keep holding? | Owner | Lapse; listed as *refund due* for Billing |
| OQ-11 | Should a waitlist notice hold a unit? | Owner | No (ADR-235) |
| OQ-12 | Does the vaccination gate apply to daycare? | Owner + clinical lead | Yes |
| OQ-13 | Is house food charged, or included in the rate? | Owner | Included — Inventory records it at cost, no charge line |
| OQ-14 | Extra deposit on extension? | Owner | None |
| OQ-15 | Should escalation also message staff (not only the dashboard)? | Owner | Dashboard only |
| OQ-16 | Billing cycle — 30 nights from arrival or calendar month? | Accountant | 30 nights (aligned with the monthly unit) |
| OQ-17 | Should owners submit a transfer reference themselves (as FR-ORD-22)? | Owner | Not built — staff record it in Billing |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `BoardingUnitType` | `Standard` (1), `Deluxe` (2), `Family` (3), `Isolation` (4) |
| `BoardingStayStatus` | `Enquiry`, `Reserved`, `Confirmed`, `CheckedIn`, `CheckedOut`, `Cancelled`, `NoShow` |
| `BoardingCancellationReason` | `ByCustomer`, `ByStaff`, `ReservationLapsed`, `EnquiryExpired` |
| `BoardingChannel` | `Online`, `Reception` |
| `DepositState` | `NotRequired`, `Awaiting`, `PendingApproval`, `Received` |
| `StayRateBasis` | `Nightly`, `Weekly`, `Monthly` |
| `RateUnitKind` | `Month`, `Week`, `Night`, `AdditionalCat` |
| `StayChargeKind` | `RateUnit`, `AdditionalCat`, `LongStayDiscount`, `Adjustment`, `DepositRefundDue`, `EarlyCheckoutCredit` |
| `FoodSource` | `OwnFood`, `HouseFood` |
| `LitterType` | `Clumping`, `Silica`, `Tofu`, `Bentonite`, `Paper`, `Other` |
| `VaccinationGateStatus` | `Passed`, `Blocked`, `Overridden` |
| `BelongingKind` | `Carrier`, `Bowl`, `Toy`, `Bedding`, `Food`, `Medication`, `Other` |
| `SignatureMethod` | `Drawn`, `Typed` |
| `CareTaskKind` | `Feed`, `Medication`, `LitterChange`, `Enrichment`, `ObservationCheck` |
| `CareTaskStatus` | `Pending`, `Done`, `Skipped`, `Voided` |
| `RoutineTaskKind` | `LitterChange`, `Enrichment`, `ObservationCheck` |
| `PortionEaten` | `None` (0), `Quarter`, `Half`, `ThreeQuarters`, `All` |
| `WaterIntake` | `None`, `Low`, `Normal`, `High` |
| `LitterOutput` | `Normal`, `None`, `Abnormal` |
| `CatMood` | `Relaxed`, `Playful`, `Anxious`, `Withdrawn`, `Aggressive` |
| `IncidentKind` | `Injury`, `Illness`, `EscapeAttempt`, `Fight`, `RefusalToEat`, `Other` |
| `IncidentSeverity` | `Low` (1), `Medium` (2), `High` (3) |
| `IncidentStatus` | `Open`, `Resolved` |
| `AcknowledgementChannel` | `Portal`, `Staff` |
| `VetApprovalStatus` | `Pending`, `Approved`, `Declined` |
| `StayMediaStatus` | `Visible`, `Hidden`, `Purged` |
| `StayAttentionReason` | `CatDeceased`, `CatOwnerChanged`, `MaintenanceOverlap`, `IsolationUnavailable` |
| `BoardingWaitlistStatus` | `Waiting`, `Notified`, `Withdrawn`, `Closed` |
| `DaycareBasis` | `Hourly`, `Day` |
| `DaycareVisitStatus` | `Booked`, `CheckedIn`, `CheckedOut`, `Cancelled`, `NoShow` |

HO-NTF-001's `NotificationSubjectKind` gains **`BoardingStayInHouse`** (append only). HO-APT-001's
`TriagePriority` is reused for referral urgency.

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`

`boarding.depositPercent` (30.00, 0–100, branch ✅) is already in the registry; this slice is its
consumer. New:

| Key | Group | Type | Default | Bounds | Branch |
|---|---|---|---|---|---|
| `boarding.depositFixedAmount` | Boarding | Decimal | 0.00 (use percent) | 0–1 000 000 | ✅ |
| `boarding.reservationHoldHours` | Boarding | Integer | 48 | 1–168 | ✅ |
| `boarding.checkInWindow` | Boarding | TimeRange | 14:00–19:00 | start < end | ✅ |
| `boarding.checkOutWindow` | Boarding | TimeRange | 08:00–11:00 | start < end | ✅ |
| `boarding.turnaroundHours` | Boarding | Integer | 3 | 0–48 | ✅ |
| `boarding.longStayThresholdNights` | Boarding | Integer | 14 | 2–60 | ✅ |
| `boarding.longStayDiscountPercent` | Boarding | Decimal | 0.00 | 0–50 | ✅ |
| `boarding.bookingHorizonDays` | Boarding | Integer | 180 | 1–365 | ✅ |
| `boarding.maxStayNights` | Boarding | Integer | 120 | 1–365 | ✅ |
| `boarding.alternativeSearchDays` | Boarding | Integer | 14 | 1–60 | ✅ |
| `boarding.cancellationWindowHours` | Boarding | Integer | 72 | 0–720 | ✅ |
| `boarding.earlyCheckoutChargedNights` | Boarding | Integer | 0 | 0–7 | ✅ |
| `boarding.earlyCheckoutRefundAsCredit` | Boarding | Boolean | false | — | ✅ |
| `boarding.noShowGraceHours` | Boarding | Integer | 12 | 0–72 | ✅ |
| `boarding.taskOverdueGraceMinutes` | Boarding | Integer | 30 | 0–240 | ✅ |
| `boarding.incidentAckEscalationMinutes` | Boarding | Integer | 30 | 5–240 | ✅ |
| `boarding.dailyUpdateHour` | Boarding | Integer | 18 | 0–23 | ✅ |
| `boarding.mediaRetentionMonths` | Boarding | Integer | 12 | 1–84 | ❌ |
| `boarding.requireBelongingsPhotos` | Boarding | Boolean | true | — | ✅ |
| `boarding.daycareDailyCapacity` | Boarding | Integer | 0 (not offered) | 0–100 | ✅ |

`TimeRange` with `start < end` is a new bound flag in the registry validator (one line, U-A9). No
migration — values are rows.

### 10.3 Other constants

`StayRules` (`MaxCatsPerStay = 6`, `NightsPerCycle = 30`, `NightsPerWeekUnit = 7`,
`NightsPerMonthUnit = 30`, collection bounds of §8.1) · `CareProfileRules` · `BoardingRoutineDefaults`
· `IncidentRules` · `MediaRules` (types, sizes) · `SignatureRules` (32 KB, 10 000 points) ·
`BoardingMessages` · the eleven template definitions of §8.19.

### 10.4 Violation codes — append to `BusinessViolationCodes`

`BoardingUnitNotFound` · `UnitCodeAlreadyExists` · `UnitHasFutureStays` · `MaintenanceOverlapsStays` ·
`MaintenanceOverlapsMaintenance` · `RateCardCannotBeBackdated` · `DuplicateRateCardEffectiveDate` ·
`UnitTypeNotPriced` · `InvalidStayDates` · `StayTooLong` · `StayBeyondHorizon` · `ArrivalTooLate` ·
`StayNotFound` · `InvalidStayTransition` · `CatNotBoardable` · `DuplicateCatInStay` ·
`UnitCapacityExceeded` · `NoUnitAvailable` · `UnitNoLongerAvailable` · `CatAlreadyBoarding` ·
`VaccinationGateBlocked` · `OverrideReasonRequired` · `CareProfileIncomplete` · `InvalidCareProfile` ·
`DepositNotYetReceived` · `ReservationNotLapsable` · `CancellationReasonRequired` ·
`CheckInNotOnArrivalDate` · `CheckInChecklistIncomplete` · `CheckOutChecklistIncomplete` ·
`InvalidSignature` · `NoShowTooEarly` · `UnitNotFreeForExtension` · `RelocationReasonRequired` ·
`IsolationUnitRequired` · `NoIsolationUnitAvailable` · `NotInIsolation` · `CareTaskNotFound` ·
`CareTaskNotPending` · `ObservationNotAllowed` · `IncidentNotFound` · `IncidentStayNotInHouse` ·
`PhoneCallOutcomeRequired` · `ReferralStayNotInHouse` · `VetApprovalNotFound` ·
`VetApprovalAlreadyDecided` · `InvalidMediaReference` · `MediaStayNotInHouse` · `UnitsStillAvailable` ·
`AlreadyWaitlisted` · `DaycareNotOffered` · `DaycareFull`.

`UnitNoLongerAvailable` and `CatAlreadyBoarding` are raised by the pre-check and, on a race, by the
constraint mapping (§12.5). `CatAlreadyBoarding` names the other stay's dates, never its id.

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Boarding/`

Namespace `CatEcommerce.Application.Commands.Boarding`. Data only; ids minted in the controller or
worker; `ValidateCommand = true` everywhere (F3).

| # | Command | Sent by | Story | Phase |
|---|---|---|---|---|
| C1 | `CreateBoardingUnitCommand` · `UpdateBoardingUnitCommand` · `SetBoardingUnitActivationCommand` | Admin | BRD-01 | B |
| C2 | `CreateMaintenanceBlockCommand` · `CancelMaintenanceBlockCommand` | Admin | BRD-01 | B |
| C3 | `SetBoardingRateCardCommand` (`UnitType`, rates, `TaxClassId`, `EffectiveFrom`) · `SetDaycareRateCardCommand` | Admin | BRD-03, 21 | B, H |
| C4 | `SetBoardingCareRoutineCommand` (`BranchId`, `Times[]`) | Admin | BRD-09 | B |
| C5 | `CreateBoardingEnquiryCommand` (`StayId`, `BranchId`, `CustomerId` — customer: from session, `ArrivalOn`, `DepartureOn`, `UnitType`, `CatIds[]`) · `ChangeBoardingEnquiryCommand` | Customer, staff | BRD-02, 04, 07 | C |
| C6 | `SetStayCareProfileCommand` (`StayId`, `CatId`, `CareProfile`) | Customer, staff | BRD-05 | C |
| C7 | `OverrideVaccinationGateCommand` (`StayId`, `CatId`, `Reason`) | Admin | BRD-04 | C |
| C8 | `ReserveBoardingStayCommand` (`StayId`) | Customer, staff | BRD-06 | D |
| C9 | `ConfirmBoardingStayCommand` (`StayId`) | Staff, worker | BRD-06 | D |
| C10 | `LapseBoardingReservationCommand` (`StayId`) · `ExpireBoardingEnquiryCommand` | Worker | BRD-06 | D |
| C11 | `CancelBoardingStayCommand` (`StayId`, `Note?`) | Customer, staff | BR-12 | D |
| C12 | `CheckInBoardingStayCommand` (`StayId`, `Cats[]`: checklist) | Staff | BRD-08 | E |
| C13 | `RecordBoardingNoShowCommand` (`StayId`) | Staff | App. C | D |
| C14 | `GenerateCareTasksCommand` (`StayId`, `LocalDate`) | Worker | BRD-09 | E |
| C15 | `CompleteCareTaskCommand` (`TaskId`, `Note?`) · `SkipCareTaskCommand` (`TaskId`, `Reason`) | Staff | BRD-10 | E |
| C16 | `RecordCareObservationCommand` (`ObservationId`, `StayId`, `CatId`, `PortionEaten`, `WaterIntake`, `LitterOutput`, `Mood`, `Notes?`) | Staff | BRD-11 | E |
| C17 | `FlagSuspectedInfectiousCommand` · `ClearSuspectedInfectiousCommand` · `RecordIsolationEntryCommand` · `RecordIsolationExitCommand` | Staff | BRD-18 | E |
| C18 | `AddStayMediaCommand` (`MediaId`, `StayId`, `CatId?`, `IncidentId?`, `MediaRef`, `Caption?`, `CapturedOnLocalDate`) · `HideStayMediaCommand` · `PurgeExpiredStayMediaCommand` (worker, one item) | Staff, worker | BRD-12 | F |
| C19 | `RecordIncidentCommand` · `AcknowledgeIncidentCommand` (customer) · `RecordIncidentAcknowledgementCommand` · `EscalateIncidentCommand` (worker) · `RecordIncidentPhoneCallCommand` · `ResolveIncidentCommand` | Staff, customer, worker | BRD-13 | F |
| C20 | `ReferStayCatToClinicCommand` · `RequestVetChargeApprovalCommand` · `RespondToVetChargeApprovalCommand` (customer) · `RecordVetChargeApprovalByStaffCommand` | Staff, customer | BRD-14 | G |
| C21 | `ExtendBoardingStayCommand` (`StayId`, `ExpectedVersion`, `NewDepartureOn`) · `RelocateBoardingStayCommand` (`StayId`, `ExpectedVersion`, `TargetUnitId`, `EffectiveFromLocalDate`, `Reason`, `NewDepartureOn?`) | Staff | BRD-15 | G |
| C22 | `CheckOutBoardingStayCommand` (`StayId`, `Cats[]`: checklist) | Staff | BRD-16, 17 | G |
| C23 | `ClearStayAttentionCommand` (`StayId`, `Note`) | Staff | — | G |
| C24 | `JoinBoardingWaitlistCommand` · `WithdrawBoardingWaitlistCommand` · `NotifyWaitlistEntryCommand` (worker) · `CloseStaleBoardingWaitlistCommand` (worker) | Customer, worker | BRD-02 AC3 | H |
| C25 | `BookDaycareVisitCommand` · `CheckInDaycareVisitCommand` · `CheckOutDaycareVisitCommand` · `CancelDaycareVisitCommand` · `RecordDaycareNoShowCommand` | Customer, staff | BRD-21 | H |

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: ids non-empty; text lengths; enum membership; `DepartureOn > ArrivalOn`; care profile
counts and `LocalTime` distinctness per feeding list; `MediaRef` type/size; signature size and
number-only strokes; money non-negative with scale 2; `Reason`/`Note` required where §8 says so.
Availability, gate, capacity, isolation, cut-offs and checklist completeness are aggregate or handler
rules.

### 11.3 Mappers and DTOs

`BoardingUnitMapper`, `BoardingStayMapper`, `CareTaskMapper`, `IncidentMapper`, `DaycareMapper`
(static `ToDomainDto()`). DTOs in `Domain/Models/` (flat): `BoardingUnitDto`, `RateCardDto`,
`EnquiryDto`, `CareProfileDto`, `CheckInCatDto`, `CheckOutCatDto`, `SignatureCaptureDto`,
`MediaRefDto`, `QuoteDto`, `IncidentDto`, `ObservationDto`, `DaycareBookingDto`.

### 11.4 Command handlers — `CommandHandlers/Boarding/`

Every handler that **takes a unit** (C8, C21, C17-with-relocation) follows one sequence, implemented
once in `UnitClaim` (Application, a static helper over `IBoardingInputs` — not a service layer):

1. Load inputs for the branch and the range.
2. Validate with `BoardingAvailabilityFinder` in the handler's mode: the target unit (or the type's
   deterministic unit) is free of allocations **and** maintenance, of an allowed type, capacity ≥ cats
   — on failure the specific code (`UnitNoLongerAvailable`, `IsolationUnitRequired`,
   `UnitCapacityExceeded`, `UnitNotFreeForExtension`).
3. Check each cat is in no other active stay overlapping the range (`CatAlreadyBoarding`).
4. Mutate and save once. A `23P01` → the constraint's code (§12.5), `409`.

| Handler | Specifics |
|---|---|
| **C5 Enquiry** | Customer: `CustomerId` from session; cats owned and alive (`404` for another's cat, `CatNotBoardable` for deceased); unit type priced; gate per cat (§8.6); `CreateEnquiry`. Response carries the gate and a quote from the finder (not stored until reserve) |
| **C6 Care profile** | Own stay (`404`); `SetCareProfile` |
| **C7 Override** | Administrator; reason; `OverrideGate` |
| **C8 Reserve** | Re-evaluate gate; `UnitClaim` for the type's deterministic unit; quote with the current card and settings; deposit policy; `Reserve`; enqueue `boarding.reserved`; `UpdateAsync` |
| **C9 Confirm** | Staff or worker; `IBoardingDepositLedger`; accepted ≥ required → gate → `Confirm` + enqueue `boarding.confirmed`; pending → `MarkDepositPendingApproval` (no message); worker: skip silently if not ready |
| **C10 Lapse** | Worker; ledger re-read inside the handler (never trust the pass's snapshot); `Lapse` + enqueue; not lapsable → success, nothing written |
| **C11 Cancel** | Customer own / staff; BR-12 outcome; enqueue `boarding.cancelled`; derives `DepositRefundDue` when not forfeited and a deposit was received |
| **C12 Check-in** | Staff; gate; `boarding.requireBelongingsPhotos`; `CheckIn`; enqueue `boarding.checked-in` |
| **C14 Generate** | §8.9; loads the stay read-only through the repository, creates tasks (`SaveAsync` each), never updates the stay |
| **C15 Complete / skip** | `CompletedBy` from `UserContext`; idempotent on `Done` |
| **C16 Observation** | Stay `CheckedIn`, cat in it (`ObservationNotAllowed`); `SaveAsync` |
| **C17 Isolation** | §8.12; with relocation → `UnitClaim` for the isolation unit |
| **C18 Media** | Stay in house or checked out ≤ 1 day; `MediaRef` rules; `RetainUntil` from settings |
| **C19 Incident** | §8.13; the message enqueued before `SaveAsync(incident)` |
| **C20 Referral / approvals** | §8.14; the approval message enqueued before `UpdateAsync(stay)` |
| **C21 Extend / relocate** | `ExpectedVersion` (`409` on stale); §8.15; re-derive lines |
| **C22 Check-out** | Checklist; early-checkout recalculation; truncate allocation; enqueue `boarding.checked-out` |
| **C24 Waitlist** | §8.18 |
| **C25 Daycare** | §8.18; capacity by the next free `slot_number` (`23505` on `ix_daycare_slot` → `409 DaycareFull`) |

### 11.5 Domain events — `Events/Boarding/`

Ids, instants, dates, enums, amounts and booleans only — **no care notes, health text, descriptions
or names** (U-X1).

`BoardingUnitCreatedEvent`, `BoardingUnitUpdatedEvent`, `BoardingUnitActivationChangedEvent`,
`BoardingEnquiryCreatedEvent`, `BoardingEnquiryChangedEvent`, `StayCareProfileSetEvent`,
`VaccinationGateOverriddenEvent`, `BoardingStayReservedEvent`, **`BoardingStayConfirmedEvent`**,
`BoardingReservationLapsedEvent`, `BoardingStayCancelledEvent`, `BoardingStayNoShowRecordedEvent`,
`BoardingStayCheckedInEvent`, **`StayCatCheckedInEvent`** (`WeightKg`), `BoardingStayExtendedEvent`,
`BoardingStayRelocatedEvent`, `BoardingStayCheckedOutEvent`, `StayCatCheckedOutEvent`,
`SuspectedInfectiousFlaggedEvent`, `SuspectedInfectiousClearedEvent`, `CareTaskCompletedEvent`,
`CareObservationRecordedEvent`, `IncidentRecordedEvent`, `IncidentEscalatedEvent`,
**`BoardedCatReferredToClinicEvent`** (HO-EMR-001 contract), **`VetChargeApprovalDecidedEvent`**
(HO-EMR-001 contract), `StayChargesChangedEvent` (HO-BIL-001 may use to refresh a draft),
`DaycareVisitCheckedOutEvent` (HO-BIL-001 contract).

### 11.6 Domain event handlers — `DomainEventHandlers/Boarding/`

| Handler | Handles | Does |
|---|---|---|
| `RecordCatWeightOnBoardingHandler` | `StayCatCheckedInEvent`, `StayCatCheckedOutEvent` | Creates `CatWeightRecordAggregate`; logs and swallows failures (stated in its summary) |
| `VoidPendingTasksOnCareProfileChangedHandler` | `StayCareProfileSetEvent` | §8.9 |
| `VoidTasksOnStayEndedHandler` | `BoardingStayCheckedOutEvent`, `BoardingStayCancelledEvent` | Voids remaining `Pending` tasks after the end |
| `ReassignCareTasksOnLeaveApprovedHandler` | HO-ADM-001 `StaffLeaveApprovedEvent` | §8.9; idempotent |
| `FlagStaysOnCatDeceasedHandler` · `FlagStaysOnCatTransferredHandler` | HO-CAT-001 events | Future/in-house stays with the cat → `NeedsAttention` with the reason; **never cancel, never enqueue** |
| `ReassignStaysOnCustomerMergedHandler` | HO-ACC-001 `CustomerMergedEvent` | Stays, waitlist entries and daycare visits of the duplicate → survivor; idempotent (ADR-039) |
| `AuditVaccinationOverrideHandler` | `VaccinationGateOverriddenEvent` | `BusinessEvent` audit with the reason |
| `LinkReferralVisitHandler` | HO-EMR-001 `ReferralVisitOpenedEvent` | Stores `VisitId`; idempotent |
| `RecordVetCommitmentHandler` | `BoardingVetChargeRecordedEvent` (HO-EMR-001 / HO-BIL-001 / HO-SRG-001) | Appends a commitment; idempotent by `ChargeId` |
| `MarkStayChargesInvoicedHandler` | HO-BIL-001 `InvoiceIssuedEvent` (lines with source `BoardingStay`) | Stamps `InvoiceId`/`InvoicedOn`; idempotent |
| `ApplyBillingDepositOutcomeHandler` | HO-BIL-001 `PaymentRejectedEvent` (reference `BoardingStayDeposit`) | `PendingApproval` → `Awaiting`; no message |

All loop with one `UpdateAsync` per row, are idempotent, and **never enqueue** (HO-NTF-001 ADR-057).

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

`DbSet`s for the fourteen roots; one configuration per root; schema `boarding`; HO-ACC-001 §12.1 /
ADR-021 conventions; `Money` as `amount numeric(12,2)` + `currency_code char(3)`; `LocalTime` →
`time`; `DateOnly` → `date`; instants `timestamptz`; signature strokes `jsonb`.

| Index / constraint | Kind |
|---|---|
| `ex_stay_allocation_unit` | exclusion on `stay_unit_allocation` (§8.4) |
| `ex_stay_cat` | exclusion on `stay_cat` (§8.4) |
| `ex_unit_maintenance` | exclusion on `unit_maintenance_block` |
| `unit (branch_id, code)` | **UNIQUE** `ix_boarding_unit_branch_code` |
| `rate_card (unit_type, effective_from)` · `daycare_rate_card (effective_from)` | **UNIQUE** |
| `stay (branch_id, arrival_on)`, `(branch_id, departure_on)`, `(branch_id, status)`, `(customer_id, arrival_on)` | arrivals, departures, dashboard, "my stays" |
| `stay (status, deposit_due_by)` filtered `WHERE status = Reserved` | lapse pass |
| `stay_unit_allocation (unit_id, occupied_from)` | availability inputs |
| `care_task (stay_id, cat_id, local_date, source_key)` | **UNIQUE** `ix_care_task_generation` |
| `care_task (branch_id, local_date, status)`, `(assigned_caretaker_id, local_date)` | day lists, overdue |
| `care_observation (stay_id, cat_id, recorded_on)` | not-eating rule, summary |
| `incident (branch_id, status)`, `(severity, owner_acknowledged_on, escalated_on)` filtered `WHERE status = Open` | dashboard, escalation pass |
| `stay_media (stay_id, captured_on_local_date)`, `(retain_until)` filtered `WHERE status <> Purged AND incident_id IS NULL` | diary, purge |
| `isolation_entry (stay_id, entered_on)` | |
| `waitlist_entry (branch_id, arrival_on, status, created_on)` · **UNIQUE** `(customer_id, arrival_on, departure_on)` filtered `WHERE status IN (Waiting, Notified)` | pass; `ix_boarding_waitlist_active` |
| `daycare_visit (branch_id, local_date, slot_number)` **UNIQUE** filtered `WHERE status NOT IN (Cancelled)` | `ix_daycare_slot` |

Predicates reference **`Enumeration.Value`** from code, never literals (U-X3).

**Migrations**, each read before commit: `AddBoardingUnits` (B), `AddBoardingStays` (C — `CREATE
EXTENSION IF NOT EXISTS btree_gist;`, both stay exclusion constraints; the `Down` keeps the
extension), `AddBoardingCare` (E), `AddBoardingIncidentsAndMedia` (F), `AddBoardingCharges` (G),
`AddBoardingWaitlistAndDaycare` (H). Phases A and D add none (D's columns ship in C).

### 12.2 Infrastructure services and the worker

| Class | Notes |
|---|---|
| `Boarding/BoardingInputs : IBoardingInputs`, `Boarding/BoardingCareInputs : IBoardingCareInputs` | `AsNoTracking`; HO-ADM-001 `IBranchCalendar` zone and `StaffAvailability` |
| `Boarding/BillingDepositLedger : IBoardingDepositLedger` | Read-only over HO-BIL-001's payment table by reference (§12.8 P1) |
| `Notifications/Guards/BoardingStayInHouseGuard` | §8.19 |
| `Notifications/NotificationLinkBuilder` | the seven methods of §8.19 added |

**`BoardingWorker : BackgroundService`** (Api, `Background/`) — HO-APT-001's `ClinicWorker` shape
(scope per unit of work, never throws out of `ExecuteAsync`, disabled in `Testing`,
`BoardingWorkCycle` callable by tests). Every `Boarding:Worker:IntervalSeconds` (60), in order:
confirm ready reservations → lapse expired → expire past enquiries → escalate incidents → generate
tasks (today, tomorrow) → cycle and final invoices (dispatch HO-BIL-001's command) → daily updates at
the hour → waitlist notices (outside quiet hours) → close stale waitlist → purge media. Each item is
one command through `IInMemoryDispatcher`. `Boarding:Worker:Enabled` in `appsettings.json`.

### 12.3 Authorization — ADR-233

Appendix D: *Boarding bookings* Customer W (own), Reception F, Vet R, Caretaker R, Admin F; *Care logs
& tasks* Customer R (own), Reception R, Nurse W, Vet R, Caretaker F, Admin F; *Incidents* Customer R
(own), Reception R, Nurse W, Vet W, Caretaker W, Admin F. Appended to the one `ActionRoles` table:

| Action | Staff roles | Endpoints |
|---|---|---|
| `Boarding.ViewUnits` | Reception, Nurse, Vet, Caretaker, Administrator | units, blocks, rate cards, routine reads |
| `Boarding.ManageUnits` | Administrator | units, blocks, rate cards, daycare card, routine writes |
| `Boarding.ViewStays` | Reception, Nurse, Vet, Caretaker, Administrator | stays, detail, summary, availability (staff), day lists, dashboard |
| `Boarding.Book` | Reception, Administrator | enquiry, care profile, reserve, cancel, no-show, confirm, daycare, attention |
| `Boarding.OverrideVaccination` | Administrator | override |
| `Boarding.ChangeStay` | Reception, Administrator | extend, relocate |
| `Boarding.CheckInOut` | Reception, Caretaker, Administrator | check-in, check-out *(Caretaker: recorded deviation)* |
| `Boarding.RecordCare` | Nurse, Caretaker, Administrator | complete/skip tasks, observations, media |
| `Boarding.RecordIncident` | Nurse, Vet, Caretaker, Administrator | record incident |
| `Boarding.FollowUpIncident` | Reception, Administrator | acknowledgement by staff, phone call, resolve |
| `Boarding.Isolation` | Nurse, Vet, Caretaker, Administrator | flag, isolation entry/exit; **clear flag: Vet, Administrator** (`Boarding.ClearIsolation`) |
| `Boarding.Refer` | Nurse, Vet, Caretaker, Administrator | referral |
| `Boarding.RequestVetApproval` | Reception, Vet, Administrator | approval request, staff-recorded answer |

Customer endpoints carry `[RequireCustomerSession]`; every handler resolves the stay **by id and
caller** — someone else's stay, incident, approval or media is **`404`**. Availability is
`[PublicEndpoint]` with the `availability` rate limit (HO-APT-001). Enquiry creation uses a
`boarding-booking` limit (10 per minute per IP).

### 12.4 Api — the contract HO-BRD-002 consumes

JSON camelCase; instants ISO-8601 with offset; local dates `yyyy-MM-dd`; times `HH:mm`; money as
HO-ADM-001 §12.4 (amount string). Every stay view carries `branchTimeZoneId`, `version`,
`allowedTransitions[]` (from `StayTransitionRules`) and, per cat, `catName`, `catPhotoUrl?`,
`catIsHighRisk`, `handlingNote?`.

**Public and customer**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/boarding/availability?branchId&arrivalOn&departureOn&catCount` | public · `availability` | `{ branchTimeZoneId, nights, occupiedFrom, occupiedTo, unitTypes: [{ unitType, capacityCats, freeUnitCount, quote: { basis, units[], discountLines[], total } }], alternatives: [{ arrivalOn, departureOn, unitType, total }], waitlistAvailable }` |
| `GET` | `/api/boarding/unit-types` | public | types with capacity and **current** nightly/weekly/monthly rates |
| `POST` | `/api/customers/me/boarding/enquiries` | customer · `boarding-booking` | C5 → `201 { stayId, vaccinationGate: { passed, blockedCats: [{ catId, catName, reasons[], bookingLink }] }, quote }` |
| `PUT` | `/api/customers/me/boarding/stays/{stayId}/enquiry` | customer | C5 change → `200 { … same }` |
| `PUT` | `/api/customers/me/boarding/stays/{stayId}/cats/{catId}/care-profile` | customer | C6 → `204` |
| `POST` | `/api/customers/me/boarding/stays/{stayId}/reserve` | customer | C8 → `200 { stay, deposit: { amount, payBy, instructionsPath } }`; `400 VaccinationGateBlocked` (+ gate), `CareProfileIncomplete`; `409 UnitNoLongerAvailable` / `CatAlreadyBoarding` |
| `POST` | `/api/customers/me/boarding/stays/{stayId}/cancel` | customer | C11 → `200 { depositForfeited }` |
| `GET` | `/api/customers/me/boarding/stays?scope=upcoming\|current\|past` | customer | paged summaries: dates, cats, unit type, status, `depositState`, `balanceDue` |
| `GET` | `/api/customers/me/boarding/stays/{stayId}` | customer | detail incl. care profiles, quote, charges, deposit, incidents summary, pending approvals |
| `GET` | `/api/customers/me/boarding/stays/{stayId}/diary?from&to` | customer | per day: observations (portion, water, litter, mood, notes), media refs, completed-task counts — FR-POR-4 |
| `GET` | `/api/customers/me/boarding/stays/{stayId}/incidents/{incidentId}` · `POST …/acknowledge` | customer | detail / C19 ack → `204` |
| `GET` | `/api/customers/me/boarding/stays/{stayId}/approvals/{approvalId}` · `POST …/respond` | customer | detail / C20 → `204` |
| `GET` | `/api/customers/me/boarding/stays/{stayId}/summary` | customer | §8.16 |
| `GET` / `POST` / `DELETE` | `/api/customers/me/boarding/waitlist` (· `/{id}`) | customer | C24 |
| `POST` / `GET` | `/api/customers/me/daycare` · `POST …/{id}/cancel` | customer | C25 |

**Staff** (branch from `X-Branch-Id`)

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` / `POST` / `PUT` | `/api/staff/boarding/units` (· `/{unitId}`, `/{unitId}/activation`) | `ViewUnits` / `ManageUnits` | list with `nightlyRate` from the type's card / C1 |
| `GET` / `POST` / `DELETE` | `/api/staff/boarding/units/{unitId}/maintenance` (· `/{blockId}`) | `ViewUnits` / `ManageUnits` | C2; `400 MaintenanceOverlapsStays` lists stay dates |
| `GET` / `POST` | `/api/staff/boarding/rate-cards?unitType` · `/api/staff/boarding/daycare-rate-cards` | `ViewUnits` / `ManageUnits` | history (Current / Scheduled / Past) / C3 |
| `GET` / `PUT` | `/api/staff/boarding/care-routine` | `ViewUnits` / `ManageUnits` | C4 |
| `GET` | `/api/staff/boarding/availability?arrivalOn&departureOn&catCount&requiresIsolation&excludeStayId` | `ViewStays` | staff mode, incl. isolation and per-unit freedom |
| `GET` | `/api/staff/boarding/occupancy?from&to` | `ViewStays` | ≤ 31 days: units × days with stay blocks, maintenance, turnaround |
| `POST` | `/api/staff/boarding/enquiries` | `Book` | C5 with `customerId` |
| `PUT` | `/api/staff/boarding/stays/{id}/enquiry` · `/cats/{catId}/care-profile` | `Book` | C5 / C6 |
| `POST` | `/api/staff/boarding/stays/{id}/cats/{catId}/vaccination-override` | `OverrideVaccination` | C7 → `204` |
| `POST` | `/api/staff/boarding/stays/{id}/reserve` · `/confirm` · `/cancel` · `/no-show` | `Book` | C8 / C9 → `200 { status, depositState, outstanding }` / C11 / C13 |
| `GET` | `/api/staff/boarding/stays?status&from&to&customerId&catId` · `/{id}` | `ViewStays` | paged / detail incl. history, allocations, gate, deposit ledger, charges, referrals, approvals, isolation entries, attention |
| `POST` | `/api/staff/boarding/stays/{id}/check-in` · `/check-out` | `CheckInOut` | C12 / C22 → `200 { stay }`; `400 CheckInChecklistIncomplete` with `missing[]` per cat |
| `POST` | `/api/staff/boarding/stays/{id}/extend` | `ChangeStay` | C21 → `200`; `400 UnitNotFreeForExtension` + `relocationOptions[]` |
| `POST` | `/api/staff/boarding/stays/{id}/relocate` | `ChangeStay` | C21 → `200` |
| `POST` | `/api/staff/boarding/stays/{id}/cats/{catId}/suspected-infectious` · `DELETE` same | `Isolation` / `ClearIsolation` | C17 |
| `POST` | `/api/staff/boarding/stays/{id}/isolation-entries` · `/{entryId}/exit` | `Isolation` | C17 |
| `GET` | `/api/staff/boarding/tasks?date&unitId&assignedTo=me&status` | `ViewStays` | the day list **grouped by unit**, each task with `isOverdue`, cat header fields, `title`, `dueLocalTime` — NFR-AVL-3's input |
| `POST` | `/api/staff/boarding/tasks/{taskId}/complete` (empty body allowed) · `/skip` | `RecordCare` | C15 → `204` |
| `POST` / `GET` | `/api/staff/boarding/stays/{id}/observations` | `RecordCare` / `ViewStays` | C16 → `201` / list with `notEating` per cat |
| `POST` / `GET` | `/api/staff/boarding/stays/{id}/media` · `POST …/{mediaId}/hide` | `RecordCare` / `ViewStays` | C18 |
| `POST` / `GET` | `/api/staff/boarding/stays/{id}/incidents` · `/api/staff/boarding/incidents?status` | `RecordIncident` / `ViewStays` | C19 → `201` / list |
| `POST` | `/api/staff/boarding/incidents/{id}/acknowledgement` · `/phone-call` · `/resolve` | `FollowUpIncident` | C19 → `204` |
| `POST` | `/api/staff/boarding/stays/{id}/referrals` | `Refer` | C20 → `201 { referralId }` |
| `POST` | `/api/staff/boarding/stays/{id}/vet-approvals` · `/{approvalId}/staff-answer` | `RequestVetApproval` | C20 |
| `GET` | `/api/staff/boarding/stays/{id}/cats/{catId}/vet-headroom` | `ViewStays` | `{ limit, committed, approvedIncrease, headroom, vetVisitAuthorised }` |
| `GET` | `/api/staff/boarding/stays/{id}/summary` | `ViewStays` | §8.16 |
| `POST` | `/api/staff/boarding/stays/{id}/attention/clear` | `Book` | C23 |
| `GET` | `/api/staff/boarding/dashboard` | `ViewStays` | §12.6 |
| `GET` / `POST` | `/api/staff/daycare?date` · `/{id}/check-in` · `/check-out` · `/cancel` · `/no-show` | `ViewStays` / `Book` | C25 |

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes (R1–R8).

### 12.5 Filters

Extend `UniqueConstraintExceptionFilter` (`23P01` by name, F30): `ex_stay_allocation_unit` → `409
UnitNoLongerAvailable`; `ex_stay_cat` → `409 CatAlreadyBoarding`; `ex_unit_maintenance` → `400
MaintenanceOverlapsMaintenance`. `23505`: `ix_boarding_unit_branch_code` → `400 UnitCodeAlreadyExists`;
the rate-card indexes → `400 DuplicateRateCardEffectiveDate`; `ix_boarding_waitlist_active` → `400
AlreadyWaitlisted`; `ix_daycare_slot` → `409 DaycareFull`; `ix_care_task_generation` → swallowed by the
generator (success). Unknown constraints rethrow.

### 12.6 Read side — `src/Read/`

Queries in `Queries/Boarding/`, handlers in `QueryHandlers/Boarding/`, ViewModels flat; explicit
projections; `AsNoTracking()`.

- `GetBoardingAvailabilityQuery` (public/staff) — through `IBoardingInputs` + the finder. **Never in SQL.**
- `GetBoardingOccupancyQuery`, `GetStaysQuery`, `GetStayDetailQuery`, `GetCustomerStaysQuery`,
  `GetStayDiaryQuery`, `GetStaySummaryQuery`, `GetCareTaskDayListQuery`, `GetStayObservationsQuery`,
  `GetIncidentsQuery`, `GetVetHeadroomQuery`, `GetRateCardsQuery`, `GetUnitsQuery`,
  `GetDaycareDayQuery`, `GetCustomerWaitlistQuery`.
- **`GetBoardingDashboardQuery`** — one handler, one round trip per section, for the active branch and
  today (branch-local): `occupancy` (`{ unitsOccupied, unitsActive, catsInHouse, byType[] }`),
  `arrivals[]` (confirmed, arriving today: cats, unit, deposit state), `departures[]`, `notEating[]`
  (`NotEatingRule` over the last two observations per in-house cat), `overdueTasks[]`,
  `unassignedTasks[]`, `openIncidents[]` (severity, acknowledged?, escalated?), `callOwner[]`
  (escalated, no phone call), `unpaidBalances[]` (reserved awaiting deposit with `depositDueBy`; in
  house/checked out with balance > 0 from Billing's read port), `refundDue[]`, `attention[]`,
  `generatedOn`. — **US-BRD-19 AC1**. Side-effect free; p95 ≤ 1.5 s (NFR-PRF-3, I-F8).
- Worker queries: `GetReservationsReadyToConfirmQuery`, `GetExpiredReservationsQuery`,
  `GetIncidentsToEscalateQuery`, `GetStaysInHouseQuery`, `GetStaysDueForInvoicingQuery`,
  `GetDailyUpdateCandidatesQuery`, `GetWaitlistCandidatesQuery`, `GetMediaToPurgeQuery`.

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler and event handler in §11–§12.6;
the ports of §8.20 (Scoped); `BoardingStayInHouseGuard` into `SubjectGuardResolver`; the eleven
templates; `BoardingWorkCycle` (Scoped); **`AddHostedService<BoardingWorker>()`**; the
`boarding-booking` rate-limit policy. `ValidateScopes`/`ValidateOnBuild` stay on; I-A2 proves it.

### 12.8 Cross-area contracts assumed — **owned by handovers not yet written**

Each is the **shape Boarding needs**. The owning handover may rename; if it changes the meaning, it
appends a §1 Amended row here and the affected rows of §18 move with it (§20.2).

| # | Owner | Contract | Direction |
|---|---|---|---|
| **P1** | **HO-BIL-001** | `RecordPaymentCommand` (US-BIL-11) accepts `Reference { Kind = BoardingStayDeposit \| BoardingStay \| DaycareVisit, Id }`, validates it with Boarding's `IBoardingStayReferenceReader.ExistsForCustomer(kind, id, customerId)`, applies FR-BIL-12's second approval, and exposes per reference the accepted and pending totals — read by `BillingDepositLedger` (`boarding` reads `billing.payment` by reference; read-only). Raises `PaymentRejectedEvent` (reference) | Boarding reads Billing |
| **P2** | **HO-BIL-001** | Invoices are composed from **sources**. Boarding implements `IBoardingBillableSource.GetUninvoicedLinesAsync(stayId, upToNight?)` → lines (`SourceLineId`, `Description`, `Amount`, `TaxClassId`, `LineOfBusiness = Boarding`, `Kind`); Billing adds clinic charges carrying `BoardingStayId` to the same invoice, applies deposit payments (P1) and turns negative lines into a credit note against the invoice that billed them (BR-11). Daycare: `DaycareVisit` source on check-out | Billing reads Boarding |
| **P3** | **HO-BIL-001** | `IssueInvoiceFromSourceCommand(Source, SourceId, UpToNight?)` — idempotent for an already-invoiced range; raises `InvoiceIssuedEvent` listing `(SourceKind, SourceLineId)`. Dispatched by `BoardingWorker` and by the FE after check-out | Boarding asks Billing |
| **P4** | **HO-BIL-001** | `GetOutstandingBalanceBySourceQuery(kind, ids[])` for the dashboard and stay reads | Boarding reads Billing |
| **I1** | **HO-IMP-001** | A `BoardingStore` location per branch (FR-IMP-6); `IssueStockCommand` (internal use) with `Reference { Kind = BoardingStay, Id }` and `CatId`, FEFO by default, **expired batches refused with no override (BR-3)**, validated with `IBoardingStayReferenceReader.IsInHouse(stayId, catId)`; recall tracing (US-IMP-11) lists stays and cats through that reference. `GetStockIssuesByReferenceQuery` feeds the summary | Inventory reads Boarding |
| **E1** | **HO-EMR-001** | Subscribes to `BoardedCatReferredToClinicEvent`; opens a visit (through HO-APT-001's walk-in with a `BoardingStayId`, or directly) linked to the stay; raises `ReferralVisitOpenedEvent (ReferralId, VisitId)` | Clinic reacts to Boarding |
| **E2** | **HO-EMR-001** (and HO-SRG-001) | Before recording a charge on a visit with a `BoardingStayId`, calls `IBoardingVetSpendingPolicy.GetHeadroomAsync(stayId, catId)`; above headroom → holds the treatment pending approval | Clinic reads Boarding |
| **E3** | **HO-EMR-001 / HO-BIL-001** | Raise `BoardingVetChargeRecordedEvent (ChargeId, StayId, CatId, Amount, Description)` for each charge on such a visit | Boarding reacts |
| **E4** | **HO-EMR-001** | Subscribes to `VetChargeApprovalDecidedEvent` to release or cancel the held treatment | Clinic reacts |

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Enums/NotificationSubjectKind.cs                              ← BoardingStayInHouse only
src/Shared/CatEcommerce.Shared/Common/Constants/{StayRules,CareProfileRules,BoardingRoutineDefaults,IncidentRules,MediaRules,SignatureRules,BoardingMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs      ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                                ← the keys of §10.2 and the TimeRange start<end bound only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs                    ← the eleven definitions of §8.19 only
src/Domain/CatEcommerce.Domain/Aggregates/Boarding/**
src/Domain/CatEcommerce.Domain/Aggregates/Cat/CatAggregate.cs                                       ← EvaluateVaccinationStatus only (read-only method)
src/Domain/CatEcommerce.Domain/Entities/{StayCat,UnitAllocation,QuotedRateUnit,StayChargeLine,VetReferral,VetChargeApproval,VetChargeCommitment,StayStatusChange,RoutineTime,StayCatFeeding,StayCatMedication,StayCatBelonging}.cs
src/Domain/CatEcommerce.Domain/ValueObjects/{CareProfile,EmergencyContact,CheckInRecord,CheckOutRecord,SignatureCapture,MediaRef,RateCardSnapshot,PricingSnapshot,GateOverride}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{the pure services and ports of §8.20}.cs
src/Domain/CatEcommerce.Domain/DomainServices/VaccinationStatusRule.cs
src/Domain/CatEcommerce.Domain/DomainServices/INotificationLinkBuilder.cs                            ← seven methods added only
src/Domain/CatEcommerce.Domain/Models/**                                                            ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Boarding/**
src/Application/CatEcommerce.Application/Commands/Boarding/**
src/Application/CatEcommerce.Application/CommandHandlers/Boarding/**                                ← incl. UnitClaim
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Boarding/**
src/Application/CatEcommerce.Application/DataMappers/{BoardingUnitMapper,BoardingStayMapper,CareTaskMapper,IncidentMapper,DaycareMapper}.cs
src/Read/CatEcommerce.Read/Queries/Boarding/**
src/Read/CatEcommerce.Read/QueryHandlers/Boarding/**
src/Read/CatEcommerce.Read/QueryHandlers/Cat/GetCatVaccinationStatusQueryHandler.cs                 ← call VaccinationStatusRule only; output unchanged
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels of §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs                ← DbSets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/Boarding/**
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                            ← the six migrations of §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Boarding/**
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Guards/BoardingStayInHouseGuard.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs             ← §8.19 methods only
src/Api/CatEcommerce.Api/Controllers/{BoardingPublic,CustomerBoarding,CustomerDaycare,StaffBoardingUnits,StaffBoardingStays,StaffBoardingCare,StaffBoardingIncidents,StaffBoardingDashboard,StaffDaycare}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Boarding/**
src/Api/CatEcommerce.Api/Background/{BoardingWorker,BoardingWorkCycle}.cs
src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs                                 ← the §12.5 mappings only
src/Api/CatEcommerce.Api/appsettings.json                                                           ← Boarding:Worker section only
src/Api/CatEcommerce.Api/Program.cs                                                                 ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-BRD-001.md
docs/gates/HO-BRD-001-*.md
.ai/decisions/adr.md                                                                                ← ADR-217…236, append only
```

The two HO-CAT-001 edits (the read-only aggregate method and the query handler calling the extracted
rule) are the only changes to another area's code; HO-CAT-001's own tests must stay green unchanged.

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-2 is a separate task) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs. A conflict is a §20 stop |
| `web/**` | HO-BRD-002 |
| Every file of HO-ACC-001, HO-ADM-001, HO-CAT-001, HO-NTF-001, HO-APT-001 beyond the lines in §13 | Their areas; **no Administration, Appointment or Notification aggregate is edited** |
| Any Billing, Payment, Invoice, Refund type or table | HO-BIL-001 (ADR-223, ADR-230). Boarding reads through the §12.8 port only |
| Any gateway SDK, payment link, card, token or online-refund code | SRS A-2, C-5, Phase 5 |
| Any Inventory, Batch, Stock ledger type or table | HO-IMP-001 (ADR-232) |
| Visit, consultation, treatment types | HO-EMR-001 |
| An upload endpoint, object-storage client, local file write | HO-CAT-001 §4.2, OQ-1 |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |

---

## 15. Unit test cases

| ID | Case | Expected |
|---|---|---|
| **A — foundation, status machine, settings** | | |
| U-A1 | `StayTransitionRules` over every `(from, action)` | Only §8.5's arrows; others `InvalidStayTransition` naming both states |
| U-A2 | Every transition | One `StatusHistory` row; `is_active` on allocations and stay-cats per §8.4 |
| U-A3 | `Lapse` before `DepositDueBy`; after with `PendingApproval`; after with `Awaiting` | `ReservationNotLapsable` ×2; `Cancelled/ReservationLapsed` |
| U-A4 | `Cancel` 73 h and 71 h before `OccupiedFrom` (window 72) | Not forfeited; forfeited |
| U-A5 | `RecordNoShow` before and after grace | `NoShowTooEarly`; recorded, forfeited |
| U-A6 | `CheckIn` the day before arrival; on arrival; day after | `CheckInNotOnArrivalDate`; allowed; allowed |
| U-A7 | `Confirm` with accepted < required; = required | `DepositNotYetReceived`; confirmed |
| U-A8 | Every template definition of §8.19 | Pass HO-NTF-001 `TemplateRules`; urgencies as stated |
| U-A9 | Registry: `boarding.checkInWindow` 19:00–14:00 | Refused (start < end) |
| **B — units, rate cards, pricing** | | |
| U-B1 | Rate card back-dated; duplicate effective date | `RateCardCannotBeBackdated`; duplicate refused |
| U-B2 | Card at *t* with two cards | The latest `EffectiveFrom ≤ t` |
| U-B3 | Quote 5 nights, threshold 14 | Nightly basis, 5 night units, no discount |
| U-B4 | Quote 16 nights, weekly 6 000, nightly 1 000, discount 10 % | Weekly basis: 2 weeks + 2 nights; discount per unit; discount lines separate — **US-BRD-03 AC1, AC2** |
| U-B5 | Quote 65 nights | Monthly: 2 months, 0 weeks, 5 nights, in order |
| U-B6 | Quote 13 vs 14 nights (threshold 14) | Nightly vs weekly — the threshold crossing |
| U-B7 | 3 cats, additional-cat rate 200 | `AdditionalCat` units = 2 × 200 × nights |
| U-B8 | 60-night quote split at night 30 | Σ of the two period totals equals the quote exactly |
| U-B9 | Deposit fixed 2 000 on a 1 500 total; percent 30 on 10 000; zero | 1 500; 3 000; `NotRequired` |
| **C — availability, gate, profile, capacity, isolation rule** | | |
| U-C1 | Occupancy window defaults on a Dhaka date | 14:00 local arrival → 11:00 + 3 h departure, as instants |
| U-C2 | Departure 11:00 + 3 h and arrival 14:00 same day; turnaround 24 | Free; not free — **US-BRD-02 AC2** |
| U-C3 | Unit with a maintenance block overlapping | Not free — **US-BRD-01 AC2** |
| U-C4 | Two free deluxe units | The first by block, order, code, every time |
| U-C5 | 3 cats; types with capacity 2 and 4 | Only the 4-capacity type offered |
| U-C6 | Online request; isolation units free | Never offered; staff with `RequiresIsolation` → only isolation |
| U-C7 | Everything full; free 3 days later | Alternatives include that range; `waitlistAvailable` — **US-BRD-02 AC3** |
| U-C8 | Type without a card | Omitted |
| U-C9 | `VaccinationStatusRule` over HO-CAT-001's U-cases | Identical results to Q4's documented rule |
| U-C10 | Core vaccine due 5 days after arrival, stay 10 nights | Blocked `DueBeforeDeparture`; due after departure → passed |
| U-C11 | Never vaccinated | Blocked `NeverAdministered` — **US-BRD-04 AC1** |
| U-C12 | Blocked cat with an override | Passes as `Overridden`; override survives re-evaluation — **AC3** |
| U-C13 | Reserve with an incomplete profile | `CareProfileIncomplete` naming the cat |
| U-C14 | Care profile with 7 feedings; duplicate time; medication with no time | Each refused |
| U-C15 | 4 cats into capacity 3 | `UnitCapacityExceeded` with *"holds up to 3 cats"* — **US-BRD-07 AC2** |
| U-C16 | Stay with a flagged cat allocated to a standard unit | `IsolationUnitRequired` — **US-BRD-18 AC1** |
| **D — deposit policy and confirmation** | | |
| U-D1 | Confirm handler with ledger accepted ≥ required | Confirmed; `boarding.confirmed` enqueued before the save |
| U-D2 | Ledger pending approval | `PendingApproval`; no message; lapse later refused |
| U-D3 | Lapse handler | Allocations inactive; `boarding.reservation-lapsed` enqueued |
| **E — care** | | |
| U-E1 | `CareTaskGenerator` for a cat with 2 feedings, 1 medication × 2 times, routine 2/1/2 | 9 tasks with the right due instants and titles — **US-BRD-09 AC1** |
| U-E2 | Generator run twice | Same keys; second run adds nothing |
| U-E3 | Medication `FromOn`/`ToOn` narrower than the stay | Tasks only inside it — **US-BRD-05 AC2** |
| U-E4 | Extended stay, new dates | Tasks exist for the added dates on generation — **US-BRD-09 AC2** |
| U-E5 | `CareTaskAssigner` with two caretakers on shift and four units | Units split round-robin; one unit's tasks to one caretaker per shift; none on shift → unassigned |
| U-E6 | Complete a pending task; complete it again | `Done` with time and caller; second is a no-op — **US-BRD-10 AC1** |
| U-E7 | Overdue rule at due + grace − 1 min / + 1 min | Not overdue; overdue — **AC2** |
| U-E8 | `NotEatingRule`: None, None; None, Half, None; single None | Flagged; not flagged; not flagged — **US-BRD-11 AC2** |
| U-E9 | Check-in checklist missing weight and signature | `CheckInChecklistIncomplete` listing both — **US-BRD-08 AC2** |
| U-E10 | Belongings present, no photos, setting true / false | Refused / allowed |
| U-E11 | Signature: strokes 40 KB; non-numeric; typed without confirmation | `InvalidSignature` each |
| U-E12 | Flag infectious in a standard unit without a target; with an isolation target | Refused; flagged and relocated in one mutation |
| U-E13 | Isolation entry for a stay not in isolation | `NotInIsolation` |
| **F — incidents, media, dashboard rules** | | |
| U-F1 | Incident low / medium / high | No message / `incident-medium` / `incident-high` enqueued |
| U-F2 | Escalation rule at 29 / 31 min unacknowledged; acknowledged at 10 min | Not / escalated; never |
| U-F3 | Media `RetainUntil`; incident-attached item past retention | 12 months later; not purged — **US-BRD-12 AC2** |
| U-F4 | Media type `text/html`; video 60 MB | Refused |
| **G — referral, charges, extension, early check-out** | | |
| U-G1 | Headroom: limit 5 000, committed 3 000, approved +2 000; `VetVisitAuthorised = false` | 4 000; limit 0 + approvals |
| U-G2 | Commitment handler twice with one `ChargeId` | One commitment |
| U-G3 | Approval answered twice | `VetApprovalAlreadyDecided` |
| U-G4 | Extension re-quote crossing the threshold | Basis changes; uninvoiced lines replaced |
| U-G5 | Extension after cycle 1 invoiced | Invoiced lines unchanged; new lines only |
| U-G6 | Early check-out after 10 of 30 booked nights, nothing invoiced | Nightly basis, discount gone, lines replaced — **US-BRD-16 AC2** |
| U-G7 | Early check-out after 45 of 60 nights, month 1 invoiced | Month 1 untouched; remaining lines for nights 31–45 |
| U-G8 | Early check-out where invoiced exceeds used | One negative `EarlyCheckoutCredit` line — **AC1** |
| U-G9 | Relocate without a reason | `RelocationReasonRequired` |
| **H — waitlist, daycare** | | |
| U-H1 | Join when units free; twice | `UnitsStillAvailable`; `AlreadyWaitlisted` |
| U-H2 | Daycare 5 h hourly 300, day 1 200; 3 h | 1 200 (capped); 900 |
| U-H3 | Daycare capacity 0 | `DaycareNotOffered` |
| **X — cross-cutting** | | |
| U-X1 | Reflection over `Events/Boarding/**` | No free-text string properties |
| U-X2 | `BoardingWorker` with a throwing pass | Logged; next pass runs |
| U-X3 | Migration predicates | Built from `Enumeration.Value`; `is_active` maintained per §8.4 in every transition |
| U-X4 | Reflection: no Boarding command handler injects a repository of a Billing, Inventory, Clinic, Appointment or Administration aggregate | None — ADR-232 |
| U-X5 | Reflection: no Boarding domain-event handler injects `INotificationOutbox` | None — ADR-057 |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1). Workers off; tests call
`BoardingWorkCycle` and `NotificationWorkCycle`, advancing `FakeTimeProvider`. Billing, Inventory and
Clinic are the **real** merged areas (§20.2); a phase whose dependency is absent is not run.

### 16.1 Additional shared-database rules

1. **Each test creates its own branch** (HO-ADM-001 endpoints) with its own units, rate cards,
   routine and rostered caretakers; assertions are scoped to it.
2. Customers, cats and vaccinations are minted per test (HO-CAT-001 endpoints).
3. Exclusion constraints are real; a test never disables them.

| ID | Case | Expected |
|---|---|---|
| I-A1 | Migrations on an empty database | `boarding` schema; three exclusion constraints (`contype = 'x'`) |
| I-A2 | Host starts with scope validation | Every registration resolves |
| I-B1 | Admin creates units of each type, a maintenance block, rate cards | `201`s; unit reads show the type's `nightlyRate` — **US-BRD-01 AC1** |
| I-B2 | Availability across the maintenance block | The unit excluded for those dates only — **US-BRD-01 AC2** |
| I-B3 | Maintenance over an existing reservation | `400 MaintenanceOverlapsStays` with the dates |
| I-B4 | Nurse `POST` a unit | `403` |
| I-C1 | Availability for 2 cats, 5 nights | Types with capacity ≥ 2, prices and totals — **US-BRD-02 AC1** |
| I-C2 | Reserve, then availability over the range; back-to-back range; turnaround 24 h | Unit gone; back-to-back offered; with 24 h not offered — **AC2** |
| I-C3 | All units taken | `alternatives[]` and `waitlistAvailable` — **AC3** |
| I-C4 | Quote 16 nights | Weekly basis, discount lines separate — **US-BRD-03** |
| I-C5 | Enquiry with an overdue cat; reserve | Gate blocked with `bookingLink`; `400 VaccinationGateBlocked` — **US-BRD-04 AC1** |
| I-C6 | Record the vaccination (HO-CAT-001), reserve the same stay | `200 Reserved`; dates, cats, profiles unchanged — **AC2** |
| I-C7 | Administrator override; reception attempts one | `204` and a `BusinessEvent` audit row with the reason; `403` — **AC3** |
| I-C8 | Care profile for two cats; reserve with one missing | `400 CareProfileIncomplete` naming the cat — **US-BRD-05 AC1** |
| I-C9 | Three cats of one household into a family unit | One stay, three profiles — **US-BRD-07 AC1** |
| I-C10 | Four cats into capacity 3 | `400 UnitCapacityExceeded` with the capacity — **AC2** |
| I-C11 | Another customer's cat in an enquiry; another's stay by id | `404`; `404` |
| I-C12 | **20 parallel reserves for the last unit** | Exactly one `200`, the rest `409 UnitNoLongerAvailable` |
| I-C13 | Same cat in two overlapping stays (two branches) | Second `409 CatAlreadyBoarding` — BR-13 |
| I-D1 | Reserve | `Reserved`, deposit amount, `payBy`, balance due; `boarding.reserved` in the in-app inbox after the delivery pass — **US-BRD-06 AC1** |
| I-D2 | Advance past the hold; run the pass | `Cancelled/ReservationLapsed`; unit available; lapse message — **AC2, AC4** |
| I-D3 | Record the deposit in Billing (P1), `POST confirm` | `Confirmed`; `boarding.confirmed` — **AC3** |
| I-D4 | Deposit above the approval threshold; advance past the hold; approve in Billing; run the pass | Not lapsed while pending; confirmed by the pass after approval |
| I-D5 | Record the deposit from Billing's screen only; run the pass | Confirmed by the worker |
| I-D6 | Customer cancels 5 days out / 1 day out | Deposit refund line / forfeited; message |
| I-D7 | Log capture: no payment details, card or token fields exist in any Boarding table (`information_schema`) | None — SRS C-5 |
| I-E1 | Check-in with missing items; complete | `400` listing items, status `Confirmed`; `200 CheckedIn`; weight in HO-CAT-001's history — **US-BRD-08** |
| I-E2 | Run the pass after check-in | Today's and tomorrow's tasks grouped by unit, due times from profile and routine — **US-BRD-09 AC1** |
| I-E3 | Run the pass twice | No duplicate task |
| I-E4 | Extend by 3 nights; pass on those days | Tasks for the added days — **AC2** |
| I-E5 | Approve a caretaker's leave (HO-ADM-001) | Their pending tasks in range reassigned |
| I-E6 | `POST complete` with an empty body | `204`; `completedBy` = caller, `completedOn` set — **US-BRD-10 AC1** |
| I-E7 | Advance past due + grace | Task `isOverdue` on the day list and in `dashboard.overdueTasks` — **AC2** |
| I-E8 | Two `None` observations | `notEating` on the stay and dashboard — **US-BRD-11** |
| I-E9 | Flag infectious with an isolation target; record entry and exit | Relocated to isolation; entry listed with the staff member — **US-BRD-18** |
| I-E10 | Relocate a flagged stay to a standard unit | `400 IsolationUnitRequired` |
| I-E11 | Issue house food (HO-IMP-001, I1) for the stay; an expired batch | Ledger entry with the stay reference; expired refused; the summary lists the issue |
| I-F1 | Add media; customer reads the diary | Item under its date — **US-BRD-12 AC1** |
| I-F2 | Advance 12 months + 1 day; pass | Item `Purged` unless incident-attached; diary readable before — **AC2** |
| I-F3 | Record a high incident at 22:30 Dhaka; run the notification pass | In-app copy delivered within one pass (≪ 15 min); `Urgent`, not held — **US-BRD-13 AC2, FR-NTF-7** |
| I-F4 | Medium incident at 22:30 | Enqueued; external copy held by quiet hours; in-app delivered |
| I-F5 | High, unacknowledged 31 min; pass | `escalatedOn` set; listed in `callOwner` — **AC3** |
| I-F6 | Owner acknowledges before the escalation time | Never escalated |
| I-F7 | Dashboard | Every section of §12.6 populated from the fixtures — **US-BRD-19 AC1** |
| I-F8 | Dashboard with 60 units, 50 in-house stays, 500 tasks | p95 ≤ 1.5 s over 50 calls |
| I-G1 | Refer a boarded cat | Referral stored; HO-EMR-001 opens a visit; `visitId` linked — **US-BRD-14 AC1** |
| I-G2 | Clinic charge within headroom | Commitment recorded; charge on the stay's invoice (P2) — **AC2** |
| I-G3 | Charge above headroom | HO-EMR-001 holds it; approval requested; owner approves in the portal; decided event releases it — **AC3** |
| I-G4 | Extend with the unit free / taken | `200` with new quote and lines / `400 UnitNotFreeForExtension` with options — **US-BRD-15** |
| I-G5 | Relocate with a reason | New allocation with the reason; old truncated |
| I-G6 | Early check-out | Recalculated lines and basis; credit line when invoiced exceeds — **US-BRD-16** |
| I-G7 | Check-out with a missing belonging note; complete | `400`; `200`; summary with logs, weights, incidents, meds, vet visits, belongings, signature — **US-BRD-17 AC1** |
| I-G8 | Issue the final invoice (P3) | Lines for deposit applied, nights, extras, vet charges — **AC2** |
| I-G9 | **AC-15**: 60-night stay; advance; passes | Two cycle invoices at the monthly rate with the discount; final invoice for remaining nights only — **US-BRD-20** |
| I-G10 | Customer merge | Stays under the survivor |
| I-G11 | Cat marked deceased with a confirmed stay | `needsAttention: CatDeceased`; nothing cancelled or sent |
| I-H1 | Full range → join waitlist → another stay lapses → pass (outside quiet hours) | Oldest entry `Notified`; message |
| I-H2 | Daycare book, check in, check out | Charge computed; no unit allocation row |
| I-H3 | Daycare capacity 2, three concurrent bookings | Two `201`, one `409 DaycareFull` |
| I-X1 | Log capture across all cases at `Information`+ | No care notes, incident descriptions, health observations or customer names |

---

## 17. Implementation order and verification

| Phase | Stories | Delivers | Needs |
|---|---|---|---|
| **A** | — | Enumerations, settings, constants, actions, templates, `StayTransitionRules`, `BoardingQuoteCalculator`, `BoardingOccupancyWindow`, `VaccinationStatusRule` extraction and the `CatAggregate` method | Written areas |
| **B** | BRD-01, 03 | Units, maintenance, rate cards, care routine; `AddBoardingUnits` | same |
| **C** | BRD-02, 04, 05, 07, 18 (rule) | Finder, enquiry, gate, override, care profiles, stay tables and **exclusion constraints**; `AddBoardingStays` | same |
| **D** | BRD-06 | Reserve, confirm, lapse, cancel, no-show, `BoardingWorker` (first passes), messages | **HO-BIL-001 P1** |
| **E** | BRD-08 … 11, 18 | Check-in, tasks, assignment, completion, observations, isolation; `AddBoardingCare`. House-food issue verified once HO-IMP-001 is merged | HO-IMP-001 I1 for I-E11 only |
| **F** | BRD-12, 13, 19 | Media, incidents, escalation, dashboard; `AddBoardingIncidentsAndMedia` | P4 for balances |
| **G** | BRD-14 … 17, 20 | Referral, approvals, charges, extension, relocation, check-out, summary, invoicing; `AddBoardingCharges` | **HO-EMR-001, HO-BIL-001 P2/P3** |
| **H** | BRD-02 AC3, 21 | Waitlist, daycare; `AddBoardingWaitlistAndDaycare` | P2 for daycare |

**Read each migration before applying it** — the constraint SQL is hand-written.

```bash
dotnet build CatEcommerce.sln
dotnet dotnet-ef migrations add <Name> -p src/Infrastructure/CatEcommerce.Infrastructure -s src/Api/CatEcommerce.Api
dotnet dotnet-ef database update -p src/Infrastructure/CatEcommerce.Infrastructure -s src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet test tests/CatEcommerce.IntegrationTests
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | BRD-01 AC1 | Units carry type, capacity, block; the nightly rate is the type's rate card (ADR-221, OQ-4 — *interpretation*) | U-B1, U-B2, I-B1 |
| A2 | BRD-01 AC2 | A unit under maintenance is excluded for that period | U-C3, I-B2, I-B3 |
| A3 | BRD-02 AC1 | Available types with prices and total for dates and cat count | U-C5, I-C1 |
| A4 | BRD-02 AC2 | Bookings, maintenance and turnaround excluded | U-C2, U-C3, I-C2, I-C12 |
| A5 | BRD-02 AC3 | Nearest alternative dates or a waitlist when full | U-C7, I-C3, U-H1, I-H1 |
| A6 | BRD-03 AC1 | Crossing the threshold applies the weekly/monthly card with the discount | U-B3 … U-B6, I-C4 |
| A7 | BRD-03 AC2 | Basis and discount itemised separately | U-B4, I-C4 |
| A8 | BRD-04 AC1 | An overdue cat blocks confirmation; a vaccination booking link is returned | U-C10, U-C11, I-C5 |
| A9 | BRD-04 AC2 | After vaccination the same booking proceeds with nothing re-entered | I-C6 |
| A10 | BRD-04 AC3 | Override: administrator only, reason mandatory, audited | U-C12, I-C7 |
| A11 | BRD-05 AC1 | Care profile with every FR-BRD-6 element, required before reserving | U-C13, U-C14, I-C8 |
| A12 | BRD-05 AC2 | Each medication generates scheduled tasks | U-E1, U-E3, I-E2 |
| A13 | BRD-06 AC1 | Deposit computed, instructions issued, balance recorded | U-B9, I-D1 |
| A14 | BRD-06 AC2 | Unpaid → `reserved`, unit held only for the window | U-A3, I-D2, I-D4 |
| A15 | BRD-06 AC3 | Deposit recorded by staff → `confirmed`, owner notified — *in-app only until a provider exists (HO-NTF-001 OQ-1)* | U-A7, U-D1, I-D3, I-D5 |
| A16 | BRD-06 AC4 | Window expires → lapses, unit released, owner notified | U-D3, I-D2 |
| A17 | BRD-06 / A-2 | No online payment; no card or token stored | I-D7, §14 |
| A18 | BRD-07 AC1 | Multiple cats, one reservation, per-cat profiles and logs | I-C9, U-E1 |
| A19 | BRD-07 AC2 | Over capacity refused with the capacity stated | U-C15, I-C10 |
| A20 | BRD-08 AC1 | Checklist covers identity, vaccination proof, weight, health, belongings with photos, signature | U-E9, U-E10, U-E11, I-E1 |
| A21 | BRD-08 AC2 | Incomplete checklist → not `checked_in` | U-E9, I-E1 |
| A22 | BRD-09 AC1 | Daily tasks for feeds, medication, litter, enrichment, observation with due times, per unit, assigned by roster | U-E1, U-E5, I-E2, I-E3, I-E5 |
| A23 | BRD-09 AC2 | Extension generates tasks for the added days | U-E4, I-E4 |
| A24 | BRD-10 AC1 | Completion in one request records time and identity (two-tap UI: HO-BRD-002) | U-E6, I-E6 |
| A25 | BRD-10 AC2 | Overdue highlighted on the dashboard | U-E7, I-E7 |
| A26 | BRD-11 AC1 | Appetite, water, litter, mood, notes per cat | I-E8 |
| A27 | BRD-11 AC2 | Two consecutive no-food entries → not eating | U-E8, I-E8 |
| A28 | BRD-12 AC1 | Media appear in the diary with their date — *references only until object storage (OQ-1)* | I-F1 |
| A29 | BRD-12 AC2 | Diary viewable for 12 months | U-F3, I-F2 |
| A30 | BRD-13 AC1 | Incident with severity, description, immediate action | U-F1, I-F3 |
| A31 | BRD-13 AC2 | Medium/high notify the owner; high within 15 minutes — *proved for the in-app copy* | U-F1, I-F3, I-F4 |
| A32 | BRD-13 AC3 | Unacknowledged high → phone-call task for staff | U-F2, I-F5, I-F6 |
| A33 | BRD-14 AC1 | Referral creates a consultation linked to the stay — *with HO-EMR-001 (E1)* | I-G1 |
| A34 | BRD-14 AC2 | Charges within the limit added to the stay invoice | U-G1, U-G2, I-G2 |
| A35 | BRD-14 AC3 | Above the limit: owner approval requested, treatment held — *hold by HO-EMR-001 (E2, E4)* | U-G3, I-G3 |
| A36 | BRD-15 AC1 | Extension when free updates stay, tasks and charges | U-G4, U-G5, I-G4, I-E4 |
| A37 | BRD-15 AC2 | Not free → relocation offered, reason recorded | U-G9, I-G4, I-G5 |
| A38 | BRD-16 AC1 | Early check-out recalculated per policy; difference refunded or credited (by HO-BIL-001) | U-G6 … U-G8, I-G6 |
| A39 | BRD-16 AC2 | Lost long-stay discount recalculated and shown | U-G6, I-G6 |
| A40 | BRD-17 AC1 | Summary with logs, weight change, incidents, meds, vet visits, belongings, signature | I-G7 |
| A41 | BRD-17 AC2 | Final invoice itemises deposit, nights, extras, vet charges — *rendered by HO-BIL-001 (P2)* | I-G8 |
| A42 | BRD-18 AC1 | Suspected-infectious cats only in isolation units | U-C16, U-E12, I-E9, I-E10 |
| A43 | BRD-18 AC2 | Staff isolation entries recorded | U-E13, I-E9 |
| A44 | BRD-19 AC1 | Dashboard shows occupancy, arrivals, departures, not eating, overdue, incidents, unpaid balances | I-F7, I-F8 |
| A45 | BRD-19 AC2 | 30 s refresh without losing input — *HO-BRD-002*; the read is side-effect free | I-F7 |
| A46 | BRD-20 AC1 | Stays over one cycle invoiced per cycle | U-B8, I-G9 |
| A47 | BRD-20 AC2 | Final invoice for remaining nights only | I-G9 |
| A48 | BRD-21 | Daycare with hourly/day rates, no overnight unit | U-H2, U-H3, I-H2, I-H3 |
| A49 | BR-13 / D-7 | A cat cannot be in two overlapping stays | I-C13 |
| A50 | ADM-03 AC1 *(boarding half)* | Task assignment follows rosters and leave | U-E5, I-E5 |

**Deferred, named in §4.2**: recording payments, invoices, credit notes and refunds (HO-BIL-001);
opening the visit and holding treatment (HO-EMR-001); issuing stock (HO-IMP-001); portal dashboard
(HO-POR-001); offline task list (HO-XCT-001); reports (HO-RPT-001).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Boarding` context and separate roots (ADR-217, OQ-2); **no cross-context write from any Boarding handler** (ADR-232, U-X4); no rule-1 exception; pure domain services; **no enqueue from any event handler** (U-X5); the two HO-CAT-001 edits confined to §13; §12.8 contracts recorded as assumptions, not built |
| 2 | SECURITY | `security-reviewer` | **Exclusion constraints are the occupancy control** (I-C12, I-C13); `404` for another customer's stay, incident, approval, media; administrator-only vaccination override; **no payment capture, card or token** (I-D7); signature payload bounds; media reference validation; no free text in events, messages or logs (U-X1, I-X1) |
| 3 | CODE_REVIEW | `code-reviewer` | `UnitClaim` used by every unit-taking handler; time only through HO-ADM-001's services; money never floating point; idempotent generator, handlers and worker commands; the worker never throws |
| 4 | TEST | `test-engineer` | `TC-HO-BRD-001.md` from §15/§16 first; §16.1's per-test branch; I-C12 and I-H3 with real parallelism; phases whose upstream is absent recorded as not run, never faked |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A1's interpretation, A15/A31's provider caveat, A28's storage caveat and A33/A35/A41's upstream dependencies recorded, not passed silently |

---

## 20. BLOCKED conditions — stop and report

**20.1 HO-ACC-001, HO-ADM-001, HO-NTF-001 or HO-APT-001 is not merged.** This slice consumes their
sessions, audit, availability, settings, outbox, guards and links. Do not stub them.

**20.2 HO-BIL-001, HO-IMP-001 or HO-EMR-001 is not merged, or its contract differs from §12.8.** Build
the phases that do not need it (§17) and stop at the one that does. **Never** create a payment,
invoice, stock or visit table or type in Boarding to fill the gap, and never shim a different
contract — report the difference so the owning handover or this one gains an Amended row.

**20.3 Someone asks for online deposit capture, a payment link or a saved card.** Forbidden (SRS A-2,
C-5, Phase 5). Report it.

**20.4 `CREATE EXTENSION btree_gist` fails** in a target environment. Stop — do not replace the
constraints with an application lock (HO-APT-001 §20.2).

**20.5 An occupancy test passes only with retries or delays.** The constraint or `is_active`
maintenance is wrong; fix that, never the test.

**20.6 A media or signature feature seems to need an upload endpoint or storage client.** Stop — OQ-1,
HO-CAT-001 §4.2.

**20.7 Do not enqueue from an event handler** (HO-NTF-001 ADR-057), **do not cancel stays
automatically** from cat or leave events, and **do not store** overdue, not-eating or availability.

**20.8 A requirement conflict is a stop, not an edit** — SRS and backlog are forbidden scope. The
gate-as-of-departure reading (OQ-8) and the per-type rate (OQ-4) are defaults, not edits.
