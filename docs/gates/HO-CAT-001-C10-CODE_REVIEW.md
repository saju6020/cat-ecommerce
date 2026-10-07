GATE: CODE_REVIEW
HANDOVER: HO-CAT-001 (C10 follow-up, owner decision 2026-09-25)
COMMITS: 1462bb6 4012393 39254c0 ca43062
DATE: 2026-09-25
STATUS: PASS

Short review of `git diff master...ho/CAT-001-c10` (base 06644ae, head ca43062): 4 commits, 8
files (204 insertions). Read `docs/handover/HO-CAT-001.md` §1, §11.5, §12.4, §15 (U41), §16
(I10a); `.ai/cqrs.md`; `.ai/command-handler-pattern.md`. Traced by hand rather than built/run —
TEST owns actually running U41 and I10a.

CHECKS
- **Failure branches, controller cast (`CatsController.cs:296-306`)** — `if (!response.IsSuccess)
  return BadRequest(response.ValidationResult);` runs *before* the
  `(RecordCatParasiteTreatmentResult)response.Result!` cast, so every violation path (cat not
  found, future-dated treatment, and — checked in the aggregate —
  `EnsureAlive()` throwing for a deceased cat) returns `400` through
  `RecordCatParasiteTreatmentCommandHandler.Violation(...)` and never reaches the cast. No `500`,
  no `InvalidCastException`, no `202`. Confirmed `EnsureAlive()` is called first inside
  `CatAggregate.RecordParasiteTreatment` (`CatAggregate.cs:318-320`) and the handler wraps the
  call in `try/catch (DomainRuleViolationException)`.
- **Same-instance re-selection (`RecordCatParasiteTreatmentCommandHandler.cs:35-43`)** —
  `cat.ParasiteTreatments.Single(t => t.Id == command.TreatmentId)` runs after `UpdateAsync`, but
  `EfAggregateRootRepository<TAggregate>.UpdateAsync` (`Infrastructure/Repositories/
  EfAggregateRootRepository.cs`) never reassigns or reloads `aggregate` — it calls
  `SaveChangesAsync` on the same tracked instance and returns `Task`, not `Task<TAggregate>`. The
  child treatment was already appended to `_parasiteTreatments` by `RecordParasiteTreatment`
  in-memory before `UpdateAsync` runs, so the post-update `Single(...)` selects the record that
  was just written, not a stale or reloaded copy. Selection by the controller-minted
  `TreatmentId` (rather than list order/last-added) is correct and matches how
  `CatMapper.ToDomainDto` carries that id through.
- **`DateOnly` JSON shape** — no custom converter is registered anywhere in `Program.cs` or the
  Api project; none is needed. `net9.0`'s `System.Text.Json` serialises `DateOnly` as
  `"yyyy-MM-dd"` by default (built in since .NET 7), matching every other `DateOnly` field already
  round-tripped by this API (`AdministeredOn`, `BatchExpiresOn`). `I10a`'s
  `DateOnly.Parse(body.GetProperty("nextDueOn").GetString()!)` will parse it correctly.
- **U41 and I10a would fail on pre-amendment code** — checked both against the diff's `-` side.
  Old handler set `Result = command.TreatmentId` (a bare `Guid`); U41's
  `Assert.IsType<RecordCatParasiteTreatmentResult>(response.Result)` fails on that. Old controller
  built its body from local `catId`/`treatmentId` variables (`new { catId, treatmentId }`), not
  from `response.Result`; I10a's exact-property-set assertion
  (`Assert.Equal(["treatmentId", "nextDueOn"], propertyNames)`) fails against that shape (wrong
  keys, no `nextDueOn`). Both regression tests are non-vacuous.
- **Integration harness (`CatApiFactory.cs`, `CatIntegrationTestBase.cs`)** — follows §16.1: fresh
  `ownerId`/cat per test via `RegisterCatAsync`, no assertion on global state, no fixed natural
  key that could collide across runs (`"BATCH-1"`/`"Drontal"` are not unique-constrained fields
  per the domain model). No `Dispose`/cleanup of created rows, but §16.1 explicitly accepts
  accumulation in the shared `catecommerce_dev` database in exchange for never asserting
  emptiness — this new test complies with that rule rather than violating it. Only one test class
  exists today, so no ordering or cross-test leakage is possible yet; the pattern (mint-your-own-
  ids) will hold under xUnit's default class-level parallelism once more tests are added. Hardcoded
  `postgres:postgres` connection string is local-only, not a secret, consistent with the comment.
- **Other callers of C10** — grepped the whole tree (`.cs`/`.ts`/`.tsx`) for
  `RecordCatParasiteTreatmentCommand`/`parasite-treatments`: only `CatsController` dispatches the
  command, and only `web/lib/api/cats.ts` (`recordCatParasiteTreatment`) /
  `useRecordCatParasiteTreatment` consume the response, matching the new `{ treatmentId,
  nextDueOn }` shape exactly (already verified independently in `HO-CAT-002-CODE_REVIEW.md`'s
  fourth pass — `catId` dropped, `nextDueOn` non-nullable). Nothing reads `catId` from this
  response body.
- **Scope** — no `src/Domain`, `src/Infrastructure`, migration or `.csproj` change. The
  `docs/handover/HO-CAT-001.md` edit only corrects §1's own amendment-history clause (already
  content-reviewed by the parallel ARCHITECTURE gate); it makes no contract change.

FINDINGS
- None at Critical/High/Medium. Agree with the parallel ARCHITECTURE gate's two LOW items
  (`CatApiFactory` duplicating `AccountsApiFactory`'s Testing-environment setup — an integrator
  follow-up once both branches merge, not a defect in this diff; and the HO-CAT-001.md §1 edit
  sitting outside §13's literal path list, which is correct and not a scope violation).

SUGGESTIONS
- None beyond the ARCHITECTURE gate's integrator-follow-up note (shared `WebApplicationFactory`
  base once multiple areas have their own).

SCOPE CHECK
- files in allowed scope: 7/7 implementation/test files inside HO-CAT-001 §13
  (`src/Api/**`, `src/Application/**`, `tests/CatEcommerce.Application.Tests/**`,
  `tests/CatEcommerce.IntegrationTests/**`).
- files outside allowed scope: none — the 8th file, `docs/handover/HO-CAT-001.md`, is the
  owner-confirmed amendment record, not an implementation path, and is not in §14.
