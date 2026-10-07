# Boarding Stay Flow

## Status: Not Started

## What it does
The cat care centre: availability, vaccination-gated booking, deposit, check-in with belongings,
daily care tasks and logs, incidents, vet referral, extension or relocation, and check-out with a
stay summary and final invoice. Short-term and long-term. SRS §4.6 (FR-BRD-1..22).

## Modules involved
| Module | Role |
|---|---|
| **Boarding** | Owns `BoardingUnit`, `BoardingStay`, `CareTask`, `Incident` |
| **CatProfiles** | **Vaccination status gate** and handling flags (contract) |
| **Clinic** | Vet referral during a stay creates a linked visit (contract) |
| **Billing** | Deposit, cycle invoices for long stays, final invoice |
| **Notifications** | Daily updates, incident alerts (outbox) |

## The flow
```
Check availability (bookings + maintenance + cleaning turnaround gap)
      │
      ▼
VACCINATION GATE ── Overdue? ──► BLOCKED, offer a vaccination appointment inline
      │ Current / DueSoon
      ▼
Care profile (feeding, medication times, litter, vet authority, SPENDING LIMIT)
      │
      ▼
Deposit required ──► stay stays `reserved` until staff record it ──► `confirmed`
      │                        └─ window expires unpaid ──► lapses, unit released
      ▼
Check-in ── identity · vaccination proof · weight · health obs · belongings + photos · signature
      │
      ▼
Daily: tasks generated from the care profile
         feeds · medication · litter · enrichment · observation
         completed with timestamp + caretaker identity
         logs + photos ──► owner sees the stay diary
      │
      ├─ Incident ──► severity ──► medium/high notifies owner (high within 15 min, escalates)
      ├─ Vet referral ──► linked Clinic visit ──► charges vs SPENDING LIMIT ──► approval if over
      ├─ Extend / relocate
      ▼
Check-out ── summary (logs, weight change, incidents, meds, vet visits, belongings) + invoice
```

## Key aggregates
- `BoardingUnitAggregate` — type, capacity, rate, maintenance blocks
- `BoardingStayAggregate` — dates, unit, care profile, deposit, status machine
  `enquiry → reserved → confirmed → checked_in → checked_out`
- `CareTask`, `Incident` — owned by the stay

## Key invariants
- **A cat cannot board without current vaccinations** (BR-1, FR-BRD-5). Administrator override
  demands a reason and is audited
- **A cat cannot have two overlapping stays** (SRS D-7)
- **A suspected-infectious cat is only assignable to an isolation unit** (BR-9, FR-BRD-19)
- Unit capacity cannot be exceeded by a multi-cat booking
- Vet charges beyond the owner's stated spending limit require explicit approval (BR-8)

## Cross-module rules
- The vaccination gate calls `IVaccinationStatusService` in CatProfiles' contract. Boarding never
  reads `cats.*` — this is the canonical example of the boundary paying for itself
- A `CatVaccinatedIntegrationEvent` from Clinic lets Boarding re-check a previously blocked cat

## Decisions made
-

## Remaining work
- Availability algorithm including the cleaning turnaround gap
- Daily task generation (idempotent — running twice must not duplicate a day's tasks)
- Stay diary media: retention (12 months, SRS D-10) and storage cost
- Long-stay cycle invoicing (FR-BRD-21) and early check-out pro-rata (FR-BRD-17)
- Blocked by SRS Q-6 (deposit and cancellation policy), Q-7 (rate card), Q-11 (unit counts)
