# Imports & Inventory — UX

The clickable design for backlog §9 *Imports, Batches and Inventory*, built against
[`HO-IMP-002`](../../handover/HO-IMP-002.md), the frontend handover. Fifth area, after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/imports-inventory/index.html (served from docs/design)

There is no hosted canvas for this area yet. `index.html` in this folder lists every artboard in
nine rows that follow HO-IMP-002 §3.2's story table.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each one loads `./support.js`, the
local runtime copied byte-for-byte from `../_shared/`, which puts in the shared staff sidebar and
header and makes the fixed-size artboard fill the window. `index.css` is the shared index stylesheet.
Neither file is edited here.

## The screens

Nine rows, thirty-three artboards. This is the largest area so far, and that matches the handover:
sixteen stories, two staff sections (Procurement and Inventory) and about thirty routes. It is also
the first area where the most dangerous action in the product (declaring a recall) and the longest
physical tasks (receiving a pallet, counting a location) share one slice.

| Row | File | Screen | Shows |
|---|---|---|---|
| Suppliers & purchase orders — US-IMP-01, 02 | `Suppliers.dc.html` | Suppliers | The list beside the edit form, lead time in days with an inline error, the deactivate confirmation naming the open order it doesn't touch |
| | `PurchaseOrderList.dc.html` | Purchase orders | Status and receipt state as two badges, each with an icon and a word |
| | `PurchaseOrderBuilder.dc.html` | PO builder | ⭐ pre-filled from suggestions, the `SkuPicker` open, a whole-units error, line totals left to the server |
| | `PurchaseOrderDetail.dc.html` | PO detail | ⭐ ordered / accepted / rejected / **outstanding** per line with units (US-IMP-02 AC2); Cancel disabled with its reason; close-short with a required reason |
| Consignments & landed cost — US-IMP-03, 05 | `ConsignmentList.dc.html` | Consignments | One consignment in each status; "Stale" written out in the landed-cost column |
| | `ConsignmentDetails.dc.html` | Consignment → Details | The status stepper; clearance refused, with the error on the missing declaration-number field |
| | `ConsignmentDocuments.dc.html` | Consignment → Documents | ⭐ a superseded document kept and labelled, **no delete control**, the add form with the honest "upload isn't available yet" note and no file input |
| | `ConsignmentCosts.dc.html` | Costs, rates & landed cost | ⭐ the `409` conflict with edits kept, the stale banner, landed unit cost per batch to 4 dp, the allocate confirmation naming 3 batches |
| | `InspectionPack.dc.html` | Inspection pack | One printable page with everything US-IMP-03 AC2 asks for |
| Reorder — US-IMP-12 | `ReorderSuggestions.dc.html` | Reorder suggestions | ⭐ the formula in one sentence with a worked example; two suppliers selected make two draft POs |
| Goods receipt — US-IMP-04, 16 AC1 | `GoodsReceipt.dc.html` | Receive goods | ⭐ the `ScanField` announcing what it added, expiry required for food and medicine, rejection reason only when rejected > 0, the storage requirement beside the line, autosave status |
| | `GoodsReceiptErrors.dc.html` | Receive goods — errors | Four violations, each on its own field, with a linked summary |
| | `GoodsReceiptDialogs.dc.html` | Receive goods — dialogs | Restore the draft, post (lines, quantities per unit, locations, permanence), discard |
| | `ReceiptDetail.dc.html` | Receipt detail | Read-only lines, batches created, ledger entries written |
| | `GoodsReceiptMobile360.dc.html` | Receive goods at 360px | The line editor in one column (UI-1) |
| Stock, batches & ledger — US-IMP-06–09, 15 | `StockOverview.dc.html` | Stock | Alert, near-expiry and recall tiles over the `StockTable`, with the sellable explanation |
| | `SkuStockDetail.dc.html` | SKU stock & FEFO preview | ⭐ the batch × location matrix as a real table, unusable rows labelled with their reason; the FEFO plan in server order and a shortfall |
| | `SkuStockMobile360.dc.html` | SKU stock at 360px | The matrix as one card per batch |
| | `BatchDetail.dc.html` | Batch detail | Provenance, positions, history, only the allowed actions, the quarantine confirmation |
| | `Ledger.dc.html` | Stock ledger | ⭐ required range, signed quantities with units, a FEFO override and its reason, no edit/delete, server CSV |
| Transfers, adjustments & counts — US-IMP-13, 14 | `TransferForm.dc.html` | New transfer | Destinations disabled with the reason in text; the confirmation names both locations |
| | `AdjustmentForm.dc.html` | New adjustment | ⭐ reasons in plain words, "removes" fixed by the reason, required note, permanence confirmation, both server outcomes |
| | `ApprovalQueue.dc.html` | Approvals | One queue for adjustments and counts; the requester-can't-approve and "stock has moved" refusals |
| | `CountSheet.dc.html` | Count sheet | ⭐ blind — no book quantity anywhere — with saved / saving / not-saved per row |
| | `CountVariance.dc.html` | Variance & commit | "Nothing has been written to stock yet", the commit confirmation naming the variance count and value, the awaiting-approval result |
| Alerts & reports — US-IMP-10, 12 | `Alerts.dc.html` | Alerts | Every kind in plain words, a resolved one greyed with its resolution, acknowledge with a note |
| | `NearExpiryReport.dc.html` | Near-expiry report | Days remaining and value at risk per row, totals, server CSV |
| Recall console — US-IMP-11 | `RecallList.dc.html` | Recalls | Active, closed and withdrawn |
| | `DeclareRecall.dc.html` | Declare a recall | ⭐ wrong lot (button disabled) beside the right lot, with the irreversibility sentence |
| | `RecallDetail.dc.html` | Recall console | ⭐ three numbered steps — stock, trace, notify — with a merged customer shown as the survivor |
| | `RecallDialogs.dc.html` | Notify, withdraw, close | The notify count, first and repeat results with the log link, withdraw to quarantine, close refused while stock is held |
| Storage, locations & policies — US-IMP-16, 06, 12 | `StorageBreach.dc.html` | Storage breach | Flagged batches and the quarantine confirmation naming the destination |
| | `StockSettings.dc.html` | Locations, storage & reorder policies | Permitted movements in words, the "still holds stock" refusal, the temperature-range error on the min field, "default" reorder points |

Each section has its own tab row inside the page (`ProcurementNav` / `InventoryNav`), and the
Inventory one carries the alert badge as text and a number ("Alerts 7 open").

## The design system

Built on the [PurrCare design system](https://claude.ai/artifact/Y2WzXo2hnfc7a9ac7GHNvN), using its
existing pieces: `Table`, `Tabs` (the section tab rows and the consignment tabs), `Dialog`, `Badge`,
`Banner` and `Field`. This area needed a few patterns that are new, but each is made from those
pieces, so none of them is a new component:

- **Quantity with unit**: a `Field` with the stock unit as a fixed suffix ("bags", "ml", "doses").
  This is how `QuantityInput`/`FieldWithUnit` looks. The same suffix is used for days, °C and hours.
- **Storage chip**: a small accent-soft label ("Refrigerated 2–8 °C"). It is always text.
- **Scan field**: a `Field` in a bordered band with a live-region line under it that says what the
  scan resolved to.
- **State panels**: when a board has to show a dialog without hiding the page, the dialog is drawn
  as a side panel with a small caps label saying when it appears ("When 'Post transfer…' is
  pressed"). Where hiding the page doesn't matter, the dialog sits over a scrim, as in earlier areas.

As in the earlier areas, token values are copied by hand into each artboard rather than installed
live.

## Rules the design encodes

1. **Every quantity carries its unit.** No bare number appears next to stock anywhere, including
   totals, variances, FEFO plans, alerts and confirmations (UI-8). Where one confirmation covers
   several units, each is listed ("18 bags and 96 pouches … 500 ml and 40 doses"). Quantities in
   different units are never added together.
2. **The server decides; the screen reports.** FEFO order, expiry, sellable figures, allowed batch
   actions, whether approval is needed, variances, suggested quantities and every total are labelled
   as the server's. No screen shows client arithmetic (ADR-FE-040, 041).
3. **Unusable stock says why, in words.** "Expired — cannot be sold, dispensed or fed",
   "Quarantined — damaged packaging", "Recalled — held", "In quarantine location". Colour is only
   added on top (ADR-FE-047).
4. **Confirmations state the consequence.** Posting a receipt, closing short, allocating landed cost,
   transferring, adjusting, committing a count, quarantining, declaring, notifying, withdrawing:
   each one names what changes, how much, where, and whether it can be undone. None asks only
   "Are you sure?".
5. **A recall is three separate steps.** Declaring needs the lot typed exactly and says no one is
   messaged yet. Notifying is its own button and shows the count. Withdrawing sends the batch back
   to quarantine, not on sale (ADR-FE-046).
6. **Counting is blind.** `CountSheet.dc.html` has no book-quantity column or hint. Book figures first
   appear on the variance report, next to "Nothing has been written to stock yet" (ADR-FE-044).
7. **Permanent records have no edit control.** The ledger, posted receipts and consignment documents
   show no edit or delete action, and say so in one line (NFR-LEG-2, US-IMP-15).
8. **Disabled controls explain themselves.** Cancelling a PO that has receipts, disabled
   destinations in a transfer, Issue before a clean save, Declare before the lot matches, and approving
   your own request each give the reason in a sentence.
9. **Every time names its zone.** Timestamps carry "(Asia/Dhaka)" or sit under a caption that
   says so. Local dates stay plain dates.

## Handover ambiguities resolved here

- **"{total} units" in the post-receipt confirmation** (UI-3) conflicts with UI-8 when a receipt mixes
  units. The design lists a total per unit instead of adding across units.
- **Outstanding on a PO** is shown as ordered minus accepted, following U-B1 (100 / 60 / 10 → 40).
  Rejected stock stays outstanding until it is replaced or the order is closed short.
- **The alert badge** is drawn on the Inventory tab row inside the page, because the shared sidebar
  in `support.js` has no badge slot.
- **Location permitted-movement wording** (for example "takes medicine only") is illustrative.
  HO-IMP-001 §8.3 holds the real rules, and the UI only shows the reason the server gives.

## Known gaps, deliberately visible

- **No live interaction.** Every state is its own artboard or a labelled side panel.
- **No document upload.** This is on purpose: object storage isn't provisioned (HO-IMP-002 §4, §20.3),
  so the Documents form records metadata and a storage key and says so.
- **No `PermissionDeniedState` artboard.** The server's `403` state comes from HO-ACC-002 unchanged
  and isn't drawn again here.
- **Not drawn as separate boards:** the stock-on-hand and reconciliation reports (the near-expiry
  board shows the shared report layout and its tabs), the consignment Batches tab (its columns
  appear in the landed-cost table), the transfer and adjustment lists, the storage-breach list and
  new-breach form (the breach board shows the same fields read-only after reporting), and the count
  sheet at 360px (same one-column pattern as the receipt line editor).
- **Headless screenshots at 360px** render wider than 360px because of a browser minimum window
  width. Both 360px boards were checked at 520px, where they stay one column.
- **All data is illustrative.** Suppliers, lots, customers and money are made up, and no API exists
  yet for this area.
