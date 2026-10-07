# Invoice Verification Flow

## Status: Not Started

## What it does
Finance officer enters supplier invoices, system performs three-way matching (PO / GRN / Invoice), prevents duplicate invoices, and tracks payment status (Pending / Partially Paid / Paid). SRS §3.6 (FR-IV-001..004), Business Rule BR-004.

## Key aggregates / components
- `SupplierInvoiceAggregate` (src/Domain/Aggregates)

## Key files
-

## Decisions made
-

## Remaining work
- Invoice entry
- Three-way matching (PO/GRN/Invoice)
- Duplicate invoice detection
- Payment status tracking
