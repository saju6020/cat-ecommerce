GATE: CODE_REVIEW (DELTA)
HANDOVER: HO-APT-001
COMMITS: f516cda (product code); 9ccf7fa, df270d1, 9a43619 (tests, came with it); 3fd1389 (docs, §1 Amended (12))
DATE: 2026-09-28
STATUS: PASS

SCOPE OF THIS PASS
Delta review of everything landed after HO-APT-001's ARCHITECTURE (3rd), SECURITY (2nd) and
CODE_REVIEW (2nd) passes: `git diff b67769d HEAD`. The only product-code change is f516cda, "name
the conflicting local time in busy/booked refusals (§10.4)" — a root-cause fix found and made while
writing ACCEPTANCE's I-F2/A14/A15 test coverage (the ACCEPTANCE record itself is out of scope here;
this is the code/test delta it produced). Reviewed `SlotClaim.cs`'s diff in full against the callers
that use it (`AppointmentBookingCommandHandlers.cs`, `AppointmentChangeCommandHandlers.cs`,
`AppointmentStaffCommandHandlers.cs`), ADR-045 (`.ai/decisions/adr.md:2253-2264`), §10.4/§12.5 in
`docs/handover/HO-APT-001.md`, and all five new/extended test files (`AppointmentMoveConflictTests.cs`,
`AppointmentReminderFlowTests.cs`, `AppointmentFlaggingTests.cs`, `AppointmentBookingFlowTests.cs`,
`ClinicNotificationsTests.cs`, `AppointmentReminderGuardTests.cs`) plus §1 Amended item (12).

VERIFICATION PERFORMED
- Confirmed via `git diff b67769d HEAD --name-only -- src` that `SlotClaim.cs` is the only file
  changed under `src/`. Nothing else in `src/` moved.
- Read `SlotClaim.Diagnose`/`FindOverlap`/`FormatLocalRange` in full:
  - `FindOverlap` uses the identical predicate to the pre-existing `Overlaps` (`i.Start < end &&
    start < i.End`, half-open), just returning the first matching interval instead of a bool — same
    selection rule the finder already validated against, so the named conflict is a real one, not a
    guess.
  - Performer/cat conflicts name the specific overlapping interval for that specific
    performer/cat. Room conflict names `roomsOfKind[0]` and its own `FindOverlap` result: since the
    branch only reaches this code when `roomsOfKind.All(r => Overlaps(...))` is true, room `[0]`
    is guaranteed to overlap, so the `roomConflict is not null` fallback path is unreachable in
    practice, not a bug — reads correctly as defensive, not as evidence the "no room free" message
    can still surface incorrectly.
  - `FormatLocalRange` converts an instant (`TimeInterval.Start`/`.End`, both `DateTimeOffset`) to
    branch-local display time via `TimeZoneInfo.ConvertTime` — the same instant-to-local **display**
    conversion `ClinicNotifications.FormatLocal` already uses (pre-existing, same file family,
    already passed earlier gates). Confirmed against ADR-045's actual text
    (`.ai/decisions/adr.md:2253-2264`): the ADR scopes *local-to-instant* conversion to
    `BranchCalendar.OpenIntervals`/`StaffAvailability.Intervals` only — this is the reverse,
    read-only direction, so the commit's own doc-comment claim ("not a new ADR-045 site") is
    accurate, not just asserted.
  - DST: converting from an instant (`DateTimeOffset`, unambiguous) to local for display has no
    ambiguous/invalid-time exposure the way constructing a local wall-clock time would — the risky
    direction is local-to-instant, which this code does not do.
  - Wording matches §10.4's example shape ("`<name>` already has an appointment `<start>–<end>`.").
- Traced the customer/staff split: `SlotClaim.ForCustomer` (`AppointmentBookingCommandHandlers.cs`)
  is untouched by this diff and still collapses `PerformerBusy`/`RoomBusy`/`CatAlreadyBooked` (and
  `PerformerNotAvailable`, `RoomNotAvailable`, `RoomKindMismatch`) to the generic
  `SlotNoLongerAvailable`, per §12.5 — confirmed both call sites that pass `IsStaff: false`
  (`PlaceSlotHoldCommandHandler`, and `AppointmentChangeCommandHandlers.cs`'s customer reschedule)
  route through `ForCustomer` before returning. No customer-facing response can contain the new
  time-stamped message.
  - Staff routes (`BookAppointmentByStaffCommandHandler`, `MoveAppointmentCommandHandler`, both
    `IsStaff: true`) return `outcome.Message` directly, unfiltered — this is where the new message
    reaches an HTTP response.
  - Content check: the performer message names the performer being booked (already known to the
    staff caller making the request) and their own conflicting appointment's time — not another
    customer's cat, cat owner, or reason for visit. The cat message names only the requesting cat's
    own other appointment. The room message names only the room and time. None of the three reveals
    another customer's identity or a different cat's details — appropriate for a staff-only 400/409
    on an endpoint staff already used to identify the performer/cat/room in question.
- §13/architecture: change is confined inside the existing `Diagnose` static helper of an
  already-`ADR-015`-compliant static class (no state, no new service layer, no new port). No new
  layering introduced.
- §1 Amended item (12) (`docs/handover/HO-APT-001.md`): read against the actual diff — accurately
  states the pre-existing generic message, the root cause, the fix (`timeZoneId` param, `FindOverlap`,
  `FormatLocalRange`), the ADR-045 non-issue, the customer/staff split, and lists the five test files
  with §16 id prefixes matching what's actually in each file (checked A12/A13/A14/A15/A21 test
  content against the claim below).
- Test files:
  - `AppointmentMoveConflictTests.cs` (I-F2..I-F4): six I-F2 sub-tests each isolate one conflict code
    and, for the three time-naming codes, assert the specific `HH:mm`-`HH:mm` substrings alongside
    the code — genuinely exercises the fix, not a tautology. I-F3 (stale version) and I-F4
    (concurrent moves) both read as their names claim.
  - I-F4's three-shape assertion (`IF4_TwoConcurrentMovesIntoTheSameSlot_ExactlyOneSucceeds`): the
    primary invariant (`succeeded == 1`, never 0 or 2) is a real, falsifiable assertion — a second
    success or a total failure both fail the test. The loser-shape check is intentionally loose
    (400 or 409, one of four known conflict-family codes) because which shape a given run produces
    is a genuine race outcome, documented in detail in the test's own doc comment and reproduced by
    the author as flaky under a stricter assertion. One imprecision: the doc comment enumerates
    "three shapes" (400 Diagnose specific-code, 409 specific-code, 409 generic) but the code's
    condition does not pair status with code (e.g. it would also accept 400+`SlotNoLongerAvailable`,
    a fourth, undescribed combination) — not a false pass risk (that combination, if it occurs, is
    still a legitimate refusal, not a masked bug) but the comment overstates the precision of what's
    checked. MEDIUM, listed below.
  - `[Collection("Notifications")]` is present on `AppointmentBookingFlowTests.cs` and
    `AppointmentReminderFlowTests.cs` — the two classes that call
    `RunNotificationDeliveryPassAsync`/scan the global notification tables — and correctly absent
    from `AppointmentMoveConflictTests.cs`/`AppointmentFlaggingTests.cs`, which don't call it.
    Consistent with the convention already established across every HO-NTF-001 test class (grepped:
    11 other files carry the same tag for the same reason).
  - `AppointmentReminderFlowTests.cs` (I-D4/I-D5/I-D6) and `AppointmentFlaggingTests.cs`
    (I-F6/I-F7): read in full, each assertion checks a real state transition (request/notification
    status, `needsRescheduling`, `rescheduleFlagReason`, the `/flagged` list, both leave-approve and
    leave-cancel directions) through real HTTP endpoints and a real delivery-pass/leave-approval
    call — not mocked, not asserting on call arguments.
  - `ClinicNotificationsTests.cs`/`AppointmentReminderGuardTests.cs` (U-D1/U-D2, Application.Tests /
    Infrastructure.Tests): spot-checked, match the stated pattern of the sibling
    `CatSubjectGuardTests.cs`.

Commands run (worktree `D:/my-repo/cat_ecommerce-wt/APT-001`, `CATECOMMERCE_TEST_DB` pointed at
`catecommerce_apt001`, never `catecommerce_dev`):
- `dotnet build CatEcommerce.sln --no-incremental` — 0 warnings, 0 errors.
- `dotnet test tests/CatEcommerce.Application.Tests --no-build` — 125/125 passed.
- `dotnet test tests/CatEcommerce.Infrastructure.Tests --no-build` — 87/87 passed.
- `dotnet test tests/CatEcommerce.IntegrationTests --no-build --filter
  "FullyQualifiedName~AppointmentMoveConflictTests|FullyQualifiedName~AppointmentReminderFlowTests|
  FullyQualifiedName~AppointmentFlaggingTests|FullyQualifiedName~AppointmentBookingFlowTests"` —
  20/20 passed.
- Re-ran `IF4_TwoConcurrentMovesIntoTheSameSlot_ExactlyOneSucceeds` alone three more times to check
  for flake — passed all three (1/1, 1/1, 1/1).

All commands ran cleanly; no shell-tool limitation to report.

FINDINGS
No BLOCKER or HIGH findings.

MEDIUM/LOW (owner rule 2026-09-27: do not block; for `docs/backlog/APT.md`, not edited here):
- [MEDIUM] `AppointmentMoveConflictTests.cs`, `IF4_TwoConcurrentMovesIntoTheSameSlot_ExactlyOneSucceeds`
  doc comment vs. assertion: the comment enumerates exactly three legitimate loser shapes (400
  Diagnose-specific-code / 409 specific-code / 409 generic `SlotNoLongerAvailable`), but the
  assertion checks status-in-{400,409} and code-in-{four codes} independently, so it would also
  accept a fourth, undescribed combination (400 + `SlotNoLongerAvailable`) without comment. Not a
  false-pass risk on its own (that outcome, if it occurs, is still a legitimate refusal), but the
  comment should either narrow to what's actually checked or the assertion should pair status with
  code to match the documented three shapes exactly.
- [LOW] `SlotClaim.cs` `FormatLocalRange`'s room-conflict fallback (`roomConflict is not null ? ... :
  "No room of the required kind is free at this time."`) is unreachable given the caller's own
  `roomsOfKind.All(r => Overlaps(...))` guard (room `[0]` must overlap when this runs) — harmless,
  but a stray branch with no test exercising it; either drop the ternary (assert non-null) or add a
  regression note explaining why it's kept as a safety net.

SCOPE CHECK
- Product code: `SlotClaim.cs` only (confirmed by `git diff b67769d HEAD --name-only -- src`) — no
  file outside the diagnosis path touched.
- Customer-facing leak check: no route reachable by a customer returns `PerformerBusy`, `RoomBusy`
  or `CatAlreadyBooked` with the new time-stamped message — `SlotClaim.ForCustomer` (unchanged)
  still collapses all three to `SlotNoLongerAvailable` before either customer-facing handler
  returns.
- §1 Amended (12): accurately describes the change, its scope, and the ADR-045 non-issue.
