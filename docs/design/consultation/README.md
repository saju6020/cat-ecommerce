# Consultation & medical records — UX

The clickable design for backlog §4 *Clinic — Consultation and Medical Records* (US-CLN-11 … 22),
built against [`HO-EMR-002`](../../handover/HO-EMR-002.md), the frontend handover. Fifth area, after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/consultation/index.html (served from docs/design)

There is no hosted canvas for this area. The `index.html` in this folder lists every artboard with
a line on what it shows.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each one loads `./support.js` (a
copy of `../_shared/support.js`), which renders it in a browser and swaps the placeholder
`.topnav` for the shared staff sidebar and header. Portal screens keep their own header.

## The screens

Nine rows, twenty-eight artboards, more than the 9–22 of the first four areas. HO-EMR-002 covers twelve
stories, eleven routes, the owner portal and three catalogues, and nearly every story has a
refusal or confirmation that needs its own artboard.

| Row | File | Screen | Shows |
|---|---|---|---|
| Visits & vitals — US-CLN-11 | `OpenVisits.dc.html` | Open visits | `/staff/visits`: vitals/note as tick-or-dash **plus a word**, high risk as icon and text, 30 s refresh, empty state |
| | `QuickVitals.dc.html` | Quick vitals | ⭐ `QuickVitalsDialog` over the queue: compact header, six `MeasurementInput`s, the early plausible-range hint, and a card reading "Vitals recorded 09:05" |
| | `VitalsConfirm.dc.html` | Confirm / refuse / record | `VitalsConfirmDialog` listing each value with its range; `VitalSignOutOfRange` as a field error with no confirm; the saved entry's "confirmed outside usual range" marker |
| Consultation workspace — US-CLN-12, 20, 22 | `Workspace.dc.html` | Workspace | ⭐ note left, panels right; auto-save status, History, Save note, Finalise; `TemplatePrefillNotice`; problems with certainty |
| | `DraftRecovery.dc.html` | Restored draft | `RestoredDraftNotice` and the Revert confirmation naming the time range and fields removed |
| | `SaveConflict.dc.html` | Save conflict | The `409` message, their note beside your read-only text, and the four auto-save states |
| | `ProblemList.dc.html` | Problem list | Diagnosis-code listbox (duplicate disabled in place), certainty, and each refusal: duplicate, retired code, limit of 20, no match |
| | `WorkspaceRoles.dc.html` | Other roles | Nurse, administrator and reception on the same visit. Buttons exist only when `allowedActions` has them |
| | `WorkspaceMobile360.dc.html` | 360 px | One column, panels stacked, Save note / Finalise in a bottom bar (UI-1) |
| Finalise & addenda — US-CLN-17, 22 | `FinaliseDialog.dc.html` | Finalise | ⭐ blocked checklist (disabled button with its reason) beside the ready state with the UI-3 sentence |
| | `VisitRecord.dc.html` | Finalised record | ⭐ `VisitRecordView` as text, "Finalised by … (Asia/Dhaka)", Complete appointment, addenda list, form and confirm |
| Clinical history — US-CLN-13 | `HistoryDrawer.dc.html` | History drawer | ⭐ sheet over the still-mounted note; Overview incl. weight chart **and** its table alternative |
| | `ClinicalHistoryPage.dc.html` | Cat → History | Full cat header with the two new tabs; lab results; Load more; `SectionUnavailable` |
| Diagnostics — US-CLN-14 | `OrderDiagnostic.dc.html` | Order a test | Laboratory with analytes, beside Imaging with the required description refused |
| | `PendingDiagnostics.dc.html` | Pending diagnostics | `/staff/diagnostics`, 30 s refresh, empty state |
| | `ResultEntry.dc.html` | Enter results | ⭐ numeric and qualitative rows, preview flags of every kind, Supersedes |
| | `DiagnosticOrderDetail.dc.html` | Order detail | Server flags, superseded set collapsed, PDF *Open* vs DICOM *Download*, every `FileUploadField` state |
| Treatments, photos & follow-up — US-CLN-15, 18, 19 | `RecordTreatment.dc.html` | Record treatment | ⭐ dispensary search (controlled item disabled with its reason), FEFO default, required override reason |
| | `TreatmentStates.dc.html` | Stock refusals | `InsufficientStock` + Record anyway, stock `409` refetch, shortfall/Reversed lines, reverse confirm |
| | `PhotosAndFollowUp.dc.html` | Photos & follow-up | ⭐ dated/noted photos, both follow-up kinds, the reminder refusal, the slot picker in a dialog |
| Summary & portal — US-CLN-16 | `DischargeSummary.dc.html` | Staff print view | Letterhead, Print / Download PDF, corrections, the prescription-labels sentence |
| | `PortalVisits.dc.html` | Portal visits & results | Owner's finalised visits; lab results with units, ranges, text flags; attachments |
| | `PortalVisitSummary.dc.html` | Portal summary | Summary + PDF beside the neutral not-found |
| Consent — US-CLN-21 | `ConsentCapture.dc.html` | Sign on screen | ⭐ form text and version, signer, witness, `SignaturePad` with Sign here / Undo stroke / Clear and the upload alternative |
| | `ConsentStates.dc.html` | Other states | Upload tab, signature too short, submit confirm, unpublished form, newer version |
| | `ConsentListAndDetail.dc.html` | Cat → Consents | List, detail with stored signature, withdraw confirm |
| Clinical catalogues | `DiagnosisCodes.dc.html` | Diagnosis codes | Search, status filter, paging, editor, code rule |
| | `CatalogueEditors.dc.html` | Analytes & templates | Low ≤ high refusal; only services without an active template |

## The design system

Built on the [PurrCare design system](https://claude.ai/artifact/Y2WzXo2hnfc7a9ac7GHNvN), using its
existing pieces: `Table`, `Tabs`, `Dialog`, `Badge`, `Banner`, `Field`, `Toggle`. The tokens are
typed into each artboard by hand, as in the other areas. This area adds four patterns. Each is
drawn once and reused:

- **Measurement input.** A numeric field with a permanent unit suffix ("kg", "°C", "bpm", "/min",
  "/9", "/10", "ml", "mg/kg", "µmol/L"). The unit is part of the field's accessible name.
- **Result flag.** A pill with an icon and a word: *High ↑*, *Low ↓*, *Abnormal*, *Normal*,
  *Not assessed*. It never relies on colour alone.
- **Compact cat header.** The high-risk strip, allergy chips and chronic conditions from
  `CatOverview.dc.html`, condensed. Dialogs that cover the header repeat the allergy chips inside
  the dialog.
- **Side sheet.** The history drawer. It is the `Dialog` pattern anchored to the right instead of
  centred.

## Rules the design encodes

1. **The cat's warnings are always visible.** Every staff screen about Mittens shows the high-risk
   strip and the allergy chips, and dialogs over the header repeat the allergies. Portal screens
   show the allergies but not the high-risk handling note, which is written for staff.
2. **Every clinical number carries its unit** (UI-8): "4.20 kg", "38.6 °C", "240 µmol/L",
   "0.4 ml". This applies to inputs, ranges and the summary. Weights use the handover's two
   decimals.
3. **A finalised visit is a record, not a disabled form** (ADR-FE-051). `VisitRecord.dc.html` has
   no inputs for the note. Corrections are addenda, and every confirmation states that.
4. **Buttons come from the server.** `WorkspaceRoles.dc.html` leaves Finalise out for the nurse
   and the administrator; it is not greyed out. Reception gets the permission-denied state. Where
   the design does show a disabled control (Finalise with a blank Plan, the problem limit, a
   controlled drug, a short signature), a sentence next to it says why.
5. **Nothing typed is lost.** Restored drafts say where the text came from (the server working
   copy, not the browser). A save conflict keeps your text beside theirs. Auto-save announces only
   its failure.
6. **Every confirmation names its consequence.** Finalise, revert a draft, add an addendum,
   reverse a treatment (quantity, unit, item, lot), withdraw consent and record consent all do. None
   is a bare "Are you sure?".
7. **Every time names its zone.** Times read "10:42 (Asia/Dhaka)", or the column header names the
   zone. Dates that are only dates (reported-on, taken-on) carry no time.
8. **"None recorded" is written out** for an empty handling note, external code, or medicines
   given. It is never left blank.
9. **No price, no payment.** Treatments have a *Billable* toggle only. Summaries show no amounts.
   There is no payment gateway in v1.

## Handover ambiguities resolved

- **Two confirmations for Finalise.** The handover asks for a checklist and a UI-3 confirmation.
  Both are drawn as one dialog in two states (blocked and ready), not as two dialogs one after the
  other.
- **History page layout.** HO-EMR-002 §11.4 says "the same sections as a page". The page uses a
  left section list instead of tabs, because a full page has room for one.
- **`DiagnosticOrderDrawer` vs `/staff/diagnostics/[orderId]`.** These have identical content, so
  one artboard (`DiagnosticOrderDetail`) stands for both.
- **"System code shown read-only"** (§11.8) is read as *the code is locked once created*, with the
  reason and the way to replace it (deactivate and add a new one).
- **Plausible-range examples.** The handover gives only the temperature range. The heart-rate
  range (100–260 bpm) and the weight limits (0.05–30.00 kg) are placeholders. The server's
  `vital-sign-rules` will provide the real values.
- **The second vet** in `SaveConflict` (Dr. Farzana Haque) is the only new name. A conflict needs
  a second author.

## Known gaps, deliberately visible

- **No live interaction.** Every state is its own artboard or a labelled panel, as in the earlier
  areas.
- **Charts are static SVG** standing in for `recharts`. The table alternative is drawn next to
  the chart, not behind a toggle.
- **Prescriptions, boarding and food purchases** appear only as "not yet available". Their
  modules (HO-PHM/BRD/ORD-002) aren't designed yet.
- **The queue card and the appointment-detail links** (HO-APT-002) are drawn only as far as
  `QuickVitals` needs. The Appointments area isn't designed yet.
- **Dates are plain text inputs.** HO-EMR-002 §10 forbids a date-picker library.
- **Data is illustrative.** No backend for this area exists yet.
