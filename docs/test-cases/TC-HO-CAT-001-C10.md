# TC-HO-CAT-001-C10 — Cat Profiles backend, C10 contract amendment

Handover: docs/handover/HO-CAT-001.md (§1 2026-09-25 amendment, §11.5, §12.4, §15 U41, §16 I10a, §18 A13)
Commit(s) under test: `1462bb6` `4012393` `39254c0` `ca43062` (branch `ho/CAT-001-c10`, head `ca43062`,
base `06644ae` on `master`)
Written: 2026-09-25 · Executed: 2026-09-25

> This is a **short, targeted** gate on the C10 amendment only (`RecordCatParasiteTreatmentCommandHandler`
> and `CatsController.RecordParasiteTreatment` now return `201` with exactly `{ treatmentId, nextDueOn }`
> instead of no body) — not a full re-audit of HO-CAT-001's other ten stories, which have no gate
> artifacts of their own yet and are out of this gate's scope. Statuses record what was **observed**,
> not inferred. `BLOCKED` marks a case that could not be executed, or whose result was reasoned about
> rather than watched — it is not a failure, and it is not hidden.

## Summary

| | Count |
|---|---|
| Total | 5 |
| PASS | 5 |
| FAIL | 0 |
| BLOCKED | 0 |
| NOT RUN | 0 |

Full solution suite alongside these 5: `CatEcommerce.Domain.Tests` 38/38, `CatEcommerce.Application.Tests`
27/27 (includes U41), `CatEcommerce.IntegrationTests` 4/4 (I10a + the 3 new failure-path cases added
at this gate), `CatEcommerce.Infrastructure.Tests` 28/28 (unrelated bounded contexts, untouched by
this change, run anyway per "all suites") — **97/97 passed, 0 failed**, `dotnet build` 0
warnings/0 errors.

## Environment

- **.NET SDK**: `dotnet build CatEcommerce.sln` — confirmed clean, 0 warnings, 0 errors, both before
  and after the three new integration tests were added, and again after the decoration check's
  working-tree round-trip.
- **PostgreSQL**: native Windows PostgreSQL 17.5 at `localhost:5432`, database `catecommerce_dev`
  (HO-CAT-001 §5.1). Reachability confirmed directly (`Test-NetConnection localhost:5432` →
  `TcpTestSucceeded: True`) before writing this sheet, and every integration test actually ran
  against it (EF Core SQL logged for every case, not skipped/mocked).
- **Test projects in the solution**: `CatEcommerce.Domain.Tests`, `CatEcommerce.Application.Tests`,
  `CatEcommerce.IntegrationTests`, `CatEcommerce.Infrastructure.Tests` (the last is other bounded
  contexts' fakes, unrelated to and untouched by this change).
- **Web cross-check**: read only, no `npm`/frontend build run — `web/lib/api/cats.ts` compared
  directly against the live JSON produced by manually starting the API host
  (`dotnet run --project src/Api/CatEcommerce.Api --urls http://localhost:5288`) and driving one
  real `POST` through `curl.exe`, then stopping the host (confirmed no leftover `dotnet.exe`
  process for this project afterwards).
- No mocks anywhere in this gate: every row that hits the API goes through a real `TestServer`
  (`WebApplicationFactory<Program>`) or, for the cross-check, a real Kestrel host — against the
  real Postgres instance above.

## Unit cases

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U41 | `RecordCatParasiteTreatmentCommandHandler.HandleAsync`, valid command | `CommandResponse.Result` is a `RecordCatParasiteTreatmentResult` carrying the created treatment's `TreatmentId` and `NextDueOn == AdministeredOn.AddDays(IntervalDays)` — not `cat.Id` (HO-CAT-001 §15 U41) | PASS | `dotnet test tests/CatEcommerce.Application.Tests --no-build` — 27/27 passed (U41 included; also re-confirmed by name in the full-suite run) |

## Integration cases

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I10a | `POST /api/cats/{catId}/parasite-treatments`, valid | `201`; body is **exactly** `{ treatmentId, nextDueOn }` (no `catId`); `nextDueOn == administeredOn.AddDays(intervalDays)` (HO-CAT-001 §16 I10a, owner decision 2026-09-25) | PASS | `dotnet test tests/CatEcommerce.IntegrationTests --no-build` — `RecordParasiteTreatment_Valid_Returns201WithTreatmentIdAndComputedNextDueOn` passed, against real `catecommerce_dev` (EF SQL logged: `INSERT INTO clinic.cat_parasite_treatment ...`). Also cross-checked live via `curl.exe`: `HTTP/1.1 201 Created`, body `{"treatmentId":"0196d7cf-615b-4347-bd12-1bf2ca3d5881","nextDueOn":"2026-12-24"}` for `administeredOn=2026-09-25, intervalDays=90` (25 Sep → 24 Dec is exactly +90 days) |
| I-C10-1 | `POST .../parasite-treatments` for an **unknown** `catId` | **`400`** with `CatNotFound` in the body — **not `404`**: the handler returns a failed `CommandResponse`, and `CatsController` maps every command failure to `BadRequest`, the same as every other `POST` command endpoint in this controller (only the two `GET`-by-id query endpoints return `404`) | PASS | New test `RecordParasiteTreatment_UnknownCat_Returns400WithCatNotFound`, added this gate — watched pass: `400`, body contains `"errorCode":"CatNotFound"` |
| I-C10-2 | `POST .../parasite-treatments` for a cat already marked **deceased** | **`400`** with `CatIsDeceased` — `CatAggregate.RecordParasiteTreatment` calls the shared `EnsureAlive()` guard (§8.2), the handler catches `DomainRuleViolationException`, the controller maps it to `400` (same pattern as I18) | PASS | New test `RecordParasiteTreatment_DeceasedCat_Returns400WithCatIsDeceased`, added this gate — registers a cat, marks it deceased via `POST .../deceased`, then attempts the treatment; watched pass: `400`, body contains `"errorCode":"CatIsDeceased"` |
| I-C10-3 | `POST .../parasite-treatments` with an **invalid body** (`intervalDays = 0`, outside the validator's `1–730` range, §11.3) | **`400`** with a validation error naming `IntervalDays` — proves F3 (`ValidateCommand = true`) actually runs the validator | PASS | New test `RecordParasiteTreatment_IntervalDaysOutOfRange_Returns400WithValidationError`, added this gate — watched pass: `400`, body contains `"propertyName":"IntervalDays"` |

## Regression cases from earlier gates

No prior TEST/ARCHITECTURE/SECURITY/CODE_REVIEW gate has run for HO-CAT-001 as a whole — this is
the first TEST-gate artifact for this handover. `docs/gates/HO-CAT-001-C10-ARCHITECTURE.md` and
`-CODE_REVIEW.md`, both present in this worktree and written in parallel with this gate on the same
delta, were read for context (not modified). Neither recorded a BLOCKER/HIGH/MEDIUM finding against
this diff — each recorded only two LOW integrator/scope notes (`CatApiFactory` duplicating
`AccountsApiFactory`'s Testing-environment setup; the HO-CAT-001.md §1 edit sitting outside §13's
literal path list), neither of which names a code defect with behaviour to regress-test. **No
regression rows are added** — there is nothing fixed by this delta for a row to guard, per this
sheet's own instructions not to invent one.

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| — | — | none — no BLOCKER/HIGH/MEDIUM finding exists against this delta | — | — | — |

## Decoration check — proving U41 and I10a are not vacuous

Per this gate's mandate, both were watched failing against the pre-amendment (`master`, `06644ae`)
handler/command/controller before being trusted, not assumed non-decorative from the diff alone.

1. `git status --short` confirmed clean before starting.
2. `git checkout 06644ae -- src/Application/CatEcommerce.Application/CommandHandlers/Cat/RecordCatParasiteTreatmentCommandHandler.cs src/Application/CatEcommerce.Application/Commands/Cat/RecordCatParasiteTreatmentCommand.cs src/Api/CatEcommerce.Api/Controllers/CatsController.cs` — restored the exact pre-C10 content of all three production files the diff touches.
3. **U41 against pre-amendment code: `dotnet build tests/CatEcommerce.Application.Tests` failed to compile** — `CS0246: The type or namespace name 'RecordCatParasiteTreatmentResult' could not be found`. Watched directly: the pre-amendment command/handler pair has no such type, so U41's own test file cannot even compile against it, let alone pass. This is the strongest form of non-decoration evidence available for a contract-shape change.
4. **I10a against pre-amendment code: `dotnet build tests/CatEcommerce.IntegrationTests` succeeded** (the integration test doesn't reference the removed type by name, only by JSON shape), and running it **failed**, watched directly:
   ```
   Assert.Equal() Failure: Collections differ
   Expected: ["treatmentId", "nextDueOn"]
   Actual:   ["catId", "treatmentId"]
   ```
   — the restored controller builds its `201` body from `new { catId, treatmentId }`, exactly the
   pre-amendment shape, and I10a's own exact-property-set assertion catches it.
5. `git checkout ca43062 -- <the same three files>` restored the branch head content; `git status
   --short` confirmed clean again.
6. `dotnet build CatEcommerce.sln` — 0 errors. `dotnet test CatEcommerce.sln --no-build` re-run in
   full: **97/97 passed** (38 Domain + 27 Application + 4 Integration + 28 Infrastructure.Tests),
   confirming the round-trip left nothing broken.
7. Nothing from this check was committed; no temporary files were left behind.

## Cross-check with the web client

`web/lib/api/cats.ts`'s `RecordCatParasiteTreatmentResponse` (`{ treatmentId: string; nextDueOn:
string }`) was compared directly against a live response, not the handover's prose alone: manually
started the API host (`dotnet run --project src/Api/CatEcommerce.Api --urls http://localhost:5289`)
against the same `catecommerce_dev` database, registered a cat, and drove one real `POST
.../parasite-treatments` through `curl.exe`. Observed raw body:
`{"treatmentId":"0196d7cf-615b-4347-bd12-1bf2ca3d5881","nextDueOn":"2026-12-24"}` — property names,
count and order match `cats.ts` exactly. No custom `DateOnly` converter is registered anywhere in
`Program.cs`; `net9.0`'s built-in `System.Text.Json` `DateOnly` converter serialises as `yyyy-MM-dd`
(observed: `"2026-12-24"` for `administeredOn=2026-09-25` + 90 days, arithmetically correct),
matching the format `cats.ts`'s own comment documents for every other `DateOnly` field this API
returns (`recordedOn`, line 42). No mismatch found. Host was stopped afterwards; confirmed no
leftover `dotnet.exe` process for this project.

While the host was up, the three new failure paths' exact response bodies were also captured live
(not just asserted via `Assert.Contains` in the automated tests), to confirm the sheet's own wording
above is accurate rather than assumed:
- Unknown cat: `400`, `{"errors":[{"errorCode":"CatNotFound","errorMessage":"Cat '...' was not
  found.","propertyName":"CatId","resourceName":"CatAggregate"}],"isValid":false}`
- Deceased cat: `400`, `{"errors":[{"errorCode":"CatIsDeceased","errorMessage":"This cat's record
  is deceased and cannot be edited.","propertyName":"LifeStatus","resourceName":"CatAggregate"}],"isValid":false}`
- `intervalDays = 0`: `400`,
  `{"errors":[{"errorCode":"InclusiveBetweenValidator","errorMessage":"'Interval Days' must be
  between 1 and 730. You entered 0.","propertyName":"IntervalDays","resourceName":null}],"isValid":false}`
  — note the FluentValidation error **code** is the validator's own name
  (`InclusiveBetweenValidator`), not `IntervalDays`; `IntervalDays` is the **property** name. The
  automated test (`I-C10-3`) asserts on `"IntervalDays"` appearing anywhere in the body, which this
  shape satisfies (via `propertyName`), and this distinction is recorded here rather than left to
  look like a claim that `IntervalDays` is itself an error code.

## Acceptance-criteria coverage

| Criterion | Covered by | Status |
|---|---|---|
| A13 (§18) — "A parasite treatment computes next-due from the configured interval, and the `201` response returns it" | U41, I10a | PASS |
| §12.4 — "The body is exactly `{ treatmentId, nextDueOn }`" | I10a, web cross-check | PASS |
| §12.4/§8.2 — "not deceased" guard still applies to C10 after the amendment | I-C10-2 | PASS |
| §16.1 rule 1/3 — every integration test mints its own owner/cat, no fixed literal id | I10a, I-C10-1, I-C10-2, I-C10-3 | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| A dedicated unit-level `RecordCatParasiteTreatmentCommandHandler` test for "cat not found" and for "deceased cat" (mirroring `UpdateCatProfileCommandHandlerTests`' U28/U29, which exercise the same generic "load, mutate, catch" shape against a *different* handler) | Out of this gate's short, C10-scoped brief, which asked for integration coverage of the failure paths; the handler-level mechanism is unchanged by C10 and already exercised generically by U28/U29 and by `CatAggregateTests`' U20/U21 (`EnsureAlive()` via `UpdateProfile`) | Whoever runs the full HO-CAT-001 TEST gate (U1–U40, I1–I22) |
| ARCHITECTURE's and CODE_REVIEW's own LOW items (the `CatApiFactory`/`AccountsApiFactory` duplication; the HO-CAT-001.md §1 edit location) | Both are integrator/scope notes with no attached behaviour to test | Integrator, at merge time |
