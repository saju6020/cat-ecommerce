# TC-HO-IMP-001 — Inventory (backend)

Handover: docs/handover/HO-IMP-001.md (§15 unit cases, §16 integration cases, §18 acceptance criteria)
Commit(s) under test: branch `ho/IMP-001` at 9c061270 (plus the TEST gate's test-only additions, committed with this sheet)
Written: 2026-10-07 (the implementer's first sheet, rewritten by the TEST gate)  ·  Executed: 2026-10-07

> Statuses record what was **observed**, not inferred. The Evidence column names the test that was run and watched. `BLOCKED` marks a case that could not be
> executed — it is not a failure, and it is not hidden. This sheet replaces the implementer's self-assessment (which used PARTIAL / NOT BUILT); every §15/§16 case
> now has its own row. Where a §15 "unit" case is only coverable against the real database or the real resolver, the row says "integration-level" and still
> counts as PASS because the §15 expectation was observed, not because a unit test of that exact shape exists.
>
> How rows were verified: every test named below was run (suites listed under Environment) and passed. The assertions of the tests the earlier gates and this gate
> singled out were read and judged (I-E4, I-E5, I-E4/mixed, I-I7, I-I2 released-reservation, FI-1…FI-5, I-D5, I-D6, the alert race, ObservedOn, the read-side convention
> test). Other rows rest on the test name and a green run; their assertions were not individually re-read.

## Summary

| | Count |
|---|---|
| Total | 160 |
| PASS | 158 |
| FAIL | 0 |
| BLOCKED | 2 |
| NOT RUN | 0 |

(Rows: 64 unit, 70 integration, 11 regression, 15 additional TEST-gate cases.)

## Environment

- Build: `dotnet build CatEcommerce.sln`, 0 warnings, 0 errors.
- Database: `catecommerce_imp` only (`CATECOMMERCE_TEST_DB` exported in the same command as every run). `catecommerce_dev` never used. PostgreSQL reachable; the database was already migrated to the branch's migrations.
- Unit suites (`--no-build`): Domain 1065/1065, Application 197/197, Infrastructure 119/119.
- Integration, Inventory classes (`FullyQualifiedName~Inventory`): 134/134 (121 existing + 13 added by this gate); the 133 run before the last added test were green four times in a row.
- Integration, whole project (run before the last four added tests): 880 passed, 2 failed — both outside Inventory, both passed when re-run alone (see T15).
- Hosts: the ordinary `InventoryApiFactory` host and its port host (real HTTP via TestServer, real Postgres, real triggers). Workers off; `InventoryWorkCycle` and `NotificationWorkCycle` driven directly. Not started: a standalone `dotnet run` host, an empty database.

## Unit cases (§15)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | `StockLocationRules` over kind × `MovementKind` | §8.3's table | PASS | `FoundationTests.U_A1_StockLocationRules_IsExactlyTheTableOfSection8_3` |
| U-A2 | `StockQuantity` bounds, 3 dp, fractional on whole-only SKU | `InvalidQuantity` / `FractionalQuantityNotAllowed` | PASS | `FoundationTests.U_A2_*` (3 tests) |
| U-A3 | `UnitCost` 4 dp, negative, no `double` | kept / refused / none | PASS | `FoundationTests.U_A3_*` (3 tests) |
| U-A4 | Storage profile min 8 max 2, −50 | `InvalidTemperatureRange` ×2 | PASS | `FoundationTests.U_A4_StorageProfile_RefusesAnInvertedOrOutOfBoundsRange` |
| U-A5 | `LocalDateOf("Asia/Dhaka", 2026-10-11T18:30Z)` | 2026-10-12 | PASS | `FoundationTests.U_A5_BranchCalendar_LocalDateOf_DhakaIsSixHoursAhead` |
| U-B1 | Supplier country `XX`, currency `usd`, lead time 400 | three refusals | PASS | `ProcurementTests.U_B1_*` (5 tests) |
| U-B2 | PO transitions | only §8.5's arrows | PASS | `ProcurementTests.U_B2_PurchaseOrderTransitions_AreExactlyThoseOfSection8_5` |
| U-B3 | No lines; delivery yesterday | `PurchaseOrderLinesRequired`; `ExpectedDeliveryInPast` | PASS | `ProcurementTests.U_B3_*` (2 tests) |
| U-B4 | Two lines, one SKU | `DuplicatePurchaseOrderSku` | PASS | `ProcurementTests.U_B4_TwoLinesForOneSku_AreRefused` |
| U-B5 | ordered 100, accepted 60, rejected 10 | outstanding 40, `PartiallyReceived` | PASS | `ProcurementTests.U_B5_PurchaseOrderProgress_Ordered100Accepted60Rejected10_Outstanding40_PartiallyReceived` |
| U-B6 | Cancel with receipts; close without reason | `PurchaseOrderHasReceipts`; `CloseReasonRequired` | PASS | `ProcurementTests.U_B6_CancelWithReceiptsIsRefused_AndCloseNeedsAReason` |
| U-C1 | Consignment transitions; clear without permit | arrows; `ClearanceDocumentsMissing` | PASS | `ConsignmentTests.U_C1_*` (5 tests) |
| U-C2 | HS codes `2309.10`, `230910`, `23.09`, `ABCD` | valid, valid, invalid, invalid | PASS | `ConsignmentTests.U_C2_HsCodePattern` |
| U-C3 | 51st document; html; 21 MB | three refusals | PASS | `ConsignmentTests.U_C3_*` (2 tests) |
| U-C4 | Supersede keeps the old; no remove method | present, `SupersededOn`; none by reflection | PASS | `ConsignmentTests.U_C4_*` (2 tests) |
| U-C5 | Replace costs after allocation | `CostsChangedSinceAllocation` | PASS | `ConsignmentTests.U_C5_ReplacingCostsAfterAnAllocationMarksItStale_AndReallocatingClearsIt` |
| U-D1 | Food SKU, accepted 10, no expiry | `ExpiryDateRequired` on `Lines[0].ExpiresOn` | PASS | `GoodsReceiptTests.U_D1_AFoodSkuAccepted10WithNoExpiry_IsExpiryDateRequired_OnTheLinesExpiresOnProperty` |
| U-D2 | General SKU, no expiry | accepted | PASS | `GoodsReceiptTests.U_D2_AGeneralSkuWithNoExpiry_IsAccepted` |
| U-D3 | Expiry yesterday; manufactured after expiry / tomorrow | `BatchAlreadyExpired`; `InvalidBatchDates` ×2 | PASS | `GoodsReceiptTests.U_D3_*` (3 tests) |
| U-D4 | Rejected 2 no reason; rejected-only line | `RejectionReasonRequired`; no batch | PASS | `GoodsReceiptTests.U_D4_*` (3 tests) |
| U-D5 | Accepted beyond outstanding | `ReceiptExceedsOutstanding` | PASS | `GoodsReceiptTests.U_D5_AcceptedBeyondTheOutstandingQuantity_*` |
| U-D6 | Three invalid lines | all three returned together | PASS | `GoodsReceiptTests.U_D6_ReceiptWithThreeInvalidLines_ReturnsAllThreeViolationsTogether` |
| U-D7 | `BatchTransitionRules` every pair; `WithdrawRecall` → `Quarantined` | §8.6.1 only | PASS | `BatchAndLedgerTests.U_D7_*` (5 tests) |
| U-D8 | `MovementKindRules.Sign` for all kinds; wrong-sign draft | table; refused | PASS | `BatchAndLedgerTests.U_D8_*` (4 tests) |
| U-D9 | Reflection over ledger and reservation entries | no setter, no method but the factory | PASS | `BatchAndLedgerTests.U_D9_LedgerAndReservationEntries_HaveNoPublicSetterAndNoPublicMethodButTheFactory` |
| U-E1 | FEFO order incl. tie by earlier receipt, undated last | 2026-12 (earlier receipt), 2026-12, 2027-03, undated | PASS | `FefoAllocatorTests.U_E1_FefoOrder_IsSoonestExpiryFirst_EarlierReceiptBreaksATie_UndatedGoodsLast` |
| U-E2 | `BatchExpiryRules` at the expiry date and the day after | not expired / expired | PASS | `FefoAllocatorTests.U_E2_ABatchIsUsableThroughItsWholeExpiryDate_AndExpiredFromTheNextBranchLocalDay` |
| U-E3 | FEFO skips expired, quarantined, recalled, zero-free | not in plan | PASS | `FefoAllocatorTests.U_E3_Fefo_SkipsExpiredQuarantinedRecalledWrittenOffAndNothingFreeCandidates` |
| U-E4 | Demand 15 over 10 and 8 free | 10 + 5 | PASS | `FefoAllocatorTests.U_E4_*` |
| U-E5 | Demand 30 over 18 free | shortfall 12; `InsufficientStock` mentioning 18 | PASS | `FefoAllocatorTests.U_E5_*` (2 tests) |
| U-E6 | Explicit plan equal / different without / with reason | unflagged; reason required; flagged | PASS | `StockDemandPlannerTests.U_E6_*` (2 tests) |
| U-E7 | Explicit plan naming an expired batch with a reason | `BatchExpired` | PASS | `StockDemandPlannerTests.U_E7_AnExplicitPlanNamingAnExpiredBatch_IsBatchExpired_NoReasonChangesIt` |
| U-E8 | `Sale` at dispensary; `Dispense` at shop floor | `MovementNotPermittedAtLocation` | PASS | `StockDemandPlannerTests.U_E8_*` |
| U-E9 | Reflection for force/bypass/ignore/allowExpired/override | none | PASS | `StockDemandPlannerTests.U_E9_*` (2 tests) |
| U-E10 | Party rules | `StockPartyRequired` ×2; POS allowed | PASS | `StockDemandPlannerTests.U_E10_*` (2 tests) |
| U-E11 | Consume a reservation of a batch recalled after reserving | `BatchNotUsable` | PASS | `StockDemandPlannerTests.U_E11_ConsumingAReservedBatchThatWasRecalledAfterReserving_IsBatchNotUsable` |
| U-F1 | Adjustment without reason / note; `Found` negative; `Theft` positive | four refusals | PASS | `StockControlTests.U_F1_*` (4 tests) |
| U-F2 | 4 999.99 / 5 000.01 at threshold 5 000.00 | `Posted` with drafts / `PendingApproval` none | PASS | `StockControlTests.U_F2_*` (3 tests) |
| U-F3 | Approve by the requester | `ApproverMustDiffer` | PASS | `StockControlTests.U_F3_TheRequesterCannotApproveTheirOwnAdjustment` |
| U-F4 | `Expired` on live batch; `RecallDisposal` on available | `AdjustmentReasonNotApplicable` ×2 | PASS | `StockControlTests.U_F4_TheExpiredReasonNeedsAnExpiredBatch_AndRecallDisposalNeedsARecalledOne` |
| U-F5 | Count sheet while Open | no book quantity | PASS | `StockControlTests.U_F5_CountingIsBlind_*`, `U_F5_ACountLineHasNoBookQuantityUntilItIsSubmitted` |
| U-F6 | Full-location submit, uncounted position of 7 | counted 0, variance −7 | PASS | `StockControlTests.U_F6_AFullLocationSubmitTurnsAnUncountedPositionOf7IntoALineCountedZeroWithVariableMinus7` |
| U-F7 | Commit | exactly the submitted variances as CountGain/CountLoss drafts referencing the count | PASS | `StockControlTests.U_F7_CommitPostsExactlyTheSubmittedVariances_*` |
| U-F8 | Transfer to same location | `TransferLocationsMustDiffer` | PASS | `StockControlTests.U_F8_ATransferToTheSameLocationIsRefused` |
| U-G1 | 1 000.00 over 3,3,3 | 333.34, 333.33, 333.33 | PASS | `LandedCostAllocatorTests.U_G1_*` |
| U-G2 | USD 2.5000 × 100 at 118.500000, costs 1 500.00 | purchase 29 625.00; landed unit 311.2500 | PASS | `LandedCostAllocatorTests.U_G2_PurchaseInUsdAtTheConsignmentRate_GivesThePurchaseBaseAndFourDecimalLandedUnitCost` |
| U-G3 | EUR with no EUR rate | `ExchangeRateMissing` naming EUR | PASS | `LandedCostAllocatorTests.U_G3_*` |
| U-G4 | No batches received | `NoReceivedQuantityToAllocate` | PASS | `LandedCostAllocatorTests.U_G4_NoReceivedQuantity_IsRefused` |
| U-H1 | Thresholds 90/60/30 at 45, 29, −1 days | 60; 30; `Expired`; never 90 | PASS | `AlertsAndReorderTests.U_H1_ABatchRaisesOnlyTheTightestThresholdItHasCrossed_AndNeverARetrospectiveOne` |
| U-H2 | Settings 90, 0, 30 | [90, 30] | PASS | `AlertsAndReorderTests.U_H2_ZeroSwitchesAThresholdOff_AndTheRestAreSortedDescendingAndDistinct` |
| U-H3 | avgDaily 2.0, lead 10, cover 14, point 10, usable 5, on order 20 | 33 | PASS | `AlertsAndReorderTests.U_H3_TheSuggestionIsTheCoverDemandPlusThePointLessThePosition` |
| U-H4 | No consumption, point 10, position 3 | 7 | PASS | `AlertsAndReorderTests.U_H4_WithNoConsumption_TheSuggestionTopsUpToTheReorderPoint` |
| U-H5 | Policy quantity 50 | 50 | PASS | `AlertsAndReorderTests.U_H5_APolicyQuantityWinsOverTheFormula` |
| U-H6 | Usable excludes expired, quarantined, reserved | as stated | PASS | `AlertsAndReorderTests.U_H6_UsableExcludesExpiredQuarantinedRecalledAndReservedStock` |
| U-I1 | Recall from WrittenOff; lot `LOT-1` vs `lot-2` | `InvalidBatchTransition`; `RecallConfirmationMismatch` | PASS | `BatchAndLedgerTests.U_I1_ARecallFromWrittenOffIsRefused_AndTheLotNumberMustMatchExactly` |
| U-I2 | Recipient set: A merged into B, cat now C's, anonymised D | {B, C}; D excluded | PASS | Integration-level, real resolver: `InventoryRecallEndpointTests.I_I2_TheTraceListsEveryConsumptionPath_FollowsTheMerge_*`, `I_I4_AnAnonymisedCustomerIsNotNotified_*` (3 of 4 requested, D absent) |
| U-I3 | `BatchRecallGuard`: recalled; withdrawn | valid; invalid | PASS | Integration-level: `I_I6_AWithdrawnRecallGoesToQuarantine_AndTheNoticeGuardNoLongerHolds*` (guard true, then false after withdraw) |
| U-I4 | Template passes `TemplateRules`; Urgent; Transactional | as stated | PASS | `BatchAndLedgerTests.U_I4_TheRecallTemplateIsTransactionalAndUrgent_AndItsDefaultsPassTheTemplateRules` |
| U-J1 | Breach at location with positions 5, 0, 3 | two affected batches | PASS | Integration-level: `InventoryBreachAndCrossCuttingEndpointTests.I_J1_AReportedBreachFlagsTheBatchesHeld_*` |
| U-J2 | Reconciliation comparator with one drifted position | reported | PASS | **Added by this gate**: `InventoryTestGateEndpointTests.U_J2_ReconciliationReportsAPositionDriftedFromItsLedger_*` — guard disabled and re-enabled inside one transaction on a test-owned row; report shows exactly that row (on hand 15 vs 10), the honest sibling is absent, a `LedgerMismatch` alert is raised, empty after repair, trigger state `O` after |
| U-X1 | Reflection: no string property on events | none | PASS | `InventorySqlAndEventTests.U_X1_NoInventoryEventHasAStringProperty` |
| U-X2 | Migration SQL uses `.Value`s; `>` not `>=` | as stated | PASS | `InventorySqlAndEventTests.U_X2_TheSellableViewAndTriggersUseTheEnumerationValues_AndTheExpiryPredicateIsStrictlyGreaterThan` |
| U-X3 | Worker with a throwing pass | logged; next pass runs | PASS | `InventoryAlertsAndReorderEndpointTests.U_X3_APassThatThrowsIsLogged_AndTheNextPassStillRuns` |
| U-X4 | CSV writer `=HYPERLINK(…)`, `+1`, `@x` | prefixed with `'` | PASS | `InventoryCsvTests.U_X4_ATextCellThatCouldBeAFormulaIsPrefixedWithAnApostrophe` |

## Integration cases (§16)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I-A1 | Migrations on an empty database | schema, tables, indexes, views, triggers, `ck_stock_position_balance`, five seeded locations | BLOCKED | Schema content observed on the already-migrated `catecommerce_imp` (`InventorySchemaTests.I_A1_*`, both PASS: every table, index, view, 8 triggers, constraints, sequences, five `MAIN` locations). Applying the migrations to an EMPTY database was not done: only `catecommerce_imp` may be touched. Unblock: a scratch database the gate may create |
| I-A2 | Host starts with scope validation; one `EfStockLedger` per scope | resolves; one instance | PASS | `InventoryLocationAndStorageEndpointTests.I_A2_*` (asserts `Assert.Same` across both ports) |
| I-A3 | Location of another branch read with this branch's header | 404 | PASS | `I_A3_ALocationOfAnotherBranchIsA404_*` |
| I-A4 | Second active quarantine location | 400 `QuarantineLocationAlreadyExists` | PASS | `I_A4_ASecondActiveQuarantineLocationInABranchIsRefused` |
| I-A5 | SKU lookup by barcode and text | SKU with `storage` | PASS | `I_A5_SkuLookupByBarcodeAndByText_ReturnsTheSkuWithItsStorageAfterC2` |
| I-B1 | Supplier with six fields | 201; GET returns them | PASS | `InventoryProcurementEndpointTests.I_B1_ASupplierRecordsNameCountryContactCurrencyLeadTimeAndPaymentTerms` |
| I-B2 | Duplicate supplier code, other case | 400 `SupplierCodeAlreadyExists` | PASS | `I_B2_ADuplicateSupplierCodeInAnotherCaseIsRefused` (and the race: T13) |
| I-B3 | Issue a PO with two SKUs | `Issued`, `PO-…` | PASS | `I_B3_ACreatedPurchaseOrderIsIssuedWithAPONumberAndItsLines` |
| I-B4 | Receive 60 of 100 | accepted 60, outstanding 40, `PartiallyReceived` | PASS | `InventoryGoodsReceiptEndpointTests.I_B4_ReceivingSixtyOfAHundred_*` |
| I-B5 | Accountant POST / GET PO | 403 / 200 | PASS | `I_B5_AnAccountantReadsPurchaseOrdersButCannotCreateThem` |
| I-B6 | Cancel issued PO; close partially received with reason | 204; 204 outstanding shown | PASS | `I_B6_AnIssuedOrderWithNoReceiptsCanBeCancelled_*`; `I_B6_AfterAReceiptTheOrderCannotBeCancelled_ButCanBeShortClosed_*` |
| I-C1 | Plan a consignment with every FR-IMP-3 field | 201; GET returns all | PASS | `InventoryConsignmentEndpointTests.I_C1_APlannedConsignmentRecordsEveryFR_IMP_3FieldWithItsCostsAndRates` |
| I-C2 | Four documents; inspection pack | one response | PASS | `I_C2_FourDocumentsAreRetrievableTogetherInTheInspectionPack` |
| I-C3 | Supersede a document | both in pack; old marked | PASS | `I_C3_ASupersededDocumentStaysInThePack_FlaggedSuperseded` |
| I-C4 | Receipt against `AtCustoms` consignment | 400 `ConsignmentNotCleared` | PASS | `InventoryGoodsReceiptEndpointTests.I_C4_AReceiptAgainstAnAtCustomsConsignmentIsRefused_*` |
| I-C5 | Clear without permit number | 400 `ClearanceDocumentsMissing` | PASS | `I_C5_ClearingWithoutAPermitNumberIsRefused_*` |
| I-D1 | Receipt: two accepted lines, one part-rejected | 201; two batches; two `Receipt` rows with `GRN-…` and user | PASS | `I_D1_PostingAReceiptCreatesOneBatchPerAcceptedLine_*` |
| I-D2 | Food SKU without expiry | 400 `ExpiryDateRequired`; nothing written | PASS | `I_D2_AFoodSkuWithoutAnExpiryIsRefused_NothingIsWritten` |
| I-D3 | Already-expired expiry | 400 `BatchAlreadyExpired` | PASS | `I_D3_AnAlreadyExpiredExpiryIsRefused_*` |
| I-D4 | Over-receipt | 400 `ReceiptExceedsOutstanding` | PASS | `I_D4_ReceivingMoreThanIsOutstandingOnAPurchaseOrderLineIsRefused` |
| I-D5 | UPDATE, DELETE, TRUNCATE on both entry tables | each raises | PASS | `I_D5_TheLedgerRefusesUpdateDeleteAndTruncate_InTheDatabase` (ledger; message "immutable" for all three, position unchanged) and, **added by this gate**, `InventoryTestGateEndpointTests.I_D5_TheReservationEntriesRefuseUpdateDeleteAndTruncate_InTheDatabase` (reservation entries; all three raise "immutable", reserved unchanged) |
| I-D6 | `UPDATE stock_position SET on_hand = 999` | raises (guard) | PASS | `I_D6_TheProjectionCannotBeWrittenDirectly` (constraint `tg_stock_position_guard`; DELETE, INSERT and TRUNCATE refused too) |
| I-D7 | Positions equal Σ ledger | reconciliation empty | PASS | `I_D7_EveryPositionEqualsTheSumOfItsLedgerEntries_*`; `TheReconciliationReport_IsEmptyForAnHonestLedger` |
| I-D8 | Stock matrix, two batches, three locations | batch × location | PASS | `I_D8_TheStockMatrixShowsEachBatchAtItsLocation*` |
| I-D9 | Ledger search without range; 400 days | 400 both; rows carry fields | PASS | `I_D9_ALedgerSearchNeedsARangeOfAtMost366Days_*` |
| I-D10 | Receive with `HoldForInspection` | `Quarantined`; not sellable | PASS | `I_D10_AReceiptLineHeldForInspectionMakesAQuarantinedBatch_*` |
| I-E1 | FEFO preview 15 | soonest first, split | PASS | `InventoryStockPortEndpointTests.I_E1_TheFefoPreviewAllocatesTheSoonestExpiringBatchFirst_SplitAsNeeded` |
| I-E2 | Soonest expired, next quarantined | both skipped | PASS | `I_E2_APreviewSkipsAnExpiredBatchAndAQuarantinedOne` |
| I-E3 | Explicit non-FEFO reserve without / with reason; commit | refused; flagged + `FefoOverridden` audit naming user | PASS | `I_E3_AnExplicitNonFefoReservationNeedsAReason_AndWithOneIsFlaggedAndAuditedInTheSameCommit` |
| I-E4 | 20 parallel scopes reserve the last unit | one commit; 19 × `23514 ck_stock_position_balance`; `reserved = 1` | PASS | `I_E4_TwentyParallelClaimsOnTheLastFreeUnit_*` — judged: all 20 scopes plan before any commit, commits released together through a `ManualResetEventSlim` on `Task.Run`, no retry, no delay; plus FI-1 on the real trigger (T14) |
| I-E5 | Same race through `DeductAsync` | one; `on_hand = 0` | PASS | `I_E5_TheSameRaceThroughAPosDeduction_*` (also asserts exactly 2 ledger rows) |
| I-E6 | Mixed stock → sellable 7; `nextExpiresOn` | 7 | PASS | `I_E6_SellableExcludesExpiredQuarantinedHeldAndReservedStock_ViaTheQueryAndTheView` |
| I-E7 | Expired batch, every use, explicit batch + reason | `BatchExpired`; nothing enlisted | PASS | `I_E7_AnExpiredBatchIsRefusedForEveryUse_*` |
| I-E8 | Direct insert of `Sale` for expired batch | `23514 tg_stock_batch_expired`; position unchanged | PASS | `I_E8_DefenceInDepth_*`; `I_E8_TheSameTriggerRefusesAQuarantinedBatch_*` |
| I-E9 | Reserve 3 consume; reserve 2 release | `Sale −3`; reserved 0; release restores | PASS | `I_E9_ReserveThenConsume_*` |
| I-E10 | 10 on hand, 8 reserved, unreserved deduct 3 | `InsufficientStock` (2 free) | PASS | `I_E10_AnUnreservedDeductionCannotEatAnotherCustomersReservation` |
| I-E11 | Transfer racing a POS deduction through HTTP | 409 `InsufficientStock` | PASS | `InventoryStockControlEndpointTests.I_F4_AndI_E11_ATransferOfStockThatMovedAway_Is409InsufficientStock_*` (sequential shape: the POS deduct commits first, then the transfer; not two simultaneous requests — the simultaneous form is I-E4/E5 and T14) |
| I-F1 | Transfer 5 warehouse → shop floor | `TransferOut −5`, `TransferIn +5`, one reference | PASS | `I_F1_ATransferMovesStockBetweenLocations_*` |
| I-F2 | Adjustment below / above threshold; second admin approves | `Posted`; `PendingApproval`; entries on approval | PASS | `I_F5_ASmallAdjustmentPostsAtOnce_*`; `I_F6_AnAdjustmentOverTheThresholdWritesNothingUntilADifferentApproverApproves` |
| I-F3 | No reason; no note; approve by requester | 400 ×3 | PASS | `I_F5_*` (reason, note), `I_F6_RejectAndWithdrawLeaveTheLedgerUntouched_AndOnlyTheRequesterMayWithdraw`, `I_F6_AnAdjustmentOverTheThreshold…` (requester approval) |
| I-F4 | Stock sold before approval | approve → 409 `InsufficientStock` | PASS | `I_F6_ApprovingAfterTheStockHasMoved_Is409InsufficientStock_AndTheAdjustmentStaysPending` |
| I-F5 | Count: open, blind read, submit | variance report; no ledger rows | PASS | `I_F7_TheCountSheetIsBlindWhileOpen_AndSubmitFreezesTheVarianceWithNothingInTheLedger` |
| I-F6 | Commit | `CountGain`/`CountLoss` with `StockCount` reference | PASS | `I_F7_CommitPostsExactlyTheReportedVariances_AndTheLedgerStillBalances` |
| I-F7 | Sale between submit and commit | commit posts reported variance; final = counted − sold | BLOCKED | No test performs a sale between submit and commit; `I_F7_CommitPosts…` commits without an intervening sale. Unblock: add the case (a port `DeductAsync` between submit and commit, then assert final stock). Not added by this gate, see Gaps |
| I-F8 | Second count at busy location; count above threshold | 400 `CountLocationBusy`; `AwaitingApproval` | PASS | `I_F8_OnlyOneActiveCountPerLocation_AndAVarianceOverTheThresholdAwaitsAnotherApprover`; the parallel form is T13 |
| I-F9 | Reception POST adjustment | 403 | PASS | `I_F3_ATransferToTheSameLocation_OrWithTheStaffRoleOfAReceptionist_IsRefused` covers reception on transfer; `I_X2_ASampleEndpointOfEveryActionRefusesARoleThatLacksIt` covers the adjustment action |
| I-G1 | Two POs (USD, EUR), costs, rates; three batches; allocate | landed unit cost per batch; totals sum to costs | PASS | `InventoryLandedCostEndpointTests.I_G1_TheCostsAreAllocatedAcrossTheReceivedBatches_AndTheAllocatedAmountsSumToTheCostsExactly` |
| I-G2 | `v_batch_unit_cost` before / after | `Purchase` → `Landed` | PASS | `I_G2_TheBatchUnitCostViewSwitchesFromPurchaseToLanded_*` |
| I-G3 | Edit costs after allocation; receive another batch | `isStale = true`; re-allocation clears | PASS | `I_G3_ChangingTheCostsOrReceivingAnotherBatch_MakesTheAllocationStale_*` |
| I-G4 | Accountant allocates; Reception allocates | 200; 403 | PASS | `I_G4_AnAccountantMayAllocate_AndReceptionMayNot` |
| I-H1 | 91/89/59/29 days; two cycles | none, 90, 60, 30; none added | PASS | `InventoryAlertsAndReorderEndpointTests.I_H1_EachBatchRaisesOnlyItsTightestThreshold_*` (offsets 100/75/45/15, a documented amendment, so no branch-date boundary flake) |
| I-H2 | Near-expiry report within 90 days | `daysRemaining`, `valueAtRisk`; CSV | PASS | `I_H2_TheNearExpiryReportListsDaysRemainingAndValueAtRisk_AndDownloadsAsCsv` |
| I-H3 | Usable below point, cycle, receive, cycle | `LowStock` open then `Resolved` | PASS | `I_H3_LowStockIsRaisedWhenUsableFallsBelowThePoint_AndResolvedWhenStockIsReceived` |
| I-H4 | Suggestions with 90 days of consumption | equals formula | PASS | `I_H4_TheSuggestedQuantityEqualsTheFormula_*` |
| I-H5 | `RecordShortfall` then commit | `Shortfall` alert | PASS | `I_H5_AShortfallRecordedThroughThePort_IsCommittedWithTheConsumersOwnSave_AsAnOpenAlert` |
| I-H6 | Acknowledge an alert | `Acknowledged` with user | PASS | `I_H6_AcknowledgingAnAlertRecordsWhoSawIt_OnlyOnce_AndNotFromAnotherBranch` |
| I-I1 | Batch at warehouse, shop floor and ANOTHER branch's warehouse; recall | `Recalled`; sellable drops in both branches; FEFO skips | PASS | `I_I1_ARecallMakesTheBatchUnusableEverywhereAtOnce_*` (one branch) and **added by this gate**: `InventoryTestGateEndpointTests.I_I1_ARecallReachesABatchThatWasTransferredToAnotherBranch_*` — sellable 7 and 3 before, 0 and 0 after in the two branches; FEFO preview from each branch skips it; port sale at the second branch refused; direct insert there raises `tg_stock_batch_not_usable`; stock still held |
| I-I2 | Sale (A), Dispense (cat X of B), BoardingUse (cat Y of C), POS no customer, reservation D; merge A→A′; trace | A′, X/B, Y/C, 1 unidentified, order D unshipped | PASS | `I_I2_TheTraceListsEveryConsumptionPath_FollowsTheMerge_AndNamesTheUnidentifiedAndTheUnshipped` |
| I-I3 | `ConsumeReservationAsync` for D; direct `Sale` insert | `BatchNotUsable`; `tg_stock_batch_not_usable` | PASS | `I_I3_ARecalledBatchCannotBeConsumedOrReserved_EvenThroughTheDatabaseDirectly` |
| I-I4 | Notify; run the delivery pass | request per recipient, batch as subject, in the NTF log, in-app copies, count | PASS | `I_I4_NotifyEnqueuesOneRequestPerRecipient_*` plus **added**: `InventoryRecallDeliveryEndpointTests.I_I4_TheDeliveryPassTurnsEachRecallRequestIntoANotificationInTheLog_*` — real `NotificationWorkCycle`; per recipient one `Sent` (SMS, capturing sender) and one `InboxDelivered` (in-app); `subject_id` = the batch on every notification |
| I-I5 | Notify again | requested 0, alreadyNotified 4 | PASS | second run in `I_I4_NotifyEnqueuesOneRequestPerRecipient_*` |
| I-I6 | Withdraw before notices sent | `Quarantined`; pending notices cancelled `SubjectNoLongerApplies` | PASS | `I_I6_AWithdrawnRecallGoesToQuarantine_*` plus **added**: `InventoryRecallDeliveryEndpointTests.I_I6_WithdrawingTheRecallBeforeDeliveryCancelsThePendingNotices_NothingIsSent` — after withdraw and the real delivery pass the request is `Suppressed` with reason `SubjectNoLongerApplies`, no notification row exists. NOTE the observable form is a suppressed REQUEST, not a `Cancelled` notification: the guard fires at expansion, so the §16 wording "pending notices `Cancelled`" does not literally occur in this flow (INFO, behaviour is correct) |
| I-I7 | Recall and reservation concurrently, 20 repetitions | never a reservation committed after the recall | PASS | Existing `I_I7_ARecallAndAReservationOnTheSameBatchAtTheSameTime_*` (20 rounds; one reserver) PLUS **added**: `InventoryTestGateEndpointTests.I_I7_SixReserversHammerOneBatchWhileItIsRecalled_*` — 6 parallel reservers committing one unit at a time against a recall; 148 reservations committed before the recall landed over 5 rounds (a genuine interleaving), every reserver ended refused by plan or by `tg_stock_batch_not_usable`, and committed count = `reserved` = trace length every round |
| I-I8 | Nurse recalls; nurse reads trace | 403; 403 | PASS | `I_I8_OnlyRecallManagersMayRecallOrReadTheTrace` (also trace.csv and notify) |
| I-I9 | Close with stock held; write off; close | 400 `RecallStockStillHeld`; 204; 204 | PASS | `I_I9_ARecallCannotBeClosedWhileStockIsHeld_UntilItIsWrittenOffAsRecallDisposal` |
| I-J1 | Breach at boarding store, two batches; quarantine; close | flagged + alert; stock moved with breach reference | PASS | `InventoryBreachAndCrossCuttingEndpointTests.I_J1_AReportedBreachFlagsTheBatchesHeld_*` |
| I-J2 | Storage profile on receipt and stock reads | `storage` present | PASS | `I_J2_TheStorageProfileComesWithTheStockReads` |
| I-X1 | Logs across recall, trace, notify | no names, mobiles, emails, advice | PASS | **Added by this gate** (was NOT BUILT): `InventoryLogPrivacyEndpointTests.I_X1_*` — capturing logger at Information+, asserts the capture contains the recall and notify lines (so an empty capture cannot pass), then that none of two customer names, a cat name, the advice text, or two mobile numbers (full and last 8 digits) appears |
| I-X2 | Every route with a role lacking its action | 403 | PASS | `I_X2_ASampleEndpointOfEveryActionRefusesARoleThatLacksIt` |
| I-X3 | Trace CSV | downloaded, guarded, `RecallTraceExported` audit | PASS | `I_I8_TheTraceCsvIsAuditedAsRecallTraceExported_AndCarriesTheContactDetails` |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | ARCH HIGH | Read handlers materialise aggregates (ADR-012) | `InventoryReadSideConventionTests.ReadHandlersNeverMaterialiseAnAggregateWithoutAProjection` passes now AND fails on an offending pattern | PASS | Passed 1/1 clean. Mutation: appended `db.Batches.ToDictionaryAsync(b => b.Id, b => b.LotNumber)` to `AlertsAndReportsQueryHandlers.cs` → test FAILED naming that file; edit reverted with `git checkout` (file clean, nothing committed); test PASS again after revert |
| R2 | CODE_REVIEW HIGH | Recall trace and recipients must net reservations per line | `I_I2_AReleasedReservationIsNotUnshipped_AndItsCustomerIsNotNotified` — reserve 3 → release → reserve 2 → recall: trace lists only the open order (quantity 2), only that customer notified | PASS | Observed; this is the new reserve → release → recall trace test |
| R3 | CODE_REVIEW HIGH | Breach `ObservedOn` with an offset crashed `timestamptz` | `AnObservedTimeWithAnOffsetIsAccepted_AndAFutureOneIsRefused` — sent as `+06:00`, stored instant equal within 1 s; two days ahead → 400 | PASS | Observed |
| R4 | CODE_REVIEW MEDIUM | Reconciliation never shown to detect drift | U-J2 (above) | PASS | Observed |
| R5 | CODE_REVIEW MEDIUM | Cross-branch recall untested | I-I1 second branch (above) | PASS | Observed |
| R6 | SECURITY MEDIUM | Write-off reached other branches | `InventoryTestGateEndpointTests.WriteOff_ByAKeeperNotAssignedToEveryHoldingBranch_IsRefusedAndWritesNothing_ButAnAdministratorOfBothMayDoIt` — keeper (branch 1 only) refused with `StockLocationNotFound`, batch still `Available`, both positions unchanged; administrator assigned to both: 204, all three positions 0. Application-level `GoodsReceiptHandlerTests` also in the green suite | PASS | Observed |
| R7 | SECURITY MEDIUM | Facts test bypassed the dev guard | `InventoryDatabaseFactsTests` use `InventoryApiFactory.RequireBranchDatabase()` | PASS | Read in source (line 16) and the facts tests ran against `catecommerce_imp` only |
| R8 | ARCH MEDIUM | `ix_supplier_code` / `ix_stock_count_one_active` unmapped → 500 on a race | T12 and T13 below | PASS | Observed, 3 repeats each |
| R9 | ARCH MEDIUM | Alert-raise race surfaced a 23505 | `TwoRacingRaisesOfTheSameAlert_BothSucceed_AndExactlyOneRowRemains` (existing) and the stronger T4 | PASS | Observed |
| R10 | ARCH MEDIUM | Recall notify wrote no `BusinessEvent` audit row | `I_I4_NotifyEnqueuesOneRequestPerRecipient_*` reads `audit.audit_entry` for `Action = 'RecallNotified'` and the batch id, asserts `"Requested":4` in the after-value | PASS | Observed |
| R11 | ARCH MEDIUM | FI-1 was false as written (§20.3) | T14: the real trigger body is the two-statement shape, and two real sessions serialise | PASS | Observed. The §20.3 decision itself is still owed by the architect or owner (see Gaps) |

## Additional TEST-gate cases (the checks you asked for)

| ID | Case | Status | Evidence |
|---|---|---|---|
| T1 | Reserve → release → recall trace | PASS | R2 |
| T2 | Breach `ObservedOn` sent with `+06:00`; a future time rejected | PASS | R3 |
| T3 | Write-off branch-assignment refusal | PASS | R6 |
| T4 | Duplicate alert raise race, a fresh key every round | PASS | **Added**: `TwoRacingRaisesOfAFreshAlertKey_EveryRound_AllSucceed_AndExactlyOneRowPerKeyRemains` — 6 rounds × 8 parallel raisers on a NEW batch/key each round; all succeed; one `inventory.stock_alert` row per key. The existing test reuses one key for all 5 rounds, so only round 1 can race (judged weak, see Weak tests) |
| T5 | `RecallNotified` audit row | PASS | R10 |
| T6 | Ledger immutability triggers | PASS | I-D5 (ledger UPDATE/DELETE/TRUNCATE raise, balance unchanged) |
| T7 | Stock cannot go negative | PASS | **Added**: `StockCannotGoNegative_ADirectLedgerRowLargerThanTheBalanceIsRefusedByTheCheck_*` — direct `Wastage −5` against a position of 1 raises `23514 ck_stock_position_balance`, position and ledger count unchanged; the HTTP adjustment of −5 is 409. Also I-E4/I-E5/I-F4 |
| T8 | FEFO order | PASS | U-E1, U-E3, U-E4, I-E1, I-E2 |
| T9 | Landed-cost allocation | PASS | U-G1…U-G4, I-G1…I-G4 (the allocated amounts sum to the costs exactly) |
| T10 | `dotnet ef migrations has-pending-model-changes` | PASS | Output: "No changes have been made to the model since the last migration." |
| T11 | `InventoryReadSideConventionTests` fails on an offending pattern | PASS | R1 |
| T12 | 8 parallel creates of one supplier code | PASS | **Added**: `EightParallelCreatesOfOneSupplierCode_*` — exactly one 201, the rest 400 `SupplierCodeAlreadyExists`, none 500 |
| T13 | 8 parallel opens of a count at one location | PASS | **Added**: `EightParallelOpensOfACountAtOneLocation_*` — exactly one opens, the rest 400 `CountLocationBusy`, none 500 |
| T14 | FI-1 against the REAL trigger and table, two sessions | PASS | **Added**: `FI1_TheRealLedgerTriggerIsTheTwoStatementShape_AndTwoSessionsSellingTheLastUnitSerialise_*` — `pg_get_functiondef('inventory.fn_apply_ledger_entry')` contains `ON CONFLICT (batch_id, location_id) DO NOTHING` and `UPDATE inventory.stock_position`, no `DO UPDATE`; the second session is observed WAITING ON A LOCK in `pg_stat_activity` (polled, no fixed sleep) while the first is uncommitted; after the first commits, the second fails `23514 ck_stock_position_balance`; one sale in the ledger. The scratch-schema facts tests (FI1 literal shape: a spurious 23514; FI1 last-unit race; FI2; FI4; FI5) also PASS |
| T15 | Known Shop flakes re-run alone | PASS | `ShopCataloguePerformanceTests.I_E11` PASS alone (it failed in the whole-project run under load); `ShopBundlePrescriptionEndpointTests.FiltersCache_IsKeyedOnTheParsedCategory_JunkInputDoesNotGrowIt` PASS alone (it passed in the whole-project run). NEW, not on your list: `TimingEnumerationTests.CustomerSignIn_LockedAccount_StillCallsVerifyAgainstDummy` failed in the whole-project run (a timing comparison, Account area) and PASSED alone |

## Acceptance-criteria coverage (§18)

| Criterion | Covered by | Status |
|---|---|---|
| A1 | U-B1, I-B1, I-B2 | PASS |
| A2 | U-B2, U-B3, U-B4, I-B3, I-B6 | PASS |
| A3 | U-B5, I-B4 | PASS |
| A4 | U-C1, U-C2, U-C3, I-C1, I-C5 (provisional on SRS Q-8) | PASS |
| A5 | U-C4, I-C2, I-C3 (metadata only until object storage) | PASS |
| A6 | U-D4, U-D6, I-D1 | PASS |
| A7 | U-D1, U-D2, I-D2 | PASS |
| A8 | U-G1…U-G4, I-G1, I-G3 | PASS |
| A9 | I-G2 | PASS |
| A10 | U-A1, I-D8, I-J1 | PASS |
| A11 | U-E1, U-E3, U-E4, I-E1, I-E2 | PASS |
| A12 | U-E6, I-E3 | PASS |
| A13 | U-H6, I-D10, I-E6, I-E10 | PASS |
| A14 | I-E4, I-E5, I-E11, T14 (real parallelism, observed) | PASS |
| A15 | U-E2, U-E7, U-E9, I-E7, I-E8 | PASS |
| A16 | U-H1, U-H2, I-H1 | PASS |
| A17 | I-H2 | PASS |
| A18 | U-I1, I-I1 (both branches), I-I7 (single and multi-reserver) | PASS |
| A19 | U-I2, I-I2 (contract level until ORD/PHM/BRD stamp real movements) | PASS |
| A20 | U-I3, U-I4, I-I4 (log + in-app, observed), I-I5, I-I6 (in-app and capturing SMS only; no real provider, NTF OQ-1) | PASS |
| A21 | U-E11, I-I3 (contract; dispatch is HO-ORD-001) | PASS |
| A22 | U-H6, I-H3 | PASS |
| A23 | U-H3, U-H4, U-H5, I-H4 | PASS |
| A24 | U-F8, I-F1 | PASS |
| A25 | U-F1…U-F4, I-F2, I-F3, I-F4, I-F9 | PASS |
| A26 | U-F5, U-F6, I-F5 | PASS |
| A27 | U-F7, I-F6, I-F8; I-F7 is BLOCKED (no sale between submit and commit is exercised) | PASS with one named gap |
| A28 | U-D8, I-D1, I-D9 | PASS |
| A29 | I-D6, I-D7, U-J2 (drift detected) | PASS |
| A30 | U-D9, I-D5 | PASS |
| A31 | U-A4, I-A5, I-J2 | PASS |
| A32 | U-J1, I-J1 | PASS |
| A33 | I-H5 | PASS |
| A34 | I-A2, I-X1 (no personal data in logs, now asserted) | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| I-A1 (empty-database half) | The brief limits the gate to `catecommerce_imp`, which is already migrated; creating a scratch database was not permitted | Permission to create and drop a scratch database, then apply all migrations to it and re-run `InventorySchemaTests` against it |
| I-F7 | No test, and none added, that sells between count submit and commit | A test that submits a count, performs a port sale at that location, commits, and asserts final stock = counted − sold |

## Weak tests found, and what was done

- `I_I7_ARecallAndAReservationOnTheSameBatchAtTheSameTime_*` (existing): one reserver against an HTTP recall, every round gated together. It asserts a correct invariant, but one reserver plus an HTTP round trip on the recall side means the two sides rarely overlap, and nothing in the test measures that they do. Left unchanged (it is correct and green); strengthened by adding the six-reserver test whose assertion fails if no reservation ever committed before the recall landed.
- `TwoRacingRaisesOfTheSameAlert_*` (existing): reuses one dedup key for all 5 rounds, so only the first round can race; the next four hit the pre-check. Left unchanged; replaced in effect by T4 (fresh key per round).
- `InventoryDatabaseFactsTests` FI-1 / FI-5 (existing): prove "the second writer blocks" with `Task.Delay(750)`, a fixed sleep (§20.4's spirit: delays must not rescue a concurrency test). The delay only gates an assertion that something is still waiting, not a pass, so LOW. T14 observes the wait in `pg_stat_activity` instead. Also these two run on a scratch schema, not the real trigger — covered by T14.
- `InventoryReadSideConventionTests` (existing): proven to fail on an offending pattern, but narrow: it only looks for `db.<AggregateDbSet>` (a different variable name, `Set<T>()`, or a context field escapes it) and any `.Select(` anywhere in the same `;`-delimited statement exempts it, even one after the materialiser. LOW.
- `I_I1_*` (existing): single branch only; replaced in effect by the second-branch test.
- No existing test was deleted or weakened.

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| §20.3 ratification of the FI-1 two-statement trigger | An architecture decision, not a test. Behaviour is verified (T14); the decision is still owed | architect / owner |
| `CustomerMergedEvent` and the other cross-area reads at a larger scale | Out of scope for this handover | HO-ORD / HO-NTF |
| From/To `DateOnly` filters converted at UTC midnight rather than branch-local day (CODE_REVIEW MEDIUM) | Not fixed in this branch and not asked of the TEST gate; behaviour unchanged | developer, IMP backlog |
| Count approver independence, controlled-drug approver verification (SECURITY MEDIUM) | Not part of the §15/§16 contract | developer, IMP backlog |
