## Fifth pass

GATE: CODE_REVIEW
HANDOVER: HO-NTF-001
COMMITS: d5d06da 217024a (reviewed against 7fb5cfa; the TEST gate's own FAIL record on that head).
Worktree D:/my-repo/cat_ecommerce-wt/NTF-001, branch ho/NTF-001, head 217024a.
DATE: 2026-09-27
STATUS: PASS

Owner rule applied: only a BLOCKER/HIGH finding fails the gate. None found this round.

Scope of this pass: (a) verify d5d06da's care-reminder dedup-key fix (I-F5/A6) is correct and
complete — nothing else builds/parses the key, the new key cannot exceed `varchar(200)`, a
re-sweep for an unchanged owner still dedupes, and what happens if ownership returns to the
original owner; (b) verify 217024a's three test-isolation fixes did not weaken any assertion.
Full build/test run from a clean `git archive 217024a` in a fresh scratch folder against a fresh
database, per instructions.

VERIFIED

(a) d5d06da — care-reminder dedup key gains the owner id
- Grepped the whole tree for the `"care:"` key literal and for `DeduplicationKey` generally: the
  key is built in exactly one place, `CareReminderSweep.RequestReminderAsync` (line 93,
  `$"care:{dedupKind}:{row.CatId:N}:{row.ReferenceId:N}:{row.OwnerId:N}:{row.DueOn:yyyyMMdd}"`).
  Nothing else constructs or parses a `"care:"`-prefixed key; `RequestNotificationCommandHandler`'s
  dedup pre-check (`requestRepository.ExistAsync(r => r.DeduplicationKey == command.DeduplicationKey)`,
  line 42) treats every producer's key as an opaque string — a correct, minimal, single-producer
  fix, matching the commit's own stated reasoning for not loosening the shared pre-check's status
  filter instead.
- Length: `NotificationRequestConfiguration.cs:46` still has `HasMaxLength(200)`
  (`NotificationRequestRules.MaxDeduplicationKeyLength = 200`, enforced in both
  `RequestNotificationCommandValidator` and `NotificationRequestAggregate.Create`). Worst case is
  `"care:"` (5) + `"vaccination"` (11, the longer of the two kinds) + 4 separators + three
  `Guid:N` (32 each) + `yyyyMMdd` (8) = 124 characters — comfortably under 200, matching the
  handover's own figure. No migration was needed and none was made; confirmed no schema/index
  change in the diff (`ix_notification_request_dedup` untouched).
- Re-sweep, same owner: `GetDueCareRemindersQueryHandler` reads the cat's *current* owner into
  `row.OwnerId` on every poll, so an unchanged owner produces the byte-identical key on every
  sweep — the pre-existing dedupe guarantee (I-F2, `SweepRunTwice_StillOnlyOneRequest`) is
  unaffected by adding a stable component to the key. Confirmed green in the full integration run
  below.
- Ownership change back to the original owner (asked for specifically): **not fully closed, but
  correctly scoped and not a new defect.** If a cat is transferred A → B (producing a `Suppressed`
  request keyed `...{ownerId=A}...` and an active one keyed `...{ownerId=B}...`, exactly as I-F5
  now proves) and then transferred back B → A *before that due date's `NextDueOn` changes*, the
  next sweep computes `...{ownerId=A}...` again — byte-identical to the original, already-`Suppressed`
  request's key. `RequestNotificationCommandHandler`'s pre-check matches that existing row
  regardless of status and returns success without creating anything, so owner A does not get a
  second reminder for that due date on the round trip. This is not a *duplicate* (the unique index
  and the pre-check both still hold), it is a reappearance of the same class of missed-reminder
  defect I-F5 reported, narrowed to a two-hop transfer instead of a one-hop transfer. It is not
  raised as a BLOCKER/HIGH here: it requires two ownership transfers of the same cat inside one
  reminder window, the failure mode is "silently one reminder short," not data loss or a
  double-send, and the commit's own chosen fix (vary the key by current owner, not by transfer
  event) is a reasonable, documented, minimal response to the reported acceptance-evidence gap —
  closing the round-trip case properly would need a transfer-sequence/ordinal component in the key
  (there is no such field on `CatAggregate` today), which is a larger design change than this test
  gate fix warranted. Recorded below as a MEDIUM for the area backlog per the gate-blocking rule.

(b) 217024a — test-isolation fixes
- `PasswordResetDeliveryEndpointTests.cs`: the two rows are now looked up by their own
  `RecipientId` (`olderCustomerId`/`newerCustomerId`, threaded back from a widened
  `RegisterCustomerAsync` return) via `SingleAsync`, replacing `OrderBy(RequestedOn) → ToListAsync
  → requests[^2]/requests[^1]` with an `Assert.True(requests.Count >= 2)` guard. This is strictly
  stronger, not weaker: `SingleAsync` throws if zero or more-than-one row matches the scoped
  filter, where the old code silently accepted whatever the last two rows *by insertion order*
  happened to be — exactly the shared-database cross-collection contamination the comment
  describes. The test's actual assertions (batch-starvation behaviour under `BatchSize=1`) are
  unchanged.
- `NotificationClaimAndDeliveryEndpointTests.cs`: `SeedQueuedResetAsync` gained a `customerId`
  parameter, scoping the request lookup by `RecipientId` and switching `FirstAsync` → `SingleAsync`
  (same strictly-stronger pattern). It also now posts the reset request through
  `daytimeFactory`'s own client (with `FakeTimeProvider`) instead of the base `Factory`'s
  (real clock) — a real, independently-diagnosed bug fix: the token's 15-minute `NotAfter` was
  previously computed against real wall-clock time while every later step ran against
  `SafeDaytimeInstant()`, which is `real now + 12h` outside `[2,15)` UTC, so the request could be
  seen as already-`Expired` before `ExpandOnlyAsync` ran, depending on the real time of day the
  suite happened to run. All three call sites (`TrySaveClaimAsync_TwoConcurrentClaims_ExactlyOneWins`,
  `TrySaveClaimAsync_LeaseExpired_CanBeReClaimed`, `CrashBetweenClaimAndRecordAttempt_
  RecoversToExactlyOneSend`) were updated consistently; none of their concurrency assertions
  (`Assert.True/False` on `TrySaveClaimAsync`'s result, `Assert.Equal(1, ...)` on notifications
  sent) changed.
- `NotificationLogAndInboxEndpointTests.cs`: the merge/anonymisation test's password-reset request
  is likewise moved onto `daytimeFactory`'s own client for the same clock-consistency reason; the
  test's own assertions (destination masked on the merged-away duplicate's row after anonymising
  the survivor) are untouched.
- The new `WebApplicationFactoryClientOptions { BaseAddress = "https://localhost/", HandleCookies =
  true }` inline usage matches the codebase's own established convention for a cookie-bearing
  client (`CatIntegrationTestBase.CreateClient()`, `AccountsIntegrationTestBase`'s own override,
  per `tests/CatEcommerce.IntegrationTests/README.md`'s explicit guidance on when to override
  `CreateClient()`), not a one-off workaround.
- No assertion was loosened, removed, or made conditional anywhere in this commit; every change
  either narrows a lookup (stronger) or fixes which clock a request's lifetime is measured against
  (a correctness fix, not a leniency fix).

Full-suite verification, clean `git archive 217024a`
- Build: `dotnet restore` + `dotnet build CatEcommerce.sln` in a new scratch folder
  (`scratchpad/ntf5cr`, deleted after the run) — 0 errors, 10 expected NU1900 offline-feed
  warnings.
- `CatEcommerce.Domain.Tests`: 415/415 passed.
- `CatEcommerce.Application.Tests`: 119/119 passed.
- `CatEcommerce.Infrastructure.Tests`: 82/82 passed (up from the fourth pass's 73/73 — the
  intervening TEST-gate commits between the fourth pass and 7fb5cfa added tests outside this
  pass's own diff scope; not re-reviewed here).
- `dotnet ef migrations has-pending-model-changes` (from `src/Infrastructure/CatEcommerce.Infrastructure`,
  startup project `CatEcommerce.Api`): "No changes have been made to the model since the last
  migration." — confirming d5d06da/217024a make no schema change, as claimed.
- `dotnet ef database update` against a freshly created `catecommerce_ntf5cr`
  (`Host=localhost;Port=5432;Database=catecommerce_ntf5cr;Username=postgres;Password=postgres`):
  succeeded from empty, single migration `20260927123315_AddNotificationsModule` applied.
- `CatEcommerce.IntegrationTests` against that same freshly migrated database: 492/492 passed,
  including `CareReminderEndpointTests.TransferCatToNewOwner_BeforeDelivery_Cancelled_
  NextSweepRequestsForTheNewOwner` and `DeduplicationKey_MatchesTheSpecifiedFormat`.
- Database `catecommerce_ntf5cr` dropped and the scratch folder/archive deleted after the run.

FINDINGS

- [MEDIUM] src/Api/CatEcommerce.Api/Background/CareReminderSweep.cs:93 — a cat transferred back to
  its original owner before that due date's `NextDueOn` changes computes the same
  `DeduplicationKey` as the original (now-`Suppressed`) request, so the round-trip owner is not
  guaranteed a fresh reminder — the same class of defect I-F5 fixed for a one-hop transfer, reopened
  for a two-hop (there-and-back) transfer. See VERIFIED (a) above for the full trace. Not a
  duplicate (the unique index and pre-check both still hold; nothing is double-sent) — a missed
  notification on a narrow, twice-transferred-inside-one-window edge case. Routed to the area
  backlog per the gate-blocking rule (owner, 2026-09-27); a real fix needs either a
  transfer-ordinal/generation component on `CatAggregate` folded into the key, or a documented
  decision that this residual gap is acceptable.

SCOPE CHECK
- d5d06da touches `docs/handover/HO-NTF-001.md` (§1 Amended row + §8.12), `CareReminderSweep.cs`
  and `CareReminderEndpointTests.cs` only — all inside the Notification area's own remit and its
  own gate-fix documentation row.
- 217024a touches exactly three integration test files
  (`NotificationClaimAndDeliveryEndpointTests.cs`, `NotificationLogAndInboxEndpointTests.cs`,
  `PasswordResetDeliveryEndpointTests.cs`) — test-only, no production code, no `.ai/**`, no other
  `docs/handover/**` file, no migration.

---

## Fourth pass

GATE: CODE_REVIEW
HANDOVER: HO-NTF-001
COMMITS: 471e52f 0988eda f5f9ece (since third pass, head 5aad2d7 / verdict commit 4f56a2b).
Worktree D:/my-repo/cat_ecommerce-wt/NTF-001, branch ho/NTF-001, head f5f9ece.
DATE: 2026-09-27
STATUS: PASS

Owner rule applied: only a BLOCKER/HIGH finding fails the gate. None found this round.

Scope of this pass: (a) verify the third pass's HIGH (expansion-failure backstop counting
transient read failures) is genuinely closed; (b) verify the master merge (bringing in
ho/FIL-001) lost nothing on either side, in particular rate-limiter/host wiring, SettingKeys,
FileRateLimitPolicies and module registration; (c) verify the regenerated
`AddNotificationsModule` migration contains only Notifications objects, has a correct `Down`,
and leaves `has-pending-model-changes` clean. Full build/test run from a clean `git archive
f5f9ece` in a fresh scratch folder against a fresh database, per instructions.

VERIFIED

(a) Third-pass HIGH (471e52f)
- `ExpandNotificationRequestCommandHandler.HandleAsync` read directly: `recipientDirectory
  .ResolveAsync`, `subjectGuardResolver.IsStillValidAsync`, `branchRepository.GetByIdAsync`,
  `templateRepository.GetByFilterAsync` and `quietHoursCalendar.NextAllowedInstantAsync` (inside
  `BuildExternalAsync`) are all plain EF Core reads — none of these can throw
  `DbUpdateException`, only `NpgsqlException`/`TimeoutException`/etc. `DbUpdateException` is
  raised only by `EfAggregateRootRepository`'s `SaveChangesAsync()`, reached from this handler
  only via `requestRepository.UpdateAsync(...)` (the `Expire`, `SuppressAsync`, and final
  `MarkExpanded` commit points). `NotificationWorkCycle.DispatchAsync`'s new condition —
  `command is ExpandNotificationRequestCommand && ex is DbUpdateException` — is therefore a
  closed, correctly-scoped signal of a genuine save failure, exactly as the commit message
  claims. Read/lookup failures now propagate out of `DispatchAsync`'s try and are simply logged
  and swallowed by the outer catch-all, leaving the request `Pending` for the next poll — no
  `AttemptCount` incremented.
- New regression test `QuietHoursEndpointTests
  .QuietHoursCalendarThrowsRepeatedly_PastMaxExpansionAttempts_StillPendingNeverCounted` drives a
  `TimeoutException`-throwing `IQuietHoursCalendar` fake for `MaxExpansionAttempts + 3` (8)
  consecutive delivery passes and asserts the request stays `Pending`/`AttemptCount == 0`
  throughout, then reaches `Expanded` once the dependency heals. Confirmed this fails on the
  pre-471e52f code: the prior condition (`command is ExpandNotificationRequestCommand`, no
  exception-type filter) would count every one of the 8 `TimeoutException` passes, cross
  `MaxExpansionAttempts` (5) on the 5th, and permanently `Suppress` the request as
  `PersistenceFailed` well before the loop's own healthy-retry check — the asserted `Pending`/
  `AttemptCount == 0` invariant would fail by the 5th iteration. This is a real,
  would-fail-without-the-fix test, not just an assertion of the new code's own behaviour.
- Overflow/22001 path (8fd5a37) still counts and parks correctly, and needed no change here:
  `RenderedMessageColumnLimits.Enforce` throwing `NotificationRenderOverflowException` (an
  `InvalidOperationException`) is caught by the handler's own closed render-exception set (line
  100) *inside* `HandleAsync`, before ever reaching `NotificationWorkCycle` — it suppresses the
  request immediately as `RenderFailed` on the first attempt and never touches `AttemptCount` at
  all (`NotificationOverflowEndpointTests` asserts `AttemptCount == 0` for this path directly).
  The `AttemptCount`/`PersistenceFailed` backstop narrowed by 471e52f exists for a genuine save
  failure on an otherwise-successfully-rendered notification (e.g. a constraint violation at
  commit time), a materially different and still-correctly-handled case; the two paths do not
  interact and neither regressed the other.
- The accompanying LOW fix (`RenderedMessageColumnLimitsTests.TruncationNeverSplitsASurrogatePair`)
  is also correct: the corrected input (98 `a`s + the surrogate pair + one more character) makes
  the naive cut point land exactly on the high surrogate (`text[98]`), so `Truncate`'s back-off
  (`cutAt--`) actually executes — confirmed by hand-tracing `RenderedMessageColumnLimits.Truncate`
  against `TitleMaxLength = 100`. Deleting the back-off line would now make this test fail (a lone
  high surrogate at the end of the truncated result), which was not true of the original input.

(b) Master merge (0988eda) and post-merge regeneration (f5f9ece)
- `SettingKeys.cs` conflict: doc-comment only. Diffed the merged file's body against both parents
  — all pre-existing keys (`BookingChangeCutoffHours` … `CommerceCurrencyCode`) are unchanged and
  present; `SettingKeys.Notifications.cs` (2 keys) and `SettingKeys.Files.cs` (3 keys, from
  ho/FIL-001) both exist untouched as their own partial files. No key lost from either side.
- `CatEcommerceDbContextModelSnapshot.cs` conflict: resolved by regeneration in f5f9ece, not by
  hand-merging — verified below under (c).
- `FileStorageModule.cs`'s rate-limiter comment: only the stale claim ("`UseRateLimiter()` is
  already called once, from `AccountsModule.UseAccountsModule()`") is corrected to describe where
  it actually now lives; the surrounding `AddRateLimiter(...)` registration, `FileUpload`/
  `FileServe` policy bodies and the lazy `GetSection(...).Get<...>()` reads are all byte-identical
  to master's own `cc5737e` version. `FileRateLimitPolicies.cs` is untouched by either side of the
  merge.
- `Program.cs`: exactly one `UseRateLimiter()` call, one `AddAccountsModule`, one
  `AddNotificationsModule`, one `AddFileStorageModule`; no `AddServiceBusProvider` anywhere;
  `AddInMemoryBusServices()` present and the file's own header comment still names it as the only
  correct choice. Nothing here was touched by the merge conflict resolution — grepped directly on
  `f5f9ece` to confirm the post-merge state, not just the merge diff.
- No forbidden-shape residue introduced by the merge or the regeneration: no
  `IBusMessageDispatcher`, `EventPublishOption` other than `InMemoryPublishOnly`, MassTransit,
  RabbitMQ, MongoDB, or a `CommandService`-style facade in any file this pass touched.

(c) Regenerated migration (f5f9ece)
- `20260927123315_AddNotificationsModule.cs`/`.Designer.cs` are the only new migration files;
  `20260927071352_AddFiles.cs` (master's own, from ho/FIL-001) is untouched. Grepped every
  `CreateTable`/`DropTable`/index `name:` in the Notifications migration: `notify.notification`,
  `notify.notification_request`, `notify.notification_template`,
  `notify.notification_template_content`, `notify.notification_attempt`, plus the
  `account.customer_notification_preferences.preferred_channel` column addition — no `stored_file`
  table, no `cat_photo` reference, anywhere in the file.
- The `DELETE FROM clinic.cat_photo;` statement (and every `cat_photo` schema change) exists only
  in `AddFiles.cs`; grepped `AddNotificationsModule.cs` for `Sql(`/`cat_photo`/`DELETE` directly —
  no matches. Not duplicated.
- `Up`/`Down` read directly (not just the designer snapshot): `Up` does `EnsureSchema("notify")`,
  adds `preferred_channel` to the pre-existing `customer_notification_preferences` table, then
  creates the five Notifications tables and their indexes; `Down` drops the five tables in
  dependency order (`notification_attempt`, `notification_request`, `notification_template_
  content`, `notification`, `notification_template`) and then drops `preferred_channel` — a
  correct, symmetric reversal of everything `Up` does, in the right order for FKs.
- Applied this exact migration to a brand-new database (`catecommerce_ntf4cr`, via
  `dotnet ef database update`) from a clean `git archive f5f9ece` — succeeded with no errors, one
  row inserted into `__EFMigrationsHistory`. `dotnet ef migrations has-pending-model-changes`
  against that same freshly-migrated database reports "No changes have been made to the model
  since the last migration." — clean, confirming the regenerated migration and the current model
  agree exactly.

Full-suite verification, clean `git archive f5f9ece`
- Build: `dotnet restore` + `dotnet build CatEcommerce.sln` in a new scratch folder
  (`scratchpad/ntf1-cr4`) — 0 errors, 0 warnings.
- `CatEcommerce.Domain.Tests`: 415/415 passed.
- `CatEcommerce.Application.Tests`: 119/119 passed.
- `CatEcommerce.Infrastructure.Tests`: 73/73 passed.
- `CatEcommerce.IntegrationTests`, against `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;
  Database=catecommerce_ntf4cr;Username=postgres;Password=postgres` (freshly created, migrated
  from empty as above): 483/483 passed.
- All four counts match f5f9ece's own commit-message claims (415/119/73/483) exactly.
- Database `catecommerce_ntf4cr` dropped (`dotnet ef database drop --force`) and the scratch
  folder (`scratchpad/ntf1-cr4`) deleted after the run.

FINDINGS

None at BLOCKER/HIGH/MEDIUM/LOW this pass. The third pass's HIGH is closed correctly and the two
merge/regeneration commits are clean.

SCOPE CHECK
- 471e52f touches only `docs/handover/HO-NTF-001.md`, `NotificationWorkCycle.cs`,
  `RenderedMessageColumnLimitsTests.cs` and `QuietHoursEndpointTests.cs` — all inside the
  Notification area's own remit and its own gate-fix documentation row.
- 0988eda is a merge of master (`cc5737e`, HO-FIL-001) with two conflict resolutions reviewed
  above (`SettingKeys.cs` doc comment, model snapshot regenerated not hand-merged); no other file
  diverges from a straight union of both parents.
- f5f9ece touches only the two migration files and the model snapshot — no other source file.
- No `.ai/**` content file edited, no other `docs/handover/**` file, no Cat/Account/Admin-area
  file, no `*.csproj`/`NuGet.Config`/`global.json` change in any of these three commits.

---

## Third pass

GATE: CODE_REVIEW
HANDOVER: HO-NTF-001
COMMITS: ce1991c 5cf5de4 54faffb 8fd5a37 e9da87f 5aad2d7 (this round's own fix/test/chore commits,
`git log cc78961..5aad2d7` excluding the two intervening gate-record commits 13381f2/ed06705).
Worktree D:/my-repo/cat_ecommerce-wt/NTF-001, branch ho/NTF-001, head 5aad2d7.
DATE: 2026-09-27
STATUS: FAIL

Owner rule applied to this verdict: only a BLOCKER/HIGH finding fails the gate; MEDIUM/LOW are
recorded below but go to the area backlog and would not by themselves fail this pass. One HIGH is
found below, so the gate still fails on its own merits under that rule.

Reviewed every fix in this round against the second pass's own two HIGH findings and the `.ai/`
docs the touched layers implicate (architecture, persistence-ef-core, domain-events-and-outbox,
enumeration-pattern). Session was restarted mid-review; the previous partial scratch build
(`scratchpad/ntf1-cr3`) was deleted and every verification below was re-run from a clean
`git archive 5aad2d7` rather than trusted from memory.

VERIFIED
- Build: `dotnet restore` + `dotnet build CatEcommerce.sln` from a clean `git archive 5aad2d7` in a
  new scratch folder — 0 errors, 0 warnings. Folder deleted after the run.
- Unit tests, no database: `CatEcommerce.Domain.Tests` 324/324, `CatEcommerce.Application.Tests`
  111/111, `CatEcommerce.Infrastructure.Tests` 45/45 — all passed, matching the implementer's own
  claimed counts.
- Migration squash (5aad2d7): exactly one `20260927114728_AddNotificationsModule` migration remains
  under `Migrations/` (the four round-specific migrations and their Designer files are gone); its
  `Up`/`Down` were read directly (not just the designer snapshot) and match every aggregate's own
  `IEntityTypeConfiguration` — `attempt_count`/`last_attempt_on` (`NotificationRequestConfiguration`),
  `resend_ordinal`, `redacted_reason`/`anonymised_on`, `xmin` row versions, all present with the
  right nullability/defaults. `dotnet ef migrations has-pending-model-changes` against the real,
  persistent `catecommerce_ntf` database reports clean, and `dotnet ef migrations list` against that
  same database shows only the one squashed migration, already applied — the "dropped and recreated"
  claim in the commit message checks out.
- Integration suite against `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_ntf;
  Username=postgres;Password=postgres`: 450/450 passed, matching the commit's own count exactly. (A
  first attempt aborted with a Postgres-client-library stack overflow inside `Npgsql.NpgsqlDbType
  Extensions.ToDataTypeName` during test-host teardown, after every test had already reported
  Passed; a clean re-run completed 450/450 with no such crash — logged here as a one-off local
  environment flake, not reproducible, and not a finding against this diff.)
- Second-pass HIGH 1 (`ExpandNotificationRequestCommandHandler`'s broadened render catch swallowing a
  genuine transient DB failure), commit ce1991c: fixed correctly. The catch is now a closed set —
  `InvalidOperationException or FormatException or ArgumentOutOfRangeException` — so
  `quietHoursCalendar.NextAllowedInstantAsync`'s branch-lookup DB read (and every other DB call in
  the method) is never caught here; it propagates out of the handler and is logged/swallowed per-item
  by `NotificationWorkCycle.DispatchAsync`'s own catch-all, leaving the request `Pending` for the next
  poll to retry. Regression test `QuietHoursEndpointTests.QuietHoursCalendarThrows_
  LeavesRequestPendingForRetry_NeverSuppressed` reproduces the exact second-pass scenario with a
  throwing `IQuietHoursCalendar` fake and asserts `Pending`/no `SuppressionReason` after the failing
  pass, then a successful expand once the calendar is healthy — a real, would-fail-without-the-fix
  test. (See the new HIGH below, though: 8fd5a37, later in this same round, reopens a bounded version
  of exactly this risk.)
- Second-pass HIGH 2 (false "I-B11/I-B12 covered" claim in `docs/handover/HO-NTF-001.md`), commit
  e9da87f: fixed correctly. The false claim is corrected inline (the original row now reads "this was
  wrong — corrected below") and a new, dated row records the real coverage. The coverage itself is
  real, not just claimed: `NotificationClaimAndDeliveryEndpointTests.cs`,
  `TrySaveClaimAsync_TwoConcurrentClaims_ExactlyOneWins` loads two independently-scoped tracked copies
  of the same `Queued` notification, calls `Claim` on both, and asserts exactly one `TrySaveClaimAsync`
  succeeds via the real `xmin` row-version column while the other returns `false` cleanly (no
  `DbUpdateConcurrencyException` escaping) — genuine two-writer concurrency against Postgres, not a
  sequential simulation. `TrySaveClaimAsync_LeaseExpired_CanBeReClaimed` and
  `CrashBetweenClaimAndRecordAttempt_RecoversToExactlyOneSend` (A15's at-least-once half) are equally
  real. I-B13 (`RequestNotification_ConcurrentSameDeduplicationKey_ExactlyOneRequestCreated`) fires 10
  concurrent dispatches sharing one dedup key and asserts exactly one row survives the real unique
  index. I-B6 (`TransientFailures_FollowBackoffTable_ThenExhaustToUndeliverable`) drives off the
  notification's own persisted `AttemptCount`/`NextAttemptOn` rather than an assumed pass cadence — a
  more robust shape than a fixed-iteration loop. I-B14 is covered in the same file
  (`ExpandRequest_RecipientAnonymisedSinceRequesting_SuppressedRecipientInactive`).
- P6c column-overflow fix (8fd5a37): `RenderedMessageColumnLimits.Enforce`'s constants
  (`SubjectMaxLength`/`TitleMaxLength`/`BodyMaxLength`/`LinkPathMaxLength`) were checked directly
  against `NotificationConfiguration`'s/`NotificationRequestConfiguration`'s own `HasMaxLength` calls
  (150/100/10000/500) — they match exactly, so a "safe" truncation cannot itself still overflow the
  column. Every one of the three call sites named in the commit message
  (`ExpandNotificationRequestCommandHandler` C4 expand + inbox, `DeliverNotificationCommandHandler`
  C5 fallback, `ResendNotificationCommandHandler` C7 resend) does call `Enforce` immediately after
  `NotificationRenderer.Render`, and each surrounding catch is the same closed, render-only exception
  set — no I/O happens inside any of the three try blocks, so broadening/narrowing them carries no
  infra-swallowing risk (unlike the finding below). `NotificationOverflowEndpointTests` reproduces all
  three shapes end to end against the real database (truncated title/queue unblocked/reset still
  expanded; fallback body overflow ends `Undeliverable` with no fallback row created; cross-channel
  resend overflow falls back to the original channel's fields and returns `201`, never `500`) — real,
  would-fail-without-the-fix tests, not just unit-level checks of `Enforce` in isolation.
- `RecordFailedAttempt`/`AttemptCount`/`PersistenceFailed` concurrency: protected by the same real
  `xmin` optimistic-concurrency column already on `NotificationRequestConfiguration`
  (`builder.Property<uint>("xmin").IsRowVersion()`), so two workers racing to record a failed attempt
  on the same row cannot double-count — one save wins, the other throws
  `DbUpdateConcurrencyException`, which `EfAggregateRootRepository`'s plain `SaveChangesAsync` does
  not swallow anywhere in the call path. No finding here.
- Queue-reordering fix (`NotificationWorkQueue.GetPendingRequestIdsAsync` sorting never-attempted
  requests before previously-failed ones): correct and directly tested
  (`GetPendingRequestIds_NeverAttemptedRequest_SortsBeforeAnOlderPreviouslyFailedOne`, asserting the
  exact ordering against two real rows, one older but already-failed).
- Backslash-disguised-link fix (5cf5de4) and the three ARCHITECTURE second-pass LOW cleanups
  (54faffb): both correct, matching what each commit message claims; regression tests for the former
  use verbatim string literals as the commit message says is necessary, and were confirmed to fail
  against the pre-fix `StartsWith('/') && !StartsWith("//")` check by inspection of the old vs. new
  `IsSiteRelativePath`.
- No forbidden-shape residue introduced this round: no `IBusMessageDispatcher`,
  `AddServiceBusProvider`, a bare C# `enum` for a new value set (`NotificationSuppressionReason
  .PersistenceFailed` is added as an `Enumeration` member, matching `.ai/enumeration-pattern.md`), no
  `CommandService`-style facade, no controller change in this round.

FINDINGS

- [HIGH] src/Api/CatEcommerce.Api/Background/NotificationWorkCycle.cs:82-92 with
  src/Application/CatEcommerce.Application/CommandHandlers/Notification/
  RecordNotificationExpansionFailureCommandHandler.cs and
  src/Domain/CatEcommerce.Domain/Aggregates/Notification/NotificationRequestAggregate.cs:143-155
  (commit 8fd5a37) — the new `AttemptCount`/`PersistenceFailed` backstop fires on ANY exception that
  escapes `ExpandNotificationRequestCommandHandler`, not just a genuine SAVE failure, reopening a
  bounded version of the exact "transient DB hiccup causes silent, permanent data loss" defect this
  same round's own ce1991c commit fixed a few commits earlier.
  - `RecordExpandFailureAsync` is called unconditionally whenever `command is
    ExpandNotificationRequestCommand` and its dispatch throws anything other than a graceful-shutdown
    `OperationCanceledException` — there is no filtering on exception type or on which part of the
    handler threw.
  - But `ExpandNotificationRequestCommandHandler.HandleAsync` makes several real database calls
    outside the closed render try/catch that ce1991c narrowed: `recipientDirectory.ResolveAsync`
    (line 52), `subjectGuardResolver.IsStillValidAsync` (line 56), `branchRepository.GetByIdAsync`
    (line 74), `templateRepository.GetByFilterAsync` (line 83), the `quietHoursCalendar
    .NextAllowedInstantAsync` call ce1991c's own regression test exists for (line 176, inside
    `BuildExternalAsync` but reached from inside the try — its exception type, e.g.
    `NpgsqlException`/`TimeoutException`, is still not in the closed catch filter so it still
    propagates), and finally `requestRepository.UpdateAsync` itself (line 146). A transient failure
    from any of these — not only the final save — now counts toward
    `NotificationRequestRules.MaxExpansionAttempts` (5) and, on the 5th occurrence for the same
    request, permanently `Suppress`es it as `PersistenceFailed`.
  - `Worker:PollIntervalSeconds` defaults to 5 (`NotificationOptions.cs:63`,
    `appsettings.json`), and this codebase configures no `EnableRetryOnFailure`/EF resiliency anywhere
    (`Program.cs:100` is a bare `UseNpgsql(...)`) — so a single transient Postgres blip (a momentary
    connection-pool exhaustion, a brief network partition, a lock wait on the branch row read by both
    `branchRepository.GetByIdAsync` and `quietHoursCalendar`) lasting as little as ~20-25 seconds
    (5 consecutive polls) is enough to permanently and silently suppress the affected request —
    including a password reset, well inside its 15-minute `NotAfter` window — with no operator alert
    beyond a single `LogError` line and no path back to `Pending`.
  - This is a strictly worse outcome, for this specific failure class, than the state ce1991c itself
    left the code in a few commits earlier in this exact round: before 8fd5a37, the same
    `quietHoursCalendar`-throws scenario left the request `Pending` and retried forever (the
    documented, tested, and explicitly intended behaviour — see
    `QuietHoursEndpointTests.QuietHoursCalendarThrows_LeavesRequestPendingForRetry_NeverSuppressed`'s
    own doc comment: "the request is left `Pending` -- retried on the very next pass, ... never
    touched by `SuppressAsync`"). After 8fd5a37, that same test still passes (it only exercises one
    failed pass), but the underlying guarantee it was written to prove no longer holds past the 5th
    consecutive failure — an interaction no test exercises.
  - The commit message and `NotificationWorkCycle.cs`'s own doc comment both describe this as
    "A SAVE-time failure ... (any cause ...)" and `docs/handover/HO-NTF-001.md` item (43) as "5 failed
    SAVE attempts" — but the mechanism, as written, is not scoped to save failures at all; it is
    scoped to "any exception that escapes the whole command handler after the render try/catch,"
    which is a materially broader and already-once-rejected-in-this-round failure class.
  - Not caught by any test: `RecordNotificationExpansionFailureCommandHandlerTests` only exercises
    `RecordFailedAttempt`/the handler in isolation (seeding a request already at N prior failures via
    direct method calls, never via a real exception path), and
    `QuietHoursCalendarThrows_LeavesRequestPendingForRetry_NeverSuppressed` only drives one failed
    pass, never five — there is no test proving (or disproving) what happens to a persistently
    (but not permanently) failing non-render dependency across enough consecutive polls to cross
    `MaxExpansionAttempts`.
  - Fix: scope the backstop to genuine save failures only — e.g., have
    `ExpandNotificationRequestCommandHandler` catch around just the final
    `requestRepository.UpdateAsync`/`enlister.Enlist` commit (or a specific, closed exception set
    proven to originate there, such as `DbUpdateException`/`DbUpdateConcurrencyException`) and record
    the failed attempt from inside that catch, rather than inferring "it must have been the save" from
    whatever happens to escape the whole handler in `NotificationWorkCycle`. Anything from
    `recipientDirectory`, `subjectGuardResolver`, `branchRepository`, `templateRepository`, or
    `quietHoursCalendar` should keep propagating and retrying indefinitely, exactly as ce1991c already
    established and tested a few commits earlier.

- [LOW] tests/CatEcommerce.Domain.Tests/DomainServices/RenderedMessageColumnLimitsTests.cs:52-71
  (`TruncationNeverSplitsASurrogatePair`, commit 8fd5a37) — does not actually exercise the
  back-off branch it is named for.
  - `title = new string('a', TitleMaxLength - 1) + cat` (99 `a`s + a 2-code-unit surrogate pair,
    `TitleMaxLength = 100`) makes `RenderedMessageColumnLimits`'s internal cut point
    `cutAt = maxLength - Ellipsis.Length = 99`, so the truncated slice `text[..99]` is exactly the 99
    `a`s and never touches the surrogate pair at all — the back-off check
    (`char.IsHighSurrogate(text[cutAt - 1])`) sees `text[98] == 'a'`, not a high surrogate, and never
    fires.
  - The property-style assertion loop (no lone surrogate anywhere in the result) is still a
    legitimate, correct check of the overall contract, and passes — but it would pass even if the
    back-off `cutAt--` line were deleted entirely, since this specific input never reaches it. The
    logic itself was read directly and is correct (confirmed by hand-computing a case that does hit
    it: a surrogate pair starting exactly at index `cutAt - 1`), so this is a test-quality gap, not a
    product bug.
  - Fix: construct the input so the naive cut point lands ON the high surrogate, e.g.
    `new string('a', TitleMaxLength - 2) + cat + "a"` (98 `a`s, then the pair, then one more
    character) — `cutAt = 99` there lands on `text[98]` being the high surrogate, exercising the
    actual back-off adjustment.

Everything else in the second pass's own FINDINGS list is verified fixed above (both HIGHs) or was
already resolved by the first pass and not reopened this round.

SCOPE CHECK
- Every file touched in `cc78961..5aad2d7` (excluding the two gate-record commits) is inside the
  Notification area's own Application/Domain/Infrastructure/Api/Shared files, their one squashed
  migration, `docs/handover/HO-NTF-001.md`'s own §1 Amended row, and their own tests. No `.ai/**`
  content file, no other `docs/handover/**` file, no `web/**`, no `*.csproj`/`NuGet.Config`/
  `global.json`, no Cat/Account/Admin-area file. One migration remains after the squash
  (`AddNotificationsModule`, regenerated), purely additive relative to master, applied to and
  matching `catecommerce_ntf`'s `__EFMigrationsHistory`.

---

## Second pass

GATE: CODE_REVIEW
HANDOVER: HO-NTF-001
COMMITS: `git diff 24f685e..4efe96f` (branch's own commits): 50395e6 ca6d3ea 0b7f201 7d08879 c91d411
482136c 4a7e967 1132c4d 027b1dc a2705b8 d875a8f b496f22 9a9f25f ff449e2 1708e16 f1839e9 9d560b6
e03f5d2 (merge 97aa959 — only its own conflict resolutions reviewed, see below) 58c7d9e d6eb049
b755903 4efe96f. Worktree D:/my-repo/cat_ecommerce-wt/NTF-001, branch ho/NTF-001, head 4efe96f.
DATE: 2026-09-27
STATUS: FAIL

Reviewed the fix round line by line against the first pass's own BLOCKER/HIGH/MEDIUM/LOW list
above and against `.ai/` (architecture, domain-events-and-outbox, di-registration-guide,
persistence-ef-core, cqrs, command-handler-pattern). For merge commit `97aa959` (master →
ho/NTF-001, bringing in `ho/CAT-001-gates`), only the merge's own conflict resolutions were
reviewed, not the full incoming CAT-001 diff (already gated on its own branch): `git diff <each
parent> 97aa959` shows exactly three files with a real three-way merge — `appsettings.json`
(Notifications' own config block combined with the new shared `PublicSite` block; correct, no data
lost from either side), `CatEcommerceDbContextModelSnapshot.cs` (the `WidenAuditEntryValues`
snapshot merged in verbatim), and `HostCompositionTests.cs` (master's own new test cases added,
no Notifications logic touched). No other file in the merge diverges from a straight union of both
parents, so no additional review was needed there.

VERIFIED
- Build: `dotnet restore` + `dotnet build CatEcommerce.sln` from a clean `git archive 4efe96f` in a
  new scratch folder — 0 errors, 0 warnings. Folder deleted after the run.
- Unit tests: `CatEcommerce.Domain.Tests` 311/311, `CatEcommerce.Application.Tests` 107/107,
  `CatEcommerce.Infrastructure.Tests` 45/45 — all passed (counts up from the first pass's 284/86/39,
  reflecting this round's new regression tests).
- Integration suite run twice against `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;
  Database=catecommerce_ntf;Username=postgres;Password=postgres` (a real, persistent, already-
  migrated database — `__EFMigrationsHistory` already carried both of this round's migrations,
  `AddNotificationRedactionReason` and `AddNotificationResendOrdinal`, applied and matching): 439/439
  both times. Re-running twice specifically to probe 9d560b6's flakiness fix (see below) — no
  failure either run.
- BLOCKER 1 (fallback chain never exhausted), commit 7d08879: fixed correctly.
  `TryBuildFallbackAsync` → `TriedChannelsForChainAsync` now walks `FallbackFromId` back through
  sibling `NotificationAggregate` rows, accumulating every channel already tried for the whole
  request, bounded by `NotificationChannelRules.DeliverableChannels.Count` hops as a defensive
  guard — this terminates even against a corrupt/cyclic `FallbackFromId` graph, since the hop count
  is bounded independently of whatever the chain actually contains. Regression test
  `NotificationRoutingEndpointTests.SmsAndEmailBothPermanentlyFail_EndsUndeliverable_
  NoInfiniteFallbackLoop` (I-B7) runs 5 delivery passes with both channels permanently failing and
  asserts exactly 2 rows, both `Undeliverable` — this is a real reproduction: reverting `tried`'s
  seed to `{ original.Channel }` makes the test fail (a 3rd/4th row appears). The same commit also
  broadens the fallback-render catch from `InvalidOperationException` to any non-cancellation
  exception; that catch wraps only `NotificationRenderer.Render` (pure, in-memory, no I/O), so
  broadening it introduces no infra-swallowing risk — unlike b496f22's broadened catch elsewhere
  (BLOCKER 3 below).
- BLOCKER 2 (redaction sweep starvation), commit 0b7f201: fixed correctly and minimally — the
  missing `r.VariablesRedactedOn == null` filter is added, mirroring the notification-side sibling
  query exactly. Regression test
  `PasswordResetDeliveryEndpointTests.RedactionSweep_WithBatchSizeSmallerThanBacklog_
  StillReachesNewerRequest` reproduces the starvation directly with `BatchSize=1` and two expired
  requests, and — after 9d560b6 hardened it (see below) — drains the actual due-backlog count
  rather than a guessed fixed pass count, so it stays correct on a shared, growing database.
- HIGH 1 (anonymisation no-op on already-redacted rows), commit c91d411: fixed correctly.
  `NotificationRedactionReason` (`Secret` | `Anonymisation`) is recorded on the aggregate; a new,
  independent `AnonymisedOn` marker (separate from `RedactedOn`) makes an `Anonymisation` redaction
  idempotent on its own right and still nulls `Destination`/`UnsubscribeLink` even when the row was
  already `Secret`-redacted. `AnonymiseNotificationsOnCustomerAnonymisedHandler` now filters on
  `AnonymisedOn == null` (not `RedactedOn`) and also walks
  `INotificationRecipientDirectory.FindMergedDuplicatesAsync` (a bounded reverse BFS of
  `MergedIntoCustomerId`, mirroring the existing forward walk) to reach a merged-away duplicate's
  own external rows, which — per §11.6 — keep the pre-merge `RecipientId` and would otherwise never
  be visited (a merged-away customer can never itself be anonymised). Checked idempotency and
  double-work directly: each `customerId` in `targetIds` (survivor + duplicates) is queried and
  updated independently by `RecipientId`, so there is no overlap between the survivor's own rows and
  a duplicate's; the `while (... AnonymisedOn == null)` loop shape means a second run of the handler
  finds nothing left to do. Regression tests are real: `NotificationLogAndInboxEndpointTests.
  Merge_MovesInboxItemToSurvivor_ExternalLogKeepsOriginalRecipient` (I-D10) and `Anonymise_
  RedactsSurvivorAndMergedDuplicateNotifications_EvenIfAlreadySecretRedacted` (I-D11) — the latter
  seeds a Secret-redacted password-reset row on the duplicate specifically, merges, anonymises the
  survivor, and asserts the duplicate's own row's `destinationMasked` is now null; this fails
  against the pre-fix code on both counts (wrong filter, no duplicate walk).
- HIGH 2 (live reset token in log detail) + read-side-pattern Select projection, commit 027b1dc:
  fixed correctly. `GetNotificationDetailQueryHandler` now shows `"[redacted]"` for
  body/subject/title whenever `ContainsSecret` is true, independent of the persisted `RedactedOn` —
  closing the live window (`Queued`/`Sending`/`RetryPending`) the first pass's SECURITY probes P1/P1b
  exploited — and both this handler and `GetNotificationTemplateQueryHandler` now `Select`-project
  instead of `Include`-ing and materialising the aggregate root, matching every other read handler.
  `EffectiveNotificationTemplate` gained a second, already-projected-content overload so the read
  side never constructs a `NotificationTemplateAggregate`. Regression test
  `PasswordResetDeliveryEndpointTests.LogDetail_OfQueuedOrRetryPendingResetNotification_
  NeverShowsTheLiveToken` reproduces both SECURITY probe windows directly (plain `Queued`, and
  `RetryPending` after a scripted `TransientFailure`) and asserts no response ever contains
  `"token="` — a real, would-fail-without-the-fix test.
- MEDIUM (unsubscribe hits the pre-merge duplicate's preferences, not the survivor's), commit
  9a9f25f: fixed correctly. `UnsubscribeFromMarketingCommandHandler` now resolves
  `notification.RecipientId` through `INotificationRecipientDirectory.ResolveAsync` (the same
  merge-aware walk routing already uses) before loading/creating the preferences aggregate.
  Regression test `ValidToken_RecipientWasMergedIntoSurvivor_RevokesSurvivorsConsent_
  NotTheDuplicates` uses a new hand-written `FakeNotificationRecipientDirectory` test double and
  asserts the survivor's preferences are updated and the duplicate's repository is never touched.
- MEDIUM (shutdown `OperationCanceledException` logged as `Error`) + `TimeProvider` discipline,
  commits d875a8f (and the closely related pipeline-order commit 1708e16, reviewed together):
  fixed correctly. `NotificationWorkCycle.DispatchAsync` now has its own
  `catch (OperationCanceledException) when (ct.IsCancellationRequested)` ahead of the generic catch,
  matching `NotificationWorker.ExecuteAsync`'s own filter — no behaviour change for a real per-item
  failure, only quieter shutdown logs. `NotificationWorker`'s sweep scheduling and
  `NotificationPasswordResetTokenDispatcher`'s expiry-minutes calculation now read `TimeProvider`
  instead of `DateTimeOffset.UtcNow`. No dedicated test for either (mechanical, LOW severity) — not
  a concern.
- MEDIUM (C7 "re-rendered only when the channel differs" not honoured), commit ff449e2:
  fixed correctly. `ResendNotificationCommandHandler.RenderForChannelAsync` re-renders from the
  original request's own variables whenever the target channel differs, falling back to reusing the
  original rendered fields only if the definition/request/content cannot be found. Regression:
  `Resend_UndeliverableSmsOnEmail_Creates201WithNewIdAndAuditRow` extended to assert the resent
  Email carries a `Subject` (the Sms original has none) — a real, would-fail-without-the-fix
  assertion.
- Resend-limit race (SECURITY LOW), commit e03f5d2: correct design. `ResendOrdinal` (1-based
  position in the chain) backs a unique filtered index `ix_notification_resend_of_id_ordinal` on
  `(ResendOfId, ResendOrdinal) WHERE resend_of_id IS NOT NULL`; `EfNotificationResendStore.
  TrySaveAsync` translates a lost race (`PostgresErrorCodes.UniqueViolation` on that exact
  constraint name) into `false` → `ResendLimitReached`, the same violation a correctly-counted-but-
  too-late resend already gets. The pre-existing `NotificationResendEvaluation.Evaluate` check
  (using `priorResends`) still gates entry before the DB even sees a write attempt; the unique index
  is the belt-and-braces layer for the race window between that check and the insert. Migration
  `AddNotificationResendOrdinal` is purely additive and correctly reversible (`Up`/`Down` reviewed
  directly, not just the designer snapshot). Regression test `ConcurrentResends_
  NeverExceedTheLimit` fires 10 concurrent resends and asserts the created count never exceeds the
  limit — correctly scoped to what the fix actually guarantees (never-exceeds, not exactly-reaches,
  per the commit's own honest caveat).
- `PublicSiteOptions` switch, commit 58c7d9e (plus its own conflict resolution inside merge
  97aa959): correct. `Notifications:WebBaseUrl` removed; `NotificationLinkBuilder.Base()` now reads
  `IOptions<PublicSiteOptions>.WebBaseUrl`, validated once by `PublicSiteStartupGuardHostedService`
  rather than duplicating host/scheme checks in `NotificationOptionsValidator`. `ApiBaseUrl` (no
  shared equivalent) is untouched. `HostCompositionTests`' two Production cases now pin
  `Notifications:ApiBaseUrl` so its own `[Required]` doesn't pre-empt the specific seam each test
  names — confirmed this is necessary and not a workaround for a bug, since ASP.NET Core's options
  `ValidateOnStart` does not aggregate failures across option types.
- Order-independent test data (SECURITY's flakiness note), commit 9d560b6: **does remove the
  specific state dependency SECURITY described.** The prior fixed historical `FakeTimeProvider`
  literals (e.g. `2026-06-16T06:00:00Z`), reused by every run against a shared, persistent,
  never-truncated database, meant repeated suite runs kept writing more rows into the exact same
  instant, so a `from`/`to` window built around that literal isolated nothing across runs.
  `SafeDaytimeInstant()` anchors to the real current instant instead (shifted into the same safe
  Asia/Dhaka-daytime UTC band `[2,15)` a fixed literal would have used — verified the hour-shift
  arithmetic covers all 24 possible starting hours without gaps). Ran the integration suite twice in
  a row against the real, already-populated `catecommerce_ntf` database specifically to probe this:
  439/439 both times, no `SearchByRecipient`/`Unsubscribe` flake. Residual, non-blocking observation:
  within a single suite run, `NotificationLogAndInboxEndpointTests.
  SearchByRecipientDestinationTemplateStatus_FindsMatchingRows_DestinationsMasked`'s `byTemplate`/
  `byDestination` sub-assertions still query by `templateKey`/`destination` and a `±5min` window
  with no `recipientId`, and only assert `Contains(... recipientId == customerId)` rather than an
  exact, self-scoped result — if enough *other* tests in the same run share both the same template
  key and an overlapping window (plausible under parallel test-class execution, since
  `SafeDaytimeInstant()` calls made within the same few seconds land in the same window), the paged
  first page could in principle still omit this test's own row. Not raised as a finding: SECURITY's
  reported failure mode was specifically cross-run accumulation against a fixed literal, which this
  fix does eliminate, and two consecutive full runs here did not reproduce the residual risk.
- Poison template/value validation, commits 50395e6 and ca6d3ea: fixed correctly.
  `ControlCharacters.ContainsPrivateUseOrFormat` (Unicode `Co`/`Cf` categories) closes the path by
  which the renderer's own internal sentinel characters (U+E000/U+E001) could reach it as literal
  admin-authored template text or a request variable and crash every later render — wired into
  `TemplateRules.Validate`, `NotificationRequestAggregate.Create`, and a new
  `TemplateRules.ValidateSampleOverride` for preview's caller-supplied samples (length cap,
  control/PUA rejection, `Url` must be https, `Date` must be `yyyy-MM-dd`). `contents:[null]`, an
  unrecognised channel name, and both save and preview's own unguarded `Enumeration.FromDisplayName`
  calls are now validator/handler-level 400s instead of NPEs/500s, on both the save and preview
  paths. Ten new regression tests across `ControlCharactersTests`, `TemplateRulesTests`, and
  `NotificationTemplateEndpointTests` — each names the specific pre-fix 500/crash it reproduces and
  would fail without the corresponding guard.

FINDINGS

- [HIGH] src/Application/CatEcommerce.Application/CommandHandlers/Notification/
  ExpandNotificationRequestCommandHandler.cs:84-107 (commit b496f22) — the broadened poison-render
  catch now also swallows a genuine, retriable infrastructure failure and turns it into permanent,
  silent notification loss, which is worse than the "stuck Pending forever" bug it replaces.
  - The fix (correctly, in isolation) broadens `catch (InvalidOperationException ex)` to
    `catch (Exception ex) when (ex is not OperationCanceledException)` around the block that builds
    the outgoing notification(s), so that a malformed Date/Url variable value (`FormatException`) or
    a sentinel-character render bug (`ArgumentOutOfRangeException`) suppresses the request
    immediately with `NotificationSuppressionReason.RenderFailed` instead of leaving it stuck
    `Pending` and blocking every other pending request behind it. That part is right.
  - But the try block's scope was never narrowed to match: `BuildExternalAsync` (line 128, inside
    the try at line 84) calls `await quietHoursCalendar.NextAllowedInstantAsync(now,
    request.BranchId)`, whose Infrastructure implementation
    (`src/Infrastructure/.../Notifications/QuietHoursCalendar.cs:19`) does
    `await branchRepository.GetByIdAsync(effectiveBranchId)` — a real database read, for every
    `Normal`-urgency external notification (i.e. most of them), not just poison ones.
  - Before this commit, only `InvalidOperationException` was caught here — a genuine Npgsql
    connectivity blip, timeout, or transient failure during that branch lookup would surface as
    `NpgsqlException`/`TimeoutException`/similar, propagate out of the handler uncaught, and be
    logged and swallowed one level up by `NotificationWorkCycle.DispatchAsync` — leaving the request
    untouched (still `Pending`) so the **next poll retries it**, which is the correct behaviour for
    a transient infra failure.
  - After this commit, that same transient DB failure is caught by the new broad clause, logged as
    `"Rendering failed"`, and the request is immediately, permanently `Suppress`ed with
    `RenderFailed` — a terminal status. The notification (which may be a password reset, or any
    other transactional message) is silently and irrecoverably dropped on a momentary database
    hiccup, rather than retried. This is a regression introduced by this exact fix, and it is worse
    than the bug being fixed: "stuck forever, retried every poll" is merely a nuisance (and, after
    BLOCKER 3/0b7f201-style batch-starvation reasoning, only for the one poison row); "silently and
    permanently discarded on any transient DB error touching a legitimate request" is data loss.
  - Contrast with the fallback-render catch in `DeliverNotificationCommandHandler.
    TryBuildFallbackAsync` (also broadened, by 7d08879, in the same fix round): that catch wraps
    only `NotificationRenderer.Render` — pure, in-memory, no I/O — so broadening it introduces no
    such risk. The `DeliverNotificationCommandHandler.SendAsync` catch (line 102, pre-existing, not
    touched this round) also wraps a real I/O call (the provider send) but correctly classifies
    *any* failure there as `TransientFailure` (retried with backoff), not a terminal suppression —
    the right pattern, not followed here.
  - Fix: narrow the try block in `ExpandNotificationRequestCommandHandler` to wrap only the actual
    rendering calls (`NotificationRenderer.Render` inside `BuildExternalAsync`/`BuildInbox`), not the
    `await quietHoursCalendar.NextAllowedInstantAsync(...)` call that precedes them — or catch a
    specific, closed set of render-failure exception types (`FormatException`,
    `ArgumentOutOfRangeException`, the existing `InvalidOperationException`) rather than `Exception`,
    so a database exception type is never caught here at all and continues to propagate for a normal
    retry on the next poll.
  - Not caught by any of this round's new tests: `MalformedDateVariable_
    SuppressesRequestOnFirstAttempt_NeverStuckPending` only exercises the poison-value path (the
    render call itself throwing), never a failure inside `quietHoursCalendar` — there is no test
    that would catch this regression, and none is expected to exist given the try block's current
    (too-wide) scope.

- [HIGH] Documentation/coverage mismatch: `docs/handover/HO-NTF-001.md` §1 Amended's own
  2026-09-27 gate-fix row claims "I-B11/I-B12 (claim/lease at-least-once, no double claim — covered
  by the existing claim-store tests, re-verified)", but no such test exists anywhere under `tests/`.
  - Searched the whole tree for `TrySaveClaimAsync`, `EfNotificationClaimStore`, `LeaseSeconds`, and
    any `Task.WhenAll`/`Parallel.ForEachAsync`-shaped concurrency test touching claim/lease
    semantics — the only concurrency-style test added this round is
    `ConcurrentResends_NeverExceedTheLimit` (resend limit, unrelated to claiming) and
    `SmsAndEmailBothPermanentlyFail_...` (sequential passes, not concurrent). A15 ("Delivery is
    at-least-once and never double-claimed") remains statically plausible from reading
    `NotificationAggregate.CanClaim`/`Claim` and `EfNotificationClaimStore`'s unique-claim shape, but
    is not proven by any test, exactly as the first pass found — the handover record asserting
    otherwise is inaccurate and should be corrected, not relied on by a later gate as evidence this
    is done.
  - I-B14 (anonymised customer with a pending request → `Suppressed RecipientInactive`) is also
    still untested — not mentioned in the handover's own "Test additions" list at all (unlike
    I-B11/I-B12, which are at least claimed), and not found in any test file.
  - `docs/test-cases/TC-HO-NTF-001.md` still does not exist (checked directly:
    `docs/test-cases/` contains only ACC/ADM/CAT files) — §13 lists it as in scope; there is still
    no traceability sheet for the TEST gate to check §16 rows off against.
  - Routed to TEST as well as recorded here, per the first pass's own convention for coverage
    findings inside this gate's worker/idempotency remit.

- [LOW, informational] `NotificationWorkQueue`/other worker-side catches were re-checked for the
  same broad-catch-swallows-infra-failure shape the HIGH finding above describes, since the task
  asked for this specifically across all three poison-handling commits (50395e6, ca6d3ea, b496f22).
  50395e6's `TryBuildFallbackAsync` catch and ca6d3ea's controller/validator/handler null-and-channel
  guards wrap no I/O (pure validation and in-memory rendering) — no equivalent risk found in either.
  The risk is specific to b496f22's `ExpandNotificationRequestCommandHandler` catch, above.

Everything else in the first pass's FINDINGS list (the two remaining first-pass MEDIUMs — the
`UnsubscribeFromMarketingCommandHandler` merge-survivor bug and the `NotificationWorkCycle` shutdown
log noise — and both first-pass LOWs, the `GetDueCareRemindersQueryHandler` re-derivation and the
`NotificationPasswordResetTokenDispatcher` raw-`UtcNow` use) is verified fixed above and not
repeated. The constant-time-compare note from the first pass was not re-examined (unchanged this
round).

SCOPE CHECK
- Every file touched in `24f685e..4efe96f` is inside the fix round (Notification-area
  Application/Domain/Infrastructure/Api/Shared files, their migrations, and their own tests) or is
  an owner-authorised cross-area host-composition change already called out in the commit messages
  (`HostPipelineExtensions.cs`/`Program.cs`/`AccountsModule.cs` rate-limiter move, `.ai/host-
  composition.md`). No `.ai/**` content file was edited (only the one doc explicitly authorised for
  the rate-limiter move), no `docs/handover/**` file other than HO-NTF-001's own §1 Amended row, no
  `web/**`, no `*.csproj`/`NuGet.Config`/`global.json` change, no Cat-area file. Two new migrations
  (`AddNotificationRedactionReason`, `AddNotificationResendOrdinal`) — both purely additive, both
  `Up`/`Down` reviewed directly (not just the designer snapshot), both already applied to
  `catecommerce_ntf` and matching this head's `__EFMigrationsHistory` exactly.

---

## First pass

GATE: CODE_REVIEW
HANDOVER: HO-NTF-001
COMMITS: `git diff master...40a3373` (5b0d077 8809cfa b5e687a feec341 e17f2b6 799e006 1ad2e7f d8d9fc5
(merge of master) b700311 0eb7969 91c6eb9 73a0aa5 9a9e739 fb161fb a864a31 ef20c3d 6f215ee 40a3373).
Task-specified head is 40a3373. The branch tip has since moved to 379d2de ("Record HO-NTF-001
ARCHITECTURE first pass (FAIL)"), which adds only `docs/gates/HO-NTF-001-ARCHITECTURE.md` — no
source file — so it does not change anything reviewed here. That ARCHITECTURE file was read as
corroborating context (see below) but is not itself part of this review's diff.
DATE: 2026-09-27
STATUS: FAIL

Reviewed `git diff master...40a3373` (196 files) against `docs/handover/HO-NTF-001.md` (incl. §1
Amended) and `.ai/` (architecture, domain-events-and-outbox, di-registration-guide,
bounded-contexts, read-side-pattern, persistence-ef-core, cqrs, command-handler-pattern). Built and
ran unit tests from a clean `git archive 40a3373` in a scratch folder (no git-ignored appsettings);
the scratch folder has been deleted. Also read the already-present
`docs/gates/HO-NTF-001-ARCHITECTURE.md` (first pass, FAIL, same head) — three of the findings below
were reached independently while reading the same handler/aggregate code and are noted as
corroborating that gate's HIGH 1, HIGH 2 and HIGH 3; they are repeated here because they sit
squarely inside this gate's own remit (§19 row 3: "idempotency of C3–C6 … the worker never throws
out of `ExecuteAsync` … no provider call inside a database transaction … time only in
`QuietHours`") and because a Critical bug that blocks merge belongs in every gate that finds it, not
only the first.

VERIFIED
- Build: `dotnet restore` + `dotnet build CatEcommerce.sln` — 0 errors (10 NU1900 warnings, the
  offline private feed, expected and unrelated).
- Unit tests, no database: `CatEcommerce.Domain.Tests` 284/284 passed,
  `CatEcommerce.Application.Tests` 86/86 passed, `CatEcommerce.Infrastructure.Tests` 39/39 passed.
  No integration tests were run (require PostgreSQL), per instructions.
- The two named bug fixes are both correct: `QuietHours.NextAllowedInstant` now normalises
  `BranchCalendar.ToInstant`'s branch-offset `DateTimeOffset` with `.ToUniversalTime()` before
  returning it, so a non-UTC branch no longer crashes `timestamptz` persistence; every caller
  (`QuietHoursCalendar`, both command handlers) feeds it `TimeProvider.GetUtcNow()`, not
  `DateTimeOffset.Now`. The unsubscribe-token hash mismatch is fixed: both
  `ExpandNotificationRequestCommandHandler.IssueUnsubscribeToken` and
  `UnsubscribeFromMarketingCommandHandler.Hash` now hash the UTF-8 bytes of the token's own
  base64url **string** form, not the raw random bytes, so issuance and verification agree.
- `DeliverNotificationCommandHandler` never calls the provider while a transaction is open: the
  claim is saved (`Claim` + `TrySaveClaimAsync`) **before** `sender.SendAsync`, and `RecordAttempt`'s
  own commit happens strictly after `SendAsync` returns. A sender exception is turned into
  `TransientFailure` with the exception's **type name** only, never `ex.Message` (which could echo
  the destination) — correct per §8.9 rule 5 / §20.4.
- `NotificationWorker.ExecuteAsync` never throws: the pass and the delay are each in their own
  try/catch, `OperationCanceledException` is excluded from the pass's catch-and-log, and the delay's
  own cancellation is swallowed silently. `NotificationWorkCycle` gives each item a fresh scope from
  `IServiceScopeFactory`, separate from the outer per-poll scope — matches
  `.ai/di-registration-guide.md`'s "one legitimate Singleton that needs a Scoped service".
- C3 (`RequestNotificationCommandHandler`), C4 (`ExpandNotificationRequestCommandHandler`), C6
  (`RedactNotificationSecretsCommandHandler`) and C8 (`MarkInboxItemReadCommandHandler`) are each
  idempotent as specified: a second run on an already-past-`Pending`/terminal/already-redacted row
  returns success and writes nothing. C5 is idempotent per notification but see BLOCKER 1 below for
  what its per-call idempotency does not prevent. Both domain event handlers
  (`ReassignInboxOnCustomerMergedHandler`, `AnonymiseNotificationsOnCustomerAnonymisedHandler`) use
  the accepted loop-until-none-match shape and are resumable after a partial failure — but see
  HIGH 1 for a defect in what `AnonymiseNotificationsOnCustomerAnonymisedHandler`'s target method
  actually does.
- Controllers: all four new controllers inject `IInMemoryDispatcher` (never `IDispatcher`), no
  controller holds a `DbContext`, repository or aggregate, and status codes match §12.4 (`204`/`200`/
  `201`/`400`/`409`/`404`; never `202`). `MarkInboxItemReadCommandHandler` returns `NotificationNotFound`
  (→ `404`) for another customer's item, never a `403`, matching §20.4-adjacent non-disclosure intent.
- Validators are all `AbstractValidator<T>` registered as `IValidator<T>` in `NotificationsModule`
  (checked the registration lines, not just the class declarations).
- Read side: every query handler uses `AsNoTracking()`; `PreviewNotificationTemplateQueryHandler` is
  pure with no repository, as specified. (`GetNotificationDetailQueryHandler` and
  `GetNotificationTemplateQueryHandler` materialize the aggregate root via `Include` rather than a
  `Select` projection — ARCHITECTURE's HIGH 4 already covers this; noting only that the same idiom
  is pre-existing elsewhere in this codebase, e.g. `BranchQueryHandlers.cs`, so it is not a
  Notifications-specific novelty even though ARCHITECTURE is right that it should be a projection.)
- The 7 extra Domain ports beyond §8.13/§13's list
  (`INotificationChannelSenderResolver`, `INotificationClaimStore`, `INotificationDeliverySettings`,
  `INotificationEnlister`, `INotificationRequestStore`, `INotificationResendCounter`,
  `ISubjectGuardResolver`) are each a narrow, single-method seam whose doc comment states exactly
  which Application→Infrastructure layering violation it avoids (an EF/Npgsql exception type, a
  concrete Infrastructure resolver type, or `IOptions<NotificationOptions>`). None wraps a whole
  repository call behind a facade the way ADR-015's forbidden `CommandService` does — each is called
  from inside a real command handler that also does its own aggregate loading and business logic.
  `INotificationEnlister.Enlist(AggregateRoot)` is the one of the seven worth a second look: it is
  untyped, so nothing stops a future handler from enlisting an unrelated aggregate into someone
  else's commit. Today it is only ever called with a fallback `NotificationAggregate` or an audit
  entry, both of which ADR-056/§11.4 C7 explicitly allow — but the interface itself does not enforce
  that. Suggestion, not a blocker: narrow the signature to the two permitted types, or make the
  method internal to the Notification command handlers.
- `AccountsModule.cs`/`NotificationsModule.cs`: the two old password-reset dispatcher classes
  (`DevelopmentPasswordResetTokenDispatcher`, `UndeliveredPasswordResetTokenDispatcher`) are deleted
  cleanly — no remaining reference anywhere in `src/` or `tests/`. `AccountOptions.WebBaseUrl` is
  removed with no dangling reference. `RequestPasswordResetCommandHandler` now dispatches before
  `SaveAsync(token)` in both the customer and staff branches, so the token and its notification
  request commit atomically (F23), as specified.
- `appsettings.json`'s `Notifications:WebBaseUrl`/`ApiBaseUrl` use RFC 2606 `.invalid` placeholders
  rather than empty strings or a real host — correctly explained by 40a3373's own commit message
  (fixes a cross-area `ValidateOnStart` regression). ARCHITECTURE's MEDIUM finding that this defeats
  ADR-061's "fail safe and loud" for a production deploy that forgets to override the setting is a
  fair point but belongs to that gate's ADR-061 lane, not repeated here as a blocker.
- No forbidden-shape residue anywhere in the new Notification files: no `IBusMessageDispatcher`,
  `AddServiceBusProvider`, `EventPublishOption` other than `InMemoryPublishOnly`, MassTransit,
  RabbitMQ, MongoDB, `202`, or a `CommandServices`-style facade.

FINDINGS

- [BLOCKER] src/Application/CatEcommerce.Application/CommandHandlers/Notification/DeliverNotificationCommandHandler.cs:159-161 — the fallback chain never exhausts; a permanently-failing recipient produces an unbounded stream of new `NotificationAggregate` rows.
  - `TryBuildFallbackAsync` seeds `tried` with only `original.Channel`: `var tried = new
    HashSet<NotificationChannel> { original.Channel };`. It never walks `FallbackFromId` back to the
    root or otherwise accumulates every channel already tried for this **request**.
  - Consequence: a recipient with both a mobile number and an email address, on a template both
    channels are permitted for, that fails on both channels (the default production state, since
    `Unconfigured` senders always return `PermanentFailure` — see §4.3) alternates forever. SMS fails
    → falls back to Email (`tried={Sms}` when building that one fallback). The Email notification
    itself then fails on its own delivery attempt; `TryBuildFallbackAsync` is called again with
    `tried={Email}` (seeded from *that* notification's own channel, not the original's), so
    `NextFallback` returns SMS again, and a third notification is enlisted. This repeats without
    bound, one new row per `Worker:PollIntervalSeconds` (5 s default) per affected notification,
    forever.
  - This is precisely the scenario I-B7 ("both channels permanently failing") is supposed to prove
    does *not* happen, and I-B7 is not implemented (see the untested-cases list below), which is why
    no test catches it.
  - Fix: derive `tried` from every channel already attempted for the request (e.g. query sibling
    `NotificationAggregate` rows sharing `RequestId`, or walk the `FallbackFromId` chain), not just
    the one notification currently failing.
  - Corroborates `docs/gates/HO-NTF-001-ARCHITECTURE.md` HIGH 1 (found independently while reading
    this handler for the assigned "retries" focus, before that file was consulted).

- [BLOCKER] src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationWorkQueue.cs:30-36 — `GetRequestsDueForVariableRedactionAsync` never excludes already-redacted requests, so the redaction sweep starves once historical rows outnumber `BatchSize`, leaving newer secret-bearing requests' plaintext variables (a reset link) live indefinitely.
  ```csharp
  public async Task<IReadOnlyList<Guid>> GetRequestsDueForVariableRedactionAsync(DateTimeOffset now, int batchSize, CancellationToken ct = default) =>
      await db.NotificationRequests.AsNoTracking()
          .Where(r => r.ContainsSecret && r.NotAfter != null && r.NotAfter < now)
          .OrderBy(r => r.NotAfter)
          .Take(batchSize)
          .Select(r => r.Id)
          .ToListAsync(ct);
  ```
  - There is no `r.VariablesRedactedOn == null` filter. `NotificationRequestAggregate.RedactVariables`
    is (correctly) idempotent, but that only stops the *command* from doing anything harmful on a
    second run — it does not stop the *query* from selecting the same already-redacted row forever,
    because `NotAfter < now` stays true permanently once it becomes true once.
  - The sibling query for the notification side, three lines below, gets this right:
    `GetNotificationsDueForRedactionAsync` filters `n.RedactedOn == null`. The request-side query is
    the one place this was missed.
  - Consequence: rows are ordered oldest-`NotAfter`-first and capped at `Worker:BatchSize` (default
    50) per poll. Once 50 secret-bearing requests have ever expired, every subsequent poll's batch is
    entirely filled by those same 50 (permanently eligible, permanently no-op) rows, and the 51st
    onward secret-bearing request whose `NotAfter` passes is never selected, so its plaintext
    `resetLink` variable stays in `notify.notification_request.variables` indefinitely — not the
    "≤ 15 minutes" residual risk ADR-063 records, and squarely the kind of thing §20.4 calls a
    SECURITY failure rather than a test to relax. It also means the worker does the same pointless
    read every 5 seconds forever, growing without bound as more secret requests accumulate.
  - Fix: add `&& r.VariablesRedactedOn == null` to the `Where` clause.
  - Corroborates ARCHITECTURE HIGH 2 (found independently, before that file was consulted, while
    reviewing this file for the assigned outbox/worker focus).

- [HIGH] src/Domain/CatEcommerce.Domain/Aggregates/Notification/NotificationAggregate.cs:230-243 with src/Application/CatEcommerce.Application/DomainEventHandlers/Notification/AnonymiseNotificationsOnCustomerAnonymisedHandler.cs:31-32 — anonymisation is a no-op for any notification that was already redacted for a different reason, so a sent password-reset notification's real destination survives account anonymisation.
  - `Redact(bool isAnonymisation, DateTimeOffset now)` returns immediately when `RedactedOn is not
    null`, with no way to distinguish "already redacted because it was sent/secret" from "already
    redacted for anonymisation". §8.10 correctly makes plain redaction idempotent, but every
    `account.password-reset` notification is *already* redacted (by C5 or C6) as soon as it reaches a
    terminal state — normally within minutes of being created. By the time
    `AnonymiseNotificationsOnCustomerAnonymisedHandler` runs (`n.RedactedOn == null` filter, so it
    skips these rows entirely), those rows keep the customer's real mobile number or email address in
    `Destination` forever, because `isAnonymisation: true`'s only additional effect
    (`if (isAnonymisation) Destination = null;`) never runs for them.
  - This directly undermines acceptance criterion A16 ("anonymisation removes personal data from the
    log", ADR-041) for exactly the message type (password reset) this slice was built to deliver
    first, and I-D11 (untested — see below) is the case that would have caught it.
  - Fix: track *why* a row was redacted (a reason, as §12.4's `redactedReason` field already implies
    the read side needs) and let an `Anonymisation` redaction still null the destination even when
    `RedactedOn` is already set for a different reason.
  - Corroborates ARCHITECTURE HIGH 3.

- [HIGH] Untested §16 rows leave real gaps in exactly the areas this gate was asked to look at.
  - No test anywhere under `tests/` references `PreferredChannel` (searched the whole tree): the new
    `CustomerNotificationPreferencesAggregate.ChangePreferredChannel`, the
    `ChangeNotificationPreferencesCommandHandler` branch that resolves and validates it
    (`PreferredChannelHasNoDestination`/`InvalidPreferredChannel`), and
    `GetNotificationPreferencesQueryHandler`'s three new fields are all exercised by nothing. This is
    §16's I-B15.
  - No test references `ReassignInboxOnCustomerMergedHandler` or
    `AnonymiseNotificationsOnCustomerAnonymisedHandler` by name or by scenario — I-D10 (merge moves
    the inbox) and I-D11 (anonymisation redacts) are both absent, which is also why HIGH above went
    unnoticed by the test suite.
  - I-B14 (anonymised customer with a pending request → `Suppressed RecipientInactive`) and I-B16
    (production-like host with `Unconfigured` senders: the startup warning, a reset ending
    `Undeliverable`, no token anywhere in the log) are also absent.
  - I-B7 (both channels permanently failing) is absent, which is the case that would have surfaced
    the BLOCKER-1 fallback loop, and I-B11/I-B12 (the at-least-once/no-double-claim proof behind
    acceptance criterion A15) are absent, so the claim/lease design is statically sound but A15 is
    unproven.
  - `docs/test-cases/TC-HO-NTF-001.md`, which §13 lists as in scope, does not exist, so there is no
    traceability sheet the TEST gate can check §16 rows off against.
  - This list matches `docs/gates/HO-NTF-001-ARCHITECTURE.md`'s own test-gap enumeration closely
    (found independently by grepping every I-* id under `tests/` before that file was read); routing
    to TEST as well as recording here since coverage of the worker/idempotency surface is this gate's
    own remit.

- [MEDIUM] src/Application/CatEcommerce.Application/CommandHandlers/Notification/UnsubscribeFromMarketingCommandHandler.cs:36 — consent is revoked on the pre-merge customer id, not the survivor, when the notification being unsubscribed from is an external (SMS/Email) message sent before a merge.
  - `preferencesRepository.GetByIdAsync(notification.RecipientId)` uses the notification's own
    `RecipientId` unchanged. §11.6 correctly leaves external notifications' `RecipientId` as the
    original customer ("it is history"), but `CustomerNotificationPreferencesAggregate` is keyed by
    customer id (ADR-035), so this unsubscribe reads/writes the **duplicate**'s preferences root, not
    the survivor's. A customer who merged accounts and then clicks an old marketing email's
    unsubscribe link does not actually stop receiving that category on their (survivor) account.
  - Fix: resolve `notification.RecipientId` through the same "merged → survivor, ≤5 hops" rule
    `INotificationRecipientDirectory` already implements (§8.5) before loading/creating preferences,
    rather than using the notification's raw `RecipientId`.

- [MEDIUM] src/Api/CatEcommerce.Api/Background/NotificationWorkCycle.cs:80 — `DispatchAsync`'s catch-all logs a cancellation during shutdown as a pass failure.
  - `catch (Exception ex)` has no `when (ex is not OperationCanceledException)` guard, unlike the
    outer `NotificationWorker.ExecuteAsync`, which does exclude it. If `stoppingToken` is cancelled
    while a per-item dispatch is in flight, the resulting `OperationCanceledException` is logged at
    `Error` ("Notification worker pass failed for {CommandType} {Id}") as if something had actually
    gone wrong, rather than being treated as the expected, graceful-shutdown case it is.
  - Not a correctness bug — no exception escapes the loop either way — but it is log noise that will
    fire on every deploy/restart and makes a real per-item failure harder to spot among expected
    shutdown noise. Align with the outer handler's own `ex is not OperationCanceledException` filter.

- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/Notification/GetDueCareRemindersQueryHandler.cs:22-58 — re-derives "latest per vaccine"/"latest deworming" with its own `GroupBy`/`OrderByDescending` instead of reusing `CatVaccinationStatusCalculator`, contrary to F26's stated rationale ("the reminder query reuses it; it does not re-derive 'latest' in SQL").
  - The two derivations currently agree (both order by `AdministeredOn` descending and take the
    first), but they are now two independent places that must be kept in sync if the "latest record"
    tie-break rule is ever revisited for US-CAT-07.
  - Also loads every alive cat's full vaccination and parasite-treatment history into memory
    (`db.Cats.Where(LifeStatus == Alive)....ToListAsync()`) before filtering by the lead-day window,
    rather than pushing any of that filtering into SQL. Acceptable at today's scale; will not scale
    as the cat count grows, since this runs on every hourly sweep.
  - Corroborates ARCHITECTURE's LOW finding on the same handler.

- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationPasswordResetTokenDispatcher.cs:31 — uses `DateTimeOffset.UtcNow` directly instead of the injected `TimeProvider` this slice uses everywhere else for time.
  - `Math.Round((request.ExpiresOn - DateTimeOffset.UtcNow).TotalMinutes)` is harmless today (it is
    already UTC, and the value is only a rounded display hint), but it breaks the "every instant
    comes from `TimeProvider`" discipline the rest of the slice follows, and it cannot be driven by
    `FakeTimeProvider` in a test. `NotificationWorker`'s own sweep-scheduling `DateTimeOffset.UtcNow`
    calls (already flagged by ARCHITECTURE as LOW) are the same pattern in a second file.

- [Note, not a finding] Unsubscribe token verification
  (`UnsubscribeFromMarketingCommandHandler.Hash` / the database `WHERE UnsubscribeTokenHash = @hash`
  lookup) is not a constant-time compare. Checked deliberately given the task's instruction to look
  for this. It is consistent with this codebase's own established precedent for the structurally
  identical password-reset-token lookup
  (`RequestPasswordResetCommandHandler`'s own comment: "timing difference; the rate limiter is the
  real defence (ADR-028), not constant time"), so this is not a Notifications-specific deviation —
  not raised as a finding.

SCOPE CHECK
- Not independently re-derived here (ARCHITECTURE already did the file-by-file count against §13);
  its conclusion — 176/196 files in scope, 20 outside (7 extra Domain ports, `AccountsModule.cs` and
  three sibling HO-ACC-001 files, `SettingKeys.cs`, `UniqueConstraintExceptionFilter.cs`,
  `NotificationRateLimitPolicies.cs`, `NotificationOptionsValidator.cs`,
  `NotificationResendEvaluation.cs`, three Infrastructure test files) — was checked spot-wise while
  reading each of those files above and matches what this pass also observed. No file touches
  `.ai/**`, `docs/handover/**`, `web/**`, a `*.csproj`, `NuGet.Config`, `global.json`, a Cat file, or
  a provider SDK/HTTP client; no NuGet package was added.
</content>
