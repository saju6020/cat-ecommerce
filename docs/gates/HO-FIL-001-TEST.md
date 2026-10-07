GATE: TEST
HANDOVER: HO-FIL-001
COMMITS: ce622b0 (branch ho/FIL-001, head; master eef1621 merged in) plus this gate's own
uncommitted test-only additions (see SCOPE CHECK) — ARCHITECTURE (4th pass PASS), SECURITY (2nd
pass PASS) and CODE_REVIEW (2nd pass PASS) all read in full before writing docs/test-cases/TC-HO-FIL-001.md.
DATE: 2026-09-27
STATUS: PASS

Test case sheet: docs/test-cases/TC-HO-FIL-001.md, written BEFORE any test was executed or added
(§15/§16 case IDs preserved verbatim; every row started NOT RUN).

COUNTS (final, all executed)
- Total 60 · PASS 57 · FAIL 0 · BLOCKED 3 · NOT RUN 0

HOW IT WAS CHECKED
- Build: `dotnet build CatEcommerce.sln` from the worktree as-is (no git-ignored appsettings
  copied) -- 0 errors, 0 warnings.
- `dotnet ef migrations has-pending-model-changes` (Infrastructure, startup project Api), against
  `catecommerce_fil` -- "No changes have been made to the model since the last migration.", both
  before and after the full test runs below.
- Unit suites (`dotnet test --no-build`): Domain.Tests 319/319, Application.Tests 101/101,
  Infrastructure.Tests 56/56.
- Integration suite (`CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_fil;
  Username=postgres;Password=postgres`, `dotnet test tests/CatEcommerce.IntegrationTests --no-build`):
  - First run: 412/413 -- one failure, in a test written for this gate
    (`FilesErasureEndpointTests.StoredFileEraser_RefusesAHeldFile_...`), diagnosed as a
    test-authoring bug (see FINDINGS) and fixed.
  - Second and third runs (the required "twice", both after the fix): **413/413**, both times,
    same counts, ~1 minute each.
- `dotnet test CatEcommerce.sln --no-build` (all four .NET test projects together) run twice more
  for the record: 319+101+56+413 = 889 both times, 0 failures.
- Targeted `--filter` runs (individually, by fully-qualified name) for every test added in this
  pass, to get PASS evidence at the single-test level rather than only aggregate counts: all
  green (see TC sheet's Evidence column and "New tests added" table).
- Web: `npm ci` (node_modules was absent in this worktree) then `npx vitest run` in `web/` --
  106/106 passed, 37 test files, confirming the merged-from-master `web/` tree is unaffected by
  FIL. `npm ci`'s `msw` postinstall regenerated `web/public/mockServiceWorker.js` (cosmetic
  reformatting only, same `PACKAGE_VERSION`/`INTEGRITY_CHECKSUM`) -- reverted with
  `git checkout -- web/public/mockServiceWorker.js` so the working tree carries only this gate's
  actual test changes.
- Host antivirus EICAR-quarantine interference (flagged as a known risk by the task) was not
  observed in any of these runs; `FilesController`'s existing `IOException` handling already
  fails closed for that case regardless (unchanged from CODE_REVIEW's own verification).

FINDINGS (test-authoring only -- no production code was touched by this gate)
- [Found and fixed] A first draft of the new `FilesErasureEndpointTests.
  StoredFileEraser_RefusesAHeldFile_TheObjectAndRowBothRemain` seeded the file, flipped
  `LegalHold` with a raw SQL `UPDATE` (there is no domain setter for it in this handover -- §8.1:
  "Set only by HO-XCT-001, not built here", always `false` out of `Register`), and then called
  `IStoredFileEraser.EraseAsync`, all through **one shared** `CatEcommerceDbContext` scope/scoped
  service resolution. `StoredFileEraser.EraseAsync` then threw `DbUpdateConcurrencyException`
  instead of returning `false`. Root cause: EF Core's change tracker resolves a query for an
  already-tracked primary key to the **in-memory, already-tracked instance** (identity
  resolution) rather than re-reading the row, so `EraseAsync`'s own `FirstOrDefaultAsync` returned
  the row as it looked **before** the raw SQL flip (stale `xmin`, `LegalHold` still logically
  `false` in that tracked instance) -- the subsequent `Remove`+`SaveChangesAsync` then issued a
  `DELETE ... WHERE "Id" = x AND xmin = <stale>`, which the database's real (raw-SQL-bumped) xmin
  no longer matched, producing the concurrency exception. This is a test-isolation mistake in the
  test itself, not evidence that `StoredFileEraser`'s `LegalHold` check is broken -- fixed by
  using three separate scopes (seed / flip via SQL / erase-and-verify), which is also how a real,
  separate-request erasure would actually run. Re-run after the fix: 4/4 green in this file, then
  413/413 in the full suite, twice.
- No other test-authoring or production defect was found while writing or running any of the new
  or existing tests in this pass.

WEAK EXISTING TEST, STRENGTHENED (not deleted, not weakened)
- U11 (`PurgeEligibilityTests`, 5 facts, all green): tests `PurgeEligibility.IsEligible`, a shared
  static predicate that -- on inspection of both real consumers -- **neither `StoredFilePurgeSource`
  nor `GetFilesForPurgeQueryHandler` actually calls**; each duplicates the identical
  `LegalHold`/`RetainUntil` logic inline as its own EF `Where` clause instead (EF Core does not
  reliably translate an arbitrary static-method call inside a LINQ-to-SQL query). U11 as written
  would therefore still pass even if the *real* SQL predicate in `StoredFilePurgeSource` regressed
  -- it verifies correct logic that is disconnected from the code path it is meant to guard.
  Not fixed here (that would mean editing production code, out of scope for a TEST gate), but
  closed for real coverage by a new integration test,
  `FilesEndpointTests.Purge_batch_is_not_starved_by_held_rows_ordered_before_eligible_ones`, which
  seeds five held (future-`RetainUntil`) rows with an *earlier* `UploadedOn` than three genuinely
  eligible rows and purges with a batch size equal to the held-row count -- proving the SQL-side
  (not in-memory, not post-`Take`) filtering the CODE_REVIEW/ARCHITECTURE gates asked for actually
  holds. Recorded as a "Gap left deliberately" in the TC sheet (whether to wire `PurgeEligibility`
  into the two real queries, or delete it, is a production-code decision for the next gate/owner).

TESTS ADDED (all confirmed passing individually and as part of the full suite; see TC sheet for
the full mapping to §15/§16 IDs, ARCHITECTURE/SECURITY/CODE_REVIEW findings and Amended-row text)
- `FilePurposeAccessTests` (new, Domain.Tests, 16 facts) -- the purpose gate's fail-closed
  behaviour (SECURITY MEDIUM, first pass) had no automated coverage at all: every
  `FilesApiFactory`-based integration test runs behind a deliberate test-only decorator that
  widens unmapped purposes back open, by design, for test convenience.
- `FileNameSanitizerTests` additions (`ForDownload` canonical-extension forcing, 9 cases; bidi/
  format-character stripping, 3 facts) -- SECURITY MEDIUM (both passes): `ForDownload` had zero
  unit tests before this gate; only the SECURITY gate's own throwaway probe had ever exercised it.
- `GcsFileStoreTests.OpenReadAsync_streams_the_full_object_content_back_byte_for_byte` (+
  `FakeStorageClient.ForObjectDownloadSuccess`) -- the happy streaming path had never been
  persisted as a test; only the failure/`leaveOpen` path was.
- `FilesErasureEndpointTests.cs` (new, 4 facts) -- `FilesPersonalDataEraser` (ARCHITECTURE HIGH,
  C-ALL-2) and `IStoredFileEraser`'s `LegalHold` backstop (ARCHITECTURE HIGH, first pass) had no
  persisted test at all before this gate.
- `FileUploadConventionTests.cs` (new) -- A6 ("no second upload path") had no convention test;
  reflects over every controller action in the Api assembly (matching
  `EndpointAccessConventionTests`'s own `ControllerFeatureProvider` technique) and fails if
  anything besides `FilesController`'s two upload actions carries
  `[DisableFormValueModelBinding]`, or if anything anywhere binds `IFormFile`/`IFormFileCollection`.
- `FilesEndpointTests.Upload_one_byte_over_the_purpose_cap_is_refused_as_FileTooLarge` -- U4 had
  only an aggregate-level (`Register`) re-check test; nothing proved the streaming per-chunk cap
  in `ProcessFileSectionAsync` returns `400 FileTooLarge` over real HTTP.
- `FilesEndpointTests.Purge_batch_is_not_starved_by_held_rows_ordered_before_eligible_ones` -- see
  "Weak existing test" above.
- `CatProfileProjectionEndpointTests.Host_ResolvesTheCrossModuleStoredFileSigningBatcherDependency`
  -- the ARCHITECTURE gate (second pass, LOW) flagged that `GetCatProfileQueryHandler` (owned by
  Cat Profiles) depending on `IStoredFileSigningBatcher` (owned by FIL's Read layer) is a DI edge
  across modules that only `ValidateOnBuild` would catch, and explicitly said "for TEST" -- this
  resolves it directly through `CatApiFactory`'s own host wiring.
- `uploadedOn` assertion added to `CatProfileProjectionEndpointTests.
  GetCat_FullProfile_ProjectsEveryFieldCorrectly` -- the ARCHITECTURE gate (second pass) restored
  `UploadedOn` to `CatPhotoViewModel` specifically so `POST /api/cats/{id}/photos`'s own `photoId`
  response would be reachable from a read again; nothing asserted it was actually serialised into
  the JSON response before this gate.

DELIBERATE GAPS (recorded, not silently dropped -- see TC sheet for the full "Gaps left
deliberately" / "Cases I could not run" tables)
- I10's memory-growth assertion (V4): three heuristics were tried and abandoned by the
  CODE_REVIEW gate-fix round for documented, reproducible reasons (background GC noise under
  real parallel test-collection execution, thread-pool thread reuse across tests). Per this
  gate's own task instruction, not re-added. What remains and DID run: a 20 MB round-trip smoke
  test, green.
- R6 (migration `DELETE FROM clinic.cat_photo;` behaviour against pre-existing rows): not
  exercisable without provisioning a second database at the pre-`AddFiles` schema state seeded
  with legacy-shaped rows -- judged disproportionate to the risk in this pass, given ARCHITECTURE
  and CODE_REVIEW already read the migration file directly and confirmed the ordering. `BLOCKED`,
  not silently dropped.
- R16 (malformed multipart -> 400/413, never 500): the SECURITY gate's own ~250 throwaway probe
  requests were never persisted as tests; porting even the core cases (no boundary at all,
  truncated preamble) was judged out of proportion to add wholesale in this pass, given the
  exception ordering is compile-enforced (`BadHttpRequestException : IOException`) and CODE_REVIEW
  independently confirmed the fix by reverting it locally. `BLOCKED`, flagged for a follow-up pass.
- Live GCS bucket (A4's live half, V1/V2/V5) and a real `clamd` (V3): no such infrastructure is
  reachable in this sandbox, consistent with every earlier gate's own finding (§1 Amended (7)).

ACCEPTANCE CRITERIA (§18) -- ALL OBSERVED PASS FOR WHAT IS TESTABLE IN THIS ENVIRONMENT
A1-A3, A5, A7: fully PASS. A4: PASS for the signed-URL/never-served-without-a-signature half and
the bucket-policy-assertion unit tests (against `FakeStorageClient`); the live-bucket half is
BLOCKED (no bucket available). A6: PASS -- closed by the new convention test in this pass (was a
real gap before it).

SCOPE CHECK
- Files changed by this gate, all under `tests/**` or `docs/**`, none under `src/**`:
  - `tests/CatEcommerce.Domain.Tests/Common/FileNameSanitizerTests.cs` (extended)
  - `tests/CatEcommerce.Domain.Tests/Common/FilePurposeAccessTests.cs` (new)
  - `tests/CatEcommerce.Infrastructure.Tests/Files/FakeStorageClient.cs` (extended)
  - `tests/CatEcommerce.Infrastructure.Tests/Files/GcsFileStoreTests.cs` (extended)
  - `tests/CatEcommerce.IntegrationTests/Endpoints/CatProfileProjectionEndpointTests.cs` (extended)
  - `tests/CatEcommerce.IntegrationTests/Endpoints/FilesEndpointTests.cs` (extended)
  - `tests/CatEcommerce.IntegrationTests/Endpoints/FilesErasureEndpointTests.cs` (new)
  - `tests/CatEcommerce.IntegrationTests/Unit/FileUploadConventionTests.cs` (new)
  - `docs/test-cases/TC-HO-FIL-001.md` (new)
  - `docs/gates/HO-FIL-001-TEST.md` (this file)
- No production code (`src/**`), no migration, no `appsettings*`, no `docker-compose.yml` was
  touched. `web/public/mockServiceWorker.js` was regenerated as a side effect of `npm ci` and
  reverted before finishing (see "How it was checked").
- Nothing was committed and nothing was pushed, per the task's own instruction.

VERDICT: PASS. Build is clean (0 warnings). Every §15/§16 case (U1-U12, I1-I10), every §18
criterion (A1-A7), and every behaviour-changing Amended-row item named in the task brief is
mapped to an executed, passing test, a documented `BLOCKED` case, or a documented deliberate gap
-- none is silently absent. Eight genuine coverage gaps (purpose-gate fail-closed, `ForDownload`/
bidi stripping, the personal-data eraser, the `LegalHold` eraser backstop, GCS happy-path
streaming, the A6 convention, the cross-module DI edge, and purge batch-starvation) were found and
closed with new tests in this pass, all green. One test-authoring bug (not a production defect)
was found and fixed while writing those tests. Three items remain `BLOCKED` for infrastructure or
proportionality reasons already accepted by earlier gates (live GCS/clamd, the migration's
pre-existing-row behaviour, and the malformed-multipart probe set) -- none is a HIGH/MEDIUM risk
left uncovered by design; each is recorded, not hidden.
