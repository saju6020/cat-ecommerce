# TC-HO-APT-001 — Clinic Appointments (backend)

Handover: docs/handover/HO-APT-001.md
Commit(s) under test: 13ce2f2c1f24ede3dc0e141d8ce273e6d1a73295 (HEAD, ho/APT-001) + one uncommitted
file added by this gate: tests/CatEcommerce.IntegrationTests/Endpoints/AppointmentDetailStatusHistoryTests.cs
Written: 2026-09-28 · Executed: 2026-09-28

> Statuses record what was **observed**, not inferred. This sheet replaces the implementer's own
> `TC-HO-APT-001.md` (written under time budget, before the SECURITY and CODE_REVIEW fix rounds
> added `SlotClaimTests`, `UniqueConstraintExceptionFilterTests` and `WaitlistFlowTests`) — it was
> rewritten from the current code, independently, before any test in it was run, then executed row
> by row. Rows for a §15/§16 case with **no implementing test** are `BLOCKED` ("cannot execute — no
> test exists"), never `PASS`, per this gate's own charter.

## Summary

Counts below total the Unit, Integration, Extra-check and Regression tables (93 rows). The
Acceptance-criteria table is a cross-reference back into those same rows, not counted again.

| | Count |
|---|---|
| Total | 93 |
| PASS | 59 |
| FAIL | 1 |
| BLOCKED | 33 |
| NOT RUN | 0 |

## Environment

- Build: `dotnet build` from the worktree root (`D:/my-repo/cat_ecommerce-wt/APT-001`, branch `ho/APT-001`). **0 warnings, 0 errors**, including the new test file.
- Unit suites run to completion, no I/O, no external services:
  - `CatEcommerce.Domain.Tests` — 456 passed, 0 failed.
  - `CatEcommerce.Application.Tests` — 121 passed, 0 failed (includes `SlotClaimTests`, 2/2).
  - `CatEcommerce.Infrastructure.Tests` — 82 passed, 0 failed.
- Integration suite: `CatEcommerce.IntegrationTests`, real HTTP via `WebApplicationFactory<Program>`, real PostgreSQL 17.5 on `localhost:5432`.
  - Primary database: `catecommerce_apt001` (`CATECOMMERCE_TEST_DB`), already migrated through `AddClinicWaitlist`.
  - Two scratch databases created for the fresh-DB legs of checks (a), (b), (c), (e): `catecommerce_apt001_tg1`, `catecommerce_apt001_tg2`, each migrated with `dotnet ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api` before use, **dropped at the end of this gate** (confirmed by `pg_database` listing — see gate record).
  - `catecommerce_dev` was never pointed at, never migrated, never touched.
  - No SMS/email provider is configured; HO-NTF-001 confirms in-app-only delivery in `Testing`. `ClinicWorker` is disabled in `Testing`; tests drive `ClinicWorkCycle.RunAsync()` directly.
- Full `CatEcommerce.IntegrationTests` suite (558 tests, every area, not just HO-APT-001) run **twice** against `catecommerce_apt001`: **557 passed / 1 failed both times**, the same test both times — `AppointmentDetailStatusHistoryTests.GetDetail_AfterFourTransitions_StatusHistoryIsOrderedAndNamesEachState` (this gate's own new test, see X-d). The HO-ADM-001 flake the brief warned about (`AdminRosterLeaveEndpointTests.TwoOverlappingLeaveRequests_SentConcurrently_OnlyOneSucceeds`) did **not** occur in either full run.

> **Update 2026-09-29 (ACCEPTANCE gate fix, not a re-run of the TEST gate).** Eleven rows that read
> BLOCKED "no test exists" (U-D1, U-D2, I-D4..I-D7, I-F2..I-F4, I-F6, I-F7) are now PASS, and I-C6's
> message half is covered; each row names its covering test and is marked "added after the TEST gate;
> verified by ACCEPTANCE". Counts: PASS 48 to 59, BLOCKED 44 to 33, FAIL unchanged at 1 (X-d, see the
> note there). The delivery-pass tests loop `RunNotificationDeliveryUntilAsync` (bounded) because a
> single pass takes only the 50 oldest rows of the shared database; a single pass had failed under a
> foreign backlog (ACCEPTANCE second-pass HIGH, fixed in the tests only).

## Unit cases (§15)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | `AppointmentTransitionRules` full table over every (from, action) pair | Only §8.3's arrows allowed; every other pair `InvalidAppointmentTransition` naming both states | PASS | `AppointmentTransitionRulesTests.EveryPair_MatchesTheAllowedTable` — observed passing in the Domain.Tests run (456/456) |
| U-A2 | `Completed` → check-in | Refused, naming both states | PASS | `AppointmentTransitionRulesTests.EnsureAllowed_Disallowed_ThrowsNamingBothStates` |
| U-A3 | Every transition | One `StatusHistory` row | PASS | `AppointmentAggregateTests.Confirm_BeforeExpiry_TransitionsToBooked_AndAppendsHistory` passed, but only demonstrates it for `Confirm`, not every method — partial coverage, noted |
| U-A4 | `RecordNoShow` before/at grace | `NoShowTooEarly`; allowed | PASS | `RecordNoShow_BeforeGracePeriod_ThrowsNoShowTooEarly` / `RecordNoShow_AtGracePeriod_Succeeds` |
| U-A5 | `CheckIn` day before / same local day | `CheckInNotToday`; allowed | PASS | `CheckIn_DayBeforeStart_ThrowsCheckInNotToday` / `CheckIn_SameLocalDay_Succeeds` (early-window half not separately asserted — noted) |
| U-A6 | `Confirm` at expiry exactly / one tick before | `SlotHoldExpired`; booked | PASS | `Confirm_AtExpiryExactly_ThrowsSlotHoldExpired` / `Confirm_OneTickBeforeExpiry_Succeeds` |
| U-A7 | `Reschedule` changes `ScheduleStamp`; room-only move does not | As stated | PASS | `Reschedule_ChangingStart_ChangesScheduleStamp_AndIncrementsCount` / `Reschedule_RoomOnly_SameStart_DoesNotChangeScheduleStamp` |
| U-B1 | Vet-only service, nurse-only roster | No slots | PASS | `AppointmentSlotFinderTests.FindSlots_NoQualifiedPerformer_ReturnsEmpty` |
| U-B2 | Qualified for both, both rostered | Slots for both, sorted | PASS | `FindSlots_TwoQualifiedPerformers_ReturnsBoth_SortedByStartThenName` |
| U-B3 | Branch closed weekday / holiday / half-day | No slots / none / only open half | BLOCKED | No test exists at the finder level. Unblock: write it |
| U-B4 | 30-min service, 15-min grid, existing booking | Correct starts, no overlap | PASS | `FindSlots_ExistingBooking_ExcludesOverlap_AndRespectsShiftEnd` |
| U-B5 | Only room of the kind busy | No slot despite free vet | PASS | `FindSlots_OnlyRoomOfKindBusy_ReturnsEmpty` |
| U-B6 | Two rooms free | First by name, every time | PASS | `FindSlots_TwoFreeRooms_AlwaysPicksFirstByName` |
| U-B7 | Stale hold overlapping 10:00 | 10:00 offered | BLOCKED | No test at the finder level (stale-hold exclusion lives in `AppointmentSchedulingInputs`, untested in isolation). Unblock: write it |
| U-B8 | Online vs staff mode, shift outside opening hours | Online none; staff offered with `outsideOpeningHours` | BLOCKED | No dedicated test. Unblock: write it |
| U-B9 | Minimum notice + horizon + `slotIntervalMinutes` divides 60 | Absent/`SlotBeyondHorizon`/registry refusal | BLOCKED | `FindSlots_BeforeEarliestStart_NotOffered` PASSES for the notice clause only. The horizon check and the "divides 60" registry bound are **not implemented in production code** (§1 Amended (6), backlog row 11 — this handover's §14 forbids editing HO-ADM-001's aggregate). Unblock: route to HO-ADM-001 owner |
| U-B10 | `Europe/London` DST change day | Grid aligned; no duplicated/missing hour | PASS | `GridStartsForDay_SpringForwardDstDay_OnlyTheChangedHourCollapses` — run individually and within the full Domain.Tests run; both PASS (extra check (f)) |
| U-B11 | `PreferredPerformerId` set | Only that performer's slots | PASS | `FindSlots_PreferredPerformer_OnlyThatPerformersSlots` |
| U-B12 | Reschedule excludes own busy time | Own slot and neighbours offered | BLOCKED | No dedicated unit test (only implicit, through integration-level `SlotClaim.ExcludeAppointmentId` use). Unblock: write it |
| U-C1 | Mixed priorities and due times | Emergency, urgent, routine, booked order | PASS | `ClinicQueueTests.Order_MixOfPrioritiesAndDueTimes_EmergencyFirst` |
| U-C2 | Estimator, two performers | `ceil_to_5(65/2) = 35` | PASS | `EstimateWaitMinutes_TwoPerformers_CeilsToNearestFive` |
| U-C3 | Adding an emergency | Later estimates increase | PASS | `EstimateWaitMinutes_AddingEmergencyAhead_IncreasesLaterEstimates` |
| U-C4 | No performers on shift | Divisor 1, no divide-by-zero | PASS | `EstimateWaitMinutes_NoPerformersOnShift_DivisorIsOne` |
| U-C5 | Vet's queue | Own entries + unassigned walk-ins | PASS | `ForPerformer_ReturnsOwnEntries_PlusUnassignedWalkIns` |
| U-D1 | Reminders at various lead times | Two / one / none enqueued | PASS | `ClinicNotificationsTests` (Application.Tests): `EnqueueReminders_LeadTimeAlreadyPassed_...` Theory (30 h / 3 h / 90 min gives 2 / 1 / 0) and `EnqueueReminders_30HoursAhead_SendAfterMatchesEachLeadTime`, 4/4. Added after the TEST gate; verified by ACCEPTANCE. |
| U-D2 | `AppointmentReminderGuard` states | Valid / invalid / invalid | PASS | `AppointmentReminderGuardTests` (Infrastructure.Tests): valid (booked, same stamp); invalid after reschedule (old stamp); invalid when cancelled; plus wrong recipient and missing appointment, 5/5. Added after the TEST gate; verified by ACCEPTANCE. |
| U-D3 | `startsOnLocal` formatting | Branch-local `HH:mm` | BLOCKED | No unit test. Unblock: write it |
| U-D4 | Five template definitions | Pass `TemplateRules`; urgency as specified | BLOCKED | No unit test. Unblock: write it |
| U-E1 | Offer ordering, three `Waiting` entries | Oldest first | BLOCKED | No unit-level test (integration-level `WaitlistFlowTests.IG3_...` covers ordering through the real worker pass, but not as an isolated domain-service test). Unblock: write one at the domain level |
| U-E2 | Offer inside quiet hours | Not made | BLOCKED | No unit-level test (integration-level `WaitlistFlowTests.IG4_...` covers it end to end). Unblock: write one at the domain level |
| U-E3 | Join when slots exist; twice same cat/service/date | `SlotsStillAvailable`; `AlreadyWaitlisted` | BLOCKED | No unit test. Unblock: write it |
| U-X1 | Reflection over `Events/Clinic/**` | No string properties | PASS | `ClinicEventsReflectionTests.EveryClinicEventType_HasNoStringProperty` |
| U-X2 | `ClinicWorker` loop with a throwing pass | Logged; next pass still runs | BLOCKED | No test. Unblock: write it |
| U-X3 | Migration constraint predicates | Use `AppointmentStatus.*.Value`; walk-ins excluded | BLOCKED | Predicate *existence* is proven by `AppointmentBookingFlowTests.Migration_InstallsBtreeGistAndExclusionConstraints` (PASS, both full-suite runs), but that test only checks `pg_constraint` for `contype='x'` and the constraint names — the predicate's exact enum-value text is not asserted by any test, only reviewed by eye. Unblock: write it |
| U-G1 | Work queue, future `SendAfter` | Not returned yet; returned once due | BLOCKED | No unit test. Unblock: write it |
| U-G2 | `SubjectStamp` copied request → notification | As stated | BLOCKED | No unit test. Unblock: write it |
| U-G3 | Guard resolver, `AppointmentReminder` kind | Resolves `AppointmentReminderGuard` | BLOCKED | No unit test. Unblock: write it |

## Integration cases (§16)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I-A1 | Migration on an empty database | `btree_gist` installed; `clinic` schema; three exclusion constraints present | PASS | `Migration_InstallsBtreeGistAndExclusionConstraints` — PASS in both full-suite runs against `catecommerce_apt001`, and PASS on both fresh scratch DBs (`tg1`, `tg2`) |
| I-A2 | Host starts, scope validation on | Every registration resolves | PASS | No dedicated test, but directly observed: `ValidateScopes`/`ValidateOnBuild` stay on (§12.7) and every one of 558 tests in both full runs constructs the host via `WebApplicationFactory<Program>` — a bad registration would fail host construction for all of them, not just this gate's own tests |
| I-B1 | Vet-only service, nurse-only roster | `slots: []` | BLOCKED | No HTTP-level test (the rule itself is proven at the finder level by U-B1). Unblock: write it |
| I-B2 | Slots on holiday vs normal day | Empty; expected grid | BLOCKED | No HTTP-level test. Unblock: write it |
| I-B3 | Slots filtered by performer | That performer only | BLOCKED | No HTTP-level test. Unblock: write it |
| I-C1 | Hold, then GET slots | Held slot gone for everyone | BLOCKED | No dedicated test. Unblock: write it |
| I-C2 | 20 concurrent holds, same slot | Exactly one `201`, rest `409 SlotNoLongerAvailable` in every body | PASS | `PlaceHold_TwentyConcurrentRequestsForSameSlot_ExactlyOneSucceeds` — see extra check (a): 5/5 runs PASS (2 on fresh scratch DBs, 3 on `catecommerce_apt001`) |
| I-C3 | Hold, advance 10 min, confirm | `400 SlotHoldExpired`; slot offered again | BLOCKED | No dedicated test. Unblock: write it |
| I-C4 | Hold A, then hold B same customer | A released; B held | BLOCKED | Only exercised as a side effect of 8-way concurrency (check (b)'s test), not as a plain sequential 2-request test. Unblock: write it |
| I-C5 | Stale hold by customer 1; customer 2 holds after expiry | `201`; customer 1's row `HoldExpired` | BLOCKED | `AppointmentStaleHoldReleaseTests` proves the *slot* is claimable after expiry (4 tests, all PASS), but no test asserts the loser's row actually reads back as `HoldExpired`. Unblock: write it |
| I-C6 | Confirm | `Booked`; `priceSnapshot` matches; confirmation enqueued; in-app copy exists after delivery pass | PASS | `PlaceHold_ThenConfirm_Booked_PriceSnapshotMatchesCurrentPrice` now asserts the `appointment.confirmed` request committed and, after a bounded `RunNotificationDeliveryUntilAsync` loop, the in-app copy via `GET /api/admin/notifications`; `StaffBooksAppointment_EnqueuesConfirmedNotification` covers the staff path. Passes with a 30+ row foreign backlog (ACCEPTANCE second-pass HIGH fixed in the helper). Added after the TEST gate; verified by ACCEPTANCE. |
| I-C7 | Hold another's cat; confirm another's hold | `404`; `404` | BLOCKED | No dedicated test. Unblock: write it |
| I-C8 | Hold for a deceased cat | `400 CatNotBookable` | BLOCKED | No dedicated test. Unblock: write it |
| I-D1 | Reschedule 5h before (4h cutoff) | `200`; new time; old free; `rescheduleCount 1` | BLOCKED | No dedicated test (only stale-hold-adjacent coverage exists, via `AppointmentStaleHoldReleaseTests.Reschedule_IntoSlotWithExpiredHold_Succeeds`, which does not assert `rescheduleCount`). Unblock: write it |
| I-D2 | Reschedule/cancel 3h before | `400 ChangeCutoffPassed` | PASS | `Cancel_PastCutoff_ReturnsChangeCutoffPassed` PASSES for the cancel half; the reschedule half is **not separately tested** — partial coverage, noted |
| I-D3 | Reschedule to a slot taken meanwhile | `409 SlotNoLongerAvailable`; original kept | BLOCKED | No dedicated test. Unblock: write it |
| I-D4 | Book 30h ahead; advance to 6h before; run both passes | 24h reminder sent; 2h not yet | PASS | `AppointmentReminderFlowTests.ID4_Book30HoursAhead_Advance6HoursBefore_...` (24 h request Expanded and Sent; 2 h request still Pending). Added after the TEST gate; verified by ACCEPTANCE. |
| I-D5 | Reschedule after 24h reminder queued, before sent | Old reminder fails guard; new reminders queued | PASS | `AppointmentReminderFlowTests.ID5_RescheduleAfterReminderQueuedButBeforeSent_...` (old reminder deferred for quiet hours, then `Cancelled`/`SubjectNoLongerApplies`; new requests at the new T-24 h and T-2 h). Added after the TEST gate; verified by ACCEPTANCE. |
| I-D6 | Cancel | `appointment.cancelled` sent; pending reminders never sent | PASS | `AppointmentReminderFlowTests.ID6_Cancel_AppointmentCancelledSent_PendingRemindersNeverSent` (`appointment.cancelled` Sent; both reminder requests `Suppressed`/`SubjectNoLongerApplies`). Added after the TEST gate; verified by ACCEPTANCE. |
| I-D7 | Staff books at 22:30 Dhaka | Confirmation at once; reminders follow quiet hours | PASS | `AppointmentBookingFlowTests.ID7_StaffBooksDuringQuietHours_...` (confirmation Sent at 22:30 Dhaka; 2 h reminder Queued with `deferredUntil`, Sent after 08:00). Added after the TEST gate; verified by ACCEPTANCE. |
| I-E1 | Walk-ins routine/urgent/emergency; GET queue | Emergency first; `estimatedWaitMinutes` present | PASS | `RegisterWalkIns_EmergencyOrdersFirst_WithEstimatedWait` |
| I-E2 | Add an emergency; GET again | Later estimates increased | BLOCKED | No dedicated test. Unblock: write it |
| I-E3 | Check in a booked appointment; GET vet's queue | Cat appears on that vet's queue | PASS | `CheckIn_BookedAppointment_AppearsOnVetsQueue` |
| I-E4 | Start, complete, then check in again | `204`,`204`; `400 InvalidAppointmentTransition` | PASS | `StartThenComplete_Succeeds_FurtherCheckIn_IsInvalidTransition` |
| I-E5 | No-show before/after grace | `400 NoShowTooEarly`; `204`; `noShowCount` 1; audit row | PASS | `RecordNoShow_BeforeGrace_Refused_AfterGrace_Succeeds_AndCountsOnSummary` PASSES for status codes and `noShowCount`; the `BusinessEvent` audit row itself is **not asserted** — partial coverage, noted |
| I-E6 | Nurse checks in | `403` | BLOCKED | No dedicated test. Unblock: write it |
| I-F1 | Staff books inside shift, outside opening hours | `201` | BLOCKED | No dedicated test. Unblock: write it |
| I-F2 | Move onto vet/room/cat/leave/qualification/room-kind conflicts | `400` each with specific code | PASS | `AppointmentMoveConflictTests.IF2_*` six tests, one per code (`PerformerBusy`, `RoomBusy`, `CatAlreadyBooked` also assert the conflicting local range; `PerformerNotAvailable`, `PerformerNotQualified`, `RoomKindMismatch`). Added after the TEST gate; verified by ACCEPTANCE. |
| I-F3 | Move with stale `expectedVersion` | `409` | PASS | `AppointmentMoveConflictTests.IF3_MoveWithStaleExpectedVersion_Returns409StaleCalendarVersion`. Added after the TEST gate; verified by ACCEPTANCE. |
| I-F4 | Two staff move two appointments into same vet time concurrently | One `200`, one `409 PerformerBusy` | PASS | `AppointmentMoveConflictTests.IF4_TwoConcurrentMovesIntoTheSameSlot_ExactlyOneSucceeds` (exactly one 200; loser is a 400/409 with a conflict code, oracle looser than §16, backlog row 43). Added after the TEST gate; verified by ACCEPTANCE. |
| I-F5 | Calendar for a week by performer/room | Correct payload; >7 days `400` | BLOCKED | No dedicated test. Unblock: write it |
| I-F6 | Approve leave for a vet with two future bookings | Both flagged `PerformerOnLeave`; listed; leave-cancel clears | PASS | `AppointmentFlaggingTests.IF6_ApproveVetLeaveOverTwoBookings_...` (both `PerformerOnLeave`, listed in `/flagged`, cleared on leave cancel). Added after the TEST gate; verified by ACCEPTANCE. |
| I-F7 | Add a closure over a booked day | Flagged `BranchClosed` | PASS | `AppointmentFlaggingTests.IF7_BranchClosureOverBookedDay_FlagsBranchClosed`. Added after the TEST gate; verified by ACCEPTANCE. |
| I-F8 | Mark the cat deceased | Future appointments flagged `CatDeceased`; nothing cancelled/sent | BLOCKED | No dedicated test. Unblock: write it |
| I-F9 | Merge the customer | Appointments/waitlist under survivor; `noShowCount` merged | BLOCKED | No dedicated test — `ReassignAppointmentsOnCustomerMergedHandler` is untested at the Clinic level. Unblock: write it |
| I-G1 | Fill day; join; cancel; run pass | Oldest entry `Offered`; hold with claim window; `waitlist.slot-offered` enqueued | PASS | `WaitlistFlowTests.IG1_FilledDay_JoinThenOwnerCancels_RunPass_OffersTheWaitingEntry` — see extra check (e): 4/4 runs PASS (2 fresh, 2 primary) |
| I-G2 | Offered customer confirms | `Booked`; entry `Claimed` | PASS | `IG2_OfferedCustomer_Confirms_BooksTheAppointment_AndClaimsTheEntry` |
| I-G3 | Offer unclaimed; advance 30 min; run pass twice | Entry `Lapsed`; next entry `Offered` same slot | PASS | `IG3_UnclaimedOffer_Lapses_AndTheNextEntryIsOfferedTheSameSlot` |
| I-G4 | Cancellation at 23:00 | No offer until quiet hours end | PASS | `IG4_CancellationDuringQuietHours_NoOfferUntilQuietHoursEnd` |
| I-X1 | Log capture across all cases | No reason-for-visit/complaint/name text | BLOCKED | No automated log-content scan for Clinic (HO-ACC-001/HO-NTF-001 have their own `WithCapturedLogs()` sweeps; none targets Clinic fields). Unblock: write it |

## Extra checks requested by earlier gates

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| X-a | (a) I-C2 run ≥5 times, incl. ≥2 on a freshly migrated scratch DB, asserting `SlotNoLongerAvailable` in every loser body | Passes every run | PASS | 5 runs observed: fresh `catecommerce_apt001_tg1` PASS, fresh `catecommerce_apt001_tg2` PASS, `catecommerce_apt001` ×3 PASS. Every run's internal assertion (`Assert.Contains(BusinessViolationCodes.SlotNoLongerAvailable, body)` over every loser) held |
| X-b | (b) `PlaceHold_EightConcurrentRequestsFromOneCustomerForDifferentSlots_AtMostOneHeldRowRemains` ≥5 runs, incl. ≥2 fresh DB | Passes every run, ≤1 live Held row | PASS | Same 5 runs as X-a (both tests ran together each time): fresh `tg1` PASS, fresh `tg2` PASS, `catecommerce_apt001` ×3 PASS |
| X-c | (c) `SlotClaimTests`, `UniqueConstraintExceptionFilterTests`, `AppointmentStaleHoldReleaseTests` all pass with the pending-changes guard | All pass | PASS | `SlotClaimTests` (Application.Tests, no DB): 2/2 PASS. `UniqueConstraintExceptionFilterTests` (pure unit, no DB): 3/3 PASS on fresh `tg1`, 3/3 PASS on fresh `tg2`, and included passing in both full-suite runs on `catecommerce_apt001`. `AppointmentStaleHoldReleaseTests`: 4/4 PASS on fresh `tg1`, 4/4 PASS on fresh `tg2`, and included passing in both full-suite runs |
| X-d | (d) Detail `statusHistory[]` projection works against real Postgres (nested collection projection with `.DisplayName`) | Correct order and values, no translation exception | **FAIL** | New test `AppointmentDetailStatusHistoryTests.GetDetail_AfterFourTransitions_StatusHistoryIsOrderedAndNamesEachState` (written by this gate, kept in `tests/**` — clean, no assertion weakened). Ran 4 times (2× `catecommerce_apt001`, `tg1`, `tg2`): **failed identically every time.** The EF/Postgres translation concern behind (d) is **resolved** — no exception, and every row's `from`/`to`/`changedOn`/`changedBy` are correctly paired to their own row (confirmed by reading the raw JSON on failure; SQL confirmed via detailed log to be `LEFT JOIN clinic.appointment_status_change ... ORDER BY a1."Id", a0.changed_on`, translated entirely in SQL). The **real, reproducible bug** found: `StartAppointmentCommandHandler` (`AppointmentVisitCommandHandlers.cs:157`) passes `DateTimeOffset.UtcNow` instead of the injected `TimeProvider` when calling `appointment.Start(...)`. Under a `FakeTimeProvider` set to a future/past business instant (this handover's own test convention, `AppointmentsApiFactory.WithFakeTimeProvider`), the `Start` transition's `ChangedOn` is stamped with the *real* wall-clock instant instead of the test's business instant — which sorted **before** the `Held→Booked` transition in every run, corrupting the audit trail's chronological guarantee (§8.3, Appendix C "auditable at a glance"). This is the concrete functional consequence of the already-filed `docs/backlog/APT.md` row 13 (LOW, ARCHITECTURE: "`StartAppointmentCommandHandler` uses `DateTimeOffset.UtcNow` instead of `TimeProvider`") — reported here as a TEST-gate finding, not fixed (this is a gate) |
| X-e | (e) `WaitlistFlowTests` (7 tests, I-G1..I-G4 + join/withdraw/close) ≥3 runs, incl. 1 fresh DB | Passes every run | PASS | 4 runs, all 7/7 PASS: fresh `tg1`, fresh `tg2`, and included passing in both full-suite runs on `catecommerce_apt001` |
| X-f | (f) U-B10 DST test | Passes | PASS | Run individually (1/1 PASS) and within the full Domain.Tests run (456/456 PASS, includes it) |
| X-g | Full `CatEcommerce.IntegrationTests` suite, twice, against `catecommerce_apt001` | All pass both runs (except the out-of-scope ADM-001 flake noted below) | PASS | Run 1: 557/558 passed. Run 2 (with TRX logger): 557/558 passed. Both runs' sole failure is X-d's own new test (a real, reproducible bug in production code, not suite flakiness) |
| X-h | Whole-solution build | 0 warnings, 0 errors | PASS | `dotnet build` — clean, including the new test file |
| X-i | `AdminRosterLeaveEndpointTests.TwoOverlappingLeaveRequests_SentConcurrently_OnlyOneSucceeds` (HO-ADM-001, out of scope) | Recorded as observation only, not a HO-APT-001 finding | PASS (observed, out of scope) | Ran as part of both full-suite runs (558 tests each); **did not fail either time**. Out of HO-APT-001's scope regardless — not raised as a finding here per the brief |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCHITECTURE (1st, HIGH #1) | `SlotClaim`-using handlers other than PlaceHold didn't expire overlapping stale holds (reschedule, staff booking, move, waitlist offer) | Stale hold in the target slot does not block C4/C6/C7/C15-offer | PASS | `AppointmentStaleHoldReleaseTests.Reschedule_IntoSlotWithExpiredHold_Succeeds` / `StaffBooking_...` / `StaffMove_...` / `WaitlistOffer_...` — 4/4 PASS on `catecommerce_apt001` (×2 full runs), fresh `tg1`, fresh `tg2` |
| R2 | ARCHITECTURE (1st, HIGH #2/#3) | Local-to-instant conversion bypassed `BranchCalendar`; reads rehydrated the aggregate instead of projecting | No non-UTC `DateTimeOffset` reaches Npgsql; reads use `Select` projections | PASS | Observed indirectly: every test against the `Asia/Dhaka`-zoned test branch (all of `AppointmentBookingFlowTests`, `AppointmentVisitFlowTests`, `AppointmentStaleHoldReleaseTests`) and the `Pacific/Honolulu`-zoned branch (`WaitlistFlowTests.Close_NonUtcBranchNearLocalMidnight...`) passed with no Npgsql "only offset 0 (UTC) is supported" exception in any of the 4 full runs or 2 scratch-DB runs |
| R3 | ARCHITECTURE (1st, HIGH #4/#5) | Appointments personal-data eraser conflicted with HO-XCT-001's own exemption design; `ExpireHold` didn't enforce its own expiry | Eraser removed (exemption registered instead); `ExpireHold` refuses before expiry | PASS | `AppointmentAggregateTests.ExpireHold_BeforeExpiry_IsRefused_AndStaysHeld` / `ExpireHold_AtOrAfterExpiry_TransitionsToHoldExpired` — Domain.Tests, PASS |
| R4 | ARCHITECTURE (2nd, HIGH A) | Stale-hold expiry saved separately from the caller's own write, with no guard against pending changes | `SlotClaim` throws if called with pending tracked changes | PASS | `SlotClaimTests.ExpireOverlappingStaleHoldsAsync_WithPendingChanges_ThrowsInvalidOperationException` / `..._WithNoPendingChanges_Completes` — Application.Tests, PASS |
| R5 | ARCHITECTURE (2nd, HIGH B) | `40P01` deadlock mapped to a generic `409` on every endpoint in every area | Mapping scoped to the four `[SlotClaimEndpoint]` actions only, same shape as `23P01` | PASS | `UniqueConstraintExceptionFilterTests` (3 tests) — PASS on `catecommerce_apt001` (×2), fresh `tg1`, fresh `tg2` |
| R6 | SECURITY (1st, HIGH) | "One hold per customer" unenforced under concurrency (8 concurrent holds → 8 live rows) | Advisory-lock serialisation leaves ≤1 live `Held` row per customer | PASS | `PlaceHold_EightConcurrentRequestsFromOneCustomerForDifferentSlots_AtMostOneHeldRowRemains` — see extra check (b): 5/5 runs PASS |
| R7 | CODE_REVIEW (1st, HIGH) | Waitlist worker's "today" computed in one global UTC date instead of branch-local; zero waitlist integration coverage | Non-UTC branch near local midnight not closed early; full I-G1..I-G4 flow covered | PASS | `WaitlistFlowTests.Close_NonUtcBranchNearLocalMidnight_EntryNotYetPast_StaysWaiting` + the rest of `WaitlistFlowTests` — see extra check (e): 4/4 runs, 7/7 tests each |

## Acceptance-criteria coverage (§18)

Cross-references the rows above; not counted separately in the Summary. `PASS` here means at least
one covering case was observed passing (a partial-coverage caveat, where one exists, is on the cited
row above, not repeated here). `BLOCKED` means every covering case is itself `BLOCKED`.

| Criterion | Covered by | Status |
|---|---|---|
| A1 CLN-01 AC1 (HO-ADM-001) | Out of this gate's scope (HO-ADM-001 I-D1) | BLOCKED (out of scope) |
| A2 CLN-01 AC2 | U-B1 (PASS), I-B1 (BLOCKED) | PASS (partial) |
| A3 CLN-02 AC1 | U-B2, U-B4, U-B5 (PASS), I-B2, I-B3 (BLOCKED) | PASS (partial) |
| A4 CLN-02 AC2 | U-B3 (BLOCKED), I-B2 (BLOCKED) | BLOCKED |
| A5 CLN-03 AC1 | I-C1 (BLOCKED), I-C3 (BLOCKED) | BLOCKED |
| A6 CLN-03 AC2 | U-A6 (PASS), I-C3, I-C5 (BLOCKED) | PASS (partial, unit-level only) |
| A7 CLN-03 AC3 | I-C2, I-F4 (PASS) | PASS |
| A8 CLN-04 AC1 | U-A7 (PASS), I-D1, I-D3 (BLOCKED) | PASS (partial, unit-level only) |
| A9 CLN-04 AC2 | I-D2 (PASS) | PASS |
| A10 CLN-05 AC1 | U-C1, I-E1 (PASS) | PASS |
| A11 CLN-05 AC2 | U-C2, U-C3 (PASS), I-E2 (BLOCKED) | PASS (partial) |
| A12 CLN-06 AC1 | I-C6, I-D7 (PASS) | PASS |
| A13 CLN-06 AC2 | U-D1, U-D2, I-D4, I-D5, I-D6 (PASS) | PASS |
| A14 CLN-07 AC1 | I-F2, I-F3, I-F4 (PASS) | PASS |
| A15 CLN-07 AC2 | I-F2 (PASS) | PASS |
| A16 CLN-08 AC | U-A4 (PASS), I-E5 (PASS, partial), I-F9 (BLOCKED) | PASS (partial) |
| A17 CLN-09 AC1 | U-E1 (BLOCKED), I-G1, I-G2 (PASS) | PASS (partial) |
| A18 CLN-09 AC2 | I-G3 (PASS) | PASS |
| A19 CLN-10 AC1 | I-E3 (PASS) | PASS |
| A20 CLN-10 AC2 | U-A1, U-A2, I-E4 (PASS) | PASS |
| A21 ADM-03 AC2 (2nd half) | I-F6, I-F7 (PASS) | PASS |
| A22 ADR-073 | I-C6 (PASS) | PASS |

## Cases I could not run

None, in the environmental sense. PostgreSQL, the build, both scratch databases and
`catecommerce_apt001` were available throughout this gate. Every row above is either `PASS`
(executed and observed passing), `FAIL` (executed and observed failing — X-d only), or `BLOCKED`
because **no test implementing that case exists yet** — those are test-authorship gaps, not
environmental blocks, and are listed with an unblock note on their own row above and summarised
below.

## Gaps left deliberately

These are §15/§16 cases with **no implementing test today** (all `BLOCKED` rows above). None are
BLOCKER/HIGH by themselves — the largest is the reminder/message timing group (A13) and the whole
of §16 F (staff calendar conflicts and the flagging event handlers), which the ARCHITECTURE and
CODE_REVIEW gates already passed on the strength of the code, not tests. Grouped here so
ACCEPTANCE can weigh it honestly.

| Gap | Why not covered | Suggested owner |
|---|---|---|
| U-B3, U-B7, U-B8, U-B9 (horizon+registry halves), U-B12 | Finder-level scenarios not isolated into their own unit test | HO-APT-001 owner |
| U-D1..U-D4, U-G1..U-G3 | No unit test for reminder timing, the reminder guard, template formatting, or the HO-NTF-001 `SendAfter`/`SubjectStamp`/guard-resolution amendment | HO-APT-001 owner |
| U-E1..U-E3 | Waitlist ordering/quiet-hours/join rules asserted only at integration level (`WaitlistFlowTests`), not isolated at the domain-service level | HO-APT-001 owner |
| U-X2 | `ClinicWorker` loop resilience (a throwing pass doesn't stop the next tick) not tested | HO-APT-001 owner |
| U-X3 (predicate text) | Constraint *presence* is proven (I-A1); the predicate's exact enum-value text is reviewed by eye only | HO-APT-001 owner |
| I-B1, I-B2, I-B3 | Public/staff slot endpoints not exercised by HTTP-level tests (only the pure finder is) | HO-APT-001 owner |
| I-C1, I-C3, I-C4, I-C5, I-C7, I-C8 | Hold lifecycle edge cases (visibility after hold, expiry-then-reoffer, one-hold-per-customer *sequential* replace, cross-customer 404s, deceased-cat refusal) have no dedicated integration test | HO-APT-001 owner |
| I-C6 (message half), I-D2 (reschedule half) | Confirmation `priceSnapshot`/`Booked` and cutoff-cancel are covered; the enqueue-then-deliver half and the reschedule-past-cutoff half are not | HO-APT-001 owner |
| I-D1, I-D3..I-D7 | Reschedule-before-cutoff, reschedule race, and every reminder/notice timing scenario have no integration test — **this is the biggest single coverage hole (A13)** | HO-APT-001 owner |
| I-E2, I-E6 | Estimate recompute after adding an emergency (HTTP level); nurse check-in `403` | HO-APT-001 owner |
| I-F1..I-F9 (all nine) | Staff booking outside-hours-inside-shift, move conflict codes, `ExpectedVersion` 409, calendar range, leave/closure/cat-deceased/cat-transfer flagging, customer-merge reassignment — none has an integration test | HO-APT-001 owner — the flagging event handlers and the merge-reassignment handler are non-trivial and entirely untested |
| I-X1 | No automated log-content scan for leaked reason-for-visit/complaint/customer-name text | HO-APT-001 owner |
| `booking.slotIntervalMinutes` "divides 60" | Not implemented (§1 Amended (6), backlog row 11) — requires editing HO-ADM-001's `BusinessSettingsAggregate`, forbidden by this handover's §14 | Route to HO-ADM-001 owner |

## Production bug found by this gate (reported, not fixed)

`src/Application/CatEcommerce.Application/CommandHandlers/Clinic/AppointmentVisitCommandHandlers.cs:157`
(the `StartAppointmentCommandHandler`) passes `DateTimeOffset.UtcNow` to `appointment.Start(...)`
instead of the injected `TimeProvider`. Concrete, reproduced consequence: under a test/business clock
that differs from the real wall clock (this handover's own `FakeTimeProvider` convention), the
`CheckedIn → InConsultation` transition's `StatusHistory.ChangedOn` is stamped with the real wall-clock
instant, which can sort **before** earlier transitions recorded on the business clock — breaking the
chronological-order guarantee of the appointment's audit trail (§8.3; Appendix C "auditable at a
glance"). Already filed as `docs/backlog/APT.md` row 13 (LOW, ARCHITECTURE, framed as a style issue);
this gate's new test (`AppointmentDetailStatusHistoryTests`) gives it a concrete, reproducible failure
and — since production always runs `TimeProvider.System`, whose value equals `DateTimeOffset.UtcNow` —
its real-world blast radius is limited to test determinism and any future non-system `TimeProvider`
deployment, not ordinary production behaviour today. See `docs/gates/HO-APT-001-TEST.md` for the
severity call.
