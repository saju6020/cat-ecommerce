GATE: ACCEPTANCE
HANDOVER: HO-SHP-002
COMMITS: 6540601 3eb7984 7a60f37 f08090b 3f218da 143bf0d 69e201b d3ade14 0b71a75 f8841c8 87d152c 80c57fb dc8da0c ea686a1 8b09024 (code; branch ho/SHP-002, range cffaa79..8b09024); ac3f9b3 (owner decision); gate records d096f3a ceb45bb 316209d 0db96a5 b9021c3 0a88bf3
DATE: 2026-10-05
STATUS: PASS

First pass. This gate read:
- all of HO-SHP-002, including §1 Amended items (1)-(10) and the 2026-10-04 owner decision on `home.spec.ts` and the sign-in allow-list;
- the four prior gate records and `docs/test-cases/TC-HO-SHP-002.md`;
- the tests and source cited for each §18 row.

Owner rule (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings belong in `docs/backlog/SHP.md`, which this gate has not edited.

MSW-only evidence is judged as in the HO-ADM-002, HO-FIL-002, HO-NTF-002 and HO-APT-002 ACCEPTANCE records:
- A criterion is MET on the MSW e2e plus unit evidence.
- The real-API half is carried as a condition. It is not counted as a pass.
- The TC sheet's seven real-API rows all read "not run — no API". Amended (8) and the sheet give the reasons, and none of them is counted here.

Owner-confirmed HO-SHP-001 decisions apply to every availability, purchasability and image clause:
- availability fails closed until HO-IMP-001;
- prescription diets, and bundles that contain them, are not purchasable;
- images are placeholders;
- HO-SHP-001 §18 A4/A5/A17/A18 are deferred to HO-IMP-001 (SHP backlog row 51).

Where a clause depends on one of these, it is marked "MET BY DEFERRAL (not evidenced)", the term the HO-SHP-001 ACCEPTANCE record uses.

PRIOR GATES (read from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: PASS on the second pass (ceb45bb).
  - The first pass (d096f3a) FAILED on 1 BLOCKER: two §14 files.
  - The owner decision recorded in §1 Amended and §13 (ac3f9b3) closed it.
  - 0 HIGH. 3 MEDIUM and the LOWs are carried.
- SECURITY: PASS (316209d). 0 BLOCKER, 0 HIGH, 1 MEDIUM, 4 LOW.
- CODE_REVIEW: PASS on the second pass (b9021c3).
  - The first pass (0db96a5) FAILED on 2 HIGH: the two-command saves and the sheet price focus.
  - Both were fixed in 8b09024 and were confirmed by tests that fail when the defect is reintroduced (TC R-5).
- TEST: PASS (0a88bf3), run at code head 8b09024.
  - The sheet has 69 rows: 68 PASS, 1 FAIL (S-1, MEDIUM), 0 BLOCKED, 0 NOT RUN.
  - `cat-vaccinations` E9 is pre-existing. The branch has no change in that area.
- No product code has changed since 8b09024. `git diff --stat 8b09024 0a88bf3` touches only the TC sheet and two gate records.

VERIFICATION RUN BY THIS GATE (web/, worktree at 0a88bf3, code identical to 8b09024)
- `npx vitest run tests/components/{shop,catalogue,product,prescription,reviews,wishlist,recommendations,staff-shop} --maxWorkers=3`: 15 files, 110 tests, all passed.
- Playwright and builds were not re-run. The e2e, build and production-bundle evidence comes from the TEST record:
  - chromium shop specs: 28/29 on the first run; the `home.spec` cold-fixture 404 passed 4/4 on rerun;
  - mobile-360: 29/29;
  - `shop-a11y-responsive`: 42/42 on each project;
  - `next build`: exit 0.
- Checked against the merged backend by this gate:
  - `src/Read/CatEcommerce.Read/ViewModels/ProductDetailViewModel.cs:5-18`: the **public** detail read returns `Food` (manufacturer, life stage, protein, ingredients, analysis, feeding guide, storage) and per-SKU `Images`.
  - `StaffProductDetailViewModel.cs`: the **staff** detail read returns none of food, images or health focus.
- `docs/backlog/SHP.md` (branch and master, 58 lines): row 51 covers stock and the expiry notice only. **No row exists for the staff detail-read gap**, which ARCHITECTURE asked for. See C-1.

CRITERIA (HO-SHP-002 §18)

| # | Result | Evidence |
|---|---|---|
| A1 | MET (MSW; storefront half and image-data clause carried, see C-1, C-2) | E-C1 (`shop-staff.spec.ts:15-88`) PASS on both projects. It covers create (name, brand, category, tax class, suggested slug), a SKU and price, a refused publish, then publish, and ends with a draft slug answering **404** at `:86-87`. U-D1 (`staff-shop.test.tsx:90`): Publish is enabled with an unmet item, and the refusal focuses the checklist. Every FR-SHP-2 field has a control: name, brand, description with preview, tags, category/subcategory and slug in `ProductDetailsForm.tsx:136-280`; status actions in `ProductEditorHeader` (`staff-shop.test.tsx:123`); images in `ImagesEditor` (U-D5 `:311`). Categories come from `GET /api/shop/categories` (`NewProductForm.tsx:55`), so every FR-SHP-1 category the server defines is offered. Images: the editor takes a `fileId`, and no upload exists (owner-confirmed placeholders, ADR-FE-035, §20.7). The "visible in the shop" half of E-C1 is a real-API case |
| A2 | MET (MSW); real stock MET BY DEFERRAL (not evidenced) | U-B1 (`product-page.test.tsx:40`): `?sku=` with price ৳650 → ৳2,450 → ৳4,400, and the 4 kg variant's own image first. U-B2 (`:61`): an out-of-stock variant offers the alert while the others read In stock. E-B1 PASS on both projects. U-D3 (`staff-shop.test.tsx:205`): `stock: null` reads "No stock recorded" and a real 0 reads "0 sellable". Fail-closed case: `product-page.test.tsx:132`, every SKU out of stock with honest text and no purchase control (TC S-4). The API sends no staff `stock` and every SKU is `OutOfStock` until HO-IMP-001 (row 51), so mixed availability on real data is deferred |
| A3 | MET (MSW); "edited" carries **condition C-1** | Shown: U-B4 (`product-page.test.tsx:158`) shows ingredients as text, the analysis `<table>` with `%` in every cell, the feeding guide through `LimitedMarkup`, storage, and the origin with its manufacturer. The public read carries `Food` (checked above). Edited: U-D2 (`staff-shop.test.tsx:157,167`): the food form appears for Cat Food with `%` units and is absent for Litter; the four-or-none analysis rule is at `:191`. E-C1 saves a life stage through the food section. Prescription flag: the confirm names the consequence (`:182`). TC R-2 (`two-step-save.test.tsx:49`) sends labelling, then `PUT …/prescription`. Publishing without origin: E-C1 `:57-66` is refused with an alert naming "country of origin", the checklist is focused and the status stays Draft. See the judgement below |
| A4 | MET (MSW); budget clause **not evidenced in this slice**, carried (C-3) | U-A1, U-A2, U-A5, U-A6 PASS (`catalogue-controls.test.tsx`, `catalogue-params.test.ts`). R-3: the sheet's price input keeps focus (the CODE_REVIEW HIGH fix). E-A2 (`shop-catalogue.spec.ts`) PASS: filters, sort and page survive reload, and Back restores the previous state; Back is exercised from the next navigation, as Amended (9) explains. E-A1 PASS on MSW. "Responses meet the budget in the AC-5 run" has no frontend measurement ("not run — no API"). The only budget evidence is backend-side: HO-SHP-001 ACCEPTANCE A11, I-E11 p95 267-278 ms against 500 ms. That is supporting evidence, not this criterion's run |
| A5 | MET (MSW) | U-C1 (`shell.test.tsx:122`): a globe for a flagless origin, the name as text, the count and a notes excerpt, with flags `alt=""`. U-C2 (`catalogue-controls.test.tsx`): origin fixed, every other filter group present. E-A1 Shop by Country → Thailand → Kitten + In stock PASS on both projects |
| A6 | MET (MSW; matching is the server's, evidenced in HO-SHP-001) | U-A4 (`product-card.test.tsx`): Did you mean rewrites `q` and keeps filters, and the popular grid renders. E-A3 (misspelled brand, nonsense) and E-A4 (ingredient, tag) PASS on both projects. Fuzzy and ingredient/tag matching are server behaviour, evidenced backend-side (HO-SHP-001 ACCEPTANCE A16, I-E8) |
| A7 | MET (rendering); populated data MET BY DEFERRAL (not evidenced) | U-B3 (`product-page.test.tsx:90`): the best-before sentence appears with `expiryNotice` and not without it. U-D3 as in A2. ARCHITECTURE verified that availability, the notice and the checklist are read only from API fields (`StatusText`, `VariantSelector`, `ProductPurchase`, `VariantsTable.tsx:25-28`). The API does not yet populate `ExpiryNotice` or staff `stock` (row 51) |
| A8 | MET | U-B9 (`reviews-recommendations.test.tsx`): an empty block is absent, and the heading reads "for your cats" when `basis` is `YourCats`. The blocks are fetched client-side from the server's recommendation endpoint, and no exclusion logic exists client-side (ARCHITECTURE) |
| A9 | MET (MSW; real run after HO-ORD-001) | U-B8: Write / Edit your review / the NotPurchased text. U-B12: `<b>x</b>` and `**x**` render literally. U-E2 (`portal-shop.test.tsx:75`): ReviewNotPermitted is shown plainly and the values are kept. U-D9 (`staff-shop.test.tsx:479`): Reject needs a reason. E-C3 (`shop-staff.spec.ts:117,135`): the moderated review appears on the product page. All PASS. The real run is impossible before HO-ORD-001, since no purchase can exist (§16) |
| A10 | MET (MSW; `/book` landing carried) | U-B6 (`prescription.test.tsx:27,40,51`): a guest gets a sign-in link with `?next=`; with no authorisation, the reason and Book a consultation → `/book?category=Consultation`; eligible cats are listed with end dates and nothing clinical. `:60`: a failed read gives a neutral retry, never "no authorisation" (the CODE_REVIEW fix; the wiring gap is TEST MEDIUM). E-B2 PASS, asserting the destination URL. Not purchasable: there is no purchase control anywhere (empty action slot, `product-page.test.tsx:124`; TC S-5), consistent with the owner decision |
| A11 | MET, **component half only; HO-ORD-002 records the cat on the order line** | U-B7 (`prescription.test.tsx:79,97`): arrow keys move, Space selects, `onChange` fires with the cat id and has no side effect, and the required error is announced. E-X1 includes the prescription gate. Wiring the picker to add-to-cart is HO-ORD-002's (§18 Deferred) |
| A12 | MET (MSW) | U-E1 (`portal-shop.test.tsx:50`): each reason as text, the allergy exclusion sentence and the date-of-birth hint. E-D3 (`shop-portal.spec.ts`): chicken products absent and the exclusion sentence shown. PASS on both projects. The hint links to the cat's profile, not an edit page, because none exists in the portal (Amended (6)(g), declared) |
| A13 | MET (MSW) | U-B5 (`product-page.test.tsx:245`): components with quantity and availability text; the kit is unavailable when a component is out. E-B3 PASS on both projects. A kit containing a prescription diet shows the server's availability, and an alert on it is refused with `PurchaseNotPermitted` (ARCHITECTURE; owner decision) |
| A14 | MET (MSW); E-D2 carried | U-B10: `willNotify: false` gives the OQ-7 text and a settings link. U-B11 (`product-page.test.tsx:102`): a guest Save → `/sign-in?next=` back to the same product and variant. U-E3 (`portal-shop.test.tsx:186`). E-D1 (wishlist, and alerts with and without consent) PASS on both projects. E-D2 (one back-in-stock message in the inbox) is real-API only and needs real stock (row 51) |
| A15 | MET (MSW; storefront edit half carried) | U-F2 (`shell.test.tsx:99,104`), U-F3 (`product-page.test.tsx:216`: the feeding guide `lang="en"`, the rest `lang="bn"`), U-D7 (`staff-shop.test.tsx:410,417`). E-F1 PASS for the staff `bn` edit persisting in the editor. "The edit appears in the shop" is not runnable under MSW (Amended (8)) |
| A16 | MET (MSW) | Banners: `shell.test.tsx:149,175` (title and body through `LimitedMarkup`, a link only when present and safe, nothing when empty). They are wired in `(shop)/layout.tsx:34,58-62` (placement `Shop`) and `shop/page.tsx:34,50` (`Home`). Maintenance: U-F1 (`shell.test.tsx:34,57,74,85`) and E-E1 (the storefront shows the notice while `/staff/shop/products` works) PASS |
| A17 | axe and 360-1440 MET (MSW); LCP ≤ 2.5 s **not evidenced**, carried (C-3) | E-X1 and E-X2 (`shop-a11y-responsive.spec.ts`): every §8.1 route, the open filter sheet, the prescription gate, the review form and the editor with errors, at 360/768/1024/1440. 42/42 on chromium and on mobile-360. E-X3 ran its structural half only: a plain `<img>` with dimensions and `fetchpriority=high`, lazy loading elsewhere, and the body server-rendered without JavaScript. The LCP figure on throttled 4G needs the real API and a production build, and no measurement exists |

**Judgement: the "edited and shown" criteria (A1, A3) against the staff detail-read gap**
- **The decision: an integrator/owner condition (C-1), not an unmet criterion.**
  - "Shown" is met. Customers see the labelling on the storefront from the public read, which carries `Food` (U-B4).
  - "Edited" is met as a capability. Staff can set every FR-SHP-4 field and the prescription flag, and the server accepts it (U-D2, R-2, E-C1).
- **What fails is re-editing.** The staff read returns no food, images, health focus or SKU attributes. So each form starts empty and a save replaces what is stored. A Details save on a food product also clears health focus, which feeds A12's reasons.
- **Why that does not unmeet A1 or A3:**
  - It is a backend contract defect. HO-SHP-001 §12.4 promises a full staff detail read and `StaffProductDetailViewModel` does not deliver it.
  - The fix lies in `src/**`, which §4 and §14 put outside this handover.
  - The builder reported it in Amended (6)(a) instead of inventing mock fields, as §20.4 and §20.5 require.
  - The UI says so on each form.
  - ARCHITECTURE and CODE_REVIEW both rated it MEDIUM.
- **It is still a real pre-production hazard.** See C-1.

**Judgement: no-JavaScript and progressive rendering (S-1)**
- **No §18 criterion is unmet.** No §18 row requires the shell header without JavaScript.
- **A17's no-JavaScript part holds for the page body.** A17 relates to it through NFR-PRF-1. E-X3 and TC S-3 show the body rendering without JavaScript: on `/shop/products`, 24 cards with real links, the H1 and the filters. The LCP element is in the body, not the header.
- **The header gap is reproduced in `next dev` only.** There, `/shop` and `/shop/products` stream the header into a hidden boundary that only the inline script reveals. Production `/shop` rendered it inline.
- It remains TEST's MEDIUM (`(shop)/layout.tsx:42-55`), which ARCHITECTURE first raised as a LOW. It contradicts ADR-FE-030's no-JavaScript reasoning and goes to the backlog.

CARRIED CONDITIONS (not gate failures; each must be closed by its owner)
- **C-1 Integrator / owner (HO-SHP-001 follow-up). The staff detail read must return `food`, `images`, `healthFocus` and the SKU attributes before any real catalogue data is entered.**
  - Until then, any re-save of the Details, Food or Images section replaces stored data that the form never loaded. A Details save on a food product, even a name change, clears health focus.
  - **Not tracked anywhere yet.** SHP backlog row 51 covers stock only, and ARCHITECTURE's requested new row has not been written. Record it at merge.
  - The frontend's optional fields (`lib/api/staff-shop.ts:155-157`) pre-fill with no further frontend change once the read returns them.
- **C-2 Owner. Images remain placeholders.**
  - `ImagesEditor` needs a File Storage `fileId`, and staff have no in-scope way to obtain one (no upload, §20.7; ARCHITECTURE LOW). Products therefore show `/images/product-placeholder.svg` with the API's alt text (TC S-6).
  - The handover that brings upload is still to be named.
- **C-3 Integrator. Real-API re-runs (§16, §17 step 7). None is counted as a pass.** These are owed:
  - E-A1 timings, each catalogue response under 500 ms (A4);
  - E-X3 LCP ≤ 2.5 s on throttled 4G with a production build (A17);
  - E-B2's `/book` landing; HO-APT-002 is now on master;
  - the storefront halves of E-C1 and E-F1;
  - E-C3 after HO-ORD-001;
  - E-D2 after real stock;
  - the staff stock copy and expiry notice after HO-IMP-001 (row 51).
- **C-4 HO-ORD-002.**
  - Key the buy action off the purchasability response, not `detail.prescriptionRequired` (SECURITY note).
  - Wire `PrescriptionCatPicker` to record the cat on the order line (A11, second half).

FINDINGS
- No BLOCKER or HIGH findings.
- [MEDIUM] C-1 above. The tracking gap is the finding: the hazard is known and has no backlog row. Record it as a new `docs/backlog/SHP.md` row at merge, scheduled before real catalogue entry.
- Carried, not re-raised. None of these is BLOCKER or HIGH:
  - ARCHITECTURE: 3 MEDIUM and its LOWs;
  - SECURITY: 1 MEDIUM (the `/\` site-relative link in `LimitedMarkup` and `isSafeLink`) and 4 LOW;
  - CODE_REVIEW: 3 open MEDIUM (health focus write-only, SKU state on Back, translations remount) and the LOWs;
  - TEST: 2 MEDIUM (S-1 header without JavaScript; the ProductPurchase → PrescriptionGate `isError` wiring has no test) and 4 LOW.

OUT-OF-SCOPE CHECK (§4, §14, §20)
- §4: none found.
  - No backend change: `git diff --stat cffaa79 8b09024 -- src tests .ai .claude` is empty.
  - No cart, add-to-cart, checkout or payment. The `actions` slot is empty, and the added lines mention "add to cart" only in comments and in tests asserting its absence.
  - No authorisation issuing.
  - No upload: no `type="file"` and no `XMLHttpRequest`.
  - No `next/image`.
  - No search-as-you-type.
  - No new dependency: `web/package.json`, `package-lock.json`, `tsconfig.json`, `next.config.ts` and `eslint.config.mjs` are unchanged.
- §20: no condition was hit and worked around without being reported.
  - The wire differences (§20.4/§20.5) are reported in Amended (6)(a)-(h), with no shim and no invented mock field.
  - The image upload (§20.7) is reported, and no uploader was built.

UNDECLARED DEVIATIONS
- [MEDIUM, carried from ARCHITECTURE] `web/lib/api/errors.ts:41-43,106-111`.
  - The shared fallback in `applyValidationErrors` and `describeApiError` now uses `shopCodeMessage(errorCode)` for every area. That goes beyond §13's "append", and §1 Amended does not record it.
  - CODE_REVIEW found no regression for non-empty messages.
  - It needs an integrator decision at merge, because HO-APT-002 met the same requirement with area-local maps.
- [LOW, carried from ARCHITECTURE] `web/components/staff-shop/VariantsTable.tsx:188` hard-codes "Amount (BDT)". ADR-FE-036 says the currency comes from the API. It is not recorded in Amended.
- No other undeclared deviation was found.
  - The mock stub, the nav registry and `shop-helpers.ts` are declared in Amended (1), (2) and (10).
  - The `home.spec.ts` and sign-in edits are owner-approved (ac3f9b3).
  - The cat-page change is the "Food suggestions" link only (+7 lines, read).
  - The `app/page.tsx` change is the signed-out target only (`/sign-in` → `/shop`, read).
  - `client.ts` is additive, adding `getPublic` only (ARCHITECTURE).

SCOPE CHECK
- files in allowed scope: 147/147. These are the 146 files in `git diff --name-only cffaa79 0a88bf3` plus this record. The 146 are 140 web files, `docs/handover/HO-SHP-002.md`, `docs/test-cases/TC-HO-SHP-002.md` and four prior gate records.
  - 137 of the 140 web files are inside the amended §13. That includes `web/app/(auth)/sign-in/page.tsx` (+3/−1, the `/shop` entry and a comment) and `web/tests/e2e/home.spec.ts`, both under the owner decision of 2026-10-04.
  - Three web files are declared in Amended (1), (2) and (10): `web/mocks/handlers/areas/shop.ts`, `web/nav/areas/shop.ts` and `web/tests/e2e/shop-helpers.ts`. They are Shop-owned and not §14.
  - The handover diff is the Amended row and the two §13 lines the decision calls for (+3/−2).
- files outside allowed scope: none
