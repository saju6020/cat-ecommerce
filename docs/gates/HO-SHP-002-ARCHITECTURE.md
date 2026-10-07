GATE: ARCHITECTURE
HANDOVER: HO-SHP-002
COMMITS: (second pass) ac3f9b3; (first pass) 6540601 3eb7984 7a60f37 f08090b 3f218da 143bf0d 69e201b d3ade14 0b71a75 f8841c8 87d152c 80c57fb dc8da0c ea686a1 (range master cffaa79...ea686a1, branch ho/SHP-002, 141 files)
DATE: 2026-10-04
STATUS: PASS

Final status after the second pass (below): PASS. The first-pass record follows unchanged; it was FAIL.

First pass. Reviewed with `git diff master...HEAD` against:
- HO-SHP-002 §1 Amended (1)–(10) of 2026-10-04, §4, §6, §8–§14 and §20;
- `web/docs/decisions.md`, `docs/handover/README.md` (the ADR-FE allocation table), HO-ACC-002 §4.3 forbidden shapes;
- the merged HO-SHP-001 backend on master `cffaa79`, for the contract claims in Amended (6);
- earlier §14 rulings: HO-ACC-002, HO-FIL-002 and HO-APT-002 ARCHITECTURE (`ho/APT-002:docs/gates/HO-APT-002-ARCHITECTURE.md`).

This is a web-only handover with no `src/**` change. §6 says, correctly, that the `.ai/` backend docs do not govern this diff, so the backend checklist (layers, aggregates, dispatch, persistence, host) has nothing to apply to. This gate built and ran nothing. Runtime items are marked for TEST.

Owner rule (2026-09-27): only BLOCKER and HIGH fail a gate. MEDIUM and LOW go to `docs/backlog/SHP.md`, which this gate does not edit.

**Verdict.** The gate fails on **one BLOCKER, and it needs a decision, not a code change**. Two §14 files were edited, and the only authority for either is the builder's own Amended row. This repo's gates do not accept that (HO-APT-002, HO-ACC-002, HO-FIL-002). There are no HIGH findings. Apart from the BLOCKER, the slice complies with its architecture.

## What was verified

**Rendering and data (ADR-FE-029, ADR-FE-030)**
- `(shop)` is a new public group with its own shell and no guard.
- Catalogue, product, countries and home are async Server Components. They read through the `…Server` functions in `lib/api/shop-catalogue.ts`, all of which go through `getPublic`.
- `generateMetadata` sets a canonical URL from the slug.
- A draft or unknown slug gives `notFound()`.
- Personal parts are client islands using hooks from `lib/hooks/`.

**Forbidden shapes (HO-ACC-002 §4.3, HO-SHP-002 §4)**
- No `useQuery`/`useMutation` inside any component or page. Every hook lives in `lib/hooks/`.
- No `fetch(` outside `lib/api/client.ts`.
- No `dangerouslySetInnerHTML`, `innerHTML`, `createContext`, global store, `localStorage` or `sessionStorage`.
- No `next/image`. `ProductImage.tsx` uses a plain `<img>`.
- No shipped code imports `@/mocks`. The only importer is the gated dynamic import in `instrumentation.ts`.

**No client-side catalogue logic (ADR-FE-031, ADR-FE-032, §20.2)**
- Results, counts, sort and pages come from `GET /api/shop/products`. `lib/shop/catalogueParams.ts` is the one URL parser.
- Each `.filter`/`.sort` in the diff is one of:
  - presentation of server data, such as featured origins on the home page, active SKUs in the staff picker, or related-link order in the editor;
  - the brand option-list narrowing that §11.2 permits.
- Availability, purchasability, eligible cats and the checklist are read only from API fields (`StatusText`, `VariantSelector`, `ProductPurchase`, `PrescriptionGate`, `ProductEditorHeader`).
- A `null`/absent stock reads *No stock recorded* (`VariantsTable.tsx:25-28`).

**Owner-confirmed HO-SHP-001 decisions**
- **Fail-closed availability.** Every SKU can be `OutOfStock`. The UI states it in text, offers the stock alert, and implies no purchase. The `actions` slot is empty, so there is no cart (§4).
- **Prescription diets and bundles that contain them are not purchasable.**
  - The gate renders from `purchasability`.
  - A bundle that contains a prescription diet shows the server's availability.
  - An alert on such a bundle is refused with `PurchaseNotPermitted`, which is mapped in `errors.ts`.
  - Nothing in the UI suggests either can be bought.
- **No image URL yet.** When `url` is null, the UI shows `/images/product-placeholder.svg` with the API's alt text.
- **Text versus markup.** `LimitedMarkup` is used only for:
  - product description and feeding guide (`ProductDetailsTabs.tsx:41,100`);
  - origin sourcing notes (`:128`, `countries/[countryCode]/page.tsx:58`);
  - the staff previews of those same three fields;
  - banners, which §11.1 specifies and which are HO-ADM-002 content, not product fields.

  Review text, names, brand, ingredient list and storage instructions render as text.
- **No upload path.** `ImagesEditor` takes a typed `fileId`. No uploader, `XMLHttpRequest` or `type="file"` was added (ADR-FE-035, §20.7).

**Dependencies.** `web/package.json`, `tsconfig.json`, `next.config.ts` and `eslint.config.mjs` are unchanged. `msw/node` is part of the existing `msw` devDependency (§10).

**`client.ts` (§13: "server-only getPublic only")**
- The diff is purely additive: one exported function appended after `apiFetch`, and nothing else changed.
- It sends no cookies and no identity headers.

**ADR numbering.** ADR-FE-029…038 match `docs/handover/README.md`'s allocation, and the decisions diff is purely additive (0 lines removed). There is no clash with:
- master: 001–020 and 149–158;
- ho/APT-002: 024–028;
- ho/NTF-002: 159–163.

NTF-002 using 159…, against an allocation of 019–023, is outside this gate's scope.

**Handover document.** `docs/handover/HO-SHP-002.md` changed only in its §1 Amended row. No section body was edited. That is the established convention (see HO-FIL-002 ARCHITECTURE), not a §14 finding.

## Judgement points from the integrator

**1. `globalThis.__mswFetch` in `instrumentation.ts` and `getPublic`: acceptable, inert in production by construction.**
- `register()` sets it only when all three hold (`instrumentation.ts:15-18`):
  - `NEXT_RUNTIME === 'nodejs'`;
  - `NODE_ENV === 'development'`;
  - `NEXT_PUBLIC_USE_MOCKS === 'true'`.
- `next build` and `next start` run with `NODE_ENV=production`, so the branch cannot execute. Nothing else in the repository writes the global.
- Next inlines `process.env.NODE_ENV` and `NEXT_PUBLIC_*` at build time, so the branch, and with it the `msw` import, should be eliminated from the production instrumentation bundle. **TEST:** confirm that `.next/server/instrumentation*.js` from `next build` contains no `msw`. This matters because `msw` is a devDependency.
- **Architecturally** it is a test seam, not a second HTTP client. The only call site is still inside `client.ts`, and the seam is recorded in ADR-FE-030 and Amended (5). See the LOW below for defence in depth.

**2. `msw/node` server-side mocks: compliant.**
- Server and browser use the same `handlers`. `mocks/server.ts` is reused unchanged.
- `mocks/handlers/index.ts` is untouched.
- The shop mock modules import only `msw` and other mock modules, never `@/lib/api/client`, so they avoid the import-order hazard HO-APT-002 hit.
- The two in-memory databases (Amended (8)) are a known MSW limit, and the affected E-C1/E-F1 halves are recorded as real-API cases. That is for TEST.

**3. `getPublic` as a second fetch path: the rule still holds.**
- "No `fetch` outside `client.ts`" is about call sites, and `getPublic` lives in `client.ts`. ADR-FE-030 and §13 sanction exactly this function.
- It throws in the browser (`client.ts:309-311`).
- It shares `ApiError`, `ApiValidationError` and `ApiNotFoundError` with `apiFetch`, so callers do not get a parallel error taxonomy.

**4. No `<Suspense>` around the catalogue: acceptable, and consistent with ADR-FE-030.**
- `/shop/products`, origin pages and product pages await `searchParams`, so they render dynamically. `useSearchParams` in their islands does not bail out to client rendering there.
- **TEST:** `next build` must succeed with no "useSearchParams should be wrapped in a suspense boundary" error. Also check the LOW on the layout's `Suspense` around `ShopHeader` below.

**5. Write-only staff forms over a read that lacks the fields: MEDIUM for this slice. The backend contract gap is not yet in the backlog.**
- **The read side.** `StaffProductDetailViewModel` (master, `src/Read/CatEcommerce.Read/ViewModels/StaffProductDetailViewModel.cs`) returns:
  - no food details;
  - no images;
  - no health focus;
  - no SKU label;
  - no per-SKU stock.
- **The write side replaces, it does not merge.** `UpdateProductDetailsRequest.HealthFocus` defaults to `[]` and is applied as a full replacement (`UpdateProductDetailsCommandHandler.cs:44-48`, `ProductAggregate.cs:127`). The images and food writes are also full replacements.
- **The consequences:**
  - **Every Details save, even a name change, clears the product's health focus** unless staff re-tick it (`ProductDetailsForm.tsx:110`, `:260-262`). Health focus drives US-SHP-11's food-suggestion reasons.
  - A Food save nulls every labelling field the form was not refilled with.
  - An Images save replaces the stored set.
- **It is MEDIUM, not HIGH, for this frontend:**
  - It follows §20.4/§20.5 (report, don't shim, invent no mock field), and it is disclosed in Amended (6)(a).
  - It warns in the UI (`FoodDetailsForm.tsx:132`, `ImagesEditor.tsx:72`, `ProductDetailsForm.tsx:260`).
  - No in-scope frontend design avoids it, because the backend's Details command always replaces health focus.
  - It violates no frontend architecture rule.
- **The real defect is a backend contract gap.** HO-SHP-001 §12.4 promises a "full detail" read (`HO-SHP-001.md:1110`), but its writes replace fields its read does not return.
- **Backlog row 51 covers only `stock`.** New SHP backlog row, recommended **before any real catalogue data is entered**: the staff detail read must return `food` (all labelling fields and the analysis), `images` (`fileId`, `skuId`, `altText`, `isPrimary`, `sortOrder`), `healthFocus`, and the SKU attributes behind the label. After that, the optional fields in `lib/api/staff-shop.ts:155-157` pre-fill with no frontend change.
- **ACCEPTANCE:** judge A1 and A3 ("food labelling is edited **and shown**") against this gap. The SKU label gap affects display only (`VariantsTable.tsx:22` composes it from code and attributes). It causes no data loss.

**6. Reuse of HO-ADM-002's `ScheduleChangeDialog`: compliant, LOW copy defect.**
- It is imported read-only (`VariantsTable.tsx:8,185`). The file is unchanged, so this is not a §14 edit.
- Its fixed sentence (`components/admin/ScheduleChangeDialog.tsx:81`, "Bookings and invoices made before …") is wrong for shop prices.
- `valueLabel="Amount (BDT)"` (`VariantsTable.tsx:188`) hard-codes the currency, whereas ADR-FE-036 says the currency comes from the API.

**7. Merge overlap with ho/APT-002: Amended (4)'s claim is inaccurate.**
- `git merge-tree HEAD ho/APT-002` merges `app/(auth)/sign-in/page.tsx` cleanly. ho/APT-002 does not touch that file, because `/book` is already on master.
- The real textual conflicts, with both ho/APT-002 and ho/NTF-002, are `web/lib/api/errors.ts` and `web/docs/decisions.md`. Both sides append, so the integrator can resolve them mechanically.
- `client.ts` auto-merges with ho/APT-002.
- There is also a semantic overlap in `errors.ts` (MEDIUM below). SHP changes the shared fallback for every area, while APT adds its own per-area fallback. After the merge both mechanisms exist.

FINDINGS

- [BLOCKER] **§14 forbidden-scope files edited, authorised only by the builder's own Amended row.**
  - **The rule.** §14 forbids "every other HO-CAT-002, HO-ACC-002 … file beyond the lines in §13".
  - **The precedent.** HO-APT-002 ARCHITECTURE (`CustomerSessionGuard.tsx`), HO-ACC-002 ARCHITECTURE (`package.json`, `eslint.config.mjs`) and HO-FIL-002 ARCHITECTURE all held that a §14 edit needs an owner or integrator decision recorded in §1 Amended. The builder's own Amended row is not enough. This gate does not authorise either edit itself.
  - **(a) `web/tests/e2e/home.spec.ts:7-8, 26-30`.**
    - This is an HO-CAT-002 foundation spec (scaffold `379a3aa`), and its second describe block is HO-ACC-002 SECURITY gate item 7. §13 does not list it.
    - The edit is the unavoidable consequence of the §13-authorised `app/page.tsx` redirect (OQ-1). It does not weaken the security property, because a signed-out visitor still never reaches `/staff/sign-in`.
    - Its only authority is Amended (3).
  - **(b) `web/app/(auth)/sign-in/page.tsx:44-45, 50`** (`'/shop'` added to the `sanitizeNextPath` allow-list).
    - This is an HO-ACC-002 file. §13 puts the change at `web/lib/routing/**`, which does not exist. The substance is pre-approved by §8.1 ("HO-ACC-002's open-redirect allow-list gains `/shop`") and §13, so only the path is wrong.
    - Ratifying it is a formality, but it is still a §14 path.
    - HO-APT-002's equivalent `/book` addition was recorded in **HO-ACC-002's** §1 Amended ("2026-09-22 — by HO-APT-002"). This one is not.
  - **To close (owner or integrator decision). No code change is needed.** Either:
    - record both edits as an owner or integrator decision in HO-SHP-002 §1 Amended, and preferably mirror (b) in HO-ACC-002 §1 Amended as the `/book` precedent did, then correct §13's nonexistent `web/lib/routing/**`. This gate would then pass on a delta check. Or:
    - revert them. That leaves OQ-1 failing HO-ACC-002's own e2e spec, and ADR-FE-038's return to the product unbuilt.
  - **SECURITY:** confirm the guard still holds. `'/shop'` is a bare prefix, so `/shopX` matches, as `/book` already does. Every match is a same-origin path, but SECURITY should judge it.

- [MEDIUM] **Point 5, the write-only staff forms.** The analysis is above.
  - **Files:** `web/components/staff-shop/ProductDetailsForm.tsx:110, 260-262`, `FoodDetailsForm.tsx:132`, `ImagesEditor.tsx:72`, `web/lib/api/staff-shop.ts:155-157`.
  - **The risk.** Saving silently replaces health focus, food labelling and images that the form never loaded. It is disclosed in the UI and in Amended (6)(a).
  - **The backend contract gap.** This is not covered by SHP backlog row 51, which is stock only. It needs a new SHP row. The details are under point 5 above.

- [MEDIUM] **`web/lib/api/errors.ts:41-43, 106-111` modified beyond §13's "append".**
  - **What changed.** The shared HO-CAT-002 §11.8 `applyValidationErrors` and `describeApiError` now fall back to `shopCodeMessage(errorCode)` for **every area**.
  - **What that does to other areas.** An empty `errorMessage` from any area's command now renders "Something went wrong. Please try again." instead of an empty string. An area's code that matches a shop key would get shop wording.
  - **What it couples.** A shared module now knows one area's codes.
  - **The stale header.** The module header (`:8-9`: "`errorCode` … is NEVER read") is now untrue. NFR-USA-4 still holds, because the code is never rendered.
  - **The two shapes.** HO-APT-002 met the same §11-style requirement with area-local maps. After the merge, the repository will have two shapes for one rule.
  - **Disclosure.** None in Amended.
  - **The requirement is legitimate.** §11.7 asks for "server's message preferred, mapped text fallback".
  - **Suggested change.** Use an area-scoped fallback passed through `options`, or record the global fallback as a shared decision for the integrator. Hand this to CODE_REVIEW.

- [MEDIUM] **The client predicts the server's next version.**
  - **Where.** `web/app/(staff)/staff/shop/products/[productId]/food/page.tsx:56` and `web/app/(staff)/staff/shop/products/[productId]/page.tsx:52`.
  - **What happens.** One form save sends two commands in sequence: food then prescription, or details then slug. The second carries `expectedVersion: p.version + 1`.
  - **Why it is fragile.**
    - It assumes each handler calls `UpdateVersion()` exactly once. That is true today (`ProductAggregate.cs`), but it is a server-internal detail.
    - The save is not atomic. If the second call fails, the first half has been saved.
    - The first mutation's `onSuccess` invalidates `['staff','product',id]`, and the page keys the form on `p.version`. A refetch that lands mid-submit can therefore remount the form and drop the entered values, against NFR-USA-5.
  - **Why the client must sequence at all.** The backend deliberately exposes the prescription flag and the slug as separate commands, so the client has no single call to make.
  - **What would remove the guess.** The write's own response could return the new version.
  - **Hand to:** CODE_REVIEW and TEST (U-D6, partial-failure case).

- [LOW] **`web/lib/api/client.ts:316`: `getPublic` reads `globalThis.__mswFetch` unconditionally.**
  - It is inert today, because only the triple-gated `register()` writes it.
  - **Suggested hardening:** read it only when `process.env.NODE_ENV === 'development'`, matching `providers.tsx:112`. The branch then compiles away in production and cannot be activated later by a stray write.

- [LOW] **`web/lib/api/client.ts:312`: `API_ORIGIN` is a new server-side environment variable.**
  - It is not documented in `web/.env.local.example` or the README, and both are outside §13.
  - Without it, server rendering falls back to `NEXT_PUBLIC_API_BASE_URL`. That works, but it takes the public route.
  - **Integrator or deployment note:** document it.

- [LOW] **`web/app/(shop)/layout.tsx:42-55`: `ShopHeader` (a `useSearchParams` island) sits inside `<Suspense fallback={null}>`.**
  - A storefront route that Next prerenders statically would ship without the header in its no-JavaScript HTML. Candidates are `/shop` and `/shop/countries`, which use `revalidate` and read no `searchParams`.
  - That conflicts with ADR-FE-030's no-JavaScript reasoning.
  - **TEST:** check the `next build` route table and the JavaScript-disabled HTML of `/shop` and `/shop/countries`.

- [LOW] **The `ScheduleChangeDialog` copy, from point 6.**
  - `components/admin/ScheduleChangeDialog.tsx:81` has service-worded copy.
  - `components/staff-shop/VariantsTable.tsx:188` hard-codes "Amount (BDT)".
  - **ADM backlog:** make the description a prop. A shared-copy follow-up is already flagged in Amended (7).

- [LOW] **`docs/handover/HO-SHP-002.md` §1 Amended (4) is inaccurate.**
  - It says HO-APT-002 edits the same allow-list line. It does not (point 7).
  - The real merge conflicts are `errors.ts` and `decisions.md`, with both ho/APT-002 and ho/NTF-002.

- [LOW] **`docs/handover/HO-SHP-002.md` has three body errors:**
  - §13 names a nonexistent `web/lib/routing/**`;
  - §8.2 names `NEXT_PUBLIC_API_MOCKING`, but the repository flag is `NEXT_PUBLIC_USE_MOCKS`;
  - §4 and §11.6 describe a "storage key + metadata" image form, but the API takes a `fileId`.

  All three are recorded in Amended (4), (5) and (6)(b). Correct the bodies when the BLOCKER is ratified.

- [LOW] **`web/components/staff-shop/ImagesEditor.tsx:70` and ADR-FE-035.**
  - The note says "enter the stored file's key (its file id)", but the field is a File Storage GUID.
  - Staff have no in-scope way to obtain a file id, because there is no upload path (owner-confirmed, ADR-FE-035). The editor is effectively unusable until an upload handover lands.
  - **Backlog:** name the handover that brings upload, and the obligation from HO-FIL-002 LOW ("first consumer deletes the harness").

- [LOW] **Two out-of-§13 files, recorded in Amended and acceptable:**
  - **`web/tests/e2e/shop-helpers.ts`.** It is a non-spec helper beside the `shop-*.spec.ts` glob (Amended (10)). This follows the HO-APT-002 `appointments-helpers.ts` precedent.
  - **`web/mocks/handlers/areas/shop.ts` and `web/nav/areas/shop.ts`.**
    - Both are pre-wired scaffold files that declare themselves "Owned entirely by the Shop area". The entries were added there instead of to the `index.ts` and layout files that §13 named, which were left untouched. Amended (1) and (2) record this.
    - The nav has exactly three portal entries and one staff entry, as §13 allows. This matches the HO-APT-002 nav-registry ruling. It is not a scope violation.

- [LOW] **Several components exceed §11's "~150 lines":**
  - `ProductDetailsForm` 298;
  - `FoodDetailsForm` 291;
  - `OriginsTable` 255;
  - `ReviewModerationQueue` 244;
  - `VariantsTable` 226;
  - `NewProductForm` 223;
  - `ProductDetailsTabs` 221;
  - `StaffProductTable` 214;
  - `CatalogueFilters` 211;
  - `ReviewForm` 203.

  For CODE_REVIEW (HO-SHP-002 §19 gate 3).

SCOPE CHECK
- files in allowed scope: 135/141 (by §13's literal globs)
- files outside allowed scope:
  - `web/tests/e2e/home.spec.ts`: **§14**, forbidden (HO-CAT-002/HO-ACC-002 file). BLOCKER; needs ratification.
  - `web/app/(auth)/sign-in/page.tsx`: **§14 by path**, forbidden (HO-ACC-002 file). The content is the allow-list change §8.1 and §13 authorise, but §13's path is wrong. BLOCKER; needs ratification.
  - `web/mocks/handlers/areas/shop.ts`: not §14. A Shop-owned scaffold stub; Amended (1). Acceptable.
  - `web/nav/areas/shop.ts`: not §14. The Shop-owned nav registry; Amended (2). Acceptable.
  - `web/tests/e2e/shop-helpers.ts`: not §14. A test helper; Amended (10). LOW.
  - `docs/handover/HO-SHP-002.md`: §14 `docs/**` by letter, but only the §1 Amended row changed. That is the established convention, so it is not a finding.

---

## Second pass: delta `d096f3a..ac3f9b3` (2026-10-04)

GATE: ARCHITECTURE
HANDOVER: HO-SHP-002
COMMITS: ac3f9b3
DATE: 2026-10-04
STATUS: PASS

**What the delta changes.** `git diff d096f3a..ac3f9b3` touches one file, `docs/handover/HO-SHP-002.md`, with 3 lines added and 2 removed. No source, test or configuration file changed, so every code finding from the first pass stands exactly as written.

**The first-pass BLOCKER (§14 edits authorised only by the builder): closed.**
- §1 Amended now ends with "**2026-10-04 — owner decision, closing the ARCHITECTURE BLOCKER (d096f3a)**". It approves both edits by name:
  - `web/tests/e2e/home.spec.ts`;
  - the `/shop` entry in `web/app/(auth)/sign-in/page.tsx`.
- §13 replaces the nonexistent `web/lib/routing/**` with `web/app/(auth)/sign-in/page.tsx` and adds `web/tests/e2e/home.spec.ts`. Each line is limited to the specific change and cites the decision.
- This is the remedy the first pass named, option one. It matches the HO-FIL-002 and HO-APT-002 precedents: a §14 edit is in scope once an owner or integrator decision in §1 Amended authorises it.
- **Provenance.** This gate checked that the decision is recorded, not that the owner made it. The integrator relayed the owner's approval, and ac3f9b3 (author shahjahan) records it.
- The code at both sites is unchanged since the first pass. Each edit is exactly what the new §13 lines allow:
  - `sign-in/page.tsx`: one array entry plus a comment;
  - `home.spec.ts`: the signed-out expectation, plus one `waitForURL` in the smoke test.

  The smoke-test line goes slightly beyond §13's wording ("the signed-out expectation … only"). It is that same expectation repeated, so it is not a finding.

**The first-pass LOW on Amended (4)'s merge-conflict claim: closed.** The appended sentence records that HO-APT-002 does not edit the sign-in page. The original wording of item (4) is left in place, and the later sentence corrects it.

**The first-pass LOW on handover body errors: partly closed.**
- §13's path is fixed.
- §8.2 still names `NEXT_PUBLIC_API_MOCKING`, and §4 and §11.6 still say "storage key". Both are recorded in Amended (5) and (6)(b). This stays LOW, for the backlog.

**Scope check after ac3f9b3.** Measured against the amended §13, 137 of the 141 implementation files are in scope. The remaining four:
- `mocks/handlers/areas/shop.ts`, `nav/areas/shop.ts` and `tests/e2e/shop-helpers.ts` are Shop-owned, are recorded in Amended, and are not §14.
- `docs/handover/HO-SHP-002.md` changed in two places:
  - its §1 Amended row, which follows the convention;
  - its §13 lines, which carry the recorded owner decision.

No file remains in §14 without a recorded decision.

FINDINGS (second pass)
- New findings: none. There are still no BLOCKER and no HIGH findings.
- [LOW] **The decision is not mirrored in HO-ACC-002's own §1 Amended.** The `/book` precedent was recorded there ("2026-09-22 — by HO-APT-002"). Readers of HO-ACC-002 will not see that two of its files changed. This does not block; it is a backlog item for the integrator.
- **Carried unchanged, for `docs/backlog/SHP.md`:**
  - the three first-pass MEDIUMs:
    - the write-only staff forms and the backend gap in the staff detail read, which needs a new SHP row because row 51 covers stock only;
    - the shared fallback added to `errors.ts`;
    - the predicted `expectedVersion`;
  - the remaining first-pass LOWs:
    - the ungated `__mswFetch` read;
    - `API_ORIGIN`;
    - `Suspense` around `ShopHeader`;
    - the `ScheduleChangeDialog` copy and the hard-coded BDT label;
    - the `ImagesEditor` copy;
    - the out-of-§13 Shop-owned files, which are acceptable;
    - component size;
    - the handover-body residue above.

**TEST items still open:**
- `msw` absent from the production instrumentation bundle;
- `next build` succeeds, with no `useSearchParams` Suspense error;
- the no-JavaScript header on statically prerendered `/shop` and `/shop/countries`;
- the U-D6 and partial-failure cases for the two-command saves.

**SECURITY item still open:** the bare `/shop` prefix on the allow-list.

SCOPE CHECK (second pass)
- files in allowed scope: 141/141. That is 137 under the amended §13, plus the three Shop-owned files recorded in Amended, plus the handover's own Amended row and §13 lines.
- files outside allowed scope: none in §14 without a recorded owner decision.
