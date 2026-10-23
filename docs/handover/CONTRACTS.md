# CONTRACTS — cross-area seams (Step 1)

This is the index the wave agents read before implementing any area. It exists because an area
often calls a port **owned by another area** that has not been built yet (`docs/handover/README.md`).
Everything listed here is an **interface, DTO, enum, or in-memory/fail-closed fake** — no aggregate,
table, handler, controller or domain logic. `dotnet ef migrations has-pending-model-changes` returns
"No changes" against this branch: nothing here adds an entity.

## The one rule

**Only the owning area may change a contract.** A consumer that needs a different signature does
not edit the port, the DTO or the fake — it stops and reports the gap. When the owning area's real
handover lands, it swaps its own registration in its own module file (see "FAKE until ... lands"
comments below); no consumer's file changes.

## How to read the fakes

- **Fail-closed** — the fake refuses/denies by default, because a false "yes" would let a consumer
  build on an assumption ("stock exists", "this diet is authorised", "there is headroom") that isn't
  true yet. Matches the precedent already set by HO-SHP-001's own deny-all diet stub (ADR-084,
  README C-4).
- **In-memory** — the fake has no business rule to enforce; it just needs to exist so a consumer's
  code compiles and can be exercised in a unit test. Where a port would normally enlist into the
  shared `DbContext` (HO-IMP-001's "enlist without save", HO-BIL-001's push path, HO-NTF-001's
  outbox), the fake cannot truly do that — no aggregate/table for it exists yet, and this pass adds
  none. Each such fake's doc comment says so.

---

## 1. Contract table

| # | Contract | Owner | Consumers (per handovers) | File(s) | Fake | Behaviour |
|---|---|---|---|---|---|---|
| 1 | `IStockLedger` (`PlanAsync`, `ReserveAsync`, `ReleaseAsync`, `ConsumeReservationAsync`, `DeductAsync`, `ReceiveReturnAsync`, `RecordShortfall`, `AdjustAsync`, `GetBranchBalancesAsync`, **`ReserveBatchAsync`, `GetBatchUsabilityAsync`** -- added 2026-10-09, HO-ORD-001 architecture-gate fix, owner decision 2; **`GetDispensaryBatchesAsync`, `GetClinicDispensaryAsync`** -- added 2026-10-09 for HO-PHM-001, additive, neither set renames the other) | HO-IMP-001 §8.8 (ADR-103) | HO-ORD-001, HO-EMR-001, HO-PHM-001, HO-SRG-001, HO-BRD-001 | `src/Domain/CatEcommerce.Domain/DomainServices/IStockLedger.cs` | `Fakes/Inventory/FakeStockLedger.cs` | **Fail-closed** — every mutating/planning method returns `InsufficientStock`; `GetBranchBalancesAsync` and `GetDispensaryBatchesAsync` succeed with an empty list (the truthful read answer); `GetClinicDispensaryAsync` returns `DispensaryNotFound` |
| 2 | `StockDemand`, `StockReference`, `StockParty`, `StockPlan`, `StockMovementLine`, `StockResult<T>`, `StockAdjustment`, `BatchBalance`, `StockReturn`, `StockShortfall`, **`StockBatchDemand`, `BatchUsability`**, `DispensaryBatch`, `DispensaryLocation` (`Models/DispensaryBatch.cs`, HO-PHM-001) (`src/Domain/CatEcommerce.Domain/Models/StockBatchModels.cs`, 2026-10-09) | HO-IMP-001 §8.8/§11.3 | same as #1 | `src/Domain/CatEcommerce.Domain/Models/Stock*.cs` | — (DTOs) | — |
| 3 | `MovementKind`, `StockReferenceKind` | HO-IMP-001 §8.7/§10.1 | same as #1 | `src/Shared/CatEcommerce.Shared/Common/Enums/{MovementKind,StockReferenceKind}.cs` | — (enums) | — |
| 4 | `IDietAuthorisationGate` (`CheckAsync`, `GetAuthorisedCatsAsync`) | HO-PHM-001 §8.10 (ADR-149) | HO-SHP-001, HO-ORD-001, HO-SUB-001 | `src/Domain/CatEcommerce.Domain/DomainServices/IDietAuthorisationGate.cs` | `Fakes/Pharmacy/DenyAllDietAuthorisationGate.cs` | **Fail-closed**, by explicit instruction (README C-4, ADR-084) — `CheckAsync` always `NoAuthorisation`; `GetAuthorisedCatsAsync` always empty |
| 5 | `DietGateResult`, `AuthorisedCat`, `DietGateOutcome` | HO-PHM-001 §8.10/§10.1 | same as #4 | `src/Domain/CatEcommerce.Domain/Models/DietGateResult.cs`, `AuthorisedCat.cs`; `Shared/.../Enums/DietGateOutcome.cs` | — | — |
| 6 | `IFileStore` (`PutAsync`, `OpenReadAsync`, `DeleteAsync`; ADR-370 adds `PutVariantAsync`, `ExistsAsync`) | HO-FIL-001 (ADR-318, ADR-370) | HO-EMR-001, HO-SHP-001, HO-BRD-001, HO-RPT-001 | `src/Domain/CatEcommerce.Domain/DomainServices/IFileStore.cs` | **Landed 2026-09-27 — no fake.** `LocalFileStore` (Development/Testing, filesystem under `%TEMP%`) / `GcsFileStore` (Staging/Production, Google Cloud Storage, ADR-318), selected by `FileStorageModule` | Real: streams to/from disk or GCS; `PutAsync` never buffers the whole file in memory; `OpenReadAsync` returns `FileObjectMissing` for a row with no backing object instead of a silent 404 |
| 7 | `IMalwareScanner` (`ScanAsync`) | HO-FIL-001 (ADR-319, inherited from HO-EMR-001 §8.10) | same as #6 | `src/Domain/CatEcommerce.Domain/DomainServices/IMalwareScanner.cs` | **Landed 2026-09-27 — no fake.** `DevelopmentNoOpScanner` (Development) / `EicarTestScanner` (Testing, deterministic EICAR-string detector) / `ClamAvScanner` (Staging/Production, real `clamd`) / `UnconfiguredMalwareScanner` (Staging/Production with no `clamd` host configured) | **Fail-closed** (ADR-319) outside Development: `UnconfiguredMalwareScanner` always reports `Unavailable`, never `Clean` |
| 8 | `IFileUrlSigner` (`Sign`, `TryVerify`) | HO-FIL-001 §8.6 (inherited from HO-EMR-001 §8.10) | same as #6 | `src/Domain/CatEcommerce.Domain/DomainServices/IFileUrlSigner.cs` | **Landed 2026-09-27 — no fake.** `HmacFileUrlSigner` — HMAC-SHA256 over `{fileId}\|{exp}\|{disp}`, constant-time verification | Real: a tampered or expired signature is refused with 403 and no detail (§8.6) |
| 9 | `FilePutRequest`, `FileStoreResult<T>`, `ScanResult`/`ScanOutcome`, `FilePurpose` | HO-FIL-001 §8.1/§8.3/§10.1 | same as #6 | `src/Domain/CatEcommerce.Domain/Models/{FilePutRequest,FileStoreResult,ScanResult}.cs`; `Shared/.../Enums/FilePurpose.cs` | — | — |
| 10 | `IBillableChargeRecorder` (`RecordAsync`, `VoidAsync`) | HO-BIL-001 §8.4 (ADR-178) | HO-ORD-001, HO-BRD-001, HO-SUB-001 | `src/Domain/CatEcommerce.Domain/DomainServices/IBillableChargeRecorder.cs` | `Fakes/Billing/InMemoryBillableChargeRecorder.cs` | **In-memory** — idempotent per `ChargeSourceKey` in a process-local dictionary; cannot truly enlist (no `ChargeAggregate`/table yet) |
| 11 | `ChargeRequestDto`, `ChargeSourceKey`, `BusinessLine`, `ItemKind`, `ChargeSourceKind`, `BillingGroupKind` | HO-BIL-001 §8.2/§8.3/§8.4/§10.1 | same as #10 | `src/Domain/CatEcommerce.Domain/Models/{ChargeRequestDto,ChargeSourceKey}.cs`; `Shared/.../Enums/{BusinessLine,ItemKind,ChargeSourceKind,BillingGroupKind}.cs` | — | — |
| 12 | `IBoardingBillableSource` (`GetUninvoicedLinesAsync`) | HO-BRD-001 §12.8 P2 | HO-BIL-001 | `src/Domain/CatEcommerce.Domain/DomainServices/IBoardingBillableSource.cs` | `Fakes/Boarding/InMemoryBoardingBillableSource.cs` | **In-memory (truthful)** — always an empty line list; no stay exists yet | **[Replaced by the real implementation, row 50, HO-BRD-001 Phase G.]**
| 13 | `IBoardingVetSpendingPolicy` (`GetHeadroomAsync`) | HO-BRD-001 §8.14/§12.8 E2 | HO-EMR-001, HO-SRG-001 | `src/Domain/CatEcommerce.Domain/DomainServices/IBoardingVetSpendingPolicy.cs` | `Fakes/Boarding/InMemoryBoardingVetSpendingPolicy.cs` | **Fail-closed** — zero headroom, `VetVisitAuthorised = false`, forcing the approval workflow | **[Replaced by the real implementation, row 51, HO-BRD-001 Phase G.]**
| 14 | `IBoardingStayReferenceReader` (`ExistsForCustomer`, `IsInHouse`) | HO-BRD-001 §12.8 P1/I1 | HO-BIL-001, HO-IMP-001 | `src/Domain/CatEcommerce.Domain/DomainServices/IBoardingStayReferenceReader.cs` | `Fakes/Boarding/InMemoryBoardingStayReferenceReader.cs` | **Fail-closed** — both methods always `false` | **[Replaced by the real implementation, row 52, HO-BRD-001 Phase C.]**
| 15 | `BoardingUninvoicedLineDto`, `VetSpendingHeadroomResult`, `BoardingStayChargeLineKind`, `BoardingPaymentReferenceKind` | HO-BRD-001 §8.14/§8.15/§12.8 | same as #12–14 | `src/Domain/CatEcommerce.Domain/Models/{BoardingUninvoicedLineDto,VetSpendingHeadroomResult}.cs`; `Shared/.../Enums/{BoardingStayChargeLineKind,BoardingPaymentReferenceKind}.cs` | — | — |
| 16 | `INotificationOutbox` (`Enqueue`) | HO-NTF-001 §8.4 (ADR-056/057) | every area that sends a message (ACC, ADM, APT, IMP, EMR, PHM, SRG, BIL, ORD, BRD, SUB, RPT) | `src/Domain/CatEcommerce.Domain/DomainServices/INotificationOutbox.cs` | `Fakes/Notifications/InMemoryNotificationOutbox.cs` | **In-memory** — records every request into an in-memory queue for test assertions; cannot truly enlist (no `NotificationRequestAggregate`/table yet) |
| 17 | `NotificationRequestDto`, `NotificationRecipientKind`, `NotificationSubjectKind`, `NotificationChannel`, `NotificationCategory` | HO-NTF-001 §11.3/§10.1 (`NotificationChannel`/`NotificationCategory` owned by HO-ACC-001 §10.1, "reused, never duplicated") | same as #16 | `src/Domain/CatEcommerce.Domain/Models/NotificationRequestDto.cs`; `Shared/.../Enums/{NotificationRecipientKind,NotificationSubjectKind,NotificationChannel,NotificationCategory}.cs` | — | — |
| 18 | `IBusinessSettings` (`GetInt`, `GetDecimal`, `GetBool`, `GetTimeRange`, `GetString` — the fifth method, added by HO-ADM-001 for `SettingType.CurrencyCode`, ADR-054) | HO-ADM-001 §8.9 | HO-ACC-001 (`security.staffIdleTimeoutMinutes`), HO-NTF-001 (`notifications.quietHours`), and every area with a setting (§10.2's Consumer column) | `src/Domain/CatEcommerce.Domain/DomainServices/IBusinessSettings.cs` | `Fakes/Administration/InMemoryBusinessSettings.cs` | **Documented divergence (gate fix 2026-09-26):** the fake returns each type's zero value (`0`, `0m`, `false`, a zero-length `TimeRange`, `""`) for **any** key, including one not in `SettingDefinitions`, and never throws. The real adapter (`CachedBusinessSettings.Resolve`) throws `InvalidOperationException` for a key absent from `SettingDefinitions` — a caller passing a typo'd or not-yet-registered key finds out at first real use, not silently. Left as a documented difference rather than made to match: the fake exists so an area landing before HO-ADM-001 can compile and run without a real settings row seeded for every key it reads, which the throw-on-unknown behaviour would defeat. |
| 19 | `IBranchCalendar` (`OpenIntervalsAsync`) | HO-ADM-001 §8.9 | HO-APT-001, HO-BRD-001 | `src/Domain/CatEcommerce.Domain/DomainServices/IBranchCalendar.cs` | `Fakes/Administration/InMemoryBranchCalendar.cs` | **In-memory (truthful)** — always no open intervals, mirroring the real "closed until hours are set" default |
| 20 | `IActiveBranchAccessor` (`GetActiveBranchId` — returns `ActiveBranchResult { Guid? BranchId, ActiveBranchRefusal? Refusal }`, not a bare `Guid?`, gate fix 2026-09-26) | HO-ADM-001 §8.3/§8.9 (ADR-046) | any staff-facing endpoint needing a branch | `src/Domain/CatEcommerce.Domain/DomainServices/IActiveBranchAccessor.cs` | `Fakes/Administration/NullActiveBranchAccessor.cs` | **In-memory (truthful)** — always `ActiveBranchResult.NoBranch` (`BranchId: null, Refusal: null`), mirroring the real "customer/anonymous → no active branch, not a refusal" case. A refusal (`BranchNotAssigned` — a bad/out-of-assignment `X-Branch-Id` header; `BranchAssignmentRequired` — no assignment and no safe single-branch default) is for the calling controller to turn into a `403`; it is never silently downgraded to "no branch". |
| 21 | `TimeRange`, `TimeInterval`, `Money`, `Unit` | Cross-cutting primitives depended on by #1–20; no single handover owns a file for them | all of the above | `src/Domain/CatEcommerce.Domain/ValueObjects/{Money,TimeRange}.cs`; `src/Domain/CatEcommerce.Domain/Models/TimeInterval.cs`; `src/Shared/CatEcommerce.Shared/Common/Helpers/Unit.cs` | — | — |
| 22 | `Actions`, `ActionRoles` — `ActionRoles.cs` is no longer an empty shell; Accounts built its reflection-based merge engine there in wave A (ADR-341, the same pattern `RecordClassification.cs` uses for tables) | HO-ACC-001 §12.8 (ADR-036) — enforcement built by Accounts in wave A | every area, when it adds its own `Actions.[Area].cs`/`ActionRoles.[Area].cs` | `src/Shared/CatEcommerce.Shared/Common/Constants/{Actions,ActionRoles}.cs` | — (no logic in `Actions.cs`; `ActionRoles.cs` holds `ActionRoles.BuildTable`, real logic, not a shell) | Your own `ActionRoles.[Area].cs` needs exactly one member: a `private static readonly (string Action, string[] Roles)[]` field **whose name ends with the literal word `Rows`** (e.g. `BillingRows`), following `ActionRoles.Accounts.cs`'s `AccountsRows` exactly. `BuildTable` discovers candidates by that name, then checks the shape — a right-shaped field with the wrong name, or a right-named member of the wrong shape (a property, a public field, a `Dictionary`), throws at first use naming the offending member, rather than silently dropping your area's rows and failing every one of its `[RequireAction]` endpoints closed with an unexplained 403. |
| 23 | `StaffRoles` | HO-ACC-001 §10.2 | `ActionRoles` rows everywhere, HO-ADM-001's `RosterableRoles` | `src/Shared/CatEcommerce.Shared/Common/Constants/StaffRoles.cs` | — | — |
| 24 | `SkuRows()` / `SkuRow` (Shop's published SKU projection) | HO-SHP-001 (**Amended 2026-10-09**: `Brand` column, owner-ratified) | HO-PHM-001 (`CatalogueReader`, `SkuPriceReader`) | `src/Infrastructure/CatEcommerce.Infrastructure/Product/SkuRows.cs` | — (real projection) | Read-only, untracked; additive columns only |
| 25 | `SkuSaleFactViewModel` / `SkuRows()` (Shop's SKU read contract; `GetSkuSaleFactsQuery`, HO-SHP-001 §8.12) | HO-SHP-001 (**Amended 2026-10-09, FU3**) | HO-ORD-001 (`ICatalogueLookup` -> `SkuSnapshot.ShippingWeightGrams`), HO-IMP-001 | `src/Read/CatEcommerce.Read/ViewModels/ShopPurchaseViewModels.cs`, `src/Infrastructure/CatEcommerce.Infrastructure/Product/SkuRows.cs` | — | **Owner decision 2026-10-09:** delivery charges are configurable by weight range. The SKU contract now carries `ShippingWeightGrams` (`int?`, null = not set, 1..100000 g), stored on `product.sku.shipping_weight_grams`, set via the staff add/update SKU commands. Appended as a trailing optional member, so existing consumers compile unchanged. |
| 26 | **`IBillingTransactionScope`** (`RunAsync`, `FlushAsync`) -- the producer entry point: one database transaction around the producer's own change and Billing's published commands; **Billing owns the savepoint rule and the per-branch numbering lock** (`DocumentNumbering` reads the series row `FOR UPDATE` inside an open transaction, ADR-179 amended). Added 2026-10-09, ADR-215 accepted by the owner (renumbered from #24 on the 2026-10-10 merge with HO-PHM-001, which took #24/#25) | HO-BIL-001 §8.13 (Amended 2026-10-09) | HO-ORD-001 (via `ITransactionalScope`), **HO-BRD-001, HO-SUB-001 -- use this, write no transaction or lock code of your own** | `src/Domain/CatEcommerce.Domain/DomainServices/IBillingTransactionScope.cs`; `Infrastructure/Billing/BillingTransactionScope.cs` | **Landed 2026-10-09 -- no fake** (registered in `BillingModule`) | Real |
| 27 | **`IBillingRefundPlanner`** (`PlanCreditAsync`, `PlanRefundAsync`) + `CreditPlan`, `CreditLinePlan`, `RefundDrawPlan`, `RefundDraw` -- Billing's planning port for a refund: how an amount is credited over an invoice's lines and drawn from payments. A producer then dispatches `IssueCreditNoteCommand` / `IssueRefundCommand`; it never loads Billing's invoice or payment aggregates or injects `IBillingFinder`. Added 2026-10-09 (renumbered from #25 on the 2026-10-10 merge with HO-PHM-001, which took #24/#25) | HO-BIL-001 §8.13 (Amended 2026-10-09) | HO-ORD-001, HO-BRD-001 (early check-out) | `src/Domain/CatEcommerce.Domain/DomainServices/IBillingRefundPlanner.cs`; `Models/BillingRefundPlanModels.cs`; `Infrastructure/Billing/EfBillingRefundPlanner.cs` | **Landed 2026-10-09 -- no fake** | Real, read-only (`AsNoTracking`) |
| 28 | **`ApprovePaymentCommand` and `RejectPaymentCommand`** published as part of Billing's §8.13 command surface (maker-checker: Billing itself refuses the person who recorded the payment). A producer records the payment as the real first person (`RecordPaymentCommand`), and the real second person approves or rejects through these -- no `UserContext` is ever copied, built or impersonated. Added 2026-10-09 (renumbered from #26 on the 2026-10-10 merge with HO-PHM-001, which took #24/#25) | HO-BIL-001 §8.13 (Amended 2026-10-09) | HO-ORD-001 (held bank transfers), HO-BRD-001 (deposits) | `src/Application/CatEcommerce.Application/Commands/Billing/BillingPaymentCommands.cs` | **Landed -- no fake** | Real |
| 29 | **`RecordPaymentCommand.ApprovalWaiverReason`** (additive `string?`, default `null`; replaces the first-cut `bool ApprovalNotRequired`) -- an in-process producer that has already decided a payment needs no second approver sets `CodCollection` (cash-on-delivery collection, method `CashOnDelivery`) or `CounterTender` (counter tender, method `CashInStore` or `CardInStore`; HO-ORD-001 OQ-7 / ADR-204 amended) and Billing accepts the payment at once, whatever its size. Billing refuses every other method/reason pairing (`400 ApprovalWaiverNotAllowed`, so a bank transfer can never skip the second approver), stores the reason on the payment (`approval_waiver_reason`), raises `PaymentApprovalWaivedEvent` and writes the audit row `PaymentApprovalWaived` (`Reason=..; Method=..; Amount=..`). Never bound from an HTTP request (`StaffPaymentsController` builds the command field by field), so payments staff record in Billing keep the approval threshold (BR-17). **Also additive:** `IssueInvoiceFromChargesCommand.PinnedTaxClassId` / `PinnedTaxRateAsOf` (in-process only) -- that class's tax rate is read as of the given instant instead of now; Ordering pins `DELIVERY` to the order's `PlacedOn` when a held transfer is confirmed. Added 2026-10-10 (HO-ORD-001-FU1 D4, fix round ORD 52/53/56) | HO-BIL-001 §8.13 (additive, owner-confirmed 2026-10-10) | HO-ORD-001 (COD collection, counter tenders, transfer confirmation) | `src/Application/CatEcommerce.Application/Commands/Billing/BillingPaymentCommands.cs`, `.../BillingInvoiceCommands.cs`, `.../CommandHandlers/Billing/PaymentCommandHandlers.cs`, `.../InvoiceIssuing.cs` | **Landed -- no fake** | Real |
| 30 | **`CustomerVisitListItemViewModel.FollowUpOn`** (`DateOnly?`, additive trailing member of `GET /api/customers/me/cats/{catId}/visits`: the visit's follow-up due date, `null` when none) -- owner-delegated decision 2026-10-10, owner to review | HO-EMR-001 (**Amended 2026-10-10**) | HO-POR-001 (`VisitsSectionReader` -> `followUpOn`) | `src/Read/CatEcommerce.Read/ViewModels/DischargeSummaryViewModels.cs`, `QueryHandlers/Visit/DischargeSummaryQueryHandlers.cs` | -- (real) | Date only; no kind, no text. Additive: no rename or removal |
| 31 | **`CustomerOrderSummaryViewModel.TrackingNumber` and `.CanReorder`** (`string?`, `bool`; additive trailing members of `GET /api/customers/me/orders`). `CanReorder` is `CustomerOrderRules.CanReorder(status)`, the one rule the order detail also uses -- owner-delegated decision 2026-10-10, owner to review | HO-ORD-001 (**Amended 2026-10-10**) | HO-POR-001 (`OrdersSectionReader` passes both through), HO-ORD-002 | `src/Read/CatEcommerce.Read/ViewModels/OrderViewModels.cs`, `QueryHandlers/Order/OrderQueryHandlers.cs` | -- (real) | Single source of the re-order eligibility flag; the portal keeps no copy. Additive: no rename or removal |
| 32 | **`IClinicalSkuReader` / `ClinicalSku`** -- HO-EMR-001 §8.12 SHP-1 realised (HO-SHP-001 published no `ISkuReader`, no stock unit and no controlled flag). One Domain port, one Infrastructure adapter `Consultation/ClinicalSkuReader` composing: `ISkuDirectory` (name, active, unit, fractional), Shop's published `SkuRows()` (tax class) and `IControlledDrugDirectory.FindBySkuAsync` (the controlled flag; OQ-12 re-answered). Owner decision 2026-10-13 "Ratify the adapter" and (B) "Yes, formulary flag" | HO-EMR-001 (**Amended 2026-10-13**) | HO-EMR-001 (Phase F) | `src/Domain/CatEcommerce.Domain/DomainServices/IClinicalSkuReader.cs`, `Models/ClinicalStockModels.cs`; `src/Infrastructure/CatEcommerce.Infrastructure/Consultation/ClinicalSkuReader.cs` | -- (real) | The unit is Inventory's `each`, whole units (HO-IMP-001 OQ-19). A SKU is a controlled drug when a formulary medicine flagged controlled is linked to it; a controlled product with no formulary link is not refused (same rule as Inventory). Backlog EMR: report unlinked dispensary SKUs (LOW) |
| 33 | **HO-EMR-001 is a consumer of four ports it does not own**, ratified by the owner 2026-10-13 (the CONTRACTS §5 "a third area does not call these" rule is waived for these four uses, each by its owner's published shape): `ISkuDirectory` (HO-IMP-001) -- name, unit; `ISkuPriceReader.PriceAtAsync` (HO-PHM-001, over HO-SHP-001's `PriceRows`) -- the SKU price at an instant; `IBillingPricingInputs.GetServicePriceAsync` and, from Phase J, `.GetServiceTaxClassIdAsync` for diagnostic orders (HO-BIL-001, a read-only view of HO-ADM-001's tables; HO-ADM-001 publishes no service-price-at-t port) -- the service price and tax class at an instant; `IControlledDrugDirectory.FindBySkuAsync` (HO-PHM-001). Alternative left open: HO-ADM-001 publishes a service-price-at-t port and EMR switches to it | HO-IMP-001, HO-PHM-001, HO-BIL-001 | HO-EMR-001 (Phase F) | `src/Application/CatEcommerce.Application/CommandHandlers/Visit/TreatmentHandlers.cs`, `src/Read/CatEcommerce.Read/QueryHandlers/Visit/TreatmentQueryHandlers.cs` | -- | Read-only uses; no foreign file edited |
| 34 | **`IStockLedger.GetDispensarySkuIdsAsync(branchId, ct)`** -- additive, recorded here (HO-EMR-001 Phase F gate fix, ARCH 41; owner-directed): the distinct SKUs with stock on hand at the branch's active clinic dispensary, so a consumer lists dispensary items without reading Inventory's positions and locations. Declared as a **default interface member** (answers `NotSupported`), so no other implementer changes; `EfStockLedger` overrides it. ratified by the integrator 2026-10-13 in HO-IMP-001 §1 | HO-IMP-001 §8.8 (Amended 2026-10-13, ratified by integrator) | HO-EMR-001 (`GET /api/staff/dispensary/items`) | `src/Domain/CatEcommerce.Domain/DomainServices/IStockLedger.cs`; `src/Infrastructure/CatEcommerce.Infrastructure/Inventory/EfStockLedger.Consumer.cs` | the default member | Read-only |
| 35 | **`GET /api/staff/customers/{customerId}/orders`** (`Orders.View`; `StaffCustomerOrderRowViewModel` rows, paged). Specified in HO-ORD-001 §12.4 but not built; **delivered 2026-10-12** by HO-ORD-001-FU2 (owner decision "Build the endpoint"). Active-branch scope; unknown customer `404` | HO-ORD-001 (**Amended 2026-10-12**) | HO-ORD-002 (`CustomerOrdersPanel`, HO-ACC-002's customer page) | `src/Api/CatEcommerce.Api/Controllers/StaffCustomerOrdersController.cs`, `src/Read/CatEcommerce.Read/QueryHandlers/Order/StaffOrderQueryHandlers.cs` | -- (real) | Delivered. See `docs/handover/HO-ORD-001-FU2.md` |
| 36 | **`IVisitBillingReader` / `VisitBillingFacts` / `VisitBillingLine`** -- HO-EMR-001 owns, HO-BIL-001 Phase J consumes (owner's architecture call 2026-10-14 on backlog EMR 43). A Domain-level read port with typed values (`Money`, `decimal`, `VisitBillableLineKind`, nullable tax class and customer) returning a visit's billable lines: billable, not reversed, priced treatment lines (`treatment:{id}`) and non-cancelled, priced diagnostic orders (`diagnostic:{id}`). Billing's **capture** never calls the Read-layer `GetVisitChargeLinesQuery` and never reads `clinic.*`; only the Read-layer `GetUncapturedSourcesQuery` list counts the same tables (a cross-context list, bounded-contexts.md). EMR kinds map to Billing's in `VisitBillableLineKindMap` (Procedure→Service, Medicine→Medicine, Consumable→Product, Diagnostic→Service), exhaustive and tested | HO-EMR-001 (**Amended 2026-10-14**) | HO-BIL-001 (Phase J) | `src/Domain/CatEcommerce.Domain/DomainServices/IVisitBillingReader.cs`, `Models/VisitBillingModels.cs`, `DomainServices/VisitBillableLineKindMap.cs`, `Shared/.../Enums/VisitBillableLineKind.cs`; `src/Infrastructure/CatEcommerce.Infrastructure/Consultation/VisitBillingReader.cs` | -- (real; fake the port) | Read-only. A visit not finalised returns `IsFinalised = false` and no lines. `CustomerId` is null on a visit finalised before Phase J |
| 37 | **`VisitFinalisedEvent.CustomerId`** (`Guid`, required; the cat's owner at the moment of finalising) and **`clinic.visit.customer_id`** (nullable `uuid`), owner decision 1 of 2026-10-14. Stored once by `VisitAggregate.Finalise`, never changes. The event has **no** `BoardingStayId` (deferred to HO-BRD-001, owner decision 3) | HO-EMR-001 (**Amended 2026-10-14**) | HO-BIL-001 (Phase J) | `src/Domain/CatEcommerce.Domain/Events/Visit/VisitEvents.cs`, `Aggregates/Visit/VisitAggregate.cs`, `Models/VisitDtos.cs`; migration `AddVisitBillingFacts` | -- | Narrows ADR-117 (ADR-344). Owner-facing reads still use the cat's current owner |
| 38 | **Diagnostic test prices**: settings `clinical.diagnosticPrice{BloodWork,Urinalysis,FelvFivTest,Imaging,Other}` (Decimal, default `0.00`, group Clinical) and `clinic.diagnostic_order.{unit_price_amount, unit_price_currency_code, tax_class_id}` (nullable), `DiagnosticPricing.SettingKeyFor(kind)`; owner decision 2 of 2026-10-14. The order freezes the price and the visit service's tax class when it is made (ADR-345) | HO-EMR-001 (**Amended 2026-10-14**) | HO-BIL-001 (Phase J, through row 36); HO-EMR-001 itself consumes `IBusinessSettings` and `IBillingPricingInputs.GetServiceTaxClassIdAsync` | `src/Shared/.../Constants/{SettingKeys,SettingDefinitions}.Consultation.cs`, `DiagnosticPricing.cs`; `Aggregates/Visit/DiagnosticOrderAggregate.cs`; `CommandHandlers/Visit/DiagnosticHandlers.cs` | -- | An order made before this has no price and is not billed |
| 39 | **HO-BIL-001 Phase J is a consumer of events and rows it does not own**: `VisitFinalisedEvent` (EMR), `PrescriptionDispensedEvent` and `DispensingReversedEvent` (PHM; the source id is `DispensingId`; owner-ratified void), `SurgeryDischargedEvent` (SRG -- the "SurgeryCompletedEvent" of §8.13 does not exist); the dispensing's and the surgery's own rows through untracked projections (PHM and SRG publish no typed billing port, as the appointment source reads appointments); `ISkuPriceReader.PriceAtAsync` (PHM) for a surgery drug's price at the time it was given; Shop's `SkuRows()` for a SKU's tax class, through the new `IBillingPricingInputs.GetSkuTaxClassIdsAsync`. Billing also reads `account.customer_account.merged_into_customer_id` (the merge chain) and the branch time zone, and its Read-layer uncaptured list counts PHM, SRG and clinic rows; Billing adds `IBillingFinder.FindChargeIdsBySourceAsync` (its own) | HO-EMR-001, HO-PHM-001, HO-SRG-001, HO-SHP-001 | HO-BIL-001 (Phase J) | `src/Infrastructure/CatEcommerce.Infrastructure/Billing/ItemSources/{Visit,Dispense,Surgery}BillableItemSource.cs`; `src/Application/CatEcommerce.Application/DomainEventHandlers/Billing/CaptureChargesOnPhaseJSources.cs` | -- (real) | A line with no recorded price is skipped and reported `SkuNotPriced`; a reversed supply voids its charges not yet invoiced (ADR-346) |
| 40 | **`SurgeryAggregate.OwnerAtDischargeId` / `clinic.surgery.owner_at_discharge_id` (nullable `uuid`) and `SurgeryDischargedEvent.OwnerAtDischargeId` (`Guid?`)** -- owner decision 2026-10-14, "Owner at discharge": the cat's owner when the surgery was discharged, stored once by `Discharge`; `SurgeryDischargeDto` gained a trailing optional `OwnerAtDischargeId`. `CustomerId` (booking customer) is unchanged | HO-SRG-001 (**Amended 2026-10-14**) | HO-BIL-001 (Phase J) | `src/Domain/CatEcommerce.Domain/Aggregates/Surgery/SurgeryAggregate.cs`, `Events/Surgery/SurgeryEvents.cs`, `Models/SurgeryDtos.cs`; migration `AddVisitBillingFacts` | -- | Null on a surgery discharged before it existed: not billable (`SurgeryCustomerNotRecorded`), billed by hand |
| 41 | **Reporting `varianceDirection`** (HO-RPT-001-FU1; owner decision 2026-10-14 "Over / Short"). A string column `varianceDirection` (`ReportColumnType.Text`) placed right after `variance` in **R13 `payment-reconciliation`** and **R14 `daily-cash-up`**; values `"Over"`, `"Short"`, `"Matched"` or `null` (nothing to compare: card in store, a cash day not yet counted, a day not closed); enum `VarianceDirection` + `VarianceDirections.Of/Name` in `src/Shared/CatEcommerce.Shared/Common/Enums/VarianceDirection.cs`. `Over` = more received or counted than expected, `Short` = less, `Matched` = equal to the cent. **Not derived from the sign of `variance`**: R13 COD and bank-transfer rows carry expected minus received (positive = short), R13's cash row and all of R14 carry counted minus expected (negative = short); `variance` is unchanged. Also additive: an optional trailing `description` on every column of the catalogue (`GET /api/staff/reports`, `columns[]`) and of the run result (`columns[]`), `null` except for `varianceDirection`, where it states the meaning. Neither report has a totals row | HO-RPT-001 (**Amended 2026-10-14**) | HO-RPT-002 (`lib/format/report.ts` `isNonZeroAmount` / `isVarianceKey`; RPT backlog row 71) | `src/Read/CatEcommerce.Read/QueryHandlers/Reporting/Sources/PaymentReportBuilders.cs`, `src/Shared/CatEcommerce.Shared/Common/Constants/ReportCatalogue.Financial.cs` | -- (real) | Delivered. See `docs/handover/HO-RPT-001-FU1.md` |
| 50 | **`IBoardingBillableSource`** real implementation `BoardingBillableSource` (HO-BRD-001 Phase G) -- replaces the empty fake of #12. Returns the stay's active, uninvoiced Billing charges. Merged Billing PULLS nothing from it (mismatch 2, HO-BRD-001 §1.1 Amended); it exists for any consumer that asks the original question. | `Infrastructure/Boarding/BoardingBillingReaders.cs` |
| 51 | **`IBoardingVetSpendingPolicy`** real implementation `BoardingVetSpendingPolicy` -- replaces the fail-closed fake of #13. `limit = (authorised ? limit : 0) + approved increases`, `headroom = max(0, limit - committed)`; an unknown stay or cat stays fail-closed (zero). | same file |
| 52 | **`IBoardingStayReferenceReader`** real implementation `BoardingStayReferenceReader` -- replaces the fail-closed fake of #14. `ExistsForCustomer(BoardingStay\|BoardingStayDeposit, id, customerId)` is true when the customer owns the stay; `DaycareVisit` ids are NOT validated yet (Billing does not call it today); `IsInHouse` is true while the stay is `CheckedIn` with the cat. | `Infrastructure/Boarding/BoardingStayReferenceReader.cs` |
| 53 | **`IBoardingDepositLedger`** (`BillingDepositLedger`) -- Boarding's READ-ONLY view of Billing payments earmarked `{BoardingStay, stayId}`: accepted and pending-approval amounts. | `Infrastructure/Boarding/BillingDepositLedger.cs` |
| 54 | **`IBoardingChargeLedger`** (`BoardingChargeLedger`) -- read-only view of the stay's Billing charges (line ref, voided, billed, net) and of the stays due a cycle or final invoice. Charges are written only through `IBillableChargeRecorder` in Boarding handlers. | `Infrastructure/Boarding/BoardingBillingReaders.cs` |
| 55 | **Events HO-EMR-001 / HO-BIL-001 / HO-SRG-001 raise or subscribe to**: `BoardedCatReferredToClinicEvent` (Boarding raises; EMR opens the visit), `ReferralVisitOpenedEvent` (EMR raises), `BoardingVetChargeRecordedEvent` (EMR/BIL/SRG raise per clinic charge on a boarded cat), `VetChargeApprovalDecidedEvent` (Boarding raises; EMR holds treatment until it). No event carries text. SRG's `SurgeryScheduledEvent`/`SurgeryRescheduledEvent`/`SurgeryDischargedEvent` are subscribed by Boarding (US-SRG-09). | `Domain/Events/Boarding/BoardingClinicEvents.cs` |
| 56 | **`DaycareVisitCheckedOutEvent` and the `DaycareVisit` charge source** -- a daycare check-out pushes one charge (source `DaycareVisit`, business line Boarding) in the same save and raises the event. | same file; `CheckOutDaycareVisitCommandHandler` |
| 57 | **XCT registrations**: `IRetentionPurgeSource` `boarding-media` (`BoardingMediaPurgeSource`) and `IPersonalDataEraser` `boarding` (`BoardingPersonalDataEraser`: holds the stay as a D-9 financial record for `retention.financialYears`). `ErasureCoverageConventionTests.ContextOfFolder` maps `Boarding` -> `boarding` (a cross-area edit). | `Infrastructure/Boarding/BoardingMediaPurgeSource.cs`, `Infrastructure/Retention/Erasers/BoardingPersonalDataEraser.cs` |
| 58 | **`FilePurposeAccess`** staff mapping for the two boarding purposes HO-FIL-001 pre-declared but left unmapped (fail-closed refused every role): `BoardingMedia` -> `Boarding.RecordCare`, `BoardingBelongings` -> `Boarding.CheckInOut` (cross-area edit). | `Shared/Common/Constants/FilePurposeAccess.cs` |
| 59 | **`GET /api/staff/boarding/stays/{id}/check-out-preview`** (`Boarding.CheckInOut`) -- owner-authorised 2026-10-14 ("Show before"). `{ isEarly, nightsBooked, nightsCharged, quote?, lines:[{lineRef,description,net,action: Keep\|Void\|Record}], credit, creditOutcome: None\|Credit\|Refund, today, bookedDepartureOn, chargedDepartureOn, unitFreeFrom, previousTotal, newTotal }`. Same calculation as the check-out. | `Read/QueryHandlers/Boarding/BoardingDepartureQueryHandlers.cs` |

Module registrations (all "FAKE until ... lands" — see each file's own comment):
`src/Api/CatEcommerce.Api/Modules/{InventoryModule,PharmacyModule,FileStorageModule,BillingModule,BoardingModule,NotificationsModule,AdministrationModule}.cs`.

Unit tests for every fake's behaviour: `tests/CatEcommerce.Infrastructure.Tests/Fakes/**`.

---

## 2. The identity header

**`X-CSRF-Token`** (HO-ACC-001 §12.5, ADR-031; enforced at §12.8 rule R7).

The caller's *identity* itself is never carried in a request header the frontend sets — it rides in
two `HttpOnly` session cookies the browser sends automatically (`pc_session` for a customer,
`pc_staff_session` for staff; ADR-031), resolved server-side into `UserContext` by
`SessionAuthenticationMiddleware` / `SessionUserContextProvider` (§12.5). Neither cookie is readable
or settable by client-side script.

The one header the frontend's API client **must** set itself is the CSRF double-submit token: read
the non-`HttpOnly` `pc_csrf` cookie and echo its value in the `X-CSRF-Token` header on every unsafe
method (`POST`/`PUT`/`DELETE`). Missing or mismatched → `403 { "error": "csrf_token_invalid" }`
before the action runs (§12.8 R7). Sign-in, registration, CAPTCHA and reset endpoints carry no
session and are exempt.

`HO-ADM-001` additionally defines a **branch-context** header, `X-Branch-Id` (§8.3, ADR-046) — sent
by a staff client to select which of several assigned branches is active for the request. This is
not an identity header (it does not authenticate anyone); it is reported here because it is the
other header a staff-facing API client needs to know about.

---

## 3. Disagreements found (owner wins per the rule above)

| # | Where | Disagreement | Resolution |
|---|---|---|---|
| D-1 | `StockReference`'s field names | HO-IMP-001 §8.8's own table: `ReferenceKind`, `ReferenceId`, `ReferenceLineId`, `ReferenceLabel`. HO-PHM-001 §8.12's inline code sample: `Kind`, `ReferenceId`, `ReferenceLineId`, `Label` | Used the owner's (HO-IMP-001) names. `src/Domain/CatEcommerce.Domain/Models/StockReference.cs` |
| D-2 | `StockReferenceKind` member names vs. what consumers' inline examples call their own reference kind | HO-PHM-001 says `Dispensing`; HO-EMR-001 says `VisitTreatment`/`VisitTreatmentReversal`; HO-SRG-001 says `SurgeryConsumable`; HO-ORD-001 says `Order`. HO-IMP-001 §10.1's own pre-declared list has `Prescription`, `Visit`, `Surgery`, `CustomerOrder` instead | Used the owner's (HO-IMP-001 §10.1) member list verbatim. `src/Shared/CatEcommerce.Shared/Common/Enums/StockReferenceKind.cs`. Each consumer will need to use the owner's actual member name when it is built, not the name its own handover's prose used |
| D-3 | HO-PHM-001 §10.1 calls the movement-kind enum `StockMovementKind`; HO-IMP-001 §10.1 (the owner) calls it `MovementKind` | Naming slip in the consumer's prose, not a structural conflict | Used `MovementKind` (owner) |
| D-4 | HO-IMP-001 §10.1: "the 14 of §8.7"; §8.7's own table lists 13 members | Documentation inconsistency inside the owning handover itself | Implemented the 13 members §8.7's table actually lists; did not invent a 14th |
| D-5 | HO-PHM-001 §11.4 C7 needs `IStockLedger.AdjustAsync(..., StockReference(ControlledDrugCorrection, entryId), ...)`, but `ControlledDrugCorrection` has no matching `StockReferenceKind` member anywhere, including the owner's pre-declared list | **Gap**, not resolved here — left out per the "don't invent a signature" rule. HO-IMP-001 must add the member when it lands, or HO-PHM-001 must map the correction onto an existing kind |

---

## 4. Gaps — no handover gives the exact shape (inferred, flagged in each file's doc comment)

| # | Type | Only evidence available | Where |
|---|---|---|---|
| G-1 | `StockReturn` | HO-IMP-001 §8.8 rule 8's prose ("writes `Return` into the branch's `Quarantine` location only, for a batch the party actually received") and its bare mention in §11.3's DTO list — no field list anywhere | `Models/StockReturn.cs` |
| G-2 | `StockShortfall` | Only HO-EMR-001 §8.6 step 5's literal, EMR-specific call (`SkuId, LocationId, Quantity, VisitId, LineId`); generalised to `ReferenceId`/`ReferenceLineId` so HO-PHM-001/HO-SRG-001 can also call it | `Models/StockShortfall.cs` |
| G-3 | `StockPlan.Shortfall` | Inferred from `FefoAllocator.Allocate(...) → Allocation(lines[], shortfall)` (HO-IMP-001 §8.9); `StockPlan` itself is only described as "lines of ..." | `Models/StockPlan.cs` |
| G-4 | `AuthorisedCat` | HO-PHM-001 §8.10 names the method (`GetAuthorisedCatsAsync`) and its purpose, never its return record's fields | `Models/AuthorisedCat.cs` |
| G-5 | `FilePutRequest` | Inferred from ADR-318's description of `GcsFileStore.PutAsync` and HO-FIL-001 §8.1's `StoredFileAggregate` fields | `Models/FilePutRequest.cs` |
| G-6 | `FileStoreResult<T>` | ADR-318's code block shows only `Task<FileStoreResult<T>>`, never the wrapper's own fields; modelled on `StockResult<T>`'s already-established shape | `Models/FileStoreResult.cs` |
| G-7 | `IMalwareScanner.ScanAsync` signature, `ScanResult` | Only the three-outcome contract (`Clean`/`Infected`/`Unavailable`) and the calling order (ADR-319, HO-FIL-001 §8.2 step 5) are given, never the method signature | `DomainServices/IMalwareScanner.cs`, `Models/ScanResult.cs` |
| G-8 | `IFileUrlSigner.Sign` signature | Only the resulting URL's shape is given (HO-FIL-001 §8.6), never the method name/parameters | `DomainServices/IFileUrlSigner.cs` |
| G-9 | `VetSpendingHeadroomResult` | Inferred from HO-BRD-001 §8.14's prose formula (`limit`, `committed`, `headroom = limit − committed`, `VetVisitAuthorised`) | `Models/VetSpendingHeadroomResult.cs` |
| G-10 | `TimeRange`, `TimeInterval` | HO-ADM-001 §8.5/§8.9 name the types and describe them only as "any" bounds / "instants (`DateTimeOffset`), sorted, merged" | `ValueObjects/TimeRange.cs`, `Models/TimeInterval.cs` |
| G-11 | `BoardingUninvoicedLineDto.Kind`'s type | HO-BRD-001 §12.8 P2 says the line carries a `Kind` without naming its type; assumed it reuses §8.15's `StayChargeLine.Kind` (`BoardingStayChargeLineKind`) since the fields otherwise match exactly | `Models/BoardingUninvoicedLineDto.cs` |

None of these gaps required adding an entity, table or migration. If a real implementation needs a
different shape, that is the owning area's call to make when it lands — these contracts exist only
to unblock compilation and testing in the meantime.

---

## 5. Deliberately out of scope

Ports that are internal to one area's own consumption of another (not published *for* other areas)
were left out, per the task's "at minimum" list plus README's contract table — e.g. `ISkuDirectory` /
`SkuStockProfile` (HO-IMP-001's own adapter over HO-SHP-001, not something a third area calls),
`ICatalogueReader` / `ISkuPriceReader` (HO-SHP-001 → HO-PHM-001, same shape of relationship),
`IInventoryLedgerWriter` (HO-IMP-001 §8.8: "used only by this context's own handlers"). If a wave
agent finds it actually needs one of these before its owner lands, that is a new contract to report,
not something to build unreviewed.

`FileRules` (HO-FIL-001 §8.3, the allow-list table) was left for FIL itself — it is domain
configuration data, not a seam another area calls through.

---

## § Cross-cutting duties — every area, every table (HO-XCT-001)

HO-XCT-001 (Cross-cutting) is built last, but four of its contracts exist **now** so that every
area landing before it can meet duties C-ALL-1…4 (HO-XCT-001 §5.2) in the same change that adds
a table, instead of retrofitting them once Cross-cutting merges:

| # | Contract | File(s) | HO-XCT-001 section |
|---|---|---|---|
| 1 | `RecordClassification` (registry), `TableClassification` (row) | `src/Shared/CatEcommerce.Shared/Common/Constants/RecordClassification.cs` — one partial file per area, `RecordClassification.[Area].cs` (ADR-341); `RecordClassification.CatProfiles.cs` classifies Cat Profiles' tables | §8.2/§10.3, ADR-298 |
| 2 | `RecordClass`, `HardDeletePolicy` (enums) | `src/Shared/CatEcommerce.Shared/Common/Enums/{RecordClass,HardDeletePolicy}.cs` | §8.2/§10.1 |
| 3 | `IPersonalDataEraser`, `ErasureOutcome` (`Completed`/`Held`/`Failed`) | `src/Domain/CatEcommerce.Domain/DomainServices/IPersonalDataEraser.cs`, `src/Domain/CatEcommerce.Domain/Models/ErasureOutcome.cs` | §8.8, ADR-302/ADR-316 |
| 4 | `IRetentionPurgeSource`, `PurgeResult` | `src/Domain/CatEcommerce.Domain/DomainServices/IRetentionPurgeSource.cs`, `src/Domain/CatEcommerce.Domain/Models/PurgeResult.cs` | §8.9, ADR-304 |
| 5 | `IRetentionReviewSource`, `RetentionReviewCount` | `src/Domain/CatEcommerce.Domain/DomainServices/IRetentionReviewSource.cs`, `src/Domain/CatEcommerce.Domain/Models/RetentionReviewCount.cs` | §8.10 |

Unlike the contracts in §1 above, **there is no fake to register** for these — each area provides
its own real implementation directly (there is nothing to stub: classifying a table and erasing a
context's own data are that area's own knowledge, not a placeholder for someone else's). The
retention worker and purge/erasure orchestration that *consume* these ports are HO-XCT-001's own
later build, not part of this contracts pass.

**The rule every area follows, in the same change that adds a table** (HO-XCT-001 §5.2):

- **C-ALL-1** — classify every table you add in your own `RecordClassification.[Area].cs`, under
  §8.2's ordered rules (care given to a cat → `Clinical`; money owed/paid/refunded or a price/tax
  in force → `Financial`; named by law → `Regulatory`; an owned collection an aggregate method
  replaces wholesale → `CurrentState`; anything else that accumulates history → `Operational`).
  Never edit another area's `RecordClassification.[Area].cs` file, and never re-classify a table
  another area's file already covers — `RecordClassification`'s aggregation throws on a
  duplicate `(schema, table)` pair.
- **C-ALL-2** — if your context stores a customer's personal data beyond the id, register an
  `IPersonalDataEraser` (`ContextKey` stable once chosen) in your own module. If it stores none,
  exempt it instead with a one-line reason (in `ErasureExemptions`, built by HO-XCT-001 itself).
- **C-ALL-3** — if your context is clinical or financial, provide an `IRetentionReviewSource`
  (clinical: `retention.clinicalYears` after the cat's last visit or death, D-8; financial:
  `retention.financialYears` after the document date, D-9).
- **C-ALL-4** — no method on a clinical, financial or regulatory aggregate removes the record, and
  no owned child that is itself a record; a correction is a new entry (D-5).
- **C-ALL-5** (integrator, 2026-09-26, HO-ADM-001 gate) — if your handlers write
  `AuditEntryAggregate` rows about clinical records, register every `EntityType` string they write
  in your own `ClinicalAuditEntityTypes.[Area].cs`
  (`src/Shared/CatEcommerce.Shared/Common/Constants/`), in the same change that adds the audit
  write. The audit log (HO-ADM-001) redacts before/after values and `Reason` for those types unless
  the caller holds `Cats.View` (owner decision 2026-09-26); an unregistered type is shown in full to
  an Accountant. Every table your `RecordClassification.[Area].cs` marks `RecordClass.Clinical`
  needs its audited entity type(s) here too. `ClinicalAuditEntityTypes.CatProfiles.cs` is the
  example. HO-EMR-001, HO-PHM-001 and HO-SRG-001 are known to owe this.

Gaps found while classifying Cat Profiles' own tables (§10.3 already gives every one of them, so
there is no gap in Cat Profiles itself): `audit.audit_entry` is mapped in the current EF model
(Cat Profiles writes `AuditEntryAggregate` rows directly, ahead of HO-ACC-001) but is an
Accounts-context table per §10.3 ("Audit | Forbidden (HO-ACC-001's triggers, F33) | D-5"). It is
deliberately left **unclassified** here so HO-ACC-001's own `RecordClassification.Accounts.cs`
does not collide with Cat Profiles' file — an intentional, temporary gap in
`RecordClassification.All` until Accounts lands.

---

## 6. What remains

- Every fake above is replaced by its owner in its owner's own module file, per the "FAKE until ...
  lands" comment on each registration. No consumer's file changes when that happens.
- `docs/handover/README.md`'s "Open conflicts between written handovers" table (C-1…C-8) is
  unaffected by anything here — those were resolved before this pass started.
- `.ai/flows/batch-recall-flow.md` and `.ai/flows/prescription-diet-flow.md` were corrected to match
  the ledger-stamp / `IDietAuthorisationGate` design (see each file's own "Updated 2026-09-25" note).
