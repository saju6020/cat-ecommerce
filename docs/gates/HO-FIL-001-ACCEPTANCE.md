GATE: ACCEPTANCE
HANDOVER: HO-FIL-001
COMMITS: master eef1621 .. ho/FIL-001 ae7c4d9 (reviewed as `git diff master...ho/FIL-001`; post-TEST commits 56efffc 1fb83ac 487d2ef b1c960e ae7c4d9 read line by line)
DATE: 2026-09-27
STATUS: PASS

Owner rule (2026-09-27): only BLOCKER/HIGH fail a gate. An acceptance criterion with no concrete
evidence is HIGH. None was found. MEDIUM/LOW items are listed under BACKLOG for docs/backlog/FIL.md
(this gate is read-only outside docs/gates/, so it has not written that file).

PRIOR GATES (from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: PASS (4th pass, head eb63f0f)
- SECURITY: PASS (2nd pass, head dbcd824)
- CODE_REVIEW: PASS (2nd pass, head 6b6256c)
- TEST: PASS (docs/test-cases/TC-HO-FIL-001.md: 60 cases, 59 PASS, 1 BLOCKED (A4's live-bucket half), 0 NOT RUN)
- Coverage note: SECURITY and CODE_REVIEW last reviewed dbcd824 and 6b6256c. The later third-round
  fixes and the master merge were reviewed by ARCHITECTURE p4 and TEST. The five post-TEST commits
  had no gate review until this one. This gate read their `src/` diff. 56efffc replaces the inline
  purge predicate with one shared `StoredFileAggregate.IsEligibleForPurge` expression, and the
  logic is equivalent. 487d2ef only reorders catch clauses and edits comments in `FilesController`.
  The reorder is safe: `InvalidDataException` is not an `IOException`, and
  `BadHttpRequestException` still precedes `IOException`. The other three commits change only
  tests and docs.

VERIFIED BY THIS GATE (commands run in D:/my-repo/cat_ecommerce-wt/FIL-001 at ae7c4d9)
- `dotnet test CatEcommerce.sln` with CATECOMMERCE_TEST_DB=...catecommerce_fil: all green.
  Domain.Tests 314/314, Application.Tests 101/101, Infrastructure.Tests 56/56, IntegrationTests 421/421.
  These counts reconcile with the TC sheet: 319 - 5 deleted PurgeEligibilityTests = 314, and
  413 + 1 migration + 6 malformed-multipart + 1 purge-query test = 421.
- `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration."
- Migrations: this branch adds exactly one migration, `20260927071352_AddFiles` (+Designer, +snapshot),
  and it sorts after `20260926135821_WidenAuditEntryValues`.
- `git merge-tree --write-tree master ho/FIL-001`: clean, tree 1929d56, exit 0. master eef1621 is the merge base.
- `npx vitest run` in web/: 37 files, 106/106 passed. The working tree was clean afterwards, and
  mockServiceWorker.js was not modified.
- No cloud credentials, bucket or clamd were configured for any of the above.

CRITERIA (§18)
- A1 MET. Uploads are type-checked, magic-byte-checked and size-capped per purpose:
  - FileRulesTests.Every_purpose_has_a_rule_and_every_declared_type_in_it_agrees_with_its_own_magic_bytes (U1)
  - .Pdf_bytes_declared_as_png_do_not_match (U2)
  - .Svg_octet_stream_and_exe_are_never_allowed_for_any_purpose and .Exe_bytes_never_match_any_allowed_content_type (U3)
  - StoredFileAggregateTests.Register_rejects_a_size_over_the_purposes_cap (U4)
  - FilesEndpointTests.Upload_one_byte_over_the_purpose_cap_is_refused_as_FileTooLarge (U4). Read: over real
    HTTP it asserts 400 with errorCode FileTooLarge.
- A2 MET. FilesEndpointTests.Upload_the_eicar_string_is_rejected_and_no_object_is_stored (I2). Read: it asserts
  400 FileRejected, and that no stored ReportExport object contains the EICAR bytes. The scan runs before
  PutAsync (FilesController ~L370-394, read). EicarTestScannerTests prove the whole stream is scanned,
  including across a chunk boundary.
- A3 MET. FilesEndpointTests.Upload_when_the_scanner_is_unavailable_is_refused_not_stored_unscanned (I3) asserts
  400 FileScanUnavailable. UnconfiguredMalwareScannerTests.ScanAsync_always_reports_Unavailable (U10).
  ClamAvScanner maps timeout and unknown results to Unavailable (source read, L34-52).
- A4 MET, with the live half carried:
  - FilesEndpointTests.Signed_url_works_twice_within_the_window_then_is_refused_after_expiry (I5/I6). It asserts
    200 twice, nosniff and Cache-Control private+no-store; after a 6-minute fake-clock advance it asserts 403
    with body "{}".
  - HmacFileUrlSignerTests: tampered fileId, exp and disp, wrong key, and a FixedTimeEquals reflection test.
  - Dicom_and_zip_are_always_served_as_attachment_even_when_inline_is_requested (I7).
  - Bucket assertions: GcsBucketPolicyCheckerTests (5 facts: UBLA, PAP, versioning, soft-delete, pass case).
  - Startup guard source in FileStorageModule L217-238 (read): outside Development/Testing it refuses a missing
    bucket, a "prod"-looking bucket name outside Production, and a signing key under 32 bytes; it resolves
    StorageClient (ADC) and calls AssertSecureAsync.
  - Carried and accepted: no real GCS bucket was exercised (V1/V2/V5). See OWNER ACTIONS.
- A5 MET.
  - FilesEndpointTests.Purge_deletes_the_object_first_then_the_row_and_a_second_run_is_a_no_op (I8) asserts the
    object and the row are both gone, and that a second run is a no-op.
  - Purge_batch_is_not_starved_by_held_rows_ordered_before_eligible_ones.
  - GetFilesForPurgeQuery_excludes_legal_hold_and_future_retain_until_includes_past_and_null.
  - FilesErasureEndpointTests.StoredFileEraser_RefusesAHeldFile_TheObjectAndRowBothRemain and
    StoredFileEraser_ANonHeldFile_ErasesTheObjectThenTheRow.
- A6 MET. FileUploadConventionTests.Only_FilesController_disables_form_value_model_binding_or_parses_multipart_itself
  (read). It reflects over every Api controller, asserts exactly two DisableFormValueModelBinding actions, both on
  FilesController, and zero IFormFile/IFormFileCollection parameters. A grep of src/ for
  MultipartReader/ReadFormAsync/IFormFile finds no other use outside FilesController.
- A7 MET. The whole .NET suite is green here (numbers above) using LocalFileStore and EicarTestScanner, with no
  GCS or clamd configuration present.

§1 AMENDED BEHAVIOUR CHANGES. Each has a named, existing, passing test:
- CatPhoto by FileId, server-verified metadata, no client StorageKey:
  - AddCatPhotoCommandHandlerTests (8 facts, incl. UnknownFileId, WrongPurpose, AnotherCaller, OutsideAttachWindow)
  - CommandValidatorsTests / AddCatPhotoCommand_has_no_client_supplied_storage_metadata
  - CatProfileProjectionEndpointTests.GetCat_FullProfile_ProjectsEveryFieldCorrectly
- Side-effect-free attach eligibility and one-file-one-cat:
  - StoredFileAggregateTests.IsEligibleForAttach_*
  - HandleAsync_PhotoLimitExceeded_/DeceasedCat_LeavesTheFileUntouchedAndAttachableElsewhere
  - CatPhotoAttachEndpointTests.Concurrent_attach_of_one_file_to_two_cats_yields_exactly_one_row
- Customer CatProfilePhoto path: CatPhotoAttachEndpointTests.Customer_uploads_and_attaches_a_photo_to_their_own_cat_and_it_is_served_back
  and Customer_cannot_attach_their_own_file_to_another_customers_cat.
- Purpose gate fails closed: FilePurposeAccessTests (4 test methods: TryGetRequiredStaffAction_refuses_every_purpose_with_no_registered_owner,
  TryGetRequiredStaffAction_maps_CatProfilePhoto_to_CatsEditProfile, IsAllowedForCustomer_refuses_every_purpose_except_CatProfilePhoto,
  IsAllowedForCustomer_allows_CatProfilePhoto).
- LegalHold enforced in the eraser, and the personal-data eraser: the FilesErasureEndpointTests (4 facts).
- Download name forced to the canonical extension, and Cc/Cf stripping: FileNameSanitizerTests.
- CSV whole-file deny-list: FilesEndpointTests.Upload_csv_with_an_hta_payload_after_benign_csv_content_is_rejected and FileRulesTests.ContainsDeniedCsvSignatureAsync_*.
- GCS streaming and leaveOpen error propagation: GcsFileStoreTests (2 facts, against FakeStorageClient).
- Compensation on a thrown save uses CancellationToken.None: When_the_save_throws_the_compensating_delete_uses_CancellationToken_None,
  plus When_the_command_save_fails_the_object_is_deleted_again_no_orphan (I4).
- Rate limits: Upload_OverLimit_ThirdReturns429 and Serve_OverLimit_ThirdReturns429.
- Malformed multipart gives 400, not 500: FilesMalformedMultipartEndpointTests (6 facts).
- Migration deletes legacy cat_photo rows: AddFilesMigrationDeletesLegacyCatPhotoRowsTests.AddFiles_DeletesPreExistingCatPhotoRows_ThenAppliesCleanly.
  Read: it asserts 2 rows before and 0 after, FileId present, the old columns gone, and ix_cat_photo_file_id present.
- One purge source per purpose, resolved by SourceKey: I8 (GetServices + SourceKey).

SCOPE
- Out-of-scope work (§4): none found. There is no EXIF, thumbnail, transcoding, V4 signed URL, direct-to-bucket
  path or quarantine store. Infected temp files are deleted, not kept.
- Files outside the literal §13 list: the Cat Profiles files, UniqueConstraintExceptionFilter.cs,
  BusinessViolationCodes.Cat.cs, the HO-CAT-001/002 docs, and the Cat test files. All are authorised
  by §1 Amended 2026-09-26 (1) and round 2 (4), plus the integrator row for CatApiFactory.cs and
  CatProfileProjectionEndpointTests.cs.
- Other declared changes: appsettings.json rate-limit entries (round 2 (6)), the Infrastructure
  csproj packages (V1), CONTRACTS.md (round 1 (20)), and SettingKeys.cs becoming partial (round 2 (5)).
- Shared constants for Files (FileRules etc.) sit in Shared per §8.3 ("FileRules (Shared)").
- The clinic schema write in AddFiles is an acknowledged exception in §14.
- Files outside allowed scope: none undeclared.

UNDECLARED DEVIATIONS
- None of substance. One minor item: StoredFilePurgeSource's heldCount is now derived from two separate
  COUNT queries (total minus eligible) rather than one. That is a statistic, not a behaviour change, and is not
  recorded in §1 Amended; it is listed under BACKLOG.

FINDINGS
- [MEDIUM] tests/CatEcommerce.IntegrationTests/Endpoints/FilesEndpointTests.cs:49 — §16 I2 also expects "audit
  entry with the signature name; no content logged". No test asserts the audit row. The evidence is source only:
  FilesController calls RecordRejectionAsync(..., scanResult.ScannerName), and
  RecordFileRejectionAuditCommandHandler writes AfterValue "Purpose=..;Scanner=..". A2 itself (the infected
  file never reaches the store) is fully evidenced, so this does not fail the gate.
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/FilesEndpointTests.cs:330-386 — this test is a time bomb in a
  long-lived test DB. Purge_batch_is_not_starved_by_held_rows_ordered_before_eligible_ones leaves 5 held rows per
  run (RetainUntil = today+30, UploadedOn = run-time minus 10 years) and never removes them. After 30 days they
  become eligible and sort before a later run's own eligible rows, so batchSize 5 purges them instead, and the
  test fails on persistent databases such as catecommerce_fil. It should clean up its held rows the way the
  GetFilesForPurgeQuery test does (ae7c4d9).
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Files/StoredFilePurgeSource.cs:50-53 — heldCount comes from
  two non-atomic COUNTs, so it can be off under concurrent writes. It is a report figure only.
- [LOW] (carried) I10 / V4 is a smoke test only (20 MB round trip). No automated memory-growth guard exists.
- [LOW] (carried) FilePurposeAccess is still one shared dictionary, not a per-area partial. It must become
  per-area before a second consuming handover registers a purpose in the same wave.
- [LOW] (carried) U11 is now covered only by integration tests. The pure unit test was deleted with the dead
  PurgeEligibility helper.
- [LOW] docs/handover/HO-FIL-001.md §13 — the allowed-scope paths (`Domain/Files/`, `Application/Files/`, …) do not
  match this solution's per-layer, per-feature layout (Aggregates/StoredFile, CommandHandlers/StoredFile,
  Shared/Common/Constants). Scope had to be judged by intent. This is a document defect for future handovers.

BACKLOG (for docs/backlog/FIL.md; owner rule, not blocking)
- The five items above: the MEDIUM I2 audit assertion and the LOWs.
- V3: run ClamAvScanner against a real clamd (the docker-compose `clamav` service) using the EICAR string.
- V1/V2/V5: live GCS verification (ADC resolves keylessly, bucket policy check passes, delete is immediate with
  soft-delete off).
- OQ-2/3/4/5 remain open with the owner: bucket per environment, DR copy, EXIF stripping, thumbnails.

OWNER ACTIONS BEFORE MERGE / FIRST NON-DEVELOPMENT DEPLOYMENT
- Merging: clean against master eef1621 (merge-tree 1929d56). AddFiles is the only new migration.
- Irreversible data effect: AddFiles runs `DELETE FROM clinic.cat_photo;`. Any existing cat photos are lost on
  upgrade and must be re-uploaded through the new flow. This is intentional and documented in round 2 (3).
  Confirm no environment holds photos you need.
- Configuration FIL requires outside Development/Testing. Startup refuses to boot without the first three:
  - FileStorage:GcsBucketName: one bucket per environment. It must not contain "prod" except in Production.
    Uniform bucket-level access on, public access prevention enforced, versioning off, soft-delete off.
    Startup asserts all four.
  - Workload Identity or ADC for the API with roles/storage.objectAdmin on that bucket only. No JSON key file.
  - FileStorage:UrlSigningKey: at least 32 bytes, a secret held outside the repository.
  - FileStorage:ClamAvHost / ClamAvPort: a reachable clamd (default port 3310). If unset, uploads fail closed with
    FileScanUnavailable. Every upload is refused; the facility does not run unscanned.
  - FileStorage:TempPath on local, non-content-root disk sized for concurrent 50 MB uploads.
  - RequestOrigin:TrustedProxies / TrustedNetworks: the load balancer or proxy addresses (§21). Without them,
    every client shares one FileServe budget (60/min) and one FileUpload budget per IP.
  - RateLimiting:FileServe / FileUpload: tune to the real page fan-out (§21).
- Behaviour to be aware of: only CatProfilePhoto is registered in FilePurposeAccess, for staff and customers.
  Every other purpose is refused with 403 FilePurposeNotAllowedForCaller until its consuming handover
  (SHP/EMR/BRD/RPT) adds its row.

SCOPE CHECK
- files in allowed scope: 131/131 (literal §13 plus the §1 Amended and integrator authorisations above)
- files outside allowed scope: none
