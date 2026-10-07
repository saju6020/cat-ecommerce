# Billing & Payments — UX

The clickable design for backlog §12 *Billing and Payments*, built against
[`HO-BIL-002`](../../handover/HO-BIL-002.md), the frontend handover (wire shapes from
[`HO-BIL-001`](../../handover/HO-BIL-001.md) §12.4). It follows
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/billing/index.html (served from docs/design)

There is no hosted canvas for this area yet. `index.html` lists every screen in seven rows.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each loads `./support.js`, the
local runtime copied from `../_shared/`. At render time it replaces each staff screen's `.topnav`
with the shared sidebar and header. Portal screens and the printed receipt keep their own layout.

## The screens

Seven rows, thirty artboards. This is the largest area so far because HO-BIL-002 is the largest
handover: twenty-one routes across the staff app, the admin section and the customer portal. It also
has more acceptance-critical states than any earlier area: card-number refusal, split tender,
approvals, reversal, credit/refund, variance and reconciliation. Every such state is drawn.

| Row | File | Screen | Shows |
|---|---|---|---|
| Home & customer account — US-BIL-03, 08 | `BillingHome.dc.html` | Billing home | Approvals / drafts / today's payments / uncaptured tiles; Capture's captured-vs-skipped result with the reason |
| | `CustomerAccount.dc.html` | Customer billing account | ⭐ summary (outstanding, "Overdue since 3 Sep", free credit, earmarked deposit, unbilled, loyalty) and the Unbilled charges tab; Void only where allowed, "Void via the order" written out elsewhere |
| | `ApplyCreditAndVoid.dc.html` | Apply credit, void charge | Apply-credit form → result with **free credit remaining**; void confirmation with its required reason |
| Invoices — US-BIL-01, 02, 07, 10 | `InvoiceComposer.dc.html` | Composer (draft) | ⭐ Clinic / Shop / Boarding groups, "Already on another invoice" row, preview with rate per line, *"Estimate at today's tax rates"*, loyalty control |
| | `ComposerDialogs.dc.html` | Composer dialogs | Manual charge; issue confirm naming customer and total; in-flight state; discard naming the charges returned |
| | `ComposerErrors.dc.html` | Composer states | 409 reloaded, missing tax rate, missing billing profile (Issue disabled with reason), empty |
| | `InvoiceList.dc.html` | Invoice list | Filters, sort, CSV, pagination; every status in words; corrected pair |
| | `InvoiceDetail.dc.html` | Invoice detail | ⭐ seller/bill-to snapshot, 12-column lines incl. rate %, loyalty share, totals to outstanding, payments; no edit |
| Payments — US-BIL-11, 04, 03 | `RecordPayment.dc.html` | Record payment | ⭐ Card in store: *Terminal slip number* refusing a card number client-side; "Pay the rest"; allocation |
| | `PaymentMethodVariants.dc.html` | Form by method | Cash, transfer, COD (collector required), deposit earmark; the flags each is drawn from; `AllocationExceedsOutstanding` message |
| | `SplitTender.dc.html` | Split tender | ⭐ 1,000 → 400 / 350 / 250 → *Invoice paid*; pending-approval result ends the loop |
| | `PaymentList.dc.html` | Payment list | Accepted, pending, reversed (struck through, with reason), rejected |
| | `PaymentDetail.dc.html` | Payment detail + reverse | Allocation trail; reverse dialog in the UI-3 wording |
| | `ApprovalsQueue.dc.html` | Approvals | ⭐ Approve disabled with *"You recorded this payment"*; approve result; reject with reason |
| | `ReceiptPrint.dc.html` | Receipt print view | No shell; A4 and 80 mm side by side |
| Corrections — US-BIL-05 | `CreditNoteWizard.dc.html` | Credit note, step 2 | ⭐ full vs per line, server-given limits, frozen rates |
| | `CreditNoteResult.dc.html` | Issue, result, refund | Confirm; released-to-credit with *Keep as customer credit* default; refund dialog with `RefundExceedsAvailable` |
| | `CreditNoteDetail.dc.html` | Credit note detail | Lines at original rates, released amount, refunds, re-issue link |
| | `CorrectedReissue.dc.html` | Corrected re-issue | Editable copied lines, original → CN → draft chain, preview |
| Close & reconcile — US-BIL-03 AC2, 12, 06, 09 | `Receivables.dc.html` | Ageing | Bucket totals and per-customer rows as server strings |
| | `CashUp.dc.html` | Cash-up (open) | ⭐ itemised expected cash, carried-in item, *Short ৳10.00*, explanation required, pending-approvals banner, close refused |
| | `CashUpClosed.dc.html` | Close / closed day | Close confirm; reception's permission-denied state; a closed day read-only with who/when |
| | `CodReconciliation.dc.html` | COD | Per-courier totals, overdue flag in words, remittance with shortfall result |
| | `BankStatements.dc.html` | Bank statements | Imports list; client-side .csv / 1 MiB refusal; malformed rows by number; duplicate refusal; template link |
| | `BankMatch.dc.html` | Bank match | ⭐ three panes, auto-match counts, keyboard Select path with announced selection, unmatch with reason |
| | `AccountingExport.dc.html` | Accounting export | Download link, 366-day refusal, permission-denied, run history with identical hashes |
| Admin — US-BIL-02 | `BillingProfile.dc.html` | Billing profile | Per branch legal/tax/address/footer, ≤ 5 bank accounts, "applies from now on" notice |
| Portal — US-BIL-03, 08, 10 | `MyBilling.dc.html` | My billing | Summary, invoices, payments, credit used; no pay-online control |
| | `MyInvoice.dc.html` | My invoice | ⭐ read-only invoice, *How to pay* with the invoice number as reference, not-found state |
| | `MyBillingMobile.dc.html` | 360px | Cards instead of tables (UI-1) |

## The design system

This area uses the [PurrCare design system](https://claude.ai/artifact/Y2WzXo2hnfc7a9ac7GHNvN) and
adds no new component. `Table` (money columns right-aligned, tabular numerals), `Tabs`, `Dialog`
(its `shadow-modal` exception), `Badge`, `Banner` and `Field` cover everything here. The only
recurring new pattern is the **side-by-side state panel**. Where a screen's key moments are a
sequence (form → server result → next step), they are drawn as labelled columns in one artboard
instead of separate boards. Token values are hand-transcribed into each artboard, as in the earlier areas.

## Rules the design encodes

1. **No payment gateway, no card numbers.** The only methods are cash in store, card in store (on
   the terminal: only the *slip number* is kept), bank transfer and cash on delivery. No screen
   has a card form, a wallet or a "pay now". `RecordPayment.dc.html` shows a card number
   typed into the slip field being refused **before sending** (C-5, ADR-FE-084).
2. **The server does the money.** Every total, tax, balance, variance and limit is drawn as a server
   figure. Refusals quote the server's message, which names the available amount
   (*"You can apply at most ৳750.00…"*). Pre-issue figures say *"Estimate at today's tax rates"*.
3. **Corrections look like corrections.** No issued invoice, credit note or payment has an edit
   control. The only paths are reverse, credit note, corrected re-issue and refund, each with its
   consequence stated.
4. **Confirmations name the consequence.** Issue names the customer and total and says it can't
   change. Reverse names the receipt, amount, invoice and the cash-up. Discard lists the charges
   returned, and close-day says it can't be reopened. None of them asks "Are you sure?" alone.
5. **Disabled controls say why.** Approve on your own payment, Issue without a billing profile, Import
   with a refused file. Where an action does not exist for a row, the row says so (*"Void via the
   order"*).
6. **Status is never colour alone.** Every pill has a glyph and a word: *Part-paid*, *Pending
   approval*, *Reversed*, *Short ৳10.00*, *Overdue since 3 Sep*.
7. **Money always carries its currency, and time its zone.** Amounts show ৳ (the build's accessible
   form is "BDT 1,500.00"); every timestamp is labelled Asia/Dhaka.
8. **Split tender is a loop.** Each tender is its own payment and receipt, and the next one is
   pre-filled with the server's new outstanding (ADR-FE-086).

## Known gaps, deliberately visible

- **No live interaction**, as in the earlier areas. Every state is drawn.
- **Portal receipt page not drawn separately.** `/portal/billing/payments/[id]` uses the same receipt
  content as `ReceiptPrint.dc.html` inside the portal header. The receipt links in `MyBilling.dc.html` go nowhere.
- **`BillingSummaryPanel` on HO-ACC-002's customer page is not drawn.** It shows the same figures as
  the summary strip in `CustomerAccount.dc.html`.
- **The 360px check covers the portal only.** The staff screens stack via the shared runtime but
  no staff board was drawn at 360px.
- **Illustrative data.** Figures are internally consistent: the invoice lines, loyalty split, cash-up,
  ageing and portal balances all agree. They are still hand-made, not API responses.
- **Approver identity.** The handover's approver is an *Accountant*; no accountant identity exists
  in the earlier areas, so Rezwan Ahmed (Administrator, who holds the same permission) approves,
  reverses, closes and reconciles.
- **Date inputs are plain text fields**, as HO-BIL-002 §10 forbids a date-picker library.
