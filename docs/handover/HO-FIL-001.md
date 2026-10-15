# HO-FIL-001 — File Storage (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-FIL-001** |
| Title | File storage — upload, scan, store, serve, delete — backend slice |
| Date | 2026-09-23 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §17 — **US-XCT-07** (safe uploads). This slice builds the facility that story describes once, for every area that uploads a file |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **NFR-SEC-5** (uploads validated, scanned, never executable), NFR-SEC-8 (access logged by the consuming read), NFR-LEG-4, D-10, C-9 |
| Pairs with | [`HO-FIL-002`](HO-FIL-002.md) — the shared uploader and viewer. **§12.4 of this document is its contract** |
| Builds on | [`HO-ACC-001`](HO-ACC-001.md) (staff, sessions, audit, `ActionRoles`), [`HO-ADM-001`](HO-ADM-001.md) (settings registry, branches) |
| Consumed by | `HO-SHP-001` (product images), `HO-EMR-001` (clinical photos, lab results, consent forms), `HO-BRD-001` (belongings, diary, incident media), `HO-RPT-001` (generated exports), `HO-XCT-001` (retention purge, backup coverage) |
| Delivery order | Section 6 of 18 in [`README.md`](README.md#delivery-order) — **moved ahead of Shop**, because five areas were blocked on it (README C-5) |
| Baseline commit | `529ff4f` |
| Origin | **Extracted from [`HO-EMR-001`](HO-EMR-001.md) §8.10 (ADR-125)**, which designed this facility correctly but owned it in the wrong place. The ports, the rules and the serving path are that design; the adapters are new (ADR-318, ADR-319) |
| Amended | **2026-09-26 (implementation, integrator-authorised).** (1) Added `FilePurpose.CatProfilePhoto` and its `FileRules` row myself, instead of waiting for a consuming handover to add it — this closes a carried security item from `docs/gates/HO-CAT-002-SECURITY.md` (`POST /api/cats/{id}/photos` trusted a client-supplied `StorageKey`/`ContentType`/`SizeInBytes`/`FileName`). `AddCatPhotoRequest`/`AddCatPhotoCommand` (Cat Profiles' own files) now carry only a `FileId` minted by this slice's `POST /api/staff/files?purpose=CatProfilePhoto`; `AddCatPhotoCommandHandler` loads the referenced `StoredFileAggregate` and refuses (`PhotoFileNotEligible`) unless it is a `CatProfilePhoto`, was uploaded by the same caller, and is within a new `FileRules.AttachWindow` (30 minutes) of now — a FIL-owned policy for "how long a minted file reference stays valid for a consuming write", not a Cat-specific rule. Touching Cat Profiles' own files is outside this handover's normal §13 scope; recorded here as the explicit integrator authorisation the task brief already grants for this specific carried item. (2) §9 OQ-7 ("who may call the upload endpoint for each purpose") is implemented so that an *unmapped* purpose is still permitted for any authenticated staff member (the baseline `[RequireAction(Actions.Files.Upload)]` check) — `FilePurposeAccess` only *narrows* a purpose that a consuming area has explicitly registered (today, only `CatProfilePhoto`). Refusing every other canonical purpose by default would have made §16's I1 (`ProductImage`, owned by HO-SHP-001, not yet landed) fail, which would be inventing a rule on an unbuilt area's behalf (forbidden by §14). (3) `FilePurposeAccess` is a plain dictionary, not the `ActionRoles`/`SettingDefinitions` reflection-aggregated partial pattern — see that file's own doc comment for why. (4) `DeleteStoredFileCommand` is built per §11.3 but not exposed on a route in this slice — no consuming area calls it yet; it exists for `StoredFilePurgeSource` and a future consuming area's explicit retire-a-file flow. (5) `EicarTestScanner`'s constructor also throws outside Testing (only `DevelopmentNoOpScanner`'s guard is stated explicitly in §8.5) — added for symmetry/defence-in-depth; not a documented requirement, just consistent with the existing pattern. (6) `DisableFormValueModelBindingAttribute` removes a third value-provider factory, `FormFileValueProviderFactory`, beyond the two (`FormValueProviderFactory`, `JQueryFormValueProviderFactory`) the classic ASP.NET Core large-file-upload sample removes — found via a from-scratch repro against this solution's actual ASP.NET Core 9.0.5 runtime: without it, constructing that factory calls `ReadFormAsync()` regardless of whether any parameter binds `IFormFile`, consuming the entire multipart body before `MultipartReader` ever sees it. Every upload would otherwise fail "Unexpected end of Stream" unconditionally; see the attribute's own doc comment. (7) V3 (ClamAvScanner against a real `clamd`) and the GCS bucket assertions in §8.4 (V1/V2/V5) were not exercised end-to-end in this environment — no GCS bucket/ADC and no running `clamd` were available in this sandbox (§5's own ❌ rows); `docker-compose.yml`'s `clamav` service is provided for whoever next has a Docker registry pull path to it, but pulling the image and running V3 for real was not done here. `GcsFileStore`/`ClamAvScanner` are implemented against the verified package APIs (`Google.Cloud.Storage.V1` 5.0.0, `nClam` 9.0.0) and compile/pass their own unit-level guards, but are otherwise unverified against live services — flagged here rather than silently assumed working. |
| Amended | **2026-09-27 (gate-fix round: CODE_REVIEW pass 1 PASS, ARCHITECTURE pass 1 FAIL, SECURITY pass 1 FAIL).** Fixes below, one per commit on `ho/FIL-001`; §18/§16 tests re-run twice against `catecommerce_fil` after every commit that could affect them. (1) **C-ALL-1/C-ALL-2 (ARCHITECTURE HIGH):** added `RecordClassification.Files.cs` (`files.stored_file` classified `Media`/`Purgeable`) and `FilesPersonalDataEraser` (`IPersonalDataEraser`, `ContextKey = "files"`) — the table was previously unclassified and this context stored `UploadedBy`/`OriginalFileName` with no erasure registration. (2) **Cat photo stores a dangling key, not a `FileId` (ARCHITECTURE HIGH):** `CatPhoto` now stores only `{ Id, FileId, IsPrimary, UploadedOn }`; `CatAggregate.AddPhoto` no longer copies `StorageKey`/`ContentType`/`SizeInBytes`/`OriginalFileName` off the referenced `StoredFileAggregate`. `GetCatProfileQueryHandler` now batches `StoredFiles` metadata and issues a real signed URL per photo via `IFileUrlSigner`, closing the "cat photos can never be served" defect the gate identified. This is a schema change to `clinic.cat_photo`, folded into this branch's own `AddFiles` migration (see item 15). (3) **Attach-eligibility rule lived in a Cat handler (ARCHITECTURE HIGH):** moved onto `StoredFileAggregate.TryAttach(expectedPurpose, callerUserId, now, attachWindow, actor, correlationId)` — the aggregate's own state (`Purpose`, `UploadedBy`, `UploadedOn`, `IsAttached`) is what the rule was already checking; `AddCatPhotoCommandHandler` now just calls it and saves both aggregates (file first, matching the existing "file locked out is recoverable; two cats sharing one file is not" precedent). Raises `StoredFileAttachedEvent`. (4) **`LegalHold` unenforced on `IStoredFileEraser.EraseAsync` (ARCHITECTURE HIGH):** `StoredFileEraser.EraseAsync` now itself refuses (returns `false`, logs a warning) a held file, rather than relying on every caller to check first. (5) **GCS bucket assertions not built (ARCHITECTURE HIGH / SECURITY cross-ref):** `FileStorageStartupGuardHostedService` now resolves `StorageClient` from `IServiceProvider` at startup (forcing ADC resolution to fail fast instead of on first request) and calls a new `IGcsBucketPolicyChecker.AssertSecureAsync` (uniform bucket-level access, public-access prevention, versioning off, soft-delete off — ADR-318), outside Development/Testing only. (6) **§10.3 settings not registered (ARCHITECTURE MEDIUM):** added `SettingDefinitions.Files.cs` (`files.maxUploadMb`, `files.signedUrlMinutes`, `files.scanTimeoutSeconds`, all `BranchOverridable = false`, read via `IBusinessSettings`); removed the corresponding properties from `FileStorageOptions`, which now holds only what §10.3 actually reserves for configuration (bucket, credentials, scanner host, signing key). (7) **Namespace segment `Files` vs `StoredFile` (ARCHITECTURE MEDIUM, ADR-125):** renamed `Aggregates.Files`/`Events.Files`/`Commands.Files`/`CommandHandlers.Files`/`Queries.Files`/`QueryHandlers.Files` to `...StoredFile` throughout Domain/Application/Read (and the mirroring test folders), matching every other area's singular-feature-name convention. Infrastructure's `Files/` adapter folder and the Api layer's `FilesController`/`FileStorageModule` are unaffected — `.ai/namespace-conventions.md` scopes the per-feature segment rule to Domain/Application/Read; Infrastructure uses `Infrastructure.[Area]` and Api controllers are named after the resource (`AccountsController`/`CatsController` precedent). (8) **Second dispatch on upload (ARCHITECTURE MEDIUM):** `FilesController` no longer dispatches `GetStoredFileQuery` after `RegisterStoredFileCommand` succeeds — `RegisterStoredFileCommandHandler` now signs the URL itself and returns `StoredFileRegistrationResult` as the command's own `Result`, which the controller returns directly (one dispatch per request, `.ai/cqrs.md` §5 rule 1). (9) **`StoredFileAggregate.Register` didn't re-check `FileRules` (ARCHITECTURE MEDIUM):** `Register` now also validates `ContentType`/`SizeInBytes` against `FileRules.Find(Purpose)` as defence in depth, matching the precedent `CatAggregate.AddPhoto` already set of checking from the aggregate's own state. (10) **Purpose gate fails open (SECURITY MEDIUM):** `IFilePurposeAccessPolicy` (new port; `DefaultFilePurposeAccessPolicy` wraps the existing `FilePurposeAccess` registry) now fails closed — `TryGetRequiredStaffAction` returns `false` for any purpose with no registered `StaffPurposeActions` entry, and `FilesController` refuses with `FilePurposeNotAllowedForCaller` (403) rather than falling through to the baseline `[RequireAction(Actions.Files.Upload)]` check. This **reverses item (2) of the 2026-09-26 entry above** per the gate's explicit instruction that a test-fixture problem (§16 I1 needing `ProductImage`, owned by HO-SHP-001) is not a reason to widen production authorisation; `ProductImage` is now granted only inside `FilesApiFactory`'s own test-only `IFilePurposeAccessPolicy` decorator (`UnownedPurposeAllowingPolicy`), never in production code. Each consuming handover still adds its own `StaffPurposeActions` row when it lands. **Known regression, flagged for ACCEPTANCE (SECURITY gate's own NOTE, not a finding):** customers hold `Cats.EditProfile` but no `FilePurpose` is registered `IsAllowedForCustomer`, so a customer can no longer add their own cat's photo via the customer route; this fails closed rather than open, and is unchanged from before this round (no customer purpose existed previously either). (11) **Download file name not reconciled with content type; Unicode bidi/format characters kept (SECURITY MEDIUM, NFR-SEC-5):** `FileNameSanitizer.Sanitize` now strips Unicode categories `Cc`/`Cf` (control and format characters, including the U+202E RTLO trick); a new `FileNameSanitizer.ForDownload(storedFileName, contentType)` strips whatever extension the client-supplied name carried and appends the canonical extension for the verified, stored `ContentType`, called from `FilesController.GetFileContent` before `SetHttpFileName`. `FileRules.MagicBytesMatch`'s `text/csv` check also now rejects a `DeniedTextSignatures` list (`<!doctype`, `<script`, `<%`, `<hta:`, `#!/bin/`, …) so an HTA/HTML/shebang payload can no longer pass as "looks like text". (12) **`GcsFileStore.OpenReadAsync` buffered the whole object in memory (SECURITY MEDIUM / CODE_REVIEW MEDIUM):** rewritten to stream via a `System.IO.Pipelines.Pipe` bridging the SDK's destination-stream-only `DownloadObjectAsync` to a readable `Stream`, so memory use stays bounded regardless of object size. Unverified against a live bucket (§1 Amended (7) above still applies — no GCS bucket available in this sandbox). (13) **Malformed/oversized multipart became an unhandled 500 (SECURITY LOW):** `FilesController` now catches `BadHttpRequestException` (mapped to its own status, typically 413) **before** any `catch (IOException ...)` clause in the same block — confirmed via reflection that `Microsoft.AspNetCore.Http.BadHttpRequestException` derives from `System.IO.IOException`, so the ordering is load-bearing, not stylistic. The pre-existing `catch (IOException ex)` (added originally for the AV-quarantine case) is narrowed to wrap only the temp-file write/read window; `PutAsync` and the command dispatch are outside it, so a genuine GCS/DB outage is no longer misreported to the caller as "your file was rejected" (CODE_REVIEW LOW). (14) **Compensation only ran on `IsSuccess = false`, not on throw (SECURITY LOW):** `FilesController` now also deletes the just-stored object in a `catch` around `dispatcher.SendAsync(command)` that re-throws, so a DB outage/timeout no longer orphans an object invisible to retention/erasure. (15) **`EicarTestScanner` only scanned the first 4096 bytes (SECURITY LOW):** rewritten to scan the whole stream with a rolling window (keeping the trailing `EicarSignature.Length - 1` bytes across chunk boundaries), so I2's guarantee ("infected content never reaches the bucket") no longer depends on the payload's position in the file. (16) **No rate limit on uploads or serving (SECURITY LOW):** added `RateLimitPolicies.FileUpload` (20/10min, partitioned by caller id or IP) and `FileServe` (60/min per IP) policies, registered via `FileStorageModule`'s own additive `AddRateLimiter(...)` call (no `Program.cs`/`AccountsModule` edit — `UseRateLimiter()` is already called once); applied via `[EnableRateLimiting(...)]` on both upload actions and `GetFileContent`. (17) **`Cache-Control: no-store` instead of `private, no-store` (SECURITY LOW):** corrected to the §8.6 verbatim text — the two are equivalent in practice, but see item 19 below for why no test can actually observe the literal wire string. (18) **`StoredFilePurgeSource` filtered eligibility in memory after `Take(batchSize)` (CODE_REVIEW LOW):** the `LegalHold`/`RetainUntil` predicate is now applied in the SQL query itself, before `Take`, so a batch made up mostly of held/future-retained rows can no longer starve later, genuinely-eligible rows. Also now one instance is registered per `FilePurpose` (each with its own `SourceKey`), per the same finding's `.ai` note that a future retention worker wants per-purpose cutoffs. (19) **`TryParseEnum`'s bespoke `{ error }` body (CODE_REVIEW LOW):** now returns a standard `ValidationResponse` with a new `BusinessViolationCodes.FilesInvalidPurpose` code — deliberately **not** the generic `InvalidEnumValue` name the gate notes `ho/CAT-001-gates` independently coins for the identical defect in `CatsController`, since `BusinessViolationCodes` partial-class members are plain `const string`s with no reflection-based collision detection (unlike `ActionRoles`/`RecordClassification`): two branches independently declaring `public const string InvalidEnumValue` in different partials would be a straight compile error on merge, and neither branch can see the other's exact declaration to coordinate. (20) **Retired fakes left stale (CODE_REVIEW LOW):** `IFileStore`/`IMalwareScanner`/`IFileUrlSigner`'s doc comments and `docs/handover/CONTRACTS.md` rows 6–8 updated to describe the real adapters now that `Fakes/FileStorage/*.cs` (unregistered dead code) has been deleted. (21) **Test-only fixes found while re-verifying the whole round together:** `FilesApiFactory` now points `FileStorage:LocalStoreRoot`/`TempPath` at a per-factory-instance GUID subtree under `%TEMP%` instead of the fixed, machine-wide defaults (SECURITY gate's own I4 observation: objects left over from another branch's run against another database); a test-only `IFilePurposeAccessPolicy` decorator (`UnownedPurposeAllowingPolicy`) grants any purpose the real, now-fail-closed policy doesn't recognise the baseline `Files.Upload` action, so §16's I1/I3–I10 (which exercise the storage facility with `ProductImage`/`LabResult`/`ReportExport`, chosen for their `FileRules` shape, not their authorisation) keep passing without widening production code; I8 was found to resolve `IRetentionPurgeSource` via `GetRequiredService<T>()`, which silently returns whichever `FilePurpose`'s source was registered last rather than the `ProductImage` one the test seeds — a real bug in the one-source-per-purpose registration (item 18), now fixed by resolving via `GetServices<T>()` and matching `SourceKey`; I10's memory heuristic was replaced twice — first from an absolute `GC.GetTotalMemory` threshold too loose to catch the regression it claims to guard (CODE_REVIEW LOW), then, after that replacement itself produced a *negative* delta under the full suite's real parallel test-collection execution (proving process-wide `GC.GetTotalMemory` noise-cancellation false), replaced again with `GC.GetAllocatedBytesForCurrentThread()`, a per-thread monotonic allocation counter immune to other threads' GC activity, still using the same small-vs-large differential for residual scheduling noise; I5/I6's Cache-Control assertion was changed to check the parsed `Private`/`NoStore` flags rather than a literal string, since `HttpClient`'s `CacheControlHeaderValue` reformats every response into its own fixed token order regardless of the server's actual wire text, making the literal `"private, no-store"` string unobservable from any HTTP client-side assertion. `CatVaccineAuthorizationSecurityTests`/`CatVaccineFullAuthorizationMatrixTests` updated to POST `{ fileId, isPrimary }` instead of the pre-HO-FIL-001 `{ storageKey, contentType, ... }` body. (22) **`AddFiles` migration regenerated:** per the integrator's plan (this branch's own migration merges second against `ho/CAT-001-gates`' `WidenAuditEntryValues`), item (2)'s `clinic.cat_photo` schema change (`FileId` column + unique index, dropping `ContentType`/`FileName`/`SizeInBytes`/`StorageKey`) was folded into this branch's own `AddFiles` migration rather than added as a second one — regenerated by dropping and recreating the disposable `catecommerce_fil` database from scratch, since EF Core 9's `has-pending-model-changes` guard refuses a live `database update`/`migrations remove` once the model has already diverged from the last-applied migration. `has-pending-model-changes` is clean. (23) **Not actioned, left as before:** `FilePurposeAccess` remains a plain shared dictionary rather than an ADR-341 per-area partial (unchanged from item (3) of the 2026-09-26 entry — still tolerable with a single registered purpose; must become per-area before a second consuming handover lands in the same wave). **Not actioned — out of this branch's scope:** the unrelated, pre-existing flake in `DuplicateCustomerGroupsEndpointTests` (Administration area) observed once during a full parallel `dotnet test` run and confirmed to pass in isolation; not touched, as it belongs to a different bounded context this handover has no authorisation to edit. |
| Amended | **2026-09-27 (gate-fix round 2: CODE_REVIEW n/a this pass, ARCHITECTURE pass 2 FAIL, SECURITY pass 2 PASS).** Fixes below, one per commit on `ho/FIL-001`; full suite re-run against `catecommerce_fil` after every commit that could affect it. (1) **Two aggregates mutated in one command (ARCHITECTURE HIGH):** `AddCatPhotoCommandHandler` mutated both `StoredFileAggregate` (`TryAttach`+`UpdateAsync`) and `CatAggregate`, with the file save committing before Cat's own invariants ran — an ordinary refusal (photo limit, deceased cat) left the file permanently, uselessly claimed. `StoredFileAggregate.TryAttach` (a mutator) is replaced by `IsEligibleForAttach`, a side-effect-free query (purpose, uploader, freshness) with nothing to save; `IsAttached`/`AttachedOn`/`StoredFileAttachedEvent` are removed entirely (the event had no consumer). The handler now mutates only `CatAggregate`. "One file, one cat" is enforced instead by `clinic.cat_photo`'s own unique index on `file_id`, explicitly named `ix_cat_photo_file_id` so the host-wide `UniqueConstraintExceptionFilter` maps its `23505` to the same `PhotoFileNotEligible` 400 the handler's own pre-check returns — a concurrent attach of one file to two cats now races on that index (atomic) instead of across two separately-committed aggregate saves. New tests: a cat-side refusal (photo limit, deceased cat) leaves the file completely untouched (`fileRepository.Updated`/`.Saved` both empty); a 12-way concurrent attach of one file across two cats yields exactly one `cat_photo` row (`CatPhotoAttachEndpointTests.Concurrent_attach_of_one_file_to_two_cats_yields_exactly_one_row`). (2) **Customer purpose text was misleading (ARCHITECTURE MEDIUM):** the 2026-09-26 entry's item (10)/note said a customer could no longer add their own cat's photo; commit `a48e73c` had already registered `CatProfilePhoto` in `FilePurposeAccess.CustomerPurposes`, restoring that path — the row was simply never corrected. Fixed here, and `CatPhotoAttachEndpointTests` now proves the customer path end to end: own cat → 201 and served back with a signed URL; another customer's cat → 404 (`OwnerScope`, before the handler runs); a wrong-uploader attach → 400 `PhotoFileNotEligible`. (3) **Destructive migration (ARCHITECTURE MEDIUM):** the `AddFiles` migration's `clinic.cat_photo` change (drop `StorageKey`/`ContentType`/`SizeInBytes`/`FileName`, add `FileId` `NOT NULL` + a unique index) would fail outright on any database already holding two or more photo rows, and would silently turn a single existing row into a photo pointing at `Guid.Empty` (which the read then hides). Every pre-FIL `cat_photo` row was written under the very client-trusted-`StorageKey` shape this handover exists to close, and none has a real `files.stored_file` row to point at, so there is no `FileId` to migrate them to — the migration now runs `DELETE FROM clinic.cat_photo;` explicitly, before the column/index changes, with the rationale recorded inline in the migration file itself. A customer or clinic with pre-existing cat photos sees an empty photo list until photos are re-uploaded through the new flow — a one-time, visible loss on upgrade, not a silent one. No environment this handover has access to holds real `cat_photo` data. (4) **Authorisation for the wider Cat Profiles file set (ARCHITECTURE MEDIUM):** the 2026-09-26 entry's item (1) authorised touching Cat Profiles' files for the carried HO-CAT-002-SECURITY item, but did not explicitly cover the full file set this round and the first gate-fix round actually touched: `CatAggregate.cs`, `CatPhoto.cs`, `CatPhotoDto.cs`, `CatConfiguration.cs` (including `UniqueConstraintExceptionFilter.cs`, HO-ACC-001's own file, for the `ix_cat_photo_file_id` mapping — item (1) above), `GetCatProfileQueryHandler.cs`, `CatProfileViewModel.cs`, the `clinic` DDL in `AddFiles`, `BusinessViolationCodes.Cat.cs`, and the Cat-side test files (`CatAggregateTests.cs`, `AddCatPhotoCommandHandlerTests.cs`, `CommandValidatorsTests.cs`, `CatVaccineAuthorizationSecurityTests.cs`, `CatVaccineFullAuthorizationMatrixTests.cs`). This row extends the existing authorisation to cover that full set. **Amended rows recording the new photo read shape were added to [`HO-CAT-001`](HO-CAT-001.md) and [`HO-CAT-002`](HO-CAT-002.md) themselves** (their own §1 Amended, same date) — `CatPhotoViewModel` gains back `Id` and `UploadedOn` (restored below) alongside the §12.4 signed fields, so `POST /api/cats/{id}/photos`'s own `photoId` response is reachable from a read again. (5) **`SettingKeys.cs` not partial (ARCHITECTURE MEDIUM, scope):** made `static partial`; File's three keys moved to `SettingKeys.Files.cs`. The base file's pre-existing content is untouched, to minimise conflict with `ho/NTF-001`'s identical change. (6) **`FileUpload`/`FileServe` in HO-ACC-001's `RateLimitPolicies.cs` (ARCHITECTURE MEDIUM, scope failure):** moved to a new `FileRateLimitPolicies.cs`, matching `ho/NTF-001`'s own precedent. Their numbers are now bound from the shared `RateLimiting` config section (`appsettings.json` gains `FileUpload`/`FileServe` entries) instead of being hard-coded — and while wiring the 429 tests this asked for, found a real bug: the config read was computed as a local variable *before* calling `AddRateLimiter(options => ...)`, which reads stale, pre-test-override configuration, because that delegate is a `services.Configure<RateLimiterOptions>` callback evaluated lazily on first materialisation, not at registration time (proven via a startup-time diagnostic exception showing the eager read saw the `appsettings.json` default even though `IConfiguration` resolved later from the same host's own DI container already showed the override). Moved the reads inside the delegate, matching `AccountsModule`'s own (correct) `RateLimitingOptions` read. `Upload_OverLimit_ThirdReturns429`/`Serve_OverLimit_ThirdReturns429` (via a new `FilesApiFactory.WithTightFileRateLimits()`) now prove a 429 on each route. (7) **SECURITY LOWs:** `catch (IOException)` added after `catch (BadHttpRequestException)` in `UploadAsync`'s section loop (a body with no boundary/a truncated preamble previously threw a plain, uncaught `IOException` → unhandled 500); a client abort (`OperationCanceledException`) is now a quiet, `Information`-logged empty response. `GcsFileStore.OpenReadAsync`'s `pipe.Writer.AsStream()` now uses `leaveOpen: true` — the default (`false`) made disposing that stream call `PipeWriter.Complete()` with no exception, unconditionally, before the `catch` block's `CompleteAsync(ex)` ever ran, so a mid-download failure reached the reader as a clean, truncated end of stream rather than an exception (a new `GcsFileStoreTests.cs`, driven by an extended `FakeStorageClient`, proves a mid-download throw now surfaces on the reader — confirmed the test genuinely fails without the fix by reverting it locally). `FileRules.ContainsDeniedCsvSignatureAsync` scans the **whole file** for the markup/script deny-list with a rolling window (mirroring `EicarTestScanner`), not just the 256-byte magic-byte sample — closing the probe that found a benign CSV prefix followed by an HTA payload passing. §21 (new, below) documents `RequestOrigin:TrustedProxies`/`TrustedNetworks` as a deployment prerequisite for `FileServe`'s per-IP limit, matching the same requirement every other IP-keyed control in this solution already has (HO-ACC-001 precedent) — not a new requirement, just documented for this consumer of it. (8) **LOWs:** `RegisterStoredFileCommandHandler` takes `TimeProvider` instead of `DateTimeOffset.UtcNow`. `BusinessViolationCodes.Cat`'s unused `InvalidPhotoContentType`/`PhotoTooLarge` removed. A FIL-owned `IStoredFileSigningBatcher`/`StoredFileSigningBatcher` (Read.QueryHandlers.StoredFile) now owns the "file ids → §12.4 signed shape" mapping in exactly one place — `GetCatProfileQueryHandler` and `GetStoredFileQueryHandler` (now a one-line wrapper) both use it, instead of `GetCatProfileQueryHandler` re-implementing the metadata-plus-sign query against `files.stored_file` itself. **Correction (gate-fix 2026-09-27, ARCHITECTURE LOW, third pass):** this row originally also claimed `GetCatProfileQueryHandler` takes `TimeProvider` — it does not; the clock read for `urlExpiresOn` moved into `StoredFileSigningBatcher` when the batcher was extracted, and *that* class now takes `TimeProvider` instead of `DateTimeOffset.UtcNow`. |
| Amended | **2026-09-27 — integrator (ARCHITECTURE pass 4 LOW).** The master merge `eb63f0f` also changed `tests/CatEcommerce.IntegrationTests/CatApiFactory.cs` (file-storage signing key and isolated local store paths) and `tests/CatEcommerce.IntegrationTests/Endpoints/CatProfileProjectionEndpointTests.cs` (rewritten for upload-then-attach by `fileId`). Both were required by the gate's merge instructions and are integrator-authorised. |
| Amended | **2026-10-15 — integrator (HO-BRD-001 merge, mirror).** HO-BRD-001 mapped the two boarding purposes this handover pre-declared but left unmapped in `FilePurposeAccess`: `BoardingMedia` -> `Boarding.RecordCare`, `BoardingBelongings` -> `Boarding.CheckInOut` (CONTRACTS row 58). Other purposes are unchanged and still fail closed. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Give the solution **one** way to accept a file from a person, satisfy itself that the file is what it
claims to be and is not malicious, keep it somewhere durable that is not the database, hand it back
to someone entitled to see it, and delete it when the retention rules say so.

Five areas need this: a shop manager uploading a product photo, a vet attaching a lab PDF, a
caretaker photographing a boarded cat, a manager downloading a generated export, and the retention
worker purging any of them. Before this slice they had five plans, one implementation and no owner
(README C-5). After it they have one port each and no upload code of their own.

The facility is deliberately dull. It stores bytes and metadata. It knows nothing about cats, visits,
stays or products — a `Purpose` is the only domain word it uses, and the consuming area is what gives
that purpose meaning.

---

## 3. In scope

### 3.1 Stories

| Story | Size | Points | What this slice delivers | Phase |
|---|---|---|---|---|
| US-XCT-07 | M | 8 | Upload with type allow-list, magic-byte agreement, size cap, malware scan, server-generated storage key; signed short-lived serving; hard delete | A–D |
| (facility) | — | — | `IFileStore` over Google Cloud Storage, `LocalFileStore` for Development and Testing (ADR-318) | B |
| (facility) | — | — | `IMalwareScanner` over ClamAV `clamd`, fail-closed (ADR-319) | C |
| (facility) | — | — | `IRetentionPurgeSource` implementation so HO-XCT-001 can purge files by rule | D |

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| Google Cloud Storage, `Google.Cloud.Storage.V1`, app-proxied serving, versioning and soft-delete off | ADR-318 |
| ClamAV before the bucket, `Unavailable` fails closed | ADR-319 |
| The upload pipeline, `FileRules`, the signed-URL scheme and the response headers | HO-EMR-001 §8.10, kept verbatim |
| Files are metadata in PostgreSQL, bytes in the bucket | ADR-318 |

---

## 4. Out of scope

| Not here | Owner |
|---|---|
| Deciding **who** may see a file | The consuming area's read. This slice issues a signed URL only when asked by an endpoint that has already authorised and logged the read |
| Clinical-record access logging (NFR-SEC-8) | HO-EMR-001 §8.11 — the file URL is issued by a read that is already logged |
| Retention **periods** and the worker that runs them | HO-XCT-001. This slice exposes what to delete and deletes it |
| Image resizing, thumbnails, transcoding, EXIF stripping | Nobody yet — see OQ-5 |
| Direct-to-bucket uploads or GCS V4 signed URLs | Deferred by ADR-318, behind its own future ADR |
| Virus **quarantine** storage for later inspection | Not built: infected content is deleted, not kept |

---

## 5. Prerequisites

| Prerequisite | State |
|---|---|
| HO-ACC-001 merged (staff, sessions, audit, `ActionRoles`) | ✅ written |
| HO-ADM-001 merged (settings registry) | ✅ written |
| A GCS bucket per environment, uniform access, public access prevention enforced, versioning and soft-delete off | ❌ **owner/infra** — Development and Testing do not need it; **§20.1** |
| Workload Identity (or ADC) granting the API `roles/storage.objectAdmin` on that bucket only | ❌ **owner/infra** — §20.1 |
| A `clamd` container reachable from the API | ❌ **infra**; Compose file is part of this slice |

---

## 6. Governing architecture documents

| Document | What binds this slice |
|---|---|
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-318, ADR-319** are this slice's charter. ADR-009…ADR-023 bind as always; ADR-012 (no projections) — this slice holds no projection; ADR-021 (`SaveChangesAsync` per repository call) |
| [`.ai/architecture.md`](../../.ai/architecture.md) | Modular monolith, single host; Infrastructure holds adapters, Domain holds ports |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Needs a **`Files`** context row — OQ-1 |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | `StoredFile` is not a listed feature name; HO-EMR-001 ADR-125 recorded it and this slice inherits it |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §3 handler shape; the upload action is an action, not a handler, because it streams — §11.2 |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | `files` schema, migrations, no `numeric(19,4)` here |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | Adapters Scoped, rules Singleton; the environment-gated registrations in §12.7 |

---

## 7. Verified platform facts

| # | Fact | How to verify | If false |
|---|---|---|---|
| V1 | `Google.Cloud.Storage.V1` targets the solution's .NET version and resolves without a transitive conflict with the platform packages | `dotnet add`, build, `dotnet list package --include-transitive` | **Stop** — §20.2 |
| V2 | ADC resolves in the target environment without a key file on disk | `StorageClient.Create()` against the dev bucket from the deploy target | Keyless auth is the point; raise with infra before falling back — §20.2 |
| V3 | `nClam` speaks to the pinned `clamav/clamav` image over TCP | EICAR string through `ClamAvScanner` in an integration test | Try `clamdscan` over a socket; if neither works, **stop** |
| V4 | Kestrel streams a multipart part without buffering it in memory at the sizes here (50 MB) | Upload a 50 MB file, watch process memory | Lower the cap and raise it as an OQ |
| V5 | A GCS object delete is immediate with soft-delete disabled | Delete, then read; expect `404` | Erasure guarantees depend on it — **stop**, tell HO-XCT-001 |

---

## 8. Domain model

```
Files — schema `files`, namespace segment `StoredFile`

StoredFileAggregate  (root, append-only)                files.stored_file
```

One root. It is created by the upload path after the scan passes, is never mutated, and is deleted
only by the retention purge or an erasure. There is no second aggregate: a file has no lifecycle
beyond existing and being deleted.

### 8.1 `StoredFileAggregate` — inherited from HO-EMR-001 §8.10

| Field | Shape |
|---|---|
| `Id` | minted |
| `Purpose` | `FilePurpose` — see §10.1. The consuming area's word for what this file is |
| `ContentType` | the declared type, already checked against the allow-list **and** the leading bytes |
| `SizeInBytes` | `long`, ≤ the purpose's cap |
| `Sha256` | hex, computed while streaming — the duplicate check and the integrity check |
| `OriginalFileName` | sanitised: path stripped, control characters removed, ≤ 255 |
| `StorageKey` | **server-generated** `{purpose}/{yyyy}/{MM}/{id:N}` — never derived from the uploaded name |
| `UploadedBy`, `UploadedOn` | actor and instant |
| `ScannerName` | which scanner passed it, so a later signature recall can find what was scanned by what |
| `RetainUntil` | `DateOnly?`, set by the consuming area; `null` = the area's own rule decides |
| `LegalHold` | `bool` — set by HO-XCT-001; a held file is never purged |

`Register(...)` is the only factory. There is no `Update`, no rename, no re-upload: a replacement is
a new file and a delete of the old one, which is what keeps `Sha256` and `StorageKey` honest.

### 8.2 The upload pipeline — NFR-SEC-5

Order matters, and it is the order HO-EMR-001 established. Each step can only reject:

1. **Stream to a temp file** under `FileStorage:TempPath`, hashing while streaming. Never buffered in
   memory (V4). The temp file is deleted in **every** path, including exceptions.
2. **Size** ≤ the purpose's cap → else `FileTooLarge`.
3. **Declared type** in the purpose's allow-list → else `FileTypeNotAllowed`.
4. **Leading bytes agree** with the declared type → else `FileContentMismatch`. A PDF that says it is
   a PNG is refused here.
5. **Scan** — `IMalwareScanner.ScanAsync`: `Clean` continues; `Infected` → `400 FileRejected`, temp
   file deleted, one audit entry with the signature name and **no content**; `Unavailable` →
   `400 FileScanUnavailable` (ADR-319, fail closed).
6. **`IFileStore.PutAsync`** — only now do bytes leave the machine.
7. **`RegisterStoredFileCommand`** saves the aggregate. If the save fails, the object is deleted
   again (compensation), because an object with no row is invisible to the retention worker.

### 8.3 `FileRules` (Shared) — allow-list per purpose

| Purpose | Allowed types (declared **and** leading bytes) | Max | Owner |
|---|---|---|---|
| `LabResult` | `application/pdf` (`%PDF-`), `image/jpeg` (`FF D8 FF`), `image/png` (`89 50 4E 47 0D 0A 1A 0A`), `application/dicom` (`DICM` at offset 128) | 50 MB | HO-EMR-001 |
| `ClinicalPhoto` | `image/jpeg`, `image/png`, `image/webp` (`RIFF….WEBP`) | 15 MB | HO-EMR-001 |
| `ConsentForm` | `application/pdf`, `image/jpeg`, `image/png` | 15 MB | HO-EMR-001 |
| `ProductImage` | `image/jpeg`, `image/png`, `image/webp` | 5 MB | HO-SHP-001 |
| `BoardingMedia` | `image/jpeg`, `image/png`, `image/webp`, `video/mp4` (`ftyp` at offset 4) | 50 MB | HO-BRD-001 |
| `BoardingBelongings` | `image/jpeg`, `image/png`, `image/webp` | 15 MB | HO-BRD-001 |
| `ReportExport` | `application/pdf`, `text/csv`, `application/zip` (`PK\x03\x04`) | 50 MB | HO-RPT-001 |

**No `application/octet-stream`, no `*/*`, no SVG** (SVG is script), and nothing executable. A new
purpose is a row here plus an enum member — and an Amended row, not a code change elsewhere.

### 8.4 `GcsFileStore` — ADR-318

`Google.Cloud.Storage.V1`, one `StorageClient` (Singleton, thread-safe), bucket from configuration.

| Operation | Detail |
|---|---|
| `PutAsync` | `UploadObjectAsync` from the temp-file stream, with `ContentType`, `CacheControl = "private, no-store"`, and `Metadata` carrying `fileId`, `purpose` and `sha256`. Key from §8.1 |
| `OpenReadAsync` | `DownloadObjectAsync` into the response stream; a missing object → `FileObjectMissing`, logged at `Error` (a row with no object is corruption, not a `404` to shrug at) |
| `DeleteAsync` | `DeleteObjectAsync`; a missing object is **success** (idempotent — the retention worker may retry) |

**Startup refuses** a bucket name that looks like the production bucket outside `Production`, a
missing bucket name outside `Development`/`Testing`, and credentials that do not resolve.
`LocalFileStore` stays the `Development`/`Testing` implementation, writing outside the content root.

### 8.5 `ClamAvScanner` — ADR-319

`nClam` over TCP to `clamd`, host, port and timeout from configuration. A timeout is `Unavailable`,
not `Clean`. `ScannerName` records `"ClamAV {engine}/{signatures}"` so a later signature recall can
tell what scanned what. The existing `DevelopmentNoOpScanner` (constructor throws outside
`Development`), `EicarTestScanner` (`Testing`) and `UnconfiguredMalwareScanner` (`Unavailable`
everywhere else) are unchanged.

### 8.6 Serving — inherited verbatim from HO-EMR-001 §8.10

A consuming read asks `IFileUrlSigner` for a URL and returns it with its view model:

`/api/files/{fileId}/content?exp={unix}&disp={inline|attachment}&sig={base64url}` where
`sig = HMAC-SHA256(FileStorage:UrlSigningKey, "{fileId}|{exp}|{disp}")` and `exp = now + 5 min`.

`GET /api/files/{fileId}/content` is `[PublicEndpoint]` — **the signature is the authorisation**,
issued only by an endpoint that already authorised and logged the read. It verifies with
`CryptographicOperations.FixedTimeEquals`, refuses expired or tampered URLs with `403` and no detail,
and streams with the stored `Content-Type`, `X-Content-Type-Options: nosniff`,
`Content-Security-Policy: default-src 'none'; sandbox`, `Cache-Control: private, no-store`,
`Referrer-Policy: no-referrer`, and `Content-Disposition` — `inline` for JPEG, PNG, WebP, MP4 and PDF
when requested, **always `attachment` for DICOM and ZIP**, with an RFC 6266 `filename*`.

The signing key comes from configuration outside the repository, ≥ 32 bytes; startup refuses a missing
or short key outside `Development`/`Testing`.

### 8.7 Deletion and retention — for HO-XCT-001

`IRetentionPurgeSource` (HO-XCT-001's C-BRD-2 contract, generalised): given a purpose and a cut-off,
return the files eligible for purge — excluding `LegalHold` and any row whose `RetainUntil` is in the
future. Purge is **object first, row second** (ADR-318): a deleted row with a live object is an
orphan nobody will ever find; a deleted object with a live row is a visible, fixable error.

An erasure (HO-XCT-001's erasure ledger) deletes the object and the row and records the file id in the
ledger, so a restore-then-reapply leaves nothing behind.

---

## 9. Open questions

| # | Question | Who decides | Recommended default |
|---|---|---|---|
| OQ-1 | `.ai/bounded-contexts.md` has no `Files` context | Architect | Add it; it owns exactly one root and no business rule |
| OQ-2 | Bucket **per environment** or one bucket with per-environment prefixes | Owner / infra | Per environment. A prefix is one misconfiguration away from a test run deleting production objects |
| OQ-3 | Is a second copy needed for disaster recovery (dual-region, or a scheduled copy to another bucket)? | Owner | Dual-region for the production bucket. HO-XCT-001's backup story covers PostgreSQL and says nothing about objects — this is the gap it left open |
| OQ-4 | **EXIF is not stripped.** A phone photo of a boarded cat can carry GPS coordinates of the owner's home, and the portal serves it to the owner — and to anyone the URL is forwarded to within five minutes | Owner + architect | Strip EXIF on upload for image purposes. Recommended, **not built here** — it needs an imaging library and a decision about clinical photos, where orientation metadata matters |
| OQ-5 | No thumbnails: the boarding diary serves full-size images to a phone | Owner | Accept at launch; revisit with a resizing step in this slice, not in Boarding |
| OQ-6 | Duplicate uploads (same `Sha256`) are stored twice | This slice | Accept. De-duplication makes deletion a reference-counting problem, and retention is already the hardest part here |
| OQ-7 | Who may call the upload endpoint for each purpose | Consuming areas | `ActionRoles` per purpose, named by the consuming handover; the endpoint refuses a purpose the caller has no action for |
| OQ-8 | Product images are not personal data and could be served publicly and cached | HO-SHP-001 | Keep everything behind signed URLs at launch; one serving path is worth more than the egress saving |

---

## 10. Enumerations, constants, violation codes

### 10.1 `FilePurpose`

`LabResult`, `ClinicalPhoto`, `ConsentForm`, `ProductImage`, `BoardingMedia`, `BoardingBelongings`,
`ReportExport`. **Appended by the consuming handover**, with a `FileRules` row in the same commit.

### 10.2 Violation codes

`FileTooLarge`, `FileTypeNotAllowed`, `FileContentMismatch`, `FileRejected`, `FileScanUnavailable`,
`FileObjectMissing`, `FileNotFound`, `FilePurposeNotAllowedForCaller`.

### 10.3 Settings (HO-ADM-001 registry)

`files.maxUploadMb` (per purpose, read-only display of `FileRules`), `files.signedUrlMinutes` (5),
`files.scanTimeoutSeconds` (30). Bucket, credentials, scanner host and the signing key are
**configuration**, not settings — they are not an administrator's to change at runtime.

---

## 11. Specified changes — write side

1. `Domain/Files/StoredFileAggregate.cs`, `FilePurpose.cs`, `FileRules.cs` (Shared).
2. `Domain/DomainServices/{IFileStore,IMalwareScanner,IFileUrlSigner,IRetentionPurgeSource}.cs`.
3. `Application/Files/RegisterStoredFileCommand(+Handler,+Validator)`, `DeleteStoredFileCommand`.
4. **`UploadFileAction`** — an action, not a handler: it streams (`DisableFormValueModelBinding`,
   `MultipartReader`), and a command handler must not take a `Stream` from the request
   (`.ai/command-handler-pattern.md` §3). It performs §8.2 steps 1–6, then dispatches the command.
5. Migration `AddFiles` — `files.stored_file`, indexes on `(purpose, uploaded_on)` for the purge and
   `(sha256)` for the duplicate report.

## 12. Specified changes — read side, Infrastructure, Api, host

- `GetStoredFileQuery` (metadata + a signed URL), `GetFilesForPurgeQuery`.
- `Infrastructure/Files/`: `GcsFileStore`, `LocalFileStore`, `ClamAvScanner`, the three existing
  scanner stubs, `HmacFileUrlSigner`, `StoredFilePurgeSource`.
- `Api/Files/FilesController`: `POST /api/staff/files?purpose=`, `POST /api/customers/me/files?purpose=`
  (owner uploads, purposes restricted), `GET /api/files/{fileId}/content` (`[PublicEndpoint]`).
- **§12.4 — the contract for HO-FIL-002**: the upload request shape, the progress/abort behaviour, the
  violation codes above and the signed-URL view model `{ fileId, url, contentType, sizeInBytes,
  originalFileName, urlExpiresOn }`. Every consuming area returns **this** shape for a file.
- Host: `StorageClient` Singleton; adapters Scoped; the environment guards in §8.4/§8.5; Compose gains
  `clamav` and, for Development, nothing else — `LocalFileStore` needs no container.

---

## 13. Allowed scope

- Everything under `Domain/Files/`, `Application/Files/`, `Infrastructure/Files/`, `Api/Files/`.
- The `files` schema and its migration.
- `docker-compose.yml`: the `clamav` service.
- **In other areas, nothing** — consuming handovers add their own `FilePurpose` member and `FileRules`
  row when they build, each with an Amended row here.

## 14. Forbidden scope

| Forbidden | Why |
|---|---|
| Any business rule about cats, visits, stays, products or orders | This context knows only `Purpose` |
| Deciding who may read a file | The consuming read decides and logs; this slice trusts the signature |
| A second upload path anywhere in the solution | The reason this slice exists (README C-5) |
| Writing to any schema but `files` | Context boundary |
| A configuration flag that skips the scan outside `Development` | ADR-319; `DevelopmentNoOpScanner` throwing in its constructor is the enforcement |
| Public bucket objects, per-object ACLs, or a signed GCS URL handed to a browser | ADR-318 |

> **Acknowledged exception (gate-fix 2026-09-27, ARCHITECTURE LOW):** `RecordFileRejectionAuditCommandHandler`
> writes to `audit.audit_entry`, not `files`. This follows the existing ADM/ACC precedent — every
> audit-only write in this solution targets the shared audit schema, not the writing context's own —
> and is called out here rather than left as a silent exception to the row above.
>
> **Acknowledged exception (gate-fix 2026-09-27, ARCHITECTURE LOW, third pass):** the `AddFiles`
> migration also writes to `clinic`, not `files` — the explicit `DELETE FROM clinic.cat_photo;`
> (removing every pre-HO-FIL-001 photo row, §1 Amended (round 2) item (3)) and the `clinic.cat_photo`
> schema change itself (`FileId` column + the `ix_cat_photo_file_id` unique index, replacing
> `StorageKey`/`ContentType`/`SizeInBytes`/`FileName`). Both are authorised under §1 Amended (round 2)
> items (3) and (4) as the carried HO-CAT-002-SECURITY fix's own schema consequence, not a new
> exception being invented here — called out for the same consistency reason as the audit write above.

---

## 15. Unit test cases

| # | Case | Expect |
|---|---|---|
| U1 | Each `FileRules` row: declared type allowed | Allowed |
| U2 | PDF bytes declared `image/png` | `FileContentMismatch` |
| U3 | SVG, `application/octet-stream`, `.exe` bytes, for every purpose | `FileTypeNotAllowed` |
| U4 | One byte over the cap | `FileTooLarge` |
| U5 | `OriginalFileName` = `../../etc/passwd\u0000.png` | Sanitised; `StorageKey` unaffected by it |
| U6 | `StorageKey` for two files uploaded in the same minute | Distinct; contains neither name nor purpose-external data |
| U7 | Signature verification: tampered `fileId`, tampered `exp`, expired `exp`, wrong key | Refused, every time |
| U8 | `FixedTimeEquals` is used, not `==` | Reflection/analyzer test |
| U9 | `DevelopmentNoOpScanner` constructed outside `Development` | Throws |
| U10 | Scanner returns `Unavailable` | Upload refused — **fail closed** (ADR-319) |
| U11 | Purge selection with `LegalHold`, future `RetainUntil`, past `RetainUntil` | Only the last is eligible |
| U12 | Reflect over `IFileStore`, `IMalwareScanner` and every DTO: no member named `/force|bypass|skipScan|allowAny/i` | None |

## 16. Integration test cases

| # | Case | Expect |
|---|---|---|
| I1 | Upload a clean PNG as `ProductImage` | `201`; row written; object present; temp file gone |
| I2 | Upload the **EICAR** string | `400 FileRejected`; **no object in the store**; audit entry with the signature name; no content logged |
| I3 | Scanner container stopped | `400 FileScanUnavailable`; no object |
| I4 | `PutAsync` succeeds, the command save then fails | Object deleted again — no orphan (§8.2 step 7) |
| I5 | Signed URL, fetched twice inside 5 minutes | `200` both times; correct headers on both |
| I6 | The same URL after `exp` | `403`, no body detail |
| I7 | DICOM and ZIP fetched with `disp=inline` | `Content-Disposition: attachment` regardless |
| I8 | Purge a file past `RetainUntil` | Object gone first, then the row; second run of the same purge is a no-op |
| I9 | Delete an object out of band, then read | `FileObjectMissing`, logged `Error`, not a silent `404` |
| I10 | 50 MB upload | Succeeds; process memory does not grow by 50 MB (V4) |

---

## 17. Implementation order

| Phase | Delivers | Gate |
|---|---|---|
| **A** | Aggregate, `FileRules`, migration, `RegisterStoredFileCommand`, `LocalFileStore`, the upload action with steps 1–4 | U1–U8 |
| **B** | `GcsFileStore` + startup guards (V1, V2, V5) | I1, I10 |
| **C** | `ClamAvScanner`, Compose service, fail-closed paths (V3) | U9, U10, I2, I3 |
| **D** | Serving, signing, purge source, `IRetentionPurgeSource` | I5–I9 |

## 18. Acceptance criteria

| # | Criterion | Evidence |
|---|---|---|
| A1 | Uploads are type-checked, magic-byte-checked and size-capped per purpose | U1–U4 |
| A2 | Uploaded content is scanned; infected content never reaches the bucket | I2 |
| A3 | A scanner outage refuses uploads rather than storing them unscanned | I3, U10 |
| A4 | Stored files are never publicly readable and are served only through short-lived signed URLs | I5, I6; bucket assertions in §8.4 |
| A5 | A file can be deleted permanently, object first, and the purge is idempotent | I8 |
| A6 | No second upload path exists in the solution | Convention test; §14 |
| A7 | The facility runs in Development and CI with no cloud credentials | Whole suite green with `LocalFileStore` |

## 19. Required gates

| # | Gate | Focus |
|---|---|---|
| 1 | ARCHITECTURE | The `Files` context (OQ-1); ports in Domain, adapters in Infrastructure; the upload action's exception to the handler pattern (§11.4) and its justification; no projection; nothing of other contexts touched |
| 2 | SECURITY | **The main gate for this slice.** NFR-SEC-5 end to end: allow-list, magic bytes, caps, scan-before-store, fail-closed, server-generated keys, signature verification in constant time, headers, no public objects, no key file in the image, secrets not logged |
| 3 | CODE_REVIEW | Stream disposal on every path including exceptions; temp-file cleanup; the compensating delete in step 7 |
| 4 | TEST | U1–U12, I1–I10 |
| 5 | ACCEPTANCE | A1–A7 |

## 20. BLOCKED conditions — stop and report, do not work around

| # | Condition |
|---|---|
| **20.1** | No bucket, or credentials that do not resolve, in a non-Development environment. Do **not** fall back to `LocalFileStore` outside Development — a file on a container filesystem is gone at the next deploy |
| **20.2** | V1 (package conflict) or V2 (ADC) fails. Do **not** commit a service-account JSON key to make it work |
| **20.3** | `clamd` cannot be reached from the API in CI. Do **not** register `DevelopmentNoOpScanner` to make the suite green |
| **20.4** | V5 fails — deletes are not immediate. HO-XCT-001's erasure guarantee depends on it; report before building the purge |
| **20.5** | A consuming area asks for a purpose that needs an allow-list this slice would not write (`*/*`, SVG, anything executable). That is a conversation, not a rule change |

---

## 21. Operational notes

Gate-fix 2026-09-27 (SECURITY LOW, second pass): `GET /api/files/{fileId}/content`'s per-IP rate
limit (`FileRateLimitPolicies.FileServe`, §12.7) is keyed on `HttpContext.Connection.RemoteIpAddress`.
Behind a load balancer or reverse proxy with no `RequestOrigin:TrustedProxies`/`TrustedNetworks`
configured (the safe-by-default, committed value -- ADR-040), every request arrives with the SAME
peer address (the proxy's own), so every client sharing that proxy shares one 60-per-minute budget.
This is deliberately the safe default against IP spoofing (an untrusted `X-Forwarded-For` header is
never honoured, per `HostPipelineExtensions`), but it means:

- **Before any non-Development deployment behind a proxy or load balancer**, its address (or
  network) must be added to `RequestOrigin:TrustedProxies`/`TrustedNetworks`, exactly as
  HO-ACC-001 §12 already requires for its own rate limits and audit IPs. This is not a new
  requirement HO-FIL-001 invents -- it is the same configuration every other rate-limited or
  IP-logged endpoint in this solution already depends on; FileServe is simply another consumer of
  it, not a special case.
- **Size the limit to the real page fan-out.** A cat profile or product page rendering several
  images in one view uses the 60-per-minute budget quickly for every client sharing one proxy
  address. `RateLimiting:FileServe:PermitLimit`/`WindowMinutes` (§10.3) are administrator-config,
  not code, specifically so this can be tuned per deployment without a rebuild.
- This is unchanged from, and does not fix, the SECURITY gate's own observation that a bad
  signature is still counted against this same budget before the signature is checked -- sizing
  the limit generously enough for legitimate fan-out is the mitigation, not a code change.
