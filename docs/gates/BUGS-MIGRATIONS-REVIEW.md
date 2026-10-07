GATE: CODE_REVIEW (combined architecture + security + code, owner rule 2026-10-07)
HANDOVER: BUGS-MIGRATIONS (backlog rows NTF-001 #1, #5; SHP #44; APT-001 #47)
COMMITS: 532b568 2fbe296 c7d36ba 2385ac1 0de8b42 e54dd60 (range 7a742ec...0de8b42)
DATE: 2026-10-07
STATUS: PASS

RUN
- dotnet build: 0 warnings, 0 errors.
- Domain.Tests 687/687, Application.Tests 134/134.
- Integration (CATECOMMERCE_TEST_DB = catecommerce_bugsmig; dev DB untouched), filtered to CareReminder,
  NotificationClaim, ProductSchemaIndex, AppointmentDetailStatusHistory: 22/22.

CHECKED, NO ISSUE
- Migrations: four, ordered after 20261003210721 (master's latest). Add-column migrations are NOT NULL
  DEFAULT 0 (safe on existing rows), Down() drops the column. Index migration Down() restores the old
  indexes; no data destroyed. Row 47 backfill ROW_NUMBER() PARTITION BY appointment_id ORDER BY changed_on, id
  is correct and runs after the column exists. Snapshot matches the configurations.
- Dedup key: generation 0 appends nothing, so in-flight keys are unchanged; OwnershipGeneration++ occurs in
  both the transfer and the merge-reassignment mutation, so A->B->A yields a new key.
- ClaimCount: MustPark needs Status==Sending AND an expired lease AND ClaimCount>=MaxAttempts(6). A healthy
  retry sits in RetryPending, so it is never parked early; AttemptCount/backoff logic is untouched. Existing
  rows start at 0 (lenient, not premature).
- Index filters: "status = 2" is interpolated from ProductStatus.Active; column is "status"; matches the
  Status == Active predicate used by the storefront queries. Descending index emitted as intended.
- Sequence: Max(Sequence)+1 stays monotonic after RemoveAt(0); legacy 0 rows are fine; read order is
  ChangedOn then Sequence.
- Architecture: logic is in the aggregates (Claim/MustPark/Park, ownership generation, Transition); handler
  only orchestrates; read side AsNoTracking projection. No superseded-architecture items. Nothing exposed.
- Tests fail without the fixes (aggregate tests, claim/delivery endpoint test, schema index test, status
  history test).

FINDINGS
- [MEDIUM] src/Application/.../DeliverNotificationCommandHandler.cs:~80 — the quiet-hours Defer block runs before
  the MustPark check. For a Normal-urgency row stuck in Sending during quiet hours, Defer() throws
  (it only accepts Queued/RetryPending), so parking is blocked until quiet hours end (pre-existing throw, now
  also delays row 5). Move the MustPark branch above the quiet-hours check, or guard Defer on CanClaim states.
- [LOW] NotificationAggregate.MustPark — a slow-but-alive 6th send whose lease lapsed can be parked by another
  worker while still in flight (xmin conflict will then fail the original's save). Consider a lease longer
  than the provider timeout; documented limitation.
- [LOW] 20261006184955_AppointmentStatusChangeSequence.cs — backfill tie-break on random Guid id makes order
  among legacy rows with identical changed_on arbitrary (acceptable, best effort).
- [LOW] AppointmentStatusChange ctor / DueCareReminderRow use default parameters (sequence = 0,
  OwnershipGeneration = 0), which can silently hide a caller forgetting to pass them.

SCOPE CHECK
- files in allowed scope: 30/30
- files outside allowed scope: none
