# Backlog — Appointments (HO-APT-001)

Medium/low findings not blocking the merge (owner rule 2026-09-27: only BLOCKER/HIGH block a gate).
Fix in a later batch.

| # | Severity | Item | Source |
|---|---|---|---|
| 1 | ~~LOW~~ Resolved | ~~`PlaceHold_TwentyConcurrentRequestsForSameSlot_ExactlyOneSucceeds` flaky on a freshly migrated DB (19 × `500`).~~ **Fixed in 8992dec:** under contention Postgres reported `40P01` deadlock (not `23P01`), and EF's execution strategy wrapped it in `InvalidOperationException`, so `UniqueConstraintExceptionFilter` never matched. The filter now unwraps the chain and maps `40P01` to the same 409. 11/11 passes across three databases, two freshly migrated. Escalated from LOW per ARCHITECTURE (§20.3). | Local run / ARCHITECTURE |
| 2 | Env | The shared `catecommerce_dev` database does not have this branch's `AddClinicAppointments` migration, so every appointment integration test fails there (`btree_gist extension is not installed`). Until the integrator merges and migrates, run against a per-branch database (`CATECOMMERCE_TEST_DB`, see `tests/CatEcommerce.IntegrationTests/README.md`); `catecommerce_apt001` exists locally for this. | Local run 2026-09-28 |
| 3 | ~~MEDIUM~~ Resolved | ~~Move handler checked `ExpectedVersion` after `Reschedule()` bumped it, so every move returned 409.~~ Fixed in 42b0925 (checks pre-mutation version; `StaleCalendarVersion` → 409). | ARCHITECTURE |
| 4 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — ADR-075 single saves: the second root's domain events are never dispatched (`WaitlistSlotOfferedEvent` silently dropped). | ARCHITECTURE |
| 5 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — `Transition` does `RemoveAt(0)` on the Clinical/Forbidden `StatusHistory` (conflicts with C-ALL-4; unreachable in practice). | ARCHITECTURE |
| 6 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — `ReleaseHold`/`ExpireHold` bypass `Transition`, so no `StatusHistory` row is written (§8.3, U-A3). | ARCHITECTURE |
| 7 | MEDIUM | Confirm, reschedule and move controllers dispatch a command then a query (cqrs.md §5 rule 1). | ARCHITECTURE |
| 8 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE (by-id staff actions scoped to the active branch).** Was — **must fix before a second branch is activated (NFR-SCL-1); condition confirmed by the owner 2026-10-04** — By-id staff actions (`StaffAppointmentsController.cs:83-196`: GetDetail, Move, Cancel, CheckIn, Triage, Start, Complete, NoShow) never compare `appointment.BranchId` with the active branch (HO-ADM-001 §8.2) — cross-branch reads of clinical `reasonForVisit` and cross-branch changes. Not HIGH today: authenticated same-business staff with role checks, single-branch launch. Fix: `404` when the branch differs. | ARCHITECTURE / SECURITY ruling |
| 9 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — Finders and Clinic reads don't filter `!IsMarkedToDelete`. | ARCHITECTURE |
| 10 | MEDIUM | The flagged list and the customer waitlist are unpaginated (UI-4). | ARCHITECTURE |
| 11 | MEDIUM | Deviation (6): `booking.slotIntervalMinutes` "divides 60" bound not built (U-B9's third clause, a value of 25 must be refused) — route a follow-up to HO-ADM-001, whose aggregate §14 forbids editing here. | ARCHITECTURE |
| 12 | MEDIUM | Files outside §13 not recorded in §1 Amended: `CareReminderSweep.cs`, `EfNotificationOutbox.cs`, `NotificationConfiguration.cs`, `ClinicOptions.cs`, `AppointmentRateLimitPolicies.cs`; `INotificationLinkBuilder.cs` goes beyond "two methods only" (`BookingLink()`, changed `CatCareLink` signature). | ARCHITECTURE |
| 13 | ~~LOW~~ Resolved | ~~`StartAppointmentCommandHandler` uses `DateTimeOffset.UtcNow` instead of `TimeProvider`.~~ TEST showed the real effect (Start's `StatusHistory` entry out of order under a fake clock); fixed after the TEST record, `AppointmentDetailStatusHistoryTests` passes. | ARCHITECTURE / TEST |
| 14 | LOW | Validators grouped in two files rather than one per command; `WaitlistMapper` absent. | ARCHITECTURE |
| 15 | ~~LOW~~ Resolved | ~~The ADR append in `.ai/decisions/adr.md` ends with a doubled `---` separator.~~ Fixed in 8d3c08d. | ARCHITECTURE |
| 16 | MEDIUM | `ErasureExemptions` base file (Shared, 8bf8e16) pre-empts HO-XCT-001, which builds that registry itself (CONTRACTS.md:168) and adds its own Appointments entry (HO-XCT-001 §8.8) — compile clash or duplicate-entry throw in `LoadAll` at merge; integrator to reconcile. Amended (7) cites the wrong precedent. | ARCHITECTURE (2nd) |
| 17 | MEDIUM | N+1 lookups remain in list handlers: `CustomerAppointmentQueryHandlers.cs:45-50`, `:135-138`; `StaffAppointmentQueryHandlers.cs:156-158`. | ARCHITECTURE (2nd) |
| 18 | MEDIUM | Per-staff loops load `StaffAccount`/`Roster` one member at a time: `StaffAppointmentQueryHandlers.cs:119-132`, `ClinicQueueQueryHandler.cs:86-97`. | ARCHITECTURE (2nd) |
| 19 | MEDIUM | A whole `BranchAggregate` is loaded only to read `TimeZoneId`: `StaffAppointmentQueryHandlers.cs:33`, `:81`. | ARCHITECTURE (2nd) |
| 20 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — New inner joins drop a row whose cat/customer/branch is missing (old code showed `""`) — queue, day list/calendar, summary. Latent: nothing is hard-deleted today. | ARCHITECTURE (2nd) |
| 21 | LOW | `SlotClaimEndpointAttribute.cs:13` and `UniqueConstraintExceptionFilter.cs:101` say "five" slot-claiming actions but list and mark four (C15 has no HTTP endpoint). | ARCHITECTURE (3rd) |
| 22 | LOW | The filter comment (`:102-104`) and §1 Amended (9) say a 40P01 maps to "the exact shape a 23P01 produces" — true only on customer routes. On staff Book/Move a 23P01 gives `PerformerBusy`/`RoomBusy`/`CatAlreadyBooked` but a 40P01 gives `SlotNoLongerAvailable` (per the owner decision); HO-APT-002's staff calendar must handle `SlotNoLongerAvailable` as a staff 409. | ARCHITECTURE (3rd) |
| 23 | LOW | `UniqueConstraintExceptionFilterTests.cs:73-82` builds endpoint metadata by hand; nothing checks the real `SlotClaim`-calling actions carry `[SlotClaimEndpoint]` — add a convention test so a missing marker can't silently turn a deadlock back into a 500. | ARCHITECTURE (3rd) |
| 24 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — `BookingSlotsController.cs:25` / `AvailabilityQueryHandlers.cs:31-32`: `catId` on the public slots endpoint is honoured without checking the caller owns the cat — diffing results reveals whether another customer's cat has appointments/holds (cat ids are on cage-card QR codes). | SECURITY |
| 25 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — Reschedule/cancel/confirm check only `appointment.CustomerId` (`AppointmentChangeCommandHandlers.cs:35-36`, `:97-98`; `AppointmentBookingCommandHandlers.cs:125-127`); after a cat ownership transfer the appointment is only flagged, so the previous owner can still change it until staff act. | SECURITY |
| 26 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — `ClinicDetailProjection.cs:57-66`: the customer detail view reuses the staff projection, exposing staff account ids (`statusHistory.changedBy`), `triagePriority`, `presentingComplaint`, `version` and staff-only `allowedTransitions`. | SECURITY |
| 27 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — Customer reschedule, confirm, cancel and waitlist join have no rate limit (only hold does), though reschedule and join run the full slot finder. | SECURITY |
| 28 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — `BookingSlotsController.cs:36-38`: public `/api/booking/performers` has no rate-limit policy. | SECURITY |
| 29 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — `AppointmentVisitCommandHandlers.cs:135-149`: Start doesn't check that the performer/room belongs to the appointment's branch. | SECURITY |
| 30 | LOW | Docs no longer match C1 (PlaceHold) after 9af0d22: stale-hold expiry is still its own save sent before the INSERT, but now commits/rolls back with the new hold inside the per-customer transaction. Add one sentence saying so to the ADR-070 amendment (`.ai/decisions/adr.md:2772-2781`, "commits … first" / "no longer atomic"), §1 Amended (8), the `SlotClaim.cs:133-148` doc comment and the `ICustomerHoldSerializer.cs:20-21` summary. | SECURITY (2nd) |
| 31 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — Stale-hold expiry now holds row locks until commit: `FindStaleOverlappingHoldIdsAsync` has no `ORDER BY` (two customers expiring the same stale holds in different orders can deadlock → correctly 409), and a request that waits on a hold another request just expired fails the `xmin` check with the generic "changed by someone else" 409 instead of `SlotNoLongerAvailable`. Order by id; skip or remap a concurrency failure during expiry. | SECURITY (2nd) |
| 32 | LOW | No test covers the per-customer `clinic-booking` rate-limit partition (the test factory raises `PermitLimit` to 100000); verified by reading only. | SECURITY (2nd) |
| 33 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-BE.** Was — Staff-vs-customer is decided by `UserContext.UserName == "staff"` (`AppointmentChangeCommandHandlers.cs:94`, `AppointmentCommandValidators.cs:52`) — a literal string match used nowhere else; the Development `dev-user` fallback is misclassified as a customer. Use the resolved caller kind instead. | CODE_REVIEW |
| 34 | MEDIUM | `WaitlistWorkerQueryHandlers.cs:56-73` (`GetPastWaitlistEntriesQueryHandler`): the branch-local date comparison runs in memory, so every `Waiting` row system-wide is loaded on each ClinicWorker tick and `Take` applies client-side (same shape as `GetWaitlistOfferCandidatesQueryHandler`). Add a coarse SQL-side date bound (e.g. `LocalDate < utcToday + 1`) before it matters at scale. | CODE_REVIEW (2nd) |
| 35 | MEDIUM | §16 group F (I-F1…I-F9) has no integration tests at all — the flagging event handlers and the customer-merge reassignment handler (cross-aggregate logic) are reviewed but never exercised. | TEST |
| 36 | LOW | Message/reminder timing (I-D1, I-D3…I-D7, U-D1…U-D4) is untested at any level — A13 has no passing case. | TEST |
| 37 | LOW | Unit coverage debt: U-B3, U-B7, U-B8, U-B9 (partial), U-B12, U-E1…U-E3, U-G1…U-G3, U-X2, U-X3 (predicate text). | TEST |
| 38 | LOW | Integration coverage debt: I-B1…I-B3, I-C1, I-C3, I-C4, I-C5, I-C7, I-C8, I-X1 (no Clinic PII log-content sweep). | TEST |
| 39 | MEDIUM | Carried halves of partially-met acceptance criteria: I-B1…I-B3, U-B3, I-C1, I-C3, I-C5, I-D1, I-D2 (reschedule HTTP half), I-D3, I-E2, I-F9 and the I-E5 audit row, U-E1 (substance overlaps rows 36-38). | ACCEPTANCE |
| 40 | LOW | `CustomerAppointmentsController.cs:40`: the C1 `201` body returns `performerId`/`roomId`, but §12.4 names them `performer`/`room` — undeclared; HO-APT-002 reads neither today. | ACCEPTANCE |
| 41 | LOW | TC sheet U-B9 and the TEST record say the booking-horizon check is unimplemented; it is implemented (`SlotClaim.cs:65-68`, `AvailabilityQueryHandlers.cs:28-29`, `WaitlistCommandHandlers.cs:42-45`) — only "divides 60" (row 11) is missing. Correct the sheet. | ACCEPTANCE |
| 42 | LOW | §4.4's "advisory lock" wording vs `CustomerHoldSerializer`: the lock enforces one hold per customer, not double booking; declared in Amended (10) but landed after ARCHITECTURE's last pass — state the carve-out in the ADR-070 amendment (overlaps row 30). | ACCEPTANCE |
| 43 | MEDIUM | `AppointmentMoveConflictTests.IF4_…`: the doc comment lists three legitimate loser shapes, but the assertion checks status ∈ {400, 409} and code ∈ {4 codes} independently, so it would also accept an undescribed 400 + `SlotNoLongerAvailable`. Pair status with code, or narrow the comment. | DELTA f516cda |
| 44 | LOW | `SlotClaim.cs` room-conflict fallback ("No room of the required kind is free at this time.") is unreachable given the caller's guard — drop the ternary or note why it's kept. | DELTA f516cda |
| 45 | ~~LOW~~ Resolved | ~~ID7 comment gave the 2 h reminder's SendAfter as 23:00Z (it is 01:00Z).~~ Fixed in 4e0b129. | ACCEPTANCE (2nd) |
| 46 | LOW | §10.4 says the race-path (constraint-mapped) refusal also names the conflicting time; it cannot (23P01 carries no interval; 40P01 is generic by owner decision (9)). Clarify §10.4 or accept (overlaps row 43). | ACCEPTANCE (2nd) |
| 47 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-MIGRATIONS.** Was — `ClinicDetailProjection` orders `statusHistory` by `ChangedOn` only; two transitions at the same instant come back in arbitrary order (seen in `AppointmentDetailStatusHistoryTests` under a fixed fake clock; the test now advances the clock). Add a tie-breaker (sequence or insertion id). | Developer, 4e0b129 |
| 48 | LOW | I-D4's delivery loop stops once the 24 h request is Expanded, but the test then requires the notification Sent; with ≥50 foreign due notifications it could still be Queued (not reproduced). Loop until Sent — safe, the 2 h reminder is 22 h away. | ACCEPTANCE (3rd) |
| 49 | LOW | `TC-HO-APT-001.md` lines 40, 144, 147 still show X-d FAIL and the full suite 557/558; X-d passes at HEAD (4/4 alone, within 573/573) — add a post-fix note. | ACCEPTANCE (3rd) |

## HO-APT-002 (appointments web)

Medium/low findings from `docs/gates/HO-APT-002-*.md` (all five gates PASS; ARCHITECTURE's BLOCKER
closed by owner decision b3377df). Real-API cells of every E-case are owed (row W20).

| # | Severity | Item | Source |
|---|---|---|---|
| W1 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-06 (integrator, owner request): the CORS policy now calls `.WithExposedHeaders("Date")`; `CorsAndCsrfTests.CrossOriginRequest_FromAConfiguredOrigin_ExposesTheDateHeader`. Real-API E-A3 run still owed (row W20).** Was — **Integrator (`src/**`):** CORS in `HostPipelineExtensions.cs:102-105` does not expose `Date`, so with the web app on a separate subdomain the hold countdown runs on the device clock. Add `.WithExposedHeaders("Date")`. Until then §18 A2 holds only under MSW (fails safe: the server owns expiry → `SlotHoldExpired`). | ARCHITECTURE / ACCEPTANCE |
| W2 | MEDIUM | **Backend:** add `serviceId` to the appointment Summary, Detail, Calendar/Day and Queue view models; then drop `findServiceIdForSnapshot` (`booking.ts:164-175`, silent "call the clinic" on rename/lookalikes) and give Move… the staff slot list (§11.6) instead of a typed time (`MoveDialog.tsx:40-50`). | ARCHITECTURE |
| W3 | MEDIUM | **Backend:** add `allowedTransitions` to Day-list and Queue entries, then remove the per-row detail reads (`useStaffAppointments.ts:80-101`, `FrontDeskView.tsx:77-83`, `FlaggedView.tsx:28-32`; ~2 req/s per Front desk on a 60-row day). Meanwhile: a `poll: false` option from Flagged (not a §8.3 polling screen) and a `staleTime`. | ARCHITECTURE / CODE_REVIEW |
| W4 | MEDIUM | **Backend:** add customer name and phone to `FlaggedAppointmentViewModel` (now joined client-side from detail + customer reads). | ARCHITECTURE |
| W5 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-WEB.** Was — `MyAppointmentView.tsx:57` — `detail.startsOn.slice(0,10)` is the UTC date; use `localDateOf(detail.startsOn, zone)`. Wrong day before 06:00 Dhaka and for evening slots west of UTC (London mock branch). | ARCHITECTURE / CODE_REVIEW |
| W6 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-WEB.** Was — `MoveDialog.tsx:117-125` / `CalendarView.tsx:79,91,104-113` — `expectedVersion` is captured when the dialog opens; after a `StaleCalendarVersion` 409 every retry resends it and 409s until the dialog is reopened (confirmed by a TEST probe). Read the version live, as `FlaggedView` does. Strengthen U-E2 (`calendar.test.tsx:259-290`) with refetch + resubmit assertions and a drop-path 409 variant. | CODE_REVIEW / TEST |
| W7 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-WEB.** Was — `StaffBookingForm.tsx:156-167` — after `SlotNoLongerAvailable` the stale `slotStart` stays set; the next Book opens a confirm with an empty consequence and Confirm silently does nothing. | CODE_REVIEW |
| W8 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-WEB.** Was — `ConfirmStep.tsx:44-66` / `BookingFlow.tsx:150-154` — the typed reason for visit is lost when the hold expires (NFR-USA-5); the component comment claims it survives. | CODE_REVIEW |
| W9 | MEDIUM | The two calendar drag e2e tests (E-E1 free slot, busy slot) failed once in the 29-minute chromium run and passed every isolated rerun — undiagnosed flake. | TEST |
| W10 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-WEB.** Was — `CustomerSessionGuard.tsx:54` — the session-expired path still builds `next` from the path only, dropping `/book?…` parameters (the first redirect at :47 keeps them). | SECURITY |
| W11 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-WEB.** Was — `lib/calendar/ics.ts:17-22` — strip other control characters (and U+0085/U+2028) from `.ics` text fields; some calendar apps reject them. | SECURITY |
| W12 | LOW | `client.ts:19` header comment should mention the optional `ApiConflictError` body; `mocks/handlers/appointments/fixtures.ts:147-149` `apiUrl()` duplicates `files/fixtures.ts:15`, and nothing prevents a mock importing `@/lib/api/client` again (the bb183b8 import-order hazard) — add a lint rule. | ARCHITECTURE |
| W13 | LOW | Undeclared second portal nav entry "Book a visit" (`nav/areas/appointments.ts:21`) — record in §1 Amended or drop. | ARCHITECTURE / ACCEPTANCE |
| W14 | LOW | `LeaveHoldGuard.tsx:15-57` copies `UnsavedChangesGuard`'s mechanism; fold back once ACC row (consequence/onConfirm props) lands. | ARCHITECTURE |
| W15 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-WEB.** Was — Calendar/Move polish: the "Moving…" placeholder uses the original drop target after the user edits it (`CalendarView.tsx:104-125`); the confirm sentence reads "with the same … in the same" before the vet/room lists load (`MoveDialog.tsx:157-163`). | CODE_REVIEW |
| W16 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-APT-WEB.** Was — Zone handling: `zonedLocalToInstant` applies a single DST correction (`lib/format/appointment.ts:104-130`); UTC fallback while data loads (`MyAppointmentView.tsx:55,64,98`, `WaitlistSection.tsx:37`, `FlaggedView.tsx:46`) with a misleading "can't be rescheduled online" while services load; zone falls back to `branches[0]` with no active branch (`CalendarView.tsx:44-47`, `FrontDeskView.tsx:48-51`). | CODE_REVIEW |
| W17 | LOW | Eleven components over ~150 lines (WalkInPanel 398, StaffBookingForm 373, CalendarGrid 327, …) — extract WalkInPanel's quick-add-cat block first; `WaitlistSection.tsx:46-52` redundant nested ternary; `FrontDeskView` 10 s highlight `setTimeout` never cleared. | CODE_REVIEW |
| W18 | LOW | Tests: mobile-360 E-E1's first test asserts only the heading; duplicate React keys in U-C2 (`StaffBookingForm.tsx` slot key `startsOn|roomId` ignores `performerId`); E-D2 takes 33–34 s (over the 30 s default); cold-start sign-in 404 race in the e2e fixture. | TEST |
| W19 | LOW | Room override on staff booking (§11.3) not built — no free-rooms endpoint exists. | build (Amended 4) |
| W20 | LOW | Integrator: run the real-API column of every E-case; the inbox legs of E-A1/E-B2/E-B3 are now satisfiable on master (HO-NTF-002 merged); E-A2 needs a genuine two-customer race; E-B3 clinic pass and E-E2 leave-approval flagging. | TEST / ACCEPTANCE |

## Bug-fix batch ho/BUGS-APT-BE (combined review PASS — `docs/gates/BUGS-APT-BE-REVIEW.md`)

Behaviour change worth a release note: by-id staff appointment actions are now scoped to the active branch (row 8) — an unassigned admin must pick a branch when more than one is active, or gets 403.

| # | Severity | Item | Source |
|---|---|---|---|
| BA1 | MEDIUM | #25 `IsCurrentOwnerAsync` (ClinicCommandHandlerSupport.cs) returns true when the cat cannot be loaded — fails open; harmless while nothing hard-deletes, but a soft-deleted cat makes `GetByIdAsync` null. Revisit when cat deletion is built. | review |
| BA2 | MEDIUM | #31 `TryCommitExpiryAsync` (StaleHoldReleaser.cs:798) swallows any `DbUpdateConcurrencyException` from a full `SaveChanges`; rethrow unless `ex.Entries` holds the stale hold entry. | review |
| BA3 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in the BUGS-MIGRATIONS merge.** Was — #5: `AppointmentStatusChange.cs:6` comment still says history is bounded at `MaxStatusHistoryEntries` (constant now test-only) — fix comment or remove it. | review |
| BA4 | LOW | #4: the no-op second `UpdateAsync` pattern is easy to forget in the next two-root single save — audit other multi-root handlers and document it in `.ai/domain-events-and-outbox.md`. | review |
| BA5 | LOW | #8: once a second branch exists, check admin screens that deep-link an appointment without `X-Branch-Id`. #27/#28 IP partitioning relies on `RemoteIpAddress` behind proxies (same as existing policies). | review |

## Bug-fix batch ho/BUGS-APT-WEB (combined review PASS — `docs/gates/BUGS-APT-WEB-REVIEW.md`; its three MEDIUMs fixed in b0b8a4e)

W18's product part (duplicate slot keys) is fixed in 1b622b0; W18's test items stay open.

| # | Severity | Item | Source |
|---|---|---|---|
| BW-A1 | LOW | `zonedLocalToInstant` has no stated policy for nonexistent/ambiguous local times (New York resolves forward, London back; ambiguous → earlier in NY, later in London) — deterministic and shown in the confirm sentence; document the policy and test those exact times. | review |
| BW-A2 | LOW | `ActiveBranchZoneGate`: if `GET /staff/branches/mine` fails it says "Choose a branch" instead of an error. | review |
| BW-A3 | LOW | The disabled Reschedule button on the customer detail shows no reason while services load. | review |
| BW-A4 | LOW | The W8 test relies on a real 2.5 s hold and a 6 s wait — may flake under load. | review |

## BUGS-MIGRATIONS combined review (2026-10-07)

| # | Severity | Finding | Source |
|---|---|---|---|
| BM2 | LOW | `20261006184955_AppointmentStatusChangeSequence` backfill breaks ties among legacy rows with identical `changed_on` by random Guid id, so their order is arbitrary (best effort, accepted). | CODE_REVIEW (`docs/gates/BUGS-MIGRATIONS-REVIEW.md`) |
| BM4 | LOW | `AppointmentStatusChange` ctor defaults `sequence = 0`, which can hide a caller forgetting to pass it; make it required. | CODE_REVIEW |
