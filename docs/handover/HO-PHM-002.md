# HO-PHM-002 — Pharmacy and Prescriptions (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-PHM-002** |
| Title | Pharmacy and Prescriptions — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §6 — **US-PHM-01 … US-PHM-08** |
| SRS | FR-PHM-1 … FR-PHM-9, FR-POR-3; **UI-1, UI-3, UI-4, UI-6, UI-7, UI-8**; HW-2 (barcode as keyboard input); NFR-USA-4, NFR-USA-5, NFR-USA-6; **AC-4**; BR-3, BR-6, BR-7 |
| Pairs with | [`HO-PHM-001`](HO-PHM-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-PHM-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, `[catId]` layout with `CatProfileHeader`, cage-card print precedent), [`HO-ACC-002`](HO-ACC-002.md) (sessions, `client.ts`, error mapping, `PermissionDeniedState`, `UnsavedChangesGuard`, idle lock), [`HO-ADM-002`](HO-ADM-002.md) (active branch, `lib/format`, admin shell), [`HO-NTF-002`](HO-NTF-002.md) (inbox — links land on §11.8's pages), [`HO-APT-002`](HO-APT-002.md) (`useServerNow`, the `/book` deep link) |
| **Embedded by — not yet written** | **`HO-EMR-002`** — the consultation screen embeds §11.2's `PrescriptionComposer` and §11.5's `DietAuthorisationForm` (§8.6 is its contract). **`HO-SRG-002`** reuses §11.4's `WitnessDialog`. **`HO-SHP-002` / `HO-ORD-002`** render the storefront gate (§4) |
| Delivery order | Section 9 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended (build) | **2026-10-11 — implementation, branch `ho/PHM-002`, built on master `a8761829` where HO-PHM-001's backend is merged. The backend as built wins over this handover's text (§20.4); each difference is recorded here.** **Owner decisions 2026-10-10 applied:** the controlled-drug witness must be a Vet or Nurse and not the dispenser; Reception may dispense; an Administrator may reverse a dispensing but cannot correct the register (Vet only); remote renewal (no open visit) is allowed. |
| Amended (contract) | **Wire shapes that differ from HO-PHM-001 §12.4 as this handover assumed it** (read from `StaffPrescriptionsController` and `PharmacyPrescriptionViewModels.cs`): (1) a **read** returns `frequency` as the label (*"twice daily"*) but a **request** needs the enumeration name (`TwiceDaily`) — the client keeps one table (`lib/format/pharmacy.ts`). (2) A dose's unit on a read is the `DoseUnit` name (`Ml`, `Mg`), a quantity's is the `DispensingUnit` name (`Millilitre`, `Tablet`), a weight's is `Kg`; `lib/format/units.ts` labels all of them. (3) The safety assessment is `weight.latest = { weight: {amount,unit}, recordedOn, ageDays }`, `doseCheck.doseMgPerKg` a string and `expectedRange = { minMgPerKg, maxMgPerKg, minMg, maxMg }` with the mg figures as quantities. (4) The `400`/`409` bodies of issue and renew carry the fresh `assessment` next to `errors`; `client.ts` already keeps the whole parsed body on `error.response`, so `assessmentFromError` reads it back with no client change. (5) The prescription list and detail carry **no `medicineId`** (only name, strength text and form): a renewal finds the medicine by searching the active formulary for an exact name + strength match and says so on the screen when it cannot — **a backend gap to close (`medicineId` on `PrescriptionSummary`)**. (6) Register entries link to each other by **number only** (`correctsEntryNumber`, `correctedByEntryNumbers`), so the list shows *Corrects #1200* / *Corrected by #1203* as text and the chain is read on the original's page — **a backend gap (`correctsEntryId`, ids on the corrected-by list)**. (7) `DispensingViewModel` has no `isControlled`; the reversal's witness step is chosen from `label.controlledNotice !== null`. (8) Customer prescription and diet lists are **plain arrays, not paged**; the cat filter and *Past*'s 20-a-page paging are applied on the client (OQ-4); `bookLink` is an absolute URL, so the portal builds `/book?catId=` itself. (9) The formulary list has no `isStocked` filter and no sort and returns `strengthText`, not a strength object: *Stock* filters, and the column sort orders, the page in view. (10) The pharmacy queue's `PrescriptionSummary` has no `catId` and the dispense plan's `cat` carries only `{ id, name, isHighRisk, handlingNote }`; allergies on the dispense screen come from the existing `VisitCatHeader` (HO-EMR-002's compact header). (11) The register export accepts only `medicineId`, `from` and `to`. |
| Amended (renewal) | §11.3 and §8.1 assumed a renewal is written from an open visit. By owner decision a renewal may be **remote**: `/staff/cats/[catId]/prescriptions/[id]/renew` takes `?visitId=` only as an option, the composer says *"Remote renewal"* and sends no visit, and `isRemoteRenewal` is shown on the detail. Every check still runs again. |
| Amended (OQ-5) | A cat's open visits: HO-EMR-001 has no per-cat read, but it does list the branch's open visits (`GET /api/staff/visits?status=Open`, `useOpenVisits`). The visit picker (`VisitPickerDialog`) filters that list to the cat and still accepts a pasted visit reference. No endpoint was invented (§20.5). |
| Amended (screens) | (1) **Export CSV** is not a plain link: an anchor cannot send `X-Branch-Id` and would export the wrong branch's register, so the register uses the existing branch-aware `ExportButton` (HO-IMP-002). (2) **Batch choice** is a checkbox plus a quantity per batch in a labelled group, not a radio group: a supply can be split across batches, which one radio cannot express. (3) `Quantity` carries no `aria-label` (a label on a bare `<span>` has no role to name, which axe flags); the visible "0.30 ml" is already read with its unit. (4) The label's print stylesheet is generated from two constants and embedded with the label (the receipt's pattern), not a new `web/styles/print-label.css`. (5) The three nav entries are in `nav/areas/pharmacy.ts` (staff and portal) and `components/admin/adminNavItems.ts` (Formulary), where the shell reads them, not in the layout files (the deviation HO-IMP-002 and HO-BIL-002 recorded); the cat layout gains its two tabs and the portal cat page its one link as §13 says. (6) Shared pieces reused rather than rebuilt: `ResponsiveTable`, `Tabs` (billing), `ReasonDialog`/`ActionDialog`/field components/`QueryBoundary`/`ExportButton` (inventory), `VisitCatHeader` (EMR), `WeightEntryForm` (cat). (7) The corrected-cat picker on a register correction filters the first hundred cats on file (there is no cat search endpoint). (8) A reversal's confirmation says the stock returns to **quarantine** (HO-PHM-001 §1 Amended (2)). (9) Dispense, reverse and register-correction calls are plain functions, not TanStack mutations, so a witness's password never sits in the mutation cache (ADR-FE-063). |
| Amended (tests) | `docs/test-cases/TC-HO-PHM-002.md` holds the cases and results. The MSW run is complete; the **real-API re-run (§5.1) was run at the TEST gate (2026-10-11, `de95d7b1`): 20 cases against a fresh database, results in the sheet's "Real API" section** (the worker-dependent parts are not covered; backlog PHM 57). Non-spec e2e helpers `pharmacy-helpers.ts` and `pharmacy-routes.ts` are accepted by precedent (PHM 48). The mock knows only an administrator and a nurse, so a Vet or Receptionist is the nurse account relabelled (EMR's helper) and the witness step uses two sign-ins only the pharmacy mock knows; the administrator's own sign-in stands in for *a witness who is neither vet nor nurse* (E-B2's "receptionist witness"). Flows run on `chromium`; the 360 px project is covered by the a11y/responsive spec. |
| Amended (tests, seeder) | **2026-10-12 — owner decision, verbatim: "Keep it, record it".** `tests/CatEcommerce.IntegrationTests/PharmacyE2ESeed.cs` stays in the backend test project although §14 reserves `tests/**` for HO-PHM-001: the real-API run needs staff in four roles, a customer, cats with open visits, locations, stocked SKUs (one only-expired) and formulary data, which cannot be produced from `web/**`. Guards: it is skipped unless `E2E_SEED_FILE` and `E2E_STAFF_PASSWORD` are set (reported as Skipped, not passed); it derives from `PharmacyApiFactory`, which refuses `catecommerce_dev` and `catecommerce_test`, so it needs `CATECOMMERCE_TEST_DB` pointing at a per-branch database; it seeds through real endpoints plus the repository staff-seeding path every area's tests use, and sets two batch expiry dates directly (the one direct write the Pharmacy tests already make). The seed is single-use per database. Owner also decided 2026-10-12 to merge with backlog PHM 50 (allergy order) open as MEDIUM. |

---

## 2. Objective

Build the screens that let a vet **prescribe safely**, let pharmacy staff **dispense the right batch
and print the label**, let a vet and an inspector **read the controlled-drug register**, and let an
owner **see what their cat is taking and ask for a repeat**.

- **Vets** compose a prescription beside the cat's clinical header. As they type, the server checks
  the dose against the cat's latest weight, the allergies and the active medications; warnings appear
  in plain language with the expected range in mg/kg **and** mg; acknowledging one is a deliberate act
  that the server binds to exactly those warnings (HO-PHM-001 ADR-139). They also authorise
  therapeutic diets and renew or cancel prescriptions.
- **Pharmacy staff** (reception, nurses, vets) work a dispensary queue, scan a prescription number and
  a batch barcode, take the server's FEFO suggestion or give a reason, collect a witness's own sign-in
  for a controlled drug, and print a label from the frozen snapshot.
- **Vets, nurses, inventory staff and administrators** read the controlled-drug register with running
  balances, seal status and correction chains; vets add corrections as new entries.
- **Administrators** maintain the formulary.
- **Owners** see each cat's active prescriptions with the remaining days and the issuing vet, request a
  repeat where allowed — and are told plainly why not when it is not — and see the foods a vet has
  authorised.

Three things make this harder than it looks:

1. **Units are never implied.** Every dose, quantity, weight and range on every screen carries its unit,
   as text, next to the number (UI-8). A bare `0.3` on a dosing screen is a defect (ADR-FE-061).
2. **The server is the only clinical authority.** The client never computes mg/kg, never matches an
   allergy, never picks a batch, never decides expiry. It renders the assessment and the plan the API
   returns and sends back the fingerprint it was shown (ADR-FE-060).
3. **Nothing here can edit history.** There is no edit control on a register entry, a dispensing, a
   label or an issued prescription anywhere in the UI. Corrections, reversals and renewals are separate,
   named actions with their own confirmation (ADR-FE-065).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack (Next.js App Router, React, TypeScript, Tailwind, shadcn/ui, TanStack Query,
react-hook-form + zod, MSW, Vitest, Playwright, axe); HO-ACC-002's session model, `client.ts`, route
groups, error mapping, `PermissionDeniedState`, `UnsavedChangesGuard` and idle lock (which preserves a
half-written prescription through re-authentication); HO-ADM-002's active branch, `lib/format` and
admin shell; HO-CAT-002's `CatProfileHeader` (UI-7) rendered from the `[catId]` layout; HO-APT-002's
`useServerNow`.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-PHM-01, 02, 03 | **Prescription composer** with the live safety panel; the cat's prescriptions tab; prescription detail with cancel, renew and decline-repeat | `/staff/cats/[catId]/prescriptions`, `/staff/cats/[catId]/prescriptions/new?visitId=`, `/staff/cats/[catId]/prescriptions/[prescriptionId]` |
| US-PHM-04 | **Formulary** list, create, edit, controlled toggle, activate/deactivate | `/staff/admin/formulary`, `/staff/admin/formulary/new`, `/staff/admin/formulary/[medicineId]` |
| US-PHM-05, 04 AC2, 06 AC1 | **Dispensary**: queue (awaiting first supply, repeat requests), prescription lookup by number/scan; **dispense screen** with batch plan, FEFO reason, witness step; **label** print view; reversal | `/staff/pharmacy`, `/staff/pharmacy/dispense/[prescriptionId]`, `/staff/pharmacy/dispensings/[dispensingId]`, `/staff/pharmacy/dispensings/[dispensingId]/label` |
| FR-PHM-7 (register) | **Controlled-drug register**: entries with running balance and seal status, balances with mismatches, chain verification, export; entry detail with correction chain; correction form | `/staff/pharmacy/controlled-drugs`, `/staff/pharmacy/controlled-drugs/[entryId]`, `/staff/pharmacy/controlled-drugs/[entryId]/correct` |
| US-PHM-06, 07 | **My prescriptions** (active / past, per cat) and detail with **Request a repeat** or the reason it is not possible | `/portal/prescriptions`, `/portal/prescriptions/[prescriptionId]` |
| US-PHM-08 | **Diet authorisation** form (from a visit) and the cat's authorisations tab with extend/revoke; the owner's **Authorised foods** tab | `/staff/cats/[catId]/diet-authorisations`, `/staff/cats/[catId]/diet-authorisations/new?visitId=`, `/portal/prescriptions?tab=diets` |
| US-NTF-03 (refills) | No new screen — the reminder links to `/portal/prescriptions/[id]?action=repeat`, which opens the repeat dialog | — |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-PHM-001 owns `src/**` |
| The consultation screen itself | **HO-EMR-002** — it embeds this slice's composer and diet form (§8.6); until it exists the vet reaches them from the cat's tabs with a `visitId` |
| The storefront's "visible but not purchasable", cat picker at add-to-cart and checkout re-check (US-SHP-10, AC-7) | **HO-SHP-002 / HO-ORD-002** — they call the gate through their own endpoints |
| Surgery anaesthesia-log drug entry | **HO-SRG-002** — it reuses §11.4's `WitnessDialog` (§8.6) |
| Printing to a label printer by driver or ESC/POS | The browser's print dialog prints the label (ADR-FE-064); a raw-printer integration is not in scope |
| Any payment or price-to-pay step | No gateway in v1; prices on dispensing go to Billing (HO-BIL) and are not shown to the owner here |
| A charting, barcode-scanner, PDF, date-picker or print library | §10 |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged, plus: **no client-side dose, allergy, interaction,
FEFO or expiry logic** (§20.2).

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002, HO-ADM-002, HO-NTF-002, HO-APT-002 built** | ❌ at baseline — §20.1 |
| HO-PHM-001 API | ❌ — build against MSW generated from §12.4 there, then re-run every E-case against the real API phase by phase (HO-PHM-001 §17) |
| HO-EMR-002 (for real visit ids) | ❌ **not written** — E-cases needing an open visit create one through HO-EMR-001's API in the real run; MSW supplies fixtures |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-PHM-001`](HO-PHM-001.md) | **§12.4 endpoints and shapes** (every quantity is `{ amount, unit }`); §10.4 codes and their messages; §8.4 the prescription states and "active"; §8.5 what the assessment contains and which warnings need acknowledgement; §8.7 what the dispense plan means and the label fields; §8.8 repeat rules; §8.9 the register, corrections and the witness rules; §8.10 the diet gate; §12.3 who may do what |
| [`HO-CAT-002`](HO-CAT-002.md) | §8.3 the `[catId]` layout (tabs are added there, one entry each); §11.3 `CatProfileHeader`; the `CageCard` `@media print` precedent |
| [`HO-ACC-002`](HO-ACC-002.md) | §8.4 session and CSRF; §8.6 accessibility rules; §11.2 client; §11.14 error mapping; ADR-FE-010 idle lock |
| [`HO-ADM-002`](HO-ADM-002.md) | Active branch; `lib/format`; admin navigation |
| [`HO-APT-002`](HO-APT-002.md) | `useServerNow` (dates "today" on the server's clock); the `/book?catId=` deep link used by "book a consultation"; ADR-FE-027's polling rule |
| [`docs/SRS.md`](../SRS.md) §3.1, §3.2, §6.4 | UI-1/3/4/6/7/8; HW-2; NFR-USA-4/5/6 |
| `.claude/agents/frontend-developer.md` | Component size, tokens, flag-don't-introduce libraries |

**The `.ai/` documents describe the .NET backend only** and do not govern this diff — same position
as HO-CAT-002 §6.

---

## 7. Build rules

| Req | Rule | Where |
|---|---|---|
| **UI-1** | 360 px floor. The composer is single-column below 1024 px with the safety panel **above** the submit button, never in a side column that scrolls away; the register table becomes cards below 768 px; the label print view is independent of screen width | §11 |
| **UI-3** | Confirm, stating what is affected: cancel a prescription (*"Cancel RX-2026-000123 for Mitu? Any open repeat request will be declined."*), revoke a diet authorisation, reverse a dispensing (*"Return 10 ml of batch L4410 to stock and undo supply 1 of RX-…?"*), clear a medicine's controlled flag, deactivate a medicine, submit a register correction (*"This adds entry #… correcting #…; the original stays as it is."*) | Each screen |
| **UI-4** | Formulary list, register and queue paginate, filter and sort; the register exports CSV (server-side) | §11.4, §11.6, §11.7 |
| **UI-6** | WCAG 2.1 AA. Warnings are text with an icon **and** a severity word, never colour alone; the safety panel is an `aria-live="polite"` region announcing **changes** only (*"1 new warning: dose above range"*), not every re-check; the witness dialog is a modal with trapped focus; batch choices are radio buttons in a labelled group | §11 |
| **UI-7** | The cat header appears on every staff cat route (inherited from the `[catId]` layout) and in compact form on the dispense screen and each queue row | HO-CAT-002 |
| **UI-8** | **Units always visible.** Dose, quantity, weight, strength and range are rendered through `<Quantity>` (§11.1) — number and unit as one text run (*"0.3 ml"*, *"4.10 kg"*, *"0.05–0.10 mg/kg (0.21–0.41 mg)"*). Inputs have the unit as an adjacent visible label or select, never only in a placeholder | Every screen |
| **HW-2** | The prescription-number field and the batch field accept keyboard-wedge scans: an `Enter` after a scan submits the lookup / selects the matching batch | §11.4 |
| **NFR-USA-4** | Server messages shown as sent (they are written for the user in HO-PHM-001); mapped fallbacks for codes; no `errorCode`, id or stack in the DOM | §11.9 |
| **NFR-USA-5** | Composer, correction and formulary forms keep values across errors, re-checks and the idle lock; `UnsavedChangesGuard` on each | §11 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (staff)/staff/
    cats/[catId]/
      layout.tsx                                   ⚠ two tab entries: Prescriptions, Diet (HO-CAT-002's layout)
      prescriptions/page.tsx                       cat's prescriptions                     US-PHM-01, 07
      prescriptions/new/page.tsx                   composer (?visitId required)            US-PHM-01, 02, 03
      prescriptions/[prescriptionId]/page.tsx      detail, cancel, renew, decline repeat   US-PHM-01, 06
      prescriptions/[prescriptionId]/renew/page.tsx composer prefilled for renewal         US-PHM-06 AC2
      diet-authorisations/page.tsx                 cat's authorisations, extend, revoke    US-PHM-08
      diet-authorisations/new/page.tsx             authorise (?visitId required)           US-PHM-08
    pharmacy/
      page.tsx                                     dispensary queue + lookup               US-PHM-05, 06
      dispense/[prescriptionId]/page.tsx           dispense screen                         US-PHM-05, 04 AC2
      dispensings/[dispensingId]/page.tsx          dispensing detail, reprint, reverse     US-PHM-05
      dispensings/[dispensingId]/label/page.tsx    print view                              US-PHM-05
      controlled-drugs/page.tsx                    register, balances, verification        FR-PHM-7
      controlled-drugs/[entryId]/page.tsx          entry + correction chain                FR-PHM-7
      controlled-drugs/[entryId]/correct/page.tsx  correction form                         FR-PHM-7
    admin/formulary/
      page.tsx · new/page.tsx · [medicineId]/page.tsx                                      US-PHM-04
  (staff)/layout.tsx                               ⚠ nav entries: Pharmacy, Controlled drugs; admin: Formulary
  (portal)/
    portal/prescriptions/page.tsx                  active / past / authorised foods        US-PHM-07, 08
    portal/prescriptions/[prescriptionId]/page.tsx detail + repeat request                 US-PHM-06, 07
    portal/cats/[catId]/page.tsx                   ⚠ gains an "Active prescriptions" summary link (HO-CAT-002)
  (portal)/layout.tsx                              ⚠ nav entry: Prescriptions
```

Staff routes are under the `[catId]` layout **so the clinical header — allergies included — is always
on screen while prescribing**. Navigation entries are rendered only for roles the session holds
(presentation only — the server enforces; HO-ACC-002 ADR-FE-009).

### 8.2 API modules

`lib/api/formulary.ts` (list, detail, create, update, controlled, active, **SKU search**),
`lib/api/prescriptions.ts` (safety check, issue, cat list, detail, cancel, renew, decline repeat, visit
list), `lib/api/pharmacy.ts` (queue, lookup, dispense plan, dispense, dispensing detail, label print,
reverse), `lib/api/controlled-drugs.ts` (register, entry, correction, balances, verification, export
URL), `lib/api/diet-authorisations.ts` (products, authorise, cat list, extend, revoke),
`lib/api/my-prescriptions.ts` (list, detail, repeat request, withdraw, authorised foods).

### 8.3 State

| Kind | Where |
|---|---|
| Composer form | react-hook-form + zod (shape only — ranges are the server's) |
| Safety assessment | `['prescriptions','safety', catId, debouncedFormHash]` — a **query** (`POST …/safety-check`), debounced 400 ms after the last change to medicine, dose, unit, route or frequency; `staleTime: 0`; the latest fingerprint kept with it |
| Acknowledgements | Component state of the composer, **keyed by fingerprint** — a new fingerprint clears them (ADR-FE-060) |
| Cat prescriptions, detail | `['cat', catId, 'prescriptions', scope]`, `['prescription', id]` — nested under HO-CAT-002's `['cat', catId]` so a cat invalidation refreshes them |
| Dispense plan | `['pharmacy','plan', prescriptionId]`, refetched on entry and after any `400`/`409` |
| Queue | `['pharmacy','queue', branchId]`, `refetchInterval: 30_000`, never resetting an open dialog |
| Witness credentials | **Local state of `WitnessDialog` only**; never in a query key, the URL, storage or a log; cleared on submit, cancel, unmount and on any response |
| Register | `['cd','register', filters]` from URL search params; `['cd','balances']`, `['cd','verification']` |
| Portal | `['me','prescriptions', scope, catId]`, `['me','prescription', id]`, `['me','diet-authorisations']` |

### 8.4 Formatting — `lib/format` additions

- `formatQuantity({ amount, unit })` → `"0.3 ml"`, `"2 tablets"`, `"4.10 kg"` — unit labels from one
  table (`lib/format/units.ts`) with singular/plural; the amount is shown as the server sent it
  (string), trailing zeros trimmed only where the server did (no client rounding of doses).
- `formatDoseRange(range)` → `"0.05–0.10 mg/kg (0.21–0.41 mg for 4.10 kg)"`.
- `formatRemaining(days)` → `"5 days left"`, `"Last day today"`, `"Course finished"`.
- Dates are `yyyy-MM-dd` strings from the API rendered with `Intl` in the branch zone; "today" hints
  use `useServerNow()` (HO-APT-002 ADR-FE-025).

### 8.5 Access and actions

Action buttons on prescription detail come **only** from the API's `allowedActions`; the portal's
repeat button only from `repeat.canRequest` with `cannotRequestReason` rendered otherwise. A `403` on
any call renders `PermissionDeniedState`; a staff member without `Prescriptions.Issue` who opens the
composer URL sees it before typing, from a session-roles check, **and** the server refuses regardless.

### 8.6 Components exported for other slices — the contract

| Component | Props | Used by |
|---|---|---|
| `PrescriptionComposer` | `catId`, `visitId`, `onIssued(prescriptionId)`, `renewFrom?` | This slice's route; **HO-EMR-002**'s consultation screen (inside its own layout, beside its draft note — the composer never navigates on its own) |
| `DietAuthorisationForm` | `catId`, `visitId`, `onAuthorised(id)` | Same |
| `VisitPrescriptionsList` | `visitId` | HO-EMR-002 discharge summary preview |
| `WitnessDialog` | `open`, `onSubmit({ identifier, password })`, `onCancel`, `witnessRoles`, `busy`, `error?` | This slice; **HO-SRG-002**'s anaesthesia log |
| `Quantity` | `value: { amount, unit }`, `as?` | Everywhere |

These live in `web/components/prescriptions/**`, `web/components/pharmacy/**` and
`web/components/common/Quantity.tsx`, have no route dependency, and are documented in a short README
next to them.

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing the series. **This handover owns ADR-FE-059 …
ADR-FE-068** (reserved by the coordinator).

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-059** | Prescribing lives **under the cat** (`/staff/cats/[catId]/prescriptions/**`) so the clinical header is always visible, and the composer is an **embeddable component** HO-EMR-002 places in the consultation screen | UI-7 while prescribing; the consultation screen does not exist yet and must not need a second composer |
| **ADR-FE-060** | **The safety panel renders the server's assessment only.** A debounced check runs on every clinically relevant change; acknowledgements are stored **against the fingerprint** and cleared when it changes; submit sends that fingerprint; `409 PrescriptionWarningsChanged` replaces the panel with the new assessment and says *"Something changed — please review the warnings again."* | The client must never decide safety, and a tick against yesterday's warnings is not consent |
| **ADR-FE-061** | **Units are part of the value.** `<Quantity>` is the only way a dose, quantity, weight, strength or range is rendered; dose inputs pair a number field with a **unit select** filtered to units the medicine allows; no bare number reaches a clinical screen (enforced by U-X1) | UI-8 |
| **ADR-FE-062** | **The dispense screen follows the server's plan**: suggested lines preselected; expired and non-dispensable batches listed **disabled with the reason as text**; choosing a non-FEFO batch reveals a required reason field; *only expired stock* shows a blocking notice with no override control; a scanned batch barcode selects the matching batch or says it is not in the plan | BR-3 and BR-4 as the user sees them; the client has no expiry logic to get wrong |
| **ADR-FE-063** | **Witness step is a modal** asking the witness to type **their own** mobile/email and password on the same device; fields `autocomplete="off"` (so the dispenser's saved password is never filled), cleared on every outcome, never retained; failures show the server's one generic message | HO-PHM-001 ADR-146; a password manager filling the dispenser's credentials would defeat the witness |
| **ADR-FE-064** | **Labels print from the frozen snapshot** through a dedicated print route with `@media print` CSS sized by `--label-width`/`--label-height` (default 62 × 100 mm, OQ-1); the **Print** action records the print (`POST …/label-prints`) **once per print action**, not per render | HO-CAT-002's cage-card precedent; a reprint must be the same label and must be counted |
| **ADR-FE-065** | **Append-only UI.** Register entries, dispensings and issued prescriptions have **no edit control**; corrections, reversals and renewals are separate routes/dialogs that create something new and say so; the correction chain is shown under the original, which stays visible | C-2, D-5 — the UI must not suggest history can change |
| **ADR-FE-066** | **Portal repeat requests** render only from `canRequest`; the refusal text is the server's, with its **Book a consultation** link to HO-APT-002's `/book?catId=`; the refill reminder's `?action=repeat` opens the dialog directly | US-PHM-06 AC2 "told why" |
| **ADR-FE-067** | **Formulary editor** edits the whole medicine in one form with `expectedVersion`; the controlled flag and activation are **separate confirmed actions**, not fields of the form; interactions are a repeatable row group with a medicine picker **or** a term | Mirrors HO-PHM-001 ADR-138's separate command; a controlled-flag change must be deliberate |
| **ADR-FE-068** | The allergy-matching limitation is **stated on the safety panel**: *"Allergy checks compare recorded allergies with this medicine's ingredients and classes. A misspelt allergy may not be matched."* | HO-PHM-001 ADR-140 — a check that can miss must say so where it is relied on |

| # | Question | Default |
|---|---|---|
| OQ-1 | Label stock size (printer and media not chosen) | 62 × 100 mm portrait; CSS variables so a different stock is a one-line change |
| OQ-2 | Should the composer offer the formulary's default dose for the cat's weight? | No — the vet enters the dose; the range is shown |
| OQ-3 | Show dispensing prices to staff on the dispense screen? | No — Billing's concern; only an "unpriced" notice when the response lists unpriced lines |
| OQ-4 | How far back does the portal *Past* tab go? | All, paginated 20 per page |
| OQ-5 | How does a vet reach the composer before HO-EMR-002 exists, given it needs an open `visitId`? | From the cat's Prescriptions tab, which asks for the visit; a real "cat's open visits" read is HO-EMR-001's (HO-PHM-001 OQ-15) — until it exists the visit id is pasted or arrives in the deep link (§20.5) |

---

## 10. Dependencies

**No new package.** Barcode scans are keyboard input (HW-2); printing is the browser's; charts are not
needed. A barcode, PDF, print, charting, date-picker or dosing library is a §20 stop.

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error / permission-denied states.

### 11.1 Common — `components/common/Quantity.tsx`, `lib/format/units.ts`

`<Quantity value={{ amount, unit }} />` renders one text run with a non-breaking space; `aria-label`
spells the unit (*"0.3 millilitres"*); it carries `data-clinical-value` so U-X1 can find every
clinical number. `QuantityInput` = number field + adjacent unit `<select>` with a shared `<label>`; the
unit is required (no default for a dose unit — the vet chooses).

### 11.2 Prescription composer — US-PHM-01, 02, 03 · `/staff/cats/[catId]/prescriptions/new?visitId=`

| Part | Component | Behaviour |
|---|---|---|
| Visit | `VisitContextBar` | Shows the visit from the `visitId`; missing `visitId` → *"Prescriptions are issued from an open visit. Open the cat's visit first."* and no form. `400 VisitNotOpen` → *"This visit has been finalised — prescriptions can no longer be added to it."* |
| Medicine | `FormularyPicker` | Search active formulary medicines (name, code); each option shows name, **strength with unit**, form, a *Controlled* badge (text) and *Not stocked* when `isStocked` is false (prescribable, but the vet is told it cannot be dispensed here) |
| Dose | `DoseFields` | `QuantityInput` for dose (units from the medicine's form: mg/mcg/ml/tablet/…); route select limited to `allowedRoutes`; frequency select with labels (*twice daily*); duration (days); quantity per supply as `QuantityInput` fixed to the medicine's dispensing unit; instructions (textarea, 1000, plain text — *"This text is printed on the label"*) |
| Repeats | `RepeatFields` | *Repeatable* switch → repeats (1–12) and **valid until** date (required); off → optional earlier "dispense by" date; helper text shows the default window |
| ⭐ Safety | `SafetyPanel` | Renders the current assessment: **Weight** (*"Latest weight 4.10 kg, 3 Sep 2026"* or the refusal with **Record weight** opening HO-CAT-002's weight form in a dialog, then re-checking); **Dose check** (*"0.075 mg/kg — within 0.05–0.10 mg/kg (0.21–0.41 mg)"* or *"Above range"* with both figures); **Warnings** list — allergy conflicts first with a **Blocking** label, then interactions by severity, then dose; **Information** (contraindications text, "no allergies recorded", unchecked profile medications); the ADR-FE-068 sentence. Checking state is shown as *"Checking…"* without clearing the last result |
| ⭐ Acknowledge | `AcknowledgementBlock` | Appears only when a warning `requiresAcknowledgement`: one checkbox per warning (*"I have reviewed: Dose above range"*); allergy conflicts additionally require a **reason** textarea (500); dose warnings an optional note. Unticked or missing reason → submit shows the field errors; a new fingerprint clears every tick and announces *"Warnings changed — review again"* |
| Submit | — | **Issue prescription** → `POST`; `201` → `onIssued` (route: to the detail page with a success toast showing the number). `400 PrescriptionWarningsNotAcknowledged` / `409 PrescriptionWarningsChanged` → panel replaced with the returned assessment; `400 RecentWeightRequired` → weight section focused |

The composer never shows its own dose maths; if the check call fails, the panel says *"The safety
check could not run. You cannot issue until it does."* and submit is refused client-side with that
reason (the server would refuse anyway).

### 11.3 Cat prescriptions and detail — `/staff/cats/[catId]/prescriptions/**`

| Component | Behaviour |
|---|---|
| `CatPrescriptionsTable` | Tabs *Active* / *All*; columns: number, medicine (strength, *Controlled*), dose (`Quantity`), route, frequency, remaining (`formatRemaining`), supplies *1 of 3*, valid until, status as text, vet, a warnings-acknowledged icon with text; **New prescription** (only with `Prescriptions.Issue`) asks for the visit (OQ-5) |
| `PrescriptionDetail` | Everything in the detail payload: instructions, **safety record** (*"Issued with 4.10 kg (3 Sep); dose 0.15 mg/kg above 0.05–0.10; acknowledged by Dr Rahman 22 Sep 10:14"*; allergy reason quoted), warnings as they were shown, dispensings (date, by, lots with expiry, status; link to each), repeat requests (with **Decline** for vets — reason required), renewal links; actions from `allowedActions`: **Dispense** (to the dispense screen), **Renew** (composer prefilled via `renewFrom`, with the note *"All checks run again on today's weight and records"*), **Cancel** (reason, UI-3) |

### 11.4 Dispensary — US-PHM-05 · `/staff/pharmacy/**`

| Component | Behaviour |
|---|---|
| `DispensaryQueue` | Two sections for the active branch: **Awaiting first supply** (issued prescriptions: number, compact cat header with high-risk flag, customer, medicine, issued) and **Repeat requests** (cat, customer, medicine, requested, the owner's note as text); each row → the dispense screen. Polls every 30 s (HO-APT-002 ADR-FE-027's rule). Header: *"Updated 10:42"* |
| `PrescriptionLookup` | One field *"Prescription number"* with scan support: `Enter` → `GET …/lookup` → dispense screen; not found → *"No prescription RX-… was found."* |
| ⭐ `DispenseScreen` | Compact cat header; prescription summary (medicine, dose, instructions, supply *2 of 3*, valid until, *Repeat request from the owner* when applicable); `blockingReason` shown as a blocking notice when `canDispense` is false (validity ended, no supplies, cancelled). **Batches**: a radio/quantity table from the plan — lot, expiry, available (`Quantity`), status; suggested lines preselected; expired rows disabled with *"Expired 12 Mar 2026 — cannot be dispensed"*; non-dispensable rows disabled with Inventory's reason; splitting across batches by editing quantities (total must equal the prescription quantity — shown as *"8 of 10 ml selected"*). **Only expired stock** → a blocking notice with the server's text and **no** dispense button (US-PHM-05 AC2). A **Scan batch** field matches the plan's `barcode`/`lotNumber`. Choosing away from the suggestion reveals *"Reason for not using the earliest-expiring batch"* (required). **Dispense** → for a controlled medicine opens `WitnessDialog` first → `POST` |
| ⭐ `WitnessDialog` | Modal: *"Controlled drug — a second member of staff must witness. Witness: enter your own sign-in details."*; witness roles listed (*"Vet or nurse"*); identifier and password fields (`autocomplete="off"`), **Confirm and dispense**; `400 WitnessNotVerified` → the server's message in the dialog, password cleared, focus to identifier; any other outcome closes it and clears both fields |
| `DispenseResult` | `201` → *"Dispensed — supply 2 of 3."* with **Print label** (primary), **Back to queue**; `unpricedLines` → a neutral notice *"One item has no price set — Billing has been told."*; refusals mapped per §11.9 and the plan refetched |
| `DispensingDetail` | Lines with lots and expiry, dispenser, witness, FEFO reason, status, label preview, print count; actions from `allowedActions`: **Reprint label**, **Reverse** → confirm (UI-3) with reason and, for controlled, `WitnessDialog` |
| ⭐ `MedicineLabel` · `/label` route | Print layout from the frozen `label`: clinic name and phone, cat name, owner, medicine line, dose line (large), instructions, quantity, dispensed date, **expiry**, prescription number, prescriber, *Controlled drug* notice when present, standard warnings. `@media print` hides everything else; `--label-width`/`--label-height`. **Print** button calls `window.print()` then `POST …/label-prints`; the route also offers **Back** |

### 11.5 Diet authorisations — US-PHM-08

| Component | Behaviour |
|---|---|
| `DietAuthorisationForm` | Needs `visitId` (same visit rules as §11.2); product search over `GET /api/staff/diet-products` (name, brand); valid from (default today) and valid to (quick picks 30 / 90 / 180 days, max shown as text); notes. `400 DietAuthorisationAlreadyActive` → *"Mitu already has an active authorisation for this food — extend it instead."* with a link |
| `CatDietAuthorisationsTable` | Product, valid from–to, **effective** status as text (*Active*, *Expired*, *Revoked*), vet; **Extend** (new valid-to) and **Revoke** (reason, UI-3) for vets |

### 11.6 Controlled-drug register — FR-PHM-7 · `/staff/pharmacy/controlled-drugs/**`

| Component | Behaviour |
|---|---|
| `RegisterToolbar` | Filters (medicine, batch, kind, date range) in URL params; **Export CSV** (a plain link to the export endpoint with the filters — the browser downloads it); tabs *Entries* / *Balances* / *Integrity* |
| ⭐ `RegisterTable` | Entry number, recorded, kind (text), medicine, lot, **quantity with sign and unit**, **running balance**, dose, wastage, cat, customer, by, witness, source label (link to the dispensing where applicable), *Corrected by #…* / *Corrects #…* links, sealed (✓ with text *"Sealed"* / *"Awaiting seal"*). **No edit or delete control exists** (ADR-FE-065). Below 768 px each entry is a card |
| `RegisterEntryDetail` | The entry, its seal (chain position, hash in a monospace block), and the **correction chain** beneath the original, each correction with its reason; **Add correction** for vets only |
| `CorrectionForm` | Shows the original read-only; fields: corrected dose text, corrected cat (search), quantity change (`QuantityInput`, may be 0, sign explained: *"−1 means one fewer unit in stock than recorded"*), notes, **reason** (required); a non-zero quantity shows *"This also adjusts stock by the same amount."*; submit → confirm (UI-3) → `WitnessDialog` → `POST` |
| `BalancesTable` | Medicine, lot, register balance, stock balance, **Mismatch** as text and icon where they differ |
| `IntegrityPanel` | *"Register intact — 1,204 entries checked 10:40"* or *"Integrity check failed at entry position 311"* (error styling **and** text) plus entries awaiting a seal for over 10 minutes |

### 11.7 Formulary — US-PHM-04 · `/staff/admin/formulary/**`

| Component | Behaviour |
|---|---|
| `FormularyTable` | Search, filters (controlled, active, stocked), columns code, name, strength, form, dosing basis, *Controlled*, *Stocked*, status |
| `FormularyMedicineForm` | Code (create only), name, active ingredients (chips, 1–5), drug classes (chips), form, **strength** (amount + unit + *per* quantity + dispensing unit — preview *"1.5 mg per 1 ml"*), default route and allowed routes, dosing basis → mg/kg min/max (required for *Per kg*), contraindications (textarea), dispensing unit, **linked SKUs** (picker over `GET /api/admin/formulary/sku-search?term`, showing product, variant and barcode; a SKU already linked to another medicine is shown disabled with that medicine's name), `InteractionRows` (medicine picker **or** term, severity, note). Saves with `expectedVersion`; `409` → *"Someone else changed this medicine. Reload to see their changes."* |
| `ControlledToggle` | Separate action with confirm; turning on needs a schedule label; turning off needs a reason when the server asks (`400 ControlledFlagChangeReasonRequired` → reason field revealed) |
| `ActivationToggle` | Confirm stating that existing prescriptions remain dispensable |

### 11.8 Portal — US-PHM-06, 07, 08 · `/portal/prescriptions/**`

| Component | Behaviour |
|---|---|
| ⭐ `MyPrescriptionsList` | Tabs *Active* / *Past* / *Authorised foods*; a cat filter when the owner has more than one cat; each active row: cat, medicine (strength, form), **dose and frequency** (*"0.3 ml once daily"*), **remaining** (*"5 days left"*), issuing vet, and a repeat status (*"2 repeats left, until 31 Dec"*, *"Repeat requested 20 Sep"*). Empty: *"No active prescriptions."* — **US-PHM-07** |
| `MyPrescriptionDetail` | Instructions, dispensing history (date, branch, quantity — no batch numbers), and the repeat area: **Request a repeat** when `canRequest` (dialog: branch to collect from, optional note, confirm) → *"Requested. We'll message you when it's ready to collect."*; otherwise `cannotRequestReason` verbatim with **Book a consultation** (`/book?catId=`) when the reason is an ended validity or no repeats left — **US-PHM-06 AC2**; an open request shows **Withdraw request**. `?action=repeat` opens the dialog on load when allowed |
| `AuthorisedFoodsList` | Per cat: product name, valid until, effective status; active rows say *"You can buy this for {cat} in the shop."* (link to the shop product when HO-SHP-002 provides a route; plain text until then); expired rows say when it ended and link to book a consultation |

### 11.9 Error mapping — `lib/api/errors.ts`

Append entries for every HO-PHM-001 command's `propertyName`s and map the §10.4 codes to the messages
used above, notably `RecentWeightRequired`, `PrescriptionWarningsNotAcknowledged`,
`PrescriptionWarningsChanged`, `AllergyAcknowledgementReasonRequired`, `VisitNotOpen`,
`CatNotTreatable`, `BatchExpired`, `OnlyExpiredStockAvailable`, `FefoOverrideReasonRequired`,
`InsufficientDispensaryStock`, `InsufficientBatchStock`, `DispenseQuantityMismatch`,
`PrescriptionValidityEnded`, `NoSuppliesRemaining`, `NoRepeatsRemaining`, `RepeatRequestAlreadyOpen`,
`WitnessRequired`, `WitnessNotVerified`, `ReversalWindowPassed`, `DietAuthorisationAlreadyActive`,
`ProductNotPrescriptionDiet`, `SkuAlreadyLinkedToMedicine`, `ControlledFlagChangeReasonRequired`. The
server's message is preferred when present; the mapped text is the fallback.

---

## 12. Mocks — `web/mocks/handlers/pharmacy/*.ts`

Generated from HO-PHM-001 §12.4. Fixtures: a cat with a `Penicillin` allergy and one without any
allergies recorded; a cat with no weight in 45 days; a formulary with a per-kg oral suspension
(1.5 mg/ml, 0.05–0.10 mg/kg), a fixed-dose topical, an amoxicillin with class `penicillin`, a
prednisolone, a controlled injectable, an unstocked medicine; SKU-search results including one already
linked; safety-check responses for within range, above range, not checkable, allergy conflict, `Major`
and `Minor` interactions, and a changed fingerprint on the second call; issue responses `201`,
`400 PrescriptionWarningsNotAcknowledged`, `409 PrescriptionWarningsChanged`,
`400 RecentWeightRequired`, `400 VisitNotOpen`, `403`; a dispense plan with FEFO suggestion across two
batches plus an expired batch; a plan with only expired stock; a controlled plan; dispense `201` with a
label and an unpriced line, `400 WitnessNotVerified`, `400 BatchExpired`, `409`; a queue with both
sections; a register page with every kind, a correction chain, sealed and unsealed entries, balances
with one mismatch, integrity intact and broken; portal prescriptions active, completed-but-running,
expired with `cannotRequestReason`, one with an open request; authorised foods active and expired;
every `403`.

---

## 13. Allowed scope

```
web/app/(staff)/staff/cats/[catId]/prescriptions/**
web/app/(staff)/staff/cats/[catId]/diet-authorisations/**
web/app/(staff)/staff/cats/[catId]/layout.tsx                 ← two tab entries only (HO-CAT-002's file)
web/app/(staff)/staff/pharmacy/**
web/app/(staff)/staff/admin/formulary/**
web/app/(staff)/layout.tsx                                    ← three nav entries only
web/app/(portal)/portal/prescriptions/**
web/app/(portal)/portal/cats/[catId]/page.tsx                 ← active-prescriptions summary link only (HO-CAT-002's page)
web/app/(portal)/layout.tsx                                   ← one nav entry only
web/components/{prescriptions,pharmacy,controlled-drugs,formulary,diet-authorisations,my-prescriptions}/**
web/components/common/Quantity.tsx
web/lib/api/{formulary,prescriptions,pharmacy,controlled-drugs,diet-authorisations,my-prescriptions}.ts
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/lib/format/**                                             ← §8.4 additions (incl. units.ts)
web/lib/hooks/**                                              ← new hooks only
web/lib/validation/**
web/styles/print-label.css                                    ← or the equivalent Tailwind layer file the scaffold uses
web/mocks/handlers/pharmacy/** · web/mocks/handlers/index.ts
web/tests/components/{prescriptions,pharmacy,controlled-drugs,formulary,diet-authorisations,my-prescriptions,common}/**
web/tests/e2e/pharmacy-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-059…068
docs/test-cases/TC-HO-PHM-002.md · docs/gates/HO-PHM-002-*.md
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**` | HO-PHM-001 |
| `.ai/**`, `.claude/**` | Governing docs and agents |
| `docs/**` except `docs/test-cases/TC-HO-PHM-002.md` and `docs/gates/HO-PHM-002-*.md` | Inputs |
| Every other HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002 and HO-APT-002 file beyond the lines in §13 | Their areas |
| Any storefront, cart or checkout file | HO-SHP-002 / HO-ORD-002 render the diet gate |
| `web/package.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs` | No dependency or configuration change (§10) |

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| **A — composer and safety** | | |
| U-A1 | Composer without `visitId`; with a finalised visit (`400 VisitNotOpen`) | No form, the open-a-visit message; the finalised message |
| U-A2 | Typing a dose | One safety check per 400 ms pause, not per keystroke; previous result stays visible while checking |
| U-A3 | Above-range assessment | *"Above range"* with mg/kg and mg figures, units visible; acknowledgement checkbox present; submit without it → field error — **US-PHM-02 AC1** |
| U-A4 | `Stale` weight assessment | Refusal text with the latest weight and date; **Record weight** opens the dialog; after save a new check runs — **US-PHM-02 AC2** |
| U-A5 | Allergy conflict | Listed first with *Blocking*; reason textarea required; tick without reason → error — **US-PHM-03 AC1** |
| U-A6 | `Major` and `Minor` interactions | Both shown with their severity words; only `Major` needs a tick — **US-PHM-03 AC2** |
| U-A7 | Fingerprint changes after ticks | Ticks cleared; live region *"Warnings changed — review again"* |
| U-A8 | Submit `409 PrescriptionWarningsChanged` | Panel replaced with the returned assessment; form values kept |
| U-A9 | Safety check request fails | *"The safety check could not run…"*; issue blocked |
| U-A10 | `NotCheckable` | Warning text shown; no tick required |
| U-A11 | ADR-FE-068 sentence and "No allergies recorded" information | Present |
| U-A12 | Unit select for a tablet medicine | Offers tablet and mass units only; no default selected |
| **B — detail** | | |
| U-B1 | Detail actions | Only `allowedActions` rendered |
| U-B2 | Safety record | Weight, dose/kg, range, acknowledger and reason rendered as issued |
| U-B3 | Cancel confirm | States the number and that an open request will be declined |
| **C — dispensing** | | |
| U-C1 | Plan with suggestion | Suggested batches preselected; expired row disabled with its date as text |
| U-C2 | Only expired stock | Blocking notice; **no dispense control in the DOM** — **US-PHM-05 AC2** |
| U-C3 | Choose a later-expiring batch | Reason field appears and is required |
| U-C4 | Totals | *"8 of 10 ml selected"*; submit refused until equal |
| U-C5 | Scan a lot barcode (typed + Enter) | Matching batch selected; unknown lot → message |
| U-C6 | Controlled dispense | `WitnessDialog` opens before any request; fields `autocomplete="off"` |
| U-C7 | `WitnessNotVerified` | Server message in the dialog; password cleared; identifier focused |
| U-C8 | Dialog cancelled / unmounted / success | Both fields empty afterwards; nothing in storage, URL or query cache |
| U-C9 | `MedicineLabel` | Every US-PHM-05 AC1 field present (cat, owner, medicine, dose, instructions, date, expiry); print CSS hides app chrome |
| U-C10 | **Print** | `window.print` called then one `POST …/label-prints`; re-rendering the route posts nothing |
| U-C11 | Reverse | Confirm names quantity, batch and supply; controlled → witness step |
| **D — register** | | |
| U-D1 | Register table | No edit or delete control anywhere (query by role and text) |
| U-D2 | Correction chain | Original shown unchanged with corrections beneath, each with reason |
| U-D3 | Correction form with non-zero quantity | Stock-adjustment note shown; confirm text names both entry numbers |
| U-D4 | Integrity broken | Error text with the position, not colour alone |
| U-D5 | Register at 360 px | Cards, no horizontal scroll |
| **E — formulary** | | |
| U-E1 | Per-kg basis without range | Field errors on min/max |
| U-E2 | Strength preview | *"1.5 mg per 1 ml"* |
| U-E3 | Controlled toggle | Separate confirm; schedule required on; reason revealed on `400` |
| U-E4 | `409` on save | Reload message; values kept |
| U-E5 | SKU picker with an already-linked SKU | Shown disabled with the other medicine's name; not selectable |
| **F — portal** | | |
| U-F1 | Active list | Medicine, dose, frequency, remaining days, issuing vet per row — **US-PHM-07** |
| U-F2 | `canRequest: false` with an ended validity | Server reason verbatim + **Book a consultation** to `/book?catId=` — **US-PHM-06 AC2** |
| U-F3 | `canRequest: true` → request | Dialog; success message; the row shows *Repeat requested* — **US-PHM-06 AC1** |
| U-F4 | `?action=repeat` | Dialog opens on load when allowed; not when refused |
| U-F5 | Authorised foods | Active and expired rows with their texts — **US-PHM-08** |
| **X — cross-cutting** | | |
| U-X1 | Every clinical screen rendered with fixtures | Every numeric dose, quantity, weight or range is inside a `[data-clinical-value]` element produced by `Quantity`; no bare clinical number elsewhere — **UI-8** |
| U-X2 | Every form: server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X3 | Dirty composer / correction / formulary form + navigate | Unsaved-changes confirm |
| U-X4 | Queue poll while a dialog is open | Dialog values kept |

## 16. End-to-end test cases

MSW first, then the real API; both runs recorded.

| ID | Case | Expected |
|---|---|---|
| E-A1 | **AC-4**: vet opens a cat with a visit, prescribes above range (warned, acknowledges), tries amoxicillin for the penicillin-allergic cat (blocking warning, reason), issues; receptionist dispenses from the queue with the FEFO batch and prints the label; owner signs in and sees the prescription under *Active* | Each step passes; the real run shows the exact batch on the dispensing detail and the reduced stock through HO-IMP's stock view |
| E-A2 | Receptionist opens the composer URL and posts directly (real API) | Permission-denied state; server `403` — **AC-18** |
| E-A3 | Stale weight → record weight from the panel → issue | Issued |
| E-B1 | Only expired stock | No dispense button; the server's message |
| E-B2 | Controlled drug with a nurse witness; then with a receptionist witness | Dispensed with a register entry visible on the register page; *"The witness could not be verified…"* |
| E-B3 | Reprint a label | Print count 2 on the dispensing |
| E-C1 | Vet adds a correction to a register entry | New entry linked beneath the original; original unchanged; integrity still intact after the worker (real run) |
| E-D1 | Owner requests a repeat inside the window; staff dispense it from *Repeat requests*; owner receives *ready to collect* in the inbox (real run) | As stated — **US-PHM-06 AC1** |
| E-D2 | Owner opens an expired repeatable prescription | Reason and **Book a consultation** — **US-PHM-06 AC2** |
| E-E1 | Vet authorises a diet from a visit; owner sees it under *Authorised foods*; (real run, time advanced) it shows as expired and the expiry message is in the inbox | As stated — **US-PHM-08** |
| E-F1 | Administrator creates a per-kg medicine with an interaction, links a clinic SKU and marks it controlled | Listed; prescribable; controlled badge in the picker; *Stocked* |
| E-X1 | axe on every route in §8.1, including the safety panel with warnings, the witness dialog and the label view | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control; label print view unchanged by width |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-PHM-001 §12.4; MSW handlers; `Quantity`, `units.ts`, `lib/format`
   additions.
3. Formulary (admin) — the data every other screen needs.
4. Composer with safety panel and acknowledgements → cat prescriptions tab → detail, cancel, renew.
5. Dispensary queue and lookup → dispense screen → witness dialog → label print → dispensing detail and
   reversal.
6. Controlled-drug register, entry detail, correction, balances, integrity.
7. Portal prescriptions and repeat requests; diet authorisation form, cat tab and authorised foods.
8. U-series, E-series on MSW, then E-series against the real API per HO-PHM-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | PHM-01 | A vet issues a prescription with every FR-PHM-1 field from an open visit, beside the cat's clinical header | U-A1, E-A1 |
| A2 | PHM-02 AC1 | An out-of-range dose shows the expected range (mg/kg and mg) and must be acknowledged | U-A3, U-A7, U-A8, E-A1 |
| A3 | PHM-02 AC2 | Without a recent weight the vet is prompted to record one before prescribing | U-A4, E-A3 |
| A4 | PHM-03 AC1 | An allergy conflict is a blocking warning needing explicit acknowledgement | U-A5, E-A1 |
| A5 | PHM-03 AC2 | Interactions are shown with their severity | U-A6 |
| A6 | PHM-04 | Administrators maintain the formulary; the controlled flag is a deliberate, confirmed act | U-E1…U-E5, E-F1 |
| A7 | PHM-04 AC2 | Dispensing a controlled medicine requires a witness authenticating with their own credentials | U-C6, U-C7, U-C8, E-B2 |
| A8 | PHM-05 AC1 | Dispensing uses the server's batch plan and prints a label with cat, owner, medicine, dose, instructions, date and expiry | U-C1, U-C3, U-C4, U-C9, U-C10, E-A1, E-B3 |
| A9 | PHM-05 AC2 | With only expired stock there is no way to dispense | U-C2, E-B1 |
| A10 | PHM-06 AC1 | An owner requests a repeat and staff dispense it without a consultation | U-F3, U-F4, E-D1 |
| A11 | PHM-06 AC2 | Outside the window the owner is told why and offered a consultation | U-F2, E-D2 |
| A12 | PHM-07 | Active prescriptions show medicine, dose, frequency, remaining duration and issuing vet | U-F1, E-A1 |
| A13 | PHM-08 | Vets authorise diets from a visit; owners see authorised foods and their expiry — *storefront gating is HO-SHP-002 / HO-ORD-002 (§4)* | U-F5, E-E1 |
| A14 | FR-PHM-7 / C-2 | The register is read-only in the UI; corrections are new linked entries; integrity is visible | U-D1…U-D4, E-C1 |
| A15 | AC-18 / BR-6 | A non-vet cannot prescribe (UI state and server refusal) | E-A2 |
| A16 | UI-8 | Units visible on every clinical value | U-X1, U-A12 |
| A17 | UI-6 / UI-1 | Zero serious/critical axe violations; 360–1440 px | E-X1, E-X2, U-D5 |
| A18 | AC-4 | Prescription issued and dispensed end to end | **E-A1** |

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering; no new dependency; **no client-side dose, allergy, interaction, FEFO or expiry logic**; the exported components (§8.6) route-independent; scoped edits to HO-CAT-002's layout and pages and the two shells match §13 |
| 2 | SECURITY | `security-reviewer` | **Witness credentials**: local state only, `autocomplete="off"`, cleared on every outcome, never logged, cached or put in a URL (U-C8); acknowledgements bound to the fingerprint; instructions, notes and reasons rendered as text; actions only from `allowedActions` / `canRequest`; no batch numbers on the portal |
| 3 | CODE_REVIEW | `code-reviewer` | `Quantity` used for every clinical value; debounce correct; polling never resets dialogs; component size; label print counted once per action |
| 4 | TEST | `test-engineer` | `TC-HO-PHM-002.md` first; mock and real runs separate; E-A1 as one continuous scenario |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A13's storefront half recorded as HO-SHP-002 / HO-ORD-002's |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold and `[catId]` layout, HO-ACC-002,
HO-ADM-002, HO-NTF-002, HO-APT-002). Do not build a parallel shell, cat header or session guard.

**20.2 A screen seems to need client-side clinical logic** — mg/kg maths, allergy matching, batch
choice, an expiry comparison. Ask the server; do not compute it.

**20.3 Someone asks for an override of an expired batch or a skip of the witness.** There is none
(BR-3, BR-7, HO-PHM-001 ADR-146). Report it.

**20.4 The real API disagrees with HO-PHM-001 §12.4.** Report it; do not shim.

**20.5 A screen needs an endpoint HO-PHM-001 does not specify** — in particular a cat's **open visits**
list, which belongs to HO-EMR-001 (OQ-5). Report it; do not invent a mock field.

**20.6 An edit control for a register entry, dispensing, label or issued prescription is requested.**
It contradicts C-2 / D-5 and ADR-FE-065. Report it.

**20.7 A new dependency seems necessary** (§10). Raise it.
