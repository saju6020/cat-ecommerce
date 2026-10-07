## Second pass

GATE: TEST
HANDOVER: HO-NTF-001
COMMITS: `d5d06da` (owner id in the care-reminder dedup key, fixes I-F5/A6), `217024a` (fixes three
  pre-existing cross-test/cross-collection row-misidentification bugs found while verifying the
  I-F5 fix — mixed clocks and unscoped latest-row lookups in
  `NotificationClaimAndDeliveryEndpointTests`, `NotificationLogAndInboxEndpointTests`,
  `PasswordResetDeliveryEndpointTests`; none of them this handover's own product code). Test
  additions this pass, on top of `217024a`: new cases in `CareReminderEndpointTests.cs` (I-F9) and
  `QuietHoursEndpointTests.cs` (I-C4), plus three new files —
  `AccountRecipientDirectoryTests.cs` (U-B14), `NotificationLogCaptureEndpointTests.cs` (I-X1),
  `NotificationPersonalDataEraserTests.cs` (R6). Also observed landing during this pass, untouched
  by this gate: `1ba4025` "Record HO-NTF-001 CODE_REVIEW pass 5 (PASS)" (a narrow, parallel
  CODE_REVIEW re-pass touching only `docs/gates/HO-NTF-001-CODE_REVIEW.md`, not touched here).
WORKTREE: D:/my-repo/cat_ecommerce-wt/NTF-001, branch ho/NTF-001, head `217024a` at the start of
  this pass.
DATE: 2026-09-27
STATUS: PASS

Full case sheet: `docs/gates/TC-HO-NTF-001.md`'s "Second pass" section — 120 rows, 120 PASS, 0 FAIL,
0 BLOCKED, 0 NOT RUN.

## What changed since the first pass

The first pass's own `FAIL` (I-F5/A6, a MEDIUM — a cat transfer permanently blocked the new owner's
reminder) is fixed by `d5d06da`: the care-reminder dedup key now includes the owner id. The
regression test the first pass committed
(`CareReminderEndpointTests.TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner`)
was left unweakened and now passes for real, watched directly, not inferred.

`217024a` fixes three pre-existing test-isolation bugs surfaced while re-running the full suite to
verify the I-F5 fix — a mixed-clock bug (a request posted through one factory's real `TimeProvider`
but expanded through another's `FakeTimeProvider` twelve hours later, so a 15-minute reset token was
already `Expired` by the time expand ran) in two test files, and an unscoped "last two rows in the
whole table" lookup in a third that a concurrently-running, different-collection test's own advanced
clock could silently push a wrong row into. None of these three fixes touch this handover's own
production code, and none weaken any assertion — only how each test finds its own row was made
precise. All three were re-run individually and as part of two full suite passes below.

A narrow CODE_REVIEW fifth pass ran in parallel (`1ba4025`, PASS) and recorded a MEDIUM finding on
the exact same dedup key `d5d06da` had just changed: a cat transferred **A → B → A** within the same
due window computes the identical key for the second A-request as the first (now-Suppressed)
A-request already consumed, so the original owner's reminder for that due date is silently missed
(never duplicated — the dedup guarantee itself holds). Per instruction from the integrator, this is
not fixed here; a new case (I-F9,
`CareReminderEndpointTests.TransferBackToOriginalOwner_BeforeDelivery_NoDuplicateRequestIsCreated`)
asserts only the guarantee that holds (no duplicate row for the reused key), and the missed-reminder
gap is recorded as a MEDIUM backlog row in `docs/backlog/NTF.md`, not as a failing test.

## The four `BLOCKED` rows, re-assessed

All four were closed this pass — cheaper than expected in every case:

- **U-B14** and **R6** simply had no test written yet; both close with a straightforward new test
  file calling the relevant service directly via DI (`INotificationRecipientDirectory`,
  `IPersonalDataEraser`) against a real, HTTP-seeded database.
- **I-C4** and **I-X1** turned out to have been `BLOCKED` on a **mistaken premise**, not a genuine
  gap: I-C4's branch-override mechanism (`PUT /api/admin/settings?branchId=...` plus
  `POST /api/admin/branches`) was already fully wired and just needed a test to drive it; I-X1's
  claim that "no log-capture harness exists for Notifications" was checked against the actual class
  hierarchy and found wrong — `NotificationsApiFactory : CatEcommerceApiFactory`, and
  `WithCapturedLogs()` is a plain, public, non-overridden method on the base class, already
  inherited. `AdministrationApiFactory`'s own `AdminLogCaptureEndpointTests` (a different area's TEST
  gate, seventh pass) made the identical discovery independently.

None of the four required new production-facing infrastructure; all four are now real, watched
`PASS` rows in the case sheet, not inferred or reasoned-about closures.

## Suite counts (this pass)

| Suite | Run 1 | Run 2 |
|---|---|---|
| Domain | 415/415 | 415/415 |
| Application | 119/119 | 119/119 |
| Infrastructure | 82/82 | 82/82 |
| Integration | 500/500 | 500/500 |

Build: `dotnet build CatEcommerce.sln` — 0 warnings, 0 errors. Database: `catecommerce_ntf`, dropped
and recreated with `psql` and migrated with `dotnet ef database update` before each of the two runs
above; `dotnet ef migrations has-pending-model-changes` reported clean both times, against the same
single squashed `20260927123315_AddNotificationsModule` migration as the first pass (no new
migration this pass — no schema change).

## Owner-rule disposition (this pass)

No BLOCKER/HIGH finding. One MEDIUM was recorded (the A→B→A missed-reminder gap CODE_REVIEW's fifth
pass found) and is backlogged, not fixed, per the owner rule and the integrator's own instruction for
this pass — see `docs/backlog/NTF.md` #1. **STATUS: PASS.**

## Never marked PASS without watching it (this pass)

Every row in `docs/gates/TC-HO-NTF-001.md`'s "Second pass" section was either watched directly (each
new or re-verified test run individually, then again as part of both full suite passes above) or is
an existing test re-run unchanged as part of those same full passes. No `BLOCKED` row remains.

---

GATE: TEST
HANDOVER: HO-NTF-001
COMMITS: test additions on top of f5f9ece (head at gate start) —
  bdfa182 (U-F1 guard tests), 1d168dd (notify-schema migration test),
  0114fa7 (I-F2/I-F5/I-F6/U-F3), bff5804 (paging limits), a3baf0b (I-B2
  atomicity), 784376d (concurrent-expand xmin race), 4f8af41 (TC sheet).
  Also observed landing during this pass, untouched by this gate:
  a47a843 "Record HO-NTF-001 CODE_REVIEW pass 4 (PASS)" (docs only).
WORKTREE: D:/my-repo/cat_ecommerce-wt/NTF-001, branch ho/NTF-001
DATE: 2026-09-27
STATUS: FAIL (owner rule: a MEDIUM finding, not a BLOCKER/HIGH — see the paragraph below)

Full case sheet: docs/gates/TC-HO-NTF-001.md — 119 rows, 114 PASS, 1 FAIL, 4 BLOCKED, 0 NOT RUN.

## Owner-rule disposition

Per the 2026-09-27 owner rule, only a BLOCKER or HIGH finding fails a gate; MEDIUM/LOW go to the
area backlog. The one production defect found this pass (below) is, in my judgement, MEDIUM: a real,
deterministic, permanent correctness gap in a courtesy reminder feature — never a security, data-loss,
financial or medical-safety issue, and the cat's actual medical care does not depend on the reminder
firing. I am recording STATUS: FAIL on the literal sheet result (a written, cited acceptance-evidence
case that fails against the shipped code is not something a TEST gate can mark PASS), but under the
stated owner rule this finding on its own would not block the gate. That determination is
ACCEPTANCE's to make, not mine — I flag it prominently rather than deciding it here.

## What I did, in order

1. Read docs/handover/HO-NTF-001.md in full (§1 amendment history, §8 domain model, §11/§12 write/read
   side, §15/§16 test cases, §18 acceptance criteria, §19/§20) and all three earlier gate files
   (ARCHITECTURE PASS on pass 2, SECURITY PASS on pass 3, CODE_REVIEW — FAIL on its third pass with one
   HIGH, fixed by 471e52f before this gate started, and PASS on a fourth pass that landed mid-gate,
   untouched by me).
2. Wrote docs/gates/TC-HO-NTF-001.md **before running anything**, enumerating every §15/§16 case ID,
   every fixed finding from the three earlier gates as a regression row, and every §18 acceptance
   criterion, each starting `NOT RUN`.
3. Confirmed the build was green before touching anything (0 warnings/errors), then ran the full
   Domain/Application/Infrastructure/Integration suites once, unmodified, as the "before" baseline.
4. Read the existing Notification test files (Domain, Application, Infrastructure, IntegrationTests)
   case by case against the sheet, updating each row to what I actually observed.
5. Where the sheet showed a gap the handover requires, added a persistent test (see below), rebuilding
   and running each new test individually before moving to the next.
6. Recreated `catecommerce_ntf` from empty (`dotnet ef database drop --force` /
   `database update`) and ran the full four suites again as the "after" run.
7. Committed the test additions and the TC sheet in seven small commits (Co-Authored-By: Claude Opus
   5.5 <noreply@anthropic.com>), leaving this verdict file uncommitted.

## Suite counts

| Suite | Before (baseline, f5f9ece) | After (this gate's additions) |
|---|---|---|
| Domain | 415/415 | 415/415 (unchanged — no Domain test added) |
| Application | 119/119 | 119/119 (unchanged — no Application test added) |
| Infrastructure | 73/73 | 82/82 (+9: `CatSubjectGuardTests`) |
| Integration | 483/483 | **491/492** (+9 new tests; 1 genuine, reproducible FAIL — see Findings) |

Build: `dotnet build CatEcommerce.sln` — 0 warnings, 0 errors, both before and after.
Migrations: `dotnet ef migrations has-pending-model-changes` reported clean both times, against a
freshly dropped-and-recreated `catecommerce_ntf` each time, applying only the one squashed
`20260927123315_AddNotificationsModule` (plus every earlier handover's own migrations).

The baseline numbers (415/119/73/483, all green) match the implementer's own claimed counts in
f5f9ece's commit message exactly.

## Findings

### FAIL — MEDIUM: a cat's ownership transfer permanently blocks a reminder for the new owner (I-F5, A6)

`CareReminderEndpointTests.TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner`
(new this pass) reproduces this deterministically. It is written directly from §16's own I-F5 wording:
*"Transfer the cat to another owner before delivery -> Cancelled; the next sweep requests one for the
new owner."*

The first half holds: the guard (§8.7, `CatVaccinationDueGuard`) correctly fails at expansion once
`OwnerId` no longer matches the request's `RecipientId`, and the request is suppressed
(`SubjectNoLongerApplies`) with nothing delivered to the original owner.

The second half does not. `CareReminderSweep`'s `DeduplicationKey` is
`care:{kind}:{catId:N}:{refId:N}:{dueOn:yyyyMMdd}` (§8.12) — it does not vary by owner or recipient.
`RequestNotificationCommandHandler`'s dedup pre-check
(`requestRepository.ExistAsync(r => r.DeduplicationKey == command.DeduplicationKey)`) matches ANY
existing row with that key, regardless of status. The FIRST request (now `Suppressed`, for the
original owner) already consumed that exact key. The next sweep computes the identical key for the
new owner (the vaccination record and its `NextDueOn` are untouched by a transfer) and is treated as
"already requested" — nothing is ever created for the new owner, for that specific due date. This is
silent and permanent: it does not self-correct until the underlying vaccination/deworming record
itself changes (i.e., the cat is next vaccinated), which can be up to a year away for an annual
vaccine.

I verified this is not a test mistake: `SeedVaccinationDueInAsync`/the guard/the routing policy all
behave exactly as documented elsewhere (every other care-reminder case, including the sibling I-F6
"renewed" case added this same pass, passes cleanly); the only variable is the dedup key's owner-
independence, read directly from `CareReminderSweep.cs`.

Reported per the gate protocol: **not fixed here.**

### Confirmed correct (no finding) — I-F6, previously untested

The "renewed" half of the same family of scenarios (`RecordNewVaccinationBeforeDelivery_Cancelled_
NoNewReminderUntilTheNewWindow`, new this pass) is NOT affected by the same class of bug, because a
renewal changes `NextDueOn`, and therefore the dedup key, so the next sweep is never blocked by the
stale key — it simply doesn't fire yet because the new due date is outside the lead window. This
passed on the first correctly-written attempt (after two of my own test-setup mistakes, described
below, were fixed).

## Existing tests I judged weak, and what I found instead

I did not find any existing Notification test whose assertions were loose enough to pass against
broken production code — the suite is unusually thorough (it already carries its own history of five
gate-fix rounds' worth of "would-fail-without-the-fix" regression tests, and I could not find one that
regressed to a tautology). The one quality issue on record, `RenderedMessageColumnLimitsTests
.TruncationNeverSplitsASurrogatePair`'s input not reaching its own named branch, was already caught and
fixed by CODE_REVIEW's own third pass (471e52f) before this gate started; I re-verified it still passes
and exercises the intended back-off branch.

## Gaps deliberately left (recorded in the TC sheet, not hidden)

- **U-B14** (`AccountRecipientDirectory` merge/anonymise resolution) has no isolated unit test — only
  end-to-end coverage through I-B14/I-D10/I-D11. Not added this pass; a focused test against a real
  `CatEcommerceDbContext` would close it.
- **I-C4** (branch-specific quiet-hours override) has no dedicated test. Not added this pass.
- **I-X1** (log capture, no body/destination/token/variable at any level) is BLOCKED:
  `NotificationsApiFactory` has no `WithCapturedLogs()` hook, unlike `CatEcommerceApiFactory`/
  `FilesApiFactory`. Building that harness plus a representative flow set was judged out of this pass's
  reasonable scope; SECURITY's own code-level review of the "never log the message" contract is the
  closest existing evidence, but it is not the same as watching the logger.
- **`NotificationPersonalDataEraser`** (ARCHITECTURE HIGH 5, item 27) is registered per the handover's
  own §13 scope, but no test — of registration or of an actual erasure pass calling it — exists
  anywhere in the tree. Its internal operations are proven indirectly through the domain-event-handler
  route's own tests, but the eraser itself is never invoked.
- The "retry/park rule" ask is closed for the **non-DbUpdateException** half (two dedicated regression
  tests already prove a read failure never counts and always leaves the request Pending) and
  substantially, but not completely, for the **DbUpdateException** half: the new
  `ConcurrentExpand_OfTheSameRequest_ExactlyOneExpansion_NeverDoubled` test proves a genuine
  `DbUpdateConcurrencyException` from a real `xmin` race is handled safely (exactly one expansion,
  never four notifications), but it dispatches the command directly rather than through
  `NotificationWorkCycle.DispatchAsync`'s own catch, so it does not itself prove the specific
  `RecordExpandFailureAsync` backstop fires correctly after 5 consecutive REAL (not simulated) save
  failures. I judged forcing that exact interaction deterministically, without a flaky race, to be more
  engineering than this pass's remaining budget justified; CODE_REVIEW's own source-level read of the
  `ex is DbUpdateException` classification (verified against `EfAggregateRootRepository.CommitAsync`
  being the only throw site) is the strongest existing evidence for that specific wiring.
- **I-D5** as its own scenario (actually changing a customer's mobile number mid-test, then resending)
  was not separately added — the underlying re-resolution code path is already exercised by the
  existing cross-channel resend cases.

None of these gaps were hidden: each has its own row in docs/gates/TC-HO-NTF-001.md's "Cases I could
not run" / "Gaps left deliberately" tables, with what would unblock it.

## What was added this pass (test-only; no production code touched)

- `tests/CatEcommerce.Infrastructure.Tests/Notifications/Guards/CatSubjectGuardTests.cs` (U-F1, 9
  cases — neither guard had a dedicated test before this pass).
- `tests/CatEcommerce.IntegrationTests/Persistence/NotificationSchemaMigrationTests.cs` ("migration
  apply from empty" for the `notify` schema — the existing `MigrationsOnAnEmptyDatabaseTests` only
  ever checked `account`/`audit`/`clinic`).
- `CareReminderEndpointTests.cs`: `ReminderWindowBoundaries_...` (I-F2/U-F2),
  `DeduplicationKey_MatchesTheSpecifiedFormat` (U-F3),
  `TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner` (I-F5 — FAIL, see
  Findings), `RecordNewVaccinationBeforeDelivery_Cancelled_NoNewReminderUntilTheNewWindow` (I-F6);
  plus `TransferCatOwnershipAsync`/`SeedAdditionalVaccinationAsync` on the shared base class.
- `NotificationLogAndInboxEndpointTests.cs`: `LogPageSizeOrPageNumberOverLimit_Returns400`,
  `InboxPageSizeOverLimit_Returns400` (paging limits — previously untested for this area).
- `PasswordResetDeliveryEndpointTests.cs`: `AccountSaveFailsAfterEnqueue_NeitherTokenNorNotification
  RequestExists` (I-B2's atomicity guarantee, forced with a genuine Postgres PK-violation, not a
  simulated failure).
- `NotificationClaimAndDeliveryEndpointTests.cs`: `ConcurrentExpand_OfTheSameRequest_
  ExactlyOneExpansion_NeverDoubled` (real `xmin` race on the SAME request being expanded twice).

## Never marked PASS without watching it

Every PASS row in the case sheet was either watched directly (I ran the specific test and read its
result) or is an existing test I re-ran as part of the full-suite passes above. Every BLOCKED row says
so and names what would unblock it. The one FAIL was watched fail, three times, with the exact same
assertion failure each time (deterministic, not flaky) — see the Findings section above and
docs/gates/TC-HO-NTF-001.md for the full detail.
