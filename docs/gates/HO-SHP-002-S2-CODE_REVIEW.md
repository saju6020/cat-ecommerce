GATE: CODE_REVIEW
HANDOVER: HO-SHP-002-S2
COMMITS: 7805d6a bf2ab07
DATE: 2026-10-06
STATUS: PASS

CHECKS RUN (web/): `tsc --noEmit` clean; `eslint lib components app` clean; `prettier --check lib/api/errors.ts` clean; `vitest run --maxWorkers=3` 137 files / 691 tests passed.

VERIFIED
- Non-shop areas: the fallback chain is `errorMessage || codeMessages?.[code] || generic`. With no map the result equals pre-SHP-002 behaviour whenever errorMessage is non-empty; an empty message now yields the generic sentence. Every non-shop caller (CAT forms, APT, BKG, ADM, BRN, CNT, LVE, RST, SET, NTF, account, auth) passes no map.
- Shop call sites: every caller under the shop slice passes SHOP_CODE_MESSAGES: submitSection.ts (all section forms), ReviewForm, NewProductForm, OriginsTable, AddSkuDialog, ReviewModerationQueue, VariantsTable (status and, via the new prop, ScheduleChangeDialog), product layout.tsx, useStaffShop useTwoStepSave, and ProductPurchase (wishlist and stock-alert errors). CatalogueView reads errors[0].errorMessage directly with its own fallback and does not use the helpers. The portal shop pages, WishlistButton, StockAlertButton and WishlistList use none of the three helpers. No `shopCodeMessage` reference remains.
- ScheduleChangeDialog's other users (admin services, tax-classes) pass no map, so they get the generic default.

FINDINGS
- [MEDIUM] web/tests/unit/errors.test.ts — the new tests cover applyValidationErrors and describeApiError only. Nothing asserts that submitWithServerErrors forwards `codeMessages` (it passes `options` through whole, so it works today), that ScheduleChangeDialog's `codeMessages` prop reaches describeApiError, or that submitSection / ReviewForm etc. pass the shop map. A call site that drops the map again would not fail any test.
- [LOW] web/lib/api/errors.ts:~103 — the describeApiError doc comment line now exceeds 100 columns after the edit (Prettier does not rewrap comments); re-wrap.
- [LOW] web/lib/api/errors.ts:39, 113 — the empty-`errors` branches still use a literal duplicate of the new GENERIC_ERROR_MESSAGE constant; use the constant.
- [LOW] Architecture fit — shop opts in at roughly 11 call sites by passing a map, while APT uses a named area function (describeAppointmentError). Both live in errors.ts and both keep wording out of the default, so the result is consistent in outcome, but a `describeShopError` / shop submit wrapper would stop a new shop call site from silently omitting the map. Optional.

SCOPE CHECK
- files in allowed scope: 14/14 (web error helpers, shop call sites, the one shared admin dialog prop, unit test, backlog row S2)
- files outside allowed scope: none
