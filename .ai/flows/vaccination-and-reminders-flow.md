# Vaccination and Reminders Flow

## Status: Not Started

## What it does
Records vaccinations and deworming, computes next-due dates and the Current / Due soon / Overdue
status, drives reminders, and feeds the boarding eligibility gate. SRS FR-CAT-5..7, FR-NTF-3,
BR-1.

## Modules involved
| Module | Role |
|---|---|
| **CatProfiles** | Owns the `Cat` and its vaccination records. Computes status |
| **Clinic** | Records the administration during a visit (contract) |
| **Inventory** | The vaccine batch, and its expiry refusal (contract) |
| **Boarding** | Consumes the status as an eligibility gate (contract) |
| **Notifications** | Due and overdue reminders (outbox) |

## The flow
```
Vet administers a vaccine during a visit
      │
      ▼
Batch check ──► expired? REFUSED, no override (BR-3)
      │
      ▼
Vaccination recorded ── vaccine · batch · date · administering vet
      │
      ▼
NEXT-DUE computed from the configurable schedule (SRS Appendix E)
      │
      ▼
Status recomputed ── Current · Due soon (default 30 days) · Overdue
      │
      ├──► Boarding eligibility gate reads this (BR-1)
      ├──► Portal shows it with a one-click path to book what is due (FR-POR-2)
      └──► Reminder queued ahead of the due date ──► outbox ──► SMS/email
```

## Key aggregates
- `CatAggregate` with an owned `Vaccinations` collection — bounded, so it stays inside the
  aggregate (unlike weight history, which is not)

## Key invariants
- The same vaccine cannot be recorded twice for one cat on one date
- Next-due is **derived**, never hand-entered — a stored due date drifts from the schedule the
  moment the schedule changes
- A deceased cat generates no reminders and is excluded from booking flows (FR-CAT-9)

## Cross-module rules
- Status is computed in **CatProfiles** and exposed through `IVaccinationStatusService`. Boarding
  must never recompute it from raw records — two implementations of one rule will diverge
- `CatVaccinatedIntegrationEvent` lets Boarding re-check a previously blocked cat (FR-BRD-5)

## Decisions made
-

## Remaining work
- Vaccination schedule as configurable data, not code (SRS Appendix E, administrator-editable
  with clinical sign-off)
- The due/overdue sweep: a scheduled job that finds cats crossing a threshold and queues reminders
- Reminder de-duplication — a cat due for three vaccines should get one message, not three
- Quiet hours and channel preference (FR-NTF-5, FR-NTF-1)
- FeLV/FIV test-before-vaccinate prompt (SRS Appendix E)
