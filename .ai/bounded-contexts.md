# Bounded Contexts

The feature contexts in this solution, derived from `docs/SRS.md` §4. These are the `[Feature]` /
`[ContextName]` values used throughout `.ai/namespace-conventions.md`.

This is **one solution with the standard layers** (Api, Application, Domain, Read, Infrastructure,
Shared), not separate projects per context. A context is a folder and a namespace segment.

---

## The contexts

| Context | Aggregates | SRS |
|---|---|---|
| **Cat** | `CatAggregate` — identity, weight history, vaccinations, flags | §4.2 |
| **Appointment** | `AppointmentAggregate` — slot, status, triage priority | §4.3 |
| **Visit** | `VisitAggregate` — vitals, SOAP note, diagnoses, treatments | §4.3 |
| **Surgery** | `SurgeryAggregate` — checklist, consent, anaesthesia log, report | §4.4 |
| **Prescription** | `PrescriptionAggregate`, `DietAuthorisation` | §4.5 |
| **BoardingUnit** | `BoardingUnitAggregate` — type, capacity, rate | §4.6 |
| **BoardingStay** | `BoardingStayAggregate` — dates, care profile, tasks, incidents | §4.6 |
| **Product** | `ProductAggregate`, SKUs, country of origin | §4.7 |
| **Supplier** | `SupplierAggregate`, `PurchaseOrderAggregate` | §4.8 |
| **Consignment** | `ConsignmentAggregate` — import permits, certificates, landed cost | §4.8 |
| **Batch** | `BatchAggregate` — lot, expiry, status; `StockLedgerEntry` | §4.8 |
| **Order** | `OrderAggregate`, `CartAggregate`, `SubscriptionAggregate` | §4.9, §4.10 |
| **Invoice** | `InvoiceAggregate`, `PaymentAggregate`, credit notes | §4.11 |
| **Notification** | Templates and the notification log | §4.12 |

---

## The one that matters most

**`CatAggregate` is shared across all four business lines.** A cat treated in the clinic is the
same record used to gate prescription diets in the shop and to validate vaccination status at
boarding check-in (SRS §1.2). It is the reason this is one product rather than three.

Practical consequence: `CatAggregate` is loaded by command handlers in several contexts. That is
expected. What it must **not** become is a dumping ground — clinical detail belongs to `Visit`,
stay detail to `BoardingStay`, and the cat keeps only what is true of the cat regardless of which
business line is asking.

---

## Aggregate boundary rules

1. **One aggregate mutated per command.** If a use case appears to need two, the boundary is
   probably wrong — raise it rather than widening the transaction.
2. **Aggregates reference each other by id**, never by navigation property. A `BoardingStay`
   holds a `CatId`, not a `CatAggregate`.
3. **An aggregate must not load an unbounded collection.** Vaccinations on a cat are bounded and
   belong inside it. Weight history, care logs and stock ledger entries are not — keep them out
   of the aggregate and query them from the Read layer.
4. **Cross-aggregate domain logic goes to a Domain Service** in `Domain.DomainServices` — FEFO
   allocation, appointment slot availability. Not to an application-layer service.

---

## Cross-context rules that span aggregates

These are the rules that most often tempt someone to reach across a boundary. Each is listed with
where it actually belongs:

| Rule | SRS | Belongs to |
|---|---|---|
| A cat cannot board with overdue vaccinations | BR-1 | `CatAggregate` exposes vaccination status; the boarding command handler checks it |
| A therapeutic diet needs a live authorisation | BR-2 | `DietAuthorisation` (Prescription context); the order handler checks it |
| Expired batches are never sellable or usable | BR-3 | `BatchAggregate` — its own state |
| Allocate the soonest-expiring batch (FEFO) | BR-4 | **Domain Service** — spans many batches |
| Surgery cannot start without consent | BR-5 | `SurgeryAggregate` — its own state |
| A recall must trace every cat that received a batch | FR-IMP-11 | A query across order lines, dispensing records and stay feeding logs — Read layer |
