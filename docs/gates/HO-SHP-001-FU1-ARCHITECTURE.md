GATE: ARCHITECTURE
HANDOVER: HO-SHP-001-FU1
COMMITS: af7304b, 67a8158, 748eb1b (range 578dc79...HEAD on ho/SHP-001-FU1)
DATE: 2026-10-06
STATUS: PASS

Reviewed against `.ai/read-side-pattern.md`, `.ai/cqrs.md`, `.ai/architecture.md`, `.ai/decisions/adr.md`
(ADR-095), `web/docs/decisions.md` (ADR-FE-032/034/035), HO-SHP-001 §12.4/§12.6/§14 and HO-SHP-002 §1 Amended (6)(a).
Build and tests not run here; FU1-T1/T1b/T2 and the web component tests are for the TEST gate.

What was checked and holds:
- Read side only. No change under `src/Domain`, `src/Application`, `src/Infrastructure`, `src/Api`,
  no migration, no `*.csproj`, no `web/package.json` (A3; no new dependency).
- `GetStaffProductDetailQueryHandler` adds no new query and no new rehydration. The one root load
  (`AsNoTracking`) is the pre-existing ADR-095 exception (shared `ProductPublicationRules.Evaluate`);
  `FoodDetails` (OwnsOne), `Images` (OwnsMany) and `HealthFocus` (value-converted column) are already
  on that loaded root. No write on the read path. Returns `QueryResponse<T>`.
- Additive: new trailing optional record parameters (`Food`, `Images`, `HealthFocus`, `StaffSkuViewModel.Label`);
  no field renamed, removed or retyped; the handler is the only constructor caller (A2).
- Reuse: `food` reuses the public `FoodDetailViewModel` / `GuaranteedAnalysisViewModel` and is built the
  same way as `GetProductDetailQueryHandler` (enumerations by `DisplayName`). A new `StaffProductImageViewModel`
  instead of the public `ProductImageRef` is justified: the staff row needs `FileId` and `SortOrder` to
  round-trip `PUT …/images`; it is a superset of §12.4's image object (`url|null, altText, isPrimary, skuId?`).
- Round trip: `healthFocus` emits `HealthFocus.DisplayName`, which is what `EnumerationParsing.TryParse`
  (used by `UpdateProductDetailsCommandHandler`/validator) matches, so a re-save is accepted.
- Web: types follow the API (ADR-FE-032), forms default from loaded values, mock mirrors the new shape,
  ADR-FE-035's upload note retained; no new route, store or dependency.

FINDINGS
- [MEDIUM] docs/handover/HO-SHP-001-FU1.md:60 (and web/lib/api/staff-shop.ts:83) — `skus[].label` is described as "product name plus size, flavour, colour, pack quantity". `Sku.LabelFor` (src/Domain/CatEcommerce.Domain/Entities/Sku.cs:32-39) returns the axes joined with " · ", with "Pack of N", and the product name only when no axis is set. The contract text is wrong; the code is right.
- [MEDIUM] web/mocks/handlers/shop/staff.ts:138-141 — the mock's `skuLabelFor` prefixes the product name and renders pack as "N pack", so the mock does not mirror the real label (HO-SHP-002 §1 Amended (6): "mocks mirror the REAL shapes"). The component test fixtures (web/tests/components/staff-shop/staff-shop.test.tsx:38, :328) bake in the same wrong format. The shape is right; the content diverges.
- [MEDIUM] docs/handover/HO-SHP-001-FU1.md — no governing-architecture section and no numbered allowed/forbidden scope sections; §2 lists four backend files and §6 says "web/ only". `docs/backlog/SHP.md` (row S1 marked resolved, 748eb1b) is not named in either. The change is the slice's stated bookkeeping and touches no forbidden path, so it is recorded here and not failed, following the HO-SHP-001 / HO-APT-001 precedent; the handover should list it.
- [LOW] src/Read/CatEcommerce.Read/ViewModels/StaffProductDetailViewModel.cs:16 — `Label` is `string?` with default `null` while the contract (§3) and the web type say `string`; the handler always sets it. Same for `Images`/`HealthFocus` as nullable lists that are always non-null. Consistent with the record's existing optional-tail style; cosmetic.
- [LOW] web/components/staff-shop/VariantsTable.tsx:118 — the row header already shows `skuLabel(sku)` (code · size · flavour · colour); the real `sku.label` under it repeats the same axes for any SKU that has them. UX redundancy, for CODE_REVIEW/design.
- [LOW] web/docs/decisions.md:1061-1062 — ADR-FE-034 status still says the merged API has "no staff read of food, images or health focus"; now stale after this slice.

SCOPE CHECK
- files in allowed scope: 12/13
- files outside allowed scope: docs/backlog/SHP.md (not listed in the slice document; not forbidden; MEDIUM above)
