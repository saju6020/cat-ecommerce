# HO-SHP-001-FU1 — Staff product detail read completeness (backlog S1)

| Field | Value |
|---|---|
| Handover ID | **HO-SHP-001-FU1** (follow-up slice to [`HO-SHP-001`](HO-SHP-001.md) / [`HO-SHP-002`](HO-SHP-002.md)) |
| Date | 2026-10-06 |
| Backlog | [`docs/backlog/SHP.md`](../backlog/SHP.md) row **S1** (owner-approved 2026-10-06) |
| Origin | `docs/gates/HO-SHP-002-ARCHITECTURE.md` point 5; HO-SHP-002 §1 Amended item (6)(a); ACCEPTANCE C-1 |
| Branch | `ho/SHP-001-FU1` from master `578dc79` |
| Status | Backend: Complete. Web: Complete |

## 1. Purpose

`GET /api/staff/shop/products/{productId}` (HO-SHP-001 §12.4) did not return food labelling, images,
health focus or the SKU label, so the staff forms were write-only and every save replaced data the
form never loaded. Most visibly, a Details save on a food product cleared its health focus
(`UpdateProductDetailsCommandHandler`: the command's `HealthFocus` replaces the stored set). This
slice returns those fields so the forms can load them and re-save them unchanged.

Governing architecture: [`HO-SHP-001`](HO-SHP-001.md) §12.4 (staff read), [`HO-SHP-002`](HO-SHP-002.md)
§11.6 and §1 Amended, ADR-FE-034 and ADR-FE-035 (`web/docs/decisions.md`), and the read-side pattern
(`.ai/read-side-pattern.md`).

## 2. Scope — backend (this half)

Read side only. **No schema change, no EF migration, no write-side change, no new route.**

Allowed files:

- `src/Read/CatEcommerce.Read/ViewModels/StaffProductDetailViewModel.cs`
- `src/Read/CatEcommerce.Read/QueryHandlers/Product/GetStaffProductDetailQueryHandler.cs`
- `tests/CatEcommerce.IntegrationTests/Endpoints/ShopStaffProductDetailReadEndpointTests.cs`
- this document

Additive only: no existing field renamed or removed. Per-SKU `stock` stays deferred (backlog row 51).

## 3. Response shape added

`GET /api/staff/shop/products/{productId}` (`Products.View`), camelCase. New top-level fields:

| Field | Type | Notes |
|---|---|---|
| `food` | object or `null` | `null` when the product has no food details (never set, or not a food product). Same shape as the public `food` object |
| `food.manufacturer` | string or null | |
| `food.lifeStage` | string or null | Enumeration name, as `PUT …/food` accepts |
| `food.proteinSource` | string or null | Enumeration name |
| `food.ingredientList` | string or null | Not localised (staff see the base text; translations are in `translations`) |
| `food.guaranteedAnalysis` | object or null | `{ proteinPercent, fatPercent, fibrePercent, moisturePercent }`, decimals; all four or null |
| `food.feedingGuide` | string or null | Base text, LimitedMarkup |
| `food.storageInstructions` | string or null | |
| `images` | array | Ordered by `sortOrder`; `[]` when none. Product-level and SKU-level together |
| `images[].fileId` | guid | Pass back to `PUT …/images` |
| `images[].url` | string or null | Always `null` for now, as the public `ProductImageRef` (no media URL builder yet) |
| `images[].altText` | string | |
| `images[].sortOrder` | int | |
| `images[].isPrimary` | bool | |
| `images[].skuId` | guid or null | `null` = product-level image |
| `healthFocus` | string[] | Enumeration names (`Sterilised`, `WeightManagement`, `Urinary`, `Renal`, `Digestive`, `SkinAndCoat`, `Hairball`, `Dental`, `Sensitive`), as `PUT …/{productId}` accepts; `[]` when none |

New field on each `skus[]` item:

| Field | Type | Notes |
|---|---|---|
| `skus[].label` | string | Same label the public page shows (`Sku.LabelFor`: size, flavour, colour and "Pack of N" joined with " · "; the product name only when none is set) |

`skus[]` already returned `code`, `barcode`, `sizeOrWeight`, `flavour`, `colour`, `packQuantity`
(the SKU attributes); they are unchanged.

Round trip: the values returned in `food`, `images[]` (`fileId`, `altText`, `sortOrder`, `isPrimary`,
`skuId`) and `healthFocus` are valid input to `PUT …/food`, `PUT …/images` and `PUT …/{productId}`
(`healthFocus`) respectively, and `version` is the `expectedVersion` for each.

## 4. Tests

`tests/CatEcommerce.IntegrationTests/Endpoints/ShopStaffProductDetailReadEndpointTests.cs`

| ID | Asserts |
|---|---|
| FU1-T1 | After writing food details, images, health focus and SKU attributes, GET returns all of them, including `skus[].label` |
| FU1-T1b | A product with no food details or images returns `food: null`, `images: []`, `healthFocus: []` |
| FU1-T2 | Round trip: read, re-save Details with the returned `healthFocus`, re-save food and images from the returned objects; health focus, food and images are preserved |

## 5. Acceptance criteria

- A1. The staff detail returns `food`, `images`, `healthFocus` and `skus[].label` as in section 3.
- A2. No existing response field is renamed, removed or retyped.
- A3. No migration, no change under `src/Domain`, `src/Application`, `src/Infrastructure` or `src/Api`.
- A4. Re-saving Details with the returned `healthFocus` leaves the product's health focus unchanged (FU1-T2).
- A5. Full integration suite (`Category!=Performance`) and the performance test (I-E11) show no regression.

## 6. Web (frontend half)

Allowed files:

- `web/lib/api/staff-shop.ts`
- `web/mocks/handlers/shop/staff.ts`, `web/mocks/handlers/shop/fixtures.ts`
- `web/components/staff-shop/ProductDetailsForm.tsx`, `FoodDetailsForm.tsx`, `ImagesEditor.tsx`, `VariantsTable.tsx`
- `web/tests/components/staff-shop/staff-shop.test.tsx`
- `web/docs/decisions.md` (ADR-FE-034 status line only)
- `docs/backlog/SHP.md` (row S1)
- `docs/gates/HO-SHP-001-FU1-*.md` (gate records)
- this document

Forbidden: anything under `src/`, `tests/` (backend), or any migration; any new route, endpoint, library or state-management approach; image upload (ADR-FE-035 stands); per-SKU stock (backlog row 51); the unrelated modification to `web/public/mockServiceWorker.js`.

Done 2026-10-06. Scope: `web/` only; no backend change.

- **Type** (`web/lib/api/staff-shop.ts`): `StaffProductDetail` gains `food` (`StaffFoodDetail | null`), `images` (`StaffImage[]`, with `url`) and `healthFocus` (`string[]`); `StaffSku` gains `label`. All four are required, not optional, as the API now always sends them.
- **Mock** (`web/mocks/handlers/shop/staff.ts`, `fixtures.ts`): the staff detail returns the same shape (`food` is `null` until any labelling field is set; `images` ordered by `sortOrder`, `url` null). The Details, food and images `PUT`s now store what they are sent, so a read after a save round-trips.
- **Forms:**
  - `ProductDetailsForm` starts with the stored `healthFocus` ticked, so a Details save that does not touch it sends it back (the S1 regression).
  - `FoodDetailsForm` and `ImagesEditor` start from `food` and `images` (they already read these fields; they now receive data).
  - `VariantsTable` shows `skus[].label` under the SKU code.
- **Copy removed:** "stored selection isn't shown here yet", "stored labelling isn't shown by the API yet. Saving replaces…" and "current images aren't shown…; saving replaces the set". The image-upload limitation note (ADR-FE-035) stays.
- **Tests:** component tests in `web/tests/components/staff-shop/staff-shop.test.tsx` ("Forms start from the loaded values"): each form shows the loaded values, and Details saved without touching health focus sends the loaded health focus. The e2e shop-staff spec asserted nothing about empty forms and is unchanged.
