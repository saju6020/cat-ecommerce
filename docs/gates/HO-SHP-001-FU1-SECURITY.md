GATE: SECURITY
HANDOVER: HO-SHP-001-FU1
COMMITS: af7304b, 67a8158, 748eb1b (range 578dc79...748eb1b)
DATE: 2026-10-06
STATUS: PASS

FINDINGS
- none (no BLOCKER, HIGH, MEDIUM or LOW)

WHAT WAS CHECKED
- Authorization: `GET /api/staff/shop/products/{productId:guid}` keeps
  `[RequireAction(Actions.Shop.ProductsView)]` (StaffProductsController.cs:78). `GetStaffProductDetailQuery`
  is dispatched only from this action. `StaffProductDetailViewModel`/`StaffProductImageViewModel` are
  referenced by no customer or public controller. The public reads (GetProductDetailQueryHandler,
  GetCatalogueQueryHandler, WishlistAndStockAlertQueryHandlers) are unchanged and still project
  `ProductImageRef(null, altText, isPrimary, skuId)` with no fileId.
- Data exposure: the new fields (food labelling, image fileId/altText/sortOrder/isPrimary/skuId,
  healthFocus, SKU label) are catalogue content that the storefront shows once a product is published.
  The staff read already returned draft data (description, translations, version, checklist) to the
  same ProductsView holders, so no new class of data reaches a new audience.
- File ids: a fileId is not a bearer credential. `GET /api/files/{fileId}/content` is authorised only
  by an HMAC signature verified with FixedTimeEquals (FilesController.cs:104-118). URLs are signed only
  by RegisterStoredFileCommandHandler, to the uploader at upload time, and by StoredFileSigningBatcher for
  GetCatProfile/GetStoredFile, which no controller exposes for arbitrary ids. The read returns
  `Url = null`, so it signs nothing.
- Re-attach via the round-trip: SetProductImagesCommandHandler skips the eligibility check only for
  fileIds already on *this* product (`existingFileIds`). Any other fileId must pass
  `IsEligibleForAttach(ProductImage, callerUserId, now, AttachWindow)`: right purpose, uploaded by the
  caller, and still fresh. Learning another product's fileId from this read therefore does not let a
  caller attach it elsewhere. The write needs ProductsManage, the same action FilePurposeAccess maps for
  ProductImage uploads.
- Rendering: ingredientList and storageInstructions are bound to react-hook-form inputs as values
  (`register(...)`). feedingGuide goes to a textarea plus the shared `LimitedMarkup` preview, which
  builds React elements and has no dangerouslySetInnerHTML (LimitedMarkup.tsx:9). altText/fileId are
  form values (ImagesEditor.tsx). `sku.label` renders as a JSX text child (VariantsTable.tsx:118).
  `grep dangerouslySetInnerHTML|innerHTML` over web/components and web/lib finds no sink.
- Mass assignment: no request contract or command changed. UpdateDetails, SetFood and SetImages still
  map explicit request properties into their commands. Re-sending the read's extra fields (url, label,
  sortOrder) either binds to existing, validated properties or is dropped by the binder.
- Mocks/tests: the MSW fixtures and test files carry no secrets. The uncommitted
  web/public/mockServiceWorker.js was left alone.

SCOPE CHECK
- files in allowed scope: 13/13 (backend read handler + view model, integration test, web staff-shop
  components/api/mocks/tests, handover doc, backlog row)
- files outside allowed scope: none
