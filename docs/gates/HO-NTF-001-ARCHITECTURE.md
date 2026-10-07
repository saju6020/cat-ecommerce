GATE: ARCHITECTURE
HANDOVER: HO-NTF-001
COMMITS: 50395e6 ca6d3ea 0b7f201 7d08879 c91d411 027b1dc 482136c 4a7e967 1132c4d 1708e16 51b68bd f1839e9 9d560b6 9a9f25f b496f22 d875a8f a2705b8 ff449e2 e03f5d2 97aa959 (merge of master eef1621; only NTF's test compile fixes checked) 58c7d9e d6eb049 b755903 4efe96f (range 24f685e..4efe96f, head 4efe96f)
DATE: 2026-09-27
STATUS: PASS

This is PASS 2 of the ARCHITECTURE gate, a delta review of the fix round. Pass 1 (FAIL) is kept below.
I reviewed every branch-own commit in 24f685e..4efe96f: 77 files. For the merge 97aa959 I checked only
NTF's own test adaptations. The merge itself was clean (`git diff-tree --cc` is empty). The compile
fixes for HO-CAT-001's new `Today` DTO parameter landed in 58c7d9e (`NotificationsIntegrationTestBase`,
4 call sites) together with the `HostCompositionTests` pins. Both are test-only and correct. 97aa959
therefore does not build on its own: it is not bisectable, but it is harmless.
I read these against HO-NTF-001 §1 Amended (22)–(40), §13/§14, `.ai/host-composition.md`, ADR-342 and
the SECURITY and CODE_REVIEW first passes.

VERIFIED
- **Build.** A scratch `git archive 4efe96f` with no git-ignored appsettings built with 0 errors and
  0 warnings. The scratch folder `scratchpad/ntf1-arch2` has been deleted.
- **Pending model changes.** `has-pending-model-changes` reports "No changes have been made to the
  model since the last migration."
- **Forbidden residue in the delta.** None: no `IDispatcher`, bus, Mongo/Bson, `QueuePublishOnly`,
  `202`, rejection event or second host. Every new `SaveAsync`/`UpdateAsync` passes `InMemoryPublishOnly`.
- **Pipeline order (ADR-342).** `UseForwardedHeaders → UseRouting → SafeExceptionHandlerMiddleware →
  UseCors → UseAccountsModule (SessionAuthenticationMiddleware) → UseRateLimiter → MapHealthChecks /
  MapControllers`.
  - The rate limiter still runs after session resolution, so `ResolvedCaller`-partitioned
    policies (`ProfileUpdate`) still resolve.
  - It runs before endpoint execution, and `UseRouting` precedes it, so endpoint
    `[EnableRateLimiting]` metadata is visible.
  - `OnRejected` is registered exactly once (`HostPipelineExtensions.AddHostPipelineServices`). The
    Accounts and Notifications `AddRateLimiter` calls add only named policies. `.ai/host-composition.md`
    documents the order.
  - I-E4 (the unsubscribe `429`) now exists.

PASS 1 FINDINGS — DISPOSITION
- HIGH 1 (fallback never exhausts) — **CLOSED** (7d08879).
  - `TriedChannelsForChainAsync` walks `FallbackFromId` to the root, bounded by the count of
    deliverable channels.
  - The fallback render catch is widened, so a claimed row can no longer be stranded in `Sending`.
  - I-B7 added.
- HIGH 2 (variable-redaction starvation) — **CLOSED** (0b7f201). The `VariablesRedactedOn == null`
  filter is added, with a BatchSize=1 regression test.
- HIGH 3 (anonymisation skipped already-redacted rows) — **CLOSED** (c91d411).
  - `Redact(NotificationRedactionReason, now)` is idempotent per reason on a separate `AnonymisedOn`.
    `Anonymisation` nulls `Destination` and `UnsubscribeLink` and overrides `Secret`.
  - The reason is persisted by `Value` conversion.
  - The handler filters on `AnonymisedOn` and also covers merged-away duplicates through a reverse
    merge walk.
  - All state changes happen inside the aggregate, each with `UpdateVersion()`. I-D10 and I-D11 added.
- HIGH 4 (read handlers rehydrate aggregates) — **CLOSED** (027b1dc). Both handlers now `Select` into
  anonymous projections. `EffectiveNotificationTemplate` gains a projection overload, so the read side
  never constructs the root.
- HIGH 5 (no `IPersonalDataEraser`) — **CLOSED** (482136c).
  - `Retention/Erasers/NotificationPersonalDataEraser` is registered Scoped in `NotificationsModule`,
    following the `AdministrationPersonalDataEraser` precedent.
  - It uses the same aggregate methods as the event handler: loop-until-none, one `UpdateAsync` each.
- MEDIUM (8 Domain ports outside §13) — **CLOSED by record.**
  - `INotificationEnlister` is narrowed to two typed overloads (`NotificationAggregate`,
    `AuditEntryAggregate`), as asked.
  - The ports are listed in Amended (§13 overflow) as sanctioned exception-translation and layering
    seams. A new port was added this round, `INotificationResendStore`, in the same shape as the
    request/claim stores (see LOW 1).
- MEDIUM (`SettingKeys.cs` edited directly) — **CLOSED** (1132c4d, integrator-authorised).
  `static partial`, and the two keys moved to `SettingKeys.Notifications.cs`.
- MEDIUM (rate limiting spans two areas) — **CLOSED** (1708e16 + 51b68bd, integrator-authorised). See
  VERIFIED.
- MEDIUM (`.invalid` placeholders in production config) — **CLOSED** (f1839e9, 58c7d9e).
  - `ApiBaseUrl` is `""` in `appsettings.json`, and `[Required]` fails startup when it is empty.
  - The validator refuses `.invalid`/`.example`/`.test`/`localhost` hosts outside Development and
    Testing.
  - The web base URL is now the shared `PublicSiteOptions.WebBaseUrl` (owner decision), which is
    required in Production by `PublicSiteStartupGuardHostedService`. See LOW 4 for the residual gap
    in non-Production environments.
- MEDIUM (`Worker:Enabled=false` only in the NTF factory) — **CLOSED** (f1839e9) by a committed
  `appsettings.Testing.json`. It is acceptable; see the INTEGRATOR NOTES.
- MEDIUM (cross-area HO-ACC-001 edits) — **CLOSED by record.** They are in HO-ACC-001 §1 and
  HO-NTF-001 §1 Amended. The `Changes`-no-longer-`required` contract change is recorded as intentional.
  The stale "wins" comments are corrected.
- MEDIUM (C-ALL-1 classifications) — **CLOSED** (4a7e967).
  - `notification_template` is now `Operational/Forbidden`.
  - `notification_attempt` is now `Notification/Forbidden`.
  - `notification_template_content` stays `AggregateOnly`, with a recorded reason (the CurrentState
    carve-out). Tests added.
- MEDIUM (cross-channel resend not re-rendered) — **CLOSED** (ff449e2). A different channel is
  re-rendered from the request's variables, through the same catalogue/`EffectiveNotificationTemplate`/
  `NotificationRenderer` path as C4/C5 (see LOW 2).
- MEDIUM (§13 handover defect) — **CLOSED** by the Amended overflow list.
- MEDIUM (test-gap record) — **PARTLY CLOSED.**
  - Added: I-B7, I-B15, I-B16, I-D10, I-D11, I-E4, plus regression tests for every fix.
  - Still open, see MEDIUM 1: I-B6, I-B11, I-B12, I-B13 (integration), I-B14 and I-X1.
- LOW (`LeaseSeconds` hard-coded; `DateTimeOffset.UtcNow` in the worker) — **CLOSED** (f1839e9, d875a8f).
  The validator also requires `LeaseSeconds > SendTimeoutSeconds`.
- LOW (squashed migration) — **CARRIED, accepted**, now with two follow-on migrations. See INTEGRATOR
  NOTES.
- LOW (`UnsubscribeLink` plaintext beside the hash) — **PARTLY CLOSED, carried and accepted.**
  - Anonymisation now nulls it.
  - Otherwise it is still stored at rest for the life of the row, which undoes the hashing if the
    table leaks. This is a SECURITY judgement, not a layering one. Acceptable as a recorded
    aggregate-internal field.
- LOW (`UniqueConstraintExceptionFilter` mapping) — unchanged and accepted; recorded.
- LOW (`Channels` config shape) — **CLOSED** (d6eb049). The shape is now `Channels:{Sms|Email}:Sender`.
- LOW (reminder query does not reuse `CatVaccinationStatusCalculator`, F26) — **CARRIED, accepted.**
  - The query is still an in-memory re-derivation over every alive cat per hourly sweep. It stays a
    read-side query, with no aggregate rehydration.
  - It is not recorded in the Amended row: the integrator should add it, or open a follow-up before
    data volume grows.
- LOW (unsubscribe revokes the duplicate's consent after a merge) — **CLOSED** (9a9f25f).
- LOW (response shapes) — the resend `201` now carries `status`/`notBefore` (a2705b8), so that part is
  **CLOSED**. The admin-log hand-built `{ error }` 400s are **CARRIED, accepted**:
  `AdminAuditEntriesController.cs:33-40` uses the same shape for the same range and paging checks
  (the ADM precedent is confirmed). These are query-string guards, not a missing `IValidator`.

FINDINGS (pass 2)
- [MEDIUM] docs/handover/HO-NTF-001.md §1 Amended, "Test additions (§16)" — **the record overstates
  coverage.**
  - **I-B11/I-B12.** The row says they are "covered by the existing claim-store tests, re-verified".
    No test exercises `EfNotificationClaimStore`, a concurrent claim or `xmin`. The only claim tests
    are domain-level `NotificationAggregate.Claim` unit tests. A15 (at-least-once, no double send)
    is therefore still statically sound but unproven.
  - **I-A1.** It is covered only by HO-ACC-001's generic `HostCompositionTests` I-A1, which is
    acceptable.
  - **Still absent:** I-B6 (backoff), I-B13 at integration level (only the application-level
    I-B13-intent test exists), I-B14 (anonymised recipient suppressed) and I-X1 (log-capture sweep
    for this area).
  - The developer's own carried list names these gaps; the Amended row should say the same.
  - For TEST: implement I-B11/I-B12 or record them as BLOCKED with a reason. For the integrator:
    correct the sentence.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Notifications/EfNotificationResendStore.cs +
  src/Domain/CatEcommerce.Domain/DomainServices/INotificationResendStore.cs — **a third instance of
  the single-save exception-translation seam** (after the request store and the claim store).
  - It is recorded in Amended, and it is legitimate: the Application layer cannot catch
    `PostgresException`.
  - It is now a pattern, though. If a fourth appears, record it as an ADR (for example one generic
    `IUniqueConstraintAwareSave<T>`) rather than per-area ports. That keeps ADR-015's
    "service that only wraps a repository call" HIGH meaningful.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Notification/ResendNotificationCommandHandler.cs:111-141 — **a third
  copy of the variable-assembly and render block** (C4 expand, C5 fallback, C7 resend).
  - This is not a layering breach, but the three copies can drift. For example, if C4 later adds an
    unsubscribe link for Marketing, C7's cross-channel path will not have it.
  - The catch at :137 swallows the exception with **no log**, then falls back to reusing the other
    channel's rendered fields. That is the very defect C7 fixed. For CODE_REVIEW: at least log it.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs:25 with
  src/Api/CatEcommerce.Api/Modules/CatProfilesModule.cs:125-131 — **ADR-061's fail-loud rule now
  holds only in Production.**
  - `PublicSiteStartupGuardHostedService` accepts an empty `PublicSite:WebBaseUrl` in *any*
    non-Production environment (Staging included), not only in Development and Testing as its own
    comment says.
  - `NotificationLinkBuilder` has no relative-path fallback, so a Staging host with the value unset
    would send `/reset-password#token=…` links that go nowhere.
  - Production is safe. The owner decided to share the URL, so this is recorded rather than failed.
  - The fix belongs to the integrator, in the guard: "required unless Development/Testing".
- [LOW] src/Api/CatEcommerce.Api/appsettings.Development.json.example:5-7 — **the committed example is
  stale for this handover's config changes.**
  - It still has `Account:WebBaseUrl`, which was removed from `AccountOptions` in pass 1.
  - It does not set `Notifications:ApiBaseUrl`. `appsettings.json` now defaults that to `""`, and
    `[Required]` fails `ValidateOnStart`, so a developer who copies the example gets a Development
    host that will not start. §13's note tells them to set it, but the example is what they copy.
  - The file is outside §13, so this is for the integrator.
- [LOW] .ai/decisions/adr.md:1905,1988 — **ADR-342's pipeline sketches still say `UseAccountsModule()`
  "adds ONLY SessionAuthenticationMiddleware + UseRateLimiter".** `.ai/host-composition.md` was
  updated, but the ADR was not. ADRs are append-only here, so the integrator should append a one-line
  amendment note to ADR-342.
- [LOW] src/Api/CatEcommerce.Api/Program.cs:30 — `using Microsoft.AspNetCore.RateLimiting;` is unused
  (`UseRateLimiter` lives in `Microsoft.AspNetCore.Builder`). This is cosmetic.
- [LOW] src/Shared/CatEcommerce.Shared/Common/Constants/SettingKeys.cs:13 — `<see cref="SettingKeys.Notifications"/>`
  names a member that does not exist. It is silent only because XML documentation is not generated.
  The line sits inside the `ho/FIL-001` conflict hunk, so fix it at resolution.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Validators/ValidationRuleExtensions.cs —
  **scope.** An additive `NoPrivateUseOrFormatCharacters` rule was added to a shared validator helper
  that §13 does not list, and the Amended overflow list does not name the file (it names only
  `ControlCharacters.ContainsPrivateUseOrFormat`). It is harmless; add it to the record.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Retention/Erasers/NotificationPersonalDataEraser.cs:36-61 vs
  AnonymiseNotificationsOnCustomerAnonymisedHandler.cs:41-66 — **the anonymise loop is duplicated
  verbatim.** Both copies call the same aggregate methods, so behaviour cannot diverge today. A shared
  helper would stop a future change landing in only one of them.

INTEGRATOR NOTES
- **Committed `appsettings.Testing.json`: acceptable.**
  - ASP.NET Core loads `appsettings.{Environment}.json` only when `ASPNETCORE_ENVIRONMENT=Testing`,
    so it does not leak into Development or Production configuration.
  - The Web SDK does copy it into publish output, but it is inert there. Its only URL
    (`api.example.test`) would in any case be refused outside Development/Testing by
    `NotificationOptionsValidator`.
  - Two caveats:
    - "Testing" must stay reserved for the automated test host. A real QA deployment named
      "Testing" would silently run with the worker off and with the Capturing senders permitted.
    - If desired, exclude the file from publish in the Api csproj. That file is §14-forbidden for
      this handover, so the integrator would have to do it.
- **Migrations: acceptable, with a recommendation.** `AddNotificationRedactionReason` (2 nullable
  columns) and `AddNotificationResendOrdinal` (1 nullable column + a filtered unique index) are
  purely additive. They sit on `AddNotificationsModule`, and none of the three is on master or any
  deployed database.
  - **Recommendation:** the second of NTF/FIL to merge regenerates anyway, so the integrator should
    squash all three NTF migrations into one `AddNotificationsModule`, regenerated with a timestamp
    later than whatever has already landed.
  - The reason is timestamps: NTF's are 20260926152619, 20260927053357 and 20260927063531, while
    FIL-001's `AddFiles` is 20260927071352. If FIL lands first and NTF's three are kept, they sort
    *before* `AddFiles` while being applied after it, and the per-migration Designer snapshots
    disagree about the model. Squashing and regenerating avoids that.
- **merge-tree results, with resolutions:**
  - `master` (eef1621) — **clean** (tree 7f741b1).
  - `ho/FIL-001` (eb63f0f) — **2 conflicts:**
    - `CatEcommerceDbContextModelSnapshot.cs`: expected. Regenerate the snapshot and migration as
      above; do not hand-merge.
    - `SettingKeys.cs`: both branches independently made the class `static partial` and moved their
      keys to per-area files (`SettingKeys.Files.cs` / `SettingKeys.Notifications.cs`). The conflict
      is **only the XML doc comment**. Resolve by keeping one comment that mentions both areas and
      dropping the bad cref. The class body is identical on both sides.
    - `UniqueConstraintExceptionFilter.cs` and `appsettings.json` auto-merge (FIL adds
      `RateLimiting:FileUpload`/`FileServe`).
    - FIL's `FileStorageModule` adds its own `AddRateLimiter` with named policies only. That is
      compatible with the moved base: its comment says `UseRateLimiter()` is called "from"
      AccountsModule, which becomes stale after NTF merges. Correct that comment.
  - `ho/ACC-002` (5ce58b9) — **3 conflicts:**
    - `appsettings.json`: keep NTF's whole `Notifications` block, and in `Account` keep
      `"CsrfCookieDomain": ""` but drop `"WebBaseUrl": ""`.
    - `AccountOptions.cs`: keep the `CsrfCookieDomain` property and its `[RegularExpression]`, and
      drop `WebBaseUrl`. Nothing in ACC-002 reads `AccountOptions.WebBaseUrl`; the other
      `WebBaseUrl` hits are all `PublicSiteOptions`.
    - `docs/handover/HO-ACC-001.md`: both branches appended to the single-line Amended cell. Keep
      both appended texts, in date order, and renumber if the item numbers collide.
    - `CustomerProfileController.cs` auto-merges. ACC-002 does not touch rate limiting, `Program.cs`
      or `SettingKeys`.
- **Also carry:** correct the Amended row's I-B11/I-B12 claim (MEDIUM 1), add F26 and
  `ValidationRuleExtensions.cs` to the record, append the ADR-342 amendment note, and fix the
  `PublicSite` guard and the Development example (LOW 4/5).

SCOPE CHECK
- files in allowed scope: 77/77 (fix round 24f685e..4efe96f, excluding the merge). Each file is
  either inside §13, or authorised or recorded as follows:
  - **Integrator-authorised this round:**
    - `Program.cs` beyond §12.7, `Hosting/HostPipelineExtensions.cs`, `Modules/AccountsModule.cs`
      and `.ai/host-composition.md` (the rate-limiter move);
    - `SettingKeys.cs` and `SettingKeys.Notifications.cs`;
    - `appsettings.Testing.json`;
    - the switch to `PublicSiteOptions`.
  - **The §1 Amended rows** in `docs/handover/HO-NTF-001.md` and `HO-ACC-001.md`. These were
    requested by this gate's pass 1 and follow the established gate-fix practice.
  - **Recorded in §1 Amended:**
    - `Retention/Erasers/NotificationPersonalDataEraser.cs` (C-ALL-2);
    - `INotificationResendStore`, `NotificationRedactionReason` and `ControlCharacters.cs`;
    - the two extra migrations;
    - `tests/CatEcommerce.Infrastructure.Tests/**`.
  - **Not recorded:** `ValidationRuleExtensions.cs` (LOW above).
- files outside allowed scope: none unauthorised. No `web/**`, no `*.csproj`, no `NuGet.Config`, no
  Cat file and no provider SDK was touched, and no package was added.

==========================================================================================
PASS 1 (superseded by pass 2 above; kept for the record)
==========================================================================================

GATE: ARCHITECTURE
HANDOVER: HO-NTF-001
COMMITS: 5b0d077 8809cfa b5e687a feec341 e17f2b6 799e006 1ad2e7f d8d9fc5 (merge of master) b700311 0eb7969 91c6eb9 73a0aa5 9a9e739 fb161fb a864a31 ef20c3d 6f215ee 40a3373 (range master...ho/NTF-001, head 40a3373; master eae3d7b merged in)
DATE: 2026-09-27
STATUS: FAIL

PASS 1 of the ARCHITECTURE gate. I reviewed `git diff master...ho/NTF-001` (196 files) against
HO-NTF-001 (including §1 Amended), `.ai/` (architecture, cqrs, bounded-contexts, namespace-conventions,
di-registration-guide, command-handler-pattern, read-side-pattern, persistence-ef-core,
domain-events-and-outbox, enumeration-pattern, host-composition), ADR-341, ADR-342 and ADR-043,
docs/handover/CONTRACTS.md (#16/#17 and duties C-ALL-1…5), HO-ACC-001 §1 Amended (4)–(7) and (21),
and the carried items in HO-ADM-001-ARCHITECTURE.

VERIFIED
- **Build.** A scratch `git archive 40a3373` with no git-ignored appsettings built with 0 errors.
  The only warnings were NU1900, the offline vulnerability feed. The scratch folder has been deleted.
- **Pending model changes.** `dotnet ef migrations has-pending-model-changes` reports "No changes
  have been made to the model since the last migration."
- **merge-tree results:**
  - `ho/FIL-001`: the expected conflict in `CatEcommerceDbContextModelSnapshot.cs`. `SettingKeys.cs`
    auto-merges because both branches edit it (see MEDIUM 2).
  - `ho/CAT-001-gates`: clean.
  - `ho/ACC-002`: **2 unexpected conflicts**, in `appsettings.json` and `AccountOptions.cs`. NTF
    removes `Account:WebBaseUrl` and ACC-002 adds `CsrfCookieDomain` on the adjacent line. Both are
    trivial to resolve: keep `CsrfCookieDomain` and drop `WebBaseUrl`.
- **Forbidden residue.** None: no `IDispatcher`, `IBusMessageDispatcher`, `IEventPublisher`,
  MassTransit, RabbitMQ, Mongo/Bson, `QueuePublishOnly`, `202`, rejection event,
  `CommandServices` namespace or second host.
- **Save/update calls.** Every `SaveAsync`/`UpdateAsync` passes `InMemoryPublishOnly`.
- **Controllers.** Each uses `IInMemoryDispatcher` only and dispatches one command or query per
  action. Each carries `[RequireAction]`, `[RequireCustomerSession]` or `[PublicEndpoint]` (ADR-043).
- **Command handlers.** Every handler and validator (`IValidator<T>`), every query handler and both
  event handlers are registered explicitly in `NotificationsModule`, all Scoped. There is one
  `AddHostedService<NotificationWorker>`. The catalogue and the sender resolver are Singletons, as
  specified.
- **Domain.** The Domain layer references no EF Core, Application, Read or Api type.
  `NotificationRenderer`, `NotificationRoutingPolicy`, `QuietHours` and `SmsSegments` are pure
  (no clock, no I/O).
- **Aggregates.** State changes only inside aggregate methods, and each method calls
  `UpdateVersion()`. Children are mapped with `OwnsMany` (template contents ≤ 4, attempts ≤ 6).
  All three roots map `xmin`. Instants are `timestamptz`, `DateOnly` maps to `date`, enumerations
  are converted by `Value`, and `Variables` is `jsonb`. The dedup and unsubscribe indexes are
  filtered unique indexes.
- **Outbox (ADR-056/057).** `EfNotificationOutbox` adds to the scoped context without saving.
  `RequestPasswordResetCommandHandler` now dispatches before `SaveAsync(token)`, so the token and
  the request commit together (F23). **No enqueue from any domain event handler.**
- **Expand and fallback.** Both enlist the new roots and commit them in one `SaveChangesAsync`, as
  ADR-056 records.
- **Worker.** One `BackgroundService` creates a scope per pass and a fresh scope per item, and
  dispatches through `IInMemoryDispatcher` with `SystemActors.NotificationWorker`. It never throws
  out of `ExecuteAsync`.
- **Claim and send ordering.** The claim is committed through `xmin` before `SendAsync`, so no
  provider call runs inside an open transaction. This is the per-call `SaveChangesAsync`
  (`.ai/persistence-ef-core.md` §2).
- **Audit exclusion.** `AuditCaptureExclusions` is data rather than an inline `if` (ADR-066).
- **Merge and anonymise handlers (ADR-039/041).** Both follow the accepted
  `ReassignCatsOnCustomerMergedHandler` shape: loop-until-none, one `UpdateAsync` each, idempotent,
  nothing enqueued. The anonymise handler has a defect, see HIGH 3.

FINDINGS
- [HIGH] src/Application/CatEcommerce.Application/CommandHandlers/Notification/DeliverNotificationCommandHandler.cs:159 — **ADR-058 R8 is violated: the fallback never exhausts.**
  - **What the code does.** `tried` holds only the failing notification's own channel. The fallback
    chain (`FallbackFromId`) is never walked.
  - **The loop.** Suppose a recipient has both identifiers and both channels fail permanently. The
    SMS fails and falls back to Email. The Email then fails with `tried={Email}`, so
    `NextFallback` returns SMS again. That fails with `tried={Sms}` and falls back to Email again,
    and so on. One new `notification` row is created per poll (5 s), without bound.
  - **Why this hits production.** It is the default production state: both senders are
    `Unconfigured` and return `PermanentFailure` (§4.3). Every care reminder to a customer with a
    mobile and an email loops forever, because the request variables are never redacted for
    non-secret templates.
  - **Other hosts.** Every other area's `Testing` host also runs the worker loop (MEDIUM 5), so the
    loop runs there too.
  - **Tests.** I-B7 (both channels failing) is not implemented, which is why no test sees this.
    TEST must add I-B7 and confirm.
  - **Fix.** Derive `tried` from the whole chain: the request's notifications, or the walk up
    `FallbackFromId`.
- [HIGH] src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationWorkQueue.cs:30-36 — **ADR-063 is violated: variable redaction starves.**
  - **The query.** `GetRequestsDueForVariableRedactionAsync` has no `VariablesRedactedOn == null`
    filter. It orders by `NotAfter` and takes `BatchSize` (50).
  - **The starvation.** Once 50 secret requests have expired, every pass returns the same 50
    already-redacted ids. C6 is a no-op for each of them. Request 51 onward is never redacted.
  - **Consequence.** From the 51st password reset onward, the plaintext `resetLink` stays in
    `notify.notification_request.variables` indefinitely. ADR-063 bounds that window at the
    15-minute token lifetime.
  - **Knock-on.** Because those requests are never redacted, HIGH 1's loop also runs forever for
    reset fallbacks.
  - **Routing.** Route to SECURITY (§20.4-class) and TEST.
  - **Fix.** Add the missing filter, as `GetNotificationsDueForRedactionAsync` already does.
- [HIGH] src/Application/CatEcommerce.Application/DomainEventHandlers/Notification/AnonymiseNotificationsOnCustomerAnonymisedHandler.cs:32 with src/Domain/CatEcommerce.Domain/Aggregates/Notification/NotificationAggregate.cs:230-243 — **ADR-041 / §11.6 / A16 are not met: anonymisation skips any notification that is already redacted.**
  - **Spec vs code.** §8.6 specifies `Redact(reason)`, re-applicable for `Anonymisation`. The code
    implements `Redact(bool isAnonymisation, …)` with an early return on `RedactedOn is not null`.
    The handler also filters on `RedactedOn == null`.
  - **Effect.** Every password-reset notification is redacted as soon as it is sent. Those rows
    therefore keep the customer's full mobile or email in `destination` after anonymisation.
  - **Knock-on.** No redaction reason is stored, so §12.4's `redactedReason` cannot be produced.
    HO-XCT-001 §8.8's eraser also depends on "not yet redacted with `Anonymisation`".
  - **Fix.** Store the reason and let `Anonymisation` override a prior non-anonymisation
    redaction. I-D11 is untested.
- [HIGH] src/Read/CatEcommerce.Read/QueryHandlers/Notification/GetNotificationDetailQueryHandler.cs:23-25 and GetNotificationTemplateQueryHandler.cs:22-24 — **read-side-pattern rule 1 / ADR-012 are violated: both rehydrate an aggregate.**
  - `db.Set<NotificationAggregate>().AsNoTracking().Include(n => n.Attempts).FirstOrDefaultAsync(...)`
    and `db.NotificationTemplates.AsNoTracking().Include(t => t.Contents).FirstOrDefaultAsync(...)`
    load the root and then map it in memory. They should `Select` into the ViewModel.
  - Every other read handler in this diff projects, and every existing read handler in the solution
    does too.
- [HIGH] src/Api/CatEcommerce.Api/Modules/NotificationsModule.cs (no registration) — **CONTRACTS.md C-ALL-2 is not met: no `IPersonalDataEraser` is registered for the Notification context.**
  - **Why one is needed.** The context stores personal data beyond the id: destination
    mobile/email, rendered bodies with the recipient's name, and request variables.
  - **The rule.** Every area must register its eraser, or a recorded exemption, "in the same change
    that adds a table".
  - **Precedent.** HO-ADM-001 set this precedent (`AdministrationPersonalDataEraser`, registered in
    its own module). HO-XCT-001 §8.8 lists `NotificationPersonalDataEraser`, but the integrator's
    CONTRACTS duty assigns it to the area.
  - **Fix.** Add `Retention/Erasers/NotificationPersonalDataEraser` implementing §8.8's row, which
    must include the HIGH 3 fix. Alternatively, the integrator records an explicit deferral to
    HO-XCT-001.
- [MEDIUM] src/Domain/CatEcommerce.Domain/DomainServices/{INotificationRequestStore,INotificationClaimStore,INotificationEnlister,INotificationResendCounter,INotificationDeliverySettings,INotificationChannelSenderResolver,ISubjectGuardResolver}.cs and EffectiveNotificationTemplate.cs — **deviation 3: 8 Domain files outside §13's explicit list.** Judged one by one:
  - **Accepted as legitimate layering seams.** `INotificationChannelSenderResolver`,
    `ISubjectGuardResolver` and `INotificationDeliverySettings` exist because Application cannot
    reference Infrastructure's `NotificationSenderResolver`, `SubjectGuardResolver` or
    `NotificationOptions`. `INotificationResendCounter` is the one function §12.6 requires C7 and
    the read side to share. `EffectiveNotificationTemplate` is a pure §8.2 rule.
  - **`INotificationRequestStore` / `INotificationClaimStore`.** Each wraps one repository save. Each
    translates a provider exception (`23505` on the dedup index, and `DbUpdateConcurrencyException`)
    that the Application layer structurally cannot catch.
    - That translation is why they do not meet ADR-015's "only wraps a repository call" HIGH.
    - They are still the shape ADR-015 warns will be copied. Record them in an ADR or Amended row as
      the sanctioned exception-translation seam, and do not generalise them.
  - **`INotificationEnlister.Enlist(AggregateRoot)`.** This is an **untyped** unit-of-work
    back door in a Domain port: any handler could enlist any root into the next commit, which
    widens rule 1 beyond ADR-056's recorded exception.
    - C7 already uses it to enlist an `AuditEntryAggregate`.
    - **Fix.** Narrow it to the two types ADR-056 and §11.4 C7 name (`NotificationAggregate` and
      the audit entry), or make it internal to the Notification handlers.
  - None of the 8 files is in §13. The handover needs an Amended row.
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Constants/SettingKeys.cs:17-21 — **deviation 1: HO-ADM-001's non-partial, shared registry was edited directly.**
  - This is outside §13. §13 lists `SettingDefinitions.cs` only, and ADR-341 turns that into
    `SettingDefinitions.Notifications.cs`, which was done correctly.
  - Editing a shared, non-partial file is exactly the cross-area touch point ADR-341 exists to
    remove. `ho/FIL-001` edits the same file; it auto-merges today.
  - **Fix.** Declare the two key constants in this area's own file, or have the integrator make
    `SettingKeys` `static partial` and move them into `SettingKeys.Notifications.cs`.
- [MEDIUM] src/Api/CatEcommerce.Api/Modules/NotificationsModule.cs:81-107 with src/Api/CatEcommerce.Api/Modules/AccountsModule.cs:221-272 — **deviation 7: rate limiting now spans two areas.**
  - **How it composes.** The second `AddRateLimiter` call composes correctly through
    `Configure<RateLimiterOptions>`. `NotificationRateLimitPolicies.cs` as its own file follows
    ADR-341 and is fine.
  - **The dependency.** Notifications' two policies now depend on Accounts for two things:
    - `OnRejected`, the 429 body;
    - `app.UseRateLimiter()` inside `UseAccountsModule()`.
  - **What this triggers.** This is the trigger for the integrator's carried item (HO-ADM-001
    ARCHITECTURE MEDIUM 1, "`UseRateLimiter` lives in AccountsModule"): **move `UseRateLimiter()`,
    and the shared `AddRateLimiter` base including `OnRejected`, into `Hosting/HostPipelineExtensions`
    / `Program.cs`'s shared block (ADR-342)**. Areas then add only their own policies.
  - **Tests.** I-E4 (the 21st unsubscribe gets `429`) is untested, so nothing proves that the
    Notification policies actually run.
- [MEDIUM] src/Api/CatEcommerce.Api/appsettings.json:19-20 — **deviation 8: the `.invalid` placeholders defeat ADR-061's "fail safe and loud".**
  - `[Required, Url]` with `ValidateOnStart` was meant to stop the host starting without real base
    URLs. With `https://app.invalid` committed, a production deploy that forgets
    `Notifications:WebBaseUrl` starts cleanly. It will build dead reset, unsubscribe and care links
    as soon as a provider exists.
  - The regression it fixes is real: other areas' `Testing` hosts failed `ValidateOnStart`. The
    right fix belongs in the tests, not the production defaults:
    - set the values in a Testing configuration or the shared test factory; or
    - have `NotificationOptionsValidator` refuse a `.invalid` host outside Development/Testing.
- [MEDIUM] src/Api/CatEcommerce.Api/appsettings.json:33 — **§12.2 "Worker:Enabled … false in Testing" is not met.**
  - Only `NotificationsApiFactory` sets `Worker:Enabled=false`. Every other area's
    `WebApplicationFactory<Program>` (Accounts, Administration, Cat) runs `Testing` with the live
    `NotificationWorker` loop polling the shared test database every 5 s.
  - That loop races the Notifications tests' manual passes on shared rows, and it drives HIGH 1's
    loop.
  - **Fix.** Default the flag off in `Testing` in one place, not per factory. Hand to TEST.
- [MEDIUM] src/Api/CatEcommerce.Api/Modules/AccountsModule.cs:83-91, Controllers/CustomerProfileController.cs:181, Contracts/Account/ChangeNotificationPreferencesRequest.cs:12, Application/.../Validators/ChangeNotificationPreferencesCommandValidator.cs — **deviation 5 and its siblings: cross-area edits to HO-ACC-001 files outside §13.**
  - **Authorised in substance:**
    - HO-ACC-001 §1 Amended (4) and HO-NTF-001 §11.4/§12.7 require replacing the dispatcher
      registration and deleting the two classes. ADR-341 moved that registration from `Program.cs`
      (§13) into `AccountsModule`.
    - §8.5/§12.4 require `preferredChannel` on `PUT` preferences.
    - Each edit is minimal.
  - **Not authorised in letter.** §14 forbids "every other file of HO-ACC-001 beyond the lines
    listed in §13", and there is no Amended row.
  - **Contract change.** `ChangeNotificationPreferencesRequest.Changes` loses `required`, which
    changes HO-ACC-001's request contract: an empty body is now accepted and falls to the
    validator. This is the only non-additive change, and HO-ACC-002 should be told.
  - **Stale comments.** The comments in both modules still say "registered a second time … wins".
    It is now the **only** registration, so the comments should be corrected (LOW).
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Constants/RecordClassification.Notifications.cs:13-17 — **C-ALL-1: two tables are classified differently from HO-XCT-001 §10.3.**
  - `notification_template` is classed `Notification`. §10.3 says "`notify.*` template tables:
    `Operational`, `Forbidden`".
  - `notification_attempt` is `AggregateOnly`. §10.3 says "every `notify.*` log and request table:
    `Notification`, `Forbidden`". Attempts are append-only log rows.
  - `notification_template_content` is `AggregateOnly`. That is defensible, because
    `ResetToDefault` deletes the rows, but it contradicts §10.3's `Forbidden`. It needs a recorded
    reason, or an XCT correction.
- [MEDIUM] src/Application/CatEcommerce.Application/CommandHandlers/Notification/ResendNotificationCommandHandler.cs:54-62 — **§11.4 C7 is not met: a cross-channel resend is not re-rendered.**
  - C7 says "re-rendered only when the channel differs". The code reuses the original channel's
    rendered fields instead.
  - **Effect.** An SMS resent as email has no `Subject` and no `HtmlBody`. An email resent as SMS
    carries the email's markup text.
  - The deviation is recorded only in a code comment.
  - **Fix.** Re-render from the request's variables, as C5's fallback already does, or record an
    Amended row.
- [MEDIUM] docs/handover/HO-NTF-001.md §13 — **handover defect.** §13 omits files that the handover's
  own §12.4 (`CustomerProfileController`, the Account preferences request), §12.5
  (`UniqueConstraintExceptionFilter`) and §12.7 (rate-limit policies, the ADR-341 module files)
  require. The integrator should add an Amended row covering these and the deviations above.
- [MEDIUM] Test-gap record (deviation 10) — **the implementer's named gap list is incomplete.**
  - **Named:** I-B2, I-C4, I-F2, I-F5, I-F6 and I-E4.
  - **I-F2 is actually present** (`CareReminderEndpointTests.SweepRunTwice_StillOnlyOneRequest`).
  - **Also absent:**
    - I-A1: the host resolves with `ValidateScopes`/`ValidateOnBuild` on.
    - I-B6: retry backoff.
    - I-B7: both channels failing, which would expose HIGH 1.
    - I-B11 and I-B12: the at-least-once and double-claim proof behind A15.
    - I-B13: concurrent dedup.
    - I-B14: anonymised recipient suppressed.
    - I-B15: preferred channel with no destination.
    - I-B16: the startup warning, and a reset ending `Undeliverable`.
    - I-D10: merge moves the inbox.
    - I-D11: anonymisation redacts, which would expose HIGH 3.
    - I-X1: the log-capture sweep.
  - For TEST and ACCEPTANCE. The claim/lease design is statically sound, but A15 is unproven until
    I-B11 and I-B12 exist.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Notification/DeliverNotificationCommandHandler.cs:38 — **deviation 9, worker design.**
  - **Structurally compliant with ADR-060:** one hosted service, a scope per pass and per item,
    claim-by-lease committed before the send, `xmin` against double claims, a lost claim returning
    success, exceptions logged and swallowed per item, nothing thrown out of `ExecuteAsync`.
  - **Two gaps:**
    - `LeaseSeconds` is hard-coded to 120 and ignores `Notifications:Worker:LeaseSeconds`. The port
      exposes only `SendTimeoutSeconds`.
    - `NotificationWorker` schedules the sweep with `DateTimeOffset.UtcNow` instead of
      `TimeProvider`.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20260926152619_AddNotificationsModule.cs — **deviation 4: one squashed migration instead of the three §12.1 names (A, B, D).**
  - Accepted: ADR-341 point 5 makes migration composition the integrator's job, so the per-phase
    names are moot. The model has no pending changes.
  - The snapshot conflicts with `ho/FIL-001`, so the integrator must regenerate one migration at
    merge, as it did for earlier waves.
- [LOW] src/Domain/CatEcommerce.Domain/Aggregates/Notification/NotificationAggregate.cs:64 — **deviation 2: `UnsubscribeLink` (the plaintext-token URL) is stored beyond §8.6.**
  - Accepted as an aggregate-internal convenience for re-sending `List-Unsubscribe` headers on
    retries.
  - However, `Redact` does not clear it, so an anonymised customer's row keeps a working
    unsubscribe URL. It also stores the plaintext token next to `UnsubscribeTokenHash`, which undoes
    the point of hashing once the body is redacted. For SECURITY.
- [LOW] src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs:33 — **deviation 6.**
  Adding `ix_notification_template_key` to the host-owned filter's `ConflictIndexes` is exactly what
  §12.5 specifies. Now that the integrator registers the filter in `Program.cs`, a per-area mapping
  line is the intended extension. It is fine; only §13's omission needs recording.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Configuration/NotificationOptions.cs:24-29 — **the configuration shape differs from §12.2.**
  - §12.2 specifies `Channels:{Sms|Email}:Sender`; the code uses `Channels:Sms` as a bare string.
  - The ADR-320 adapters (`SmtpEmailSender`, `HttpSmsGateway`) will need sibling keys under each
    channel. Settle the shape now, before a provider lands.
- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/Notification/GetDueCareRemindersQueryHandler.cs:16-60 — **the reminder query does not reuse `CatVaccinationStatusCalculator` (F26).**
  - It re-derives "latest per vaccine" in memory.
  - Every hourly sweep projects every alive cat, with all its vaccinations and treatments, into
    memory. This is acceptable today but unbounded as the data grows.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Notification/UnsubscribeFromMarketingCommandHandler.cs:134 — **consent is revoked on the wrong account after a merge.**
  - It revokes consent on `notification.RecipientId`, which is the pre-merge duplicate, not the
    survivor the directory routes to.
  - An unsubscribe from a pre-merge email therefore leaves the survivor subscribed. For
    CODE_REVIEW.
- [LOW] src/Api/CatEcommerce.Api/Controllers/AdminNotificationsController.cs:24-31,78 — **response shapes differ from §12.4.**
  - Hand-built range and paging `400`s return `{ error }` rather than the `ValidationResponse` shape
    that R1–R8 require. This follows the HO-ADM-001 paging-cap precedent, so it is not the MEDIUM
    hand-validation rule.
  - The resend `201` body lacks §12.4's `status` and `notBefore`.

INTEGRATOR NOTES
- **Rate limiting.** Carry out the carried `UseRateLimiter` move now (MEDIUM 3): Notifications is the
  second area with rate-limit policies.
- **`ho/ACC-002`.** Resolve its 2 textual conflicts: keep `CsrfCookieDomain` and drop `WebBaseUrl`.
- **`ho/FIL-001`.** Regenerate the migration at merge (snapshot conflict), and check that the two
  `SettingKeys.cs` insertions both survive.
- **Amended row in HO-NTF-001 §1.** Record the §13 overflow:
  - the 8 Domain files;
  - `SettingKeys.cs`;
  - the four HO-ACC-001 files;
  - `UniqueConstraintExceptionFilter.cs` and `NotificationRateLimitPolicies.cs`;
  - `NotificationOptionsValidator.cs` and `NotificationResendEvaluation.cs`;
  - `tests/CatEcommerce.Infrastructure.Tests/**`;
  - the squashed migration.

SCOPE CHECK
- files in allowed scope: 176/196. I counted as in scope: ADR-341's per-area translations
  (`NotificationsModule.cs`, `CatEcommerceDbContext.Notifications.cs`, the `*.Notifications.cs`
  partials) and the CONTRACTS C-ALL-1 file `RecordClassification.Notifications.cs`.
- files outside allowed scope (20):
  - src/Api/CatEcommerce.Api/Modules/AccountsModule.cs
  - src/Api/CatEcommerce.Api/Controllers/CustomerProfileController.cs
  - src/Api/CatEcommerce.Api/Contracts/Account/ChangeNotificationPreferencesRequest.cs
  - src/Application/CatEcommerce.Application/CommandHandlers/Validators/ChangeNotificationPreferencesCommandValidator.cs
  - src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs
  - src/Api/CatEcommerce.Api/RateLimiting/NotificationRateLimitPolicies.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/INotificationRequestStore.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/INotificationClaimStore.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/INotificationEnlister.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/INotificationResendCounter.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/INotificationDeliverySettings.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/INotificationChannelSenderResolver.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/ISubjectGuardResolver.cs
  - src/Domain/CatEcommerce.Domain/DomainServices/EffectiveNotificationTemplate.cs
  - src/Infrastructure/CatEcommerce.Infrastructure/Configuration/NotificationOptionsValidator.cs
  - src/Shared/CatEcommerce.Shared/Common/Constants/SettingKeys.cs
  - src/Shared/CatEcommerce.Shared/Common/Helpers/NotificationResendEvaluation.cs
  - tests/CatEcommerce.Infrastructure.Tests/Auditing/AuditCaptureExclusionsTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Configuration/NotificationOptionsValidatorTests.cs
  - tests/CatEcommerce.Infrastructure.Tests/Notifications/SenderConstructionTests.cs
- No file touches `.ai/**`, `docs/handover/**`, `web/**`, any `*.csproj`, `NuGet.Config`,
  `global.json`, a Cat file, or a provider SDK or HTTP client. No NuGet package was added.
