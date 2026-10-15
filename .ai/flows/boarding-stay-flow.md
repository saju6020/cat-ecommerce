# Boarding Stay Flow

## Status: Backend complete (HO-BRD-001, 2026-10-14) -- frontend is HO-BRD-002

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

## Built -- HO-BRD-001 (updated 2026-10-14)

Status: Complete (backend, phases A-H). One `Boarding` context, schema `boarding`, ADR-350 ... ADR-369 (`.ai/decisions/adr.md`); the contract and every departure from the handover are in `docs/handover/HO-BRD-001.md` section 1.1 Amended.

**Aggregates** (`src/Domain/CatEcommerce.Domain/Aggregates/Boarding/`): `BoardingUnit`, `UnitMaintenanceBlock`, `BoardingRateCard`, `BoardingCareRoutine`, `BoardingStay` (partials: core, `.Lifecycle`, `.Care`, `.Clinic`, `.Departure`), `CareTask`, `CareObservation`, `IsolationEntry`, `BoardingIncident`, `StayMedia`, `BoardingWaitlistEntry`, `DaycareRateCard`, `DaycareVisit`.

**Pure domain services**: `BoardingAvailabilityFinder`, `BoardingQuoteCalculator`, `BoardingOccupancyWindow`, `BoardingChargePlanner`, `EarlyCheckout`, `RelocationOptions`, `VetSpendingHeadroom`, `DaycarePricing`, `CareTaskGenerator`, `CareTaskAssigner`, `NotEatingRule`, `VaccinationStatusRule`.

**Money flow** (push model): deposit = Billing payment earmarked `{BoardingStay, stayId}`; charges recorded at check-in through `IBillableChargeRecorder` and re-derived by revision on extension / early check-out; 30-night cycle invoices and the final invoice issued by `BoardingWorker` through `IssueInvoiceFromChargesCommand`. Early check-out preview = `GET .../check-out-preview` (same code as the check-out).

**Where**: handlers `src/Application/.../CommandHandlers/Boarding/`, event handlers `.../DomainEventHandlers/Boarding/`, reads `src/Read/.../{Queries,QueryHandlers}/Boarding/`, controllers `src/Api/.../Controllers/{BoardingPublic,CustomerBoarding,CustomerDaycare,StaffBoardingUnits,StaffBoardingStays,StaffBoardingCare,StaffBoardingIncidents,StaffBoardingDeparture,StaffDaycare}*`, host `Modules/BoardingModule.cs`, worker `Background/BoardingWorkCycle.cs`, ports and readers `src/Infrastructure/.../Boarding/`.

**Remaining**: merge-time integration with EMR (referral events), BIL Phase J (`BoardingStayId` on clinic charges), IMP house food, HO-RPT-001 reports R24-R26 and boarding tiles; a way to resolve `creditDue` once the credit note is issued; waitlist notice quiet hours rely on the notification service.

**Concurrency** (ACCEPTANCE ruling, 2026-10-15): no application lock for occupancy. A reserve, isolation flag or daycare booking that loses an exclusion or unique constraint is retried in a fresh DI scope with the next free unit or slot (up to 4 attempts), then answered 409; see HO-BRD-001 section 1.1 "Concurrency".

**Late check-out** (owner, 2026-10-15, "Charge extra nights, never block"): extra nights are charged but the allocation is not extended; an overlapping booking of the same unit is flagged `NeedsAttention` (`UnitOverstayed`).
