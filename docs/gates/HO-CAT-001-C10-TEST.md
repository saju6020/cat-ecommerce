GATE: TEST
HANDOVER: HO-CAT-001 (C10 follow-up, owner decision 2026-09-25)
COMMITS: 1462bb6 4012393 39254c0 ca43062
DATE: 2026-09-25
STATUS: PASS

Short, targeted gate on the C10 contract amendment only (`RecordCatParasiteTreatmentCommandHandler`
and `CatsController.RecordParasiteTreatment` now return `201` with exactly `{ treatmentId,
nextDueOn }`) — not a full HO-CAT-001 audit; no TEST/ARCHITECTURE/SECURITY/CODE_REVIEW gate has run
for this handover as a whole, and the other ten stories (U1-U40, I1-I22 minus I10a) are out of this
gate's scope. Full test-case sheet, cases written before execution, each row marked as observed:
`docs/test-cases/TC-HO-CAT-001-C10.md`. 5 rows: 5 PASS, 0 FAIL, 0 BLOCKED, 0 NOT RUN.

BUILD AND FULL SUITE

`dotnet build CatEcommerce.sln` — 0 warnings, 0 errors, run three times (before adding tests, after
adding them, and after the decoration check's working-tree round-trip). `dotnet test
CatEcommerce.sln --no-build` — **97/97 passed, 0 failed**: `CatEcommerce.Domain.Tests` 38/38,
`CatEcommerce.Application.Tests` 27/27 (includes U41), `CatEcommerce.IntegrationTests` 4/4 (I10a
plus 3 new failure-path cases added at this gate), `CatEcommerce.Infrastructure.Tests` 28/28 (other
bounded contexts' fakes, untouched by this change, run anyway per "all suites"). PostgreSQL 17.5 at
`localhost:5432`/`catecommerce_dev` (HO-CAT-001 §5.1) confirmed reachable directly
(`Test-NetConnection` → `TcpTestSucceeded: True`) before running, and every integration test's EF
Core SQL was logged, confirming it actually hit the real database rather than an in-memory provider
(§20.1).

NEW INTEGRATION TESTS ADDED THIS GATE

HO-CAT-001 §16 names only the valid path (I10a) for C10; the failure paths have no §16 ID of their
own. Three added to `tests/CatEcommerce.IntegrationTests/Endpoints/ParasiteTreatmentEndpointTests.cs`,
none pre-existing:
- `RecordParasiteTreatment_UnknownCat_Returns400WithCatNotFound` — an unknown `catId` returns `400`
  with `CatNotFound`, **not `404`**. Checked directly against `CatsController.cs` before writing
  this: every `POST` command endpoint in this controller maps `!response.IsSuccess` to
  `BadRequest`; only the two `GET`-by-id query endpoints (`GetCat`, `GetCatVaccinationStatus`)
  return `404`, from `response.Result is null`. C10 is a `POST` command endpoint, so it follows the
  first pattern. This corrects an initial assumption in this gate's own brief that named `404` for
  this row — traced the actual code rather than the brief's wording, per this gate's mandate not to
  invent statuses.
- `RecordParasiteTreatment_DeceasedCat_Returns400WithCatIsDeceased` — registers a cat, marks it
  deceased via the real `POST .../deceased` endpoint, then attempts the treatment; `400` with
  `CatIsDeceased` (the shared `EnsureAlive()` guard, §8.2, unchanged by this amendment).
- `RecordParasiteTreatment_IntervalDaysOutOfRange_Returns400WithValidationError` — `intervalDays =
  0`, outside the validator's `1-730` range (§11.3); `400` naming `IntervalDays`, proving F3
  (`ValidateCommand = true`) actually runs the validator.

All three follow §16.1 (fresh owner/cat per test via the existing `RegisterCatAsync` helper, no
fixed literal id, no assertion on global state).

DECORATION CHECK

U41 and I10a were watched failing against the exact pre-amendment (`master`, `06644ae`) content of
all three production files the diff touches
(`RecordCatParasiteTreatmentCommandHandler.cs`/`RecordCatParasiteTreatmentCommand.cs`/
`CatsController.cs`), not assumed non-decorative from the diff alone.
- **U41**: restoring the pre-amendment files and building `tests/CatEcommerce.Application.Tests`
  **failed to compile** — `CS0246: The type or namespace name 'RecordCatParasiteTreatmentResult'
  could not be found`. The pre-amendment handler has no such type; U41's own test file cannot even
  compile against it. The strongest available non-decoration evidence for a contract-shape change.
- **I10a**: the integration test project still compiled (it asserts on JSON shape, not a compiled
  type), but running it against the restored controller **failed**, watched directly:
  `Assert.Equal() Failure: Collections differ — Expected: ["treatmentId", "nextDueOn"] — Actual:
  ["catId", "treatmentId"]` — the pre-amendment controller's `new { catId, treatmentId }` body,
  exactly as expected.
- Restored the branch head (`ca43062`) content of the same three files; `git status --short` was
  clean before and after. Rebuilt (0 errors) and re-ran the full suite (97/97 passed), confirming
  the round-trip broke nothing. Nothing from this check was committed.

CROSS-CHECK WITH THE WEB CLIENT

`web/lib/api/cats.ts`'s `RecordCatParasiteTreatmentResponse` (`{ treatmentId: string; nextDueOn:
string }`) was checked against a live response, not the handover's prose alone: started the API
host manually (`dotnet run --project src/Api/CatEcommerce.Api`) against the same `catecommerce_dev`
database and drove a real `POST .../parasite-treatments` through `curl.exe`. Observed body:
`{"treatmentId":"0196d7cf-615b-4347-bd12-1bf2ca3d5881","nextDueOn":"2026-12-24"}` for
`administeredOn=2026-09-25, intervalDays=90` — property names, count, order and the `+90 days`
arithmetic all match. No custom `DateOnly` converter exists in `Program.cs`; `net9.0`'s built-in
`System.Text.Json` `DateOnly` converter serialises `yyyy-MM-dd` by default, matching the format
`cats.ts`'s own comment documents for the API's other `DateOnly` fields. No mismatch. Host stopped
afterwards; confirmed no leftover process.

Also captured live, while the host was up, the exact response bodies for the three new failure
cases (not just the automated tests' own `Assert.Contains` checks) — see
`docs/test-cases/TC-HO-CAT-001-C10.md`'s "Cross-check with the web client" section for the full raw
bodies. One distinction worth recording: the `intervalDays = 0` case's FluentValidation `errorCode`
is `InclusiveBetweenValidator` (the validator's own name), not `IntervalDays` — `IntervalDays` is
the **property** name the error is against. The automated test's `Assert.Contains("IntervalDays",
raw)` is satisfied via `propertyName`, correctly, but this is recorded explicitly so it isn't read
as a claim that `IntervalDays` is itself an error code.

FINDINGS

None. No BLOCKER/HIGH/MEDIUM/LOW findings against the C10 diff itself. One correction to this
gate's own starting brief, not a code defect: the brief's failure-path list named `404` for an
unknown `catId`; tracing `CatsController.cs` directly shows every `POST` command endpoint —
including C10 — maps a command failure to `400`, and only the two `GET`-by-id query endpoints
return `404`. The test case sheet and the new test are written against the `400` behaviour actually
in the code, per this gate's mandate not to invent statuses.

EXISTING TESTS JUDGED WEAK

None found in the C10-touched surface. U41 and I10a were both re-read for whether they would still
pass against broken production code (loose substring matches, unverified mocks) — both assert on a
concrete, observable outcome (`RecordCatParasiteTreatmentResult`'s exact fields for U41; the exact
property set for I10a) and both were proven non-decorative directly (see above), not merely judged
by reading.

GAPS LEFT DELIBERATELY

- No dedicated unit-level `RecordCatParasiteTreatmentCommandHandler` test for "cat not found" or
  "deceased cat" was added. This gate's brief asked for integration coverage of the failure paths,
  which was added; the handler-level "load, fail, no throw" and "catch DomainRuleViolationException"
  shapes are unchanged by C10 and are already exercised generically elsewhere in the existing suite
  (`UpdateCatProfileCommandHandlerTests`' U28/U29 against a different handler; `CatAggregateTests`'
  U20/U21 exercising `EnsureAlive()`/`CatIsDeceased` via `UpdateProfile`). Left for whoever runs the
  full HO-CAT-001 TEST gate (U1-U40, I1-I22), not raised as a finding since nothing in the C10 diff
  touches these guards.
- ARCHITECTURE's and CODE_REVIEW's own LOW items (the `CatApiFactory`/`AccountsApiFactory`
  Testing-environment duplication; the HO-CAT-001.md §1 edit's location outside §13's literal path
  list) are integrator/scope notes with no attached behaviour — nothing to test, left for the
  integrator at merge time.

SCOPE CHECK

- Files added/modified by this gate, all within HO-CAT-001 §13:
  - modified: `tests/CatEcommerce.IntegrationTests/Endpoints/ParasiteTreatmentEndpointTests.cs`
    (three new `[Fact]`s appended; the existing I10a test is untouched).
  - added: `docs/test-cases/TC-HO-CAT-001-C10.md`, this file (`docs/gates/HO-CAT-001-C10-TEST.md`).
- Working-tree round-trips used for the decoration check, each confirmed clean (`git status
  --short`) both before and after: `git checkout 06644ae -- <the three production files>`, then
  `git checkout ca43062 -- <the same three files>`.
- A manually-started `dotnet run --project src/Api/CatEcommerce.Api` (two short-lived instances, on
  ports 5288 and 5289) was used for the web cross-check and the live failure-body capture; both
  stopped before finishing, confirmed no leftover `dotnet.exe` process for this project either time.
- Files read but not modified: `web/lib/api/cats.ts`, `docs/gates/HO-CAT-001-C10-ARCHITECTURE.md`
  and `-CODE_REVIEW.md` (in-progress, written in parallel by those gates on this same commit — read
  for context only, per the task's instruction not to touch other gates' files), the full
  `docs/handover/HO-CAT-001.md`.
- No commit was made by this gate (per the task's explicit instruction — ARCHITECTURE and
  CODE_REVIEW are writing in parallel). No push. No other worktree touched.
- Files outside allowed scope: none.
