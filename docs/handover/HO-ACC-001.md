# HO-ACC-001 — Accounts and Identity (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-ACC-001** |
| Title | Accounts and Identity — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §1 — **US-ACC-01, 04, 05, 06, 07, 08, 10, 11, 12, 13, 14**. Withdrawn and **not** built: US-ACC-02, 03, 09 |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-ACC-1, 3, 4, 5, 6, 7, 9, 10, 11, 12, 13**, FR-POR-6, FR-ADM-2, FR-NTF-4, **NFR-SEC-1**, NFR-SEC-2, NFR-SEC-4, **NFR-SEC-6**, NFR-SEC-7, NFR-SEC-8, NFR-LEG-4, C-3, C-7, C-9, D-5, D-11, Appendix D. FR-ACC-2 and FR-ACC-8 are **withdrawn** |
| Pairs with | [`HO-ACC-002`](HO-ACC-002.md) — the frontend for the same stories. **§12 of this document is the contract it builds against** |
| Supersedes | The 13 per-story backend handovers `HO-US-ACC-01-BE` … `HO-US-ACC-13-BE`, removed on consolidation (2026-09-22). They contradicted each other on the context name, the hash algorithm, the aggregate name and ADR numbering, and several built verification, OTP and 2FA that the owner has since removed. **Nothing in them binds this build**; §9 settles every point they disagreed on |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — by [`HO-ADM-001`](HO-ADM-001.md), before implementation.** (1) The Cat Profile and Vaccine endpoints that §4.3 left unguarded are guarded by HO-ADM-001 §12.3, which extends this handover's `RequireActionFilter` with customer ownership scoping. (2) ADR-036's note that administrator-editable permissions belong "to US-ADM-02" is superseded by HO-ADM-001 ADR-044: permissions remain code-defined. (3) The staff idle timeout is read from HO-ADM-001's business settings, with `AccountOptions.StaffIdleTimeoutMinutes` as the seed default. Nothing in this document's own scope changes. **2026-09-22 — by [`HO-NTF-001`](HO-NTF-001.md), before implementation.** (4) Reset delivery (§4.3, §20.4) is built by HO-NTF-001: `DevelopmentPasswordResetTokenDispatcher` and `UndeliveredPasswordResetTokenDispatcher` are replaced by `NotificationPasswordResetTokenDispatcher`, and C4 calls `DispatchAsync` **before** `SaveAsync` so the token and its notification request commit together. No SMS or email provider exists yet (HO-NTF-001 OQ-1), so reset still reaches nobody outside Development. (5) `CustomerNotificationPreferencesAggregate` gains `PreferredChannel`, carried by C8 and Q-E2 — built by HO-NTF-001 §8.5. (6) §11.9 step 7 also excludes the two notification delivery roots from change capture (HO-NTF-001 ADR-066). (7) `AccountOptions.WebBaseUrl` is removed; links are built from `Notifications:WebBaseUrl`. **2026-09-22 — by [`HO-APT-001`](HO-APT-001.md), before implementation.** (8) `UniqueConstraintExceptionFilter` also maps named PostgreSQL exclusion-constraint violations (`23P01`), for the appointment double-booking constraints. (9) Appointments and waitlist entries subscribe to `CustomerMergedEvent` (HO-APT-001 §11.6). **2026-09-26 — by [`HO-ADM-001`](HO-ADM-001.md), gate fix.** (10) ADR-043 (there) redesigns the `401`/`403` split this document's own §12.8 and I-F3 predate: §12.8's "caller not staff → R5 `401 session_invalid`" and I-F3's "a customer session calls it → `401`" both describe an authenticated **customer** getting a `401` for calling a staff-only action -- ADR-043 changes this so only a truly **anonymous** caller gets `401`; an authenticated customer calling an action outside their scope now gets the same `403` + audited-denial path a staff caller failing `ActionRoles` already gets (there is an identity to attribute the denial to). I-F3's expected result is now `403 ActionNotPermitted` plus an `AuthorizationDenied` audit row, identical in shape to I-F2's. §12.8's own R5 text needs the same correction when this document is next revised; not edited directly here per §14. <br><br>**2026-09-23 — appended by later handovers (owed rows, recorded now that all 18 pairs exist).** Everything below is an **append** inside the lines each handover lists in its §13; no existing behaviour changes. These are recorded here so this document is not read as the whole picture. **Appends:** `AuditCategory.ClinicalRecordAccess` and the audit-capture exclusions (HO-EMR-001, HO-IMP-001); `ActionRoles` rows for billing, boarding, inventory, pharmacy, surgery, reporting and file purposes (HO-BIL-001, HO-BRD-001, HO-IMP-001, HO-PHM-001, HO-SRG-001, HO-RPT-001, HO-FIL-001); `SystemActors` entries (`InventoryWorker`, subscription and order workers); unique-constraint filter mappings, including `23514` from Inventory's stock `CHECK` and `23P01` from Boarding's exclusion constraints; `CustomerMergedEvent` subscribers in Billing, Shop and Reporting. Every context that holds customer personal data must also register an `IPersonalDataEraser` or a recorded exemption (HO-XCT-001 C-ALL-2). **2026-09-25 — ARCHITECTURE gate fix on `aa53249` (self-amendment, this handover's own build).** (10) The `Use<Area>Module` pipeline hook does not follow §12.9's literal order: `Platform.Infrastructure.Host.WebApi.UseHttpPipeline()` (1.0.14), decompiled, bundles `UseRouting()`, `GlobalExceptionHandlerMiddleware`, CORS and the actual endpoint dispatch (`UseEndpoints`) into one call, so anything this module needs to run for every request — session resolution, rate limiting — must be registered **before** `UseHttpPipeline()` is called, which puts it outside that specific exception-handler instance unless the module also calls the platform's own, separately published `UseGlobalExceptionHandler()` first. `AccountsModule.UseAccountsModule()` now does exactly that. Recorded as [`ADR-342`](../../.ai/decisions/adr.md#adr-342--a-modules-pipeline-hook-runs-before-usehttppipeline-with-its-own-copy-of-the-platforms-exception-handler), which amends ADR-341. (11) Three files sit outside §13's explicit list, each the home for a type this handover specifies elsewhere and none adding a feature: `src/Domain/CatEcommerce.Domain/DomainServices/ISessionPolicy.cs` (§13 names "the six ports in §8.10"; this is a seventh — needed because `IOptions<SessionOptions>`/`AccountOptions` live in `Infrastructure`, which `Application` cannot reference, §12.2); `src/Infrastructure/CatEcommerce.Infrastructure/Hosting/ResolvedCaller.cs` (named in §12.5 and ADR-032, but not in §13's `Hosting/` list, which names only `HttpContextClientIpAccessor`, `SessionUserContextProvider`, `DevUserContextProvider`); `src/Shared/CatEcommerce.Shared/Common/Helpers/IdentifierMasking.cs` (the masking §11.8 requires, but `Helpers/` in §13 names only `CustomerRecordStatusRules`, `SessionStateRules`, `NotificationPreferenceRules`). (12) `src/Infrastructure/CatEcommerce.Infrastructure/CatEcommerce.Infrastructure.csproj` adds a `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, beyond §13's "the three packages + embedded resource only" — needed because `HttpContextClientIpAccessor` (§12.2) and `MemoryCacheCaptchaService` (§12.6) need `IHttpContextAccessor`/`MemoryCache` types that ship in the ASP.NET Core shared framework, not as a downloadable package for a class library targeting net9.0; it adds no NuGet package. (13) `AuthController`, `StaffAuthController` and `StaffSessionController` inject `ISessionTokenIssuer`, `SessionCookieWriter`, `ISessionPolicy` and/or `TimeProvider` in addition to `IInMemoryDispatcher`/`IUserContextProvider` — §12.4 says controllers inject "IInMemoryDispatcher and IUserContextProvider only", with only `CaptchaController` (ADR-029) recorded as an exception. These three controllers hash a cookie token and write/read session cookies (C3/C13/Q-G1, required by §12.5) and read server time for expiry — not business logic, but not the dispatcher/user-context pair either, so recorded here as the same class of exception ADR-029 already establishes rather than left silent. **2026-09-25 — second ARCHITECTURE gate pass on `d0b19cf` (self-amendment, this handover's own build).** (14) Item (10) above is superseded: `UseGlobalExceptionHandler()` turned out to be the platform's own `GlobalExceptionHandlerMiddleware`, which reads and logs the full request body for a `POST` with `Content-Type: application/json` — exactly what a plaintext password on sign-in, change-password, the deletion step-up or staff sign-in arrives as. Placed ahead of model binding, a database fault during session resolution would have logged it (§4.4, §20.2). Fixed: `UseAccountsModule()` no longer calls any exception handler, `UseForwardedHeaders()` or `UseRouting()` — those move into ONE shared, host-level block in `Program.cs`, run once, ahead of every area's own middleware, ending in a small purpose-written `SafeExceptionHandlerMiddleware` (`src/Api/CatEcommerce.Api/Hosting/`) that never reads the request body. Rewritten as [`ADR-342`](../../.ai/decisions/adr.md#adr-342--a-modules-pipeline-hook-runs-before-usehttppipeline-with-its-own-copy-of-the-platforms-exception-handler)'s amendment, which also corrects the ADR-341 citation (ADR-341 defines only `Add<Area>Module`) and flags itself for integrator/owner ratification as a cross-cutting decision made from an area branch. (15) `src/Api/CatEcommerce.Api/Hosting/SafeExceptionHandlerMiddleware.cs` is a fourth file outside §13's explicit list (alongside (11)'s three) — the ARCHITECTURE gate's own mandated fix for (14), not a feature; §13 does not have a slot for a host-wide, cross-area middleware, because none was owed to this handover before this fix. **2026-09-25 — SECURITY gate fix on `81cf928` (self-amendment, this handover's own build).** (16) §11.4 C15's "duplicate mobile — primary or additional — on an active customer → 400" is narrowed to **primary only**. Additional phone numbers are never verified and never unique-checked, so hard-refusing a walk-in registration on an additional-number match let an attacker who had already put a victim's real mobile into their own `additionalPhoneNumbers` (via `PUT /api/customers/me`, no step-up needed) permanently block reception from ever registering that victim under their own genuine number — the prescribed follow-up (`GET /api/staff/customers?mobile=`) would open the attacker's record instead, and staff attaching the victim's cats to it, or merging the victim into it, would hand the attacker the victim's clinical history, with no recovery path (unlike primary-number squatting, which password reset undoes). An additional-number hit is no longer a refusal; Q-H2 (`FindCustomersQueryHandler`) now reports `mobileMatchedOn` (`"Primary"` / `"Additional"` / `null`) per result so the screen can show the difference rather than treat both the same. (17) §8.3's `UpdateContactDetails` gains a step-up requirement not in the original text: changing `EmailAddress` (adding, removing or modifying it) now requires `CurrentPassword` in `UpdateCustomerContactDetailsCommand`, verified the same way C18 (account deletion) already verifies one. Email is also a login and password-reset identifier; without this, a stolen session alone let an attacker redirect resets to an address they control and rotate the security stamp, turning a temporary session theft into a permanent account takeover that only mobile-based reset (which has no delivery channel yet, §4.3) could undo. `PUT /api/customers/me` gains an optional `currentPassword` field, required only when the handler detects the email is actually changing. See the matching Amended row in [`HO-ACC-002`](HO-ACC-002.md) §1 for the `ProfileForm` field this adds. **2026-09-25 — ARCHITECTURE gate fourth pass / SECURITY gate second pass fix, owner-ratified same day (self-amendment, this handover's own build).** (18) Several items above are ratified or corrected by the owner (2026-09-25), and further scope authorised: **(a)** dropping `UseHttpPipeline()` entirely (items (14)/(15) above) is ratified as the permanent design, not a provisional self-amendment awaiting integrator review — the `.ai/` guide docs that still prescribe `UseHttpPipeline()` will be corrected by the integrator after wave A, not on this branch. **(b)** `src/Infrastructure/CatEcommerce.Infrastructure/Configuration/CorsOptions.cs` and `appsettings.json`'s `Cors` section (a sixth section beyond §13's five) are likewise ratified, not flagged for integrator carry-over. **(c)** The CSRF design in ADR-342 is replaced: the `CsrfTokenBinder` process-lifetime HMAC key caused a HIGH (SECURITY gate, second pass) — after any restart, every signed-in user was locked out of every write, including sign-in and sign-out, until their session cookie expired, because the key does not survive a restart but sessions live in Postgres for up to 7 days. `CustomerSessionAggregate`/`StaffSessionAggregate` now carry a `CsrfTokenHash` column, set at `Issue()` from a second, independent `ISessionTokenIssuer.Issue()` call (the same mechanism the session token itself uses) and persisted on the session row — durable across a restart exactly like the session it protects. `SessionAuthenticationMiddleware` hashes the `X-CSRF-Token` header and compares it (constant-time) against the stored hash; the `pc_csrf` cookie's own value is never trusted, only read on a safe (GET) request to decide whether to opportunistically re-issue it (`RefreshCustomerSessionCsrfTokenCommand`/`RefreshStaffSessionCsrfTokenCommand`). Both sign-in actions carry a new `[CsrfExempt]` marker so re-authenticating can never itself be blocked by CSRF, even against a stale, still-resolvable session cookie. See `ADR-342`'s rewritten text for the full design and the corrected claims. **Correction (2026-09-25, ARCHITECTURE gate fifth pass, MEDIUM):** `[CsrfExempt]` does more than its name, and more than the paragraph above, say — `SessionAuthenticationMiddleware` skips session resolution **entirely** for a `[CsrfExempt]` endpoint, not only the CSRF check, so both sign-in actions run as anonymous regardless of what either session cookie contains. Consequences: a stale or otherwise-unresolvable staff session cookie no longer triggers the R5 refusal at sign-in, unlike every other staff endpoint; an idle-expired staff session is no longer ended through `EndForIdleTimeout` and audited at the moment a stale cookie is presented to sign-in — that row is made terminal and audited only when it is next presented to a non-exempt endpoint, exactly like an abandoned or cleared cookie already is (ADR-037's "audited once, on detection" is unaffected; only the detection moment moves); the session row a caller replaces by signing in again survives the sign-in unended — unchanged from before this fix, since neither the old nor the new code ever ends the PREVIOUS session on a fresh sign-in, so whoever already holds that token can still use it until it expires on its own. See `ADR-342`'s third amendment, corrected the same way. **Also (2026-09-25):** the `AddSessionCsrfTokenHash` migration (`src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20260925132759_AddSessionCsrfTokenHash.cs`) was generated on this branch under the parallel build plan's rule that this is the only model-changing branch in wave A; the integrator accepted it as-is on 2026-09-25 rather than regenerating it at merge. **(d)** The host-wide CORS policy and `ForwardedHeadersOptions` configuration move out of `AccountsModule` (an area module) into `src/Api/CatEcommerce.Api/Hosting/HostPipelineExtensions.cs`, called once from `Program.cs`'s shared block — both are host-level concerns applied ahead of every area, not an Accounts-owned policy (ARCHITECTURE gate, fourth pass, MEDIUM). The CORS policy is renamed from `AccountsAllowedOrigins` to `HostAllowedOrigins`. **(e)** `Cors:AllowedOrigins: ["http://localhost:3000"]` is added to this worktree's git-ignored `appsettings.Development.json` (the committed `.example` file already had it; the actual file did not, breaking dev CORS for HO-CAT-002's real-API testing). **(f)** The email step-up (`PUT /api/customers/me` when the email is changing) now refuses while the account is locked (before verifying) and calls `RecordFailedLogin` on a mismatch, exactly like C18 deletion, closing an unthrottled password oracle and unbounded-Argon2id DoS (SECURITY gate MEDIUM, second pass); `CustomerProfileController.UpdateProfile` carries `[EnableRateLimiting(...)]` on the whole action (a judgement call: splitting the step-up into its own endpoint was the alternative offered, but was judged unnecessary extra API surface for a still-rare, interactive action). **Correction (2026-09-25, ARCHITECTURE gate fifth pass, MEDIUM):** the rate limit above originally reused `RateLimitPolicies.AccountSensitive`, sharing one IP-partitioned bucket with `POST /me/deletion` and `POST /me/personal-data-export` — five profile saves could then block a genuine deletion attempt for the rest of the window, and customers behind shared NAT or clinic Wi-Fi could block each other. `PUT /api/customers/me` now carries its own `RateLimitPolicies.ProfileUpdate` policy, same 5-per-60-minutes numbers, but partitioned by the signed-in customer id (`ResolvedCaller`, read from `HttpContext.Items`, set by `SessionAuthenticationMiddleware` ahead of `UseRateLimiter()`), falling back to IP only when no session resolves. `AccountSensitive` keeps its IP partition for deletion and export, which need to bound an attacker regardless of which account they are targeting. See the §12.4 row below and the matching Amended row in [`HO-ACC-002`](HO-ACC-002.md) §1. **(g)** A self-service additional phone number that equals another **active** customer's **primary** mobile is now refused (400, `AdditionalPhoneNumberMatchesAnotherCustomer`) in `UpdateCustomerContactDetailsCommandHandler` — a repository check, per ADR-015, checked only for newly-added numbers so a number accepted before this fix does not retroactively block an unrelated edit (SECURITY gate LOW, second pass). This is additional to, and does not change, C15's primary-only refusal in `RegisterWalkInCustomerCommandHandler` (item (16) above). **(h)** The duplicates report (Q-I1, §12.3) gains the same `mobileMatchedOn` label Q-H2 already carries, per member of a group, so staff can see whether a pairing is a genuine shared primary number or only an unverified additional-number claim before merging or attaching cats (SECURITY gate LOW, second pass). **(i)** Integrator authorisation, 2026-09-25: [`HO-ACC-002`](HO-ACC-002.md) §1, [`CONTRACTS.md`](CONTRACTS.md) #22, [`README.md`](README.md) and [`HO-CAT-002`](HO-CAT-002.md) §1/§12.3 were edited on this branch under integrator authorisation, resolving the ARCHITECTURE gate's BLOCKER on editing another handover's file outside §14's normal scope. **2026-09-25 — CODE_REVIEW gate fix (self-amendment, this handover's own build).** (19) Two clock-handling gaps CODE_REVIEW found: **(a) MEDIUM** — `CustomerAccountAggregate.MergeInto` read `DateTimeOffset.UtcNow` directly for `MergedOn`, instead of being threaded in the way every sibling timestamped mutation in this aggregate already is (`Anonymise`'s `AnonymisedOn`, `RecordSuccessfulLogin`/`RecordFailedLogin`'s `asOf`). Fixed: `CustomerMergeDto` gains a `MergedOn` field, `MergeCustomerCommandHandler` now injects `TimeProvider` and passes `timeProvider.GetUtcNow()` through `ToMergeDto`, and `MergeInto` just assigns `dto.MergedOn`. New tests assert the exact value: a domain-level test (`CustomerAccountAggregateTests.MergeInto_SetsMergedOnToTheSuppliedTimestamp`) and a handler-level test with a `FakeTimeProvider` (`MergeCustomerCommandHandlerTests.HandleAsync_Success_SetsMergedOnToTheTimeProvidersTime`). **(b) LOW** — every audit-writing call site (`AuditEntryDto.OccurredOn`) that read `DateTimeOffset.UtcNow` directly, rather than through `TimeProvider`, was switched to `TimeProvider.GetUtcNow()`: 16 `DomainEventHandlers` (13 under `Account`, 3 under `Cat`), `AuditChangeCaptureInterceptor` (constructed in `Program.cs`, which now also resolves `TimeProvider` there), `RecordAuthorizationDenialCommandHandler`, and the two audit-writing helper methods each in `SignInCustomerCommandHandler`/`SignInStaffCommandHandler` (both of which already inject `TimeProvider` for their main flow — this was a case of using it inconsistently within the same class, not adding a new dependency). Chosen over recording an exception because it was genuinely mechanical: every one of the 20 files already had, or could trivially take, a constructor-injected `TimeProvider` (already a registered singleton, `AccountsModule.cs`), and no test in the suite constructs any of the 16 `DomainEventHandlers` or the interceptor directly (only through the full DI-resolved pipeline), so the change carried no test-breakage risk. Three handlers in this same audit surface (`AuditCustomerSignedInHandler`, `AuditStaffSignedInHandler`, `AuditStaffSessionIdleExpiredHandler`) already derived `OccurredOn` from the triggering domain event's own field (`@event.OccurredOn`/`@event.DetectedOn`) rather than a fresh clock read — left unchanged, since that is arguably more correct (the exact moment the underlying business event happened, not the moment its audit row happened to be written) and was never the finding. **2026-09-25 — ACCEPTANCE gate fix, first pass (self-amendment, this handover's own build), owner-ratified same day.** (20) **(a) HIGH, A28 not met.** `AuditChangeCaptureInterceptor` recorded the wrong before-value for every insert and every change to an owned value object or owned collection. Two defects: it read only `Added`/`Modified` change-tracker entries, so a `Deleted` owned entry (the OLD half of a wholesale-replaced value object such as `LoginThrottle`, `EmailAddress`, `MobileNumber`, `PasswordCredential`, or a removed address) never reached either snapshot; and it read `CurrentValue` for an `Added` entry even when building "before", so an inserted root's own "before" showed its just-inserted state, and a newly-added owned entry (the NEW half of a replace, or a newly-added collection member) appeared in "before" too. Observed live: a failed sign-in's `LoginThrottle.FailedCount` showed `1 → 1`; setting or changing an email showed the new address in "before"; an added or removed delivery address showed the same address on both sides, or on neither. Fixed: `Capture` now also collects `Deleted` entries, excludes `Added` owned entries from the before-snapshot (and skips building one at all for an `Added` root), and excludes `Deleted` owned entries from the after-snapshot. `U-A8` (`AuditImmutabilityTests.cs`) is strengthened from "a `DataChange` row exists" to explicit before/after content assertions covering: registration (before `null`, after populated) and the same for a cat, with the request's IP address asserted on both via a new `AccountsApiFactory.WithFixedRemoteIpAddress` test host (TestServer supplies no remote address otherwise, so no earlier test could observe a non-null audit IP); a modified `CatAggregate` field; an email set from `null` and an email change old → new; an address added and an address removed; a failed sign-in's `LoginThrottle.FailedCount` `0 → 1`; `xmin` absent from every snapshot; secrets redacted. Two further `WithFixedRemoteIpAddress` tests cover I-A7: the audit IP reflects a forwarded address only when the immediate peer is a configured trusted proxy, and is the raw peer address otherwise. Every new content assertion was proven to fail against the pre-fix interceptor by temporarily reverting it, running this file, and restoring the fix — not committed in that state. **(b) OWNER DECISION, 2026-09-25 — personal data in `DataChange` before/after values.** Fixing (a) means every changed record's prior email, mobile number and address now appears in full in the row that changed it, and stays there forever in a table the database itself forbids editing (§11.9's immutability trigger) or the anonymisation exemption does not reach (anonymisation only nulls its *own* row's before-value, per §11.9 step 4 — every earlier row for that customer keeps whatever personal data it already recorded). The owner chose to record these values **in full**, matching FR-ACC-12 as written, rather than mask them the way §11.8 masks sign-in identifiers — masking a `DataChange` row would remove exactly the information §11.9 exists to keep (what the value used to be). This is a real, acknowledged tension with D-11 (the right to erasure): **carried condition** — HO-XCT-001's `IPersonalDataEraser` must account for audit rows once it is built, since anonymisation alone does not remove this personal data from the audit trail. **(c) OWNER DECISION, 2026-09-25 — the `InternalsVisibleTo` in `CatEcommerce.Api.csproj`.** `bfa3ce6` added `<InternalsVisibleTo Include="CatEcommerce.IntegrationTests" />` to let `SessionAuthenticationMiddlewareTests` unit-test `ShouldRefuseUnsafeRequest` directly; the csproj is not in §13's list (only `Infrastructure.csproj` and `tests/*/**.csproj` are named there). The owner authorised this edit; recorded here as the Amended row the ACCEPTANCE gate's MEDIUM finding asked for. **2026-09-26 — integrator (`ho/integrator-host`), after merge.** (21) Closes this handover's own carried item: `PUT /api/customers/me` with a NUL byte in `fullName` previously returned `500`. HO-ADM-001's global `NulByteRejectionFilter` (its §1 Amended (32)) now intercepts it first and returns `400 ContainsControlCharacter`; confirmed live by a new test, `ProfileEndpointTests.UpdateProfile_FullNameWithNulByte_Returns400`. Carried item dropped. Also: `PagingRules` (`Shared.Common.Constants`, added by HO-ADM-001) is now applied to `GET /api/staff-accounts` and `GET /api/staff/customers/duplicates`, capping `pageNumber`/`pageSize` the same way `AdminLeaveRequestsController` does — `pageNumber`/`pageSize` at or near `int.MaxValue` previously reached `Query<T>.Skip` unchecked, overflowing `int` to a negative offset and a `500`; both endpoints now return `400`. Finally, `UniqueConstraintExceptionFilter`'s registration moves from `AccountsModule` to `Program.cs`'s own `AddControllers(...)` call, alongside `ConcurrencyExceptionFilter` and `NulByteRejectionFilter` — it is no longer Accounts-only, since HO-ADM-001 extended it to map several of its own areas' unique-constraint violations too. See HO-ADM-001 §1 Amended (50). **2026-09-26 — HO-ACC-002 first-pass gate fix round, SECURITY gate item 7, owner decision 2026-09-26 (integrator-authorised edit).** (22) `AccountOptions` gains `CsrfCookieDomain` (empty by default — host-only, unchanged behaviour for local development and any environment that does not set it). `SessionCookieWriter.WriteCsrf`/`ExpireCsrf` now set the `pc_csrf` cookie's `Domain` from it; `pc_session`/`pc_staff_session` stay host-only always, since only the CSRF cookie needs cross-subdomain readability (it is deliberately not `HttpOnly` — the frontend reads it via `document.cookie` to echo it into `X-CSRF-Token`, a need the two session cookies never have). `CustomerProfileController.DeleteAccount` now calls `SessionCookieWriter.ExpireCustomerSession`/`ExpireCsrf` instead of a bare `HttpContext.Response.Cookies.Delete(name)`, which never carried this `Domain` and so would never actually have removed a domain-scoped `pc_csrf` once a production deployment set one. See `tests/CatEcommerce.IntegrationTests/Endpoints/CsrfCookieDomainTests.cs` and the matching Amended row in [`HO-ACC-002`](HO-ACC-002.md) §1. **2026-09-26 — HO-ACC-002 first-pass gate fix round, CR MEDIUM 2 (item 13), integrator-authorised edit.** (23) §11.12/§11.13's prose already asked for `recordStatus`/`registrationChannel` per duplicate-group member and a per-cat list on the fresh single-customer read; the actual read models fell short of both, in opposite directions -- `DuplicateCustomerMemberViewModel` (Q-I1) already listed cats by name but carried neither `RecordStatus` nor `RegistrationChannel`, while `StaffCustomerViewModel` (Q-H1) already carried both but reported only `CatCount`, not the cats themselves. Closed by extending both: `DuplicateCustomerMemberViewModel` gains `RecordStatus`/`RegistrationChannel` (`GetDuplicateCustomerGroupsQueryHandler`'s existing per-member projection now also selects `PasswordCredential != null`/`RegistrationChannel`, using the same `CustomerRecordStatusRules.Derive` helper `GetStaffCustomerQueryHandler` already uses); `StaffCustomerViewModel` gains `Cats` (reusing `DuplicateCustomerCatViewModel`, the shape being generic despite its name), alongside the unchanged `CatCount` for any other consumer already reading it. Both are read-side projection changes only -- no entity, aggregate or migration changes (`dotnet ef migrations has-pending-model-changes` confirmed clean before and after). New integration tests: `DuplicateCustomerGroupsEndpointTests` (two, covering a self-registered pair and a walk-in/self-registered pair, proving the fields are genuinely per-member, not constant) and `WalkInEndpointTests.GetStaffCustomer_ReportsEveryOwnedCatByNameAlongsideCatCount`. See the matching Amended row in [`HO-ACC-002`](HO-ACC-002.md) §1 for the frontend half. **2026-09-26 — HO-ACC-002 second-pass gate fix round, SECURITY gate optional hardening (not a finding), integrator-authorised edit.** (24) `AccountOptions.CsrfCookieDomain` ((22) above) had no validation, so an operator typo would only surface once real cross-subdomain traffic started failing with a `403` in production. A `[RegularExpression]` attribute now enforces it at startup instead, through the same `ValidateDataAnnotations().ValidateOnStart()` pipeline `StaffIdleTimeoutMinutes`'s `[Range]` already uses: empty (host-only, the default) or a bare hostname, an optional leading dot, one or more dot-separated alphanumeric-and-hyphen labels — no scheme, port, path or whitespace, which is what an accidental copy-paste of a full origin would produce. `CsrfCookieDomainTests.cs` gains 7 invalid-value cases (scheme, port, path, three whitespace positions, an empty label), each asserting host startup itself throws, and 4 valid-value cases (including the empty default and a bare hostname with no leading dot), each asserting it does not. **Correction (2026-09-27, ARCHITECTURE gate fourth pass, LOW): this row's own count is stale.** The `[RegularExpression]` was tightened again on `81cf928`'s branch (CODE_REVIEW gate third pass, LOW, recorded only in `AccountOptions.cs`'s own doc comment, not here) to also reject an IP literal (a dotted-quad, with or without the conventional leading dot, and three IPv6 forms), bringing `CsrfCookieDomainTests.cs` to 13 invalid-value cases in total, not 7 -- corrected here rather than left silently wrong. **2026-09-27 — HO-ACC-002 fourth-pass gate fix round, CODE_REVIEW LOW, integrator-authorised edit.** (25) The IP-literal rejection above (13 cases) still missed several browser-recognised alternate-base/shorthand IPv4 forms that are not four-part decimal dotted-quads at all: octal (`0177.0.0.1`), hex (`0x7f.0.0.1`, or a single bare label `0x7f000001`), shorthand (`127.1`, `127.0.1`) and a bare 32-bit integer (`2130706433`) -- none of these matched the existing `(\d{1,3}\.){3}\d{1,3}` lookahead, so all six passed startup validation despite being real loopback-address literals a browser or IPv4 parser would still recognise. Rather than enumerate every alternate-base form (there is no finite list), the pattern now rejects whenever the value's LAST dot-separated label (or the whole value, when it has none) is all-digits or begins with `0x`/`0X` -- a real DNS TLD is never either, so this catches every form above (each one's final label is a plain decimal octet, or, for the single-label bare-hex form, the whole value itself begins with `0x`) while leaving a digit- or `0x`-prefixed NON-final label (e.g. `0x123.example.test`, already valid hostname shapes) untouched, since only the last label is inspected. `CsrfCookieDomainTests.cs` gains the 6 new invalid-value cases above (now 19 invalid-value cases in total) and one new valid-value case (`0x123.example.test`, proving a non-final `0x`-prefixed label is unaffected; now 5 valid-value cases in total). **2026-09-27 — CODE_REVIEW gate fifth pass, LOW (completeness, not a gap), self-amendment (this handover's own build).** `CsrfCookieDomainTests.cs` gains one more invalid-value case, `[::1]` — the bracketed form a URL authority component (e.g. `https://[::1]/`) would use for the same IPv6 loopback the existing bare `::1` case already covers; the base pattern's label character class (`[a-zA-Z0-9-]`) has no `[`/`]`/`:` either way, so this was already rejected before this test existed, purely a completeness addition (now 20 invalid-value cases in total). *(append further rows here)* <br><br>**2026-09-27 — HO-NTF-001 gate fixes, self-amendment on that handover's own build.** (51) Rows (4)/(5)/(7) above are corrected: `AccountsModule.cs`'s two dispatcher registrations (`DevelopmentPasswordResetTokenDispatcher`, `UndeliveredPasswordResetTokenDispatcher`) are deleted outright, not merely superseded — `NotificationPasswordResetTokenDispatcher` (`NotificationsModule.cs`) has been the only `IPasswordResetTokenDispatcher` registration since; the stale "registered a second time … wins" comment in both modules is corrected to say so. `AccountOptions.WebBaseUrl` (row (7)) is removed with no dangling reference — its replacement, `Notifications:WebBaseUrl`, is itself now removed in favour of the shared `PublicSiteOptions.WebBaseUrl` (`CatProfilesModule.cs`, HO-CAT-001-gates), used for both the cage-card QR code and every notification link. (52) Four HO-ACC-001 files edited outside HO-NTF-001's own §13, each minimal and already authorised in substance by this document's own row (5)/(7) and HO-NTF-001 §12.4/§12.7: `CustomerProfileController.cs` and `ChangeNotificationPreferencesRequest.cs` gain `preferredChannel` on `GET`/`PUT` `/api/customers/me/notification-preferences` (HO-NTF-001 §8.5); `ChangeNotificationPreferencesCommandValidator.cs` allows `Changes` to be empty when `PreferredChannel` alone is supplied — `ChangeNotificationPreferencesRequest.Changes` therefore loses `required` (the one non-additive contract change; HO-ACC-002 should account for an empty-`Changes`, `preferredChannel`-only request now being valid). `UniqueConstraintExceptionFilter.cs` gains `ix_notification_template_key` → `409`, the same per-area mapping-line extension HO-ADM-001's own rows already use. (53) `UseRateLimiter()` and the shared 429 response shape (`OnRejected`) move out of `AccountsModule.AddAccountsModule`/`UseAccountsModule` into `HostPipelineExtensions.AddHostPipelineServices`/`Program.cs`'s shared block — carried from HO-ADM-001-ARCHITECTURE MEDIUM 1 ("`UseRateLimiter` lives in AccountsModule"), integrator-authorised, now that Notifications is a second area contributing its own named rate-limit policies (`AccountsModule`'s own `AddRateLimiter` call keeps only its own policies; composition through `Configure<RateLimiterOptions>` is unaffected by call order). `.ai/host-composition.md` updated. Full detail: `docs/gates/HO-NTF-001-{SECURITY,ARCHITECTURE,CODE_REVIEW}.md` and HO-NTF-001 §1 Amended's own 2026-09-27 row. <br><br>**2026-10-07 — owed row from [`HO-EMR-001`](HO-EMR-001.md) (OQ-17, §13's note), recorded by the integrator at that handover's merge (`d3527166`); an append, no existing behaviour changes.** (54) **`AuditCategory.ClinicalRecordAccess`** (value 4) is added; HO-EMR-001's `ClinicalRecordAccessFilter` writes one such audit row per clinical-record read, before the data is returned, failing closed (NFR-SEC-8, ADR-129). Its entity types are registered in `ClinicalAuditEntityTypes.Consultation.cs`. (55) **One audit-capture exclusion:** `ConsultationDraftAggregate` is appended to `AuditCaptureExclusions` (the auto-saved working copy is overwritten every few seconds and is not the record). `AuditChangeCaptureInterceptor` itself is **not** edited; two limits of its existing snapshot logic were exposed by clinical records and are recorded in `docs/backlog/EMR.md` for an owner decision: owned rows of one type collapse to the last row, and nested owned rows (`diagnostic_result_value` under a result set) are not captured. (56) `UniqueConstraintExceptionFilter` also maps the `ix_visit_appointment` and `ix_consultation_draft_visit_author` races (HO-EMR-001 §12.5). <br><br>**2026-10-07 — owed rows from [`HO-IMP-001`](HO-IMP-001.md) (§1 "Amendments this slice needs elsewhere", §12.5, ADR-098), recorded by the integrator at that handover's merge (`9d2346f0`) after checking each against `src/`; appends only, no existing behaviour changes.** (57) `UniqueConstraintExceptionFilter` maps Inventory's named constraints. Unique indexes answer `400` with the handler's own pre-check code: `ix_stock_location_branch_code`, `ix_stock_location_one_quarantine`, `ix_supplier_code`, `ix_stock_count_one_active` (`CountLocationBusy`) and `ix_batch_receipt_lot`. Named `23514` refusals answer `409`: `ck_stock_position_balance` → `InsufficientStock`, `tg_stock_batch_expired` → `BatchExpired`, `tg_stock_batch_not_usable` → `BatchNotUsable`. A first-write race on `product_storage_profile` (table-name match, `409`) is one mapping beyond §12.5's list. The immutability and position-guard triggers are deliberately unmapped (reaching them is a bug, so a `500` is right). The alert-dedup indexes never reach the filter: `EfInventoryAlertWriter` treats a violation of `ix_stock_alert_dedup_key` or `ix_stock_alert_one_live_per_pair` as "already raised". (58) Change capture (§11.9 step 7) excludes `StockLedgerEntryAggregate` and `StockReservationEntryAggregate` through `AuditCaptureExclusions` (HO-IMP-001 ADR-098: each row is already an attributable record). <br><br>**2026-10-07 — owed rows from [`HO-BIL-001`](HO-BIL-001.md) (§1 "Amendments this slice makes to other handovers", §11.6, §12.3, §12.5), recorded by the integrator at that handover's merge (`0442f9ff`) after checking each against `src/`; appends only, no existing behaviour changes.** (59) **`ActionRoles.Billing.cs` appends 15 rows** (the actions are in `Actions.Billing.cs`), matching HO-BIL-001 §12.3 and Appendix D: `Billing.View`, `Billing.ManageCharges`, `Billing.DraftInvoices`, `Billing.IssueInvoices`, `Payments.Record` and `Payments.Allocate` for Reception, ShopManager, Accountant and Administrator; `Payments.Approve`, `Payments.Reverse`, `CashUp.Close`, `Reconciliation.Manage` and `AccountingExport.Run` for Accountant and Administrator only; `CreditNotes.Issue` and `Refunds.Issue` for ShopManager, Accountant and Administrator; `CashUp.Count` for Reception, Accountant and Administrator; `BillingProfile.Manage` for Administrator only. The customer portal reads carry no `ActionRoles` row (they are `[RequireCustomerSession]` reads of the caller's own documents). (60) **`UniqueConstraintExceptionFilter` maps Billing's named constraints** (messages from `BillingMessages`, shared with each handler's pre-check): `ix_charge_source` → `409 ChargeAlreadyRecorded`; `ix_invoice_line_charge` → `409 ChargeAlreadyInvoiced`; `ix_bank_statement_source` → `400 StatementDuplicate`; `ix_bank_match_line` → `409 BankLineAlreadyMatched`; `ix_bank_match_payment` → `409 PaymentAlreadyMatched`; and a first-write race on `billing_profile` or `loyalty_account` (table-name match, `409`), one mapping beyond §12.5's list (`cash_up` is missing from it, HO-BIL-001 backlog row 17). **Deliberately unmapped:** `ix_invoice_number` / `ix_invoice_sequence` (a numbering bug must be loud) and the Billing trigger violations (`P0001`, a code defect). **The `ix_*_idempotency` indexes are not answered by this filter,** unlike §12.5's text: a separate global exception filter, `BillingIdempotencyReplayFilter`, registered from `BillingModule` after this one with equal order, sees the exception first and answers with the original result (HO-BIL-001 §1 Amended, 2026-10-07, deviation (d)). (61) **Billing subscribes to `CustomerMergedEvent`** (`ReassignBillingOnCustomerMergedHandler`, ADR-192): the duplicate's unbilled active charges and draft invoices move to the survivor, its loyalty balance moves as a `MergeTransferOut` / `MergeTransferIn` pair committed in one save, and issued invoices, payments, credit notes and refunds keep the original customer id (rewriting an issued document would break ADR-180) while customer-facing and summary reads fold in the merged customer. The handler never enqueues a message. (62) `BillingPersonalDataEraser` (C-ALL-2) reports `Held` under D-9 and mutates nothing; no `AuditCategory`, `SystemActors` entry or audit-capture exclusion is added by Billing. <br><br>**2026-10-13 — Amended (integrator, HO-XCT-001 §5.3); ratifies edits already made on `ho/XCT-001-build`, recorded after the fact as the integrator's OQ-1 duty (ACCEPTANCE gate `docs/gates/HO-XCT-001-ACCEPTANCE.md`, BLOCKED on §19/§20.2).** Appends only; no existing behaviour changes. (63) **§11.9 step 7 — change-capture exclusions:** `RetentionRunAggregate` (`ops.retention_run`) and `WebVitalBatchAggregate` (`ops.web_vital_batch`, with its owned samples) are appended to `AuditCaptureExclusions` (`src/Infrastructure/CatEcommerce.Infrastructure/Auditing/AuditCaptureExclusions.cs`). Why (HO-XCT-001 §5.3): a retention run is itself an audit-grade log, summarised by one `BusinessEvent` row per run (ADR-303), and a web-vital batch is anonymous telemetry that must not write one audit row per page view (ADR-309) — the same mechanism as HO-NTF-001 ADR-066. `AuditChangeCaptureInterceptor` itself is not edited. (64) **§12.4 / §12.9 — a second exception-filter entry:** `UniqueConstraintExceptionFilter` (`src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs`, `TryMapRecordIsRetained`) answers `409 RecordIsRetained` for `ProtectedRecordDeletionException` (raised by HO-XCT-001's `HardDeleteGuardInterceptor` before any SQL) and for a PostgreSQL `P0001` whose message starts "Hard delete of" (HO-XCT-001's retention trigger), per HO-XCT-001 §12.5 and ADR-299. Why: HO-XCT-001 §5.3. Both edits were made by the HO-XCT-001 build before this row existed (its §20.2 stop was passed without a report); each matches the §5.3 text exactly, lies inside HO-XCT-001 §13, carries a code comment citing it, and was reviewed as append-only by ARCHITECTURE. The integrator ratifies them as made; nothing is to be rebuilt.<br><br>**2026-10-13 — Amended (integrator, HO-RPT-001 §11.7/§10): owed rows from [`HO-RPT-001`](HO-RPT-001.md) (§8.8, §12.3, OQ-15), recorded by the integrator before that handover's Phase A; appends only, no existing behaviour changes.** (63) **Ten actions are appended to the one `ActionRoles` table** (own partial, per-area convention): `Reports.ClinicOperations.View` for Reception, Vet and Administrator; `Reports.Clinical.View` for Vet and Administrator; `Reports.Boarding.View` for Caretaker and Administrator; `Reports.Stock.View` for Inventory and Administrator; `Reports.Sales.View` for ShopManager and Administrator; `Reports.Financial.View` for Accountant and Administrator; `Reports.Growth.View` for Administrator; `Reports.Any` (marker on every `/api/staff/reports/**` endpoint) for every staff role except Nurse; `Reports.Schedule` for Administrator; `Dashboard.View` for all eight staff roles. The report's own category action is enforced inside the query handler with the same `403 ActionNotPermitted` body, and the denial is audited through the existing denial-audit command. (64) The export audit entry `ReportExported` is written through the existing audit factory as a `BusinessEvent` for entity type `Report`; no `AuditCategory` and no capture exclusion is added, and the two Reporting roots (schedule and run) are captured like any other aggregate, with the stored export file kept out of the audit table. <br><br>**2026-10-13 — owner decision (HO-RPT-001 gate fix round).** (65) `Reports.Sales.View` is held by ShopManager, **Accountant** and Administrator (it was ShopManager and Administrator in (63)); the Accountant therefore opens R10, R11 and R27 and can receive R10 on a schedule. The cost and margin columns still need `Reports.Financial.View`. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade, see §7 |

---

## 2. Objective

Deliver the **Accounts and Identity** bounded context end to end on the server: who a customer is,
who a staff member is, how each of them signs in, what they may do, and the audit trail that
records it. Every other business line hangs off this — `CatAggregate.OwnerId` has been an
unvalidated `Guid` since HO-CAT-001 because there was no one to point at, and bookings, orders and
invoices all need a customer and an authorised staff member.

The owner's instructions of 2026-09-22 shape the whole slice:

- **No phone, email or OTP verification of any kind.** A customer registers with a mobile number
  and/or an email plus a password and can use the account at once.
- **Sign-in is identifier + password only**, for customers and staff alike. No OTP login, no
  two-factor authentication.
- **Registration is protected by a CAPTCHA** against automated and denial-of-service abuse.
- **Password reset stays** — it is account recovery, not verification.

Removing verification removes the thing that proved a registrant was a real person. That moves the
weight of the security work onto four controls, and **most of this document is about getting them
right**: the CAPTCHA and per-IP rate limits (§12.6, §12.7), the memory-hard password hash and
progressive lockout (§8, §10.2), non-disclosing responses (ADR-028), and an audit trail that is
written in the same transaction as the change and cannot be edited (§11.9, ADR-040).

This slice is large — eleven stories, seven aggregates. §17 builds it in **ten phases**, each of
which compiles, migrates and passes its own tests before the next begins. A phase is not a separate
handover; the gates judge the whole.

---

## 3. In scope

### 3.1 Stories

All eleven active stories in backlog §1. **§18 holds the testable acceptance criteria.**

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-ACC-01 | M | 5 | Self-service registration with name, mobile and/or email, password; CAPTCHA; per-IP limit; duplicate-identifier refusal that discloses nothing about the *other* identifier; password length and breach checks | B |
| US-ACC-04 | M | 3 | Customer sign-in with mobile or email + password; one generic failure; progressive lockout; every attempt audited; session cookie; sign-out | C |
| US-ACC-05 | M | 2 | Password reset for customers **and staff** — neutral request response, single-use time-limited token, all sessions invalidated on success | D |
| US-ACC-06 | M | 5 | Profile (name, email, extra phone numbers), up to 10 labelled addresses with exactly one default, per-channel marketing consent with transactional always on | E |
| US-ACC-07 | M | 5 | Staff accounts with 1…8 roles from SRS Appendix D; the role catalogue endpoint; server-side authorization that fails closed; every denial audited | F |
| US-ACC-14 | M | 3 | Staff sign-in with mobile or email + password; staff session carrying roles; first password set through the reset flow; a customer credential never yields a staff session | G |
| US-ACC-08 | M | 2 | Staff session idle expiry (default 30 min, configurable), terminal once expired, refused requests have no side effect, passive status and activity heartbeat | G |
| US-ACC-10 | M | 3 | Receptionist creates a walk-in customer with name and phone; derived *incomplete* status; the walk-in sets a password through reset | H |
| US-ACC-11 | S | 8 | Duplicates report; administrator merge that retires the duplicate and moves its cats; audited; re-send is the recovery | I |
| US-ACC-12 | M | 5 | IP, category and correlation on every audit entry; same-transaction change capture for every aggregate; audit entries immutable in the application **and** the database | A |
| US-ACC-13 | S | 8 | Customer self-service personal-data export and account deletion by in-place anonymisation, with step-up password confirmation | J |

### 3.2 Foundation added by this slice

| Foundation item | Specified in |
|---|---|
| The **`Account`** bounded context — folder and namespace segment `Account`, schema `account` | §8.1, ADR-024 |
| Identifier value objects `MobileNumber`, `EmailAddress`; `PasswordCredential`; `LoginThrottle` | §8.2 |
| Security ports and their implementations: hasher, breach checker, session token issuer, reset token generator, reset dispatcher, client IP accessor | §8.10, §12.2, ADR-025 |
| Session cookies, CSRF check, and caller resolution into `UserContext` — replaces `DevUserContextProvider` as the source of identity | §12.5, ADR-031, ADR-032 |
| The authorization filter and the solution-wide action-to-role table | §12.8, ADR-036 |
| CAPTCHA and the rate limiter | §12.6, §12.7, ADR-029, ADR-030 |
| Audit trail integrity: change-capture interceptor, immutability interceptor, database triggers, trusted-proxy IP | §11.9, §12.1, ADR-040 |
| `UniqueConstraintExceptionFilter` — `23505` → `400`, not `500` | §12.4 |

### 3.3 Decisions already settled upstream — do not re-litigate

| Decision | Where it was taken |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only | ADR-010, ADR-013, ADR-019, ADR-020 |
| PostgreSQL via EF Core, one `DbContext`; `xmin` optimistic concurrency → `409` | ADR-011, ADR-016 |
| Handlers load aggregates directly; no `CommandService` layer | ADR-015 |
| Commands are synchronous and return their real result; never `202` | ADR-014 |
| `SaveChangesAsync` fires per repository call | ADR-021 |
| `Enumeration` is defined in `Shared` | ADR-022 |
| Authorization is an action-to-role table checked server-side, not `[Authorize(Roles=…)]` | ADR-009 (carried into this solution by ADR-036) |

---

## 4. Out of scope

Building anything in this section is a **FAIL** at the acceptance gate — it means scope was
expanded without approval.

### 4.1 Frontend — all of it

This slice is **backend only**. The screens for these same eleven stories are
[`HO-ACC-002`](HO-ACC-002.md). Acceptance criteria phrased as user experience are satisfied **at
the API contract level** here — §18 states each one in the form this slice can be judged on.

| Story AC (UI phrasing) | UI half — HO-ACC-002 | Server half — delivered here |
|---|---|---|
| US-ACC-01 "offered login or password reset" | The two links | The `errorMessage` names both options. No URL is built server-side |
| US-ACC-01 "a fresh CAPTCHA is presented" | Refetching and rendering the image | `POST /api/captcha` can be called at any time; a challenge is single-use |
| US-ACC-01 / 04 / 05 "when I may retry" | The message and countdown | `429` with `Retry-After` |
| US-ACC-04 / 14 "my dashboard / the back office loads" | Navigation after sign-in | `200` + the session cookie; `GET` session returns who is signed in |
| US-ACC-08 AC2 "unsaved form data is preserved" | Holding the draft in memory across re-authentication | A refused request **had no side effect**; the refusal is distinct and carries no identity; the status endpoint returns `staffAccountId` so the draft is restored only to the same person |
| US-ACC-11 "both are listed with their cats, orders and balances" | Rendering; "not yet recorded" for orders and balances | Cats per record; **no orders or balances exist in any context** (§4.3) |

### 4.2 Withdrawn — never build

| Capability | Why |
|---|---|
| Mobile OTP verification, email verification links, any `IsVerified`/`VerificationStatus` field | FR-ACC-2 withdrawn (SRS v2.1); US-ACC-02/03 withdrawn |
| OTP login, an OTP table, an SMS code sender | FR-ACC-3 reworded (SRS v2.2) |
| Two-factor authentication, TOTP, recovery codes | FR-ACC-8 withdrawn (SRS v2.2); US-ACC-09 withdrawn |

### 4.3 Not buildable yet — named gaps, not silent omissions

| Gap | Why | Consequence for this slice |
|---|---|---|
| **Delivering** a reset token by SMS or email | No Notification context (SRS §4.12), no outbox | `IPasswordResetTokenDispatcher` has a **Development-only** implementation that logs the reset link, and a non-Development implementation that records the request and delivers nothing. **Password reset — and therefore a walk-in's or a new staff member's first password — does not work outside Development until the Notification context lands.** §20.4 |
| Orders, invoices, visits, credits, balances | Those contexts do not exist | The merge moves **cats**; `CustomerMergedEvent` is the contract later contexts subscribe to (ADR-039). The duplicates report returns cats only |
| Audit log query / export screen | FR-ADM-6 belongs to Administration | Entries are **written**; no read endpoint |
| Guarding the Cat Profile endpoints with roles | They are HO-CAT-001's; changing their authorization changes that shipped slice's behaviour | They stay unguarded. `[RequireAction]` is applied to this slice's staff endpoints only. **Name it in the SECURITY gate report.** A follow-up amendment to HO-CAT-001 applies it |
| `CatAggregate.OwnerId` → customer foreign key | Changes a shipped aggregate's table | Stays an unvalidated `Guid`. The merge's one Cat method (§8.9) is the only Cat change |
| Administrator-editable roles and permissions | US-ADM-02 | The role catalogue is code-defined (ADR-036) |
| Staff account deactivation, branch assignment, licence number | No story | Not modelled |
| Proactive sweep of abandoned sessions | Needs an `IHostedService`; no AC requires it | Idle and absolute expiry are derived at the next presentation (ADR-037) |
| Distributed CAPTCHA / rate-limit state | Single host (ADR-010) | In process memory; a restart resets both |
| Changing the primary login identifier from the profile | Needs proof of possession of the new identifier, which no longer exists as a mechanism | The profile edits the email only when the account also has a mobile number, and never the mobile (§8.4). OQ-5 |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, `IEventPublisher`, `IDispatcher`, MassTransit, RabbitMQ, MongoDB, a
second host, an `Application/CommandServices` layer, a rejection event, or a `202 Accepted`.

Specific to this slice:

- **No ASP.NET Core Identity**, no `IdentityDbContext`, no `UserManager`/`SignInManager`, no
  `AddAuthentication`/`AddAuthorization` scheme, no JWT package, no `[Authorize]`. Identity is this
  context's own aggregates (ADR-024); authorization is `[RequireAction]` (ADR-036).
- **No third-party CAPTCHA** — no reCAPTCHA, hCaptcha, Turnstile or outbound verification call.
- **No `AspNetCoreRateLimit`** or other limiter package — the framework limiter is built in.
- **No plaintext password, token or CAPTCHA answer** in any column, log line, event, audit value,
  ViewModel, response body or exception message. §20.2.
- **No endpoint that returns a password hash, a token hash or a security stamp** in any shape.

---

## 5. Prerequisites

| Required | State |
|---|---|
| [`HO-CAT-001`](HO-CAT-001.md) merged — `Enumeration`, `BusinessViolationCodes`, `CatEcommerceDbContext`, `EfAggregateRootRepository<T>`, `DevUserContextProvider`, `AuditEntryAggregate`, `ConcurrencyExceptionFilter`, `Program.cs`, the initial migration | ✅ present at `4007225` |
| `Platform.Infrastructure.Core` 1.0.34 | ✅ on the feed, referenced by all six projects |
| `dotnet-ef` 9.0.0 | ✅ pinned in `.config/dotnet-tools.json`. Invoke as `dotnet dotnet-ef` |
| PostgreSQL 17.5 at `localhost:5432`, database `catecommerce_dev` | ✅ used by dev **and** the integration suite (HO-CAT-001 §5.1) |
| `appsettings.Development.json` present locally and git-ignored | ✅ HO-CAT-001 §17.1 / §20.2 — **inherit the rule** |
| `Konscious.Security.Cryptography.Argon2` on the configured feed | ⚠️ **unverified** — restore in Phase A step 1. If it does not resolve, **stop** (§20.1) |
| `SkiaSharp` + `SkiaSharp.NativeAssets.Linux.NoDependencies` on the configured feed | ⚠️ **unverified** — restore in Phase A step 1. If either does not resolve, **stop** (§20.13) |
| `Microsoft.Extensions.TimeProvider.Testing` (test projects only) | ⚠️ **unverified** — restore with the others |

### 5.1 One database, used for everything — unchanged

Development and the integration tests share `catecommerce_dev`. **It is not empty and not yours
alone.** The rules in §16.1 apply, and two of them bind harder than in HO-CAT-001 because mobile
number and email are **unique keys**.

---

## 6. Governing architecture documents

Where any of these disagrees with a `.ai/templates/*.cs` file, **the markdown wins**.

| Document | What you need from it |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer responsibilities and the project-reference direction that forces the security ports into `Domain` (ADR-025); §6 the verified platform surface; §6.2 the two rules that fail silently |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | Why `IInMemoryDispatcher` and nothing else — and why a plaintext password on a command is safe **only** because dispatch is in-process |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Rule 1 "one aggregate mutated per command" — two recorded exceptions (ADR-033, ADR-040); rule 2 aggregates reference by id; **rule 3 decides that sessions and reset tokens are their own roots** |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | Every namespace in §11/§12. `Account` is a new `[Feature]` segment (ADR-024). Do not invent others |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 are binding.** ADR-009 (action-to-role table), ADR-014, ADR-015, ADR-016, ADR-021 are the ones this slice turns on |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §3 handler shape; §4 the uniqueness pre-check; **§6 the rule-placement table** |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | `AsNoTracking()`; project into the ViewModel before materialising; a query never writes |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §2 the repository (**already implemented — do not re-copy**); §4 owned mapping; §6 `xmin`; §8 column types |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §2 runtime-type dispatch; **§3 a failing event handler fails the command after the aggregate committed** — the reason data-change audit moves into the transaction (ADR-040); §5 event vs outbox |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | The twelve enumerations in §10.1 — base class is ours, in `Shared` |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | §3 register validators as `IValidator<T>`; §4 `ValidateScopes`/`ValidateOnBuild` stay on |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs` shape; §3 lifetimes; §5 the never-register list |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File order within each phase |
| [`HO-CAT-001`](HO-CAT-001.md) | §7 platform facts; §8.4 `AuditEntryAggregate`; §12.1 EF configuration rules; §16.1 shared-database rules. **The reference implementation — match it** |

---

## 7. Verified platform facts

F1–F10 were established by HO-CAT-001 §7. F11–F18 were established by reading the code at
`4007225`. **Do not re-derive these.**

| # | Fact | Consequence for your code |
|---|---|---|
| F1 | `AddInMemoryBusServices()` registers exactly `IResolver`, `IHandlerResolver`, `IInMemoryDispatcher`, `ICommandSender`, `IQueryProcessor`, `IValidationService`, `IValidationProvider` | Never hand-register any of them |
| F2 | `IInMemoryDispatcher` has **no** `PublishAsync` | Commands and queries only |
| F3 | **`Command.ValidateCommand` is `bool?`**, and validation is skipped when it is null | **Stamp `ValidateCommand = true` in every controller action and in every filter that dispatches.** Otherwise a 4-character password reaches the aggregate |
| F4 | Generic `PublishAsync<TEvent>` binds `TEvent` from the static type | Already handled in `EfAggregateRootRepository`. Do not re-implement dispatch |
| F5 | `Enumeration` ships in no platform package; ours is in `Shared` (ADR-022) | Every enumeration in §10.1 derives from `CatEcommerce.Shared.Common.Enums.Enumeration` |
| F6 | `AggregateRoot : Entity` supplies `Id`, `Version`, `IsMarkedToDelete`, `CreatedBy/Date`, `LastUpdatedBy/Date`, `TenantId`, `VerticalId`, `Metadata`, `Tags`, `SetDefaultValue(UserContext)`, protected `AddDomainEvent` / `UpdateVersion()`, public `ClearEvents()` | Never redeclare. `CreatedDate` is the registration time — **no `RegisteredOn`** |
| F7 | `ValidationResponse.AddError(errorMessage, propertyName, errorCode, resourceName)` | Named arguments, always |
| F8 | `Query<TResult>` already carries paging | Never redeclare paging |
| F9 | `EventPublishOption` — `DoNotPublish=0`, `InMemoryPublishOnly=1` | `InMemoryPublishOnly` for every aggregate here; `DoNotPublish` for audit entries |
| F10 | `Event` supplies `CorrelationId`, `UserContext`, `TimeStamp`, `AggregateRootId`, `AggregateRootVersion` | Never redeclare. **`UserContext` rides on every event** — one more reason no credential may be placed on one |
| F11 | `CatEcommerceDbContext` has four `DbSet`s and `ApplyConfigurationsFromAssembly` | Add `DbSet`s; configurations are discovered. **One** `DbContext` |
| F12 | `EfAggregateRootRepository<T>` is registered open-generic | Every new aggregate resolves with **no new registration**. Do not write a per-aggregate repository |
| F13 | `DevUserContextProvider` returns a fixed `UserId`/`TenantId` from `DevUser:*` configuration | **Replaced** as the source of identity by `SessionUserContextProvider` (§12.5). It survives only as the Development fallback when no session is presented and `DevUser:Enabled` is `true` |
| F14 | `ConcurrencyExceptionFilter` maps `DbUpdateConcurrencyException` → `409` and is a global MVC filter | Add a **separate** `UniqueConstraintExceptionFilter`; do not widen this one |
| F15 | Shipped EF configurations set `Tags`, `Language`, `ServiceId` `IsRequired(false)`, `Ignore(Events)`, `Ignore(Metadata)`, `Property<uint>("xmin").IsRowVersion()` | Every new configuration does all five |
| F16 | Aggregate ids are `ValueGeneratedNever()` and minted in the controller | Same here, including owned-entity ids |
| F17 | Platform `UserContext` has `UserId`, `TenantId`, `UserName`, `DisplayName` and **`Roles` as `string[]`** | Staff roles travel as the role-id strings of §10.2. Customers carry the single role `Customer` |
| F18 | ASP.NET Core 9's `Microsoft.AspNetCore.RateLimiting`, `Microsoft.Extensions.Caching.Memory`, `ForwardedHeadersMiddleware` and `TimeProvider` are in the shared framework the Api targets | **No package reference** for any of them |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Account context — schema `account`
CustomerAccountAggregate  (root)                       account.customer_account
├── MobileNumber?          [owned one, opt]            → mobile_number          (unique where not null)
├── EmailAddress?          [owned one, opt]            → email_address          (unique where not null)
├── PasswordCredential?    [owned one, opt]            → password_hash, password_algorithm, password_set_on
├── LoginThrottle          [owned one]                 → failed_login_count, locked_until, last_failed_login_on
└── AdditionalPhoneNumbers [primitive collection ≤ 3]  → additional_phone_numbers text[]

CustomerSessionAggregate                 (root)  → CustomerAccountId   account.customer_session
PasswordResetTokenAggregate              (root)  → AccountKind + AccountId  account.password_reset_token
CustomerAddressBookAggregate             (root, Id == customer id)     account.customer_address_book
└── Addresses [owned many ≤ 10] DeliveryAddress                        account.customer_delivery_address
CustomerNotificationPreferencesAggregate (root, Id == customer id)     account.customer_notification_preferences
└── MarketingConsents [owned many ≤ 4] MarketingConsent                account.customer_marketing_consent

StaffAccountAggregate  (root)                          account.staff_account
├── MobileNumber?  EmailAddress?  PasswordCredential?  LoginThrottle   (same value objects)
└── RoleIds        [primitive collection 1…8]          → role_ids text[]
StaffSessionAggregate  (root)  → StaffAccountId        account.staff_session

Existing roots touched
AuditEntryAggregate  (extended, §8.11)                 audit.audit_entry
CatAggregate         (one method added, §8.9)          clinic.cat
```

**Why customer and staff are two account aggregates, not one `User`.** Different creators (the
public vs an administrator), different contents (roles vs addresses and consent), different readers,
and FR-ACC-3's rule that a customer credential never grants a staff session. One table with a type
column would make every customer query step around staff rows and make the "never grants" rule a
filter someone can forget. Two tables make it structural. The identifier and credential **value
objects are shared** so the rules cannot drift. Whether one person can hold both is OQ-3; they are
not linked.

**Why sessions, reset tokens, the address book and the preferences are separate roots.** Sessions
and reset tokens accumulate without bound (`.ai/bounded-contexts.md` rule 3, the ADR-023 reasoning).
The address book and preferences have invariants that need nothing from the account, and putting two
owned collections on the account would add two joins to the sign-in path — the hottest write path in
the context. The address book and preferences take the **customer id as their own id** — a 1:1 shared
key, created on first write (ADR-035).

**One aggregate is mutated per command**, with two recorded exceptions: sign-in updates the account's
throttle **and** creates a session (ADR-033); and the audit change-capture writes an audit row in the
same transaction as whatever it audits (ADR-040). Nothing else crosses the line — in particular the
merge mutates only the duplicate, and cats move in event handlers (ADR-039).

### 8.2 Value objects

`src/Domain/CatEcommerce.Domain/ValueObjects/` · namespace `CatEcommerce.Domain.ValueObjects` (flat,
matching the shipped `Weight`). Each is a `sealed record` with a **private constructor** and a static
factory that throws `DomainRuleViolationException`.

| VO | Shape | Factory rules |
|---|---|---|
| `MobileNumber` | `Value` | `Create(raw)`: strip spaces, hyphens, parentheses and a leading `00`; accept `01XXXXXXXXX` or `+8801XXXXXXXXX` / `8801XXXXXXXXX`; **normalise to `+8801XXXXXXXXX`**; else `InvalidMobileNumber`. ≤ 20. **The only mobile normaliser in the solution** — OQ-1 |
| `EmailAddress` | `Value` | `Create(raw)`: trim, **lower-case invariant**, exactly one `@`, non-empty local part, dotted domain, ≤ 256; else `InvalidEmailAddress` |
| `PasswordCredential` | `Hash`, `Algorithm`, `SetOn` | `Create(hash, algorithm, setOn)`: `Hash` non-empty ≤ 256, `Algorithm` non-empty ≤ 50; else `InvalidPasswordCredential`. **There is no factory that takes a plaintext** |
| `LoginThrottle` | `FailedCount`, `LockedUntil?`, `LastFailedOn?` | `None` static; `AfterFailure(asOf)` returns a new throttle with `FailedCount + 1` and `LockedUntil = asOf + LoginThrottleRules.DelayFor(FailedCount + 1)` (or null when the delay is zero); `IsLocked(asOf)` |
| `AccountIdentifier` | `Mobile?`, `Email?` | `Parse(raw)`: contains `@` → `EmailAddress.Create`, else `MobileNumber.Create`. The one place a login/reset "identifier" string becomes a typed lookup |

Record equality over the normalised value means `01712345678` and `+880 1712-345678` are the same
person, and `Foo@X.com` and `foo@x.com` are the same email. That is what makes every uniqueness check
in this document correct rather than cosmetic.

### 8.3 `CustomerAccountAggregate`

`src/Domain/CatEcommerce.Domain/Aggregates/Account/CustomerAccountAggregate.cs` · namespace
`CatEcommerce.Domain.Aggregates.Account`. Private parameterless constructor for EF;
`public CustomerAccountAggregate(Guid id) : base(id)`. All setters **private**.

| Property | Type | Notes |
|---|---|---|
| `FullName` | `string` | ≤ 200, required |
| `MobileNumber` | `MobileNumber?` | Unique among customers where not null |
| `EmailAddress` | `EmailAddress?` | Unique among customers where not null |
| `PasswordCredential` | `PasswordCredential?` | Null for a walk-in until they set one (§8.7), and after anonymisation |
| `LoginThrottle` | `LoginThrottle` | Consecutive failures and lock |
| `SecurityStamp` | `Guid` | Rotated by password set/reset and anonymisation. **Sessions and reset tokens are valid only while their copy of the stamp matches** (ADR-034) |
| `RegistrationChannel` | `RegistrationChannel` | `SelfService` \| `Reception`. Set once |
| `Status` | `CustomerAccountStatus` | `Active` \| `Merged` \| `Anonymised` |
| `AdditionalPhoneNumbers` | `IReadOnlyList<string>` over a private list | ≤ 3 normalised numbers. **Contact only** — never a login identifier, never unique |
| `LastSuccessfulLoginOn` | `DateTimeOffset?` | |
| `MergedIntoCustomerId` | `Guid?` | Set once by `MergeInto` (§8.9) |
| `MergedOn` | `DateTimeOffset?` | |
| `AnonymisedOn` | `DateTimeOffset?` | Set once by `Anonymise` (§8.10a) |
| `LastPersonalDataExportOn` | `DateTimeOffset?` | |
| `RecordStatus` | `CustomerRecordStatus` | **Computed, not mapped**: `CustomerRecordStatusRules.Derive(PasswordCredential is not null)` → `Incomplete` \| `Complete` (ADR-038) |

**Invariant, checked by a private `EnsureHasIdentifier()` at the end of every mutation that can
touch identifiers:** at least one of `MobileNumber` / `EmailAddress` is non-null, except after
`Anonymise` → else `IdentifierRequired`.

**Mutation methods.** Each that changes state ends `AddDomainEvent(...)` then `UpdateVersion()`.

| Method | DTO | Invariants | Event |
|---|---|---|---|
| `Register` | `CustomerRegistrationDto` | Name required ≤ 200 → `NameRequired`; at least one identifier → `IdentifierRequired`; credential required with non-empty hash and algorithm → `InvalidPasswordCredential`; sets `RegistrationChannel = SelfService`, `Status = Active`, `LoginThrottle = None`, a fresh `SecurityStamp` — **each explicitly, not by initialiser**; `SetDefaultValue(userContext)` | `CustomerRegisteredEvent` |
| `RegisterWalkIn` | `WalkInCustomerRegistrationDto` | Name required; **mobile required** (FR-ACC-9 "name and phone"); `RegistrationChannel = Reception`, `PasswordCredential = null`, `EmailAddress = null`, `Status = Active`, fresh stamp — explicitly. The DTO has **no** field that can set a credential, an email or the channel | `WalkInCustomerRegisteredEvent` |
| `RecordSuccessfulLogin` | `LoginSuccessDto` | `EnsureCanSignIn(asOf)`; resets `LoginThrottle` to `None`; sets `LastSuccessfulLoginOn` | `CustomerSignedInEvent` |
| `RecordFailedLogin` | `LoginFailureDto` | `Status == Active` (a merged or anonymised account records nothing and stays dead); `LoginThrottle = LoginThrottle.AfterFailure(asOf)` | `CustomerSignInFailedEvent` |
| `SetPassword` | `PasswordSetDto` | `Status == Active` → else `AccountNotActive`; credential valid; **rotates `SecurityStamp`**; resets `LoginThrottle` | `CustomerPasswordSetEvent` (`WasFirstPassword`) |
| `UpdateContactDetails` | `CustomerContactDetailsDto` | Name required; ≤ 3 additional numbers → `AdditionalPhoneNumberLimitExceeded`; none equal the primary mobile or each other → `DuplicatePhoneNumber`; **may change or remove `EmailAddress` only if `MobileNumber` is not null** → else `PrimaryIdentifierReadOnly` (an email-only account's email is its only login identifier — OQ-5); **no primary-mobile field exists on the DTO**; unchanged → no event, no version bump | `CustomerContactDetailsUpdatedEvent` (booleans only) |
| `MergeInto` | `CustomerMergeDto` | §8.9 | `CustomerMergedEvent` |
| `Anonymise` | `CustomerAnonymisationDto` | §8.10a | `CustomerAccountAnonymisedEvent` |
| `RecordPersonalDataExport` | `PersonalDataExportDto` | `Status == Active` | `CustomerPersonalDataExportedEvent` |

Private guard `EnsureCanSignIn(asOf)`, in this order: `Status != Active` → `AccountNotActive`;
`PasswordCredential is null` → `InvalidCredentials`; `LoginThrottle.IsLocked(asOf)` → `AccountLocked`.
**All three collapse to the same public response** (§12.4 Rule 1).

> **The aggregate never sees a plaintext password.** Handlers call `IPasswordHasher`; the aggregate
> receives an already-built `PasswordCredential` or a boolean outcome. The policy decision is made
> outside, the invariant is enforced inside — the HO-CAT-001 `ValidityMonths` shape. ADR-025.

### 8.4 `CustomerSessionAggregate` and `StaffSessionAggregate`

Both in `Aggregates/Account/`. Same mechanism, different subject and policy.

| Property | Customer session | Staff session |
|---|---|---|
| Subject | `CustomerAccountId` | `StaffAccountId` |
| `TokenHash` | SHA-256 hex, ≤ 128, unique index | same |
| `IssuedForSecurityStamp` | the account's stamp at issue | same |
| `IssuedOn`, `ExpiresOn` | absolute, default 7 days | absolute, default 12 hours |
| `LastActivityOn` | — | set to `IssuedOn`; advanced by `RecordActivity`; never moves back |
| `EndedOn`, `EndReason` | `SignedOut` | `SignedOut` \| `IdleTimeout` |
| `IpAddress?`, `UserAgent?` | ≤ 45 / ≤ 512 (truncate) | same |

| Method | Invariants | Event |
|---|---|---|
| `Issue(dto)` | subject and hash non-empty; `ExpiresOn > IssuedOn` | `…SessionIssuedEvent` |
| `End(dto)` (sign-out) | not already ended; idempotent re-call is a no-op | `…SessionEndedEvent` |
| `StateAt(asOf, currentStamp[, idleTimeout])` | Query, no mutation. `Ended` → `Ended`; `asOf ≥ ExpiresOn` → `Expired`; stamp mismatch → `Revoked`; staff: `asOf − LastActivityOn ≥ idleTimeout` → `IdleExpired`; else `Active`. Delegates to `SessionStateRules.Classify` in `Shared` so the aggregate and the read side cannot disagree |
| `RecordActivity(dto)` (staff) | `StateAt == Active` → else the state's code; advances `LastActivityOn` **only if ≥ 60 s later** than the stored value (write amplification bound; idle expiry is never late, at most 60 s early — ADR-037); no event | — |
| `EndForIdleTimeout(dto)` (staff) | not ended **and** `StateAt == IdleExpired`; `EndedOn = LastActivityOn + idleTimeout` (the moment it actually expired) | `StaffSessionIdleExpiredEvent` |

There is **no** `Revive`, `Extend` or `Reactivate` method, and there must never be one. An ended
session is dead; signing in again issues a new one.

### 8.5 `PasswordResetTokenAggregate`

Append-only: created once, never updated. `AccountKind` (`Customer` \| `Staff`), `AccountId`,
`TokenHash` (Base64Url SHA-256, unique), `IssuedOn`, `ExpiresOn` (default 15 min),
`IssuedForSecurityStamp`, `Channel` (`Mobile` \| `Email`).

| Method | Behaviour | Event |
|---|---|---|
| `Issue(dto)` | Sets every property; `ExpiresOn > IssuedOn` | `PasswordResetRequestedEvent` |
| `EnsureUsable(asOf, currentStamp)` | **Non-mutating guard.** `asOf ≥ ExpiresOn` → `PasswordResetTokenExpired`; stamp mismatch → `PasswordResetTokenSuperseded`. Both collapse to `InvalidPasswordResetToken` in the handler | — |

**Single use by stamp rotation, not by a flag.** A successful set/reset rotates the account's
`SecurityStamp`; that one write kills this token, **every other outstanding token**, and **every
session** for the account (US-ACC-05 AC3). It also keeps the command to one aggregate. There is no
`Status` or `UsedOn` — a stored "expired" would go stale (ADR-023's reasoning). ADR-034.

### 8.6 `CustomerAddressBookAggregate` and `CustomerNotificationPreferencesAggregate`

**Address book** — `Id` is the customer id. `DefaultAddressId: Guid?` on the root; `Addresses`
owned many, ≤ `DeliveryAddressRules.MaxAddresses` (10). `DeliveryAddress` (`Domain/Entities/`,
owned): `Id` (controller-minted), `Label`, `Line1`, `Line2?`, `City`, `District`, `PostalCode?`,
`DeliveryInstructions?`, `AddedOn`, `LastChangedOn`. **No `IsDefault` column** — two defaults are not
representable.

| Method | Invariants | Event |
|---|---|---|
| `Open` | Once, on a new instance | `CustomerAddressBookOpenedEvent` |
| `AddAddress` | count < max → `DeliveryAddressLimitExceeded`; fields present and within §10.2 lengths → `InvalidDeliveryAddress`; **first address becomes the default** | `DeliveryAddressAddedEvent` (`BecameDefault`) |
| `UpdateAddress` | exists → `DeliveryAddressNotFound`; same field rules | `DeliveryAddressUpdatedEvent` |
| `RemoveAddress` | exists; **the default cannot be removed while another address exists** → `DefaultAddressCannotBeRemoved`; removing the last clears the default | `DeliveryAddressRemovedEvent` |
| `SetDefaultAddress` | exists; already default → no event, no version bump | `DefaultDeliveryAddressChangedEvent` |
| `Clear` | Used by anonymisation only: removes every address, clears default | `CustomerAddressBookClearedEvent` |

Every mutation ends `UpdateVersion()` then a private `EnsureDefaultIsConsistent()` that throws
`InvalidOperationException` (a programming error) unless `(Addresses.Count == 0) == (DefaultAddressId is null)`.
**`UpdateVersion()` on the root is load-bearing**: it makes EF update the root row, which brings its
`xmin` into play, so two concurrent adds at nine addresses cannot leave eleven.

**Notification preferences** — `Id` is the customer id. `MarketingConsents` owned many, at most one
per `NotificationChannel`. `MarketingConsent`: `Id`, `Channel`, `IsGranted`, `RecordedOn`, `Source`
(≤ 50). **Only marketing cells are stored**; there is no representation of "transactional: off", so
no bug or forged request can produce one (FR-NTF-4 by construction).

| Method | Invariants | Event |
|---|---|---|
| `Open` | Once | `CustomerNotificationPreferencesOpenedEvent` |
| `ChangePreferences` | non-empty; no cell twice → `DuplicateNotificationPreferenceChange`; every cell editable (`NotificationPreferenceRules.IsEditable`) → `NotificationPreferenceNotEditable`; **all-or-nothing**; a first explicit decision is recorded even if it equals the default (it is a consent record) | `NotificationPreferencesChangedEvent` |
| `Clear` | anonymisation only | `CustomerNotificationPreferencesClearedEvent` |
| `Permits(channel, category)` | Query — `NotificationPreferenceRules.Permits(...)` | — |

**Marketing is opt-in** (`DefaultMarketingGranted = false`) — NFR-LEG-4 requires consent to be
captured, and a default of "granted" records consent nobody gave. OQ-7.

### 8.7 `StaffAccountAggregate`

| Property | Type | Notes |
|---|---|---|
| `FullName` | `string` | ≤ 200 |
| `MobileNumber?`, `EmailAddress?` | shared VOs | At least one; each unique **among staff** where not null |
| `PasswordCredential?` | shared VO | **Null at creation.** Set by the holder through reset (US-ACC-14 AC3) |
| `LoginThrottle`, `SecurityStamp`, `LastSuccessfulLoginOn` | as customer | |
| `RoleIds` | `IReadOnlyList<string>` | 1…8 distinct ids from `StaffRoles.All`, **stored in catalogue order** |

| Method | Invariants | Event |
|---|---|---|
| `Create` | name; identifier; roles normalised (trim, ordinal de-dup, catalogue order); ≥ 1 → `StaffRolesRequired`; each in catalogue, ordinal case-sensitive → `UnknownStaffRole`; `PasswordCredential = null` explicitly | `StaffAccountCreatedEvent` |
| `ChangeRoles` | same role rules; **replaces the whole set**; equal set → no event, no version bump; **rotates `SecurityStamp`** so existing staff sessions stop carrying stale roles | `StaffAccountRolesChangedEvent` (previous and new sets) |
| `RecordSuccessfulLogin`, `RecordFailedLogin`, `SetPassword` | exactly as the customer versions; `EnsureCanSignIn` has no status check | `StaffSignedInEvent`, `StaffSignInFailedEvent`, `StaffPasswordSetEvent` |

Role rules live in the aggregate (they are FR-ACC-6's invariant); the validator repeats only the
non-empty check so the error names the field early.

### 8.8 Walk-in and "incomplete" — ADR-038

A walk-in is a **customer account** (FR-ACC-9 says "a customer record"), created by `RegisterWalkIn`
with `RegistrationChannel = Reception` and **no password**. Since verification no longer exists,
"incomplete" means exactly one thing:

```
RecordStatus = Incomplete   ⇔   PasswordCredential is null
```

It is **derived**, never stored, by `CustomerRecordStatusRules.Derive` in `Shared`, called by the
aggregate and by every query. It ends the moment the customer sets a password — through the reset
flow sent to the recorded phone (the proposed answer to **SRS Q-16**, §9.1 OQ-2). A walk-in cannot
register online with the same number: the unique index refuses it with the duplicate-mobile message,
whose text offers password reset — which is exactly the path that completes the record.

`RegistrationChannel` is kept even though `RecordStatus` no longer uses it: it is the only fact that
tells a reception-created account from a self-registered one after both have passwords, and the
future "walk-ins registered this week" report needs it.

### 8.9 Merge — ADR-039

**`CustomerAccountAggregate.MergeInto(CustomerMergeDto)`** — the command mutates **the duplicate
only**. The survivor is loaded and read, never modified. Invariants in order:

1. `dto.SurvivingCustomerId != Id` → `CannotMergeCustomerIntoItself`
2. `Reason` non-blank ≤ 1000 → `ReasonRequired`
3. `Status == Anonymised` → `AccountNotActive`
4. `MergedIntoCustomerId` set **and different** → `CustomerAlreadyMerged`
5. `MergedIntoCustomerId == dto.SurvivingCustomerId` → **re-application**: nothing re-set; event
   raised with `IsReapplication = true`; `UpdateVersion()`; return
6. **Match rule** — the two records share at least one contact number or email:
   `{this.Mobile} ∪ this.AdditionalPhoneNumbers` intersects `{survivor.Mobile} ∪ survivor.AdditionalPhoneNumbers`,
   or both emails are non-null and equal → else `CustomersNotDuplicates`. **Two nulls never match.**
7. `MergedIntoCustomerId`, `MergedOn`, `Status = Merged`; **rotate `SecurityStamp`** (the retired
   record's sessions die)

> **Why the match rule includes additional numbers.** Primary mobile and email are unique among
> customers, so two records can no longer share a *primary* identifier. The duplicates FR-ACC-10
> means are now: a walk-in whose phone is another account's additional number, or two people's
> records that list the same household phone. The report (§12.3 Q-I1) uses the same rule.

**`CatAggregate.ReassignOwnerOnCustomerMerge(CatOwnerMergeReassignmentDto)`** — the **one** Cat
change. `OwnerId == dto.DuplicateCustomerId` → else `CatOwnerMismatch`; `SurvivingCustomerId != OwnerId`
→ else `OwnerUnchanged`; **does not call `EnsureAlive()`** — a deceased cat's history must move too
(FR-CAT-9 "retained"); sets `OwnerId`; raises `CatOwnerReassignedOnMergeEvent`. It is not
`TransferOwnership`: a merge is not a change of person, and a future subscriber to
`CatOwnershipTransferredEvent` ("notify the new owner") must not fire on one.

Cats move in `ReassignCatsOnCustomerMergedHandler`, one `UpdateAsync` per cat, querying for cats
**still** on the duplicate — so a re-send after a part-way failure moves whatever is left. **The
merge is not atomic, and re-sending is the recovery** (ADR-039).

### 8.10 Outbound ports

`src/Domain/CatEcommerce.Domain/DomainServices/` (flat), implementations in
`Infrastructure/Security/` or `Infrastructure/Hosting/` (§12.2). ADR-025.

| Port | Surface |
|---|---|
| `IPasswordHasher` | `PasswordCredential Hash(string plaintext)`; `bool Verify(string plaintext, PasswordCredential credential)`; `void VerifyAgainstDummy(string plaintext)` — burns the same work for an unknown identifier |
| `IBreachedPasswordChecker` | `Task<bool> IsBreachedAsync(string plaintext, CancellationToken ct = default)` |
| `ISessionTokenIssuer` | `SessionTokenMaterial Issue()` → `(Token, TokenHash)`; `string HashToken(string token)` |
| `IPasswordResetTokenGenerator` | `PasswordResetTokenMaterial Generate(DateTimeOffset asOf)`; `string ComputeHash(string plainToken)` |
| `IPasswordResetTokenDispatcher` | `Task DispatchAsync(PasswordResetDispatchRequest request, CancellationToken ct)` |
| `IClientIpAccessor` | `IPAddress? GetClientIpAddress()` |

These are **ports**, not domain services in `.ai/command-handler-pattern.md` §5's sense. They sit in
`Domain.DomainServices` because `Application` references only `Domain` and `Shared` and that is the
only sanctioned namespace it can reach. **Do not "fix" this by moving them into Infrastructure** — it
will not compile from `Application`.

### 8.10a Anonymisation — ADR-041

`CustomerAccountAggregate.Anonymise(CustomerAnonymisationDto)` — DTO carries a timestamp and the
platform context, **nothing personal**. Invariants: `Status == Anonymised` → `AccountAlreadyAnonymised`;
`Status != Active` → `AccountNotActive`. Effect: `FullName = AnonymisationRules.PlaceholderFullName`
(`"Anonymised customer"`); `MobileNumber`, `EmailAddress`, `PasswordCredential`, `LastSuccessfulLoginOn`
→ null; `AdditionalPhoneNumbers` → empty; `LoginThrottle = None`; **rotate `SecurityStamp`**;
`Status = Anonymised`; `AnonymisedOn` set. Raises `CustomerAccountAnonymisedEvent` — ids only.

The row and its id survive, so every clinical and financial reference stays valid and shows the
placeholder (US-ACC-13 AC2). The unique indexes are **filtered on not-null**, so the freed number and
email can be registered again. Event handlers then `Clear` the address book and the preferences
(§11.7). **U-A5 reflects over every public string on the aggregate** and fails if a new personal
field is added without being cleared here — that is the forward obligation, enforced by a test.

### 8.11 `AuditEntryAggregate` — extended (US-ACC-12)

Add `IpAddress` (`IPAddress?`, null only when there is no HTTP request — never a fabricated
`0.0.0.0`), `Category` (`AuditCategory`), `CorrelationId` (`Guid?`). `Record(AuditEntryDto)` becomes
**one-shot**: a second call → `AuditEntryAlreadyRecorded`; `EntityType`/`Action` non-blank ≤ 100,
`Reason` non-blank ≤ 1000 → else `InvalidAuditEntry`. **No other public method may exist** — no
`Correct`, `Amend`, `Redact`, `Void`. A wrong entry is answered by another entry (D-5).

`AuditEntryDto` gains two **required** positional parameters with no defaults — `AuditCategory
Category, IPAddress? IpAddress` — so the compiler lists every writer that has not decided. The three
shipped `AuditCat*Handler`s are updated (`BusinessEvent`, IP from `IClientIpAccessor`).

### 8.12 Domain exception, no domain service

Reuse `DomainRuleViolationException`. **No new exception type** except the programming-error
`InvalidOperationException`s named above. Nothing here is a domain service: every cross-record lookup
(identifier uniqueness, token by hash, survivor exists) is a repository call in the handler. **Do not
wrap a repository call in a service** — ADR-015, a HIGH finding.

---

## 9. ADRs to record during this build

Append to [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) in the implementation commit of the
phase that needs each one, in the file's existing format. The last recorded ADR is **ADR-023**; this
handover owns **ADR-024 … ADR-041**. The retired per-story handovers' proposals under those numbers
are void.

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-024** | Accounts and Identity is the **`Account`** bounded context: folder/namespace segment `Account`, schema `account`. It owns the seven roots of §8.1. Customers and staff are **separate account aggregates** sharing identifier and credential value objects | `.ai/bounded-contexts.md` and `.ai/namespace-conventions.md` list neither (OQ-4). The retired handovers named it three ways |
| **ADR-025** | Security ports live in `Domain.DomainServices`, implementations in `Infrastructure`. Aggregates never see a plaintext password, token or CAPTCHA answer | The layering forces it, but `Domain.DomainServices` is documented for cross-aggregate logic, so a reviewer will "fix" it unless written down |
| **ADR-026** | Mobile is normalised to `+8801XXXXXXXXX`; email is lower-cased. **At least one** identifier per account; **each is unique per account kind**, enforced twice: a handler pre-check for the friendly message and a **filtered** unique index (`WHERE … IS NOT NULL`) for correctness under concurrency, the `23505` mapped to the same `400` | Two-layer enforcement looks redundant and will be "simplified"; the filter is what lets anonymisation free an identifier |
| **ADR-027** | Passwords are **Argon2id** (`Konscious`), `m=19456 KiB, t=2, p=1`, 16-byte salt, 32-byte output, PHC-encoded. A fixed dummy hash is verified for unknown identifiers so response time does not reveal existence. Breached passwords are checked against a **locally shipped list**; HIBP is deferred behind the same port | NFR-SEC-1 permits two algorithms; changing later means rehashing every credential. The retired handovers disagreed (bcrypt vs Argon2id) |
| **ADR-028** | **Disclosure rules.** Registration discloses that the submitted **mobile** (or **email**) is in use — US-ACC-01 requires it — and never mentions the other identifier. **Sign-in and reset request never disclose** whether an account exists: one generic `401`, one neutral `200`. Enumeration through registration is an accepted residual risk, mitigated by CAPTCHA and rate limits | A security reviewer will flag registration as an enumeration oracle. Without a decision naming the AC as the cause and the mitigations, the finding either blocks or is waved through silently |
| **ADR-029** | **Self-hosted image CAPTCHA** on registration: `POST /api/captcha` issues a random 5-character code rendered as a noisy PNG (SkiaSharp); answer in a size-bounded in-process `MemoryCache`, 120 s TTL, **single-use** (removed on the first attempt, right or wrong); checked by an Api action filter **before** the handler. No vendor, no table, no audit per challenge. `CaptchaController` injects the CAPTCHA services directly — a **recorded exception** to "controllers inject the dispatcher and user context only", because a challenge is not a domain command | Owner instruction; the controller exception and filter placement look like violations unless written down |
| **ADR-030** | **Built-in ASP.NET Core rate limiter**, fixed windows, `QueueLimit = 0`, `429` + `Retry-After`: `captcha-issue`, `registration`, `sign-in`, `password-reset`, `account-sensitive` — all partitioned by client IP (a null remote address partitions as `"unknown"`, never bypasses); `profile-update` (§1 Amended (18)(f)) is partitioned by the signed-in customer id instead, falling back to IP only when no session resolves. Applied per action, never globally | NFR-SEC-6 (extended to registration in SRS v2.1). The aggregate's lockout protects one account from many IPs; the limiter protects many accounts from one IP (or, for `profile-update`, one account from itself) — neither replaces the other |
| **ADR-031** | **Sessions are opaque 256-bit tokens**, stored only as SHA-256 hashes, delivered in **`HttpOnly; Secure; SameSite=Lax; Path=/`** cookies — `pc_session` (customer) and `pc_staff_session` (staff) — with an absolute expiry and a copy of the account's security stamp. **Not JWT.** Unsafe methods require a **double-submit CSRF token**: a non-HttpOnly `pc_csrf` cookie echoed in `X-CSRF-Token`. Explicit sign-out ends the session | NFR-SEC-4 names signed short-lived tokens **and** CSRF tokens. Server-side sessions make "invalidate all sessions on reset" one stamp rotation; JWT would make it impossible. A cookie survives a reload; an in-memory bearer token (the retired proposal) logs everyone out on refresh |
| **ADR-032** | **Caller resolution is one middleware and one provider.** `SessionAuthenticationMiddleware` resolves the session cookie(s) into a `ResolvedCaller` in `HttpContext.Items`; `SessionUserContextProvider` (Scoped) builds `UserContext` from it — staff: `UserId = StaffAccountId`, `Roles` = role ids; customer: `UserId = CustomerAccountId`, `Roles = ["Customer"]`; none: anonymous (`Guid.Empty`, no roles). `DevUserContextProvider` is kept only as the fallback when **no** session is presented, the environment is `Development` and `DevUser:Enabled` is `true`; the host refuses to start with it enabled elsewhere | Every audit writer and every authorization check reads `UserContext`. One resolution point means they cannot disagree; the Development fallback keeps HO-CAT-002's screens usable before staff sign-in is wired in the browser |
| **ADR-033** | **Sign-in touches two aggregates** — the account's throttle reset and a new session — in one handler, a documented exception to `.ai/bounded-contexts.md` rule 1 | The alternative (issue the session from an event handler) splits "counter reset" from "session created" across commits and forces the token plaintext out of the handler that returns it |
| **ADR-034** | **Password reset tokens** are 256 CSPRNG bits, persisted as SHA-256, 15 min, bound to the account's `SecurityStamp`; a successful set rotates the stamp, killing every token and session in one write. The request endpoint returns **one neutral `200`** on every outcome; the confirm endpoint collapses every token failure to `InvalidPasswordResetToken`. One mechanism serves customers, staff first-password and walk-in completion (`AccountKind`) | The neutral response and the collapsed code both read as bugs to anyone who has not read this; stamp rotation is the part most likely to be "simplified" into a `UsedOn` flag |
| **ADR-035** | Address book and notification preferences are **separate roots keyed by the customer id**, created on first write; the default address is one `Guid?` on the root; only marketing consent is stored, opt-in by default | Keeps two collections off the sign-in path and makes "two defaults" and "transactional off" unrepresentable |
| **ADR-036** | **Staff roles are a code-defined catalogue** of the eight staff columns of SRS Appendix D; the Appendix D matrix is served read-only by `GET /api/staff-roles`. Authorization is `[RequireAction(action)]`, an `IAsyncAuthorizationFilter` resolving the action against **`ActionRoles`, the one action-to-role table for the solution**; ordinal exact match; **unmapped actions fail closed**; every denial writes one audit entry and returns `403` whether or not the write succeeds | ADR-009 applied here. A code catalogue looks like an oversight against FR-ADM-2 unless recorded as the step before US-ADM-02 |
| **ADR-037** | **Staff idle expiry is derived** from `LastActivityOn` and made **terminal** on detection (`EndForIdleTimeout`), audited once. Activity writes are throttled to one per 60 s, so expiry is never late and at most 60 s early. A request refused for session reasons **has executed nothing** and may be replayed after re-authentication. `GET /api/staff/session` never extends | US-ACC-08 AC2 depends on the no-side-effect guarantee; the 60 s throttle is a deliberate accuracy trade the FE must know |
| **ADR-038** | A walk-in is a customer account with `RegistrationChannel = Reception` and no password; **`Incomplete ⇔ PasswordCredential is null`**, derived by one function in `Shared` | Verification no longer exists to end "incomplete"; a stored flag would go stale on every path that sets a password |
| **ADR-039** | **Merge** is a single-aggregate command on the duplicate, which is **retained** (`Status = Merged`, `MergedIntoCustomerId`), never deleted. Dependent data moves in handlers of `CustomerMergedEvent` — **the binding integration contract every future context subscribes to, idempotently**. Not atomic; re-sending the identical merge is the recovery | N+1 commits in one request is a real departure from atomicity; the ARCHITECTURE gate must judge it as a decision, not discover it |
| **ADR-040** | **Audit integrity.** Every aggregate change is audited **in the same transaction** by `AuditChangeCaptureInterceptor` (EF `SaveChangesInterceptor`, before/after diff, secrets redacted fail-closed, `Category = DataChange`). Authentication and business events keep event-handler writers (`AuthenticationEvent` / `BusinessEvent`). Audit entries are immutable at three layers: the aggregate, `AuditEntryImmutabilityInterceptor`, and **PostgreSQL triggers that refuse `UPDATE`, `DELETE` and `TRUNCATE`**. IP is server-derived: `ForwardedHeadersMiddleware` with `XForwardedFor` only, `ForwardLimit = 1`, trusted proxies **only** from configuration, empty by default | `.ai/domain-events-and-outbox.md` §3 makes a handler-written audit fail *after* the change commits, which C-9 forbids. This changes where audit writes happen for the whole solution |
| **ADR-041** | **Account deletion is in-place anonymisation**, never row removal: personal fields overwritten, credential destroyed, stamp rotated, `Status = Anonymised`. Self-service deletion requires **step-up confirmation with the current password**, which shares the sign-in lockout. The personal-data export is `POST` (it writes an access record) and returns versioned JSON | Clinical and financial references hold the account id; removing the row dangles them. The step-up and the shared lockout stop deletion becoming a password oracle |

### 9.1 Open questions — raised, not resolved

Where a default was needed to keep the slice buildable, it is stated. **Do not change a default
without an Amended row.**

| # | Question | Owner | Default in this build |
|---|---|---|---|
| **OQ-1** | Accepted mobile format — SRS gives none | Owner | Bangladesh `+880` policy (§8.2). Change is confined to `MobileNumber.Create` and its tests |
| **OQ-2** | **SRS Q-16** — how a walk-in sets a password | Owner | Password reset to the recorded phone (§8.8). US-ACC-10 AC2's acceptance row is marked *provisional* until confirmed |
| **OQ-3** | Can one person be both customer and staff, with the same mobile? | Owner + architect | Unique **per account kind**; not linked |
| **OQ-4** | `.ai/bounded-contexts.md` and `.ai/namespace-conventions.md` need the `Account` context | Architect — a separate authorised task (§14) | ADR-024 records it; the gate reads the omission as known |
| **OQ-5** | How does a customer change their primary login identifier? No proof-of-possession exists any more | Owner | Mobile never editable; email editable only when a mobile exists (§8.3) |
| **OQ-6** | Breached-password corpus — which list, how large, refreshed how | Owner | A widely published common-password list, provenance in its header (ADR-027) |
| **OQ-7** | Marketing opt-in by default? | Owner | Yes (ADR-035) |
| **OQ-8** | Reverse proxy in production? | Deployment | Trusted proxies empty (ADR-040); rate limits partition on the direct peer |
| **OQ-9** | **SRS Q-17** — is reception an acceptable non-visual CAPTCHA alternative? | Owner / accessibility | Yes; no audio challenge |
| **OQ-10** | Session lifetimes — customer 7 days, staff 12 hours? | Owner | Those defaults, in configuration |
| **OQ-11** | Should removing the last `Administrator` role be refused? | Owner | Refused → `LastAdministratorCannotBeRemoved` (a staff account can lock the system out of its own staff admin otherwise) |
| **OQ-12** | Is the Appendix D matrix enforced for anything other than the actions in §12.8? | Owner + US-ADM-02 | No — the catalogue endpoint serves it for display; only mapped actions are enforced |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations

`src/Shared/CatEcommerce.Shared/Common/Enums/` · derive from our `Enumeration` (ADR-022), values from
**1**, `nameof()` display names, persisted **by `Value`**. Follow the seven shipped enumerations.

| Type | Members |
|---|---|
| `CustomerAccountStatus` | `Active`, `Merged`, `Anonymised` |
| `RegistrationChannel` | `SelfService`, `Reception` |
| `CustomerRecordStatus` | `Complete`, `Incomplete` — **computed only, never persisted** |
| `AccountKind` | `Customer`, `Staff` |
| `PasswordResetChannel` | `Mobile`, `Email` |
| `SessionEndReason` | `SignedOut`, `IdleTimeout` |
| `SessionState` | `Active`, `Expired`, `Revoked`, `Ended`, `IdleExpired` — **computed only** |
| `SignInOutcome` | `Success`, `InvalidCredentials`, `AccountLocked`, `AccountNotActive` — for audit and tests, **never** a response body |
| `NotificationChannel` / `NotificationCategory` | `Email`, `Sms`, `Push`, `InApp` / `Transactional`, `Marketing` |
| `CapabilityAccess` | `None`, `ReadOnly`, `CreateEdit`, `Full` — **least to most permissive**, so a union is a numeric max |
| `AuditCategory` | `DataChange`, `AuthenticationEvent`, `BusinessEvent` |

### 10.2 Constants and helpers

`Common/Constants/` and `Common/Helpers/` in `Shared`, namespace `CatEcommerce.Shared.Common.Constants`
/ `.Helpers` (flat).

| File | Content |
|---|---|
| `PasswordRules` | `MinimumLength = 10` (NFR-SEC-1) · `MaximumLength = 256` — a resource bound on the hasher for anonymous callers, not a strength rule |
| `LoginThrottleRules` | `DelayFor(int consecutiveFailures)`: 1–2 → 0 · 3 → 5 s · 4 → 15 s · 5 → 60 s · 6 → 300 s · ≥ 7 → 900 s (cap). Monotonic non-decreasing, capped — **the shape is the requirement** (US-ACC-04 AC3) |
| `StaffRoles` | Eight `const string` ids from Appendix D's staff columns: `Reception`, `Nurse`, `Vet`, `Caretaker`, `Inventory`, `ShopManager`, `Accountant`, `Administrator`; `All` in catalogue order. `Customer` is **not** a staff role; `Groomer` appears in SRS §2.3 but has no Appendix D column, so it is not a role |
| `StaffRoleCatalogue` | The twenty Appendix D capability rows × eight staff roles as read-only data (`CapabilityAccess` + optional qualifier such as `"own"`, `"ops"`) |
| `ActionRoles` | The **single** action-to-role table (§12.8) |
| `CustomerRecordStatusRules` | `Derive(bool hasPassword)` |
| `SessionStateRules` | `Classify(...)` used by both session aggregates and the read side |
| `NotificationPreferenceRules` | `IsEditable`, `IsAlwaysOn`, `DefaultMarketingGranted = false`, `Permits(channel, category, marketingGranted)` |
| `DeliveryAddressRules` | `MaxAddresses = 10`; field lengths — `Label` ≤ 50, `Line1` ≤ 200, `Line2` ≤ 200, `City` ≤ 100, `District` ≤ 100, `PostalCode` ≤ 20, `DeliveryInstructions` ≤ 500 |
| `CustomerProfileRules` | `MaxAdditionalPhoneNumbers = 3` |
| `AnonymisationRules` | `PlaceholderFullName = "Anonymised customer"` |
| `AccountMessages` | Every user-facing message that appears on two paths (handler pre-check **and** `UniqueConstraintExceptionFilter`; handler **and** neutral endpoint) as one `const`, so the two paths cannot drift |

### 10.3 Violation codes

Append to `Common/Constants/BusinessViolationCodes.cs` in the existing style. **Do not reorder or
rename the existing thirteen.**

| Group | Codes |
|---|---|
| Identifiers & registration | `NameRequired` · `IdentifierRequired` · `InvalidMobileNumber` · `InvalidEmailAddress` · `MobileNumberAlreadyRegistered` · `EmailAddressAlreadyRegistered` · `PrimaryIdentifierReadOnly` · `DuplicatePhoneNumber` · `AdditionalPhoneNumberLimitExceeded` |
| Password | `PasswordTooShort` · `BreachedPassword` · `InvalidPasswordCredential` |
| CAPTCHA & limits | `CaptchaRequired` · `CaptchaInvalid` · `TooManyRequests` |
| Sign-in & sessions | `InvalidCredentials` · `AccountLocked` · `AccountNotActive` · `SessionInvalid` · `SessionIdleTimeout` · `CsrfTokenInvalid` · `ReauthenticationFailed` |
| Reset | `InvalidPasswordResetToken` · `PasswordResetTokenExpired` · `PasswordResetTokenSuperseded` |
| Addresses & preferences | `DeliveryAddressLimitExceeded` · `InvalidDeliveryAddress` · `DeliveryAddressNotFound` · `DefaultAddressCannotBeRemoved` · `DuplicateNotificationPreferenceChange` · `NotificationPreferenceNotEditable` |
| Staff & authorization | `StaffRolesRequired` · `UnknownStaffRole` · `LastAdministratorCannotBeRemoved` · `StaffAccountNotFound` · `ActionNotPermitted` |
| Customers (staff-facing) | `CustomerNotFound` · `CannotMergeCustomerIntoItself` · `CustomerAlreadyMerged` · `CustomersNotDuplicates` · `CatOwnerMismatch` · `ReasonRequired` |
| Deletion | `AccountAlreadyAnonymised` |
| Audit | `AuditEntryAlreadyRecorded` · `InvalidAuditEntry` |

> **`PasswordTooShort` and `BreachedPassword` are two codes, deliberately** — US-ACC-01 AC "rejected
> with a specific reason". `CaptchaInvalid` covers wrong, expired, used and unknown alike — one message:
> *"The characters didn't match, or the code expired. Type the new code shown."* `InvalidCredentials`,
> `AccountLocked` and `AccountNotActive` exist for audit and tests and **never reach a response body**.

---

## 11. Specified changes — write side

### 11.1 Commands — `src/Application/CatEcommerce.Application/Commands/Account/`

Namespace `CatEcommerce.Application.Commands.Account`. Extend `Command`, `required`/`init`, **data
only**. Ids are minted in the controller. `IpAddress?`/`UserAgent?` are read by the controller from the
connection, never from the body.

| # | Command | Payload | Phase |
|---|---|---|---|
| C1 | `RegisterCustomerCommand` | `CustomerId`, `FullName`, `MobileNumber?`, `EmailAddress?`, `Password` | B |
| C2 | `SignInCustomerCommand` | `SessionId`, `Identifier`, `Password`, `IpAddress?`, `UserAgent?` | C |
| C3 | `SignOutCustomerCommand` | `TokenHash` | C |
| C4 | `RequestPasswordResetCommand` | `TokenId`, `AccountKind`, `Identifier` | D |
| C5 | `ResetPasswordCommand` | `Token`, `NewPassword` | D |
| C6 | `UpdateCustomerContactDetailsCommand` | `FullName`, `EmailAddress?`, `AdditionalPhoneNumbers` | E |
| C7 | `AddDeliveryAddressCommand` / `UpdateDeliveryAddressCommand` / `RemoveDeliveryAddressCommand` / `SetDefaultDeliveryAddressCommand` | `AddressId` + fields | E |
| C8 | `ChangeNotificationPreferencesCommand` | `Changes[]` (`Channel`, `Category`, `IsGranted`) | E |
| C9 | `CreateStaffAccountCommand` | `StaffAccountId`, `FullName`, `MobileNumber?`, `EmailAddress?`, `RoleIds` | F |
| C10 | `ChangeStaffRolesCommand` | `StaffAccountId`, `RoleIds` | F |
| C11 | `RecordAuthorizationDenialCommand` | `Action`, `Method`, `Path`, `IpAddress?` | F |
| C12 | `SignInStaffCommand` | as C2 | G |
| C13 | `SignOutStaffCommand` | `TokenHash` | G |
| C14 | `RecordStaffSessionActivityCommand` | `TokenHash`, `IpAddress?` | G |
| C15 | `RegisterWalkInCustomerCommand` | `CustomerId`, `FullName`, `MobileNumber` | H |
| C16 | `MergeCustomerCommand` | `DuplicateCustomerId`, `SurvivingCustomerId`, `Reason` | I |
| C17 | `RecordPersonalDataExportCommand` | — (caller from `UserContext`) | J |
| C18 | `DeleteCustomerAccountCommand` | `CurrentPassword` | J |

**Customer self-service commands (C6–C8, C17, C18) carry no customer id.** The handler takes it from
`command.UserContext.UserId`, which `SessionUserContextProvider` filled from the verified session
(ADR-032). A client-supplied id would be an insecure direct object reference.

> **`Password`, `NewPassword`, `CurrentPassword` and `Token` are the only plaintext secrets in the
> solution, and each exists for one in-process dispatch.** Each property carries
> `// SECURITY: plaintext. Never logged, never persisted, never echoed.` — the marker reviewers grep
> for. They are never interpolated into a log, exception or validation message (U-B5, I-X1). This is
> safe only because dispatch is in-memory and synchronous (ADR-013).

**No command carries a CAPTCHA field** — the CAPTCHA is consumed by an Api filter before the command
exists (§12.6).

### 11.2 Validators — `CommandHandlers/Validators/` (flat)

One `AbstractValidator<T>` per command, registered as **`IValidator<T>`** (F3 + di-registration §3 —
two independent ways for a rule to silently never run). **Shape only** — nothing needing a database,
and nothing that reveals whether an account exists.

| Validator | Rules |
|---|---|
| C1 | `CustomerId` not empty; `FullName` not empty ≤ 200; **at least one of mobile/email** (`IdentifierRequired`, property `MobileNumber`); mobile ≤ 20; email ≤ 256; `Password` not empty, `MinimumLength(10)` with **`PasswordTooShort`**, ≤ 256. The message states the minimum and never echoes the value |
| C2, C12 | `Identifier` not empty ≤ 256; `Password` not empty ≤ 256 — **no minimum length** here (that is a strength rule; reporting it would say "that can't be your password") |
| C4 | `Identifier` not empty ≤ 256 |
| C5 | `Token` not empty ≤ 128; `NewPassword` as C1's password |
| C6 | `FullName`; each additional number ≤ 20; count ≤ 3 |
| C7 | field lengths per §10.2 |
| C8 | non-empty `Changes` |
| C9, C10 | `FullName`; identifier as C1; `RoleIds` non-empty (`StaffRolesRequired`) — **catalogue membership is the aggregate's** |
| C15 | `FullName`; `MobileNumber` not empty |
| C16 | ids not empty; `Reason` not empty ≤ 1000 |
| C18 | `CurrentPassword` not empty ≤ 256 |

### 11.3 Data mappers — `DataMappers/` (flat)

`CustomerAccountMapper`, `StaffAccountMapper`, `AddressBookMapper`, `NotificationPreferencesMapper`:
static `ToDomainDto()` extensions taking **already-constructed value objects** as arguments, so the
handler can catch a factory failure and turn it into a `400`. **Always pass `UserContext` and
`CorrelationId` through.** No mapper maps a plaintext secret into a DTO an event can reach.

DTOs in `src/Domain/CatEcommerce.Domain/Models/` (flat), one per aggregate method in §8. None has a
`Password` field; the registration and set-password DTOs carry a `PasswordCredential`.

### 11.4 Command handlers — `CommandHandlers/Account/`

Namespace `CatEcommerce.Application.CommandHandlers.Account`. `ICommandHandlerAsync<T>` directly,
primary constructor, the `RegisterCatCommandHandler` shape including the private `Violation(...)`
helper with named `AddError` arguments and `resourceName`. Every handler logs one line —
`"{Command} {Id}. CorrelationId: {CorrelationId}"` — and **never an identifier, name, password, token
or answer**.

**C1 `RegisterCustomerCommandHandler`** — injects the customer repository, `IPasswordHasher`,
`IBreachedPasswordChecker`. Order:
1. Normalise mobile and email through the VOs (catch → `400` on the right property).
2. **Uniqueness pre-checks, one per supplied identifier, mobile first**:
   `ExistAsync(c => c.MobileNumber!.Value == mobile.Value)` → `MobileNumberAlreadyRegistered` on
   `MobileNumber`, message `AccountMessages.MobileInUse` (*"This mobile number is already registered.
   You can log in, or reset your password."*); then the same for email → `EmailAddressAlreadyRegistered`,
   `AccountMessages.EmailInUse`. **Return on the first hit.** Neither message mentions the other
   identifier. Plain expressions over the owned property so EF translates them.
3. Breached check → `BreachedPassword`, **before hashing and before any write**.
4. Hash; `new CustomerAccountAggregate(id).Register(...)`; `SaveAsync(..., InMemoryPublishOnly)`.
5. `Result = customer.Id`.

> Steps 2 and 4 race, and that is expected. Two simultaneous registrations both pass step 2; one
> fails at the unique index; `UniqueConstraintExceptionFilter` turns its `23505` into **the same**
> `400`. Do not close the race with a lock, transaction or retry (ADR-026).

**C2 / C12 `SignIn…CommandHandler`** — injects the account repository, the session repository,
`IPasswordHasher`, `ISessionTokenIssuer`, `IOptions<SessionOptions>`, `TimeProvider`,
`IAggregateRootRepository<AuditEntryAggregate>`.
1. `AccountIdentifier.Parse` (failure → the uniform `InvalidCredentials`, not a format error — a
   format error on sign-in would say "that isn't even an identifier we accept").
2. Look up by mobile or email. **Not found → `passwordHasher.VerifyAgainstDummy(password)`**, write
   the unknown-identifier audit entry (§11.8), return `InvalidCredentials`.
3. Found → if `EnsureCanSignIn` would refuse (locked / not active / no credential): record nothing new,
   audit the outcome, return the violation (collapsed at the Api).
4. `Verify` fails → `account.RecordFailedLogin(...)` → **`UpdateAsync`** (a lockout counter that is
   not saved is not a lockout) → `InvalidCredentials`.
5. Success → `RecordSuccessfulLogin` → `UpdateAsync`; `Issue()` a token; new session with the
   account's current stamp → `SaveAsync`. **Two aggregates — ADR-033.**
6. `Result = new SignInResult(token, expiresOn, accountId)` (`Shared/SharedDto/Account/`). The token
   plaintext exists only here and in the cookie the controller writes.

**C3 / C13 `SignOut…`** — load session by hash; missing or ended → success (idempotent); else `End`.

**C4 `RequestPasswordResetCommandHandler`** — **generate token material before branching** (removes
the largest timing difference; the rate limiter is the real defence — say so in the summary comment,
do not claim constant time). Find the account of `AccountKind` by identifier. Not found, merged or
anonymised → return success, write nothing. Found → pick the channel: the identifier the caller typed
if it is on the account, else mobile, else email; `Issue` a token with the account's stamp;
`SaveAsync`; `dispatcher.DispatchAsync(new PasswordResetDispatchRequest(kind, accountId, channel,
masked destination, plain token, expiresOn))`. **Every path returns `IsSuccess = true` with no
`ValidationResult`.**

**C5 `ResetPasswordCommandHandler`** — hash the token → load by hash; load the account of the token's
`AccountKind`; `token.EnsureUsable(now, account.SecurityStamp)`; breached check; hash;
`account.SetPassword(...)` → `UpdateAsync`. **Every token failure** (unknown, expired, superseded,
account gone) → `InvalidPasswordResetToken` with one message. A breached or short new password does
**not** consume the token — the stamp only rotates on success.

**C6–C8** — load or lazily create the aggregate keyed by `UserContext.UserId` (`Open` then the
mutation, `SaveAsync`; otherwise `UpdateAsync`). A first-write race between two tabs produces a
`23505` on the primary key → `409` via `UniqueConstraintExceptionFilter` (the "someone else changed
this" path the FE already handles).

**C9 / C10** — duplicate identifier among **staff** → `MobileNumberAlreadyRegistered` /
`EmailAddressAlreadyRegistered` (staff-facing, so disclosure is fine). C10: if the change removes
`Administrator` from the last staff account holding it → `LastAdministratorCannotBeRemoved` (a
repository count in the handler — OQ-11). Both carry the ADR-009 **defence-in-depth guard**: the
handler re-checks `ActionRoles` against `command.UserContext.Roles` and refuses with
`ActionNotPermitted`. **The filter is the control; the guard is one line and one test (U-F9). Do not
describe it as the control.**

**C11 `RecordAuthorizationDenialCommandHandler`** — writes one `AuditEntryAggregate` directly
(`Action = "AuthorizationDenied"`, `Reason` = the action, `AfterValue` = method, path without query,
roles; `Category = AuthenticationEvent`). A denial mutates no aggregate, so there is no event to hang
it on.

**C14 `RecordStaffSessionActivityCommandHandler`** — load the staff session by hash and its staff
account's stamp; if `StateAt == IdleExpired` → `EndForIdleTimeout` → `UpdateAsync` → return
`SessionIdleTimeout`; any other non-active state → `SessionInvalid`; else `RecordActivity` →
`UpdateAsync` only if it changed. Dispatched by the middleware in **its own DI scope** with one retry
on `DbUpdateConcurrencyException`; a second conflict → `409`.

**C15 `RegisterWalkInCustomerCommandHandler`** — duplicate mobile (primary **or** additional on an
active customer) → `400 MobileNumberAlreadyRegistered` on `MobileNumber`. The response keeps the
standard `ValidationResponse` shape — no extra id field. The caller is authorised Reception or
Administrator staff (§12.8), so the screen follows up with `GET /api/staff/customers?mobile=` to open
the existing record; for staff that lookup is a feature, not an oracle. No hasher, no email.

**C16 `MergeCustomerCommandHandler`** — load duplicate and survivor (either missing → `CustomerNotFound`;
survivor not `Active` → `AccountNotActive`); build the DTO with the survivor's identifiers;
`duplicate.MergeInto(dto)` → `UpdateAsync`. Nothing else — cats move in §11.7.

**C17** — `RecordPersonalDataExport` → `UpdateAsync`. The controller then runs Q-J1 with the same id.

**C18 `DeleteCustomerAccountCommandHandler`** — load by `UserContext.UserId`; **if locked →
`AccountLocked` before verifying** (so the step-up is not a way to test passwords during a lockout);
verify `CurrentPassword`; failure → `RecordFailedLogin` → `UpdateAsync` → `ReauthenticationFailed`;
success → `Anonymise` → `UpdateAsync`.

### 11.5 Domain events — `Events/Account/`

`sealed class … : Event`, `init`, past tense, namespace `CatEcommerce.Domain.Events.Account`
(`CatOwnerReassignedOnMergeEvent` in `Events/Cat/`). **No event carries a name, phone number, email,
address line, password, hash, token, token hash or security stamp** — ids, booleans, enums and
timestamps only. U-X1 asserts this by reflection over every event type in the assembly, so a later
addition fails on purpose.

`CustomerRegisteredEvent` (`CustomerId`, `HasMobile`, `HasEmail`) · `WalkInCustomerRegisteredEvent` ·
`CustomerSignedInEvent` / `StaffSignedInEvent` (`AccountId`, `OccurredOn`) ·
`CustomerSignInFailedEvent` / `StaffSignInFailedEvent` (`AccountId`, `Outcome`, `ConsecutiveFailures`,
`LockedUntil?`) · `CustomerSessionIssuedEvent` / `StaffSessionIssuedEvent` · `…SessionEndedEvent` ·
`StaffSessionIdleExpiredEvent` (`LastActivityOn`, `ExpiredOn`, `DetectedOn`, `IdleTimeoutMinutes`) ·
`PasswordResetRequestedEvent` (`AccountKind`, `AccountId`, `Channel`, `ExpiresOn`) ·
`CustomerPasswordSetEvent` / `StaffPasswordSetEvent` (`WasFirstPassword`) ·
`CustomerContactDetailsUpdatedEvent` (booleans per field) · the six address-book events · the three
preferences events (changes as channel/category display names and before/after booleans) ·
`StaffAccountCreatedEvent` / `StaffAccountRolesChangedEvent` (role-id lists — not personal data) ·
`CustomerMergedEvent` (`DuplicateCustomerId`, `SurvivingCustomerId`, `Reason`, `IsReapplication`) ·
`CatOwnerReassignedOnMergeEvent` · `CustomerAccountAnonymisedEvent` · `CustomerPersonalDataExportedEvent`.

### 11.6 Where audit entries come from

| Source | Category | What |
|---|---|---|
| `AuditChangeCaptureInterceptor` (§11.9) | `DataChange` | **Every** aggregate insert/update in the solution, same transaction, before/after |
| Event handlers (§11.7) | `AuthenticationEvent` | Sign-in success and failure, sign-out, session issue, idle expiry, reset requested, password set |
| Event handlers (§11.7) | `BusinessEvent` | Merge (with reason), cat reassigned on merge, anonymisation, data export, and the three shipped Cat handlers |
| Direct writes (§11.8) | `AuthenticationEvent` | Unknown-identifier sign-in failure; authorization denial (C11) |

Flagging a cat high-risk therefore produces one `DataChange` row (what changed) and one
`BusinessEvent` row (why), joined by `CorrelationId`. Neither replaces the other.

### 11.7 Domain event handlers — `DomainEventHandlers/Account/`

Each injects `IAggregateRootRepository<AuditEntryAggregate>` and `IClientIpAccessor` and calls
`SaveAsync(entry, EventPublishOption.DoNotPublish)`. **Never `SaveChangesAsync` in a handler** — a
BLOCKER. Each carries the summary comment that **a failure here fails the request after the change
committed** (`.ai/domain-events-and-outbox.md` §3) — intended for authentication events, which must
not succeed unlogged (C-9).

For authentication events `ActorUserId` is the **resolved account id**, not the (anonymous) caller.

Non-audit handlers:

| Handler | Handles | Does |
|---|---|---|
| `ReassignCatsOnCustomerMergedHandler` | `CustomerMergedEvent` | Query `clinic.cat` ids with `OwnerId == DuplicateCustomerId` via the Cat repository filter; for each, `ReassignOwnerOnCustomerMerge` → `UpdateAsync`. Idempotent |
| `ClearAddressBookOnAnonymisedHandler` | `CustomerAccountAnonymisedEvent` | Load the address book if it exists → `Clear` → `UpdateAsync` |
| `ClearPreferencesOnAnonymisedHandler` | same | same for preferences |

### 11.8 The two audit entries that are not event-driven

- **Sign-in against an identifier that matches no account.** `EntityId = Guid.Empty`,
  `ActorUserId = Guid.Empty`, `Action = "CustomerSignInFailed"` / `"StaffSignInFailed"`,
  `BeforeValue` = the identifier **masked** (mobile: last 3 digits; email: first character and domain).
  Storing the raw string would let a credential-stuffing run fill the audit table with its dictionary.
- **Authorization denial** — C11.

### 11.9 Audit change capture and immutability — US-ACC-12 (Phase A)

`src/Infrastructure/CatEcommerce.Infrastructure/Auditing/` · namespace
`CatEcommerce.Infrastructure.Auditing`.

**`AuditChangeCaptureInterceptor : SaveChangesInterceptor`** — in `SavingChangesAsync`:
1. For every tracked `AggregateRoot` entry in `Added`/`Modified` state (owned entities folded into
   their root), build a before/after JSON diff from `OriginalValues`/`CurrentValues`.
2. **Exclude** `xmin`, `Version`, `LastUpdatedBy/Date`, `Metadata`, `Events`.
3. **Redact fail-closed**: any property whose name matches `/password|hash|token|stamp|secret|answer/i`
   is written as `"[redacted]"`; an explicit allow-list in `AuditRedactionRules` covers the non-secret
   exceptions. A new secret-bearing property is redacted by default.
4. **Personal data**: values are recorded (FR-ACC-12 requires before/after), **except** for
   `CustomerAccountAggregate.Anonymise`, whose after-values are recorded and whose before-values are
   **omitted** — the "before" state of an anonymisation must not survive it (US-ACC-13, D-11).
5. `Action` = the root's pending event type names, else `"<Aggregate>Changed"`.
6. Add one `AuditEntryAggregate` (`DataChange`, IP from `IClientIpAccessor`, actor from the entry's
   `UserContext`) to the **same** context, so it commits in the same `SaveChangesAsync`.
7. Never audit `AuditEntryAggregate` itself.

**`AuditEntryImmutabilityInterceptor`** — throws `AuditEntryImmutableException` (Infrastructure,
programming error) on any `Modified` or `Deleted` `AuditEntryAggregate` before SQL is sent.

**PostgreSQL** (§12.1 migration): `BEFORE UPDATE OR DELETE` row trigger and `BEFORE TRUNCATE`
statement trigger on `audit.audit_entry` that `RAISE EXCEPTION`. The database is the authoritative
layer — "anyone" in US-ACC-12 AC2 includes someone at a `psql` prompt.

> **Integration-test consequence.** The shared-database rule "never `TRUNCATE`" (§16.1) was a
> convention; after this migration it is **enforced**. A test that tries it fails loudly.

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

**`CatEcommerceDbContext`** — add seven `DbSet`s: `CustomerAccounts`, `CustomerSessions`,
`PasswordResetTokens`, `CustomerAddressBooks`, `CustomerNotificationPreferences`, `StaffAccounts`,
`StaffSessions`. Register the two interceptors (§11.9) via `AddInterceptors` in the `AddDbContext`
call. Nothing else changes (F11).

**Configurations** — `Persistence/Configurations/`, one per root, modelled on
`VaccineConfiguration` / `AuditEntryConfiguration`: `ToTable(…, "account")`, `ValueGeneratedNever()`
on every root **and owned** id, the five F15 lines, enumerations **by `Value`**, every
`DateTimeOffset` → `timestamptz` (C-7), owned VOs via `OwnsOne` with explicit column names, primitive
collections as `text[]`.

| Index | Kind | Why |
|---|---|---|
| `customer_account.mobile_number` | **UNIQUE, filtered `WHERE mobile_number IS NOT NULL`**, `ix_customer_account_mobile_number` | ADR-026; the filter lets anonymisation free a number |
| `customer_account.email_address` | **UNIQUE, filtered**, `ix_customer_account_email_address` | Login identifier (FR-ACC-1) |
| `customer_account.additional_phone_numbers` | **GIN** | The duplicates report's match |
| `customer_account.merged_into_customer_id` | non-unique | Follow a retired record |
| `staff_account.mobile_number` / `.email_address` | **UNIQUE, filtered**, `ix_staff_account_*` | ADR-026 per kind |
| `staff_account.role_ids` | GIN | `= ANY(role_ids)` for the last-administrator count |
| `customer_session.token_hash`, `staff_session.token_hash` | **UNIQUE** | The only lookup |
| `customer_session (customer_account_id, expires_on)` | non-unique | "Sessions of this account" |
| `password_reset_token.token_hash` | **UNIQUE** | Confirm lookup |
| `audit_entry (entity_type, entity_id)`, `audit_entry (correlation_id)` | non-unique | Investigation joins |

**Migrations** — one per phase, each **read before commit** and confirmed to touch only what its
phase names: `AddAuditTrailIntegrity` (A: three audit columns, two triggers), `AddCustomerAccount`
(B), `AddCustomerSession` (C), `AddPasswordResetToken` (D), `AddAddressBookAndPreferences` (E),
`AddStaffAccount` (F), `AddStaffSession` (G). Phases H–J add no table. **No migration touches
`clinic.*`** (the merge changes `clinic.cat` rows through the aggregate, not its schema).

### 12.2 Infrastructure services

| Class | Namespace | Notes |
|---|---|---|
| `Configuration/AccountOptions` | `…Infrastructure.Configuration` | Section `Account`: Argon2 parameters (ADR-027), `CustomerSessionLifetimeHours = 168`, `StaffSessionLifetimeHours = 12`, `StaffIdleTimeoutMinutes = 30` (validated 5…240, `ValidateOnStart`), `PasswordResetTokenLifetimeMinutes = 15`, `WebBaseUrl` (for the Development reset link only) |
| `Configuration/CaptchaOptions` | same | §12.6 |
| `Configuration/RateLimitingOptions` | same | §12.7 |
| `Configuration/RequestOriginOptions` | same | Section `RequestOrigin`: `TrustedProxies[]`, `TrustedNetworks[]` — **empty in the committed file** (ADR-040) |
| `Security/Argon2idPasswordHasher` | `…Infrastructure.Security` | Fresh salt per call from `RandomNumberGenerator`; PHC string; `CryptographicOperations.FixedTimeEquals`; the dummy credential built once at startup |
| `Security/LocalListBreachedPasswordChecker` | same | Embedded resource `Security/breached-passwords.txt`, loaded once into a case-insensitive `HashSet`; **no network call**; provenance in the file's header comment (OQ-6) |
| `Security/OpaqueSessionTokenIssuer` | same | 32 bytes `RandomNumberGenerator`, Base64Url; hash = hex SHA-256 |
| `Security/RandomPasswordResetTokenGenerator` | same | 32 bytes; Base64Url SHA-256 hash; lifetime from options |
| `Security/Captcha/*` | `…Security.Captcha` | §12.6 |
| `Notifications/DevelopmentPasswordResetTokenDispatcher` | `…Infrastructure.Notifications` | Logs **`{WebBaseUrl}/reset-password#token={token}`** at `Warning` with a banner line. **Constructor throws unless `IHostEnvironment.IsDevelopment()`** — a token in a production log is a credential in a log |
| `Notifications/UndeliveredPasswordResetTokenDispatcher` | same | Registered outside Development. Logs `"Password reset requested for {AccountKind} {AccountId}; no delivery channel is configured"` — **no token, no destination**. §4.3, §20.4 |
| `Hosting/HttpContextClientIpAccessor` | `…Infrastructure.Hosting` | `HttpContext.Connection.RemoteIpAddress` after the forwarded-headers middleware; IPv4-mapped IPv6 → IPv4; null when no request |
| `Hosting/SessionUserContextProvider` | same | §12.5 |
| `Hosting/DevUserContextProvider` | same (existing) | Edited: reads `DevUser:Roles` (`string[]`, default empty) and is used **only** as ADR-032's fallback |

New NuGet packages — **permitted here, and only these**: `Konscious.Security.Cryptography.Argon2`,
`SkiaSharp`, `SkiaSharp.NativeAssets.Linux.NoDependencies` (Infrastructure);
`Microsoft.Extensions.TimeProvider.Testing` (test projects).

### 12.3 Read side — `src/Read/`

ViewModels are `sealed record`s, namespace `CatEcommerce.Read.ViewModels` (flat). Queries in
`Queries/Account/`, handlers in `QueryHandlers/Account/`. `AsNoTracking()`, **explicit
member-by-member `Select`** before materialising — never an auto-mapped shape, so a new aggregate
property cannot leak. **No projection includes a password credential, token hash, security stamp or
throttle field in any form.**

| # | Query → ViewModel | Used by |
|---|---|---|
| Q-C1 | `GetCurrentCustomerSessionQuery(tokenHash)` → `CustomerSessionViewModel?` (`customerId`, `fullName`, `expiresOn`, `recordStatus`) | `GET /api/auth/session`, the middleware |
| Q-C2 | `GetCustomerProfileQuery(customerId)` → `CustomerProfileViewModel` (`fullName`, `mobileNumber?`, `emailAddress?`, `hasMobileNumber`, `hasEmailAddress`, `canEditEmail`, `additionalPhoneNumbers[]`, `registeredOn`) | `GET /api/customers/me` |
| Q-E1 | `GetDeliveryAddressesQuery(customerId)` → `DeliveryAddressBookViewModel` (`addresses[]` with `isDefault` computed, `maxAddresses`) — absent aggregate → empty book | `GET /api/customers/me/addresses` |
| Q-E2 | `GetNotificationPreferencesQuery(customerId)` → `NotificationPreferencesViewModel`: **every** channel × category cell with `isGranted`, `isEditable`, `isAlwaysOn` from `NotificationPreferenceRules` — the client never hardcodes which cells are locked | `GET /api/customers/me/notification-preferences` |
| Q-F1 | `GetStaffRoleCatalogueQuery` → `StaffRoleCatalogueViewModel` (roles, capabilities, matrix cells) | `GET /api/staff-roles` |
| Q-F2 | `GetStaffAccountsQuery` (paged, `search?`, `roleId?`) → `PagedResult<StaffAccountSummaryViewModel>` `{ items, pageNumber, pageSize, totalCount }` | `GET /api/staff-accounts` |
| Q-F3 | `GetStaffAccountQuery(id)` → `StaffAccountViewModel?` (`hasPassword` **is** included here — an administrator needs to know the holder has not yet set one) | `GET /api/staff-accounts/{id}` |
| Q-G1 | `GetStaffSessionStatusQuery(tokenHash)` → `StaffSessionStatusViewModel?` (`staffAccountId`, `fullName`, `roleIds`, `issuedOn`, `lastActivityOn`, `idleExpiresOn`, `absoluteExpiresOn`, `idleTimeoutMinutes`, `serverTime`) — **never extends** | `GET /api/staff/session` |
| Q-H1 | `GetStaffCustomerQuery(id)` → `StaffCustomerViewModel?` (`recordStatus`, `registrationChannel`, identifiers, `catCount`, `status`, `mergedIntoCustomerId?`) | `GET /api/staff/customers/{id}` |
| Q-H2 | `FindCustomersQuery(mobile? , email?)` → `StaffCustomerSummaryViewModel[]` (exact normalised match on primary **or** additional number, or email; ≤ 10 rows; each row carries `mobileMatchedOn`: `"Primary"` / `"Additional"` / `null`, distinguishing a genuine primary-number match from an unverified additional-number claim — SECURITY gate MEDIUM, §1 Amended (16)) | `GET /api/staff/customers?mobile=` |
| Q-I1 | `GetDuplicateCustomerGroupsQuery` (paged) → groups of ≥ 2 **active** customers linked by §8.9's match rule, each member with its cats (`catId`, `name`, `isDeceased`) and `mobileMatchedOn` (`"Primary"` / `"Additional"` / `null` — same meaning as Q-H2's field, but per member of the group: "Additional" means this member's only phone overlap with the rest of the group is via an unverified additional number, so staff should confirm identity before merging or attaching cats — SECURITY gate LOW, §1 Amended (18)) — **no orders, invoices or balances fields exist** (§4.3) | `GET /api/staff/customers/duplicates` |
| Q-J1 | `GetPersonalDataExportQuery(customerId)` → `PersonalDataExportViewModel` (`schemaVersion: 1`, `exportedOn`, profile, addresses, preferences, cats with their profile fields) | `POST /api/customers/me/personal-data-export` |

Every staff-facing query derives `recordStatus` through `CustomerRecordStatusRules.Derive` **after**
materialising `hasPassword` — never re-implemented in a `CASE`.

### 12.4 Api — controllers and the contract HO-ACC-002 consumes

Controllers inject `IInMemoryDispatcher` and `IUserContextProvider` only (except `CaptchaController`,
ADR-029). Each action mints ids server-side, stamps `UserContext` / `CorrelationId` /
**`ValidateCommand = true`** (F3), dispatches **one** command or query. JSON is camelCase. Request DTOs
in `Contracts/Account/` (namespace `CatEcommerce.Api.Contracts.Account`) are **not** the commands.

**Guards**: `[RequireCustomerSession]` → `401 session_invalid` if the caller is not a customer;
`[RequireAction(Actions.X)]` → `401` if not staff, `403` if staff without the role (§12.8);
`[ServiceFilter(typeof(CaptchaValidationFilter))]`; `[EnableRateLimiting(policy)]`; `[PassiveSession]`
marks a staff endpoint that must not record activity.

| Method | Route | Guard · limit | Command / query | Success |
|---|---|---|---|---|
| `POST` | `/api/captcha` | `captcha-issue` | — | `200 { captchaId, imageDataUrl, expiresInSeconds }`, `Cache-Control: no-store` |
| `POST` | `/api/customers` | CAPTCHA · `registration` | C1 | `201 { customerId }` |
| `POST` | `/api/auth/sign-in` | `sign-in` | C2 | `200 { customerId, expiresOn }` + `Set-Cookie: pc_session`, `pc_csrf` |
| `POST` | `/api/auth/sign-out` | customer | C3 | `204` + expired cookies |
| `GET` | `/api/auth/session` | — | Q-C1 | `200` session · `401 session_invalid` |
| `POST` | `/api/auth/password-reset/request` | `password-reset` | C4 (`Customer`) | **`200` neutral, always** |
| `POST` | `/api/staff/auth/password-reset/request` | `password-reset` | C4 (`Staff`) | **`200` neutral, always** |
| `POST` | `/api/auth/password-reset/confirm` | `password-reset` | C5 | `204` — the user then signs in (no auto sign-in: the link may be opened on a shared device) |
| `GET` | `/api/customers/me` | customer | Q-C2 | `200` |
| `PUT` | `/api/customers/me` | customer · `profile-update` | C6 | `204` |
| `GET` / `POST` | `/api/customers/me/addresses` | customer | Q-E1 / C7 add | `200` / `201 { addressId }` |
| `PUT` / `DELETE` | `/api/customers/me/addresses/{addressId}` | customer | C7 | `204` |
| `PUT` | `/api/customers/me/addresses/{addressId}/default` | customer | C7 | `204` (idempotent) |
| `GET` / `PUT` | `/api/customers/me/notification-preferences` | customer | Q-E2 / C8 | `200` / `204` |
| `POST` | `/api/customers/me/personal-data-export` | customer · `account-sensitive` | C17 then Q-J1 | `200` JSON, `Content-Disposition: attachment; filename="purrcare-my-data.json"`, `Cache-Control: no-store` |
| `POST` | `/api/customers/me/deletion` | customer · `account-sensitive` | C18 | `204` + expired cookies |
| `GET` | `/api/staff-roles` | `StaffAccounts.View` | Q-F1 | `200` |
| `GET` | `/api/staff-accounts` | `StaffAccounts.View` | Q-F2 | `200` paged |
| `GET` | `/api/staff-accounts/{id}` | `StaffAccounts.View` | Q-F3 | `200` / `404` |
| `POST` | `/api/staff-accounts` | `StaffAccounts.Create` | C9 | `201 { staffAccountId }` |
| `PUT` | `/api/staff-accounts/{id}/roles` | `StaffAccounts.ChangeRoles` | C10 | `204` — replaces the whole set |
| `POST` | `/api/staff/auth/sign-in` | `sign-in` | C12 | `200 { staffAccountId, expiresOn }` + `pc_staff_session`, `pc_csrf` |
| `POST` | `/api/staff/auth/sign-out` | staff | C13 | `204` |
| `GET` | `/api/staff/session` | staff · `[PassiveSession]` | Q-G1 | `200` |
| `POST` | `/api/staff/session/activity` | staff | (recorded by the middleware) | `200` status |
| `POST` | `/api/staff/customers` | `Customers.RegisterWalkIn` | C15 | `201 { customerId }` |
| `GET` | `/api/staff/customers?mobile=&email=` | `Customers.View` | Q-H2 | `200` |
| `GET` | `/api/staff/customers/{id}` | `Customers.View` | Q-H1 | `200` / `404` |
| `GET` | `/api/staff/customers/duplicates` | `Customers.Merge` | Q-I1 | `200` paged |
| `POST` | `/api/staff/customers/{duplicateId}/merge` | `Customers.Merge` | C16 | `200 { survivingCustomerId, isReapplication }` |

**Response rules the SECURITY gate checks:**

| Rule | Detail |
|---|---|
| **R1 — one sign-in failure** | Every sign-in failure (unknown identifier, wrong password, locked, not active, no password yet) is `401 { "error": "invalid_credentials", "message": AccountMessages.SignInFailed }` and nothing else. **`ValidationResult` is not forwarded on this path** — the codes are an account-state oracle. The real code is logged at Information with the correlation id. **No `Retry-After` on a `401`** — it would say the account exists and is locked |
| **R2 — shape errors stay `400`** | A missing field is not an authentication outcome |
| **R3 — neutral reset request** | One constant body, `AccountMessages.PasswordResetRequested` (*"If an account matches, we've sent a link to reset the password."*), identical in shape, length and fields on every outcome. Never `404` |
| **R4 — `429` leaks nothing** | Keyed on the request, not the account, so it **must** carry `Retry-After`; body `ValidationResponse`-shaped, `errorCode: TooManyRequests`, no IP, no key |
| **R5 — session failures** | `401 { "error": "session_invalid" \| "session_idle_timeout", "message", "idleTimeoutMinutes"? }`, `Cache-Control: no-store`. `session_idle_timeout` is returned by **every** staff endpoint for an idle session, forever after. **No identity, no timestamps** in either |
| **R6 — `403`** | `ValidationResponse` with one error, `errorCode: ActionNotPermitted`, naming no role and no action |
| **R7 — CSRF** | An unsafe method (`POST`/`PUT`/`DELETE`) presenting a session cookie without a matching `X-CSRF-Token` → `403 { "error": "csrf_token_invalid" }` **before** the action. Sign-in, registration, CAPTCHA and reset endpoints carry no session and are exempt |
| **R8 — never `202`** | ADR-014 |

**`Filters/UniqueConstraintExceptionFilter`** — new, registered **alongside**
`ConcurrencyExceptionFilter`. `PostgresException` `23505`: `ix_customer_account_mobile_number` /
`ix_staff_account_mobile_number` → the **same** `400` as the handler pre-check (`AccountMessages`
constant); `…email_address` → the email equivalent; a primary-key collision on the address-book or
preferences tables → `409`. **Any other constraint → rethrow.**

### 12.5 Sessions and caller resolution — ADR-031, ADR-032

**Cookies** written only by `Api/Sessions/SessionCookieWriter`:

| Cookie | Attributes |
|---|---|
| `pc_session`, `pc_staff_session` | `HttpOnly; Secure; SameSite=Lax; Path=/; Expires` = session `ExpiresOn` |
| `pc_csrf` | **not** HttpOnly (the client must read it); `Secure; SameSite=Lax; Path=/`; 32 random bytes; rotated at every sign-in |

Sign-out, deletion and every `session_*` `401` **expire** the relevant cookies.

**`Api/Sessions/SessionAuthenticationMiddleware`** — placed after `UseForwardedHeaders` and routing,
before the rate limiter and `MapControllers`:
1. If `pc_staff_session` is present: hash it, run Q-G1; for a non-`[PassiveSession]` endpoint
   dispatch C14 in a fresh scope (one retry on concurrency). `Active` → `ResolvedCaller.Staff(id,
   roleIds)`. `IdleExpired` or any failure → R5 **immediately, without calling the endpoint**, and
   expire the cookie.
2. Else if `pc_session` is present: hash, Q-C1 (filters `EndedOn is null`, `ExpiresOn > now` and the
   stamp match **in SQL**). Valid → `ResolvedCaller.Customer(id)`. Invalid → expire the cookie and
   continue **anonymous** (a guest page must still render).
3. Unsafe method with a resolved caller → R7 check.
4. Store the caller in `HttpContext.Items`. **The token and its hash go nowhere else** — not a
   command, not a log, not `Items`.

> **Why staff wins when both cookies are present.** One browser can hold both (a receptionist who is
> also a customer). A staff screen must act as staff; a portal call made in that browser would act as
> staff too, and the customer guard refuses it with `401` — safe, and visible. OQ-3.

**`Hosting/SessionUserContextProvider : IUserContextProvider`** (Scoped, `IHttpContextAccessor`) —
builds `UserContext` from `ResolvedCaller` per ADR-032. `TenantId` from `DevUser:TenantId` as today.
Anonymous → `UserId = Guid.Empty`, no roles, `UserName = "anonymous"`. The Development fallback to
`DevUserContextProvider` applies **only** when no session cookie was presented.

**Startup guard** in `Program.cs`: throw if `DevUser:Enabled` is `true` outside `Development`.

### 12.6 CAPTCHA — ADR-029

`Infrastructure/Security/Captcha/`: `ICaptchaService` (`CaptchaChallenge Issue()`,
`bool Verify(string? id, string? answer)`), `MemoryCacheCaptchaService` (Singleton, **owns** a
`MemoryCache` with `SizeLimit = MaxOutstandingChallenges`; each entry `Size = 1`; never the shared
`IMemoryCache`), `ICaptchaImageRenderer` / `SkiaCaptchaImageRenderer` (Singleton). Api:
`Contracts/ICaptchaProtectedRequest` (`CaptchaId`, `CaptchaAnswer`), `Filters/CaptchaValidationFilter`
(Scoped, via `[ServiceFilter]`), `Controllers/CaptchaController`.

| Rule | Detail |
|---|---|
| Id | 16 bytes `RandomNumberGenerator`, Base64Url |
| Answer | `Length` (default 5) characters from `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` via `RandomNumberGenerator.GetInt32` — no `0/O`, `1/I/L`; never `System.Random` |
| Verify | blank → false; look up then **remove unconditionally, before comparing** — one challenge buys one guess; trim, upper-case, `FixedTimeEquals`; expired/unknown → false with no distinction |
| Render | PNG 200 × 70, glyph/background ≥ 4.5:1 contrast, per-glyph rotation ±20° and offset, 4–6 crossing lines, dot noise. **Not SVG** (machine-readable). No font file committed; if the default typeface renders blank glyphs on the target host → §20.13 |
| Filter | Finds the one `ICaptchaProtectedRequest` argument — **none found → `InvalidOperationException`** (never fail open); blank → `400 CaptchaRequired`; false → `400 CaptchaInvalid`; both on `CaptchaAnswer`, short-circuit so nothing downstream runs; logs path and correlation id, **never id or answer** |
| Consumption | **Every submit that passes the rate limiter consumes the challenge**, including one that then fails on a short password — the client must fetch a new one after any failure. Re-using a solved challenge would let a script solve once and probe many times |
| Test seam | `Captcha:TestAnswer` — when set, `Issue()` uses it; everything else unchanged. Honoured only in `Development`/`Testing`; **startup throws** if set anywhere else. Never in the committed `appsettings.json` |

`CaptchaOptions` (section `Captcha`): `Length = 5` (4–8, `ValidateOnStart`), `TtlSeconds = 120`,
`MaxOutstandingChallenges = 10000`, `TestAnswer` absent.

### 12.7 Rate limiting — ADR-030

`Api/RateLimiting/RateLimitPolicies.cs` (`const string` names), `RateLimitingOptions` (section
`RateLimiting`). `RateLimitPartition.GetFixedWindowLimiter` on `RemoteIpAddress` (null → `"unknown"`),
`QueueLimit = 0`, `RejectionStatusCode = 429`, `OnRejected` writes R4 with `Retry-After` from
`MetadataName.RetryAfter` (else the window).

| Policy | Default | Endpoints |
|---|---|---|
| `captcha-issue` | 20 / 10 min | `POST /api/captcha` |
| `registration` | 5 / 10 min | `POST /api/customers` |
| `sign-in` | 10 / 1 min | both sign-in endpoints |
| `password-reset` | 5 / 60 min | the three reset endpoints |
| `account-sensitive` | 5 / 60 min | export, deletion — partitioned by client IP |
| `profile-update` | 5 / 60 min | `PUT /api/customers/me` — partitioned by the signed-in customer id (falls back to IP only when no session resolves; §1 Amended (18)(f)) |

The limiter runs **before** the CAPTCHA filter, so an attacker over the limit cannot even spend
challenges. Integration tests raise all limits in the factory except in the rate-limit cases.

### 12.8 Authorization — ADR-036

`Api/Authorization/`: `RequireActionAttribute(string action) : TypeFilterAttribute` →
`RequireActionFilter : IAsyncAuthorizationFilter`; `RequireCustomerSessionAttribute`;
`PassiveSessionAttribute` (endpoint metadata). `Actions` constants and **`ActionRoles`** live in
`Shared` (`Common/Constants/`) so the handler guard (C9/C10) reads the same table.

| Action | Roles |
|---|---|
| `StaffAccounts.View`, `StaffAccounts.Create`, `StaffAccounts.ChangeRoles` | `Administrator` |
| `Customers.View` | `Reception`, `Nurse`, `Vet`, `Administrator` |
| `Customers.RegisterWalkIn` | `Reception`, `Administrator` |
| `Customers.Merge` | `Administrator` |

`RequireActionFilter`: caller not staff → R5 `401 session_invalid`; action not in `ActionRoles` →
**deny** (fail closed); no role in common (ordinal, exact) → dispatch C11 **then** return R6 — **the
`403` is returned even if the audit write throws** (logged at `Error`, never an allow, never a `500`).
There is no override, no "Administrator bypasses the table", no wildcard.

### 12.9 Host — `Program.cs`

Additive, in the existing sections. `ValidateScopes`/`ValidateOnBuild` stay **on**.
`AddInMemoryBusServices()` is the only dispatch registration.

- Every command handler, validator (**as `IValidator<T>`**), query handler and event handler in §11
  and §12.3 — **count them**; there is no assembly scanning, and a missing one compiles and throws at
  dispatch. I-A1 proves the count.
- Ports (§8.10) → implementations (§12.2), Scoped except the CAPTCHA services and the token
  issuers/generators (Singleton — stateless or self-owned state) and `TimeProvider.System` (Singleton,
  `TryAdd`).
- Dispatcher: `DevelopmentPasswordResetTokenDispatcher` in Development, else
  `UndeliveredPasswordResetTokenDispatcher`.
- `IUserContextProvider` → **`SessionUserContextProvider`** (replacing the `DevUserContextProvider`
  line); `DevUserContextProvider` registered as its concrete type for the fallback; `AddHttpContextAccessor()`.
- Options: `Account`, `Captcha`, `RateLimiting`, `RequestOrigin` — `ValidateOnStart` where §12 says so.
- `AddRateLimiter` (§12.7); `Configure<ForwardedHeadersOptions>` per ADR-040 (clear both known
  lists, then fill from `RequestOrigin`).
- MVC filters: `UniqueConstraintExceptionFilter` alongside `ConcurrencyExceptionFilter`.
  `CaptchaValidationFilter` and `RequireActionFilter` are resolved per action, **never global**.
- Pipeline order: `UseForwardedHeaders()` → *(platform pipeline)* → `UseRouting()` →
  `SessionAuthenticationMiddleware` → `UseRateLimiter()` → `MapControllers()`. I-C9 and I-A7 prove
  the order; if they fail, fix the order, do not delete the test.
- Startup guards: `Captcha:TestAnswer` and `DevUser:Enabled` outside Development/Testing → throw.

**Do not register**: `IInMemoryDispatcher`, `IDispatcher`, `IResolver`, `IHandlerResolver`,
`ICommandSender`, `IQueryProcessor`, `IValidationService`, `IValidationProvider`, `IEventPublisher`,
`IBusMessageDispatcher`. **No `AddAuthentication`/`AddAuthorization`.**

`appsettings.json` gains `Account`, `Captcha`, `RateLimiting`, `RequestOrigin` and `DevUser:Enabled:
false` — **no secret, no `TestAnswer`**. Mirror them into `appsettings.Development.json.example`.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the twelve enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/BusinessViolationCodes.cs            ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/{PasswordRules,LoginThrottleRules,StaffRoles,StaffRoleCatalogue,Actions,ActionRoles,DeliveryAddressRules,CustomerProfileRules,AnonymisationRules,AccountMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Helpers/{CustomerRecordStatusRules,SessionStateRules,NotificationPreferenceRules}.cs
src/Shared/CatEcommerce.Shared/SharedDto/Account/**
src/Domain/CatEcommerce.Domain/Aggregates/Account/**
src/Domain/CatEcommerce.Domain/Aggregates/Audit/AuditEntryAggregate.cs               ← §8.11 only
src/Domain/CatEcommerce.Domain/Aggregates/Cat/CatAggregate.cs                        ← ReassignOwnerOnCustomerMerge only
src/Domain/CatEcommerce.Domain/Entities/{DeliveryAddress,MarketingConsent}.cs
src/Domain/CatEcommerce.Domain/ValueObjects/{MobileNumber,EmailAddress,PasswordCredential,LoginThrottle,AccountIdentifier}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{the six ports in §8.10}.cs
src/Domain/CatEcommerce.Domain/Models/**                                             ← new DTOs; AuditEntryDto two parameters
src/Domain/CatEcommerce.Domain/Events/Account/**
src/Domain/CatEcommerce.Domain/Events/Cat/CatOwnerReassignedOnMergeEvent.cs
src/Application/CatEcommerce.Application/Commands/Account/**
src/Application/CatEcommerce.Application/CommandHandlers/Account/**
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Account/**
src/Application/CatEcommerce.Application/DomainEventHandlers/Cat/AuditCat*Handler.cs ← Category + IP only
src/Application/CatEcommerce.Application/DataMappers/{CustomerAccountMapper,StaffAccountMapper,AddressBookMapper,NotificationPreferencesMapper}.cs
src/Read/CatEcommerce.Read/Queries/Account/**
src/Read/CatEcommerce.Read/QueryHandlers/Account/**
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.3}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs  ← DbSets + interceptors
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{one per new root}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/AuditEntryConfiguration.cs ← three columns + indexes
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**              ← the seven in §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Auditing/**
src/Infrastructure/CatEcommerce.Infrastructure/Configuration/{AccountOptions,CaptchaOptions,RateLimitingOptions,RequestOriginOptions}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Security/**
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/**
src/Infrastructure/CatEcommerce.Infrastructure/Hosting/{HttpContextClientIpAccessor,SessionUserContextProvider,DevUserContextProvider}.cs
src/Infrastructure/CatEcommerce.Infrastructure/CatEcommerce.Infrastructure.csproj     ← the three packages + embedded resource only
src/Api/CatEcommerce.Api/Contracts/Account/**
src/Api/CatEcommerce.Api/Contracts/Captcha/**
src/Api/CatEcommerce.Api/Contracts/ICaptchaProtectedRequest.cs
src/Api/CatEcommerce.Api/Controllers/{Captcha,Customers,Auth,CustomerProfile,StaffAccounts,StaffRoles,StaffAuth,StaffSession,StaffCustomers}Controller.cs
src/Api/CatEcommerce.Api/Filters/{UniqueConstraintExceptionFilter,CaptchaValidationFilter}.cs
src/Api/CatEcommerce.Api/Authorization/**
src/Api/CatEcommerce.Api/Sessions/**
src/Api/CatEcommerce.Api/RateLimiting/**
src/Api/CatEcommerce.Api/Program.cs                                                   ← §12.9 only
src/Api/CatEcommerce.Api/appsettings.json                                             ← the five sections only
src/Api/CatEcommerce.Api/appsettings.Development.json.example
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
tests/*/**.csproj                                                                     ← TimeProvider.Testing only
docs/test-cases/TC-HO-ACC-001.md
docs/gates/HO-ACC-001-*.md
.ai/decisions/adr.md                                                                  ← ADR-024…041, append only
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md` | Governing documents. The `Account` context entry is OQ-4, a separate task |
| `.claude/**` | Agent and gate definitions |
| `docs/SRS.md`, `docs/USER_STORIES.md` | Requirements are an input. A conflict is a §20 stop |
| `docs/handover/HO-CAT-00*.md`, `docs/handover/HO-ACC-002.md` | Other handovers — disagree in a finding, not an edit |
| `web/**` | HO-ACC-002 and HO-CAT-002 |
| `src/Domain/…/Aggregates/Cat/**` other than the one method; `Aggregates/Vaccination/**` | Shipped. `OwnerId` stays unvalidated |
| `src/Api/…/Controllers/{Cats,Vaccines,…}Controller.cs` | HO-CAT-001's endpoints stay unguarded (§4.3) |
| `src/Api/…/Filters/ConcurrencyExceptionFilter.cs` | Add a new filter; do not widen this one |
| `src/Api/…/appsettings.Development.json` | Git-ignored; holds the database password (NFR-SEC-7) |
| `NuGet.Config`, `global.json`, `CatEcommerce.sln`, `.config/dotnet-tools.json` | Build configuration |
| Any other bounded context | Appointment, Visit, Order, Invoice, Notification… |

---

## 15. Unit test cases

`tests/CatEcommerce.Domain.Tests` and `tests/CatEcommerce.Application.Tests` (using the existing
`TestSupport/FakeAggregateRootRepository`). Rows that exercise `Infrastructure` or `Api` classes go in
`tests/CatEcommerce.IntegrationTests` as plain unit tests — **do not** add an `Infrastructure`
reference to `Application.Tests`. The test-engineer turns each row into a sheet row.

| ID | Case | Expected |
|---|---|---|
| **Foundation & audit (A)** | | |
| U-A1 | `MobileNumber.Create` with `01712345678`, `+880 1712-345678`, `008801712345678` | All `+8801712345678`, equal, same hash code |
| U-A2 | `MobileNumber.Create` with `""`, `"abc"`, `"0171234"`, 25 digits | `InvalidMobileNumber` |
| U-A3 | `EmailAddress.Create("  Foo@Example.COM ")`; `"a@b"`, `"@x.com"` | `foo@example.com`; `InvalidEmailAddress` |
| U-A4 | `LoginThrottleRules.DelayFor(1…20)` | Monotonic non-decreasing, 0 for 1–2, capped at 900 s |
| U-A5 | Reflect over `CustomerAccountAggregate` after `Anonymise` | Every public string is null, the placeholder, or on the short allow-list — **fails when a new personal field is added and not cleared** |
| U-A6 | `AuditEntryAggregate.Record` twice | `AuditEntryAlreadyRecorded` |
| U-A7 | Reflect over `AuditEntryAggregate` | `Record` is the only public mutator |
| U-A8 | Change-capture on a modified `CatAggregate` (in-memory SQLite not allowed — use the integration DB, so this row lives in `IntegrationTests`) | One `DataChange` row, correct before/after, `xmin` excluded |
| U-A9 | Change-capture on a `CustomerAccountAggregate` with a credential | `password_hash`, `security_stamp` written as `"[redacted]"` |
| U-X1 | Reflect over every `Event` type in `CatEcommerce.Domain` | No property named like `/name|mobile|phone|email|address|line|password|hash|token|stamp/i` except the allow-listed booleans and ids |
| **Registration (B)** | | |
| U-B1 | `Register` valid | Properties set; `SelfService`, `Active`, stamp non-empty; one event; version bumped |
| U-B2 | `Register` with neither identifier | `IdentifierRequired` |
| U-B3 | Reflect: `CustomerAccountAggregate`, its DTOs and ViewModels | No `Verif*`, `Otp*`, `TwoFactor*`, `Captcha*` member |
| U-B4 | Validator: 9 vs 10-character password | `PasswordTooShort` on `Password` / passes |
| U-B5 | Validator: `"hunter2-hunter2"` short-circuit messages | Submitted value appears in no message |
| U-B6 | Handler: mobile exists | `MobileNumberAlreadyRegistered`, message mentions log in and reset, **contains no "email"**; hasher never called |
| U-B7 | Handler: email exists, mobile not supplied | `EmailAddressAlreadyRegistered`, message contains no "mobile"/"phone" |
| U-B8 | Handler: breached | `BreachedPassword`; no hash, no save |
| U-B9 | Captcha service: 1 000 issues | Unique ids; answers from the alphabet, correct length |
| U-B10 | Captcha verify: right answer different case/spaces; replay after right; replay after wrong; after TTL (`FakeTimeProvider`); null/blank | true; false; false; false; false — no exception |
| U-B11 | Captcha overflow beyond `MaxOutstandingChallenges` | No exception, bounded; overflow fails verify |
| U-B12 | Renderer | PNG signature, 200 × 70, answer not in the bytes |
| U-B13 | `CaptchaValidationFilter` on an action without `ICaptchaProtectedRequest` | `InvalidOperationException` |
| U-B14 | `Argon2idPasswordHasher.Hash` twice | Different encodings; neither contains the plaintext; `Verify` true for each |
| U-B15 | `LocalListBreachedPasswordChecker` | Known common password → true; random 20 chars → false; case-insensitive |
| **Sign-in & sessions (C, G)** | | |
| U-C1 | `RecordFailedLogin` ×3 at t | `LockedUntil = t + 5 s`; event outcome and count |
| U-C2 | `RecordSuccessfulLogin` while locked | `AccountLocked` |
| U-C3 | `RecordSuccessfulLogin` after lock expiry | Throttle reset |
| U-C4 | `EnsureCanSignIn` on merged / anonymised / no credential | `AccountNotActive` / `AccountNotActive` / `InvalidCredentials` |
| U-C5 | Handler: unknown identifier | `VerifyAgainstDummy` called once; masked audit entry written; `InvalidCredentials` |
| U-C6 | Handler: wrong password | `UpdateAsync` on the account **called**; no session saved |
| U-C7 | Handler: success | Throttle reset + session saved with the account's stamp; result carries a token whose hash equals the saved hash |
| U-C8 | `SessionStateRules.Classify` truth table (ended, expired, revoked, idle, active) | Each state; boundary `asOf == ExpiresOn` → `Expired` |
| U-G1 | Staff `RecordActivity` 30 s after last | No change; 61 s → advanced |
| U-G2 | `EndForIdleTimeout` | `EndedOn = LastActivityOn + idle`; second call throws |
| U-G3 | Idle-expired session after the configured period is lengthened | Still `Ended` — terminal |
| U-G4 | Reflect over both session aggregates | No `Revive`/`Extend`/`Reactivate` |
| U-G5 | Staff sign-in handler with customer credentials | `InvalidCredentials` — the customer repository is never queried |
| **Reset (D)** | | |
| U-D1 | `EnsureUsable` expired / superseded / fresh | `…Expired` / `…Superseded` / ok |
| U-D2 | `SetPassword` | Stamp rotated; throttle reset; `WasFirstPassword` correct |
| U-D3 | Request handler: unknown, merged, anonymised, found | All `IsSuccess = true`, null `ValidationResult`; only "found" saves and dispatches; generator called on **every** path |
| U-D4 | Confirm handler: each token failure | Single `InvalidPasswordResetToken` |
| U-D5 | Confirm with a breached new password | `BreachedPassword`; account unchanged; token still usable |
| U-D6 | `DevelopmentPasswordResetTokenDispatcher` constructed in `Production` | Throws |
| **Profile, addresses, preferences (E)** | | |
| U-E1 | `UpdateContactDetails` on an email-only account changing the email | `PrimaryIdentifierReadOnly` |
| U-E2 | 4 additional numbers; one equal to the primary | `AdditionalPhoneNumberLimitExceeded`; `DuplicatePhoneNumber` |
| U-E3 | No change | No event, no version bump |
| U-E4 | First address becomes default; remove default with others present; remove last | default set; `DefaultAddressCannotBeRemoved`; default cleared |
| U-E5 | 200 random add/update/remove/set-default operations | `EnsureDefaultIsConsistent` never throws |
| U-E6 | 11th address | `DeliveryAddressLimitExceeded` |
| U-E7 | `ChangePreferences` with a transactional cell | `NotificationPreferenceNotEditable`, **nothing applied** |
| U-E8 | `NotificationPreferenceRules` full truth table over `GetAll()` | Transactional always permitted; marketing only when granted; default not granted |
| **Staff & authorization (F)** | | |
| U-F1 | `Create` with `[" Vet", "Vet", "Nurse"]` | `["Nurse", "Vet"]` in catalogue order |
| U-F2 | Empty roles; `"vet"` lower-case; `"Groomer"` | `StaffRolesRequired`; `UnknownStaffRole`; `UnknownStaffRole` |
| U-F3 | `ChangeRoles` to the same set | No event, no version bump, stamp unchanged |
| U-F4 | `ChangeRoles` to a different set | Stamp rotated; event with both sets |
| U-F5 | `StaffRoleCatalogue` | 8 roles × 20 capabilities; no `Customer` role |
| U-F6 | `RequireActionFilter`: unmapped action | Denied |
| U-F7 | `RequireActionFilter`: audit write throws | Still `403`, no exception escapes |
| U-F8 | C10 removing `Administrator` from the last holder | `LastAdministratorCannotBeRemoved` |
| U-F9 | C9 handler guard with a `UserContext` lacking `Administrator` | `ActionNotPermitted` |
| **Walk-in (H)** | | |
| U-H1 | `RegisterWalkIn` | `Reception`, no credential, no email, `RecordStatus == Incomplete` |
| U-H2 | Then `SetPassword` | `RecordStatus == Complete`, channel still `Reception` |
| U-H3 | Reflect over `WalkInCustomerRegistrationDto` | No credential, email, channel or status field |
| **Merge (I)** | | |
| U-I1 | `MergeInto` self; blank reason; anonymised | Each code |
| U-I2 | Match: shared additional number; shared email; both emails null and no shared number | ok; ok; `CustomersNotDuplicates` |
| U-I3 | Merge then re-send same survivor; then a different survivor | Re-application event, state unchanged; `CustomerAlreadyMerged` |
| U-I4 | `ReassignOwnerOnCustomerMerge` on a deceased cat | Moves; `EnsureAlive` not called |
| U-I5 | Wrong owner | `CatOwnerMismatch` |
| **Deletion & export (J)** | | |
| U-J1 | `Anonymise` | Fields per §8.10a; stamp rotated; status; event carries ids only |
| U-J2 | `Anonymise` twice | `AccountAlreadyAnonymised` |
| U-J3 | Delete handler while locked | `AccountLocked`; hasher **not** called |
| U-J4 | Delete handler wrong password | `RecordFailedLogin` persisted; `ReauthenticationFailed` |

## 16. Integration test cases

`tests/CatEcommerce.IntegrationTests` — real HTTP through `WebApplicationFactory<Program>` in
environment `Testing`, against the **shared** `catecommerce_dev`. `PASS` means you watched it pass.

### 16.1 Writing for a shared, non-empty database

1. **Every test mints its own ids, its own mobile number and its own email** — e.g.
   `"01" + Random.Shared.NextInt64(100_000_000, 999_999_999)` retried on collision, and
   `$"t-{Guid.NewGuid():N}@example.test"`. A fixed literal passes once and fails for ever.
2. **Never assert on a global count or emptiness.**
3. **No test depends on another's data or order.** Do not disable parallelism to hide shared state.
4. **Never `TRUNCATE`, `DROP` or clean** — after Phase A the triggers refuse it anyway.
5. **CAPTCHA**: the factory sets `Captcha:TestAnswer`; a helper calls `POST /api/captcha` per
   registration. Do not replace `ICaptchaService` with an always-pass fake.
6. **Rate limits** are raised in the factory except in the rate-limit cases.
7. **Staff callers**: a helper seeds a staff account through the repository, sets its password
   through `SetPassword`, and signs in through `POST /api/staff/auth/sign-in` — so tests exercise the
   real cookie path. `DevUser:Enabled` is **false** in `Testing`.
8. A cookie-aware `HttpClient` helper echoes `pc_csrf` into `X-CSRF-Token`; R7 cases deliberately omit it.

| ID | Case | Expected |
|---|---|---|
| **A — foundation & audit** | | |
| I-A1 | Host starts with `ValidateScopes` + `ValidateOnBuild` | No exception — every registration resolves; no broker type needed |
| I-A2 | Host in `Production` with `Captcha:TestAnswer` set; with `DevUser:Enabled=true` | Startup fails, each with a clear message |
| I-A3 | Migrations on an empty database | Schema `account` with every table, index and filter in §12.1; `audit.audit_entry` has `ip_address`, `category`, `correlation_id` and both triggers; `clinic.*` unchanged |
| I-A4 | `UPDATE`, `DELETE`, `TRUNCATE` on `audit.audit_entry` through Npgsql | Each raises |
| I-A5 | `UpdateAsync` on an `AuditEntryAggregate` | `AuditEntryImmutableException`, nothing sent |
| I-A6 | Register a cat through the shipped endpoint | A `DataChange` row in the same transaction; IP recorded |
| I-A7 | Request with `X-Forwarded-For: 203.0.113.9` and no trusted proxy | Audit IP is the direct peer, not the header |
| I-A8 | A handler throws after the change-capture row was added | Neither the change nor its audit row is committed |
| **B — registration** | | |
| I-B1 | Register with name, mobile, 12-char password, CAPTCHA | `201`; row exists; no verification column anywhere in the table |
| I-B2 | Register with email only | `201`; login by email works (I-C2) |
| I-B3 | Neither identifier | `400 IdentifierRequired` |
| I-B4 | Same mobile, different format | `400 MobileNumberAlreadyRegistered`; body contains no "email"; one row |
| I-B5 | Same email, different case | `400 EmailAddressAlreadyRegistered`; body contains no "mobile"/"phone" |
| I-B6 | 9-char password → then breached ≥ 10-char | `PasswordTooShort` → `BreachedPassword`, distinct messages. **A `201` on the first means F3 was missed** |
| I-B7 | No CAPTCHA fields; wrong answer | `400 CaptchaRequired`; `400 CaptchaInvalid`; no row, no audit, hasher spy never called |
| I-B8 | Replay a solved challenge | `400 CaptchaInvalid` |
| I-B9 | `registration` limit 2: three posts | Third `429` + `Retry-After`; no IP in body or log |
| I-B10 | Two concurrent registrations, same mobile | One `201`, one `400` — never `500`/`409`, never two rows |
| I-B11 | Persisted row | `password_hash` is argon2id PHC, not containing the plaintext |
| I-B12 | `POST /api/captcha` | `200`, PNG data URL, `no-store`, no test answer in the body |
| **C — customer sign-in** | | |
| I-C1 | Sign in by mobile | `200`; `pc_session` HttpOnly+Secure+SameSite=Lax; `pc_csrf` present; `GET /api/auth/session` → `200` |
| I-C2 | Sign in by email | Same |
| I-C3 | Unknown identifier vs wrong password | **Byte-identical** `401` bodies; no `Retry-After` |
| I-C4 | Three wrong then the right password within 5 s | `401` on the fourth (locked), audited as `AccountLocked` server-side, same public body |
| I-C5 | Every attempt in I-C3/I-C4 | One `AuthenticationEvent` audit row each; unknown identifier masked |
| I-C6 | Sign out, then `GET` session | `204`; `401 session_invalid`; cookie expired |
| I-C7 | `PUT /api/customers/me` without `X-CSRF-Token` | `403 csrf_token_invalid`; nothing changed |
| I-C8 | A tampered / random `pc_session` on a guest endpoint | Treated as anonymous; the page's endpoint still answers |
| I-C9 | `sign-in` limit 2: three attempts | Third `429` — proves the limiter sits after the middleware and before the action |
| **D — reset** | | |
| I-D1 | Request for unknown / known customer | **Identical** `200` bodies |
| I-D2 | Known: dispatcher spy | Received one token; its hash is in `password_reset_token` |
| I-D3 | Confirm with it, 12-char new password | `204`; old password fails; new succeeds |
| I-D4 | The same token again; an older second token | Both `400 InvalidPasswordResetToken` |
| I-D5 | A session created before the reset | `401 session_invalid` after — **US-ACC-05 AC3** |
| I-D6 | Expired token (`FakeTimeProvider`) | `400 InvalidPasswordResetToken`, same message as I-D4 |
| I-D7 | Staff reset request via the staff route; walk-in reset | Tokens carry `AccountKind` Staff / Customer; confirm sets the right account's password |
| **E — profile** | | |
| I-E1 | `GET /api/customers/me` | Profile of the **signed-in** customer; no credential fields (`/pass|hash|stamp|token|locked/i`) |
| I-E2 | Add three addresses, set the second default, remove the first | Exactly one `isDefault` at every step |
| I-E3 | Remove the default while others exist | `400 DefaultAddressCannotBeRemoved` |
| I-E4 | Two concurrent adds at nine addresses | One succeeds, one `409` — never eleven |
| I-E5 | Two concurrent first writes (fresh customer) | One `2xx`, one `409` |
| I-E6 | `GET` preferences before any write | Every cell; transactional `isAlwaysOn`, marketing not granted |
| I-E7 | `PUT` a transactional cell off | `400`, nothing applied |
| I-E8 | Any `/me` endpoint as a guest or as staff | `401` |
| **F — staff & authorization** | | |
| I-F1 | Administrator creates a staff account with roles | `201`; roles stored in catalogue order; `hasPassword: false` |
| I-F2 | A `Nurse` calls `POST /api/staff-accounts` directly | `403 ActionNotPermitted`; one `AuthorizationDenied` audit row with method, path, roles, IP — **US-ACC-07 AC2** |
| I-F3 | A customer session calls it | `401` |
| I-F4 | Replace roles | `204`; the member's existing staff session → `401` on next request (stamp rotated) |
| I-F5 | `GET /api/staff-roles` | 8 roles, 20 capabilities |
| I-F6 | Remove `Administrator` from the last administrator | `400 LastAdministratorCannotBeRemoved` |
| **G — staff sign-in & idle** | | |
| I-G1 | New staff member: set password via reset, sign in | `200` + `pc_staff_session`; `GET /api/staff/session` returns roles — **US-ACC-14** |
| I-G2 | Customer credentials on staff sign-in | Same `401` as I-C3 |
| I-G3 | Idle beyond `StaffIdleTimeoutMinutes` (`FakeTimeProvider`) | `401 session_idle_timeout` from a staff endpoint, the heartbeat and the status endpoint, **forever after** |
| I-G4 | The refused request was a `POST /api/staff/customers` | No customer row was created — no side effect |
| I-G5 | Idle expiry audited | Exactly one `StaffSessionIdleExpired` row even with three concurrent refused requests |
| I-G6 | `GET /api/staff/session` repeatedly | `lastActivityOn` unchanged |
| I-G7 | Sign in again after idle | New session works; `staffAccountId` equal to before |
| **H — walk-in** | | |
| I-H1 | Reception creates a walk-in | `201`; `GET` shows `recordStatus: "Incomplete"`, `registrationChannel: "Reception"` |
| I-H2 | Duplicate mobile | `400 MobileNumberAlreadyRegistered`; `GET /api/staff/customers?mobile=` finds the existing record |
| I-H3 | Walk-in completes via reset, then signs in | `recordStatus: "Complete"`; sign-in `200` |
| I-H4 | The walk-in tries self-registration with the same mobile | `400 MobileNumberAlreadyRegistered` with the reset offer |
| **I — merge** | | |
| I-I1 | Two customers sharing an additional number, each with a cat (one deceased) | Listed together in duplicates |
| I-I2 | Merge | `200`; duplicate `Merged`; **both** cats on the survivor; merge + two reassignment `BusinessEvent` rows; duplicate's session `401` |
| I-I3 | Re-send | `200 isReapplication: true`; state unchanged; a cat registered on the duplicate id after the merge now moved |
| I-I4 | Non-duplicates | `400 CustomersNotDuplicates` |
| I-I5 | As `Reception` | `403` |
| **J — export & deletion** | | |
| I-J1 | Export | `200` attachment, `schemaVersion: 1`, profile, addresses, preferences, cats; `no-store`; an export audit row |
| I-J2 | Delete with the right password | `204`; cookies expired; row `Anonymised` with placeholder name and null identifiers; address book and preferences cleared; cats still reference the id; sign-in fails with the generic `401` |
| I-J3 | A new registration with the deleted mobile | `201` — the filtered index freed it |
| I-J4 | Delete with the wrong password ×3 | `ReauthenticationFailed` then the account locks — the same throttle as sign-in |
| I-J5 | Audit for the deletion | Actor = the customer; the `DataChange` row has no before-values for the anonymised fields |
| **Cross-cutting** | | |
| I-X1 | Capture logs across every B, C, D, J case | No password, token, token hash, CAPTCHA answer or unmasked identifier in any line at any level |
| I-X2 | Scan every response body in this suite | No field matching `/pass|hash|stamp|secret/i` |

---

## 17. Implementation order and verification

Build **phase by phase**. Each phase: code → its unit tests green → its migration generated **and
read** → its integration tests green. Record each phase's commit in the test sheet.

| Phase | Stories | Delivers |
|---|---|---|
| **A** | US-ACC-12 + foundation | Restore packages (§5 — stop on failure); enums, constants, codes; VOs; ports and implementations; audit extensions, interceptors, triggers, IP accessor, forwarded headers; rate limiter shell; CAPTCHA; `UniqueConstraintExceptionFilter`; update the three `AuditCat*Handler`s |
| **B** | US-ACC-01 | Customer account, registration |
| **C** | US-ACC-04 | Customer session, sign-in/out, middleware, `SessionUserContextProvider`, CSRF |
| **D** | US-ACC-05 | Reset tokens, dispatchers, both request routes, confirm |
| **E** | US-ACC-06 | Profile, address book, preferences |
| **F** | US-ACC-07 | Staff account, catalogue, `ActionRoles`, `RequireActionFilter`, denial audit |
| **G** | US-ACC-14, US-ACC-08 | Staff session, sign-in/out, idle expiry, status and heartbeat |
| **H** | US-ACC-10 | Walk-in, staff customer lookup |
| **I** | US-ACC-11 | Merge, duplicates report, Cat method, reassignment handler |
| **J** | US-ACC-13 | Export, deletion, clearing handlers |

```bash
dotnet restore CatEcommerce.sln          # Argon2, SkiaSharp, TimeProvider.Testing resolve — §20.1, §20.13
dotnet build CatEcommerce.sln            # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add <PhaseMigration> --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output-dir Persistence/Migrations
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
dotnet run --project src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.IntegrationTests
```

Environment: add the five sections to the committed `appsettings.json`; locally, a developer may set
`DevUser:Enabled: true`, `Captcha:TestAnswer` and `Account:WebBaseUrl` in the git-ignored
`appsettings.Development.json` — **never a secret in the committed file** (§20.9).

---

## 18. Acceptance criteria

Each row is checked against its evidence. **A row with no evidence is not met.**

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | ACC-01 | A guest with name, mobile and/or email, password and a solved CAPTCHA gets a ready-to-use account — no verification state exists | U-B1, U-B3, I-B1, I-B2 |
| A2 | ACC-01 | Neither identifier → told one is required | U-B2, I-B3 |
| A3 | ACC-01 | Missing/wrong/expired/used CAPTCHA refused before any account check; a fresh challenge is always obtainable | U-B10, I-B7, I-B8, I-B12 |
| A4 | ACC-01 | Over the limit → `429` with retry time | I-B9 |
| A5 | ACC-01 | Duplicate mobile → in use, offered log in/reset, **no mention of email** | U-B6, I-B4 |
| A6 | ACC-01 | Duplicate email → in use, offered log in/reset, **no mention of mobile** | U-B7, I-B5 |
| A7 | ACC-01 | Short and breached passwords rejected with **distinct** reasons | U-B4, U-B8, I-B6 |
| A8 | ACC-01 | Concurrent duplicate registration → one account, one `400` | I-B10 |
| A9 | ACC-04 | Sign-in by mobile or by email creates a session; no OTP path exists | I-C1, I-C2, U-B3 |
| A10 | ACC-04 | Unknown identifier and wrong password are indistinguishable | U-C5, I-C3 |
| A11 | ACC-04 | Delays increase progressively; attempts are logged | U-A4, U-C1, I-C4, I-C5 |
| A12 | ACC-05 | Reset request is neutral; token sent to the registered channel | U-D3, I-D1, I-D2 |
| A13 | ACC-05 | Used or expired token rejected; another can be requested | U-D1, U-D4, I-D4, I-D6 |
| A14 | ACC-05 | Successful reset invalidates all other sessions | U-D2, I-D5 |
| A15 | ACC-06 | Multiple labelled addresses, exactly one default | U-E4, U-E5, I-E2, I-E3, I-E4 |
| A16 | ACC-06 | Marketing opt-outs honoured; transactional cannot be turned off | U-E7, U-E8, I-E6, I-E7 |
| A17 | ACC-07 | Administrator assigns one or more Appendix D roles | U-F1, U-F5, I-F1, I-F5 |
| A18 | ACC-07 | Non-administrator refused server-side with `403`, attempt logged | U-F6, U-F7, I-F2 |
| A19 | ACC-14 | Staff sign in with mobile/email + password; session carries roles | I-G1 |
| A20 | ACC-14 | New staff set their first password via reset | I-D7, I-G1 |
| A21 | ACC-14 | Customer credentials never yield a staff session | U-G5, I-G2 |
| A22 | ACC-08 | Idle beyond the configured period → re-authentication required, terminally | U-G2, U-G3, I-G3 |
| A23 | ACC-08 | A refused request had no side effect; the status endpoint returns `staffAccountId` for same-person restore | I-G4, I-G6, I-G7 |
| A24 | ACC-10 | Walk-in with name and phone is created *incomplete* | U-H1, I-H1 |
| A25 | ACC-10 | *(provisional on SRS Q-16)* Setting a password completes the record and enables sign-in | U-H2, I-H3 |
| A26 | ACC-11 | Records sharing a phone or email are listed with their cats | I-I1 |
| A27 | ACC-11 | Merge moves all cats (deceased included) and is audited; re-send recovers | U-I3, U-I4, I-I2, I-I3 |
| A28 | ACC-12 | Every state-changing action records actor, action, entity, before/after, timestamp and IP, in the same transaction | U-A8, I-A6, I-A7, I-A8 |
| A29 | ACC-12 | Editing or deleting an audit entry is refused — application and database | U-A6, U-A7, I-A4, I-A5 |
| A30 | ACC-13 | Deletion anonymises personal data and ends sign-in | U-J1, I-J2 |
| A31 | ACC-13 | Clinical records retained, owner shown anonymised | I-J2 |
| A32 | ACC-13 | Export is machine-readable | I-J1 |
| A33 | ACC-13 | Deletion is audited with who and when | I-J5 |
| A34 | NFR-SEC-1 | Passwords are Argon2id; no plaintext anywhere | U-B14, I-B11, I-X1, I-X2 |
| A35 | NFR-SEC-4 | Session cookies are HttpOnly/Secure/SameSite; unsafe requests require the CSRF token | I-C1, I-C7 |
| A36 | — | Host starts with scope validation and every registration; test seams cannot reach production | I-A1, I-A2 |

**Deferred, named in §4.3**: reset delivery outside Development; orders/invoices/balances in the
duplicates report and merge; audit query/export; role guards on the Cat endpoints.

---

## 19. Required gates

In order. Each writes `docs/gates/HO-ACC-001-<GATE>.md` **before** reporting.

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | The `Account` context (ADR-024, OQ-4); ports placement (ADR-025); the two recorded rule-1 exceptions (ADR-033, ADR-040) and the merge's event-driven shape (ADR-039); `CaptchaController`'s recorded exception (ADR-029); no auth framework; §13/§14 scope; ADR-024…041 recorded |
| 2 | SECURITY | `security-reviewer` | **The central gate.** Argon2id parameters; plaintext lifetime; R1–R8; cookie attributes and CSRF; caller resolution and the Development fallback guard; CAPTCHA (single use, CSPRNG, fixed-time, bounded, fails closed, test-answer guard); rate-limit order; `ActionRoles` fail-closed and denial audit; audit redaction and immutability; the disclosure trade-off (ADR-028); **named gaps: unguarded Cat endpoints, reset delivery, proxy trust** |
| 3 | CODE_REVIEW | `code-reviewer` | `.ai/` conventions; shared message constants on two-path errors; explicit read projections; handler shape |
| 4 | TEST | `test-engineer` | Writes `TC-HO-ACC-001.md` from §15/§16 **first**, then executes by phase and marks each row as observed. §16.1 rules are mandatory |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row against evidence; A25 marked provisional; §4 confirmed — nothing withdrawn was built |

---

## 20. BLOCKED conditions — stop and report, do not work around

**20.1 Argon2 does not restore.** Never hand-roll a hash or fall back to SHA-*, MD5, unsalted or
low-iteration PBKDF2. `BCrypt.Net-Next` at cost ≥ 12 is the approved alternative — **ask first**; it
changes ADR-027 and introduces 72-byte truncation that `PasswordRules.MaximumLength` must handle.

**20.2 A plaintext secret needs a second home.** Passwords, reset tokens, session tokens and CAPTCHA
answers exist on their command (or cookie) and in the stack frame that uses them — nowhere else. If
you need one in a second place, the design is wrong; stop. U-B5, I-X1, I-X2 failing is a
security-gate BLOCKER, not a fixable finding.

**20.3 Do not build anything withdrawn** (§4.2) — no verification field "for later", no OTP, no 2FA.

**20.4 Reset delivery does not exist outside Development.** Do not add an SMS or email provider, an
outbox or a Notification context. State in the SECURITY and ACCEPTANCE reports that walk-in completion
and staff first-password therefore work only in Development until the Notification context lands.

**20.5 Do not guard the Cat endpoints** (§4.3), and do not stamp role checks into HO-CAT-001 code.

**20.6 Two-path messages must not drift.** If the handler's duplicate message and the filter's
differ by one character, a caller can tell which path they hit. Use the `AccountMessages` constants.

**20.7 Do not link `CatAggregate.OwnerId` to the account table** — no foreign key, no existence check.

**20.8 A requirement conflict is a stop, not an edit.** SRS and backlog are forbidden scope.

**20.9 No secret enters source control.** The committed `appsettings.json` carries parameters only.

**20.10 No auth framework** — no ASP.NET Core Identity, no `AddAuthentication`, no JWT, no `[Authorize]`.

**20.11 Do not downgrade `Platform.Infrastructure.Core` below 1.0.34.**

**20.12 Do not trust forwarded headers.** `ForwardedHeaders.All` or a trust-all proxy list lets any
caller choose their IP, their audit record and their rate-limit bucket. Trusted proxies come only from
`RequestOrigin`, empty by default.

**20.13 SkiaSharp does not restore, or renders blank glyphs on the target host.** Do **not**
substitute a hosted CAPTCHA, a text or arithmetic question, or SVG text — all leave the endpoint
effectively open while tests pass. `SixLabors.ImageSharp.Drawing` needs owner approval of its licence.

**20.14 Fail closed.** An exception in the CAPTCHA filter, the session middleware or
`RequireActionFilter` must never let the action run. No `catch { /* allow */ }`, no "skip when the
cache is full", no production flag that disables a control.

**20.15 An unanswered open question that blocks a behaviour is a stop.** Build to the stated defaults
in §9.1; if one proves wrong, an Amended row changes it — never a silent code change.
