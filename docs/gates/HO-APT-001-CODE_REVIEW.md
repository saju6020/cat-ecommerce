GATE: CODE_REVIEW
HANDOVER: HO-APT-001
COMMITS: 6ef788b..6d990c0 (branch ho/APT-001; `git diff 6ef788b HEAD --stat` — 8 files, 438
insertions(+), 11 deletions(-): `docs/handover/HO-APT-001.md`, `ClinicWorkCycle.cs`,
`AppointmentVisitCommandHandlers.cs`, `ClinicQueries.cs`, `AvailabilityQueryHandlers.cs`,
`WaitlistWorkerQueryHandlers.cs`, `AppointmentsIntegrationTestBase.cs`, `WaitlistFlowTests.cs`)
DATE: 2026-09-28
STATUS: PASS

This is the **second pass**. First pass (`docs/gates/HO-APT-001-CODE_REVIEW.md`, committed 6ef788b)
failed on two HIGHs: (1) waitlist "today" computed once in UTC against branch-local `LocalDate`;
(2) zero integration coverage for the waitlist HTTP surface and the `ClinicWorkCycle` waitlist pass.
Reviewed `git diff 6ef788b HEAD` in full against fix commits de97463 and 6d990c0, re-read the
relevant slice of `.ai/decisions/adr.md` (ADR-045) and the first-pass gate record, and ran the
suites below. Did not re-raise `docs/backlog/APT.md` rows 1-33.

Commands run (worktree `D:/my-repo/cat_ecommerce-wt/APT-001`, `CATECOMMERCE_TEST_DB` pointed at
`catecommerce_apt001`, never `catecommerce_dev`):
- `dotnet build CatEcommerce.sln --no-incremental` — 0 warnings, 0 errors.
- `dotnet test tests/CatEcommerce.Domain.Tests --no-build` — 456/456 passed.
- `dotnet test tests/CatEcommerce.Application.Tests --no-build` — 121/121 passed.
- `dotnet test tests/CatEcommerce.Infrastructure.Tests --no-build` — 82/82 passed.
- `dotnet test tests/CatEcommerce.IntegrationTests --no-build --filter "FullyQualifiedName~Clinic|FullyQualifiedName~Appointment|FullyQualifiedName~Waitlist|FullyQualifiedName~UniqueConstraintExceptionFilter|FullyQualifiedName~CareReminder"` — 34/34 passed (the first pass's 27 plus the 7 new `WaitlistFlowTests`).
- `dotnet test tests/CatEcommerce.IntegrationTests --no-build` (full suite, time allowed) — 557/557 passed. `AdminRosterLeaveEndpointTests.TwoOverlappingLeaveRequests_SentConcurrently_OnlyOneSucceeds` (HO-ADM-001, out of this handover's scope) did **not** flake in this run — observation only, not a finding here.

All commands ran cleanly; no shell-tool limitation to report.

Both first-pass HIGHs verified fixed:

1. **Branch-local "today" for the waitlist Close pass.** `GetPastWaitlistEntriesQuery` now carries
   `Now` (`DateTimeOffset`) instead of a precomputed `DateOnly`; `ClinicWorkCycle.RunAsync` passes
   `timeProvider.GetUtcNow()` straight through, no longer computing a UTC date itself.
   `GetPastWaitlistEntriesQueryHandler` joins every `Waiting` row to its own branch's `TimeZoneId` in
   one round trip (`join b in db.Set<BranchAggregate>() on w.BranchId equals b.Id`, projected to
   `{Id, LocalDate, TimeZoneId}` — no N+1 over rows or branches, confirmed by reading the query, not
   just the comment), materializes that projection, then compares each row against
   `BranchCalendar.LocalDateOf(query.Now, c.TimeZoneId)` — the same helper (and the same
   `TimeZoneInfo.ConvertTime`-based, DST-correct implementation, unchanged in this diff) every other
   branch-local "today" computation in this codebase already uses. The projection query itself (join
   + `Select` to three scalar columns) translates to SQL cleanly — confirmed both by reading the LINQ
   (nothing in it requires client evaluation) and by the new `Close_NonUtcBranchNearLocalMidnight_EntryNotYetPast_StaysWaiting`
   test actually running it against Postgres and passing. The comparison against
   `BranchCalendar.LocalDateOf` — which cannot run inside SQL — happens only after `ToListAsync()`,
   on an in-memory `List<>`, so there is no silent client-evaluation warning/exception risk; EF never
   sees that call. The DST/midnight boundary case the first pass's own reproduction used
   (`Pacific/Honolulu`, UTC-10, 30 minutes left in the branch's local day) is now covered by a
   dedicated regression test and passes; the fix comment records that this same test was proven to
   fail against the reverted pre-fix logic during development.
2. **Waitlist integration coverage.** `tests/CatEcommerce.IntegrationTests/Endpoints/WaitlistFlowTests.cs`
   adds 7 tests, all driving the real `CustomerWaitlistController` HTTP endpoints and a real
   `ClinicWorkCycle.RunAsync` pass via the new `RunClinicWorkCycleAsync` test-base helper — never
   `OfferWaitlistSlotCommand` dispatched directly. Verified by reading, not just the test names:
   - `IG1_FilledDay_JoinThenOwnerCancels_RunPass_OffersTheWaitingEntry` — fills the day's one slot,
     joins, cancels, runs the pass, asserts the entry is `Offered` with a hold in `Held` status and
     the `waitlist.slot-offered` notification enqueued.
   - `IG2_OfferedCustomer_Confirms_BooksTheAppointment_AndClaimsTheEntry` — drives the real
     `/confirm` endpoint, asserts `Booked`/`Claimed`.
   - `IG3_UnclaimedOffer_Lapses_AndTheNextEntryIsOfferedTheSameSlot` — two entries queued by
     `CreatedOn`, advances the fake `TimeProvider` past the claim window, runs the pass twice, asserts
     the first is `Lapsed` and the second is `Offered` the same slot — genuinely exercises queue
     order and the lapse→re-offer path, not just one branch of it.
   - `IG4_CancellationDuringQuietHours_NoOfferUntilQuietHoursEnd` — cancels at 23:00 branch-local
     (inside the default 21:00-08:00 quiet window), runs the pass, asserts the entry is still
     `Waiting` (not offered), advances past quiet hours, runs again, asserts `Offered`. Traced the
     quiet-hours check into `OfferWaitlistSlotCommandHandler` (`quietHoursCalendar.NextAllowedInstantAsync`
     gates the offer before slot-finding) — this is the actual production code path, not a stub, so
     the test is not bypassing quiet hours to get a pass.
   - Plus `Join_ThenListOwnEntries`, `Withdraw_MarksTheEntryWithdrawn`, and the
     `Close_NonUtcBranchNearLocalMidnight...` regression (finding 1, above).
   All 7 use `Factory.WithFakeTimeProvider` and advance time explicitly (`timeProvider.SetUtcNow`)
   rather than relying on wall-clock timing, so they are deterministic; confirmed by two consecutive
   full runs producing identical pass counts.

Two related MEDIUM fixes bundled into the same commit (`RegisterWalkInCommandHandler`,
`GetBookingPerformersQueryHandler` now load the branch and use `BranchCalendar.LocalDateOf` instead
of `DateTimeOffset.UtcNow`'s date) were checked for the two things this second pass was specifically
asked to rule out: each adds exactly one `branchRepository.GetByIdAsync` call, matching the existing
sibling pattern (`CheckInAppointmentCommandHandler`, `GetAvailableSlotsQueryHandler`) — no loop, no
N+1; and neither handler's response shape changed (only the `today` value feeding
`AppointmentSchedulingInputRequest` changed).

`docs/handover/HO-APT-001.md` §1 Amended (11) accurately records both fixes and the new test file;
no other file changed since the first pass (`git diff 6ef788b HEAD --stat` matches the two fix
commits' own file lists exactly, nothing extra).

FINDINGS

- No BLOCKER or HIGH.

- [MEDIUM] `src/Read/CatEcommerce.Read/QueryHandlers/Clinic/WaitlistWorkerQueryHandlers.cs:56-73`
  (`GetPastWaitlistEntriesQueryHandler`) — the branch-local comparison this pass added cannot run in
  SQL, so the handler now pulls **every** `Status == Waiting` row system-wide (joined to its branch)
  into memory before filtering by date and applying `Take(WaitlistRules.MaxClosedPerPass)` — the
  `Take` that used to bound the query at the database is now applied client-side, after the full scan.
  `ClinicWorker` polls this on a fixed interval, so this runs on every tick. Not a new architectural
  violation — `GetWaitlistOfferCandidatesQueryHandler` right above it in the same file already does
  the identical "materialize everything, then group/filter/take in memory" for the same underlying
  reason (`BranchCalendar` conversion can't translate to SQL) and has passed three prior ARCHITECTURE
  gate reviews with that shape intact — but worth bounding before it matters: a coarse, translatable
  SQL-side predicate (e.g. `w.LocalDate <= <UTC-date-based upper bound with a ±1-day margin>`) would
  cut the materialized set to entries that could plausibly be past-due in any time zone, before the
  precise per-branch check runs in memory. Route to `docs/backlog/APT.md`; not blocking.

Everything else re-checked from the first-pass record still holds (status machine, `SlotClaim`/
`StaleHoldReleaser`/`CustomerHoldSerializer`, the exclusion-constraint migrations, outbox-before-save
ordering, no superseded-architecture residue) — this pass did not re-derive those, only the two
fixed files and their blast radius.

SCOPE CHECK
- files in allowed scope: 8/8 (all match the two fix commits' own stated file lists; §1 Amended (11)
  records both).
- files outside allowed scope: none new. The 5 already carried as backlog row 12 (MEDIUM,
  ARCHITECTURE 1st pass) are unchanged by this diff and not re-raised.
