# Clinic Visit Flow

## Status: Not Started

## What it does
The core clinical path: an owner books or walks in, the cat is triaged and checked in, a nurse
records vitals, the vet consults and records a SOAP note with a coded diagnosis, orders
diagnostics, records treatment and consumables, prescribes, and the visit is finalised into an
immutable record with a discharge summary. SRS §4.3 (FR-CLN-1..24).

## Modules involved
| Module | Role |
|---|---|
| **Clinic** | Owns `Appointment`, `Visit`, `Prescription`. Drives the whole flow |
| **CatProfiles** | Supplies the cat, its allergies, weight history and handling flags (contract) |
| **Inventory** | Deducts consumables and dispensed medicine by batch (contract) |
| **Billing** | Receives the charges as invoice lines (integration event) |
| **Identity** | Supplies the acting vet and enforces the vet-only policy |

## The flow
```
Book / walk-in ──► Check-in ──► Vitals ──► Consultation (SOAP)
                                             │
                    ┌────────────────────────┼──────────────────┐
                    ▼                        ▼                  ▼
              [Diagnostics]            Treatment          [Prescription]
                    │                        │                  │
                    └────────────────────────┼──────────────────┘
                                             ▼
                                    Finalise visit (immutable)
                                             │
                        ┌────────────────────┼────────────────────┐
                        ▼                    ▼                    ▼
                Discharge summary      Invoice lines       Follow-up reminder
```

## Key aggregates
- `AppointmentAggregate` — slot, status machine `booked → checked_in → in_consultation →
  completed`, triage priority
- `VisitAggregate` — vitals, SOAP note, diagnoses, treatments, `FinalisedAt`, addenda
- `PrescriptionAggregate` — medicine, dose, validity, repeats

## Key invariants (belong in the aggregate, not the handler)
- A visit cannot be finalised twice; after finalisation the note body is immutable and further
  changes are **addenda** with their own timestamp and author (FR-CLN-19, BR-11)
- An appointment cannot transition backwards; `completed` and `cancelled` are terminal
  (FR-CLN-11)
- A consultation cannot be finalised without at least one diagnosis
- Only a user holding the Veterinarian role may finalise or prescribe — enforced at the API
  policy **and** re-checked in the handler (FR-CLN-24, BR-6)

## Cross-module rules
- Consumables and dispensed medicine deduct through the Inventory contract, which refuses an
  expired batch outright (BR-3, FR-IMP-9) — no override exists anywhere
- Charges reach Billing as `VisitFinalisedIntegrationEvent`; Clinic never writes to `billing.*`

## Decisions made
-

## Remaining work
- Appointment slot availability: roster + room + duration + transactional hold (FR-CLN-3/4)
- Walk-in queue with emergency pre-emption (FR-CLN-6)
- SOAP note draft auto-save and the finalisation boundary
- Diagnostic result attachment (structured values + file upload with reference ranges)
- Discharge summary generation (print + portal)
- Decide the coded diagnosis vocabulary — free list, or a standard the vets already use
