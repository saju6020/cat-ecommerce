# TC-HO-ORD-001 — Orders (backend)

Handover: docs/handover/HO-ORD-001.md
Commit(s) under test: branch `ho/ORD-001`, per phase —
A `663cc93f` · A edge `2aab161e` · B `9a0bdfb2`, `fa8296c4` · C `681a4dbc` · D `5471674e`, `6f43c041`, `b8f941bf`, `e3fafc5f` · E `2a5544c7` · F `cf81c2c1` · G `26d21c9b` · H `7b357ce7`
Written: 2026-10-09  ·  Executed: 2026-10-09 (implementer's own run) and 2026-10-10 (independent TEST gate, head 85c74f53 plus the new test file)
Gate-fix round: 2026-10-09 (ARCHITECTURE FAIL: 1 BLOCKER, 4 HIGH) -- rows below marked **gate fix** are new or changed; C24 (I-G5, I-G6) is removed.

> Statuses record what was observed. Integration rows ran against the per-branch database `catecommerce_ord`
> (never `catecommerce_dev`); the test factory refuses an unset or dev `CATECOMMERCE_TEST_DB`.
> `SKIPPED` rows are skipped in code with the reason in the `Skip` text.

## Unit (CatEcommerce.Domain.Tests / Ordering)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A..D | Pricing, zones, charges, slots, discounts, payment options, transitions, order aggregate | per handover §16.1 | PASS | `CheckoutPricingTests`, `ZoneAndChargeTests`, `DiscountTests`, `OrderTransitionRulesTests`, `OrderAggregateTests` |
| U-E | Reconciliation issue (no card number, no exception text, resolved once) | per §16.1 | PASS | `ReconciliationIssueTests` |
| U-G1..G8 | Return eligibility (window, quantity, BR-10 and its exceptions), refund method table, cumulative rounding, return state machine | per §16.1 | PASS | `ReturnsAndRefundsTests` (26 cases) |
| U-H1 | Tenders: cash 2000 for 1927.50; card 1000 + cash 1000; card 2000 for 1927.50 | change 72.50; change 72.50; `TendersDoNotCoverTotal` | PASS | `PosTests` |
| U-H2 | `CompleteCounterSale` | three history rows | PASS | `OrderAggregateTests.U_H2_*` |
| U-H3 | `ReceiptTextRenderer` | every line at most 48 columns; totals right-aligned; amounts whole | PASS | `PosTests` |

## Integration (CatEcommerce.IntegrationTests / Endpoints)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I-B..C | Configuration, cart, guest cookie, merge, prescription lines, out of stock | per §16.2 | PASS | `Ordering*Configuration*`, `OrderingCartEndpointTests` |
| I-D1..D14 | Quote, placement (atomic with Billing), concurrency on the last unit, double submit, discount last use, no card data in `ordering.*` | per §16.2 | PASS | `OrderingPlacementEndpointTests`, `OrderStockTests` |
| I-D5, I-D5b, I-C4 authorised cat | Diet gate against the REAL HO-PHM-001 `IDietAuthorisationGate` (2026-10-10, after the merge of master): a Rx line without a cat cannot be placed (`PrescriptionCatRequired`); an authorised Rx line is placed with its cat; an authorisation that lapses between quote and placement refuses the old quote (`409 CheckoutQuoteChanged`) and a fresh quote excludes the line (`PrescriptionAuthorisationExpired`) | per §16.2 | PASS | `OrderingPlacementEndpointTests.I_D5_*`, `I_D5b_*`; `OrderingCartEndpointTests.I_C4_AnAuthorisedCat_*` (authorisation seeded through `DietAuthorisationAggregate`, the table the real gate reads) |
| I-D15 | Maintenance mode blocks placement | — | PASS (reflection only) | a global flip is unsafe in the shared database |
| I-E2..E11 | Transfer reference, confirmation with second approval, expiry, cancellation + credit note, re-order, reconciliation issues, customer data events | per §16.2 | PASS | `OrderingPaymentEndpointTests`, `OrderingPaymentFaultEndpointTests`, `OrderingCustomerDataEndpointTests` |
| I-F1..F12 | Packing, dispatch (BR-14), collection, tracking, COD delivery, delivery failure, FEFO override, courier webhook (HMAC, dedupe), pick list, manifest | per §16.2 | PASS | `OrderingFulfilmentEndpointTests` (15) |
| I-F9 | Refused COD parcel: `Returned`, `Approved` DeliveryFailure return, invoice credited, receive to quarantine (restock is Inventory's, **gate fix 3**) | per §16.2 | PASS | `OrderingReturnsEndpointTests.I_F9_*` |
| I-G1 | Sealed food inside the window: request, approve, notify | per §16.2 | PASS | `I_G1_*`, window-closed and rejection cases |
| I-G2 | Opened food for a change of mind refused; eligible once the batch is recalled | per §16.2 | PASS | `I_G2_*`, mixed-request case |
| I-G3 | Opened food claimed defective: approval needs the defect confirmed and noted | per §16.2 | PASS | `I_G3_*` |
| I-G4 | Receive into quarantine against the original batch; receiving more than approved is refused | per §16.2 | PASS | `I_G4_*`, `I_G4b_*` |
| ~~I-G5, I-G6~~ | ~~Restock / write-off decisions (C24)~~ -- **removed, gate fix 3**: disposition is Inventory's quarantine workflow (OQ-19) | — | REMOVED | tests and endpoint deleted |
| I-G7..G9 | Refund options, cash refund with receipt and credit note, over-refund, idempotent edge, branch scoping | per §16.2 | PASS | `I_G7_*`, `I_G8_*`, `I_G9_*` |
| I-H1 | Barcode lookup with FEFO batch; cash sale with change; ledger entries of the same kind as online | per §16.2 | PASS | `OrderingPosEndpointTests.I_H1_*` |
| I-H2 | Rx SKU without a customer and cat | `400 PrescriptionCatRequired` | PASS | `I_H2_*` |
| I-H3 | Receipt structured data and 48-column text | per §16.2 | PASS | `I_H3_*` |
| I-H4 | POS double submit with one key | one sale | PASS | `I_H4_*` |


## Gate fix (2026-10-09) -- new and changed cases

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| GF-2a | Ordering takes no Inventory or Billing internal as a dependency (`IStockPositionReader`, `IInventoryClock`, `IBillingFinder`, `IDocumentNumbering`, Billing aggregates); `ITransactionalScope` has only `RunAsync` and `FlushAsync` | none found | PASS | `OrderingPublishedPortsOnlyTests` (3) |
| GF-2b | BR-14 by Inventory's verdict: recalled / expired / withdrawn named with the product; usable batches pass with no reservation | codes `BatchRecalled` / `BatchExpired` / `BatchNotUsable` | PASS | `OrderStockTests.BR14_*` |
| GF-2c | Reallocation passes only the staff-chosen batch and reason; the old reservation is released and flushed; another item's / branch's batch is `BatchNotFound`; a bundle's other components go FEFO | one `ReserveBatchAsync`, no explicit location lines | PASS | `OrderStockTests.Reallocation_*` |
| GF-2d | `IStockLedger.ReserveBatchAsync`: Inventory picks the locations, flags and audits the override; refuses unknown / other-branch / expired / short | per HO-IMP-001 Amended | PASS | `InventoryStockPortEndpointTests.ReserveBatch_*` |
| GF-2e | `IStockLedger.GetBatchUsabilityAsync`: recalled, withdrawn, expired, unknown, other branch; writes nothing | per HO-IMP-001 Amended | PASS | `InventoryStockPortEndpointTests.BatchUsability_*` |
| GF-3 | C24 gone: no `decisions` route, no restock / write-off call into Inventory | `404` | PASS | `OrderingReturnsEndpointTests` (I-F9 ends at the receipt) |
| GF-4a | Held transfer above the threshold: the Billing payment exists at once, `PendingApproval`, recorded by the first person, invoice unpaid; the recorder cannot approve; a shop manager cannot approve (`403`); an accountant approves; recorded-by the first, approved-by the second | per HO-ORD-001 Amended (gate fix 4) | PASS | `OrderingPaymentEndpointTests.I_E3_*` |
| GF-4b | Rejected hold: the Billing payment is `Rejected`, the invoice stays; entering again reuses the invoice and confirms | one invoice, order `Confirmed` | PASS | `ARejectedHeldConfirmation_*` |
| GF-5 | Scoped counter sales and staff-issued invoices racing at one branch from a cold series | 14 distinct, contiguous numbers | PASS | `OrderingBillingNumberingEndpointTests` (run three times) |
| GF-5b | Stock-race translation with no database: each of the three constraints becomes `StockNoLongerAvailable` (`RunAsync` and `FlushAsync`); other failures pass through | per backlog ORD 1 | PASS | `TransactionalScopeTests` (6) |
| GF-6a | Counter sale to a new mobile creates a passwordless Reception-channel customer who carries the order and the invoice; a second sale finds the same customer | one account | PASS | `ACounterSale_ToANewMobile_...` |
| GF-6b | Walk-in customer who later self-registers with the same mobile gets `MobileNumberAlreadyRegistered` with the reset offer; still one account | `400`, "reset your password" | PASS | `TheWalkInCustomer_WhoLaterRegistersOnline_...` |
| GF-6c | Name and a valid mobile are required; a refused sale leaves no account | `400`; zero accounts | PASS | `ACounterSale_NeedsTheCustomersNameAndAValidMobile`, `ARefusedSale_LeavesNoWalkInAccountBehind` |
| GF-6d | An existing customer found by mobile is used (not duplicated); the typed name is ignored; a prescription food without a cat is refused | one account | PASS | `ASaleToAKnownCustomer_...`, `I_H2_*` |
| GF-7a | Weight bands applied to the cart's shipping weight (600 g, 3,000 g, 10,000 g, across lines); a SKU without a weight counts as 0 g | 60 / 100 / 160 / 160 | PASS | `CheckoutPricingTests.TheDeliveryCharge_AddsTheWeightBandSurcharge_*`, `ASkuWithoutAShippingWeight_*` |
| GF-7b | No 0 % tax class: the delivery charge cannot be invoiced; a clean business refusal, not a 5xx. With a class it becomes a `Fee` charge; with no delivery the class is not needed | `DeliveryTaxClassNotConfigured` (a `DomainRuleViolationException` the handlers map to 400) | PASS (unit) | `OrderBillingTests` (2) |
| GF-8 | Reconciliation issues: another branch neither lists nor resolves (404); the owner branch resolves | per HO-ORD-001 Amended (gate fix 8) | PASS | `AReconciliationIssue_IsListedAndResolvedOnlyByItsOwnBranch` |
| GF-9a | `ix_sales_order_customer_idempotency` | `409 DuplicateSubmission` | PASS | `OrderingUniqueConstraintMappingTests` |
| GF-9b | Benign concurrency conflict: no reconciliation issue, `409` | zero issues | PASS | `ABenignConcurrencyConflict_OpensNoReconciliationIssue_AndAnswers409` |
| GF-9c | `BankTransferDisabled` over HTTP, via the branch's `orders.bankTransferEnabled` override | quote unavailable, `canPlace` false | PASS | `BankTransfer_TurnedOffForTheBranch_...` |
| GF-ACT | `Orders.ApprovePayment` roles = Billing's `Payments.Approve` (Accountant, Administrator) | table | PASS | `OrderingConventionTests` |

Notes. GF-7b has no HTTP row: it would need a test database with no active 0 % tax class, which the shared database can never be (every test branch creates one and other areas' tax classes persist). The throw happens before anything is written, in the one place both the placement and the transfer paths build charges.
## TEST gate (2026-10-10) -- cases handed over by the ARCHITECTURE re-check, and the full re-run

Environment. .NET 9 build green (0 errors). Database `catecommerce_ord` (Npgsql connection string in `CATECOMMERCE_TEST_DB`; never `catecommerce_dev`). In-process `WebApplicationFactory` host, real PostgreSQL 17 on localhost:5432. No broker, courier or SMS provider is involved in this slice. Unit suites: Domain 1773, Application 372, Infrastructure 172, all passed. Integration suite, full run: 1325 total / 1315 passed / 3 failed / 7 skipped (details below). New file: `tests/CatEcommerce.IntegrationTests/Endpoints/OrderingTestGateEndpointTests.cs` (12 tests).

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| T1 | Counter sales racing on one NEW mobile (6 rounds x 8 concurrent, fresh mobile each) | **all 8 sales 201**; one account per mobile; the retry sells; every sale has its invoice and one unit of stock; **no reconciliation issue** | PASS (F-2 fixed 2026-10-10) | Buyer resolved by `ICounterBuyerRegistrar` in its own short transaction before the sale scope; the loser re-finds the winner's account. Re-run after the fix: 12 of 12 class runs green |
| T2 | Sale to a mobile held by a non-Active (Merged) account | clean 400 `AccountNotActive` (cashier-facing, not the reset offer); stock and orders unchanged; **no reconciliation issue** | PASS (F-3 fixed 2026-10-10) | Owner question: follow the merge to the surviving account instead? |
| T3 | Reception cashier's customer lookup (FindCustomersQuery run as the cashier) | the existing customer is found, the sale is theirs, no second account | PASS | `T3_*`; also `ASaleToAKnownCustomer_*`. If the lookup returned nothing, registration would hit 23505 and refuse (T1/T2 show that outcome) |
| T4a | 8 scoped counter sales + 6 staff invoices at one branch, cold series | all 2xx; invoice numbers 1..14 | PASS (F-1 fixed 2026-10-10) | 12 of 12 class runs, plus the full-suite run |
| T4b | 3 scoped cancellations (each issues a credit note in Billing's scope) + 4 staff credit notes at one branch | all 2xx; credit-note numbers 1..7; no deadlock | PASS (F-1 fixed 2026-10-10; un-skipped, assertions unchanged) | Lock order now series row first on both paths (ADR-179 amended 2026-10-10); a second hole (lock taken before a concurrent creator's series row was visible) also closed |
| T4c | All four issuers together | as above | PASS (F-1 fixed 2026-10-10; un-skipped) | 12 of 12 class runs |
| T4f | Rolled-back unscoped attempt consumes no number | numbers stay contiguous 1..n after deadlock / collision retries | PASS | Asserted by the contiguity checks in T4a-T4e and `I_C3_ACollisionOnTheSeriesRow_IsRetriedWithTheNextNumber`; the unscoped transaction rolls back the series update with the document |
| T4d | 3 scoped cancellations alone | all 2xx; contiguous | PASS | 4 of 4 runs |
| T4e | 4 staff credit notes alone | all 2xx; contiguous | PASS | 4 of 4 runs |
| T5a | Approve a held transfer after Billing already settled the payment (PaymentNotPending) | 4xx `PaymentNotPending`; order keeps hold, status, version, no payments | PASS | `T5a_*` |
| T5b | The recorder rejects their own hold (Billing refuses: ApproverMustDiffer) | 4xx `ApproverMustDiffer`; hold intact; Billing payment still PendingApproval | PASS | `T5b_*` |
| T5c | A hold made before `held_payment_id` existed | 400 `NoPaymentAwaitingApproval`; nothing changes | PASS | `T5c_*` (legacy hold built by setting the aggregate property through reflection, saved, then approved over HTTP) |
| T6 | Cash refund on a fully paid, fully allocated invoice (payment Unallocated = 0 before) | Billing records a refund of the whole amount against the credit note | PASS | `T6_*`: precondition Unallocated = 0 asserted; afterwards Billing `refund` rows for the credit note sum to 40.00 on the order's payment |
| FULL-U | Domain, Application, Infrastructure unit suites | green | PASS | 1773 / 372 / 172 passed, 0 failed |
| FULL-I | Integration suite | green | FAIL (3) | `T4a` (F-1); `I_E11` p95 1976 ms under full-suite load, PASS alone (known load flake); `ShopStaffProductDetailReadEndpointTests.FU1_T1...` (another area's test, upload step `400`), PASS alone 3 of 3 |
| SKIP | Diet gate | un-skipped 2026-10-10 | PASS | `I_C4`, `I_D5`, `I_D5b` now run against the real PHM gate (see the I-D5 row). Still skipped, not ORD: `ClinicalRecordGateTests.G5` (known EMR defect), `BillingTestGateEndpointTests.XI_15` |

### Existing tests judged weak

- `I_G8` asserted the credit note and the order's refund total but never read Billing's refund rows, so it would pass if the draw plan returned nothing and the refund silently skipped Billing. T6 asserts the Billing rows (I_G8 itself is untouched).
- `I_E3` proves `SecondApproverMustDiffer` (Ordering's own check) but never reaches Billing's refusal path, so a Billing refusal that failed to roll back would go unnoticed. T5a and T5b drive Billing's refusal.
- `OrderingBillingNumberingEndpointTests` is correct but not stable in a full run (F-1); it is not weakened.

## Acceptance criteria (handover section 18)

Evidence is the handover's own; status is from the 2026-10-10 full run.

| Criteria | Covered by | Status |
|---|---|---|
| A1-A4, A6-A13, A15-A19, A21-A26, A28-A33, A35-A40, A42-A46 | the unit and integration cases the handover names | PASS |
| A14 (no card data stored) | I-D14 | PASS |
| A5, A41 (prescription line needs its cat; authorisation lapse) | U-D2 unit PASS; I-C4, I-D5, I-D5b PASS against the real HO-PHM-001 gate (2026-10-10) | PASS |
| A20 (refused COD parcel to quarantine) | I-F9 | PASS (disposition moved to Inventory, gate fix 3) |
| A27 (courier API shipment) | I-F4 (tracking number only) | NOT MET by design (SRS Q-4); `ICourierGateway` not declared, backlog ORD 30 |
| A34 (restock / write-off) | removed, gate fix 3 | REMOVED |
| A21 and A22 under contention | I-D8, I-E7; plus T1, T4 | PASS alone; contention behaviour in F-1, F-2 |

## Counts at the last full run

TEST gate run (2026-10-10): see docs/gates/HO-ORD-001-TEST.md for the totals.

## Merge of master (PHM-001 + SHP-001-FU3), 2026-10-10

- New: `OrderingCatalogueShippingWeightEndpointTests` -- `ICatalogueLookup` carries the SKU's `ShippingWeightGrams` Shop published (CONTRACTS #25), null when unset. PASS.
- Fixed while un-skipping I-D5: a cart holding only a Rx line without a cat was refused as `NothingToPlace`; `CheckoutPricing` now reports `PrescriptionCatRequired` instead (spec I-D5).
- Ordering's `CatalogueSku` record renamed `OrderCatalogueSku` (Pharmacy merged its own `CatalogueSku` into the same namespace).
- Post-merge runs (2026-10-10, `catecommerce_ord`): unit Domain 1871 / Application 372 / Infrastructure 174 passed, 0 failed. Integration 1425 passed, 2 skipped (`ClinicalRecordGateTests.G5`, `BillingTestGateEndpointTests.XI_15`, both not ORD), 1 failed in the full run (`I_E11` p95 2979 ms under load, known flake) and PASS alone.
- `BillingTestGateTraceEndpointTests.XI_1b`: the vacuous `Assert.True(retries >= 0)` was replaced by 40 distinct numbers plus no unhandled numbering exception in the hosts' logs.

## FU1 -- owner decisions of 2026-10-10 (branch `ho/ORD-001-FU1`)

Per-branch database `catecommerce_ordfu1` (migration `SeedDeliveryTaxClass`, data only). Handover row: HO-ORD-001 section 1 "Amended (owner decisions 2026-10-10, FU1)".

| Case | Decision | Level | Proves | Status |
|---|---|---|---|---|
| `D1_ACounterSale_ToAMobileHeldByAMergedAccount_SellsToTheSurvivingAccount` | D1 | integration | order and invoice belong to the survivor; no new account; no reconciliation issue | PASS |
| `D1_AChainOfMerges_IsFollowedToTheLastAccount` | D1 | integration | A -> B -> C sells to C | PASS |
| `D1_AMergedAccount_WhoseSurvivorIsNotActive_KeepsTheCashierFacingRefusal_AndNothingMoves` | D1 | integration | survivor Anonymised: `400 AccountNotActive`, stock and orders unchanged | PASS |
| `D1_ALoopOfMerges_IsARefusal_NotAnEndlessWalk` | D1 | integration | A <-> B terminates with `AccountNotActive` | PASS |
| `T2_ACounterSale_ToAMobileHeldByANonActiveAccount_...` (kept) | D1 | integration | a merged account with **no** recorded survivor still refuses cleanly | PASS (unchanged assertions) |
| `CompleteCounterSaleBuyerTests` (4 unit) | D1, D2 | unit | registrar called before the business checks; existing buyer not re-registered; invalid mobile registers nobody; inactive-account refusal precedes pricing | PASS |
| `D2_ASaleRefusedForStock_StillStoresTheWalkIn_AndTheRetryReusesIt` | D2 | integration | refused sale (409 `StockNoLongerAvailable`) leaves one passwordless account named as typed, no order, no stock change; the retry sells to the same account | PASS |
| `D2_ASaleRefusedForItsTenders_StillStoresTheWalkIn` | D2 | integration | a tender refusal also stores the buyer | PASS |
| `D2_ARequestThatIsNotShapeValid_StoresNobody` | D2 | integration | blank name / invalid mobile store nothing | PASS |
| `D2_TheStoredWalkIn_WhoLaterRegistersOnline_WithTheSameMobile_IsLinkedToTheSameAccount_NotDuplicated` | D2 | integration | online registration -> `MobileNumberAlreadyRegistered` + reset offer; reset token goes to the walk-in account; after the reset the person signs in as the same account id; one account throughout | PASS |
| `ARefusedSale_StillLeavesTheWalkInAccountBehind_AndNothingIsSold` | D2 | integration | **replaces** `ARefusedSale_LeavesNoWalkInAccountBehind` (asserted 0 accounts) | PASS |
| `TheDeliveryTax_DefaultsToZeroPercent_...`, `AConfiguredDeliveryTax_IsAddedToTheTaxTotal...`, `ThePickupOrder_HasNoDeliveryCharge_...` | D3 | unit | 0 % leaves totals unchanged; 15 % of 60.00 = 9.00 added to tax and grand totals, half-away rounding (50.10 -> 7.52), fingerprint changes; pickup unaffected | PASS |
| `U_D4_ADeliveryTax_IsPartOfTheTaxTotal_OnlyWhenTheOrderDeclaresIt` | D3 | unit | the aggregate's re-sum accepts the declared delivery tax and still refuses an undeclared or mis-declared one | PASS |
| `ADeliveryCharge_WithNoTaxClassAtAll_IsAConfigurationFault_NotABusinessRefusal` | D3 | unit | **replaces** `ADeliveryCharge_WithNoZeroRateTaxClass_IsABusinessRefusal...` (asserted `DeliveryTaxClassNotConfigured`): now `BillingSeamException` | PASS |
| `D3_TheDeliveryTax_StartsAtZeroPercent_AnAdminCanChangeIt_AndTheNextOrderAppliesIt` | D3 | integration | default 0 % with no configuration; Admin posts 15 % through `POST /api/admin/tax-classes/{id}/rates`; the next quote, order and invoice all carry 9.00 more tax and agree to the cent | PASS |
| `D3_ATransferOrder_IsInvoicedAtTheDeliveryTaxInForceWhenItIsConfirmed_...` | D3 | integration | transfer confirmation invoices the delivery line and the totals agree | PASS |
| `D3_TheDeliveryTaxClass_IsSeededAtZeroPercent` | D3 | integration | the migration's seed: one rate, 0 | PASS |
| `D4_ACodCollection_AboveTheApprovalThreshold_IsRecordedAsReceived_WithNoPendingApproval` | D4 | integration | threshold 1000.00, COD 2135.00: payment `Accepted`, invoice paid, order `Delivered` | PASS |
| `D4_ACounterTender_AboveTheApprovalThreshold_IsRecordedAsReceived_WithNoPendingApproval` | D4 | integration | threshold 50.00, cash 300.00: payment `Accepted`, order `Delivered` | PASS |
| `D4_ABankTransfer_AboveTheThreshold_IsStillHeldForASecondPerson` | D4 | integration | transfers unchanged: `awaitingApproval`, payment `PendingApproval` | PASS |

Note on D3 isolation: the D3 test creates its 15 % rate effective two minutes ahead and moves its own factory's clock past it, then deletes it (the shared database has one global `DELIVERY` class). Other test classes use real time and never see it.

Runs (2026-10-10, `catecommerce_ordfu1`): Domain 1875, Application 380, Infrastructure 174 passed, 0 failed. Integration 1442 passed, 2 skipped, 2 failed in the full run: `ClinicalHistoryPerformanceTests.I_E4` (a p95 budget; passes alone) and `CatLocalDateEndpointTests.RecordCatWeight_LocalTodayJustAfterMidnightInDhaka_...` (not FU1: it pins the clock to 2026-10-09T19:00Z and gets `401` registering its cat, so it is time-of-day dependent; fails alone too, untouched by this branch).

### FU1 fix round (2026-10-10, migration `AddOrderDeliveryTaxAndPaymentWaiver`; backlog ORD 52-56)

| Case | Item | Level | Proves | Status |
|---|---|---|---|---|
| `AWaivedApproval_AcceptsAboveTheThreshold_AndRecordsTheReasonAndAnEvent`, `AnOrdinaryPayment_HasNoWaiverReasonAndNoWaiverEvent` | ORD 52 | unit | waiver reason stored, `PaymentApprovalWaivedEvent` raised; ordinary payments carry neither | PASS |
| `AWaiver_ForAnotherMethodOrAnUnknownReason_IsRefused` (5 rows) | ORD 53 | unit | `ApprovalWaiverNotAllowed` for BankTransfer (either reason), COD with CounterTender, cash with CodCollection, unknown reason | PASS |
| `D4_ACodCollection_...` / `D4_ACounterTender_...` (extended) | ORD 52 | integration | payment row has `CodCollection` / `CounterTender`; one audit row `PaymentApprovalWaived` with `Reason=` | PASS |
| `D4_ABankTransfer_AboveTheThreshold_IsStillHeldForASecondPerson` (extended) | ORD 53 | integration | transfer payment has no waiver reason | PASS |
| `TaxClassAggregateTests` (2) | ORD 54a | unit | DELIVERY cannot be deactivated (`DeliveryTaxClassRequired`), can be renamed; other classes can be deactivated | PASS |
| `D3_TheDeliveryTaxClass_CannotBeDeactivated` | ORD 54a | integration | `PUT /api/admin/tax-classes/{DELIVERY}` with `isActive=false` -> 400 `DeliveryTaxClassRequired`, class still active | PASS |
| `ADeliveryChargeWithNoUsableDeliveryTaxClass_BlocksTheQuote_ButPickupDoesNot` | ORD 54b/c | unit | no class -> `DeliveryTaxNotConfigured`, `CanPlace=false`; pickup unaffected | PASS |
| `U_D4_TheDeliveryTax_IsStoredOnTheOrder_AndIsBoundedByTheDeliveryCharge` | ORD 55 | unit | stored; refused with no charge, above the charge, or negative | PASS |
| `D3_AHeldTransfer_ConfirmedAfterARateChange_IsInvoicedAtTheDeliveryTaxItWasQuotedAt` | ORD 56 | integration | placed at 0 %, rate raised to 15 %, confirmed: invoice total and tax equal the order's | PASS |
| `D3_TheDeliveryTax_StartsAtZeroPercent_...` (extended) | ORD 56 | integration | `delivery_tax_amount` is 0 at 0 % and equals the delivery tax at 15 % on the order row | PASS |

Not covered by an automated test: the `delivery-tax-class` health check (Degraded plus a log warning when the class is unusable) and the reader returning `(null, 0)` for an inactive class (the integration database cannot deactivate the shared class without disturbing parallel classes); both are small and were checked by reading.
