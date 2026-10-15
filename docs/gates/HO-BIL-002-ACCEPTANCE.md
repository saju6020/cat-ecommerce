GATE: ACCEPTANCE
HANDOVER: HO-BIL-002
COMMITS: 81396c75..717bce9e on ho/BIL-002 (30 non-merge commits; 221ab7e3 is a clean `git merge master`). Code last changed at 006e5a4b. `git diff ec5f21cf..HEAD -- web/` is empty, so everything after the R2 review is docs only (TC sheet, gate files, backlog).
DATE: 2026-10-09
STATUS: PASS

PRIOR GATES (from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: round 1 FAIL (HO-BIL-002-ARCHITECTURE.md: 1 BLOCKER, 1 HIGH). Closed by REVIEW-R2.
- SECURITY: PASS (HO-BIL-002-SECURITY.md).
- CODE_REVIEW: round 1 FAIL (HO-BIL-002-CODE_REVIEW.md: 2 HIGH). Closed by REVIEW-R2.
- REVIEW-R2 (combined ARCHITECTURE + CODE_REVIEW + SECURITY follow-up, per the gate scope rule): PASS. It verified every BLOCKER/HIGH closed.
- TEST: PASS (HO-BIL-002-TEST.md; sheet docs/test-cases/TC-HO-BIL-002.md rows G1-G10, run at 221ab7e3, which has the same web/ tree as HEAD).

CRITERIA (§18)
- A1 MET. U-A5 `documents-ops.test.tsx:47` checks the issue confirm names the customer and total and says the invoice can no longer be changed. Real E-A1 `billing-real-api.spec.ts:95` checks the heading matches `INV-.+-\d{4}-\d{6}`. Real E-A2 `:116` checks the second issue of the same draft is refused (400/409). Mock E-A2 checks the UI shows *already been issued*.
- A2 MET. Mock E-A1 `billing-functional.spec.ts:19` and real E-A1 put Clinic, Shop and Boarding lines on one issued invoice with `15.00%` per line. U-A1 checks the grouping and U-B1 checks tax per line. Note: the real run used manual fees for the clinic and boarding lines, because the Phase J item sources are deferred by owner decision (backlog row 32).
- A3 MET. U-A1/U-A3 `documents-ops.test.tsx:34` check the estimate banner and the rate. U-B1 `:68` checks the rate, net, tax and gross per line and the seller snapshot. Both E-A1 runs check the *Estimate at today's tax rates* banner.
- A4 MET. U-C8 `payment-flows.test.tsx:108` and real E-B1 (`detail.status == 'Paid'`, three payments) cover partial payments. Deposits: `RecordPaymentForm.tsx:38,142,211` sends the `earmark` prop, and `CustomerBillingSummary.tsx` lists the earmarked deposits and the outstanding amount. Real E-C1 loads the customer account page, which renders that panel. The deposit and outstanding figures are evidenced by source, not by assertion (backlog 62).
- A5 MET. U-F2 `money-guard.test.ts` checks the ageing CSV copies the server strings. `AgeingTable.tsx:57-61,87-91` renders the five bucket columns and totals. The real run axe-scans `/staff/billing/receivables`.
- A6 MET. U-C8, mock E-B1, and real E-B1 (prefill 1150 → 750 → 400, *Invoice paid.*).
- A7 MET. U-B2 `payment-flows.test.tsx:156` checks that only listed actions render and there is never an edit control. A grep of components/billing finds no Edit affordance.
- A8 MET. Mock E-C1/E-C2 `billing-functional.spec.ts:108` checks the CN issued, *Released to customer credit*, *Refund now*, and the corrected draft showing *Corrects INV-…*. Real E-C1/E-C2 `billing-real-api.spec.ts:213` checks the CN issued and `correctedByInvoiceId` linked back.
- A9 MET. Real E-D2 `:275` checks that a 700 COD paid with a 500 remittance shows *Shortfall lines*. Mock E-D2 does the same.
- A10 MET. U-E1 (`validateStatementFile`, 1 MiB + 1 byte and .txt), U-E2 (malformed rows by row number), U-E3 (keyboard match, lists shrink), and real E-D3 `:294` (import, auto-match 1, manual match, unmatched lines to 0).
- A11 MET. U-D1/U-D2 `documents-ops.test.tsx:80`, U-D3 `:95`, and real E-D1 `:251` (*Short BDT 10.00*, Explanation `required`, close refused, then closed and read-only).
- A12 MET. U-C1 (terminal slip label, required), U-C2, U-C7, U-C9 (`payment-flows.test.tsx:143`), real E-B2 (reception records 60 000, accountant approves), and real E-B4 (reversed, then re-entered with a new receipt and the invoice Paid). The U-C10 text is asserted in part. `PaymentDetailScreen.tsx:167` builds the receipt, amount, invoice and cash-up wording.
- A13 MET. Real E-C1 checks *Free credit remaining* and the target invoice `paid.amount == '100.00'`. Mock E-C1b does the same.
- A14 MET. Real E-D4 `:325` fetches the same range twice and gets an identical SHA-256 and `X-Export-Sha256`. Mock E-D4 checks the Download ZIP link href.
- A15 MET, with a recorded gap. Mock E-E1 (chromium and mobile-360 PASS, G5/G7) checks the server-returned redemption on the draft invoice (*Redeeming 100 points*). `InvoiceDocumentView.tsx:175-179` renders `invoice.loyaltyRedemption` on the issued staff and portal views. I checked the wire shape against the real backend by reading the source: `StaffLoyaltyController.cs:67` returns `{ invoice }`, and `BillingViewModels.cs:49,66` gives `LoyaltyRedemptionViewModel(int Points, MoneyViewModel Value, Guid LoyaltyEntryId)`. This matches `billing-staff.ts:314-340` and the mock. The real-API E-E1 is NOT RUN (G9) because the dev DB has no loyalty data, and the mock case stops before issue. Not blocking, because the behaviour is evidenced by a passing test plus contract-checked source. Backlog 57.
- A16 MET. U-C4 `payment-flows.test.tsx:68` shows the submit blocked and no request sent. Mock and real E-B3 record zero POSTs. A direct real POST returns `ReferenceLooksLikeCardNumber`, and the body does not echo the value.
- A17 MET. U-C5/U-C6 `payment-flows.test.tsx:83` (same key across a retry, renewed after success), plus `refund-dialog.test.tsx` (2 cases).
- A18 MET. U-C3 `payment-flows.test.tsx:53` (unknown method rendered from its flags, `isOnline` filtered out) and U-F1 `documents-ops.test.tsx:150` (How to pay, no pay-online control). A grep finds no pay-now, card, wallet or gateway surface and no `switch` on a method.
- A19 MET. `billing-a11y-responsive.spec.ts` (G6, 31/31) covers every §8.1 route: all 18 staff billing pages, the admin billing profile, the staff customer page and the 3 portal pages, plus the customer tabs, both receipt widths, the match view and the not-found portal invoice. It checks no horizontal overflow at 360/768/1024/1440 and axe serious/critical = 0 at 360 and 1440. The real run repeats this on 12 staff routes at 360/1440. Gap: only 3 dialog components are axe-scanned (backlog 58).

FINDINGS
- [MEDIUM] web/tests/e2e/billing-functional.spec.ts:140 — E-E1 stops at the draft and was never run on the real API. Nothing asserts the redemption on an issued invoice. Backlog 57.
- [MEDIUM] web/tests/e2e/billing-a11y-responsive.spec.ts:86-101 — §16 E-X1 says "including dialogs". Only ManualChargeDialog, ApplyCreditDialog and the reverse ReasonDialog are scanned. These are not: RefundDialog, AllocatePaymentDialog, the ConfirmDialog instances, and the reject/unmatch/void reason dialogs. E-X2 checks scroll overflow only, not clipped controls. Backlog 58.
- [LOW] web/components/billing/PaymentMethodFields.tsx:83-110 — the collector is a courier or the signed-in user. §11.4's staff-member picker is missing. This is a declared narrowing and is not part of the A12 wording. Backlog 59.
- [LOW] docs/handover/HO-BIL-002.md §1 Amended is still "(none)". The nav-registry substitution and the declared deviations are unrecorded. web/docs/decisions.md marks ADR-FE-081/083 "Built as specified" despite declared deviations. Backlog 60.
- [LOW] Test gaps: U-A6/U-B3 are e2e only, U-X1 is partial, U-C10 is asserted in part, U-C2 asserts presence only, and there is no earmark test. Backlog 62.
- [LOW] docs/gates/HO-BIL-002-TEST.md does not use the docs/gates/README.md header format. Backlog 65.
- Earlier gates' MEDIUM/LOW findings that were still open had no rows in docs/backlog/BIL.md. This gate added them as rows 54-56, 61, 63 and 64. No BLOCKER or HIGH is open.

ADRs
- ADR-FE-079…086 are in web/docs/decisions.md:1110-1174. I compared each Decision and Why with the §9 table and they are word for word. ADR-FE-087/088 are unused. `git diff master...HEAD -- .ai .claude src tests` is empty.

SCOPE CHECK
- files in allowed scope: 131/134 (branch-side diff `master...HEAD`)
- files outside allowed scope:
  - web/nav/areas/billing.ts (+4/−1) and web/components/admin/adminNavItems.ts (+6/−0). These are the web/README-mandated equivalents of §13's three layout nav lines (HO-NTF-002/HO-SHP-002 precedent). ARCHITECTURE and REVIEW-R2 accepted them. The handover defect (§13 names the layouts) is backlog 60.
  - docs/backlog/BIL.md: written under the owner's gate blocking rule (2026-09-27). It is a gate record, not implementation.
- §4 out-of-scope work found: none. There is no online-payment UI, no card field, no edit of issued documents, no new dependency (package.json, tsconfig.json and next.config.ts unchanged), no POS, checkout or boarding money screen, and no thermal printing.
- Forbidden paths (`src/**`, backend `tests/**`, `.ai/**`, `.claude/**`): untouched.

UNDECLARED DEVIATIONS
- ADR-FE-081 says the key is "renewed only after success". After the SECURITY fix, RefundDialog also renews the key on close and on a changed request. The change is recorded in the TC sheet and REVIEW-R2, but not in the ADR or in §1 Amended. LOW, backlog 60.
- The Apply credit visibility is client-derived (`moneySign`) instead of server flags. ARCHITECTURE found it; it was not declared by the developer. MEDIUM, backlog 54(b).
