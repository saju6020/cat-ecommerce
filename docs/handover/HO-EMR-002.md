# HO-EMR-002 — Clinic Consultation and Medical Records (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-EMR-002** |
| Title | Clinic — Consultation and Medical Records — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §4 — **US-CLN-11 … US-CLN-22** |
| SRS | FR-CLN-12 … FR-CLN-24; FR-POR-3; **UI-1, UI-3, UI-5, UI-6, UI-7, UI-8**; NFR-PRF-4; NFR-USA-4, NFR-USA-5; NFR-SEC-5; AC-2, AC-18 |
| Pairs with | [`HO-EMR-001`](HO-EMR-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-EMR-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, `CatProfileHeader`, cat tabs, `recharts`), [`HO-ACC-002`](HO-ACC-002.md) (sessions, `client.ts`, `IdleLock`, `PermissionDeniedState`, `UnsavedChangesGuard`, error mapping), [`HO-ADM-002`](HO-ADM-002.md) (admin shell, `adminNavItems`, `lib/format`, `LimitedMarkup` renderer), [`HO-APT-002`](HO-APT-002.md) (front desk, queue, appointment detail, staff booking slot picker) |
| Delivery order | Section 8 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | *(none — append rows here)* |

---

## 2. Objective

Build the **consultation workspace** a vet writes the medical record in, the **quick vitals entry** a
nurse uses at check-in, the **clinical history** a vet reads without leaving the note, **consent
capture** for reception, the **clinical catalogues** for administrators and vets, and the owner's
**visit summaries and lab results** in the portal.

Four things make this harder than it looks:

1. **Nothing typed may be lost.** A consultation note is written over twenty minutes on a shared
   clinic PC. It auto-saves to the server's per-author working copy, restores on return, survives the
   idle lock, and is never written to browser storage (ADR-FE-050).
2. **Finalised means read-only, visibly.** After finalisation the workspace renders a record, not a
   disabled form, with addenda beneath it. What the user may do comes only from the server's
   `allowedActions` (ADR-FE-051).
3. **Numbers always carry units** (UI-8). A weight is "4.20 kg", never "4.2". One input and one
   formatter do this everywhere (ADR-FE-053).
4. **History must not cost the draft.** It opens in a drawer over the workspace, loads section by
   section, and renders a 10-year cat inside the 2-second budget (NFR-PRF-4, ADR-FE-049, ADR-FE-056).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack and layering (`app` → `components` → `lib/hooks` → `lib/api`); HO-ACC-002's
session model, `client.ts`, route groups, error mapping, `PermissionDeniedState`,
`UnsavedChangesGuard` and `IdleLock` (the page stays mounted under the lock; refused requests are never
replayed automatically); HO-ADM-002's admin shell, `lib/format`, `LimitedMarkup` renderer; HO-CAT-002's
`CatProfileHeader` (UI-7); HO-APT-002's front desk, `ClinicQueue`, `StaffAppointmentDetail` and the
staff slot picker.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-CLN-11 | **Vitals** — a dialog from the queue card and a panel in the workspace; soft-warning confirm flow | `/staff/front-desk` (HO-APT-002's queue card), `/staff/visits/[visitId]` |
| US-CLN-12, 20 | **Consultation workspace**: SOAP editor, coded problem list, home-care instructions, template pre-fill, auto-save and restore, explicit Save | `/staff/visits/[visitId]` |
| US-CLN-13 | **History drawer** in the workspace (timeline, visits, diagnoses, lab results, vaccinations, weight trend, photos, and "not yet available" panels); the same sections as a full page on the cat | `/staff/visits/[visitId]` (drawer), `/staff/cats/[catId]/history` |
| US-CLN-14 | Order dialog, pending list, result entry with live flags, attachments, result detail | workspace panel, `/staff/diagnostics`, `/staff/diagnostics/[orderId]` |
| US-CLN-15 | Treatments panel: record dialog with dispensary item search, FEFO batch default, override reason, shortfall confirm; reversal | workspace panel |
| US-CLN-16 | Discharge summary print view + PDF (staff); summary page + PDF (portal); portal visit list and lab results | `/staff/visits/[visitId]/summary`, `/portal/cats/[catId]/visits`, `/portal/visits/[visitId]` |
| US-CLN-17, 22 | **Finalise** dialog (vet only, from `allowedActions`); read-only record view; addendum form and list | `/staff/visits/[visitId]` |
| US-CLN-18 | Follow-up panel: *Book an appointment* (HO-APT-002's slot picker) or *Send a reminder* | workspace panel |
| US-CLN-19 | Photos panel with upload, date and note; photos on the timeline | workspace panel, history |
| US-CLN-21 | Consent capture: form text, signer, **signature pad** or signed-form upload; consent list; withdrawal | `/staff/consents/new`, `/staff/cats/[catId]/consents`, `/staff/consents/[consentId]` |
| — | Clinical catalogues: diagnosis codes, lab analytes, note templates | `/staff/admin/clinical/**` |
| — | Open visits list for the branch | `/staff/visits` |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-EMR-001 owns `src/**` |
| Prescribing, dispensing, labels | HO-PHM-002. The workspace shows **no** prescribe button |
| Surgery screens; "cannot start without consent" | HO-SRG-002 consumes `GET /api/staff/consents/status` |
| Prescriptions, boarding stays and food purchases panels with data | HO-PHM-002, HO-BRD-002, HO-ORD-002 — the drawer renders their "not yet available" state from the server (§11.4) |
| Invoice or payment anything | HO-BIL-002. No price-to-pay is shown on clinical screens |
| A DICOM viewer | DICOM files are downloaded, never rendered |
| The portal dashboard | HO-POR-002 |
| Rich-text or markdown in clinical text | ADR-FE-058 |
| A signature, rich-text, file-upload, virtual-list or charting library beyond `recharts` | §10 |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged, plus: **no clinical text in `localStorage`,
`sessionStorage`, IndexedDB or the Cache API** (ADR-FE-050); **no signed file URL stored, logged or put
in a query key**.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002, HO-ADM-002, HO-APT-002 built** | ❌ at baseline — §20.1 |
| HO-EMR-001 API | ❌ — build against MSW generated from §12.4 there, then re-run every E-case against the real API per HO-EMR-001 phase. Treatments (Phase F there) wait on HO-IMP-001 |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-EMR-001`](HO-EMR-001.md) | §12.4 endpoints and shapes; §10.4 codes and messages; §8.2–§8.4 visit rules and `allowedActions`; §8.3 vital-sign table; §8.5 flags; §8.6 treatment and stock responses; §8.7 history sections; §8.9 consent and signature shape; §8.10 file rules and signed URLs; §12.3 who may do what |
| [`HO-APT-002`](HO-APT-002.md) | `ClinicQueue`, `StaffAppointmentDetail`, the staff slot picker (`SlotStep` in staff mode), `useServerNow` |
| [`HO-ACC-002`](HO-ACC-002.md) | §11.2 client; §11.9 `IdleLock`; §11.14 error mapping |
| [`HO-CAT-002`](HO-CAT-002.md) | `CatProfileHeader` (full and compact), `[catId]` layout tabs, weight chart, date rules (§9) |
| [`HO-ADM-002`](HO-ADM-002.md) | admin shell; `lib/format`; `LimitedMarkup` renderer (consent form text) |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.1, §6.4 | UI-1/3/5/6/7/8; NFR-PRF-4; NFR-USA-4/5 |

---

## 7. Build rules

| Req | Rule | Where |
|---|---|---|
| **UI-1** | 360 px floor. The workspace is one column below 1024 px (panels stacked, history drawer full-screen); the tablet/desktop layout is note on the left, panels on the right. Consent capture works on a tablet in portrait | §11.1, §11.8 |
| **UI-3** | Confirm, stating what is affected: **finalise** (*"Finalise Mitu's visit? The note, problems, vitals, treatments and photos become permanent. Corrections will be added as addenda."*), reverse a treatment (*"Return 2 ml of Meloxicam batch L123 to the dispensary?"*), withdraw consent, discard restored draft changes | Each screen |
| **UI-5** | The open-visits list and the pending-diagnostics list refetch every 30 s without resetting any form (HO-APT-002 ADR-FE-027's rule) | §11.9 |
| **UI-6** | WCAG 2.1 AA. Result flags are **text plus icon**, not colour ("High ↑"); vitals warnings are announced; the auto-save status is announced only on failure; the signature pad has a non-drawing alternative (upload) and a labelled "Clear"; every drawer and dialog traps focus and returns it | §11 |
| **UI-7** | `CatProfileHeader` (compact) at the top of the workspace, the vitals dialog, the summary, the consent screen; full on the cat's history page (from the `[catId]` layout) | HO-CAT-002 |
| **UI-8** | Every weight, temperature, rate, score, quantity and dose is shown and entered with its unit, via `MeasurementInput` / `formatMeasurement`; a bare clinical number in the DOM is a test failure (U-X3) | ADR-FE-053 |
| **NFR-PRF-4** | History drawer summary rendered ≤ 2 s p95 for the 10-year fixture, measured by E-D2 | ADR-FE-056 |
| **NFR-USA-4/5** | Server messages in plain language; the note survives validation errors, `409`s, the idle lock and navigation | ADR-FE-050 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (staff)/staff/
    visits/page.tsx                              open visits at the branch                     —
    visits/[visitId]/page.tsx                    ⭐ workspace (open) / record (finalised)      US-CLN-11…22
    visits/[visitId]/summary/page.tsx            printable discharge summary                  US-CLN-16
    diagnostics/page.tsx                         pending orders                               US-CLN-14
    diagnostics/[orderId]/page.tsx               order detail, results, attachments           US-CLN-14
    consents/new/page.tsx                        consent capture (?catId&appointmentId&kind)  US-CLN-21
    consents/[consentId]/page.tsx                consent detail, withdraw                     US-CLN-21
    cats/[catId]/history/page.tsx                full clinical history                        US-CLN-13
    cats/[catId]/consents/page.tsx               consents for the cat                         US-CLN-21
    cats/[catId]/layout.tsx                      ⚠ two tab entries: History, Consents (HO-CAT-002)
    admin/clinical/diagnosis-codes/**            catalogue                                    US-CLN-13 (codes)
    admin/clinical/lab-analytes/**               catalogue                                    US-CLN-14
    admin/clinical/note-templates/**             catalogue                                    US-CLN-20
    front-desk/…                                 ⚠ queue card actions (HO-APT-002's ClinicQueue)
    appointments/[appointmentId]/…               ⚠ Visit and Consent links (HO-APT-002's detail)
  (staff)/layout.tsx                             ⚠ nav entries: Visits, Diagnostics
  (portal)/portal/
    cats/[catId]/visits/page.tsx                 finalised visits + lab results               US-CLN-16, FR-POR-3
    visits/[visitId]/page.tsx                    discharge summary + PDF                      US-CLN-16
    cats/[catId]/page.tsx                        ⚠ "Visits and results" link (HO-CAT-002)
```

### 8.2 API modules

`lib/api/visits.ts` (open, detail, open list, vitals, draft, note, finalise, addenda, photos,
follow-up, treatments, reverse, summary), `lib/api/dispensary.ts`, `lib/api/diagnostics.ts`,
`lib/api/clinical-history.ts`, `lib/api/consents.ts`, `lib/api/files.ts` (multipart upload — the one
place `client.ts` sends `FormData`), `lib/api/clinical-catalogue.ts`, `lib/api/my-visits.ts` (portal).

### 8.3 State

| Kind | Where |
|---|---|
| Visit | `['visit', visitId]` — the source of `status`, `version`, `allowedActions`, `draft`, `template` |
| Note form | react-hook-form, initialised once per visit load from (restored draft ‖ saved note ‖ template); **not** re-initialised by a refetch while dirty |
| Auto-save | `useConsultationAutosave` — debounce and flush state in a ref; status `idle \| saving \| saved(at) \| failed` |
| History | `['cat', catId, 'clinical-history']` (summary) and `['cat', catId, 'clinical-history', section, page]`; `staleTime` 60 s |
| Dispensary | `['dispensary', 'items', search]`, `['dispensary', skuId, 'batches']` — `staleTime: 0`, refetched after any stock `409` |
| Diagnostic order | `['diagnostic', orderId]`; pending list `['diagnostics', 'pending']` polled 30 s |
| Signed URLs | Only inside the fetched payload; a query holding them has `staleTime` 4 min (URLs live 5); an `<img>` error triggers one refetch |
| Consent capture | Local component state (strokes in a ref, never serialised until submit) |

Invalidation: any visit write invalidates `['visit', visitId]` and `['cat', catId, 'clinical-history']`;
vitals with a weight also invalidate `['cat', catId]` and `['cat', catId, 'weights']` (the header's
latest weight).

### 8.4 Formatting — `lib/format` additions

`formatMeasurement({ value, unit })` → `"4.20 kg"`, `"38.6 °C"`, `"180 bpm"`, `"5 /9"`; values are
rendered **as the server's string** (no float re-formatting). `formatFlag(flag)` → `"High ↑"`,
`"Low ↓"`, `"Abnormal"`, `"Normal"`, `"Not assessed"`. Instants via the branch zone
(`branchTimeZoneId`, HO-ADM-002 ADR-FE-014); `DateOnly` strings never become `Date` objects.

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing the series. **This handover owns ADR-FE-049…058** and
uses all ten.

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-049** | The consultation is **one route**; history, diagnostics detail and summaries open in a **drawer or dialog over it**, never a navigation; the note form stays mounted throughout | US-CLN-13 "without losing my draft" is guaranteed by structure, not by saving first |
| **ADR-FE-050** | **Auto-save to the server working copy** (`PUT …/draft`): 3 s after typing stops, at most every 20 s while typing, and on `visibilitychange`/route change with `fetch(…, { keepalive: true })`. On load, a newer draft is **restored automatically** with a notice and **Revert to saved note**. **Nothing is written to browser storage.** Explicit **Save note** writes the record. Under the idle lock auto-save pauses (requests fail) and resumes on the next edit after unlock — never a replay | US-CLN-12 AC2 on shared clinic PCs; HO-ACC-002's idle-lock rule |
| **ADR-FE-051** | **Actions come only from `allowedActions`**; a finalised visit renders as a read-only record component (text, not disabled inputs) with addenda beneath | A disabled form invites "enable it in dev tools"; the server is the authority (US-CLN-17, US-CLN-22) |
| **ADR-FE-052** | **Vitals confirm flow is server-driven**: the client shows the server's plausible ranges early (from `vital-sign-rules`), but saves by posting; a `400 VitalsOutsidePlausibleRange` opens a confirm listing each value with its range; *Confirm and save* re-posts with `confirmedOutOfRange` | US-CLN-11 AC2; the warning that counts is the one the server recorded |
| **ADR-FE-053** | **`MeasurementInput`** — numeric input with a permanent visible unit suffix (part of the accessible name: *"Weight, kilograms"*), precision from the rules, no spinner; **`formatMeasurement`** for display | UI-8 in one place |
| **ADR-FE-054** | **Signature pad is an in-repo canvas** with pointer events, capturing strokes as normalised `[x, y]` points (4 dp) sent as data; *Clear* and *Undo stroke*; the non-drawing alternative is **upload a signed paper form** | ADR-132 (strokes, not images); no library; UI-6 needs an alternative for a pointer-only control |
| **ADR-FE-055** | **`FileUploadField`** pre-checks type and size from a map mirroring HO-EMR-001 `FileRules`, uploads with progress, then posts the attach command with `fileId`; the server's refusal (`FileContentMismatch`, `FileRejected`, `FileScanUnavailable`) is shown as the message. Files are displayed only from signed URLs in payloads | NFR-SEC-5 is the server's; the client only saves a round trip |
| **ADR-FE-056** | **History loads summary first, sections on demand**: the drawer renders the summary (active problems, last visits, last vitals, weight trend, pending labs) from one call and fetches a section's first page only when its tab opens; "Load more" pages (no virtual-list library) | NFR-PRF-4 without a dependency |
| **ADR-FE-057** | **Printing** uses a print stylesheet on `/staff/visits/[visitId]/summary` for the browser path and the server's PDF for download and the portal; the client never generates a PDF | One source of truth for the summary (ADR-130) |
| **ADR-FE-058** | **Clinical text is plain text** — rendered with `white-space: pre-wrap`, never parsed as markdown or HTML; consent-form bodies (server `LimitedMarkup`) use HO-ADM-002's renderer | Notes are untrusted input displayed to other staff and owners |

| # | Question | Default |
|---|---|---|
| OQ-1 | Should the vet be able to type the note while the nurse's vitals entry is open in another tab? | Yes — separate forms; the workspace refetches vitals after the nurse saves (on focus) |
| OQ-2 | Keyboard shortcut for Save note | `Ctrl/Cmd+S` saves the note, never the browser page |
| OQ-3 | Show normal (not plausible) reference ranges next to vitals inputs? | No — only the server's plausible ranges; normal ranges await HO-EMR-001 OQ-3 |
| OQ-4 | Where does the vet start from — queue card or open-visits list? | Both; the queue card is primary |

---

## 10. Dependencies

**No new package.** `recharts` (HO-CAT-002) draws the weight trend. A signature, editor, upload,
virtual-list, PDF or date-picker library is a §20 stop.

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error / `403` states.

### 11.1 Workspace — US-CLN-12, 17, 20, 22 · `/staff/visits/[visitId]` (`components/visit/`)

| Component | Behaviour |
|---|---|
| ⭐ `VisitWorkspace` | Composes the header, note column and panels; decides open vs record view from `status` |
| `VisitHeaderBar` | Compact `CatProfileHeader`; service, appointment time (branch zone), opened by/at, status as text; **History** button (drawer); auto-save status (*"Draft saved 10:42"*, `aria-live="polite"` only for *"Couldn't save your draft — your text is still here"*); **Save note**; **Finalise** (only if `finalise` ∈ `allowedActions`) |
| ⭐ `SoapEditor` | Four labelled textareas (Subjective, Objective, Assessment, Plan) + **Home-care instructions**; character counters at the server limits; initial values per §8.3; `UnsavedChangesGuard` while dirty against the **saved note** (the working copy protects content, the guard protects intent) |
| `TemplatePrefillNotice` | When `template` is present and the form was initialised from it: *"Pre-filled from the {name} template. Edit freely."* — **US-CLN-20** |
| `RestoredDraftNotice` | When initialised from `draft`: *"Unsaved changes from 10:42 were restored."* + **Revert to saved note** (confirm) — **US-CLN-12 AC2** |
| ⭐ `ProblemListEditor` | Diagnosis-code combobox (`GET /api/staff/diagnosis-codes?search&activeOnly=true`, debounced, keyboard-navigable listbox), certainty radio (*Provisional* / *Confirmed*), note; up to 20; duplicate prevented in the UI and mapped from `DuplicateProblem` |
| `FinaliseDialog` | Checklist from the current form (≥ 1 problem, Assessment, Plan) with links to the missing field; warns *"2 diagnostic orders are still pending — results can be added after finalising"*; unsaved changes → saves first, then finalises with the new `version`; the UI-3 confirm text; `403` → `PermissionDeniedState` text inline. After success: record view and a **Complete appointment** button (HO-APT-001's endpoint, shown when the appointment is `InConsultation`) |
| ⭐ `VisitRecordView` | Finalised: every section as text (pre-wrap), vitals with units, problems, treatments, photos, follow-up, *"Finalised by Dr Rahman on Mon 12 Oct 2026, 11:04"*; **Addenda** below |
| `AddendumList` · `AddendumForm` | Timestamped, attributed list; form (text ≤ 4000, optional reason) shown only with `addAddendum`; submit confirm *"Addenda cannot be edited or removed."* — **US-CLN-17 AC2** |
| `useConsultationAutosave` | ADR-FE-050; sends the full form snapshot; skips when unchanged since the last success; exposes `flush()` used before finalise and on route leave |

`409` on **Save note** → *"Someone else saved this note since you opened it. Your text is kept below —
reload to see their version."* with **Reload** that keeps the local text in a side-by-side read-only
box for copying. No automatic merge.

### 11.2 Vitals — US-CLN-11 (`components/visit/vitals/`)

| Component | Behaviour |
|---|---|
| ⭐ `VitalsForm` | Six `MeasurementInput`s (weight kg, temperature °C, heart rate bpm, respiratory rate /min, body condition /9, pain /10), measured-at (defaults now, branch zone), note; at least one value; inline hint when a value is outside the plausible range from `vital-sign-rules` (*"Outside the usual range 35.0–41.5 °C — you'll be asked to confirm"*) |
| `VitalsConfirmDialog` | On `400 VitalsOutsidePlausibleRange`: lists each value with its range; **Confirm and save** re-posts with `confirmedOutOfRange`; **Correct values** returns focus to the first listed field. `VitalSignOutOfRange` is a field error, no confirm — **AC2** |
| `VitalsPanel` | Entries newest first, each sign with its unit and a *"confirmed outside usual range"* marker (text); **Record vitals** when allowed |
| `QuickVitalsDialog` | From the queue card: opens the visit if needed (`POST /api/staff/visits`, idempotent), shows compact header + `VitalsForm`; on success, the card shows *"Vitals recorded 09:12"* and the header's weight updates — nurse path in ≤ 6 fields and one Save |

### 11.3 Panels — US-CLN-14, 15, 18, 19 (`components/visit/panels/`)

| Component | Behaviour |
|---|---|
| `DiagnosticsPanel` | Orders for the visit with status and a flag summary (*"2 High, 1 Abnormal"*); **Order test** → `OrderDiagnosticDialog` (kind, description required for Imaging/Other, analytes multi-select from the catalogue) — **US-CLN-14 AC1**; each row opens `DiagnosticOrderDrawer` |
| ⭐ `ResultEntryForm` | Rows (analyte picker pre-fills name, unit and range, all editable; numeric or qualitative); a **preview flag** computed with the same rule as the server for immediate feedback, replaced by the server's flag on save; reported-on date; note; *Supersedes* selector when earlier sets exist — **AC2** |
| `ResultTable` | Value with unit, range, `formatFlag` text + icon; superseded sets collapsed with *"Superseded on …"* |
| `DiagnosticOrderDrawer` | Same content as `/staff/diagnostics/[orderId]`, over the workspace (ADR-FE-049); attachments list with *Open* (PDF/images inline in a new tab via signed URL) or *Download* (DICOM); `FileUploadField purpose="LabResult"` + result date + note — **AC3** |
| ⭐ `TreatmentsPanel` | Lines with kind, description, dose/route, quantity + unit, batch lot and expiry, *"Shortfall 2 ml recorded"*, *Reversed* state; **Record treatment** → `RecordTreatmentDialog`; **Reverse** with reason (UI-3 confirm) |
| ⭐ `RecordTreatmentDialog` | Kind; Procedure → optional service; Medicine/Consumable → `DispensaryItemSearch` (name, available quantity with unit, *Controlled — record in the register* disabled items); batch list FEFO with the default marked *"Use first (expires soonest)"*; choosing another reveals a required reason; expired batches are **not offered**; quantity (`MeasurementInput` in the stock unit), dose, route, billable toggle. `400 InsufficientStock` → inline *"Only 3 ml in the dispensary."* with **Record anyway and note the shortfall** (re-posts `acceptShortfall`) — **US-CLN-15 AC2**; `409` → batches refetched with *"Stock changed — check the batch and quantity."* |
| `PhotosPanel` | Thumbnails (signed URLs) with date and note; **Add photo** → `FileUploadField purpose="ClinicalPhoto"` + taken-on date + note → attach — **US-CLN-19** |
| ⭐ `FollowUpPanel` | Current follow-up (date, kind, appointment time). **Book an appointment**: embeds HO-APT-002's staff slot picker (service, date, vet) in a dialog, books through HO-APT-001, then sets the follow-up with the new id. **Send a reminder**: date picker (after today) → sets a reminder; text *"We'll remind {owner} 3 days before."* — **US-CLN-18**. **Remove** clears it |

### 11.4 History — US-CLN-13 (`components/clinical-history/`)

| Component | Behaviour |
|---|---|
| ⭐ `HistoryDrawer` | Right-side sheet (full screen < 1024 px); tabs from the summary's `sections[]` in server order; **Overview** tab first from the summary |
| `HistoryOverview` | Active problems, last 5 visits (open in a nested read-only `VisitRecordView` inside the drawer), last vitals with units, `WeightTrendChart` (recharts, with a table alternative for screen readers), vaccination status badge (HO-CAT-002's), pending lab orders |
| `HistorySection` | First page on tab open, **Load more**; each item type rendered by a small component (`VisitItem`, `ProblemItem`, `LabResultItem`, `PhotoItem`, `VaccinationItem`, `WeightItem`) |
| `SectionUnavailable` | For `available: false`: *"Prescriptions will appear here once the pharmacy module is live."* (text from the section key; owner never shown as a handover id) |
| `ClinicalHistoryPage` | `/staff/cats/[catId]/history` — the same sections as a page under the cat layout |

### 11.5 Summary and portal — US-CLN-16, FR-POR-3

| Component | Behaviour |
|---|---|
| `DischargeSummaryView` | Branch letterhead, cat, date, vet, diagnosis, treatments, medicines administered, home-care instructions, follow-up date, corrections (addenda); when `prescriptionsAvailable` is false, the server's sentence about labels. Print stylesheet hides chrome (ADR-FE-057); **Download PDF** uses the server PDF |
| `MyCatVisits` | `/portal/cats/[catId]/visits`: finalised visits (date, service, vet, diagnoses) linking to the summary; **Lab results** section with values, units, ranges and flags as text; attachments *Open*/*Download* |
| `MyVisitSummary` | `/portal/visits/[visitId]` — `DischargeSummaryView` + PDF; `404` → *"We couldn't find this visit summary."* (no hint whether it exists) |

### 11.6 Consent — US-CLN-21 (`components/consent/`)

| Component | Behaviour |
|---|---|
| ⭐ `ConsentCaptureForm` | From the appointment detail or cat page with `?catId&appointmentId&kind`; compact header; the current consent-form text (`LimitedMarkup` renderer) with its version shown; procedure description; signer name and relationship (Owner / Authorised agent; owner name pre-filled from the customer); witness (optional staff picker); method tabs **Sign on screen** / **Upload signed form**; submit confirm. `404` on the form → *"The {kind} consent form hasn't been published yet. Ask an administrator."* |
| ⭐ `SignaturePad` | ADR-FE-054: canvas sized to the container, `touch-action: none`, strokes captured in a ref, *Undo stroke*, *Clear*, a visible "Sign here" line; minimum 10 points client-side; `aria-describedby` explaining the upload alternative |
| `ConsentList` | Per cat: kind, procedure, date, signer, status as text; **View** |
| `ConsentDetail` | Text version signed, signature (SVG image from the server URL) or the signed file; **Withdraw** with reason (confirm) |

### 11.7 Files — `components/files/FileUploadField.tsx`

ADR-FE-055: native `<input type="file">` with a labelled button, `accept` from the purpose map, size
check, progress bar with text percentage, cancel; after upload, a chip with the sanitised name and
size; server errors mapped (`FileScanUnavailable` → *"File uploads aren't available yet on this
system. Record the result values instead, or keep the paper copy."*).

### 11.8 Catalogues — `/staff/admin/clinical/**` (`components/clinical-catalogue/`)

Lists with search, active filter and paging (UI-4 minus CSV, which HO-EMR-001 does not specify);
editors: `DiagnosisCodeForm` (code upper-case, name, category, external system/code; the system code
shown read-only), `LabAnalyteForm` (kind, unit, low ≤ high, normal text), `NoteTemplateForm` (service
select — only services without an active template, five textareas, version shown). Entries in
`adminNavItems` for Vet and Administrator.

### 11.9 Lists and integrations

| Component | Behaviour |
|---|---|
| `OpenVisitsTable` | `/staff/visits`: cat, service, opened, vitals ✓/—, note ✓/— (text), high-risk icon with text; polls 30 s |
| `PendingDiagnosticsTable` | `/staff/diagnostics`: cat, kind, ordered, ordered by; polls 30 s |
| Queue card actions (HO-APT-002 `ClinicQueue`) | **Record vitals** (nurse, vet) → `QuickVitalsDialog`; **Open consultation** (vet) → starts the appointment through HO-APT-002's existing Start flow if still `CheckedIn`, opens the visit, navigates to the workspace |
| Appointment detail (HO-APT-002) | **Visit** link when one exists; **Record consent** link for theatre appointments |

### 11.10 Error mapping — `lib/api/errors.ts`

Append the HO-EMR-001 §10.4 codes used here with the §11 texts (`VisitFinalised`,
`VitalsOutsidePlausibleRange`, `VitalSignOutOfRange`, `ProblemRequired`, `AssessmentAndPlanRequired`,
`DuplicateProblem`, `DiagnosisCodeInactive`, `AddendumRequiresFinalisedVisit`, `BatchExpired`,
`BatchOverrideReasonRequired`, `InsufficientStock`, `ControlledDrugNotSupportedHere`,
`FollowUpDateInPast`, `FollowUpAppointmentInvalid`, `ConsentFormVersionNotCurrent`, `InvalidSignature`,
`FileTooLarge`, `FileTypeNotAllowed`, `FileContentMismatch`, `FileRejected`, `FileScanUnavailable`,
`VisitNotFinalised`, `VisitInProgress`, `VisitBelongsToAnotherBranch`). The server's message wins when
present.

---

## 12. Mocks — `web/mocks/handlers/visits/*.ts`

Generated from HO-EMR-001 §12.4. Fixtures: a visit in each status with a nurse, vet and administrator
`allowedActions` variant; a draft newer than the note and one older; a service with a template; vitals
responses for plausible-warning (two signs), absolute refusal and success; a `409` note save; finalise
refusals for each rule and `403`; addenda; diagnostic orders pending/resulted/superseded with every
flag; a DICOM and a PDF attachment; upload refusals for each code incl. `FileScanUnavailable`;
dispensary items incl. a controlled one; batches with FEFO default; `InsufficientStock` then
shortfall success; stock `409`; follow-up both kinds; **a 10-year history fixture** (60 visits, 250
weights, 80 lab orders) with the three unavailable sections; consent form published/unpublished;
consent recorded, withdrawn; portal visits and lab results; signed URLs that expire after 20 s (to
exercise the refetch).

---

## 13. Allowed scope

```
web/app/(staff)/staff/visits/**
web/app/(staff)/staff/diagnostics/**
web/app/(staff)/staff/consents/**
web/app/(staff)/staff/cats/[catId]/history/**
web/app/(staff)/staff/cats/[catId]/consents/**
web/app/(staff)/staff/cats/[catId]/layout.tsx              ← two tab entries only (HO-CAT-002)
web/app/(staff)/staff/admin/clinical/**
web/app/(staff)/layout.tsx                                  ← two nav entries only
web/app/(portal)/portal/cats/[catId]/visits/**
web/app/(portal)/portal/visits/**
web/app/(portal)/portal/cats/[catId]/page.tsx               ← one link only (HO-CAT-002)
web/components/{visit,clinical-history,consent,files,clinical-catalogue}/**
web/components/front-desk/ClinicQueue*.tsx                  ← the two card actions only (HO-APT-002)
web/components/appointments/StaffAppointmentDetail*.tsx     ← the two links only (HO-APT-002)
web/components/admin/adminNavItems.ts                       ← three entries only (HO-ADM-002)
web/lib/api/{visits,dispensary,diagnostics,clinical-history,consents,files,clinical-catalogue,my-visits}.ts
web/lib/api/client.ts                                       ← FormData upload support only
web/lib/api/types.ts · web/lib/api/errors.ts                ← append
web/lib/format/**                                           ← §8.4 additions
web/lib/hooks/**                                            ← new hooks only (incl. useConsultationAutosave)
web/lib/validation/**
web/mocks/handlers/visits/** · web/mocks/handlers/index.ts
web/tests/components/{visit,clinical-history,consent,files,clinical-catalogue}/**
web/tests/e2e/emr-*.spec.ts
web/docs/decisions.md                                       ← ADR-FE-049…058
docs/test-cases/TC-HO-EMR-002.md · docs/gates/HO-EMR-002-*.md
```

## 14. Forbidden scope

`src/**`, `tests/**`, `.ai/**`, `.claude/**`, `docs/**` (except the two named paths), every other
HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002 and HO-APT-002 file beyond the lines in §13,
`web/package.json`, `package-lock.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs`.

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| U-A1 | Workspace, open visit, vet `allowedActions` | Editor, panels, Save note, Finalise |
| U-A2 | Same visit, nurse | Vitals panel writable; note read-only text; no Finalise |
| U-A3 | Same visit, administrator | Read-only everywhere; no Finalise — **US-CLN-22** |
| U-A4 | Finalised visit | `VisitRecordView` (no inputs in the DOM for the note); addendum form only with `addAddendum` — **US-CLN-17** |
| U-A5 | Load with a newer draft; with an older draft | Restored with notice + Revert; saved note shown, no notice — **US-CLN-12 AC2** |
| U-A6 | Load with a template and no saved note | Pre-filled, editable, notice shown — **US-CLN-20** |
| U-A7 | Auto-save timing (fake timers): type, pause 3 s; type continuously 25 s; hide the tab | One `PUT`; `PUT` at ≤ 20 s intervals; a `keepalive` `PUT` on hide |
| U-A8 | Auto-save failure; idle-lock `session_idle_timeout` | Failure announced once, text kept; lock shown, no retry until the next edit after unlock |
| U-A9 | `localStorage`/`sessionStorage`/IndexedDB spies across a full editing session | Never written with note content |
| U-A10 | Save note `409` | Message; local text kept in the comparison box |
| U-A11 | Finalise with no problem / blank Plan | Checklist blocks, links focus the field; a server `ProblemRequired` also mapped |
| U-A12 | Finalise with unsaved changes | Note saved first, finalise sent with the new version |
| U-A13 | Addendum submit | Confirm, then listed with author and time |
| U-B1 | `MeasurementInput` accessible name and suffix | *"Weight, kilograms"*, "kg" visible — UI-8 |
| U-B2 | Vitals `400 VitalsOutsidePlausibleRange` (two signs) | Confirm lists both with ranges; Confirm re-posts with both — **US-CLN-11 AC2** |
| U-B3 | `VitalSignOutOfRange` | Field error; no confirm offered |
| U-B4 | Vitals with weight success | Header weight query invalidated |
| U-C1 | Result entry preview flags; server response differs | Preview shown, replaced by the server's flag |
| U-C2 | Flags rendered | Text + icon, never colour alone — UI-6 |
| U-C3 | DICOM attachment | *Download*, no inline open |
| U-C4 | `FileUploadField` wrong type / oversize client-side; `FileScanUnavailable` from server | Refused before upload; server message shown |
| U-D1 | Treatment dialog | FEFO batch preselected; choosing another requires a reason; no expired batch in the list; controlled item disabled with text |
| U-D2 | `InsufficientStock` | Available quantity with unit; **Record anyway** re-posts `acceptShortfall` — **US-CLN-15 AC2** |
| U-D3 | Stock `409` | Batches refetched, message shown |
| U-D4 | Reverse confirm text | Names quantity, unit, item and lot |
| U-E1 | Follow-up reminder; appointment path | Reminder set with explanatory text; slot picker books then sets the follow-up with the returned id |
| U-F1 | History drawer open/close while the note is dirty | Note value unchanged; no navigation occurred — **US-CLN-13** |
| U-F2 | Unavailable section | `SectionUnavailable` text, no fetch of a page |
| U-F3 | Weight trend | Chart plus a table alternative |
| U-G1 | Summary with `prescriptionsAvailable: false` | Server sentence rendered |
| U-G2 | Portal summary `404` | Neutral message |
| U-H1 | `SignaturePad` stroke capture | Points normalised to [0,1], 4 dp; Undo removes the last stroke; < 10 points blocks submit |
| U-H2 | Consent form unpublished | Message; submit absent |
| U-H3 | Consent form body | Rendered by `LimitedMarkup`; no HTML injection from a crafted body |
| U-X1 | Every form: server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X2 | SOAP text containing `<script>` and `**bold**` | Rendered literally — ADR-FE-058 |
| U-X3 | Scan rendered clinical screens in tests for a number adjacent to a measurement label without a unit | None — UI-8 |
| U-X4 | Signed URLs | Not present in any query key, log or storage |

## 16. End-to-end test cases

MSW first, then the real API; both runs recorded.

| ID | Case | Expected |
|---|---|---|
| E-A1 | **AC-2**: nurse records vitals from the queue (one warning confirmed); vet opens the consultation, writes SOAP with a problem, orders blood work, records results, records a consumable, sets a reminder follow-up, finalises, completes the appointment | Visit finalised; summary available; consumable deducted (real API); appointment completed |
| E-A2 | Vet types, closes the tab, reopens the visit | Text restored — **US-CLN-12 AC2** |
| E-A3 | Idle lock while typing; sign in again | Text still on screen; next edit auto-saves |
| E-A4 | Receptionist and administrator open the visit URL and try finalise via the UI | Receptionist `PermissionDeniedState`; administrator sees no Finalise — AC-18 |
| E-A5 | After finalising, add an addendum | Original text unchanged, addendum listed |
| E-B1 | Vet opens history from the workspace, reads three sections, closes it | Draft untouched — **US-CLN-13** |
| E-D2 | **10-year fixture**: open the history drawer 20 times | Overview rendered ≤ 2 s p95 — NFR-PRF-4 |
| E-C1 | Upload a lab PDF and open it | Opens in a new tab from a signed URL (real API, Development/Testing scanner) |
| E-C2 | Record treatment with an overridden batch (reason) and a shortfall | Line shows batch and shortfall |
| E-G1 | Owner signs in, opens the cat's visits, opens the summary, downloads the PDF | Summary shown; PDF downloaded — **US-CLN-16**, FR-POR-3 |
| E-G2 | Owner opens a visit that is still open (URL guess) | Neutral not-found |
| E-H1 | Receptionist captures on-screen consent on a tablet viewport (768×1024, touch emulation) | Consent listed with date, signer and form version — **US-CLN-21** |
| E-H2 | Consent by uploaded signed form | Listed; file opens |
| E-X1 | axe on every route in §8.1, incl. the workspace with drawer open, vitals confirm, finalise dialog, signature screen | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-EMR-001 §12.4; MSW handlers; `lib/format` additions; `MeasurementInput`.
3. Workspace shell, record view, SOAP editor, problems, template and draft notices, auto-save, finalise, addenda (HO-EMR-001 Phases B–C).
4. Vitals form, confirm flow, quick vitals from the queue.
5. `FileUploadField`, diagnostics, photos (Phase D).
6. History drawer and page (Phase E).
7. Summary view, portal pages, follow-up (Phase G); consent capture and signature pad (Phase H).
8. Treatments panel (Phase F — last, after HO-IMP-001).
9. Catalogues, lists, queue/appointment integrations.
10. U-series; E-series on MSW; E-series against the real API per HO-EMR-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | CLN-11 | Vitals with units; plausible-range warning confirmed before saving | U-B1…U-B4, E-A1 |
| A2 | CLN-12 | SOAP + coded problems saved; auto-saved draft restored on return, never in browser storage | U-A5, U-A7…U-A10, E-A2, E-A3 |
| A3 | CLN-13 | History reachable without losing the draft; 10-year cat ≤ 2 s p95 — *prescriptions, boarding, food shown as not yet available (HO-EMR-001 §4.2)* | U-F1…U-F3, E-B1, E-D2 |
| A4 | CLN-14 | Order, results with text flags, attachments | U-C1…U-C4, E-A1, E-C1 |
| A5 | CLN-15 | Batch shown and deducted; shortfall recordable | U-D1…U-D4, E-C2 |
| A6 | CLN-16 | Printable summary and portal summary with PDF | U-G1, U-G2, E-G1, E-G2 |
| A7 | CLN-17 | Finalised visit is read-only; addenda added and listed | U-A4, U-A13, E-A5 |
| A8 | CLN-18 | Follow-up by appointment or reminder from the workspace | U-E1, E-A1 |
| A9 | CLN-19 | Dated, noted photos attached and on the timeline | U-C4, E-C1 |
| A10 | CLN-20 | Template pre-fills and stays editable | U-A6 |
| A11 | CLN-21 | On-screen or uploaded consent captured with form version | U-H1…U-H3, E-H1, E-H2 |
| A12 | CLN-22 / AC-18 | Only vets see and can use Finalise; others get the server's refusal | U-A2, U-A3, E-A4 |
| A13 | UI-8 | No clinical number without its unit | U-B1, U-X3 |
| A14 | UI-6 / UI-1 | Zero serious/critical axe violations; 360–1440 px | E-X1, E-X2 |

---

## 19. Required gates

| # | Gate | Focus |
|---|---|---|
| 1 | ARCHITECTURE | §8 layering; one-route workspace (ADR-FE-049); actions only from `allowedActions`; no new dependency; edits to HO-CAT-002, HO-APT-002, HO-ADM-002 files and the layouts match §13 exactly |
| 2 | SECURITY | No clinical text in browser storage (U-A9); plain-text rendering (U-X2); signed URLs never stored or logged (U-X4); consent body only through `LimitedMarkup`; uploads only via the server's validation; portal `404`s neutral |
| 3 | CODE_REVIEW | Auto-save hook correctness (debounce, flush, keepalive, no replay); units through `formatMeasurement`; component size; loading/empty/error states |
| 4 | TEST | `TC-HO-EMR-002.md` first; mock and real runs separate; E-D2 measures time on the 10-year fixture |
| 5 | ACCEPTANCE | Every §18 row; A3's unavailable panels recorded as deferred, not passed |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002, HO-APT-002).
Do not build a parallel shell, header, queue or slot picker.

**20.2 A screen seems to need client-side rules the server owns** — finalisation readiness beyond the
checklist hint, stock availability, batch eligibility, consent validity. Ask the server.

**20.3 Someone asks to keep drafts in `localStorage` "for offline".** Refuse — ADR-FE-050; offline is
HO-XCT-002's.

**20.4 The real API disagrees with HO-EMR-001 §12.4.** Report it; do not shim.

**20.5 A screen needs an endpoint HO-EMR-001 does not specify** (e.g. prescriptions in the drawer).
Report it; do not invent a mock field.

**20.6 A new dependency seems necessary** (signature, editor, upload, virtual list, PDF). Raise it.

**20.7 Uploads fail with `FileScanUnavailable` in a shared environment.** That is the specified
behaviour until a scanner is chosen (HO-EMR-001 OQ-6) — show the message, do not work around it.
