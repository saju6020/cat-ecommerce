GATE: CODE_REVIEW
HANDOVER: INTEGRATOR-HOST
COMMITS (this pass): 1ed7493 337c19e (delta 0421d4f..337c19e, branch `ho/integrator-host`)
DATE: 2026-09-26
STATUS: PASS

SECOND PASS (delta 0421d4f..337c19e)

SUMMARY

Two commits closing the first pass's own carried findings:

1. `1ed7493` — adds a deterministic `ORDER BY` to
   `GetDuplicateCustomerGroupsQueryHandler` (first pass's MEDIUM finding): groups are now ordered
   by each group's newest member's registration time descending (read off the earliest
   `audit.audit_entry` `DataChange` row for that `CustomerAccountAggregate`, since customer
   accounts carry no `CreatedOn` column), tiebroken ascending by the group's smallest member id.
   Adds `GetDuplicates_NewerGroupRanksAheadOfOlderGroup_AndPagingIsStableAcrossTwoPages`.
2. `337c19e` — corrects the "HO-ADM-001-ARCHITECTURE MEDIUM 1" mis-citation (first pass's LOW
   finding) in `StaffAccountsController.cs`, `StaffCustomersController.cs` and
   `StaffEndpointTests.cs` to the actual source, `HO-ADM-001-SECURITY`'s carried line.

DESIGN QUESTION: JOINING THE AUDIT TRAIL FOR A CREATION TIMESTAMP

The task asked me to specifically judge this choice rather than take it at face value. Findings:

- **Read-layer coupling to `audit.audit_entry` is not a convention violation.**
  `.ai/read-side-pattern.md` §5 states a query handler may join across aggregates/tables — "it is
  one database and one solution, and the write-side aggregate boundary does not constrain a read"
  — and only asks for a database view "for heavy reports" with "a large LINQ join". This is neither:
  the audit lookup is a single indexed `GROUP BY EntityId` restricted to the already-computed
  duplicate-candidate set, not an ad hoc report.
- **Query cost is bounded and indexed, not a table scan.** The filter is
  `EntityType == nameof(CustomerAccountAggregate) && memberIds.Contains(a.EntityId)`, i.e. an
  equality lookup on a fixed, typically-small candidate set (only customers already known to share
  an identifier — the same set the rest of the handler already materializes). `AuditEntryConfiguration.cs:43`
  has `HasIndex(x => new { x.EntityType, x.EntityId, x.OccurredOn })`, a composite index that
  covers exactly this predicate plus the `Min(OccurredOn)` aggregate per id — Postgres can serve
  it without touching audit rows for any other entity type or any non-candidate customer. Cost
  scales with the number of duplicate-candidate members, not with `audit_entry`'s total size.
- **Correctness under retention/erasure does not degenerate.** I read
  `20260924212111_AddAccounts.cs` (lines 458-472): `audit.audit_entry` has
  `BEFORE UPDATE OR DELETE` (row-level) and `BEFORE TRUNCATE` (statement-level) triggers that
  unconditionally raise on any attempt to touch an existing row — no session-role exception, no
  retention carve-out. `docs/handover/HO-XCT-001.md` (not yet landed, but its own design was
  checked) confirms this is permanent by architecture, not an oversight it intends to fix: F33
  says future retention work must **not** add a second trigger and must accept these triggers as
  the protection for that table; the classification table (line 641) lists
  `audit.audit_entry` as `Forbidden` for hard delete, distinctly from the `Purgeable`/
  `AggregateOnly` categories that `IRetentionPurgeSource` and the personal-data eraser act on. The
  one landed eraser (`AdministrationPersonalDataEraser`) only redacts an IP address on
  `PolicyAcceptanceAggregate`; nothing anywhere redacts or deletes `audit_entry` rows. Since
  `AuditChangeCaptureInterceptor` (checked in full) fires for every aggregate root
  `Added`/`Modified` unconditionally, in the same transaction, with no opt-out, the "earliest
  `DataChange` row for this customer" the query relies on cannot silently disappear or move under
  the currently-planned architecture. The concern the task asked me to check for (retention/erasure
  purging or anonymising audit rows and silently changing the order) does not apply here — it's
  foreclosed by a documented, DB-enforced invariant, not merely assumed.
- **Would ordering by a stable member id alone be simpler and sufficient?** For "deterministic,
  stable paging" taken narrowly, yes. But I checked how customer ids are generated
  (`Guid.NewGuid()`, not sequential/time-ordered — grepped `CustomerAccountAggregate.cs` and the
  registration handler), which matters for the *actual* problem the first pass's MEDIUM finding
  named: `catecommerce_dev`/the shared test DB is never truncated (§16.1) and only grows. Under a
  random-GUID id-ordering, a freshly created group's rank among however many groups have
  accumulated over months of runs is effectively uniform-random — as the total exceeds the page
  cap (100), a new group has a rising, unbounded chance of landing off page 1, which is exactly
  the flakiness the first pass's MEDIUM was raised to fix, not a resolution of it. Recency-based
  ordering does not have that failure mode: a row created "now" has an `OccurredOn` later than
  every row from every past run, by construction, so it always sorts at or near the front
  regardless of how large the historical backlog grows — it is only sensitive to genuinely
  concurrent duplicate-group creation within the same narrow time window, a small, bounded number,
  not the accumulated total. I reran
  `DuplicateCustomerGroupsEndpointTests` three consecutive times against the same live shared DB
  (each run adds more rows permanently) and it passed all three times with no adjustment needed,
  which is the property recency-ordering is supposed to buy. **Recommendation: keep** the
  audit-based ordering — it is the one of the two options the first pass itself suggested that
  actually solves the stated growth problem, not simplification for its own sake.

Two low-severity items worth a follow-up, not a blocker:

- [LOW] `GetDuplicateCustomerGroupsQueryHandler.cs:99-107` — the comment cites ADR-040 for "no
  schema change needed" but not *why* the ordering signal is safe to rely on forever (the
  `audit.audit_entry` hard-delete-forbidden trigger, HO-XCT-001 F33/D-5). Worth a one-line pointer
  so whoever eventually builds HO-XCT-001's retention worker sees, from this file, that a Read
  handler's correctness — not just audit-trail integrity in the abstract — depends on that table
  staying append-only forever.
- [LOW] The new test's assertion that the newer group ranks ahead of the older one reads correctly
  today, but relies on no other concurrently-running test creating >100 of its own duplicate
  groups inside the same narrow window (bounded by test parallelism, not historical volume — see
  above). Not a real risk at current suite size/parallelism; flagged only so a future large jump in
  parallel duplicate-group-creating tests doesn't reintroduce flakiness this fix was meant to
  remove.

BUILD + TEST (scratch, this pass)

`git archive 337c19e` into a fresh scratch folder (not the branch worktree), `CATECOMMERCE_TEST_DB`
/ `ConnectionStrings__CatEcommerce` = `Host=localhost;Port=5432;Database=catecommerce_host;
Username=postgres;Password=postgres`:

- `dotnet build CatEcommerce.sln -c Release`: 0 errors, 0 warnings.
- `dotnet ef migrations list` / `dotnet ef database update`: all four migrations already applied;
  "already up to date".
- `dotnet test CatEcommerce.sln -c Release --no-build`: **645/645 passing** — Domain 221,
  Application 73, Infrastructure 28, Integration 323 (322 + the new
  `GetDuplicates_NewerGroupRanksAheadOfOlderGroup_AndPagingIsStableAcrossTwoPages`).
- Reran `DuplicateCustomerGroupsEndpointTests` alone three more times against the same live shared
  DB (state accumulating across runs, as it does in normal use): 11/11 passing each time — no
  flakiness observed.
- Scratch folder deleted after the run.

SCOPE CHECK (this pass)
- files in allowed scope: 4/4 (`GetDuplicateCustomerGroupsQueryHandler.cs`,
  `DuplicateCustomerGroupsEndpointTests.cs`, `StaffAccountsController.cs`,
  `StaffCustomersController.cs`, `StaffEndpointTests.cs` — all either the query handler the
  carried MEDIUM named or comment/citation fixes to the carried LOW; the integrator is the actor
  authorised to close its own gate's carried findings).
- files outside allowed scope: none.

---

FIRST PASS (original)

GATE: CODE_REVIEW
HANDOVER: INTEGRATOR-HOST
COMMITS: e55e5ee b21865f 99861dd 79376c4 (branch `ho/integrator-host`, diff `master(5200d75)..ho/integrator-host`)
DATE: 2026-09-26
STATUS: PASS

SUMMARY

Four small, integrator-authorised, post-merge cleanups on top of `master`:

1. `e55e5ee` — moves `NulByteRejectionFilter` (from `AdministrationModule`) and
   `UniqueConstraintExceptionFilter` (from `AccountsModule`) into `Program.cs`'s own
   `AddControllers(...)` call, alongside `ConcurrencyExceptionFilter`, and moves
   `BusinessViolationCodes.ContainsControlCharacter` from
   `BusinessViolationCodes.Administration.cs` into the base `BusinessViolationCodes.cs`. This is
   exactly the fix `docs/gates/HO-ADM-001-ARCHITECTURE.md` MEDIUM 1 (and its Question 1)
   prescribed for the integrator to do at merge.
2. `b21865f` — adds a test proving `PUT /api/customers/me` with a NUL byte in `fullName` now
   returns 400 (closing HO-ACC-001's own carried item, its §1 Amended (21)).
3. `99861dd` — caps `pageNumber`/`pageSize` on `GET /api/staff-accounts` and
   `GET /api/staff/customers/duplicates` with the shared `PagingRules`, the same way
   `AdminLeaveRequestsController` already does, closing the `Skip` integer-overflow gap
   `docs/gates/HO-ADM-001-SECURITY.md` carried forward ("HO-ACC-001: `GET
   /api/staff-accounts?pageNumber=2147483647` → 500"). Two pre-existing
   `DuplicateCustomerGroupsEndpointTests` cases have their `pageSize=1000` lowered to the new cap
   (100).
4. `79376c4` — fixes three stale `AccountOptions.StaffIdleTimeoutMinutes` comments in
   `StaffSessionEndpointTests.cs` that predate the DB-backed settings registry.

Both `HO-ADM-001.md` and `HO-ACC-001.md` get accurate Amended rows recording all of this, cross-
referencing each other.

WHAT WAS CHECKED

- **Filter registration correctness.** All three filters (`ConcurrencyExceptionFilter`,
  `UniqueConstraintExceptionFilter`, `NulByteRejectionFilter`) are each registered exactly once,
  only from `Program.cs` (grepped the whole `src/` tree for `Filters.Add<` — no other call site).
  `ConcurrencyExceptionFilter`/`UniqueConstraintExceptionFilter` are `IExceptionFilter`;
  `NulByteRejectionFilter` is `IActionFilter` (`OnActionExecuting`, runs pre-action, short-
  circuiting with `context.Result` on a NUL hit). Exception filters only run on an unhandled
  exception from the action; the action filter runs regardless, before the action. The two
  exception filters target non-overlapping conditions
  (`DbUpdateConcurrencyException` vs. `DbUpdateException{InnerException: PostgresException}` with
  a `23505`/`23P01` `SqlState` — a concurrency-token mismatch does not carry a `PostgresException`
  inner exception). Registration order among the three therefore has no behavioural effect, and
  the claim in the new `Program.cs` comment ("Order is unchanged... each still runs as an
  ordinary, unordered `MvcOptions.Filters` entry") holds.
- **Nothing else depended on the removed per-module registrations.** This is a single-host
  application: `Program.cs` calls all 18 `Add<Area>Module(...)` extensions unconditionally in one
  fluent chain (lines 122–139+), and every integration test factory
  (`AccountsApiFactory`/`AdministrationApiFactory`/etc.) is a `WebApplicationFactory<Program>` —
  there is no test or factory anywhere that builds a host from only one module. Full suite run
  confirms this (see below).
- **Paging coverage.** Read `StaffAccountsController` and `StaffCustomersController` in full:
  each has exactly one paged `[HttpGet]` action (`GetStaffAccounts`, `GetDuplicates`), and both
  are the ones fixed. `GetStaffAccounts`'s `search`/`roleId`, `StaffCustomersController.Find`
  (`mobile`/`email`), `GetStaffAccount`/`GetCustomer` (single-resource `GET`s) and the `POST`/`PUT`
  actions take no `pageNumber`/`pageSize`. No missed paged endpoint in either controller.
- **400 body shape.** `{ error = $"pageNumber cannot exceed {PagingRules.MaxPageNumber}." }` /
  same for `pageSize` — byte-for-byte the same anonymous-object shape
  `AdminLeaveRequestsController.cs:27,30` and `AdminAuditEntriesController.cs:44` already use.
- **`pageSize=1000` → `100` test change.** See Warning 1 below — this is the one real risk found.
- **ADR-341/342 conformance.** ADR-341 point 4 puts an area's own registrations in its module;
  ADR-342's second amendment puts cross-area pipeline/MVC behaviour in host composition once. A
  global `MvcOptions` filter that acts on every controller (not opt-in like
  `MaintenanceModeFilter`) is exactly the case ADR-342 reserves for `Program.cs`, and this move is
  the corresponding consolidation `HO-ADM-001-ARCHITECTURE.md`'s MEDIUM 1 named. No new `.ai/`
  entry is needed for a registration-location move that doesn't introduce a new pattern (the
  host-wide-concern-lives-in-Program.cs precedent already exists for CORS/forwarded-headers, ADR-
  342 owner decision B).
- **Docs.** `HO-ACC-001.md` §1 Amended (21) and `HO-ADM-001.md` §1 Amended (50) both describe the
  filter move, the code move and the NUL-fullName fix accurately and consistently with the actual
  diff. The claim "the web client (`web/lib/api`) has no client yet for either endpoint" (commit
  `99861dd`'s message) was checked and holds (`grep` for both routes under `web/lib/api` returns
  nothing).
- **`git merge-tree` against sibling branches** (informational, not a defect in this diff):
  - `ho/integrator-host` × `ho/CAT-001-gates`: clean, no conflicts.
  - `ho/integrator-host` × `ho/ACC-002`: one conflict, in `docs/handover/HO-ACC-001.md` — both
    branches append their own clause to the end of the same single-line `Amended` table cell.
    Purely textual (concatenation of two independent appended sentences), not a logical/code
    conflict. `appsettings.json` and
    `tests/CatEcommerce.IntegrationTests/Endpoints/DuplicateCustomerGroupsEndpointTests.cs` (both
    touched by `ho/ACC-002` too) auto-merge cleanly with no markers. Flagged for whoever performs
    that merge; no action needed on this branch.
- **Build and full test run**, from a clean `git archive ho/integrator-host` in a new scratch
  folder (no git-ignored `appsettings.*` present — only the tracked `appsettings.json`),
  `CATECOMMERCE_TEST_DB`/`ConnectionStrings__CatEcommerce` pointed at
  `Host=localhost;Port=5432;Database=catecommerce_host;Username=postgres;Password=postgres`:
  - `dotnet build CatEcommerce.sln -c Release`: 0 errors, 0 warnings.
  - `dotnet ef migrations list`: all four migrations already applied to that DB
    (`AddCatProfile`, `AddAccounts`, `AddSessionCsrfTokenHash`, `AddAdministration`); `database
    update` reported "already up to date".
  - `dotnet test CatEcommerce.sln -c Release --no-build`: **644/644 passing** — Domain 221,
    Application 73, Infrastructure 28, Integration 322 (includes the new
    `ProfileEndpointTests.UpdateProfile_FullNameWithNulByte_Returns400`, the six new paging-
    overflow tests across `StaffEndpointTests`/`DuplicateCustomerGroupsEndpointTests`, and every
    pre-existing cross-area NUL/authorization test). Scratch folder deleted afterward.

FINDINGS

- [MEDIUM] `tests/CatEcommerce.IntegrationTests/Endpoints/DuplicateCustomerGroupsEndpointTests.cs`
  `TwoCustomers_SharingAnAdditionalNumber_AppearInTheSameDuplicateGroup` and
  `..._AreBothLabelledAdditionalNotPrimary` — lowering `pageSize=1000` to `100` is the correct
  response to the new cap, but it narrows, rather than removes, the pre-existing risk the tests'
  own comment names ("`catecommerce_dev` is shared and never cleaned between test runs"). Neither
  `GetDuplicateCustomerGroupsQueryHandler`'s customer query (`GetDuplicateCustomerGroupsQueryHandler.cs:39-48`)
  nor its final `groups.Skip(query.Skip).Take(query.Take)` (line 93) has an explicit `ORDER BY` —
  Postgres does not guarantee row order without one, and group order is whatever order the
  unordered EF query happens to return. At `pageSize=1000` this was very unlikely to bite; at the
  new cap of 100, once the shared dev database accumulates more than 100 duplicate groups across
  runs, a test's own freshly-created group is no longer guaranteed to land on page 1, and these
  two tests become flaky for a reason unrelated to the logic they test. This is also a latent
  production correctness gap independent of this diff (unordered paging can return a row twice
  across pages, or skip one, as rows are inserted between requests) that this diff does not
  introduce but does make more likely to surface soon in the shared dev DB. **Suggested fix:** add
  a deterministic `OrderByDescending` to the query (e.g. by each group's most-recently-created
  member) so a newly created group is always sorted toward page 1 regardless of how many older
  groups exist, or teach the test to iterate pages until it finds its own group instead of
  assuming page 1. Not a blocker for this diff — the cap itself is correct and closes a real 500 —
  but worth fixing before the shared dev DB actually crosses 100 duplicate groups.
  **[CLOSED second pass, 1ed7493]** — see above.
- [LOW] `src/Api/CatEcommerce.Api/Controllers/StaffAccountsController.cs:22`,
  `StaffCustomersController.cs:56`, and commit `99861dd`'s message — all cite "HO-ADM-001-
  ARCHITECTURE MEDIUM 1" as the source of the paging-cap fix. `docs/gates/HO-ADM-001-
  ARCHITECTURE.md`'s actual MEDIUM 1 is about the `NulByteRejectionFilter`/
  `UniqueConstraintExceptionFilter` registration location (correctly cited by `e55e5ee`), not
  paging. The tracked item for the `staff-accounts` overflow is
  `docs/gates/HO-ADM-001-SECURITY.md`'s "CARRIED (not findings against this branch)" line: "HO-
  ACC-001: `GET /api/staff-accounts?pageNumber=2147483647` → 500 (the Skip overflow)." The
  `staff/customers/duplicates` half of the fix has no prior tracked citation at all — this
  integrator pass appears to be the first to name it. Purely a citation mix-up (the fix itself,
  the cap values and the test coverage are all correct); worth correcting the two code comments
  and the commit trailer's reference the next time either file is touched, so someone tracing gate
  history later isn't sent to the wrong section.
  **[CLOSED second pass, 337c19e]** — see above.

SCOPE CHECK
- files in allowed scope: 13/13 (integrator explicitly authorised to touch `Program.cs`, area
  modules, controllers, `BusinessViolationCodes.*`, tests and the two named handover docs for this
  class of post-merge, cross-area consolidation).
- files outside allowed scope: none.
