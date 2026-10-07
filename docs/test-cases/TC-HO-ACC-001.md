# TC-HO-ACC-001 — Accounts and Identity (backend)

Handover: docs/handover/HO-ACC-001.md
Commit(s) under test: **first pass** `df6739f`, plus the test-only additions listed below (uncommitted
at the time, since CODE_REVIEW was writing in parallel on this same worktree). **Second pass**
`913d204` (branch head, `ho/ACC-001`) — covers the ACCEPTANCE-driven fixes `5be2458`, `1975810`,
`fdec8b0`, `ed92088` and the doc-only `913d204`; see the "Second pass note" below the summary.

First-pass test-only additions (all committed by the integrator since):
- `tests/CatEcommerce.IntegrationTests/Endpoints/LegacyCsrfTokenHashTests.cs` (new file, 3 cases)
- `tests/CatEcommerce.IntegrationTests/Endpoints/HostCompositionTests.cs` (2 new cases)
- `tests/CatEcommerce.IntegrationTests/Persistence/MigrationsOnAnEmptyDatabaseTests.cs` (new file, 1 case)
- `tests/CatEcommerce.IntegrationTests/Endpoints/StaffSessionEndpointTests.cs` (1 new case)
- `tests/CatEcommerce.IntegrationTests/Endpoints/DuplicateCustomerGroupsEndpointTests.cs` (4 new cases)
- `tests/CatEcommerce.IntegrationTests/Endpoints/ProfileEndpointTests.cs` (4 new cases + 1 existing
  case's assertion strengthened — see FLAKY TEST DIAGNOSIS in the first-pass gate file)

Second-pass test additions (by the developer, fixing ACCEPTANCE's 1st-pass FAIL):
- `tests/CatEcommerce.IntegrationTests/Endpoints/AuditImmutabilityTests.cs` grew from 3 to 13 cases
  (`5be2458`, `913d204`) — see U-A8, I-A9–I-A16.
- `tests/CatEcommerce.IntegrationTests/AccountsApiFactory.cs` gained `WithFixedRemoteIpAddress`.
- `tests/CatEcommerce.Domain.Tests/Aggregates/CustomerAccountAggregateTests.cs` and
  `tests/CatEcommerce.Application.Tests/CommandHandlers/Account/MergeCustomerCommandHandlerTests.cs`
  each gained one `MergedOn`/`TimeProvider` test (`1975810`) — see U-A10, U-A11.
No test was added for `ed92088` (AuthController's `TimeProvider` fix) or `fdec8b0` (the 16-handler
`OccurredOn` switch) — see R15, R16.

Written: 2026-09-25 · Executed: 2026-09-25 (first pass); second pass executed 2026-09-25 at `913d204`

> **Second pass note.** ACCEPTANCE (`279a825`) FAILED A28 against this sheet's own U-A8, which this
> gate's first pass had already flagged weak but not fixed. The developer's fix landed as `5be2458`
> (interceptor), `1975810`/`fdec8b0`/`ed92088` (TimeProvider on `MergedOn` and on every audit writer),
> and `5be2458`/`ed92088`/`913d204` grew `AuditImmutabilityTests` from 3 to 13 cases and added
> `AccountsApiFactory.WithFixedRemoteIpAddress`. This pass: ran the full suite once (388/388);
> independently restored the pre-fix interceptor (`e07a2d8`) in this worktree, watched 6 of the 13
> `AuditImmutabilityTests` cases fail on the exact content assertion each one names, then restored the
> fix and watched all 13 pass again; read the real audit rows this run wrote back out of Postgres with
> `default_transaction_read_only = on`; and added rows below for every test and TimeProvider change
> named in this round's brief. See U-A8, I-A9–I-A16, R15, R16.

> Statuses record what was **observed**, not inferred. The Evidence column names what was actually
> seen. `BLOCKED` marks a case that could not be executed or whose result was reasoned about rather
> than watched — it is not a failure, and it is not hidden.

## Summary

| | Count |
|---|---|
| Total | 186 |
| PASS | 181 |
| FAIL | 0 |
| BLOCKED | 5 |
| NOT RUN | 0 |

Breakdown (second pass, `913d204`): 75 unit cases (75 PASS, +2 from the first pass: U-A10, U-A11) + 81
integration cases (77 PASS — 6 of those carry an explicit caveat, see their own rows: I-A4, I-A15,
I-I1, I-I2, I-J1, I-X1 — and 4 BLOCKED: I-A8, I-F6, I-J5, I-X2; +8 rows from the first pass: I-A9–I-A16)
+ 14 deferred-gate-item rows (14 PASS) + 16 regression rows (15 PASS, 1 BLOCKED; +2 from the first
pass: R15 BLOCKED, R16 PASS). The acceptance-criteria table is a cross-reference over the rows above,
not counted separately here — see its own PASS/caveat/BLOCKED marks, which mirror the rows they cite.

## Environment

- **Platform**: .NET 9.0.300. `dotnet build CatEcommerce.sln` — 0 warnings, 0 errors, confirmed before
  writing this sheet (the "stop on red build" rule) and again after every set of test additions below.
- **Database**: PostgreSQL at `localhost:5432`, database `catecommerce_dev` (port confirmed open, real
  connection used throughout — no mocking of Postgres anywhere in this suite). Shared with development,
  per §5.1 — every test mints its own mobile/email per §16.1. Two tests
  (`AddAccountsAuditBackfillTests`, `MigrationsOnAnEmptyDatabaseTests`) additionally create and drop
  their own disposable scratch databases on the same server, per §16.1's ban on touching
  `catecommerce_dev` itself for migration-state tests.
- **Test projects**: `CatEcommerce.Domain.Tests` (160 tests), `CatEcommerce.Infrastructure.Tests` (28),
  `CatEcommerce.Application.Tests` (64), `CatEcommerce.IntegrationTests` (124, real HTTP via
  `WebApplicationFactory<Program>`, environment `Testing`, against the shared database and, for two
  files, disposable scratch databases).
- **VPN**: up, per task briefing (not otherwise exercised — no external service is called anywhere in
  this slice; CAPTCHA, hashing and rate limiting are all in-process, per §4.4's forbidden-shapes list).
- **Decoration checks** for the four named security regression tests used an isolated scratch `git
  clone` of this worktree at `df6739f` (`C:\tmp_acc001_check`, since deleted) rather than mutating this
  shared worktree while CODE_REVIEW was writing in parallel here. See the gate file's FLAKY TEST
  DIAGNOSIS and DECORATION CHECKS sections for exactly what was reverted, watched fail, and restored.
- Developer's reported figure: 360/360 passing at the start of this gate. This gate found that figure
  accurate for what existed, added 16 new cases of its own (12 for genuine §16 coverage gaps: I-A1,
  I-A2 (×2 cases), I-A3, I-I2–I-I5, I-J1–I-J4; 4 for two deferred items with no committed test: D3,
  D13 (×3 cases)), and fixed one genuinely flaky
  existing case (`TwoConcurrentAddsAtNineAddresses_OneSucceedsOneConflictsNeverEleven` — see the gate
  file). Final count: **376/376**, confirmed with two full, independent `dotnet test CatEcommerce.sln`
  runs after every change in this sheet was in place.
- **Second pass** (`913d204`): `dotnet build CatEcommerce.sln` — 0 warnings, 0 errors. `dotnet test
  CatEcommerce.sln` once, per this round's (deliberately shorter) brief: **388/388**
  (`Domain.Tests` 161, `Infrastructure.Tests` 28, `Application.Tests` 65, `IntegrationTests` 134),
  matching the developer's own reported figure exactly.

## Unit cases

`tests/CatEcommerce.Domain.Tests` and `tests/CatEcommerce.Application.Tests`, per §15's routing rule
(U-A8 is the one row §15 itself routes to `IntegrationTests` instead — see its own row below).
Evidence for every row in this table: `dotnet test tests/CatEcommerce.Domain.Tests` — **160/160
passed**, and `dotnet test tests/CatEcommerce.Application.Tests` — **64/64 passed**, both confirmed
across two independent full-suite runs (2026-09-25), and both included in the two full-solution runs
recorded in the Environment section above.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | `MobileNumber.Create` with `01712345678`, `+880 1712-345678`, `008801712345678` | All `+8801712345678`, equal, same hash code | PASS | `MobileNumberTests.cs` — normalisation/equality cases, part of the 160/160 |
| U-A2 | `MobileNumber.Create` with `""`, `"abc"`, `"0171234"`, 25 digits | `InvalidMobileNumber` | PASS | `MobileNumberTests.cs`, same run |
| U-A3 | `EmailAddress.Create("  Foo@Example.COM ")`; `"a@b"`, `"@x.com"` | `foo@example.com`; `InvalidEmailAddress` | PASS | `EmailAddressTests.cs`, same run |
| U-A4 | `LoginThrottleRules.DelayFor(1…20)` | Monotonic non-decreasing, 0 for 1–2, capped at 900 s | PASS | `LoginThrottleRulesTests.cs`, same run |
| U-A5 | Reflect over `CustomerAccountAggregate` after `Anonymise` | Every public string is null, the placeholder, or on the short allow-list | PASS | `CustomerAccountAggregateTests.cs`, same run |
| U-A6 | `AuditEntryAggregate.Record` twice | `AuditEntryAlreadyRecorded` | PASS | `AuditEntryAggregateTests.cs`, same run |
| U-A7 | Reflect over `AuditEntryAggregate` | `Record` is the only public mutator | PASS | `AuditEntryAggregateTests.cs`, same run |
| U-A8 | Change-capture on a modified `CatAggregate` (§15 routes this to `IntegrationTests`) | One `DataChange` row, correct before/after, `xmin` excluded | PASS | **Second pass, no longer weak.** `AuditImmutabilityTests` now asserts before/after value content directly across every shape ACCEPTANCE's A28 finding named — insert, scalar update, replaced value object (×2: email, login throttle), owned-collection add/remove, and `xmin` exclusion — see I-A9–I-A16. **Independently reproved non-decorative this pass**: restored the pre-fix `AuditChangeCaptureInterceptor` (commit `e07a2d8`) in this worktree, rebuilt (0 warnings/errors), re-ran the 13-test file — **6 of 13 failed**: `AddingASecondAddress_TheNewAddressAppearsOnlyInAfter`, `RemovingAnAddress_TheRemovedAddressAppearsInBeforeAndNotInAfter`, `ChangingEmail_OldToNew_BeforeHasTheOldValueAfterHasTheNew`, `FailedSignIn_RecordsFailedCountGoingFromZeroToOne`, `SettingEmail_FromNull_TheNewAddressAppearsOnlyInAfter`, `Registration_ProducesADataChangeAuditRowInTheSameTransactionWithIp` — each on the exact content assertion its name describes (e.g. `Assert.Null(row.Before)` failed because the pre-fix code built a non-null before-snapshot for an insert). Restored the fix, rebuilt, re-ran — **13/13 passed**. `xmin` exclusion additionally confirmed against real rows: a read-only query (`default_transaction_read_only = on`) found zero rows with `"xmin"`/`"Xmin"` as an actual JSON key across all 25,816 rows in the shared `audit.audit_entry` table |
| U-A9 | Change-capture on a `CustomerAccountAggregate` with a credential | `password_hash`, `security_stamp` written as `"[redacted]"` | PASS | `EventPersonalDataReflectionTests.cs` / audit redaction cases, part of the 160/160. Also independently re-confirmed at integration level this pass — see I-A16 |
| U-A10 | `MergeInto` sets `MergedOn` to the exact caller-supplied timestamp, not a wall-clock read | Aggregate's `MergedOn` equals the DTO's `MergedOn` exactly | PASS | **New test, second pass.** `CustomerAccountAggregateTests.MergeInto_SetsMergedOnToTheSuppliedTimestamp` (added at `1975810`, a CODE_REVIEW MEDIUM fix) — watched pass individually and as part of the 161/161 |
| U-A11 | `MergeCustomerCommandHandler` threads `TimeProvider.GetUtcNow()` into `CustomerMergeDto.MergedOn`, not `DateTimeOffset.UtcNow` read inside the aggregate | Handler-level: survivor's `MergedOn` equals the injected `FakeTimeProvider`'s time | PASS | **New test, second pass.** `MergeCustomerCommandHandlerTests.HandleAsync_Success_SetsMergedOnToTheTimeProvidersTime` (added at `1975810`) — watched pass individually and as part of the 65/65 |
| U-X1 | Reflect over every `Event` type in `CatEcommerce.Domain` | No property named like `/name\|mobile\|phone\|email\|address\|line\|password\|hash\|token\|stamp/i` except the allow-listed booleans and ids | PASS | `EventPersonalDataReflectionTests.cs`, same run |
| U-B1 | `Register` valid | Properties set; `SelfService`, `Active`, stamp non-empty; one event; version bumped | PASS | `CustomerAccountAggregateTests.cs`, same run |
| U-B2 | `Register` with neither identifier | `IdentifierRequired` | PASS | `CustomerAccountAggregateTests.cs`, same run |
| U-B3 | Reflect: `CustomerAccountAggregate`, its DTOs and ViewModels | No `Verif*`, `Otp*`, `TwoFactor*`, `Captcha*` member | PASS | `CustomerAccountAggregateTests.cs`, same run |
| U-B4 | Validator: 9 vs 10-character password | `PasswordTooShort` on `Password` / passes | PASS | `AccountCommandValidatorsTests.cs`, part of the 64/64 |
| U-B5 | Validator: `"hunter2-hunter2"` short-circuit messages | Submitted value appears in no message | PASS | `AccountCommandValidatorsTests.cs`, same run |
| U-B6 | Handler: mobile exists | `MobileNumberAlreadyRegistered`, message mentions log in and reset, contains no "email"; hasher never called | PASS | `RegisterCustomerCommandHandlerTests.cs`, same run |
| U-B7 | Handler: email exists, mobile not supplied | `EmailAddressAlreadyRegistered`, message contains no "mobile"/"phone" | PASS | `RegisterCustomerCommandHandlerTests.cs`, same run |
| U-B8 | Handler: breached | `BreachedPassword`; no hash, no save | PASS | `RegisterCustomerCommandHandlerTests.cs`, same run |
| U-B9 | Captcha service: 1 000 issues | Unique ids; answers from the alphabet, correct length | PASS | `MemoryCacheCaptchaServiceTests.cs` (`CatEcommerce.IntegrationTests/Unit/`), part of the 124 — routed there per §15 ("exercises Infrastructure") |
| U-B10 | Captcha verify: right answer different case/spaces; replay after right; replay after wrong; after TTL; null/blank | true; false; false; false; false — no exception | PASS | `MemoryCacheCaptchaServiceTests.cs`, same |
| U-B11 | Captcha overflow beyond `MaxOutstandingChallenges` | No exception, bounded; overflow fails verify | PASS | `MemoryCacheCaptchaServiceTests.cs`, same |
| U-B12 | Renderer | PNG signature, 200×70, answer not in the bytes | PASS | `SkiaCaptchaImageRendererTests.cs`, same |
| U-B13 | `CaptchaValidationFilter` on an action without `ICaptchaProtectedRequest` | `InvalidOperationException` | PASS | part of the 64/64 (`Application.Tests`) |
| U-B14 | `Argon2idPasswordHasher.Hash` twice | Different encodings; neither contains the plaintext; `Verify` true for each | PASS | `Argon2idPasswordHasherTests.cs`, part of the 124 |
| U-B15 | `LocalListBreachedPasswordChecker` | Known common password → true; random 20 chars → false; case-insensitive | PASS | `LocalListBreachedPasswordCheckerTests.cs`, part of the 124 |
| U-C1 | `RecordFailedLogin` ×3 at t | `LockedUntil = t + 5 s`; event outcome and count | PASS | `CustomerAccountAggregateTests.cs`, part of the 160/160 |
| U-C2 | `RecordSuccessfulLogin` while locked | `AccountLocked` | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-C3 | `RecordSuccessfulLogin` after lock expiry | Throttle reset | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-C4 | `EnsureCanSignIn` on merged / anonymised / no credential | `AccountNotActive` / `AccountNotActive` / `InvalidCredentials` | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-C5 | Handler: unknown identifier | `VerifyAgainstDummy` called once; masked audit entry written; `InvalidCredentials` | PASS | `SignInCustomerCommandHandlerTests.cs`, part of the 64/64 |
| U-C6 | Handler: wrong password | `UpdateAsync` on the account called; no session saved | PASS | `SignInCustomerCommandHandlerTests.cs`, same |
| U-C7 | Handler: success | Throttle reset + session saved with account's stamp; result token hash equals saved hash | PASS | `SignInCustomerCommandHandlerTests.cs`, same |
| U-C8 | `SessionStateRules.Classify` truth table (ended, expired, revoked, idle, active) | Each state; boundary `asOf == ExpiresOn` → `Expired` | PASS | `SessionStateRulesTests.cs`, part of the 160/160 |
| U-G1 | Staff `RecordActivity` 30 s after last | No change; 61 s → advanced | PASS | `StaffSessionAggregateTests.cs`, same |
| U-G2 | `EndForIdleTimeout` | `EndedOn = LastActivityOn + idle`; second call throws | PASS | `StaffSessionAggregateTests.cs`, same |
| U-G3 | Idle-expired session after the configured period is lengthened | Still `Ended` — terminal | PASS | `StaffSessionAggregateTests.cs`, same |
| U-G4 | Reflect over both session aggregates | No `Revive`/`Extend`/`Reactivate` | PASS | `CustomerSessionAggregateTests.cs` / `StaffSessionAggregateTests.cs`, same |
| U-G5 | Staff sign-in handler with customer credentials | `InvalidCredentials` — customer repository never queried | PASS | `SignInCustomerCommandHandlerTests.cs` (staff-credential case), part of the 64/64 |
| U-D1 | `EnsureUsable` expired / superseded / fresh | `…Expired` / `…Superseded` / ok | PASS | `PasswordResetTokenAggregateTests.cs`, part of the 160/160 |
| U-D2 | `SetPassword` | Stamp rotated; throttle reset; `WasFirstPassword` correct | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-D3 | Request handler: unknown, merged, anonymised, found | All `IsSuccess=true`, null `ValidationResult`; only "found" saves and dispatches; generator called on every path | PASS | `RequestPasswordResetCommandHandlerTests.cs`, part of the 64/64 |
| U-D4 | Confirm handler: each token failure | Single `InvalidPasswordResetToken` | PASS | `ResetPasswordCommandHandlerTests.cs`, same |
| U-D5 | Confirm with a breached new password | `BreachedPassword`; account unchanged; token still usable | PASS | `ResetPasswordCommandHandlerTests.cs`, same |
| U-D6 | `DevelopmentPasswordResetTokenDispatcher` constructed in `Production` | Throws | PASS | part of the 64/64 |
| U-E1 | `UpdateContactDetails` on an email-only account changing the email | `PrimaryIdentifierReadOnly` | PASS | `CustomerAccountAggregateTests.cs`, part of the 160/160 |
| U-E2 | 4 additional numbers; one equal to the primary | `AdditionalPhoneNumberLimitExceeded`; `DuplicatePhoneNumber` | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-E3 | No change | No event, no version bump | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-E4 | First address becomes default; remove default with others present; remove last | default set; `DefaultAddressCannotBeRemoved`; default cleared | PASS | `CustomerAddressBookAggregateTests.cs`, same |
| U-E5 | 200 random add/update/remove/set-default operations | `EnsureDefaultIsConsistent` never throws | PASS | `CustomerAddressBookAggregateTests.cs`, same |
| U-E6 | 11th address | `DeliveryAddressLimitExceeded` | PASS | `CustomerAddressBookAggregateTests.cs`, same |
| U-E7 | `ChangePreferences` with a transactional cell | `NotificationPreferenceNotEditable`, nothing applied | PASS | `CustomerNotificationPreferencesAggregateTests.cs`, same |
| U-E8 | `NotificationPreferenceRules` full truth table over `GetAll()` | Transactional always permitted; marketing only when granted; default not granted | PASS | `NotificationPreferenceRulesTests.cs`, same |
| U-F1 | `Create` with `[" Vet", "Vet", "Nurse"]` | `["Nurse", "Vet"]` in catalogue order | PASS | `StaffAccountAggregateTests.cs`, same |
| U-F2 | Empty roles; `"vet"` lower-case; `"Groomer"` | `StaffRolesRequired`; `UnknownStaffRole`; `UnknownStaffRole` | PASS | `StaffAccountAggregateTests.cs`, same |
| U-F3 | `ChangeRoles` to the same set | No event, no version bump, stamp unchanged | PASS | `StaffAccountAggregateTests.cs`, same |
| U-F4 | `ChangeRoles` to a different set | Stamp rotated; event with both sets | PASS | `StaffAccountAggregateTests.cs`, same |
| U-F5 | `StaffRoleCatalogue` | 8 roles × 20 capabilities; no `Customer` role | PASS | `StaffRoleCatalogueTests.cs`, same |
| U-F6 | `RequireActionFilter`: unmapped action | Denied | PASS | `ActionRolesAggregationTests.cs`, same |
| U-F7 | `RequireActionFilter`: audit write throws | Still `403`, no exception escapes | PASS | `ActionRolesAggregationTests.cs`, same |
| U-F8 | C10 removing `Administrator` from the last holder | `LastAdministratorCannotBeRemoved` | PASS | `StaffAccountAggregateTests.cs`, same |
| U-F9 | C9 handler guard with a `UserContext` lacking `Administrator` | `ActionNotPermitted` | PASS | `ChangeStaffRolesCommandHandlerTests.cs`, part of the 64/64 |
| U-H1 | `RegisterWalkIn` | `Reception`, no credential, no email, `RecordStatus == Incomplete` | PASS | `CustomerAccountAggregateTests.cs`, part of the 160/160 |
| U-H2 | Then `SetPassword` | `RecordStatus == Complete`, channel still `Reception` | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-H3 | Reflect over `WalkInCustomerRegistrationDto` | No credential, email, channel or status field | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-I1 | `MergeInto` self; blank reason; anonymised | Each code | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-I2 | Match: shared additional number; shared email; both null and no shared number | ok; ok; `CustomersNotDuplicates` | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-I3 | Merge then re-send same survivor; then a different survivor | Re-application event, state unchanged; `CustomerAlreadyMerged` | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-I4 | `ReassignOwnerOnCustomerMerge` on a deceased cat | Moves; `EnsureAlive` not called | PASS | `CatAggregateMergeTests.cs`, same |
| U-I5 | Wrong owner | `CatOwnerMismatch` | PASS | `CatAggregateMergeTests.cs`, same |
| U-J1 | `Anonymise` | Fields per §8.10a; stamp rotated; status; event carries ids only | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-J2 | `Anonymise` twice | `AccountAlreadyAnonymised` | PASS | `CustomerAccountAggregateTests.cs`, same |
| U-J3 | Delete handler while locked | `AccountLocked`; hasher not called | PASS | `DeleteCustomerAccountCommandHandlerTests.cs`, part of the 64/64 |
| U-J4 | Delete handler wrong password | `RecordFailedLogin` persisted; `ReauthenticationFailed` | PASS | `DeleteCustomerAccountCommandHandlerTests.cs`, same |

## Integration cases

`tests/CatEcommerce.IntegrationTests` — real HTTP through `WebApplicationFactory<Program>`, environment
`Testing`, against the shared `catecommerce_dev` (two files use disposable scratch databases instead —
noted per row). Evidence for every PASS row below: two independent full runs of
`dotnet test tests/CatEcommerce.IntegrationTests` — **124/124 passed** both times (2026-09-25),
individually re-confirmed with a `--filter` run against each named test class while writing this sheet.

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I-A1 | Host starts with `ValidateScopes`+`ValidateOnBuild` | No exception — every registration resolves | PASS | **New test, added at this gate.** `HostCompositionTests.Host_BuildsAndStartsWithNoException_EveryRegistrationResolves` — watched pass; also implicit in every other test in this suite (all depend on the same host building) |
| I-A2 | Host in `Production` with `Captcha:TestAnswer` set; with `DevUser:Enabled=true` | Startup fails, each with a clear message | PASS | **New test, added at this gate.** `HostCompositionTests.HostInProduction_WithADevelopmentOnlySeamConfigured_FailsToStart` (`[Theory]`, both cases) — watched fail-to-start against `AccountStartupGuardHostedService`, each with its own named key in the exception |
| I-A3 | Migrations on an empty database | Schema `account` complete; `audit.audit_entry` has `ip_address`, `category`, `correlation_id`, both triggers | PASS | **New test, added at this gate.** `MigrationsOnAnEmptyDatabaseTests.EveryMigration_AppliedInOrderFromEmpty_ProducesTheFullAccountSchemaAndAuditAdditions`, against a fresh disposable scratch database — watched pass, 3 runs (stability check) |
| I-A4 | `UPDATE`, `DELETE`, `TRUNCATE` on `audit.audit_entry` through Npgsql | Each raises | PASS, caveat | `AuditImmutabilityTests.UpdateOnAuditEntry_ThroughRawSql_Raises` / `DeleteOnAuditEntry_ThroughRawSql_Raises` — both watched pass. TRUNCATE's own sub-case was never attempted against any database in this run: §16.1 rule 4 reads "Never `TRUNCATE`, `DROP` or clean" as an absolute ban on issuing that statement against the shared `catecommerce_dev`, not only a ban on using it to reset test state — deliberately not risked, even though the trigger is expected to refuse it. See "Cases I could not run" |
| I-A5 | `UpdateAsync` on an `AuditEntryAggregate` | `AuditEntryImmutableException`, nothing sent | PASS | `AuditImmutabilityInterceptorRegistrationTests.DbContextResolvedFromDi_HasTheImmutabilityInterceptorActive` |
| I-A6 | Register a cat through the shipped endpoint | A `DataChange` row in the same transaction; IP recorded | PASS | `AuditImmutabilityTests.Registration_ProducesADataChangeAuditRowInTheSameTransactionWithIp`. **Second pass**: this case is now one of the 6 that fail against the pre-fix interceptor (`Assert.Null(row.Before)` fails — see U-A8) — no longer a case that would pass regardless of the bug |
| I-A7 | A request's audit IP reflects a forwarded address only when the immediate peer is a trusted proxy, and never otherwise | Trusted proxy: audit IP = forwarded address. Untrusted: audit IP = raw peer, header ignored | PASS | **Second pass — direct tests now exist**, replacing the first pass's indirect citation of a rate-limiting test. `AuditImmutabilityTests.AuditIp_WithATrustedProxy_ReflectsTheForwardedAddressNotTheProxys` and `AuditIp_WithoutATrustedProxy_IgnoresTheForwardedHeader` (both added at `913d204`, using the new `AccountsApiFactory.WithFixedRemoteIpAddress(ip, trustAsProxy:)`) — both watched pass this pass; both also passed against the pre-fix interceptor (expected — `IClientIpAccessor` is a separate mechanism from the owned-entry capture bug, not itself part of A28) |
| I-A8 | A handler throws after the change-capture row was added | Neither the change nor its audit row is committed | BLOCKED | No test exists. Forcing a mid-`SaveChangesAsync` failure specifically after the change-capture interceptor has queued its audit entry but before commit needs a seam (a throwing interceptor, or a second, colliding write inside the same transaction) this gate did not have time to build safely; ADR-040's guarantee here rests on EF's own single-`SaveChangesAsync`-is-one-transaction behaviour, which is a platform property rather than something this slice's own code could break independently. Not added at either pass — see "Cases I could not run" |
| I-A9 | A modified root's scalar field only (cat profile rename) | Before/after pair with the old and new name | PASS | **New, second pass.** `AuditImmutabilityTests.CatProfileUpdate_ProducesABeforeAfterPairWithTheOldAndNewName` — watched pass. Also passed against the pre-fix interceptor (root scalar changes never took the buggy Added-owned path — included for completeness of the file's coverage, not itself an A28 regression case) |
| I-A10 | Setting an email from null (a brand-new owned value object, tracked as `Added`) | The new address appears only in "after", never in "before" | PASS | **New, second pass — regression for ACCEPTANCE A28.** `AuditImmutabilityTests.SettingEmail_FromNull_TheNewAddressAppearsOnlyInAfter` — failed pre-fix (`Assert.DoesNotContain` found the new email already in `Before`, because `CollectProperties` read `CurrentValue` for an `Added` entry regardless of which snapshot); watched pass with the fix restored |
| I-A11 | Changing an email (old value `Deleted`, new value `Added`, same property) | `Before` has the old email only, `After` has the new email only | PASS | **New, second pass — regression for ACCEPTANCE A28, the account-takeover-relevant case.** `AuditImmutabilityTests.ChangingEmail_OldToNew_BeforeHasTheOldValueAfterHasTheNew` — failed pre-fix (`Assert.Contains` — the old email was absent from `Before`; `Deleted` owned entries were dropped entirely, so the row could not say what the email used to be); watched pass with the fix restored. **Independently confirmed against the real row this run wrote**: read-only query (`default_transaction_read_only = on`) of the latest `CustomerContactDetailsUpdatedEvent` row shows `EmailAddress.Value` = the first minted email in `BeforeValue` and the second in `AfterValue`, neither string appearing in the other's column |
| I-A12 | Adding a second address to an existing (already-`Modified`) address book | The new address appears only in "after" | PASS | **New, second pass — regression for ACCEPTANCE A28 (owned-collection add).** `AuditImmutabilityTests.AddingASecondAddress_TheNewAddressAppearsOnlyInAfter` — failed pre-fix; watched pass with the fix restored |
| I-A13 | Removing an address from the book | The removed address appears in "before", not in "after" | PASS | **New, second pass — regression for ACCEPTANCE A28 (owned-collection remove).** `AuditImmutabilityTests.RemovingAnAddress_TheRemovedAddressAppearsInBeforeAndNotInAfter` — failed pre-fix (a `Deleted` owned collection member was dropped from both snapshots — the audit trail lost the removed address entirely); watched pass with the fix restored |
| I-A14 | A failed sign-in's `LoginThrottle` (an owned value object wholesale-replaced, `Deleted`-old + `Added`-new) | `LoginThrottle.FailedCount` is `0` in "before", `1` in "after" | PASS | **New, second pass — regression for ACCEPTANCE A28, the live example the gate first observed.** `AuditImmutabilityTests.FailedSignIn_RecordsFailedCountGoingFromZeroToOne` — failed pre-fix (`Assert.Contains` — no `"LoginThrottle.FailedCount":0` in `Before`; every failed-sign-in row showed "1 → 1"); watched pass with the fix restored. **Independently confirmed against the real row this run wrote**: read-only query of the latest `CustomerSignInFailedEvent` row shows `LoginThrottle.FailedCount` = `0` in `BeforeValue`, `1` in `AfterValue` |
| I-A15 | No captured snapshot ever mentions the EF/Postgres `xmin` concurrency token | Neither `BeforeValue` nor `AfterValue` contains `xmin` | PASS, caveat | `AuditImmutabilityTests.NoAuditRow_EverMentionsXmin` — watched pass both before and after the interceptor revert (not itself an A28 regression; `xmin` was already excluded pre-fix). **Caveat found by this pass's own read-only DB scan**: a stricter regex check for `xmin`/`Xmin` as an actual JSON key (`"[Xx]min"\s*:`) returns **zero** rows across all 25,816 rows in the shared table, but a plain `ILIKE '%xmin%'` — the same shape as this test's own `Assert.DoesNotContain("xmin", ..., OrdinalIgnoreCase)` — hits exactly one row: a `FullName` of `"No Xmin Please"` from an earlier, uncommitted probe, not the concurrency token. The test would currently pass either way here since it only checks its own run's rows, but its substring approach has the same latent blind spot; not a production bug, flagged for awareness only |
| I-A16 | A snapshot built by the fixed interceptor still redacts secrets | `PasswordCredential.Hash` and `SecurityStamp` are `"[redacted]"`, never the real value | PASS | **New, second pass — confirms the A28 fix did not regress U-A9's redaction guarantee.** `AuditImmutabilityTests.Registration_RedactsThePasswordCredentialAndSecurityStamp` — watched pass both before and after the interceptor revert (redaction path untouched by A28) |
| I-B1 | Register with name, mobile, 12-char password, CAPTCHA | `201`; row exists; no verification column anywhere | PASS | `RegistrationEndpointTests.cs` |
| I-B2 | Register with email only | `201`; login by email works | PASS | `RegistrationEndpointTests.cs` |
| I-B3 | Neither identifier | `400 IdentifierRequired` | PASS | `RegistrationEndpointTests.cs` |
| I-B4 | Same mobile, different format | `400 MobileNumberAlreadyRegistered`; body contains no "email"; one row | PASS | `RegistrationEndpointTests.cs` |
| I-B5 | Same email, different case | `400 EmailAddressAlreadyRegistered`; body contains no "mobile"/"phone" | PASS | `RegistrationEndpointTests.cs` |
| I-B6 | 9-char password → then breached ≥10-char | `PasswordTooShort` → `BreachedPassword`, distinct messages | PASS | `RegistrationEndpointTests.cs` |
| I-B7 | No CAPTCHA fields; wrong answer | `400 CaptchaRequired`; `400 CaptchaInvalid`; no row, no audit, hasher spy never called | PASS | `RegistrationEndpointTests.cs` |
| I-B8 | Replay a solved challenge | `400 CaptchaInvalid` | PASS | `RegistrationEndpointTests.cs` |
| I-B9 | `registration` limit 2: three posts | Third `429`+`Retry-After`; no IP in body | PASS | `RateLimitingTests.Registration_OverLimit_ThirdReturns429WithRetryAfterAndNoIpInBody` |
| I-B10 | Two concurrent registrations, same mobile | One `201`, one `400` — never `500`/`409`, never two rows | PASS | `RegistrationEndpointTests.cs` |
| I-B11 | Persisted row | `password_hash` is argon2id PHC, not containing the plaintext | PASS | `RegistrationEndpointTests.cs` |
| I-B12 | `POST /api/captcha` | `200`, PNG data URL, `no-store`, no test answer in body | PASS | `RegistrationEndpointTests.cs` |
| I-C1 | Sign in by mobile | `200`; `pc_session` HttpOnly+Secure+SameSite=Lax; `pc_csrf` present; `GET /api/auth/session` → `200` | PASS | `CustomerAuthEndpointTests.SignIn_ByMobile_SetsSessionCookieAndSessionEndpointWorks` |
| I-C2 | Sign in by email | Same | PASS | `CustomerAuthEndpointTests.SignIn_ByEmail_Returns200` |
| I-C3 | Unknown identifier vs wrong password | Byte-identical `401` bodies; no `Retry-After` | PASS | `CustomerAuthEndpointTests.SignIn_UnknownIdentifierVsWrongPassword_ReturnByteIdenticalBodies` |
| I-C4 | Three wrong then the right password within 5 s | `401` on the fourth (locked), audited server-side, same public body | PASS | `CustomerAuthEndpointTests.SignIn_ThreeWrongThenRightWithin5s_FourthIsLockedWithSameBodyAndAudited` |
| I-C5 | Every attempt in I-C3/I-C4 | One `AuthenticationEvent` audit row each; unknown identifier masked | PASS | `CustomerAuthEndpointTests.SignIn_UnknownIdentifier_AuditedWithMaskedIdentifierNeverRaw` (+ the same-file C4 case) |
| I-C6 | Sign out, then `GET` session | `204`; `401 session_invalid`; cookie expired | PASS | `CustomerAuthEndpointTests.SignOut_ThenGetSession_Returns401AndCookieExpired` |
| I-C7 | `PUT /api/customers/me` without `X-CSRF-Token` | `403 csrf_token_invalid`; nothing changed | PASS | `CustomerAuthEndpointTests.PutProfile_WithoutCsrfHeader_Returns403AndChangesNothing` |
| I-C8 | A tampered/random `pc_session` on a guest endpoint | Treated as anonymous; endpoint still answers | PASS | `CustomerAuthEndpointTests.GuestEndpoint_WithTamperedSessionCookie_TreatedAsAnonymous` |
| I-C9 | `sign-in` limit 2: three attempts | Third `429` | PASS | `RateLimitingTests.SignIn_OverLimit_ThirdReturns429` |
| I-D1 | Request for unknown/known customer | Identical `200` bodies | PASS | `PasswordResetEndpointTests.RequestReset_UnknownVsKnown_ReturnsIdenticalBodies` |
| I-D2 | Known: dispatcher spy | Received one token; hash in `password_reset_token` | PASS | `PasswordResetEndpointTests.RequestThenConfirm_Works_AndTokenIsSingleUse` |
| I-D3 | Confirm with it, 12-char new password | `204`; old password fails; new succeeds | PASS | `PasswordResetEndpointTests.RequestThenConfirm_Works_AndTokenIsSingleUse` |
| I-D4 | The same token again; an older second token | Both `400 InvalidPasswordResetToken` | PASS | `PasswordResetEndpointTests.RequestThenConfirm_Works_AndTokenIsSingleUse` (inline `// I-D4` case) |
| I-D5 | A session created before the reset | `401 session_invalid` after — US-ACC-05 AC3 | PASS | `PasswordResetEndpointTests.SuccessfulReset_InvalidatesSessionsCreatedBeforeIt` |
| I-D6 | Expired token | `400 InvalidPasswordResetToken`, same message as I-D4 | PASS | `PasswordResetEndpointTests.ExpiredToken_Returns400WithSameMessageAsReplay` |
| I-D7 | Staff reset request via staff route; walk-in reset | Tokens carry `AccountKind`; confirm sets the right account's password | PASS | `PasswordResetEndpointTests.StaffPasswordReset_ConfirmSetsTheStaffAccountsPassword` (+ `WalkInEndpointTests.WalkInCompletesViaReset_ThenSignsIn_RecordStatusComplete`) |
| I-E1 | `GET /api/customers/me` | Profile of signed-in customer; no credential fields | PASS | `ProfileEndpointTests.GetProfile_ReturnsSignedInCustomersProfileWithNoCredentialFields` |
| I-E2 | Add three addresses, set second default, remove first | Exactly one `isDefault` at every step | PASS | `ProfileEndpointTests.AddThreeAddresses_SetSecondDefault_RemoveFirst_ExactlyOneDefaultAtEveryStep` |
| I-E3 | Remove the default while others exist | `400 DefaultAddressCannotBeRemoved` | PASS | `ProfileEndpointTests.RemoveDefaultAddress_WhileOthersExist_Returns400` |
| I-E4 | Two concurrent adds at nine addresses | One succeeds, one `409` — never eleven | PASS | `ProfileEndpointTests.TwoConcurrentAddsAtNineAddresses_OneSucceedsOneConflictsNeverEleven` — **assertion strengthened at this gate**, see the gate file's FLAKY TEST DIAGNOSIS; watched pass 20/20 in isolation after the fix, plus both full-suite runs |
| I-E5 | Two concurrent first writes (fresh customer) | One `2xx`, one `409` | PASS | `ProfileEndpointTests.TwoConcurrentFirstWrites_NeverBothLostNeverAServerError` |
| I-E6 | `GET` preferences before any write | Every cell; transactional `isAlwaysOn`, marketing not granted | PASS | `ProfileEndpointTests.GetPreferences_BeforeAnyWrite_EveryCellTransactionalAlwaysOnMarketingNotGranted` |
| I-E7 | `PUT` a transactional cell off | `400`, nothing applied | PASS | `ProfileEndpointTests.PutTransactionalCellOff_Returns400AndAppliesNothing` |
| I-E8 | Any `/me` endpoint as guest or staff | `401` | PASS | `ProfileEndpointTests.MeEndpoint_AsGuestOrStaff_Returns401` |
| I-F1 | Administrator creates a staff account with roles | `201`; roles stored in catalogue order; `hasPassword: false` | PASS | `StaffEndpointTests.Administrator_CreatesStaffAccount_RolesStoredInCatalogueOrderAndHasPasswordFalse` |
| I-F2 | A `Nurse` calls `POST /api/staff-accounts` directly | `403 ActionNotPermitted`; one `AuthorizationDenied` audit row — US-ACC-07 AC2 | PASS | `StaffEndpointTests.NonAdministrator_CallingCreateStaffAccount_Returns403AndAudits` |
| I-F3 | A customer session calls it | `401` | PASS | `StaffEndpointTests.CustomerSession_CallingStaffEndpoint_Returns401` |
| I-F4 | Replace roles | `204`; member's existing staff session → `401` on next request | PASS | `StaffEndpointTests.ChangeRoles_RotatesStamp_AffectedMembersExistingSessionIs401OnNextRequest` |
| I-F5 | `GET /api/staff-roles` | 8 roles, 20 capabilities | PASS | `StaffEndpointTests.StaffRoles_ReturnsEightRolesAndTwentyCapabilities` |
| I-F6 | Remove `Administrator` from the last administrator | `400 LastAdministratorCannotBeRemoved` | BLOCKED | The developer's own documented reasoning in `StaffEndpointTests.cs` (a code comment, not a skipped test): the guard checks for the existence of ANY OTHER active Administrator system-wide, so reproducing "this is the last one" in a shared, non-empty, never-truncated database (§16.1 rules 2 and 4) is not constructible from this test alone. Confirmed at unit level instead (U-F8, PASS) |
| I-G1 | New staff member: set password via reset, sign in | `200`+`pc_staff_session`; `GET /api/staff/session` returns roles — US-ACC-14 | PASS | `StaffSessionEndpointTests.NewStaffMember_SetsPasswordViaReset_ThenSignsIn` |
| I-G2 | Customer credentials on staff sign-in | Same `401` as I-C3 | PASS | `StaffEndpointTests.CustomerCredentials_OnStaffSignIn_ReturnsSameGenericFailure` |
| I-G3 | Idle beyond `StaffIdleTimeoutMinutes` | `401 session_idle_timeout` forever after | PASS | `StaffSessionEndpointTests.IdleBeyondTimeout_RefusesForeverAfter_NoSideEffectAndAuditedExactlyOnce` |
| I-G4 | The refused request was a `POST /api/staff/customers` | No customer row created — no side effect | PASS | `StaffSessionEndpointTests.IdleBeyondTimeout_RefusesForeverAfter_NoSideEffectAndAuditedExactlyOnce` (same test) |
| I-G5 | Idle expiry audited | Exactly one `StaffSessionIdleExpired` row even with three concurrent refused requests | PASS | `StaffSessionEndpointTests.IdleBeyondTimeout_RefusesForeverAfter_NoSideEffectAndAuditedExactlyOnce` (same test) |
| I-G6 | `GET /api/staff/session` repeatedly | `lastActivityOn` unchanged | PASS | `StaffSessionEndpointTests.GetSession_Repeatedly_DoesNotAdvanceLastActivityOn` |
| I-G7 | Sign in again after idle | New session works; `staffAccountId` equal to before | PASS | `StaffSessionEndpointTests.SignInAgainAfterIdle_NewSessionWorks_SameStaffAccountId` |
| I-H1 | Reception creates a walk-in | `201`; `GET` shows `recordStatus: "Incomplete"`, `registrationChannel: "Reception"` | PASS | `WalkInEndpointTests.Reception_CreatesWalkIn_201AndIncompleteReceptionChannel` |
| I-H2 | Duplicate mobile | `400 MobileNumberAlreadyRegistered`; `GET /api/staff/customers?mobile=` finds the existing record | PASS | `WalkInEndpointTests.DuplicateMobile_Returns400AndFindEndpointLocatesExistingRecord` |
| I-H3 | Walk-in completes via reset, then signs in | `recordStatus: "Complete"`; sign-in `200` | PASS | `WalkInEndpointTests.WalkInCompletesViaReset_ThenSignsIn_RecordStatusComplete` |
| I-H4 | The walk-in tries self-registration with the same mobile | `400 MobileNumberAlreadyRegistered` with the reset offer | PASS | `WalkInEndpointTests.WalkInTriesSelfRegistrationWithSameMobile_Returns400MobileAlreadyRegistered` |
| I-I1 | Two customers sharing an additional number, each with a cat (one deceased) | Listed together in duplicates | PASS, caveat | `DuplicateCustomerGroupsEndpointTests.TwoCustomers_SharingAnAdditionalNumber_AppearInTheSameDuplicateGroup` — proves the grouping; neither this nor any other committed test attaches cats to the two members (the "one deceased" detail is not separately exercised at integration level, though U-I4 covers the deceased-cat reassignment rule at domain level) |
| I-I2 | Merge | `200`; duplicate `Merged`; both cats on survivor; merge+two reassignment `BusinessEvent` rows; duplicate's session `401` | PASS | **New test, added at this gate.** `DuplicateCustomerGroupsEndpointTests.Merge_MovesBothCatsToTheSurvivorAndEndsTheDuplicatesSession` — watched pass; confirms both cats' `ownerId` moves and the duplicate's own session returns `401` after merge. Does not separately assert two distinct `BusinessEvent` audit rows for the two cat reassignments (the reassignment is change-capture, not a named `BusinessEvent` — the wording in §16 assumed a shape the shipped design does not use) |
| I-I3 | Re-send | `200 isReapplication: true`; state unchanged; a cat registered on the duplicate id after the merge now moved | PASS | **New test, added at this gate.** `DuplicateCustomerGroupsEndpointTests.ReSendingTheSameMerge_ReturnsIsReapplicationTrue_StateUnchanged` — watched pass |
| I-I4 | Non-duplicates | `400 CustomersNotDuplicates` | PASS | **New test, added at this gate.** `DuplicateCustomerGroupsEndpointTests.Merge_TwoUnrelatedCustomers_Returns400CustomersNotDuplicates` — watched pass |
| I-I5 | As `Reception` | `403` | PASS | **New test, added at this gate.** `DuplicateCustomerGroupsEndpointTests.Merge_AsReception_Returns403` — watched pass |
| I-J1 | Export | `200` attachment, `schemaVersion: 1`, profile, addresses, preferences, cats; `no-store`; export audit row | PASS | **New test, added at this gate.** `ProfileEndpointTests.ExportPersonalData_ReturnsAttachmentWithSchemaVersionProfileAddressesPreferencesAndCats` — watched pass. Does not independently query the audit table for the export's own audit row (asserted only via the command's own success) |
| I-J2 | Delete with the right password | `204`; cookies expired; row `Anonymised`; sign-in fails with the generic `401` | PASS | **New test, added at this gate.** `ProfileEndpointTests.DeleteAccount_WithRightPassword_AnonymisesEndsSessionAndSignInFailsGenerically` — watched pass |
| I-J3 | A new registration with the deleted mobile | `201` — filtered index freed it | PASS | **New test, added at this gate.** `ProfileEndpointTests.NewRegistration_WithAPreviouslyDeletedMobile_Succeeds` — watched pass |
| I-J4 | Delete with the wrong password ×3 | `ReauthenticationFailed` then the account locks | PASS | **New test, added at this gate.** `ProfileEndpointTests.DeleteAccount_WrongPasswordThreeTimes_LocksTheAccountSameThrottleAsSignIn` — watched pass (also confirms the 4th, correct-password attempt is refused `AccountLocked`) |
| I-J5 | Audit for the deletion | Actor = customer; `DataChange` row has no before-values for anonymised fields | BLOCKED | No test exists. Not added at this gate — the general audit-row-shape guarantee is exercised by I-A6/U-A9 for other aggregates; a deletion-specific row-content assertion was judged lower priority than I-J1–I-J4's end-to-end coverage of a previously entirely untested story, given this gate's remaining time. See "Cases I could not run" |
| I-X1 | Capture logs across every B, C, D, J case | No password, token, token hash, CAPTCHA answer or unmasked identifier in any line | PASS, caveat | `SessionResolutionFailureTests`' two cases use `AccountsApiFactory.WithCapturedLogs()` and confirm no plaintext password appears across those two specific flows (a DB-fault-during-session-resolution case and a throw-before-model-binding case). Not re-run across literally every B/C/D/J case in this suite — judged as partial, real coverage of the mechanism, not the full sweep the case's wording describes |
| I-X2 | Scan every response body in this suite | No field matching `/pass\|hash\|stamp\|secret/i` | BLOCKED | No blanket, cross-cutting response-body scan exists or was added at this gate — it would need a shared `DelegatingHandler` wrapping every test client in the suite, a test-infrastructure change judged too invasive to add unilaterally within this gate's scope. Individual endpoints are spot-checked by name instead (e.g. I-E1's regex check, I-B11's PHC-format check) |

## Deferred gate items (named explicitly by ARCHITECTURE fifth pass and SECURITY third pass, owed to TEST)

Each of these was raised by an earlier gate as a runtime claim it could not itself execute. D3 and D13
had **no** committed test before this gate.

| ID | Deferred item | Source | Case | Status | Evidence |
|---|---|---|---|---|---|
| D1 | CSRF survives a simulated process restart (customer) | ARCHITECTURE 4th pass HIGH / SECURITY 2nd+3rd pass HIGH | `CsrfRestartTests.Customer_AfterSimulatedRestart_PutSignOutAndReSignIn_AllSucceedOnTheSecondHost` | PASS | Watched pass against current code. **Proven non-decorative**: reverted `src/` to the pre-fix commit (`c36d1d0`, immediately before `53fb683`) in an isolated scratch clone — both `CsrfRestartTests` cases **failed 2/2** with `403 Forbidden` (the exact process-lifetime-key lockout the fix removes); restored and re-ran — **2/2 passed** |
| D2 | CSRF survives a simulated process restart (staff) | same | `CsrfRestartTests.Staff_AfterSimulatedRestart_WalkInSignOutAndReSignIn_AllSucceedOnTheSecondHost` | PASS | Same decoration check as D1 (both cases in one file, one revert) |
| D3 | `[CsrfExempt]` skips session resolution entirely for sign-in, not only the CSRF check — an idle-expired staff cookie presented to sign-in must not itself end/audit the old session | ARCHITECTURE 5th pass MEDIUM (finding 1) | `StaffSessionEndpointTests.SignInWithAnIdleExpiredCookie_NeitherEndsNorAuditsTheOldSession_UntilItIsNextPresentedToANonExemptEndpoint` (**new test, added at this gate**) | PASS | Watched pass: re-sign-in with an idle-expired cookie leaves the old session un-ended and un-audited (`EndedOn` still null, zero `StaffSessionIdleExpired` rows), and only ends/audits it once that same cookie is next presented to a non-exempt endpoint (`GET /api/staff/session` → `401 session_idle_timeout`, then exactly one audit row) |
| D4 | `ProfileUpdate` is its own rate-limit bucket, 5-per-60-min | ARCHITECTURE 5th pass MEDIUM (finding 2) / Amended (18) correction | `ProfileEndpointTests.UpdateProfile_UnderTightProfileUpdateLimit_SixthReturns429` | PASS | Watched pass |
| D5 | `ProfileUpdate` does not share a bucket with `AccountSensitive` (deletion/export) | same | `ProfileEndpointTests.UpdateProfile_FiveProfileSaves_DoNotBlockADeletionAttempt` | PASS | Watched pass |
| D6 | `ProfileUpdate` is partitioned by customer id, not IP | same | `ProfileEndpointTests.UpdateProfile_TwoDifferentCustomersFromTheSameIp_DoNotShareTheProfileBucket` | PASS | Watched pass |
| D7 | `X-Forwarded-For` cannot be used to bypass a rate limit with no trusted proxy configured | SECURITY 1st pass HIGH (closed pass 2, regression-guarded) | `RateLimitingTests.SignIn_RotatingXForwardedFor_StillHitsTheRateLimit` | PASS | Watched pass against current code. **Proven non-decorative**: temporarily reverted `HostPipelineExtensions.cs`'s `ForwardedHeadersOptions` block to the pre-fix behaviour (empty `KnownProxies`/`KnownNetworks` with `ForwardedHeaders.XForwardedFor` set — "trust any peer") in the same isolated scratch clone — the test **failed** (8 rotating XFF requests → all `401`, never `429`, the exact HIGH bug); restored and re-ran — **passed** |
| D8 | The `AddAccounts` migration backfills pre-existing audit rows to a valid category **before** the immutability triggers are installed | ARCHITECTURE 2nd pass MEDIUM | `AddAccountsAuditBackfillTests.AddAccounts_BackfillsPreExistingAuditRows_ToBusinessEventAndIsRecordedTrue_BeforeTriggersAreInstalled` | PASS | Watched pass, against a fresh disposable scratch database |
| D9 | A locked customer account still burns one Argon2 dummy verify (no timing/call-count oracle) | SECURITY MEDIUM (ADR-028) | `TimingEnumerationTests.CustomerSignIn_LockedAccount_StillCallsVerifyAgainstDummy` | PASS | Watched pass against current code. **Proven non-decorative**: temporarily removed the `VerifyAgainstDummy` call from `SignInCustomerCommandHandler`'s locked-account catch block, in the same scratch clone — the test **failed** with its own named assertion message; restored and re-ran — **passed** |
| D10 | Same, staff | same | `TimingEnumerationTests.StaffSignIn_LockedAccount_StillCallsVerifyAgainstDummy` | PASS | Same decoration check applied to `SignInStaffCommandHandler` — **failed**, then restored and **passed** |
| D11 | A database fault during session resolution on a password-bearing request fails closed and never logs the password | ARCHITECTURE 2nd pass HIGH | `SessionResolutionFailureTests.DatabaseFaultDuringSessionResolution_OnAStepUpRequestWithAStaleCookie_FailsClosedWithoutLoggingThePassword` | PASS | Watched pass against current code |
| D12 | An exception before model binding on sign-in never logs the password | SECURITY LOW | `SessionResolutionFailureTests.ThrowBeforeModelBinding_OnSignIn_DoesNotLogThePassword` | PASS | Watched pass against current code. **Proven non-decorative**: reverted `Program.cs`/`AccountsModule.cs`'s exception-handler wiring to the commit immediately before `56bc233` (deleting `SafeExceptionHandlerMiddleware`) in the scratch clone — this exact test **failed**, with the plaintext password found in a captured log line from `Platform.Infrastructure.Host.WebApi.Middlewares.GlobalExceptionHandlerMiddleware`; restored and re-ran — **passed** |
| D13 | A session row written before the `AddSessionCsrfTokenHash` migration (`csrf_token_hash=''`) fails closed on every unsafe request until a safe request re-issues a fresh token | ARCHITECTURE 4th pass Q1 / SECURITY 3rd pass assessment 1 — previously verified only by an uncommitted probe | `LegacyCsrfTokenHashTests.*` (**3 new tests, added at this gate**) | PASS | All 3 watched pass: a real header matching the pre-migration session's own (now-legacy) hash is refused; an empty header against an empty stored hash is refused, not wrongly matched; a safe `GET` re-issues a fresh token, which then works |
| D14 | The unused `Platform.Infrastructure.Host.WebApi` package reference contributes no controllers | ARCHITECTURE carried item | `HostCompositionTests.MapControllers_ExposesNoControllerFromThePlatformHostWebApiAssembly` (pre-existing) | PASS | Watched pass, part of the 124/124 |

## Regression cases from earlier gates

One row per finding raised by an earlier gate and fixed, that is not already listed above as a
deferred item.

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | SECURITY 2nd pass | LOW: a customer could squat another active customer's primary mobile as an "additional" number | `ProfileEndpointTests.UpdateProfile_AdditionalNumberMatchesAnotherActiveCustomersPrimary_Returns400` | PASS | Watched pass |
| R2 | SECURITY 2nd pass (same finding) | An additional number accepted before the fix must not retroactively block an unrelated later edit | `ProfileEndpointTests.UpdateProfile_UnrelatedEditWithAPreviouslyAcceptedAdditionalNumber_StillSucceeds` | PASS | Watched pass |
| R3 | HO-ACC-001 §1 Amended (16) (SECURITY-driven) | A walk-in registration must still succeed when the victim's number was squatted as someone else's additional number | `WalkInEndpointTests.VictimsNumberSquattedAsSomeoneElsesAdditionalNumber_ReceptionCanStillRegisterTheVictim` | PASS | Watched pass |
| R4 | HO-ACC-001 §1 Amended (16)/(h) | Duplicates report labels a shared additional number as `"Additional"`, not `"Primary"` | `DuplicateCustomerGroupsEndpointTests.TwoCustomers_SharingAnAdditionalNumber_AreBothLabelledAdditionalNotPrimary` | PASS | Watched pass |
| R5 | SECURITY 2nd pass MEDIUM | Email step-up (`PUT /me`) requires `currentPassword` when the email is changing | `ProfileEndpointTests.UpdateProfile_SettingEmail_WithoutCurrentPassword_Returns400` | PASS | Watched pass |
| R6 | SECURITY 2nd pass MEDIUM (same) | Wrong step-up password refused, correct one succeeds | `ProfileEndpointTests.UpdateProfile_SettingEmail_WithWrongCurrentPassword_Returns400` / `UpdateProfile_SettingEmail_WithCorrectCurrentPassword_Succeeds` | PASS | Watched pass, both cases |
| R7 | Amended (18)(f) SECURITY MEDIUM | Step-up path is an unthrottled password oracle otherwise — repeated wrong step-up locks the account | `ProfileEndpointTests.UpdateProfile_SettingEmail_RepeatedWrongPassword_LocksTheAccount` | PASS | Watched pass |
| R8 | SECURITY MEDIUM (CORS) | CORS grants only a configured origin, never a wildcard-with-credentials | `CorsAndCsrfTests.Preflight_FromAnUnconfiguredOrigin_GetsNoCorsGrant` / `Preflight_FromAConfiguredOrigin_GetsTheCorsGrant` / `Preflight_FromAConfiguredOrigin_DoesNotGrantADifferentOrigin` | PASS | Watched pass, all three |
| R9 | SECURITY MEDIUM (cookie tossing) | A planted `pc_csrf` cookie matching its own header is still rejected — the value is bound to the session, not self-referential | `CorsAndCsrfTests.PlantedCsrfCookie_MatchingItsOwnHeader_IsStillRejected` | PASS | Watched pass |
| R10 | (control for R9) | The real, session-issued CSRF token is accepted | `CorsAndCsrfTests.RealCsrfCookie_EchoedInTheHeader_IsAccepted` | PASS | Watched pass |
| R11 | ARCHITECTURE 2nd pass LOW | `[PassiveSession]` idle-expired session is made terminal and audited on its own detection, not left to a later request | `StaffSessionEndpointTests.PassiveGet_IdleExpired_EndsTheSessionAndAuditsExactlyOnce` | PASS | Watched pass |
| R12 | ARCHITECTURE MEDIUM | `[PassiveSession]` applies `SessionStateRules.Classify` — an idle-expired session's first post-idle passive GET does not return identity | `StaffSessionEndpointTests.PassiveGet_IdleExpired_AsTheFirstRequestAfterIdling_ReturnsSessionIdleTimeoutNotIdentity` | PASS | Watched pass |
| R13 | ARCHITECTURE MEDIUM (sibling) | Absolute expiry on the passive path still refuses cleanly with no identity | `StaffSessionEndpointTests.PassiveGet_AbsoluteExpired_ReturnsSessionInvalidNotIdentity` | PASS | Watched pass |
| R14 | ARCHITECTURE 5th pass LOW | `ShouldRefuseUnsafeRequest` fails closed if a caller is ever set without a resolved session | `SessionAuthenticationMiddlewareTests.NonAnonymousCaller_WithNoResolvedSession_FailsClosed` (+ 3 sibling cases in the same file) | PASS | Watched pass, all 6 cases in the file |
| R15 | ACCEPTANCE 1st pass LOW | `GET /api/auth/session` stamped `AsOf = DateTimeOffset.UtcNow` instead of the already-injected `TimeProvider`, risking disagreement with `SessionAuthenticationMiddleware`'s own clock reads under a fake/controlled clock | Fixed at `ed92088` (one-line change, `AuthController.cs`) — no dedicated test was added | BLOCKED | No test constructs `AuthController` with a `FakeTimeProvider` set to disagree with wall-clock time to prove the two paths now agree — the sibling middleware has exactly this shape of test (R14's `SessionAuthenticationMiddlewareTests`), `AuthController` does not. Verified only by reading the one-line diff, not by watching a test fail then pass; behaviour under the real system clock is unchanged, so I-C1/I-C6/I-C8 continuing to pass is expected either way and is not evidence for this specific fix. See "Cases I could not run" |
| R16 | CODE_REVIEW 2nd pass LOW | Every audit-writing site's `OccurredOn` read `DateTimeOffset.UtcNow` directly instead of the injected `TimeProvider` (16 handlers + the interceptor), unlike every other timestamp in this handover | Fixed at `fdec8b0` — mechanical, no test constructs any of these handlers directly (only via the full DI-resolved pipeline, per real system time) | PASS, caveat | Full-suite green both before and after (`388/388`), confirming no observable regression under the real clock this pass ran against. No controlled-clock test exists or was added for these 16 sites specifically (same gap as R15) — under real time, `TimeProvider.System.GetUtcNow()` and `DateTimeOffset.UtcNow` are indistinguishable, so no integration test in this suite could have caught a regression here even if one existed; the fix's correctness rests on the mechanical nature of the change (constructor already took, or trivially could take, the same singleton `TimeProvider` every other timestamped call site already uses) |

## Acceptance-criteria coverage

Mirrors §18. Status records whether every cited case actually passed at this gate.

| Criterion | Covered by | Status |
|---|---|---|
| A1 — ready-to-use account, no verification state | U-B1, U-B3, I-B1, I-B2 | PASS |
| A2 — neither identifier → told one is required | U-B2, I-B3 | PASS |
| A3 — CAPTCHA refused before any account check; fresh challenge obtainable | U-B10, I-B7, I-B8, I-B12 | PASS |
| A4 — over the limit → `429` with retry time | I-B9 | PASS |
| A5 — duplicate mobile discloses nothing about email | U-B6, I-B4 | PASS |
| A6 — duplicate email discloses nothing about mobile | U-B7, I-B5 | PASS |
| A7 — short/breached passwords, distinct reasons | U-B4, U-B8, I-B6 | PASS |
| A8 — concurrent duplicate registration → one account, one `400` | I-B10 | PASS |
| A9 — sign-in by mobile or email; no OTP path | I-C1, I-C2, U-B3 | PASS |
| A10 — unknown identifier / wrong password indistinguishable | U-C5, I-C3 | PASS |
| A11 — progressive delays; attempts logged | U-A4, U-C1, I-C4, I-C5 | PASS |
| A12 — reset request neutral; token sent to registered channel | U-D3, I-D1, I-D2 | PASS |
| A13 — used/expired token rejected; another can be requested | U-D1, U-D4, I-D4, I-D6 | PASS |
| A14 — successful reset invalidates all other sessions | U-D2, I-D5 | PASS |
| A15 — multiple labelled addresses, exactly one default | U-E4, U-E5, I-E2, I-E3, I-E4 | PASS |
| A16 — marketing opt-outs honoured; transactional cannot be turned off | U-E7, U-E8, I-E6, I-E7 | PASS |
| A17 — administrator assigns Appendix D roles | U-F1, U-F5, I-F1, I-F5 | PASS |
| A18 — non-administrator refused server-side, attempt logged | U-F6, U-F7, I-F2 | PASS |
| A19 — staff sign-in; session carries roles | I-G1 | PASS |
| A20 — new staff set first password via reset | I-D7, I-G1 | PASS |
| A21 — customer credentials never yield a staff session | U-G5, I-G2 | PASS |
| A22 — idle beyond configured period → re-auth required, terminally | U-G2, U-G3, I-G3 | PASS |
| A23 — refused request no side effect; status endpoint returns `staffAccountId` | I-G4, I-G6, I-G7 | PASS |
| A24 — walk-in with name+phone created incomplete | U-H1, I-H1 | PASS |
| A25 — (provisional, SRS Q-16) setting a password completes the record | U-H2, I-H3 | PASS |
| A26 — records sharing phone/email listed with cats | I-I1 | PASS, caveat (see I-I1's own row) |
| A27 — merge moves all cats, audited; re-send recovers | U-I3, U-I4, I-I2, I-I3 | PASS |
| A28 — every state-changing action audited, same transaction | U-A8, I-A6, I-A7, I-A9–I-A16, I-A8 | PASS, caveat — I-A8 itself is still BLOCKED (no test); everything else the criterion touches (correct before/after content, not merely row existence) is now directly and non-decoratively proven, closing the gap ACCEPTANCE's 1st-pass FAIL found in U-A8/I-A6. See the "Second pass" note |
| A29 — editing/deleting an audit entry refused, app and DB | U-A6, U-A7, I-A4, I-A5 | PASS, caveat — I-A4's TRUNCATE sub-case is BLOCKED (see its own row); UPDATE and DELETE are both proven |
| A30 — deletion anonymises personal data, ends sign-in | U-J1, I-J2 | PASS |
| A31 — clinical records retained, owner shown anonymised | I-J2 | PASS |
| A32 — export is machine-readable | I-J1 | PASS |
| A33 — deletion audited with who/when | I-J5 | BLOCKED — see I-J5's own row. Not independently re-proven elsewhere; recorded as a gap for ACCEPTANCE |
| A34 — Argon2id; no plaintext anywhere | U-B14, I-B11, I-X1, I-X2 | PASS, caveat — I-X1 is partial and I-X2 is BLOCKED (see their own rows); the Argon2id/no-plaintext claim itself is otherwise well covered by U-B14, I-B11 and the four decoration checks under D1/D7/D9/D10/D12 |
| A35 — cookies HttpOnly/Secure/SameSite; unsafe requests need CSRF | I-C1, I-C7 | PASS |
| A36 — host starts with scope validation, every registration resolves | I-A1, I-A2 | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| I-A4 (TRUNCATE sub-case) | §16.1 rule 4 reads as an absolute ban on issuing `TRUNCATE` against the shared `catecommerce_dev`, not only a ban on using it to reset test state — this gate chose not to risk it even against the expectation that the trigger refuses it | Run once, deliberately, against a disposable scratch database (the same pattern `MigrationsOnAnEmptyDatabaseTests`/`AddAccountsAuditBackfillTests` already use), migrated to the current head, then `TRUNCATE audit.audit_entry` and assert it raises |
| I-A8 | No seam exists to force a handler to throw specifically after the change-capture interceptor has queued its audit row but before `SaveChangesAsync` commits, without either a throwing test-only interceptor or a colliding concurrent write timed precisely inside the same transaction | Add a test-only `SaveChangesInterceptor` (registered only in a dedicated `WebApplicationFactory` variant) that throws immediately after the base interceptors run, then confirm both the business change and its audit row are absent afterward |
| I-F6 | Same reasoning as the developer's own (see the row) — the guard's "last Administrator" check is global, not scoped to this test's own data, in a shared database this gate must not truncate or assume empty | A dedicated, fully isolated test database (or an explicit `IAdministratorCountReader` test seam that can be substituted) — a bigger change than this gate's scope |
| I-J5 | Judged lower priority than closing I-J1–I-J4's complete absence of coverage, given this gate's remaining time after the CSRF/timing/rate-limit decoration checks | Query `audit.audit_entry` after `DeleteAccount_WithRightPassword...` for a `DataChange` row with `ActorUserId` equal to the deleted customer's own id and confirm no personal-data field appears in its `BeforeValue` |
| I-X1 (full sweep) | `WithCapturedLogs()` is exercised for exactly two flows in this suite (`SessionResolutionFailureTests`); running literally every B/C/D/J case against a captured-log factory and scanning the combined output was judged out of this gate's remaining scope | Route every B/C/D/J-section test through `AccountsApiFactory.WithCapturedLogs()` (or a suite-wide fixture) and scan the aggregate log for the named patterns at suite teardown |
| I-X2 | No shared response-body-capturing `DelegatingHandler` exists across this suite's `HttpClient` instances | Add one to `AccountsIntegrationTestBase.CreateClient()` (and the factory's other client-creation paths), capturing every response body into a collection scanned once at the end of the run |
| R15 (second pass) | No test constructs `AuthController` with a controlled/fake `TimeProvider` to prove it now agrees with `SessionAuthenticationMiddleware`'s own clock reads (the ACCEPTANCE LOW finding `ed92088` fixed) | Add a test in the same shape as `SessionAuthenticationMiddlewareTests` (R14): a `FakeTimeProvider` set to a value that would flip session freshness, asserting `GET /api/auth/session` and the middleware's own gate agree |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| Reset delivery outside `Development` | No Notification context yet (§4.3, §20.4) — named as a deliberate gap by the handover itself, not a test omission | ACCEPTANCE / owner, tracked against HO-NTF-001 |
| Orders/invoices/balances in the duplicates report and merge | Those contexts do not exist (§4.3) | ACCEPTANCE / owner |
| Audit log query/export screen | Belongs to Administration (§4.3) | Future HO-ADM handover |
| Role guards on the Cat Profile endpoints | HO-CAT-001's own follow-up, explicitly out of scope here (§4.3, §20.5) | Follow-up amendment to HO-CAT-001 |
| A dedicated `dotnet ef migrations has-pending-model-changes` re-check | ARCHITECTURE fifth pass already ran this read-only check clean against `2ff0a6d`; this gate's own new test files add no mapped types, so the model is unaffected | Re-check only if a future pass touches mapped types |
