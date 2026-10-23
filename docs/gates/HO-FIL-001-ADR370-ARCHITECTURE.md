GATE: ARCHITECTURE
HANDOVER: HO-FIL-001 (owner-approved follow-up ADR-370, amends ADR-318; owner approval 2026-10-18, "all three")
COMMITS: 914ab5f0 (master...feat/direct-image-urls, worktree D:/my-repo/cat_ecommerce-img)
DATE: 2026-10-18
STATUS: PASS

No BLOCKER or HIGH findings. The MEDIUM and LOW items below do not block (gate rule, owner 2026-09-27). They belong in docs/backlog/FIL.md.

FINDINGS
- [MEDIUM] docs/handover/HO-FIL-001.md:74,75,311 (and §9 OQ-4/OQ-5/OQ-8): the handover is not amended for ADR-370.
  - §4 still lists "Image resizing, thumbnails" and "GCS V4 signed URLs" as out of scope.
  - §14 still forbids "a signed GCS URL handed to a browser (ADR-318)".
  - The code now does both. ADR-370 and the owner's approval authorise it, so this gate does not count it as a forbidden-scope breach. ACCEPTANCE, however, judges against §14 and will find the contradiction.
  - Separately, HO-SHP-001 files were touched (ProductImageUrls.cs, GetProductDetailQueryHandler.cs and two Shop test files), and no Amended row records this. ADR-370 §1 names the behaviour these files implement (card 480 px, product page 1,200 px), so in substance they are authorised.
  - docs/handover/CONTRACTS.md row 6 still lists IFileStore as Put/OpenRead/Delete only.
  - Fix: add a §1 Amended row to HO-FIL-001 that cites ADR-370 and lists the file set. Annotate the §4, §14 and OQ-4/5/8 rows as amended for `FileStorage:DirectServePurposes`. Mirror the row in HO-SHP-001. Update CONTRACTS row 6.
- [MEDIUM] src/Api/CatEcommerce.Api/Background/ImageVariantBackfillWorker.cs:31: the Api-layer worker resolves `CatEcommerceDbContext` and queries `StoredFiles` itself. It is the only class in Api/Background that does this. The others read through a dispatched query (ShopWorkCycle uses `GetDueStockAlertsQuery`) or an Infrastructure work-queue class (RetentionWorkQueue). The read is read-only and touches no aggregate, so the problem stays contained. Fix: list the keys through a Read query (for example `GetStoredFileKeysByPurposeQuery`) or an Infrastructure/Files helper.
- [MEDIUM, for TEST] src/Api/CatEcommerce.Api/Modules/FileStorageModule.cs:89-98, 241-260: HO-FIL-001 §8.4 says "startup refuses credentials that do not resolve". `FileStorageStartupGuardHostedService` forces `StorageClient`, but it never resolves `UrlSigner`/`GcsDirectFileUrlSigner` and never signs once.
  - If the IAM Service Account Credentials API is off, or `roles/iam.serviceAccountTokenCreator` is missing (ADR-370 §4), the host still starts.
  - Every product-image read then throws: catalogue, product page, cart, orders, wishlist. There is no fallback to the HMAC URL.
  - This is runtime behaviour this gate cannot observe. TEST and ACCEPTANCE should verify one direct-signed fetch on the VM.
  - Suggested fix: resolve the signer and sign one probe key in the startup guard outside Development/Testing. Alternatively, fall back to the HMAC URL and log an error.
- [LOW, for TEST] src/Infrastructure/CatEcommerce.Infrastructure/Configuration/FileStorageOptions.cs:36: `DirectServePurposes` has an initializer default of `["ProductImage"]`. The .NET configuration binder appends configured array elements to a pre-populated array; it does not replace them. So configuration can add a purpose but cannot remove `ProductImage`, and there is no configuration switch that turns direct serving off. TEST should confirm this. Fix: default to null or empty and apply `ProductImage` only when the section is absent, or add an explicit enable flag.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Configuration/FileStorageOptions.cs:40, src/Infrastructure/CatEcommerce.Infrastructure/Files/GcsFileStore.cs (VariantCacheControl): `DirectUrlWindowMinutes` is a URL-lifetime knob kept in configuration.
  - Its analogue `files.signedUrlMinutes` was moved to the ADM settings registry under §10.3 (gate-fix 2026-09-27).
  - ADR-370 names it as configuration explicitly, so the placement is sanctioned. `DirectServePurposes` and `BackfillVariants` are correctly configuration: they are deployment decisions, like the bucket.
  - The window is clamped to 5-720 minutes, but the copies carry a hard-coded `max-age=3600`. ADR-370's trade-off ("at most 1 hour") therefore only holds at the default.
  - Suggested fix: clamp to 30 or record the coupling in ADR-370.
- [LOW] src/Domain/CatEcommerce.Domain/DomainServices/IFileStore.cs:27,30: the port gained `PutVariantAsync` and `ExistsAsync`. ADR-318 said "the port does not change", and ADR-370 mentions only the change to `DeleteAsync`. The additions are reasonable, but ADR-370 should record them.
- [LOW] src/Api/CatEcommerce.Api/Modules/FileStorageModule.cs:100: `CatEcommerce.Api.Background.ImageVariantBackfillWorker` is fully qualified where a using directive would do. Cosmetic.

NOTED FOR SECURITY (not architecture findings)
- SkiaImageVariantGenerator.cs:34-38: `GetScaledDimensions` only reduces JPEG at decode time. A 5 MB PNG or WebP with very large pixel dimensions is decoded at full size (w*h*4 bytes) inside the upload request, which is a decompression-bomb surface. A pixel-count cap before decoding would close it.
- Bytes served directly from the bucket skip nosniff, the sandbox CSP and Referrer-Policy (ADR-370 accepts this). For a direct-served purpose with no variant requested, the original (client-uploaded bytes) is served from the bucket, not only the WebP copies.

VERIFIED (no finding)
- Layering:
  - New ports `IDirectFileUrlSigner` and `IImageVariantGenerator` are in Domain/DomainServices and reference only Shared.
  - Adapters `GcsDirectFileUrlSigner`, `NoDirectFileUrlSigner` and `SkiaImageVariantGenerator` are in Infrastructure/Files.
  - `FileVariants` is in Shared.Common.Constants (flat, beside FileRules).
  - Read's `StoredFileSigningBatcher` depends on the Domain port, not the adapter. Application does not reference Read, and Read does not reference Application.
- No new migration and no schema change. `StoredFileAggregate` is untouched and stays immutable; copies are objects only.
- Program.cs, csproj files and appsettings are untouched. No new NuGet package: SkiaSharp, Google.Cloud.Storage.V1 (UrlSigner) and the EF Core-transitive Caching.Memory were already present.
- Nothing from the superseded architecture: no IDispatcher, IBusMessageDispatcher, MassTransit, Mongo, 202, rejection events or projections.
- Upload pipeline order matches ADR-370 §1: copies are made after the scan and before PutAsync. A decode failure becomes `FileRejected` and records an audit entry. If storing a copy fails or throws, `DeleteAsync` removes the original and any copies (the same no-orphan compensation as §8.2 step 7). Still one dispatch per outcome.
- `DeleteAsync` (Local and GCS) removes the copies, derived from the server-generated key prefix (StorageKeyFactory uses `{purpose.DisplayName}/...`). Retention, erasure and compensation are unchanged.
- Read side:
  - The batcher still uses one `AsNoTracking` query projected with `Select`.
  - Access decisions stay with the calling endpoint (§4 and §14 "deciding who may read" unchanged).
  - Purposes not listed keep the HMAC app-served URL exactly as in ADR-318.
  - The query writes nothing; the in-memory URL cache is not persistence.
- DI per environment: `IDirectFileUrlSigner` selects `NoDirectFileUrlSigner` in Development/Testing and `GcsDirectFileUrlSigner` elsewhere, the same rule as `IFileStore`. `UrlSigner` is created lazily, so Development/Testing never touches ADC.
- Lifetimes: the new adapters are Singleton, stateless and hold no Scoped dependency, matching the `HmacFileUrlSigner` Singleton precedent. There is no Transient in the request path.
- The backfill worker is a BackgroundService in the single host (no second host). It is off by default, uses a scope per unit and writes no rows.
- No signing key file: signing uses ADC (ADR-318 credential rule kept). Bucket policy assertions are unchanged; public access prevention is compatible with V4 signed URLs.
- ADR-370 is a unique number, its status line states that it amends ADR-318, and ADR-FE-155 carries the "Narrowed by ADR-370" note.

SCOPE CHECK
- files in allowed scope: 29/29.
  - FIL-owned (20): ports, adapters, options, FilesController, FileStorageModule, the backfill worker, the Read StoredFile batcher and interface, Shared FileVariants, and FIL tests.
  - Decision records (2): .ai/decisions/adr.md and web/docs/decisions.md.
  - HO-SHP-001 (4): ProductImageUrls.cs, GetProductDetailQueryHandler.cs, ShopSection16GapEndpointTests.cs and ShopStaffProductDetailReadEndpointTests.cs. These are authorised by ADR-370 §1 and the owner's approval; the paperwork gap is the first MEDIUM finding.
  - Shared test helper (1): TestImages.cs.
  - Remaining FIL integration-test support (2): FilesApiFactory.cs and FilesIntegrationTestBase.cs.
- files outside allowed scope: none. The Shop files are counted as in scope on the strength of ADR-370 and the owner's approval. If the integrator requires an Amended row before counting them, the first MEDIUM finding closes that gap.
