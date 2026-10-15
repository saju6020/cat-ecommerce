# HO-SRG-002 — Surgery: Spay, Neuter and Procedures (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-SRG-002** |
| Title | Surgery — Spay, Neuter and Procedures — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §5 — **US-SRG-01 … US-SRG-11** (US-SRG-09 deferred, §4) |
| SRS | FR-SRG-1 … FR-SRG-11; **UI-1, UI-3, UI-5, UI-6, UI-7, UI-8**; NFR-USA-4, NFR-USA-5, NFR-SEC-2; BR-5, BR-7; AC-3, AC-18 |
| Pairs with | [`HO-SRG-001`](HO-SRG-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-SRG-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, cat header, weight entry), [`HO-ACC-002`](HO-ACC-002.md) (sessions, `client.ts`, error mapping, customer search, `PermissionDeniedState`, `UnsavedChangesGuard`), [`HO-ADM-002`](HO-ADM-002.md) (active branch, `lib/format`, in-repo grid approach), [`HO-NTF-002`](HO-NTF-002.md) (inbox), [`HO-APT-002`](HO-APT-002.md) (front desk, calendar, `useServerNow`, time formatting) |
| Depends on (not yet written) | **`HO-EMR-002`** (consent capture component), **`HO-PHM-002`** (witness confirmation component, medicine picker), **`HO-IMP-002`** (usable-batch picker). The assumptions are in §8.5 |
| Delivery order | Section 10 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended (build) | **2026-10-14 -- implementation, branch `ho/SRG-002`, built on master `5a187b6b` where HO-SRG-001's backend, HO-PHM-002, HO-EMR-002, HO-IMP-002 and HO-APT-002 are merged. The backend as built wins over this handover's text (section 20.4); each difference is recorded here.** **(1) Built:** every screen in section 3.2 except US-SRG-09 (deferred by section 4): booking (customer and cat, procedure, three-resource picker with `BusyExplainer`, campaign with the eligibility note, all clashes at once), theatre day list, surgery workspace (header, stepper, reschedule, cancel, Pre-op, Anaesthesia, Report, Recovery, Discharge, History tabs by URL hash), post-op sheet, protocols, campaigns and cases, the front desk's Recovery tab, the calendar's surgery block, the owner's pages. ADR-FE-069 to 078 are in `web/docs/decisions.md` (077 and 078 are build decisions). **(2) Witness (section 8.5, ADR-FE-071) -- HO-SRG-001 section 1 Amended (16):** the backend takes the witness's identifier and password in the SAME request as the drug entry or correction, not a confirmation id. HO-PHM-002's `WitnessDialog` (it returns the credentials to its caller) is reused as it is; the drug dialog's primary button for a controlled drug is **Next: witness**, disabled until the wastage (0 allowed) and every other required part is filled, and the entry is saved when the witness confirms. `useLogDrug` and `useCorrectEntry` are plain functions (not TanStack mutations) so a password is never held in a mutation cache. The idempotency `entryId` is one per entry and changes only if the medicine, batch, quantity drawn or dose change. `WitnessConfirmationInvalid` is not a code the backend raises (it is still mapped for the section 11.4 message). **(3) Components that were not built as section 8.5 assumed:** HO-EMR-002 built a consent capture PAGE (`/staff/consents/new?catId&appointmentId&kind`), not a `ConsentCaptureDialog`, so *Record consent* links to it (once for Surgery, once for Anaesthesia: the backend requires both kinds). HO-IMP-002 built no `BatchPicker`: `DrugBatchChooser` is a thin radio list over its `useBatches` hook (usable batches only, soonest expiry first), a deviation from section 20.2, **owner-accepted 2026-10-14 ("Accept it")**; a shared picker is a later Inventory follow-up (IMP backlog 63, SRG backlog 50). HO-PHM-002's `FormularyPicker` and `useFormularyMedicine` are the medicine picker. **(4) Detail and list views give less than the screens in section 11 want (section 20.5: nothing invented):** (a) `GET /api/staff/surgeries/{id}` has no `serviceId`, so the reschedule dialog finds the service in `GET /api/staff/surgeries/services` by the procedure's name; (b) a checklist item carries `completedBy` as an id only, so a name is shown only when it is the surgeon, assistant or overriding vet; (c) `neuterStatusApplied: false` carries no reason, so the notice states that the record was not updated and offers **Apply neuter status**; (d) `POST .../discharge` returns the surgery only, with no channel, so the success line says the instructions were sent to the owner and are in their inbox; (e) `GET /api/customers/me/surgeries/{id}` has the branch NAME only, so the clinic's phone and address come from the public branch list (`useClinicBranches`); its `bookFollowUpLink` is absolute and is turned into the portal's own `/book?...` path; (f) `allowedTransitions` is status-only, not permission-aware (`Start` is listed for a receptionist), so which control to show also reads the staff session's roles, for presentation only; (g) there is no theatre list endpoint in Surgery, so the booking form's theatres are the branch's active rooms of kind `Theatre` from `GET /api/admin/branches/{id}/rooms` (HO-APT-002's precedent for the front desk); (h) `ReportIncomplete` is ONE violation whose message names every missing part, so the missing parts are read out of the message; (i) a campaign with no branches runs at all branches (`SurgeryCampaignAggregate.Evaluate`), which the form now says. **(5) Scope notes:** nav entries are in `nav/areas/surgery.ts` (a staff and a portal item), not the two layouts (HO-PHM-002, HO-IMP-002 and HO-BIL-002 recorded the same deviation); the portal entry is always shown, not "only when the customer has a surgery" (nothing tells the shell that without an extra request); the front desk's Recovery tab is in `app/(staff)/staff/front-desk/FrontDeskView.tsx`, where HO-APT-002 put the tabs, not `page.tsx`; `components/calendar/CalendarGrid.tsx` and `CalendarAppointment` in `lib/api/staff-appointments.ts` (two optional fields, `surgeryId` and `assistantId`, which the calendar endpoint already returns) were edited for section 11.10; `CalendarAgenda` and the calendar's empty-theatre-cell deep link to the booking form are NOT changed (outside section 13); `web/styles/print.css` was not created -- the shared `components/print/PrintSheet` (HO-ORD-002 ADR-FE-094) carries the print rule; shared pieces reused: `ResponsiveTable`, `Tabs`, `Banner`, `PageHeading`, `Panel`, `DefinitionList`, `ReasonDialog`/`ActionDialog`, `VisitCatHeader`, `CompactCatHeader`, `CustomerPicker`, `QuantityInput`, `FieldWithUnit`. Non-spec e2e helpers `surgery-helpers.ts` and `surgery-routes.ts` are accepted by precedent (Pharmacy backlog 48). `web/tests/unit/surgery-format-and-validation.test.ts` (units, error mapping, report-field parsing) is outside the literal §13 test paths and is recorded here as an accepted addition. Two small deviations from the wording of section 11: the checklist's weight item has no "use an existing weight" action (the weight completes from the cat's own weight record, as section 11.3 says); the report's surgeon and assistant are the surgery's own and shown as text (not selects). **(6) Mock layer:** `web/mocks/handlers/surgeries/**`. The cat and customer mocks have no intact female cat, so `support.ts` answers `GET /api/cats/{id}` (+ weights, vaccination status), the customer lookup and the SKU batch list ONLY for ids this mock owns and falls through for every other; it is registered first in `mocks/handlers/index.ts`. A consent posted for a surgery's appointment is recorded in the EMR mock's own database (the EMR mock only knows appointments of its own). `setSurgeryCallerRoles`, `resetSurgeryMockData`, `surgeryFixtureIds` are the test hooks. **(7) Tests:** `docs/test-cases/TC-HO-SRG-002.md`. 123 component and unit cases in 15 files (the whole web suite last ran at 1636 tests in 234 files, before the re-check fixes); 11 flow cases, 24 axe cases (chromium and mobile-360) and 20 responsive cases on MSW. **(8) Gate fix rounds (2026-10-14).** First round (ARCHITECTURE FAIL H1, CODE_REVIEW FAIL H1, SECURITY PASS): the six other-area hook files and the existing hunks of `lib/api/errors.ts` that Prettier had reformatted were reverted to 5a187b6b (only the appended Surgery block remains); the report editor flushes a pending autosave when it unmounts (a tab switch); booking clears an assistant who becomes the surgeon and a deep-linked theatre that is not a Theatre room; the drug `entryId` key resets on `AnaesthesiaEntryIdMismatch`, and drawn/wasted clear when the batch unit changes; a reschedule with no matching service says so; campaign prices are strict (`^[0-9]+([.][0-9]{1,2})?$`); every path id in the surgery API modules is `encodeURIComponent`-ed; the override toast follows `overrideIgnored`/`consentWasOverridden`; "Last vitals" skips struck-through entries; `instantFromTime` handles midnight; the day list groups by theatre id. Second round (combined re-check R1 to R8): every successful report save also writes the saved text and version into the report query cache, so a remounted editor starts from what was saved; the `entryId` fingerprint is exactly the server's four replay fields (medicine, batch, quantity drawn, dose); a correction across midnight takes the nearest instant around the original entry; a failed services fetch is an error, not "procedure no longer found". Backlog: SRG 50 to 72 and later, IMP 63. **Not run: the real-API re-run** (no surgery seeder exists and `tests/**` is forbidden; nothing touched `catecommerce_dev` or `catecommerce_test`).
| Amended | **2026-10-11 -- HO-SRG-001 section 1 (18) contract changes, mirrored for the FE build (SRG backlog 44), with the later outcomes of the idempotency key (backlog 41, 42).** (1) `GET /api/staff/surgery-campaigns` and `GET /api/customers/me/surgeries` return `PagedResult<T>` (`{ items, pageNumber, pageSize, totalCount }`) with `pageNumber` and `pageSize` query parameters (`pageSize` clamped to 100; `pageSize<=0` and `pageNumber>1000000` are 400), not arrays. (2) `GET /api/staff/surgery-campaigns/{id}/cases` keeps `cases` and `totals` (totals cover every case) and adds `pageNumber`, `pageSize`, `totalCases`. (3) `POST .../anaesthesia-log/drugs` accepts an optional client-generated `entryId` GUID, the idempotency key: the SAME id and body again returns the first `201` result with no second entry, deduction or register entry. Generate one id per drug entry and reuse it on every retry. (4) New `409` codes: `DuplicateAnaesthesiaEntry` (the id belongs to another surgery or another user; use a NEW id), `AnaesthesiaEntryRaced` (an identical request is being saved at the same moment; retry with the SAME id, which then replays), `AnaesthesiaEntryIdMismatch` (the id was used on this surgery with a different medicine, batch, quantity drawn or dose; send the original values or use a new id), `AnaesthesiaEntryAlreadyCorrected` (two simultaneous corrections of one entry; reload and correct the latest). A sequential second correction of an entry that is no longer the latest is `400 CorrectionTargetNotFound`, so the FE handles both. (5) `PUT .../recovery-status` past 50 changes is `RecoveryHistoryLimitReached`. |

---

## 2. Objective

Build the **surgery workspace for clinical staff** and the **pre-op and post-op pages for owners**.

- **Reception** books a surgery by choosing a procedure, a date, a surgeon, an assistant and a theatre,
  and sees real available starts. Every clash is explained at once rather than one at a time.
  Reception also sees the theatre day list, with outstanding checklist items, and a **recovery board**
  on the front desk for owners who call.
- **Nurses and vets** work through one surgery page: pre-op checklist, consent status, admit, start
  (with the vet-only emergency override), the **anaesthesia log** as a timeline, the surgical report,
  recovery status and discharge with post-op instructions and follow-ups.
- **Owners** see the date, admission time and fasting instructions before surgery, and afterwards the
  post-op instructions and follow-up dates, with a link to book the follow-up.

**There is no payment step and no deposit.** The price is shown for information (HO-SRG-001 ADR-170).

Three things make this harder than it looks:

1. **The server is the only authority on clashes and gates.** The booking form shows server
   availability and server reasons; the start button reflects the server's `startBlockers` and
   `allowedTransitions`. Nothing is computed in the browser (ADR-FE-069, ADR-FE-072).
2. **Append-only must look append-only.** The anaesthesia log has no edit or delete controls
   anywhere. A mistake is fixed with **Correct…**, which adds a linked entry and leaves the original
   visible and struck through (ADR-FE-070).
3. **A controlled drug needs a second person at the screen.** The witness types their own
   identifier and password into a dialog on the same device. The credential goes straight to
   HO-PHM-001 and is never stored, logged or kept in state after submit (ADR-FE-071).

---

## 3. In scope

### 3.1 Inherited — do not revisit

HO-CAT-002's stack and cat header (UI-7); HO-ACC-002's session model, `client.ts`, route groups,
error mapping, `PermissionDeniedState`, `UnsavedChangesGuard`, customer search; HO-ADM-002's active
branch and `lib/format`; HO-APT-002's `useServerNow`, `formatSlot`/`formatAppointmentWhen`, front-desk
page and calendar grid.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-SRG-01, 11 | **Book a surgery**: customer → cat → procedure → date → surgeon, assistant, theatre → available starts → optional campaign → book | `/staff/surgeries/new` |
| US-SRG-01 | Reschedule (same picker) and cancel from the surgery page | `/staff/surgeries/[surgeryId]` |
| US-SRG-02 | **Theatre day list** with checklist status and outstanding items by name; checklist panel on the surgery page | `/staff/surgeries`, `/staff/surgeries/[surgeryId]` (Pre-op tab) |
| US-SRG-03 | Consent status, **Record consent** (HO-EMR-002 component), Start, and the vet-only **emergency override** dialog | Pre-op tab |
| US-SRG-04, 08 | **Anaesthesia log** timeline; add drug (with batch and, for controlled drugs, wastage and witness), vitals, milestone; **Correct…** | Anaesthesia tab |
| US-SRG-05, 06 | End procedure; **surgical report** editor with draft autosave, finalise, read-only view and addenda; neuter-status confirmation and repair action | Report tab |
| US-SRG-10 | Recovery status control on the surgery page; **Recovery** tab on the front desk | Recovery tab, `/staff/front-desk` (new tab) |
| US-SRG-07 | Discharge form (instructions and follow-ups, protocol-filled), printable post-op sheet; owner surgery pages | Discharge tab, `/portal/surgeries`, `/portal/surgeries/[surgeryId]` |
| US-SRG-07 | Protocol editor per surgical service | `/staff/surgeries/protocols` |
| US-SRG-11 | Campaign list and editor; campaign case list with totals | `/staff/surgeries/campaigns`, `/staff/surgeries/campaigns/[campaignId]` |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-SRG-001 owns `src/**` |
| **Any payment, deposit or price-to-pay step** | HO-SRG-001 ADR-170 |
| **US-SRG-09** (linking a boarding stay, stay invoice) | Deferred to HO-BRD-001 / HO-BRD-002 |
| Building the consent capture UI, the witness confirmation UI, the medicine picker or the batch picker | HO-EMR-002, HO-PHM-002, HO-IMP-002 own them; this slice embeds them (§8.5) |
| Report exports (CSV/PDF) | HO-RPT-002 |
| A charting, timeline, rich-text, signature, date-picker or time-zone library | §10 |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| HO-ACC-002, HO-ADM-002, HO-NTF-002, **HO-APT-002** built | ❌ at baseline — §20.1 |
| **HO-EMR-002** consent component, **HO-PHM-002** witness dialog and medicine picker, **HO-IMP-002** batch picker | ❌ handovers not yet written — §20.2. Screens that need them are built last (§17) |
| HO-SRG-001 API | ❌ — build against MSW generated from §12.4 there, then re-run every E-case against the real API |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-SRG-001`](HO-SRG-001.md) | §12.4 endpoints and shapes; §10.4 codes and their messages; §8.3 status machine; §8.5 clash codes and their order; §8.6 checklist rules; §8.7 start gate and override; §8.8–§8.9 log entry kinds, corrections and controlled-drug fields; §8.10 follow-ups; §12.3 who may do what |
| [`HO-APT-002`](HO-APT-002.md) | §8.4 time formatting and `useServerNow`; §11.5 front desk (a tab is added); §11.6 calendar (surgery blocks link here); ADR-FE-027 polling that never resets input; ADR-FE-028 actions from `allowedTransitions` |
| [`HO-ADM-002`](HO-ADM-002.md) | §8.3 active branch; §8.4 `lib/format` |
| [`HO-ACC-002`](HO-ACC-002.md) | §11.2 client; §11.14 error mapping; customer search; `UnsavedChangesGuard` |
| [`HO-CAT-002`](HO-CAT-002.md) | Cat header (UI-7), weight entry form |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.4 | UI-1/3/5/6/7/8; NFR-USA-4/5 |

---

## 7. Build rules

| Req | Rule | Where |
|---|---|---|
| **UI-1** | 360 px floor. The day list becomes cards; the surgery page tabs become a select; the anaesthesia timeline is a single column (time left, entry right) at every width, with no horizontal scroll | §11 |
| **UI-3** | Confirm, stating what is affected: cancel surgery (*"Cancel Mitu's spay on Mon 12 Oct at 09:00 with Dr Rahman? The owner will be told."*), reschedule, start with override, end procedure (*"End the procedure as Completed? For a spay this sets Mitu's neuter status."*), finalise report (*"After finalising, the report can only be changed by addendum."*), discharge | Each screen |
| **UI-5** | Day list, recovery board and the open surgery page refetch every **30 s** without resetting any open form, dialog or unsaved report text (HO-APT-002 ADR-FE-027) | §11.2, §11.8 |
| **UI-6** | WCAG 2.1 AA. Checklist state is text plus icon; start blockers are a list read by a live region; the timeline is an ordered list with time as text; the witness dialog is fully keyboard operable and labels its fields "Witness identifier" and "Witness password"; struck-through corrected entries also say *"Corrected"* in text | §11 |
| **UI-7** | The cat header (name, photo, age, sex/neuter, weight, allergies, conditions, alerts, high-risk flag) is at the top of the surgery page, the booking form (after the cat is chosen) and each recovery-board card (compact) | HO-CAT-002's component |
| **UI-8** | Dose, quantity, weight and every vital are shown and entered with their **unit always visible** (`0.5 mL`, `4.2 kg`, `38.6 °C`, `120 bpm`), never as bare numbers; the mg/kg figure is labelled | §11.4 |
| **NFR-USA-4/5** | Server messages in plain language; forms keep values across errors; the report draft survives navigation (autosave + `UnsavedChangesGuard`) | HO-ACC-002 §11.14 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (staff)/staff/
    surgeries/page.tsx                                theatre day list                  US-SRG-02
    surgeries/new/page.tsx                            book a surgery                    US-SRG-01, 11
    surgeries/[surgeryId]/page.tsx                    surgery workspace (tabs)          US-SRG-01…08, 10, 07
    surgeries/[surgeryId]/post-op-sheet/page.tsx      printable post-op sheet           US-SRG-07
    surgeries/protocols/page.tsx                      protocol editor                   US-SRG-07
    surgeries/campaigns/page.tsx                      campaigns                         US-SRG-11
    surgeries/campaigns/[campaignId]/page.tsx         campaign editor + cases           US-SRG-11
    front-desk/page.tsx                               ⚠ gains a Recovery tab (HO-APT-002) US-SRG-10
  (portal)/
    portal/surgeries/page.tsx                         my cats' surgeries                US-SRG-07
    portal/surgeries/[surgeryId]/page.tsx             pre-op / post-op instructions     US-SRG-07
  (staff)/layout.tsx                                  ⚠ nav entry: Surgery
  (portal)/layout.tsx                                 ⚠ nav entry: Surgeries (shown only when the customer has one)
web/components/calendar/CalendarGrid.tsx              ⚠ surgery blocks link to the surgery page (HO-APT-002)
```

Surgery tabs are driven by the URL hash (`#pre-op`, `#anaesthesia`, `#report`, `#recovery`,
`#discharge`, `#history`). The default tab follows status: `Scheduled`/`PreOp` → Pre-op; `InTheatre` →
Anaesthesia; `Recovery` → Recovery; `Discharged` → Discharge.

### 8.2 API modules

`lib/api/surgeries.ts` (services, staff, availability, book, list, day, detail, reschedule, cancel,
checklist, ASA, admit, start, override, end procedure, recovery, discharge draft, discharge, follow-ups,
apply-sterilisation-outcome), `lib/api/anaesthesia-log.ts` (read, drug, vitals, milestone,
correction), `lib/api/surgical-reports.ts`, `lib/api/surgery-protocols.ts`,
`lib/api/surgery-campaigns.ts`, `lib/api/my-surgeries.ts`.

### 8.3 State

| Kind | Where |
|---|---|
| Booking choices (customer, cat, service, date, surgeon, assistant, theatre) | **URL search params** — reload-safe; the calendar's empty-theatre-cell click can deep-link here |
| Availability | `['surgeries','availability', params]`, refetched on any choice change and after a `400`/`409` |
| Day list, recovery board | `['surgeries','day', branch, date]`, `['surgeries','recovery', branch]`, `refetchInterval: 30_000` |
| Surgery detail, log, report | `['surgeries', id]`, `['surgeries', id, 'log']`, `['surgeries', id, 'report']`; detail and log poll every 30 s while `PreOp`/`InTheatre`/`Recovery` |
| Report draft | react-hook-form; autosave (debounced 5 s) with `ExpectedVersion`; `409` → reload prompt that keeps the local text for copying |
| Witness credential | **Component state of the witness dialog only**; cleared on submit, cancel and unmount; never in a query key, URL, storage or log (ADR-FE-071) |
| Forms | react-hook-form + zod |

### 8.4 Units — `lib/format` additions

`formatDose(amount, unit)`, `formatQuantity(amount, unit)`, `formatVital(kind, value)` (`bpm`, `/min`,
`%`, `mmHg`, `°C`), `formatMgPerKg(doseMg, weightKg)` labelled *"≈ 0.2 mg/kg at 4.2 kg (weighed 11
Oct)"*. Numeric inputs use `inputMode="decimal"` with the unit rendered as a visible suffix inside the
field's label group (UI-8).

### 8.5 Components owned by other frontend handovers — assumed

| Component | Owner | Assumed props | Used in |
|---|---|---|---|
| `ConsentCaptureDialog` | **HO-EMR-002** | `catId`, `procedureKind="Surgery"`, `procedureId`, `requiredKinds=['Surgery','Anaesthesia']`, `onRecorded()` | Pre-op tab |
| `WitnessConfirmationDialog` | **HO-PHM-002** | `excludeStaffIds`, `onConfirmed({ witnessId, witnessConfirmationId })`; it posts the witness's identifier and password to HO-PHM-001 and returns only the ids | Drug dialog |
| `MedicinePicker` | **HO-PHM-002** | `onSelect({ medicineId, name, strength, isControlled, skuId?, allowedDoseUnits, defaultRoutes })` | Drug dialog |
| `BatchPicker` | **HO-IMP-002** | `skuId`, `locationId="clinical"`, `onSelect({ batchId, batchNumber, expiresOn, onHand, unit })`; lists only usable batches, FEFO first | Drug dialog |

If any is missing or differs when this slice is built, stop (§20.2). Do not build a local copy.

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing the series. **This handover owns ADR-FE-069 …
ADR-FE-078** and uses 069–076.

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-069** | **Booking is a single form with a three-resource picker**: choosing procedure, date, surgeon, assistant and theatre fetches server availability; the starts are buttons; busy intervals per resource are listed under the picker (*"Dr Rahman: 10:00–11:00 in surgery"*) so a missing time is explained before anyone clicks. On submit, **all** server clashes are shown at once next to their fields | US-SRG-01 "each checked … with the reason"; the server is the only clash authority |
| **ADR-FE-070** | **The anaesthesia log is an append-only timeline**: an ordered list by `occurredOn`; no edit or delete controls; **Correct…** opens a prefilled form that posts a correction; the corrected entry stays, struck through, with *"Corrected at 10:42 by Dr Akter — reason"* and a link to its correction | US-SRG-04 AC2 and US-SRG-08 AC2 must be visible, not just enforced |
| **ADR-FE-071** | **Controlled drugs require the in-dialog witness step** (HO-PHM-002's component) before **Save** is enabled; the witness's credential is never held outside that component; after save the entry shows vet and witness names | BR-7 with identifier + password only; a password must never linger in React state or a devtools cache |
| **ADR-FE-072** | **Start renders from the server**: the button is enabled only when `allowedTransitions` has `Start` and `startBlockers` is empty; otherwise the blockers are listed as links to fix them. **Start with emergency override** appears only for vets and only when consent is the sole blocker. It demands a reason of at least 10 characters and a confirm that names the vet | BR-5; the gate is the server's, and the UI must not offer a path the server refuses |
| **ADR-FE-073** | **Recovery board** is a tab on the front desk, polled every 30 s, sorted *Ready for discharge* → *Recovering* → *Stable*, each card with status as text, *"since 10:15"*, who set it, and the owner's phone | FR-SRG-10 "near real time" = UI-5 polling (HO-SRG-001 ADR-168) |
| **ADR-FE-074** | **The report editor autosaves drafts** with `ExpectedVersion`; **Finalise** is a separate, confirmed action; once finalised, the page renders read-only text plus **Add addendum**, never an editor | FR-SRG-5 immutability, visible to the user |
| **ADR-FE-075** | **Units are part of the control**, not a placeholder (UI-8); dose entry shows the mg/kg at the latest weight for information and the weight's date; no warning logic in the browser | UI-8; dose warnings are server policy (HO-SRG-001 OQ-11) |
| **ADR-FE-076** | **The printable post-op sheet** is an HTML route with a print stylesheet (no PDF library): cat, procedure, date, instructions as text, follow-up dates, clinic contact | US-SRG-07 "produced"; no new dependency |

| # | Question | Default |
|---|---|---|
| OQ-1 | Vitals entry cadence reminder (e.g. prompt every 5 min in theatre)? | None — a visible *"Last vitals 12 min ago"* only |
| OQ-2 | Should the anaesthesia tab offer a chart of vitals as well as the list? | List only in this slice (no chart library); an in-repo SVG sparkline per vital is allowed if under the component-size rule |
| OQ-3 | Owner's surgery page during `InCare` — show anything live? | *"Mitu is in our care. Call {branch} on {phone} for an update."* (HO-SRG-001 OQ-12) |
| OQ-4 | The HO-EMR-002 / HO-PHM-002 / HO-IMP-002 components in §8.5 | Assumed as stated; confirmed by those handovers |

---

## 10. Dependencies

**No new package.** A charting, timeline, rich-text, signature-pad, date-picker, PDF or time-zone
library is a §20 stop.

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error / permission-denied states.

### 11.1 Book a surgery — US-SRG-01, 11 · `/staff/surgeries/new`

| Part | Component | Behaviour |
|---|---|---|
| 1 | `SurgeryCustomerCatStep` | Customer search (HO-ACC-002) → cat select (alive cats) → cat header. A **sex mismatch** or **already neutered** answer from the server is shown with its message; *already neutered* offers *"Book anyway — the record may be wrong"*, which re-submits with `confirmNeuterStatusDiscrepancy` |
| 2 | `ProcedureSelect` | From `GET /api/staff/surgeries/services`: name, duration, price *"for information — paid at the clinic"*, sterilisation badge |
| 3 | ⭐ `SurgeryResourcePicker` | Date (in-repo date strip as HO-APT-002), surgeon and assistant selects from `…/staff?serviceId` (the assistant list excludes the chosen surgeon), theatre select (active theatres of the branch). Starts from `…/availability` as buttons *"09:00–10:00"*, marked *"Outside opening hours"* where flagged. Under it, **`BusyExplainer`** lists each resource's busy intervals with labels (ADR-FE-069). With no starts: *"No time on {date} when all three are free."* plus the next day with a start (fetched ahead, up to 7 days) |
| 4 | `CampaignSelect` | From `…/surgery-campaigns/applicable?serviceId&date`; *"Standard price"* by default; choosing a campaign shows its price and remaining cases and requires the **eligibility note** |
| 5 | Submit | Confirm (*"Book Mitu's spay on Mon 12 Oct, 09:00–10:00, theatre 1, Dr Rahman with Nurse Akter?"*). `201` → surgery page. `400` → **every** clash rendered by its `propertyName` beside the theatre, surgeon, assistant, cat or time field, plus a summary list at the top with focus moved to it. `409` race → *"Someone has just booked that time. Availability has been refreshed."* and refetch |

### 11.2 Theatre day list — US-SRG-02 · `/staff/surgeries`

`SurgeryDayList` — date control; grouped by theatre; each row: time, cat (with high-risk and deceased
flags as text), procedure, surgeon, assistant, customer and phone, status as text, **checklist**
(*"Complete"* or *"3 outstanding: consent, weight, ASA grade"* as text with an icon), and a *"Needs
rescheduling"* badge when flagged. Polls 30 s. Links: **Book a surgery**, **Protocols**, **Campaigns**
(each only when the caller has the action).

### 11.3 Surgery page — header and Pre-op tab — US-SRG-02, 03

| Component | Behaviour |
|---|---|
| `SurgeryHeader` | Cat header; procedure; when (branch zone); theatre, surgeon, assistant; status stepper *Scheduled → Pre-op → In theatre → Recovery → Discharged* with the current step as text; price and basis; **Reschedule** (opens `SurgeryResourcePicker` prefilled, with `excludeSurgeryId`) and **Cancel** (reason required) when in `allowedTransitions`; the consent-override banner when one exists (*"Started without recorded consent — emergency override by Dr Rahman at 09:05: {reason}"*) |
| ⭐ `PreOpChecklist` | Five rows in server order: status (text + icon), who and when, note or waiver reason. Actions by item and permission: *Fasting instructions issued* → **Mark issued** (how: message / printed / verbal); *Blood work* → **Mark done** (note) or **Waive** (vets; reason); *Weight* → **Record weight** (HO-CAT-002's weight form; the item completes from the server event, and the row refetches); *Consent* → **Record consent** (`ConsentCaptureDialog`, §8.5); *ASA grade* → `AsaGradeControl` (vets; I–V radio plus *Emergency (E)*) |
| `AdmitButton` | When allowed; `400 AdmissionNotToday` shown plainly |
| ⭐ `StartPanel` | ADR-FE-072. Blocker list from `startBlockers` (each a link that scrolls to its checklist row). **Start surgery** confirm. Vets with consent as the only blocker see **Start with emergency override** → `ConsentOverrideDialog`: reason (10–1000, counter), a confirm naming the vet and saying the override is recorded permanently |

### 11.4 Anaesthesia tab — US-SRG-04, 08

| Component | Behaviour |
|---|---|
| ⭐ `AnaesthesiaTimeline` | Ordered list by `occurredOn`: time (branch zone, `HH:mm`), kind icon **and** text, content (drug: `Medetomidine 0.05 mL IM`, batch, *Controlled — witnessed by Nurse Akter*, *Stock shortfall recorded* where flagged; vitals: each value with its unit; milestone), *"logged 10:42 by Dr Rahman"*. Corrected entries struck through, with *"Corrected"* text and a link to the correction (ADR-FE-070). Header: latest weight with date, *"Last vitals 12 min ago"*, log open/closed. Polls 30 s while open |
| `AddEntryBar` | **Drug** (vets), **Vitals**, **Milestone** — shown by permission and only while `isOpen` |
| ⭐ `DrugEntryDialog` | `MedicinePicker` → dose amount + unit (from `allowedDoseUnits`), route, time given (defaults to now on the server clock, editable back), administered by (defaults to the caller; controlled → vets only). With a SKU: `BatchPicker` (usable batches only), **quantity drawn** with unit. Controlled: **wastage** with unit (required, 0 allowed), then the **Witness** step (`WitnessConfirmationDialog`, excluding the administering vet and the caller) — **Save** is disabled until the witness step returns (ADR-FE-071). mg/kg info (ADR-FE-075). Server errors mapped: `BatchNotUsable` (*"This batch can't be used — it has expired or is quarantined. Choose another."*), `WitnessConfirmationInvalid` (*"The witness confirmation has expired or was already used. Ask the witness to confirm again."*) |
| `VitalsEntryDialog` | HR, RR, SpO2, EtCO2, temperature, systolic, MAP — each optional, units visible; `ImplausibleVitalValue` → the out-of-range fields marked with the server's message and a **Save anyway** checkbox |
| `MilestoneEntryDialog` | Milestone select, time, optional note |
| `CorrectEntryDialog` | Prefilled from the entry; reason (10–500) required; drug corrections of controlled entries need a new witness step; confirm says *"The original entry stays in the log."* |

### 11.5 Report tab — US-SRG-05, 06

| Component | Behaviour |
|---|---|
| `EndProcedurePanel` | In `InTheatre`: **End procedure** with outcome *Completed* / *Abandoned* and the UI-3 confirm (mentions neuter status for sterilisations) |
| ⭐ `SurgicalReportEditor` | Procedure performed (prefilled with the procedure name), findings, technique, suture material, complications (*"Complications occurred"* checkbox reveals the text, required when checked), surgeon and assistant (prefilled). Autosave *"Draft saved 10:42"* (ADR-FE-074). **Finalise** (vets) with confirm; `ReportIncomplete` lists the missing fields and focuses the first |
| `SurgicalReportView` | Finalised: read-only text, *"Finalised by Dr Rahman on …"*, addenda in order with author and time, **Add addendum** (vets) |
| `NeuterStatusNotice` | For sterilisations after *Completed*: *"Neuter status set to Neutered on 12 Oct"* from the refetched cat header; when `neuterStatusApplied` is false, the reason and **Apply neuter status** (the repair endpoint) |

### 11.6 Recovery and discharge tabs — US-SRG-10, 07

| Component | Behaviour |
|---|---|
| `RecoveryStatusControl` | Segmented control *Recovering / Stable / Ready for discharge* with optional note; history list with who and when |
| ⭐ `DischargeForm` | From `…/discharge-draft`: post-op instructions (plain-text area, prefilled from the protocol, editable, 4000 limit with counter); follow-ups rows (kind, date constrained to the window, **Remove** for suture removal with *"Absorbable sutures"*). Submit disabled until *Ready for discharge* (the server also refuses). Success → *"Discharged. Post-op instructions sent to the owner's {channel} and inbox."* and **Print post-op sheet** |
| `FollowUpList` | After discharge: each follow-up with due date and status; **Mark done** / **Cancel** (reason) |
| `PostOpSheet` (route) | ADR-FE-076 |

### 11.7 Protocols and campaigns — US-SRG-07, 11

| Component | Behaviour |
|---|---|
| `SurgeryProtocolTable` / `SurgeryProtocolForm` | One row per surgical service, *"Using defaults"* where none is saved; form: fasting instructions, fasting hours, post-op instructions, follow-ups (kind, day offset, window) with window validation |
| `SurgeryCampaignTable` / `SurgeryCampaignForm` | Code, name, description, dates, branches, per-sterilisation-service prices, max cases, active. `CampaignPriceLocked` → *"Prices can't change once a surgery has been booked under this campaign."* |
| `CampaignCasesTable` | Date range; cases with price and standard price; totals row (*"12 cases · ৳24,000 campaign · ৳60,000 standard"*) — **US-SRG-11 "reportable separately"** (export is HO-RPT-002) |

### 11.8 Front desk Recovery tab — US-SRG-10 · HO-APT-002's `/staff/front-desk`

`RecoveryBoard` — ADR-FE-073. Cards: compact cat header, customer and `tel:` phone, procedure, *"Ended
09:55"*, status as text with *"since 10:15 · set by Nurse Akter"*. Header *"Updated 10:42"*. Polls 30 s.
Empty: *"No cats in recovery."*

### 11.9 Owner pages — US-SRG-07 · `/portal/surgeries/**`

| Component | Behaviour |
|---|---|
| `MySurgeriesList` | Upcoming / past: cat, procedure, date (branch zone), status as text |
| `MySurgeryDetail` | Before: date, **admit by** time, fasting instructions (text), branch address and phone. During `InCare`: OQ-3 text. After discharge: post-op instructions (text), follow-ups with dates and **Book follow-up** (`bookFollowUpLink` → HO-APT-002's `/book`), **Print** |

### 11.10 Calendar link — HO-APT-002's `CalendarGrid`

Blocks carrying `surgeryId` render with a *"Surgery"* label and the assistant's name, open the surgery
page on click or Enter, and are **not draggable** (the server refuses moves anyway —
`AppointmentManagedBySurgery`).

### 11.11 Error mapping — `lib/api/errors.ts`

Append every HO-SRG-001 command's `propertyName`s and map the §10.4 codes to the messages used in §11
(`TheatreBusy`, `SurgeonBusy`, `AssistantBusy`, `StaffBusy`, `SurgeonNotAvailable`,
`AssistantNotAvailable`, `AssistantSameAsSurgeon`, `ProcedureSexMismatch`, `CatAlreadyNeutered`,
`ConsentNotRecorded`, `PreOpChecklistIncomplete`, `ConsentOverrideReasonRequired`,
`AdmissionNotToday`, `AnaesthesiaLogClosed`, `ImplausibleVitalValue`, `BatchNotUsable`,
`WitnessConfirmationInvalid`, `WitnessSameAsAdministrator`, `WastageRequired`, `ReportIncomplete`,
`ReportNotEditable`, `RecordIsImmutable`, `NotReadyForDischarge`, `FollowUpOutsideWindow`,
`CampaignNotApplicable`, `CampaignPriceLocked`, `InvalidSurgeryTransition`). The server's message is
preferred; the mapped text is the fallback.

---

## 12. Mocks — `web/mocks/handlers/surgeries/*.ts`

Generated from HO-SRG-001 §12.4. Fixtures: a branch in `Asia/Dhaka` and one in another zone; two
theatres; surgeons and nurses; availability with busy intervals per resource and a day with no common
start; booking `400` with **three clashes at once**; `409 TheatreBusy`; sex mismatch and
already-neutered responses; surgeries in every status; checklists partly complete; a surgery whose only
blocker is consent; an overridden surgery; a log with drug (controlled and not), vitals, milestones, a
correction chain and a shortfall; `BatchNotUsable`, `WitnessConfirmationInvalid`; report draft,
finalised with addenda, `ReportNotEditable`; `neuterStatusApplied: false`; recovery board with each
status; discharge draft with defaults; owner surgery before/during/after; campaigns applicable,
exhausted and locked; every `403`. **Stand-ins for the §8.5 components exist only in the mock layer
and tests**, never in `components/`.

---

## 13. Allowed scope

```
web/app/(staff)/staff/surgeries/**
web/app/(staff)/staff/front-desk/page.tsx                     ← Recovery tab only (HO-APT-002's page)
web/app/(staff)/layout.tsx                                    ← one nav entry only
web/app/(portal)/portal/surgeries/**
web/app/(portal)/layout.tsx                                   ← one nav entry only
web/components/{surgery,anaesthesia,surgical-report,recovery,surgery-campaigns}/**
web/components/calendar/CalendarGrid.tsx                      ← §11.10 only (HO-APT-002's component)
web/lib/api/{surgeries,anaesthesia-log,surgical-reports,surgery-protocols,surgery-campaigns,my-surgeries}.ts
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/lib/format/**                                             ← §8.4 additions
web/lib/hooks/**                                              ← new hooks only
web/lib/validation/**
web/styles/print.css                                          ← post-op sheet only (create if absent)
web/mocks/handlers/surgeries/** · web/mocks/handlers/index.ts
web/tests/components/{surgery,anaesthesia,surgical-report,recovery,surgery-campaigns}/**
web/tests/e2e/surgery-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-069…076
docs/test-cases/TC-HO-SRG-002.md · docs/gates/HO-SRG-002-*.md
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**` | HO-SRG-001 |
| `.ai/**`, `.claude/**`, `docs/**` (except the two named paths) | Governing docs and inputs |
| The §8.5 components' source (HO-EMR-002, HO-PHM-002, HO-IMP-002) | Consumed, not built; a local copy is a §20.2 stop |
| Every other HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002 and HO-APT-002 file beyond §13's lines | Their areas |
| `web/package.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs` | No dependency or config change |

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| U-A1 | Resource picker: assistant list after choosing a surgeon | Surgeon absent from the assistant options |
| U-A2 | Availability with busy intervals | `BusyExplainer` lists each resource's busy times with labels |
| U-A3 | Booking `400` with `TheatreBusy`, `SurgeonBusy`, `AssistantBusy` | All three shown beside their fields and in the summary; focus on the summary — **US-SRG-01** |
| U-A4 | Booking `409` | Race message; availability refetched |
| U-A5 | `CatAlreadyNeutered` → *Book anyway* | Resubmits with `confirmNeuterStatusDiscrepancy: true` |
| U-A6 | Campaign chosen without an eligibility note | Field error; submit blocked |
| U-A7 | Price shown *"for information"*; no payment control anywhere | As stated — ADR-170 |
| U-B1 | Day list row with three outstanding items | *"3 outstanding: consent, weight, ASA grade"* as text — **US-SRG-02 AC2** |
| U-B2 | Checklist actions by role (nurse, vet, receptionist) | Only permitted actions rendered; *Waive* on blood work only, vets only |
| U-B3 | `StartPanel` with consent the only blocker, as nurse / as vet | Blocker listed, no override / override button shown — ADR-FE-072 |
| U-B4 | Override reason 9 chars | Submit disabled; counter shows the minimum |
| U-B5 | Start enabled only when `allowedTransitions` has `Start` and no blockers | As stated |
| U-B6 | Poll while the override dialog has text | Text kept |
| U-C1 | Timeline ordering with drug, vitals, milestone, correction | Ordered by `occurredOn`; corrected entry struck **and** labelled *Corrected* with a link — ADR-FE-070 |
| U-C2 | No edit or delete control exists on any entry | None in the DOM |
| U-C3 | Controlled drug dialog before the witness step | **Save** disabled; wastage required |
| U-C4 | Witness dialog submit, cancel and unmount | Password field value gone from state after each; never in the query cache or URL — ADR-FE-071 |
| U-C5 | Units on every dose, quantity, weight and vital, both in entry and display | Unit text present — UI-8 |
| U-C6 | `ImplausibleVitalValue` | Out-of-range field marked; *Save anyway* resubmits with the flag |
| U-C7 | `BatchNotUsable` / `WitnessConfirmationInvalid` | Mapped plain messages; the dialog keeps other values |
| U-D1 | Report autosave then `409` | Reload prompt; local text preserved for copying |
| U-D2 | Finalised report | No editor; read-only text + addenda + *Add addendum* for vets — ADR-FE-074 |
| U-D3 | `ReportIncomplete` | Missing fields listed; focus on the first |
| U-D4 | `neuterStatusApplied: false` | Reason and **Apply neuter status** shown |
| U-E1 | Discharge disabled until *Ready for discharge* | As stated |
| U-E2 | Follow-up date outside the window | Field error before submit (from the draft's window) and server message on `400` |
| U-E3 | Recovery board ordering and text | Ready → Recovering → Stable; status as text with since/by |
| U-E4 | Post-op sheet print view | Contains instructions as text, follow-ups, clinic contact; no navigation chrome |
| U-F1 | Owner detail before / during / after | Fasting and admit-by / OQ-3 text / instructions, follow-ups and *Book follow-up* |
| U-X1 | Every form: server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X2 | Dirty report or discharge form + navigate | Unsaved-changes confirm |
| U-X3 | Calendar surgery block | Not draggable; opens the surgery page |

## 16. End-to-end test cases

MSW first, then the real API; both runs recorded. Cases that touch the §8.5 components run against the
real ones when their handovers are built.

| ID | Case | Expected |
|---|---|---|
| E-A1 | Reception books a spay choosing surgeon, assistant and theatre | Surgery page opens; the block appears on the calendar for the theatre and the assistant |
| E-A2 | Book when the assistant is busy with a vaccination | Clash shown with the time; choose another start → booked |
| E-A3 | Two browser contexts book the same theatre and time | One succeeds; the other sees the race message |
| E-B1 | Day list shows outstanding items; record weight (cat page form) and consent; refresh | Items clear — **US-SRG-02** |
| E-B2 | Admit and try to start without consent | Blocked with the consent blocker — **US-SRG-03 AC1** |
| E-B3 | Vet starts with override and reason | *In theatre*; override banner with vet and reason — **US-SRG-03 AC2** |
| E-B4 | Receptionist opens a surgery page | No start, drug, report or override controls; direct API attempts `403` (real API) — AC-18 |
| E-C1 | Log premed, induction, vitals ×3, a controlled drug with a witness (second user types their credentials) | Timeline in order with units; controlled entry names vet and witness — **US-SRG-04, US-SRG-08** |
| E-C2 | Correct the controlled entry | Original struck with *Corrected*; correction linked; a new witness step was required |
| E-D1 | End a spay *Completed*; finalise the report; try to edit | Cat header shows *Neutered*; report read-only; addendum added — **US-SRG-05, US-SRG-06** |
| E-E1 | Set recovery statuses; front desk *Recovery* tab | Card updates on the next poll — **US-SRG-10** |
| E-E2 | Discharge with defaults; print sheet; owner signs in | Owner page shows instructions and two follow-ups with *Book follow-up*; post-op message in the inbox (real API) — **US-SRG-07** |
| E-F1 | Admin creates a campaign; reception books under it; open cases | Campaign price on the surgery; case in the list with totals — **US-SRG-11** |
| E-X1 | **AC-3** full spay flow through the UI (E-A1 → E-E2 in one run) | Completes; every step's result visible |
| E-X2 | axe on every route in §8.1, including open dialogs (override, drug with witness, correction, discharge) | Zero serious/critical |
| E-X3 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-SRG-001 §12.4; MSW handlers; `lib/format` unit helpers.
3. Book a surgery → day list → surgery header, reschedule, cancel; calendar link (§11.10).
4. Pre-op tab (checklist, ASA, admit, start, override) — consent dialog last, when HO-EMR-002 exists.
5. Report tab and neuter notice → Recovery tab and front-desk board → Discharge, post-op sheet, owner pages.
6. Protocols → campaigns and cases.
7. Anaesthesia tab (timeline, vitals, milestones) → drug dialog with batch and witness → corrections,
   when HO-PHM-002 and HO-IMP-002 exist.
8. U-series, E-series on MSW, then E-series against the real API per HO-SRG-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | SRG-01 | Reception books against theatre, surgeon and assistant; every clash shown with its reason; races explained | U-A1…U-A4, E-A1, E-A2, E-A3 |
| A2 | SRG-02 | The checklist is visible, actionable by role, and outstanding items show on the day list | U-B1, U-B2, E-B1 |
| A3 | SRG-03 | Start is blocked without consent; the vet override demands a reason and is visibly recorded | U-B3…U-B6, E-B2, E-B3 |
| A4 | SRG-04 | Drugs and vitals are entered with units and shown on one attributed timeline | U-C1, U-C5, U-C6, E-C1 |
| A5 | SRG-05 | The report is edited as a draft, finalised, then read-only with addenda | U-D1…U-D3, E-D1 |
| A6 | SRG-06 | Neuter status appears automatically after a completed spay or neuter; a repair action exists | U-D4, E-D1 |
| A7 | SRG-07 | Post-op instructions and follow-ups are produced, printable, and visible to the owner | U-E1, U-E2, U-E4, U-F1, E-E2 |
| A8 | SRG-08 | Controlled drugs need a witness; entries cannot be edited or deleted; corrections are linked | U-C2, U-C3, U-C4, U-C7, E-C1, E-C2 |
| A9 | SRG-09 | **Deferred to HO-BRD-002** (§4) | — |
| A10 | SRG-10 | Recovery status is visible on the front desk and refreshes every 30 s | U-E3, E-E1 |
| A11 | SRG-11 | Campaign pricing can be chosen at booking and cases are listed separately | U-A6, E-F1 |
| A12 | ADR-170 | No payment step exists | U-A7 |
| A13 | AC-3 / AC-18 | Spay end to end through the UI; non-vets see no clinical write controls | E-X1, E-B4 |
| A14 | UI-6 / UI-1 / UI-8 | Zero serious/critical axe violations; 360–1440 px; units always visible | E-X2, E-X3, U-C5 |

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering; no new dependency; no client-side clash, checklist or consent logic; §8.5 components consumed, not copied; the scoped edits to HO-APT-002's front desk and calendar match §13 |
| 2 | SECURITY | `security-reviewer` | Witness credential lifetime (U-C4); override only for vets and only via the server's blockers; no edit/delete controls on the log; all free text rendered as text; owner pages only through the customer endpoints |
| 3 | CODE_REVIEW | `code-reviewer` | Units through `lib/format`; polling never resets forms or report drafts; component size; actions only from `allowedTransitions` |
| 4 | TEST | `test-engineer` | `TC-HO-SRG-002.md` first; mock and real runs separate; E-C1 uses two real staff accounts for the witness |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A9 recorded as deferred, not passed |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002, HO-NTF-002,
HO-APT-002). Do not build a parallel shell, search, front desk or calendar.

**20.2 A §8.5 component is missing or differs** (HO-EMR-002 consent capture, HO-PHM-002 witness dialog
or medicine picker, HO-IMP-002 batch picker). Build the other screens; do not write a local
substitute, and never collect a witness password in a component of this slice.

**20.3 A screen seems to need client-side clash, checklist or consent logic.** Ask the server.

**20.4 The real API disagrees with HO-SRG-001 §12.4.** Report it; do not shim.

**20.5 A screen needs an endpoint HO-SRG-001 does not specify.** Report it; do not invent a mock field.

**20.6 An edit or delete control for a log entry, a finalised report or an addendum is requested.**
It contradicts US-SRG-08 AC2 and FR-SRG-5; report it.

**20.7 A payment or deposit step is requested** — HO-SRG-001 ADR-170.

**20.8 A new dependency seems necessary** (§10). Raise it.
