# Subscriptions — UX

The clickable design for backlog §11 *Subscriptions*, built against
[`HO-SUB-002`](../../handover/HO-SUB-002.md), the frontend handover. It follows
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/subscriptions/index.html (served from docs/design)

There is no hosted canvas for this area. `index.html` links every screen in the five rows below.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each one loads `./support.js`,
the local copy of the shared runtime in `../_shared/`. The runtime renders the page, replaces a
staff screen's plain top bar with the shared sidebar and header, and makes the fixed-size artboard
fill the window. Customer-portal screens (brand `PurrCare`, no "· staff") are left as designed.

## The screens

Five rows and twenty-two artboards: fifteen for customers and seven for staff. That puts this area
at the top of the 9–22 range, and the handover's scope explains why. It has six stories, a
customer area with seven confirmation dialogs, each of which reads differently after the cut-off,
and two staff lists that acceptance depends on. US-SUB-03 (the reminder) and US-SUB-04 (the
discount setting) have no screen of their own, as HO-SUB-002 §3.2 says. The reminder's link is
drawn in `SkipDialogs`. The discount shows in every estimate.

| Row | File | Screen | Shows |
|---|---|---|---|
| Subscribe — US-SUB-01, 04 | `Subscribe.dc.html` | Subscribe | ⭐ The one-column form: quantity, interval, address and **Add an address**, payment method with COD disabled and Orders' reason as text, a native `<input type="date">` with its range written out, and the estimate with the **Subscription discount −10 %** line |
| | `SubscribePickup.dc.html` | Prescription, pickup, errors | The cat picker ("Mishti — authorised until Thu 14 Jan 2027"), pickup branches, **Pay at the counter**, and an error summary with `PaymentMethodUnavailable` and `InvalidFirstDeliveryDate` mapped to their fields |
| | `SubscribeUnavailable.dc.html` | Explained, not offered | Not subscribable, already subscribed, a prescription diet with no authorised cat (Book a consultation and no Start button), an unknown SKU, and `SubscribeButton` in three states (OQ-1 default: beneath Add to cart) |
| | `SubscribeMobile.dc.html` | Subscribe at 360px | UI-1: the same form with stacked options and 44px targets |
| My subscriptions — US-SUB-01, 02, 05, 06 | `MySubscriptions.dc.html` | My subscriptions | ⭐ Six cards covering every §11.2 warning line. Status is shown as a word and an icon. Where there is no next order, a sentence says why instead of leaving a blank |
| | `MySubscriptionsEmpty.dc.html` | Empty and cancelled | The empty-state copy from §11.2, and the Cancelled tab |
| Detail and control — US-SUB-02, 03 | `SubscriptionDetail.dc.html` | Detail | ⭐ **Next order on Tue 20 Oct**, the cut-off sentence in Asia/Dhaka, `LastChangeAlert` as `role="status"`, terms, actions from `allowedActions` only, and order history with every payment state as text |
| | `SubscriptionDetailLocked.dc.html` | After the cut-off | ⭐ The locked sentence with `followingDeliveryOn`, the server's message naming Thu 8 Oct, and `PendingChangesPanel` with **Withdraw scheduled changes** |
| | `ChangeTermsDialog.dc.html` | Change terms | Three states: editing, after a `409` (values kept), and opened from `deliveryAddressMissing` with the address field focused |
| | `SkipDialogs.dc.html` | Skip and the reminder's link | ⭐ ADR-FE-111. A matching `?skip=` opens a confirmation and the parameter is removed. A stale link explains itself and offers **Skip that one**. `CycleNoLongerSkippable` after confirming shows the server's message and the next date |
| | `PauseDialog.dc.html` | Pause | The UI-3 wording before and after the cut-off, then the paused page ("Paused — no orders until you resume.") |
| | `CancelDialog.dc.html` | Cancel | The required reason (OQ-3), the field error when none is chosen, an optional note up to 500 characters, and the locked wording |
| | `ResumeAndWithdraw.dc.html` | Resume and withdraw | Resume with an optional date, resume refused with `PrescriptionAuthorisationRequired` (Book a consultation), and the withdraw confirmation listing what won't happen |
| | `SubscriptionDetailMobile.dc.html` | Detail at 360px | Panels stacked, and history as cards |
| Suspended — US-SUB-05, 06 | `SuspensionBanners.dc.html` | Suspended | ⭐ `UnpaidCycles` with the branch phone and two *Expired unpaid* orders, next to the banner for each of the other four reasons |
| Staff — US-SUB-05, 06 | `StaffSubscriptions.dc.html` | All | ⭐ Tabs, filters in the URL, sort, pages, **Export CSV**, and the `413` "Too many rows — narrow the filters." |
| | `StaffUnpaid.dc.html` | Unpaid | ⭐ US-SUB-06 AC2: separate **Suspended** and **At risk** groups, each row listing its unpaid orders (date, number, pay-by, state) |
| | `StaffPrescriptionSuspended.dc.html` | Prescription suspended | The tab's fixed filters as chips, and a note that these restart by themselves |
| | `StaffSubscriptionDetail.dc.html` | Staff detail | Read-only panels, customer, records written out ("Never — none recorded"), and Reinstate or Cancel from `allowedStaffActions` |
| | `StaffDialogs.dc.html` | Reinstate and cancel | *"Restart Farhana's subscription? Orders resume from Sat 26 Sep."* with the reason-required error, a reinstate refused for a prescription food, and staff cancel with a required note |
| | `StaffDetailReadOnly.dc.html` | Reception and 403 | E-C3: Reception gets no actions and a sentence saying who can act. U-C5: `PermissionDeniedState` replaces the action instead of appearing as a toast |
| | `CustomerSubscriptionsPanel.dc.html` | Customer page panel | `SubscriptionSummaryPanel` on HO-ACC-002's staff customer page |

## The design system

Built on the PurrCare design system that the earlier areas set up. The pieces used are `Tabs`
(status tabs and staff lists), `Table` (history and staff lists), `Dialog` (every confirmation),
`Badge` (status, always a word plus an icon), `Banner`'s shape (suspension reasons and the locked
panel) and `Field` (radios, native date and number inputs, textareas).

This area adds no new component. It uses one pattern repeatedly: **an option that can't be chosen
stays in the list, disabled, with the reason printed under it**. Cash on delivery over the ৳3,000
limit is the main example. The pattern already existed as a disabled control that says why it is
disabled. Here it is applied to a radio group.

As in the earlier areas, token values are copied by hand into each artboard.

**Customer layer** (added 25 Sep 2026). Customer screens add the warmer PurrCare customer tokens
on top of the usual set: `--surface-page:#fbf6ef`, `--warm:#e8894a` (fill and decoration only,
never text), `--warm-ink:#8a3f14` (7.5:1 on white), `--warm-soft:#fdeee2`, and `--radius-md` of
14px. Teal stays the colour for actions, and staff screens are unchanged. `MySubscriptionsEmpty`
uses the line-art cat and a warmer line. On `MySubscriptions`, the one subscription tied to a cat
(the prescription food) shows a *For Mittens* label. Other subscriptions are not linked to a cat,
so they show none.

## Rules the design encodes

1. **No payment capture anywhere (ADR-FE-113).** There is no card field, saved method or
   auto-charge wording. The payment method is an intention, followed by "No card needed. Each
   delivery creates an order that you pay for by …". Bank details and pay-by windows belong to
   the order page.
2. **The server owns the schedule (ADR-FE-110).** Every "starts with the delivery on …" sentence
   is the server's `message`. Locked wording appears only where `isLocked` would be true. Action
   buttons are drawn from `allowedActions` / `allowedStaffActions`, which is why a paused
   subscription offers only Resume and Cancel, and a Reception user gets none.
3. **A link never acts on load (ADR-FE-111).** `SkipDialogs` shows the parameter being read and
   removed, and a confirmation before anything happens.
4. **Dates are dates. Instants name their zone.** Order dates are drawn date-only ("Tue 20 Oct").
   Cut-offs and pay-by times always carry "(Asia/Dhaka)".
5. **Confirmations state the consequence.** Each one names the product, the delivery affected and
   what still happens: "Your order on Thu 24 Sep will still be placed; the subscription ends after
   it."
6. **Status is never colour alone, and missing data is written out.** Every status and payment
   state is a word with an icon. An empty field says why it is empty, for example "None while
   suspended", "Not needed for this food" or "Never — none recorded".
7. **Money always carries the currency, and an estimate is labelled as one**: "৳3,915.00 per
   delivery", followed by the "Delivery and tax are added …" note.

## Handover ambiguities resolved here

- **Where the Subscribe button sits (OQ-1).** Drawn with the handover's default, beneath Add to
  cart and labelled "Subscribe & save 10 %". It is not drawn on a full product page, because
  HO-SHP-002 is not written.
- **The customer's cancellation reasons (OQ-3)** use HO-SUB-001 §10.1's six customer-chosen values
  in plain words.
- **The Unpaid "At risk" group** is read as one unpaid order below
  `maxConsecutiveUnpaidCycles` (2). HO-SUB-002 names the group but does not define it.
- **What the CatDeceased and CatOwnerChanged banners say.** The handover asks only for "a neutral
  sentence". This design uses a short sentence that names the cat.
- **A customer can't resume a Suspended subscription.** The refused-resume case is therefore drawn
  on a *paused* prescription subscription whose authorisation ended while it was paused.

## Known gaps, deliberately visible

- **No live interaction.** Every state is its own artboard or a panel beside the others.
- **No staff list at 360px.** HO-SUB-002 asks for staff tables to become cards below 768px. The
  pattern is the same as `SubscriptionDetailMobile`'s history cards and Notifications'
  `NotificationLogMobile`, so it is not drawn again.
- **The product page and the customer order page aren't drawn.** They belong to HO-SHP-002 and
  HO-ORD-002, which are not written yet (FA-1, FA-2). Order numbers are links to nowhere.
- **Fixture data is illustrative.** The prices, the ৳3,000 COD limit, the branch phone and the
  1,284-row count are all invented. No backend exists yet.
- **Native date inputs show in the browser's own format** (for example 09/26/2026). The helper
  text beside each one gives the range in words, as HO-SUB-002 §7 UI-6 asks.
