# Surgery Flow (Spay / Neuter and Procedures)

## Status: Not Started

## What it does
Schedules and records surgery: theatre and staff booking, pre-operative checklist, consent
capture, anaesthesia log, surgical report, automatic neuter-status update, and post-operative
follow-up scheduling. SRS §4.4 (FR-SRG-1..11).

## Modules involved
| Module | Role |
|---|---|
| **Clinic** | Owns `Surgery`. The whole flow |
| **CatProfiles** | Neuter status is set on the cat when the procedure completes (contract) |
| **Inventory** | Drug and consumable deduction, including the controlled-drug register |
| **Identity** | Surgeon and assistant; the override authority |

## The flow
```
Book surgery (theatre + surgeon + assistant, all three checked)
      │
      ▼
Pre-op checklist ── fasting issued · bloods · weight · CONSENT · ASA grade
      │
      ├─ consent missing ──► START BLOCKED (vet override only, reason recorded)
      ▼
Admit ──► Anaesthesia log (drugs, doses, times, periodic vitals)
      │
      ▼
Procedure ──► Surgical report ──► neuter status set automatically
      │
      ▼
Recovery (recovering / stable / ready for discharge — visible at reception)
      │
      ▼
Discharge + post-op instructions ──► wound check ~day 3 ──► sutures ~day 10-14
```

## Key aggregates
- `SurgeryAggregate` — schedule, checklist, consent, anaesthesia log, report, status machine
  `scheduled → pre_op → in_theatre → recovery → discharged`

## Key invariants
- **Cannot transition to `in_theatre` without recorded consent** (FR-SRG-3, BR-5). The override
  is a vet-only path that demands a reason and is audited — it is not a flag
- Anaesthesia log entries are append-only and timestamped
- A completed spay/neuter sets the cat's neuter status and date — never entered by hand (FR-SRG-6)

## Cross-module rules
- Controlled drugs write an **append-only** register entry naming the vet, cat, dose, wastage and
  a second staff witness. Entries are never edited or deleted; corrections are new linked entries
  (FR-PHM-7, BR-7, SRS C-2). This is the strongest immutability requirement in the system
- Neuter status update goes to CatProfiles as an integration event — Clinic does not write to
  `cats.*`
- A surgery on a currently boarding cat links to the stay and its charges flow to the stay invoice
  (FR-SRG-9)

## Decisions made
-

## Remaining work
- Three-way conflict check (theatre, surgeon, assistant) in one transaction
- Consent capture: on-screen signature vs uploaded scan; storage and retention
- Controlled-drug register: the append-only guarantee, the witness capture, and the report an
  inspector will ask for
- Post-op follow-up scheduling from a configurable protocol per procedure
- Decide whether campaign/subsidised pricing is in scope at launch (SRS Q-15)
