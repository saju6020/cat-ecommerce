GATE: SECURITY (DELTA)
HANDOVER: HO-SHP-001
COMMITS: 4bbd6c3 (one product file, FilePurposeAccess.cs; tests; HO-SHP-001 §1 Amended)
DATE: 2026-10-04
STATUS: PASS

SCOPE OF THIS PASS
A delta review of everything that landed after HO-SHP-001's ARCHITECTURE (2nd), SECURITY (2nd),
CODE_REVIEW (2nd) and TEST passes: `git diff 8fe100b HEAD`, which is commit 4bbd6c3 only. The only
product-code change maps `FilePurpose.ProductImage` to `Actions.Shop.ProductsManage` in
HO-FIL-001's shared `FilePurposeAccess.cs`. Before this change the purpose had no mapping, so the
fail-closed policy refused it for every staff role. That meant no product image could be uploaded
in the real host. HO-FIL-001's own tests never saw the problem because they run behind the
test-only `UnownedPurposeAllowingPolicy` decorator.

VERIFICATION PERFORMED
- `git diff 8fe100b HEAD --name-only`: under `src/`, only
  `src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs` changed. Its diff adds one
  entry, `[FilePurpose.ProductImage] = Actions.Shop.ProductsManage`. The `CatProfilePhoto` entry,
  `CustomerPurposes` (still `{CatProfilePhoto}` only), `TryGetRequiredStaffAction` and
  `IsAllowedForCustomer` are unchanged. No other purpose's behaviour changes.
- Enforcement path: `FilesController.UploadStaffFile` (`POST /api/staff/files`) uses
  `[RequireAction(Actions.Files.Upload)]` to accept any staff role. It then consults
  `IFilePurposeAccessPolicy.TryGetRequiredStaffAction`, then
  `ActionRoles.TryGetRoles(requiredAction)`, and finally requires the caller's roles to intersect
  that list. Otherwise it returns 403 `FilePurposeNotAllowedForCaller`. Production DI
  (`FileStorageModule.cs:85`) registers `DefaultFilePurposeAccessPolicy`, which delegates to the
  static map. `ActionRoles.Shop.cs:14` limits `Products.Manage` to `ShopManager` and
  `Administrator`. As a result, the other six staff roles (Receptionist, Veterinarian, and so on)
  are still refused `ProductImage`.
- Customers: `UploadCustomerFile` (`POST /api/customers/me/files`) gates on `IsAllowedForCustomer`,
  which is still false for `ProductImage`. The staff endpoint needs `Files.Upload`, a staff-only
  action. Customers therefore cannot upload `ProductImage` by either route.
- Attaching an image to a product: `SetProductImagesCommandHandler` sits behind
  `[RequireAction(Actions.Shop.ProductsManage)]` on `StaffProductsController`, so the action that
  governs the upload is the same one that governs the attach. For every newly referenced FileId it
  requires `StoredFileAggregate.IsEligibleForAttach(FilePurpose.ProductImage, callerUserId, now,
  AttachWindow)`, which checks purpose, uploader and freshness. So a file uploaded under another
  purpose, or by another user (for example a customer's `CatProfilePhoto`), cannot become a product
  image. Products are not branch-scoped, so there is no object-level gap beyond Products.Manage.
- The mapping matches the specs: HO-SHP-001 §11 C5 `SetProductImagesCommand` is a Staff command
  under Products.Manage. HO-FIL-001 OQ-7 says each purpose's action is "named by the consuming
  handover", and HO-FIL-001 §8.3 lists ProductImage as consumed by HO-SHP-001.
- `FileRules` is not in the diff. The `ProductImage` row is still jpeg/png/webp at 5 MB, so
  image/gif is still refused. This is proven by the new `U_A5b_NoProductImageRuleAllowsGif` and
  `U_A5b_ProductImageFile_WithGifContentType_IsRefused`, and over HTTP by
  `U_A5b_GifProductImage_IsRefusedAtUpload...` and
  `U_A5b_ProductImageUpload_IsRefusedForAGifEvenWhenThePartIsLabelledAsPngOnlyByExtension`, which
  both expect 400 `FileTypeNotAllowed`.
- HO-FIL-001 ownership: HO-SHP-001 §1 Amended (2026-10-04, Phase F) records the change, its cause,
  the role set (ShopManager, Administrator), that customers are still refused, and the
  `FilePurposeAccessTests` edit. In `FilePurposeAccessTests`, `ProductImage` was removed from the
  "unmapped" member data. In its place are two explicit facts: a positive mapping to
  `Shop.ProductsManage`, and `IsAllowedForCustomer_refuses_ProductImage`. The other five purposes
  are still asserted unmapped and refused to customers, so the HO-FIL-001 guarantee for them is not
  weakened.
- Test seams: `ShopApiFactory` adds `FileStorage:UrlSigningKey` (the same test literal already
  used in `CatApiFactory` and `FilesApiFactory`, not a production secret, and absent from every
  `src/` appsettings) and a per-factory temp `LocalStoreRoot`/`TempPath`. These are test project
  only. Production DI is unchanged, and `FileStorageModule.cs:232` still requires a key of at least
  32 bytes outside Development/Testing. `ShopApiFactory` does NOT register the allow-all purpose
  decorator, so the new Shop upload tests use the real fail-closed policy as ShopManager. This is
  the first real-host proof that the mapping works.
- Tests: `ShopSection16GapEndpointTests` mints a unique brand per test (`NewBrand`) and asserts
  within that brand. I-A12 also seeds a decoy with the same name under another brand, and the CSV
  export test asserts exactly 4 lines. `ShopCatalogueEndpointTests.Search_NoResults` now asserts
  `totalCount == 0` unconditionally, guaranteed by a brand filter that matches nothing, and then
  asserts the brand appears in `didYouMean`. The old `if` that could skip the assertion is gone.
  `ProductSection16GapTests` (Domain) asserts what its names say, including the gif rule, the
  bounds and sum of the guaranteed analysis, and the R3 error codes.

Commands run (worktree `D:/my-repo/cat_ecommerce-wt/SHP-001`,
`CATECOMMERCE_TEST_DB=...Database=catecommerce_shp...`):
- `dotnet build`: Build succeeded, 0 warnings, 0 errors.
- `dotnet test tests/CatEcommerce.Domain.Tests --no-build`: 683/683 passed.
- `dotnet test tests/CatEcommerce.Domain.Tests --no-build --filter
  "FilePurposeAccessTests|ProductSection16GapTests|FileRules"`: 71/71 passed.
- `dotnet test tests/CatEcommerce.IntegrationTests --no-build --filter
  "ShopSection16GapEndpointTests|ShopCatalogueEndpointTests|FilesEndpointTests|FileStorage|FilePurpose"`:
  51/51 passed.
- `dotnet test tests/CatEcommerce.Infrastructure.Tests --no-build --filter Files`: 33/33 passed.

FINDINGS
No BLOCKER or HIGH findings.

MEDIUM/LOW (owner rule 2026-09-27: these do not block; for `docs/backlog/SHP.md`, not edited here):
- [LOW] `src/Shared/CatEcommerce.Shared/Common/Constants/FilePurposeAccess.cs:16-26`: the type's
  doc comment is now stale. It still says "Only CatProfilePhoto is mapped so far" and that
  `ProductImage` is a test-only purpose registered by `FilesApiFactory`'s decorator, "never here".
  Both statements contradict line 54. The decorator's comment in `FilesApiFactory.cs` likely needs
  the same update.
- [LOW] No HTTP-level negative test exercises the real policy for this purpose. Nothing asserts that
  a staff role without Products.Manage (for example Receptionist) gets 403
  `FilePurposeNotAllowedForCaller` on `POST /api/staff/files?purpose=ProductImage`, or that a
  customer is refused it on `/api/customers/me/files`. The map is unit-tested, and the controller
  path was verified by reading, but the wiring for the newly opened purpose has only a positive
  test.
- [LOW] `tests/CatEcommerce.Domain.Tests/Common/FilePurposeAccessTests.cs:17`:
  `EveryCanonicalPurposeExceptCatProfilePhoto` now also excludes `ProductImage`, so its name is
  wrong. The list is also hand-maintained rather than derived from every `FilePurpose` member, so a
  future purpose that is neither listed nor mapped would escape the fail-closed assertions.

SCOPE CHECK
- Product code: `FilePurposeAccess.cs` only (one dictionary entry and its comment). Confirmed with
  `git diff 8fe100b HEAD --name-only`.
- Other files: tests (`ProductSection16GapTests.cs`, `FilePurposeAccessTests.cs`,
  `ShopCatalogueEndpointTests.cs`, `ShopSection16GapEndpointTests.cs`, `ShopApiFactory.cs`,
  `ShopIntegrationTestBase.cs`) and `docs/handover/HO-SHP-001.md` (§1 Amended). None is outside
  allowed scope. The cross-handover edit to HO-FIL-001's file is recorded in HO-SHP-001 §1 Amended.
- Files in allowed scope: 8/8. Files outside allowed scope: none.
