# Purchase Order (PO) Flow

## Status: Not Started

## What it does
Generates a PO from an approved quotation, contract, or direct purchase, with a unique PO number (e.g. `PO-2026-DHK-000012`), multi-currency support, tax calculation (VAT/customs duty), configurable approval, and revision history. SRS §3.4 (FR-PO-001..006).

## Key aggregates / components
- `PurchaseOrderAggregate` (src/Domain/Aggregates)

## Key files
-

## Decisions made
-

## Remaining work
- PO creation from quotation/contract/direct purchase
- PO numbering scheme
- Multi-currency + tax calculation
- PO approval workflow
- PO revision history
