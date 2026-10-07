GATE: ARCHITECTURE
HANDOVER: HO-FIL-001
COMMITS: 4b10af8 4eb7e0c 4147ea8 eb63f0f — reviewed as `d7e98f1..eb63f0f` (branch ho/FIL-001, head eb63f0f; eb63f0f merges master eef1621)
DATE: 2026-09-27
STATUS: PASS

Pass: fourth ARCHITECTURE pass (delta only). The third, second and first passes are kept below, unchanged.

Verified mechanically:
- **Build.** `git archive eb63f0f` was unpacked into a new scratch folder (`fil1-arch4/head`; no git-ignored appsettings). `dotnet build CatEcommerce.sln` gave 0 errors and 0 warnings (NU1900 filtered out).
- **Model.** `dotnet ef migrations has-pending-model-changes` (Infrastructure project, Api as startup) reported "No changes have been made to the model since the last migration." Migration list ends `…AddAdministration`, `20260926135821_WidenAuditEntryValues`, `20260927071352_AddFiles`.
- **`git merge-tree --write-tree master ho/FIL-001`** is clean (tree 3d15596). master (eef1621) is the merge base, so FIL now sits directly on master.
- **Tests.** None were run. The new `CancellationToken.None` test, the rewritten `CatProfileProjectionEndpointTests`, and the CAT acceptance suite on the merged tree are for the TEST gate.

THIRD-PASS FINDINGS — CLOSURE
- MEDIUM scope failure, `RateLimitPolicies.cs`: **closed.** `git diff master eb63f0f -- src/Api/CatEcommerce.Api/RateLimiting/RateLimitPolicies.cs` is empty; the file is byte-identical to master.
- LOW `StoredFileSigningBatcher` clock: **closed.** It now takes `TimeProvider` (`StoredFileSigningBatcher.cs:16,32`). The item (8) text in §1 Amended is corrected.
- LOW duplicate `<summary>` on `CatPhotoViewModel`: **closed.**
- LOW §14 acknowledged exceptions: **closed.** The `clinic.cat_photo` DELETE and DDL are now listed next to the audit write.
- LOW `FileStorageModule.cs:130` comment ("UseRateLimiter() is already called once, from AccountsModule"): **still accurate against master and left alone.** It goes stale only when ho/NTF-001 lands (see below). Carried.
- MEDIUM carried `FilePurposeAccess` shared dictionary: unchanged. It stays on the integrator's list.

MERGE OF MASTER (eb63f0f) — CONFLICT RESOLUTIONS CHECKED AGAINST THE THIRD-PASS INSTRUCTIONS
1. `HO-CAT-001.md`: **as specified.** The diff against master is one added row, FIL's, after CAT's rows. Nothing of CAT's was removed.
2. `HO-CAT-002.md`: **as specified.** One added row, after CAT's rows and before the `*(append further rows here)*` marker. Nothing removed.
3. `AddCatPhotoCommandHandler.cs`: **as specified.** It has FIL's `fileRepository` constructor parameter and `ToDomainDto(now)`. It mutates only `CatAggregate` and calls `UpdateAsync(…, InMemoryPublishOnly)`.
4. `CatMapper.cs`: **as specified.** It takes FIL's `(PhotoId, FileId, IsPrimary, uploadedOn, …)`. The diff against master touches only this overload.
5. `GetCatProfileQueryHandler.cs`: **as specified.**
   - CAT's two-phase `AsNoTracking` projection and doc comment are kept, with FIL's two paragraphs appended. The contradicting "loads the aggregate's rows directly" sentence is gone.
   - The projection is `new { p.Id, p.FileId, p.IsPrimary, p.UploadedOn }`.
   - The code outside the old conflict markers now reads `projected.Photos`, and the final view model receives `Photos: photos`.
   - The constructor is `(logger, db, IStoredFileSigningBatcher)`.
   - The read still does not rehydrate.
6. `BusinessViolationCodes.Cat.cs`: **as specified.** CAT's `StaffAccountNotVeterinarian`, `InvalidEnumValue`, `InvalidPageSize` and `InvalidPageNumber` stay, and FIL adds `PhotoFileNotEligible`. `git grep InvalidPhotoContentType|PhotoTooLarge` over `src` and `tests` finds nothing. The stale `CommandValidatorsTests` comment is tidied.
- **Nothing of CAT's lost.** Of CAT's files, only the photo-contract edits differ from master:
  - `CatsController.cs`, `CatAggregate.cs`, `CatConfiguration.cs`, the query handler, the view model, `CommandValidatorsTests.cs`, `CatAggregateTests.cs` and the CatVaccine tests;
  - `CatApiFactory.cs` and `CatProfileProjectionEndpointTests.cs`, both covered below.
  Everything else is byte-identical to master, including:
  - the handlingNote list read, `PublicSiteOptions`/`PublicSiteStartupGuardHostedService` (`CatProfilesModule.cs`) and the paging helper;
  - the CAT acceptance/I1–I22 tests;
  - all of `web/`.
  The only deletions against master are FIL's three retired `Fakes/FileStorage` files and their tests.
- **Build fixes for CAT's new `Today` parameter:** these are mechanical constructor-argument additions in `AddCatPhotoCommandHandlerTests.cs` and `FilesIntegrationTestBase.SeedCatAsync`. There is no production change.
- **`CatProfileProjectionEndpointTests.cs`: rewritten as instructed.** It uploads a `CatProfilePhoto` through `/api/staff/files` as the same signed-in caller, POSTs `{ fileId, isPrimary }`, and asserts `photos[0].fileId`, a non-empty `url`, a non-empty `id`, and `isPrimary`.
- **`CatApiFactory.cs`:** it adds `FileStorage:UrlSigningKey` and an isolated per-instance `LocalStoreRoot`/`TempPath`, the same fix `FilesApiFactory` already has.
  - This is a test-fixture consequence of the rewrite above, and it is accepted.
  - CAT's other overrides are untouched: `DevUser`, `SignIn` and `Registration`.
  - `RateLimiting:FileUpload` is not raised here. That is fine for the one upload the test makes, but TEST should note it if more Cat tests start uploading.
- **Migration: correct.** `20260927071352_AddFiles` is ordered after `20260926135821_WidenAuditEntryValues`, and the Designer carries `[Migration("20260927071352_AddFiles")]`.
  - `Up` starts with the hand-restored `migrationBuilder.Sql("DELETE FROM clinic.cat_photo;")`, with its rationale, before the four `DropColumn`s, the `FileId` add and the `ix_cat_photo_file_id` unique index.
  - The snapshot diff against master changes only the `cat_photo` columns and index plus the new `files.stored_file` entity. WidenAuditEntryValues' widened audit columns are intact.

QUESTION FROM THE CALLER — should signed file URLs use `PublicSite:WebBaseUrl`? **Agree that they should not.**
- `HmacFileUrlSigner` issues `/api/files/{id}/content?exp&disp&sig`. That is an **API** route, served and HMAC-verified by `FilesController.GetFileContent`.
- `PublicSite:WebBaseUrl` is the **web site's** origin (HO-CAT-001 §1 Amended (b)). It exists so that printed or external QR links resolve to a web page.
- Prefixing the file URL with the web origin would point it at the Next.js app, not at the API. So it would be wrong, not merely unnecessary.
- The URL is consumed only by the browser that received it in the same API response, so a relative path resolves against whatever origin already serves `/api` to that browser. That is the same assumption every other `/api` call in the web app makes.
- If a signed URL is ever emitted into a channel that leaves the browser (an email or SMS attachment link), it will need an absolute **API** base. That is a decision for the consuming handover, not a FIL defect.

NTF MERGE (`git merge-tree --write-tree ho/NTF-001 (4efe96f) ho/FIL-001 (eb63f0f)`) — 2 content conflicts, both expected:
1. `SettingKeys.cs`: **only the doc comment conflicts.**
   - Both sides are `public static partial class SettingKeys` with identical members.
   - Resolution: take NTF's `<summary>`, and add one sentence naming `SettingKeys.Files.cs` next to `SettingKeys.Notifications.cs`.
2. `CatEcommerceDbContextModelSnapshot.cs`: both branches add migrations. NTF adds `20260927053357_AddNotificationRedactionReason` and `20260927063531_AddNotificationResendOrdinal`; FIL adds `20260927071352_AddFiles`.
   - Resolution: whichever branch merges second regenerates its own migrations and the snapshot on top of the other.
   - **If FIL merges second:** delete AddFiles and its Designer, take NTF's snapshot, run `migrations add AddFiles`, then hand-restore the leading `DELETE FROM clinic.cat_photo;` and confirm the `ix_cat_photo_file_id` name.
   - **If NTF merges second:** regenerate NTF's two migrations so their ids sort after `20260927071352`.
   - Then rerun `has-pending-model-changes`.
- Auto-merged:
  - `UniqueConstraintExceptionFilter.cs`: NTF's `ix_notification_template_key` and FIL's `CatBadRequestIndexes` are independent;
  - `appsettings.json`: NTF's `Notifications` section and FIL's `RateLimiting:FileUpload`/`FileServe` both survive.
- **Trial build of that merge:** 0 errors and 0 warnings. It was taken from merge-tree db56a66, with both conflicted files set to NTF's side, in `fil1-arch4/ntf`.
  - The build proves FIL compiles against NTF's `Program.cs`/`HostPipelineExtensions` rate-limiter move.
  - FIL's additive `AddRateLimiter(o => o.AddPolicy(...))` in `FileStorageModule` needs no code change.
  - The `FileStorageModule.cs:130` comment should be reworded to point at `Program.cs` during that merge.
  - The 429 tests on the combined tree are for TEST.

FINDINGS
- [LOW] docs/handover/HO-FIL-001.md §1 Amended (round 2) item (4) — **two Cat-side test files are missing from the authorised file list.**
  - `tests/CatEcommerce.IntegrationTests/CatApiFactory.cs` and `tests/CatEcommerce.IntegrationTests/Endpoints/CatProfileProjectionEndpointTests.cs` were changed by the merge. The merge commit message records this, but the item (4) list does not.
  - Both changes were required by this gate's third-pass merge instructions, so they are accepted as in scope.
  - Add them to item (4)'s list so the record matches the diff.
- [LOW carried] src/Api/CatEcommerce.Api/Modules/FileStorageModule.cs:130 — the comment about where `UseRateLimiter()` is called goes stale once ho/NTF-001 lands. Reword it at that merge.
- [MEDIUM carried] src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs — still a shared dictionary. The deferral is accepted, and it stays on the integrator's list.

SCOPE CHECK (delta d7e98f1..eb63f0f: 27 files authored or resolved on the FIL side; the other ~85 files in the merge diff arrive unchanged from master)
- files in allowed scope: 27/27. They are:
  - FIL's own layers: `FilesController`, `StoredFileSigningBatcher` and the Files tests/factory;
  - the Cat Profiles files under §1 Amended (round 2) item (4), plus the two Cat test files noted in the LOW above;
  - the regenerated `AddFiles` migration and the snapshot;
  - the HO-FIL-001/HO-CAT-001/HO-CAT-002 Amended rows;
  - `RateLimitPolicies.cs`, now identical to master, so its net change is zero.
- files outside allowed scope: none

VERDICT: PASS. There are no BLOCKER and no HIGH findings, and no file is outside scope. Every third-pass item is closed or carried as agreed. The six master conflicts are resolved exactly as instructed with nothing of CAT's lost, the migration is correctly ordered with the DELETE first, and the model is clean. Remaining: two LOWs and one carried MEDIUM.

---

GATE: ARCHITECTURE
HANDOVER: HO-FIL-001
COMMITS: 8dc02ca 1d2c75e 0fec3fe f9c0a4c 3133ff2 6a06d90 39b2697 6b6256c — reviewed as `git diff fdf065c..6b6256c` (branch ho/FIL-001, head 6b6256c, 36 files)
DATE: 2026-09-27
STATUS: FAIL

Pass: third ARCHITECTURE pass (delta only). The second and first passes are kept below, unchanged.

Verified mechanically:
- **Build.** `git archive 6b6256c` was unpacked into a new scratch folder (`fil1-arch3`; no git-ignored appsettings). `dotnet build CatEcommerce.sln` gave 0 errors and 0 warnings (NU1900 filtered out).
- **Model.** `dotnet ef migrations has-pending-model-changes` (Infrastructure project, Api as startup) reported "No changes have been made to the model since the last migration."
- **Tests.** None were run. The new concurrent-attach, customer-path, 429 and GcsFileStore tests are for the TEST gate to execute.
- **Superseded-architecture scan** of the added lines: none of the following appear — `IDispatcher`, `IBusMessageDispatcher`, `IEventPublisher`, MassTransit, RabbitMQ, Mongo/Bson, `202`, `IReadRepository`, `ViewModelBase`, `CommandServices`, `QueuePublishOnly`, `AddServiceBusProvider`, `SaveChangesAsync`. No code references `TryAttach`, `IsAttached` or `StoredFileAttachedEvent`; the only mentions left are historical doc comments.

SECOND-PASS FINDINGS — CLOSURE
- HIGH two-aggregate attach: **closed.**
  - `AddCatPhotoCommandHandler.cs:36-76` loads `StoredFileAggregate` read-only. It asks `IsEligibleForAttach` and then mutates and saves only `CatAggregate` (`UpdateAsync(..., InMemoryPublishOnly)`). `fileRepository.UpdateAsync` is gone.
  - `StoredFileAggregate.IsEligibleForAttach` (`StoredFileAggregate.cs:135-136`) is an expression-bodied predicate over the aggregate's own `Purpose`/`UploadedBy`/`UploadedOn`. It has no event, no `UpdateVersion` and no field write, so it is side-effect-free. `IsAttached`, `AttachedOn` and `StoredFileAttachedEvent` are removed, and the columns are gone from `AddFiles`.
  - "One file, one cat" rests on `ix_cat_photo_file_id` (`CatConfiguration.cs:99`, named explicitly in the migration). A cat-side refusal can no longer strand a file.
- **Is a per-index mapping in `UniqueConstraintExceptionFilter` the sanctioned extension? Yes.** ADR-026 establishes "index for correctness under concurrency, `23505` → the same 400 as the handler's own refusal", and names this filter. The filter is host-wide (registered once in `Program.cs:61-63`) precisely because several areas map into it. HO-APT-001, HO-BIL-001, HO-EMR-001, HO-ORD-001 and HO-SUB-001 each list `Filters/UniqueConstraintExceptionFilter.cs ← §12.5 mappings only` in their own §13, and HO-ADM-001 and HO-NTF-001 already added per-area sets. FIL's change adds one dictionary (`CatBadRequestIndexes`) and one lookup branch; unknown constraints still rethrow. The authorisation is recorded in §1 Amended (round 2) item (4).
  - Merge note: the NTF merge auto-merges this file (NTF extends `ConflictIndexes`, and FIL adds its dictionary below it). No conflict.
  - The handler has no pre-check for "already attached" (ADR-026's first layer), because the file id lives on another aggregate's rows. The index-only enforcement returns the identical 400, so no disclosure difference arises. Accepted.
- MEDIUM customer-purpose record: **closed.** §1 Amended (round 2) item (2) corrects the text. `CatPhotoAttachEndpointTests` covers the customer's own cat (201), another customer's cat (404) and the wrong uploader (400); TEST should run them.
- MEDIUM destructive migration: **closed.**
  - `20260927054251_AddFiles.cs:14-27` runs `DELETE FROM clinic.cat_photo;` before the drop/add/unique-index steps, with an inline rationale.
  - §1 Amended (round 2) item (3) records the visible one-time loss and that no accessible environment holds data.
  - HO-CAT-001's new Amended row repeats it.
- MEDIUM Cat Profiles authorisation and CAT-001/CAT-002 Amended rows: **closed, and the rows are accurate.**
  - HO-CAT-001 row: `{FileId}` request, the `{Id, FileId, IsPrimary, UploadedOn}` table, the deletion, and a Q1 `photos` shape that matches `CatPhotoViewModel(Id, FileId, Url, ContentType, SizeInBytes, OriginalFileName, UrlExpiresOn, IsPrimary, UploadedOn)` field-for-field.
  - HO-CAT-002 row: the `{ fileId, isPrimary }` body, upload-first through `/api/staff/files?purpose=CatProfilePhoto`, the 30-minute window (= `FileRules.AttachWindow`), URL expiry, and that the web page and MSW update is outstanding and the page 400s until then. Accurate.
  - The photo `Id` and `UploadedOn` are restored to the read.
- MEDIUM `SettingKeys.cs`: **closed on this branch.** It is now `static partial`, with the three keys in `SettingKeys.Files.cs`. See the NTF merge below for the one comment conflict this creates.
- MEDIUM `RateLimitPolicies.cs` scope failure: **not closed; see finding 1.** The constants moved to `FileRateLimitPolicies.cs`, which is correct. But HO-ACC-001's file is **still modified relative to master**, by a six-line FIL comment left in it.
- LOW Read-side re-implementation of sign + metadata: **closed.**
  - `IStoredFileSigningBatcher`/`StoredFileSigningBatcher` (FIL-owned, Read layer) does one `AsNoTracking` + `Select` query, then signs each file.
  - `GetStoredFileQueryHandler` is now a thin wrapper, and `GetCatProfileQueryHandler` consumes the batcher.
  - Placement and layering are acceptable. It lives in Read, depends only on the `DbContext`, the Domain port `IFileUrlSigner` and `IBusinessSettings` (the same dependencies the query handler already had), and returns a Read ViewModel. It has no Application reference. It is registered Scoped in `FileStorageModule` alongside the query handlers.
  - `.ai/namespace-conventions.md` has no row for a Read-side non-handler service. Putting it in `Read.QueryHandlers.StoredFile` beside the handler it serves is the least surprising choice. It is not a finding.
  - Cat's read (registered in `CatProfilesModule`) depending on a FIL service is a DI edge across modules. `ValidateOnBuild` will catch it at host start if `FileStorageModule` is ever omitted; the build cannot show that, so it is for TEST.
- LOW `StoredFileAttachedEvent` without a consumer: **closed** (deleted).
- LOW rate limits hard-coded, no 429 test: **closed in code.** Values are bound from `RateLimiting:FileUpload`/`FileServe` inside the lazy configure delegate (the same shape as Accounts). The 429 tests exist, and TEST should run them.
- LOW unused `InvalidPhotoContentType`/`PhotoTooLarge`: **closed** (removed).
- LOW `DateTimeOffset.UtcNow`: **partly closed.** `RegisterStoredFileCommandHandler` now uses `TimeProvider`. See finding 3.
- MEDIUM carried `FilePurposeAccess` shared dictionary: unchanged. It stays on the integrator's list.

FINDINGS
- [MEDIUM — scope failure] src/Api/CatEcommerce.Api/RateLimiting/RateLimitPolicies.cs:16-21 — **HO-ACC-001's file is still changed by this branch.**
  - `git diff master...ho/FIL-001` on it shows a six-line comment added by FIL ("FileUpload/FileServe moved to … FileRateLimitPolicies"). No constants remain, but the file is not back to master.
  - It is not in HO-FIL-001 §13, and §1 Amended (round 2) item (4)'s authorised file list does not name it (item (6) only says the constants "moved").
  - The second-pass remediation was to bring the file back into scope, which means leaving it untouched. The history belongs in `FileRateLimitPolicies.cs`'s doc comment, where it already is.
  - Fix: `git checkout master -- src/Api/CatEcommerce.Api/RateLimiting/RateLimitPolicies.cs`. That is a one-file revert with no code effect.
- [LOW] src/Api/CatEcommerce.Api/Modules/FileStorageModule.cs:130-131 — the comment says `UseRateLimiter()` "is already called once, from AccountsModule.UseAccountsModule()".
  - On ho/NTF-001 (ff449e2) that is no longer true. `UseRateLimiter()` has moved to `Program.cs:198`, after `UseAccountsModule()`, and `OnRejected` has moved to `HostPipelineExtensions.AddHostPipelineServices`.
  - Behaviour after the merge is correct. FIL's additive `AddRateLimiter(options => options.AddPolicy(...))` is exactly shape (c) of the second-pass reconciliation, and the `FileUpload` partition's dependency on `ResolvedCaller` is satisfied because `UseRateLimiter()` runs after `SessionAuthenticationMiddleware`.
  - Only the comment goes stale. Whoever merges second should reword it to point at `Program.cs`.
- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/StoredFile/StoredFileSigningBatcher.cs:30 — it still computes `urlExpiresOn` from `DateTimeOffset.UtcNow`.
  - HO-FIL-001 §1 Amended (round 2) item (8) says "`RegisterStoredFileCommandHandler`/`GetCatProfileQueryHandler` take `TimeProvider`". That is inaccurate: `GetCatProfileQueryHandler` takes no `TimeProvider`, and the clock read moved into the batcher.
  - Inject `TimeProvider` into the batcher and correct the Amended text.
- [LOW] src/Read/CatEcommerce.Read/ViewModels/CatProfileViewModel.cs:39-47 — `CatPhotoViewModel` has two consecutive `<summary>` elements. Merge them into one. Cosmetic.
- [LOW] docs/handover/HO-FIL-001.md:311 (§14) — the `DELETE FROM clinic.cat_photo` data write and the `clinic.cat_photo` DDL in `AddFiles` are writes outside the `files` schema.
  - The §14 row "Writing to any schema but `files`" is qualified only by the audit acknowledged exception.
  - Both writes are authorised in §1 Amended (round 2) items (3) and (4), so this is not a scope failure. For consistency, add them to the §14 acknowledged-exception block, as the audit write was.
- [MEDIUM carried] src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs — still a shared dictionary (deferral accepted; integrator list).

MERGE CONFLICTS (`git merge-tree --write-tree`, run 2026-09-27)
- **ho/CAT-001-gates (430e922) + ho/FIL-001 (6b6256c)** — CAT-001-gates merges first. Six files have content conflicts:
  1. `docs/handover/HO-CAT-001.md` (§1 Amended table): keep both sides. CAT's six 2026-09-26/27 rows come first, then FIL's 2026-09-27 photo-contract row.
  2. `docs/handover/HO-CAT-002.md` (§1 Amended table): keep both sides. CAT's three rows come first (including the blank-line separation already there), then FIL's row, then the `*(append further rows here)*` marker.
  3. `AddCatPhotoCommandHandler.cs`, 2 hunks:
     - constructor: take FIL's `fileRepository` parameter;
     - `AddPhoto`: take FIL's `ToDomainDto(now)`. FIL already reads `timeProvider.GetUtcNow()` into `now` for the eligibility check, so the two are equivalent.
  4. `CatMapper.cs`: take FIL's side (`new(command.PhotoId, command.FileId, command.IsPrimary, uploadedOn, ...)` with its doc comment). CAT's side still references `command.StorageKey`/`ContentType`/`SizeInBytes`/`FileName`, which FIL removed.
  5. `GetCatProfileQueryHandler.cs`, 2 hunks, **plus breakage outside the markers**:
     - keep CAT's two-phase projection and doc comment, then append FIL's two gate-fix paragraphs (drop FIL's "Loads the aggregate's rows directly" sentence, which contradicts CAT's projection);
     - in the projection, change `Photos = c.Photos.Select(p => new { p.Id, p.StorageKey, p.ContentType, p.SizeInBytes, p.FileName, p.IsPrimary, p.UploadedOn })` to `Photos = c.Photos.Select(p => new { p.Id, p.FileId, p.IsPrimary, p.UploadedOn }).ToList()`;
     - change the two `cat.Photos` references (outside the markers; `cat` does not exist on the merged side) to `projected.Photos`;
     - in the `Photos:` hunk, take FIL's `Photos: photos`;
     - keep the constructor as `(logger, db, IStoredFileSigningBatcher)`. It auto-merged to that already.
  6. `BusinessViolationCodes.Cat.cs`: keep both sides — `StaffAccountNotVeterinarian`, `InvalidEnumValue`, `InvalidPageSize`, `InvalidPageNumber` from CAT, and `PhotoFileNotEligible` from FIL. `InvalidPhotoContentType`/`PhotoTooLarge` are already gone on the merged side, and nothing in the merged tree references them. The only mention left is a stale comment in `CommandValidatorsTests.cs:59-60`, which should be tidied.
  - Auto-merged: `CatsController.cs`, both appsettings files, `CatAggregate.cs`, `Infrastructure.csproj`, the model snapshot, `CommandValidatorsTests.cs`, `CatAggregateTests.cs` and `CatVaccineAuthorizationSecurityTests.cs`.
  - **Semantic break, unchanged from the second pass:** `tests/CatEcommerce.IntegrationTests/Endpoints/CatProfileProjectionEndpointTests.cs:46-57,102` (CAT side) POSTs `storageKey`/`contentType`/`sizeInBytes`/`fileName` and asserts `photos[0].storageKey`. Rewrite it:
    - upload a `CatProfilePhoto` through `/api/staff/files` as the same staff caller;
    - POST `{ fileId, isPrimary }`;
    - assert `photos[0].fileId`, `url` and `id`.
  - **Migration:** regenerate FIL's `AddFiles` on top of CAT-001-gates' `WidenAuditEntryValues`. The regenerated migration must keep, by hand:
    - the leading `migrationBuilder.Sql("DELETE FROM clinic.cat_photo;")`, which is hand-written and **not** reproduced by `migrations add`;
    - the `ix_cat_photo_file_id` name.
    Then rerun `has-pending-model-changes`.
  - After resolving, build the merged tree. The `GetCatProfileQueryHandler` fix above is the one place a textual resolution alone will not compile.
- **ho/NTF-001 (ff449e2) + ho/FIL-001 (6b6256c)** — 2 content conflicts:
  1. `SettingKeys.cs`: **doc-comment only.** Both branches made the class `static partial`, and the member bodies are identical (neither kept its own keys in the base file). Resolution: take NTF's `<summary>` and add one sentence naming `SettingKeys.Files.cs` beside `SettingKeys.Notifications.cs`. Both per-area files (`SettingKeys.Files.cs`, `SettingKeys.Notifications.cs`) coexist without collision. This is the expected, trivial cost of both branches doing the same split independently.
  2. `CatEcommerceDbContextModelSnapshot.cs`: both branches add a migration. Whichever merges second regenerates its migration and the snapshot (for FIL, with the hand-written `DELETE` preserved as above).
  - Auto-merged: `UniqueConstraintExceptionFilter.cs` (NTF adds `ix_notification_template_key` to `ConflictIndexes`, and FIL adds `CatBadRequestIndexes`; the two are independent) and `appsettings.json` (NTF's `Notifications` section plus FIL's two `RateLimiting` entries; both survive).
  - Rate limiting: NTF has now actually done the move (`UseRateLimiter()` in `Program.cs:198` after `UseAccountsModule()`, and `OnRejected` in `HostPipelineExtensions`). FIL needs no code change for it; only the LOW comment above goes stale.

SCOPE CHECK (delta fdf065c..6b6256c, 36 files)
- files in allowed scope: 35/36. They are:
  - FIL's own layers, `FileStorageModule`, `FilesController`, `FileRateLimitPolicies.cs` and `SettingKeys.Files.cs` (per-area files);
  - the `partial` modifier on `SettingKeys.cs`, which the second-pass gate asked for;
  - FIL's own `RateLimiting` entries in the shared `appsettings.json`, which gates asked for;
  - the migration;
  - the Cat Profiles files and `UniqueConstraintExceptionFilter.cs` under §1 Amended (round 2) item (4), with the filter following the sanctioned per-area mapping pattern;
  - the HO-FIL-001/HO-CAT-001/HO-CAT-002 Amended rows;
  - FIL and Cat-side tests.
- files outside allowed scope: `src/Api/CatEcommerce.Api/RateLimiting/RateLimitPolicies.cs` — HO-ACC-001's file, still differing from master by a FIL comment; not authorised. Revert it to master.

VERDICT: FAIL. There are no BLOCKER and no HIGH findings, and every second-pass HIGH and MEDIUM is closed except one. The branch still changes `RateLimitPolicies.cs`, a file outside allowed scope, which is an automatic FAIL. Reverting that file to master (a comment-only change) would leave only LOWs and the carried MEDIUM, which is PASS on a fourth pass.

---

GATE: ARCHITECTURE
HANDOVER: HO-FIL-001
COMMITS: a48e73c 33a266d 1853cb1 d7c9bbe 6e0af33 1b66708 ea4f818 7fa3bbe 421f9c9 dbcd824 — the fix round, reviewed as `git diff ff99c44..dbcd824` (branch ho/FIL-001, head dbcd824)
DATE: 2026-09-27
STATUS: FAIL

Pass: second ARCHITECTURE pass. The first pass is kept below, unchanged.
Read:
- the HO-FIL-001 §1 Amended 2026-09-27 row and the §14 acknowledgement;
- the three first-pass records (ARCHITECTURE FAIL, SECURITY FAIL, CODE_REVIEW PASS);
- `.ai/command-handler-pattern.md` (§4–§6, summary rules), `.ai/bounded-contexts.md` (aggregate boundary rules) and `.ai/persistence-ef-core.md`;
- the ho/NTF-001 ARCHITECTURE record, for the rate-limiter item;
- every file changed in ff99c44..dbcd824 (78 files).

Verified mechanically:
- **Build.** `git archive dbcd824` was unpacked into a fresh scratch folder with no git-ignored appsettings. `dotnet build CatEcommerce.sln` gave 0 errors and 0 warnings; NU1900 was filtered out.
- **Model.** `dotnet ef migrations has-pending-model-changes` (Infrastructure project, Api as startup) reported "No changes have been made to the model since the last migration."
- **Tests.** None were run. U/I-case behaviour is for the TEST gate.
- **Superseded-architecture scan.** There is none of the following in the diff: `IDispatcher`, `IBusMessageDispatcher`, `IEventPublisher`, MassTransit, RabbitMQ, MongoDB, `202`, a rejection event, `IReadRepository`, `ViewModelBase`, or a `CommandServices` namespace.
- **Dispatch and registration.** Every new `UpdateAsync`/`SaveAsync` passes `InMemoryPublishOnly`. Everything new is registered explicitly in `FileStorageModule`: `IFilePurposeAccessPolicy`, `IGcsBucketPolicyChecker`, `IPersonalDataEraser`, and the per-purpose `IRetentionPurgeSource`. All of these are Scoped. `Program.cs` is untouched.
- **`IFileUrlSigner.TryVerify`.** It was added to contract row #8. That is allowed: FIL owns the contract ("only the owning area may change a contract"), and CONTRACTS.md rows 6–8 were updated. The only implementer outside FIL was the deleted fake. No other branch implements the port; only the baseline copies exist.

FIRST-PASS FINDINGS — CLOSURE
- HIGH C-ALL-1/C-ALL-2 (classification, eraser): **closed.**
  - `RecordClassification.Files.cs` classifies the table as Media/Purgeable.
  - `FilesPersonalDataEraser` is registered as `IPersonalDataEraser`. It honours LegalHold and RetainUntil and goes through the aggregate.
  - Note for SECURITY: it redacts `UploadedBy` and `OriginalFileName` but keeps the customer-uploaded bytes.
- HIGH CatPhoto copied file fields: **closed.**
  - `CatPhoto` is now `{Id, FileId, IsPrimary, UploadedOn}`, with a unique index on `FileId`.
  - `GetCatProfileQueryHandler` returns the §12.4 signed shape.
- HIGH attach rule in the Cat handler: **closed as asked** — `StoredFileAggregate.TryAttach`. The way it was done introduces a new HIGH (finding 1 below).
- HIGH LegalHold not enforced in `StoredFileEraser`: **closed.** See `StoredFileEraser.cs:26`.
- HIGH ADR-318 startup assertions: **closed in code.**
  - `GcsBucketPolicyChecker` asserts UBLA, PAP=enforced, versioning off and soft-delete off.
  - The startup guard forces `StorageClient` (ADC) resolution outside Development/Testing.
  - This is unit-tested against a fake client only. Checking it against a live bucket is for TEST/ops (§1 Amended (7) still applies).
- MEDIUM §10.3 settings: **closed.** `SettingDefinitions.Files.cs` added, and `FileStorageOptions` trimmed.
- MEDIUM namespace `Files` → `StoredFile`: **closed** in Domain, Application and Read.
- MEDIUM second dispatch on upload: **closed.** `RegisterStoredFileCommandHandler` returns `StoredFileRegistrationResult`, and the controller dispatches only once.
- MEDIUM `FilePurposeAccess` is a shared dictionary: **deferred, and the reason is acceptable.**
  - Only one purpose is mapped, and the Amended (23) row records that the file must become per-area before a second consumer arrives in the same wave.
  - It stays open as a carried MEDIUM for the integrator.
- MEDIUM web contract for `POST /api/cats/{id}/photos`: **still open, and not FIL's to fix.** It belongs to HO-CAT-002 and has grown; see finding 4.
- MEDIUM `Register` did not re-check `FileRules`: **closed.**
- LOW audit-schema write: **closed.** §14 now has an acknowledged exception.
- LOW bespoke `{ error }` body: **closed**, using `FilesInvalidPurpose`.
- LOW concrete signer injected: **closed.** `TryVerify` is now on the port. The Singleton lifetime stays; the signer is stateless, so that is accepted.
- LOW stale fakes: **closed.** The fakes are deleted, and the port comments and CONTRACTS.md are updated.
- LOW purge ignores purpose: **closed.** There is now one source per purpose, and eligibility is filtered in SQL.
- LOW old photo body in the CatVaccine tests: **closed.**

FINDINGS
- [HIGH] src/Application/CatEcommerce.Application/CommandHandlers/Cat/AddCatPhotoCommandHandler.cs:49-73 — **two aggregates are mutated in one command.**
  - The two aggregates are `StoredFileAggregate` (`TryAttach`, then `fileRepository.UpdateAsync`) and `CatAggregate` (`AddPhoto`, then `catRepository.UpdateAsync`).
  - Rules broken: `.ai/command-handler-pattern.md` summary rule 7 and `.ai/bounded-contexts.md` aggregate rule 1 ("raise it rather than widening the transaction").
  - The handler's doc comment calls this a "deliberate exception", but no ADR or architect decision backs it. The "precedent" it cites exists in no `.ai` document.
  - Each `UpdateAsync` commits separately (`.ai/persistence-ef-core.md`), and the file claim is committed **before** `CatAggregate.AddPhoto` runs its own invariants (`EnsureAlive`, `PhotoLimitExceeded`). So an ordinary business refusal returns 400 `PhotoLimitExceeded` and still leaves the file permanently `IsAttached`, unusable by anyone. The same happens when the cat's `UpdateAsync` hits an xmin conflict. This is not a crash-only edge case.
  - This is also the template HO-EMR-001, HO-SHP-001 and HO-BRD-001 will copy.
  - To comply, either:
    - keep the eligibility rule on `StoredFileAggregate` as a side-effect-free check (purpose, uploader, window) and do not mutate the file. The first-pass finding asked for the rule to *live* in the aggregate, not for the file to change state. The new `clinic.cat_photo.FileId` unique index already backstops "one file, one cat";
    - or take the two-aggregate attach to the architect as an ADR before it becomes the pattern.
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs:51-54 against docs/handover/HO-FIL-001.md §1 Amended 2026-09-27 item (10) — **the customer cat-photo purpose was added, but the handover says it was not.**
  - Commit a48e73c registers `FilePurpose.CatProfilePhoto` in `CustomerPurposes`. That restores the customer "add a photo" path (customers hold `Cats.EditProfile`).
  - The Amended row still says "a customer can no longer add their own cat's photo … unchanged from before this round". That misleads ACCEPTANCE.
  - No test exercises `POST /api/customers/me/files?purpose=CatProfilePhoto` followed by the attach as the owning customer. The only test mention is a doc comment in `FilesIntegrationTestBase.cs:78`.
  - Fix the Amended text, and have TEST confirm the customer path end to end, including a customer attaching to a cat they do not own → 404 by OwnerScope.
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20260927035918_AddFiles.cs:14-45 and :87-92 — **the `clinic.cat_photo` change is destructive and not safe to run over existing rows.**
  - It drops `StorageKey`, `ContentType`, `FileName` and `SizeInBytes`.
  - It adds `FileId uuid NOT NULL DEFAULT '00000000-…'` and then creates a UNIQUE index on it.
  - Any database that already holds two or more photo rows will fail the migration on the index. One existing row would silently become a photo pointing at `Guid.Empty`, which the read then hides.
  - The pre-FIL rows carry client-supplied keys that point at nothing. Either delete them explicitly in the migration (and say so in §1 Amended), or record that no environment holds `cat_photo` data.
- [MEDIUM] Cat Profiles files changed in this round — `CatAggregate.cs`, `CatPhoto.cs`, `CatPhotoDto.cs`, `CatConfiguration.cs`, `GetCatProfileQueryHandler.cs`, `CatProfileViewModel.cs`, the `clinic` DDL in `AddFiles`, and `CatAggregateTests.cs`.
  - These edits were what the first-pass HIGH required, so this gate accepts them as covered by §1 Amended (1)'s integrator authorisation for the carried item.
  - However, the 2026-09-27 row does not state integrator authorisation for the wider file set.
  - HO-CAT-001 and HO-CAT-002 have no Amended row for the changed read contract. `CatPhotoViewModel` changed from `{Id, StorageKey, ContentType, SizeInBytes, FileName, IsPrimary, UploadedOn}` to `{FileId, Url, ContentType, SizeInBytes, OriginalFileName, UrlExpiresOn, IsPrimary}`.
  - The photo `Id` and `UploadedOn` are gone from the read, yet `POST /api/cats/{id}/photos` still returns `{ catId, photoId }`, and no read exposes that `photoId`.
  - Record the authorisation. Add the CAT-001/CAT-002 Amended rows (web `CatPhoto` type, photos page, MSW mocks). Keep the photo `Id` in the view model alongside the §12.4 fields.
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Constants/SettingKeys.cs:19-23 — the change adds three keys to a shared file that is not partial. This is the same cross-area touch point that ho/NTF-001's ARCHITECTURE gate raised as MEDIUM.
  - It auto-merges with NTF today; `git merge-tree` shows both insertions survive.
  - The integrator should make `SettingKeys` `static partial`, with `SettingKeys.Files.cs` and `SettingKeys.Notifications.cs`.
- [MEDIUM] src/Api/CatEcommerce.Api/RateLimiting/RateLimitPolicies.cs:16-23 — `FileUpload` and `FileServe` were added to **HO-ACC-001's** policy-name file. That file is outside HO-FIL-001 §13, and no authorisation for it is recorded; see SCOPE CHECK.
  - ho/NTF-001 put its names in its own `NotificationRateLimitPolicies.cs`, and its gate endorsed that as the ADR-341 shape.
  - Move the two constants to `Api/RateLimiting/FileRateLimitPolicies.cs`. That is a one-file change and brings the file back into scope.
- [MEDIUM carried] src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs — still a shared dictionary rather than a per-area partial. The deferral is accepted (see above), and it remains on the integrator's list.
- [LOW] src/Read/CatEcommerce.Read/QueryHandlers/Cat/GetCatProfileQueryHandler.cs:44-63 — **signing URLs in a Read handler is acceptable, not a finding.**
  - `IFileUrlSigner` is a Domain port. §8.6/§12.4 say "a consuming read asks `IFileUrlSigner` for a URL and returns it with its view model". FIL's own `GetStoredFileQueryHandler` already does the same.
  - The smaller concern is that Cat's read now queries `files.stored_file` directly and re-implements the metadata-plus-sign mapping that `GetStoredFileQueryHandler` owns. Every consuming area will copy that.
  - A FIL-owned batch helper (ids → §12.4 shapes) would keep it "owned once". This is for HO-EMR-001, which is next to consume.
  - `DateTimeOffset.UtcNow` is used here and in `RegisterStoredFileCommandHandler.cs:57` rather than `TimeProvider`.
- [LOW] src/Domain/CatEcommerce.Domain/Events/StoredFile/StoredFileAttachedEvent.cs — it has no consumer: there is no handler and no outbox mapping. That is harmless, but it exists only because of the mutation in the HIGH above. If `TryAttach` becomes side-effect-free, delete the event together with `IsAttached` and `AttachedOn` (both regenerated in `AddFiles`).
- [LOW] src/Api/CatEcommerce.Api/Modules/FileStorageModule.cs:128-154 — `FileUpload` (20 per 10 minutes) and `FileServe` (60 per minute per IP) are hard-coded.
  - Accounts binds its policies from the `RateLimiting` configuration section, and SECURITY's remediation text asked for "the existing `RateLimiting` section".
  - No test proves a 429 on either route.
  - `FileServe` is per IP, so a clinic behind one NAT address shares 60 image loads a minute. That is for ACCEPTANCE/SECURITY to size.
- [LOW] src/Shared/CatEcommerce.Shared/Common/Constants/BusinessViolationCodes.Cat.cs:20-21 — `InvalidPhotoContentType` and `PhotoTooLarge` are now unused because `CatAggregate.AddPhoto` no longer raises them. This belongs to Cat Profiles; the integrator can remove them at merge.
- [LOW] src/Shared/CatEcommerce.Shared/Common/Constants/BusinessViolationCodes.Files.cs:21 — once ho/CAT-001-gates (`InvalidEnumValue`) has merged, `FilesInvalidPurpose` can be collapsed into it. This is optional, and avoiding the compile collision before then was sensible.

MERGE CONFLICTS (`git merge-tree --write-tree`, run 2026-09-27)
- **ho/CAT-001-gates (bbd005a) + ho/FIL-001 (dbcd824)** — 4 files have content conflicts.
  1. `AddCatPhotoCommandHandler.cs`, 2 hunks:
     - constructor: keep FIL's `fileRepository` parameter (CAT side is empty);
     - `AddPhoto` call: take FIL's `ToDomainDto(now)`, which is equivalent to CAT's `timeProvider.GetUtcNow()`.
  2. `CatMapper.cs`: take FIL's side. Both sides now take `DateTimeOffset uploadedOn`, and FIL's version carries `FileId`.
  3. `GetCatProfileQueryHandler.cs`, 2 hunks (doc comment and `Photos:`), **plus breakage outside the markers.**
     - CAT-001-gates replaced the aggregate load with a two-phase projection; its own gate raised "project, never rehydrate" as a HIGH.
     - In the auto-merged text, the projection still selects `p.StorageKey`, `p.ContentType`, `p.SizeInBytes` and `p.FileName`, which FIL removed. FIL's signing block uses `cat.Photos`, and `cat` no longer exists.
     - The result will not compile. Resolve as follows:
       - keep CAT's projection with `Photos = c.Photos.Select(p => new { p.Id, p.FileId, p.IsPrimary, p.UploadedOn }).ToList()`;
       - build `photoFileIds` and `photos` from `projected.Photos`;
       - keep FIL's constructor (`IFileUrlSigner`, `IBusinessSettings`);
       - keep CAT's doc comment and add FIL's paragraph.
  4. `BusinessViolationCodes.Cat.cs`: keep both sides — `StaffAccountNotVeterinarian`, `InvalidEnumValue`, `InvalidPageSize` and `InvalidPageNumber`, plus `PhotoFileNotEligible`.
  - Auto-merged: `CatsController.cs`, `CatAggregate.cs`, `Infrastructure.csproj`, the model snapshot, `CommandValidatorsTests.cs`, `CatAggregateTests.cs` and `CatVaccineAuthorizationSecurityTests.cs`.
  - Semantic break: `tests/CatEcommerce.IntegrationTests/Endpoints/CatProfileProjectionEndpointTests.cs:46-99` on CAT-001-gates POSTs `storageKey` and asserts `photos[0].storageKey`. It has to be rewritten:
    - upload `CatProfilePhoto` through `/api/staff/files`;
    - POST `{ fileId, isPrimary }`;
    - assert `fileId` and `url`.
  - **Migration.** CAT-001-gates merges first, so FIL's migration must be regenerated on top of `WidenAuditEntryValues`. The regenerated `AddFiles` must still contain the `clinic.cat_photo` change. Rerun `has-pending-model-changes` afterwards.
  - **Should the CatPhoto change live in FIL's migration at all?** It is acceptable, because the only ordering constraint is FIL-after-CAT-001-gates, and FIL owns the change under §1 Amended. Two conditions: first address the destructive-migration MEDIUM, then either name it in the Amended row or split it into its own `CatPhotoByFileId` migration, so that a reader of `AddFiles` is not surprised by `clinic` DDL.
- **ho/NTF-001 (ca6d3ea) + ho/FIL-001 (dbcd824)** — 1 content conflict.
  - `CatEcommerceDbContextModelSnapshot.cs`: both branches add a migration. Whichever merges second regenerates its migration and the snapshot.
  - `SettingKeys.cs` auto-merged, and both sets of keys survive.
  - **Rate limiting.** There is no textual conflict today. Neither ca6d3ea nor the NTF worktree has actually moved anything:
    - `UseRateLimiter()` is still at `AccountsModule.UseAccountsModule()`:272;
    - `OnRejected` is still in Accounts' `AddRateLimiter`;
    - the move is a recommendation in HO-NTF-001-ARCHITECTURE for the integrator.
  - FIL's policies depend on Accounts for both the 429 body and the middleware, just as NTF's do. The recommended reconciliation, done once by the integrator when the move happens:
    - (a) Move `UseRateLimiter()` into `Hosting/HostPipelineExtensions`/`Program.cs`'s shared block, **after** `SessionAuthenticationMiddleware`. FIL's `FileUpload` partition and Accounts' `ProfileUpdate` read `ResolvedCaller` from `HttpContext.Items`, so this ordering is load-bearing.
    - (b) Move the base `AddRateLimiter` (only `OnRejected` with the `TooManyRequests` `ValidationResponse`) into the same shared block.
    - (c) Leave each area's additive `AddRateLimiter(options => options.AddPolicy(...))` in its own module: Accounts, Notifications and FileStorage.
    - (d) Move FIL's two constants out of `RateLimitPolicies.cs` into `FileRateLimitPolicies.cs`. FIL can do this before merge.
    - (e) Optionally bind FIL's numbers from the `RateLimiting` section.
    - (f) Add one 429 test per area, because none of FIL's or NTF's policies is proven to run.

SCOPE CHECK
- files in allowed scope: 77/78.
  - FIL's own layers (renamed to `StoredFile`), `FileStorageModule`, `FilesController`, the per-area partials (`RecordClassification.Files`, `SettingDefinitions.Files`, `BusinessViolationCodes.Files`), `IFilePurposeAccessPolicy`, the owner-updated CONTRACTS.md rows 6–8, the handover itself and the FIL tests.
  - The Cat Profiles files and the `clinic` DDL required by the first-pass HIGH, accepted under §1 Amended (1). A MEDIUM asks for the authorisation to be recorded explicitly.
  - `SettingKeys.cs`, which is required to register §10.3 and follows NTF precedent. It is a MEDIUM, not a scope failure.
- files outside allowed scope: `src/Api/CatEcommerce.Api/RateLimiting/RateLimitPolicies.cs`. It is HO-ACC-001's file, not in §13, and no authorisation is recorded. Moving the two constants to `FileRateLimitPolicies.cs` fixes it.

VERDICT: FAIL. There is one HIGH (the two-aggregate attach) and one file outside allowed scope. Every first-pass HIGH is closed.

---

GATE: ARCHITECTURE
HANDOVER: HO-FIL-001
COMMITS: cc88238 5590969 bc8a78d d193ffd aec5d3c 96f9d01 1c21a3d 5b3849a 4fcf31f (merge of master eae3d7b) 2a0ca74 — reviewed as `git diff master...ho/FIL-001` (head 2a0ca74)
DATE: 2026-09-26
STATUS: FAIL

Pass: first ARCHITECTURE pass.
Read: HO-FIL-001 in full, including the §1 Amended row; CONTRACTS.md; the `.ai` guides (architecture,
cqrs, bounded-contexts, namespace-conventions, di-registration-guide, host-composition,
command-handler-pattern, read-side-pattern, persistence-ef-core, enumeration-pattern); ADR-318, ADR-319,
ADR-341, ADR-342 and ADR-043; HO-FIL-002 §3/§4/§13/§14; HO-CAT-002 §11.6/§20.4; and the carried item in
docs/gates/HO-CAT-002-SECURITY.md.

Verified mechanically:
- The build was run from a scratch `git archive 2a0ca74`, without a git-ignored appsettings file. It
  gave 0 errors. The only warnings were NU1900, because the vulnerability feed could not be reached.
- `dotnet ef migrations has-pending-model-changes` (Infrastructure, startup project Api) reported: "No
  changes have been made to the model since the last migration." Migration `AddFiles` is correct:
  - it writes only to the `files` schema;
  - `UploadedOn` is `timestamptz`;
  - it has a `xmin` row version;
  - it has the §11.5 indexes, plus one on `UploadedBy`.
- `Program.cs` is not edited. `AddFileStorageModule` already exists in the ADR-341 chain.
- Nothing is hand-registered that `AddInMemoryBusServices()` already provides. There are no
  `IDispatcher`, `IBusMessageDispatcher` or `IEventPublisher` types, no MassTransit, RabbitMQ or MongoDB
  residue, and no `202`.
- Every handler and validator is registered as `IValidator<T>`. Every command sets
  `ValidateCommand = true`.
- The only `SaveAsync` calls use `InMemoryPublishOnly`, apart from the audit write, which uses
  `DoNotPublish`. That matches the precedent set by ADM and ACC.
- Contract rows #6–#9 are unchanged: `IFileStore`, `IMalwareScanner`, `IFileUrlSigner`,
  `FilePutRequest`, `FileStoreResult` and `ScanResult` have no diff.
- `FilePurpose` gained only the member `CatProfilePhoto = 8`. This is additive, so consumers' fakes
  still fit. The fakes are now unregistered; see the LOW finding below.
- New NuGet packages:
  - `Google.Cloud.Storage.V1` 5.0.0 and `nClam` 9.0.0 are named by ADR-318, ADR-319 and §7 V1, so they
    are permitted.
  - `Microsoft.Extensions.TimeProvider.Testing` 9.0.0 was added to Infrastructure.Tests. It is already
    referenced by two other test projects at the same version.
- `docker-compose.yml` at the repository root is explicitly allowed by §13 and §12. `DisableFormValueModelBindingAttribute` sits in `Api/Filters/`, next to the other filters. Both are fine.
- The upload action taking the §8.2 steps itself is the exception that §11.4 sanctions. It holds no
  DbContext, repository or aggregate.
- The runtime behaviour needs checking at the TEST gate (U1–U12, I1–I10). This gate did not run any
  tests.

FINDINGS
- [HIGH] src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/StoredFileConfiguration.cs:14 — duty C-ALL-1 (CONTRACTS.md, cross-cutting duties; HO-XCT-001 §5.2) is not met.
  - The rule: classify every table you add in your own `RecordClassification.[Area].cs`, in the same change that adds the table.
  - What the code does: it adds `files.stored_file` with no `RecordClassification.Files.cs`, so the table is missing from `RecordClassification.All`. HO-NTF-001, in the same wave, ships `RecordClassification.Notifications.cs`.
  - C-ALL-2 is also unanswered. The context stores `UploadedBy`, `OriginalFileName` and customer-uploadable content (`POST /api/customers/me/files`), but it registers no `IPersonalDataEraser` and records no exemption reason.
  - Because the table is unclassified, C-ALL-4 cannot be evaluated either. Is a `LabResult` or `ConsentForm` row a clinical record that `IStoredFileEraser` may hard-delete? The classification is what decides that.
- [HIGH] src/Application/CatEcommerce.Application/DataMappers/CatMapper.cs:38 (with src/Domain/CatEcommerce.Domain/Entities/CatPhoto.cs) — the carried fix copies `file.StorageKey`, `ContentType`, `SizeInBytes` and `OriginalFileName` into `CatPhoto`. It does not store the `StoredFile`'s id.
  - Rule broken: `.ai/bounded-contexts.md` rule 2, "Aggregates reference each other by id".
  - Serving is impossible. The only serving path is keyed on `fileId` (`IFileUrlSigner.Sign(fileId, …)`, `GET /api/files/{fileId}/content`, §8.6). No Cat read can ever issue a signed URL for a cat photo. The §12.4 shape `{ fileId, url, … }`, which "every consuming area returns … for a file", is unreachable for Cat, and `GetCatProfileQueryHandler` keeps returning a raw storage key to the browser.
  - Deleted objects leave stale keys. FIL's purge and erase (`StoredFilePurgeSource`, `IStoredFileEraser`) can delete the object while `clinic.cat_photo` still holds its key. The link that would make this visible does not exist.
  - Nothing ties a photo to a file. The same `FileId` can be attached to any number of cats, or several times to one cat, within the attach window.
  - The authorisation in §1 Amended covers touching Cat's files. It does not set aside the aggregate-reference rule.
- [HIGH] src/Application/CatEcommerce.Application/CommandHandlers/Cat/AddCatPhotoCommandHandler.cs:67 — the attach-eligibility rule is a private predicate in a Cat handler. The rule checks `Purpose == CatProfilePhoto`, `UploadedBy == caller`, and `now - UploadedOn <= FileRules.AttachWindow`.
  - §1 Amended itself calls it "a FIL-owned policy … not a Cat-specific rule", and it is decided entirely from `StoredFileAggregate`'s own state.
  - Rule broken: `.ai/command-handler-pattern.md` §6, "If a rule can be broken by looking only at the aggregate's own state, it belongs in the aggregate."
  - This is the template that HO-SHP-001, HO-EMR-001 and HO-BRD-001 will copy when they attach a file. Each copy can drift, which is the "one way, owned once" problem that HO-FIL-001 §2 exists to remove.
  - The read-only `StoredFileAggregate` lookup from a Cat handler is itself fine: it is an existence and ownership check, and only one aggregate (Cat) is mutated.
- [HIGH] src/Infrastructure/CatEcommerce.Infrastructure/Files/StoredFileEraser.cs:15 — `LegalHold` ("a held file is never purged", §8.1) is not enforced on the single erase path.
  - `IStoredFileEraser.EraseAsync` deletes a held file if asked. The hold is checked separately in `DeleteStoredFileCommandHandler.cs:35` and in `StoredFilePurgeSource.cs:42` (through an `AsNoTracking` snapshot).
  - The erasure described in §8.7 ("an erasure deletes the object and the row") will call this port directly, and `ErasureOutcome.Held` exists for exactly this case.
  - An invariant held in the aggregate's own state lives in the aggregate or the one path that acts on it, not in each caller (`.ai/command-handler-pattern.md` §6).
- [HIGH] src/Api/CatEcommerce.Api/Modules/FileStorageModule.cs:120 — part of ADR-318's decision is not built.
  - ADR-318 says bucket configuration is "all of it asserted at startup and by an infrastructure test": uniform access, public-access prevention, versioning off, soft-delete off. §8.4 also says startup refuses "credentials that do not resolve".
  - `FileStorageStartupGuardHostedService` checks only the bucket name and the signing key. `StorageClient.Create()` (line 46) is lazy, so ADC is never resolved at startup. No bucket-policy assertion exists anywhere.
  - §1 Amended (7) says the GCS bucket assertions "were not exercised". In fact they were never written. That undocumented deviation from the slice's own charter ADR has to be either built or recorded as a deferral. SECURITY should rule on depth.
- [MEDIUM] (no file) — the §10.3 settings are not in the HO-ADM-001 registry: `files.maxUploadMb`, `files.signedUrlMinutes` and `files.scanTimeoutSeconds`.
  - There is no `SettingDefinitions.Files.cs`.
  - `SignedUrlMinutes` and `ScanTimeoutSeconds` are read from `FileStorageOptions` instead (src/Infrastructure/CatEcommerce.Infrastructure/Configuration/FileStorageOptions.cs:28 and :35). §10.3 reserves configuration for the bucket, credentials, scanner host and key only.
  - This deviation is not documented in §1 Amended.
- [MEDIUM] src/Domain/CatEcommerce.Domain/Aggregates/Files/StoredFileAggregate.cs:8 — the namespace segment is `Files` in every layer (`Aggregates.Files`, `Commands.Files`, `Events.Files`, `Queries.Files`). HO-FIL-001 §8 and ADR-125 name the segment `StoredFile`.
  - The deviation is not documented.
  - The plural form also breaks the singular convention of its neighbours (`Cat`, `Account`).
  - Either rename the segment, or add an Amended row. `.ai/bounded-contexts.md` still has no `Files` row: OQ-1 is open and belongs to the architect, not this branch.
- [MEDIUM] src/Api/CatEcommerce.Api/Controllers/FilesController.cs:296 and :305 — on success, the upload action dispatches `RegisterStoredFileCommand` and then `GetStoredFileQuery`.
  - Rule broken: `.ai/cqrs.md` §5 rule 1, "a controller dispatches exactly one command or one query". §11.4 sanctions the streaming exception, not a second dispatch.
  - The controller already holds the signer, so the response could be shaped without the query.
- [MEDIUM] src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs:35 — `FilePurposeAccess` is a single shared append-only registry that consuming areas will edit, not ADR-341's per-area partial.
  - This is documented in §1 Amended (3), and it is tolerable because the handover already makes `FilePurpose.cs` and `FileRules.cs` single-file touch points for consumers.
  - It must become per-area before two consuming areas run in the same wave.
  - OQ-7 is resolved against its recommended default: an unmapped purpose is permitted for every staff role, including `ReportExport`, which is normally server-generated. This is documented in §1 Amended (2). It is an authorisation decision for SECURITY, not something this gate rules on.
- [MEDIUM] src/Api/CatEcommerce.Api/Contracts/Cat/AddCatPhotoRequest.cs:11 — the HTTP contract of `POST /api/cats/{catId}/photos` changed to `{ fileId, isPrimary }` with no consumer change alongside it. Merged alone, this branch leaves the staff Photos page failing with 400 on every submission.
  - The web must change in three places:
    - `web/lib/api/cats.ts:306-311`: `AddCatPhotoRequest` becomes `{ fileId: string; isPrimary: boolean }`, and `CatPhoto` (lines 61-66) must eventually carry `fileId` plus a signed URL instead of `storageKey`. That second part depends on the HIGH finding about `CatPhoto` above.
    - `web/app/(staff)/staff/cats/[catId]/photos/page.tsx:33-38`: the page first sends a multipart POST to `/api/staff/files?purpose=CatProfilePhoto`, with CSRF and `X-Branch-Id` headers through the one shared client. It reads `fileId` from the 201 `StoredFileViewModel`, then POSTs `{ fileId, isPrimary }` within 30 minutes. `FilePurposeNotAllowedForCaller`, `FileTooLarge`, `FileTypeNotAllowed`, `FileContentMismatch`, `FileRejected`, `FileScanUnavailable` and `PhotoFileNotEligible` all need mapping onto the field.
    - `web/mocks/handlers/areas/cat-profiles.ts` must be updated to match.
  - Ownership: the page and route belong to **HO-CAT-002**, as an Amended row that replaces §11.6 and §20.4. HO-FIL-002 §14 forbids FIL-002 from touching any page.
  - The uploader itself (`useFileUpload()` / `<FileUploader>`) is **HO-FIL-002**'s. The CAT-002 change should consume it once it exists, rather than building a second client upload path.
  - On the backend, ho/CAT-001-gates adds `CatProfileProjectionEndpointTests.cs:46-99`, which POSTs `storageKey` and asserts it back. It will fail once both branches merge (see MERGE CONFLICTS).
- [MEDIUM] src/Domain/CatEcommerce.Domain/Aggregates/Files/StoredFileAggregate.cs:43 — `Register` does not check `ContentType` and `SizeInBytes` against `FileRules.Find(Purpose)`, although both can be checked from the aggregate's own state. The controller pipeline is the only guard.
  - §8.2 rightly runs these checks before the scan, but `CatAggregate.AddPhoto` sets the local precedent of re-checking in the aggregate.
- [LOW] src/Application/CatEcommerce.Application/CommandHandlers/Files/RecordFileRejectionAuditCommandHandler.cs:44 — it writes `audit.audit_entry`.
  - §14 forbids "writing to any schema but `files`", while §8.2 step 5 requires an audit entry.
  - This follows the ADM/ACC precedent. The handover should acknowledge the exception in §14.
- [LOW] src/Api/CatEcommerce.Api/Controllers/FilesController.cs:393 — `TryParseEnum` returns a bespoke `{ error }` 400 body instead of the ADR-014 `ValidationResult` shape. The HO-CAT-001 gates flagged this same defect in `CatsController`, and ho/CAT-001-gates fixes it there with `InvalidEnumValue`.
- [LOW] src/Api/CatEcommerce.Api/Controllers/FilesController.cs:37 and FileStorageModule.cs:73 — the controller injects the concrete Infrastructure `HmacFileUrlSigner` to reach `TryVerify`, and the signer is a Singleton although §6 and §12 say adapters are Scoped. The signer is stateless, so this is harmless. It is recorded as a deviation.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Fakes/FileStorage/*.cs — the three fakes are now unregistered dead code.
  - The port doc comments in `IFileStore.cs`, `IMalwareScanner.cs` and `IFileUrlSigner.cs` still say "until HO-FIL-001 lands".
  - The Fake column of CONTRACTS.md rows 6–8 is now stale. The integrator should update it at merge.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Files/StoredFilePurgeSource.cs:30 — purge ignores purpose, although §8.7 says "given a purpose and a cut-off". This is documented as a limitation of the `IRetentionPurgeSource` contract. It is noted for HO-XCT-001.
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/CatVaccineAuthorizationSecurityTests.cs:35 and CatVaccineFullAuthorizationMatrixTests.cs:82 — both still POST the old photo body `{ storageKey, contentType, … }`. They probably pass only because authorisation runs before binding. The TEST gate should check that the authorised-role rows are still meaningful.
- [LOW] .ai/templates/*.cs — known stale, and not used to judge this change. No template defect is newly relevant to this diff.

MERGE CONFLICTS (git merge-tree --write-tree)
- ho/FIL-001 + ho/CAT-001-gates (d5985fe) — 3 content conflicts:
  - `AddCatPhotoCommandHandler.cs`: both add a constructor parameter. The union is `(logger, catRepository, fileRepository, timeProvider)`.
  - `CatMapper.cs`: CAT-001-gates threads `uploadedOn` through as a parameter and removes the mapper's own `DateTimeOffset.UtcNow`. FIL adds a `StoredFileAggregate file` parameter. The resolution needs both, `ToDomainDto(command, file, uploadedOn)`, and the handler call has to be updated to match.
  - `BusinessViolationCodes.Cat.cs`: both append at the end of the file. Keep both, including `PhotoFileNotEligible`.
  - Auto-merged: `CatsController.cs`, `Infrastructure.csproj`, the model snapshot (both branches add a migration: WidenAuditEntryValues 20260926135821 and AddFiles 20260926143144) and `CommandValidatorsTests.cs`.
  - Semantic conflict: CAT-001-gates' `CatProfileProjectionEndpointTests` uses the old photo body and will fail at runtime.
  - Rerun `has-pending-model-changes` after resolving.
- ho/FIL-001 + ho/NTF-001 (9a9e739) — 1 content conflict: `CatEcommerceDbContextModelSnapshot.cs`, as expected, because both add a migration (AddFiles 20260926143144, AddNotificationsModule 20260926152619).
  - Under the integrator plan, whichever branch merges second regenerates its migration and snapshot.

SCOPE CHECK
- files in allowed scope: 81/81. 72 are FIL's own files: the §11/§12 specified changes mapped to the solution's layer folders, the ADR-341 per-area partials, the `files` migration, the compose `clamav` service, the §1 Amended row and the FileStorage example config. The other 9 are Cat Profiles files covered by the §1 Amended integrator authorisation:
  - `AddCatPhotoRequest.cs`
  - `CatsController.cs`
  - `AddCatPhotoCommand.cs`
  - `AddCatPhotoCommandHandler.cs`
  - `AddCatPhotoCommandValidator.cs`
  - `CatMapper.cs`
  - `BusinessViolationCodes.Cat.cs`
  - `AddCatPhotoCommandHandlerTests.cs`
  - `CommandValidatorsTests.cs`
- files outside allowed scope: none unauthorised
