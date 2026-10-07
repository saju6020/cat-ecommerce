# TC-HO-FIL-001 — File storage (backend)

Handover: docs/handover/HO-FIL-001.md
Commit(s) under test: ce622b0 (branch ho/FIL-001; master eef1621 merged in) — working tree also
carries this gate's own new/changed test files (not committed; see report)
Written: 2026-09-27  ·  Executed: 2026-09-27

> Statuses record what was **observed**, not inferred. `BLOCKED` marks a case that could not be
> executed or whose result was reasoned about rather than watched.

## Summary

| | Count |
|---|---|
| Total | 60 |
| PASS | 59 |
| FAIL | 0 |
| BLOCKED | 1 |
| NOT RUN | 0 |

**Update 2026-09-27 (post-TEST-gate close-out)**: R6 and R16, originally recorded `BLOCKED`
above, are now `PASS` — see their rows in §Unit/Integration cases and "Cases I could not run"
below, both marked `**Closed 2026-09-27**`. The remaining `BLOCKED` count (1) is A4's live-bucket
half; V3 (real ClamAV) is a `Gaps left deliberately` item, not counted `BLOCKED`, matching this
sheet's original accounting.

## Environment

- Build: `dotnet build CatEcommerce.sln` — **0 errors, 0 warnings** (observed).
- `CATECOMMERCE_TEST_DB=Host=localhost;Port=5432;Database=catecommerce_fil;Username=postgres;Password=postgres` — reachable; already at migration `20260927071352_AddFiles` (head).
- Unit suites: `CatEcommerce.Domain.Tests` (319/319), `CatEcommerce.Application.Tests` (101/101), `CatEcommerce.Infrastructure.Tests` (56/56) — no external services, no DB.
- Integration suite: `CatEcommerce.IntegrationTests` (413/413, run three times total in this pass — see report) — real Postgres (`catecommerce_fil`), `LocalFileStore` (Development/Testing adapter — no real GCS bucket), `EicarTestScanner` (Testing adapter — no real `clamd`). No GCS bucket, no ADC, and no `clamd` were reachable in this sandbox (per HO-FIL-001 §1 Amended (7) and §5's own ❌ rows) — every case that would need a live GCS bucket or a live `clamd` is `BLOCKED` for that reason.
- Web unit tests: `npx vitest run` in `web/` — **106/106, 37 test files** — confirms the merged-from-master `web/` tree still passes; FIL does not touch `web/`.
- Host antivirus quarantine of EICAR temp files was not observed to interfere in this run; `FilesController`'s existing `IOException` handling is designed to fail closed for that case regardless.

## Unit cases (§15)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U1 | Each `FileRules` row: declared type allowed | Allowed | PASS | `FileRulesTests.Every_purpose_has_a_rule_and_every_declared_type_in_it_agrees_with_its_own_magic_bytes` — part of the 319/319 Domain.Tests run |
| U2 | PDF bytes declared `image/png` | `FileContentMismatch` | PASS | `FileRulesTests.Pdf_bytes_declared_as_png_do_not_match` / `.Png_bytes_declared_as_pdf_do_not_match` |
| U3 | SVG, `application/octet-stream`, `.exe` bytes, every purpose | `FileTypeNotAllowed` | PASS | `FileRulesTests.Svg_octet_stream_and_exe_are_never_allowed_for_any_purpose` (8 purposes) + `.Exe_bytes_never_match_any_allowed_content_type` |
| U4 | One byte over the cap | `FileTooLarge` | PASS | `StoredFileAggregateTests.Register_rejects_a_size_over_the_purposes_cap` (aggregate re-check) **and** the new integration case `FilesEndpointTests.Upload_one_byte_over_the_purpose_cap_is_refused_as_FileTooLarge` (streaming pipeline, real HTTP) — individually confirmed passing (`dotnet test --filter`, both green) |
| U5 | `OriginalFileName` = `../../etc/passwd\u0000.png` | Sanitised; `StorageKey` unaffected | PASS | `FileNameSanitizerTests.Strips_path_traversal_and_the_null_byte` (exact case) + `StorageKeyFactoryTests.Build_never_contains_a_file_name` |
| U6 | `StorageKey` for two files uploaded in the same minute | Distinct; no name/purpose-external data | PASS | `StorageKeyFactoryTests.Build_returns_distinct_keys_for_two_files_uploaded_in_the_same_minute`, `.Build_never_contains_a_file_name`, `.Build_has_the_documented_shape` |
| U7 | Signature verification: tampered fileId/exp/disp, expired, wrong key | Refused, every time | PASS | `HmacFileUrlSignerTests.TryVerify_refuses_a_tampered_fileId/_exp/_disposition`, `_the_wrong_key`, `_an_expired_url_at_the_caller_level`, `_a_malformed_signature_without_throwing` |
| U8 | `FixedTimeEquals` used, not `==` | Reflection/analyzer test | PASS | `HmacFileUrlSignerTests.TryVerify_calls_CryptographicOperations_FixedTimeEquals` |
| U9 | `DevelopmentNoOpScanner` constructed outside `Development` | Throws | PASS | `DevelopmentNoOpScannerTests.Constructor_throws_outside_Development` (Testing/Production/Staging) |
| U10 | Scanner returns `Unavailable` | Upload refused — fail closed | PASS | `UnconfiguredMalwareScannerTests.ScanAsync_always_reports_Unavailable` (unit) + I3 (integration, end-to-end) |
| U11 | Purge selection with `LegalHold`, future `RetainUntil`, past `RetainUntil` | Only the last eligible | PASS | **Closed 2026-09-27**: the dead `PurgeEligibility` (Shared) helper and its 5-fact `PurgeEligibilityTests` are deleted. Both real consumers now share one `Expression<Func<StoredFileAggregate, bool>>`, `StoredFileAggregate.IsEligibleForPurge`, passed to EF Core directly by `StoredFilePurgeSource` and `GetFilesForPurgeQueryHandler` — no more independently-duplicated inline predicate. Covered by the existing `FilesEndpointTests.Purge_batch_is_not_starved_by_held_rows_ordered_before_eligible_ones` (the real purge path) and the new `FilesEndpointTests.GetFilesForPurgeQuery_excludes_legal_hold_and_future_retain_until_includes_past_and_null` (the preview-query path), both confirmed individually passing |
| U12 | Reflect over `IFileStore`/`IMalwareScanner`/every DTO: no `/force\|bypass\|skipScan\|allowAny/i` member | None | PASS | `FilePortSurfaceReflectionTests.No_member_offers_a_way_to_force_bypass_or_skip_the_scan` (8 types) |

## Integration cases (§16)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I1 | Upload a clean PNG as `ProductImage` | `201`; row written; object present; temp file gone | PASS | `FilesEndpointTests.Upload_a_clean_png_as_ProductImage_succeeds_and_the_object_and_row_exist` — part of the 413/413 IntegrationTests runs |
| I2 | Upload the EICAR string | `400 FileRejected`; no object; audit entry; no content logged | PASS | `FilesEndpointTests.Upload_the_eicar_string_is_rejected_and_no_object_is_stored` |
| I3 | Scanner container stopped | `400 FileScanUnavailable`; no object | PASS | `FilesEndpointTests.Upload_when_the_scanner_is_unavailable_is_refused_not_stored_unscanned` |
| I4 | `PutAsync` succeeds, command save then fails | Object deleted again — no orphan | PASS | `FilesEndpointTests.When_the_command_save_fails_the_object_is_deleted_again_no_orphan` |
| I5 | Signed URL, fetched twice inside 5 min | `200` both times; correct headers | PASS | `FilesEndpointTests.Signed_url_works_twice_within_the_window_then_is_refused_after_expiry` (first half) |
| I6 | Same URL after `exp` | `403`, no body detail | PASS | same test, second half (fake-clock advance) |
| I7 | DICOM and ZIP fetched with `disp=inline` | `Content-Disposition: attachment` regardless | PASS | `FilesEndpointTests.Dicom_and_zip_are_always_served_as_attachment_even_when_inline_is_requested` |
| I8 | Purge a file past `RetainUntil` | Object gone first, then row; second run no-op | PASS | `FilesEndpointTests.Purge_deletes_the_object_first_then_the_row_and_a_second_run_is_a_no_op` |
| I9 | Delete an object out of band, then read | `FileObjectMissing`, logged `Error`, not silent `404` | PASS | `FilesEndpointTests.Reading_a_file_whose_object_was_deleted_out_of_band_is_a_logged_error_not_a_silent_404` |
| I10 | 50 MB upload; process memory does not grow by 50 MB (V4) | Succeeds; bounded memory | PASS (smoke test only) | `FilesEndpointTests.A_large_upload_succeeds_and_is_served_back_intact` ran and passed (20 MB round trip, byte-identical). The memory-growth assertion itself is a **deliberate, documented gap** (see Gaps) — three heuristics were tried by the CODE_REVIEW gate-fix round and each produced false results under this suite's real parallel execution; this pass did not re-add one, per the task's own instruction |

## Acceptance-criteria coverage (§18)

| Criterion | Covered by | Status |
|---|---|---|
| A1 — type/magic-byte/size checks per purpose | U1–U4 | PASS |
| A2 — infected content never reaches the bucket | I2 | PASS |
| A3 — scanner outage refuses rather than stores unscanned | I3, U10 | PASS |
| A4 — never publicly readable; signed URLs only | I5, I6; `GcsBucketPolicyCheckerTests` (4 facts, against `FakeStorageClient`) | PASS for everything observable in this environment. The live-bucket half (a real GCS bucket actually enforcing UBLA/PAP) is `BLOCKED` — see below |
| A5 — deletable, object-first, idempotent purge | I8, and the new starvation test | PASS |
| A6 — no second upload path in the solution | New convention test `FileUploadConventionTests.Only_FilesController_disables_form_value_model_binding_or_parses_multipart_itself` | PASS — confirmed individually (`dotnet test --filter`, green) |
| A7 — runs in Development/CI with no cloud credentials | Whole suite green with `LocalFileStore`, no GCS/clamd credentials configured | PASS — Domain/Application/Infrastructure/Integration suites all green, twice, with no cloud credentials present |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCHITECTURE p1 | HIGH: two aggregates mutated in `AddCatPhotoCommandHandler` | Eligible attach mutates only `CatAggregate`; cat-side refusal leaves file untouched; concurrent attach yields exactly one row | PASS | `AddCatPhotoCommandHandlerTests.HandleAsync_EligibleFile_...` (asserts `fileRepository.Updated`/`.Saved` empty), `.HandleAsync_PhotoLimitExceeded_...`, `.HandleAsync_DeceasedCat_...` — part of 101/101 Application.Tests; `CatPhotoAttachEndpointTests.Concurrent_attach_of_one_file_to_two_cats_yields_exactly_one_row` — part of 413/413 IntegrationTests |
| R2 | ARCHITECTURE p1 | HIGH: `CatPhoto` stored a dangling storage key, not a `FileId` | Photo read returns `{id, fileId, isPrimary, uploadedOn, url}` and is servable | PASS | `CatProfileProjectionEndpointTests.GetCat_FullProfile_ProjectsEveryFieldCorrectly` — extended this gate with an explicit `uploadedOn` assertion, confirmed individually passing; `CatPhotoAttachEndpointTests.Customer_uploads_and_attaches_a_photo_to_their_own_cat_and_it_is_served_back` |
| R3 | ARCHITECTURE p2 | HIGH: attach-eligibility rule lived in the Cat handler | Rule moved to `StoredFileAggregate.IsEligibleForAttach`, side-effect-free | PASS | `StoredFileAggregateTests.IsEligibleForAttach_is_true_...never_mutates_state`, `.gives_the_same_answer_when_asked_repeatedly`, `.refuses_the_wrong_purpose`, `.refuses_a_caller_who_did_not_upload_the_file`, `.refuses_outside_the_attach_window` — part of 319/319 Domain.Tests |
| R4 | ARCHITECTURE p1 | HIGH: `LegalHold` unenforced in `IStoredFileEraser.EraseAsync` | A held file's erase attempt returns `false`; object and row remain | PASS | New `FilesErasureEndpointTests.StoredFileEraser_RefusesAHeldFile_TheObjectAndRowBothRemain` — confirmed individually (4/4 in this new file, then part of 413/413) |
| R5 | ARCHITECTURE p1 | HIGH: GCS bucket assertions/startup ADC guard not built | `GcsBucketPolicyChecker` asserts UBLA/PAP/versioning/soft-delete | PASS | `GcsBucketPolicyCheckerTests` (4 facts, against `FakeStorageClient`) — part of 56/56 Infrastructure.Tests |
| R6 | ARCHITECTURE p2 | MEDIUM: destructive `cat_photo` migration must not silently corrupt pre-existing rows | `DELETE FROM clinic.cat_photo;` runs before the schema change | PASS | **Closed 2026-09-27**: new `AddFilesMigrationDeletesLegacyCatPhotoRowsTests.AddFiles_DeletesPreExistingCatPhotoRows_ThenAppliesCleanly`, against a disposable scratch database (created/dropped per test, `MigrationsOnAnEmptyDatabaseTests`' own pattern): migrates to `WidenAuditEntryValues`, inserts two legacy-shaped `cat_photo` rows via raw SQL, migrates to head through `AddFiles`, asserts success, zero rows, the new `FileId` column present, the old `StorageKey`/`ContentType`/`SizeInBytes`/`FileName` columns gone, and `ix_cat_photo_file_id` built. Regression-verified: temporarily removing the migration's `DELETE FROM clinic.cat_photo;` line reproduces a genuine unique-index-violation (`23505`) failure |
| R7 | SECURITY p1 | MEDIUM: purpose gate failed open (any staff role could upload any unmapped purpose) | Unmapped purpose refused for every staff role; only `CatProfilePhoto` mapped | PASS | New `FilePurposeAccessTests` (16 facts: 7 purposes × 2 directions + 2 CatProfilePhoto-specific) — confirmed individually passing, all green |
| R8 | SECURITY p1/p2 | MEDIUM: served file name not reconciled with content type; bidi/format characters kept | `ForDownload` forces the canonical extension; `Sanitize` strips Cf/Cc | PASS | New `FileNameSanitizerTests` additions (`ForDownload` × 9 cases, bidi/format stripping × 3 cases) — confirmed individually passing |
| R9 | SECURITY p1/p2 | MEDIUM: `GcsFileStore.OpenReadAsync` buffered the whole object in memory; `leaveOpen` swallowed mid-download failures | Streams via `Pipe`; a mid-download failure surfaces as an exception, not a clean truncated 200 | PASS | `GcsFileStoreTests.OpenReadAsync_a_mid_download_failure_surfaces_as_an_exception_on_the_reader_not_a_clean_truncated_end` (existing) **and** the new `.OpenReadAsync_streams_the_full_object_content_back_byte_for_byte` (happy path, 5 MB round trip) — both confirmed individually passing |
| R10 | SECURITY p1/p2 | LOW: no rate limit on uploads/serving | `FileUpload`/`FileServe` 429 after the limit, with `Retry-After` | PASS | `FilesEndpointTests.Upload_OverLimit_ThirdReturns429`, `.Serve_OverLimit_ThirdReturns429` |
| R11 | SECURITY p1/p2 | LOW: compensation ran only on `IsSuccess=false`, not on throw | Object deleted on a thrown save too | PASS | `FilesEndpointTests.When_the_command_save_fails_the_object_is_deleted_again_no_orphan` (IsSuccess=false path); throw path is R14 |
| R12 | SECURITY p1/p2 | LOW: EICAR scanner only scanned the first 4096 bytes | Whole-stream rolling-window scan | PASS | `EicarTestScannerTests.ScanAsync_finds_the_eicar_string_placed_after_five_thousand_bytes_of_leading_text`, `.ScanAsync_finds_the_eicar_string_split_across_a_chunk_boundary` |
| R13 | SECURITY p2 | LOW: `Cache-Control` literal string vs `private, no-store` | Parsed `Private`/`NoStore` flags both set | PASS | `FilesEndpointTests.Signed_url_works_twice_within_the_window_then_is_refused_after_expiry` |
| R14 | CODE_REVIEW p2 (3rd pass) | LOW: compensating delete on a *thrown* save used the ambient (cancellable) token, not `CancellationToken.None` | Delete uses a token with `CanBeCanceled == false` | PASS | `FilesEndpointTests.When_the_save_throws_the_compensating_delete_uses_CancellationToken_None` |
| R15 | CODE_REVIEW p1 (carried) / ARCHITECTURE p2 | LOW: `StoredFilePurgeSource` filtered eligibility in memory after `Take`; one shared source for every purpose | SQL-side filtering before `Take`; one `IRetentionPurgeSource` per purpose, resolved by `SourceKey` | PASS | I8 proves the `GetServices`+`SourceKey` fix; the new `Purge_batch_is_not_starved_by_held_rows_ordered_before_eligible_ones` proves the SQL-side-before-`Take` ordering itself — confirmed individually passing |
| R16 | SECURITY p1 | LOW: malformed multipart (no boundary, truncated body, client abort) → unhandled 500 | 400/413/quiet empty result, never a 500 | PASS | **Closed 2026-09-27**: new `FilesMalformedMultipartEndpointTests` (6 facts: no boundary, an over-length boundary, a truncated body via a non-seekable/chunked stream, >16 part headers, an oversized part header, a JSON body instead of multipart) — all assert 400 with the uniform `FileRejected` body, confirmed individually and as part of the full suite. Each case's exception attribution is regression-verified (disable the specific catch in `FilesController`, confirm an unhandled 500 with no effect on the other cases, restore) — see the doc comments on `UploadAsync`'s and `ProcessFileSectionAsync`'s catch clauses. The over-cap-body → 413 case is not re-probed here (`TestServer` does not enforce Kestrel's `MaxRequestBodySize` at all); the SECURITY gate's own live Kestrel run already confirmed it directly |

## Amended-row behaviour changes — explicit mapping (per task brief)

| Behaviour | Mapped to | Status |
|---|---|---|
| Purpose gate fails closed | R7 | PASS |
| Customer `CatProfilePhoto` upload | `CatPhotoAttachEndpointTests.Customer_uploads_and_attaches_a_photo_to_their_own_cat_and_it_is_served_back` | PASS |
| Attach eligibility side-effect-free + one-file-one-cat unique index | R1, R3 | PASS |
| Cat-side refusal leaves the file attachable | `AddCatPhotoCommandHandlerTests.HandleAsync_PhotoLimitExceeded_...`/`.HandleAsync_DeceasedCat_...` | PASS |
| Download filename extension forced + bidi stripping | R8 | PASS |
| CSV whole-file deny scan | `FileRulesTests.ContainsDeniedCsvSignatureAsync_*` (3 facts), `FilesEndpointTests.Upload_csv_with_an_hta_payload_after_benign_csv_content_is_rejected` | PASS |
| GCS streaming + `leaveOpen` error propagation | R9 | PASS |
| Compensation on throw with `CancellationToken.None` | R11, R14 | PASS |
| Rate limits `FileUpload`/`FileServe` 429 | R10 | PASS |
| Legal hold in the eraser | R4 | PASS |
| Personal-data eraser | New `FilesErasureEndpointTests.FilesPersonalDataEraser_RedactsAPlainFile_ButHoldsAFutureRetainFile_AndLeavesOtherCustomersFilesAlone` + `.FilesPersonalDataEraser_ForACustomerWithNoFiles_ReturnsCompleted` | PASS |
| Purge SQL-side filtering, one-source-per-purpose | R15 | PASS |
| GCS bucket policy startup checks | R5 | PASS |
| Migration DELETE of old `cat_photo` rows | R6 | PASS |
| Cat profile photos `{id, fileId, isPrimary, uploadedOn, url}` | R2 | PASS |

## New tests added (this gate)

All confirmed passing individually via `dotnet test --filter`, then again as part of the full suite (run twice, 413/413 both times):

| Test | File | Closes |
|---|---|---|
| `Upload_one_byte_over_the_purpose_cap_is_refused_as_FileTooLarge` | `tests/CatEcommerce.IntegrationTests/Endpoints/FilesEndpointTests.cs` | U4 integration gap |
| `Purge_batch_is_not_starved_by_held_rows_ordered_before_eligible_ones` | `tests/CatEcommerce.IntegrationTests/Endpoints/FilesEndpointTests.cs` | R15/U11 gap |
| `FilesPersonalDataEraser_RedactsAPlainFile_ButHoldsAFutureRetainFile_AndLeavesOtherCustomersFilesAlone`, `.FilesPersonalDataEraser_ForACustomerWithNoFiles_ReturnsCompleted`, `StoredFileEraser_RefusesAHeldFile_TheObjectAndRowBothRemain`, `StoredFileEraser_ANonHeldFile_ErasesTheObjectThenTheRow` | new `tests/CatEcommerce.IntegrationTests/Endpoints/FilesErasureEndpointTests.cs` | personal-data eraser gap, R4 |
| `FilePurposeAccessTests` (16 facts) | new `tests/CatEcommerce.Domain.Tests/Common/FilePurposeAccessTests.cs` | R7 gap |
| `FileNameSanitizerTests` additions: `ForDownload` (9 theory cases) + bidi/format stripping (3 facts) | `tests/CatEcommerce.Domain.Tests/Common/FileNameSanitizerTests.cs` | R8 gap |
| `OpenReadAsync_streams_the_full_object_content_back_byte_for_byte` (+ `FakeStorageClient.ForObjectDownloadSuccess`) | `tests/CatEcommerce.Infrastructure.Tests/Files/GcsFileStoreTests.cs`, `FakeStorageClient.cs` | R9 gap |
| `Only_FilesController_disables_form_value_model_binding_or_parses_multipart_itself` | new `tests/CatEcommerce.IntegrationTests/Unit/FileUploadConventionTests.cs` | A6 gap |
| `Host_ResolvesTheCrossModuleStoredFileSigningBatcherDependency` | `tests/CatEcommerce.IntegrationTests/Endpoints/CatProfileProjectionEndpointTests.cs` | cross-module DI edge (ARCHITECTURE p2 LOW) |
| `uploadedOn` assertion added to `GetCat_FullProfile_ProjectsEveryFieldCorrectly` | `tests/CatEcommerce.IntegrationTests/Endpoints/CatProfileProjectionEndpointTests.cs` | R2 gap |
| `GetFilesForPurgeQuery_excludes_legal_hold_and_future_retain_until_includes_past_and_null` | `tests/CatEcommerce.IntegrationTests/Endpoints/FilesEndpointTests.cs` | U11 weak-test note |
| `AddFilesMigrationDeletesLegacyCatPhotoRowsTests.AddFiles_DeletesPreExistingCatPhotoRows_ThenAppliesCleanly` | new `tests/CatEcommerce.IntegrationTests/Persistence/AddFilesMigrationDeletesLegacyCatPhotoRowsTests.cs` | R6 |
| `FilesMalformedMultipartEndpointTests` (6 facts: no boundary, over-length boundary, truncated body, >16 part headers, oversized part header, JSON body) | new `tests/CatEcommerce.IntegrationTests/Endpoints/FilesMalformedMultipartEndpointTests.cs` | R16 |

**A product/test-infrastructure bug found and fixed while writing the held-file eraser test** (reported here, not a source-code bug): the first draft of `StoredFileEraser_RefusesAHeldFile_...` used one shared `CatEcommerceDbContext` scope for seeding, the raw-SQL `LegalHold` flip, and the call to `IStoredFileEraser`. That made the eraser's own query resolve to EF's already-tracked (stale) entity via identity-map resolution rather than a fresh read, so the row's real (raw-SQL-bumped) `xmin` disagreed with the tracked one and `EraseAsync` **threw `DbUpdateConcurrencyException`** instead of returning `false` — observed directly (see report). This is a **test-authoring** bug, not a `StoredFileEraser` defect: fixed by using three separate scopes (seed / flip / erase), matching how a real, separate-request erasure would behave. No production code was touched.

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| A4 (live-bucket half) | No GCS bucket/ADC available in this sandbox (§1 Amended (7), §5 ❌ rows) | A real GCS bucket + Workload Identity/ADC in a non-Development environment |
| V3 (ClamAV via real `clamd`) | No `clamd` reachable from this sandbox | `docker-compose.yml`'s `clamav` service, pulled and started, with `ClamAvHost` configured |
| ~~R6 / migration DELETE behaviour~~ | **Closed 2026-09-27** — see R6 row above; the scratch-database approach flagged here as the unblocker was implemented in `AddFilesMigrationDeletesLegacyCatPhotoRowsTests` | — |
| ~~R16 / malformed-multipart 500→400 mapping~~ | **Closed 2026-09-27** — see R16 row above; `FilesMalformedMultipartEndpointTests` ports the no-boundary, over-length-boundary, truncated-body, >16-headers and oversized-header cases from the SECURITY gate's own probe list | — |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| I10 (50 MB / V4 memory-growth assertion) | Three heuristics were tried by the CODE_REVIEW gate-fix round and each produced a false result under this suite's real parallel execution (documented in `FilesEndpointTests.cs`); a reliable per-request allocation measurement needs an out-of-process profiler, not a `GC.*` call inside the test. This TEST gate deliberately did not re-add one, per its own instructions | Whoever next touches `ProcessFileSectionAsync`'s read loop should get extra scrutiny at review time, since no automated test will catch a buffering regression there |
| V3 (real ClamAV) / live GCS bucket (V1/V2/V5) | Infra not available in any environment this handover has access to (§1 Amended (7)) | Owner/infra, before the first non-Development deployment |
| ~~`PurgeEligibility` (Shared) is dead code from production's point of view~~ | **Closed 2026-09-27** — see U11 row above; `PurgeEligibility` and its tests are deleted, replaced by the shared `StoredFileAggregate.IsEligibleForPurge` expression both consumers now actually use | — |
| ~~R16 malformed-multipart persisted test~~ | **Closed 2026-09-27** — see "Cases I could not run" and the R16 row above | — |
