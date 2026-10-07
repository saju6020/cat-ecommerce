# Orders — UX

The clickable design for backlog §10 *Cart, Checkout and Orders*, built against
[`HO-ORD-002`](../../handover/HO-ORD-002.md), the frontend handover. Fifth area, after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/orders/index.html (served from docs/design)

There is no hosted canvas for this area yet. `index.html` in this folder links every screen.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each loads `./support.js` (a byte-for-byte
copy of `../_shared/support.js`), which renders them in a browser and, on staff screens, swaps the
simple `topnav` for the shared sidebar and header. Customer screens keep the header they were drawn with.

## The screens

Nine rows, forty artboards: the largest area so far. HO-ORD-002 covers 19 stories and about 30 routes,
roughly twice Administration's scope. It spans both sides of the counter: a customer's cart, checkout and
order history, and the staff queue, money screens, fulfilment, returns and the point of sale.

| Row | File | Screen | Shows |
|---|---|---|---|
| Cart — US-ORD-01, 02 | `Cart.dc.html` | Cart | ⭐ merge notice with a dropped line; price change, "Only 2 left" at its maximum, an unavailable line that blocks checkout; the undo toast instead of a confirm |
| | `CartStates.dc.html` | Cart states | Empty cart; `AddToCartButton` idle / pending / added (live region) / disabled with reason; `CartBadge` hidden at 0; checkout while signed out |
| | `PrescriptionCatDialog.dc.html` | Prescription cat picker | Unauthorised cats disabled with "No current authorisation for this food" and **Book a consultation**; the signed-out version |
| Checkout — US-ORD-02…07, 18 | `CheckoutFulfilment.dc.html` | 1 · Delivery or pickup | "We don't deliver to Gazipur yet" with pickup preselected (US-ORD-06 AC2) |
| | `CheckoutSlot.dc.html` | 2 · Slot | Service levels with the server's charge; slot windows labelled Asia/Dhaka; the skipped-step case (OQ-3) |
| | `CheckoutPayment.dc.html` | 3 · Payment | ⭐ unavailable methods disabled with the server's reason; the three COD reasons side by side; **no card option** |
| | `CheckoutReview.dc.html` | 4 · Review | ⭐ `ChangesPanel` as sentences, grouped, with its acknowledgement; missing prescription cat; rejected discount code; pending policy; six-row `OrderSummary`; Place order disabled, with every reason listed |
| | `CheckoutOutcomes.dc.html` | Placing the order | Ready; `409 CheckoutQuoteChanged` (tick cleared, announced); `409 RequestInProgress`; a `400` sent back to its step |
| | `CheckoutMobile.dc.html` | Review at 360 px | Single column, Place order pinned to the bottom |
| | `OrderPlaced.dc.html` | Order placed · bank transfer | ⭐ reference, copy buttons, countdown on the server clock, "I've made the transfer", and the confirmation that follows |
| My orders — FR-ORD-13, US-ORD-12, 13, 19 | `MyOrders.dc.html` | My orders | Filter, pagination, empty state |
| | `OrderDetail.dc.html` | Order detail · tracking | ⭐ Track your parcel; timeline with no staff names (OQ-6); contact card instead of Cancel; return disabled with reason |
| | `CancelOrder.dc.html` | Cancel an order | The UI-3 confirm; the paid version adds the refund sentence; the contact card after `CancellationNotAvailable` |
| | `ExpiredOrder.dc.html` | Expired transfer | The explanation, **Order again**, and the cart listing what couldn't be added |
| | `ReturnRequest.dc.html` | Request a return | Reason and condition per line, the policy line, a server `400` on its line |
| Staff orders — US-ORD-09, 11, 12, 20 | `OrderQueue.dc.html` | Order queue | ⭐ filters, sort, pagination, 30 s refresh, every flag as a text chip, manifest CSV |
| | `OrderQueueMobile.dc.html` | Queue at 360 px | Cards below 768 px |
| | `StaffOrderDetail.dc.html` | Staff order detail | ⭐ action bar from `allowedActions` only; batch and expiry per line; history with staff names |
| | `DispatchDialog.dc.html` | Dispatch, reallocate, tracking | `BatchRecalled` with a **Reallocate** shortcut; reallocate's mandatory reason; tracking entered later |
| | `RecordDelivery.dc.html` | Record delivery · COD | ⭐ amount, collector and date required; confirmation restates the amount (US-ORD-20 AC2) |
| | `RecordCollection.dc.html` | Record collection | Pickup and pay on collection: balance due read-only, terminal slip reference required for card |
| | `DeliveryFailure.dc.html` | Delivery failure & staff cancel | Only the allowed reasons, and the quarantine sentence (US-ORD-20 AC3). Staff cancel states what is released and which refund becomes due |
| Money — US-ORD-08, 15, 19 | `AwaitingPayment.dc.html` | Awaiting payment | ⭐ countdowns, reported references, "Confirm ৳5,640.00 received against ORD-…?", and the second-approval result |
| | `PaymentApproval.dc.html` | Approve a held payment | Recorder vs second approver; `SecondApproverMustDiffer`; reject needs a note |
| | `Reconciliation.dc.html` | Reconciliation issues | Entered payment details; Resolve needs a note |
| | `Refunds.dc.html` | Refunds due · refund | ⭐ allowed methods only, "Store credit isn't available yet.", suggested and maximum amounts from the server |
| Fulfilment — US-ORD-10 | `PickList.dc.html` | Pick list | ⭐ grouped by location then batch; bulk Start packing with a per-order failure |
| | `PackingSlip.dc.html` | Packing slip | A4 print sheet, no navigation; COD amount prominent |
| Returns — US-ORD-13, 14 | `ReturnsList.dc.html` | Returns | Status filter, the next step for each return |
| | `ReturnDetail.dc.html` | Return review | ⭐ Approve disabled until the flagged line's defect is confirmed with a note |
| | `ReturnReceive.dc.html` | Receive & decide | ⭐ "Received items go to quarantine"; restock to the named original batch or write off; a recalled batch refused on its line |
| Point of sale — US-ORD-16, 18 | `PosSale.dc.html` | Scan and tender | ⭐ scan field with focus ring, unknown barcode announced, prescription line refused, split tenders, Complete sale disabled with the server's shortfall |
| | `PosComplete.dc.html` | Sale complete · void | Change due in large type; void confirmation; "This is already being processed." |
| | `PosReceipt.dc.html` | Receipt | 80 mm print beside the server's 48-column plain text |
| | `PosMobile.dc.html` | POS at 360 px | Stacked basket, tender as a bottom sheet |
| | `CustomerOrdersPanel.dc.html` | Customer page · orders | The panel on HO-ACC-002's customer page; New counter sale for this customer |
| Settings — US-ORD-05, 06, 11, 19 | `DeliveryZones.dc.html` | Delivery zones | Coverage chips, service levels, weight bands, slot windows, Test an address |
| | `DiscountCodes.dc.html` | Discount codes | List and form; a server rejection on its field |
| | `Couriers.dc.html` | Couriers | Template must contain `{tracking}`; a courier without a tracking page |
| | `PaymentSettings.dc.html` | Payment settings | Bank account, customer preview, "Customers will be asked to transfer to this account." |

## The design system

Built on the PurrCare design system. The tokens are hand-copied into each artboard, as in the earlier
areas. This area adds no new component, only new uses of existing ones:

- **`Badge`**: order and flag chips always pair a word with an `aria-hidden` icon. The cat chip (white,
  bordered) marks a prescription line's cat.
- **`Dialog`**: several states of one flow are drawn side by side on a single scrim, for example
  form → confirmation, or refused → shortcut. The screen stays readable with no live interaction.
- **`Table`**, **`Tabs`** (the order views: All / Awaiting payment / Reconciliation / Refunds due),
  **`Field`**, **`Toggle`** and **`Banner`**, as in the earlier areas.
- **Order summary**: one definition list with six rows, always all six (zero rows show `৳0.00`). It is
  used in checkout, order detail, staff detail, POS and the receipt.
- **Print sheets**: the packing slip and the receipt have no app chrome at all. The pick list is shown
  inside the staff shell with its sheet as a preview, because the page has actions (Start packing) that
  are not printed.

**Customer layer** (added 25 Sep 2026). Customer screens add the warmer PurrCare customer tokens
on top of the usual set: `--surface-page:#fbf6ef`, `--warm:#e8894a` (fill and decoration only,
never text), `--warm-ink:#8a3f14` (7.5:1 on white), `--warm-soft:#fdeee2`, and `--radius-md` of
14px. Teal stays the colour for actions, and staff screens are unchanged. Customer checkout and
order screens take the tokens only. `PrescriptionCatDialog`, `PackingSlip` and `PosReceipt` keep
the neutral tokens.

## Rules the design encodes

1. **No payment instrument UI, anywhere.** Payment is a choice of *who you pay later*: the courier, our
   bank, the counter. Card in store records a **terminal slip reference** only, and every screen that
   touches it says so (C-5, ADR-FE-092).
2. **Every amount is the server's.** Totals, delivery charges, "still to pay", change due, suggested and
   maximum refunds are labelled as coming from the server, and no screen implies client arithmetic (C-6).
3. **Changes must be seen before paying.** The changes panel names each product in a sentence, and
   Place order stays disabled until the acknowledgement is ticked *for this quote*. `CheckoutOutcomes`
   shows the tick cleared again after a `409` (ADR-FE-091).
4. **Disabled always says why.** Checkout, unavailable payment methods, unauthorised cats, Place order,
   Approve payment, Approve return, Complete sale and Request a return each show a reason next to the
   control.
5. **Confirmations name the consequence and the amount.** Cancel names the order and "your reserved
   items are released". Money confirmations restate the figure: "Confirm ৳5,640.00 received…", "Record
   ৳4,437.50 cash collected…", "Refund ৳651.00 in cash…", "Restock 1 and write off 1?".
6. **Staff actions come from the server.** The order detail's action bar is exactly the `allowedActions`
   for that status, and refusals are shown as the server's message rather than as missing buttons.
7. **Time names its zone** (Asia/Dhaka) on every slot, deadline, countdown and history row. **Status is
   never colour alone.**
8. **Money forms fire once.** Pending buttons are disabled, and `RequestInProgress` gets its own message
   instead of a resubmit (ADR-FE-097).

## Handover ambiguities resolved here

- **Checkout with an unavailable line.** The cart disables Checkout until the line is removed, and says
  so. The handover says only that the line "cannot be checked out and offers removal".
- **Pickup + pay on collection.** The handover's *Record delivery* action is labelled **Record
  collection** for pickup orders. It is the same dialog with the tender fields (§11.8).
- **The contact card** appears in two places: on a detail where `canCancel` is false (U-C3), and in
  place of Cancel after `CancellationNotAvailable` (§11.6).
- **Non-returnable lines** are left out of the return form, as §11.7 says, and are named in one sentence
  below it so they don't just vanish.
- **Refunds due and reconciliation** live as tabs of the staff Orders page. The handover gives them
  their own routes but no separate nav entries (§12.2).

## Known gaps, deliberately visible

- **No live interaction.** Each state is its own artboard or panel.
- **Cart and checkout overlap with the storefront.** HO-SHP-002 leaves cart and checkout to this
  handover, so they are drawn here. The product page and listing are not. `AddToCartButton` and
  `CartBadge` are shown as they would sit in HO-SHP-002's layout.
- **Admin forms beyond the lists are partial.** The weight-band and slot-window editors are shown
  read-only with an edit link, and the discount product picker is a chip list.
- **All data is illustrative.** No backend exists yet. Courier names and bank details are placeholders.
- **No date-picker widget**, following HO-ORD-002 §10. Date fields are plain inputs.
