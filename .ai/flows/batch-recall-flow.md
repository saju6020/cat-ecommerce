# Batch Recall Flow

## Status: Not Started

## What it does
Marks a batch recalled, quarantines every remaining unit of it across all locations, and traces
everyone who received it — through shop orders, clinic dispensing and boarding feeding — so they
can be notified. SRS FR-IMP-11, BR-14, D-4.

**This is the flow that justifies batch tracking.** Everything else about batches is bookkeeping;
this is the one that protects cats.

## Modules involved

> **Updated 2026-09-25 (docs/handover/README.md C-3).** The table below described the withdrawn
> per-module fan-out design. Inventory now traces a recall alone, reading the `StockParty` /
> `StockReference` stamp every consumer is required to write on every `IStockLedger` movement
> (HO-IMP-001 §8.8, ADR-104). No other module implements a trace contract.

| Module | Role |
|---|---|
| **Inventory** | Owns the batch and the recall. Initiates, quarantines, traces (alone) and dispatches notices |
| **Ordering** | Stamps `StockParty`/`StockReference` on every order movement — supplies rows to the trace, implements nothing |
| **Clinic** (Consultation, Pharmacy, Surgery) | Stamps `StockParty`/`StockReference` on every dispense/administration movement — supplies rows, implements nothing |
| **Boarding** | Stamps `StockParty`/`StockReference` on every feeding movement — supplies rows, implements nothing |
| **Notifications** | Notifying every affected owner (outbox) |

## The flow
```
Recall declared on a batch
      │
      ├─► ALL remaining stock quarantined immediately, every location, one transaction
      │
      ├─► Unshipped orders containing the batch ──► BLOCKED from dispatch (BR-14) — a recalled or
      │   expired reserved batch cannot be consumed (IStockLedger.ConsumeReservationAsync rule 7)
      │
      ▼
TRACE ── a single ledger read inside Inventory, over every stock_ledger_entry row whose BatchId
          matches, grouped by the StockParty (CustomerId, CatId) and StockReference stamped on it
          at the moment of the original movement (Sale / Dispense / ClinicalUse / BoardingUse) —
          no other module is asked anything
      │
      ▼
Affected list (customer, cat, quantity, date, channel) — customers resolved through merges and
cats through current ownership at read time
      │
      ▼
Notify all, in one action ──► outbox ──► SMS / email ──► every send logged
```

## Key invariants
- Quarantine is **immediate and atomic** — no window exists in which recalled stock is still
  sellable
- The trace must cover every consumption path (shop orders, clinic dispensing/treatment, boarding
  feeding, and any future one). This now holds **by construction**: a movement cannot exist without
  a `StockParty`/`StockReference` stamp, so a consuming context cannot forget to be traceable —
  there is no per-module trace implementation left to omit
- Notifications go through the outbox: at-least-once, logged, retryable. A lost recall notice is
  the worst failure mode in the system

## Cross-module rules

> **Superseded 2026-09-22 by HO-IMP-001 ADR-104 (README C-3).** This section originally had each
> consuming module answer `IBatchConsumptionQuery.WhoReceivedAsync(batchId, ct)` for itself. That is
> **not** what was built, and the fan-out design has been withdrawn.

- **Every stock movement goes through one port, `IStockLedger`** (HO-IMP-001 §8.8), and the port
  **requires** the caller to stamp `StockReference` (what this movement was for) and `StockParty`
  (which customer, which cat) at the moment of the movement. A `Dispense` refuses to enlist without
  a cat and a customer.
- **The trace is therefore a ledger read**, done entirely inside Inventory. No module implements a
  trace contract, and none is asked anything.
- **Why this replaced the fan-out.** A contract implemented by every module is a contract every
  *future* module must remember to implement — and the failure is silent: the recall runs, returns
  fewer rows, and nobody learns that a consumption path was missed. The stamp cannot be forgotten,
  because the port refuses the movement without it.
- The "fourth consumption path added later" argument holds better under the stamp: a new module
  cannot move stock at all except through `IStockLedger`, so it is in the trace by construction.
- Customers are resolved through merges and cats through current ownership **at read time**, so no
  handler rewrites immutable ledger rows when a customer is merged.

## Decisions made
- **ADR-104** — recall is one batch-row status change, atomic across locations and serialised against
  consumption with `FOR SHARE`; the trace is a ledger read over the party stamp; notices go one per
  distinct customer with a dedup key and a subject guard.
- The consuming slices build nothing: HO-ORD-001 and HO-PHM-001 each deleted their trace
  implementation and assert the stamp on every call site instead.
- **2026-09-25 (docs/handover/README.md, Step 1 contracts pass).** `IStockLedger`'s interface and
  DTOs (`StockDemand`, `StockReference`, `StockParty`, `StockResult<T>`, ...) now exist ahead of
  HO-IMP-001's own build, plus a fail-closed fake registered in `InventoryModule` so every consuming
  slice can compile and test its own stamping obligation before Inventory is real — see
  `src/Domain/CatEcommerce.Domain/DomainServices/IStockLedger.cs` and `docs/handover/CONTRACTS.md`.

## Remaining work
- Decide whether the trace is synchronous (slow, complete, one screen) or a queued job with a
  progress view — depends on expected order volume
- Notification template and channel escalation for a recall specifically
- Regulatory reporting: who must be told outside the business, and within what window
  (blocked by SRS Q-1)
- Partial recall (a date range within a batch) — decide whether it is in scope
