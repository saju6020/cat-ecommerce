GATE: TEST
HANDOVER: HO-CAT-001
COMMITS: worktree `ho/CAT-001-gates`, head `80d2ed5` at task start; `2b40542` (CODE_REVIEW third-pass
doc record, docs-only) and `cb2657c` (Merged/Anonymised owner fix, `AccountNotActive`) landed
mid-gate as announced by the coordinator, plus the developer's own test commit `3c8dcbf`
(`CatOwnerCustomerStatusEndpointTests.cs`). This gate's own new test files are **uncommitted**, per
the task's explicit instruction not to commit.
DATE: 2026-09-26/27
STATUS: PASS

This is the first full HO-CAT-001 TEST gate — ARCHITECTURE, SECURITY and CODE_REVIEW second/third
passes all PASSED and carried the §16 I1–I22 suite / `docs/test-cases/TC-HO-CAT-001.md` to this gate
as CODE_REVIEW's own carried HIGH. Both are now written. Full sheet, every row written before
execution, then executed one row at a time: `docs/test-cases/TC-HO-CAT-001.md` — **106/106 PASS, 0
FAIL, 0 BLOCKED, 0 NOT RUN** (41 unit, 23 integration inc. I10a, 10 regression, 25 acceptance rows;
the C10 amendment's own short sheet, `TC-HO-CAT-001-C10.md`, is unchanged and not re-litigated here).

## What this gate added

The §16 I1–I22 integration suite did not exist before this gate (only I10a, from the C10 amendment,
and three unrelated regression suites the gate-fix rounds added). Seven new test files, all
uncommitted, under `tests/CatEcommerce.IntegrationTests/`:

- `Endpoints/CatCrudLifecycleEndpointTests.cs` — I2–I10, I18 (10 cases)
- `Endpoints/CatAuditAndOwnershipEndpointTests.cs` — I12–I17 (6 cases)
- `Endpoints/CatVaccinationStatusEndpointTests.cs` — I11, all three states (3 cases)
- `Endpoints/CatOwnerScopedPaginationEndpointTests.cs` — I19's literal 25-cats/pageSize-10 case (1 case)
- `Endpoints/CatMedicalHistoryPdfRenderFailureTests.cs` — the port-level PDF render-failure path via
  a failing fake `ICatMedicalHistoryPdfRenderer` (1 case; task item 2)
- `Endpoints/CatOwnerAndTransferCustomerValidationTests.cs` — `CustomerNotFound` on register and
  ownership transfer: random id, a staff account's id, a real customer → success (6 cases; task
  item 2)
- `Persistence/CatSchemaIndexTests.cs` — I22, every §12.1 index from an empty database, not just
  table existence (1 case)

I1, I10a, I20, I21 and most of the regression rows were already covered by existing tests
(`HostCompositionTests`, `ParasiteTreatmentEndpointTests`, `CatProfileProjectionEndpointTests`,
`CatsPaginationEndpointTests`, `CommandValidatorsTests`) and are cited, not duplicated — see the
sheet's Integration/Regression tables for exactly which test backs which ID.

**Coordination note**: mid-gate, the coordinator relayed that a developer was landing a fix so
`RegisterCat`/`TransferCatOwnership` also reject a Merged or Anonymised owner (CODE_REVIEW
third-pass MEDIUM). `cb2657c` (the fix, `AccountNotActive`) and `3c8dcbf` (the developer's own 4
tests, `CatOwnerCustomerStatusEndpointTests.cs`) both landed and were picked up via `git log` before
the final run. This gate's own `CatOwnerAndTransferCustomerValidationTests.cs` therefore covers only
the unknown-id/staff-id/real-customer cases (as the coordinator said would stay planned) and does
not duplicate the developer's Merged/Anonymised cases or their `AccountNotActive` errorCode.

## Existing tests judged

Every existing test cited as evidence in the sheet (`CatAggregateTests`, `CommandValidatorsTests`,
`CatVaccinationStatusCalculatorTests`, `CatProfileProjectionEndpointTests`,
`CatsPaginationEndpointTests`, `ParasiteTreatmentEndpointTests`, `HostCompositionTests`,
`CatOwnerCustomerStatusEndpointTests`) was read, not just counted: each asserts a concrete,
observable outcome (an exact error code, an exact computed date, an exact response shape, a direct
database read) rather than a loose "it didn't throw" — none would still pass against a broken
implementation of the behaviour it names. No existing test was found to be decorative. None were
weakened or deleted.

## Production bugs found

**None.** Three of this gate's own **new** tests initially failed — reported here for completeness,
per the "judge the assertions, not the count" mandate, even though the root cause was in the new
test, not production code:

1. `CatMedicalHistoryPdfRenderFailureTests`'s original assertion (`Assert.Empty(body)` on the `500`
   response) was wrong: the host's own default ASP.NET Core ProblemDetails behaviour fills a generic
   RFC 9110 problem body for a bare `StatusCode(500)`, which `CatsController` does not opt into
   itself and which carries no exception detail. Fixed by asserting the actually load-bearing
   property instead (no exception message/type/QuestPDF name in the body or logs).
2. `CatVaccinationStatusEndpointTests` (×2, the Current and DueSoon cases) originally asserted
   `overallStatus`, which `GetCatVaccinationStatusQueryHandler` computes over the **whole shared
   catalogue's** active core vaccines — a concurrently running, unrelated test class registering its
   own new core vaccine mid-run pushed `overallStatus` to `Overdue` regardless of this test's own
   setup (watched directly: `Assert.Equal() Failure ... Expected: "Current" / Actual: "Overdue"`).
   Fixed by asserting the per-vaccine `status`/`reason` for the vaccine each test itself created,
   which is deterministic and immune to that race while still exercising the real read model over
   HTTP. Documented in the sheet's Gaps section so the coupling is visible, not silently patched
   over.

Both fixes are new-test-authoring corrections, not production defects — no `src/**` file was
touched by this gate.

## Investigation: transient 500 in `CatVaccineFullAuthorizationMatrixTests` (task item 3)

- **Reported**: a transient `500` on 3 of 8 staff roles for one endpoint, in one isolated run.
- **Attempted reproduction**: the class alone, 10 consecutive isolated runs
  (`dotnet test --filter FullyQualifiedName~CatVaccineFullAuthorizationMatrixTests`) —
  **22/22 passed every time, 0 failures across 220 total test executions.** Also green in all three
  full `CatEcommerce.IntegrationTests` runs this gate did, and in the one full-solution run.
- **Not reproduced.** No server-side exception was captured via `CatEcommerceApiFactory
  .WithCapturedLogs()` because the failure never recurred to capture.
- **Code read for a plausible mechanism**: `RecordCatVaccinationCommandHandler`,
  `RegisterCatCommandHandler`, `TransferCatOwnershipCommandHandler` and
  `RecordCatParasiteTreatmentCommandHandler` all return clean `400` violations for every
  not-found/invalid-role case the matrix's fixed request bodies can hit — no unhandled-exception
  path found. The test's own `StaffCache` (`ConcurrentDictionary<string, Lazy<Task<...>>>`) is
  safely idempotent under concurrent `GetOrAdd` and process-local, ruling out a cross-run leak.
- **Most likely explanation, unproven**: the handover's own §5.1 warns `catecommerce_cat` "is not
  empty and is not yours alone" — this gate observed a live instance of exactly that class of
  cross-test interference (the vaccination-status race above). The original report came from a
  period when other gates were noted running probes against the same shared Postgres instance in
  parallel. A transient connection-pool/lock-contention hiccup from genuinely concurrent, unrelated
  processes on the same physical database is plausible but not demonstrated. **Recorded as
  unreproduced, not as a closed non-issue** — see the sheet's "Cases I could not run" and Gaps
  sections.

## Build / full suite (run twice, per the task)

- `dotnet build CatEcommerce.sln` — **0 warnings, 0 errors** (three times across this gate: before
  adding tests, after adding them, and after the render-failure/vaccination-status fixes).
- `dotnet dotnet-ef migrations has-pending-model-changes` — clean, "No changes have been made to the
  model since the last migration."
- `dotnet dotnet-ef database update` against `catecommerce_cat` — already up to date, no migration
  applied by this gate.
- `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_cat;Username=postgres;Password=postgres`:
  - `CatEcommerce.Domain.Tests` — **223/223**, run twice (standalone + inside the full-solution run).
  - `CatEcommerce.Application.Tests` — **93/93**, run twice.
  - `CatEcommerce.Infrastructure.Tests` — **28/28**, run twice.
  - `CatEcommerce.IntegrationTests` — **371/371**, run **three** times (twice standalone, once
    inside the full-solution run) — identical result every time, no flake.
  - Combined full-solution run (`dotnet test CatEcommerce.sln --no-build`): **715/715 passed, 0
    failed** (28 + 223 + 93 + 371).

## Web

- `npx vitest run` — **37 files, 104/104 passed**.
- Playwright, isolated port: `playwright.config.ts` hardcodes `localhost:3000` and does not read
  `PORT` as shipped. Temporarily edited to read `process.env.PORT` (fallback `3000`), run once as
  `PORT=3104 npx playwright test`, then reverted (`git checkout -- web/playwright.config.ts`,
  confirmed clean diff) immediately after — no process left listening on `3104` afterward. Nothing
  under `web/` committed.
  - **57 passed, 53 failed**, out of 110. Every failure is a pre-existing **HO-CAT-002** spec
    (`cat-*.spec.ts`, `staff-shell-*.spec.ts`), mirrored between `chromium` and `mobile-360`; none
    are HO-CAT-001 specs (HO-CAT-001 has none — §4.1, backend only). One captured failure shows the
    page rendering correctly but a mutation producing "Something went wrong. Please try again." —
    consistent with the mocked/real network call failing, not a routing or rendering defect. Not
    investigated further and not attributed to a cause (not cross-checked against port 3000, which
    this gate deliberately avoided) — this is HO-CAT-002's own frontend surface, out of scope for a
    backend-only handover's TEST gate, and is reported factually rather than diagnosed or fixed.

## Tests left uncommitted (per the task's explicit instruction not to commit)

- `docs/test-cases/TC-HO-CAT-001.md` (new)
- `docs/gates/HO-CAT-001-TEST.md` (this file, new)
- `tests/CatEcommerce.IntegrationTests/Endpoints/CatCrudLifecycleEndpointTests.cs` (new)
- `tests/CatEcommerce.IntegrationTests/Endpoints/CatAuditAndOwnershipEndpointTests.cs` (new)
- `tests/CatEcommerce.IntegrationTests/Endpoints/CatVaccinationStatusEndpointTests.cs` (new)
- `tests/CatEcommerce.IntegrationTests/Endpoints/CatOwnerScopedPaginationEndpointTests.cs` (new)
- `tests/CatEcommerce.IntegrationTests/Endpoints/CatMedicalHistoryPdfRenderFailureTests.cs` (new)
- `tests/CatEcommerce.IntegrationTests/Endpoints/CatOwnerAndTransferCustomerValidationTests.cs` (new)
- `tests/CatEcommerce.IntegrationTests/Persistence/CatSchemaIndexTests.cs` (new)

No `src/**` file was touched by this gate. `web/playwright.config.ts` was touched and reverted (see
Web section); confirmed no diff remains.

## Gaps left deliberately (see the sheet for full detail)

- I11's `overallStatus` is not asserted for the Current/DueSoon cases — a genuine, observed race
  against the shared, concurrently-written vaccine catalogue; per-vaccine status is asserted instead.
- I7 (concurrent `409`) asserts the legitimate outcome set (`200`/`409`, never `500`), not a
  guaranteed `409` — the same accepted approach this codebase's own `ProfileEndpointTests` I-E4/I-E5
  already use for an unforceable black-box HTTP race.
- I20 does not attempt PDF text extraction (QuestPDF/SkiaSharp output is not reliably
  ASCII-searchable) — the existing Bengali-name regression test is used as stronger, direct evidence
  instead.
- `CatVaccineFullAuthorizationMatrixTests`'s reported transient 500 was not reproduced.
- The Playwright failures above (HO-CAT-002 surface) are recorded, not diagnosed or fixed.

## SCOPE CHECK

- Files added: 9 (7 test files + this sheet + this gate doc), all within HO-CAT-001 §13
  (`tests/CatEcommerce.IntegrationTests/**`, `docs/test-cases/TC-HO-CAT-001.md`,
  `docs/gates/HO-CAT-001-*.md`).
- Files modified then reverted, confirmed clean: `web/playwright.config.ts` (temporary `PORT`
  support for the isolated-port run, per this gate's own task instructions — not a HO-CAT-001 file
  and not left changed).
- Files outside allowed scope: none. No `src/**`, `.ai/**`, `.claude/**`, `NuGet.Config`,
  `global.json`, `.sln`, or any other bounded context touched.
- No commit made by this gate, per the task's explicit instruction. No push. No other worktree
  touched. No git-ignored `appsettings` file copied or created.
- Result: **PASS. 106/106 sheet rows PASS, 0 FAIL, 0 BLOCKED, 0 NOT RUN. 0 production bugs found.**
  The one carried HIGH from CODE_REVIEW (the §16 suite not existing) is closed. The
  `CatVaccineFullAuthorizationMatrixTests` transient-500 investigation did not reproduce the issue;
  recorded honestly as unreproduced rather than closed as a non-issue.
