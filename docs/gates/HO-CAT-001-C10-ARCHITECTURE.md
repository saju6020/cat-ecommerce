GATE: ARCHITECTURE
HANDOVER: HO-CAT-001 (C10 follow-up, owner decision 2026-09-25)
COMMITS: 1462bb6 4012393 39254c0 ca43062
DATE: 2026-09-25
STATUS: PASS

This is a short delta review of `git diff master...ho/CAT-001-c10` (base 06644ae, head ca43062):
4 commits and 8 files. It was checked against HO-CAT-001 §1 (the 2026-09-25 Amended row), §11.5,
§12.4, §13, §14, §15 U41, §16 I10a and §18 A13, and against `.ai/cqrs.md` §2 and
`.ai/command-handler-pattern.md`. This gate built and ran nothing. U41 and I10a are for TEST to
run. I10a needs the local `catecommerce_dev` PostgreSQL.

CHECKS
- Returning a result record through `CommandResponse.Result`: **conforms.**
  - `CommandResponse.Result` is the platform's untyped result slot. Every other handler already
    uses it to return the real result (ADR-014, `.ai/cqrs.md` §2). The other 12 handlers put a
    `Guid` there. This handler puts
    `RecordCatParasiteTreatmentResult(Guid TreatmentId, DateOnly NextDueOn)` there.
  - It is the first non-scalar result in the codebase, and HO-CAT-001 §11.5 names it as the one
    exception.
  - The record sits next to its command in `Commands/Cat/`, as §11.5 requires. It carries only
    primitives, and the Domain layer does not leak into Api.
  - The handler still injects `IAggregateRootRepository<CatAggregate>` and calls one aggregate
    mutation. It calls `UpdateAsync(..., EventPublishOption.InMemoryPublishOnly)`.
  - There is no CommandService, no `IDispatcher` and no rejection event.
- Controller shape: **conforms to §12.4.**
  - The controller still mints `TreatmentId`, dispatches one command through
    `IInMemoryDispatcher.SendAsync`, and maps `!IsSuccess` to `400` with the `ValidationResult`.
    It never returns `202`.
  - `CreatedAtAction(nameof(GetCat), new { catId }, ...)` uses the same `GetCat` location as
    `AddCatPhoto` (`CatsController.cs:181`).
  - The body differs from the other 201 endpoints. `RegisterCat` (:52), `AddCatPhoto` (:181),
    `RecordWeight` (:238), `RecordVaccination` (:280) and `VaccinesController` (:37) all return
    `{ catId, <x>Id }` or `{ <x>Id }`. C10 returns exactly `{ treatmentId, nextDueOn }` with no
    `catId`, which is the exception §12.4 documents. The code matches the §12.4 snippet line for
    line.
  - The cast is `(RecordCatParasiteTreatmentResult)response.Result!`. It runs only on the success
    path, where the handler always sets that type. §12.4 specifies this cast.
- Selection by `TreatmentId`: **conforms to §11.5.**
  - The handler selects with `cat.ParasiteTreatments.Single(t => t.Id == command.TreatmentId)`
    after `UpdateAsync`.
  - The aggregate builds the child from `dto.TreatmentId` (`CatAggregate.cs:330-332`), and
    `CatMapper.cs:53` carries `command.TreatmentId`, so exactly one child matches.
  - `NextDueOn` is read from the aggregate-computed child, so the §8.2 rule stays in the domain.
    Nothing reimplements it.
- No model or migration change: **confirmed.**
  - The diff has no `src/Domain`, `src/Infrastructure`, `Migrations` or `csproj` changes.
  - `CatParasiteTreatment.NextDueOn` was already a non-null `DateOnly`, so the required
    `nextDueOn` the owner confirmed needs no schema change.
- Integration-test harness (`CatApiFactory`, `CatIntegrationTestBase`): **acceptable. It coexists
  with ho/ACC-001.**
  - It follows §16.1: `Testing` environment, the connection string supplied explicitly, no
    Testcontainers and no in-memory provider. The existing `Program` partial is reused
    (`Program.cs:111`), and there is no second host.
  - ho/ACC-001's `AccountsApiFactory` / `AccountsIntegrationTestBase` have distinct type names.
    Each is its own `IClassFixture<T>`. The two share only the namespace and the connection-string
    literal. Neither branch touches the other's files, and this branch leaves the test `csproj`
    unchanged. They will merge without a conflict.
  - After ACC-001 merges, `SessionUserContextProvider` resolves an anonymous caller to
    `Guid.Empty`, and CSRF is enforced only for non-anonymous callers
    (`SessionAuthenticationMiddleware`). The unauthenticated `POST`s in I10a should therefore
    still reach the handler. This is a runtime claim. TEST should re-run I10a on the merged tree.
- HO-CAT-001 §1 corrected sentence: **accurate.**
  - The row now says the implementation had already landed on master in `4007225` (2026-09-21),
    and that this amendment changes existing code.
  - Verified: `4007225` is an ancestor of master, and it adds both
    `RecordCatParasiteTreatmentCommandHandler.cs` and `CatsController.cs`. The commit message
    ("added handover docs") undersells this.
  - The edit changes only that clause. It leaves the contract, rule and section list unchanged.

FINDINGS
- [LOW] tests/CatEcommerce.IntegrationTests/CatApiFactory.cs:20-34: integrator follow-up, not a
  defect in this change.
  - `CatApiFactory` and ho/ACC-001's `AccountsApiFactory` duplicate the `Testing` environment
    setup and the `catecommerce_dev` connection-string literal.
  - Two areas can live with this. Sixteen more area handovers each adding their own copy cannot.
  - Once both are on master, the integrator should extract one shared base, for example
    `CatEcommerceApiFactory : WebApplicationFactory<Program>`, holding the environment and
    connection string, with virtual config and service overrides. Each area factory would then
    add only its own settings: ACC's CAPTCHA answer, rate limits, spies and `FakeTimeProvider`.
    Waves A-G should use that shared base.
  - Converging now is not required, and ACC-001 is out of this branch's scope.
- [LOW] docs/handover/HO-CAT-001.md:16: this edit is outside §13's list, which covers
  implementation paths.
  - It is not in §14 either. It edits the owner-confirmed 2026-09-25 Amended row itself, and the
    content is factually correct (see above).
  - It is recorded here so ACCEPTANCE sees it was reviewed. It is not a scope violation.

No BLOCKER, HIGH or MEDIUM findings.

SCOPE CHECK
- files in allowed scope: 7/7 implementation and test files are inside §13:
  - `src/Api/**`
  - `src/Application/**`
  - `tests/CatEcommerce.Application.Tests/**`
  - `tests/CatEcommerce.IntegrationTests/**`
- The eighth file, docs/handover/HO-CAT-001.md, is the owner-confirmed amendment record. It is
  not an implementation file and is not in §14.
- files outside allowed scope: none (no §14 path touched, no package change)
