GATE: ACCEPTANCE
HANDOVER: HO-ORD-001
COMMITS: ho/ORD-001 at a24a2f3c (merge-base with master e6eeb192; 319 files). Phases 663cc93f..7b357ce7; bookkeeping 900c4c5a; gate-fix round cfb8aea9, cff232bd, 31d03485; TEST 64231f11; TEST fix round 070557e2; master merge 5a38cc2e; delta review 7d14da4c; tidy a24a2f3c
DATE: 2026-10-10
STATUS: PASS

PRIOR GATES (read from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: FAIL at 900c4c5a (1 BLOCKER, 4 HIGH). RE-CHECK 1 PASS covers cfb8aea9, cff232bd and 31d03485. Points to the delta review.
- SECURITY: PASS at 900c4c5a (2 MEDIUM, 5 LOW, all in the backlog). Points to the delta review.
- CODE_REVIEW: PASS at 900c4c5a. The "Delta review" section is a PASS for 64231f11, 070557e2 and 5a38cc2e (fix diff 85c74f53..070557e2 plus a remerge-diff of 5a38cc2e).
- TEST: PASS at 85c74f53 (F-1..F-4: MEDIUM/LOW). TC-HO-ORD-001.md records the re-runs after the fixes. T1, T2 and T4a-c PASS (12 of 12 class runs). I-C4, I-D5 and I-D5b are un-skipped and PASS against the real PHM gate. Post-merge: unit tests 1871/372/174 all pass. Integration: 1425 passed, 1 failed (`I_E11` p95 under load, passes alone, backlog 44) and 2 skipped (not ORD).

DELTA COVERAGE (every commit after the full gates at 900c4c5a)
- dc0e4e1f: master only, not ORD code.
- cfb8aea9, cff232bd and 31d03485: ARCHITECTURE re-check 1. The owner rule of 2026-10-07 says a re-check after a HIGH covers only the failing gate's delta. TEST at 85c74f53 then exercised that code.
- 85c74f53, 64231f11 and 7d14da4c: gate records and tests only.
- 070557e2 and 5a38cc2e: the combined delta review (PASS).
- a24a2f3c: test and doc edits only (two Billing tests, CONTRACTS numbers, backlog). The owner's tiny-fix rule applies, so no gate is required. Its run is not recorded (backlog 49).
- Coverage complete. No production code changed after the last review.

OWNER AUTHORISATION FOR THE §14 FILES
- ARCHITECTURE re-check 1 conditioned its PASS on the owner having authorised Billing and Inventory edits from this branch.
- That authorisation is recorded in the HO-ORD-001 §1 "Amended (TEST fix round)" row: "owner confirmed on 2026-10-10 that the ORD branch may make its additive edits to Inventory's `IStockLedger` and to the Billing files, including this fix round's Billing change". The coordinator's brief relays the same decision.
- The condition is met.

CRITERIA (§18)
- A1 MET: `CartTests.U_C2_Merge_GuestA2B1_IntoAccountA3C1_GivesA3B1C1_WithNoDuplicateLine`, `U_C3_Merge_BeyondTheLineCap_*` and `OrderingCartEndpointTests.I_C2_*`, which asserts one line per SKU with the larger quantity.
- A2 MET: `I_C3_TheSameCustomerOnASecondDevice_SeesTheSameCart` asserts that an edit on one client shows on the other, with one Active cart.
- A3 MET: `U_D2_APriceRise_*`, `U_D3_*` and `U_D5_*`. `I_D1_PlacingWithAStaleFingerprint_IsRefusedAfterAPriceRise_AndTheNewQuoteIsAccepted` asserts 409 `CheckoutQuoteChanged`, then 201.
- A4 MET: `U_B4_*` (three tests) and `I_D4_*`, which asserts pickup 201 and a slot not offered gives `DeliverySlotUnavailable`.
- A5 MET:
  - `U_D2_APrescriptionLineWithoutACat_MakesCanPlaceFalse_UntilTheCatIsSet`.
  - `OrderingPlacementEndpointTests.I_D5_APrescriptionLineWithoutACat_CannotBePlaced` runs with no Skip. It asserts `canPlace=false`, `PrescriptionCatRequired` in `blockingReasons`, placement refused with `PrescriptionCatRequired` and zero orders.
  - Real gate: `PharmacyModule.cs:184` registers `DietAuthorisationGate`, and no test factory overrides it.
- A6 MET: `U_D1_TheWorkedCart_HasEveryComponentSeparate_AndTheDocumentedTotal`. `I_D2_*` (quote and order) asserts each component and the 1927.50 worked total.
- A7 MET: `MinorUnitsTests.U_A1_*`, `U_A2_*` and `U_A3_*` (34/33/33, 1/0/0, exact sums).
- A8 MET: `DiscountTests.U_B6..U_B10_*`, `I_B2_AShopManagerManagesDiscountCodes_AnInventoryOfficerCannot` and `I_D12_*`.
- A9 MET: `U_B6_EachFailingRule_ReturnsItsCodeAndAPlainMessage_InTheDocumentedOrder`. `I_D12` re-quote asserts `reasonCode = DiscountUsageLimitReached`.
- A10 MET: `U_B2_*` and `U_B3_*`. Weight is now applied: `CheckoutPricingTests.TheDeliveryCharge_AddsTheWeightBandSurcharge_*` and `ASkuWithoutAShippingWeight_*`. `OrderingCatalogueShippingWeightEndpointTests` covers Shop's weight (CONTRACTS #25). Also `I_D2_*`.
- A11 MET: `U_B1_NoMatch_*` and `I_D3_AnAddressOutsideEveryZone_MakesDeliveryUnavailable_AndPickupIsOffered`.
- A12 MET: `U_B5_*`, `I_D7_*`, `I_E1_*`, `I_D4_*` and `I_F12_*`. The last asserts `CardInStore` with slip reference SLIP-77123 and stock issued at collection.
- A13 MET: `U_B5_CodNotOfferedInTheZone`, `U_B5_CodAboveTheCeiling_NamesTheLimit`, `I_D6_CodInAZoneWithoutCod_*`, `I_D6_CodAboveTheCeiling_*` and the placement `I_D6_*`.
- A14 MET:
  - `I_D14_*` scans the `ordering` and `ops` columns for card, pan, cvv, wallet and token names, and the stored bodies for 13-19-digit runs.
  - `OrderingConventionTests.ThereIsNoGatewayCardWalletOrTokenType_*`.
  - SECURITY found C-5 clean.
- A15 MET: `I_E1_*` asserts `PendingPayment`, sellable stock reduced by 2, the account number and the payment reference (the order number), and `order.awaiting-transfer`.
- A16 MET:
  - `I_E2_*` asserts `Confirmed` and one `order.confirmed` notification.
  - `I_E3_*` and `ARejectedHeldConfirmation_*` (second approver).
  - `T5a`, `T5b` and `T5c` (Billing refusals).
- A17 MET:
  - `U_E1_*`.
  - `I_E4_*` asserts `Cancelled`, stock sellable again, `order.expired` and `canReorder`.
  - `I_E5_*` (a held order is not expired).
  - `I_E6_*` (order again).
- A18 MET: `I_D7_*` (COD confirmed at once) and `I_F8_*`.
- A19 MET: `U_F2_*`. `I_F8_*` asserts the collector "Rider Rahim" on the order and the Billing payment `CashOnDelivery`, plus `codCollection` on the detail.
- A20 MET:
  - `U_F3_*`.
  - `I_F9_ARefusedCodParcel_*` asserts `Returned`, a `DeliveryFailure` return, the invoice credited and 2 units on hand in quarantine.
  - Disposition after quarantine is Inventory's (gate fix 3).
- A21 MET:
  - `I_D8_WhenBillingCannotIssueTheInvoice_NothingOfThePlacementSurvives` uses the real seam (no billing profile). It asserts a 5xx and zero orders, charges, reservations and notifications, with stock and cart unchanged.
  - `I_D7_*` and `I_E2_*`.
  - Under contention: `T4a-c` and `OrderingBillingNumberingEndpointTests`.
- A22 MET: `I_E7_*` asserts a 5xx, the order still `PendingPayment` and an `Open` issue that can be listed and resolved. Also `ABenignConcurrencyConflict_OpensNoReconciliationIssue_*` and `AReconciliationIssue_IsListedAndResolvedOnlyByItsOwnBranch`.
- A23 MET: `OrderTransitionRulesTests.U_A5_*` (exactly 14 arrows, no ninth state) and `I_F1_*`.
- A24 MET: `U_A5_AnInvalidMove_*`, `U_A6_EveryTransition_AppendsExactlyOneAttributedTimestampedRow` and `I_F1_*`. `I_F1` asserts `changedBy` equals the reception user and the second pack gives `InvalidOrderTransition`.
- A25 MET: `I_F2_ThePickList_GroupsByLocationThenBatch_*`.
- A26 MET: `I_F3_ThePackingSlip_ShowsTheContentsWithBatchNumbers_TheCustomerAndTheCodAmount`.
- A27 MET for the half §18 defines:
  - `I_F4_DispatchWithACourierAndTracking_ShowsTheNumberAndTheCourierLinkToTheCustomer`.
  - Courier-API shipment creation is NOT met, by design (SRS Q-4). The gap is written into A27 itself, §4.3 and §18's deferred list. It is recorded here, not passed.
  - `ICourierGateway` is not declared although §4.3/§13 say it is. ARCHITECTURE L2 recorded this as backlog 30 (LOW).
- A28 MET: `I_F5_*` asserts the CSV carries the order and its COD amount 2135.00, tracking added later and one `order.tracking-added`. `TheManifest_GuardsAgainstSpreadsheetFormulas`.
- A29 MET:
  - `U_E2_*` and `U_E3_*`.
  - `I_E8_*` asserts stock released, the credit note on the order and `order.cancelled`.
  - `I_E9_*` asserts `refundDue` 2135.00 and `order.cancelled-refund-due`.
- A30 MET: `U_E2_Cancel_ByTheCustomerFromPacking_IsCancellationNotAvailable_*` and `I_E10_*`.
- A31 MET: `ReturnsAndRefundsTests.TheWindow_IsInclusiveOfTheLastDay` (`ReturnWindowClosed`) and `I_G1_*`.
- A32 MET: `ReturnsAndRefundsTests` cover opened food (`ReturnNotEligibleOpenedFood`), recalled batch, quality defect and used litter. Also `I_G2_*`, `I_G3_*` and `UsedLitterAndAMixedRequest_*`.
- A33 MET: `I_G4_*` asserts 1 unit in quarantine with sellable unchanged. `I_G4b_*` and `I_F9_*`.
- A34 REMOVED by owner decision 3 (Amended gate fix 3; §18 row struck). Disposition is Inventory's quarantine workflow (OQ-19, HO-IMP-001 §8.8). GF-3 confirms the route is gone. This is not counted as met; it is no longer this slice's criterion.
- A35 MET:
  - `ReturnsAndRefundsTests` `RefundMethodPolicy.Evaluate` cases.
  - `I_G7_*` (options follow how the customer paid; store credit allowed but unavailable).
  - `I_G8_*` (`RefundMethodUnavailable`).
- A36 MET:
  - `I_G8_*` asserts the amount, the credit note linked on the order and in Billing, a `RefundIssued` audit row and the over-refund refusal.
  - `I_G9_*` asserts that the refund row is stored once.
  - `T6` asserts that Billing's refund rows sum to the amount.
  - The method, reference, reason and staff stored on the refund are shown by source: `OrderRefundAggregate.Issue` sets `Method`, `Reference`, `Reason`, `IssuedBy` and `CreditNoteId`, and `AuditOrderMoneyEventsHandler` records `ActorUserId`.
  - Test strength is weak on those fields (backlog 50).
- A37 MET:
  - `PosTests` and `U_H2_*`.
  - `I_H1_*` asserts change 102.50, `Delivered`, 3 history rows, and ledger entries of kind `Sale`/`PosSale` against the FEFO batch with a paid invoice.
  - `I_H3_*` asserts that every receipt line is at most 48 columns.
- A38 MET: `I_H1_*` asserts that the barcode returns the SKU and a FEFO batch from its batches, and an unknown barcode gives 404.
- A39 MET:
  - `OrderingEdgeEndpointTests.U_X2_*` (including 20 concurrent submissions of one key).
  - `I_D10_*` asserts one order and a replay with the same `orderId`. Its loop retries only on setup `PolicyAcceptanceRequired`, not on the race. CODE_REVIEW judged that compliant with §20.3.
  - `I_D11_*` (422) and `I_H4_*`.
- A40 MET: `I_F10_AnEventDeliveredTwice_IsProcessedOnce_*` and `I_F11_ABadSignature_IsRefused_NothingIsWritten_*`.
- A41 MET:
  - Cat at add-to-cart, recorded on the line: `OrderingCartEndpointTests.I_C4_AnAuthorisedCat_AddsTheLine_AndTheCartShowsTheCat` uses a real `DietAuthorisationAggregate` judged by the real gate. It asserts the line's cat id and `prescriptionStatus = Authorised`. `I_D5_AnAuthorisedPrescriptionLine_IsPlaced_*` asserts the order line carries the cat.
  - Expired authorisation blocked: `I_D5b_ALapsedAuthorisation_BlocksThePrescriptionLineAtPlacement` asserts 409 on the old quote, zero orders, a fresh quote excluding the line with `PrescriptionAuthorisationExpired`, and `NothingToPlace`. Also `U_D2_ALapsedPrescriptionAuthorisation_*`.
  - Refusals: `I_C4_APrescriptionSku_NeedsASignedInCustomerAndACat` and `I_C4_AnotherCustomersCat_IsA404_*`.
  - Delegation assumption (OQ-12) recorded: HO-SHP-001 lines 125-126 place US-SHP-10 AC2 and AC3 on HO-ORD-001, so the assumption holds.
- A42 MET: `I_D9_*` runs 20 real parallel placements behind a start gate. It asserts exactly one 201, every loser 409 (`StockNoLongerAvailable` or `CheckoutQuoteChanged`), one order and sellable 0. `TransactionalScopeTests` covers the commit-time 23514 path deterministically.
- A43 MET: `I_F6_*` (`BatchRecalled` blocks dispatch), `I_F7_*` (the recall trace lists order, customer and cat) and `OrderStockTests.BR14_*`.
- A44 MET:
  - `I_F6_*` covers `ReallocationReasonRequired` and 403 without the action.
  - `InventoryStockPortEndpointTests.ReserveBatch_*_AndTheOverrideIsFlaggedAndAudited` asserts `FefoOverridden` and its audit row.
  - `AuditOrderMoneyEventsHandler` records `OrderLineReallocated`.
- A45 MET: `I_E4_*`.
- A46 MET: `I_D13_APendingTermsVersion_BlocksPlacement_UntilTheCustomerAcceptsIt` (`PolicyAcceptanceRequired`, then 201).
- US-ORD-17 deferral is named in §4.2 and §18 (HO-BIL-001, HO-BRD-001).

SCOPE
- Out-of-scope work found: none.
  - No `web/**`, csproj, sln or NuGet change.
  - No gateway, card or token type.
  - No courier adapter, combined invoice, store credit or cart reminder built.
  - C24 restock/write-off was removed.
- Files outside the literal §13 list: all were recorded by earlier gates. None is unrecorded.
  - backlog 26 and 38 (MEDIUM);
  - the delta review's `ICounterBuyerRegistrar`/`AccountsCounterBuyerRegistrar`;
  - the §14 Billing/Inventory/handover files, accepted under the owner's 2026-10-10 confirmation.

FINDINGS
- [LOW] docs/test-cases/TC-HO-ORD-001.md:127: describes XI_1b assertions that a24a2f3c removed. No run of a24a2f3c's changed tests is recorded. Backlog 49.
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/OrderingReturnsEndpointTests.cs `I_G9_*`: does not test its §16 row (Refunded, audit names staff). A36's field-level proof rests on source reading. Backlog 50.
- [LOW] docs/gates/HO-ORD-001-ARCHITECTURE.md:215,217: edited by a non-gate commit (a24a2f3c). The annotations are accurate. Backlog 51.
- Note (not a finding of this gate): `I_E11` fails intermittently under full-suite load (backlog 44). The owner rule "nothing merges with a failing test" is the integrator's to apply at merge.

OPEN OWNER QUESTIONS (flagged, not blocking)
- A mobile held by a Merged account: follow the merge to the survivor, or refuse? Today the code refuses with `400 AccountNotActive`.
- An orphan walk-in account after a refused or failed sale (backlog 47).
- 0 % tax class for delivery: today `400 DeliveryTaxClassNotConfigured` until an Admin creates one (OQ-6 default said delivery is untaxed).
- COD or counter amounts above Billing's approval threshold stay Pending (OQ-7).
- BR-3 in Ordering.

UNDECLARED DEVIATIONS
- None found.
- `ICourierGateway` is undeclared, against §4.3 and §13. ARCHITECTURE L2 already declared this (backlog 30).

SCOPE CHECK
- files in allowed scope: 319/319 accounted for. They are §13 paths, ADR-341 partials, owner-approved §14 edits, or extras recorded in backlog 26/38 and the delta review.
- files outside allowed scope: none unrecorded.
