# Import to Shelf Flow

## Status: Not Started

## What it does
Sourcing food from multiple countries: supplier and purchase order, import consignment with its
permits and certificates, goods receipt with batch and expiry, landed-cost allocation, and the
expiry lifecycle through to markdown or write-off. SRS §4.8 (FR-IMP-1..16).

## Modules involved
| Module | Role |
|---|---|
| **Inventory** | Owns `Supplier`, `PurchaseOrder`, `Consignment`, `Batch`, `StockLedgerEntry` |
| **Catalog** | The SKU being received, and its country of origin (contract) |
| **Billing** | Supplier invoices and landed cost |
| **Notifications** | Expiry threshold alerts (outbox) |

## The flow
```
Supplier ──► Purchase order (SKUs, qty, unit cost, currency, expected date)
      │
      ▼
IMPORT CONSIGNMENT ── origin country · shipment ref · HS/tariff code · import permit
                      health/veterinary certificate · customs declaration · documents
      │
      ▼
Customs clearance
      │
      ▼
GOODS RECEIPT ── per SKU: batch/lot · manufacture date · EXPIRY DATE · qty received
                 qty rejected + reason
      │            └─ food or medicine with no expiry ──► RECEIPT REFUSED
      ▼
Landed cost allocated across received qty (freight · duty · clearing · insurance)
      │
      ▼
Stock ledger entry ──► batch available ──► sellable
      │
      ▼
Expiry alerts at 90 / 60 / 30 days ──► markdown ──► or expiry ──► write-off
```

## Key aggregates
- `SupplierAggregate`, `PurchaseOrderAggregate`
- `ConsignmentAggregate` — the import documents and cost components
- `BatchAggregate` — lot, expiry, status machine `in_transit → received → available`, with
  `quarantined` / `recalled` / `expired` / `written_off`
- `StockLedgerEntry` — **immutable**; current stock is derived from it, never stored as an
  editable number (SRS D-3)

## Key invariants
- **Expiry date is mandatory** for food and medicine batches (SRS D-2)
- **An expired batch can never be sold, dispensed or fed — no override exists** (BR-3,
  FR-IMP-9). This is the hardest rule in the system; it has no administrator bypass
- Sellable stock excludes expired, quarantined, damaged and reserved quantities (FR-IMP-8)
- Stock ledger entries are append-only; an adjustment is a new entry with a mandatory reason

## Cross-module rules
- Inventory is the **only** module that allocates or deducts stock. Clinic, Boarding and Ordering
  all call its contract
- Country of origin lives on the Catalog SKU and is surfaced in the storefront's "Shop by
  Country" view (FR-SHP-6); the consignment records where a specific batch actually came from

## Decisions made
-

## Remaining work
- Landed-cost allocation method (by value, by weight, by unit) — affects margin reporting
- Import document storage and retention for inspection (SRS NFR-LEG-2)
- Partial receipt and rejection handling against an open PO
- Cycle counts and stock takes with a variance report before commit
- Blocked by SRS Q-8 (source countries and per-origin licence constraints)
