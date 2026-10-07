# Order Fulfilment Flow

## Status: Not Started

## What it does
Cart to delivery: catalogue browse, prescription gating, checkout, FEFO stock allocation, payment
recording, pick and pack, dispatch with tracking, delivery, and returns. SRS §4.9 (FR-ORD-1..22).

## Modules involved
| Module | Role |
|---|---|
| **Ordering** | Owns `Cart`, `Order`, `Shipment`, `Subscription` |
| **Catalog** | Product and SKU data, the `RequiresPrescription` flag (contract) |
| **Inventory** | Sellable stock, **FEFO batch allocation**, reservation (contract) |
| **Clinic** | Prescription diet authorisation check (contract) |
| **Billing** | Invoice, payment recording, refunds |
| **Notifications** | Order status messages (outbox) |

## The flow
```
Browse / filter (incl. by country of origin) ──► Product page (live sellable stock + expiry)
      │
      ▼
Add to cart ──► PRESCRIPTION GATE ──► no valid authorisation? blocked, with the reason
      │                                 valid? select the cat, recorded on the order line
      ▼
Checkout ── address or pickup · delivery slot · payment method
      │        (COD · cash in store · card in store · bank transfer — NO gateway, SRS Phase 5)
      ▼
Re-validate price + availability ──► show any change, require acknowledgement
      │
      ▼
Payment recorded ──┬─ COD ──────────► confirmed, no prepayment
                   ├─ bank transfer ─► `pending_payment`, stock reserved,
                   │                    expires after the window and releases stock
                   └─ in store ──────► staff records it
      │
      ▼
FEFO ALLOCATION ── soonest-expiring non-expired batch first; expired never allocated
      │
      ▼
Pick list (by location + batch) ──► Pack ──► Dispatch (tracking) ──► Delivered
      │
      └─ [Return window] ──► approval ──► quarantine ──► restock or write off
```

## Key aggregates
- `CartAggregate`, `OrderAggregate` (status machine `pending_payment → confirmed → packing →
  dispatched → delivered`), `SubscriptionAggregate`

## Key invariants
- An order line records the **specific batch** allocated, so a recall can trace batch → order →
  customer → cat (SRS D-4)
- An order cannot dispatch while any line is unallocated or its batch is recalled (BR-14)
- Opened food and used litter are non-returnable unless defective or recalled (BR-10)
- Duplicate submission is prevented by an idempotency key (FR-ORD-20) — still required even
  though nothing is queued, because users double-click

## Cross-module rules
- Stock reservation and FEFO allocation belong to **Inventory**, called through its contract.
  Ordering never picks a batch itself and never writes to `inventory.*`
- The prescription gate calls Clinic's `IDietAuthorisationService` (BR-2)
- **No payment gateway** — see SRS ADR/§10 Phase 5. Payment is a record of money received, taken
  by a person

## Decisions made
-

## Remaining work
- Concurrent checkout on the last unit: exactly one succeeds (needs a reservation transaction)
- Bank-transfer hold expiry as a background job
- Courier integration (blocked by SRS Q-4) with CSV manifest fallback
- Returns quarantine and the restock/write-off decision
- Blocked by SRS Q-3 (COD policy — now the primary payment route), Q-3b, Q-5 (tax)
