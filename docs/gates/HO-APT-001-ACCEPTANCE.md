GATE: ACCEPTANCE
HANDOVER: HO-APT-001
COMMITS: branch ho/APT-001, HEAD 3fe176e. This pass reviewed `git diff e892fb2 HEAD` (e892fb2 records the second pass): 4e0b129 (tests and TC sheet only) and 3fe176e (docs/backlog/APT.md row 47). Earlier passes reviewed the full branch 2bdc066..e892fb2: the first pass 2bdc066..17ed7f0, the second b67769d..c7b0c73.
DATE: 2026-09-29
STATUS: PASS

THIRD PASS. This record replaces the second-pass record (committed e892fb2), which was a FAIL on
one HIGH finding. A12 and A13's integration evidence assumed that a single global
`NotificationWorkCycle.RunDeliveryPassAsync` would reach each test's own rows. On the shared,
never-truncated database of §16.1 that pass takes only the 50 oldest rows. I-C6, I-D7 and I-D5
failed on demand under a backlog left by other test classes.

That HIGH is RESOLVED by 4e0b129, which changes tests only. All 22 §18 criteria are now MET. Some
are partial, with named halves carried, unchanged from earlier passes. No BLOCKER or HIGH findings
remain.

Owner rule (2026-09-27): only BLOCKER/HIGH findings fail a gate. MEDIUM/LOW items are listed here
for docs/backlog/APT.md; this gate has not edited that file.

PRIOR GATES (read from docs/gates/ and docs/test-cases/ at HEAD)
- ARCHITECTURE: PASS (third pass, acc5d47).
- SECURITY: PASS (second pass, 9af0d22). The row-8 ruling stands: by-id staff actions must be branch-scoped before a second branch is activated.
- CODE_REVIEW: PASS (second pass, 6d990c0).
- CODE_REVIEW (DELTA f516cda): PASS. The product code has not changed since then.
- TEST: PASS (recorded c0e3ed9). docs/test-cases/TC-HO-APT-001.md was updated in 4e0b129: 59 PASS / 33 BLOCKED / 1 FAIL (X-d).
  - Each of the eleven newly passing rows names its test and is marked as added after the TEST gate. I checked each one against the test source (see CRITERIA).
  - The 33 BLOCKED rows are not counted as passes. They are the carried halves listed under A2-A6, A8, A9, A11, A16 and A17.
  - X-d remains FAIL on the sheet, but its test passes at HEAD (see LOW 2).

WHAT CHANGED SINCE THE SECOND PASS (`git diff e892fb2 HEAD`, 6 files)
- `git diff e892fb2 HEAD --name-only -- src` is **empty**, so no product code changed. The files are four `tests/**` files, the TC sheet and backlog row 47.
- New helper `AppointmentsIntegrationTestBase.RunNotificationDeliveryUntilAsync(apiFactory, isDone, maxPasses = 200)`:
  - It runs one real delivery pass, then evaluates `isDone` in a fresh scope with `AsNoTracking`.
  - It repeats until `isDone` is true. After 200 passes it calls `Assert.Fail`, so it never succeeds silently.
- The five exposed tests (I-C6, I-D4, I-D5, I-D6, I-D7) now call this helper instead of a fixed number of passes. Each predicate targets the test's own rows: `RecipientId == customerId`, `SubjectId == appointmentId`, or a captured notification id.
- `AppointmentDetailStatusHistoryTests` now advances the fake clock by one minute between transitions.
  - Under a fixed clock, `ChangedOn` values tie, and `ClinicDetailProjection` sorts by `ChangedOn` only, so the order of tied rows is arbitrary.
  - The product tie-breaker gap is recorded as backlog row 47 (LOW). The assertion itself is unchanged.
- The ID7 comment now reads 01:00Z (backlog row 45 is resolved in substance).

CAN THE HELPER MASK THE QUIET-HOURS ASSERTIONS? No. I read the helper and each call site, and the reasoning below is backed by the runs.
- **Extra passes cannot deliver a held row early.** The helper never moves the clock. At a fixed fake "now", extra passes can only process rows that are already due:
  - `GetPendingRequestIdsAsync` filters on `SendAfter <= now`.
  - `GetDueNotificationIdsAsync` filters on `Queued && NotBefore <= now` and `RetryPending && NextAttemptOn <= now`.
  So a row deferred for quiet hours cannot become Sent inside a loop.
- **I-D7 (AppointmentBookingFlowTests.cs):**
  - At 01:05Z (07:05 Dhaka) the loop's predicate is only "the reminder notification exists". It does not wait for Sent.
  - After the loop, the test still asserts `status == "Queued"` and a non-null `deferredUntil` from `GET /api/admin/notifications`, before the clock moves to 02:05Z.
  - The confirmation loop waits for Sent at 22:30 Dhaka. If the confirmation were wrongly held for quiet hours, it could never become Sent at that instant, so the loop would reach `Assert.Fail`.
- **I-D5 (AppointmentReminderFlowTests.cs):**
  - At 20:05Z (02:05 Dhaka) the predicate is "the reminder notification exists". The test then asserts `Status == Queued` and `NotBefore != null`, and captures `oldReminderId`, before the reschedule and before the clock moves.
  - The final loop waits on that specific id reaching `Cancelled`. If the guard failed and the reminder were Sent, the loop would reach `Assert.Fail`.
  - The `CloseReason == SubjectNoLongerApplies` assertion is kept.
- **I-D6:** the loop runs until no reminder request is still `Pending`. Afterwards the test still asserts that none was ever Sent and that all are `Suppressed` with `SubjectNoLongerApplies`. A reminder that expanded instead would fail those assertions.
- **I-D4:** the loop runs until the 24 h request is `Expanded`. The 2 h request cannot be pulled forward, because its `SendAfter` is 22 h in the future. The test then asserts the 2 h request is `Pending` and exactly one reminder is `Sent` (see LOW 1 for a residual gap).
- **I-C6:** the loop runs until this customer's InApp `appointment.confirmed` exists. InApp copies are created at expansion (`ExpandNotificationRequestCommandHandler.cs:195-204`). The test then asserts the copy through `GET /api/admin/notifications`.

COMMANDS RUN (worktree D:/my-repo/cat_ecommerce-wt/APT-001; CATECOMMERCE_TEST_DB=catecommerce_apt001 only, catecommerce_dev never touched; no source edited)
- `dotnet build CatEcommerce.sln`: 0 warnings, 0 errors.
- **Reproduction recipe, round 1 (my own, independent of the developer's runs):** `AppointmentMoveConflictTests` + `AppointmentFlaggingTests` 3 times (10/10 each), then I-C6, I-D7, I-D5, I-D6 and I-D4 in one run: **5/5 passed**.
- **Reproduction recipe, round 2:** same backlog build (3 x 10/10), then the same five tests: **5/5 passed**.
- **Harder variant, each test run alone.** Running the five together lets the first test drain the backlog for the others, so I rebuilt the backlog (3 x 10/10) before each test and ran it alone: I-D5 1/1, I-D7 1/1, I-D4 1/1, I-D6 1/1. (I-C6 alone was covered by rounds 1 and 2, where it runs first.)
  - For comparison, at e892fb2 this same recipe failed I-C6 alone, and failed I-D7 and I-D5.
- **Wide filter** `Appointment|Waitlist|UniqueConstraintExceptionFilter|CareReminder|Notification`: run twice, 102/102 both times.
- `AppointmentDetailStatusHistoryTests` alone: 4 runs, 4/4 passed.
- **Full solution** `dotnet test CatEcommerce.sln --no-build`: Domain 456/456, Application 125/125, Infrastructure 87/87, **Integration 573/573**.

CRITERIA (HO-APT-001 §18)

| # | Result | Evidence |
|---|---|---|
| A1 | MET (by HO-ADM-001) | Unchanged. docs/gates/HO-ADM-001-ACCEPTANCE.md records A8 as MET on I-D1 `CreateServiceAndPrice_PublicServicesShowsCurrentPriceOnly`. |
| A2 | MET (HTTP half carried) | Unchanged. U-B1 `AppointmentSlotFinderTests.FindSlots_NoQualifiedPerformer_ReturnsEmpty`. I-B1 carried. |
| A3 | MET (HTTP half carried) | Unchanged. U-B2, U-B4, U-B5, U-B11. I-B2 and I-B3 carried. |
| A4 | MET (composed) | Unchanged. BranchCalendar, StaffAvailability and slot-finder tests, plus pass-through wiring read in source. U-B3 and I-B2 carried. |
| A5 | MET-partial | Unchanged. I-C2 and `AppointmentStaleHoldReleaseTests`. I-C1 and I-C3 carried. |
| A6 | MET-partial | Unchanged. U-A6 and `AppointmentStaleHoldReleaseTests`. I-C3 and I-C5 carried. |
| A7 | MET | Unchanged. I-C2 `PlaceHold_TwentyConcurrentRequestsForSameSlot_ExactlyOneSucceeds` and I-F4 `IF4_TwoConcurrentMovesIntoTheSameSlot_ExactlyOneSucceeds`: 7/7 each in the second pass, and passed again in both wide runs and the full suite this pass. |
| A8 | MET-partial | Unchanged. `Reschedule_IntoSlotWithExpiredHold_Succeeds` and U-A7. I-D1 and I-D3 carried. |
| A9 | MET (reschedule HTTP half carried) | Unchanged. I-D2 `Cancel_PastCutoff_ReturnsChangeCutoffPassed`. |
| A10 | MET | Unchanged. U-C1 and I-E1. |
| A11 | MET (HTTP half carried) | Unchanged. U-C2, U-C3 and I-E1. I-E2 carried. |
| A12 | **MET** | Three tests, all through the real delivery pass, now reliable under a shared-database backlog (rounds 1 and 2, the per-test variant, and the full suite). **I-C6** `PlaceHold_ThenConfirm_Booked_PriceSnapshotMatchesCurrentPrice`: the `appointment.confirmed` request is committed with the confirm, and after the delivery passes the InApp copy appears in `GET /api/admin/notifications`. **`StaffBooksAppointment_EnqueuesConfirmedNotification`**: a staff booking over HTTP also commits the request. **I-D7** `ID7_StaffBooksDuringQuietHours_...`: a staff booking at 22:30 Dhaka gets a non-InApp confirmation `Sent` at that same instant (Urgent). Its 2 h reminder is `Queued` with `deferredUntil` inside quiet hours, and `Sent` after 08:00. **§19 provider caveat, recorded and not passed silently:** outside Development, delivery is in-app only until HO-NTF-001 OQ-1 is resolved. I-D7's non-InApp "Sent" comes from the Testing environment's provider. |
| A13 | **MET** | **U-D1** `ClinicNotificationsTests` (4/4): bookings 30 h, 3 h and 90 min ahead get 2, 1 and 0 reminders; `SendAfter` is start minus 24 h and minus 2 h; the stamp is `ScheduleStamp`. **U-D2** `AppointmentReminderGuardTests` (5/5): valid for the same stamp; invalid for the old stamp after a reschedule, valid for the new one; invalid when cancelled; invalid for a wrong recipient or a missing appointment. **I-D4**: at T-6h the 24 h reminder is Expanded and Sent, and the 2 h reminder is still Pending. **I-D5**: after a reschedule, the old reminder (held for quiet hours) ends `Cancelled` with `SubjectNoLongerApplies`, and new requests exist at the new T-24h and T-2h. **I-D6**: `appointment.cancelled` is Sent, both reminder requests end `Suppressed` with `SubjectNoLongerApplies`, and none is ever Sent. The I-D tests go through real HTTP and the real delivery pass and are reliable under backlog (see A12). |
| A14 | MET | Unchanged. `AppointmentMoveConflictTests`: I-F2 (six tests), I-F3 (409 `StaleCalendarVersion`) and I-F4 (exactly one 200). The looser oracle for the I-F4 loser is backlog row 43. |
| A15 | MET | Unchanged. I-F2 asserts HTTP 400 and the specific code for all six refusals. The three busy codes also assert the conflicting local range "14:00"/"14:30" (f516cda, DELTA PASS). |
| A16 | MET (merge half carried) | Unchanged. U-A4 and I-E5. I-F9 carried. |
| A17 | MET | Unchanged. I-G1 and I-G3 through a real `ClinicWorkCycle`. U-E1 carried. |
| A18 | MET | Unchanged. I-G3. |
| A19 | MET | Unchanged. I-E3. |
| A20 | MET | Unchanged. U-A1, U-A2 and I-E4. |
| A21 | MET | Unchanged. `AppointmentFlaggingTests` through the real HO-ADM-001 endpoints. I-F6: approving leave flags both appointments `PerformerOnLeave`, both appear in `/flagged`, and cancelling the leave clears both. I-F7: a branch closure flags the appointment `BranchClosed`. |
| A22 | MET | Unchanged. I-C6 asserts `priceAmount == 500.00`. No deposit or payment exists, and the product code is unchanged since the second pass. |

The previously MET criteria are unaffected: no file under src/ changed since e892fb2, and the full suite passes 573/573.

FINDINGS
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/AppointmentReminderFlowTests.cs (I-D4) — the loop stops once the 24 h *request* is `Expanded`. The test then requires the resulting notification to already be `Sent`. Expansion and delivery run in the same pass, but delivery is also capped at the 50 oldest due notifications. A backlog of 50 or more foreign *due notifications* (as opposed to pending requests) could still leave it Queued. This was not reproduced: the recipe builds a request backlog, and I-D4 passed under it. To close the gap, loop until that notification is `Sent`. This is safe, because the 2 h request's `SendAfter` is 22 h away and cannot be pulled forward.
- [LOW] docs/test-cases/TC-HO-APT-001.md X-d and X-g (lines 40, 144, 147) — still record X-d as FAIL, and X-g's full-suite result as 557/558. The X-d test (`AppointmentDetailStatusHistoryTests`) passes at HEAD: the handler was fixed in a5cf718 and the clock advance added in 4e0b129; 4/4 alone, and 573/573 in the full suite. These are historical TEST-gate rows, but the sheet now reads as if a failure is outstanding. Add a post-fix note, as the 2026-09-29 update block does for the other rows.
- Carried unchanged, not re-raised: backlog rows 8, 12, 35-38, 40-44, 46 and 47.
  - Row 45 (the ID7 comment) is fixed in 4e0b129 and can be closed.
  - Row 47 (no tie-breaker in the statusHistory ordering) is correctly placed as LOW. The test works around it, and production transitions carry distinct real timestamps.

OUT-OF-SCOPE CHECK (§4, §14, §20)
- The e892fb2..HEAD delta is tests, the TC sheet and backlog only. It adds no functionality and touches none of §14's forbidden paths.
- §20: no condition was hit. §20.3 does not apply to the new loop, because no double-booking test (I-C2, I-F4) uses retries or delays. The loop is confined to notification delivery passes, which are §16's own mechanism ("tests call NotificationWorkCycle").

UNDECLARED DEVIATIONS
- None. The helper and the status-history clock change are explained in their own doc comments and in the TC sheet's 2026-09-29 update block, and the product gap they expose is recorded as row 47.

SCOPE CHECK
- files in allowed scope: 6/6 in e892fb2..HEAD (4 `tests/**`, the TC sheet, docs/backlog/APT.md).
  - Earlier passes: 132/137 for 2bdc066..17ed7f0, and 11/11 for b67769d..c7b0c73.
- files outside allowed scope: none new. The 5 undeclared files from the first pass (backlog row 12, MEDIUM, non-blocking) are unchanged.
- This gate wrote only this file, which is left uncommitted. No source, test, handover, backlog or `.ai/` file was edited. The tests ran against catecommerce_apt001 only.
