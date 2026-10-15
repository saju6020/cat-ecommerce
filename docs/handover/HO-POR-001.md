# HO-POR-001 — Customer Portal (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-POR-001** |
| Title | Customer Portal — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §15 — **US-POR-01, US-POR-02, US-POR-03** (all three stories in the section) |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-POR-1, FR-POR-4, FR-POR-5**, FR-BRD-13; consumed, not re-delivered: FR-POR-2 (US-CAT-07), FR-POR-3 (US-CLN-16, US-PHM-07), FR-POR-6 (US-ACC-06); FR-SHP-11 / BR-2 (re-order gating), FR-ORD-3, NFR-SEC-2, NFR-SEC-5, **NFR-SEC-8**, NFR-PRF-1, NFR-PRF-3, D-10, UI-4, C-6, §1.2 (no payment gateway) |
| Pairs with | [`HO-POR-002`](HO-POR-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-CAT-001`](HO-CAT-001.md) (cats, vaccination status), [`HO-ACC-001`](HO-ACC-001.md) (customer session, profile, `[RequireCustomerSession]`), [`HO-ADM-001`](HO-ADM-001.md) (endpoint-access convention, money shape), [`HO-APT-001`](HO-APT-001.md) (customer appointments). **Assumed, not yet written** (being written in parallel): HO-EMR-001, HO-PHM-001, HO-SHP-001, HO-IMP-001, HO-BIL-001, HO-ORD-001, HO-BRD-001, HO-SUB-001 — §10.2 states every contract assumed |
| Delivery order | Section 15 of 17 in [`README.md`](README.md#delivery-order) — "Needs first: everything it summarises" |
| Approved design | None — no `architect` design exists for this slice. The composition shape is proposed in §8 and recorded as ADR-257 … ADR-267 for the ARCHITECTURE gate to judge |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — README C-6 (duplicate stay diary).** §8.8, ADR-265, `GetStayDiaryQuery` and the `/api/portal/stays/**` route are **retired** in favour of HO-BRD-001's `GET /api/customers/me/boarding/stays/{id}/diary`, which is backed by the aggregates that own the data and the purge. US-POR-03 is evidenced in HO-BRD-001/002; this slice only links to it. OQ-6 is closed by that decision. |
| Amended | **2026-10-09 — Phases A, B (visits only) and C built on `ho/POR-001`; §10.2 reconciled with the real contracts on master (`4a4acb88`).** Not built, because the upstream is not merged (§17, §20.1): the prescriptions reader (HO-PHM-001, rest of Phase B), Phase D (orders reader, re-order, HO-ORD-001 / SHP / IMP), Phase E (stays, HO-BRD-001), Phase F (subscriptions, HO-SUB-001). **Decision for the unbuilt sections:** the handover gives no placeholder guidance, so the dashboard response simply has **no key** for a section whose reader does not exist yet (`prescriptions`, `stays`, `orders`, `subscriptions`); each phase adds its key, its reader and its registration. HO-POR-002 must treat a missing section key as "not delivered yet", never as "empty" or "unavailable". Only `PortalSectionState` is built of §10.1 (`ReorderLineOutcome` arrives with Phase D; `StayDiaryState` is retired with §8.8). **Contract mismatches, each absorbed in the one reader (§20.2) — none blocks:** (1) **CAT** — `GetCatsQuery` now returns `PagedResult<CatListItemViewModel>` (it has a `TotalCount`, F31 is out of date) and the item gained `HandlingNote`; the reader still asks for `DashboardCatLimit + 1` (U-B3) and drops the note (ADR-262). (2) **APT** — matches: `GetCustomerAppointmentsQuery { CustomerId, Scope = "upcoming" }`; note `upcoming` means Booked, CheckedIn and InConsultation with no date filter, so in-progress walk-ins are listed too. (3) **ACC** — matches. (4) **EMR** — the real read is **per cat**: `GetCustomerCatVisitsQuery { CatId, CustomerId }` → `CustomerVisitListItemViewModel(Id, Date, Service, Vet, Problems, HasSummary)?`, finalised visits only, newest first, `null` when the cat is not the customer's now. So: no per-customer query (the visits reader reads the caller's alive cats with `GetCatsQuery`, then asks EMR for each, sequentially); no `catName` (taken from the cat list); **no `isFinalised`** (EMR returns finalised visits only, so the "filter again" defence in depth of §8.3 cannot be applied and U-B9 is adapted to "only the latest visit of the list per cat; a draft is proved absent by I-B2"); the problem names are read and discarded; and **no `followUpOn`** — it exists only inside the full discharge summary (`GetCustomerVisitSummaryQuery`), which would pull the whole clinical summary per cat into every dashboard read against ADR-262, so `followUpOn` is **omitted from `PortalVisitItemViewModel`** (HO-POR-002 does not use it; if the owner wants it, EMR should add a field to its list item). (5) **BIL** — there is no `GetCustomerBalanceSummaryQuery`/`status` filter: Billing publishes `GetCustomerBillingSummaryQuery { CustomerId, BranchId? }` (one `Outstanding` and one `Overdue` Money in the commerce currency, `OldestDueOn`, **no invoice count**; its own comment names HO-POR-001 as the consumer) and `GetCustomerInvoicesQuery { CustomerId }` (all issued invoices, newest first, **no status filter**, `InvoiceSummaryViewModel`). The balances reader therefore makes each Money at most one list entry (never summed across currencies, ADR-266), and walks at most `PortalRules.BalanceInvoiceMaxPages` (5) pages of 100 invoices keeping `Issued`/`PartPaid` ones with a balance, oldest due first; `openInvoiceCount` counts the open invoices found in that scan (a customer with more than 500 issued invoices may see a lower count — a request to Billing for a status filter would remove the cap). Billing includes customers merged into the caller (its ADR-192). Billing's newer names `BillingDocumentKind` and `ToBillingActor()` are write-side and not consumed here. (6) `PortalSectionComposer.RunAsync` takes the correlation id as an extra argument so the one log line can carry it. **ADR text:** §9 of this handover holds only the open questions; ADR-257 … ADR-267 have no text in it, so `.ai/decisions/adr.md` records them sentence by sentence from §2, §4.4, §8 and §10 (ADR-263 pending Phase D, ADR-265 withdrawn). **No migration is generated by this slice; the I-A2 probe was run and discarded.** |
| Amended | **2026-10-10 (build) -- merge of master `a8761829`; Phase B completed (prescriptions), Phase D partly built (orders reader). Re-order, stays and subscriptions NOT built.** `catecommerce_por` migrated to master; this slice still has no migration. **Built:** (1) the **prescriptions reader** over Pharmacy's published `GetCustomerPrescriptionsQuery { CustomerId, Scope = "active" }` (the real contract has no `endsOn`/`prescriptionId` under those names: the end date is `CourseEndsOn`); per cat it yields `activeCount` and `earliestEndsOn`, and the medicine, dose, route, frequency, repeat block and vet are read and discarded (ADR-262); rows Pharmacy flags inactive are dropped again. (2) the **orders reader** over Orders' published `GetCustomerOrdersQuery { CustomerId }` (newest first, page of `DashboardItemLimit`). The dashboard now carries `prescriptions` and `orders` keys. **Deviations absorbed in the one reader (§20.2):** (a) **no `trackingNumber`** on the order item -- Orders' list item does not carry it and the only source is the order detail, which also holds the bank-transfer instructions, so it is not pulled per order into every dashboard read (if wanted, Orders should add it to its list item); (b) **`canReorder` follows Orders' own rule, not OQ-2's default**: OQ-2 said "every status except `pending_payment`" but also that Orders' rule wins, and Orders' order-detail `canReorder` is true only for `Cancelled`, `Delivered`, `Returned`, `Refunded` -- the dashboard must not offer a re-order the order page withholds; Orders' list item has no flag, so the four statuses are named in `OrdersSectionReader` (the set goes if Orders adds `canReorder` to its list item); (c) `total` is Orders' `GrandTotal`, unchanged. **Not built, and why:** **re-order (C1, validator, handler, `ReorderResultDto`, `ReorderLineOutcome`, `OrderNotFound`/`OrderNotReorderable`, `POST /api/portal/orders/{id}/reorder`) -- BLOCKED by §20.4.** Orders published no reusable cart-line eligibility port or service: its eligibility (purchasable, in stock, `PrescriptionLineGuard`, cart line limits) lives inside its own `ReorderCommandHandler` (C15), which already serves `POST /api/customers/me/orders/{orderId}/reorder`, adds the full ordered quantity (no top-up, no `AlreadyInCart`, no partial quantity, no price-changed flag) and reports `unavailable[] { productName, reason }`. Building §8.7 would mean re-implementing stock, prescription and cart rules in the portal (§20.4: stop). An owner decision is needed (see the report): reuse Orders' endpoint as the only re-order, or ask ORD for a reusable eligibility service and top-up semantics. **Stays reader (HO-BRD-001) and subscriptions reader (HO-SUB-001): not built -- both handovers are unbuilt on master (§20.1); the dashboard has no `stays` / `subscriptions` key** (HO-POR-002 must treat a missing key as "not delivered yet"). **Still open from 2026-10-09:** `followUpOn` on visits (EMR's list item lacks it); Billing open-invoice count capped at 5 pages of 100 (500 invoices). The gate condition ("after Phase F") cannot be met until HO-BRD-001 and HO-SUB-001 are built and the re-order question is settled. |
| Amended | **2026-10-10 (finalise) -- owner-delegated decision 2026-10-10, owner to review: the portal ships WITHOUT stays and subscriptions and WITHOUT its own re-order; four additive upstream fields.** (1) **Boarding (HO-BRD-001) and Subscriptions (HO-SUB-001) are skipped for now.** Phases E and F are *deferred by owner*, not failed; the dashboard has no `stays` and no `subscriptions` key and HO-POR-002 must not show those tiles. U-B14 and U-B15 are deferred with them. (2) **The gate condition "after Phase F" becomes "after Phase D"** (§17, §19): the gates judge this slice as built (phases A-D). (3) **Re-order: the portal does NOT implement its own.** C1 (`ReorderPreviousOrderCommand`), §11.2-§11.4, §8.7, the result DTO, `ReorderLineOutcome`, `POST /api/portal/orders/{orderId}/reorder`, ADR-263, U-C* and I-A4/I-C* are **retired** (not blocked): the customer re-orders through Orders' existing `POST /api/customers/me/orders/{orderId}/reorder` (HO-ORD-001 C15), which owns stock, prescription and cart rules; HO-POR-002 links to / calls that endpoint, shown when the order item's `canReorder` is true. OQ-2 and OQ-3 are closed by this (Orders' rule wins; Orders' endpoint adds the full quantity and reports `unavailable[]`). §20.4 is therefore no longer a blocker for this slice. (4) **`followUpOn` on dashboard visits: yes.** Clinical records (EMR) added `followUpOn` (`DateOnly?`, the visit's follow-up due date, null when none) additively to its published `CustomerVisitListItemViewModel` (`GET /api/customers/me/cats/{catId}/visits`) and the query projection; `PortalVisitItemViewModel.FollowUpOn` passes it through. A date alone carries no diagnosis or instruction, so ADR-262 is amended (the date is identifying metadata; the follow-up kind and any text stay out). This supersedes the 2026-10-09 "omitted" decision in row 2. (5) **`trackingNumber` and `canReorder` on the order list item: yes.** Ordering added both additively to its published `CustomerOrderSummaryViewModel` (`GET /api/customers/me/orders`): `trackingNumber` (`string?`, the shipment's number, null until dispatched/recorded) and `canReorder` (computed by Orders' own `CustomerOrderRules.CanReorder`, the single source also used by the order detail). `OrdersSectionReader` passes both straight through; **the portal's own 4-status copy is deleted** and `trackingNumber` is now on `PortalOrderItemViewModel`. ADR-268 is amended. This supersedes the 2026-10-10 (build) deviations (a) and (b). (6) **OQ-4 closed: NO access logging** when a customer reads their own cat's visits or prescriptions (the legal concern is staff access; the owner reading their own data is not). §20.9 no longer applies. (7) **Open-invoice count cap of 500 stays as is** (backlog BIL 66: a Billing status filter would remove it). (8) Cross-area edits, additive only, no migration: EMR `DischargeSummaryViewModels.cs` + `DischargeSummaryQueryHandlers.cs`; Orders `OrderViewModels.cs` + `OrderQueryHandlers.cs` (`CustomerOrderRules`); CONTRACTS rows 30-31; Amended rows in HO-EMR-001, HO-ORD-001 and HO-POR-002. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Give a signed-in cat owner **one place that shows everything the business holds about them** —
cats and their vaccination status, upcoming appointments, boarding stays, recent orders, outstanding
balances, recent visit summaries, active prescriptions and subscriptions — plus two actions on top:
**re-order a past order in one step** and **read a boarding stay's diary**.

The portal is a **window, not a module**. Every fact it shows is owned by another area: Cat
Profiles, Appointments, Consultation, Pharmacy, Orders, Billing, Boarding and Subscriptions each
already store, compute and authorise their own data. This slice owns as little as possible:

1. **No aggregate, no table, no schema, no migration, no domain event, no event handler** (ADR-257).
2. **Composition only through other modules' published query handlers** — never their tables
   (ADR-258). If Billing changes how it stores invoices, the portal does not notice.
3. **One anti-corruption adapter per upstream module** (a *section reader*) is the only file that
   knows an upstream ViewModel's shape (ADR-259).
4. **One write**, re-order, which mutates only Orders' cart through Orders' own aggregate and Orders'
   own eligibility rules; the portal never re-implements stock or prescription gating (ADR-263).

Three things this slice is careful about:

- **Owner scoping by construction.** The customer id comes only from the session. Ids handed to an
  upstream handler come only from an owner-scoped upstream result in the same request, or after an
  owner check. Someone else's order or stay is `404`, never `403` (ADR-261).
- **One broken module must not blank the dashboard.** Each section succeeds or is reported
  `Unavailable` on its own; an unavailable section never pretends to be empty (ADR-260).
- **No payment, anywhere.** Balances show what is owed and how to pay offline. There is no
  pay-online button or endpoint — the gateway is SRS Phase 5 (ADR-264).

§17 builds the slice in **six phases**, one per upstream module group; each compiles and passes its
own tests first.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-POR-01 | M | 8 | `GET /api/portal/dashboard`: cats (with vaccination status), upcoming appointments, boarding stays, recent orders, outstanding balances — **the AC's five** — plus recent visit summaries, active prescriptions and subscriptions (the portal "summarises everything"). `hasCats` drives the empty state. Per-section `Ready` / `Unavailable` | A–F |
| US-POR-02 | S | 5 | `POST /api/portal/orders/{orderId}/reorder`: available lines topped up in the cart; each other line returned with its reason (out of stock, no longer sold, newly prescription-gated, authorisation expired, cat not eligible) | D |
| US-POR-03 | S | 3 | **Not delivered here — retired to HO-BRD-001 (README C-6).** Boarding owns the observations, the media, the signed URLs and the 12-month purge, and HO-BRD-001 already publishes `GET /api/customers/me/boarding/stays/{id}/diary` for the same story. This slice's dashboard **links** to it from the stays tile; Phase E builds nothing | — |

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` in controllers; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, `xmin` → `409` | ADR-011, ADR-016 |
| Query handlers read with `AsNoTracking`, project, never write | `.ai/read-side-pattern.md` §4 |
| One aggregate mutated per command | `.ai/bounded-contexts.md` rule 1 |
| Customer sessions, `[RequireCustomerSession]`, `401 session_invalid` | HO-ACC-001 ADR-031, ADR-032, §12.4 |
| Every endpoint declares its access; a customer must not learn another customer's resource id exists (`404`) | HO-ADM-001 ADR-043, §12.3 |
| Money is `decimal(12,2)` + ISO code; on the wire `{ "amount": "1500.00", "currencyCode": "BDT" }` | HO-ADM-001 ADR-054, §12.4 |
| Deceased cats are excluded from default cat reads | HO-CAT-001 US-CAT-09, Q2 |
| Vaccination status is computed read-side per cat, never stored | HO-CAT-001 §12.3 Q4 |
| Appointments are open, unpaid bookings | HO-APT-001 ADR-073 |
| **No payment gateway in v1**; payments are COD, cash, in-store card and bank transfer recorded by staff | SRS §1.2, FR-ORD-8, owner v1.1 |
| **Identifier + password only**; no OTP or verification step anywhere | SRS v2.2, HO-ACC-001 §4.2 |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-POR-002`](HO-POR-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-POR-01 "all visible on one screen" | One response carrying every section (§12.4) |
| US-POR-01 "an empty state invites me to add one" | `cats.hasCats: false` only when the cats section is `Ready` with zero cats; `null` when `Unavailable` (never a false "you have no cats") |
| US-POR-02 "listed with the reason" | `lines[].outcome` per line, enumerated in §10.1 |
| US-POR-03 "viewable in date order" | `days[]` ascending by local date; media ascending by `takenOn` within a day |

### 4.2 Owned by another area — the portal links to it, never rebuilds it

| Capability | Owner | What the portal does |
|---|---|---|
| Vaccination status and booking link (FR-POR-2, US-CAT-07) | HO-CAT-001 / HO-CAT-002 | Shows `overallStatus`, `earliestNextDueOn`, `bookingEligible` per cat on the dashboard |
| Visit / discharge summaries and lab results (FR-POR-3, US-CLN-16) | **HO-EMR-001** | Latest finalised visit per cat — identifying metadata only (ADR-262) |
| Active prescription list with dose and frequency (US-PHM-07) | **HO-PHM-001** | Count and soonest end date per cat only (ADR-262) |
| Profile, addresses, preferences (FR-POR-6, US-ACC-06) | HO-ACC-001 | Reads `fullName` for the greeting |
| Appointment list, detail, reschedule, cancel | HO-APT-001 | Next upcoming appointments |
| Order history, order detail, tracking (FR-ORD-13) | **HO-ORD-001** | Recent orders; the re-order action |
| Invoices, invoice PDF, ageing (FR-BIL-3) | **HO-BIL-001** | Outstanding balance per currency and open invoices |
| Boarding bookings list, stay summary, photo upload, retention purge (US-BRD-12, US-BRD-17, D-10) | **HO-BRD-001** | Current and upcoming stays; the diary composition |
| Subscription management (US-SUB-*) | **HO-SUB-001** | Counts by status and next delivery |
| Inbox and unread badge | HO-NTF-001 / HO-NTF-002 | Nothing — already in the portal header |
| Staff "view customer" pages | HO-ACC-002, HO-APT-002, … | Nothing — this is a customer-only slice (OQ-11) |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| Cat photo on the dashboard | HO-CAT-001's list ViewModel carries no photo (F32) and photos are storage keys with no serving path (HO-CAT-001 §4.2). The cat tile shows name and flags only (OQ-10) |
| Online payment | SRS Phase 5. Balances carry `howToPay` guidance only (ADR-264) |
| Every upstream area other than Cats | Not implemented at baseline; most not yet written. §5, §20.1 |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, `IEventPublisher`, `IDispatcher`, MassTransit, RabbitMQ, MongoDB, a
second host, a `CommandService` layer, `202 Accepted`, `[Authorize]`. Specific to this slice:

- **No `AggregateRoot`, `DbSet`, `IEntityTypeConfiguration`, migration, schema, `Event` or
  `IEventHandlerAsync` in the `Portal` context** (ADR-257).
- **No `CatEcommerceDbContext` injected into any portal query handler or section reader** — the
  portal never reads another module's tables, not even "just one join" (ADR-258).
- **No nested `IInMemoryDispatcher` call** from a query handler (ADR-258).
- **No re-implementation** of sellable stock, FEFO, prescription gating (BR-2) or cat ownership
  rules in the re-order handler (ADR-263).
- **No payment, pay-link, gateway, card or wallet type or endpoint** (ADR-264).
- **No proxying, caching or re-signing of media bytes or URLs** (ADR-265).
- **No clinical content** (diagnosis, SOAP text, dose, frequency, instructions, lab values) in any
  dashboard ViewModel (ADR-262).
- **No write from a query** — not an access log, not a "last viewed" stamp.

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` (backend implemented) |
| HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-APT-001 merged | ❌ **written, not implemented** — §20.1 |
| HO-EMR-001, HO-PHM-001, HO-SHP-001, HO-IMP-001, HO-BIL-001, HO-ORD-001, HO-BRD-001, HO-SUB-001 merged | ❌ **not written at the time of this handover** — each phase in §17 starts only once its upstream is merged and its contract reconciled with §10.2 (§20.2) |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ — **but no migration is generated by this slice** |
| New NuGet packages | **None** |

The shared-database rules of HO-CAT-001 §16.1 and HO-ACC-001 §16.1 apply. §16.1 adds the portal's.

---

## 6. Governing architecture documents

Where any of these disagrees with a `.ai/templates/*.cs` file, **the markdown wins**.

| Document | What you need from it |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) | Layer responsibilities; **Read references Infrastructure, Application never references Read**; §6 the verified platform surface; §6.2 `ValidateCommand = true` |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) | §3 queries; **§5 rule 1 — a controller dispatches exactly one command or query** (why the dashboard is one query, not eight); rule 2 — Application never references Read (why re-order reads upstream through repositories and ports, not queries) |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | Rule 1 one aggregate per command (re-order mutates only the cart); rule 2 reference by id; the Order context owns `CartAggregate`; the Prescription context owns `DietAuthorisation` and "the order handler checks it" (BR-2) — which is why re-order must use Orders' check (ADR-263). `Portal` is not listed (OQ-1) |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | Every namespace in §11/§12; `Portal` is a new `[Feature]` segment (ADR-257) |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | **ADR-009 … ADR-023 binding**; ADR-012 (read the same tables, no projections), ADR-014, ADR-016, ADR-019 are the ones this slice turns on. This handover owns **ADR-257 … ADR-276** |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | §4 rules — project, `AsNoTracking`, paginate, **a query never writes**, return `QueryResponse<T>`; **§5 "reads that span aggregates"** permits cross-context joins — this slice deliberately takes the narrower path of composing published queries instead (ADR-258 records why) |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) | §3 handler shape for re-order; §5 lookups in the handler are not domain logic; **§6 rule placement** — gating is the cart/eligibility owner's rule, not the portal's |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) | §2 the repository (already implemented); §6 `xmin` → `409` on the cart |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) | §2–§3 — the cart's own events fire as usual on re-order; the portal adds no handler |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) | §3 validators as `IValidator<T>`; §4 `ValidateScopes`/`ValidateOnBuild` stay on — **they are what prove every upstream handler the portal depends on is registered** (I-A1) |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) | `Program.cs` shape; §3 lifetimes (section readers Scoped); §5 never-register list |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | The two enumerations in §10.1 (not persisted — serialised by `DisplayName`) |
| [`.ai/feature-creation-checklist.md`](../../.ai/feature-creation-checklist.md) | File order within each phase |
| [HO-CAT-001](HO-CAT-001.md) §12.3, [HO-APT-001](HO-APT-001.md) §12.4/§12.6, [HO-ACC-001](HO-ACC-001.md) §12.3–§12.5, [HO-ADM-001](HO-ADM-001.md) §12.3–§12.4 | The upstream contracts consumed in Phase A |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001
F28–F30 apply. Additionally, read from the code at `4007225`:

| # | Fact | Consequence |
|---|---|---|
| F31 | `GetCatsQuery` has `OwnerId?` and `IncludeDeceased` (default `false`); its handler orders by `Name`, pages with the base `Skip`/`Take`, and **returns no total count** | The cats reader requests `PageSize = PortalRules.DashboardCatLimit + 1` and derives `hasMore`; it never asks for a count the handler does not produce |
| F32 | `CatListItemViewModel(Id, OwnerId, Name, Species, Breed, Sex, IsHighRisk, LifeStatus)` — **no photo** | No cat photo on the dashboard (§4.3, OQ-10). Do not call `GetCatProfileQuery` per cat to get one |
| F33 | `CatVaccinationStatusViewModel(CatId, OverallStatus: string, EarliestNextDueOn: DateOnly?, BookingEligible, Vaccines[])` | Mapped to three fields; the per-vaccine rows are dropped |
| F34 | Query handlers are registered in `Program.cs` as `IQueryHandlerAsync<TQuery, TResult>`, all Scoped | Section readers can take them by constructor; with `ValidateOnBuild` a missing upstream registration fails **startup**, not a customer's request |
| F35 | `tests/CatEcommerce.Application.Tests` already references `CatEcommerce.Read` (test-only, HO-CAT-001 U34–U38) | Reader and composer unit tests go there; no new project reference |
| F36 | The shared `CatEcommerceDbContext` is Scoped and not thread-safe; nested handlers resolved in the request scope share it | Sections and per-cat status calls run **sequentially**. `Task.WhenAll` over upstream handlers is a defect (ADR-267) |

**Unverified — prove in Phase A step 1:** that a Scoped query handler whose constructor takes other
`IQueryHandlerAsync<,>` instances resolves with `ValidateScopes`/`ValidateOnBuild` on and is
invoked correctly by `IInMemoryDispatcher`. Nothing about it is unusual (all Scoped, no cycle), but
it is the first handler in the solution to do it. If it fails → §20.5.

---

## 8. Domain model — none; the composition model

### 8.1 Context boundary — ADR-257

```
Portal context — namespace segment `Portal`, NO schema, NO tables
Read/Queries/Portal/           GetPortalDashboardQuery, GetStayDiaryQuery
Read/QueryHandlers/Portal/     GetPortalDashboardQueryHandler, GetStayDiaryQueryHandler, PortalSectionComposer
Read/QueryHandlers/Portal/Sections/   one reader per upstream module (§8.3)
Application/Commands/Portal/   ReorderPreviousOrderCommand                 ← the only write
Application/CommandHandlers/Portal/   ReorderPreviousOrderCommandHandler   ← mutates Orders' CartAggregate only
```

**Why the portal is a context at all, if it owns nothing.** Its code has to live somewhere, its
decisions (fault isolation, owner-scoping-by-construction, the clinical-content line) need one
place, and the gates need one folder to check for forbidden types. A namespace segment costs
nothing; a schema would invite someone to "just cache" a dashboard in it.

### 8.2 How composition works — ADR-258

```
PortalController ──(one query)──► IInMemoryDispatcher ──► GetPortalDashboardQueryHandler
                                                              │  (sequential, same scope — F36)
                                                              ├─► CatsSectionReader ─────► IQueryHandlerAsync<GetCatsQuery,…>  (HO-CAT-001)
                                                              │                         └► IQueryHandlerAsync<GetCatVaccinationStatusQuery,…> × ≤ 12
                                                              ├─► AppointmentsSectionReader ► IQueryHandlerAsync<GetCustomerAppointmentsQuery,…> (HO-APT-001)
                                                              ├─► StaysSectionReader ──────► HO-BRD-001 published query
                                                              ├─► OrdersSectionReader ─────► HO-ORD-001 published query
                                                              ├─► BalancesSectionReader ───► HO-BIL-001 published queries
                                                              ├─► VisitsSectionReader ─────► HO-EMR-001 published query
                                                              ├─► PrescriptionsSectionReader ► HO-PHM-001 published query
                                                              ├─► SubscriptionsSectionReader ► HO-SUB-001 published query
                                                              └─► ProfileReader ───────────► IQueryHandlerAsync<GetCustomerProfileQuery,…> (HO-ACC-001)
```

- The **controller dispatches one query** (`.ai/cqrs.md` §5 rule 1). The handler composes.
- Readers take upstream handlers as **`IQueryHandlerAsync<TQuery, TResult>` by constructor** and
  call `HandleAsync` directly. Not the dispatcher: a nested dispatch hides the dependency from the
  constructor (the ARCHITECTURE gate cannot see what the portal depends on) and turns a missing
  registration into a runtime failure the fault-isolation policy would then swallow as
  `Unavailable`. Constructor injection makes a missing upstream a startup failure (F34).
- Every upstream query the portal builds carries the portal query's **`UserContext` and
  `CorrelationId`**, and the caller's id in whatever owner/customer filter the upstream exposes.
- Readers **never** receive a `DbContext`. If an upstream query cannot answer what the portal needs,
  that is a missing upstream contract (§20.2), not a reason to join its tables.

### 8.3 Section readers — ADR-259

One class per upstream module in `Read/QueryHandlers/Portal/Sections/`, each implementing
`IPortalSectionReader<TSection>` with `Task<TSection> ReadAsync(PortalCaller caller)`
(`PortalCaller` = `CustomerId`, `UserContext`, `CorrelationId`). Each maps upstream ViewModels
**member by member** into the portal's own section ViewModel — never passes an upstream type
through, so a new upstream field (possibly staff-only) cannot leak.

| Reader | Upstream (owner) | Rule |
|---|---|---|
| `CatsSectionReader` | HO-CAT-001 Q2, Q4 | `GetCatsQuery { OwnerId = caller, IncludeDeceased = false, PageNumber = 1, PageSize = DashboardCatLimit + 1 }`; `hasMore` from the extra row; then Q4 **per shown cat, sequentially**. A failing Q4 for one cat → that cat's `vaccination = null`, `vaccinationState = Unavailable`; the section stays `Ready`. `hasCats = items.Count > 0` |
| `AppointmentsSectionReader` | HO-APT-001 `GetCustomerAppointmentsQuery` | `scope = upcoming`, page 1, `DashboardItemLimit`; maps id, cat, service name, `startsOn`, `endsOn`, performer name, branch name, `branchTimeZoneId`, status |
| `StaysSectionReader` | HO-BRD-001 (§10.2) | Current (`checked_in`) and upcoming (`reserved`, `confirmed`) stays, limit `DashboardItemLimit`; `hasDiary = status ∈ {CheckedIn, CheckedOut}` |
| `OrdersSectionReader` | HO-ORD-001 (§10.2) | Most recent `DashboardItemLimit` orders by `placedOn` desc; `canReorder = status != PendingPayment` (OQ-2) |
| `BalancesSectionReader` | HO-BIL-001 (§10.2) | Outstanding and overdue **per currency, never summed across currencies** (ADR-266); open invoices, limit `DashboardItemLimit`, oldest due first; `hasOutstanding`; `howToPay = PortalMessages.HowToPayOffline` |
| `VisitsSectionReader` | HO-EMR-001 (§10.2) | **Finalised** visits only — filtered again here even if upstream already does (defence in depth); latest per cat, alive cats from the same response only; ADR-262 fields only |
| `PrescriptionsSectionReader` | HO-PHM-001 (§10.2) | Active prescriptions grouped by cat → `activeCount`, `earliestEndsOn`. Medicine, strength, dose, frequency and instructions are **read and discarded** (ADR-262) |
| `SubscriptionsSectionReader` | HO-SUB-001 (§10.2) | Counts by status (`active`, `paused`, `suspended`); `nextDelivery` = the active subscription with the earliest `nextRunOn` (product name, date) |
| `ProfileReader` | HO-ACC-001 Q-C2 | `fullName` only |

### 8.4 Fault isolation — ADR-260

`PortalSectionComposer.RunAsync<T>(string sectionName, Func<Task<T>> read)` is the **only**
`try/catch` in the dashboard path:

```
try   { return PortalSection<T>.Ready(await read()); }
catch (OperationCanceledException) { throw; }            // request aborted — never swallow
catch (Exception ex)
{
    logger.LogError(ex, "Portal section {Section} unavailable. CorrelationId={CorrelationId}", sectionName, correlationId);
    return PortalSection<T>.Unavailable();                // data = null, never an empty list
}
```

- `Unavailable` carries **no data** — `null`, not `[]`, not `0`. The FE must not render "nothing to
  pay" or "no cats" for a section it could not read (HO-ACC-002 ADR-FE-012).
- The profile read is inside the composer too; a missing greeting never fails the page.
- The log line carries the section name, the exception and the correlation id — **no customer data,
  no upstream payload** (I-X1).
- Sections run in the fixed order of §8.2; the order is not a priority, it is deterministic for tests.

### 8.5 Owner scoping — ADR-261

| Input | Where the owner comes from | Mismatch |
|---|---|---|
| Dashboard | `UserContext.UserId` of a customer session; the query has **no customer-id property** a client could set | — |
| Cat ids for Q4 | Only ids returned by the owner-filtered Q2 **in the same request** | Not possible by construction |
| `stayId` (diary) | Boarding's owner query (§10.2) → `customerId`; compared to the caller | **`404 StayNotFound`** — same body for "not found" and "not yours" |
| `orderId` (re-order) | `OrderAggregate.CustomerId`, loaded read-only | **`404 OrderNotFound`** — same |

Calling an upstream **handler** directly bypasses that module's **controller filter**
(HO-ADM-001 §12.3 `OwnerScope`). The rows above are what replace it; U-B12 and I-B3/I-C6/I-D3 prove
them.

### 8.6 The clinical-content line — ADR-262

NFR-SEC-8: clinical records are visible to clinical roles and the owning customer, and **every
access is logged**. A query cannot log (read-side rule 5), and a controller cannot dispatch a query
and a command (cqrs rule 1). So the dashboard carries **only what identifies a record**, and the
owning module's detail page — which can log — shows the content:

| Section | Allowed | Never |
|---|---|---|
| Visits | `visitId`, `catId`, `catName`, `visitedOn`, `serviceName`, `vetName`, `hasDischargeSummary`, `followUpOn?` | diagnosis, SOAP text, treatments, medicines, lab values, notes |
| Prescriptions | `catId`, `catName`, `activeCount`, `earliestEndsOn?` | medicine, strength, dose, route, frequency, instructions |
| Cats | name, `isHighRisk`, vaccination `overallStatus`, `earliestNextDueOn`, `bookingEligible` | allergies, conditions, medications, handling note |

Whether an owner reading their **own** record counts as an "access" to log is OQ-4 — the line above
keeps the dashboard safe under either answer.

### 8.7 Re-order — US-POR-02 — ADR-263 — RETIRED 2026-10-10

> **Retired (owner-delegated decision 2026-10-10, owner to review).** The portal has no re-order of its own; the customer re-orders through Orders' `POST /api/customers/me/orders/{orderId}/reorder`, and the dashboard order item's `canReorder` is Orders' own flag. The text below is kept for the record only and is not to be built. See §1 Amended 2026-10-10 (finalise).

`ReorderPreviousOrderCommand(OrderId)` → `ReorderPreviousOrderCommandHandler`
(`Application/CommandHandlers/Portal/`). **Application cannot reference Read**, so this handler
reads through repositories and ports, like HO-CAT-001's vaccine lookup:

1. `IAggregateRootRepository<OrderAggregate>.GetByIdAsync(orderId)` — **read-only lookup, never
   saved**. `null` or `CustomerId != caller` → `404 OrderNotFound`.
2. `Status == PendingPayment` → `400 OrderNotReorderable` (the order is still live — OQ-2).
3. Load the caller's `CartAggregate` through Orders' own "cart for customer" lookup; if none, open
   one through Orders' own factory (§10.2). The portal never constructs cart state by hand.
4. For each order line, in line order:
   - Not a SKU line (a service or boarding charge on a combined order, FR-ORD-19) → `NotAProduct`.
   - `already = quantity of that SKU (and prescription cat) already in the cart`;
     `wanted = line.Quantity − already`. `wanted ≤ 0` → **`AlreadyInCart`** — this is what makes a
     double click harmless without an idempotency table (**top-up semantics**).
   - Ask **Orders' cart-line eligibility** (§10.2) for `(customer, sku, wanted, line.PrescriptionCatId)`.
     It is the same check Orders' own add-to-cart uses: SKU active, sellable stock (IMP), prescription
     authorisation for the recorded cat (PHM, BR-2), cat still owned and alive. Map its reason to
     §10.1's outcome. **The portal adds no rule of its own.**
   - Eligible with `sellable < wanted` → add `sellable`, outcome `AddedWithReducedQuantity` (OQ-3).
   - Eligible → `cart.AddLine(...)` at **today's price**; `priceChanged = currentUnitPrice !=
     line.UnitPrice`.
   - `cart.AddLine` throws `DomainRuleViolationException` (e.g. the cart's line limit) → that line
     `NotAdded` with the aggregate's message; continue.
5. Anything added → **one** `UpdateAsync(cart, InMemoryPublishOnly)` (or `SaveAsync` for a new cart).
   Nothing added → **no write**. Either way `200` with the result (§12.4) — a re-order where nothing
   was available is an answer, not an error.
6. `DbUpdateConcurrencyException` on the cart → `409` via the existing filter.

A line whose product became prescription-only **after** the order has no recorded cat, so it is
`PrescriptionRequired` and is not added; the customer adds it from the product page, choosing the cat
(FR-SHP-11). The portal never guesses a cat.

### 8.8 Stay diary — US-POR-03 — retired (README C-6)

> **Reconciled 2026-09-22.** This section originally specified `GetStayDiaryQuery` and
> `GET /api/portal/stays/{stayId}/diary`. HO-BRD-001 was written in parallel and specifies the same
> story at `GET /api/customers/me/boarding/stays/{id}/diary`, backed by the aggregates it owns. Two
> endpoints for one story is a duplicate, not a choice, so **this one is retired**: Boarding owns the
> data, the media, the signed URLs and the retention purge, and owning the read next to them keeps
> the purge and the read from drifting apart.
>
> **What this slice still does.** The dashboard's boarding tile (§8.4) links each stay to Boarding's
> diary page. No `GetStayDiaryQuery`, no `/api/portal/stays/**` route, no diary handler or test here.
> ADR-265 is withdrawn; the acceptance row for US-POR-03 is evidenced in HO-BRD-001, not here.

### 9.1 Open questions — raised, not resolved

| # | Question | Owner | Default in this build |
|---|---|---|---|
| **OQ-1** | `.ai/bounded-contexts.md` / `.ai/namespace-conventions.md` need the `Portal` context | Architect (separate task) | ADR-257 records it |
| **OQ-2** | Which past orders can be re-ordered? | Owner | Every status except `pending_payment` (cancelled and returned orders included — the customer wants the same items) |
| **OQ-3** | Partial stock on re-order: add what is available, or skip the line? | Owner | Add the sellable quantity, outcome `AddedWithReducedQuantity` — unless HO-ORD-001's add-to-cart rule says otherwise, in which case Orders' rule wins |
| ~~OQ-4~~ | **Closed 2026-10-10 (owner-delegated decision 2026-10-10, owner to review): NO logging.** Staff access to clinical records is logged (NFR-SEC-8); an owner reading their own cat's visits or prescriptions is not an access to log. | — | — |
| **OQ-5** | After D-10's 12-month media purge, do written daily observations stay visible in the diary? | Owner | Yes — they are part of the stay record; only media is purged |
| ~~OQ-6~~ | **Closed 2026-09-22 (README C-6).** HO-BRD-001 does publish a customer diary endpoint, so — as this row anticipated — one was retired. **Boarding won**, because it owns the observations, the media, the signed URLs and the 12-month purge; a read living next to its own retention rule cannot drift from it. This slice's diary endpoint and page are withdrawn | — | — |
| **OQ-7** | Every §10.2 contract is assumed | Authors of HO-EMR/PHM/SHP/IMP/BIL/ORD/BRD/SUB-001 | Their actual names and shapes win; the difference is absorbed in the one reader (§20.2) |
| **OQ-8** | Should dashboard limits be administrator settings? | Owner | No — `PortalRules` constants (5 items, 12 cats) |
| **OQ-9** | Should the balances tile show bank-transfer account details? | Owner + accountant | No — `howToPay` text only; the FE shows branch contacts from HO-ADM-001 `GET /api/branches` |
| **OQ-10** | Cat photo on the dashboard | Owner | None until photos are servable (HO-CAT-001 §4.2) |
| **OQ-11** | A staff "view the customer's dashboard" mode? | Owner | No — not in any story |

---

## 10. Enumerations, constants, violation codes and assumed contracts

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, **not persisted**

| Type | Members |
|---|---|
| `PortalSectionState` | `Ready`, `Unavailable` |
| `ReorderLineOutcome` | `Added`, `AddedWithReducedQuantity`, `AlreadyInCart`, `OutOfStock`, `NoLongerSold`, `PrescriptionRequired`, `AuthorisationExpired`, `CatNotEligible`, `NotAProduct`, `NotAdded` |
| `StayDiaryState` | `NotStarted`, `Open`, `Closed` |

Serialised by `DisplayName`. `ReorderLineOutcome` messages live in `PortalMessages` (plain language,
no ids — NFR-USA-4).

### 10.2 Assumed upstream contracts — the dependency register

**Consumed from handovers that exist** (contracts read, names verified against the handover text):

| Owner | Query / port | Used for |
|---|---|---|
| HO-CAT-001 (implemented) | `GetCatsQuery` → `IReadOnlyList<CatListItemViewModel>`; `GetCatVaccinationStatusQuery(CatId)` → `CatVaccinationStatusViewModel` | Cats section (F31–F33) |
| HO-ACC-001 | `GetCustomerProfileQuery(customerId)` → `CustomerProfileViewModel.fullName` | Greeting |
| HO-APT-001 | `GetCustomerAppointmentsQuery(scope, paging)` → `AppointmentSummary` (§12.4 there) | Appointments section |

**Assumed from handovers not yet written** — each owner must publish these (or equivalents); the
names are placeholders that the owning handover's names replace (OQ-7):

| Owner | Assumed contract | Fields the portal needs | Used by |
|---|---|---|---|
| **HO-EMR-001** | `GetCustomerVisitSummariesQuery { CustomerId, CatId?, paging }` → visits | `visitId`, `catId`, `catName`, `visitedOn` (date), `serviceName`, `vetName`, **`isFinalised`**, `hasDischargeSummary`, `followUpOn?` | Visits |
| **HO-PHM-001** | `GetCustomerActivePrescriptionsQuery { CustomerId }` → prescriptions | `prescriptionId`, `catId`, `catName`, `endsOn?`, `status` (other fields ignored) | Prescriptions |
| **HO-PHM-001** | Diet-authorisation check **inside** HO-ORD-001's eligibility (not called by the portal directly) | — | Re-order |
| **HO-SHP-001** | SKU active/archived and `prescriptionRequired`, **inside** HO-ORD-001's eligibility | — | Re-order |
| **HO-IMP-001** | Sellable quantity per SKU (FR-IMP-8), **inside** HO-ORD-001's eligibility | — | Re-order |
| **HO-ORD-001** | `GetCustomerOrdersQuery { CustomerId, paging, sort = placedOn desc }` → orders | `orderId`, `orderNumber`, `placedOn`, `status`, `total` (Money), `itemCount`, `trackingNumber?` | Orders |
| **HO-ORD-001** | `OrderAggregate` public read surface | `CustomerId`, `Status`, `Lines[]` { `LineKind` (Sku / charge), `SkuId`, `ProductName`, `VariantName`, `Quantity`, `UnitPrice` (Money), `PrescriptionCatId?` } | Re-order |
| **HO-ORD-001** | `CartAggregate` + lookup/factory | "cart for customer" lookup; `OpenFor(customerId, …)`; `AddLine(CartLineDto { SkuId, Quantity, PrescriptionCatId?, UnitPrice })`; a read of current lines | Re-order |
| **HO-ORD-001** | **Cart-line eligibility** domain service / port, used by Orders' own add-to-cart | `EvaluateAsync(customerId, skuId, quantity, prescriptionCatId?)` → eligible (`sellableQuantity`, `currentUnitPrice`) or ineligible (reason ∈ out of stock, discontinued, prescription required, authorisation expired/missing, cat not eligible) | Re-order — **§20.4 if it does not exist** |
| **HO-BIL-001** | `GetCustomerBalanceSummaryQuery { CustomerId }` | per currency `outstanding`, `overdue`; `openInvoiceCount`; `oldestDueOn?` | Balances |
| **HO-BIL-001** | `GetCustomerInvoicesQuery { CustomerId, status = open, paging }` | `invoiceId`, `invoiceNumber`, `issuedOn`, `dueOn?`, `total`, `balanceDue` (Money), `status` | Balances |
| **HO-BRD-001** | `GetCustomerStaysQuery { CustomerId, scope = current \| upcoming \| past, paging }` | `stayId`, `cats[]` {`catId`, `name`}, `branchName`, `startsOn`, `endsOn` (dates), `status` (Appendix C), `unitTypeName` | Stays |
| **HO-BRD-001** | `GetStayOwnerQuery { StayId }` → `{ customerId, status }?` | owner check, status | Diary |
| **HO-BRD-001** | `GetStayCareObservationsQuery { StayId, CatId? }` | per entry `catId`, `catName`, **`localDate`**, `recordedOn`, `appetite`, `waterIntake`, `litterOutput`, `mood`, `notes?` (caretaker free text, customer-visible by Boarding's rule) | Diary |
| **HO-BRD-001** | `GetStayMediaQuery { StayId, CatId? }` | `mediaId`, `catIds[]`, `kind` (photo/video), **`localDate`**, `takenOn`, `caption?`, `url` + `thumbnailUrl?` (**signed**), `urlExpiresOn`; stay-level `mediaRetainedUntil?`, `mediaPurged` | Diary |
| **HO-SUB-001** | `GetCustomerSubscriptionsQuery { CustomerId }` | `subscriptionId`, `productName`, `intervalWeeks`, `nextRunOn?`, `status` (Appendix C) | Subscriptions |

Every row is also listed in §5 and in the report as a cross-area dependency.

### 10.3 Constants

`PortalRules` — `DashboardItemLimit = 5`, `DashboardCatLimit = 12`.
`PortalMessages` — one plain-language message per `ReorderLineOutcome`; `HowToPayOffline`
(*"You can pay at the clinic by cash or card, or by bank transfer. Contact the clinic if you need the
transfer details."*); section-unavailable log template.

### 10.4 Violation codes — append to `BusinessViolationCodes` (reuse if the owner already defines one)

`OrderNotFound` · `OrderNotReorderable` · `StayNotFound`.

---

## 11. Specified changes — write side

### 11.1 Command — `Application/Commands/Portal/`

| # | Command | Payload | Phase |
|---|---|---|---|
| C1 | `ReorderPreviousOrderCommand` | `OrderId` (route), `UserContext`, `CorrelationId`, `ValidateCommand = true` | D |

### 11.2 Validator — `CommandHandlers/Validators/ReorderPreviousOrderCommandValidator.cs`

`OrderId` not empty. Registered as `IValidator<ReorderPreviousOrderCommand>`.

### 11.3 Result DTO — `Shared/SharedDto/Portal/ReorderResultDto.cs`

In `Shared` because Application cannot reference Read. `OrderId`, `AddedLineCount`,
`CartItemCount`, `Lines[]` { `SkuId`, `ProductName`, `VariantName?`, `OrderedQuantity`,
`AddedQuantity`, `Outcome`, `Message`, `CatName?`, `PriceChanged` }.

### 11.4 Handler — `CommandHandlers/Portal/ReorderPreviousOrderCommandHandler.cs`

§8.7 exactly. Injects `ILogger<T>`, `IAggregateRootRepository<OrderAggregate>` (read-only),
`IAggregateRootRepository<CartAggregate>`, and HO-ORD-001's eligibility port (and its cart lookup if
that is a separate port). **Nothing else** — in particular no Read query, no `DbContext`, no Product
or Batch repository. Returns `CommandResponse { IsSuccess = true, Result = ReorderResultDto }` on
every non-404/non-400 path.

### 11.5 Events and event handlers

**None.** The cart's own events fire from `AddLine` as Orders designed them.

---

## 12. Specified changes — read side, Infrastructure, Api, host

### 12.1 Persistence

**None.** No `DbSet`, configuration, index or migration. I-A2 proves it.

### 12.2 Infrastructure

**None.**

### 12.3 Read side — `src/Read/`

Queries in `Queries/Portal/`, handlers in `QueryHandlers/Portal/`, readers in
`QueryHandlers/Portal/Sections/`, ViewModels flat in `ViewModels/` prefixed `Portal…` / `StayDiary…`.

| # | Query → ViewModel | Notes |
|---|---|---|
| Q1 | `GetPortalDashboardQuery` (no properties beyond the base) → `PortalDashboardViewModel` | §8.2–§8.4; caller from `UserContext` |
| Q2 | `GetStayDiaryQuery { StayId, CatId? }` → `StayDiaryViewModel?` | §8.8; `null` → `404` |

Supporting types: `IPortalSectionReader<T>`, `PortalCaller`, `PortalSection<T>`,
`PortalSectionComposer` (Scoped).

### 12.4 Api — the contract HO-POR-002 consumes

`PortalController`, route `api/portal`, injects `IInMemoryDispatcher` and `IUserContextProvider`
only. Every action carries **`[RequireCustomerSession]`** (HO-ADM-001 ADR-043's convention test sees
it). JSON camelCase; dates `yyyy-MM-dd`; instants ISO-8601 with offset; money
`{ "amount": "…", "currencyCode": "…" }`.

| Method | Route | Command / query | Success | Errors |
|---|---|---|---|---|
| `GET` | `/api/portal/dashboard` | Q1 | `200 PortalDashboardViewModel`, `Cache-Control: no-store` | `401 session_invalid` |
| `POST` | `/api/portal/orders/{orderId}/reorder` | C1 | `200 ReorderResultDto` | `404 OrderNotFound`, `400 OrderNotReorderable`, `409` (cart changed), `401`, `403 csrf_token_invalid` |
| ~~`GET`~~ | ~~`/api/portal/stays/{stayId}/diary`~~ | **Retired (README C-6)** — HO-BRD-001 serves `GET /api/customers/me/boarding/stays/{id}/diary` | — | — |

**`PortalDashboardViewModel`**

```jsonc
{
  "generatedOn": "2026-10-12T04:30:00+00:00",
  "customer":      { "state": "Ready", "data": { "fullName": "Nusrat Jahan" } },
  "cats":          { "state": "Ready", "data": {
                       "hasCats": true, "hasMore": false,
                       "items": [{ "catId": "…", "name": "Mitu", "isHighRisk": false,
                                   "vaccinationState": "Ready",
                                   "vaccination": { "overallStatus": "DueSoon", "earliestNextDueOn": "2026-10-30", "bookingEligible": true } }] } },
  "appointments":  { "state": "Ready", "data": { "items": [{ "appointmentId": "…", "catId": "…", "catName": "Mitu",
                       "serviceName": "Vaccination", "startsOn": "…", "endsOn": "…", "performerName": "Dr Rahman",
                       "branchName": "Main", "branchTimeZoneId": "Asia/Dhaka", "status": "Booked" }] } },
  "stays":         { "state": "Ready", "data": { "items": [{ "stayId": "…", "cats": [{ "catId": "…", "name": "Mitu" }],
                       "branchName": "Main", "unitTypeName": "Deluxe", "startsOn": "2026-10-20", "endsOn": "2026-10-27",
                       "status": "Confirmed", "hasDiary": false }] } },
  "orders":        { "state": "Ready", "data": { "items": [{ "orderId": "…", "orderNumber": "SO-2026-00042", "placedOn": "…",
                       "status": "Delivered", "total": { "amount": "3450.00", "currencyCode": "BDT" },
                       "itemCount": 3, "trackingNumber": "…", "canReorder": true }] } },
  "balances":      { "state": "Unavailable", "data": null },
  "visits":        { "state": "Ready", "data": { "items": [{ "visitId": "…", "catId": "…", "catName": "Mitu",
                       "visitedOn": "2026-09-30", "serviceName": "Consultation", "vetName": "Dr Rahman",
                       "hasDischargeSummary": true, "followUpOn": "2026-10-14" }] } },
  "prescriptions": { "state": "Ready", "data": { "byCat": [{ "catId": "…", "catName": "Mitu", "activeCount": 2, "earliestEndsOn": "2026-10-18" }] } },
  "subscriptions": { "state": "Ready", "data": { "activeCount": 1, "pausedCount": 0, "suspendedCount": 0,
                       "nextDelivery": { "subscriptionId": "…", "productName": "Royal Canin Indoor 2 kg", "nextRunOn": "2026-10-25" } } }
}
```

`balances` when `Ready`:
`{ "hasOutstanding": true, "outstanding": [{ "amount": "1500.00", "currencyCode": "BDT" }], "overdue": […],
"openInvoiceCount": 2, "oldestDueOn": "2026-09-01", "invoices": [{ "invoiceId", "invoiceNumber", "issuedOn",
"dueOn", "total", "balanceDue", "status" }], "howToPay": "…" }`. **No payment URL or action field exists.**

**`StayDiaryViewModel`**

```jsonc
{
  "stayId": "…", "branchName": "Main", "branchTimeZoneId": "Asia/Dhaka",
  "startsOn": "2026-10-20", "endsOn": "2026-10-27",
  "diaryState": "Open", "cats": [{ "catId": "…", "name": "Mitu" }],
  "mediaRetainedUntil": "2027-10-27", "mediaPurged": false,
  "days": [{
    "localDate": "2026-10-20",
    "observations": [{ "catId": "…", "catName": "Mitu", "recordedOn": "…", "appetite": "Ate all",
                       "waterIntake": "Normal", "litterOutput": "Normal", "mood": "Settled", "notes": "…" }],
    "media": [{ "mediaId": "…", "kind": "Photo", "catIds": ["…"], "takenOn": "…", "caption": "…",
                "url": "https://…signed…", "thumbnailUrl": "https://…signed…", "urlExpiresOn": "…" }]
  }]
}
```

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes (R1–R8).

### 12.5 Host — `Program.cs`

Register: C1's handler and validator (as `IValidator<T>`); Q1 and Q2 handlers as
`IQueryHandlerAsync<,>`; `PortalSectionComposer` and every `IPortalSectionReader<T>` (Scoped).
**Count them** against §8.3 and §11. `ValidateScopes`/`ValidateOnBuild` stay on — I-A1 then also
proves every upstream handler the readers take is registered. No new package, no hosted service.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{PortalSectionState,ReorderLineOutcome,StayDiaryState}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{PortalRules,PortalMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/BusinessViolationCodes.cs          ← append only (§10.4)
src/Shared/CatEcommerce.Shared/SharedDto/Portal/**
src/Application/CatEcommerce.Application/Commands/Portal/**
src/Application/CatEcommerce.Application/CommandHandlers/Portal/**
src/Application/CatEcommerce.Application/CommandHandlers/Validators/ReorderPreviousOrderCommandValidator.cs
src/Read/CatEcommerce.Read/Queries/Portal/**
src/Read/CatEcommerce.Read/QueryHandlers/Portal/**                                  ← incl. Sections/
src/Read/CatEcommerce.Read/ViewModels/{Portal*,StayDiary*}.cs
src/Api/CatEcommerce.Api/Controllers/PortalController.cs
src/Api/CatEcommerce.Api/Contracts/Portal/**
src/Api/CatEcommerce.Api/Program.cs                                                 ← §12.5 registrations only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-POR-001.md
docs/gates/HO-POR-001-*.md
.ai/decisions/adr.md                                                                ← ADR-257…267, append only
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`; `.claude/**` | Governing docs and agents (OQ-1 is a separate task) |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs. A conflict is §20.7 |
| `web/**` | HO-POR-002 |
| `src/Infrastructure/**` — including `CatEcommerceDbContext`, `Persistence/Configurations/**`, `Persistence/Migrations/**` | The portal owns no persistence (ADR-257) |
| `src/Domain/**` | No aggregate, event, domain service or DTO is added or changed. Orders' `CartAggregate` is **called**, not edited |
| Every file of HO-CAT, ACC, ADM, NTF, APT, EMR, PHM, SHP, IMP, BIL, ORD, BRD and SUB-001 | Their areas. A missing upstream query or field is **§20.2**, never an edit here |
| `src/Api/…/Filters/**`, `Authorization/**`, `Sessions/**` | Reused unchanged |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package or reference is added |

Touching anything here is an automatic **FAIL** at the ARCHITECTURE and ACCEPTANCE gates.

---

## 15. Unit test cases

`tests/CatEcommerce.Application.Tests` (references Read — F35). Upstream handlers are **hand-written
fakes** of `IQueryHandlerAsync<,>`; no database.

| ID | Case | Expected |
|---|---|---|
| **A — boundaries** | | |
| U-A1 | Reflection over every type whose namespace contains `.Portal` | None derives from `AggregateRoot`, `Event`, or implements `IEntityTypeConfiguration<>` / `IEventHandlerAsync<>` — ADR-257 |
| U-A2 | Reflection over constructors in `Read.QueryHandlers.Portal**` | No `CatEcommerceDbContext`, no `IInMemoryDispatcher`, no repository parameter — ADR-258 |
| U-A3 | Reflection over every public property of every `Portal*` ViewModel (recursive) | No property whose type is from another module's ViewModel namespace set — ADR-259 |
| U-A4 | Reflection over `PortalController` | Every action carries `[RequireCustomerSession]`; no route contains `pay` — ADR-264 |
| U-A5 | Reflection over the visits and prescriptions section ViewModels | Only the §8.6 allow-listed property names — ADR-262 |
| **B — dashboard composition** | | |
| U-B1 | Composer with the balances reader throwing `InvalidOperationException` | `balances.state = Unavailable`, `data = null`; every other section `Ready`; one `Error` log with the section name and correlation id — ADR-260 |
| U-B2 | Composer with a reader throwing `OperationCanceledException` | Propagates; no section result |
| U-B3 | Cats reader: fake returns 13 cats for limit 12 | 12 items, `hasMore = true`; the fake received `OwnerId = caller`, `IncludeDeceased = false`, `PageSize = 13` — F31 |
| U-B4 | Cats reader: zero cats | `Ready`, `hasCats = false` |
| U-B5 | Cats section `Unavailable` | Dashboard `cats.data = null` — `hasCats` is absent, never `false` |
| U-B6 | Q4 fake throws for one of three cats | That cat `vaccinationState = Unavailable`, `vaccination = null`; the others mapped; section `Ready` |
| U-B7 | Q4 is called only with ids returned by Q2 in the same call | The recorded ids equal Q2's ids — ADR-261 |
| U-B8 | Appointments reader | `scope = upcoming`, page size 5; fields mapped, branch zone carried |
| U-B9 | Visits reader: upstream returns a draft and two finalised visits for one cat | Only the latest **finalised** visit; the draft never appears — FR-POR-3 |
| U-B10 | Prescriptions reader: three active across two cats | Two rows with counts 2 and 1 and the earliest `endsOn`; no medicine or dose in the output |
| U-B11 | Balances reader: BDT 1500 and USD 20 outstanding | Two entries, no total — ADR-266; zero outstanding → `hasOutstanding = false`, `outstanding = []` |
| U-B12 | Every reader | Passes the caller id, `UserContext` and `CorrelationId` of the portal query to its upstream query |
| U-B13 | Orders reader: statuses `PendingPayment`, `Confirmed`, `Delivered`, `Cancelled` | `canReorder` false, true, true, true — OQ-2 |
| U-B14 | Stays reader | Current and upcoming only; `hasDiary` true only for `CheckedIn` / `CheckedOut` |
| U-B15 | Subscriptions reader | Counts by status; `nextDelivery` = active subscription with the earliest `nextRunOn`; none active → `null` |
| U-B16 | Composer invokes readers | Sequentially — a fake that detects overlapping calls never fires — ADR-267 |
| **C — re-order** | | |
| U-C1 | Order of another customer; unknown order | `OrderNotFound`, cart repository never touched |
| U-C2 | `PendingPayment` order | `OrderNotReorderable` |
| U-C3 | Three eligible lines, empty cart | Three `Added`; one `UpdateAsync`/`SaveAsync` on the cart; the order is **never saved** |
| U-C4 | Cart already holds 2 of a line ordered ×3 | `AddLine` with 1; outcome `Added`. Holds 3 → `AlreadyInCart`, no `AddLine` — top-up |
| U-C5 | Eligibility reasons: out of stock, discontinued, prescription required, authorisation expired, cat not eligible | `OutOfStock`, `NoLongerSold`, `PrescriptionRequired`, `AuthorisationExpired`, `CatNotEligible`; none added |
| U-C6 | Eligible with sellable 1 for wanted 3 | Adds 1, `AddedWithReducedQuantity` |
| U-C7 | Nothing eligible | `IsSuccess = true`, zero added, **no cart write** |
| U-C8 | `AddLine` throws `DomainRuleViolationException` on line 2 of 3 | Line 2 `NotAdded` with the message; lines 1 and 3 added; one save |
| U-C9 | Charge line on a combined order | `NotAProduct` |
| U-C10 | Current price differs from the order line's | `priceChanged = true`, added at the current price |
| U-C11 | No cart exists | Opened through Orders' factory; `SaveAsync` |
| U-C12 | Validator, empty `OrderId` | Fails on `OrderId` |
| **D — diary** | | |
| U-D1 | Observations and media over three local dates, supplied out of order | Days ascending; observations by cat then time; media by `takenOn`; no empty day |
| U-D2 | `CatId` filter on a two-cat stay | Passed to both upstream queries; only that cat's entries |
| U-D3 | Owner query returns another customer, or `null` | Handler returns `null` |
| U-D4 | Stay `Reserved` | `diaryState = NotStarted`, `days = []` |
| U-D5 | Stay `Cancelled` / `NoShow` | `null` |
| U-D6 | `mediaPurged = true` upstream | Passed through; observations still present — OQ-5 |
| U-D7 | Media URLs | Byte-for-byte the upstream values; `urlExpiresOn` unchanged — ADR-265 |

## 16. Integration test cases

Real HTTP through `WebApplicationFactory`, `Testing` environment, the shared `catecommerce_dev`
database. `PASS` means you watched it pass.

### 16.1 Additional shared-database rules

1. **Every test mints its own customer** (registration with the CAPTCHA test answer, HO-ACC-001
   §12.6) and signs in; the dashboard is asserted **only for that customer**.
2. Upstream data is arranged **through the owning modules' own endpoints** (staff sessions as the
   matching roles) — never by inserting rows into their tables.
3. Two-customer cases create both customers in the test.
4. No test asserts a global count.

| ID | Case | Expected |
|---|---|---|
| I-A1 | Host starts with `ValidateScopes` + `ValidateOnBuild` | No exception — **every section reader and every upstream handler it takes resolves** |
| I-A2 | Compare the EF model before and after this slice | No new entity type, table or migration; `dotnet dotnet-ef migrations add Probe` produces an empty migration (then discard it) |
| I-A3 | Each portal endpoint without a session; with a **staff** session only | `401 session_invalid` both |
| I-A4 | `POST …/reorder` with a customer session but no `X-CSRF-Token` | `403 csrf_token_invalid`; nothing written |
| I-B1 | New customer, nothing recorded | `200`; `cats.state = Ready`, `hasCats = false`; every other section `Ready` with empty data — **US-POR-01 AC2** |
| I-B2 | Customer with two cats (one deceased), a booked appointment, a confirmed stay, a delivered order, an open invoice, a finalised and a draft visit, an active prescription, an active subscription | Every section `Ready` and populated; deceased cat absent; draft visit absent — **US-POR-01 AC1** |
| I-B3 | Two customers each with a cat, order and invoice | Each dashboard shows only its own records — ADR-261 |
| I-B4 | Vaccination status per cat on the dashboard vs `GET /api/cats/{id}/vaccination-status` | Identical `overallStatus`, `earliestNextDueOn`, `bookingEligible` |
| I-B5 | Test host replaces Billing's balance handler with a throwing fake | `200`; `balances` `Unavailable` with `data: null`; all other sections `Ready`; an `Error` log with the correlation id — ADR-260 |
| I-B6 | 13 alive cats | 12 returned, `hasMore: true` |
| I-B7 | Raw JSON of I-B2's dashboard | No key named `diagnosis`, `soap`, `dose`, `frequency`, `strength`, `instructions`, `allergies`, `handlingNote`, `notes` anywhere — ADR-262 |
| I-B8 | I-B2's customer, 20 sequential dashboard calls | p95 recorded in the test sheet; **≤ 1 500 ms** (NFR-PRF-3) on the development machine |
| I-C1 | Re-order a delivered order whose SKUs are all sellable | `200`; every line `Added`; `GET` cart (Orders' endpoint) contains them at today's prices — **US-POR-02** |
| I-C2 | One SKU with no sellable stock, one archived | Those lines `OutOfStock` / `NoLongerSold` with messages; the rest added |
| I-C3 | A product marked prescription-required **after** the order | `PrescriptionRequired`; not in the cart — "newly prescription-gated" |
| I-C4 | A prescription line with the recorded cat: authorisation valid; then expired (advance `FakeTimeProvider`) | `Added` with the cat on the cart line; then `AuthorisationExpired` — BR-2 |
| I-C5 | Re-order the same order twice | Second call: every line `AlreadyInCart`; cart quantities unchanged — ADR-263 |
| I-C6 | Another customer's order; an unknown id | `404 OrderNotFound` both, identical bodies |
| I-C7 | A `pending_payment` order | `400 OrderNotReorderable` |
| I-C8 | Price raised since the order | `priceChanged: true`; cart line at the new price |
| I-C9 | Re-order and a cart edit (Orders' endpoint) concurrently, 10 attempts | Each attempt ends `200` or `409`; the cart never loses a line or double-counts |
| I-D1 | Own checked-in stay; observations on three days and photos on two | `200`; three days ascending; media on the right days; every `url` is Boarding's signed URL with `urlExpiresOn` in the future — **US-POR-03** |
| I-D2 | Two-cat stay, `?catId=` | Only that cat's entries |
| I-D3 | Another customer's stay; unknown id | `404 StayNotFound` both |
| I-D4 | A confirmed stay not yet checked in | `200`, `diaryState: NotStarted`, `days: []` |
| I-D5 | A checked-out stay after Boarding's media purge (Boarding's purge seam, clock +12 months) | Observations present, `media` empty, `mediaPurged: true` — D-10, OQ-5 |
| I-X1 | Log capture at `Information`+ across all cases | No signed URL, no observation note text, no customer name, no money amount |

---

## 17. Implementation order and verification

Each phase starts only when its upstream handover is **merged** and §10.2 is reconciled with its
real contract (§20.1, §20.2).

| Phase | Upstream required | Delivers |
|---|---|---|
| **A** | HO-CAT-001, HO-ACC-001, HO-ADM-001, HO-APT-001 | Step 1: the §7 verification spike (a composing handler resolves). Enumerations, constants, `PortalSectionComposer`, `IPortalSectionReader<T>`, cats/profile/appointments readers, Q1 with those sections (others not yet registered in the composer), `PortalController` dashboard action; U-A*, U-B1–B8, B12, B16; I-A1–A3, I-B1 (partial), I-B4, I-B6 |
| **B** | HO-EMR-001, HO-PHM-001 | Visits and prescriptions readers; U-A5, U-B9, U-B10; I-B7 |
| **C** | HO-BIL-001 | Balances reader; U-B11; I-B5 |
| **D** | HO-SHP-001, HO-IMP-001, HO-ORD-001 | Orders reader (re-order RETIRED 2026-10-10: Orders' endpoint is used); ~~re-order~~ command, validator, handler, result DTO, endpoint; U-B13, U-C*; I-A4, I-C* |
| **E** | HO-BRD-001 | **DEFERRED by owner 2026-10-10 (HO-BRD skipped).** Stays reader; Q2 diary; endpoint; U-B14, U-D*; I-D* |
| **F** | HO-SUB-001 | **DEFERRED by owner 2026-10-10 (HO-SUB skipped).** Subscriptions reader; U-B15; then the full I-B2, I-B3, I-B8, I-X1 |

The gates judge the whole slice after Phase D (was Phase F; §1 Amended 2026-10-10 finalise); Phases E and F are deferred by owner and re-order is retired. A phase is not a separate handover.

```bash
dotnet build CatEcommerce.sln                       # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Application.Tests
dotnet run --project src/Api/CatEcommerce.Api       # starts clean — I-A1
dotnet test tests/CatEcommerce.IntegrationTests     # shared catecommerce_dev; §16.1
```

No `dotnet-ef migrations add` is part of this slice except I-A2's discarded probe.

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | POR-01 AC1 | Cats, upcoming appointments, boarding stays, recent orders and outstanding balances are returned together for the signed-in customer | U-B3, U-B8, U-B11, U-B13, U-B14, **I-B2** |
| A2 | POR-01 AC2 | With no cats, the response says so explicitly (`hasCats: false`) — and never when the cats section failed | U-B4, U-B5, **I-B1** |
| A3 | POR-01 (FR-POR-1 "everything") | Visit summaries, active prescriptions, subscriptions and per-cat vaccination status are on the same dashboard | U-B6, U-B9, U-B10, U-B15, I-B2, I-B4 |
| A4 | POR-01 | Only the caller's own data appears; deceased cats and unfinalised visits never do | U-B7, U-B9, I-B2, **I-B3** |
| A5 | POR-01 | One failing module leaves the rest of the dashboard usable and marks its section unavailable, not empty | U-B1, U-B2, U-B5, **I-B5** |
| A6 | POR-02 | Re-ordering adds the available items of a past order to the cart in one action | U-C3, U-C4, U-C11, **I-C1** |
| A7 | POR-02 | Unavailable items are listed with the reason | U-C5, U-C6, U-C8, U-C9, I-C2 |
| A8 | POR-02 | Newly prescription-gated items (and expired authorisations) are listed, not added | U-C5, **I-C3, I-C4** |
| A9 | POR-02 | Re-ordering twice does not double the cart; another customer's order is `404` | U-C1, U-C4, I-C5, I-C6, I-C9 |
| A10 | POR-03 | A current or past stay's daily logs and photos are viewable in date order | U-D1, U-D2, U-D4, **I-D1**, I-D2 |
| A11 | POR-03 | Within retention the diary shows media; after Boarding's purge the logs remain and media are gone | U-D6, I-D5 |
| A12 | POR-03 | Media are served only through Boarding's signed expiring URLs; another customer's stay is `404` | U-D3, U-D7, I-D1, I-D3 |
| A13 | ADR-262 | No clinical content on the dashboard | U-A5, U-B10, **I-B7** |
| A14 | ADR-264 | No payment action or endpoint exists | U-A4 |
| A15 | ADR-257/258 | The portal owns no persistence and reads no other module's tables | U-A1, U-A2, **I-A2** |
| A16 | — | Every portal endpoint requires a customer session; the host starts with scope validation on | U-A4, **I-A1**, I-A3, I-A4 |
| A17 | NFR-PRF-3 | Dashboard p95 within 1.5 s for a customer with records in every section | I-B8 |

**Deferred, named in §4.2**: vaccination booking link (HO-CAT-002), discharge summaries and lab
results (HO-EMR), prescription detail (HO-PHM), order detail and tracking (HO-ORD), invoice detail
(HO-BIL), stay summary and photo upload (HO-BRD), subscription management (HO-SUB).

---

## 19. Required gates

In order. Each writes `docs/gates/HO-POR-001-<GATE>.md` before reporting.

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | **No persistence, aggregate, event or handler in `Portal`** (ADR-257); composition only through injected upstream `IQueryHandlerAsync<,>` — no `DbContext`, no nested dispatch (ADR-258); readers as the only mapping site (ADR-259); re-order mutates only `CartAggregate` via Orders' eligibility (ADR-263); §13/§14; `Portal` missing from `.ai/bounded-contexts.md` recorded as OQ-1 |
| 2 | SECURITY | `security-reviewer` | Owner scoping by construction (ADR-261) replacing the bypassed controller filters; `404` indistinguishability; no clinical content (ADR-262, I-B7) and the OQ-4 position; signed URLs passed through, never logged (ADR-265, I-X1); CSRF on re-order; no payment surface (ADR-264); the one catch site logs no customer data |
| 3 | CODE_REVIEW | `code-reviewer` | Sequential composition (ADR-267); `Unavailable` is `null`, never empty; top-up arithmetic; cart saved at most once; no business rule duplicated from Orders |
| 4 | TEST | `test-engineer` | `TC-HO-POR-001.md` from §15/§16 first; upstream data arranged through upstream endpoints (§16.1); I-B5 uses a test-host override, not a production switch; I-C9 with real parallelism |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row against named evidence; all four prior gates `PASS`; every §10.2 assumption reconciled with the real upstream contract or recorded as a finding |

---

## 20. BLOCKED conditions — stop and report, do not work around

**20.1 An upstream handover is not merged.** Do not start its phase and **do not stub its query in
production code** — a fake upstream is a test-only object (U-series, I-B5).

**20.2 An upstream contract differs from §10.2 or lacks a field the portal needs** (for example no
`isFinalised` on visits, no `localDate` on diary entries, no owner query for stays). Adapt the one
reader if the information exists under another name. If it does not exist, **stop and report** —
never read the other module's tables, never add a `DbSet`, never edit its files.

**20.3 A section "only needs one join".** It does not. ADR-258. Stop.

**20.4 HO-ORD-001 exposes no reusable cart-line eligibility** (or no cart lookup/factory). Stop.
**Do not re-implement** sellable stock, FEFO, prescription gating or cat ownership in the portal —
a second copy of BR-2 is how a therapeutic diet gets sold without authorisation.

**20.5 The composing handler does not resolve or run** (the §7 unverified item). Stop and report. Do
not switch to a nested `IInMemoryDispatcher` call or to raw SQL.

**20.6 A payment, pay-link or "pay now" is requested.** It contradicts SRS §1.2 and ADR-264. Report.

**20.7 A requirement conflict is a stop, not an edit** — SRS, backlog and handovers are forbidden
scope.

**20.8 The diary seems to need media bytes proxied or URLs stored.** Stop — ADR-265, NFR-SEC-5.

**20.9 A gate rules that an owner's read of visit or prescription metadata must be access-logged
(OQ-4).** Stop. Do not add a write to a query; the fix is a design decision (for example removing the
section), not a workaround.

**20.10 Do not downgrade `Platform.Infrastructure.Core` below 1.0.34.**
