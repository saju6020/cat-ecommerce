GATE: ARCHITECTURE
HANDOVER: HO-PHM-002
COMMITS: 86fe980e 94ee0882 5fac9fa8 b9d7a412 f52236c9 3f644f46 2b0cb44d 9ec5cf7c 7bd1fa59 a13cbba8 fd84e943 00da7afe 2ff7e7d6 2bd470d0 aa78e655 (diff a8761829..aa78e655, branch ho/PHM-002; the later SECURITY/CODE_REVIEW verdict commits were not reviewed)
DATE: 2026-10-11
STATUS: PASS

This is a frontend gate. HO-PHM-002 §6 says `.ai/` governs the backend only, so this review is against:
- HO-PHM-002 §1 (including the six Amended rows), §8, §9, §10, §11, §13, §14 and §20;
- `web/docs/decisions.md`;
- the web conventions and the frontend gate precedent (HO-BIL-002, HO-EMR-002 ARCHITECTURE).

The wire contract was checked against the real backend on master: `src/Api` controllers and request contracts, and `src/Read` view models. Nothing was built or run here. The real-API E-run is NOT RUN (see §1 Amended (tests)) and is referred to TEST.

FINDINGS
- [MEDIUM] web/lib/format/units.ts:96-113 (`DOSE_UNITS_BY_FORM`, used by PrescriptionComposer.tsx:171 and DoseFields)
  - **Rule:** ADR-FE-061 says the dose-unit select is filtered to "units the medicine allows". §20.2 and §20.5 say: ask the server, do not assume.
  - **What the code does:** the client owns a form → dose-unit table. `FormularyMedicineDetailViewModel` carries no allowed-dose-unit list. The server accepts any `DoseUnit` and returns `DoseNotCheckable` where it cannot convert (DoseConverter.cs, PrescriptionSafetyAssessor.cs:77). So the table is a client rule the server does not share.
  - **Impact:** it can only hide a unit, never let through one the server refuses. U-A12 specifies the behaviour, so this is not HIGH.
  - **Not reported:** neither §20.5 nor §1 Amended records it.
  - Same class as HO-EMR-002's consent-category MEDIUM.
  - **Backlog PHM (backend):** add `allowedDoseUnits` to the formulary detail, then drop the table.
- [MEDIUM] web/components/prescriptions/PrescriptionComposer.tsx:148-157 — declared deviation, §1 Amended (contract)(5). A renewal finds its medicine by an exact name + `strengthText` match over the active formulary, because the prescription detail has no `medicineId`.
  - **Why this is not HIGH:**
    - `RenewPrescriptionRequest` sends no `medicineId`; the server renews the source's own medicine.
    - The matched id only drives the client's safety check, routes and units.
    - A wrong match (same name and strength, different form) gives a fingerprint the server's renew check will not reproduce, so the server answers `409 PrescriptionWarningsChanged` with the true assessment (ADR-FE-060).
    - When nothing matches, the screen says so.
  - **Still a problem:** it is a client-side identity heuristic, the shim §20.4 says not to write. The vet can briefly see an assessment for the wrong medicine.
  - **Backlog PHM (backend):** `medicineId` on `PrescriptionSummary` and the detail. Then delete the match.
- [MEDIUM] Cross-area component coupling — declared in §1 Amended (screens)(6) and accepted as reuse, but the shape will spread if copied. PHM-002 is the first area outside Inventory and Billing to import their internals:
  - `components/inventory/{fields,QueryBoundary,ReasonDialog,ExportButton}` — 30, 9, 5 and 1 imports;
  - `components/billing/{ResponsiveTable,Tabs}` — 6 and 3 imports;
  - `lib/hooks/useInventoryQuery`'s `noRetryOnRefusal`, imported by all six new hooks.

  A change in either area now breaks pharmacy screens. These are de facto shared pieces living in area folders. Moving them into `components/common` / `lib/hooks` would edit §14 files, so building it this way was correct for this handover.

  **Backlog (cross-cutting):** promote these pieces to `components/common` (and a shared query helper) under an owner-approved handover.
- [MEDIUM] Backend contract gaps recorded in §1 Amended (contract)(6)-(11). The client works around each one honestly, but each is a server-owned decision made, or approximated, on the client:
  - the formulary list has no `isStocked` filter and no sort, so *Stock* and the column sort apply to the page in view only (UI-4);
  - the owner's prescription and diet lists are unpaged arrays, so *Past* is paged on the client;
  - `DispensingViewModel` has no `isControlled`, so the reversal's witness step is inferred from `label.controlledNotice !== null`. The server still requires and verifies the witness;
  - register corrections link by entry number only;
  - the queue's summary has no `catId`.

  **Backlog PHM (backend):** one row covering `isStocked` + sort on `GET /api/staff/formulary`, paging on `/api/customers/me/{prescriptions,diet-authorisations}`, `isControlled` on `DispensingViewModel`, `correctsEntryId` + corrected-by ids on `RegisterEntryViewModel`, and `catId` on the queue item.
- [LOW] web/docs/decisions.md (ADR-FE-059…068) — the Decision and Why texts are reworded from §9, with build notes folded in. Examples: ADR-FE-063 adds "or a cache retaining a password", and ADR-FE-064 replaces the CSS variables with two constants. They are not recorded verbatim with a separate Status line, as the HO-BIL-002 and HO-EMR-002 records were. No meaning is reversed, and each departure is in §1 Amended. **Backlog:** restore the §9 texts verbatim and move the realisation notes into Status lines.
- [LOW] web/tests/e2e/pharmacy-helpers.ts, web/tests/e2e/pharmacy-routes.ts — outside §13's `web/tests/e2e/pharmacy-*.spec.ts` glob by the letter only. These are non-spec helpers, following the APT, SHP and EMR `*-helpers.ts` precedent. §1 Amended (tests) does not name them. **Backlog:** record them in the Amended row.
- [LOW] tests/components/common/clinical-logic-scan.test.ts:9-10 — the header says the dispense screen's sum lives in `lib/format/pharmacy.ts`. In fact `DispenseScreen.tsx:92-98` re-implements it inline with `reduce` over `toScaled`, alongside the unused-there `sumAmounts`. This is not clinical logic: it is the specified "8 of 10 ml" total (U-C4), done in exact integer arithmetic, and the server re-checks. But the scan's stated boundary is not where the code is. **Backlog:** call `sumAmounts`, or correct the comment.

DECLARED DEVIATIONS: RULINGS
- **Nav entries in `nav/areas/pharmacy.ts` and `adminNavItems.ts`, not in the layouts** (Amended (screens)(5)): **accepted, no finding.**
  - This follows web/README.md "Adding a functional area" and the HO-NTF, SHP, BIL, IMP and EMR-002 precedent.
  - The edits are additive: pharmacy.ts +14/−2, which flips `audience` to `both` with per-item audiences as `billing.ts` does; adminNavItems.ts +6/−0, one entry.
  - The Formulary entry's roles equal `Formulary.View` in ActionRoles.Pharmacy.cs:12.
  - `(staff)/layout.tsx` and `(portal)/layout.tsx` are untouched.
  - The deviation is recorded in §1, so the LOW owed by BIL and EMR is not owed here.
- **Register export through the branch-aware `ExportButton`, not a plain link:** **accepted.** This is the correct answer to the HO-BIL-002 and HO-EMR-002 HIGH: an anchor cannot send `X-Branch-Id`, and the export endpoint resolves the branch.
- **Dispense, reverse and correction as plain functions inside hooks, not TanStack mutations** (ADR-FE-063): **accepted.**
  - They are called only from `lib/hooks/usePharmacy.ts:62,102` and `useControlledDrugs.ts:64`.
  - Components never call `lib/api` functions directly.
- **Remote renewal; open visits from HO-EMR-001's existing branch list** (Amended (renewal), (OQ-5)): **accepted.** These are owner decisions, and no endpoint or mock field was invented.
- **Batch choice as checkbox + quantity per batch:** **accepted.** A split supply cannot be expressed with one radio, and the server's plan still decides what is offered.
- **Role checks where the server sends no action list:** **accepted.** These are diet extend/revoke (§11.5 "for vets"), *Add correction* (§11.6 "vets only"), *New prescription* and the composer pre-check (§8.5). `DietAuthorisationViewModel` and `RegisterEntryViewModel` carry no `allowedActions`. All are presentation, and the server's `RequireAction` refuses regardless.

CHECKS (clean)
- **No client-side clinical logic (§4, §20.2, ADR-FE-060/062).** Read in full: `composerModel.ts`, `lib/format/pharmacy.ts`, `lib/format/units.ts`, and the dispense, composer and safety paths.
  - No mg/kg or mg arithmetic. `formatDoseRange` only joins the server's strings.
  - No allergy or interaction matching. Acknowledgement needs come from `warning.requiresAcknowledgement`; "allergy reason required" is keyed on the server's `AllergyConflict` warning kind.
  - No batch ordering. Batches are preselected from `plan.suggestedLines`.
  - No expiry comparison: `isExpired`, `isDispensable` and `notDispensableReason` come from the server, and so does `canDispense`/`blockingReason`. With `canDispense: false` there is no Dispense control in the DOM.
  - Diet effective status (`Active`/`Expired`/`Revoked`) is the server's.
  - `formatRemaining` renders the server's `remainingDays`.
  - Grep over the slice's components, pages and API modules for `Number(`/`parseFloat`/`new Date(` finds only form-shape validation (mirroring the FluentValidation limits in PrescriptionCommandValidators.cs:23-28), URL page params and request serialisation.
  - The U-X5 repo scan exists and covers 11 slice folders. Its stated boundary is the LOW above.
- **Buttons from server state (§8.5).**
  - Prescription detail: Dispense, Renew, Cancel and DeclineRepeat come only from `allowedActions` (PrescriptionDetail.tsx:49). The names match `PrescriptionActions` (Shared/…/PrescriptionActionRules.cs:9-12).
  - Dispensing detail: Reprint and Reverse come only from `allowedActions` (DispensingDetail.tsx:32), matching DispensingQueryHandlers.cs:166-170.
  - Portal: the repeat button only from `repeat.canRequest`, with `cannotRequestReason` shown verbatim. *Book a consultation* is chosen on the server's `cannotRequestCode`.
- **Mocks match the real DTOs (HO-ACC-002 §5.1).**
  - All object keys in `mocks/handlers/pharmacy/*.ts` were cross-checked against identifiers in `src/**`. The ones not found are fixture ids, handler names, witness test credentials and store-internal fields (`renewedFromId`/`renewedById`/`brokenAtPosition`/`planBatches`/`latestKg`). `views.ts` projects these to the real `renewedFrom`/`renewedBy`/`firstBrokenPosition` shapes, so none reaches a response body.
  - Compared field by field with the TS types (spot-check):
    - `DispensePlanViewModel`/`DispensePlanBatchViewModel`/`SuggestedLineViewModel`;
    - `DispensingViewModel`/`MedicineLabelViewModel`;
    - the queue view models;
    - `RegisterEntryViewModel`/`RegisterEntryDetailViewModel`/`RegisterBalanceViewModel`/`RegisterVerificationViewModel`;
    - `CustomerPrescription(Detail)ViewModel`/`CustomerRepeatViewModel`/`CustomerDietAuthorisationViewModel`;
    - `PrescriptionMedicineViewModel`;
    - the request bodies `DispensePrescriptionRequest`, `ReverseDispensingRequest`, `RecordCorrectionRequest`, `RequestRepeatRequest`;
    - the anonymous `unpricedLines` (StaffPharmacyController.cs:92) and the correction's `{ entryId, entryNumber }` (StaffControlledDrugsController.cs:83).
  - Routes match the controllers (`api/staff/formulary`, `api/admin/formulary`, `api/customers/me`, `api/staff/controlled-drugs`, …).
  - The formulary mock honours only the server's real filters (`search`, `isControlled`, `isActive`), so the mock and real runs behave alike.
  - The mocks register only pharmacy routes; no other area's endpoint is shadowed.
- **§8 layering (`app` → `components` → `lib/hooks` → `lib/api`).**
  - No `fetch(`, `XMLHttpRequest` or `axios` in the slice.
  - No `useQuery`/`useMutation`/`useQueryClient` outside `lib/hooks/`.
  - Nothing outside `mocks/`/`tests/` imports `@/mocks`.
  - Components import from `lib/api` only types, error helpers, `assessmentFromError`, `REGISTER_ENTRY_KINDS` and `registerExportPath`.
  - No `localStorage`/`sessionStorage`/`indexedDB`.
- **Query keys** follow §8.3. Branch-scoped lists (queue, plan, dispensing, register, balances, verification) carry `branchId`.
  - The prescription keys omit it as §8.3 specifies. That is safe because, on those reads, `GetPrescriptionDetailQuery` etc. use the branch only for the local "today" (PrescriptionQueryHandlers.cs:36-101), not to filter rows.
- **Overlays on `components/ui/dialog.tsx`.**
  - `WitnessDialog`, `RecordWeightDialog`, `VisitPickerDialog` and `RepeatRequestDialog` import it.
  - Confirms use `components/common/ConfirmDialog` and Inventory's `ReasonDialog`, both built on it.
  - No hand-rolled `role="dialog"`, `aria-modal`, `fixed inset-0`, `createPortal`, `<dialog>` or `window.confirm`.
- **Staff screens vs the warm customer layer.**
  - No `warm-*`, `data-theme` or `EmptyCatIllustration` in any staff component or page of the slice.
  - The portal pages inherit `data-theme="customer"` from `(portal)/layout.tsx`. Only `my-prescriptions/**` uses the warm empty-state art.
- **Exported components (§8.6) are route-independent.**
  - `PrescriptionComposer`, `DietAuthorisationForm`, `VisitPrescriptionsList`, `WitnessDialog` and `Quantity` use no `next/navigation`, router or params.
  - The composer never navigates; `onIssued` decides.
  - `WitnessDialog`'s props match §8.6 (`onSubmit({ identifier, password })`), plus optional `confirmLabel`/`title`.
  - The README sits next to them.
- **Routing** matches §8.1 route for route:
  - 7 under `cats/[catId]`;
  - 7 under `pharmacy/**`, including the `/label` print route;
  - 3 under `admin/formulary`;
  - 2 under `portal/prescriptions`.
- **Scoped edits to other areas' files, each matching §13:**
  - `cats/[catId]/layout.tsx` +3/−0: two tab entries;
  - `portal/cats/[catId]/page.tsx` +8/−0: one link;
  - `lib/api/errors.ts` +101/−0: append-only;
  - `mocks/handlers/index.ts` +4/−0: the alias pattern;
  - `docs/handover/HO-PHM-002.md` +6/−1: the handover's own Amended rows (HO-ADM/APT-002 precedent).

  Untouched: `client.ts`, `types.ts`, `components/ui/*`, existing `components/common/*`, existing `lib/hooks/*`, both shell layouts, HO-EMR-002's workspace.
- **No new dependency (§10).** `web/package.json`, `package-lock.json`, `tsconfig.json`, `next.config.ts` and `eslint.config.mjs` are unchanged. `public/mockServiceWorker.js` was restored by aa78e655 and is not in the net diff.
- **No forbidden path.** `src/**`, `tests/**`, `.ai/**` and `.claude/**` have 0 lines in the diff.
- `git merge-tree --write-tree master aa78e655`: clean (master is a8761829).

REFERRED
- **To CODE_REVIEW** (already in PHM backlog 35-41): component size against §11's ~150 lines (FormularyMedicineForm 618, PrescriptionComposer 419, PrescriptionDetail 336, DispenseScreen 319, SafetyPanel 316); the portal's branch zone taken from `branches[0]`.
- **To TEST:** the real-API E-run (NOT RUN). In particular, the safety-assessment body on `400`/`409`, and the renewal's name + strength match against real formulary data.

BACKLOG (MEDIUM/LOW, owner rule 2026-09-27): PHM rows 43-49, prefixed "PHM-002 ARCHITECTURE:".

SCOPE CHECK
- files in allowed scope: 117/117.
  - 112 are inside the literal §13 globs (`lib/hooks/**` new hooks included).
  - The other 5 are accepted:
    - `docs/handover/HO-PHM-002.md` — its own §1 Amended rows (precedent);
    - `web/nav/areas/pharmacy.ts` and `web/components/admin/adminNavItems.ts` — the convention-mandated equivalents of §13's layout nav lines, recorded in §1 Amended (screens)(5);
    - `web/tests/e2e/pharmacy-helpers.ts` and `pharmacy-routes.ts` — non-spec e2e helpers beside the `pharmacy-*.spec.ts` glob (precedent; Amended row owed, LOW).
- files outside allowed scope: none (no §14 path touched)
