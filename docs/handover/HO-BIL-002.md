# HO-BIL-002 — Billing and Payments (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-BIL-002** |
| Title | Billing and Payments — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §12 — **US-BIL-01 … US-BIL-12** |
| SRS | FR-BIL-1 … FR-BIL-13; **C-5**; **UI-1, UI-3, UI-4, UI-6**; NFR-USA-4, NFR-USA-5; NFR-SEC-5 (upload); NFR-LEG-3 (what an invoice shows); HW-1 (receipt printing — browser print only here); AC-16 |
| Pairs with | [`HO-BIL-001`](HO-BIL-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-BIL-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold), [`HO-ACC-002`](HO-ACC-002.md) (sessions, `client.ts`, error mapping, customer search and customer page, `PermissionDeniedState`, `UnsavedChangesGuard`), [`HO-ADM-002`](HO-ADM-002.md) (active branch, `lib/format` incl. `formatMoney`, admin section, UI-4 list pattern), [`HO-NTF-002`](HO-NTF-002.md) (inbox links land on invoices/receipts), [`HO-APT-002`](HO-APT-002.md) (`useServerNow`, front-desk layout) |
| Delivery order | Section 11 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | *(none — append rows here)* |
| Amendments to other handovers | The coordinator appends §1 Amended rows to **HO-ACC-002** (customer page gains a `BillingSummaryPanel`; the error map gains Billing codes) and **HO-ADM-002** (admin nav gains *Billing profile*) |

---

## 2. Objective

Give **reception, shop and accounts staff** the screens to turn charges into invoices, take money by
every v1 method, correct mistakes the only allowed way, and close the day and the books — and give
**customers** a clear, itemised view of what they owe and have paid.

- **Staff**: a customer's billing account (balance, credit, unbilled charges, invoices, payments);
  an invoice composer that combines clinic, shop and boarding charges; issue; record payments with
  split tenders; an approvals queue; reversal, credit notes, corrected re-issue and refunds; the daily
  cash-up; COD and bank reconciliation; receivables ageing; the accounting export; the billing
  profile.
- **Customers**: `/portal/billing` — outstanding balance, invoices with PDF, payments and receipts,
  customer credit and loyalty points, and how to pay by bank transfer.

**There is no payment gateway and no online payment screen** (owner instruction; SRS Phase 5).
Customers never enter card details; staff never enter card numbers either (C-5) — only a terminal slip
number.

Four things make this harder than it looks:

1. **The server does all the money.** Totals, tax, balances, caps and statuses come from the API.
   Amounts are **strings** end to end; the client never parses money into a JavaScript `number`
   (ADR-FE-080).
2. **A double click must not take money twice.** Every money-moving form carries one idempotency key
   for its lifetime (ADR-FE-081).
3. **The method list is data.** Payment fields are rendered from the server's method capability flags,
   so a Phase-5 online method needs no form rewrite (ADR-FE-082).
4. **Corrections look like corrections.** There is no "edit invoice" or "edit payment" anywhere. The
   only actions are the ones the server lists (`allowedActions`), and they are reversal, credit note
   and re-issue (ADR-FE-083).

---

## 3. In scope

### 3.1 Inherited — do not revisit

HO-CAT-002's stack (Next.js 15, React 19, TypeScript strict, Tailwind v4, shadcn/ui, TanStack Query
v5, react-hook-form + zod, Vitest, Playwright + axe, MSW); HO-ACC-002's session model, `client.ts`,
error mapping (`errorCode` never rendered), forbidden shapes (§4.3 there); HO-ADM-002's active branch,
`lib/format` (`formatMoney`, `formatInZone`) and the admin section layout; HO-APT-002's `useServerNow`.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-BIL-03, 08 | **Customer billing account**: summary (outstanding, overdue, free credit, earmarked deposits, unbilled, loyalty), tabs *Unbilled charges* / *Invoices* / *Payments*; **Apply credit** | `/staff/billing/customers/[customerId]` |
| US-BIL-02, 07, ORD-17 | **Invoice composer**: pick unbilled charges (any business line), add a manual charge, loyalty redemption, server preview of lines and tax; save draft; issue | `/staff/billing/invoices/new?customerId` , `/staff/billing/invoices/[invoiceId]` (draft state) |
| US-BIL-01, 02, 05 | Invoice list (filters, CSV); **invoice detail** with number, seller and bill-to, lines with tax rate per line and business line, totals, payments, credit notes, links to corrections; PDF | `/staff/billing/invoices`, `/staff/billing/invoices/[invoiceId]` |
| US-BIL-11, 04, 03 | **Record payment** (method-driven fields, allocation to invoices, earmark, split-tender loop until the balance clears); payment list; payment detail with receipt print, allocations, approval trail | `/staff/billing/payments/new?customerId&invoiceId`, `/staff/billing/payments`, `/staff/billing/payments/[paymentId]` |
| US-BIL-11 AC2 | **Approvals queue** | `/staff/billing/approvals` |
| US-BIL-11 AC3 | **Reverse payment** (reason) from the payment detail | dialog |
| US-BIL-05, FR-BIL-5 | **Credit note** wizard from an invoice; credit-note detail; **corrected re-issue** draft; **refund** dialog | `/staff/billing/invoices/[invoiceId]/credit`, `/staff/billing/credit-notes/[creditNoteId]` |
| US-BIL-03 AC2 | **Receivables ageing** | `/staff/billing/receivables` |
| US-BIL-12, 06 AC3 | **Daily cash-up** | `/staff/billing/cash-up?date` |
| US-BIL-06 AC1, AC2 | **Reconciliation**: *COD* (collections by courier, record remittance, shortfalls) and *Bank* (import statement CSV, auto-match, manual match/unmatch, unmatched both ways) | `/staff/billing/reconciliation/cod`, `/staff/billing/reconciliation/bank`, `/staff/billing/reconciliation/bank/[statementId]` |
| US-BIL-09 | **Accounting export**: range → download; run history | `/staff/billing/exports` |
| US-BIL-02 (NFR-LEG-3) | **Billing profile** per branch (legal name, tax registration, address, footer, bank accounts) | `/staff/admin/billing-profile` |
| — | Unbilled / uncaptured sources and **Capture** | `/staff/billing` (home: pending approvals count, drafts, uncaptured sources, today's payments) |
| US-BIL-03, 08, 10 (customer) | **My billing**: balance, invoices, payments, credit, loyalty; invoice detail with PDF and *How to pay*; receipt | `/portal/billing`, `/portal/billing/invoices/[invoiceId]`, `/portal/billing/payments/[paymentId]` |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-BIL-001 owns `src/**` |
| **Any online payment UI** — card form, wallet button, "pay now", saved cards, gateway redirect | SRS Phase 5; backlog §18.5; owner instruction. §20.3 |
| **Any "edit" of an issued invoice, credit note, payment or refund** | BR-11; HO-BIL-001 ADR-180, ADR-183 |
| Client-side money or tax arithmetic, balance or cap checks | ADR-FE-080 — the server's preview and responses are the only figures |
| Checkout, order, POS and boarding screens that take money | HO-ORD-002, HO-BRD-002 — they reuse `RecordPaymentForm` (§11.4) through its public props |
| Thermal ESC/POS printing (HW-1) | HO-ORD-002 (POS). Here receipts print through the browser (ADR-FE-085) |
| Portal dashboard tile | HO-POR-002 — it reads `/me/billing/summary` |
| Financial reports and charts | HO-RPT-002 |
| Gift vouchers; scheduled exports | Not built (HO-BIL-001 OQ-7, OQ-19) |
| A money, decimal, CSV, PDF, date-picker or table library | §10 |

Forbidden shapes are HO-ACC-002 §4.3's, plus: **no `Number(`, `parseFloat(`, `+amount` or arithmetic
on a money value** (lint rule, §11.12); **no card-number field** anywhere; **no `switch` on a payment
method value** — only on its flags.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002, HO-ADM-002, HO-NTF-002, HO-APT-002 built** | ❌ at baseline — §20.1 |
| HO-BIL-001 API | ❌ — build against MSW generated from §12.4 there; then re-run every E-case against the real API per HO-BIL-001 phase |

HO-ACC-002 §5.1 applies: **mocks may not contain a field HO-BIL-001 does not specify.**

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-BIL-001`](HO-BIL-001.md) | **§12.4** endpoints and shapes; §10.4 codes and messages; §8.6 invoice statuses and `allowedActions`; §8.8 payment statuses, allocation, earmarks, credit; §8.8.1 method flags; §8.8.2 the card-number rule; §8.9 credit notes and refunds; §8.10 cash-up; §12.3 who may do what |
| [`HO-ADM-002`](HO-ADM-002.md) §8.3, §8.4, ADR-FE-013, ADR-FE-018 | Active branch; `formatMoney` (amounts stay strings); admin section; effective-dated records |
| [`HO-ACC-002`](HO-ACC-002.md) §8.4–§8.6, §11.2, §11.14 | Session, client, accessibility rules, error mapping |
| [`HO-APT-002`](HO-APT-002.md) §8.4 | `useServerNow` for "today" on the cash-up |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.4, C-5, NFR-LEG-3 | UI-1/3/4/6; NFR-USA-4/5; what must never be captured; what an invoice must show |

**The `.ai/` documents describe the .NET backend only** and do not govern this diff (HO-CAT-002 §6).

---

## 7. Build rules

| Req | Rule | Where |
|---|---|---|
| **UI-1** | 360 px floor. Tables become stacked cards below 768 px; the composer and payment form are single-column; money columns right-aligned with tabular numerals at ≥ 768 px | §11 |
| **UI-3** | Confirm, stating exactly what is affected: **issue** (*"Issue invoice for BDT 2,340.00 to Rahima Akter? It gets the next number and can no longer be changed."*), discard draft, **reverse payment** (*"Reverse receipt R-000123, BDT 500.00 cash? It will be removed from INV-… and the day's cash-up."*), credit note, refund, close cash-up, unmatch, void charge | Each dialog |
| **UI-4** | Invoice, payment, refund, charge and ageing lists filter, sort, paginate and export CSV (server CSV where HO-BIL-001 offers it) | §11 |
| **UI-6** | WCAG 2.1 AA. Status is text plus colour; money has an accessible form (*"BDT 1,500.00"*, never a bare number); totals regions are `aria-live="polite"` when a preview recomputes; tables have real headers | §11 |
| **C-5** | No field accepts a card number. The reference field rejects a Luhn-valid 13–19-digit run **before submit**, and the value is never sent (ADR-FE-084) | §11.4 |
| **NFR-USA-4/5** | Server messages in plain language (they name available amounts); every form keeps values across errors; `UnsavedChangesGuard` on the composer, payment form, credit wizard and cash-up | HO-ACC-002 §11.14 |
| **NFR-SEC-5** | The statement upload accepts `.csv` ≤ 1 MiB, checked client-side for size and extension (the server re-checks content); the file is sent once and never kept in the browser | §11.9 |
| **NFR-LEG-3** | Invoice views show the seller's legal name, tax registration number and address from the snapshot, never from current settings | §11.3 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (staff)/staff/billing/
    page.tsx                                   billing home                         BIL-02, 11
    customers/[customerId]/page.tsx            customer billing account             BIL-03, 08
    invoices/page.tsx                          list                                 BIL-01
    invoices/new/page.tsx                      composer                             BIL-02, 07, ORD-17
    invoices/[invoiceId]/page.tsx              detail (draft = composer view)       BIL-01, 02, 05
    invoices/[invoiceId]/credit/page.tsx       credit-note wizard                   BIL-05
    credit-notes/[creditNoteId]/page.tsx       credit-note detail                   BIL-05
    payments/page.tsx                          list                                 BIL-11
    payments/new/page.tsx                      record payment                       BIL-11, 04, 03
    payments/[paymentId]/page.tsx              detail, receipt, reverse, refund     BIL-11
    payments/[paymentId]/receipt/page.tsx      print view                           BIL-11
    approvals/page.tsx                         pending approvals                    BIL-11 AC2
    receivables/page.tsx                       ageing                               BIL-03 AC2
    cash-up/page.tsx                           daily cash-up                        BIL-12, 06
    reconciliation/cod/page.tsx                COD                                  BIL-06 AC1
    reconciliation/bank/page.tsx               statements                           BIL-06 AC2
    reconciliation/bank/[statementId]/page.tsx match view                           BIL-06 AC2
    exports/page.tsx                           accounting export                    BIL-09
  (staff)/staff/admin/billing-profile/page.tsx billing profile per branch           BIL-02
  (staff)/staff/customers/[customerId]/page.tsx   ⚠ gains BillingSummaryPanel (HO-ACC-002)
  (staff)/layout.tsx                           ⚠ nav entry: Billing
  (staff)/staff/admin/layout.tsx               ⚠ nav entry: Billing profile (HO-ADM-002)
  (portal)/portal/billing/
    page.tsx                                   my billing                           BIL-03, 08, 10
    invoices/[invoiceId]/page.tsx              invoice + how to pay                 BIL-02
    payments/[paymentId]/page.tsx              receipt                              BIL-11
  (portal)/layout.tsx                          ⚠ nav entry: Billing
```

The inbox links built by HO-BIL-001 (`/portal/billing/invoices/{id}`, `/portal/billing/payments/{id}`)
land on these routes.

### 8.2 API modules

`lib/api/billing-staff.ts` (summary, charges, capture, preview, drafts, issue, invoices, credit notes,
payments, approvals, allocation, credit, earmark, reversal, refunds, ageing, payment methods),
`lib/api/billing-cashup.ts`, `lib/api/billing-reconciliation.ts` (COD, bank, statement upload),
`lib/api/billing-export.ts`, `lib/api/billing-profile.ts`, `lib/api/my-billing.ts` (portal).
Hooks in `lib/hooks/billing/*` — never `useQuery` in components.

### 8.3 State

| Kind | Where |
|---|---|
| Server data | TanStack Query; key families `['billing','customer',id,…]`, `['billing','invoices',…]`, `['billing','invoice',id]`, `['billing','payments',…]`, `['billing','approvals']`, `['billing','cash-up',branch,date]`, `['billing','bank',statementId]`, `['me','billing',…]`; every key includes the active branch where the endpoint is branch-scoped |
| After any money-moving mutation | Invalidate the customer's summary, the invoice(s) touched, the payment, `approvals` and the day's cash-up — the list is one helper, `invalidateAfterPosting(result)`, so nothing shows a stale balance |
| Composer selection (charge ids, redemption points) | Component state; the preview is a query keyed on the sorted ids + points, debounced 400 ms, `placeholderData: keepPreviousData` |
| Idempotency key | `useIdempotencyKey()` — one `crypto.randomUUID()` per form mount, kept across retries of the same submit, **renewed only after a success or an explicit "start again"** (ADR-FE-081) |
| List filters | URL search params |
| Forms | react-hook-form + zod; money inputs as strings validated by `moneyString` (§8.4) |

### 8.4 Money in the browser — `lib/money/` (ADR-FE-080)

- `moneyString` (zod): `^\d{1,10}(\.\d{1,2})?$`, normalised to two decimals as a **string** by string
  manipulation (`"12.5"` → `"12.50"`), never via `Number`.
- `formatMoney` (HO-ADM-002) for display; `a11yMoney` returns *"BDT 1,500.00"*.
- **No comparison or arithmetic helpers.** "Is this more than the outstanding?" is answered by the
  server's `400 AllocationExceedsOutstanding`, whose message names the available amount; the form shows
  it under the field. The one display convenience — *"Pay the rest"* — copies the server's
  `outstanding` string into the field.
- An ESLint rule (`no-restricted-syntax`) forbids `Number(`, `parseFloat(`, `parseInt(` and unary `+`
  in `components/billing/**`, `lib/api/billing-*`, `lib/money/**` (U-X3).

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing the series. **This handover owns ADR-FE-079…088**;
ADR-FE-079…086 are used, 087…088 left free.

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-079** | Billing is a route-based section `(staff)/staff/billing/**` with the **customer billing account** as its hub; the billing profile lives in the admin section (ADR-FE-013); the portal gets `/portal/billing/**` | Reception works customer-first; deep links from the inbox and other areas need stable routes |
| **ADR-FE-080** | **Money stays a string end to end; the client does no money arithmetic or comparison**; composer totals come from the server's preview endpoint, labelled *"Estimate at today's tax rates"* until issue; a lint rule enforces it | HO-BIL-001 ADR-181/194 — one calculator; JavaScript `number` cannot represent 0.10 exactly |
| **ADR-FE-081** | **Every money-moving form sends one `Idempotency-Key` per mount**, reused on retry (network error, `5xx`, double click) and renewed only after success; submit buttons also disable while in flight | HO-BIL-001 ADR-187; a lost response followed by a retry must not record twice |
| **ADR-FE-082** | **Payment-method UI is rendered from `GET /api/billing/payment-methods` flags** (`requiresReference` + `referenceLabel`, `requiresCollector`, `countsInTill`, `allowedRefundMethods`); no component switches on a method value; `isOnline` methods (none in v1) are filtered out of staff recording | Phase 5 adds a method without a form rewrite (HO-BIL-001 ADR-182) |
| **ADR-FE-083** | **Actions only from the server's `allowedActions`**; there is no edit affordance on issued documents or payments; corrections are presented as *Reverse*, *Credit note*, *Corrected re-issue* and *Refund*, each with a UI-3 confirm | BR-11; the state machines are the server's |
| **ADR-FE-084** | **C-5 client guard**: reference, notes and refund reference inputs run the same Luhn check as the server on change and on submit; a match blocks submit, clears nothing else, and shows *"This looks like a card number…"*; the value is never sent, logged or put in the URL | The only way to keep a PAN out of request logs is not to send it |
| **ADR-FE-085** | **Documents**: invoices and credit notes download as the server's PDF; receipts print from a dedicated route with a print stylesheet (A4 and an 80 mm width variant) built from the receipt endpoint's data, text only | One legal rendering (the server's); thermal ESC/POS is POS's (HO-ORD-002) |
| **ADR-FE-086** | **Split tender is a loop, not a multi-row form**: after each accepted payment the form shows the server's new outstanding and offers *"Take another payment"* pre-filled with it, until `0.00`; pending-approval results end the loop with an explanation | Each tender is its own record with its own receipt and approval rule (US-BIL-04, US-BIL-11) |

| # | Question | Default |
|---|---|---|
| OQ-1 | Should the composer auto-select all unbilled charges for the customer? | No — none selected; *"Select all"* available, grouped by business line |
| OQ-2 | Receipt paper widths | A4 and 80 mm |
| OQ-3 | Cash-up denomination breakdown (notes/coins) or a single counted total? | Single total (the API takes one amount) |
| OQ-4 | Portal: show drafts or pending payments? | Never (server does not return them) |

---

## 10. Dependencies

**No new package.** Everything is on HO-CAT-002 §10's list. A decimal/money library, a CSV parser, a
PDF viewer, a print library or a data-grid library is a §20.6 stop. The browser's `crypto.randomUUID()`
and `FormData` suffice.

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error / permission-denied states.
Money always via `formatMoney`; dates in the branch zone via `lib/format`.

### 11.1 Billing home and customer account — US-BIL-03, 08

| Component | Behaviour |
|---|---|
| `BillingHome` | Cards: *Pending approvals ({n})* (approvers only — hidden otherwise, the server still decides), *Drafts*, *Uncaptured charges* (from `/uncaptured-sources`, each with **Capture** → shows `captured`/`skipped` reasons), *Today's payments* at the active branch; customer search (HO-ACC-002) → account |
| ⭐ `CustomerBillingSummary` | Outstanding, overdue (text *"Overdue since 3 Oct"*), free credit, earmarked deposits (label + amount each), unbilled total, loyalty points (when enabled). Actions: **New invoice**, **Record payment**, **Apply credit** (only when free credit > 0 and an invoice has an outstanding — both flags from the server) — **US-BIL-03 AC1** |
| `UnbilledChargesTable` | Date, business line, kind, description, qty, unit price, discount; checkboxes feed the composer; **Void** (reason, confirm) where `allowedActions` has it |
| `CustomerInvoicesTable`, `CustomerPaymentsTable` | Status as text; outstanding; links |
| `ApplyCreditDialog` | Invoice (with outstanding), amount (optional — *"As much as possible"*), result shows `applied[]` and **`freeCreditRemaining`** — **US-BIL-08** |
| `BillingSummaryPanel` | On HO-ACC-002's customer page: the summary figures and a link to the account |

### 11.2 Invoice composer — US-BIL-02, 07, ORD-17

| Component | Behaviour |
|---|---|
| ⭐ `InvoiceComposer` | Left: unbilled charges grouped **by business line** (Clinic / Shop / Boarding headings) with checkboxes and *Select all in group*; **Add manual charge** (`ManualChargeDialog`: service from the public/admin services list — price shown as the server's — or a fee with description, price, tax class, quantity). Right: `InvoicePreview` from `POST …/invoices/preview` — lines with tax class code and **rate**, net, tax, gross, totals; banner *"Estimate at today's tax rates. Rates are fixed when the invoice is issued."* Loyalty: `LoyaltyRedemptionControl` (points input; shown only when enabled and the draft exists). Buttons **Save draft** / **Issue** (confirm, idempotency key) |
| `DraftInvoiceView` | A saved draft reopened: same composer bound to `PUT …/charges` with `expectedVersion`; `409` → *"This draft changed — reloaded."*; **Discard** (confirm names the charges returned to unbilled) |
| Errors | `ChargeAlreadyInvoiced` → the row is marked *"Already on another invoice"* and deselected; `TaxRateNotConfigured` → message naming the tax class with a link for admins to HO-ADM-002's tax page; `BillingProfileMissing` → message with a link to the billing profile |

### 11.3 Invoice detail, credit notes, corrections — US-BIL-01, 02, 05

| Component | Behaviour |
|---|---|
| ⭐ `InvoiceDocumentView` | Number, status (text), issue date, due date; **seller** snapshot (legal name, tax registration, address) and **bill-to** snapshot; lines table with business line, kind, description, qty, unit price, discount, loyalty share, tax class, **rate %**, net, tax, gross; totals (net, discounts, tax, total, credited, paid, **outstanding**); payments applied (receipt, method, amount, date); credit notes; *"Corrects INV-…"* / *"Corrected by INV-…"* links; **Download PDF** — **US-BIL-02**, NFR-LEG-3 |
| `InvoiceActions` | From `allowedActions`: *Record payment*, *Apply credit*, *Credit note*, *Corrected re-issue*; never *Edit* (ADR-FE-083) |
| ⭐ `CreditNoteWizard` | Step 1 kind + reason (required, 1000); step 2 **full** or per line (quantity or amount, capped hints from the server's remaining values); step 3 review with the server's result: CN number, total, **released to customer credit** and the choice *"Keep as customer credit"* (default) or **Refund now** (opens `RefundDialog` pre-linked) — **US-BIL-05 AC2**, FR-ORD-17 |
| `CorrectedDraftEditor` | For a draft created from a credit note: editable copied lines (description, qty, unit price, discount, tax class), preview, issue |
| `CreditNoteView` | Number, invoice link, lines at the original rates, total, released amount, refunds; PDF |

### 11.4 Payments — US-BIL-11, 04, 03

| Component | Behaviour |
|---|---|
| ⭐ `RecordPaymentForm` | Public props `{ customerId, invoiceIds?, earmark?, onRecorded? }` so HO-ORD-002 and HO-BRD-002 can reuse it. Fields: **method** (radio from `payment-methods`, ADR-FE-082); **amount** (`moneyString`; *"Pay the rest"* copies the server's outstanding); **date received** (default today in branch time via `useServerNow`; not future); **reference** — shown and required per `requiresReference` with the server's `referenceLabel` (*"Terminal slip number"*), with the C-5 guard (ADR-FE-084); **collector** when `requiresCollector` (staff member picker or courier code + name); **apply to** invoice(s) with amounts (the server caps/validates); **earmark** (deposit: kind + source + label, pre-filled by the calling area); notes. Submit with the idempotency key. Result panel: status, **receipt number**, applied amounts, `unallocated`, each invoice's new outstanding; for `PendingApproval` → *"Recorded. An accountant must approve payments above BDT 50,000.00 before they count."* (threshold text from the server message) |
| `SplitTenderLoop` | ADR-FE-086: *"Take another payment"* with the new outstanding pre-filled; ends at `0.00` with *"Invoice paid"* — **US-BIL-04** |
| `PaymentDetail` | Method, amount, reference, date received, recorder, approval trail, allocation entries (allocate/release with reasons), refunds, status; actions from `allowedActions`: *Print receipt*, *Allocate*, *Release earmark* (reason), *Refund*, *Reverse* (reason, confirm per §7) |
| ⭐ `ApprovalsQueue` | Pending payments at the active branch: recorder, method, amount, reference, requested allocations; **Approve** (disabled with the text *"You recorded this payment"* when `canApprove` is false — the server still refuses) and **Reject** (reason); approve result shows applied and left-unallocated — **US-BIL-11 AC2** |
| `RefundDialog` | Payment (or from a credit note), method from `allowedRefundMethods`, amount, reference (required for transfer; C-5 guard), reason; server message names the available amount on refusal |
| `ReceiptPrintView` | Route `…/receipt`: seller header, receipt number, customer, method, reference, amount, date, applied to, recorded by; `@media print` A4 and 80 mm (`?width=80`); **Print** button calls `window.print()` |

### 11.5 Receivables ageing — US-BIL-03 AC2

`AgeingTable` — as-of date (default today, branch zone); totals row per bucket (*Current, 1–30,
31–60, 61–90, 90+ days*) and one row per customer with a link to the account; sortable by oldest due;
CSV export built from the same rows as text (no calculation — the server returns the bucket amounts).

### 11.6 Cash-up — US-BIL-12, US-BIL-06 AC3

| Component | Behaviour |
|---|---|
| ⭐ `CashUpScreen` | Date picker (today default; no future); **Expected**: cash, card, transfer, cash refunds, reversals, carried-in items (listed individually), each with its line list expandable; **Counted cash** input (`moneyString`) → `PUT …/count` returns the **variance** (server-computed), shown as *"Short BDT 10.00"* / *"Over …"* / *"Balanced"*; **Explanation** (required by the server when variance ≠ 0 — the field is marked required as soon as the server returns a non-zero variance); pending approvals banner with a link; **Close day** (confirm; `CashUp.Close` holders — others see the server's `403` as `PermissionDeniedState`). Closed days are read-only with who/when |

### 11.7 COD reconciliation — US-BIL-06 AC1

`CodCollectionsTable` (filter courier, status *Unremitted / Shortfall / Remitted*, overdue flag as text)
and `RecordRemittanceForm` (courier, date, total amount, method, reference, then tick collections and
enter the amount remitted for each; the server returns shortfalls and unapplied amount);
`CodReconciliationSummary` per courier — collected, remitted, **shortfall** with line list.

### 11.8 Bank reconciliation — US-BIL-06 AC2

| Component | Behaviour |
|---|---|
| `StatementUpload` | Bank account label, period, file (`accept=".csv"`, ≤ 1 MiB checked before upload); a link to download the **canonical CSV template** (static file in `web/public/templates/bank-statement-template.csv` with the header from HO-BIL-001 OQ-10); server `StatementMalformed` errors listed by row; `StatementDuplicate` explained |
| ⭐ `BankMatchView` | Three panes: **Unmatched statement lines**, **Unmatched transfer payments**, **Matched** (with method *Auto/Manual* and who). **Auto-match** → counts (*matched / ambiguous / none*). Manual: select one line and one payment → **Match** (server enforces equal amounts); **Unmatch** (reason, confirm). Keyboard path: each row has a *Select* button; selections announced — **flagged both ways** |

### 11.9 Accounting export — US-BIL-09

`ExportPanel` — from/to (branch-local dates), optional branch; **Download** (a plain link to the
download endpoint, so the browser streams the ZIP); run history table (who, when, range, counts,
SHA-256 shortened) with the note *"Downloading the same range again produces an identical file."*;
`ExportRangeTooLarge` message.

### 11.10 Billing profile — admin

`BillingProfileForm` per branch (branch picker): legal name, tax registration number, address lines,
phone, email, invoice footer, bank accounts (≤ 5: bank, account name, account number, branch, routing).
A notice: *"Changes apply to invoices issued from now on. Issued invoices keep the details printed on
them."*

### 11.11 Portal — My billing

| Component | Behaviour |
|---|---|
| `MyBillingSummary` | Outstanding, overdue, customer credit, loyalty points (when enabled) |
| `MyInvoicesList`, `MyPaymentsList` | Issued invoices (number, date, total, outstanding, status as text); payments with receipt links |
| ⭐ `MyInvoiceDetail` | The same `InvoiceDocumentView` in read-only customer mode; **Download PDF**; `HowToPay` — the issuing branch's bank accounts from the response, *"Use {invoiceNumber} as the payment reference"*, and *"or pay at the clinic by cash or card"*; **no pay-online button** |
| `MyReceipt` | Receipt data and print |

### 11.12 Error mapping and guards

Append to `lib/api/errors.ts` every HO-BIL-001 command's `propertyName`s and map the §10.4 codes to
the messages used above; the server's message is preferred (it names amounts). Add
`lib/validation/cardNumberGuard.ts` (Luhn, identical rules to HO-BIL-001 §8.8.2) and the ESLint money
rule (§8.4).

---

## 12. Specified changes — mocks

`web/mocks/handlers/billing/*.ts`, generated from HO-BIL-001 §12.4. Fixtures: two branches (one without
a billing profile); a customer with unbilled charges in all three business lines, a draft, invoices in
every status (issued, part-paid, paid, credited), a corrected pair, an earmarked deposit and free
credit; payment methods with the four v1 flag sets; a payment above the threshold (pending) recorded by
the signed-in user and one by another; a reversed payment; credit note with released credit; refunds;
ageing across all buckets; a cash-up with variance, carried-in items and a pending approval; COD
collections with a shortfall; a statement with matched, ambiguous and unmatched lines; export runs;
loyalty on and off; every `403`; `409` stale draft; `TaxRateNotConfigured`; `BillingProfileMissing`;
`ReferenceLooksLikeCardNumber`; idempotent replay returning `200` with the same body.

---

## 13. Allowed scope

```
web/app/(staff)/staff/billing/**
web/app/(staff)/staff/admin/billing-profile/**
web/app/(staff)/staff/admin/layout.tsx                        ← one nav entry only (HO-ADM-002's file)
web/app/(staff)/staff/customers/[customerId]/page.tsx         ← BillingSummaryPanel only (HO-ACC-002's page)
web/app/(staff)/layout.tsx                                    ← one nav entry only
web/app/(portal)/portal/billing/**
web/app/(portal)/layout.tsx                                   ← one nav entry only
web/components/billing/**
web/lib/api/{billing-staff,billing-cashup,billing-reconciliation,billing-export,billing-profile,my-billing}.ts
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/lib/hooks/billing/**
web/lib/money/**
web/lib/validation/cardNumberGuard.ts
web/public/templates/bank-statement-template.csv
web/eslint.config.mjs                                         ← the money rule of §8.4 only
web/mocks/handlers/billing/** · web/mocks/handlers/index.ts
web/tests/components/billing/**
web/tests/e2e/billing-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-079…086
docs/test-cases/TC-HO-BIL-002.md · docs/gates/HO-BIL-002-*.md
```

## 14. Forbidden scope

| Path / shape | Why |
|---|---|
| `src/**`, `tests/**` (backend), `.ai/**`, `.claude/**`, `docs/**` except the two named paths | Backend and inputs |
| Every other HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002, HO-APT-002 file beyond the lines in §13 | Their areas |
| `web/package.json`, `tsconfig.json`, `next.config.ts` | No dependency or config change |
| Any online-payment component, card field, wallet button, gateway script or CSP relaxation for a payment provider | SRS Phase 5; C-5 |
| Order checkout, POS, boarding deposit screens | HO-ORD-002, HO-BRD-002 (they import `RecordPaymentForm`) |

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| U-A1 | Composer groups charges by business line; *Select all in group* | Three groups; selection per group |
| U-A2 | Preview recompute after selection change | One request after 400 ms; totals region announced; previous totals kept while loading |
| U-A3 | Preview banner before issue; issued view | *"Estimate at today's tax rates"* / absent, rates shown per line |
| U-A4 | `ChargeAlreadyInvoiced` on save | Row marked and deselected; message shown |
| U-A5 | Issue confirm text | Names the customer and total; states it can no longer be changed |
| U-A6 | Draft `409` | Reload message; server state shown |
| U-B1 | Invoice detail lines | Tax class, rate %, net, tax, gross per line; business line column; seller tax registration from the snapshot |
| U-B2 | `allowedActions` without edit | No edit control anywhere; only listed actions rendered |
| U-B3 | Credit wizard full vs per-line; result with released credit | Choices; *Keep as credit* default; *Refund now* opens the linked dialog |
| U-C1 | Method `CardInStore` selected | Reference labelled *Terminal slip number*, required; collector hidden |
| U-C2 | Method `CashOnDelivery` | Collector required |
| U-C3 | Methods payload with an extra unknown method with flags | Rendered from flags without code change; `isOnline: true` filtered out |
| U-C4 | Reference `4111 1111 1111 1111` | Submit blocked; message; **no request sent** (MSW spy) — C-5 |
| U-C5 | Double click submit; network error then retry | One key for both requests; one payment in the result |
| U-C6 | Key renewed after success; not after failure | As stated |
| U-C7 | Pending-approval result | Explanation text; split loop ends |
| U-C8 | Split tender 400 → 350 → 250 against 1000 | Loop pre-fills 600, then 250, ends *"Invoice paid"* |
| U-C9 | Approve own payment | Button disabled with reason text; a forced request's `400 ApproverMustDiffer` mapped |
| U-C10 | Reverse confirm | Names receipt, amount, invoice and the cash-up effect |
| U-D1 | Cash-up with non-zero variance returned | Explanation marked required; close refused message shown until filled |
| U-D2 | Cash-up pending approvals | Banner with link; close refused message mapped |
| U-D3 | Closed day | Read-only, who/when |
| U-E1 | Statement upload 1 MiB + 1 byte; `.txt` | Blocked client-side with message |
| U-E2 | `StatementMalformed` rows | Listed by row number |
| U-E3 | Bank match keyboard path | Select line, select payment, Match — Tab/Enter only; selections announced |
| U-F1 | Portal invoice detail | *How to pay* with bank accounts and reference; no pay-online control |
| U-F2 | Ageing table | Buckets as server strings; CSV contains the same strings |
| U-X1 | Every form: server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X2 | Dirty composer / payment form / credit wizard / cash-up + navigate | Unsaved-changes confirm |
| U-X3 | ESLint over billing folders with `Number(amount)` injected | Lint fails |
| U-X4 | Money rendering | Every amount has currency code in its accessible text |

## 16. End-to-end test cases

MSW first, then the real API; both runs recorded.

| ID | Case | Expected |
|---|---|---|
| E-A1 | **AC-16**: customer with a consultation (completed appointment), a shop charge and boarding nights (seeded through the backend port in the real run) → compose → issue | One invoice, three business lines, tax per line, a number in the branch series |
| E-A2 | Issue the same draft from two tabs | One succeeds; the other sees the stale/already-issued message |
| E-B1 | Pay 1000 by cash 400, card 350 (slip), transfer 250 (ref) | Three receipts; invoice *Paid* — **US-BIL-04** |
| E-B2 | Record 60 000 as reception; accountant approves in another context | Pending → approved; invoice updated — **US-BIL-11 AC2** |
| E-B3 | Try a card number as reference | Blocked client-side; nothing in the network log |
| E-B4 | Reverse a payment and re-enter it correctly | Reversed shown; new receipt; balance correct — **US-BIL-11 AC3** |
| E-C1 | Credit note on a paid invoice, keep as credit, then **Apply credit** to another invoice | Credit released then applied; remaining credit shown — **US-BIL-05 AC2, US-BIL-08** |
| E-C2 | Corrected re-issue | New invoice linked both ways |
| E-C3 | Refund cash from released credit | Refund recorded; credit reduced |
| E-D1 | Cash-up with a deliberate 10.00 short count | Variance shown; close refused without explanation; closes with one — **US-BIL-12** |
| E-D2 | COD remittance with a shortfall | Shortfall listed — **US-BIL-06 AC1** |
| E-D3 | Upload template statement; auto-match; match the ambiguous one manually | Unmatched lists shrink both ways — **US-BIL-06 AC2** |
| E-D4 | Download the same export range twice (real API) | Identical file hashes — **US-BIL-09** |
| E-E1 | Loyalty on: redeem points on a draft, issue | Redemption row on the invoice — **US-BIL-10 AC2** |
| E-F1 | Customer opens an invoice from the inbox link; downloads PDF; opens another customer's invoice id | Detail + PDF / not-found state |
| E-X1 | axe on every route in §8.1, including dialogs, the print view and the match view | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |

---

## 17. Implementation order and verification

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-BIL-001 §12.4; MSW handlers; `lib/money`, `cardNumberGuard`,
   `useIdempotencyKey`, `invalidateAfterPosting`, the ESLint rule.
3. Billing profile (admin) → customer billing account and summary panel.
4. Composer, preview, drafts, issue → invoice detail and PDF link.
5. Record payment (method flags, C-5 guard, idempotency) → split loop → approvals → payment detail,
   receipt print, reverse.
6. Credit wizard, corrected re-issue, refunds, apply credit.
7. Ageing → cash-up → COD → bank → export.
8. Portal billing.
9. U-series; E-series on MSW; E-series against the real API as each HO-BIL-001 phase lands.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | BIL-01 | Issued invoices show their sequential number; concurrent issue is handled | U-A5, E-A1, E-A2 |
| A2 | BIL-02 / ORD-17 / AC-16 | Charges from all three business lines combine into one itemised invoice with tax per line | U-A1, U-B1, E-A1 |
| A3 | BIL-07 | Rate per line visible; pre-issue figures are labelled estimates | U-A3, U-B1 |
| A4 | BIL-03 AC1 | Partial payments and deposits recorded; outstanding balance visible on the customer | U-C8, E-B1, §11.1 panel in E-C1 |
| A5 | BIL-03 AC2 | Ageing view by bucket | U-F2 |
| A6 | BIL-04 | Multiple tenders until the balance clears | U-C8, E-B1 |
| A7 | BIL-05 AC1 | No edit of an issued invoice is offered | U-B2 |
| A8 | BIL-05 AC2 | Credit note and linked corrected re-issue | U-B3, E-C1, E-C2 |
| A9 | BIL-06 AC1 | COD shortfalls listed | E-D2 |
| A10 | BIL-06 AC2 | Statement import and matching; unmatched both ways | U-E1…U-E3, E-D3 |
| A11 | BIL-06 AC3 / BIL-12 | Cash-up with expected totals, variance and required explanation | U-D1…U-D3, E-D1 |
| A12 | BIL-11 | Method-driven recording with reference and date; second approval; reversal not edit | U-C1, U-C2, U-C7, U-C9, U-C10, E-B2, E-B4 |
| A13 | BIL-08 | Customer credit applied with remaining balance shown | E-C1 |
| A14 | BIL-09 | Export download; identical on re-run | E-D4 |
| A15 | BIL-10 | Redemption shown on the invoice | E-E1 |
| A16 | C-5 | No card number can be entered or sent | U-C4, E-B3 |
| A17 | ADR-FE-081 | No double recording from double clicks or retries | U-C5, U-C6 |
| A18 | Phase 5 seam | Method UI is data-driven; no online payment UI exists | U-C3, U-F1 |
| A19 | UI-6 / UI-1 | Zero serious/critical axe violations; 360–1440 px | E-X1, E-X2 |

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering; no new dependency; **no client-side money arithmetic** (lint rule present and green); method UI from flags; scoped edits to HO-ACC-002/HO-ADM-002 files and layouts match §13 |
| 2 | SECURITY | `security-reviewer` | C-5 guard blocks sending (U-C4); idempotency keys (U-C5); no online-payment surface; CSV upload bounds; PDF and receipt views render text only; customer routes show only the caller's documents (E-F1) |
| 3 | CODE_REVIEW | `code-reviewer` | Hooks in `lib/hooks/billing`; `invalidateAfterPosting` used by every money mutation; component size; strings for money |
| 4 | TEST | `test-engineer` | `TC-HO-BIL-002.md` first; mock and real runs separate; E-D4 compares hashes |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; nothing from §4 built |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002, HO-NTF-002,
HO-APT-002). Do not build a parallel shell, customer search or money formatter.

**20.2 A screen seems to need client-side money arithmetic** (a running total, a remaining balance, a
tax figure). Ask the server — use the preview endpoint or the mutation response; if the API lacks the
figure, report it (§20.5).

**20.3 An online payment, card entry or "pay now" is requested.** It contradicts the owner's
instruction and SRS Phase 5; report it.

**20.4 The real API disagrees with HO-BIL-001 §12.4.** Report it; do not shim.

**20.5 A screen needs an endpoint or field HO-BIL-001 does not specify.** Report it; do not invent a
mock field.

**20.6 A new dependency seems necessary** (§10). Raise it.
