# Prescription Diet Flow

## Status: Not Started

## What it does
Gates therapeutic food so it can only be bought for a cat with a live veterinary authorisation,
and suspends the subscription when that authorisation lapses. SRS BR-2, FR-SHP-11, FR-PHM-9,
FR-SUB-6.

**Included as the reference example of a cross-module feature.** Three modules, one rule, no
boundary crossed.

## Modules involved
| Module | Owns | Exposes |
|---|---|---|
| **Pharmacy** (`HO-PHM-001`) | `DietAuthorisation` — a vet issued it | **`IDietAuthorisationGate.CheckAsync(customerId, catId, skuId, ct)`** → `DietGateResult(Outcome, AuthorisationId?, ValidTo?, Message)` |
| **Catalog** | The `RequiresPrescription` flag on the SKU — a property of the product | The flag, on the SKU DTO |
| **Ordering** | The cart and order; calls the gate and records the cat on the line | — |

## The flow
```
Vet issues a diet authorisation (cat + SKU + validity window)
      │
      ▼
Owner adds the product to the cart
      │
      ▼
Catalog says: RequiresPrescription = true
      │
      ▼
Ordering calls Pharmacy: IDietAuthorisationGate.CheckAsync(customerId, catId, skuId, ct)
      │
      ├─ NotRequired / Authorised ─► owner selects WHICH CAT ──► recorded on the order line
      └─ NoAuthorisation / AuthorisationExpired / CatNotOwned / CatDeceased
                                  ──► visible but not purchasable, with the reason + a link to book
                                      a consultation
      │
      ▼
Checkout re-checks (same call) ──► expired between cart and checkout? line blocked, reason shown
      │
      ▼
Order placed
      ⋮
Authorisation expires
      │
      ▼
Pharmacy's expiry sweep dispatches ExpireDietAuthorisationCommand ──► raises DietAuthorisationExpiredEvent
      │
      ▼
Ordering/Subscriptions subscribe and suspend any matching subscription ──► notifies customer AND
clinic (FR-SUB-6)
```

## Why this is the reference example
- **Ownership is decided by who creates the fact**, not by who consumes it. The vet issues the
  authorisation, so Clinic owns it — even though Ordering is the module that cares
- **The gate is checked twice** — at add-to-cart for a good user experience, and at checkout
  because the first check can go stale. Both call the same contract method
- **The reaction is an integration event, not a query.** Ordering does not poll Clinic for
  expiries; Clinic announces them
- **Ordering never reads `clinic.diet_authorisation`, and Clinic never knows what an order is.**
  Either would collapse the boundary and neither is necessary

## Key invariants
- The cat is recorded on the **order line**, not just the order — one order may contain a
  prescription diet for one cat and ordinary food for another
- An authorisation is valid for a specific cat **and** a specific SKU. Neither alone is enough

## Decisions made

> **Updated 2026-09-22 (README C-4).** The port is **`IDietAuthorisationGate`**, owned by
> `HO-PHM-001` §8.10, not `IDietAuthorisationService` in Clinic. Three slices had each assumed a
> different name for it; they are now all renamed to this one.
>
> **Updated 2026-09-25 (docs/handover/README.md, Step 1 contracts pass).** "The flow" diagram above
> still called a placeholder `IsAuthorisedAsync(catId, skuId)` on "Clinic" — the port had already
> been renamed everywhere else in this document. Corrected to the real signature,
> `IDietAuthorisationGate.CheckAsync(customerId, catId, skuId, ct)` on **Pharmacy**, and to the real
> event name, `DietAuthorisationExpiredEvent` (HO-PHM-001 §8.10), not
> `DietAuthorisationExpiredIntegrationEvent`. The port's interface and DTOs, plus a fail-closed fake
> registered in `PharmacyModule`, now exist ahead of HO-PHM-001's own build — see
> `src/Domain/CatEcommerce.Domain/DomainServices/IDietAuthorisationGate.cs` and
> `docs/handover/CONTRACTS.md`.

- **The gate returns an outcome, not a `bool`**: `NotRequired | Authorised | NoAuthorisation |
  AuthorisationExpired | CatNotOwned | CatDeceased`. A caller needs to know *why* it was refused to
  say anything useful to the owner, and a `bool` forces every caller to guess.
- **"Today" is recomputed on every call**, so a product locks again at midnight whether or not a
  sweep has run. The expiry sweep is an optimisation for notifications, never the thing that enforces
  the rule.
- Messages are owner-facing and carry **no clinical detail** — "This food needs a vet's authorisation
  for your cat", never a diagnosis.
- `HO-SHP-001` ships a **deny-all stub** until Pharmacy is merged, so a prescription diet is
  unpurchasable rather than accidentally open (fail closed).

## Remaining work
- Expiry sweep: a scheduled job that finds lapsed authorisations and publishes the event
- Cart revalidation at checkout (shared with the general price/availability recheck, FR-ORD-3)
- Portal messaging that tells the owner *why* they cannot buy, without leaking clinical detail
