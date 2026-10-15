# HO-APT-001 — Clinic Appointments (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-APT-001** |
| Title | Clinic — Appointments — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §3 — **US-CLN-01 … US-CLN-10** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-CLN-1 … FR-CLN-11**, FR-NTF-3 (appointment messages), FR-ADM-3, NFR-USA-1, NFR-USA-2, UI-5, C-7, AC-1, Appendix C (appointment states), Appendix D ("Appointments") |
| Pairs with | [`HO-APT-002`](HO-APT-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ADM-001`](HO-ADM-001.md) (branches, rooms, rosters, leave, services, prices, settings, time model), [`HO-NTF-001`](HO-NTF-001.md) (outbox, templates, guards), [`HO-ACC-001`](HO-ACC-001.md) (customers, staff, sessions, merge contract), [`HO-CAT-001`](HO-CAT-001.md) (cats) |
| Delivery order | Section 5 of 17 in [`README.md`](README.md#delivery-order) |
| Baseline commit | `4007225` |
| Amended | **2026-09-23 — appended by later handovers (owed rows, recorded now that all 18 pairs exist).** Everything below is an **append** inside the lines each handover lists in its §13; no existing behaviour changes. These are recorded here so this document is not read as the whole picture. **Appends:** `BookingChannel.Surgery` (HO-SRG-001); theatre-room services become unbookable through ordinary appointments and surgery appointments refuse every ordinary transition via `AppointmentManagedBySurgery` (HO-SRG-001 ADR-172, applied inside that slice's scope). HO-EMR-001 ADR-133 edits the complete/cancel handlers so an **open visit** blocks completion. Charge capture for appointments moves to HO-BIL-001's source-event path. **2026-09-28 — implementation-time deviations from this handover's own §13, recorded by the agent that built it (not a later handover), so the architecture gate can see every file touched outside the literal §13 list:** (1) The HO-NTF-001 amendment in practice needed more than `NotificationRequestAggregate.cs`: `NotificationAggregate.cs` also gained a `SubjectStamp` column (mirrored from the request) so `AppointmentReminderGuard` can be re-checked immediately before each delivery attempt, not only at expansion (§8.7's own requirement); `INotificationSubjectGuard`/`ISubjectGuardResolver` gained an additive, default-valued `subjectStamp` parameter; `SubjectGuardResolver.cs`, `CatVaccinationDueGuard.cs` and `CatDewormingDueGuard.cs` were updated to the new signature (behaviour unchanged, the parameter unused); `ExpandNotificationRequestCommandHandler.cs` and `DeliverNotificationCommandHandler.cs` (both HO-NTF-001's own files) now pass the stamp through to the guard call and into `NotificationCreateDto`. All additive and backward-compatible; without this the guard could not fulfil ADR-074/§8.9 as specified. (2) Two Domain ports beyond §8.12's table, needed because `IAggregateRootRepository<T>` has no "find every matching row" method: `IStaleHoldReleaser` (ADR-070's lazy stale-hold expiry) and `IFlaggableAppointmentFinder`/`IWaitlistEntryFinder` (§8.10 flagging, §11.6 customer-merge reassignment), with EF implementations in `Infrastructure/Scheduling/`. (3) `ServiceSnapshot` (`src/Domain/CatEcommerce.Domain/ValueObjects/ServiceSnapshot.cs`, already in §13) gained a private parameterless constructor for EF: a record's one positional constructor cannot bind a nested owned navigation (`Money` inside `ServiceSnapshot` inside `AppointmentAggregate`) -- `dotnet ef migrations add` failed outright without it. (4) HO-NTF-001's own `CareReminderEndpointTests.cs` asserted the pre-amendment `CatCareLink` URL; updated to the new `/book?catId=...&category=...` shape this handover's §8.9 specifies. (5) Three Read-side/Infrastructure files (`AppointmentSchedulingInputs.cs`, `StaffAppointmentQueryHandlers.cs`, and `AppointmentSlotFinder.cs`'s grid generation) now call `.ToUniversalTime()` on every branch-local instant before it reaches EF: Npgsql 9.x rejects a non-UTC-offset `DateTimeOffset` as a query or insert parameter outright ("only offset 0 (UTC) is supported"), which is what these values become when `BranchCalendar.ToInstant` computes them for a branch not on UTC -- undiscovered before this handover because no earlier area persisted a `BranchCalendar`-derived instant. (6) `booking.slotIntervalMinutes` "divides 60" (§10.2's own closing sentence) is **not implemented**: it needs a bound-check line inside `BusinessSettingsAggregate.TryValidateValue`, which belongs to HO-ADM-001 and which §14 forbids editing ("no Administration aggregate is edited"). Left undone rather than guessed at; owner/integrator to route to HO-ADM-001 as a small follow-up. **2026-09-28 (later same day) — ARCHITECTURE gate HIGH-finding fixes, one file recorded outside §13:** (7) `src/Shared/CatEcommerce.Shared/Common/Constants/ErasureExemptions.cs` and `ErasureExemptions.Appointments.cs` are new -- HO-XCT-001's own file (its §13 names it), not yet built anywhere in this codebase at the time this gate-fix landed. Created early, same precedent as (2)/(3)'s HO-XCT-001-owned ports and RecordClassification's per-area partial pattern, so `AppointmentsPersonalDataEraser` (removed this same pass -- it contradicted HO-XCT-001 §8.8/OQ-7's own exemption design, see the ARCHITECTURE gate) has a registered exemption instead of no record at all. `UniqueConstraintExceptionFilter.cs` (already in §13, "23P01 mappings only") also now maps `40P01` (deadlock, which Postgres can raise for a losing transaction under concurrent exclusion-constraint inserts instead of a clean `23P01`) to the same conflict shape -- the mapping is generic (no constraint/table name is available on a deadlock's diagnostic fields), not Appointments-specific, but it lives in this file because that is where the equivalent `23P01` handling already is. **2026-09-28 (ARCHITECTURE gate second pass, two new HIGH findings, owner decisions):** (8) HIGH A -- `SlotClaim.ExpireOverlappingStaleHoldsAsync` (added in the first-pass fix round) commits each stale hold's expiry in its own save, not "in the same save" as ADR-070 originally said, because EF Core does not order two `Modified` rows of one table by which was mutated first in C# and Postgres checks an exclusion constraint per statement. Owner decision: **keep the separate save**, recorded as a deliberate ADR-070 deviation (see the ADR's own 2026-09-28 amendment) rather than restored to one save. `IStaleHoldReleaser` gained `HasPendingChanges()`; the helper asserts it is false on entry and throws `InvalidOperationException` otherwise, so a future caller that mutates before calling the helper fails loudly instead of silently committing half its unit of work. The two comments that had gone stale (`AppointmentBookingCommandHandlers.cs`, `StaleHoldReleaser.cs`) are corrected. (9) HIGH B -- the `40P01` mapping (7) added to `UniqueConstraintExceptionFilter.cs` was global, mapping every Postgres deadlock in every area to the same generic `409`. Owner decision: **scope it to slot claims.** A new marker, `Filters/SlotClaimEndpointAttribute.cs`, is applied to exactly the four HTTP slot-claiming actions (§11.4): `CustomerAppointmentsController.PlaceHold` (C1), `CustomerAppointmentsController.Reschedule` (C4), `StaffAppointmentsController.Book` (C6), `StaffAppointmentsController.Move` (C7). C15 (the waitlist offer) is dispatched only by `ClinicWorker`, never by a controller (ADR-075), so there is no HTTP endpoint to mark. On a marked action a `40P01` now maps to the exact `409 SlotNoLongerAvailable` shape a `23P01` on the appointment exclusion constraints produces (ADR-068, §12.4); everywhere else -- every other area, and every unmarked Appointments action -- a deadlock is left unhandled and becomes the logged `500` it always was. `UniqueConstraintExceptionFilter.cs` remains the §13-listed file; this narrows what it does, it does not widen its scope. **2026-09-28 (SECURITY gate first pass, one HIGH finding):** (10) "One hold per customer" (§8.6 step 3, `booking.maxHoldsPerCustomer` = 1, ADR-070, US-CLN-03) was enforced only by an unlocked `AsNoTracking` read of the caller's own hold followed by an insert -- reproduced: one customer, one cat, 8 concurrent `POST /api/customers/me/appointment-holds` for 8 different slots gave 8 × `201` and left 8 live `Held` rows. New `ICustomerHoldSerializer` (Domain port) / `CustomerHoldSerializer` (Infrastructure, `Scheduling/`) runs `PlaceSlotHoldCommandHandler`'s stale-hold-expiry, own-hold lookup/release and new-hold insert inside one database transaction, guarded by a Postgres transaction-scoped advisory lock (`pg_advisory_xact_lock(hashtextextended(customerId, 0))`) keyed on the caller's own customer id -- chosen over an in-memory lock because it must hold across processes (a real deployment runs more than one API instance) and release itself automatically on a crash or dropped connection, which an in-process lock cannot do. Keyed only on the calling customer's own id, so it cannot lock-order-deadlock with ADR-070's stale-hold release (item (8)): expiring a different customer's stale hold is an ordinary row update, never a second advisory-lock acquisition (see `ICustomerHoldSerializer`'s own doc comment for the full reasoning, including why the stale-hold expiry's separate per-hold saves still preserve HIGH A's statement-ordering fix even though they now participate in this ambient transaction instead of auto-committing). The `clinic-booking` rate-limit partition (`AppointmentsModule.cs`) now keys on the resolved customer id (`ResolvedCaller`, the same pattern `AccountsModule`'s `ProfileUpdate` and `FileStorageModule`'s `FileUpload` policies use) with an IP fallback for an unresolved caller, instead of IP alone; the configured limits are unchanged. New regression test `PlaceHold_EightConcurrentRequestsFromOneCustomerForDifferentSlots_AtMostOneHeldRowRemains` (proven to fail without the lock: 8 live `Held` rows, matching the reviewer's own reproduction). **2026-09-28 (CODE_REVIEW gate first pass, one HIGH + two of that pass's MEDIUMs fixed alongside it; no file outside §13):** (11) "Today" for the waitlist worker's Close pass (§8.11) was one global UTC date compared against each entry's branch-local `LocalDate` -- fixed the same way this handover's own `JoinWaitlistCommandHandler`/`AvailabilityQueryHandlers` already compute it: `GetPastWaitlistEntriesQuery` now carries `Now` (a `DateTimeOffset`, matching its sibling worker queries) instead of a precomputed `Today`; `GetPastWaitlistEntriesQueryHandler` joins each candidate row to its own branch's time zone in one round trip (no N+1 over rows or branches) and compares against `BranchCalendar.LocalDateOf(now, branch.TimeZoneId)` per row; `ClinicWorkCycle` passes `now` straight through. Fixed the same UTC-vs-branch-local slip in two call sites the gate marked MEDIUM (routed to it, but cheap to fix in the same pass): `RegisterWalkInCommandHandler` and `GetBookingPerformersQueryHandler` now load the branch and use its local date instead of `DateTimeOffset.UtcNow`'s date. New `tests/CatEcommerce.IntegrationTests/Endpoints/WaitlistFlowTests.cs` covers §16 I-G1..I-G4 end to end through the real `CustomerWaitlistController` HTTP surface and a real `ClinicWorkCycle.RunAsync` pass (a new `RunClinicWorkCycleAsync` test-base helper, the same shape `NotificationsIntegrationTestBase.RunDeliveryPassAsync` already uses) -- never `OfferWaitlistSlotCommand` dispatched directly -- plus join/list, withdraw, and a `Pacific/Honolulu` branch-local-midnight regression for the Close fix, proven during development to fail against the pre-fix UTC logic (temporarily reverted, confirmed the entry closed early, then restored). **2026-09-28 (ACCEPTANCE gate fix, five §18 criteria with no test evidence; one product-code fix found and root-caused while writing I-F2, no file outside §13 for the fix itself):** (12) While writing I-F2 (A14/A15) it came out that §10.4's own requirement — `PerformerBusy`, `RoomBusy` and `CatAlreadyBooked` must name the conflicting appointment's local time in their message (*"Dr Rahman already has an appointment 10:00–10:30."*) — was never implemented: `SlotClaim.Diagnose` (already in §13) only ever produced the generic "This performer already has an appointment at this time." A test asserting the CURRENT (spec-non-compliant) message would have passed but been wrong, so the root cause was fixed instead (the instruction under which this pass ran is explicit: fix the bug, don't weaken the test). `SlotClaim.Diagnose` now takes the branch's own `timeZoneId`, finds the specific overlapping interval (`FindOverlap`, new) and formats it with `TimeZoneInfo.ConvertTime` (`FormatLocalRange`, new) — the same instant-to-local-**display** conversion `ClinicNotifications.FormatLocal` already uses for message variables; ADR-045 governs local-to-*instant* conversion, not this read-only display direction, so this is not a new ADR-045 site. Customer-facing responses are unaffected (`SlotClaim.ForCustomer` still collapses all three to the generic `SlotNoLongerAvailable`, §12.5) — only staff routes (`Move`, staff `Book`) see the specific, now-time-stamped message. Five new integration test files close A12–A15/A21's missing evidence, each traceable to its own §16 id by an `IDn_`/`IFn_` prefix: `AppointmentBookingFlowTests.cs` (extended, I-C6/I-D7), `AppointmentReminderFlowTests.cs` (new, I-D4/I-D5/I-D6), `AppointmentMoveConflictTests.cs` (new, I-F2/I-F3/I-F4), `AppointmentFlaggingTests.cs` (new, I-F6/I-F7); `AppointmentsIntegrationTestBase.cs` gained `RunNotificationDeliveryPassAsync`, the same shape `RunClinicWorkCycleAsync` (11) already added, since this base class does not itself inherit `NotificationsIntegrationTestBase`. Two small unit-test additions for A13 "if practical": `ClinicNotificationsTests.cs` (Application.Tests, U-D1) and `AppointmentReminderGuardTests.cs` (Infrastructure.Tests, U-D2, matching `CatSubjectGuardTests.cs`'s own pattern for the sibling care-reminder guards). <br><br>**2026-10-07 — owed row from [`HO-EMR-001`](HO-EMR-001.md) (OQ-17, §13's note), recorded by the integrator at that handover's merge (`d3527166`); an append, no existing behaviour changes.** (13) **ADR-133 guard lines**, one in each of the two handlers HO-EMR-001 §13 names, now in the files that hold them: `CompleteAppointmentCommandHandler` (`AppointmentVisitCommandHandlers.cs`) refuses with `VisitNotFinalised` while a visit for the appointment has `Status == Open` (finalising a visit does not complete the appointment; the vet completes it afterwards), and `CancelAppointmentCommandHandler` (`AppointmentChangeCommandHandlers.cs`) refuses a **staff** cancel of a `CheckedIn` appointment that already has a visit, with `VisitInProgress`. Each handler gained one constructor parameter, the `VisitAggregate` repository (read through `ExistAsync`), plus its usings; nothing else in either file changed. Two HO-APT-001 test sites that construct these handlers were updated for the parameter, and `tests/CatEcommerce.IntegrationTests/AppointmentsApiFactory.cs` was unsealed so `ConsultationApiFactory` can derive from it. See HO-EMR-001 §1 Amended (2026-10-07). <br><br>**2026-10-07 — owed row from [`HO-BIL-001`](HO-BIL-001.md) (§8.5, "Appointment item source"), recorded by the integrator at that handover's merge (`0442f9ff`) after checking `src/`; no existing behaviour changes and no HO-APT-001 file was edited.** (14) **Charge capture for appointments is now built, in Billing.** `AppointmentCompletedEvent` is consumed by `CaptureChargesOnAppointmentCompletedHandler` (registered in `BillingModule`), which opens a fresh DI scope, dispatches `CaptureSourceChargesCommand` there, and catches and logs every failure (ids only) so a Billing fault can never fail or poison the completion; a failed capture stays visible in Billing's `GetUncapturedSourcesQuery` and is re-run by staff. `AppointmentBillableItemSource` reads the appointment read-only: one `Service` charge in the `Clinic` business line from the appointment's `ServiceSnapshot`, the service's **current** tax class (the snapshot carries none, F34), `ServiceDate` the local date of `CompletedOn` in the branch zone, source line ref `"service"`; walk-ins are appointments and are charged the same way. The consultation fee is charged here only; HO-EMR-001's later items are additional (HO-BIL-001 §8.5, OQ-14). A completed appointment whose service has no tax class is skipped and reported (`TaxClassNotFound`), never guessed. Billing makes **no** change to the Appointment aggregate, its event or its handlers. |
| Amended | **2026-10-11 -- HO-SRG-001's full amendment of this slice, recorded by the gate fix round on `ho/SRG-001` (closes HO-SRG-001 OQ-4; ARCHITECTURE backlog SRG 30-31).** Everything below is an **append** that HO-SRG-001 section 11.7 binds; no existing ordinary-appointment behaviour changes. **(1) Staff-time table.** New table `clinic.appointment_staff_time` (one row per staff member an appointment occupies: the performer, and for a surgery the assistant too), kept in step with the appointment by `AppointmentAggregate.SyncStaffTime()` on every transition that changes who or when; exclusion constraint `ex_appointment_staff_time` (GiST on `staff_account_id` and `tstzrange(starts_on, ends_on, '[)')`, `WHERE (occupies)`) makes a double-booked assistant impossible even under a race. Migration `AddAppointmentStaffTime` (HO-SRG-001). **(2) Columns.** `clinic.appointment` gains `surgery_id` and `assistant_id` (both nullable; set only by `BookForSurgery`). **(3) New aggregate methods** (only the surgery slice calls them): `BookForSurgery`, `MoveForSurgery`, `CancelForSurgery`, `CompleteSurgerySlot`, and the private `SyncStaffTime`. While `SurgeryId` is set every ordinary transition refuses with `AppointmentManagedBySurgery` (`EnsureNotManagedBySurgery()` in seven methods; the early-return in `AppointmentChangeCommandHandlers.cs` and `AppointmentStaffCommandHandlers.cs`, ADR-172). **(4) G4 -- an assistant counts as busy.** `AppointmentSchedulingInputs` includes the assistant column when it builds a staff member's busy intervals, so ordinary availability for a nurse who is assisting a surgery no longer offers that time. **(5) G5 -- calendar and list.** Staff calendar and list filters and view fields carry the surgery id and the assistant (`ClinicViewModels.cs`, `StaffAppointmentQueryHandlers.cs`, `CustomerAppointmentQueryHandlers.cs`), so a surgery appears on the theatre/vet/assistant calendars. **(6) G7 -- leave flagging.** `FlaggableAppointmentFinder.cs` matches `PerformerId == staff || AssistantId == staff`, so a booked surgery is flagged when its assistant (not only its performer) goes on leave. **(7) G8 -- `409 PerformerBusy`.** A `23P01` on `ex_appointment_staff_time` maps to `PerformerBusy` (and, on `/api/staff/surgeries/**`, to the surgery-specific `StaffBusy`), alongside the existing `ex_appointment_performer`. **(8) Theatre services** (`RequiredRoomKind = Theatre`) are refused by the ordinary booking path (`ServiceNotBookable`); only the surgery slice books them. Scope note: the files touched are listed in HO-SRG-001 section 1 Amended (build) (11) and (14). |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Let customers **book, move and cancel clinic appointments online**, and let reception run the day:
phone bookings, walk-ins in a triaged queue, check-in, no-shows and a calendar where moves are
re-validated.

**There is no deposit and no payment at booking** (owner instruction, 2026-09-22). A booking is an
**open appointment**: it holds a vet, a room and a time for a named cat, and the visit is paid at the
clinic through Billing later (HO-BIL). The SRS never asked for an appointment deposit — FR-BRD-7's
deposit is boarding's — so nothing here contradicts it.

Four things this slice is careful about:

1. **No double booking, ever.** Two customers clicking the same slot at the same moment get exactly
   one hold (US-CLN-03 AC3). This is enforced by **PostgreSQL exclusion constraints** on the vet's,
   the room's and the cat's time ranges — not by a read-then-write check that a race can slip through
   (ADR-068, F28).
2. **Slots are computed, not stored.** Availability is derived on every request from HO-ADM-001's
   branch calendar, rosters and leave, the service's duration and qualified roles, the rooms, and the
   existing bookings — in one pure domain service (ADR-069).
3. **Holds are short and single.** A hold lasts `booking.slotHoldMinutes` (10). A customer can hold
   **one** slot at a time, so nobody can block a morning by opening tabs (ADR-070).
4. **Messages ride the booking.** Confirmation, reminders at 24 h and 2 h, reschedule and cancellation
   notices are enqueued **in the same transaction** as the change, through HO-NTF-001's outbox, and
   a reminder for an appointment that has since moved is never sent (ADR-074).

§17 builds the slice in **seven phases**; each compiles, migrates and passes its own tests first.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-CLN-01 | M | 5 | AC1 is **HO-ADM-001's** service catalogue (name, duration, price, description, qualified roles). **AC2 here**: slots are offered only against staff holding a qualified role | B |
| US-CLN-02 | M | 8 | Slots by service, date, branch and (optionally) vet, from roster, room availability, duration and bookings; none on closed days | B |
| US-CLN-03 | M | 5 | Slot hold with expiry (default 10 min); expired holds release; exactly one concurrent hold succeeds; one hold per customer | C |
| US-CLN-04 | M | 5 | Customer cancel and reschedule before the cut-off (default 4 h); the reschedule is one atomic update; after the cut-off, refused with the branch's contact details | D |
| US-CLN-06 | M | 3 | Confirmation at once on the preferred channel; reminders 24 h and 2 h before; notices on reschedule and cancellation | D |
| US-CLN-10 | M | 3 | Status machine `booked → checked_in → in_consultation → completed`, `cancelled`, `no_show`; invalid transitions refused; check-in puts the cat on the vet's queue | E |
| US-CLN-05 | M | 8 | Walk-ins into a live queue with triage priority; emergencies first; estimated waits recalculated on every read | E |
| US-CLN-08 | S | 3 | No-show recorded by staff after the start time passes; per-customer count, visible at booking | E |
| US-CLN-07 | S | 8 | Day/week calendar per vet and per room; staff booking and **move** with every conflict re-validated and a specific reason on refusal | F |
| US-CLN-09 | C | 5 | Waitlist for a full day; freed slots offered in queue order with a claim window (`booking.waitlistClaimMinutes`, 30); an unclaimed offer passes to the next person | G |

Also delivered, from HO-ADM-001's contracts: **appointments flagged for rescheduling** when a vet's
leave is approved or a closure is added (HO-ADM-001 US-ADM-03 AC2, ADR-048).

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409` | ADR-011, ADR-016 |
| **No deposit, no payment at booking** | Owner, 2026-09-22 |
| Branch-local time via IANA zones; conversion only in HO-ADM-001's domain services | HO-ADM-001 ADR-045 |
| Availability inputs: `IBranchCalendar`, `StaffAvailability` | HO-ADM-001 ADR-047 |
| Prices are snapshotted at transaction time, never re-read | HO-ADM-001 ADR-049 |
| `StaffLeaveApprovedEvent`, `LeaveCancelledEvent`, `BranchClosureCreatedEvent` are the contracts to flag bookings | HO-ADM-001 ADR-048, §11.5 |
| Active branch from `X-Branch-Id` for staff; customers pass `branchId` | HO-ADM-001 ADR-046 |
| Messages only through `INotificationOutbox.Enqueue`, in the command handler, before the save; never from an event handler | HO-NTF-001 ADR-056, ADR-057 |
| `CustomerMergedEvent` is binding on every context holding a customer id | HO-ACC-001 ADR-039 |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-APT-002`](HO-APT-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-CLN-03 "with a visible countdown" | The hold returns `expiresOn`; confirm after it is refused with `SlotHoldExpired` |
| US-CLN-04 "the clinic's contact details are shown" | `ChangeCutoffPassed` is refused with a `400`; `GET /api/branches` (HO-ADM-001) already returns the contact details |
| US-CLN-05 "estimated wait … displayed" | The queue read returns `estimatedWaitMinutes` per entry, recomputed on every call |
| US-CLN-07 "I drag an appointment" | `POST …/move` with the target; the drag is the FE's |
| US-CLN-08 "visible at booking" | `noShowCount` on the staff customer summary used by the booking screen |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Where it lands | What this slice delivers |
|---|---|---|
| Vitals at check-in, SOAP notes, "complete requires a finalised note" | **HO-EMR-001** | The `CheckedIn → InConsultation → Completed` transitions and `AppointmentStartedEvent` / `AppointmentCompletedEvent`. HO-EMR-001 may add an invariant to `Complete` by an Amended row |
| Surgery scheduling in theatres | HO-SRG-001 | Surgery bookings are appointments on `Theatre` rooms; the exclusion constraints already cover them |
| Charging for the visit | **HO-BIL-001** | `PriceSnapshot` on every appointment; `AppointmentCompletedEvent` |
| Portal dashboard of upcoming appointments | HO-POR-001 | `GET /api/customers/me/appointments` |
| Offline read-only day list (NFR-AVL-3) | HO-XCT-001 | The day list read (§12.4) |
| Reporting (daily schedule, no-shows) | HO-RPT-001 | The tables |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No SMS or email provider** (HO-NTF-001 OQ-1) | Confirmations and reminders are enqueued, rendered and logged; outside Development only the **in-app copy** is delivered |
| Emergency / after-hours model (SRS Q-10) | Staff may book inside a performer's shift even outside opening hours (§8.5); online booking never can |
| Grooming at launch (SRS Q-9) | Grooming is an ordinary service category; nothing special is built |
| Notifying the customer automatically when their appointment is flagged for rescheduling | Reception reschedules or cancels, which sends the normal notice (OQ-4) |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, a second host, a `CommandService` layer, `202 Accepted`,
`[Authorize]`. **No stored slot table** — slots are computed (ADR-069). **No application-level lock,
advisory lock or `SELECT … FOR UPDATE`** to prevent double booking — the exclusion constraints are the
control (ADR-068). **No payment, deposit or balance** on an appointment. **No enqueue from an event
handler** (HO-NTF-001 ADR-057). **No time-zone library.**

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001 merged** | ❌ **not implemented at baseline** — §20.1 |
| PostgreSQL 17.5 with the **`btree_gist`** extension available | ✅ verified on `catecommerce_dev` (F28); installed by this slice's first migration |
| `dotnet-ef` 9.0.0 | ✅ |
| New NuGet packages | **None** |

The shared-database rules of HO-CAT-001 §16.1 and HO-ACC-001 §16.1 apply. **Every test creates its
own branch** (HO-ADM-001 lets a test create one) with its own rooms and rostered staff, so slot and
conflict assertions never see another test's bookings (§16.1).

---

## 6. Governing architecture documents

Identical to [HO-ACC-001 §6](HO-ACC-001.md#6-governing-architecture-documents). Of note here:

| Document | Why it matters in this slice |
|---|---|
| `.ai/bounded-contexts.md` rule 4 | Names **"appointment slot availability"** as a domain service — `AppointmentSlotFinder` (ADR-069) |
| `.ai/bounded-contexts.md` rule 1 | One aggregate per command; two recorded exceptions here (ADR-070, ADR-075) |
| `.ai/domain-events-and-outbox.md` §3–§5 | Event handlers for leave, closures, cat changes and merges mutate appointments but never enqueue |
| `.ai/persistence-ef-core.md` | Exclusion constraints are raw SQL in the migration; EF does not model them |
| HO-ADM-001 §8.2 | The branch-scoping rule: an appointment carries `BranchId` |
| HO-NTF-001 §8.4, §8.7 | The producer contract and the send-time guard extension point |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22 and HO-NTF-001 F23–F27 apply.
Additionally:

| # | Fact | Consequence |
|---|---|---|
| F28 | On the local PostgreSQL 17.5, **`btree_gist` 1.7 is available** (not installed). In a rolled-back transaction, `EXCLUDE USING gist (staff WITH =, tstzrange(starts, ends, '[)') WITH &&) WHERE (status IN (…))` on a `uuid` column: **refused an overlapping row** with *"conflicting key value violates exclusion constraint"*; **accepted a back-to-back row** (`[)` ranges); **accepted two rows with a `NULL` staff id**; **accepted an overlapping row whose status was outside the predicate** | The constraints in §12.1 work as designed: adjacent slots are fine, unassigned walk-ins never conflict, cancelled and expired rows free their time. The violation's SQLSTATE is **`23P01`**, not `23505` |
| F29 | `btree_gist` is a **trusted** extension on PostgreSQL 13+, so the database owner can `CREATE EXTENSION` without superuser | The migration can install it; production needs the same (§20.2) |
| F30 | HO-ACC-001's `UniqueConstraintExceptionFilter` maps `23505` only | Extend it to map named `23P01` constraints (§12.5); unknown ones still rethrow |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Clinic context — schema `clinic`   (HO-EMR-001 adds its roots to the same context)
AppointmentAggregate     (root)  → BranchId, CustomerId, CatId, ServiceId, PerformerId?, RoomId?   clinic.appointment
└── StatusHistory [owned many ≤ 20]  AppointmentStatusChange                                       clinic.appointment_status_change
WaitlistEntryAggregate   (root)  → BranchId, CustomerId, CatId, ServiceId, PreferredPerformerId?   clinic.waitlist_entry
```

**A hold is an appointment in state `Held`.** Keeping holds and bookings in one table is what lets
one set of exclusion constraints stop a hold overlapping a booking, a booking overlapping a hold, and
two holds overlapping each other (ADR-068). A hold never appears as an appointment in any list; it
becomes one on `Confirm`.

**One aggregate mutated per command**, with two recorded exceptions, each committed in **one**
`SaveChangesAsync`:
- **Placing a hold** also expires stale holds that overlap it and the customer's previous hold
  (ADR-070).
- **Offering a waitlist slot** creates the hold and updates the entry (ADR-075).

Cross-aggregate reads — the cat's owner, the service's price, the performer's roles, the room's kind —
are repository or query calls in the handler.

### 8.2 `AppointmentAggregate`

| Field | Type | Notes |
|---|---|---|
| `BranchId` | Guid | HO-ADM-001 §8.2's rule |
| `CustomerId`, `CatId` | Guid | The cat's owner at booking time |
| `ServiceId` | Guid | |
| `ServiceSnapshot` | owned VO | `Name`, `Category`, `DurationMinutes`, `Price` (`Money`) — **read at booking, never re-read** (HO-ADM-001 ADR-049) |
| `PerformerId?`, `RoomId?` | Guid? | Null only for a walk-in not yet started |
| `StartsOn?`, `EndsOn?` | `DateTimeOffset?` | `EndsOn = StartsOn + DurationMinutes`. Null only for a walk-in |
| `IsWalkIn` | bool | |
| `Channel` | `BookingChannel` | `Online`, `Reception`, `WalkIn`, `Waitlist` |
| `Status` | `AppointmentStatus` | §8.3 |
| `HoldExpiresOn?` | `DateTimeOffset?` | `Held` only |
| `WaitlistEntryId?` | Guid? | A hold offered to a waitlist entry |
| `ReasonForVisit?` | string ≤ 500 | From the customer; plain text |
| `TriagePriority` | `TriagePriority` | `Routine` (default), `Urgent`, `Emergency` |
| `PresentingComplaint?` | string ≤ 500 | Walk-ins |
| `CheckedInOn?`, `StartedOn?`, `CompletedOn?` | | |
| `CancelledOn?`, `CancelledBy?`, `CancellationReason?` (≤ 500), `CancelledByCustomer` | | |
| `NoShowRecordedOn?`, `NoShowRecordedBy?` | | |
| `RescheduleCount` | int | |
| `ScheduleStamp` | string | `StartsOn` in round-trip format, **changed by every reschedule or move** — the reminder guard compares it (§8.9) |
| `NeedsRescheduling`, `RescheduleFlagReason?`, `RescheduleFlagSourceId?` | bool / enum / Guid? | §8.10 |
| `StatusHistory` | owned | `From`, `To`, `ChangedOn`, `ChangedBy` — Appendix C transitions are auditable at a glance |

### 8.3 The status machine — US-CLN-10 — ADR-067

```
Held ──Confirm──► Booked ──CheckIn──► CheckedIn ──Start──► InConsultation ──Complete──► Completed
  │                 │                    │
  └─► HoldExpired   ├─► Cancelled        └─► Cancelled (staff only)
     HoldReleased   └─► NoShow
WalkIn: created directly as CheckedIn (IsWalkIn, no start/end, no performer until Start)
```

| Method | From | Rules | Event |
|---|---|---|---|
| `PlaceHold` | — | §8.6 inputs; `HoldExpiresOn = now + booking.slotHoldMinutes` | — *(no event; a hold is not a booking)* |
| `Confirm(now)` | `Held` | `now < HoldExpiresOn` else **`SlotHoldExpired`** (and the hold becomes `HoldExpired` in the same save) | `AppointmentBookedEvent` |
| `ReleaseHold` / `ExpireHold(now)` | `Held` | Customer abandons / the expiry passed | — |
| `BookDirect` | — | Staff booking and moves skip the hold; same slot rules (§8.5) | `AppointmentBookedEvent` |
| `Reschedule(slot, now, cutoff)` | `Booked` | Customer: `now ≤ StartsOn − cutoff` (**current** start) else `ChangeCutoffPassed`; staff: no cut-off. New slot valid (§8.5). **Same row, one update** — the old time is released and the new one taken atomically (US-CLN-04 AC1) | `AppointmentRescheduledEvent` |
| `Cancel(by, reason, now, cutoff)` | `Booked`; staff also `CheckedIn` | Customer: before the cut-off, else `ChangeCutoffPassed` | `AppointmentCancelledEvent` |
| `CheckIn(now, zone)` | `Booked` | On the appointment's **local date** in the branch zone, or up to `clinic.earlyCheckInMinutes` (60) before a start just after midnight; not after a no-show | `AppointmentCheckedInEvent` |
| `RegisterWalkIn` | — | Creates `CheckedIn`, `IsWalkIn`, `CheckedInOn = now` | `WalkInRegisteredEvent` |
| `SetTriagePriority(p)` | `CheckedIn` | | `TriagePriorityChangedEvent` |
| `Start(performer, room, now)` | `CheckedIn` | A walk-in gets its performer and room here (the performer must hold a qualified role) | `AppointmentStartedEvent` |
| `Complete(now)` | `InConsultation` | HO-EMR-001 may add invariants | `AppointmentCompletedEvent` |
| `RecordNoShow(now)` | `Booked` | `now ≥ StartsOn + clinic.noShowGraceMinutes` (15) else `NoShowTooEarly` | `AppointmentNoShowRecordedEvent` |

Any other transition → **`InvalidAppointmentTransition`**, message naming both states (*"A completed
appointment cannot be checked in."*) — US-CLN-10 AC2. Every transition appends to `StatusHistory`.

### 8.4 Time ranges and the exclusion constraints — ADR-068

An appointment **occupies** its performer, its room and its cat for `[StartsOn, EndsOn)` while its
status is `Held`, `Booked`, `CheckedIn` or `InConsultation`, and it is not a walk-in. Three
constraints in `clinic.appointment` enforce it (§12.1):

| Constraint | Stops |
|---|---|
| `ex_appointment_performer` | A vet or nurse in two places at once |
| `ex_appointment_room` | Two appointments in one room |
| `ex_appointment_cat` | One cat in two appointments at once |

**Stale holds.** A `Held` row whose `HoldExpiresOn` has passed still matches the predicate — a
constraint cannot call `now()`. It is released **lazily**: the availability query ignores it, and every
write that takes time first expires the stale holds overlapping its range, in the same save (ADR-070).
A stale hold therefore never blocks a slot that the customer was shown as free.

### 8.5 Slot rules — US-CLN-01 AC2, US-CLN-02 — ADR-069

`AppointmentSlotFinder` (Domain.DomainServices, **pure**, no I/O):

```
FindSlots(SlotRequest request) → IReadOnlyList<Slot(StartsOn, EndsOn, PerformerId, RoomId)>

SlotRequest:
  Service         (DurationMinutes, QualifiedRoleIds, RequiredRoomKind)
  LocalDate, Zone (branch)
  Performers      [(StaffAccountId, RoleIds, AvailableIntervals)]   // HO-ADM-001 StaffAvailability
  Rooms           [(RoomId, Kind, Name, IsActive)]
  Busy            performer → intervals, room → intervals, cat → intervals
  GridMinutes     booking.slotIntervalMinutes
  EarliestStart   now + booking.minimumNoticeMinutes   (online) · now (staff)
  PreferredPerformerId?
```

Rules, each tested:

1. **Qualified performers only**: a performer is a candidate only if their roles intersect the
   service's `QualifiedRoleIds`. A vet-only service offers nothing against a nurse-only roster —
   **US-CLN-01 AC2**.
2. **Availability**: a slot `[s, s + duration)` lies wholly inside one of the performer's available
   intervals. For **online** requests those intervals come from `StaffAvailability` with
   `restrictToOpeningHours = true` (so closed days and holidays yield nothing — **US-CLN-02 AC2**);
   for **staff** requests with `false` (a shift outside hours counts — Q-10).
3. **Grid**: starts are aligned to `GridMinutes` from **local midnight in the branch zone** (10:00,
   10:15, …), converted to instants by HO-ADM-001's calendar.
4. `s ≥ EarliestStart`; for online requests `LocalDate ≤ today + booking.horizonDays`.
5. Not overlapping the performer's, the cat's, or — for the chosen room — the room's busy intervals.
6. **Room**: the first active room of `RequiredRoomKind` that is free, ordered by name —
   deterministic, so the hold gets the room the customer was shown.
7. `PreferredPerformerId` set → only that performer's slots.
8. Result sorted by start, then performer name; no duplicates.

**Busy intervals** are appointments in an occupying status (§8.4) **excluding holds whose
`HoldExpiresOn` has passed**, and excluding the appointment being rescheduled.

**A service is bookable** when it is active and priced (HO-ADM-001 `IsPriced`); **online** also needs
`IsBookableOnline`. Otherwise `ServiceNotBookable`.

### 8.6 Holds — US-CLN-03 — ADR-070

`PlaceSlotHoldCommand` handler:

1. Service bookable online; cat exists, **alive**, owned by the caller → else `404` (a customer must
   not learn that a cat id exists — HO-ADM-001 §12.3's rule).
2. Recompute availability for the requested day (§8.5) and require the requested
   `(StartsOn, PerformerId?)` to be among the slots → else `SlotNoLongerAvailable`. With no performer
   chosen, take the first slot at that start (rule 8).
3. **Expire** any stale holds overlapping the slot's performer, room or cat range (repository query →
   `ExpireHold` each), and **release** the caller's own previous `Held` appointment, if any
   (`booking.maxHoldsPerCustomer` = 1 — ADR-070).
4. `PlaceHold` → enlist, then save — **one** `SaveChangesAsync` for all of it.
5. A **`23P01`** from any of the three constraints means another hold won the race →
   **`409 SlotNoLongerAvailable`** (US-CLN-03 AC3). The handler does not retry.

`ConfirmAppointmentCommand`: load the caller's hold (`404` otherwise); `Confirm(now)` (else
`SlotHoldExpired`, **US-CLN-03 AC2**); re-check the cat is alive; store `ReasonForVisit`; enqueue the
confirmation and the reminders (§8.9); `UpdateAsync`.

### 8.7 Walk-ins and the queue — US-CLN-05, US-CLN-10 — ADR-071

**The queue** of a branch is every appointment in `CheckedIn` (booked and walk-in) plus those
`InConsultation`, read on demand — there is no queue aggregate.

**Order** (`ClinicQueueOrdering`, pure):
1. `TriagePriority` descending — **emergencies first** (US-CLN-05 AC1).
2. Within a priority: booked appointments by `StartsOn`, walk-ins by `CheckedInOn`, interleaved by
   that one timestamp ("due time").
3. Ties: `CheckedInOn`, then id.

**A vet's queue** is the branch queue filtered to entries whose performer is that vet **or** that
have no performer yet (unassigned walk-ins any qualified performer may take) — US-CLN-10 AC1's "the
cat appears on the vet's queue".

**Estimated wait** (`ClinicQueueEstimator`, pure) for a waiting entry *i*:

```
ahead      = Σ duration of waiting entries before i  +  Σ remaining minutes of InConsultation entries
performers = max(1, number of qualified performers on shift at the branch now)   // StaffAvailability
wait(i)    = ceil_to_5( ahead / performers )
```

It is an **estimate**, labelled as one, and **recomputed on every read** — adding an emergency changes
every later entry's wait the next time the screen refreshes (UI-5, 30 s) — US-CLN-05 AC2. There is no
stored wait to go stale.

### 8.8 No-shows — US-CLN-08

`RecordNoShow` (§8.3). The **count** is derived: `COUNT(*) WHERE customer_id = … AND status =
NoShow` — surfaced by `GetCustomerAppointmentSummaryQuery` (§12.6), which the staff booking screen
and the customer page read. A derived count cannot drift and survives a merge by the reassignment in
§11.6.

### 8.9 Messages — US-CLN-06 — ADR-074

Templates appended to HO-NTF-001's catalogue (Transactional; all with `InApp`):

| Key | Urgency | Enqueued by | Variables |
|---|---|---|---|
| `appointment.confirmed` | **Urgent** — the customer booked it seconds ago, or was on the phone (the same reasoning as reset) | Confirm, staff booking | `catName`, `serviceName`, `startsOnLocal`, `performerName`, `branchName`, `branchAddress`, `manageLink` (Url) |
| `appointment.reminder` | Normal | Confirm, staff booking, reschedule, move | same + `hoursBefore` |
| `appointment.rescheduled` | Urgent | Reschedule, move | same + `previousStartsOnLocal` |
| `appointment.cancelled` | Urgent | Cancel (either side) | `catName`, `serviceName`, `startsOnLocal`, `cancelledByClinic` ("yes"/"no" as text), `bookLink` (Url) |
| `waitlist.slot-offered` | Normal | Waitlist offer (§8.11) | `catName`, `serviceName`, `startsOnLocal`, `claimLink` (Url), `claimByLocal` |

`startsOnLocal` is formatted by the producer in the **branch zone** as `ddd d MMM yyyy, HH:mm`
(`Mon 12 Oct 2026, 10:30`) — HO-NTF-001's `Date` kind carries dates only.

**Reminders** — enqueued at booking with two amendments to HO-NTF-001 (§1 Amended row there):

- **`SendAfter`** on the request: the request is not expanded before it. Reminders use
  `StartsOn − booking.firstReminderHours` (24) and `StartsOn − booking.secondReminderHours` (2); a
  reminder whose `SendAfter` is already past at booking time is **not enqueued**. `NotAfter = StartsOn`.
- **`SubjectStamp`** on the request, and a new subject kind **`AppointmentReminder`** whose guard
  (`AppointmentReminderGuard`) is valid while the appointment is `Booked` **and** its `ScheduleStamp`
  equals the request's stamp. A reschedule or move changes the stamp and enqueues fresh reminders; the
  old ones fail the guard and are never sent. A cancellation fails it too.

`manageLink` = `INotificationLinkBuilder.AppointmentLink(appointmentId)` →
`{WebBaseUrl}/portal/appointments/{id}` (new method). **This slice also changes `CatCareLink(catId)`**
to `{WebBaseUrl}/book?catId={catId}&category={Vaccination|FollowUp}` — the booking link HO-NTF-001 §4.2
reserved for it.

### 8.10 Flagging for rescheduling — HO-ADM-001 ADR-048

Event handlers set `NeedsRescheduling` with a reason and a source id on every **future `Booked`**
appointment affected. They **never cancel and never enqueue**; reception acts on the flagged list
(§12.4), and whatever reception does sends the normal notice.

| Event | Affected | Reason |
|---|---|---|
| `StaffLeaveApprovedEvent` | `PerformerId == StaffAccountId`, local date in `[StartsOn, EndsOn]` | `PerformerOnLeave` (source = leave request id) |
| `LeaveCancelledEvent` (`WasApproved`) | Flagged with that source id | **Cleared** |
| `BranchClosureCreatedEvent` | `BranchId`, inside the closure (a partial closure by its times) | `BranchClosed` (source = closure id) |
| `BranchClosureCancelledEvent` | Flagged with that source id | Cleared |
| `CatMarkedDeceasedEvent` | The cat's future appointments | `CatDeceased` |
| `CatOwnershipTransferredEvent` | The cat's future appointments | `CatOwnerChanged` |

Each handler loops with one `UpdateAsync` per appointment and is **idempotent** (already flagged with
the same source → skip) — ADR-048's requirement. Rescheduling or cancelling clears the flag.

### 8.11 Waitlist — US-CLN-09 — ADR-075

**`WaitlistEntryAggregate`** — `BranchId`, `CustomerId`, `CatId`, `ServiceId`, `LocalDate`,
`PreferredPerformerId?`, `Status` (`Waiting` → `Offered` → `Claimed`; `Offered` → `Lapsed`; `Waiting`
/`Offered` → `Withdrawn`; `Waiting` → `Closed` when the date passes), `OfferedAppointmentId?`,
`OfferExpiresOn?`, `CreatedOn` (the queue order).

- **Join**: only when the day has **no** slot for the service (else `SlotsStillAvailable` — book one
  instead); the cat alive and owned; one `Waiting`/`Offered` entry per cat, service and date
  (`AlreadyWaitlisted`); the date within the horizon.
- **Offer** — `WaitlistOfferPass` in the worker (§12.2), every minute, **outside the branch's quiet
  hours only** (nobody is offered a slot at 03:00): for each branch/service/date with `Waiting`
  entries and no live offer, compute slots; if one exists, dispatch `OfferWaitlistSlotCommand` for the
  **oldest** entry (US-CLN-09 AC1 "in queue order"). The handler places a hold for that customer with
  `HoldExpiresOn = now + booking.waitlistClaimMinutes`, `Channel = Waitlist`, marks the entry
  `Offered`, and enqueues `waitlist.slot-offered` — **one** save (ADR-075). A `23P01` means the slot
  went elsewhere: the entry stays `Waiting`.
- **Claim** — the customer confirms the held appointment with the normal confirm endpoint; the entry
  becomes `Claimed` (read by id in the confirm handler, second root in the same save — within
  ADR-075's exception).
- **Lapse** — the next pass finds an `Offered` entry whose `OfferExpiresOn` has passed →
  `LapseWaitlistOfferCommand` → `Lapsed`; the stale hold is freed lazily, and the following pass offers
  the slot to the **next** entry (US-CLN-09 AC2).

### 8.12 Ports and domain services

| Name | Kind | Notes |
|---|---|---|
| `AppointmentSlotFinder` | pure domain service | §8.5 |
| `ClinicQueueOrdering`, `ClinicQueueEstimator` | pure | §8.7 |
| `AppointmentSchedulingInputs` | port, Infrastructure | Loads, for one branch and date: the service, performers with roles and HO-ADM-001 availability, rooms, busy intervals (non-stale). One place, used by the slot query and by every command that takes time |
| `AppointmentReminderGuard` | `INotificationSubjectGuard` (HO-NTF-001 §8.7) | §8.9 |

### 8.13 Exceptions

Reuse `DomainRuleViolationException`. No new type.

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. HO-NTF-001 owns ADR-055…066; **this handover owns ADR-067…076**
(README ADR table).

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-067** | **`Clinic` bounded context**, schema `clinic`, shared with HO-EMR-001. Appointment statuses are Appendix C's plus two **pre-booking** states, `Held` and `HoldExpired`/`HoldReleased`, which never appear as appointments | Not in `.ai/bounded-contexts.md` (OQ-1); the extra states look like a deviation from Appendix C |
| **ADR-068** | **Double booking is prevented by three PostgreSQL exclusion constraints** (performer, room, cat) over `tstzrange(starts_on, ends_on, '[)')` for occupying statuses, using `btree_gist`. `23P01` → `409 SlotNoLongerAvailable`. No application lock | FR-CLN-3 "prevented by a transactional hold"; a read-then-write check has a race; the constraint has none (F28) |
| **ADR-069** | **Slots are computed** by the pure `AppointmentSlotFinder` from HO-ADM-001's calendar and availability, service, rooms and bookings, on a branch-local grid; no slot table | `.ai/bounded-contexts.md` rule 4; a stored slot table must be regenerated on every roster, leave, closure or service change |
| **ADR-070** | **Holds** are `Held` appointments, 10 min, **one per customer**; stale holds are released lazily by the next write that overlaps them, in the same save — a recorded rule-1 exception | Without one-per-customer a script can hold a morning; without lazy release a stale hold blocks a slot shown as free |
| **ADR-071** | **The queue is a read**, ordered by triage then due time; the estimated wait is computed on every read from durations and performers on shift, and labelled an estimate | A stored wait goes stale the moment an emergency arrives |
| **ADR-072** | **Staff bookings and moves skip the hold** and are validated by the same slot rules, except the online-only rules (notice, horizon, opening hours, `IsBookableOnline`); the same constraints protect them | Reception on the phone should not wait on a countdown; an after-hours emergency needs a shift, not opening hours |
| **ADR-073** | **No deposit, no payment** at booking; the appointment carries a `PriceSnapshot` for Billing | Owner instruction; the snapshot keeps HO-ADM-001 ADR-049's promise |
| **ADR-074** | **Appointment messages** are enqueued in the command handler with the change; reminders use HO-NTF-001's new `SendAfter` and `SubjectStamp`, so a moved or cancelled appointment's reminders fail their guard. Confirmation, reschedule and cancellation notices are `Urgent` | FR-CLN-7 "immediately" against FR-NTF-5 quiet hours — the recipient is the one acting |
| **ADR-075** | **Waitlist** offers are made by a worker pass outside quiet hours, oldest entry first; an offer is a hold with the claim window; one save for hold + entry + message — a recorded rule-1 exception | Offering from the cancel handler would couple cancellation to waitlist logic and could not respect quiet hours |
| **ADR-076** | **Flagging, not cancelling**: leave, closure, death and ownership events flag affected future appointments; handlers are idempotent and never enqueue | ADR-048's contract; an automatic cancellation would message a customer about a decision nobody made |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | `.ai/bounded-contexts.md` needs the `Clinic` context | Architect | ADR-067 records it |
| OQ-2 | Buffer time between appointments (cleaning a room)? | Owner + clinical lead | None |
| OQ-3 | Appendix D gives nurses **R** on appointments — may nurses check in, triage or start a vaccination appointment? | Owner | No — Appendix D as written (§12.3) |
| OQ-4 | Should a customer be messaged automatically when their appointment is flagged? | Owner | No — reception acts |
| OQ-5 | Show vets' names publicly in availability? | Owner | Yes — full name as recorded |
| OQ-6 | Limit on upcoming appointments per customer? | Owner | None beyond one hold at a time |
| OQ-7 | Block online booking for customers with many no-shows? | Owner | No — shown to staff only |
| OQ-8 | Should an unclaimed waitlist entry rejoin the queue? | Owner | No — `Lapsed` is terminal; the customer may join again |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `AppointmentStatus` | `Held`, `HoldExpired`, `HoldReleased`, `Booked`, `CheckedIn`, `InConsultation`, `Completed`, `Cancelled`, `NoShow` |
| `BookingChannel` | `Online`, `Reception`, `WalkIn`, `Waitlist` |
| `TriagePriority` | `Routine` (1), `Urgent` (2), `Emergency` (3) — ordered by `Value` |
| `RescheduleFlagReason` | `PerformerOnLeave`, `BranchClosed`, `CatDeceased`, `CatOwnerChanged` |
| `WaitlistStatus` | `Waiting`, `Offered`, `Claimed`, `Lapsed`, `Withdrawn`, `Closed` |

HO-NTF-001's `NotificationSubjectKind` gains **`AppointmentReminder`** (append only).

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`

Already in the registry, consumed here: `booking.changeCutoffHours` (4), `booking.slotHoldMinutes`
(10), `booking.waitlistClaimMinutes` (30). New:

| Key | Group | Type | Default | Bounds | Branch override |
|---|---|---|---|---|---|
| `booking.slotIntervalMinutes` | Booking | Integer | 15 | 5–60, divides 60 | ✅ |
| `booking.minimumNoticeMinutes` | Booking | Integer | 60 | 0–2880 | ✅ |
| `booking.horizonDays` | Booking | Integer | 60 | 1–365 | ✅ |
| `booking.firstReminderHours` | Booking | Integer | 24 | 0–168 (0 = off) | ❌ |
| `booking.secondReminderHours` | Booking | Integer | 2 | 0–48 (0 = off) | ❌ |
| `clinic.noShowGraceMinutes` | Clinic | Integer | 15 | 0–240 | ✅ |
| `clinic.earlyCheckInMinutes` | Clinic | Integer | 60 | 0–240 | ✅ |

No migration (values are rows). `booking.slotIntervalMinutes` "divides 60" is a new bound check in the
registry's validator: one line, tested (U-D6 of HO-ADM-001 extended by U-B9 here).

### 10.3 Other constants

`AppointmentRules` (`MaxHoldsPerCustomer = 1`, `MaxReasonLength = 500`, `MaxCalendarRangeDays = 7`,
`MaxSlotRangeDays = 1`) · `WaitlistRules` · `ClinicMessages` · the five template definitions of §8.9
appended to `NotificationTemplateCatalogue`.

### 10.4 Violation codes — append to `BusinessViolationCodes`

`ServiceNotBookable` · `SlotNoLongerAvailable` · `SlotOutsideAvailability` · `SlotNotOnGrid` ·
`SlotTooSoon` · `SlotBeyondHorizon` · `SlotHoldExpired` · `HoldNotFound` · `AppointmentNotFound` ·
`InvalidAppointmentTransition` · `ChangeCutoffPassed` · `NoShowTooEarly` · `CheckInNotToday` ·
`PerformerNotQualified` · `PerformerNotAvailable` · `PerformerBusy` · `RoomKindMismatch` ·
`RoomNotAvailable` · `RoomBusy` · `CatAlreadyBooked` · `CatNotBookable` · `SlotsStillAvailable` ·
`AlreadyWaitlisted` · `WaitlistEntryNotFound` · `CancellationReasonRequired`.

`PerformerBusy`, `RoomBusy` and `CatAlreadyBooked` name the conflicting appointment's local time in
their message (*"Dr Rahman already has an appointment 10:00–10:30."*) — US-CLN-07 AC2's "specific
reason". They are raised by the handler's pre-check and, on a race, by the constraint mapping (§12.5).

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Clinic/`

Namespace `CatEcommerce.Application.Commands.Clinic`. Data only; ids minted in the controller;
`ValidateCommand = true` everywhere.

| # | Command | Sent by | Phase |
|---|---|---|---|
| C1 | `PlaceSlotHoldCommand` (`AppointmentId`, `BranchId`, `ServiceId`, `CatId`, `StartsOn`, `PerformerId?`) | Customer | C |
| C2 | `ReleaseSlotHoldCommand` (`AppointmentId`) | Customer | C |
| C3 | `ConfirmAppointmentCommand` (`AppointmentId`, `ReasonForVisit?`) | Customer | C |
| C4 | `RescheduleAppointmentCommand` (`AppointmentId`, `StartsOn`, `PerformerId?`) | Customer | D |
| C5 | `CancelAppointmentCommand` (`AppointmentId`, `Reason?`) | Customer, staff | D |
| C6 | `BookAppointmentByStaffCommand` (`AppointmentId`, `CustomerId`, `CatId`, `ServiceId`, `StartsOn`, `PerformerId`, `RoomId?`, `ReasonForVisit?`) — branch from the active branch | Staff | F |
| C7 | `MoveAppointmentCommand` (`AppointmentId`, `ExpectedVersion`, `StartsOn`, `PerformerId`, `RoomId`) | Staff | F |
| C8 | `RegisterWalkInCommand` (`AppointmentId`, `CustomerId`, `CatId`, `ServiceId`, `TriagePriority`, `PresentingComplaint?`) | Staff | E |
| C9 | `CheckInAppointmentCommand` (`AppointmentId`, `TriagePriority?`) | Staff | E |
| C10 | `SetTriagePriorityCommand` (`AppointmentId`, `TriagePriority`) | Staff | E |
| C11 | `StartAppointmentCommand` (`AppointmentId`, `PerformerId?`, `RoomId?` — required for a walk-in) | Staff | E |
| C12 | `CompleteAppointmentCommand` (`AppointmentId`) | Staff | E |
| C13 | `RecordNoShowCommand` (`AppointmentId`) | Staff | E |
| C14 | `JoinWaitlistCommand` (`WaitlistEntryId`, `BranchId`, `ServiceId`, `CatId`, `LocalDate`, `PreferredPerformerId?`) · `WithdrawFromWaitlistCommand` | Customer | G |
| C15 | `OfferWaitlistSlotCommand` (`WaitlistEntryId`, `AppointmentId`) · `LapseWaitlistOfferCommand` · `CloseStaleWaitlistEntriesCommand` (entries whose date has passed, one per call) | Worker | G |

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: ids non-empty, `StartsOn` has an offset, text lengths, enum membership, `LocalDate` not in
the past **by UTC date minus one day** (the precise "today" is branch-local and the handler's).
Availability, cut-off, grid and qualification are aggregate or handler rules.

### 11.3 Mappers and DTOs

`AppointmentMapper`, `WaitlistMapper` (static `ToDomainDto()` extensions). DTOs in `Domain/Models/`:
`SlotHoldDto`, `DirectBookingDto`, `RescheduleDto`, `WalkInDto`, `AppointmentStartDto`,
`WaitlistEntryDto`, `ServiceSnapshot` inputs.

### 11.4 Command handlers — `CommandHandlers/Clinic/`

Every handler that **takes time** (C1, C4, C6, C7, C15-offer) follows one sequence, implemented once
in `SlotClaim` (Application, a static helper over the ports — not a service layer):

1. Load inputs through `AppointmentSchedulingInputs` for the branch and the local date of `StartsOn`.
2. Validate the requested slot with `AppointmentSlotFinder` rules (online or staff mode) — on failure
   the **specific** code: `PerformerNotQualified`, `PerformerNotAvailable`, `SlotNotOnGrid`,
   `SlotTooSoon`, `SlotBeyondHorizon`, `RoomKindMismatch`, `RoomNotAvailable`, `PerformerBusy`,
   `RoomBusy`, `CatAlreadyBooked`.
3. Expire overlapping stale holds (enlisted).
4. Mutate and save once. A `23P01` → the constraint's code (§12.5), `409`.

| Handler | Specifics |
|---|---|
| **C1 Place hold** | §8.6 |
| **C2 Release** | Own `Held` only; anything else → success, nothing written |
| **C3 Confirm** | §8.6. Enqueue `appointment.confirmed` (channel from preferences) and the two reminders (§8.9). If the hold has `WaitlistEntryId`, load the entry and `Claim` it in the same save (ADR-075) |
| **C4 Reschedule** (customer) | Own appointment (`404` otherwise); cut-off against the **current** start; `SlotClaim` in online mode, excluding this appointment's own busy time; `Reschedule`; enqueue `appointment.rescheduled` and fresh reminders; clears `NeedsRescheduling` |
| **C5 Cancel** | Customer: own, cut-off. Staff: `Appointments.Book`, any time, reason required (`CancellationReasonRequired` via validator when the caller is staff). Enqueue `appointment.cancelled`. The freed time is picked up by the waitlist pass |
| **C6 Staff booking** | Branch from `IActiveBranchAccessor`; cat's owner == `CustomerId` (`CatNotBookable`); `SlotClaim` in **staff** mode; `BookDirect` with `Channel = Reception`; room chosen by rule 6 when `RoomId` is null; enqueue confirmation and reminders |
| **C7 Move** | `ExpectedVersion` (the drag started from a stale calendar → `409`); `SlotClaim` in staff mode excluding itself; `Reschedule` (staff); enqueue `appointment.rescheduled` + fresh reminders **only if the start changed** (a room swap at the same time sends nothing) |
| **C8 Walk-in** | Cat owned by the customer; service active; `RegisterWalkIn` |
| **C9 Check-in** | `CheckIn` with the branch zone; optional triage |
| **C11 Start** | Walk-in: `PerformerId` and `RoomId` required, performer qualified and assigned to the branch, room of the kind and active. Booked: may reassign the room (`RoomBusy` checked). No exclusion constraint applies to walk-ins (§8.4) |
| **C13 No-show** | `RecordNoShow` |
| **C14 Join** | §8.11 rules; `SlotsStillAvailable` uses the online slot finder for that date |
| **C15 Offer** | §8.11; skipped (success, no write) if the entry is no longer `Waiting` |

### 11.5 Domain events — `Events/Clinic/`

Ids, instants, enums and booleans only — no reason text, complaint text or names.

`AppointmentBookedEvent` (`AppointmentId`, `BranchId`, `CustomerId`, `CatId`, `ServiceId`,
`PerformerId`, `StartsOn`, `Channel`), `AppointmentRescheduledEvent` (+ `PreviousStartsOn`),
`AppointmentCancelledEvent` (`CancelledByCustomer`), `AppointmentCheckedInEvent`,
`WalkInRegisteredEvent`, `TriagePriorityChangedEvent`, **`AppointmentStartedEvent`**,
**`AppointmentCompletedEvent`** (HO-EMR-001 and HO-BIL-001 contracts), `AppointmentNoShowRecordedEvent`,
`AppointmentFlaggedForRescheduleEvent`, `WaitlistJoinedEvent`, `WaitlistSlotOfferedEvent`,
`WaitlistOfferLapsedEvent`.

### 11.6 Domain event handlers — `DomainEventHandlers/Clinic/`

| Handler | Handles | Does |
|---|---|---|
| `FlagAppointmentsOnLeaveApprovedHandler` · `UnflagAppointmentsOnLeaveCancelledHandler` | HO-ADM-001 leave events | §8.10 |
| `FlagAppointmentsOnClosureHandler` · `UnflagAppointmentsOnClosureCancelledHandler` | HO-ADM-001 closure events | §8.10 |
| `FlagAppointmentsOnCatDeceasedHandler` · `FlagAppointmentsOnCatTransferredHandler` | HO-CAT-001 events | §8.10 |
| `ReassignAppointmentsOnCustomerMergedHandler` | HO-ACC-001 `CustomerMergedEvent` | Every appointment and waitlist entry of the duplicate → the survivor; idempotent. **No-show counts follow**, because they are derived (§8.8) |
| `AuditNoShowHandler` | `AppointmentNoShowRecordedEvent` | `BusinessEvent` audit row with the recorder — a no-show is held against a customer, so who recorded it matters |

All of them loop with one `UpdateAsync` per row and **never enqueue** (HO-NTF-001 ADR-057). Data
changes are audited by HO-ACC-001's interceptor.

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

Two `DbSet`s; one configuration per root; schema `clinic`; the HO-ACC-001 §12.1 conventions;
`ServiceSnapshot.Price` as `amount numeric(12,2)` + `currency_code char(3)`.

| Index / constraint | Kind |
|---|---|
| `ex_appointment_performer` | `EXCLUDE USING gist (performer_id WITH =, tstzrange(starts_on, ends_on, '[)') WITH &&) WHERE (status IN (Held, Booked, CheckedIn, InConsultation) AND NOT is_walk_in)` |
| `ex_appointment_room` | same over `room_id` |
| `ex_appointment_cat` | same over `cat_id` |
| `appointment (branch_id, starts_on)` | calendar, day list, slot inputs |
| `appointment (performer_id, starts_on)`, `(room_id, starts_on)` | calendar per vet / room |
| `appointment (customer_id, starts_on)`, `(customer_id, status)` | "my appointments", no-show count |
| `appointment (branch_id, status)` | queue |
| `appointment (status, hold_expires_on)` | stale-hold lookup |
| `appointment (needs_rescheduling, branch_id)` filtered `WHERE needs_rescheduling` | flagged list |
| `waitlist_entry (branch_id, service_id, local_date, status, created_on)` | offer pass |
| `waitlist_entry (cat_id, service_id, local_date)` **UNIQUE** filtered `WHERE status IN (Waiting, Offered)` | `ix_waitlist_active_per_cat` |

The status values in the predicate are the **`Enumeration.Value` integers**, written from the
enumeration in the migration code (`AppointmentStatus.Held.Value`, …) — never typed as literals.
U-X3 checks the constraint text against the enumeration.

**Migrations**, each read before commit:
- `AddClinicAppointments` (C): `CREATE EXTENSION IF NOT EXISTS btree_gist;`, the table, the three
  exclusion constraints (`migrationBuilder.Sql`) and the indexes. The `Down` drops the constraints and
  table but **not** the extension (another object may use it).
- `AddClinicWaitlist` (G).

Phases A, B, D, E and F add no migration.

### 12.2 Infrastructure services and the worker

| Class | Notes |
|---|---|
| `Scheduling/AppointmentSchedulingInputs` | §8.12; `AsNoTracking`; calls HO-ADM-001's `IBranchCalendar` and `StaffAvailability`; one round trip per input set |
| `Notifications/Guards/AppointmentReminderGuard` | §8.9 |
| `Notifications/NotificationLinkBuilder` | `AppointmentLink`, `WaitlistClaimLink` (= `AppointmentLink` of the held appointment) added; `CatCareLink` changed (§8.9) |

**`ClinicWorker : BackgroundService`** (Api, `Background/`) — the same shape and rules as HO-NTF-001's
`NotificationWorker` (a scope per unit of work, never throws out of `ExecuteAsync`, disabled in
`Testing`, `ClinicWorkCycle` callable by tests). One pass, every 60 s: close past-date waitlist
entries, lapse expired offers, then offer freed slots (§8.11). Configuration `Clinic:Worker:Enabled`,
`Clinic:Worker:IntervalSeconds` (60). **Holds are not expired by the worker** — lazily (ADR-070).

### 12.3 Authorization

Appendix D "Appointments": Customer **W (own)**, Reception **F**, Nurse **R**, Vet **W**, Admin **F**.
Appended to the one `ActionRoles` table:

| Action | Staff roles | Endpoints |
|---|---|---|
| `Appointments.View` | Reception, Nurse, Vet, Administrator | calendar, day list, queue, detail, availability (staff mode), flagged list, customer summary |
| `Appointments.Book` | Reception, Vet, Administrator | staff booking, move, cancel, walk-in |
| `Appointments.CheckIn` | Reception, Vet, Administrator | check-in, triage |
| `Appointments.Treat` | Vet, Administrator | start, complete |
| `Appointments.RecordNoShow` | Reception, Administrator | no-show |

Nurses read only — OQ-3 records the likely objection. Customer endpoints carry
`[RequireCustomerSession]` and every handler resolves the appointment **by id and caller**: someone
else's appointment is **`404`**. Availability is `[PublicEndpoint]` with an `availability` rate limit
(60 per minute per IP).

### 12.4 Api — the contract HO-APT-002 consumes

JSON camelCase; instants ISO-8601 with offset; local dates `yyyy-MM-dd`; money as HO-ADM-001 §12.4.
Every appointment view carries `branchTimeZoneId`, so the client formats in the branch zone.

**Public and customer**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/booking/slots?branchId&serviceId&date&performerId&catId` | public · `availability` | `{ date, branchTimeZoneId, slots: [{ startsOn, endsOn, performers: [{ id, name }] }] }` — grouped by start. With `catId` and a customer session, the cat's own bookings are excluded as busy; without, cat conflicts are checked at hold time |
| `GET` | `/api/booking/performers?branchId&serviceId` | public | staff holding a qualified role and assigned to the branch: `{ id, name }` — for the "choose a vet" filter |
| `POST` | `/api/customers/me/appointment-holds` | customer · `booking` limit | C1 → `201 { appointmentId, startsOn, endsOn, performer, room, holdExpiresOn }`; `409 SlotNoLongerAvailable` |
| `DELETE` | `/api/customers/me/appointment-holds/{appointmentId}` | customer | C2 → `204` |
| `POST` | `/api/customers/me/appointments/{appointmentId}/confirm` | customer | C3 → `200 { appointment }`; `400 SlotHoldExpired` |
| `GET` | `/api/customers/me/appointments?scope=upcoming\|past&pageNumber&pageSize` | customer | paged `AppointmentSummary`: `id`, `catId`, `catName`, `service` (snapshot), `startsOn`, `endsOn`, `performerName`, `branch` (`id`, `name`, `phone`), `branchTimeZoneId`, `status`, `canChange` + `changeCutoffOn` |
| `GET` | `/api/customers/me/appointments/{id}` | customer | detail; `404` if not the caller's. **Returns the caller's own `Held` appointment too**, with `holdExpiresOn` and `isWaitlistOffer`, so a waitlist claim link (§8.11) opens it and offers **Confirm** |
| `POST` | `/api/customers/me/appointments/{id}/reschedule` | customer | C4 → `200 { appointment }`; `400 ChangeCutoffPassed`; `409 SlotNoLongerAvailable` |
| `POST` | `/api/customers/me/appointments/{id}/cancel` | customer | C5 → `204`; `400 ChangeCutoffPassed` |
| `GET` / `POST` | `/api/customers/me/waitlist` | customer | the caller's entries / C14 → `201 { waitlistEntryId, claimWindowMinutes }` |
| `DELETE` | `/api/customers/me/waitlist/{id}` | customer | withdraw → `204` |

**Staff** (branch from `X-Branch-Id`, HO-ADM-001 §8.3)

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/staff/appointments/slots?serviceId&date&performerId&catId` | `Appointments.View` | staff-mode slots (§8.5 rule 2), with `outsideOpeningHours` per slot |
| `GET` | `/api/staff/appointments/calendar?from&to&view=performer\|room&ids` | `Appointments.View` | ≤ 7 days: `gridMinutes` (`booking.slotIntervalMinutes` for the branch), `resources[]` (performers or rooms), `availability[]` per performer (HO-ADM-001 intervals), `closures[]`, `appointments[]` (id, `version`, cat, customer, service, start, end, performer, room, status, `isWalkIn`, `needsRescheduling`, `catIsHighRisk`) |
| `GET` | `/api/staff/appointments/day?date` | `Appointments.View` | the day list for the branch, sorted by start (NFR-AVL-3's input) |
| `GET` | `/api/staff/appointments/{id}` | `Appointments.View` | detail incl. `statusHistory[]`, `reasonForVisit`, flags, `allowedTransitions[]` (computed by the aggregate's rules — the UI shows only these) |
| `POST` | `/api/staff/appointments` | `Appointments.Book` | C6 → `201 { appointmentId }` |
| `POST` | `/api/staff/appointments/{id}/move` | `Appointments.Book` | C7 → `200 { appointment }`; `400` specific reason; `409` stale version or race |
| `POST` | `/api/staff/appointments/{id}/cancel` | `Appointments.Book` | C5 → `204` |
| `POST` | `/api/staff/walk-ins` | `Appointments.Book` | C8 → `201 { appointmentId }` |
| `POST` | `/api/staff/appointments/{id}/check-in` | `Appointments.CheckIn` | C9 → `204` |
| `PUT` | `/api/staff/appointments/{id}/triage` | `Appointments.CheckIn` | C10 → `204` |
| `POST` | `/api/staff/appointments/{id}/start` · `/complete` | `Appointments.Treat` | C11 / C12 → `204` |
| `POST` | `/api/staff/appointments/{id}/no-show` | `Appointments.RecordNoShow` | C13 → `204` |
| `GET` | `/api/staff/queue?performerId` | `Appointments.View` | `{ generatedOn, performersOnShift, entries: [{ appointmentId, position, catName, catIsHighRisk, customerName, service, triagePriority, isWalkIn, dueOn, checkedInOn, status, performerName?, estimatedWaitMinutes? }] }` — `performerId=me` resolves to the caller |
| `GET` | `/api/staff/appointments/flagged` | `Appointments.View` | future appointments with `needsRescheduling`, reason and source |
| `GET` | `/api/staff/customers/{customerId}/appointment-summary` | `Appointments.View` | `{ noShowCount, upcoming[], lastVisitOn? }` — **US-CLN-08 "visible at booking"** |

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes (R1–R8).

### 12.5 Filters

Extend HO-ACC-001's `UniqueConstraintExceptionFilter` to **`23P01`** by constraint name (F30):
`ex_appointment_performer` → `409 PerformerBusy`; `ex_appointment_room` → `409 RoomBusy`;
`ex_appointment_cat` → `409 CatAlreadyBooked`; for **customer** routes all three collapse to
`409 SlotNoLongerAvailable` (a customer does not need to know which resource lost the race). Map
`ix_waitlist_active_per_cat` (`23505`) → `400 AlreadyWaitlisted`. Unknown constraints rethrow.

### 12.6 Read side — `src/Read/`

Queries in `Queries/Clinic/`, handlers in `QueryHandlers/Clinic/`, ViewModels flat; explicit
projections, `AsNoTracking()`.

- `GetAvailableSlotsQuery` (public and staff modes) — loads through `AppointmentSchedulingInputs`, calls
  `AppointmentSlotFinder`. **Never computes availability in SQL.**
- `GetCalendarQuery`, `GetDayListQuery`, `GetAppointmentDetailQuery` (with `allowedTransitions` from
  `AppointmentTransitionRules` — the aggregate uses the same table).
- `GetClinicQueueQuery` — `ClinicQueueOrdering` + `ClinicQueueEstimator`.
- `GetCustomerAppointmentsQuery`, `GetCustomerAppointmentDetailQuery` — `canChange` from the same
  cut-off function the commands use.
- `GetCustomerAppointmentSummaryQuery`, `GetFlaggedAppointmentsQuery`, `GetCustomerWaitlistQuery`.
- Worker queries: `GetWaitlistOfferCandidatesQuery`, `GetLapsedWaitlistOffersQuery`,
  `GetPastWaitlistEntriesQuery`.

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler and event handler in §11–§12.6;
`AppointmentSchedulingInputs` (Scoped); `AppointmentReminderGuard` into HO-NTF-001's
`SubjectGuardResolver`; the five templates via the catalogue; `ClinicWorkCycle` (Scoped);
**`AddHostedService<ClinicWorker>()`**; the `availability` and `booking` (10 holds per minute per
IP, HO-ACC-001 ADR-030's shape) rate-limit policies. `ValidateScopes`/`ValidateOnBuild` stay on.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the five enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Enums/NotificationSubjectKind.cs                              ← AppointmentReminder only
src/Shared/CatEcommerce.Shared/Common/Constants/{AppointmentRules,WaitlistRules,ClinicMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs      ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                                ← the seven keys of §10.2 and the divides-60 bound only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs                    ← the five definitions of §8.9 only
src/Domain/CatEcommerce.Domain/Aggregates/Clinic/**
src/Domain/CatEcommerce.Domain/Aggregates/Notification/NotificationRequestAggregate.cs              ← SendAfter, SubjectStamp only (HO-NTF-001 amendment)
src/Domain/CatEcommerce.Domain/Entities/AppointmentStatusChange.cs
src/Domain/CatEcommerce.Domain/ValueObjects/ServiceSnapshot.cs
src/Domain/CatEcommerce.Domain/DomainServices/{AppointmentSlotFinder,ClinicQueueOrdering,ClinicQueueEstimator,AppointmentTransitionRules,IAppointmentSchedulingInputs}.cs
src/Domain/CatEcommerce.Domain/DomainServices/INotificationLinkBuilder.cs                            ← two methods added only
src/Domain/CatEcommerce.Domain/Models/**                                                            ← new DTOs; NotificationRequestDto gains SendAfter, SubjectStamp
src/Domain/CatEcommerce.Domain/Events/Clinic/**
src/Application/CatEcommerce.Application/Commands/Clinic/**
src/Application/CatEcommerce.Application/CommandHandlers/Clinic/**                                  ← incl. SlotClaim
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Clinic/**
src/Application/CatEcommerce.Application/DataMappers/{AppointmentMapper,WaitlistMapper}.cs
src/Read/CatEcommerce.Read/Queries/Clinic/**
src/Read/CatEcommerce.Read/QueryHandlers/Clinic/**
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs                ← DbSets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{AppointmentConfiguration,WaitlistEntryConfiguration}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/NotificationRequestConfiguration.cs ← two columns only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                            ← AddClinicAppointments, AddClinicWaitlist, AddNotificationRequestScheduling only
src/Infrastructure/CatEcommerce.Infrastructure/Scheduling/AppointmentSchedulingInputs.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Guards/AppointmentReminderGuard.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs             ← §12.2 only
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationWorkQueue.cs               ← honour SendAfter only
src/Api/CatEcommerce.Api/Controllers/{BookingSlots,CustomerAppointments,CustomerWaitlist,StaffAppointments,StaffWalkIns,StaffQueue}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Clinic/**
src/Api/CatEcommerce.Api/Background/{ClinicWorker,ClinicWorkCycle}.cs
src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs                                 ← 23P01 mappings only
src/Api/CatEcommerce.Api/appsettings.json                                                           ← Clinic:Worker section only
src/Api/CatEcommerce.Api/Program.cs                                                                 ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-APT-001.md
docs/gates/HO-APT-001-*.md
.ai/decisions/adr.md                                                                                ← ADR-067…076, append only
```

The HO-NTF-001 amendment (`SendAfter`, `SubjectStamp`) is small but it is another area's aggregate:
its migration is `AddNotificationRequestScheduling`, its tests are U-G1…U-G3 here, and HO-NTF-001's
§1 Amended row records it.

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-1) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs |
| `web/**` | HO-APT-002 |
| Every file of HO-ACC-001, HO-ADM-001, HO-CAT-001 and HO-NTF-001 beyond the lines in §13 | Their areas. **No Administration aggregate is edited** — availability is consumed, not changed |
| Any payment, deposit, invoice or balance type | ADR-073; Billing is HO-BIL |
| Visit, vitals, consultation-note types | HO-EMR-001 |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |

---

## 15. Unit test cases

| ID | Case | Expected |
|---|---|---|
| **A — status machine** | | |
| U-A1 | `AppointmentTransitionRules` full table over every `(from, action)` pair | Only §8.3's arrows allowed; every other pair `InvalidAppointmentTransition` naming both states — **US-CLN-10 AC2** |
| U-A2 | `Completed` → check-in | Refused, *"A completed appointment cannot be checked in."* |
| U-A3 | Every transition | One `StatusHistory` row |
| U-A4 | `RecordNoShow` 14 min after start with grace 15; 15 min | `NoShowTooEarly`; allowed |
| U-A5 | `CheckIn` the day before; the same local day at 07:00 for a 16:00 appointment | `CheckInNotToday`; allowed |
| U-A6 | `Confirm` at `HoldExpiresOn` exactly; one tick before | `SlotHoldExpired`; booked |
| U-A7 | `Reschedule` changes `ScheduleStamp`; a room-only move does not | As stated |
| **B — slots** | | |
| U-B1 | Vet-only service; roster with a nurse only | No slots — **US-CLN-01 AC2** |
| U-B2 | Service qualified for Vet and Nurse; both rostered | Slots for both, sorted by start then name |
| U-B3 | Branch closed that weekday; public holiday; half-day closure | No slots / none / only the open half — **US-CLN-02 AC2** |
| U-B4 | 30-min service, 15-min grid, shift 09:00–12:00, a booking 10:00–10:30 | Starts 09:00…09:30 and 10:30…11:30; none overlapping 10:00–10:30; 11:45 absent (would end 12:15) |
| U-B5 | Only room of the kind busy at 10:00 | No 10:00 slot even though the vet is free |
| U-B6 | Two rooms free | The first by name, every time |
| U-B7 | A stale hold (expired) overlapping 10:00 | 10:00 offered |
| U-B8 | Online vs staff mode with a shift 18:00–20:00 outside opening hours (closes 17:00) | Online none; staff offered with `outsideOpeningHours` |
| U-B9 | Minimum notice 60 min at 09:10; horizon 60 days + 1 | 09:00–10:00 starts absent; `SlotBeyondHorizon`; `booking.slotIntervalMinutes = 25` refused by the registry |
| U-B10 | `Europe/London` branch on the DST change day | Grid aligned to local wall-clock; no duplicated or missing hour slots beyond the real change |
| U-B11 | `PreferredPerformerId` set | Only that performer's slots |
| U-B12 | Reschedule excludes the appointment's own busy time | Its current slot and neighbours offered |
| **C — queue** | | |
| U-C1 | Routine walk-in 09:00, booked 09:15 checked in at 09:05, urgent walk-in 09:20, emergency 09:30 | Order: emergency, urgent, routine 09:00, booked 09:15 — **US-CLN-05 AC1** |
| U-C2 | Estimator with two performers on shift, 30 + 15 + 20 min ahead | ceil_to_5(65 / 2) = 35 |
| U-C3 | Adding an emergency | Every later entry's estimate increases by `duration / performers` rounded — **US-CLN-05 AC2** |
| U-C4 | No performers on shift | Divisor 1, never divide by zero |
| U-C5 | Vet's queue | Own entries plus unassigned walk-ins only |
| **D — messages** | | |
| U-D1 | Booking 30 h ahead; 3 h ahead; 90 min ahead | Two reminders; one (2 h); none |
| U-D2 | `AppointmentReminderGuard`: booked + same stamp; rescheduled; cancelled | Valid; invalid; invalid |
| U-D3 | `startsOnLocal` for 04:30Z at a Dhaka branch | `… 10:30` |
| U-D4 | All five template definitions | Pass HO-NTF-001 `TemplateRules`; `appointment.confirmed`, `.rescheduled`, `.cancelled` are `Urgent` |
| **E — waitlist** | | |
| U-E1 | Offer ordering across three `Waiting` entries | Oldest first |
| U-E2 | Offer inside the branch's quiet hours | Not made |
| U-E3 | Join when slots exist; twice for the same cat/service/date | `SlotsStillAvailable`; `AlreadyWaitlisted` |
| **X — cross-cutting** | | |
| U-X1 | Reflection over `Events/Clinic/**` | No string properties — ids, instants, enums and booleans only |
| U-X2 | `ClinicWorker` loop with a throwing pass | Logged; next pass still runs |
| U-X3 | The migration's constraint predicates | Use `AppointmentStatus.{Held,Booked,CheckedIn,InConsultation}.Value`; walk-ins excluded |
| **G — HO-NTF-001 amendment** | | |
| U-G1 | Work queue with a request whose `SendAfter` is in the future | Not returned; returned once time passes |
| U-G2 | `SubjectStamp` copied from request to notification | As stated |
| U-G3 | Guard resolver with `AppointmentReminder` | Resolves `AppointmentReminderGuard` |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1). Workers off; tests call
`ClinicWorkCycle` and HO-NTF-001's `NotificationWorkCycle`, advancing `FakeTimeProvider`.

### 16.1 Additional shared-database rules

1. **Each test creates its own branch** (via HO-ADM-001's endpoints as an administrator), its rooms,
   and its rostered staff, and books only there. Slot and conflict assertions are scoped to that branch.
2. Customers and cats are minted per test.
3. Exclusion constraints are real; a test never disables them.

| ID | Case | Expected |
|---|---|---|
| I-A1 | Migration on an empty database | `btree_gist` installed; `clinic` schema; the three exclusion constraints present (`pg_constraint` `contype = 'x'`) |
| I-A2 | Host starts, scope validation on | Every registration resolves |
| I-B1 | `GET /api/booking/slots` for a vet-only service with only a nurse rostered | `slots: []` — **US-CLN-01 AC2** |
| I-B2 | Slots on a holiday; on a normal day | Empty; the expected grid — **US-CLN-02 AC1, AC2** |
| I-B3 | Slots filtered by performer | That performer only — FR-CLN-2 "by vet" |
| I-C1 | Hold, then `GET` slots | The held slot is gone for everyone |
| I-C2 | **Two customers `POST` holds for the same slot concurrently** (20 parallel attempts) | Exactly **one** `201`, the rest `409 SlotNoLongerAvailable` — **US-CLN-03 AC3** |
| I-C3 | Hold, advance 10 min, confirm | `400 SlotHoldExpired`; the slot is offered again — **US-CLN-03 AC1, AC2** |
| I-C4 | Hold A, then hold B by the same customer | A released; B held — one hold per customer |
| I-C5 | Stale hold by customer 1; customer 2 holds the same slot after expiry | `201`; customer 1's row `HoldExpired` |
| I-C6 | Confirm | `Booked`; `priceSnapshot` equals the service's current price; a confirmation request committed with it; after the delivery pass the in-app copy exists — **US-CLN-06 AC1** |
| I-C7 | Hold another customer's cat; confirm another customer's hold | `404`; `404` |
| I-C8 | Hold for a deceased cat | `400 CatNotBookable` |
| I-D1 | Reschedule 5 h before (cut-off 4 h) | `200`; one row, new time, old time free, `rescheduleCount 1` — **US-CLN-04 AC1** |
| I-D2 | Reschedule or cancel 3 h before | `400 ChangeCutoffPassed` — **US-CLN-04 AC2** |
| I-D3 | Reschedule to a slot taken meanwhile | `409 SlotNoLongerAvailable`; the original time kept |
| I-D4 | Book 30 h ahead; advance to 6 h before; run both passes | The 24 h reminder sent; the 2 h one not yet |
| I-D5 | Reschedule after the 24 h reminder was queued but before it was sent | The old reminder `Cancelled SubjectNoLongerApplies`; new reminders for the new time — ADR-074 |
| I-D6 | Cancel | `appointment.cancelled` sent; pending reminders never sent |
| I-D7 | Staff books at 22:30 Dhaka | Confirmation sent at once (`Urgent`); reminders follow quiet hours |
| I-E1 | Walk-ins routine, urgent, emergency in that order; `GET /api/staff/queue` | Emergency first; each entry has `estimatedWaitMinutes` — **US-CLN-05** |
| I-E2 | Add an emergency; `GET` again | Later entries' estimates increased |
| I-E3 | Check in a booked appointment; `GET /api/staff/queue?performerId={vet}` | The cat appears on that vet's queue — **US-CLN-10 AC1** |
| I-E4 | Start and complete; then check in again | `204`, `204`; `400 InvalidAppointmentTransition` — **US-CLN-10 AC2** |
| I-E5 | Record a no-show before the grace passes; after | `400 NoShowTooEarly`; `204`; `noShowCount` 1 on the summary; a `BusinessEvent` audit row — **US-CLN-08** |
| I-E6 | Nurse checks in | `403` (Appendix D — OQ-3) |
| I-F1 | Staff books a slot inside a shift but outside opening hours | `201` |
| I-F2 | Move onto a time where the vet is busy; room busy; cat already booked; vet on leave; nurse for a vet-only service; wrong room kind | `400` with `PerformerBusy`, `RoomBusy`, `CatAlreadyBooked`, `PerformerNotAvailable`, `PerformerNotQualified`, `RoomKindMismatch`, each message specific — **US-CLN-07 AC1, AC2** |
| I-F3 | Move with a stale `expectedVersion` | `409` |
| I-F4 | Two staff move two appointments into the same vet time concurrently | One `200`, one `409 PerformerBusy` |
| I-F5 | Calendar for a week by performer and by room | Appointments, availability and closures for the range; > 7 days `400` |
| I-F6 | Approve leave for a vet with two future bookings | Both `needsRescheduling` with `PerformerOnLeave`; listed in `/flagged`; cancelling the leave clears both |
| I-F7 | Add a closure over a booked day | Flagged `BranchClosed` |
| I-F8 | Mark the cat deceased | Its future appointments flagged `CatDeceased`; nothing cancelled, nothing sent |
| I-F9 | Merge the customer | Appointments and waitlist entries under the survivor; the survivor's `noShowCount` includes the duplicate's |
| I-G1 | Fill a day; customer joins the waitlist; another customer cancels; run the clinic pass | The oldest entry `Offered`; a hold with the claim window; `waitlist.slot-offered` enqueued — **US-CLN-09 AC1** |
| I-G2 | Offered customer confirms | `Booked`, entry `Claimed` |
| I-G3 | Offer not claimed; advance 30 min; run the pass twice | Entry `Lapsed`; the next entry `Offered` the same slot — **US-CLN-09 AC2** |
| I-G4 | Cancellation at 23:00 | No offer until the quiet hours end |
| I-X1 | Log capture across all cases at `Information`+ | No reason-for-visit text, no complaint text, no customer names |

---

## 17. Implementation order

| Phase | Stories | Delivers |
|---|---|---|
| **A** | — | Enumerations, settings, constants, the status machine and `AppointmentTransitionRules`, `ServiceSnapshot`; the HO-NTF-001 amendment (`SendAfter`, `SubjectStamp`, the guard kind) and its migration |
| **B** | US-CLN-01 AC2, US-CLN-02 | `AppointmentSlotFinder`, `AppointmentSchedulingInputs`, the two public read endpoints |
| **C** | US-CLN-03 | The appointment table, **exclusion constraints**, holds, confirm; migration `AddClinicAppointments` |
| **D** | US-CLN-04, US-CLN-06 | Reschedule, cancel, templates, reminders, guard, link builder changes |
| **E** | US-CLN-05, US-CLN-08, US-CLN-10 | Walk-ins, check-in, triage, queue and estimator, start/complete, no-shows, summary |
| **F** | US-CLN-07 | Staff booking, calendar, move, flagging handlers, merge handler |
| **G** | US-CLN-09 | Waitlist, `ClinicWorker`; migration `AddClinicWaitlist` |

Commands as HO-ACC-001 §17. **Read the `AddClinicAppointments` migration before applying it** — the
constraint SQL is hand-written.

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | CLN-01 AC1 | Services with name, duration, price, description and roles — **delivered by HO-ADM-001** (A8 there); consumed here | HO-ADM-001 I-D1 |
| A2 | CLN-01 AC2 | No slot for a vet-only service against a nurse-only roster | U-B1, I-B1 |
| A3 | CLN-02 AC1 | Only slots satisfying roster, room and duration are shown | U-B2, U-B4, U-B5, I-B2, I-B3 |
| A4 | CLN-02 AC2 | No slots on closed days or holidays | U-B3, I-B2 |
| A5 | CLN-03 AC1 | A chosen slot is held for the configured period, with its expiry returned | I-C1, I-C3 |
| A6 | CLN-03 AC2 | An expired hold releases the slot and the customer is told to choose again | U-A6, I-C3, I-C5 |
| A7 | CLN-03 AC3 | Exactly one concurrent hold succeeds | **I-C2**, I-F4 |
| A8 | CLN-04 AC1 | Before the cut-off, reschedule releases the old slot and books the new one atomically | U-A7, I-D1, I-D3 |
| A9 | CLN-04 AC2 | After the cut-off, online change is refused (contact details from `GET /api/branches`) | I-D2 |
| A10 | CLN-05 AC1 | Walk-ins queue with triage, emergencies first | U-C1, I-E1 |
| A11 | CLN-05 AC2 | Estimated waits recalculated when an emergency is added | U-C2, U-C3, I-E2 |
| A12 | CLN-06 AC1 | A confirmation is sent at once on the preferred channel — *in-app only outside Development until a provider exists (HO-NTF-001 OQ-1)* | I-C6, I-D7 |
| A13 | CLN-06 AC2 | Reminders at 24 h and 2 h, never for a moved or cancelled appointment | U-D1, U-D2, I-D4, I-D5, I-D6 |
| A14 | CLN-07 AC1 | Moves re-validate vet, room and duration conflicts before committing | I-F2, I-F3, I-F4 |
| A15 | CLN-07 AC2 | A conflicting move is refused with the specific reason | I-F2 |
| A16 | CLN-08 AC | No-shows increment a count visible at booking | U-A4, I-E5, I-F9 |
| A17 | CLN-09 AC1 | Waitlisted customers are offered freed slots in order with a claim window | U-E1, I-G1, I-G2 |
| A18 | CLN-09 AC2 | An unclaimed offer passes to the next customer | I-G3 |
| A19 | CLN-10 AC1 | Check-in moves `booked → checked_in` and the cat appears on the vet's queue | I-E3 |
| A20 | CLN-10 AC2 | Invalid transitions are refused | U-A1, U-A2, I-E4 |
| A21 | ADM-03 AC2 *(second half)* | Approved leave flags the vet's existing appointments | I-F6, I-F7 |
| A22 | ADR-073 | No deposit or payment exists on an appointment; the price is snapshotted | I-C6 |

**Deferred, named in §4.2**: consultation, vitals and "complete requires a note" (HO-EMR-001), billing
(HO-BIL-001), portal dashboard (HO-POR-001), offline day list (HO-XCT-001).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Clinic` context (ADR-067, OQ-1); the pure slot finder, queue ordering and estimator; the two rule-1 exceptions (ADR-070, ADR-075); **no enqueue from any event handler**; the HO-NTF-001 amendment confined to §13's lines; no Administration aggregate edited |
| 2 | SECURITY | `security-reviewer` | **Exclusion constraints are the double-booking control** (I-C2); one hold per customer and the rate limits; `404` for another customer's cat, hold or appointment; the public availability endpoint discloses only names and times; `23P01` collapse for customer routes; no free text in events or logs |
| 3 | CODE_REVIEW | `code-reviewer` | Time only through HO-ADM-001's services and the slot finder; `SlotClaim` used by every time-taking handler; idempotent event handlers; the worker never throws |
| 4 | TEST | `test-engineer` | `TC-HO-APT-001.md` from §15/§16 first; §16.1's per-test branch; I-C2 with real parallelism, not sequential calls |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A1 as HO-ADM-001's; A12's provider caveat recorded, not passed silently |

---

## 20. BLOCKED conditions — stop and report

**20.1 HO-ACC-001, HO-ADM-001 or HO-NTF-001 is not merged.** This slice consumes their availability,
settings, outbox, guards, sessions and merge contract. Do not stub them.

**20.2 `CREATE EXTENSION btree_gist` fails** in a target environment (no privilege, a managed database
that forbids it). Stop — do not replace the constraints with an application lock. F29 says the
database owner can create it; if that is not true somewhere, it is a deployment decision.

**20.3 A double-booking test passes only with retries or delays.** The constraint is wrong; fix it,
never the test.

**20.4 Do not add a deposit, payment or balance** — ADR-073, owner instruction.

**20.5 Do not store slots** or precompute availability into a table — ADR-069.

**20.6 Do not cancel appointments automatically** from leave, closure or cat events — ADR-076.

**20.7 Do not enqueue from an event handler** — HO-NTF-001 ADR-057.

**20.8 A requirement conflict is a stop, not an edit** — SRS and backlog are forbidden scope.
