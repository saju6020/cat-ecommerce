# HO-ORD-001 — Cart, Checkout and Orders (backend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-ORD-001** |
| Title | Cart, Checkout and Orders — backend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §10 — **US-ORD-01 … US-ORD-20** (US-ORD-17 deferred, §4.2) |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **FR-ORD-1 … FR-ORD-22**; FR-SHP-11 (cat on the order line), FR-IMP-7, FR-IMP-8, FR-IMP-9, FR-IMP-11 (order side), FR-BIL-5, FR-BIL-7 (inputs), FR-BIL-12, FR-NTF-3 (order messages), FR-ADM-4 (delivery zones and charges), **C-5, C-6**, A-2, A-3, A-4, D-4, BR-2, BR-3, BR-4, BR-10, BR-14, BR-15, BR-16, BR-17, COM-3, NFR-SEC-6, NFR-PRF-5, UI-5, AC-6, AC-7, AC-8, AC-8b, AC-9, Appendix C (order states), Appendix D ("Orders & fulfilment", "Invoices & payments", "Refunds & credit notes") |
| Pairs with | [`HO-ORD-002`](HO-ORD-002.md) — the frontend. **§12.4 of this document is its contract** |
| Builds on | [`HO-ACC-001`](HO-ACC-001.md) (customers, sessions, address book, `CustomerMergedEvent`, anonymisation, rate limiter, `ActionRoles`), [`HO-ADM-001`](HO-ADM-001.md) (branches, tax rates, settings, money, `[StorefrontEndpoint]`, policy acceptance), [`HO-NTF-001`](HO-NTF-001.md) (outbox, templates, link builder), [`HO-CAT-001`](HO-CAT-001.md) (cats). **Consumes assumed contracts** of `HO-SHP-001`, `HO-IMP-001`, `HO-PHM-001` and `HO-BIL-001`, none of which exists at the time of writing (§5, §8.14) |
| Built on by | `HO-SUB-001` (subscription orders reuse `OrderPlacement`, §4.2), `HO-POR-001` (portal order list, re-order), `HO-BRD-001` (combined invoice, with HO-BIL) |
| Delivery order | Section 12 of 17 in [`README.md`](README.md#delivery-order) |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — README C-2, C-3, C-4.** §8.9/§12.5 reconciled against HO-IMP-001 as written: `IStockAvailability` becomes the view `inventory.v_sellable_stock` (ADR-116) and `IStockReservationService` becomes `IStockLedger`, whose eight assumed methods map onto `ReserveAsync` / `ReleaseAsync` / `ConsumeReservationAsync` / `DeductAsync` / `ReceiveReturnAsync`. **`OrderBatchConsumptionQuery` is deleted** — HO-IMP-001 ADR-104 traces recalls from the party stamp instead, so this slice's obligation is to stamp every movement (U-D11). The diet port is renamed to HO-PHM-001's `IDietAuthorisationGate`. New OQ-19. |
| Amended (build) | **2026-10-09 — build-time deviations, phase by phase (branch `ho/ORD-001`; the integrator regenerates the migrations at the merge).** **(1) The Billing seam (§8.14, §20.2) — ADR-215.** HO-BIL-001 as merged does **not** publish the enlisting `IInvoiceIssuer`, `IPaymentRecorder`, `ICreditNoteIssuer` or `IStoreCreditLedger` that §8.14 assumed (executable record: `OrderingStockContractEndpointTests.V3_BillingPublishesNoEnlistingInvoicePaymentOrCreditNotePort`). It publishes (§8.13) the enlisting **charge** recorder `IBillableChargeRecorder` and three **commands that save on their own**: `IssueInvoiceFromChargesCommand`, `RecordPaymentCommand`, `IssueCreditNoteCommand` (+ `IssueRefundCommand`). Ordering therefore records the order's lines (and its delivery fee) as charges in the placement's own save and issues the invoice with the published command, **and runs the whole unit of work in one database transaction** (`ITransactionalScope`, Infrastructure `TransactionalScope`; precedent `ICustomerHoldSerializer`) so US-ORD-08 AC1 stays literally true: if the invoice step (or any later step) fails, the order, its reservation, its charges, the discount use, the cart change and the message all roll back (I-D8 proves it with the real seam — a branch with no billing profile). Two consequences, both inside the scope: EF Core's automatic savepoints are **off** inside the scope (Billing's frozen-invoice trigger needs the invoice row written by the top-level transaction, `xmin = pg_current_xact_id()`), so Billing's retry of a document-number collision cannot recover there — the caller takes a transaction advisory lock per branch (`SerialiseAsync`, before the series row is read) so issuers that go through Ordering queue instead of colliding; a collision with a *staff-issued* invoice at the same instant rolls the placement back (a 5xx the customer retries; stock is released). The same pattern serves C11/C12/C14/C18/C25/C26 (payments, credit notes) in phases E to H. `IStoreCreditLedger` is absent, so `StoreCredit` refunds stay `RefundMethodUnavailable` (OQ-8). **(2) The delivery fee is invoiced under a zero-rate tax class (OQ-6).** A Billing charge always carries a tax class; the delivery charge is untaxed, so `IOrderBillingReader` finds an active class whose rate in force is 0 %; with a delivery charge and no such class placement fails closed (`BillingSeamException`, 5xx, nothing written). **A 0 % tax class must exist before delivery orders can be placed — owner question.** **(3) Ports Ordering owns because the owning area published projections, not ports** (the HO-IMP-001 `ISkuDirectory` precedent): `ICatalogueLookup` (over Shop's `SkuRows`/`PriceRows`/bundle rows; Shop holds **no shipping weight**, so `ShippingWeightGrams` is always null and the weight band surcharge never applies — OQ below), and the batch-status check for BR-14 reads Inventory's `Batches` (no `IBatchStatusQuery` exists). **(4) Diet gate** stays on HO-PHM-001's `IDietAuthorisationGate` deny-all fake until that area merges: prescription-line integration rows (I-C4 authorised path, I-D5, US-SHP-10 AC3) are `Skip = "needs HO-PHM-001 merged"`. **(5) Phase-D placement answers `201` with the real result and the `Idempotency-Key` is required**; on the placement route a `DbUpdateConcurrencyException` is `409 CheckoutQuoteChanged` (`[ConflictAsQuoteChanged]` metadata read by `ConcurrencyExceptionFilter`); a commit-time stock race (`ck_stock_position_balance`, `tg_stock_batch_expired`, `tg_stock_batch_not_usable`, `23514`) is translated to `409 StockNoLongerAvailable` by the scope. Extra files outside §13's literal list: `Domain/DomainServices/{ITransactionalScope,IOrderBillingReader}.cs`, `Infrastructure/Ordering/{TransactionalScope,OrderBillingReader}.cs`, `Api/Filters/ConflictAsQuoteChangedAttribute.cs`, `Application/CommandHandlers/Order/{OrderBilling,OrderStock,OrderNotifications,OrderPlacement}.cs`.**(6) Phases E to H (2026-10-09).** E: staff commands carry the staff member's active `BranchId` (an order of another branch is `404`); `OrderWorkCycle.RunAsync(ct, onlyBranchId)` exists so tests restrict expiry to their branch; `IOrderFinder` / `IReturnFinder` ports; `CartAggregate.AbsorbDuplicateCustomerCart` (customer merge, `MergeFrom` is guest-only); the held-payment approval records the Billing payment under the first person then approves it as the second; reconciliation issues keep the exception type only; `OrderFreeText` refuses card-like text (C-5). F: courier webhook inbox is `INSERT ... ON CONFLICT` (`IWebhookInbox`); `OrderAggregate.ReallocateLine`. G: `OrderRefundMethod` (name clash with Billing's `RefundMethod`), `IOrderingStaging` stages the one new root (the DeliveryFailure return) in C19's save, Inventory has no restock refusal or quality-defect flag so Ordering applies BR-3 itself (an expired batch must be written off; a recalled batch makes opened food eligible outright), refund validator is `IssueOrderRefundCommandValidator`, refunds-due rows carry `Reasons`, store credit stays `RefundMethodUnavailable`. H: `CheckoutPricing` treats `FulfilmentMethod.Counter` as paid by tenders (no online payment option, no policy gate); `ICheckoutInputsLoader.LoadCounterAsync`; `OrderStock.DeductAsync`; `TenderCalculator` and `ReceiptTextRenderer` (pure, 48 columns); the cash tender amount is the money handed over (no separate `CashTendered`); `sales_order.change_given` (migration `AddOrderingCounterChange`); the anonymous walk-in is invoiced to a shared account (**ADR-216, owner decision**); `CompleteCounterSale(changeGiven, ...)`. **Migrations** `AddOrderingReconciliationIssues`, `AddOrderingReturnsAndRefunds`, `AddOrderingCounterChange` were applied by hand to the per-branch DB `catecommerce_ord` (the integrator regenerates them at merge). **Skipped on purpose:** I-D5 / I-D5b and the authorised-prescription rows (`needs HO-PHM-001 merged`); I-D15 maintenance mode is a reflection test only (flipping the global setting is unsafe in the shared test database); cart-abandonment reminders are not built. **Owner questions:** a 0 % tax class must exist for delivery orders; a COD or counter payment above Billing's approval threshold stays `Pending` in Billing; Shop holds no shipping weight so the weight-band surcharge never applies; the shared walk-in customer and its reserved mobile (ADR-216). **Superseded in part by the gate-fix rows that follow (2026-10-09, after the ARCHITECTURE gate failed this slice on 1 BLOCKER and 4 HIGH): item (1) -- the advisory lock and the Ordering-owned transaction (gate fix 5, ADR-215 accepted); item (2) -- a missing 0 % tax class is now a clean 400 (fix 7); item (3) -- BR-14 no longer reads Inventory's `Batches`, `IStockPositionReader` or `IInventoryClock` (fix 2); item (6) F/G -- the reallocation picks no locations, C24 restock/write-off and Ordering's local BR-3 are REMOVED (fix 3), the held-payment approval no longer records the payment under a synthesised identity and Ordering no longer reads Billing's aggregates or `IBillingFinder` (fix 4); item (6) H -- the shared walk-in account (ADR-216) is replaced by a customer found or created by mobile (fix 6). The owner questions above are answered by those rows.**  *(append further rows here)* |
| Amended (gate fix 1) | **2026-10-09 -- ARCHITECTURE gate BLOCKER, owner decision 1.** `.ai/flows/order-fulfilment-flow.md` is reverted to its master content: §14 forbids `.ai/**` edits other than `.ai/decisions/adr.md`. The coordinator updates the flow at or after the merge. |
| Amended (gate fix 2) | **2026-10-09 -- Inventory internals out of Ordering, owner decision 2 (the route chosen for Pharmacy too).** Ordering no longer uses `IStockPositionReader` or `IInventoryClock` anywhere (`OrderStockFulfilment`, `OrderFulfilmentCommandHandlers`, `ReturnCommandHandlers`). It uses published `IStockLedger` members only, and **never picks a batch or a location itself**: (a) **BR-14** at dispatch and ready-for-collection asks the ledger for each allocated batch's usability (`GetBatchUsabilityAsync` -> `BatchRecalled` / `BatchNotUsable` / `BatchExpired`, in the branch's local date), and `ConsumeReservationAsync` still refuses a recalled or expired reserved batch (rule 7); (b) **BR-10** (an opened food is eligible outright when an allocated batch is recalled) reads the same verdict; (c) **reallocation / FEFO override** (US-IMP-07 AC2) releases the line's reservation and calls the new `ReserveBatchAsync(StockBatchDemand(branch, sku, quantity, targetBatchId, reason))` -- the staff-chosen batch and the reason are all Ordering passes; Inventory picks the locations, refuses an unknown / other-branch / recalled / expired / short batch and audits the override; a bundle's other components are re-reserved FEFO. Two members are **new on the published port** (additive, no rename; HO-PHM-001's own dispensary members are separate and merge beside them): see HO-IMP-001's Amended (HO-ORD-001 gate fix) row and `CONTRACTS.md` rows 1, 2. This corrects item (3) of the Amended (build) row, which said BR-14 reads Inventory's `Batches`. Guarded by `OrderingPublishedPortsOnlyTests`. |
| Amended (gate fix 3) | **2026-10-09 -- quarantine disposition is not Ordering's, owner decision 3 (closed OQ-19).** C24 (`DecideReturnedStockCommand`, `POST /api/staff/returns/{id}/decisions`, `Returns.DecideStock`) is **deleted** with its handler, validator, request DTOs, `ReturnedStockDecidedEvent` and its audit subscription, `ReturnStockDecision`, `StockDecisionReasonRequired`, the `ReturnLine` decision fields and the four `return_line` columns (`stock_decision`, `decision_reason`, `decided_by`, `decided_on`; migration `OrderingGateFixes`, regenerated by the integrator). That includes Ordering's calls to Inventory's `PostStockTransferCommand` / `RequestStockAdjustmentCommand` and the local BR-3 re-implementation. **Ordering's responsibility ends at `ReceiveReturnAsync`** (C23): returned goods sit in the branch's quarantine location; restock or write-off is Inventory's quarantine workflow. The return stays `Received` (the `Closed` status is no longer reached by Ordering; refund eligibility already reads `Received` or `Closed`). The staff return detail no longer carries `stockDecision` / `decisionReason`. Tests I-G5 and I-G6 are removed (their rows in §15 and acceptance A34 are struck through above); I-F9 now ends at the receipt. This is also noted in the Amended (build) row, item G. |
| Amended (gate fix 4) | **2026-10-09 -- Billing internals out of Ordering, owner decision 4.** Ordering no longer injects `IAggregateRootRepository<InvoiceAggregate>`, `IAggregateRootRepository<PaymentAggregate>` or `IBillingFinder`, and no longer builds a `UserContext`. (a) **Refund planning** (C25) uses Billing's new read-only port `IBillingRefundPlanner` (`PlanCreditAsync`: the per-line credit or a full credit; `PlanRefundAsync`: the draws from the order's payments, newest first) and then dispatches Billing's published `IssueCreditNoteCommand` / `IssueRefundCommand`. (b) **Held transfers (ADR-204, BR-17) are redone around Billing's own maker-checker.** At or below `billing.paymentApprovalThreshold`, C11 is as before (charges, invoice, payment, confirmation, one transaction). **Above it, C11 now records the payment in Billing at once, as the person who entered it** (invoice issued with the charges; the Billing payment stays `PendingApproval`, the invoice unpaid) and the order keeps the hold, now naming the payment (`HeldPaymentConfirmation.PaymentId`, column `held_payment_id`). **C12 approve** is a *different, real* person calling Billing's published `ApprovePaymentCommand` with their own identity (Billing refuses the recorder), then the order is confirmed in the same transaction; **C12 reject** calls Billing's published `RejectPaymentCommand` (reason = the note) and clears the hold -- the invoice stays on the order and the next confirmation reuses it. The code that copied the approver's roles onto `held.RecordedBy` is deleted. **`Orders.ApprovePayment` is now Accountant and Administrator** (= Billing's `Payments.Approve`; ShopManager no longer approves; reject uses the same action). A recorder cannot reject their own hold (Billing's rule). A hold made before this change (no `PaymentId`) must be rejected and entered again. A cancellation while held leaves Billing's pending payment for an accountant to approve (money really arrived: it becomes unallocated credit) or reject. HO-BIL-001 §8.13 and `CONTRACTS.md` rows 27, 28 publish what is used. Open (backlog): a payment approved directly on Billing's list leaves the order's hold until the order's approve route is used. |
| Amended (gate fix 5) | **2026-10-09 -- ADR-215 accepted by the owner ("keep one atomic transaction, Billing publishes it"), decision 5.** Billing now publishes the entry point: `IBillingTransactionScope` (HO-BIL-001 §8.13 Amended). It owns the transaction, the savepoint rule and **the per-branch numbering lock** (`DocumentNumbering` reads the series row `FOR UPDATE` inside an open transaction -- ADR-179 amended in `.ai/decisions/adr.md`). Ordering's `ITransactionalScope` is that scope plus the one thing that is Ordering's own, the commit-time stock-race translation to `StockNoLongerAvailable`; `SerialiseAsync`, `OrderBilling.NumberingLock` and `SerialiseCreditNotesAsync` are deleted -- Ordering names no lock and reads no trigger internals. This replaces item (1) of the Amended (build) row (the advisory lock `billing-numbering:{branch}`). HO-BRD-001 and HO-SUB-001 reuse `IBillingTransactionScope`. Proved by `OrderingBillingNumberingEndpointTests` (scoped counter sales and staff-issued invoices racing at one branch from a cold series: distinct, contiguous numbers). ADR-215 status: **Accepted by the owner on 2026-10-09**. |
| Amended (gate fix 6) | **2026-10-09 -- ADR-216 rewritten, owner decision 6: the counter sale's buyer is a real customer; the shared "Walk-in customer" account is gone.** A counter sale records the customer's **name and mobile number, both required** (`customerName`, `customerMobile` on `POST /api/staff/pos/sales`; `customerId` is removed from the sale, it stays on the quote). The handler normalises the mobile, finds an **Active customer with that primary number** through Accounts' published `FindCustomersQuery` (adapter `AccountsCounterCustomerFinder` in the Api project: Application does not reference Read), and otherwise creates a **passwordless customer through the existing `RegisterWalkInCustomerCommand`** (HO-ACC-001 §8.8, ADR-038: `RegistrationChannel = Reception`, Incomplete until a password is set) **inside the sale's transaction**, after every check has passed (a refused sale leaves no account). The order and the invoice carry that `CustomerId`. If the person later registers online with the same mobile, HO-ACC-001 answers `MobileNumberAlreadyRegistered` with the reset offer, which completes the same account -- proved by `TheWalkInCustomer_WhoLaterRegistersOnline_WithTheSameMobile_...`. Removed: `WalkInCustomer` (the fixed id, "Walk-in customer", the reserved mobile `01000000000`) and everything that seeded or special-cased it. Closes SECURITY backlog MEDIUM 1 (ORD 15) and LOW 7 (ORD 21), and ARCHITECTURE M1 (ORD 22). Note for the FE: a prescription food in a counter sale needs an existing customer who owns the cat. ADR-216 status: **Accepted by the owner on 2026-10-09**. |
| Amended (gate fix 7) | **2026-10-09 -- delivery charges by weight range, owner decision 7.** The zone service weight bands (`UpToGrams` + `Surcharge`, open-ended last band) were already editable in the staff zone configuration; they are now **applied**: `CheckoutPricing` sums `quantity x CatalogueSku.ShippingWeightGrams` over the priced lines and passes the total to `DeliveryChargeCalculator` (a SKU without a weight counts as 0 g; `CheckoutInputsLoader` logs that once per process, with no data). `ICatalogueLookup` still passes `null` for the weight: **Shop's `ShippingWeightGrams` is not on master yet** (HO-SHP-001-FU3, a parallel branch; Shop is not edited here) -- **TODO(owner decision 7): map the field in `CatalogueLookup.SnapshotAsync` once `SkuRow` carries it**; everything else already works the moment it is populated (unit tests `TheDeliveryCharge_AddsTheWeightBandSurcharge_...`). **No 0 % tax class (OQ-6): chosen = a clean 4xx configuration error, not a seed** -- placement and transfer confirmation with a delivery charge now answer `400 DeliveryTaxClassNotConfigured` ("Delivery orders cannot be taken right now ...") instead of a 5xx, with nothing written; a 0 % class is still needed before delivery orders can be taken (an Admin creates it in the tax configuration). Seeding another area's reference data from Ordering was rejected. |
| Amended (merge of master) | **2026-10-10 -- PHM-001 and SHP-001-FU3 merged into this branch.** (1) `CatalogueLookup` now maps Shop's published `SkuRow.ShippingWeightGrams` (CONTRACTS #25) into the SKU snapshot, so the delivery weight bands add a surcharge; a SKU with no weight still counts as 0 g. (2) Pharmacy also declares a `CatalogueSku`; Ordering's record is renamed **`OrderCatalogueSku`** (`Models/OrderCatalogueSku.cs`). (3) This handover's published contracts were renumbered in `CONTRACTS.md`: `IBillingTransactionScope`, `IBillingRefundPlanner` and the payment approve/reject commands are now **#26, #27, #28** (PHM/SHP took #24, #25). (4) `IStockLedger` carries both HO-ORD-001's and HO-PHM-001's additive members. (5) The diet-gate integration tests (I-C4, I-D5, I-D5b) run against the real PHM gate. (6) A cart whose only line is a prescription line without a cat is refused as `PrescriptionCatRequired` (was `NothingToPlace`). |
| Amended (gate fix 8) | **2026-10-09 -- reconciliation issues are branch-scoped, SECURITY MEDIUM 2, owner decision 8.** `ordering.reconciliation_issue.branch_id` (nullable; migration backfills it from the order, a counter-sale issue carries the till's branch from now on) is stored on every new issue (`ReconciliationIssueDto.BranchId`). `GET /api/staff/orders/reconciliation-issues` lists only the caller's **active branch**'s issues and `POST .../{id}/resolve` answers **404** for another branch's issue (the same as one that does not exist); both resolve the branch like the other staff order endpoints (`X-Branch-Id`, `403` when refused). A row raised before the column existed and not backfilled (no order) is listed to no branch. Test `AReconciliationIssue_IsListedAndResolvedOnlyByItsOwnBranch`. |
| Amended (gate fix 9) | **2026-10-09 -- cheap MEDIUMs fixed, owner decision 9 (marked Resolved in `docs/backlog/ORD.md`).** (a) `ix_sales_order_customer_idempotency` -> `409 DuplicateSubmission` in `UniqueConstraintExceptionFilter` (ORD 25; `OrderingUniqueConstraintMappingTests`). (b) A benign `DbUpdateConcurrencyException` (two staff, or the worker) or a cancelled request no longer opens a reconciliation issue in C11, C12, C25, C26 or the delivery payment; the conflict answers `409` (`ConcurrencyExceptionFilter` now also recognises it inside a nested dispatch) (ORD 2; `ABenignConcurrencyConflict_OpensNoReconciliationIssue_AndAnswers409`). (c) The 23514 -> `StockNoLongerAvailable` translation has a deterministic test with no database (ORD 1; `TransactionalScopeTests`). (d) The HTTP-level `BankTransferDisabled` rule is proved through the branch's own `orders.bankTransferEnabled` override, not the global bank-account row, which parallel classes switch on (ORD 6; `BankTransfer_TurnedOffForTheBranch_...`); the account half (`BankAccountActive`) stays covered by U-B5. Other backlog rows stay open. |
| Amended (gate fix 10) | **2026-10-09 -- cart-abandonment reminders, owner decision 10:** later. A backlog row records it (`docs/backlog/ORD.md`). Nothing is built. |
| Amended (TEST fix round) | **2026-10-10 -- owner confirmed on 2026-10-10 that the ORD branch may make its additive edits to Inventory's `IStockLedger` and to the Billing files, including this fix round's Billing change** (`DocumentNumbering`: the unscoped issuer takes the series row lock first, ADR-179 amended again). Also: the counter sale's buyer is found or registered in its own short transaction before the sale scope opens (`ICounterBuyerRegistrar`, ADR-216 amended; ORD 42); a mobile held by a Merged / Anonymised account answers `400 AccountNotActive` with a cashier-facing message and opens no reconciliation issue (ORD 43; owner question: follow a merge to the surviving account instead?); `ResolveReconciliationIssueCommandValidator` requires `BranchId` (ORD 39). |
| Amended (owner decisions 2026-10-10, FU1) | **2026-10-10 -- HO-ORD-001-FU1 (branch `ho/ORD-001-FU1`), the owner's answers to the open questions.** **D1 -- a Merged mobile follows the merge.** A counter sale to a mobile held by a Merged account sells to the **surviving account** (chains followed, at most 10 hops, a loop is refused); only when the survivor is itself not Active (e.g. Anonymised) or no survivor is recorded does the cashier-facing `400 AccountNotActive` stay. `AccountsCounterCustomerFinder` follows `MergedIntoCustomerId` through Accounts' **already published** `FindCustomersQuery` and `GetStaffCustomerQuery` (Q-H2, Q-H1) -- no new published member. ORD 43 follow-up closed. **D2 -- the walk-in buyer is always stored.** `CompleteCounterSaleCommandHandler` now finds or registers the buyer (`ICounterBuyerRegistrar`, its own short transaction) right after the branch and the mobile shape are validated and **before every business check** (prescription, stock, tenders, card-number text), so a refused sale still leaves the passwordless walk-in; the retry reuses it. A later online registration with that mobile gets Accounts' `MobileNumberAlreadyRegistered` + reset offer, which completes the same account (proved end to end: refused sale, registration refused as duplicate, reset, sign-in, one account). The registrar now detaches only the entries its own failed attempt added (ORD 47). **D3 -- the delivery tax is configured, default 0 %.** `DeliveryTaxClassNotConfigured` (code and message) is **removed**. The delivery fee is invoiced under the tax class with code `DELIVERY`, seeded at 0 % by the data-only migration `SeedDeliveryTaxClass` (no schema change); an Administrator changes the delivery tax with the **existing** `POST /api/admin/tax-classes/{id}/rates` (`TaxClasses.Manage`, effective-dated, no back-dating, ADR-049) -- no new setting, endpoint or permission. `IOrderBillingReader.GetDeliveryTaxAsync` returns the class and the rate in force (falling back to any active 0 % class when `DELIVERY` is missing; with none at all a delivery order is a configuration fault, 5xx, nothing written -- `BillingSeamException`). `CheckoutInputs.DeliveryTax` carries it into `CheckoutPricing`, which adds `round(delivery x rate)` to `Totals.TaxTotal` and `GrandTotal` (the quote and its fingerprint show it; a changed rate makes a pending quote stale); `PlaceOrderDto.DeliveryTaxAmount` lets the aggregate's re-sum accept the extra tax (not stored: the invoice's delivery line carries it). Placement and transfer confirmation apply the rate in force at that moment (a held transfer confirmed after a rate change is invoiced at the new rate -- same rule as line tax, ADR-049). OQ-6 answer amended (ADR-217). **D4 -- COD and counter payments need no second approver.** `RecordPaymentCommand.ApprovalNotRequired` (additive, in-process only, never bound from HTTP -- `CONTRACTS.md` #29) makes Billing accept the payment at once whatever its size; Ordering sets it for cash-on-delivery collections and counter tenders. Bank-transfer holds (ADR-204, `Orders.ApprovePayment`) are **unchanged** (the handover ties only COD and the till to OQ-7); staff-recorded payments in Billing keep the threshold. OQ-7 answer is now literally true. **BR-3 open question closed as moot:** Ordering's local BR-3 went with restock / write-off (gate fix 3); Inventory enforces expiry and quarantine. **Tests replaced:** `ARefusedSale_LeavesNoWalkInAccountBehind` (now `ARefusedSale_StillLeavesTheWalkInAccountBehind_AndNothingIsSold`), the unit `ADeliveryCharge_WithNoZeroRateTaxClass_IsABusinessRefusal...` (now `..._IsAConfigurationFault_NotABusinessRefusal`); `T2` kept (a merge with no recorded survivor). See `docs/test-cases/TC-HO-ORD-001.md` section FU1. | **FU1 fix round (2026-10-10, migration `AddOrderDeliveryTaxAndPaymentWaiver`; backlog ORD 52-56).** (1) A waived approval is recorded: `RecordPaymentCommand.ApprovalWaiverReason` (`CodCollection` / `CounterTender`) replaces the bool; Billing stores it on the payment and audits `PaymentApprovalWaived`; Billing refuses the waiver for any other method (`ApprovalWaiverNotAllowed`). (2) The `DELIVERY` tax class **cannot be deactivated** (`400 DeliveryTaxClassRequired`; tax classes cannot be deleted) and the "any active 0 % class" fallback is gone: delivery is always invoiced under `DELIVERY`. If it is nonetheless missing, inactive or rate-less, a delivery **quote** (and so placement) is blocked with `DeliveryTaxNotConfigured` (`canPlace=false`; placement answers `400` with the same code) instead of a 5xx after a valid quote; counter sales have no delivery charge and are unaffected; `/health` reports `delivery-tax-class` Degraded with a log warning. (3) The delivery tax is **stored on the order** at placement (`sales_order.delivery_tax_amount`, bounded `0 <= tax <= delivery charge`, ORD 55); a held transfer confirmed after a rate change is invoiced with the `DELIVERY` rate as of the order's `PlacedOn` (what the customer was quoted). Line tax is unchanged: still priced by Billing at issue (ADR-217, owner question).
| Amended (HO-POR-001) | **2026-10-10 -- owner-delegated decision 2026-10-10, owner to review.** `CustomerOrderSummaryViewModel` (`GET /api/customers/me/orders`, §12.4) gains trailing `TrackingNumber` (`string?`, the shipment's number; null until recorded) and `CanReorder` (`bool`). `CanReorder` is computed by the new `CustomerOrderRules.CanReorder(OrderStatus)` (Cancelled, Delivered, Returned, Refunded), which the order detail now calls too -- one source of truth. Additive only: no rename, removal or migration. Tests: `OrderingFulfilmentEndpointTests` (dispatch with tracking) asserts both on the list item and that `canReorder` equals the detail's. CONTRACTS row 31. HO-ORD-002 may use the list `canReorder` / `trackingNumber`. |
| Platform | `Platform.Infrastructure.Core` **1.0.34** — do not downgrade |

---

## 2. Objective

Let a cat owner **put products in a cart, check out and receive them**, and let the shop **take the
money, pack, dispatch, take returns and refund** — all without an online payment gateway.

**There is no payment gateway in v1** (SRS §1.2, A-2, Phase 5). Money arrives in exactly four ways:
**cash on delivery**, **bank transfer confirmed by staff**, **cash or card at the counter when a pickup
order is collected**, and **cash or card at the point of sale**. An in-store card payment is recorded as
a method, an amount and a terminal slip reference — **no card number and no payment token enters the
system** (C-5). Every payment is recorded by a named person (BR-17).

Five things this slice is careful about:

1. **Nothing is sold twice.** Stock is reserved by batch, FEFO, at the moment an order is placed, by
   Inventory's reservation contract. Two customers checking out the last unit get exactly one order
   (US-IMP-08 AC2). Ordering never picks a batch itself and never writes `inventory.*` (ADR-208).
2. **Placing an order is one unit of work.** The order, its stock reservation, the discount use, the
   invoice (when it is confirmed) and the confirmation message commit in **one** `SaveChangesAsync`
   through *enlisting* ports — the same technique HO-NTF-001's outbox uses (F23) — so US-ORD-08's "all
   atomically" is literally true (ADR-201).
3. **The customer sees what they pay.** Checkout re-prices the cart, lists every change since the items
   were added, and refuses to place an order whose quote the customer has not seen — by fingerprint,
   not by trust (ADR-200). Money is computed in integer minor units (US-ORD-04 AC2, ADR-198).
4. **One submit, one order.** Double clicks and repeated posts carry an idempotency key and create one
   order (FR-ORD-20, ADR-202). Courier webhooks are processed once by event id (COM-3, ADR-209).
5. **Unconfirmed money never holds stock for ever.** Bank-transfer orders expire after the configured
   window (default 48 h), release their stock and tell the customer (BR-16, ADR-203).

§17 builds the slice in **eight phases**; each compiles, migrates and passes its own tests first.

---

## 3. In scope

### 3.1 Stories

| Story | Pri | Pts | Capability delivered here | Phase |
|---|---|---|---|---|
| US-ORD-01 | M | 5 | Server-side cart for guests (opaque HttpOnly cookie) and customers; one active cart per customer across devices; guest cart merged into the account cart without duplicate lines | C |
| US-ORD-02 | M | 5 | Checkout quote re-prices and re-checks availability and authorisation, lists every change, and placement is refused unless the customer acknowledged that exact quote | D |
| US-ORD-03 | M | 8 | Delivery to a saved address or pickup at a branch; delivery slot where the service level offers slots; prescription lines need their cat before checkout proceeds | D |
| US-ORD-04 | M | 5 | Line subtotals, line discounts, order discount, delivery charge and tax each shown before the total; every calculation in integer minor units | D |
| US-ORD-05 | S | 8 | Discount codes: percentage or fixed, minimum spend, product/category scope, first-order-only, overall and per-customer limits, validity window; rejection with the specific reason | B, D |
| US-ORD-06 | M | 8 | Delivery zones with coverage, service levels (standard/express), weight bands and free-over thresholds; charge shown before payment; outside every zone → delivery unavailable and pickup offered | B, D |
| US-ORD-07 | M | 8 | COD, bank transfer and pay-on-collection offered by configuration; COD hidden with its reason when disabled for the zone or above the ceiling; no card data or token anywhere | D |
| US-ORD-19 | M | 8 | Bank-transfer order held in `pending_payment` with stock reserved and bank details shown; staff verification → `confirmed` and a message; expiry after the window with stock released, a message and re-order | E |
| US-ORD-20 | M | 8 | COD confirmed without prepayment; cash collected, collector and date recorded at delivery; refused/undelivered parcel through the returns path into quarantine | D, F |
| US-ORD-08 | M | 8 | Confirmation = order + FEFO reservation by batch + confirmation message + invoice, atomically; a technical failure after staff entered a payment becomes a reconciliation issue, never lost | D, E |
| US-ORD-09 | M | 5 | Appendix C status machine; invalid transitions refused; every transition timestamped and attributed | F |
| US-ORD-10 | M | 5 | Pick list grouped by location and batch; packing slip per order | F |
| US-ORD-11 | M | 8 | Dispatch with courier and tracking number shown to the customer; CSV manifest; manual tracking entry. **Courier API adapter is a named gap (SRS Q-4)** — the extension point is delivered | F |
| US-ORD-12 | M | 5 | Customer cancels before packing: stock released, invoice credited, refund obligation raised where money was received; after packing → refused with "contact the shop" | E |
| US-ORD-13 | M | 8 | Return requests within the window, with reason and condition, to staff approval; opened food and used litter refused unless a quality defect or recall is recorded (BR-10) | G |
| US-ORD-14 | M | 5 | Returned stock received into quarantine; restock or write-off is not this slice: it is Inventory's quarantine workflow (Amended gate fix 3) | G |
| US-ORD-15 | M | 5 | Refunds as cash in store, bank transfer or store credit by payment method; method, amount, reference, staff and reason recorded; credit note linked to the invoice; audited | G |
| US-ORD-16 | M | 13 | Point of sale: barcode lookup → SKU and FEFO batch preview; tenders (cash with change, in-store card with slip reference); stock issued through the same ledger; invoice; receipt data and 48-column text | H |
| US-ORD-18 | M | 5 | Idempotency key on placement and every money-recording endpoint; courier webhooks processed once by event id | A, D, F |

**Also delivered, from other areas' contracts:**

| Criterion | Owner story | Why here |
|---|---|---|
| Prescription line: cat selected at add-to-cart and recorded on the order line; checkout blocks the line if the authorisation expired meanwhile | US-SHP-10 AC2, AC3 | The cart and the order line are this slice's. **Assumes HO-SHP-001 defers these two criteria here** (OQ-12) |
| Staff override of the FEFO allocation for an order line, reason mandatory, audited | US-IMP-07 AC2 (order half) | The override happens at picking; Inventory executes it (§11.4 C21) |
| Every order, customer and cat that received a batch; unshipped orders cannot be dispatched | US-IMP-11 AC2 (order path), AC4 | **Nothing to implement** (README C-3): this slice stamps `StockParty(CustomerId, CatId)` and `StockReference(Order, orderId, lineId)` on every movement, and HO-IMP-001's trace reads them back. Batch status is still checked at dispatch (BR-14) |
| Policy versions accepted before an order is placed | US-ADM-07 AC2 (checkout half) | HO-ADM-001 §4.2 routes checkout acceptance here — checked, not written (§8.7) |
| Customers track status and get status messages on their preferred channel | FR-ORD-13 | Order reads + §8.12 templates |
| Re-order a previous order | US-POR-02 | Needed now for US-ORD-19 AC3 ("an option to re-order"); HO-POR-001 reuses the endpoint |

### 3.2 Decisions already settled upstream — do not re-litigate

| Decision | Where |
|---|---|
| In-memory CQRS, one host, `IInMemoryDispatcher` only; never `202` | ADR-010, 013, 014, 019, 020 |
| PostgreSQL / EF Core, one `DbContext`, per-call `SaveChangesAsync`, `xmin` → `409` | ADR-011, ADR-016, ADR-021 |
| Idempotency is required at the HTTP edge, not on the aggregate | ADR-014 |
| FEFO allocation is a Domain Service and belongs to Inventory | ADR-015, `.ai/bounded-contexts.md` rule 4, `.ai/flows/order-fulfilment-flow.md` |
| **No payment gateway, no card data, no token** | SRS §1.2, C-5, A-2; owner v1.1 |
| **Identifier + password only; no OTP or verification** before checkout | SRS v2.2, HO-ACC-001 |
| Money is `decimal(12,2)` + ISO code; JSON amount is a string | HO-ADM-001 ADR-054, §12.4 |
| Prices and tax rates read at transaction time and **snapshotted** | HO-ADM-001 ADR-049, §8.6 |
| Delivery zones and charges are configured by this slice | HO-ADM-001 §4.2 |
| Storefront endpoints carry `[StorefrontEndpoint]`; the back office is never gated | HO-ADM-001 ADR-053 |
| Messages only through `INotificationOutbox.Enqueue` in the command handler, before the save; never from an event handler | HO-NTF-001 ADR-056, ADR-057 |
| `CustomerMergedEvent` is binding on every context holding a customer id | HO-ACC-001 ADR-039 |
| Every aggregate change is audited in the same transaction by the interceptor | HO-ACC-001 ADR-040 |
| Staff roles are the eight Appendix D columns | HO-ACC-001 §10.2 `StaffRoles` |

---

## 4. Out of scope

### 4.1 Frontend — all of it

Screens are [`HO-ORD-002`](HO-ORD-002.md). Criteria phrased as UI are met here at the contract level:

| Story AC (UI phrasing) | Server half — delivered here |
|---|---|
| US-ORD-02 "shown explicitly and I must acknowledge it" | The quote's `changes[]` and `fingerprint`; placement with a stale fingerprint → `409 CheckoutQuoteChanged` |
| US-ORD-04 "each shown separately" | The quote and the order carry every component as its own field |
| US-ORD-06 AC2 "checkout explains … and offers pickup" | `delivery.available = false`, `delivery.unavailableReason = AddressOutsideServedZones`, `pickupBranches[]` populated |
| US-ORD-07 AC2 "COD is hidden with the reason available" | `paymentOptions[]` with `available` and `unavailableReason` per method |
| US-ORD-10 "a packing slip prints" | Packing-slip and pick-list reads; the print is the FE's |
| US-ORD-12 AC2 "directed to contact the shop" | `400 CancellationNotAvailable`; branch contact details from `GET /api/branches` |
| US-ORD-16 "a receipt prints" | Receipt read with structured data and a 48-column text rendering; printing is the FE's (HW-1 gap, §4.3) |

### 4.2 Deferred to a named later area — contracts delivered here

| Capability | Story / FR | Where it lands | What this slice delivers |
|---|---|---|---|
| **One invoice combining consultation, shop and boarding charges** | **US-ORD-17**, FR-ORD-19, AC-16 | **HO-BIL-001** (invoice composition) and **HO-BRD-001** (boarding charges) | Every order invoice line is issued with `BusinessLine = Shop`, its tax class, rate and amount (§8.14 `IInvoiceIssuer`), so a combined invoice can carry them. Invoice composition is Billing's aggregate; boarding charges do not exist before HO-BRD (OQ-10) |
| Subscription orders | US-SUB-01, 06 | **HO-SUB-001** | `OrderPlacement` (§11.4) is a static helper over the ports, reusable by a subscription command; `OrderChannel` is append-only so `Subscription` can be added; `ExpireUnpaidOrderCommand` already implements FR-ORD-22 expiry for any transfer order |
| COD, transfer and till reconciliation | US-BIL-06, FR-BIL-7 | **HO-BIL-001** | Every payment is recorded in Billing with method, reference, collector and recorder (§8.9); COD orders carry tracking numbers for remittance matching |
| Daily cash-up | US-BIL-12 | HO-BIL-001 | Counter tenders recorded in Billing with branch and time |
| Portal dashboard of recent orders | US-POR-01 | HO-POR-001 | `GET /api/customers/me/orders` |
| Order fulfilment report, cart abandonment report | SRS §8.1, §8.3 | HO-RPT-001 | The tables |
| Abandoned-cart reminders, back-in-stock alerts | FR-NTF-3 | No §10 story; back-in-stock is HO-SHP (US-SHP-13) | Carts carry `LastModifiedOn`; nothing is sent (OQ-11) |
| Offline front desk | NFR-AVL-3 | HO-XCT-001 | Nothing — orders are not in NFR-AVL-3's list |

### 4.3 Not buildable yet — named gaps

| Gap | Consequence |
|---|---|
| **No courier chosen, no courier API** (SRS Q-4) | Dispatch records courier and tracking manually; CSV manifest export (US-ORD-11 AC2). US-ORD-11 AC1's "shipment is created via courier API" is **not met** until an adapter exists: `ICourierGateway` is declared and ADR-209 fixes how the adapter plugs in (outbox-driven worker pass). Tracking webhooks work for any provider registered in configuration; production has none |
| **COD policy** (SRS Q-3), **transfer window and who confirms** (Q-3b), **in-store card terminal** (Q-3c) | Built on settings with the SRS defaults (§10.2); changing them is data, not code |
| **Currency and tax regime** (SRS Q-5) | Prices are tax-exclusive per FR-ORD-5's formula; the delivery charge carries the configured delivery tax, default 0 % (OQ-6, amended FU1 D3) |
| **No SMS or email provider** (HO-NTF-001 OQ-1) | Order messages are enqueued, rendered and logged; outside Development only the in-app copy is delivered |
| **Direct ESC/POS printing** (HW-1) | The browser prints the receipt layout through the OS printer driver (HO-ORD-002). No server-side printer driver |
| Store credit as refund method | Depends on HO-BIL-001 building US-BIL-08 (Could). Until then `StoreCredit` is refused with `RefundMethodUnavailable` (OQ-8) |

### 4.4 Explicitly forbidden shapes

No `IBusMessageDispatcher`, MassTransit, RabbitMQ, a second host, a `CommandService` layer,
`202 Accepted`, `[Authorize]`. **No payment gateway type, SDK, webhook or field** — no card number, PAN
fragment, expiry, CVV, wallet id or token column anywhere (C-5). **No batch selection in Ordering** —
FEFO is Inventory's; **no write to `inventory.*` or `billing.*` tables** except through their ports.
**No floating-point money** (`double`, `float`) — not even in a test helper. **No enqueue from an event
handler** (HO-NTF-001 ADR-057). **No client-trusted price or total** — every amount is recomputed by
the server. **No stock reservation in the cart.** **No guest checkout** (OQ-1).

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-CAT-001 merged | ✅ at `4007225` |
| **HO-ACC-001, HO-ADM-001, HO-NTF-001 merged** | ❌ not implemented at baseline — §20.1 |
| **HO-SHP-001 merged** — SKU catalogue lookup (§8.14) | ❌ **handover not yet written** — §20.1, §20.2 |
| **HO-IMP-001 merged** — `inventory.v_sellable_stock`, `IStockLedger` (reserve / release / consume / return), batch status | ✅ written 2026-09-22 — contracts reconciled (README C-2, C-3) |
| **HO-PHM-001 merged** — `IDietAuthorisationGate` | ❌ **handover not yet written** — §20.1, §20.2 |
| **HO-BIL-001 merged** — invoice issue, payment record, credit note, approval threshold | ❌ **handover not yet written** — §20.1, §20.2 |
| PostgreSQL 17.5 `catecommerce_dev`, `dotnet-ef` 9.0.0 | ✅ |
| New NuGet packages | **None.** `System.Security.Cryptography` (SHA-256, HMAC, `RandomNumberGenerator`), `System.Text.Json`, the framework rate limiter and `TimeProvider` suffice |

The shared-database rules of HO-CAT-001 §16.1 and HO-ACC-001 §16.1 apply. **Every test creates its
own branch, delivery zone, SKUs and stock** (through the upstream endpoints or test seams) so that
stock, discount-usage and order-number assertions never see another test's rows (§16.1).

---

## 6. Governing architecture documents

Identical to [HO-ACC-001 §6](HO-ACC-001.md#6-governing-architecture-documents) — `.ai/architecture.md`,
`.ai/cqrs.md`, `.ai/bounded-contexts.md`, `.ai/namespace-conventions.md`, `.ai/decisions/adr.md`,
`.ai/command-handler-pattern.md`, `.ai/read-side-pattern.md`, `.ai/persistence-ef-core.md`,
`.ai/domain-events-and-outbox.md`, `.ai/enumeration-pattern.md`, `.ai/di-registration-guide.md`,
`.ai/host-composition.md`, `.ai/feature-creation-checklist.md`. Of note here:

| Document | Why it matters in this slice |
|---|---|
| [`.ai/architecture.md`](../../.ai/architecture.md) §4, §6 | The command path; ports in `Domain.DomainServices` because Application cannot reference Infrastructure |
| [`.ai/cqrs.md`](../../.ai/cqrs.md) §2, §5 | Synchronous commands return their real result — the idempotency filter replays that result (ADR-202) |
| [`.ai/bounded-contexts.md`](../../.ai/bounded-contexts.md) | `Order` context owns `OrderAggregate` and `CartAggregate` (table row "Order"); **rule 1** — placement, confirmation, cancellation, refund and counter sale are recorded exceptions (ADR-201); **rule 3** — returns, refunds and reconciliation issues are their own roots; **rule 4** — FEFO is Inventory's domain service; "a therapeutic diet needs a live authorisation — the order handler checks it" |
| [`.ai/namespace-conventions.md`](../../.ai/namespace-conventions.md) | Feature segment **`Order`** (listed). Do not invent `Cart`, `Checkout` or `Pos` segments — they are all `Order` |
| [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | ADR-009 … ADR-023 binding. ADR-014 (idempotency at the edge), ADR-015 (no service layer), ADR-016 (`xmin`), ADR-021 (per-call save — why enlisting works) |
| [`.ai/command-handler-pattern.md`](../../.ai/command-handler-pattern.md) §5, §6 | "Allocate stock FEFO" is a Domain Service — Inventory's. The rule-placement table decides every row of §11.4 |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) §5 | The pick list, manifest and recall trace are reads across aggregates |
| [`.ai/persistence-ef-core.md`](../../.ai/persistence-ef-core.md) §2, §4, §6, §7, §9 | Owned lines and allocations; `xmin`; the order-number sequence is raw SQL in the migration |
| [`.ai/domain-events-and-outbox.md`](../../.ai/domain-events-and-outbox.md) §3, §4 | Everything leaving the process — messages, the future courier API — goes through an outbox; an event handler never enqueues |
| [`.ai/enumeration-pattern.md`](../../.ai/enumeration-pattern.md) | The enumerations in §10.1 |
| [`.ai/di-registration-guide.md`](../../.ai/di-registration-guide.md) §3, §4 | Validators as `IValidator<T>`; the reconciliation recorder creates its own scope (the one Singleton-to-Scoped exception pattern) |
| [`.ai/host-composition.md`](../../.ai/host-composition.md) §1, §3, §5 | `Program.cs` additions; `OrderWorker` as a hosted service |
| [`.ai/flows/order-fulfilment-flow.md`](../../.ai/flows/order-fulfilment-flow.md) | The flow this slice implements; its "Remaining work" list is closed here except courier integration |
| [`.ai/flows/prescription-diet-flow.md`](../../.ai/flows/prescription-diet-flow.md) | The gate is checked twice — add-to-cart and checkout — through the same contract method |
| [`.ai/flows/batch-recall-flow.md`](../../.ai/flows/batch-recall-flow.md) | **Superseded by HO-IMP-001 ADR-104** (README C-3): Ordering answers nothing; it stamps party and reference on every movement and Inventory reads the trace. Unshipped orders with a recalled batch still cannot dispatch |
| HO-ADM-001 §8.2, §8.6 | Branch-scoping rule (orders carry `BranchId`); snapshot-at-transaction contract |
| HO-NTF-001 §8.2, §8.4, §8.8 | Template catalogue extension, producer contract, link builder |

---

## 7. Verified platform facts

HO-CAT-001 F1–F10, HO-ACC-001 F11–F18, HO-ADM-001 F19–F22, HO-NTF-001 F23–F27 and HO-APT-001
F28–F30 apply. **F23 is the fact this slice leans on hardest**: `SaveChangesAsync` on the scoped
context commits every tracked entity, so a row staged by a port before the order's save commits
atomically with it.

**No new platform fact was verified for this handover** — the areas it touches do not exist in the
code at `4007225`. The following are **to be verified at the start of Phase A** and recorded in the
test sheet; a failure is a §20 stop:

| # | To verify | Why |
|---|---|---|
| V1 | `migrationBuilder.Sql("CREATE SEQUENCE ordering.order_number_seq …")` plus `SELECT nextval(...)` through `DbContext.Database.SqlQueryRaw<long>` returns increasing values inside a transaction and **does not roll back** on transaction abort | Order numbers may have gaps (they are not invoice numbers); the handler must not assume contiguity |
| V2 | A tracked `CartAggregate` mutated in the same scope as `orderRepository.SaveAsync(order)` is committed by that save, and **its domain events are not dispatched** (the repository dispatches only the passed aggregate's) | ADR-201 relies on both halves: atomic commit, and "enlisted roots raise no events anyone depends on" |
| V3 | The ports of HO-SHP-001, HO-IMP-001, HO-PHM-001 and HO-BIL-001 match §8.14 by name and shape | §20.2 |

---

## 8. Domain model

### 8.1 Context and aggregate boundaries

```
Order context — schema `ordering`
CartAggregate                     (root)  → CustomerId? | GuestTokenHash?     ordering.cart
└── Lines [owned many ≤ 50]       CartLine                                     ordering.cart_line
OrderAggregate                    (root)  → CustomerId?, BranchId              ordering.sales_order
├── Lines [owned many ≤ 50]       OrderLine                                    ordering.order_line
│   └── Allocations [owned ≤ 10]  OrderLineAllocation (batch, location)        ordering.order_line_allocation
├── StatusHistory [owned ≤ 30]    OrderStatusChange                            ordering.order_status_change
├── Payments [owned ≤ 10]         OrderPaymentRef (Billing payment id)         ordering.order_payment_ref
├── Fulfilment [owned one]        FulfilmentDetails (address, zone, slot, pickup branch — snapshot)
├── Shipment [owned one, opt]     Shipment (courier, tracking)
├── HeldPayment [owned one, opt]  HeldPaymentConfirmation (awaiting second approval)
└── CodCollection [owned one, opt]
ReturnRequestAggregate            (root)  → OrderId, CustomerId?               ordering.return_request
└── Lines [owned many ≤ 50]       ReturnLine
OrderRefundAggregate              (root, append-only) → OrderId, ReturnRequestId?   ordering.order_refund
DiscountCodeAggregate             (root)                                        ordering.discount_code
DeliveryZoneAggregate             (root)  → BranchId                            ordering.delivery_zone
├── Coverage [owned ≤ 200]        ZoneCoverage (District, PostalCodes[])        ordering.delivery_zone_coverage
└── Services [owned ≤ 2]          ZoneServiceLevel (+ WeightBands ≤ 10, SlotWindows ≤ 21 as jsonb)  ordering.delivery_zone_service
CourierAggregate                  (root)                                        ordering.courier
BankTransferAccountAggregate      (root, singleton)                             ordering.bank_transfer_account
OrderReconciliationIssueAggregate (root)  → OrderId?                            ordering.reconciliation_issue
```

Generic edge infrastructure, owned here and reusable by later areas (schema `ops`):

```
IdempotencyRecord   (plain entity, not an aggregate)   ops.idempotency_record      ADR-202
InboundWebhookReceipt (plain entity)                   ops.inbound_webhook_receipt  ADR-209
```

**Why returns and refunds are their own roots.** An order can have several returns over its window and
several refunds; both are append-heavy and carry their own approval state (rule 3). **Why discount
redemptions are not a table.** The order records `DiscountCodeId`; per-customer use is a count of that
customer's orders with the code and `DiscountReleased = false`, and the overall limit is a counter on
the code whose `xmin` serialises concurrent redemptions (ADR-207).

**One aggregate mutated per command**, with recorded exceptions, each committed in **one**
`SaveChangesAsync` (ADR-201): placement, transfer confirmation/approval, cancellation, expiry, delivery
with payment, return receipt and decision, refund, counter sale. In each, the **saved** root is the order
(or the refund / return), and the other changes are either **tracked** Order-context roots loaded in the
same scope (cart, discount code, order) or **staged** by an enlisting port of another context (Inventory,
Billing, Notification). §11.4 lists exactly which, per handler.

### 8.2 `CartAggregate` — US-ORD-01 — ADR-199

| Field | Type | Notes |
|---|---|---|
| `OwnerKind` | `CartOwnerKind` | `Guest`, `Customer` |
| `CustomerId?` | Guid? | Customer carts only. **Unique among `Active` carts** |
| `GuestTokenHash?` | string (64 hex) | SHA-256 of the 32-byte random cookie token; guest carts only; unique |
| `Status` | `CartStatus` | `Active`, `Merged` (a guest cart absorbed into a customer cart), `Abandoned` (a guest cart past retention) |
| `MergedIntoCartId?` | Guid? | |
| `Lines` | owned ≤ `orders.maxCartLines` (hard cap 50) | `CartLine`: `Id`, `SkuId`, `CatId?`, `Quantity` (1…`orders.maxQuantityPerLine`), `PriceWhenAdded` (`Money`), `AddedOn`, `UpdatedOn` |
| `LastActivityOn` | `DateTimeOffset` | Every mutation |

A line's **key** is `(SkuId, CatId)`: the same prescription food for two cats is two lines; the same
ordinary SKU is one line.

| Method | Rules | Event |
|---|---|---|
| `OpenForGuest(tokenHash)` / `OpenForCustomer(customerId)` | — | — *(carts raise no events: nothing subscribes; the audit interceptor records changes)* |
| `AddLine(sku snapshot, qty, catId?, now)` | Existing key → quantity becomes `min(existing + qty, max)`; else a new line (`CartFull` at the cap). `PriceWhenAdded` = the SKU's current price **only for a new line** | — |
| `SetQuantity(lineId, qty)` | 1…max; 0 is not accepted (use remove) | — |
| `SetLineCat(lineId, catId)` | The handler checked ownership and authorisation; the new key must not collide → `DuplicateCartLine` | — |
| `RemoveLine(lineId)` / `RemovePlacedLines(lineIds)` | Idempotent | — |
| `MergeFrom(guestCart)` | For each guest line: key exists → quantity = **the larger** of the two (not the sum — ADR-199, OQ-3), `PriceWhenAdded` kept from the older line; else copied (lines beyond the cap are dropped and reported). Guest cart → `Merged` | — |
| `Abandon()` | Guest only | — |

**No stock is reserved by a cart.** Availability shown in the cart is advisory (§12.6).

### 8.3 `OrderAggregate` — fields

| Field | Type | Notes |
|---|---|---|
| `OrderNumber` | string ≤ 20, unique | `ORD-{yyyy}-{n:D6}` from `ordering.order_number_seq` (V1); gaps allowed |
| `Channel` | `OrderChannel` | `Online`, `Counter` (append-only; HO-SUB adds `Subscription`) |
| `CustomerId?` | Guid? | Null only for an anonymous counter sale |
| `BranchId` | Guid | The zone's branch, the pickup branch, or the active branch at the counter (HO-ADM-001 §8.2 rule) |
| `FulfilmentMethod` | `FulfilmentMethod` | `Delivery`, `Pickup`, `Counter` |
| `PaymentMethod` | `OrderPaymentMethod` | `CashOnDelivery`, `BankTransfer`, `PayOnCollection`, `InStore` (counter) |
| `Status` | `OrderStatus` | §8.4 |
| `Lines` | owned | §8.3.1 |
| `Fulfilment` | owned VO | `RecipientName` (≤ 200), `RecipientPhone` (normalised `MobileNumber` or ≤ 30 text), address snapshot (`Line1`, `Line2?`, `City`, `District`, `PostalCode?`, `DeliveryInstructions?` — HO-ACC-001 `DeliveryAddressRules` lengths), `DeliveryZoneId?`, `ZoneName?`, `ServiceLevel?`, `SlotDate?` (`DateOnly`), `SlotWindow?` (`"10:00–13:00"` text + start/end `TimeOnly`), `PickupBranchId?` |
| `Totals` | owned VO | `Subtotal`, `LineDiscountTotal`, `OrderDiscount`, `DeliveryCharge`, `TaxTotal`, `GrandTotal` — `Money`, one currency |
| `DiscountCodeId?`, `DiscountCodeText?`, `DiscountReleased` | | ADR-207 |
| `QuoteFingerprint` | string (64 hex) | What the customer acknowledged (ADR-200) |
| `IdempotencyKey?` | Guid? | Unique with `CustomerId` (belt and braces behind ADR-202) |
| `PaymentDueBy?` | `DateTimeOffset?` | Bank transfer only |
| `CustomerTransferReference?` (≤ 100), `TransferReportedOn?` | | FR-ORD-22 |
| `HeldPayment?` | owned | `Method`, `Amount`, `Reference`, `ReceivedOn`, `RecordedBy`, `RecordedOn` — a confirmation awaiting second approval (ADR-204) |
| `InvoiceId?`, `InvoiceNumber?`, `CreditNoteIds` (≤ 10, jsonb) | | From Billing |
| `Payments` | owned `OrderPaymentRef` | `PaymentId` (Billing), `Method`, `Amount`, `RecordedOn` |
| `AmountPaid`, `AmountRefunded`, `RefundDue` | `Money` | `AmountPaid` = Σ `Payments`; `RefundDue` raised by cancellation of a paid order (§8.6) and reduced by refunds |
| `Shipment?` | owned | `CourierId`, `CourierName`, `TrackingNumber?` (≤ 100), `TrackingUrl?` (from the courier's template, https only), `DispatchedOn`, `DispatchedBy`, `LastTrackingStatus?`, `LastTrackingEventOn?` |
| `ReadyForCollectionOn?`, `CollectByDate?` | | Pickup |
| `DeliveredOn?`, `DeliveryRecordedBy?` | | |
| `CodCollection?` | owned | `Amount`, `CollectorKind` (`Courier` \| `Staff`), `CollectorName` (≤ 200), `CollectorStaffId?`, `CollectedOn`, `RecordedBy` — US-ORD-20 AC2 |
| `DeliveryFailureReason?` | `DeliveryFailureReason?` | `Refused`, `Undeliverable`, `Uncollected` |
| `CancellationReason?`, `CancellationNote?` (≤ 500), `CancelledOn?`, `CancelledBy?`, `CancelledByCustomer` | | |
| `ConfirmedOn?`, `PackingStartedOn?` | | |
| `CustomerAnonymised` | bool | §11.6 |
| `StatusHistory` | owned | `From`, `To`, `ChangedOn`, `ChangedBy`, `Note?` (≤ 200, e.g. *"Payment window expired"*) — **US-ORD-09 AC2 "timestamped and attributed"** |

#### 8.3.1 `OrderLine`

`Id`, `LineNumber`, `SkuId`, `ProductId`, `ProductName` (≤ 200), `VariantLabel` (≤ 200), `Barcode?`,
`ReturnClass` (`ProductReturnClass`: `Food`, `Litter`, `Other` — from the SKU's category, §8.14),
`IsBundle`, `CatId?` (**FR-SHP-11 / D-4**), `Quantity`, `UnitPrice`, `LineSubtotal`, `LineDiscount`,
`OrderDiscountShare` (the apportioned order discount — the tax base and the refund base need it),
`TaxClassId`, `TaxRatePercent`, `TaxAmount`, `LineTotal`, `QuantityReturned`, `Allocations`
(`SkuId` — the component for a bundle, `BatchId`, `LocationId`, `Quantity`, `BatchExpiresOn`).

**Every line records the batches it was allocated** (D-4), so a recall traces batch → order → customer →
cat.

### 8.4 The status machine — US-ORD-09 — ADR-197

Appendix C's states exactly: `PendingPayment`, `Confirmed`, `Packing`, `Dispatched`, `Delivered`,
`Cancelled`, `Returned`, `Refunded`. **No state is added.** An expired transfer order is `Cancelled`
with `CancellationReason = PaymentWindowExpired`; a pickup order uses `Dispatched` for *ready for
collection* and `Delivered` for *collected* (labels are the FE's, keyed on `FulfilmentMethod`).

```
            ┌─ BankTransfer ──► PendingPayment ──ConfirmPayment──┐
Place ──────┤                        │ Expire / Cancel            ▼
            └─ COD, PayOnCollection ─────────────────────────► Confirmed ──StartPacking──► Packing
                                     ▼                            │ Cancel                    │ Cancel (staff)
                                 Cancelled ◄──────────────────────┴───────────────────────────┘
Packing ──Dispatch / MarkReadyForCollection──► Dispatched ──RecordDelivery──► Delivered
Dispatched ──RecordDeliveryFailure──► Returned
Delivered ──(every line fully returned and received)──► Returned
Delivered / Returned ──(AmountRefunded == AmountPaid > 0)──► Refunded
Counter: CompleteCounterSale → PendingPayment → Confirmed → Delivered in one call (three history rows)
```

| Method | From | Rules | Event |
|---|---|---|---|
| `Place(dto)` | — | Lines ≥ 1; totals consistent with lines (the aggregate re-sums minor units and refuses a mismatch — a programming error); method/fulfilment pairing (§8.8) | `OrderPlacedEvent` |
| `AwaitTransfer(dueBy)` | new | BankTransfer only | — |
| `RecordAllocations(allocations)` | `PendingPayment`, `Confirmed` | Σ per line and component == required quantity, else `InvalidOperationException` | — |
| `Confirm(invoiceRef, now, by)` | new (COD / PayOnCollection) or `PendingPayment` (via `ConfirmPayment`) | Allocations complete | `OrderConfirmedEvent` |
| `SubmitTransferReference(ref, now)` | `PendingPayment` | BankTransfer; overwrite allowed while pending | `OrderTransferReferenceSubmittedEvent` |
| `HoldPaymentForApproval(details)` | `PendingPayment` | No hold already → `PaymentAlreadyAwaitingApproval` | `OrderPaymentHeldForApprovalEvent` |
| `ConfirmPayment(paymentRef, invoiceRef, now, by)` | `PendingPayment` | Clears any hold | `OrderConfirmedEvent` |
| `RejectHeldPayment(by, now)` | `PendingPayment` with hold | | `OrderHeldPaymentRejectedEvent` |
| `Expire(now)` | `PendingPayment` | BankTransfer, `now ≥ PaymentDueBy`, **no hold** (money claimed received is not expired) → else `OrderNotExpirable` | `OrderCancelledEvent` (`PaymentWindowExpired`) |
| `Cancel(by, byCustomer, reason, note, now)` | customer: `PendingPayment`, `Confirmed`; staff: also `Packing` | Else **`CancellationNotAvailable`** (*"Packing has started, so this order can no longer be cancelled online. Please contact the shop."*) — **US-ORD-12 AC2**. Paid → `RefundDue = AmountPaid − AmountRefunded` | `OrderCancelledEvent` (`Reason`, `WasPaid`) |
| `StartPacking(by, now)` | `Confirmed` | | `OrderPackingStartedEvent` |
| `Dispatch(shipment, now, by)` | `Packing` | Delivery only; allocations complete; **batch check passed by the handler (BR-14)** | `OrderDispatchedEvent` |
| `MarkReadyForCollection(collectBy, now, by)` | `Packing` | Pickup only; same batch check | `OrderReadyForCollectionEvent` |
| `RecordTracking(number, url)` | `Dispatched` | Delivery | — |
| `ApplyTrackingEvent(status, occurredOn)` | `Dispatched`, `Delivered` | Only a newer `occurredOn` updates | — |
| `RecordDelivery(deliveredOn, codCollection?, paymentRef?, by)` | `Dispatched` | COD → collection required (`CodCollectionRequired`); PayOnCollection → payment required and equal to the balance due (`CollectionPaymentMismatch`); `deliveredOn ≤ now` | `OrderDeliveredEvent` |
| `RecordDeliveryFailure(reason, note, by)` | `Dispatched` | `Uncollected` only for pickup; `Refused`/`Undeliverable` only for delivery | `OrderDeliveryFailedEvent` → status `Returned` |
| `RegisterReturnedQuantity(lineId, qty)` | `Delivered`, `Returned` | ≤ delivered − returned; all lines fully returned while `Delivered` → `Returned` | `OrderReturnedEvent` (when it transitions) |
| `ApplyRefund(amount)` | any status with `AmountPaid > 0` | `AmountRefunded + amount ≤ AmountPaid` → else `RefundExceedsAmountPaid`; reduces `RefundDue` (floor 0); `Delivered`/`Returned` with full refund → `Refunded` | `OrderRefundedEvent` (when it transitions) |
| `CompleteCounterSale(dto)` | — | §8.11 | `CounterSaleCompletedEvent` |

Any other transition → **`InvalidOrderTransition`**, message naming both states (*"A delivered order
cannot be packed."*) — **US-ORD-09 AC2**. Every transition appends to `StatusHistory` with the actor.
`OrderTransitionRules` (Domain.DomainServices, pure) holds the table; the aggregate and the read side's
`allowedActions` use the same table.

### 8.5 Pricing — US-ORD-04, US-ORD-05, US-ORD-06 — ADR-198, ADR-200

`CheckoutPricing` (Domain.DomainServices, **pure**, no I/O) takes a `CheckoutInputs` value and returns a
`CheckoutQuote`. All arithmetic is on **`long` minor units** (`MinorUnits` value type, Shared); `Money`
(ADR-054) appears only at the edges, converted exactly (`amount × 10^digits`, digits = 2 for BDT from an
ISO table in Shared; a currency with other digits is refused at start-up — OQ-5).

Algorithm, each step tested:

1. **Lines.** For each cart line that is purchasable: `unit` = current price; `qty` = `min(cart qty,
   sellable)`; `subtotal = unit × qty`.
2. **Line discounts.** A code scoped to products or categories gives each matching line
   `round(subtotal × pct)` (percentage) or a share of the fixed value apportioned across matching lines
   by subtotal (largest-remainder, so shares sum exactly); a line discount never exceeds its subtotal.
3. **Order discount.** An unscoped code applies to `Σ(subtotal − lineDiscount)`; apportioned across
   lines by the same method into `OrderDiscountShare`.
4. **Tax.** Per line, `taxBase = subtotal − lineDiscount − orderDiscountShare`;
   `tax = round(taxBase × ratePercent / 100)`. Rounding is **half away from zero at the line**, never on
   the total. Rates are read at the quote instant (HO-ADM-001 ADR-049) and snapshotted on the line.
5. **Delivery charge** (§8.8), untaxed (OQ-6).
6. **Totals.** `GrandTotal = Σsubtotal − ΣlineDiscount − orderDiscount + delivery + Σtax` — FR-ORD-5's
   formula, each component returned separately. The aggregate's `Place` re-sums and refuses a mismatch.
7. **Fingerprint.** SHA-256 over a canonical, culture-invariant serialisation of: fulfilment choice,
   address id, zone, service level, slot, pickup branch, payment method, discount code, and per line
   `(skuId, catId, qty, unit, lineDiscount, orderDiscountShare, taxRate, tax)`, plus the totals. It is
   not a secret; it is a comparison token (ADR-200).

**Changes** since the item entered the cart (US-ORD-02), each with the line and a plain message:

| Change | When | Effect on the quote |
|---|---|---|
| `PriceIncreased` / `PriceDecreased` | current price ≠ `PriceWhenAdded` | Priced at the current price |
| `QuantityReduced` | 0 < sellable < cart quantity | Quantity = sellable |
| `NowUnavailable` | sellable = 0, or the product/variant is no longer purchasable | Line excluded |
| `PrescriptionAuthorisationExpired` | Rx SKU and the authorisation for the line's cat is no longer valid (**US-SHP-10 AC3**) | Line excluded, message with a consultation-booking hint |
| `PrescriptionCatRequired` | Rx SKU with no cat on the line (**US-ORD-03 AC2**) | Quote `canPlace = false` until the cat is set |

Excluded lines stay in the cart after placement; placed lines are removed (§11.4 C9).

### 8.6 Money in and money out — ADR-203, ADR-204, ADR-211

| Method | Fulfilment | Confirmed when | Payment recorded (Billing) | Unpaid hold |
|---|---|---|---|---|
| `CashOnDelivery` | Delivery | **At placement** — accepted as COD (FR-ORD-9, FR-ORD-21) | At delivery: amount, collector, date (US-ORD-20 AC2) | — |
| `PayOnCollection` | Pickup | **At placement**, like COD (OQ-2) | At collection: cash, or in-store card with the terminal slip reference | Uncollected after `CollectByDate` is listed; staff record `Uncollected` → returns path |
| `BankTransfer` | Delivery or pickup | When staff verify receipt against the bank record (FR-ORD-22) | At that confirmation | `PendingPayment`, stock reserved, expires at `PaymentDueBy` (BR-16) |
| `InStore` | Counter | At completion of the sale | Each tender at completion | — |

**Second approval** (FR-BIL-12, US-BIL-11 AC2): a bank-transfer confirmation whose amount exceeds
Billing's `billing.paymentApprovalThreshold` is **held on the order** (`HeldPayment`) and nothing is
written to Billing; a different staff member approves through this slice's endpoint, and only then are
the invoice, the payment (recorded-by = the first person, approved-by = the second) and the
confirmation committed. The expiry worker skips held orders (ADR-204). COD collections and counter
tenders are not held — they are reconciled against courier remittances and the till (OQ-7).

**Transfer amount** must equal `GrandTotal` exactly → else `TransferAmountMismatch` (OQ-4).

**Refund methods by how the customer paid** (US-ORD-15 AC1):

| Paid by | Allowed refund methods |
|---|---|
| Cash (COD, cash at collection, cash at the counter) | `CashInStore`, `BankTransfer`, `StoreCredit` |
| `BankTransfer` | `BankTransfer`, `StoreCredit` |
| In-store card | `CashInStore`, `BankTransfer`, `StoreCredit` (no card refund path exists without a gateway — OQ-9) |

`CashInStore` needs a receipt number, `BankTransfer` a transfer reference; `StoreCredit` needs Billing's
store-credit port (OQ-8). An order paid by several tenders allows the union.

### 8.7 Checkout rules — US-ORD-03, US-ORD-07

`PlaceOrderCommand` is accepted only when, in order:

1. The caller is a customer (no guest checkout — OQ-1); the cart is theirs, `Active`, non-empty.
2. The customer has **no pending acceptance** of a current `RequiresAcceptance` policy (HO-ADM-001
   §8.8, read through `ICustomerPolicyStatus`) → else `PolicyAcceptanceRequired`. The client accepts
   through HO-ADM-001's endpoint first; this slice never writes acceptances.
3. The recomputed quote's fingerprint equals the command's → else **`409 CheckoutQuoteChanged`**.
4. `canPlace` is true (≥ 1 purchasable line; every Rx line has a cat).
5. The chosen fulfilment is available: delivery → the address is in the customer's address book
   (HO-ACC-001) and inside an active zone with the chosen service level active; pickup → an active
   branch; a slot, when given, is one the service level offers on that date, not in the past, and after
   the service's lead time.
6. The payment option is available (§8.8) → else `PaymentMethodUnavailable` with the reason code.
7. Delivery needs a recipient phone: the command's, else the customer's mobile → else
   `RecipientPhoneRequired`.

### 8.8 Delivery zones, charges, slots and payment availability — US-ORD-06, US-ORD-07 — ADR-206

**`DeliveryZoneAggregate`** — `BranchId`, `Name` (≤ 100, unique), `Priority` (int, lower wins),
`IsActive`, `CodEnabled`, `CodMaxOrderValue?` (`Money`; null → the setting), `Coverage` (owned:
`District` (≤ 100, compared case- and whitespace-insensitively), `PostalCodes` (≤ 100 codes, jsonb;
empty = the whole district)), `Services` (owned, ≤ 2: `ServiceLevel` `Standard` \| `Express`,
`IsActive`, `BaseCharge`, `FreeOverOrderValue?`, `WeightBands` (≤ 10: `UpToGrams`, `Surcharge`; last
band open-ended), `LeadTimeDays` (0…14), `SlotWindows` (≤ 21: `DayOfWeek`, `Start`, `End` in branch-local
time; empty = no slot choice)).

**Zone for an address** (`DeliveryZoneResolver`, pure): active zones whose coverage has the address's
district and (no postal codes, or the address's postal code in the list). A postal-code-specific match
beats a district-wide one; then lowest `Priority`; then name. None → `AddressOutsideServedZones` —
**US-ORD-06 AC2**.

**Charge** (`DeliveryChargeCalculator`, pure, minor units): `goodsValue = Σ(subtotal − lineDiscount) −
orderDiscount`; if `FreeOverOrderValue` set and `goodsValue ≥` it → 0; else `BaseCharge` + the surcharge
of the first band with `totalWeight ≤ UpToGrams` (weight = Σ SKU `ShippingWeightGrams × qty`; a SKU
without a weight counts as 0 and is logged once at `Warning`) — **US-ORD-06 AC1** (zone, order value,
weight, service level).

**Slots** (`DeliverySlotCalculator`, pure): the service level's windows for the next 7 branch-local
dates on or after `today + LeadTimeDays`, excluding HO-ADM-001 closures of the zone's branch
(`IBranchCalendar`). **Slots are preference windows with no capacity** (OQ-13). "Where applicable" =
the service level has windows.

**Payment availability** (`PaymentOptionPolicy`, pure) — each method `available` or an
`unavailableReason`:

| Method | Unavailable when | Reason code |
|---|---|---|
| `CashOnDelivery` | fulfilment is pickup | `NotForPickup` |
| | `orders.codEnabled` false (branch) | `CodDisabled` |
| | the zone has `CodEnabled = false` | `CodNotOfferedInZone` — **BR-15** |
| | `GrandTotal >` the zone's ceiling (or the setting) | `CodAboveOrderLimit` (message names the limit) — **BR-15, US-ORD-07 AC2** |
| `BankTransfer` | `orders.bankTransferEnabled` false, or no active `BankTransferAccount` | `BankTransferDisabled` |
| `PayOnCollection` | fulfilment is delivery; or `orders.payOnCollectionEnabled` false | `NotForDelivery` / `PayOnCollectionDisabled` |

### 8.9 Stock through Inventory — ADR-208

| Moment | Call (enlisting, §8.14) | Result on the order |
|---|---|---|
| Placement (every method) | `IStockLedger.ReserveAsync(demand, StockReference(Order, orderId, lineId), StockParty(customerId, catId))` — FEFO per component, never an expired or quarantined batch (BR-3, BR-4) | `RecordAllocations`; `ViolationCode = InsufficientStock` → **`409 StockNoLongerAvailable`** naming the product (US-IMP-08 AC2) |
| Staff override at picking | `ReallocateAsync(orderId, lineId, targetBatchId, reason)` | Allocations replaced; reason mandatory (US-IMP-07 AC2) |
| Dispatch (delivery) / collection (pickup) | `ConvertReservationToIssueAsync(orderId)` — the sale ledger entries | — |
| Cancellation / expiry | `ReleaseReservationAsync(orderId)` | — |
| Return received | `ReceiveIntoQuarantineAsync(returnId, lines)` | — |
| Return decided | `RestockFromQuarantineAsync` (to the **original batch**) / `WriteOffFromQuarantineAsync(reason)` | — |
| Counter sale | `IssueDirectAsync(orderId, branchId, requirements)` — FEFO at the branch's counter location | `RecordAllocations` |

**BR-14 at dispatch and ready-for-collection:** `IBatchStatusQuery.GetAsync(allocated batch ids)`; any
`IsRecalled` → `BatchRecalled`, any `IsExpired` → `BatchExpired` (message: reallocate the line); any
line not fully allocated → `OrderNotFullyAllocated`. **US-IMP-11 AC4** is met by this check.

**Recall tracing** — **nothing is built here** (README C-3). HO-IMP-001 ADR-104 replaced the
per-module `IBatchConsumptionQuery` fan-out with a stamp taken at movement time, so this slice's
obligation is to pass `StockReference(Order, orderId, lineId)` and `StockParty(customerId, catId)` on
**every** `IStockLedger` call — reserve, consume, deduct and return alike. A movement missing the stamp
is a hole in the trace, so U-D11 asserts it on every call site (**US-IMP-11 AC2**, order path; D-4).

### 8.10 Returns and refunds — US-ORD-13, 14, 15, US-ORD-20 AC3 — ADR-210, ADR-211

**`ReturnRequestAggregate`** — `OrderId`, `CustomerId?`, `Kind` (`CustomerReturn` \| `DeliveryFailure`),
`Status` (`Requested` → `Approved` \| `Rejected`; `Approved` → `Received` → `Closed`), `RequestedBy`,
`DecidedBy?`, `DecisionNote?` (≤ 500), `ReceivedOn?`, `Lines` (owned: `OrderLineId`, `Quantity`,
`ReasonCode` (`WrongItem`, `Damaged`, `QualityDefect`, `ChangedMind`, `Recalled`, `Other`),
`Condition` (`Sealed`, `Opened`, `Used`), `CustomerNote?` (≤ 500), `DefectConfirmed`, `DefectNote?`,
`QuantityReceived`, `StockDecision?` (`Restock` \| `WriteOff`), `DecisionReason?`, `DecidedBy?`,
`DecidedOn?`).

**Eligibility** (`ReturnEligibilityPolicy`, pure) per line — **US-ORD-13**:

1. Order `Delivered`, and today (branch-local) ≤ `DeliveredOn + orders.returnWindowDays` →
   else `ReturnWindowClosed`.
2. Quantity ≤ delivered − returned − quantities in open returns → else `ReturnQuantityExceeded`.
3. **BR-10**: `ReturnClass = Food` with `Condition ≠ Sealed`, or `ReturnClass = Litter` with
   `Condition = Used` → **refused** (`ReturnNotEligibleOpenedFood` / `ReturnNotEligibleUsedLitter`)
   **unless** any allocated batch `IsRecalled` or `HasQualityDefect` (Inventory), **or** the reason is
   `QualityDefect` — in which case the line is accepted *for approval* with `RequiresDefectConfirmation`,
   and approval must set `DefectConfirmed` with a note (the defect is then "recorded"). A recall or an
   Inventory defect makes the line eligible outright.
4. Otherwise eligible → `Requested`, awaiting staff approval — **US-ORD-13 AC1**.

A request with any refused line is refused whole, with a per-line reason (`propertyName`
`Lines[i]`), so the customer can resubmit without it.

**Delivery failure** (US-ORD-20 AC3): `RecordDeliveryFailure` creates a `DeliveryFailure` return,
already `Approved`, covering every line; COD → Billing credits the unpaid invoice in full; the parcel is
then received like any return.

**Receipt** → Inventory quarantine (**US-ORD-14 AC1**); `QuantityReceived` per line; the order's
`RegisterReturnedQuantity`. **Decision** per line → restock against the original batch or write off,
reason and user recorded (**US-ORD-14 AC2**); Inventory refuses restocking an expired or recalled batch
(BR-3) and the handler surfaces its code. All lines decided → `Closed`.

**`OrderRefundAggregate`** (append-only) — `OrderId`, `ReturnRequestId?`, `Method` (`CashInStore`,
`BankTransfer`, `StoreCredit`), `Amount`, `Reference` (≤ 100), `Reason` (≤ 500), `IssuedBy`,
`IssuedOn`, `CreditNoteId`. Created by `IssueRefundCommand`; amount ≤ `AmountPaid − AmountRefunded`;
method allowed by §8.6; **a credit note is raised through Billing in the same save** unless the order's
invoice was already credited in full at cancellation, in which case that credit note is linked
(**US-ORD-15 AC2**). The suggested amount for a return is `RefundCalculator` (pure): per returned unit,
`(LineTotal) / Quantity` in minor units with the remainder on the last unit; the delivery charge is
never included automatically (OQ-9). **No refund is automated** (SRS §18.5).

### 8.11 Point of sale — US-ORD-16 — ADR-212

A counter sale is an `OrderAggregate` with `Channel = Counter`, `FulfilmentMethod = Counter`,
`PaymentMethod = InStore`, `BranchId` = the active branch.

1. **Scan.** A barcode (HW-2 keyboard wedge, FE) → `GET /api/staff/pos/sku?barcode=` → the SKU
   (Shop's barcode lookup), its current price, sellable quantity at the branch's counter location and a
   **FEFO batch preview** (Inventory) — **US-ORD-16 AC2**. Manual search is the fallback.
2. **Complete.** `CompleteCounterSaleCommand` (lines, optional customer, optional discount code, tenders):
   pricing through `CheckoutPricing` (no delivery); an Rx line needs a customer and cat with a valid
   authorisation; tenders: `Cash` (`amountTendered` ≥ the cash due; `changeDue` returned) and
   `InStoreCard` (amount ≤ the remaining balance; **terminal slip reference required**, ≤ 50);
   Σ tenders (card at face value, cash up to the balance) must equal `GrandTotal` →
   else `TendersDoNotCoverTotal`.
3. Inventory `IssueDirectAsync` — **the same ledger as online orders** (US-ORD-16 AC1); Billing invoice
   and one payment per tender; `CompleteCounterSale` records `PendingPayment → Confirmed → Delivered`.
   One save.
4. **Receipt** — `GET /api/staff/pos/sales/{orderId}/receipt`: branch name, address, legal identifiers
   (from Billing's invoice — NFR-LEG-3), invoice and order numbers, lines, discounts, tax per line,
   totals, tenders, change, cashier, time in branch zone; plus `text` — a 48-column plain rendering
   (`ReceiptTextRenderer`, pure) for 80 mm printers.

Anonymous walk-in sales carry no customer (Billing issues to "Walk-in customer" — OQ-14).

### 8.12 Messages — FR-ORD-13, FR-NTF-3 — ADR-213

Templates appended to HO-NTF-001's catalogue. All **Transactional** (order and payment — FR-NTF-4) with
`Sms`, `Email`, `InApp`. Standard variables (`recipientName`, `clinicName`, `clinicPhone`) come from
Notification. Money variables are pre-formatted text (`BDT 1,250.00`); instants are formatted in the
branch zone by the producer as HO-APT-001 §8.9 does.

| Key | Urgency | Enqueued by | Producer variables (required on every channel unless noted) |
|---|---|---|---|
| `order.confirmed` | **Urgent** — the customer placed it seconds ago, or staff confirmed their money | C9 (COD, pay-on-collection), C11/C12 (transfer) | `orderNumber`, `orderTotal`, `paymentSummary` (*"Cash on delivery"*, *"Paid by bank transfer"*), `fulfilmentSummary`, `orderLink` (Url) |
| `order.awaiting-transfer` | **Urgent** | C9 (transfer) | `orderNumber`, `amountDue`, `bankAccountName`, `bankAccountNumber`, `bankName`, `bankBranch` (email/in-app), `paymentReference`, `payByLocal`, `orderLink` |
| `order.expired` | Normal | C13 | `orderNumber`, `orderLink` (the order page offers **Order again** — US-ORD-19 AC3) |
| `order.cancelled` | Urgent | C14 (no money received) | `orderNumber`, `cancelledByShop` (text), `orderLink` |
| `order.cancelled-refund-due` | Urgent | C14 (money received) | `orderNumber`, `refundAmount`, `orderLink` |
| `order.dispatched` | Normal | C16 | `orderNumber`, `courierName`, `trackingNumber` (text; *"to follow"* when blank), `orderLink` |
| `order.tracking-added` | Normal | C17 (first number only) | `orderNumber`, `courierName`, `trackingNumber`, `orderLink` |
| `order.ready-for-collection` | Normal | C16 (pickup) | `orderNumber`, `branchName`, `branchAddress`, `collectByLocal`, `amountDue`, `orderLink` |
| `order.delivered` | Normal | C18 | `orderNumber`, `orderLink` |
| `order.delivery-failed` | Normal | C19 | `orderNumber`, `orderLink` |
| `order.return-decided` | Normal | C23 approve/reject | `orderNumber`, `returnOutcome` (text), `orderLink` |
| `order.refund-issued` | Urgent — money | C26 | `orderNumber`, `refundAmount`, `refundMethod`, `orderLink` |

`INotificationLinkBuilder` gains **`OrderLink(orderId)`** → `{WebBaseUrl}/portal/orders/{orderId}`.
The customer is the recipient; an anonymous counter sale sends nothing. **No template carries a
tracking URL** — Url variables must come from the link builder; the order page shows the courier link.

### 8.13 Idempotency and webhooks — US-ORD-18 — ADR-202, ADR-209

**`[Idempotent]`** (Api filter, `IAsyncResourceFilter`) on: placement, transfer confirmation, payment
approval, delivery recording, refund issue, counter-sale completion, re-order.

1. `Idempotency-Key` header required (a UUID) → else `400 IdempotencyKeyRequired`.
2. Scope = caller kind + caller id + route template. Request hash = SHA-256 of the canonical body.
3. `INSERT … ON CONFLICT DO NOTHING` into `ops.idempotency_record (scope, key)` with `State = InProgress`,
   **committed on its own** before the action (a separate short-lived `DbContext` from
   `IDbContextFactory` — never the request's scoped context).
4. Conflict: same hash and `Completed` → **replay** the stored status and body, header
   `Idempotent-Replayed: true`; same hash and `InProgress` → `409 RequestInProgress`; different hash →
   `422 IdempotencyKeyReused`.
5. After the action: `2xx`/`4xx` → store status and body (≤ 64 KB), `Completed`; an unhandled exception →
   delete the record so a retry can run.
6. Records older than 24 h are purged by `OrderWorker`.

The order also stores `IdempotencyKey` with a unique index on `(customer_id, idempotency_key)`; a
violation maps to `409 DuplicateSubmission` — it can only occur if the filter were bypassed.

**Inbound webhooks** — `POST /api/webhooks/couriers/{provider}`: the provider must be registered in
`Couriers:Webhooks:{provider}` (secret, signature header name) → else `404`. Signature: HMAC-SHA256 of the
raw body with the provider secret, compared in fixed time → else `401` and a `Warning` log (no body).
`ops.inbound_webhook_receipt (provider, event_id)` unique: a duplicate event id → `200` and nothing
else (**US-ORD-18 AC2**). The body shape is this slice's **generic tracking contract** (`eventId`,
`trackingNumber`, `status`, `occurredOn`) until a real courier's mapper is written (Q-4). Accepted
events dispatch `ApplyCourierTrackingEventCommand`.

### 8.14 Ports — consumed and owned

**Consumed — assumed contracts of areas whose handovers do not yet exist** (§20.2 if they differ).
"Enlisting" = stages entities on the scoped `CatEcommerceDbContext` and **never saves** (the HO-NTF-001
`INotificationOutbox` technique); staged roots raise no domain event anyone depends on — consumers
subscribe to this slice's order events instead.

| Port | Owner | Surface assumed | Kind |
|---|---|---|---|
| `ICatalogueLookup` | **HO-SHP-001** | `GetSkusAsync(skuIds)` / `GetByBarcodeAsync(barcode)` / `SearchSellableAsync(text, take)` → `SkuSnapshot(SkuId, ProductId, ProductName, VariantLabel, Barcode?, CategoryRoot → ProductReturnClass, CurrentPrice Money, TaxClassId, IsPurchasable, RequiresPrescription, ShippingWeightGrams?, ImagePath?, BundleComponents[(SkuId, Quantity)])` | read |
| `inventory.v_sellable_stock` / `GetSellableStockQuery` | **HO-IMP-001** (ADR-116) | Per SKU per branch: sellable quantity and the FEFO-first expiry (FR-IMP-8: excludes expired, quarantined, damaged, reserved). A **view**, not a port — joinable, so cart and catalogue reads stay one query | read |
| `IStockLedger` | **HO-IMP-001** §8.8 | The eight operations this slice needs map onto five published methods: reserve → `ReserveAsync`; release → `ReleaseAsync`; dispatch/collect → `ConsumeReservationAsync`; counter sale → `DeductAsync`; return into quarantine → `ReceiveReturnAsync` (rule 8 already writes returns to the branch's `Quarantine` only). Reallocation is `ReleaseAsync` + `ReserveAsync` with `ExplicitLines` + `OverrideReason` (audited by rule 5); restock-from-quarantine and write-off are **Inventory's screens, not this slice's calls** (OQ-19, closed). The port **never throws for a business rule**: it returns `StockResult` with `ViolationCode`, and this slice maps `InsufficientStock` to `409 StockNoLongerAvailable` (§12.5); a race after planning surfaces at **this slice's** commit as `23514` | enlisting |
| `IBatchStatusQuery` | **HO-IMP-001** | `GetAsync(batchIds)` → `IsRecalled`, `IsExpired`, `HasQualityDefect` | read |
| `IDietAuthorisationGate` | **HO-PHM-001** | `IsAuthorisedAsync(catId, skuId, asOf)` → bool (valid, unexpired, for that cat and product) — `.ai/flows/prescription-diet-flow.md` | read |
| `IInvoiceIssuer` | **HO-BIL-001** | `IssueForOrderAsync(InvoiceRequest(OrderId, CustomerId?, BranchId, Lines[(SourceLineId, Description, Qty, UnitPrice, LineDiscount, OrderDiscountShare, TaxClassId, TaxRatePercent, TaxAmount, BusinessLine = Shop)], DeliveryCharge, Totals))` → `InvoiceRef(InvoiceId, InvoiceNumber)`; the sequential number is allocated inside the same transaction (US-BIL-01) | enlisting |
| `IPaymentRecorder` | **HO-BIL-001** | `RecordAsync(PaymentRecordRequest(InvoiceId, Method, Amount, Reference, ReceivedOn, RecordedBy, ApprovedBy?, CollectorName?, CollectorStaffId?))` → `PaymentId`. Methods `Cash`, `InStoreCard`, `BankTransfer`, `CashOnDelivery` | enlisting |
| `ICreditNoteIssuer` | **HO-BIL-001** | `IssueAsync(invoiceId, amount \| full, reason, refundId?)` → `CreditNoteId` | enlisting |
| `IStoreCreditLedger` | **HO-BIL-001** (US-BIL-08, Could) | `CreditAsync(customerId, amount, reference)`; **optional** — absent → `RefundMethodUnavailable` | enlisting |
| Setting `billing.paymentApprovalThreshold` | **HO-BIL-001** | Decimal, read through `IBusinessSettings` | read |

**Consumed — existing:** HO-ACC-001 address book read (`GetDeliveryAddressesQuery` data via a read port
`ICustomerDirectory` over `account.*`, AsNoTracking: addresses, name, mobile, status), HO-ADM-001
`IBusinessSettings`, `IBranchCalendar`, tax rate "at *t*" (read over `admin.tax_rate` inside
`CheckoutInputsLoader`), `ICustomerPolicyStatus` (read over `admin.policy_*`), `IActiveBranchAccessor`,
HO-NTF-001 `INotificationOutbox`, `INotificationLinkBuilder`.

**Owned:**

| Name | Kind | Notes |
|---|---|---|
| `CheckoutPricing`, `DeliveryZoneResolver`, `DeliveryChargeCalculator`, `DeliverySlotCalculator`, `PaymentOptionPolicy`, `OrderTransitionRules`, `ReturnEligibilityPolicy`, `RefundCalculator`, `RefundMethodPolicy`, `ReceiptTextRenderer`, `DiscountRules` | pure domain services | §8.4–§8.11 |
| `ICheckoutInputsLoader` | port → `Infrastructure/Ordering/CheckoutInputsLoader` | Loads everything `CheckoutPricing` needs in as few round trips as possible: cart, SKU snapshots, sellable, authorisations, tax rates, zone, slots inputs, discount code + the customer's use count + prior-order flag, settings |
| `IGuestCartTokens` | port → Infrastructure | Mint (32 bytes, base64url) and hash |
| `IReconciliationIssueRecorder` | port → Infrastructure | Writes an issue in a **new DI scope** (ADR-205) |
| `ICourierGateway` | port, **no implementation** in this slice | Declared for the Q-4 adapter (ADR-209) |

### 8.15 Discount codes — US-ORD-05 — ADR-207

**`DiscountCodeAggregate`** — `Code` (3…30, `^[A-Z0-9-]+$`, stored upper-case, unique), `Description?`,
`Kind` (`Percentage` \| `FixedAmount`), `Percent?` (0.01…100, 2 dp) / `Amount?` (`Money`),
`MinimumSpend?`, `Scope` (`Order` \| `Products` \| `Categories`), `ProductIds` / `CategoryIds` (≤ 200,
jsonb), `FirstOrderOnly`, `UsageLimit?`, `PerCustomerLimit?`, `ValidFrom?`, `ValidTo?`, `IsActive`,
`TimesRedeemed`. Methods `Create`, `Update` (all but code; `UsageLimit` not below `TimesRedeemed`),
`SetActivation`, `Redeem()` (`TimesRedeemed + 1`, refused at the limit), `ReleaseRedemption()`
(floor 0).

**`DiscountRules.Evaluate(code, cartLines, customerFacts, now)`** returns applied, or the **first failing
rule** in this order, each its own code and plain message — **US-ORD-05 AC2**: `DiscountCodeNotFound`,
`DiscountCodeInactive`, `DiscountCodeNotYetValid`, `DiscountCodeExpired`, `DiscountUsageLimitReached`,
`DiscountCustomerLimitReached`, `DiscountFirstOrderOnly` (*"This code is for a first order only."* —
a customer with any order not `Cancelled`), `DiscountNotApplicableToItems`, `DiscountMinimumSpendNotMet`
(*"Spend BDT 1,500.00 on eligible items to use this code."*). One code per order.

The quote never fails for a bad code — it returns `discount.applied = false` with the reason. Placement
re-evaluates; `Redeem` runs on the **tracked** code in the placement save, so two placements racing for
the last use collide on `xmin` → one `409` (the FE re-quotes). Cancellation and expiry before dispatch
release the use.

### 8.16 Reconciliation issues — US-ORD-08 AC2 — ADR-205

**`OrderReconciliationIssueAggregate`** — `OrderId?`, `Kind` (`TransferConfirmationFailed`,
`PaymentApprovalFailed`, `DeliveryPaymentFailed`, `CounterSaleFailed`, `RefundFailed`),
`PaymentDetails` (method, amount, reference, received on — what the staff member entered), `ErrorSummary`
(exception type and a fixed message; **never** the exception message, which may carry data),
`CorrelationId`, `RaisedBy`, `RaisedOn`, `Status` (`Open` → `Resolved`), `ResolvedBy?`,
`ResolutionNote?` (≤ 500).

The handlers named in §11.4 wrap their save in `try`/`catch`: any exception **other than**
`DomainRuleViolationException` (a business refusal writes nothing and needs no reconciliation) →
`IReconciliationIssueRecorder.RecordAsync(...)` in a fresh scope, then rethrow. The staff member sees the
`500`-class problem response with *"The payment details were saved for reconciliation (reference
{issueId})."*; the issue is listed for accountants until resolved. The recorder itself never throws out
(logged at `Critical` if it fails).

### 8.17 Exceptions

Reuse `DomainRuleViolationException`. No new type.

---

## 9. ADRs to record during this build

Append to `.ai/decisions/adr.md`. **This handover owns ADR-197 … ADR-216** (coordinator allocation;
the README ADR table is updated by the coordinator, not by this slice). ADR-215 and ADR-216 are unused
and remain reserved to this handover for amendments.

| ADR | Decision | Why it must be recorded |
|---|---|---|
| **ADR-197** | **`Order` bounded context**, namespace segment `Order`, schema `ordering`, owning the roots of §8.1. Order statuses are **exactly Appendix C's**: expiry is `Cancelled` + `PaymentWindowExpired`; pickup reuses `Dispatched` (ready for collection) and `Delivered` (collected) | Adding states would diverge from Appendix C and from every report built on it; the mapping must be deliberate |
| **ADR-198** | **Money is computed in `long` minor units** in the pure pricing services and converted exactly to ADR-054's `decimal(12,2)` `Money` at the boundary; rounding half away from zero at the line; apportionment by largest remainder | US-ORD-04 AC2 and C-6 say *integer minor units*; ADR-054 fixed storage as decimal. This ADR satisfies both without amending ADR-054 (OQ-5 recommends ADR-054 cite it) |
| **ADR-199** | **Carts are server-side.** Guests get an opaque 32-byte token in an `HttpOnly; Secure; SameSite=Lax` cookie `pc_cart` (hash stored); one `Active` cart per customer; merge is an explicit, idempotent `POST …/cart/merge` that takes the larger quantity per `(SKU, cat)` key; carts reserve nothing | US-ORD-01 AC2 "switch device" needs a server cart; HO-ACC-002 forbids storing tokens or drafts in browser storage; a `GET` must not merge |
| **ADR-200** | **Quote-and-fingerprint acknowledgement.** The quote is computed, never stored; placement recomputes it and refuses `409 CheckoutQuoteChanged` unless the submitted fingerprint matches | FR-ORD-3 "must acknowledge"; a client-side "I agree" flag proves nothing about what was shown |
| **ADR-201** | **Enlisting ports and multi-root saves.** Placement, confirmation, approval, cancellation, expiry, delivery-with-payment, return receipt/decision, refund and counter sale commit the order (or refund/return) plus tracked Order-context roots and entities staged by Inventory, Billing and Notification ports in **one** `SaveChangesAsync`. Staged roots raise no domain events anyone depends on. Recorded exception to `.ai/bounded-contexts.md` rule 1 | US-ORD-08 "all atomically"; the alternative (event handlers after commit) fails *after* the order is durable (`.ai/domain-events-and-outbox.md` §3) |
| **ADR-202** | **HTTP idempotency** via `[Idempotent]` and `ops.idempotency_record` (scope, key, request hash, replayed response, 24 h), inserted in its own short transaction; `InProgress` → `409`; different body → `422`; failure frees the key. Plus the unique `(customer_id, idempotency_key)` on orders | ADR-014 moved idempotency to the edge; FR-ORD-20; the filter is reusable by Billing and Boarding |
| **ADR-203** | **Payment methods and holds.** COD and pay-on-collection confirm at placement; bank transfer holds `PendingPayment` with stock reserved until `orders.bankTransferWindowHours`, then `OrderWorker` expires it through a command. **No gateway, no card data, no token; in-store card = method + amount + slip reference** | C-5, A-2, BR-16, FR-ORD-21/22; the pay-on-collection reading of FR-ORD-9 is a choice (OQ-2) |
| **ADR-204** | **Second approval of received money** above Billing's threshold is held on the order and completed through this slice's approve endpoint by a different person; Billing is written only when final; held orders do not expire | FR-BIL-12 / BR-17 without a Billing-to-Order event that would have to confirm the order from an event handler |
| **ADR-205** | **Reconciliation issues.** A non-business failure after staff entered payment details writes an issue in a fresh DI scope, then rethrows; accountants resolve it | US-ORD-08 AC2 "surfaced … not silently lost" when the one transaction has rolled back |
| **ADR-206** | **Delivery zones** by district plus optional postal codes, priority-ordered; two service levels with weight bands and free-over thresholds; COD toggle and ceiling per zone; slots are capacity-free preference windows; zones edited in place (audited) and **snapshotted on the order** | FR-ORD-7, BR-15; HO-ADM-001 §4.2 routed zones here; ADR-049's snapshot rule is what protects history, not append-only zone rows |
| **ADR-207** | **Discount codes** evaluated by one pure rule list with a fixed order of failure reasons; usage counted on the code (`xmin` serialises) and per customer from orders; scoped → line discounts, unscoped → order discount; released on cancel/expiry before dispatch | FR-ORD-6; a redemption table would be one more root in the placement save for no gain |
| **ADR-208** | **Stock only through `IStockLedger`.** Reserve FEFO at placement for every method, `ConsumeReservationAsync` at dispatch (delivery) or collection (pickup), `ReleaseAsync` on cancel/expiry; BR-14 checked at dispatch; every call carries the party and reference stamp that feeds Inventory's recall trace (README C-3) | `.ai/flows/order-fulfilment-flow.md` "Ordering never picks a batch itself"; D-4 |
| **ADR-209** | **Couriers.** Manual courier, manual tracking and CSV manifest now; `ICourierGateway` declared for an API adapter that will run as an outbox-driven worker pass (never inside a request); inbound webhooks HMAC-verified per configured provider and deduplicated by `(provider, event_id)` | SRS Q-4 open; COM-3; `.ai/domain-events-and-outbox.md` §4 "courier API goes through the outbox" |
| **ADR-210** | **Returns are their own root**; BR-10 eligibility from Shop's category class and Inventory's batch status, with staff-confirmed defects; delivery failures use the same path; returned stock always enters quarantine first | FR-ORD-15/16/21 in one path the gates can follow |
| **ADR-211** | **Refunds are recorded by this slice** (`OrderRefundAggregate`) with a Billing credit note in the same save; methods by how the customer paid; `Refunded` status only on full refund of a delivered or returned order; a cancelled paid order stays `Cancelled` with `RefundDue` | US-ORD-15 is an order story; FR-BIL-5 puts the credit note on the invoice. If HO-BIL-001 owns refund records, this is amended (OQ-15) |
| **ADR-212** | **The counter sale is an order** with channel `Counter`, completed in one command through the same pricing, ledger and invoice paths; receipt is data plus a 48-column text rendering; no server-side printer driver | FR-ORD-18 "same inventory ledger"; HW-1 printing belongs to the browser and OS |
| **ADR-213** | **Order messages** are enqueued in command handlers with the change (HO-NTF-001 ADR-057); confirmation, awaiting-transfer, cancellation and refund are `Urgent`; the rest `Normal`; no external URL in any variable | FR-ORD-13; the customer who just acted should not wait for quiet hours |
| **ADR-214** | **Orders are financial records.** Merge reassigns carts, orders and returns; anonymisation redacts the recipient and address snapshot of terminal orders and flags open ones for redaction at their terminal transition; nothing is deleted | D-6, D-9, D-11; HO-ACC-001 ADR-039 |

### 9.1 Open questions

| # | Question | Owner | Default in this build |
|---|---|---|---|
| OQ-1 | Guest checkout, or sign-in required? | Owner | **Sign-in required** (US-ACC-01 "register … to place orders"); guests may build a cart |
| OQ-2 | Pickup "paid in store": confirm at placement (like COD) or only after payment? | Owner | **Confirm at placement, pay at collection** (ADR-203); uncollected → staff mark `Uncollected` → returns path |
| OQ-3 | Guest-cart merge for the same item: larger quantity or sum? | Owner | **Larger quantity** |
| OQ-4 | A transfer that is short or over by bank fees? | Owner / accountant | **Exact amount required**; otherwise refused and handled by contacting the customer |
| OQ-5 | ADR-054 (decimal) vs C-6 / US-ORD-04 (integer minor units) | Architect | ADR-198: compute in minor units, store as ADR-054; recommend ADR-054 references ADR-198. Currencies with ≠ 2 minor digits refused at start-up |
| OQ-6 | Prices tax-inclusive or exclusive? Is delivery taxable? (SRS Q-5) | Accountant | **Exclusive** (FR-ORD-5's formula); delivery **untaxed** -- *amended 2026-10-10 (FU1 D3): delivery tax is configured, default 0 %, changed through the `DELIVERY` tax class (ADR-217)* |
| OQ-7 | Second approval for COD collections and counter tenders above the threshold? | Owner / accountant | **No** — transfer confirmations only; COD and till are reconciled (US-BIL-06) -- *built 2026-10-10 (FU1 D4): COD and counter payments are accepted at once, above the threshold too* |
| OQ-8 | Store credit as a refund method before HO-BIL builds US-BIL-08? | Owner | Refused with `RefundMethodUnavailable` until Billing provides `IStoreCreditLedger` |
| OQ-9 | Refund of the delivery charge; refund for card payments | Owner | Staff decide the amount (suggested = returned goods value, no delivery); card payments refunded as cash or transfer |
| OQ-10 | US-ORD-17 combined invoice — Billing's or Ordering's? | Architect | **HO-BIL-001**, with boarding charges from HO-BRD-001 |
| OQ-11 | Abandoned-cart reminders (FR-NTF-3) have no backlog story | Owner | Not built |
| OQ-12 | Does HO-SHP-001 defer US-SHP-10 AC2/AC3 to this slice? | Coordinator | **Assumed yes**; delivered here |
| OQ-13 | Delivery-slot capacity? | Owner | **None** — slots are preference windows |
| OQ-14 | Anonymous walk-in counter sales without a customer record? | Owner / accountant | Allowed; Billing issues to "Walk-in customer" |
| OQ-15 | Refund records — Ordering (ADR-211) or Billing? | Architect, with HO-BIL-001 | Ordering records, Billing credits; amend if HO-BIL-001 decides otherwise |
| OQ-16 | "Warehouse packer" and "courier coordinator" are not Appendix D roles | Owner | Packing/dispatch: `Reception`, `ShopManager`, `Administrator`; `Inventory` reads pick lists (Appendix D gives it **R** on orders) |
| OQ-17 | A transfer that arrives after the order expired | Owner | Confirmation refused (`OrderNotAwaitingPayment`); staff refund or re-create the order |
| OQ-18 | Carts are audited by HO-ACC-001's interceptor like every aggregate — acceptable volume? | Architect | Yes at launch; the `cart` rate limit bounds it |
| ~~OQ-19~~ | **Closed 2026-09-23 — confirmed as recommended.** HO-IMP-001 §8.8 states it: quarantine disposition (restock, write-off) is an Inventory screen, not an Ordering call, and **reallocation is `ReleaseAsync` + `ReserveAsync` with `ExplicitLines` + `OverrideReason`**, which HO-IMP-001 rule 5 already audits. Ordering's responsibility ends at `ReceiveReturnAsync`. Phase G is unblocked | — | — |

---

## 10. Enumerations, constants and violation codes

### 10.1 Enumerations — `Shared/Common/Enums/`, derived from `Enumeration`, persisted by `Value`

| Type | Members |
|---|---|
| `CartOwnerKind` | `Guest`, `Customer` |
| `CartStatus` | `Active`, `Merged`, `Abandoned` |
| `OrderChannel` | `Online`, `Counter` *(append-only)* |
| `OrderStatus` | `PendingPayment`, `Confirmed`, `Packing`, `Dispatched`, `Delivered`, `Cancelled`, `Returned`, `Refunded` |
| `FulfilmentMethod` | `Delivery`, `Pickup`, `Counter` |
| `OrderPaymentMethod` | `CashOnDelivery`, `BankTransfer`, `PayOnCollection`, `InStore` |
| `TenderMethod` | `Cash`, `InStoreCard` |
| `DeliveryServiceLevel` | `Standard`, `Express` |
| `OrderCancellationReason` | `CustomerRequest`, `StaffDecision`, `PaymentWindowExpired` |
| `DeliveryFailureReason` | `Refused`, `Undeliverable`, `Uncollected` |
| `CollectorKind` | `Courier`, `Staff` |
| `ProductReturnClass` | `Food`, `Litter`, `Other` |
| `ReturnKind` | `CustomerReturn`, `DeliveryFailure` |
| `ReturnStatus` | `Requested`, `Approved`, `Rejected`, `Received`, `Closed` |
| `ReturnReason` | `WrongItem`, `Damaged`, `QualityDefect`, `ChangedMind`, `Recalled`, `Other` |
| `ItemCondition` | `Sealed`, `Opened`, `Used` |
| `ReturnStockDecision` | `Restock`, `WriteOff` |
| `RefundMethod` | `CashInStore`, `BankTransfer`, `StoreCredit` |
| `DiscountKind` | `Percentage`, `FixedAmount` |
| `DiscountScope` | `Order`, `Products`, `Categories` |
| `CheckoutChangeKind` | `PriceIncreased`, `PriceDecreased`, `QuantityReduced`, `NowUnavailable`, `PrescriptionAuthorisationExpired`, `PrescriptionCatRequired` |
| `ReconciliationIssueKind` | `TransferConfirmationFailed`, `PaymentApprovalFailed`, `DeliveryPaymentFailed`, `CounterSaleFailed`, `RefundFailed` |
| `ReconciliationIssueStatus` | `Open`, `Resolved` |

### 10.2 Settings — appended to HO-ADM-001's `SettingDefinitions`

| Key | Group | Type | Default | Bounds | Branch override | SRS |
|---|---|---|---|---|---|---|
| `orders.codEnabled` | Orders | Boolean | true | — | ✅ | FR-ORD-8, Q-3 |
| `orders.codMaxOrderValue` | Orders | Decimal | 5000.00 | 0–1 000 000 | ✅ | BR-15, Q-3 |
| `orders.bankTransferEnabled` | Orders | Boolean | true | — | ✅ | FR-ORD-8 |
| `orders.bankTransferWindowHours` | Orders | Integer | 48 | 1–168 | ❌ | FR-ORD-22, Q-3b |
| `orders.payOnCollectionEnabled` | Orders | Boolean | true | — | ✅ | FR-ORD-8 |
| `orders.pickupCollectionDays` | Orders | Integer | 7 | 1–60 | ✅ | BR-16 |
| `orders.returnWindowDays` | Orders | Integer | 7 | 0–90 | ❌ | FR-ORD-15 |
| `orders.maxQuantityPerLine` | Orders | Integer | 20 | 1–999 | ❌ | — |
| `orders.maxCartLines` | Orders | Integer | 50 | 1–50 | ❌ | — |
| `orders.guestCartRetentionDays` | Orders | Integer | 30 | 1–180 | ❌ | — |

No migration (values are rows).

### 10.3 Other constants

`OrderRules` (`MaxLines = 50`, `MaxQuantityPerLineHardCap = 999`, `MaxReasonLength = 500`,
`MaxStatusHistory = 30`, `OrderNumberFormat`) · `CartRules` (`GuestCookieName = "pc_cart"`,
`TokenBytes = 32`) · `IdempotencyRules` (`HeaderName`, `RetentionHours = 24`, `MaxStoredBodyBytes = 65536`)
· `CurrencyMinorDigits` (ISO table) · `MinorUnits` (value type, `long`) · `OrderMessages` · the twelve
template definitions of §8.12 appended to `NotificationTemplateCatalogue`.

### 10.4 Violation codes — append to `BusinessViolationCodes`

| Group | Codes |
|---|---|
| Cart | `CartNotFound` · `CartFull` · `CartLineNotFound` · `DuplicateCartLine` · `SkuNotPurchasable` · `SkuUnavailable` · `QuantityOutOfRange` · `PrescriptionCatRequired` · `PrescriptionNotAuthorised` · `CatNotOwnedByCaller` *(never returned — `404`)* |
| Checkout | `CheckoutQuoteChanged` · `CartEmpty` · `NothingToPlace` · `PolicyAcceptanceRequired` · `DeliveryAddressNotFound` · `AddressOutsideServedZones` · `ServiceLevelUnavailable` · `DeliverySlotUnavailable` · `PickupBranchUnavailable` · `PaymentMethodUnavailable` · `RecipientPhoneRequired` · `StockNoLongerAvailable` · `DuplicateSubmission` · `IdempotencyKeyRequired` · `IdempotencyKeyReused` · `RequestInProgress` |
| Discounts | `DiscountCodeNotFound` · `DiscountCodeInactive` · `DiscountCodeNotYetValid` · `DiscountCodeExpired` · `DiscountUsageLimitReached` · `DiscountCustomerLimitReached` · `DiscountFirstOrderOnly` · `DiscountNotApplicableToItems` · `DiscountMinimumSpendNotMet` · `DiscountCodeAlreadyExists` · `InvalidDiscountDefinition` |
| Order lifecycle | `OrderNotFound` · `InvalidOrderTransition` · `CancellationNotAvailable` · `CancellationReasonRequired` · `OrderNotAwaitingPayment` · `OrderNotExpirable` · `TransferAmountMismatch` · `PaymentAlreadyAwaitingApproval` · `NoPaymentAwaitingApproval` · `SecondApproverMustDiffer` · `OrderNotFullyAllocated` · `BatchRecalled` · `BatchExpired` · `CourierNotFound` · `CodCollectionRequired` · `CollectionPaymentMismatch` · `TerminalReferenceRequired` · `DeliveryFailureReasonInvalid` · `TendersDoNotCoverTotal` · `ReallocationReasonRequired` |
| Returns & refunds | `ReturnNotFound` · `ReturnWindowClosed` · `ReturnQuantityExceeded` · `ReturnNotEligibleOpenedFood` · `ReturnNotEligibleUsedLitter` · `DefectConfirmationRequired` · `InvalidReturnTransition` · `ReturnReceiptExceedsApproved` · `RefundExceedsAmountPaid` · `RefundMethodNotAllowed` · `RefundMethodUnavailable` · `RefundReferenceRequired` |
| Configuration | `DeliveryZoneNotFound` · `DeliveryZoneNameAlreadyExists` · `InvalidDeliveryZone` · `CourierNameAlreadyExists` · `InvalidBankTransferAccount` · `ReconciliationIssueNotFound` |

Messages are plain and actionable (NFR-USA-4), e.g. `StockNoLongerAvailable`: *"Sorry — 'Royal Canin
Kitten 2 kg' has just sold out. It has been removed from your order; please review and place it again."*

---

## 11. Specified changes — write side

### 11.1 Commands — `Application/Commands/Order/`

Namespace `CatEcommerce.Application.Commands.Order`. Data only; ids minted in the controller;
`ValidateCommand = true` everywhere; the caller comes from `UserContext`, never the body.

| # | Command | Sent by | Phase |
|---|---|---|---|
| C1 | `CreateDeliveryZoneCommand` · `UpdateDeliveryZoneCommand` · `SetDeliveryZoneActivationCommand` | Admin, shop manager | B |
| C2 | `CreateCourierCommand` · `UpdateCourierCommand` (`Name`, `TrackingUrlTemplate?` containing `{tracking}`, https, `SupportsCod`, `IsActive`) | Admin, shop manager | B |
| C3 | `SetBankTransferAccountCommand` (`AccountName`, `AccountNumber`, `BankName`, `BranchName`, `RoutingNumber?`, `IsActive`) | Admin | B |
| C4 | `CreateDiscountCodeCommand` · `UpdateDiscountCodeCommand` · `SetDiscountCodeActivationCommand` | Shop manager, admin | B |
| C5 | `AddCartLineCommand` (`CartId?`, `NewCartId`, `GuestTokenHash?`, `SkuId`, `Quantity`, `CatId?`) | Guest, customer | C |
| C6 | `UpdateCartLineCommand` (`LineId`, `Quantity?`, `CatId?`) · `RemoveCartLineCommand` | Guest, customer | C |
| C7 | `MergeGuestCartCommand` (`GuestTokenHash`) | Customer | C |
| C8 | `PurgeStaleGuestCartsCommand` (one batch of ≤ 200 per call) | Worker | C |
| C9 | `PlaceOrderCommand` (`OrderId`, `IdempotencyKey`, `FulfilmentMethod`, `DeliveryAddressId?`, `ServiceLevel?`, `SlotDate?`, `SlotWindowStart?`, `PickupBranchId?`, `PaymentMethod`, `DiscountCode?`, `RecipientName?`, `RecipientPhone?`, `QuoteFingerprint`) | Customer | D |
| C10 | `SubmitTransferReferenceCommand` (`OrderId`, `Reference`) | Customer | E |
| C11 | `ConfirmBankTransferCommand` (`OrderId`, `ExpectedVersion`, `Amount`, `BankReference`, `ReceivedOn`) | Staff | E |
| C12 | `ApproveHeldPaymentCommand` (`OrderId`, `ExpectedVersion`) · `RejectHeldPaymentCommand` (`OrderId`, `Note`) | Staff (second person) | E |
| C13 | `ExpireUnpaidOrderCommand` (`OrderId`) | Worker | E |
| C14 | `CancelOrderCommand` (`OrderId`, `Reason?`, `Note?`) | Customer, staff | E |
| C15 | `ReorderCommand` (`SourceOrderId`) | Customer | E |
| C16 | `StartPackingCommand` · `DispatchOrderCommand` (`OrderId`, `CourierId`, `TrackingNumber?`) · `MarkReadyForCollectionCommand` (`OrderId`) | Staff | F |
| C17 | `RecordTrackingNumberCommand` (`OrderId`, `TrackingNumber`) | Staff | F |
| C18 | `RecordDeliveryCommand` (`OrderId`, `ExpectedVersion`, `DeliveredOn`, `CodCollection?` (`Amount`, `CollectorKind`, `CollectorName?`, `CollectorStaffId?`, `CollectedOn`), `CollectionPayment?` (`TenderMethod`, `Amount`, `Reference?`)) | Staff | F |
| C19 | `RecordDeliveryFailureCommand` (`OrderId`, `ReturnRequestId`, `Reason`, `Note?`) | Staff | F |
| C20 | `ApplyCourierTrackingEventCommand` (`Provider`, `EventId`, `TrackingNumber`, `Status`, `OccurredOn`) | Webhook | F |
| C21 | `ReallocateOrderLineCommand` (`OrderId`, `LineId`, `TargetBatchId`, `Reason`) | Staff | F |
| C22 | `RequestReturnCommand` (`ReturnRequestId`, `OrderId`, `Lines[]`) · `ApproveReturnCommand` (`Lines[]` with `DefectConfirmed`, `DefectNote?`) · `RejectReturnCommand` (`Note`) | Customer / staff | G |
| C23 | `ReceiveReturnCommand` (`ReturnRequestId`, `Lines[] (OrderLineId, QuantityReceived)`) | Staff | G |
| ~~C24~~ *Removed 2026-10-09 -- disposition is Inventory's quarantine workflow (OQ-19); see the Amended (gate fix 3) row.* | `DecideReturnedStockCommand` (`ReturnRequestId`, `Decisions[] (OrderLineId, Decision, Reason)`) | Inventory officer, shop manager | G |
| C25 | `IssueRefundCommand` (`RefundId`, `OrderId`, `ReturnRequestId?`, `Method`, `Amount`, `Reference?`, `Reason`) | Accountant, shop manager | G |
| C26 | `CompleteCounterSaleCommand` (`OrderId`, `CustomerId?`, `Lines[] (SkuId, Quantity, CatId?)`, `DiscountCode?`, `Tenders[] (Method, Amount, Reference?)`, `CashTendered?`) | Staff | H |
| C27 | `ResolveReconciliationIssueCommand` (`IssueId`, `Note`) | Accountant | E |

### 11.2 Validators — `CommandHandlers/Validators/` (flat), registered as `IValidator<T>`

Shape only: ids non-empty; quantities 1…999; amounts > 0 with ≤ 2 decimals and a known currency; text
lengths; enum membership; `Idempotency` key a UUID; `ReceivedOn`/`DeliveredOn`/`CollectedOn` not more than
5 minutes in the future; `Lines` non-empty and unique by line id; discount definition shape (kind ↔
percent/amount present). **Availability, eligibility, zone, payment availability, transitions and
authorisations are aggregate or handler rules.**

### 11.3 Mappers and DTOs

`OrderMapper`, `CartMapper`, `ReturnMapper`, `DeliveryZoneMapper`, `DiscountCodeMapper` (static
`ToDomainDto()` extensions). DTOs in `Domain/Models/`: `PlaceOrderDto`, `OrderLineDto`,
`AllocationDto`, `FulfilmentDetailsDto`, `CheckoutInputs`, `CheckoutQuote`, `SkuSnapshot`,
`ReservationRequest`, `InvoiceRequest`, `PaymentRecordRequest`, `ReturnLineDto`, `CounterSaleDto`,
`ZoneDto`, `DiscountDefinitionDto`.

### 11.4 Command handlers — `CommandHandlers/Order/`

**`OrderPlacement`** (Application, a static helper over the ports — not a service layer, the same shape
as HO-APT-001's `SlotClaim`) implements the steps shared by C9, C26 and HO-SUB-001's future command:
load inputs → price → create order → reserve/issue → confirm-with-invoice when applicable → enqueue.

| Handler | Specifics | Saved root · tracked · staged |
|---|---|---|
| **C1–C4** | Uniqueness pre-checks (zone name, courier name, code) with the unique index behind each; zone branch exists and is active; discount `ProductIds`/`CategoryIds` exist in Shop | one root |
| **C5 Add line** | Resolve the cart: customer → their `Active` cart (create if none); guest → by token hash (create with `NewCartId` if none — the controller then sets the cookie). SKU purchasable (`SkuNotPurchasable`); sellable ≥ 1 (`SkuUnavailable`); Rx SKU → customer session **and** `CatId` owned by the caller (`404` otherwise) **and** `IsAuthorisedAsync` (`PrescriptionNotAuthorised` — *"This food needs a current veterinary authorisation for the cat. Book a consultation to get one."*) — **US-SHP-10 AC2**; guests cannot add Rx SKUs (`PrescriptionCatRequired`) | cart |
| **C6** | Own cart only; `CatId` change re-checks ownership and authorisation | cart |
| **C7 Merge** | Guest cart by hash, `Active` → `MergeFrom`; missing or already merged → success, nothing written (idempotent). The customer cart is created if none | customer cart · tracked guest cart |
| **C9 Place** | §8.7 in order, then `OrderPlacement`: order number from the sequence; `Place`; `ReserveAsync` → `RecordAllocations`; discount → `Redeem` on the tracked code; cart `RemovePlacedLines`; COD / pay-on-collection → `IssueForOrderAsync` + `Confirm` + enqueue `order.confirmed`; transfer → `AwaitTransfer(now + window)` + enqueue `order.awaiting-transfer`; `SaveAsync(order)` | order · cart, discount code · reservation, invoice, notification |
| **C10** | Own order (`404`), `PendingPayment`, transfer | order |
| **C11 Confirm transfer** | Staff; `ExpectedVersion`; `PendingPayment`, transfer, no hold (`OrderNotAwaitingPayment`, `PaymentAlreadyAwaitingApproval`); amount == `GrandTotal` (`TransferAmountMismatch`). Amount > `billing.paymentApprovalThreshold` → `HoldPaymentForApproval` and save — response `awaitingApproval: true`. Else invoice → payment (`BankTransfer`, reference, received on, recorded by caller) → `ConfirmPayment` → enqueue `order.confirmed`. **ADR-205 wrapper** | order · — · invoice, payment, notification |
| **C12 Approve / reject** | Approve: hold present (`NoPaymentAwaitingApproval`); caller ≠ `HeldPayment.RecordedBy` (`SecondApproverMustDiffer`); then as C11's final branch with `ApprovedBy` = caller. Reject: note required; clears the hold. **ADR-205 wrapper** on approve | order · — · invoice, payment, notification |
| **C13 Expire** | `Expire(now)`; `ReleaseReservationAsync`; discount released; enqueue `order.expired`. Not expirable → success, no write (the worker may race a confirmation) | order · discount code · reservation release, notification |
| **C14 Cancel** | Customer: own order (`404`). Staff: `Orders.Cancel`, reason required. `Cancel`; release reservation; release discount; invoice exists → `ICreditNoteIssuer.IssueAsync(full)` and record the credit note; enqueue `order.cancelled` or `order.cancelled-refund-due` | order · discount code · release, credit note, notification |
| **C15 Re-order** | Own order; for each line: purchasable, sellable, Rx authorised for the line's cat → `AddLine` to the active cart at the current price; others listed with a reason. No order is created | cart |
| **C16** | `StartPacking` / `Dispatch` (courier active; BR-14 check §8.9; `ConvertReservationToIssueAsync`; tracking URL from the courier template; enqueue `order.dispatched`) / `MarkReadyForCollection` (BR-14 check; `CollectByDate = today + orders.pickupCollectionDays`; enqueue `order.ready-for-collection`) | order · — · issue, notification |
| **C17** | Sets tracking; enqueue `order.tracking-added` only when the number was previously empty | order |
| **C18 Delivery / collection** | COD: collection required; payment `CashOnDelivery` with collector and the tracking number as reference. Pay-on-collection: `CollectionPayment` required, equal to `GrandTotal − AmountPaid`; `InStoreCard` needs the slip reference; pickup → `ConvertReservationToIssueAsync`. Enqueue `order.delivered`. **ADR-205 wrapper** when a payment is included | order · — · payment, issue (pickup), notification |
| **C19 Delivery failure** | `RecordDeliveryFailure`; create the `DeliveryFailure` return (`Approved`, all lines) as a **tracked new root added through the return repository's staging** — see note; COD → full credit note; enqueue `order.delivery-failed` | order · new return · credit note, notification |
| **C20 Tracking event** | Inbox row inserted first (duplicate → success); order by tracking number (unknown → success, logged at `Information`); `ApplyTrackingEvent`. A `delivered` event does **not** mark the order delivered — COD money must be recorded by a person (US-ORD-20 AC2) — it sets `LastTrackingStatus` and the staff queue highlights it | order |
| **C21 Reallocate** | Reason required; `ReallocateAsync`; `Confirmed` or `Packing` only; audited `BusinessEvent` with the reason | order · — · reallocation |
| **C22 Returns** | Request: own order (customer) or `Returns.Manage` (staff on the customer's behalf); `ReturnEligibilityPolicy` with Inventory batch status. Approve: every `RequiresDefectConfirmation` line has `DefectConfirmed` + note (`DefectConfirmationRequired`); enqueue `order.return-decided`. Reject: note | return |
| **C23 Receive** | `Approved`; quantities ≤ approved; `ReceiveIntoQuarantineAsync`; the order (tracked) `RegisterReturnedQuantity` per line; status `Received` | return · order · quarantine receipt |
| ~~**C24 Decide**~~ *Removed 2026-10-09 -- disposition is Inventory's quarantine workflow (OQ-19); see the Amended (gate fix 3) row.* | `Received`; every decision has a reason; restock → `RestockFromQuarantineAsync(original batch)`, write-off → `WriteOffFromQuarantineAsync`; all decided → `Closed` | return · — · restock / write-off |
| **C25 Refund** | `RefundMethodPolicy` (§8.6); reference rules; `ApplyRefund` on the tracked order; credit note (or link the cancellation's full credit note); store credit via the optional port; enqueue `order.refund-issued`. **ADR-205 wrapper** | refund · order · credit note, store credit, notification |
| **C26 Counter sale** | §8.11 via `OrderPlacement`; active branch from `IActiveBranchAccessor`; `IssueDirectAsync`; invoice; one payment per tender; `CompleteCounterSale`. **ADR-205 wrapper** | order · discount code · issue, invoice, payments |
| **C27 Resolve issue** | `Open` → `Resolved` with note | issue |

> **Staging a new root (C19).** A new `ReturnRequestAggregate` in the same save as the order is added
> with `IAggregateRootRepository<ReturnRequestAggregate>`… **not** `SaveAsync` (which commits
> immediately), but by `IOrderingStaging.Add(aggregate)` — a two-line Infrastructure port that calls
> `db.Add(...)` without saving, exactly as `EfNotificationOutbox` does. Its creation event is not
> dispatched (V2) and nothing subscribes to it. This is the only new-root staging in the slice.

### 11.5 Domain events — `Events/Order/`

Ids, instants, enums, booleans and `Money` only — no names, addresses, phone numbers, references, notes
or reasons as text (HO-ACC-001 U-X1 covers the assembly).

`OrderPlacedEvent` (`OrderId`, `CustomerId?`, `BranchId`, `Channel`, `PaymentMethod`,
`FulfilmentMethod`, `Status`, `GrandTotal`) · `OrderConfirmedEvent` (`OrderId`, `InvoiceId`) ·
`OrderTransferReferenceSubmittedEvent` · `OrderPaymentHeldForApprovalEvent` ·
`OrderHeldPaymentRejectedEvent` · `OrderCancelledEvent` (`Reason`, `WasPaid`, `ByCustomer`) ·
`OrderPackingStartedEvent` · `OrderDispatchedEvent` (`CourierId`, `HasTracking`) ·
`OrderReadyForCollectionEvent` · `OrderDeliveredEvent` (`HadCodCollection`) ·
`OrderDeliveryFailedEvent` (`Reason`) · `OrderReturnedEvent` · `OrderRefundedEvent` ·
`OrderLineReallocatedEvent` (`LineId`, `FromBatchId`, `ToBatchId`) · `CounterSaleCompletedEvent` ·
`ReturnRequestedEvent` · `ReturnApprovedEvent` · `ReturnRejectedEvent` · `ReturnReceivedEvent` ·
`ReturnedStockDecidedEvent` (`OrderLineId`, `Decision`) · `RefundIssuedEvent` (`RefundId`, `Method`,
`Amount`) · `DeliveryZoneChangedEvent` · `DiscountCodeChangedEvent` · `ReconciliationIssueRaisedEvent`.

**Contracts for later areas:** `OrderPlacedEvent`, `OrderConfirmedEvent`, `OrderCancelledEvent`,
`OrderDeliveredEvent` (HO-SUB-001, HO-RPT-001, HO-POR-001).

### 11.6 Domain event handlers — `DomainEventHandlers/Order/`

| Handler | Handles | Does |
|---|---|---|
| `ReassignOrderingOnCustomerMergedHandler` | HO-ACC-001 `CustomerMergedEvent` | The duplicate's active cart merged into the survivor's (or re-keyed if the survivor has none); every order and return → the survivor. Idempotent |
| `RedactOrdersOnCustomerAnonymisedHandler` | HO-ACC-001 `CustomerAccountAnonymisedEvent` | Terminal orders: recipient name → `"Anonymised customer"`, phone and address lines → `"[redacted]"` (district kept for reporting). Open orders: `CustomerAnonymised = true`; the aggregate redacts on its terminal transition. Active cart abandoned. Idempotent (ADR-214) |
| `AuditOrderMoneyEventsHandler` | `OrderConfirmedEvent`, `OrderPaymentHeldForApprovalEvent`, `OrderDeliveredEvent` (with COD), `RefundIssuedEvent`, `CounterSaleCompletedEvent`, `OrderLineReallocatedEvent` | A `BusinessEvent` audit row naming the actor — BR-17 attributable money; C-9 |

All loop with one `UpdateAsync` per row and **never enqueue** (HO-NTF-001 ADR-057). Data changes are
audited by HO-ACC-001's interceptor.

---

## 12. Specified changes — Infrastructure, read side, Api, host

### 12.1 Persistence

Nine `DbSet`s (`Carts`, `Orders`, `ReturnRequests`, `OrderRefunds`, `DiscountCodes`, `DeliveryZones`,
`Couriers`, `BankTransferAccounts`, `ReconciliationIssues`) plus two plain entities
(`IdempotencyRecords`, `InboundWebhookReceipts`). One configuration per root; schema `ordering` (`ops`
for the two plain entities); HO-ACC-001 §12.1 / F15 / F16 conventions; `Money` as `amount numeric(12,2)`
+ `currency_code char(3)`; list-valued fields as `jsonb`.

| Index / constraint | Kind |
|---|---|
| `cart (customer_id)` **UNIQUE** filtered `WHERE status = Active AND customer_id IS NOT NULL` | `ix_cart_active_customer` |
| `cart (guest_token_hash)` **UNIQUE** filtered not null | `ix_cart_guest_token` |
| `cart (owner_kind, status, last_activity_on)` | guest purge |
| `sales_order (order_number)` **UNIQUE** | `ix_sales_order_number` |
| `sales_order (customer_id, idempotency_key)` **UNIQUE** filtered not null | `ix_sales_order_customer_idempotency` |
| `sales_order (customer_id, placed_on)` | "my orders", first-order check |
| `sales_order (branch_id, status, placed_on)` | staff queue |
| `sales_order (status, payment_method, payment_due_by)` | expiry pass, awaiting-payment list |
| `sales_order (discount_code_id, customer_id)` filtered not null | per-customer usage |
| `sales_order (shipment_tracking_number)` filtered not null | webhook lookup |
| `order_line_allocation (batch_id)` | **recall trace** (D-4) |
| `return_request (order_id)`, `(status, created_date)` | |
| `order_refund (order_id)` | |
| `discount_code (code)` **UNIQUE** | `ix_discount_code_code` |
| `delivery_zone (name)` **UNIQUE**; `courier (name)` **UNIQUE** | |
| `reconciliation_issue (status, raised_on)` | |
| `ops.idempotency_record (scope, key)` **UNIQUE**, `(created_on)` | ADR-202 |
| `ops.inbound_webhook_receipt (provider, event_id)` **UNIQUE** | ADR-209 |
| sequence `ordering.order_number_seq` | raw SQL (V1) |

**Migrations**, each read before commit: `AddOrderingEdgeInfrastructure` (A — `ops` tables),
`AddOrderingConfiguration` (B — zones, couriers, bank account, discount codes), `AddOrderingCart` (C),
`AddOrderingOrders` (D — orders, lines, allocations, history, payments, sequence),
`AddOrderingReconciliationIssues` (E), `AddOrderingReturnsAndRefunds` (G). Phases F and H add no
migration.

### 12.2 Infrastructure services and the worker

| Class | Notes |
|---|---|
| `Ordering/CheckoutInputsLoader : ICheckoutInputsLoader` | §8.14; `AsNoTracking` except the cart and discount code, which the placement handler mutates |
| `Ordering/GuestCartTokens : IGuestCartTokens` | `RandomNumberGenerator`, SHA-256 |
| `Ordering/OrderingStaging : IOrderingStaging` | `db.Add` without saving (C19 only) |
| `Ordering/ReconciliationIssueRecorder : IReconciliationIssueRecorder` | `IServiceScopeFactory` → new scope → repository `SaveAsync`; never throws out |
| `Ordering/OrderNumberGenerator` | `nextval` through the scoped context |
| `Ordering/CustomerDirectory : ICustomerDirectory`, `CustomerPolicyStatus : ICustomerPolicyStatus` | Read-only over `account.*` / `admin.*` |
| `Notifications/NotificationLinkBuilder` | `OrderLink` added — **the only edit to HO-NTF-001 code** |

**`OrderWorker : BackgroundService`** (Api, `Background/`) — HO-NTF-001's `NotificationWorker` shape (a
scope per unit of work, never throws out of `ExecuteAsync`, disabled in `Testing`, `OrderWorkCycle`
callable by tests). Every 60 s: expire unpaid transfer orders (`ExpireUnpaidOrderCommand`, one per
order), purge stale guest carts (C8), purge idempotency records older than 24 h. Configuration
`Orders:Worker:Enabled`, `Orders:Worker:IntervalSeconds` (60).

### 12.3 Authorization

Appendix D rows "Orders & fulfilment" (Customer R own, Reception W, Inventory R, Shop Mgr F,
Accountant R, Admin F), "Invoices & payments" (Reception W, Shop Mgr W, Accountant F, Admin F) and
"Refunds & credit notes" (Shop Mgr W, Accountant F, Admin F). Appended to the one `ActionRoles` table:

| Action | Staff roles | Endpoints |
|---|---|---|
| `Orders.View` | Reception, Inventory, ShopManager, Accountant, Administrator | queue, detail, pick list, packing slip, returns list, customer order panel |
| `Orders.Fulfil` | Reception, ShopManager, Administrator | packing, dispatch, ready-for-collection, tracking, delivery failure, manifest |
| `Orders.RecordDelivery` | Reception, ShopManager, Administrator | delivery / collection (records COD or collection money) |
| `Orders.ConfirmPayment` | Reception, ShopManager, Accountant, Administrator | transfer confirmation, awaiting-payment list |
| `Orders.ApprovePayment` | ShopManager, Accountant, Administrator | approve / reject a held payment (different person enforced in the handler) |
| `Orders.Cancel` | Reception, ShopManager, Administrator | staff cancel |
| `Orders.Reallocate` | Inventory, ShopManager, Administrator | FEFO override |
| `Orders.Reconcile` | Accountant, ShopManager, Administrator | reconciliation issues |
| `Returns.Manage` | Reception, ShopManager, Administrator | request on behalf, approve, reject, receive |
| ~~`Returns.DecideStock`~~ *Removed 2026-10-09 -- disposition is Inventory's quarantine workflow (OQ-19); see the Amended (gate fix 3) row.* | Inventory, ShopManager, Administrator | restock / write-off |
| `Refunds.Issue` | ShopManager, Accountant, Administrator | refunds, refunds-due list |
| `Pos.Sell` | Reception, ShopManager, Administrator | POS lookup, sale, receipt |
| `DeliveryZones.View` / `.Manage` | ShopManager, Accountant, Administrator / ShopManager, Administrator | zones |
| `Couriers.Manage` | ShopManager, Administrator | couriers |
| `Discounts.View` / `.Manage` | ShopManager, Accountant, Administrator / ShopManager, Administrator | discount codes |
| `Orders.ManagePaymentSettings` | Administrator | bank transfer account |

Customer endpoints carry `[RequireCustomerSession]` and resolve every order, return and cart **by id and
caller** — someone else's is **`404`**. Cart endpoints are `[PublicEndpoint]` (guest or customer).
The webhook is `[PublicEndpoint]` with signature verification. `EndpointAccessConventionTests`
(HO-ADM-001) must pass.

**Rate limits** (HO-ACC-001 ADR-030 shape): `cart` 120/min per IP on cart writes; `checkout` 10/min per
IP on placement and quote (NFR-SEC-6 "checkout endpoints"); `courier-webhook` 600/min per IP.

**Maintenance mode**: `[StorefrontEndpoint]` on every cart endpoint, quote and placement (HO-ADM-001
§4.2). Portal order reads, cancel, returns and every staff and webhook endpoint are **not** gated.

### 12.4 Api — the contract HO-ORD-002 consumes

JSON camelCase; instants ISO-8601 with offset; local dates `yyyy-MM-dd`; **money as HO-ADM-001 §12.4**
(`{ "amount": "1250.00", "currencyCode": "BDT" }`, amount a string). Every order view carries
`branchTimeZoneId`. Money-recording and placement endpoints require `Idempotency-Key` (§8.13).

**Cart — guest or customer**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/cart` | public · storefront | `{ cartId?, lines: [{ lineId, skuId, productId, productName, variantLabel, imagePath?, quantity, cat?: { id, name }, unitPrice, priceWhenAdded, priceChanged, availability: "InStock"\|"Limited"\|"Unavailable", availableQuantity?, requiresPrescription, prescriptionStatus?: "Authorised"\|"NotAuthorised"\|"CatRequired", lineSubtotal }], itemCount, subtotal, maxQuantityPerLine, guestCartPending }` — `guestCartPending` true when a customer session **and** a live guest cookie are both present. No cookie and no session → empty cart, **no cookie set** |
| `POST` | `/api/cart/lines` | public · storefront · `cart` | C5 → `201 { lineId, cartId }` (+ `Set-Cookie: pc_cart` when a guest cart was created); `400 SkuUnavailable` / `PrescriptionNotAuthorised` / `CartFull` |
| `PUT` | `/api/cart/lines/{lineId}` | public · storefront · `cart` | C6 → `204` |
| `DELETE` | `/api/cart/lines/{lineId}` | public · storefront · `cart` | C6 → `204` |
| `POST` | `/api/customers/me/cart/merge` | customer | C7 → `200 { mergedLineCount, droppedLines: [{ productName, reason }] }` + expired `pc_cart` |

**Checkout and orders — customer**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/customers/me/checkout/quote?fulfilment&addressId&serviceLevel&slotDate&slotWindowStart&pickupBranchId&paymentMethod&discountCode` | customer · storefront · `checkout` | `CheckoutQuote`: `lines[]` (skuId, productName, variantLabel, cat?, quantity, unitPrice, lineSubtotal, lineDiscount, taxRatePercent, taxAmount, lineTotal), `excludedLines[]`, `changes[]` (`kind`, `lineRef`, `productName`, `before?`, `after?`, `message`), `delivery` (`available`, `unavailableReason?`, `zoneName?`, `serviceLevels[]` (level, charge, leadTimeDays, `slots[]` (date, windowStart, windowEnd, label))), `pickupBranches[]` (id, name, address, phone), `paymentOptions[]` (`method`, `available`, `unavailableReason?`, `message?`), `discount?` (`code`, `applied`, `reasonCode?`, `message?`), `totals` (subtotal, lineDiscountTotal, orderDiscount, deliveryCharge, taxTotal, grandTotal), `pendingPolicies[]` (key, versionNumber, title), `canPlace`, `blockingReasons[]`, `fingerprint` |
| `POST` | `/api/customers/me/orders` | customer · storefront · `checkout` · **idempotent** | C9 → `201 { orderId, orderNumber, status, grandTotal, paymentDueBy?, transferInstructions? }`; `409 CheckoutQuoteChanged` / `StockNoLongerAvailable` / `RequestInProgress`; `400` §8.7 codes |
| `GET` | `/api/customers/me/orders?status&pageNumber&pageSize` | customer | paged: `id`, `orderNumber`, `placedOn`, `status`, `fulfilmentMethod`, `paymentMethod`, `grandTotal`, `itemCount`, `branchTimeZoneId` |
| `GET` | `/api/customers/me/orders/{orderId}` | customer | detail: lines (with cat), totals, fulfilment, `statusHistory[]` (to, changedOn — no staff names), `paymentDueBy?`, `transferInstructions?` (account name, number, bank, branch, routing, `paymentReference`, `amountDue`) while `PendingPayment`, `customerTransferReference?`, `shipment?` (courierName, trackingNumber?, trackingUrl?, lastTrackingStatus?), `collectBy?`, `invoiceNumber?`, `amountPaid`, `amountRefunded`, `refundDue`, `refunds[]` (method, amount, issuedOn), `returns[]` (id, status), `canCancel`, `canRequestReturn`, `returnWindowEndsOn?`, `returnableLines[]` (lineId, maxQuantity, returnClass), `canReorder` |
| `POST` | `/api/customers/me/orders/{orderId}/transfer-reference` | customer | C10 → `204` |
| `POST` | `/api/customers/me/orders/{orderId}/cancel` | customer | C14 → `204`; `400 CancellationNotAvailable` |
| `POST` | `/api/customers/me/orders/{orderId}/reorder` | customer · **idempotent** | C15 → `200 { addedCount, unavailable: [{ productName, reason }] }` |
| `POST` | `/api/customers/me/orders/{orderId}/returns` | customer | C22 → `201 { returnRequestId, status }`; `400` with per-line `propertyName` `lines[i]` |
| `GET` | `/api/customers/me/returns/{returnRequestId}` | customer | lines, status, decision note |

**Staff** (branch from `X-Branch-Id`, HO-ADM-001 §8.3)

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` | `/api/staff/orders?status&fulfilment&paymentMethod&channel&from&to&search&pageNumber&pageSize` | `Orders.View` | paged queue; `search` matches order number, customer name, phone; each row: id, number, placedOn, customer, status, fulfilment, payment method, total, `ageMinutes`, `flags[]` (`AwaitingApproval`, `TransferReported`, `CourierReportsDelivered`, `CollectionOverdue`, `CustomerAnonymised`) |
| `GET` | `/api/staff/orders/{orderId}` | `Orders.View` | full detail incl. `version`, staff names in `statusHistory`, allocations per line (batch number, expiry, location), `heldPayment?`, `codCollection?`, payments, credit notes, `allowedActions[]` (from `OrderTransitionRules` and the caller's actions) |
| `GET` | `/api/staff/orders/awaiting-payment` | `Orders.ConfirmPayment` | transfer orders: number, customer, amount, `paymentDueBy`, `customerTransferReference?`, `heldPayment?` |
| `POST` | `/api/staff/orders/{orderId}/payment/bank-transfer` | `Orders.ConfirmPayment` · **idempotent** | C11 → `200 { status, awaitingApproval }` |
| `POST` | `/api/staff/orders/{orderId}/payment/approve` · `/reject` | `Orders.ApprovePayment` · approve **idempotent** | C12 → `200 { status }` / `204` |
| `POST` | `/api/staff/orders/{orderId}/cancel` | `Orders.Cancel` | C14 → `204` |
| `POST` | `/api/staff/orders/{orderId}/packing` | `Orders.Fulfil` | C16 → `204` |
| `POST` | `/api/staff/orders/{orderId}/dispatch` | `Orders.Fulfil` | C16 → `204`; `400 BatchRecalled` / `BatchExpired` / `OrderNotFullyAllocated` |
| `POST` | `/api/staff/orders/{orderId}/ready-for-collection` | `Orders.Fulfil` | C16 → `204` |
| `PUT` | `/api/staff/orders/{orderId}/tracking` | `Orders.Fulfil` | C17 → `204` |
| `POST` | `/api/staff/orders/{orderId}/delivery` | `Orders.RecordDelivery` · **idempotent** | C18 → `204` |
| `POST` | `/api/staff/orders/{orderId}/delivery-failure` | `Orders.Fulfil` | C19 → `201 { returnRequestId }` |
| `POST` | `/api/staff/orders/{orderId}/lines/{lineId}/reallocate` | `Orders.Reallocate` | C21 → `204` |
| `GET` | `/api/staff/fulfilment/pick-list?orderIds` | `Orders.View` | `{ generatedOn, groups: [{ locationId, locationName, batches: [{ batchId, batchNumber, expiresOn, skuId, productName, variantLabel, quantity, orders: [{ orderNumber, quantity }] }] }] }` — grouped by location, then batch (US-ORD-10 AC1). Without `orderIds`: every `Confirmed` and `Packing` order at the branch |
| `GET` | `/api/staff/orders/{orderId}/packing-slip` | `Orders.View` | order number, placed on, customer name, recipient, phone, address or pickup branch, slot, lines with quantity, cat name and batch number, COD amount due, notes (US-ORD-10 AC2) |
| `GET` | `/api/staff/fulfilment/manifest.csv?courierId&date` | `Orders.Fulfil` | `text/csv`: order number, recipient, phone, address, district, postal code, parcels (1), weight (g), COD amount (0 when prepaid), tracking number; orders dispatched with that courier on that branch-local date; cells beginning `= + - @` prefixed with `'` (HO-ADM-001 ADR-051) |
| `GET` | `/api/staff/orders/refunds-due` | `Refunds.Issue` | orders with `refundDue > 0` or received returns without a refund |
| `POST` | `/api/staff/orders/{orderId}/refunds` | `Refunds.Issue` · **idempotent** | C25 → `201 { refundId, creditNoteId }`; `400 RefundMethodNotAllowed` / `RefundExceedsAmountPaid` / `RefundMethodUnavailable` |
| `GET` | `/api/staff/orders/{orderId}/refund-options` | `Refunds.Issue` | allowed methods with availability, `maxRefundable`, `suggestedAmount` per open return |
| `GET` | `/api/staff/returns?status&pageNumber&pageSize` · `/{returnRequestId}` | `Orders.View` | list / detail with eligibility notes, batch status, `requiresDefectConfirmation` per line |
| `POST` | `/api/staff/orders/{orderId}/returns` | `Returns.Manage` | C22 → `201` |
| `POST` | `/api/staff/returns/{id}/approve` · `/reject` · `/receive` | `Returns.Manage` | C22 / C23 → `204` |
| ~~`POST`~~ | ~~`/api/staff/returns/{id}/decisions`~~ | ~~`Returns.DecideStock`~~ | ~~C24 → `204`~~ *Removed 2026-10-09 -- disposition is Inventory's quarantine workflow (OQ-19); see the Amended (gate fix 3) row.* |
| `GET` | `/api/staff/orders/reconciliation-issues?status` · `POST …/{id}/resolve` | `Orders.Reconcile` | list / C27 → `204` |
| `GET` | `/api/staff/customers/{customerId}/orders` | `Orders.View` | the customer's recent orders (for HO-ACC-002's customer page) |
| `GET` | `/api/staff/pos/sku?barcode` | `Pos.Sell` | `{ sku: SkuSnapshot fields, unitPrice, sellableAtCounter, fefoBatch?: { batchNumber, expiresOn }, requiresPrescription }`; `404` unknown barcode |
| `GET` | `/api/staff/pos/sku-search?q` | `Pos.Sell` | ≤ 20 sellable SKUs |
| `POST` | `/api/staff/pos/quote` | `Pos.Sell` | the counter quote (lines, discount, tax, totals) — a query with a body, no state change |
| `POST` | `/api/staff/pos/sales` | `Pos.Sell` · **idempotent** | C26 → `201 { orderId, orderNumber, invoiceNumber, changeDue }` |
| `GET` | `/api/staff/pos/sales/{orderId}/receipt` | `Pos.Sell` | structured receipt + `text` (48 columns) |

**Administration**

| Method | Route | Access | Command / query → success |
|---|---|---|---|
| `GET` / `POST` | `/api/admin/delivery-zones` | `DeliveryZones.View` / `.Manage` | list / C1 → `201 { zoneId }` |
| `GET` / `PUT` | `/api/admin/delivery-zones/{zoneId}` | same | detail / C1 → `204` |
| `PUT` | `/api/admin/delivery-zones/{zoneId}/activation` | `.Manage` | `204` |
| `GET` | `/api/admin/delivery-zones/resolve?district&postalCode` | `.View` | which zone an address falls in — for the admin's test tool |
| `GET` / `POST` / `PUT` | `/api/admin/couriers` (· `/{id}`) | `Couriers.Manage` (GET also `Orders.Fulfil`) | C2 |
| `GET` / `PUT` | `/api/admin/bank-transfer-account` | `Orders.ManagePaymentSettings` | C3 → `204` |
| `GET` / `POST` / `PUT` | `/api/admin/discount-codes` (· `/{id}`, `/{id}/activation`) | `Discounts.View` / `.Manage` | list with `timesRedeemed` / C4 |

**Webhook**

| Method | Route | Access | → |
|---|---|---|---|
| `POST` | `/api/webhooks/couriers/{provider}` | public · signature · `courier-webhook` | `200` (accepted or duplicate); `401` bad signature; `404` unknown provider |

Every write uses HO-ACC-001's `400` / `401` / `403` / `409` shapes (R1–R8), plus `422
IdempotencyKeyReused`.

### 12.5 Filters

- **`IdempotencyFilter`** (§8.13) — new, `[Idempotent]` attribute.
- **`UniqueConstraintExceptionFilter`** (HO-ACC-001) — append: `ix_discount_code_code` → `400
  DiscountCodeAlreadyExists`; `ix_delivery_zone_name` → `400 DeliveryZoneNameAlreadyExists`;
  `ix_courier_name` → `400 CourierNameAlreadyExists`; `ix_sales_order_customer_idempotency` → `409
  DuplicateSubmission`; `ix_cart_active_customer` → `409` with *"Your cart changed on another device —
  please retry."*; **Inventory's documented reservation constraint(s)** → `409 StockNoLongerAvailable`
  (§8.14; names confirmed at V3). Unknown constraints rethrow.
- **`ConcurrencyExceptionFilter`** unchanged — except that on the **placement** route a
  `DbUpdateConcurrencyException` is answered `409 CheckoutQuoteChanged` (a discount or stock counter moved
  under the quote), via an endpoint-metadata override, so the client re-quotes instead of "reload".

### 12.6 Read side — `src/Read/`

Queries in `Queries/Order/`, handlers in `QueryHandlers/Order/`, ViewModels flat; explicit projections,
`AsNoTracking()`.

- `GetCartQuery` — availability by joining `inventory.v_sellable_stock` (advisory), Rx status through
  `IDietAuthorisationGate`.
- `GetCheckoutQuoteQuery` — `ICheckoutInputsLoader` + `CheckoutPricing`. **Never prices in SQL.**
- `GetCustomerOrdersQuery`, `GetCustomerOrderDetailQuery` (`canCancel`, `canRequestReturn` from the same
  rules the commands use), `GetCustomerReturnQuery`.
- `GetStaffOrderQueueQuery`, `GetStaffOrderDetailQuery`, `GetAwaitingPaymentOrdersQuery`,
  `GetPickListQuery`, `GetPackingSlipQuery`, `GetCourierManifestQuery` (CSV streamed),
  `GetRefundsDueQuery`, `GetRefundOptionsQuery`, `GetReturnsQuery`, `GetReturnDetailQuery`,
  `GetReconciliationIssuesQuery`, `GetCustomerOrdersForStaffQuery`.
- `GetPosSkuQuery`, `SearchPosSkusQuery`, `GetCounterQuoteQuery`, `GetReceiptQuery` (`ReceiptTextRenderer`).
- `GetDeliveryZonesQuery`, `ResolveDeliveryZoneQuery`, `GetCouriersQuery`, `GetBankTransferAccountQuery`,
  `GetDiscountCodesQuery`.
- Worker queries: `GetExpirableOrdersQuery`, `GetStaleGuestCartsQuery`.

### 12.7 Host — `Program.cs`

Register every handler, validator (as `IValidator<T>`), query handler and event handler in §11–§12.6 —
count them (HO-ACC-001 I-A1); the pure domain services (Singleton — stateless); the Infrastructure ports
(Scoped; `ReconciliationIssueRecorder` Scoped, creating its own inner scope); `IDbContextFactory` for the
idempotency filter; the twelve templates via the
catalogue; `OrderWorkCycle` (Scoped); **`AddHostedService<OrderWorker>()`**; the `cart`, `checkout` and
`courier-webhook` rate-limit policies; `Couriers:Webhooks` and `Orders:Worker` options.
`ValidateScopes`/`ValidateOnBuild` stay on.

---

## 13. Allowed scope

```
src/Shared/CatEcommerce.Shared/Common/Enums/{the enumerations in §10.1}.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{OrderRules,CartRules,IdempotencyRules,CurrencyMinorDigits,OrderMessages}.cs
src/Shared/CatEcommerce.Shared/Common/Helpers/MinorUnits.cs
src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles,BusinessViolationCodes}.cs      ← append only
src/Shared/CatEcommerce.Shared/Common/Constants/SettingDefinitions.cs                                ← the ten keys of §10.2 only
src/Shared/CatEcommerce.Shared/Common/Constants/NotificationTemplateCatalogue.cs                    ← the twelve definitions of §8.12 only
src/Domain/CatEcommerce.Domain/Aggregates/Order/**
src/Domain/CatEcommerce.Domain/Entities/{CartLine,OrderLine,OrderLineAllocation,OrderStatusChange,OrderPaymentRef,ReturnLine,ZoneCoverage,ZoneServiceLevel}.cs
src/Domain/CatEcommerce.Domain/ValueObjects/{FulfilmentDetails,OrderTotals,Shipment,HeldPaymentConfirmation,CodCollection}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{CheckoutPricing,DeliveryZoneResolver,DeliveryChargeCalculator,DeliverySlotCalculator,PaymentOptionPolicy,OrderTransitionRules,ReturnEligibilityPolicy,RefundCalculator,RefundMethodPolicy,ReceiptTextRenderer,DiscountRules}.cs
src/Domain/CatEcommerce.Domain/DomainServices/{ICheckoutInputsLoader,IGuestCartTokens,IOrderingStaging,IReconciliationIssueRecorder,ICourierGateway,ICustomerDirectory,ICustomerPolicyStatus}.cs
src/Domain/CatEcommerce.Domain/DomainServices/INotificationLinkBuilder.cs                            ← OrderLink only
src/Domain/CatEcommerce.Domain/Models/**                                                            ← new DTOs only
src/Domain/CatEcommerce.Domain/Events/Order/**
src/Application/CatEcommerce.Application/Commands/Order/**
src/Application/CatEcommerce.Application/CommandHandlers/Order/**                                   ← incl. OrderPlacement
src/Application/CatEcommerce.Application/CommandHandlers/Validators/{one per command in §11.1}.cs
src/Application/CatEcommerce.Application/DomainEventHandlers/Order/**
src/Application/CatEcommerce.Application/DataMappers/{OrderMapper,CartMapper,ReturnMapper,DeliveryZoneMapper,DiscountCodeMapper}.cs
src/Read/CatEcommerce.Read/Queries/Order/**
src/Read/CatEcommerce.Read/QueryHandlers/Order/**                                                   ← incl. OrderBatchConsumptionQuery
src/Read/CatEcommerce.Read/ViewModels/{the ViewModels in §12.4}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/CatEcommerceDbContext.cs                ← DbSets only
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Configurations/{one per root and plain entity in §12.1}.cs
src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/**                            ← the six migrations of §12.1 only
src/Infrastructure/CatEcommerce.Infrastructure/Ordering/**
src/Infrastructure/CatEcommerce.Infrastructure/Notifications/NotificationLinkBuilder.cs             ← OrderLink only
src/Api/CatEcommerce.Api/Controllers/{Cart,CustomerCheckout,CustomerOrders,CustomerReturns,StaffOrders,StaffFulfilment,StaffReturns,StaffRefunds,StaffReconciliation,StaffPos,AdminDeliveryZones,AdminCouriers,AdminBankTransferAccount,AdminDiscountCodes,CourierWebhooks}Controller.cs
src/Api/CatEcommerce.Api/Contracts/Order/**
src/Api/CatEcommerce.Api/Idempotency/**                                                             ← IdempotentAttribute, IdempotencyFilter
src/Api/CatEcommerce.Api/Ordering/GuestCartCookieWriter.cs
src/Api/CatEcommerce.Api/Background/{OrderWorker,OrderWorkCycle}.cs
src/Api/CatEcommerce.Api/Filters/UniqueConstraintExceptionFilter.cs                                 ← §12.5 mappings only
src/Api/CatEcommerce.Api/Filters/ConcurrencyExceptionFilter.cs                                      ← the placement-route override only
src/Api/CatEcommerce.Api/RateLimiting/RateLimitPolicies.cs                                          ← three policy names only
src/Api/CatEcommerce.Api/appsettings.json                                                           ← Orders:Worker, Couriers:Webhooks (no secrets), RateLimiting entries only
src/Api/CatEcommerce.Api/Program.cs                                                                 ← §12.7 only
tests/CatEcommerce.Domain.Tests/**
tests/CatEcommerce.Application.Tests/**
tests/CatEcommerce.IntegrationTests/**
docs/test-cases/TC-HO-ORD-001.md
docs/gates/HO-ORD-001-*.md
.ai/decisions/adr.md                                                                                ← ADR-197…214, append only
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `.ai/**` except `decisions/adr.md`, `.claude/**` | Governing docs and agents |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs; a conflict is §20.9 |
| `web/**` | HO-ORD-002 |
| Every file of HO-ACC-001, HO-ADM-001, HO-CAT-001, HO-NTF-001, HO-APT-001 beyond the lines in §13 | Their areas. **No Account, Administration or Notification aggregate is edited** |
| Every file of HO-SHP-001, HO-IMP-001, HO-PHM-001, HO-BIL-001 | Consumed through their ports only. A missing port method is §20.2, never a local re-implementation |
| Any `inventory.*`, `billing.*`, `catalogue.*`, `clinic.*`, `account.*`, `admin.*` table written by this slice's code | Ports only; the reads named in §8.14 are `AsNoTracking` |
| **Any payment-gateway, card, wallet or token type, field, package or endpoint** | C-5, SRS Phase 5 |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | No package is added |

---

## 15. Unit test cases

| ID | Case | Expected |
|---|---|---|
| **A — foundation and money** | | |
| U-A1 | `MinorUnits` ↔ `Money` round trip for 0.01, 1250.50, 9 999 999 999.99 | Exact; no `double` anywhere (reflection over `Order` assemblies finds no `double`/`float` money property) |
| U-A2 | Half-away-from-zero at the line: 2.5 → 3, −2.5 → −3; a total is never rounded | As stated |
| U-A3 | Largest-remainder apportionment of 100 over 3 equal lines; of 1 over 3 lines | 34/33/33; 1/0/0 — always sums exactly — **US-ORD-04 AC2** |
| U-A4 | Currency with 3 minor digits in `commerce.currencyCode` | Start-up refusal (OQ-5) |
| U-A5 | `OrderTransitionRules` full table over every `(from, action)` | Only §8.4's arrows; others `InvalidOrderTransition` naming both states — **US-ORD-09 AC2** |
| U-A6 | Every transition | One `StatusHistory` row with actor and instant — **US-ORD-09 AC2** |
| U-A7 | All twelve template definitions | Pass HO-NTF-001 `TemplateRules`; the Urgent set is exactly §8.12's |
| U-A8 | Reflection over `Events/Order/**` | No string property; no name/address/phone/reference — HO-ACC-001 U-X1 |
| **B — zones, charges, discounts** | | |
| U-B1 | Resolver: district-wide zone A and postal-code zone B both match | B; neither matches → `AddressOutsideServedZones` — **US-ORD-06 AC2** |
| U-B2 | Charge: base 60, band ≤ 2000 g +0, ≤ 5000 g +40, 3.2 kg order | 100 — **US-ORD-06 AC1** |
| U-B3 | Free-over 2000; goods 1999.99 / 2000.00; express vs standard | Charged / 0; express uses its own rules |
| U-B4 | Slots: windows Mon–Sat, lead time 1 day, a closure tomorrow | Tomorrow absent; seven dates listed |
| U-B5 | `PaymentOptionPolicy`: pickup; COD disabled; zone without COD; total above ceiling; transfer disabled | `NotForPickup`; `CodDisabled`; `CodNotOfferedInZone`; `CodAboveOrderLimit` naming the limit; `BankTransferDisabled` — **US-ORD-07 AC2, BR-15** |
| U-B6 | `DiscountRules` for each failing rule, in order | The first failing code with its message — **US-ORD-05 AC2** |
| U-B7 | 10 % code scoped to category "Litter" on a mixed cart | Line discounts on litter lines only; order discount 0 |
| U-B8 | Fixed 100 unscoped on three lines | Order discount 100 apportioned exactly into `OrderDiscountShare` |
| U-B9 | First-order-only with a previous cancelled order; with a previous delivered order | Applies; `DiscountFirstOrderOnly` |
| U-B10 | `Redeem` at `UsageLimit`; `ReleaseRedemption` at 0 | `DiscountUsageLimitReached`; stays 0 |
| **C — cart** | | |
| U-C1 | `AddLine` twice for one SKU; once per cat for an Rx SKU | One line qty summed (capped); two lines |
| U-C2 | `MergeFrom`: guest {A×2, B×1}, account {A×3, C×1} | {A×3, B×1, C×1} — no duplicate line — **US-ORD-01 AC1** |
| U-C3 | Merge beyond the line cap | Extra lines dropped and reported |
| U-C4 | `PriceWhenAdded` on an existing line after a price change | Unchanged (the change is reported at checkout) |
| **D — pricing and quote** | | |
| U-D1 | Worked cart: 2 × 850.00 (VAT 15 %), 1 × 120.00 (VAT 0 %), 10 % order code, delivery 60 | subtotal 1820.00, order discount 182.00, tax 229.50, delivery 60.00, total 1927.50 — each component separate — **US-ORD-04 AC1** |
| U-D2 | Price changed since add; stock 1 of 3; product archived; Rx authorisation lapsed; Rx line without cat | `PriceIncreased`; `QuantityReduced` (qty 1); `NowUnavailable` (excluded); `PrescriptionAuthorisationExpired` (excluded); `PrescriptionCatRequired`, `canPlace = false` — **US-ORD-02, US-SHP-10 AC3, US-ORD-03 AC2** |
| U-D3 | Fingerprint stable across runs and cultures; changes when any priced field, the method or the slot changes | As stated |
| U-D4 | `Place` with totals that do not re-sum | `InvalidOperationException` |
| U-D5 | Rate change between quote and placement | New fingerprint → the handler refuses (`CheckoutQuoteChanged`) |
| **E — payment paths and cancellation** | | |
| U-E1 | `Expire` one tick before `PaymentDueBy`; at it; with a held payment | `OrderNotExpirable`; cancelled `PaymentWindowExpired`; `OrderNotExpirable` |
| U-E2 | `Cancel` by customer from `Confirmed`; from `Packing`; by staff from `Packing` | Allowed; `CancellationNotAvailable`; allowed — **US-ORD-12** |
| U-E3 | Cancel a paid order | `RefundDue = AmountPaid`, event `WasPaid` |
| U-E4 | Approve by the recorder | `SecondApproverMustDiffer` |
| U-E5 | Transfer amount 1927.49 against 1927.50 | `TransferAmountMismatch` |
| **F — fulfilment** | | |
| U-F1 | `Dispatch` with an unallocated line | `OrderNotFullyAllocated` |
| U-F2 | `RecordDelivery` COD without collection; pay-on-collection with a short payment; card without slip reference | `CodCollectionRequired`; `CollectionPaymentMismatch`; `TerminalReferenceRequired` |
| U-F3 | `RecordDeliveryFailure(Uncollected)` on a delivery order | `DeliveryFailureReasonInvalid` |
| U-F4 | `ApplyTrackingEvent` older than the last | Ignored |
| **G — returns and refunds** | | |
| U-G1 | Eligibility: sealed food day 7 of 7; day 8 | Eligible; `ReturnWindowClosed` — **US-ORD-13 AC1** |
| U-G2 | Opened food, reason `ChangedMind`; opened food with batch recalled; opened food with Inventory defect; opened food reason `QualityDefect` | `ReturnNotEligibleOpenedFood`; eligible; eligible; eligible with `RequiresDefectConfirmation` — **BR-10, US-ORD-13 AC2** |
| U-G3 | Used litter; opened (unused) litter; opened toy | `ReturnNotEligibleUsedLitter`; eligible; eligible |
| U-G4 | Quantity beyond delivered − returned − pending | `ReturnQuantityExceeded` |
| U-G5 | `RefundMethodPolicy` per payment method | §8.6 table exactly — **US-ORD-15 AC1** |
| U-G6 | `ApplyRefund` over `AmountPaid − AmountRefunded`; exact full refund on `Delivered` | `RefundExceedsAmountPaid`; `Refunded` |
| U-G7 | `RefundCalculator` for 2 of 3 units of a 1000.00 line | 666.67 (remainder on the last unit when all three are returned) |
| U-G8 | Every line fully returned while `Delivered` | `Returned` |
| **H — POS** | | |
| U-H1 | Tenders: cash 2000 for 1927.50; card 1000 + cash 1000; card 2000 for 1927.50 | change 72.50; change 72.50; `TendersDoNotCoverTotal` (card may not exceed the balance) |
| U-H2 | `CompleteCounterSale` | Three history rows `PendingPayment → Confirmed → Delivered` |
| U-H3 | `ReceiptTextRenderer` | Every line ≤ 48 chars; totals right-aligned; no line break inside an amount |
| **X — cross-cutting** | | |
| U-X1 | `OrderWorker` loop with a throwing pass | Logged; the next pass runs |
| U-X2 | Idempotency: same key, same body, completed; in progress; different body | Replay with `Idempotent-Replayed`; `409 RequestInProgress`; `422 IdempotencyKeyReused` |
| U-X3 | Webhook signature: valid; tampered body; wrong provider | Accepted; `401`; `404` |

## 16. Integration test cases

Real HTTP, `Testing` environment, shared database (HO-ACC-001 §16.1). Workers off; tests call
`OrderWorkCycle` and HO-NTF-001's `NotificationWorkCycle`, advancing `FakeTimeProvider`. Shop,
Inventory, Pharmacy and Billing are the **real** implementations from their merged slices — not fakes.

### 16.1 Additional shared-database rules

1. **Each test creates its own branch, zone, SKUs and batches** (via upstream endpoints or their test
   seams) and a customer; stock and discount counters are therefore private to the test.
2. Discount codes are minted per test (`T-{guid:N}` truncated).
3. Idempotency keys are fresh UUIDs per test unless the case is about reuse.

| ID | Case | Expected |
|---|---|---|
| I-A1 | Migrations on an empty database | `ordering` and `ops` schemas; every index of §12.1; the sequence exists |
| I-A2 | Host starts, scope validation on | Every registration resolves; `EndpointAccessConventionTests` pass |
| I-A3 | V1/V2 verification cases | Recorded in the test sheet |
| I-B1 | Create zone, service levels, bands; resolve an address | Zone returned; duplicate name `400 DeliveryZoneNameAlreadyExists` |
| I-B2 | Discount code CRUD as shop manager; as inventory officer | `201`; `403` |
| I-C1 | Guest adds two SKUs | `201`, `pc_cart` HttpOnly+Secure+SameSite=Lax; `GET /api/cart` shows them |
| I-C2 | Guest signs in with an account cart holding one of the same SKUs; `GET /api/cart` then `POST …/merge` | `guestCartPending: true`; after merge one line per SKU with the larger quantity; guest cart `Merged`; cookie expired — **US-ORD-01 AC1** |
| I-C3 | Same customer, second client (no guest cookie) | Same cart — **US-ORD-01 AC2** |
| I-C4 | Guest adds an Rx SKU; customer adds it without cat; with an unauthorised cat; with another customer's cat; with an authorised cat | `400 PrescriptionCatRequired`; `400 PrescriptionCatRequired`; `400 PrescriptionNotAuthorised`; `404`; `201` with the cat — **US-SHP-10 AC2** |
| I-C5 | Add an out-of-stock SKU | `400 SkuUnavailable` |
| I-D1 | Quote after a price rise; place with the **old** fingerprint; re-quote; place with the new one | `changes[PriceIncreased]`; `409 CheckoutQuoteChanged`; `201` — **US-ORD-02** |
| I-D2 | Quote shows subtotal, line discounts, order discount, delivery, tax and total; the order stores the same | Components equal to the minor unit — **US-ORD-04** |
| I-D3 | Address outside every zone | `delivery.available = false`, `AddressOutsideServedZones`, pickup branches listed — **US-ORD-06 AC2** |
| I-D4 | Pickup order with a slot-less branch; delivery with a slot not offered | `201`; `400 DeliverySlotUnavailable` — **US-ORD-03 AC1** |
| I-D5 | Rx line without cat reaches placement | `canPlace = false`; placement `400 PrescriptionCatRequired` — **US-ORD-03 AC2** |
| I-D6 | COD in a zone without COD; above the ceiling | `paymentOptions` COD unavailable with the reason; placing with COD `400 PaymentMethodUnavailable` — **US-ORD-07 AC2** |
| I-D7 | COD order placed | `201`, `Confirmed`; allocations per line with batch; an invoice number; `order.confirmed` request committed; the cart no longer holds the placed lines — **US-ORD-08 AC1, US-ORD-20 AC1** |
| I-D8 | Force Billing's staged invoice to fail (test seam throws at save) during a COD placement | `500`-class problem; **no** order, reservation, invoice or notification row exists — atomicity — **US-ORD-08 AC1** |
| I-D9 | **20 parallel placements for the last unit** by 20 customers | Exactly **one** `201`, the rest `409 StockNoLongerAvailable` — **US-IMP-08 AC2** |
| I-D10 | **Double submit**: two concurrent `POST /orders` with one key; a third after both | One order; the other `409 RequestInProgress` or the replay; the third replays `201` with the same `orderId` — **US-ORD-18 AC1** |
| I-D11 | Same key, different body | `422 IdempotencyKeyReused` |
| I-D12 | Discount with usage limit 1; two customers place concurrently | One applied, one `409 CheckoutQuoteChanged` → re-quote shows `DiscountUsageLimitReached` — **US-ORD-05** |
| I-D13 | Placement with a pending terms version | `400 PolicyAcceptanceRequired`; after accepting through HO-ADM-001, `201` |
| I-D14 | Scan the database after I-D7, I-E2, I-H1 | No column named like `card|pan|cvv|token|wallet` in `ordering.*`; no request body stored containing digits in a 13–19-digit run — **US-ORD-07 AC3, C-5** |
| I-D15 | Maintenance mode on | Cart and checkout `503`; portal order detail and staff endpoints `200` |
| I-E1 | Transfer order placed | `PendingPayment`, stock reserved (sellable reduced), `transferInstructions` with reference; `order.awaiting-transfer` enqueued — **US-ORD-19 AC1** |
| I-E2 | Customer submits a reference; staff confirms the exact amount | `Confirmed`; invoice + payment in Billing with the recorder; `order.confirmed` delivered in-app — **US-ORD-19 AC2, US-ORD-08 AC1** |
| I-E3 | Staff confirms an amount above the approval threshold; the same person approves; another approves | `awaitingApproval: true`; `400 SecondApproverMustDiffer`; `Confirmed` with approved-by — **FR-BIL-12** |
| I-E4 | Advance 48 h; run `OrderWorkCycle` | `Cancelled` (`PaymentWindowExpired`), stock sellable again, discount released, `order.expired` enqueued; detail `canReorder` — **US-ORD-19 AC3, BR-16, AC-8b** |
| I-E5 | Held order past its due time; run the cycle | Not expired |
| I-E6 | Re-order the expired order | Available lines in the cart; an archived SKU listed as unavailable — **US-ORD-19 AC3** |
| I-E7 | Force the Billing payment stage to throw during C11 | `500`-class; an `Open` reconciliation issue with the entered amount and reference; the order still `PendingPayment` — **US-ORD-08 AC2** |
| I-E8 | Customer cancels a `Confirmed` COD order | `204`; reservation released; full credit note; `order.cancelled` — **US-ORD-12 AC1** |
| I-E9 | Customer cancels a transfer-paid `Confirmed` order | `refundDue` = total; listed in refunds-due; `order.cancelled-refund-due` — **US-ORD-12 AC1** |
| I-E10 | Customer cancels in `Packing` | `400 CancellationNotAvailable` — **US-ORD-12 AC2** |
| I-E11 | Another customer's order: detail, cancel, returns | `404` each |
| I-F1 | Walk `Confirmed → Packing → Dispatched → Delivered`; then pack again | `204` each with history rows attributed; `400 InvalidOrderTransition` — **US-ORD-09** |
| I-F2 | Pick list for three orders sharing a SKU across two batches and two locations | Grouped by location then batch, quantities per order — **US-ORD-10 AC1** |
| I-F3 | Packing slip | Contents with batch numbers and customer details — **US-ORD-10 AC2** |
| I-F4 | Dispatch with courier and tracking; customer detail | Tracking number and courier link shown; `order.dispatched` — **US-ORD-11 AC1 (manual half)** |
| I-F5 | Dispatch without tracking; manifest CSV; enter tracking later | CSV has the order with COD amount; `PUT tracking` → `order.tracking-added` — **US-ORD-11 AC2** |
| I-F6 | Recall a batch allocated to a `Packing` order (Inventory endpoint); dispatch | `400 BatchRecalled`; reallocate with reason → dispatch succeeds — **BR-14, US-IMP-11 AC4, US-IMP-07 AC2** |
| I-F7 | HO-IMP-001's recall trace for that batch | Lists the order, customer and cat from the stamp this slice wrote — **US-IMP-11 AC2 (order path)** |
| I-F8 | COD delivery with collection by courier name | Delivered; Billing payment `CashOnDelivery` with collector and date; `codCollection` on the detail — **US-ORD-20 AC2, AC-8** |
| I-F9 | COD parcel refused | `Returned`; a `DeliveryFailure` return `Approved`; invoice credited; receive it → quarantine; decide restock — **US-ORD-20 AC3** |
| I-F10 | Webhook event delivered twice with one event id | One receipt row, one order update, second `200` — **US-ORD-18 AC2** |
| I-F11 | Webhook with a bad signature | `401`; nothing written |
| I-F12 | Pickup, pay on collection: ready for collection, collect with card and slip reference | `Delivered`; payment `InStoreCard` with reference; stock issued at collection |
| I-G1 | Sealed food return within window | `201 Requested`; staff approve — **US-ORD-13 AC1** |
| I-G2 | Opened food, changed mind | `400 ReturnNotEligibleOpenedFood`; after recalling the batch, the same request `201` — **US-ORD-13 AC2, AC-9** |
| I-G3 | Opened food claimed defective; approve without defect confirmation; with it | `201`; `400 DefectConfirmationRequired`; `204` |
| I-G4 | Receive the approved return | Inventory shows the quantity in the quarantine location, not sellable — **US-ORD-14 AC1** |
| ~~I-G5~~ *Removed 2026-10-09 -- disposition is Inventory's quarantine workflow (OQ-19); see the Amended (gate fix 3) row.* | Decide restock one line, write off another | Restocked to the original batch; written off with reason and user in the ledger and audit — **US-ORD-14 AC2** |
| ~~I-G6~~ *Removed 2026-10-09 -- disposition is Inventory's quarantine workflow (OQ-19); see the Amended (gate fix 3) row.* | Restock a line whose batch expired meanwhile | Inventory's refusal surfaced |
| I-G7 | Refund by transfer for a COD-paid order; by cash for a transfer-paid order | `201` with credit note linked to the invoice; `400 RefundMethodNotAllowed` — **US-ORD-15** |
| I-G8 | Refund with store credit (no Billing store credit) | `400 RefundMethodUnavailable` (OQ-8) |
| I-G9 | Full refund on the delivered order | `Refunded`; audit rows name the issuing staff — **US-ORD-15 AC2** |
| I-H1 | POS: barcode lookup; sale with cash and change | SKU and FEFO batch preview; `201` with invoice number and change; ledger issue entries of the same kind as online orders — **US-ORD-16 AC1, AC2** |
| I-H2 | POS sale of an Rx SKU without a customer | `400 PrescriptionCatRequired` |
| I-H3 | POS receipt | Structured data and 48-column text |
| I-H4 | POS double submit with one key | One sale |
| I-X1 | Log capture at `Information`+ across all cases | No recipient names, addresses, phone numbers, transfer or terminal references, or webhook bodies |
| I-X2 | Merge the customer | Carts merged, orders and returns under the survivor |
| I-X3 | Anonymise a customer with a delivered and an open order | Delivered redacted; open flagged, redacted when delivered |
| I-X4 | Nurse calls any staff order endpoint | `403` |

---

## 17. Implementation order and verification

| Phase | Stories | Delivers |
|---|---|---|
| **A** | US-ORD-18 (edge) + foundation | V1–V3 verification (§7 — stop on V3 failure); enumerations, settings, constants, `MinorUnits`; `OrderTransitionRules`; idempotency filter and `ops` tables; webhook receipt table; templates; `OrderLink`; migration `AddOrderingEdgeInfrastructure` |
| **B** | US-ORD-05, 06 (configuration) | Zones, couriers, bank account, discount codes; resolver, charge and slot calculators; `AddOrderingConfiguration` |
| **C** | US-ORD-01, US-SHP-10 AC2 | Cart, guest cookie, merge, purge; `AddOrderingCart` |
| **D** | US-ORD-02, 03, 04, 05, 06, 07, 08 (COD, pay-on-collection), 18 AC1, US-SHP-10 AC3 | `CheckoutPricing`, quote, placement, `OrderPlacement`, reservation and invoice staging; `AddOrderingOrders` |
| **E** | US-ORD-19, 08 AC2, 12 | Transfer reference, confirmation, approval, expiry, `OrderWorker`, cancellation, re-order, reconciliation issues; `AddOrderingReconciliationIssues` |
| **F** | US-ORD-09, 10, 11, 18 AC2, 20, US-IMP-07 AC2, US-IMP-11 AC2/AC4 | Packing, pick list, packing slip, dispatch, manifest, tracking, webhook, delivery, delivery failure, reallocation, `OrderBatchConsumptionQuery` |
| **G** | US-ORD-13, 14, 15 | Returns, receipt, decisions, refunds; `AddOrderingReturnsAndRefunds` |
| **H** | US-ORD-16 | POS lookup, counter quote, sale, receipt (built in three steps: scan → tender → receipt, per the backlog's split note) |

Each phase: code → its unit tests green → its migration generated **and read** → its integration tests
green. Record each phase's commit in the test sheet.

```bash
dotnet restore CatEcommerce.sln
dotnet build CatEcommerce.sln            # 0 warnings, 0 errors
dotnet test tests/CatEcommerce.Domain.Tests
dotnet test tests/CatEcommerce.Application.Tests
dotnet dotnet-ef migrations add <PhaseMigration> --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api --output-dir Persistence/Migrations
dotnet dotnet-ef database update --project src/Infrastructure/CatEcommerce.Infrastructure --startup-project src/Api/CatEcommerce.Api
dotnet run --project src/Api/CatEcommerce.Api
dotnet test tests/CatEcommerce.IntegrationTests
```

`Couriers:Webhooks:*` secrets go only in the git-ignored `appsettings.Development.json` or the secret
store (NFR-SEC-7) — **never in the committed file** (§20.8).

---

## 18. Acceptance criteria

Each row is checked against its evidence. **A row with no evidence is not met.**

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | ORD-01 AC1 | A guest cart merges into the account cart on sign-in without duplicate lines | U-C2, U-C3, I-C2 |
| A2 | ORD-01 AC2 | The cart is present on another device | I-C3 |
| A3 | ORD-02 | Price and stock changes since adding are shown at checkout and must be acknowledged before placing | U-D2, U-D3, U-D5, I-D1 |
| A4 | ORD-03 AC1 | Saved address or pickup, plus a slot where the service offers slots | U-B4, I-D4 |
| A5 | ORD-03 AC2 | A prescription line needs its cat before checkout proceeds | U-D2, I-D5 |
| A6 | ORD-04 AC1 | Subtotals, line discounts, order discount, delivery and tax shown separately before the total | U-D1, I-D2 |
| A7 | ORD-04 AC2 | Every money calculation in integer minor units | U-A1, U-A2, U-A3 |
| A8 | ORD-05 AC1 | Codes support every rule listed | U-B6…U-B10, I-B2, I-D12 |
| A9 | ORD-05 AC2 | A failing code is rejected with the specific reason | U-B6, I-D12 |
| A10 | ORD-06 AC1 | Charge by zone, value, weight and service level, shown before payment | U-B2, U-B3, I-D2 |
| A11 | ORD-06 AC2 | Outside every zone → delivery unavailable, pickup offered | U-B1, I-D3 |
| A12 | ORD-07 AC1 | COD and transfer offered by configuration; pickup may be paid in store | U-B5, I-D7, I-E1, I-F12 |
| A13 | ORD-07 AC2 | COD hidden with its reason when disabled for the zone or above the ceiling | U-B5, I-D6 |
| A14 | ORD-07 AC3 | No card number and no payment token stored anywhere | I-D14, §4.4, §14 |
| A15 | ORD-19 AC1 | Transfer order held in `pending_payment` with stock reserved and bank details and reference shown | I-E1 |
| A16 | ORD-19 AC2 | Staff verification moves it to `confirmed` and the customer is notified | I-E2, I-E3 |
| A17 | ORD-19 AC3 | Unconfirmed past the window → expired, stock released, customer notified with re-order | U-E1, I-E4, I-E5, I-E6 |
| A18 | ORD-20 AC1 | COD confirmed and fulfilled with no prepayment | I-D7, I-F8 |
| A19 | ORD-20 AC2 | Cash amount, collector and date recorded against the order | U-F2, I-F8 |
| A20 | ORD-20 AC3 | Refused or undelivered parcel → returns path → quarantine | U-F3, I-F9 |
| A21 | ORD-08 AC1 | Order, batch reservation, confirmation and invoice happen atomically | I-D7, **I-D8**, I-E2 |
| A22 | ORD-08 AC2 | A failure after payment was recorded is surfaced for reconciliation, never lost | I-E7 |
| A23 | ORD-09 AC1 | Transitions follow Appendix C with the alternates | U-A5, I-F1 |
| A24 | ORD-09 AC2 | Invalid transitions refused; valid ones timestamped and attributed | U-A5, U-A6, I-F1 |
| A25 | ORD-10 AC1 | Pick list grouped by location and batch | I-F2 |
| A26 | ORD-10 AC2 | Packing slip with contents and customer details | I-F3 |
| A27 | ORD-11 AC1 | Tracking number stored and shown to the customer — **courier-API shipment creation not met until an adapter exists (SRS Q-4, §4.3)** | I-F4 |
| A28 | ORD-11 AC2 | CSV manifest and manual tracking when no API is available | I-F5 |
| A29 | ORD-12 AC1 | Cancel before packing releases stock and triggers the refund of captured money | U-E2, U-E3, I-E8, I-E9 |
| A30 | ORD-12 AC2 | Self-service cancellation refused once packing began | U-E2, I-E10 |
| A31 | ORD-13 AC1 | Sealed item within window → return request to staff approval | U-G1, I-G1 |
| A32 | ORD-13 AC2 | Opened food / used litter refused unless defect or recall recorded | U-G2, U-G3, I-G2, I-G3 |
| A33 | ORD-14 AC1 | Returned items enter quarantine | I-G4, I-F9 |
| A34 | ORD-14 AC2 (**C24 removed 2026-10-09: this row is now Inventory's quarantine screen, not this slice**) | Restock to the original batch or write off, with decision, reason and user | I-G5, I-G6 |
| A35 | ORD-15 AC1 | Refund method by how the customer paid and their choice | U-G5, I-G7, I-G8 |
| A36 | ORD-15 AC2 | Method, amount, reference, staff and reason recorded; credit note linked; audited | U-G6, I-G7, I-G9 |
| A37 | ORD-16 AC1 | Counter sale prints a receipt and deducts through the same ledger | U-H1, U-H2, U-H3, I-H1, I-H3 |
| A38 | ORD-16 AC2 | A barcode selects the SKU and its FEFO batch | I-H1 |
| A39 | ORD-18 AC1 | A double submit with one key creates one order | U-X2, **I-D10**, I-D11, I-H4 |
| A40 | ORD-18 AC2 | A redelivered webhook is processed once by event id | U-X3, I-F10, I-F11 |
| A41 | SHP-10 AC2, AC3 *(assumed delegated, OQ-12)* | Cat chosen at add-to-cart and recorded on the line; checkout blocks a line whose authorisation expired | U-D2, I-C4, I-D5 |
| A42 | IMP-08 AC2 | Concurrent checkout for the last unit: exactly one succeeds | **I-D9** |
| A43 | IMP-11 AC2 (order path), AC4; BR-14 | Orders that received a batch are traceable; unshipped orders with a recalled batch cannot dispatch | I-F6, I-F7 |
| A44 | IMP-07 AC2 (order half) | FEFO override at picking needs a reason and is audited | I-F6 |
| A45 | BR-16 | No order is held indefinitely on unconfirmed payment | I-E4 |
| A46 | ADM-07 AC2 (checkout half) | Current policies accepted before an order is placed | I-D13 |

**Deferred, named in §4.2**: US-ORD-17 combined invoice (HO-BIL-001, HO-BRD-001); courier API adapter
(Q-4); reconciliation (HO-BIL-001); portal dashboard (HO-POR-001).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | `Order` context and Appendix C states (ADR-197); **the enlisting ports and multi-root saves (ADR-201)** — each handler's saved/tracked/staged set matches §11.4; no write to another context's tables; **no enqueue from any event handler**; pure pricing services; ports declared in Domain; the idempotency filter's separate context (ADR-202); §13 scope incl. the one line in HO-NTF-001's link builder; ADR-197…214 recorded |
| 2 | SECURITY | `security-reviewer` | **C-5: no card/token anywhere (I-D14)**; server-side pricing only; fingerprint acknowledgement; `404` for another customer's cart, order, return or cat; guest cookie attributes and hashing; CSRF on customer writes (HO-ACC-001 R7); webhook HMAC in fixed time and dedupe; second approver ≠ recorder; CSV-injection guard on the manifest; rate limits on cart, checkout, webhook; no personal data or references in events or logs (I-X1); reconciliation issues store no exception messages |
| 3 | CODE_REVIEW | `code-reviewer` | No `double`/`float` money; `OrderPlacement` shared by placement and POS; `OrderTransitionRules` used by aggregate and reads; ADR-205 wrapper on every money handler; the worker never throws; idempotent event handlers |
| 4 | TEST | `test-engineer` | `TC-HO-ORD-001.md` from §15/§16 first; §16.1 isolation; **I-D8, I-D9 and I-D10 with real parallelism and real upstream implementations**, not fakes or sequential calls |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; A27's courier-API gap and A41's delegation assumption recorded, not passed silently; US-ORD-17 deferral named |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is not merged** — HO-ACC-001, HO-ADM-001, HO-NTF-001, HO-SHP-001, HO-IMP-001,
HO-PHM-001 or HO-BIL-001. Do not stub them, and never build a local catalogue, stock table, invoice or
authorisation store "for now".

**20.2 An upstream port differs from §8.14** (name, shape, enlisting behaviour, the reservation race
error). Stop and report the difference. The fix is an Amended row here (and, if needed, there) — not an
adapter that hides a behavioural gap. In particular: **if Inventory's reservation or Billing's invoice
issue commits on its own instead of staging**, US-ORD-08's atomicity cannot be delivered as designed —
report it; do not add compensation logic quietly.

**20.3 A concurrency test (I-D9, I-D10, I-D12) passes only with retries, delays or serialisation in
the test.** The control is wrong; fix it, never the test.

**20.4 Do not add a payment gateway, card field, token, wallet or online-payment endpoint** — C-5,
SRS Phase 5. Report any request for one.

**20.5 Do not pick a batch in Ordering or write Inventory or Billing tables** — ADR-208. If a
fulfilment step seems to need it, the port is missing a method (§20.2).

**20.6 Do not enqueue from an event handler** — HO-NTF-001 ADR-057.

**20.7 Do not add an order status** beyond Appendix C — ADR-197. A screen that "needs" one is served by
flags on the read model.

**20.8 A secret (webhook secret, bank account in configuration) appears in a committed file.** Stop;
move it to the secret store (NFR-SEC-7). The bank transfer account is data (C3), not configuration.

**20.9 A requirement conflict is a stop, not an edit** — SRS and backlog are forbidden scope. Known
tensions are recorded as OQ-2, OQ-5 and OQ-10; a new one is reported the same way.
