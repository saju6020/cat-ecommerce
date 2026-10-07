GATE: CODE_REVIEW
HANDOVER: HO-SHP-001-FU1
COMMITS: af7304b 67a8158 becc18f
DATE: 2026-10-06
STATUS: PASS

CHECKS RUN
- web: `npx tsc --noEmit` clean; `npm run lint` clean (max-warnings 0); `npx vitest run --maxWorkers=3` 137 files / 694 tests passed.
- `dotnet build` 0 warnings, 0 errors.
- `dotnet test --filter FullyQualifiedName~ShopStaffProductDetailRead` 3/3 passed (FU1_T1, FU1_T1b, FU1_T2).

REVIEW NOTES (no findings at BLOCKER/HIGH)
- Handler mapping: images ordered by SortOrder, product-level and SKU-level together with SkuId carried; food null when FoodDetails is null; enumeration DisplayName used for lifeStage, proteinSource and healthFocus, which is what the write side parses (EnumerationParsing.TryParse), proven by FU1_T2. Read-only, additive, no migration.
- Web forms: Details, Food and Images initialise via useForm defaultValues from loaded values, so isDirty compares against them and the unsaved-changes guard is not tripped by loading. Save does reset(values); the page stays mounted on save (not keyed by version) and Reload refetches then bumps the key, so the form re-initialises from fresh values. No conflict with useTwoStepSave (invalidate only).
- S1 regression test: without the `healthFocus: product.healthFocus ?? []` change the default is [] and the assertion on the sent health focus fails, so the test does guard the fix.
- Mock: Details, food and images PUTs store what they are sent and the read returns it, so the round trip works.

FINDINGS
- [MEDIUM] tests/CatEcommerce.IntegrationTests/Endpoints/ShopStaffProductDetailReadEndpointTests.cs:FU1_T1/FU1_T2 — only one product-level image is seeded. Multi-image ordering by sortOrder and a SKU-level image (skuId non-null) are not asserted, and the FU1_T2 re-save drops skuId. Add a second image with a SKU scope, written out of sortOrder order, and assert order and skuId on read and after re-save.
- [LOW] src/Read/CatEcommerce.Read/ViewModels/StaffProductDetailViewModel.cs — Food, Images, HealthFocus and Label are declared nullable with default null, while the web type treats them as required and the handler always supplies them. Make them non-nullable (Label string) so the contract cannot drift.
- [LOW] web/mocks/handlers/shop/staff.ts:staffFood — the mock returns food null whenever every labelling field is empty. The real read returns an object when FoodDetails exists with empty fields. Harmless to the forms (both initialise to empty) but the mock is not exact.
- [LOW] web/tests/components/staff-shop/staff-shop.test.tsx — no test that Reload re-initialises a form from refetched values (the new initial values interplay with the Reload counter), and no test that a loaded form is not dirty and so does not trip the unsaved-changes guard. Behaviour is correct by reading; add one test each.
- [LOW] web pages — a form mounted from stale cached product data would start from stale healthFocus, food and images (same limitation as every other field; Reload is the remedy). Note only.

SCOPE CHECK
- files in allowed scope: 16/16 (incl. docs/gates ARCHITECTURE and SECURITY records, docs/backlog/SHP.md, web/docs/decisions.md)
- files outside allowed scope: none (web/public/mockServiceWorker.js left untouched and uncommitted)
