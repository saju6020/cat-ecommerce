GATE: ACCEPTANCE
HANDOVER: HO-IMP-001
COMMITS: 5732fa28..8f915bdf on ho/IMP-001 (base master 50f1ab1). Includes the TEST gate's 431c14b5 and the post-gate fix 8f915bdf, which no other gate saw and which this gate delta-reviewed.
DATE: 2026-10-07
STATUS: PASS

Owner gate rule (2026-09-27): only BLOCKER and HIGH findings block. No BLOCKER or HIGH was found. MEDIUM and LOW findings go to docs/backlog/IMP.md.

PRIOR GATES (read from docs/gates and docs/test-cases, not from commit messages)
- ARCHITECTURE: FAIL in round 1 (HIGH: the read side materialised aggregates). Closed by REVIEW-R2.
- CODE_REVIEW: FAIL in round 1 (2 HIGH: the recall trace netting, the breach ObservedOn offset). Both closed by REVIEW-R2.
- SECURITY: PASS (5 MEDIUM, 4 LOW).
- REVIEW-R2 (combined ARCH, CR and SEC follow-up): PASS. All round-1 HIGHs closed.
- TEST: PASS. docs/test-cases/TC-HO-IMP-001.md has 160 rows: 158 PASS, 2 BLOCKED (I-A1 on an empty DB, I-F7), 0 FAIL, 0 NOT RUN. The BLOCKED rows are not counted as passes below.

RE-EXECUTION BY THIS GATE (at 8f915bdf; catecommerce_imp only; CATECOMMERCE_TEST_DB exported in the same command)
- `dotnet build CatEcommerce.sln --no-incremental`: 0 warnings, 0 errors.
- Domain tests: 1065/1065. Application tests: 198/198 (197 plus 8f915bdf's new test). Infrastructure tests: 119/119.
- Integration tests filtered to `FullyQualifiedName~Inventory`: 135/135 (134 plus 8f915bdf's new test). The TRX outcomes were read for I-E4 (both), I-E5, I-I7 (both), FI-1 (real trigger plus the scratch-schema pair), both alert-race tests, U-J2, I-X1, I-I4 and the new atomicity test: all Passed.

DELTA REVIEW OF 8f915bdf
- The recall-notify audit row commits in the same SaveChanges: VERIFIED.
  - In `NotifyRecallRecipientsCommandHandler`, `EfNotificationOutbox.Enqueue` only does `db.NotificationRequests.Add`. `EfInventoryAuditEnlister.Enlist` only does `db.AuditEntries.Add`.
  - `repository.UpdateAsync(batch)` then makes the one `db.SaveChangesAsync()` on the same scoped DbContext (`EfAggregateRootRepository.CommitAsync`).
  - Proven by `InventoryRecallEndpointTests.I_I4_AFailingAuditWriteRollsBackTheWholeNotifyRun_SoARetryStillFindsEveryoneWaiting` (Passed). An audit PK clash fails the request, no notification request survives, `notifiedRecipientCount` stays 0, and a retry requests 4.
- `EfInventoryAlertWriter` swallows only 23505 on the two named indexes: VERIFIED by reading.
  - The filter is `DbUpdateException { InnerException: PostgresException { SqlState: UniqueViolation, ConstraintName: name } }` with `name` in {`ix_stock_alert_dedup_key`, `ix_stock_alert_one_live_per_pair`}.
  - Both names match the EF configuration (`AlertsAndReorderConfigurations.cs:35-36`) and the migration.
  - Any other failure propagates. The failed entity is detached.
  - The reflection-based `DuplicateKey` is gone. Application no longer sees the driver.
- Regressions: none found. All suites above are green. `I_A2_*` resolves every Inventory handler with the two new registrations.
- Scope: none of this was in §4. `IInventoryAuditEnlister.cs` is a new file outside §13's literal list (see SCOPE CHECK).
- Its tests assert the right things, with two caveats:
  - The new Application test (`ARaceLostAtTheUniqueIndex_IsASuccessThatKeepsTheFirst`) proves the handler treats `false` as success and writes nothing.
  - The real writer is exercised under real parallelism by `TwoRacingRaisesOfAFreshAlertKey_*`, through DI, 6 rounds x 8 racers, one row per key.
  - Caveat 1 (LOW): no test proves that a 23505 on another constraint propagates.
  - Caveat 2 (LOW): the atomicity test asserts only `!IsSuccessStatusCode`, not a 500 or the cause.

ADRs
- ADR-097..116: the Decision and Reason of all 20 match §9 word for word. This was compared by script (imp-accept-scratch/adrcheck.py, 20/20) over the parsed §9 table and the parsed adr.md sections.
- Append-only: `git diff 50f1ab1..HEAD -- .ai/decisions/adr.md` is 203 insertions, 0 deletions, in one hunk at the end of the file (ac348b77 adds 180 lines, 772967dc adds 23). The "HO-IMP-001 build amendments" section only adds lines.

C-ALL DUTIES
- C-ALL-1: MET. All 27 tables created by the Inventory migrations are classified in `RecordClassification.Inventory.cs`, plus the raw-SQL `stock_position`. This was compared by script; there is no missing table.
- C-ALL-2: MET. `ErasureExemptions.Inventory.cs` exempts the context with a reason (ids only, resolved at read time, ADR-104).
- C-ALL-3: MET. `InventoryRetentionReviewSource` covers POs and receipts. It is registered and only reads with projections.
- C-ALL-4: MET. No public Remove* or Delete* method exists on any Inventory aggregate. The ledger and reservation entries are immutable (I-D5, both tables).
- C-ALL-5: NOT APPLICABLE today, but a latent gap (MEDIUM, see FINDINGS).

CRITERIA (§18)
- A1 MET — `ProcurementTests.U_B1_Supplier_RecordsAllSixFieldsAndNormalisesTheCodeAndEmail` asserts name, country, contact, currency, lead time and terms. `I_B1_ASupplierRecords…` asserts the same over HTTP (all but name). I-B2 covers the duplicate code.
- A2 MET — U-B2 to U-B4, `I_B3_ACreatedPurchaseOrderIsIssuedWithAPONumberAndItsLines`, I-B6.
- A3 MET — `U_B5_…Outstanding40_PartiallyReceived`. `I_B4_ReceivingSixtyOfAHundred_*` asserts ordered 100, accepted 60, rejected 10, outstanding 40 and `PartiallyReceived`.
- A4 MET, PROVISIONAL on SRS Q-8 (OQ-15). `I_C1_APlannedConsignmentRecordsEveryFR_IMP_3FieldWithItsCostsAndRates` asserts origin, shipment reference, permit, health certificate, customs declaration, HS codes, POs, costs and rate. U-C1 to U-C3 and I-C5 also cover it.
- A5 MET, PROVISIONAL (metadata only until object storage exists, OQ-14):
  - `I_C2_FourDocumentsAreRetrievableTogetherInTheInspectionPack`: 4 documents, plus details, POs and batches in one payload.
  - I-C3: a superseded document is kept.
  - `I_C2_…AndThereIsNoUploadRoute`.
- A6 MET:
  - `I_D1_PostingAReceipt…` asserts accepted 8, rejected 2, reason `Damaged`, one batch per accepted line and two `Receipt` ledger rows.
  - U-D4 and U-D6 cover rejection and lot rules. I-D8 asserts expiry on the batch read.
  - Lot and manufacture date: verified in source, not by an assertion. The path is request → command → `GoodsReceiptLine` and `BatchAggregate` (`ManufacturedOn` and `LotNumber` persisted; `BatchConfiguration.cs:27`, `GoodsReceiptConfiguration.cs:46`) → returned by the receipt and batch reads (`StockQueryHandlers.cs:64,88,276`). See LOW-1.
- A7 MET — the two U-D1 tests. `I_D2_AFoodSkuWithoutAnExpiryIsRefused_NothingIsWritten`: `ExpiryDateRequired` on `Lines[0].ExpiresOn`, with 0 receipts and 0 batches written. U-D2 covers a General SKU accepted with no expiry.
- A8 MET — U-G1 to U-G4. `I_G1_…AllocatedAmountsSumToTheCostsExactly`: totals 12 850.01 = the sum of the lines, landed unit cost 360.5001. I-G3 covers staleness.
- A9 MET — `I_G2_TheBatchUnitCostViewSwitchesFromPurchaseToLanded_*`: the view goes from Purchase 296.25 to Landed 360.5001, and an adjustment is valued at landed cost. The margin report itself belongs to HO-RPT-001.
- A10 MET:
  - U-A1.
  - `I_D8_TheStockMatrixShowsEachBatchAtItsLocation*`: batch × location across warehouse, shop floor and dispensary.
  - `I_J1_*`: boarding store and quarantine positions.
- A11 MET — U-E1, U-E3, U-E4. `I_E1_*`: soonest first, split 10+5. `I_E2_*`: expired and quarantined batches skipped.
- A12 MET — U-E6. `I_E3_AnExplicitNonFefoReservationNeedsAReason_AndWithOneIsFlaggedAndAuditedInTheSameCommit`: refused without a reason; with a reason it is flagged, and a BusinessEvent audit row carrying the reason, the actor and the before/after plans is enlisted in the same commit.
- A13 MET:
  - U-H6.
  - I-D10: a held batch has usable 0 and sellable 0.
  - `I_E6_*`: sellable 7 excludes expired, quarantined, reserved and non-retail stock.
  - `I_E10_*`: an unreserved deduction cannot take reserved stock.
- A14 MET:
  - `I_E4_TwentyParallelClaimsOnTheLastFreeUnit_*` and `I_E4_AReservationAndADeductionRacing…` are real-parallel: exactly one commit, the rest fail `23514 ck_stock_position_balance`.
  - `I_E5_*`: on_hand 0 and one sale.
  - T14 `FI1_TheRealLedgerTrigger…`: the second session is observed waiting in pg_stat_activity.
  - All re-run Passed here. I-E11 is sequential (noted in the sheet).
- A15 MET:
  - U-E2, U-E7, and U-E9 (reflection: no force/bypass parameter).
  - `I_E7_*`: Sale, Dispense, ClinicalUse, BoardingUse and Reserve are all `BatchExpired`, even with an explicit batch and a reason, and nothing is enlisted.
  - `I_E8_*`: a direct insert raises `23514 tg_stock_batch_expired` and the position is unchanged.
- A16 MET — U-H1, U-H2. `I_H1_EachBatchRaisesOnlyItsTightestThreshold_*`: 90/60/30, nothing for the far batch, and a second run adds none. Test offsets are 100/75/45/15, a declared amendment.
- A17 MET — `I_H2_*`: daysRemaining, valueAtRisk 50.00 and costBasis; the CSV is text/csv.
- A18 MET:
  - U-I1.
  - `I_I1_ARecallMakesTheBatchUnusableEverywhereAtOnce_*`.
  - The TEST gate's second-branch I-I1: sellable 7 and 3 drop to 0 and 0, FEFO skips the batch, and the trigger refuses.
  - Both I-I7 race tests Passed.
- A19 MET at CONTRACT LEVEL (until HO-ORD, HO-PHM and HO-BRD stamp real movements). `I_I2_TheTraceListsEveryConsumptionPath_FollowsTheMerge_*` lists:
  - a sale resolved through the merge A→A′;
  - a dispense to cat Xena of B;
  - a boarding feed for C's cat;
  - 1 unidentified POS sale;
  - order D, unshipped.
  - `I_I2_AReleasedReservationIsNotUnshipped_*` (R2) also applies.
- A20 MET, IN-APP ONLY outside Development until a provider exists (HO-NTF-001 OQ-1):
  - U-I4. U-I2 and U-I3 at integration level.
  - `I_I4_NotifyEnqueuesOneRequestPerRecipient_*`: 4 requests, a second run adds 0, and a `RecallNotified` audit row.
  - TEST's `I_I4_TheDeliveryPass…`: a Sent and an InboxDelivered entry per recipient, logged.
  - I-I6: a withdrawn recall is `Suppressed / SubjectNoLongerApplies`.
  - The new atomicity test shows the audit log is never lost.
- A21 MET at CONTRACT LEVEL (dispatch belongs to HO-ORD-001) — U-E11. `I_I3_ARecalledBatchCannotBeConsumedOrReserved_EvenThroughTheDatabaseDirectly`: `BatchNotUsable` from the port, and `tg_stock_batch_not_usable` on a direct insert.
- A22 MET — U-H6. `I_H3_LowStockIsRaisedWhenUsableFallsBelowThePoint_*`: Open at 5, then Resolved after a receipt.
- A23 MET — U-H3, U-H4, U-H5. `I_H4_TheSuggestedQuantityEqualsTheFormula_*`: usable 5, on order 20, lead time 21. `I_H4_APolicyQuantityOverridesTheFormula`.
- A24 MET — U-F8. `I_F1_*`: source 10→6, destination 0→4, 3 ledger rows, and ledger equals position. `StockTransferAggregate.cs:75-76` builds the TransferOut/TransferIn pair with one reference.
- A25 MET — U-F1 to U-F4:
  - `I_F5_*`: reason and note are mandatory.
  - `I_F6_AnAdjustmentOverTheThreshold…`: no ledger row until a different approver approves; the keeper gets 403.
  - `I_F6_ApprovingAfterTheStockHasMoved_Is409`.
  - Reject and withdraw.
  - I-X2 covers 403 for a role without the action.
- A26 MET — U-F5, U-F6. `I_F7_TheCountSheetIsBlindWhileOpen_AndSubmitFreezesTheVarianceWithNothingInTheLedger`: book and variance are null while Open, variances are -2 and -7 after submit, and the ledger row count is unchanged.
- A27 MET:
  - `U_F7_CommitPostsExactlyTheSubmittedVariances_AsCountGainAndCountLoss_ReferencingTheCount` asserts kind, delta and `StockReferenceKind.StockCount` plus the count id and number.
  - `I_F7_CommitPostsExactlyTheReportedVariances_*`.
  - `I_F8_*`: above the threshold it waits for approval.
  - I-F7 (a sale between submit and commit) is BLOCKED in the sheet and accepted to the backlog. It does not remove this evidence.
- A28 MET — U-D8. `I_D1_*`: Receipt rows carry the GRN label and the user. `I_D9_*`: SKU, batch, delta, location, performedBy and sequence; range limits return 400.
- A29 MET:
  - `I_D6_TheProjectionCannotBeWrittenDirectly`: `tg_stock_position_guard`.
  - `I_D7_EveryPositionEqualsTheSumOfItsLedgerEntries_*`.
  - U-J2: drift is detected.
  - No command or endpoint sets a quantity (§4.4 grep is clean).
- A30 MET — U-D9. `I_D5_*` on both entry tables: UPDATE, DELETE and TRUNCATE raise.
- A31 MET — U-A4. `I_A5_*`: the SKU lookup (receiving) returns `storage`. `I_J2_TheStorageProfileComesWithTheStockReads`.
- A32 MET — U-J1 at integration level. `I_J1_AReportedBreachFlagsTheBatchesHeld_*`: 2 affected batches and an alert; quarantine moves free stock with the breach as reference; ledger equals position.
- A33 MET — `I_H5_AShortfallRecordedThroughThePort_IsCommittedWithTheConsumersOwnSave_AsAnOpenAlert`, and the uncommitted case leaves nothing.
- A34 MET:
  - `I_A2_HostStartsWithScopeValidation_*`: every handler, validator and query resolves, and there is one EfStockLedger per scope behind both ports.
  - `I_X1_*`: the log capture is non-empty and holds no names, mobiles, cat name or advice text.

§20 CONDITIONS
- §20.3 was hit: FI-1 is false as literally written.
  - It was declared, not hidden. It is in the build-amendment ADR, ARCH MEDIUM-2 and TEST T-3.
  - The substitute keeps ADR-099's trigger, CHECK and row lock. It adds no application lock, advisory lock, retry loop or stored balance, and is verified on the real trigger (T14).
  - Ratification is still owed by the owner or architect. Per the owner's instruction it is backlog and does not fail this gate. It remains an open owner action, not a closed one.
- No other §20 condition was found worked around:
  - no expiry bypass (U-E9);
  - no ledger UPDATE;
  - no enqueue from an event handler;
  - no Inventory event handlers;
  - no upload endpoint;
  - no FOR UPDATE or advisory lock in C# (grep).

FINDINGS
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Inventory/EfStockLedger.Consumer.cs:235 and :290 (`EnlistAudit`) — C-ALL-5, latent.
  - The port writes BusinessEvent audit rows whose EntityType is the caller's reference kind (`Prescription`, `Visit`, `Surgery`, `BoardingStay`, `ControlledDrugCorrection`) and whose Reason is the caller's free text (`FefoOverridden`, `StockCorrectedThroughLedger`).
  - None of these types is registered in a `ClinicalAuditEntityTypes.*.cs`. Once HO-EMR, HO-PHM or HO-SRG call the port, an Accountant would see that reason text unredacted.
  - Nothing calls the port this way yet. The integrator or the consuming handovers should decide which area registers these strings.
- [MEDIUM] Scope: 18 files fall outside §13's literal list, not 16. The 16 that ARCH listed, plus 2 added after that gate:
  - `src/Domain/CatEcommerce.Domain/DomainServices/IInventoryAuditEnlister.cs` (8f915bdf; it also declares `IInventoryAlertWriter`);
  - `tests/CatEcommerce.Infrastructure.Tests/Persistence/InventoryReadSideConventionTests.cs` (38f5dbf0).
  - Both are the same same-area class as the accepted 16, and neither is in §14. The integrator's §1 Amended row must list all 18.
- [LOW] A6 evidence: no test asserts that the lot number and manufacture date round-trip through a posted receipt or batch read. The criterion is met on source. Add the two asserts to `I_D1_*`.
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Inventory/EfInventoryAlertWriter.cs:24 — the writer calls `db.SaveChangesAsync` directly. Its precedent, `EfNotificationRequestStore`, goes through `repository.SaveAsync(…, InMemoryPublishOnly)`. As a result `StockAlertRaisedEvent` is no longer dispatched in-process. Nothing subscribes today (§11.6: no Inventory event handlers), so behaviour is unchanged. It is an undeclared divergence.
- [LOW] tests/CatEcommerce.IntegrationTests/Endpoints/InventoryRecallEndpointTests.cs (`I_I4_AFailingAuditWrite…`) asserts only `!IsSuccessStatusCode`. Assert a 500 so that an unrelated refusal (403/404) cannot pass it vacuously. No test proves that `EfInventoryAlertWriter` propagates a 23505 on another constraint.
- [LOW] The new ports `IInventoryAuditEnlister` and `IInventoryAlertWriter` (and R2's `OpenReservations`) are not in the "HO-IMP-001 build amendments" ADR list. They are declared only in commit messages. The integrator should fold them in with the rest. `IInventoryAlertWriter` also lives in a file named after the other interface.

Accepted to backlog by owner instruction (recorded, not re-judged):
- FI-1 §20.3 ratification;
- write-off approval thresholds, count self-approval, and reserved units after a breach quarantine (owner decisions);
- the write-off vs concurrent-transfer race;
- the convention-test blind spots;
- I-A1 on an empty DB and I-F7 (BLOCKED);
- GetBatchUnitCostQuery not built;
- paging gaps;
- InventoryWorker in other areas' test hosts;
- the files outside §13 (now 18).

SCOPE CHECK
- files in allowed scope: 248/266 (`git diff --name-only 50f1ab1..8f915bdf`). §13 paths count together with their ADR-341 partial and module equivalents, as ARCH counted them.
- files outside allowed scope: the 16 listed in HO-IMP-001-ARCHITECTURE.md SCOPE CHECK, plus `src/Domain/CatEcommerce.Domain/DomainServices/IInventoryAuditEnlister.cs` and `tests/CatEcommerce.Infrastructure.Tests/Persistence/InventoryReadSideConventionTests.cs`. None is in §14 (no web/, docs/handover, SRS, .csproj, .sln or .claude; `.ai/` touched only in decisions/adr.md, append-only).
- out-of-scope (§4) work found: none.

UNDECLARED DEVIATIONS
- `EfInventoryAlertWriter` bypasses the repository, so the alert-raised event is not published (LOW above).
- 2 more files outside §13 than the 16 recorded (MEDIUM above).
- The new ports are absent from the build-amendment ADR (LOW above).
