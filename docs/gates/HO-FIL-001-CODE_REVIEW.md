GATE: CODE_REVIEW
HANDOVER: HO-FIL-001
COMMITS: 4fc20fd..6b6256c (branch ho/FIL-001, head 6b6256c; excludes the gate-record-only commits
9ee2720, fdf065c in that range) — second pass, combined delta covering both the ARCHITECTURE
second-pass fix round and the SECURITY second-pass fix round, also reviewed from the SECURITY
angle per task instruction.
DATE: 2026-09-27
STATUS: PASS

Pass: second CODE_REVIEW pass. Read HO-FIL-001-CODE_REVIEW.md (pass 1, PASS with 2 MEDIUM + 2 LOW),
HO-FIL-001-SECURITY.md (both passes; second pass PASS with 4 LOW carried) and
HO-FIL-001-ARCHITECTURE.md (both passes) first, so as not to duplicate either gate's own findings.
Reviewed every commit in the range line by line, not just the net diff, so an intermediate
fix-then-refix (e.g. `AddCatPhoto` moving from two-aggregate to one-aggregate across 8dc02ca vs. the
earlier a48e73c) was read in its final, integrated form.

How it was checked:
- Read every file the range touches (FilesController, GcsFileStore, GcsBucketPolicyChecker,
  StoredFileAggregate, AddCatPhotoCommandHandler, UniqueConstraintExceptionFilter,
  FilesPersonalDataEraser, StoredFilePurgeSource, FileRateLimitPolicies/RateLimitPolicies,
  SettingKeys(.Files), IStoredFileSigningBatcher/StoredFileSigningBatcher, the regenerated AddFiles
  migration, EicarTestScanner, RegisterStoredFileCommandHandler, DeleteStoredFileCommandHandler,
  CatMapper/FileMapper).
- Fresh `git archive 6b6256c` into a NEW scratch folder (no git-ignored appsettings):
  `dotnet build CatEcommerce.sln -c Debug` — 0 warnings, 0 errors.
- `dotnet test --no-build` per unit-test project:
  - CatEcommerce.Domain.Tests: 287/287.
  - CatEcommerce.Application.Tests: 81/81.
  - CatEcommerce.Infrastructure.Tests: 55/55.
- Fresh-ish Postgres database `catecommerce_fil` (already at migration `20260927054251_AddFiles`
  from a prior gate run against this same head; `dotnet-ef migrations list` confirmed no drift, so
  no `database update` was needed). `CATECOMMERCE_TEST_DB` set to it, `TEMP`/`TMP` redirected into
  the scratch folder so file-store paths were isolated.
- `dotnet test tests/CatEcommerce.IntegrationTests --no-build`: 338/338 on the first run.
- `CatPhotoAttachEndpointTests` (the concurrent-attach test) re-run 5 more times in isolation to
  check for flakiness given the file-level race it exercises: 3/3 passed, all 5 times — no flake
  observed.
- Two regression probes, done by deliberately reverting one line at a time in the scratch checkout,
  re-running the specific test, then restoring the file from a backup and confirming the full build
  was clean again afterwards (source tree in the scratch folder only; the worktree under review was
  never touched):
  - `GcsFileStore.cs`: changed `pipe.Writer.AsStream(leaveOpen: true)` back to `leaveOpen: false`.
    `GcsFileStoreTests.OpenReadAsync_a_mid_download_failure_surfaces_as_an_exception_on_the_reader_not_a_clean_truncated_end`
    then failed (`Assert.NotNull() Failure: Value is null` — the reader saw a clean EOF instead of
    the exception). Restored, re-ran: passes. The test is real, not tautological.
  - `FileStorageModule.cs`: moved the two `configuration.GetSection("RateLimiting:File...")` reads
    from inside the `AddRateLimiter(options => ...)` delegate to before the `AddRateLimiter(...)`
    call (reproducing the exact eager-read shape the gate-fix's own doc comment describes as the
    bug). Both `Upload_OverLimit_ThirdReturns429` and `Serve_OverLimit_ThirdReturns429` then failed
    (`Expected: TooManyRequests / Actual: Created` and `.../ Actual: OK` — the tight limits from
    `WithTightFileRateLimits()` were never applied, so nothing 429'd inside the 3-request loop).
    Restored, re-ran: passes. The 429 tests are real, not tautological, and would have caught the
    exact bug the second-pass fix describes.
- Scratch folder deleted after the run (only that folder; nothing under the reviewed worktree was
  modified).

FINDINGS
- [LOW] src/Api/CatEcommerce.Api/Controllers/FilesController.cs:412 and :421 (the two
  `fileStore.DeleteAsync(putResult.Value!, ct)` compensating-delete calls added/kept by this
  round's "compensation on throw" fix) — both pass the request's own `ct` (bound to
  `HttpContext.RequestAborted` via ASP.NET Core's automatic `CancellationToken` parameter binding),
  not `CancellationToken.None`, to the cleanup call itself.
  - The fix's own doc comment frames the scenario as "the save THROWS (e.g. a DB outage/timeout)",
    but the same catch block also fires when the CLIENT disconnects while `dispatcher.SendAsync`
    is in flight — `ct` observes that as cancellation too, and by the time the `catch` runs, `ct`
    is already in the cancelled state.
  - `GcsFileStore.DeleteAsync` forwards that same token straight through to
    `client.DeleteObjectAsync(bucket, storageKey, cancellationToken: ct)` (GcsFileStore.cs:125).
    An already-cancelled token passed to that call will not delete the object — the SDK either
    throws `OperationCanceledException` before issuing the request or the request itself is
    aborted, and the exception is swallowed here (the `catch` block's own `throw;` re-throws the
    ORIGINAL `dispatcher.SendAsync` exception, not a secondary failure from the delete). Net
    effect: on a client abort mid-dispatch against the GCS store, the object is silently orphaned —
    exactly the outcome ("invisible to retention and erasure") this round's own fix was written to
    close, just for a different trigger (client abort vs. genuine save failure) than the one the
    comment names.
  - `LocalFileStore.DeleteAsync` ignores its `ct` parameter entirely (LocalFileStore.cs:51-57), so
    this is invisible in every test here (Development/Testing never construct `GcsFileStore`) and
    was not caught by I4 or by this round's own new tests.
  - A precedent for the right shape already exists in this same round:
    `DeleteStoredFileCommandHandler.HandleAsync` deliberately calls
    `eraser.EraseAsync(command.FileId, CancellationToken.None)` (not the ambient command token) for
    exactly this reason — a delete that exists to keep state consistent should not be skippable by
    the same cancellation that triggered it.
  - Fix: pass `CancellationToken.None` (or a short-lived, independent token) to both
    `fileStore.DeleteAsync` calls in `ProcessFileSectionAsync`, matching
    `DeleteStoredFileCommandHandler`'s own pattern.

Everything else asked for in this round is either fixed correctly, or fixed and demonstrated
correct by a test that would fail without the fix (confirmed above by reverting each one):

- **FilesController exception ordering** — `catch (BadHttpRequestException)` before
  `catch (OperationCanceledException) when (ct.IsCancellationRequested)` before
  `catch (InvalidDataException)` before `catch (IOException)`, both around the section loop and
  around the temp-file write window. `BadHttpRequestException : IOException`, so this order is
  required for the code to compile at all (a base-type catch before a derived-type catch is a C#
  compile error) — confirmed by the clean build. The narrower, inner `IOException` catch (temp-file
  write only) no longer swallows `PutAsync`/dispatch failures, closing the first pass's own LOW.
- **Client-abort handling** — a cancelled `ct` during the multipart read is now a quiet
  `EmptyResult()` logged at Information, not an unhandled exception logged at `fail` with a stack
  trace.
- **Compensation on throw** — `dispatcher.SendAsync` is now wrapped in its own try/catch that
  deletes the just-`PutAsync`'d object and re-throws (see the one LOW above about the token it
  passes to that delete).
- **Single dispatch** — the upload actions now return `RegisterStoredFileCommand`'s own
  `CommandResponse.Result` (a `StoredFileRegistrationResult` built inside
  `RegisterStoredFileCommandHandler` from the aggregate it just saved plus one `IFileUrlSigner.Sign`
  call) directly from `CreatedAtAction`, rather than dispatching a second `GetStoredFileQuery`
  immediately afterward. `GetStoredFileQuery`/`GetStoredFileQueryHandler` are kept (now a thin
  wrapper over `IStoredFileSigningBatcher`) for a future single-file lookup by a different consumer,
  not dead code.
- **`ForDownload` filename** — `FileNameSanitizer.ForDownload` discards the stored name's own
  extension entirely and substitutes the canonical one for the verified, stored `ContentType`
  (8-entry map matching exactly the 8 content types `FileRules` allows); `Sanitize` additionally
  strips Unicode category `Cf` (format/bidi) alongside `Cc` (control) at upload time. Matches the
  SECURITY second-pass probe results verbatim (`cat.hta` (PNG) → `cat.png`, U+202E stripped, etc.).
- **CSV whole-file deny scan** — wired correctly: `FilesController.ProcessFileSectionAsync` calls
  `FileRules.ContainsDeniedCsvSignatureAsync` on a fresh read-only handle to the already-written
  temp file, only when `declaredContentType == "text/csv"`, after the size loop and before the
  malware scan, and maps a hit to `FileContentMismatch` — same violation code and shape as the
  existing magic-byte mismatch path. The rolling-window implementation mirrors
  `EicarTestScanner`'s own technique (chunk + `signature.Length - 1` overlap), correctly handling a
  signature split across a chunk boundary.
- **`GcsFileStore` Pipe streaming** — `OpenReadAsync` now does a cheap `GetObjectAsync` existence
  check, then streams via `Pipe`, with the download running on a background `Task.Run` that writes
  through `pipe.Writer.AsStream(leaveOpen: true)` and always completes the writer itself (success or
  with the caught exception) in a `finally`-equivalent `try/catch`. `leaveOpen: true` is the load-
  bearing fix — confirmed by reverting it (see above): without it, `await using (writerStream)`'s
  own disposal calls `PipeWriter.Complete()` unconditionally as the `await using` block unwinds
  (exception path included), racing ahead of the `catch` block's own `CompleteAsync(ex)` and
  reporting a clean, truncated EOF to the reader instead of the real failure. No deadlock if the
  reader stops early: `pipe.Reader.AsStream()`'s disposal completes the reader, which unblocks any
  pending `Writer.FlushAsync()` in the background task with `FlushResult.IsCompleted = true` rather
  than blocking on backpressure forever (`System.IO.Pipelines`' own documented behaviour); separately,
  the background task is passed the request's own `ct`, so a client disconnect on the *reading* side
  (`GetFileContent`'s own `ct`) also cancels the `DownloadObjectAsync` call directly. Cancellation
  and early-reader-exit are two independent, both-safe paths to the same background task ending.
- **`GcsBucketPolicyChecker`** — asserts uniform bucket-level access, enforced public-access
  prevention, versioning off and soft-delete off; called from the startup guard outside
  Development/Testing, alongside a forced `StorageClient` resolution so missing ADC now fails
  startup instead of the first request. Covered by `GcsBucketPolicyCheckerTests.cs` (one test per
  control, using the hand-written `FakeStorageClient`).
- **`StoredFileAggregate.IsEligibleForAttach` + `Register` re-validation** — `IsEligibleForAttach`
  is a pure, side-effect-free predicate (purpose, uploader, freshness window) with no state
  mutation and nothing to save; "one file, one cat" is enforced purely by
  `ix_cat_photo_file_id`. `Register` independently re-checks `FileRules` (allowed content type,
  size cap) against its own `dto`, matching `CatAggregate.AddPhoto`'s existing defence-in-depth
  precedent rather than trusting the upload pipeline's earlier check alone.
- **`AddCatPhotoCommandHandler` single-aggregate** — confirmed by reading the handler: it calls
  `fileRepository.GetByIdAsync` (read) and `catRepository.UpdateAsync` (the only write). The
  `StoredFileAggregate` returned by the repository is never passed to `UpdateAsync`/`SaveAsync`
  anywhere in this handler.
- **`UniqueConstraintExceptionFilter` mapping for `ix_cat_photo_file_id`** — maps to the same
  `400 PhotoFileNotEligible` with a generic message and no distinguishing detail, matching the
  ordinary ineligibility refusal from the handler's own pre-check, via the same
  `CatBadRequestIndexes` dictionary pattern already used for the other per-area constraint maps
  in this filter. No information leak between "unique-index race lost" and "ordinary refusal".
- **`FilesPersonalDataEraser` + legal hold** — redacts `UploadedBy`/`OriginalFileName` via
  `StoredFileAggregate.RedactUploaderPersonalData` (never raw SQL); skips (and reports `Held`) any
  row with `LegalHold == true` or a future `RetainUntil`, tracking the earliest held-until date
  across all of a customer's files; idempotent (checks the sentinel redacted values before acting
  again).
- **`StoredFilePurgeSource` SQL-side filtering / one source per purpose** — the
  `LegalHold`/`RetainUntil` predicate now lives in the `Where` clause before `Take(batchSize)`
  (previously in-memory, after the batch was already fetched — closes the first pass's own LOW).
  `FileStorageModule` now registers one `IRetentionPurgeSource` per `FilePurpose` via
  `Enumeration.GetAll<FilePurpose>()`, each with its own `SourceKey` (`"stored-file:{purpose}"`),
  each constructed with its own captured `purpose` — the classic captured-loop-variable bug does
  not apply here since `capturedPurpose` is a fresh local per iteration and C# 5+ loop-variable
  semantics give `purpose` its own scope per iteration regardless. This is the "`GetServices` fix"
  the task refers to: a future retention worker resolving `IEnumerable<IRetentionPurgeSource>` now
  gets one instance per purpose, not one shared instance with no purpose filter.
- **`FileRateLimitPolicies` lazy config read** — `configuration.GetSection(...).Get<...>()` calls
  are made INSIDE the `AddRateLimiter(options => ...)` delegate, which is invoked lazily on first
  `RateLimiterOptions` materialisation, not at module-registration time. Confirmed both that this
  is necessary (reverting to an eager read outside the delegate breaks
  `WithTightFileRateLimits()`'s `ConfigureAppConfiguration` override, exactly as the doc comment
  predicts) and that the two dedicated tests (`Upload_OverLimit_ThirdReturns429`,
  `Serve_OverLimit_ThirdReturns429`) genuinely fail without the fix, not just theoretically.
- **`SettingKeys` partial** — `SettingKeys.Files.cs` adds FIL's three keys in their own file;
  `SettingKeys.cs`'s own pre-existing content is untouched beyond the `partial` modifier, matching
  the per-area-partial convention already used for `ActionRoles`/`RecordClassification`.
- **`IStoredFileSigningBatcher`** — one query for however many file ids are requested
  (`Where(f => fileIds.Contains(f.Id))`), `AsNoTracking()` + anonymous-type `Select` projection, one
  `IFileUrlSigner.Sign` call per row in memory — no N+1 dispatch, no aggregate rehydration for a
  read (`.ai/read-side-pattern.md`). `GetStoredFileQueryHandler` and (per the ARCHITECTURE gate)
  `GetCatProfileQueryHandler` both now go through it instead of duplicating the mapping.
- **The migration's `DELETE`** — `DELETE FROM clinic.cat_photo;` runs before the column
  drop/add, is well-justified in its own comment (every pre-existing row was written under the
  client-trusted shape this handover replaces, has no `FileId` to migrate to, and the unique index
  on `FileId` would refuse to build over duplicate `Guid.Empty` defaults regardless) and is
  correctly ordered — the table is empty by the time the `NOT NULL` `FileId` column with a default
  is added, so no constraint violation is possible on non-empty pre-existing data. A one-time,
  visible data loss on upgrade, not a silent one; acceptable pre-launch.
- **I10 memory heuristic removed — what still guards against full buffering**: for the SERVE
  (download) path, the `Pipe`'s own backpressure bounds memory regardless of object size (verified
  in the SECURITY second pass by measuring a 60 MB fake download; also structurally true of any
  `Pipe`-based bridge). For the UPLOAD path, nothing at runtime does any more — the three earlier
  heuristics (absolute `GC.GetTotalMemory` delta, marginal delta, per-thread allocation counter)
  are removed with a documented, specific empirical failure mode for each (background GC noise from
  concurrent test collections, thread-pool thread reuse across tests). What remains is a smoke test
  (`A_large_upload_succeeds_and_is_served_back_intact`, 20 MB round-trip) plus an explicit comment
  that the no-full-buffering guarantee is now source-review-only for this path. This is a reasonable
  and honestly-documented trade-off — it directly resolves the first pass's own MEDIUM finding
  asking for exactly this choice to be made explicit rather than left as a loose, misleading
  threshold — but it is worth recording plainly: a future regression that reintroduces whole-file
  buffering in `ProcessFileSectionAsync`'s read loop will not be caught by the automated suite. Not
  a blocking finding; noting it so a later change to that method gets the extra scrutiny the removed
  test used to provide automatically.
- **Tests real and deterministic**:
  - The 429 tests and the `GcsFileStore` failure test were confirmed, by reverting each fix in turn,
    to fail without it (see "How it was checked" above) — not tautological.
  - `Concurrent_attach_of_one_file_to_two_cats_yields_exactly_one_row` passed 6/6 total runs (the
    full-suite run plus 5 isolated re-runs). Worth noting for the record: this test's invariant
    (exactly 1×`201`, the rest all `400 PhotoFileNotEligible`) holds only because the race that
    matters here is the database-level unique-index race on `ix_cat_photo_file_id`, not a
    `CatAggregate`-level optimistic-concurrency race — a `DbUpdateConcurrencyException` from two
    concurrent writers touching the *same* cat's own row would instead surface as `409` via the
    host-wide `ConcurrencyExceptionFilter` (Program.cs), not `400`, and was not observed in any of
    the 6 runs. If this ever becomes flaky under heavier load or a slower CI machine, that
    `CatAggregate`-row race — not the file-uniqueness race the test's own comment describes — is
    the first place to look.

BUILD/TEST
- `git archive 6b6256c` into a clean, new scratch folder: `dotnet build CatEcommerce.sln -c Debug` —
  0 warnings, 0 errors.
- `dotnet test --no-build` per unit-test project: Domain 287/287, Application 81/81,
  Infrastructure 55/55.
- Integration suite against `catecommerce_fil` (Postgres, already migrated to head):
  `dotnet test tests/CatEcommerce.IntegrationTests --no-build` — 338/338.
- `CatPhotoAttachEndpointTests` re-run 5 additional times in isolation: 3/3 every time.
- Regression probes (both reverted-then-restored in the scratch checkout only): the
  `GcsFileStore` leaveOpen fix and the `FileRateLimitPolicies` lazy-read fix each demonstrated to
  be load-bearing — their own tests fail without them and pass with them.
- Scratch folder deleted after the run.

SCOPE CHECK
- Non-gate files changed in 4fc20fd..6b6256c: 92. All are FIL's own layers (renamed `StoredFile`
  segment per ADR-125), the per-area partials (`SettingKeys.Files`, plus the pre-existing
  `RecordClassification.Files`/`SettingDefinitions.Files`/`BusinessViolationCodes.Files`), FIL's
  own `RateLimiting` file and `appsettings.json` entries, the regenerated `AddFiles` migration, the
  Cat Profiles files carried under §1 Amended (1)/(round 2) as the documented exception to §13
  (`AddCatPhotoCommand(Handler)`, `CatMapper`, `CatAggregate`/`CatPhoto`, `CatConfiguration`,
  `GetCatProfileQueryHandler`/`CatProfileViewModel`, the Cat-vaccine test body updates for the
  `{fileId, isPrimary}` shape change), `UniqueConstraintExceptionFilter.cs` under the same
  authorised exception, and `docs/handover/{HO-FIL-001,HO-CAT-001,HO-CAT-002,CONTRACTS}.md`
  themselves.
- files outside allowed scope: none beyond what ARCHITECTURE has already ruled on in
  HO-FIL-001-ARCHITECTURE.md (which owns the scope ruling for this handover, per the SECURITY
  gate's own established convention) — no new out-of-scope file was introduced by this delta that
  ARCHITECTURE's second pass had not already seen and accepted or flagged.

=====================================================================================================
FIRST PASS (kept for the record)
=====================================================================================================

GATE: CODE_REVIEW
HANDOVER: HO-FIL-001
COMMITS: eae3d7b..2a0ca74 (branch ho/FIL-001, head 2a0ca74)
DATE: 2026-09-26
STATUS: PASS

FINDINGS
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Files/GcsFileStore.cs:50-69 (OpenReadAsync) — §8.4 says "DownloadObjectAsync into the response stream"; this instead downloads the whole object into a `MemoryStream` before returning it. For a 50 MB `ReportExport`/`BoardingMedia`/`LabResult` object this fully buffers the file in server memory per concurrent read, which is exactly the kind of unbounded-memory risk V4 calls out for uploads (unverified in this environment per §1 Amended (7), so untested against a real bucket). Prefer streaming directly from `DownloadObjectAsync`'s destination stream into the HTTP response (e.g. a `Pipe`/`Stream` the controller writes through) rather than materialising the object in a `MemoryStream`.
- [MEDIUM] tests/CatEcommerce.IntegrationTests/Endpoints/FilesEndpointTests.cs:277-309 (I10) — not flaky, but the threshold (`Math.Max(largeContent.Length * 5, 150 MB)` = 150 MB for a 20 MB file) is loose enough that the test would pass identically even if `ProcessFileSectionAsync` regressed to buffering the entire 20 MB upload in one array/`MemoryStream` (20 MB ≪ 150 MB). As written it cannot actually catch the regression its own comment describes ("reading the whole request body into one buffer"); it only catches something an order of magnitude worse. Consider asserting a per-request bound tied to the fixed 80 KB read buffer (e.g. an allocation-count/`GC.GetAllocatedBytesForCurrentThread()` check scoped to just the upload call) rather than whole-process `GC.GetTotalMemory`, or explicitly documenting that this case is now source-review-only and not a real regression guard.
- [LOW] src/Api/CatEcommerce.Api/Controllers/FilesController.cs:314-326 (`catch (IOException ex)`) — wraps the entire pipeline (temp-file write, hashing, scan, `PutAsync`, command dispatch) and converts any `IOException` into `400 FileRejected`, logged at `Warning`. This is fine for the documented case (AV quarantining a temp file mid-write) but it also swallows unrelated infrastructure failures the same way — e.g. the local disk filling up while writing the temp file, or a transient network `IOException` from `GcsFileStore.PutAsync`'s underlying HTTP transport — reporting them to the caller as "your file was rejected" rather than a 5xx, and at `Warning` rather than `Error`. A systemic outage (e.g. disk full) would look like a wave of bad uploads rather than an infra alert. Consider narrowing the catch to the temp-file read/write step only, or logging at `Error` and/or returning `503`/`500` for `IOException`s that occur outside the AV-quarantine window (e.g. after the scan has already completed cleanly).
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Files/StoredFilePurgeSource.cs:29-35 — the eligibility filter (`LegalHold`, `RetainUntil`) is applied in memory after fetching `batchSize` candidates ordered by `UploadedOn`, not in the SQL query. If the oldest `batchSize` rows past the cutoff are mostly under `LegalHold` or a future `RetainUntil`, a purge run can do no real work even though later, genuinely-eligible rows exist beyond the batch. Not exercised by a consumer yet (HO-XCT-001 not built), but worth pushing the eligibility predicate into the `Where` clause before `Take(batchSize)` when that worker lands.

Everything else reviewed is clean:
- `FilesController`'s streaming pipeline (`DisableFormValueModelBinding` + `MultipartReader`) enforces the size cap per-chunk before writing to disk or hashing, uses a bounded 80 KB buffer throughout (no full-body buffering), computes the SHA-256 incrementally, and disposes the temp `FileStream`/scan/put streams via `await using` on every path, with `finally { TryDeleteTempFile(...) }` guaranteeing temp-file cleanup on success, validation failure and exception alike.
- `DisableFormValueModelBindingAttribute` removing `FormFileValueProviderFactory` in addition to the two classic-sample factories is correct for ASP.NET Core 9 and well justified (confirmed independently: `FormFileValueProviderFactory.CreateValueProviderAsync` calls `ReadFormAsync()` unconditionally); without it every multipart upload would fail as the doc comment describes.
- The compensating delete in §8.2 step 7 (`fileStore.DeleteAsync` after a failed `RegisterStoredFileCommand`) is present and covered by I4.
- `ClamAvScanner` fails closed on both its own timeout and any other exception talking to `clamd`, never returning `Clean` in those paths; the three scanner stubs are each environment-gated in their own constructors, and `FileStorageModule`'s environment-based `IMalwareScanner`/`IFileStore` factories match §8.4/§8.5.
- `HmacFileUrlSigner.TryVerify` uses `CryptographicOperations.FixedTimeEquals`; the U8 IL-reflection test resolves the actual call-site metadata token rather than pattern-matching source text, so it will genuinely fail if a future edit swaps in `==`/`SequenceEqual` — a reasonably robust technique, not brittle in the usual sense (only meaningful caveat: `GetMethodBody()` would return null under NativeAOT, not a concern for this test host).
- `StoredFileAggregate.Register` is the only mutator, raises `StoredFileRegisteredEvent` and calls `UpdateVersion()`; no `Update`/rename member exists, matching §8.1.
- `LocalFileStore`/`GcsFileStore.PutAsync` write via `UploadObjectAsync`/an exclusive `FileStream`, both effectively atomic for this use (single `CreateNew`-equivalent write keyed by a fresh server-generated `StorageKey`, no read-modify-write); `LocalFileStore.ResolvePath` re-confines every key under `LocalStoreRoot` as a second guard against traversal even though keys are always server-generated.
- `AddCatPhotoCommandHandler`/`AddCatPhotoCommand`/`AddCatPhotoRequest` correctly closes the HO-CAT-002-SECURITY carried item: the command now carries only `FileId`; the handler loads `StoredFileAggregate` read-only and re-derives `StorageKey`/`ContentType`/`SizeInBytes`/`OriginalFileName` from it, never from client input; ownership is checked via `UserContext.UserId`, which is populated from the same `AccountId` the upload endpoint stamped as `UploadedBy` for both staff and customer callers (verified via `SessionUserContextProvider`'s doc comment) — no identifier mismatch.
- Validators are registered as `IValidator<TCommand>` in `FileStorageModule`, not by concrete type; query handlers use `AsNoTracking()` + `Select` projections, never rehydrating `StoredFileAggregate` for a read.
- `RecordFileRejectionAuditCommandHandler`'s use of `EventPublishOption.DoNotPublish` matches the established pattern used by every other audit-only handler in this codebase (not a new deviation).
- Migration `AddFiles` uses `timestamptz`/`date` correctly, adds the two indexes specified in §11.5, and the EF configuration's `Tags`/`Language`/`ServiceId`/`Metadata` handling mirrors every other aggregate configuration in the solution (base-class boilerplate, not FIL-specific).
- No superseded-architecture symbols (`IBusMessageDispatcher`, `AddServiceBusProvider`, MassTransit/RabbitMQ/MongoDB, `IReadRepository`, `ViewModelBase`, etc.) appear anywhere in the diff.

BUILD/TEST
- `git archive 2a0ca74` into a clean scratch folder (no git-ignored appsettings), `dotnet build CatEcommerce.sln -c Debug`: succeeded, 0 errors.
- `dotnet test` (no `--filter`, per project, `--no-build`) on the three unit-test projects only (integration tests excluded per task scope):
  - CatEcommerce.Domain.Tests: 277/277 passed.
  - CatEcommerce.Application.Tests: 79/79 passed.
  - CatEcommerce.Infrastructure.Tests: 52/52 passed.
- Scratch folder deleted after the run.

SCOPE CHECK
- files in allowed scope: 81/81 — everything under `Domain/Files`, `Application/.../Files`, `Application/.../Cat` (AddCatPhoto* — integrator-authorised per §1 Amended), `Infrastructure/Files`, `Api/Files`/`Api/Controllers/FilesController.cs`, `Api/Filters/DisableFormValueModelBindingAttribute.cs`, `Api/Modules/FileStorageModule.cs`, the `files` schema migration, `docker-compose.yml`'s `clamav` service, and `docs/handover/HO-FIL-001.md` itself.
- files outside allowed scope: none. The Cat Profiles touches (`AddCatPhotoRequest`, `CatsController`, `AddCatPhotoCommand(Handler/Validator)`, `CatMapper`, `BusinessViolationCodes.Cat.cs`) are explicitly authorised by §1 Amended (1) as the documented exception to §13, closing a named carried security item from `docs/gates/HO-CAT-002-SECURITY.md`.
