# HO-NTF-001 — Notifications (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-NTF-001** |
| Title | Notifications — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §13 — **US-NTF-01 … US-NTF-06** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-NTF-1 … FR-NTF-7**, FR-CAT-7 (reminder inputs), FR-ACC-9 (reset delivery), §3.3 (SMS, email, push interfaces), NFR-LEG-4, C-7, C-8, C-9 |
| Pairs with | [`HO-NTF-002`](HO-NTF-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ACC-001`](HO-ACC-001.md) (accounts, preferences, reset tokens, audit capture, `ActionRoles`), [`HO-ADM-001`](HO-ADM-001.md) (settings, branches, time model, `LimitedMarkup`) and [`HO-CAT-001`](HO-CAT-001.md) (vaccinations, parasite treatments) |
| Delivery order | Section 4 of 17 in [`README.md`](README.md#delivery-order) |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — by [`HO-APT-001`](HO-APT-001.md), before implementation.** (1) `NotificationRequestAggregate` and `NotificationRequestDto` gain **`SendAfter`** (the request is not expanded before it; the work queue honours it) and **`SubjectStamp`** (copied to the notification and passed to guards); migration `AddNotificationRequestScheduling`. (2) `NotificationSubjectKind` gains `AppointmentReminder`, guarded by `AppointmentReminderGuard`. (3) `INotificationLinkBuilder` gains `AppointmentLink`, and `CatCareLink` now points at `/book?catId=…&category=…` (the change §4.2 reserved). (4) Five appointment and waitlist templates are appended to the catalogue. All built by HO-APT-001. *(append further rows here)* <br><br>**2026-09-23 — ADR-320 (transports).** OQ-1 is settled on the design side: **email** is SMTP through MailKit (`SmtpEmailSender`), so any provider is a configuration change; **SMS** is one config-driven HTTP adapter (`HttpSmsGateway`) matching the shape Bangladeshi aggregators expose. Both meet the §8.9 adapter contract recorded in ADR-061, so nothing in this slice's machinery changes. **Still owner-supplied:** an SMTP account and an SMS aggregator account with a sender ID — until those exist the `Unconfigured` senders stay registered and the §3 caveat stands. <br><br>**2026-09-23 — appended by later handovers (owed rows, recorded now that all 18 pairs exist).** Everything below is an **append** inside the lines each handover lists in its §13; no existing behaviour changes. These are recorded here so this document is not read as the whole picture. **Appends:** templates from Billing (3), Boarding (11), Pharmacy (5), Surgery (6), Inventory (1), Shop (1), Orders (12), Subscriptions and Reporting; new `NotificationSubjectKind` members and guards (`BatchRecall`, `PrescriptionRefillDue`, `BoardingStayInHouse`, `StockAlert`, `ScheduledReport`, two surgery kinds); `INotificationLinkBuilder` methods (invoice, receipt, order, product, report, seven boarding links). HO-RPT-001 additionally needs attachments **by reference** (`AttachmentKind` / `AttachmentId` resolved at send time), which is the one structural change rather than an append — HO-RPT-001 §11.7 specifies it. <br><br>**2026-09-27 — SECURITY/ARCHITECTURE/CODE_REVIEW gate fixes, first-pass FAIL on `40a3373` (self-amendment, this handover's own build).** Full findings in `docs/gates/HO-NTF-001-{SECURITY,ARCHITECTURE,CODE_REVIEW}.md`; this row records what changed and the §13 overflow. **HIGH/CRITICAL:** (22) Detail/search now show `"[redacted]"` for a secret-bearing template's body/subject/title whenever `ContainsSecret` is true, regardless of `RedactedOn` — a Reception `NotificationLog.View` holder could otherwise read a live password-reset link for any account, including an Administrator, while the notification was `Queued`/`Sending`/`RetryPending` (SECURITY HIGH 1). (23) `TryBuildFallbackAsync`'s `tried` channel set now walks the `FallbackFromId` chain to the root instead of seeding from only the currently-failing notification's own channel — both channels permanently failing (the default state with `Unconfigured` senders) previously alternated forever, one new row per poll (ADR-058 R8; SECURITY HIGH 1 / CODE_REVIEW BLOCKER 1). (24) `GetRequestsDueForVariableRedactionAsync` gains the `VariablesRedactedOn == null` filter its notification-side sibling already had — without it, the sweep starved once `BatchSize` secret requests had ever expired, leaving every later reset link's plaintext in `notification_request.variables` indefinitely instead of ADR-063's 15-minute bound (SECURITY HIGH / CODE_REVIEW BLOCKER). (25) `Redact` takes a `NotificationRedactionReason` (`Secret` | `Anonymisation`, new enum) instead of a bool; `Anonymisation` is idempotent on a new, separate `AnonymisedOn` marker so it still nulls `Destination`/`UnsubscribeLink` on a row already `Secret`-redacted (the normal case for a delivered password reset) — migration `AddNotificationRedactionReason`. `AnonymiseNotificationsOnCustomerAnonymisedHandler` filters on `AnonymisedOn` and now also processes the merged-away duplicates of the customer being anonymised (new `INotificationRecipientDirectory.FindMergedDuplicatesAsync`, a reverse walk of `MergedIntoCustomerId`) — §11.6 keeps an external notification's `RecipientId` as the pre-merge duplicate, and a merged-away customer can never itself be anonymised, so those rows were never reached (A16/ADR-041; SECURITY/ARCHITECTURE/CODE_REVIEW HIGH). (26) `GetNotificationDetailQueryHandler`/`GetNotificationTemplateQueryHandler` now `Select`-project instead of `Include`-ing and materialising the aggregate root (`EffectiveNotificationTemplate` gains a second, already-projected-content overload) — read-side-pattern rule 1 (ARCHITECTURE HIGH 4). (27) `NotificationPersonalDataEraser` registered (CONTRACTS.md C-ALL-2; ARCHITECTURE HIGH 5) — reuses the same `Redact(Anonymisation)`/`RedactVariables` operations and merged-duplicate coverage as (25). **MEDIUM:** (28) `TemplateRules`/`NotificationRequestAggregate.Create` reject Private-Use/Format Unicode characters (`ControlCharacters.ContainsPrivateUseOrFormat`, new), closing the path by which the renderer's own internal sentinel characters (U+E000/U+E001) could reach it as literal admin-authored or request-variable text and crash every later render (`ArgumentOutOfRangeException`/`FormatException`, uncaught). `ExpandNotificationRequestCommandHandler`'s render catch is broadened from `InvalidOperationException` to any non-cancellation exception, suppressing with a new `NotificationSuppressionReason.RenderFailed` on the very first failed attempt instead of leaving a poison request stuck `Pending` and blocking every other pending request behind it (head-of-line blocking). Preview/save 500s (`contents:[null]`, an unrecognised channel, a non-https `Url` sample, a malformed `Date` sample) are now 400s. (29) `UnsubscribeFromMarketingCommandHandler` resolves `notification.RecipientId` through `INotificationRecipientDirectory` before loading preferences, so an unsubscribe from a pre-merge marketing message revokes the survivor's consent, not the (history-only) duplicate's (§11.4 C7-adjacent; CODE_REVIEW MEDIUM). (30) `ResendNotificationCommandHandler` re-renders from the original request's own variables when the target channel differs from the original, instead of always reusing the original channel's rendered fields verbatim (§11.4 C7; ARCHITECTURE/CODE_REVIEW MEDIUM). (31) `SettingKeys` is now `static partial` (integrator-authorised) — this area's two keys move to `SettingKeys.Notifications.cs`. (32) `UseRateLimiter()` and the shared 429 response shape move out of `AccountsModule` into `HostPipelineExtensions`/`Program.cs`'s shared block (carried from HO-ADM-001-ARCHITECTURE MEDIUM 1, integrator-authorised) — Notifications is now a second area contributing its own rate-limit policies; `.ai/host-composition.md` updated. (33) `RecordClassification.Notifications.cs` corrected against HO-XCT-001 §10.3: `notification_template` is `Operational` (was `Notification`); `notification_attempt` is `Forbidden` (was `AggregateOnly`) — it is append-only log history, not a wholesale-replaced owned collection like `notification_template_content` (kept `AggregateOnly`, reason now recorded explicitly). (34) `INotificationEnlister.Enlist(AggregateRoot)` narrowed to two typed overloads (`NotificationAggregate`, `AuditEntryAggregate`) — the untyped signature was an unbounded unit-of-work back door beyond what §11.4 C7 names. **LOW:** (35) Resend limit race: `NotificationAggregate.ResendOrdinal` (new; migration `AddNotificationResendOrdinal`) backs a unique filtered index on `(ResendOfId, ResendOrdinal)`; `INotificationResendStore`/`EfNotificationResendStore` (new, same shape as `INotificationRequestStore`) translate a lost race into `ResendLimitReached` instead of letting concurrent resends exceed the limit. (36) `DeliverNotificationCommandHandler`'s claim lease is read from `INotificationDeliverySettings.LeaseSeconds` (new) instead of a hard-coded 120; `NotificationOptionsValidator` requires `LeaseSeconds > SendTimeoutSeconds`. (37) `NotificationWorkCycle`'s per-item catch no longer logs a shutdown `OperationCanceledException` at `Error`; `NotificationPasswordResetTokenDispatcher`/`NotificationWorker`'s sweep scheduling read `TimeProvider` instead of `DateTimeOffset.UtcNow`. (38) Resend `201` body is `{ notificationId, status, notBefore? }` (was `{ notificationId }`) — `ResendNotificationResult`, new. (39) `Channels:Sms`/`Channels:Email` are now `{ Sender }` objects, matching §12.2's literal `Channels:Sms:Sender`/`Channels:Email:Sender` shape (was a bare string) — `NotificationChannelSenderOptions`, new. **§13 overflow, recorded (deviations 1/3/7/8/9/10 from the implementer's own report, now closed or corrected above):** 8 extra Domain ports (`INotificationChannelSenderResolver`, `INotificationClaimStore`, `INotificationDeliverySettings`, `INotificationEnlister`, `INotificationRequestStore`, `INotificationResendCounter`, `INotificationResendStore`, `ISubjectGuardResolver`) plus `EffectiveNotificationTemplate`, `NotificationRedactionReason`, `INotificationResendStore`/`EfNotificationResendStore` — each a narrow, single-purpose seam avoiding an Application→Infrastructure layering violation, sanctioned per ARCHITECTURE MEDIUM; `SettingKeys.Notifications.cs`; the four HO-ACC-001 files (`AccountsModule.cs`, `CustomerProfileController.cs`, `ChangeNotificationPreferencesRequest.cs`, `ChangeNotificationPreferencesCommandValidator.cs` — `Changes` losing `required` is intentional, §12.4's "optional `preferredChannel`" amendment, not reverted); `UniqueConstraintExceptionFilter.cs`, `NotificationRateLimitPolicies.cs`; `NotificationOptionsValidator.cs`, `NotificationResendEvaluation.cs`; the squashed `AddNotificationsModule` migration (integrator's own call, ADR-341 point 5); `tests/CatEcommerce.Infrastructure.Tests/**`. The stale "registered a second time … wins" comments in `AccountsModule.cs`/`NotificationsModule.cs` are corrected — `NotificationPasswordResetTokenDispatcher` has been the only `IPasswordResetTokenDispatcher` registration since the two dispatcher classes it replaced were deleted. **Test additions (§16):** I-A1 (host resolves with `ValidateScopes`/`ValidateOnBuild`, already covered), I-B7 (both channels permanently failing), I-B11/I-B12 (**this was wrong — corrected in the 2026-09-27 second-pass row below**: at the time, no test exercised `EfNotificationClaimStore`, a concurrent claim, or `xmin`; the only claim tests were domain-level `NotificationAggregate.Claim` unit tests, which never touch the store), I-D10 (merge moves the inbox), I-D11 (anonymisation redacts, including a merged-away duplicate), I-E4 (unsubscribe 429), plus new cases for the redaction-sweep starvation fix, the poison-render fix and the concurrent-resend fix. `NotificationsIntegrationTestBase.SafeDaytimeInstant()` replaces three test files' fixed historical `FakeTimeProvider` literals with a value anchored to the real current instant (still shifted into the same safe daytime UTC band) — against this branch's shared, persistent database (§16.1), a fixed literal reused across every run stopped isolating anything, and pushed a fresh test's own row off a paged, unscoped query (reproduced: `SearchByRecipientDestinationTemplateStatus_FindsMatchingRows_DestinationsMasked`). <br><br>**2026-09-27 — integrator merge (`eef1621`, ho/CAT-001-gates), self-amendment.** (40) `Notifications:WebBaseUrl` is removed; `NotificationLinkBuilder`'s `PasswordResetLink`/`CatCareLink`/`UnsubscribeLink` now read the shared `PublicSiteOptions.WebBaseUrl` (`CatProfilesModule.cs`, also used for the cage-card QR code, US-CAT-11), validated by `PublicSiteStartupGuardHostedService` — this also resolves item (28)'s sibling `.invalid`-placeholder concern for the web URL, since that guard already fails Production loud when the shared value is unset. `Notifications:ApiBaseUrl` is unaffected (no shared equivalent exists). `PublicSite:WebBaseUrl` stays empty by default in Testing (`CatApiFactory`'s own owner-ratified default, ACCEPTANCE gate A22); `NotificationsApiFactory` sets it explicitly for its own tests, the same way `CatApiFactory.WithPublicSiteWebBaseUrl` opts one Cat test in — `NotificationLinkBuilder` has no relative-path fallback the way `GetCatCageCardQueryHandler` does. Migration check: the merge auto-combined the snapshot cleanly (no conflict with `ho/CAT-001-gates`' own `WidenAuditEntryValues`); `has-pending-model-changes` stayed clean without regenerating `AddNotificationsModule`. <br><br>**2026-09-27 — SECURITY/CODE_REVIEW gate fixes, second pass FAIL on `4efe96f`, self-amendment.** Full findings in `docs/gates/HO-NTF-001-{SECURITY,CODE_REVIEW}.md` (ARCHITECTURE passed this round, `cc78961`). (41) `ExpandNotificationRequestCommandHandler`'s render catch (widened in (28) to any non-cancellation exception) is narrowed to a CLOSED set — `InvalidOperationException`, `FormatException`, `ArgumentOutOfRangeException` — because that widening also caught a genuine, transient database failure from `quietHoursCalendar.NextAllowedInstantAsync` and permanently `Suppressed` the request instead of leaving it `Pending` to retry (CODE_REVIEW HIGH). (42) `RenderedMessageColumnLimits.Enforce` (new, Domain), called right after every `NotificationRenderer.Render` (C4 expand and inbox, C5 fallback, C7 resend): truncates `Subject`/`Title` with an ellipsis (never splitting a UTF-16 surrogate pair) and throws `NotificationRenderOverflowException` (an `InvalidOperationException`, already in the closed sets above) for an overlong `Body`/`LinkPath` — a rendered field longer than its `notify.notification` column used to reach Postgres as a 22001 error OUTSIDE the render try/catch, leaving the row stuck `Pending` forever (C4, oldest-first, blocking every request behind it including password resets — probe P6c), `Sending` forever (C5 fallback), or returning 500 (C7 resend, which now falls back to the original channel's already-safe fields instead). (43) `NotificationRequestAggregate.RecordFailedAttempt` (new `AttemptCount`/`LastAttemptOn`; migration `AddNotificationRequestAttemptTracking`) moves a request to a new terminal `NotificationSuppressionReason.PersistenceFailed` after `NotificationRequestRules.MaxExpansionAttempts` (5) failed SAVE attempts (never render failures, suppressed immediately by (42)) — a backstop for any OTHER repeated save failure; `NotificationWorkQueue.GetPendingRequestIdsAsync` now orders never-attempted requests before previously-failed ones. `RecordNotificationExpansionFailureCommand`/`Handler` (new) records the failure from a BRAND NEW scope in `NotificationWorkCycle.DispatchAsync`, since a scope whose `SaveChangesAsync` just failed inside an implicit transaction refuses every further command. **(as first written, this fired on ANY exception, not only a save failure — corrected in the 2026-09-27 third-pass row below.)** (44) `TemplateRules`'s "site-relative" link check (`IsSiteRelativePath`, new) now also rejects any target containing a backslash — `/\evil.test` used to pass (`StartsWith('/') && !StartsWith("//")`) and a browser resolves it to `https://evil.test/` under WHATWG URL parsing (SECURITY LOW). (45) The I-B11/I-B12 claim in this row's 2026-09-27 first-pass entry above was false — see the correction inline there; real coverage is `NotificationClaimAndDeliveryEndpointTests` (I-B6 backoff, I-B11 concurrent claim via `xmin`, I-B12 expired-lease reclaim, the A15 at-least-once crash-recovery case, I-B13 concurrent dedup) and `NotificationOverflowEndpointTests`/`NotificationLogAndInboxEndpointTests` (I-B14 anonymised recipient suppressed). **LOW:** (46) `SettingKeys.cs`'s XML doc corrected (named a nonexistent `SettingKeys.Notifications` member). (47) `Program.cs`'s unused `using Microsoft.AspNetCore.RateLimiting;` removed. (48) `ResendNotificationCommandHandler`'s cross-channel re-render catch now logs a `Warning` (notification id and channel only, never content) before falling back, instead of silently swallowing. (49) `ValidationRuleExtensions.cs` (the `NoPrivateUseOrFormatCharacters` rule referenced in (28)) is recorded here as the file ARCHITECTURE's second pass asked to add to this list — it was an oversight in the first-pass row, not a new file this round. Migration: `AddNotificationRequestAttemptTracking` from this round is squashed together with the three from the first pass into one regenerated `AddNotificationsModule` before this branch merges (see (35)'s and the ARCHITECTURE gate's own squash note). <br><br>**2026-09-27 — CODE_REVIEW gate fix, third pass FAIL on `5aad2d7` (SECURITY passed this round, `ebeba86`), self-amendment.** Full finding in `docs/gates/HO-NTF-001-CODE_REVIEW.md`. (50) **Correction to (43):** `NotificationWorkCycle.DispatchAsync`'s catch called `RecordExpandFailureAsync` (and so counted toward `MaxExpansionAttempts`) on ANY exception escaping `ExpandNotificationRequestCommand`'s dispatch, not only a genuine save failure — a transient DB read failure from `recipientDirectory.ResolveAsync`, `subjectGuardResolver.IsStillValidAsync`, `branchRepository.GetByIdAsync`, `templateRepository.GetByFilterAsync` or `quietHoursCalendar.NextAllowedInstantAsync` (the exact scenario (41)'s own regression test exists for) therefore counted too, and after 5 consecutive failing polls (~20-25s at the default 5s interval) permanently `Suppressed` the request as `PersistenceFailed` — reopening a bounded version of the very defect (41) fixed a few commits earlier in the SAME round (CODE_REVIEW HIGH). Fixed by narrowing the condition to `ex is DbUpdateException` (`DbUpdateConcurrencyException` derives from it) — thrown only by `EfAggregateRootRepository.CommitAsync`'s `SaveChangesAsync()`, never by the plain read calls above, which throw `NpgsqlException`/`TimeoutException`/a bare `Exception` instead. New regression test drives the throwing quiet-hours calendar for `MaxExpansionAttempts + 3` consecutive passes and asserts the request stays `Pending` with `AttemptCount` at 0 throughout, then heals once the dependency recovers (`QuietHoursEndpointTests.QuietHoursCalendarThrowsRepeatedly_PastMaxExpansionAttempts_StillPendingNeverCounted`). (51) `RenderedMessageColumnLimitsTests.TruncationNeverSplitsASurrogatePair`'s input corrected so the naive cut point actually lands on the high surrogate (98 `a`s + the pair + one more character, not 99 `a`s + the pair) — the original input never reached the back-off branch it was named for (CODE_REVIEW LOW, test-quality only, no product bug). <br><br>**2026-09-27 — TEST gate fix, FAIL on `7fb5cfa` (owner rule: one MEDIUM), self-amendment.** Full finding in `docs/gates/HO-NTF-001-TEST.md`/`docs/gates/TC-HO-NTF-001.md`. (52) **§8.12 corrected:** `CareReminderSweep`'s `DeduplicationKey` gains the owner id — `"care:{kind}:{catId:N}:{refId:N}:{ownerId:N}:{dueOn:yyyyMMdd}"` (was `"care:{kind}:{catId:N}:{refId:N}:{dueOn:yyyyMMdd}"`, no owner component at all). Without it, `RequestNotificationCommandHandler`'s dedup pre-check (`requestRepository.ExistAsync(r => r.DeduplicationKey == ...)`, §8.4/§11.1 C3, which matches ANY existing row with that key regardless of status) meant a cat's ownership transfer permanently blocked a reminder for the new owner: the original owner's request is correctly `Suppressed(SubjectNoLongerApplies)` at expansion once the guard (§8.7) sees the owner mismatch, but that request had already consumed the key, and a transfer changes neither the vaccination/treatment record nor its `NextDueOn` — so every later sweep computed the IDENTICAL key for the new owner and was silently treated as "already requested", for as long as a year (until the vaccine's own `NextDueOn` next changed). This is I-F5/A6's own acceptance evidence ("the next sweep requests one for the new owner"), reproduced deterministically by the TEST gate's own committed regression test, `CareReminderEndpointTests.TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner` — left unweakened; it now passes for real. **Why the owner id, not "ignore suppressed rows on dedup":** `RequestNotificationCommandHandler`'s dedup pre-check is shared by every producer of `RequestNotificationCommand` (§11.1 C3), not only the care-reminder sweep; loosening its status filter would be a broader behavioural change to dedup semantics for every other caller (a lower-risk, single-producer fix was preferred). **Migration/index impact: none.** `deduplication_key` is `varchar(200)` (`NotificationRequestConfiguration`) and the unique filtered index (`ix_notification_request_dedup`) is unchanged — only the STRING CONTENT computed for one producer changed (comfortably still within `NotificationRequestRules.MaxDeduplicationKeyLength` (200) at ~124 characters), no column/index/schema change, so no migration was needed; `has-pending-model-changes` was re-verified clean against `catecommerce_ntf`. (53) U-F3 (`DeduplicationKey_MatchesTheSpecifiedFormat`) updated to assert the new format; its own doc comment cross-references (52). <br><br>**2026-09-27 — ACCEPTANCE gate fix, FAIL on `2cd47d5` (one HIGH: ADR-055..066 never recorded), self-amendment.** Full finding in `docs/gates/HO-NTF-001-ACCEPTANCE.md`. (54) **ADR-055 to ADR-066 appended** to `.ai/decisions/adr.md` (README's ADR table already allocated this range to this handover; §9 said to append them, and this was missed every earlier gate round) — each describes the shipped design, folding in (25) into ADR-063, (40) into ADR-061, (43)/(50) into ADR-056 and (52) into ADR-065 so the ADR text matches the code, not the first draft. (55) **§1 overflow, recorded (ACCEPTANCE A-6):** `src/Api/CatEcommerce.Api/appsettings.Testing.json` (authorised in ARCHITECTURE pass 2, never recorded here) and `src/Api/CatEcommerce.Api/Modules/FileStorageModule.cs` (a comment-only correction ARCHITECTURE asked for at the master merge) are both integrator/gate-authorised files outside the literal §13 list. (56) §15 U-F3 and §8.8 corrected in place to the owner-id key format and the shared `PublicSiteOptions.WebBaseUrl` (ACCEPTANCE A-5) — see those sections directly rather than repeating the text here. (57) `docs/test-cases/TC-HO-NTF-001.md` (moved from `docs/gates/`, ACCEPTANCE A-1, matching every other area's location) is this handover's test-case sheet; `docs/gates/HO-NTF-001-TEST.md` and this row both point to the new path. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Give every area **one reliable way to reach a person**. After this slice a command handler anywhere
in the solution can say "send template *X* to customer *Y* with these values", and the Notification
context decides the channel, respects consent and quiet hours, delivers at least once, falls back
when a channel fails, keeps an in-app copy, and logs every attempt so support can see and resend it.

The first real consumer is **password reset**. HO-ACC-001 §4.3 left reset delivery as a
Development-only log line. This slice replaces that with real delivery machinery. The second consumer
is **care reminders** for vaccinations and deworming, which Cat Profiles already records.

**No SMS or email provider is chosen here** (owner instruction, 2026-09-22). The slice delivers the
provider-neutral sending port, a Development sender that logs, a Testing sender that captures, and an
`Unconfigured` sender used everywhere else. Adding a real provider later is one adapter class and one
configuration section (§8.9, OQ-1). **Until then nothing leaves the building outside Development**,
and the in-app inbox is the only channel that works in production (§4.3).

Five things this slice is careful about:

1. **Atomic intent.** A notification request commits in the **same transaction** as the business
   change that caused it, so there is never a reset token without its message or a message about a
   change that rolled back (ADR-056, F23).
2. **Consent.** Transactional messages always go. Marketing messages go only where consent is
   granted, per channel, and always carry an unsubscribe link (ADR-058, ADR-064).
3. **Secrets.** A reset link is a credential. It is never logged outside Development, never copied to
   the inbox, never resent, and redacted from storage as soon as it is delivered or expires (ADR-063).
4. **Injection.** Values such as a cat's name are inserted into templates as literal text *after*
   the template is parsed. A value can never become markup, a link or a placeholder (ADR-062).
5. **Time.** Quiet hours are evaluated in the branch's time zone through HO-ADM-001's time model;
   every instant is `timestamptz` (ADR-059).

§17 builds the slice in **six phases**; each compiles, migrates and passes its own tests first.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-NTF-02 | S | 8 | Code-defined template catalogue; administrator-edited wording per channel with `{{placeholder}}` validation (the offending variable is named) and a server-rendered preview; reset to default | A |
| US-NTF-01 | M | 8 | Notification requests enlisted in the producer's transaction; routing to the customer's **preferred channel**; in-app inbox copy always on; delivery worker with retry; fallback to the other channel on permanent failure, logged; password-reset delivery | B |
| US-NTF-04 | M | 5 | Transactional always delivered regardless of marketing consent; marketing only on consented channels; unsubscribe link required in every marketing template; public unsubscribe (page and one-click) | B, E |
| US-NTF-05 | S | 3 | Quiet hours from HO-ADM-001's `notifications.quietHours` setting: normal messages are held until the window opens; urgent ones are not; the inbox copy is never held | C |
| US-NTF-06 | M | 5 | Searchable notification log with channel, template, recipient, status and every provider response; manual resend, logged and audited | D |
| US-NTF-03 | M | 8 | Vaccination and deworming reminders ahead of the due date with a direct link; never for a deceased cat, a transferred cat or a record that has since been renewed. Prescription refills: the contract only (§4.2) | F |

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409` | ADR-011, ADR-016 |
| **Anything that leaves the process goes through an outbox**, delivered by a `BackgroundService` in the Api host | `.ai/domain-events-and-outbox.md` §4, `.ai/architecture.md` |
| `[RequireAction]` + the one `ActionRoles` table; every endpoint declares its access | HO-ACC-001 ADR-036, HO-ADM-001 ADR-043 |
| Marketing consent is stored per channel, opt-in by default; transactional is not representable as "off" | HO-ACC-001 ADR-035 |
| Reset tokens: 256 bits, hashed at rest, 15 min, neutral request response | HO-ACC-001 ADR-034 |
| Every aggregate change audited in the same transaction, with secrets redacted | HO-ACC-001 ADR-040 |
| Business settings through `IBusinessSettings`; branch-local time via the branch's IANA zone | HO-ADM-001 ADR-045, ADR-050 |
| Content is `LimitedMarkup`, never HTML | HO-ADM-001 ADR-052 |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-NTF-002`](HO-NTF-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-NTF-02 "preview it with sample data before saving" | `POST …/preview` renders unsaved content with the definition's samples; nothing is written |
| US-NTF-02 "saving is refused with the offending variable named" | `400 UnknownTemplatePlaceholder` whose message names `{{variable}}` and whose `propertyName` points at the channel field |
| US-NTF-06 "logged and searchable" | Paged, filtered `GET /api/admin/notifications` and a detail endpoint with every attempt |
| US-NTF-01 "in-app inbox" | `GET /api/customers/me/inbox`, unread count, mark read |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Story / FR | Where it lands | What this slice delivers |
|---|---|---|---|
| Appointment confirmation and reminders | FR-NTF-3 | **HO-APT-001** | `INotificationOutbox`, the catalogue extension rule (§8.2) and the subject-guard extension point (§8.7) |
| Prescription refill reminders | US-NTF-03 AC1 | **HO-PHM-001** | Same — a `PrescriptionRefillDue` subject kind is added by Pharmacy |
| Surgery pre-op and post-op messages | FR-NTF-3 | HO-SRG-001 | Same |
| Boarding confirmation, check-in and daily updates; **incident alerts within 15 minutes with phone-call escalation** | FR-NTF-3, **FR-NTF-7** | **HO-BRD-001** | `Urgent` templates bypass quiet hours and are picked up within one poll interval (5 s); escalation to a phone-call task is Boarding's |
| Order confirmation, dispatch, delivery; subscription pre-charge; back-in-stock; abandoned cart | FR-NTF-3 | HO-ORD, HO-SUB, HO-SHP | Same; abandoned-cart and back-in-stock are **marketing** templates and inherit the unsubscribe rule |
| Booking link in reminders pointing at the booking flow | US-NTF-03 AC1 | **HO-APT-001** | `INotificationLinkBuilder.CatCareLink(catId)` returns the portal cat page until HO-APT-001 changes that one method (§8.8) |
| Notifying staff of leave decisions | HO-ADM-001 §4.3 | HO-ADM amendment | Staff are supported recipients (§8.5); the producer is not written here |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No SMS or email provider** (OQ-1) | Outside Development the SMS and email senders are `Unconfigured`: every attempt is a permanent failure with the provider response *"No SMS provider is configured"*, the fallback is tried and fails the same way, and the notification ends `Undeliverable`. **The in-app copy is delivered.** Password reset therefore still does not work in production until a provider adapter is added. A startup warning says so (§12.2) |
| Push notifications (SRS §3.3 says Phase 2) | `Push` exists as a channel value (HO-ACC-001) and templates may carry push wording, but routing never selects it (`NotificationChannelRules.DeliverableChannels`) |
| Delivery receipts ("delivered to handset", bounces) | `Sent` means **accepted by the provider**. Receipt webhooks need a provider (OQ-1) |
| A second language (SRS C-8, Q-12) | Templates carry a `Locale` column fixed to `en`; a second locale is data plus a recipient language preference, later |
| Log retention | The log keeps rows indefinitely; retention enforcement is HO-XCT |
| Marketing campaigns (sending one message to many) | No FR asks for a campaign tool. Marketing templates are sent by the areas that own them (back-in-stock, abandoned cart) |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, `IEventPublisher`, MassTransit, RabbitMQ, a second host, a queue product, a
`CommandService` layer, `202 Accepted`, `[Authorize]`. **No HTML template input** — email bodies are
`LimitedMarkup`, rendered to HTML by the server. **No templating library** (Scriban, Handlebars,
Razor) — the placeholder grammar is one regular expression (§8.3). **No provider SDK** in this slice.
**No enqueueing from a domain event handler** (ADR-057).

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| **HO-ACC-001 merged** — accounts, preferences, reset tokens, `IPasswordResetTokenDispatcher`, audit interceptor, `ActionRoles`, rate limiter | ❌ **not implemented at baseline** — §20.1 |
| **HO-ADM-001 merged** — `IBusinessSettings`, `SettingDefinitions`, branches and time zones, `LimitedMarkup`, `EndpointAccessConventionTests` | ❌ **not implemented at baseline** — §20.1 |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ |
| New NuGet packages | **None.** `BackgroundService`, `TimeProvider`, `System.Text.Json`, `RandomNumberGenerator` and `System.Net.WebUtility` are in the runtime |

The shared-database rules of HO-CAT-001 §16.1 and HO-ACC-001 §16.1 apply. **Deduplication keys and
template keys are unique** — mint deduplication keys per test; tests never edit a shipped template
without restoring it (§16.1).

---

## 6. Governing architecture documents

Identical to [HO-ACC-001 §6](HO-ACC-001.md#6-governing-architecture-documents). Of note here:

| Document | Why it matters in this slice |
|---|---|
| **`.ai/domain-events-and-outbox.md` §4** | The outbox this slice builds. "The intent to send is written in the same transaction"; "delivery is at-least-once"; "never put a business decision in the payload"; "the dispatcher is a `BackgroundService` in the Api host" |
| `.ai/domain-events-and-outbox.md` §3 | A failing event handler fails the command *after* it committed — the reason producers enqueue in the command handler, never in an event handler (ADR-057) |
| `.ai/di-registration-guide.md` | "The one legitimate Singleton that needs a Scoped service is the outbox dispatcher" — it creates a scope per unit of work |
| `.ai/bounded-contexts.md` rule 1 | One aggregate per command; this slice records one exception (ADR-056) |
| `.ai/bounded-contexts.md` rule 3 | Requests and notifications accumulate without bound — their own roots, never collections on a customer |
| HO-ADM-001 ADR-047 | "No background job" — this slice's worker is the outbox dispatcher the architecture already names, plus one timed producer (ADR-065). ADR-047 still holds for Administration |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18 and HO-ADM-001 F19–F22 apply. Additionally, read from the code
at `4007225`:

| # | Fact | Consequence |
|---|---|---|
| F23 | `EfAggregateRootRepository.CommitAsync` calls `db.SaveChangesAsync()` on the **scoped** `CatEcommerceDbContext`, which commits **every tracked entity**, not only the aggregate passed in | An entity added to the same context before a repository save commits **atomically** with it. `INotificationOutbox.Enqueue` relies on this (§8.4) |
| F24 | The repository dispatches domain events **after** `SaveChangesAsync`, in the same scope | An event handler that enqueued would need a second commit — after the first is durable. Hence ADR-057 |
| F25 | `CatAggregate` exposes `LifeStatus` (`Alive`, `Deceased`), `OwnerId`, `Vaccinations[]` (`VaccineId`, `NextDueOn`) and `ParasiteTreatments[]` (`Type`, `NextDueOn`); `ParasiteTreatmentType` is `Deworming`, `FleaAndTick`, `Combined` | Reminder inputs exist today. `Combined` counts as deworming |
| F26 | `CatVaccinationStatusCalculator` already selects the latest record per vaccine | The reminder query reuses it; it does not re-derive "latest" in SQL |
| F27 | `CatAggregate` raises `CatMarkedDeceasedEvent` and `CatOwnershipTransferredEvent` | Not subscribed to. Reminders are re-checked against the cat at send time instead (§8.7), which covers both without an event handler touching many rows |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Notification context — schema `notify`
NotificationTemplateAggregate   (root)                        notify.notification_template
└── Contents [owned many ≤ 4]   Channel, Subject?, Title?, Body, LinkPath?   notify.notification_template_content
NotificationRequestAggregate    (root)                        notify.notification_request
NotificationAggregate           (root)  → RequestId           notify.notification
└── Attempts [owned many ≤ 6]   DeliveryAttempt               notify.notification_attempt
```

**One aggregate mutated per command**, with one recorded exception (ADR-056): **expanding a request**
and **recording a failure that triggers a fallback** each commit the source root and the new
notification(s) in **one** `SaveChangesAsync` — atomic, unlike HO-ADM-001's two-write exception.

**Why requests and notifications are separate roots.** A request is what a producer asked for, in the
producer's transaction, before anyone knows the channel. A notification is one message on one channel
to one destination, with its own attempts and status. One request becomes zero (suppressed), one, or
two (external + inbox copy) notifications, and a failure adds a fallback. The notification row is at
once the **outbox item**, the **delivery log** (FR-NTF-6) and, for the in-app channel, the **inbox
item** — three jobs, one row, no copies to drift.

**Not owned here.** Marketing consent and the preferred channel live on HO-ACC-001's
`CustomerNotificationPreferencesAggregate` (§8.5). Quiet hours and reminder lead times are HO-ADM-001
settings (§10.2).

### 8.2 Template catalogue and templates — US-NTF-02 — ADR-062

**What exists is code; what it says is data.** `NotificationTemplateCatalogue` (Shared, static,
exposed through `INotificationTemplateCatalogue` so tests can add definitions) lists every template the
system can send. A definition:

| Field | Meaning |
|---|---|
| `Key` | `^[a-z]+(\.[a-z0-9-]+)+$`, ≤ 80, e.g. `account.password-reset` |
| `DisplayName`, `Description` | For the admin screen |
| `Category` | `Transactional` \| `Marketing` (HO-ACC-001's `NotificationCategory`) |
| `Urgency` | `Urgent` (ignores quiet hours) \| `Normal` |
| `Channels` | The channels the template has wording for — a subset of `Sms`, `Email`, `InApp`, `Push` |
| `ContainsSecret` | The rendered message carries a credential (§8.10) |
| `Variables` | `Name` (`^[a-z][A-Za-z0-9]*$`), `Description`, `Kind` (`Text` \| `Url` \| `Date`), `Sample`, `RequiredOn` (channels whose body must contain it) |
| `Defaults` | The shipped wording per channel |

**Standard variables**, available to every template and filled by the Notification context, not the
producer: `recipientName` (the account's full name), `clinicName` and `clinicPhone` (the request's
branch, else `AdministrationSeed.MainBranchId`). Marketing templates also get **`unsubscribeLink`**
(`Url`), which is **required on every channel** of a marketing template and **unavailable** to a
transactional one.

**Shipped definitions** (later areas append their own, in their handover):

| Key | Category | Urgency | Channels | Secret | Producer-supplied variables (required on) |
|---|---|---|---|---|---|
| `account.password-reset` | Transactional | **Urgent** — the recipient asked for it seconds ago | Sms, Email | **✅** | `resetLink` (Url; Sms, Email), `expiresInMinutes` (Text) |
| `care.vaccination-due` | Transactional (medical, FR-NTF-4) | Normal | Sms, Email, InApp | — | `catName`, `vaccineName`, `dueOn` (Date), `bookingLink` (Url; every channel — "a direct booking link", US-NTF-03) |
| `care.deworming-due` | Transactional | Normal | Sms, Email, InApp | — | `catName`, `treatmentName`, `dueOn`, `bookingLink` (every channel) |

**`NotificationTemplateAggregate`** — exists only once a template is customised. `Key` (unique, must be
in the catalogue), `Locale` (`en` — C-8), `Contents` (one per channel of the definition, exactly),
`ContentVersion` (1, 2, 3 …), `LastEditedBy`, `LastEditedOn`.

| Method | Invariants | Event |
|---|---|---|
| `Customise(definition, contents)` | `TemplateRules.Validate` (below) | `NotificationTemplateCustomisedEvent` (`Key`, `ContentVersion`) |
| `Edit(definition, contents)` | same; `ContentVersion + 1` | same |
| `ResetToDefault()` | Clears `Contents`; `ContentVersion + 1`; reads fall back to the catalogue | `NotificationTemplateResetEvent` |

**Effective template** = the aggregate's contents if customised, else the definition's defaults
(`ContentVersion = 0`). Every notification records the version it was rendered from.

**`TemplateRules.Validate(definition, contents)`** — Shared, pure, one violation list:

1. Every channel of the definition present once, no other channel → `TemplateChannelMismatch`.
2. Placeholders match `\{\{([a-z][A-Za-z0-9]*)\}\}`; any other `{{` or `}}` →
   `MalformedTemplatePlaceholder` (message quotes the fragment).
3. A placeholder that is neither a definition variable nor a standard variable →
   **`UnknownTemplatePlaceholder`**, message *"`{{catname}}` is not a variable of this template.
   Available: catName, vaccineName, dueOn, bookingLink, recipientName, clinicName, clinicPhone."*,
   `propertyName` `Contents[i].Body` (or `.Subject` / `.Title` / `.LinkPath`). **US-NTF-02 AC2.**
4. A `RequiredOn` variable missing from that channel's body → `RequiredTemplatePlaceholderMissing`.
5. Lengths: email `Subject` 1…150, `Body` 1…10 000; SMS `Body` 1…480; in-app `Title` 1…100, `Body`
   1…1000; push `Title` 1…65, `Body` 1…240.
6. Email `Body` passes HO-ADM-001's `LimitedMarkup.Validate`, with one extension: a link target may be
   a single `Url`-kind placeholder, e.g. `[Book now]({{bookingLink}})`. SMS, in-app and push bodies are
   **plain text** — `<`, `>` and markup link syntax are refused (`InvalidTemplateText`).
7. In-app `LinkPath` is empty, a site-relative path (`/…`, never `//`), or one `Url` placeholder.

### 8.3 Rendering — ADR-062

`NotificationRenderer` (Domain.DomainServices, pure): `Render(effectiveTemplate, channel, values) →
RenderedMessage(Subject?, Title?, TextBody, HtmlBody?, LinkPath?)`.

1. **Parse first, substitute second.** The template text is tokenised (markup tokens and
   `{{placeholder}}` tokens); values are then inserted as **literal text nodes**. A cat named
   `**Mitu**](https://evil)` renders as those characters. A value containing `{{x}}` is not expanded.
2. `Url` values must be absolute `https://` URLs built by `INotificationLinkBuilder` (§8.8); any other
   value for a `Url` variable is a programming error (`InvalidOperationException`).
3. `Date` values are `yyyy-MM-dd` strings from the producer and are rendered `d MMM yyyy` in the
   invariant English culture (`12 Oct 2026`).
4. **Email**: `HtmlBody` is produced from the markup tree with **every text node HTML-encoded**
   (`WebUtility.HtmlEncode`) inside a fixed, inline-styled shell (clinic name header, body, footer with
   `clinicPhone` and, for marketing, the unsubscribe link). `TextBody` is the markup text with values
   substituted — the plain-text alternative.
5. **SMS**: `TextBody` only. `SmsSegments.Measure(text)` → `(Encoding: Gsm7 | Ucs2, Characters,
   Segments)` — 160/153 for GSM-7, 70/67 for UCS-2. The renderer **never truncates** (a truncated reset
   link is a broken one); the preview shows the segment count instead.
6. Missing value for a required variable → the request is **suppressed** with `MissingVariable`
   (the producer broke the contract — logged at `Error` with the key and variable name, never values).

### 8.4 Requests — the outbox — ADR-056, ADR-057

**`NotificationRequestAggregate`**

| Field | Type | Rules |
|---|---|---|
| `TemplateKey` | string | In the catalogue |
| `RecipientKind` / `RecipientId` | `NotificationRecipientKind` (`Customer` \| `Staff`) / Guid | |
| `Variables` | `jsonb` map string → string | ≤ 30 entries, keys per the definition, values ≤ 2000 chars |
| `ChannelHint` | `NotificationChannel?` | Transactional only; "use this channel first" (password reset uses the identifier the person typed) |
| `SubjectKind` / `SubjectId` / `SubjectRef` / `SubjectDueOn` | `NotificationSubjectKind?` / Guid? / Guid? / DateOnly? | What the message is about, for the send-time guard (§8.7) |
| `BranchId` | Guid? | Branch context for `clinicName` and quiet hours |
| `DeduplicationKey` | string? ≤ 200 | Unique when present (§8.11) |
| `NotAfter` | `DateTimeOffset?` | Past this instant nothing is sent (a reset link's expiry) |
| `ContainsSecret` | bool | Copied from the definition at creation |
| `Status` | `NotificationRequestStatus` | `Pending` → `Expanded` \| `Suppressed` \| `Expired` |
| `SuppressionReason` | `NotificationSuppressionReason?` | Set with `Suppressed` |
| `RequestedOn`, `ProcessedOn?`, `RequestedBy`, `CorrelationId`, `VariablesRedactedOn?` | | |

Methods: `Create`, `MarkExpanded`, `Suppress(reason)`, `Expire`, `RedactVariables` (replaces every
value with `"[redacted]"`, idempotent). **No domain events** — nothing subscribes to them.

**`INotificationOutbox`** (Domain.DomainServices port) — the only way a producer asks for a message:

```csharp
void Enqueue(NotificationRequestDto request);
```

The implementation (`EfNotificationOutbox`) builds the aggregate and **adds it to the scoped
`DbContext` without saving**. The producer's own repository save then commits it with the business
change (F23). If the producer's handler throws before saving, the scope is discarded and the request
never existed. That is the whole outbox guarantee, and it costs one line in the producer.

**The producer rules** (ADR-057, checked by the ARCHITECTURE gate):

1. Call `Enqueue` **in the command handler, before** the aggregate's `SaveAsync` / `UpdateAsync`.
2. **Never from a domain event handler.** Events dispatch after the commit (F24); a failure there
   fails a request whose change is already durable (`.ai/domain-events-and-outbox.md` §3).
3. **Timed producers** (the reminder sweep, §8.12) have no business change to ride on. They dispatch
   `RequestNotificationCommand` with a `DeduplicationKey`, which saves the request on its own and
   treats a duplicate key as success.
4. Never pass a business decision in `Variables`. If a message should not be sent, do not enqueue it.

### 8.5 Recipients and routing — US-NTF-01, US-NTF-04 — ADR-058

**Preferred channel — an amendment to HO-ACC-001's aggregate, built here.**
`CustomerNotificationPreferencesAggregate` gains `PreferredChannel` (`NotificationChannel?`, only `Sms`
or `Email`) and `ChangePreferredChannel(channel)`. It is null until chosen; the **effective** preferred
channel is `PreferredChannel ?? (mobile present ? Sms : Email)`. The handler refuses a channel the
account has no identifier for (`PreferredChannelHasNoDestination`). Sent through HO-ACC-001's existing
`ChangeNotificationPreferencesCommand` (new optional `PreferredChannel`) and returned by its Q-E2.
Staff have no preferences: the effective channel is the hint, else email, else SMS.

**`INotificationRecipientDirectory`** (port, implemented read-only over `account.*`):
`ResolveAsync(kind, id) → RecipientProfile?` with `Kind`, `Id`, `IsActive`, `FullName`, `MobileNumber?`,
`EmailAddress?`, `EffectivePreferredChannel`, `MarketingGranted(channel)`.

- A **merged** customer resolves to the survivor (`MergedIntoCustomerId`, followed at most 5 hops).
- An **anonymised** customer, or a staff account that is not active, resolves with `IsActive = false`.

**`NotificationRoutingPolicy`** (Domain.DomainServices, **pure**):

```
Plan(definition, recipient, channelHint)       → RoutingPlan(PrimaryChannel?, InboxCopy, SuppressionReason?)
NextFallback(definition, recipient, tried[])   → NotificationChannel?
```

| Rule | |
|---|---|
| R1 | Recipient inactive → suppressed `RecipientInactive` |
| R2 | **Candidate external channels**, in order: `channelHint` (transactional only), the effective preferred channel, the other of `Sms`/`Email`. Kept only if the definition has wording for it **and** the recipient has a destination for it |
| R3 | **Transactional**: every candidate is permitted. Consent is not consulted — **US-NTF-04 AC2** |
| R4 | **Marketing**: a candidate is permitted only if `MarketingGranted(channel)` — **US-NTF-04 AC1** |
| R5 | Primary = the first permitted candidate |
| R6 | **Inbox copy** = recipient is a customer **and** the definition has `InApp` wording **and** it is not `ContainsSecret` **and** (transactional, or marketing with `InApp` consent). It is created even when a primary exists — "always-on" (US-NTF-01 AC1) |
| R7 | No primary and no inbox copy → suppressed `NoPermittedChannel` (e.g. marketing to someone who consented to nothing) |
| R8 | `NextFallback` = the next permitted candidate not in `tried`; none → null, and the failure stands, logged |
| R9 | `Push` is never a candidate (`NotificationChannelRules.DeliverableChannels`) — §4.3 |

### 8.6 Notifications — delivery state — ADR-060

**`NotificationAggregate`**

| Field | Notes |
|---|---|
| `RequestId`, `TemplateKey`, `TemplateVersion`, `Locale`, `Category`, `Urgency`, `ContainsSecret` | Copied at creation |
| `RecipientKind`, `RecipientId`, `Subject*`, `BranchId` | Copied from the request (subject fields drive the send-time guard) |
| `Channel` | `Sms`, `Email` or `InApp` |
| `Destination` | Normalised mobile or email; **null for `InApp`** |
| `Subject?`, `Title?`, `Body`, `HtmlBody?`, `LinkPath?` | The rendered message, frozen — the log shows exactly what was sent |
| `Status` | `NotificationStatus`, below |
| `NotBefore?`, `DeferredForQuietHours` | Held until this instant |
| `NotAfter?` | From the request |
| `NextAttemptOn?`, `LeaseExpiresOn?`, `AttemptCount` | Delivery bookkeeping |
| `Attempts` | Owned, ≤ `NotificationRetryRules.MaxAttempts` (6): `AttemptedOn`, `Outcome` (`Accepted` \| `TransientFailure` \| `PermanentFailure`), `ProviderName`, `ProviderMessageId?` (≤ 200), `ProviderResponse?` (≤ 1000, truncated) |
| `FallbackFromId?`, `ResendOfId?`, `ResentBy?` | The chain |
| `UnsubscribeTokenHash?` | Marketing only (§8.11) |
| `ReadOn?` | `InApp` only |
| `CreatedOn`, `SentOn?`, `ClosedOn?`, `CloseReason?`, `RedactedOn?` | |

**Status machine**

```
            ┌──────────── Defer(until) ───────────┐
            ▼                                     │
Queued ──Claim──► Sending ──Accepted──► Sent      │
  ▲                  │ Transient, attempts left ──► RetryPending ──Claim──► Sending
  │                  │ Transient, none left / Permanent ──► Undeliverable (+ fallback, R8)
  └── lease expired ─┘
Queued / RetryPending ──► Expired (NotAfter passed) · Cancelled (send-time guard)
InboxDelivered (InApp — terminal at creation; ReadOn set later)
```

| Method | Rules |
|---|---|
| `CreateExternal(…)` / `CreateInbox(…)` | Inbox copies are `InboxDelivered` with `SentOn = now` — **never held by quiet hours** |
| `Claim(now, lease)` | Allowed from `Queued`/`RetryPending` when due (`NotBefore`, `NextAttemptOn` ≤ now) or from `Sending` when `LeaseExpiresOn` < now (a crash mid-send). Else `NotificationNotDue`. The claim is committed **before** the provider is called, so the `xmin` check stops two workers claiming one row |
| `RecordAttempt(outcome, now)` | `Accepted` → `Sent`. `TransientFailure` → `RetryPending` with `NextAttemptOn = now + Backoff[AttemptCount-1]` (1, 5, 15, 60, 240 min) while attempts remain, else `Undeliverable`. `PermanentFailure` → `Undeliverable` |
| `Defer(until)` | Only `Queued`/`RetryPending`; sets `NotBefore`, `DeferredForQuietHours = true` |
| `Expire(now)` / `Cancel(reason)` | Only non-terminal |
| `MarkRead(now)` | `InApp` only; idempotent |
| `Redact(reason)` | Body, subject, title, HTML, link → `"[redacted]"`; destination kept unless the reason is `Anonymisation`; idempotent |

**At-least-once.** A crash after the provider accepted but before `RecordAttempt` commits leaves the
row `Sending`; after the lease it is claimed again and sent twice. The notification id is passed to
the provider as the **idempotency reference** (§8.9) so a provider that supports it can drop the
duplicate. The ADR says this plainly.

### 8.7 The send-time guard — US-NTF-03 AC2

A reminder is enqueued hours or days before it is sent, and quiet hours can hold it overnight. Between
enqueue and send, the cat can die, change owner, or get vaccinated. **The guard runs at expansion and
again immediately before each delivery attempt.**

`INotificationSubjectGuard` — one implementation per `NotificationSubjectKind`, resolved by kind:

| Kind | Still valid when | Otherwise |
|---|---|---|
| `CatVaccinationDue` | cat exists, `LifeStatus == Alive`, `OwnerId == RecipientId`, and the latest vaccination for `SubjectRef` (vaccine) still has `NextDueOn == SubjectDueOn` | `Cancelled` / `Suppressed` with `SubjectNoLongerApplies` |
| `CatDewormingDue` | same, over the latest `Deworming`/`Combined` treatment | same |

A request with no subject is always valid. Later areas add kinds (an appointment reminder is valid
while the appointment is still booked). **A guard reads; it never writes.**

### 8.8 Links — `INotificationLinkBuilder`

Port, implemented in Infrastructure over the shared `PublicSiteOptions.WebBaseUrl` (absolute;
**https required outside Development**, validated on start by `PublicSiteStartupGuardHostedService`
— amended by (40)/ADR-061; this was originally a Notifications-only `NotificationOptions.WebBaseUrl`
until Cat Profiles' cage-card QR code needed the same base URL, ACCEPTANCE A-5):

| Method | Result |
|---|---|
| `PasswordResetLink(token)` | `{WebBaseUrl}/reset-password#token={token}` — the fragment keeps the token out of server logs (HO-ACC-002's route) |
| `CatCareLink(catId)` | `{WebBaseUrl}/portal/cats/{catId}` — **HO-APT-001 changes this one method** to the booking flow |
| `UnsubscribeLink(token)` | `{WebBaseUrl}/unsubscribe#token={token}` |
| `OneClickUnsubscribeUrl(token)` | `{ApiBaseUrl}/api/notifications/unsubscribe/one-click?token={token}` (RFC 8058 needs it in the URL) |

In-app `LinkPath` values are stored **site-relative** (the base is stripped), so the inbox never holds
an absolute URL.

### 8.9 Sending — the provider-neutral port — ADR-061

```csharp
public interface INotificationChannelSender
{
    NotificationChannel Channel { get; }
    string ProviderName { get; }
    Task<SendOutcome> SendAsync(OutgoingMessage message, CancellationToken ct);
}

public sealed record OutgoingMessage(
    Guid NotificationId,                 // idempotency reference for the provider
    string Destination,
    string? Subject, string TextBody, string? HtmlBody,
    IReadOnlyDictionary<string, string> Headers);   // List-Unsubscribe, List-Unsubscribe-Post

public sealed record SendOutcome(
    SendOutcomeKind Kind, string? ProviderMessageId, string? ProviderResponse);
```

**Shipped implementations** (`Infrastructure/Notifications/Senders/`), chosen per channel by
`Notifications:Channels:{Sms|Email}:Sender`:

| Sender | Where it may run | Behaviour |
|---|---|---|
| `DevelopmentChannelSender` | **Development only — constructor throws otherwise** | Logs at `Warning` with a `NOT SENT — development sender` banner: channel, destination, subject, text body (so a developer can click the reset link). Returns `Accepted` |
| `CapturingChannelSender` | **Testing only — constructor throws otherwise** | Records every message in memory for assertions; returns outcomes from rules the test factory sets per destination (accept, transient ×N, permanent) |
| `UnconfiguredChannelSender` | Anywhere; **the default outside Development** | Returns `PermanentFailure` with `ProviderResponse = "No {SMS\|email} provider is configured"`. Logs nothing about the message |

**The contract a future provider adapter must meet** (OQ-1; recorded in ADR-061 so the adapter is
reviewed against it):

1. HTTPS only; credentials from environment or secret store, never a committed file.
2. A timeout (`SendTimeoutSeconds`, default 10); a timeout is `TransientFailure`.
3. Classify: network errors, `429` and `5xx` → transient; an invalid destination, a rejected sender or
   `4xx` other than `429` → permanent.
4. Pass `NotificationId` as the provider's client reference or idempotency key where supported.
5. Never log the body, the subject or the full destination. Log the notification id and the outcome.
6. Store the provider's response text truncated to 1000 characters, never the request.

### 8.10 Secret-bearing messages — ADR-063

For a definition with `ContainsSecret` (today only `account.password-reset`):

1. **No inbox copy** (R6), so the link never sits in the portal.
2. **Not resendable** (`NotificationCannotBeResent`). Support tells the person to request a new reset —
   the old token is one request away from superseded anyway.
3. **Redacted as soon as it cannot be needed**: a notification on `Sent`, `Expired` or `Cancelled`,
   or on `Undeliverable` with no fallback left, redacts its body in the same save; the request's
   variables are redacted once the request's `NotAfter` has passed (the worker's sweep, §12.2). A
   **live reset link therefore exists in plaintext in `notify.*` for at most the token lifetime (15
   minutes)**. That is the recorded residual risk; the encrypted-at-rest alternative is OQ-3.
4. **Excluded from audit change capture** (§12.1) — the interceptor would otherwise copy the link into
   the immutable audit table, where it could never be removed.
5. Never logged at any level outside Development.

### 8.11 Unsubscribe and deduplication — US-NTF-04 — ADR-064

**Unsubscribe.** At render time every **marketing** notification gets a fresh 256-bit random token
(`RandomNumberGenerator`, base64url); its SHA-256 is stored in `UnsubscribeTokenHash` (unique index);
the plaintext goes into `unsubscribeLink` and, for email, into `List-Unsubscribe` /
`List-Unsubscribe-Post: List-Unsubscribe=One-Click` headers. Tokens **do not expire** — an old
newsletter's link must still work.

`UnsubscribeFromMarketingCommand(Token)` → hash → load the notification → load the recipient's
`CustomerNotificationPreferencesAggregate` → `ChangePreferences` revoking marketing for **that
notification's channel** with `Source = "unsubscribe-link"`. One aggregate mutated (the preferences);
the change is audited by the interceptor with before and after. Unknown token →
`InvalidUnsubscribeLink`. Already revoked → success, `wasSubscribed: false`. Nothing about the
customer is returned.

**Deduplication.** `DeduplicationKey` has a filtered unique index. `RequestNotificationCommand`
pre-checks and treats the index's `23505` as "already requested" (success, no second row). Enlisted
requests (§8.4) normally carry no key, so a duplicate can never fail a producer's business command.

### 8.12 Care reminders — US-NTF-03 — ADR-065

`CareReminderSweep` (Api, run by the worker every `ReminderSweepIntervalMinutes`, default 60):

1. `today` = the local date in the **main branch's** time zone.
2. Dispatch `GetDueCareRemindersQuery(today, vaccinationLeadDays, dewormingLeadDays)` — lead days from
   `IBusinessSettings` (§10.2). The query returns, for **alive** cats whose owner is an **active**
   customer: the latest vaccination per **active** vaccine (via `CatVaccinationStatusCalculator`, F26)
   and the latest `Deworming`/`Combined` treatment, where `NextDueOn − leadDays ≤ today ≤ NextDueOn`.
3. For each row, dispatch `RequestNotificationCommand` with `care.vaccination-due` /
   `care.deworming-due`, the variables, the subject (`CatVaccinationDue`, cat id, vaccine id, due
   date), and `DeduplicationKey = "care:{kind}:{catId:N}:{refId:N}:{ownerId:N}:{dueOn:yyyyMMdd}"`
   (the owner id was added 2026-09-27, TEST gate fix — see the dated Amended row: without it, a cat
   transfer permanently blocked the new owner's reminder for that due date).

The window, not an exact date, means a reminder missed during downtime still goes the next hour, and
the key means it goes **once**. A renewed vaccination changes `NextDueOn`, so the old request fails the
guard (§8.7) and a new due date gets its own reminder. A transferred cat changes the owner, so the old
(now-suppressed) request's key no longer matches the new owner's, and the new owner gets their own
reminder for the same due date. **Overdue items get no reminder** (OQ-5).

### 8.13 Ports

`src/Domain/CatEcommerce.Domain/DomainServices/` (flat), implementations in Infrastructure unless stated.

| Port | Implementation |
|---|---|
| `INotificationOutbox` | `Notifications/EfNotificationOutbox` — adds to the scoped context (§8.4) |
| `INotificationTemplateCatalogue` | `Notifications/StaticNotificationTemplateCatalogue` (Singleton) |
| `INotificationRecipientDirectory` | `Notifications/AccountRecipientDirectory` (Scoped, `AsNoTracking`) |
| `INotificationSubjectGuard` | `Notifications/Guards/{CatVaccinationDueGuard,CatDewormingDueGuard}` + `SubjectGuardResolver` |
| `INotificationLinkBuilder` | `Notifications/NotificationLinkBuilder` |
| `INotificationChannelSender` | §8.9, resolved by `NotificationSenderResolver` |
| `IQuietHoursCalendar` | `Notifications/QuietHoursCalendar` — loads the setting and the branch zone, calls the pure `QuietHours` (§8.14) |

`NotificationRenderer`, `NotificationRoutingPolicy`, `QuietHours` and `SmsSegments` are **pure** domain
services, like HO-ADM-001's `BranchCalendar`, so they are tested without a database.

### 8.14 Quiet hours — US-NTF-05 — ADR-059

`QuietHours.NextAllowedInstant(now, TimeRange quiet, TimeZoneInfo zone) → DateTimeOffset?` — null when
`now` is outside the window, else the window's end as an instant. The range may wrap midnight
(21:00–08:00, the default). Conversion only here, through HO-ADM-001's time model (ADR-045).

- The setting is `notifications.quietHours`, read for the notification's `BranchId`, else the main
  branch (branch override → global → default, HO-ADM-001 §8.7).
- **Applied to `Normal` external notifications**, at creation (so the log shows *"Held for quiet hours
  until 08:00"*) **and re-checked before each attempt** (a retry can fall inside the window).
- **Never applied** to `Urgent` templates (US-NTF-05 AC2) or to inbox copies.
- A hold that would pass `NotAfter` expires the notification instead of sending late.

### 8.15 Exceptions

Reuse `DomainRuleViolationException`. No new exception type besides the programming-error
`InvalidOperationException`s named above.

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. HO-ADM-001 owns ADR-042…054; **this handover owns ADR-055…066**
(README ADR table).

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-055** | **`Notification` bounded context**, namespace segment `Notification`, schema `notify`, owning the three roots of §8.1. Consent and the preferred channel stay in `Account` | Not in `.ai/bounded-contexts.md` (OQ-2); the preference split looks like a mistake unless recorded |
| **ADR-056** | **Two-stage outbox.** Producers enlist a `NotificationRequestAggregate` in their own unit of work (F23); the Notification context expands it into per-channel `NotificationAggregate`s, which are the outbox item, the log and the inbox item. **Expansion and fallback commit several roots in one `SaveChangesAsync`** — a recorded exception to rule 1, atomic by construction | The architecture doc shows one `outbox_message` table; this is a deliberate refinement that keeps routing out of producers |
| **ADR-057** | **Producer rules**: enqueue in the command handler before its save; never from a domain event handler; timed producers use `RequestNotificationCommand` with a deduplication key; no business decisions in variables | F24 — an event-handler enqueue splits intent from change across commits |
| **ADR-058** | **Routing policy** R1–R9: preferred channel (on the Account preferences aggregate, defaulting to the registration identifier), transactional ignores marketing consent, marketing needs per-channel consent, always-on inbox copy for customers except secrets, fallback to the other external channel, no push in v1, merged → survivor | FR-NTF-1 and FR-NTF-4 in one pure function the gates can read |
| **ADR-059** | **Quiet hours** apply to `Normal` external notifications, at creation and before each attempt, in the branch's zone; `Urgent` and inbox copies are never held; a hold past `NotAfter` expires | FR-NTF-5; "held" and "sent late" are different failures |
| **ADR-060** | **Delivery**: one `BackgroundService` (`NotificationWorker`) in the Api host polls every 5 s; per item a fresh scope and one command; claim-by-lease on the aggregate (committed before the provider call, `xmin` prevents double claims); backoff 1/5/15/60/240 min, 6 attempts; **at-least-once**, notification id as the idempotency reference; `Sent` = accepted by the provider | The only asynchronous element in the solution; its guarantees must be stated, not inferred |
| **ADR-061** | **Provider-neutral sending port**; Development and Testing senders refuse to construct elsewhere; `Unconfigured` is the default outside Development and logs a startup warning; the six-point adapter contract (§8.9) | Owner instruction: the provider choice is open. The default must fail safe and loud |
| **ADR-062** | **Templates**: code-defined catalogue, data-defined wording; `{{name}}` placeholders validated against the definition; email bodies in `LimitedMarkup` rendered server-side with every text node encoded; other channels plain text; **values substituted after parsing**; no templating library | FR-NTF-2 plus the injection risk of user-supplied values (cat names) in messages |
| **ADR-063** | **Secret-bearing messages**: no inbox copy, no resend, redacted on terminal state or at `NotAfter`, excluded from audit capture, never logged outside Development. Residual risk: plaintext for ≤ 15 min | A reset link is a credential; HO-ACC-001 stores its hash only. The window must be a decision, not a discovery |
| **ADR-064** | **Unsubscribe** by a random per-notification token (hash stored, no expiry), public page plus RFC 8058 one-click; revokes marketing for that channel through the Account aggregate with `Source = "unsubscribe-link"` | NFR-LEG-4; one-click is expected by major mail providers |
| **ADR-065** | **Care-reminder sweep** — a timed producer in the same worker, hourly, window-based and deduplicated; validity re-checked at send time by subject guards instead of event handlers | The second asynchronous element; the guard choice avoids event handlers that would touch many rows |
| **ADR-066** | **Audit**: `notify.notification_request` and `notify.notification` are excluded from `AuditChangeCaptureInterceptor` (the log is the record, and it holds secrets); template edits are audited normally; resends write a `BusinessEvent`; unsubscribes are audited through the preferences change | Amends HO-ACC-001 §11.9 step 7 |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| **OQ-1** | **Which SMS and email providers?** | Owner | **None** — `Unconfigured` outside Development (ADR-061). Password reset and reminders reach only the inbox until chosen |
| OQ-2 | `.ai/bounded-contexts.md` needs the `Notification` context | Architect | ADR-055 records it |
| OQ-3 | Encrypt secret-bearing bodies and variables at rest instead of the 15-minute plaintext window? | SECURITY gate + owner | No — needs a key-ring decision and a package (ADR-063) |
| OQ-4 | Who may view the log and resend — "support agent" is not an Appendix D role | Owner | `Reception` and `Administrator` (§12.3) |
| OQ-5 | Remind about overdue items, and how often? | Owner + clinical lead | No overdue reminders |
| OQ-6 | Group several reminders for one cat into one message? | Owner | No — one message per item |
| OQ-7 | Per-customer quiet hours? | Owner | No — business-level setting, per branch (FR-NTF-5 "configured quiet hours") |
| OQ-8 | Can a customer delete inbox items? | Owner | No — mark read only |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `NotificationUrgency` | `Normal`, `Urgent` |
| `NotificationRecipientKind` | `Customer`, `Staff` |
| `NotificationRequestStatus` | `Pending`, `Expanded`, `Suppressed`, `Expired` |
| `NotificationSuppressionReason` | `RecipientInactive`, `NoPermittedChannel`, `SubjectNoLongerApplies`, `MissingVariable`, `UnknownTemplate` |
| `NotificationStatus` | `Queued`, `Sending`, `Sent`, `RetryPending`, `Undeliverable`, `Expired`, `Cancelled`, `InboxDelivered` |
| `NotificationCloseReason` | `SubjectNoLongerApplies`, `RecipientInactive`, `RecipientOptedOut`, `NotAfterPassed` |
| `SendOutcomeKind` | `Accepted`, `TransientFailure`, `PermanentFailure` |
| `NotificationSubjectKind` | `CatVaccinationDue`, `CatDewormingDue` |
| `TemplateVariableKind` | `Text`, `Url`, `Date` |
| `SmsEncoding` | `Gsm7`, `Ucs2` |

`NotificationChannel` and `NotificationCategory` are HO-ACC-001's — reused, never duplicated.

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`

| Key | Group | Type | Default | Bounds | Branch override | Consumer |
|---|---|---|---|---|---|---|
| `notifications.quietHours` | Notifications | TimeRange | 21:00–08:00 | any | ✅ | **§8.14 — already in the registry; this slice is its consumer** |
| `notifications.vaccinationReminderLeadDays` | Notifications | Integer | 14 | 1–90 | ❌ | §8.12 |
| `notifications.dewormingReminderLeadDays` | Notifications | Integer | 7 | 1–90 | ❌ | §8.12 |

Two registry entries, no migration (HO-ADM-001 §10.2: values are rows).

### 10.3 Other constants

`NotificationTemplateCatalogue` · `StandardTemplateVariables` · `NotificationChannelRules`
(`DeliverableChannels = [Sms, Email, InApp]`) · `NotificationRetryRules` (`MaxAttempts = 6`, `Backoff`)
· `NotificationRequestRules` (`MaxVariables = 30`, `MaxVariableLength = 2000`, `MaxDeduplicationKeyLength
= 200`) · `NotificationResendRules.MaxResendsPerNotification = 3` · `NotificationLogRules`
(`MaxRangeDays = 93`) · `DestinationMask` · `SystemActors.NotificationWorker` (a fixed Guid used as the
worker's `UserContext.UserId`) · `NotificationMessages`.

**Actions** — appended to the one `ActionRoles` table (§12.3).

### 10.4 Violation codes — append to `BusinessViolationCodes`

`TemplateNotInCatalogue` · `TemplateChannelMismatch` · `MalformedTemplatePlaceholder` ·
`UnknownTemplatePlaceholder` · `RequiredTemplatePlaceholderMissing` · `InvalidTemplateText` ·
`InvalidTemplateLength` · `InvalidTemplateLink` · `TemplateVersionConflict` · `InvalidNotificationRequest`
· `NotificationNotFound` · `NotificationNotDue` · `NotificationCannotBeResent` · `ResendLimitReached` ·
`ResendChannelNotAvailable` · `InvalidUnsubscribeLink` · `PreferredChannelHasNoDestination` ·
`InvalidPreferredChannel`.

`NotificationCannotBeResent` carries a **reason** in its message: *secret-bearing*, *in-app copy*,
*recipient inactive*, *recipient has since opted out*, or *still being delivered*.

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Notification/`

Namespace `CatEcommerce.Application.Commands.Notification`. Data only; ids minted in the controller or
the worker; `ValidateCommand = true` everywhere.

| # | Command | Sent by | Phase |
|---|---|---|---|
| C1 | `SaveNotificationTemplateCommand` (`Key`, `ExpectedContentVersion`, `Contents[]`: `Channel`, `Subject?`, `Title?`, `Body`, `LinkPath?`) | Admin | A |
| C2 | `ResetNotificationTemplateCommand` (`Key`, `ExpectedContentVersion`) | Admin | A |
| C3 | `RequestNotificationCommand` (the `NotificationRequestDto` fields + `RequestId`) | Timed producers, tests | B |
| C4 | `ExpandNotificationRequestCommand` (`RequestId`) | Worker | B |
| C5 | `DeliverNotificationCommand` (`NotificationId`) | Worker | B |
| C6 | `RedactNotificationSecretsCommand` (`RequestId?`, `NotificationId?` — exactly one) | Worker | B |
| C7 | `ResendNotificationCommand` (`NotificationId`, `NewNotificationId`, `Channel?` — null = same) | Staff | D |
| C8 | `MarkInboxItemReadCommand` (`NotificationId` — customer from `UserContext`) | Customer | D |
| C9 | `UnsubscribeFromMarketingCommand` (`Token`) | Public | E |

HO-ACC-001's `ChangeNotificationPreferencesCommand` gains **`PreferredChannel?`** (Phase B, §8.5).

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: key pattern, required fields, `Contents` non-empty and ≤ 4, lengths as upper bounds,
`Token` non-empty ≤ 100, `Channel` in `NotificationChannelRules.DeliverableChannels` minus `InApp` for
C7. **Placeholder and markup rules are `TemplateRules`, called by the aggregate** — they need the
definition.

### 11.3 Mappers and DTOs

`NotificationMapper` (static `ToDomainDto()` extensions, `UserContext`/`CorrelationId` passed through).
DTOs in `Domain/Models/` (flat): `NotificationTemplateContentDto`, `NotificationRequestDto`,
`NewNotificationDto`, `DeliveryAttemptDto`. `NotificationRequestDto` is **the producer contract** —
later handovers build it, nothing else:

```csharp
public sealed record NotificationRequestDto(
    Guid RequestId, string TemplateKey,
    NotificationRecipientKind RecipientKind, Guid RecipientId,
    IReadOnlyDictionary<string, string> Variables,
    NotificationChannel? ChannelHint = null,
    NotificationSubjectKind? SubjectKind = null, Guid? SubjectId = null,
    Guid? SubjectRef = null, DateOnly? SubjectDueOn = null,
    Guid? BranchId = null, string? DeduplicationKey = null, DateTimeOffset? NotAfter = null,
    Guid CorrelationId = default, UserContext? UserContext = null);
```

### 11.4 Command handlers — `CommandHandlers/Notification/`

The `RegisterCatCommandHandler` shape. The worker's handlers (C4–C6) are **idempotent**: a second run
on an item already past its step returns success and writes nothing.

| Handler | Steps |
|---|---|
| **C1 Save template** | Definition from the catalogue (`TemplateNotInCatalogue`); load by key; `ExpectedContentVersion` ≠ current (0 when absent) → `TemplateVersionConflict` (`409`); absent → `Customise` + `SaveAsync`, else `Edit` + `UpdateAsync` |
| **C2 Reset** | Absent → success; version check; `ResetToDefault` |
| **C3 Request** | Definition exists; variables' keys ⊆ the definition's; `DeduplicationKey` present and already used → **success, nothing written**; `Create` + `SaveAsync`; a `23505` on `ix_notification_request_dedup` → success (the race lost cleanly) |
| **C4 Expand** | Load; not `Pending` → success. `NotAfter` passed → `Expire`. Unknown template → `Suppress(UnknownTemplate)` + `Error` log. Resolve recipient (§8.5). Guard (§8.7) → `Suppress(SubjectNoLongerApplies)`. `NotificationRoutingPolicy.Plan` → suppressed? `Suppress`. Else: standard variables added; required variables present (else `Suppress(MissingVariable)`); render the primary and the inbox copy with the **effective** template; marketing → issue an unsubscribe token per notification; `Normal` → `IQuietHoursCalendar` may set `NotBefore`; **enlist** each `NotificationAggregate`; `MarkExpanded`; `UpdateAsync(request)` — one commit (ADR-056) |
| **C5 Deliver** | Load; terminal or not due → success. `NotAfter` passed → `Expire` (+ `Redact` if secret). Guard → `Cancel(SubjectNoLongerApplies)`. Recipient re-resolved: inactive → `Cancel(RecipientInactive)`; marketing and consent since revoked → `Cancel(RecipientOptedOut)`. `Normal` inside quiet hours → `Defer`. Otherwise `Claim` → `UpdateAsync` (a `409` here means another worker has it → return success). Resolve the sender for the channel; `SendAsync` under a `SendTimeoutSeconds` token; any exception from the sender → `TransientFailure` with the exception **type name** as the response (never its message — it may echo the destination). `RecordAttempt`. `Sent` and secret → `Redact`. `Undeliverable` → `NextFallback`; a channel → render from the request's variables (if redacted → no fallback), enlist the fallback (`FallbackFromId`), secret with no fallback → `Redact`. `UpdateAsync` — one commit |
| **C6 Redact** | Request: `ContainsSecret`, `NotAfter` passed → `RedactVariables`. Notification: `ContainsSecret` and terminal → `Redact`. Anything else → success, nothing written |
| **C7 Resend** | Load (`NotificationNotFound`). Refuse (`NotificationCannotBeResent`, with the reason) when: secret; `InApp`; status `Queued`/`Sending`/`RetryPending`; recipient inactive; marketing without current consent on the target channel. Count resends of the original (`ResendOfId` chain root) ≥ 3 → `ResendLimitReached`. Target channel = `Channel ?? original.Channel`, must be deliverable and the recipient must have a destination for it (`ResendChannelNotAvailable`). The destination is **re-resolved** (a corrected mobile number is used). New notification: same rendered content (re-rendered only when the channel differs), `ResendOfId` = the chain root, `ResentBy` = caller, quiet hours applied for `Normal`. Enlist a `BusinessEvent` audit entry (`Action = "NotificationResent"`, entity = the original, `AfterValue` = new id and channel) and `SaveAsync(new)` — one commit |
| **C8 Mark read** | Load; not `InApp` or `RecipientId ≠ caller` → `NotificationNotFound` (**`404`, never `403`**); `MarkRead` |
| **C9 Unsubscribe** | §8.11. `Source = "unsubscribe-link"`; `UserContext` = `SystemActors.NotificationWorker` with the customer id recorded in the audit reason, because the caller is anonymous |
| **HO-ACC-001 C4** *(amended)* | `RequestPasswordResetCommandHandler` calls `dispatcher.DispatchAsync(...)` **before** `SaveAsync(token)`, so the token and its notification request commit together. Nothing else in that handler changes |
| **HO-ACC-001 C8** *(amended)* | `ChangeNotificationPreferencesCommandHandler` applies `PreferredChannel` when present: `Sms`/`Email` only (`InvalidPreferredChannel`), and the account must hold that identifier (`PreferredChannelHasNoDestination`) |

**`NotificationPasswordResetTokenDispatcher : IPasswordResetTokenDispatcher`** (Infrastructure,
replaces HO-ACC-001's `DevelopmentPasswordResetTokenDispatcher` and
`UndeliveredPasswordResetTokenDispatcher`, which are **deleted**): `outbox.Enqueue` with
`account.password-reset`, recipient = the account (`AccountKind` → recipient kind), `ChannelHint` = the
channel C4 chose, `resetLink` from `PasswordResetLink(token)`, `expiresInMinutes`,
`NotAfter = expiresOn`. It returns a completed task; it does not save.

### 11.5 Domain events — `Events/Notification/`

Ids, keys, enums and booleans only — no bodies, destinations or variable values.

`NotificationTemplateCustomisedEvent` (`Key`, `ContentVersion`), `NotificationTemplateResetEvent`
(`Key`, `ContentVersion`). The request and notification roots raise **no** events (§8.4) — nothing
subscribes, and the worker reads state, not events. HO-ACC-001's `NotificationPreferencesChangedEvent`
is raised by the unsubscribe as by any preference change.

### 11.6 Domain event handlers — `DomainEventHandlers/Notification/`

| Handler | Handles | Does |
|---|---|---|
| `ReassignInboxOnCustomerMergedHandler` | HO-ACC-001 `CustomerMergedEvent` | For each `InApp` notification of the duplicate: set `RecipientId` to the survivor, one `UpdateAsync` each, **idempotent** (already the survivor → skip). The external log keeps the original recipient — it is history. Required by HO-ACC-001 ADR-039 |
| `AnonymiseNotificationsOnCustomerAnonymisedHandler` | HO-ACC-001 `CustomerAccountAnonymisedEvent` | For each of the customer's notifications: `Redact(Anonymisation)`, which **also nulls the destination**; for each request: `RedactVariables`. Idempotent. Pending ones are then suppressed by R1 |

Both run after the source commit and loop over rows, which is the accepted shape of HO-ACC-001's merge
contract (ADR-039). Neither enqueues anything (ADR-057).

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

Three `DbSet`s; one configuration per root in `Persistence/Configurations/`, schema `notify`, modelled
on HO-ACC-001 §12.1: `ValueGeneratedNever()`, the F15 lines, enumerations by `Value`, `Variables` as
`jsonb`, instants `timestamptz`, `DateOnly` → `date`.

| Index | Kind |
|---|---|
| `notification_template.key` | **UNIQUE** `ix_notification_template_key` |
| `notification_template_content (template_id, channel)` | **UNIQUE** |
| `notification_request.deduplication_key` | **UNIQUE, filtered** `WHERE deduplication_key IS NOT NULL` — `ix_notification_request_dedup` |
| `notification_request (status, requested_on)` | worker: pending work |
| `notification (status, not_before, next_attempt_on)` | worker: due work |
| `notification (recipient_id, channel, read_on, created_on)` | inbox list and unread count |
| `notification (recipient_id, created_on)`, `(destination, created_on)`, `(template_key, created_on)`, `(created_on)` | log filters |
| `notification.unsubscribe_token_hash` | **UNIQUE, filtered** `WHERE … IS NOT NULL` |
| `notification (request_id)`, `(resend_of_id)`, `(fallback_from_id)` | chain |

**Account amendment**: `account.customer_notification_preferences.preferred_channel` (`integer NULL`).

**Audit exclusion (ADR-066)**: `AuditChangeCaptureInterceptor` step 7 becomes *"Never audit
`AuditEntryAggregate`, `NotificationRequestAggregate` or `NotificationAggregate`"*, driven by an
`AuditCaptureExclusions` list in Infrastructure so the rule is data, not an `if`. U-X2 proves a reset
link never reaches `audit.audit_entry`.

**Migrations**, each read before commit: `AddNotificationTemplates` (A), `AddNotificationDelivery` (B —
requests, notifications, attempts, and the `preferred_channel` column), `AddNotificationLogIndexes`
(D). Phases C, E and F have no migration.

### 12.2 Infrastructure services and the worker

| Class | Notes |
|---|---|
| `Configuration/NotificationOptions` | Section `Notifications`: `WebBaseUrl`, `ApiBaseUrl`, `Channels:Sms:Sender`, `Channels:Email:Sender` (`Development` \| `Capturing` \| `Unconfigured`; default `Unconfigured`), `Email:FromAddress`, `Email:FromName`, `Sms:SenderId`, `Worker:Enabled` (default true; **false in `Testing`**), `Worker:PollIntervalSeconds` (5, 1–60), `Worker:BatchSize` (50), `Worker:LeaseSeconds` (120), `Worker:SendTimeoutSeconds` (10), `Worker:ReminderSweepIntervalMinutes` (60). `ValidateOnStart`: https base URLs outside Development; `Development`/`Capturing` senders refused outside their environments |
| `Notifications/EfNotificationOutbox` | §8.4 — adds to the scoped context; never saves |
| `Notifications/NotificationPasswordResetTokenDispatcher` | §11.4 |
| `Notifications/Senders/**` | §8.9 and `NotificationSenderResolver` |
| `Notifications/AccountRecipientDirectory`, `QuietHoursCalendar`, `NotificationLinkBuilder`, `StaticNotificationTemplateCatalogue`, `Guards/**` | §8.13 |
| `Notifications/NotificationWorkQueue` | Read-only due-work queries: pending requests; due `Queued`/`RetryPending`; `Sending` with an expired lease; secrets due for redaction. `AsNoTracking`, ordered oldest first, `BatchSize` each. **It returns ids; it never writes** |

**`NotificationWorkCycle`** (Api, `Background/`, Scoped) — one pass:
`RunDeliveryPassAsync()` = for each id from the work queue, a **fresh scope**, dispatch C4 / C5 / C6
with `SystemActors.NotificationWorker`'s `UserContext` and a new correlation id; an exception is logged
at `Error` with the id and swallowed so one bad row cannot stop the queue. `RunReminderSweepAsync()` =
§8.12.

**`NotificationWorker : BackgroundService`** (Api, `Background/`) — when `Worker:Enabled`, loops every
`PollIntervalSeconds` calling `RunDeliveryPassAsync`, and — once 30 s after start, then every
`ReminderSweepIntervalMinutes` — calling `RunReminderSweepAsync`, each in a new scope; honours the stopping token; never throws out of
`ExecuteAsync`. **At start**, if either channel's sender is `Unconfigured` outside Development, log one
`Warning`: *"No SMS provider is configured: SMS notifications, including password reset, will not be
delivered. See HO-NTF-001 OQ-1."* (and the email equivalent).

**Tests do not run the loop.** `Worker:Enabled = false` in `Testing`; tests resolve
`NotificationWorkCycle` and call its passes, with `FakeTimeProvider` advancing time.

### 12.3 Authorization

Appended to the one `ActionRoles` table:

| Action | Staff roles | Endpoints |
|---|---|---|
| `NotificationTemplates.View` | Administrator, ShopManager | template list, detail, preview |
| `NotificationTemplates.Manage` | Administrator | save, reset |
| `NotificationLog.View` | Reception, Administrator | log list, detail, summary (OQ-4) |
| `NotificationLog.Resend` | Reception, Administrator | resend |

Customer endpoints carry `[RequireCustomerSession]`; the unsubscribe endpoints `[PublicEndpoint]` with
the new `unsubscribe` rate-limit policy (20 per minute per IP, HO-ACC-001 ADR-030's shape). HO-ADM-001's
`EndpointAccessConventionTests` covers every new action with no change.

### 12.4 Api — the contract HO-NTF-002 consumes

JSON camelCase; instants ISO-8601 with offset; destinations **masked** in lists
(`DestinationMask`: `+88017•••••678`, `r•••@example.com`) and full only in the staff detail view.

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/admin/notification-templates` | `NotificationTemplates.View` | `[{ key, displayName, description, category, urgency, channels[], containsSecret, isCustomised, contentVersion, lastEditedOn? }]` |
| `GET` | `/api/admin/notification-templates/{key}` | `NotificationTemplates.View` | the list fields plus `variables[]` (`name`, `description`, `kind`, `sample`, `requiredOn[]`, `isStandard`), `contents[]` (effective), `defaults[]`, `limits` per channel |
| `PUT` | `/api/admin/notification-templates/{key}` | `NotificationTemplates.Manage` | C1 → `204`; `409` on version conflict |
| `POST` | `/api/admin/notification-templates/{key}/reset` | `NotificationTemplates.Manage` | C2 → `204` |
| `POST` | `/api/admin/notification-templates/{key}/preview` | `NotificationTemplates.View` · `template-preview` limit | body: unsaved `contents[]`, optional `sampleValues{}` → `200 { channels: [{ channel, subject?, title?, textBody, htmlBody?, linkPath?, sms?: { encoding, characters, segments } }] }`. **Validation errors are the same `400`s as C1.** Writes nothing |
| `GET` | `/api/admin/notifications?from&to&recipientId&destination&channel&templateKey&status&pageNumber&pageSize` | `NotificationLog.View` | paged; `from`/`to` **required**, ≤ 93 days; `destination` normalised (mobile via `MobileNumber`, email lower-cased) and matched exactly. Item: `id`, `createdOn`, `templateKey`, `templateName`, `category`, `channel`, `destinationMasked`, `recipientKind`, `recipientId`, `recipientName`, `status`, `attemptCount`, `sentOn?`, `lastProviderResponse?` (≤ 200), `deferredUntil?`, `isResend`, `isFallback` |
| `GET` | `/api/admin/notifications/{id}` | `NotificationLog.View` | the item plus `destination` (full), `subject?`, `title?`, `body` (**`"[redacted]"` when redacted**, with `redactedReason`), `attempts[]` (`attemptedOn`, `outcome`, `providerName`, `providerMessageId?`, `providerResponse?`), `chain` (`requestId`, `fallbackFromId?`, `fallbacks[]`, `resendOfId?`, `resends[]`), `canResend`, `cannotResendReason?`, `resendableChannels[]` |
| `POST` | `/api/admin/notifications/{id}/resend` | `NotificationLog.Resend` | body `{ channel? }` → C7 → `201 { notificationId, status, notBefore? }` |
| `GET` | `/api/admin/notifications/summary` | `NotificationLog.View` | `{ queued, retryPending, sending, oldestQueuedOn?, last24h: { sent, undeliverable, expired, cancelled, suppressed } , channelsConfigured: { sms, email } }` — `channelsConfigured` is `false` for an `Unconfigured` sender, so the screen can say so |
| `GET` | `/api/customers/me/inbox?unreadOnly&pageNumber&pageSize` | customer | paged `{ items: [{ id, title, body, linkPath?, category, createdOn, readOn? }], … }`, newest first |
| `GET` | `/api/customers/me/inbox/unread-count` | customer | `{ count }` |
| `POST` | `/api/customers/me/inbox/{id}/read` | customer | C8 → `204`; another customer's id → `404` |
| `POST` | `/api/notifications/unsubscribe` | public · `unsubscribe` | body `{ token }` → C9 → `200 { channel, wasSubscribed }`; `400 InvalidUnsubscribeLink` |
| `POST` | `/api/notifications/unsubscribe/one-click?token=` | public · `unsubscribe` | RFC 8058 (`List-Unsubscribe=One-Click` form body) → C9 → `200`, empty body. **Request logging must not record the query string for this route** |
| `GET` / `PUT` | `/api/customers/me/notification-preferences` | customer *(HO-ACC-001, amended)* | gains `preferredChannel`, `effectivePreferredChannel` and `availablePreferredChannels[]` (the channels the account has an identifier for) on `GET`; optional `preferredChannel` on `PUT` |

The two unsubscribe endpoints carry no session and are exempt from the CSRF check by HO-ACC-001 R7's
existing rule (R7 applies only when a session cookie is presented). A signed-in customer's browser
posts through the portal client, which sends the token anyway.

Every write uses HO-ACC-001's `400` (`ValidationResponse`) / `401` / `403` / `409` shapes (R1–R8).

### 12.5 Filters

`UniqueConstraintExceptionFilter` (HO-ACC-001) — map `ix_notification_template_key` (a first-save race)
→ `409`. `ix_notification_request_dedup` is handled **inside** C3 and never reaches the filter. The
unsubscribe-token index cannot collide in practice (256 bits); an unmapped `23505` still rethrows.

### 12.6 Read side — `src/Read/`

Queries in `Queries/Notification/`, handlers in `QueryHandlers/Notification/`, ViewModels flat.
Explicit projections, `AsNoTracking()`.

- `GetNotificationTemplatesQuery`, `GetNotificationTemplateQuery(key)` — catalogue definitions merged
  with stored customisations.
- `PreviewNotificationTemplateQuery(key, contents, sampleValues)` — calls `TemplateRules.Validate` and
  `NotificationRenderer` with the samples; failures return the C1 `ValidationResult` shape. **Pure — no
  repository.**
- `GetNotificationLogQuery`, `GetNotificationDetailQuery`, `GetNotificationSummaryQuery` — recipient
  names by left joins to `account.*`; `cannotResendReason` computed by the **same** function C7 uses
  (`NotificationResendRules.Evaluate`), so the screen and the command cannot disagree.
- `GetInboxQuery(customerId, …)`, `GetInboxUnreadCountQuery(customerId)` — `Channel = InApp`,
  `RecipientId = caller`.
- `GetDueCareRemindersQuery(today, leads)` — §8.12; alive cats, active owners, active vaccines; the
  latest-record rule through `CatVaccinationStatusCalculator`.
- HO-ACC-001's Q-E2 `GetNotificationPreferencesQuery` — adds the three preferred-channel fields.

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler and event handler in §11–§12.6;
the ports of §8.13 (Scoped, except the catalogue and `NotificationSenderResolver` — Singleton);
`NotificationOptions` with `ValidateOnStart`; `NotificationWorkCycle` (Scoped);
**`AddHostedService<NotificationWorker>()`** — it creates a scope per pass
(`.ai/di-registration-guide.md`); the `unsubscribe` and `template-preview` rate-limit policies; replace
the `IPasswordResetTokenDispatcher` registration with `NotificationPasswordResetTokenDispatcher`.
`ValidateScopes`/`ValidateOnBuild` stay on; I-A1 proves the registrations.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the ten enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{NotificationTemplateCatalogue,StandardTemplateVariables,NotificationChannelRules,NotificationRetryRules,NotificationRequestRules,NotificationResendRules,NotificationLogRules,SystemActors,NotificationMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs     ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                              ← the two keys in §10.2 only
src/Shared/CatEcommerce.Shared/Common/Helpers/{TemplateRules,DestinationMask}.cs
src/Domain/CatEcommerce.Domain/Aggregates/Notification/**
src/Domain/CatEcommerce.Domain/Aggregates/Account/CustomerNotificationPreferencesAggregate.cs      ← PreferredChannel only (§8.5)
src/Domain/CatEcommerce.Domain/Entities/{NotificationTemplateContent,DeliveryAttempt}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{INotificationOutbox,INotificationTemplateCatalogue,INotificationRecipientDirectory,INotificationSubjectGuard,INotificationLinkBuilder,INotificationChannelSender,IQuietHoursCalendar,NotificationRenderer,NotificationRoutingPolicy,QuietHours,SmsSegments}.cs
src/Domain/CatEcommerce.Domain/Models/**                                                          ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Notification/**
src/Application/CatEcommerce.Application/Commands/Notification/**
src/Application/CatEcommerce.Application/CommandHandlers/Notification/**
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Notification/**
src/Application/CatEcommerce.Application/DataMappers/NotificationMapper.cs
src/Application/CatEcommerce.Application/Commands/Account/ChangeNotificationPreferencesCommand.cs              ← PreferredChannel only
src/Application/CatEcommerce.Application/CommandHandlers/Account/ChangeNotificationPreferencesCommandHandler.cs ← PreferredChannel only
src/Application/CatEcommerce.Application/CommandHandlers/Account/RequestPasswordResetCommandHandler.cs         ← dispatch before save only
src/Read/CatEcommerce.Read/Queries/Notification/**
src/Read/CatEcommerce.Read/QueryHandlers/Notification/**
src/Read/CatEcommerce.Read/QueryHandlers/Account/GetNotificationPreferencesQueryHandler.cs        ← preferred-channel fields only
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4, NotificationPreferencesViewModel ← three fields}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs              ← DbSets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{one per new root}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/CustomerNotificationPreferencesConfiguration.cs ← the column only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                          ← the three in §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Auditing/{AuditChangeCaptureInterceptor,AuditCaptureExclusions}.cs ← exclusion only (ADR-066)
src/Infrastructure/CatEcommerce.Infrastructure/Configuration/NotificationOptions.cs
src/Infrastructure/CatEcommerce.Infrastructure/Configuration/AccountOptions.cs                    ← remove WebBaseUrl only
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/**                                  ← HO-ACC-001's two dispatchers deleted, §12.2 added
src/Api/CatEcommerce.Api/Controllers/{AdminNotificationTemplates,AdminNotifications,CustomerInbox,NotificationUnsubscribe}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Notification/**
src/Api/CatEcommerce.Api/Background/{NotificationWorker,NotificationWorkCycle,CareReminderSweep}.cs
src/Api/CatEcommerce.Api/appsettings.json                                                         ← the Notifications section, senders Unconfigured
src/Api/CatEcommerce.Api/Program.cs                                                               ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-NTF-001.md
docs/gates/HO-NTF-001-*.md
.ai/decisions/adr.md                                                                              ← ADR-055…066, append only
```

`appsettings.Development.json` is git-ignored (HO-ACC-001 §17): set `Notifications:WebBaseUrl`,
`ApiBaseUrl` and both senders to `Development` there, and say so in the implementation report.

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-2 is a separate task) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs |
| `web/**` | HO-NTF-002 |
| Every other file of HO-ACC-001, HO-ADM-001 and HO-CAT-001, beyond the lines listed in §13 | Their areas. In particular **no Cat file changes** — reminders read Cat data through a query |
| `audit.audit_entry` columns and triggers | HO-ACC-001's |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |
| A provider SDK or HTTP client for a real SMS or email service | OQ-1 is open (§20.3) |
| Any other bounded context | Appointments, Boarding, Orders … are producers later |

---

## 15. Unit test cases

`tests/CatEcommerce.Domain.Tests` / `Application.Tests`; Infrastructure/Api-class rows in
`IntegrationTests` as plain unit tests.

| ID | Case | Expected |
|---|---|---|
| **A — templates and rendering** | | |
| U-A1 | `TemplateRules`: `{{catname}}` in a `care.vaccination-due` body | `UnknownTemplatePlaceholder`; the message contains `{{catname}}` and the available list; `propertyName` points at that channel's body — **US-NTF-02 AC2** |
| U-A2 | `{{ catName }}`, `{catName}`, `{{catName`, `}}` alone | `MalformedTemplatePlaceholder` each |
| U-A3 | Reset template SMS body without `{{resetLink}}` | `RequiredTemplatePlaceholderMissing` |
| U-A4 | A marketing definition without `{{unsubscribeLink}}` on one channel; a transactional one **with** it | `RequiredTemplatePlaceholderMissing`; `UnknownTemplatePlaceholder` |
| U-A5 | Channel set differs from the definition (missing email; extra push) | `TemplateChannelMismatch` |
| U-A6 | Email body with `<b>`, `javascript:` link, `[x]({{catName}})` (a `Text` variable as a link target) | `InvalidContentMarkup` / `InvalidTemplateLink` |
| U-A7 | SMS body with `<`, or with `[x](https://a)` | `InvalidTemplateText` |
| U-A8 | Every shipped catalogue definition's defaults | Pass `TemplateRules.Validate`; keys unique and match the pattern |
| U-A9 | Render with `catName = "**Mitu**](https://evil){{resetLink}}"` | Email HTML contains the literal characters, encoded; no extra `<a>`, no `<strong>`, no expansion |
| U-A10 | Render email with `catName = "<script>"` | `&lt;script&gt;` in `HtmlBody`; literal in `TextBody` |
| U-A11 | `dueOn = "2026-10-12"` | `12 Oct 2026` |
| U-A12 | `SmsSegments`: 160 GSM-7 chars; 161; 70 chars with one `৳`; 71 | 1 / 2 / 1 UCS-2 / 2 UCS-2 |
| U-A13 | Aggregate `Edit` twice, then `ResetToDefault` | Versions 1, 2, 3; contents cleared |
| **B — routing and delivery** | | |
| U-B1 | `Plan` transactional, preferred SMS, both identifiers | Primary SMS; inbox copy — **US-NTF-01 AC1** |
| U-B2 | `Plan` transactional, preferred SMS, **no mobile** | Primary email |
| U-B3 | `Plan` transactional, customer opted out of all marketing | Primary on the preferred channel — **US-NTF-04 AC2** |
| U-B4 | `Plan` marketing, consent on email only, preferred SMS | Primary email; inbox copy only if in-app consent |
| U-B5 | `Plan` marketing, no consent anywhere | Suppressed `NoPermittedChannel` — **US-NTF-04 AC1** |
| U-B6 | `Plan` secret template | No inbox copy |
| U-B7 | `Plan` with `channelHint = Email` on a transactional template; on a marketing one | Email first; hint ignored |
| U-B8 | `Plan` staff recipient | No inbox copy; email before SMS |
| U-B9 | `NextFallback` after SMS; after SMS and email | Email; null |
| U-B10 | `Push` never selected, whatever the preference | As stated |
| U-B11 | `RecordAttempt`: transient ×5 then transient; permanent first time | `RetryPending` with 1/5/15/60/240-minute gaps, then `Undeliverable`; `Undeliverable` at once |
| U-B12 | `Claim` when not due; when leased and lease live; when lease expired | `NotificationNotDue` / `NotificationNotDue` / claimed |
| U-B13 | `Redact` | Body, subject, title, HTML, link redacted; destination kept, except for `Anonymisation` |
| U-B14 | Directory: merged customer (two hops); anonymised | Resolves to the survivor; `IsActive = false` |
| U-B15 | `DevelopmentChannelSender` / `CapturingChannelSender` constructed in `Production` | Throws |
| U-B16 | `NotificationOptions` in `Production` with `http://` base URL, or a `Development` sender | Start-up validation fails |
| **C — quiet hours** | | |
| U-C1 | `QuietHours` 21:00–08:00 at 20:59, 21:00, 02:00, 08:00 (Asia/Dhaka) | null / 08:00 next day / 08:00 same day / null |
| U-C2 | Non-wrapping range 13:00–14:00 | Correct window |
| U-C3 | `Europe/London` branch across the DST change | Window end correct in UTC |
| U-C4 | Hold that passes `NotAfter` | Expires rather than defers |
| **D — log and resend** | | |
| U-D1 | `NotificationResendRules.Evaluate` for secret, in-app, queued, inactive, opted-out marketing, 3 prior resends, sent SMS | Each refusal reason; `ResendLimitReached`; allowed |
| U-D2 | `DestinationMask` | `+88017•••••678`, `r•••@example.com`, a one-letter local part masked safely |
| **E — unsubscribe** | | |
| U-E1 | Token issuance | 32 random bytes, base64url, only the SHA-256 stored |
| **F — reminders** | | |
| U-F1 | Guard: alive + owner + same due date; deceased; transferred; renewed (`NextDueOn` changed) | Valid / three `SubjectNoLongerApplies` — **US-NTF-03 AC2** |
| U-F2 | Reminder window: due in 14 days with lead 14; 15; due today; overdue by 1 | In / out / in / out |
| U-F3 | Deduplication key format | `care:vaccination:{catId:N}:{vaccineId:N}:{ownerId:N}:{yyyyMMdd}` (owner id added by (52), ACCEPTANCE A-5) |
| **X — cross-cutting** | | |
| U-X1 | Reflection over `Events/Notification/**` | No property named like body, destination, variable, token |
| U-X2 | `AuditCaptureExclusions` | Contains both notify roots and the audit root; nothing else |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1 rules 1–8). **Senders are
`Capturing`; the worker loop is off; tests call `NotificationWorkCycle` passes and advance
`FakeTimeProvider`.**

### 16.1 Additional shared-database rules

1. Deduplication keys, template edits and destinations are minted per test (a fresh customer each).
2. A test that customises a shipped template restores it with `POST …/reset` in `finally`.
3. Assertions on the worker filter by the test's own request or recipient ids — other tests' rows are
   in the same queue.

| ID | Case | Expected |
|---|---|---|
| I-A1 | Host starts, scope validation on | Every registration resolves; the hosted worker is registered but idle in `Testing` |
| I-A2 | Admin edits `care.vaccination-due` with a valid body; `GET` it | `204`; `isCustomised`, version 1 |
| I-A3 | Save with `{{catname}}` | `400 UnknownTemplatePlaceholder`, message names `{{catname}}` — **US-NTF-02 AC2** |
| I-A4 | Preview unsaved content with samples | `200` with rendered SMS (segments), email (text + HTML) and in-app; **no row written** — **US-NTF-02 AC1** |
| I-A5 | Two saves with the same `expectedContentVersion` | One `204`, one `409` |
| I-A6 | ShopManager previews; ShopManager saves; nurse views | `200`; `403`; `403` |
| I-A7 | Any template save | `DataChange` audit row with before/after |
| I-B1 | **Reset request by mobile** → run the delivery pass | Request `Expanded`; one SMS notification `Sent`; the capturing sender got a body containing `/reset-password#token=`; **no inbox copy**; the notification body is `[redacted]` afterwards |
| I-B2 | Reset request where the account save fails (inject a failure after enqueue) | **Neither** the token nor the request exists — atomicity (F23) |
| I-B3 | Search every log and `audit.audit_entry` after I-B1 | The token and the link appear **nowhere** — ADR-063, ADR-066 |
| I-B4 | Customer with preferred **email**; a care reminder request | Email notification + inbox copy — **US-NTF-01 AC1** |
| I-B5 | SMS destination set to fail **permanently** | SMS `Undeliverable` with the provider response; an email fallback with `fallbackFromId`; email `Sent` — **US-NTF-01 AC2** |
| I-B6 | Transient ×2 then accept | Two `RetryPending` gaps (1, 5 min via `FakeTimeProvider`); then `Sent`; three attempts recorded |
| I-B7 | Both channels permanently failing | Both `Undeliverable`; inbox copy still `InboxDelivered`; log shows the chain |
| I-B8 | Customer opted out of all marketing; a transactional template | Delivered — **US-NTF-04 AC2** |
| I-B9 | Test marketing definition (added through the catalogue port by the factory); consent on email only | Email only, containing an unsubscribe link and `List-Unsubscribe` headers — **US-NTF-04 AC1** |
| I-B10 | Same, no consent anywhere | Request `Suppressed NoPermittedChannel`; nothing sent |
| I-B11 | Two delivery passes in parallel over the same due notification | Exactly one send |
| I-B12 | A notification left `Sending` with an expired lease | Re-claimed and sent (the duplicate-risk case, documented) |
| I-B13 | `RequestNotificationCommand` twice with one deduplication key, concurrently | One request |
| I-B14 | Anonymised customer with a pending request | `Suppressed RecipientInactive` |
| I-B15 | `PUT` preferences `preferredChannel: "Email"` for a mobile-only walk-in | `400 PreferredChannelHasNoDestination` |
| I-B16 | Production-like host with `Unconfigured` senders (factory override) | Start-up warning logged; a reset ends `Undeliverable` with *"No SMS provider is configured"*; no token in the log |
| I-C1 | `Normal` reminder created at 22:30 Dhaka | Notification `Queued`, `deferredUntil` 08:00; not sent at 23:00; sent after 08:00 — **US-NTF-05 AC1** |
| I-C2 | `Urgent` template (test definition) at 22:30 | Sent at once — **US-NTF-05 AC2** |
| I-C3 | Inbox copy at 22:30 | `InboxDelivered` at once |
| I-C4 | Branch override of quiet hours to 23:00–06:00 for the request's branch | That window applies |
| I-D1 | Log search by recipient, by destination (typed unnormalised), by template, by status, by range | Correct rows; destinations masked — **US-NTF-06 AC1** |
| I-D2 | Detail of I-B5's SMS | Full destination, attempts with provider responses, chain to the fallback |
| I-D3 | Resend an `Undeliverable` SMS on email | `201`; new notification with `resendOfId`, `resentBy`; a `NotificationResent` audit row — **US-NTF-06 AC2** |
| I-D4 | Resend the reset notification; an inbox copy; a fourth resend | `400 NotificationCannotBeResent` (secret); same (in-app); `400 ResendLimitReached` |
| I-D5 | Resend after the customer changed their mobile | Sent to the **new** number |
| I-D6 | Nurse opens the log | `403` |
| I-D7 | Log without a range; with 100 days | `400` each |
| I-D8 | Customer reads inbox, unread count, marks one read | Newest first; count drops by one |
| I-D9 | Customer marks another customer's inbox item | `404` |
| I-D10 | Merge the customer into a survivor | Inbox items now listed for the survivor; the SMS log still names the original |
| I-D11 | Anonymise a customer | Their notifications' bodies and destinations redacted; requests' variables redacted |
| I-E1 | `POST /api/notifications/unsubscribe` with I-B9's token | `200 { channel: "Email", wasSubscribed: true }`; email marketing consent revoked with `Source = "unsubscribe-link"`; a `DataChange` audit row |
| I-E2 | Same token again; an unknown token | `wasSubscribed: false`; `400 InvalidUnsubscribeLink` |
| I-E3 | One-click `POST` with form body, no cookies | `200`; consent revoked |
| I-E4 | 21 unsubscribe posts in a minute from one IP | `429` on the 21st |
| I-E5 | A pending marketing email after unsubscribing | `Cancelled RecipientOptedOut` at the next pass |
| I-F1 | Cat with a rabies vaccination due in 14 days; run the reminder sweep | One `care.vaccination-due` request; after the delivery pass, a message containing the cat's name, the vaccine, the date and a link to `/portal/cats/{catId}` — **US-NTF-03 AC1** |
| I-F2 | Run the sweep twice | Still one request |
| I-F3 | Deworming due in 7 days (`Combined` treatment) | One `care.deworming-due` request |
| I-F4 | Mark the cat deceased after the request, before delivery | Notification `Cancelled SubjectNoLongerApplies`; nothing sent — **US-NTF-03 AC2** |
| I-F5 | Transfer the cat to another owner before delivery | Cancelled; the next sweep requests one for the new owner |
| I-F6 | Record a new vaccination (due date moves a year) before delivery | Cancelled; no new reminder until the new window |
| I-F7 | Deceased cat at sweep time | No request |
| I-F8 | Change `notifications.vaccinationReminderLeadDays` to 30 | The sweep picks up an item due in 25 days |
| I-X1 | Log capture across all cases at `Information` and above | No body, no full destination, no token, no variable value |

---

## 17. Implementation order

| Phase | Stories | Delivers |
|---|---|---|
| **A** | US-NTF-02 | Enumerations, catalogue, `TemplateRules`, `NotificationRenderer`, `SmsSegments`, template aggregate, C1–C2, preview query, template endpoints; migration `AddNotificationTemplates` |
| **B** | US-NTF-01, US-NTF-04 (AC2) | Request and notification roots, `INotificationOutbox`, directory, routing policy, senders, worker and work cycle, C3–C6, the preferred-channel amendment, the reset dispatcher and C4 reorder, audit exclusion; migration `AddNotificationDelivery` |
| **C** | US-NTF-05 | `QuietHours`, `IQuietHoursCalendar`, defer at creation and before attempts |
| **D** | US-NTF-06 | Log, detail, summary, resend (C7), inbox (C8), merge and anonymisation handlers; migration `AddNotificationLogIndexes` |
| **E** | US-NTF-04 (AC1) | Unsubscribe tokens, headers, C9, public endpoints, rate limit |
| **F** | US-NTF-03 | Subject guards, `GetDueCareRemindersQuery`, `CareReminderSweep`, the two settings |

Commands as HO-ACC-001 §17 (`restore` → `build` → unit tests → `migrations add` → read the migration →
`database update` → run → integration tests). In Development, start the Api and request a reset: the
link appears in the console under the `NOT SENT — development sender` banner.

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | NTF-01 AC1 | Each notification goes to the preferred channel, with the in-app copy always on | U-B1, U-B2, I-B4 |
| A2 | NTF-01 AC2 | A permanent channel failure uses the fallback channel and is logged | U-B9, U-B11, I-B5, I-B7 |
| A3 | NTF-02 AC1 | Templates are edited with placeholders and previewed with sample data before saving | U-A13, I-A2, I-A4 |
| A4 | NTF-02 AC2 | An invalid placeholder is refused with the variable named | U-A1, U-A2, I-A3 |
| A5 | NTF-03 AC1 | Vaccination and deworming reminders go ahead of the due date with a direct link — *the link targets the cat page until HO-APT-001* | U-F2, I-F1, I-F3, I-F8 |
| A6 | NTF-03 AC2 | No reminder for a deceased cat or an inactive record | U-F1, I-F4…I-F7 |
| A7 | NTF-04 AC1 | Marketing honours opt-out and every marketing message carries an unsubscribe mechanism | U-A4, U-B4, U-B5, I-B9, I-B10, I-E1…I-E5 |
| A8 | NTF-04 AC2 | Password-reset, payment, medical and incident messages are delivered regardless of marketing preferences — *proved for the templates that exist (reset, care); later templates inherit the rule by category* | U-B3, I-B1, I-B8 |
| A9 | NTF-05 AC1 | Non-urgent messages inside quiet hours are held and sent when the window opens | U-C1…U-C4, I-C1, I-C4 |
| A10 | NTF-05 AC2 | Urgent messages ignore quiet hours | I-C2 |
| A11 | NTF-06 AC1 | Channel, template, recipient, status and provider response are logged and searchable | I-D1, I-D2 |
| A12 | NTF-06 AC2 | A failed or missed message can be resent manually, and the resend is logged | U-D1, I-D3…I-D5 |
| A13 | FR-ACC-9 | Password-reset delivery exists end to end; the token commits with its request and never reaches a log or the audit table | I-B1, I-B2, I-B3, I-X1 |
| A14 | ADR-061 | With no provider configured the system fails safe and says so | U-B15, U-B16, I-B16 |
| A15 | — | Delivery is at-least-once and never double-claimed | U-B12, I-B11, I-B12, I-B13 |
| A16 | ADR-039 / ADR-041 | Merge moves the inbox; anonymisation removes personal data from the log | I-D10, I-D11 |

**Deferred, named in §4.2**: every other FR-NTF-3 message (their areas), FR-NTF-7's escalation
(HO-BRD-001), prescription refill reminders (HO-PHM-001), the booking-flow link (HO-APT-001).
**Named gap, §4.3**: no real provider (OQ-1).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Notification` context (ADR-055, OQ-2); the enlist-in-unit-of-work outbox (ADR-056, F23) and its rule-1 exception; **producer rules (ADR-057) — no enqueue from an event handler anywhere**; the hosted worker is the only asynchronous element and creates a scope per unit of work; pure domain services; the scoped edits to HO-ACC-001 and HO-ADM-001 files match §13 exactly |
| 2 | SECURITY | `security-reviewer` | **Secret handling end to end (ADR-063, I-B3, I-X1)**; audit exclusion (ADR-066); injection through variables (U-A9, U-A10); email HTML only from the renderer; Development/Testing senders refuse other environments; unsubscribe token entropy, hashing, rate limit, no disclosure; `404` for another customer's inbox item; the one-click route not logging its query string; OQ-3 recorded as a residual risk |
| 3 | CODE_REVIEW | `code-reviewer` | Idempotency of C3–C6 and both event handlers; the worker never throws out of `ExecuteAsync`; no provider call inside a database transaction; time only in `QuietHours` |
| 4 | TEST | `test-engineer` | `TC-HO-NTF-001.md` from §15/§16 first; §16.1's queue-isolation rule; `FakeTimeProvider` for every retry and quiet-hours case |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A5's link and A8's scope recorded as stated; OQ-1 recorded as a gap, not a pass |

---

## 20. BLOCKED conditions — stop and report

**20.1 HO-ACC-001 or HO-ADM-001 is not merged.** This slice extends the preferences aggregate, the
reset dispatcher, the audit interceptor, `ActionRoles`, the settings registry and `LimitedMarkup`. Do
not stub them.

**20.2 A producer seems to need to enqueue from an event handler.** Stop. Enqueue in the command
handler that raises the event, or raise it as a design question — ADR-057.

**20.3 Do not add an SMS or email provider, SDK or HTTP client.** OQ-1 is open by owner instruction.
If a provider is chosen mid-build, it arrives as an Amended row with its own tests.

**20.4 A reset link or token appears in a log, an audit row, the inbox or a resend.** It is a
SECURITY failure, not a test to relax.

**20.5 Do not add a templating library or accept HTML templates** — ADR-062.

**20.6 Do not call a provider while a database transaction is open.** Claim, commit, send, record.

**20.7 Do not subscribe to `CatMarkedDeceasedEvent` or `CatOwnershipTransferredEvent`** to cancel
reminders — the send-time guard does it (F27, ADR-065).

**20.8 A requirement conflict is a stop, not an edit** — SRS and backlog are forbidden scope.
