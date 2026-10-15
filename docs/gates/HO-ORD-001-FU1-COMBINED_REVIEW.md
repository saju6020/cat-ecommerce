GATE: CODE_REVIEW (combined: architecture + security + code quality, per the gate scope rule for follow-ups)
HANDOVER: HO-ORD-001-FU1
COMMITS: 27e58b2e (diff 2c305b64..27e58b2e)
DATE: 2026-10-10
STATUS: PASS

FINDINGS
- [MEDIUM] src/Application/CatEcommerce.Application/CommandHandlers/Billing/PaymentCommandHandlers.cs:92 — D4: a payment accepted via ApprovalNotRequired records nothing saying approval was waived or why (threshold swapped for decimal.MaxValue). Recorder, method and collector are audited, the waiver is not. Backlog ORD 52.
- [MEDIUM] src/Application/CatEcommerce.Application/CommandHandlers/Billing/PaymentCommandHandlers.cs:92 — D4: the flag is honoured for every method, including BankTransfer. Only Ordering sets it (verified: StaffPaymentsController builds RecordPaymentCommand field by field from RecordPaymentRequest; no other controller or message handler constructs it; Ordering passes it only for COD and counter tenders, TransferConfirmation does not). Billing should restrict it to cash-like methods. Backlog ORD 53.
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Ordering/OrderBillingReader.cs:29-33 — D3: the fallback to "any active 0 % class" also fires when an existing DELIVERY class (possibly set to e.g. 5 %) is deactivated, silently making delivery untaxed, and bills the fee under an unrelated class. Backlog ORD 54.
- [LOW] src/Domain/CatEcommerce.Domain/Aggregates/Order/OrderAggregate.cs:661 — DeliveryTaxAmount is derived by the caller as TaxTotal minus line tax, so the re-sum no longer catches a tax mismatch; no bounds (0 when no delivery charge, <= charge). Backlog ORD 55.
- [LOW] OrderPaymentCommandHandlers.cs (TransferConfirmation.FinaliseAsync) — a held transfer confirmed after a DELIVERY rate change is invoiced at the new rate; invoice total differs from the order total shown to the customer. Documented in ADR-217, same as line tax. Backlog ORD 56.
- [LOW] AccountsCounterBuyerRegistrar / CompleteCounterSaleCommandHandler — D2: unlimited walk-in creation by any Pos.Sell holder, including from refused sales; mobile squatting. Backlog ORD 57.
- [LOW] AccountsCounterCustomerFinder.cs:40 — D1: full staff customer view (email, cats) read up to 10 times just for the merge pointer. Backlog ORD 58.

CHECKED, NO FINDING
- D4 external reach: not bound from HTTP, no other producer. ADR-204 amendment, CONTRACTS #29, HO-BIL-001 and HO-ORD-001 Amended rows agree.
- D1: loop (seen set) and 10-hop bound correct; non-active survivor, missing record and broken pointer all give null -> AccountNotActive; reads are in-process behind Pos.Sell through Accounts' published queries.
- D2: registration happens after shape validation (blank name / bad mobile store nobody), in its own short transaction; the registrar detaches only its own Added entries (snapshot taken before dispatch).
- D3: rounding is half-away-from-zero in CheckoutPricing (MinorUnits.Percent) and in Billing (InvoiceCalculator.TaxOf), tax-exclusive on a single undiscounted fee line, so quote, order and invoice agree. Delivery tax is in TaxTotal/GrandTotal and so in the fingerprint; a rate change between quote and place yields CheckoutQuoteChanged. Counter/pickup carry DeliveryTax null = 0. Migration is data-only, guarded by NOT EXISTS (idempotent), empty Down is justified, model snapshot untouched. Dead DeliveryTaxClassNotConfigured removed. Replaced old-behaviour tests are appropriate; new tests cover merge chain/loop, buyer stored on a refused sale, delivery tax, ApprovalNotRequired.
- No superseded-architecture constructs introduced; Ordering uses only published ports.
- Tests were not re-run by this review.

OWNER QUESTIONS (recommendations)
1. Missing DELIVERY class, 5xx vs 400: keep 5xx (a configuration fault the customer cannot fix) but make it unreachable: refuse deactivating/deleting the DELIVERY class in the tax-class commands, drop the "any 0 %" fallback for an existing-but-inactive DELIVERY (ORD 54), and detect it at quote time or in a health check so customers do not pass the quote and hit a 5xx at placement.
2. Transfers still held: correct under the owner's rule (second approval for transfers only); leave them. Fix the rate-change mismatch (ORD 56) by storing the delivery tax on the order at placement, not by changing the hold.

SCOPE CHECK
- files in allowed scope: 36/36 (src, tests, .ai/decisions/adr.md amendments, handovers, CONTRACTS, backlog, test cases)
- files outside allowed scope: none

---

## Re-check (fix round dc76e62a)

Delta `61a4b0c3..dc76e62a` (architecture + security + code, combined). The `96e1d612` master merge brought only `CatLocalDateEndpointTests.cs` (one test file, +9/-4, pins the clock to a future instant); verified, no production code.

VERDICT: PASS (no BLOCKER or HIGH). Closes ORD 52-56. New LOW rows ORD 59-61 in docs/backlog/ORD.md.

EXTERNAL REACH (the specific concern)
- `RecordPaymentCommand.ApprovalWaiverReason`, `IssueInvoiceFromChargesCommand.PinnedTaxClassId/PinnedTaxRateAsOf`: set only in `OrderBillingMoney.RecordPaymentAsync` / `OrderBilling.IssueInvoiceAsync` (Ordering, in-process). `StaffPaymentsController` and `StaffInvoicesController.IssueFromCharges` build the commands field by field from request DTOs that have no such members, so model binding cannot reach them. No message handler or other producer exists. No external caller can pin a tax rate or waive approval.
- Pin values are not caller-chosen: class is the DELIVERY class resolved server-side, as-of is `order.PlacedOn` (persisted).

CHECKED, NO FINDING
- ORD 52/53: waiver stored on `billing.payment.approval_waiver_reason`, `PaymentApprovalWaivedEvent` raised, audit row `PaymentApprovalWaived` carries actor/correlation (event UserContext), Reason, Method and Amount; handler registered in BillingModule. `PaymentAggregate.Record` enforces `PaymentMethodRules.CanWaiveApproval` (CodCollection<->CashOnDelivery, CounterTender<->CashInStore/CardInStore) before any state change; BankTransfer or an unknown reason gives `ApprovalWaiverNotAllowed`. A waived payment skips the threshold only in the aggregate; the `decimal.MaxValue` trick is gone. `RecordDelivery` passes CodCollection when a COD collection exists, else CounterTender with a Cash/Card in-store method: pairs correctly.
- ORD 54: `TaxClassAggregate.Update` refuses deactivating DELIVERY (no delete exists); reader has no fallback; `CheckoutPricing` blocks with `DeliveryTaxNotConfigured` and placement maps it to 400 (PropertyFor/MessageFor); health check is read-only through the published `IOrderBillingReader`, returns Degraded (HTTP 200) with no data in the description.
- ORD 56 equality: stored `DeliveryTaxAmount` = `MinorUnits.Percent(charge, rate)` at quote instant `now`; PlacedOn = the same `now` (one `GetUtcNow()` in PlaceOrderCommandHandler); invoice delivery line tax = `Round(net * rate / 100)` on a single undiscounted Fee line, half-away-from-zero both sides. Tax rates are stored at 2 dp (`TaxRateAggregate`), so `PercentToHundredths` never throws and both paths use the identical rate. Rate effective exactly at PlacedOn: both reader and pricing use `EffectiveFrom <= asOf` (inclusive), so the same row is chosen. Quote, order and invoice agree for delivery tax. Line tax at confirmation is deliberately unchanged (ADR-217, documented).
- ORD 55: bound (0 <= tax <= charge, so 0 without a charge) in `OrderAggregate.Place`, with an aggregate test.
- Migration `20261010114755_AddOrderDeliveryTaxAndPaymentWaiver`: two AddColumn (numeric(12,2) NOT NULL default 0; varchar(30) NULL), matching the snapshot and configurations (`HasMoney`, MaxLength 30); Down drops both; no data loss, no table rewrite concerns. Only this branch touches the model; the earlier `SeedDeliveryTaxClass` is data-only.
- Module boundaries: Ordering reaches Billing only via `IssueInvoiceFromChargesCommand`, `RecordPaymentCommand` and `IOrderBillingReader`; both commands' additive fields are recorded in CONTRACTS #29 and ADR-217/204 amendments. No superseded-architecture constructs.
- Tests: aggregate tests (waiver pairing, DELIVERY deactivation, delivery-tax bound, CheckoutPricing block) and endpoint tests (stored delivery tax, held transfer after rate change, DELIVERY not deactivatable) are meaningful; see LOW 60 for the one weak spot. Tests were not re-run by this review.

NEW FINDINGS (all LOW, backlog ORD 59-61): bare indexer on the pinned rate lookup; rate-change test uses a 0 % starting rate so it does not exercise rounding/equality; migration default 0 for pre-existing orders (dev data only).

SCOPE CHECK
- files outside allowed scope: none (src, tests, ADR amendment, CONTRACTS/handover rows, backlog, test cases).
