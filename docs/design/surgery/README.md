# Surgery — UX

The clickable design for backlog §5 *Surgery: Spay, Neuter and Procedures*, built against
[`HO-SRG-002`](../../handover/HO-SRG-002.md), the frontend handover. Built after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/surgery/index.html (served from docs/design)

No hosted canvas exists for this area yet.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each one loads `./support.js`,
the local runtime (a byte-for-byte copy of `../_shared/support.js`). At render time it swaps each
staff screen's `.topnav` for the shared sidebar and header. Customer screens and the print sheet
are left as designed.

## The screens

Seven rows, twenty-six artboards. That is the largest area so far, and it matches the handover:
ten stories (US-SRG-09 is deferred), and one surgery page that holds booking, pre-op, theatre,
report, recovery and discharge.

| Row | File | Screen | Shows |
|---|---|---|---|
| Booking & day list — US-SRG-01, 02, 11 | `TheatreDayList.dc.html` | Theatre day list | ⭐ Grouped by theatre. Checklist as text and icon ("3 outstanding: consent, weight, ASA grade"). High-risk and deceased flags as words. "Needs rescheduling" badges |
| | `BookSurgery.dc.html` | Book a surgery | ⭐ Cat header once the cat is chosen. Price "for information — paid at the clinic". The three-resource picker, starts as buttons, the `BusyExplainer`, campaign price and eligibility note |
| | `BookSurgeryClashes.dc.html` | Booking clashes | A `400` with three clashes shown at once, in a summary and beside the theatre, surgeon and assistant fields |
| | `BookSurgeryStates.dc.html` | Booking states | Six panels: the confirm, already neutered with "Book anyway", sex mismatch, no common start plus the next free day, the `409` race, and a campaign with no note |
| Surgery page, pre-op — US-SRG-01, 02, 03 | `SurgeryPreOp.dc.html` | Pre-op (nurse) | ⭐ The surgery header, the five-row checklist, admission, and Start disabled with its blockers as links |
| | `SurgeryPreOpVet.dc.html` | Pre-op (vet) | Consent is the only blocker, so the vet sees "Start with emergency override". Also the ASA control, a waived row, and what nurse and reception see instead |
| | `ConsentOverride.dc.html` | Override dialog | ⭐ Two states. A 9-character reason with Continue disabled. The final confirm, which names the vet and says the override is permanent |
| | `RescheduleCancel.dc.html` | Reschedule & cancel | The prefilled picker with an old → new confirm. The cancel dialog with a required reason |
| Anaesthesia — US-SRG-04, 08 | `AnaesthesiaLog.dc.html` | Anaesthesia log | ⭐ An append-only ordered list. No edit or delete anywhere; the only action is "Correct…". A corrected entry is struck through **and** says "Corrected", with a link to its correction |
| | `DrugEntry.dc.html` | Drug + witness | ⭐ For a controlled drug, Save stays disabled until the witness step returns. The witness dialog shows "Witness identifier" and "Witness password" and the expired-confirmation error. Also `BatchNotUsable` |
| | `VitalsEntry.dc.html` | Vitals & milestone | Units on every field. `ImplausibleVitalValue` marks the field and offers "Save anyway" |
| | `CorrectEntry.dc.html` | Correct an entry | The original read-only, a reason of 10–500 characters, a new witness step, and "The original entry stays in the log" |
| Report — US-SRG-05, 06 | `EndProcedure.dc.html` | End procedure | Completed or Abandoned. The confirm says a spay sets the neuter status |
| | `ReportDraft.dc.html` | Report draft | ⭐ Autosave status and `ReportIncomplete` with a summary. The finalise confirm. The `409` conflict that keeps the local text for copying |
| | `ReportFinal.dc.html` | Finalised report | Read-only text and addenda, and the neuter-status notice. When the status wasn't applied, it gives the reason and an "Apply neuter status" button |
| Recovery & discharge — US-SRG-10, 07 | `RecoveryTab.dc.html` | Recovery status | Segmented control with icon and word, plus the history |
| | `RecoveryBoard.dc.html` | Front desk → Recovery | ⭐ Compact cat header on each card, sorted Ready → Recovering → Stable. Shows "since … · set by …", a `tel:` link, "Updated", and the empty state |
| | `DischargeForm.dc.html` | Discharge form | ⭐ Instructions filled from the protocol, with a counter. Follow-up windows with an out-of-window error. Discharge is disabled with the reason, and the confirm is shown |
| | `Discharged.dc.html` | Discharged | The success line names the channel. Print post-op sheet. Follow-ups with Mark done and Cancel (reason required) |
| | `PostOpSheet.dc.html` | Post-op sheet | A print page with no app navigation |
| Owner pages — US-SRG-07 | `PortalSurgeries.dc.html` | My cats' surgeries | Before surgery: date, admit-by time, fasting instructions and branch contact. Nothing to pay online |
| | `PortalSurgeryAfter.dc.html` | During / after | The OQ-3 "in our care" text. After discharge: instructions, follow-ups with Book follow-up, and Print |
| Protocols, campaigns, 360px — US-SRG-07, 11 | `Protocols.dc.html` | Protocols | "Using defaults" rows, and the editor with follow-up window validation |
| | `Campaigns.dc.html` | Campaigns | The list and editor. `CampaignPriceLocked` is written as a sentence |
| | `CampaignCases.dc.html` | Campaign cases | Campaign vs standard price per case, and a totals row |
| | `Mobile360.dc.html` | At 360px (UI-1) | Day list rows as cards. Tabs as a select. The log stays one column |

## The design system

Built on the [PurrCare design system](https://claude.ai/artifact/Y2WzXo2hnfc7a9ac7GHNvN) tokens,
hand-transcribed into each artboard as in the earlier areas. This area uses the system's existing
pieces: `Table`, `Tabs`, `Dialog`, `Badge`, `Banner`, `Field` and `Button`. It adds a few
screen-level patterns but no new tokens:

- **Surgery header.** The HO-CAT-002 cat header, then the procedure, time and team, then a
  five-step status stepper. The current step is shown as a word ("Current"), not only by colour.
- **Compact cat header.** Photo, name, a "High risk" badge and allergy chips. It is used inside
  every clinical dialog and on each recovery card.
- **Unit-suffixed input.** The unit sits inside the field's group (`mL`, `bpm`, `°C`, `mmHg`,
  `days`) rather than in a placeholder.
- **Append-only timeline.** Time in the left column and the entry on the right, at every width.
- **State tag.** A dashed label marking a side panel that shows another state of the same screen.

## Rules the design encodes

1. **Allergies and the high-risk warning are on every clinical screen.** Surgery pages carry the full
   cat header. Every drug, vitals, correction, override and end-procedure dialog carries the
   compact one. This matters most for Mittens, who has a severe penicillin allergy and is being
   dosed.
2. **Append-only looks append-only.** The anaesthesia log has no edit or delete control anywhere.
   A correction leaves the original struck through, says "Corrected" in words, and links both ways
   (ADR-FE-070).
3. **The server is the only gate.** Start, Discharge and the override are rendered from blockers
   and statuses the server sends. Each disabled control says why and links to the fix
   (ADR-FE-072).
4. **Confirmations name the consequence.** Booking, reschedule, cancel, override, end procedure,
   finalise and discharge each give who, what, when and what follows. None of them just asks
   "Are you sure?"
5. **Units always travel with numbers.** Doses, quantities, wastage, weights and every vital show
   their unit. mg/kg is labelled "for information" with the weight's date (UI-8).
6. **Every time names its zone.** Times are captioned or suffixed with Asia/Dhaka.
7. **Status is never colour alone.** Checklist rows, surgery status, recovery status, campaign
   status and follow-ups each pair an icon with a word.
8. **No payment step.** Prices are shown "for information — paid at the clinic". There is no
   deposit, gateway or pay button anywhere (ADR-170).
9. **The witness signs in with identifier and password only**, in a dialog on the same screen. The
   design says the credential is used once and cleared.

## Known gaps, deliberately visible

- **No live interaction.** Every state is its own artboard or a labelled side panel.
- **Three embedded components belong to handovers that are not written yet.** They are the
  consent capture (HO-EMR-002), the witness dialog and medicine picker (HO-PHM-002), and the batch
  picker (HO-IMP-002). They are drawn only as far as HO-SRG-002 §8.5 describes them. The witness
  dialog is labelled as HO-PHM-002's. Consent capture appears only as its "Record consent" trigger.
- **Not drawn:** the History tab (named in §8.1 but not specified in §11); the calendar's surgery
  block (§11.10, which belongs to the Appointments canvas); the "Record weight" form, which is
  HO-CAT-002's; and the milestone and follow-up "Mark done" dialogs beyond their triggers.
- **The front-desk page is only sketched.** Its other tab belongs to HO-APT-002, which has no
  canvas yet.
- **All data is illustrative.** Clinical values, batches, dates and prices are examples, not API
  responses.
