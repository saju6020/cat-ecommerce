GATE: ARCHITECTURE
HANDOVER: HO-IMP-002
COMMITS: 4ba7b3cb 352a0e5d 0d68ea64 dca04343 036d6779 5013e291 20d8f466 bf137d9d ade324cf eae4b540 b1c5cabe 5e716752 e4f0abd5 1a1c139d (base master 9d2346f0; 4b2f98b9 and 9c087f0f are the CODE_REVIEW and SECURITY verdict files and were not reviewed)
DATE: 2026-10-08
STATUS: FAIL

Frontend gate. §6 names no `.ai/` document, and `.ai/` governs the backend only (HO-ACC-002 §19:
"`.ai/` backend rules do not apply"). This review is against HO-IMP-002 §8/§9/§10/§13/§14, the
inherited rules (HO-CAT-002 §8.2, HO-ACC-002 §4.3, HO-ADM-002 active branch and server CSV) and the
frontend gate precedent (HO-BIL-002, HO-NTF-002 ARCHITECTURE). Nothing was built or run here. The
real-API E-run is recorded as NOT RUN in TC-HO-IMP-002 and is referred to TEST.

Note: the handover's gate table is §19, not §17.

FINDINGS
- [HIGH] web/components/inventory/ExportLink.tsx:22, web/lib/api/inventory-stock.ts:265, web/lib/api/inventory-reports.ts:51,88, web/lib/hooks/useExportBranchNote.ts:9-13 — the ledger, near-expiry and stock-on-hand CSV exports are plain `<a href download>` links.
  - An anchor cannot send `X-Branch-Id`. `StaffLedgerController.ExportCsv` (:53) and `StaffInventoryReportsController.NearExpiryCsv` / `StockOnHandCsv` (:44, :69) resolve the branch with `TryResolveBranch`, which falls back to the caller's **primary** branch.
  - So staff working at a non-primary branch download the primary branch's ledger, stock and value-at-risk. Their own branch's file cannot be downloaded at all.
  - Worse, the ledger and stock-on-hand links carry filters from the picked branch (`locationId`, `skuId`, `batchId`), and those filters are then applied to the primary branch's data.
  - The note "This file covers your primary branch…" discloses the problem but does not fix it. UI-4 requires CSV for these three reports, and for these users the requirement is not met.
  - This is the same defect HO-BIL-002 ARCHITECTURE rated HIGH, and the same standard applies. BIL-002 fixed it in 6527673b with a shared XHR/blob helper that sends CSRF and `X-Branch-Id`.
  - Fix in the frontend: download through a `lib/api` blob helper that sends `X-Branch-Id`, maps `413` and `403` to their messages, and saves the file with `URL.createObjectURL` plus `download`. Then delete `useExportBranchNote`.
  - Not affected: `getRecallTraceCsvUrl` (inventory-recalls.ts:134). `StaffRecallsController.TraceCsv` is keyed by batch and does not call `TryResolveBranch`, so a plain link is correct there.
- [MEDIUM] web/components/recall/RecallConsole.tsx:46-47,114,136 — TC wire finding #4. The notify count ("{n} customers have not yet been messaged" and the "Notify {n} customers" confirm) is derived in the client: `countTraceCustomers(trace)` minus `notifiedRecipientCount`.
  - The server picks recipients with `IRecallRecipientResolver` and caps each run at `RecallRules.MaxRecipientsPerRun` (RecallCommandHandlers.cs:90-92). The two counts can differ, and the difference lands in the confirm of an irreversible action (UI-3).
  - It decides nothing: the button gate is the server's `status`, and the run's result text comes from the server.
  - The developer reported it under §20.4, as the precedent requires. This is the same class of gap as HO-BIL-002's derived visibility, which was rated MEDIUM.
  - Backlog IMP (backend): add `pendingRecipientCount` to `RecallSummaryViewModel`.
- [MEDIUM] §8.3 "Forms: react-hook-form + zod". Only `SupplierForm` uses react-hook-form and zod. The other ten editors use local `useState` and hand validation (`quantityProblem`, pattern constants):
  - the declared line-based editors: GoodsReceiptForm, PurchaseOrderBuilder, TransferForm, AdjustmentForm, CountSheet, ConsignmentCosts;
  - and, not declared, the non-line forms StorageBreachForm, StorageProfileForm, CountOpenForm and ConsignmentForm.
  - The zod schemas in `lib/validation/inventory.ts:43-83` are used by nothing except SupplierForm.
  - Not HIGH, because the properties §8.3 protects still hold. The state is component-local (no store), values are kept across a `400`, decimals stay strings, and `UnsavedChangesGuard` is wired (U-X1, U-X2). The line editors' per-line server-error distribution and draft autosave are a reasonable reason to deviate.
  - Backlog: record the deviation as an ADR in `web/docs/decisions.md` (owner sign-off), or move the four non-line forms to react-hook-form.
- [MEDIUM] web/components/inventory/CountSheet.tsx:14, StockLineEditor.tsx:6, SkuPicker.tsx:5 — components fetch their own data through `lib/hooks` query hooks, against HO-CAT-002 §8.2 rule 4.
  - Rule 3 holds: every `useQuery` is declared in `lib/hooks/useInventoryQuery.ts`.
  - SkuPicker is §11.1's self-contained type-ahead over `GET …/skus`.
  - Same class and severity as HO-NTF-002 ARCHITECTURE's MEDIUM. Backlog as there: lift the batch queries to the pages, or record the "self-contained widget" exception.
- [MEDIUM] web/app/(staff)/staff/procurement/consignments/[consignmentId]/costs/page.tsx:13,38, web/components/inventory/receiptModel.ts:51, web/components/procurement/SupplierForm.tsx:57 — declared deviation: the base currency falls back to a hard-coded `'BDT'` whenever the landed-cost payload has no `totalCostsBase`, which includes every consignment before its first allocation.
  - That label is what the user reads when entering exchange rates ("Rate to BDT").
  - Master already has the configurable `FALLBACK_CURRENCY_CODE` (lib/shop/currency.ts:7). This is a second, unconfigurable copy in three places.
  - It works for the current single-currency deployment, which is why it is not HIGH.
  - Backlog: use `FALLBACK_CURRENCY_CODE` now. Later, have HO-IMP-001 expose the base currency on the consignment payload.
- [LOW] web/nav/areas/inventory.ts:14-20 — declared deviation: §13 names `(staff)/layout.tsx`, but the nav entries go into the area registry.
  - This is correct under web/README.md "Adding a functional area" and follows the HO-NTF-002, HO-SHP-002 and HO-BIL-002 precedent. The edit is +7/−1 and additive, and `(staff)/layout.tsx` is untouched.
  - The alert badge lives in the section's own layout because the shared sidebar has no badge slot. That is acceptable.
  - HO-IMP-002 §1 Amended still says "(none)". Add the row; §13 names the wrong file, which is a handover defect.
- [LOW] web/app/(staff)/staff/inventory/counts/[countId]/page.tsx:52 — the count sheet, with **Submit** and per-line save, renders on `status === 'Open'`, although `StockCountDetailViewModel.AllowedActions` carries `record-lines` and `submit`.
  - The server refuses where these are not allowed, so this is presentation only. It is still a looser reading of ADR-FE-048 than the variance and commit screens, which do use `allowedActions`.
  - Backlog: gate Submit and line saving on `allowedActions`.
- [LOW] TC wire finding #3 (consignment form): the real `ClearanceDocumentsMissing` always names `ImportPermitNumber`, so the UI marks every empty number of the three. This decides nothing, because the server still refuses.
  - Backlog IMP (backend): name each missing property.

DECLARED DEVIATIONS — RULINGS
- Nav entries in `nav/areas/inventory.ts`: accepted (LOW above, §1 Amended row owed).
- Line editors on local state: accepted as MEDIUM. The undeclared non-line forms are part of the same finding.
- Receipt confirm shows each line's quantity, with no received date: **accepted, no finding.**
  - A grand total of units would need client arithmetic (ADR-FE-041) across lines that may have different units.
  - `PostGoodsReceiptRequest` (StockRequests.cs:27-38) has no received-date field, so omitting it follows §20.4 ("do not invent a field").
  - Referred to ACCEPTANCE (A4 and UI-3 wording).
- Base currency fallback to "BDT": MEDIUM above.
- ScanField ignores bursts that cover only the tail of the field: **accepted, no finding.**
  - A burst that is not the whole field value would look up a partial barcode, so ignoring it is safer.
  - Typing plus **Look up** and the SkuPicker are always available, as ADR-FE-042 requires.
  - The 30 ms threshold is as specified (ScanField.tsx:21).
- CSV exports as plain links: **HIGH above**, the BIL-002 standard.
- Mock seed changed (ade324cf, store.ts:387): **accepted, no finding.** Line 2 of the part-received PO moves from wet food to litter so that the fixture PO no longer covers the reorder shortfall E-E2 needs. §12 still holds (a PO part-received with outstanding quantities), and the change is mock-only, in scope.

REAL-API GAPS (TC-HO-IMP-002 "Wire-contract findings") vs "allowedActions and flags from the server"
- #1 decimal strings on the wire: a contract confirmation for the real run, referred to TEST.
- #2 rows with no stock unit: the unit is joined from the stock summary or SKU lookup. Where it is unavailable, the quantity is left out rather than shown bare.
  - This is presentation, not a stock decision.
  - Backlog IMP (backend): add `stockUnit` to the alert, recall-held, breach-batch, reorder-policy and report rows.
- #3: LOW above.
- #4: MEDIUM above. It is the only gap where the UI derives a figure the server should supply. It still decides no action.
- #5: HIGH above.
- #6 `InsufficientStock` arriving as both `409` and `400`: both are handled, so no finding.
- #7 no landed-cost staleness on list rows: omitted, not derived. Correct under §20.4.
- #8 no storage-profile list: the page starts from an item. Omitted, not invented. Correct.
- None of them breaks ADR-FE-040, ADR-FE-047 or ADR-FE-048:
  - batch actions render only from `allowedActions` (BatchDetail.tsx:45);
  - the PO cancel/close, consignment advance, adjustment approve/reject, count commit and breach actions do too;
  - `isExpired` and `daysToExpiry` are always the server's;
  - nothing in the slice sorts or recomputes a FEFO plan (the only `.sort` calls are the country list and SectionNav);
  - `requiresApproval` comes from the response (AdjustmentForm.tsx:102).

CHECKS (clean)
- **§8.5 layering.**
  - No `fetch(`, `XMLHttpRequest` or `axios` in the slice.
  - No `useQuery` or `useMutation` outside `lib/hooks/`.
  - Nothing outside `mocks/` and `tests/` imports `@/mocks`.
  - Components import only the error classes, error helpers and the CSV URL builders from `lib/api`, and never call an endpoint.
- **§8.3 state.**
  - Every Inventory/Procurement key goes through `useInventoryQuery`: `['inventory', family, …, branchId]`, sent only once the branch is known (`ready`).
  - Mutations invalidate by family plus `alerts`.
  - Only the alert badge polls (useInventory.ts:269, ADR-FE-045).
  - URL-driven list state is in `useInventoryListState`.
  - There is no store or context.
  - The receipt draft is in `sessionStorage` only (receipt-draft.ts:51), as ADR-FE-043 allows.
- **Overlays.**
  - `ReasonDialog`, `DeclareRecallDialog`, `LocationManager` and `ReorderPolicyTable` import `@/components/ui/dialog`, and every other confirm goes through `ReasonDialog`.
  - No hand-rolled `role="dialog"`, `aria-modal`, `fixed inset-0`, `createPortal`, `<dialog>` or `window.confirm`.
- **ADR-FE-039…048.**
  - All ten Decision and Why texts in `web/docs/decisions.md` were extracted by script and compared with the §9 table: 10/10 identical.
  - Recorded once each.
  - `.ai/**` and `.claude/**` are untouched (0 lines in the diff).
- **MSW mocks (HO-ACC-002 §5.1).**
  - All 492 object keys in `mocks/handlers/inventory/*.ts` were cross-checked against the identifiers in `src/**`. The 163 keys not found there are all store-internal: `_branchId`, `_variance`, `_poLines`, `_requesterId`, `_allocationBatchIds`, `_staleOnApprove`, `recallStatus`, fixture ids and helper names. They are stripped with `omit()` or never serialised; response builders are typed against the `lib/api` view-model types.
  - Every exported response type in the twelve API modules was compared field by field with its C# record (Read view models and Application results):
    - no TS-only field, except `RequestedAdjustment.adjustmentId`, which the controller adds itself (StaffAdjustmentsController.cs:52-56);
    - `PurchaseOrderReceiptRef` matches `PurchaseOrderReceiptViewModel`;
    - the only C#-only fields are `currencyCode` on two results (shipped inside the money object) and `FefoPreview.refusalCode` and `refusalMessage` (unused).
- **No new dependency (§10).** `web/package.json`, the lockfile, `tsconfig.json`, `next.config.ts` and `eslint.config.mjs` are unchanged. No scanner, camera, CSV, chart, grid, date or decimal library.
- **Scoped edits.**
  - `lib/api/errors.ts`: append-only (+110/−0).
  - `mocks/handlers/index.ts`: +4/−0, the alias pattern.
  - `client.ts`, `types.ts`, `components/ui/*`, `(staff)/layout.tsx`, `(portal)/**` and `(auth)/**`: untouched.
  - The only `components/common` file is `ScanField.tsx`, which §13 allows.
- **No upload path.** There is no file input and no storage client. ConsignmentDocuments takes metadata only.
- `git merge-tree master HEAD`: clean.

REFERRED
- **To CODE_REVIEW:** component size. GoodsReceiptForm is 386 lines and AdjustmentForm 251 against §8.5's ~150, and several others are over 200. `lib/format/inventory.ts:35` `Number(cost.amount)`, display-only via `Intl` (the `formatMoney` allowance).
- **To TEST:** the real-API E-run (NOT RUN; needs seeded staff credentials).

BACKLOG (MEDIUM/LOW, owner rule 2026-09-27)
- **IMP backend:** `pendingRecipientCount` on the recall summary; `stockUnit` on the alert, recall-held, breach-batch, reorder-policy and report rows; per-field `ClearanceDocumentsMissing`; the base currency on the consignment payload.
- **IMP frontend:**
  - an ADR for local-state editors, or react-hook-form for the four non-line forms;
  - lift the batch queries out of CountSheet and StockLineEditor;
  - `FALLBACK_CURRENCY_CODE` instead of the `'BDT'` literals;
  - gate count Submit and line saves on `allowedActions`.
- **HO-IMP-002 §1 Amended:** record the nav-registry substitution.

SCOPE CHECK
- files in allowed scope: 166/166 reviewed (165 literal; `web/nav/areas/inventory.ts` is the convention-mandated equivalent of §13's `(staff)/layout.tsx` line, accepted pending the §1 Amended row)
- files outside allowed scope: none
