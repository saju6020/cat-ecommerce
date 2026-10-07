# HO-SUB-001 — Subscriptions (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-SUB-001** |
| Title | Subscriptions — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §11 — **US-SUB-01 … US-SUB-06** (all six; none withdrawn). §18.5 lists what stays deferred with the gateway |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-SUB-1 … FR-SUB-6**, FR-ORD-4, FR-ORD-9, FR-ORD-20, FR-ORD-21, **FR-ORD-22**, FR-BIL-7, FR-NTF-3 ("subscription pre-charge"), FR-SHP-11, FR-PHM-9, **BR-2**, BR-15, **BR-16**, C-5, C-6, D-12, NFR-PRF-7, §5.1 "Subscription" entity, §8.3 "Subscription health", AC-17, Appendix C (subscription states), Appendix D ("Orders & fulfilment", by inference — §12.3) |
| Pairs with | [`HO-SUB-002`](HO-SUB-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ACC-001`](HO-ACC-001.md) (customers, address book, sessions, `ActionRoles`, audit capture, merge contract), [`HO-ADM-001`](HO-ADM-001.md) (settings registry, branches, time model, money), [`HO-NTF-001`](HO-NTF-001.md) (outbox, templates, guards, link builder — with [`HO-APT-001`](HO-APT-001.md)'s `SendAfter` / `SubjectStamp` amendment), [`HO-CAT-001`](HO-CAT-001.md) (cats). **Depends on handovers not yet written**: `HO-SHP-001` (SKUs), `HO-PHM-001` (diet authorisations), **`HO-ORD-001` (placing orders, order events)** — the contracts assumed of each are in §8.12 |
| Delivery order | Section 14 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None beyond this document. No `architect` design exists for this slice — §8 is this handover's own operationalisation of FR-SUB-1…6 and the `.ai/` docs; every decision that goes beyond them is an ADR in §9 |
| Baseline commit | `4007225` |
| Amends (to be recorded) | This slice **appends** to other areas' shared files (the lines are listed in §13): HO-NTF-001's template catalogue, subject kinds and link builder; HO-ADM-001's settings registry; HO-ACC-001's `ActionRoles`, `BusinessViolationCodes`, `SystemActors`. Per the parallel-writing rule this author does not edit those handovers; **the README owner appends the matching §1 Amended rows there** before implementation starts (§20.9) |
| Amended | **2026-09-22 — README C-4.** HO-PHM-001 is written and owns the diet port: the assumed `IDietAuthorisationService.IsAuthorisedAsync` + `GetCurrentAsync` become the single published `IDietAuthorisationGate.CheckAsync`, whose `DietGateResult` already carries `ValidTo`. C-PHM-1 in §8.12 and the C1/C5/C9–C11 rules were rewritten against it. New OQ-16. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Let a cat owner **subscribe to recurring deliveries of food or litter**, stay in control of the
schedule, and never have to re-order by hand. Let the shop run the subscriptions that go wrong: orders
nobody paid for, prescription diets whose authorisation has lapsed, products that stopped selling.

**There is no payment gateway in v1** (SRS §10 Phase 5; backlog §18.5). There is therefore **no saved
card, no payment token and no automatic charge**. A subscription saves the customer from
**re-ordering, not from paying**: each cycle **raises an ordinary order** through the Orders area
(HO-ORD-001). The order is paid the way any order is paid. That means cash on delivery, a bank transfer
that staff confirm, or payment at the counter for a pickup (FR-SUB-2, FR-ORD-21, FR-ORD-22). Auto-charge
and failed-charge retry stay in Phase 5 and are **forbidden** here (§4.4, ADR-238).

Five things this slice is careful about:

1. **One order per cycle, ever.** The worker can crash, retry or run twice. Each cycle carries an
   idempotency key (`sub:{subscriptionId}:{cycleOn}`), which HO-ORD-001 honours (FR-ORD-20). A unique
   index on `(subscription_id, cycle_on)` backs it up. A retried cycle finds its order rather than
   placing a second one (ADR-241).
2. **The customer is always told which cycle a change affects.** A change made before the generation
   cut-off applies to the next delivery. A change made after it is **scheduled** for the following one.
   Every change response names the date (US-SUB-02 AC2, ADR-239).
3. **Orders own orders.** Stock reservation, FEFO, COD eligibility (BR-15), delivery zones, tax, the
   invoice and the bank-transfer expiry window all belong to HO-ORD-001 and the areas behind it. This
   slice **never reimplements them**. It asks Orders to place an order and records what came back
   (ADR-241, ADR-243).
4. **A prescription diet is never generated without a live authorisation** (BR-2). The authorisation
   is checked when the customer subscribes, on every worker pass, and by Orders at generation. A lapsed
   authorisation suspends the subscription and notifies the customer and the clinic. A renewed one
   resumes it without re-creating anything (US-SUB-05, ADR-244).
5. **Unpaid cycles are chased, then stopped.** A bank-transfer order gets a payment reminder before
   its window closes. Orders expires it per FR-ORD-22. After a configurable number of consecutive
   unpaid cycles the subscription suspends, and staff see it on an unpaid-subscriptions list
   (US-SUB-06, ADR-243).

§17 builds the slice in **eight phases**. Each phase compiles, migrates and passes its own tests first.

---

## 3. In scope

### 3.1 Stories

All six stories in backlog §11. **§18 holds the testable acceptance criteria.**

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-SUB-01 | S | 8 | **AC1**: subscribe to a food or litter SKU with quantity, interval (2, 4, 6, 8 or 12 weeks — OQ-2), delivery address or pickup branch, and an intended payment method (COD, bank transfer, pay at pickup). The next delivery date is set, validated and returned. **AC2**: when the date arrives the worker raises an order through HO-ORD-001, which reserves stock and confirms it as COD or holds it as a bank transfer awaiting confirmation | B, D |
| US-SUB-02 | S | 5 | Pause, resume, skip one delivery, change interval, quantity, fulfilment or payment method, cancel. Before the cut-off (default 2 days) a change applies to the next delivery. After it, the change is scheduled for the following cycle and the response names that date. Pending changes can be withdrawn | C |
| US-SUB-03 | S | 3 | A pre-charge notice the configured number of days before each cycle (default 3). It carries a skip link and a manage link, and is never sent for a cycle that has since been skipped, paused, cancelled or moved | E |
| US-SUB-04 | C | 3 | A configurable subscription discount (setting, default 0 %). It is passed to Orders as a labelled line discount on every subscription order and snapshotted on the cycle. The invoice shows it through Orders and Billing (§4.2) | D |
| US-SUB-05 | S | 5 | Prescription-diet subscriptions are suspended when the authorisation lapses, before the next generation. The customer and the issuing vet are notified. A renewed authorisation resumes the subscription with no re-creation | G |
| US-SUB-06 | S | 5 | Payment reminder before a subscription order's bank-transfer window closes. The order's outcome is mirrored from Orders' events. After `subscription.maxConsecutiveUnpaidCycles` (default 2) unpaid cycles the subscription suspends and notifies the customer. A staff unpaid-subscriptions list, and reinstatement by staff | F |

Also delivered: lifecycle reactions to customer merge, account deletion, a cat's death and a cat's
change of owner (Phase H, ADR-247); the staff subscription list with CSV export (UI-4); and cancellation
reasons captured for the SRS §8.3 "Subscription health" report (the report itself is HO-RPT-001).

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409` | ADR-011, ADR-016 |
| Handlers load aggregates directly; no `CommandService` layer; `SaveChangesAsync` per repository call | ADR-015, ADR-021 |
| **No payment gateway, no saved token, no auto-charge** in v1; payment methods are COD, cash/card in store, bank transfer | SRS §10 Phase 5, FR-ORD-8, FR-SUB-2; owner instruction; backlog §18.5 |
| Identifier + password only; no OTP or verification anywhere | HO-ACC-001 §4.2; SRS v2.2 |
| `[RequireAction]` / `[RequireCustomerSession]` / `[PublicEndpoint]` on every endpoint; one `ActionRoles` table | HO-ACC-001 ADR-036, HO-ADM-001 ADR-043 |
| Settings through `IBusinessSettings`; branch-local time via IANA zones; money `decimal(12,2)` + currency, amounts as JSON strings | HO-ADM-001 ADR-045, ADR-050, ADR-054, §12.4 |
| Prices are read **at transaction time** and snapshotted on the transaction, never re-read | HO-ADM-001 ADR-049 |
| Messages only through `INotificationOutbox.Enqueue` in the command handler before the save; timed producers use `RequestNotificationCommand` with a deduplication key; **never from an event handler** | HO-NTF-001 ADR-056, ADR-057 |
| `SendAfter` and `SubjectStamp` on notification requests; subject guards read, never write | HO-APT-001 amendment to HO-NTF-001; HO-NTF-001 §8.7 |
| `CustomerMergedEvent` is binding on every context holding a customer id | HO-ACC-001 ADR-039 |
| The Order bounded context owns `SubscriptionAggregate`; `Subscription` is a listed feature segment; `SubscriptionStatus` = Active, Paused, Suspended, Cancelled | `.ai/bounded-contexts.md`, `.ai/namespace-conventions.md`, `.ai/enumeration-pattern.md` |
| Stock reservation and FEFO belong to Inventory, called by Orders; **Ordering never picks a batch** | `.ai/flows/order-fulfilment-flow.md` |

---

## 4. Out of scope

Building anything in this section is a **FAIL** at the acceptance gate.

### 4.1 Frontend — all of it

Screens are [`HO-SUB-002`](HO-SUB-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-SUB-01 "the next delivery date is set and shown" | Every subscription view carries `nextDeliveryOn` (a local date) and `commerceTimeZoneId` |
| US-SUB-02 "I am told which one" | Every change returns `SubscriptionChangeResult` with `appliedImmediately`, `appliesFromCycleOn` and a plain-language `message` |
| US-SUB-03 "with a link to skip or change" | `subscription.pre-charge` carries `skipLink` (`/portal/subscriptions/{id}?skip=yyyy-MM-dd`) and `manageLink`. The skip command takes the cycle date, so a stale link is refused specifically (`CycleNoLongerSkippable`) |
| US-SUB-04 "shown on the invoice" | The discount goes to Orders as a **labelled line discount** (§8.12 C-ORD-1). Rendering it on the invoice is HO-ORD-001's and HO-BIL-001's (§4.2) |
| US-SUB-06 "staff see it in an unpaid-subscriptions list" | `GET /api/staff/subscriptions/unpaid` |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Where it lands | What this slice delivers |
|---|---|---|
| The invoice line showing the subscription discount (US-SUB-04 "shown on the invoice") | **HO-ORD-001 / HO-BIL-001** | The discount passed on every generated order as `LineDiscount { Percent, Label = "Subscription discount" }`, and the percent snapshotted on the cycle (A9 evidences the order line; the invoice rendering is theirs) |
| Order confirmation, bank details, the expiry notice ("the order expires per FR-ORD-22") | **HO-ORD-001** | Nothing is duplicated. This slice adds only the **reminder before** expiry (ADR-246) |
| Reconciling COD and transfers for subscription orders (FR-BIL-7) | HO-BIL-001 | Subscription orders are ordinary orders; `SourceReference` lets reconciliation filter them |
| Portal dashboard tile of active subscriptions (FR-POR-1) | HO-POR-001 | `GET /api/customers/me/subscriptions` |
| "Subscription health" report — active, paused, churned, churn reasons (SRS §8.3) | **HO-RPT-001** | Status, suspension reason and **cancellation reason** columns, indexed |
| Offering "Subscribe" from the product page | HO-SHP-002 (page) / HO-SUB-002 (button) | `GET /api/customers/me/subscriptions/options?skuId` |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No SMS or email provider** (HO-NTF-001 OQ-1) | Notices, reminders and suspension messages are enqueued, rendered and logged. Outside Development **only the in-app copy reaches a customer**. The **clinic's** copy of a prescription suspension (US-SUB-05) goes to a **staff** recipient. Staff get no inbox copy (HO-NTF-001 R6), so outside Development it reaches nobody until a provider exists. The staff list filtered by `AuthorisationExpired` (§12.4) is the dependable channel, and A13 records the caveat |
| **HO-SHP-001, HO-PHM-001, HO-ORD-001 not written** | The contracts of §8.12 are **assumptions**. A mismatch found at implementation is a §20.2 stop and an Amended row, never a shim |
| COD policy (SRS Q-3), transfer window (Q-3b) | Both are HO-ORD-001's. This slice reads the outcome (`paymentExpiresOn`, refusal codes) and adapts to whatever Orders configures |
| Tax regime (SRS Q-5) | The estimate shown before subscribing is **subtotal and discount only**. Delivery and tax are "calculated when each order is placed" by Orders |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, RabbitMQ, a second host, a queue product, a `CommandService`
layer, `202 Accepted`, `[Authorize]`.

Specific to this slice:

- **No payment token, card number, card fingerprint, gateway customer id, mandate or "auto-charge"
  flag** in any column, DTO, event, ViewModel or setting (C-5, D-12, ADR-238). **No retry-charge
  loop.** `SubscriptionPaymentMethod` has no online value (§10.1).
- **No order, order-line, stock, reservation, batch, invoice or payment table or aggregate** in this
  slice. It holds order **ids** and a mirrored **outcome** only (ADR-241, ADR-243).
- **No reimplementation** of COD eligibility (BR-15), delivery zones, delivery charges, tax, FEFO or
  the bank-transfer window. If a screen needs one, ask HO-ORD-001's contract (§8.12).
- **No nested command dispatch inside one DI scope** (ADR-241: F23 would commit this slice's tracked
  entities inside Orders' save).
- **No enqueue from a domain event handler** (HO-NTF-001 ADR-057). **No automatic cancellation from
  cat, customer or authorisation events.** Those suspend or reassign only (ADR-247). Account deletion
  is the one exception: it cancels.
- **No verification or OTP step** anywhere in the subscribe or change flows (HO-ACC-001 §4.2).

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001 merged** | ❌ **not implemented at baseline** — §20.1 |
| **HO-APT-001's HO-NTF-001 amendment** (`SendAfter`, `SubjectStamp`, `AddNotificationRequestScheduling`) merged | ❌ — §20.1. Phases E and F need it |
| **HO-SHP-001 merged** with the SKU contract C-SHP-1 (§8.12) | ❌ handover not written — §20.2 |
| **HO-ORD-001 merged** with C-ORD-1…C-ORD-4 (§8.12) | ❌ handover being written in parallel — §20.2. Phases D and F cannot start without it |
| **HO-PHM-001 merged** with C-PHM-1 (§8.12) | ❌ handover not written — §20.2. Phase G cannot start without it |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ |
| New NuGet packages | **None** |

Phases A–C (schedule, create, changes) need only ACC, ADM, NTF and SHP. Phase D onwards needs ORD.
**Do not stub Orders to get Phase D green** (§20.3).

The shared-database rules of HO-CAT-001 §16.1 and HO-ACC-001 §16.1 apply. **Each test mints its own
customer, SKU and cat** (§16.1). The live-subscription unique index is then never shared.

---

## 6. Governing architecture documents

Identical to [HO-ACC-001 §6](HO-ACC-001.md#6-governing-architecture-documents), which names all
twelve `.ai/` documents. Where any of these disagrees with a `.ai/templates/*.cs` file, **the markdown
wins**. Of note here:

| Document | Why it matters in this slice |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer direction. The contracts of §8.12 are ports in `Domain.DomainServices`, because `Application` can reach nothing else |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | The worker dispatches commands and queries through `IInMemoryDispatcher` only, **one scope per command** (ADR-241) |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Row "Order" owns `SubscriptionAggregate`, so ADR-237 places this slice there. **Rule 1**: one recorded exception (ADR-240). **Rule 2**: orders, SKUs, cats and addresses are held by id. **Rule 3**: cycles accumulate without bound, so they are their own root. **Rule 4**: the schedule and unpaid-streak rules are pure domain services |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | `Subscription` is a listed feature name. Every namespace in §11/§12 uses it. Do not invent others |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 binding.** ADR-009 (action table), ADR-014 (synchronous commands), ADR-016 (`xmin`), ADR-021 (per-call save) are the ones this slice turns on |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §5 when a domain service is warranted (`SubscriptionSchedule`, `UnpaidCycleRule`); **§6 rule placement**: transitions in the aggregate, uniqueness in the handler |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | `AsNoTracking()`; project before materialising; worker queries never write |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §4 owned mapping (`SubscriptionTerms`, `PendingTerms`); §6 `xmin` (worker vs customer race); §8 `date` / `numeric` column types |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | **§3**: a failing handler fails the command after commit, so the order-outcome handlers are idempotent and never enqueue. **§4**: the payment reminder and notices are outbox items. **§5**: suspension is a command, not an event side effect |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | `SubscriptionStatus` exactly as listed there; the §10.1 enumerations derive from `Shared`'s `Enumeration` |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | §3 validators as `IValidator<T>`; §4 scope validation on; the worker is a Singleton that creates a scope per unit of work |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs` shape; `AddHostedService<SubscriptionWorker>()`; §5 the never-register list |
| [`.ai/flows/order-fulfilment-flow.md`](../../.ai/flows/order-fulfilment-flow.md) | Ordering owns `Subscription`; Inventory owns reservation; no gateway |
| [`.ai/flows/prescription-diet-flow.md`](../../.ai/flows/prescription-diet-flow.md) | The flow doc names `IDietAuthorisationGate`; **HO-PHM-001 as written publishes `IDietAuthorisationGate.CheckAsync`** (README C-4) and the doc needs updating. **ADR-244 deviates from its "integration event" reaction** and says why (OQ-1) |
| HO-NTF-001 §8.4, §8.7, §8.12 | Producer rules, the guard extension point, and the timed-producer pattern this slice copies |
| HO-APT-001 §8.9, §12.2 | `SendAfter`/`SubjectStamp` usage; the `ClinicWorker` shape `SubscriptionWorker` copies |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001 F28–F30
apply, and so does every fact recorded by HO-SHP-001, HO-IMP-001, HO-PHM-001, HO-BIL-001 and HO-ORD-001
once written. **F23 matters most here**: a repository save commits *every* entity tracked by the scoped
`DbContext`. That is why the worker never runs Orders' command and this slice's command in one scope
(ADR-241).

This handover adds **no new verified fact**. It could not run the code for this handover. The following
are **unverified** and are checked in Phase A step 1; a false one is a §20.6 stop:

| # | To verify | If true | If false |
|---|---|---|---|
| V1 | Npgsql EF Core 9 supports `HasIndex(...).AreNullsDistinct(false)` on PostgreSQL 17.5 (`NULLS NOT DISTINCT`, PG 15+) | Use it for `ix_subscription_live_per_sku_cat` (§12.1) | Write the index in `migrationBuilder.Sql` with `NULLS NOT DISTINCT`; still no sentinel Guid |
| V2 | `DateOnly` maps to `date` with no value converter (as HO-ADM-001 assumes for closures) | As written | Stop — HO-ADM-001 has the same dependency |
| V3 | HO-ORD-001's `PlaceOrderCommand` (C-ORD-1) accepts a **system actor** `UserContext` placing an order for a named `CustomerId` | The worker dispatches it as `SystemActors.SubscriptionWorker` | §20.2 — Orders must accept it; do not impersonate the customer's session |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Order context (per .ai/bounded-contexts.md) — feature segment `Subscription`, schema `subscription`
SubscriptionAggregate        (root)  → CustomerId, SkuId, CatId?, DeliveryAddressId?, PickupBranchId?   subscription.subscription
├── Product        [owned one]  SubscribedProduct   (display snapshot)
├── Terms          [owned one]  SubscriptionTerms
└── PendingTerms?  [owned one]  SubscriptionTerms   (scheduled after the locked cycle)
SubscriptionCycleAggregate   (root)  → SubscriptionId, CustomerId, OrderId?                              subscription.subscription_cycle
```

**Why the schema is `subscription` and not Orders' schema.** HO-ORD-001 is being written in parallel
and its schema name is not known. A separate schema means this slice never adds to or migrates an
Orders table, and the two slices can be built and reverted independently. The bounded context is still
Order (ADR-237). Merging schemas later is a rename, not a redesign (OQ-1).

**Why cycles are their own root.** A weekly-ish subscription accumulates cycles for years
(`.ai/bounded-contexts.md` rule 3). The unpaid streak and the history are **read** from cycles; the
subscription never loads them.

**One aggregate mutated per command**, with one recorded exception (ADR-240). **Recording a processed
cycle** creates the cycle row and advances the subscription in **one** `SaveChangesAsync`, which is
atomic (F23). Nothing else crosses the line. Order outcomes update the cycle only. Suspensions,
resumptions and reinstatements update the subscription only.

Cross-aggregate reads go through the §8.12 ports or through repository calls in the handler: the SKU,
the cat's owner and life status, the address book, the pickup branch, the authorisation.

### 8.2 `SubscriptionAggregate`

| Field | Type | Notes |
|---|---|---|
| `CustomerId` | Guid | The owner. **Not branch-scoped** — a customer's subscription is theirs at any branch (HO-ADM-001 §8.2 rule) |
| `SkuId` | Guid | HO-SHP-001's SKU |
| `Product` | owned `SubscribedProduct` | `ProductName` (≤ 200), `VariantLabel` (≤ 200), `IsPrescriptionDiet` — **display snapshot at creation**. Reads prefer SHP's current names (§12.6) |
| `CatId?` | Guid? | **Required iff `IsPrescriptionDiet`** (FR-SHP-11: the cat is recorded on the order line); refused otherwise (`CatNotAllowedForProduct`) — nothing else would use it |
| `Terms` | owned `SubscriptionTerms` | §8.3 |
| `PendingTerms?` | owned `SubscriptionTerms` | Terms that take effect **after** the locked cycle (§8.5) |
| `PendingSkip` | bool | Skip the cycle **after** the locked one |
| `PendingStatusChange?` | `SubscriptionPendingStatusChange?` | `Pause` or `Cancel`, applied after the locked cycle |
| `Status` | `SubscriptionStatus` | `Active`, `Paused`, `Suspended`, `Cancelled` — Appendix C |
| `SuspensionReason?`, `SuspendedOn?` | enum / instant | §10.1 |
| `NextDeliveryOn?` | `DateOnly?` | The date the next cycle's order is raised — **"next delivery date"** in the backlog's words (ADR-239, OQ-3). **Set only while `Active`**; null otherwise |
| `NextDeliverySetOn?` | `DateOnly?` | The local date `NextDeliveryOn` was last set **by a person** (create, resume, reinstate, interval change). Suppresses a pre-charge notice the person has just been shown (§8.8) |
| `StartedOn` | `DateOnly` | First delivery date |
| `LastCycleOn?` | `DateOnly?` | Last processed cycle date, generated or failed |
| `LastSkippedOn?` | `DateOnly?` | For display |
| `PausedOn?` | instant | |
| `CancelledOn?`, `CancelledBy?`, `CancellationReason?`, `CancellationNote?` (≤ 500, plain text) | | Reason from §10.1; the note is free text and **never** goes into an event or a log |
| `ReinstatedOn?`, `ReinstatedBy?`, `ReinstatementReason?` (≤ 500) | | Staff reinstatement (US-SUB-06) |

### 8.3 `SubscriptionTerms` (value object) — US-SUB-01, US-SUB-02

`sealed record`, private constructor, `Create` throws `DomainRuleViolationException`:

| Field | Rule |
|---|---|
| `Quantity` | 1 … `SubscriptionRules.MaxQuantity` (20) → `InvalidSubscriptionQuantity` |
| `IntervalWeeks` | In `SubscriptionRules.AllowedIntervalWeeks` = **{2, 4, 6, 8, 12}** (OQ-2) → `InvalidSubscriptionInterval` |
| `Fulfilment` | `Delivery` \| `Pickup` |
| `DeliveryAddressId?` | Required for `Delivery`, null for `Pickup` → `FulfilmentDetailsInvalid` |
| `PickupBranchId?` | Required for `Pickup`, null for `Delivery` → same |
| `PaymentMethod` | `CashOnDelivery` \| `BankTransfer` \| `PayAtPickup`. `PayAtPickup` only with `Pickup`; `CashOnDelivery` only with `Delivery` → `PaymentMethodNotAllowedForFulfilment`. `BankTransfer` with either |

Record equality makes "no change" detectable (`NoChangeRequested`). **Whether a method is currently
available** (COD disabled for the zone, above the ceiling — BR-15) is **not** a terms rule. It is asked
of Orders (C-ORD-2) by the handler (§11.4).

### 8.4 The status machine — Appendix C — ADR-239

```
            ┌──── Pause (unlocked) ────┐
            ▼                          │
Create ─► Active ◄── Resume ──────── Paused
            │  ▲                        │
            │  └── ResumeAfterAuthorisation / Reinstate ── Suspended ◄─┐
            │                                                          │
            ├── Suspend(reason) ───────────────────────────────────────┘ (from Active or Paused)
            └── Cancel ─► Cancelled   (from Active, Paused or Suspended; terminal)
Locked cycle: Pause / Cancel become PendingStatusChange and apply in RecordCycleProcessed
```

| Method | From | Rules | Event |
|---|---|---|---|
| `Create(dto, clock)` | — | Terms valid; `FirstDeliveryOn` (default `clock.EarliestDeliveryOn`) within `[EarliestDeliveryOn, Today + MaxFirstDeliveryDays]` → else `InvalidFirstDeliveryDate`; `NextDeliveryOn = StartedOn = FirstDeliveryOn`; `NextDeliverySetOn = Today` | `SubscriptionCreatedEvent` |
| `ChangeTerms(terms, clock)` | `Active`, `Paused` | §8.5 | `SubscriptionTermsChangedEvent` (`AppliedImmediately`, `AppliesFromCycleOn?`) |
| `SkipDelivery(cycleOn, clock)` | `Active` | §8.5 | `SubscriptionDeliverySkippedEvent` |
| `Pause(clock)` | `Active` | §8.5 | `SubscriptionPausedEvent` / `SubscriptionChangeScheduledEvent` |
| `Resume(firstDeliveryOn?, clock)` | `Paused` | `NextDeliveryOn = firstDeliveryOn ?? EarliestDeliveryOn`, validated as in `Create`; `NextDeliverySetOn = Today`. From `Suspended` → **`SubscriptionSuspended`** (a customer cannot resume a suspension — §8.9, §8.10) | `SubscriptionResumedEvent` (`Trigger = Customer`) |
| `Cancel(by, reason, note, clock)` | `Active`, `Paused`, `Suspended` | `Active` **and locked** → `PendingStatusChange = Cancel` (the locked order still goes). Otherwise `Cancelled` now; `NextDeliveryOn = null`; pendings cleared | `SubscriptionCancelledEvent` / `SubscriptionChangeScheduledEvent` |
| `WithdrawPendingChanges()` | any non-cancelled | Clears `PendingTerms`, `PendingSkip`, `PendingStatusChange`; nothing pending → `NoPendingChange` | `SubscriptionPendingChangesWithdrawnEvent` |
| `RecordCycleProcessed(cycleOn, outcome, clock)` | `Active` | §8.6 | `SubscriptionCycleProcessedEvent` |
| `Suspend(reason, now)` | `Active`, `Paused` | `Suspended`, `SuspensionReason`, `SuspendedOn`; `NextDeliveryOn = null`; pendings cleared. **Already `Suspended` → no event, no version bump** (idempotent for event handlers) | `SubscriptionSuspendedEvent` |
| `ResumeAfterAuthorisation(clock)` | `Suspended` with `AuthorisationExpired` only → else `InvalidSubscriptionTransition` | `Active`; `NextDeliveryOn = EarliestDeliveryOn`; `NextDeliverySetOn = Today` | `SubscriptionResumedEvent` (`Trigger = AuthorisationRenewed`) |
| `Reinstate(by, reason, firstDeliveryOn?, clock)` | `Suspended` (any reason except `CatDeceased` → `SubscriptionCannotBeReinstated`) | Reason non-blank ≤ 500 → `ReinstatementReasonRequired`; as `Resume` | `SubscriptionReinstatedEvent` |
| `ReassignCustomerOnMerge(dto)` / `CancelAsMergeDuplicate(dto)` / `CancelOnAccountDeletion(now)` | any | ADR-247 | `SubscriptionCustomerReassignedEvent` / `SubscriptionCancelledEvent` |

Any other transition → **`InvalidSubscriptionTransition`**, with a message naming the state (*"A
cancelled subscription cannot be paused."*). Every mutation ends `UpdateVersion()`.

### 8.5 The schedule, the cut-off and pending changes — US-SUB-02 — ADR-239

**Dates are local dates in the commerce time zone**, which is the **main branch's** IANA zone
(`AdministrationSeed.MainBranchId`), exactly as HO-NTF-001's care-reminder sweep defines "today".
Subscriptions are not branch-scoped, so one zone governs all of them (OQ-15).

**`SubscriptionClock`** (value object built by the handler, the **only** place a time-zone conversion
happens in this slice, through HO-ADM-001's time model — ADR-045):

```
SubscriptionClock.Create(now: DateTimeOffset, zone: TimeZoneInfo, cutoffDays: int)
  Today                    = local date of now in zone
  CutoffInstant(cycleOn)   = local midnight at the start of (cycleOn − cutoffDays), as an instant
  IsLocked(cycleOn)        = now ≥ CutoffInstant(cycleOn)
  EarliestDeliveryOn       = Today + cutoffDays + 1          // its own cut-off is still in the future
```

**The locked cycle.** Once `clock.IsLocked(NextDeliveryOn)` is true, the upcoming cycle is committed.
Its order is raised with the terms current at that moment, and changes are scheduled after it.
`SubscriptionSchedule` (Domain.DomainServices, **pure**) owns the rules below; the aggregate calls it.

| Change | Unlocked (before the cut-off) | Locked (after the cut-off) |
|---|---|---|
| **Quantity, fulfilment, payment method** | `Terms = new`. `AppliesFromCycleOn = NextDeliveryOn` | `PendingTerms = new` (replaces any earlier pending terms). `AppliesFromCycleOn = FollowingCycleOn` |
| **Interval** | `Terms = new`. If `LastCycleOn` is null (no cycle yet) `NextDeliveryOn` is unchanged; else `NextDeliveryOn = max(LastCycleOn + 7 × newInterval, EarliestDeliveryOn)`, and `NextDeliverySetOn = Today`. `AppliesFromCycleOn = NextDeliveryOn` | `PendingTerms`. `AppliesFromCycleOn = NextDeliveryOn + 7 × newInterval` |
| **Skip** `(cycleOn)` | `cycleOn` must equal `NextDeliveryOn` → `NextDeliveryOn += 7 × interval`; `LastSkippedOn = cycleOn` | `cycleOn` must equal `FollowingCycleOn` → `PendingSkip = true` (already true → `SkipAlreadyScheduled`) |
| **Pause** | `Paused`, `NextDeliveryOn = null` | `PendingStatusChange = Pause`. `AppliesFromCycleOn = FollowingCycleOn` (the first cycle that will not be raised) |
| **Cancel** | `Cancelled` | `PendingStatusChange = Cancel`; the locked order still goes |

`FollowingCycleOn = NextDeliveryOn + 7 × (PendingTerms?.IntervalWeeks ?? Terms.IntervalWeeks)`.
A skip whose `cycleOn` matches neither rule → **`CycleNoLongerSkippable`**, message naming the next
skippable date (*"The delivery on 20 Oct can no longer be skipped. You can skip the one on 17 Nov."*).
That covers a stale skip link from an old notice.

**Paused subscriptions** accept `ChangeTerms` (always immediate — there is no locked cycle) and
`Cancel`. `Skip` and `Pause` → `InvalidSubscriptionTransition`.

**The change result.** `SubscriptionSchedule.Describe(outcome)` builds the plain-language `message`
the API returns, in the commerce zone, formatted `ddd d MMM`:
- immediate: *"Done. Your next delivery on Tue 20 Oct will be 3 × Royal Canin Kitten 2 kg."*
- scheduled: *"Your delivery on Tue 20 Oct is already being prepared, so this change starts with the
  delivery on Tue 17 Nov."* — **US-SUB-02 AC2**.

### 8.6 Processing a cycle — US-SUB-01 AC2 — ADR-240, ADR-241

`RecordCycleProcessed(cycleOn, outcome, clock)`, called from C8 only:

1. `Status == Active` and `NextDeliveryOn == cycleOn` → else `CycleNotDue` (the handler turns a
   re-record of an already processed cycle into a success, §11.4).
2. `LastCycleOn = cycleOn`.
3. `PendingTerms` → `Terms`; cleared.
4. `NextDeliveryOn = cycleOn + 7 × Terms.IntervalWeeks`, **plus one more interval if `PendingSkip`**
   (`LastSkippedOn` = the skipped date); `PendingSkip = false`.
5. `PendingStatusChange`: `Pause` → `Paused`, `NextDeliveryOn = null`. `Cancel` → `Cancelled` with
   `CancellationReason` as recorded when scheduled. Cleared.
6. `outcome` is a failure with `AuthorisationExpired` or `ProductUnavailable`, and the subscription is
   still `Active` → `Suspend(reason)` in the same call. **A pending cancellation wins over a
   suspension.**

A failed cycle **consumes** the cycle: the subscription moves on to the next date, and the customer is
told what failed (`subscription.cycle-failed`). Out-of-stock cycles are **not retried** (OQ-4).

### 8.7 `SubscriptionCycleAggregate` — US-SUB-01 AC2, US-SUB-04, US-SUB-06

| Field | Notes |
|---|---|
| `SubscriptionId`, `CustomerId`, `CycleOn` (`DateOnly`) | **Unique `(subscription_id, cycle_on)`** — `ix_subscription_cycle_per_date` |
| `SkuId`, `Quantity`, `CatId?`, `Fulfilment`, `PaymentMethod` | The terms **as sent** to Orders |
| `DiscountPercent` | `numeric(5,2)` — the setting's value at generation (US-SUB-04), snapshotted (HO-ADM-001 ADR-049) |
| `IdempotencyKey` | `sub:{subscriptionId:N}:{cycleOn:yyyyMMdd}` — what Orders received |
| `Outcome` | `Generated` \| `Failed` |
| `OrderId?`, `OrderNumber?` (≤ 40) | `Generated` only. **Unique where not null** |
| `OrderState?` | `SubscriptionCycleOrderState` — `AwaitingPayment`, `Confirmed`, `Expired`, `Cancelled`, `Delivered`, `Refused`; the mirror (ADR-243) |
| `PaymentExpiresOn?` | From Orders, for a held bank-transfer order |
| `FailureReason?`, `FailureCode?` (≤ 100) | `Failed` only; the raw Orders code is kept for support |
| `ProcessedOn`, `OrderStateChangedOn?` | |

| Method | Rules | Event |
|---|---|---|
| `RecordGenerated(dto)` / `RecordFailed(dto)` | Creation only | `SubscriptionOrderGeneratedEvent` / `SubscriptionCycleFailedEvent` |
| `RecordOrderState(state, at)` | Allowed: `AwaitingPayment → Confirmed \| Expired \| Cancelled`; `Confirmed → Delivered \| Cancelled \| Refused`. **Same state → no-op, no event** (redelivery). Anything else → ignored, logged at `Warning` with ids only (Orders is the authority; the mirror never refuses a fact) | `SubscriptionCycleOrderStateChangedEvent` |
| `ReassignCustomer(survivorId)` | Merge only | — |

`IsUnpaid` ⇔ `OrderState ∈ {Expired, Refused}` (Refused = a COD parcel refused on delivery, OQ-5).

### 8.8 Pre-charge notices — US-SUB-03 — ADR-245

`SubscriptionSchedule.NoticeDateFor(cycleOn, noticeDays, cutoffDays) = cycleOn − max(noticeDays,
cutoffDays + 1)`. The notice therefore always lands **before** the cut-off, so its skip link works.

A pass in the worker (§12.2) selects `Active` subscriptions where `NoticeDate ≤ Today < NextDeliveryOn`
**and** `NextDeliverySetOn < NoticeDate`. The second condition means someone who has just chosen the
date, and received `subscription.created` or a change result naming it, is not re-told. For each row
the pass dispatches **`RequestNotificationCommand`** (HO-NTF-001 rule 3, a timed producer):

- template `subscription.pre-charge`;
- subject `SubscriptionCycleDue`, `SubjectId = subscriptionId`, `SubjectDueOn = NextDeliveryOn`;
- `DeduplicationKey = "sub:notice:{subscriptionId:N}:{cycleOn:yyyyMMdd}"`, so it is sent **once**;
- `NotAfter` = the cycle's cut-off instant. A notice held by quiet hours past the cut-off expires
  instead of offering a skip that no longer works.

`SubscriptionCycleDueGuard` (HO-NTF-001 §8.7): valid while the subscription exists, is `Active`,
`CustomerId == RecipientId`, and `NextDeliveryOn == SubjectDueOn`. A skip, pause, cancel, suspension
or interval change between enqueue and send fails the guard: `Cancelled SubjectNoLongerApplies`.

### 8.9 Unpaid cycles — US-SUB-06 — ADR-243, ADR-246

**Reminder.** When C8 records a `Generated` cycle whose order is `AwaitingPayment` with a
`PaymentExpiresOn`, and `subscription.paymentReminderHours > 0`, it enqueues
`subscription.payment-reminder` **in the same save** with:
- `SendAfter = PaymentExpiresOn − paymentReminderHours` (not enqueued if that is already past);
- `NotAfter = PaymentExpiresOn`;
- subject `SubscriptionOrderAwaitingPayment`, `SubjectId = cycleId`.

`SubscriptionPaymentDueGuard`: valid while the cycle's mirrored `OrderState == AwaitingPayment`. A
transfer confirmed in the meantime fails the guard. The customer is never chased for money they have
paid. The **expiry** itself, the stock release and the expiry message are Orders' (FR-ORD-22).

**The streak.** `UnpaidCycleRule.Streak(cycles)` (Domain.DomainServices, **pure**) takes the most recent
cycles in descending `CycleOn`:
1. Ignores `Failed` cycles and cycles still `AwaitingPayment`: neither paid nor unpaid yet.
2. Ignores `Cancelled`. A customer who cancelled an order before paying did not default.
3. Counts consecutive `IsUnpaid` from the most recent. A `Confirmed` or `Delivered` cycle ends the
   streak.

**Suspension.** The worker's unpaid pass selects `Active` subscriptions whose streak is ≥
`subscription.maxConsecutiveUnpaidCycles` and dispatches C9 (`UnpaidCycles`). C9 **re-evaluates the
rule** in the handler before suspending (the pass only nominates), then enqueues
`subscription.suspended`. **Only staff can reinstate** an `UnpaidCycles` suspension (C11, OQ-6). The
customer can still cancel.

### 8.10 Prescription diets — US-SUB-05 — ADR-244

| When | Check | On failure |
|---|---|---|
| **Create** (C1) | Cat exists, alive, owned by the caller (`404` otherwise); `IDietAuthorisationGate.CheckAsync(customerId, catId, skuId)` returns `Authorised` or `NotRequired` (C-PHM-1) | `400 PrescriptionAuthorisationRequired` |
| **Every worker pass** — `Active` subscriptions with `CatId` | The same call | C9 `AuthorisationExpired`. C9 **re-checks** in the handler, suspends, and enqueues `subscription.suspended` to the customer and `subscription.authorisation-lapsed` to the **issuing vet** (C-PHM-1's `IssuedByStaffId`) — US-SUB-05 AC1 "customer and clinic notified" |
| **Every worker pass** — `Suspended` with `AuthorisationExpired` | The same call returns true | C10 → `ResumeAfterAuthorisation`; enqueues `subscription.resumed` — **US-SUB-05 AC2 "without needing to be recreated"** |
| **Generation** | Orders' own prescription gate (BR-2, FR-SHP-11) inside C-ORD-1 | Refusal mapped to `AuthorisationExpired` → the cycle fails and the subscription suspends (§8.6 step 6), with the same two messages |

The pass runs every `Subscriptions:Worker:IntervalSeconds` (900). An authorisation that lapses at
23:59 is therefore caught within 15 minutes. Because generation re-checks anyway, a pass missed during
downtime still cannot ship an unauthorised diet — "suspends **before** the next generation" is
guaranteed by the order of the passes (§12.2: authorisation first, generation last).

**A paused prescription subscription is not suspended.** Nothing ships while paused. On `Resume` the
handler checks the authorisation, and if it has lapsed the resume is refused with
`PrescriptionAuthorisationRequired` and a link text to book a consultation.

### 8.11 Messages — ADR-245, ADR-246

Templates appended to HO-NTF-001's catalogue. All are **Transactional**: they concern an order the
customer asked for, and the prescription ones are medical (FR-NTF-4). All carry `InApp` except the
staff one.

| Key | Urgency | Channels | Recipient | Enqueued by | Producer variables (required on) |
|---|---|---|---|---|---|
| `subscription.created` | **Urgent** — the customer did it seconds ago | Sms, Email, InApp | Customer | C1 | `productName`, `quantity`, `intervalWeeks`, `firstDeliveryOn` (Date), `paymentMethodText`, `manageLink` (Url; every channel) |
| `subscription.changed` | Normal | **InApp only** | Customer | C2–C5, C7 | `productName`, `changeMessage` (the §8.5 `message`), `manageLink` (Url) |
| `subscription.cancelled` | **Urgent** | Sms, Email, InApp | Customer | C6 (customer or staff) | `productName`, `endsText` (*"now"* / *"after your delivery on Tue 20 Oct"*), `subscriptionsLink` (Url) |
| `subscription.pre-charge` | Normal | Sms, Email, InApp | Customer | Worker (§8.8) | `productName`, `quantity`, `deliveryOn` (Date), `paymentMethodText`, `estimatedAmount`, `skipLink` (Url; every channel), `manageLink` (Url) |
| `subscription.cycle-failed` | Normal | Sms, Email, InApp | Customer | C8 (failure) | `productName`, `cycleOn` (Date), `reasonText`, `manageLink` (Url) |
| `subscription.payment-reminder` | Normal | Sms, Email, InApp | Customer | C8 (§8.9, `SendAfter`) | `productName`, `amountDue`, `payByLocal`, `orderLink` (Url; every channel) |
| `subscription.suspended` | Normal | Sms, Email, InApp | Customer | C8 (§8.6 step 6), C9 | `productName`, `reasonText`, `manageLink` (Url) |
| `subscription.resumed` | Normal | Sms, Email, InApp | Customer | C10, C11 | `productName`, `nextDeliveryOn` (Date), `manageLink` (Url) |
| `subscription.authorisation-lapsed` | Normal | Sms, Email | **Staff** (issuing vet) | C8 / C9 (`AuthorisationExpired`) | `catName`, `productName`, `customerName`, `staffLink` (Url) |

Rules:
- `paymentMethodText`, `reasonText`, `endsText`, `changeMessage`, `estimatedAmount`, `amountDue` and
  `payByLocal` are **display text** from `SubscriptionMessages` and `SubscriptionSchedule.Describe`.
  None of them carries a decision (HO-NTF-001 rule 4). Money is formatted `BDT 1,450.00` from the
  string amount; `payByLocal` is `ddd d MMM yyyy, HH:mm` in the commerce zone.
- Nothing about the cat or the diet goes into a message beyond `catName` and `productName`. **No
  authorisation dates, diagnosis or clinical text.**
- **Links** (`INotificationLinkBuilder`, new methods): `SubscriptionLink(id)` →
  `{WebBaseUrl}/portal/subscriptions/{id}`; `SubscriptionSkipLink(id, cycleOn)` →
  `…/portal/subscriptions/{id}?skip={yyyy-MM-dd}`; `SubscriptionsLink()` → `…/portal/subscriptions`;
  `StaffSubscriptionLink(id)` → `{WebBaseUrl}/staff/subscriptions/{id}`. **`OrderLink(orderId)` is
  HO-ORD-001's** (C-ORD-4).

### 8.12 Contracts assumed of handovers that do not exist yet

These are **assumptions**, written so the implementer can check them against the real handover in
one read. Each is a port or a message owned by the **other** area. This slice consumes them and never
implements them. If the real contract differs in name only, record the mapping in §1 Amended and carry
on. If it differs in substance (a missing field, a different guarantee), it is a **§20.2 stop**.

| ID | Owner | Assumed contract | Used by |
|---|---|---|---|
| **C-SHP-1** | **HO-SHP-001** | `ISkuSaleCatalogue.GetAsync(Guid skuId, CancellationToken)` → `SkuSaleInfo?` with `SkuId`, `ProductId`, `ProductName`, `VariantLabel`, `CategoryGroup` (at least distinguishing **Cat Food** and **Litter** from the other FR-SHP-1 groups), `IsActive` (product active **and** SKU sellable), `PrescriptionRequired`, `UnitPrice` (`Money`, current), `PrimaryImageUrl?` | C1, C2, options and estimate reads |
| **C-PHM-1** | **HO-PHM-001** §8.10 | **`IDietAuthorisationGate.CheckAsync(Guid customerId, Guid catId, Guid skuId, CancellationToken)`** → `DietGateResult(Outcome, AuthorisationId?, ValidTo?, Message)`, where `Outcome` is `NotRequired \| Authorised \| NoAuthorisation \| AuthorisationExpired \| CatNotOwned \| CatDeceased`. **One call replaces the two this slice assumed**: the result carries `ValidTo`, so no separate `GetCurrentAsync` is needed. `IssuedByStaffId` is **not** returned — see OQ-16. "Today" is recomputed per call, so a lapsed authorisation is caught on the cycle pass without a sweep | C1, C5, C9, C10, C11, worker pass |
| **C-ORD-1** | **HO-ORD-001** | **`PlaceOrderCommand`** dispatched by the worker in its own scope: `OrderId` (caller-minted), `CustomerId`, `Channel = Subscription`, **`IdempotencyKey`**, `Lines[] { SkuId, Quantity, CatId?, LineDiscount? { Percent, Label } }`, `Fulfilment` (`Delivery { DeliveryAddressId }` \| `Pickup { BranchId }`), `PaymentMethod` (`CashOnDelivery` \| `BankTransfer` \| Orders' pickup pay-in-store value), `SourceReference { Kind = "Subscription", SubscriptionId, CycleOn }`, **no interactive price-change acknowledgement** (system-placed, ADR-242). Success → `{ orderId, orderNumber, status: PendingPayment \| Confirmed, paymentExpiresOn?, total }`. Refusal → `400` with a violation code. **The same `IdempotencyKey` returns the original order and places nothing** (FR-ORD-20). Stock is reserved by batch inside it (FR-ORD-9, FR-IMP-7) | Worker (§12.2) |
| **C-ORD-2** | HO-ORD-001 | `GetAvailablePaymentMethodsQuery(CustomerId, DeliveryAddressId?, PickupBranchId?, OrderValue)` → `[{ method, isAvailable, unavailableReason? }]` — the BR-15 decision (COD per zone and ceiling) | C1, C2, payment-methods read |
| **C-ORD-3** | HO-ORD-001 | Domain events carrying `OrderId`: **`OrderConfirmedEvent`** (transfer confirmed or COD accepted), **`OrderPaymentExpiredEvent`** (FR-ORD-22 window passed, stock released), **`OrderCancelledEvent`**, **`OrderDeliveredEvent`**, **`OrderReturnedUndeliveredEvent`** (COD refused or undelivered — FR-ORD-21) | §11.6 mirror handlers |
| **C-ORD-4** | HO-ORD-001 | `INotificationLinkBuilder.OrderLink(orderId)` → the customer's order page, which shows the bank details and reference (FR-ORD-22) | `subscription.payment-reminder` |

**Refusal mapping** (`SubscriptionFailureClassifier`, Shared, pure). Orders' codes, as named by
HO-ORD-001, map to `SubscriptionCycleFailureReason`:

| Orders refusal (assumed meaning) | Reason | Subscription effect |
|---|---|---|
| Insufficient sellable stock | `OutOfStock` | Cycle consumed; customer notified |
| SKU inactive / archived / not sellable | `ProductUnavailable` | **Suspended** `ProductUnavailable` |
| Prescription authorisation missing or expired | `AuthorisationExpired` | **Suspended** `AuthorisationExpired`; customer and vet notified |
| COD unavailable for zone or value (BR-15) | `PaymentMethodUnavailable` | Cycle consumed; customer told to choose another method |
| Address outside every zone; pickup branch inactive | `DeliveryUnavailable` | Cycle consumed; customer notified |
| Delivery address not found | `DeliveryAddressMissing` | Cycle consumed; customer notified |
| Any other `400` | `OrderRefused` | Cycle consumed; the raw code kept in `FailureCode` |

An exception or `5xx` from Orders is **not** a refusal. Nothing is recorded, the error is logged with
ids, and the next pass retries under the same idempotency key (§20.7).

### 8.13 Ports and domain services

| Name | Kind | Notes |
|---|---|---|
| `SubscriptionClock` | value object | §8.5 — the one conversion point |
| `SubscriptionSchedule` | pure domain service | §8.5, §8.8 — locked/unlocked rules, following cycle, notice date, `Describe` |
| `UnpaidCycleRule` | pure domain service | §8.9 |
| `SubscriptionEstimate` | pure domain service | `unitPrice × quantity`, discount rounded **half-up to the minor unit**, all in **integer minor units** (C-6, US-ORD-04 AC2). Labelled an estimate; Orders' total governs |
| `SubscriptionFailureClassifier` | pure, Shared | §8.12 |
| `ICommerceClock` | port, Infrastructure | Loads the main branch's zone and `subscription.changeCutoffDays`, returns a `SubscriptionClock` for `TimeProvider.GetUtcNow()`. Used by every handler and the worker |
| `SubscriptionCycleDueGuard`, `SubscriptionPaymentDueGuard` | `INotificationSubjectGuard` | §8.8, §8.9 |
| C-SHP-1, C-PHM-1, C-ORD-2 | **other areas' ports** | Consumed, not implemented |

### 8.14 Exceptions

Reuse `DomainRuleViolationException`. No new exception type.

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. **This handover owns ADR-237 … ADR-256** (range reserved by the
coordinator); it uses **ADR-237 … ADR-249**. ADR-250 … ADR-256 stay free for amendments to this slice.

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-237** | **Subscriptions are part of the Order bounded context** (per `.ai/bounded-contexts.md`), with feature segment `Subscription` and their **own schema `subscription`**. They hold orders, SKUs, cats and addresses by id, and never read or write another area's tables except through §8.12's contracts | The schema split looks like a context split unless recorded. It is a delivery convenience while HO-ORD-001 is written in parallel (OQ-1) |
| **ADR-238** | **No auto-charge in v1.** A cycle raises an ordinary order paid by COD, bank transfer or at pickup. No token, card, mandate or gateway field exists anywhere; `SubscriptionPaymentMethod` has no online value; there is no charge retry. Phase 5 adds a method and a charging step behind the same cycle record | FR-SUB-2, C-5, D-12, backlog §18.5. An "auto-charge" flag with nothing behind it is a state no code can honour |
| **ADR-239** | **Schedule model**: cycles are local dates in the main branch's zone; `NextDeliveryOn` is the date the cycle's order is raised; the cut-off is local midnight `cutoffDays` before it; after the cut-off, changes become **pending** and apply from the following cycle; every change response names the cycle. `SubscriptionClock` is the only conversion point (extends HO-ADM-001 ADR-045) | FR-SUB-3 and US-SUB-02 AC2; "next delivery date" is ambiguous without a gateway-less fulfilment lead time (OQ-3) |
| **ADR-240** | **Two roots**: `SubscriptionAggregate` and `SubscriptionCycleAggregate` (unbounded history, rule 3). **Recording a processed cycle writes the new cycle and the advanced subscription in one save** — a recorded rule-1 exception, atomic by F23 | The cycle and the subscription's next date must never disagree. Two saves would leave a window where a retry creates a second cycle or skips one |
| **ADR-241** | **Generation is orchestrated by the worker, one scope per command**: this slice's query builds the request, then HO-ORD-001's `PlaceOrderCommand` runs in its own scope with `IdempotencyKey = sub:{id:N}:{yyyyMMdd}`, then `RecordSubscriptionCycleCommand` runs in a third scope. Recovery from a crash between steps is **re-running the pass**; the key and `ix_subscription_cycle_per_date` make it exactly-once in effect. **Never dispatch Orders' command from inside this slice's handler** | F23: in one scope, Orders' save would commit this slice's half-built tracked entities. Placing an order and recording it are two aggregates in two contexts |
| **ADR-242** | **Price at generation**: the subscription stores no price; each order is priced by Orders at generation (HO-ADM-001 ADR-049). The subscription discount is a **setting** (`subscription.discountPercent`), passed as a labelled line discount and snapshotted on the cycle. System-placed orders carry no interactive price-change acknowledgement (FR-ORD-3); the pre-charge notice carries the current estimate instead | FR-SUB-5; a price frozen at subscription time would be a promise nobody approved (OQ-11) |
| **ADR-243** | **Order outcomes are mirrored, not owned**: idempotent handlers of Orders' events update the cycle's `OrderState`; they never enqueue and never touch the subscription. The **unpaid streak is derived** from cycles by `UnpaidCycleRule`. Suspension for non-payment is a worker-nominated **command** that re-checks the rule and sends the notice | `.ai/domain-events-and-outbox.md` §3 and HO-NTF-001 ADR-057; a stored counter drifts on redelivered events |
| **ADR-244** | **Prescription-diet authorisation is checked at creation, on every worker pass (suspend if lapsed, resume if renewed), and by Orders at generation.** Suspension and resumption are commands that send the customer and issuing-vet messages. **This deviates from `.ai/flows/prescription-diet-flow.md`**, which reacts to a `DietAuthorisationExpiredIntegrationEvent`: an event handler may not enqueue (ADR-057), FR-SUB-6 requires the notice with the change, and the flow already names `IsAuthorisedAsync` as the contract. Subscribing to the event later is additive | Deviating from a governing flow doc silently would be a gate finding |
| **ADR-245** | **Pre-charge notice**: a timed producer in the worker; `RequestNotificationCommand` with key `sub:notice:{id}:{date}`; notice date = cycle − max(noticeDays, cutoffDays + 1); `NotAfter` = the cut-off; guarded by `SubscriptionCycleDue`; not sent when the person set the date inside the notice window | FR-SUB-4 with a skip link that must still work when it arrives |
| **ADR-246** | **Payment reminder** for held bank-transfer subscription orders: enqueued with the cycle record, `SendAfter = expiry − paymentReminderHours`, `NotAfter = expiry`, guarded by the mirrored `AwaitingPayment`. Orders owns the expiry and its message | US-SUB-06 AC1 "reminded and the order expires per FR-ORD-22" — two halves, two owners |
| **ADR-247** | **Lifecycle reactions**: cat marked deceased → suspend `CatDeceased` (never reinstated; the customer cancels); cat transferred → suspend `CatOwnerChanged`; customer merged → reassign to the survivor, or cancel the duplicate's subscription if the survivor already has a live one for the same SKU and cat; account deleted → cancel. Handlers are idempotent and **never enqueue** | Shipping food for a dead or re-homed cat is a harm; the merge contract (HO-ACC-001 ADR-039) is binding |
| **ADR-248** | **Authorization**: `Subscriptions.View`, `Subscriptions.Manage`, `Subscriptions.Export` added to the one `ActionRoles` table, **inferred** from Appendix D "Orders & fulfilment" plus Vet read (OQ-7); customer endpoints resolve by id **and** caller, and another customer's subscription is `404`; an `UnpaidCycles` suspension is lifted by staff only | Appendix D has no subscription row; the inference must be visible to the ACCEPTANCE gate |
| **ADR-249** | **`SubscriptionWorker`**: one `BackgroundService` in the single host (ADR-010), same shape as `NotificationWorker` / `ClinicWorker` — a scope per unit of work, never throws out of `ExecuteAsync`, disabled in `Testing`. Passes in fixed order **authorisation → unpaid → notices → generation**, each bounded by `SubscriptionRules.WorkerBatchSize` (200). NFR-PRF-7's "separate workers" is met as a separate background loop within the one host, bounded per pass | NFR-PRF-7 against ADR-010; the pass order is what guarantees "suspended before the next generation" |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | `.ai/bounded-contexts.md` should record the `subscription` schema, and `.ai/flows/prescription-diet-flow.md` the check-not-event reaction (ADR-244). Merge into Orders' schema once HO-ORD-001 lands? | Architect | ADR-237 and ADR-244 record both; no merge |
| OQ-2 | Which intervals? FR-SUB-1 says "e.g. every 2, 4 or 8 weeks" | Owner | **{2, 4, 6, 8, 12} weeks** (`SubscriptionRules.AllowedIntervalWeeks`) |
| OQ-3 | Should the order be raised **N days before** a target arrival date, rather than on the date shown? | Owner | Raised **on** `NextDeliveryOn`; the parcel then follows Orders' fulfilment. Screens say "next order on" (HO-SUB-002) |
| OQ-4 | Retry an out-of-stock cycle for a few days? | Owner | No — the cycle fails, the customer is told, the next cycle runs as scheduled |
| OQ-5 | Does a refused COD parcel count as an unpaid cycle? | Owner + accountant | **Yes** (`Refused` is unpaid), provided HO-ORD-001 emits C-ORD-3's `OrderReturnedUndeliveredEvent` |
| OQ-6 | Who may lift an `UnpaidCycles` suspension, and may the customer do it themselves (e.g. by switching to COD)? | Owner | Staff only — `Subscriptions.Manage` (ShopManager, Administrator) |
| OQ-7 | "The clinic is notified" (US-SUB-05): the issuing vet, a branch inbox, or reception? May vets read subscriptions? | Owner + clinical lead | The **issuing vet** by message, plus the staff list filtered to `AuthorisationExpired`; `Vet` gets `Subscriptions.View` |
| OQ-8 | One global subscription discount, or per product / category? | Owner | One global setting, default **0 %** |
| OQ-9 | Are pickup subscriptions (pay at the counter) wanted? | Owner | **Yes** — FR-ORD-4 allows pickup and the owner lists in-store payment |
| OQ-10 | Subscriptions beyond food and litter (e.g. flea treatment — SRS Appendix E)? | Owner | **No** — FR-SUB-1 says food or litter |
| OQ-11 | A price rise since subscribing: generate at the new price, or ask first? | Owner | Generate at the new price; the pre-charge notice shows the current estimate; the customer can skip or cancel before the cut-off |
| OQ-12 | Pause until a date, resuming automatically? | Owner | No — indefinite pause, manual resume with an optional first date |
| OQ-13 | Limits: quantity per delivery, live subscriptions per customer | Owner | 20 and 20 |
| OQ-14 | Cut-off, notice and reminder defaults | Owner | Cut-off **2 days**, notice **3 days**, payment reminder **24 h** before expiry, suspension after **2** unpaid cycles |
| OQ-15 | One commerce time zone (the main branch's) for every subscription? | Owner | Yes — subscriptions are not branch-scoped; a second-country branch would need this revisited |
| OQ-16 | **US-SUB-05 notifies "the clinic" when a prescription-diet subscription is suspended**, and this slice wanted the issuing vet from the authorisation. HO-PHM-001's `DietGateResult` does not return `IssuedByStaffId` | HO-PHM-001 author | Notify the **branch's clinic list** rather than an individual vet (the default already recorded for OQ-7), or HO-PHM-001 adds the field. Do not widen the gate result just for a notice recipient |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `SubscriptionStatus` | `Active`, `Paused`, `Suspended`, `Cancelled` — exactly `.ai/enumeration-pattern.md` |
| `SubscriptionSuspensionReason` | `AuthorisationExpired`, `UnpaidCycles`, `ProductUnavailable`, `CatDeceased`, `CatOwnerChanged` |
| `SubscriptionFulfilment` | `Delivery`, `Pickup` |
| `SubscriptionPaymentMethod` | `CashOnDelivery`, `BankTransfer`, `PayAtPickup` — **no online value** (ADR-238). Mapped to Orders' payment method at generation |
| `SubscriptionPendingStatusChange` | `Pause`, `Cancel` |
| `SubscriptionCancellationReason` | Customer-chosen: `NoLongerNeeded`, `TooMuchStock`, `CatDislikesIt`, `TooExpensive`, `SwitchingProduct`, `Other`. System: `StaffCancelled`, `CustomerMerged`, `AccountDeleted` — the churn reasons of SRS §8.3 |
| `SubscriptionCycleOutcome` | `Generated`, `Failed` |
| `SubscriptionCycleOrderState` | `AwaitingPayment`, `Confirmed`, `Expired`, `Cancelled`, `Delivered`, `Refused` |
| `SubscriptionCycleFailureReason` | `OutOfStock`, `ProductUnavailable`, `AuthorisationExpired`, `PaymentMethodUnavailable`, `DeliveryUnavailable`, `DeliveryAddressMissing`, `OrderRefused` |
| `SubscriptionResumeTrigger` | `Customer`, `AuthorisationRenewed`, `StaffReinstatement` |

HO-NTF-001's `NotificationSubjectKind` gains **`SubscriptionCycleDue`** and
**`SubscriptionOrderAwaitingPayment`** (append only).

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`, group `Subscriptions`

| Key | Type | Default | Bounds | Branch override | Consumer |
|---|---|---|---|---|---|
| `subscription.changeCutoffDays` | Integer | 2 | 0–7 | ❌ | §8.5 |
| `subscription.preChargeNoticeDays` | Integer | 3 | 1–14 | ❌ | §8.8 (effective lead is never below cut-off + 1) |
| `subscription.discountPercent` | Decimal | 0.00 | 0–50 | ❌ | §8.6, US-SUB-04 |
| `subscription.paymentReminderHours` | Integer | 24 | 0–168 (0 = off) | ❌ | §8.9 |
| `subscription.maxConsecutiveUnpaidCycles` | Integer | 2 | 1–6 | ❌ | §8.9 |

No migration — values are rows (HO-ADM-001 §10.2). The HO-ADM-002 settings screen lists them with no
frontend change, because it renders every registry entry. **That screen is how the shop manager
configures the discount** (US-SUB-04).

### 10.3 Other constants

`SubscriptionRules` (`AllowedIntervalWeeks = {2, 4, 6, 8, 12}`, `MaxQuantity = 20`,
`MaxLiveSubscriptionsPerCustomer = 20`, `MaxFirstDeliveryDays = 90`, `MaxNoteLength = 500`,
`WorkerBatchSize = 200`, `SubscribableCategoryGroups = {CatFood, Litter}`) · `SubscriptionKeys`
(`IdempotencyKey(id, cycleOn)`, `NoticeDeduplicationKey(id, cycleOn)`) · `SubscriptionMessages` ·
`SubscriptionFailureClassifier` · `SystemActors.SubscriptionWorker` (a fixed Guid, appended beside
HO-NTF-001's `NotificationWorker`) · the nine template definitions of §8.11.

### 10.4 Violation codes — append to `BusinessViolationCodes`

`SubscriptionNotFound` · `SkuNotSubscribable` · `InvalidSubscriptionQuantity` ·
`InvalidSubscriptionInterval` · `FulfilmentDetailsInvalid` · `PaymentMethodNotAllowedForFulfilment` ·
`PaymentMethodUnavailable` · `PickupBranchNotAvailable` · `InvalidFirstDeliveryDate` ·
`SubscriptionAlreadyExists` · `SubscriptionLimitReached` · `CatRequiredForPrescriptionDiet` ·
`CatNotAllowedForProduct` · `PrescriptionAuthorisationRequired` · `InvalidSubscriptionTransition` ·
`SubscriptionSuspended` · `SubscriptionCannotBeReinstated` · `ReinstatementReasonRequired` ·
`CancellationReasonRequired` · `NoChangeRequested` · `NoPendingChange` · `CycleNoLongerSkippable` ·
`SkipAlreadyScheduled` · `CycleNotDue`.

`DeliveryAddressNotFound` is **HO-ACC-001's**, reused. `PaymentMethodUnavailable`'s message carries
Orders' `unavailableReason` (*"Cash on delivery isn't available for orders over BDT 10,000."*).
`SubscriptionSuspended`'s message names the reason and what to do (*"This subscription was paused
after two unpaid orders. Please contact the shop to restart it."*).

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Subscription/`

Namespace `CatEcommerce.Application.Commands.Subscription`. Data only; ids minted in the controller or
the worker; `ValidateCommand = true` everywhere (F3).

| # | Command | Sent by | Phase |
|---|---|---|---|
| C1 | `CreateSubscriptionCommand` (`SubscriptionId`, `SkuId`, `Quantity`, `IntervalWeeks`, `FirstDeliveryOn?`, `CatId?`, `Fulfilment`, `DeliveryAddressId?`, `PickupBranchId?`, `PaymentMethod`) | Customer | B |
| C2 | `ChangeSubscriptionTermsCommand` (`SubscriptionId`, `ExpectedVersion`, `Quantity`, `IntervalWeeks`, `Fulfilment`, `DeliveryAddressId?`, `PickupBranchId?`, `PaymentMethod`) | Customer | C |
| C3 | `SkipSubscriptionDeliveryCommand` (`SubscriptionId`, `CycleOn`) | Customer | C |
| C4 | `PauseSubscriptionCommand` (`SubscriptionId`) | Customer | C |
| C5 | `ResumeSubscriptionCommand` (`SubscriptionId`, `FirstDeliveryOn?`) | Customer | C |
| C6 | `CancelSubscriptionCommand` (`SubscriptionId`, `Reason`, `Note?`) | Customer, staff | C |
| C7 | `WithdrawPendingSubscriptionChangesCommand` (`SubscriptionId`) | Customer | C |
| C8 | `RecordSubscriptionCycleCommand` (`SubscriptionCycleId`, `SubscriptionId`, `CycleOn`, `IdempotencyKey`, `DiscountPercent`, then either `Generated { OrderId, OrderNumber, OrderStatus, PaymentExpiresOn? }` or `Failed { FailureCode }`) | Worker | D |
| C9 | `SuspendSubscriptionCommand` (`SubscriptionId`, `Reason` ∈ {`AuthorisationExpired`, `UnpaidCycles`}) | Worker | F, G |
| C10 | `ResumeSubscriptionAfterAuthorisationCommand` (`SubscriptionId`) | Worker | G |
| C11 | `ReinstateSubscriptionCommand` (`SubscriptionId`, `Reason`, `FirstDeliveryOn?`) | Staff | F |

The pre-charge notices use HO-NTF-001's `RequestNotificationCommand`. The order is placed with
HO-ORD-001's `PlaceOrderCommand` (C-ORD-1). Both are dispatched by the worker, not defined here.

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: ids non-empty; `Quantity` 1–20; `IntervalWeeks` in the allowed set; enum membership;
`Fulfilment` and its id consistent; `Note` / `Reason` lengths; C6 `Reason` required, and for a
**staff** caller it must be `StaffCancelled` with a non-blank note (`CancellationReasonRequired`); C11
reason non-blank. Date windows (earliest, cut-off, horizon) are the aggregate's, because they need the
commerce clock.

### 11.3 Mappers and DTOs

`SubscriptionMapper`, `SubscriptionCycleMapper` (static `ToDomainDto()` extensions). DTOs in
`Domain/Models/`: `CreateSubscriptionDto`, `SubscriptionTermsDto`, `SubscriptionChangeOutcome`
(`AppliedImmediately`, `AppliesFromCycleOn?`, `Message`), `CycleGeneratedDto`, `CycleFailedDto`,
`SubscriptionMergeDto`, `SubscriptionOrderRequest` (what the worker hands to C-ORD-1).

### 11.4 Command handlers — `CommandHandlers/Subscription/`

Every customer handler first loads the subscription **by id and `CustomerId == caller`**. Not found or
not owned → **`404 SubscriptionNotFound`**. Every handler obtains the clock from `ICommerceClock`.

| Handler | Specifics |
|---|---|
| **C1 Create** | C-SHP-1: SKU exists → else `404`; `IsActive` and `CategoryGroup ∈ SubscribableCategoryGroups` → else `SkuNotSubscribable` (message names why: inactive, or not food/litter). `PrescriptionRequired` → `CatId` required (`CatRequiredForPrescriptionDiet`), cat alive and owned by caller (`404` otherwise), C-PHM-1 authorised (`PrescriptionAuthorisationRequired`); else `CatId` must be null. Delivery: address in the caller's `CustomerAddressBookAggregate` (`DeliveryAddressNotFound`). Pickup: branch active (`PickupBranchNotAvailable`). C-ORD-2 for the terms' method at `SubscriptionEstimate` value → `PaymentMethodUnavailable` with Orders' reason. Live count < 20 (`SubscriptionLimitReached`); no live subscription for the same SKU and cat (pre-check → `SubscriptionAlreadyExists`; the index catches the race). `Create`; enqueue `subscription.created`; `SaveAsync`. Returns the view (§12.4) |
| **C2 Change terms** | `ExpectedVersion` (`409` on a stale screen). Same address / branch / C-ORD-2 checks for the new terms; quantity × current price for C-ORD-2. `ChangeTerms`; enqueue `subscription.changed` with the outcome message; `UpdateAsync`. Returns `SubscriptionChangeResult` |
| **C3 Skip** | `SkipDelivery(cycleOn, clock)`; enqueue `subscription.changed` |
| **C4 Pause** | `Pause`; enqueue `subscription.changed` |
| **C5 Resume** | Prescription → C-PHM-1 authorised, else `PrescriptionAuthorisationRequired`. `Resume`; enqueue `subscription.changed` |
| **C6 Cancel** | Customer: own. Staff: `Subscriptions.Manage`, any subscription, `StaffCancelled` + note. `Cancel`; enqueue `subscription.cancelled` (`endsText` from the outcome) |
| **C7 Withdraw** | `WithdrawPendingChanges`; enqueue `subscription.changed` |
| **C8 Record cycle** | **Idempotent.** A cycle row for `(SubscriptionId, CycleOn)` already exists → success, nothing written (a retried pass). Load the subscription; if not `Active` or `NextDeliveryOn != CycleOn` and no cycle row exists → log `Warning` with ids and succeed without writing (the subscription changed between the query and the record; the order, if any, stands as an ordinary order and appears on staff's list with `SourceReference`). Otherwise: build the cycle (`RecordGenerated` / `RecordFailed` with the classifier's reason); `RecordCycleProcessed`; enqueue per outcome: failure → `subscription.cycle-failed`, and for `AuthorisationExpired` / `ProductUnavailable` also `subscription.suspended` (+ `subscription.authorisation-lapsed` to the vet); `AwaitingPayment` → `subscription.payment-reminder` (§8.9). **`SaveAsync(cycle)` then `UpdateAsync(subscription)` inside one unit of work — both entities are tracked before the first save, so one `SaveChangesAsync` commits both** (ADR-240, F23). A `23505` on `ix_subscription_cycle_per_date` → success (another pass won). A `409` on the subscription → the whole save rolls back and the next pass retries |
| **C9 Suspend** | `AuthorisationExpired`: re-check C-PHM-1; now authorised → success, no write. `UnpaidCycles`: load the last 10 cycles, `UnpaidCycleRule.Streak ≥ setting` → else success, no write. `Suspend`; enqueue `subscription.suspended` (+ the vet message for authorisation, when C-PHM-1 gives an issuing vet who is an active staff account) |
| **C10 Resume after authorisation** | Re-check C-PHM-1 authorised → else success, no write. `ResumeAfterAuthorisation`; enqueue `subscription.resumed` |
| **C11 Reinstate** | `Subscriptions.Manage`. `AuthorisationExpired` → C-PHM-1 must be authorised (`PrescriptionAuthorisationRequired`). `Reinstate`; enqueue `subscription.resumed` |

### 11.5 Domain events — `Events/Subscription/`

Ids, dates, instants, enums, integers and booleans only. **No note, reason text, product name or
cat name.**

`SubscriptionCreatedEvent` (`SubscriptionId`, `CustomerId`, `SkuId`, `CatId?`, `IntervalWeeks`,
`Quantity`, `PaymentMethod`, `NextDeliveryOn`), `SubscriptionTermsChangedEvent`,
`SubscriptionChangeScheduledEvent`, `SubscriptionDeliverySkippedEvent`, `SubscriptionPausedEvent`,
`SubscriptionResumedEvent` (`Trigger`), `SubscriptionCancelledEvent` (`Reason`),
`SubscriptionPendingChangesWithdrawnEvent`, `SubscriptionCycleProcessedEvent`,
`SubscriptionSuspendedEvent` (`Reason`), `SubscriptionReinstatedEvent`,
`SubscriptionCustomerReassignedEvent`, `SubscriptionOrderGeneratedEvent` (`CycleId`, `OrderId`),
`SubscriptionCycleFailedEvent` (`CycleId`, `Reason`), `SubscriptionCycleOrderStateChangedEvent`.
Nothing inside this slice subscribes to them. They exist for HO-RPT-001 and audit.

### 11.6 Domain event handlers — `DomainEventHandlers/Subscription/`

| Handler | Handles | Does |
|---|---|---|
| `MirrorOrderConfirmedHandler` · `MirrorOrderPaymentExpiredHandler` · `MirrorOrderCancelledHandler` · `MirrorOrderDeliveredHandler` · `MirrorOrderReturnedUndeliveredHandler` | C-ORD-3 events | Load the cycle by `OrderId` (none → return; not a subscription order) → `RecordOrderState` → `UpdateAsync`. Idempotent |
| `SuspendSubscriptionsOnCatDeceasedHandler` | HO-CAT-001 `CatMarkedDeceasedEvent` | Live subscriptions with that `CatId` → `Suspend(CatDeceased)` |
| `SuspendSubscriptionsOnCatTransferredHandler` | HO-CAT-001 `CatOwnershipTransferredEvent` | Live subscriptions with that `CatId` whose `CustomerId` is the previous owner → `Suspend(CatOwnerChanged)` |
| `ReassignSubscriptionsOnCustomerMergedHandler` | HO-ACC-001 `CustomerMergedEvent` | For each subscription of the duplicate: survivor has a live one for the same SKU and cat → `CancelAsMergeDuplicate`; else `ReassignCustomerOnMerge`. Then every cycle of the duplicate → `ReassignCustomer`. Queries for rows **still** on the duplicate, so a re-send finishes a partial run |
| `CancelSubscriptionsOnAccountDeletedHandler` | HO-ACC-001 `CustomerAccountAnonymisedEvent` | Every non-cancelled subscription → `CancelOnAccountDeletion` |

All of them loop with **one `UpdateAsync` per row**, are **idempotent**, and **never enqueue**
(HO-NTF-001 ADR-057). HO-ACC-001's interceptor audits every change with the system or triggering
actor.

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

Two `DbSet`s; one configuration per root in `Persistence/Configurations/`; schema `subscription`; the
HO-ACC-001 §12.1 conventions and the F15 lines; enumerations by `Value`; `DateOnly` → `date`;
`DiscountPercent` → `numeric(5,2)`; owned `Terms` / `PendingTerms` as prefixed columns (`terms_*`,
`pending_terms_*`).

| Index / constraint | Kind |
|---|---|
| `subscription (customer_id, sku_id, cat_id)` **UNIQUE**, `NULLS NOT DISTINCT`, filtered `WHERE status IN (Active, Paused, Suspended)` | `ix_subscription_live_per_sku_cat` (V1) |
| `subscription (status, next_delivery_on)` | due and notice passes |
| `subscription (customer_id, status)` | "my subscriptions", live count |
| `subscription (cat_id)` filtered `WHERE cat_id IS NOT NULL` | authorisation pass, cat events |
| `subscription (status, suspension_reason)` | staff lists |
| `subscription_cycle (subscription_id, cycle_on)` **UNIQUE** | `ix_subscription_cycle_per_date` |
| `subscription_cycle (order_id)` **UNIQUE** filtered `WHERE order_id IS NOT NULL` | `ix_subscription_cycle_order` — mirror lookups |
| `subscription_cycle (customer_id, cycle_on)` | merge, history |

The status values in the filter are the **`Enumeration.Value` integers**, written from the enumeration
in the migration (`SubscriptionStatus.Active.Value`, …), never typed as literals. U-X3 checks it.

**Migration** `AddSubscriptions` (Phase B) — both tables and every index. Read before commit. No other
phase adds a migration. The notification changes ride on HO-APT-001's `AddNotificationRequestScheduling`.

### 12.2 Infrastructure services and the worker

| Class | Notes |
|---|---|
| `Subscriptions/CommerceClock : ICommerceClock` | §8.13 — the main branch's zone via HO-ADM-001, the cut-off via `IBusinessSettings`, `TimeProvider` |
| `Notifications/Guards/{SubscriptionCycleDueGuard,SubscriptionPaymentDueGuard}` | §8.8, §8.9 — `AsNoTracking`, read only |
| `Notifications/NotificationLinkBuilder` | The four methods of §8.11 added |

**`SubscriptionWorker : BackgroundService`** (Api, `Background/`) — the `ClinicWorker` shape (a scope
**per unit of work**, never throws out of `ExecuteAsync`, disabled in `Testing`, `SubscriptionWorkCycle`
callable by tests with a `FakeTimeProvider`). Every `Subscriptions:Worker:IntervalSeconds` (900):

| # | Pass | Per unit of work (own scope) |
|---|---|---|
| 1 | **Authorisation** | `GetPrescriptionSubscriptionsForReviewQuery` → per row, C-PHM-1 → C9 (`AuthorisationExpired`) for lapsed `Active`, C10 for renewed `Suspended` |
| 2 | **Unpaid** | `GetUnpaidSuspensionCandidatesQuery(threshold)` → C9 (`UnpaidCycles`) |
| 3 | **Notices** | `GetPreChargeNoticeCandidatesQuery(today)` → `RequestNotificationCommand` (§8.8) |
| 4 | **Generation** | `GetDueSubscriptionCyclesQuery(today)` (`Active`, `NextDeliveryOn ≤ today`) → per row: **scope 1** `GetSubscriptionOrderRequestQuery(id, cycleOn)` (null → skip); **scope 2** C-ORD-1 `PlaceOrderCommand` (system actor, the idempotency key, the discount); **scope 3** C8 with the result or the refusal code. An exception in scope 2 → log with ids, no C8, retried next pass |

Each pass takes at most `WorkerBatchSize` rows, oldest `NextDeliveryOn` first, so a backlog after
downtime drains over several passes without starving interactive requests (NFR-PRF-7). Configuration:
`Subscriptions:Worker:Enabled`, `Subscriptions:Worker:IntervalSeconds`.

### 12.3 Authorization — ADR-248

Appendix D has no subscription row. Mapping **inferred** from "Orders & fulfilment" (Customer R own,
Reception W, Inventory R, Shop Mgr F, Accountant R, Admin F), plus Vet read for prescription
suspensions (OQ-7). Appended to the one `ActionRoles` table:

| Action | Staff roles | Endpoints |
|---|---|---|
| `Subscriptions.View` | Reception, Inventory, Vet, ShopManager, Accountant, Administrator | staff list, unpaid list, detail, customer summary |
| `Subscriptions.Manage` | ShopManager, Administrator | reinstate, staff cancel |
| `Subscriptions.Export` | ShopManager, Accountant, Administrator | CSV export |

Customer endpoints carry `[RequireCustomerSession]`, and every handler and query resolves **by id and
caller**. Someone else's subscription is `404`. No endpoint in this slice is public. Record the
inference in ADR-248 and flag it in the ACCEPTANCE report.

### 12.4 Api — the contract HO-SUB-002 consumes

JSON camelCase; local dates `yyyy-MM-dd`; instants ISO-8601 with offset; money as HO-ADM-001 §12.4
(`{ "amount": "1450.00", "currencyCode": "BDT" }`, amount a string). Every subscription view carries
`commerceTimeZoneId`.

**`SubscriptionView`**: `id`, `version`, `status`, `suspensionReason?`, `product` (`skuId`,
`productName`, `variantLabel`, `imageUrl?`, `isPrescriptionDiet`, `isAvailable`), `cat?` (`id`,
`name`), `terms` (`quantity`, `intervalWeeks`, `fulfilment`, `deliveryAddress?` (`id`, `label`,
`line1`, `city`), `deliveryAddressMissing`, `pickupBranch?` (`id`, `name`), `paymentMethod`),
`nextDeliveryOn?`, `changeCutoffOn?` (instant), `isLocked`, `followingDeliveryOn?`,
`skippableCycleOn?`, `pending?` (`terms?`, `skip`, `statusChange?`, `appliesFromCycleOn`), `estimate`
(`unitPrice`, `quantity`, `subtotal`, `discountPercent`, `discountAmount`, `estimatedTotal`, `note`:
*"Delivery and tax are added when each order is placed."*), **`allowedActions[]`** ⊆ {`ChangeTerms`,
`Skip`, `Pause`, `Resume`, `Cancel`, `WithdrawPending`} (computed by the aggregate's rules — the UI
shows only these), `lastCycle?` (`cycleOn`, `outcome`, `failureReason?`, `orderId?`, `orderNumber?`,
`orderState?`, `paymentExpiresOn?`), `startedOn`, `cancelledOn?`, `commerceTimeZoneId`.

**`SubscriptionChangeResult`**: `subscription` (`SubscriptionView`), `appliedImmediately`,
`appliesFromCycleOn?`, `message`.

**Customer**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/customers/me/subscriptions/options?skuId` | customer | `{ sku (C-SHP-1 summary + price), subscribable, notSubscribableReason?, allowedIntervalWeeks[], maxQuantity, discountPercent, earliestFirstDeliveryOn, latestFirstDeliveryOn, changeCutoffDays, preChargeNoticeDays, requiresCat, eligibleCats[] ({ id, name, authorisedUntil? } — alive, owned, authorised for this SKU), existingSubscriptionId? }` |
| `GET` | `/api/customers/me/subscriptions/payment-methods?skuId&quantity&deliveryAddressId&pickupBranchId` | customer | C-ORD-2 through `SubscriptionEstimate`: `[{ method, isAvailable, unavailableReason? }]` limited to the fulfilment's allowed methods (§8.3) |
| `GET` | `/api/customers/me/subscriptions?status=live\|cancelled&pageNumber&pageSize` | customer | paged `SubscriptionView` |
| `GET` | `/api/customers/me/subscriptions/{id}` | customer | `SubscriptionView` + `cycles` (latest 12; `…/{id}/cycles?pageNumber` for more): `cycleOn`, `outcome`, `failureReason?`, `orderId?`, `orderNumber?`, `orderState?`, `paymentExpiresOn?`, `discountPercent`; `404` if not the caller's |
| `POST` | `/api/customers/me/subscriptions` | customer | C1 → `201 SubscriptionView` |
| `PUT` | `/api/customers/me/subscriptions/{id}/terms` | customer | C2 → `200 SubscriptionChangeResult`; `409` stale version |
| `POST` | `/api/customers/me/subscriptions/{id}/skip` (`{ cycleOn }`) | customer | C3 → `200 SubscriptionChangeResult`; `400 CycleNoLongerSkippable` |
| `POST` | `/api/customers/me/subscriptions/{id}/pause` · `/resume` (`{ firstDeliveryOn? }`) | customer | C4 / C5 → `200 SubscriptionChangeResult`; `400 SubscriptionSuspended` |
| `POST` | `/api/customers/me/subscriptions/{id}/cancel` (`{ reason, note? }`) | customer | C6 → `200 SubscriptionChangeResult` |
| `DELETE` | `/api/customers/me/subscriptions/{id}/pending-changes` | customer | C7 → `200 SubscriptionChangeResult`; `400 NoPendingChange` |

**Staff**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/staff/subscriptions?status&suspensionReason&paymentMethod&q&sort&pageNumber&pageSize` | `Subscriptions.View` | paged: `id`, `customer` (`id`, `name`, `mobile`), `product`, `cat?`, `status`, `suspensionReason?`, `nextDeliveryOn?`, `paymentMethod`, `unpaidStreak`, `lastCycle?` |
| `GET` | `/api/staff/subscriptions/unpaid?pageNumber&pageSize` | `Subscriptions.View` | **US-SUB-06 AC2**: suspended `UnpaidCycles` **and** at-risk (`Active` with `unpaidStreak ≥ 1` or a cycle `AwaitingPayment`); each with `unpaidStreak`, the unpaid cycles (date, order number, `paymentExpiresOn`, state) and a `group` of `Suspended` \| `AtRisk` |
| `GET` | `/api/staff/subscriptions/export.csv?…same filters` | `Subscriptions.Export` | Streamed CSV, ≤ 50 000 rows (`413` above), cells starting `= + - @` prefixed with `'` (HO-ADM-001 ADR-051's guard) |
| `GET` | `/api/staff/subscriptions/{id}` | `Subscriptions.View` | `SubscriptionView` + customer + all cycles (paged) + `cancellationReason?`, `cancellationNote?`, `reinstatement?`; `allowedStaffActions[]` ⊆ {`Reinstate`, `Cancel`} |
| `POST` | `/api/staff/subscriptions/{id}/reinstate` (`{ reason, firstDeliveryOn? }`) | `Subscriptions.Manage` | C11 → `200 SubscriptionView` |
| `POST` | `/api/staff/subscriptions/{id}/cancel` (`{ note }`) | `Subscriptions.Manage` | C6 (staff) → `200 SubscriptionView` |
| `GET` | `/api/staff/customers/{customerId}/subscriptions` | `Subscriptions.View` | `{ live[], cancelledCount, suspendedCount }` — the customer-page panel |

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes (R1–R8).

### 12.5 Filters

`UniqueConstraintExceptionFilter` (HO-ACC-001): `ix_subscription_live_per_sku_cat` →
`400 SubscriptionAlreadyExists`. `ix_subscription_cycle_per_date` is handled inside C8 (success) and
never reaches the filter. Unknown constraints still rethrow.

### 12.6 Read side — `src/Read/`

Queries in `Queries/Subscription/`, handlers in `QueryHandlers/Subscription/`, ViewModels flat;
explicit projections, `AsNoTracking()`.

- `GetCustomerSubscriptionsQuery`, `GetCustomerSubscriptionDetailQuery`, `GetSubscriptionCyclesQuery` —
  `allowedActions`, `isLocked`, `skippableCycleOn` and `followingDeliveryOn` from **`SubscriptionSchedule`**
  (the same function the aggregate uses); product names from C-SHP-1 with the snapshot as fallback;
  address label from the address book (`deliveryAddressMissing` when it has gone).
- `GetSubscriptionOptionsQuery`, `GetSubscriptionPaymentMethodsQuery` — C-SHP-1, C-PHM-1, C-ORD-2,
  `SubscriptionEstimate`.
- `GetStaffSubscriptionsQuery`, `GetUnpaidSubscriptionsQuery` (streak via `UnpaidCycleRule` over the
  latest 10 cycles per subscription, loaded in one query), `GetStaffSubscriptionDetailQuery`,
  `GetCustomerSubscriptionSummaryQuery`, `ExportSubscriptionsQuery`.
- Worker queries: `GetPrescriptionSubscriptionsForReviewQuery`, `GetUnpaidSuspensionCandidatesQuery`,
  `GetPreChargeNoticeCandidatesQuery`, `GetDueSubscriptionCyclesQuery`,
  **`GetSubscriptionOrderRequestQuery`** (builds `SubscriptionOrderRequest`: lines, cat, fulfilment,
  mapped payment method, `DiscountPercent` from the setting, idempotency key).

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler and event handler in §11–§12.6;
`ICommerceClock` (Scoped); the two guards into HO-NTF-001's `SubjectGuardResolver`; the nine templates
through the catalogue; `SubscriptionWorkCycle` (Scoped); **`AddHostedService<SubscriptionWorker>()`**.
`ValidateScopes`/`ValidateOnBuild` stay on. **No package.**

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the ten enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Enums/NotificationSubjectKind.cs                              ← SubscriptionCycleDue, SubscriptionOrderAwaitingPayment only
src/Shared/CatEcommerce.Shared/Common/Constants/{SubscriptionRules,SubscriptionKeys,SubscriptionMessages,SubscriptionFailureClassifier}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs      ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SystemActors.cs                                      ← SubscriptionWorker only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                                ← the five keys of §10.2 only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs                    ← the nine definitions of §8.11 only
src/Domain/CatEcommerce.Domain/Aggregates/Subscription/**
src/Domain/CatEcommerce.Domain/ValueObjects/{SubscriptionTerms,SubscribedProduct,SubscriptionClock}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{SubscriptionSchedule,UnpaidCycleRule,SubscriptionEstimate,ICommerceClock}.cs
src/Domain/CatEcommerce.Domain/DomainServices/INotificationLinkBuilder.cs                            ← the four methods of §8.11 only
src/Domain/CatEcommerce.Domain/Models/**                                                            ← the new DTOs of §11.3 only
src/Domain/CatEcommerce.Domain/Events/Subscription/**
src/Application/CatEcommerce.Application/Commands/Subscription/**
src/Application/CatEcommerce.Application/CommandHandlers/Subscription/**
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Subscription/**
src/Application/CatEcommerce.Application/DataMappers/{SubscriptionMapper,SubscriptionCycleMapper}.cs
src/Read/CatEcommerce.Read/Queries/Subscription/**
src/Read/CatEcommerce.Read/QueryHandlers/Subscription/**
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels of §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs                ← two DbSets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{SubscriptionConfiguration,SubscriptionCycleConfiguration}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                            ← AddSubscriptions only
src/Infrastructure/CatEcommerce.Infrastructure/Subscriptions/CommerceClock.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/Guards/{SubscriptionCycleDueGuard,SubscriptionPaymentDueGuard}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs             ← §8.11 methods only
src/Api/CatEcommerce.Api/Controllers/{CustomerSubscriptions,StaffSubscriptions}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Subscription/**
src/Api/CatEcommerce.Api/Background/{SubscriptionWorker,SubscriptionWorkCycle}.cs
src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs                                 ← the one mapping of §12.5 only
src/Api/CatEcommerce.Api/appsettings.json                                                           ← Subscriptions:Worker section only
src/Api/CatEcommerce.Api/Program.cs                                                                 ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-SUB-001.md
docs/gates/HO-SUB-001-*.md
.ai/decisions/adr.md                                                                                ← ADR-237…249, append only
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents (OQ-1 goes to the architect) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs. Amended rows in other handovers are the README owner's (§1) |
| `web/**` | HO-SUB-002 |
| **Every file of HO-ORD-001, HO-SHP-001, HO-IMP-001, HO-PHM-001, HO-BIL-001** — their aggregates, commands, handlers, configurations, migrations, events and ports | Their areas. This slice **consumes** C-SHP-1, C-PHM-1 and C-ORD-1…4 and edits none of them. A missing contract is §20.2 |
| Every file of HO-ACC-001, HO-ADM-001, HO-CAT-001, HO-NTF-001, HO-APT-001 beyond the lines in §13 | Their areas. **No Account, Cat or Notification aggregate is edited** |
| Any order, order-line, stock, reservation, batch, invoice, payment, refund or token type or table | ADR-238, ADR-241 |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |

---

## 15. Unit test cases

| ID | Case | Expected |
|---|---|---|
| **A — clock and schedule** | | |
| U-A1 | `SubscriptionClock` at 2026-10-17 23:30 Asia/Dhaka (17:30Z), cut-off 2, cycle 2026-10-20 | `Today` 17 Oct; `CutoffInstant` = 18 Oct 00:00 +06:00; not locked; at 18:00Z locked |
| U-A2 | `EarliestDeliveryOn` with cut-off 0 and 2 | Today + 1; Today + 3 |
| U-A3 | `NoticeDateFor(20 Oct, notice 3, cut-off 2)`; `(notice 1, cut-off 2)` | 17 Oct; 17 Oct (floored at cut-off + 1) — ADR-245 |
| U-A4 | `Describe` immediate and scheduled | The two §8.5 sentences with local dates — **US-SUB-02 AC2** |
| U-A5 | `SubscriptionEstimate` for `1450.00 × 3` at 7.5 % | Subtotal `4350.00`, discount `326.25`, total `4023.75`, computed in minor units; `0.005` rounding half-up |
| **B — create and transitions** | | |
| U-B1 | `Create` with no first date | `NextDeliveryOn == StartedOn == EarliestDeliveryOn`; `Active` — **US-SUB-01 AC1** |
| U-B2 | First date = Today + cut-off (too soon); Today + 91 | `InvalidFirstDeliveryDate` both |
| U-B3 | `SubscriptionTerms`: quantity 0 / 21; interval 3; pickup with COD; delivery with `PayAtPickup`; delivery without address | The specific code each |
| U-B4 | `SubscriptionTransitionRules` over every `(status, action)` pair | Only §8.4's arrows; every other pair `InvalidSubscriptionTransition` naming the state |
| U-B5 | `Resume` from `Suspended` | `SubscriptionSuspended` |
| U-B6 | `Suspend` twice | Second call: no event, version unchanged |
| U-B7 | `Reinstate` a `CatDeceased` suspension; blank reason | `SubscriptionCannotBeReinstated`; `ReinstatementReasonRequired` |
| U-B8 | `ResumeAfterAuthorisation` on `UnpaidCycles` | `InvalidSubscriptionTransition` |
| **C — before and after the cut-off (US-SUB-02)** | | |
| U-C1 | Unlocked: change quantity | `Terms` changed; `AppliedImmediately`; `AppliesFromCycleOn == NextDeliveryOn` — **AC1** |
| U-C2 | Unlocked: interval 4 → 8 with `LastCycleOn` 22 Sep, today 1 Oct | `NextDeliveryOn` 17 Nov |
| U-C3 | Unlocked: interval change with no cycle yet | `NextDeliveryOn` unchanged |
| U-C4 | Unlocked: interval change whose recomputed date is inside the cut-off | Floored at `EarliestDeliveryOn` |
| U-C5 | Unlocked: skip `NextDeliveryOn` | Moved one interval; `LastSkippedOn` set — **AC1** |
| U-C6 | Unlocked: pause; cancel | `Paused`, next null; `Cancelled` — **AC1** |
| U-C7 | Locked: change quantity | `PendingTerms` set; `Terms` unchanged; `AppliesFromCycleOn == FollowingCycleOn` — **AC2** |
| U-C8 | Locked: skip `NextDeliveryOn`; skip `FollowingCycleOn`; again | `CycleNoLongerSkippable` naming the following date; `PendingSkip`; `SkipAlreadyScheduled` — **AC2** |
| U-C9 | Locked: pause; cancel | `PendingStatusChange` Pause / Cancel; status still `Active` — **AC2** |
| U-C10 | `RecordCycleProcessed` with pending terms (interval 8), pending skip and pending pause | Terms applied; `NextDeliveryOn` null (paused wins after applying); pendings cleared |
| U-C11 | `RecordCycleProcessed` with pending skip only, interval 4 | `NextDeliveryOn = cycle + 8 weeks` |
| U-C12 | `RecordCycleProcessed` failure `AuthorisationExpired` with pending cancel | `Cancelled`, not suspended |
| U-C13 | `WithdrawPendingChanges` with none | `NoPendingChange` |
| U-C14 | Change to identical terms | `NoChangeRequested` |
| **D — cycles and the unpaid streak (US-SUB-06)** | | |
| U-D1 | `RecordOrderState` redelivered (same state) | No event, no version bump |
| U-D2 | `AwaitingPayment → Delivered` (skipping `Confirmed`) | Ignored, logged; state unchanged |
| U-D3 | Streak over [AwaitingPayment, Expired, Expired, Confirmed] | 2 |
| U-D4 | Streak over [Expired, Failed, Refused, Delivered] | 2 (failed ignored, refused unpaid) |
| U-D5 | Streak over [Expired, Cancelled, Expired] | 2 (cancelled ignored) |
| U-D6 | Streak over [Confirmed, Expired, Expired] | 0 |
| U-D7 | `SubscriptionFailureClassifier` over every §8.12 row and an unknown code | Mapped reasons; unknown → `OrderRefused` |
| **E — messages** | | |
| U-E1 | All nine template definitions | Pass HO-NTF-001 `TemplateRules`; `created` and `cancelled` are `Urgent`; `changed` is InApp-only; `authorisation-lapsed` has no InApp |
| U-E2 | `SubscriptionCycleDueGuard`: active + same date; skipped; paused; cancelled; another recipient | Valid; invalid ×4 |
| U-E3 | `SubscriptionPaymentDueGuard`: `AwaitingPayment`; `Confirmed`; `Expired` | Valid; invalid; invalid |
| U-E4 | Notice candidate filter: `NextDeliverySetOn` after the notice date | Not a candidate |
| U-E5 | Payment reminder whose `SendAfter` is already past at record time | Not enqueued |
| U-E6 | `payByLocal` for 2026-10-22T04:30Z, Dhaka | `Thu 22 Oct 2026, 10:30` |
| **X — cross-cutting** | | |
| U-X1 | Reflection over `Events/Subscription/**` | No `string` properties |
| U-X2 | Reflection over every type in `Aggregates/Subscription/**`, `Models/**` (subscription DTOs), `Contracts/Subscription/**`, `ViewModels` (subscription) | No member matching `/card|token|pan|cvv|mandate|gateway|autoCharge/i` — ADR-238 |
| U-X3 | The migration's index filter | Uses `SubscriptionStatus.{Active,Paused,Suspended}.Value` |
| U-X4 | `SubscriptionWorker` loop with a throwing pass | Logged; the next pass runs; later passes in the same cycle still run |
| U-X5 | `SubscriptionPaymentMethod` members | Exactly the three of §10.1 |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1). Workers off. Tests call
`SubscriptionWorkCycle` and HO-NTF-001's `NotificationWorkCycle`, advancing `FakeTimeProvider`. **Orders,
Shop and Pharmacy are the real, merged areas** (§5, §20.3), with no fakes for C-ORD-1/C-SHP-1/C-PHM-1.

### 16.1 Additional shared-database rules

1. **Each test creates its own SKU** (through HO-SHP-001's endpoints as a shop manager) with stock
   received through HO-IMP-001, so stock and uniqueness assertions never see another test's data.
2. Customers, addresses and cats are minted per test; prescription tests issue their own authorisation
   through HO-PHM-001.
3. Settings changed by a test are changed **on a branch override where possible, or restored in a
   `finally`**. Subscription settings are global-only, so tests that change them run in one
   non-parallel collection, `SubscriptionSettings`.

| ID | Case | Expected |
|---|---|---|
| **A — foundation** | | |
| I-A1 | Migration on an empty database | `subscription` schema, both tables, `ix_subscription_live_per_sku_cat` with `NULLS NOT DISTINCT` and the filter; `ix_subscription_cycle_per_date` |
| I-A2 | Host starts, scope validation on | Every registration resolves, including the worker |
| **B — subscribe (US-SUB-01 AC1)** | | |
| I-B1 | `GET …/options?skuId` for a dry-food SKU | `subscribable: true`, intervals `[2,4,6,8,12]`, `earliestFirstDeliveryOn` = today + 3, discount from the setting |
| I-B2 | `POST` with delivery, COD, interval 4, no first date | `201`; `nextDeliveryOn` = earliest; a `subscription.created` request committed with it; the in-app copy after the delivery pass — **US-SUB-01 AC1** |
| I-B3 | Subscribe to a carrier (Bags & Carriers); to an archived SKU | `400 SkuNotSubscribable` with the specific reason each |
| I-B4 | Same SKU twice; 20 parallel identical `POST`s | `400 SubscriptionAlreadyExists`; exactly one `201` |
| I-B5 | Another customer's address; another customer's cat | `400 DeliveryAddressNotFound`; `404` |
| I-B6 | COD above Orders' ceiling (C-ORD-2) | `400 PaymentMethodUnavailable` with Orders' reason; bank transfer accepted |
| I-B7 | Prescription SKU without `catId`; with a cat lacking an authorisation; with a valid one | `CatRequiredForPrescriptionDiet`; `PrescriptionAuthorisationRequired`; `201` |
| I-B8 | Any `/me/subscriptions` route as a guest; as staff; another customer's id | `401`; `401`; `404` |
| **C — control (US-SUB-02)** | | |
| I-C1 | Change quantity 5 days before the cycle (cut-off 2) | `200`, `appliedImmediately: true`, `appliesFromCycleOn` = the next date — **AC1** |
| I-C2 | Skip the next cycle before the cut-off | `nextDeliveryOn` + 4 weeks — **AC1** |
| I-C3 | Pause, then resume with no date | `Paused` / `Active` with `nextDeliveryOn` = earliest — **AC1** |
| I-C4 | Cancel before the cut-off with reason `TooExpensive` | `Cancelled`; reason stored; `subscription.cancelled` enqueued — **AC1** |
| I-C5 | Change interval with a stale `version` | `409` |
| I-C6 | Advance past the cut-off; change quantity | `appliedImmediately: false`; `appliesFromCycleOn` = following date; `message` names both dates — **AC2** |
| I-C7 | Past the cut-off: cancel; run generation | The locked order is placed; afterwards `Cancelled`; `endsText` names the date — **AC2** |
| I-C8 | Past the cut-off: skip with the next date; with the following date | `400 CycleNoLongerSkippable`; `200` with `pending.skip` — **AC2** |
| I-C9 | Withdraw the pending change | `pending` null |
| I-C10 | `allowedActions` for Active-unlocked, Active-locked, Paused, Suspended, Cancelled | Exactly §8.4/§8.5's sets |
| **D — generation (US-SUB-01 AC2, US-SUB-04)** | | |
| I-D1 | COD subscription reaches its date; run the pass | One order via Orders with `Channel = Subscription`, status `Confirmed`, **stock reserved by batch** (Orders' reservation read); cycle `Generated`, `orderState: Confirmed`; `nextDeliveryOn` + interval — **AC2** |
| I-D2 | Bank-transfer subscription reaches its date | Orders' order `PendingPayment` with `paymentExpiresOn`; cycle `AwaitingPayment` — **AC2** |
| I-D3 | Run the pass twice; then simulate a crash between Orders and C8 (C8 skipped once) and run again | Exactly **one** order (same idempotency key), exactly one cycle row — ADR-241 |
| I-D4 | Stock insufficient | Cycle `Failed OutOfStock`; `subscription.cycle-failed`; subscription moves to the next date |
| I-D5 | `subscription.discountPercent` = 10; generate | The order line carries a 10 % line discount labelled "Subscription discount" (Orders' read); cycle `discountPercent 10.00` — **US-SUB-04** (invoice rendering: §4.2) |
| I-D6 | Change the discount after generation | The existing cycle and order keep 10 % |
| I-D7 | SKU archived before the date | Cycle `Failed ProductUnavailable`; subscription `Suspended ProductUnavailable`; customer message |
| I-D8 | Delivery address removed before the date | Cycle `Failed DeliveryAddressMissing`; subscription still `Active` |
| I-D9 | Customer changes quantity (pending) at the same moment C8 runs | One `200`/`409` pair; after retry both effects present, no lost update |
| **E — pre-charge notice (US-SUB-03)** | | |
| I-E1 | Cycle on day D, notice 3: run the pass on D−4, D−3, D−3 again | Nothing; one `subscription.pre-charge` with `skipLink` for D and `manageLink`; no duplicate — **US-SUB-03** |
| I-E2 | Notice queued in quiet hours; customer skips before delivery | The notice is `Cancelled SubjectNoLongerApplies` |
| I-E3 | Subscribe on D−3 (inside the window) | No pre-charge notice for D (the confirmation named it) |
| I-E4 | Follow the skip link's cycle date through `POST …/skip` | `200`; a second attempt with the same date `400 CycleNoLongerSkippable` |
| **F — unpaid (US-SUB-06)** | | |
| I-F1 | Bank-transfer cycle generated (window 48 h, reminder 24 h); advance 25 h; run both passes | `subscription.payment-reminder` sent with `orderLink` — **AC1** |
| I-F2 | Staff confirm the transfer before the reminder time | Mirror `Confirmed`; the reminder `Cancelled SubjectNoLongerApplies` |
| I-F3 | Let the window pass (Orders expires the order, FR-ORD-22) | Mirror `Expired`; stock released by Orders — **AC1** |
| I-F4 | Two consecutive expired cycles; run the pass | `Suspended UnpaidCycles`; `subscription.suspended`; listed in `/api/staff/subscriptions/unpaid` group `Suspended` — **AC2** |
| I-F5 | One expired cycle | Listed as `AtRisk`, still `Active` |
| I-F6 | Customer tries `POST …/resume` | `400 SubscriptionSuspended` |
| I-F7 | Shop manager reinstates with a reason; a Reception user tries | `200 Active` + `subscription.resumed`; `403` |
| I-F8 | Mirror events redelivered twice | One state change, one audit row |
| **G — prescription (US-SUB-05)** | | |
| I-G1 | Active prescription subscription; expire the authorisation; run the pass before the next date | `Suspended AuthorisationExpired`; `subscription.suspended` to the customer (in-app copy after the delivery pass); `subscription.authorisation-lapsed` request committed for the issuing vet; the subscription listed by `GET /api/staff/subscriptions?status=Suspended&suspensionReason=AuthorisationExpired`; **no order at the next date** — **AC1** |
| I-G2 | Authorisation expires and the pass is skipped (worker down) until the date; run a full cycle | The authorisation pass runs first and suspends; generation places nothing — **AC1** |
| I-G3 | Vet issues a new authorisation for the same cat and SKU; run the pass | `Active` again, same subscription id, `nextDeliveryOn` = earliest, `subscription.resumed` — **AC2** |
| I-G4 | Orders refuses at generation for authorisation (authorisation expires between the pass and generation) | Cycle `Failed AuthorisationExpired`; suspended; both messages |
| I-G5 | Paused prescription subscription with a lapsed authorisation: resume | `400 PrescriptionAuthorisationRequired`; stays `Paused` |
| **H — lifecycle** | | |
| I-H1 | Mark the cat deceased | `Suspended CatDeceased`; nothing sent; staff reinstate refused |
| I-H2 | Transfer the cat | `Suspended CatOwnerChanged` |
| I-H3 | Merge a duplicate with a subscription to SKU X into a survivor with none; and one where the survivor has X too | Reassigned; cancelled `CustomerMerged`. Re-send: no change |
| I-H4 | Delete the account | Every subscription `Cancelled AccountDeleted` |
| **X — cross-cutting** | | |
| I-X1 | Log capture across every case at `Information`+ | No cancellation note, reinstatement reason, product or cat name, address text |
| I-X2 | Scan every response body in this suite | No field matching U-X2's pattern |
| I-X3 | `GET /api/staff/subscriptions/export.csv` with a product named `=HYPERLINK(...)` | Cell prefixed with `'` |

---

## 17. Implementation order and verification

Build **phase by phase**: code → its unit tests green → its migration generated **and read** → its
integration tests green. Record each phase's commit in the test sheet.

| Phase | Stories | Delivers |
|---|---|---|
| **A** | — | Verify V1–V3; enumerations, settings, constants, codes; `SubscriptionClock`, `SubscriptionSchedule`, `UnpaidCycleRule`, `SubscriptionEstimate`, classifier; the nine templates; the two subject kinds; link-builder methods |
| **B** | US-SUB-01 AC1 | `SubscriptionAggregate`, `SubscriptionCycleAggregate`, migration `AddSubscriptions`; C1; options, payment-methods, list and detail reads |
| **C** | US-SUB-02 | C2–C7; `allowedActions`; change results |
| **D** | US-SUB-01 AC2, US-SUB-04 | `SubscriptionWorker` and the generation pass; C8; order-request query; discount — **requires HO-ORD-001** |
| **E** | US-SUB-03 | Notice pass, `SubscriptionCycleDueGuard` |
| **F** | US-SUB-06 | Mirror handlers (C-ORD-3), payment reminder and guard, unpaid pass, C9 (unpaid), C11; staff lists and export |
| **G** | US-SUB-05 | Authorisation pass, C9 (authorisation), C10, C5's check, vet message — **requires HO-PHM-001** |
| **H** | — | Lifecycle handlers (ADR-247) |

```bash
dotnet restore CatEcommerce.sln
dotnet build CatEcommerce.sln            # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add AddSubscriptions --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output-dir Persistence/Migrations
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
dotnet run --project src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.IntegrationTests
```

**Read the `AddSubscriptions` migration before applying it**: the partial unique index and its filter
values must match U-X3.

---

## 18. Acceptance criteria

Each row is checked against its evidence. **A row with no evidence is not met.**

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | SUB-01 AC1 | Subscribing to a SKU with an interval sets the next delivery date and returns it | U-B1, U-B2, I-B1, I-B2 |
| A2 | SUB-01 AC1 | Only active food and litter SKUs are subscribable; a prescription diet needs an authorised cat | U-B3, I-B3, I-B7 |
| A3 | SUB-01 AC2 | When the date arrives an order is generated automatically through Orders and stock is reserved | I-D1, I-D2 |
| A4 | SUB-01 AC2 | The order is raised as COD (confirmed) or as a bank transfer awaiting confirmation — **no auto-charge, no token** | I-D1, I-D2, U-X2, U-X5, I-X2 |
| A5 | SUB-01 AC2 | Exactly one order per cycle despite retries or a crash | **I-D3**, I-B4 |
| A6 | SUB-02 AC1 | Before the cut-off: pause, skip one delivery, change interval or quantity, cancel | U-C1…U-C6, I-C1…I-C4 |
| A7 | SUB-02 AC2 | After the cut-off a change applies to the following cycle and the response names it | U-A4, U-C7…U-C12, I-C6…I-C8 |
| A8 | SUB-03 AC | A notice goes the configured days before each cycle, with skip and change links, once, and never for a cycle that has changed | U-A3, U-E2, U-E4, I-E1…I-E4 — *in-app only outside Development until a provider exists (HO-NTF-001 OQ-1)* |
| A9 | SUB-04 AC | The configured discount applies to every subscription order as a labelled line discount and is snapshotted | I-D5, I-D6. **"Shown on the invoice" is met by HO-ORD-001 / HO-BIL-001** (§4.2) |
| A10 | SUB-05 AC1 | A lapsed authorisation suspends the subscription before the next generation | I-G1, I-G2, I-G4 |
| A11 | SUB-05 AC1 | Customer and clinic are notified | I-G1 (customer in-app copy; vet request committed) |
| A12 | SUB-05 AC2 | A renewed authorisation resumes the same subscription | I-G3 |
| A13 | SUB-05 AC1 *(caveat)* | The clinic's message reaches a person outside Development | **Not met until a provider exists** (HO-NTF-001 OQ-1). The staff list filter `AuthorisationExpired` is the interim channel (I-G1 asserts the listing) — recorded, not passed silently |
| A14 | SUB-06 AC1 | An unpaid subscription order is reminded before its window closes and expires per FR-ORD-22 | I-F1, I-F2, I-F3, U-E3, U-E5 |
| A15 | SUB-06 AC2 | Repeated unpaid cycles suspend the subscription and staff see it on an unpaid list | U-D3…U-D6, I-F4, I-F5, I-F6, I-F7 |
| A16 | SRS AC-17 | Next order generates on schedule as COD or awaiting transfer, notifies in advance, and skipping works | I-D1, I-D2, I-E1, I-C2 (end to end in HO-SUB-002 E-A3) |
| A17 | ADR-247 | Death, transfer, merge and deletion never leave food shipping to the wrong cat or person | I-H1…I-H4 |
| A18 | ADR-248 | Customers see only their own subscriptions; staff actions follow the inferred matrix | I-B8, I-F7 |

**Deferred, named in §4.2**: the invoice rendering of the discount (HO-ORD-001 / HO-BIL-001), the
portal dashboard tile (HO-POR-001), the subscription-health report (HO-RPT-001).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | ADR-237 (Order context, own schema); **no Orders, Shop, Inventory, Pharmacy or Billing file touched** — contracts consumed only; ADR-240's one rule-1 exception; ADR-241's **one scope per command** in the worker (no nested dispatch); **no enqueue from any event handler**; the ADR-244 deviation from the flow doc recorded; shared-file appends confined to §13's lines |
| 2 | SECURITY | `security-reviewer` | **No token, card or gateway field anywhere** (U-X2, I-X2); `404` for another customer's subscription, cat or address; staff actions per §12.3 and the inference flagged; CSV-injection guard on export; no free text in events or logs; the system actor used for C-ORD-1 is not a customer session |
| 3 | CODE_REVIEW | `code-reviewer` | Time only through `SubscriptionClock`; `SubscriptionSchedule` shared by aggregate and reads; C8 idempotent under replay; mirror handlers idempotent; the worker never throws and respects the pass order; money in minor units |
| 4 | TEST | `test-engineer` | `TC-HO-SUB-001.md` from §15/§16 first; §16.1's per-test SKU; **I-D3 with a real crash simulation**, not two sequential happy runs; I-B4 with real parallelism; real Orders, Shop and Pharmacy, no fakes |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A9's invoice half named as HO-ORD/HO-BIL's; A13 recorded as not met with its interim channel; every §8.12 assumption checked against the real handover and any mismatch recorded in §1 Amended |

---

## 20. BLOCKED conditions — stop and report

**20.1 HO-ACC-001, HO-ADM-001, HO-NTF-001 or HO-APT-001's HO-NTF-001 amendment is not merged.** This
slice consumes their sessions, address book, settings, outbox, guards, `SendAfter` and link builder.
Do not stub them.

**20.2 A §8.12 contract does not exist or differs in substance** in HO-SHP-001, HO-PHM-001 or
HO-ORD-001 as written or built. Examples: no idempotency key on place-order, no system-actor
placement, no line discount, no expiry event, no payment-method availability, no authorisation
query. **Stop and report.** Never add the missing piece to the other area's code, never read its
tables directly, and never reimplement its rule here. A name-only difference is an Amended row, not a
stop.

**20.3 Phase D, F or G would need a fake Orders, Pharmacy or Shop to go green.** Stop. The integration
suite runs against the real areas. A fake that passes proves nothing about the idempotency and
reservation guarantees this slice depends on.

**20.4 Do not add auto-charge, a saved token, a card field, a gateway reference or a charge retry**
(ADR-238, owner instruction, SRS Phase 5).

**20.5 Do not enqueue from an event handler** (HO-NTF-001 ADR-057), and **do not cancel subscriptions
automatically** from cat, merge or authorisation events. Account deletion is the one exception
(ADR-247).

**20.6 V1, V2 or V3 is false** (§7). V1 has a stated fallback. V2 and V3 are stops.

**20.7 A cycle keeps failing with an exception** (not a refusal) across passes for more than 24 hours
in any environment. Stop and report with the ids. Never record it as a refusal to "unstick" the
subscription, and never change the idempotency key. Either would risk a second order.

**20.8 An owner answer to an OQ contradicts a default here** (e.g. auto-charge wanted, per-product
discounts). It is an Amended row and possibly a new ADR from ADR-250…256, never a silent edit.

**20.9 The §1 "Amends" rows have not been appended** to HO-NTF-001, HO-ADM-001 and HO-ACC-001 when
implementation starts. Ask the README owner. Do not edit those handovers yourself.

**20.10 A requirement conflict is a stop, not an edit** — SRS and backlog are forbidden scope.
