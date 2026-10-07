GATE: CODE_REVIEW
HANDOVER: HO-ADM-001
COMMITS: `git diff dd495c5..517d22f` (ninth pass, delta closing the ACCEPTANCE gate's first-pass A3,
A6 and A10 findings; commits 8bc27ac, 517d22f — test-only plus doc/TC-sheet bookkeeping, no source
changed)
DATE: 2026-09-26
STATUS: PASS

Ninth pass (delta dd495c5..517d22f). Scope: `8bc27ac` (four new/changed test files —
`CatVaccineFullAuthorizationMatrixTests.cs` (new, 20 endpoints × anonymous/8 roles/customer),
`StaffAvailabilityTests.cs` (+5 unit cases), `AdminRosterLeaveEndpointTests.cs` (+2 integration
cases), `AdminGlobalSettingsLiveEffectEndpointTests.cs` (+2 integration cases)) and `517d22f`
(handover Amended (45)-(49), `TC-HO-ADM-001.md` bookkeeping — doc-only). No source file changed in
either commit (confirmed: `git diff dd495c5..517d22f -- 'src/**'` is empty). Read
`docs/gates/HO-ADM-001-ACCEPTANCE.md` first (its FAIL verdict and A3/A6/A10 rows are what this round
answers), then HO-ADM-001.md §12.3/§12.4/§16/§19 directly, then each new test file in full,
independent of the commit messages' or the TC sheet's own characterization.

## Key question: does I-A2 require Administration (/api/admin/**, /api/staff/**) endpoints too?

No. §16's own I-A2 row reads verbatim: "For **each** Cat and Vaccine endpoint: anonymous, customer
(own), customer (other's cat), each staff role | Status matches §12.3's table for every cell —
**US-ADM-02 AC1**". §12.3's own table is headed "`ActionRoles` additions" and lists exactly the Cat
and Vaccine actions (`Cats.View` through `Vaccines.Manage`) with their endpoints — it does not treat
`Branches.*`, `Rosters.*`, `Leave.*`, `Services.*`, `Settings.*`, `AuditLog.*`, `Content.*`,
`Policies.*` or `System.ManageMaintenance` as part of I-A2's scope, even though those rows sit in the
same table for a different reason (declaring the full `ActionRoles` additions the build makes, not
I-A2's test scope). §19 row 4 ("I-A2 is a full matrix, not a sample") is the TEST gate's own
instruction and refers to this same I-A2 case by name — it does not enlarge I-A2's stated scope to
every `[RequireAction]` endpoint in the slice. §12.4's Api table lists the `/api/admin/**` and
`/api/staff/**` routes with their required action, but no integration-test case in §16 (I-A1 through
I-X1) asks for a full-matrix authorization sweep of those routes; each Administration area instead has
its own scoped access tests (I-A3, I-B5, I-C5, I-C6, I-E4, etc.) proving specific denial cells, which
is the same "well-chosen sample" pattern the ACCEPTANCE gate itself accepted for every other area
(only I-A2's Cat/Vaccine matrix carries the explicit "not a sample" instruction).

**Conclusion: `CatVaccineFullAuthorizationMatrixTests.cs`'s 20 Cat/Vaccine endpoints are the correct
and complete scope for I-A2. No FAIL finding for missing Administration controllers.** If the owner
wants the same full-matrix treatment for `/api/admin/**`/`/api/staff/**`, that is a new instruction,
not something §16/§19 as written already require — flagged as a Suggestion below, not a gate finding.

## A3/I-A2 — `CatVaccineFullAuthorizationMatrixTests.cs`

Read the full 237-line file. Two independent halves, deliberately kept apart so neither can quietly
substitute for the other:
- **Endpoint discovery is reflection-based** (`DiscoverEndpoints()`), walking `CatsController`/
  `VaccinesController`'s own `[Http*]`+`[RequireAction]` actions — not a hand-maintained list, so a
  future endpoint cannot be silently left out (the commit message's own claim that this is exactly how
  the previously-missing `POST /api/cats/{id}/vaccinations` case was found is verified: the older
  `CatVaccineAuthorizationSecurityTests.Anonymous_EveryCatAndVaccineEndpoint_IsRefused` hand-written
  probe list is confirmed, by reading it, not to name that route).
- **Expected roles per action are hand-typed from §12.3** into `SpecRolesByAction`, a `Dictionary`
  keyed by `Actions.Cats.*`/`Actions.Vaccines.*` string constants — never read from
  `ActionRoles.Administration.cs`. This is the correct anti-tautology design: a bug in the production
  `ActionRoles` table (e.g. an accidentally-added role) would be caught, not rubber-stamped, because
  the expected table lives independently in the test file. Checked every row in `SpecRolesByAction`
  against §12.3's table by hand — all ten actions match exactly (`Cats.View`: Reception/Nurse/Vet/
  Caretaker/Administrator; `Vaccines.View`: all eight roles; etc.).
- `EveryDiscoveredAction_IsInTheHandTypedSpecTable` fails loudly if a new `[RequireAction]` action
  appears on either controller with no row in `SpecRolesByAction` — confirmed this is a real guard
  (not vacuously true): `discovered.Select(...).Except(SpecRolesByAction.Keys)` is a genuine
  set-difference check. `EveryDiscoveredEndpoint_HasABodyEntryIfItNeedsOne` is the same pattern for
  the `BodiesByRoute` table.
- `Endpoint_MatchesTheSpecTableForEveryCallerType` is one `[Theory]` per discovered endpoint,
  asserting: anonymous → 401; each of the 8 staff roles → allowed (not 401/403) or 403 per the
  hand-typed table; customer-own → allowed for the three owner-scoped actions (`Cats.View`,
  `Cats.Register`, `Cats.EditProfile`), 403 for every other action; customer-other → 404 for the
  owner-scoped, `OwnerScope.Cat` actions. This is the real cross-product §19 demands: 20 endpoints ×
  (1 anonymous + 8 roles + up to 2 customer cases) — confirmed by running it (below): 20 theory cases,
  each covering 9-11 caller-type assertions internally (collected into one `failures` list per case
  rather than stopping at the first, so a run's failure output shows every wrong cell for that
  endpoint at once — a genuine improvement in diagnosability over a single `Assert.Equal`).
- Traced `GetOrSeedStaffAsync`'s `ConcurrentDictionary<string, Lazy<Task<...>>>` caching: one seeded
  staff account per role is reused across all 20 theory cases (not reseeded per case), which is
  correct test hygiene (avoids seeding 160 staff accounts) and does not weaken the assertion — each
  case still independently signs in and calls the endpoint fresh.
- Confirmed the file does not read `ActionRoles.Administration.cs` anywhere (grepped the file) — the
  anti-tautology design is real, not just claimed in the doc comment.

**A3 finding from the ACCEPTANCE gate's first pass is closed.** This is the exhaustive, non-sample
matrix §19 row 4 requires, scoped correctly to Cat/Vaccine per I-A2's own wording (see above).

## A6/U-C1/U-C3/I-C1 — leave and extra-shift effect on availability

Read `StaffAvailabilityTests.cs`'s five new cases and `AdminRosterLeaveEndpointTests.cs`'s two new
integration cases in full.

- `Intervals_ApprovedLeaveCoveringTheDate_RemovesTheWholeDay` (U-C1 rule 5, U-C3): builds a roster with
  a Monday shift, then an `Approve`d `LeaveRequestAggregate` covering that Monday, calls
  `StaffAvailability.Intervals` with the leave in the `approvedLeave` list → asserts `Empty`. The
  negative half (requested-but-not-approved leave changes nothing) is proven by calling the same
  scenario with an **empty** list for that parameter and asserting `Single` — this is exactly what the
  production caller does for a merely-`Requested` leave (per the code's own comment; only `Approved`
  leave is ever passed to this parameter by the real query handler). Genuinely would fail if rule 5
  were removed (the shift would still show with leave passed in), and the negative half genuinely
  exercises a different call shape, not a coincidental pass.
- `Intervals_ExtraShiftException_AddsAnIntervalOnADayWithNoPatternSlot` (rule 4 add-half): a roster
  with only a Monday pattern slot, an `ExtraShift` exception on Tuesday → before: `Empty`; after:
  `Single` with the exact exception times. Would fail if `ExtraShift` handling were removed from
  `StaffAvailability.Intervals`.
- `Intervals_ShiftCancelledException_ShortensOrRemovesTheShift` (rule 4 remove-half, rule 7's
  "never zero-length"): a partial `ShiftCancelled` (09:00-12:00 of a 09:00-17:00 shift) shortens to
  12:00-17:00; a full-day cancellation removes the interval entirely (not a zero-length one). Both
  assertions are specific enough to fail if cancellation handling were removed or implemented
  differently.
- `Intervals_BranchClosedWholeDay_RestrictToOpeningHoursTrue_ReturnsNoIntervalThatDay` (rule 2 composed
  with `BranchCalendar`): empty `branchOpenIntervals` (what a fully-closed day returns) +
  `restrictToOpeningHours=true` → `Empty`, despite a roster shift existing. Correctly scoped: the
  closure computation itself remains `BranchCalendarTests`'s job; this proves the two compose.
- `Intervals_AbuttingPatternShiftAndExtraShift_MergeIntoOneInterval` (rule 7 merge): a 09:00-12:00
  pattern shift plus an abutting 12:00-15:00 `ExtraShift` merge into one 09:00-15:00 interval, not two
  adjacent ones — would fail if the merge/coalesce logic were removed (two separate intervals, or a
  gap, would result instead).
- `AdminRosterLeaveEndpointTests.ApprovedLeaveDay_ThenGetAvailability_ExcludesThatDay` (I-C1, end to
  end): sets branch hours, assigns staff, sets a pattern, confirms the shift via the real
  `GET .../availability`, has the staff member request leave and the admin approve it through the real
  API, re-queries and confirms the day is now empty. `ExtraShiftException_ThenGetAvailability_
  IncludesTheExtraShift`: confirms an empty Tuesday, adds an `ExtraShift` exception through the real
  API, re-queries and confirms the interval now appears. Both are genuine round-trips through
  controller → command handler → aggregate → read-side query, not a domain-only test — would fail if
  any layer between the API and `StaffAvailability` dropped the leave/exception effect.

All seven `StaffAvailability` rules (1-7) now have a dedicated unit test, confirmed against §8.5's own
rule list. **A6 finding from the ACCEPTANCE gate's first pass is closed.**

## A10/I-D5 — settings-change audit, watched not inferred

Read `AdminGlobalSettingsLiveEffectEndpointTests.cs`'s two new cases and the collection they join.
- `ChangeGlobalSetting_AuditsADataChangeRowWithBeforeAndAfterValues`: reads the current effective value
  of `booking.slotHoldMinutes`, changes it to a different value via `PUT /api/admin/settings`, searches
  `/api/admin/audit-entries?entityId=<GlobalId>&entityType=BusinessSettingsAggregate&action=
  BusinessSettingsChangedEvent&from=...&to=...`, and asserts the row's `beforeValue` contains the
  original value and `afterValue` contains the new one (`Assert.Contains($"\"{originalValue}\"",
  before)` — a real content check against a JSON-serialized snapshot, not just "a row exists").
  Restores the original value in `finally`.
- `ChangeBranchSettingOverride_AuditsADataChangeRowWithTheAfterValue`: creates a fresh branch, sets a
  branch-scoped override, searches for the `DataChange` row scoped to that branch's id, asserts
  `afterValue` contains the override value (before-value is correctly not asserted — the class's own
  doc comment explains a first-write insert has no prior snapshot, consistent with
  `AuditChangeCaptureInterceptor`'s documented behaviour).
- Both tests are in `AdminGlobalSettingsLiveEffectEndpointTests`, already
  `[Collection(AdministrationGlobalSettingsCollection.Name)]` at the class level (confirmed by reading
  the class declaration) — correctly serialized against the other settings-mutating tests in the same
  collection, consistent with the eighth pass's own HIGH fix for this exact race.
- This is the missing half the ACCEPTANCE gate named precisely: no earlier test had searched the audit
  log after a settings change. These two do, with real content assertions, not an inferred "the generic
  interceptor mechanism must be working." **A10 finding from the ACCEPTANCE gate's first pass is
  closed.** The TC sheet's own claim that no source change was needed (EF's change tracker already
  marks `BusinessSettingsAggregate` `Modified` via its own `Version` column update) is consistent with
  these two tests passing without any corresponding source diff in `8bc27ac`.

## Endpoint-discovery / table-honesty checks (from the task brief)

- **Expected-outcome table typed from spec, not derived from `ActionRoles`:** confirmed above — no
  tautology.
- **Endpoint discovery fails if a new action is added without a table row:** confirmed —
  `EveryDiscoveredAction_IsInTheHandTypedSpecTable` and `EveryDiscoveredEndpoint_HasABodyEntryIfItNeedsOne`
  are both real `Except(...)` set-difference assertions.
- **Availability tests would fail if leave/extra-shift handling were removed:** confirmed by tracing
  each assertion against what `StaffAvailability.Intervals` would return with that rule's branch
  deleted — each test's "before" state and "after" state differ specifically because of the rule under
  test, not by coincidence.
- **Settings-audit tests assert real before/after values:** confirmed — `Assert.Contains` against the
  actual `originalValue`/`newValue`/`overrideValue` strings, not merely `NotNull`.
- **New global-settings tests are inside `AdministrationGlobalSettingsCollection`:** confirmed.
- **TC sheet counts are honest:** read the Summary section's rewrite. It now separates `PASS (fully
  covered)` (90) from `PASS (partial/indirect — see row)` (10), correcting the prior pass's folding of
  both into one `PASS: 100` figure — matches the ACCEPTANCE gate's own LOW finding about this exact
  sheet. The three criteria targeted this round (A3, A6, A10) are updated to plain `Covered`/`PASS`;
  the other three qualified rows (A5/U-A5, A7/I-C4, A9/U-D6) are left honestly marked as out of this
  round's scope, not silently absorbed into a clean 100%. No overclaiming found.

## Docs accuracy — Amended (45)-(49)

Read Amended (45) through (49) in `docs/handover/HO-ADM-001.md` (embedded in the handover's single
running "Amended" cell) against the actual diff. (45) names the full-matrix file and its anti-
tautology design correctly. (46) names the five `StaffAvailabilityTests` cases and two
`AdminRosterLeaveEndpointTests` cases correctly, and correctly notes the one pre-existing gap left
as-is (`CatVaccineAuthorizationSecurityTests`'s own probe list still missing the vaccinations route,
additive not corrected in place — confirmed true, that file is unchanged in this diff). (47) correctly
states no source change was needed for A10 and cites the specific SQL evidence
(`UPDATE admin.business_settings SET "Version"` alongside the owned-collection `UPDATE`) for why. (48)
is the LOW scope finding from the ACCEPTANCE gate (`NullActiveBranchAccessor.cs`/
`IActiveBranchAccessor.cs` undeclared) — recorded as a documentation fix, not a code change; confirmed
these two files are untouched by this diff, consistent with "declaration, not a change." (49) matches
the TC-sheet Summary rewrite described above. No overstatement found in any of the five entries.

## No superseded-architecture patterns

Grepped the full delta (`git diff dd495c5..517d22f`) for every Critical-list name
(`IBusMessageDispatcher`, `AddServiceBusProvider`, `EventPublishOption` misuse,
`[BsonIgnoreExtraElements]`, `IReadRepository`, `ViewModelBase`, `CustomQueueConfig`, a rejection
event, `Application.CommandServices`, `CommandWorker`/`EventWorker`, MassTransit/RabbitMQ/MongoDB) —
none appears. This delta is test-only plus doc bookkeeping; no controller, handler, repository or DI
registration changed. `IInMemoryDispatcher`/`GetResultAsync` usage in the new test files' HTTP calls
goes through the existing, unchanged controllers.

## Build and tests (scratch checkout of `517d22f`)

`git archive 517d22f` into a freshly created `scratchpad/cr9-adm` under this session's scratch
directory (deleted after, and only that folder — no other scratch content touched): `dotnet build -c
Release` — 0 errors, 20 `NU1900` warnings only (private-feed vulnerability-index lookup blocked in
this sandbox, unrelated to the diff), 0 code warnings.

`dotnet test -c Release --no-build` against `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;
Database=catecommerce_adm;Username=postgres;Password=postgres`:
- `CatEcommerce.Domain.Tests`: **221/221** (up from the eighth pass's 216 — the 5 new
  `StaffAvailabilityTests` cases).
- `CatEcommerce.Application.Tests`: **73/73** (unchanged).
- `CatEcommerce.Infrastructure.Tests`: **28/28** (unchanged).
- `CatEcommerce.IntegrationTests`: **315/315** (up from 289 — the 20-case
  `CatVaccineFullAuthorizationMatrixTests` theory + 2 housekeeping facts = 22, plus 2
  `AdminRosterLeaveEndpointTests` cases, plus 2 `AdminGlobalSettingsLiveEffectEndpointTests` cases =
  26 new; 289+26=315, matches).
- **Total: 637/637**, matching the TC sheet's own reported ninth-pass tally exactly. 0 failed, 0
  skipped.

## Findings summary

None outstanding. All three ACCEPTANCE-gate first-pass findings (A3/I-A2 sample-not-matrix,
A6/leave-and-extra-shift unobserved, A10/settings-audit not directly watched) are closed by this
delta with real, non-tautological tests, verified independently against the code (not taken on the
commit messages' or the TC sheet's own word), and the handover/TC-sheet bookkeeping accurately
describes what was done. I-A2's scope (Cat/Vaccine only, not `/api/admin/**`/`/api/staff/**`) is
correct per §16's own wording — no FAIL finding for missing Administration-endpoint coverage; see the
"Key question" section above.

**Suggestion (non-blocking):** if the owner wants the same full-matrix, anti-tautology treatment
applied to the `/api/admin/**`/`/api/staff/**` endpoint surface (beyond the scoped per-area denial
tests already in place — I-A3, I-B5, I-C5, I-C6, I-E4, etc.), that would need to be a new instruction
added to a future handover revision or a follow-up test-engineering task; it is not something §16/§19
as currently written require.

SCOPE CHECK
- files in allowed scope: 6/6 (`docs/handover/HO-ADM-001.md`, `docs/test-cases/TC-HO-ADM-001.md`, and
  the four test files under `tests/**` — `CatVaccineFullAuthorizationMatrixTests.cs`,
  `StaffAvailabilityTests.cs`, `AdminRosterLeaveEndpointTests.cs`,
  `AdminGlobalSettingsLiveEffectEndpointTests.cs`)
- files outside allowed scope: none

---

## Eighth pass (superseded by the above)

GATE: CODE_REVIEW
HANDOVER: HO-ADM-001
COMMITS: `git diff 86d2e45..f108c5b` (eighth pass, delta over the seventh pass's PASS at `6118ac1`;
worktree head has since advanced to `86d2e45` with no source change before this delta, then to
`f108c5b`; commits ca3809d, c3dee41, f108c5b — closing this file's own seventh-pass HIGH, MEDIUM and
LOW)
DATE: 2026-09-26
STATUS: PASS

Eighth pass (delta 86d2e45..f108c5b), a short combined gate closing the three non-blocking findings
this file's own seventh pass left open. Scope: `ca3809d` (`[Range(1, AuditExportRules.MaxRows)]` on
`AuditExportOptions.MaxRows`; `AdministrationModule` switched from `services.Configure<T>()` to
`services.AddOptions<T>().Bind(...).ValidateDataAnnotations().ValidateOnStart()`), `c3dee41`
(`AdminCatalogueSettingsEndpointTests` and HO-ACC-001's `StaffSessionEndpointTests` both joined
`AdministrationGlobalSettingsCollection`; a new startup-validation theory/fact pair in
`AdminAuditEntriesEndpointTests`; a new negative-price `400` test in
`AdminCatalogueSettingsEndpointTests`), and `f108c5b` (handover Amended (42)-(44) and
`TC-HO-ADM-001.md` bookkeeping, doc-only). Read this file's own seventh-pass section (below) first,
then independently re-derived each of its three findings against the actual diff rather than taking
the commit messages on trust.

## MEDIUM — `AuditExportOptions.MaxRows` validated at startup

`ca3809d` adds `[Range(1, CatEcommerce.Shared.Common.Constants.AuditExportRules.MaxRows)]`
(50,000, the product's own hard ceiling) to `AuditExportOptions.MaxRows`, and
`AdministrationModule.cs` (in `src/Api/CatEcommerce.Api/Modules/`, not `Infrastructure` — checked the
actual path, the class lives in the API project) changes registration from
`services.Configure<AuditExportOptions>(...)` to `services.AddOptions<AuditExportOptions>()
.Bind(...).ValidateDataAnnotations().ValidateOnStart()`, the exact pattern `AccountOptions` already
uses (confirmed by reading `AccountsModule`'s own registration, unchanged, alongside it). This closes
every sub-case the seventh pass named:
- `MaxRows == 0` and negative values are now rejected by `[Range(1, ...)]` before the host finishes
  starting — no longer relying on `PageSize` arithmetic to fail closed by accident, and no longer
  capable of reaching `Query<T>.Skip`/`Take` with a negative `PageSize`.
- A value above the product's own cap (50,000) is now also rejected — `[Range(1, 50000)]`'s upper
  bound is `AuditExportRules.MaxRows` itself, not `int.MaxValue`, so an operator can lower the cap but
  never raise it past what the product ships with, and `maxRows + 1` in
  `AdminAuditEntriesController.Export` can no longer approach `int.MaxValue`, closing the overflow
  concern.
- `ValidateOnStart()` means a misconfigured value now fails host boot, not the first export request
  (or never, if it silently raised the cap).

Tests: `AdminAuditEntriesEndpointTests.HostStartup_AuditExportMaxRowsOutOfRange_FailsToStart` (a
`[Theory]` over `"0"`, `"-1"`, `"50001"`) and
`HostStartup_AuditExportMaxRowsAtTheProductCap_StartsSuccessfully` (a boundary positive control at
exactly `50000`, guarding against an off-by-one in the `Range` upper bound). Both use
`Factory.WithWebHostBuilder(...)` to override `AuditExport:MaxRows` via `AddInMemoryCollection`, then
`Record.Exception(() => factory.CreateClient())` to force the host to build — the same technique
`HostCompositionTests.HostInProduction_WithADevelopmentOnlySeamConfigured_FailsToStart` already uses,
confirmed by reading that file. Both `badFactory`/`okFactory` instances are `using`-scoped, so the
derived `WebApplicationFactory` (and, for the failing case, whatever partially-built host state
`ValidateOnStart` leaves behind before throwing) is disposed at the end of each test — no leaked
`TestServer`/host between the two theory cases or the two test methods. Traced `Assert.Contains
("MaxRows", exception.ToString())`: `ValidateDataAnnotations()` throws
`OptionsValidationException` whose `Message`/`ToString()` includes the `[Range]` attribute's
`ErrorMessage` text, which contains the literal substring `MaxRows` — a real, non-tautological
assertion, not merely "some exception was thrown". **MEDIUM closed.**

## HIGH — cross-class settings-test race

The seventh pass's finding was precise: `AdministrationGlobalSettingsCollection` isolated only its
own file's three tests, leaving two pre-existing, unrelated test classes exposed to a race against
the same shared-database global settings — `AdminCatalogueSettingsEndpointTests` (`booking
.slotHoldMinutes`) and HO-ACC-001's own `StaffSessionEndpointTests`
(`security.staffIdleTimeoutMinutes`). `c3dee41` adds `[Collection(AdministrationGlobalSettingsCollection
.Name)]` to both classes — the "broadest, safest fix" the seventh pass itself named, over
capture/restore per assertion.

Verified this is not merely cosmetic:
- `AdministrationGlobalSettingsCollection` is `[CollectionDefinition(Name, DisableParallelization =
  true)]` with no `ICollectionFixture<T>` — it only serializes execution of every class carrying
  `[Collection(Name)]`, it does not share a fixture instance. Confirmed both newly-joined classes keep
  their own `IClassFixture<TFactory>` construction (`AdministrationApiFactory` for
  `AdminCatalogueSettingsEndpointTests`, `AccountsApiFactory` for `StaffSessionEndpointTests`)
  unchanged — joining the collection does not disturb either class's own fixture lifecycle, so this
  does not break `StaffSessionEndpointTests`'s own setup as asked. No other test in the file gained or
  lost behaviour from the attribute beyond serialization.
- Confirmed `StaffSessionEndpointTests` had no pre-existing `[Collection(...)]` attribute to conflict
  with (checked the class declaration before this diff) — nothing double-applies.
- Confirmed `CachedBusinessSettings.StaffIdleTimeout` (the code path
  `PassiveGet_IdleExpired_AsTheFirstRequestAfterIdling_ReturnsSessionIdleTimeoutNotIdentity` actually
  exercises via `GET /api/staff/session`) reads `SettingKeys.SecurityStaffIdleTimeoutMinutes` from the
  database, not `AccountOptions.StaffIdleTimeoutMinutes` from static configuration — so this test
  genuinely races the same global database row `AdminGlobalSettingsLiveEffectEndpointTests`'s own
  I-D4 test mutates, confirming the finding was correctly diagnosed and not a false positive. (Aside,
  not a finding for this pass: the test's own inline comment, at three call sites in this file, still
  says "`AccountOptions.StaffIdleTimeoutMinutes = 30 (appsettings.json)`" — stale relative to
  `CachedBusinessSettings`'s HO-ACC-001 §12.2 rewire, which this diff did not touch and which predates
  this handover's own build; not introduced or worsened by this delta, so not raised as a finding
  here, but worth HO-ACC-001's own future attention since the comment now misdescribes which layer
  actually governs the value.)
- Swept the whole `IntegrationTests` project (not just the two classes the gate named) for every other
  read/write of the three keys `AdminGlobalSettingsLiveEffectEndpointTests` mutates
  (`clinical.vaccinationDueSoonDays`, `security.staffIdleTimeoutMinutes`, `booking.slotHoldMinutes`):
  grepped each key name and each settings-key constant across `tests/CatEcommerce.IntegrationTests`.
  `clinical.vaccinationDueSoonDays` and `booking.slotHoldMinutes` each appear in exactly the two files
  already in the collection (`AdminCatalogueSettingsEndpointTests`,
  `AdminGlobalSettingsLiveEffectEndpointTests`) plus, for the vaccination key, a pure unit test
  (`CatEcommerce.Application.Tests/QueryHandlers/Cat/CatVaccinationStatusCalculatorTests.cs`) that
  never touches the database or an `HttpClient`, hence cannot race. `security.staffIdleTimeoutMinutes`
  appears only in `StaffSessionEndpointTests` and `AdminGlobalSettingsLiveEffectEndpointTests` (plus
  production code). No fourth exposed test class exists. Also confirmed there is still exactly one
  `[CollectionDefinition]` in the whole project (`AdministrationGlobalSettingsCollection` itself) and
  no assembly-level `[CollectionBehavior(DisableTestParallelization = true)]`, so this remains the only
  serialization mechanism — consistent with the seventh pass's own analysis, not superseded by
  anything else in this delta.
- Confirmed neither factory type resets the database between test classes (`AccountsApiFactory.cs`,
  `AdministrationApiFactory.cs` — no Respawn/reset call in either), so the two classes genuinely
  contend for the same physical row with nothing else serializing them; the new `[Collection]`
  attribute is the only thing now doing so, and it is sufficient given both classes use
  `IClassFixture`, not `ICollectionFixture` (no fixture-sharing hazard to introduce).
- Ran the full integration suite twice from a scratch checkout (below, all green), then the three
  settings-touching classes together three more times specifically targeting this race — 19/19 in
  every run, no failure. Timing-dependent as ever, but the static fix (same collection now covers
  every known racer) is what actually closes the gap, not the absence of an observed failure.

**HIGH closed**, on both classes named, with the sweep for a third finding none.

## LOW — `SetServicePrice_NegativeAmount_Returns400`

`c3dee41` adds this test to `AdminCatalogueSettingsEndpointTests.cs`: creates a service, then
`POST /api/admin/services/{id}/prices` with `amount: "-5.00"`, asserting `400`. This is the exact gap
the seventh pass named — the TEST gate's U-D7 correction rested its whole argument on
`SetServicePriceCommandValidator.Amount.GreaterThanOrEqualTo(0)` being live, but nothing had exercised
it end-to-end. Traced the test: it does not stub or bypass the validator pipeline, goes through the
real controller/command-dispatch path, and a negative amount with no other test asserting `201` for
this shape means the assertion is genuine (would fail today if the validator rule were removed or
mis-registered). **LOW closed.**

## Docs accuracy

Read Amended (42)-(44) in `docs/handover/HO-ADM-001.md` and the corresponding "Eighth pass" block in
`docs/test-cases/TC-HO-ADM-001.md` (rows U-D5, U-D7, I-D4, I-E3) in full, diffed against `86d2e45`.
Both describe exactly what the code does: Amended (42)'s naming of both `AdminCatalogueSettingsEndpointTests`
and HO-ACC-001's `StaffSessionEndpointTests` as the two classes joined to the collection matches the
diff; the "editing `StaffSessionEndpointTests.cs` on HO-ACC-001's behalf" framing matches this
repository's established "ahead of that area's own change" precedent already used elsewhere in this
handover's own history. Amended (43)'s description of the `[Range]` bound and the
`ValidateOnStart()` switch matches `ca3809d` exactly, including "an operator may still lower the cap,
never raise it past `AuditExportRules.MaxRows`". Amended (44) and the U-D7 TC-sheet row both correctly
name `SetServicePrice_NegativeAmount_Returns400` as the closing test. No overstatement found.

## No superseded-architecture patterns

Grepped the full delta for every Critical-list name — none appears. No controller in this diff touches
a `DbContext`, repository or aggregate; `AdministrationModule`'s options-registration change is a
configuration-binding change (`AddOptions<T>().Bind().ValidateDataAnnotations().ValidateOnStart()`),
not a validator or handler registration, so it does not implicate the `IValidator<TCommand>` rule or
assembly-scanning conventions. `[Collection]`/`[CollectionDefinition]` are ordinary xUnit v2
test-only constructs, not part of the production DI/dispatch surface.

## Build and tests (scratch checkout of `f108c5b`)

`git archive f108c5b` into a newly created `scratchpad/cr8-adm` under this session's scratch
directory (deleted after, and only that folder — no other scratch content touched): `dotnet build
CatEcommerce.sln` — 0 errors, 20 `NU1900` warnings only (private feed vulnerability-index lookup
blocked in this sandbox, unrelated to the diff), 0 code warnings.

- `CatEcommerce.Domain.Tests`: **216/216** (unchanged from the seventh pass — this delta touched no
  domain code or domain tests).
- `CatEcommerce.Application.Tests`: **73/73** (unchanged).
- `CatEcommerce.Infrastructure.Tests`: **28/28** (unchanged).
- `CatEcommerce.IntegrationTests`, against `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;
  Database=catecommerce_adm;Username=postgres;Password=postgres`: **289/289**, run twice, both green
  (up from the seventh pass's 284 — 5 new: `SetServicePrice_NegativeAmount_Returns400`,
  `HostStartup_AuditExportMaxRowsOutOfRange_FailsToStart` ×3 theory cases,
  `HostStartup_AuditExportMaxRowsAtTheProductCap_StartsSuccessfully`). A third, targeted run of just
  `AdminCatalogueSettingsEndpointTests` + `AdminGlobalSettingsLiveEffectEndpointTests` +
  `StaffSessionEndpointTests` together, three times, for the specific race this pass closes: 19/19
  every time.

## Findings summary

None outstanding. All three findings from this file's own seventh pass — MEDIUM (`AuditExportOptions
.MaxRows` unvalidated), HIGH (settings-test isolation not covering every racer), LOW (U-D7's
enforcement point untested end-to-end) — are closed by this delta, verified independently against the
code and by test execution, not taken on the commit messages' or the handover doc's own word alone.

SCOPE CHECK
- files in allowed scope: 7/7 (`docs/handover/HO-ADM-001.md`, `docs/test-cases/TC-HO-ADM-001.md`,
  `AdministrationModule.cs`, `AuditExportOptions.cs`, `AdminAuditEntriesEndpointTests.cs`,
  `AdminCatalogueSettingsEndpointTests.cs`, `StaffSessionEndpointTests.cs` — the last edited on
  HO-ACC-001's behalf per the integrator's own note, consistent with this handover's established
  cross-area-fix precedent)
- files outside allowed scope: none

---

## Seventh pass (superseded by the above)

GATE: CODE_REVIEW
HANDOVER: HO-ADM-001
COMMITS: `git diff f87a02a..6118ac1` (seventh pass, delta over the sixth pass's PASS at `649e8a0`;
commits 407391f, fbc1fa4, 6118ac1 — TEST gate seventh pass: the `AuditExportOptions` seam for I-E3,
17 closed BLOCKED rows, and handover/TC-sheet bookkeeping)
DATE: 2026-09-26
STATUS: PASS

Seventh pass (delta f87a02a..6118ac1), a short combined gate covering architecture, security and
test-honesty. Scope: `407391f` (`AuditExportOptions`, its `IOptions<T>` consumption in
`AdminAuditEntriesController.Export`, its `services.Configure<>` in `AdministrationModule`, and the
`appsettings.json` `AuditExport` section), `fbc1fa4` (12 new/changed test files closing the 17
BLOCKED rows from `docs/gates/HO-ADM-001-TEST.md`), and `6118ac1` (handover Amended (38)-(41) and
`TC-HO-ADM-001.md` bookkeeping, doc-only). Read `docs/gates/HO-ADM-001-TEST.md` and the full
`TC-HO-ADM-001.md` sheet first, then the actual diff, not the sheet's own characterization of it.

## The options change (`AuditExportOptions`)

- **Layering: acceptable.** `AuditExportOptions` lives in `Infrastructure/Configuration/`, matching
  every other bound-options type in this codebase (`AccountOptions`, `CaptchaOptions`,
  `CorsOptions`, `RateLimitingOptions`, `RequestOriginOptions` — all in the same folder/namespace).
  `CatEcommerce.Api` already has a `ProjectReference` to `CatEcommerce.Infrastructure` (needed for
  `AdministrationModule`'s own DI wiring), so `AdminAuditEntriesController` injecting
  `IOptions<AuditExportOptions>` introduces no new assembly dependency. It is a new *pattern* in the
  sense that no other controller currently injects `IOptions<T>` directly (existing options
  consumers are all Infrastructure-layer services or `Program.cs`/`HostPipelineExtensions`), but
  nothing in `.ai/architecture.md` or `.ai/di-registration-guide.md` forbids a controller reading a
  plain configuration value this way, and it does not put a `DbContext`, repository or aggregate on
  the controller. Not a finding.
- **No secret in `appsettings.json`.** `AuditExport.MaxRows: 50000` is a row-count cap, not a
  credential. Fine in source control.
- **Default preserved.** `AuditExportOptions.MaxRows` defaults to
  `AuditExportRules.MaxRows` (50,000) via the property initializer, so an environment with no
  `AuditExport` section (or a section present without `MaxRows`) still gets 50,000 — confirmed by
  reading the class and by the fact the production `appsettings.json` entry equals the constant.
- **[MEDIUM] No validation of the configured value — "0/negative must not disable the cap" is true
  only by accident, and a large value silently *does* disable it.** Nothing rejects `MaxRows <= 0`
  or an unreasonably large value, and nothing bounds `maxRows + 1` against `int` overflow
  (`AdminAuditEntriesController.Export`, `var maxRows = auditExportOptions.Value.MaxRows; ...
  PageSize = maxRows + 1; ... if (result.TotalCount > maxRows) return StatusCode(413, ...)`):
  - `MaxRows == 0` happens to fail closed today (`PageSize = 1`, and any non-empty result has
    `TotalCount > 0`, so it 413s) — but that is a side effect of the check's arithmetic, not a
    guarded invariant, and it also 413s a legitimately empty-result export it shouldn't need to.
  - A negative `MaxRows` (e.g. a stray `-1` from a bad merge of config layers) produces a negative
    `PageSize`, which is not validated before being sent to the query pipeline — behaviour then
    depends on how the platform's `Query<T>.Skip`/`Take` handles a negative `Take` (untested here;
    plausibly an unhandled exception from the EF/Npgsql translation, i.e. a 500 instead of a clean
    4xx).
  - A very large `MaxRows` (an operator "raising" the cap, or a fat-fingered digit) does the
    opposite of fail-closed: it raises the export ceiling with no upper bound at all, and
    `maxRows + 1` overflows silently (unchecked arithmetic) once `MaxRows` is near `int.MaxValue`,
    flipping `PageSize` negative for no operator-intended reason.
  - No test exercises any of these edges — `ExportCsv_OverALoweredCap_Returns413` (the one new test
    that uses the seam) only ever sets `MaxRows` to a small positive number (3).
  - Fix: validate on start (`services.AddOptions<AuditExportOptions>().Bind(...).Validate(o =>
    o.MaxRows is > 0 and <= <some sane ceiling>, "AuditExport:MaxRows must be a positive integer
    within range").ValidateOnStart()`), so a misconfiguration fails at boot, not silently at the
    first export request (or not at all, if it silently raises the cap).

## Three "corrected the claim" rows

Read each row's own reasoning in `TC-HO-ADM-001.md`, the doc comment or spec text it cites, and the
actual code, independently — not taking the sheet's own verdict on trust.

- **U-D7 (`Money` "negative rejected").** `Money.cs`'s own doc comment (pre-existing, unchanged by
  this diff) is explicit: "non-negative where a given field requires it... [is] each owning
  aggregate's job, not this type's." `HO-ADM-001.md` §8.6 ties the "≥ 0" constraint specifically to
  `ServicePriceAggregate.Amount` ("`Amount` (`Money`: `decimal(12,2)` ≥ 0 + `CurrencyCode`)"), not to
  the shared `Money` value object in general — and `Money` is reused solution-wide (HO-BIL-001,
  HO-BRD-001, etc.) per its own doc comment, where an unconditionally non-negative representation
  type would not fit every future consumer (e.g. a refund/adjustment). Reading §8.6 as a per-field
  constraint on `ServicePriceAggregate`, not a universal `Money` invariant, is a faithful,
  defensible reading, not the test bent to fit the code. The delegated enforcement is real —
  `SetServicePriceCommandValidator.RuleFor(x => x.Amount).GreaterThanOrEqualTo(0)` exists and is
  registered as `IValidator<SetServicePriceCommand>` (confirmed in `AdministrationModule.cs`) — so
  the claim is not just plausible, it is true of the code. **Gap, not a deviation:** no test (new or
  pre-existing) actually submits a negative `Amount` through `POST
  /api/admin/services/{id}/prices` and asserts `400`; `AdminCatalogueSettingsEndpointTests` only
  covers backdating. `MoneyTests.Constructor_NegativeAmount_IsNotRejectedByThisType` proves the type
  doesn't reject it, but nothing proves the aggregate-level rule the correction now rests its whole
  argument on. [LOW] — add one integration test asserting `400`/`ValidationResponse` for a negative
  `SetServicePriceCommand.Amount`, closing the loop the correction opens.
- **I-C2 (roster promotion "on read").** `HO-ADM-001.md` §8.4's own table text is precise and was
  read correctly: `PromoteDuePattern` is "**Called lazily** by any command that loads the roster",
  while §8.5 rule 3 ("Pattern for a date = `NextPattern` if its effective date ≤ date, else
  `CurrentPattern`") is the availability calculator's own pure, unpersisted computation — a
  different "on read" than a raw roster-listing GET. `GetBranchRostersQueryHandler` (confirmed by
  reading it) is a plain `AsNoTracking` projection with no call to `PromoteDuePattern` anywhere in
  it. The new test
  (`ScheduleNextPattern_AdvancePastEffectiveDate_NotPromotedByABareRead_PromotedByTheNextCommand`)
  correctly disambiguates the two meanings of "read" in the spec rather than bending either — a
  faithful reading. The companion `StaffAvailabilityTests` case
  (`Intervals_NextPatternEffectiveOnASpecificDate_...`) independently proves rule 3's pure
  computation still works. No deviation.
- **I-X1 (log-capture harness).** The correction ("the harness already existed") is verifiably true —
  `CatEcommerceApiFactory.WithCapturedLogs()` predates this pass and is already used by HO-ACC-001's
  `SessionResolutionFailureTests`; grepped and confirmed. The new test
  (`RepresentativeFlows_CarryingFreeTextFields_LeaveNoTraceInTheLogs`) is a real, non-tautological
  check: it plants unique per-run markers in a policy body, a leave note, a decision note, a
  maintenance message and a branch phone number, then asserts none of the markers appear anywhere in
  the captured log text at any level — this would fail if any of those free-text fields were logged.
  No deviation.

## Isolated collection for global-settings tests — does NOT fully prevent parallel leakage

**[HIGH]** `tests/CatEcommerce.IntegrationTests/Endpoints/AdminGlobalSettingsLiveEffectEndpointTests.cs`
introduces `AdministrationGlobalSettingsCollection` (`[CollectionDefinition(Name,
DisableParallelization = true)]`) specifically to stop this new file's own three tests (which
mutate global, shared-database settings — `clinical.vaccinationDueSoonDays`,
`security.staffIdleTimeoutMinutes`, `booking.slotHoldMinutes`) from racing each other. Each test
does capture-before/restore-in-`finally` correctly, and `finally` runs even if an `Assert` inside
the `try` throws — verified by reading the control flow of all three tests. That part is sound.

What it does **not** do is prevent this new suite from racing a **pre-existing, unrelated** test
class that touches the exact same shared global setting:
`AdminCatalogueSettingsEndpointTests.GetSettings_ReturnsEveryRegistryKeyWithBoundsAndEffectiveValue`
(unchanged by this diff) hard-asserts the *ambient* value of `SettingKeys.BookingSlotHoldMinutes` is
`"10"` — the registry default — with no capture/restore of its own, on the assumption nothing else
concurrently changes it. `AdminCatalogueSettingsEndpointTests` carries no `[Collection(...)]`
attribute, so by xUnit v2's default behaviour it is its own implicit collection, and (confirmed:
grepped the whole `IntegrationTests` project — `AdministrationGlobalSettingsCollection` is the only
`[CollectionDefinition]` anywhere, and there is no assembly-level
`[CollectionBehavior(DisableTestParallelization = true)]` or `xunit.runner.json` disabling
collection parallelism) different collections run **concurrently** by default. Both classes also use
`IClassFixture<AdministrationApiFactory>` (confirmed via `CatEcommerceIntegrationTestBase<TFactory>`
— per-class fixture instances, not a shared `ICollectionFixture`), so there is no fixture-level
serialization either; both ultimately hit the same physical `catecommerce_adm` database row.

Concretely: `AdminGlobalSettingsLiveEffectEndpointTests.SettingResolutionOrder_...` sets the global
`BookingSlotHoldMinutes` to `"18"`, then a branch override to `"25"`, then restores the original —
several real HTTP round-trips wide. If `AdminCatalogueSettingsEndpointTests`'s
`GetSettings_ReturnsEveryRegistryKeyWithBoundsAndEffectiveValue` happens to run its
`Assert.Equal("10", ...)` inside that window, it fails — not because of a product defect, but
because the new isolation only covers the new file. This is exactly the class of flakiness the new
collection's own doc comment says it is closing off ("bounding the window during which a changed
value is visible to the shortest span possible"), and the claim does not hold across the whole
suite. Ran the full integration suite and the two files' tests together several times in this
review's own run (see below) without reproducing a failure — timing-dependent, so absence of a
failure in a handful of runs is not proof of absence, and the static analysis above (different
collections, same database row, one side with no ambient-value defence) is conclusive on its own.
Fix: either add `AdminCatalogueSettingsEndpointTests` to `AdministrationGlobalSettingsCollection`
too (broadest, safest), or change that one assertion to capture-before/restore-after like every
other test that touches shared global state per §16.1, rather than asserting the registry default is
still ambient.

## Every new test would fail if the behaviour it names were removed

Read all twelve new/changed test files in full, not just the ones sampled above:
`MoneyTests.cs`, `ClinicServiceAggregateTests.cs` (+), `StaffLeaveApprovedEventShapeTests.cs`,
`StaffAvailabilityTests.cs` (+), `LeaveCommandHandlersTests.cs` (+), `AdministrationApiFactory.cs`
(+`WithLoweredAuditExportCap`/`WithFakeTimeProvider`), `AdminAuditEntriesEndpointTests.cs` (+),
`AdminContentPolicyEndpointTests.cs` (+), `AdminGlobalSettingsLiveEffectEndpointTests.cs` (new),
`AdminLogCaptureEndpointTests.cs` (new), `AdminRosterLeaveEndpointTests.cs` (+),
`ActionRolesCoverageTests.cs` (new). None is a tautology: each asserts a specific status code, a
specific field value against a value deliberately changed mid-test, or an absence (a marker string,
a promoted pattern, an unauthorized-role) that the actual pre-fix or broken-on-purpose code path
would visibly produce differently. `ActionRolesCoverageTests` in particular reuses
`RequireActionAttribute`'s own `TypeFilterAttribute.Arguments` (a genuine base-class property, not a
`CustomAttributeData` mismatch — checked the attribute's definition to confirm `Arguments![0]` is a
valid runtime read of the bound `action` string) and would fail today if any controller action's
`[RequireAction]` string had no `ActionRoles` row, or if any `ActionRoles` row named `"Customer"` or
an unknown role — both real, previously-uncaught failure modes.

## TC sheet ↔ tests

Spot-checked every test name the sheet cites for a row touched by this delta
(`ActionRolesCoverageTests`, `MoneyTests`, `StaffAvailabilityTests`,
`AdminRosterLeaveEndpointTests.ScheduleNextPattern_...`, `AdminLogCaptureEndpointTests`,
`AdminGlobalSettingsLiveEffectEndpointTests` (all three methods),
`ClinicServiceAggregateTests.Create_DurationOutOfBoundsOrNotAMultipleOfFive_ThrowsInvalidServiceDuration`,
`LeaveCommandHandlersTests.{ApproveLeave_AlreadyApproved_ReturnsLeaveNotPending,
RequestLeave_NurseRequestsForSomeoneElse_ReturnsActionNotPermitted,
ApproveLeave_SoleAdministratorApprovesOwnRequest_IsAllowedAndNotePrefixed}`,
`AdminContentPolicyEndpointTests.GetBanners_OutsideVisibilityWindow_...`) — every one exists at the
named file and method. No sheet row cites a test that isn't there.

## No superseded-architecture patterns

Grepped the full delta for every Critical-list name (`IBusMessageDispatcher`,
`AddServiceBusProvider`, `EventPublishOption` misuse, `[BsonIgnoreExtraElements]`,
`IReadRepository`, `ViewModelBase`, `CustomQueueConfig`, a rejection event, an
`Application.CommandServices` class, `CommandWorker`/`EventWorker`, MassTransit/RabbitMQ/MongoDB):
none appears. `AdminAuditEntriesController` still injects `IInMemoryDispatcher`, still uses
`GetResultAsync`, still returns the real result (`413`/`200`) rather than `202`. No controller in
this diff touches a `DbContext`, repository or aggregate. `AuditExportOptions` is registered via
`services.Configure<T>()` — configuration binding, not a validator or handler registration, so it
does not implicate the `IValidator<TCommand>`/assembly-scanning rules.

## Build and tests (scratch checkout of `6118ac1`)

`git archive 6118ac1` into a new `scratchpad/cr7-adm` (deleted after): `dotnet build
CatEcommerce.sln` — 0 errors, 20 `NU1900` warnings only (private feed vulnerability-index lookup
blocked in this sandbox, unrelated to the diff), 0 code warnings.

- `CatEcommerce.Domain.Tests`: **216/216** (up from the prior gate's 190 — 26 new: `MoneyTests` (10),
  `ClinicServiceAggregateTests` (+), `StaffLeaveApprovedEventShapeTests` (1), `StaffAvailabilityTests`
  (+2), etc.)
- `CatEcommerce.Application.Tests`: **73/73** (up from 68 — `LeaveCommandHandlersTests` additions)
- `CatEcommerce.Infrastructure.Tests`: **28/28** (unchanged)
- `CatEcommerce.IntegrationTests`, run once against `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;
  Database=catecommerce_adm;Username=postgres;Password=postgres`: **284/284** (up from 275). Ran the
  full suite once, then `AdminCatalogueSettingsEndpointTests` +
  `AdminGlobalSettingsLiveEffectEndpointTests` together three more times specifically hunting for the
  race described above — no failure observed in these four runs (timing-dependent; the HIGH finding
  above rests on the static collection analysis, not on reproducing a failure).

## Findings summary

- **[MEDIUM]** `AuditExportOptions.MaxRows` has no validation — `<= 0` fails closed only as an
  arithmetic accident (and 413s an empty-result export unnecessarily), a negative value is untested
  against the query pipeline, and a very large value both defeats the cap's purpose and risks
  `int` overflow in `maxRows + 1`. Add `ValidateOnStart` (or an `IValidateOptions<AuditExportOptions>`)
  requiring `MaxRows` in a sane positive range.
- **[HIGH]** The new `AdministrationGlobalSettingsCollection` isolates only its own file's tests.
  `AdminCatalogueSettingsEndpointTests.GetSettings_ReturnsEveryRegistryKeyWithBoundsAndEffectiveValue`
  (pre-existing, untouched by this diff) asserts the same shared global setting's ambient default
  value with no isolation and no capture/restore, in a different xUnit collection that can run
  concurrently with the new one. Real flakiness risk, not reproduced in four runs here but not
  disproven either. Fix by adding that test to the isolated collection or by giving it its own
  capture/restore.
- **[LOW]** U-D7's correction is faithful to the spec and the code, but nothing tests the
  `SetServicePriceCommandValidator` rejection of a negative `Amount` end-to-end — the correction's
  own cited enforcement point is itself unverified by any test.

Neither finding is a production security or data-loss issue, a layering violation, or a superseded-
architecture regression; both are fixable without touching a handover or `.ai/` doc. Not blocking
this pass, given the gate's own PASS bar in this repo (no BLOCKER/Critical found) — flagged for the
acceptance gate's and the next fix round's attention, same as prior passes' non-blocking findings.

SCOPE CHECK
- files in allowed scope: 18/18 (`docs/handover/HO-ADM-001.md`, `docs/test-cases/TC-HO-ADM-001.md`,
  `AdminAuditEntriesController.cs`, `AdministrationModule.cs`, `appsettings.json`,
  `AuditExportOptions.cs` (new), and 12 test files under `tests/**`)
- files outside allowed scope: none

---

## Sixth pass (superseded by the above)

GATE: CODE_REVIEW
HANDOVER: HO-ADM-001
COMMITS: `git diff c7dea08..649e8a0` (sixth pass, delta over the fifth pass's PASS at `c7dea08`;
commits feb1f32, 649e8a0)
DATE: 2026-09-26
STATUS: PASS

Sixth pass (delta c7dea08..649e8a0), scoped to this round's fix: `feb1f32` (the blank-`placement`
500 the fifth pass's own CODE_REVIEW and ARCHITECTURE passes both raised as a WARNING/MEDIUM 2, plus
a doc-comment correction on `ValidationRuleExtensions.cs`) and `649e8a0` (handover-doc bookkeeping,
Amended (37) and a correction to Amended (32)). Only four files changed: `AdministrationMapper.cs`,
`ValidationRuleExtensions.cs`, `AdminHardeningRegressionEndpointTests.cs`, `docs/handover/HO-ADM-001.md`.
Read the fifth-pass CODE_REVIEW verdict (below) and independently re-derived every claim rather than
taking it on trust.

## The fix

- `AdministrationMapper.ParseNullableEnum<T>` changed from `value is null ? null : ParseEnum<T>(value)`
  to `string.IsNullOrWhiteSpace(value) ? null : ParseEnum<T>(value)`. This is the minimal, correct
  fix: it makes blank/whitespace behave identically to absent, matching
  `EnumerationParsing.TryParse`'s own "blank = no value" contract that every other caller in this
  file already relies on. No behavioural change for a genuinely-present value (an inhabited string is
  never `IsNullOrWhiteSpace`, so it still reaches `ParseEnum<T>` unchanged).
- Grepped the full source tree for every `ParseEnum`/`ParseNullableEnum` call site (`grep -rn
  "ParseEnum\|ParseNullableEnum" --include=*.cs src`). `ParseNullableEnum` has exactly one call site
  pattern, both for `BannerPlacement` (`CreateContentItemCommand.Placement`,
  `EditContentDraftCommand.Placement`) — the only nullable enum field in Administration — so the fix
  covers its entire surface. `ParseEnum` (non-nullable) is called for `ClosureKind`, `RoomKind` (×2),
  `RosterExceptionKind`, `LeaveKind`, `ServiceCategory` (×2) and `ContentKind` — every one of these is
  a required field carrying both `.NotEmpty()` and `.MustBeValidEnumeration<>()` on its validator
  (confirmed in the fifth pass and re-confirmed here by reading each validator file unchanged in this
  diff). FluentValidation 8.5.1's `NotEmptyValidator` treats a whitespace-only string as empty
  (`string.IsNullOrWhiteSpace` internally), so a blank value on any of these fields 400s at the
  validator before `AdministrationMapper` ever runs — `ParseEnum` itself cannot receive a blank string
  through any reachable path today. The one unrelated `TryParseEnum<T>` in `CatsController.cs` is a
  different, private per-controller helper (HO-CAT-001's own code, out of this handover's scope) that
  does not call into `AdministrationMapper`'s private methods — not the same call site, and not
  touched by this diff. No other `ParseEnum`/`ParseNullableEnum` call site still throws on blank.

## Is a blank/absent placement on a Banner consistent with the handover?

Checked this directly rather than trusting the handover's own Amended (37) claim. `docs/handover/HO-ADM-001.md`
§8.8 (`ContentItemAggregate`) reads: "...and for banners `Placement` (`Home` | `Shop` | `Portal`)" —
this states which values `Placement` may take and that it is meaningful for the `Banner` kind, not
that it is mandatory for `Banner`. No sentence in §8.8, §11 (commands/handlers), §12.4 (API contract)
or §18 (acceptance criteria) states a "Banner requires Placement" invariant. Confirmed against the
actual domain code: `ContentItemAggregate.Placement` is `BannerPlacement?` (nullable), and neither
`Create` nor `EditDraft` (`src/Domain/CatEcommerce.Domain/Aggregates/Administration/ContentItemAggregate.cs`)
checks `Kind == ContentKind.Banner` before accepting a null `Placement` — there is no domain-level
"Banner must carry a Placement" rule to violate. Also checked `docs/SRS.md`: the only banner-related
requirement is `FR-ADM-7` ("content management capability for storefront pages, banners, FAQs and
policy documents"), which says nothing about placement being mandatory. Accepting a blank placement
for `Banner` (falling back to `null`, the same as an absent one) is therefore consistent with both the
handover and the SRS as written — this fix does not silently create a data-integrity gap against the
spec. (Whether a bannerless-placement `Banner` is *useful* — e.g. it would never be selected by
`GET /api/content/banners?placement=X` — is a product question the spec is simply silent on, not a
correctness defect in this fix; the fix's job was to stop the 500, not to invent a new mandatory-field
rule the spec never named, and Amended (37) says exactly this.)

## Tests

Read `AdminHardeningRegressionEndpointTests.cs`'s new addition in full (52 lines): two theories,
`BlankPlacementOnCreate_Returns201_NeverA500` and `BlankPlacementOnEdit_Returns204_NeverA500`, each
parameterised over `{"", " ", "\t"} × {Page, Banner}` (12 cases total). Traced each against the exact
pre-fix code at `c7dea08`: `ParseNullableEnum(" ")` (or `""`/`"\t"`) is not `null`, so it called
`ParseEnum<BannerPlacement>(" ")` → `EnumerationParsing.TryParse` correctly returns `(true, null)`
for a blank/whitespace input → `ParseEnum`'s guard `if (!TryParse(...) || result is null)` evaluates
`result is null` as true → throws `InvalidOperationException`, an unhandled 500 via
`SafeExceptionHandlerMiddleware`, for both `Page` and `Banner` (the mapper runs before any
`Kind`-specific branching, so `Page`'s blank placement threw identically to `Banner`'s). All 12 cases
would fail against `c7dea08` (`500 != 201` / `500 != 204`) and pass against `649e8a0`. Test design is
correct: covers create and edit, both `ContentKind` values that reach `ParseNullableEnum`, and all
three whitespace variants (`""`, `" "`, `"\t"`) that `IsNullOrWhiteSpace` treats identically —
adequate coverage for this fix's actual surface (there is no fourth variant worth adding; a `null`
placement, i.e. the field omitted from the JSON entirely, was already covered by pre-existing tests
and is unaffected by this change).

## Doc-comment fix

`ValidationRuleExtensions.cs`'s `NoControlCharacters` doc comment previously claimed (fifth-pass
wording) that the per-field rule is "a second line of defence behind the API-wide
`NulByteRejectionFilter`" and "also catches free text no filter reaches yet" for a NUL byte
specifically. Re-read `NulByteRejectionFilter` (an `IActionFilter`, runs in the MVC filter pipeline
before the action method, hence before FluentValidation, which runs inside the command-dispatch
pipeline the action method invokes) and confirmed the corrected comment's claim is right: for a NUL
byte specifically, the global filter always intercepts first, so the per-field rule never fires for
that character — it is not "defence in depth" for `\u0000`, it is dead code for that one character.
The corrected comment narrows the per-field rule's real remaining purpose to every other disallowed
C0 control character (BEL, ESC, VT, FF, etc.) that the filter does not scan for (confirmed:
`NulByteRejectionFilter.ContainsNulByte` checks only for `'\0'`, nothing else). This is an accurate
correction, not a behavioural change — no code path changed as a result of this comment edit.

## Docs accuracy

Read Amended (37) and the "ARCH LOW correction" paragraph appended to Amended (32) in
`docs/handover/HO-ADM-001.md` in full. Both match the actual diff precisely: Amended (37)'s
description of the bug (`value is null` vs. blank-but-non-null, the `InvalidOperationException`, the
fix, and the "no domain/validator rule requires Banner to carry Placement, inventing one is out of
scope" reasoning) is exactly what the code and the tests do. The ARCH LOW correction's description of
`NulByteRejectionFilter` always running first matches the code as traced above. No overstatement or
inconsistency found.

## Scope

All four changed files are within §13's allowed scope: `AdministrationMapper.cs`
(`src/Application/CatEcommerce.Application/DataMappers/AdministrationMapper.cs`, listed verbatim),
`ValidationRuleExtensions.cs` (under `CommandHandlers/Validators/{one per command}.cs` — this is the
shared rule file the per-command validators use, already in scope for the fifth pass's own edits to
it), `tests/CatEcommerce.IntegrationTests/**` (listed verbatim), and `docs/handover/HO-ADM-001.md`
(the handover doc's own self-amendment, the established pattern for every prior round). No file
outside §13 touched; no forbidden-scope (§14) pattern introduced.

## Findings

None. The fix is correct and minimal (a one-line predicate change plus an accurate doc-comment
correction), closes the exact gap the fifth pass flagged without introducing a new one, has adequate
regression coverage that would fail against the pre-fix commit, and the "blank placement on a Banner
is accepted" outcome is consistent with the handover and SRS as written (there is no requirement,
domain rule or acceptance criterion this contradicts).

SCOPE CHECK
- files in allowed scope: 4/4 (`AdministrationMapper.cs`, `ValidationRuleExtensions.cs`,
  `AdminHardeningRegressionEndpointTests.cs`, `docs/handover/HO-ADM-001.md`)
- files outside allowed scope: none

Build/test: `dotnet build` on a scratch `git archive` of `649e8a0` (built and deleted under
`.../scratchpad/cr6-adm`, a newly created, uniquely named folder; removed afterward) — 0 errors, 0
warnings. `CatEcommerce.Domain.Tests` 190/190, `CatEcommerce.Application.Tests` 68/68,
`CatEcommerce.Infrastructure.Tests` 28/28, all green. Did not run `CatEcommerce.IntegrationTests` per
instructions, but traced every new assertion against the pre-fix commit by hand (see above).

---

## Fifth pass (superseded by the above)

GATE: CODE_REVIEW
HANDOVER: HO-ADM-001
COMMITS: `git diff a311677..8495e32` (fifth pass, delta over the fourth pass's PASS at `a311677`;
commits 6dec5d0, 0c2a617, 8495e32; branch head has since advanced to `d80a82c`, ARCHITECTURE's own
fifth-pass gate-record commit only, no source change)
DATE: 2026-09-26
STATUS: PASS

Fifth pass, scoped to this round's "hardening round" commits: `6dec5d0` (systemic fixes — an
API-wide `NulByteRejectionFilter` replacing per-field NUL scans; request-body enum validation via a
new shared `EnumerationParsing.TryParse`, used by `EnumQueryParameter` (refactored), a new
`ValidationRuleExtensions.MustBeValidEnumeration<TCommand,TEnum>` rule on six command validators, and
`AdministrationMapper`'s `ParseEnum`/`ParseNullableEnum` replacing every direct
`Enumeration.FromDisplayName` call; `SlugFormat`/`CodeFormat` anchored `\A…\z` instead of `^…$`;
`PagingRules.MaxPageSize` closing the leave-requests `pageSize` half of the `Skip`-overflow gap the
fourth pass's own CODE_REVIEW flagged as WARNING), `0c2a617` (the matching regression tests, one new
file `AdminHardeningRegressionEndpointTests.cs` plus a justified rename/assertion change in
`AdminContentPolicyEndpointTests.cs`), and `8495e32` (handover-doc bookkeeping, Amended (32)-(36)).
Built the full solution and ran the three unit-test projects from a scratch `git archive` of
`8495e32` (not the worktree, no git-ignored appsettings needed): `dotnet build` 0 errors, 0 code
warnings; `CatEcommerce.Domain.Tests` 190/190, `CatEcommerce.Application.Tests` 68/68,
`CatEcommerce.Infrastructure.Tests` 28/28 — all green. Scratch folder deleted after. Did not run
`CatEcommerce.IntegrationTests` per instructions, but read every changed/added integration-test
assertion in full and traced it against the exact pre-fix code at `a020bcb` (the commit before this
round) to confirm each would fail there.

### This round's fixes, verified

- **`NulByteRejectionFilter` (new).** Read the full reflective walk in
  `Api/Filters/NulByteRejectionFilter.cs`. Correctness: strings checked directly; `IEnumerable`
  recursed item-by-item (covers `IReadOnlyList<ShiftSlotRequest>` etc.); recursion into a value's own
  properties gated to types whose namespace starts `CatEcommerce` (never reflects into framework
  types a controller action can also receive, e.g. `HttpContext`); indexer properties skipped
  (`GetIndexParameters().Length > 0`); a throwing getter is caught and skipped, not propagated;
  `MaxDepth` (8) bounds recursion — no explicit cycle/visited-set tracking, but the depth bound makes
  one unnecessary (a genuine reference cycle just stops at depth 8 rather than stack-overflowing).
  Traced the actual bound-argument types for every case the new regression tests exercise
  (`CreateContentItemRequest`, `ShiftSlotRequest`, etc., all in `CatEcommerce.Api.Contracts.*` or
  `CatEcommerce.Application.Commands.*`) — all plain string/Guid/primitive properties or
  `CatEcommerce`-namespaced nested types, so the walk reaches every field the tests send a NUL byte
  in. One theoretical gap, not currently reachable: a `Dictionary<TKey,TValue>`/`KeyValuePair<,>`
  property would not be recursed into (`KeyValuePair<,>` is a `System` type, not `CatEcommerce`, and
  isn't a `string`), so a NUL inside a dictionary's keys/values would slip past the filter — no
  Administration command currently has a dictionary-typed field, so this doesn't affect this round;
  flagged as a Suggestion below for whoever adds the first one. Registration
  (`AdministrationModule.cs`, global `MvcOptions.Filters.Add<T>()`) and the resulting effect on other
  areas (Accounts sign-in 401→400 on a NUL, NUL-before-CAPTCHA ordering) is a placement/layering
  question that ARCHITECTURE owns (its fifth pass already flagged MEDIUM 1, recommending the
  integrator move the registration to `Program.cs`); not re-litigated here.
- **`EnumerationParsing`/`MustBeValidEnumeration`/`AdministrationMapper.ParseEnum`+`ParseNullableEnum`.**
  Traced all six command validators changed (`ContentCommandValidators`, `RoomCommandValidators`,
  `BranchClosureCommandValidators`, `RosterCommandValidators`, `ClinicServiceCommandValidators`,
  `LeaveCommandValidators`) and confirmed each required enum field (`Kind`, `Category`,
  `RequiredRoomKind`) now carries both `.NotEmpty()` and `.MustBeValidEnumeration<>()`, so an
  unrecognised or wrongly-cased value 400s before `AdministrationMapper` ever runs, and a valid
  case-variant (`"page"`, `"consultation"`, `"annual"`) succeeds instead of throwing. Confirmed the
  now-shared `EnumQueryParameter.TryParse` delegation is behaviourally identical to its inlined
  predecessor (case-insensitive `DisplayName` match, blank/absent = valid no-op).
  **Found one confirmed bug, already independently caught by this round's ARCHITECTURE gate as
  MEDIUM 2 and reproduced here from the code, not taken on trust:** `BannerPlacement` is the one
  enum field that is genuinely optional (`Placement` has no `.NotEmpty()`, only
  `.MustBeValidEnumeration<>()`) on both `CreateContentItemCommand` and `EditContentDraftCommand`.
  `EnumerationParsing.TryParse` treats `IsNullOrWhiteSpace` as "valid, no value" and returns
  `(true, null)` — so `MustBeValidEnumeration` passes for `""` or `" "` and the request reaches
  `AdministrationMapper`. There, `ParseNullableEnum<T>(string? value) => value is null ? null :
  ParseEnum<T>(value)` only short-circuits on `null`, not blank/whitespace, so `ParseEnum("")` runs:
  `TryParse` returns `(true, null)`, and `ParseEnum`'s guard is `if (!TryParse(...) || result is
  null)` — `result is null` is true, so it throws `InvalidOperationException`, an unhandled 500. A
  form's "no placement" option submitting `""` rather than omitting the field is a plausible, entirely
  ordinary way to trigger this — not a crafted attack. This is exactly the defect class Amended (33)
  and this file's own fourth-pass history exist to close, left open on the one field that is
  legitimately nullable. See WARNING below; not a new defect (`FromDisplayName("")` also threw before
  this round), so it is carried forward rather than introduced, and — same reasoning as the fourth
  pass's own accepted `pageSize` WARNING — it is staff-only-reachable, self-inflicted, and not a
  security or data-loss risk, so it does not block this pass.
- **`SlugFormat`/`CodeFormat` `\A…\z` anchors.** Confirmed the specific .NET regex behaviour the doc
  comments claim: `$` (unlike `\z`) also matches immediately before a trailing `\n`, so
  `"about\n"` passed the old `^…$` pattern. `\A…\z` matches only true start/end. Both patterns'
  character classes are otherwise unchanged, so no existing valid slug/code regresses.
- **`PagingRules.MaxPageSize` (100).** Closes exactly the gap the fourth pass's own CODE_REVIEW
  WARNING named: `AdminLeaveRequestsController.Get` now rejects `pageSize > 100` the same way it
  already rejected `pageNumber > 1_000_000`, before either reaches
  `Query<T>.Skip = (PageNumber-1)*PageSize`'s unchecked `int` arithmetic. Confirmed the audit
  search's own higher clamp-based cap (`AuditExportRules.MaxSearchPageSize`, 200) is untouched and
  deliberately left as a separate, pre-existing policy — consistent with the doc comment's claim.
- **Regression tests (`0c2a617`).** Read `AdminHardeningRegressionEndpointTests.cs` in full (361
  lines, four theories + two facts). For each case, checked the exact validator/mapper/controller
  code as it existed at `a020bcb` (the pre-round commit) to confirm the assertion would not have held
  there:
  - NUL-byte cases (leave note, leave decisionNote, roster-exception reason, branch-closure reason,
    branch phoneNumber, content linkUrl): none of these six fields carried `.NoControlCharacters()`
    at `a020bcb` (verified by diffing each validator file) and none was covered by the (not yet
    existing) global filter, so each would have reached its aggregate/database unfiltered. All six
    are legitimate, previously-unclosed gaps.
  - Unknown-enum-in-body cases (content kind, banner placement, room kind, closure kind, service
    category, leave kind): at `a020bcb` none of these validators had `MustBeValidEnumeration`, and
    `AdministrationMapper` called `Enumeration.FromDisplayName` directly, which throws on no match —
    an unhandled exception reaching `SafeExceptionHandlerMiddleware` as a 500, not the 400 the test
    now asserts. Confirmed `SafeExceptionHandlerMiddleware` is the app's generic unhandled-exception
    handler (`Api/Hosting/SafeExceptionHandlerMiddleware.cs`, wired in `Program.cs`), so this
    reasoning holds.
  - Lower-case-enum-in-body cases: same six fields, `FromDisplayName` is case-sensitive, so
    `"page"`/`"consultation"`/`"annual"`/etc. would also have thrown at `a020bcb` — the test now
    correctly expects `201 Created`.
  - Trailing-newline-in-formatted-field cases (policy key, content slug, branch code, tax-class
    code): at `a020bcb` both regexes used `^…$`, which (per the .NET behaviour above) accepted a
    trailing `\n` — the test's `201`-would-have-been-wrong assumption is correct; now correctly
    expects `400`.
  - `pageSize` cases: at `a020bcb`, `AdminLeaveRequestsController.Get` capped only `pageNumber`.
    `pageSize=2147483647` alone (with default `pageNumber`) does not itself overflow `Skip`
    (`Skip=0`), so at `a020bcb` this specific request most likely returned `200`, not `500` — the
    test's name ("Never500") slightly overstates the old failure mode, but the test still correctly
    fails against `a020bcb` (`200 != 400`) and is a legitimate regression guard against an unbounded
    `pageSize`. The second case (`pageNumber=3, pageSize=1_500_000_000`) does overflow `Skip` negative
    (`3,000,000,000` wraps in 32-bit `int`), which reaches Postgres as a negative `OFFSET` — Postgres
    rejects a negative `OFFSET` at the protocol level, an uncaught 500 at `a020bcb`. Both tests
    correctly fail against the pre-fix code and pass against `8495e32`.
- **`ContentWithNulByte` rename (`AdminContentPolicyEndpointTests.cs`).** Renamed
  `ContentWithNulByte_Returns400InvalidContentMarkup` → `ContentWithNulByte_Returns400_ViaGlobalNulGuard`,
  asserting `ContainsControlCharacter` instead of `InvalidContentMarkup`. Justified: `draftBody` is a
  plain `string` on `CreateContentItemRequest` (`CatEcommerce.Api.Contracts.Administration`
  namespace), so the new global `NulByteRejectionFilter` (an `IActionFilter`, runs before the action
  method — and therefore before the command dispatch pipeline that runs `LimitedMarkup`'s own NUL
  check) now intercepts first, short-circuiting with its own generic error code before
  `ContentItemAggregate.Create`'s markup validation ever runs. `LimitedMarkup`'s own NUL check is
  retained and unreachable only for this one input shape, still proven live for every other
  disallowed-markup case by the untouched `ContentWithScriptTag_Returns400InvalidContentMarkup`
  alongside it. The handover doc (Amended (32)) states the same reasoning and flags the same-shaped
  carried item on HO-ACC-001 for that handover to confirm — appropriately, not verified here.
- **Dead code removed.** `ValidationRuleExtensions.ContainsDisallowedControlCharacter` deleted;
  grepped the whole tree for it — zero remaining callers (the only other
  `ContainsDisallowedControlCharacter` is `Api/Controllers/ControlCharacterGuard.cs`'s own unrelated
  method of the same name, delegating to the same shared `ControlCharacters.ContainsDisallowed`,
  untouched by this diff).
- **No superseded-architecture patterns.** Grepped the full diff for every name on the "Critical"
  list (`IBusMessageDispatcher`, `AddServiceBusProvider`, `EventPublishOption`,
  `[BsonIgnoreExtraElements]`, `IReadRepository`, `ViewModelBase`, `CustomQueueConfig`, a rejection
  event, a `CommandServices` class, `CommandWorker`/`EventWorker`, MassTransit/RabbitMQ/MongoDB): none
  appears. No controller in this diff injects a repository/`DbContext`/aggregate directly; no handler
  sets aggregate state outside a mutation method; no new validator is registered as a class rather
  than `IValidator<TCommand>` (all six changed validators keep their existing
  `AbstractValidator<TCommand>` shape and existing DI registration, unchanged by this diff).

### New finding (that pass)

- **[WARNING]** `src/Application/CatEcommerce.Application/DataMappers/AdministrationMapper.cs`
  (`ParseNullableEnum`, near the bottom of the file) and
  `src/Application/CatEcommerce.Application/CommandHandlers/Validators/ContentCommandValidators.cs`
  (`Placement` rules on both validators). Confirmed independently (see above) and also caught by this
  round's ARCHITECTURE gate as its MEDIUM 2: `"placement": ""` or `"placement": " "` on
  `POST /api/admin/content` or `PUT /api/admin/content/{id}` passes validation (blank is
  "valid, no filter" per `EnumerationParsing.TryParse`, and `Placement` has no `.NotEmpty()` because
  it is legitimately optional) but then throws `InvalidOperationException` inside
  `AdministrationMapper.ParseEnum`, because `ParseNullableEnum` only treats `null` — not
  blank/whitespace — as "absent". This is an unhandled 500, not the 400 this round's own commit
  message and Amended (33) describe closing for every enum-typed command field; it is the one field
  of the six where "absent" and "blank" are different code paths and only one of them was closed. Not
  newly introduced by this diff (`Enumeration.FromDisplayName("")` also threw before it), and every
  other five enum fields are safe because they are `.NotEmpty()` as well as
  `.MustBeValidEnumeration<>()`, so a blank value is rejected by `NotEmpty` before the mapper ever
  runs — `Placement` is the only field where that safety net is (correctly, since it is optional)
  absent, and the blank case falls through both the validator and the mapper's own null-check. Fix:
  either have `ParseNullableEnum` short-circuit on `string.IsNullOrWhiteSpace` rather than `is null`,
  or have `EnumerationParsing.TryParse`'s blank-input contract distinguish "absent" from a value that
  is present-but-blank so the mapper can reject the latter with a `ValidationResponse` instead of
  throwing. Low impact (staff-only endpoint, self-inflicted 500, no data-loss or authorization
  exposure — same category as the fourth pass's own accepted `pageSize` WARNING, which this round
  fixed) — does not block this pass, but should not carry to a sixth round given this round's stated
  purpose was closing exactly this class of bug. **Closed in the sixth pass above.**
- **[SUGGESTION]** `src/Api/CatEcommerce.Api/Filters/NulByteRejectionFilter.cs`, `ContainsNulByte`.
  A `Dictionary<TKey,TValue>`/`KeyValuePair<,>`-typed property would silently bypass the walk (see
  above) — no current Administration command has one, so this is not reachable today, but worth a
  `KeyValuePair<,>` special case (or checking `IDictionary` before falling through to the generic
  `IEnumerable` branch) the first time one is added, so the gap isn't rediscovered the same
  one-field-at-a-time way the NUL scan itself was.
- **[SUGGESTION]** Same file. `GetProperties(BindingFlags.Public | BindingFlags.Instance)` is called
  fresh, uncached, on every request for every controller action in the host (the filter is global).
  For Administration's request sizes and traffic this is immaterial; if this pattern is reused for a
  higher-traffic area, a per-type cached property list would avoid repeated reflection cost.

Both suggestions and the one WARNING do not block merge.

SCOPE CHECK (fifth pass)
- Files in `git diff a311677..8495e32` reviewed closely: all 21 changed files — the new filter, all
  seven changed validator files, the two Shared helpers (`SlugFormat`, `CodeFormat`), the new
  `EnumerationParsing`, `AdministrationMapper`'s enum-parsing rework, `PagingRules`,
  `BusinessViolationCodes.Administration.cs`'s new code, `AdministrationModule.cs`'s filter
  registration, `AdminLeaveRequestsController`'s new cap, `EnumQueryParameter`'s refactor, both test
  files (one new, one edited), and the handover doc's actual text (Amended (32)-(36)), not just its
  diffstat.
- Specifically re-derived per that pass's brief: read the reflective walk for correctness (cycles,
  indexers, throwing getters, nullable handling — see findings above); traced every regression test's
  assertion against the exact pre-round code at `a020bcb` to confirm each would fail there;
  independently reproduced (not merely cited) the ARCHITECTURE-flagged blank-`placement` 500 by
  reading `EnumerationParsing.TryParse`, `ParseNullableEnum` and `ParseEnum` together line by line.
- Ran (in a scratch `git archive` of `8495e32`, deleted after): `dotnet build` (0 errors, 0 code
  warnings), `CatEcommerce.Domain.Tests` (190/190), `CatEcommerce.Application.Tests` (68/68),
  `CatEcommerce.Infrastructure.Tests` (28/28). Did not run `CatEcommerce.IntegrationTests` per
  instructions.
- Files outside this handover's declared scope: none found in this range (matches ARCHITECTURE's
  independent scope check for the same range).

---

## Fourth pass (superseded by the above)

GATE: CODE_REVIEW
HANDOVER: HO-ADM-001
COMMITS: `git diff 4463196..a020bcb`, worktree head `a020bcb`
DATE: 2026-09-26
STATUS: PASS

Verified policy authorization moved to `[RequireAction(Actions.Policies.Manage)]`, `SlugFormat`/
`CodeFormat` introduced (`^…$`, not yet `\A…\z`) with no regression on existing codes/slugs, and the
three duplicate control-character scans consolidated into `Shared.Common.Helpers.ControlCharacters`.
Ran `dotnet build` (0 errors) plus the three unit-test projects (190/68/28, all green) from a scratch
archive. One new WARNING raised: `AdminLeaveRequestsController` capped `pageNumber` but not
`pageSize`, so `pageSize=1_500_000_000` with a small `pageNumber` still overflowed `Skip` negative —
fully closed in the fifth pass above (`PagingRules.MaxPageSize`). Full findings text: see version
control history of this file.

## Third pass and earlier (superseded)

- **Third pass** (`git diff 72d92f0..4463196`, PASS): clinical-audit registry, `EnumQueryParameter`
  case-insensitivity, NUL/control-character hardening on six field groups, reserved-policy-key
  enforcement, concurrent-acceptance idempotency. One LOW raised (NUL-hardening scoped too narrowly)
  — closed in the fourth pass.
- **Second pass** (`git diff 08e1dd2..04195cc`, PASS): confirmed all first-pass findings fixed. Two
  new LOWs raised — both fixed in the third pass.
- **First pass** (full branch diff at `7f5c284`, FAIL): BLOCKER (`BranchCalendar.OpenIntervals`
  dropped a second same-day partial closure), HIGH (pervasive read-side rehydration), two MEDIUM, four
  LOW. All fixed in the second pass.

Full findings text for all three: see version control history of this file.
