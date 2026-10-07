# Goods Receiving (GRN) Flow

## Status: Not Started

## What it does
Store officer receives items against a PO, supports partial delivery and QC inspection, generates a GRN document, and updates inventory automatically once approved. SRS §3.5 (FR-GRN-001..005).

## Key aggregates / components
- `GRNAggregate` (src/Domain/Aggregates)

## Key files
-

## Decisions made
-

## Remaining work
- Receive goods against PO
- Partial receiving
- Quality inspection gate
- Inventory integration event
