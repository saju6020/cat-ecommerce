GATE: TEST
HANDOVER: HO-APT-001
COMMITS: HEAD at task start `13ce2f2c1f24ede3dc0e141d8ce273e6d1a73295` (branch `ho/APT-001`, CODE_REVIEW
second pass PASS). This gate's own new file, `tests/CatEcommerce.IntegrationTests/Endpoints/AppointmentDetailStatusHistoryTests.cs`,
is uncommitted on top of it, as is the rewritten `docs/test-cases/TC-HO-APT-001.md` — both left for
the integrator/owner, per instructions.
DATE: 2026-09-28
STATUS: PASS

Test-case sheet: `docs/test-cases/TC-HO-APT-001.md`. Rewritten from scratch as an independent
TEST-gate sheet — one row per §15/§16 case plus the extra checks (a)-(i) the earlier three gates
named by name — every row starting `NOT RUN`, before any test in it was executed. Executed after,
one row at a time.

## Counts

| | Count |
|---|---|
| Total (Unit + Integration + Extra checks + Regression) | 93 |
| PASS | 48 |
| FAIL | 1 |
| BLOCKED | 44 |
| NOT RUN | 0 |

Unit: 37 (20 PASS, 17 BLOCKED — no test exists). Integration: 40 (13 PASS, 27 BLOCKED — no test
exists). Extra checks: 9 (8 PASS, 1 FAIL). Regression: 7 (7 PASS). The Acceptance-criteria table
(§18, 22 rows) cross-references these and is not counted again; 15/22 criteria have a passing
covering case (several only partially — noted on the sheet), 7/22 have none.

Suite results, exact:
- `dotnet build`: **0 warnings, 0 errors** (whole solution, including the new test file).
- `CatEcommerce.Domain.Tests`: **456/456 passed**, 0 failed.
- `CatEcommerce.Application.Tests`: **121/121 passed**, 0 failed (includes `SlotClaimTests`, 2/2).
- `CatEcommerce.Infrastructure.Tests`: **82/82 passed**, 0 failed.
- `CatEcommerce.IntegrationTests` (558 tests, every area, not just HO-APT-001) against `catecommerce_apt001`, **twice**:
  - Run 1: **557 passed / 1 failed**.
  - Run 2 (with a TRX logger, to get the exact identity): **557 passed / 1 failed**.
  - **Same single failure both times**: `AppointmentDetailStatusHistoryTests.GetDetail_AfterFourTransitions_StatusHistoryIsOrderedAndNamesEachState` — this gate's own new test (extra check (d)). See Findings.
  - The HO-ADM-001 flake the brief warned about (`AdminRosterLeaveEndpointTests.TwoOverlappingLeaveRequests_SentConcurrently_OnlyOneSucceeds`) **did not occur in either run**. Recorded as an observation only, per the brief — not a HO-APT-001 finding, and not re-run separately since it is out of this handover's scope.

## What this gate did

1. Read `HO-APT-001.md` §15 (unit cases), §16 (integration cases, incl. §16.1's shared-database
   rules), §18 (acceptance criteria), and §1 Amended (1)-(11) in full.
2. Read the three earlier gate verdicts (`docs/gates/HO-APT-001-{ARCHITECTURE,SECURITY,CODE_REVIEW}.md`)
   for exactly what each named as owed to TEST — `SlotClaimTests`, `UniqueConstraintExceptionFilterTests`,
   the pending-changes guard on `AppointmentStaleHoldReleaseTests`, and the `statusHistory[]`
   nested-projection concern — and `docs/backlog/APT.md` rows 1-34 for context on open MEDIUM/LOW items.
3. Enumerated every test method under `tests/CatEcommerce.Domain.Tests`, `tests/CatEcommerce.Application.Tests`
   and `tests/CatEcommerce.IntegrationTests` that touches Clinic code, and mapped each §15/§16 row to
   the concrete method(s) that cover it — a row with none is `BLOCKED`, never `PASS` (confirmed the
   implementer's own prior sheet, which predates the CODE_REVIEW-round additions, undercounted what
   now exists and overcounted what doesn't; rewritten independently rather than patched).
4. Rewrote `docs/test-cases/TC-HO-APT-001.md` in full, every row `NOT RUN`, before running anything.
5. Ran the three unit suites, then `dotnet build`, then the full `CatEcommerce.IntegrationTests` suite
   twice against `catecommerce_apt001` (never `catecommerce_dev`).
6. Wrote one new test, `AppointmentDetailStatusHistoryTests.cs` (extra check (d) — no existing test
   read `statusHistory[]` at all), drove a real appointment through Confirm → CheckIn → Start →
   Complete against real Postgres, and asserted the detail endpoint's `statusHistory[]` is
   chronologically ordered and each row correctly names its own `from`/`to`/`changedBy`. It failed,
   reproducibly. Diagnosed the cause (added a temporary `ITestOutputHelper` dump of the raw JSON,
   confirmed the SQL EF generated, then left the dump in as it is a clean, useful addition — no
   assertion was loosened to make it pass). See Findings.
7. Ran extra checks (a), (b), (c), (e) as combined filtered runs on two freshly created and migrated
   scratch databases (`catecommerce_apt001_tg1`, `catecommerce_apt001_tg2`), then three more times on
   `catecommerce_apt001`, to reach ≥5 runs (≥2 fresh) for (a)/(b) and ≥3 runs (≥1 fresh) for (e). Ran
   check (f) individually and confirmed it also runs inside the full Domain.Tests pass.
8. Dropped both scratch databases at the end; confirmed via a throwaway Npgsql console listing every
   `pg_database` row that only `catecommerce_apt001` (plus every pre-existing, unrelated database)
   remains — `catecommerce_dev` was never pointed at.
9. Left the sheet, this gate file and the new test uncommitted; edited no product code.

## Extra checks (a)-(f), results

- **(a)** `PlaceHold_TwentyConcurrentRequestsForSameSlot_ExactlyOneSucceeds` (I-C2): **5/5 runs PASS**
  — fresh `tg1`, fresh `tg2`, `catecommerce_apt001` ×3. Every run's own internal assertion (every
  loser body contains `SlotNoLongerAvailable`, not just a bare `409`) held.
- **(b)** `PlaceHold_EightConcurrentRequestsFromOneCustomerForDifferentSlots_AtMostOneHeldRowRemains`:
  **5/5 runs PASS** — same 5 runs as (a) (the two tests share a filtered run each time).
- **(c)** `SlotClaimTests` (Application.Tests, 2/2), `UniqueConstraintExceptionFilterTests` (3/3) and
  `AppointmentStaleHoldReleaseTests` (4/4): **all PASS**, on `catecommerce_apt001` (×2 full runs) and
  on both fresh scratch databases.
- **(d)** New test written (see above). **Confirmed the EF/Postgres nested-collection-projection
  concern itself is resolved** — `ClinicDetailProjection.BuildAsync`'s
  `a.StatusHistory.OrderBy(h => h.ChangedOn).Select(h => new AppointmentStatusChangeViewModel(h.From.DisplayName, h.To.DisplayName, ...))`
  translates entirely into one SQL statement (`LEFT JOIN clinic.appointment_status_change ... ORDER BY
  a1."Id", a0.changed_on`, confirmed in the EF debug log), throws no translation exception, and every
  returned row's `from`/`to`/`changedOn`/`changedBy` are correctly paired to their own row — but the
  test still **fails**, because of a different, real bug it surfaced. See Findings.
- **(e)** `WaitlistFlowTests` (7 tests): **4/4 runs, 7/7 each — all PASS** — fresh `tg1`, fresh `tg2`,
  and included in both full-suite runs.
- **(f)** `AppointmentSlotFinderTests.GridStartsForDay_SpringForwardDstDay_OnlyTheChangedHourCollapses`
  (U-B10): **PASS**, run individually and inside the full Domain.Tests pass.

## FINDINGS

- [MEDIUM] `src/Application/CatEcommerce.Application/CommandHandlers/Clinic/AppointmentVisitCommandHandlers.cs:157`
  — `StartAppointmentCommandHandler` passes `DateTimeOffset.UtcNow` to `appointment.Start(...)`
  instead of the injected `TimeProvider`. **Reproduced, not inferred**: this gate's new
  `AppointmentDetailStatusHistoryTests` test drives Confirm → CheckIn → Start → Complete under
  `AppointmentsApiFactory.WithFakeTimeProvider` (this handover's own test convention, used throughout
  `AppointmentBookingFlowTests`, `AppointmentVisitFlowTests`, `WaitlistFlowTests`), then reads
  `GET /api/staff/appointments/{id}` once. The `CheckedIn → InConsultation` row's `changedOn` is
  stamped with the *real* wall-clock instant (`2026-09-28T15:5x:xx`, whatever time this gate actually
  ran) instead of the fake business instant (`2026-10-12T06:00:00Z`) every other transition in the
  same test correctly carries — and because the real run date sorted *before* the fake future baseline
  in all four runs, the returned `statusHistory[]` opened with `CheckedIn→InConsultation`, not
  `Held→Booked`, breaking the chronological-order guarantee §8.3/Appendix C promise ("auditable at a
  glance"). This is the concrete functional consequence of `docs/backlog/APT.md` row 13 (filed by
  ARCHITECTURE as LOW, framed only as a style deviation — "uses `DateTimeOffset.UtcNow` instead of
  `TimeProvider`"). Escalated to MEDIUM here because it is now a demonstrated, reproducible
  correctness bug in the audit trail, not just an inconsistency — but **not HIGH**: production always
  registers `TimeProvider.System`, whose value equals `DateTimeOffset.UtcNow` at the resolution this
  code uses, so ordinary production behaviour is unaffected; the blast radius is test determinism
  today and any future non-system `TimeProvider` deployment. Reported here, **not fixed** — this is a
  gate. Regression test: `AppointmentDetailStatusHistoryTests.GetDetail_AfterFourTransitions_StatusHistoryIsOrderedAndNamesEachState`,
  kept failing in `tests/**` rather than weakened, so it stays red until `StartAppointmentCommandHandler`
  is fixed.
- [MEDIUM] §16 group F (`I-F1`..`I-F9`, all nine cases) has **zero integration-test coverage**: staff
  booking inside a shift but outside opening hours, every move-conflict code (`PerformerBusy`,
  `RoomBusy`, `CatAlreadyBooked`, `PerformerNotAvailable`, `PerformerNotQualified`, `RoomKindMismatch`),
  the `ExpectedVersion` stale-calendar `409`, the >7-day calendar-range `400`, and — most notably —
  every one of the flagging domain-event handlers (`FlagAppointmentsOnLeaveApprovedHandler`,
  `UnflagAppointmentsOnLeaveCancelledHandler`, `FlagAppointmentsOnClosureHandler`,
  `UnflagAppointmentsOnClosureCancelledHandler`, `FlagAppointmentsOnCatDeceasedHandler`,
  `FlagAppointmentsOnCatTransferredHandler`) and `ReassignAppointmentsOnCustomerMergedHandler` are
  entirely untested. These are non-trivial, cross-aggregate handlers (§8.10, §11.6) that ARCHITECTURE
  and CODE_REVIEW passed on the strength of a code read, not a test run. Not HIGH: the code was
  reviewed by two earlier gates and nothing observed in this gate's own runs (concurrent moves,
  concurrent leave approvals, etc., all passing elsewhere in the suite) suggests these handlers are
  actually broken — this is a coverage gap, not an observed defect. Route to `docs/backlog/APT.md`.
- [LOW] §16 group D message/reminder timing (`I-D1`, `I-D3`..`I-D7`) and the matching unit cases
  (`U-D1`..`U-D4`) have no test at any level — the biggest single gap in the acceptance-criteria table
  (A13, "reminders at 24h/2h, never for a moved/cancelled appointment", has no passing case at all).
  Route to `docs/backlog/APT.md`.
- [LOW] `U-B3, U-B7, U-B8, U-B9` (horizon/registry halves), `U-B12`, `U-E1`..`U-E3`, `U-G1`..`U-G3`,
  `U-X2`, `U-X3` (predicate text) — unit-level coverage debt; each already named individually on the
  test-case sheet with an unblock note. Route to `docs/backlog/APT.md`.
- [LOW] `I-B1`..`I-B3`, `I-C1`, `I-C3`, `I-C4`, `I-C5`, `I-C7`, `I-C8`, `I-X1` — integration-level
  coverage debt for hold-lifecycle edge cases, cross-customer `404`s, the deceased-cat refusal, and
  the log-content PII sweep. Route to `docs/backlog/APT.md`.
- No BLOCKER or additional HIGH findings.

## SCOPE CHECK

- Files touched by this gate: `docs/test-cases/TC-HO-APT-001.md` (rewritten — in §13's allowed scope,
  a process artifact), `tests/CatEcommerce.IntegrationTests/Endpoints/AppointmentDetailStatusHistoryTests.cs`
  (new — `tests/**` is in §13), `docs/gates/HO-APT-001-TEST.md` (this file — in §13). Both the sheet
  and the new test file are left **uncommitted**, per instructions.
- No product code was edited. No file outside `tests/**`, `docs/test-cases/**` or `docs/gates/**` was
  touched.
- No Administration aggregate, no other area's file, and no forbidden-scope path (§14) was touched.
- Database safety: only `catecommerce_apt001` (pre-migrated, pre-existing) and two scratch databases
  this gate created itself (`catecommerce_apt001_tg1`, `catecommerce_apt001_tg2`, both migrated with
  `dotnet ef database update` before use) were used. Both scratch databases were dropped at the end;
  confirmed by listing `pg_database` before and after. `catecommerce_dev` was never connected to,
  never migrated, never queried.
