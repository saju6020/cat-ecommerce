# HO-IMP-002 — Imports, Batches and Inventory (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-IMP-002** |
| Title | Imports, Batches and Inventory — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §9 — **US-IMP-01 … US-IMP-16** |
| SRS | FR-IMP-1 … FR-IMP-16; **UI-1, UI-3, UI-4, UI-6, UI-8 (units always visible)**, **HW-2 (barcode as keyboard-wedge input)**; NFR-USA-4, NFR-USA-5, NFR-USA-6; NFR-SEC-2; NFR-LEG-2; AC-6, AC-10 |
| Pairs with | [`HO-IMP-001`](HO-IMP-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-IMP-001 wins (§20.5) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, stack, `DataTable`, `ConfirmDialog`, `FieldWithUnit`), [`HO-ACC-002`](HO-ACC-002.md) (session guards, `client.ts`, `PermissionDeniedState`, `UnsavedChangesGuard`, error mapping), [`HO-ADM-002`](HO-ADM-002.md) (active branch and `X-Branch-Id`, `lib/format`, money as strings, server-side CSV) |
| Depends on, not yet written | **`HO-SHP-002`** — the product/SKU screens. This slice needs only a **SKU picker** fed by HO-IMP-001's own `GET /api/staff/inventory/skus` (§10, OQ-1) |
| Delivery order | Section 7 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | *(none — append rows here)* |

---

## 2. Objective

Build the **back office for stock**: two sections of the staff application.

- **Procurement** — suppliers, purchase orders with their outstanding quantities, import consignments
  with permits, certificates, costs and documents, landed-cost allocation, and reorder suggestions.
- **Inventory** — goods receipt (the screen a person uses standing at a pallet with a scanner), stock
  by SKU, batch and location, batch actions, transfers, adjustments with approval, cycle counts,
  the immutable ledger, alerts, near-expiry and stock-on-hand reports, storage-breach reports, and
  **the recall console**: declare, trace, notify.

Four things make this harder than a set of CRUD screens:

1. **The server decides everything about stock.** FEFO order, whether a batch is expired, what is
   sellable, whether an adjustment needs approval, which batch actions are allowed — all come from the
   API. The UI never computes any of it (ADR-FE-040).
2. **Quantities are not numbers.** They arrive as strings with a stock unit and up to three decimals,
   and money and unit costs are strings too. Nothing goes through `Number` for arithmetic
   (ADR-FE-041) — and every quantity is shown with its unit (UI-8's discipline, applied to stock).
3. **Receiving and counting are long, physical jobs.** A receipt may have fifty lines typed next to a
   delivery; a count sheet is filled in over an hour. Both must survive a mistyped field, a reload and
   a lost tab (ADR-FE-043, ADR-FE-044).
4. **A recall is the most dangerous button in the product.** It is deliberate, confirm-heavy, and
   notification is always a separate explicit action, never automatic (ADR-FE-046).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack, layering and `components/common` (`DataTable`, `ConfirmDialog`,
Empty/Loading/Error states, `FieldWithUnit`); HO-ACC-002's session model, `client.ts`, route groups,
error mapping, `PermissionDeniedState`, `UnsavedChangesGuard`; HO-ADM-002's active-branch picker and
`X-Branch-Id`, `lib/format` (money as strings, `Intl` zone rendering), and the rule that **CSV is
downloaded from the server, never generated client-side**.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-IMP-01 | Supplier list, create/edit | `/staff/procurement/suppliers/**` |
| US-IMP-02 | PO list, builder (SKU picker, quantities, unit costs), detail with **outstanding per line**, issue / cancel / short-close | `/staff/procurement/purchase-orders/**` |
| US-IMP-03 | Consignment list; detail tabs **Details · Documents · Costs & rates · Batches**; inspection pack view | `/staff/procurement/consignments/**` |
| US-IMP-05 | Landed-cost panel on the consignment: allocate, per-batch landed unit cost, stale banner | `/staff/procurement/consignments/[consignmentId]` (Costs tab) |
| US-IMP-12 | Reorder suggestions with "add to a purchase order"; reorder policies | `/staff/procurement/reorder`, `/staff/inventory/reorder-policies` |
| US-IMP-04 | **Goods receipt** — scan or pick a SKU, lot, dates, accepted / rejected with reason, location, cost; posted in one action | `/staff/inventory/receipts/**` |
| US-IMP-06 | Stock overview by SKU; SKU detail as a **batch × location matrix** | `/staff/inventory/stock/**` |
| US-IMP-07 | FEFO preview panel ("what would be picked next") on the SKU detail | `/staff/inventory/stock/[skuId]` |
| US-IMP-08, 09 | Sellable / held / expired figures everywhere; batch rows state plainly why they cannot be used | `/staff/inventory/stock/**`, `/batches/**` |
| US-IMP-10 | Alerts list and nav badge; near-expiry report with value at risk | `/staff/inventory/alerts`, `/staff/inventory/reports/near-expiry` |
| US-IMP-11 | **Recall console**: declare (type the lot to confirm), trace table with CSV, notify with a count, withdraw, close | `/staff/inventory/recalls/**` |
| US-IMP-13 | Transfers; adjustments with approval queue | `/staff/inventory/transfers/**`, `/staff/inventory/adjustments/**` |
| US-IMP-14 | Counts: blind count sheet, variance report, commit | `/staff/inventory/counts/**` |
| US-IMP-15 | Ledger search (read-only, CSV) | `/staff/inventory/ledger` |
| US-IMP-16 | Storage profiles; storage-breach report and quarantine | `/staff/inventory/storage-profiles`, `/staff/inventory/storage-breaches/**` |
| — | Stock locations (administrator) | `/staff/inventory/locations` |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-IMP-001 owns `src/**` |
| Product and SKU editing, catalogue browsing, storefront availability | HO-SHP-002 |
| Order, POS, picking, dispensing, clinical-use and boarding screens | HO-ORD-002, HO-PHM-002, HO-EMR-002, HO-BRD-002 — they call the same stock contract |
| Margin, revenue and valuation dashboards | HO-RPT-002 (this slice has the two operational reports the stories ask for) |
| Supplier invoices and payments | Not in the backlog |
| **Uploading a document file** | Object storage is not provisioned (HO-IMP-001 §4.3). The form takes validated metadata and a storage key, with an honest note — §11.4, §20.3 |
| A camera barcode scanner, a scanning library, a spreadsheet/CSV library, a chart library, a grid/table library | §10 |
| Editing a ledger entry, a posted receipt or a stock figure | They do not exist server-side |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged: no global store, no token in JS, no `fetch` outside
`client.ts`, no hooks inside components, no `dangerouslySetInnerHTML`, no client-side authorization
decision.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002 and HO-ADM-002 built** (staff shell, session guard, active branch, `lib/format`) | ❌ at baseline — §20.1 |
| HO-IMP-001 API | ❌ — build against MSW generated from its §12.4, then re-run every E-case against the real API |
| HO-SHP-002 | Not required (§10, OQ-1) |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-IMP-001`](HO-IMP-001.md) | **§12.4** endpoints and payload shapes; §10.4 codes to map; §8.3 what may happen at which location kind; §8.6.1 batch states and `allowedActions`; §8.9 what FEFO means (so the preview can explain it); §8.10 the approval rule; §8.12 the recall flow; §8.13 alerts and the reorder formula; §12.3 who may do what |
| [`HO-ADM-002`](HO-ADM-002.md) | §8.3 active branch; §8.4 `lib/format` (money as strings, `Intl` zones); ADR-FE-018 append-only history presentation; §11.7 the server-CSV pattern |
| [`HO-ACC-002`](HO-ACC-002.md) | §11.2 `client.ts`; §11.14 error mapping; `PermissionDeniedState`; `UnsavedChangesGuard` |
| [`HO-CAT-002`](HO-CAT-002.md) | §8–§10 layering, state, conventions, dependencies; `DataTable` (UI-4); `FieldWithUnit` |
| [`docs/SRS.md`](../SRS.md) §3.1, §3.2, §6.4 | UI-1/3/4/6/8, **HW-2**, NFR-USA-4/5/6 |

---

## 7. SRS UI requirements as build rules

| Req | Rule | Where enforced |
|---|---|---|
| **UI-1** | 360 px floor. Every table becomes stacked cards below 768 px; the receipt line editor and the count sheet are one column on a phone and never scroll horizontally; the batch × location matrix becomes a per-batch list | §11.3, §11.5, §11.6, E-X2 |
| **UI-3** | Confirm, stating exactly what is affected: post a receipt (*"{n} lines, {total} units into {location}. A posted receipt cannot be edited."*), issue/cancel/close a PO, post a transfer, request an adjustment (*"This reduces stock by {q} {unit} and cannot be undone — a correction is a new adjustment."*), approve/reject, commit a count (*"{n} variances, {value}."*), quarantine / release / write off a batch, **declare a recall** (type the lot number), **notify** (*"{n} customers will be messaged."*), withdraw a recall, quarantine breach stock, deactivate a location | Each screen |
| **UI-4** | Every list filters, sorts and paginates through `DataTable` with URL-driven state; **CSV for the ledger, near-expiry, stock-on-hand and the recall trace comes from the server** | §11.7, §11.9 |
| **UI-6** | WCAG 2.1 AA. Expiry and stock states are **text first** (*"Expired 3 days ago"*, *"Recalled — held"*), never colour alone; the matrix is a real `<table>` with row and column headers; the scan field announces what it resolved; every dialog is focus-trapped | §11 |
| **UI-8** | **Every quantity is rendered with its stock unit** (`FieldWithUnit` and `formatQuantity`), never a bare number — the same discipline the SRS requires of clinical doses | §8.4, U-X3 |
| **HW-2** | Barcode/QR arrives as **keyboard-wedge input**: a `ScanField` that collects fast keystrokes ending in Enter and resolves through `GET /api/staff/inventory/skus?barcode=`. No camera, no scanning library (ADR-FE-042) | §11.3, §11.5 |
| **NFR-USA-4** | Server messages in plain language; `errorCode` never rendered | HO-ACC-002 §11.14 |
| **NFR-USA-5** | Every editor keeps its values across a validation error; `UnsavedChangesGuard` everywhere; the receipt form and the count sheet additionally autosave a draft (ADR-FE-043, ADR-FE-044) | §11.3, §11.6 |
| **NFR-LEG-2** | Consignment documents are listed with their numbers and dates, superseded ones still visible; there is no delete control | §11.4 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/(staff)/staff/
  procurement/
    layout.tsx                                section nav (Suppliers · Purchase orders · Consignments · Reorder)
    suppliers/page.tsx · new/page.tsx · [supplierId]/page.tsx                   US-IMP-01
    purchase-orders/page.tsx · new/page.tsx · [purchaseOrderId]/page.tsx        US-IMP-02
    consignments/page.tsx · new/page.tsx
      [consignmentId]/layout.tsx  (tabs)
      [consignmentId]/page.tsx            details                               US-IMP-03
      [consignmentId]/documents/page.tsx                                        US-IMP-03
      [consignmentId]/costs/page.tsx      costs, rates, landed cost             US-IMP-03, 05
      [consignmentId]/batches/page.tsx    what arrived                          US-IMP-04, 05
      [consignmentId]/inspection-pack/page.tsx                                  US-IMP-03 AC2
    reorder/page.tsx                        suggestions → build a PO            US-IMP-12
  inventory/
    layout.tsx                              section nav + alert badge
    page.tsx                                overview: alerts, near-expiry teaser, stock search
    stock/page.tsx · stock/[skuId]/page.tsx                                     US-IMP-06, 07, 08
    batches/page.tsx · batches/[batchId]/page.tsx                               US-IMP-06, 09, 11
    receipts/page.tsx · receipts/new/page.tsx · receipts/[receiptId]/page.tsx   US-IMP-04
    transfers/page.tsx · transfers/new/page.tsx                                 US-IMP-13
    adjustments/page.tsx · adjustments/new/page.tsx · adjustments/[adjustmentId]/page.tsx  US-IMP-13
    counts/page.tsx · counts/new/page.tsx · counts/[countId]/page.tsx           US-IMP-14
    ledger/page.tsx                                                             US-IMP-15
    alerts/page.tsx                                                             US-IMP-10, 12
    reports/near-expiry/page.tsx · reports/stock-on-hand/page.tsx · reports/reconciliation/page.tsx
    recalls/page.tsx · recalls/[batchId]/page.tsx                               US-IMP-11
    storage-breaches/page.tsx · new/page.tsx · [breachId]/page.tsx              US-IMP-16
    storage-profiles/page.tsx                                                   US-IMP-16
    reorder-policies/page.tsx                                                   US-IMP-12
    locations/page.tsx                                                          US-IMP-06
  layout.tsx                                ⚠ gains two nav entries: Inventory (with alert badge), Procurement
```

Both sections are `(staff)` only — there is no customer-facing screen in this slice.

### 8.2 API modules — `web/lib/api/`

`procurement-suppliers.ts`, `procurement-orders.ts`, `procurement-consignments.ts`,
`procurement-reorder.ts`, `inventory-lookup.ts` (SKU search and barcode, locations, storage profiles),
`inventory-stock.ts` (stock, batches, FEFO preview, ledger), `inventory-receipts.ts`,
`inventory-control.ts` (transfers, adjustments, counts), `inventory-alerts.ts`,
`inventory-reports.ts`, `inventory-recalls.ts`, `inventory-breaches.ts`.

### 8.3 State

| Kind | Where |
|---|---|
| Server data | TanStack Query, one key family per resource; **every key includes the active branch** so switching branches refetches; mutations invalidate their family plus `['inventory','alerts','summary']` and the affected stock keys |
| List filters (stock, batches, ledger, alerts, POs, consignments, reports) | **URL search params** — shareable, back-button safe |
| Alert badge | `['inventory','alerts','summary']`, `refetchInterval: 60_000` (ADR-FE-045) |
| **Receipt draft** | `sessionStorage` under `imp.receipt-draft.{staffAccountId}`, autosaved on change, cleared on post or explicit discard (ADR-FE-043). Never `localStorage`, never anything but SKU ids, lots, dates, quantities and costs |
| **Count sheet** | On the **server** — lines are saved as they are counted (`PUT …/counts/{id}/lines`), so two people and a reload cannot lose them (ADR-FE-044) |
| Forms | react-hook-form + zod; quantities and money validated as **decimal strings**, never coerced |

### 8.4 Formatting — `lib/format` additions

- `formatQuantity(value, stockUnit)` → `"12 bags"`, `"0.5 ml"` — trims trailing zeros, keeps the value
  a string, **always appends the unit** (UI-8).
- `formatUnitCost({ amount, currencyCode })` → 4-dp aware, reusing HO-ADM-002's `formatMoney` rules.
- `formatExpiry(expiresOn, isExpired, daysToExpiry)` → `"12 Oct 2026 · 47 days"`, `"12 Oct 2026 ·
  expires today"`, `"12 Oct 2026 · expired 3 days ago"` — **text**, plus a severity token used only to
  add an icon with its own label.
- Dates and instants render through HO-ADM-002's `formatInZone` with the payload's
  `branchTimeZoneId`; local dates stay `yyyy-MM-dd` strings.

### 8.5 Layering rules

Pages fetch through hooks in `lib/hooks/**`; components take data as props and are under ~150 lines
with distinct loading / empty / error states; no component calls `client.ts` directly; every screen
renders `PermissionDeniedState` on a server `403`.

---

## 9. ADRs and design conventions

Record in `web/docs/decisions.md`, continuing HO-APT-002's series. **This handover owns
ADR-FE-039 … ADR-FE-048.**

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-039** | Two route-based sections — `/staff/procurement/**` (buying) and `/staff/inventory/**` (holding) — each with its own nav layout; a person with only `Stock.View` sees Inventory and not Procurement, **as presentation only** | They are different jobs and different roles; every page still renders the server's `403` |
| **ADR-FE-040** | **The server owns every stock decision.** FEFO order, `isExpired` / `daysToExpiry`, sellable and held quantities, `allowedActions`, `requiresApproval`, variance figures and suggested reorder quantities are rendered from the payload; the UI computes none of them and never re-sorts a FEFO plan | An independent client rule silently drifts from BR-3/BR-4 — the two rules with no override |
| **ADR-FE-041** | **Quantities, money and unit costs are strings end to end**, validated by regex (`^\d+(\.\d{1,3})?$` / `(\.\d{1,4})?`) and never passed through `Number`; totals shown come from the server | Float arithmetic on stock and money (C-6) is how a count sheet ends up off by 0.0000001 |
| **ADR-FE-042** | **Barcode input is a keyboard-wedge `ScanField`**: keystrokes arriving faster than 30 ms apart and ending with Enter are treated as a scan, looked up server-side, and announced in a live region; typing and picking from the SKU picker always remain available | HW-2; no camera permission, no scanning library, and a scanner that is just a keyboard cannot break the form |
| **ADR-FE-043** | The **goods-receipt form autosaves a draft to `sessionStorage`** per staff member, restores it with an explicit *"Restore the receipt you were entering?"*, and clears it on post | Fifty lines typed beside a pallet must survive a reload; nothing in the draft is personal data |
| **ADR-FE-044** | The **count sheet saves each line to the server as it is counted** and never shows book quantities while the count is open | Counts are long and shared; blind counting is the control (HO-IMP-001 ADR-108) |
| **ADR-FE-045** | Alerts are a **nav badge polled every 60 s** plus an alerts page; nothing else polls, and a poll never resets an open form or dialog | Expiry and low stock are the two things nobody should have to go looking for (UI-5's spirit, without a live screen) |
| **ADR-FE-046** | The **recall console is a three-step, irreversible-by-design flow**: declare (type the lot number), trace (read, export), notify (explicit, with the recipient count). Notification is never automatic and never fires from the declare step | The most consequential action in the product; AC-10 |
| **ADR-FE-047** | **Expiry and unusable states are text first**, with icons that carry their own labels; an unusable row always says *why* (*"Expired"*, *"Quarantined"*, *"Recalled — held"*, *"In quarantine location"*) and never offers an action the server would refuse | UI-6; US-IMP-09's absoluteness has to be visible, not inferred from a red row |
| **ADR-FE-048** | **Approval-gated actions render from the server's state** (`requiresApproval`, `status`, `allowedActions`), and the approval screens are one queue shared by adjustments and counts | ADR-107's separation of requester and approver must be obvious, not discovered on submit |

| # | Question | Default |
|---|---|---|
| OQ-1 | The SKU picker needs product names; HO-SHP-002 is not written | Use HO-IMP-001's `GET /api/staff/inventory/skus` (it returns code, display name, unit, class, storage); link to HO-SHP-002's product page later |
| OQ-2 | Where should the reorder suggestion → PO hand-off go? | Selected suggestions pre-fill the PO builder through URL params, grouped by preferred supplier |
| OQ-3 | Default near-expiry window | The largest configured threshold (90 days), from the payload — never hardcoded |
| OQ-4 | Should the receipt form default the location? | The branch's warehouse when it exists, else the first location permitting receipt; always changeable per line |
| OQ-5 | How much of the recall trace is shown before export? | Paged 50 at a time, contact details visible (the screen is `Recalls.Manage` only) |

---

## 10. Dependencies

**No new package.** A barcode/scanner library, camera access, a CSV or spreadsheet library, a chart
library, a data-grid library, a date-picker or a decimal library is a **§20 stop**. CSV comes from the
server (HO-ADM-002's rule); tables are `DataTable`; decimals stay strings.

---

## 11. Specified changes — components and screens

### 11.1 Section shells and shared pieces

| Component | Behaviour |
|---|---|
| `ProcurementNav` / `InventoryNav` | Section navigation filtered by `roleIds` against a presentation map (`procurementNavItems`, `inventoryNavItems`) mirroring HO-IMP-001 §12.3 **by name only**; `InventoryNav` carries the alert badge (`{n} open`, text + number) |
| ⭐ `SkuPicker` | Type-ahead over `GET …/skus?search=`; shows code, name, unit, class and a storage chip; keyboard-first; emits `skuId` + profile (OQ-1) |
| ⭐ `ScanField` | ADR-FE-042. On resolve: fills the SKU, announces *"{name} — {code}"*, moves focus to quantity. On no match: *"No SKU has barcode {value}."* and focus stays |
| `LocationSelect` | The branch's locations with kind; options disabled (with the reason in text) when the location's `permittedMovements` exclude the screen's movement |
| ⭐ `QuantityInput` | Decimal string input; `stockUnit` shown as a suffix; refuses more decimals than the SKU allows and whole-only SKUs (`FractionalQuantityNotAllowed` mirrored client-side for immediacy, server is authority) |
| `MoneyInput` / `UnitCostInput` | String inputs with the currency shown; 2 dp / 4 dp |
| ⭐ `ExpiryBadge` | `formatExpiry` text + icon-with-label; `Expired` variant repeats *"cannot be sold, dispensed or fed"* |
| `BatchStateBadge` | `Available` / `Quarantined` / `Recalled — held` / `Written off` / `In quarantine location`, text first (ADR-FE-047) |
| `StockFigures` | `onHand`, `reserved`, `usable`, `sellable`, `held`, `expired` as a labelled list with units; a disclosure explains *"Sellable excludes expired, quarantined, damaged and reserved stock."* (US-IMP-08 AC1 made legible) |

### 11.2 Procurement — US-IMP-01, US-IMP-02, US-IMP-03, US-IMP-05, US-IMP-12

| Component | Behaviour |
|---|---|
| `SupplierTable` / `SupplierForm` | Code, name, country (a `<select>` of ISO names, value = alpha-2), contact, currency, lead-time days, payment terms (text) and term days, notes, active. Deactivate with confirm |
| `PurchaseOrderTable` | Number, supplier, status, **receipt state**, expected delivery, total; filters by status, supplier, receipt state, date range (URL) |
| ⭐ `PurchaseOrderBuilder` | Lines: `SkuPicker` (or `ScanField`), quantity with unit, unit cost in the PO currency, line total (server-computed on save, previewed as a string sum only after the server responds — the draft shows per-line values only); deliver-to location; expected delivery date; currency defaulting from the supplier. Draft save, then **Issue** (confirm). Pre-filled from `?suggestions=` (OQ-2) |
| ⭐ `PurchaseOrderDetail` | Header with status and `allowedActions`; line table showing ordered / accepted / **rejected** / **outstanding** with units — **US-IMP-02 AC2**; receipts list linking to each GRN; **Cancel** (confirm; disabled with an explanation when receipts exist) and **Close short** (reason required) |
| `ConsignmentTable` / `ConsignmentForm` | Reference, origin country, shipment reference, status, expected arrival, POs (multi-select of issued POs in this branch); HS codes (repeatable code + description); permit, health/veterinary certificate and customs declaration numbers with the note that clearance needs all three |
| `ConsignmentStatusStepper` | `Planned → In transit → At customs → Cleared`, each advance confirmed; `ClearanceDocumentsMissing` shown against the missing field |
| ⭐ `ConsignmentDocuments` | Table of kind, number, issued date, file name, size, uploaded by, and *Superseded* rows kept visible (NFR-LEG-2). **Add** takes kind, number, date, file name, content type, size and a storage key with an honest inline note: *"File upload is not available yet — record the document's details and where it is stored."* No delete control (§20.3) |
| ⭐ `ConsignmentCosts` | Cost components (kind, amount, currency, note) and exchange rates (currency → rate to base) edited together and saved with `expectedVersion`; a `409` says *"Someone else changed these costs — reload."* |
| ⭐ `LandedCostPanel` | **Allocate landed cost** (confirm: *"This recalculates the landed unit cost of {n} batches."*); result table per batch: quantity, purchase cost, allocated cost, **landed unit cost**; a **stale** banner when `isStale` (*"Costs or batches changed since the last allocation."*); basis explained in one sentence (by received quantity) — **US-IMP-05 AC1** |
| `ConsignmentBatches` | Batches received on the consignment with lot, expiry, quantity, location, unit cost and basis |
| ⭐ `InspectionPack` | One printable page: consignment details, HS codes, permit / certificate / declaration numbers, POs, every document (incl. superseded) and every batch with lot and expiry — **US-IMP-03 AC2**; `window.print()` with a print stylesheet, no PDF library |
| ⭐ `ReorderSuggestions` | Per SKU × location: usable, reorder point, on order, average daily consumption, lead time, **suggested quantity**, preferred supplier; a disclosure explains the formula in one sentence (HO-IMP-001 §8.13); multi-select → **Create purchase orders** grouped by supplier — **US-IMP-12 AC2** |
| `ReorderPolicyTable` | Reorder point, optional fixed reorder quantity, preferred supplier per SKU × location; rows using the default point are marked *"default"* |

### 11.3 Goods receipt — US-IMP-04 · `/staff/inventory/receipts/new`

⭐ `GoodsReceiptForm` — the screen someone uses standing at a delivery:

1. **Header** — source: *Against a purchase order* (picker, which pre-fills lines with outstanding
   quantities), *Against a consignment* (picker; cleared only), or *Direct*; delivery reference;
   received date (defaults now).
2. **Lines** — `ScanField` at the top adds a line per scan (HW-2); otherwise `SkuPicker`. Per line:
   lot number, manufacture date, expiry date, **accepted** and **rejected** quantities with unit,
   rejection reason + note (shown only when rejected > 0), location (OQ-4), unit cost (pre-filled from
   the PO line and read-only there), *hold for inspection* checkbox, and the SKU's **storage
   requirement** displayed beside the row (*"Refrigerated 2–8 °C"*) — **US-IMP-16 AC1**.
3. **Expiry is required** for food and medicine: the field is marked required from the SKU's
   `expiryRequired`, and the server's `ExpiryDateRequired` maps to it — **US-IMP-04 AC2**.
4. **Autosave** (ADR-FE-043) with a restore prompt; **Discard draft** with confirm.
5. **Post** — confirm naming line count, total units and the location(s), and *"a posted receipt
   cannot be edited; corrections are adjustments"*. A `400` distributes **every** line violation to its
   own field and scrolls to the first.
6. `ReceiptDetail` — read-only: header, lines, the batches created (links), and the ledger entries it
   wrote.

### 11.4 Stock, batches and the ledger — US-IMP-06, 07, 08, 09, 15

| Component | Behaviour |
|---|---|
| `StockTable` | Per SKU: on hand, reserved, **usable**, **sellable**, held, expired, next expiry; filters by text, location, class, *below reorder point* (URL); row → SKU detail |
| ⭐ `SkuStockMatrix` | A real `<table>`: rows = batches (lot, `ExpiryBadge`, `BatchStateBadge`), columns = locations, cells = on hand / reserved with units; totals row; **below 768 px a per-batch list**. Unusable rows are labelled with the reason (ADR-FE-047) — **US-IMP-06** |
| ⭐ `FefoPreviewPanel` | Quantity + purpose (sale / dispense / clinical use / boarding use) + optional location → the server's plan, listed in pick order with lot and expiry; *"This is what would be picked next."*; a shortfall says how much is missing — **US-IMP-07 AC1** made visible |
| ⭐ `BatchDetail` | Provenance (receipt, PO, consignment, supplier, origin), dates, `ExpiryBadge`, status and history, positions per location, unit cost and basis (hidden without `InventoryReports.View`), recall panel when recalled; actions **only** from `allowedActions`: Quarantine (reason), Release, Write off (reason), Recall — each confirmed and each naming the batch and lot |
| ⭐ `LedgerSearch` | Date range **required** (default 7 days, ≤ 366 shown before submit); filters SKU, batch, location, kind, reference; columns: time, kind, quantity with unit and sign, location, batch/lot, reference label, performer, **FEFO override** with its reason; **no edit or delete control anywhere** and a one-line note *"Stock ledger entries are permanent."*; **Export CSV** navigates to the server URL — **US-IMP-15** |

### 11.5 Transfers, adjustments, counts — US-IMP-13, US-IMP-14

| Component | Behaviour |
|---|---|
| ⭐ `TransferForm` | From / to location (the same location disabled with a reason), lines by `ScanField`/`SkuPicker` → batch picker showing each batch's on-hand at the source with `ExpiryBadge`, quantity, optional reason. Confirm names both locations — **US-IMP-13 AC1** |
| ⭐ `AdjustmentForm` | Location; reason (radio list with plain-language help: damage, theft, sampling, internal use, expired, recall disposal, found, correction); **note required**; lines with batch and signed quantity (the sign is fixed by the reason and shown as *"removes"* / *"adds"*); a live *"This will need approval"* hint once the server returns `requiresApproval` on submit. Confirm is explicit about permanence — **US-IMP-13 AC2** |
| `AdjustmentDetail` / `ApprovalQueue` | Status, requester, value, lines; **Approve** / **Reject (note)** for holders of the approval action, with *"You cannot approve your own request"* rendered from the server's refusal; the queue is shared with counts (ADR-FE-048); a `409 InsufficientStock` on approval says *"Stock has moved since this was requested — recount and raise a new adjustment."* |
| ⭐ `CountSheet` | Blind: **no book quantity is shown while the count is open** (ADR-FE-044). Scan or pick a SKU → batch → counted quantity; each line saved as it is entered with a per-row saved indicator; scope (full location / selected SKUs) shown in the header; **Submit** (confirm) |
| ⭐ `CountVariance` | After submit: book, counted, variance and value per line, totals, and *"Nothing has been written to stock yet."* — **US-IMP-14 AC1**. **Commit** (confirm naming the number of variances and the value) → `Committed` or `AwaitingApproval` with an explanation |

### 11.6 Alerts and reports — US-IMP-10, US-IMP-12

| Component | Behaviour |
|---|---|
| `AlertList` | Filters by kind and status; rows: kind in plain words (*"Expires in 60 days"*, *"Below reorder point"*, *"Shortfall recorded"*, *"Storage breach"*, *"Ledger mismatch"*), SKU/batch/location, quantity, raised date; **Acknowledge** (note); resolved alerts shown greyed with their resolution — **US-IMP-10 AC1, US-IMP-12 AC1** |
| ⭐ `NearExpiryReport` | Window selector defaulting to the payload's largest threshold (OQ-3), *include expired* toggle; rows: SKU, lot, expiry, **days remaining**, quantity per location, unit cost, **value at risk**; totals; server CSV — **US-IMP-10 AC2** |
| `StockOnHandReport` | SKU × batch × location with unit cost, basis (*landed* / *purchase*) and value; filters by location and class; server CSV |
| `ReconciliationReport` | Expected empty; when not, each mismatch with ledger sum vs position and a *"report this"* note |

### 11.7 Recall console — US-IMP-11 · `/staff/inventory/recalls/**`

| Component | Behaviour |
|---|---|
| `RecallList` | Active and closed recalls: batch, lot, SKU, declared by/on, notified count, stock still held |
| ⭐ `DeclareRecallDialog` | Opened from `BatchDetail`. Shows the batch, lot, expiry and where its stock is; requires **typing the lot number** to enable the button; reason (internal) and **advice to customers** (the text they receive); confirm: *"All remaining stock of lot {lot} becomes unusable immediately, everywhere. This cannot be undone — only withdrawn."* — **US-IMP-11 AC1** |
| ⭐ `RecallDetail` | Three panels. **1 Stock**: held quantity per location with a link to write it off. **2 Trace** (`RecallTraceTable`): tabs *Received* (date, kind in plain words — bought / dispensed / fed during boarding, quantity, reference, customer with phone and email, cat, current owner), *Returned*, **Unshipped — dispatch blocked**, *Unidentified counter sales*; paged (OQ-5); **Export CSV** from the server — **US-IMP-11 AC2**. **3 Notify**: *"{n} customers have not yet been messaged"*, **Notify everyone** (confirm with the count), then the result (*"{requested} messaged, {alreadyNotified} previously, {remaining} left — run again"*) and a note that every message is logged; a link to HO-NTF-002's notification log filtered to this batch — **US-IMP-11 AC3** |
| `WithdrawRecallDialog` | Reason; confirm: *"The batch goes back to quarantine, not on sale. Messages not yet sent will not be sent."* |
| `CloseRecallDialog` | Note; the server's `RecallStockStillHeld` renders as *"Write off or return the remaining {q} {unit} first."* |

### 11.8 Storage, locations, profiles — US-IMP-16, US-IMP-06

| Component | Behaviour |
|---|---|
| `StorageProfileTable` / `Form` | Per product: condition, min/max °C, handling notes; the server's `InvalidTemperatureRange` maps to the min field |
| ⭐ `StorageBreachForm` | Location, observed date/time, what happened, temperature, duration → on success shows the **affected batches** it flagged; then **Quarantine affected stock** (confirm: *"{q} units move to {quarantine location} pending a decision."*) and **Close** (note) — **US-IMP-16 AC2** |
| `LocationTable` / `Form` | Code, name, kind, active, permitted movements listed as text; administrator only; deactivation shows the server's `StockLocationHasStock` in plain language |

### 11.9 Error mapping — `lib/api/errors.ts`

Append `propertyName → field` entries for every HO-IMP-001 command, and map its §10.4 codes to the
wording used above. The load-bearing ones:

| Code | Message |
|---|---|
| `InsufficientStock` | *"There isn't enough stock left — someone may have just taken it. Refresh and try again."* |
| `BatchExpired` | *"Batch {lot} has expired and cannot be sold, dispensed or fed. There is no override."* |
| `BatchNotUsable` | *"This batch is {status} and cannot be used."* |
| `ExpiryDateRequired` | *"Food and medicine must be received with an expiry date."* (on the line's expiry field) |
| `BatchAlreadyExpired` | *"This stock has already expired — reject it instead of accepting it."* |
| `AllocationOverrideReasonRequired` | *"You're not taking the soonest-expiring batch. Please say why."* |
| `ReceiptExceedsOutstanding` | *"That's more than the purchase order still expects ({outstanding})."* |
| `ApproverMustDiffer` | *"Someone else has to approve your request."* |
| `CountLocationBusy` | *"A count is already open at this location."* |
| `RecallConfirmationMismatch` | *"The lot number you typed doesn't match."* |
| `ConsignmentNotCleared` | *"The consignment must clear customs before goods can be received against it."* |
| `MovementNotPermittedAtLocation` | *"{Location} is a {kind} — that movement isn't allowed there."* |

The server's message is preferred when present; the mapped text is the fallback (HO-ACC-002 §11.14).

---

## 12. Mocks — `web/mocks/handlers/inventory/*.ts`

Generated from HO-IMP-001 §12.4. Fixtures must include: two branches with different locations; SKUs
that are food (expiry required, whole units), medicine (fractional, refrigerated storage) and general;
a PO part-received with outstanding quantities; a consignment in each status with documents (one
superseded), multi-currency costs and rates, an allocation and a **stale** allocation; batches
expiring in 120 / 45 / 0 / −3 days, one quarantined, one recalled, one written off; positions with
reserved quantity and stock in a quarantine location; a FEFO plan and a shortfall; a ledger page
including a FEFO override with its reason; an adjustment below and above the approval threshold plus a
`409 InsufficientStock` on approval; an open count (blind), a submitted count with variances, and a
count needing approval; alerts of every kind with a summary count that changes between polls; a recall
with a trace covering a purchase, a dispense, a boarding feed, an unshipped reservation and
unidentified counter sales, with a merged customer shown as the survivor; notify responses for a first
and a repeat run; a storage breach with affected batches; every `403`; `413` on a CSV export; and one
example of every `400` code in §11.9.

---

## 13. Allowed scope

```
web/app/(staff)/staff/procurement/**
web/app/(staff)/staff/inventory/**
web/app/(staff)/layout.tsx                        ← two nav entries (one with the alert badge) only
web/components/{procurement,inventory,stock,recall}/**
web/components/common/ScanField.tsx
web/lib/api/{procurement-suppliers,procurement-orders,procurement-consignments,procurement-reorder,inventory-lookup,inventory-stock,inventory-receipts,inventory-control,inventory-alerts,inventory-reports,inventory-recalls,inventory-breaches}.ts
web/lib/api/types.ts · web/lib/api/errors.ts      ← append only
web/lib/format/**                                 ← §8.4 additions only
web/lib/hooks/**                                  ← new hooks only
web/lib/validation/**                             ← quantity, unit-cost, lot and HS-code schemas
web/lib/drafts/receipt-draft.ts                   ← ADR-FE-043 sessionStorage helper
web/mocks/handlers/inventory/** · web/mocks/handlers/index.ts
web/tests/components/{procurement,inventory,stock,recall}/**
web/tests/e2e/inventory-*.spec.ts · web/tests/e2e/procurement-*.spec.ts
web/docs/decisions.md                             ← ADR-FE-039…048
docs/test-cases/TC-HO-IMP-002.md · docs/gates/HO-IMP-002-*.md
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**` | HO-IMP-001 owns the server |
| `.ai/**`, `.claude/**` | Governing docs and agents |
| `docs/**` except the two named paths | Inputs |
| Every HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002 and HO-APT-002 file beyond §13's lines | Their areas — in particular `client.ts`, the session code and the active-branch picker are **read and used, never edited** |
| `web/app/(portal)/**`, `web/app/(auth)/**` | No customer screen in this slice |
| `web/package.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs` | No dependency is added (§10) |

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| **A — shared pieces** | | |
| U-A1 | `formatQuantity("12.000","bags")`, `("0.500","ml")`, `("3","each")` | `"12 bags"`, `"0.5 ml"`, `"3 each"` — never a bare number |
| U-A2 | `QuantityInput` on a whole-only SKU: `1.5`; on a fractional SKU: `0.125`, `0.1234` | Refused with the unit-aware message; accepted; refused (3 dp) |
| U-A3 | `ScanField`: keystrokes 10 ms apart ending in Enter; slow typing; unknown barcode | Lookup fired and announced; no lookup; *"No SKU has barcode …"* with focus kept |
| U-A4 | `ExpiryBadge` for +47 d, 0 d, −3 d, no expiry | *"47 days"*, *"expires today"*, *"expired 3 days ago"*, *"No expiry date"* — text present without colour |
| U-A5 | `StockFigures` disclosure | Names expired, quarantined, damaged and reserved as excluded from sellable |
| U-A6 | `LocationSelect` for a transfer at a dispensary vs a sale | Options disabled with the reason in text |
| **B — procurement** | | |
| U-B1 | `PurchaseOrderDetail` with ordered 100, accepted 60, rejected 10 | Outstanding 40 shown with units — **US-IMP-02 AC2** |
| U-B2 | Cancel on a PO with receipts | Control disabled with the explanation; server `400` also rendered |
| U-B3 | `ConsignmentDocuments` with a superseded document | Still listed and labelled; **no delete control exists** |
| U-B4 | Document add form | Storage-key note shown; no file input in the DOM |
| U-B5 | `LandedCostPanel` with `isStale` | Stale banner; allocate confirm names the batch count; landed unit costs shown to 4 dp as strings |
| U-B6 | `ConsignmentCosts` save with `409` | *"Someone else changed these costs"*; values kept |
| U-B7 | `InspectionPack` | Details, POs, all documents (incl. superseded) and batches on one page — **US-IMP-03 AC2** |
| U-B8 | `ReorderSuggestions` disclosure and selection | Formula sentence rendered from the payload's fields; selecting two suppliers' rows builds two POs |
| **C — receiving** | | |
| U-C1 | Food SKU line with no expiry, submitted; server `ExpiryDateRequired` | Field marked required beforehand; error lands on that line's expiry field — **US-IMP-04 AC2** |
| U-C2 | Rejected quantity > 0 | Reason field appears and is required |
| U-C3 | Scan adds a line; the SKU's storage requirement | Line added and announced; *"Refrigerated 2–8 °C"* shown — **US-IMP-16 AC1** |
| U-C4 | Draft autosave, reload, restore, discard | Restore prompt; values returned; discard clears `sessionStorage` |
| U-C5 | Post confirm | Names line count, total units, location and permanence |
| U-C6 | Server `400` with three line violations | Each on its own field; first scrolled to; `errorCode` not in the DOM |
| **D — stock** | | |
| U-D1 | `SkuStockMatrix` at 1024 px / 360 px | `<table>` with headers / per-batch list, no horizontal scroll |
| U-D2 | Matrix with an expired, a quarantined and a recalled batch | Each labelled with its reason; no action offered that the server would refuse — **US-IMP-09** visible |
| U-D3 | `FefoPreviewPanel` with a plan and with a shortfall | Pick order exactly as returned (never re-sorted); shortfall stated — **US-IMP-07 AC1** |
| U-D4 | `BatchDetail` `allowedActions` `[Quarantine, WriteOff]` | Only those two buttons render |
| U-D5 | `BatchDetail` unit cost for a role without `InventoryReports.View` (mock `403` on cost) | Cost panel absent, page still renders |
| U-D6 | `LedgerSearch` without a range; 400-day range; a FEFO-override row | Default 7 days; blocked with the server's rule; override and reason shown; **no edit/delete control** — **US-IMP-15** |
| **E — control** | | |
| U-E1 | `AdjustmentForm` reason `Theft` | Quantity presented as *"removes"*; note required; confirm states permanence |
| U-E2 | Submit returning `requiresApproval` | Status *Pending approval* and the explanation; no ledger claim made in the UI |
| U-E3 | Approve as the requester (server `ApproverMustDiffer`) | *"Someone else has to approve your request."* |
| U-E4 | Approve returning `409 InsufficientStock` | The recount message |
| U-E5 | `CountSheet` while open | **No book quantity anywhere in the DOM**; each line shows saved state — **US-IMP-14** |
| U-E6 | `CountVariance` | *"Nothing has been written to stock yet."*; commit confirm names variance count and value — **US-IMP-14 AC1** |
| U-E7 | `TransferForm` same from/to | Blocked with the reason |
| **F — alerts, reports, recall** | | |
| U-F1 | `AlertList` kinds | Each rendered in plain words; acknowledge posts and refreshes |
| U-F2 | Alert badge poll with an open dialog | Badge updates; dialog untouched (ADR-FE-045) |
| U-F3 | `NearExpiryReport` | Days remaining and value at risk per row; totals; CSV is a server URL (no client generation) — **US-IMP-10 AC2** |
| U-F4 | `DeclareRecallDialog` with the wrong lot typed, then the right one | Button disabled, then enabled; confirm text names immediacy and irreversibility |
| U-F5 | `RecallTraceTable` with a purchase, dispense, boarding feed, unshipped reservation, unidentified sales and a merged customer | All four tabs populated; survivor shown; contact details present — **US-IMP-11 AC2** |
| U-F6 | Notify: first run then repeat | Count in the confirm; result text; *"every message is logged"* with the log link — **US-IMP-11 AC3** |
| U-F7 | `WithdrawRecallDialog` | Says the batch returns to quarantine, not to sale |
| U-F8 | `StorageBreachForm` | Affected batches listed after submit; quarantine confirm names the destination — **US-IMP-16 AC2** |
| **X — cross-cutting** | | |
| U-X1 | Every form: server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X2 | Every form: dirty + navigate | Unsaved-changes confirm (receipt and count included) |
| U-X3 | Scan the DOM of every screen for a quantity without a unit | None (UI-8) |
| U-X4 | Any arithmetic on quantities or money in the diff | None — grep test for `Number(`/`parseFloat` in `components/{inventory,procurement,stock,recall}` and the api modules |
| U-X5 | A page mounted with a `403` mock | `PermissionDeniedState`, nothing else fetched |

## 16. End-to-end test cases

MSW first, then the real API; both runs recorded.

| ID | Case | Expected |
|---|---|---|
| E-A1 | Create a supplier, build and issue a PO, receive part of it | PO shows outstanding; receipt detail lists the batches created — **US-IMP-01, 02, 04** |
| E-A2 | Receive a food SKU without an expiry date | Blocked on the line's expiry field; adding the date posts |
| E-A3 | Receive 20 lines using the scanner (simulated keyboard-wedge input), reload mid-entry, restore, post | Draft restored; posted once; confirm shown before posting |
| E-B1 | Plan a consignment, add documents, clear it, receive against it, allocate landed cost | Inspection pack shows everything; landed unit costs appear on the batches — **US-IMP-03, 05** |
| E-B2 | Try to receive against an uncleared consignment | Refused with the server's plain-language reason |
| E-C1 | SKU with several batches: open the FEFO preview, then quarantine the soonest-expiring batch and preview again | The preview changes to the next batch; the quarantined batch is labelled — **US-IMP-07, 08, 09** |
| E-C2 | Transfer stock between two locations and read the ledger | Two entries, both directions, with the transfer reference — **US-IMP-13 AC1, US-IMP-15** |
| E-D1 | Raise an adjustment above the threshold; approve as a second user | Pending, then posted; the requester cannot approve — **US-IMP-13 AC2** |
| E-D2 | Run a full-location count: count blind, submit, review variances, commit | No book quantities while counting; variance report before anything is written; commit posts — **US-IMP-14** |
| E-E1 | Advance time (mock) so a batch crosses 60 days; open alerts and the near-expiry report | Alert present; report shows days remaining and value at risk — **US-IMP-10** |
| E-E2 | Consume stock below the reorder point; open alerts and reorder suggestions; build a PO from them | Low-stock alert; suggestion with a quantity; PO pre-filled — **US-IMP-12** |
| E-F1 | **Declare a recall, read the trace, export it, notify, then run notify again** | Batch immediately shown as recalled and held; trace covers purchase, dispense, boarding and an unshipped order; CSV downloads; first notify reports the count, the second reports none left — **AC-10, US-IMP-11** |
| E-F2 | Withdraw a recall | Batch quarantined, not available; the recall detail says messages not yet sent will not be sent |
| E-G1 | Report a storage breach, quarantine the affected stock, close it | Batches flagged; stock shown in the quarantine location and excluded from sellable — **US-IMP-16** |
| E-X1 | axe on every route in §8.1 — clean, with errors, with each dialog open (declare recall, notify, approve, commit count) | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |
| E-X3 | Sign in as a nurse and visit `/staff/inventory/receipts/new` and `/staff/procurement/suppliers` | `PermissionDeniedState` from the server's `403`, not a UI-only block |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-IMP-001 §12.4; MSW handlers; `lib/format` additions; `ScanField`,
   `SkuPicker`, `QuantityInput`, `ExpiryBadge`, `BatchStateBadge`, `LocationSelect`, `StockFigures`.
3. Section shells and nav (with the alert badge); locations and storage profiles.
4. Suppliers → purchase orders → consignments (details, documents, costs, landed cost, inspection pack).
5. **Goods receipt** (with draft autosave) → receipt detail.
6. Stock overview → SKU matrix → FEFO preview → batch detail → ledger search.
7. Transfers → adjustments and the approval queue → counts (sheet, variance, commit).
8. Alerts and the badge → near-expiry, stock-on-hand, reconciliation reports → reorder suggestions and policies.
9. **Recall console** → storage breaches.
10. U-series, then E-series on MSW, then E-series against the real API per HO-IMP-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | IMP-01 | Suppliers are maintained with country, currency, lead time and terms | E-A1 |
| A2 | IMP-02 | A PO is raised and issued; outstanding quantity stays visible after a partial receipt | U-B1, U-B2, E-A1 |
| A3 | IMP-03 | A consignment's documents, permit, certificate, declaration and costs are captured and retrievable together | U-B3, U-B4, U-B7, E-B1, E-B2 |
| A4 | IMP-04 | Receiving captures lot, dates, accepted and rejected quantities; food and medicine cannot be received without an expiry date | U-C1…U-C6, E-A2, E-A3 |
| A5 | IMP-05 | Landed cost is allocated and shown per batch, with staleness visible | U-B5, U-B6, E-B1 |
| A6 | IMP-06 | Stock is shown per batch per location, down to 360 px | U-D1, U-D2 |
| A7 | IMP-07 | The FEFO pick order is shown exactly as the server computes it; overrides show their reason | U-D3, U-D6, E-C1 |
| A8 | IMP-08 | Sellable is distinguished from on hand, reserved, held and expired, and the exclusions are explained | U-A5, U-D2, E-C1 |
| A9 | IMP-09 | An expired batch is labelled as unusable and no action is offered that would use it | U-A4, U-D2, U-D4, E-C1 |
| A10 | IMP-10 | Expiry alerts are visible without looking for them; the near-expiry report shows days remaining and value at risk | U-F1, U-F2, U-F3, E-E1 |
| A11 | IMP-11 AC1 | Declaring a recall is deliberate and its effect is stated before confirming | U-F4, E-F1 |
| A12 | IMP-11 AC2 | The trace lists purchases, dispensing, boarding feeding, unshipped orders and unidentified sales, and exports | U-F5, E-F1 |
| A13 | IMP-11 AC3 | Everyone affected is notified in one action and the messages are logged | U-F6, E-F1 |
| A14 | IMP-12 | Low-stock alerts and reorder suggestions lead to a purchase order | U-B8, U-F1, E-E2 |
| A15 | IMP-13 | Transfers post both ends; adjustments need a reason and, above the value, a different approver | U-E1…U-E4, U-E7, E-C2, E-D1 |
| A16 | IMP-14 | Counting is blind; the variance report precedes any change; committing is confirmed | U-E5, U-E6, E-D2 |
| A17 | IMP-15 | The ledger is searchable and exportable, and offers no way to change an entry | U-D6, E-C2 |
| A18 | IMP-16 | Storage requirements appear on receiving and stock screens; a breach flags batches and can quarantine them | U-C3, U-F8, E-G1 |
| A19 | UI-8 / ADR-FE-041 | Every quantity carries its unit; no client-side arithmetic on quantities or money | U-A1, U-A2, U-X3, U-X4 |
| A20 | NFR-SEC-2 | Authorization is the server's: a role without an action sees `PermissionDeniedState` | U-X5, E-X3 |
| A21 | UI-6 / UI-1 | Zero serious/critical axe violations; usable 360–1440 px | E-X1, E-X2 |

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering and state; **no new dependency** (§10); no client-side FEFO, expiry, sellability or approval logic (ADR-FE-040); the scoped edits to `(staff)/layout.tsx` match §13; nothing under `src/**` |
| 2 | SECURITY | `security-reviewer` | Recall trace contact details only behind `Recalls.Manage` and never logged or cached in `sessionStorage`; the receipt draft holds no personal data; CSV downloads come from the server; no file upload path; actions rendered only from `allowedActions`; no authorization decision in the client |
| 3 | CODE_REVIEW | `code-reviewer` | Quantities and money as strings throughout (U-X4); time only through `lib/format`; component size; polling never resets a form or dialog |
| 4 | TEST | `test-engineer` | `TC-HO-IMP-002.md` from §15/§16 first; mock and real runs recorded separately; E-A3 exercises real wedge-style input timing |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A3's document caveat (metadata only, no upload) recorded rather than passed silently |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002). Do not build a
parallel shell, session or branch picker.

**20.2 A screen seems to need client-side stock logic** — computing FEFO, deciding whether a batch is
expired or sellable, or deciding whether an adjustment needs approval. Ask the server; if the endpoint
does not return it, report it (§20.4).

**20.3 Document upload is requested.** There is nowhere to upload to (HO-IMP-001 §4.3). Do not add an
upload route, an object-storage client or a local file write.

**20.4 A screen needs an endpoint or field HO-IMP-001 does not specify.** Report it; do not invent a
mock field and build on it.

**20.5 The real API disagrees with HO-IMP-001 §12.4.** Report it; do not shim.

**20.6 A new dependency seems necessary** — scanner, camera, CSV, charts, grid, decimal (§10). Raise it.

**20.7 Something suggests showing a way round an expired or recalled batch** — a "use anyway" control,
an admin override, a hidden action. There is none (BR-3); report the request.

**20.8 HO-SHP-002 lands with a conflicting SKU picker.** Report it and converge on one component rather
than shipping two.
