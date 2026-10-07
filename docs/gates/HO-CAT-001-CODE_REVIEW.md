# Fifth pass (delta bbd005a..20225d3)

GATE: CODE_REVIEW
HANDOVER: HO-CAT-001 (+ the owner-authorised HO-CAT-002 web change on this branch)
COMMITS: bbd005a..20225d3 — 0a3d028 (new `PublicSiteOptions` + `PublicSiteStartupGuardHostedService`,
section `PublicSite:WebBaseUrl`, replacing `CatProfileOptions:PublicWebBaseUrl`), d207d26 (QR decode
test with a BCL-only PNG decoder, `QrPngDecoder.cs`, + `ZXing.Net` 0.16.10 test-only package),
efae3ec + 47c9ba2 (cat list read model and web `CatListItem` carry `handlingNote`), b673a0c
(ACCEPTANCE gate's own scratch probes turned into checked-in tests, `CatAcceptanceGateEndpointTests.cs`,
including a deterministic `409` via a held Postgres row lock), 2e18d05 (TC sheet corrected to cite
the new tests), c5cfdb2 (`docs/handover/HO-CAT-001.md`/`HO-CAT-002.md` §1 Amended rows), 20225d3
(web-only, owner-authorised: `HighRiskBadge` gains `variant="list"`, showing the handling note in
the **staff** cat list; the portal list is unchanged). This is the ACCEPTANCE gate's own first-pass
FAIL (A22, `docs/gates/HO-CAT-001-ACCEPTANCE.md`, `bbd005a`) worked through to a fix, plus the owner
decision this fix's own A9 discovery required. Reviewed as a short delta gate; the fourth pass
(7e22d6e, PASS) already covers everything before this range in full.
DATE: 2026-09-27
STATUS: PASS

## History
- **First pass** (7abc9e4): FAIL. 2 HIGH, 4 MEDIUM, 1 LOW carried.
- **Second pass** (67f5865): PASS. 1 MEDIUM, 4 LOW (new).
- **Third pass** (80d2ed5): PASS. 1 MEDIUM (new) — the merged/anonymised-owner gap this pass closes.
- **Fourth pass** (7e22d6e): PASS. 0 BLOCKER, 0 HIGH, 0 MEDIUM (new), 0 LOW (new).
- **Fifth pass** (this section, 20225d3): PASS. 0 BLOCKER, 0 HIGH, 1 MEDIUM (new, defence-in-depth
  on `PublicSiteStartupGuardHostedService`'s URL shape check), 0 LOW (new).

## What I reviewed
- `PublicSiteOptions.cs` (new) and `PublicSiteStartupGuardHostedService` (`CatProfilesModule.cs`) in
  full, against `AccountStartupGuardHostedService` (`AccountsModule.cs:331`) — both resolve
  `IHostEnvironment` via DI (not a raw `ASPNETCORE_ENVIRONMENT` configuration key, which does not
  reflect `WebApplicationFactory`'s `UseEnvironment`), both throw from `StartAsync` to abort host
  startup, both correctly treat `"Testing"` as a third environment distinct from
  `IsDevelopment()`/`IsProduction()`. Consistent shape, no divergence.
- Every branch of the guard's own validation by hand, against the task's specific bad-value list —
  see the new MEDIUM below and the "task-specific checks" section.
- `GetCatCageCardQueryHandler.cs`'s diff: `row.Id` (a `Guid` from the database, never attacker text)
  is the only thing interpolated into the path; `WebBaseUrl.TrimEnd('/') + basePath` cannot be
  manipulated by a caller — the only input to this string is the trusted config value itself.
- `QrPngDecoder.cs` in full: a from-spec PNG decoder (IHDR parse, zlib inflate via the BCL
  `ZLibStream`, per-spec filter reconstruction for all five PNG filter types even though only two
  ever appear in QRCoder's own output) scoped explicitly to the one fixed shape
  `QRCoder.PngByteQRCode.GetGraphic` produces (non-interlaced, 1-bit grayscale, no palette) — it
  throws `NotSupportedException` on any other `bitDepth`/`colorType`/`interlace` combination rather
  than silently misreading it, so a future QRCoder version change that alters the output format
  would fail loudly, not decode garbage. Confirmed `ZXing.Net` (`tests/CatEcommerce.IntegrationTests.csproj`
  only, `grep ZXing **/*.csproj` finds no `src/` reference) is Apache-2.0, no commercial tier — the
  same class of licensing question the QuestPDF §20.6 decision needed, correctly avoided here by
  picking a permissively-licensed dependency instead of requesting another owner decision.
- `GetCatCageCard_QrPayload_DecodesToTheAbsoluteProfileUrl` (`CatProfileProjectionEndpointTests.cs`):
  decodes the actual returned PNG bytes end to end (`QrPngDecoder` → `RGBLuminanceSource` →
  `HybridBinarizer` → `MultiFormatReader.decode`) and asserts the exact string
  `$"{webBaseUrl}/cats/{catId}"` — a real assertion on the payload, not the envelope-only check the
  ACCEPTANCE gate correctly flagged as insufficient (`GetCatCageCard_Returns200WithNoStoreCacheControl`,
  left unchanged and still useful for the no-store/content-type case). Uses
  `CatApiFactory.WithPublicSiteWebBaseUrl`, the same "second factory instance, one setting
  overridden" technique `AdministrationApiFactory.WithLoweredAuditExportCap` already uses elsewhere
  in this suite — not a new pattern.
- `CatAcceptanceGateEndpointTests.cs` in full, all eight cases, cross-checked one by one against the
  ACCEPTANCE gate's own P1/P3–P7 probe descriptions (`docs/gates/HO-CAT-001-ACCEPTANCE.md`) — each
  test asserts what its probe claimed, not a weaker restatement. In particular
  `ConcurrentProfileUpdate_ForcedRowLockRace_Returns409AndKeepsTheCompetingEdit` (P1/A24): a second
  raw `NpgsqlConnection` opens a transaction and `UPDATE`s the cat row directly, holding an exclusive
  row lock; the PUT is issued and `Task.WhenAny` against a 3 s `Task.Delay` proves it is still
  blocked; only then is the locking transaction committed, and the PUT is awaited to completion and
  asserted `409`. Checked specifically for a hang-on-failure risk (task instruction): the locking
  connection and transaction are both `await using`, declared in the order `lockingConnection` then
  `lockingTransaction`, so C#'s reverse-declaration-order disposal rolls back the transaction (hence
  releases the row lock) before the connection closes, on **any** exit path from the method,
  including the `Assert.False` on line 245 throwing — the row lock cannot outlive a failed
  assertion, so a failure here fails fast rather than hanging the test run or blocking a later test
  against the same row. `putResponse`/`getResponse` are only read after the lock is confirmed
  released, so there is no scenario where the PUT is awaited while still legitimately blocked.
- `GetCatsQueryHandler.cs`/`CatListItemViewModel.cs` (A9 fix): `HandlingNote` is added to the same
  `AsNoTracking().Select(...)` DB-translatable projection `IsHighRisk` already used — the two-phase
  projection pattern (DB-translatable Enumeration members, then client-side `DisplayName`) this
  handler's own comment documents is unchanged in shape, just one more scalar column. Read-side
  pattern (`.ai/read-side-pattern.md`) intact: no aggregate rehydration, `AsNoTracking()` present, a
  projection not a bare `IQueryable` returned.
- `web/lib/api/cats.ts`, `web/mocks/handlers/areas/cat-profiles.ts`,
  `web/app/(staff)/staff/cats/page.tsx`, `web/components/cat/HighRiskBadge.tsx` (`variant="list"`,
  new) and `web/tests/unit/cat/cat-list-page.test.tsx`'s two new cases: `handlingNote` flows through
  the type, the mock handler, and the staff list's `HighRiskBadge` prop untouched (`{handlingNote}`
  in JSX, `title={handlingNote}` on the `<span>`) — confirmed `grep dangerouslySetInnerHTML` finds
  nothing in either changed file, so React's default text/attribute escaping is the only thing
  rendering this clinical free-text field; no injection surface. The portal list
  (`app/(portal)/portal/cats/page.tsx`) is untouched and still passes `handlingNote={null}` with
  `variant="compact"`, matching the handover's stated customer/staff split exactly.
- Checked whether showing `handlingNote` in the **staff** list creates a new authorization gap: both
  `GetCats` (list) and `GetCat` (profile) are gated by the identical `[RequireAction(Actions.Cats.View, ...)]`,
  and `ActionRoles.Administration.cs:13` grants `Cats.View` to
  `[Reception, Nurse, Vet, Caretaker, Administrator]` — every staff role that could already see this
  same note on the profile endpoint can now see it in the list too. Nothing widens who can see it;
  this only removes an extra click, and the handover's own US-CAT-04 row explicitly calls for the
  note on "clinic / boarding / order screens" — Reception and Caretaker seeing it is the point, not
  an oversight. No finding here.
- `docs/test-cases/TC-HO-CAT-001.md`'s diff cross-checked against the actual new tests: every A-row
  the ACCEPTANCE gate flagged as under-evidenced (A1, A8, A9, A11, A17, A24) and I14/I21 now cite the
  correct new test by name, and the added prose states in-line what gap each closes, matching what
  the tests actually assert (verified independently above, not just trusted).
- `docs/handover/HO-CAT-001.md`/`HO-CAT-002.md`'s new Amended rows (c5cfdb2): read in full against
  the code — accurately describe all three owner decisions (a: pending smart-redirect page, b: the
  shared `PublicSiteOptions`, c: `handlingNote` on the list) and the work items each required. No
  discrepancy found.

## New finding, this pass
- **[MEDIUM] `PublicSiteStartupGuardHostedService` (`CatProfilesModule.cs`) validates the URL's
  path/query/fragment are empty but never checks `Uri.UserInfo`, so a value like
  `https://legit-looking@attacker.example` passes every check the guard performs.**
  `PublicSiteStartupGuardHostedService.StartAsync` checks, in order: absolute + http(s) scheme,
  `uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0`, then the
  Production/https rules — but never inspects `uri.UserInfo`. Confirmed directly against the real
  BCL `Uri` type (a throwaway console program, not decompiled): `Uri.TryCreate("https://evil.example@trusted.example", UriKind.Absolute, out var uri)`
  succeeds, with `uri.Host == "trusted.example"`, `uri.UserInfo == "evil.example"`,
  `uri.AbsolutePath == "/"`, `uri.Query == ""`, `uri.Fragment == ""` — every one of the guard's own
  checks passes. The resulting `GetCatCageCardQueryHandler` output would literally be
  `https://evil.example@trusted.example/cats/{id}` — a URL whose leading text reads as one host but
  that actually navigates to another, the same visual-deception shape phishing URLs use userinfo
  for. `PublicSite:WebBaseUrl` is operator/deployment configuration, not end-user input, so this is
  not remotely exploitable by an external attacker today — but the guard's own stated purpose is
  exactly "no path, query or fragment", i.e. reject anything in the URL beyond a bare origin, and
  userinfo is the same class of extraneous component the guard already explicitly excludes for the
  other three; missing it is a real, if narrow, gap in a check whose whole job is to keep this value
  to a bare origin before it gets baked into every QR code and (once Notifications adopts it) every
  notification link the system sends. Fix: add `if (!string.IsNullOrEmpty(uri.UserInfo)) throw ...`
  alongside the existing path/query/fragment check.
- Also confirmed, not findings: leading/trailing whitespace and a trailing tab in the configured
  value are silently trimmed by `Uri.TryCreate` itself (documented BCL leniency, not a Cat-specific
  behaviour) — a padded value normalizes rather than fails, which is a minor robustness note at most,
  not a security gap, since the trimmed result is still validated in full afterward. A trailing
  slash (`https://example.com/`) and no trailing slash (`https://example.com`) both normalize to the
  same `AbsolutePath == "/"` and are both accepted — correct, not an inconsistency, since
  `GetCatCageCardQueryHandler`'s own `TrimEnd('/')` treats them identically too. An IDN host
  (`https://xn--e1aybc.xn--p1ai`) parses and validates exactly like an ASCII host — no special
  handling needed or missing.

## Task-specific checks, answered directly
- **Bad values tried mentally, then confirmed against the real `Uri` type**: userinfo (passes —
  see the new MEDIUM), trailing slash (passes, correctly), leading/trailing whitespace (silently
  trimmed, then passes if the trimmed value is otherwise valid — not a gap since nothing bypasses
  validation, just a leniency in what "the same string" means), IDN (passes, correctly, no special
  case needed).
- **Is the startup guard consistent with `AccountStartupGuardHostedService`?** Yes — identical
  `IHostedService`/DI-resolved-`IHostEnvironment` shape, identical "empty string bypasses everything
  else" short-circuit style, identical treatment of `"Testing"` as its own environment. No
  divergence in pattern.
- **ZXing.Net licence and test-project-only?** Apache-2.0 (no commercial tier), and confirmed by
  `grep` that only `tests/CatEcommerce.IntegrationTests.csproj` references it — no `src/` project
  does.
- **Is the custom PNG decoder test-only and robust enough?** Yes to both — it lives under `tests/`,
  is `internal`, and is deliberately narrow: it throws rather than misinterprets any PNG shape
  outside the one QRCoder always emits, and its filter-reconstruction implements the full PNG spec
  (all five filter types) rather than only the ones observed in today's output, which is exactly
  right for something scoped to "this one library's output today" without silently rotting the
  moment a future QRCoder version changes its filter choice.
- **Is showing `handlingNote` in the staff list appropriate for every staff role that can list
  cats — anything the handover restricts?** No new exposure — see "What I reviewed" above.
  `Cats.View` already gated the identical field on the profile endpoint for the identical role set;
  the list change removes a click, not a barrier.
- **Is the note HTML-escaped (React default, no `dangerouslySetInnerHTML`)?** Confirmed —
  `{handlingNote}` as JSX children and `title={handlingNote}` as a plain attribute value, neither
  file uses `dangerouslySetInnerHTML`, `grep` across the changed web files confirms it.
- **Is the 409 test deterministic — row lock released in a `finally`, no hang on failure,
  timeouts?** Deterministic, and no hang risk, though not via an explicit `finally` — see "What I
  reviewed" above for why `await using`'s reverse-declaration-order disposal is equivalent here
  (rolls back and releases the lock on every exit path, including an assertion throwing). Not a
  finding; the task's own phrasing ("released in finally") describes the intent this code already
  achieves by a different, equally correct C# mechanism.

## Build / unit tests (this pass)
`git archive 20225d3` into a new scratch folder (`cat1-cr5`), deleted afterward.
- `dotnet build CatEcommerce.sln` — **0 warnings, 0 errors**.
- `dotnet test CatEcommerce.sln` against
  `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_cat;Username=postgres;Password=postgres`
  — **724/724 passed** (Domain 223, Infrastructure 28, Application 93, Integration 380 — 371 at the
  ACCEPTANCE gate's first pass, +9 new: the 8 `CatAcceptanceGateEndpointTests` cases and
  `GetCatCageCard_QrPayload_DecodesToTheAbsoluteProfileUrl`).
- `dotnet ef migrations has-pending-model-changes` — "No changes have been made to the model since
  the last migration." (This delta adds no new column/table; `HandlingNote` already existed on
  `clinic.cat`, only the read-side projection changed.)
- In the worktree's `web/`: `npx tsc --noEmit` — clean, no output. `npx eslint .` — clean, no
  output. `npx vitest run` — **106/106 passed** (104 at the ACCEPTANCE gate's first pass, +2: the two
  new `cat-list-page.test.tsx` cases).
- Scratch folder deleted afterward.

## Dead code / duplication
None of the superseded-architecture shapes appear anywhere in this delta (`grep` for
`IBusMessageDispatcher`, `AddServiceBusProvider`, `EventPublishOption`, `BsonIgnoreExtraElements`,
`IReadRepository`, `ViewModelBase`, `CustomQueueConfig`, `CommandConsumerAdapter`,
`EventConsumerAdapter`, MassTransit, RabbitMQ, MongoDB, or a `202` finds nothing in the diff). No new
`CommandService`/bare-repository-wrapper. `CatProfileOptions.PublicWebBaseUrl` is removed cleanly,
not left dangling alongside the new `PublicSiteOptions.WebBaseUrl` — a genuine replacement, not an
addition living next to dead code.

## SCOPE CHECK
- files in allowed scope: 21/21 (`docs/handover/HO-CAT-001.md`, `docs/handover/HO-CAT-002.md`,
  `docs/test-cases/TC-HO-CAT-001.md`, `CatProfilesModule.cs`,
  `appsettings.Development.json.example`, `appsettings.json`, `CatProfileOptions.cs`,
  `PublicSiteOptions.cs` (new), `GetCatCageCardQueryHandler.cs`, `GetCatsQueryHandler.cs`,
  `CatListItemViewModel.cs`, `CatApiFactory.cs`, `CatEcommerce.IntegrationTests.csproj`,
  `CatAcceptanceGateEndpointTests.cs` (new), `CatProfileProjectionEndpointTests.cs`,
  `QrPngDecoder.cs` (new), plus the 5 owner-authorised `web/**` files (§4.1/HO-CAT-002 §3's
  `handlingNote` carry, and HO-CAT-002 §1 Amended's `variant="list"` change)) — all within
  HO-CAT-001's own §13 surface or HO-CAT-002's owner-authorised web delta, matching what
  `docs/gates/HO-CAT-001-ACCEPTANCE.md` and the handovers' own Amended rows declare.
- files outside allowed scope: none.
- Result: **PASS. 0 BLOCKER, 0 HIGH, 1 MEDIUM (new), 0 LOW (new).** The A22 fix correctly closes the
  ACCEPTANCE gate's FAIL (absolute URL, a shared setting, a real QR-decode test); the A9 owner
  decision is correctly implemented end to end (handler → view model → web type → mock → UI) with
  React's default escaping intact; all six probes-turned-tests are faithful to what the ACCEPTANCE
  gate's own probes proved, including a genuinely deterministic forced-409 test with no hang risk on
  assertion failure. The one new MEDIUM is a narrow defence-in-depth gap in a config-value validator
  (userinfo not rejected alongside path/query/fragment) — not exploitable by an external attacker
  since the value is operator-configured, not user input, but worth closing before Notifications
  adopts the same setting for outbound links to a wider audience.

---

# Fourth pass (delta 2b40542..7e22d6e)

GATE: CODE_REVIEW
HANDOVER: HO-CAT-001
COMMITS: 2b40542..7e22d6e — cb2657c (RegisterCat/TransferCatOwnership require an Active customer,
closing the third pass's own MEDIUM), 3c8dcbf (CatOwnerCustomerStatusEndpointTests.cs, 4 cases),
48c4497 (DuplicateCustomerGroupsEndpointTests reseeds its late cat via the repository instead of
POST /api/cats, a regression the cb2657c fix itself caused), d5985fe (doc record), 7e22d6e (TEST
gate: TC sheet, verdict, the I1-I22 suite -- 7 new integration test files, 1718 lines). Reviewed as
a short delta gate; the third pass (2b40542, PASS) already covers everything before this range in
full.
DATE: 2026-09-27
STATUS: PASS

## History
- **First pass** (7abc9e4): FAIL. 2 HIGH, 4 MEDIUM, 1 LOW carried.
- **Second pass** (67f5865): PASS. 1 MEDIUM, 4 LOW (new).
- **Third pass** (80d2ed5): PASS. 1 MEDIUM (new) -- the merged/anonymised-owner gap this pass closes.
- **Fourth pass** (this section, 7e22d6e): PASS. 0 BLOCKER, 0 HIGH, 0 MEDIUM (new), 0 LOW (new).
  The third pass's own MEDIUM is closed with a correct fix and real tests; the TEST gate's new suite
  (7 files, 1718 lines) is genuine, deterministic and does not weaken anything.

## What I reviewed
- RegisterCatCommandHandler.cs/TransferCatOwnershipCommandHandler.cs's diff (cb2657c) against
  MergeCustomerCommandHandler.cs's own `survivor.Status != CustomerAccountStatus.Active` check and
  CustomerAccountStatus.cs (Active/Merged/Anonymised, a proper Enumeration) -- the new guard
  (`owner.Status != CustomerAccountStatus.Active` -> Violation(AccountNotActive, nameof(...OwnerId),
  ...)) is the identical shape and reuses the identical violation code, on the identical
  property-naming convention (nameof(command.OwnerId)/nameof(command.NewOwnerId)).
- CatOwnerCustomerStatusEndpointTests.cs (3c8dcbf) in full: both SeedMergedCustomerAsync (walks a
  fresh customer through RegisterWalkIn -> MergeInto -> UpdateAsync) and SeedAnonymisedCustomerAsync
  (RegisterWalkIn -> Anonymise -> UpdateAsync) against CustomerAccountAggregate.MergeInto/Anonymise
  read in full -- confirmed MergeInto never requires the survivor id to exist as a saved aggregate
  (it only stores denormalised survivor data on the duplicate's own row), so seeding a Merged row
  this way is faithful, not a shortcut that happens to compile. All four cases assert the actual
  errorCode/propertyName pair (AccountNotActive / OwnerId or NewOwnerId) from the response body -- a
  test that would fail immediately if the cb2657c guard were reverted or the wrong violation code
  were returned.
- DuplicateCustomerGroupsEndpointTests.cs's diff (48c4497) against
  ReassignCatsOnCustomerMergedHandler.cs read in full: the handler's resend-recovery loop
  (`GetByFilterAsync(c => c.OwnerId == duplicateId)`) never reads the owning customer's Status, only
  CatAggregate.OwnerId -- so the new SeedCatOwnedByAsync (seeds a CatAggregate directly via
  IAggregateRootRepository<CatAggregate>, bypassing the now-correctly-guarded HTTP endpoint) is a
  faithful stand-in for "a cat the first merge's reassignment loop had not yet reached", and
  ReSendingTheSameMerge_ReturnsIsReapplicationTrue_StateUnchanged's own ADR-039 assertions
  (isReapplication == true, the cat's OwnerId moves to the survivor, no double-processing) are
  unchanged -- only the means of reaching the pre-resend state changed.
- CatVaccinationStatusEndpointTests.cs in full against GetCatVaccinationStatusQueryHandler.cs:
  confirmed overallStatus is genuinely computed over `db.Vaccines.Where(v => v.IsCore && v.IsActive)`
  -- every active core vaccine in the whole shared catalogue, not scoped to one cat's own test data
  -- so asserting it in a class that shares catecommerce_cat with every concurrently-running test
  class is a real, previously-observed source of flakiness (documented in the TEST gate's own
  "Production bugs found" section with a captured Assert.Equal failure). Asserting each test's own
  vaccine entry in the per-vaccine `vaccines` array instead is deterministic and still exercises the
  real read model end to end over HTTP (S12.3 steps 1-3), which is what S16 I11 asks for. The one
  case that does still assert overallStatus (NoVaccinationsRecorded_ReturnsOverdueWithNeverAdministeredReason)
  is safe regardless of concurrent registrations: a never-administered core vaccine is already
  Overdue, the worst possible status, so no concurrently-added vaccine can push the aggregate any
  higher. This is an acceptable, well-reasoned trade -- a testability property of a shared,
  concurrently-written catalogue on a shared database, not a weakening of what S16 I11 actually needs
  proved.
- CatCrudLifecycleEndpointTests.cs's I7 case (ConcurrentProfileUpdates_NeverAServerErrorAndAtLeastOneSucceeds):
  asserts every one of 8 concurrent PUT responses is 200 or 409 (never 500), at least one 200, and
  the final GET reflects one of the attempted names -- the same "assert the legitimate outcome set,
  not a guaranteed interleaving" pattern this codebase's own ProfileEndpointTests I-E4/I-E5 already
  use for an identical black-box concurrency limitation on the Account side. Not a weaker assertion
  than a hypothetical guaranteed-409 test would be -- a guaranteed 409 cannot actually be forced over
  black-box HTTP without production-code test hooks, which this gate correctly does not add.
- TC-HO-CAT-001.md cross-checked against the actual test files: R10's row cites
  CatOwnerCustomerStatusEndpointTests (cb2657c/3c8dcbf) by name and case count (4), matches the file;
  I7's row states the {200, 409} outcome set and the Gaps section explains why a guaranteed 409 is
  not asserted, matching the test exactly; I11's Gaps row states the overallStatus
  cross-test-interference finding with the same captured failure message quoted in both the sheet
  and the TEST gate's own verdict file. No row overstates what its cited test actually checks.
- docs/gates/HO-CAT-001-TEST.md in full: verdict, scope, "Production bugs found" section (the three
  self-corrected test bugs, none of which were production defects) and its explicit statement that
  no existing test was weakened or deleted -- independently confirmed by diffing 48c4497 above,
  which is the one place an existing test's setup changed.
- docs/handover/HO-CAT-001.md's new S1 Amended row (d5985fe): accurately describes the
  cb2657c/3c8dcbf/48c4497 trio; no discrepancy with the code.

## Build / unit tests (this pass)
git archive 7e22d6e into a new scratch folder (cat1-cr4), deleted afterward.
- dotnet build CatEcommerce.sln -- **0 warnings (beyond the expected NU1900 offline-package-index
  notices), 0 errors**.
- dotnet test tests/CatEcommerce.Domain.Tests -- **223/223 passed** (unchanged from the third pass --
  this delta touches no Domain-layer file besides the two handlers, which are Application-layer).
- dotnet test tests/CatEcommerce.Application.Tests -- **93/93 passed** (unchanged count from the
  third pass -- the two handler files gained a guard clause each, no new Application-layer unit test
  was added in this delta; the new coverage is all integration-level).
- dotnet test tests/CatEcommerce.Infrastructure.Tests -- **28/28 passed** (unchanged).
- dotnet test tests/CatEcommerce.IntegrationTests against
  CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_cat;Username=postgres;Password=postgres
  -- **371/371 passed**, matching the TEST gate's own reported count exactly, run once as instructed.
- Scratch folder deleted afterward.

## Findings
None. The Active check is correct and consistent with MergeCustomerCommandHandler's own identical
check on the surviving customer; the four new CatOwnerCustomerStatusEndpointTests cases are real
(each asserts the specific errorCode/propertyName the fix returns, not a loose "it's 400"); the
DuplicateCustomerGroupsEndpointTests reseed is a faithful substitute that does not change what the
test actually proves; the vaccination-status per-vaccine reassertion is an acceptable,
well-documented response to a genuine shared-database race, not a weakening; the I7 {200, 409}
assertion is the correct shape for a genuine black-box concurrency race; and the TC sheet's claims
match the tests it cites.

## Dead code / duplication
None of the superseded-architecture shapes appear anywhere in this delta.

## SCOPE CHECK
- files in allowed scope: all non-web/ files in 2b40542..7e22d6e (RegisterCatCommandHandler.cs,
  TransferCatOwnershipCommandHandler.cs, 7 new integration test files,
  DuplicateCustomerGroupsEndpointTests.cs, docs/handover/HO-CAT-001.md,
  docs/test-cases/TC-HO-CAT-001.md, docs/gates/HO-CAT-001-TEST.md) -- all within HO-CAT-001's own
  S13 surface. web/ changes in this worktree (a concurrent frontend developer's own work) were
  explicitly out of scope per the task and not reviewed.
- files outside allowed scope: none.
- Result: **PASS. 0 BLOCKER, 0 HIGH, 0 MEDIUM, 0 LOW.** The third pass's MEDIUM is closed correctly;
  the TEST gate's own new suite is genuine and does not weaken any existing coverage.

---

# Third pass (delta a7e0be9..80d2ed5)

GATE: CODE_REVIEW
HANDOVER: HO-CAT-001
COMMITS: a7e0be9..80d2ed5 — fc48deb, a980126, a6ae5ca, 1825386, aeac91b, 76fb7d0, 0d57ad7 (merge of
master eae3d7b, confirmed clean, see below), 80d2ed5 (doc record). Reviewed as a combined delta on
top of the second pass (67f5865), which already covers everything before `a7e0be9`.
DATE: 2026-09-26
STATUS: PASS

## History
- **First pass** (7abc9e4): FAIL. 2 HIGH, 4 MEDIUM, 1 LOW carried.
- **Second pass** (67f5865): PASS. 1 MEDIUM, 4 LOW (new).
- **Third pass** (this section, 80d2ed5): PASS. 0 BLOCKER, 0 HIGH, 1 MEDIUM (new), 0 LOW (new).
  Both second-pass findings this gate could act on are closed; the two remaining second-pass LOWs
  not owned by this commit range (`PagingRules.cs` doc comment, `VaccinesController` duplication)
  were fixed incidentally by `a980126` alongside its own item, see below.

## What I reviewed
- Every one of the seven fix commits individually (`git show <sha>`), not just the combined diff —
  `fc48deb` (customer-must-exist), `a980126` (pageNumber cap + shared `PagingHelper`), `a6ae5ca`
  (medical-profile list cap), `1825386` (PDF port-level exception), `aeac91b` (restore
  `RecordedByStaffId.NotEmpty()`), `76fb7d0` (`Cache-Control: no-store`), `80d2ed5` (doc-only).
- `0d57ad7` (merge of `master`/`eae3d7b`, ho/integrator-host): diffed against both parents —
  `git diff 76fb7d0..0d57ad7` is byte-identical to `git diff 76fb7d0^..eae3d7b`'s own file set (15
  files, all outside HO-CAT-001's own surface: `StaffAccountsController.cs`,
  `StaffCustomersController.cs`, `AccountsModule.cs`, `AdministrationModule.cs`, `Program.cs`,
  `GetDuplicateCustomerGroupsQueryHandler.cs`, `BusinessViolationCodes.*`, four test files, two
  handover docs, one gate doc). No conflict markers, nothing dropped from either side, nothing this
  branch owns touched — a genuinely clean merge, not re-litigated on its own content (already PASSed
  by `INTEGRATOR-HOST-CODE_REVIEW.md`).
- `RegisterCatCommandHandler.cs`, `TransferCatOwnershipCommandHandler.cs`, and the
  `CustomerAccountAggregate`/`CustomerAccountStatus`/`EfAggregateRootRepository.GetByIdAsync` code
  the new lookup depends on, read in full (not just the diff) to answer the task's specific
  questions below.
- `PagingHelper.cs` in full, plus every call site (`CatsController.ApplyPaging` calls removed,
  `VaccinesController.GetVaccines`) to confirm the duplication it replaces is actually gone.
- `CatMedicalHistoryRenderException.cs`, `QuestPdfCatMedicalHistoryRenderer.Render`, and
  `CatsController.GetCatMedicalHistoryPdf`'s catch clause.
- `RecordCatWeightCommandValidator.cs` alongside `CatsController.RecordCatWeight` to confirm
  execution order (see finding-by-finding notes below).
- `CatIntegrationTestBase.SeedCustomerAsync`, and every call site of `RegisterCatAsync`/
  `SeedCustomerAsync` this delta touches, to see what the new customer-must-exist behaviour is and
  is not covered by.

## Build / unit tests (this pass)
`git archive 80d2ed5` into a new scratch folder (`cat1-cr3`), deleted afterward.
- `dotnet build CatEcommerce.sln` — **0 warnings, 0 errors**.
- `dotnet test tests/CatEcommerce.Domain.Tests` — **223/223 passed** (unchanged from the second
  pass — this delta touches no Domain-layer file).
- `dotnet test tests/CatEcommerce.Application.Tests` — **93/93 passed** (87 at second pass; +6, the
  medical-profile list-cap boundary tests from `a6ae5ca`).
- `dotnet test tests/CatEcommerce.Infrastructure.Tests` — **28/28 passed** (unchanged).
- Integration tests and the shared Postgres database were not touched, per the task's instructions.

## Second-pass findings: status now
- **[MEDIUM] `pageNumber` had no upper bound: CLOSED, with a real test.** `a980126`'s
  `PagingHelper.ApplyPaging` rejects `pageNumber > PagingRules.MaxPageNumber` with 400
  `InvalidPageNumber`, applied identically by both `CatsController` (three list endpoints) and
  `VaccinesController.GetVaccines`. `CatsPaginationEndpointTests.cs` adds a
  `pageNumber=2147483647 → 400` case for all three endpoints. This commit also incidentally closed
  two second-pass LOWs it wasn't asked to: `VaccinesController` no longer reaches into
  `CatsController.InvalidPageSizeValidationResponse` (both now call the one shared, `internal`
  `PagingHelper`), and `PagingRules.cs`'s doc comment now states both enforcement policies
  (reject-vs-clamp) explicitly.
- **[LOW 3] `CatsController` catching a QuestPDF type across the port: CLOSED, cleanly.** `1825386`
  moves the `catch (DocumentDrawingException)` into `QuestPdfCatMedicalHistoryRenderer.Render`
  itself, which re-throws the new port-level `CatMedicalHistoryRenderException`; the controller now
  only references that type. The new exception's `Message` is a fixed, generic string — the
  SECURITY gate's own concern (QuestPDF's message quotes the offending free text verbatim) is not
  reintroduced, and the controller's cat-id-only logging is unchanged.
- **[LOW 4] Stale `RecordCatWeightCommandValidator` comment / dropped `NotEmpty()`: CLOSED, and I
  independently confirmed the restored rule cannot reject a legitimate request.** The task asked
  specifically whether the validator runs before or after the controller sets the field —
  `CatsController.RecordCatWeight` builds the *entire* `RecordCatWeightCommand`, including
  `RecordedByStaffId = userContextProvider.GetUserContext().UserId`, before calling
  `dispatcher.SendAsync(command)`; `ValidateCommand = true` runs the validator against that
  already-fully-populated command inside the dispatch pipeline, never before. Combined with
  `[RequireAction(Actions.Cats.RecordClinical)]` (staff-only, not in `CustomerActions.Cat`) refusing
  an unauthenticated or non-staff caller with 401 before the action method itself runs, `UserId` is
  never `Guid.Empty` for any caller that reaches the validator — the restored `NotEmpty()` is inert
  today and can only ever catch a future regression in the controller's own assignment, exactly as
  the commit message claims. No behavioural risk.

## New finding, this pass
- **[MEDIUM] The new customer-must-exist check does not reject a merged-away or anonymised
  customer, only a non-existent one.**
  `src/Application/CatEcommerce.Application/CommandHandlers/Cat/RegisterCatCommandHandler.cs:34-37`
  and `TransferCatOwnershipCommandHandler.cs:35-38` both do
  `if (owner is null) return Violation(CustomerNotFound, ...)` and nothing else.
  `CustomerAccountAggregate` has three states (`CustomerAccountStatus.Active`/`Merged`/
  `Anonymised` — a proper `Enumeration`, not a bug in itself), and
  `EfAggregateRootRepository.GetByIdAsync` (`.Where(a => a.Id == id && !a.IsMarkedToDelete)`) only
  excludes a *hard*-deleted row, not a customer whose `Status` has moved to `Merged` or
  `Anonymised` — those rows are ordinary, present rows with a different `Status` value. A staff
  caller can therefore still name a customer id that has been merged away (`MergeCustomerCommand`
  sets `Status = Merged`, `MergedIntoCustomerId` on the duplicate, but does not mark it deleted) or
  anonymised as `RegisterCatCommand.OwnerId`/`TransferCatOwnershipCommand.NewOwnerId`, and the
  lookup passes — a cat gets registered to, or transferred to, a customer record that the account
  side no longer treats as a live customer. This is exactly the "orphaning" failure mode the fix's
  own commit message says it closes ("instead of silently orphaning a cat"), just reachable through
  the account lifecycle rather than through a typo'd or fabricated id: after `MergeCustomerCommand`,
  any lookup keyed off the merged-away id represents a record the merge was specifically meant to
  retire, and nothing here stops it from acquiring a brand-new cat afterward — the opposite of what
  a merge is supposed to achieve (consolidating one customer's cats under the survivor, not letting
  the retired id keep accumulating new ones). The doc comment on both handlers ("must resolve to a
  real, non-deleted `CustomerAccountAggregate`") is accurate as written but reads as if it also
  covers this case, since a merged/anonymised row is not what the comment's own English would call
  "real" in context. Not Critical — no security bypass, no crash, and the underlying
  `CustomerAccountAggregate` invariants are still enforced correctly wherever they are otherwise
  entered (this is a gap in a defence-in-depth foreign-key-style check, not a broken domain
  invariant) — but it is a genuine, currently-unclosed half of the finding this commit set out to
  close. Fix: check `owner.Status == CustomerAccountStatus.Active` (or explicitly exclude
  `Merged`/`Anonymised`) in both handlers, the same way `MergeCustomerCommandHandler`'s own domain
  call already refuses a re-merge of an already-merged record.

## Task-specific checks, answered directly
- **Can a customer calling Register still only register to themselves?** Yes, unaffected by this
  delta. `CatsController.RegisterCat:44-45` forces `ownerId = caller.AccountId` whenever
  `caller.Kind == ResolvedCallerKind.Customer`; `request.OwnerId` (and hence the new existence
  check) is only reachable with a staff-supplied value. Confirmed by reading the controller, not
  just the handler.
- **Does the lookup distinguish a deleted/merged customer?** No — see the new MEDIUM above. It
  distinguishes only a hard-deleted (`IsMarkedToDelete`) row from an existing one; `Merged` and
  `Anonymised` are both "existing" as far as this check is concerned.
- **Timing/enumeration concerns for staff-only callers?** None found. Every caller that can reach
  either handler's new lookup is already a staff account authorized for `Actions.Cats.Register` or
  `Actions.Cats.TransferOwnership` (`[RequireAction]`, staff-only per HO-ADM-001 §12.3), and staff
  already has full, direct customer-directory visibility via `StaffCustomersController` — a 400
  `CustomerNotFound` for a guessed id discloses nothing a staff caller could not already query
  directly and legitimately. No unauthenticated or customer-role path reaches this check.
- **`PagingHelper`'s placement — a static helper in `Controllers`, or should it be a filter?** The
  static-helper placement is fine and not a finding. A filter would need to be generic over
  `Query<TResult>`'s concrete type per action and would still need each controller to read
  `pageNumber`/`pageSize` from its own query-string parameters and pick which `Query<T>` to mutate;
  the current shape (`ApplyPaging<TResult>(Query<TResult> query, ...)` called explicitly, returning
  `IActionResult?` for the caller to short-circuit on) is the simplest form that reuses the existing
  `Query<T>`/`ValidationResponse`/`BadRequestObjectResult` shapes without inventing a new
  cross-cutting mechanism for two controllers. One residual, non-blocking observation: the admin
  paging caps (`AdminAuditEntriesController`, `AdminLeaveRequestsController`) still each implement
  their own copy of the identical `pageNumber` cap rather than also calling `PagingHelper` — a
  reasonable scope boundary for this fix (this delta is Cat/Vaccine-owned, admin controllers are
  not), but worth folding in if `PagingHelper` is ever generalised beyond Cat/Vaccine.
- **Every pre-existing test still meaningful?** Yes. The three call sites `fc48deb` touched
  (`CatIntegrationTestBase.RegisterCatAsync`, `CatProfileProjectionEndpointTests`,
  `CatsPaginationEndpointTests`) were changed only to seed a real customer instead of passing a bare
  `Guid.NewGuid()` that the new check would now reject with 400 before the test's own assertions
  ever ran — none had an assertion weakened or removed. `1294ffe`/`ac79f82` (already reviewed at the
  second pass) are outside this delta's own commit range and unchanged since.
- **Missing tests the TEST gate should add.** No test in this delta (or anywhere in
  `tests/CatEcommerce.IntegrationTests`) exercises the new behaviour's own failure path: there is no
  `RegisterCat_UnknownOwnerId_Returns400CustomerNotFound`,
  `TransferCatOwnership_UnknownNewOwnerId_Returns400CustomerNotFound`, or a
  merged-away/anonymised-customer case for either command (`grep CustomerNotFound` under `tests/`
  finds nothing in the Cat integration suite). `CatIntegrationTestBase.SeedCustomerAsync` exists and
  is exactly the seam a test would need — it is only ever used to make the *happy* path pass, never
  to prove the 400. This is the one concrete gap this pass surfaces for TEST: add positive coverage
  for the check this delta added, plus a case for the new MEDIUM above once/if it is fixed.

## Dead code / duplication
None of the superseded-architecture shapes appear anywhere in this delta. No new
`CommandService`/bare-repository-wrapper — both new lookups
(`RegisterCatCommandHandler`/`TransferCatOwnershipCommandHandler`) are handler-level
`IAggregateRootRepository<CustomerAccountAggregate>.GetByIdAsync` calls, the same shape
`RecordCatVaccinationCommandHandler` already uses for the vet id (correctly cited by the commit
message itself). The paging-validation duplication flagged at the second pass (LOW,
`VaccinesController` reaching into `CatsController`) is gone, not just moved.

## SCOPE CHECK
- files in allowed scope: 15/15 non-doc files across the seven fix commits
  (`RegisterCatCommandHandler.cs`, `TransferCatOwnershipCommandHandler.cs`, `CatsController.cs`,
  `PagingHelper.cs` (new), `VaccinesController.cs`, `BusinessViolationCodes.Cat.cs`,
  `PagingRules.cs`, `UpdateCatMedicalProfileCommandValidator.cs`, `CatMedicalProfileRules.cs` (new),
  `CatMedicalHistoryRenderException.cs` (new), `QuestPdfCatMedicalHistoryRenderer.cs`,
  `RecordCatWeightCommandValidator.cs`, plus three test files) — all within HO-CAT-001's own §13
  surface, all gate-fix-scoped (each commit fixes exactly the second-pass item its message names).
  `0d57ad7`'s 15 merged-in files are outside HO-CAT-001's scope but already PASSed by
  `INTEGRATOR-HOST-CODE_REVIEW.md`, confirmed clean above, not re-reviewed on their own content.
  `80d2ed5` is doc-only (`docs/handover/HO-CAT-001.md`).
- files outside allowed scope: none.
- Result: **PASS. 0 BLOCKER, 0 HIGH, 1 MEDIUM (new, the merged/anonymised-customer gap), 0 LOW
  (new).** All four second-pass findings this gate owned are closed with real fixes and, where
  claimed, real tests; the merge is confirmed clean. The one new MEDIUM does not block — it is a
  gap in a defence-in-depth check, not a broken invariant, no crash, and no security bypass — but it
  should be fixed before the customer-merge story is fully exercised in production, and TEST should
  add the missing 400-path coverage noted above regardless of whether the MEDIUM is fixed first.

---

GATE: CODE_REVIEW
HANDOVER: HO-CAT-001
COMMITS: master(5200d75)...ho/CAT-001-gates(67f5865) — 177232d 8519b06 ccc3aa2 103fa7b 832bae4 5e26340 d4eea55 00967ce cb2ddd2 c529f8f ac79f82 1294ffe af20041 67f5865
DATE: 2026-09-26
STATUS: PASS

## History
- **First pass** (7abc9e4, below): FAIL. 0 BLOCKER, 2 HIGH, 4 MEDIUM, 1 LOW carried.
- **Second pass** (this section, 67f5865): PASS. 0 BLOCKER, 0 HIGH, 1 MEDIUM, 4 LOW.

## What I reviewed
- `git diff master...ho/CAT-001-gates` (76 files, the branch's own contribution — `master` is
  5200d75, HO-ADM-001's `[RequireAction]`/`OwnerScope`/ADR-043 authorization, taken as reviewed
  code and not re-litigated).
- Every earlier gate-fix commit on this branch since `177232d`, line by line: `8519b06` (stale
  comment), `ccc3aa2` (Q1/Q5 projection rewrite), `103fa7b` (`Reason` MaximumLength), `832bae4`
  (`TimeProvider` seam), `5e26340` (paging cap/total, identity trust, standard 400 shape, safe PDF
  logging), `d4eea55` (Bengali font), `00967ce` (worst-status-wins test), `cb2ddd2` (web follow-up,
  read only — TypeScript is HO-CAT-002's own gates' job), `c529f8f` (doc record).
- The merge conflict resolutions at `ac79f82` (`CatsController.cs`, `CatIntegrationTestBase.cs`)
  against both parents (`c529f8f` and `5200d75`), and `1294ffe`'s edits to the three ADM-001-owned
  test files (`CatVaccineAuthorizationSecurityTests.cs`, `AdminGlobalSettingsLiveEffectEndpointTests.cs`,
  `AdministrationIntegrationTestBase.cs`).
- `af20041`'s migration (`WidenAuditEntryValues`) against `AuditEntryConfiguration.cs` and the
  model snapshot.
- `docs/handover/HO-CAT-001.md` §1's two new 2026-09-26 Amended rows in full.
- My own first-pass file (below) and `docs/gates/HO-CAT-001-ARCHITECTURE.md`'s second pass, which
  reviewed the identical commit range and reached PASS with 1 MEDIUM/4 LOW — I cross-checked my
  own findings against its "New code this pass" and "Merge resolution" sections rather than
  duplicate its `git merge-tree` / `has-pending-model-changes` checks.
- Owner decisions taken as given, not re-judged: `pageSize` clamp at 100 + `PagedResult<T>` with a
  total; C10 `{treatmentId, nextDueOn}`; audit records personal data in full; the photo
  `StorageKey` trust issue carried to HO-FIL-001.

## Build / unit tests (this pass)
`git archive 67f5865` into a new scratch folder (no git-ignored `appsettings`, only the checked-in
`.example`).
- `dotnet build CatEcommerce.sln` — **0 warnings, 0 errors**.
- `dotnet test tests/CatEcommerce.Domain.Tests` — **223/223 passed** (161 at first pass; +62 from
  HO-ADM-001's own domain tests now merged in, plus this pass's own U26 broadening and the two new
  vaccination-status calculator tests).
- `dotnet test tests/CatEcommerce.Application.Tests` — **87/87 passed** (66 at first pass; +21,
  including the eight new `Reason`-boundary validator tests).
- `dotnet test tests/CatEcommerce.Infrastructure.Tests` — **28/28 passed** (not run at first pass;
  included this time as a cheap sanity check, no Cat-specific content).
- Integration tests and the shared Postgres database were not touched, per the task's instructions
  — no in-memory/SQLite provider or Testcontainers appear anywhere in the delta either way.
- Scratch folder deleted afterward, as instructed.

## First-pass findings: status now
- **[HIGH] `Reason` had no `MaximumLength`, so an over-long value committed the cat change and
  then failed the audit write with an unhandled 500: CLOSED, with real tests.**
  `FlagCatHighRiskCommandValidator`/`ClearCatHighRiskFlagCommandValidator`/
  `TransferCatOwnershipCommandValidator` all now have `.MaximumLength(1000)` on `Reason`, matching
  `AuditEntryConfiguration`'s column limit. `CommandValidatorsTests.cs` adds six new boundary
  tests — `ReasonAtMaximum_Succeeds` (1000 chars) and `ReasonOverMaximum_FailsOnReason` (1001
  chars) for all three commands — genuinely exercising the boundary, not just asserting the rule
  exists. Independently, the integrator's `af20041` migration widens
  `AuditEntryConfiguration.BeforeValue`/`AfterValue` from `varchar(2000)` to `text`, closing the
  second half of the same failure mode (the SECURITY gate's MEDIUM on the same commit trail) —
  `has-pending-model-changes` is clean and the model snapshot matches the new column type.
- **[HIGH] The base handover's own §16 integration-test suite (I1–I22) was never written: NOT
  CLOSED, correctly deferred to the TEST gate, does not block this gate.** `docs/test-cases/
  TC-HO-CAT-001.md` still does not exist. This pass added three targeted regression suites —
  `CatProfileProjectionEndpointTests.cs` (proves the Q1/Q5 rewrite is byte-identical to the old
  rehydrated output, field by field, including nested owned collections), `CatsPaginationEndpointTests.cs`
  (400 on `pageSize<=0`, clamp-to-100, `totalCount`/`items` shape for all three list endpoints) and
  the C10 amendment's own `ParasiteTreatmentEndpointTests.cs` — each a real regression test for
  *this pass's own* fix, not a rewrite of the original finding's scope. The original finding was
  explicit that closing it is TEST's job ("should block ACCEPTANCE until TEST writes and runs this
  suite"), and nothing in this pass's own remit (fixing ARCHITECTURE/SECURITY/CODE_REVIEW findings)
  was to write I1–I22. Carried forward, unchanged in substance, to the TEST gate.
- **[MEDIUM] `PageSize` uncapped, no total count: CLOSED, with real tests.** `GetCatsQuery`/
  `GetCatWeightHistoryQuery`/`GetVaccinesQuery` all now return `PagedResult<T>` (`{items,
  pageNumber, pageSize, totalCount}`), reusing the Account side's existing type rather than adding
  a second one. `CatsController.ApplyPaging`/`VaccinesController.GetVaccines` clamp `pageSize` to
  `PagingRules.MaxPageSize` (100) and return a 400 (`BusinessViolationCodes.InvalidPageSize`) for
  `pageSize<=0`. `CatsPaginationEndpointTests.cs` proves all three behaviours over real HTTP for
  all three endpoints (8 tests): zero/negative → 400, `pageSize=100000` → clamped to 100 in the
  response body, and `totalCount`/`items.Length` both correct against a known seeded count. See
  new MEDIUM below for the one half of `PagingRules` this pass did not reuse (`MaxPageNumber`).
- **[MEDIUM] Wall-clock reads instead of an injected `TimeProvider`: CLOSED, cleanly.**
  `CatAggregate.Today()` is deleted; `ValidateDateOfBirth` and every future-date check
  (`RecordNeuterStatus`, `RecordVaccination`, `RecordParasiteTreatment`, `MarkDeceased`) now take
  `today` as a parameter on the Domain DTO. `CatWeightRecordAggregate.Record` and
  `CatMapper.ToDomainDto(AddCatPhotoCommand, ...)` do the same for `uploadedOn`. Every one of the
  seven command handlers that needs "now" (`RegisterCat`, `UpdateCatProfile`,
  `RecordCatNeuterStatus`, `RecordCatWeight`, `RecordCatVaccination`, `RecordCatParasiteTreatment`,
  `MarkCatDeceased`, `AddCatPhoto`) now injects `TimeProvider` and reads it once per call — the
  same seam `MergeCustomerCommandHandler` already uses. This is a real seam, not cosmetic: the
  aggregate's own invariant methods take the value as data, so a test can pin an exact boundary
  (a future gate no longer has to note the "midnight-rollover flake" risk the first pass called
  out). No test yet pins the exact boundary this way for Cat itself, but that was not a first-pass
  requirement — the finding was about the seam's existence, which now exists.
- **[MEDIUM] "Worst status wins" untested with more than one core vaccine: CLOSED, with a real
  test.** Two new tests in `CatVaccinationStatusCalculatorTests.cs` —
  `Calculate_OneCoreOverdueAndOneCoreCurrent_OverallStatusIsOverdue` and
  `Calculate_OneCoreDueSoonAndOneCoreCurrent_OverallStatusIsDueSoon` — each construct two
  `ActiveCoreVaccine`/`CatVaccinationRecord` pairs at different statuses and assert
  `OverallStatus` picks the worse one, exactly the gap the first pass named.
- **[LOW] U26 narrower than its own name: CLOSED, with a genuinely parametrised test.**
  `EveryMutationMethod_RaisesExactlyOneEventAndIncrementsVersion` now calls all eleven §8.2
  mutation methods in a precondition-respecting sequence (`Register` → `UpdateProfile` →
  `RecordNeuterStatus` → `UpdateMedicalProfile` → `AddPhoto` → `FlagHighRisk` →
  `ClearHighRiskFlag` → `RecordVaccination` → `RecordParasiteTreatment` → `TransferOwnership` →
  `MarkDeceased` last, since `EnsureAlive` forecloses everything after it), asserting the event
  count and version delta independently after each call via a shared
  `AssertRaisesOneEventAndIncrementsVersion` helper — a method that stops raising its event now
  fails on its own line, not just the running total.
- **[LOW, carried, not new] QuestPDF §20.6 licensing: CLOSED by owner decision.** The Community
  licence is confirmed (2026-09-26); the "not resolved here" comments in
  `QuestPdfCatMedicalHistoryRenderer.cs` and `CatProfilesModule.cs` are updated to say so.

## New findings, this pass
- **[MEDIUM] `pageNumber` has no upper bound, unlike `pageSize`.**
  `src/Api/CatEcommerce.Api/Controllers/CatsController.cs` (`ApplyPaging`) and
  `VaccinesController.cs:56-58` both only check `pageNumber is > 0` before assigning it straight to
  `query.PageNumber`. `PagingRules.MaxPageNumber` (1,000,000) exists — it is already applied by
  every other paged admin controller (`AdminAuditEntriesController`, `AdminLeaveRequestsController`)
  specifically because `Query<T>.Skip` is `(PageNumber-1)*PageSize`, which overflows `int` for a
  large `pageNumber` and produces a negative `OFFSET` Postgres rejects. This branch reuses
  `PagingRules.MaxPageSize` for the fix it was asked to make, but not the sibling
  `PagingRules.MaxPageNumber` this same class already defines for exactly this failure mode. With
  `pageSize` now clamped to 100, `pageNumber` above roughly 21.5 million still overflows `Skip` to
  a negative value. I did not reproduce this against a live database (out of this pass's scope,
  same as `ARCHITECTURE`'s MEDIUM 1 on the identical line), so the concrete failure mode (500 vs.
  a caught exception) is a runtime claim for SECURITY/TEST, not verified here — but the gap itself
  is visible in the source: nothing at all bounds `pageNumber` on these two controllers. Fix: cap
  `pageNumber` at `PagingRules.MaxPageNumber` in both controllers' paging helpers, the same way
  `pageSize` is now capped.
- **[LOW] `RecordCatWeightCommandValidator.cs:13-18` — a stale comment as of this same merge.**
  The comment explaining why `RecordedByStaffId.NotEmpty()` was dropped says `UserId` is
  `Guid.Empty` "until HO-ADM-001's `[RequireAction]` lands on this controller (out of this fix's
  scope)". `[RequireAction(Actions.Cats.RecordClinical)]` landed on this same action at the
  `ac79f82` merge, two commits later in this same branch — the precondition the comment describes
  no longer holds, and the guard could now be restored as defence in depth (a caller that reaches
  the validator is authenticated, so `NotEmpty()` would never legitimately fire, but it would catch
  a future regression in the controller's own assignment). Not a behavioural bug — `CatsController`
  still always sets the field from `UserContext.UserId` — just a comment that now describes a
  precondition the same branch closed. (Also flagged independently by
  `HO-CAT-001-ARCHITECTURE.md`'s second pass, LOW 4, on the same line.)
- **[LOW] `VaccinesController.cs:56-65` duplicates `CatsController`'s paging validation instead of
  sharing it.** `GetVaccines` re-implements `ApplyPaging`'s body inline (`pageNumber is > 0`,
  `pageSize <= 0` → 400, `Math.Min(pageSize, PagingRules.MaxPageSize)`) and calls
  `CatsController.InvalidPageSizeValidationResponse()` directly — one controller reaching into
  another's static members. It works today (both controllers agree on the shape), but the natural
  owner for shared paging logic is `PagingRules` or a small shared helper, not a
  controller-to-controller dependency; a future change to one copy risks silently diverging from
  the other. Fix: move `ApplyPaging`/`InvalidPageSizeValidationResponse` to a shared static class
  (e.g. `PagingRules` itself, or a small `PagingHelper` in `Api`) both controllers call.
- **[LOW] `CatsController.cs:429` catches a QuestPDF type across the document-renderer port.**
  `GetCatMedicalHistoryPdf` catches `QuestPDF.Drawing.Exceptions.DocumentDrawingException` — the
  Api layer now has to know which PDF library sits behind `ICatMedicalHistoryPdfRenderer`. The
  logging decision itself is sound (cat id only, never the exception or its message, which would
  otherwise quote the offending free text per the SECURITY gate's LOW). The cleaner fix is for
  `QuestPdfCatMedicalHistoryRenderer` to catch its own library's exception and translate it into a
  port-level failure (e.g. a bool return or a renderer-owned exception type), so the controller
  depends only on the port's own contract. Not blocking — the port is otherwise intact and this is
  a one-file, one-catch-clause change whenever the renderer is next touched.
- **[LOW] `PagingRules.cs`'s doc comment no longer describes both of its callers accurately.** It
  says `MaxPageSize` is "applied by rejecting with 400" for every paged admin endpoint. The Cat and
  Vaccine endpoints apply it by clamping instead (owner decision, 2026-09-26) — a legitimate,
  different policy, but the shared constant's own comment reads as if there is one policy. Fix:
  note that `MaxPageSize` has two call sites with two different enforcement policies (reject vs.
  clamp), so a reader of the constant does not assume the wrong one for a new caller.

## Dead code / duplication
No new instance of the forbidden legacy shapes anywhere in the delta: `IDispatcher`,
`IEventPublisher`, `IBusMessageDispatcher`, `AddServiceBusProvider`, a broker type, Mongo/Bson,
`ViewModelBase`, `IReadRepository`, a `CommandServices` class, a rejection event, or a `202`. No
`CommandService` wrapping a bare repository call — the two new staff-account lookups in
`RecordCatVaccinationCommandHandler`/`RecordCatParasiteTreatmentCommandHandler` are handler-level
`IAggregateRootRepository<StaffAccountAggregate>.GetByIdAsync` calls, the same shape as the
existing `VaccineAggregate` lookup, not a service. The `Violation(...)` per-handler helper
duplication already accepted at first pass is unchanged.

## Confirmed: merge and post-merge test edits did not weaken assertions
- `ac79f82` (`CatsController.cs`): kept `master`'s `[RequireAction]`/`OwnerScope`/`CurrentCaller()`
  attributes exactly, attribute-for-attribute (cross-checked against `git show master:`), alongside
  this branch's own `ApplyPaging`/`InvalidPageSizeValidationResponse` additions. No action lost its
  guard.
- `ac79f82` (`CatIntegrationTestBase.cs`): purely additive — `RegisterCatAsync` kept its new `name`
  parameter and gained master's staff sign-in flow; `SeedStaffAccountAsync` (this branch) and
  `SignInAsFreshAdministratorAsync` (master) both kept, used independently elsewhere.
- `1294ffe` (`CatVaccineAuthorizationSecurityTests.cs`): the only change is
  `listBody.EnumerateArray()` → `listBody.GetProperty("items").EnumerateArray()` in two places, to
  follow the same test's own `Assert.Contains`/`Assert.DoesNotContain` calls through to the new
  `PagedResult<T>` envelope — the assertions themselves are untouched and still check the same
  thing (the transferred-away cat is/isn't in the caller's own list).
- `1294ffe` (`AdminGlobalSettingsLiveEffectEndpointTests.cs`): `SeedStaffAsync(StaffRoles.Administrator)`
  → `SeedStaffAsync(StaffRoles.Administrator, StaffRoles.Vet)`, because the same admin is now also
  used as the administering vet and this pass's new `StaffAccountNotVeterinarian` check requires
  the `Vet` role — an addition, not a relaxation, of what the test sets up.
- `1294ffe` (`AdministrationIntegrationTestBase.cs`): adds the now-required `Today` argument to a
  `CatRegistrationDto` construction; no assertion changed.

## SCOPE CHECK
- files in allowed scope: 76/76 (the same set `HO-CAT-001-ARCHITECTURE.md`'s second pass counted:
  63 under HO-CAT-001 §13 globs, 2 `docs/handover/*.md` Amended rows, 11 `web/**` inside HO-CAT-002
  §13 for owner decision (c)). I reviewed all of the backend 65 (63 + 2 doc rows); I read but did
  not independently re-review the 11 `web/**` files — HO-CAT-002's own gates own that surface, and
  the handover's Amended row records `npx vitest`/`tsc`/`eslint` all green.
  - The three ADM-001-owned test files and the `Application.Tests.csproj`/`Infrastructure.csproj`
    edits are in scope only on the strength of the Amended rows (post-merge compatibility fixes,
    and the pre-existing `ProjectReference`/font-embedding items respectively) — same as
    `ARCHITECTURE`'s scope note.
- files outside allowed scope: none.
- Result: **PASS. 0 BLOCKER, 0 HIGH, 1 MEDIUM (new), 4 LOW (new) + 1 HIGH carried to TEST (the
  §16 suite, correctly out of this gate's remit) + 1 LOW carried-closed (QuestPDF, owner decision).**
  Both first-pass HIGHs this gate owned resolution of are closed with real, non-tautological tests
  (boundary tests at exactly the validator's new maximum, an HTTP-level regression suite proving
  the projection rewrite is output-identical, and an HTTP-level paging suite proving clamp/400/total
  behaviour). All four first-pass MEDIUMs are closed. The one first-pass HIGH not closed here (the
  full §16 integration suite) was correctly out of this pass's remit and is carried to TEST, which
  has not yet run.

---

# First pass (2026-09-26, reviewed at 7abc9e4) — retained unchanged below

GATE: CODE_REVIEW
HANDOVER: HO-CAT-001
COMMITS: `4007225` (2026-09-21, base slice, never gated) through `110913c` (2026-09-25, C10
amendment merge, gated separately in `HO-CAT-001-C10-*.md`) as they stand on `master` at `7abc9e4`.
Reviewed the full Cat Profiles write side, read side, Infrastructure, Api and both unit-test
projects; did not re-litigate C10, which already has its own PASS record.
DATE: 2026-09-26
STATUS: FAIL

This is the first CODE_REVIEW pass for this handover — it landed on master with no gates at all
(§19 was skipped entirely for the base slice; only the later C10 amendment got its own
ARCHITECTURE/CODE_REVIEW/TEST triad). I read `CatAggregate`, `CatWeightRecordAggregate`,
`VaccineAggregate`, every command handler, validator, mapper, domain event handler, EF
configuration, the repository, all seven query handlers, the PDF/QR renderers, the controllers, and
both `CatEcommerce.Domain.Tests`/`CatEcommerce.Application.Tests` test suites line by line. I ran
`dotnet build` (0 warnings/0 errors) and both unit test projects (Domain 161/161, Application
66/66, all green) per the task's instructions; I did not touch the shared Postgres database or run
the integration suite.

## Domain model vs §8

`CatAggregate.cs`, `CatWeightRecordAggregate.cs`, `VaccineAggregate.cs` all match §8.2/§8.4 exactly:
private setters, `IReadOnlyList<T>` over a private backing `List<T>`, every mutation method ends
`AddDomainEvent` then `UpdateVersion()`. `EnsureAlive()` is called everywhere §8.2's table requires
it (`UpdateProfile`, `RecordNeuterStatus`, `UpdateMedicalProfile`, `AddPhoto`, `RecordVaccination`,
`RecordParasiteTreatment`, `TransferOwnership`) and correctly *not* called where the table doesn't
list it (`FlagHighRisk`, `ClearHighRiskFlag`, `MarkDeceased` — which instead checks
`CatAlreadyDeceased` itself). Neuter/vaccination/deceased date rules (not future, not before DOB)
are all present and correct. The vaccination batch-expiry boundary (`BatchExpiresOn ==
AdministeredOn` succeeds, `<` throws `ExpiredVaccineBatch`) and the duplicate-same-day check are
both correct and covered (U12/U13/U15/U16). `TransferOwnership` leaves every owned collection
untouched (U24 proves it explicitly). The high-risk flag and ownership-transfer audit trail
(`AuditCatHighRiskFlaggedHandler`/`AuditCatHighRiskFlagClearedHandler`/
`AuditCatOwnershipTransferredHandler`) was correctly amended by HO-ACC-001 (19)(b) to add
`TimeProvider`/`Category`/`IpAddress` — no issue there.

**Vaccination status (US-CAT-07, Q4).** `CatVaccinationStatusCalculator.Calculate` is a pure
function taking `asOf` explicitly (`GetCatVaccinationStatusQueryHandler.cs:45` passes
`DateOnly.FromDateTime(DateTime.UtcNow)`), so the boundary logic is genuinely unit-testable:
`nextDueOn < asOf` → `Overdue`, `nextDueOn <= asOf.AddDays(window)` → `DueSoon`, else `Current` —
matches §12.3 exactly, and U34–U38 (`CatVaccinationStatusCalculatorTests.cs`) prove all four cases
plus the `today + window` boundary correctly. Weight history is correctly kept out of
`CatAggregate` (ADR-023) and resolved read-side as "latest row", never denormalised.

## Findings

- **[HIGH]** `src/Application/CatEcommerce.Application/CommandHandlers/Validators/FlagCatHighRiskCommandValidator.cs:12`,
  `ClearCatHighRiskFlagCommandValidator.cs:11`, `TransferCatOwnershipCommandValidator.cs:12` —
  `Reason` is validated with `NotEmpty()` only, no `MaximumLength`. `Reason` is not persisted on
  `CatAggregate` at all; it only travels into `AuditEntryDto.Reason`
  (`src/Domain/CatEcommerce.Domain/Models/AuditEntryDto.cs:17`), which
  `AuditEntryConfiguration.cs:29` maps to `HasMaxLength(1000)` — a Postgres `varchar(1000)`. A
  caller submitting a `Reason` over 1000 characters passes validation, the command mutates and
  commits `CatAggregate` (`FlagCatHighRiskCommandHandler`/`ClearCatHighRiskFlagCommandHandler`/
  `TransferCatOwnershipCommandHandler` all call `catRepository.UpdateAsync` successfully), and only
  then does the domain event handler's own `auditRepository.SaveAsync` →
  `db.SaveChangesAsync()` throw an uncaught `DbUpdateException` (string too long for the column),
  which is not a `DbUpdateConcurrencyException` so `ConcurrencyExceptionFilter` does not catch it —
  it propagates as an unhandled 500. §11.7's "a failure here fails the command after the cat has
  already been committed" is a *deliberate* trade-off for genuine audit-write failures (SRS C-9),
  but this specific trigger is entirely avoidable: the client gets a 500 for what looks like a
  failed request, while the cat's high-risk flag or ownership transfer has in fact already taken
  effect — a real state/response inconsistency, not a display nicety. Compare
  `FlagCatHighRiskCommandValidator.cs:11`, which does cap `HandlingNote` at 500 to match
  `CatAggregate.HandlingNote`'s own column limit, and `MergeCustomerCommandValidator.cs` (Account
  side), which correctly caps its own audit-bound `Reason` at 1000. Fix: add
  `.MaximumLength(1000)` to `Reason` on all three Cat validators.

- **[HIGH] The base handover's own §16 integration-test suite (I1–I22) was never written.**
  `docs/test-cases/TC-HO-CAT-001.md` does not exist — only `TC-HO-CAT-001-C10.md` (the later
  amendment's own short, explicitly-scoped sheet) and `TC-HO-CAT-002.md`. Under
  `tests/CatEcommerce.IntegrationTests/`, the only Cat-specific endpoint test file is
  `Endpoints/ParasiteTreatmentEndpointTests.cs` (3 cases, all added by the C10 amendment,
  2026-09-25). `CatIntegrationTestBase.cs` and `CatApiFactory.cs` exist and are ready (they carry a
  `RegisterCatAsync` helper following §16.1's per-test-owner rule correctly), but nothing besides
  the C10 tests calls them. There is no test exercising: registration end-to-end (I2–I5), the
  vaccine catalogue uniqueness constraint (I6), **concurrent-update 409 via `xmin`** (I7), the
  weight series round-trip (I8), an expired-batch rejection over real HTTP (I9), a real vaccination
  recording (I10), the audit row actually landing in `audit.audit_entry` for flag/clear/transfer
  (I12–I14), owner-scoped list exclusion after transfer or death (I15–I17), the deceased
  read-only guard over HTTP (I18), **pagination** (I19), the medical-history PDF (I20), the cage-card
  QR (I21), or the migration producing every index from an empty database (I22). The C10 gate's own
  sheet says this explicitly: "not a full re-audit of HO-CAT-001's other ten stories, which have no
  gate artifacts of their own yet and are out of this gate's scope." The Domain/Application unit
  suites (U1–U41) are genuinely solid and cover the business rules well — but §18's acceptance
  table cites an I-row as evidence for the large majority of its criteria (A1, A5, A6, A9–A25), and
  none of that evidence exists yet. This is not a defect in the shipped code, but it means most of
  §18 is currently unverifiable, and it should block ACCEPTANCE until TEST writes and runs this
  suite.

- **[MEDIUM] `PageSize` is not capped anywhere, and none of the Cat list endpoints return a total
  count.** `CatsController.ApplyPaging` (`CatsController.cs:408-412`) and the equivalent in
  `VaccinesController.cs:54` only check `pageSize is > 0` before assigning it straight to
  `query.PageSize`. `Platform.Infrastructure.Core.Queries.Query<TResult>.PageSize` (decompiled,
  1.0.34) is a plain auto-property with no upper bound — `Take => PageSize` is used directly in
  `GetCatsQueryHandler.cs:37`, `GetCatWeightHistoryQueryHandler.cs:24` and
  `GetVaccinesQueryHandler.cs:29`. Concretely: `GET /api/cats?pageSize=1000` (or `pageSize=2000000`)
  is accepted as-is and executes `.Skip(...).Take(1000)` (or 2,000,000) against Postgres with no
  server-side ceiling — CAT-002's assumption that `pageSize=1000` "works" is correct, literally,
  because nothing rejects or clamps it. Separately, none of `GetCatsQuery`/
  `GetCatWeightHistoryQuery`/`GetVaccinesQuery` return a total count or `PagedResult<T>` (contrast
  `GetStaffAccountsQueryHandler`/`GetDuplicateCustomerGroupsQueryHandler` on the Account side, which
  do) — a client has no signal for how many pages exist beyond the page it received, which is
  itself a pressure toward requesting one very large page instead of paging properly. §16 I19 asks
  for "paging headers correct", but no header or total count is ever produced for these three
  endpoints; that row cannot currently be evidenced as written even once the test exists. Fix: clamp
  `pageSize` to a maximum (e.g. 100) in each controller's paging helper, and consider surfacing a
  total count for at least `GetCatsQuery` (the one with a genuinely unbounded owner+catalogue
  population).

- **[MEDIUM] Wall-clock reads instead of an injected clock, inconsistent with the rest of the
  current codebase.** `CatAggregate.cs:489` (`Today() => DateOnly.FromDateTime(DateTime.UtcNow)`,
  used at lines 116, 274, 322, 354, 482), `CatWeightRecordAggregate.cs:29`, and
  `CatMapper.cs:35` (`UploadedOn = DateTimeOffset.UtcNow`) all read the system clock directly rather
  than through `TimeProvider`. Every `TimeProvider`-consuming type elsewhere in the current
  solution — every Account handler, and (since HO-ACC-001 §8.11) even this same slice's own three
  audit domain-event handlers — takes the clock as a seam. This wasn't a documented rule at the
  time this slice was written (`.ai/command-handler-pattern.md`'s own worked examples still inline
  `DateOnly.FromDateTime(DateTime.UtcNow)` in validator lambdas), so it isn't a `.ai/` violation,
  but it is now the one inconsistency between "the pattern this codebase actually uses" and "what
  this slice does": contrast with `CatVaccinationStatusCalculator.Calculate`, in the same handover,
  which correctly takes `asOf` as a parameter specifically so it can be pinned in a test. None of
  U1–U26's boundary tests can pin "today" — they all recompute `Today` locally
  (`CatAggregateTests.cs:17`) and rely on it matching the aggregate's own separate `UtcNow` read
  within the same test run, which is fine in practice but is a real (if rare) source of a
  midnight-rollover flake, and forecloses ever testing "exactly at the boundary" the way U38 does
  for the read side. Not blocking; worth aligning if this file is touched again.

- **[MEDIUM] The "worst status wins" aggregation (§12.3 step 4) has no test with more than one
  core vaccine.** `CatVaccinationStatusCalculator.cs:47-53` computes `overallStatus` as the worst
  across *all* `items`, which is the one piece of Q4's logic that actually needs more than a single
  vaccine to exercise meaningfully. All five calculator tests
  (`CatVaccinationStatusCalculatorTests.cs`, U34–U38) construct exactly one
  `ActiveCoreVaccine`/`CatVaccinationRecord` pair — the per-vaccine status mapping is well covered,
  but "one core vaccine is Overdue while another is Current → overall must be Overdue" is never
  asserted. Fix: add one test with two vaccines at different statuses and assert `OverallStatus`
  picks the worse one.

- **[LOW] `CatAggregateTests.cs:387-397` (`EveryMutationMethod_RaisesExactlyOneEventAndIncrementsVersion`,
  U26) is narrower than its own name and its own row claim.** §15 U26 says "Every mutation method —
  raises exactly one event and calls `UpdateVersion()`"; the test only calls `FlagHighRisk`. It's
  not wrong (that one call does behave as claimed), but the name and the row both read as a
  universal claim the test doesn't back — every other mutation method's own dedicated test already
  incidentally proves the same thing for itself (e.g. U1 asserts `Assert.Single(cat.Events)` after
  `Register`), so this row is effectively covered by the sum of the others, but this specific test
  should either be renamed to say what it actually checks, or parametrised over all eleven mutation
  methods to earn its name.

- **[LOW, carried, not new]** `QuestPdfCatMedicalHistoryRenderer.cs:13-14` still states "QuestPDF
  licensing... must be confirmed before merge... Not resolved here" (§20.6). `CatProfilesModule.cs:91`
  does set `QuestPDF.Settings.License = LicenseType.Community` in code, but the underlying
  business question (whether this deployment is under QuestPDF Community's revenue threshold) is
  still open per the code's own comment. Not a code defect; flagging so it isn't lost now that this
  handover is finally being gated.

## Dead code / duplication

None beyond the expected, intentional kind: every Cat command handler repeats the same private
`Violation(...)` helper, which is the same "canonical shape, deliberately self-contained handler"
pattern already accepted (not flagged) in HO-ACC-001's own CODE_REVIEW pass. No stale
`IBusMessageDispatcher`/`AddServiceBusProvider`/`EventPublishOption`-superseded-shape/etc. anywhere
in this slice's code. No `CommandService` wrapping a bare repository call (`RegisterVaccineCommandHandler`'s
uniqueness check is a handler-level repository call, exactly as §8.7 specifies, not a service).

## Build / unit tests (run as part of this review)

`dotnet build CatEcommerce.sln` — 0 warnings, 0 errors.
`dotnet test tests/CatEcommerce.Domain.Tests` — 161/161 passed.
`dotnet test tests/CatEcommerce.Application.Tests` — 66/66 passed.
Integration tests and the shared `catecommerce_dev` database were not touched, per the task's
instructions (SECURITY is using the database in parallel).

SCOPE CHECK
- Reviewed: every file under `src/Domain/CatEcommerce.Domain/Aggregates/Cat/`,
  `.../Aggregates/Vaccination/`, `.../Entities/` (Cat*), `.../Events/Cat/`, `.../Events/Vaccination/`,
  `.../Models/` (Cat*/Vaccine*), `.../ValueObjects/Weight.cs`; all of
  `src/Application/CatEcommerce.Application/CommandHandlers/{Cat,Vaccination}/`,
  `CommandHandlers/Validators/*Cat*`, `*Vaccin*`, `DataMappers/CatMapper.cs`,
  `DomainEventHandlers/Cat/`; all of `src/Read/CatEcommerce.Read/{Queries,QueryHandlers,ViewModels}`
  touching Cat/Vaccination; `src/Infrastructure/CatEcommerce.Infrastructure/{Documents,
  Persistence/Configurations (Cat*/Vaccine*/AuditEntry), Repositories/EfAggregateRootRepository.cs,
  Configuration/CatProfileOptions.cs}`; `src/Api/CatEcommerce.Api/{Controllers/CatsController.cs,
  Controllers/VaccinesController.cs, Contracts/Cat/*, Contracts/Vaccination/*,
  Modules/CatProfilesModule.cs}`; both `tests/CatEcommerce.Domain.Tests` and
  `tests/CatEcommerce.Application.Tests` in full; `tests/CatEcommerce.IntegrationTests` structure
  (confirmed what does and does not exist for Cat).
- Files outside this handover's declared §13 scope: none found touched beyond what the handover's
  own §1 amendments already record (the three audit handlers amended by HO-ACC-001 (19)(b), the
  merge-reassignment method added by HO-ACC-001 §8.9, the C10 amendment already gated separately).
