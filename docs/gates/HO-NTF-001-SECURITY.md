GATE: SECURITY
HANDOVER: HO-NTF-001
COMMITS: pass 3 reviewed `ho/NTF-001` at head 5aad2d7, as a fresh copy of the tree at that head.
  - The delta since pass 2 (4efe96f) is ce1991c, 5cf5de4, 54faffb, 8fd5a37, e9da87f and 5aad2d7
    (plus the gate-record commits cc78961, ed06705 and 13381f2).
DATE: 2026-09-27
STATUS: PASS

HISTORY
- Pass 1 (head 40a3373): FAIL. 1 HIGH, 3 MEDIUM, 6 LOW.
- Pass 2 (head 4efe96f): FAIL, on one MEDIUM (a rendered field too long for its column blocks the queue).
- Pass 3 (this entry): PASS. Owner rule, 2026-09-27: only a BLOCKER or HIGH fails a gate. MEDIUM and LOW
  findings go to docs/backlog/NTF.md. This pass has no BLOCKER or HIGH, and no MEDIUM either.
  - The pass-2 MEDIUM is closed. Both parts of the pass-2 remedy were implemented.
  - The pass-2 `/\` link LOW is closed.
  - Two LOWs are left open (the PublicSite placeholder-URL residual and the unsubscribe plaintext, both
    carried unchanged). One new LOW is added.

HOW IT WAS CHECKED (pass 3)
- Scratch setup:
  - a copy of the 5aad2d7 tree in `scratchpad\ntf1-sec3`. `diff -rq` against the NTF-001 worktree's `src`
    showed no differences;
  - fresh DB `catecommerce_ntf3sec`, built with `dotnet dotnet-ef database update`. The squashed
    `20260927114728_AddNotificationsModule` applied cleanly and created `attempt_count` and `last_attempt_on`.
- Build: 0 warnings, 0 errors.
- Suites, all green on the fresh DB on the first run:

  | Suite | Result |
  |---|---|
  | Domain | 324/324 |
  | Application | 111/111 |
  | Infrastructure | 45/45 |
  | Integration | 450/450 |

- Probe P6c from pass 2 now ships as
  `NotificationOverflowEndpointTests.P6c_OverlongCustomisedInAppTitle_TruncatedNotStuck_QueueNeverBlocked_ResetStillExpanded`.
  Setup: the same admin title, a 100-character cat name, `BatchSize=3`, then a reset. Result: all reminders
  Expanded with a truncated title, and the reset Expanded. It passed in the run above. The fallback (C5)
  and cross-channel resend (C7) overflow shapes are covered by the tests in the same file, which also pass.

PASS-2 FINDINGS, RE-CHECKED
- [MEDIUM, CLOSED] A rendered overflow blocked the queue. Three changes close it:
  1. `RenderedMessageColumnLimits.Enforce` runs on the render path at all three places a rendered row is built:
     - C4 external and inbox;
     - C5 fallback;
     - C7 resend.
     Subject and Title are truncated at 150 and 100 characters, without splitting a surrogate pair. An
     overlong Body or LinkPath throws `NotificationRenderOverflowException`, which is an
     `InvalidOperationException` and so is inside each closed catch set. The request is then RenderFailed,
     the fallback is dropped, or the resend degrades to the original fields and returns 201.
  2. The limits match `NotificationConfiguration` (150/100/10000/500). `html_body` is `text`, so it has no
     limit.
  3. Persistence failures that are not overflows are counted by `RecordNotificationExpansionFailureCommand`,
     in a fresh scope. After `MaxExpansionAttempts` (5) the request is parked as
     Suppressed/PersistenceFailed. `GetPendingRequestIdsAsync` puts never-attempted requests first, so a
     failing row can no longer hold the head of the queue.
- [LOW, CLOSED] `/\evil.test` was accepted as a site-relative link. `TemplateRules.IsSiteRelativePath` now
  rejects any backslash, for both email link targets and the InApp `linkPath`.
- Render catch narrowing (ce1991c): C4, C5 and C7 now catch only
  `InvalidOperationException`, `FormatException` and `ArgumentOutOfRangeException`. A database exception is
  no longer converted into a permanent RenderFailed suppression. Security sees no regression from this.

FINDINGS
- [LOW] src/Api/CatEcommerce.Api/Background/NotificationWorkCycle.cs:89-90 and
  NotificationRequestAggregate.cs `RecordFailedAttempt` (`MaxExpansionAttempts = 5`).
  **Any exception from an Expand dispatch counts toward the 5-strike park, including transient ones.**
  - This covers a lost connection, a timeout, or a failed quiet-hours read. It is not limited to a failure
    that repeats deterministically.
  - The counter is not time-based. A database that flaps (it fails the Expand, then recovers in time for the
    recovery write) can park a genuine request as PersistenceFailed after about 5 polls, roughly 25 s.
    That includes a password reset.
  - Impact: the reset is lost silently. The user has to ask again. No data is disclosed, and an attacker
    cannot trigger this.
  - Fix, either of:
    - count only non-transient failures (for example, skip `NpgsqlException.IsTransient` and timeouts);
    - or back off by `LastAttemptOn` and park on elapsed time rather than on count.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Notification/DeliverNotificationCommandHandler.cs:94-125.
  **The C5 path has no equivalent of the C4 attempt counter.**
  - Overflow is now prevented before the save, so the known cause is gone.
  - A save failure of any other kind after the committed claim still leaves the row `Sending`. It is then
    re-claimed and re-sent every lease.
  - `GetDueNotificationIdsAsync` orders by `CreatedOn`, so such rows sort first.
  - No input that triggers this is known today, so it is recorded for the backlog only.
- [LOW, CARRIED] CatProfilesModule.cs `PublicSiteStartupGuardHostedService`. There is still no placeholder-host
  check for `PublicSite:WebBaseUrl` outside Production. This is unchanged from pass 2 and fails safe.
- [LOW, CARRIED] The unsubscribe plaintext is stored beside its hash. This is unchanged from pass 2 and
  latent: v1 has no Marketing template.

VERIFIED (no finding)
- Authorization: no endpoint, policy or permission changed in the delta.
  - `RecordNotificationExpansionFailureCommand` is dispatched only in-process by the worker, under
    `SystemActors.NotificationWorker`.
  - No controller exposes it.
- Logging: the new `LogError`/`LogWarning` calls log ids, the channel name and the exception.
  `NotificationRenderOverflowException` messages carry only lengths, never content.
- 5aad2d7 only squashes migrations. The schema matches the model, and there is no security-relevant DDL
  change beyond the two new columns.

SCOPE CHECK
- The delta touches 24 src files, all in the Notifications area, plus:
  - `SettingKeys.cs` and `NotificationRequestRules.cs` (shared constants for this area);
  - `TemplateRules.cs` (this area's helper);
  - `Program.cs` (one unused `using` removed);
  - tests and docs.
- files outside allowed scope: none that add exposure. File-by-file accounting belongs to ARCHITECTURE and
  CODE_REVIEW.

---

PASS 2 RECORD (kept for history)

GATE: SECURITY
HANDOVER: HO-NTF-001
COMMITS: pass 2 reviewed `ho/NTF-001` at head 4efe96f, as a fresh `git archive 4efe96f`.
  - The delta since pass 1 (40a3373) is the gate-fix commits 50395e6, ca6d3ea, 0b7f201, 7d08879, c91d411,
    027b1dc, 482136c, 4a7e967, 1132c4d, 1708e16, 51b68bd, f1839e9, 9d560b6, 9a9f25f, b496f22, d875a8f,
    a2705b8, ff449e2, e03f5d2, 58c7d9e, d6eb049, b755903 and 4efe96f.
  - It also includes the master merge 97aa959, which brings in eef1621 (HO-CAT-001-gates).
  - The merged result was reviewed for the notification, rate-limit and host-composition surface.
DATE: 2026-09-27
STATUS: FAIL

HISTORY
- Pass 1 (head 40a3373): FAIL. There was 1 HIGH, 3 MEDIUM and 6 LOW. The pass-1 record is kept below.
- Pass 2 (this entry): FAIL, on one MEDIUM.
  - The HIGH is closed, and so are two of the three MEDIUMs. All were re-proven by live probe.
  - The third MEDIUM ("one bad template or value blocks the whole pipeline") is only partly fixed:
    - Render-time exceptions are now caught and the request is suppressed.
    - A request that fails when it is **saved** still stays Pending forever and still blocks the queue
      head-first. Probe P6c proves it: a template an Administrator is allowed to save, plus a valid cat name,
      holds up a password reset.
    - The pass-1 remedy (an attempt count or parking, plus queue ordering) was not implemented.
  - Everything else is closed, or is LOW and does not block the gate.

HOW IT WAS CHECKED (pass 2)
- Scratch setup:
  - fresh `git archive 4efe96f` into `scratchpad\ntf1-sec2`, with no git-ignored appsettings;
  - fresh DB `catecommerce_ntf2sec`, created with `dotnet dotnet-ef database update --connection ...`. All
    migrations applied, including AddNotificationResendOrdinal;
  - `CATECOMMERCE_TEST_DB` set to the full connection string.
- Suites, all green on the fresh DB on the first run. The pass-1 order-dependent QuietHours failure did not
  recur.

  | Suite | Result |
  |---|---|
  | Domain | 311/311 |
  | Application | 107/107 |
  | Infrastructure | 45/45 |
  | Integration | 439/439 |

- Probes:
  - `SecProbe2NtfTests.cs` has 12 probes, run through `NotificationsApiFactory` with real sign-in and CSRF.
  - `startup-probe.sh` ran the built API DLL against the fresh DB under Production and Staging with
    different configurations (8 runs).
  - Everything is kept in `scratchpad\ntf1-sec2-probes\`: the probe source, `probe-run1.log`,
    `probe-run2-P6c.log`, `startup-probes.log` and the suite logs.
  - Nothing was added to the worktree.

PASS-1 FINDINGS, RE-PROBED
- [HIGH, CLOSED] Reception could read a live reset token (GetNotificationDetailQueryHandler).
  - Detail now returns `"[redacted]"` for body, subject and title whenever `ContainsSecret` is set,
    whatever `RedactedOn` says, with reason `Secret`.
  - P1: an Administrator's staff reset, held in RetryPending by a scripted transient 503. Reception and a
    second Administrator each read all of these, and none contained `token=`:
    - the log search by recipient;
    - the log search by `templateKey=account.password-reset`;
    - an unfiltered page of 100;
    - the summary;
    - the detail;
    - the resend response (400: resending a secret is refused).
  - The database body did still hold the token (`dbBodyHasToken=True`). This is expected: ADR-063 keeps
    it at rest.
  - P1b: the same holds in the plain Queued window, for a customer reset over SMS and over email. The
    customer's own inbox is empty (there is no inbox copy).
  - There is no other read path for body, HTML or unsubscribe link:
    - the list projection has no body;
    - notification roots are excluded from audit;
    - no export or personal-data export includes notification bodies;
    - resend of a secret is refused.
- [MEDIUM, CLOSED] Variable-redaction sweep starvation (NotificationWorkQueue).
  - `VariablesRedactedOn == null` has been added to the filter.
  - P2: 56 expired secret requests, then 4 passes. Result: 0 unredacted, the 56th request is redacted, and
    0 secret notifications hold a plaintext token.
- [MEDIUM, CLOSED] Anonymisation skipped already-redacted rows.
  - `Redact(Anonymisation)` is now idempotent on its own `AnonymisedOn` marker, and it nulls `Destination`
    and `UnsubscribeLink` even on Secret-redacted rows.
  - P3 covers both erasure paths: the `CustomerAccountAnonymisedEvent` handler, and the new
    `NotificationPersonalDataEraser` (outcome Completed). Afterwards:
    - every row, including Sent password resets, has `dest=null`, `AnonymisedOn` set and reason
      Anonymisation;
    - request variables are `[redacted]`.
  - Merged-away duplicates are now reached through `FindMergedDuplicatesAsync`, which is transitive with a
    hop bound. The branch's `Anonymise_RedactsSurvivorAndMergedDuplicateNotifications_EvenIfAlreadySecretRedacted`
    runs the real merge and deletion endpoints and is green.
- [MEDIUM, PARTLY CLOSED -> see MEDIUM below] Poison template or value blocks the pipeline.
  - Closed parts:
    - P6a: U+E000/U+E001 sentinels, other BMP private-use characters, supplementary private-use
      characters, ZWSP and RLO are all rejected with 400 on save and on preview, in body, subject and title
      (18 of 18).
    - Request variables that carry them are refused by the aggregate.
    - P6b: 60 requests with a bad `Date` value all end Suppressed (RenderFailed) within 3 passes, and a
      password reset queued behind them is Sent. The render catch in C4, and in the C5 fallback, is now
      `catch (Exception) when not OperationCanceledException`.
  - Still open: persistence-time failures (see below).
- [LOW, CLOSED] Resend limit race (P9).
  - The new unique filtered index `ix_notification_resend_of_id_ordinal (resend_of_id, resend_ordinal)`
    holds.
  - 20 parallel resends of one notification (Administrator and Reception, mixed SMS/Email) gave exactly
    3 × 201 and 17 × 400, with 3 resend rows in the database and no 5xx.
- [LOW, CLOSED] Preview and save returned 500 (P8/P10). There were 0 × 5xx across 32 malformed inputs:
  - `contents` null, `[null]` or a null channel;
  - channel `Fax` on save, preview and resend;
  - a 2 MB sample (now capped at 500);
  - `javascript:`, a bad date or a sentinel in a sample;
  - a sentinel in the body;
  - a NUL in destination, token or channel;
  - a 1 MB SMS body.
- [LOW, CLOSED] LeaseSeconds was ignored.
  - C5 now reads `deliverySettings.LeaseSeconds`.
  - The validator refuses `LeaseSeconds <= SendTimeoutSeconds`, and startup probe E (LeaseSeconds=10)
    fails.
  - The requested safety margin is not enforced (11/10 is accepted). This is a configuration matter only;
    no finding.
- [LOW, MOSTLY CLOSED] `.invalid` placeholder URLs in Production. Startup probes against the real DLL:
  - A: Production with nothing set fails (ApiBaseUrl is required and must be https).
  - B: `PublicSite:WebBaseUrl` empty fails with "required in Production".
  - F: `ApiBaseUrl=https://api.invalid` fails with the placeholder-host check.
  - G: the Capturing sender in Production fails.
  - The residual is the LOW below.
- [LOW, CARRIED] Plaintext unsubscribe URL stored beside its hash. Judged below.
- [LOW, CLOSED] Merged-away customer's messages were not anonymised. See the anonymisation item above.

NEW SURFACE: RATE LIMITER MOVED TO THE HOST PIPELINE
- Code: `AddRateLimiter` is called three times: in the host base (`OnRejected` only), in Accounts' policies and
  in Notifications' policies. They compose on one `RateLimiterOptions`. `app.UseRateLimiter()` runs in
  Program.cs:
  - after `UseRouting()`, which endpoint `[EnableRateLimiting]` metadata requires;
  - after `UseAccountsModule()`, where SessionAuthenticationMiddleware writes `ResolvedCaller`, which
    ProfileUpdate partitions on;
  - before `MapControllers()`.
- RL probe, on a factory with tight limits and the real Program.cs pipeline. Every policy still returns
  429 with `Retry-After` and the shared `TooManyRequests` body:
  - CaptchaIssue: 200, 200, 429, with Retry-After 600.
  - SignIn: customer 401, 401, 429; staff 429.
  - PasswordReset: customer request 200, 200, 429 with Retry-After 3600; confirm 429; staff request 429.
  - Unsubscribe (JSON): 400, 400, 429.
  - Unsubscribe (one-click): 429, because it shares the bucket with JSON unsubscribe by design (I-E4).
  - TemplatePreview: 400, 400, 429.
  - AccountSensitive is covered by the green HO-ACC-001 suite.
- Per-caller partitioning: with ProfileUpdate limited to 2, customer A got 204, 204, 429, and customer B's
  first call was still 204. The customer-id partition works in the moved pipeline.
- The IP-partitioned policies are unchanged in code, and ASP.NET scopes partitions per policy. The TestServer
  reports no remote IP, so I could not show per-IP separation live.
- Customer and staff sign-in share one SignIn bucket per IP. Reset request and confirm share one
  PasswordReset bucket. Both predate this handover (HO-ACC-001 policy names), so neither is a regression.
- `appsettings.Testing.json` holds only `Notifications:ApiBaseUrl=https://api.example.test` and
  `Worker:Enabled=false`. It is loaded only when the environment is `Testing` (standard
  `WebApplication.CreateBuilder`; Program.cs has no explicit `AddJsonFile`). It ships in `bin/`, but:
  - Startup probe H (Production, with valid URLs) logs the worker's "No SMS/email provider configured"
    warnings. Those are emitted only on the `Enabled` branch, so the worker runs.
  - The `.example.test` placeholder would fail in Production anyway (probe F logic).
  - Nothing else in that file relaxes a control.

FINDINGS

- [MEDIUM] ExpandNotificationRequestCommandHandler.cs:99-121 (the try/catch covers rendering only; the save at
  :127 is outside it), NotificationWorkQueue.cs:12-18 (`Pending`, ordered by `RequestedOn`, `Take(BatchSize)`,
  with no attempt count and no parking) and NotificationConfiguration.cs:41-45 (subject varchar(150),
  title varchar(100), body varchar(10000), link_path varchar(500)).
  **A request whose rendered text is too long for its column can never leave Pending. Once BatchSize such
  requests exist, no notification is expanded any more, including password resets.** This is the part of
  the pass-1 MEDIUM that is still open.
  - Template validation checks the length of the raw template, before substitution. Substitution then adds
    up to 2000 characters per variable.
  - Values within their own limits therefore overflow a column: a cat name of 100 characters, or a
    customer FullName of up to 200.
  - Postgres raises 22001 in `UpdateAsync`, which is outside the catch. `DispatchAsync` logs it and swallows
    it, and the request stays Pending.
  - It is re-selected, oldest first, on every poll (every 5 s) and never parked.

  Probe P6c:
  1. An Administrator saves the deworming InApp title "Time for {{catName}}'s {{treatmentName}} treatment -
     book now at the clinic" (76 characters). The save returns 204.
  2. Three reminders are queued for a cat named with 100 characters.
  3. With `Worker:BatchSize=3`, after 4 passes all 3 requests are still Pending, with a 22001 error logged
     each pass.
  4. A password reset requested after them is still Pending, with no notification created.
  5. In production the same thing happens with 50 stuck rows.

  Two things make this worse than a one-off failure:
  - Care reminders have no `NotAfter` (CareReminderSweep), so stuck rows never age out; they accumulate.
  - If an Administrator puts `{{recipientName}}` in an email subject or an InApp title, a customer who
    registers a long name can mint stuck rows on demand. Password resets do carry `NotAfter` (15 min), but
    enough reset requests from rotating IPs keep the queue saturated.

  The same shape exists on the delivery side:
  - The fallback's rendered row is saved in C5's final `UpdateAsync` (DeliverNotificationCommandHandler.cs:125).
  - If that save throws, the claim is already committed, so the row stays `Sending`. It is re-claimed and
    re-sent to the failing channel every lease, and it is re-selected ahead of newer rows by
    `GetDueNotificationIdsAsync`.
  - A cross-channel resend that overflows returns 500, because `EfNotificationResendStore` catches only the
    unique violation.

  **Fix (any one closes the availability issue; the first two together are the robust answer):**
  1. After rendering, check each rendered field against its column limit. An overflow counts as a render
     failure: suppress the request as RenderFailed, or truncate subject and title with an ellipsis.
  2. Give requests (and notifications) a failure count, and move them to a terminal state after N persistence
     failures. The worker has to do this in a fresh scope, because the failing context is poisoned.
     Alternatively, order the pending queue by last attempt so failures stop sorting first.
  3. Optionally, at template save, refuse content whose template length plus the maximum variable lengths
     can exceed the column.

  Add a test with more than `BatchSize` overflowing requests followed by a reset, and assert the reset is
  Sent.

- [LOW] TemplateRules.cs:237 and :261 (`StartsWith('/') && !StartsWith("//")`).
  **The "site-relative" check accepts `/\evil.test`.**
  - P10: an Administrator saved an InApp `linkPath` of `/\evil.test` and got 204.
  - WHATWG URL parsing treats `\` as `/` for http(s), so a browser resolves this to `https://evil.test/`.
  - The rule on in-app links is "empty, a site-relative path, or a single Url placeholder", and this path is
    exactly what it is meant to stop.
  - The impact is low:
    - Only an Administrator can save templates.
    - Email bodies may already link to any https host.
    - The portal inbox UI (HO-NTF-002) does not exist yet.
  - The same check is used for email link targets.
  - Fix: also reject `/\` (and `\` anywhere in a path). Better still, require the path to match
    `^/[A-Za-z0-9\-._~/%?#=&]*$`. HO-NTF-002 should still treat `linkPath` as untrusted.
  - The pass-1 probe did not catch this: the C# literal `"/\evil.test"` compiled to `/<ESC>vil.test`, which
    the control-character rule rejected.

- [LOW] CatProfilesModule.cs:122-151 (`PublicSiteStartupGuardHostedService`). This is the residual of the
  pass-1 placeholder-URL LOW.
  - `PublicSite:WebBaseUrl` is required only when `IsProduction()`, and it has no placeholder-host check.
    (`Notifications:ApiBaseUrl` has one.)
  - Startup probe C: Production with `https://app.invalid` starts.
  - Startup probe D: Staging with it empty starts.
  - This fails safe:
    - An empty base makes `resetLink` non-https, the renderer throws, and the reset is suppressed as
      RenderFailed.
    - `.invalid` cannot resolve.
    - No token reaches a third party.
  - Users do silently get no reset, or a dead link.
  - Fix: outside Development and Testing, require the value and refuse `.invalid`, `.example`, `.test` and
    `localhost` hosts, matching `NotificationOptionsValidator`. The setting is shared with HO-CAT-001's
    cage-card QR, so this needs integrator sign-off.

- [LOW, CARRIED] ExpandNotificationRequestCommandHandler.cs:133-141 and DeliverNotificationCommandHandler.cs:170-181.
  `UnsubscribeLink` and `Body`/`HtmlBody` store the plaintext unsubscribe token next to
  `UnsubscribeTokenHash`.
  **Judgement: LOW, and latent in v1.**
  - The v1 catalogue has no Marketing template. The only templates are account.password-reset,
    care.vaccination-due and care.deworming-due, all Transactional or secret. No unsubscribe token is issued
    in production today; the path runs only with test-only definitions.
  - When marketing ships, the token can do exactly one thing: revoke the recipient's marketing consent on
    one channel. Revocation is idempotent and reversible by the customer. It grants no sign-in, discloses
    nothing but the channel name, and cannot opt anyone in.
  - Anyone who can read `notify.notification` already holds far more: every destination, and every body
    within the ADR-063 window.
  - A `NotificationLog.View` holder (Reception or Administrator) would see the link in the log detail.
  - The hash therefore works as a lookup key, not a confidentiality control, and ADR-064 overstates it.
  - This does not block, but before a Marketing template is added, do one of these:
    - clear `UnsubscribeLink` and redact the token from `Body`/`HtmlBody` once the notification is
      terminal (the same C6 hook secret redaction uses), and mask it in log detail;
    - or amend ADR-064 to say the hash is a lookup key only.

VERIFIED (no finding)
- Authorization matrix (P7). Anonymous, customer, Reception, Nurse, Vet, ShopManager, Accountant and
  Administrator were run against all 12 endpoints, and the results match §12.3 unchanged since pass 1:
  - Templates: Administrator and ShopManager can read and preview. Save and reset are Administrator only.
  - Log, summary, detail and resend: Reception and Administrator only.
  - Inbox: customer session only. Another customer's id returns 404.
- Unsubscribe authorization after 9a9f25f: a token revokes consent only on its own notification's recipient,
  or that recipient's merge survivor. The response still discloses only the channel and a bool.
- Template injection (P5 from pass 1). The renderer is unchanged apart from the catch, and value
  substitution still happens after parsing. Admin link rules held on re-run (P8): `javascript:`, `//evil`,
  raw HTML and a 1 MB body all return 400. The `/\` gap is listed above.
- Secrets and logging:
  - No new logger call includes a body, a variable, a destination or a token.
  - The new `LogError(ex, "Rendering failed ...")` logs the exception. Renderer exception messages carry
    only the variable name, never the value.
  - Sender exceptions are still recorded by type name only.
- `DeliveryAttempt.ProviderResponse` is shown unredacted in the log list and detail, even for secret
  notifications. This is safe today:
  - the Capturing and Unconfigured senders never echo the request;
  - `DeliveryAttempt` documents "never the request".
  The real SMS and email adapters (OQ-1) must keep that contract, because some gateways echo the message
  text in their error bodies. This is recorded for the provider handover, not as a finding here.

SCOPE CHECK
- NTF gate-fix commits since pass 1: 80 files. Outside the Notifications area and docs, they touch:
  - Program.cs, HostPipelineExtensions.cs and AccountsModule.cs (the rate-limiter move, integrator-authorised);
  - SettingKeys.cs (made partial, integrator-authorised);
  - ControlCharacters.cs, TemplateRules.cs and ValidationRuleExtensions.cs (the private-use and format
    character rule);
  - appsettings.json and appsettings.Testing.json;
  - tests.
- For security purposes each of these was checked above, and none adds exposure.
- The master merge 97aa959 (HO-CAT-001-gates) was gated separately. File-by-file scope accounting belongs to
  ARCHITECTURE and CODE_REVIEW.

---

PASS 1 RECORD (kept for history)

GATE: SECURITY
HANDOVER: HO-NTF-001
COMMITS: range master...ho/NTF-001, head 40a3373 (reviewed as `git archive 40a3373`)
DATE: 2026-09-27
STATUS: FAIL

This is pass 1 of the SECURITY gate. I reviewed `git diff master...ho/NTF-001` (197 files) against
HO-NTF-001, covering §8.2–§8.11, §11.4, §12.1–§12.5 and ADR-056 to ADR-066. I also ran live probes
against a fresh scratch archive with no git-ignored appsettings, on a fresh database
(`catecommerce_ntf1sec`, created by `dotnet ef database update`).

The probes are kept in the scratchpad at `ntf1-sec1-probes/`: `SecProbeNtfTests.cs` and the
`probe-run*.log` files. They are not part of the deliverable.

SUITES (fresh DB, CATECOMMERCE_TEST_DB = full connection string)
- Domain 284/284, Application 86/86, Infrastructure 39/39.
- Integration: 356/357 on the first run. `QuietHoursEndpointTests.NormalReminder_At2230_…` failed
  with Expected "Queued", Actual "Undeliverable", and passes when run alone. The second run, made
  after my probes, gave 355/357 with two different failures, both of the same "Undeliverable
  instead of Queued" shape. The suite depends on test order and database state. This belongs to
  the TEST gate. It is not a security finding.

FINDINGS

- [HIGH] src/Read/CatEcommerce.Read/QueryHandlers/Notification/GetNotificationDetailQueryHandler.cs:65-70
  (`notification.Body` is returned as-is) together with
  ActionRoles.Notifications.cs (`NotificationLog.View` = Reception, Administrator).
  **A Reception user can read a live password-reset link for any account, including an
  Administrator, and take the account over.** Detail replaces the body only once `RedactedOn` is
  set. A secret-bearing notification is redacted only when it reaches Sent, Undeliverable or
  Expired. While it is Queued, Sending or RetryPending, the body holds the plaintext
  `/reset-password#token=…` link and is served to any `NotificationLog.View` holder.
  Proven end to end (probe P1):
  1. Reception posts, anonymously, `/api/staff/auth/password-reset/request` with the
     Administrator's mobile number.
  2. The provider returns one transient failure (429/5xx/timeout), so the notification goes to
     `RetryPending` for 1–5 min.
  3. Reception searches the log with `templateKey=account.password-reset`.
  4. Reception calls `GET /api/admin/notifications/{id}`, and the body contains the token.
  5. `POST /api/auth/password-reset/confirm` returns 204.
  6. Staff sign-in as the Administrator with the new password returns 200.

  Probe P1b shows the same leak in the plain Queued window, with no provider failure: the
  notification has been expanded but not yet claimed. With a real provider there is always a window
  of about the length of the send call. A transient failure widens it to minutes, and the attacker
  can retry as often as the reset rate limit allows.
  ADR-063 accepts the plaintext only at rest in `notify.*`. It does not accept serving the
  plaintext over the API. The handover also says "no inbox copy, so the link never sits in the
  portal", and the staff portal is the same kind of exposure.
  **Fix:** In detail, and in any other read of body, subject, title, HTML or link, return
  `"[redacted]"` (reason `Secret`) whenever `ContainsSecret` is true, whatever `RedactedOn` says. Add
  an integration test in which Reception opens a Queued and a RetryPending reset notification and
  the token appears in no response.

- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationWorkQueue.cs:31-37
  **The variable-redaction sweep never excludes rows it has already redacted, so once 50 expired
  secret requests exist, every later reset link stays in plaintext indefinitely.**
  `GetRequestsDueForVariableRedactionAsync` filters on `ContainsSecret && NotAfter < now`, ordered
  by `NotAfter`, and takes `BatchSize`. It has no `VariablesRedactedOn == null` condition, so the
  same oldest 50 rows are selected and re-processed on every 5-second pass, forever.
  Probe P2: 62 rows matched and 56 were already redacted. The newest request's `resetLink` was
  still in plaintext 2 hours after its `NotAfter`, after 6 passes. ADR-063/§8.10 bounds the
  plaintext at "at most the token lifetime (15 minutes)". In production that bound is gone after
  the first 50 password resets. The token has expired by then, but the plaintext link is kept in
  the database and in every backup.
  **Fix:** Add `&& r.VariablesRedactedOn == null` to the query, and add a test with more than
  `BatchSize` expired secret requests.

- [MEDIUM] src/Domain/CatEcommerce.Domain/Aggregates/Notification/NotificationAggregate.cs:230-239
  together with AnonymiseNotificationsOnCustomerAnonymisedHandler.cs:30-35.
  **Anonymisation leaves the phone number or email address on every notification already
  redacted as a secret, which includes every delivered password reset.** `Redact` returns early
  when `RedactedOn` is already set, and the handler's loop filters `RedactedOn == null`. A reset
  SMS that was sent and secret-redacted therefore keeps `Destination` after the customer is
  anonymised. Probe P3 confirmed this: after anonymisation, `account.password-reset Sms
  status=Sent dest=+8801917190142 body=[redacted]`. This breaks §11.6 ("Redact(Anonymisation),
  which also nulls the destination") and the erasure guarantee. The log search by `destination`
  would still find the anonymised person.
  **Fix:** On anonymisation, null `Destination` (and `UnsubscribeLink`) even when the row is
  already redacted, for example with a separate `Anonymise()` that is idempotent on its own
  marker. Select rows with `Destination != null || RedactedOn == null`. Add the missing I-D11 test.

- [MEDIUM] src/Domain/CatEcommerce.Domain/DomainServices/NotificationRenderer.cs:24-25,95-99, ExpandNotificationRequestCommandHandler.cs:100 (catches `InvalidOperationException` only),
  DeliverNotificationCommandHandler.cs:188 (catches `InvalidOperationException` only), and NotificationWorkQueue.cs:12-18.
  **One bad template or value blocks the whole notification pipeline, password reset included.**
  - The renderer's sentinel characters, U+E000 and U+E001, are not rejected by `TemplateRules`
    or `NoControlCharacters`. An Administrator saved `care.deworming-due` with `99`
    in the InApp body and got 204.
  - Every later expansion of that template then throws `ArgumentOutOfRangeException`. This is not
    an `InvalidOperationException`, so the request is never suppressed and stays `Pending`
    (probe P6: status is still Pending after 3 passes, with an Error log on each pass).
  - A `Date` value that is not `yyyy-MM-dd` does the same through `FormatException`.
  - Requests have no attempt count, so such a request is retried every 5 s indefinitely.
  - `GetPendingRequestIdsAsync` takes the 50 oldest Pending requests. One reminder sweep over 50
    cats therefore produces 50 stuck requests, and after that **no** request is expanded,
    including password resets. Because this is head-of-line blocking, the notification system is
    down for everyone.
  - The fallback render in C5 has the same narrow catch. A throw there comes after the claim and
    before the final save, so the notification stays `Sending` and is re-claimed and re-sent every
    lease (120 s) with no limit.

  **Fix:**
  - Reject U+E000/U+E001 (or all private-use code points) in `TemplateRules`.
  - Catch every non-fatal exception around rendering in C4 and C5, and suppress or fail the item.
  - Give requests a failure or attempt count so a poison item is parked instead of retried.
  - Stop the due queue from returning the same poison ids ahead of healthy ones, for example by
    parking failed items or ordering by the last attempt.

- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Notification/ResendNotificationCommandHandler.cs:39-47
  **The resend limit can be bypassed with parallel requests.** The handler counts existing resends
  and then inserts, with no lock or unique constraint. In probe P9, 10 parallel
  `POST …/resend` calls from one Administrator gave **8 × 201**, against the limit of 3. A
  Reception user can use this to flood a customer with SMS or email and run up provider cost.
  **Fix:** Serialise per chain root. Options are a row lock on the original, an optimistic version
  bump on the original in the same commit, or a unique `(ResendOfId, ResendOrdinal)` index.

- [LOW] PreviewNotificationTemplateQueryHandler.cs:27-29,60-63 and the Save path.
  **Several inputs cause 500 instead of 400** (the "400 not 500" rule).
  - Preview returns 500 for:
    - `contents:[null]`
    - `channel:"Fax"` (`Enumeration.FromDisplayName` throws)
    - a `Url` sample override that is not https, for example `javascript:alert(1)` (throws
      `InvalidOperationException`)
    - a `Date` sample override of `"junk"`
    - a body containing the sentinel characters
  - `PUT` returns 500 for `contents:[null]`.
  - Preview also accepts a 2 MB `sampleValues` entry and echoes it back three times. It is
    rate-limited and restricted to Administrator and ShopManager.

  No data leaks through these, but they are unhandled exceptions reachable by staff.
  **Fix:** Validate the preview body the same way as C1: channel enumeration, non-null items, and
  control or private-use characters. Validate sample values by kind (Url must be https, Date must
  be `yyyy-MM-dd`) with a length cap. Map renderer failures to 400.

- [LOW] ExpandNotificationRequestCommandHandler.cs:130-135 and NotificationAggregate `UnsubscribeLink`.
  **The unsubscribe token is hashed at rest, but the plaintext is stored beside the hash.** The
  full link, with the plaintext token, is stored in `notification.unsubscribe_link` and inside
  `body`/`html_body`. Anyone who can read the table, or any Reception/Administrator user through
  the log detail, can unsubscribe the customer. The impact is low because staff can edit
  preferences anyway. However, hashing is claimed as a control (ADR-064) and gives no protection
  here.
  **Fix:** Do not persist `UnsubscribeLink` (rebuild the header from the body at send, or keep it
  only for the send). Alternatively, document that the hash exists only as a lookup key.

- [LOW] DeliverNotificationCommandHandler.cs:38 (`private const int LeaseSeconds = 120`).
  **The configured lease is ignored.** `Notifications:Worker:LeaseSeconds` has no effect, while
  `SendTimeoutSeconds` may be set up to 120. With that configuration the lease can expire during
  a slow send and a second poll or instance can re-claim and send twice.
  **Fix:** Read the lease from options and validate `LeaseSeconds > SendTimeoutSeconds` plus a
  margin in `NotificationOptionsValidator`.

- [LOW] src/Api/CatEcommerce.Api/appsettings.json (`WebBaseUrl` https://app.invalid, `ApiBaseUrl` https://api.invalid) and NotificationOptionsValidator.cs.
  **The shipped placeholder URLs fail safe, but silently.** The `.invalid` hosts cannot resolve
  (RFC 6761), so links cannot reach an attacker. The base URLs come only from configuration, never
  from the Host header or from a request. This part is fine. However, the placeholders pass
  `ValidateOnStart` in Production. A deployment that forgets to set them sends dead reset and
  unsubscribe links, with no warning.
  **Fix:** Outside Development, refuse `.invalid`, `.example`, `.test` and `localhost` hosts at
  start (or at least warn), as is already done for non-https URLs.

- [LOW] AnonymiseNotificationsOnCustomerAnonymisedHandler.cs:30.
  **Anonymising a merge survivor leaves the merged-away customer's messages untouched.** The
  handler redacts only rows where `RecipientId == survivor`. External-channel rows of a customer
  merged into the survivor keep their original `RecipientId` (§11.6: "the log keeps the original
  recipient"), so they keep their body and destination. Merged accounts cannot themselves be
  anonymised (the aggregate requires Active), so nothing ever redacts these rows. This mirrors the
  shape of HO-ACC-001.
  **Fix:** Also redact rows whose recipient has `MergedIntoCustomerId` pointing to the survivor
  (transitively), or record this as an accepted residual.

VERIFIED (no finding)

- **Authorization matrix** (probe P7): anonymous, customer, Reception, Nurse, Vet, ShopManager,
  Accountant and Administrator against all 12 new endpoints. Every result matches §12.3:
  - Template list, detail and preview: Administrator and ShopManager only.
  - Template save and reset: Administrator only.
  - Log, summary, detail and resend: Reception and Administrator only. Nurse gets 403.
  - Inbox: customer session only. Staff and anonymous get 401.
- **Inbox isolation:** another customer's id gives 404 (I-D9 and probe P7). The inbox queries
  filter on the session `UserId`, and no id-taking read exists.
- **Template injection** (probe P5):
  - A cat name of `**Mitu**](https://evil.test) <script>… " onmouseover=… {{recipientName}}
    [a](https://evil.test)` is rendered as literal text on every channel. It is HTML-encoded in
    `htmlBody`, creates no `<a>`, and the placeholder is not re-expanded. Values are substituted
    after parsing.
  - Admin-authored links are restricted (probe P10): `javascript:`, `http:`, raw HTML, and a
    Text placeholder as the link target are each refused with 400, as are an in-app `LinkPath` of
    `javascript:` or `{{catName}}`.
  - Malformed placeholders (`{{ catName }}`, `{{constructor}}`) and a placeholder from another
    template (`{{resetLink}}`) are refused, and the offending name is given.
  - There is no templating engine and no reflection-based property access.
  - Preview uses definition samples and fixed standard values, never real customer data.
- **Reset tokens:**
  - The request and the token commit in one transaction (the dispatcher runs before `SaveAsync`).
  - There is no inbox copy.
  - Resend is refused for secrets.
  - Notification and request roots are excluded from audit capture, and no token appears in audit
    (I-B3).
  - No logger call in the changed code logs a body, variable, destination or token. Sender
    exceptions are recorded by type name only.
  - The Development sender logs bodies, but it refuses to construct outside Development and is
    also refused by `ValidateOnStart`.
- **Unconfigured providers fail safe** (ADR-061): the default and any unknown sender kind resolve
  to `UnconfiguredChannelSender`, which returns a permanent failure. The Capturing sender is
  restricted to Testing.
- **Unsubscribe:**
  - Tokens are 256-bit (`RandomNumberGenerator`), base64url, looked up by SHA-256.
  - Repeating an unsubscribe is idempotent, and the response discloses only the channel and a
    bool.
  - Tokens cannot be enumerated.
  - The endpoint changes only the preferences of the notification's own recipient.
  - **I-E4 checked** (probe P4): with PermitLimit 20, mixed JSON and one-click posts from one IP
    returned 20 × 400 and then **429** on the 21st.
  - Token input returns 400 for NUL, control characters, 101 chars, 100 k chars, null, an empty
    body, and a missing one-click token (probe P8).
- **Log search input** (probe P8): NUL or control characters in destination return 400. NUL in
  channel returns 400. An overlong templateKey is handled harmlessly. Ranges are required,
  limited to 93 days, and `to < from` is refused. Paging caps hold. Resend channel is validated
  (InApp, junk and 10 k chars all return 400).
- **Delivery:**
  - The claim is committed before the provider call, and `xmin` stops a double claim (I-B11).
  - Retries are capped at 6 attempts with backoff.
  - A secret on a terminal status is redacted in the same save.
  - The marketing opt-out is re-checked at send.
- **Quiet hours:** the reset template is `Urgent` and is never deferred. Quiet hours are evaluated
  in the branch zone (I-C1 to I-C4 exist; see the flaky-suite note above).

SCOPE CHECK
- Files changed: 197. For security purposes I checked the edits outside §13 that touch security
  surfaces. Each one is a deviation recorded by the implementation:
  - `Modules/AccountsModule.cs`: the dispatcher registrations were removed.
  - `Filters/UniqueConstraintExceptionFilter.cs`: `ix_notification_template_key` now maps to 409.
  - `RequestPasswordResetCommandHandler.cs`: the dispatcher now runs before the save.
  - `ChangeNotificationPreferencesCommandHandler.cs` and `CustomerProfileController.cs`:
    `preferredChannel` was added, and it is scoped to the session user.
- None of these adds exposure. The full file-by-file scope accounting belongs to ARCHITECTURE and
  CODE_REVIEW.
