# Pharmacy & Prescriptions — UX

The clickable design for backlog §6 *Pharmacy and Prescriptions*, built against
[`HO-PHM-002`](../../handover/HO-PHM-002.md), the frontend handover. Fifth area, after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/pharmacy/index.html (served from docs/design)

There is no hosted canvas for this area yet. `index.html` in this folder lists every screen with a
one-line description of what it shows.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each loads `./support.js`, the
local runtime copied unchanged from `../_shared/`, which renders the artboard and replaces the
`PurrCare · staff` top bar with the shared staff sidebar and header. Customer-portal screens keep
their own `PurrCare` top bar.

## The screens

Six rows, twenty-six artboards — the largest area so far. HO-PHM-002 §3.2 covers six screen groups
and seventeen routes, and nearly every one has a state the acceptance criteria depend on (a
blocking allergy, only-expired stock, a failed witness), so those get a board or a panel each.

| Row | File | Screen | Shows |
|---|---|---|---|
| Prescribing — US-PHM-01, 02, 03 | `CatPrescriptions.dc.html` | Cat → Prescriptions | Mittens' Active tab under the clinical header; New prescription asks for the visit (OQ-5) |
| | `ComposerWarnings.dc.html` | Composer — warnings | ⭐ the safety panel beside the form: above-range dose in mg/kg **and** mg, Major and Minor interactions, the acknowledgement block with a field error, the ADR-FE-068 sentence |
| | `ComposerAllergyBlock.dc.html` | Composer — allergy | ⭐ amoxicillin for a penicillin-allergic cat: a **Blocking** warning listed first, reason required and missing |
| | `ComposerWeightRequired.dc.html` | Composer — stale weight | The refusal with the latest weight and date; Record weight as a dialog that re-checks on save |
| | `ComposerStates.dc.html` | Composer — states | Nine panels: no visit, finalised visit, no prescribing rights, medicine picker (Controlled / Not stocked), checking, check failed, warnings changed, not checkable, issued |
| | `PrescriptionDetail.dc.html` | Prescription detail | Safety record as issued, dispensings with lots, the owner's repeat request, Renew's "all checks run again", the cancel confirmation |
| Formulary — US-PHM-04 | `FormularyList.dc.html` | Formulary | Filters (controlled, active, stocked), strength and range with units |
| | `FormularyEditor.dc.html` | New medicine | Strength preview, per-kg min/max errors, an already-linked SKU disabled with its medicine's name, interaction rows (medicine **or** term) |
| | `FormularyActions.dc.html` | Controlled flag & activation | The three separate confirmed actions, and the `409` "someone else changed this medicine" banner |
| Dispensary — US-PHM-05, 04 AC2 | `DispensaryQueue.dc.html` | Dispensary queue | Scan-or-type lookup and its not-found message; Awaiting first supply and Repeat requests with the compact cat header per row |
| | `DispenseScreen.dc.html` | Dispense | ⭐ the server's batch plan: suggestions preselected, quarantined and expired lots disabled **with the reason as text**, "10 of 10 ml selected" |
| | `DispenseStates.dc.html` | Dispense — states | ⭐ FEFO override reason, short total, unknown scan, **only expired stock with no Dispense button**, a blocked prescription, stock moved on submit, dispensed with the unpriced notice |
| | `WitnessDialog.dc.html` | Witness step | ⭐ the witness types their own sign-in; `WitnessNotVerified` shown in the dialog, password cleared |
| | `DispensingDetail.dc.html` | Dispensing detail | Lots, witness, print count, and the reversal confirmation naming quantity, batch and supply |
| | `MedicineLabel.dc.html` | Label print view | 62 × 100 mm from the frozen label, plus the controlled-drug variant |
| Controlled-drug register — FR-PHM-7 | `ControlledRegister.dc.html` | Register entries | ⭐ every kind, signed quantity, running balance, correction/reversal links, Sealed / Awaiting seal — no edit control |
| | `RegisterBalancesIntegrity.dc.html` | Balances & integrity | A mismatch as text and icon; integrity intact beside integrity broken at a named position |
| | `RegisterEntryDetail.dc.html` | Entry detail | The original unchanged, its seal and hash, the correction chain beneath it |
| | `RegisterCorrection.dc.html` | Add a correction | Original read-only, the sign explained, the stock note, the confirmation naming both entry numbers |
| | `RegisterMobile360.dc.html` | Register at 360px | Entries as cards below 768px (UI-1, U-D5) |
| Diet authorisations — US-PHM-08 | `DietAuthorisationForm.dc.html` | Authorise a diet | From an open visit; quick picks; the "already authorised — extend it instead" refusal |
| | `CatDietAuthorisations.dc.html` | Cat → Diet | Active / Expired / Revoked in words; the revoke confirmation |
| Owner portal — US-PHM-06, 07, 08 | `MyPrescriptions.dc.html` | My prescriptions | ⭐ dose and frequency, days left, issuing vet, repeat status per row; cat filter; empty state |
| | `MyPrescriptionDetail.dc.html` | Request a repeat | Collection history without batch numbers; the request dialog |
| | `RepeatNotPossible.dc.html` | Repeat not possible | The reason with **Book a consultation**; validity ended, just requested, open request with Withdraw |
| | `AuthorisedFoods.dc.html` | Authorised foods | Active, expired, revoked, and a cat with none |

## The design system

Built on the PurrCare design system, with the same hand-transcribed tokens as the earlier areas
(`--surface-*`, `--ink-*`, `--accent*`, `--warning-*`, `--danger-*`, Newsreader for titles,
Public Sans for everything else). Nothing new was added to the system. This area reuses:

- **`Table`** for the prescriptions tab, queue, batch plan, register, balances, formulary and diet
  tabs.
- **`Tabs`** for the cat's sections (Prescriptions and Diet added as two more tabs), Active / All,
  Entries / Balances / Integrity, and the portal's Active / Past / Authorised foods.
- **`Dialog`** for cancel, reverse, revoke, the controlled-flag and deactivation confirmations, the
  correction confirmation, the weight form, the witness step and the repeat request.
- **`Badge`**, always as icon **and** word: Active, Awaiting first supply, Controlled, Not stocked,
  Suggested, Expired, Not dispensable, Sealed, Awaiting seal, Mismatch, and the warning severities
  Blocking, Major, Warning and Minor.
- **`Field`**, including the quantity input: a number field joined to a unit select or a fixed unit
  label, so the unit is never only a placeholder.
- The cat header from Cat Profiles in two sizes: the full header (high-risk banner, weight with its
  date, allergy chips) on every cat route, and a one-line version on the dispense screens and queue
  rows.

## Rules the design encodes

1. **The cat's allergies are on screen wherever a drug is prescribed or dispensed for it.** Every
   composer, detail and dispense board carries the header or its compact form. Queue rows carry it
   too (UI-7).
2. **Units are part of the value.** Every dose, quantity, weight, strength and range has its unit
   next to the number: "0.4 ml", "4.20 kg", "0.05–0.10 mg/kg (0.21–0.42 mg for 4.20 kg)". Dose
   inputs pair the number with a unit select (UI-8, ADR-FE-061).
3. **The server is the only clinical authority.** The safety panel, the batch plan and every
   refusal are drawn as server output. No screen shows its own dose maths, allergy matching, FEFO
   choice or expiry check (ADR-FE-060, ADR-FE-062).
4. **Severity is never colour alone.** Warnings carry an icon and a severity word, and allergy
   conflicts come first and are labelled *Blocking* (UI-6).
5. **Nothing can edit history.** Issued prescriptions, dispensings, labels and register entries have
   no edit control. Renew, cancel, reverse and correct are separate named actions that create
   something new and say so (ADR-FE-065).
6. **No override exists for expired stock or for the witness.** With only expired stock there is no
   Dispense button. The witness step has only Confirm or Cancel (BR-3, BR-7).
7. **Confirmations name the consequence**, for example "Cancel RX-2026-000123 for Mittens? Any open
   repeat request will be declined." or "This adds entry #1205 correcting #1200; the original stays
   as it is." (UI-3).
8. **Disabled controls say why**, as with Issue prescription after a failed check or Dispense when
   the total is short. Missing data is written out ("None recorded", "Not collected yet") rather
   than left blank.
9. **Every time names its zone** (Asia/Dhaka). The portal takes no payment: a repeat is paid for at
   collection, and staff screens show no prices (OQ-3).

## Known gaps, deliberately visible

- **No live interaction.** Every state is a separate artboard or a labelled panel on a states board.
- **The visit picker is a pasted reference.** A list of a cat's open visits belongs to HO-EMR-001
  (OQ-5 / §20.5). `CatPrescriptions.dc.html` asks for the visit reference and says why.
- **The consultation screen that embeds the composer (HO-EMR-002) doesn't exist yet**, so the
  composer is shown on its own route under the cat.
- **The label stock is not chosen** (OQ-1). The 62 × 100 mm label is drawn enlarged, and the print
  stylesheet itself isn't shown.
- **Server message wording is illustrative.** HO-PHM-001's messages are shown word for word in the
  build. The text here stands in for them.
- **Clinical figures are illustrative**, including dose ranges, interactions, lots and hashes. They
  are consistent across boards but are not a formulary.
